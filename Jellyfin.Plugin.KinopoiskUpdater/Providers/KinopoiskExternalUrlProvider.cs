using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk;
using Jellyfin.Plugin.KinopoiskUpdater.Metadata;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;

namespace Jellyfin.Plugin.KinopoiskUpdater.Providers;

/// <summary>
/// "Kinopoisk" link on movie, series and person pages.
/// </summary>
public class KinopoiskExternalUrlProvider : IExternalUrlProvider
{
    /// <inheritdoc />
    public string Name => KinopoiskConstants.ProviderName;

    /// <inheritdoc />
    public IEnumerable<string> GetExternalUrls(BaseItem item)
    {
        if (item is null)
        {
            yield break;
        }

        var id = KinopoiskIdResolver.GetStoredId(item.ProviderIds);
        if (id is null)
        {
            yield break;
        }

        switch (item)
        {
            case Movie or Series:
                yield return KinopoiskConstants.GetWebUri(id.Value).ToString();
                break;
            case Person:
                yield return string.Create(CultureInfo.InvariantCulture, $"https://www.kinopoisk.ru/name/{id.Value}/");
                break;
        }
    }
}
