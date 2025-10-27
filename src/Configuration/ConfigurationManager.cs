using CodeLogic.Models;
using CodeLogic.Internal;
using System.Collections.Concurrent;

namespace CodeLogic.Configuration;

/// <summary>
/// Manages hierarchical configuration with support for multiple sources and validation
/// </summary>
public class ConfigurationManager
{
    private readonly string _configDirectory;
    private readonly ConcurrentDictionary<string, ConfigSection> _sections = new();
    private readonly List<IConfigValidator> _validators = new();

    public ConfigurationManager(string configDirectory)
    {
        _configDirectory = configDirectory;
        FileSystemHelper.EnsureDirectoryExists(_configDirectory);
    }

    /// <summary>
    /// Loads all configuration files from the config directory
    /// </summary>
    public async Task<Result> LoadAsync()
    {
        try
        {
            var files = FileSystemHelper.GetFilesInDirectory(_configDirectory, "*.json", recursive: false);

            foreach (var file in files)
            {
                string sectionName = Path.GetFileNameWithoutExtension(file);
                string json = await FileSystemHelper.ReadFileAsync(file);

                var data = JsonHelper.DeserializeToDictionary(json);

                if (data != null)
                {
                    _sections[sectionName] = new ConfigSection
                    {
                        Name = sectionName,
                        Data = new ConcurrentDictionary<string, object>(data)
                    };
                }
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to load configuration: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets a configuration value
    /// </summary>
    public T? Get<T>(string section, string key, T? defaultValue = default)
    {
        if (!_sections.TryGetValue(section, out var configSection))
            return defaultValue;

        if (!configSection.Data.TryGetValue(key, out var value))
            return defaultValue;

        try
        {
            if (value is T typedValue)
                return typedValue;

            // Try to convert
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Gets a configuration value by path (e.g., "Database:ConnectionString")
    /// </summary>
    public T? GetByPath<T>(string path, T? defaultValue = default)
    {
        var parts = path.Split(':');
        if (parts.Length < 2)
            return defaultValue;

        string section = parts[0];
        string key = string.Join(":", parts.Skip(1));

        return Get(section, key, defaultValue);
    }

    /// <summary>
    /// Sets a configuration value
    /// </summary>
    public void Set(string section, string key, object value)
    {
        if (!_sections.ContainsKey(section))
        {
            _sections[section] = new ConfigSection
            {
                Name = section,
                Data = new ConcurrentDictionary<string, object>()
            };
        }

        _sections[section].Data[key] = value;
    }

    /// <summary>
    /// Saves configuration to file
    /// </summary>
    public async Task<Result> SaveAsync(string section)
    {
        try
        {
            if (!_sections.TryGetValue(section, out var configSection))
                return Result.Failure($"Section '{section}' not found");

            string fileName = FileSystemHelper.NormalizePath(_configDirectory, $"{section}.json");
            string json = JsonHelper.Serialize(configSection.Data);
            await FileSystemHelper.WriteFileAsync(fileName, json, append: false);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to save configuration section '{section}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates all configuration
    /// </summary>
    public ValidationResult Validate()
    {
        var errors = new List<ValidationError>();

        foreach (var validator in _validators)
        {
            var result = validator.Validate(this);
            if (!result.IsValid)
            {
                errors.AddRange(result.Errors);
            }
        }

        return errors.Count == 0
            ? ValidationResult.Valid()
            : ValidationResult.Invalid(errors);
    }

    /// <summary>
    /// Registers a configuration validator
    /// </summary>
    public void AddValidator(IConfigValidator validator)
    {
        _validators.Add(validator);
    }

    /// <summary>
    /// Gets all sections
    /// </summary>
    public IEnumerable<string> GetSections() => _sections.Keys;

    /// <summary>
    /// Checks if a section exists
    /// </summary>
    public bool HasSection(string section) => _sections.ContainsKey(section);

    /// <summary>
    /// Gets all keys in a section
    /// </summary>
    public IEnumerable<string> GetKeys(string section)
    {
        return _sections.TryGetValue(section, out var configSection)
            ? configSection.Data.Keys
            : Enumerable.Empty<string>();
    }

    /// <summary>
    /// Generates a default configuration file
    /// </summary>
    public async Task GenerateDefaultAsync(string section, Dictionary<string, object> defaults)
    {
        string fileName = FileSystemHelper.NormalizePath(_configDirectory, $"{section}.json");

        if (FileSystemHelper.FileExists(fileName))
            return; // Don't overwrite existing config

        string json = JsonHelper.Serialize(defaults);
        await FileSystemHelper.WriteFileAsync(fileName, json, append: false);
    }

    /// <summary>
    /// Merges configuration from another source
    /// </summary>
    public void Merge(string section, Dictionary<string, object> values)
    {
        if (!_sections.ContainsKey(section))
        {
            _sections[section] = new ConfigSection
            {
                Name = section,
                Data = new ConcurrentDictionary<string, object>()
            };
        }

        foreach (var kvp in values)
        {
            _sections[section].Data[kvp.Key] = kvp.Value;
        }
    }

    private class ConfigSection
    {
        public required string Name { get; init; }
        public required ConcurrentDictionary<string, object> Data { get; init; }
    }
}

/// <summary>
/// Interface for configuration validators
/// </summary>
public interface IConfigValidator
{
    ValidationResult Validate(ConfigurationManager config);
}
