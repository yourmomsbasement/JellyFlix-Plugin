using System.Collections.Generic;

namespace JellyFlix.Appearance;

/// <summary>
/// The curated shelves of each season.
/// </summary>
public static class SeasonalShelves
{
    private static readonly string[] _halloween =
    [
        "halloween", "haunted", "haunting", "ghost", "ghosts", "witch", "witches", "vampire", "vampires", "zombie",
        "zombies", "werewolf", "monster", "monsters", "pumpkin", "spooky", "possessed", "demon", "demons", "cursed",
        "exorcism", "poltergeist", "skeleton", "graveyard", "cemetery", "occult", "supernatural", "creepy"
    ];

    private static readonly string[] _christmas =
    [
        "christmas", "xmas", "santa", "claus", "holiday", "holidays", "snow", "snowman", "snowmen", "elf", "elves",
        "reindeer", "sleigh", "mistletoe", "grinch", "nutcracker", "winter", "festive", "yuletide", "carol"
    ];

    private static readonly string[] _summer =
    [
        "summer", "beach", "beaches", "vacation", "surf", "surfing", "island", "islands", "ocean", "tropical", "sunny",
        "camp", "camping", "lake", "shark", "sharks", "heatwave", "pool", "sailing", "boardwalk"
    ];

    private static readonly Dictionary<Season, SeasonalShelf[]> _shelves = new()
    {
        [Season.Halloween] =
        [
            new("SeasonalHalloweenPicks", _halloween, []),
            new("SeasonalHalloweenFamily", _halloween, ["Family", "Animation"]),
            new("SeasonalHalloweenHorror", [], ["Horror"])
        ],
        [Season.Christmas] =
        [
            new("SeasonalChristmasPicks", _christmas, []),
            new("SeasonalChristmasFamily", [], ["Family", "Animation"])
        ],
        [Season.Summer] =
        [
            new("SeasonalSummerPicks", _summer, []),
            new("SeasonalSummerBlockbusters", [], ["Action", "Adventure"])
        ]
    };

    /// <summary>
    /// Gets a season's shelves, in the order they're shown.
    /// </summary>
    /// <param name="season">The season.</param>
    /// <returns>The shelves.</returns>
    public static IReadOnlyList<SeasonalShelf> For(Season season)
        => _shelves.TryGetValue(season, out var shelves) ? shelves : [];
}
