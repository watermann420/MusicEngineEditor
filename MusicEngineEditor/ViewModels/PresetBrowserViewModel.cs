using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngine.Core;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// ViewModel for the Preset Browser control.
/// </summary>
public partial class PresetBrowserViewModel : ViewModelBase
{
    private readonly PresetManager _presetManager;
    private List<PresetInfo> _allPresets = [];

    #region Observable Properties

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = "All";

    [ObservableProperty]
    private PresetTargetType? _selectedTargetType;

    [ObservableProperty]
    private PresetInfo? _selectedPreset;

    [ObservableProperty]
    private PresetBankNode? _selectedBank;

    [ObservableProperty]
    private bool _isPreviewPlaying;

    [ObservableProperty]
    private bool _showOnlyFavorites;

    [ObservableProperty]
    private bool _isGridView = true;

    [ObservableProperty]
    private ObservableCollection<PresetInfo> _filteredPresets = [];

    [ObservableProperty]
    private ObservableCollection<string> _categories = ["All"];

    [ObservableProperty]
    private ObservableCollection<PresetBankNode> _bankNodes = [];

    [ObservableProperty]
    private ObservableCollection<TagInfo> _tags = [];

    [ObservableProperty]
    private ObservableCollection<string> _selectedTags = [];

    [ObservableProperty]
    private string _sortBy = "Name";

    [ObservableProperty]
    private bool _sortAscending = true;

    #endregion

    /// <summary>
    /// Event raised when a preset is selected for loading.
    /// </summary>
    public event EventHandler<PresetInfo>? PresetLoadRequested;

    /// <summary>
    /// Event raised when preview should start.
    /// </summary>
    public event EventHandler<PresetInfo>? PreviewRequested;

    /// <summary>
    /// Event raised when preview should stop.
    /// </summary>
    public event EventHandler? PreviewStopRequested;

    /// <summary>
    /// Creates a new PresetBrowserViewModel with the specified PresetManager.
    /// </summary>
    /// <param name="presetManager">The preset manager to use.</param>
    public PresetBrowserViewModel(PresetManager presetManager)
    {
        _presetManager = presetManager;
        _presetManager.BanksChanged += OnBanksChanged;
        LoadPresets();
    }

    /// <summary>
    /// Parameterless constructor for design-time support.
    /// </summary>
    public PresetBrowserViewModel() : this(new PresetManager())
    {
        // Add some design-time data
        if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(new System.Windows.DependencyObject()))
        {
            FilteredPresets.Add(new PresetInfo { Name = "Epic Bass", Category = "Bass", Author = "Demo" });
            FilteredPresets.Add(new PresetInfo { Name = "Warm Pad", Category = "Pads", Author = "Demo" });
            FilteredPresets.Add(new PresetInfo { Name = "Bright Lead", Category = "Leads", Author = "Demo" });
        }
    }

    #region Property Changed Handlers

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnSelectedCategoryChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnSelectedTargetTypeChanged(PresetTargetType? value)
    {
        ApplyFilters();
    }

    partial void OnShowOnlyFavoritesChanged(bool value)
    {
        ApplyFilters();
    }

    partial void OnSortByChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnSortAscendingChanged(bool value)
    {
        ApplyFilters();
    }

    partial void OnSelectedBankChanged(PresetBankNode? value)
    {
        ApplyFilters();
    }

    #endregion

    #region Private Methods

    private void OnBanksChanged(object? sender, EventArgs e)
    {
        LoadPresets();
    }

    private void LoadPresets()
    {
        _allPresets.Clear();
        BankNodes.Clear();
        Categories.Clear();
        Categories.Add("All");
        Tags.Clear();

        foreach (var bank in _presetManager.Banks)
        {
            var bankNode = new PresetBankNode
            {
                Id = bank.Id,
                Name = bank.Name
            };

            var categoryGroups = bank.Presets
                .GroupBy(p => string.IsNullOrWhiteSpace(p.Category) ? "Uncategorized" : p.Category)
                .OrderBy(g => g.Key);

            foreach (var group in categoryGroups)
            {
                var categoryNode = new PresetCategoryNode { Name = group.Key };

                foreach (var preset in group)
                {
                    var presetInfo = PresetInfo.FromPreset(preset, bank.Name, bank.Id);
                    categoryNode.Presets.Add(presetInfo);
                    _allPresets.Add(presetInfo);
                }

                bankNode.Categories.Add(categoryNode);

                if (!Categories.Contains(group.Key))
                {
                    Categories.Add(group.Key);
                }
            }

            BankNodes.Add(bankNode);
        }

        // Build tag cloud
        var tagStats = _presetManager.GetTagStatistics();
        foreach (var (tag, count) in tagStats.OrderByDescending(t => t.Value).Take(50))
        {
            Tags.Add(new TagInfo { Name = tag, Count = count });
        }

        ApplyFilters();
        StatusMessage = $"{_allPresets.Count} presets loaded";
    }

    private void ApplyFilters()
    {
        var filtered = _allPresets.AsEnumerable();

        // Filter by search text
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var searchLower = SearchText.ToLowerInvariant();
            filtered = filtered.Where(p =>
                p.Name.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                p.Author.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                p.Description.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                p.Tags.Any(t => t.Contains(searchLower, StringComparison.OrdinalIgnoreCase)));
        }

        // Filter by category
        if (SelectedCategory != "All")
        {
            filtered = filtered.Where(p => p.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase));
        }

        // Filter by target type
        if (SelectedTargetType.HasValue)
        {
            filtered = filtered.Where(p => p.TargetType == SelectedTargetType.Value);
        }

        // Filter by favorites
        if (ShowOnlyFavorites)
        {
            filtered = filtered.Where(p => p.IsFavorite);
        }

        // Filter by bank
        if (SelectedBank != null)
        {
            filtered = filtered.Where(p => p.BankId == SelectedBank.Id);
        }

        // Filter by selected tags
        if (SelectedTags.Count > 0)
        {
            filtered = filtered.Where(p => SelectedTags.All(t => p.Tags.Contains(t, StringComparer.OrdinalIgnoreCase)));
        }

        // Apply sorting
        filtered = SortBy switch
        {
            "Name" => SortAscending ? filtered.OrderBy(p => p.Name) : filtered.OrderByDescending(p => p.Name),
            "Author" => SortAscending ? filtered.OrderBy(p => p.Author) : filtered.OrderByDescending(p => p.Author),
            "Category" => SortAscending ? filtered.OrderBy(p => p.Category) : filtered.OrderByDescending(p => p.Category),
            "Date" => SortAscending ? filtered.OrderBy(p => p.ModifiedDate) : filtered.OrderByDescending(p => p.ModifiedDate),
            "Rating" => SortAscending ? filtered.OrderBy(p => p.Rating) : filtered.OrderByDescending(p => p.Rating),
            _ => filtered.OrderBy(p => p.Name)
        };

        FilteredPresets.Clear();
        foreach (var preset in filtered)
        {
            FilteredPresets.Add(preset);
        }

        StatusMessage = $"{FilteredPresets.Count} of {_allPresets.Count} presets";
    }

    #endregion

    #region Commands

    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = string.Empty;
    }

    [RelayCommand]
    private void SelectCategory(string category)
    {
        SelectedCategory = category;
    }

    [RelayCommand]
    private void ToggleGridView()
    {
        IsGridView = !IsGridView;
    }

    [RelayCommand]
    private void ToggleFavorite()
    {
        if (SelectedPreset != null)
        {
            SelectedPreset.IsFavorite = !SelectedPreset.IsFavorite;
            SelectedPreset.UpdateFavoriteStatus();

            // Update the source preset in the manager
            if (SelectedPreset.SourcePreset != null)
            {
                var bank = _presetManager.GetBankById(SelectedPreset.BankId);
                if (bank != null)
                {
                    _presetManager.SavePreset(SelectedPreset.SourcePreset, bank);
                }
            }

            // Re-apply filters if showing favorites only
            if (ShowOnlyFavorites)
            {
                ApplyFilters();
            }
        }
    }

    [RelayCommand]
    private void SetRating(int rating)
    {
        if (SelectedPreset != null)
        {
            SelectedPreset.Rating = rating;
            SelectedPreset.UpdateRating();

            // Update the source preset in the manager
            if (SelectedPreset.SourcePreset != null)
            {
                var bank = _presetManager.GetBankById(SelectedPreset.BankId);
                if (bank != null)
                {
                    _presetManager.SavePreset(SelectedPreset.SourcePreset, bank);
                }
            }
        }
    }

    [RelayCommand]
    private void ToggleTag(TagInfo tag)
    {
        tag.IsSelected = !tag.IsSelected;

        if (tag.IsSelected)
        {
            if (!SelectedTags.Contains(tag.Name))
            {
                SelectedTags.Add(tag.Name);
            }
        }
        else
        {
            SelectedTags.Remove(tag.Name);
        }

        ApplyFilters();
    }

    [RelayCommand]
    private void ClearTags()
    {
        foreach (var tag in Tags)
        {
            tag.IsSelected = false;
        }
        SelectedTags.Clear();
        ApplyFilters();
    }

    [RelayCommand]
    private void LoadPreset()
    {
        if (SelectedPreset != null)
        {
            PresetLoadRequested?.Invoke(this, SelectedPreset);
        }
    }

    [RelayCommand]
    private async Task PreviewAsync()
    {
        if (SelectedPreset == null) return;

        if (IsPreviewPlaying)
        {
            StopPreview();
        }
        else
        {
            IsPreviewPlaying = true;
            PreviewRequested?.Invoke(this, SelectedPreset);

            // Auto-stop after 5 seconds
            await Task.Delay(5000);
            if (IsPreviewPlaying)
            {
                StopPreview();
            }
        }
    }

    [RelayCommand]
    private void StopPreview()
    {
        IsPreviewPlaying = false;
        PreviewStopRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void FilterBySynths()
    {
        SelectedTargetType = SelectedTargetType == PresetTargetType.Synth ? null : PresetTargetType.Synth;
    }

    [RelayCommand]
    private void FilterByEffects()
    {
        SelectedTargetType = SelectedTargetType == PresetTargetType.Effect ? null : PresetTargetType.Effect;
    }

    [RelayCommand]
    private void ToggleFavoritesFilter()
    {
        ShowOnlyFavorites = !ShowOnlyFavorites;
    }

    [RelayCommand]
    private void SetSortBy(string sortField)
    {
        if (SortBy == sortField)
        {
            SortAscending = !SortAscending;
        }
        else
        {
            SortBy = sortField;
            SortAscending = true;
        }
    }

    [RelayCommand]
    private void Refresh()
    {
        LoadPresets();
    }

    [RelayCommand]
    private async Task ScanDirectoryAsync(string? directoryPath)
    {
        if (string.IsNullOrEmpty(directoryPath)) return;

        IsBusy = true;
        StatusMessage = "Scanning for presets...";

        try
        {
            await Task.Run(() => _presetManager.ScanPresets(directoryPath));
            StatusMessage = $"Scan complete. {_allPresets.Count} presets loaded.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Called when a preset is double-clicked.
    /// </summary>
    public void OnPresetDoubleClick()
    {
        LoadPreset();
    }

    /// <summary>
    /// Scans the default preset directories.
    /// </summary>
    public void ScanDefaultDirectories()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var presetsPath = System.IO.Path.Combine(appDataPath, "MusicEngine", "Presets");

        if (System.IO.Directory.Exists(presetsPath))
        {
            _presetManager.ScanPresets(presetsPath);
        }

        // Also scan the executable directory for factory presets
        var exePath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        if (!string.IsNullOrEmpty(exePath))
        {
            var factoryPresetsPath = System.IO.Path.Combine(exePath, "Presets");
            if (System.IO.Directory.Exists(factoryPresetsPath))
            {
                _presetManager.ScanPresets(factoryPresetsPath);
            }
        }
    }

    /// <summary>
    /// Gets the PresetManager instance.
    /// </summary>
    public PresetManager PresetManager => _presetManager;

    /// <summary>
    /// Renames the currently selected preset.
    /// </summary>
    /// <param name="newName">The new name for the preset.</param>
    public void RenameSelectedPreset(string newName)
    {
        if (SelectedPreset?.SourcePreset == null || string.IsNullOrWhiteSpace(newName))
            return;

        SelectedPreset.Name = newName;
        SelectedPreset.SourcePreset.Name = newName;
        SelectedPreset.SourcePreset.ModifiedDate = DateTime.UtcNow;

        // Save the updated preset
        var bank = _presetManager.GetBankById(SelectedPreset.BankId);
        if (bank != null)
        {
            _presetManager.SavePreset(SelectedPreset.SourcePreset, bank);
        }

        // Refresh the filtered list to update the display
        ApplyFilters();
        StatusMessage = $"Preset renamed to '{newName}'";
    }

    /// <summary>
    /// Deletes the currently selected preset.
    /// </summary>
    public void DeleteSelectedPreset()
    {
        if (SelectedPreset?.SourcePreset == null)
            return;

        var presetName = SelectedPreset.Name;
        var bank = _presetManager.GetBankById(SelectedPreset.BankId);

        if (bank != null)
        {
            _presetManager.DeletePreset(SelectedPreset.SourcePreset, bank);
        }

        // Remove from local lists
        _allPresets.Remove(SelectedPreset);
        FilteredPresets.Remove(SelectedPreset);
        SelectedPreset = null;

        ApplyFilters();
        StatusMessage = $"Preset '{presetName}' deleted";
    }

    #endregion
}
