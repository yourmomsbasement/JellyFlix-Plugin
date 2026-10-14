using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JellyFlix.Configuration;
using JellyFlix.WebUpdater;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace JellyFlix.Tests.WebUpdater;

public sealed class WebUpdateServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "jellyflix-tests-" + Guid.NewGuid().ToString("N"));
    private readonly FakeSource _source = new();
    private readonly PluginConfiguration _config = new();
    private readonly WebUpdateService _service;

    public WebUpdateServiceTests()
    {
        Directory.CreateDirectory(_root);
        var paths = new Mock<IApplicationPaths>();
        paths.SetupGet(p => p.ProgramDataPath).Returns(_root);
        paths.SetupGet(p => p.WebPath).Returns(Path.Combine(_root, "stock-web"));
        _service = new WebUpdateService(_source, paths.Object, () => new Version(13, 0, 0), () => _config, TimeProvider.System, NullLogger<WebUpdateService>.Instance);
    }

    public void Dispose()
    {
        _service.Dispose();
        Directory.Delete(_root, true);
    }

    [Fact]
    public async Task InstallsNewestReleaseForThisServer()
    {
        _source.Add("2026.10.13.1", "13.0", Zip(("index.html", "old")));
        _source.Add("2026.10.13.2", "13.0", Zip(("index.html", "new"), ("app.js", "x")));

        var status = await _service.CheckAndInstallAsync(CancellationToken.None);

        Assert.Equal("2026.10.13.2", status.Installed?.Version);
        Assert.Equal("new", await File.ReadAllTextAsync(Path.Combine(_service.InstallPath, "index.html"), TestContext.Current.CancellationToken));
        Assert.True(File.Exists(Path.Combine(_service.InstallPath, "app.js")));
        Assert.True(status.IsServedByPlugin);
        Assert.True(status.IsServerUsingInstall);
        Assert.StartsWith("Installed", status.LastResult, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReportsTheStockClientWhenServingIsOff()
    {
        _config.ServeWebClient = false;
        _source.Add("2026.10.13.1", "13.0", Zip(("index.html", "v1")));

        var status = await _service.CheckAndInstallAsync(CancellationToken.None);

        Assert.NotNull(status.Installed);
        Assert.False(status.IsServedByPlugin);
        Assert.False(status.IsServerUsingInstall);
    }

    [Fact]
    public async Task StopsReadingReleasesAtTheFirstOneForThisServer()
    {
        _source.Add("2026.10.01.1", "13.0", Zip(("index.html", "old")));
        _source.Add("2026.10.02.1", "13.0", Zip(("index.html", "older")));
        _source.Add("2026.10.03.1", "13.0", Zip(("index.html", "newest")));
        var progress = new List<double>();

        var status = await _service.CheckAndInstallAsync(CancellationToken.None, new SyncProgress(progress.Add));

        Assert.Equal("2026.10.03.1", status.Installed?.Version);
        Assert.Equal(1, _source.ReleasesRead);
        Assert.Equal(0, progress[0]);
        Assert.True(progress.SequenceEqual(progress.Order()), "progress only moves forward");
        Assert.Equal(95, progress[^1]);
    }

    [Fact]
    public async Task SkipsReleasesForOtherServerVersions()
    {
        _source.Add("2026.10.13.1", "13.0", Zip(("index.html", "for 13")));
        _source.Add("2026.12.01.1", "14.0", Zip(("index.html", "for 14")));

        var status = await _service.CheckAndInstallAsync(CancellationToken.None);

        Assert.Equal("2026.10.13.1", status.Installed?.Version);
    }

    [Fact]
    public async Task ReportsWhenNoReleaseFitsThisServer()
    {
        _source.Add("2026.12.01.1", "14.0", Zip(("index.html", "for 14")));

        var status = await _service.CheckAndInstallAsync(CancellationToken.None);

        Assert.Null(status.Installed);
        Assert.Contains("13.0", status.LastResult, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DoesNotDownloadAgainWhenUpToDate()
    {
        _source.Add("2026.10.13.1", "13.0", Zip(("index.html", "v1")));
        await _service.CheckAndInstallAsync(CancellationToken.None);
        var downloads = _source.Downloads;

        var status = await _service.CheckAndInstallAsync(CancellationToken.None);

        Assert.Equal(downloads, _source.Downloads);
        Assert.StartsWith("Up to date", status.LastResult, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReplacesTheOldBuildCompletely()
    {
        _source.Add("2026.10.13.1", "13.0", Zip(("index.html", "v1"), ("old.js", "gone soon")));
        await _service.CheckAndInstallAsync(CancellationToken.None);
        _source.Add("2026.10.14.1", "13.0", Zip(("index.html", "v2")));

        var status = await _service.CheckAndInstallAsync(CancellationToken.None);

        Assert.Equal("2026.10.14.1", status.Installed?.Version);
        Assert.False(File.Exists(Path.Combine(_service.InstallPath, "old.js")));
        Assert.Equal(["jellyflix-web"], Array.ConvertAll(Directory.GetFileSystemEntries(_root), p => Path.GetFileName(p)));
    }

    [Fact]
    public async Task ChecksumMismatch_KeepsTheInstalledBuild()
    {
        _source.Add("2026.10.13.1", "13.0", Zip(("index.html", "good")));
        await _service.CheckAndInstallAsync(CancellationToken.None);
        _source.Add("2026.10.14.1", "13.0", Zip(("index.html", "tampered")), checksumOverride: new string('0', 64));

        var status = await _service.CheckAndInstallAsync(CancellationToken.None);

        Assert.Equal("2026.10.13.1", status.Installed?.Version);
        Assert.Equal("good", await File.ReadAllTextAsync(Path.Combine(_service.InstallPath, "index.html"), TestContext.Current.CancellationToken));
        Assert.StartsWith("Update failed: Checksum mismatch", status.LastResult, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RejectsBuildsWithoutIndexHtml()
    {
        _source.Add("2026.10.13.1", "13.0", Zip(("main.js", "no index")));

        var status = await _service.CheckAndInstallAsync(CancellationToken.None);

        Assert.Null(status.Installed);
        Assert.False(Directory.Exists(_service.InstallPath));
        Assert.Contains("index.html", status.LastResult, StringComparison.Ordinal);
    }

    [Fact]
    public void DetectsWhenTheServerServesTheInstallFolder()
    {
        var paths = new Mock<IApplicationPaths>();
        paths.SetupGet(p => p.ProgramDataPath).Returns(_root);
        paths.SetupGet(p => p.WebPath).Returns(Path.Combine(_root, "jellyflix-web") + Path.DirectorySeparatorChar);
        using var service = new WebUpdateService(_source, paths.Object, () => new Version(13, 0), () => _config, TimeProvider.System, NullLogger<WebUpdateService>.Instance);

        Assert.True(service.GetStatus().IsServerUsingInstall);
    }

    private static byte[] Zip(params (string Name, string Content)[] files)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
        {
            foreach (var (name, content) in files)
            {
                using var entry = zip.CreateEntry(name).Open();
                entry.Write(Encoding.UTF8.GetBytes(content));
            }
        }

        return buffer.ToArray();
    }

    /// <summary>Reports straight away, unlike <see cref="Progress{T}"/>.</summary>
    private sealed class SyncProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }

    private sealed class FakeSource : IReleaseSource
    {
        private readonly List<WebRelease> _releases = [];
        private readonly Dictionary<Uri, byte[]> _files = [];

        public int Downloads { get; private set; }

        /// <summary>How many releases were read; releases older than the one installed shouldn't be.</summary>
        public int ReleasesRead { get; private set; }

        public void Add(string version, string serverVersion, byte[] zip, string? checksumOverride = null)
        {
            var zipUrl = new Uri($"https://example.test/{version}/jellyflix-web.zip");
            var shaUrl = new Uri($"https://example.test/{version}/jellyflix-web.zip.sha256");
            _files[zipUrl] = zip;
            _files[shaUrl] = Encoding.UTF8.GetBytes((checksumOverride ?? Convert.ToHexStringLower(SHA256.HashData(zip))) + "\n");

            // Each added release is newer than the previous one; GitHub lists newest first
            _releases.Insert(0, new WebRelease(version, "commit-" + version, serverVersion, DateTimeOffset.UtcNow, zipUrl, shaUrl));
        }

        public async IAsyncEnumerable<WebRelease> GetReleasesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var release in _releases.ToArray())
            {
                await Task.Yield();
                ReleasesRead++;
                yield return release;
            }
        }

        public Task<WebDownload> DownloadAsync(Uri url, CancellationToken cancellationToken)
        {
            Downloads++;
            var bytes = _files[url];
            return Task.FromResult(new WebDownload(new MemoryStream(bytes), bytes.Length));
        }
    }
}
