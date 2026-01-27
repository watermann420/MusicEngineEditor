// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code-behind for the ModuleControl.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MusicEngineEditor.ViewModels;

using ContextMenu = System.Windows.Controls.ContextMenu;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Control for displaying and interacting with a modular synth module.
/// </summary>
public partial class ModuleControl : UserControl
{
    private bool _isDragging;
    private Point _dragStartPoint;
    private Point _moduleStartPosition;

    /// <summary>
    /// Event raised when a port is clicked to start or complete a connection.
    /// </summary>
    public event EventHandler<PortClickedEventArgs>? PortClicked;

    /// <summary>
    /// Event raised when the module is selected.
    /// </summary>
    public event EventHandler<ModuleViewModel>? ModuleSelected;

    /// <summary>
    /// Event raised when the module position changes.
    /// </summary>
    public event EventHandler<ModulePositionChangedEventArgs>? PositionChanged;

    /// <summary>
    /// Event raised when delete is requested via context menu or keyboard.
    /// </summary>
    public event EventHandler<ModuleViewModel>? DeleteRequested;

    public ModuleControl()
    {
        InitializeComponent();

        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseMove += OnMouseMove;
        MouseRightButtonUp += OnMouseRightButtonUp;
        KeyDown += OnKeyDown;

        Focusable = true;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not ModuleViewModel module)
            return;

        // Select the module
        ModuleSelected?.Invoke(this, module);

        // Start drag
        _isDragging = true;
        _dragStartPoint = e.GetPosition(Parent as UIElement);
        _moduleStartPosition = new Point(module.X, module.Y);
        CaptureMouse();

        e.Handled = true;
        Focus();
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || DataContext is not ModuleViewModel module)
            return;

        var currentPosition = e.GetPosition(Parent as UIElement);
        var delta = currentPosition - _dragStartPoint;

        var newX = _moduleStartPosition.X + delta.X;
        var newY = _moduleStartPosition.Y + delta.Y;

        // Clamp to positive values
        newX = Math.Max(0, newX);
        newY = Math.Max(0, newY);

        module.X = newX;
        module.Y = newY;

        PositionChanged?.Invoke(this, new ModulePositionChangedEventArgs(module, newX, newY));

        e.Handled = true;
    }

    private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not ModuleViewModel module)
            return;

        // Show context menu
        var contextMenu = new ContextMenu();

        var deleteItem = new MenuItem { Header = "Delete Module" };
        deleteItem.Click += (_, _) => DeleteRequested?.Invoke(this, module);
        contextMenu.Items.Add(deleteItem);

        var duplicateItem = new MenuItem { Header = "Duplicate Module" };
        duplicateItem.Click += (_, _) =>
        {
            // Raise an event or handle duplication
        };
        contextMenu.Items.Add(duplicateItem);

        contextMenu.Items.Add(new Separator());

        var disconnectAllItem = new MenuItem { Header = "Disconnect All" };
        disconnectAllItem.Click += (_, _) =>
        {
            // Raise an event or handle disconnection
        };
        contextMenu.Items.Add(disconnectAllItem);

        contextMenu.IsOpen = true;
        e.Handled = true;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ModuleViewModel module)
            return;

        if (e.Key == Key.Delete || e.Key == Key.Back)
        {
            DeleteRequested?.Invoke(this, module);
            e.Handled = true;
        }
    }

    private void OnPortClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        if (button.DataContext is not PortViewModel port)
            return;

        PortClicked?.Invoke(this, new PortClickedEventArgs(port, GetPortScreenPosition(button)));
        e.Handled = true;
    }

    /// <summary>
    /// Gets the screen position of a port.
    /// </summary>
    public Point GetPortScreenPosition(Button portButton)
    {
        var transform = portButton.TransformToAncestor(this);
        var localPos = transform.Transform(new Point(portButton.ActualWidth / 2, portButton.ActualHeight / 2));
        return localPos;
    }

    /// <summary>
    /// Gets the position of a port relative to a parent element.
    /// </summary>
    public Point GetPortPosition(PortViewModel port, UIElement? relativeTo = null)
    {
        // Find the port button
        var portButton = FindPortButton(port);
        if (portButton == null)
            return new Point();

        if (relativeTo == null)
            relativeTo = this;

        try
        {
            var transform = portButton.TransformToAncestor(relativeTo);
            return transform.Transform(new Point(portButton.ActualWidth / 2, portButton.ActualHeight / 2));
        }
        catch
        {
            return new Point();
        }
    }

    private Button? FindPortButton(PortViewModel port)
    {
        return FindPortButtonRecursive(this, port);
    }

    private static Button? FindPortButtonRecursive(DependencyObject parent, PortViewModel targetPort)
    {
        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is Button button && button.DataContext == targetPort)
            {
                return button;
            }

            var result = FindPortButtonRecursive(child, targetPort);
            if (result != null)
                return result;
        }

        return null;
    }
}

/// <summary>
/// Event arguments for port click events.
/// </summary>
public class PortClickedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the clicked port.
    /// </summary>
    public PortViewModel Port { get; }

    /// <summary>
    /// Gets the screen position of the port.
    /// </summary>
    public Point ScreenPosition { get; }

    public PortClickedEventArgs(PortViewModel port, Point screenPosition)
    {
        Port = port;
        ScreenPosition = screenPosition;
    }
}

/// <summary>
/// Event arguments for module position change events.
/// </summary>
public class ModulePositionChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the module that was moved.
    /// </summary>
    public ModuleViewModel Module { get; }

    /// <summary>
    /// Gets the new X position.
    /// </summary>
    public double NewX { get; }

    /// <summary>
    /// Gets the new Y position.
    /// </summary>
    public double NewY { get; }

    public ModulePositionChangedEventArgs(ModuleViewModel module, double newX, double newY)
    {
        Module = module;
        NewX = newX;
        NewY = newY;
    }
}
