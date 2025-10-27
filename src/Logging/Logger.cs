using CodeLogic.Abstractions;
using CodeLogic.Internal;
using System.Collections.Concurrent;

namespace CodeLogic.Logging;

/// <summary>
/// File-based logger implementation with support for multiple log levels and structured output
/// </summary>
public class Logger : ILogger
{
    private readonly string _logDirectory;
    private readonly LogLevel _minimumLevel;
    private readonly string _loggerName;
    private static readonly ConcurrentQueue<LogEntry> _logQueue = new();
    private static readonly SemaphoreSlim _flushSemaphore = new(1, 1);
    private static readonly Timer _flushTimer;
    private static bool _autoFlushEnabled = true;

    static Logger()
    {
        // Auto-flush logs every 5 seconds
        _flushTimer = new Timer(async _ => await FlushAllAsync(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    public Logger(string logDirectory, string loggerName = "Application", LogLevel minimumLevel = LogLevel.Info)
    {
        _logDirectory = logDirectory;
        _loggerName = loggerName;
        _minimumLevel = minimumLevel;

        // Ensure log directory exists
        FileSystemHelper.EnsureDirectoryExists(_logDirectory);
    }

    public void Log(LogLevel level, string message, Exception? exception = null, params object[] args)
    {
        if (level < _minimumLevel)
            return;

        var entry = new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = level,
            LoggerName = _loggerName,
            LogDirectory = _logDirectory,
            Message = args.Length > 0 ? string.Format(message, args) : message,
            Exception = exception
        };

        _logQueue.Enqueue(entry);

        // Flush immediately for errors and fatal logs
        if (level >= LogLevel.Error && _autoFlushEnabled)
        {
            _ = Task.Run(async () => await FlushAllAsync());
        }
    }

    public void Trace(string message, params object[] args) => Log(LogLevel.Trace, message, null, args);

    public void Debug(string message, params object[] args) => Log(LogLevel.Debug, message, null, args);

    public void Info(string message, params object[] args) => Log(LogLevel.Info, message, null, args);

    public void Warning(string message, params object[] args) => Log(LogLevel.Warning, message, null, args);

    public void Error(string message, Exception? exception = null, params object[] args) =>
        Log(LogLevel.Error, message, exception, args);

    public void Fatal(string message, Exception? exception = null, params object[] args) =>
        Log(LogLevel.Fatal, message, exception, args);

    /// <summary>
    /// Flushes all pending log entries to disk
    /// </summary>
    public static async Task FlushAllAsync()
    {
        if (_logQueue.IsEmpty)
            return;

        await _flushSemaphore.WaitAsync();

        try
        {
            var entriesByFile = new Dictionary<string, List<LogEntry>>();

            // Group log entries by file
            while (_logQueue.TryDequeue(out var entry))
            {
                string fileName = GetLogFileName(entry.LogDirectory, entry.Timestamp, entry.Level);

                if (!entriesByFile.ContainsKey(fileName))
                {
                    entriesByFile[fileName] = new List<LogEntry>();
                }

                entriesByFile[fileName].Add(entry);
            }

            // Write all entries
            foreach (var kvp in entriesByFile)
            {
                string content = string.Join(Environment.NewLine, kvp.Value.Select(FormatLogEntry));
                await FileSystemHelper.WriteFileAsync(kvp.Key, content + Environment.NewLine, append: true);
            }
        }
        finally
        {
            _flushSemaphore.Release();
        }
    }

    /// <summary>
    /// Disables auto-flushing (for performance in high-throughput scenarios)
    /// </summary>
    public static void DisableAutoFlush() => _autoFlushEnabled = false;

    /// <summary>
    /// Enables auto-flushing
    /// </summary>
    public static void EnableAutoFlush() => _autoFlushEnabled = true;

    private static string GetLogFileName(string logDirectory, DateTime timestamp, LogLevel level)
    {
        string date = timestamp.ToString("yyyy-MM-dd");
        string levelStr = level.ToString().ToLower();
        return FileSystemHelper.NormalizePath(logDirectory, $"{date}_{levelStr}.log");
    }

    private static string FormatLogEntry(LogEntry entry)
    {
        var parts = new List<string>
        {
            entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            $"[{entry.Level.ToString().ToUpper()}]",
            $"[{entry.LoggerName}]",
            entry.Message
        };

        if (entry.Exception != null)
        {
            parts.Add($"\n  Exception: {entry.Exception.GetType().Name}: {entry.Exception.Message}");
            parts.Add($"\n  StackTrace: {entry.Exception.StackTrace}");

            if (entry.Exception.InnerException != null)
            {
                parts.Add($"\n  InnerException: {entry.Exception.InnerException.GetType().Name}: {entry.Exception.InnerException.Message}");
            }
        }

        return string.Join(" ", parts);
    }

    private class LogEntry
    {
        public DateTime Timestamp { get; init; }
        public LogLevel Level { get; init; }
        public string LoggerName { get; init; } = string.Empty;
        public string LogDirectory { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public Exception? Exception { get; init; }
    }
}

/// <summary>
/// Factory for creating loggers
/// </summary>
public class LoggerFactory
{
    private readonly string _logDirectory;
    private readonly LogLevel _minimumLevel;
    private readonly ConcurrentDictionary<string, ILogger> _loggers = new();

    public LoggerFactory(string logDirectory, LogLevel minimumLevel = LogLevel.Info)
    {
        _logDirectory = logDirectory;
        _minimumLevel = minimumLevel;
    }

    public ILogger CreateLogger(string name)
    {
        return _loggers.GetOrAdd(name, n => new Logger(_logDirectory, n, _minimumLevel));
    }

    public ILogger CreateLogger<T>()
    {
        return CreateLogger(typeof(T).Name);
    }
}
