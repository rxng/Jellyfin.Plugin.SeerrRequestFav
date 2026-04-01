using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.SeerrRequestFav.BridgeModels;

/// <summary>
/// Generic paginated response from Jellyseerr API.
/// </summary>
public class JellyseerrPaginatedResponse<T>
{
    [JsonPropertyName("pageInfo")]
    public PageInfo? PageInfo { get; set; }

    [JsonPropertyName("results")]
    public List<T> Results { get; set; } = new();
}

public class PageInfo
{
    [JsonPropertyName("pages")]
    public int Pages { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("results")]
    public int Results { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }
}
