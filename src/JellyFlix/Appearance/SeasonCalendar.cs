using System;

namespace JellyFlix.Appearance;

/// <summary>
/// Decides which seasonal look applies.
/// </summary>
public static class SeasonCalendar
{
    /// <summary>
    /// The setting that picks the season by date.
    /// </summary>
    public const string Automatic = "Auto";

    /// <summary>
    /// Resolves the seasonal theme setting for a date.
    /// </summary>
    /// <param name="setting">"Off", "Auto", or a season's name.</param>
    /// <param name="date">Today, in the server's time zone.</param>
    /// <returns>The season to show.</returns>
    public static Season Resolve(string? setting, DateOnly date)
    {
        if (string.Equals(setting, Automatic, StringComparison.OrdinalIgnoreCase))
        {
            return ForDate(date);
        }

        return Enum.TryParse<Season>(setting, true, out var season) && Enum.IsDefined(season) ? season : Season.None;
    }

    /// <summary>
    /// The season for a date: Halloween through October, Christmas from December 1 to January 1, and summer from
    /// June 21 to August 31 (northern hemisphere).
    /// </summary>
    /// <param name="date">The date.</param>
    /// <returns>The season, or <see cref="Season.None"/>.</returns>
    public static Season ForDate(DateOnly date) => date switch
    {
        { Month: 10 } => Season.Halloween,
        { Month: 12 } or { Month: 1, Day: 1 } => Season.Christmas,
        { Month: 6, Day: >= 21 } or { Month: 7 } or { Month: 8 } => Season.Summer,
        _ => Season.None
    };
}
