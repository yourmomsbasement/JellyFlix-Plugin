using System;
using System.Collections.Generic;
using System.Linq;

namespace JellyFlix.Search;

/// <summary>
/// The pure ranking steps of description search, kept apart from the library so they can be tested.
/// </summary>
public static class SearchRanking
{
    /// <summary>
    /// Word matches scoring under this share of the best match are dropped: they share a word or two, nothing more.
    /// </summary>
    public const double MinRelativeTextScore = 0.3;

    /// <summary>
    /// Meaning matches this far below the best one are dropped.
    /// </summary>
    public const double MaxSimilarityGap = 0.12;

    /// <summary>
    /// Meaning matches below this similarity are dropped whatever the best one is.
    /// </summary>
    public const double MinSimilarity = 0.2;

    // The usual constant for reciprocal rank fusion
    private const double FusionK = 60;

    /// <summary>
    /// Keeps the word matches that are close enough to the best one.
    /// </summary>
    /// <param name="matches">Matches, best first.</param>
    /// <returns>The kept matches.</returns>
    public static IReadOnlyList<Guid> KeepStrongTextMatches(IReadOnlyList<(Guid Id, double Score)> matches)
    {
        if (matches.Count == 0)
        {
            return [];
        }

        var threshold = matches[0].Score * MinRelativeTextScore;
        return matches.Where(m => m.Score >= threshold).Select(m => m.Id).ToList();
    }

    /// <summary>
    /// Ranks titles by how close their embedding is to the query's.
    /// </summary>
    /// <param name="query">The query's unit vector.</param>
    /// <param name="titles">Unit vectors of the titles that may be returned.</param>
    /// <param name="limit">How many results at most.</param>
    /// <returns>The closest titles, best first.</returns>
    public static IReadOnlyList<Guid> RankBySimilarity(float[] query, IEnumerable<KeyValuePair<Guid, float[]>> titles, int limit)
    {
        var scored = titles
            .Where(t => t.Value.Length == query.Length)
            .Select(t => (t.Key, Similarity: Dot(query, t.Value)))
            .OrderByDescending(t => t.Similarity)
            .Take(limit)
            .ToList();
        if (scored.Count == 0)
        {
            return [];
        }

        var threshold = Math.Max(MinSimilarity, scored[0].Similarity - MaxSimilarityGap);
        return scored.Where(t => t.Similarity >= threshold).Select(t => t.Key).ToList();
    }

    /// <summary>
    /// Merges ranked lists with reciprocal rank fusion: titles high in either list rise, titles in both rise most.
    /// </summary>
    /// <param name="lists">Ranked lists, best first.</param>
    /// <param name="limit">How many results at most.</param>
    /// <returns>The merged ranking.</returns>
    public static IReadOnlyList<Guid> Fuse(IEnumerable<IReadOnlyList<Guid>> lists, int limit)
    {
        var scores = new Dictionary<Guid, double>();
        foreach (var list in lists)
        {
            for (var rank = 0; rank < list.Count; rank++)
            {
                scores[list[rank]] = scores.GetValueOrDefault(list[rank]) + (1 / (FusionK + rank + 1));
            }
        }

        return scores.OrderByDescending(s => s.Value).ThenBy(s => s.Key).Take(limit).Select(s => s.Key).ToList();
    }

    private static double Dot(float[] a, float[] b)
    {
        double sum = 0;
        for (var i = 0; i < a.Length; i++)
        {
            sum += a[i] * b[i];
        }

        return sum;
    }
}
