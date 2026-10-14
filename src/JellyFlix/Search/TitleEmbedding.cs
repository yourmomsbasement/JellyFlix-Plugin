namespace JellyFlix.Search;

/// <summary>
/// A title's embedding and a fingerprint of the text it was made from.
/// </summary>
public class TitleEmbedding
{
    /// <summary>
    /// Gets or sets a hash of the embedded text; when the metadata changes, the vector is recomputed.
    /// </summary>
    public string TextHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the unit-length vector.
    /// </summary>
#pragma warning disable CA1819 // Stored as-is in the JSON file
    public float[] Vector { get; set; } = [];
#pragma warning restore CA1819
}
