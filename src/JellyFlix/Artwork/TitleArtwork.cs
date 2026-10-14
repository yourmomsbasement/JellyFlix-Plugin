using System.Text.Json.Serialization;

namespace JellyFlix.Artwork;

/// <summary>
/// A profile's choices for one title.
/// </summary>
public class TitleArtwork
{
    /// <summary>
    /// Gets or sets the chosen poster, or null for the library's.
    /// </summary>
    public ArtworkChoice? Poster { get; set; }

    /// <summary>
    /// Gets or sets the chosen backdrop, or null for the library's.
    /// </summary>
    public ArtworkChoice? Backdrop { get; set; }

    /// <summary>
    /// Gets a value indicating whether nothing is chosen.
    /// </summary>
    [JsonIgnore]
    public bool IsEmpty => Poster is null && Backdrop is null;

    /// <summary>
    /// Gets the choice of a kind.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The choice, or null.</returns>
    public ArtworkChoice? Get(ArtworkKind kind) => kind == ArtworkKind.Poster ? Poster : Backdrop;

    /// <summary>
    /// Sets the choice of a kind.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="choice">The choice, or null for the library's.</param>
    public void Set(ArtworkKind kind, ArtworkChoice? choice)
    {
        if (kind == ArtworkKind.Poster)
        {
            Poster = choice;
        }
        else
        {
            Backdrop = choice;
        }
    }
}
