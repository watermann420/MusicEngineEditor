using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using MusicEngine.Core;
using MusicEngine.Core.Vst;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Plugin Manager dialog for managing VST2/VST3 plugins.
/// Supports favorites, tags, enabling/disabling, and plugin scanning.
/// </summary>
public partial class PluginManagerDialog : Window, INotifyPropertyChanged
{
    private string _searchText = "";
    private PluginCategory? _selectedCategory;
    private PluginManagerItem? _selectedPlugin;
    private string _statusText = "";
    private string _scanStatus = "";
    private bool _isScanning;

    private readonly VstHost _vstHost;
    private readonly List<PluginManagerItem> _allPlugins = new();
    private readonly ObservableCollection<PluginManagerItem> _filteredPlugins = new();
    private readonly ObservableCollection<PluginCategory> _categories = new();
    private readonly PluginManagerSettings _settings;
    private readonly string _settingsPath;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets or sets the search text.
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
    /// Gets whether there is search text.
    /// </summary>
    public bool HasSearchText => !string.IsNullOrEmpty(_searchText);

    /// <summary>
    /// Gets or sets the selected category.
    /// </summary>
    public PluginCategory? SelectedCategory
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
    /// Gets or sets the selected plugin.
    /// </summary>
    public PluginManagerItem? SelectedPlugin
    {
        get => _selectedPlugin;
        set
        {
            if (_selectedPlugin != value)
            {
                _selectedPlugin = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedPlugin));
            }
        }
    }

    /// <summary>
    /// Gets whether a plugin is selected.
    /// </summary>
    public bool HasSelectedPlugin => _selectedPlugin != null;

    /// <summary>
    /// Gets the filtered plugins collection.
    /// </summary>
    public ObservableCollection<PluginManagerItem> FilteredPlugins => _filteredPlugins;

    /// <summary>
    /// Gets the categories collection.
    /// </summary>
    public ObservableCollection<PluginCategory> Categories => _categories;

    /// <summary>
    /// Gets or sets the status text.
    /// </summary>
    public string StatusText
    {
        get => _statusText;
        set
        {
            if (_statusText != value)
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the scan status.
    /// </summary>
    public string ScanStatus
    {
        get => _scanStatus;
        set
        {
            if (_scanStatus != value)
            {
                _scanStatus = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Creates a new PluginManagerDialog.
    /// </summary>
    /// <param name="vstHost">The VST host.</param>
    public PluginManagerDialog(VstHost vstHost)
    {
        _vstHost = vstHost ?? throw new ArgumentNullException(nameof(vstHost));

        _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MusicEngineEditor", "plugin_manager.json");

        _settings = LoadSettings();

        InitializeComponent();
        DataContext = this;

        InitializeCategories();
        LoadPlugins();
        ApplyFilters();

        Loaded += (s, e) => SearchBox.Focus();
    }

    private void InitializeCategories()
    {
        _categories.Add(new PluginCategory("All", "all"));
        _categories.Add(new PluginCategory("Favorites", "favorites"));
        _categories.Add(new PluginCategory("VST2", "vst2"));
        _categories.Add(new PluginCategory("VST3", "vst3"));
        _categories.Add(new PluginCategory("Effects", "effects"));
        _categories.Add(new PluginCategory("Instruments", "instruments"));
        _categories.Add(new PluginCategory("Disabled", "disabled"));

        _selectedCategory = _categories.FirstOrDefault();
    }

    private void LoadPlugins()
    {
        _allPlugins.Clear();

        // Load VST2 plugins
        foreach (var plugin in _vstHost.DiscoveredPlugins)
        {
            var item = new PluginManagerItem
            {
                Name = plugin.Name,
                Vendor = plugin.Vendor,
                Path = plugin.Path,
                Version = plugin.Version,
                IsVst3 = false,
                IsInstrument = plugin.IsInstrument,
                NumInputs = plugin.NumInputs,
                NumOutputs = plugin.NumOutputs,
                NumParameters = plugin.NumParameters,
                Category = plugin.IsInstrument ? "Instrument" : "Effect"
            };

            ApplySettingsToItem(item);
            _allPlugins.Add(item);
        }

        // Load VST3 plugins
        foreach (var plugin in _vstHost.DiscoveredVst3Plugins)
        {
            var item = new PluginManagerItem
            {
                Name = plugin.Name,
                Vendor = plugin.Vendor,
                Path = plugin.Path,
                Version = plugin.Version,
                IsVst3 = true,
                IsInstrument = plugin.IsInstrument,
                NumInputs = plugin.NumInputs,
                NumOutputs = plugin.NumOutputs,
                NumParameters = plugin.NumParameters,
                Category = !string.IsNullOrEmpty(plugin.SubCategories) ? plugin.SubCategories : (plugin.IsInstrument ? "Instrument" : "Effect")
            };

            ApplySettingsToItem(item);
            _allPlugins.Add(item);
        }

        // Sort by name
        _allPlugins.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        UpdateCategoryCounts();
        UpdateStatusText();
    }

    private void ApplySettingsToItem(PluginManagerItem item)
    {
        var key = item.Path.ToLowerInvariant();
        if (_settings.PluginSettings.TryGetValue(key, out var pluginSettings))
        {
            item.IsFavorite = pluginSettings.IsFavorite;
            item.IsEnabled = pluginSettings.IsEnabled;
            item.Tags = pluginSettings.Tags ?? new List<string>();
        }
    }

    private void UpdateCategoryCounts()
    {
        foreach (var category in _categories)
        {
            category.Count = category.Id switch
            {
                "all" => _allPlugins.Count,
                "favorites" => _allPlugins.Count(p => p.IsFavorite),
                "vst2" => _allPlugins.Count(p => !p.IsVst3),
                "vst3" => _allPlugins.Count(p => p.IsVst3),
                "effects" => _allPlugins.Count(p => !p.IsInstrument),
                "instruments" => _allPlugins.Count(p => p.IsInstrument),
                "disabled" => _allPlugins.Count(p => !p.IsEnabled),
                _ => 0
            };
        }
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
                "favorites" => filtered.Where(p => p.IsFavorite),
                "vst2" => filtered.Where(p => !p.IsVst3),
                "vst3" => filtered.Where(p => p.IsVst3),
                "effects" => filtered.Where(p => !p.IsInstrument),
                "instruments" => filtered.Where(p => p.IsInstrument),
                "disabled" => filtered.Where(p => !p.IsEnabled),
                _ => filtered
            };
        }

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var searchLower = _searchText.ToLowerInvariant();
            filtered = filtered.Where(p =>
                p.Name.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                p.Vendor.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                p.TagsDisplay.Contains(searchLower, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var plugin in filtered)
        {
            _filteredPlugins.Add(plugin);
        }

        UpdateStatusText();
    }

    private void UpdateStatusText()
    {
        var total = _allPlugins.Count;
        var shown = _filteredPlugins.Count;
        var enabled = _allPlugins.Count(p => p.IsEnabled);

        StatusText = $"Showing {shown} of {total} plugins ({enabled} enabled)";
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        SearchText = "";
    }

    private async void RescanPlugins_Click(object sender, RoutedEventArgs e)
    {
        if (_isScanning) return;

        _isScanning = true;
        ScanProgress.Visibility = Visibility.Visible;
        ScanStatus = "Scanning plugins...";

        try
        {
            await Task.Run(() =>
            {
                _vstHost.ScanForPlugins();
            });

            Dispatcher.Invoke(() =>
            {
                LoadPlugins();
                ApplyFilters();
                ScanStatus = $"Scan complete. Found {_allPlugins.Count} plugins.";
            });
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                ScanStatus = $"Scan failed: {ex.Message}";
            });
        }
        finally
        {
            _isScanning = false;
            Dispatcher.Invoke(() =>
            {
                ScanProgress.Visibility = Visibility.Collapsed;
            });
        }
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select VST Plugin Folder",
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            // Add to settings for next scan
            Settings.VstPluginSearchPaths.Add(dialog.SelectedPath);
            ScanStatus = $"Added folder: {dialog.SelectedPath}. Click Rescan to discover plugins.";
        }
    }

    private void FavoriteToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Primitives.ToggleButton toggle &&
            toggle.DataContext is PluginManagerItem item)
        {
            SavePluginSettings(item);
            UpdateCategoryCounts();
        }
    }

    private void EnabledToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.CheckBox checkBox && checkBox.DataContext is PluginManagerItem item)
        {
            SavePluginSettings(item);
            UpdateCategoryCounts();
            UpdateStatusText();
        }
    }

    private void TagsTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_selectedPlugin != null)
        {
            SavePluginSettings(_selectedPlugin);
        }
    }

    private void ShowInExplorer_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPlugin == null) return;

        try
        {
            var path = _selectedPlugin.Path;
            if (File.Exists(path))
            {
                Process.Start("explorer.exe", $"/select,\"{path}\"");
            }
            else if (Directory.Exists(path))
            {
                Process.Start("explorer.exe", path);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open Explorer: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        Close();
    }

    private void SavePluginSettings(PluginManagerItem item)
    {
        var key = item.Path.ToLowerInvariant();
        _settings.PluginSettings[key] = new PluginSettings
        {
            IsFavorite = item.IsFavorite,
            IsEnabled = item.IsEnabled,
            Tags = item.Tags
        };

        SaveSettings();
    }

    private PluginManagerSettings LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<PluginManagerSettings>(json) ?? new PluginManagerSettings();
            }
        }
        catch
        {
            // Ignore errors, return default settings
        }

        return new PluginManagerSettings();
    }

    private void SaveSettings()
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        SaveSettings();
        base.OnClosing(e);
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Represents a plugin item in the plugin manager.
/// </summary>
public class PluginManagerItem : INotifyPropertyChanged
{
    private bool _isFavorite;
    private bool _isEnabled = true;
    private List<string> _tags = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Plugin name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Plugin vendor.</summary>
    public string Vendor { get; set; } = "";

    /// <summary>Plugin path.</summary>
    public string Path { get; set; } = "";

    /// <summary>Plugin version.</summary>
    public string Version { get; set; } = "";

    /// <summary>True if VST3, false if VST2.</summary>
    public bool IsVst3 { get; set; }

    /// <summary>True if instrument, false if effect.</summary>
    public bool IsInstrument { get; set; }

    /// <summary>Number of audio inputs.</summary>
    public int NumInputs { get; set; }

    /// <summary>Number of audio outputs.</summary>
    public int NumOutputs { get; set; }

    /// <summary>Number of parameters.</summary>
    public int NumParameters { get; set; }

    /// <summary>Plugin category.</summary>
    public string Category { get; set; } = "";

    /// <summary>Format display string.</summary>
    public string FormatDisplay => IsVst3 ? "VST3" : "VST2";

    /// <summary>Gets or sets whether this plugin is a favorite.</summary>
    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (_isFavorite != value)
            {
                _isFavorite = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Gets or sets whether this plugin is enabled.</summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled != value)
            {
                _isEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Gets or sets the tags.</summary>
    public List<string> Tags
    {
        get => _tags;
        set
        {
            _tags = value ?? new List<string>();
            OnPropertyChanged();
            OnPropertyChanged(nameof(TagsDisplay));
        }
    }

    /// <summary>Tags as comma-separated string.</summary>
    public string TagsDisplay
    {
        get => string.Join(", ", _tags);
        set
        {
            _tags = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            OnPropertyChanged();
            OnPropertyChanged(nameof(Tags));
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Represents a category in the plugin manager.
/// </summary>
public class PluginCategory : INotifyPropertyChanged
{
    private int _count;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Category name.</summary>
    public string Name { get; }

    /// <summary>Category ID.</summary>
    public string Id { get; }

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

    public PluginCategory(string name, string id)
    {
        Name = name;
        Id = id;
    }
}

/// <summary>
/// Settings for the plugin manager.
/// </summary>
internal class PluginManagerSettings
{
    public Dictionary<string, PluginSettings> PluginSettings { get; set; } = new();
}

/// <summary>
/// Settings for a single plugin.
/// </summary>
internal class PluginSettings
{
    public bool IsFavorite { get; set; }
    public bool IsEnabled { get; set; } = true;
    public List<string>? Tags { get; set; }
}
