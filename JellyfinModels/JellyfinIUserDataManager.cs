using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using System.Threading;

#if JELLYFIN_10_11
using JellyfinUserEntity = Jellyfin.Database.Implementations.Entities.User;
#else
using JellyfinUserEntity = Jellyfin.Data.Entities.User;
#endif

namespace Jellyfin.Plugin.SeerrRequestFav.JellyfinModels;

/// <summary>
/// Wrapper around Jellyfin's IUserDataManager interface.
/// </summary>
public class JellyfinIUserDataManager : WrapperBase<IUserDataManager>
{
    public JellyfinIUserDataManager(IUserDataManager userDataManager) : base(userDataManager)
    {
        InitializeVersionSpecific();
    }

    /// <summary>
    /// Gets all favorites for all users.
    /// </summary>
    public Dictionary<JellyfinUser, List<T>> GetUserFavorites<T>(
        JellyfinIUserManager userManager,
        JellyfinILibraryManager libraryManager) where T : class
    {
        var userFavorites = new Dictionary<JellyfinUser, List<T>>();
        var users = userManager.GetAllUsers().ToList();

        foreach (var user in users)
        {
            var userFavs = libraryManager.Inner.GetItemList(new InternalItemsQuery(user.Inner)
            {
                IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Series },
                IsFavorite = true,
                Recursive = true
            });

            var convertedFavs = userFavs.Select<BaseItem, T?>(item =>
            {
                if (typeof(T) == typeof(JellyfinMovie) && item is Movie movie)
                    return (T)(object)JellyfinMovie.FromMovie(movie);
                else if (typeof(T) == typeof(JellyfinSeries) && item is Series series)
                    return (T)(object)JellyfinSeries.FromSeries(series);
                else if (typeof(T) == typeof(IJellyfinItem))
                {
                    if (item is Movie m) return (T)(object)JellyfinMovie.FromMovie(m);
                    else if (item is Series s) return (T)(object)JellyfinSeries.FromSeries(s);
                }
                return null;
            }).Where(item => item != null).Cast<T>().ToList();

            userFavorites[user] = convertedFavs;
        }

        return userFavorites;
    }

    /// <summary>
    /// Unfavorites the item for the given user.
    /// </summary>
    public async Task<bool> TryUnfavoriteAsync(JellyfinILibraryManager libraryManager, JellyfinUser user, IJellyfinItem item)
    {
        try
        {
            var userEntity = user.Inner;
            var baseItem = libraryManager.Inner.GetItemById<BaseItem>(item.Id, userEntity);
            if (baseItem is null)
                return false;

#if JELLYFIN_10_11
            var data = Inner.GetUserData(userEntity, baseItem);
            if (data is null) return false;
#else
            var data = Inner.GetUserData(userEntity, baseItem);
#endif

            if (!data.IsFavorite)
                return false;

            data.IsFavorite = false;
            await Task.Run(() => Inner.SaveUserData(userEntity, baseItem, data, UserDataSaveReason.UpdateUserRating, CancellationToken.None)).ConfigureAwait(false);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Occurs when user data is saved.
    /// </summary>
    public event EventHandler<UserDataSaveEventArgs>? UserDataSaved
    {
        add { Inner.UserDataSaved += value; }
        remove { Inner.UserDataSaved -= value; }
    }
}
