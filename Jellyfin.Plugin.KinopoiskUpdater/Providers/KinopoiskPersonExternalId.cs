using Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.KinopoiskUpdater.Providers;

/// <summary>
/// Kinopoisk id field of people (staff id, set from cast and crew).
/// </summary>
public class KinopoiskPersonExternalId : IExternalId
{
    /// <inheritdoc />
    public string ProviderName => KinopoiskConstants.ProviderName;

    /// <inheritdoc />
    public string Key => KinopoiskConstants.ProviderIdKey;

    /// <inheritdoc />
    public ExternalIdMediaType? Type => ExternalIdMediaType.Person;

    /// <inheritdoc />
    public bool Supports(IHasProviderIds item) => item is Person;
}
