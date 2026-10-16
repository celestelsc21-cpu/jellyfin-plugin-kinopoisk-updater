using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk;
using Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.KinopoiskUpdater.Metadata;

/// <summary>
/// Converts Kinopoisk responses into Jellyfin item fields and people.
/// </summary>
internal static partial class KinopoiskMapper
{
    private static readonly Dictionary<string, PersonKind> PersonKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ACTOR"] = PersonKind.Actor,
        ["DIRECTOR"] = PersonKind.Director,
        ["WRITER"] = PersonKind.Writer,
        ["PRODUCER"] = PersonKind.Producer,
        ["PRODUCER_USSR"] = PersonKind.Producer,
        ["COMPOSER"] = PersonKind.Composer,
        ["EDITOR"] = PersonKind.Editor
    };

    /// <summary>
    /// Returns a value indicating whether a card describes a series.
    /// </summary>
    /// <param name="film">Kinopoisk card.</param>
    /// <returns><c>true</c> for series and TV shows.</returns>
    public static bool IsSeries(KpFilm film)
    {
        ArgumentNullException.ThrowIfNull(film);
        return film.Serial == true || KinopoiskParsing.IsSeriesType(film.Type);
    }

    /// <summary>
    /// Writes the fields of a film or series card. Fields that Kinopoisk leaves empty are not touched.
    /// </summary>
    /// <param name="item">Movie or series.</param>
    /// <param name="film">Kinopoisk card.</param>
    /// <param name="premiereDate">Premiere date from distributions, if known.</param>
    public static void ApplyFilm(BaseItem item, KpFilm film, DateTime? premiereDate)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(film);

        var name = KinopoiskParsing.FirstNonEmpty(film.NameRu, film.NameEn, film.NameOriginal);
        if (name is not null)
        {
            item.Name = name;
        }

        var originalTitle = KinopoiskParsing.FirstNonEmpty(film.NameOriginal, film.NameEn);
        if (originalTitle is not null)
        {
            item.OriginalTitle = originalTitle;
        }

        var overview = KinopoiskParsing.FirstNonEmpty(film.Description, film.ShortDescription);
        if (overview is not null)
        {
            item.Overview = overview;
        }

        var tagline = KinopoiskParsing.FirstNonEmpty(film.Slogan);
        if (tagline is not null)
        {
            item.Tagline = tagline;
        }

        var year = IsSeries(film) ? film.StartYear ?? film.Year : film.Year;
        if (year is > 0)
        {
            item.ProductionYear = year;
        }

        if (premiereDate.HasValue)
        {
            item.PremiereDate = premiereDate;
        }

        var officialRating = KinopoiskParsing.ParseAgeLimit(film.RatingAgeLimits);
        if (officialRating is not null)
        {
            item.OfficialRating = officialRating;
        }

        if (film.RatingKinopoisk is > 0 and <= 10)
        {
            item.CommunityRating = (float)Math.Round(film.RatingKinopoisk.Value, 1);
        }

        var genres = Distinct(film.Genres?.Select(g => KinopoiskParsing.Capitalize(g.Genre)));
        if (genres.Length > 0)
        {
            item.Genres = genres;
        }

        var countries = Distinct(film.Countries?.Select(c => KinopoiskParsing.FirstNonEmpty(c.Country)));
        if (countries.Length > 0)
        {
            item.ProductionLocations = countries;
        }

        if (film.KinopoiskId > 0)
        {
            item.SetProviderId(KinopoiskConstants.ProviderIdKey, film.KinopoiskId.ToString(CultureInfo.InvariantCulture));
        }

        var imdbId = film.ImdbId?.Trim();
        if (imdbId is not null && ImdbIdRegex().IsMatch(imdbId))
        {
            item.SetProviderId(MetadataProvider.Imdb, imdbId);
        }

        if (item is Series series && film.Completed.HasValue)
        {
            series.Status = film.Completed.Value ? SeriesStatus.Ended : SeriesStatus.Continuing;
        }
    }

    /// <summary>
    /// Finds an episode by season and episode number.
    /// </summary>
    /// <param name="episodes">All episodes of the series.</param>
    /// <param name="seasonNumber">Season number in Jellyfin.</param>
    /// <param name="episodeNumber">Episode number in Jellyfin.</param>
    /// <returns>The episode, or <c>null</c> when there is no pair.</returns>
    public static KpEpisode? FindEpisode(IEnumerable<KpEpisode> episodes, int? seasonNumber, int? episodeNumber)
    {
        ArgumentNullException.ThrowIfNull(episodes);
        if (seasonNumber is null || episodeNumber is null)
        {
            return null;
        }

        return episodes.FirstOrDefault(e => e.SeasonNumber == seasonNumber && e.EpisodeNumber == episodeNumber);
    }

    /// <summary>
    /// Writes the fields of an episode. Fields that Kinopoisk leaves empty are not touched.
    /// </summary>
    /// <param name="item">Episode.</param>
    /// <param name="episode">Kinopoisk episode.</param>
    public static void ApplyEpisode(BaseItem item, KpEpisode episode)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(episode);

        var name = KinopoiskParsing.FirstNonEmpty(episode.NameRu, episode.NameEn);
        if (name is not null)
        {
            item.Name = name;
        }

        var originalTitle = KinopoiskParsing.FirstNonEmpty(episode.NameEn);
        if (originalTitle is not null)
        {
            item.OriginalTitle = originalTitle;
        }

        var overview = KinopoiskParsing.FirstNonEmpty(episode.Synopsis);
        if (overview is not null)
        {
            item.Overview = overview;
        }

        var date = KinopoiskParsing.ParseDate(episode.ReleaseDate);
        if (date.HasValue)
        {
            item.PremiereDate = date;
            item.ProductionYear = date.Value.Year;
        }

        if (episode.EpisodeNumber is > 0)
        {
            item.IndexNumber = episode.EpisodeNumber;
        }

        if (episode.SeasonNumber is >= 0)
        {
            item.ParentIndexNumber = episode.SeasonNumber;
        }
    }

    /// <summary>
    /// Picks the earliest world premiere, falling back to the earliest premiere.
    /// </summary>
    /// <param name="distributions">Distribution entries.</param>
    /// <returns>Premiere date, or <c>null</c>.</returns>
    public static DateTime? SelectPremiereDate(IEnumerable<KpDistribution> distributions)
    {
        ArgumentNullException.ThrowIfNull(distributions);
        var list = distributions.ToList();
        return EarliestOfType(list, "WORLD_PREMIER") ?? EarliestOfType(list, "PREMIERE");
    }

    /// <summary>
    /// Converts Kinopoisk staff into Jellyfin people: actors (limited) first, then crew.
    /// </summary>
    /// <param name="staff">Staff in Kinopoisk order.</param>
    /// <param name="maxActors">Maximum number of actors.</param>
    /// <returns>People; professions without a Jellyfin equivalent are skipped.</returns>
    public static List<PersonInfo> BuildPeople(IEnumerable<KpStaff> staff, int maxActors)
    {
        ArgumentNullException.ThrowIfNull(staff);

        var people = new List<PersonInfo>();
        var actors = 0;
        foreach (var member in staff)
        {
            if (member.ProfessionKey is null || !PersonKinds.TryGetValue(member.ProfessionKey, out var kind))
            {
                continue;
            }

            var name = KinopoiskParsing.FirstNonEmpty(member.NameRu, member.NameEn);
            if (name is null)
            {
                continue;
            }

            if (kind == PersonKind.Actor && ++actors > maxActors)
            {
                continue;
            }

            var person = new PersonInfo
            {
                Name = name,
                Type = kind,
                Role = kind == PersonKind.Actor ? KinopoiskParsing.FirstNonEmpty(member.Description) ?? string.Empty : string.Empty,
                ImageUrl = IsHttpUrl(member.PosterUrl) ? member.PosterUrl : null,
                SortOrder = people.Count
            };

            if (member.StaffId > 0)
            {
                person.SetProviderId(KinopoiskConstants.ProviderIdKey, member.StaffId.ToString(CultureInfo.InvariantCulture));
            }

            people.Add(person);
        }

        return people;
    }

    private static DateTime? EarliestOfType(List<KpDistribution> distributions, string type)
        => distributions
            .Where(d => string.Equals(d.Type, type, StringComparison.OrdinalIgnoreCase))
            .Select(d => KinopoiskParsing.ParseDate(d.Date))
            .Where(d => d.HasValue)
            .Min();

    private static string[] Distinct(IEnumerable<string?>? values)
        => values is null
            ? []
            : values.OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    private static bool IsHttpUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    [GeneratedRegex("^tt[0-9]{5,10}$", RegexOptions.CultureInvariant)]
    private static partial Regex ImdbIdRegex();
}
