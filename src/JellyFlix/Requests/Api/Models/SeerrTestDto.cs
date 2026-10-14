using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace JellyFlix.Requests.Api.Models;

/// <summary>
/// The result of testing the Seerr connection.
/// </summary>
public class SeerrTestDto
{
    /// <summary>
    /// Gets or sets the Seerr version.
    /// </summary>
    public string Version { get; set; } = string.Empty;
}
