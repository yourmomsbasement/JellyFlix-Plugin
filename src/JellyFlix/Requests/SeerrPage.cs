using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace JellyFlix.Requests;

/// <summary>
/// A page of Seerr results.
/// </summary>
/// <typeparam name="T">The result type.</typeparam>
public class SeerrPage<T>
{
    /// <summary>
    /// Gets or sets the results.
    /// </summary>
    [JsonPropertyName("results")]
    public IReadOnlyList<T> Results { get; set; } = [];
}
