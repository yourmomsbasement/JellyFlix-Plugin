using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using JellyFlix.Configuration;
using MediaBrowser.Common.Net;

namespace JellyFlix.Search;

/// <summary>
/// Turns text into embedding vectors through an OpenAI-compatible <c>/v1/embeddings</c> API: OpenAI itself, or a
/// local server such as Ollama or LM Studio.
/// </summary>
public class EmbeddingClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Func<PluginConfiguration> _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddingClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="configuration">Returns the current plugin configuration.</param>
    public EmbeddingClient(IHttpClientFactory httpClientFactory, Func<PluginConfiguration> configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    /// <summary>
    /// Gets the configured model name.
    /// </summary>
    public string Model => _configuration().EmbeddingsModel.Trim();

    /// <summary>
    /// Embeds texts.
    /// </summary>
    /// <param name="texts">The texts.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>One unit-length vector per text, in order.</returns>
    public virtual async Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        var config = _configuration();
        if (string.IsNullOrWhiteSpace(config.EmbeddingsUrl) || string.IsNullOrWhiteSpace(config.EmbeddingsModel))
        {
            throw new InvalidOperationException("Semantic search isn't set up.");
        }

        // Accept the server address ("http://ollama:11434"), the API base ("…/v1") or the full endpoint
        var url = config.EmbeddingsUrl.Trim().TrimEnd('/');
        if (!url.EndsWith("/embeddings", StringComparison.OrdinalIgnoreCase))
        {
            url += url.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) ? "/embeddings" : "/v1/embeddings";
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(url));
        if (!string.IsNullOrWhiteSpace(config.EmbeddingsApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.EmbeddingsApiKey.Trim());
        }

        var body = JsonSerializer.Serialize(new { model = config.EmbeddingsModel.Trim(), input = texts });
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await _httpClientFactory.CreateClient(NamedClient.Default).SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException($"The embeddings API answered {(int)response.StatusCode}: {Truncate(error, 300)}");
        }

        var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken).ConfigureAwait(false);
        var data = result?.Data ?? [];
        if (data.Count != texts.Count)
        {
            throw new InvalidOperationException($"The embeddings API returned {data.Count} vectors for {texts.Count} texts.");
        }

        return data.OrderBy(d => d.Index).Select(d => Normalize(d.Embedding)).ToList();
    }

    /// <summary>
    /// Scales a vector to unit length, so a dot product is the cosine similarity.
    /// </summary>
    /// <param name="vector">The vector.</param>
    /// <returns>The unit vector.</returns>
    internal static float[] Normalize(float[] vector)
    {
        double sum = 0;
        foreach (var v in vector)
        {
            sum += v * v;
        }

        var length = Math.Sqrt(sum);
        return length == 0 ? vector : vector.Select(v => (float)(v / length)).ToArray();
    }

    private static string Truncate(string text, int length) => text.Length <= length ? text : text[..length] + "…";

    private sealed class EmbeddingResponse
    {
        [JsonPropertyName("data")]
        public List<EmbeddingData> Data { get; set; } = [];
    }

    private sealed class EmbeddingData
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("embedding")]
        public float[] Embedding { get; set; } = [];
    }
}
