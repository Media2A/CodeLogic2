using System.Reflection;
using CodeLogic.Abstractions;

namespace CodeLogic.Discovery;

/// <summary>
/// Generic filesystem-based library discovery provider.
/// Scans a specified directory for assemblies matching a pattern and discovers ILibrary implementations.
/// </summary>
public class FileSystemLibraryDiscoveryProvider : ILibraryDiscoveryProvider
{
    private readonly string _searchPath;
    private readonly string _searchPattern;
    private readonly ILogger? _logger;

    /// <summary>
    /// Create a new filesystem library discovery provider
    /// </summary>
    /// <param name="searchPath">Directory path to search for libraries</param>
    /// <param name="searchPattern">File pattern to match (e.g., "*.dll", "MyApp.Plugins.*.dll")</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    public FileSystemLibraryDiscoveryProvider(string searchPath, string searchPattern = "*.dll", ILogger? logger = null)
    {
        _searchPath = searchPath ?? throw new ArgumentNullException(nameof(searchPath));
        _searchPattern = searchPattern ?? throw new ArgumentNullException(nameof(searchPattern));
        _logger = logger;
    }

    /// <summary>
    /// Discover libraries from the filesystem
    /// </summary>
    public async Task<IEnumerable<LibraryDiscoveryResult>> DiscoverLibrariesAsync()
    {
        var results = new List<LibraryDiscoveryResult>();

        _logger?.Info($"Discovering libraries in: {_searchPath} (pattern: {_searchPattern})");

        // Check if directory exists
        if (!Directory.Exists(_searchPath))
        {
            _logger?.Warning($"Discovery path does not exist: {_searchPath}");
            return results;
        }

        try
        {
            // Find all matching DLL files
            var assemblyFiles = Directory.GetFiles(_searchPath, _searchPattern, SearchOption.TopDirectoryOnly);

            _logger?.Info($"Found {assemblyFiles.Length} assembly file(s) matching pattern");

            foreach (var assemblyPath in assemblyFiles)
            {
                try
                {
                    _logger?.Debug($"Examining assembly: {Path.GetFileName(assemblyPath)}");

                    // Load the assembly
                    var assembly = Assembly.LoadFrom(assemblyPath);

                    // Find all types that implement ILibrary
                    var libraryTypes = assembly.GetTypes()
                        .Where(t => typeof(ILibrary).IsAssignableFrom(t)
                                 && !t.IsInterface
                                 && !t.IsAbstract
                                 && t.GetConstructor(Type.EmptyTypes) != null); // Must have parameterless constructor

                    foreach (var libraryType in libraryTypes)
                    {
                        try
                        {
                            // Create an instance to get the manifest
                            var instance = Activator.CreateInstance(libraryType) as ILibrary;
                            if (instance?.Manifest == null)
                            {
                                _logger?.Warning($"Library type {libraryType.Name} has null manifest, skipping");
                                continue;
                            }

                            var result = new LibraryDiscoveryResult
                            {
                                LibraryId = instance.Manifest.Id,
                                AssemblyPath = assemblyPath,
                                LibraryInstance = instance,
                                Metadata = new Dictionary<string, object>
                                {
                                    ["TypeName"] = libraryType.FullName ?? libraryType.Name,
                                    ["AssemblyName"] = assembly.GetName().Name ?? "Unknown",
                                    ["DiscoveryMethod"] = "FileSystem",
                                    ["SearchPath"] = _searchPath
                                }
                            };

                            results.Add(result);

                            _logger?.Info($"Discovered library: {instance.Manifest.Id} v{instance.Manifest.Version} ({libraryType.Name})");
                        }
                        catch (Exception ex)
                        {
                            _logger?.Error($"Failed to instantiate library type {libraryType.Name}", ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Warning($"Failed to examine assembly {Path.GetFileName(assemblyPath)}: {ex.Message}");
                }
            }

            _logger?.Info($"Discovery complete: {results.Count} library/libraries discovered");
        }
        catch (Exception ex)
        {
            _logger?.Error($"Library discovery failed", ex);
        }

        return await Task.FromResult(results);
    }
}
