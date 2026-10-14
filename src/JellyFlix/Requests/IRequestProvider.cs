using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JellyFlix.Requests.Api.Models;

namespace JellyFlix.Requests;

/// <summary>
/// A request service titles can be requested through: Seerr or Ombi.
/// </summary>
public interface IRequestProvider
{
    /// <summary>
    /// Gets the service's name, as the setting and the dashboard call it.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a value indicating whether the service's address and API key are set.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Finds movies and shows matching a search, with where each stands.
    /// </summary>
    /// <param name="query">The search text.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The titles, the service's best matches first.</returns>
    Task<IReadOnlyList<RequestableTitleDto>> SearchAsync(string query, CancellationToken cancellationToken);

    /// <summary>
    /// Requests a movie, or every season of a show, on behalf of a household.
    /// </summary>
    /// <param name="ownerId">The household owner's Jellyfin user id; the request is made as their account where possible.</param>
    /// <param name="mediaType">"movie" or "tv".</param>
    /// <param name="tmdbId">The title's TheMovieDb id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task RequestAsync(Guid ownerId, string mediaType, int tmdbId, CancellationToken cancellationToken);

    /// <summary>
    /// Checks the connection and API key.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The service's version.</returns>
    Task<string> GetVersionAsync(CancellationToken cancellationToken);
}
