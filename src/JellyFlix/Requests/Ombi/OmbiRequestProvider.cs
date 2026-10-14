using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JellyFlix.Configuration;
using JellyFlix.Requests.Api.Models;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace JellyFlix.Requests.Ombi;

/// <summary>
/// Requests through Ombi.
/// </summary>
public class OmbiRequestProvider : IRequestProvider
{
    /// <summary>
    /// Ombi's search doesn't say what's already requested, so the first results are looked up one by one.
    /// </summary>
    private const int MaxLookups = 12;

    private static readonly TimeSpan _usersCacheTime = TimeSpan.FromMinutes(10);

    private readonly OmbiClient _ombi;
    private readonly IUserManager _userManager;
    private readonly Func<PluginConfiguration> _configuration;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OmbiRequestProvider> _logger;

    private IReadOnlyList<OmbiUser>? _users;
    private DateTimeOffset _usersLoadedAt;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmbiRequestProvider"/> class.
    /// </summary>
    /// <param name="ombi">The Ombi client.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="configuration">Returns the current plugin configuration.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="logger">The logger.</param>
    public OmbiRequestProvider(OmbiClient ombi, IUserManager userManager, Func<PluginConfiguration> configuration, TimeProvider timeProvider, ILogger<OmbiRequestProvider> logger)
    {
        _ombi = ombi;
        _userManager = userManager;
        _configuration = configuration;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Ombi";

    /// <inheritdoc />
    public bool IsConfigured
    {
        get
        {
            var config = _configuration();
            return !string.IsNullOrWhiteSpace(config.OmbiUrl) && !string.IsNullOrWhiteSpace(config.OmbiApiKey);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RequestableTitleDto>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var results = await _ombi.SearchAsync(query, cancellationToken).ConfigureAwait(false);
        var titles = results
            .Where(r => r.MediaType is SeerrMediaType.Movie or SeerrMediaType.Tv)
            .Select(r => (Result: r, Id: int.TryParse(r.Id, out var id) ? id : 0))
            .Where(r => r.Id > 0)
            .Take(MaxLookups)
            .ToList();

        // Look up where each stands at the same time
        return await Task.WhenAll(titles.Select(async t =>
        {
            OmbiTitleInfo? info = null;
            try
            {
                info = await _ombi.GetTitleInfoAsync(t.Result.MediaType!, t.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (RequestProviderException ex)
            {
                _logger.LogDebug(ex, "Couldn't get Ombi's details for {MediaType} {TmdbId}", t.Result.MediaType, t.Id);
            }

            return ToDto(t.Result, t.Id, info);
        })).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RequestAsync(Guid ownerId, string mediaType, int tmdbId, CancellationToken cancellationToken)
    {
        var ownerName = _userManager.GetUserById(ownerId)?.Username;
        var ombiUser = ownerName is null ? null : await FindOmbiUserAsync(ownerName, cancellationToken).ConfigureAwait(false);
        if (ombiUser is null)
        {
            _logger.LogInformation("No Ombi user is named {UserName}; requesting as the API key's user", ownerName);
        }

        await _ombi.CreateRequestAsync(mediaType, tmdbId, (ombiUser, ownerName), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<string> GetVersionAsync(CancellationToken cancellationToken) => _ombi.GetVersionAsync(cancellationToken);

    /// <summary>
    /// Where a title stands, from Ombi's details.
    /// </summary>
    /// <param name="mediaType">"movie" or "tv".</param>
    /// <param name="info">Ombi's details, if it gave any.</param>
    /// <returns>The status.</returns>
    internal static RequestStatus GetStatus(string mediaType, OmbiTitleInfo? info)
    {
        if (info is null)
        {
            return RequestStatus.None;
        }

        if (mediaType == SeerrMediaType.Tv ? info.FullyAvailable : info.Available)
        {
            return RequestStatus.Available;
        }

        if (mediaType == SeerrMediaType.Tv && info.PartlyAvailable)
        {
            return RequestStatus.PartiallyAvailable;
        }

        if (info.Denied == true)
        {
            return RequestStatus.Blocked;
        }

        if (info.Requested)
        {
            return info.Approved ? RequestStatus.Processing : RequestStatus.Requested;
        }

        return RequestStatus.None;
    }

    private static RequestableTitleDto ToDto(OmbiSearchResult result, int tmdbId, OmbiTitleInfo? info)
    {
        var date = result.MediaType == SeerrMediaType.Tv ? info?.FirstAired : info?.ReleaseDate;
        var poster = result.Poster;
        return new RequestableTitleDto
        {
            TmdbId = tmdbId,
            MediaType = result.MediaType!,
            Title = result.Title ?? string.Empty,
            Year = date?.Length >= 4 && int.TryParse(date.AsSpan(0, 4), out var year) && year > 1800 ? year : null,
            Overview = result.Overview,
            PosterUrl = string.IsNullOrEmpty(poster) ? null : poster.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? poster : SeerrRequestProvider.PosterBaseUrl + poster,
            Status = GetStatus(result.MediaType!, info)
        };
    }

    private async Task<string?> FindOmbiUserAsync(string userName, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        if (_users is null || now - _usersLoadedAt > _usersCacheTime)
        {
            _users = await _ombi.GetUsersAsync(cancellationToken).ConfigureAwait(false);
            _usersLoadedAt = now;
        }

        // Users imported from Jellyfin keep their Jellyfin name
        return _users.FirstOrDefault(u => string.Equals(u.UserName, userName, StringComparison.OrdinalIgnoreCase))?.UserName;
    }
}
