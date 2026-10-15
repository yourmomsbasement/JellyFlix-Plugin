using System;
using JellyFlix.Recommendations.Library;
using MediaBrowser.Model.Entities;
using Xunit;

namespace JellyFlix.Recommendations.Tests;

public class RecommendationCachingTests
{
    private static readonly DateTimeOffset _start = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(UserDataSaveReason.PlaybackStart, false)]
    [InlineData(UserDataSaveReason.PlaybackProgress, false)]
    [InlineData(UserDataSaveReason.PlaybackFinished, true)]
    [InlineData(UserDataSaveReason.TogglePlayed, true)]
    [InlineData(UserDataSaveReason.UpdateUserRating, true)]
    [InlineData(UserDataSaveReason.UpdateUserData, true)]
    [InlineData(UserDataSaveReason.Import, true)]
    public void AffectsTaste_IgnoresPlaybackProgress(UserDataSaveReason reason, bool expected)
    {
        Assert.Equal(expected, RecommendationService.AffectsTaste(reason));
    }

    [Fact]
    public void Catalog_IsBuiltWhenThereIsNone()
    {
        Assert.True(new CatalogFreshness().NeedsRebuild(_start));
    }

    [Fact]
    public void Catalog_IsKeptWhileTheLibraryIsUnchanged()
    {
        var freshness = new CatalogFreshness();
        freshness.Built(_start);

        Assert.False(freshness.NeedsRebuild(_start.AddHours(5)));
    }

    [Fact]
    public void Catalog_WaitsForTheLibraryToSettle()
    {
        var freshness = new CatalogFreshness();
        freshness.Built(_start);
        freshness.Changed(_start.AddSeconds(10));

        Assert.False(freshness.NeedsRebuild(_start.AddSeconds(30)));
        Assert.True(freshness.NeedsRebuild(_start.AddSeconds(10) + CatalogFreshness.QuietPeriod));
    }

    [Fact]
    public void Catalog_IsRebuiltDuringALongScan()
    {
        var freshness = new CatalogFreshness();
        freshness.Built(_start);

        // A change every 20 seconds keeps the library from ever being quiet for a minute
        var now = _start;
        for (var i = 0; i < 30; i++)
        {
            now = now.AddSeconds(20);
            freshness.Changed(now);
            if (now - _start.AddSeconds(20) >= CatalogFreshness.MaxStaleness)
            {
                Assert.True(freshness.NeedsRebuild(now));
                return;
            }

            Assert.False(freshness.NeedsRebuild(now));
        }

        Assert.Fail("The catalog was never rebuilt during the scan");
    }

    [Fact]
    public void Catalog_ForgetsChangesOnceRebuilt()
    {
        var freshness = new CatalogFreshness();
        freshness.Built(_start);
        freshness.Changed(_start.AddSeconds(5));
        freshness.Built(_start.AddMinutes(2));

        Assert.False(freshness.NeedsRebuild(_start.AddMinutes(10)));
    }
}
