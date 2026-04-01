using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.SeerrRequestFav.Configuration;

namespace Jellyfin.Plugin.SeerrRequestFav.Utils;

/// <summary>
/// Logger wrapper that routes debug/trace messages to Information level when the
/// corresponding debug/trace logging flags are enabled in plugin config.
/// </summary>
public class DebugLogger<T> : ILogger<T>
{
    private readonly ILogger<T> _innerLogger;

    public DebugLogger(ILogger<T> logger)
    {
        _innerLogger = logger;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        => _innerLogger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => _innerLogger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => _innerLogger.Log(logLevel, eventId, state, exception, formatter);

    public void LogInformation(string message, params object?[] args)
        => _innerLogger.LogInformation(message, args);

    public void LogInformation(Exception? exception, string message, params object?[] args)
        => _innerLogger.LogInformation(exception, message, args);

    public void LogWarning(string message, params object?[] args)
        => _innerLogger.LogWarning(message, args);

    public void LogWarning(Exception? exception, string message, params object?[] args)
        => _innerLogger.LogWarning(exception, message, args);

    public void LogError(string message, params object?[] args)
        => _innerLogger.LogError(message, args);

    public void LogError(Exception? exception, string message, params object?[] args)
        => _innerLogger.LogError(exception, message, args);

    public void LogDebug(string message, params object?[] args)
        => LogDebugInternal(null, message, args);

    public void LogDebug(Exception? exception, string message, params object?[] args)
        => LogDebugInternal(exception, message, args);

    public void LogTrace(string message, params object?[] args)
        => LogTraceInternal(null, message, args);

    public void LogTrace(Exception? exception, string message, params object?[] args)
        => LogTraceInternal(exception, message, args);

    private void LogDebugInternal(Exception? exception, string message, object?[] args)
    {
        var enableDebug = Plugin.GetConfigOrDefault<bool>(nameof(PluginConfiguration.EnableDebugLogging));
        if (enableDebug)
        {
            if (exception != null) _innerLogger.LogInformation(exception, "[DEBUG] " + message, args);
            else _innerLogger.LogInformation("[DEBUG] " + message, args);
        }
        else
        {
            if (exception != null) _innerLogger.LogDebug(exception, message, args);
            else _innerLogger.LogDebug(message, args);
        }
    }

    private void LogTraceInternal(Exception? exception, string message, object?[] args)
    {
        var config = Plugin.GetConfiguration();
        var enableTrace = Plugin.GetConfigOrDefault<bool>(nameof(PluginConfiguration.EnableTraceLogging), config);
        var enableDebug = Plugin.GetConfigOrDefault<bool>(nameof(PluginConfiguration.EnableDebugLogging), config);

        if (enableTrace && enableDebug)
        {
            var memberName = GetCallingMethodName();
            var prefix = !string.IsNullOrEmpty(memberName) ? $"[{memberName}] " : "";
            var prefixed = "[TRACE] " + prefix + message;
            if (exception != null) _innerLogger.LogInformation(exception, prefixed, args);
            else _innerLogger.LogInformation(prefixed, args);
        }
        else
        {
            if (exception != null) _innerLogger.LogTrace(exception, message, args);
            else _innerLogger.LogTrace(message, args);
        }
    }

    private static string? GetCallingMethodName()
    {
        try
        {
            var frames = new StackTrace().GetFrames();
            foreach (var frame in frames)
            {
                var method = frame.GetMethod();
                if (method == null) continue;
                var declaringType = method.DeclaringType;
                if (declaringType == null) continue;
                if (declaringType == typeof(DebugLogger<T>)) continue;
                return method.Name;
            }
        }
        catch { }
        return null;
    }
}
