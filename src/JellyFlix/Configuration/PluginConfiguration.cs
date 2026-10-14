using MediaBrowser.Model.Plugins;

namespace JellyFlix.Configuration;

/// <summary>
/// Server-wide JellyFlix settings.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the maximum number of profiles in a household, including the owner's own profile.
    /// </summary>
    public int MaxProfilesPerHousehold { get; set; } = 5;

    /// <summary>
    /// Gets or sets a value indicating whether non-administrator accounts may create profiles.
    /// </summary>
    public bool AllowNonAdminOwners { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum parental rating score applied to kids profiles (10 = PG / TV-PG in the US rating system).
    /// </summary>
    public int KidsMaxParentalRating { get; set; } = 10;

    /// <summary>
    /// Gets or sets a value indicating whether the Web Updater installs new JellyFlix web client releases.
    /// </summary>
    public bool WebUpdatesEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the plugin serves the installed JellyFlix web client at <c>/web</c>, in
    /// place of the one that ships with Jellyfin. Turn off to get Jellyfin's stock web client back.
    /// </summary>
    public bool ServeWebClient { get; set; } = true;

    /// <summary>
    /// Gets or sets the GitHub repository ("owner/name") whose releases provide the JellyFlix web client.
    /// </summary>
    public string WebReleasesRepository { get; set; } = "yourmomsbasement/JellyFlix-Web";

    /// <summary>
    /// Gets or sets the GitHub API base URL. Only changed for testing or GitHub Enterprise.
    /// </summary>
    public string GitHubApiUrl { get; set; } = "https://api.github.com";
}
