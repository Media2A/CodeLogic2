namespace CodeLogic.Abstractions;

/// <summary>
/// Interface for library discovery providers that can locate and discover libraries from various sources
/// (filesystem, database, network, etc.)
/// </summary>
public interface ILibraryDiscoveryProvider
{
    /// <summary>
    /// Discover available libraries from the provider's source
    /// </summary>
    /// <returns>Collection of discovered library results</returns>
    Task<IEnumerable<LibraryDiscoveryResult>> DiscoverLibrariesAsync();
}

/// <summary>
/// Result of a library discovery operation
/// </summary>
public class LibraryDiscoveryResult
{
    /// <summary>
    /// Library identifier (from ILibraryManifest.Id)
    /// </summary>
    public string LibraryId { get; set; } = string.Empty;

    /// <summary>
    /// Full path to the library assembly
    /// </summary>
    public string AssemblyPath { get; set; } = string.Empty;

    /// <summary>
    /// Optional metadata about the discovered library
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// The discovered ILibrary instance (if already instantiated during discovery)
    /// </summary>
    public ILibrary? LibraryInstance { get; set; }
}
