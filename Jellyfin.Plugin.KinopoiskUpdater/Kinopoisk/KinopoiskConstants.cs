using System;

namespace Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk;

/// <summary>
/// Shared Kinopoisk identifiers and endpoints.
/// </summary>
public static class KinopoiskConstants
{
    /// <summary>
    /// Provider name shown in library settings and the "Identify" dialog.
    /// </summary>
    public const string ProviderName = "Kinopoisk";

    /// <summary>
    /// Key of the Kinopoisk id in item provider ids. Never change it: stored items rely on it.
    /// </summary>
    public const string ProviderIdKey = "KinoPoisk";

    /// <summary>
    /// Name of the HTTP client registered for the Kinopoisk API.
    /// </summary>
    public const string HttpClientName = "Kinopoisk";

    /// <summary>
    /// Base address of the kinopoiskapiunofficial.tech API. Fixed on purpose: the plugin never
    /// requests addresses taken from settings, so there is nothing to redirect (no SSRF surface).
    /// </summary>
    public static readonly Uri ApiBaseUri = new("https://kinopoiskapiunofficial.tech/");

    /// <summary>
    /// Builds the public kinopoisk.ru page address of a film or series.
    /// </summary>
    /// <param name="id">Kinopoisk id.</param>
    /// <returns>Page address.</returns>
    public static Uri GetWebUri(int id) => new(FormattableString.Invariant($"https://www.kinopoisk.ru/film/{id}/"));
}
