//MusicEngineEditor - Plugin Search Panel
// copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Defines the plugin category.
/// </summary>
public enum PluginCategory
{
    /// <summary>Equalizer.</summary>
    EQ,
    /// <summary>Compressor/Dynamics.</summary>
    Compressor,
    /// <summary>Reverb.</summary>
    Reverb,
    /// <summary>Delay.</summary>
    Delay,
    /// <summary>Modulation effects.</summary>
    Modulation,
    /// <summary>Distortion/Saturation.</summary>
    Distortion,
    /// <summary>Filter.</summary>
    Filter,
    /// <summary>VST Plugin.</summary>
    VST,
    /// <summary>Other/Utility.</summary>
    Other
}

/// <summary>
/// Represents a searchable plugin in the effect chain.
/// </summary>
public partial class SearchablePlugin : ObservableObject
{
    /// <summary>
    /// Gets the plugin ID.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the plugin name.
    /// </summary>
    [ObservableProperty]
    private string _name = "";

    /// <summary>
    /// Gets or sets the plugin category.
    /// </summary>
    [ObservableProperty]
    private PluginCategory _category = PluginCategory.Other;

    /// <summary>
    /// Gets or sets the track name where this plugin is located.
    /// </summary>
    [ObservableProperty]
    private string _trackName = "";

    /// <summary>
    /// Gets or sets the track index.
    /// </summary>
    [ObservableProperty]
    private int _trackIndex;

    /// <summary>
    /// Gets or sets the slot index in the effect chain.
    /// </summary>
    [ObservableProperty]
    private int _slotIndex;

    /// <summary>
    /// Gets or sets whether the plugin is bypassed.
    /// </summary>
    [ObservableProperty]
    private bool _isBypassed;

    /// <summary>
    /// Gets or sets whether the plugin is selected.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Gets or sets the key parameters preview string.
    /// </summary>
    [ObservableProperty]
    private string _parametersPreview = "";

    /// <summary>
    /// Gets the category display color.
    /// </summary>
    public string CategoryColor => Category switch
    {
        PluginCategory.EQ => "#4CAF50",
        PluginCategory.Compressor => "#FF9800",
        PluginCategory.Reverb => "#2196F3",
        PluginCategory.Delay => "#9C27B0",
        PluginCategory.Modulation => "#E91E63",
        PluginCategory.Distortion => "#F44336",
        PluginCategory.Filter => "#00BCD4",
        PluginCategory.VST => "#607D8B",
        _ => "#757575"
    };

    /// <summary>
    /// Creates a new searchable plugin.
    /// </summary>
    public SearchablePlugin() { }

    /// <summary>
    /// Creates a new searchable plugin with specified values.
    /// </summary>
    public SearchablePlugin(string name, PluginCategory category, string trackName, int trackIndex, int slotIndex)
    {
        Name = name;
        Category = category;
        TrackName = trackName;
        TrackIndex = trackIndex;
        SlotIndex = slotIndex;
    }
}

/// <summary>
/// Search panel for finding plugins in effect chains.
/// </summary>
public partial class PluginSearchPanel : UserControl
{
    private readonly ObservableCollection<SearchablePlugin> _allPlugins = [];
    private readonly ObservableCollection<SearchablePlugin> _filteredPlugins = [];
    private string _searchQuery = "";
    private readonly System.Collections.Generic.HashSet<PluginCategory> _activeFilters = [];

    /// <summary>
    /// Gets the collection of all plugins.
    /// </summary>
    public ObservableCollection<SearchablePlugin> AllPlugins => _allPlugins;

    /// <summary>
    /// Event raised when a plugin is selected.
    /// </summary>
    public event EventHandler<SearchablePlugin>? PluginSelected;

    /// <summary>
    /// Event raised when the user requests to navigate to a plugin.
    /// </summary>
    public event EventHandler<SearchablePlugin>? PluginNavigateRequested;

    /// <summary>
    /// Creates a new PluginSearchPanel.
    /// </summary>
    public PluginSearchPanel()
    {
        InitializeComponent();
        PluginList.ItemsSource = _filteredPlugins;

        InitializeSamplePlugins();
        ApplyFilters();
    }

    /// <summary>
    /// Initializes sample plugins for demonstration.
    /// </summary>
    private void InitializeSamplePlugins()
    {
        // Kick track
        _allPlugins.Add(new SearchablePlugin("ParametricEQ", PluginCategory.EQ, "Kick", 0, 0)
        {
            ParametersPreview = "Low Cut: 60Hz, Peak: +3dB @ 100Hz"
        });
        _allPlugins.Add(new SearchablePlugin("Compressor", PluginCategory.Compressor, "Kick", 0, 1)
        {
            ParametersPreview = "Ratio: 4:1, Attack: 10ms, Release: 100ms"
        });

        // Snare track
        _allPlugins.Add(new SearchablePlugin("ParametricEQ", PluginCategory.EQ, "Snare", 1, 0)
        {
            ParametersPreview = "High Pass: 80Hz, Presence: +2dB @ 5kHz"
        });
        _allPlugins.Add(new SearchablePlugin("TransientShaper", PluginCategory.Compressor, "Snare", 1, 1)
        {
            ParametersPreview = "Attack: +10, Sustain: -5"
        });

        // Bass track
        _allPlugins.Add(new SearchablePlugin("Filter", PluginCategory.Filter, "Bass", 3, 0)
        {
            ParametersPreview = "Type: LP, Cutoff: 2kHz, Res: 0.5"
        });
        _allPlugins.Add(new SearchablePlugin("Distortion", PluginCategory.Distortion, "Bass", 3, 1)
        {
            ParametersPreview = "Drive: 30%, Mix: 50%",
            IsBypassed = true
        });
        _allPlugins.Add(new SearchablePlugin("Compressor", PluginCategory.Compressor, "Bass", 3, 2)
        {
            ParametersPreview = "Ratio: 6:1, Knee: Soft"
        });

        // Lead track
        _allPlugins.Add(new SearchablePlugin("Chorus", PluginCategory.Modulation, "Lead Synth", 4, 0)
        {
            ParametersPreview = "Rate: 0.5Hz, Depth: 50%, Mix: 30%"
        });
        _allPlugins.Add(new SearchablePlugin("Delay", PluginCategory.Delay, "Lead Synth", 4, 1)
        {
            ParametersPreview = "Time: 1/4, Feedback: 40%, Mix: 25%"
        });

        // Pad track
        _allPlugins.Add(new SearchablePlugin("EnhancedReverb", PluginCategory.Reverb, "Pad", 5, 0)
        {
            ParametersPreview = "Size: Large, Decay: 3.5s, Mix: 40%"
        });

        // Vocals track
        _allPlugins.Add(new SearchablePlugin("DeEsser", PluginCategory.Compressor, "Vocals", 7, 0)
        {
            ParametersPreview = "Freq: 6kHz, Threshold: -20dB"
        });
        _allPlugins.Add(new SearchablePlugin("ParametricEQ", PluginCategory.EQ, "Vocals", 7, 1)
        {
            ParametersPreview = "High Pass: 100Hz, Air: +3dB @ 12kHz"
        });
        _allPlugins.Add(new SearchablePlugin("FabFilter Pro-Q 3", PluginCategory.VST, "Vocals", 7, 2)
        {
            ParametersPreview = "VST3 Plugin - 8 bands active"
        });

        // Master
        _allPlugins.Add(new SearchablePlugin("MultibandCompressor", PluginCategory.Compressor, "Master", -1, 0)
        {
            ParametersPreview = "3 bands, Auto-release"
        });
        _allPlugins.Add(new SearchablePlugin("Limiter", PluginCategory.Compressor, "Master", -1, 1)
        {
            ParametersPreview = "Ceiling: -0.3dB, Release: Auto"
        });
    }

    /// <summary>
    /// Sets the plugins to search.
    /// </summary>
    /// <param name="plugins">The plugins collection.</param>
    public void SetPlugins(ObservableCollection<SearchablePlugin> plugins)
    {
        _allPlugins.Clear();
        foreach (var plugin in plugins)
        {
            _allPlugins.Add(plugin);
        }
        ApplyFilters();
    }

    /// <summary>
    /// Adds a plugin to the search list.
    /// </summary>
    /// <param name="plugin">The plugin to add.</param>
    public void AddPlugin(SearchablePlugin plugin)
    {
        _allPlugins.Add(plugin);
        ApplyFilters();
    }

    /// <summary>
    /// Clears all plugins.
    /// </summary>
    public void ClearPlugins()
    {
        _allPlugins.Clear();
        _filteredPlugins.Clear();
    }

    /// <summary>
    /// Refreshes the plugin list from effect chains.
    /// </summary>
    public void Refresh()
    {
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        _filteredPlugins.Clear();

        var filtered = _allPlugins.Where(p =>
        {
            // Text search
            if (!string.IsNullOrWhiteSpace(_searchQuery))
            {
                bool matchesSearch = p.Name.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                                    p.TrackName.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                                    p.Category.ToString().Contains(_searchQuery, StringComparison.OrdinalIgnoreCase);
                if (!matchesSearch)
                    return false;
            }

            // Category filter
            if (_activeFilters.Count > 0 && !_activeFilters.Contains(p.Category))
            {
                return false;
            }

            return true;
        });

        foreach (var plugin in filtered.OrderBy(p => p.TrackIndex).ThenBy(p => p.SlotIndex))
        {
            plugin.IsSelected = false;
            _filteredPlugins.Add(plugin);
        }
    }

    private void UpdatePreview(SearchablePlugin plugin)
    {
        PreviewPanel.Visibility = Visibility.Visible;
        PreviewName.Text = $"{plugin.Name} ({plugin.Category})";
        PreviewParams.Text = string.IsNullOrEmpty(plugin.ParametersPreview)
            ? "No parameters preview"
            : plugin.ParametersPreview;
    }

    private void NavigateToPlugin(SearchablePlugin plugin)
    {
        PluginNavigateRequested?.Invoke(this, plugin);
    }

    #region Event Handlers

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchQuery = SearchBox.Text;
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(_searchQuery) ? Visibility.Visible : Visibility.Collapsed;
        ApplyFilters();
    }

    private void FilterAll_Checked(object sender, RoutedEventArgs e)
    {
        _activeFilters.Clear();

        // Uncheck all other filters
        FilterEQ.IsChecked = false;
        FilterCompressor.IsChecked = false;
        FilterReverb.IsChecked = false;
        FilterDelay.IsChecked = false;
        FilterModulation.IsChecked = false;
        FilterDistortion.IsChecked = false;
        FilterVST.IsChecked = false;

        ApplyFilters();
    }

    private void Filter_Checked(object sender, RoutedEventArgs e)
    {
        FilterAll.IsChecked = false;

        if (sender is ToggleButton button)
        {
            var category = GetCategoryFromButton(button);
            if (category.HasValue)
            {
                _activeFilters.Add(category.Value);
            }
        }

        ApplyFilters();
    }

    private void Filter_Unchecked(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton button)
        {
            var category = GetCategoryFromButton(button);
            if (category.HasValue)
            {
                _activeFilters.Remove(category.Value);
            }
        }

        // If no filters active, check "All"
        if (_activeFilters.Count == 0)
        {
            FilterAll.IsChecked = true;
        }

        ApplyFilters();
    }

    private PluginCategory? GetCategoryFromButton(ToggleButton button)
    {
        if (button == FilterEQ) return PluginCategory.EQ;
        if (button == FilterCompressor) return PluginCategory.Compressor;
        if (button == FilterReverb) return PluginCategory.Reverb;
        if (button == FilterDelay) return PluginCategory.Delay;
        if (button == FilterModulation) return PluginCategory.Modulation;
        if (button == FilterDistortion) return PluginCategory.Distortion;
        if (button == FilterVST) return PluginCategory.VST;
        return null;
    }

    private void PluginList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PluginList.SelectedItem is SearchablePlugin plugin)
        {
            // Clear previous selection
            foreach (var p in _filteredPlugins)
            {
                p.IsSelected = false;
            }

            plugin.IsSelected = true;
            UpdatePreview(plugin);
            PluginSelected?.Invoke(this, plugin);
        }
        else
        {
            PreviewPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void PluginList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (PluginList.SelectedItem is SearchablePlugin plugin)
        {
            NavigateToPlugin(plugin);
        }
    }

    private void GoToPlugin_Click(object sender, RoutedEventArgs e)
    {
        if (PluginList.SelectedItem is SearchablePlugin plugin)
        {
            NavigateToPlugin(plugin);
        }
    }

    #endregion
}
