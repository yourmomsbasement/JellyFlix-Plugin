using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MediaBrowser.Common.Configuration;

namespace JellyFlix.Artwork;

/// <summary>
/// Each profile's artwork choices, in <c>jellyflix-artwork/artwork.json</c> under Jellyfin's data folder. Chosen
/// images downloaded from metadata providers are kept next to it.
/// </summary>
public class ArtworkStore
{
    private readonly string _path;
    private readonly object _lock = new();
    private Dictionary<Guid, Dictionary<Guid, TitleArtwork>>? _choices;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArtworkStore"/> class.
    /// </summary>
    /// <param name="applicationPaths">The application paths.</param>
    public ArtworkStore(IApplicationPaths applicationPaths)
    {
        var folder = Path.Combine(applicationPaths.DataPath, "jellyflix-artwork");
        _path = Path.Combine(folder, "artwork.json");
        ImagesPath = Path.Combine(folder, "images");
    }

    /// <summary>
    /// Gets the folder downloaded images are kept in.
    /// </summary>
    public string ImagesPath { get; }

    /// <summary>
    /// Gets a profile's choices.
    /// </summary>
    /// <param name="userId">The profile.</param>
    /// <returns>The choices by title, as a copy.</returns>
    public IReadOnlyDictionary<Guid, TitleArtwork> GetAll(Guid userId)
    {
        lock (_lock)
        {
            return Load().TryGetValue(userId, out var titles) ? new Dictionary<Guid, TitleArtwork>(titles) : new Dictionary<Guid, TitleArtwork>();
        }
    }

    /// <summary>
    /// Gets a profile's choice for a title.
    /// </summary>
    /// <param name="userId">The profile.</param>
    /// <param name="itemId">The title.</param>
    /// <param name="kind">Poster or backdrop.</param>
    /// <returns>The choice, or null for the library's.</returns>
    public ArtworkChoice? Get(Guid userId, Guid itemId, ArtworkKind kind)
    {
        lock (_lock)
        {
            return Load().TryGetValue(userId, out var titles) && titles.TryGetValue(itemId, out var artwork) ? artwork.Get(kind) : null;
        }
    }

    /// <summary>
    /// Sets or clears a profile's choice for a title.
    /// </summary>
    /// <param name="userId">The profile.</param>
    /// <param name="itemId">The title.</param>
    /// <param name="kind">Poster or backdrop.</param>
    /// <param name="choice">The choice, or null to go back to the library's.</param>
    public void Set(Guid userId, Guid itemId, ArtworkKind kind, ArtworkChoice? choice)
    {
        lock (_lock)
        {
            var all = Load();
            if (!all.TryGetValue(userId, out var titles))
            {
                if (choice is null)
                {
                    return;
                }

                titles = all[userId] = [];
            }

            if (!titles.TryGetValue(itemId, out var artwork))
            {
                artwork = titles[itemId] = new TitleArtwork();
            }

            artwork.Set(kind, choice);
            if (artwork.IsEmpty)
            {
                titles.Remove(itemId);
                if (titles.Count == 0)
                {
                    all.Remove(userId);
                }
            }

            Save(all);
        }
    }

    /// <summary>
    /// Forgets a profile's choices, when the profile is deleted.
    /// </summary>
    /// <param name="userId">The profile.</param>
    public void RemoveUser(Guid userId)
    {
        lock (_lock)
        {
            var all = Load();
            if (all.Remove(userId))
            {
                Save(all);
            }
        }
    }

    /// <summary>
    /// Gets the downloaded files some profile still uses.
    /// </summary>
    /// <returns>The file names.</returns>
    public IReadOnlySet<string> GetFilesInUse()
    {
        lock (_lock)
        {
            return Load().Values
                .SelectMany(titles => titles.Values)
                .SelectMany(artwork => new[] { artwork.Poster?.File, artwork.Backdrop?.File })
                .OfType<string>()
                .ToHashSet(StringComparer.Ordinal);
        }
    }

    private Dictionary<Guid, Dictionary<Guid, TitleArtwork>> Load()
    {
        if (_choices is not null)
        {
            return _choices;
        }

        try
        {
            _choices = File.Exists(_path)
                ? JsonSerializer.Deserialize<Dictionary<Guid, Dictionary<Guid, TitleArtwork>>>(File.ReadAllText(_path)) ?? []
                : [];
        }
        catch (JsonException)
        {
            _choices = [];
        }

        return _choices;
    }

    private void Save(Dictionary<Guid, Dictionary<Guid, TitleArtwork>> all)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(all));
        File.Move(temp, _path, true);
    }
}
