// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code-behind for the Modular Synth Patch Editor view.

using System;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MusicEngineEditor.Controls;
using MusicEngineEditor.ViewModels;

using ContextMenu = System.Windows.Controls.ContextMenu;

namespace MusicEngineEditor.Views;

/// <summary>
/// View for the Modular Synth Patch Editor.
/// Provides a visual canvas for creating and connecting modular synth modules.
/// </summary>
public partial class ModularSynthView : UserControl
{
    private bool _isPanning;
    private Point _panStartPoint;
    private Point _panStartOffset;
    private Point _connectionStartPoint;

    /// <summary>
    /// Gets the view model.
    /// </summary>
    private ModularSynthViewModel? ViewModel => DataContext as ModularSynthViewModel;

    public ModularSynthView()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
        SizeChanged += OnSizeChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        DrawGridLines();
        UpdateAllCables();
        Focus();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ModularSynthViewModel oldVm)
        {
            oldVm.Modules.CollectionChanged -= OnModulesCollectionChanged;
            oldVm.Cables.CollectionChanged -= OnCablesCollectionChanged;
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (e.NewValue is ModularSynthViewModel newVm)
        {
            newVm.Modules.CollectionChanged += OnModulesCollectionChanged;
            newVm.Cables.CollectionChanged += OnCablesCollectionChanged;
            newVm.PropertyChanged += OnViewModelPropertyChanged;
            UpdateAllCables();
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawGridLines();
    }

    private void OnModulesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Update cables when modules change position
        Dispatcher.BeginInvoke(new Action(UpdateAllCables), System.Windows.Threading.DispatcherPriority.Render);
    }

    private void OnCablesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(UpdateAllCables), System.Windows.Threading.DispatcherPriority.Render);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ModularSynthViewModel.IsConnecting) ||
            e.PropertyName == nameof(ModularSynthViewModel.ConnectionPreviewEnd))
        {
            UpdateConnectionPreview();
        }
        else if (e.PropertyName == nameof(ModularSynthViewModel.ZoomLevel))
        {
            UpdateAllCables();
        }
    }

    /// <summary>
    /// Draws the background grid lines.
    /// </summary>
    private void DrawGridLines()
    {
        GridCanvas.Children.Clear();

        double width = GridCanvas.ActualWidth > 0 ? GridCanvas.ActualWidth : 2000;
        double height = GridCanvas.ActualHeight > 0 ? GridCanvas.ActualHeight : 1500;

        const double minorGridSize = 20;
        const double majorGridSize = 100;

        // Minor grid lines
        var minorBrush = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
        for (double x = 0; x <= width; x += minorGridSize)
        {
            var line = new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = height,
                Stroke = minorBrush,
                StrokeThickness = 0.5
            };
            GridCanvas.Children.Add(line);
        }

        for (double y = 0; y <= height; y += minorGridSize)
        {
            var line = new Line
            {
                X1 = 0,
                Y1 = y,
                X2 = width,
                Y2 = y,
                Stroke = minorBrush,
                StrokeThickness = 0.5
            };
            GridCanvas.Children.Add(line);
        }

        // Major grid lines
        var majorBrush = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x25));
        for (double x = 0; x <= width; x += majorGridSize)
        {
            var line = new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = height,
                Stroke = majorBrush,
                StrokeThickness = 1
            };
            GridCanvas.Children.Add(line);
        }

        for (double y = 0; y <= height; y += majorGridSize)
        {
            var line = new Line
            {
                X1 = 0,
                Y1 = y,
                X2 = width,
                Y2 = y,
                Stroke = majorBrush,
                StrokeThickness = 1
            };
            GridCanvas.Children.Add(line);
        }
    }

    /// <summary>
    /// Updates all cable positions based on module positions.
    /// </summary>
    private void UpdateAllCables()
    {
        if (ViewModel == null) return;

        foreach (var cable in ViewModel.Cables)
        {
            UpdateCablePosition(cable);
        }
    }

    /// <summary>
    /// Updates a single cable's position.
    /// </summary>
    private void UpdateCablePosition(CableViewModel cable)
    {
        // Find the module controls
        var sourceModuleControl = FindModuleControl(cable.SourcePort.Owner);
        var destModuleControl = FindModuleControl(cable.DestinationPort.Owner);

        if (sourceModuleControl == null || destModuleControl == null)
            return;

        // Get port positions relative to the canvas
        var sourcePos = GetPortPositionOnCanvas(sourceModuleControl, cable.SourcePort);
        var destPos = GetPortPositionOnCanvas(destModuleControl, cable.DestinationPort);

        cable.UpdateEndpoints(sourcePos, destPos);
    }

    /// <summary>
    /// Finds the ModuleControl for a given ModuleViewModel.
    /// </summary>
    private ModuleControl? FindModuleControl(ModuleViewModel moduleVm)
    {
        foreach (var item in ModulesItemsControl.Items)
        {
            if (item != moduleVm) continue;

            var container = ModulesItemsControl.ItemContainerGenerator.ContainerFromItem(item) as ContentPresenter;
            if (container != null)
            {
                return FindVisualChild<ModuleControl>(container);
            }
        }
        return null;
    }

    /// <summary>
    /// Gets the position of a port on the canvas.
    /// </summary>
    private Point GetPortPositionOnCanvas(ModuleControl moduleControl, PortViewModel port)
    {
        var portPos = moduleControl.GetPortPosition(port, moduleControl);
        var modulePos = new Point(port.Owner.X, port.Owner.Y);

        return new Point(modulePos.X + portPos.X, modulePos.Y + portPos.Y);
    }

    /// <summary>
    /// Updates the connection preview line.
    /// </summary>
    private void UpdateConnectionPreview()
    {
        if (ViewModel == null || !ViewModel.IsConnecting || ViewModel.PendingConnectionSource == null)
        {
            ConnectionPreviewPath.Data = null;
            return;
        }

        // Find the source port position
        var sourceModuleControl = FindModuleControl(ViewModel.PendingConnectionSource.Owner);
        if (sourceModuleControl == null)
        {
            ConnectionPreviewPath.Data = null;
            return;
        }

        var startPos = GetPortPositionOnCanvas(sourceModuleControl, ViewModel.PendingConnectionSource);
        var endPos = ViewModel.ConnectionPreviewEnd;

        _connectionStartPoint = startPos;

        // Create bezier curve
        var geometry = new PathGeometry();
        var figure = new PathFigure { StartPoint = startPos };

        // Calculate control points for a natural cable droop
        var controlPoint1 = new Point(startPos.X + Math.Abs(endPos.X - startPos.X) * 0.5, startPos.Y);
        var controlPoint2 = new Point(endPos.X - Math.Abs(endPos.X - startPos.X) * 0.5, endPos.Y);

        figure.Segments.Add(new BezierSegment(controlPoint1, controlPoint2, endPos, true));
        geometry.Figures.Add(figure);

        ConnectionPreviewPath.Data = geometry;
    }

    #region Mouse Event Handlers

    private void OnCanvasMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel == null) return;

        // Check if clicking on empty space (not on a module)
        if (e.OriginalSource == CanvasContainer || e.OriginalSource == PatchCanvas || e.OriginalSource == GridCanvas)
        {
            // Deselect everything
            ViewModel.SelectModule(null);
            ViewModel.SelectCable(null);

            // Start panning with middle button or Ctrl+Left
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                _isPanning = true;
                _panStartPoint = e.GetPosition(this);
                _panStartOffset = new Point(ViewModel.PanX, ViewModel.PanY);
                CaptureMouse();
            }
        }

        Focus();
    }

    private void OnCanvasMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel == null) return;

        // Cancel any pending connection
        if (ViewModel.IsConnecting)
        {
            ViewModel.CancelConnectionCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Show context menu for adding modules
        if (e.OriginalSource == CanvasContainer || e.OriginalSource == PatchCanvas || e.OriginalSource == GridCanvas)
        {
            var position = e.GetPosition(PatchCanvas);
            ShowAddModuleContextMenu(position);
            e.Handled = true;
        }
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        if (ViewModel == null) return;

        var currentPos = e.GetPosition(PatchCanvas);

        // Handle panning
        if (_isPanning)
        {
            var screenPos = e.GetPosition(this);
            var delta = screenPos - _panStartPoint;
            ViewModel.PanX = _panStartOffset.X + delta.X;
            ViewModel.PanY = _panStartOffset.Y + delta.Y;
        }
        // Handle connection preview
        else if (ViewModel.IsConnecting)
        {
            ViewModel.UpdateConnectionPreview(currentPos);
        }
    }

    private void OnCanvasMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (ViewModel == null) return;

        // Zoom with mouse wheel
        double zoomDelta = e.Delta > 0 ? 0.1 : -0.1;
        double newZoom = Math.Clamp(ViewModel.ZoomLevel + zoomDelta, 0.25, 3.0);
        ViewModel.ZoomLevel = newZoom;

        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (_isPanning)
        {
            _isPanning = false;
            ReleaseMouseCapture();
        }
    }

    #endregion

    #region Module Event Handlers

    private void OnModulePortClicked(object? sender, PortClickedEventArgs e)
    {
        if (ViewModel == null) return;

        if (!ViewModel.IsConnecting)
        {
            // Start a new connection
            ViewModel.StartConnection(e.Port);
            _connectionStartPoint = GetPortPositionOnCanvas(sender as ModuleControl, e.Port);
        }
        else
        {
            // Complete the connection
            ViewModel.CompleteConnection(e.Port);
            UpdateAllCables();
        }
    }

    private void OnModuleSelected(object? sender, ModuleViewModel module)
    {
        ViewModel?.SelectModule(module);
    }

    private void OnModulePositionChanged(object? sender, ModulePositionChangedEventArgs e)
    {
        // Update cables connected to this module
        if (ViewModel == null) return;

        foreach (var cable in ViewModel.Cables.Where(c =>
            c.SourcePort.Owner == e.Module || c.DestinationPort.Owner == e.Module))
        {
            UpdateCablePosition(cable);
        }
    }

    private void OnModuleDeleteRequested(object? sender, ModuleViewModel module)
    {
        ViewModel?.DeleteModule(module);
    }

    #endregion

    #region Cable Event Handlers

    private void OnCableMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel == null) return;

        if (sender is Path path && path.DataContext is CableViewModel cable)
        {
            ViewModel.SelectCable(cable);
            e.Handled = true;
        }
    }

    #endregion

    #region Context Menu

    private void ShowAddModuleContextMenu(Point position)
    {
        if (ViewModel == null) return;

        var contextMenu = new ContextMenu();

        // Oscillators
        var oscMenu = new MenuItem { Header = "Oscillators" };
        AddModuleMenuItem(oscMenu, "VCO", ModuleType.VCO, position);
        AddModuleMenuItem(oscMenu, "Noise", ModuleType.Noise, position);
        contextMenu.Items.Add(oscMenu);

        // Filters
        var filterMenu = new MenuItem { Header = "Filters" };
        AddModuleMenuItem(filterMenu, "VCF", ModuleType.VCF, position);
        contextMenu.Items.Add(filterMenu);

        // Amplifiers
        var ampMenu = new MenuItem { Header = "Amplifiers" };
        AddModuleMenuItem(ampMenu, "VCA", ModuleType.VCA, position);
        contextMenu.Items.Add(ampMenu);

        // Modulators
        var modMenu = new MenuItem { Header = "Modulators" };
        AddModuleMenuItem(modMenu, "LFO", ModuleType.LFO, position);
        AddModuleMenuItem(modMenu, "ADSR", ModuleType.ADSR, position);
        contextMenu.Items.Add(modMenu);

        // Utilities
        var utilMenu = new MenuItem { Header = "Utilities" };
        AddModuleMenuItem(utilMenu, "Mixer", ModuleType.Mixer, position);
        AddModuleMenuItem(utilMenu, "Quantizer", ModuleType.Quantizer, position);
        AddModuleMenuItem(utilMenu, "Sample & Hold", ModuleType.SampleAndHold, position);
        AddModuleMenuItem(utilMenu, "Slew Limiter", ModuleType.SlewLimiter, position);
        AddModuleMenuItem(utilMenu, "Multiply", ModuleType.Multiply, position);
        AddModuleMenuItem(utilMenu, "Utility", ModuleType.Utility, position);
        contextMenu.Items.Add(utilMenu);

        // Sequencing
        var seqMenu = new MenuItem { Header = "Sequencing" };
        AddModuleMenuItem(seqMenu, "Sequencer", ModuleType.Sequencer, position);
        AddModuleMenuItem(seqMenu, "Clock", ModuleType.Clock, position);
        contextMenu.Items.Add(seqMenu);

        // Effects
        var fxMenu = new MenuItem { Header = "Effects" };
        AddModuleMenuItem(fxMenu, "Delay", ModuleType.Delay, position);
        contextMenu.Items.Add(fxMenu);

        // Logic
        var logicMenu = new MenuItem { Header = "Logic" };
        AddModuleMenuItem(logicMenu, "Logic Gates", ModuleType.Logic, position);
        contextMenu.Items.Add(logicMenu);

        // Output
        contextMenu.Items.Add(new Separator());
        AddModuleMenuItem(contextMenu, "Output", ModuleType.Output, position);

        contextMenu.IsOpen = true;
    }

    private void AddModuleMenuItem(ItemsControl parent, string header, ModuleType type, Point position)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => ViewModel?.AddModuleAt(type, position.X, position.Y);
        parent.Items.Add(item);
    }

    #endregion

    #region Helper Methods

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T result)
                return result;

            var descendant = FindVisualChild<T>(child);
            if (descendant != null)
                return descendant;
        }
        return null;
    }

    #endregion
}
