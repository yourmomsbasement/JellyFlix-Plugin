using System;

namespace JellyFlix.Recommendations.Library;

/// <summary>
/// Decides when the shared title catalog is rebuilt after the library changes.
/// </summary>
/// <remarks>
/// A library scan updates titles one after another, sometimes for many minutes. Rebuilding on every change would
/// rebuild the catalog for every request during a scan, so a rebuild waits until the library has been quiet for
/// <see cref="QuietPeriod"/>, and happens at least every <see cref="MaxStaleness"/> while changes keep coming.
/// Not thread-safe; the caller locks.
/// </remarks>
public sealed class CatalogFreshness
{
    /// <summary>
    /// How long the library has to be unchanged before a changed catalog is rebuilt.
    /// </summary>
    public static readonly TimeSpan QuietPeriod = TimeSpan.FromMinutes(1);

    /// <summary>
    /// The longest a changed catalog keeps being used while changes keep coming.
    /// </summary>
    public static readonly TimeSpan MaxStaleness = TimeSpan.FromMinutes(5);

    private DateTimeOffset? _builtAt;
    private DateTimeOffset? _firstChangeSinceBuild;
    private DateTimeOffset? _lastChange;

    /// <summary>
    /// Records that a movie or show was added, removed or updated.
    /// </summary>
    /// <param name="now">The current time.</param>
    public void Changed(DateTimeOffset now)
    {
        _firstChangeSinceBuild ??= now;
        _lastChange = now;
    }

    /// <summary>
    /// Records that the catalog was just built.
    /// </summary>
    /// <param name="now">The current time.</param>
    public void Built(DateTimeOffset now)
    {
        _builtAt = now;
        _firstChangeSinceBuild = null;
        _lastChange = null;
    }

    /// <summary>
    /// Whether the catalog should be (re)built now.
    /// </summary>
    /// <param name="now">The current time.</param>
    /// <returns>True when there is no catalog, or it changed and the library is quiet or it has waited long enough.</returns>
    public bool NeedsRebuild(DateTimeOffset now)
    {
        if (_builtAt is null)
        {
            return true;
        }

        if (_lastChange is not { } last || _firstChangeSinceBuild is not { } first)
        {
            return false;
        }

        return now - last >= QuietPeriod || now - first >= MaxStaleness;
    }
}
