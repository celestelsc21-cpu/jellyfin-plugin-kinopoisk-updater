using System;
using System.Diagnostics.CodeAnalysis;

namespace Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk;

/// <summary>
/// Hosts that Kinopoisk serves posters and photos from. Image downloads through the plugin
/// are limited to them, so the provider cannot be used to fetch arbitrary addresses (SSRF).
/// </summary>
internal static class KinopoiskImageHosts
{
    private static readonly string[] AllowedDomains =
    [
        "kinopoiskapiunofficial.tech",
        "kinopoisk.ru",
        "yandex.net",
        "yastatic.net"
    ];

    /// <summary>
    /// Checks that an address is an absolute http(s) address on a Kinopoisk image host.
    /// </summary>
    /// <param name="url">Address to check.</param>
    /// <param name="uri">Parsed address when allowed.</param>
    /// <returns><c>true</c> when the address may be downloaded.</returns>
    public static bool IsAllowed(string? url, [NotNullWhen(true)] out Uri? uri)
    {
        uri = null;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp)
            || !string.IsNullOrEmpty(parsed.UserInfo))
        {
            return false;
        }

        var host = parsed.IdnHost;
        foreach (var domain in AllowedDomains)
        {
            if (string.Equals(host, domain, StringComparison.OrdinalIgnoreCase)
                || host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase))
            {
                uri = parsed;
                return true;
            }
        }

        return false;
    }
}
