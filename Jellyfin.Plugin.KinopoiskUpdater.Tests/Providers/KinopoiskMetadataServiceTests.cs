using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.KinopoiskUpdater.Configuration;
using Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk;
using Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk.Models;
using Jellyfin.Plugin.KinopoiskUpdater.Metadata;
using Jellyfin.Plugin.KinopoiskUpdater.Providers;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.KinopoiskUpdater.Tests.Providers;

public class KinopoiskMetadataServiceTests
{
    private readonly Mock<IKinopoiskClient> _client = new();
    private readonly Mock<ILibraryManager> _libraryManager = new();
    private readonly Mock<IHttpClientFactory> _httpClientFactory = new(MockBehavior.Strict);
    private readonly PluginConfiguration _config = new() { ApiKey = "key", MaxActors = 1 };
    private readonly KinopoiskMetadataService _service;

    public KinopoiskMetadataServiceTests()
    {
        _libraryManager.Setup(l => l.ParseName(It.IsAny<string>()))
            .Returns((string name) => new ItemLookupInfo { Name = name.Replace(".1999.1080p", string.Empty, StringComparison.Ordinal), Year = name.Contains("1999", StringComparison.Ordinal) ? 1999 : null });
        _client.Setup(c => c.GetDistributionsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<KpDistribution>());
        _client.Setup(c => c.GetStaffAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<KpStaff>());

        var accessor = new Mock<IPluginConfigurationAccessor>();
        accessor.Setup(a => a.Current).Returns(_config);

        _service = new KinopoiskMetadataService(
            _client.Object,
            new KinopoiskIdResolver(_client.Object),
            accessor.Object,
            _libraryManager.Object,
            _httpClientFactory.Object,
            NullLogger<KinopoiskMetadataService>.Instance);
    }

    private static KpFilm Matrix() => new()
    {
        KinopoiskId = 301,
        ImdbId = "tt0133093",
        NameRu = "Матрица",
        NameOriginal = "The Matrix",
        Year = 1999,
        Type = "FILM",
        PosterUrl = "https://kinopoiskapiunofficial.tech/images/posters/kp/301.jpg",
        PosterUrlPreview = "https://kinopoiskapiunofficial.tech/images/posters/kp_small/301.jpg"
    };

    private static MovieInfo WithKinopoiskId(string id) => new() { ProviderIds = new Dictionary<string, string> { ["KinoPoisk"] = id } };

    [Fact]
    public async Task GetMetadataAsync_StoredId_MapsFilmPremiereAndPeople()
    {
        _client.Setup(c => c.GetFilmAsync(301, It.IsAny<CancellationToken>())).ReturnsAsync(Matrix());
        _client.Setup(c => c.GetDistributionsAsync(301, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new KpDistribution { Type = "WORLD_PREMIER", Date = "1999-03-24" } });
        _client.Setup(c => c.GetStaffAsync(301, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new KpStaff { StaffId = 1, NameRu = "Киану Ривз", ProfessionKey = "ACTOR", Description = "Neo" },
                new KpStaff { StaffId = 2, NameRu = "Лоренс Фишбёрн", ProfessionKey = "ACTOR" },
                new KpStaff { StaffId = 3, NameRu = "Лана Вачовски", ProfessionKey = "DIRECTOR" }
            });

        var result = await _service.GetMetadataAsync<Movie>(WithKinopoiskId("301"), isSeries: false, CancellationToken.None);

        Assert.True(result.HasMetadata);
        Assert.True(result.QueriedById);
        Assert.Equal(KinopoiskConstants.ProviderName, result.Provider);
        Assert.Equal("Матрица", result.Item.Name);
        Assert.Equal(new DateTime(1999, 3, 24, 0, 0, 0, DateTimeKind.Utc), result.Item.PremiereDate);
        Assert.Equal("301", result.Item.GetProviderId(KinopoiskConstants.ProviderIdKey));
        Assert.Equal(new[] { "Киану Ривз", "Лана Вачовски" }, result.People.Select(p => p.Name));
        Assert.Equal(PersonKind.Director, result.People[1].Type);
    }

    [Fact]
    public async Task GetMetadataAsync_WithoutIds_SearchesParsedFileName()
    {
        _client.Setup(c => c.SearchByKeywordAsync("Matrix", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new KpCandidate { Id = 301, NameEn = "Matrix", Type = "FILM", Year = 1999 } });
        _client.Setup(c => c.GetFilmAsync(301, It.IsAny<CancellationToken>())).ReturnsAsync(Matrix());

        var result = await _service.GetMetadataAsync<Movie>(new MovieInfo { Name = "Matrix.1999.1080p" }, isSeries: false, CancellationToken.None);

        Assert.True(result.HasMetadata);
        Assert.False(result.QueriedById);
        Assert.Equal(1999, result.Item.ProductionYear);
    }

    [Fact]
    public async Task GetMetadataAsync_NoConfidentMatch_ReturnsNoMetadata()
    {
        _client.Setup(c => c.SearchByKeywordAsync("Matrix", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new KpCandidate { Id = 1, NameEn = "Matrix", Type = "TV_SERIES", Year = 1999 } });

        var result = await _service.GetMetadataAsync<Movie>(new MovieInfo { Name = "Matrix", Year = 1999 }, isSeries: false, CancellationToken.None);

        Assert.False(result.HasMetadata);
        _client.Verify(c => c.GetFilmAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetMetadataAsync_ApiError_ReturnsNoMetadataInsteadOfThrowing()
    {
        _client.Setup(c => c.GetFilmAsync(301, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KinopoiskApiException("quota", KinopoiskErrorKind.QuotaExceeded, 402));

        var result = await _service.GetMetadataAsync<Movie>(WithKinopoiskId("301"), isSeries: false, CancellationToken.None);

        Assert.False(result.HasMetadata);
    }

    [Fact]
    public async Task GetMetadataAsync_StaffFails_KeepsFilmData()
    {
        _client.Setup(c => c.GetFilmAsync(301, It.IsAny<CancellationToken>())).ReturnsAsync(Matrix());
        _client.Setup(c => c.GetStaffAsync(301, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KinopoiskApiException("down", KinopoiskErrorKind.Unavailable, 503));

        var result = await _service.GetMetadataAsync<Movie>(WithKinopoiskId("301"), isSeries: false, CancellationToken.None);

        Assert.True(result.HasMetadata);
        Assert.Empty(result.People ?? []);
    }

    [Fact]
    public async Task GetMetadataAsync_Series_SetsStatus()
    {
        var film = Matrix();
        film.Type = "TV_SERIES";
        film.Completed = false;
        _client.Setup(c => c.GetFilmAsync(77, It.IsAny<CancellationToken>())).ReturnsAsync(film);
        var info = new SeriesInfo { ProviderIds = new Dictionary<string, string> { ["KinoPoisk"] = "77" } };

        var result = await _service.GetMetadataAsync<Series>(info, isSeries: true, CancellationToken.None);

        Assert.Equal(SeriesStatus.Continuing, result.Item.Status);
    }

    [Fact]
    public async Task SearchAsync_StoredId_ReturnsCardAsSingleResult()
    {
        _client.Setup(c => c.GetFilmAsync(301, It.IsAny<CancellationToken>())).ReturnsAsync(Matrix());

        var results = await _service.SearchAsync(WithKinopoiskId("301"), isSeries: false, CancellationToken.None);

        var result = Assert.Single(results);
        Assert.Equal("Матрица", result.Name);
        Assert.Equal(1999, result.ProductionYear);
        Assert.Equal("https://kinopoiskapiunofficial.tech/images/posters/kp_small/301.jpg", result.ImageUrl);
        Assert.Equal("301", result.ProviderIds[KinopoiskConstants.ProviderIdKey]);
        Assert.Equal("tt0133093", result.ProviderIds["Imdb"]);
        Assert.Equal(KinopoiskConstants.ProviderName, result.SearchProviderName);
    }

    [Fact]
    public async Task SearchAsync_ByTitle_ReturnsRankedCandidates()
    {
        _client.Setup(c => c.SearchByKeywordAsync("Матрица", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new KpCandidate { Id = 10, NameRu = "Матрица: Воскрешение", Type = "FILM", Year = 2021 },
                new KpCandidate { Id = 301, NameRu = "Матрица", Type = "FILM", Year = 1999 }
            });

        var results = await _service.SearchAsync(new MovieInfo { Name = "Матрица", Year = 1999 }, isSeries: false, CancellationToken.None);

        Assert.Equal(new[] { "301", "10" }, results.Select(r => r.ProviderIds[KinopoiskConstants.ProviderIdKey]));
    }

    [Fact]
    public async Task SearchAsync_ApiError_ReturnsEmpty()
    {
        _client.Setup(c => c.SearchByKeywordAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KinopoiskApiException("no key", KinopoiskErrorKind.NotConfigured));

        Assert.Empty(await _service.SearchAsync(new MovieInfo { Name = "Матрица" }, isSeries: false, CancellationToken.None));
    }

    [Fact]
    public async Task GetEpisodeMetadataAsync_UsesSeriesIdAndNumbers()
    {
        _client.Setup(c => c.GetEpisodesAsync(77, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new KpEpisode { SeasonNumber = 1, EpisodeNumber = 9, NameRu = "Девятая" },
                new KpEpisode { SeasonNumber = 1, EpisodeNumber = 10, NameRu = "Десятая" }
            });
        var info = new EpisodeInfo
        {
            ParentIndexNumber = 1,
            IndexNumber = 10,
            SeriesProviderIds = new Dictionary<string, string> { ["KinoPoisk"] = "77" }
        };

        var result = await _service.GetEpisodeMetadataAsync(info, CancellationToken.None);

        Assert.True(result.HasMetadata);
        Assert.Equal("Десятая", result.Item.Name);
    }

    [Fact]
    public async Task GetEpisodeMetadataAsync_MultiEpisodeFile_JoinsNames()
    {
        _client.Setup(c => c.GetEpisodesAsync(77, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new KpEpisode { SeasonNumber = 1, EpisodeNumber = 9, NameRu = "Девятая" },
                new KpEpisode { SeasonNumber = 1, EpisodeNumber = 10, NameRu = "Десятая" }
            });
        var info = new EpisodeInfo
        {
            ParentIndexNumber = 1,
            IndexNumber = 9,
            IndexNumberEnd = 10,
            SeriesProviderIds = new Dictionary<string, string> { ["KinoPoisk"] = "77" }
        };

        var result = await _service.GetEpisodeMetadataAsync(info, CancellationToken.None);

        Assert.Equal("Девятая / Десятая", result.Item.Name);
        Assert.Equal(9, result.Item.IndexNumber);
        Assert.Equal(10, result.Item.IndexNumberEnd);
    }

    [Fact]
    public async Task GetEpisodeMetadataAsync_SeasonDefaultsToOne()
    {
        _client.Setup(c => c.GetEpisodesAsync(77, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new KpEpisode { SeasonNumber = 1, EpisodeNumber = 2, NameRu = "Вторая" } });
        var info = new EpisodeInfo { IndexNumber = 2, SeriesProviderIds = new Dictionary<string, string> { ["KinoPoisk"] = "77" } };

        var result = await _service.GetEpisodeMetadataAsync(info, CancellationToken.None);

        Assert.Equal("Вторая", result.Item.Name);
    }

    [Theory]
    [InlineData(true, 1, "77")]
    [InlineData(false, null, "77")]
    [InlineData(false, 1, null)]
    public async Task GetEpisodeMetadataAsync_MissingPrerequisites_MakesNoRequest(bool isMissing, int? episodeNumber, string? seriesId)
    {
        var info = new EpisodeInfo { IsMissingEpisode = isMissing, ParentIndexNumber = 1, IndexNumber = episodeNumber };
        if (seriesId is not null)
        {
            info.SeriesProviderIds["KinoPoisk"] = seriesId;
        }

        var result = await _service.GetEpisodeMetadataAsync(info, CancellationToken.None);

        Assert.False(result.HasMetadata);
        _client.Verify(c => c.GetEpisodesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetEpisodeMetadataAsync_NoPair_ReturnsNoMetadata()
    {
        _client.Setup(c => c.GetEpisodesAsync(77, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new KpEpisode { SeasonNumber = 1, EpisodeNumber = 1 } });
        var info = new EpisodeInfo
        {
            ParentIndexNumber = 2,
            IndexNumber = 1,
            SeriesProviderIds = new Dictionary<string, string> { ["KinoPoisk"] = "77" }
        };

        Assert.False((await _service.GetEpisodeMetadataAsync(info, CancellationToken.None)).HasMetadata);
    }

    [Fact]
    public async Task GetImagesAsync_ReturnsPosterForStoredId()
    {
        _client.Setup(c => c.GetFilmAsync(301, It.IsAny<CancellationToken>())).ReturnsAsync(Matrix());
        var movie = new Movie();
        movie.SetProviderId(KinopoiskConstants.ProviderIdKey, "301");

        var images = await _service.GetImagesAsync(movie, CancellationToken.None);

        var poster = Assert.Single(images);
        Assert.Equal(ImageType.Primary, poster.Type);
        Assert.Equal("https://kinopoiskapiunofficial.tech/images/posters/kp/301.jpg", poster.Url);
    }

    [Fact]
    public async Task GetImagesAsync_WithoutId_MakesNoRequest()
    {
        var images = await _service.GetImagesAsync(new Movie(), CancellationToken.None);

        Assert.Empty(images);
        _client.Verify(c => c.GetFilmAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("http://192.168.1.1/admin")]
    [InlineData("https://evil.example/kinopoisk.ru/x.jpg")]
    [InlineData("file:///etc/passwd")]
    public async Task GetImageResponseAsync_ForeignHost_IsRefusedWithoutRequest(string url)
    {
        using var response = await _service.GetImageResponseAsync(url, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        _httpClientFactory.Verify(f => f.CreateClient(It.IsAny<string>()), Times.Never);
    }
}
