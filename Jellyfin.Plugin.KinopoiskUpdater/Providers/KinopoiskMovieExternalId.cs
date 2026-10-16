using Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.KinopoiskUpdater.Providers;

/// <summary>
/// Kinopoisk id field of movies (metadata editor and "Identify" dialog).
/// </summary>
public class KinopoiskMovieExternalId : IExternalId
{
    /// <inheritdoc />
    public string ProviderName => KinopoiskConstants.ProviderName;

    /// <inheritdoc />
    public string Key => KinopoiskConstants.ProviderIdKey;

    /// <inheritdoc />
    public ExternalIdMediaType? Type => ExternalIdMediaType.Movie;

    /// <inheritdoc />
    public bool Supports(IHasProviderIds item) => item is Movie;
}
