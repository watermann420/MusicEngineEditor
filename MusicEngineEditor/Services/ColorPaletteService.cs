// MusicEngineEditor - Color Palette Service
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
/// Represents a color palette with a collection of colors for track styling.
/// </summary>
public class ColorPalette
{
    /// <summary>
    /// Gets or sets the unique identifier for this palette.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the display name of the palette.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "Untitled Palette";

    /// <summary>
    /// Gets or sets the description of the palette.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the list of colors in hex format (e.g., "#FF5500").
    /// </summary>
    [JsonPropertyName("colors")]
    public List<string> Colors { get; set; } = [];

    /// <summary>
    /// Gets or sets whether this is a built-in palette that cannot be deleted.
    /// </summary>
    [JsonPropertyName("isBuiltIn")]
    public bool IsBuiltIn { get; set; }

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
    /// Creates a deep copy of this palette.
    /// </summary>
    public ColorPalette Clone()
    {
        return new ColorPalette
        {
            Id = Guid.NewGuid().ToString(),
            Name = Name + " (Copy)",
            Description = Description,
            Colors = new List<string>(Colors),
            IsBuiltIn = false,
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Service for managing track color palettes with persistence and built-in presets.
/// </summary>
public class ColorPaletteService
{
    private static readonly string PalettesFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MusicEngineEditor",
        "ColorPalettes");

    private static readonly string UserPalettesFile = Path.Combine(PalettesFolder, "user-palettes.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly List<ColorPalette> _palettes = [];

    /// <summary>
    /// Gets all available palettes (built-in and user-created).
    /// </summary>
    public ObservableCollection<ColorPalette> Palettes { get; } = [];

    /// <summary>
    /// Gets the currently selected/active palette.
    /// </summary>
    public ColorPalette? CurrentPalette { get; private set; }

    /// <summary>
    /// Event raised when the current palette changes.
    /// </summary>
    public event EventHandler<ColorPalette?>? CurrentPaletteChanged;

    /// <summary>
    /// Event raised when palettes are loaded or modified.
    /// </summary>
    public event EventHandler? PalettesChanged;

    /// <summary>
    /// Creates a new ColorPaletteService and loads palettes.
    /// </summary>
    public ColorPaletteService()
    {
        EnsureDirectoryExists();
        InitializeBuiltInPalettes();
        LoadUserPalettes();
        RefreshCollection();

        // Set default palette
        CurrentPalette = _palettes.FirstOrDefault(p => p.Name == "Default");
    }

    /// <summary>
    /// Gets a palette by its ID.
    /// </summary>
    public ColorPalette? GetPaletteById(string id)
    {
        return _palettes.FirstOrDefault(p => p.Id == id);
    }

    /// <summary>
    /// Gets a palette by its name.
    /// </summary>
    public ColorPalette? GetPaletteByName(string name)
    {
        return _palettes.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Sets the current active palette.
    /// </summary>
    public void SetCurrentPalette(ColorPalette? palette)
    {
        if (CurrentPalette == palette) return;

        CurrentPalette = palette;
        CurrentPaletteChanged?.Invoke(this, palette);
    }

    /// <summary>
    /// Sets the current active palette by name.
    /// </summary>
    public void SetCurrentPaletteByName(string name)
    {
        var palette = GetPaletteByName(name);
        SetCurrentPalette(palette);
    }

    /// <summary>
    /// Creates a new user palette.
    /// </summary>
    public ColorPalette CreatePalette(string name, List<string>? colors = null, string? description = null)
    {
        var palette = new ColorPalette
        {
            Name = name,
            Description = description,
            Colors = colors ?? GenerateDefaultColors(),
            IsBuiltIn = false
        };

        _palettes.Add(palette);
        RefreshCollection();
        SaveUserPalettes();
        PalettesChanged?.Invoke(this, EventArgs.Empty);

        return palette;
    }

    /// <summary>
    /// Duplicates an existing palette.
    /// </summary>
    public ColorPalette DuplicatePalette(ColorPalette source)
    {
        var clone = source.Clone();
        _palettes.Add(clone);
        RefreshCollection();
        SaveUserPalettes();
        PalettesChanged?.Invoke(this, EventArgs.Empty);

        return clone;
    }

    /// <summary>
    /// Updates an existing palette.
    /// </summary>
    public void UpdatePalette(ColorPalette palette)
    {
        if (palette.IsBuiltIn)
        {
            throw new InvalidOperationException("Cannot modify built-in palettes.");
        }

        palette.Modified = DateTime.UtcNow;
        SaveUserPalettes();
        PalettesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Deletes a user palette.
    /// </summary>
    public bool DeletePalette(ColorPalette palette)
    {
        if (palette.IsBuiltIn)
        {
            throw new InvalidOperationException("Cannot delete built-in palettes.");
        }

        if (_palettes.Remove(palette))
        {
            if (CurrentPalette == palette)
            {
                SetCurrentPalette(_palettes.FirstOrDefault(p => p.Name == "Default"));
            }

            RefreshCollection();
            SaveUserPalettes();
            PalettesChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Exports a palette to a JSON file.
    /// </summary>
    public void ExportPalette(ColorPalette palette, string filePath)
    {
        var json = JsonSerializer.Serialize(palette, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Imports a palette from a JSON file.
    /// </summary>
    public ColorPalette ImportPalette(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Palette file not found.", filePath);
        }

        var json = File.ReadAllText(filePath);
        var palette = JsonSerializer.Deserialize<ColorPalette>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize palette.");

        // Ensure imported palette is not marked as built-in
        palette.IsBuiltIn = false;
        palette.Id = Guid.NewGuid().ToString();

        // Ensure unique name
        var baseName = palette.Name;
        var counter = 1;
        while (_palettes.Any(p => p.Name.Equals(palette.Name, StringComparison.OrdinalIgnoreCase)))
        {
            palette.Name = $"{baseName} ({counter++})";
        }

        _palettes.Add(palette);
        RefreshCollection();
        SaveUserPalettes();
        PalettesChanged?.Invoke(this, EventArgs.Empty);

        return palette;
    }

    /// <summary>
    /// Gets the next color from the current palette for cycling through colors.
    /// </summary>
    public string GetNextColor(int index)
    {
        var palette = CurrentPalette ?? _palettes.FirstOrDefault();
        if (palette == null || palette.Colors.Count == 0)
        {
            return "#4A9EFF"; // Default blue
        }

        return palette.Colors[index % palette.Colors.Count];
    }

    /// <summary>
    /// Applies a palette to a collection of mixer channels.
    /// </summary>
    public void ApplyPaletteToChannels(ColorPalette palette, IEnumerable<Models.MixerChannel> channels)
    {
        int index = 0;
        foreach (var channel in channels)
        {
            if (palette.Colors.Count > 0)
            {
                channel.Color = palette.Colors[index % palette.Colors.Count];
                index++;
            }
        }
    }

    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(PalettesFolder))
        {
            Directory.CreateDirectory(PalettesFolder);
        }
    }

    private void InitializeBuiltInPalettes()
    {
        // Default palette - Professional DAW colors
        _palettes.Add(new ColorPalette
        {
            Id = "builtin-default",
            Name = "Default",
            Description = "Classic DAW track colors",
            IsBuiltIn = true,
            Colors =
            [
                "#4A9EFF", // Blue
                "#FF6B6B", // Red
                "#4ECDC4", // Teal
                "#FFE66D", // Yellow
                "#A78BFA", // Purple
                "#FF8C42", // Orange
                "#6BCB77", // Green
                "#FF6B9D", // Pink
                "#45B7D1", // Cyan
                "#C9B037"  // Gold
            ]
        });

        // Warm palette
        _palettes.Add(new ColorPalette
        {
            Id = "builtin-warm",
            Name = "Warm",
            Description = "Warm sunset-inspired colors",
            IsBuiltIn = true,
            Colors =
            [
                "#FF6B6B", // Coral Red
                "#FF8E53", // Orange
                "#FFA500", // Amber
                "#FFD93D", // Gold
                "#F8B400", // Marigold
                "#FF7F50", // Coral
                "#E74C3C", // Crimson
                "#D4AC0D", // Mustard
                "#F39C12", // Sunflower
                "#C0392B"  // Pomegranate
            ]
        });

        // Cool palette
        _palettes.Add(new ColorPalette
        {
            Id = "builtin-cool",
            Name = "Cool",
            Description = "Cool ocean-inspired colors",
            IsBuiltIn = true,
            Colors =
            [
                "#3498DB", // Blue
                "#1ABC9C", // Turquoise
                "#9B59B6", // Purple
                "#2980B9", // Dark Blue
                "#16A085", // Green Sea
                "#8E44AD", // Wisteria
                "#2C3E50", // Midnight Blue
                "#00CED1", // Dark Turquoise
                "#5DADE2", // Light Blue
                "#48C9B0"  // Medium Turquoise
            ]
        });

        // Neon palette
        _palettes.Add(new ColorPalette
        {
            Id = "builtin-neon",
            Name = "Neon",
            Description = "Vibrant neon colors",
            IsBuiltIn = true,
            Colors =
            [
                "#FF00FF", // Magenta
                "#00FFFF", // Cyan
                "#FF1493", // Deep Pink
                "#00FF00", // Lime
                "#FF6600", // Neon Orange
                "#FFFF00", // Yellow
                "#FF0066", // Electric Pink
                "#00FF99", // Spring Green
                "#9933FF", // Electric Purple
                "#00CCFF"  // Electric Blue
            ]
        });

        // Pastel palette
        _palettes.Add(new ColorPalette
        {
            Id = "builtin-pastel",
            Name = "Pastel",
            Description = "Soft pastel colors",
            IsBuiltIn = true,
            Colors =
            [
                "#FFB3BA", // Light Pink
                "#BAFFC9", // Light Green
                "#BAE1FF", // Light Blue
                "#FFFFBA", // Light Yellow
                "#FFDFBA", // Peach
                "#E0BBE4", // Lavender
                "#D4F0F0", // Light Cyan
                "#FCE4D6", // Light Coral
                "#D5F5E3", // Mint
                "#FADBD8"  // Blush
            ]
        });
    }

    private void LoadUserPalettes()
    {
        if (!File.Exists(UserPalettesFile))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(UserPalettesFile);
            var userPalettes = JsonSerializer.Deserialize<List<ColorPalette>>(json, JsonOptions);

            if (userPalettes != null)
            {
                foreach (var palette in userPalettes)
                {
                    palette.IsBuiltIn = false;
                    _palettes.Add(palette);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load user palettes: {ex.Message}");
        }
    }

    private void SaveUserPalettes()
    {
        try
        {
            var userPalettes = _palettes.Where(p => !p.IsBuiltIn).ToList();
            var json = JsonSerializer.Serialize(userPalettes, JsonOptions);
            File.WriteAllText(UserPalettesFile, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save user palettes: {ex.Message}");
        }
    }

    private void RefreshCollection()
    {
        Palettes.Clear();
        foreach (var palette in _palettes.OrderBy(p => p.IsBuiltIn ? 0 : 1).ThenBy(p => p.Name))
        {
            Palettes.Add(palette);
        }
    }

    private static List<string> GenerateDefaultColors()
    {
        return
        [
            "#4A9EFF",
            "#FF6B6B",
            "#4ECDC4",
            "#FFE66D",
            "#A78BFA",
            "#FF8C42",
            "#6BCB77",
            "#FF6B9D"
        ];
    }
}
