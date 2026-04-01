using Jellyfin.Plugin.SeerrRequestFav.Configuration;
using Jellyfin.Plugin.SeerrRequestFav.JellyfinModels;
using Jellyfin.Plugin.SeerrRequestFav.Utils;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SeerrRequestFav.Services;

/// <summary>
/// Background service that listens to Jellyfin user-data-saved events and triggers
/// a Jellyseerr request whenever an item is marked as favourite, provided that
/// <see cref="PluginConfiguration.ResponsiveFavoriteRequests"/> is enabled.
/// </summary>
public class FavoriteEventHandler : IHostedService
{
    private readonly DebugLogger<FavoriteEventHandler> _logger;
    private readonly IUserDataManager _sourceUserDataManager;
    private readonly IServiceScopeFactory _scopeFactory;

    public FavoriteEventHandler(
        ILogger<FavoriteEventHandler> logger,
        IUserDataManager sourceUserDataManager,
        IServiceScopeFactory scopeFactory)
    {
        _logger = new DebugLogger<FavoriteEventHandler>(logger);
        _sourceUserDataManager = sourceUserDataManager;
        _scopeFactory = scopeFactory;
    }

    // -------------------------------------------------------------------------------
    // IHostedService
    // -------------------------------------------------------------------------------

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _sourceUserDataManager.UserDataSaved += OnUserDataSaved;
        _logger.LogDebug("FavoriteEventHandler started – subscribed to UserDataSaved");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _sourceUserDataManager.UserDataSaved -= OnUserDataSaved;
        _logger.LogDebug("FavoriteEventHandler stopped – unsubscribed from UserDataSaved");
        return Task.CompletedTask;
    }

    // -------------------------------------------------------------------------------
    // Event handler
    // -------------------------------------------------------------------------------

    private void OnUserDataSaved(object? sender, UserDataSaveEventArgs e)
    {
        try
        {
            // Only react to explicit favourite-toggle actions.
            if (e.SaveReason != UserDataSaveReason.UpdateUserRating) return;
            if (e.UserData?.IsFavorite != true) return;

            if (!Plugin.GetConfigOrDefault<bool>(nameof(PluginConfiguration.ResponsiveFavoriteRequests)))
            {
                _logger.LogTrace("ResponsiveFavoriteRequests is disabled – ignoring favourite event");
                return;
            }

            _logger.LogDebug(
                "Favourite event detected for '{ItemName}' – queuing SyncFavorites",
                e.Item?.Name ?? "unknown");

            // Fire and forget – run on the thread-pool so we don't block the event pipeline.
            _ = Task.Run(() => TriggerSyncAsync(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in FavoriteEventHandler.OnUserDataSaved");
        }
    }

    private async Task TriggerSyncAsync(UserDataSaveEventArgs e)
    {
        try
        {
            // Create a DI scope so we can resolve scoped services from a hosted (singleton) service.
            using var scope = _scopeFactory.CreateScope();
            var favoriteController = scope.ServiceProvider
                .GetService<Controllers.FavoriteController>();

            if (favoriteController == null)
            {
                _logger.LogWarning("Could not resolve FavoriteController – skipping responsive favourite sync");
                return;
            }

            _logger.LogDebug("Triggering SyncFavorites via responsive favourite event");
            await favoriteController.SyncFavoritesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during responsive favourite sync triggered by '{ItemName}'",
                e.Item?.Name ?? "unknown");
        }
    }
}
