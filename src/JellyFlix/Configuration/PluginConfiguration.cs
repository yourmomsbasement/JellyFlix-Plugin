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

    /// <summary>
    /// Gets or sets a value indicating whether titles that aren't in the library can be requested through Seerr
    /// (Jellyseerr / Overseerr) from search.
    /// </summary>
    public bool RequestsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the Seerr address, e.g. <c>http://192.168.1.10:5055</c>.
    /// </summary>
    public string SeerrUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Seerr API key (Seerr → Settings → General). It never leaves the server.
    /// </summary>
    public string SeerrApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the request service: "Seerr" or "Ombi".
    /// </summary>
    public string RequestsProvider { get; set; } = "Seerr";

    /// <summary>
    /// Gets or sets the Ombi address, e.g. <c>http://192.168.1.10:3579</c>.
    /// </summary>
    public string OmbiUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Ombi API key (Ombi → Settings → Ombi). It never leaves the server.
    /// </summary>
    public string OmbiApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether kids profiles may request titles.
    /// </summary>
    public bool AllowKidsRequests { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether description search also matches by meaning, using embeddings.
    /// </summary>
    public bool SemanticSearchEnabled { get; set; }

    /// <summary>
    /// Gets or sets the address of an OpenAI-compatible embeddings API, e.g. <c>http://192.168.1.10:11434</c> for Ollama.
    /// </summary>
    public string EmbeddingsUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the embeddings model.
    /// </summary>
    public string EmbeddingsModel { get; set; } = "nomic-embed-text";

    /// <summary>
    /// Gets or sets the embeddings API key, if the API needs one. It never leaves the server.
    /// </summary>
    public string EmbeddingsApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the home page's seasonal look: "Off", "Auto" (by date), "Halloween", "Christmas" or "Summer".
    /// </summary>
    public string SeasonalTheme { get; set; } = "Auto";

    /// <summary>
    /// Gets or sets a value indicating whether buttons, backgrounds and progress bars take their color from the
    /// title's artwork.
    /// </summary>
    public bool DynamicColors { get; set; } = true;
}
