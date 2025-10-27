namespace CodeLogic.Abstractions;

/// <summary>
/// Extension methods for ILogger to provide convenient logging with context
/// </summary>
public static class LoggerExtensions
{
    /// <summary>
    /// Logs an informational message with context
    /// </summary>
    public static void LogInfo(this ILogger logger, string context, string message, params object[] args)
    {
        string formattedMessage = $"[{context}] {message}";
        logger.Info(formattedMessage, args);
    }

    /// <summary>
    /// Logs a success message (as Info level) with context
    /// </summary>
    public static void LogSuccess(this ILogger logger, string context, string message, params object[] args)
    {
        string formattedMessage = $"[{context}] ✓ {message}";
        logger.Info(formattedMessage, args);
    }

    /// <summary>
    /// Logs a warning message with context
    /// </summary>
    public static void LogWarning(this ILogger logger, string context, string message, params object[] args)
    {
        string formattedMessage = $"[{context}] {message}";
        logger.Warning(formattedMessage, args);
    }

    /// <summary>
    /// Logs an error message with context
    /// </summary>
    public static void LogError(this ILogger logger, string context, string message, Exception? exception = null, params object[] args)
    {
        string formattedMessage = $"[{context}] {message}";
        logger.Error(formattedMessage, exception, args);
    }

    /// <summary>
    /// Logs a fatal message with context
    /// </summary>
    public static void LogFatal(this ILogger logger, string context, string message, Exception? exception = null, params object[] args)
    {
        string formattedMessage = $"[{context}] {message}";
        logger.Fatal(formattedMessage, exception, args);
    }

    /// <summary>
    /// Logs a debug message with context
    /// </summary>
    public static void LogDebug(this ILogger logger, string context, string message, params object[] args)
    {
        string formattedMessage = $"[{context}] {message}";
        logger.Debug(formattedMessage, args);
    }

    /// <summary>
    /// Logs a trace message with context
    /// </summary>
    public static void LogTrace(this ILogger logger, string context, string message, params object[] args)
    {
        string formattedMessage = $"[{context}] {message}";
        logger.Trace(formattedMessage, args);
    }
}
