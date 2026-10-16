using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.KinopoiskUpdater.Configuration;
using Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.KinopoiskUpdater.Tests.Kinopoisk;

public sealed class KinopoiskClientTests : IDisposable
{
    private readonly StubHandler _handler = new();
    private readonly PluginConfiguration _config = new() { ApiKey = "test-key" };
    private readonly KinopoiskClient _client;

    public KinopoiskClientTests()
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(KinopoiskConstants.HttpClientName))
            .Returns(() => new HttpClient(_handler, disposeHandler: false));
        var accessor = new Mock<IPluginConfigurationAccessor>();
        accessor.Setup(a => a.Current).Returns(() => _config);

        _client = new KinopoiskClient(
            factory.Object,
            accessor.Object,
            new KinopoiskResponseCache(TimeProvider.System, TimeSpan.FromMinutes(30)),
            NullLogger<KinopoiskClient>.Instance)
        {
            RetryDelayUnit = TimeSpan.Zero
        };
    }

    public void Dispose()
    {
        _client.Dispose();
        _handler.Dispose();
    }

    [Fact]
    public async Task GetFilmAsync_ParsesCardAndSendsKey()
    {
        _handler.Enqueue(HttpStatusCode.OK, """
            {
              "kinopoiskId": 301, "imdbId": "tt0133093", "nameRu": "Матрица", "nameOriginal": "The Matrix",
              "ratingKinopoisk": 8.5, "year": 1999, "type": "FILM", "ratingAgeLimits": "age16",
              "genres": [{ "genre": "фантастика" }], "countries": [{ "country": "США" }],
              "unknownField": { "nested": true }
            }
            """);

        var film = await _client.GetFilmAsync(301, CancellationToken.None);

        Assert.NotNull(film);
        Assert.Equal("Матрица", film.NameRu);
        Assert.Equal(8.5, film.RatingKinopoisk);
        Assert.Equal("фантастика", film.Genres![0].Genre);
        var request = Assert.Single(_handler.Requests);
        Assert.Equal("https://kinopoiskapiunofficial.tech/api/v2.2/films/301", request.Uri.ToString());
        Assert.Equal("test-key", request.ApiKey);
    }

    [Fact]
    public async Task GetFilmAsync_NotFound_ReturnsNull()
    {
        _handler.Enqueue(HttpStatusCode.NotFound, "{}");

        Assert.Null(await _client.GetFilmAsync(1, CancellationToken.None));
    }

    [Fact]
    public async Task GetFilmAsync_IsCached()
    {
        _handler.Enqueue(HttpStatusCode.OK, """{ "kinopoiskId": 7 }""");

        await _client.GetFilmAsync(7, CancellationToken.None);
        var second = await _client.GetFilmAsync(7, CancellationToken.None);

        Assert.Equal(7, second!.KinopoiskId);
        Assert.Single(_handler.Requests);
    }

    [Fact]
    public async Task Request_RetriesOnTooManyRequestsAndServerErrors()
    {
        _handler.Enqueue(HttpStatusCode.TooManyRequests, string.Empty);
        _handler.Enqueue(HttpStatusCode.BadGateway, string.Empty);
        _handler.Enqueue(HttpStatusCode.OK, """{ "kinopoiskId": 9 }""");

        var film = await _client.GetFilmAsync(9, CancellationToken.None);

        Assert.Equal(9, film!.KinopoiskId);
        Assert.Equal(3, _handler.Requests.Count);
    }

    [Fact]
    public async Task Request_GivesUpAfterMaxAttempts()
    {
        for (var i = 0; i < KinopoiskClient.MaxAttempts; i++)
        {
            _handler.Enqueue(HttpStatusCode.ServiceUnavailable, string.Empty);
        }

        var error = await Assert.ThrowsAsync<KinopoiskApiException>(() => _client.GetFilmAsync(9, CancellationToken.None));

        Assert.Equal(KinopoiskErrorKind.Unavailable, error.Kind);
        Assert.Equal(503, error.StatusCode);
        Assert.Equal(KinopoiskClient.MaxAttempts, _handler.Requests.Count);
    }

    [Fact]
    public async Task Request_NetworkError_IsRetried()
    {
        _handler.EnqueueException(new HttpRequestException("connection reset"));
        _handler.Enqueue(HttpStatusCode.OK, """{ "kinopoiskId": 3 }""");

        var film = await _client.GetFilmAsync(3, CancellationToken.None);

        Assert.Equal(3, film!.KinopoiskId);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, KinopoiskErrorKind.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, KinopoiskErrorKind.Unauthorized)]
    [InlineData(HttpStatusCode.PaymentRequired, KinopoiskErrorKind.QuotaExceeded)]
    [InlineData(HttpStatusCode.BadRequest, KinopoiskErrorKind.Unexpected)]
    public async Task Request_NonRetryableStatus_ThrowsImmediately(HttpStatusCode status, KinopoiskErrorKind kind)
    {
        _handler.Enqueue(status, string.Empty);

        var error = await Assert.ThrowsAsync<KinopoiskApiException>(() => _client.GetFilmAsync(5, CancellationToken.None));

        Assert.Equal(kind, error.Kind);
        Assert.Single(_handler.Requests);
        Assert.DoesNotContain("test-key", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Request_FailureIsNotCached()
    {
        _handler.Enqueue(HttpStatusCode.Unauthorized, string.Empty);
        _handler.Enqueue(HttpStatusCode.OK, """{ "kinopoiskId": 5 }""");

        await Assert.ThrowsAsync<KinopoiskApiException>(() => _client.GetFilmAsync(5, CancellationToken.None));
        var film = await _client.GetFilmAsync(5, CancellationToken.None);

        Assert.Equal(5, film!.KinopoiskId);
    }

    [Fact]
    public async Task Request_WithoutApiKey_ThrowsNotConfigured()
    {
        _config.ApiKey = string.Empty;

        var error = await Assert.ThrowsAsync<KinopoiskApiException>(() => _client.GetFilmAsync(5, CancellationToken.None));

        Assert.Equal(KinopoiskErrorKind.NotConfigured, error.Kind);
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task Request_InvalidJson_ThrowsUnexpected()
    {
        _handler.Enqueue(HttpStatusCode.OK, "<html>maintenance</html>");

        var error = await Assert.ThrowsAsync<KinopoiskApiException>(() => _client.GetFilmAsync(5, CancellationToken.None));

        Assert.Equal(KinopoiskErrorKind.Unexpected, error.Kind);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetFilmAsync_InvalidId_Throws(int id)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _client.GetFilmAsync(id, CancellationToken.None));
    }

    [Fact]
    public async Task SearchByKeywordAsync_EscapesKeywordAndParsesStringYears()
    {
        _handler.Enqueue(HttpStatusCode.OK, """
            {
              "films": [
                { "filmId": 1, "nameRu": "Шерлок", "type": "TV_SERIES", "year": "2010-2017" },
                { "filmId": 2, "nameRu": "Шерлок Холмс", "type": "FILM", "year": 2009 },
                { "filmId": 3, "nameRu": "Без года", "type": "FILM", "year": "null" },
                { "filmId": 0, "nameRu": "Мусор" }
              ]
            }
            """);

        var results = await _client.SearchByKeywordAsync(" Шерлок & Co ", CancellationToken.None);

        Assert.Equal(new[] { 1, 2, 3 }, results.Select(r => r.Id));
        Assert.Equal(new int?[] { 2010, 2009, null }, results.Select(r => r.Year));
        var query = Assert.Single(_handler.Requests).Uri.Query;
        Assert.Contains("keyword=%D0%A8%D0%B5%D1%80%D0%BB%D0%BE%D0%BA%20%26%20Co", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchByKeywordAsync_Blank_MakesNoRequest()
    {
        var results = await _client.SearchByKeywordAsync("   ", CancellationToken.None);

        Assert.Empty(results);
        Assert.Empty(_handler.Requests);
    }

    [Theory]
    [InlineData("tt12")]
    [InlineData("tt0133093&x=1")]
    [InlineData("0133093")]
    public async Task FindByImdbIdAsync_InvalidId_MakesNoRequest(string imdbId)
    {
        var results = await _client.FindByImdbIdAsync(imdbId, CancellationToken.None);

        Assert.Empty(results);
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task FindByImdbIdAsync_ParsesItems()
    {
        _handler.Enqueue(HttpStatusCode.OK, """{ "items": [{ "kinopoiskId": 301, "nameRu": "Матрица", "year": 1999, "type": "FILM" }] }""");

        var results = await _client.FindByImdbIdAsync("tt0133093", CancellationToken.None);

        Assert.Equal(301, Assert.Single(results).Id);
        Assert.EndsWith("api/v2.2/films?imdbId=tt0133093", _handler.Requests[0].Uri.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetEpisodesAsync_FlattensSeasonsAndFillsSeasonNumber()
    {
        _handler.Enqueue(HttpStatusCode.OK, """
            {
              "items": [
                { "number": 1, "episodes": [
                  { "seasonNumber": 1, "episodeNumber": 1, "nameRu": "Пилот", "releaseDate": "2008-01-20" },
                  { "episodeNumber": 2, "nameRu": "Вторая" } ] },
                { "number": 2, "episodes": [ { "seasonNumber": 2, "episodeNumber": 1 } ] }
              ]
            }
            """);

        var episodes = await _client.GetEpisodesAsync(404900, CancellationToken.None);

        Assert.Equal(3, episodes.Count);
        Assert.Equal(1, episodes[1].SeasonNumber);
        Assert.Equal(2, episodes[2].SeasonNumber);
    }

    [Fact]
    public async Task GetStaffAsync_ParsesArray()
    {
        _handler.Enqueue(HttpStatusCode.OK, """[{ "staffId": 7, "nameRu": "Киану Ривз", "professionKey": "ACTOR", "description": "Neo" }]""");

        var staff = await _client.GetStaffAsync(301, CancellationToken.None);

        Assert.Equal("Neo", Assert.Single(staff).Description);
        Assert.EndsWith("api/v1/staff?filmId=301", _handler.Requests[0].Uri.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetDistributionsAsync_ParsesItems()
    {
        _handler.Enqueue(HttpStatusCode.OK, """{ "items": [{ "type": "WORLD_PREMIER", "date": "1999-03-24" }] }""");

        var items = await _client.GetDistributionsAsync(301, CancellationToken.None);

        Assert.Equal("1999-03-24", Assert.Single(items).Date);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _responses = new();

        public List<(Uri Uri, string? ApiKey)> Requests { get; } = [];

        public void Enqueue(HttpStatusCode status, string body)
            => _responses.Enqueue(() => new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });

        public void EnqueueException(Exception exception)
            => _responses.Enqueue(() => throw exception);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var apiKey = request.Headers.TryGetValues("X-API-KEY", out var values) ? values.FirstOrDefault() : null;
            Requests.Add((request.RequestUri!, apiKey));
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("Unexpected request: " + request.RequestUri);
            }

            return Task.FromResult(_responses.Dequeue()());
        }
    }
}
