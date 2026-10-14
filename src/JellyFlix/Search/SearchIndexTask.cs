using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Tasks;

namespace JellyFlix.Search;

/// <summary>
/// The "Build Lanternfish search index" scheduled task: embeds new and changed titles for semantic search.
/// </summary>
public class SearchIndexTask : IScheduledTask
{
    private readonly DescriptionSearchService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchIndexTask"/> class.
    /// </summary>
    /// <param name="service">The description search service.</param>
    public SearchIndexTask(DescriptionSearchService service)
    {
        _service = service;
    }

    /// <inheritdoc />
    public string Name => "Build JellyFlix search index";

    /// <inheritdoc />
    public string Key => "JellyFlixSearchIndex";

    /// <inheritdoc />
    public string Description => "Embeds new and changed titles so search can match descriptions by meaning. Does nothing while semantic search is off.";

    /// <inheritdoc />
    public string Category => "Lanternfish";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        await _service.BuildEmbeddingsAsync(progress, cancellationToken).ConfigureAwait(false);
        progress.Report(100);
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() =>
    [
        new TaskTriggerInfo { Type = TaskTriggerInfoType.StartupTrigger },
        new TaskTriggerInfo { Type = TaskTriggerInfoType.IntervalTrigger, IntervalTicks = TimeSpan.FromHours(24).Ticks }
    ];
}
