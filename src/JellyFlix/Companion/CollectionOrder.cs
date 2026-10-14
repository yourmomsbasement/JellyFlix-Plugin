using System;
using System.Collections.Generic;
using System.Linq;

namespace JellyFlix.Companion;

/// <summary>
/// Works out what comes next in a collection such as a film series.
/// </summary>
public static class CollectionOrder
{
    /// <summary>
    /// Finds the title released after the current one.
    /// </summary>
    /// <param name="titles">The collection's titles: id, release date and year.</param>
    /// <param name="currentId">The title that just finished.</param>
    /// <returns>The next title's id, or null when it's the last one or isn't in the collection.</returns>
    public static Guid? NextAfter(IEnumerable<(Guid Id, DateTime? Released, int? Year)> titles, Guid currentId)
    {
        // Undated titles go last; the id breaks ties, so the order is stable
        var ordered = titles
            .OrderBy(t => t.Released ?? (t.Year is { } year ? new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc) : DateTime.MaxValue))
            .ThenBy(t => t.Id)
            .Select(t => t.Id)
            .ToList();
        var index = ordered.IndexOf(currentId);
        return index >= 0 && index + 1 < ordered.Count ? ordered[index + 1] : null;
    }
}
