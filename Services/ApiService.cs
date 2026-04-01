using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Collections;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.SeerrRequestFav.Configuration;
using Jellyfin.Plugin.SeerrRequestFav.JellyseerrModel;
using Jellyfin.Plugin.SeerrRequestFav.JellyseerrModel.Api;
using Jellyfin.Plugin.SeerrRequestFav.BridgeModels;
using Jellyfin.Plugin.SeerrRequestFav.Utils;

namespace Jellyfin.Plugin.SeerrRequestFav.Services;

/// <summary>
/// Service for interacting with the Jellyseerr API.
/// </summary>
public class ApiService
{
    public const int MAX_SAFE_INTEGER = int.MaxValue;
    public const int MAX_PAGES = int.MaxValue;

    private readonly HttpClient _httpClient;
    private readonly DebugLogger<ApiService> _logger;

    public ApiService(HttpClient httpClient, ILogger<ApiService> logger)
    {
        _httpClient = httpClient;
        _logger = new DebugLogger<ApiService>(logger);
    }

    #region Endpoint Configuration

    private class JellyseerrEndpointConfig
    {
        public string Path { get; set; } = string.Empty;
        public HttpMethod Method { get; set; } = HttpMethod.Get;
        public Type ResponseModel { get; set; } = typeof(object);
        public Type ReturnModel { get; set; } = typeof(object);
        public bool IsPaginated { get; set; }
        public int? MaxPages { get; set; }

        public JellyseerrEndpointConfig(
            string path,
            Type responseModel,
            Type? returnModel = null,
            HttpMethod? method = null,
            bool isPaginated = false,
            int? maxPages = null)
        {
            Path = path;
            ResponseModel = responseModel;
            ReturnModel = returnModel ?? responseModel;
            Method = method ?? HttpMethod.Get;
            IsPaginated = isPaginated;
            MaxPages = maxPages;
        }
    }

    private static readonly Dictionary<JellyseerrEndpoint, JellyseerrEndpointConfig> _endpointConfigs = new()
    {
        [JellyseerrEndpoint.Status] = new JellyseerrEndpointConfig(
            "/api/v1/status",
            typeof(SystemStatus)
        ),
        [JellyseerrEndpoint.ReadRequests] = new JellyseerrEndpointConfig(
            "/api/v1/request",
            typeof(JellyseerrPaginatedResponse<JellyseerrMediaRequest>),
            returnModel: typeof(List<JellyseerrMediaRequest>),
            isPaginated: true
        ),
        [JellyseerrEndpoint.CreateRequest] = new JellyseerrEndpointConfig(
            "/api/v1/request",
            typeof(JellyseerrMediaRequest),
            method: HttpMethod.Post
        ),
        [JellyseerrEndpoint.AuthMe] = new JellyseerrEndpointConfig(
            "/api/v1/auth/me",
            typeof(JellyseerrUser)
        ),
        [JellyseerrEndpoint.UserList] = new JellyseerrEndpointConfig(
            "/api/v1/user",
            typeof(JellyseerrPaginatedResponse<JellyseerrUser>),
            returnModel: typeof(List<JellyseerrUser>),
            isPaginated: true
        ),
    };

    private static JellyseerrEndpointConfig GetEndpoint(JellyseerrEndpoint endpoint)
        => _endpointConfigs.TryGetValue(endpoint, out var config)
            ? config
            : new JellyseerrEndpointConfig("/", typeof(object));

    #endregion

    #region URL Builder

    private static class JellyseerrUrlBuilder
    {
        private static string BuildUrl(string baseUrl, JellyseerrEndpoint endpoint, Dictionary<string, object>? parameters = null)
        {
            var url = $"{baseUrl.TrimEnd('/')}{GetEndpoint(endpoint).Path}";

            if (parameters != null && parameters.Count > 0)
            {
                var qs = string.Join("&", parameters.Select(kvp =>
                    $"{kvp.Key}={Uri.EscapeDataString(kvp.Value?.ToString() ?? "")}"));
                url = $"{url}?{qs}";
            }

            return url;
        }

        public static HttpRequestMessage BuildEndpointRequest(
            string baseUrl,
            JellyseerrEndpoint endpoint,
            string apiKey,
            Dictionary<string, object>? parameters = null)
        {
            var config = GetEndpoint(endpoint);
            var isBodyMethod = config.Method == HttpMethod.Post || config.Method == HttpMethod.Put;

            var url = BuildUrl(baseUrl, endpoint, !isBodyMethod ? parameters : null);
            var requestMessage = new HttpRequestMessage(config.Method, url);
            requestMessage.Headers.Add("X-Api-Key", apiKey);
            requestMessage.Headers.Add("Accept", "application/json");

            if (isBodyMethod && parameters != null)
            {
                var json = JsonSerializer.Serialize(parameters);
                requestMessage.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            return requestMessage;
        }
    }

    #endregion

    #region Core API Call

    /// <summary>
    /// Calls a Jellyseerr API endpoint, handling pagination automatically.
    /// </summary>
    public async Task<object> CallEndpointAsync(
        JellyseerrEndpoint endpoint,
        PluginConfiguration? config = null,
        Dictionary<string, object>? parameters = null)
    {
        config ??= Plugin.GetConfiguration();
        string? content = null;
        var operationName = endpoint.ToString();

        try
        {
            var endpointConfig = GetEndpoint(endpoint);
            var (queryParams, _) = HandleEndpointSpecificLogic(endpoint, config);

            // Merge caller-supplied parameters (caller wins on conflict).
            queryParams = queryParams?
                .Concat(parameters ?? Enumerable.Empty<KeyValuePair<string, object>>())
                .GroupBy(kvp => kvp.Key)
                .ToDictionary(g => g.Key, g => g.Last().Value)
                ?? parameters;

            if (endpointConfig.IsPaginated)
            {
                var maxPages = endpointConfig.MaxPages ?? MAX_PAGES;
                var responseModelType = endpointConfig.ResponseModel;
                var itemType = responseModelType.GetGenericArguments()[0];
                var listType = typeof(List<>).MakeGenericType(itemType);
                var allItems = (IList)Activator.CreateInstance(listType)!;

                int page = 1;
                do
                {
                    var pageParameters = queryParams != null
                        ? new Dictionary<string, object>(queryParams) { ["page"] = page }
                        : new Dictionary<string, object> { ["page"] = page };
                    // If take is present, send it as-is without a page number (one large fetch)
                    if (queryParams != null && queryParams.ContainsKey("take"))
                        pageParameters = queryParams;

                    var req = JellyseerrUrlBuilder.BuildEndpointRequest(
                        config.JellyseerrUrl, endpoint, config.ApiKey, pageParameters);
                    content = await MakeApiRequestAsync(req, config);
                    if (content == null) break;

                    var pageResponse = SeerrRequestFavJsonSerializer.Deserialize(content, endpointConfig.ResponseModel);
                    if (pageResponse == null) break;

                    var resultsProperty = pageResponse.GetType().GetProperty("Results");
                    var pageInfoProperty = pageResponse.GetType().GetProperty("PageInfo");
                    var pageInfo = pageInfoProperty?.GetValue(pageResponse) as BridgeModels.PageInfo;

                    if (resultsProperty?.GetValue(pageResponse) is IEnumerable resultsEnum)
                    {
                        var itemCount = 0;
                        foreach (var item in resultsEnum)
                        {
                            allItems.Add(item);
                            itemCount++;
                        }
                        if (itemCount == 0) break;
                        // Use pageInfo.Pages when available — prevents infinite loops
                        // when Jellyseerr returns the same page repeatedly (e.g. huge take value).
                        if (pageInfo != null && page >= pageInfo.Pages) break;
                    }
                    else
                    {
                        break;
                    }
                    page++;
                } while (page <= maxPages);

                return allItems ?? GetDefaultReturn(endpoint);
            }
            else
            {
                var req = JellyseerrUrlBuilder.BuildEndpointRequest(
                    config.JellyseerrUrl, endpoint, config.ApiKey, queryParams);
                content = await MakeApiRequestAsync(req, config);
                if (content == null) return GetDefaultReturn(endpoint);

                _logger.LogTrace("Raw response for {Operation}: {Content}", operationName, content);
                var response = SeerrRequestFavJsonSerializer.Deserialize(content, endpointConfig.ResponseModel);
                return response ?? GetDefaultReturn(endpoint);
            }
        }
        catch (JsonException jsonEx)
        {
            _logger.LogWarning(jsonEx, "Failed to deserialize {Operation} response. Content: {Content}", operationName, content);
            return GetDefaultReturn(endpoint);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call {Operation}", endpoint);
            return GetDefaultReturn(endpoint);
        }
    }

    private static (Dictionary<string, object>? queryParameters, object? unused) HandleEndpointSpecificLogic(
        JellyseerrEndpoint endpoint, PluginConfiguration config)
    {
        return endpoint switch
        {
            JellyseerrEndpoint.CreateRequest => (
                new Dictionary<string, object>
                {
                    ["mediaType"] = "",
                    ["mediaId"] = -1,
                    ["seasons"] = Plugin.GetConfigOrDefault<bool>(nameof(PluginConfiguration.RequestFirstSeason), config)
                        ? new List<int> { 1 } : (object)"all",
                    ["userId"] = 0,
                    ["is4k"] = false
                }, null),
            JellyseerrEndpoint.ReadRequests => (
                new Dictionary<string, object>
                {
                    ["take"] = MAX_SAFE_INTEGER,
                    ["mediaType"] = "all"
                }, null),
            // UserList: use take to fetch all users in one request (no page param — matches JellyBridge)
            JellyseerrEndpoint.UserList => (
                new Dictionary<string, object> { ["take"] = MAX_SAFE_INTEGER }, null),
            _ => (null, null)
        };
    }

    private object GetDefaultReturn(JellyseerrEndpoint endpoint)
    {
        var returnType = GetEndpoint(endpoint).ReturnModel;
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(List<>))
            return Activator.CreateInstance(returnType) ?? new List<object>();
        return Activator.CreateInstance(returnType) ?? new object();
    }

    #endregion

    #region HTTP

    /// <summary>
    /// Makes an HTTP request with retry/timeout logic.
    /// </summary>
    private async Task<string?> MakeApiRequestAsync(HttpRequestMessage requestMessage, PluginConfiguration config)
    {
        var requestTimeout = Plugin.GetConfigOrDefault<int>(nameof(PluginConfiguration.RequestTimeout), config);
        var retryAttempts = Plugin.GetConfigOrDefault<int>(nameof(PluginConfiguration.RetryAttempts), config);
        var url = requestMessage.RequestUri?.ToString() ?? "";

        for (int attempt = 1; attempt <= retryAttempts; attempt++)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(requestTimeout));
            HttpRequestMessage req = attempt == 1
                ? requestMessage
                : CloneRequest(requestMessage);

            try
            {
                _logger.LogDebug("API request attempt {Attempt}/{Max}: {Method} {Url}",
                    attempt, retryAttempts, req.Method, url);

                var response = await _httpClient.SendAsync(req, cts.Token).ConfigureAwait(false);

                // 409 Conflict = already requested in Jellyseerr – not an error, just skip.
                if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    _logger.LogDebug("Jellyseerr returned 409 (already exists) for {Url} – skipping", url);
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"Jellyseerr returned {(int)response.StatusCode} for {url}",
                        null,
                        response.StatusCode);
                }

                var content = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                _logger.LogDebug("API request succeeded: {Method} {Url}", req.Method, url);
                return content;
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                _logger.LogWarning("API request timed out (attempt {Attempt}/{Max}): {Url}", attempt, retryAttempts, url);
                if (attempt == retryAttempts)
                    throw new TimeoutException($"Request to {url} timed out after {retryAttempts} attempts");
            }
            catch (HttpRequestException)
            {
                throw;  // Don't retry HTTP errors (non-2xx): surface to caller.
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "API request failed (attempt {Attempt}/{Max}): {Url}", attempt, retryAttempts, url);
                if (attempt == retryAttempts) throw;
                var backoff = Math.Min(5 * attempt, 60);
                await Task.Delay(TimeSpan.FromSeconds(backoff)).ConfigureAwait(false);
            }
        }

        return null;
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);
        foreach (var header in original.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        if (original.Content != null)
        {
            var body = original.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            clone.Content = new StringContent(body,
                System.Text.Encoding.UTF8,
                original.Content.Headers.ContentType?.MediaType ?? "application/json");
        }
        return clone;
    }

    #endregion

    #region Test Connection

    /// <summary>
    /// Tests connectivity to Jellyseerr and validates the API key.
    /// </summary>
    /// <param name="connectionTimeoutSeconds">Override request timeout. 0 = use plugin config value.</param>
    /// <param name="maxRetries">Override retry attempts. 0 = use plugin config value.</param>
    public async Task<SystemStatus> TestConnectionAsync(
        string? jellyseerUrl = null,
        string? apiKey = null,
        int connectionTimeoutSeconds = 0,
        int maxRetries = 0)
    {
        jellyseerUrl ??= Plugin.GetConfigOrDefault<string>(nameof(PluginConfiguration.JellyseerrUrl));
        apiKey ??= Plugin.GetConfigOrDefault<string>(nameof(PluginConfiguration.ApiKey));

        var testConfig = new PluginConfiguration
        {
            JellyseerrUrl = jellyseerUrl,
            ApiKey = apiKey,
            RequestTimeout = connectionTimeoutSeconds > 0 ? connectionTimeoutSeconds : null,
            RetryAttempts = maxRetries > 0 ? maxRetries : null
        };

        var statusRequest = JellyseerrUrlBuilder.BuildEndpointRequest(
            jellyseerUrl, JellyseerrEndpoint.Status, apiKey);
        var statusContent = await MakeApiRequestAsync(statusRequest, testConfig);

        var status = SeerrRequestFavJsonSerializer.Deserialize<SystemStatus>(statusContent!);
        if (status == null || string.IsNullOrEmpty(status.Version))
        {
            throw new HttpRequestException(
                $"Jellyseerr returned empty status response from {statusRequest.RequestUri}",
                null,
                System.Net.HttpStatusCode.BadGateway);
        }

        var authRequest = JellyseerrUrlBuilder.BuildEndpointRequest(
            jellyseerUrl, JellyseerrEndpoint.AuthMe, apiKey);
        var authContent = await MakeApiRequestAsync(authRequest, testConfig);

        var userInfo = SeerrRequestFavJsonSerializer.Deserialize<JellyseerrUser>(authContent!);
        if (userInfo == null || userInfo.Id == 0)
        {
            throw new HttpRequestException(
                $"Jellyseerr API key validation failed for {authRequest.RequestUri}",
                null,
                System.Net.HttpStatusCode.Unauthorized);
        }

        return status;
    }

    #endregion
}
