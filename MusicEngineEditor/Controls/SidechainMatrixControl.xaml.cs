// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code-behind for sidechain matrix control.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MusicEngine.Core.Routing;
using MusicEngineEditor.ViewModels;

using ComboBox = System.Windows.Controls.ComboBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Sidechain Matrix Control for visual configuration of sidechain routing between tracks.
/// Displays a grid where rows are sources and columns are destinations.
/// </summary>
public partial class SidechainMatrixControl : UserControl
{
    private SidechainMatrixViewModel? _viewModel;
    private const int CellSize = 28;
    private const int HeaderWidth = 80;
    private const int HeaderHeight = 60;

    // Cached brushes for performance
    private static readonly SolidColorBrush BackgroundBrush = new(Color.FromRgb(0x14, 0x14, 0x14));
    private static readonly SolidColorBrush GridLineBrush = new(Color.FromRgb(0x33, 0x33, 0x33));
    private static readonly SolidColorBrush HeaderBackgroundBrush = new(Color.FromRgb(0x1A, 0x1A, 0x1A));
    private static readonly SolidColorBrush DiagonalBrush = new(Color.FromRgb(0x1A, 0x1A, 0x1A));
    private static readonly SolidColorBrush ActiveBrush = new(Color.FromRgb(0x00, 0xFF, 0x88));
    private static readonly SolidColorBrush AccentBrush = new(Color.FromRgb(0x00, 0xD9, 0xFF));
    private static readonly SolidColorBrush HoverBrush = new(Color.FromRgb(0x2A, 0x4A, 0x4A));
    private static readonly SolidColorBrush TextPrimaryBrush = new(Color.FromRgb(0xE0, 0xE0, 0xE0));
    private static readonly SolidColorBrush TextSecondaryBrush = new(Color.FromRgb(0x80, 0x80, 0x80));

    /// <summary>
    /// Creates a new SidechainMatrixControl.
    /// </summary>
    public SidechainMatrixControl()
    {
        InitializeComponent();

        _viewModel = new SidechainMatrixViewModel();
        DataContext = _viewModel;

        _viewModel.TrackNames.CollectionChanged += OnTrackNamesChanged;
        _viewModel.MatrixCells.CollectionChanged += OnMatrixCellsChanged;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// Initializes the control with a sidechain matrix and track names.
    /// </summary>
    /// <param name="sidechainMatrix">The sidechain matrix to manage.</param>
    /// <param name="trackNames">The list of track names.</param>
    public void Initialize(SidechainMatrix sidechainMatrix, IEnumerable<string> trackNames)
    {
        _viewModel?.Initialize(sidechainMatrix, trackNames);
        BuildMatrixGrid();
    }

    /// <summary>
    /// Updates the track list and rebuilds the matrix.
    /// </summary>
    /// <param name="trackNames">The new list of track names.</param>
    public void UpdateTracks(IEnumerable<string> trackNames)
    {
        _viewModel?.UpdateTracks(trackNames);
        BuildMatrixGrid();
    }

    /// <summary>
    /// Gets the view model for this control.
    /// </summary>
    public SidechainMatrixViewModel? ViewModel => _viewModel;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BuildMatrixGrid();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _viewModel?.Shutdown();
    }

    private void OnTrackNamesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        BuildMatrixGrid();
    }

    private void OnMatrixCellsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Update cell visuals if needed
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            BuildMatrixGrid();
        }
    }

    private void BuildMatrixGrid()
    {
        if (_viewModel == null)
            return;

        MatrixContent.Children.Clear();
        MatrixContent.RowDefinitions.Clear();
        MatrixContent.ColumnDefinitions.Clear();

        var trackNames = _viewModel.TrackNames.ToList();
        int trackCount = trackNames.Count;

        // Show empty state if no tracks
        if (trackCount == 0)
        {
            EmptyStateText.Visibility = Visibility.Visible;
            return;
        }

        EmptyStateText.Visibility = Visibility.Collapsed;

        // Create grid structure: header row + data rows, header column + data columns
        // Row 0: Column headers (destination tracks)
        MatrixContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(HeaderHeight) });

        // Data rows (one per source track)
        for (int i = 0; i < trackCount; i++)
        {
            MatrixContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(CellSize) });
        }

        // Column 0: Row headers (source tracks)
        MatrixContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(HeaderWidth) });

        // Data columns (one per destination track)
        for (int i = 0; i < trackCount; i++)
        {
            MatrixContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(CellSize) });
        }

        // Add corner cell (top-left)
        var cornerCell = CreateCornerCell();
        Grid.SetRow(cornerCell, 0);
        Grid.SetColumn(cornerCell, 0);
        MatrixContent.Children.Add(cornerCell);

        // Add column headers (destination tracks)
        for (int col = 0; col < trackCount; col++)
        {
            var header = CreateColumnHeader(trackNames[col], col);
            Grid.SetRow(header, 0);
            Grid.SetColumn(header, col + 1);
            MatrixContent.Children.Add(header);
        }

        // Add row headers and cells
        for (int row = 0; row < trackCount; row++)
        {
            // Row header (source track)
            var rowHeader = CreateRowHeader(trackNames[row], row);
            Grid.SetRow(rowHeader, row + 1);
            Grid.SetColumn(rowHeader, 0);
            MatrixContent.Children.Add(rowHeader);

            // Cells for this row
            for (int col = 0; col < trackCount; col++)
            {
                int cellIndex = row * trackCount + col;
                if (cellIndex < _viewModel.MatrixCells.Count)
                {
                    var cell = _viewModel.MatrixCells[cellIndex];
                    var cellControl = CreateMatrixCell(cell, row, col);
                    Grid.SetRow(cellControl, row + 1);
                    Grid.SetColumn(cellControl, col + 1);
                    MatrixContent.Children.Add(cellControl);
                }
            }
        }
    }

    private Border CreateCornerCell()
    {
        var border = new Border
        {
            Background = HeaderBackgroundBrush,
            BorderBrush = GridLineBrush,
            BorderThickness = new Thickness(0, 0, 1, 1)
        };

        var grid = new Grid();

        // Add diagonal line
        var line = new Line
        {
            X1 = 0,
            Y1 = 0,
            X2 = HeaderWidth,
            Y2 = HeaderHeight,
            Stroke = GridLineBrush,
            StrokeThickness = 1
        };
        grid.Children.Add(line);

        // Add labels
        var sourceLabel = new TextBlock
        {
            Text = "Source",
            FontSize = 8,
            Foreground = TextSecondaryBrush,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(4, 0, 0, 4)
        };
        grid.Children.Add(sourceLabel);

        var destLabel = new TextBlock
        {
            Text = "Dest",
            FontSize = 8,
            Foreground = TextSecondaryBrush,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 4, 4, 0)
        };
        grid.Children.Add(destLabel);

        border.Child = grid;
        return border;
    }

    private Border CreateColumnHeader(string trackName, int index)
    {
        var border = new Border
        {
            Background = HeaderBackgroundBrush,
            BorderBrush = GridLineBrush,
            BorderThickness = new Thickness(0, 0, 1, 1)
        };

        var textBlock = new TextBlock
        {
            Text = trackName,
            FontSize = 9,
            Foreground = TextSecondaryBrush,
            TextTrimming = TextTrimming.CharacterEllipsis,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(2, 0, 2, 4),
            LayoutTransform = new RotateTransform(-45),
            ToolTip = trackName
        };

        border.Child = textBlock;
        return border;
    }

    private Border CreateRowHeader(string trackName, int index)
    {
        var border = new Border
        {
            Background = HeaderBackgroundBrush,
            BorderBrush = GridLineBrush,
            BorderThickness = new Thickness(0, 0, 1, 1)
        };

        var textBlock = new TextBlock
        {
            Text = trackName,
            FontSize = 9,
            Foreground = TextSecondaryBrush,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 4, 0),
            ToolTip = trackName
        };

        border.Child = textBlock;
        return border;
    }

    private Border CreateMatrixCell(SidechainMatrixCell cellData, int row, int col)
    {
        var border = new Border
        {
            Background = cellData.IsDiagonal ? DiagonalBrush : Brushes.Transparent,
            BorderBrush = GridLineBrush,
            BorderThickness = new Thickness(0, 0, 1, 1),
            Cursor = cellData.IsDiagonal ? Cursors.Arrow : Cursors.Hand,
            Tag = cellData,
            ToolTip = cellData.Tooltip
        };

        var grid = new Grid();

        // Active indicator (green circle)
        var indicator = new Ellipse
        {
            Width = 12,
            Height = 12,
            Fill = ActiveBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = cellData.IsActive ? Visibility.Visible : Visibility.Collapsed
        };
        grid.Children.Add(indicator);

        // Gain indicator for active cells with non-default gain
        if (cellData.IsActive && Math.Abs(cellData.Gain - 1.0f) > 0.01f)
        {
            var gainText = new TextBlock
            {
                Text = $"{cellData.Gain:F1}",
                FontSize = 7,
                Foreground = TextSecondaryBrush,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 2, 1)
            };
            grid.Children.Add(gainText);
        }

        border.Child = grid;

        // Event handlers
        if (!cellData.IsDiagonal)
        {
            border.MouseEnter += (s, e) =>
            {
                if (s is Border b && b.Tag is SidechainMatrixCell)
                {
                    b.Background = HoverBrush;
                    b.BorderBrush = AccentBrush;
                }
            };

            border.MouseLeave += (s, e) =>
            {
                if (s is Border b && b.Tag is SidechainMatrixCell cell)
                {
                    b.Background = cell.IsDiagonal ? DiagonalBrush : Brushes.Transparent;
                    b.BorderBrush = GridLineBrush;
                }
            };

            border.MouseLeftButtonUp += OnCellClick;
            border.MouseRightButtonUp += OnCellRightClick;
        }

        // Subscribe to property changes on the cell data
        cellData.PropertyChanged += (s, e) =>
        {
            if (s is SidechainMatrixCell cell)
            {
                Dispatcher.Invoke(() =>
                {
                    if (e.PropertyName == nameof(SidechainMatrixCell.IsActive))
                    {
                        indicator.Visibility = cell.IsActive ? Visibility.Visible : Visibility.Collapsed;
                        border.ToolTip = cell.Tooltip;
                    }
                    else if (e.PropertyName == nameof(SidechainMatrixCell.Gain))
                    {
                        border.ToolTip = cell.Tooltip;
                    }
                });
            }
        };

        return border;
    }

    private void OnCellClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is SidechainMatrixCell cellData)
        {
            _viewModel?.ToggleRoutingCommand.Execute(cellData);

            // Update the visual
            if (border.Child is Grid grid)
            {
                var indicator = grid.Children.OfType<Ellipse>().FirstOrDefault();
                if (indicator != null)
                {
                    indicator.Visibility = cellData.IsActive ? Visibility.Visible : Visibility.Collapsed;
                }
            }

            border.ToolTip = cellData.Tooltip;
        }
    }

    private void OnCellRightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is SidechainMatrixCell cellData && cellData.IsActive)
        {
            _viewModel?.ShowGainEditorCommand.Execute(cellData);
        }
    }

    private void PresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedItem is SidechainPreset preset)
        {
            _viewModel?.ApplyPresetCommand.Execute(preset);

            // Rebuild the grid to show the new routings
            BuildMatrixGrid();
        }
    }

    /// <summary>
    /// Adds demo tracks for testing purposes.
    /// </summary>
    public void LoadDemoTracks()
    {
        var demoTracks = new List<string>
        {
            "Kick",
            "Snare",
            "Hi-Hat",
            "Bass",
            "Sub Bass",
            "Synth Lead",
            "Synth Pad",
            "Vocal",
            "Guitar",
            "Piano"
        };

        var matrix = new SidechainMatrix();
        Initialize(matrix, demoTracks);
    }
}
