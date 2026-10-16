using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.KinopoiskUpdater.Configuration;
using Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk;
using Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk.Models;
using Jellyfin.Plugin.KinopoiskUpdater.Metadata;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.KinopoiskUpdater.Providers;

/// <summary>
/// Default <see cref="IKinopoiskMetadataService"/> on top of the Kinopoisk client.
/// </summary>
internal sealed class KinopoiskMetadataService : IKinopoiskMetadataService
{
    /// <summary>
    /// Maximum number of results shown in the "Identify" dialog.
    /// </summary>
    internal const int MaxSearchResults = 20;

    private const string ResultLanguage = "ru";

    private readonly IKinopoiskClient _client;
    private readonly KinopoiskIdResolver _resolver;
    private readonly IPluginConfigurationAccessor _config;
    private readonly ILibraryManager _libraryManager;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<KinopoiskMetadataService> _logger;
    private int _notConfiguredLogged;

    /// <summary>
    /// Initializes a new instance of the <see cref="KinopoiskMetadataService"/> class.
    /// </summary>
    /// <param name="client">Kinopoisk client.</param>
    /// <param name="resolver">Id resolver.</param>
    /// <param name="config">Plugin configuration.</param>
    /// <param name="libraryManager">Library manager (title parsing).</param>
    /// <param name="httpClientFactory">HTTP client factory (image downloads).</param>
    /// <param name="logger">Logger.</param>
    public KinopoiskMetadataService(
        IKinopoiskClient client,
        KinopoiskIdResolver resolver,
        IPluginConfigurationAccessor config,
        ILibraryManager libraryManager,
        IHttpClientFactory httpClientFactory,
        ILogger<KinopoiskMetadataService> logger)
    {
        _client = client;
        _resolver = resolver;
        _config = config;
        _libraryManager = libraryManager;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<MetadataResult<T>> GetMetadataAsync<T>(ItemLookupInfo info, bool isSeries, CancellationToken cancellationToken)
        where T : BaseItem, new()
    {
        ArgumentNullException.ThrowIfNull(info);

        try
        {
            var storedId = KinopoiskIdResolver.GetStoredId(info.ProviderIds);
            var id = storedId ?? await _resolver.ResolveAsync(CleanLookup(info), isSeries, cancellationToken).ConfigureAwait(false);
            if (id is null)
            {
                _logger.LogDebug("No reliable Kinopoisk match for {Name} ({Year})", info.Name, info.Year);
                return new MetadataResult<T>();
            }

            var film = await _client.GetFilmAsync(id.Value, cancellationToken).ConfigureAwait(false);
            if (film is null)
            {
                _logger.LogWarning("Kinopoisk id {KinopoiskId} does not exist", id.Value);
                return new MetadataResult<T>();
            }

            if (KinopoiskMapper.IsSeries(film) != isSeries)
            {
                _logger.LogWarning(
                    "Kinopoisk id {KinopoiskId} is a {KinopoiskType}, but {Name} is a {ItemType} in Jellyfin",
                    id.Value,
                    film.Type,
                    info.Name,
                    typeof(T).Name);
            }

            var distributions = await GetOptionalAsync(
                () => _client.GetDistributionsAsync(id.Value, cancellationToken),
                "release dates",
                id.Value).ConfigureAwait(false);

            var item = new T();
            KinopoiskMapper.ApplyFilm(item, film, KinopoiskMapper.SelectPremiereDate(distributions));

            var result = new MetadataResult<T>
            {
                Item = item,
                HasMetadata = true,
                QueriedById = storedId.HasValue,
                Provider = KinopoiskConstants.ProviderName,
                ResultLanguage = ResultLanguage
            };

            var staff = await GetOptionalAsync(
                () => _client.GetStaffAsync(id.Value, cancellationToken),
                "people",
                id.Value).ConfigureAwait(false);
            foreach (var person in KinopoiskMapper.BuildPeople(staff, _config.Current.MaxActors))
            {
                result.AddPerson(person);
            }

            return result;
        }
        catch (KinopoiskApiException ex)
        {
            LogApiError(ex, info.Name);
            return new MetadataResult<T>();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RemoteSearchResult>> SearchAsync(ItemLookupInfo info, bool isSeries, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(info);

        try
        {
            var storedId = KinopoiskIdResolver.GetStoredId(info.ProviderIds);
            if (storedId.HasValue)
            {
                var film = await _client.GetFilmAsync(storedId.Value, cancellationToken).ConfigureAwait(false);
                var found = new List<RemoteSearchResult>();
                if (film is not null)
                {
                    found.Add(ToSearchResult(film));
                }

                return found;
            }

            IReadOnlyList<KpCandidate> candidates = [];
            var imdbId = KinopoiskIdResolver.GetImdbId(info.ProviderIds);
            if (imdbId is not null)
            {
                candidates = await _client.FindByImdbIdAsync(imdbId, cancellationToken).ConfigureAwait(false);
            }

            if (candidates.Count == 0)
            {
                var lookup = CleanLookup(info);
                candidates = await _resolver
                    .SearchAsync(lookup.Name, lookup.OriginalTitle, lookup.Year, isSeries, cancellationToken)
                    .ConfigureAwait(false);
            }

            return candidates.Take(MaxSearchResults).Select(c => ToSearchResult(c)).ToList();
        }
        catch (KinopoiskApiException ex)
        {
            LogApiError(ex, info.Name);
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<MetadataResult<Episode>> GetEpisodeMetadataAsync(EpisodeInfo info, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(info);

        // Virtual "missing" episodes would multiply API calls during scans.
        if (info.IsMissingEpisode || info.IndexNumber is not { } episodeNumber)
        {
            return new MetadataResult<Episode>();
        }

        var seriesId = KinopoiskIdResolver.GetStoredId(info.SeriesProviderIds);
        if (seriesId is null)
        {
            return new MetadataResult<Episode>();
        }

        var seasonNumber = info.ParentIndexNumber ?? 1;
        try
        {
            var episodes = await _client.GetEpisodesAsync(seriesId.Value, cancellationToken).ConfigureAwait(false);
            var matched = KinopoiskMapper.FindEpisodes(episodes, seasonNumber, episodeNumber, info.IndexNumberEnd);
            if (matched.Count == 0)
            {
                _logger.LogDebug(
                    "Kinopoisk series {KinopoiskId} has no episode S{Season}E{Episode}",
                    seriesId.Value,
                    seasonNumber,
                    episodeNumber);
                return new MetadataResult<Episode>();
            }

            var item = new Episode();
            KinopoiskMapper.ApplyEpisodes(item, matched);
            if (info.IndexNumberEnd > episodeNumber)
            {
                item.IndexNumber = episodeNumber;
                item.IndexNumberEnd = info.IndexNumberEnd;
            }

            return new MetadataResult<Episode>
            {
                Item = item,
                HasMetadata = true,
                QueriedById = true,
                Provider = KinopoiskConstants.ProviderName,
                ResultLanguage = ResultLanguage
            };
        }
        catch (KinopoiskApiException ex)
        {
            LogApiError(ex, info.Name);
            return new MetadataResult<Episode>();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RemoteImageInfo>> GetImagesAsync(BaseItem item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);

        var id = KinopoiskIdResolver.GetStoredId(item.ProviderIds);
        if (id is null)
        {
            return [];
        }

        try
        {
            var film = await _client.GetFilmAsync(id.Value, cancellationToken).ConfigureAwait(false);
            return film is null ? [] : KinopoiskMapper.BuildImages(film);
        }
        catch (KinopoiskApiException ex)
        {
            LogApiError(ex, item.Name);
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<HttpResponseMessage> GetImageResponseAsync(string url, CancellationToken cancellationToken)
    {
        if (!KinopoiskImageHosts.IsAllowed(url, out var uri))
        {
            _logger.LogWarning("Refusing to download an image outside the Kinopoisk hosts");
            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        }

        return await _httpClientFactory
            .CreateClient(NamedClient.Default)
            .GetAsync(uri, cancellationToken)
            .ConfigureAwait(false);
    }

    private static RemoteSearchResult ToSearchResult(KpCandidate candidate)
    {
        var result = new RemoteSearchResult
        {
            Name = KinopoiskParsing.FirstNonEmpty(candidate.NameRu, candidate.NameEn, candidate.NameOriginal)
                   ?? candidate.Id.ToString(CultureInfo.InvariantCulture),
            ProductionYear = candidate.Year,
            ImageUrl = candidate.PosterUrl,
            SearchProviderName = KinopoiskConstants.ProviderName
        };
        result.SetProviderId(KinopoiskConstants.ProviderIdKey, candidate.Id.ToString(CultureInfo.InvariantCulture));
        return result;
    }

    private static RemoteSearchResult ToSearchResult(KpFilm film)
    {
        var item = new Movie();
        KinopoiskMapper.ApplyFilm(item, film, null);

        var result = new RemoteSearchResult
        {
            Name = item.Name ?? film.KinopoiskId.ToString(CultureInfo.InvariantCulture),
            ProductionYear = item.ProductionYear,
            Overview = item.Overview,
            ImageUrl = film.PosterUrlPreview ?? film.PosterUrl,
            SearchProviderName = KinopoiskConstants.ProviderName
        };
        result.SetProviderId(KinopoiskConstants.ProviderIdKey, film.KinopoiskId.ToString(CultureInfo.InvariantCulture));
        var imdbId = item.GetProviderId(MetadataProvider.Imdb);
        if (imdbId is not null)
        {
            result.SetProviderId(MetadataProvider.Imdb, imdbId);
        }

        return result;
    }

    // File and folder names carry years and release tags ("Matrix.1999.1080p"); search with the parsed title.
    private ItemLookupInfo CleanLookup(ItemLookupInfo info)
    {
        var name = info.Name;
        var year = info.Year;
        if (!string.IsNullOrWhiteSpace(name))
        {
            var parsed = _libraryManager.ParseName(name);
            if (!string.IsNullOrWhiteSpace(parsed?.Name))
            {
                name = parsed.Name;
            }

            year ??= parsed?.Year;
        }

        return new ItemLookupInfo
        {
            Name = name,
            OriginalTitle = info.OriginalTitle,
            Year = year,
            ProviderIds = info.ProviderIds
        };
    }

    private async Task<IReadOnlyList<TItem>> GetOptionalAsync<TItem>(
        Func<Task<IReadOnlyList<TItem>>> fetch,
        string part,
        int id)
    {
        try
        {
            return await fetch().ConfigureAwait(false);
        }
        catch (KinopoiskApiException ex)
        {
            _logger.LogWarning("Kinopoisk {Part} for id {KinopoiskId} are unavailable: {Reason}", part, id, ex.Message);
            return [];
        }
    }

    private void LogApiError(KinopoiskApiException ex, string? name)
    {
        if (ex.Kind == KinopoiskErrorKind.NotConfigured)
        {
            // Every item of a library scan hits this; say it once at warning level.
            if (Interlocked.Exchange(ref _notConfiguredLogged, 1) == 0)
            {
                _logger.LogWarning("Kinopoisk is skipped: the API key is not set in the Kinopoisk Updater plugin settings");
            }

            return;
        }

        Interlocked.Exchange(ref _notConfiguredLogged, 0);
        _logger.LogWarning("Kinopoisk lookup for {Name} failed ({Kind}): {Reason}", name, ex.Kind, ex.Message);
    }
}
