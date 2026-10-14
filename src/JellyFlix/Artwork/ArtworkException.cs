using System;

namespace JellyFlix.Artwork;

/// <summary>
/// An artwork choice that can't be made, with the reason in words.
/// </summary>
public class ArtworkException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArtworkException"/> class.
    /// </summary>
    public ArtworkException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ArtworkException"/> class.
    /// </summary>
    /// <param name="message">The reason.</param>
    public ArtworkException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ArtworkException"/> class.
    /// </summary>
    /// <param name="message">The reason.</param>
    /// <param name="innerException">The cause.</param>
    public ArtworkException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
