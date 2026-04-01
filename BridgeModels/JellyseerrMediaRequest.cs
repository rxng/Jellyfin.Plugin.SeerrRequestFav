using System.Text.Json.Serialization;
using Jellyfin.Plugin.SeerrRequestFav.JellyseerrModel;
using Jellyfin.Plugin.SeerrRequestFav.JellyfinModels;

namespace Jellyfin.Plugin.SeerrRequestFav.BridgeModels;

/// <summary>
/// Extends MediaRequest with correct JSON type converters and bridge model references.
/// </summary>
public class JellyseerrMediaRequest : MediaRequest
{
    [JsonPropertyName("type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public new MediaType Type { get; set; }

    [JsonPropertyName("media")]
    public new JellyseerrMedia? Media { get; set; }

    /// <summary>
    /// Returns true if this request matches the given Jellyfin item by TMDB ID and media type.
    /// </summary>
    public bool EqualsItem(IJellyfinItem? item)
    {
        if (item == null || Media == null) return false;
        var tmdbId = item.GetTmdbId();
        if (!tmdbId.HasValue) return false;
        var mediaType = item switch
        {
            JellyfinMovie => MediaType.MOVIE,
            JellyfinSeries => MediaType.TV,
            _ => throw new NotSupportedException($"Unsupported item type: {item.GetType().Name}")
        };
        return Media.TmdbId == tmdbId.Value && Media.MediaType == mediaType;
    }
}
