using System;
using System.Net;

namespace JellyFlix.Requests;

/// <summary>
/// Seerr refused a call, or couldn't be reached.
/// </summary>
public class SeerrException : Exception
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
    /// <param name="message">The message.</param>
    public SeerrException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SeerrException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public SeerrException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SeerrException"/> class.
    /// </summary>
    /// <param name="statusCode">The status code Seerr answered with.</param>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public SeerrException(HttpStatusCode statusCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    /// <summary>
    /// Gets the status code Seerr answered with.
    /// </summary>
    public HttpStatusCode StatusCode { get; } = HttpStatusCode.BadGateway;
}
