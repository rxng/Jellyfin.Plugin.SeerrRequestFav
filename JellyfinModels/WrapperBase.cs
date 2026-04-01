namespace Jellyfin.Plugin.SeerrRequestFav.JellyfinModels;

/// <summary>
/// Base wrapper class for Jellyfin objects using composition pattern.
/// </summary>
public abstract class WrapperBase<T> where T : class
{
    public readonly T Inner;

    protected WrapperBase(T inner) => Inner = inner;

    public static implicit operator T(WrapperBase<T> wrapper) => wrapper.Inner;

    protected virtual void InitializeVersionSpecific()
    {
#if JELLYFIN_10_11
        InitializeV10_11();
#else
        InitializeV10_10_7();
#endif
    }

#if JELLYFIN_10_11
    protected virtual void InitializeV10_11() { }
#else
    protected virtual void InitializeV10_10_7() { }
#endif
}
