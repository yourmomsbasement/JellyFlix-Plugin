using System;

namespace JellyFlix.Search.Api.Models;

/// <summary>
/// The state of description search.
/// </summary>
public class SearchStatusDto
{
    /// <summary>
    /// Gets or sets a value indicating whether semantic search is turned on and set up.
    /// </summary>
    public bool IsSemanticEnabled { get; set; }

    /// <summary>
    /// Gets or sets how many titles the text index holds.
    /// </summary>
    public int Titles { get; set; }

    /// <summary>
    /// Gets or sets how many titles have embeddings for the configured model.
    /// </summary>
    public int EmbeddedTitles { get; set; }

    /// <summary>
    /// Gets or sets the model the stored embeddings came from.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Gets or sets when the embeddings were last built since the server started.
    /// </summary>
    public DateTimeOffset? LastBuild { get; set; }

    /// <summary>
    /// Gets or sets how far the build that's running has got, from 0 to 100, or null when none is.
    /// </summary>
    public double? BuildProgress { get; set; }

    /// <summary>
    /// Gets or sets what the last build did.
    /// </summary>
    public string? LastBuildResult { get; set; }
}
