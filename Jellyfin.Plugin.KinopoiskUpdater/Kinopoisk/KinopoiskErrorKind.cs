namespace Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk;

/// <summary>
/// Reason of a failed Kinopoisk request.
/// </summary>
public enum KinopoiskErrorKind
{
    /// <summary>
    /// Unexpected status code or response format.
    /// </summary>
    Unexpected = 0,

    /// <summary>
    /// No API key in the plugin settings.
    /// </summary>
    NotConfigured = 1,

    /// <summary>
    /// The API rejected the key (401/403).
    /// </summary>
    Unauthorized = 2,

    /// <summary>
    /// The daily request quota of the key is exhausted (402).
    /// </summary>
    QuotaExceeded = 3,

    /// <summary>
    /// Too many requests (429) even after retries.
    /// </summary>
    RateLimited = 4,

    /// <summary>
    /// Network error, timeout or 5xx after retries.
    /// </summary>
    Unavailable = 5
}
