using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.KinopoiskUpdater.Providers;

/// <summary>
/// Kinopoisk lookups shared by the metadata and image providers.
/// Failures of the Kinopoisk API are logged and returned as empty results.
/// </summary>
public interface IKinopoiskMetadataService
{
    /// <summary>
    /// Gets movie or series metadata and people.
    /// </summary>
    /// <typeparam name="T">Movie or Series.</typeparam>
    /// <param name="info">Lookup info of the item.</param>
    /// <param name="isSeries">Whether the item is a series.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Metadata, or a result without metadata when nothing reliable was found.</returns>
    Task<MetadataResult<T>> GetMetadataAsync<T>(ItemLookupInfo info, bool isSeries, CancellationToken cancellationToken)
        where T : BaseItem, new();

    /// <summary>
    /// Searches Kinopoisk for the "Identify" dialog.
    /// </summary>
    /// <param name="info">Search info (title, year, ids entered by the user).</param>
    /// <param name="isSeries">Whether a series is wanted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Search results, best first.</returns>
    Task<IReadOnlyList<RemoteSearchResult>> SearchAsync(ItemLookupInfo info, bool isSeries, CancellationToken cancellationToken);

    /// <summary>
    /// Gets episode metadata using the Kinopoisk id of the series.
    /// </summary>
    /// <param name="info">Lookup info of the episode.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Metadata, or a result without metadata.</returns>
    Task<MetadataResult<Episode>> GetEpisodeMetadataAsync(EpisodeInfo info, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the images of a movie or series with a stored Kinopoisk id.
    /// </summary>
    /// <param name="item">Movie or series.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Images; empty without an id.</returns>
    Task<IReadOnlyList<RemoteImageInfo>> GetImagesAsync(BaseItem item, CancellationToken cancellationToken);

    /// <summary>
    /// Downloads an image from a Kinopoisk image host.
    /// </summary>
    /// <param name="url">Image address.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response; 400 for addresses outside the Kinopoisk hosts.</returns>
    Task<HttpResponseMessage> GetImageResponseAsync(string url, CancellationToken cancellationToken);
}
