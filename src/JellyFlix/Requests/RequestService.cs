using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using JellyFlix.Configuration;
using JellyFlix.Requests.Api.Models;
using Microsoft.Extensions.Logging;

namespace JellyFlix.Requests;

/// <summary>
/// Lets profiles find titles that aren't in the library and request them through Seerr or Ombi.
/// </summary>
public class RequestService
{
    private const int MaxResults = 20;

    private readonly IReadOnlyList<IRequestProvider> _providers;
    private readonly IRequesterResolver _requesters;
    private readonly Func<PluginConfiguration> _configuration;
    private readonly ILogger<RequestService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestService"/> class.
    /// </summary>
    /// <param name="providers">The request services titles can be requested through.</param>
    /// <param name="requesters">Works out who a request is for.</param>
    /// <param name="configuration">Returns the current plugin configuration.</param>
    /// <param name="logger">The logger.</param>
    public RequestService(IEnumerable<IRequestProvider> providers, IRequesterResolver requesters, Func<PluginConfiguration> configuration, ILogger<RequestService> logger)
    {
        _providers = providers.ToList();
        _requesters = requesters;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Gets the request service chosen in the settings.
    /// </summary>
    public IRequestProvider Provider
        => _providers.FirstOrDefault(p => string.Equals(p.Name, _configuration().RequestsProvider, StringComparison.OrdinalIgnoreCase))
            ?? _providers[0];

    /// <summary>
    /// Gets a value indicating whether requests are turned on and the chosen service is set up.
    /// </summary>
    public bool IsEnabled => _configuration().RequestsEnabled && Provider.IsConfigured;

    /// <summary>
    /// Finds movies and shows matching a search that aren't in the library.
    /// </summary>
    /// <param name="userId">The signed-in profile.</param>
    /// <param name="query">The search text.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The titles, and whether the profile may request them.</returns>
    public async Task<RequestSearchDto> SearchAsync(Guid userId, string query, CancellationToken cancellationToken)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(query))
        {
            return new RequestSearchDto { IsEnabled = IsEnabled };
        }

        var requester = await _requesters.ResolveAsync(userId).ConfigureAwait(false);
        var results = await Provider.SearchAsync(query.Trim(), cancellationToken).ConfigureAwait(false);

        return new RequestSearchDto
        {
            IsEnabled = true,
            CanRequest = CanRequest(requester),
            Results = results.Where(r => r.Status != RequestStatus.Available).Take(MaxResults).ToList()
        };
    }

    /// <summary>
    /// Requests a movie, or every season of a show, as the household owner.
    /// </summary>
    /// <param name="userId">The signed-in profile.</param>
    /// <param name="request">What to request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task RequestAsync(Guid userId, CreateRequestDto request, CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            throw new RequestProviderException(HttpStatusCode.ServiceUnavailable, "Requests aren't turned on.");
        }

        if (request.MediaType is not (SeerrMediaType.Movie or SeerrMediaType.Tv))
        {
            throw new RequestProviderException(HttpStatusCode.BadRequest, "Only movies and shows can be requested.");
        }

        var requester = await _requesters.ResolveAsync(userId).ConfigureAwait(false);
        if (!CanRequest(requester))
        {
            throw new RequestProviderException(HttpStatusCode.Forbidden, "Kids profiles can't request titles.");
        }

        var provider = Provider;
        await provider.RequestAsync(requester.OwnerId, request.MediaType, request.TmdbId, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Requested {MediaType} {TmdbId} through {Provider} for {UserId}", request.MediaType, request.TmdbId, provider.Name, requester.OwnerId);
    }

    /// <summary>
    /// Checks the chosen service's connection with the saved settings.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The service and its version.</returns>
    public async Task<SeerrTestDto> TestAsync(CancellationToken cancellationToken)
    {
        var provider = Provider;
        return new SeerrTestDto { Provider = provider.Name, Version = await provider.GetVersionAsync(cancellationToken).ConfigureAwait(false) };
    }

    private bool CanRequest(Requester requester) => !requester.IsKids || _configuration().AllowKidsRequests;
}
