// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Event args for section events.
/// </summary>
public class SectionEventArgs : EventArgs
{
    public ArrangerSection Section { get; }
    public double OldStartBeat { get; }
    public double NewStartBeat { get; }

    public SectionEventArgs(ArrangerSection section, double oldStart = 0, double newStart = 0)
    {
        Section = section;
        OldStartBeat = oldStart;
        NewStartBeat = newStart;
    }
}

/// <summary>
/// Control for displaying and editing song sections on a timeline.
/// </summary>
public partial class ArrangerTrackControl : UserControl
{
    private readonly Dictionary<Guid, Border> _sectionElements = new();
    private ArrangerSection? _selectedSection;
    private ArrangerSection? _draggingSection;
    private Point _dragStartPoint;
    private double _dragStartBeat;
    private bool _isDragging;
    private double _contextMenuBeat;

    /// <summary>
    /// Gets or sets the collection of sections.
    /// </summary>
    public ObservableCollection<ArrangerSection> Sections { get; } = new();

    /// <summary>
    /// Gets or sets the pixels per beat for horizontal scaling.
    /// </summary>
    public double PixelsPerBeat
    {
        get => (double)GetValue(PixelsPerBeatProperty);
        set => SetValue(PixelsPerBeatProperty, value);
    }

    public static readonly DependencyProperty PixelsPerBeatProperty =
        DependencyProperty.Register(nameof(PixelsPerBeat), typeof(double), typeof(ArrangerTrackControl),
            new PropertyMetadata(20.0, OnPixelsPerBeatChanged));

    /// <summary>
    /// Gets or sets the scroll offset in beats.
    /// </summary>
    public double ScrollOffset
    {
        get => (double)GetValue(ScrollOffsetProperty);
        set => SetValue(ScrollOffsetProperty, value);
    }

    public static readonly DependencyProperty ScrollOffsetProperty =
        DependencyProperty.Register(nameof(ScrollOffset), typeof(double), typeof(ArrangerTrackControl),
            new PropertyMetadata(0.0, OnScrollOffsetChanged));

    /// <summary>
    /// Gets or sets the playback position in beats.
    /// </summary>
    public double PlaybackPosition
    {
        get => (double)GetValue(PlaybackPositionProperty);
        set => SetValue(PlaybackPositionProperty, value);
    }

    public static readonly DependencyProperty PlaybackPositionProperty =
        DependencyProperty.Register(nameof(PlaybackPosition), typeof(double), typeof(ArrangerTrackControl),
            new PropertyMetadata(0.0, OnPlaybackPositionChanged));

    /// <summary>
    /// Event raised when a section is moved.
    /// </summary>
    public event EventHandler<SectionEventArgs>? SectionMoved;

    /// <summary>
    /// Event raised when a section is added.
    /// </summary>
    public event EventHandler<SectionEventArgs>? SectionAdded;

    /// <summary>
    /// Event raised when a section is removed.
    /// </summary>
    public event EventHandler<SectionEventArgs>? SectionRemoved;

    /// <summary>
    /// Event raised when a section is selected.
    /// </summary>
    public event EventHandler<SectionEventArgs>? SectionSelected;

    public ArrangerTrackControl()
    {
        InitializeComponent();

        Sections.CollectionChanged += (_, _) =>
        {
            RefreshSections();
            UpdateSectionCount();
        };

        SizeChanged += (_, _) => RefreshSections();
        Loaded += (_, _) => RefreshSections();
    }

    private static void OnPixelsPerBeatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ArrangerTrackControl control)
            control.RefreshSections();
    }

    private static void OnScrollOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ArrangerTrackControl control)
            control.RefreshSections();
    }

    private static void OnPlaybackPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ArrangerTrackControl control)
            control.UpdatePlayhead();
    }

    /// <summary>
    /// Refreshes the visual display of all sections.
    /// </summary>
    public void RefreshSections()
    {
        // Clear existing section elements
        foreach (var element in _sectionElements.Values)
        {
            ArrangerCanvas.Children.Remove(element);
        }
        _sectionElements.Clear();

        // Draw grid lines
        DrawGridLines();

        // Add section elements
        foreach (var section in Sections)
        {
            var element = CreateSectionElement(section);
            _sectionElements[section.Id] = element;
            ArrangerCanvas.Children.Add(element);
            PositionSectionElement(section, element);
        }

        UpdatePlayhead();
    }

    private void DrawGridLines()
    {
        // Remove existing grid lines
        var linesToRemove = ArrangerCanvas.Children.OfType<Line>()
            .Where(l => l.Tag?.ToString() == "GridLine")
            .ToList();
        foreach (var line in linesToRemove)
        {
            ArrangerCanvas.Children.Remove(line);
        }

        if (ArrangerCanvas.ActualWidth <= 0 || ArrangerCanvas.ActualHeight <= 0)
            return;

        var gridBrush = new SolidColorBrush(Color.FromRgb(0x39, 0x3B, 0x40));
        var strongGridBrush = new SolidColorBrush(Color.FromRgb(0x43, 0x45, 0x4A));

        // Draw vertical lines every bar (4 beats)
        double startBeat = Math.Floor(ScrollOffset / 4) * 4;
        for (double beat = startBeat; beat < ScrollOffset + (ArrangerCanvas.ActualWidth / PixelsPerBeat) + 4; beat += 4)
        {
            double x = (beat - ScrollOffset) * PixelsPerBeat;
            if (x < 0 || x > ArrangerCanvas.ActualWidth) continue;

            var line = new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = ArrangerCanvas.ActualHeight,
                Stroke = beat % 16 == 0 ? strongGridBrush : gridBrush,
                StrokeThickness = beat % 16 == 0 ? 1 : 0.5,
                Tag = "GridLine"
            };
            ArrangerCanvas.Children.Insert(0, line);
        }
    }

    private Border CreateSectionElement(ArrangerSection section)
    {
        var color = ParseColor(section.Color);
        var brush = new SolidColorBrush(color);

        var border = new Border
        {
            Background = brush,
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Tag = section,
            Cursor = Cursors.Hand,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 4,
                ShadowDepth = 1,
                Opacity = 0.3
            },
            Child = new Grid
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = section.Name,
                        Foreground = Brushes.White,
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        VerticalAlignment = System.Windows.VerticalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        Margin = new Thickness(4)
                    }
                }
            }
        };

        // Event handlers
        border.MouseLeftButtonDown += SectionElement_MouseLeftButtonDown;
        border.MouseEnter += SectionElement_MouseEnter;
        border.MouseLeave += SectionElement_MouseLeave;

        // Visual feedback for selection
        if (section.IsSelected)
        {
            border.BorderBrush = new SolidColorBrush(Colors.White);
            border.BorderThickness = new Thickness(2);
        }

        return border;
    }

    private void PositionSectionElement(ArrangerSection section, Border element)
    {
        if (ArrangerCanvas.ActualWidth <= 0)
            return;

        double x = (section.StartBeat - ScrollOffset) * PixelsPerBeat;
        double width = section.LengthBeats * PixelsPerBeat;

        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, 4);
        element.Width = Math.Max(20, width - 2);
        element.Height = ArrangerCanvas.ActualHeight - 8;
    }

    private void UpdatePlayhead()
    {
        if (ArrangerCanvas.ActualWidth <= 0)
            return;

        double x = (PlaybackPosition - ScrollOffset) * PixelsPerBeat;

        if (x >= 0 && x <= ArrangerCanvas.ActualWidth)
        {
            Playhead.Visibility = Visibility.Visible;
            Canvas.SetLeft(Playhead, x);
            Playhead.Height = ArrangerCanvas.ActualHeight;
        }
        else
        {
            Playhead.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateSectionCount()
    {
        SectionCountText.Text = $" ({Sections.Count})";
    }

    private double PositionToBeats(double x)
    {
        return (x / PixelsPerBeat) + ScrollOffset;
    }

    private double SnapToGrid(double beats, double gridSize = 4.0)
    {
        return Math.Round(beats / gridSize) * gridSize;
    }

    private static Color ParseColor(string hex)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return Colors.DodgerBlue;
        }
    }

    private SectionType GetSelectedSectionType()
    {
        if (SectionTypeCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            return Enum.TryParse<SectionType>(tag, out var type) ? type : SectionType.Custom;
        }
        return SectionType.Custom;
    }

    private void SelectSection(ArrangerSection? section)
    {
        // Deselect previous
        if (_selectedSection != null)
        {
            _selectedSection.IsSelected = false;
            if (_sectionElements.TryGetValue(_selectedSection.Id, out var oldElement))
            {
                oldElement.BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
                oldElement.BorderThickness = new Thickness(1);
            }
        }

        _selectedSection = section;

        // Select new
        if (_selectedSection != null)
        {
            _selectedSection.IsSelected = true;
            if (_sectionElements.TryGetValue(_selectedSection.Id, out var newElement))
            {
                newElement.BorderBrush = new SolidColorBrush(Colors.White);
                newElement.BorderThickness = new Thickness(2);
            }
            SectionSelected?.Invoke(this, new SectionEventArgs(_selectedSection));
        }

        // Update context menu state
        RenameMenuItem.IsEnabled = _selectedSection != null;
        SetColorMenuItem.IsEnabled = _selectedSection != null;
        DuplicateMenuItem.IsEnabled = _selectedSection != null;
        DeleteMenuItem.IsEnabled = _selectedSection != null && !_selectedSection.IsLocked;
        LockMenuItem.IsEnabled = _selectedSection != null;
        LockMenuItem.Header = _selectedSection?.IsLocked == true ? "Unlock Section" : "Lock Section";
    }

    #region Event Handlers

    private void SectionElement_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is ArrangerSection section)
        {
            SelectSection(section);

            if (!section.IsLocked)
            {
                _draggingSection = section;
                _dragStartPoint = e.GetPosition(ArrangerCanvas);
                _dragStartBeat = section.StartBeat;
                _isDragging = false;
                border.CaptureMouse();
            }

            e.Handled = true;
        }
    }

    private void SectionElement_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is Border border)
        {
            border.Opacity = 0.9;
        }
    }

    private void SectionElement_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is Border border)
        {
            border.Opacity = 1.0;
        }
    }

    private void ArrangerCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Double-click to add section
        if (e.ClickCount == 2)
        {
            var position = SnapToGrid(PositionToBeats(e.GetPosition(ArrangerCanvas).X));
            AddSectionAtBeat(position);
        }
        else
        {
            // Deselect if clicking on empty space
            SelectSection(null);
        }
    }

    private void ArrangerCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggingSection != null)
        {
            if (_isDragging)
            {
                var oldStart = _dragStartBeat;
                var newStart = _draggingSection.StartBeat;
                if (Math.Abs(oldStart - newStart) > 0.001)
                {
                    SectionMoved?.Invoke(this, new SectionEventArgs(_draggingSection, oldStart, newStart));
                }
            }

            if (_sectionElements.TryGetValue(_draggingSection.Id, out var element))
            {
                element.ReleaseMouseCapture();
            }

            _draggingSection = null;
            _isDragging = false;
        }
    }

    private void ArrangerCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _contextMenuBeat = SnapToGrid(PositionToBeats(e.GetPosition(ArrangerCanvas).X));

        // Check if clicking on a section
        var hitSection = Sections.FirstOrDefault(s =>
            e.GetPosition(ArrangerCanvas).X >= (s.StartBeat - ScrollOffset) * PixelsPerBeat &&
            e.GetPosition(ArrangerCanvas).X <= (s.EndBeat - ScrollOffset) * PixelsPerBeat);

        SelectSection(hitSection);
    }

    private void ArrangerCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingSection != null && e.LeftButton == MouseButtonState.Pressed)
        {
            var currentPoint = e.GetPosition(ArrangerCanvas);
            var deltaX = currentPoint.X - _dragStartPoint.X;

            if (!_isDragging && Math.Abs(deltaX) > 5)
            {
                _isDragging = true;
            }

            if (_isDragging)
            {
                var deltaBeat = deltaX / PixelsPerBeat;
                var newStart = SnapToGrid(_dragStartBeat + deltaBeat);
                newStart = Math.Max(0, newStart);

                _draggingSection.StartBeat = newStart;

                if (_sectionElements.TryGetValue(_draggingSection.Id, out var element))
                {
                    PositionSectionElement(_draggingSection, element);
                }
            }
        }
    }

    private void AddSection_Click(object sender, RoutedEventArgs e)
    {
        // Add section at the end of existing sections or at position 0
        double startBeat = 0;
        if (Sections.Count > 0)
        {
            startBeat = Sections.Max(s => s.EndBeat);
        }

        AddSectionAtBeat(startBeat);
    }

    private void AddSectionHere_Click(object sender, RoutedEventArgs e)
    {
        AddSectionAtBeat(_contextMenuBeat);
    }

    private void AddSectionAtBeat(double startBeat)
    {
        var type = GetSelectedSectionType();
        var section = new ArrangerSection($"{type}", startBeat, 16, type);

        Sections.Add(section);
        SelectSection(section);
        SectionAdded?.Invoke(this, new SectionEventArgs(section));
    }

    private void DeleteSection_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSection == null || _selectedSection.IsLocked)
            return;

        var result = MessageBox.Show(
            $"Delete section '{_selectedSection.Name}'?",
            "Delete Section",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            var section = _selectedSection;
            Sections.Remove(section);
            SectionRemoved?.Invoke(this, new SectionEventArgs(section));
            SelectSection(null);
        }
    }

    private void DuplicateSection_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSection == null)
            return;

        var duplicate = _selectedSection.Clone();
        duplicate.StartBeat = _selectedSection.EndBeat;
        duplicate.Name = _selectedSection.Name + " (Copy)";

        Sections.Add(duplicate);
        SelectSection(duplicate);
        SectionAdded?.Invoke(this, new SectionEventArgs(duplicate));
    }

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSection == null)
            return;

        // Simple input dialog - in production, use a proper dialog
        var dialog = new Window
        {
            Title = "Rename Section",
            Width = 300,
            Height = 120,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this),
            ResizeMode = ResizeMode.NoResize,
            Background = new SolidColorBrush(Color.FromRgb(0x2B, 0x2D, 0x30))
        };

        var stack = new StackPanel { Margin = new Thickness(16) };
        var textBox = new TextBox
        {
            Text = _selectedSection.Name,
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1F, 0x22)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x43, 0x45, 0x4A)),
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 0, 0, 12)
        };
        textBox.SelectAll();

        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        var okButton = new Button { Content = "OK", Width = 70, Margin = new Thickness(0, 0, 8, 0) };
        var cancelButton = new Button { Content = "Cancel", Width = 70 };

        okButton.Click += (_, _) =>
        {
            _selectedSection.Name = textBox.Text;
            RefreshSections();
            dialog.DialogResult = true;
        };
        cancelButton.Click += (_, _) => dialog.DialogResult = false;

        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        stack.Children.Add(textBox);
        stack.Children.Add(buttonPanel);
        dialog.Content = stack;

        textBox.Focus();
        dialog.ShowDialog();
    }

    private void SetColor_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSection == null || sender is not MenuItem menuItem || menuItem.Tag is not string color)
            return;

        _selectedSection.Color = color;
        RefreshSections();
    }

    private void LockSection_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSection == null)
            return;

        _selectedSection.IsLocked = !_selectedSection.IsLocked;
        LockMenuItem.Header = _selectedSection.IsLocked ? "Unlock Section" : "Lock Section";
        DeleteMenuItem.IsEnabled = !_selectedSection.IsLocked;
    }

    #endregion
}
