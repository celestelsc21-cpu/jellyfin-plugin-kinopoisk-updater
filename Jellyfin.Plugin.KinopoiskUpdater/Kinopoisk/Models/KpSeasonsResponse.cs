using System.Collections.Generic;

namespace Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk.Models;

/// <summary>
/// Response of <c>/api/v2.2/films/{id}/seasons</c>.
/// </summary>
internal sealed class KpSeasonsResponse
{
    public IReadOnlyList<KpSeason>? Items { get; set; }
}
