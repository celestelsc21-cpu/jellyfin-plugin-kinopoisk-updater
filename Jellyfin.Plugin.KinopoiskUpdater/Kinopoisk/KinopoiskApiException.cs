using System;

namespace Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk;

/// <summary>
/// Thrown when a Kinopoisk API request fails. Messages never contain the API key.
/// </summary>
public sealed class KinopoiskApiException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KinopoiskApiException"/> class.
    /// </summary>
    public KinopoiskApiException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="KinopoiskApiException"/> class.
    /// </summary>
    /// <param name="message">Error message.</param>
    public KinopoiskApiException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="KinopoiskApiException"/> class.
    /// </summary>
    /// <param name="message">Error message.</param>
    /// <param name="innerException">Cause.</param>
    public KinopoiskApiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="KinopoiskApiException"/> class.
    /// </summary>
    /// <param name="message">Error message.</param>
    /// <param name="kind">Error kind.</param>
    /// <param name="statusCode">HTTP status code, if a response was received.</param>
    /// <param name="innerException">Cause.</param>
    public KinopoiskApiException(string message, KinopoiskErrorKind kind, int? statusCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
        StatusCode = statusCode;
    }

    /// <summary>
    /// Gets the error kind.
    /// </summary>
    public KinopoiskErrorKind Kind { get; }

    /// <summary>
    /// Gets the HTTP status code, if a response was received.
    /// </summary>
    public int? StatusCode { get; }
}
