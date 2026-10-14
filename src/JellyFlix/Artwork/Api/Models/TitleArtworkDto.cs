namespace JellyFlix.Artwork.Api.Models;

/// <summary>
/// The images a profile sees for a title instead of the library's: paths under the server, or null for the library's.
/// </summary>
public class TitleArtworkDto
{
    /// <summary>
    /// Gets or sets the poster.
    /// </summary>
    public string? Poster { get; set; }

    /// <summary>
    /// Gets or sets the backdrop.
    /// </summary>
    public string? Backdrop { get; set; }
}
