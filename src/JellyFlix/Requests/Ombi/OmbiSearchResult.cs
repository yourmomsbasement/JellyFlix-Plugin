namespace JellyFlix.Requests.Ombi;

/// <summary>
/// A result of Ombi's multi search (<c>POST /api/v2/Search/multi/{term}</c>).
/// </summary>
public class OmbiSearchResult
{
    /// <summary>
    /// Gets or sets the TheMovieDb id, as text.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets "movie", "tv" or "person".
    /// </summary>
    public string? MediaType { get; set; }

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the TheMovieDb poster path, e.g. "/abc.jpg".
    /// </summary>
    public string? Poster { get; set; }

    /// <summary>
    /// Gets or sets the plot.
    /// </summary>
    public string? Overview { get; set; }
}
