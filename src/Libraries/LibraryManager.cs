using CodeLogic.Abstractions;
using CodeLogic.Logging;
using CodeLogic.Models;
using System.Collections.Concurrent;
using System.Reflection;

namespace CodeLogic.Libraries;

/// <summary>
/// Manages the lifecycle of CodeLogic libraries including loading, dependency resolution, and unloading
/// </summary>
public class LibraryManager
{
    private readonly string _librariesDirectory;
    private readonly ConcurrentDictionary<string, LoadedLibrary> _loadedLibraries = new();
    private readonly ConcurrentDictionary<string, Assembly> _loadedAssemblies = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly string _dataDirectory;
    private readonly string _logDirectory;
    private readonly Action<string> _logOutput;

    public LibraryManager(string librariesDirectory, string dataDirectory, string logDirectory, IServiceProvider serviceProvider, Action<string>? logOutput = null)
    {
        _librariesDirectory = librariesDirectory;
        _dataDirectory = dataDirectory;
        _logDirectory = logDirectory;
        _serviceProvider = serviceProvider;
        _logOutput = logOutput ?? Console.WriteLine;

        Directory.CreateDirectory(_librariesDirectory);
        Directory.CreateDirectory(_dataDirectory);
    }

    /// <summary>
    /// Discovers all available libraries in the libraries directory
    /// </summary>
    public async Task<Result<List<LibraryInfo>>> DiscoverLibrariesAsync()
    {
        var libraries = new List<LibraryInfo>();

        try
        {
            // Look for library DLLs (starting with "CL." to avoid loading system/framework DLLs)
            var dllFiles = Directory.GetFiles(_librariesDirectory, "CL.*.dll", SearchOption.TopDirectoryOnly);

            foreach (var dllFile in dllFiles)
            {
                try
                {
                    var assembly = Assembly.LoadFrom(dllFile);
                    var libraryTypes = assembly.GetTypes()
                        .Where(t => typeof(ILibrary).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                    foreach (var type in libraryTypes)
                    {
                        var library = (ILibrary?)Activator.CreateInstance(type);
                        if (library != null)
                        {
                            libraries.Add(new LibraryInfo
                            {
                                Manifest = library.Manifest,
                                AssemblyPath = dllFile,
                                TypeName = type.FullName!
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log but continue discovering other libraries
                    _logOutput($"  ⚠ Warning: Could not load library from {Path.GetFileName(dllFile)}: {ex.Message}");
                }
            }

            return Result<List<LibraryInfo>>.Success(libraries);
        }
        catch (Exception ex)
        {
            return Result<List<LibraryInfo>>.Failure($"Failed to discover libraries: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Loads a library with dependency resolution
    /// </summary>
    public async Task<Result> LoadLibraryAsync(string libraryId)
    {
        try
        {
            // Check if already loaded
            if (_loadedLibraries.ContainsKey(libraryId))
            {
                _logOutput($"  ℹ Library '{libraryId}' is already loaded");
                return Result.Success();
            }

            // Discover available libraries
            var discoveryResult = await DiscoverLibrariesAsync();
            if (!discoveryResult.IsSuccess || discoveryResult.Value == null)
                return Result.Failure($"Failed to discover libraries: {discoveryResult.ErrorMessage}");

            var libraryInfo = discoveryResult.Value.FirstOrDefault(e => e.Manifest.Id == libraryId);
            if (libraryInfo == null)
                return Result.Failure($"Library '{libraryId}' not found");

            _logOutput($"  → Loading {libraryInfo.Manifest.Name} v{libraryInfo.Manifest.Version}...");

            // Validate dependencies
            var depValidation = await ValidateDependenciesAsync(libraryInfo.Manifest);
            if (!depValidation.IsSuccess)
                return depValidation;

            // Load dependencies first
            foreach (var dependency in libraryInfo.Manifest.Dependencies.Where(d => !d.IsOptional))
            {
                if (!_loadedLibraries.ContainsKey(dependency.Id))
                {
                    var depResult = await LoadLibraryAsync(dependency.Id);
                    if (!depResult.IsSuccess)
                        return Result.Failure($"Failed to load dependency '{dependency.Id}': {depResult.ErrorMessage}");
                }
            }

            // Load the assembly if not already loaded
            if (!_loadedAssemblies.ContainsKey(libraryInfo.AssemblyPath))
            {
                var assembly = Assembly.LoadFrom(libraryInfo.AssemblyPath);
                _loadedAssemblies[libraryInfo.AssemblyPath] = assembly;
            }

            // Create instance
            var assembly2 = _loadedAssemblies[libraryInfo.AssemblyPath];
            var type = assembly2.GetType(libraryInfo.TypeName);
            if (type == null)
                return Result.Failure($"Type '{libraryInfo.TypeName}' not found in assembly");

            var library = (ILibrary?)Activator.CreateInstance(type);
            if (library == null)
                return Result.Failure($"Failed to create instance of library '{libraryId}'");

            // Create library context
            var libraryLogger = new Logger(_logDirectory, libraryInfo.Manifest.Name);
            var context = new LibraryContext
            {
                Services = _serviceProvider,
                Configuration = new Dictionary<string, object>(), // Load from config
                Logger = libraryLogger,
                DataDirectory = Path.Combine(_dataDirectory, libraryId)
            };

            Directory.CreateDirectory(context.DataDirectory);

            // Call lifecycle hooks
            await library.OnLoadAsync(context);
            await library.OnInitializeAsync();

            // Store loaded library
            _loadedLibraries[libraryId] = new LoadedLibrary
            {
                Info = libraryInfo,
                Instance = library,
                Context = context,
                LoadedAt = DateTime.UtcNow
            };

            _logOutput($"  ✓ {libraryInfo.Manifest.Name} loaded successfully");

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logOutput($"  ✗ Failed to load library '{libraryId}': {ex.Message}");
            return Result.Failure($"Failed to load library '{libraryId}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Unloads a library
    /// </summary>
    public async Task<Result> UnloadLibraryAsync(string libraryId)
    {
        try
        {
            if (!_loadedLibraries.TryGetValue(libraryId, out var loadedLibrary))
                return Result.Failure($"Library '{libraryId}' is not loaded");

            // Check if any loaded libraries depend on this one
            var dependents = _loadedLibraries.Values
                .Where(e => e.Info.Manifest.Dependencies.Any(d => d.Id == libraryId))
                .Select(e => e.Info.Manifest.Name)
                .ToList();

            if (dependents.Any())
                return Result.Failure($"Cannot unload '{libraryId}' because it is required by: {string.Join(", ", dependents)}");

            _logOutput($"  → Unloading {loadedLibrary.Info.Manifest.Name}...");

            // Call unload hook
            await loadedLibrary.Instance.OnUnloadAsync();

            // Remove from loaded libraries
            _loadedLibraries.TryRemove(libraryId, out _);

            _logOutput($"  ✓ {loadedLibrary.Info.Manifest.Name} unloaded");

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logOutput($"  ✗ Failed to unload library '{libraryId}': {ex.Message}");
            return Result.Failure($"Failed to unload library '{libraryId}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates library dependencies
    /// </summary>
    private async Task<Result> ValidateDependenciesAsync(ILibraryManifest manifest)
    {
        var discoveryResult = await DiscoverLibrariesAsync();
        if (!discoveryResult.IsSuccess || discoveryResult.Value == null)
            return Result.Failure("Failed to discover libraries for dependency validation");

        var availableLibraries = discoveryResult.Value;

        foreach (var dependency in manifest.Dependencies.Where(d => !d.IsOptional))
        {
            var depLibrary = availableLibraries.FirstOrDefault(e => e.Manifest.Id == dependency.Id);

            if (depLibrary == null)
            {
                return Result.Failure($"Required dependency '{dependency.Id}' not found");
            }

            // Version check (simple semantic versioning)
            if (!IsVersionCompatible(depLibrary.Manifest.Version, dependency.MinVersion))
            {
                return Result.Failure($"Dependency '{dependency.Id}' version mismatch. Required: {dependency.MinVersion}, Found: {depLibrary.Manifest.Version}");
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Checks if installed version meets minimum version requirement
    /// </summary>
    private bool IsVersionCompatible(string installedVersion, string minVersion)
    {
        try
        {
            var installed = Version.Parse(installedVersion);
            var required = Version.Parse(minVersion);
            return installed >= required;
        }
        catch
        {
            // If version parsing fails, assume compatible
            return true;
        }
    }

    /// <summary>
    /// Gets all loaded libraries
    /// </summary>
    public IEnumerable<LibraryInfo> GetLoadedLibraries()
    {
        return _loadedLibraries.Values.Select(e => e.Info);
    }

    /// <summary>
    /// Gets library instance by ID
    /// </summary>
    public ILibrary? GetLibrary(string libraryId)
    {
        return _loadedLibraries.TryGetValue(libraryId, out var loaded) ? loaded.Instance : null;
    }

    /// <summary>
    /// Performs health check on all loaded libraries
    /// </summary>
    public async Task<Dictionary<string, HealthCheckResult>> HealthCheckAllAsync()
    {
        var results = new Dictionary<string, HealthCheckResult>();

        foreach (var kvp in _loadedLibraries)
        {
            try
            {
                results[kvp.Key] = await kvp.Value.Instance.HealthCheckAsync();
            }
            catch (Exception ex)
            {
                results[kvp.Key] = HealthCheckResult.Unhealthy($"Health check threw exception: {ex.Message}", ex);
            }
        }

        return results;
    }

    /// <summary>
    /// Unloads all libraries
    /// </summary>
    public async Task UnloadAllAsync()
    {
        _logOutput("\n=== Shutting Down Libraries ===");

        var libraryIds = _loadedLibraries.Keys.ToList();

        // Unload in reverse dependency order
        var sorted = TopologicalSort(libraryIds);

        foreach (var id in Enumerable.Reverse(sorted))
        {
            await UnloadLibraryAsync(id);
        }
    }

    /// <summary>
    /// Topological sort for dependency-ordered unloading
    /// </summary>
    private List<string> TopologicalSort(List<string> libraryIds)
    {
        var result = new List<string>();
        var visited = new HashSet<string>();

        void Visit(string id)
        {
            if (visited.Contains(id))
                return;

            visited.Add(id);

            if (_loadedLibraries.TryGetValue(id, out var lib))
            {
                foreach (var dep in lib.Info.Manifest.Dependencies)
                {
                    if (libraryIds.Contains(dep.Id))
                    {
                        Visit(dep.Id);
                    }
                }
            }

            result.Add(id);
        }

        foreach (var id in libraryIds)
        {
            Visit(id);
        }

        return result;
    }

    private class LoadedLibrary
    {
        public required LibraryInfo Info { get; init; }
        public required ILibrary Instance { get; init; }
        public required LibraryContext Context { get; init; }
        public DateTime LoadedAt { get; init; }
    }
}

/// <summary>
/// Information about a discovered library
/// </summary>
public class LibraryInfo
{
    public required ILibraryManifest Manifest { get; init; }
    public required string AssemblyPath { get; init; }
    public required string TypeName { get; init; }
}
