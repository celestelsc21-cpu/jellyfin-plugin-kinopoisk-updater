namespace Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk;

/// <summary>
/// Search result normalized from the different Kinopoisk search endpoints.
/// </summary>
internal sealed class KpCandidate
{
    /// <summary>
    /// Gets the Kinopoisk id.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Gets the Russian title.
    /// </summary>
    public string? NameRu { get; init; }

    /// <summary>
    /// Gets the English title.
    /// </summary>
    public string? NameEn { get; init; }

    /// <summary>
    /// Gets the original title.
    /// </summary>
    public string? NameOriginal { get; init; }

    /// <summary>
    /// Gets the first year (start year for series).
    /// </summary>
    public int? Year { get; init; }

    /// <summary>
    /// Gets the Kinopoisk type (FILM, TV_SERIES, ...).
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// Gets the small poster address.
    /// </summary>
    public string? PosterUrl { get; init; }
}
