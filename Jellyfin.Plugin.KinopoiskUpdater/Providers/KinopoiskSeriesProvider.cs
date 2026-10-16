using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.KinopoiskUpdater.Providers;

/// <summary>
/// Kinopoisk metadata for series ("Refresh metadata" and "Identify").
/// </summary>
public class KinopoiskSeriesProvider : IRemoteMetadataProvider<Series, SeriesInfo>, IHasOrder
{
    private readonly IKinopoiskMetadataService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="KinopoiskSeriesProvider"/> class.
    /// </summary>
    /// <param name="service">Kinopoisk metadata service.</param>
    public KinopoiskSeriesProvider(IKinopoiskMetadataService service)
    {
        _service = service;
    }

    /// <inheritdoc />
    public string Name => KinopoiskConstants.ProviderName;

    /// <inheritdoc />
    public int Order => 0;

    /// <inheritdoc />
    public Task<MetadataResult<Series>> GetMetadata(SeriesInfo info, CancellationToken cancellationToken)
        => _service.GetMetadataAsync<Series>(info, isSeries: true, cancellationToken);

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken)
        => await _service.SearchAsync(searchInfo, isSeries: true, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        => _service.GetImageResponseAsync(url, cancellationToken);
}
