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
using JellyFlix.Search;
using Moq;
using Xunit;

namespace JellyFlix.Tests.Search;

public class DescriptionSearchTests
{
    private static readonly Guid _martian = Guid.NewGuid();
    private static readonly Guid _gravity = Guid.NewGuid();
    private static readonly Guid _notebook = Guid.NewGuid();
    private static readonly Guid _jaws = Guid.NewGuid();

    [Fact]
    public void TokenizeDropsStopwordsAndStems()
    {
        Assert.Equal(["space", "strand", "astronaut"], Tokenizer.Tokenize("A space movie with a stranded astronaut"));
        Assert.Equal(["shark", "attack", "beach"], Tokenizer.Tokenize("Sharks attacking the beach!"));
        Assert.Equal(["pokemon"], Tokenizer.Tokenize("Pokémon"));
        Assert.Equal(["story"], Tokenizer.Tokenize("stories"));
    }

    [Fact]
    public void TextIndexRanksTheBestDescriptionFirst()
    {
        var results = Library().Search("space movie with a stranded astronaut", 10);

        Assert.Equal(_martian, results[0].Id);
        Assert.Contains(results, r => r.Id == _gravity);
        Assert.DoesNotContain(results, r => r.Id == _notebook);
    }

    [Fact]
    public void TextIndexWeighsTheNameAboveTheOverview()
    {
        var results = Library().Search("gravity", 10);

        Assert.Equal(_gravity, results[0].Id);
    }

    [Fact]
    public void WeakTextMatchesAreDropped()
    {
        var kept = SearchRanking.KeepStrongTextMatches([(_martian, 10), (_gravity, 5), (_jaws, 1)]);

        Assert.Equal([_martian, _gravity], kept);
    }

    [Fact]
    public void FuseFavorsTitlesInBothLists()
    {
        var fused = SearchRanking.Fuse([[_jaws, _martian, _gravity], [_martian, _notebook]], 10);

        Assert.Equal(_martian, fused[0]);
        Assert.Equal(4, fused.Count);
        Assert.Single(SearchRanking.Fuse([[_jaws, _martian]], 1));
    }

    [Fact]
    public void RankBySimilarityKeepsTheCloseOnes()
    {
        var query = EmbeddingClient.Normalize([1, 0, 0]);
        var titles = new Dictionary<Guid, float[]>
        {
            [_martian] = EmbeddingClient.Normalize([0.9f, 0.1f, 0]),
            [_gravity] = EmbeddingClient.Normalize([0.8f, 0.3f, 0]),
            [_notebook] = EmbeddingClient.Normalize([0, 0, 1]),
            [_jaws] = [1, 0]
        };

        var ranked = SearchRanking.RankBySimilarity(query, titles, 10);

        Assert.Equal([_martian, _gravity], ranked);
    }

    [Fact]
    public async Task EmbeddingClientCallsAnOpenAiCompatibleApi()
    {
        using var handler = new FakeEmbeddings();
        using var client = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);
        var config = new PluginConfiguration { EmbeddingsUrl = "http://ollama.test:11434/", EmbeddingsModel = "nomic-embed-text", EmbeddingsApiKey = "k" };

        var vectors = await new EmbeddingClient(factory.Object, () => config).EmbedAsync(["a", "b"], CancellationToken.None);

        Assert.Equal("http://ollama.test:11434/v1/embeddings", handler.Url);
        Assert.Equal("Bearer k", handler.Authorization);
        Assert.Equal("nomic-embed-text", handler.Body!.RootElement.GetProperty("model").GetString());
        Assert.Equal(2, vectors.Count);

        // Ordered by index and scaled to unit length
        Assert.Equal([1f, 0f], vectors[0]);
        Assert.Equal([0f, 1f], vectors[1]);
    }

    private static TextIndex Library()
    {
        var index = new TextIndex();
        index.Add(_martian, [("The Martian", 3), ("Stranded on Mars after a storm, an astronaut must survive alone until help crosses space to reach him.", 1), ("Science Fiction", 1.5), ("Drama", 1.5)]);
        index.Add(_gravity, [("Gravity", 3), ("Two astronauts work together to survive after an accident leaves them adrift in space.", 1), ("Science Fiction", 1.5)]);
        index.Add(_notebook, [("The Notebook", 3), ("A poor yet passionate young man falls in love with a rich young woman.", 1), ("Romance", 1.5)]);
        index.Add(_jaws, [("Jaws", 3), ("A giant shark terrorizes a beach town. Gravity of the situation is ignored.", 1), ("Thriller", 1.5)]);
        return index;
    }

    private sealed class FakeEmbeddings : HttpMessageHandler
    {
        public string? Url { get; private set; }

        public string? Authorization { get; private set; }

        public JsonDocument? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Url = request.RequestUri!.ToString();
            Authorization = request.Headers.Authorization?.ToString();
            Body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));

            // Out of order on purpose
            const string Json = """{ "data": [ { "index": 1, "embedding": [0, 3] }, { "index": 0, "embedding": [2, 0] } ] }""";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Json, Encoding.UTF8, "application/json") };
        }
    }
}
