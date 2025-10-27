using CodeLogic.Abstractions;
using CodeLogic.Models;
using CodeLogic.Caching;
using CodeLogic.Configuration;
using CodeLogic.Libraries;
using CodeLogic.Localization;
using CodeLogic.Logging;
using CodeLogic.Startup;

namespace CodeLogic;

/// <summary>
/// Main framework class that orchestrates all CodeLogic services
/// </summary>
public class Framework : IDisposable
{
    private readonly FrameworkOptions _options;
    private bool _initialized;

    public ConfigurationManager Configuration { get; private set; } = null!;
    public LoggerFactory LoggerFactory { get; private set; } = null!;
    public LocalizationManager Localization { get; private set; } = null!;
    public CacheManager Cache { get; private set; } = null!;
    public LibraryManager Libraries { get; private set; } = null!;
    public ILogger Logger { get; private set; } = null!;

    public Framework(FrameworkOptions? options = null)
    {
        _options = options ?? FrameworkOptions.CreateDefault();
    }

    /// <summary>
    /// Initializes the framework with validation
    /// </summary>
    public async Task<Result> InitializeAsync(bool validateFirst = true)
    {
        if (_initialized)
            return Result.Failure("Framework is already initialized");

        try
        {
            PrintBanner();

            Console.WriteLine("=== CodeLogic Framework Initialization ===\n");

            // Step 1: Validate system
            if (validateFirst)
            {
                Console.WriteLine("→ Validating system requirements...");
                var validator = new StartupValidator();
                var validationResult = await validator.ValidateAsync(_options);

                if (!validationResult.IsValid)
                {
                    Console.WriteLine("✗ Validation failed!\n");
                    foreach (var error in validationResult.Errors)
                    {
                        Console.WriteLine($"  • {error.PropertyName}: {error.Message}");
                    }
                    return Result.Failure($"Framework validation failed");
                }

                // Log warnings
                var warnings = validator.GetWarnings();
                if (warnings.Count > 0)
                {
                    Console.WriteLine($"⚠ Validation warnings ({warnings.Count}):");
                    foreach (var warning in warnings)
                    {
                        Console.WriteLine($"  • {warning}");
                    }
                }

                Console.WriteLine("✓ System validation passed\n");
            }

            // Step 2: Initialize logging
            Console.WriteLine("→ Initializing logging system...");
            LoggerFactory = new LoggerFactory(_options.LogDirectory, LogLevel.Info);
            Logger = LoggerFactory.CreateLogger("Framework");
            Console.WriteLine($"✓ Logging initialized (directory: {Path.GetFileName(_options.LogDirectory)})");
            Logger.Info("CodeLogic Framework starting up");

            // Step 3: Initialize configuration
            Console.WriteLine("\n→ Loading configuration...");
            Configuration = new ConfigurationManager(_options.ConfigDirectory);

            // Generate default configurations
            await GenerateDefaultConfigurationsAsync();

            var configResult = await Configuration.LoadAsync();
            if (!configResult.IsSuccess)
            {
                Logger.Error($"Configuration load failed: {configResult.ErrorMessage}");
                return configResult;
            }

            Console.WriteLine($"✓ Configuration loaded ({Configuration.GetSections().Count()} section(s))");
            Logger.Info($"Configuration loaded from {_options.ConfigDirectory}");

            // Step 4: Initialize caching
            Console.WriteLine("\n→ Initializing cache manager...");
            var cacheOptions = new CacheOptions
            {
                MemoryCacheSizeLimit = Configuration.Get("Cache", "SizeLimit", 10000L),
                DefaultExpiration = TimeSpan.FromHours(Configuration.Get("Cache", "DefaultExpirationHours", 1)),
                EnableCompression = Configuration.Get("Cache", "EnableCompression", true)
            };

            Cache = new CacheManager(cacheOptions);
            Console.WriteLine($"✓ Cache manager ready (size limit: {cacheOptions.MemoryCacheSizeLimit})");
            Logger.Info("Cache manager initialized");

            // Step 5: Initialize localization
            Console.WriteLine("\n→ Loading localization resources...");
            Localization = new LocalizationManager(_options.LocalizationDirectory);

            // Generate default localization
            await GenerateDefaultLocalizationAsync();
            await Localization.LoadAsync();

            var cultures = Localization.GetAvailableCultures().ToList();
            Console.WriteLine($"✓ Localization ready ({cultures.Count} culture(s) loaded)");
            Logger.Info($"Localization loaded with {cultures.Count} cultures");

            // Step 6: Initialize library system
            Console.WriteLine("\n=== Loading CodeLogic Libraries ===\n");
            Libraries = new LibraryManager(
                _options.LibrariesDirectory,
                _options.DataDirectory,
                _options.LogDirectory,
                new FrameworkServiceProvider(this),
                Console.WriteLine
            );

            // Discover libraries
            var discoveryResult = await Libraries.DiscoverLibrariesAsync();
            if (discoveryResult.IsSuccess && discoveryResult.Value != null && discoveryResult.Value.Count > 0)
            {
                Console.WriteLine($"Discovered {discoveryResult.Value.Count} library(ies):\n");

                foreach (var lib in discoveryResult.Value)
                {
                    Console.WriteLine($"  • {lib.Manifest.Name} v{lib.Manifest.Version}");
                    Console.WriteLine($"    {lib.Manifest.Description}");
                    Console.WriteLine($"    by {lib.Manifest.Author}");

                    if (lib.Manifest.Dependencies.Any())
                    {
                        Console.WriteLine($"    Dependencies: {string.Join(", ", lib.Manifest.Dependencies.Select(d => d.Id))}");
                    }

                    Console.WriteLine();
                }

                Logger.Info($"Discovered {discoveryResult.Value.Count} libraries");
            }
            else
            {
                Console.WriteLine("⚠ No libraries found in libraries directory\n");
            }

            _initialized = true;

            Console.WriteLine("=== Framework Initialization Complete ===\n");
            Logger.Info("CodeLogic Framework initialized successfully");

            return Result.Success();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n✗ Initialization failed: {ex.Message}\n");
            return Result.Failure($"Framework initialization failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Loads a specific library
    /// </summary>
    public async Task<Result> LoadLibraryAsync(string libraryId)
    {
        if (!_initialized)
            return Result.Failure("Framework must be initialized before loading libraries");

        Console.WriteLine($"\n→ Loading library: {libraryId}");
        Logger.Info($"Loading library: {libraryId}");

        var result = await Libraries.LoadLibraryAsync(libraryId);

        if (result.IsSuccess)
        {
            Logger.Info($"Library '{libraryId}' loaded successfully");
        }
        else
        {
            Logger.Error($"Failed to load library '{libraryId}': {result.ErrorMessage}");
        }

        return result;
    }

    /// <summary>
    /// Loads all discovered libraries
    /// </summary>
    public async Task<Result> LoadAllLibrariesAsync()
    {
        if (!_initialized)
            return Result.Failure("Framework must be initialized before loading libraries");

        var discoveryResult = await Libraries.DiscoverLibrariesAsync();

        if (!discoveryResult.IsSuccess || discoveryResult.Value == null)
            return Result.Failure($"Library discovery failed: {discoveryResult.ErrorMessage}");

        Console.WriteLine("\n=== Loading All Libraries ===\n");

        var errors = new List<string>();

        // Load in dependency order
        foreach (var lib in discoveryResult.Value.OrderBy(l => l.Manifest.Dependencies.Count))
        {
            var result = await LoadLibraryAsync(lib.Manifest.Id);
            if (!result.IsSuccess)
            {
                errors.Add($"{lib.Manifest.Name}: {result.ErrorMessage}");
            }
        }

        Console.WriteLine();

        if (errors.Count > 0)
        {
            return Result.Failure($"Some libraries failed to load: {string.Join("; ", errors)}");
        }

        Console.WriteLine("✓ All libraries loaded successfully\n");
        return Result.Success();
    }

    /// <summary>
    /// Performs health check on all components
    /// </summary>
    public async Task<HealthCheckResult> HealthCheckAsync()
    {
        if (!_initialized)
            return HealthCheckResult.Unhealthy("Framework not initialized");

        try
        {
            var checks = await Libraries.HealthCheckAllAsync();
            var unhealthy = checks.Where(c => !c.Value.IsHealthy).ToList();

            if (unhealthy.Any())
            {
                return HealthCheckResult.Unhealthy(
                    $"{unhealthy.Count} library(ies) are unhealthy: {string.Join(", ", unhealthy.Select(c => c.Key))}"
                );
            }

            return HealthCheckResult.Healthy($"All {checks.Count} library(ies) are healthy");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Health check failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Shuts down the framework and unloads all libraries
    /// </summary>
    public async Task ShutdownAsync()
    {
        if (!_initialized)
            return;

        Console.WriteLine("\n=== CodeLogic Framework Shutdown ===\n");
        Logger.Info("Shutting down CodeLogic Framework");

        // Unload all libraries
        await Libraries.UnloadAllAsync();

        // Flush logs
        await Logging.Logger.FlushAllAsync();

        _initialized = false;

        Console.WriteLine("\n✓ Framework shutdown complete\n");
        Logger.Info("Framework shutdown complete");
    }

    private async Task GenerateDefaultConfigurationsAsync()
    {
        // Framework config
        await Configuration.GenerateDefaultAsync("Framework", new Dictionary<string, object>
        {
            ["Name"] = "CodeLogic Application",
            ["Version"] = "1.0.0",
            ["Environment"] = "Development"
        });

        // Cache config
        await Configuration.GenerateDefaultAsync("Cache", new Dictionary<string, object>
        {
            ["SizeLimit"] = 10000,
            ["DefaultExpirationHours"] = 1,
            ["EnableCompression"] = true
        });

        // Logging config
        await Configuration.GenerateDefaultAsync("Logging", new Dictionary<string, object>
        {
            ["MinimumLevel"] = "Info",
            ["EnableConsole"] = true,
            ["EnableFile"] = true
        });
    }

    private async Task GenerateDefaultLocalizationAsync()
    {
        if (!Localization.IsCultureSupported("en-US"))
        {
            await Localization.GenerateTemplateAsync("en-US");
        }
    }

    private void PrintBanner()
    {
        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════════════════╗");
        Console.WriteLine("║          CodeLogic Framework v2.0                ║");
        Console.WriteLine("║    Modern Application Framework for .NET         ║");
        Console.WriteLine("╚══════════════════════════════════════════════════╝");
        Console.WriteLine();
    }

    public void Dispose()
    {
        if (_initialized)
        {
            ShutdownAsync().GetAwaiter().GetResult();
        }
    }

    private class FrameworkServiceProvider : IServiceProvider
    {
        private readonly Framework _framework;

        public FrameworkServiceProvider(Framework framework)
        {
            _framework = framework;
        }

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(ILogger))
                return _framework.Logger;

            if (serviceType == typeof(ICache))
                return _framework.Cache;

            if (serviceType == typeof(ConfigurationManager))
                return _framework.Configuration;

            if (serviceType == typeof(LocalizationManager))
                return _framework.Localization;

            return null;
        }
    }
}
