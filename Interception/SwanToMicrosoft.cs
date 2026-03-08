using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Swan.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using LogLevel = Swan.Logging.LogLevel;

namespace OpenShock.Desktop.Modules.Interception;

public sealed class SwanToMicrosoft : Swan.Logging.ILogger
{
    private readonly ILoggerFactory _factory;
    private readonly ConcurrentDictionary<string, ILogger> _loggers = new();

    public SwanToMicrosoft(ILoggerFactory factory)
    {
        _factory = factory;
    }

    public void Dispose()
    {
    }

    public void Log(LogMessageReceivedEventArgs logEvent)
    {
        var loggerKey = logEvent.Source ?? "SWAN";
        var logger = _loggers.GetOrAdd(loggerKey, static (key, fact) => fact.CreateLogger(key), _factory);
        var mappedLevel = MapLogLevel(logEvent.MessageType);

        if (!logger.IsEnabled(mappedLevel)) return;

#pragma warning disable CA2254
        logger.Log(mappedLevel, logEvent.Message);
#pragma warning restore CA2254
    }

    public LogLevel LogLevel => LogLevel.Trace;

    private Microsoft.Extensions.Logging.LogLevel MapLogLevel(LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => Microsoft.Extensions.Logging.LogLevel.Trace,
            LogLevel.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
            LogLevel.Info => Microsoft.Extensions.Logging.LogLevel.Information,
            LogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
            LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
            LogLevel.Fatal => Microsoft.Extensions.Logging.LogLevel.Critical,
            _ => Microsoft.Extensions.Logging.LogLevel.None
        };
    }
}