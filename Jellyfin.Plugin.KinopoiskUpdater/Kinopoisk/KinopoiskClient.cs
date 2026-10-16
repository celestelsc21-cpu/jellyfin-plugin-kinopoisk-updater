using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.KinopoiskUpdater.Configuration;
using Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk;

/// <summary>
/// kinopoiskapiunofficial.tech client with retries, a concurrency limit and response caching.
/// </summary>
internal sealed partial class KinopoiskClient : IKinopoiskClient, IDisposable
{
    /// <summary>
    /// Attempts per request (first try plus retries) for 429, 5xx, network errors and timeouts.
    /// </summary>
    internal const int MaxAttempts = 3;

    /// <summary>
    /// Maximum keyword length sent to the search endpoint.
    /// </summary>
    internal const int MaxKeywordLength = 200;

    // The free tier allows about 20 requests per second; stay well below it.
    private const int MaxConcurrentRequests = 4;

    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan TotalTimeout = TimeSpan.FromSeconds(90);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IPluginConfigurationAccessor _config;
    private readonly KinopoiskResponseCache _cache;
    private readonly ILogger<KinopoiskClient> _logger;
    private readonly SemaphoreSlim _concurrency = new(MaxConcurrentRequests, MaxConcurrentRequests);

    /// <summary>
    /// Initializes a new instance of the <see cref="KinopoiskClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="config">Plugin configuration.</param>
    /// <param name="cache">Response cache.</param>
    /// <param name="logger">Logger.</param>
    public KinopoiskClient(
        IHttpClientFactory httpClientFactory,
        IPluginConfigurationAccessor config,
        KinopoiskResponseCache cache,
        ILogger<KinopoiskClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Gets or sets the base retry delay (multiplied by the attempt number). Tests set it to zero.
    /// </summary>
    internal TimeSpan RetryDelayUnit { get; set; } = TimeSpan.FromSeconds(1);

    /// <inheritdoc />
    public Task<KpFilm?> GetFilmAsync(int id, CancellationToken cancellationToken)
    {
        ValidateId(id);
        return GetJsonAsync<KpFilm>(FormattableString.Invariant($"api/v2.2/films/{id}"), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<KpCandidate>> FindByImdbIdAsync(string imdbId, CancellationToken cancellationToken)
    {
        var normalized = imdbId?.Trim() ?? string.Empty;
        if (!ImdbIdRegex().IsMatch(normalized))
        {
            return [];
        }

        var response = await GetJsonAsync<KpFilmListResponse>(
            "api/v2.2/films?imdbId=" + Uri.EscapeDataString(normalized),
            cancellationToken).ConfigureAwait(false);

        return (response?.Items ?? [])
            .Where(i => i.KinopoiskId > 0)
            .Select(i => new KpCandidate
            {
                Id = i.KinopoiskId,
                NameRu = i.NameRu,
                NameEn = i.NameEn,
                NameOriginal = i.NameOriginal,
                Year = i.Year,
                Type = i.Type,
                PosterUrl = i.PosterUrlPreview
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<KpCandidate>> SearchByKeywordAsync(string keyword, CancellationToken cancellationToken)
    {
        var normalized = (keyword ?? string.Empty).Trim();
        if (normalized.Length == 0)
        {
            return [];
        }

        if (normalized.Length > MaxKeywordLength)
        {
            normalized = normalized[..MaxKeywordLength];
        }

        var response = await GetJsonAsync<KpKeywordSearchResponse>(
            "api/v2.1/films/search-by-keyword?page=1&keyword=" + Uri.EscapeDataString(normalized),
            cancellationToken).ConfigureAwait(false);

        return (response?.Films ?? [])
            .Where(f => f.FilmId > 0)
            .Select(f => new KpCandidate
            {
                Id = f.FilmId,
                NameRu = f.NameRu,
                NameEn = f.NameEn,
                Year = KinopoiskParsing.ParseYear(f.Year),
                Type = f.Type,
                PosterUrl = f.PosterUrlPreview
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<KpEpisode>> GetEpisodesAsync(int seriesId, CancellationToken cancellationToken)
    {
        ValidateId(seriesId);
        var response = await GetJsonAsync<KpSeasonsResponse>(
            FormattableString.Invariant($"api/v2.2/films/{seriesId}/seasons"),
            cancellationToken).ConfigureAwait(false);

        var episodes = new List<KpEpisode>();
        foreach (var season in response?.Items ?? [])
        {
            foreach (var episode in season.Episodes ?? [])
            {
                episode.SeasonNumber ??= season.Number;
                episodes.Add(episode);
            }
        }

        return episodes;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<KpDistribution>> GetDistributionsAsync(int id, CancellationToken cancellationToken)
    {
        ValidateId(id);
        var response = await GetJsonAsync<KpDistributionsResponse>(
            FormattableString.Invariant($"api/v2.2/films/{id}/distributions"),
            cancellationToken).ConfigureAwait(false);
        return response?.Items ?? [];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<KpStaff>> GetStaffAsync(int id, CancellationToken cancellationToken)
    {
        ValidateId(id);
        var staff = await GetJsonAsync<List<KpStaff>>(
            FormattableString.Invariant($"api/v1/staff?filmId={id}"),
            cancellationToken).ConfigureAwait(false);
        return staff ?? [];
    }

    /// <inheritdoc />
    public void Dispose() => _concurrency.Dispose();

    private static void ValidateId(int id)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
    }

    private async Task<T?> GetJsonAsync<T>(string relativeUrl, CancellationToken cancellationToken)
        where T : class
    {
        var json = await _cache.GetOrAddAsync(relativeUrl, () => FetchAsync(relativeUrl), cancellationToken).ConfigureAwait(false);
        if (json is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new KinopoiskApiException("Kinopoisk returned a response in an unexpected format.", KinopoiskErrorKind.Unexpected, null, ex);
        }
    }

    // Runs detached from any caller token (the result is shared through the cache);
    // bounded by the HTTP client timeout per attempt and by TotalTimeout overall.
    private async Task<string?> FetchAsync(string relativeUrl)
    {
        var apiKey = _config.Current.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new KinopoiskApiException("Kinopoisk API key is not configured in the plugin settings.", KinopoiskErrorKind.NotConfigured);
        }

        using var timeout = new CancellationTokenSource(TotalTimeout);
        var cancellationToken = timeout.Token;
        var path = PathForLog(relativeUrl);

        for (var attempt = 1; ; attempt++)
        {
            var result = await SendOnceAsync(relativeUrl, apiKey, cancellationToken).ConfigureAwait(false);
            var retryable = result.Error is not null
                || result.Status == HttpStatusCode.TooManyRequests
                || (int)result.Status >= 500;

            if (result.Error is null && result.Status == HttpStatusCode.OK)
            {
                return result.Body;
            }

            if (result.Error is null && result.Status == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!retryable)
            {
                throw CreateError(result.Status, path);
            }

            if (attempt >= MaxAttempts)
            {
                if (result.Error is not null)
                {
                    throw new KinopoiskApiException(
                        "Kinopoisk is unavailable (network error or timeout).",
                        KinopoiskErrorKind.Unavailable,
                        null,
                        result.Error);
                }

                throw CreateError(result.Status, path);
            }

            var delay = result.RetryAfter ?? (RetryDelayUnit * attempt);
            if (delay > MaxRetryDelay)
            {
                delay = MaxRetryDelay;
            }

            _logger.LogWarning(
                "Kinopoisk request {Path} failed ({Reason}), retry {Attempt} of {MaxRetries} in {Delay} ms",
                path,
                result.Error?.GetType().Name ?? ((int)result.Status).ToString(CultureInfo.InvariantCulture),
                attempt,
                MaxAttempts - 1,
                (int)delay.TotalMilliseconds);

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<SendResult> SendOnceAsync(string relativeUrl, string apiKey, CancellationToken cancellationToken)
    {
        await _concurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(KinopoiskConstants.ApiBaseUri, relativeUrl));
            request.Headers.TryAddWithoutValidation("X-API-KEY", apiKey);
            request.Headers.Accept.ParseAdd("application/json");

            var client = _httpClientFactory.CreateClient(KinopoiskConstants.HttpClientName);
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = response.StatusCode == HttpStatusCode.OK
                ? await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)
                : null;
            return new SendResult(response.StatusCode, body, GetRetryAfter(response), null);
        }
        catch (HttpRequestException ex)
        {
            return new SendResult(default, null, null, ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient.Timeout elapsed for this attempt.
            return new SendResult(default, null, null, ex);
        }
        finally
        {
            _concurrency.Release();
        }
    }

    private static TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta)
        {
            return delta;
        }

        if (retryAfter?.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
        }

        return null;
    }

    private static KinopoiskApiException CreateError(HttpStatusCode status, string path)
    {
        var code = (int)status;
        return status switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new KinopoiskApiException(
                "Kinopoisk rejected the API key. Check it in the plugin settings.",
                KinopoiskErrorKind.Unauthorized,
                code),
            HttpStatusCode.PaymentRequired => new KinopoiskApiException(
                "The daily request quota of the Kinopoisk API key is exhausted.",
                KinopoiskErrorKind.QuotaExceeded,
                code),
            HttpStatusCode.TooManyRequests => new KinopoiskApiException(
                "Kinopoisk keeps answering 'too many requests'.",
                KinopoiskErrorKind.RateLimited,
                code),
            _ when code >= 500 => new KinopoiskApiException(
                string.Create(CultureInfo.InvariantCulture, $"Kinopoisk server error {code} for {path}."),
                KinopoiskErrorKind.Unavailable,
                code),
            _ => new KinopoiskApiException(
                string.Create(CultureInfo.InvariantCulture, $"Kinopoisk answered {code} for {path}."),
                KinopoiskErrorKind.Unexpected,
                code)
        };
    }

    // Logs only the endpoint path: query strings may carry user-entered titles.
    private static string PathForLog(string relativeUrl)
    {
        var query = relativeUrl.IndexOf('?', StringComparison.Ordinal);
        return query < 0 ? relativeUrl : relativeUrl[..query];
    }

    [GeneratedRegex("^tt[0-9]{5,10}$", RegexOptions.CultureInvariant)]
    private static partial Regex ImdbIdRegex();

    private sealed class SendResult
    {
        public SendResult(HttpStatusCode status, string? body, TimeSpan? retryAfter, Exception? error)
        {
            Status = status;
            Body = body;
            RetryAfter = retryAfter;
            Error = error;
        }

        public HttpStatusCode Status { get; }

        public string? Body { get; }

        public TimeSpan? RetryAfter { get; }

        public Exception? Error { get; }
    }
}
