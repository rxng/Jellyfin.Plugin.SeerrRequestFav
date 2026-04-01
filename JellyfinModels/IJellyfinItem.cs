using Jellyfin.Data.Enums;

namespace Jellyfin.Plugin.SeerrRequestFav.JellyfinModels;

/// <summary>
/// Common interface for Jellyfin item wrappers.
/// </summary>
public interface IJellyfinItem
{
    BaseItemKind TypeName { get; }
    Guid Id { get; }
    string Name { get; }
    string Path { get; }
    Dictionary<string, string> ProviderIds { get; }
    int? GetTmdbId();
    string? GetProviderId(string name);
    bool ItemsMatch(IJellyfinItem other);
    /// <summary>
    /// Returns true when an actual media file/folder exists on disk for this item.
    /// Virtual items, streams, and placeholders return false.
    /// </summary>
    bool HasLocalFile();
}
