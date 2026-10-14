using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;

namespace JellyFlix.Search;

/// <summary>
/// Embedding vectors for the library's titles, kept in a file so they're only computed once per title.
/// </summary>
public class EmbeddingStore
{
    private readonly string _path;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddingStore"/> class.
    /// </summary>
    /// <param name="applicationPaths">The application paths.</param>
    public EmbeddingStore(IApplicationPaths applicationPaths)
    {
        _path = Path.Combine(applicationPaths.DataPath, "jellyflix-search", "embeddings.json");
    }

    /// <summary>
    /// Loads the stored vectors.
    /// </summary>
    /// <returns>The vectors, or an empty set.</returns>
    public virtual async Task<EmbeddingSet> LoadAsync()
    {
        if (!File.Exists(_path))
        {
            return new EmbeddingSet();
        }

        try
        {
            var stream = File.OpenRead(_path);
            await using (stream.ConfigureAwait(false))
            {
                return await JsonSerializer.DeserializeAsync<EmbeddingSet>(stream).ConfigureAwait(false) ?? new EmbeddingSet();
            }
        }
        catch (JsonException)
        {
            return new EmbeddingSet();
        }
    }

    /// <summary>
    /// Saves the vectors.
    /// </summary>
    /// <param name="set">The vectors.</param>
    /// <returns>A task.</returns>
    public virtual async Task SaveAsync(EmbeddingSet set)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp";
        var stream = File.Create(temp);
        await using (stream.ConfigureAwait(false))
        {
            await JsonSerializer.SerializeAsync(stream, set).ConfigureAwait(false);
        }

        File.Move(temp, _path, true);
    }
}
