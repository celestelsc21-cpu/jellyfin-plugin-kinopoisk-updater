namespace Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk.Models;

/// <summary>
/// Item of <see cref="KpFilmListResponse"/>.
/// </summary>
internal sealed class KpFilmListItem
{
    public int KinopoiskId { get; set; }

    public string? NameRu { get; set; }

    public string? NameEn { get; set; }

    public string? NameOriginal { get; set; }

    public int? Year { get; set; }

    public string? Type { get; set; }

    public string? PosterUrlPreview { get; set; }
}
