using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace JellyFlix.Requests;

/// <summary>
/// Seerr's media types.
/// </summary>
public static class SeerrMediaType
{
    /// <summary>
    /// A movie.
    /// </summary>
    public const string Movie = "movie";

    /// <summary>
    /// A show.
    /// </summary>
    public const string Tv = "tv";
}
