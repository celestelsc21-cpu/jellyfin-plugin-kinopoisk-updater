using System;
using System.Globalization;

namespace Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk;

/// <summary>
/// Parsing of loosely formatted Kinopoisk values.
/// </summary>
internal static class KinopoiskParsing
{
    private const int MinYear = 1850;
    private const int MaxYear = 2200;

    /// <summary>
    /// Returns a value indicating whether a Kinopoisk type denotes a series.
    /// </summary>
    /// <param name="type">Kinopoisk type.</param>
    /// <returns><c>true</c> for TV_SERIES, MINI_SERIES and TV_SHOW.</returns>
    public static bool IsSeriesType(string? type)
        => string.Equals(type, "TV_SERIES", StringComparison.OrdinalIgnoreCase)
           || string.Equals(type, "MINI_SERIES", StringComparison.OrdinalIgnoreCase)
           || string.Equals(type, "TV_SHOW", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Takes the first year of values such as "2010", "2010-2015" or "2010–...".
    /// </summary>
    /// <param name="value">Raw value.</param>
    /// <returns>Year, or <c>null</c> when absent or implausible.</returns>
    public static int? ParseYear(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();
        if (text.Length < 4
            || !int.TryParse(text.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            || (text.Length > 4 && char.IsDigit(text[4])))
        {
            return null;
        }

        return year is >= MinYear and <= MaxYear ? year : null;
    }

    /// <summary>
    /// Parses a date such as "2008-01-20" or "2008-01-20T00:00:00" into a UTC date without time.
    /// </summary>
    /// <param name="value">Raw value.</param>
    /// <returns>Date, or <c>null</c> when absent or invalid.</returns>
    public static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();
        var datePart = text.Length >= 10 ? text[..10] : text;
        if (DateTime.TryParseExact(datePart, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            && date.Year is >= MinYear and <= MaxYear)
        {
            return DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        }

        return null;
    }

    /// <summary>
    /// Converts the Russian age limit "age16" into the rating "16+".
    /// </summary>
    /// <param name="ageLimit">Raw value.</param>
    /// <returns>Rating, or <c>null</c>.</returns>
    public static string? ParseAgeLimit(string? ageLimit)
    {
        if (string.IsNullOrWhiteSpace(ageLimit))
        {
            return null;
        }

        var text = ageLimit.Trim();
        if (text.StartsWith("age", StringComparison.OrdinalIgnoreCase))
        {
            text = text[3..];
        }

        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var age) && age is >= 0 and <= 21
            ? age.ToString(CultureInfo.InvariantCulture) + "+"
            : null;
    }

    /// <summary>
    /// Upper-cases the first letter ("драма" → "Драма").
    /// </summary>
    /// <param name="value">Raw value.</param>
    /// <returns>Trimmed value with a capital first letter, or <c>null</c> when empty.</returns>
    public static string? Capitalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();
        return string.Create(text.Length, text, static (span, source) =>
        {
            source.AsSpan().CopyTo(span);
            span[0] = char.ToUpperInvariant(span[0]);
        });
    }

    /// <summary>
    /// Returns the first value that is not empty, trimmed.
    /// </summary>
    /// <param name="values">Candidates in priority order.</param>
    /// <returns>Value, or <c>null</c>.</returns>
    public static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}
