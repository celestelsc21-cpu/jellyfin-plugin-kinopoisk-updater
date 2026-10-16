using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk.Models;

/// <summary>
/// Item of <see cref="KpKeywordSearchResponse"/>.
/// </summary>
internal sealed class KpKeywordFilm
{
    public int FilmId { get; set; }

    public string? NameRu { get; set; }

    public string? NameEn { get; set; }

    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the year: a string such as "2010" or "2010-2015" (sometimes "null").
    /// </summary>
    [JsonConverter(typeof(LenientStringConverter))]
    public string? Year { get; set; }

    public string? PosterUrlPreview { get; set; }
}
