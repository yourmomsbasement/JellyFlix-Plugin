using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JellyFlix.Configuration;
using JellyFlix.Requests.Api.Models;
using Microsoft.Extensions.Logging;

namespace JellyFlix.Requests;

/// <summary>
/// Requests through Seerr (Jellyseerr / Overseerr).
/// </summary>
public class SeerrRequestProvider : IRequestProvider
{
    /// <summary>
    /// Where TheMovieDb posters are served from.
    /// </summary>
    public const string PosterBaseUrl = "https://image.tmdb.org/t/p/w342";

    private static readonly TimeSpan _usersCacheTime = TimeSpan.FromMinutes(10);

    private readonly SeerrClient _seerr;
    private readonly Func<PluginConfiguration> _configuration;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SeerrRequestProvider> _logger;

    private IReadOnlyList<SeerrUser>? _users;
    private DateTimeOffset _usersLoadedAt;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeerrRequestProvider"/> class.
    /// </summary>
    /// <param name="seerr">The Seerr client.</param>
    /// <param name="configuration">Returns the current plugin configuration.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="logger">The logger.</param>
    public SeerrRequestProvider(SeerrClient seerr, Func<PluginConfiguration> configuration, TimeProvider timeProvider, ILogger<SeerrRequestProvider> logger)
    {
        _seerr = seerr;
        _configuration = configuration;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Seerr";

    /// <inheritdoc />
    public bool IsConfigured
    {
        get
        {
            var config = _configuration();
            return !string.IsNullOrWhiteSpace(config.SeerrUrl) && !string.IsNullOrWhiteSpace(config.SeerrApiKey);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RequestableTitleDto>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var results = await _seerr.SearchAsync(query, cancellationToken).ConfigureAwait(false);
        return results
            .Where(r => r.MediaType is SeerrMediaType.Movie or SeerrMediaType.Tv)
            .Select(ToDto)
            .ToList();
    }

    /// <inheritdoc />
    public async Task RequestAsync(Guid ownerId, string mediaType, int tmdbId, CancellationToken cancellationToken)
    {
        var seerrUserId = await FindSeerrUserAsync(ownerId, cancellationToken).ConfigureAwait(false);
        if (seerrUserId is null)
        {
            _logger.LogInformation("No Seerr user is linked to Jellyfin user {UserId}; requesting as the API key's user", ownerId);
        }

        await _seerr.CreateRequestAsync(mediaType, tmdbId, seerrUserId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<string> GetVersionAsync(CancellationToken cancellationToken) => _seerr.GetVersionAsync(cancellationToken);

    /// <summary>
    /// Where a title stands, from Seerr's media info.
    /// </summary>
    /// <param name="mediaInfo">Seerr's media info, if it knows the title.</param>
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
