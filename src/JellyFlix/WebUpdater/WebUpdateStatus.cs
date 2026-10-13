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
    /// Gets or sets a value indicating whether Jellyfin serves the web client the updater installs.
    /// When false, updates are downloaded but users don't see them until Jellyfin's web folder is pointed at <see cref="InstallPath"/>.
    /// </summary>
    public bool IsServerUsingInstall { get; set; }

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
