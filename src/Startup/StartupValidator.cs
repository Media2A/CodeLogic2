using CodeLogic.Models;
using CodeLogic.Internal;
using CodeLogic.Configuration;
using CodeLogic.Libraries;

namespace CodeLogic.Startup;

/// <summary>
/// Validates system readiness before application startup
/// </summary>
public class StartupValidator
{
    private readonly List<ValidationError> _errors = new();
    private readonly List<string> _warnings = new();

    /// <summary>
    /// Validates the entire framework configuration and dependencies
    /// </summary>
    public async Task<ValidationResult> ValidateAsync(FrameworkOptions options)
    {
        _errors.Clear();
        _warnings.Clear();

        // Validate directories
        ValidateDirectories(options);

        // Validate configuration
        await ValidateConfigurationAsync(options);

        // Validate libraries
        await ValidateLibrariesAsync(options);

        // System checks
        ValidateSystem();

        return _errors.Count == 0
            ? ValidationResult.Valid()
            : ValidationResult.Invalid(_errors);
    }

    /// <summary>
    /// Validates required directories exist or can be created
    /// </summary>
    private void ValidateDirectories(FrameworkOptions options)
    {
        var directories = new Dictionary<string, string>
        {
            ["Config"] = options.ConfigDirectory,
            ["Logs"] = options.LogDirectory,
            ["Localization"] = options.LocalizationDirectory,
            ["Libraries"] = options.LibrariesDirectory,
            ["Data"] = options.DataDirectory
        };

        foreach (var dir in directories)
        {
            try
            {
                if (!FileSystemHelper.DirectoryExists(dir.Value))
                {
                    Directory.CreateDirectory(dir.Value);
                    _warnings.Add($"Created missing {dir.Key} directory: {dir.Value}");
                }
            }
            catch (Exception ex)
            {
                _errors.Add(new ValidationError
                {
                    PropertyName = $"Directory.{dir.Key}",
                    Message = $"Failed to access or create {dir.Key} directory: {ex.Message}",
                    Code = "DIR_ACCESS_FAILED"
                });
            }
        }
    }

    /// <summary>
    /// Validates configuration system
    /// </summary>
    private async Task ValidateConfigurationAsync(FrameworkOptions options)
    {
        try
        {
            var configManager = new ConfigurationManager(options.ConfigDirectory);
            var loadResult = await configManager.LoadAsync();

            if (!loadResult.IsSuccess)
            {
                _errors.Add(new ValidationError
                {
                    PropertyName = "Configuration",
                    Message = $"Configuration load failed: {loadResult.ErrorMessage}",
                    Code = "CONFIG_LOAD_FAILED"
                });
                return;
            }

            // Validate configuration
            var validationResult = configManager.Validate();
            if (!validationResult.IsValid)
            {
                _errors.AddRange(validationResult.Errors);
            }
        }
        catch (Exception ex)
        {
            _errors.Add(new ValidationError
            {
                PropertyName = "Configuration",
                Message = $"Configuration validation threw exception: {ex.Message}",
                Code = "CONFIG_VALIDATION_ERROR"
            });
        }
    }

    /// <summary>
    /// Validates libraries system
    /// </summary>
    private async Task ValidateLibrariesAsync(FrameworkOptions options)
    {
        try
        {
            // Create a temporary library manager for validation
            var libraryManager = new LibraryManager(
                options.LibrariesDirectory,
                options.DataDirectory,
                options.LogDirectory,
                new EmptyServiceProvider()
            );

            var discoveryResult = await libraryManager.DiscoverLibrariesAsync();

            if (!discoveryResult.IsSuccess)
            {
                _warnings.Add($"Library discovery warning: {discoveryResult.ErrorMessage}");
                return;
            }

            if (discoveryResult.Value == null || discoveryResult.Value.Count == 0)
            {
                _warnings.Add("No libraries found in libraries directory");
                return;
            }

            // Validate each library's dependencies
            foreach (var lib in discoveryResult.Value)
            {
                foreach (var dep in lib.Manifest.Dependencies.Where(d => !d.IsOptional))
                {
                    var depExists = discoveryResult.Value.Any(e => e.Manifest.Id == dep.Id);
                    if (!depExists)
                    {
                        _errors.Add(new ValidationError
                        {
                            PropertyName = $"Library.{lib.Manifest.Id}",
                            Message = $"Library '{lib.Manifest.Name}' requires missing dependency: {dep.Id} (>= {dep.MinVersion})",
                            Code = "MISSING_DEPENDENCY"
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _warnings.Add($"Library validation warning: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates system requirements
    /// </summary>
    private void ValidateSystem()
    {
        // Check available memory
        try
        {
            var gcMemory = GC.GetTotalMemory(false);
            if (gcMemory > 1_000_000_000) // > 1GB
            {
                _warnings.Add($"High memory usage detected: {gcMemory / 1_000_000}MB");
            }
        }
        catch
        {
            // Ignore memory check errors
        }

        // Check .NET version
        var version = Environment.Version;
        if (version.Major < 10)
        {
            _warnings.Add($"Running on .NET {version}. Framework is optimized for .NET 10+");
        }
    }

    /// <summary>
    /// Gets validation warnings
    /// </summary>
    public IReadOnlyList<string> GetWarnings() => _warnings;

    private class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}

/// <summary>
/// Framework configuration options
/// </summary>
public class FrameworkOptions
{
    public required string ConfigDirectory { get; init; }
    public required string LogDirectory { get; init; }
    public required string LocalizationDirectory { get; init; }
    public required string LibrariesDirectory { get; init; }
    public required string DataDirectory { get; init; }

    /// <summary>
    /// Creates default options based on application directory
    /// </summary>
    public static FrameworkOptions CreateDefault(string? baseDirectory = null)
    {
        baseDirectory ??= AppDomain.CurrentDomain.BaseDirectory;

        return new FrameworkOptions
        {
            ConfigDirectory = Path.Combine(baseDirectory, "config"),
            LogDirectory = Path.Combine(baseDirectory, "logs"),
            LocalizationDirectory = Path.Combine(baseDirectory, "localization"),
            // Libraries are in the same directory as the executable (where all DLLs are copied during build)
            LibrariesDirectory = baseDirectory,
            DataDirectory = Path.Combine(baseDirectory, "data")
        };
    }
}
