using System.Collections.Generic;

namespace Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk.Models;

/// <summary>
/// Response of <c>/api/v2.2/films/{id}/distributions</c>.
/// </summary>
internal sealed class KpDistributionsResponse
{
    public IReadOnlyList<KpDistribution>? Items { get; set; }
}
