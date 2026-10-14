using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using JellyFlix.Configuration;
using MediaBrowser.Common.Net;

namespace JellyFlix.Requests;

/// <summary>
/// The parts of the Seerr (Jellyseerr / Overseerr) API that JellyFlix uses. Calls are made with the server's API key.
/// </summary>
public class SeerrClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Func<PluginConfiguration> _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeerrClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="configuration">Returns the current plugin configuration.</param>
    public SeerrClient(IHttpClientFactory httpClientFactory, Func<PluginConfiguration> configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    /// <summary>
    /// Searches TMDB through Seerr. Results include Seerr's availability and request status.
    /// </summary>
    /// <param name="query">The search text.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The first page of results.</returns>
    public virtual async Task<IReadOnlyList<SeerrSearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var page = await SendAsync<SeerrPage<SeerrSearchResult>>(HttpMethod.Get, $"search?query={Uri.EscapeDataString(query)}&page=1", null, null, cancellationToken).ConfigureAwait(false);
        return page?.Results ?? [];
    }

    /// <summary>
    /// Gets Seerr's users, to find the one linked to a Jellyfin account.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The users.</returns>
    public virtual async Task<IReadOnlyList<SeerrUser>> GetUsersAsync(CancellationToken cancellationToken)
    {
        var page = await SendAsync<SeerrPage<SeerrUser>>(HttpMethod.Get, "user?take=1000", null, null, cancellationToken).ConfigureAwait(false);
        return page?.Results ?? [];
    }

    /// <summary>
    /// Requests a movie, or every season of a show.
    /// </summary>
    /// <param name="mediaType"><c>movie</c> or <c>tv</c>.</param>
    /// <param name="tmdbId">The TMDB id.</param>
    /// <param name="seerrUserId">The Seerr user to request as; the API key's user when null.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public virtual Task CreateRequestAsync(string mediaType, int tmdbId, int? seerrUserId, CancellationToken cancellationToken)
    {
        object body = mediaType == SeerrMediaType.Tv
            ? new { mediaType, mediaId = tmdbId, seasons = "all" }
            : new { mediaType, mediaId = tmdbId };
        return SendAsync<JsonElement>(HttpMethod.Post, "request", body, seerrUserId, cancellationToken);
    }

    /// <summary>
    /// Checks the connection and the API key.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The Seerr version.</returns>
    public virtual async Task<string> GetVersionAsync(CancellationToken cancellationToken)
    {
        // /status is public, so also read something that needs the API key
        var status = await SendAsync<SeerrStatus>(HttpMethod.Get, "status", null, null, cancellationToken).ConfigureAwait(false);
        await SendAsync<JsonElement>(HttpMethod.Get, "settings/main", null, null, cancellationToken).ConfigureAwait(false);
        return status?.Version ?? "unknown";
    }

    private async Task<T?> SendAsync<T>(HttpMethod method, string path, object? body, int? seerrUserId, CancellationToken cancellationToken)
    {
        var config = _configuration();
        if (string.IsNullOrWhiteSpace(config.SeerrUrl) || string.IsNullOrWhiteSpace(config.SeerrApiKey))
        {
            throw new SeerrException(HttpStatusCode.ServiceUnavailable, "Seerr isn't set up.");
        }

        using var request = new HttpRequestMessage(method, new Uri($"{config.SeerrUrl.Trim().TrimEnd('/')}/api/v1/{path}"));
        request.Headers.Add("X-Api-Key", config.SeerrApiKey.Trim());
        if (seerrUserId is not null)
        {
            // Seerr acts as this user when the API key belongs to an administrator
            request.Headers.Add("X-Api-User", seerrUserId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (body is not null)
        {
            // A plain body with a length: some proxies in front of Seerr mishandle chunked requests
            request.Content = new StringContent(JsonSerializer.Serialize(body, body.GetType()), Encoding.UTF8, "application/json");
        }

        HttpResponseMessage response;
        try
        {
            response = await _httpClientFactory.CreateClient(NamedClient.Default).SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new SeerrException(HttpStatusCode.BadGateway, "Can't reach Seerr: " + ex.Message, ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var error = await ReadErrorAsync(response, cancellationToken).ConfigureAwait(false);
                throw new SeerrException(response.StatusCode, error);
            }

            return await response.Content.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<SeerrError>(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(error?.Message))
            {
                return error.Message;
            }
        }
        catch (JsonException)
        {
            // Not a Seerr error body
        }

        return $"Seerr answered {(int)response.StatusCode} {response.ReasonPhrase}.";
    }

    private sealed class SeerrError
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }

    private sealed class SeerrStatus
    {
        [JsonPropertyName("version")]
        public string? Version { get; set; }
    }
}
