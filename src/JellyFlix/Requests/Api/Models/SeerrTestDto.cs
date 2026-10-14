using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace JellyFlix.Requests.Api.Models;

/// <summary>
/// The result of testing the request service's connection.
/// </summary>
public class SeerrTestDto
{
    /// <summary>
    /// Gets or sets the service's version.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets which service was tested: "Seerr" or "Ombi".
    /// </summary>
    public string Provider { get; set; } = string.Empty;
}
