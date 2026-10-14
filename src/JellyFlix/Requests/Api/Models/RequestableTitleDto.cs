using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace JellyFlix.Requests.Api.Models;

/// <summary>
/// A movie or show that isn't in the library.
/// </summary>
public class RequestableTitleDto
{
    /// <summary>
    /// Gets or sets the TMDB id.
    /// </summary>
    public int TmdbId { get; set; }

    /// <summary>
    /// Gets or sets <c>movie</c> or <c>tv</c>.
    /// </summary>
    public string MediaType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the year it came out.
    /// </summary>
    public int? Year { get; set; }

    /// <summary>
    /// Gets or sets the overview.
    /// </summary>
    public string? Overview { get; set; }

    /// <summary>
    /// Gets or sets the TMDB poster URL.
    /// </summary>
    public string? PosterUrl { get; set; }

    /// <summary>
    /// Gets or sets where the title stands.
    /// </summary>
    public RequestStatus Status { get; set; }
}
