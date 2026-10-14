using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using JellyFlix.Configuration;
using JellyFlix.Search.Api.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace JellyFlix.Search;

/// <summary>
/// Finds titles from a description ("space movie with a stranded astronaut") using their metadata: a full-text index
/// always, and an embeddings index when semantic search is set up.
/// </summary>
public sealed class DescriptionSearchService : IDisposable
{
    private const int CandidateCount = 100;
    private const int EmbeddingBatchSize = 32;
    private const int MaxEmbeddingTextLength = 2000;
    private const int MaxPeople = 8;
    private static readonly TimeSpan _visibilityCacheTime = TimeSpan.FromMinutes(5);

    private readonly ILibraryManager _libraryManager;
    private readonly EmbeddingClient _embeddings;
    private readonly EmbeddingStore _store;
    private readonly Func<PluginConfiguration> _configuration;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DescriptionSearchService> _logger;
    private readonly SemaphoreSlim _buildLock = new(1, 1);
    private readonly ConcurrentDictionary<Guid, (DateTimeOffset At, HashSet<Guid> Ids)> _visible = new();
    private readonly ConcurrentDictionary<string, float[]> _queryVectors = new(StringComparer.Ordinal);
    private readonly object _indexLock = new();

    private TextIndex? _textIndex;
    private EmbeddingSet? _embeddingSet;
    private DateTimeOffset? _lastBuild;
    private string? _lastBuildResult;
    private double? _buildProgress;

    /// <summary>
    /// Initializes a new instance of the <see cref="DescriptionSearchService"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="embeddings">The embeddings client.</param>
    /// <param name="store">Where embeddings are kept.</param>
    /// <param name="configuration">Returns the current plugin configuration.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="logger">The logger.</param>
    public DescriptionSearchService(
        ILibraryManager libraryManager,
        EmbeddingClient embeddings,
        EmbeddingStore store,
        Func<PluginConfiguration> configuration,
        TimeProvider timeProvider,
        ILogger<DescriptionSearchService> logger)
    {
        _libraryManager = libraryManager;
        _embeddings = embeddings;
        _store = store;
        _configuration = configuration;
        _timeProvider = timeProvider;
        _logger = logger;

        _libraryManager.ItemAdded += OnLibraryChanged;
        _libraryManager.ItemUpdated += OnLibraryChanged;
        _libraryManager.ItemRemoved += OnLibraryChanged;
    }

    /// <summary>
    /// Gets a value indicating whether semantic search is turned on and set up.
    /// </summary>
    public bool IsSemanticEnabled
    {
        get
        {
            var config = _configuration();
            return config.SemanticSearchEnabled && !string.IsNullOrWhiteSpace(config.EmbeddingsUrl) && !string.IsNullOrWhiteSpace(config.EmbeddingsModel);
        }
    }

    /// <summary>
    /// Finds the titles the user may see that best match a description.
    /// </summary>
    /// <param name="user">The signed-in profile.</param>
    /// <param name="query">The description.</param>
    /// <param name="limit">How many results at most.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching items, best first, and whether meaning was used as well as words.</returns>
    public async Task<(IReadOnlyList<BaseItem> Items, bool IsSemantic)> SearchAsync(User user, string query, int limit, CancellationToken cancellationToken)
    {
        query = query.Trim();
        if (query.Length == 0)
        {
            return ([], false);
        }

        var visible = GetVisibleIds(user);
        var textMatches = SearchRanking.KeepStrongTextMatches(
            GetTextIndex().Search(query, CandidateCount * 2).Where(m => visible.Contains(m.Id)).Take(CandidateCount).ToList());

        var lists = new List<IReadOnlyList<Guid>> { textMatches };
        var isSemantic = false;
        if (IsSemanticEnabled)
        {
            try
            {
                var set = await GetEmbeddingSetAsync().ConfigureAwait(false);
                if (set.Model == _embeddings.Model && set.Titles.Count > 0)
                {
                    var queryVector = await EmbedQueryAsync(query, cancellationToken).ConfigureAwait(false);
                    var candidates = set.Titles
                        .Where(t => visible.Contains(t.Key))
                        .Select(t => new KeyValuePair<Guid, float[]>(t.Key, t.Value.Vector));
                    lists.Add(SearchRanking.RankBySimilarity(queryVector, candidates, CandidateCount));
                    isSemantic = true;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Words alone still give results
                _logger.LogWarning(ex, "Semantic search failed; using text search only");
            }
        }

        var ids = SearchRanking.Fuse(lists, limit);
        var items = ids.Select(id => _libraryManager.GetItemById(id)).OfType<BaseItem>().ToList();
        return (items, isSemantic);
    }

    /// <summary>
    /// Brings the embeddings index up to date: titles that are new or whose metadata changed are embedded.
    /// </summary>
    /// <param name="progress">Reports progress from 0 to 100.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>What happened, in words.</returns>
    public async Task<string> BuildEmbeddingsAsync(IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (!IsSemanticEnabled)
        {
            return "Semantic search is off.";
        }

        await _buildLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _buildProgress = 0;
            var tracked = new InlineProgress(value =>
            {
                _buildProgress = value;
                progress?.Report(value);
            });
            _lastBuildResult = await BuildEmbeddingsCoreAsync(tracked, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Building the JellyFlix search index failed");
            _lastBuildResult = "Failed: " + ex.Message;
        }
        finally
        {
            _buildProgress = null;
            _lastBuild = _timeProvider.GetUtcNow();
            _buildLock.Release();
        }

        return _lastBuildResult;
    }

    /// <summary>
    /// Starts bringing the embeddings index up to date in the background, unless that's already happening.
    /// </summary>
    public void StartBuildingEmbeddings()
    {
        if (_buildProgress is not null || !IsSemanticEnabled)
        {
            return;
        }

        // Before the task starts, so a status read straight after shows the build
        _buildProgress = 0;
        _ = Task.Run(() => BuildEmbeddingsAsync(null, CancellationToken.None));
    }

    /// <summary>
    /// Embeds a sample, to check the embeddings API.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The model and the size of its vectors.</returns>
    public async Task<SemanticTestDto> TestAsync(CancellationToken cancellationToken)
    {
        var vectors = await _embeddings.EmbedAsync(["A space movie with a stranded astronaut"], cancellationToken).ConfigureAwait(false);
        return new SemanticTestDto { Model = _embeddings.Model, Dimensions = vectors[0].Length };
    }

    /// <summary>
    /// Gets the state of the search indexes.
    /// </summary>
    /// <returns>The status.</returns>
    public async Task<SearchStatusDto> GetStatusAsync()
    {
        var set = await GetEmbeddingSetAsync().ConfigureAwait(false);
        return new SearchStatusDto
        {
            IsSemanticEnabled = IsSemanticEnabled,
            Titles = GetTextIndex().Count,
            EmbeddedTitles = set.Model == _embeddings.Model ? set.Titles.Count : 0,
            Model = set.Model,
            LastBuild = _lastBuild,
            BuildProgress = _buildProgress,
            LastBuildResult = _lastBuildResult
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _libraryManager.ItemAdded -= OnLibraryChanged;
        _libraryManager.ItemUpdated -= OnLibraryChanged;
        _libraryManager.ItemRemoved -= OnLibraryChanged;
        _buildLock.Dispose();
    }

    /// <summary>
    /// The text a title is embedded from: what it's called and what it's about.
    /// </summary>
    /// <param name="item">The title.</param>
    /// <param name="people">Its cast and crew.</param>
    /// <returns>The text.</returns>
    internal static string GetEmbeddingText(BaseItem item, IReadOnlyList<PersonInfo> people)
    {
        var text = new StringBuilder();
        text.Append(item.Name);
        if (item.ProductionYear is { } year)
        {
            text.Append(" (").Append(year).Append(')');
        }

        text.Append(". ");
        if (item.Genres.Length > 0)
        {
            text.Append("Genres: ").AppendJoin(", ", item.Genres).Append(". ");
        }

        if (!string.IsNullOrWhiteSpace(item.Tagline))
        {
            text.Append(item.Tagline).Append(' ');
        }

        text.Append(item.Overview);
        if (item.Tags.Length > 0)
        {
            text.Append(" Keywords: ").AppendJoin(", ", item.Tags.Take(20)).Append('.');
        }

        var cast = people.Where(p => p.IsType(PersonKind.Actor)).Take(MaxPeople).Select(p => p.Name).ToList();
        if (cast.Count > 0)
        {
            text.Append(" Starring ").AppendJoin(", ", cast).Append('.');
        }

        var result = text.ToString();
        return result.Length <= MaxEmbeddingTextLength ? result : result[..MaxEmbeddingTextLength];
    }

    private static IEnumerable<(string? Text, double Weight)> GetFields(BaseItem item, IReadOnlyList<PersonInfo> people)
    {
        yield return (item.Name, 3);
        yield return (item.OriginalTitle, 2);
        yield return (item.Tagline, 2);
        yield return (item.Overview, 1);
        foreach (var genre in item.Genres)
        {
            yield return (genre, 1.5);
        }

        foreach (var tag in item.Tags)
        {
            yield return (tag, 1.5);
        }

        foreach (var studio in item.Studios)
        {
            yield return (studio, 0.5);
        }

        foreach (var person in people.Take(MaxPeople * 2))
        {
            yield return (person.Name, person.IsType(PersonKind.Actor) || person.IsType(PersonKind.Director) ? 1 : 0.5);
        }
    }

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];

    private IReadOnlyList<BaseItem> GetAllTitles() => _libraryManager.GetItemList(new InternalItemsQuery
    {
        IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Series],
        Recursive = true,
        IsVirtualItem = false
    });

    private TextIndex GetTextIndex()
    {
        lock (_indexLock)
        {
            if (_textIndex is not null)
            {
                return _textIndex;
            }

            var started = _timeProvider.GetTimestamp();
            var items = GetAllTitles();
            var people = items.Count == 0 ? new Dictionary<Guid, IReadOnlyList<PersonInfo>>() : _libraryManager.GetPeopleByItems(items.Select(i => i.Id).ToList());
            var index = new TextIndex();
            foreach (var item in items)
            {
                index.Add(item.Id, GetFields(item, people.TryGetValue(item.Id, out var credits) ? credits : []));
            }

            _logger.LogDebug("Built the JellyFlix text search index: {Count} titles in {Elapsed} ms", index.Count, _timeProvider.GetElapsedTime(started).TotalMilliseconds);
            _textIndex = index;
            return index;
        }
    }

    /// <summary>
    /// Titles the profile may see: parental controls and library access apply to search too.
    /// </summary>
    private HashSet<Guid> GetVisibleIds(User user)
    {
        var now = _timeProvider.GetUtcNow();
        if (_visible.TryGetValue(user.Id, out var cached) && now - cached.At < _visibilityCacheTime)
        {
            return cached.Ids;
        }

        var ids = _libraryManager.GetItemIds(new InternalItemsQuery(user)
        {
            IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Series],
            Recursive = true,
            IsVirtualItem = false
        }).ToHashSet();
        _visible[user.Id] = (now, ids);
        return ids;
    }

    private async Task<EmbeddingSet> GetEmbeddingSetAsync()
        => _embeddingSet ??= await _store.LoadAsync().ConfigureAwait(false);

    private async Task<float[]> EmbedQueryAsync(string query, CancellationToken cancellationToken)
    {
        var key = _embeddings.Model + "\n" + query.ToLowerInvariant();
        if (_queryVectors.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var vector = (await _embeddings.EmbedAsync([query], cancellationToken).ConfigureAwait(false))[0];
        if (_queryVectors.Count > 500)
        {
            _queryVectors.Clear();
        }

        _queryVectors[key] = vector;
        return vector;
    }

    private async Task<string> BuildEmbeddingsCoreAsync(IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var model = _embeddings.Model;
        var set = await _store.LoadAsync().ConfigureAwait(false);
        if (set.Model != model)
        {
            // Vectors from different models can't be compared: start over
            set = new EmbeddingSet { Model = model };
        }

        var items = GetAllTitles();
        var people = items.Count == 0 ? new Dictionary<Guid, IReadOnlyList<PersonInfo>>() : _libraryManager.GetPeopleByItems(items.Select(i => i.Id).ToList());
        var texts = items.ToDictionary(i => i.Id, i => GetEmbeddingText(i, people.TryGetValue(i.Id, out var credits) ? credits : []));

        // Forget titles that left the library
        foreach (var gone in set.Titles.Keys.Where(id => !texts.ContainsKey(id)).ToList())
        {
            set.Titles.Remove(gone);
        }

        var pending = texts
            .Where(t => !set.Titles.TryGetValue(t.Key, out var existing) || existing.TextHash != Hash(t.Value))
            .ToList();

        for (var i = 0; i < pending.Count; i += EmbeddingBatchSize)
        {
            var batch = pending.Skip(i).Take(EmbeddingBatchSize).ToList();
            var vectors = await _embeddings.EmbedAsync(batch.Select(b => b.Value).ToList(), cancellationToken).ConfigureAwait(false);
            for (var j = 0; j < batch.Count; j++)
            {
                set.Titles[batch[j].Key] = new TitleEmbedding { TextHash = Hash(batch[j].Value), Vector = vectors[j] };
            }

            progress?.Report(100.0 * Math.Min(i + EmbeddingBatchSize, pending.Count) / pending.Count);

            // Save as it goes, so a long first run isn't lost to a restart
            if ((i / EmbeddingBatchSize) % 10 == 9)
            {
                await _store.SaveAsync(set).ConfigureAwait(false);
            }
        }

        set.UpdatedAt = _timeProvider.GetUtcNow();
        await _store.SaveAsync(set).ConfigureAwait(false);
        _embeddingSet = set;
        _logger.LogInformation("JellyFlix search index: embedded {Count} titles with {Model}", pending.Count, model);
        return pending.Count == 0 ? $"Up to date ({set.Titles.Count} titles)." : $"Embedded {pending.Count} titles ({set.Titles.Count} in all).";
    }

    private void OnLibraryChanged(object? sender, ItemChangeEventArgs e)
    {
        if (e.Item is MediaBrowser.Controller.Entities.Movies.Movie or MediaBrowser.Controller.Entities.TV.Series)
        {
            lock (_indexLock)
            {
                _textIndex = null;
            }

            _visible.Clear();
        }
    }

    /// <summary>
    /// Reports straight away on the calling thread, unlike <see cref="Progress{T}"/>, so no report lands after the build.
    /// </summary>
    private sealed class InlineProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
