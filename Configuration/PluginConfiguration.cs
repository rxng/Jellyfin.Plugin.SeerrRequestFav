using System.ComponentModel.DataAnnotations;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.SeerrRequestFav.Configuration;

/// <summary>
/// Plugin configuration for SeerrRequestFav.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Default values for all configuration properties.
    /// </summary>
    public static readonly Dictionary<string, object> DefaultValues = new()
    {
        { nameof(IsEnabled), false },
        { nameof(JellyseerrUrl), "http://localhost:5055" },
        { nameof(ApiKey), string.Empty },
        { nameof(ResponsiveFavoriteRequests), true },
        { nameof(RemoveRequestedFromFavorites), false },
        { nameof(RequestFirstSeason), false },
        { nameof(TaskTimeoutMinutes), 10 },
        { nameof(RequestTimeout), 60 },
        { nameof(RetryAttempts), 3 },
        { nameof(EnableDebugLogging), false },
        { nameof(EnableTraceLogging), false },
    };

    // ===== General =====

    /// <summary>
    /// Gets or sets whether the plugin is enabled.
    /// </summary>
    public bool? IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the Jellyseerr base URL.
    /// </summary>
    [Required]
    public string JellyseerrUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Jellyseerr API key.
    /// </summary>
    [Required]
    public string ApiKey { get; set; } = string.Empty;

    // ===== Favorite Request Settings =====

    /// <summary>
    /// When enabled, automatically requests items from Jellyseerr whenever they are marked as
    /// favorite in Jellyfin (real-time, event-driven).
    /// </summary>
    public bool? ResponsiveFavoriteRequests { get; set; }

    /// <summary>
    /// When enabled, removes the item from all users' favorites after a request is successfully
    /// created in Jellyseerr.
    /// </summary>
    public bool? RemoveRequestedFromFavorites { get; set; }

    /// <summary>
    /// When enabled, only the first season is requested for TV shows.
    /// </summary>
    public bool? RequestFirstSeason { get; set; }

    // ===== Advanced Settings =====

    /// <summary>
    /// Gets or sets the timeout (in minutes) for plugin tasks.
    /// </summary>
    public int? TaskTimeoutMinutes { get; set; }

    /// <summary>
    /// Gets or sets the HTTP request timeout in seconds.
    /// </summary>
    public int? RequestTimeout { get; set; }

    /// <summary>
    /// Gets or sets the number of HTTP retry attempts.
    /// </summary>
    public int? RetryAttempts { get; set; }

    /// <summary>
    /// Gets or sets whether to enable debug logging.
    /// </summary>
    public bool? EnableDebugLogging { get; set; }

    /// <summary>
    /// Gets or sets whether to enable trace logging (requires debug logging).
    /// </summary>
    public bool? EnableTraceLogging { get; set; }
}
