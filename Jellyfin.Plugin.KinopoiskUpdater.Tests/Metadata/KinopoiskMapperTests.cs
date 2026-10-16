using System;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk;
using Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk.Models;
using Jellyfin.Plugin.KinopoiskUpdater.Metadata;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using Xunit;

namespace Jellyfin.Plugin.KinopoiskUpdater.Tests.Metadata;

public class KinopoiskMapperTests
{
    private static KpFilm Film() => new()
    {
        KinopoiskId = 301,
        ImdbId = "tt0133093",
        NameRu = "Матрица",
        NameOriginal = "The Matrix",
        Description = "Жизнь Томаса Андерсона разделена на две части.",
        ShortDescription = "Хакер Нео узнаёт правду.",
        Slogan = "Добро пожаловать в реальный мир",
        Year = 1999,
        RatingAgeLimits = "age16",
        RatingKinopoisk = 8.53,
        Type = "FILM",
        Genres = [new KpGenre { Genre = "фантастика" }, new KpGenre { Genre = "боевик" }, new KpGenre { Genre = "Фантастика" }],
        Countries = [new KpCountry { Country = "США" }]
    };

    [Fact]
    public void ApplyFilm_Movie_MapsAllFields()
    {
        var movie = new Movie();
        var premiere = new DateTime(1999, 3, 24, 0, 0, 0, DateTimeKind.Utc);

        KinopoiskMapper.ApplyFilm(movie, Film(), premiere);

        Assert.Equal("Матрица", movie.Name);
        Assert.Equal("The Matrix", movie.OriginalTitle);
        Assert.Equal("Жизнь Томаса Андерсона разделена на две части.", movie.Overview);
        Assert.Equal("Добро пожаловать в реальный мир", movie.Tagline);
        Assert.Equal(1999, movie.ProductionYear);
        Assert.Equal(premiere, movie.PremiereDate);
        Assert.Equal("16+", movie.OfficialRating);
        Assert.Equal(8.5f, movie.CommunityRating);
        Assert.Equal(new[] { "Фантастика", "Боевик" }, movie.Genres);
        Assert.Equal(new[] { "США" }, movie.ProductionLocations);
        Assert.Equal("301", movie.GetProviderId(KinopoiskConstants.ProviderIdKey));
        Assert.Equal("tt0133093", movie.GetProviderId(MetadataProvider.Imdb));
    }

    [Fact]
    public void ApplyFilm_EmptyKinopoiskFields_KeepExistingValues()
    {
        var movie = new Movie { Name = "Старое", Overview = "Описание", OfficialRating = "R", Genres = ["Drama"] };
        var film = new KpFilm { KinopoiskId = 5, ImdbId = "not-an-id" };

        KinopoiskMapper.ApplyFilm(movie, film, null);

        Assert.Equal("Старое", movie.Name);
        Assert.Equal("Описание", movie.Overview);
        Assert.Equal("R", movie.OfficialRating);
        Assert.Equal(new[] { "Drama" }, movie.Genres);
        Assert.Null(movie.GetProviderId(MetadataProvider.Imdb));
    }

    [Fact]
    public void ApplyFilm_ShortDescription_IsFallbackOverview()
    {
        var movie = new Movie();
        var film = Film();
        film.Description = " ";

        KinopoiskMapper.ApplyFilm(movie, film, null);

        Assert.Equal("Хакер Нео узнаёт правду.", movie.Overview);
    }

    [Fact]
    public void ApplyFilm_Series_UsesStartYearAndStatus()
    {
        var series = new Series();
        var film = Film();
        film.Type = "TV_SERIES";
        film.Year = 2012;
        film.StartYear = 2008;
        film.Completed = true;

        KinopoiskMapper.ApplyFilm(series, film, null);

        Assert.Equal(2008, series.ProductionYear);
        Assert.Equal(SeriesStatus.Ended, series.Status);
    }

    [Fact]
    public void IsSeries_UsesSerialFlagOrType()
    {
        Assert.True(KinopoiskMapper.IsSeries(new KpFilm { Serial = true, Type = "FILM" }));
        Assert.True(KinopoiskMapper.IsSeries(new KpFilm { Type = "MINI_SERIES" }));
        Assert.False(KinopoiskMapper.IsSeries(new KpFilm { Type = "FILM" }));
    }

    [Fact]
    public void ApplyEpisode_MapsFields()
    {
        var episode = new Episode();
        var kp = new KpEpisode
        {
            SeasonNumber = 1,
            EpisodeNumber = 9,
            NameRu = "Девятая серия",
            NameEn = "Episode Nine",
            Synopsis = "Сюжет",
            ReleaseDate = "2021-03-05"
        };

        KinopoiskMapper.ApplyEpisode(episode, kp);

        Assert.Equal("Девятая серия", episode.Name);
        Assert.Equal("Episode Nine", episode.OriginalTitle);
        Assert.Equal("Сюжет", episode.Overview);
        Assert.Equal(new DateTime(2021, 3, 5, 0, 0, 0, DateTimeKind.Utc), episode.PremiereDate);
        Assert.Equal(2021, episode.ProductionYear);
        Assert.Equal(9, episode.IndexNumber);
        Assert.Equal(1, episode.ParentIndexNumber);
    }

    [Fact]
    public void ApplyEpisode_OnlyEnglishName_UsesIt()
    {
        var episode = new Episode { Name = "Episode 1" };

        KinopoiskMapper.ApplyEpisode(episode, new KpEpisode { NameEn = "Pilot" });

        Assert.Equal("Pilot", episode.Name);
    }

    [Fact]
    public void FindEpisode_MatchesSeasonAndNumber()
    {
        KpEpisode[] episodes =
        [
            new KpEpisode { SeasonNumber = 1, EpisodeNumber = 9, NameRu = "s1e9" },
            new KpEpisode { SeasonNumber = 2, EpisodeNumber = 9, NameRu = "s2e9" }
        ];

        Assert.Equal("s2e9", KinopoiskMapper.FindEpisode(episodes, 2, 9)?.NameRu);
        Assert.Null(KinopoiskMapper.FindEpisode(episodes, 3, 9));
        Assert.Null(KinopoiskMapper.FindEpisode(episodes, null, 9));
    }

    [Fact]
    public void SelectPremiereDate_PrefersEarliestWorldPremiere()
    {
        KpDistribution[] items =
        [
            new KpDistribution { Type = "PREMIERE", Date = "1999-03-01" },
            new KpDistribution { Type = "WORLD_PREMIER", Date = "1999-03-31" },
            new KpDistribution { Type = "WORLD_PREMIER", Date = "1999-03-24" },
            new KpDistribution { Type = "COUNTRY_SPECIFIC", Date = "1999-01-01" }
        ];

        Assert.Equal(new DateTime(1999, 3, 24, 0, 0, 0, DateTimeKind.Utc), KinopoiskMapper.SelectPremiereDate(items));
    }

    [Fact]
    public void SelectPremiereDate_FallsBackToPremiere()
    {
        KpDistribution[] items =
        [
            new KpDistribution { Type = "PREMIERE", Date = "2001-05-02" },
            new KpDistribution { Type = "PREMIERE", Date = "2001-04-30" },
            new KpDistribution { Type = "WORLD_PREMIER", Date = null }
        ];

        Assert.Equal(new DateTime(2001, 4, 30, 0, 0, 0, DateTimeKind.Utc), KinopoiskMapper.SelectPremiereDate(items));
        Assert.Null(KinopoiskMapper.SelectPremiereDate([]));
    }

    [Fact]
    public void BuildPeople_MapsKindsLimitsActorsAndSkipsUnknown()
    {
        KpStaff[] staff =
        [
            new KpStaff { StaffId = 1, NameRu = "Лана Вачовски", ProfessionKey = "DIRECTOR" },
            new KpStaff { StaffId = 2, NameRu = "Киану Ривз", Description = "Neo", ProfessionKey = "ACTOR", PosterUrl = "https://st.kp.yandex.net/images/actor_iphone/iphone360_2.jpg" },
            new KpStaff { StaffId = 3, NameEn = "Laurence Fishburne", Description = "Morpheus", ProfessionKey = "ACTOR" },
            new KpStaff { StaffId = 4, NameRu = "Третий актёр", ProfessionKey = "ACTOR" },
            new KpStaff { StaffId = 5, NameRu = "Оператор", ProfessionKey = "OPERATOR" },
            new KpStaff { StaffId = 6, ProfessionKey = "WRITER" },
            new KpStaff { StaffId = 7, NameRu = "Продюсер", ProfessionKey = "PRODUCER_USSR", PosterUrl = "not a url" }
        ];

        var people = KinopoiskMapper.BuildPeople(staff, maxActors: 2);

        Assert.Equal(new[] { "Лана Вачовски", "Киану Ривз", "Laurence Fishburne", "Продюсер" }, people.Select(p => p.Name));
        Assert.Equal(new[] { PersonKind.Director, PersonKind.Actor, PersonKind.Actor, PersonKind.Producer }, people.Select(p => p.Type));
        Assert.Equal("Neo", people[1].Role);
        Assert.Equal(string.Empty, people[0].Role);
        Assert.Equal("https://st.kp.yandex.net/images/actor_iphone/iphone360_2.jpg", people[1].ImageUrl);
        Assert.Null(people[3].ImageUrl);
        Assert.Equal("2", people[1].GetProviderId(KinopoiskConstants.ProviderIdKey));
        Assert.Equal(new[] { 0, 1, 2, 3 }, people.Select(p => p.SortOrder ?? -1));
    }

    [Fact]
    public void BuildPeople_ZeroActors_KeepsCrew()
    {
        KpStaff[] staff =
        [
            new KpStaff { NameRu = "Актёр", ProfessionKey = "ACTOR" },
            new KpStaff { NameRu = "Композитор", ProfessionKey = "COMPOSER" }
        ];

        var people = KinopoiskMapper.BuildPeople(staff, maxActors: 0);

        Assert.Single(people);
        Assert.Equal(PersonKind.Composer, people[0].Type);
    }
}
