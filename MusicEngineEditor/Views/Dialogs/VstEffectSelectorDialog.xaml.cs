using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MusicEngine.Core;
using MusicEngine.Core.Vst;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Dialog for selecting VST effect plugins to add to a channel.
/// Filters to show only effects (not instruments) and supports category filtering and search.
/// </summary>
public partial class VstEffectSelectorDialog : Window, INotifyPropertyChanged
{
    private string _searchText = "";
    private EffectCategory? _selectedCategory;
    private EffectPluginItem? _selectedPlugin;
    private bool _showRecentPlugins = true;

    private readonly List<EffectPluginItem> _allPlugins = new();
    private readonly ObservableCollection<EffectPluginItem> _filteredPlugins = new();
    private readonly ObservableCollection<EffectPluginItem> _recentPlugins = new();
    private readonly ObservableCollection<EffectCategory> _categories = new();

    // Static list to persist recent plugins across dialog instances
    private static readonly List<string> _recentPluginPaths = new();
    private const int MaxRecentPlugins = 5;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets the selected plugin after the dialog closes with OK result.
    /// </summary>
    public EffectPluginItem? SelectedPlugin
    {
        get => _selectedPlugin;
        set
        {
            if (_selectedPlugin != value)
            {
                _selectedPlugin = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelection));
            }
        }
    }

    /// <summary>
    /// Gets or sets the search text for filtering plugins.
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                _searchText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSearchText));
                ApplyFilters();
            }
        }
    }

    /// <summary>
    /// Gets whether there is search text entered.
    /// </summary>
    public bool HasSearchText => !string.IsNullOrEmpty(_searchText);

    /// <summary>
    /// Gets or sets the selected category for filtering.
    /// </summary>
    public EffectCategory? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (_selectedCategory != value)
            {
                _selectedCategory = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }
    }

    /// <summary>
    /// Gets the collection of effect categories.
    /// </summary>
    public ObservableCollection<EffectCategory> Categories => _categories;

    /// <summary>
    /// Gets the filtered plugins collection.
    /// </summary>
    public ObservableCollection<EffectPluginItem> FilteredPlugins => _filteredPlugins;

    /// <summary>
    /// Gets the recent plugins collection.
    /// </summary>
    public ObservableCollection<EffectPluginItem> RecentPlugins => _recentPlugins;

    /// <summary>
    /// Gets whether there are recent plugins to show.
    /// </summary>
    public bool HasRecentPlugins => _recentPlugins.Count > 0;

    /// <summary>
    /// Gets or sets whether to show the recent plugins section.
    /// </summary>
    public bool ShowRecentPlugins
    {
        get => _showRecentPlugins;
        set
        {
            if (_showRecentPlugins != value)
            {
                _showRecentPlugins = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets whether a plugin is selected.
    /// </summary>
    public bool HasSelection => _selectedPlugin != null;

    /// <summary>
    /// Gets whether to show the empty message.
    /// </summary>
    public bool ShowEmptyMessage => _filteredPlugins.Count == 0 && !string.IsNullOrEmpty(_searchText);

    /// <summary>
    /// Gets the status text showing plugin count.
    /// </summary>
    public string StatusText => $"{_filteredPlugins.Count} effect(s) available";

    /// <summary>
    /// Command to clear the search text.
    /// </summary>
    public ICommand ClearSearchCommand { get; }

    /// <summary>
    /// Creates a new VstEffectSelectorDialog.
    /// </summary>
    /// <param name="vstHost">The VST host containing discovered plugins.</param>
    public VstEffectSelectorDialog(VstHost vstHost)
    {
        InitializeComponent();
        DataContext = this;

        ClearSearchCommand = new RelayCommand(_ => SearchText = "");

        InitializeCategories();
        LoadPlugins(vstHost);
        LoadRecentPlugins();
        ApplyFilters();

        // Handle double-click to select and close
        PluginList.MouseDoubleClick += OnPluginDoubleClick;

        // Focus search box on load
        Loaded += (s, e) => SearchBox.Focus();
    }

    /// <summary>
    /// Creates a new VstEffectSelectorDialog with built-in effects only (no VstHost).
    /// </summary>
    public VstEffectSelectorDialog() : this(new VstHost())
    {
    }

    private void InitializeCategories()
    {
        _categories.Add(new EffectCategory("All", "all", true));
        _categories.Add(new EffectCategory("Dynamics", "dynamics"));
        _categories.Add(new EffectCategory("EQ", "eq"));
        _categories.Add(new EffectCategory("Time-Based", "time"));
        _categories.Add(new EffectCategory("Modulation", "modulation"));
        _categories.Add(new EffectCategory("Distortion", "distortion"));
        _categories.Add(new EffectCategory("VST", "vst"));
        _categories.Add(new EffectCategory("Built-in", "builtin"));

        _selectedCategory = _categories.FirstOrDefault();
    }

    private void LoadPlugins(VstHost vstHost)
    {
        _allPlugins.Clear();

        // Add built-in effects
        AddBuiltInEffects();

        // Add VST2 effect plugins (filter out instruments)
        foreach (var plugin in vstHost.DiscoveredPlugins.Where(p => !p.IsInstrument))
        {
            _allPlugins.Add(new EffectPluginItem
            {
                Name = plugin.Name,
                Vendor = plugin.Vendor,
                Path = plugin.Path,
                Format = "VST2",
                IsVst3 = false,
                IsBuiltIn = false,
                CategoryTag = DetermineCategory(plugin.Name, ""),
                VstPluginInfo = plugin
            });
        }

        // Add VST3 effect plugins (filter out instruments)
        foreach (var plugin in vstHost.DiscoveredVst3Plugins.Where(p => !p.IsInstrument))
        {
            _allPlugins.Add(new EffectPluginItem
            {
                Name = plugin.Name,
                Vendor = plugin.Vendor,
                Path = plugin.Path,
                Format = "VST3",
                IsVst3 = true,
                IsBuiltIn = false,
                CategoryTag = DetermineCategory(plugin.Name, plugin.SubCategories),
                Vst3PluginInfo = plugin
            });
        }

        // Sort by name
        _allPlugins.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        // Update category counts
        UpdateCategoryCounts();
    }

    private void AddBuiltInEffects()
    {
        // Dynamics
        AddBuiltInEffect("Compressor", "MusicEngine", "dynamics", typeof(MusicEngine.Core.Effects.Dynamics.CompressorEffect));
        AddBuiltInEffect("Limiter", "MusicEngine", "dynamics", typeof(MusicEngine.Core.Effects.Dynamics.LimiterEffect));
        AddBuiltInEffect("Gate", "MusicEngine", "dynamics", typeof(MusicEngine.Core.Effects.Dynamics.GateEffect));
        AddBuiltInEffect("Multiband Compressor", "MusicEngine", "dynamics", typeof(MusicEngine.Core.Effects.Dynamics.MultibandCompressor));
        AddBuiltInEffect("Sidechain Compressor", "MusicEngine", "dynamics", typeof(MusicEngine.Core.Effects.Dynamics.SideChainCompressorEffect));

        // EQ / Filters
        AddBuiltInEffect("Parametric EQ", "MusicEngine", "eq", typeof(MusicEngine.Core.Effects.Filters.ParametricEQEffect));
        AddBuiltInEffect("Filter", "MusicEngine", "eq", typeof(MusicEngine.Core.Effects.Filters.FilterEffect));

        // Time-Based
        AddBuiltInEffect("Reverb", "MusicEngine", "time", typeof(MusicEngine.Core.ReverbEffect));
        AddBuiltInEffect("Enhanced Reverb", "MusicEngine", "time", typeof(MusicEngine.Core.Effects.TimeBased.EnhancedReverbEffect));
        AddBuiltInEffect("Convolution Reverb", "MusicEngine", "time", typeof(MusicEngine.Core.Effects.TimeBased.ConvolutionReverb));
        AddBuiltInEffect("Delay", "MusicEngine", "time", typeof(MusicEngine.Core.DelayEffect));
        AddBuiltInEffect("Enhanced Delay", "MusicEngine", "time", typeof(MusicEngine.Core.Effects.TimeBased.EnhancedDelayEffect));

        // Modulation
        AddBuiltInEffect("Chorus", "MusicEngine", "modulation", typeof(MusicEngine.Core.ChorusEffect));
        AddBuiltInEffect("Enhanced Chorus", "MusicEngine", "modulation", typeof(MusicEngine.Core.Effects.Modulation.EnhancedChorusEffect));
        AddBuiltInEffect("Flanger", "MusicEngine", "modulation", typeof(MusicEngine.Core.Effects.Modulation.FlangerEffect));
        AddBuiltInEffect("Phaser", "MusicEngine", "modulation", typeof(MusicEngine.Core.Effects.Modulation.PhaserEffect));
        AddBuiltInEffect("Tremolo", "MusicEngine", "modulation", typeof(MusicEngine.Core.Effects.Modulation.TremoloEffect));
        AddBuiltInEffect("Vibrato", "MusicEngine", "modulation", typeof(MusicEngine.Core.Effects.Modulation.VibratoEffect));

        // Distortion
        AddBuiltInEffect("Distortion", "MusicEngine", "distortion", typeof(MusicEngine.Core.Effects.Distortion.DistortionEffect));
        AddBuiltInEffect("Bitcrusher", "MusicEngine", "distortion", typeof(MusicEngine.Core.Effects.Distortion.BitcrusherEffect));
        AddBuiltInEffect("Tape Saturation", "MusicEngine", "distortion", typeof(MusicEngine.Core.Effects.TapeSaturation));
        AddBuiltInEffect("Exciter", "MusicEngine", "distortion", typeof(MusicEngine.Core.Effects.Exciter));

        // Other
        AddBuiltInEffect("Stereo Widener", "MusicEngine", "modulation", typeof(MusicEngine.Core.Effects.StereoWidener));
        AddBuiltInEffect("Vocoder", "MusicEngine", "modulation", typeof(MusicEngine.Core.Effects.Vocoder));
    }

    private void AddBuiltInEffect(string name, string vendor, string category, Type effectType)
    {
        _allPlugins.Add(new EffectPluginItem
        {
            Name = name,
            Vendor = vendor,
            Path = effectType.FullName ?? effectType.Name,
            Format = "INT",
            IsVst3 = false,
            IsBuiltIn = true,
            CategoryTag = category,
            EffectType = effectType
        });
    }

    private string DetermineCategory(string name, string subCategories)
    {
        var lowerName = name.ToLowerInvariant();
        var lowerSub = subCategories?.ToLowerInvariant() ?? "";

        // Check subcategories first (VST3)
        if (lowerSub.Contains("dynamics") || lowerSub.Contains("compressor") || lowerSub.Contains("limiter") || lowerSub.Contains("gate"))
            return "dynamics";
        if (lowerSub.Contains("eq") || lowerSub.Contains("filter"))
            return "eq";
        if (lowerSub.Contains("reverb") || lowerSub.Contains("delay"))
            return "time";
        if (lowerSub.Contains("modulation") || lowerSub.Contains("chorus") || lowerSub.Contains("flanger") || lowerSub.Contains("phaser"))
            return "modulation";
        if (lowerSub.Contains("distortion") || lowerSub.Contains("saturation"))
            return "distortion";

        // Check name
        if (lowerName.Contains("compressor") || lowerName.Contains("limiter") || lowerName.Contains("gate") || lowerName.Contains("dynamics"))
            return "dynamics";
        if (lowerName.Contains("eq") || lowerName.Contains("filter") || lowerName.Contains("equalizer"))
            return "eq";
        if (lowerName.Contains("reverb") || lowerName.Contains("delay") || lowerName.Contains("echo"))
            return "time";
        if (lowerName.Contains("chorus") || lowerName.Contains("flanger") || lowerName.Contains("phaser") || lowerName.Contains("tremolo") || lowerName.Contains("vibrato"))
            return "modulation";
        if (lowerName.Contains("distortion") || lowerName.Contains("overdrive") || lowerName.Contains("saturation") || lowerName.Contains("bitcrush"))
            return "distortion";

        return "";
    }

    private void UpdateCategoryCounts()
    {
        foreach (var category in _categories)
        {
            category.Count = category.Id switch
            {
                "all" => _allPlugins.Count,
                "vst" => _allPlugins.Count(p => !p.IsBuiltIn),
                "builtin" => _allPlugins.Count(p => p.IsBuiltIn),
                _ => _allPlugins.Count(p => p.CategoryTag == category.Id)
            };
        }
    }

    private void LoadRecentPlugins()
    {
        _recentPlugins.Clear();

        foreach (var path in _recentPluginPaths)
        {
            var plugin = _allPlugins.FirstOrDefault(p => p.Path == path);
            if (plugin != null)
            {
                _recentPlugins.Add(plugin);
            }
        }

        OnPropertyChanged(nameof(HasRecentPlugins));
    }

    private void ApplyFilters()
    {
        _filteredPlugins.Clear();

        var filtered = _allPlugins.AsEnumerable();

        // Apply category filter
        if (_selectedCategory != null && _selectedCategory.Id != "all")
        {
            filtered = _selectedCategory.Id switch
            {
                "vst" => filtered.Where(p => !p.IsBuiltIn),
                "builtin" => filtered.Where(p => p.IsBuiltIn),
                _ => filtered.Where(p => p.CategoryTag == _selectedCategory.Id)
            };
        }

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var searchLower = _searchText.ToLowerInvariant();
            filtered = filtered.Where(p =>
                p.Name.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                p.Vendor.Contains(searchLower, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var plugin in filtered)
        {
            _filteredPlugins.Add(plugin);
        }

        OnPropertyChanged(nameof(ShowEmptyMessage));
        OnPropertyChanged(nameof(StatusText));
    }

    private void OnPluginDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement element)
        {
            var listBoxItem = FindParent<ListBoxItem>(element);
            if (listBoxItem != null && _selectedPlugin != null)
            {
                AddAndClose();
            }
        }
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);

        while (parent != null)
        {
            if (parent is T typedParent)
            {
                return typedParent;
            }
            parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
        }

        return null;
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        AddAndClose();
    }

    private void AddAndClose()
    {
        if (_selectedPlugin == null)
        {
            MessageBox.Show("Please select an effect to add.",
                "No Effect Selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Add to recent plugins
        AddToRecentPlugins(_selectedPlugin.Path);

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static void AddToRecentPlugins(string path)
    {
        // Remove if already exists
        _recentPluginPaths.Remove(path);

        // Add to front
        _recentPluginPaths.Insert(0, path);

        // Trim to max size
        while (_recentPluginPaths.Count > MaxRecentPlugins)
        {
            _recentPluginPaths.RemoveAt(_recentPluginPaths.Count - 1);
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Represents an effect plugin item in the selector.
/// </summary>
public class EffectPluginItem
{
    /// <summary>Plugin display name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Plugin vendor/manufacturer.</summary>
    public string Vendor { get; set; } = "";

    /// <summary>Full path to the plugin or type name for built-in effects.</summary>
    public string Path { get; set; } = "";

    /// <summary>Format identifier: VST2, VST3, or INT (internal/built-in).</summary>
    public string Format { get; set; } = "";

    /// <summary>True if this is a VST3 plugin.</summary>
    public bool IsVst3 { get; set; }

    /// <summary>True if this is a built-in effect.</summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>Category tag for filtering (dynamics, eq, time, modulation, distortion).</summary>
    public string CategoryTag { get; set; } = "";

    /// <summary>VST2 plugin info (if applicable).</summary>
    public MusicEngine.Core.VstPluginInfo? VstPluginInfo { get; set; }

    /// <summary>VST3 plugin info (if applicable).</summary>
    public MusicEngine.Core.Vst3PluginInfo? Vst3PluginInfo { get; set; }

    /// <summary>Effect type for built-in effects.</summary>
    public Type? EffectType { get; set; }
}

/// <summary>
/// Represents a category in the effect selector.
/// </summary>
public class EffectCategory : INotifyPropertyChanged
{
    private int _count;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Display name of the category.</summary>
    public string Name { get; }

    /// <summary>Internal identifier for the category.</summary>
    public string Id { get; }

    /// <summary>Whether to show the count badge.</summary>
    public bool ShowCount { get; }

    /// <summary>Number of plugins in this category.</summary>
    public int Count
    {
        get => _count;
        set
        {
            if (_count != value)
            {
                _count = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
            }
        }
    }

    public EffectCategory(string name, string id, bool showCount = false)
    {
        Name = name;
        Id = id;
        ShowCount = showCount;
    }
}

/// <summary>
/// Simple relay command implementation.
/// </summary>
internal class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);
}
