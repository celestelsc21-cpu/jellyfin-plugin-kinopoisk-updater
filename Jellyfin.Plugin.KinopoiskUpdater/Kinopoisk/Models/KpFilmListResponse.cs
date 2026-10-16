using System.Collections.Generic;

namespace Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk.Models;

/// <summary>
/// Response of <c>/api/v2.2/films?imdbId=</c>.
/// </summary>
internal sealed class KpFilmListResponse
{
    public IReadOnlyList<KpFilmListItem>? Items { get; set; }
}
