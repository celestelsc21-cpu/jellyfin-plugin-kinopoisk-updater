using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk.Models;

namespace Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk;

/// <summary>
/// Typed access to the kinopoiskapiunofficial.tech API.
/// All methods throw <see cref="KinopoiskApiException"/> on API failures.
/// </summary>
internal interface IKinopoiskClient
{
    /// <summary>
    /// Gets a film or series card.
    /// </summary>
    /// <param name="id">Kinopoisk id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The card, or <c>null</c> when the id does not exist.</returns>
    Task<KpFilm?> GetFilmAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// Finds films by IMDb id.
    /// </summary>
    /// <param name="imdbId">IMDb id such as tt0111161.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching films (usually one); empty for an invalid id.</returns>
    Task<IReadOnlyList<KpCandidate>> FindByImdbIdAsync(string imdbId, CancellationToken cancellationToken);

    /// <summary>
    /// Searches films and series by title.
    /// </summary>
    /// <param name="keyword">Title or part of it.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>First page of results.</returns>
    Task<IReadOnlyList<KpCandidate>> SearchByKeywordAsync(string keyword, CancellationToken cancellationToken);

    /// <summary>
    /// Gets all episodes of a series in one request.
    /// </summary>
    /// <param name="seriesId">Kinopoisk id of the series.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Episodes with season numbers filled in.</returns>
    Task<IReadOnlyList<KpEpisode>> GetEpisodesAsync(int seriesId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets release dates.
    /// </summary>
    /// <param name="id">Kinopoisk id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Distribution entries.</returns>
    Task<IReadOnlyList<KpDistribution>> GetDistributionsAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// Gets cast and crew.
    /// </summary>
    /// <param name="id">Kinopoisk id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>People in Kinopoisk order.</returns>
    Task<IReadOnlyList<KpStaff>> GetStaffAsync(int id, CancellationToken cancellationToken);
}
