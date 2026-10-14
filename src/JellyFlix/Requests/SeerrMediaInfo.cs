using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace JellyFlix.Requests;

/// <summary>
/// Seerr's availability and request state for a title.
/// </summary>
public class SeerrMediaInfo
{
    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    [JsonPropertyName("status")]
    public SeerrMediaStatus Status { get; set; } = SeerrMediaStatus.Unknown;
}
