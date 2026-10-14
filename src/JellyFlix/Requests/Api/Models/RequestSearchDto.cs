using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace JellyFlix.Requests.Api.Models;

/// <summary>
/// Titles matching a search that aren't in the library.
/// </summary>
public class RequestSearchDto
{
    /// <summary>
    /// Gets or sets a value indicating whether requests are turned on.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this profile may request titles.
    /// </summary>
    public bool CanRequest { get; set; }

    /// <summary>
    /// Gets or sets the titles.
    /// </summary>
    public IReadOnlyList<RequestableTitleDto> Results { get; set; } = [];
}
