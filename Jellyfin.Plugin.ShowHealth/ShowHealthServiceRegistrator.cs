using System.IO;
using System.Net.Http;
using Jellyfin.Plugin.ShowHealth.Services;
using Jellyfin.Plugin.ShowHealth.Services.ImdbApi;
using Jellyfin.Plugin.ShowHealth.Services.Jellyfin;
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
        serviceCollection.AddSingleton<ImdbApiRateLimiter>();
        serviceCollection.AddSingleton<ImdbApiClient>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var appPaths = sp.GetRequiredService<IServerApplicationPaths>();
            var cacheDir = Path.Combine(appPaths.PluginConfigurationsPath, "ShowHealth", "cache");
            var rateLimiter = sp.GetRequiredService<ImdbApiRateLimiter>();
            return new ImdbApiClient(httpClientFactory, cacheDir, rateLimiter);
        });
        serviceCollection.AddScoped<JellyfinLibraryService>();
        serviceCollection.AddScoped<ShowHealthAnalyzer>();
    }
}
