using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.SeerrRequestFav.BridgeModels;

/// <summary>
/// Extends User with a helper to get the Jellyfin user GUID in standard format.
/// </summary>
public class JellyseerrUser : JellyseerrModel.User
{
    /// <summary>
    /// Returns the Jellyfin user GUID in standard hyphenated format.
    /// Jellyseerr stores it without hyphens; this normalizes it.
    /// </summary>
    [JsonIgnore]
    public string? JellyfinUserGuid
    {
        get
        {
            var raw = JellyfinUserId;
            if (string.IsNullOrEmpty(raw)) return null;
            if (Guid.TryParse(raw, out var guid)) return guid.ToString();
            return null;
        }
    }
}
