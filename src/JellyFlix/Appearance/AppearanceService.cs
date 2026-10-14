using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using JellyFlix.Appearance.Api.Models;
using JellyFlix.Configuration;
using JellyFlix.Search;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;

namespace JellyFlix.Appearance;

/// <summary>
/// Works out the seasonal look and its curated shelves.
/// </summary>
public class AppearanceService
{
    /// <summary>
    /// Shelves with fewer titles than this aren't shown.
    /// </summary>
    public const int MinShelfSize = 3;

    private const int MaxShelfSize = 20;
    private static readonly TimeSpan _cacheTime = TimeSpan.FromMinutes(30);

    private static readonly DtoOptions _dtoOptions = new()
    {
        Fields = [ItemFields.Overview, ItemFields.Genres, ItemFields.PrimaryImageAspectRatio, ItemFields.ChildCount],
        ImageTypes = [ImageType.Primary, ImageType.Backdrop, ImageType.Logo],
        ImageTypeLimit = 1,
        EnableUserData = true
    };

    private readonly ILibraryManager _libraryManager;
    private readonly IDtoService _dtoService;
    private readonly Func<PluginConfiguration> _configuration;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<(Guid, Season), (DateTimeOffset At, List<(string Key, List<Guid> Ids)> Shelves)> _cache = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AppearanceService"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="dtoService">The DTO service.</param>
    /// <param name="configuration">Returns the current plugin configuration.</param>
    /// <param name="timeProvider">The time provider.</param>
    public AppearanceService(ILibraryManager libraryManager, IDtoService dtoService, Func<PluginConfiguration> configuration, TimeProvider timeProvider)
    {
        _libraryManager = libraryManager;
        _dtoService = dtoService;
        _configuration = configuration;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Gets the current look for a profile.
    /// </summary>
    /// <param name="user">The profile.</param>
    /// <returns>The look, with the season's shelves.</returns>
    public AppearanceDto Get(User user)
    {
        var config = _configuration();
        var now = _timeProvider.GetLocalNow();
        var season = SeasonCalendar.Resolve(config.SeasonalTheme, DateOnly.FromDateTime(now.DateTime));
        var result = new AppearanceDto { Season = season, DynamicColors = config.DynamicColors };
        if (season == Season.None)
        {
            return result;
        }

        if (!_cache.TryGetValue((user.Id, season), out var cached) || now - cached.At > _cacheTime)
        {
            cached = (now, BuildShelves(user, season));
            _cache[(user.Id, season)] = cached;
        }

        result.Shelves = cached.Shelves
            .Select(shelf => new SeasonalShelfDto
            {
                Key = shelf.Key,
                Items = _dtoService.GetBaseItemDtos(
                    shelf.Ids.Select(_libraryManager.GetItemById).OfType<BaseItem>().ToList(),
                    _dtoOptions,
                    user,
                    skipVisibilityCheck: true)
            })
            .Where(shelf => shelf.Items.Count >= MinShelfSize)
            .ToList();
        return result;
    }

    /// <summary>
    /// Fills a season's shelves from the titles a profile may see. A title appears on one shelf at most.
    /// </summary>
    /// <param name="shelves">The shelves.</param>
    /// <param name="titles">Each title's id, stemmed words, genres and rating.</param>
    /// <returns>The shelves' title ids, best first.</returns>
    internal static List<(string Key, List<Guid> Ids)> Fill(
        IReadOnlyList<SeasonalShelf> shelves,
        IReadOnlyList<(Guid Id, IReadOnlySet<string> Words, IReadOnlyList<string> Genres, float Rating)> titles)
    {
        var used = new HashSet<Guid>();
        var result = new List<(string Key, List<Guid> Ids)>();
        foreach (var shelf in shelves)
        {
            var ids = titles
                .Where(t => !used.Contains(t.Id))
                .Select(t => (t.Id, Score: shelf.Score(t.Words, t.Genres), t.Rating))
                .Where(t => t.Score > 0)
                .OrderByDescending(t => t.Score)
                .ThenByDescending(t => t.Rating)
                .ThenBy(t => t.Id)
                .Take(MaxShelfSize)
                .Select(t => t.Id)
                .ToList();
            if (ids.Count >= MinShelfSize)
            {
                used.UnionWith(ids);
                result.Add((shelf.Key, ids));
            }
        }

        return result;
    }

    private List<(string Key, List<Guid> Ids)> BuildShelves(User user, Season season)
    {
        // Parental controls and library access apply, so kids profiles get kids' titles
        var items = _libraryManager.GetItemList(new InternalItemsQuery(user)
        {
            IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Series],
            Recursive = true,
            IsVirtualItem = false
        });

        var titles = items
            .Select(item => (
                item.Id,
                (IReadOnlySet<string>)Tokenizer.Tokenize(string.Join(' ', [item.Name, item.Tagline, item.Overview, .. item.Tags])).ToHashSet(StringComparer.Ordinal),
                (IReadOnlyList<string>)item.Genres,
                item.CommunityRating ?? 0))
            .ToList();
        return Fill(SeasonalShelves.For(season), titles);
    }
}
