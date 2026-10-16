using System.Collections.Generic;

namespace Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk.Models;

/// <summary>
/// Season with its episodes.
/// </summary>
internal sealed class KpSeason
{
    public int Number { get; set; }

    public IReadOnlyList<KpEpisode>? Episodes { get; set; }
}
