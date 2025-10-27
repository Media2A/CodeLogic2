namespace CodeLogic.Abstractions;

/// <summary>
/// Defines the metadata and requirements for a CodeLogic library
/// </summary>
public interface ILibraryManifest
{
    /// <summary>
    /// Unique identifier for the library (should be lowercase, e.g., "cl.mysql2")
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Display name of the library
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Semantic version (e.g., "1.0.0")
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Author or organization name
    /// </summary>
    string Author { get; }

    /// <summary>
    /// Brief description of the library's purpose
    /// </summary>
    string Description { get; }

    /// <summary>
    /// List of dependencies required by this library
    /// </summary>
    IReadOnlyList<LibraryDependency> Dependencies { get; }

    /// <summary>
    /// Optional tags for categorization
    /// </summary>
    IReadOnlyList<string> Tags { get; }
}

/// <summary>
/// Represents a dependency on another library
/// </summary>
public record LibraryDependency
{
    /// <summary>
    /// Identifier of the required library (lowercase)
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Minimum required version (semantic versioning)
    /// </summary>
    public required string MinVersion { get; init; }

    /// <summary>
    /// Whether this dependency is optional
    /// </summary>
    public bool IsOptional { get; init; } = false;
}
