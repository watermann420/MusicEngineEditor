// MusicEngineEditor - Color Palette Dialog
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using MusicEngineEditor.Services;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Converts a hex color string to a Color object for the Color Palette Dialog.
/// </summary>
public class ColorPaletteStringToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hexColor)
        {
            try
            {
                return (Color)ColorConverter.ConvertFromString(hexColor);
            }
            catch
            {
                return Colors.Gray;
            }
        }
        return Colors.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }
        return "#808080";
    }
}

/// <summary>
/// Dialog for managing color palettes.
/// </summary>
public partial class ColorPaletteDialog : Window
{
    private readonly ColorPaletteService _paletteService;
    private ColorPalette? _selectedPalette;
    private bool _isUpdating;

    /// <summary>
    /// Event raised when a palette should be applied to tracks.
    /// </summary>
    public event EventHandler<ColorPalette>? ApplyPaletteRequested;

    /// <summary>
    /// Event raised when a palette should be applied to all tracks.
    /// </summary>
    public event EventHandler<ColorPalette>? ApplyPaletteToAllRequested;

    /// <summary>
    /// Creates a new ColorPaletteDialog.
    /// </summary>
    public ColorPaletteDialog(ColorPaletteService paletteService)
    {
        InitializeComponent();

        _paletteService = paletteService ?? throw new ArgumentNullException(nameof(paletteService));

        // Add converter to resources
        Resources.Add("StringToColorConverter", new ColorPaletteStringToColorConverter());

        LoadPalettes();
    }

    private void LoadPalettes()
    {
        PaletteList.ItemsSource = _paletteService.Palettes;

        if (_paletteService.CurrentPalette != null)
        {
            PaletteList.SelectedItem = _paletteService.CurrentPalette;
        }
        else if (_paletteService.Palettes.Count > 0)
        {
            PaletteList.SelectedIndex = 0;
        }
    }

    private void UpdateEditor()
    {
        _isUpdating = true;
        try
        {
            if (_selectedPalette == null)
            {
                EditorPanel.IsEnabled = false;
                PaletteNameBox.Text = string.Empty;
                PaletteDescriptionBox.Text = string.Empty;
                ColorsListControl.ItemsSource = null;
                return;
            }

            EditorPanel.IsEnabled = !_selectedPalette.IsBuiltIn;
            PaletteNameBox.Text = _selectedPalette.Name;
            PaletteDescriptionBox.Text = _selectedPalette.Description ?? string.Empty;
            ColorsListControl.ItemsSource = _selectedPalette.Colors;

            // Disable editing for built-in palettes
            PaletteNameBox.IsEnabled = !_selectedPalette.IsBuiltIn;
            PaletteDescriptionBox.IsEnabled = !_selectedPalette.IsBuiltIn;
            AddColorButton.IsEnabled = !_selectedPalette.IsBuiltIn;
            DeleteButton.IsEnabled = !_selectedPalette.IsBuiltIn;
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void PaletteList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedPalette = PaletteList.SelectedItem as ColorPalette;
        UpdateEditor();
    }

    private void NewPaletteButton_Click(object sender, RoutedEventArgs e)
    {
        var name = GenerateUniqueName("New Palette");
        var palette = _paletteService.CreatePalette(name);
        PaletteList.SelectedItem = palette;
    }

    private void DuplicateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPalette == null) return;

        var palette = _paletteService.DuplicatePalette(_selectedPalette);
        PaletteList.SelectedItem = palette;
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPalette == null || _selectedPalette.IsBuiltIn) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete the palette '{_selectedPalette.Name}'?",
            "Delete Palette",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            _paletteService.DeletePalette(_selectedPalette);
            if (_paletteService.Palettes.Count > 0)
            {
                PaletteList.SelectedIndex = 0;
            }
        }
    }

    private void PaletteNameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdating || _selectedPalette == null || _selectedPalette.IsBuiltIn) return;

        _selectedPalette.Name = PaletteNameBox.Text;
        _paletteService.UpdatePalette(_selectedPalette);

        // Refresh the list to show updated name
        var index = PaletteList.SelectedIndex;
        PaletteList.Items.Refresh();
        PaletteList.SelectedIndex = index;
    }

    private void PaletteDescriptionBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdating || _selectedPalette == null || _selectedPalette.IsBuiltIn) return;

        _selectedPalette.Description = string.IsNullOrWhiteSpace(PaletteDescriptionBox.Text)
            ? null
            : PaletteDescriptionBox.Text;
        _paletteService.UpdatePalette(_selectedPalette);
    }

    private void AddColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPalette == null || _selectedPalette.IsBuiltIn) return;

        // Show color picker dialog
        var colorDialog = new System.Windows.Forms.ColorDialog
        {
            AnyColor = true,
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(74, 158, 255) // Default blue
        };

        if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var color = colorDialog.Color;
            var hexColor = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            _selectedPalette.Colors.Add(hexColor);
            _paletteService.UpdatePalette(_selectedPalette);
            UpdateEditor();
        }
    }

    private void ColorSwatch_Click(object sender, MouseButtonEventArgs e)
    {
        if (_selectedPalette == null || _selectedPalette.IsBuiltIn) return;
        if (sender is not FrameworkElement fe || fe.DataContext is not string hexColor) return;

        EditColorAt(_selectedPalette.Colors.IndexOf(hexColor));
    }

    private void EditColor_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPalette == null || _selectedPalette.IsBuiltIn) return;
        if (sender is not MenuItem menuItem) return;
        if (menuItem.Parent is not System.Windows.Controls.ContextMenu contextMenu) return;
        if (contextMenu.PlacementTarget is not FrameworkElement fe) return;
        if (fe.DataContext is not string hexColor) return;

        EditColorAt(_selectedPalette.Colors.IndexOf(hexColor));
    }

    private void EditColorAt(int index)
    {
        if (_selectedPalette == null || index < 0 || index >= _selectedPalette.Colors.Count) return;

        var currentHex = _selectedPalette.Colors[index];

        try
        {
            var currentColor = (Color)ColorConverter.ConvertFromString(currentHex);

            var colorDialog = new System.Windows.Forms.ColorDialog
            {
                AnyColor = true,
                FullOpen = true,
                Color = System.Drawing.Color.FromArgb(currentColor.R, currentColor.G, currentColor.B)
            };

            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var newColor = colorDialog.Color;
                var newHex = $"#{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
                _selectedPalette.Colors[index] = newHex;
                _paletteService.UpdatePalette(_selectedPalette);
                UpdateEditor();
            }
        }
        catch { }
    }

    private void RemoveColor_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPalette == null || _selectedPalette.IsBuiltIn) return;
        if (sender is not MenuItem menuItem) return;
        if (menuItem.Parent is not System.Windows.Controls.ContextMenu contextMenu) return;
        if (contextMenu.PlacementTarget is not FrameworkElement fe) return;
        if (fe.DataContext is not string hexColor) return;

        _selectedPalette.Colors.Remove(hexColor);
        _paletteService.UpdatePalette(_selectedPalette);
        UpdateEditor();
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import Color Palette",
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            FilterIndex = 1
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var palette = _paletteService.ImportPalette(dialog.FileName);
                PaletteList.SelectedItem = palette;
                MessageBox.Show(
                    $"Palette '{palette.Name}' imported successfully.",
                    "Import Successful",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to import palette: {ex.Message}",
                    "Import Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPalette == null) return;

        var dialog = new SaveFileDialog
        {
            Title = "Export Color Palette",
            Filter = "JSON Files (*.json)|*.json",
            FilterIndex = 1,
            FileName = $"{_selectedPalette.Name}.json"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                _paletteService.ExportPalette(_selectedPalette, dialog.FileName);
                MessageBox.Show(
                    $"Palette exported to {dialog.FileName}",
                    "Export Successful",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to export palette: {ex.Message}",
                    "Export Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPalette == null) return;

        _paletteService.SetCurrentPalette(_selectedPalette);
        ApplyPaletteRequested?.Invoke(this, _selectedPalette);
    }

    private void ApplyToAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPalette == null) return;

        _paletteService.SetCurrentPalette(_selectedPalette);
        ApplyPaletteToAllRequested?.Invoke(this, _selectedPalette);
    }

    private string GenerateUniqueName(string baseName)
    {
        var name = baseName;
        var counter = 1;

        while (_paletteService.Palettes.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            name = $"{baseName} {counter++}";
        }

        return name;
    }
}
