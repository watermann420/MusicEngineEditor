using System;

namespace MusicEngineEditor.Models;

/// <summary>
/// Represents a MusicEngine script file (.me)
/// </summary>
public class MusicScript
{
    public string FilePath { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public bool IsEntryPoint { get; set; }
    public string Content { get; set; } = string.Empty;
    public MusicProject? Project { get; set; }
    public bool IsDirty { get; set; }
    public DateTime LastModified { get; set; } = DateTime.UtcNow;

    public string FileName => System.IO.Path.GetFileName(FilePath);
    public string FileNameWithoutExtension => System.IO.Path.GetFileNameWithoutExtension(FilePath);

    /// <summary>
    /// Gets the relative path from the project root
    /// </summary>
    public string RelativePath
    {
        get
        {
            if (Project == null || string.IsNullOrEmpty(Project.ProjectDirectory))
                return FilePath;

            return System.IO.Path.GetRelativePath(Project.ProjectDirectory, FilePath);
        }
    }
}
