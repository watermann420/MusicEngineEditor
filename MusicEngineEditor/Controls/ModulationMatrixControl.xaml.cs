// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Modulation Matrix control for routing modulation sources to destinations.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MusicEngine.Core.Modulation;
using MusicEngineEditor.ViewModels;

using ContextMenu = System.Windows.Controls.ContextMenu;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace MusicEngineEditor.Controls;

#region Value Converters

/// <summary>
/// Converts ModulationSourceType to an icon/symbol string.
/// </summary>
public class SourceTypeToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ModulationSourceType type)
        {
            return type switch
            {
                ModulationSourceType.LFO => "~",
                ModulationSourceType.Envelope => "/\\",
                ModulationSourceType.Velocity => "V",
                ModulationSourceType.Aftertouch => "AT",
                ModulationSourceType.ModWheel => "MW",
                ModulationSourceType.Expression => "EX",
                ModulationSourceType.Random => "?",
                ModulationSourceType.PitchBend => "PB",
                ModulationSourceType.KeyTrack => "KT",
                ModulationSourceType.MPESlide => "SL",
                ModulationSourceType.MPEPressure => "PR",
                _ => "?"
            };
        }
        return "?";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts modulation amount (-1 to 1) to a color (red for negative, green for positive).
/// </summary>
public class AmountToColorConverter : IValueConverter
{
    private static readonly Color PositiveColor = Color.FromRgb(0x00, 0xFF, 0x88);
    private static readonly Color NegativeColor = Color.FromRgb(0xFF, 0x47, 0x57);
    private static readonly Color NeutralColor = Color.FromRgb(0x80, 0x80, 0x80);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is float amount)
        {
            if (Math.Abs(amount) < 0.001f)
            {
                return new SolidColorBrush(NeutralColor);
            }
            return new SolidColorBrush(amount > 0 ? PositiveColor : NegativeColor);
        }
        return new SolidColorBrush(NeutralColor);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts modulation amount to opacity (0.2 to 1.0 based on magnitude).
/// </summary>
public class AmountToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is float amount)
        {
            float absAmount = Math.Abs(amount);
            return 0.2 + absAmount * 0.8;
        }
        return 0.2;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts modulation amount to display text (e.g., "+50%" or "-25%").
/// </summary>
public class AmountToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is float amount)
        {
            int percent = (int)Math.Round(amount * 100);
            if (percent >= 0)
            {
                return $"+{percent}%";
            }
            return $"{percent}%";
        }
        return "0%";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts amount to slider value (multiplied by 100).
/// </summary>
public class AmountToSliderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is float amount)
        {
            return amount * 100.0;
        }
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double sliderValue)
        {
            return (float)(sliderValue / 100.0);
        }
        return 0f;
    }
}

/// <summary>
/// Converts boolean to Visibility.
/// </summary>
public class ModMatrixBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility == Visibility.Visible;
        }
        return false;
    }
}

#endregion

/// <summary>
/// A visual control for displaying and editing the modulation matrix.
/// Shows a grid of sources (rows) x destinations (columns) with modulation amounts.
/// </summary>
public partial class ModulationMatrixControl : UserControl
{
    #region Constants

    private const double CellWidth = 60;
    private const double CellHeight = 32;
    private const double RowHeaderWidth = 100;
    private const double ColumnHeaderHeight = 40;

    private static readonly Color BackgroundColor = Color.FromRgb(0x0D, 0x0D, 0x0D);
    private static readonly Color PanelColor = Color.FromRgb(0x18, 0x18, 0x18);
    private static readonly Color BorderColor = Color.FromRgb(0x2A, 0x2A, 0x2A);
    private static readonly Color HeaderColor = Color.FromRgb(0x22, 0x22, 0x22);
    private static readonly Color CellColor = Color.FromRgb(0x1A, 0x1A, 0x1A);
    private static readonly Color CellHoverColor = Color.FromRgb(0x25, 0x25, 0x25);
    private static readonly Color CellSelectedColor = Color.FromRgb(0x33, 0x33, 0x33);
    private static readonly Color AccentColor = Color.FromRgb(0x00, 0xD9, 0xFF);
    private static readonly Color PositiveModColor = Color.FromRgb(0x00, 0xFF, 0x88);
    private static readonly Color NegativeModColor = Color.FromRgb(0xFF, 0x47, 0x57);
    private static readonly Color TextPrimaryColor = Color.FromRgb(0xE0, 0xE0, 0xE0);
    private static readonly Color TextSecondaryColor = Color.FromRgb(0x80, 0x80, 0x80);

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty ModulationMatrixProperty =
        DependencyProperty.Register(nameof(ModulationMatrix), typeof(ModulationMatrix), typeof(ModulationMatrixControl),
            new PropertyMetadata(null, OnModulationMatrixChanged));

    /// <summary>
    /// Gets or sets the ModulationMatrix instance.
    /// </summary>
    public ModulationMatrix? ModulationMatrix
    {
        get => (ModulationMatrix?)GetValue(ModulationMatrixProperty);
        set => SetValue(ModulationMatrixProperty, value);
    }

    #endregion

    #region Private Fields

    private ModulationMatrixViewModel? _viewModel;
    private Border?[,]? _cellBorders;
    private bool _isInitialized;
    private bool _isDragging;
    private ModulationCellViewModel? _dragStartCell;

    #endregion

    #region Constructor

    public ModulationMatrixControl()
    {
        InitializeComponent();

        _viewModel = new ModulationMatrixViewModel();
        DataContext = _viewModel;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    #endregion

    #region Lifecycle

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = true;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        BuildMatrixGrid();

        // Register keyboard shortcuts
        var window = Window.GetWindow(this);
        if (window != null)
        {
            window.PreviewKeyDown += Window_PreviewKeyDown;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        var window = Window.GetWindow(this);
        if (window != null)
        {
            window.PreviewKeyDown -= Window_PreviewKeyDown;
        }
    }

    #endregion

    #region Property Changed Handlers

    private static void OnModulationMatrixChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ModulationMatrixControl control && control._viewModel != null && e.NewValue is ModulationMatrix matrix)
        {
            control._viewModel.SetModulationMatrix(matrix);
            if (control._isInitialized)
            {
                control.BuildMatrixGrid();
            }
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!_isInitialized) return;

        switch (e.PropertyName)
        {
            case nameof(ModulationMatrixViewModel.RowCount):
            case nameof(ModulationMatrixViewModel.ColumnCount):
                BuildMatrixGrid();
                break;

            case nameof(ModulationMatrixViewModel.SelectedCell):
                UpdateCellSelection();
                break;
        }
    }

    #endregion

    #region Grid Building

    private void BuildMatrixGrid()
    {
        if (_viewModel == null) return;

        MatrixGrid.Children.Clear();
        MatrixGrid.RowDefinitions.Clear();
        MatrixGrid.ColumnDefinitions.Clear();

        int rowCount = _viewModel.RowCount;
        int colCount = _viewModel.ColumnCount;

        if (rowCount == 0 || colCount == 0) return;

        _cellBorders = new Border[rowCount, colCount];

        // Create row definitions (header + data rows)
        MatrixGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(ColumnHeaderHeight) });
        for (int i = 0; i < rowCount; i++)
        {
            MatrixGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(CellHeight) });
        }

        // Create column definitions (header + data columns)
        MatrixGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(RowHeaderWidth) });
        for (int i = 0; i < colCount; i++)
        {
            MatrixGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(CellWidth) });
        }

        // Create corner cell (empty)
        var cornerCell = CreateCornerCell();
        Grid.SetRow(cornerCell, 0);
        Grid.SetColumn(cornerCell, 0);
        MatrixGrid.Children.Add(cornerCell);

        // Create column headers (destinations)
        for (int col = 0; col < colCount; col++)
        {
            var destination = _viewModel.Destinations[col];
            var header = CreateColumnHeader(destination, col);
            Grid.SetRow(header, 0);
            Grid.SetColumn(header, col + 1);
            MatrixGrid.Children.Add(header);
        }

        // Create row headers (sources) and data cells
        for (int row = 0; row < rowCount; row++)
        {
            var source = _viewModel.Sources[row];

            // Row header
            var rowHeader = CreateRowHeader(source, row);
            Grid.SetRow(rowHeader, row + 1);
            Grid.SetColumn(rowHeader, 0);
            MatrixGrid.Children.Add(rowHeader);

            // Data cells
            for (int col = 0; col < colCount; col++)
            {
                var cellVm = _viewModel.Cells[row * colCount + col];
                var cell = CreateDataCell(cellVm, row, col);
                Grid.SetRow(cell, row + 1);
                Grid.SetColumn(cell, col + 1);
                MatrixGrid.Children.Add(cell);
                _cellBorders[row, col] = cell;
            }
        }
    }

    private Border CreateCornerCell()
    {
        return new Border
        {
            Background = new SolidColorBrush(HeaderColor),
            BorderBrush = new SolidColorBrush(BorderColor),
            BorderThickness = new Thickness(0, 0, 1, 1)
        };
    }

    private Border CreateColumnHeader(ModulationDestinationViewModel destination, int columnIndex)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(HeaderColor),
            BorderBrush = new SolidColorBrush(BorderColor),
            BorderThickness = new Thickness(0, 0, 1, 1),
            ToolTip = $"{destination.Category}.{destination.DisplayName}\nClick to clear column"
        };

        var stackPanel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var nameText = new TextBlock
        {
            Text = destination.DisplayName,
            Foreground = new SolidColorBrush(TextPrimaryColor),
            FontSize = 9,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = CellWidth - 8
        };

        stackPanel.Children.Add(nameText);
        border.Child = stackPanel;

        // Context menu for column operations
        var contextMenu = new ContextMenu();
        var clearItem = new MenuItem { Header = "Clear Column" };
        clearItem.Click += (s, e) => _viewModel?.ClearColumnCommand.Execute(columnIndex);
        contextMenu.Items.Add(clearItem);
        border.ContextMenu = contextMenu;

        return border;
    }

    private Border CreateRowHeader(ModulationSourceViewModel source, int rowIndex)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(HeaderColor),
            BorderBrush = new SolidColorBrush(BorderColor),
            BorderThickness = new Thickness(0, 0, 1, 1),
            ToolTip = $"{source.SourceType}: {source.Name}\nClick to clear row"
        };

        var stackPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 4, 0)
        };

        // Type icon
        var iconText = new TextBlock
        {
            Text = GetSourceTypeIcon(source.SourceType),
            Foreground = new SolidColorBrush(AccentColor),
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Width = 24,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        // Name
        var nameText = new TextBlock
        {
            Text = source.Name,
            Foreground = new SolidColorBrush(TextPrimaryColor),
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = RowHeaderWidth - 40
        };

        stackPanel.Children.Add(iconText);
        stackPanel.Children.Add(nameText);
        border.Child = stackPanel;

        // Context menu for row operations
        var contextMenu = new ContextMenu();

        var copyItem = new MenuItem { Header = "Copy Row" };
        copyItem.Click += (s, e) => _viewModel?.CopyRowCommand.Execute(rowIndex);
        contextMenu.Items.Add(copyItem);

        var pasteItem = new MenuItem { Header = "Paste Row" };
        pasteItem.Click += (s, e) => _viewModel?.PasteRowCommand.Execute(rowIndex);
        contextMenu.Items.Add(pasteItem);

        contextMenu.Items.Add(new Separator());

        var clearItem = new MenuItem { Header = "Clear Row" };
        clearItem.Click += (s, e) => _viewModel?.ClearRowCommand.Execute(rowIndex);
        contextMenu.Items.Add(clearItem);

        border.ContextMenu = contextMenu;

        return border;
    }

    private Border CreateDataCell(ModulationCellViewModel cellVm, int row, int col)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(CellColor),
            BorderBrush = new SolidColorBrush(BorderColor),
            BorderThickness = new Thickness(0, 0, 1, 1),
            Cursor = Cursors.Hand,
            Tag = cellVm
        };

        var grid = new Grid();

        // Background fill indicator (shows modulation amount via color)
        var fillRect = new Rectangle
        {
            Fill = GetAmountBrush(cellVm.Amount),
            Opacity = GetAmountOpacity(cellVm.Amount),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        grid.Children.Add(fillRect);

        // Amount text
        var amountText = new TextBlock
        {
            Text = cellVm.IsActive ? FormatAmount(cellVm.Amount) : "",
            Foreground = new SolidColorBrush(TextPrimaryColor),
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        grid.Children.Add(amountText);

        border.Child = grid;

        // Wire up events
        border.MouseEnter += (s, e) => OnCellMouseEnter(border, cellVm);
        border.MouseLeave += (s, e) => OnCellMouseLeave(border, cellVm);
        border.MouseLeftButtonDown += (s, e) => OnCellMouseLeftButtonDown(border, cellVm, e);
        border.MouseLeftButtonUp += (s, e) => OnCellMouseLeftButtonUp(border, cellVm);
        border.MouseMove += (s, e) => OnCellMouseMove(border, cellVm, e);
        border.MouseWheel += (s, e) => OnCellMouseWheel(cellVm, e);

        // Subscribe to cell changes
        cellVm.PropertyChanged += (s, e) => UpdateCellVisual(border, cellVm, fillRect, amountText);

        return border;
    }

    private void UpdateCellVisual(Border border, ModulationCellViewModel cellVm, Rectangle fillRect, TextBlock amountText)
    {
        Dispatcher.Invoke(() =>
        {
            fillRect.Fill = GetAmountBrush(cellVm.Amount);
            fillRect.Opacity = GetAmountOpacity(cellVm.Amount);
            amountText.Text = cellVm.IsActive ? FormatAmount(cellVm.Amount) : "";

            if (cellVm.IsSelected)
            {
                border.BorderBrush = new SolidColorBrush(AccentColor);
                border.BorderThickness = new Thickness(2);
            }
            else if (cellVm.IsHovered)
            {
                border.Background = new SolidColorBrush(CellHoverColor);
                border.BorderBrush = new SolidColorBrush(BorderColor);
                border.BorderThickness = new Thickness(0, 0, 1, 1);
            }
            else
            {
                border.Background = new SolidColorBrush(CellColor);
                border.BorderBrush = new SolidColorBrush(BorderColor);
                border.BorderThickness = new Thickness(0, 0, 1, 1);
            }
        });
    }

    private void UpdateCellSelection()
    {
        if (_viewModel == null || _cellBorders == null) return;

        for (int row = 0; row < _viewModel.RowCount; row++)
        {
            for (int col = 0; col < _viewModel.ColumnCount; col++)
            {
                var border = _cellBorders[row, col];
                if (border?.Tag is ModulationCellViewModel cellVm)
                {
                    if (border.Child is Grid grid)
                    {
                        var fillRect = grid.Children[0] as Rectangle;
                        var amountText = grid.Children[1] as TextBlock;
                        if (fillRect != null && amountText != null)
                        {
                            UpdateCellVisual(border, cellVm, fillRect, amountText);
                        }
                    }
                }
            }
        }
    }

    #endregion

    #region Cell Event Handlers

    private void OnCellMouseEnter(Border border, ModulationCellViewModel cellVm)
    {
        cellVm.IsHovered = true;
        border.Background = new SolidColorBrush(CellHoverColor);
    }

    private void OnCellMouseLeave(Border border, ModulationCellViewModel cellVm)
    {
        cellVm.IsHovered = false;
        if (!cellVm.IsSelected)
        {
            border.Background = new SolidColorBrush(CellColor);
        }
    }

    private void OnCellMouseLeftButtonDown(Border border, ModulationCellViewModel cellVm, MouseButtonEventArgs e)
    {
        _viewModel?.SelectCellCommand.Execute(cellVm);
        _isDragging = true;
        _dragStartCell = cellVm;
        border.CaptureMouse();
        e.Handled = true;
    }

    private void OnCellMouseLeftButtonUp(Border border, ModulationCellViewModel cellVm)
    {
        _isDragging = false;
        _dragStartCell = null;
        border.ReleaseMouseCapture();
    }

    private void OnCellMouseMove(Border border, ModulationCellViewModel cellVm, MouseEventArgs e)
    {
        if (!_isDragging || _viewModel == null || ModulationMatrix == null) return;
        if (cellVm != _viewModel.SelectedCell) return;

        // Calculate amount based on vertical mouse position
        var position = e.GetPosition(border);
        var height = border.ActualHeight;

        // Top = +100%, Bottom = -100%
        float amount = (float)(1.0 - (position.Y / height) * 2.0);
        amount = Math.Clamp(amount, -1f, 1f);

        // Snap to 5% increments while dragging
        amount = MathF.Round(amount * 20f) / 20f;

        cellVm.SetAmount(amount, ModulationMatrix);
        _viewModel.EditAmount = cellVm.Amount;
    }

    private void OnCellMouseWheel(ModulationCellViewModel cellVm, MouseWheelEventArgs e)
    {
        if (_viewModel == null || ModulationMatrix == null) return;

        // Select the cell if not already selected
        if (cellVm != _viewModel.SelectedCell)
        {
            _viewModel.SelectCellCommand.Execute(cellVm);
        }

        // Increment/decrement by 5%
        float delta = e.Delta > 0 ? 0.05f : -0.05f;
        float newAmount = Math.Clamp(cellVm.Amount + delta, -1f, 1f);
        cellVm.SetAmount(newAmount, ModulationMatrix);
        _viewModel.EditAmount = cellVm.Amount;

        e.Handled = true;
    }

    #endregion

    #region Slider Event Handler

    private void AmountSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_viewModel?.SelectedCell == null || ModulationMatrix == null) return;

        float amount = (float)(e.NewValue / 100.0);
        _viewModel.SelectedCell.SetAmount(amount, ModulationMatrix);
        _viewModel.EditAmount = _viewModel.SelectedCell.Amount;
    }

    #endregion

    #region Keyboard Handling

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel == null) return;

        // Only handle if this control is focused
        if (!IsKeyboardFocusWithin && !IsFocused) return;

        switch (e.Key)
        {
            case Key.Escape:
                _viewModel.ExitEditModeCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Delete:
                if (_viewModel.IsEditMode)
                {
                    _viewModel.ClearSelectedRoutingCommand.Execute(null);
                    e.Handled = true;
                }
                break;

            case Key.C when (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control:
                _viewModel.CopyRoutingCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.V when (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control:
                _viewModel.PasteRoutingCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Up:
                NavigateCell(0, -1);
                e.Handled = true;
                break;

            case Key.Down:
                NavigateCell(0, 1);
                e.Handled = true;
                break;

            case Key.Left:
                NavigateCell(-1, 0);
                e.Handled = true;
                break;

            case Key.Right:
                NavigateCell(1, 0);
                e.Handled = true;
                break;

            case Key.Add:
            case Key.OemPlus:
                _viewModel.IncrementAmountCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Subtract:
            case Key.OemMinus:
                _viewModel.DecrementAmountCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void NavigateCell(int colDelta, int rowDelta)
    {
        if (_viewModel?.SelectedCell == null) return;

        int newRow = _viewModel.SelectedCell.RowIndex + rowDelta;
        int newCol = _viewModel.SelectedCell.ColumnIndex + colDelta;

        // Wrap around
        if (newRow < 0) newRow = _viewModel.RowCount - 1;
        if (newRow >= _viewModel.RowCount) newRow = 0;
        if (newCol < 0) newCol = _viewModel.ColumnCount - 1;
        if (newCol >= _viewModel.ColumnCount) newCol = 0;

        int index = newRow * _viewModel.ColumnCount + newCol;
        if (index >= 0 && index < _viewModel.Cells.Count)
        {
            _viewModel.SelectCellCommand.Execute(_viewModel.Cells[index]);
        }
    }

    #endregion

    #region Helper Methods

    private static string GetSourceTypeIcon(ModulationSourceType type)
    {
        return type switch
        {
            ModulationSourceType.LFO => "~",
            ModulationSourceType.Envelope => "/\\",
            ModulationSourceType.Velocity => "V",
            ModulationSourceType.Aftertouch => "AT",
            ModulationSourceType.ModWheel => "MW",
            ModulationSourceType.Expression => "EX",
            ModulationSourceType.Random => "?",
            ModulationSourceType.PitchBend => "PB",
            ModulationSourceType.KeyTrack => "KT",
            ModulationSourceType.MPESlide => "SL",
            ModulationSourceType.MPEPressure => "PR",
            _ => "?"
        };
    }

    private static Brush GetAmountBrush(float amount)
    {
        if (Math.Abs(amount) < 0.001f)
        {
            return Brushes.Transparent;
        }
        return amount > 0
            ? new SolidColorBrush(PositiveModColor)
            : new SolidColorBrush(NegativeModColor);
    }

    private static double GetAmountOpacity(float amount)
    {
        float absAmount = Math.Abs(amount);
        return absAmount * 0.6; // Max 60% opacity for visibility
    }

    private static string FormatAmount(float amount)
    {
        int percent = (int)Math.Round(amount * 100);
        if (percent >= 0)
        {
            return $"+{percent}%";
        }
        return $"{percent}%";
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Refreshes the matrix display from the underlying data.
    /// </summary>
    public void Refresh()
    {
        if (_viewModel != null && ModulationMatrix != null)
        {
            _viewModel.SetModulationMatrix(ModulationMatrix);
        }
        BuildMatrixGrid();
    }

    /// <summary>
    /// Updates the source values for real-time display.
    /// Call this periodically from a timer for live modulation visualization.
    /// </summary>
    public void UpdateSourceValues()
    {
        _viewModel?.UpdateSourceValuesCommand.Execute(null);
    }

    /// <summary>
    /// Adds a new destination parameter to the matrix.
    /// </summary>
    /// <param name="parameterPath">The parameter path (e.g., "Filter.Cutoff").</param>
    public void AddDestination(string parameterPath)
    {
        _viewModel?.AddDestinationCommand.Execute(parameterPath);
    }

    /// <summary>
    /// Clears all modulation routings.
    /// </summary>
    public void ClearAll()
    {
        _viewModel?.ClearAllRoutingsCommand.Execute(null);
    }

    #endregion
}
