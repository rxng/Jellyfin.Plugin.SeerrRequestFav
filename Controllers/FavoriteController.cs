using Jellyfin.Plugin.SeerrRequestFav.BridgeModels;
using Jellyfin.Plugin.SeerrRequestFav.Configuration;
using Jellyfin.Plugin.SeerrRequestFav.JellyseerrModel;
using Jellyfin.Plugin.SeerrRequestFav.Services;
using Jellyfin.Plugin.SeerrRequestFav.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Jellyfin.Plugin.SeerrRequestFav.Controllers;

[ApiController]
[Route("SeerrRequestFav")]
public class FavoriteController : ControllerBase
{
    private readonly DebugLogger<FavoriteController> _logger;
    private readonly ApiService _apiService;
    private readonly FavoriteService _favoriteService;

    public FavoriteController(
        ILogger<FavoriteController> logger,
        ApiService apiService,
        FavoriteService favoriteService)
    {
        _logger = new DebugLogger<FavoriteController>(logger);
        _apiService = apiService;
        _favoriteService = favoriteService;
    }

    // ---------------------------------------------------------------------------------
    // Public controller actions
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Synchronises all Jellyfin favourites to Jellyseerr as media requests.
    /// This method can also be called internally from <see cref="Services.FavoriteEventHandler"/>.
    /// </summary>
    [HttpPost("SyncFavorites")]
    public async Task<IActionResult> SyncFavorites()
    {
        _logger.LogInformation("SyncFavorites requested");
        try
        {
            var report = await SyncFavoritesAsync();
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SyncFavorites failed unexpectedly");
            return StatusCode(500, new
            {
                success = false,
                message = "SyncFavorites encountered an unexpected error",
                details = $"{ex.GetType().Name}: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Tests the Jellyseerr connection and privilege level using the supplied (or saved) credentials.
    /// </summary>
    [HttpPost("TestConnection")]
    public async Task<IActionResult> TestConnection([FromBody] JsonElement requestData)
    {
        _logger.LogInformation("TestConnection requested");
        try
        {
            var jellyseerrUrl = (requestData.TryGetProperty("JellyseerrUrl", out var urlEl)
                && !string.IsNullOrWhiteSpace(urlEl.GetString())
                ? urlEl.GetString()
                : null)
                ?? Plugin.GetConfigOrDefault<string>(nameof(PluginConfiguration.JellyseerrUrl));

            var apiKey = requestData.TryGetProperty("ApiKey", out var keyEl)
                && !string.IsNullOrWhiteSpace(keyEl.GetString())
                ? keyEl.GetString()
                : null;

            if (string.IsNullOrEmpty(apiKey))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "API Key is required"
                });
            }

            // Test basic connectivity — short timeout so the UI doesn't hang.
            _logger.LogInformation("Testing basic connectivity to Jellyseerr at: {Url}", jellyseerrUrl);
            var status = await _apiService.TestConnectionAsync(jellyseerrUrl, apiKey, connectionTimeoutSeconds: 10, maxRetries: 1);
            _logger.LogInformation("Basic connectivity OK (Jellyseerr v{Version})", status?.Version);

            // Check that the API key has user-list privileges (required for request mapping).
            // Use the same short timeout to avoid the UI hanging during the privileges check.
            var testConfig = new PluginConfiguration
            {
                JellyseerrUrl = jellyseerrUrl!,
                ApiKey = apiKey,
                RequestTimeout = 10,
                RetryAttempts = 1
            };

            _logger.LogInformation("Checking user-list privileges for API key");
            var usersResult = await _apiService.CallEndpointAsync(JellyseerrEndpoint.UserList, testConfig);
            var users = usersResult as List<JellyseerrUser>;

            if (users == null || users.Count == 0)
            {
                return StatusCode(403, new
                {
                    success = false,
                    message = "API key lacks required permissions to list users",
                    errorCode = "INSUFFICIENT_PRIVILEGES"
                });
            }

            _logger.LogInformation("TestConnection succeeded. Found {UserCount} Jellyseerr user(s)", users.Count);
            return Ok(new
            {
                success = true,
                message = $"Connected to Jellyseerr v{status?.Version}. Found {users.Count} user(s).",
                userCount = users.Count
            });
        }
        catch (HttpRequestException ex) when (ex.StatusCode != null)
        {
            _logger.LogWarning(ex, "TestConnection HTTP error ({StatusCode})", (int)ex.StatusCode);
            return StatusCode((int)ex.StatusCode, new
            {
                success = false,
                message = ex.StatusCode switch
                {
                    System.Net.HttpStatusCode.Unauthorized => "Invalid API key – Jellyseerr rejected the credentials.",
                    System.Net.HttpStatusCode.Forbidden    => "API key lacks required permissions.",
                    System.Net.HttpStatusCode.BadGateway   => "Could not reach Jellyseerr – check the URL.",
                    _ => $"Jellyseerr returned HTTP {(int)ex.StatusCode}"
                },
                details = ex.Message
            });
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex, "TestConnection timed out");
            return StatusCode(408, new
            {
                success = false,
                message = "Connection timed out – check that Jellyseerr is running and the URL is correct.",
                errorCode = "TIMEOUT"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TestConnection failed unexpectedly");
            return StatusCode(500, new
            {
                success = false,
                message = "Connection test failed",
                details = $"{ex.GetType().Name}: {ex.Message}"
            });
        }
    }

    // ---------------------------------------------------------------------------------
    // Internal helper (also called from FavoriteEventHandler)
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Executes the full sync pipeline within a named plugin lock and returns a summary object.
    /// </summary>
    public async Task<object> SyncFavoritesAsync()
    {
        return await Plugin.ExecuteWithLockAsync<object>(async () =>
        {
            _logger.LogInformation("Starting SyncFavorites pipeline");

            // Step 1 – Check plugin is enabled and has valid config.
            if (!Plugin.GetConfigOrDefault<bool>(nameof(PluginConfiguration.IsEnabled)))
            {
                _logger.LogInformation("Plugin is disabled – skipping SyncFavorites");
                return (object)new { success = true, message = "Plugin is disabled", skipped = true };
            }

            var url = Plugin.GetConfigOrDefault<string>(nameof(PluginConfiguration.JellyseerrUrl));
            var apiKey = Plugin.GetConfigOrDefault<string>(nameof(PluginConfiguration.ApiKey));
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("JellyseerrUrl or ApiKey not configured – skipping SyncFavorites");
                return (object)new { success = false, message = "JellyseerrUrl or ApiKey not configured" };
            }

            // Step 2 – Test connection.
            await _apiService.TestConnectionAsync();

            // Step 3 – Load all user favourites from every Jellyfin library.
            var allFavorites = _favoriteService.GetUserFavorites();
            _logger.LogInformation("Found {Count} total favourites across all users", allFavorites.Count);

            if (allFavorites.Count == 0)
                return (object)new { success = true, message = "No favourites found", created = 0 };

            // Step 4 – Skip items that already have a local media file on disk.
            var (alreadyLocal, needsRequest) = _favoriteService.SplitByLocalFile(allFavorites);
            _logger.LogInformation("{Local} items already on disk (skipped), {Needs} to check",
                alreadyLocal.Count, needsRequest.Count);
            foreach (var f in alreadyLocal.DistinctBy(f => f.item.Id))
                _logger.LogInformation("  [on disk]   {Name}", f.item.Name);

            if (needsRequest.Count == 0)
                return (object)new
                {
                    success = true,
                    message = "All favourited items are already on disk.",
                    created = 0,
                    skippedLocal = alreadyLocal.Count
                };

            // Step 5 – Fetch Jellyseerr users.
            var jellyseerrUsers = await _favoriteService.GetJellyseerrUsersAsync();
            if (jellyseerrUsers.Count == 0)
            {
                _logger.LogWarning("No Jellyseerr users found – cannot map favourites to requests");
                return (object)new { success = false, message = "No Jellyseerr users found (ensure Jellyfin auth is linked)" };
            }

            // Step 6 – Filter already-requested items (from the not-yet-local subset).
            var (alreadyRequested, pendingFavorites) =
                await _favoriteService.FilterRequestsFromFavorites(needsRequest);

            _logger.LogInformation("{PendingCount} pending, {AlreadyCount} already requested",
                pendingFavorites.Count, alreadyRequested.Count);
            foreach (var f in alreadyRequested.DistinctBy(f => f.item.Id))
                _logger.LogInformation("  [requested] {Name}", f.item.Name);
            foreach (var f in pendingFavorites.DistinctBy(f => f.item.Id))
                _logger.LogInformation("  [pending]   {Name}", f.item.Name);

            // Step 7 – Map Jellyfin users to Jellyseerr users.
            var withUser = _favoriteService.EnsureJellyseerrUser(pendingFavorites, jellyseerrUsers);

            // Step 8 – Create requests for pending favourites.
            var (processed, blocked) = await _favoriteService.RequestFavorites(withUser);

            // Step 9 – Optionally unfavourite requested items.
            // Items skipped because they already exist on disk are NOT unfavourited.
            var allRequestedFavs = alreadyRequested
                .Concat(processed.Select(p => needsRequest.First(f => f.item.Id == p.item.Id)))
                .ToList();

            var cleared = await _favoriteService.ClearRequestedFavoritesAsync(allRequestedFavs);

            _logger.LogInformation(
                "SyncFavorites complete: {Created} created, {Failed} failed, {Cleared} unfavourited, {Local} skipped (on disk)",
                processed.Count, blocked.Count, cleared.Count, alreadyLocal.Count);
            foreach (var (item, _) in processed)
                _logger.LogInformation("  [created]   {Name}", item.Name);
            foreach (var item in blocked)
                _logger.LogInformation("  [failed]    {Name}", item.Name);

            return (object)new
            {
                success = true,
                message = $"Sync complete. Created {processed.Count} request(s).",
                created = processed.Count,
                alreadyRequested = alreadyRequested.Count,
                failed = blocked.Count,
                cleared = cleared.Count,
                skippedLocal = alreadyLocal.Count,
                skippedNoUser = pendingFavorites.Count - withUser.Count
            };
        }, _logger, "SyncFavorites");
    }
}
