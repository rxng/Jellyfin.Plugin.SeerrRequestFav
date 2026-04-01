using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.SeerrRequestFav.JellyfinModels;

/// <summary>
/// Wrapper around Jellyfin's Series class.
/// </summary>
public class JellyfinSeries : WrapperBase<Series>, IJellyfinItem
{
    public BaseItemKind TypeName => BaseItemKind.Series;

    public JellyfinSeries(Series series) : base(series)
    {
        InitializeVersionSpecific();
    }

    public static JellyfinSeries FromSeries(Series series) => new JellyfinSeries(series);

    public static JellyfinSeries FromItem(BaseItem item)
    {
        if (item is Series series)
            return new JellyfinSeries(series);
        throw new ArgumentException($"Item is not a Series. Type: {item?.GetType().Name}", nameof(item));
    }

    public static explicit operator JellyfinSeries(BaseItem item) => FromItem(item);

    public Guid Id => Inner.Id;
    public string Name => Inner.Name;
    public string Path => Inner.Path;
    public Dictionary<string, string> ProviderIds => Inner.ProviderIds;

    public int? GetTmdbId()
    {
        try
        {
            if (ProviderIds.TryGetValue("Tmdb", out var id) && !string.IsNullOrEmpty(id) && int.TryParse(id, out var result))
                return result;
        }
        catch { }
        return null;
    }

    public string? GetProviderId(string name)
    {
        try
        {
            return ProviderIds.TryGetValue(name, out var id) ? id : null;
        }
        catch { }
        return null;
    }

    public bool ItemsMatch(IJellyfinItem other) => other != null && Id == other.Id;

    /// <summary>
    /// True when a real series folder exists on disk (not a virtual/stream item).
    /// Series paths are always directories; checking for at least one episode file
    /// would be more precise but also expensive — the folder check is the right proxy.
    /// </summary>
    public bool HasLocalFile() =>
        Inner.LocationType == LocationType.FileSystem &&
        !string.IsNullOrEmpty(Inner.Path) &&
        Directory.Exists(Inner.Path);
}
