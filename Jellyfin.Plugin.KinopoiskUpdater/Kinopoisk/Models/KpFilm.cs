using System.Collections.Generic;

namespace Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk.Models;

/// <summary>
/// Film or series card from <c>/api/v2.2/films/{id}</c>.
/// </summary>
internal sealed class KpFilm
{
    public int KinopoiskId { get; set; }

    public string? ImdbId { get; set; }

    public string? NameRu { get; set; }

    public string? NameEn { get; set; }

    public string? NameOriginal { get; set; }

    public string? PosterUrl { get; set; }

    public string? PosterUrlPreview { get; set; }

    public string? CoverUrl { get; set; }

    public string? LogoUrl { get; set; }

    public double? RatingKinopoisk { get; set; }

    public int? Year { get; set; }

    public int? StartYear { get; set; }

    public int? EndYear { get; set; }

    public string? Slogan { get; set; }

    public string? Description { get; set; }

    public string? ShortDescription { get; set; }

    /// <summary>
    /// Gets or sets the type: FILM, VIDEO, TV_SERIES, MINI_SERIES or TV_SHOW.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the Russian age limit, e.g. "age16".
    /// </summary>
    public string? RatingAgeLimits { get; set; }

    public bool? Serial { get; set; }

    public bool? Completed { get; set; }

    public IReadOnlyList<KpCountry>? Countries { get; set; }

    public IReadOnlyList<KpGenre>? Genres { get; set; }
}
