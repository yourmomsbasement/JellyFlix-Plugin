namespace JellyFlix.Artwork.Api.Models;

/// <summary>
/// An image a profile can choose.
/// </summary>
public class ArtworkOptionDto
{
    /// <summary>
    /// Gets or sets the option's id, to choose it with.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a preview: a path under the server ("Items/…") or a full URL.
    /// </summary>
    public string PreviewUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets where it comes from: "Library", or the metadata provider.
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the width, if known.
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Gets or sets the height, if known.
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    /// Gets or sets the language, if it has text.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is what everyone sees.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this profile sees it.
    /// </summary>
    public bool IsSelected { get; set; }
}
