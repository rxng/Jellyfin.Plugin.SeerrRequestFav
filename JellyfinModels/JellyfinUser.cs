using System;

#if JELLYFIN_10_11
using JellyfinUserEntity = Jellyfin.Database.Implementations.Entities.User;
#else
using JellyfinUserEntity = Jellyfin.Data.Entities.User;
#endif

namespace Jellyfin.Plugin.SeerrRequestFav.JellyfinModels;

/// <summary>
/// Wrapper around Jellyfin's User class.
/// </summary>
public class JellyfinUser : WrapperBase<JellyfinUserEntity>
{
    public JellyfinUser(JellyfinUserEntity user) : base(user)
    {
        InitializeVersionSpecific();
    }

    public string Username => Inner.Username;
    public Guid Id => Inner.Id;
}
