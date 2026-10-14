using System;

namespace JellyFlix.WebUpdater;

/// <summary>
/// The Web Updater's state, shown on the JellyFlix settings page.
/// </summary>
public class WebUpdateStatus
{
    /// <summary>
    /// Gets or sets a value indicating whether automatic updates are on.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the folder the Web Updater installs the web client into.
    /// </summary>
    public string InstallPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the folder Jellyfin currently serves the web client from.
    /// </summary>
    public string ServerWebPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether users get the web client the updater installs, either served by the
    /// plugin or because Jellyfin's web folder points at <see cref="InstallPath"/>.
    /// </summary>
    public bool IsServerUsingInstall { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the plugin serves the installed web client in place of Jellyfin's own.
    /// </summary>
    public bool IsServedByPlugin { get; set; }

    /// <summary>
    /// Gets or sets the installed build, if any.
    /// </summary>
    public InstalledWebClient? Installed { get; set; }

    /// <summary>
    /// Gets or sets the newest build compatible with this server, from the last check.
    /// </summary>
    public string? LatestVersion { get; set; }

    /// <summary>
    /// Gets or sets when the last check ran.
    /// </summary>
    public DateTimeOffset? LastCheck { get; set; }

    /// <summary>
    /// Gets or sets the outcome of the last check, in plain words.
    /// </summary>
    public string? LastResult { get; set; }
}
