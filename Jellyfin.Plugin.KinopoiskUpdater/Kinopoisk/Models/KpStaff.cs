namespace Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk.Models;

/// <summary>
/// Person of <c>/api/v1/staff?filmId=</c>.
/// </summary>
internal sealed class KpStaff
{
    public int StaffId { get; set; }

    public string? NameRu { get; set; }

    public string? NameEn { get; set; }

    /// <summary>
    /// Gets or sets the character name for actors.
    /// </summary>
    public string? Description { get; set; }

    public string? PosterUrl { get; set; }

    /// <summary>
    /// Gets or sets the profession: ACTOR, DIRECTOR, WRITER, PRODUCER, COMPOSER, EDITOR, ...
    /// </summary>
    public string? ProfessionKey { get; set; }
}
