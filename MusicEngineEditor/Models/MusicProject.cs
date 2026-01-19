using System;
using System.Collections.ObjectModel;

namespace MusicEngineEditor.Models;

/// <summary>
/// Represents a MusicEngine project
/// </summary>
public class MusicProject
{
    public string Name { get; set; } = "Untitled";
    public Guid Guid { get; set; } = Guid.NewGuid();
    public string Namespace { get; set; } = "MusicProject";
    public string FilePath { get; set; } = string.Empty;
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public DateTime Modified { get; set; } = DateTime.UtcNow;
    public string MusicEngineVersion { get; set; } = "1.0.0";

    public ProjectSettings Settings { get; set; } = new();
    public ObservableCollection<MusicScript> Scripts { get; } = new();
    public ObservableCollection<AudioAsset> AudioAssets { get; } = new();
    public ObservableCollection<ProjectReference> References { get; } = new();

    public string ProjectDirectory => System.IO.Path.GetDirectoryName(FilePath) ?? string.Empty;

    public bool IsDirty { get; set; }
}

/// <summary>
/// Project settings
/// </summary>
public class ProjectSettings
{
    public int SampleRate { get; set; } = 44100;
    public int BufferSize { get; set; } = 512;
    public int DefaultBpm { get; set; } = 120;
    public string OutputDevice { get; set; } = "default";
    public string OutputPath { get; set; } = "bin/";
    public string IntermediateOutput { get; set; } = "obj/";
}
