using System;
using System.Collections.Generic;

namespace JellyFlix.Search;

/// <summary>
/// Stored title embeddings, for one model.
/// </summary>
public class EmbeddingSet
{
    /// <summary>
    /// Gets or sets the model the vectors came from; vectors from another model can't be compared.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the set was last brought up to date.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the vectors by title id.
    /// </summary>
#pragma warning disable CA2227 // A settable dictionary keeps the stored JSON simple to read and write
    public Dictionary<Guid, TitleEmbedding> Titles { get; set; } = [];
#pragma warning restore CA2227
}
