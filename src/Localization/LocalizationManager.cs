using CodeLogic.Internal;
using System.Collections.Concurrent;
using System.Globalization;

namespace CodeLogic.Localization;

/// <summary>
/// Manages localization resources with support for multiple cultures and fallbacks
/// </summary>
public class LocalizationManager
{
    private readonly string _localizationDirectory;
    private readonly CultureInfo _defaultCulture;
    private readonly ConcurrentDictionary<string, LocalizationResource> _resources = new();

    public LocalizationManager(string localizationDirectory, string defaultCulture = "en-US")
    {
        _localizationDirectory = localizationDirectory;
        _defaultCulture = new CultureInfo(defaultCulture);

        FileSystemHelper.EnsureDirectoryExists(_localizationDirectory);
    }

    /// <summary>
    /// Loads all localization files from the localization directory
    /// </summary>
    public async Task LoadAsync()
    {
        var files = FileSystemHelper.GetFilesInDirectory(_localizationDirectory, "*.json", recursive: false);

        foreach (var file in files)
        {
            try
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                string json = await FileSystemHelper.ReadFileAsync(file);

                var translations = JsonHelper.Deserialize<Dictionary<string, string>>(json);

                if (translations != null)
                {
                    _resources[fileName] = new LocalizationResource
                    {
                        Culture = fileName,
                        Translations = translations
                    };
                }
            }
            catch (Exception ex)
            {
                // Log error but continue loading other files
                Console.WriteLine($"Error loading localization file {file}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Gets a localized string for the specified key and culture
    /// </summary>
    public string GetString(string key, string? culture = null)
    {
        culture ??= _defaultCulture.Name;

        // Try exact culture match
        if (_resources.TryGetValue(culture, out var resource))
        {
            if (resource.Translations.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        // Try fallback to parent culture (e.g., en-US -> en)
        if (culture.Contains('-'))
        {
            string parentCulture = culture.Split('-')[0];
            if (_resources.TryGetValue(parentCulture, out var parentResource))
            {
                if (parentResource.Translations.TryGetValue(key, out var value))
                {
                    return value;
                }
            }
        }

        // Try default culture
        if (_resources.TryGetValue(_defaultCulture.Name, out var defaultResource))
        {
            if (defaultResource.Translations.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        // Return key if not found
        return $"[{key}]";
    }

    /// <summary>
    /// Gets a formatted localized string with parameters
    /// </summary>
    public string GetString(string key, string? culture, params object[] args)
    {
        string template = GetString(key, culture);
        return args.Length > 0 ? string.Format(template, args) : template;
    }

    /// <summary>
    /// Adds or updates a translation
    /// </summary>
    public async Task SetStringAsync(string key, string value, string culture)
    {
        if (!_resources.ContainsKey(culture))
        {
            _resources[culture] = new LocalizationResource
            {
                Culture = culture,
                Translations = new Dictionary<string, string>()
            };
        }

        _resources[culture].Translations[key] = value;

        // Persist to file
        await SaveCultureAsync(culture);
    }

    /// <summary>
    /// Saves a culture's translations to file
    /// </summary>
    private async Task SaveCultureAsync(string culture)
    {
        if (!_resources.TryGetValue(culture, out var resource))
            return;

        string fileName = FileSystemHelper.NormalizePath(_localizationDirectory, $"{culture}.json");
        string json = JsonHelper.Serialize(resource.Translations);
        await FileSystemHelper.WriteFileAsync(fileName, json, append: false);
    }

    /// <summary>
    /// Gets all available cultures
    /// </summary>
    public IEnumerable<string> GetAvailableCultures()
    {
        return _resources.Keys;
    }

    /// <summary>
    /// Checks if a culture is supported
    /// </summary>
    public bool IsCultureSupported(string culture)
    {
        return _resources.ContainsKey(culture) ||
               _resources.ContainsKey(culture.Split('-')[0]);
    }

    /// <summary>
    /// Generates a template localization file for a new culture
    /// </summary>
    public async Task GenerateTemplateAsync(string culture)
    {
        var template = new Dictionary<string, string>
        {
            ["welcome"] = "Welcome",
            ["hello"] = "Hello, {0}",
            ["goodbye"] = "Goodbye",
            ["error.generic"] = "An error occurred",
            ["error.notfound"] = "Item not found",
            ["success.saved"] = "Successfully saved"
        };

        string fileName = FileSystemHelper.NormalizePath(_localizationDirectory, $"{culture}.json");
        string json = JsonHelper.Serialize(template);
        await FileSystemHelper.WriteFileAsync(fileName, json, append: false);
    }

    private class LocalizationResource
    {
        public required string Culture { get; init; }
        public required Dictionary<string, string> Translations { get; init; }
    }
}
