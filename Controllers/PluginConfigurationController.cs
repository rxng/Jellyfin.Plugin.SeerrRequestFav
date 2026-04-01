using Jellyfin.Plugin.SeerrRequestFav.Configuration;
using Jellyfin.Plugin.SeerrRequestFav.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Jellyfin.Plugin.SeerrRequestFav.Controllers;

[ApiController]
[Route("SeerrRequestFav")]
public class PluginConfigurationController : ControllerBase
{
    private readonly DebugLogger<PluginConfigurationController> _logger;

    public PluginConfigurationController(ILogger<PluginConfigurationController> logger)
    {
        _logger = new DebugLogger<PluginConfigurationController>(logger);
    }

    [HttpGet("PluginConfiguration")]
    public IActionResult GetPluginConfiguration()
    {
        _logger.LogDebug("GetPluginConfiguration called");
        try
        {
            var config = Plugin.GetConfiguration();
            var response = new
            {
                IsEnabled = config.IsEnabled,
                JellyseerrUrl = config.JellyseerrUrl,
                ApiKey = config.ApiKey,
                ResponsiveFavoriteRequests = config.ResponsiveFavoriteRequests,
                RemoveRequestedFromFavorites = config.RemoveRequestedFromFavorites,
                RequestFirstSeason = config.RequestFirstSeason,
                TaskTimeoutMinutes = config.TaskTimeoutMinutes,
                RequestTimeout = config.RequestTimeout,
                RetryAttempts = config.RetryAttempts,
                EnableDebugLogging = config.EnableDebugLogging,
                EnableTraceLogging = config.EnableTraceLogging,
                PluginVersion = Plugin.Instance.GetType().Assembly.GetName().Version?.ToString(),
                ConfigDefaults = PluginConfiguration.DefaultValues
            };
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting plugin configuration");
            return StatusCode(500, new { error = "Failed to get configuration", details = ex.Message });
        }
    }

    [HttpPost("PluginConfiguration")]
    public IActionResult UpdatePluginConfiguration([FromBody] JsonElement configData)
    {
        _logger.LogInformation("UpdatePluginConfiguration called");
        try
        {
            var config = Plugin.GetConfiguration();

            SetValue<bool?>(configData, nameof(config.IsEnabled),
                v => config.IsEnabled = v ?? config.IsEnabled);
            SetValue<string?>(configData, nameof(config.JellyseerrUrl),
                v => config.JellyseerrUrl = v ?? config.JellyseerrUrl);
            SetValue<string?>(configData, nameof(config.ApiKey),
                v => config.ApiKey = v ?? config.ApiKey);
            SetValue<bool?>(configData, nameof(config.ResponsiveFavoriteRequests),
                v => config.ResponsiveFavoriteRequests = v ?? config.ResponsiveFavoriteRequests);
            SetValue<bool?>(configData, nameof(config.RemoveRequestedFromFavorites),
                v => config.RemoveRequestedFromFavorites = v ?? config.RemoveRequestedFromFavorites);
            SetValue<bool?>(configData, nameof(config.RequestFirstSeason),
                v => config.RequestFirstSeason = v ?? config.RequestFirstSeason);
            SetValue<int?>(configData, nameof(config.TaskTimeoutMinutes),
                v => config.TaskTimeoutMinutes = v ?? config.TaskTimeoutMinutes);
            SetValue<int?>(configData, nameof(config.RequestTimeout),
                v => config.RequestTimeout = v ?? config.RequestTimeout);
            SetValue<int?>(configData, nameof(config.RetryAttempts),
                v => config.RetryAttempts = v ?? config.RetryAttempts);
            SetValue<bool?>(configData, nameof(config.EnableDebugLogging),
                v => config.EnableDebugLogging = v ?? config.EnableDebugLogging);
            SetValue<bool?>(configData, nameof(config.EnableTraceLogging),
                v => config.EnableTraceLogging = v ?? config.EnableTraceLogging);

            Plugin.Instance.SaveConfiguration(config);
            _logger.LogInformation("Configuration saved");
            return Ok(new { success = true, message = "Configuration saved" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving plugin configuration");
            return StatusCode(500, new { error = "Failed to save configuration", details = ex.Message });
        }
    }

    // ---------------------------------------------------------------------------------
    // Helper
    // ---------------------------------------------------------------------------------

    private static void SetValue<T>(JsonElement data, string key, Action<T> setter)
    {
        if (!data.TryGetProperty(key, out var el)) return;
        try
        {
            var value = el.Deserialize<T>();
            setter(value!);
        }
        catch
        {
            // Silently skip properties that cannot be deserialised so the rest still save.
        }
    }
}
