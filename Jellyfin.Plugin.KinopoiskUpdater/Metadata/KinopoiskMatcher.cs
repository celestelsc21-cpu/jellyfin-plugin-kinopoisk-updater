using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk;

namespace Jellyfin.Plugin.KinopoiskUpdater.Metadata;

/// <summary>
/// Ranks title search results and decides whether the best one is safe to apply automatically.
/// </summary>
internal static class KinopoiskMatcher
{
    /// <summary>
    /// Scores a candidate: +2 for the right kind (film/series), +3 for the exact year, +1 for a year off by one.
    /// </summary>
    /// <param name="candidate">Search result.</param>
    /// <param name="year">Year of the Jellyfin item.</param>
    /// <param name="isSeries">Whether the Jellyfin item is a series.</param>
    /// <returns>Score from 0 to 5.</returns>
    public static int Score(KpCandidate candidate, int? year, bool isSeries)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var score = KinopoiskParsing.IsSeriesType(candidate.Type) == isSeries ? 2 : 0;
        if (year.HasValue && candidate.Year.HasValue)
        {
            var difference = Math.Abs(candidate.Year.Value - year.Value);
            score += difference == 0 ? 3 : difference == 1 ? 1 : 0;
        }

        return score;
    }

    /// <summary>
    /// Orders candidates by score; equal scores keep the Kinopoisk relevance order.
    /// </summary>
    /// <param name="candidates">Search results.</param>
    /// <param name="year">Year of the Jellyfin item.</param>
    /// <param name="isSeries">Whether the Jellyfin item is a series.</param>
    /// <returns>Ranked candidates.</returns>
    public static IReadOnlyList<KpCandidate> Rank(IEnumerable<KpCandidate> candidates, int? year, bool isSeries)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        return candidates.OrderByDescending(c => Score(c, year, isSeries)).ToList();
    }

    /// <summary>
    /// Returns a value indicating whether a candidate may be applied without the user choosing it:
    /// the kind must match and either the year is within one or, without years to compare, a title matches exactly.
    /// </summary>
    /// <param name="candidate">Search result.</param>
    /// <param name="name">Title of the Jellyfin item.</param>
    /// <param name="originalTitle">Original title of the Jellyfin item.</param>
    /// <param name="year">Year of the Jellyfin item.</param>
    /// <param name="isSeries">Whether the Jellyfin item is a series.</param>
    /// <returns><c>true</c> for a confident match.</returns>
    public static bool IsConfidentMatch(KpCandidate candidate, string? name, string? originalTitle, int? year, bool isSeries)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (KinopoiskParsing.IsSeriesType(candidate.Type) != isSeries)
        {
            return false;
        }

        if (year.HasValue && candidate.Year.HasValue)
        {
            return Math.Abs(candidate.Year.Value - year.Value) <= 1;
        }

        var wanted = new[] { name, originalTitle }.Select(Normalize).Where(t => t.Length > 0).ToList();
        var offered = new[] { candidate.NameRu, candidate.NameEn, candidate.NameOriginal }.Select(Normalize).Where(t => t.Length > 0);
        return offered.Any(o => wanted.Contains(o, StringComparer.Ordinal));
    }

    /// <summary>
    /// Normalizes a title for comparison: lower case, "ё" as "е", letters and digits only, single spaces.
    /// </summary>
    /// <param name="title">Title.</param>
    /// <returns>Normalized title (empty for <c>null</c>).</returns>
    public static string Normalize(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(title.Length);
        var pendingSpace = false;
        foreach (var raw in title)
        {
            var c = char.ToLowerInvariant(raw);
            if (c == 'ё')
            {
                c = 'е';
            }

            if (char.IsLetterOrDigit(c))
            {
                if (pendingSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(c);
                pendingSpace = false;
            }
            else
            {
                pendingSpace = true;
            }
        }

        return builder.ToString();
    }
}
