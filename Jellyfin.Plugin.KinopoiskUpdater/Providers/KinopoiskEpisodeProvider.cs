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
/// Kinopoisk metadata for episodes, matched by season and episode number
/// within the series that carries a Kinopoisk id.
/// </summary>
public class KinopoiskEpisodeProvider : IRemoteMetadataProvider<Episode, EpisodeInfo>, IHasOrder
{
    private readonly IKinopoiskMetadataService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="KinopoiskEpisodeProvider"/> class.
    /// </summary>
    /// <param name="service">Kinopoisk metadata service.</param>
    public KinopoiskEpisodeProvider(IKinopoiskMetadataService service)
    {
        _service = service;
    }

    /// <inheritdoc />
    public string Name => KinopoiskConstants.ProviderName;

    /// <inheritdoc />
    public int Order => 0;

    /// <inheritdoc />
    public Task<MetadataResult<Episode>> GetMetadata(EpisodeInfo info, CancellationToken cancellationToken)
        => _service.GetEpisodeMetadataAsync(info, cancellationToken);

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(EpisodeInfo searchInfo, CancellationToken cancellationToken)
    {
        var metadata = await _service.GetEpisodeMetadataAsync(searchInfo, cancellationToken).ConfigureAwait(false);
        if (!metadata.HasMetadata)
        {
            return [];
        }

        var item = metadata.Item;
        return
        [
            new RemoteSearchResult
            {
                Name = item.Name,
                Overview = item.Overview,
                IndexNumber = item.IndexNumber,
                IndexNumberEnd = item.IndexNumberEnd,
                ParentIndexNumber = item.ParentIndexNumber,
                PremiereDate = item.PremiereDate,
                ProductionYear = item.ProductionYear,
                ProviderIds = item.ProviderIds,
                SearchProviderName = Name
            }
        ];
    }

    /// <inheritdoc />
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        => _service.GetImageResponseAsync(url, cancellationToken);
}
