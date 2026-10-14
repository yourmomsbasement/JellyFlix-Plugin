namespace JellyFlix.Artwork;

/// <summary>
/// The image a profile chose for a title: one of the library's own, or a downloaded alternative.
/// </summary>
public class ArtworkChoice
{
    /// <summary>
    /// Gets or sets the index of the library image, when one of the library's own was chosen.
    /// </summary>
    public int? ImageIndex { get; set; }

    /// <summary>
    /// Gets or sets the downloaded file's name, when an alternative from a metadata provider was chosen.
    /// </summary>
    public string? File { get; set; }

    /// <summary>
    /// Gets or sets where the downloaded file came from.
    /// </summary>
    public string? SourceUrl { get; set; }
}
