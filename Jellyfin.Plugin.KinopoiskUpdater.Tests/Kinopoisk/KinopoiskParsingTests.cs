using System;
using Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk;
using Xunit;

namespace Jellyfin.Plugin.KinopoiskUpdater.Tests.Kinopoisk;

public class KinopoiskParsingTests
{
    [Theory]
    [InlineData("2010", 2010)]
    [InlineData("2010-2015", 2010)]
    [InlineData(" 1999–... ", 1999)]
    [InlineData("null", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("20101", null)]
    [InlineData("0042", null)]
    public void ParseYear_HandlesKinopoiskFormats(string? value, int? expected)
    {
        Assert.Equal(expected, KinopoiskParsing.ParseYear(value));
    }

    [Theory]
    [InlineData("2008-01-20")]
    [InlineData("2008-01-20T15:30:00")]
    [InlineData(" 2008-01-20 ")]
    public void ParseDate_ReturnsUtcDate(string value)
    {
        var date = KinopoiskParsing.ParseDate(value);

        Assert.Equal(new DateTime(2008, 1, 20, 0, 0, 0, DateTimeKind.Utc), date);
        Assert.Equal(DateTimeKind.Utc, date!.Value.Kind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("скоро")]
    [InlineData("2008-13-40")]
    public void ParseDate_Invalid_ReturnsNull(string? value)
    {
        Assert.Null(KinopoiskParsing.ParseDate(value));
    }

    [Theory]
    [InlineData("age16", "16+")]
    [InlineData("AGE0", "0+")]
    [InlineData("18", "18+")]
    [InlineData("age", null)]
    [InlineData("r", null)]
    [InlineData(null, null)]
    public void ParseAgeLimit_ConvertsToRussianRating(string? value, string? expected)
    {
        Assert.Equal(expected, KinopoiskParsing.ParseAgeLimit(value));
    }

    [Theory]
    [InlineData("драма", "Драма")]
    [InlineData(" комедия ", "Комедия")]
    [InlineData("sci-fi", "Sci-fi")]
    [InlineData("  ", null)]
    public void Capitalize_UpperCasesFirstLetter(string? value, string? expected)
    {
        Assert.Equal(expected, KinopoiskParsing.Capitalize(value));
    }

    [Theory]
    [InlineData("TV_SERIES", true)]
    [InlineData("MINI_SERIES", true)]
    [InlineData("tv_show", true)]
    [InlineData("FILM", false)]
    [InlineData("VIDEO", false)]
    [InlineData(null, false)]
    public void IsSeriesType_RecognizesSeries(string? type, bool expected)
    {
        Assert.Equal(expected, KinopoiskParsing.IsSeriesType(type));
    }

    [Fact]
    public void FirstNonEmpty_SkipsBlankValues()
    {
        Assert.Equal("Matrix", KinopoiskParsing.FirstNonEmpty(null, " ", " Matrix "));
        Assert.Null(KinopoiskParsing.FirstNonEmpty(null, string.Empty));
    }
}
