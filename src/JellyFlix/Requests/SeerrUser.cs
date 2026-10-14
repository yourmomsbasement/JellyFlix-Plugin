using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace JellyFlix.Requests;

/// <summary>
/// A Seerr user.
/// </summary>
public class SeerrUser
{
    /// <summary>
    /// Gets or sets the Seerr user id.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the linked Jellyfin user id, for users imported from Jellyfin.
    /// </summary>
    [JsonPropertyName("jellyfinUserId")]
    public string? JellyfinUserId { get; set; }
}
