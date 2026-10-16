using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk;
using Jellyfin.Plugin.KinopoiskUpdater.Metadata;
using MediaBrowser.Controller.Providers;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.KinopoiskUpdater.Tests.Metadata;

public class KinopoiskIdResolverTests
{
    private readonly Mock<IKinopoiskClient> _client = new(MockBehavior.Strict);

    private static IReadOnlyList<KpCandidate> L(params KpCandidate[] candidates) => candidates;

    [Theory]
    [InlineData("KinoPoisk", "301", 301)]
    [InlineData("kinopoisk", " 42 ", 42)]
    [InlineData("KinoPoisk", "abc", null)]
    [InlineData("KinoPoisk", "-5", null)]
    [InlineData("Tmdb", "603", null)]
    public void GetStoredId_ReadsKinopoiskKey(string key, string value, int? expected)
    {
        var ids = new Dictionary<string, string> { [key] = value };

        Assert.Equal(expected, KinopoiskIdResolver.GetStoredId(ids));
    }

    [Fact]
    public async Task ResolveAsync_StoredId_MakesNoRequests()
    {
        var resolver = new KinopoiskIdResolver(_client.Object);
        var info = new MovieInfo { ProviderIds = new Dictionary<string, string> { ["KinoPoisk"] = "301" } };

        Assert.Equal(301, await resolver.ResolveAsync(info, isSeries: false, CancellationToken.None));
    }

    [Fact]
    public async Task ResolveAsync_ImdbId_UsesImdbLookup()
    {
        _client.Setup(c => c.FindByImdbIdAsync("tt0133093", It.IsAny<CancellationToken>()))
            .ReturnsAsync(L(new KpCandidate { Id = 301, Type = "FILM", Year = 1999 }));
        var resolver = new KinopoiskIdResolver(_client.Object);
        var info = new MovieInfo { Name = "Матрица", ProviderIds = new Dictionary<string, string> { ["Imdb"] = "tt0133093" } };

        Assert.Equal(301, await resolver.ResolveAsync(info, isSeries: false, CancellationToken.None));
    }

    [Fact]
    public async Task ResolveAsync_ImdbNotFound_FallsBackToTitleSearch()
    {
        _client.Setup(c => c.FindByImdbIdAsync("tt0133093", It.IsAny<CancellationToken>())).ReturnsAsync(L());
        _client.Setup(c => c.SearchByKeywordAsync("Матрица", It.IsAny<CancellationToken>()))
            .ReturnsAsync(L(new KpCandidate { Id = 301, NameRu = "Матрица", Type = "FILM", Year = 1999 }));
        var resolver = new KinopoiskIdResolver(_client.Object);
        var info = new MovieInfo
        {
            Name = "Матрица",
            Year = 1999,
            ProviderIds = new Dictionary<string, string> { ["Imdb"] = "tt0133093" }
        };

        Assert.Equal(301, await resolver.ResolveAsync(info, isSeries: false, CancellationToken.None));
    }

    [Fact]
    public async Task ResolveAsync_TitleSearch_PicksBestConfidentCandidate()
    {
        _client.Setup(c => c.SearchByKeywordAsync("Шерлок", It.IsAny<CancellationToken>()))
            .ReturnsAsync(L(
                new KpCandidate { Id = 1, NameRu = "Шерлок Холмс", Type = "FILM", Year = 2009 },
                new KpCandidate { Id = 2, NameRu = "Шерлок", Type = "TV_SERIES", Year = 2010 }));
        _client.Setup(c => c.SearchByKeywordAsync("Sherlock", It.IsAny<CancellationToken>()))
            .ReturnsAsync(L(new KpCandidate { Id = 2, NameRu = "Шерлок", Type = "TV_SERIES", Year = 2010 }));
        var resolver = new KinopoiskIdResolver(_client.Object);
        var info = new SeriesInfo { Name = "Шерлок", OriginalTitle = "Sherlock", Year = 2010 };

        Assert.Equal(2, await resolver.ResolveAsync(info, isSeries: true, CancellationToken.None));
    }

    [Fact]
    public async Task ResolveAsync_NoConfidentCandidate_ReturnsNull()
    {
        _client.Setup(c => c.SearchByKeywordAsync("Матрица", It.IsAny<CancellationToken>()))
            .ReturnsAsync(L(new KpCandidate { Id = 301, NameRu = "Матрица", Type = "FILM", Year = 1999 }));
        var resolver = new KinopoiskIdResolver(_client.Object);
        var info = new MovieInfo { Name = "Матрица", Year = 2021 };

        Assert.Null(await resolver.ResolveAsync(info, isSeries: false, CancellationToken.None));
    }

    [Fact]
    public async Task SearchAsync_MergesQueriesWithoutDuplicatesAndRanks()
    {
        _client.Setup(c => c.SearchByKeywordAsync("Матрица", It.IsAny<CancellationToken>()))
            .ReturnsAsync(L(
                new KpCandidate { Id = 10, Type = "FILM", Year = 2021 },
                new KpCandidate { Id = 301, Type = "FILM", Year = 1999 }));
        _client.Setup(c => c.SearchByKeywordAsync("The Matrix", It.IsAny<CancellationToken>()))
            .ReturnsAsync(L(new KpCandidate { Id = 301, Type = "FILM", Year = 1999 }));
        var resolver = new KinopoiskIdResolver(_client.Object);

        var result = await resolver.SearchAsync("Матрица", "The Matrix", 1999, isSeries: false, CancellationToken.None);

        Assert.Equal(new[] { 301, 10 }, result.Select(c => c.Id));
    }

    [Fact]
    public async Task SearchAsync_SameTitles_QueriesOnce()
    {
        _client.Setup(c => c.SearchByKeywordAsync("Dune", It.IsAny<CancellationToken>())).ReturnsAsync(L());
        var resolver = new KinopoiskIdResolver(_client.Object);

        await resolver.SearchAsync("Dune", "dune", null, isSeries: false, CancellationToken.None);

        _client.Verify(c => c.SearchByKeywordAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
