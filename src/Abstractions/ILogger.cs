namespace CodeLogic.Abstractions;

/// <summary>
/// Abstraction for logging operations
/// </summary>
public interface ILogger
{
    /// <summary>
    /// Logs a message at the specified level
    /// </summary>
    void Log(LogLevel level, string message, Exception? exception = null, params object[] args);

    /// <summary>
    /// Logs a trace message (most verbose)
    /// </summary>
    void Trace(string message, params object[] args);

    /// <summary>
    /// Logs a debug message
    /// </summary>
    void Debug(string message, params object[] args);

    /// <summary>
    /// Logs an informational message
    /// </summary>
    void Info(string message, params object[] args);

    /// <summary>
    /// Logs a warning message
    /// </summary>
    void Warning(string message, params object[] args);

    /// <summary>
    /// Logs an error message
    /// </summary>
    void Error(string message, Exception? exception = null, params object[] args);

    /// <summary>
    /// Logs a fatal error message
    /// </summary>
    void Fatal(string message, Exception? exception = null, params object[] args);
}

/// <summary>
/// Logging levels
/// </summary>
public enum LogLevel
{
    Trace = 0,
    Debug = 1,
    Info = 2,
    Warning = 3,
    Error = 4,
    Fatal = 5
}
