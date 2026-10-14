using System;
using System.IO;
using JellyFlix.Artwork;
using MediaBrowser.Common.Configuration;
using Moq;
using Xunit;

namespace JellyFlix.Tests.Artwork;

public sealed class ArtworkStoreTests : IDisposable
{
    private static readonly Guid _sam = Guid.NewGuid();
    private static readonly Guid _leo = Guid.NewGuid();
    private static readonly Guid _movie = Guid.NewGuid();

    private readonly string _dataPath = Path.Combine(Path.GetTempPath(), "jellyflix-artwork-tests-" + Guid.NewGuid().ToString("N"));
    private readonly Mock<IApplicationPaths> _paths = new();

    public ArtworkStoreTests()
    {
        _paths.SetupGet(p => p.DataPath).Returns(_dataPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dataPath))
        {
            Directory.Delete(_dataPath, true);
        }
    }

    [Fact]
    public void ChoicesBelongToOneProfile()
    {
        var store = new ArtworkStore(_paths.Object);

        store.Set(_sam, _movie, ArtworkKind.Backdrop, new ArtworkChoice { ImageIndex = 2 });

        Assert.Equal(2, store.Get(_sam, _movie, ArtworkKind.Backdrop)?.ImageIndex);
        Assert.Null(store.Get(_sam, _movie, ArtworkKind.Poster));
        Assert.Null(store.Get(_leo, _movie, ArtworkKind.Backdrop));
        Assert.Empty(store.GetAll(_leo));
    }

    [Fact]
    public void ChoicesAreKeptAcrossRestarts()
    {
        new ArtworkStore(_paths.Object).Set(_sam, _movie, ArtworkKind.Poster, new ArtworkChoice { File = "a.jpg", SourceUrl = "https://image.test/a.jpg" });

        var reloaded = new ArtworkStore(_paths.Object);

        Assert.Equal("a.jpg", reloaded.Get(_sam, _movie, ArtworkKind.Poster)?.File);
        Assert.Contains("a.jpg", reloaded.GetFilesInUse());
    }

    [Fact]
    public void ResettingEverythingLeavesNothingBehind()
    {
        var store = new ArtworkStore(_paths.Object);
        store.Set(_sam, _movie, ArtworkKind.Poster, new ArtworkChoice { File = "a.jpg" });
        store.Set(_sam, _movie, ArtworkKind.Backdrop, new ArtworkChoice { ImageIndex = 1 });

        store.Set(_sam, _movie, ArtworkKind.Poster, null);
        Assert.Single(store.GetAll(_sam));
        store.Set(_sam, _movie, ArtworkKind.Backdrop, null);

        Assert.Empty(store.GetAll(_sam));
        Assert.Empty(store.GetFilesInUse());
    }

    [Fact]
    public void DeletedProfilesAreForgotten()
    {
        var store = new ArtworkStore(_paths.Object);
        store.Set(_sam, _movie, ArtworkKind.Poster, new ArtworkChoice { ImageIndex = 1 });
        store.Set(_leo, _movie, ArtworkKind.Poster, new ArtworkChoice { ImageIndex = 2 });

        store.RemoveUser(_sam);

        Assert.Empty(store.GetAll(_sam));
        Assert.Equal(2, new ArtworkStore(_paths.Object).Get(_leo, _movie, ArtworkKind.Poster)?.ImageIndex);
    }

    [Fact]
    public void DownloadNamesAreHashesThatCantEscapeTheFolder()
    {
        var name = ArtworkFiles.FileName("https://image.tmdb.org/t/p/original/x.jpg", "image/jpeg");

        Assert.NotNull(name);
        Assert.True(ArtworkFiles.IsValidFileName(name));
        Assert.Equal(name, ArtworkFiles.FileName("https://image.tmdb.org/t/p/original/x.jpg", "image/jpeg"));
        Assert.Null(ArtworkFiles.FileName("https://image.test/x.svg", "image/svg+xml"));
        Assert.False(ArtworkFiles.IsValidFileName("../artwork.json"));
        Assert.False(ArtworkFiles.IsValidFileName("..%2Fartwork.json"));
        Assert.False(ArtworkFiles.IsValidFileName(null));
        Assert.StartsWith(ArtworkFiles.RemotePrefix, ArtworkFiles.RemoteId("https://image.test/x.jpg"), StringComparison.Ordinal);
    }
}
