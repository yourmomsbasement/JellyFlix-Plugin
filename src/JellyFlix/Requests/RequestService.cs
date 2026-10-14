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
/// Lets profiles find titles that aren't in the library and request them through Seerr.
/// </summary>
public class RequestService
{
    private const string PosterBaseUrl = "https://image.tmdb.org/t/p/w342";
    private const int MaxResults = 20;
    private static readonly TimeSpan _usersCacheTime = TimeSpan.FromMinutes(10);

    private readonly SeerrClient _seerr;
    private readonly IRequesterResolver _requesters;
    private readonly Func<PluginConfiguration> _configuration;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RequestService> _logger;

    private IReadOnlyList<SeerrUser>? _users;
    private DateTimeOffset _usersLoadedAt;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestService"/> class.
    /// </summary>
    /// <param name="seerr">The Seerr client.</param>
    /// <param name="requesters">Finds who requests are made for.</param>
    /// <param name="configuration">Returns the current plugin configuration.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="logger">The logger.</param>
    public RequestService(SeerrClient seerr, IRequesterResolver requesters, Func<PluginConfiguration> configuration, TimeProvider timeProvider, ILogger<RequestService> logger)
    {
        _seerr = seerr;
        _requesters = requesters;
        _configuration = configuration;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Gets a value indicating whether requests are turned on and Seerr is set up.
    /// </summary>
    public bool IsEnabled
    {
        get
        {
            var config = _configuration();
            return config.RequestsEnabled && !string.IsNullOrWhiteSpace(config.SeerrUrl) && !string.IsNullOrWhiteSpace(config.SeerrApiKey);
        }
    }

    /// <summary>
    /// Finds movies and shows matching a search that aren't in the library yet.
    /// </summary>
    /// <param name="userId">The signed-in user.</param>
    /// <param name="query">The search text.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The titles and whether this profile may request them.</returns>
    public async Task<RequestSearchDto> SearchAsync(Guid userId, string query, CancellationToken cancellationToken)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(query))
        {
            return new RequestSearchDto { IsEnabled = IsEnabled };
        }

        var requester = await _requesters.ResolveAsync(userId).ConfigureAwait(false);
        var results = await _seerr.SearchAsync(query.Trim(), cancellationToken).ConfigureAwait(false);

        return new RequestSearchDto
        {
            IsEnabled = true,
            CanRequest = CanRequest(requester),
            Results = results
                .Where(r => r.MediaType is SeerrMediaType.Movie or SeerrMediaType.Tv)
                .Where(r => r.MediaInfo?.Status != SeerrMediaStatus.Available)
                .Take(MaxResults)
                .Select(ToDto)
                .ToList()
        };
    }

    /// <summary>
    /// Requests a movie, or every season of a show, for the user's household.
    /// </summary>
    /// <param name="userId">The signed-in user.</param>
    /// <param name="request">What to request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task RequestAsync(Guid userId, CreateRequestDto request, CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            throw new SeerrException(HttpStatusCode.ServiceUnavailable, "Requests aren't turned on.");
        }

        if (request.MediaType is not (SeerrMediaType.Movie or SeerrMediaType.Tv))
        {
            throw new SeerrException(HttpStatusCode.BadRequest, "Only movies and shows can be requested.");
        }

        var requester = await _requesters.ResolveAsync(userId).ConfigureAwait(false);
        if (!CanRequest(requester))
        {
            throw new SeerrException(HttpStatusCode.Forbidden, "Kids profiles can't request titles.");
        }

        var seerrUserId = await FindSeerrUserAsync(requester.OwnerId, cancellationToken).ConfigureAwait(false);
        if (seerrUserId is null)
        {
            _logger.LogInformation("No Seerr user is linked to Jellyfin user {UserId}; requesting as the API key's user", requester.OwnerId);
        }

        await _seerr.CreateRequestAsync(request.MediaType, request.TmdbId, seerrUserId, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Requested {MediaType} {TmdbId} through Seerr for {UserId}", request.MediaType, request.TmdbId, requester.OwnerId);
    }

    /// <summary>
    /// Checks the Seerr connection.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The Seerr version.</returns>
    public Task<string> TestAsync(CancellationToken cancellationToken) => _seerr.GetVersionAsync(cancellationToken);

    /// <summary>
    /// Maps Seerr's status to the one the web client shows.
    /// </summary>
    /// <param name="mediaInfo">Seerr's media info, if any.</param>
    /// <returns>The status.</returns>
    internal static RequestStatus GetStatus(SeerrMediaInfo? mediaInfo) => mediaInfo?.Status switch
    {
        SeerrMediaStatus.Pending => RequestStatus.Requested,
        SeerrMediaStatus.Processing => RequestStatus.Processing,
        SeerrMediaStatus.PartiallyAvailable => RequestStatus.PartiallyAvailable,
        SeerrMediaStatus.Available => RequestStatus.Available,
        SeerrMediaStatus.Blocklisted => RequestStatus.Blocked,
        _ => RequestStatus.None
    };

    private static RequestableTitleDto ToDto(SeerrSearchResult result)
    {
        var date = result.MediaType == SeerrMediaType.Movie ? result.ReleaseDate : result.FirstAirDate;
        return new RequestableTitleDto
        {
            TmdbId = result.Id,
            MediaType = result.MediaType,
            Title = (result.MediaType == SeerrMediaType.Movie ? result.Title : result.Name) ?? string.Empty,
            Year = date?.Length >= 4 && int.TryParse(date.AsSpan(0, 4), out var year) ? year : null,
            Overview = result.Overview,
            PosterUrl = string.IsNullOrEmpty(result.PosterPath) ? null : PosterBaseUrl + result.PosterPath,
            Status = GetStatus(result.MediaInfo)
        };
    }

    private bool CanRequest(Requester requester) => !requester.IsKids || _configuration().AllowKidsRequests;

    private async Task<int?> FindSeerrUserAsync(Guid jellyfinUserId, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        if (_users is null || now - _usersLoadedAt > _usersCacheTime)
        {
            _users = await _seerr.GetUsersAsync(cancellationToken).ConfigureAwait(false);
            _usersLoadedAt = now;
        }

        return _users.FirstOrDefault(u => Guid.TryParse(u.JellyfinUserId, out var id) && id.Equals(jellyfinUserId))?.Id;
    }
}
