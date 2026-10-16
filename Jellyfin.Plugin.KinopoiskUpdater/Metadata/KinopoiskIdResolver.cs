using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.KinopoiskUpdater.Metadata;

/// <summary>
/// Finds the Kinopoisk id of a Jellyfin item: stored id → IMDb id → title and year.
/// </summary>
internal sealed class KinopoiskIdResolver
{
    private readonly IKinopoiskClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="KinopoiskIdResolver"/> class.
    /// </summary>
    /// <param name="client">Kinopoisk client.</param>
    public KinopoiskIdResolver(IKinopoiskClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Reads a stored Kinopoisk id (the key is compared case-insensitively for ids written by other tools).
    /// </summary>
    /// <param name="providerIds">Provider ids of the item.</param>
    /// <returns>Id, or <c>null</c> when absent or invalid.</returns>
    public static int? GetStoredId(IEnumerable<KeyValuePair<string, string>>? providerIds)
    {
        if (providerIds is null)
        {
            return null;
        }

        foreach (var pair in providerIds)
        {
            if (string.Equals(pair.Key, KinopoiskConstants.ProviderIdKey, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(pair.Value?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                && id > 0)
            {
                return id;
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves the Kinopoisk id for automatic metadata refresh.
    /// A title search result is used only when it is a confident match.
    /// </summary>
    /// <param name="info">Lookup info of the item.</param>
    /// <param name="isSeries">Whether the item is a series.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Id, or <c>null</c> when nothing reliable was found.</returns>
    public async Task<int?> ResolveAsync(ItemLookupInfo info, bool isSeries, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(info);

        var stored = GetStoredId(info.ProviderIds);
        if (stored.HasValue)
        {
            return stored;
        }

        var imdbId = GetImdbId(info.ProviderIds);
        if (imdbId is not null)
        {
            var byImdb = await _client.FindByImdbIdAsync(imdbId, cancellationToken).ConfigureAwait(false);
            if (byImdb.Count > 0)
            {
                return byImdb[0].Id;
            }
        }

        var candidates = await SearchAsync(info.Name, info.OriginalTitle, info.Year, isSeries, cancellationToken).ConfigureAwait(false);
        return candidates
            .FirstOrDefault(c => KinopoiskMatcher.IsConfidentMatch(c, info.Name, info.OriginalTitle, info.Year, isSeries))
            ?.Id;
    }

    /// <summary>
    /// Searches by title and original title and ranks the merged results.
    /// </summary>
    /// <param name="name">Title.</param>
    /// <param name="originalTitle">Original title.</param>
    /// <param name="year">Year.</param>
    /// <param name="isSeries">Whether a series is wanted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Ranked candidates without duplicates.</returns>
    public async Task<IReadOnlyList<KpCandidate>> SearchAsync(
        string? name,
        string? originalTitle,
        int? year,
        bool isSeries,
        CancellationToken cancellationToken)
    {
        var queries = new[] { name, originalTitle }
            .Where(q => !string.IsNullOrWhiteSpace(q))
            .Select(q => q!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var found = new List<KpCandidate>();
        var seen = new HashSet<int>();
        foreach (var query in queries)
        {
            var results = await _client.SearchByKeywordAsync(query, cancellationToken).ConfigureAwait(false);
            foreach (var candidate in results)
            {
                if (seen.Add(candidate.Id))
                {
                    found.Add(candidate);
                }
            }
        }

        return KinopoiskMatcher.Rank(found, year, isSeries);
    }

    /// <summary>
    /// Reads a stored IMDb id (key compared case-insensitively).
    /// </summary>
    /// <param name="providerIds">Provider ids of the item.</param>
    /// <returns>IMDb id, or <c>null</c>.</returns>
    public static string? GetImdbId(IEnumerable<KeyValuePair<string, string>>? providerIds)
    {
        if (providerIds is null)
        {
            return null;
        }

        const string key = nameof(MetadataProvider.Imdb);
        foreach (var pair in providerIds)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(pair.Value))
            {
                return pair.Value.Trim();
            }
        }

        return null;
    }
}
