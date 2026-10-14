using System.ComponentModel.DataAnnotations;

namespace JellyFlix.Artwork.Api.Models;

/// <summary>
/// Choosing an image.
/// </summary>
public class ChooseArtworkDto
{
    /// <summary>
    /// Gets or sets poster or backdrop.
    /// </summary>
    public ArtworkKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the option's id, from the options list.
    /// </summary>
    [Required]
    public string OptionId { get; set; } = string.Empty;
}
