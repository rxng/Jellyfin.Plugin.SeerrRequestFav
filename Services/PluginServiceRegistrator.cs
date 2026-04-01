using Microsoft.Extensions.DependencyInjection;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Jellyfin.Plugin.SeerrRequestFav.Controllers;
using Jellyfin.Plugin.SeerrRequestFav.Services;
using Jellyfin.Plugin.SeerrRequestFav.JellyfinModels;

namespace Jellyfin.Plugin.SeerrRequestFav.Services;

/// <summary>
/// Registers SeerrRequestFav services with the Jellyfin dependency injection container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddLogging();

        // HTTP client for the Jellyseerr API.
        // AddHttpClient<T>() registers ApiService as a typed client with a managed HttpClient.
        // Do NOT also call AddScoped<ApiService>() — that would shadow the typed client
        // registration and break HttpClient injection.
        serviceCollection.AddHttpClient<ApiService>();

        // Thin wrappers around Jellyfin interfaces that handle version-conditional types.
        serviceCollection.AddScoped<JellyfinILibraryManager>(provider =>
            new JellyfinILibraryManager(
                provider.GetRequiredService<MediaBrowser.Controller.Library.ILibraryManager>()));

        serviceCollection.AddScoped<JellyfinIUserDataManager>(provider =>
            new JellyfinIUserDataManager(
                provider.GetRequiredService<MediaBrowser.Controller.Library.IUserDataManager>()));

        serviceCollection.AddScoped<JellyfinIUserManager>(provider =>
            new JellyfinIUserManager(
                provider.GetRequiredService<MediaBrowser.Controller.Library.IUserManager>()));

        // Core services.
        serviceCollection.AddScoped<FavoriteService>();

        // Controllers.
        serviceCollection.AddScoped<FavoriteController>();
        serviceCollection.AddScoped<PluginConfigurationController>();

        // Hosted service (event listener).
        serviceCollection.AddHostedService<FavoriteEventHandler>();
    }
}
