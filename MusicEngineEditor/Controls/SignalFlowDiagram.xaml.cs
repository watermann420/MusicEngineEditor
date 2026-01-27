// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Type of routing node in the signal flow diagram.
/// </summary>
public enum SignalNodeType
{
    /// <summary>Audio track node.</summary>
    Track,
    /// <summary>Bus/group node.</summary>
    Bus,
    /// <summary>Master output node.</summary>
    Master,
    /// <summary>Send node.</summary>
    Send,
    /// <summary>Return node.</summary>
    Return
}

/// <summary>
/// Represents a node in the signal flow diagram.
/// </summary>
public class SignalNode
{
    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the node type.
    /// </summary>
    public SignalNodeType NodeType { get; set; }

    /// <summary>
    /// Gets or sets the node color.
    /// </summary>
    public Color Color { get; set; } = Colors.Gray;

    /// <summary>
    /// Gets or sets the output target ID (where audio routes to).
    /// </summary>
    public string? OutputTargetId { get; set; }

    /// <summary>
    /// Gets or sets the send target IDs.
    /// </summary>
    public List<string> SendTargets { get; set; } = new();

    /// <summary>
    /// Gets or sets the X position in the diagram.
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Gets or sets the Y position in the diagram.
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// Gets or sets whether this node is selected.
    /// </summary>
    public bool IsSelected { get; set; }

    /// <summary>
    /// Gets or sets the visual element for this node.
    /// </summary>
    public FrameworkElement? Visual { get; set; }
}

/// <summary>
/// An interactive signal flow diagram for visualizing audio routing.
/// Shows track nodes, connection lines, bus visualization, and supports click to select/route.
/// </summary>
public partial class SignalFlowDiagram : UserControl
{
    #region Constants

    private const double NodeWidth = 100;
    private const double NodeHeight = 40;
    private const double NodeSpacingX = 150;
    private const double NodeSpacingY = 80;
    private const double MinZoom = 0.5;
    private const double MaxZoom = 2.0;
    private const double ZoomStep = 0.1;

    #endregion

    #region Private Fields

    private readonly Dictionary<string, SignalNode> _nodes = new();
    private readonly List<(string from, string to, bool isSend)> _connections = new();
    private SignalNode? _selectedNode;
    private Point _lastMousePosition;
    private bool _isDragging;
    private double _currentZoom = 1.0;
    private bool _isHierarchyView = true;

    #endregion

    #region Events

    /// <summary>
    /// Event raised when a node is selected.
    /// </summary>
    public event EventHandler<SignalNode>? NodeSelected;

    /// <summary>
    /// Event raised when a connection is requested.
    /// </summary>
#pragma warning disable CS0067 // Event is never used - available for external routing requests
    public event EventHandler<(string fromId, string toId)>? ConnectionRequested;
#pragma warning restore CS0067

    #endregion

    #region Constructor

    public SignalFlowDiagram()
    {
        InitializeComponent();
        UpdateZoomDisplay();
    }

    #endregion

    #region Event Handlers

    private void OnViewModeChanged(object sender, RoutedEventArgs e)
    {
        _isHierarchyView = HierarchyViewButton.IsChecked == true;
        RebuildDiagram();
    }

    private void OnZoomIn(object sender, RoutedEventArgs e)
    {
        SetZoom(_currentZoom + ZoomStep);
    }

    private void OnZoomOut(object sender, RoutedEventArgs e)
    {
        SetZoom(_currentZoom - ZoomStep);
    }

    private void OnFitToView(object sender, RoutedEventArgs e)
    {
        FitToView();
    }

    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _lastMousePosition = e.GetPosition(DiagramScrollViewer);
            _isDragging = true;
            DiagramCanvas.CaptureMouse();
        }
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
        {
            var currentPosition = e.GetPosition(DiagramScrollViewer);
            var delta = currentPosition - _lastMousePosition;

            DiagramScrollViewer.ScrollToHorizontalOffset(DiagramScrollViewer.HorizontalOffset - delta.X);
            DiagramScrollViewer.ScrollToVerticalOffset(DiagramScrollViewer.VerticalOffset - delta.Y);

            _lastMousePosition = currentPosition;
        }
    }

    private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        DiagramCanvas.ReleaseMouseCapture();
    }

    private void OnCanvasMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            double zoomDelta = e.Delta > 0 ? ZoomStep : -ZoomStep;
            SetZoom(_currentZoom + zoomDelta);
            e.Handled = true;
        }
    }

    private void OnNodeClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is SignalNode node)
        {
            SelectNode(node);
            e.Handled = true;
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Clears all nodes and connections.
    /// </summary>
    public void Clear()
    {
        _nodes.Clear();
        _connections.Clear();
        _selectedNode = null;
        DiagramCanvas.Children.Clear();
        SelectionInfoText.Text = "Click a node to select";
    }

    /// <summary>
    /// Adds a node to the diagram.
    /// </summary>
    public void AddNode(string id, string name, SignalNodeType nodeType, Color color)
    {
        var node = new SignalNode
        {
            Id = id,
            Name = name,
            NodeType = nodeType,
            Color = color
        };

        _nodes[id] = node;
    }

    /// <summary>
    /// Adds a connection between two nodes.
    /// </summary>
    /// <param name="fromId">Source node ID.</param>
    /// <param name="toId">Destination node ID.</param>
    /// <param name="isSend">Whether this is a send connection (vs main output).</param>
    public void AddConnection(string fromId, string toId, bool isSend = false)
    {
        _connections.Add((fromId, toId, isSend));

        if (_nodes.TryGetValue(fromId, out var fromNode))
        {
            if (isSend)
            {
                fromNode.SendTargets.Add(toId);
            }
            else
            {
                fromNode.OutputTargetId = toId;
            }
        }
    }

    /// <summary>
    /// Rebuilds and redraws the entire diagram.
    /// </summary>
    public void RebuildDiagram()
    {
        DiagramCanvas.Children.Clear();
        LayoutNodes();
        DrawConnections();
        DrawNodes();
    }

    /// <summary>
    /// Sets the sample data for demonstration/testing.
    /// </summary>
    public void SetSampleData()
    {
        Clear();

        // Add tracks
        AddNode("track1", "Drums", SignalNodeType.Track, Color.FromRgb(0x4A, 0x9E, 0xFF));
        AddNode("track2", "Bass", SignalNodeType.Track, Color.FromRgb(0x34, 0xC7, 0x59));
        AddNode("track3", "Synth", SignalNodeType.Track, Color.FromRgb(0x8B, 0x5C, 0xF6));
        AddNode("track4", "Vocals", SignalNodeType.Track, Color.FromRgb(0xFF, 0x6B, 0x6B));

        // Add buses
        AddNode("bus1", "Drum Bus", SignalNodeType.Bus, Color.FromRgb(0x8B, 0x5C, 0xF6));
        AddNode("bus2", "FX Return", SignalNodeType.Return, Color.FromRgb(0xFF, 0x95, 0x00));

        // Add master
        AddNode("master", "Master", SignalNodeType.Master, Color.FromRgb(0xFF, 0x95, 0x00));

        // Add connections (main routing)
        AddConnection("track1", "bus1");
        AddConnection("track2", "master");
        AddConnection("track3", "master");
        AddConnection("track4", "master");
        AddConnection("bus1", "master");
        AddConnection("bus2", "master");

        // Add send connections
        AddConnection("track3", "bus2", true);
        AddConnection("track4", "bus2", true);

        RebuildDiagram();
    }

    #endregion

    #region Private Methods - Layout

    private void LayoutNodes()
    {
        if (_isHierarchyView)
        {
            LayoutHierarchy();
        }
        else
        {
            LayoutRouting();
        }
    }

    private void LayoutHierarchy()
    {
        // Organize nodes into columns: Tracks -> Buses -> Master
        var tracks = new List<SignalNode>();
        var buses = new List<SignalNode>();
        SignalNode? master = null;

        foreach (var node in _nodes.Values)
        {
            switch (node.NodeType)
            {
                case SignalNodeType.Track:
                    tracks.Add(node);
                    break;
                case SignalNodeType.Bus:
                case SignalNodeType.Return:
                    buses.Add(node);
                    break;
                case SignalNodeType.Master:
                    master = node;
                    break;
            }
        }

        double startX = 50;
        double startY = 50;

        // Layout tracks (column 0)
        for (int i = 0; i < tracks.Count; i++)
        {
            tracks[i].X = startX;
            tracks[i].Y = startY + i * NodeSpacingY;
        }

        // Layout buses (column 1)
        for (int i = 0; i < buses.Count; i++)
        {
            buses[i].X = startX + NodeSpacingX;
            buses[i].Y = startY + i * NodeSpacingY;
        }

        // Layout master (column 2)
        if (master != null)
        {
            master.X = startX + 2 * NodeSpacingX;
            master.Y = startY + Math.Max(tracks.Count, buses.Count) / 2.0 * NodeSpacingY;
        }

        // Update canvas size
        double maxX = startX + 3 * NodeSpacingX;
        double maxY = startY + Math.Max(tracks.Count, buses.Count) * NodeSpacingY;
        DiagramCanvas.Width = maxX;
        DiagramCanvas.Height = maxY;
    }

    private void LayoutRouting()
    {
        // Layout all nodes in a more free-form routing view
        int index = 0;
        foreach (var node in _nodes.Values)
        {
            double row = index / 3;
            double col = index % 3;
            node.X = 50 + col * NodeSpacingX;
            node.Y = 50 + row * NodeSpacingY;
            index++;
        }

        DiagramCanvas.Width = 50 + 4 * NodeSpacingX;
        DiagramCanvas.Height = 50 + ((_nodes.Count / 3) + 1) * NodeSpacingY;
    }

    #endregion

    #region Private Methods - Drawing

    private void DrawConnections()
    {
        foreach (var (fromId, toId, isSend) in _connections)
        {
            if (_nodes.TryGetValue(fromId, out var fromNode) &&
                _nodes.TryGetValue(toId, out var toNode))
            {
                DrawConnection(fromNode, toNode, isSend);
            }
        }
    }

    private void DrawConnection(SignalNode from, SignalNode to, bool isSend)
    {
        double startX = from.X + NodeWidth;
        double startY = from.Y + NodeHeight / 2;
        double endX = to.X;
        double endY = to.Y + NodeHeight / 2;

        // Create a bezier curve for smooth connection
        var bezier = new Path
        {
            Stroke = isSend
                ? FindResource("SendConnectionBrush") as Brush
                : FindResource("ConnectionBrush") as Brush,
            StrokeThickness = 2,
            StrokeDashArray = isSend ? new DoubleCollection { 4, 2 } : null
        };

        double controlPointOffset = (endX - startX) / 2;

        var geometry = new PathGeometry();
        var figure = new PathFigure { StartPoint = new Point(startX, startY) };
        figure.Segments.Add(new BezierSegment(
            new Point(startX + controlPointOffset, startY),
            new Point(endX - controlPointOffset, endY),
            new Point(endX, endY),
            true));
        geometry.Figures.Add(figure);

        bezier.Data = geometry;
        DiagramCanvas.Children.Add(bezier);

        // Add arrow at the end
        DrawArrow(endX, endY, isSend);
    }

    private void DrawArrow(double x, double y, bool isSend)
    {
        var arrow = new Polygon
        {
            Points = new PointCollection
            {
                new Point(x - 8, y - 4),
                new Point(x, y),
                new Point(x - 8, y + 4)
            },
            Fill = isSend
                ? FindResource("SendConnectionBrush") as Brush
                : FindResource("ConnectionBrush") as Brush
        };
        DiagramCanvas.Children.Add(arrow);
    }

    private void DrawNodes()
    {
        foreach (var node in _nodes.Values)
        {
            var nodeVisual = CreateNodeVisual(node);
            Canvas.SetLeft(nodeVisual, node.X);
            Canvas.SetTop(nodeVisual, node.Y);
            DiagramCanvas.Children.Add(nodeVisual);
            node.Visual = nodeVisual;
        }
    }

    private Border CreateNodeVisual(SignalNode node)
    {
        var border = new Border
        {
            Width = NodeWidth,
            Height = NodeHeight,
            Background = FindResource("NodeBrush") as Brush,
            BorderBrush = new SolidColorBrush(node.Color),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(6),
            Cursor = Cursors.Hand,
            Tag = node
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Color indicator
        var colorIndicator = new Rectangle
        {
            Fill = new SolidColorBrush(node.Color),
            RadiusX = 2,
            RadiusY = 2
        };
        Grid.SetColumn(colorIndicator, 0);
        grid.Children.Add(colorIndicator);

        // Node content
        var content = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 4, 0)
        };
        Grid.SetColumn(content, 1);

        var nameText = new TextBlock
        {
            Text = node.Name,
            FontSize = 11,
            Foreground = FindResource("TextPrimaryBrush") as Brush,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        content.Children.Add(nameText);

        var typeText = new TextBlock
        {
            Text = node.NodeType.ToString(),
            FontSize = 9,
            Foreground = FindResource("TextSecondaryBrush") as Brush
        };
        content.Children.Add(typeText);

        grid.Children.Add(content);
        border.Child = grid;

        // Event handlers
        border.MouseLeftButtonDown += OnNodeClick;
        border.MouseEnter += (s, e) => border.Background = FindResource("NodeHoverBrush") as Brush;
        border.MouseLeave += (s, e) =>
        {
            if (node != _selectedNode)
                border.Background = FindResource("NodeBrush") as Brush;
        };

        if (node.IsSelected)
        {
            border.Background = FindResource("NodeSelectedBrush") as Brush;
        }

        return border;
    }

    #endregion

    #region Private Methods - Selection

    private void SelectNode(SignalNode node)
    {
        // Deselect previous
        if (_selectedNode != null)
        {
            _selectedNode.IsSelected = false;
            if (_selectedNode.Visual is Border oldBorder)
            {
                oldBorder.Background = FindResource("NodeBrush") as Brush;
            }
        }

        // Select new
        _selectedNode = node;
        node.IsSelected = true;
        if (node.Visual is Border newBorder)
        {
            newBorder.Background = FindResource("NodeSelectedBrush") as Brush;
        }

        // Update info text
        string info = $"{node.Name} ({node.NodeType})";
        if (node.OutputTargetId != null && _nodes.TryGetValue(node.OutputTargetId, out var target))
        {
            info += $" -> {target.Name}";
        }
        if (node.SendTargets.Count > 0)
        {
            info += $" | Sends: {node.SendTargets.Count}";
        }
        SelectionInfoText.Text = info;

        NodeSelected?.Invoke(this, node);
    }

    #endregion

    #region Private Methods - Zoom

    private void SetZoom(double zoom)
    {
        _currentZoom = Math.Max(MinZoom, Math.Min(MaxZoom, zoom));
        CanvasScale.ScaleX = _currentZoom;
        CanvasScale.ScaleY = _currentZoom;
        UpdateZoomDisplay();
    }

    private void UpdateZoomDisplay()
    {
        ZoomLevelText.Text = $"{_currentZoom * 100:F0}%";
    }

    private void FitToView()
    {
        if (DiagramCanvas.Width <= 0 || DiagramCanvas.Height <= 0)
            return;

        double availableWidth = DiagramScrollViewer.ViewportWidth;
        double availableHeight = DiagramScrollViewer.ViewportHeight;

        if (availableWidth <= 0 || availableHeight <= 0)
            return;

        double scaleX = availableWidth / DiagramCanvas.Width;
        double scaleY = availableHeight / DiagramCanvas.Height;
        double scale = Math.Min(scaleX, scaleY) * 0.9; // 90% to leave margin

        SetZoom(scale);
    }

    #endregion
}
