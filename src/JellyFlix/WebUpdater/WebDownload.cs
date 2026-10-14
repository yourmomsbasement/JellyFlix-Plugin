using System;
using System.IO;
using System.Threading.Tasks;

namespace JellyFlix.WebUpdater;

/// <summary>
/// A download in progress: its contents and, when known, its size.
/// </summary>
public sealed class WebDownload : IAsyncDisposable
{
    private readonly IDisposable? _owner;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebDownload"/> class.
    /// </summary>
    /// <param name="content">The contents.</param>
    /// <param name="length">The size in bytes, if known.</param>
    /// <param name="owner">Disposed with the download, e.g. the HTTP response.</param>
    public WebDownload(Stream content, long? length, IDisposable? owner = null)
    {
        Content = content;
        Length = length;
        _owner = owner;
    }

    /// <summary>
    /// Gets the contents.
    /// </summary>
    public Stream Content { get; }

    /// <summary>
    /// Gets the size in bytes, if known.
    /// </summary>
    public long? Length { get; }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await Content.DisposeAsync().ConfigureAwait(false);
        _owner?.Dispose();
    }
}
