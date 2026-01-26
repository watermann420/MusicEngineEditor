using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using MusicEngineEditor.Services;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Dialog for recording, editing, and managing macros.
/// </summary>
public partial class MacroRecorderDialog : Window
{
    private readonly MacroRecorderService _macroService;
    private Macro? _selectedMacro;
    private bool _isUpdatingUI;

    /// <summary>
    /// Creates a new MacroRecorderDialog.
    /// </summary>
    /// <param name="macroService">The macro recorder service.</param>
    public MacroRecorderDialog(MacroRecorderService macroService)
    {
        InitializeComponent();
        _macroService = macroService;

        _macroService.RecordingStarted += OnRecordingStarted;
        _macroService.RecordingStopped += OnRecordingStopped;
        _macroService.StepRecorded += OnStepRecorded;

        RefreshMacroList();
        UpdateCategoryComboBox();
    }

    private void RefreshMacroList()
    {
        MacroListBox.ItemsSource = new ObservableCollection<Macro>(_macroService.Macros);
    }

    private void UpdateCategoryComboBox()
    {
        var categories = _macroService.GetCategories().ToList();
        if (!categories.Contains("General"))
            categories.Insert(0, "General");

        CategoryComboBox.ItemsSource = categories;
    }

    private void UpdateMacroDetails()
    {
        _isUpdatingUI = true;
        try
        {
            if (_selectedMacro == null)
            {
                MacroNameTextBox.Text = string.Empty;
                CategoryComboBox.SelectedItem = "General";
                ShortcutTextBox.Text = string.Empty;
                StepsListBox.ItemsSource = null;
            }
            else
            {
                MacroNameTextBox.Text = _selectedMacro.Name;
                CategoryComboBox.SelectedItem = _selectedMacro.Category;
                ShortcutTextBox.Text = _selectedMacro.ShortcutDisplay;
                StepsListBox.ItemsSource = new ObservableCollection<MacroStep>(_selectedMacro.Steps);
            }
        }
        finally
        {
            _isUpdatingUI = false;
        }
    }

    private void OnMacroSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedMacro = MacroListBox.SelectedItem as Macro;
        UpdateMacroDetails();
        PlayButton.IsEnabled = _selectedMacro != null;
        DeleteButton.IsEnabled = _selectedMacro != null;
    }

    private void OnRecordClick(object sender, RoutedEventArgs e)
    {
        if (_macroService.IsRecording)
            return;

        var name = $"Macro {_macroService.Macros.Count + 1}";
        _macroService.StartRecording(name);
    }

    private void OnRecordingStarted(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            RecordButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            PlayButton.IsEnabled = false;
            RecordingIndicator.Visibility = Visibility.Visible;
        });
    }

    private void OnStopClick(object sender, RoutedEventArgs e)
    {
        _macroService.StopRecording();
    }

    private void OnRecordingStopped(object? sender, Macro macro)
    {
        Dispatcher.Invoke(() =>
        {
            RecordButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            PlayButton.IsEnabled = true;
            RecordingIndicator.Visibility = Visibility.Collapsed;

            RefreshMacroList();

            // Select the newly recorded macro
            MacroListBox.SelectedItem = _macroService.Macros.FirstOrDefault(m => m.Id == macro.Id);
        });
    }

    private void OnStepRecorded(object? sender, MacroRecordingEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            // Update steps list if currently viewing the recording macro
            if (_macroService.RecordingMacro != null)
            {
                StepsListBox.ItemsSource = new ObservableCollection<MacroStep>(_macroService.RecordingMacro.Steps);
            }
        });
    }

    private async void OnPlayClick(object sender, RoutedEventArgs e)
    {
        if (_selectedMacro == null) return;

        PlayButton.IsEnabled = false;
        RecordButton.IsEnabled = false;

        try
        {
            await _macroService.PlayMacroAsync(_selectedMacro);
        }
        finally
        {
            PlayButton.IsEnabled = true;
            RecordButton.IsEnabled = true;
        }
    }

    private void OnNewMacroClick(object sender, RoutedEventArgs e)
    {
        var macro = new Macro
        {
            Name = $"New Macro {_macroService.Macros.Count + 1}",
            Category = "General"
        };

        _macroService.SaveMacro(macro);
        RefreshMacroList();
        MacroListBox.SelectedItem = _macroService.Macros.FirstOrDefault(m => m.Id == macro.Id);
    }

    private void OnDeleteMacroClick(object sender, RoutedEventArgs e)
    {
        if (_selectedMacro == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete '{_selectedMacro.Name}'?",
            "Delete Macro",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _macroService.DeleteMacro(_selectedMacro.Id);
            RefreshMacroList();
            _selectedMacro = null;
            UpdateMacroDetails();
        }
    }

    private void OnMacroNameChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingUI || _selectedMacro == null) return;

        _selectedMacro.Name = MacroNameTextBox.Text;
        _macroService.SaveMacro(_selectedMacro);
        RefreshMacroList();
    }

    private void OnCategoryChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingUI || _selectedMacro == null) return;

        var category = CategoryComboBox.SelectedItem?.ToString() ?? "General";
        _selectedMacro.Category = category;
        _macroService.SaveMacro(_selectedMacro);
        UpdateCategoryComboBox();
    }

    private void OnShortcutKeyDown(object sender, KeyEventArgs e)
    {
        if (_selectedMacro == null) return;

        // Ignore modifier keys alone
        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.System)
        {
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;

        if (_macroService.AssignShortcut(_selectedMacro.Id, key, modifiers))
        {
            ShortcutTextBox.Text = _selectedMacro.ShortcutDisplay;
        }
        else
        {
            MessageBox.Show("This shortcut is already assigned to another macro.",
                "Shortcut Conflict", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        e.Handled = true;
    }

    private void OnClearShortcutClick(object sender, RoutedEventArgs e)
    {
        if (_selectedMacro == null) return;

        _macroService.AssignShortcut(_selectedMacro.Id, Key.None, ModifierKeys.None);
        ShortcutTextBox.Text = string.Empty;
    }

    private void OnRemoveStepClick(object sender, RoutedEventArgs e)
    {
        if (_selectedMacro == null) return;
        if (sender is not Button button || button.Tag is not MacroStep step) return;

        _selectedMacro.Steps.Remove(step);
        ReorderSteps();
        _macroService.SaveMacro(_selectedMacro);
        StepsListBox.ItemsSource = new ObservableCollection<MacroStep>(_selectedMacro.Steps);
    }

    private void OnMoveStepUpClick(object sender, RoutedEventArgs e)
    {
        if (_selectedMacro == null || StepsListBox.SelectedItem is not MacroStep step) return;

        var index = _selectedMacro.Steps.IndexOf(step);
        if (index > 0)
        {
            _selectedMacro.Steps.RemoveAt(index);
            _selectedMacro.Steps.Insert(index - 1, step);
            ReorderSteps();
            _macroService.SaveMacro(_selectedMacro);
            StepsListBox.ItemsSource = new ObservableCollection<MacroStep>(_selectedMacro.Steps);
            StepsListBox.SelectedIndex = index - 1;
        }
    }

    private void OnMoveStepDownClick(object sender, RoutedEventArgs e)
    {
        if (_selectedMacro == null || StepsListBox.SelectedItem is not MacroStep step) return;

        var index = _selectedMacro.Steps.IndexOf(step);
        if (index < _selectedMacro.Steps.Count - 1)
        {
            _selectedMacro.Steps.RemoveAt(index);
            _selectedMacro.Steps.Insert(index + 1, step);
            ReorderSteps();
            _macroService.SaveMacro(_selectedMacro);
            StepsListBox.ItemsSource = new ObservableCollection<MacroStep>(_selectedMacro.Steps);
            StepsListBox.SelectedIndex = index + 1;
        }
    }

    private void ReorderSteps()
    {
        if (_selectedMacro == null) return;

        for (int i = 0; i < _selectedMacro.Steps.Count; i++)
        {
            _selectedMacro.Steps[i].Order = i;
        }
    }

    private async void OnImportClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Macro files (*.json)|*.json|All files (*.*)|*.*",
            Title = "Import Macro"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var macro = await _macroService.ImportMacroAsync(dialog.FileName);
                if (macro != null)
                {
                    RefreshMacroList();
                    MacroListBox.SelectedItem = _macroService.Macros.FirstOrDefault(m => m.Id == macro.Id);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import macro: {ex.Message}", "Import Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void OnExportClick(object sender, RoutedEventArgs e)
    {
        if (_selectedMacro == null)
        {
            MessageBox.Show("Please select a macro to export.", "Export",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "Macro files (*.json)|*.json",
            FileName = $"{_selectedMacro.Name}.json",
            Title = "Export Macro"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _macroService.ExportMacroAsync(_selectedMacro, dialog.FileName);
                MessageBox.Show("Macro exported successfully.", "Export",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export macro: {ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void OnSaveAllClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await _macroService.SaveMacrosAsync();
            MessageBox.Show("All macros saved successfully.", "Save",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save macros: {ex.Message}", "Save Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _macroService.RecordingStarted -= OnRecordingStarted;
        _macroService.RecordingStopped -= OnRecordingStopped;
        _macroService.StepRecorded -= OnStepRecorded;
        base.OnClosed(e);
    }
}
