using System;
using System.Collections.Generic;
using System.Linq;
using JellyFlix.Appearance;
using JellyFlix.Search;
using Xunit;

namespace JellyFlix.Tests.Appearance;

public class SeasonalTests
{
    [Theory]
    [InlineData(2026, 10, 1, Season.Halloween)]
    [InlineData(2026, 10, 31, Season.Halloween)]
    [InlineData(2026, 11, 1, Season.None)]
    [InlineData(2026, 12, 1, Season.Christmas)]
    [InlineData(2027, 1, 1, Season.Christmas)]
    [InlineData(2027, 1, 2, Season.None)]
    [InlineData(2026, 6, 20, Season.None)]
    [InlineData(2026, 6, 21, Season.Summer)]
    [InlineData(2026, 8, 31, Season.Summer)]
    [InlineData(2026, 9, 1, Season.None)]
    public void AutomaticFollowsTheCalendar(int year, int month, int day, Season expected)
    {
        Assert.Equal(expected, SeasonCalendar.Resolve("Auto", new DateOnly(year, month, day)));
    }

    [Fact]
    public void ASeasonCanBeForcedOrTurnedOff()
    {
        var july = new DateOnly(2026, 7, 4);
        Assert.Equal(Season.Christmas, SeasonCalendar.Resolve("christmas", july));
        Assert.Equal(Season.None, SeasonCalendar.Resolve("Off", july));
        Assert.Equal(Season.None, SeasonCalendar.Resolve("Easter", july));
        Assert.Equal(Season.None, SeasonCalendar.Resolve(null, july));
    }

    [Fact]
    public void ShelvesMatchStemmedWordsAndGenres()
    {
        var picks = SeasonalShelves.For(Season.Halloween)[0];
        var family = SeasonalShelves.For(Season.Halloween)[1];
        var words = Words("Three witches haunt a quiet village");

        Assert.True(picks.Score(words, ["Comedy"]) > 0);
        Assert.Equal(0, family.Score(words, ["Comedy"]));
        Assert.True(family.Score(words, ["Animation"]) > 0);
        Assert.Equal(0, picks.Score(Words("A romance in Paris"), ["Romance"]));
        Assert.True(picks.Score(Words("Haunted house of ghosts and zombies"), []) > picks.Score(words, []));
    }

    [Fact]
    public void FillKeepsTitlesOnOneShelfAndDropsSmallShelves()
    {
        var shelves = SeasonalShelves.For(Season.Christmas);
        var titles = Enumerable.Range(0, 4)
            .Select(i => Title($"A Christmas Story {i}", ["Family"], 7 - i))
            .Append(Title("Snowed In", ["Comedy"], 9))
            .Append(Title("Space Cats", ["Family"], 5))
            .ToList();

        var filled = AppearanceService.Fill(shelves, titles);

        var picks = Assert.Single(filled);
        Assert.Equal("SeasonalChristmasPicks", picks.Key);
        Assert.Equal(5, picks.Ids.Count);
        Assert.Equal(titles[4].Id, picks.Ids[0]);
    }

    private static HashSet<string> Words(string text) => Tokenizer.Tokenize(text).ToHashSet();

    private static (Guid Id, IReadOnlySet<string> Words, IReadOnlyList<string> Genres, float Rating) Title(string text, string[] genres, float rating)
        => (Guid.NewGuid(), Words(text), genres, rating);
}
