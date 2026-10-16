using System.Linq;
using Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk;
using Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk.Models;
using Jellyfin.Plugin.KinopoiskUpdater.Metadata;
using Jellyfin.Plugin.KinopoiskUpdater.Providers;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using Xunit;

namespace Jellyfin.Plugin.KinopoiskUpdater.Tests.Providers;

public class KinopoiskProvidersTests
{
    [Theory]
    [InlineData("https://kinopoiskapiunofficial.tech/images/posters/kp/301.jpg", true)]
    [InlineData("https://avatars.mds.yandex.net/get-kinopoisk-image/1/2/orig", true)]
    [InlineData("https://st.kp.yandex.net/images/actor_iphone/iphone360_7.jpg", true)]
    [InlineData("http://www.kinopoisk.ru/images/film_big/301.jpg", true)]
    [InlineData("https://notkinopoisk.ru/x.jpg", false)]
    [InlineData("https://yandex.net.evil.example/x.jpg", false)]
    [InlineData("https://user:pass@st.kp.yandex.net/x.jpg", false)]
    [InlineData("ftp://st.kp.yandex.net/x.jpg", false)]
    [InlineData("/images/posters/kp/301.jpg", false)]
    [InlineData(null, false)]
    public void ImageHosts_AllowOnlyKinopoiskHosts(string? url, bool expected)
    {
        Assert.Equal(expected, KinopoiskImageHosts.IsAllowed(url, out var uri));
        Assert.Equal(expected, uri is not null);
    }

    [Fact]
    public void ExternalUrls_MovieSeriesAndPerson()
    {
        var provider = new KinopoiskExternalUrlProvider();
        var movie = new Movie();
        movie.SetProviderId(KinopoiskConstants.ProviderIdKey, "301");
        var series = new Series();
        series.SetProviderId(KinopoiskConstants.ProviderIdKey, "77");
        var person = new Person();
        person.SetProviderId(KinopoiskConstants.ProviderIdKey, "7");

        Assert.Equal("https://www.kinopoisk.ru/film/301/", Assert.Single(provider.GetExternalUrls(movie)));
        Assert.Equal("https://www.kinopoisk.ru/film/77/", Assert.Single(provider.GetExternalUrls(series)));
        Assert.Equal("https://www.kinopoisk.ru/name/7/", Assert.Single(provider.GetExternalUrls(person)));
        Assert.Empty(provider.GetExternalUrls(new Movie()));
        Assert.Empty(provider.GetExternalUrls(new Episode()));
    }

    [Fact]
    public void ExternalIds_UseOneKeyAndMatchTypes()
    {
        Assert.True(new KinopoiskMovieExternalId().Supports(new Movie()));
        Assert.False(new KinopoiskMovieExternalId().Supports(new Series()));
        Assert.True(new KinopoiskSeriesExternalId().Supports(new Series()));
        Assert.True(new KinopoiskPersonExternalId().Supports(new Person()));
        Assert.Equal(KinopoiskConstants.ProviderIdKey, new KinopoiskSeriesExternalId().Key);
    }

    [Fact]
    public void FindEpisodes_SingleAndRange()
    {
        KpEpisode[] episodes =
        [
            new KpEpisode { SeasonNumber = 1, EpisodeNumber = 10, NameRu = "e10" },
            new KpEpisode { SeasonNumber = 1, EpisodeNumber = 9, NameRu = "e9" },
            new KpEpisode { SeasonNumber = 2, EpisodeNumber = 9, NameRu = "s2e9" }
        ];

        Assert.Equal(new[] { "e9" }, KinopoiskMapper.FindEpisodes(episodes, 1, 9, null).Select(e => e.NameRu));
        Assert.Equal(new[] { "e9", "e10" }, KinopoiskMapper.FindEpisodes(episodes, 1, 9, 10).Select(e => e.NameRu));
        Assert.Equal(new[] { "e9" }, KinopoiskMapper.FindEpisodes(episodes, 1, 9, 8).Select(e => e.NameRu));
        Assert.Empty(KinopoiskMapper.FindEpisodes(episodes, 3, 1, null));
    }

    [Fact]
    public void ApplyEpisodes_JoinsOnlyNonEmptyParts()
    {
        var episode = new Episode();

        KinopoiskMapper.ApplyEpisodes(
            episode,
            [
                new KpEpisode { SeasonNumber = 1, EpisodeNumber = 9, NameRu = "Девятая", Synopsis = "Первая часть" },
                new KpEpisode { SeasonNumber = 1, EpisodeNumber = 10, NameEn = "Tenth" }
            ]);

        Assert.Equal("Девятая / Tenth", episode.Name);
        Assert.Equal("Первая часть", episode.Overview);
    }

    [Fact]
    public void BuildImages_MapsPosterCoverAndLogo()
    {
        var film = new KpFilm
        {
            PosterUrl = "https://kinopoiskapiunofficial.tech/images/posters/kp/1.jpg",
            PosterUrlPreview = "https://kinopoiskapiunofficial.tech/images/posters/kp_small/1.jpg",
            CoverUrl = "https://avatars.mds.yandex.net/get-ott/cover/orig",
            LogoUrl = "not a url"
        };

        var images = KinopoiskMapper.BuildImages(film);

        Assert.Equal(new[] { ImageType.Primary, ImageType.Backdrop }, images.Select(i => i.Type));
        Assert.Equal("https://kinopoiskapiunofficial.tech/images/posters/kp_small/1.jpg", images[0].ThumbnailUrl);
        Assert.Equal(images[1].Url, images[1].ThumbnailUrl);
        Assert.All(images, i => Assert.Equal(KinopoiskConstants.ProviderName, i.ProviderName));
    }

    [Fact]
    public void ImageProvider_SupportsMoviesAndSeriesOnly()
    {
        var provider = new KinopoiskImageProvider(new Moq.Mock<IKinopoiskMetadataService>().Object);

        Assert.True(provider.Supports(new Movie()));
        Assert.True(provider.Supports(new Series()));
        Assert.False(provider.Supports(new Episode()));
        Assert.False(provider.Supports(new Person()));
    }
}
