using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Dialog for viewing and editing keyboard shortcuts.
/// </summary>
public partial class KeyboardShortcutsDialog : Window
{
    #region Fields

    private readonly ObservableCollection<ShortcutDisplayItem> _displayItems = new();
    private readonly Dictionary<string, KeyboardShortcut> _shortcuts = new();
    private readonly Dictionary<string, KeyboardShortcut> _originalShortcuts = new();
    private readonly HashSet<string> _modifiedIds = new();
    private readonly HashSet<string> _conflictingIds = new();

    private ShortcutDisplayItem? _editingItem;
    private Key _capturedKey = Key.None;
    private ModifierKeys _capturedModifiers = ModifierKeys.None;
    private string _searchFilter = string.Empty;
    private string _categoryFilter = string.Empty;
    private bool _showConflictsOnly;

    #endregion

    #region Constructor

    public KeyboardShortcutsDialog()
    {
        InitializeComponent();

        ShortcutsItemsControl.ItemsSource = _displayItems;
        LoadDefaultShortcuts();
        RefreshDisplay();
    }

    public KeyboardShortcutsDialog(IEnumerable<KeyboardShortcut> shortcuts) : this()
    {
        LoadShortcuts(shortcuts);
    }

    #endregion

    #region Event Handlers

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (ShortcutEditorPanel.Visibility == Visibility.Visible && _editingItem != null)
        {
            // Capture the key
            if (e.Key != Key.LeftCtrl && e.Key != Key.RightCtrl &&
                e.Key != Key.LeftAlt && e.Key != Key.RightAlt &&
                e.Key != Key.LeftShift && e.Key != Key.RightShift &&
                e.Key != Key.LWin && e.Key != Key.RWin &&
                e.Key != Key.System)
            {
                _capturedKey = e.Key == Key.System ? e.SystemKey : e.Key;
                _capturedModifiers = Keyboard.Modifiers;

                UpdateShortcutInputDisplay();
                DetectConflictsForCapture();
            }

            e.Handled = true;
        }
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchFilter = SearchTextBox.Text ?? string.Empty;
        RefreshDisplay();
    }

    private void CategoryFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CategoryFilterCombo.SelectedItem is ComboBoxItem item)
        {
            _categoryFilter = item.Tag?.ToString() ?? string.Empty;
            RefreshDisplay();
        }
    }

    private void ShowConflictsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _showConflictsOnly = ShowConflictsCheckBox.IsChecked == true;
        RefreshDisplay();
    }

    private void PresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PresetCombo.SelectedItem is ComboBoxItem item && item.Content is string preset)
        {
            if (preset != "Custom")
            {
                LoadPreset(preset);
            }
        }
    }

    private void EditShortcutButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is ShortcutDisplayItem item)
        {
            StartEditing(item);
        }
    }

    private void ClearShortcutButton_Click(object sender, RoutedEventArgs e)
    {
        _capturedKey = Key.None;
        _capturedModifiers = ModifierKeys.None;
        ShortcutInputText.Text = "Not Set";
        ShortcutInputText.Foreground = FindResource("SecondaryForegroundBrush") as System.Windows.Media.Brush;
    }

    private void CancelEditButton_Click(object sender, RoutedEventArgs e)
    {
        CancelEditing();
    }

    private void ApplyShortcutButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyShortcutEdit();
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            Title = "Import Keyboard Shortcuts"
        };

        if (dialog.ShowDialog() == true)
        {
            ImportShortcuts(dialog.FileName);
        }
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            Title = "Export Keyboard Shortcuts",
            FileName = "KeyboardShortcuts.json"
        };

        if (dialog.ShowDialog() == true)
        {
            ExportShortcuts(dialog.FileName);
        }
    }

    private void ResetAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(
            "Reset all shortcuts to their default values?",
            "Reset Shortcuts",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            ResetToDefaults();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    #endregion

    #region Public Methods

    public void LoadShortcuts(IEnumerable<KeyboardShortcut> shortcuts)
    {
        _shortcuts.Clear();
        _originalShortcuts.Clear();

        foreach (var shortcut in shortcuts)
        {
            _shortcuts[shortcut.Id] = shortcut.Clone();
            _originalShortcuts[shortcut.Id] = shortcut.Clone();
        }

        RefreshDisplay();
    }

    public IEnumerable<KeyboardShortcut> GetModifiedShortcuts()
    {
        return _shortcuts.Values.Where(s => _modifiedIds.Contains(s.Id));
    }

    public IEnumerable<KeyboardShortcut> GetAllShortcuts()
    {
        return _shortcuts.Values;
    }

    #endregion

    #region Private Methods

    private void LoadDefaultShortcuts()
    {
        var defaults = GetDefaultShortcuts();
        LoadShortcuts(defaults);
    }

    private void RefreshDisplay()
    {
        _displayItems.Clear();
        DetectAllConflicts();

        var filteredShortcuts = _shortcuts.Values.AsEnumerable();

        // Apply category filter
        if (!string.IsNullOrEmpty(_categoryFilter))
        {
            if (Enum.TryParse<ShortcutCategory>(_categoryFilter, out var category))
            {
                filteredShortcuts = filteredShortcuts.Where(s => s.Category == category);
            }
        }

        // Apply search filter
        if (!string.IsNullOrEmpty(_searchFilter))
        {
            var search = _searchFilter.ToLowerInvariant();
            filteredShortcuts = filteredShortcuts.Where(s =>
                s.CommandName.ToLowerInvariant().Contains(search) ||
                s.Description.ToLowerInvariant().Contains(search) ||
                s.DisplayString.ToLowerInvariant().Contains(search));
        }

        // Apply conflicts filter
        if (_showConflictsOnly)
        {
            filteredShortcuts = filteredShortcuts.Where(s => _conflictingIds.Contains(s.Id));
        }

        // Group by category
        var grouped = filteredShortcuts
            .OrderBy(s => s.Category)
            .ThenBy(s => s.CommandName)
            .GroupBy(s => s.Category);

        foreach (var group in grouped)
        {
            // Add category header
            _displayItems.Add(new ShortcutDisplayItem
            {
                IsHeader = true,
                CategoryName = group.Key.ToString()
            });

            // Add shortcuts
            foreach (var shortcut in group)
            {
                _displayItems.Add(new ShortcutDisplayItem
                {
                    IsShortcut = true,
                    Shortcut = shortcut,
                    HasConflict = _conflictingIds.Contains(shortcut.Id)
                });
            }
        }

        UpdateStatusText();
    }

    private void DetectAllConflicts()
    {
        _conflictingIds.Clear();

        var shortcutGroups = _shortcuts.Values
            .Where(s => s.Key != Key.None)
            .GroupBy(s => new { s.Key, s.Modifiers })
            .Where(g => g.Count() > 1);

        foreach (var group in shortcutGroups)
        {
            foreach (var shortcut in group)
            {
                _conflictingIds.Add(shortcut.Id);
            }
        }
    }

    private void DetectConflictsForCapture()
    {
        if (_editingItem == null || _capturedKey == Key.None) return;

        var conflicts = _shortcuts.Values
            .Where(s => s.Id != _editingItem.Shortcut?.Id &&
                       s.Key == _capturedKey &&
                       s.Modifiers == _capturedModifiers)
            .ToList();

        if (conflicts.Count > 0)
        {
            ShortcutInputText.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xF4, 0x43, 0x36));

            var conflictNames = string.Join(", ", conflicts.Select(c => c.CommandName));
            ShortcutInputText.ToolTip = $"Conflicts with: {conflictNames}";
        }
        else
        {
            ShortcutInputText.Foreground = FindResource("ForegroundBrush") as System.Windows.Media.Brush;
            ShortcutInputText.ToolTip = null;
        }
    }

    private void StartEditing(ShortcutDisplayItem item)
    {
        _editingItem = item;
        _capturedKey = item.Shortcut?.Key ?? Key.None;
        _capturedModifiers = item.Shortcut?.Modifiers ?? ModifierKeys.None;

        UpdateShortcutInputDisplay();
        ShortcutEditorPanel.Visibility = Visibility.Visible;
    }

    private void CancelEditing()
    {
        _editingItem = null;
        _capturedKey = Key.None;
        _capturedModifiers = ModifierKeys.None;
        ShortcutEditorPanel.Visibility = Visibility.Collapsed;
    }

    private void ApplyShortcutEdit()
    {
        if (_editingItem?.Shortcut == null) return;

        var shortcut = _shortcuts[_editingItem.Shortcut.Id];
        shortcut.Key = _capturedKey;
        shortcut.Modifiers = _capturedModifiers;

        _modifiedIds.Add(shortcut.Id);

        CancelEditing();
        RefreshDisplay();

        PresetCombo.SelectedIndex = 5; // Set to "Custom"
    }

    private void UpdateShortcutInputDisplay()
    {
        if (_capturedKey == Key.None)
        {
            ShortcutInputText.Text = "Not Set";
            ShortcutInputText.Foreground = FindResource("SecondaryForegroundBrush") as System.Windows.Media.Brush;
            return;
        }

        var parts = new List<string>();
        if ((_capturedModifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
        if ((_capturedModifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
        if ((_capturedModifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
        parts.Add(GetKeyDisplayName(_capturedKey));

        ShortcutInputText.Text = string.Join(" + ", parts);
        ShortcutInputText.Foreground = FindResource("ForegroundBrush") as System.Windows.Media.Brush;
    }

    private void UpdateStatusText()
    {
        var conflictCount = _conflictingIds.Count;
        ConflictCountText.Text = conflictCount == 0
            ? "No conflicts"
            : $"{conflictCount} conflict{(conflictCount != 1 ? "s" : "")}";

        ConflictCountText.Foreground = conflictCount > 0
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF4, 0x43, 0x36))
            : FindResource("SecondaryForegroundBrush") as System.Windows.Media.Brush;

        var modifiedCount = _modifiedIds.Count;
        ModifiedCountText.Text = modifiedCount > 0
            ? $"{modifiedCount} modified"
            : string.Empty;
    }

    private void LoadPreset(string presetName)
    {
        var presetShortcuts = GetPresetShortcuts(presetName);
        if (presetShortcuts != null)
        {
            foreach (var preset in presetShortcuts)
            {
                if (_shortcuts.TryGetValue(preset.Id, out var shortcut))
                {
                    shortcut.Key = preset.Key;
                    shortcut.Modifiers = preset.Modifiers;
                    _modifiedIds.Add(shortcut.Id);
                }
            }
            RefreshDisplay();
        }
    }

    private void ResetToDefaults()
    {
        foreach (var original in _originalShortcuts.Values)
        {
            if (_shortcuts.TryGetValue(original.Id, out var shortcut))
            {
                shortcut.Key = original.Key;
                shortcut.Modifiers = original.Modifiers;
            }
        }

        _modifiedIds.Clear();
        PresetCombo.SelectedIndex = 0;
        RefreshDisplay();
    }

    private void ImportShortcuts(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            var imported = JsonSerializer.Deserialize<List<KeyboardShortcut>>(json);

            if (imported != null)
            {
                foreach (var shortcut in imported)
                {
                    if (_shortcuts.TryGetValue(shortcut.Id, out var existing))
                    {
                        existing.Key = shortcut.Key;
                        existing.Modifiers = shortcut.Modifiers;
                        _modifiedIds.Add(shortcut.Id);
                    }
                }

                PresetCombo.SelectedIndex = 5;
                RefreshDisplay();
                MessageBox.Show("Shortcuts imported successfully.", "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to import shortcuts: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportShortcuts(string filePath)
    {
        try
        {
            var json = JsonSerializer.Serialize(_shortcuts.Values.ToList(), new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
            MessageBox.Show("Shortcuts exported successfully.", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to export shortcuts: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string GetKeyDisplayName(Key key)
    {
        return key switch
        {
            Key.OemPlus => "+",
            Key.OemMinus => "-",
            Key.OemComma => ",",
            Key.OemPeriod => ".",
            Key.D0 => "0", Key.D1 => "1", Key.D2 => "2", Key.D3 => "3", Key.D4 => "4",
            Key.D5 => "5", Key.D6 => "6", Key.D7 => "7", Key.D8 => "8", Key.D9 => "9",
            Key.Return => "Enter",
            Key.Back => "Backspace",
            Key.Escape => "Esc",
            Key.Prior => "Page Up",
            Key.Next => "Page Down",
            _ => key.ToString()
        };
    }

    private static IEnumerable<KeyboardShortcut> GetDefaultShortcuts()
    {
        return new List<KeyboardShortcut>
        {
            // File
            new() { Id = "file.new", CommandName = "New Project", Description = "Create a new project", Key = Key.N, Ctrl = true, Category = ShortcutCategory.File },
            new() { Id = "file.open", CommandName = "Open Project", Description = "Open an existing project", Key = Key.O, Ctrl = true, Category = ShortcutCategory.File },
            new() { Id = "file.save", CommandName = "Save", Description = "Save the current project", Key = Key.S, Ctrl = true, Category = ShortcutCategory.File },
            new() { Id = "file.saveAs", CommandName = "Save As", Description = "Save with a new name", Key = Key.S, Ctrl = true, Shift = true, Category = ShortcutCategory.File },
            new() { Id = "file.export", CommandName = "Export", Description = "Export audio", Key = Key.E, Ctrl = true, Shift = true, Category = ShortcutCategory.File },

            // Edit
            new() { Id = "edit.undo", CommandName = "Undo", Description = "Undo the last action", Key = Key.Z, Ctrl = true, Category = ShortcutCategory.Edit },
            new() { Id = "edit.redo", CommandName = "Redo", Description = "Redo the last undone action", Key = Key.Y, Ctrl = true, Category = ShortcutCategory.Edit },
            new() { Id = "edit.cut", CommandName = "Cut", Description = "Cut selection", Key = Key.X, Ctrl = true, Category = ShortcutCategory.Edit },
            new() { Id = "edit.copy", CommandName = "Copy", Description = "Copy selection", Key = Key.C, Ctrl = true, Category = ShortcutCategory.Edit },
            new() { Id = "edit.paste", CommandName = "Paste", Description = "Paste from clipboard", Key = Key.V, Ctrl = true, Category = ShortcutCategory.Edit },
            new() { Id = "edit.delete", CommandName = "Delete", Description = "Delete selection", Key = Key.Delete, Category = ShortcutCategory.Edit },
            new() { Id = "edit.selectAll", CommandName = "Select All", Description = "Select all items", Key = Key.A, Ctrl = true, Category = ShortcutCategory.Edit },
            new() { Id = "edit.duplicate", CommandName = "Duplicate", Description = "Duplicate selection", Key = Key.D, Ctrl = true, Category = ShortcutCategory.Edit },

            // Transport
            new() { Id = "transport.play", CommandName = "Play/Pause", Description = "Toggle playback", Key = Key.Space, Category = ShortcutCategory.Transport },
            new() { Id = "transport.stop", CommandName = "Stop", Description = "Stop playback", Key = Key.Space, Shift = true, Category = ShortcutCategory.Transport },
            new() { Id = "transport.record", CommandName = "Record", Description = "Toggle recording", Key = Key.R, Category = ShortcutCategory.Transport },
            new() { Id = "transport.loop", CommandName = "Loop", Description = "Toggle loop mode", Key = Key.L, Category = ShortcutCategory.Transport },
            new() { Id = "transport.gotoStart", CommandName = "Go to Start", Description = "Move to project start", Key = Key.Home, Category = ShortcutCategory.Transport },
            new() { Id = "transport.gotoEnd", CommandName = "Go to End", Description = "Move to project end", Key = Key.End, Category = ShortcutCategory.Transport },
            new() { Id = "transport.metronome", CommandName = "Metronome", Description = "Toggle metronome", Key = Key.M, Ctrl = true, Category = ShortcutCategory.Transport },

            // View
            new() { Id = "view.mixer", CommandName = "Mixer", Description = "Show/hide mixer", Key = Key.M, Category = ShortcutCategory.View },
            new() { Id = "view.pianoRoll", CommandName = "Piano Roll", Description = "Show piano roll editor", Key = Key.P, Category = ShortcutCategory.View },
            new() { Id = "view.arrangement", CommandName = "Arrangement", Description = "Show arrangement view", Key = Key.A, Category = ShortcutCategory.View },
            new() { Id = "view.zoomIn", CommandName = "Zoom In", Description = "Zoom in", Key = Key.OemPlus, Ctrl = true, Category = ShortcutCategory.View },
            new() { Id = "view.zoomOut", CommandName = "Zoom Out", Description = "Zoom out", Key = Key.OemMinus, Ctrl = true, Category = ShortcutCategory.View },
            new() { Id = "view.zoomFit", CommandName = "Zoom to Fit", Description = "Fit all in view", Key = Key.F, Ctrl = true, Category = ShortcutCategory.View },
            new() { Id = "view.fullscreen", CommandName = "Fullscreen", Description = "Toggle fullscreen", Key = Key.F11, Category = ShortcutCategory.View },

            // Tools
            new() { Id = "tools.select", CommandName = "Select Tool", Description = "Switch to selection tool", Key = Key.V, Category = ShortcutCategory.Tools },
            new() { Id = "tools.pencil", CommandName = "Pencil Tool", Description = "Switch to pencil tool", Key = Key.B, Category = ShortcutCategory.Tools },
            new() { Id = "tools.eraser", CommandName = "Eraser Tool", Description = "Switch to eraser tool", Key = Key.E, Category = ShortcutCategory.Tools },
            new() { Id = "tools.split", CommandName = "Split Tool", Description = "Switch to split tool", Key = Key.T, Category = ShortcutCategory.Tools },
            new() { Id = "tools.mute", CommandName = "Mute Tool", Description = "Switch to mute tool", Key = Key.U, Category = ShortcutCategory.Tools },

            // Navigation
            new() { Id = "nav.nextMarker", CommandName = "Next Marker", Description = "Go to next marker", Key = Key.Right, Ctrl = true, Category = ShortcutCategory.Navigation },
            new() { Id = "nav.prevMarker", CommandName = "Previous Marker", Description = "Go to previous marker", Key = Key.Left, Ctrl = true, Category = ShortcutCategory.Navigation },
            new() { Id = "nav.gotoBar", CommandName = "Go to Bar", Description = "Jump to specific bar", Key = Key.G, Ctrl = true, Category = ShortcutCategory.Navigation },

            // Help
            new() { Id = "help.shortcuts", CommandName = "Keyboard Shortcuts", Description = "Show keyboard shortcuts", Key = Key.K, Ctrl = true, Category = ShortcutCategory.Help },
            new() { Id = "help.commandPalette", CommandName = "Command Palette", Description = "Open command palette", Key = Key.P, Ctrl = true, Shift = true, Category = ShortcutCategory.Help },
        };
    }

    private static IEnumerable<KeyboardShortcut>? GetPresetShortcuts(string presetName)
    {
        // Return preset-specific shortcuts
        return presetName switch
        {
            "Pro Tools" => GetProToolsPreset(),
            "Logic Pro" => GetLogicProPreset(),
            "Cubase" => GetCubasePreset(),
            "Ableton Live" => GetAbletonPreset(),
            _ => null
        };
    }

    private static IEnumerable<KeyboardShortcut> GetProToolsPreset()
    {
        return new List<KeyboardShortcut>
        {
            new() { Id = "transport.record", Key = Key.D3 },
            new() { Id = "edit.undo", Key = Key.Z, Ctrl = true },
        };
    }

    private static IEnumerable<KeyboardShortcut> GetLogicProPreset()
    {
        return new List<KeyboardShortcut>
        {
            new() { Id = "transport.record", Key = Key.R },
            new() { Id = "view.mixer", Key = Key.X },
        };
    }

    private static IEnumerable<KeyboardShortcut> GetCubasePreset()
    {
        return new List<KeyboardShortcut>
        {
            new() { Id = "transport.record", Key = Key.NumPad0 },
            new() { Id = "view.mixer", Key = Key.F3 },
        };
    }

    private static IEnumerable<KeyboardShortcut> GetAbletonPreset()
    {
        return new List<KeyboardShortcut>
        {
            new() { Id = "transport.record", Key = Key.F9 },
            new() { Id = "view.arrangement", Key = Key.Tab },
        };
    }

    #endregion
}

/// <summary>
/// Display item for the shortcuts list (can be header or shortcut).
/// </summary>
public class ShortcutDisplayItem : INotifyPropertyChanged
{
    private bool _isHeader;
    private bool _isShortcut;
    private string _categoryName = string.Empty;
    private KeyboardShortcut? _shortcut;
    private bool _hasConflict;

    public bool IsHeader { get => _isHeader; set { _isHeader = value; OnPropertyChanged(); } }
    public bool IsShortcut { get => _isShortcut; set { _isShortcut = value; OnPropertyChanged(); } }
    public string CategoryName { get => _categoryName; set { _categoryName = value; OnPropertyChanged(); } }
    public KeyboardShortcut? Shortcut { get => _shortcut; set { _shortcut = value; OnPropertyChanged(); OnPropertyChanged(nameof(CommandName)); OnPropertyChanged(nameof(Description)); OnPropertyChanged(nameof(KeyParts)); OnPropertyChanged(nameof(IsNotSet)); OnPropertyChanged(nameof(IsCustomizable)); } }
    public bool HasConflict { get => _hasConflict; set { _hasConflict = value; OnPropertyChanged(); } }

    public string CommandName => Shortcut?.CommandName ?? string.Empty;
    public string Description => Shortcut?.Description ?? string.Empty;
    public bool IsNotSet => Shortcut?.Key == Key.None;
    public bool IsCustomizable => Shortcut?.IsCustomizable ?? true;

    public IEnumerable<string> KeyParts
    {
        get
        {
            if (Shortcut == null || Shortcut.Key == Key.None)
                return Enumerable.Empty<string>();

            var parts = new List<string>();
            if (Shortcut.Ctrl) parts.Add("Ctrl");
            if (Shortcut.Alt) parts.Add("Alt");
            if (Shortcut.Shift) parts.Add("Shift");
            parts.Add(GetKeyName(Shortcut.Key));
            return parts;
        }
    }

    private static string GetKeyName(Key key)
    {
        return key switch
        {
            Key.OemPlus => "+",
            Key.OemMinus => "-",
            Key.D0 => "0", Key.D1 => "1", Key.D2 => "2", Key.D3 => "3", Key.D4 => "4",
            Key.D5 => "5", Key.D6 => "6", Key.D7 => "7", Key.D8 => "8", Key.D9 => "9",
            Key.Return => "Enter",
            Key.Back => "Backspace",
            Key.Escape => "Esc",
            _ => key.ToString()
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
