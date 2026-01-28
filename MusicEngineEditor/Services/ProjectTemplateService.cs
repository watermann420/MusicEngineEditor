// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Service implementation.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Services;

/// <summary>
/// Service for managing project templates.
/// Provides loading, saving, and creation of projects from templates.
/// </summary>
public class ProjectTemplateService : IProjectTemplateService
{
    private static ProjectTemplateService? _instance;
    private static readonly object _lock = new();

    private readonly List<ProjectTemplate> _templates = new();
    private readonly string _templatesFolder;
    private readonly string _builtInTemplatesFolder;
    private bool _isInitialized;

    /// <summary>
    /// Path to the MusicEngine test_script.csx file used as default startup content.
    /// </summary>
    private static readonly string TestScriptPath = GetTestScriptPath();

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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Gets the singleton instance of the ProjectTemplateService.
    /// </summary>
    public static ProjectTemplateService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new ProjectTemplateService();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Gets all loaded templates.
    /// </summary>
    public IReadOnlyList<ProjectTemplate> Templates => _templates.AsReadOnly();

    /// <summary>
    /// Gets the built-in templates.
    /// </summary>
    public IReadOnlyList<ProjectTemplate> BuiltInTemplates =>
        _templates.Where(t => t.IsBuiltIn).ToList().AsReadOnly();

    /// <summary>
    /// Gets user-created templates.
    /// </summary>
    public IReadOnlyList<ProjectTemplate> UserTemplates =>
        _templates.Where(t => !t.IsBuiltIn).ToList().AsReadOnly();

    /// <summary>
    /// Event raised when templates are reloaded.
    /// </summary>
    public event EventHandler? TemplatesChanged;

    /// <summary>
    /// Creates a new ProjectTemplateService.
    /// </summary>
    private ProjectTemplateService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _templatesFolder = Path.Combine(appData, "MusicEngineEditor", "Templates");
        _builtInTemplatesFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");
    }

    /// <summary>
    /// Initializes the service and loads all templates.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        // Ensure directories exist
        Directory.CreateDirectory(_templatesFolder);

        // Load built-in templates first
        LoadBuiltInTemplates();

        // Load user templates from disk
        await LoadUserTemplatesAsync();

        _isInitialized = true;
    }

    /// <summary>
    /// Loads all templates from the templates folder.
    /// </summary>
    public async Task LoadTemplatesAsync()
    {
        _templates.Clear();

        // Load built-in templates
        LoadBuiltInTemplates();

        // Load user templates
        await LoadUserTemplatesAsync();

        TemplatesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Gets templates filtered by category.
    /// </summary>
    /// <param name="category">The category to filter by.</param>
    /// <returns>Templates matching the category.</returns>
    public IReadOnlyList<ProjectTemplate> GetTemplatesByCategory(TemplateCategory category)
    {
        return _templates.Where(t => t.Category == category).ToList().AsReadOnly();
    }

    /// <summary>
    /// Searches templates by name or description.
    /// </summary>
    /// <param name="searchText">Text to search for.</param>
    /// <returns>Matching templates.</returns>
    public IReadOnlyList<ProjectTemplate> SearchTemplates(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return _templates.AsReadOnly();

        var lower = searchText.ToLowerInvariant();
        return _templates
            .Where(t =>
                t.TemplateName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                t.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                t.Tags.Any(tag => tag.Contains(lower, StringComparison.OrdinalIgnoreCase)))
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Gets a template by its ID.
    /// </summary>
    /// <param name="id">The template ID.</param>
    /// <returns>The template, or null if not found.</returns>
    public ProjectTemplate? GetTemplateById(Guid id)
    {
        return _templates.FirstOrDefault(t => t.Id == id);
    }

    /// <summary>
    /// Creates a new MusicProject from a template.
    /// </summary>
    /// <param name="template">The template to use.</param>
    /// <param name="projectName">Name for the new project.</param>
    /// <param name="projectPath">Path where the project will be created.</param>
    /// <returns>The created project.</returns>
    public MusicProject CreateProjectFromTemplate(ProjectTemplate template, string projectName, string projectPath)
    {
        var project = new MusicProject
        {
            Name = projectName,
            Guid = Guid.NewGuid(),
            Namespace = SanitizeNamespace(projectName),
            FilePath = Path.Combine(projectPath, projectName, $"{projectName}.meproj"),
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            Settings = new ProjectSettings
            {
                SampleRate = template.SampleRate,
                BufferSize = template.BufferSize,
                DefaultBpm = template.DefaultBpm
            }
        };

        // Create project directory structure
        var projectDir = Path.GetDirectoryName(project.FilePath)!;
        Directory.CreateDirectory(projectDir);
        Directory.CreateDirectory(Path.Combine(projectDir, "Scripts"));
        Directory.CreateDirectory(Path.Combine(projectDir, "Audio"));
        Directory.CreateDirectory(Path.Combine(projectDir, "bin"));
        Directory.CreateDirectory(Path.Combine(projectDir, "obj"));

        // Create default Main.me script with template track setup code
        var mainScript = CreateTemplateScript(project, template);
        project.Scripts.Add(mainScript);

        return project;
    }

    /// <summary>
    /// Saves a project as a new template.
    /// </summary>
    /// <param name="project">The project to save as template.</param>
    /// <param name="templateName">Name for the template.</param>
    /// <param name="category">Category for the template.</param>
    /// <param name="description">Description for the template.</param>
    /// <returns>The created template.</returns>
    public async Task<ProjectTemplate> SaveAsTemplateAsync(
        MusicProject project,
        string templateName,
        TemplateCategory category,
        string description = "")
    {
        var template = new ProjectTemplate
        {
            Id = Guid.NewGuid(),
            TemplateName = templateName,
            Description = description,
            Author = Environment.UserName,
            Category = category,
            DefaultBpm = project.Settings.DefaultBpm,
            SampleRate = project.Settings.SampleRate,
            BufferSize = project.Settings.BufferSize,
            IsBuiltIn = false,
            CreatedDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow
        };

        // Save template to disk
        await SaveTemplateAsync(template);

        _templates.Add(template);
        TemplatesChanged?.Invoke(this, EventArgs.Empty);

        return template;
    }

    /// <summary>
    /// Saves a template to disk.
    /// </summary>
    /// <param name="template">The template to save.</param>
    public async Task SaveTemplateAsync(ProjectTemplate template)
    {
        var fileName = $"{SanitizeFileName(template.TemplateName)}.json";
        var filePath = Path.Combine(_templatesFolder, fileName);

        template.ModifiedDate = DateTime.UtcNow;

        var json = JsonSerializer.Serialize(template, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Deletes a user template.
    /// </summary>
    /// <param name="template">The template to delete.</param>
    /// <returns>True if deleted successfully.</returns>
    public bool DeleteTemplate(ProjectTemplate template)
    {
        if (template.IsBuiltIn)
        {
            return false; // Cannot delete built-in templates
        }

        var fileName = $"{SanitizeFileName(template.TemplateName)}.json";
        var filePath = Path.Combine(_templatesFolder, fileName);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        _templates.Remove(template);
        TemplatesChanged?.Invoke(this, EventArgs.Empty);

        return true;
    }

    /// <summary>
    /// Imports a template from a file.
    /// </summary>
    /// <param name="filePath">Path to the template file (.metemplate or .json).</param>
    /// <returns>The imported template.</returns>
    public async Task<ProjectTemplate?> ImportTemplateAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        try
        {
            ProjectTemplate? template = null;
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            if (extension == ".metemplate")
            {
                // Import from zipped template package
                template = await ImportZippedTemplateAsync(filePath);
            }
            else if (extension == ".json")
            {
                // Import from plain JSON
                var json = await File.ReadAllTextAsync(filePath);
                template = JsonSerializer.Deserialize<ProjectTemplate>(json, JsonOptions);
            }

            if (template != null)
            {
                // Generate new ID for imported template
                template.Id = Guid.NewGuid();
                template.IsBuiltIn = false;
                template.ModifiedDate = DateTime.UtcNow;

                // Check for duplicate names
                var baseName = template.TemplateName;
                var counter = 1;
                while (_templates.Any(t => t.TemplateName.Equals(template.TemplateName, StringComparison.OrdinalIgnoreCase)))
                {
                    template.TemplateName = $"{baseName} ({counter++})";
                }

                // Save to user templates folder
                await SaveTemplateAsync(template);

                _templates.Add(template);
                TemplatesChanged?.Invoke(this, EventArgs.Empty);
            }

            return template;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to import template from {filePath}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Exports a template to a file.
    /// </summary>
    /// <param name="template">The template to export.</param>
    /// <param name="filePath">Destination file path.</param>
    /// <param name="asPackage">If true, exports as .metemplate package; otherwise as .json.</param>
    public async Task ExportTemplateAsync(ProjectTemplate template, string filePath, bool asPackage = true)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        if (asPackage || extension == ".metemplate")
        {
            await ExportAsPackageAsync(template, filePath);
        }
        else
        {
            // Export as plain JSON
            var json = JsonSerializer.Serialize(template, JsonOptions);
            await File.WriteAllTextAsync(filePath, json);
        }
    }

    /// <summary>
    /// Creates a template from an existing project.
    /// </summary>
    /// <param name="project">The project to create a template from.</param>
    /// <param name="templateName">Name for the new template.</param>
    /// <param name="category">Category for the template.</param>
    /// <param name="description">Description of the template.</param>
    /// <returns>The created template.</returns>
    public async Task<ProjectTemplate> CreateTemplateFromProjectAsync(
        MusicProject project,
        string templateName,
        TemplateCategory category,
        string description = "")
    {
        var template = new ProjectTemplate
        {
            Id = Guid.NewGuid(),
            TemplateName = templateName,
            Description = string.IsNullOrWhiteSpace(description)
                ? $"Template created from project '{project.Name}'"
                : description,
            Author = Environment.UserName,
            Category = category,
            DefaultBpm = project.Settings.DefaultBpm,
            SampleRate = project.Settings.SampleRate,
            BufferSize = project.Settings.BufferSize,
            IsBuiltIn = false,
            CreatedDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow,
            Tags = new List<string> { "custom", "user-created" }
        };

        // Note: In a full implementation, we would extract track information from the project
        // For now, we create a basic template structure

        // Save the template
        await SaveTemplateAsync(template);

        _templates.Add(template);
        TemplatesChanged?.Invoke(this, EventArgs.Empty);

        return template;
    }

    /// <summary>
    /// Duplicates an existing template.
    /// </summary>
    /// <param name="template">The template to duplicate.</param>
    /// <returns>The duplicated template.</returns>
    public async Task<ProjectTemplate> DuplicateTemplateAsync(ProjectTemplate template)
    {
        var duplicate = template.Clone();
        duplicate.TemplateName = $"{template.TemplateName} (Copy)";
        duplicate.IsBuiltIn = false;
        duplicate.Author = Environment.UserName;

        // Ensure unique name
        var counter = 1;
        var baseName = duplicate.TemplateName;
        while (_templates.Any(t => t.TemplateName.Equals(duplicate.TemplateName, StringComparison.OrdinalIgnoreCase)))
        {
            duplicate.TemplateName = $"{baseName.Replace(" (Copy)", "")} (Copy {counter++})";
        }

        await SaveTemplateAsync(duplicate);

        _templates.Add(duplicate);
        TemplatesChanged?.Invoke(this, EventArgs.Empty);

        return duplicate;
    }

    /// <summary>
    /// Gets all distinct categories that have templates.
    /// </summary>
    public IEnumerable<TemplateCategory> GetUsedCategories()
    {
        return _templates.Select(t => t.Category).Distinct().OrderBy(c => c);
    }

    /// <summary>
    /// Gets the required plugins for a template.
    /// </summary>
    /// <param name="template">The template to check.</param>
    /// <returns>List of required plugin/instrument names.</returns>
    public IReadOnlyList<string> GetRequiredPlugins(ProjectTemplate template)
    {
        var plugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var track in template.Tracks)
        {
            if (!string.IsNullOrEmpty(track.DefaultInstrument))
            {
                plugins.Add(track.DefaultInstrument);
            }

            foreach (var effect in track.DefaultEffects)
            {
                plugins.Add(effect);
            }
        }

        foreach (var effect in template.MasterEffects)
        {
            plugins.Add(effect);
        }

        return plugins.OrderBy(p => p).ToList();
    }

    private async Task<ProjectTemplate?> ImportZippedTemplateAsync(string packagePath)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"metemplate_{Guid.NewGuid()}");

        try
        {
            Directory.CreateDirectory(tempDir);
            ZipFile.ExtractToDirectory(packagePath, tempDir);

            var templateJsonPath = Path.Combine(tempDir, "template.json");
            if (!File.Exists(templateJsonPath))
                return null;

            var json = await File.ReadAllTextAsync(templateJsonPath);
            return JsonSerializer.Deserialize<ProjectTemplate>(json, JsonOptions);
        }
        finally
        {
            // Cleanup temp directory
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); }
                catch { /* Ignore cleanup errors */ }
            }
        }
    }

    private async Task ExportAsPackageAsync(ProjectTemplate template, string packagePath)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"metemplate_{Guid.NewGuid()}");

        try
        {
            Directory.CreateDirectory(tempDir);

            // Write template JSON
            var templateJson = JsonSerializer.Serialize(template, JsonOptions);
            await File.WriteAllTextAsync(Path.Combine(tempDir, "template.json"), templateJson);

            // Write metadata
            var metadata = new
            {
                Version = "1.0",
                ExportDate = DateTime.UtcNow,
                MusicEngineVersion = "1.0.0"
            };
            var metadataJson = JsonSerializer.Serialize(metadata, JsonOptions);
            await File.WriteAllTextAsync(Path.Combine(tempDir, "metadata.json"), metadataJson);

            // Ensure output path has correct extension
            if (!packagePath.EndsWith(".metemplate", StringComparison.OrdinalIgnoreCase))
            {
                packagePath += ".metemplate";
            }

            // Delete existing file if present
            if (File.Exists(packagePath))
            {
                File.Delete(packagePath);
            }

            // Create zip package
            ZipFile.CreateFromDirectory(tempDir, packagePath);
        }
        finally
        {
            // Cleanup temp directory
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); }
                catch { /* Ignore cleanup errors */ }
            }
        }
    }

    /// <summary>
    /// Loads built-in templates.
    /// </summary>
    private void LoadBuiltInTemplates()
    {
        // Empty Project template
        _templates.Add(new ProjectTemplate
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            TemplateName = "Empty Project",
            Description = "A blank canvas with just a master track. Perfect for starting from scratch.",
            Author = "MusicEngine",
            Category = TemplateCategory.Empty,
            IsBuiltIn = true,
            DefaultBpm = 120,
            Tags = new List<string> { "empty", "blank", "minimal" },
            Tracks = new List<TemplateTrack>
            {
                new() { Name = "Master", Type = TemplateTrackType.Master, Color = "#6F737A" }
            }
        });

        // Basic Beat template
        _templates.Add(new ProjectTemplate
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
            TemplateName = "Basic Beat",
            Description = "A simple setup with drums, bass, and synth tracks. Great for quick ideas.",
            Author = "MusicEngine",
            Category = TemplateCategory.Electronic,
            IsBuiltIn = true,
            DefaultBpm = 128,
            Tags = new List<string> { "beat", "drums", "bass", "synth", "electronic" },
            Tracks = new List<TemplateTrack>
            {
                new() { Name = "Drums", Type = TemplateTrackType.Midi, Color = "#FF5722", DefaultInstrument = "SimpleSynth" },
                new() { Name = "Bass", Type = TemplateTrackType.Midi, Color = "#3F51B5", DefaultInstrument = "SimpleSynth" },
                new() { Name = "Synth", Type = TemplateTrackType.Midi, Color = "#9C27B0", DefaultInstrument = "PolySynth" },
                new() { Name = "Master", Type = TemplateTrackType.Master, Color = "#6F737A" }
            }
        });

        // Full Band template
        _templates.Add(new ProjectTemplate
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000003"),
            TemplateName = "Full Band",
            Description = "Complete band setup with drums, bass, guitar, keys, and vocals tracks.",
            Author = "MusicEngine",
            Category = TemplateCategory.Band,
            IsBuiltIn = true,
            DefaultBpm = 120,
            Tags = new List<string> { "band", "rock", "live", "acoustic", "vocals" },
            Tracks = new List<TemplateTrack>
            {
                new() { Name = "Drums", Type = TemplateTrackType.Midi, Color = "#FF5722", DefaultInstrument = "SimpleSynth", Group = "Rhythm" },
                new() { Name = "Bass", Type = TemplateTrackType.Midi, Color = "#3F51B5", DefaultInstrument = "SimpleSynth", Group = "Rhythm" },
                new() { Name = "Guitar", Type = TemplateTrackType.Audio, Color = "#795548", Group = "Instruments" },
                new() { Name = "Keys", Type = TemplateTrackType.Midi, Color = "#009688", DefaultInstrument = "PolySynth", Group = "Instruments" },
                new() { Name = "Vocals", Type = TemplateTrackType.Audio, Color = "#E91E63" },
                new() { Name = "Master", Type = TemplateTrackType.Master, Color = "#6F737A" }
            }
        });

        // Electronic template
        _templates.Add(new ProjectTemplate
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000004"),
            TemplateName = "Electronic",
            Description = "Professional electronic music setup with drums, bass, lead, pad, and FX tracks.",
            Author = "MusicEngine",
            Category = TemplateCategory.Electronic,
            IsBuiltIn = true,
            DefaultBpm = 130,
            Tags = new List<string> { "electronic", "edm", "synth", "dance", "house", "techno" },
            Tracks = new List<TemplateTrack>
            {
                new() { Name = "Kick", Type = TemplateTrackType.Midi, Color = "#FF4757", DefaultInstrument = "SimpleSynth", Group = "Drums" },
                new() { Name = "Snare", Type = TemplateTrackType.Midi, Color = "#FF9800", DefaultInstrument = "SimpleSynth", Group = "Drums" },
                new() { Name = "Hi-Hats", Type = TemplateTrackType.Midi, Color = "#FFC107", DefaultInstrument = "SimpleSynth", Group = "Drums" },
                new() { Name = "Bass", Type = TemplateTrackType.Midi, Color = "#3F51B5", DefaultInstrument = "SimpleSynth" },
                new() { Name = "Lead", Type = TemplateTrackType.Midi, Color = "#9C27B0", DefaultInstrument = "SupersawSynth" },
                new() { Name = "Pad", Type = TemplateTrackType.Midi, Color = "#00BCD4", DefaultInstrument = "PolySynth" },
                new() { Name = "FX", Type = TemplateTrackType.Midi, Color = "#00CC66", DefaultInstrument = "GranularSynth" },
                new() { Name = "Reverb Return", Type = TemplateTrackType.Return, Color = "#607D8B" },
                new() { Name = "Delay Return", Type = TemplateTrackType.Return, Color = "#607D8B" },
                new() { Name = "Master", Type = TemplateTrackType.Master, Color = "#6F737A", DefaultEffects = new List<string> { "Limiter" } }
            }
        });

        // Orchestral Sketch template
        _templates.Add(new ProjectTemplate
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000005"),
            TemplateName = "Orchestral Sketch",
            Description = "Cinematic orchestral template with strings, brass, woodwinds, and percussion sections.",
            Author = "MusicEngine",
            Category = TemplateCategory.Orchestral,
            IsBuiltIn = true,
            DefaultBpm = 90,
            TimeSignatureNumerator = 4,
            TimeSignatureDenominator = 4,
            Tags = new List<string> { "orchestral", "cinematic", "film", "classical", "epic" },
            Tracks = new List<TemplateTrack>
            {
                new() { Name = "Violins I", Type = TemplateTrackType.Midi, Color = "#8B4513", DefaultInstrument = "PolySynth", Group = "Strings" },
                new() { Name = "Violins II", Type = TemplateTrackType.Midi, Color = "#A0522D", DefaultInstrument = "PolySynth", Group = "Strings" },
                new() { Name = "Violas", Type = TemplateTrackType.Midi, Color = "#CD853F", DefaultInstrument = "PolySynth", Group = "Strings" },
                new() { Name = "Cellos", Type = TemplateTrackType.Midi, Color = "#D2691E", DefaultInstrument = "PolySynth", Group = "Strings" },
                new() { Name = "Basses", Type = TemplateTrackType.Midi, Color = "#8B0000", DefaultInstrument = "PolySynth", Group = "Strings" },
                new() { Name = "French Horns", Type = TemplateTrackType.Midi, Color = "#DAA520", DefaultInstrument = "PolySynth", Group = "Brass" },
                new() { Name = "Trumpets", Type = TemplateTrackType.Midi, Color = "#FFD700", DefaultInstrument = "PolySynth", Group = "Brass" },
                new() { Name = "Trombones", Type = TemplateTrackType.Midi, Color = "#B8860B", DefaultInstrument = "PolySynth", Group = "Brass" },
                new() { Name = "Flutes", Type = TemplateTrackType.Midi, Color = "#87CEEB", DefaultInstrument = "PolySynth", Group = "Woodwinds" },
                new() { Name = "Clarinets", Type = TemplateTrackType.Midi, Color = "#4682B4", DefaultInstrument = "PolySynth", Group = "Woodwinds" },
                new() { Name = "Timpani", Type = TemplateTrackType.Midi, Color = "#8B4513", DefaultInstrument = "SimpleSynth", Group = "Percussion" },
                new() { Name = "Percussion", Type = TemplateTrackType.Midi, Color = "#A52A2A", DefaultInstrument = "SimpleSynth", Group = "Percussion" },
                new() { Name = "Reverb Hall", Type = TemplateTrackType.Return, Color = "#607D8B", DefaultEffects = new List<string> { "ConvolutionReverb" } },
                new() { Name = "Master", Type = TemplateTrackType.Master, Color = "#6F737A" }
            }
        });

        // Hip-Hop / Trap template
        _templates.Add(new ProjectTemplate
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000006"),
            TemplateName = "Hip-Hop / Trap",
            Description = "Modern hip-hop and trap production template with 808s, hi-hats, melody, and vocal tracks.",
            Author = "MusicEngine",
            Category = TemplateCategory.HipHop,
            IsBuiltIn = true,
            DefaultBpm = 140,
            TimeSignatureNumerator = 4,
            TimeSignatureDenominator = 4,
            Tags = new List<string> { "hip-hop", "trap", "rap", "urban", "808", "beats" },
            Tracks = new List<TemplateTrack>
            {
                new() { Name = "808 Bass", Type = TemplateTrackType.Midi, Color = "#E91E63", DefaultInstrument = "SimpleSynth", DefaultEffects = new List<string> { "Distortion", "Compressor" } },
                new() { Name = "Kick", Type = TemplateTrackType.Midi, Color = "#FF4757", DefaultInstrument = "SimpleSynth", Group = "Drums" },
                new() { Name = "Snare/Clap", Type = TemplateTrackType.Midi, Color = "#FF9800", DefaultInstrument = "SimpleSynth", Group = "Drums" },
                new() { Name = "Hi-Hats", Type = TemplateTrackType.Midi, Color = "#FFEB3B", DefaultInstrument = "SimpleSynth", Group = "Drums" },
                new() { Name = "Percs", Type = TemplateTrackType.Midi, Color = "#FFC107", DefaultInstrument = "SimpleSynth", Group = "Drums" },
                new() { Name = "Melody", Type = TemplateTrackType.Midi, Color = "#9C27B0", DefaultInstrument = "PolySynth" },
                new() { Name = "Chords", Type = TemplateTrackType.Midi, Color = "#673AB7", DefaultInstrument = "PolySynth" },
                new() { Name = "Lead Vocal", Type = TemplateTrackType.Audio, Color = "#00D9FF", DefaultEffects = new List<string> { "Compressor", "ParametricEQ", "Reverb" } },
                new() { Name = "Ad-libs", Type = TemplateTrackType.Audio, Color = "#03A9F4", DefaultEffects = new List<string> { "Compressor", "Delay" } },
                new() { Name = "FX/Risers", Type = TemplateTrackType.Midi, Color = "#00BCD4", DefaultInstrument = "GranularSynth" },
                new() { Name = "Reverb Return", Type = TemplateTrackType.Return, Color = "#607D8B" },
                new() { Name = "Delay Return", Type = TemplateTrackType.Return, Color = "#78909C" },
                new() { Name = "Master", Type = TemplateTrackType.Master, Color = "#6F737A", DefaultEffects = new List<string> { "MultibandCompressor", "Limiter" } }
            }
        });

        // Podcast template
        _templates.Add(new ProjectTemplate
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000007"),
            TemplateName = "Podcast",
            Description = "Professional podcast recording template with multiple host tracks, guest track, and intro/outro beds.",
            Author = "MusicEngine",
            Category = TemplateCategory.Podcast,
            IsBuiltIn = true,
            DefaultBpm = 120,
            SampleRate = 48000,
            TimeSignatureNumerator = 4,
            TimeSignatureDenominator = 4,
            Tags = new List<string> { "podcast", "voice", "speech", "interview", "recording", "spoken" },
            Tracks = new List<TemplateTrack>
            {
                new() { Name = "Host", Type = TemplateTrackType.Audio, Color = "#00CC66", DefaultEffects = new List<string> { "ParametricEQ", "Compressor", "DeEsser", "Gate" } },
                new() { Name = "Co-Host", Type = TemplateTrackType.Audio, Color = "#8BC34A", DefaultEffects = new List<string> { "ParametricEQ", "Compressor", "DeEsser", "Gate" } },
                new() { Name = "Guest", Type = TemplateTrackType.Audio, Color = "#CDDC39", DefaultEffects = new List<string> { "ParametricEQ", "Compressor", "DeEsser", "NoiseReduction" } },
                new() { Name = "Intro Music", Type = TemplateTrackType.Audio, Color = "#FF9800" },
                new() { Name = "Outro Music", Type = TemplateTrackType.Audio, Color = "#FF5722" },
                new() { Name = "Sound FX", Type = TemplateTrackType.Audio, Color = "#9C27B0" },
                new() { Name = "Background Music", Type = TemplateTrackType.Audio, Color = "#673AB7", Volume = 0.3f },
                new() { Name = "Voice Bus", Type = TemplateTrackType.Bus, Color = "#00D9FF", DefaultEffects = new List<string> { "Compressor" } },
                new() { Name = "Master", Type = TemplateTrackType.Master, Color = "#6F737A", DefaultEffects = new List<string> { "Limiter", "LoudnessNormalizer" } }
            }
        });

        // Film Scoring template
        _templates.Add(new ProjectTemplate
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000008"),
            TemplateName = "Film Scoring",
            Description = "Complete film scoring template with orchestra, synths, sound design, and stems for delivery.",
            Author = "MusicEngine",
            Category = TemplateCategory.FilmScoring,
            IsBuiltIn = true,
            DefaultBpm = 85,
            SampleRate = 48000,
            TimeSignatureNumerator = 4,
            TimeSignatureDenominator = 4,
            Tags = new List<string> { "film", "score", "cinematic", "soundtrack", "media", "video" },
            Tracks = new List<TemplateTrack>
            {
                new() { Name = "Strings Ensemble", Type = TemplateTrackType.Midi, Color = "#8B4513", DefaultInstrument = "PolySynth", Group = "Orchestra" },
                new() { Name = "Brass Section", Type = TemplateTrackType.Midi, Color = "#DAA520", DefaultInstrument = "PolySynth", Group = "Orchestra" },
                new() { Name = "Woodwinds", Type = TemplateTrackType.Midi, Color = "#87CEEB", DefaultInstrument = "PolySynth", Group = "Orchestra" },
                new() { Name = "Choir", Type = TemplateTrackType.Midi, Color = "#E91E63", DefaultInstrument = "PolySynth", Group = "Orchestra" },
                new() { Name = "Synth Pad", Type = TemplateTrackType.Midi, Color = "#9C27B0", DefaultInstrument = "PolySynth", Group = "Synths" },
                new() { Name = "Synth Texture", Type = TemplateTrackType.Midi, Color = "#673AB7", DefaultInstrument = "GranularSynth", Group = "Synths" },
                new() { Name = "Synth Bass", Type = TemplateTrackType.Midi, Color = "#3F51B5", DefaultInstrument = "SimpleSynth", Group = "Synths" },
                new() { Name = "Percussion", Type = TemplateTrackType.Midi, Color = "#FF5722", DefaultInstrument = "SimpleSynth", Group = "Rhythm" },
                new() { Name = "Taikos/Epic Drums", Type = TemplateTrackType.Midi, Color = "#FF4757", DefaultInstrument = "SimpleSynth", Group = "Rhythm" },
                new() { Name = "Risers/Hits", Type = TemplateTrackType.Midi, Color = "#00BCD4", DefaultInstrument = "GranularSynth", Group = "FX" },
                new() { Name = "Drones/Ambience", Type = TemplateTrackType.Midi, Color = "#009688", DefaultInstrument = "GranularSynth", Group = "FX" },
                new() { Name = "Foley/SFX", Type = TemplateTrackType.Audio, Color = "#795548", Group = "FX" },
                new() { Name = "Hall Reverb", Type = TemplateTrackType.Return, Color = "#607D8B", DefaultEffects = new List<string> { "ConvolutionReverb" } },
                new() { Name = "Room Reverb", Type = TemplateTrackType.Return, Color = "#78909C", DefaultEffects = new List<string> { "Reverb" } },
                new() { Name = "Delay", Type = TemplateTrackType.Return, Color = "#90A4AE", DefaultEffects = new List<string> { "Delay" } },
                new() { Name = "Orchestra Bus", Type = TemplateTrackType.Bus, Color = "#5D4037" },
                new() { Name = "Synth Bus", Type = TemplateTrackType.Bus, Color = "#311B92" },
                new() { Name = "Master", Type = TemplateTrackType.Master, Color = "#6F737A", DefaultEffects = new List<string> { "ParametricEQ", "Limiter" } }
            }
        });

        // Lo-Fi / Chill template
        _templates.Add(new ProjectTemplate
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000009"),
            TemplateName = "Lo-Fi Chill",
            Description = "Relaxed lo-fi hip-hop and chill beats template with vinyl textures and warm sounds.",
            Author = "MusicEngine",
            Category = TemplateCategory.Electronic,
            IsBuiltIn = true,
            DefaultBpm = 85,
            TimeSignatureNumerator = 4,
            TimeSignatureDenominator = 4,
            Tags = new List<string> { "lofi", "chill", "study", "beats", "relax", "ambient" },
            Tracks = new List<TemplateTrack>
            {
                new() { Name = "Drums", Type = TemplateTrackType.Midi, Color = "#795548", DefaultInstrument = "SimpleSynth", DefaultEffects = new List<string> { "Bitcrusher", "Filter" } },
                new() { Name = "Bass", Type = TemplateTrackType.Midi, Color = "#5D4037", DefaultInstrument = "SimpleSynth", DefaultEffects = new List<string> { "TapeSaturation" } },
                new() { Name = "Keys/Piano", Type = TemplateTrackType.Midi, Color = "#FFAB91", DefaultInstrument = "PolySynth", DefaultEffects = new List<string> { "Chorus", "TapeSaturation" } },
                new() { Name = "Guitar", Type = TemplateTrackType.Audio, Color = "#A1887F", DefaultEffects = new List<string> { "Chorus", "Reverb" } },
                new() { Name = "Pad", Type = TemplateTrackType.Midi, Color = "#CE93D8", DefaultInstrument = "PolySynth" },
                new() { Name = "Melody", Type = TemplateTrackType.Midi, Color = "#90CAF9", DefaultInstrument = "PolySynth" },
                new() { Name = "Vinyl Noise", Type = TemplateTrackType.Midi, Color = "#607D8B", DefaultInstrument = "NoiseGenerator", Volume = 0.15f },
                new() { Name = "Ambience", Type = TemplateTrackType.Audio, Color = "#80CBC4", Volume = 0.2f },
                new() { Name = "Reverb Return", Type = TemplateTrackType.Return, Color = "#546E7A", DefaultEffects = new List<string> { "Reverb" } },
                new() { Name = "Master", Type = TemplateTrackType.Master, Color = "#6F737A", DefaultEffects = new List<string> { "TapeSaturation", "Compressor" } }
            }
        });

        // Jazz Combo template
        _templates.Add(new ProjectTemplate
        {
            Id = Guid.Parse("00000000-0000-0000-0000-00000000000A"),
            TemplateName = "Jazz Combo",
            Description = "Classic jazz combo setup with piano, bass, drums, and horn section.",
            Author = "MusicEngine",
            Category = TemplateCategory.Jazz,
            IsBuiltIn = true,
            DefaultBpm = 120,
            TimeSignatureNumerator = 4,
            TimeSignatureDenominator = 4,
            Tags = new List<string> { "jazz", "swing", "bebop", "combo", "acoustic", "live" },
            Tracks = new List<TemplateTrack>
            {
                new() { Name = "Piano", Type = TemplateTrackType.Midi, Color = "#37474F", DefaultInstrument = "PolySynth", Group = "Rhythm Section" },
                new() { Name = "Upright Bass", Type = TemplateTrackType.Midi, Color = "#5D4037", DefaultInstrument = "SimpleSynth", Group = "Rhythm Section" },
                new() { Name = "Drums", Type = TemplateTrackType.Midi, Color = "#8D6E63", DefaultInstrument = "SimpleSynth", Group = "Rhythm Section" },
                new() { Name = "Guitar", Type = TemplateTrackType.Audio, Color = "#A1887F", Group = "Rhythm Section" },
                new() { Name = "Trumpet", Type = TemplateTrackType.Midi, Color = "#FFD54F", DefaultInstrument = "PolySynth", Group = "Horns" },
                new() { Name = "Saxophone", Type = TemplateTrackType.Midi, Color = "#FFB74D", DefaultInstrument = "PolySynth", Group = "Horns" },
                new() { Name = "Trombone", Type = TemplateTrackType.Midi, Color = "#FFA726", DefaultInstrument = "PolySynth", Group = "Horns" },
                new() { Name = "Room Reverb", Type = TemplateTrackType.Return, Color = "#607D8B", DefaultEffects = new List<string> { "Reverb" } },
                new() { Name = "Master", Type = TemplateTrackType.Master, Color = "#6F737A" }
            }
        });
    }

    /// <summary>
    /// Loads user templates from the templates folder.
    /// </summary>
    private async Task LoadUserTemplatesAsync()
    {
        if (!Directory.Exists(_templatesFolder))
        {
            Directory.CreateDirectory(_templatesFolder);
            return;
        }

        var files = Directory.GetFiles(_templatesFolder, "*.json");

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var template = JsonSerializer.Deserialize<ProjectTemplate>(json, JsonOptions);

                if (template != null)
                {
                    template.IsBuiltIn = false; // Ensure user templates are marked correctly
                    _templates.Add(template);
                }
            }
            catch (Exception ex)
            {
                // Log error but continue loading other templates
                System.Diagnostics.Debug.WriteLine($"Failed to load template from {file}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Creates a default script for a template-based project.
    /// Uses the MusicEngine test_script.csx content if available.
    /// </summary>
    private MusicScript CreateTemplateScript(MusicProject project, ProjectTemplate template)
    {
        var projectDir = Path.GetDirectoryName(project.FilePath)!;
        var scriptPath = Path.Combine(projectDir, "Scripts", "Main.me");
        var ns = $"{project.Namespace}.Scripts";

        string content;

        // Use MusicEngine test_script.csx content if available
        if (!string.IsNullOrEmpty(TestScriptPath) && File.Exists(TestScriptPath))
        {
            content = File.ReadAllText(TestScriptPath);
        }
        else
        {
            // Fallback to template-based content
            var trackSetup = new System.Text.StringBuilder();
            foreach (var track in template.Tracks)
            {
                if (track.Type == TemplateTrackType.Master)
                    continue;

                trackSetup.AppendLine($"            // {track.Name} Track");
                if (track.Type == TemplateTrackType.Midi && !string.IsNullOrEmpty(track.DefaultInstrument))
                {
                    trackSetup.AppendLine($"            // var {ToVariableName(track.Name)} = new {track.DefaultInstrument}();");
                }
                trackSetup.AppendLine();
            }

            content = $@"// ============================================
// MusicEngine Script
// Project: {project.Name}
// Template: {template.TemplateName}
// Namespace: {ns}
// File: Main.me
// Created: {DateTime.Now:yyyy-MM-dd}
// ============================================

#project {project.Name}

namespace {ns}
{{
    public class Main : MusicScript
    {{
        public override void Setup()
        {{
            // Template: {template.TemplateName}
            // {template.Description}

            Bpm = {template.DefaultBpm};

            // Track Setup:
{trackSetup}
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
";
        }

        return new MusicScript
        {
            FilePath = scriptPath,
            Namespace = ns,
            IsEntryPoint = true,
            Content = content,
            Project = project,
            IsDirty = true
        };
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

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var result = new System.Text.StringBuilder();
        foreach (char c in name)
        {
            if (!invalid.Contains(c))
            {
                result.Append(c);
            }
            else
            {
                result.Append('_');
            }
        }
        return result.ToString();
    }

    private static string ToVariableName(string name)
    {
        var result = new System.Text.StringBuilder();
        var nextUpper = false;

        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c))
            {
                if (result.Length == 0)
                {
                    result.Append(char.ToLowerInvariant(c));
                }
                else if (nextUpper)
                {
                    result.Append(char.ToUpperInvariant(c));
                    nextUpper = false;
                }
                else
                {
                    result.Append(c);
                }
            }
            else
            {
                nextUpper = true;
            }
        }

        return result.Length > 0 ? result.ToString() : "track";
    }
}
