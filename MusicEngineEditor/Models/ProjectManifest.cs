using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MusicEngineEditor.Models;

/// <summary>
/// JSON serialization model for .meproj files
/// </summary>
public class ProjectManifest
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("guid")]
    public string Guid { get; set; } = string.Empty;

    [JsonPropertyName("namespace")]
    public string Namespace { get; set; } = string.Empty;

    [JsonPropertyName("created")]
    public string Created { get; set; } = string.Empty;

    [JsonPropertyName("modified")]
    public string Modified { get; set; } = string.Empty;

    [JsonPropertyName("musicEngineVersion")]
    public string MusicEngineVersion { get; set; } = "1.0.0";

    [JsonPropertyName("scripts")]
    public List<ScriptDefinition> Scripts { get; set; } = new();

    [JsonPropertyName("audioAssets")]
    public List<AudioAssetDefinition> AudioAssets { get; set; } = new();

    [JsonPropertyName("references")]
    public List<ReferenceDefinition> References { get; set; } = new();

    [JsonPropertyName("settings")]
    public ProjectSettings Settings { get; set; } = new();

    [JsonPropertyName("build")]
    public BuildSettings? Build { get; set; }
}

public class ScriptDefinition
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("entryPoint")]
    public bool EntryPoint { get; set; }

    [JsonPropertyName("namespace")]
    public string Namespace { get; set; } = string.Empty;
}

public class AudioAssetDefinition
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("alias")]
    public string Alias { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = "General";
}

public class ReferenceDefinition
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "project";

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("alias")]
    public string Alias { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string? Version { get; set; }
}

public class BuildSettings
{
    [JsonPropertyName("outputPath")]
    public string OutputPath { get; set; } = "bin/";

    [JsonPropertyName("intermediateOutput")]
    public string IntermediateOutput { get; set; } = "obj/";
}
