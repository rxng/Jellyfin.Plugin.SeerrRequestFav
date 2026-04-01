using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.SeerrRequestFav.JellyfinModels;

/// <summary>
/// Wrapper around Jellyfin's IUserManager interface.
/// </summary>
public class JellyfinIUserManager : WrapperBase<IUserManager>
{
    public JellyfinIUserManager(IUserManager userManager) : base(userManager)
    {
        InitializeVersionSpecific();
    }

    public IEnumerable<JellyfinUser> GetAllUsers()
    {
        return Inner.Users.Select(user => new JellyfinUser((dynamic)user));
    }

    public JellyfinUser? GetUserById(Guid id)
    {
        var user = Inner.GetUserById(id);
        return user != null ? new JellyfinUser((dynamic)user) : null;
    }
}
