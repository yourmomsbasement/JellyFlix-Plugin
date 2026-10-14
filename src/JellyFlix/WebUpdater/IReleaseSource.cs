using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace JellyFlix.WebUpdater;

/// <summary>
/// Where JellyFlix web client releases come from.
/// </summary>
public interface IReleaseSource
{
    /// <summary>
    /// Lists recent releases, newest first. Releases are read one at a time, so callers can stop at the first one they
    /// can use.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The releases.</returns>
    IAsyncEnumerable<WebRelease> GetReleasesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Downloads a release asset.
    /// </summary>
    /// <param name="url">The asset URL.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The download; the caller disposes it.</returns>
    Task<WebDownload> DownloadAsync(Uri url, CancellationToken cancellationToken);
}
