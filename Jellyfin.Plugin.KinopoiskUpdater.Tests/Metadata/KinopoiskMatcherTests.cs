using System.Linq;
using Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk;
using Jellyfin.Plugin.KinopoiskUpdater.Metadata;
using Xunit;

namespace Jellyfin.Plugin.KinopoiskUpdater.Tests.Metadata;

public class KinopoiskMatcherTests
{
    [Theory]
    [InlineData("FILM", 1999, false, 5)]
    [InlineData("FILM", 2000, false, 3)]
    [InlineData("FILM", 2003, false, 2)]
    [InlineData("TV_SERIES", 1999, false, 3)]
    [InlineData("TV_SERIES", 1999, true, 5)]
    [InlineData("FILM", null, false, 2)]
    public void Score_CombinesKindAndYear(string type, int? candidateYear, bool isSeries, int expected)
    {
        var candidate = new KpCandidate { Id = 1, Type = type, Year = candidateYear };

        Assert.Equal(expected, KinopoiskMatcher.Score(candidate, 1999, isSeries));
    }

    [Fact]
    public void Rank_OrdersByScoreAndKeepsOrderForTies()
    {
        KpCandidate[] candidates =
        [
            new KpCandidate { Id = 1, Type = "TV_SERIES", Year = 2003 },
            new KpCandidate { Id = 2, Type = "FILM", Year = 2003 },
            new KpCandidate { Id = 3, Type = "FILM", Year = 1999 },
            new KpCandidate { Id = 4, Type = "FILM", Year = 2004 }
        ];

        var ranked = KinopoiskMatcher.Rank(candidates, 1999, isSeries: false);

        Assert.Equal(new[] { 3, 2, 4, 1 }, ranked.Select(c => c.Id));
    }

    [Theory]
    [InlineData("FILM", 1999, true)]
    [InlineData("FILM", 2000, true)]
    [InlineData("FILM", 2001, false)]
    [InlineData("TV_SERIES", 1999, false)]
    public void IsConfidentMatch_WithYears_RequiresKindAndCloseYear(string type, int candidateYear, bool expected)
    {
        var candidate = new KpCandidate { Id = 1, NameRu = "Другое название", Type = type, Year = candidateYear };

        Assert.Equal(expected, KinopoiskMatcher.IsConfidentMatch(candidate, "Матрица", null, 1999, isSeries: false));
    }

    [Theory]
    [InlineData("Ёлки", true)]
    [InlineData("ёлки!", true)]
    [InlineData("Ёлки 2", false)]
    public void IsConfidentMatch_WithoutYear_RequiresSameTitle(string candidateName, bool expected)
    {
        var candidate = new KpCandidate { Id = 1, NameRu = candidateName, Type = "FILM" };

        Assert.Equal(expected, KinopoiskMatcher.IsConfidentMatch(candidate, "Елки", null, null, isSeries: false));
    }

    [Fact]
    public void IsConfidentMatch_WithoutYear_MatchesOriginalTitle()
    {
        var candidate = new KpCandidate { Id = 1, NameRu = "Во все тяжкие", NameEn = "Breaking Bad", Type = "TV_SERIES" };

        Assert.True(KinopoiskMatcher.IsConfidentMatch(candidate, "Breaking Bad", null, null, isSeries: true));
    }

    [Theory]
    [InlineData("  The  Matrix: Reloaded ", "the matrix reloaded")]
    [InlineData("Ёжик в тумане", "ежик в тумане")]
    [InlineData(null, "")]
    public void Normalize_SimplifiesTitles(string? title, string expected)
    {
        Assert.Equal(expected, KinopoiskMatcher.Normalize(title));
    }
}
