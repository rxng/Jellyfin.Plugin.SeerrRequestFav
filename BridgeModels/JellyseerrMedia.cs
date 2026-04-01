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

    [JsonIgnore]
    public new List<DownloadingItem> DownloadStatus { get; set; } = new();
    [JsonPropertyName("downloadStatus")]
    public List<JellyseerrDownloadingItem> DownloadStatusList { get; set; } = new();

    [JsonIgnore]
    public new List<DownloadingItem> DownloadStatus4k { get; set; } = new();
    [JsonPropertyName("downloadStatus4k")]
    public List<JellyseerrDownloadingItem> DownloadStatus4kList { get; set; } = new();

    [JsonIgnore]
    public new string Watchlists { get; set; } = string.Empty;
    [JsonPropertyName("watchlists")]
    public List<JellyseerrWatchlist> WatchlistsList { get; set; } = new();

    [JsonIgnore]
    public new string? ServiceId { get; set; }
    [JsonPropertyName("serviceId")]
    public int? ServiceIdInt { get; set; }

    [JsonIgnore]
    public new string? ServiceId4k { get; set; }
    [JsonPropertyName("serviceId4k")]
    public int? ServiceId4kInt { get; set; }

    [JsonIgnore]
    public new string? ExternalServiceId { get; set; }
    [JsonPropertyName("externalServiceId")]
    public int? ExternalServiceIdInt { get; set; }

    [JsonIgnore]
    public new string? ExternalServiceId4k { get; set; }
    [JsonPropertyName("externalServiceId4k")]
    public int? ExternalServiceId4kInt { get; set; }

    [JsonPropertyName("requests")]
    public List<JellyseerrMediaRequest> JellyseerrRequests { get; set; } = new();
}
