using System;
using System.Collections.Generic;
using MusicEngine.Core;

namespace MusicEngineEditor.Models;

/// <summary>
/// UI model representing a preset for display in the preset browser.
/// </summary>
public class PresetInfo
{
    /// <summary>
    /// Gets or sets the unique identifier of the preset.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the preset.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the author of the preset.
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the preset.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the category of the preset.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tags associated with the preset.
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Gets or sets the target type (Synth or Effect).
    /// </summary>
    public PresetTargetType TargetType { get; set; }

    /// <summary>
    /// Gets or sets the target class name.
    /// </summary>
    public string TargetClassName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this preset is marked as a favorite.
    /// </summary>
    public bool IsFavorite { get; set; }

    /// <summary>
    /// Gets or sets the rating (0-5).
    /// </summary>
    public int Rating { get; set; }

    /// <summary>
    /// Gets or sets the creation date.
    /// </summary>
    public DateTime CreatedDate { get; set; }

    /// <summary>
    /// Gets or sets the last modification date.
    /// </summary>
    public DateTime ModifiedDate { get; set; }

    /// <summary>
    /// Gets or sets the name of the bank this preset belongs to.
    /// </summary>
    public string BankName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the bank ID this preset belongs to.
    /// </summary>
    public string BankId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the underlying preset object reference.
    /// </summary>
    public Preset? SourcePreset { get; set; }

    /// <summary>
    /// Gets a formatted display string for the target type.
    /// </summary>
    public string TargetTypeDisplay => TargetType switch
    {
        PresetTargetType.Synth => "Synthesizer",
        PresetTargetType.Effect => "Effect",
        _ => "Unknown"
    };

    /// <summary>
    /// Gets a short display string for the creation date.
    /// </summary>
    public string CreatedDateDisplay => CreatedDate.ToLocalTime().ToString("yyyy-MM-dd");

    /// <summary>
    /// Gets a short display string for the modification date.
    /// </summary>
    public string ModifiedDateDisplay => ModifiedDate.ToLocalTime().ToString("yyyy-MM-dd");

    /// <summary>
    /// Gets the tags as a comma-separated string.
    /// </summary>
    public string TagsDisplay => Tags.Count > 0 ? string.Join(", ", Tags) : string.Empty;

    /// <summary>
    /// Gets the first letter of the name for grouping.
    /// </summary>
    public string FirstLetter => string.IsNullOrEmpty(Name) ? "#" : char.ToUpperInvariant(Name[0]).ToString();

    /// <summary>
    /// Creates a PresetInfo from a Preset object.
    /// </summary>
    /// <param name="preset">The source preset.</param>
    /// <param name="bankName">The name of the bank.</param>
    /// <param name="bankId">The ID of the bank.</param>
    /// <returns>A new PresetInfo instance.</returns>
    public static PresetInfo FromPreset(Preset preset, string bankName, string bankId)
    {
        return new PresetInfo
        {
            Id = preset.Id,
            Name = preset.Name,
            Author = preset.Author,
            Description = preset.Description,
            Category = preset.Category,
            Tags = [.. preset.Tags],
            TargetType = preset.TargetType,
            TargetClassName = preset.TargetClassName,
            IsFavorite = preset.IsFavorite,
            Rating = preset.Rating,
            CreatedDate = preset.CreatedDate,
            ModifiedDate = preset.ModifiedDate,
            BankName = bankName,
            BankId = bankId,
            SourcePreset = preset
        };
    }

    /// <summary>
    /// Updates the source preset's favorite status.
    /// </summary>
    public void UpdateFavoriteStatus()
    {
        if (SourcePreset != null)
        {
            SourcePreset.IsFavorite = IsFavorite;
        }
    }

    /// <summary>
    /// Updates the source preset's rating.
    /// </summary>
    public void UpdateRating()
    {
        if (SourcePreset != null)
        {
            SourcePreset.Rating = Rating;
        }
    }
}

/// <summary>
/// Represents a category node in the hierarchical preset view.
/// </summary>
public class PresetCategoryNode : System.ComponentModel.INotifyPropertyChanged
{
    private string _name = string.Empty;
    private bool _isExpanded = true;
    private bool _isSelected;

    /// <summary>
    /// Gets or sets the name of the category.
    /// </summary>
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    /// <summary>
    /// Gets or sets the presets in this category.
    /// </summary>
    public List<PresetInfo> Presets { get; set; } = [];

    /// <summary>
    /// Gets or sets the child categories (subcategories).
    /// </summary>
    public List<PresetCategoryNode> Children { get; set; } = [];

    /// <summary>
    /// Gets or sets whether this category is expanded in the tree view.
    /// </summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                OnPropertyChanged(nameof(IsExpanded));
            }
        }
    }

    /// <summary>
    /// Gets or sets whether this category is selected.
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }
    }

    /// <summary>
    /// Gets the count of presets in this category (including subcategories).
    /// </summary>
    public int Count => Presets.Count + Children.Sum(c => c.Count);

    /// <summary>
    /// Gets a display string with the count.
    /// </summary>
    public string DisplayName => $"{Name} ({Count})";

    /// <summary>
    /// Gets whether this category has any children.
    /// </summary>
    public bool HasChildren => Children.Count > 0;

    /// <summary>
    /// Event raised when a property changes.
    /// </summary>
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises the PropertyChanged event.
    /// </summary>
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Represents a bank node in the hierarchical preset view.
/// </summary>
public class PresetBankNode : System.ComponentModel.INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _name = string.Empty;
    private bool _isExpanded = true;
    private bool _isSelected;

    /// <summary>
    /// Gets or sets the bank ID.
    /// </summary>
    public string Id
    {
        get => _id;
        set
        {
            if (_id != value)
            {
                _id = value;
                OnPropertyChanged(nameof(Id));
            }
        }
    }

    /// <summary>
    /// Gets or sets the name of the bank.
    /// </summary>
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    /// <summary>
    /// Gets or sets the categories in this bank.
    /// </summary>
    public List<PresetCategoryNode> Categories { get; set; } = [];

    /// <summary>
    /// Gets or sets whether this bank is expanded in the tree view.
    /// </summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                OnPropertyChanged(nameof(IsExpanded));
            }
        }
    }

    /// <summary>
    /// Gets or sets whether this bank is selected.
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }
    }

    /// <summary>
    /// Gets the total count of presets in this bank.
    /// </summary>
    public int TotalCount => Categories.Sum(c => c.Count);

    /// <summary>
    /// Gets a display string with the count.
    /// </summary>
    public string DisplayName => $"{Name} ({TotalCount})";

    /// <summary>
    /// Event raised when a property changes.
    /// </summary>
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises the PropertyChanged event.
    /// </summary>
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Represents a tag with its usage count.
/// </summary>
public class TagInfo
{
    /// <summary>
    /// Gets or sets the tag name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the usage count.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets whether this tag is currently selected.
    /// </summary>
    public bool IsSelected { get; set; }

    /// <summary>
    /// Gets the font size based on usage count (for tag cloud).
    /// </summary>
    public double FontSize => Math.Min(24, Math.Max(11, 11 + Math.Log(Count + 1) * 3));
}
