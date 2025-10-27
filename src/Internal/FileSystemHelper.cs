namespace CodeLogic.Internal;

/// <summary>
/// Internal file system utilities for CodeLogic framework
/// </summary>
internal static class FileSystemHelper
{
    /// <summary>
    /// Checks if a directory exists
    /// </summary>
    public static bool DirectoryExists(string path)
    {
        return Directory.Exists(path);
    }

    /// <summary>
    /// Checks if a file exists
    /// </summary>
    public static bool FileExists(string path)
    {
        return File.Exists(path);
    }

    /// <summary>
    /// Ensures a directory exists, creating it if necessary
    /// </summary>
    public static void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    /// <summary>
    /// Gets files in a directory matching a pattern
    /// </summary>
    public static string[] GetFilesInDirectory(string path, string searchPattern = "*", bool recursive = false)
    {
        if (!Directory.Exists(path))
            return Array.Empty<string>();

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory.GetFiles(path, searchPattern, searchOption);
    }

    /// <summary>
    /// Reads all text from a file asynchronously
    /// </summary>
    public static async Task<string> ReadFileAsync(string path)
    {
        return await File.ReadAllTextAsync(path);
    }

    /// <summary>
    /// Writes text to a file asynchronously
    /// </summary>
    public static async Task WriteFileAsync(string path, string content, bool append = false)
    {
        if (append)
        {
            await File.AppendAllTextAsync(path, content);
        }
        else
        {
            await File.WriteAllTextAsync(path, content);
        }
    }

    /// <summary>
    /// Normalizes a path by combining path parts
    /// </summary>
    public static string NormalizePath(params string[] parts)
    {
        return Path.Combine(parts);
    }
}
