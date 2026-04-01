using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;

namespace Jellyfin.Plugin.SeerrRequestFav.JellyfinModels;

/// <summary>
/// Wrapper around Jellyfin's ILibraryManager interface.
/// </summary>
public class JellyfinILibraryManager : WrapperBase<ILibraryManager>
{
    public JellyfinILibraryManager(ILibraryManager libraryManager) : base(libraryManager)
    {
        InitializeVersionSpecific();
    }
}
