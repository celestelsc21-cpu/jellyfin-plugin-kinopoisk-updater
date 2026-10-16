using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.KinopoiskUpdater.Providers;

/// <summary>
/// Kinopoisk poster, cover and logo for movies and series.
/// </summary>
public class KinopoiskImageProvider : IRemoteImageProvider, IHasOrder
{
    private readonly IKinopoiskMetadataService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="KinopoiskImageProvider"/> class.
    /// </summary>
    /// <param name="service">Kinopoisk metadata service.</param>
    public KinopoiskImageProvider(IKinopoiskMetadataService service)
    {
        _service = service;
    }

    /// <inheritdoc />
    public string Name => KinopoiskConstants.ProviderName;

    /// <inheritdoc />
    public int Order => 0;

    /// <inheritdoc />
    public bool Supports(BaseItem item) => item is Movie || item is Series;

    /// <inheritdoc />
    public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        => [ImageType.Primary, ImageType.Backdrop, ImageType.Logo];

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        => await _service.GetImagesAsync(item, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        => _service.GetImageResponseAsync(url, cancellationToken);
}
