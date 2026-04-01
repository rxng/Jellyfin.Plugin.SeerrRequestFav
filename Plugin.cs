using Jellyfin.Plugin.SeerrRequestFav.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Jellyfin.Plugin.SeerrRequestFav.Utils;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SeerrRequestFav
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        private readonly DebugLogger<Plugin> _logger;
        private readonly ILoggerFactory _loggerFactory;

        public override Guid Id => Guid.Parse("c1d2e3f4-a5b6-c7d8-e9f0-a1b2c3d4e5f6");
        public override string Name => "SeerrRequestFav";

        public static Plugin Instance { get; private set; } = null!;

        public ILoggerFactory LoggerFactory => _loggerFactory;

        // Locking: Only one operation (any name) can run at a time, with one queued per operation name.
        private static readonly object _operationSyncLock = new object();
        private static bool _isOperationRunning = false;
        private static readonly Dictionary<string, bool> _isOperationQueuedByName = new Dictionary<string, bool>();

        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, ILoggerFactory loggerFactory)
            : base(applicationPaths, xmlSerializer)
        {
            _loggerFactory = loggerFactory;
            _logger = new DebugLogger<Plugin>(loggerFactory.CreateLogger<Plugin>());
            Instance = this;

            _logger.LogInformation("SeerrRequestFav plugin initialized - Version {Version}", GetType().Assembly.GetName().Version);
        }

        /// <summary>
        /// Gets the current plugin configuration.
        /// </summary>
        public static PluginConfiguration GetConfiguration()
        {
            return Instance.Configuration ?? new PluginConfiguration();
        }

        public override void UpdateConfiguration(BasePluginConfiguration configuration)
        {
            _logger.LogTrace("Configuration update requested");
            var pluginConfig = (PluginConfiguration)configuration;
            base.UpdateConfiguration(pluginConfig);
            _logger.LogInformation("Configuration updated successfully");
        }

        /// <summary>
        /// Gets a configuration value or its default value.
        /// </summary>
        public static T GetConfigOrDefault<T>(string propertyName, PluginConfiguration? config = null) where T : notnull
        {
            config ??= GetConfiguration();
            var propertyInfo = typeof(PluginConfiguration).GetProperty(propertyName);
            var rawValue = propertyInfo?.GetValue(config);

            if (rawValue != null && rawValue is T t &&
                ((rawValue is not string) || (rawValue is string str && !string.IsNullOrEmpty(str))))
            {
                return t;
            }

            if (PluginConfiguration.DefaultValues?.TryGetValue(propertyName, out var defaultValue) == true)
            {
                return (T)defaultValue;
            }

            throw new InvalidOperationException($"Cannot provide default value for type {typeof(T)}");
        }

        /// <summary>
        /// Gets the plugin pages for the Jellyfin configuration UI.
        /// </summary>
        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = Name,
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.ConfigurationPage.html",
                },
                new PluginPageInfo
                {
                    Name = "ConfigurationPage.js",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.ConfigurationPage.js"
                }
            };
        }

        /// <summary>
        /// Checks if any operation is currently running.
        /// </summary>
        public static bool IsOperationRunning => _isOperationRunning;

        /// <summary>
        /// Executes an operation with locking that queues instead of canceling.
        /// Only one operation runs at a time; one queued per operation name.
        /// </summary>
        public static async Task<T> ExecuteWithLockAsync<T>(Func<Task<T>> operation, ILogger logger, string operationName, TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromMinutes(Plugin.GetConfigOrDefault<int>(nameof(PluginConfiguration.TaskTimeoutMinutes)));
            var startTime = DateTime.UtcNow;
            var isQueued = false;

            while (true)
            {
                lock (_operationSyncLock)
                {
                    if (!_isOperationRunning)
                    {
                        _isOperationRunning = true;
                        _isOperationQueuedByName[operationName] = false;
                        break;
                    }
                    else if (!isQueued && _isOperationQueuedByName.TryGetValue(operationName, out var queued))
                    {
                        if (!queued)
                        {
                            isQueued = true;
                            _isOperationQueuedByName[operationName] = true;
                        }
                        else
                        {
                            logger.LogWarning("Operation {OperationName} already queued, skipping duplicate.", operationName);
                            return default!;
                        }
                    }
                    else if (DateTime.UtcNow - startTime >= timeout.Value)
                    {
                        _isOperationQueuedByName[operationName] = false;
                        throw new TimeoutException($"Operation '{operationName}' timed out after {timeout.Value.TotalMinutes} minutes");
                    }
                }

                await Task.Delay(10000);
            }

            try
            {
                return await operation();
            }
            finally
            {
                lock (_operationSyncLock)
                {
                    _isOperationRunning = false;
                }
            }
        }
    }
}
