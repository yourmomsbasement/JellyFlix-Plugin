using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace JellyFlix.WebUpdater;

/// <summary>
/// Where JellyFlix web client releases come from.
/// </summary>
public interface IReleaseSource
{
    /// <summary>
    /// Lists recent releases, newest first.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The releases.</returns>
    Task<IReadOnlyList<WebRelease>> GetReleasesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Downloads a release asset.
    /// </summary>
    /// <param name="url">The asset URL.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The asset contents; the caller disposes it.</returns>
    Task<Stream> DownloadAsync(Uri url, CancellationToken cancellationToken);
}
