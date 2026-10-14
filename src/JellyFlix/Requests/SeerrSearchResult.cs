using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace JellyFlix.Requests;

/// <summary>
/// A Seerr search result: a movie, show or person from TMDB.
/// </summary>
public class SeerrSearchResult
{
    /// <summary>
    /// Gets or sets the TMDB id.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the media type: <c>movie</c>, <c>tv</c> or <c>person</c>.
    /// </summary>
    [JsonPropertyName("mediaType")]
    public string MediaType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a movie's title.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets a show's name.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets a movie's release date.
    /// </summary>
    [JsonPropertyName("releaseDate")]
    public string? ReleaseDate { get; set; }

    /// <summary>
    /// Gets or sets a show's first air date.
    /// </summary>
    [JsonPropertyName("firstAirDate")]
    public string? FirstAirDate { get; set; }

    /// <summary>
    /// Gets or sets the overview.
    /// </summary>
    [JsonPropertyName("overview")]
    public string? Overview { get; set; }

    /// <summary>
    /// Gets or sets the TMDB poster path.
    /// </summary>
    [JsonPropertyName("posterPath")]
    public string? PosterPath { get; set; }

    /// <summary>
    /// Gets or sets the TMDB popularity.
    /// </summary>
    [JsonPropertyName("popularity")]
    public double Popularity { get; set; }

    /// <summary>
    /// Gets or sets what Seerr knows about the title, if anything.
    /// </summary>
    [JsonPropertyName("mediaInfo")]
    public SeerrMediaInfo? MediaInfo { get; set; }
}
