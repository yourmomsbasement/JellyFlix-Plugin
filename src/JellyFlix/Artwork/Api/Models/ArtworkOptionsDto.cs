using System.Collections.Generic;

namespace JellyFlix.Artwork.Api.Models;

/// <summary>
/// The images a profile can choose from for a title.
/// </summary>
public class ArtworkOptionsDto
{
    /// <summary>
    /// Gets or sets the options: the library's own first.
    /// </summary>
    public IReadOnlyList<ArtworkOptionDto> Options { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the metadata providers couldn't be reached, so only the library's own are listed.
    /// </summary>
    public bool ProvidersFailed { get; set; }
}
