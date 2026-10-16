using System.Collections.Generic;

namespace Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk.Models;

/// <summary>
/// Response of <c>/api/v2.1/films/search-by-keyword</c>.
/// </summary>
internal sealed class KpKeywordSearchResponse
{
    public IReadOnlyList<KpKeywordFilm>? Films { get; set; }
}
