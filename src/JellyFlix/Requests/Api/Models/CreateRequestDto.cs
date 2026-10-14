using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace JellyFlix.Requests.Api.Models;

/// <summary>
/// A request for a movie or show.
/// </summary>
public class CreateRequestDto
{
    /// <summary>
    /// Gets or sets the TMDB id.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int TmdbId { get; set; }

    /// <summary>
    /// Gets or sets <c>movie</c> or <c>tv</c>.
    /// </summary>
    [Required]
    public string MediaType { get; set; } = string.Empty;
}
