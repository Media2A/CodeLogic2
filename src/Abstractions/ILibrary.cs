namespace CodeLogic.Abstractions;

/// <summary>
/// Base interface for all CodeLogic libraries
/// </summary>
public interface ILibrary
{
    /// <summary>
    /// Gets the library manifest containing metadata and dependencies
    /// </summary>
    ILibraryManifest Manifest { get; }

    /// <summary>
    /// Called when the library is loaded into memory
    /// </summary>
    /// <param name="context">Context containing framework services and configuration</param>
    Task OnLoadAsync(LibraryContext context);

    /// <summary>
    /// Called after all dependencies are loaded and the library should initialize
    /// </summary>
    Task OnInitializeAsync();

    /// <summary>
    /// Called when the library is being unloaded
    /// </summary>
    Task OnUnloadAsync();

    /// <summary>
    /// Health check to verify the library is functioning correctly
    /// </summary>
    Task<HealthCheckResult> HealthCheckAsync();
}

/// <summary>
/// Context provided to libraries during lifecycle operations
/// </summary>
public class LibraryContext
{
    /// <summary>
    /// Service provider for dependency injection
    /// </summary>
    public required IServiceProvider Services { get; init; }

    /// <summary>
    /// Configuration values specific to this library
    /// </summary>
    public required IReadOnlyDictionary<string, object> Configuration { get; init; }

    /// <summary>
    /// Logger instance for this library
    /// </summary>
    public required ILogger Logger { get; init; }

    /// <summary>
    /// Data directory for this library
    /// </summary>
    public required string DataDirectory { get; init; }
}

/// <summary>
/// Result of a health check operation
/// </summary>
public record HealthCheckResult
{
    /// <summary>
    /// Whether the health check passed
    /// </summary>
    public required bool IsHealthy { get; init; }

    /// <summary>
    /// Optional message providing details
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Optional exception if the check failed
    /// </summary>
    public Exception? Exception { get; init; }

    public static HealthCheckResult Healthy(string? message = null) =>
        new() { IsHealthy = true, Message = message };

    public static HealthCheckResult Unhealthy(string message, Exception? exception = null) =>
        new() { IsHealthy = false, Message = message, Exception = exception };
}
