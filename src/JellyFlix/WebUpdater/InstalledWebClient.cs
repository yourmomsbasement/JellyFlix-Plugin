using System;

namespace JellyFlix.WebUpdater;

/// <summary>
/// The build in the install folder, recorded in <c>jellyflix-release.json</c> next to the web files.
/// </summary>
public class InstalledWebClient
{
    /// <summary>
    /// Gets or sets the build version.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the JellyFlix-Web commit it was built from.
    /// </summary>
    public string Commit { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Jellyfin version it was built for.
    /// </summary>
    public string ServerVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when it was installed.
    /// </summary>
    public DateTimeOffset InstalledAt { get; set; }
}
