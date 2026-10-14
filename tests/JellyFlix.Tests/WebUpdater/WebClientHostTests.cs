using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using JellyFlix.Configuration;
using JellyFlix.WebUpdater;
using MediaBrowser.Common.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Moq;
using Xunit;

namespace JellyFlix.Tests.WebUpdater;

public sealed class WebClientHostTests : IDisposable
{
    /// <summary>What Jellyfin's own pipeline answers with, so tests can tell when a request fell through to it.</summary>
    private const int JellyfinStatus = 299;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "jellyflix-host-" + Guid.NewGuid().ToString("N"));
    private readonly PluginConfiguration _config = new();
    private readonly Mock<IApplicationPaths> _paths = new();
    private readonly ServiceProvider _services;
    private string _baseUrl = string.Empty;

    public WebClientHostTests()
    {
        Directory.CreateDirectory(_root);
        _paths.SetupGet(p => p.ProgramDataPath).Returns(_root);

        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.WebRootFileProvider).Returns(new NullFileProvider());
        environment.SetupGet(e => e.ContentRootPath).Returns(_root);
        _services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(environment.Object)
            .BuildServiceProvider();
    }

    private string InstallPath => Path.Combine(_root, "jellyflix-web");

    public void Dispose()
    {
        _services.Dispose();
        Directory.Delete(_root, true);
    }

    [Fact]
    public async Task ServesTheInstalledClient()
    {
        Install(("index.html", "jellyflix"), ("main.js", "code"));

        var page = await GetAsync("/web/");
        var script = await GetAsync("/web/main.js");

        Assert.Equal((200, "jellyflix"), (page.Status, page.Body));
        Assert.Equal("no-cache", page.CacheControl);
        Assert.Equal((200, "code"), (script.Status, script.Body));
    }

    [Fact]
    public async Task FallsThroughUntilAClientIsInstalled()
    {
        Assert.Equal(JellyfinStatus, (await GetAsync("/web/")).Status);
    }

    [Fact]
    public async Task FallsThroughWhenServingIsTurnedOff()
    {
        Install(("index.html", "jellyflix"));
        _config.ServeWebClient = false;

        Assert.Equal(JellyfinStatus, (await GetAsync("/web/index.html")).Status);
    }

    [Fact]
    public async Task FallsThroughForFilesTheClientDoesNotHave()
    {
        Install(("index.html", "jellyflix"));

        Assert.Equal(JellyfinStatus, (await GetAsync("/web/stock-only.js")).Status);
    }

    [Fact]
    public async Task LeavesTheRestOfTheServerAlone()
    {
        Install(("index.html", "jellyflix"));

        Assert.Equal(JellyfinStatus, (await GetAsync("/Users/Public")).Status);
        Assert.Equal(JellyfinStatus, (await GetAsync("/webhooks")).Status);
    }

    [Fact]
    public async Task FollowsTheServerBaseUrl()
    {
        Install(("index.html", "jellyflix"));
        _baseUrl = "jellyfin/";

        Assert.Equal(200, (await GetAsync("/jellyfin/web/index.html")).Status);
        Assert.Equal(JellyfinStatus, (await GetAsync("/web/index.html")).Status);
    }

    [Fact]
    public async Task PicksUpAnUpdateThatSwappedTheFolder()
    {
        Install(("index.html", "v1"));
        Assert.Equal("v1", (await GetAsync("/web/index.html")).Body);

        Directory.Move(InstallPath, InstallPath + ".previous");
        Install(("index.html", "v2"));

        Assert.Equal("v2", (await GetAsync("/web/index.html")).Body);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("/", "")]
    [InlineData("jellyfin", "/jellyfin")]
    [InlineData("/jellyfin/", "/jellyfin")]
    public void NormalizesTheBaseUrl(string? baseUrl, string expected)
        => Assert.Equal(expected, WebClientHost.NormalizeBaseUrl(baseUrl));

    private void Install(params (string Name, string Content)[] files)
    {
        Directory.CreateDirectory(InstallPath);
        foreach (var (name, content) in files)
        {
            File.WriteAllText(Path.Combine(InstallPath, name), content);
        }
    }

    private async Task<(int Status, string Body, string? CacheControl)> GetAsync(string path)
    {
        using var host = new WebClientHost(_paths.Object, () => _config, () => _baseUrl);
        var app = new ApplicationBuilder(_services);
        host.Configure(jellyfin => jellyfin.Run(context =>
        {
            context.Response.StatusCode = JellyfinStatus;
            return Task.CompletedTask;
        }))(app);

        var context = new DefaultHttpContext { RequestServices = _services };
        context.Request.Method = "GET";
        context.Request.Path = path;
        using var body = new MemoryStream();
        context.Response.Body = body;

        await app.Build()(context);

        return (context.Response.StatusCode, Encoding.UTF8.GetString(body.ToArray()), context.Response.Headers.CacheControl.ToString() is { Length: > 0 } cache ? cache : null);
    }
}
