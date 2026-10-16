namespace Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk.Models;

/// <summary>
/// Release (distribution) entry.
/// </summary>
internal sealed class KpDistribution
{
    /// <summary>
    /// Gets or sets the type: PREMIERE, WORLD_PREMIER, COUNTRY_SPECIFIC, ALL, RERELEASE.
    /// </summary>
    public string? Type { get; set; }

    public string? Date { get; set; }
}
