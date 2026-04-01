using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.SeerrRequestFav.JellyfinModels;

/// <summary>
/// Wrapper around Jellyfin's Movie class.
/// </summary>
public class JellyfinMovie : WrapperBase<Movie>, IJellyfinItem
{
    public BaseItemKind TypeName => BaseItemKind.Movie;

    public JellyfinMovie(Movie movie) : base(movie)
    {
        InitializeVersionSpecific();
    }

    public static JellyfinMovie FromMovie(Movie movie) => new JellyfinMovie(movie);

    public static JellyfinMovie FromItem(BaseItem item)
    {
        if (item is Movie movie)
            return new JellyfinMovie(movie);
        throw new ArgumentException($"Item is not a Movie. Type: {item?.GetType().Name}", nameof(item));
    }

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
    /// True when a real video file exists on disk (not a virtual/stream item).
    /// </summary>
    public bool HasLocalFile() =>
        Inner.LocationType == LocationType.FileSystem &&
        !string.IsNullOrEmpty(Inner.Path) &&
        File.Exists(Inner.Path);
}
