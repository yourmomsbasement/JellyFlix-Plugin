namespace JellyFlix.Requests.Ombi;

/// <summary>
/// The parts of Ombi's movie and show details that say where a title stands
/// (<c>GET /api/v2/Search/movie/{id}</c> and <c>GET /api/v2/Search/tv/moviedb/{id}</c>).
/// </summary>
public class OmbiTitleInfo
{
    /// <summary>
    /// Gets or sets a value indicating whether it's requested.
    /// </summary>
    public bool Requested { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the request is approved.
    /// </summary>
    public bool Approved { get; set; }

    /// <summary>
    /// Gets or sets whether the request was denied.
    /// </summary>
    public bool? Denied { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a movie is available.
    /// </summary>
    public bool Available { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether every episode of a show is available.
    /// </summary>
    public bool FullyAvailable { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether some of a show is available.
    /// </summary>
    public bool PartlyAvailable { get; set; }

    /// <summary>
    /// Gets or sets a movie's release date, e.g. "2021-09-15T00:00:00".
    /// </summary>
    public string? ReleaseDate { get; set; }

    /// <summary>
    /// Gets or sets a show's first air date.
    /// </summary>
    public string? FirstAired { get; set; }
}
