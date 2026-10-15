using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using JellyFlix.Recommendations.Engine;
using JellyFlix.Recommendations.Library;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace JellyFlix.Recommendations;

/// <summary>
/// Builds and caches each profile's recommendation model.
/// </summary>
/// <remarks>
/// <para>
/// Every title's features (genres, tags, studios, cast and crew) are the same for every profile, and looking up the
/// cast and crew of the whole library is the expensive part, so they're kept in one shared catalog. It's rebuilt after
/// the library changes, once the changes settle (see <see cref="CatalogFreshness"/>). A profile's model only picks the
/// titles the profile may see from it and adds the profile's taste.
/// </para>
/// <para>
/// A profile's model is rebuilt when its taste changes (something watched, a favorite, a thumbs up or down), when the
/// catalog is rebuilt, and at least every <see cref="MaxAge"/> so freshness keeps moving. Playback progress doesn't
/// count: it's saved every few seconds while something plays.
/// </para>
/// </remarks>
public sealed class RecommendationService : IDisposable
{
    /// <summary>
    /// The longest a cached model is used.
    /// </summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(30);

    private readonly ILibraryManager _libraryManager;
    private readonly IUserDataManager _userDataManager;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RecommendationService> _logger;
    private readonly ConcurrentDictionary<Guid, ProfileModel> _models = new();
    private readonly ConcurrentDictionary<Guid, object> _buildLocks = new();
    private readonly object _catalogLock = new();
    private readonly CatalogFreshness _catalogFreshness = new();
    private IReadOnlyDictionary<Guid, Title> _catalog = new Dictionary<Guid, Title>();
    private int _catalogVersion;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecommendationService"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="userDataManager">The user data manager.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="logger">The logger.</param>
    public RecommendationService(
        ILibraryManager libraryManager,
        IUserDataManager userDataManager,
        TimeProvider timeProvider,
        ILogger<RecommendationService> logger)
    {
        _libraryManager = libraryManager;
        _userDataManager = userDataManager;
        _timeProvider = timeProvider;
        _logger = logger;

        _userDataManager.UserDataSaved += OnUserDataSaved;
        _libraryManager.ItemAdded += OnLibraryChanged;
        _libraryManager.ItemRemoved += OnLibraryChanged;
        _libraryManager.ItemUpdated += OnLibraryChanged;
    }

    /// <summary>
    /// Whether a user data save can change a profile's taste. Playback start and progress saves can't: they happen every
    /// few seconds while something plays, and what was watched is saved again when playback finishes.
    /// </summary>
    /// <param name="reason">Why the user data was saved.</param>
    /// <returns>True when the profile's model should be rebuilt.</returns>
    public static bool AffectsTaste(UserDataSaveReason reason)
        => reason is not (UserDataSaveReason.PlaybackStart or UserDataSaveReason.PlaybackProgress);

    /// <summary>
    /// Gets the profile's recommendation model, building it if needed.
    /// </summary>
    /// <param name="user">The profile's user.</param>
    /// <returns>The model.</returns>
    public ProfileModel GetModel(User user)
    {
        if (TryGetFresh(user.Id, out var cached))
        {
            return cached;
        }

        // One build per profile at a time: the home page and a title page can ask at once
        lock (_buildLocks.GetOrAdd(user.Id, _ => new object()))
        {
            if (TryGetFresh(user.Id, out cached))
            {
                return cached;
            }

            var model = Build(user);
            _models[user.Id] = model;
            return model;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _userDataManager.UserDataSaved -= OnUserDataSaved;
        _libraryManager.ItemAdded -= OnLibraryChanged;
        _libraryManager.ItemRemoved -= OnLibraryChanged;
        _libraryManager.ItemUpdated -= OnLibraryChanged;
    }

    private bool TryGetFresh(Guid userId, out ProfileModel model)
    {
        var (_, version) = GetCatalog();
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (_models.TryGetValue(userId, out var cached) && cached.CatalogVersion == version && now - cached.BuiltAt < MaxAge)
        {
            model = cached;
            return true;
        }

        model = null!;
        return false;
    }

    /// <summary>
    /// The shared catalog of every movie and show, rebuilt when the library has changed and settled.
    /// </summary>
    private (IReadOnlyDictionary<Guid, Title> Catalog, int Version) GetCatalog()
    {
        lock (_catalogLock)
        {
            var now = _timeProvider.GetUtcNow();
            if (_catalogFreshness.NeedsRebuild(now))
            {
                var started = _timeProvider.GetTimestamp();
                var items = _libraryManager.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Series],
                    Recursive = true,
                    IsVirtualItem = false
                });
                _catalog = CatalogBuilder.Build(_libraryManager, items);
                _catalogVersion++;
                _catalogFreshness.Built(now);
                _logger.LogDebug(
                    "Built the recommendation catalog: {Titles} titles in {Elapsed} ms",
                    _catalog.Count,
                    _timeProvider.GetElapsedTime(started).TotalMilliseconds);
            }

            return (_catalog, _catalogVersion);
        }
    }

    private ProfileModel Build(User user)
    {
        var started = _timeProvider.GetTimestamp();
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var (catalog, version) = GetCatalog();

        // Only what this profile may see: parental controls and library access apply here
        var items = _libraryManager.GetItemList(new InternalItemsQuery(user)
        {
            IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Series],
            Recursive = true,
            IsVirtualItem = false
        });

        // Titles added since the catalog was built (it waits for the library to settle) are built here
        var missing = items.Where(item => !catalog.ContainsKey(item.Id)).ToList();
        var added = missing.Count == 0 ? catalog : CatalogBuilder.Build(_libraryManager, missing);
        var titles = items
            .Select(item => catalog.TryGetValue(item.Id, out var title) ? title : added[item.Id])
            .ToList();

        var engine = new RecommendationEngine(titles, now);
        var (signals, exclude, watched) = TasteCollector.Collect(_libraryManager, _userDataManager, user);
        var taste = engine.BuildTaste(signals);

        _logger.LogDebug(
            "Built recommendations for {User}: {Titles} titles ({Missing} not in the catalog yet), {Signals} signals in {Elapsed} ms",
            user.Username,
            titles.Count,
            missing.Count,
            signals.Count,
            _timeProvider.GetElapsedTime(started).TotalMilliseconds);

        return new ProfileModel(engine, taste, signals, exclude, watched, items.ToDictionary(i => i.Id), now, version);
    }

    private void OnUserDataSaved(object? sender, UserDataSaveEventArgs e)
    {
        if (AffectsTaste(e.SaveReason))
        {
            _models.TryRemove(e.UserId, out _);
        }
    }

    private void OnLibraryChanged(object? sender, ItemChangeEventArgs e)
    {
        if (e.Item is MediaBrowser.Controller.Entities.Movies.Movie or MediaBrowser.Controller.Entities.TV.Series)
        {
            lock (_catalogLock)
            {
                _catalogFreshness.Changed(_timeProvider.GetUtcNow());
            }
        }
    }
}
