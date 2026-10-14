using System;
using System.Net;

namespace JellyFlix.Requests;

/// <summary>
/// Seerr refused a call, or couldn't be reached.
/// </summary>
public class SeerrException : RequestProviderException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SeerrException"/> class.
    /// </summary>
    public SeerrException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SeerrException"/> class.
    /// </summary>
    /// <param name="message">The reason.</param>
    public SeerrException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SeerrException"/> class.
    /// </summary>
    /// <param name="message">The reason.</param>
    /// <param name="innerException">The cause.</param>
    public SeerrException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SeerrException"/> class.
    /// </summary>
    /// <param name="statusCode">The HTTP status that fits.</param>
    /// <param name="message">The reason.</param>
    /// <param name="innerException">The cause, if any.</param>
    public SeerrException(HttpStatusCode statusCode, string message, Exception? innerException = null)
        : base(statusCode, message, innerException)
    {
    }
}
