using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JellyFlix.Configuration;
using JellyFlix.Requests;
using JellyFlix.Requests.Api.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace JellyFlix.Tests.Requests;

public sealed class RequestServiceTests : IDisposable
{
    private static readonly Guid _ownerId = Guid.NewGuid();
    private static readonly Guid _profileId = Guid.NewGuid();

    private readonly FakeSeerr _seerr = new();
    private readonly PluginConfiguration _config = new()
    {
        RequestsEnabled = true,
        SeerrUrl = "http://seerr.test:5055/",
        SeerrApiKey = "secret-key"
    };

    private readonly HttpClient _httpClient;
    private Requester _requester = new(_ownerId, false);

    public RequestServiceTests()
    {
        _httpClient = new HttpClient(_seerr);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _seerr.Dispose();
    }

    [Fact]
    public async Task SearchShowsMoviesAndShowsThatAreNotInTheLibrary()
    {
        _seerr.SearchResults = """
            { "results": [
                { "id": 1, "mediaType": "movie", "title": "Dune", "releaseDate": "2021-09-15", "posterPath": "/dune.jpg", "overview": "Spice." },
                { "id": 2, "mediaType": "tv", "name": "Severance", "firstAirDate": "2022-02-18", "mediaInfo": { "status": 2 } },
                { "id": 3, "mediaType": "movie", "title": "In the library", "mediaInfo": { "status": 5 } },
                { "id": 4, "mediaType": "person", "name": "An actor" },
                { "id": 5, "mediaType": "movie", "title": "On its way", "mediaInfo": { "status": 3 } }
            ] }
            """;

        var result = await CreateService().SearchAsync(_profileId, "dune", CancellationToken.None);

        Assert.True(result.IsEnabled);
        Assert.True(result.CanRequest);
        Assert.Equal([1, 2, 5], result.Results.Select(r => r.TmdbId));
        var dune = result.Results[0];
        Assert.Equal(("movie", "Dune", 2021, "https://image.tmdb.org/t/p/w342/dune.jpg", RequestStatus.None), (dune.MediaType, dune.Title, dune.Year, dune.PosterUrl, dune.Status));
        Assert.Equal(("Severance", 2022, RequestStatus.Requested), (result.Results[1].Title, result.Results[1].Year, result.Results[1].Status));
        Assert.Equal(RequestStatus.Processing, result.Results[2].Status);
        Assert.Equal("secret-key", _seerr.Requests[0].ApiKey);
        Assert.Equal("http://seerr.test:5055/api/v1/search?query=dune&page=1", _seerr.Requests[0].Url);
    }

    [Fact]
    public async Task DoesNothingWhileTurnedOff()
    {
        _config.RequestsEnabled = false;

        var result = await CreateService().SearchAsync(_profileId, "dune", CancellationToken.None);

        Assert.False(result.IsEnabled);
        Assert.Empty(_seerr.Requests);
        await Assert.ThrowsAsync<SeerrException>(() => CreateService().RequestAsync(_profileId, new CreateRequestDto { TmdbId = 1, MediaType = "movie" }, CancellationToken.None));
    }

    [Fact]
    public async Task RequestsAsTheHouseholdOwnersSeerrUser()
    {
        _seerr.Users = $$"""{ "results": [ { "id": 7, "jellyfinUserId": "{{_ownerId:N}}" }, { "id": 8, "jellyfinUserId": "{{Guid.NewGuid():N}}" } ] }""";

        await CreateService().RequestAsync(_profileId, new CreateRequestDto { TmdbId = 42, MediaType = "tv" }, CancellationToken.None);

        var request = _seerr.Requests.Single(r => r.Method == HttpMethod.Post);
        Assert.Equal("7", request.ApiUser);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal("tv", body.RootElement.GetProperty("mediaType").GetString());
        Assert.Equal(42, body.RootElement.GetProperty("mediaId").GetInt32());
        Assert.Equal("all", body.RootElement.GetProperty("seasons").GetString());
    }

    [Fact]
    public async Task RequestsAsTheApiKeysUserWithoutALinkedSeerrUser()
    {
        await CreateService().RequestAsync(_profileId, new CreateRequestDto { TmdbId = 1, MediaType = "movie" }, CancellationToken.None);

        var request = _seerr.Requests.Single(r => r.Method == HttpMethod.Post);
        Assert.Null(request.ApiUser);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.False(body.RootElement.TryGetProperty("seasons", out _));
    }

    [Fact]
    public async Task KidsProfilesCantRequestUnlessAllowed()
    {
        _requester = new Requester(_ownerId, true);
        var service = CreateService();

        Assert.False((await service.SearchAsync(_profileId, "dune", CancellationToken.None)).CanRequest);
        var error = await Assert.ThrowsAsync<SeerrException>(() => service.RequestAsync(_profileId, new CreateRequestDto { TmdbId = 1, MediaType = "movie" }, CancellationToken.None));
        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);

        _config.AllowKidsRequests = true;
        await service.RequestAsync(_profileId, new CreateRequestDto { TmdbId = 1, MediaType = "movie" }, CancellationToken.None);
        Assert.Contains(_seerr.Requests, r => r.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task PassesOnSeerrsReasonForRefusing()
    {
        _seerr.RequestStatus = HttpStatusCode.Forbidden;
        _seerr.RequestResponse = """{ "message": "You do not have permission to make movie requests." }""";

        var error = await Assert.ThrowsAsync<SeerrException>(() => CreateService().RequestAsync(_profileId, new CreateRequestDto { TmdbId = 1, MediaType = "movie" }, CancellationToken.None));

        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
        Assert.Equal("You do not have permission to make movie requests.", error.Message);
    }

    [Theory]
    [InlineData(null, RequestStatus.None)]
    [InlineData(SeerrMediaStatus.Unknown, RequestStatus.None)]
    [InlineData(SeerrMediaStatus.Pending, RequestStatus.Requested)]
    [InlineData(SeerrMediaStatus.Processing, RequestStatus.Processing)]
    [InlineData(SeerrMediaStatus.PartiallyAvailable, RequestStatus.PartiallyAvailable)]
    [InlineData(SeerrMediaStatus.Blocklisted, RequestStatus.Blocked)]
    [InlineData(SeerrMediaStatus.Deleted, RequestStatus.None)]
    public void MapsSeerrsStatus(SeerrMediaStatus? status, RequestStatus expected)
        => Assert.Equal(expected, RequestService.GetStatus(status is null ? null : new SeerrMediaInfo { Status = status.Value }));

    private RequestService CreateService()
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(_httpClient);
        var requesters = new Mock<IRequesterResolver>();
        requesters.Setup(r => r.ResolveAsync(_profileId)).ReturnsAsync(() => _requester);
        return new RequestService(new SeerrClient(factory.Object, () => _config), requesters.Object, () => _config, TimeProvider.System, NullLogger<RequestService>.Instance);
    }

    private sealed record SentRequest(HttpMethod Method, string Url, string? ApiKey, string? ApiUser, string? Body);

    private sealed class FakeSeerr : HttpMessageHandler
    {
        public List<SentRequest> Requests { get; } = [];

        public string SearchResults { get; set; } = """{ "results": [] }""";

        public string Users { get; set; } = """{ "results": [] }""";

        public HttpStatusCode RequestStatus { get; set; } = HttpStatusCode.Created;

        public string RequestResponse { get; set; } = """{ "id": 1 }""";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new SentRequest(
                request.Method,
                request.RequestUri!.ToString(),
                request.Headers.TryGetValues("X-Api-Key", out var keys) ? keys.Single() : null,
                request.Headers.TryGetValues("X-Api-User", out var users) ? users.Single() : null,
                body));

            var path = request.RequestUri.AbsolutePath;
            var (status, json) = path switch
            {
                "/api/v1/search" => (HttpStatusCode.OK, SearchResults),
                "/api/v1/user" => (HttpStatusCode.OK, Users),
                "/api/v1/request" => (RequestStatus, RequestResponse),
                _ => (HttpStatusCode.NotFound, """{ "message": "Not found" }""")
            };
            return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }
}
