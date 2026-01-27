// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Dialog window implementation.

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Dialog for managing the plugin blacklist.
/// Allows adding, removing, and scanning for problematic plugins.
/// </summary>
public partial class PluginBlacklistDialog : Window
{
    private readonly PluginBlacklistData _blacklistData;
    private readonly ObservableCollection<PluginBlacklistEntry> _blacklistedPlugins;

    /// <summary>
    /// Gets the collection of blacklisted plugins.
    /// </summary>
    public ObservableCollection<PluginBlacklistEntry> BlacklistedPlugins => _blacklistedPlugins;

    /// <summary>
    /// Creates a new PluginBlacklistDialog.
    /// </summary>
    public PluginBlacklistDialog()
    {
        InitializeComponent();

        _blacklistData = PluginBlacklistData.Load();
        _blacklistedPlugins = new ObservableCollection<PluginBlacklistEntry>(_blacklistData.BlacklistedPlugins);

        BlacklistListView.ItemsSource = _blacklistedPlugins;
        UpdateStatusText();

        // Add a simple converter for the IsEnabled binding
        Resources.Add("GreaterThanZeroConverter", new BlacklistGreaterThanZeroConverter());
    }

    /// <summary>
    /// Updates the status text with the current count.
    /// </summary>
    private void UpdateStatusText()
    {
        StatusText.Text = _blacklistedPlugins.Count == 1
            ? "1 plugin blacklisted"
            : $"{_blacklistedPlugins.Count} plugins blacklisted";
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Plugin to Blacklist",
            Filter = "VST Plugins|*.dll;*.vst3|VST3 Plugins|*.vst3|VST2 Plugins|*.dll|All Files|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var filePath in dialog.FileNames)
            {
                var entry = new PluginBlacklistEntry
                {
                    PluginName = Path.GetFileNameWithoutExtension(filePath),
                    PluginPath = filePath,
                    PluginType = filePath.EndsWith(".vst3", StringComparison.OrdinalIgnoreCase) ? "VST3" : "VST2",
                    Reason = "Manually blacklisted",
                    DateAdded = DateTime.UtcNow
                };

                if (!_blacklistData.IsBlacklisted(filePath))
                {
                    _blacklistData.Add(entry);
                    _blacklistedPlugins.Add(entry);
                }
            }

            UpdateStatusText();
        }
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = BlacklistListView.SelectedItems;
        if (selectedItems.Count == 0) return;

        var itemsToRemove = new PluginBlacklistEntry[selectedItems.Count];
        selectedItems.CopyTo(itemsToRemove, 0);

        foreach (var item in itemsToRemove)
        {
            _blacklistData.Remove(item);
            _blacklistedPlugins.Remove(item);
        }

        UpdateStatusText();
    }

    private async void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        ScanButton.IsEnabled = false;
        ScanButton.Content = "Scanning...";
        StatusText.Text = "Scanning plugin directories...";

        try
        {
            await Task.Run(() => ScanForProblematicPlugins());
        }
        finally
        {
            ScanButton.IsEnabled = true;
            ScanButton.Content = "Scan for Problematic Plugins";
            UpdateStatusText();
        }
    }

    private void ScanForProblematicPlugins()
    {
        // Common VST plugin directories
        var pluginDirectories = new[]
        {
            @"C:\Program Files\Common Files\VST3",
            @"C:\Program Files\VSTPlugins",
            @"C:\Program Files (x86)\Common Files\VST3",
            @"C:\Program Files (x86)\VSTPlugins",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "VST3"),
        };

        int foundCount = 0;

        foreach (var directory in pluginDirectories)
        {
            if (!Directory.Exists(directory)) continue;

            try
            {
                // Scan for VST3 plugins
                foreach (var file in Directory.GetFiles(directory, "*.vst3", SearchOption.AllDirectories))
                {
                    if (IsProblematicPlugin(file, out string reason))
                    {
                        AddProblematicPlugin(file, "VST3", reason);
                        foundCount++;
                    }
                }

                // Scan for VST2 plugins
                foreach (var file in Directory.GetFiles(directory, "*.dll", SearchOption.AllDirectories))
                {
                    if (IsProblematicPlugin(file, out string reason))
                    {
                        AddProblematicPlugin(file, "VST2", reason);
                        foundCount++;
                    }
                }
            }
            catch
            {
                // Skip directories we can't access
            }
        }

        Dispatcher.Invoke(() =>
        {
            if (foundCount > 0)
            {
                MessageBox.Show(
                    $"Found {foundCount} potentially problematic plugin(s).\nThey have been added to the blacklist.",
                    "Scan Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    "No problematic plugins were found.",
                    "Scan Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        });
    }

    private bool IsProblematicPlugin(string filePath, out string reason)
    {
        reason = string.Empty;

        try
        {
            var fileInfo = new FileInfo(filePath);

            // Check for very small files (likely corrupt)
            if (fileInfo.Length < 1024)
            {
                reason = "File too small (possibly corrupt)";
                return true;
            }

            // Check for very old plugins (older than 10 years)
            if (fileInfo.LastWriteTime < DateTime.Now.AddYears(-10))
            {
                // Only flag if it's a VST2 plugin (DLL)
                if (filePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    reason = "Very old plugin (compatibility issues likely)";
                    return true;
                }
            }

            // Check for known problematic plugin names
            var fileName = Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant();
            if (fileName.Contains("_old") || fileName.Contains("_backup") || fileName.Contains("_copy"))
            {
                reason = "Backup or duplicate plugin";
                return true;
            }
        }
        catch
        {
            reason = "Unable to read file";
            return true;
        }

        return false;
    }

    private void AddProblematicPlugin(string filePath, string pluginType, string reason)
    {
        if (_blacklistData.IsBlacklisted(filePath)) return;

        var entry = new PluginBlacklistEntry
        {
            PluginName = Path.GetFileNameWithoutExtension(filePath),
            PluginPath = filePath,
            PluginType = pluginType,
            Reason = reason,
            DateAdded = DateTime.UtcNow
        };

        _blacklistData.Add(entry);

        Dispatcher.Invoke(() => _blacklistedPlugins.Add(entry));
    }

    private void ClearAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (_blacklistedPlugins.Count == 0) return;

        var result = MessageBox.Show(
            "Are you sure you want to clear all blacklisted plugins?",
            "Clear Blacklist",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _blacklistData.ClearAll();
            _blacklistedPlugins.Clear();
            UpdateStatusText();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

/// <summary>
/// Converter that returns true if the value is greater than zero.
/// </summary>
public class BlacklistGreaterThanZeroConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is int intValue)
        {
            return intValue > 0;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
