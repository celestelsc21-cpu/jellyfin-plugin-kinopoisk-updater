using Jellyfin.Plugin.KinopoiskUpdater.Configuration;
using Xunit;

namespace Jellyfin.Plugin.KinopoiskUpdater.Tests.Configuration;

public class PluginConfigurationTests
{
    [Fact]
    public void Defaults_AreSafe()
    {
        var config = new PluginConfiguration();

        Assert.Equal(string.Empty, config.ApiKey);
        Assert.False(config.HasApiKey);
        Assert.Equal(PluginConfiguration.DefaultMaxActors, config.MaxActors);
    }

    [Fact]
    public void Normalize_TrimsApiKey()
    {
        var config = new PluginConfiguration { ApiKey = "  0123abcd-ef45-6789  " };

        config.Normalize();

        Assert.Equal("0123abcd-ef45-6789", config.ApiKey);
        Assert.True(config.HasApiKey);
    }

    [Theory]
    [InlineData("abc\r\nX-Injected: 1")]
    [InlineData("ключ")]
    [InlineData("abc def")]
    public void Normalize_InvalidApiKey_IsCleared(string key)
    {
        var config = new PluginConfiguration { ApiKey = key };

        config.Normalize();

        Assert.Equal(string.Empty, config.ApiKey);
    }

    [Fact]
    public void Normalize_TooLongApiKey_IsCleared()
    {
        var config = new PluginConfiguration { ApiKey = new string('a', PluginConfiguration.ApiKeyMaxLength + 1) };

        config.Normalize();

        Assert.Equal(string.Empty, config.ApiKey);
    }

    [Fact]
    public void Normalize_NullApiKey_BecomesEmpty()
    {
        var config = new PluginConfiguration { ApiKey = null! };

        config.Normalize();

        Assert.Equal(string.Empty, config.ApiKey);
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(0, 0)]
    [InlineData(30, 30)]
    [InlineData(1000, PluginConfiguration.MaxActorsLimit)]
    public void Normalize_ClampsMaxActors(int value, int expected)
    {
        var config = new PluginConfiguration { MaxActors = value };

        config.Normalize();

        Assert.Equal(expected, config.MaxActors);
    }
}
