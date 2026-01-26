// MusicEngineEditor - Export Preset Service
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MusicEngineEditor.Services;

/// <summary>
/// Audio format for export.
/// </summary>
public enum ExportFormat
{
    WAV,
    MP3,
    FLAC,
    OGG,
    AIFF
}

/// <summary>
/// Naming convention for exported files.
/// </summary>
public enum ExportNamingConvention
{
    /// <summary>Use the project name as filename.</summary>
    ProjectName,
    /// <summary>Use project name with date.</summary>
    ProjectNameDate,
    /// <summary>Use project name with version number.</summary>
    ProjectNameVersion,
    /// <summary>Use project name with date and version.</summary>
    ProjectNameDateVersion,
    /// <summary>Use custom template.</summary>
    Custom
}

/// <summary>
/// Represents an export preset with all export settings.
/// </summary>
public class ExportPreset
{
    /// <summary>
    /// Gets or sets the unique identifier for this preset.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the display name of the preset.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "Untitled Preset";

    /// <summary>
    /// Gets or sets the description of the preset.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets whether this is a built-in preset that cannot be deleted.
    /// </summary>
    [JsonPropertyName("isBuiltIn")]
    public bool IsBuiltIn { get; set; }

    /// <summary>
    /// Gets or sets the export format.
    /// </summary>
    [JsonPropertyName("format")]
    public ExportFormat Format { get; set; } = ExportFormat.WAV;

    /// <summary>
    /// Gets or sets the sample rate in Hz.
    /// </summary>
    [JsonPropertyName("sampleRate")]
    public int SampleRate { get; set; } = 44100;

    /// <summary>
    /// Gets or sets the bit depth (16, 24, 32).
    /// </summary>
    [JsonPropertyName("bitDepth")]
    public int BitDepth { get; set; } = 24;

    /// <summary>
    /// Gets or sets the bit rate for lossy formats in kbps.
    /// </summary>
    [JsonPropertyName("bitRate")]
    public int BitRate { get; set; } = 320;

    /// <summary>
    /// Gets or sets whether to normalize loudness.
    /// </summary>
    [JsonPropertyName("normalizeLoudness")]
    public bool NormalizeLoudness { get; set; }

    /// <summary>
    /// Gets or sets the target LUFS for loudness normalization.
    /// </summary>
    [JsonPropertyName("targetLufs")]
    public double TargetLufs { get; set; } = -14.0;

    /// <summary>
    /// Gets or sets the maximum true peak in dBTP.
    /// </summary>
    [JsonPropertyName("maxTruePeak")]
    public double MaxTruePeak { get; set; } = -1.0;

    /// <summary>
    /// Gets or sets whether to add dither.
    /// </summary>
    [JsonPropertyName("addDither")]
    public bool AddDither { get; set; } = true;

    /// <summary>
    /// Gets or sets the naming convention.
    /// </summary>
    [JsonPropertyName("namingConvention")]
    public ExportNamingConvention NamingConvention { get; set; } = ExportNamingConvention.ProjectName;

    /// <summary>
    /// Gets or sets the custom naming template.
    /// Available variables: {project}, {date}, {time}, {version}, {format}, {samplerate}, {bitdepth}
    /// </summary>
    [JsonPropertyName("customNameTemplate")]
    public string CustomNameTemplate { get; set; } = "{project}_{date}";

    /// <summary>
    /// Gets or sets the default output path.
    /// </summary>
    [JsonPropertyName("outputPath")]
    public string? OutputPath { get; set; }

    /// <summary>
    /// Gets or sets whether to create a subfolder for exports.
    /// </summary>
    [JsonPropertyName("createSubfolder")]
    public bool CreateSubfolder { get; set; }

    /// <summary>
    /// Gets or sets the subfolder name template.
    /// </summary>
    [JsonPropertyName("subfolderTemplate")]
    public string SubfolderTemplate { get; set; } = "Exports";

    /// <summary>
    /// Gets or sets the creation date.
    /// </summary>
    [JsonPropertyName("created")]
    public DateTime Created { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the last modified date.
    /// </summary>
    [JsonPropertyName("modified")]
    public DateTime Modified { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the file extension for the current format.
    /// </summary>
    [JsonIgnore]
    public string FileExtension => Format switch
    {
        ExportFormat.WAV => ".wav",
        ExportFormat.MP3 => ".mp3",
        ExportFormat.FLAC => ".flac",
        ExportFormat.OGG => ".ogg",
        ExportFormat.AIFF => ".aiff",
        _ => ".wav"
    };

    /// <summary>
    /// Creates a deep copy of this preset.
    /// </summary>
    public ExportPreset Clone()
    {
        return new ExportPreset
        {
            Id = Guid.NewGuid().ToString(),
            Name = Name + " (Copy)",
            Description = Description,
            IsBuiltIn = false,
            Format = Format,
            SampleRate = SampleRate,
            BitDepth = BitDepth,
            BitRate = BitRate,
            NormalizeLoudness = NormalizeLoudness,
            TargetLufs = TargetLufs,
            MaxTruePeak = MaxTruePeak,
            AddDither = AddDither,
            NamingConvention = NamingConvention,
            CustomNameTemplate = CustomNameTemplate,
            OutputPath = OutputPath,
            CreateSubfolder = CreateSubfolder,
            SubfolderTemplate = SubfolderTemplate,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Generates a filename based on the naming convention.
    /// </summary>
    public string GenerateFileName(string projectName, int? version = null)
    {
        var now = DateTime.Now;
        var dateStr = now.ToString("yyyy-MM-dd");
        var timeStr = now.ToString("HH-mm-ss");
        var versionStr = version?.ToString() ?? "1";

        string baseName = NamingConvention switch
        {
            ExportNamingConvention.ProjectName => projectName,
            ExportNamingConvention.ProjectNameDate => $"{projectName}_{dateStr}",
            ExportNamingConvention.ProjectNameVersion => $"{projectName}_v{versionStr}",
            ExportNamingConvention.ProjectNameDateVersion => $"{projectName}_{dateStr}_v{versionStr}",
            ExportNamingConvention.Custom => CustomNameTemplate
                .Replace("{project}", projectName)
                .Replace("{date}", dateStr)
                .Replace("{time}", timeStr)
                .Replace("{version}", versionStr)
                .Replace("{format}", Format.ToString().ToLowerInvariant())
                .Replace("{samplerate}", SampleRate.ToString())
                .Replace("{bitdepth}", BitDepth.ToString()),
            _ => projectName
        };

        // Sanitize filename
        var invalidChars = Path.GetInvalidFileNameChars();
        baseName = new string(baseName.Where(c => !invalidChars.Contains(c)).ToArray());

        return baseName + FileExtension;
    }
}

/// <summary>
/// Service for managing export presets with persistence.
/// </summary>
public class ExportPresetService
{
    private static readonly string PresetsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MusicEngineEditor",
        "ExportPresets");

    private static readonly string UserPresetsFile = Path.Combine(PresetsFolder, "user-presets.json");
    private static readonly string LastUsedFile = Path.Combine(PresetsFolder, "last-used.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly List<ExportPreset> _presets = [];

    /// <summary>
    /// Gets all available presets (built-in and user-created).
    /// </summary>
    public ObservableCollection<ExportPreset> Presets { get; } = [];

    /// <summary>
    /// Gets or sets the last used preset.
    /// </summary>
    public ExportPreset? LastUsedPreset { get; private set; }

    /// <summary>
    /// Event raised when presets are loaded or modified.
    /// </summary>
    public event EventHandler? PresetsChanged;

    /// <summary>
    /// Creates a new ExportPresetService and loads presets.
    /// </summary>
    public ExportPresetService()
    {
        EnsureDirectoryExists();
        InitializeBuiltInPresets();
        LoadUserPresets();
        LoadLastUsed();
        RefreshCollection();
    }

    /// <summary>
    /// Gets a preset by its ID.
    /// </summary>
    public ExportPreset? GetPresetById(string id)
    {
        return _presets.FirstOrDefault(p => p.Id == id);
    }

    /// <summary>
    /// Gets a preset by its name.
    /// </summary>
    public ExportPreset? GetPresetByName(string name)
    {
        return _presets.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Sets the last used preset and saves it.
    /// </summary>
    public void SetLastUsed(ExportPreset preset)
    {
        LastUsedPreset = preset;
        SaveLastUsed();
    }

    /// <summary>
    /// Creates a new user preset.
    /// </summary>
    public ExportPreset CreatePreset(string name)
    {
        var preset = new ExportPreset
        {
            Name = name,
            IsBuiltIn = false
        };

        _presets.Add(preset);
        RefreshCollection();
        SaveUserPresets();
        PresetsChanged?.Invoke(this, EventArgs.Empty);

        return preset;
    }

    /// <summary>
    /// Creates a new preset from the current export settings.
    /// </summary>
    public ExportPreset CreatePresetFromSettings(
        string name,
        ExportFormat format,
        int sampleRate,
        int bitDepth,
        int bitRate = 320,
        bool normalizeLoudness = false,
        double targetLufs = -14.0,
        double maxTruePeak = -1.0)
    {
        var preset = new ExportPreset
        {
            Name = name,
            IsBuiltIn = false,
            Format = format,
            SampleRate = sampleRate,
            BitDepth = bitDepth,
            BitRate = bitRate,
            NormalizeLoudness = normalizeLoudness,
            TargetLufs = targetLufs,
            MaxTruePeak = maxTruePeak
        };

        _presets.Add(preset);
        RefreshCollection();
        SaveUserPresets();
        PresetsChanged?.Invoke(this, EventArgs.Empty);

        return preset;
    }

    /// <summary>
    /// Duplicates an existing preset.
    /// </summary>
    public ExportPreset DuplicatePreset(ExportPreset source)
    {
        var clone = source.Clone();
        _presets.Add(clone);
        RefreshCollection();
        SaveUserPresets();
        PresetsChanged?.Invoke(this, EventArgs.Empty);

        return clone;
    }

    /// <summary>
    /// Updates an existing preset.
    /// </summary>
    public void UpdatePreset(ExportPreset preset)
    {
        if (preset.IsBuiltIn)
        {
            throw new InvalidOperationException("Cannot modify built-in presets.");
        }

        preset.Modified = DateTime.UtcNow;
        SaveUserPresets();
        PresetsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Deletes a user preset.
    /// </summary>
    public bool DeletePreset(ExportPreset preset)
    {
        if (preset.IsBuiltIn)
        {
            throw new InvalidOperationException("Cannot delete built-in presets.");
        }

        if (_presets.Remove(preset))
        {
            if (LastUsedPreset == preset)
            {
                LastUsedPreset = _presets.FirstOrDefault();
                SaveLastUsed();
            }

            RefreshCollection();
            SaveUserPresets();
            PresetsChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Exports presets to a JSON file.
    /// </summary>
    public void ExportPresets(IEnumerable<ExportPreset> presets, string filePath)
    {
        var json = JsonSerializer.Serialize(presets.ToList(), JsonOptions);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Imports presets from a JSON file.
    /// </summary>
    public IList<ExportPreset> ImportPresets(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Presets file not found.", filePath);
        }

        var json = File.ReadAllText(filePath);
        var imported = JsonSerializer.Deserialize<List<ExportPreset>>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize presets.");

        var addedPresets = new List<ExportPreset>();

        foreach (var preset in imported)
        {
            // Ensure imported preset is not marked as built-in
            preset.IsBuiltIn = false;
            preset.Id = Guid.NewGuid().ToString();

            // Ensure unique name
            var baseName = preset.Name;
            var counter = 1;
            while (_presets.Any(p => p.Name.Equals(preset.Name, StringComparison.OrdinalIgnoreCase)))
            {
                preset.Name = $"{baseName} ({counter++})";
            }

            _presets.Add(preset);
            addedPresets.Add(preset);
        }

        RefreshCollection();
        SaveUserPresets();
        PresetsChanged?.Invoke(this, EventArgs.Empty);

        return addedPresets;
    }

    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(PresetsFolder))
        {
            Directory.CreateDirectory(PresetsFolder);
        }
    }

    private void InitializeBuiltInPresets()
    {
        // CD Quality WAV
        _presets.Add(new ExportPreset
        {
            Id = "builtin-cd-wav",
            Name = "CD Quality (WAV)",
            Description = "Standard CD quality: 44.1kHz, 16-bit WAV",
            IsBuiltIn = true,
            Format = ExportFormat.WAV,
            SampleRate = 44100,
            BitDepth = 16,
            AddDither = true
        });

        // High Resolution WAV
        _presets.Add(new ExportPreset
        {
            Id = "builtin-hires-wav",
            Name = "High Resolution (WAV)",
            Description = "High-resolution audio: 96kHz, 24-bit WAV",
            IsBuiltIn = true,
            Format = ExportFormat.WAV,
            SampleRate = 96000,
            BitDepth = 24,
            AddDither = false
        });

        // Studio Master WAV
        _presets.Add(new ExportPreset
        {
            Id = "builtin-studio-wav",
            Name = "Studio Master (WAV)",
            Description = "Professional master: 48kHz, 24-bit WAV",
            IsBuiltIn = true,
            Format = ExportFormat.WAV,
            SampleRate = 48000,
            BitDepth = 24,
            AddDither = false
        });

        // MP3 320kbps
        _presets.Add(new ExportPreset
        {
            Id = "builtin-mp3-320",
            Name = "MP3 High Quality",
            Description = "High quality MP3: 320kbps CBR",
            IsBuiltIn = true,
            Format = ExportFormat.MP3,
            SampleRate = 44100,
            BitRate = 320
        });

        // MP3 256kbps
        _presets.Add(new ExportPreset
        {
            Id = "builtin-mp3-256",
            Name = "MP3 Standard",
            Description = "Standard quality MP3: 256kbps CBR",
            IsBuiltIn = true,
            Format = ExportFormat.MP3,
            SampleRate = 44100,
            BitRate = 256
        });

        // FLAC Lossless
        _presets.Add(new ExportPreset
        {
            Id = "builtin-flac",
            Name = "FLAC Lossless",
            Description = "Lossless compression: 44.1kHz, 24-bit FLAC",
            IsBuiltIn = true,
            Format = ExportFormat.FLAC,
            SampleRate = 44100,
            BitDepth = 24
        });

        // Streaming Optimized
        _presets.Add(new ExportPreset
        {
            Id = "builtin-streaming",
            Name = "Streaming Optimized",
            Description = "Optimized for streaming: -14 LUFS, -1 dBTP",
            IsBuiltIn = true,
            Format = ExportFormat.WAV,
            SampleRate = 44100,
            BitDepth = 24,
            NormalizeLoudness = true,
            TargetLufs = -14.0,
            MaxTruePeak = -1.0
        });

        // Broadcast Standard
        _presets.Add(new ExportPreset
        {
            Id = "builtin-broadcast",
            Name = "Broadcast Standard",
            Description = "EBU R128 broadcast: -23 LUFS, -1 dBTP",
            IsBuiltIn = true,
            Format = ExportFormat.WAV,
            SampleRate = 48000,
            BitDepth = 24,
            NormalizeLoudness = true,
            TargetLufs = -23.0,
            MaxTruePeak = -1.0
        });
    }

    private void LoadUserPresets()
    {
        if (!File.Exists(UserPresetsFile))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(UserPresetsFile);
            var userPresets = JsonSerializer.Deserialize<List<ExportPreset>>(json, JsonOptions);

            if (userPresets != null)
            {
                foreach (var preset in userPresets)
                {
                    preset.IsBuiltIn = false;
                    _presets.Add(preset);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load user export presets: {ex.Message}");
        }
    }

    private void SaveUserPresets()
    {
        try
        {
            var userPresets = _presets.Where(p => !p.IsBuiltIn).ToList();
            var json = JsonSerializer.Serialize(userPresets, JsonOptions);
            File.WriteAllText(UserPresetsFile, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save user export presets: {ex.Message}");
        }
    }

    private void LoadLastUsed()
    {
        if (!File.Exists(LastUsedFile))
        {
            LastUsedPreset = _presets.FirstOrDefault();
            return;
        }

        try
        {
            var json = File.ReadAllText(LastUsedFile);
            var lastUsedId = JsonSerializer.Deserialize<string>(json, JsonOptions);
            LastUsedPreset = _presets.FirstOrDefault(p => p.Id == lastUsedId) ?? _presets.FirstOrDefault();
        }
        catch
        {
            LastUsedPreset = _presets.FirstOrDefault();
        }
    }

    private void SaveLastUsed()
    {
        try
        {
            var json = JsonSerializer.Serialize(LastUsedPreset?.Id, JsonOptions);
            File.WriteAllText(LastUsedFile, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save last used preset: {ex.Message}");
        }
    }

    private void RefreshCollection()
    {
        Presets.Clear();
        foreach (var preset in _presets.OrderBy(p => p.IsBuiltIn ? 0 : 1).ThenBy(p => p.Name))
        {
            Presets.Add(preset);
        }
    }
}
