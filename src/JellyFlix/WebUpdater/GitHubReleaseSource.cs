using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using JellyFlix.Configuration;
using MediaBrowser.Common.Net;

namespace JellyFlix.WebUpdater;

/// <summary>
/// Reads JellyFlix web client releases from GitHub. Each release has <c>jellyflix-web.zip</c>,
/// <c>jellyflix-web.zip.sha256</c> and <c>release.json</c>, published by the JellyFlix-Web release workflow.
/// </summary>
public class GitHubReleaseSource : IReleaseSource
{
    /// <summary>
    /// Releases are tagged "web-{version}".
    /// </summary>
    public const string TagPrefix = "web-";

    private const int ReleasesToScan = 20;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Func<PluginConfiguration> _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubReleaseSource"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="configuration">Returns the current plugin configuration.</param>
    public GitHubReleaseSource(IHttpClientFactory httpClientFactory, Func<PluginConfiguration> configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<WebRelease> GetReleasesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var config = _configuration();
        var url = new Uri($"{config.GitHubApiUrl.TrimEnd('/')}/repos/{config.WebReleasesRepository}/releases?per_page={ReleasesToScan}");
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        var client = _httpClientFactory.CreateClient(NamedClient.Default);
        List<GitHubRelease> releases;
        using (var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();
            releases = await response.Content.ReadFromJsonAsync<List<GitHubRelease>>(cancellationToken).ConfigureAwait(false) ?? [];
        }

        foreach (var release in releases
            .Where(r => !r.Draft && r.TagName.StartsWith(TagPrefix, StringComparison.Ordinal))
            .OrderByDescending(r => r.PublishedAt))
        {
            var zip = release.Assets.Find(a => a.Name == "jellyflix-web.zip");
            var sha = release.Assets.Find(a => a.Name == "jellyflix-web.zip.sha256");
            var info = release.Assets.Find(a => a.Name == "release.json");
            if (zip is null || sha is null || info is null)
            {
                continue;
            }

            // Each release's metadata is a separate download, so it is only fetched when the caller gets this far
            var metadata = await client.GetFromJsonAsync<ReleaseMetadata>(info.BrowserDownloadUrl, cancellationToken).ConfigureAwait(false);
            if (metadata is not null)
            {
                yield return new WebRelease(metadata.Version, metadata.Commit, metadata.ServerVersion, release.PublishedAt, zip.BrowserDownloadUrl, sha.BrowserDownloadUrl)
                {
                    AlsoSupports = metadata.ServerVersions
                };
            }
        }
    }

    /// <inheritdoc />
    public async Task<WebDownload> DownloadAsync(Uri url, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(NamedClient.Default);
        var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        try
        {
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return new WebDownload(content, response.Content.Headers.ContentLength, response);
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("draft")]
        public bool Draft { get; set; }

        [JsonPropertyName("published_at")]
        public DateTimeOffset PublishedAt { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubAsset> Assets { get; set; } = [];
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public Uri BrowserDownloadUrl { get; set; } = null!;
    }

    private sealed class ReleaseMetadata
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("commit")]
        public string Commit { get; set; } = string.Empty;

        [JsonPropertyName("serverVersion")]
        public string ServerVersion { get; set; } = string.Empty;

        // Every server version the build runs on, including serverVersion; missing from older releases
        [JsonPropertyName("serverVersions")]
        public string[] ServerVersions { get; set; } = [];
    }
}
