namespace JellyFlix.Search.Api.Models;

/// <summary>
/// The result of testing the embeddings API.
/// </summary>
public class SemanticTestDto
{
    /// <summary>
    /// Gets or sets the model that answered.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the size of the model's vectors.
    /// </summary>
    public int Dimensions { get; set; }
}
