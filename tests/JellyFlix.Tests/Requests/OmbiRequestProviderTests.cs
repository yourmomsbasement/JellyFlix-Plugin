using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using JellyFlix.Configuration;
using JellyFlix.Requests;
using JellyFlix.Requests.Api.Models;
using JellyFlix.Requests.Ombi;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace JellyFlix.Tests.Requests;

public sealed class OmbiRequestProviderTests : IDisposable
{
    private static readonly Guid _ownerId = Guid.NewGuid();
    private static readonly Guid _profileId = Guid.NewGuid();

    private readonly FakeOmbi _ombi = new();
    private readonly HttpClient _httpClient;
    private readonly PluginConfiguration _config = new()
    {
        RequestsEnabled = true,
        RequestsProvider = "Ombi",
        OmbiUrl = "http://ombi.test:3579/",
        OmbiApiKey = "ombi-key"
    };

    public OmbiRequestProviderTests()
    {
        _httpClient = new HttpClient(_ombi);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _ombi.Dispose();
    }

    [Fact]
    public async Task SearchShowsWhereEachTitleStands()
    {
        _ombi.Search = """
            [
                { "id": "438631", "mediaType": "movie", "title": "Dune", "poster": "/dune.jpg", "overview": "Spice." },
                { "id": "90228", "mediaType": "tv", "title": "Dune: Prophecy", "poster": null },
                { "id": "1", "mediaType": "person", "title": "An actor" },
                { "id": "693134", "mediaType": "movie", "title": "Dune: Part Two" },
                { "id": "841", "mediaType": "movie", "title": "Dune (1984)" }
            ]
            """;
        _ombi.Details["/api/v2/Search/movie/438631"] = """{ "requested": false, "available": false, "releaseDate": "2021-09-15T00:00:00" }""";
        _ombi.Details["/api/v2/Search/tv/moviedb/90228"] = """{ "requested": true, "approved": false, "partlyAvailable": false, "firstAired": "2024-11-17" }""";
        _ombi.Details["/api/v2/Search/movie/693134"] = """{ "requested": true, "approved": true, "available": false }""";
        _ombi.Details["/api/v2/Search/movie/841"] = """{ "available": true }""";

        var result = await CreateService().SearchAsync(_profileId, "dune", CancellationToken.None);

        Assert.True(result.IsEnabled);
        Assert.Equal(["Dune", "Dune: Prophecy", "Dune: Part Two"], result.Results.Select(r => r.Title));
        var dune = result.Results[0];
        Assert.Equal(438631, dune.TmdbId);
        Assert.Equal(2021, dune.Year);
        Assert.Equal("https://image.tmdb.org/t/p/w342/dune.jpg", dune.PosterUrl);
        Assert.Equal(RequestStatus.None, dune.Status);
        Assert.Equal(RequestStatus.Requested, result.Results[1].Status);
        Assert.Equal(2024, result.Results[1].Year);
        Assert.Equal(RequestStatus.Processing, result.Results[2].Status);

        var search = _ombi.Requests.First();
        Assert.Equal(HttpMethod.Post, search.Method);
        Assert.Equal("http://ombi.test:3579/api/v2/Search/multi/dune", search.Url);
        Assert.Equal("ombi-key", search.ApiKey);
        using var filter = JsonDocument.Parse(search.Body!);
        Assert.True(filter.RootElement.GetProperty("movies").GetBoolean());
        Assert.False(filter.RootElement.GetProperty("people").GetBoolean());
    }

    [Fact]
    public async Task RequestsAsTheHouseholdOwnersOmbiUser()
    {
        _ombi.Users = """[ { "userName": "someone" }, { "userName": "Alex" } ]""";

        await CreateService().RequestAsync(_profileId, new CreateRequestDto { TmdbId = 90228, MediaType = "tv" }, CancellationToken.None);

        var request = _ombi.Requests.Single(r => r.Url.EndsWith("/api/v2/Requests/tv", StringComparison.Ordinal));
        Assert.Equal("Alex", request.UserName);
        Assert.Equal("alex", request.Alias);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal(90228, body.RootElement.GetProperty("theMovieDbId").GetInt32());
        Assert.True(body.RootElement.GetProperty("requestAll").GetBoolean());
    }

    [Fact]
    public async Task RequestsAsTheApiUserWithAnAliasWithoutAMatchingOmbiUser()
    {
        await CreateService().RequestAsync(_profileId, new CreateRequestDto { TmdbId = 1, MediaType = "movie" }, CancellationToken.None);

        var request = _ombi.Requests.Single(r => r.Url.EndsWith("/api/v1/Request/movie", StringComparison.Ordinal));
        Assert.Null(request.UserName);
        Assert.Equal("alex", request.Alias);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal(1, body.RootElement.GetProperty("theMovieDbId").GetInt32());
    }

    [Fact]
    public async Task PassesOnOmbisReasonForRefusing()
    {
        _ombi.RequestResult = """{ "result": false, "errorMessage": "You have exceeded your Movie request quota!" }""";

        var error = await Assert.ThrowsAsync<RequestProviderException>(() => CreateService().RequestAsync(_profileId, new CreateRequestDto { TmdbId = 1, MediaType = "movie" }, CancellationToken.None));

        Assert.Equal("You have exceeded your Movie request quota!", error.Message);
    }

    [Fact]
    public async Task ABadApiKeyIsReportedInOmbisWords()
    {
        _ombi.Unauthorized = true;

        var error = await Assert.ThrowsAsync<RequestProviderException>(() => CreateService().TestAsync(CancellationToken.None));

        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
        Assert.Equal("Invalid API Key", error.Message);
    }

    [Fact]
    public async Task TestReportsTheServiceAndItsVersion()
    {
        var result = await CreateService().TestAsync(CancellationToken.None);

        Assert.Equal("Ombi", result.Provider);
        Assert.Equal("4.47.1", result.Version);
    }

    [Fact]
    public async Task TheSettingPicksTheService()
    {
        _config.RequestsProvider = "Seerr";
        _config.SeerrUrl = string.Empty;

        // Seerr isn't set up, so requests are off even though Ombi is
        Assert.False((await CreateService().SearchAsync(_profileId, "dune", CancellationToken.None)).IsEnabled);
        Assert.Empty(_ombi.Requests);
    }

    private RequestService CreateService()
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(_httpClient);
        var users = new Mock<IUserManager>();
        users.Setup(u => u.GetUserById(_ownerId)).Returns(new User("alex", "Default", "Default"));
        var requesters = new Mock<IRequesterResolver>();
        requesters.Setup(r => r.ResolveAsync(_profileId)).ReturnsAsync(new Requester(_ownerId, false));

        var ombi = new OmbiRequestProvider(new OmbiClient(factory.Object, () => _config), users.Object, () => _config, TimeProvider.System, NullLogger<OmbiRequestProvider>.Instance);
        var seerr = new SeerrRequestProvider(new SeerrClient(factory.Object, () => _config), () => _config, TimeProvider.System, NullLogger<SeerrRequestProvider>.Instance);
        return new RequestService([seerr, ombi], requesters.Object, () => _config, NullLogger<RequestService>.Instance);
    }

    private sealed record SentRequest(HttpMethod Method, string Url, string? ApiKey, string? UserName, string? Alias, string? Body);

    private sealed class FakeOmbi : HttpMessageHandler
    {
        public List<SentRequest> Requests { get; } = [];

        public Dictionary<string, string> Details { get; } = [];

        public string Search { get; set; } = "[]";

        public string Users { get; set; } = "[]";

        public string RequestResult { get; set; } = """{ "result": true, "message": "Request added", "requestId": 3 }""";

        public bool Unauthorized { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string? Header(string name) => request.Headers.TryGetValues(name, out var values) ? values.Single() : null;
            Requests.Add(new SentRequest(
                request.Method,
                request.RequestUri!.ToString(),
                Header("ApiKey"),
                Header("UserName"),
                Header("ApiAlias"),
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));

            if (Unauthorized)
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("Invalid API Key") };
            }

            var path = request.RequestUri.AbsolutePath;
            var json = path switch
            {
                _ when path.StartsWith("/api/v2/Search/multi/", StringComparison.Ordinal) => Search,
                "/api/v1/Identity/Users" => Users,
                "/api/v1/Settings/about" => """{ "version": "4.47.1" }""",
                "/api/v1/Request/movie" or "/api/v2/Requests/tv" => RequestResult,
                _ => Details.GetValueOrDefault(path)
            };
            return json is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }
}
