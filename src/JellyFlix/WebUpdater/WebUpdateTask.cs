using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JellyFlix.Configuration;
using MediaBrowser.Model.Tasks;

namespace JellyFlix.WebUpdater;

/// <summary>
/// The "Update Lanternfish web client" scheduled task: at startup and every six hours.
/// </summary>
public class WebUpdateTask : IScheduledTask
{
    private readonly WebUpdateService _service;
    private readonly Func<PluginConfiguration> _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebUpdateTask"/> class.
    /// </summary>
    /// <param name="service">The web update service.</param>
    /// <param name="configuration">Returns the current plugin configuration.</param>
    public WebUpdateTask(WebUpdateService service, Func<PluginConfiguration> configuration)
    {
        _service = service;
        _configuration = configuration;
    }

    /// <inheritdoc />
    public string Name => "Update JellyFlix web client";

    /// <inheritdoc />
    public string Key => "JellyFlixWebUpdate";

    /// <inheritdoc />
    public string Description => "Downloads and installs the newest Lanternfish web client built for this Jellyfin version.";

    /// <inheritdoc />
    public string Category => "Lanternfish";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        if (_configuration().WebUpdatesEnabled)
        {
            await _service.CheckAndInstallAsync(cancellationToken, progress).ConfigureAwait(false);
        }

        progress.Report(100);
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() =>
    [
        new TaskTriggerInfo { Type = TaskTriggerInfoType.StartupTrigger },
        new TaskTriggerInfo { Type = TaskTriggerInfoType.IntervalTrigger, IntervalTicks = TimeSpan.FromHours(6).Ticks }
    ];
}
