using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.KinopoiskUpdater.Providers;

/// <summary>
/// Kinopoisk metadata for movies ("Refresh metadata" and "Identify").
/// </summary>
public class KinopoiskMovieProvider : IRemoteMetadataProvider<Movie, MovieInfo>, IHasOrder
{
    private readonly IKinopoiskMetadataService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="KinopoiskMovieProvider"/> class.
    /// </summary>
    /// <param name="service">Kinopoisk metadata service.</param>
    public KinopoiskMovieProvider(IKinopoiskMetadataService service)
    {
        _service = service;
    }

    /// <inheritdoc />
    public string Name => KinopoiskConstants.ProviderName;

    /// <inheritdoc />
    public int Order => 0;

    /// <inheritdoc />
    public Task<MetadataResult<Movie>> GetMetadata(MovieInfo info, CancellationToken cancellationToken)
        => _service.GetMetadataAsync<Movie>(info, isSeries: false, cancellationToken);

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MovieInfo searchInfo, CancellationToken cancellationToken)
        => await _service.SearchAsync(searchInfo, isSeries: false, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        => _service.GetImageResponseAsync(url, cancellationToken);
}
