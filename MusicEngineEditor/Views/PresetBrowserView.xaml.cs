using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using MusicEngine.Core;
using MusicEngineEditor.Controls;
using MusicEngineEditor.Models;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Views;

/// <summary>
/// Preset Browser view for browsing and selecting synth and effect presets.
/// </summary>
public partial class PresetBrowserView : UserControl
{
    private PresetBrowserViewModel? _viewModel;

    /// <summary>
    /// Event raised when a preset is selected for loading.
    /// </summary>
    public event EventHandler<PresetInfo>? PresetLoadRequested;

    /// <summary>
    /// Event raised when preview is requested.
    /// </summary>
    public event EventHandler<PresetInfo>? PreviewRequested;

    /// <summary>
    /// Event raised when preview should stop.
    /// </summary>
    public event EventHandler? PreviewStopRequested;

    public PresetBrowserView()
    {
        InitializeComponent();
        Loaded += PresetBrowserView_Loaded;
    }

    private void PresetBrowserView_Loaded(object sender, RoutedEventArgs e)
    {
        // Get the PresetManager from DI if available, otherwise create a new one
        PresetManager presetManager;
        try
        {
            presetManager = App.Services.GetService(typeof(PresetManager)) as PresetManager
                            ?? new PresetManager();
        }
        catch
        {
            presetManager = new PresetManager();
        }

        _viewModel = new PresetBrowserViewModel(presetManager);
        _viewModel.PresetLoadRequested += ViewModel_PresetLoadRequested;
        _viewModel.PreviewRequested += ViewModel_PreviewRequested;
        _viewModel.PreviewStopRequested += ViewModel_PreviewStopRequested;
        DataContext = _viewModel;

        // Scan default directories for presets
        _viewModel.ScanDefaultDirectories();
    }

    private void ViewModel_PresetLoadRequested(object? sender, PresetInfo preset)
    {
        PresetLoadRequested?.Invoke(this, preset);
    }

    private void ViewModel_PreviewRequested(object? sender, PresetInfo preset)
    {
        PreviewRequested?.Invoke(this, preset);
    }

    private void ViewModel_PreviewStopRequested(object? sender, EventArgs e)
    {
        PreviewStopRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        SearchTextBox.Text = string.Empty;
        _viewModel?.ClearSearchCommand.Execute(null);
    }

    private void Category_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && _viewModel != null)
        {
            var category = button.Content?.ToString() ?? "All";
            _viewModel.SelectCategoryCommand.Execute(category);
        }
    }

    private void SynthFilter_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.FilterBySynthsCommand.Execute(null);
    }

    private void EffectFilter_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.FilterByEffectsCommand.Execute(null);
    }

    private void Tag_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TagInfo tag } && _viewModel != null)
        {
            _viewModel.ToggleTagCommand.Execute(tag);
        }
    }

    private void ListViewToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.IsGridView = false;
        }
    }

    private void BankTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_viewModel == null) return;

        if (e.NewValue is PresetBankNode bankNode)
        {
            _viewModel.SelectedBank = bankNode;
            _viewModel.SelectedCategory = "All";
        }
        else if (e.NewValue is PresetCategoryNode categoryNode)
        {
            _viewModel.SelectedCategory = categoryNode.Name;
        }
    }

    private void PresetCard_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is PresetCard { Preset: not null } card)
        {
            _viewModel?.OnPresetDoubleClick();
        }
    }

    private void PresetCard_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is PresetCard { Preset: not null } card && _viewModel != null)
        {
            _viewModel.SelectedPreset = card.Preset;
        }
    }

    private void PresetListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        _viewModel?.OnPresetDoubleClick();
    }

    private void ContextMenu_LoadPreset_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.LoadPresetCommand.Execute(null);
    }

    private void ContextMenu_ToggleFavorite_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.ToggleFavoriteCommand.Execute(null);
    }

    private void ContextMenu_Rename_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.SelectedPreset == null) return;

        var dialog = new Views.Dialogs.InputDialog
        {
            Title = "Rename Preset",
            Prompt = "Enter new name:",
            Value = _viewModel.SelectedPreset.Name,
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.Value))
        {
            _viewModel.RenameSelectedPreset(dialog.Value);
        }
    }

    private void ContextMenu_Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.SelectedPreset == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete the preset '{_viewModel.SelectedPreset.Name}'?",
            "Delete Preset",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _viewModel.DeleteSelectedPreset();
        }
    }

    /// <summary>
    /// Gets the ViewModel for external access.
    /// </summary>
    public PresetBrowserViewModel? ViewModel => _viewModel;

    /// <summary>
    /// Refreshes the preset list.
    /// </summary>
    public void Refresh()
    {
        _viewModel?.RefreshCommand.Execute(null);
    }

    /// <summary>
    /// Scans a directory for presets.
    /// </summary>
    /// <param name="directoryPath">The directory to scan.</param>
    public void ScanDirectory(string directoryPath)
    {
        _viewModel?.ScanDirectoryCommand.Execute(directoryPath);
    }
}

#region Value Converters

/// <summary>
/// Converts a boolean to its inverse.
/// </summary>
public class InverseBoolConverter : MarkupExtension, IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;
        return false;
    }

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}

/// <summary>
/// Converts a PresetTargetType to a boolean for toggle button binding.
/// </summary>
public class TargetTypeConverter : MarkupExtension, IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is PresetTargetType selectedType && parameter is string paramStr)
        {
            return paramStr switch
            {
                "Synth" => selectedType == PresetTargetType.Synth,
                "Effect" => selectedType == PresetTargetType.Effect,
                _ => false
            };
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter is string paramStr)
        {
            return paramStr switch
            {
                "Synth" => PresetTargetType.Synth,
                "Effect" => PresetTargetType.Effect,
                _ => null
            };
        }
        return null;
    }

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}

/// <summary>
/// Converts the selected category to "Active" tag for styling.
/// </summary>
public class CategoryActiveConverter : MarkupExtension, IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string selectedCategory && parameter is string buttonCategory)
        {
            return selectedCategory == buttonCategory ? "Active" : null;
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}

/// <summary>
/// Converts a boolean to Visibility.
/// </summary>
public class BoolToVisibilityConverter : MarkupExtension, IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var boolValue = value is bool b && b;
        var inverse = parameter?.ToString() == "Inverse";

        if (inverse)
            boolValue = !boolValue;

        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}

/// <summary>
/// Converts non-null values to true.
/// </summary>
public class NotNullToBoolConverter : MarkupExtension, IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value != null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}

#endregion
