using Jellyfin.Plugin.SeerrRequestFav.BridgeModels;
using Jellyfin.Plugin.SeerrRequestFav.JellyseerrModel;
using Jellyfin.Plugin.SeerrRequestFav.JellyfinModels;
using Jellyfin.Plugin.SeerrRequestFav.Configuration;
using Jellyfin.Plugin.SeerrRequestFav.Utils;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SeerrRequestFav.Services;

/// <summary>
/// Service that handles favoriting items to Jellyseerr requests.
/// </summary>
public class FavoriteService
{
    private readonly DebugLogger<FavoriteService> _logger;
    private readonly ApiService _apiService;
    private readonly JellyfinIUserDataManager _userDataManager;
    private readonly JellyfinILibraryManager _libraryManager;
    private readonly JellyfinIUserManager _userManager;

    public FavoriteService(
        ILogger<FavoriteService> logger,
        ApiService apiService,
        JellyfinIUserDataManager userDataManager,
        JellyfinILibraryManager libraryManager,
        JellyfinIUserManager userManager)
    {
        _logger = new DebugLogger<FavoriteService>(logger);
        _apiService = apiService;
        _userDataManager = userDataManager;
        _libraryManager = libraryManager;
        _userManager = userManager;
    }

    /// <summary>
    /// Gets all favorited movies and series for all Jellyfin users.
    /// </summary>
    public List<(JellyfinUser user, IJellyfinItem item)> GetUserFavorites()
    {
        var allFavoritesDict = _userDataManager.GetUserFavorites<IJellyfinItem>(_userManager, _libraryManager);
        _logger.LogDebug("Retrieved favorites for {UserCount} users", allFavoritesDict.Count);

        var flattened = allFavoritesDict
            .SelectMany(kv => kv.Value.Select(item => (kv.Key, item)))
            .ToList();

        _logger.LogDebug("Total flattened favorites: {Count}", flattened.Count);
        return flattened;
    }

    /// <summary>
    /// Splits favourites into items that already have a local media file on disk
    /// (skip entirely – no request needed) and items that don't (proceed normally).
    /// </summary>
    public (List<(JellyfinUser user, IJellyfinItem item)> local,
            List<(JellyfinUser user, IJellyfinItem item)> needsRequest)
        SplitByLocalFile(List<(JellyfinUser user, IJellyfinItem item)> favorites)
    {
        var local = new List<(JellyfinUser, IJellyfinItem)>();
        var needsRequest = new List<(JellyfinUser, IJellyfinItem)>();

        foreach (var fav in favorites)
        {
            if (fav.item.HasLocalFile())
            {
                _logger.LogDebug("Skipping '{ItemName}' – media file already exists on disk", fav.item.Name);
                local.Add(fav);
            }
            else
            {
                needsRequest.Add(fav);
            }
        }

        _logger.LogDebug("SplitByLocalFile: {Local} already local, {Needs} need requesting",
            local.Count, needsRequest.Count);
        return (local, needsRequest);
    }

    /// <summary>
    /// Gets all Jellyseerr users, deduplicating by JellyfinUserGuid.
    /// </summary>
    public async Task<List<JellyseerrUser>> GetJellyseerrUsersAsync()
    {
        try
        {
            var result = await _apiService.CallEndpointAsync(JellyseerrEndpoint.UserList);
            if (result is List<JellyseerrUser> users)
            {
                var unique = users
                    .Where(u => !string.IsNullOrEmpty(u.JellyfinUserGuid))
                    .GroupBy(u => u.JellyfinUserGuid!)
                    .Select(g => g.First())
                    .ToList();

                _logger.LogDebug("Fetched {Total} Jellyseerr users, {Unique} with unique JellyfinUserGuid",
                    users.Count, unique.Count);
                return unique;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Jellyseerr users");
        }
        return new List<JellyseerrUser>();
    }

    /// <summary>
    /// Separates favorites into already-requested and pending (not yet requested or declined).
    /// </summary>
    public async Task<(
        List<(JellyfinUser user, IJellyfinItem item)> requested,
        List<(JellyfinUser user, IJellyfinItem item)> pending)>
        FilterRequestsFromFavorites(List<(JellyfinUser user, IJellyfinItem item)> allFavorites)
    {
        var requested = new List<(JellyfinUser user, IJellyfinItem item)>();
        var pending = new List<(JellyfinUser user, IJellyfinItem item)>();

        try
        {
            var requestsResult = await _apiService.CallEndpointAsync(JellyseerrEndpoint.ReadRequests);
            var jellyseerrRequests = requestsResult as List<JellyseerrMediaRequest> ?? new List<JellyseerrMediaRequest>();

            _logger.LogDebug("Fetched {Count} existing requests from Jellyseerr", jellyseerrRequests.Count);

            // Build lookup of (MediaType, TmdbId) for non-declined requests.
            var requestedLookup = new HashSet<(MediaType type, int tmdbId)>(
                jellyseerrRequests
                    .Where(r => r != null &&
                                r.Status != MediaRequestStatus.DECLINED &&
                                r.JellyseerrMedia != null)
                    .Select(r => (r.JellyseerrMedia!.MediaType, r.JellyseerrMedia.TmdbId)));

            foreach (var fav in allFavorites)
            {
                var tmdb = fav.item.GetTmdbId();
                var type = GetMediaType(fav.item);
                if (tmdb.HasValue && requestedLookup.Contains((type, tmdb.Value)))
                    requested.Add(fav);
                else
                    pending.Add(fav);
            }

            _logger.LogDebug("Filtered favorites: {PendingCount} pending, {RequestedCount} already requested",
                pending.Count, requested.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to filter favorites by existing requests");
        }

        return (requested, pending);
    }

    /// <summary>
    /// Maps each Jellyfin favorite to the corresponding Jellyseerr user account.
    /// Items whose Jellyfin user has no linked Jellyseerr account are skipped.
    /// </summary>
    public List<(JellyseerrUser jellyseerrUser, IJellyfinItem item)> EnsureJellyseerrUser(
        List<(JellyfinUser user, IJellyfinItem item)> favorites,
        List<JellyseerrUser> jellyseerrUsers)
    {
        var lookup = jellyseerrUsers
            .Where(u => !string.IsNullOrEmpty(u.JellyfinUserGuid))
            .ToDictionary(u => u.JellyfinUserGuid!, u => u);

        var result = new List<(JellyseerrUser, IJellyfinItem)>();

        foreach (var (jfUser, item) in favorites)
        {
            if (!lookup.TryGetValue(jfUser.Id.ToString(), out var jsUser))
            {
                _logger.LogWarning(
                    "Jellyfin user '{Username}' (ID: {Id}) has no linked Jellyseerr account – skipping",
                    jfUser.Username, jfUser.Id);
                continue;
            }
            result.Add((jsUser, item));
        }

        return result;
    }

    /// <summary>
    /// Creates Jellyseerr requests for all supplied (user, item) pairs.
    /// </summary>
    public async Task<(
        List<(IJellyfinItem item, JellyseerrMediaRequest request)> processed,
        List<IJellyfinItem> blocked)>
        RequestFavorites(List<(JellyseerrUser user, IJellyfinItem item)> favoritesWithUser)
    {
        var results = new List<(IJellyfinItem, JellyseerrMediaRequest)>();
        var blocked = new List<IJellyfinItem>();
        var processed = new HashSet<Guid>();

        // Randomise to spread user assignments across items.
        var shuffled = favoritesWithUser
            .Where(f => f.item != null)
            .OrderBy(_ => Guid.NewGuid())
            .ToList();

        foreach (var (user, item) in shuffled)
        {
            if (processed.Contains(item.Id)) continue;

            var tmdbId = item.GetTmdbId();
            if (!tmdbId.HasValue)
            {
                _logger.LogWarning("Skipping '{ItemName}' – no TMDB ID", item.Name);
                continue;
            }

            var mediaType = GetMediaType(item).ToString().ToLower();

            try
            {
                var reqParams = new Dictionary<string, object>
                {
                    ["mediaType"] = mediaType,
                    ["mediaId"] = tmdbId.Value,
                    ["userId"] = user.Id,
                };

                _logger.LogDebug("Requesting '{ItemName}' (TMDB {TmdbId}) for user '{UserName}'",
                    item.Name, tmdbId.Value, user.JellyfinUsername ?? user.Username ?? "Unknown");

                var raw = await _apiService.CallEndpointAsync(JellyseerrEndpoint.CreateRequest, parameters: reqParams);
                var request = raw as JellyseerrMediaRequest;

                if (request != null && request.Id != 0)
                {
                    processed.Add(item.Id);
                    results.Add((item, request));
                    _logger.LogDebug("Request created for '{ItemName}'", item.Name);
                }
                else if (raw == null)
                {
                    // null = 409 already exists in Jellyseerr – not a failure
                    _logger.LogDebug("'{ItemName}' already requested in Jellyseerr – skipping", item.Name);
                    processed.Add(item.Id);
                }
                else
                {
                    // HTTP 2xx was received (no exception thrown) so Jellyseerr created the request –
                    // we just couldn't parse the response ID. Treat as success.
                    _logger.LogDebug("Request created for '{ItemName}' (response Id unreadable, treating as success)", item.Name);
                    processed.Add(item.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create request for '{ItemName}'", item.Name);
                blocked.Add(item);
            }
        }

        _logger.LogDebug("RequestFavorites complete: {Created} created, {Blocked} blocked",
            results.Count, blocked.Count);
        return (results, blocked);
    }

    /// <summary>
    /// Optionally unfavorites items that were successfully requested in Jellyseerr,
    /// depending on the <see cref="PluginConfiguration.RemoveRequestedFromFavorites"/> setting.
    /// </summary>
    public async Task<List<IJellyfinItem>> ClearRequestedFavoritesAsync(
        List<(JellyfinUser user, IJellyfinItem item)> requestedFavorites)
    {
        var cleared = new List<IJellyfinItem>();
        var removeRequested = Plugin.GetConfigOrDefault<bool>(nameof(PluginConfiguration.RemoveRequestedFromFavorites));
        if (!removeRequested) return cleared;

        var tasks = requestedFavorites.Select(async fav =>
        {
            try
            {
                var unfavorited = await _userDataManager.TryUnfavoriteAsync(_libraryManager, fav.user, fav.item);
                if (unfavorited)
                {
                    _logger.LogTrace("Unfavorited '{ItemName}' for user '{UserName}'",
                        fav.item.Name, fav.user.Username);
                    return fav.item;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to unfavorite '{ItemName}'", fav.item?.Name);
            }
            return null;
        });

        var results = await Task.WhenAll(tasks);
        cleared.AddRange(results.Where(r => r != null)!);
        return cleared;
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static MediaType GetMediaType(IJellyfinItem item) => item switch
    {
        JellyfinMovie => MediaType.MOVIE,
        JellyfinSeries => MediaType.TV,
        _ => throw new NotSupportedException($"Unsupported item type: {item.GetType().Name}")
    };
}
