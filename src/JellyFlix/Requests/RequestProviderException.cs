using System;
using System.Net;

namespace JellyFlix.Requests;

/// <summary>
/// A request service (Seerr or Ombi) refused or couldn't be reached, with the reason in words.
/// </summary>
public class RequestProviderException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RequestProviderException"/> class.
    /// </summary>
    public RequestProviderException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestProviderException"/> class.
    /// </summary>
    /// <param name="message">The reason.</param>
    public RequestProviderException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestProviderException"/> class.
    /// </summary>
    /// <param name="message">The reason.</param>
    /// <param name="innerException">The cause.</param>
    public RequestProviderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestProviderException"/> class.
    /// </summary>
    /// <param name="statusCode">The HTTP status that fits.</param>
    /// <param name="message">The reason.</param>
    /// <param name="innerException">The cause, if any.</param>
    public RequestProviderException(HttpStatusCode statusCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    /// <summary>
    /// Gets the HTTP status that fits the failure.
    /// </summary>
    public HttpStatusCode StatusCode { get; } = HttpStatusCode.BadGateway;
}
