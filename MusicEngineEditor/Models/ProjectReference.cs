namespace MusicEngineEditor.Models;

/// <summary>
/// Represents a reference to another project or package
/// </summary>
public class ProjectReference
{
    /// <summary>
    /// Type of reference: "project" or "nuget"
    /// </summary>
    public string Type { get; set; } = "project";

    /// <summary>
    /// Path to the referenced project (for project references)
    /// or package name (for NuGet references)
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Alias used to reference this project in code
    /// </summary>
    public string Alias { get; set; } = string.Empty;

    /// <summary>
    /// Version (for NuGet references)
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// The loaded project (cached after resolution)
    /// </summary>
    public MusicProject? ResolvedProject { get; set; }

    /// <summary>
    /// Whether the reference has been successfully resolved
    /// </summary>
    public bool IsResolved => Type == "nuget" || ResolvedProject != null;
}
