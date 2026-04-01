using System.Text.Json.Serialization;
using Jellyfin.Plugin.SeerrRequestFav.JellyseerrModel;

namespace Jellyfin.Plugin.SeerrRequestFav.BridgeModels;

public class JellyseerrWatchlist : Watchlist
{
    [JsonPropertyName("mediaType")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public new MediaType MediaType { get; set; }
}
