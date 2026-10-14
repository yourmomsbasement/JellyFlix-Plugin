using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using JellyFlix.Companion.Api.Models;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;

namespace JellyFlix.Companion;

/// <summary>
/// Finds what to offer when a title ends: the next film in its collection, its extras and its soundtrack.
/// </summary>
public class CompanionService
{
    private const int MaxExtras = 20;
    private const int MaxSoundtracks = 6;

    private static readonly DtoOptions _dtoOptions = new()
    {
        Fields = [ItemFields.Overview, ItemFields.Genres, ItemFields.PrimaryImageAspectRatio],
        ImageTypes = [ImageType.Primary, ImageType.Backdrop, ImageType.Logo, ImageType.Thumb],
        ImageTypeLimit = 1,
        EnableUserData = true
    };

    private static readonly string[] _soundtrackWords = ["soundtrack", "score", "music from", "ost"];

    private readonly ILibraryManager _libraryManager;
    private readonly IDtoService _dtoService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompanionService"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="dtoService">The DTO service.</param>
    public CompanionService(ILibraryManager libraryManager, IDtoService dtoService)
    {
        _libraryManager = libraryManager;
        _dtoService = dtoService;
    }

    /// <summary>
    /// Gets what to offer after a title, for a profile.
    /// </summary>
    /// <param name="user">The profile.</param>
    /// <param name="itemId">The title that finished.</param>
    /// <returns>The offers, or null when the profile can't see the title.</returns>
    public CompanionDto? Get(User user, Guid itemId)
    {
        var item = _libraryManager.GetItemById<BaseItem>(itemId, user);
        if (item is null)
        {
            return null;
        }

        var result = new CompanionDto();
        var (collection, next) = FindNextInCollection(user, item);
        if (collection is not null)
        {
            result.CollectionName = collection.Name;
            result.NextInCollection = next is null ? null : _dtoService.GetBaseItemDto(next, _dtoOptions, user);
        }

        var extras = item.GetExtras(BaseItem.DisplayExtraTypes, user).Take(MaxExtras).ToList();
        result.Extras = _dtoService.GetBaseItemDtos(extras, _dtoOptions, user);

        result.Composers = _libraryManager.GetPeople(item)
            .Where(p => p.IsType(PersonKind.Composer))
            .Select(p => p.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        result.Soundtracks = _dtoService.GetBaseItemDtos(FindSoundtracks(user, item, result.Composers), _dtoOptions, user);
        return result;
    }

    private (BoxSet? Collection, BaseItem? Next) FindNextInCollection(User user, BaseItem item)
    {
        var collections = _libraryManager.GetItemList(new InternalItemsQuery(user)
        {
            IncludeItemTypes = [BaseItemKind.BoxSet],
            Recursive = true
        }).OfType<BoxSet>().Where(c => c.ContainsLinkedChildByItemId(item.Id));

        // A title can be in several collections (a franchise and "Oscar winners"): prefer one that has a next title,
        // then the smallest, which is most likely the film series
        (BoxSet? Collection, BaseItem? Next) best = (null, null);
        var bestSize = int.MaxValue;
        foreach (var collection in collections)
        {
            var titles = collection.GetLinkedChildren(user).Where(c => c is Movie or MediaBrowser.Controller.Entities.TV.Series).ToList();
            var nextId = CollectionOrder.NextAfter(titles.Select(t => (t.Id, t.PremiereDate, t.ProductionYear)), item.Id);
            var next = nextId is null ? null : titles.First(t => t.Id == nextId);
            var isBetter = best.Collection is null
                || (next is not null && best.Next is null)
                || ((next is null) == (best.Next is null) && titles.Count < bestSize);
            if (isBetter)
            {
                best = (collection, next);
                bestSize = titles.Count;
            }
        }

        return best;
    }

    private List<BaseItem> FindSoundtracks(User user, BaseItem item, IReadOnlyList<string> composers)
    {
        if (string.IsNullOrWhiteSpace(item.Name))
        {
            return [];
        }

        var albums = _libraryManager.GetItemList(new InternalItemsQuery(user)
        {
            IncludeItemTypes = [BaseItemKind.MusicAlbum],
            Recursive = true,
            SearchTerm = item.Name,
            Limit = 30
        });

        // Albums named after the title are only soundtracks if they say so, or are by its composer
        return albums
            .Where(a => a.Name.Contains(item.Name, StringComparison.OrdinalIgnoreCase))
            .Where(a => _soundtrackWords.Any(w => a.Name.Contains(w, StringComparison.OrdinalIgnoreCase))
                || (a is MediaBrowser.Controller.Entities.Audio.MusicAlbum album && album.AlbumArtists.Any(artist => composers.Contains(artist, StringComparer.OrdinalIgnoreCase)))
                || a.Genres.Any(g => g.Contains("soundtrack", StringComparison.OrdinalIgnoreCase)))
            .Take(MaxSoundtracks)
            .ToList();
    }
}
