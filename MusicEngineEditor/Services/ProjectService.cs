// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Service implementation.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Services;

/// <summary>
/// Service for managing MusicEngine projects
/// </summary>
public class ProjectService : IProjectService
{
    private const string ProjectExtension = ".meproj";
    private const string ScriptExtension = ".me";
    private const int MaxRecentProjects = 10;
    private const string RecentProjectsFileName = "recent.json";

    /// <summary>
    /// Path to the MusicEngine test_script.csx file used as default startup content.
    /// This is resolved relative to the solution directory.
    /// </summary>
    private static readonly string TestScriptPath = GetTestScriptPath();

    private readonly List<RecentProjectEntry> _recentProjects = new();
    private readonly string _recentProjectsPath;

    private static string GetTestScriptPath()
    {
        // Try to find test_script.csx relative to the application
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;

        // Navigate up from bin/Debug/net8.0-windows to find MusicEngine sibling folder
        var dir = new DirectoryInfo(baseDir);
        while (dir != null && dir.Parent != null)
        {
            var testScriptPath = Path.Combine(dir.Parent.FullName, "MusicEngine", "test_script.csx");
            if (File.Exists(testScriptPath))
            {
                return testScriptPath;
            }

            // Also check in RiderProjects parent
            var riderProjectsPath = Path.Combine(dir.FullName, "..", "..", "MusicEngine", "test_script.csx");
            if (File.Exists(riderProjectsPath))
            {
                return Path.GetFullPath(riderProjectsPath);
            }

            dir = dir.Parent;
        }

        // Fallback: Try common development paths
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var fallbackPath = Path.Combine(userProfile, "RiderProjects", "MusicEngine", "test_script.csx");
        if (File.Exists(fallbackPath))
        {
            return fallbackPath;
        }

        return string.Empty;
    }

    public MusicProject? CurrentProject { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<RecentProjectEntry> RecentProjects => _recentProjects.AsReadOnly();

    public event EventHandler<MusicProject>? ProjectLoaded;
    public event EventHandler? ProjectClosed;

    private static JsonSerializerOptions JsonOptions => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ProjectService()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(localAppData, "MusicEngineEditor");
        Directory.CreateDirectory(appFolder);
        _recentProjectsPath = Path.Combine(appFolder, RecentProjectsFileName);

        LoadRecentProjects();
    }

    public async Task<MusicProject> CreateProjectAsync(string name, string path)
    {
        var projectDir = Path.Combine(path, name);
        Directory.CreateDirectory(projectDir);
        Directory.CreateDirectory(Path.Combine(projectDir, "Scripts"));
        Directory.CreateDirectory(Path.Combine(projectDir, "Audio"));
        Directory.CreateDirectory(Path.Combine(projectDir, "bin"));
        Directory.CreateDirectory(Path.Combine(projectDir, "obj"));

        var project = new MusicProject
        {
            Name = name,
            Guid = Guid.NewGuid(),
            Namespace = SanitizeNamespace(name),
            FilePath = Path.Combine(projectDir, $"{name}{ProjectExtension}"),
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            MusicEngineVersion = "1.0.0",
            Settings = new ProjectSettings
            {
                SampleRate = 44100,
                BufferSize = 512,
                DefaultBpm = 120
            }
        };

        // Create default Main.me script
        var mainScript = CreateDefaultScript(project, "Main", isEntryPoint: true);
        project.Scripts.Add(mainScript);
        await SaveScriptAsync(mainScript);

        await SaveProjectAsync(project);

        CurrentProject = project;
        ProjectLoaded?.Invoke(this, project);

        // Add to recent projects
        AddToRecentProjects(project.FilePath);

        return project;
    }

    public async Task<MusicProject> OpenProjectAsync(string projectFilePath)
    {
        var json = await File.ReadAllTextAsync(projectFilePath);
        var manifest = JsonSerializer.Deserialize<ProjectManifest>(json, JsonOptions);

        if (manifest == null)
            throw new InvalidOperationException("Could not parse project file");

        var projectDir = Path.GetDirectoryName(projectFilePath)!;

        var project = new MusicProject
        {
            Name = manifest.Name,
            Guid = Guid.Parse(manifest.Guid),
            Namespace = manifest.Namespace,
            FilePath = projectFilePath,
            Created = DateTime.Parse(manifest.Created),
            Modified = DateTime.Parse(manifest.Modified),
            MusicEngineVersion = manifest.MusicEngineVersion,
            Settings = manifest.Settings
        };

        // Load scripts
        foreach (var scriptDef in manifest.Scripts)
        {
            var scriptPath = Path.Combine(projectDir, scriptDef.Path);
            if (File.Exists(scriptPath))
            {
                var script = new MusicScript
                {
                    FilePath = scriptPath,
                    Namespace = scriptDef.Namespace,
                    IsEntryPoint = scriptDef.EntryPoint,
                    Content = await File.ReadAllTextAsync(scriptPath),
                    Project = project
                };
                project.Scripts.Add(script);
            }
        }

        // Load audio assets
        foreach (var audioDef in manifest.AudioAssets)
        {
            var audioPath = Path.Combine(projectDir, audioDef.Path);
            if (File.Exists(audioPath))
            {
                project.AudioAssets.Add(new AudioAsset
                {
                    FilePath = audioPath,
                    Alias = audioDef.Alias,
                    Category = audioDef.Category
                });
            }
        }

        // Load project references
        foreach (var refDef in manifest.References)
        {
            project.References.Add(new ProjectReference
            {
                Type = refDef.Type,
                Path = refDef.Path,
                Alias = refDef.Alias,
                Version = refDef.Version
            });
        }

        CurrentProject = project;
        ProjectLoaded?.Invoke(this, project);

        // Add to recent projects
        AddToRecentProjects(projectFilePath);

        return project;
    }

    public async Task SaveProjectAsync(MusicProject project)
    {
        project.Modified = DateTime.UtcNow;

        var manifest = new ProjectManifest
        {
            Schema = "https://musicengine.dev/schema/meproj-1.0.json",
            Name = project.Name,
            Guid = project.Guid.ToString(),
            Namespace = project.Namespace,
            Created = project.Created.ToString("o"),
            Modified = project.Modified.ToString("o"),
            MusicEngineVersion = project.MusicEngineVersion,
            Settings = project.Settings,
            Scripts = project.Scripts.Select(s => new ScriptDefinition
            {
                Path = Path.GetRelativePath(Path.GetDirectoryName(project.FilePath)!, s.FilePath),
                Namespace = s.Namespace,
                EntryPoint = s.IsEntryPoint
            }).ToList(),
            AudioAssets = project.AudioAssets.Select(a => new AudioAssetDefinition
            {
                Path = Path.GetRelativePath(Path.GetDirectoryName(project.FilePath)!, a.FilePath),
                Alias = a.Alias,
                Category = a.Category
            }).ToList(),
            References = project.References.Select(r => new ReferenceDefinition
            {
                Type = r.Type,
                Path = r.Path,
                Alias = r.Alias,
                Version = r.Version
            }).ToList()
        };

        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        await File.WriteAllTextAsync(project.FilePath, json);
        project.IsDirty = false;
    }

    public async Task CloseProjectAsync()
    {
        if (CurrentProject != null && CurrentProject.IsDirty)
        {
            await SaveProjectAsync(CurrentProject);
        }

        CurrentProject = null;
        ProjectClosed?.Invoke(this, EventArgs.Empty);
    }

    public MusicScript CreateScript(MusicProject project, string name, string? folder = null)
    {
        var projectDir = Path.GetDirectoryName(project.FilePath)!;
        var scriptsDir = Path.Combine(projectDir, "Scripts");

        if (!string.IsNullOrEmpty(folder))
        {
            scriptsDir = Path.Combine(scriptsDir, folder);
            Directory.CreateDirectory(scriptsDir);
        }

        var filePath = Path.Combine(scriptsDir, $"{name}{ScriptExtension}");
        var ns = string.IsNullOrEmpty(folder)
            ? $"{project.Namespace}.Scripts"
            : $"{project.Namespace}.Scripts.{folder.Replace('/', '.').Replace('\\', '.')}";

        return CreateDefaultScript(project, name, ns, filePath, false);
    }

    private MusicScript CreateDefaultScript(MusicProject project, string name,
        string? ns = null, string? filePath = null, bool isEntryPoint = false)
    {
        var projectDir = Path.GetDirectoryName(project.FilePath)!;
        ns ??= $"{project.Namespace}.Scripts";
        filePath ??= Path.Combine(projectDir, "Scripts", $"{name}{ScriptExtension}");

        string content;

        if (isEntryPoint && !string.IsNullOrEmpty(TestScriptPath) && File.Exists(TestScriptPath))
        {
            // Use the MusicEngine test_script.csx content as the default startup script
            content = File.ReadAllText(TestScriptPath);
        }
        else
        {
            // Fallback to the original template format
            var header = $@"// ============================================
// MusicEngine Script
// Project: {project.Name}
// Namespace: {ns}
// File: {name}{ScriptExtension}
// Created: {DateTime.Now:yyyy-MM-dd}
// ============================================

#project {project.Name}
";

            content = isEntryPoint ? $@"{header}
namespace {ns}
{{
    public class {name} : MusicScript
    {{
        public override void Setup()
        {{
            // Initialize instruments and patterns here
            Bpm = 120;
        }}

        public override void Play()
        {{
            // Start your patterns here
            Pattern.Note(""C4 E4 G4"")
                .Every(1.0)
                .Gain(0.8f)
                .Start();
        }}

        public override void Stop()
        {{
            // Cleanup when stopped
        }}
    }}
}}
" : $@"{header}
namespace {ns}
{{
    public class {name}
    {{
        // Define your instrument, pattern, or utility here
    }}
}}
";
        }

        return new MusicScript
        {
            FilePath = filePath,
            Namespace = ns,
            IsEntryPoint = isEntryPoint,
            Content = content,
            Project = project,
            IsDirty = true
        };
    }

    public async Task SaveScriptAsync(MusicScript script)
    {
        // Update project stamp in header
        if (script.Project != null)
        {
            var lines = script.Content.Split('\n').ToList();
            for (int i = 0; i < Math.Min(15, lines.Count); i++)
            {
                if (lines[i].StartsWith("// Project:"))
                {
                    lines[i] = $"// Project: {script.Project.Name}";
                }
                if (lines[i].StartsWith("#project"))
                {
                    lines[i] = $"#project {script.Project.Name}";
                }
            }
            script.Content = string.Join('\n', lines);
        }

        var dir = Path.GetDirectoryName(script.FilePath)!;
        Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(script.FilePath, script.Content);
        script.IsDirty = false;
        script.LastModified = DateTime.UtcNow;
    }

    public async Task DeleteScriptAsync(MusicScript script)
    {
        if (File.Exists(script.FilePath))
        {
            File.Delete(script.FilePath);
        }

        script.Project?.Scripts.Remove(script);

        if (script.Project != null)
        {
            await SaveProjectAsync(script.Project);
        }
    }

    public async Task<AudioAsset> ImportAudioAsync(MusicProject project, string sourcePath,
        string alias, string category = "General")
    {
        var projectDir = Path.GetDirectoryName(project.FilePath)!;
        var audioDir = Path.Combine(projectDir, "Audio", category);
        Directory.CreateDirectory(audioDir);

        var fileName = Path.GetFileName(sourcePath);
        var destPath = Path.Combine(audioDir, fileName);

        File.Copy(sourcePath, destPath, overwrite: true);

        var asset = new AudioAsset
        {
            FilePath = destPath,
            Alias = alias,
            Category = category
        };

        project.AudioAssets.Add(asset);
        await SaveProjectAsync(project);

        return asset;
    }

    public async Task DeleteAudioAssetAsync(AudioAsset asset)
    {
        if (File.Exists(asset.FilePath))
        {
            File.Delete(asset.FilePath);
        }

        // Find and remove from project
        foreach (var project in new[] { CurrentProject })
        {
            if (project?.AudioAssets.Contains(asset) == true)
            {
                project.AudioAssets.Remove(asset);
                await SaveProjectAsync(project);
                break;
            }
        }
    }

    public async Task AddReferenceAsync(MusicProject project, ProjectReference reference)
    {
        project.References.Add(reference);
        await SaveProjectAsync(project);
    }

    public async Task RemoveReferenceAsync(MusicProject project, ProjectReference reference)
    {
        project.References.Remove(reference);
        await SaveProjectAsync(project);
    }

    /// <inheritdoc />
    public void AddToRecentProjects(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        // Remove existing entry with same path (case-insensitive on Windows)
        var existing = _recentProjects.FirstOrDefault(
            r => string.Equals(r.Path, path, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            _recentProjects.Remove(existing);
        }

        // Add new entry at the beginning
        var entry = new RecentProjectEntry
        {
            Path = path,
            Name = Path.GetFileNameWithoutExtension(path),
            LastOpened = DateTime.UtcNow
        };

        _recentProjects.Insert(0, entry);

        // Trim to max count
        while (_recentProjects.Count > MaxRecentProjects)
        {
            _recentProjects.RemoveAt(_recentProjects.Count - 1);
        }

        SaveRecentProjects();
    }

    /// <inheritdoc />
    public void ClearRecentProjects()
    {
        _recentProjects.Clear();
        SaveRecentProjects();
    }

    private void LoadRecentProjects()
    {
        _recentProjects.Clear();

        if (!File.Exists(_recentProjectsPath))
            return;

        try
        {
            var json = File.ReadAllText(_recentProjectsPath);
            var entries = JsonSerializer.Deserialize<List<RecentProjectEntry>>(json, JsonOptions);

            if (entries != null)
            {
                // Only add entries where the file still exists
                foreach (var entry in entries.Where(e => File.Exists(e.Path)).Take(MaxRecentProjects))
                {
                    _recentProjects.Add(entry);
                }
            }
        }
        catch
        {
            // Ignore errors loading recent projects
        }
    }

    private void SaveRecentProjects()
    {
        try
        {
            var json = JsonSerializer.Serialize(_recentProjects, JsonOptions);
            File.WriteAllText(_recentProjectsPath, json);
        }
        catch
        {
            // Ignore errors saving recent projects
        }
    }

    private static string SanitizeNamespace(string name)
    {
        var result = new System.Text.StringBuilder();
        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                result.Append(c);
            }
        }
        return result.Length > 0 ? result.ToString() : "MusicProject";
    }
}
