using System;
using System.Collections.Generic;
using System.Linq;

namespace JellyFlix.Search;

/// <summary>
/// A BM25 full-text index over titles' metadata, with fields weighted by how telling they are.
/// </summary>
public sealed class TextIndex
{
    // Standard BM25 parameters
    private const double K1 = 1.2;
    private const double B = 0.75;

    private readonly Dictionary<string, Dictionary<Guid, double>> _postings = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, double> _lengths = [];

    /// <summary>
    /// Gets the number of documents.
    /// </summary>
    public int Count => _lengths.Count;

    /// <summary>
    /// Adds a title.
    /// </summary>
    /// <param name="id">The title id.</param>
    /// <param name="fields">Its text, each with a weight (e.g. the name counts more than the overview).</param>
    public void Add(Guid id, IEnumerable<(string? Text, double Weight)> fields)
    {
        double length = 0;
        foreach (var (text, weight) in fields)
        {
            foreach (var term in Tokenizer.Tokenize(text))
            {
                if (!_postings.TryGetValue(term, out var postings))
                {
                    postings = [];
                    _postings[term] = postings;
                }

                postings[id] = postings.GetValueOrDefault(id) + weight;
                length += weight;
            }
        }

        _lengths[id] = length;
    }

    /// <summary>
    /// Finds the titles best matching a query.
    /// </summary>
    /// <param name="query">The query, e.g. "space movie with a stranded astronaut".</param>
    /// <param name="limit">How many results at most.</param>
    /// <returns>Matching titles, best first, with their scores.</returns>
    public IReadOnlyList<(Guid Id, double Score)> Search(string query, int limit)
    {
        var terms = Tokenizer.Tokenize(query).Distinct(StringComparer.Ordinal).ToList();
        if (terms.Count == 0 || _lengths.Count == 0)
        {
            return [];
        }

        var averageLength = _lengths.Values.Average();
        var scores = new Dictionary<Guid, double>();
        foreach (var term in terms)
        {
            if (!_postings.TryGetValue(term, out var postings))
            {
                continue;
            }

            // Rare terms ("astronaut") say more than common ones ("love")
            var idf = Math.Log(1 + ((_lengths.Count - postings.Count + 0.5) / (postings.Count + 0.5)));
            foreach (var (id, frequency) in postings)
            {
                var norm = K1 * (1 - B + (B * _lengths[id] / averageLength));
                scores[id] = scores.GetValueOrDefault(id) + (idf * frequency * (K1 + 1) / (frequency + norm));
            }
        }

        return scores
            .OrderByDescending(s => s.Value)
            .ThenBy(s => s.Key)
            .Take(limit)
            .Select(s => (s.Key, s.Value))
            .ToList();
    }
}
