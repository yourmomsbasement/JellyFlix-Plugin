using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common;
using MediaBrowser.Common.Plugins;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JellyFlix.Migration;

/// <summary>
/// Removes the separate JellyFlix Profiles and JellyFlix Recommendations plugins that this plugin replaces. Both
/// would otherwise answer the same API routes as this one. Also removes older copies of this plugin: Jellyfin only
/// cleans up older versions that have the same name, so the copy installed as "JellyFlix" stays loaded next to the
/// one installed as "Lanternfish", and the two copies break each other's settings.
/// </summary>
/// <param name="pluginManager">The plugin manager.</param>
/// <param name="applicationHost">The application host.</param>
/// <param name="logger">The logger.</param>
public sealed class LegacyPluginCleanup(IPluginManager pluginManager, IApplicationHost applicationHost, ILogger<LegacyPluginCleanup> logger) : IHostedService
{
    /// <summary>
    /// The ids of the plugins this one replaces.
    /// </summary>
    internal static readonly Guid[] LegacyPluginIds =
    [
        Guid.Parse("7b06ba47-197e-4e62-8582-e23a1eb25e2d"), // JellyFlix Profiles
        Guid.Parse("be5662d2-87dd-493e-804c-190a18e8d974") // JellyFlix Recommendations
    ];

    /// <summary>
    /// Gets the version of this copy of the plugin.
    /// </summary>
    internal static Version CurrentVersion { get; } = typeof(Plugin).Assembly.GetName().Version ?? new Version(0, 0);

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var legacy = pluginManager.Plugins
            .Where(plugin => LegacyPluginIds.Contains(plugin.Id) || (plugin.Id == Plugin.PluginId && plugin.Version < CurrentVersion))
            .ToList();
        var removed = 0;
        foreach (var plugin in legacy)
        {
            if (pluginManager.RemovePlugin(plugin))
            {
                logger.LogWarning("Removed {Plugin} {Version}: it is replaced by Lanternfish {CurrentVersion}", plugin.Name, plugin.Version, CurrentVersion);
                removed++;
            }
            else
            {
                logger.LogError("Could not remove {Plugin} {Version}; uninstall it from Dashboard > Plugins", plugin.Name, plugin.Version);
            }
        }

        if (removed > 0)
        {
            // The removed plugins stay loaded until Jellyfin restarts
            applicationHost.NotifyPendingRestart();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
