using System.Text.Json.Serialization;
using Jellyfin.Plugin.SeerrRequestFav.JellyseerrModel;

namespace Jellyfin.Plugin.SeerrRequestFav.BridgeModels;

public class JellyseerrDownloadingItem : DownloadingItem
{
    [JsonPropertyName("mediaType")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public new JellyseerrModel.MediaType MediaType { get; set; }
}
