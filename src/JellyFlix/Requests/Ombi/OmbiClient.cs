using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JellyFlix.Configuration;
using MediaBrowser.Common.Net;

namespace JellyFlix.Requests.Ombi;

/// <summary>
/// Talks to Ombi's API with the API key from the plugin settings.
/// </summary>
public class OmbiClient
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Func<PluginConfiguration> _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmbiClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="configuration">Returns the current plugin configuration.</param>
    public OmbiClient(IHttpClientFactory httpClientFactory, Func<PluginConfiguration> configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    /// <summary>
    /// Searches TheMovieDb through Ombi for movies and shows.
    /// </summary>
    /// <param name="query">The search text.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The results.</returns>
    public virtual async Task<IReadOnlyList<OmbiSearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
        => await SendAsync<List<OmbiSearchResult>>(
            HttpMethod.Post,
            "v2/Search/multi/" + Uri.EscapeDataString(query),
            new { movies = true, tvShows = true, people = false, music = false },
            null,
            cancellationToken).ConfigureAwait(false) ?? [];

    /// <summary>
    /// Gets where a movie or show stands in Ombi.
    /// </summary>
    /// <param name="mediaType">"movie" or "tv".</param>
    /// <param name="tmdbId">The TheMovieDb id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The details.</returns>
    public virtual Task<OmbiTitleInfo?> GetTitleInfoAsync(string mediaType, int tmdbId, CancellationToken cancellationToken)
        => SendAsync<OmbiTitleInfo>(
            HttpMethod.Get,
            mediaType == SeerrMediaType.Tv ? $"v2/Search/tv/moviedb/{tmdbId}" : $"v2/Search/movie/{tmdbId}",
            null,
            null,
            cancellationToken);

    /// <summary>
    /// Gets Ombi's users.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The users.</returns>
    public virtual async Task<IReadOnlyList<OmbiUser>> GetUsersAsync(CancellationToken cancellationToken)
        => await SendAsync<List<OmbiUser>>(HttpMethod.Get, "v1/Identity/Users", null, null, cancellationToken).ConfigureAwait(false) ?? [];

    /// <summary>
    /// Requests a movie, or every season of a show.
    /// </summary>
    /// <param name="mediaType">"movie" or "tv".</param>
    /// <param name="tmdbId">The TheMovieDb id.</param>
    /// <param name="identity">Who the request is from: an Ombi user to act as, and a name to show otherwise.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public virtual async Task CreateRequestAsync(string mediaType, int tmdbId, (string? UserName, string? Alias) identity, CancellationToken cancellationToken)
    {
        object body = mediaType == SeerrMediaType.Tv
            ? new { theMovieDbId = tmdbId, requestAll = true, latestSeason = false, firstSeason = false, seasons = Array.Empty<object>(), languageCode = "en" }
            : new { theMovieDbId = tmdbId, languageCode = "en", is4kRequest = false };
        var path = mediaType == SeerrMediaType.Tv ? "v2/Requests/tv" : "v1/Request/movie";
        var result = await SendAsync<RequestEngineResult>(HttpMethod.Post, path, body, identity, cancellationToken).ConfigureAwait(false);

        // Ombi answers 200 with the outcome in the body
        if (result is not null && (!result.Result || !string.IsNullOrWhiteSpace(result.ErrorMessage)))
        {
            throw new RequestProviderException(HttpStatusCode.Conflict, result.ErrorMessage ?? result.Message ?? "Ombi didn't accept the request.");
        }
    }

    /// <summary>
    /// Checks the address and API key.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Ombi's version.</returns>
    public virtual async Task<string> GetVersionAsync(CancellationToken cancellationToken)
    {
        var about = await SendAsync<About>(HttpMethod.Get, "v1/Settings/about", null, null, cancellationToken).ConfigureAwait(false);

        // The users list needs the API key's admin rights, so it proves the key works
        await GetUsersAsync(cancellationToken).ConfigureAwait(false);
        return about?.Version ?? "unknown";
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        // Ombi's middleware answers in plain text ("Invalid API Key"), its controllers mostly in JSON
        var text = (await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)).Trim();
        try
        {
            using var json = JsonDocument.Parse(text);
            foreach (var name in new[] { "errorMessage", "message", "title" })
            {
                if (json.RootElement.ValueKind == JsonValueKind.Object
                    && json.RootElement.TryGetProperty(name, out var value)
                    && value.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(value.GetString()))
                {
                    return value.GetString()!;
                }
            }
        }
        catch (JsonException)
        {
            if (text.Length is > 0 and < 200 && !text.StartsWith('<'))
            {
                return text;
            }
        }

        return $"Ombi answered {(int)response.StatusCode} {response.ReasonPhrase}.";
    }

    private async Task<T?> SendAsync<T>(HttpMethod method, string path, object? body, (string? UserName, string? Alias)? identity, CancellationToken cancellationToken)
    {
        var config = _configuration();
        if (string.IsNullOrWhiteSpace(config.OmbiUrl) || string.IsNullOrWhiteSpace(config.OmbiApiKey))
        {
            throw new RequestProviderException(HttpStatusCode.ServiceUnavailable, "Ombi isn't set up.");
        }

        using var request = new HttpRequestMessage(method, new Uri($"{config.OmbiUrl.Trim().TrimEnd('/')}/api/{path}"));
        request.Headers.Add("ApiKey", config.OmbiApiKey.Trim());
        if (!string.IsNullOrWhiteSpace(identity?.UserName))
        {
            // Ombi acts as this user; it must exist, or Ombi refuses the request
            request.Headers.Add("UserName", identity.Value.UserName);
        }

        if (!string.IsNullOrWhiteSpace(identity?.Alias))
        {
            // Shown as who asked when the request is made as the API key's user
            request.Headers.Add("ApiAlias", identity.Value.Alias);
        }

        if (body is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body, body.GetType(), _json), Encoding.UTF8, "application/json");
        }

        HttpResponseMessage response;
        try
        {
            response = await _httpClientFactory.CreateClient(NamedClient.Default).SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new RequestProviderException(HttpStatusCode.BadGateway, "Can't reach Ombi: " + ex.Message, ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new RequestProviderException(response.StatusCode, await ReadErrorAsync(response, cancellationToken).ConfigureAwait(false));
            }

            return await response.Content.ReadFromJsonAsync<T>(_json, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class RequestEngineResult
    {
        public bool Result { get; set; }

        public string? Message { get; set; }

        public string? ErrorMessage { get; set; }
    }

    private sealed class About
    {
        public string? Version { get; set; }
    }
}
