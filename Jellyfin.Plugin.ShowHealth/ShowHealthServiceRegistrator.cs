using System.Net.Http;
using Jellyfin.Plugin.ShowHealth.Services;
using Jellyfin.Plugin.ShowHealth.Services.Jellyfin;
using Jellyfin.Plugin.ShowHealth.Services.TvMaze;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.ShowHealth;

/// <summary>
/// Registers plugin services into the Jellyfin DI container.
/// </summary>
public class ShowHealthServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<TvMazeRateLimiter>();
        serviceCollection.AddSingleton<TvMazeClient>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var appPaths = sp.GetRequiredService<IServerApplicationPaths>();
            ShowHealthPaths.RemoveLegacyImdbCache(appPaths);
            var cacheDir = ShowHealthPaths.GetCacheDirectory(appPaths);
            var rateLimiter = sp.GetRequiredService<TvMazeRateLimiter>();
            return new TvMazeClient(httpClientFactory, cacheDir, rateLimiter);
        });
        serviceCollection.AddSingleton<JellyfinLibraryService>();
        serviceCollection.AddSingleton<ShowHealthAnalyzer>();
    }
}
