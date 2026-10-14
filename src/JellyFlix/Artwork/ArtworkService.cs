using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using JellyFlix.Artwork.Api.Models;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace JellyFlix.Artwork;

/// <summary>
/// Lets each profile pick its own poster and backdrop for a title, without changing them for anyone else.
/// </summary>
public class ArtworkService
{
    private const int MaxRemoteOptions = 60;
    private static readonly TimeSpan _remoteCacheTime = TimeSpan.FromMinutes(15);

    private readonly ILibraryManager _libraryManager;
    private readonly IProviderManager _providerManager;
    private readonly IImageProcessor _imageProcessor;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ArtworkStore _store;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ArtworkService> _logger;
    private readonly ConcurrentDictionary<(Guid, ImageType), (DateTimeOffset At, IReadOnlyList<RemoteImageInfo> Images)> _remote = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ArtworkService"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="providerManager">The provider manager.</param>
    /// <param name="imageProcessor">The image processor.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="store">Where choices are kept.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="logger">The logger.</param>
    public ArtworkService(
        ILibraryManager libraryManager,
        IProviderManager providerManager,
        IImageProcessor imageProcessor,
        IHttpClientFactory httpClientFactory,
        ArtworkStore store,
        TimeProvider timeProvider,
        ILogger<ArtworkService> logger)
    {
        _libraryManager = libraryManager;
        _providerManager = providerManager;
        _imageProcessor = imageProcessor;
        _httpClientFactory = httpClientFactory;
        _store = store;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Gets the images a profile sees instead of the library's, by title.
    /// </summary>
    /// <param name="user">The profile.</param>
    /// <returns>Paths under the server, by title id.</returns>
    public Dictionary<string, TitleArtworkDto> GetOverrides(User user)
    {
        var result = new Dictionary<string, TitleArtworkDto>(StringComparer.Ordinal);
        foreach (var (itemId, artwork) in _store.GetAll(user.Id))
        {
            var item = _libraryManager.GetItemById(itemId);
            if (item is null)
            {
                continue;
            }

            var dto = new TitleArtworkDto
            {
                Poster = GetPath(item, ArtworkKind.Poster, artwork.Poster),
                Backdrop = GetPath(item, ArtworkKind.Backdrop, artwork.Backdrop)
            };
            if (dto.Poster is not null || dto.Backdrop is not null)
            {
                result[itemId.ToString("N")] = dto;
            }
        }

        return result;
    }

    /// <summary>
    /// Lists the images a profile can choose from: the library's own, then the metadata providers'.
    /// </summary>
    /// <param name="user">The profile.</param>
    /// <param name="itemId">The title.</param>
    /// <param name="kind">Poster or backdrop.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The options.</returns>
    public async Task<ArtworkOptionsDto> GetOptionsAsync(User user, Guid itemId, ArtworkKind kind, CancellationToken cancellationToken)
    {
        var item = GetItem(user, itemId);
        var choice = _store.Get(user.Id, item.Id, kind);
        var options = GetLibraryOptions(item, kind, choice);

        var providersFailed = false;
        try
        {
            var remote = await GetRemoteImagesAsync(item, kind, cancellationToken).ConfigureAwait(false);
            options.AddRange(remote.Select(image => new ArtworkOptionDto
            {
                Id = ArtworkFiles.RemoteId(image.Url),
                PreviewUrl = string.IsNullOrEmpty(image.ThumbnailUrl) ? image.Url : image.ThumbnailUrl,
                Source = image.ProviderName,
                Width = image.Width,
                Height = image.Height,
                Language = image.Language,
                IsSelected = choice?.SourceUrl == image.Url
            }));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Offline servers still get the library's own images
            _logger.LogWarning(ex, "Couldn't list alternative artwork for {Item}", item.Name);
            providersFailed = true;
        }

        if (!options.Any(o => o.IsSelected))
        {
            var library = options.FirstOrDefault(o => o.IsDefault);
            if (library is not null)
            {
                library.IsSelected = true;
            }
        }

        return new ArtworkOptionsDto { Options = options, ProvidersFailed = providersFailed };
    }

    /// <summary>
    /// Chooses an image for a profile. Images from providers are downloaded, so they keep working.
    /// </summary>
    /// <param name="user">The profile.</param>
    /// <param name="itemId">The title.</param>
    /// <param name="kind">Poster or backdrop.</param>
    /// <param name="optionId">The option, from <see cref="GetOptionsAsync"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task ChooseAsync(User user, Guid itemId, ArtworkKind kind, string optionId, CancellationToken cancellationToken)
    {
        var item = GetItem(user, itemId);
        if (optionId.StartsWith(ArtworkFiles.LibraryPrefix, StringComparison.Ordinal))
        {
            if (!int.TryParse(optionId.AsSpan(ArtworkFiles.LibraryPrefix.Length), out var index) || GetLibraryImage(item, kind, index) is null)
            {
                throw new ArtworkException("That image isn't in the library any more.");
            }

            // The library's first image is what everyone sees anyway
            _store.Set(user.Id, item.Id, kind, index == 0 ? null : new ArtworkChoice { ImageIndex = index });
            return;
        }

        // Only images the providers offer for this title can be chosen: never an address from the request
        var remote = await GetRemoteImagesAsync(item, kind, cancellationToken).ConfigureAwait(false);
        var image = remote.FirstOrDefault(i => ArtworkFiles.RemoteId(i.Url) == optionId)
            ?? throw new ArtworkException("That image isn't offered for this title.");
        var file = await DownloadAsync(image.Url, cancellationToken).ConfigureAwait(false);
        _store.Set(user.Id, item.Id, kind, new ArtworkChoice { File = file, SourceUrl = image.Url });
        RemoveUnusedFiles();
    }

    /// <summary>
    /// Goes back to the library's image for a profile.
    /// </summary>
    /// <param name="user">The profile.</param>
    /// <param name="itemId">The title.</param>
    /// <param name="kind">Poster or backdrop.</param>
    public void Reset(User user, Guid itemId, ArtworkKind kind)
    {
        _store.Set(user.Id, itemId, kind, null);
        RemoveUnusedFiles();
    }

    /// <summary>
    /// Gets the path of a downloaded image.
    /// </summary>
    /// <param name="name">The file name.</param>
    /// <returns>The path, or null when there's no such image.</returns>
    public string? GetImagePath(string name)
    {
        if (!ArtworkFiles.IsValidFileName(name))
        {
            return null;
        }

        var path = Path.Combine(_store.ImagesPath, name);
        return File.Exists(path) ? path : null;
    }

    private static ImageType ToImageType(ArtworkKind kind) => kind == ArtworkKind.Poster ? ImageType.Primary : ImageType.Backdrop;

    private static ItemImageInfo? GetLibraryImage(BaseItem item, ArtworkKind kind, int index)
    {
        var images = item.GetImages(ToImageType(kind)).ToList();
        return index >= 0 && index < images.Count ? images[index] : null;
    }

    private BaseItem GetItem(User user, Guid itemId)
        => _libraryManager.GetItemById<BaseItem>(itemId, user) ?? throw new ArtworkException("That title isn't in the library.");

    private List<ArtworkOptionDto> GetLibraryOptions(BaseItem item, ArtworkKind kind, ArtworkChoice? choice)
    {
        var type = ToImageType(kind);
        return item.GetImages(type).Select((image, index) => new ArtworkOptionDto
        {
            Id = ArtworkFiles.LibraryPrefix + index,
            PreviewUrl = LibraryPath(item, type, index, image),
            Source = "Library",
            Width = image.Width > 0 ? image.Width : null,
            Height = image.Height > 0 ? image.Height : null,
            IsDefault = index == 0,
            IsSelected = choice?.ImageIndex == index
        }).ToList();
    }

    private string LibraryPath(BaseItem item, ImageType type, int index, ItemImageInfo image)
        => $"Items/{item.Id:N}/Images/{type}/{index}?tag={_imageProcessor.GetImageCacheTag(item, image)}";

    private string? GetPath(BaseItem item, ArtworkKind kind, ArtworkChoice? choice)
    {
        if (choice?.File is { } file)
        {
            return GetImagePath(file) is null ? null : "JellyFlix/Artwork/Images/" + file;
        }

        if (choice?.ImageIndex is { } index && GetLibraryImage(item, kind, index) is { } image)
        {
            return LibraryPath(item, ToImageType(kind), index, image);
        }

        return null;
    }

    private async Task<IReadOnlyList<RemoteImageInfo>> GetRemoteImagesAsync(BaseItem item, ArtworkKind kind, CancellationToken cancellationToken)
    {
        var type = ToImageType(kind);
        var now = _timeProvider.GetUtcNow();
        if (_remote.TryGetValue((item.Id, type), out var cached) && now - cached.At < _remoteCacheTime)
        {
            return cached.Images;
        }

        var images = (await _providerManager.GetAvailableRemoteImages(
                item,
                new RemoteImageQuery(string.Empty) { ImageType = type, IncludeAllLanguages = true },
                cancellationToken).ConfigureAwait(false))
            .Where(i => Uri.TryCreate(i.Url, UriKind.Absolute, out var url) && (url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp))
            .DistinctBy(i => i.Url)
            .Take(MaxRemoteOptions)
            .ToList();
        _remote[(item.Id, type)] = (now, images);
        return images;
    }

    private async Task<string> DownloadAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _httpClientFactory.CreateClient(NamedClient.Default)
            .GetAsync(new Uri(url), HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new ArtworkException($"The image couldn't be downloaded ({(int)response.StatusCode}).");
        }

        var name = ArtworkFiles.FileName(url, response.Content.Headers.ContentType?.MediaType)
            ?? throw new ArtworkException("The provider didn't send an image.");
        if (response.Content.Headers.ContentLength > ArtworkFiles.MaxImageBytes)
        {
            throw new ArtworkException("The image is too large.");
        }

        var path = Path.Combine(_store.ImagesPath, name);
        if (File.Exists(path))
        {
            return name;
        }

        Directory.CreateDirectory(_store.ImagesPath);
        var temp = path + ".tmp";
        var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (source.ConfigureAwait(false))
        {
            var target = File.Create(temp);
            await using (target.ConfigureAwait(false))
            {
                // Copies by hand to stop at the size limit when the provider didn't say how large it is
                var buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    total += read;
                    if (total > ArtworkFiles.MaxImageBytes)
                    {
                        throw new ArtworkException("The image is too large.");
                    }

                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                }
            }
        }

        File.Move(temp, path, true);
        return name;
    }

    private void RemoveUnusedFiles()
    {
        if (!Directory.Exists(_store.ImagesPath))
        {
            return;
        }

        var inUse = _store.GetFilesInUse();
        foreach (var path in Directory.EnumerateFiles(_store.ImagesPath))
        {
            if (!inUse.Contains(Path.GetFileName(path)))
            {
                try
                {
                    File.Delete(path);
                }
                catch (IOException ex)
                {
                    _logger.LogDebug(ex, "Couldn't remove unused artwork {Path}", path);
                }
            }
        }
    }
}
