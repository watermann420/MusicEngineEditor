using System.Collections.ObjectModel;
using System.ComponentModel;

namespace MusicEngineEditor.Models;

/// <summary>
/// Represents a preset category with hierarchical subcategories support.
/// Used for organizing presets in a tree structure.
/// </summary>
public class PresetCategory : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private bool _isExpanded = true;
    private bool _isSelected;
    private PresetCategory? _parent;

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
                OnPropertyChanged(nameof(FullPath));
            }
        }
    }

    /// <summary>
    /// Gets or sets the parent category.
    /// </summary>
    public PresetCategory? Parent
    {
        get => _parent;
        set
        {
            if (_parent != value)
            {
                _parent = value;
                OnPropertyChanged(nameof(Parent));
                OnPropertyChanged(nameof(FullPath));
                OnPropertyChanged(nameof(Depth));
            }
        }
    }

    /// <summary>
    /// Gets or sets the child categories (subcategories).
    /// </summary>
    public ObservableCollection<PresetCategory> Children { get; } = [];

    /// <summary>
    /// Gets or sets the presets in this category.
    /// </summary>
    public ObservableCollection<PresetInfo> Presets { get; } = [];

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
    /// Gets or sets whether this category is selected in the tree view.
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
    /// Gets the count of presets in this category only (not including subcategories).
    /// </summary>
    public int DirectCount => Presets.Count;

    /// <summary>
    /// Gets the total count of presets in this category and all subcategories.
    /// </summary>
    public int TotalCount => Presets.Count + Children.Sum(c => c.TotalCount);

    /// <summary>
    /// Gets a display string with the count.
    /// </summary>
    public string DisplayName => $"{Name} ({TotalCount})";

    /// <summary>
    /// Gets the full path of the category (e.g., "Synths/Bass/SubBass").
    /// </summary>
    public string FullPath => Parent != null ? $"{Parent.FullPath}/{Name}" : Name;

    /// <summary>
    /// Gets the depth of this category in the tree (0 for root).
    /// </summary>
    public int Depth => Parent != null ? Parent.Depth + 1 : 0;

    /// <summary>
    /// Gets whether this category has any children.
    /// </summary>
    public bool HasChildren => Children.Count > 0;

    /// <summary>
    /// Gets whether this category has any presets (directly or in subcategories).
    /// </summary>
    public bool HasPresets => TotalCount > 0;

    /// <summary>
    /// Adds a child category.
    /// </summary>
    /// <param name="child">The child category to add.</param>
    public void AddChild(PresetCategory child)
    {
        child.Parent = this;
        Children.Add(child);
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(HasChildren));
    }

    /// <summary>
    /// Removes a child category.
    /// </summary>
    /// <param name="child">The child category to remove.</param>
    /// <returns>True if removed, false otherwise.</returns>
    public bool RemoveChild(PresetCategory child)
    {
        if (Children.Remove(child))
        {
            child.Parent = null;
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(HasChildren));
            return true;
        }
        return false;
    }

    /// <summary>
    /// Adds a preset to this category.
    /// </summary>
    /// <param name="preset">The preset to add.</param>
    public void AddPreset(PresetInfo preset)
    {
        Presets.Add(preset);
        OnPropertyChanged(nameof(DirectCount));
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(HasPresets));
    }

    /// <summary>
    /// Removes a preset from this category.
    /// </summary>
    /// <param name="preset">The preset to remove.</param>
    /// <returns>True if removed, false otherwise.</returns>
    public bool RemovePreset(PresetInfo preset)
    {
        if (Presets.Remove(preset))
        {
            OnPropertyChanged(nameof(DirectCount));
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(HasPresets));
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets or creates a subcategory by name.
    /// </summary>
    /// <param name="name">The name of the subcategory.</param>
    /// <returns>The existing or newly created subcategory.</returns>
    public PresetCategory GetOrCreateChild(string name)
    {
        var existing = Children.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            return existing;

        var newChild = new PresetCategory { Name = name };
        AddChild(newChild);
        return newChild;
    }

    /// <summary>
    /// Finds a category by path (e.g., "Bass/SubBass").
    /// </summary>
    /// <param name="path">The path to search for.</param>
    /// <returns>The found category, or null if not found.</returns>
    public PresetCategory? FindByPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return this;

        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return this;

        var child = Children.FirstOrDefault(c => c.Name.Equals(parts[0], StringComparison.OrdinalIgnoreCase));
        if (child == null)
            return null;

        if (parts.Length == 1)
            return child;

        return child.FindByPath(string.Join('/', parts.Skip(1)));
    }

    /// <summary>
    /// Gets all presets in this category and all subcategories.
    /// </summary>
    /// <returns>An enumerable of all presets.</returns>
    public IEnumerable<PresetInfo> GetAllPresets()
    {
        foreach (var preset in Presets)
            yield return preset;

        foreach (var child in Children)
        {
            foreach (var preset in child.GetAllPresets())
                yield return preset;
        }
    }

    /// <summary>
    /// Expands this category and all parent categories.
    /// </summary>
    public void ExpandToRoot()
    {
        IsExpanded = true;
        Parent?.ExpandToRoot();
    }

    /// <summary>
    /// Collapses this category and all child categories.
    /// </summary>
    public void CollapseAll()
    {
        IsExpanded = false;
        foreach (var child in Children)
            child.CollapseAll();
    }

    /// <summary>
    /// Expands this category and all child categories.
    /// </summary>
    public void ExpandAll()
    {
        IsExpanded = true;
        foreach (var child in Children)
            child.ExpandAll();
    }

    /// <summary>
    /// Event raised when a property changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises the PropertyChanged event.
    /// </summary>
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Creates a category hierarchy from a flat list of presets.
    /// </summary>
    /// <param name="presets">The presets to organize.</param>
    /// <returns>A root category containing all presets organized by their categories.</returns>
    public static PresetCategory CreateFromPresets(IEnumerable<PresetInfo> presets)
    {
        var root = new PresetCategory { Name = "All" };

        foreach (var preset in presets)
        {
            var categoryPath = string.IsNullOrWhiteSpace(preset.Category) ? "Uncategorized" : preset.Category;
            var parts = categoryPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            var current = root;
            foreach (var part in parts)
            {
                current = current.GetOrCreateChild(part);
            }

            current.AddPreset(preset);
        }

        return root;
    }
}
