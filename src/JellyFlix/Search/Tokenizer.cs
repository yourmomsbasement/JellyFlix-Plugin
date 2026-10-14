using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace JellyFlix.Search;

/// <summary>
/// Turns text into search terms: lower case, without accents, common words or plural and tense endings, so
/// "astronauts stranded" finds "an astronaut is stranded".
/// </summary>
public static class Tokenizer
{
    private static readonly FrozenSet<string> _stopWords = new[]
    {
        "a", "about", "after", "all", "an", "and", "any", "are", "as", "at", "be", "been", "but", "by", "can", "do",
        "for", "from", "get", "gets", "has", "have", "he", "her", "his", "how", "i", "in", "into", "is", "it", "its",
        "me", "my", "of", "on", "one", "or", "our", "out", "she", "so", "some", "than", "that", "the", "their", "them",
        "then", "there", "they", "this", "to", "up", "was", "we", "were", "what", "when", "where", "which", "who",
        "will", "with", "you", "your",

        // Words that describe every title in a media library
        "movie", "movies", "film", "films", "show", "shows", "series", "tv", "episode", "episodes", "season", "seasons",
        "watch", "like", "where", "want", "something", "kind", "type", "story"
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// Splits text into search terms.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The terms, in order, repeats included.</returns>
    public static IReadOnlyList<string> Tokenize(string? text)
    {
        var terms = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return terms;
        }

        var word = new StringBuilder();
        foreach (var c in RemoveAccents(text))
        {
            if (char.IsLetterOrDigit(c))
            {
                word.Append(char.ToLowerInvariant(c));
            }
            else if (c != '\'' && c != '’')
            {
                // Apostrophes are dropped ("don't" → "dont"); anything else ends the word
                Flush(word, terms);
            }
        }

        Flush(word, terms);
        return terms;
    }

    /// <summary>
    /// Reduces a word to a simple stem: "stranded" → "strand", "stories" → "story", "astronauts" → "astronaut".
    /// </summary>
    /// <param name="word">A lower-case word.</param>
    /// <returns>The stem.</returns>
    public static string Stem(string word)
    {
        if (word.Length <= 3 || char.IsDigit(word[^1]))
        {
            return word;
        }

        if (word.EndsWith("ies", StringComparison.Ordinal) && word.Length > 4)
        {
            return word[..^3] + "y";
        }

        foreach (var suffix in (ReadOnlySpan<string>)["ingly", "edly", "ing", "ed", "ly", "es", "s"])
        {
            if (word.EndsWith(suffix, StringComparison.Ordinal) && word.Length - suffix.Length >= 3)
            {
                // "es" only after s, x, z, ch, sh ("boxes", "watches"); otherwise just the "s" ("movies" handled above)
                if (suffix == "es" && !(word[..^2].EndsWith('s') || word[..^2].EndsWith('x') || word[..^2].EndsWith('z') || word[..^2].EndsWith("ch", StringComparison.Ordinal) || word[..^2].EndsWith("sh", StringComparison.Ordinal)))
                {
                    continue;
                }

                if (suffix == "s" && (word.EndsWith("ss", StringComparison.Ordinal) || word.EndsWith("us", StringComparison.Ordinal) || word.EndsWith("is", StringComparison.Ordinal)))
                {
                    return word;
                }

                return word[..^suffix.Length];
            }
        }

        return word;
    }

    private static void Flush(StringBuilder word, List<string> terms)
    {
        if (word.Length == 0)
        {
            return;
        }

        var text = word.ToString();
        word.Clear();
        if (!_stopWords.Contains(text))
        {
            terms.Add(Stem(text));
        }
    }

    private static string RemoveAccents(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }
}
