using System.Text.Json.Serialization;
using Jellyfin.Plugin.SeerrRequestFav.JellyseerrModel;

namespace Jellyfin.Plugin.SeerrRequestFav.BridgeModels;

/// <summary>
/// Extends JellyseerrModel.Media with correct JSON mappings for actual Jellyseerr API responses.
/// </summary>
public class JellyseerrMedia : Media
{
    [JsonPropertyName("mediaType")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public new MediaType MediaType { get; set; }

    [JsonPropertyName("downloadStatus")]
    public new List<JellyseerrDownloadingItem> DownloadStatus { get; set; } = new();

    [JsonPropertyName("downloadStatus4k")]
    public new List<JellyseerrDownloadingItem> DownloadStatus4k { get; set; } = new();

    [JsonPropertyName("watchlists")]
    public List<JellyseerrWatchlist> WatchlistsList { get; set; } = new();

    [JsonPropertyName("serviceId")]
    public int? ServiceIdInt { get; set; }

    [JsonPropertyName("serviceId4k")]
    public int? ServiceId4kInt { get; set; }

    [JsonPropertyName("externalServiceId")]
    public int? ExternalServiceIdInt { get; set; }

    [JsonPropertyName("externalServiceId4k")]
    public int? ExternalServiceId4kInt { get; set; }

    [JsonPropertyName("requests")]
    public List<JellyseerrMediaRequest> JellyseerrRequests { get; set; } = new();

    // Suppress Plex/Tautulli properties not used
    [JsonIgnore]
    public object? PlexUrl { get; set; }

    [JsonIgnore]
    public object? IsPlex { get; set; }
}
