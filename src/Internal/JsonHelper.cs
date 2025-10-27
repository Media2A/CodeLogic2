using Newtonsoft.Json;

namespace CodeLogic.Internal;

/// <summary>
/// Internal JSON utilities for CodeLogic framework
/// </summary>
internal static class JsonHelper
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore
    };

    /// <summary>
    /// Serializes an object to JSON
    /// </summary>
    public static string Serialize<T>(T obj)
    {
        return JsonConvert.SerializeObject(obj, Settings);
    }

    /// <summary>
    /// Deserializes JSON to an object
    /// </summary>
    public static T? Deserialize<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return default;

        return JsonConvert.DeserializeObject<T>(json, Settings);
    }

    /// <summary>
    /// Deserializes JSON to a dictionary
    /// </summary>
    public static Dictionary<string, object> DeserializeToDictionary(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, object>();

        return JsonConvert.DeserializeObject<Dictionary<string, object>>(json, Settings)
               ?? new Dictionary<string, object>();
    }
}
