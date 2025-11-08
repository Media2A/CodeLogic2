namespace CodeLogic.Models;

/// <summary>
/// Result of a library discovery and load operation
/// </summary>
public class DiscoveryLoadResult
{
    /// <summary>
    /// Total number of libraries discovered
    /// </summary>
    public int TotalDiscovered { get; set; }

    /// <summary>
    /// Number of libraries that were already loaded
    /// </summary>
    public int AlreadyLoaded { get; set; }

    /// <summary>
    /// Number of libraries successfully loaded
    /// </summary>
    public int SuccessfullyLoaded { get; set; }

    /// <summary>
    /// Number of libraries that failed to load
    /// </summary>
    public int Failed { get; set; }

    /// <summary>
    /// List of error messages for failed libraries
    /// </summary>
    public List<string> Errors { get; set; } = new();
}
