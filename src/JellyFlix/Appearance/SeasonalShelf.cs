using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using JellyFlix.Search;

namespace JellyFlix.Appearance;

/// <summary>
/// A curated shelf on the seasonal home page: titles whose metadata mentions the season, from some genres, or both.
/// </summary>
public sealed class SeasonalShelf
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SeasonalShelf"/> class.
    /// </summary>
    /// <param name="key">The shelf's name for the client, which shows it in the profile's language.</param>
    /// <param name="keywords">Words a title must mention (in its name, tagline, plot or tags); none for any.</param>
    /// <param name="genres">Genres a title must have one of; none for any.</param>
    public SeasonalShelf(string key, IEnumerable<string> keywords, IEnumerable<string> genres)
    {
        Key = key;
        // Stemmed like the titles' text, so "witches" matches "witch"
        Keywords = keywords.SelectMany(k => Tokenizer.Tokenize(k)).ToFrozenSet();
        Genres = genres.ToFrozenSet(System.StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the shelf's name for the client.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the stemmed words a title must mention.
    /// </summary>
    public FrozenSet<string> Keywords { get; }

    /// <summary>
    /// Gets the genres a title must have one of.
    /// </summary>
    public FrozenSet<string> Genres { get; }

    /// <summary>
    /// Scores how well a title fits the shelf.
    /// </summary>
    /// <param name="words">The title's stemmed words, from <see cref="Tokenizer"/>.</param>
    /// <param name="genres">The title's genres.</param>
    /// <returns>0 when it doesn't belong; higher the more it mentions the season.</returns>
    public int Score(IReadOnlySet<string> words, IEnumerable<string> genres)
    {
        var genreMatch = Genres.Count == 0 || genres.Any(Genres.Contains);
        if (!genreMatch)
        {
            return 0;
        }

        if (Keywords.Count == 0)
        {
            return 1;
        }

        var hits = Keywords.Count(words.Contains);
        return hits == 0 ? 0 : 1 + hits;
    }
}
