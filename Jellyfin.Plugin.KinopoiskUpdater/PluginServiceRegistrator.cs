using System;
using System.Net;
using System.Net.Http;
using Jellyfin.Plugin.KinopoiskUpdater.Configuration;
using Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk;
using Jellyfin.Plugin.KinopoiskUpdater.Metadata;
using Jellyfin.Plugin.KinopoiskUpdater.Providers;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.KinopoiskUpdater;

/// <summary>
/// Registers plugin services with the server's DI container.
/// Metadata, image, external id and URL providers are discovered by the server itself
/// and get <see cref="IKinopoiskMetadataService"/> injected.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    private static readonly TimeSpan ResponseCacheTimeToLive = TimeSpan.FromMinutes(30);

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<IPluginConfigurationAccessor, PluginConfigurationAccessor>();

        serviceCollection
            .AddHttpClient(KinopoiskConstants.HttpClientName, client =>
            {
                client.BaseAddress = KinopoiskConstants.ApiBaseUri;
                client.Timeout = TimeSpan.FromSeconds(20);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Jellyfin-Plugin-KinopoiskUpdater");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                // The API never redirects; do not follow redirects off the fixed host.
                AllowAutoRedirect = false
            });

        serviceCollection.AddSingleton(_ => new KinopoiskResponseCache(TimeProvider.System, ResponseCacheTimeToLive));
        serviceCollection.AddSingleton<IKinopoiskClient, KinopoiskClient>();
        serviceCollection.AddSingleton<KinopoiskIdResolver>();
        serviceCollection.AddSingleton<IKinopoiskMetadataService, KinopoiskMetadataService>();
    }
}
