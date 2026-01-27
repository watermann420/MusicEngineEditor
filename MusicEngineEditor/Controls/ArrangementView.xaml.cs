// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Main arrangement/timeline view.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MusicEngine.Core;
using MusicEngineEditor.Models;
using MusicEngineEditor.Services;
using MusicEngineEditor.ViewModels;
using Line = System.Windows.Shapes.Line;
using SectionType = MusicEngine.Core.SectionType;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Control for displaying and editing song arrangement with sections.
/// </summary>
public partial class ArrangementView : UserControl
{
    private ArrangementViewModel? _viewModel;
    private readonly Dictionary<Guid, UIElement> _sectionElements = [];
    private ArrangementSection? _selectedSection;
    private ArrangementSection? _draggingSection;
    private Point _dragStartPoint;
    private double _dragStartPosition;
    private bool _isDragging;
    private bool _isResizing;
    private bool _isResizingLeft;
    private double _contextMenuPosition;

    // Scrubbing support
    private bool _isScrubbing;
    private Point _scrubStartPoint;

    // Waveform support
    private WaveformService? _waveformService;
    private WaveformData? _audioWaveformData;
    private string? _loadedAudioFilePath;

    // Clips support
    private readonly ObservableCollection<ClipViewModel> _clips = [];
    private readonly Dictionary<Guid, UIElement> _clipElements = [];
    private ClipViewModel? _selectedClip;
    private ClipViewModel? _draggingClip;
    private bool _isClipDragging;
    private bool _isClipResizingLeft;
    private bool _isClipResizingRight;
    private Point _clipDragStartPoint;
    private double _clipDragStartPosition = 0.0;
    private double _clipDragStartLength = 0.0;
    private double _clipContextMenuPosition;

    /// <summary>
    /// Gets or sets the view model.
    /// </summary>
    public ArrangementViewModel? ViewModel
    {
        get => _viewModel;
        set
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            _viewModel = value;
            DataContext = _viewModel;

            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
                MarkerTrackControl.MarkerTrack = _viewModel.Arrangement?.MarkerTrack;
            }

            RefreshSections();
        }
    }

    /// <summary>
    /// Event raised when the playback position should change.
    /// </summary>
    public event EventHandler<double>? SeekRequested;

    /// <summary>
    /// Event raised when a section is selected.
    /// </summary>
    public event EventHandler<ArrangementSection>? SectionSelected;

    /// <summary>
    /// Event raised when a clip is selected.
    /// </summary>
    public event EventHandler<ClipViewModel>? ClipSelected;

    /// <summary>
    /// Gets the collection of clips in the arrangement.
    /// </summary>
    public ObservableCollection<ClipViewModel> Clips => _clips;

    /// <summary>
    /// Gets or sets whether the clips area is visible.
    /// </summary>
    public bool IsClipsAreaVisible
    {
        get => ClipsAreaBorder.Visibility == Visibility.Visible;
        set => ClipsAreaBorder.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    public ArrangementView()
    {
        InitializeComponent();
        SizeChanged += (_, _) => RefreshView();
        Loaded += ArrangementView_Loaded;

        // Subscribe to clips collection changes
        _clips.CollectionChanged += (_, _) => RefreshClips();
    }

    private void ArrangementView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null)
        {
            // Create default view model if not set
            ViewModel = new ArrangementViewModel();
        }

        RefreshView();
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ArrangementViewModel.Arrangement):
                MarkerTrackControl.MarkerTrack = _viewModel?.Arrangement?.MarkerTrack;
                RefreshSections();
                UpdateWaveformSync();
                break;
            case nameof(ArrangementViewModel.PlaybackPosition):
                UpdatePlayhead();
                MarkerTrackControl.PlaybackPosition = _viewModel?.PlaybackPosition ?? 0;
                UpdateWaveformSync();
                break;
            case nameof(ArrangementViewModel.VisibleBeats):
            case nameof(ArrangementViewModel.ScrollOffset):
                RefreshView();
                MarkerTrackControl.VisibleBeats = _viewModel?.VisibleBeats ?? 64;
                MarkerTrackControl.ScrollOffset = _viewModel?.ScrollOffset ?? 0;
                UpdateWaveformSync();
                break;
        }
    }

    /// <summary>
    /// Refreshes the entire view.
    /// </summary>
    public void RefreshView()
    {
        RefreshTimeline();
        RefreshSections();
        RefreshClips();
        UpdatePlayhead();
        UpdateMarkerTrack();
    }

    private void UpdateMarkerTrack()
    {
        if (_viewModel == null) return;

        var pixelsPerBeat = SectionCanvas.ActualWidth / _viewModel.VisibleBeats;
        ArrangementMarkerTrack.PixelsPerBeat = pixelsPerBeat;
        ArrangementMarkerTrack.ScrollOffset = _viewModel.ScrollOffset;
        ArrangementMarkerTrack.PlayheadPosition = _viewModel.PlaybackPosition;
    }

    private void RefreshTimeline()
    {
        TimelineRuler.Children.Clear();

        if (_viewModel == null || SectionCanvas.ActualWidth <= 0)
            return;

        var pixelsPerBeat = SectionCanvas.ActualWidth / _viewModel.VisibleBeats;
        var beatsPerBar = _viewModel.Arrangement?.TimeSignatureNumerator ?? 4;

        // Draw bar lines and numbers
        var startBeat = _viewModel.ScrollOffset;
        var endBeat = startBeat + _viewModel.VisibleBeats;

        // Determine grid spacing based on zoom
        var gridSpacing = beatsPerBar;
        if (pixelsPerBeat * beatsPerBar < 30) gridSpacing *= 2;
        if (pixelsPerBeat * beatsPerBar < 15) gridSpacing *= 2;

        for (var beat = Math.Floor(startBeat / gridSpacing) * gridSpacing; beat <= endBeat; beat += gridSpacing)
        {
            var x = (beat - startBeat) * pixelsPerBeat;
            if (x < 0) continue;

            // Bar line
            var line = new Line
            {
                X1 = x,
                Y1 = 16,
                X2 = x,
                Y2 = 24,
                Stroke = Brushes.Gray,
                StrokeThickness = 1
            };
            TimelineRuler.Children.Add(line);

            // Bar number
            var barNumber = (int)(beat / beatsPerBar) + 1;
            var text = new TextBlock
            {
                Text = barNumber.ToString(),
                FontSize = 10,
                Foreground = new SolidColorBrush(Colors.Gray)
            };
            Canvas.SetLeft(text, x + 2);
            Canvas.SetTop(text, 2);
            TimelineRuler.Children.Add(text);
        }
    }

    /// <summary>
    /// Refreshes the section display.
    /// </summary>
    public void RefreshSections()
    {
        // Clear existing section elements
        foreach (var element in _sectionElements.Values)
        {
            SectionCanvas.Children.Remove(element);
        }
        _sectionElements.Clear();

        if (_viewModel?.Arrangement == null)
            return;

        // Update canvas width based on total length
        var pixelsPerBeat = Math.Max(1, SectionCanvas.ActualWidth / _viewModel.VisibleBeats);
        SectionCanvas.Width = Math.Max(SectionCanvas.ActualWidth, _viewModel.Arrangement.TotalLength * pixelsPerBeat + 100);

        // Draw grid lines
        DrawGridLines(pixelsPerBeat);

        // Add section elements
        foreach (var section in _viewModel.Arrangement.Sections)
        {
            var element = CreateSectionElement(section);
            _sectionElements[section.Id] = element;
            SectionCanvas.Children.Add(element);
            PositionSectionElement(section, element, pixelsPerBeat);
        }

        // Ensure playhead is on top
        if (SectionCanvas.Children.Contains(Playhead))
        {
            SectionCanvas.Children.Remove(Playhead);
            SectionCanvas.Children.Add(Playhead);
        }
    }

    private void DrawGridLines(double pixelsPerBeat)
    {
        var beatsPerBar = _viewModel?.Arrangement?.TimeSignatureNumerator ?? 4;
        var totalBeats = _viewModel?.Arrangement?.TotalLength ?? 64;

        // Determine grid spacing based on zoom
        var gridSpacing = beatsPerBar;
        if (pixelsPerBeat * beatsPerBar < 30) gridSpacing *= 2;
        if (pixelsPerBeat * beatsPerBar < 15) gridSpacing *= 2;

        for (double beat = 0; beat <= totalBeats + gridSpacing; beat += gridSpacing)
        {
            var x = beat * pixelsPerBeat;
            var line = new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = SectionCanvas.ActualHeight,
                Stroke = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                StrokeThickness = beat % (beatsPerBar * 4) == 0 ? 1 : 0.5,
                IsHitTestVisible = false
            };
            SectionCanvas.Children.Add(line);
        }
    }

    private UIElement CreateSectionElement(ArrangementSection section)
    {
        var color = ParseColor(section.Color);
        var brush = new SolidColorBrush(color);
        var lightBrush = new SolidColorBrush(Color.FromArgb(180, color.R, color.G, color.B));

        var container = new Grid
        {
            Tag = section,
            Cursor = Cursors.Hand,
            Opacity = section.IsMuted ? 0.5 : 1.0
        };

        // Main section rectangle
        var rect = new Border
        {
            Background = lightBrush,
            BorderBrush = brush,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(1, 4, 1, 4)
        };

        // Section content
        var contentGrid = new Grid();
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Header with name
        var header = new Border
        {
            Background = brush,
            CornerRadius = new CornerRadius(2, 2, 0, 0),
            Padding = new Thickness(6, 2, 6, 2)
        };

        var headerContent = new StackPanel { Orientation = Orientation.Horizontal };
        headerContent.Children.Add(new TextBlock
        {
            Text = section.Name,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White
        });

        if (section.RepeatCount > 1)
        {
            headerContent.Children.Add(new TextBlock
            {
                Text = $" x{section.RepeatCount}",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        if (section.IsLocked)
        {
            headerContent.Children.Add(new TextBlock
            {
                Text = " [Locked]",
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)),
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        header.Child = headerContent;
        Grid.SetRow(header, 0);
        contentGrid.Children.Add(header);

        // Info area
        var info = new StackPanel
        {
            Margin = new Thickness(6, 4, 6, 4),
            VerticalAlignment = VerticalAlignment.Center
        };

        info.Children.Add(new TextBlock
        {
            Text = $"{section.StartPosition:F1} - {section.EndPosition:F1} ({section.Length:F1} beats)",
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255))
        });

        Grid.SetRow(info, 1);
        contentGrid.Children.Add(info);

        rect.Child = contentGrid;
        container.Children.Add(rect);

        // Resize handles
        var leftHandle = new Rectangle
        {
            Width = 6,
            Fill = Brushes.Transparent,
            Cursor = Cursors.SizeWE,
            HorizontalAlignment = HorizontalAlignment.Left,
            Tag = "LeftHandle"
        };

        var rightHandle = new Rectangle
        {
            Width = 6,
            Fill = Brushes.Transparent,
            Cursor = Cursors.SizeWE,
            HorizontalAlignment = HorizontalAlignment.Right,
            Tag = "RightHandle"
        };

        container.Children.Add(leftHandle);
        container.Children.Add(rightHandle);

        // Event handlers
        container.MouseLeftButtonDown += SectionElement_MouseLeftButtonDown;
        container.MouseEnter += SectionElement_MouseEnter;
        container.MouseLeave += SectionElement_MouseLeave;
        leftHandle.MouseLeftButtonDown += ResizeHandle_MouseLeftButtonDown;
        rightHandle.MouseLeftButtonDown += ResizeHandle_MouseLeftButtonDown;

        return container;
    }

    private void PositionSectionElement(ArrangementSection section, UIElement element, double pixelsPerBeat)
    {
        if (element is not Grid container)
            return;

        var x = (section.StartPosition - (_viewModel?.ScrollOffset ?? 0)) * pixelsPerBeat;
        var width = section.Length * pixelsPerBeat;

        Canvas.SetLeft(container, x);
        Canvas.SetTop(container, 0);
        container.Width = Math.Max(20, width);
        container.Height = SectionCanvas.ActualHeight > 0 ? SectionCanvas.ActualHeight : 80;
    }

    private void UpdatePlayhead()
    {
        if (_viewModel == null || SectionCanvas.ActualWidth <= 0)
            return;

        var pixelsPerBeat = SectionCanvas.ActualWidth / _viewModel.VisibleBeats;
        var x = (_viewModel.PlaybackPosition - _viewModel.ScrollOffset) * pixelsPerBeat;

        if (x >= 0 && x <= SectionCanvas.Width)
        {
            Playhead.Visibility = Visibility.Visible;
            Canvas.SetLeft(Playhead, x);
            Playhead.Height = SectionCanvas.ActualHeight;
        }
        else
        {
            Playhead.Visibility = Visibility.Collapsed;
        }
    }

    private double PositionToBeats(double x)
    {
        if (_viewModel == null || SectionCanvas.ActualWidth <= 0)
            return 0;

        var pixelsPerBeat = SectionCanvas.ActualWidth / _viewModel.VisibleBeats;
        return (x / pixelsPerBeat) + _viewModel.ScrollOffset;
    }

    private static Color ParseColor(string hex)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return Colors.Blue;
        }
    }

    #region Event Handlers

    private void SectionElement_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Grid container && container.Tag is ArrangementSection section)
        {
            _selectedSection = section;
            _draggingSection = section;
            _dragStartPoint = e.GetPosition(SectionCanvas);
            _dragStartPosition = section.StartPosition;
            _isDragging = false;
            _isResizing = false;

            SectionSelected?.Invoke(this, section);
            container.CaptureMouse();
            e.Handled = true;
        }
    }

    private void ResizeHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Rectangle handle && handle.Parent is Grid container && container.Tag is ArrangementSection section)
        {
            _selectedSection = section;
            _draggingSection = section;
            _dragStartPoint = e.GetPosition(SectionCanvas);
            _dragStartPosition = handle.Tag?.ToString() == "LeftHandle" ? section.StartPosition : section.EndPosition;
            _isResizing = true;
            _isResizingLeft = handle.Tag?.ToString() == "LeftHandle";
            _isDragging = false;

            container.CaptureMouse();
            e.Handled = true;
        }
    }

    private void SectionElement_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is Grid container)
        {
            container.Opacity = container.Tag is ArrangementSection section && section.IsMuted ? 0.6 : 0.9;
        }
    }

    private void SectionElement_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is Grid container)
        {
            container.Opacity = container.Tag is ArrangementSection section && section.IsMuted ? 0.5 : 1.0;
        }
    }

    private void SectionCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Double-click to seek
        if (e.ClickCount == 2)
        {
            var position = PositionToBeats(e.GetPosition(SectionCanvas).X);
            SeekRequested?.Invoke(this, position);
            return;
        }

        // Single click - start scrubbing if clicking in empty area (not on a section)
        var clickedSection = GetSectionAtPosition(e.GetPosition(SectionCanvas));
        if (clickedSection == null)
        {
            StartTimelineScrub(e.GetPosition(SectionCanvas));
            e.Handled = true;
        }
    }

    private void SectionCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _contextMenuPosition = PositionToBeats(e.GetPosition(SectionCanvas).X);

        // Check if clicking on a section
        var hitSection = GetSectionAtPosition(e.GetPosition(SectionCanvas));
        _selectedSection = hitSection;

        // Enable/disable context menu items
        EditSectionMenuItem.IsEnabled = hitSection != null;
        DuplicateSectionMenuItem.IsEnabled = hitSection != null;
        DeleteSectionMenuItem.IsEnabled = hitSection != null && !hitSection.IsLocked;
        SetRepeatMenuItem.IsEnabled = hitSection != null && !hitSection.IsLocked;
        MuteSectionMenuItem.IsEnabled = hitSection != null;
        MuteSectionMenuItem.Header = hitSection?.IsMuted == true ? "Unmute Section" : "Mute Section";
        LockSectionMenuItem.IsEnabled = hitSection != null;
        LockSectionMenuItem.Header = hitSection?.IsLocked == true ? "Unlock Section" : "Lock Section";
    }

    private void SectionCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        // Handle timeline scrubbing
        if (_isScrubbing && e.LeftButton == MouseButtonState.Pressed)
        {
            UpdateTimelineScrub(e.GetPosition(SectionCanvas));
            return;
        }

        // Handle section dragging
        if (_draggingSection != null && e.LeftButton == MouseButtonState.Pressed)
        {
            var currentPoint = e.GetPosition(SectionCanvas);
            var deltaX = currentPoint.X - _dragStartPoint.X;

            if (!_isDragging && Math.Abs(deltaX) > 5)
            {
                _isDragging = true;
            }

            if (_isDragging && !_draggingSection.IsLocked)
            {
                var newPosition = PositionToBeats(currentPoint.X);
                newPosition = Math.Max(0, newPosition);

                // Snap to grid (quarter note)
                newPosition = Math.Round(newPosition * 4) / 4;

                if (_isResizing)
                {
                    if (_isResizingLeft)
                    {
                        var newStart = Math.Min(newPosition, _draggingSection.EndPosition - 1);
                        _draggingSection.StartPosition = newStart;
                    }
                    else
                    {
                        var newEnd = Math.Max(newPosition, _draggingSection.StartPosition + 1);
                        _draggingSection.EndPosition = newEnd;
                    }
                }
                else
                {
                    _viewModel?.Arrangement?.MoveSection(_draggingSection, newPosition);
                }

                _draggingSection.Touch();
                RefreshSections();
            }
        }
    }

    private void SectionCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // Handle timeline scrub end
        if (_isScrubbing)
        {
            // Check if Shift is held to continue playback
            var continuePlayback = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            EndTimelineScrub(continuePlayback);
            return;
        }

        // Handle section drag end
        if (_draggingSection != null)
        {
            // Find and release the container
            if (_sectionElements.TryGetValue(_draggingSection.Id, out var element) && element is Grid container)
            {
                container.ReleaseMouseCapture();
            }

            _draggingSection = null;
            _isDragging = false;
            _isResizing = false;
        }
    }

    private ArrangementSection? GetSectionAtPosition(Point point)
    {
        var beats = PositionToBeats(point.X);
        return _viewModel?.Arrangement?.GetSectionAt(beats);
    }

    private void SectionScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_viewModel == null) return;

        var pixelsPerBeat = SectionCanvas.ActualWidth / _viewModel.VisibleBeats;
        if (pixelsPerBeat > 0)
        {
            _viewModel.ScrollOffset = e.HorizontalOffset / pixelsPerBeat;
        }
    }

    private void AddSection_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && _viewModel?.Arrangement != null)
        {
            var typeName = button.Content?.ToString() ?? "Section";
            var type = GetSectionTypeFromName(typeName);

            // Add at end of arrangement
            var startPosition = _viewModel.Arrangement.TotalLength;
            var length = 16.0; // Default 4 bars at 4/4

            _viewModel.Arrangement.AddSection(startPosition, startPosition + length, type);
            RefreshSections();
        }
    }

    private void AddSectionHere_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.Arrangement == null) return;

        var startPosition = Math.Round(_contextMenuPosition * 4) / 4; // Snap to quarter note
        var length = 16.0;

        _viewModel.Arrangement.AddSection(startPosition, startPosition + length, "New Section");
        RefreshSections();
    }

    private void AddSectionType_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && _viewModel?.Arrangement != null)
        {
            var typeTag = menuItem.Tag?.ToString() ?? "Custom";
            var type = Enum.TryParse<SectionType>(typeTag, out var parsed) ? parsed : SectionType.Custom;

            var startPosition = Math.Round(_contextMenuPosition * 4) / 4;
            var length = 16.0;

            _viewModel.Arrangement.AddSection(startPosition, startPosition + length, type);
            RefreshSections();
        }
    }

    private void CreateStandardStructure_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "This will replace the current arrangement with a standard song structure. Continue?",
            "Create Standard Structure",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes && _viewModel != null)
        {
            _viewModel.Arrangement = Arrangement.CreateStandardStructure();
            MarkerTrackControl.MarkerTrack = _viewModel.Arrangement.MarkerTrack;
            RefreshSections();
        }
    }

    private void EditSection_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSection == null) return;

        MessageBox.Show(
            $"Edit section: {_selectedSection.Name}\n" +
            $"Position: {_selectedSection.StartPosition:F2} - {_selectedSection.EndPosition:F2}\n" +
            $"Type: {_selectedSection.Type}\n" +
            $"Repeat: {_selectedSection.RepeatCount}x",
            "Edit Section",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void DuplicateSection_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSection == null || _viewModel?.Arrangement == null) return;

        _viewModel.Arrangement.DuplicateSection(_selectedSection);
        RefreshSections();
    }

    private void DeleteSection_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSection == null || _viewModel?.Arrangement == null) return;

        if (_selectedSection.IsLocked)
        {
            MessageBox.Show("Cannot delete a locked section.", "Section Locked",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"Delete section '{_selectedSection.Name}'?",
            "Delete Section",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _viewModel.Arrangement.RemoveSection(_selectedSection);
            _selectedSection = null;
            RefreshSections();
        }
    }

    private void SetRepeat_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSection == null) return;

        // In a full implementation, show a dialog to set repeat count
        _selectedSection.RepeatCount = _selectedSection.RepeatCount >= 4 ? 1 : _selectedSection.RepeatCount + 1;
        _selectedSection.Touch();
        RefreshSections();
    }

    private void MuteSection_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSection == null) return;

        _selectedSection.IsMuted = !_selectedSection.IsMuted;
        _selectedSection.Touch();
        RefreshSections();
    }

    private void LockSection_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSection == null) return;

        _selectedSection.IsLocked = !_selectedSection.IsLocked;
        _selectedSection.Touch();
        RefreshSections();
    }

    private void MarkerTrackControl_MarkerSelected(object? sender, Marker marker)
    {
        // Handle marker selection if needed
    }

    private void MarkerTrackControl_JumpRequested(object? sender, double position)
    {
        SeekRequested?.Invoke(this, position);
    }

    private static SectionType GetSectionTypeFromName(string name)
    {
        return name.Replace("-", "").Replace(" ", "") switch
        {
            "Intro" => SectionType.Intro,
            "Verse" => SectionType.Verse,
            "PreChorus" => SectionType.PreChorus,
            "Chorus" => SectionType.Chorus,
            "PostChorus" => SectionType.PostChorus,
            "Bridge" => SectionType.Bridge,
            "Breakdown" => SectionType.Breakdown,
            "Buildup" or "BuildUp" => SectionType.Buildup,
            "Drop" => SectionType.Drop,
            "Solo" => SectionType.Solo,
            "Interlude" => SectionType.Interlude,
            "Outro" => SectionType.Outro,
            _ => SectionType.Custom
        };
    }

    #endregion

    #region Timeline Scrubbing

    /// <summary>
    /// Starts scrubbing on the timeline.
    /// </summary>
    /// <param name="position">The mouse position on the canvas.</param>
    private void StartTimelineScrub(Point position)
    {
        if (_isScrubbing)
        {
            return;
        }

        _isScrubbing = true;
        _scrubStartPoint = position;

        var beat = PositionToBeats(position.X);
        ScrubService.Instance.StartScrub(beat);

        // Capture mouse for tracking drag outside control bounds
        SectionCanvas.CaptureMouse();

        // Update cursor to indicate scrubbing
        SectionCanvas.Cursor = Cursors.IBeam;
    }

    /// <summary>
    /// Updates the scrub position during drag.
    /// </summary>
    /// <param name="position">The current mouse position.</param>
    private void UpdateTimelineScrub(Point position)
    {
        if (!_isScrubbing)
        {
            return;
        }

        var beat = PositionToBeats(position.X);
        ScrubService.Instance.UpdateScrub(beat);

        // Also update the view model's playback position for visual feedback
        if (_viewModel != null)
        {
            _viewModel.PlaybackPosition = beat;
        }
    }

    /// <summary>
    /// Ends the timeline scrubbing operation.
    /// </summary>
    /// <param name="continuePlayback">Whether to continue playback from the scrub position.</param>
    private void EndTimelineScrub(bool continuePlayback = false)
    {
        if (!_isScrubbing)
        {
            return;
        }

        _isScrubbing = false;
        SectionCanvas.ReleaseMouseCapture();
        SectionCanvas.Cursor = Cursors.Arrow;

        ScrubService.Instance.EndScrub(continuePlayback);

        // Notify that seek was requested
        SeekRequested?.Invoke(this, ScrubService.Instance.ScrubPosition);
    }

    /// <summary>
    /// Cancels the scrubbing operation.
    /// </summary>
    private void CancelTimelineScrub()
    {
        if (!_isScrubbing)
        {
            return;
        }

        _isScrubbing = false;
        SectionCanvas.ReleaseMouseCapture();
        SectionCanvas.Cursor = Cursors.Arrow;

        ScrubService.Instance.CancelScrub();
    }

    #endregion

    #region Waveform / Audio Track

    /// <summary>
    /// Gets or sets whether the audio track is visible.
    /// </summary>
    public bool IsAudioTrackVisible
    {
        get => AudioTrackBorder.Visibility == Visibility.Visible;
        set => AudioTrackBorder.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Gets the currently loaded waveform data.
    /// </summary>
    public WaveformData? AudioWaveformData => _audioWaveformData;

    /// <summary>
    /// Gets or sets the waveform service for loading audio files.
    /// </summary>
    public WaveformService WaveformService
    {
        get
        {
            _waveformService ??= new WaveformService();
            return _waveformService;
        }
        set => _waveformService = value;
    }

    /// <summary>
    /// Loads an audio file into the audio track.
    /// </summary>
    /// <param name="filePath">Path to the audio file.</param>
    public async Task LoadAudioFileAsync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return;

        try
        {
            AudioWaveformDisplay.IsLoading = true;
            IsAudioTrackVisible = true;

            _audioWaveformData = await WaveformService.LoadFromFileAsync(filePath);
            _loadedAudioFilePath = filePath;

            AudioWaveformDisplay.WaveformData = _audioWaveformData;
            AudioTrackName.Text = Path.GetFileName(filePath);

            // Sync zoom and scroll
            SyncWaveformToArrangement();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load audio file: {ex.Message}");
            MessageBox.Show($"Failed to load audio file:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            AudioWaveformDisplay.IsLoading = false;
        }
    }

    /// <summary>
    /// Clears the audio track.
    /// </summary>
    public void ClearAudioTrack()
    {
        _audioWaveformData = null;
        _loadedAudioFilePath = null;
        AudioWaveformDisplay.WaveformData = null;
        AudioTrackName.Text = "";
        IsAudioTrackVisible = false;
    }

    /// <summary>
    /// Shows the audio track (empty if no audio loaded).
    /// </summary>
    public void ShowAudioTrack()
    {
        IsAudioTrackVisible = true;
    }

    /// <summary>
    /// Hides the audio track.
    /// </summary>
    public void HideAudioTrack()
    {
        IsAudioTrackVisible = false;
    }

    private void SyncWaveformToArrangement()
    {
        if (_viewModel == null || _audioWaveformData == null || !_audioWaveformData.IsLoaded)
            return;

        // Calculate samples per pixel based on visible beats and BPM
        var bpm = _viewModel.Arrangement?.Bpm ?? 120;
        var visibleBeats = _viewModel.VisibleBeats;
        var visibleSeconds = visibleBeats / (bpm / 60.0);
        var visibleSamples = (int)(visibleSeconds * _audioWaveformData.SampleRate);
        var canvasWidth = SectionCanvas.ActualWidth;

        if (canvasWidth > 0 && visibleSamples > 0)
        {
            AudioWaveformDisplay.SamplesPerPixel = Math.Max(1, visibleSamples / (int)canvasWidth);
        }

        // Calculate scroll offset
        var scrollBeats = _viewModel.ScrollOffset;
        var scrollSeconds = scrollBeats / (bpm / 60.0);
        var scrollSamples = (long)(scrollSeconds * _audioWaveformData.SampleRate);
        AudioWaveformDisplay.ScrollOffset = scrollSamples;

        // Calculate playhead position
        var playbackBeats = _viewModel.PlaybackPosition;
        var playbackSeconds = playbackBeats / (bpm / 60.0);
        var playbackSamples = (long)(playbackSeconds * _audioWaveformData.SampleRate);
        AudioWaveformDisplay.PlayheadPosition = playbackSamples;
    }

    private void LoadAudio_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Load Audio File",
            Filter = "Audio Files|*.wav;*.mp3;*.aiff;*.aif|WAV Files|*.wav|MP3 Files|*.mp3|All Files|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            _ = LoadAudioFileAsync(dialog.FileName);
        }
    }

    private void ClearAudio_Click(object sender, RoutedEventArgs e)
    {
        ClearAudioTrack();
    }

    private void AudioWaveformDisplay_PlayheadRequested(object? sender, WaveformPositionEventArgs e)
    {
        if (_viewModel == null || _audioWaveformData == null || !_audioWaveformData.IsLoaded)
            return;

        // Convert sample position to beats
        var bpm = _viewModel.Arrangement?.Bpm ?? 120;
        var seconds = (double)e.SamplePosition / _audioWaveformData.SampleRate;
        var beats = seconds * (bpm / 60.0);

        SeekRequested?.Invoke(this, beats);
    }

    /// <summary>
    /// Updates the waveform display when the arrangement view changes.
    /// </summary>
    private void UpdateWaveformSync()
    {
        if (_audioWaveformData != null && _audioWaveformData.IsLoaded)
        {
            SyncWaveformToArrangement();
        }
    }

    #endregion

    #region Clips Management

    /// <summary>
    /// Shows the clips area.
    /// </summary>
    public void ShowClipsArea()
    {
        IsClipsAreaVisible = true;
        RefreshClips();
    }

    /// <summary>
    /// Hides the clips area.
    /// </summary>
    public void HideClipsArea()
    {
        IsClipsAreaVisible = false;
    }

    /// <summary>
    /// Adds an audio clip to the arrangement.
    /// </summary>
    /// <param name="name">The clip name.</param>
    /// <param name="startPosition">Start position in beats.</param>
    /// <param name="length">Length in beats.</param>
    /// <param name="filePath">Optional file path for the audio.</param>
    /// <param name="trackIndex">Track index (default 0).</param>
    /// <returns>The created clip view model.</returns>
    public ClipViewModel AddAudioClip(string name, double startPosition, double length, string? filePath = null, int trackIndex = 0)
    {
        var clip = ClipViewModel.CreateAudioClip(name, startPosition, length, filePath);
        clip.TrackIndex = trackIndex;

        // Subscribe to clip events
        SubscribeToClipEvents(clip);

        _clips.Add(clip);
        ShowClipsArea();

        return clip;
    }

    /// <summary>
    /// Adds a MIDI clip to the arrangement.
    /// </summary>
    /// <param name="name">The clip name.</param>
    /// <param name="startPosition">Start position in beats.</param>
    /// <param name="length">Length in beats.</param>
    /// <param name="trackIndex">Track index (default 0).</param>
    /// <returns>The created clip view model.</returns>
    public ClipViewModel AddMidiClip(string name, double startPosition, double length, int trackIndex = 0)
    {
        var clip = ClipViewModel.CreateMidiClip(name, startPosition, length);
        clip.TrackIndex = trackIndex;

        // Subscribe to clip events
        SubscribeToClipEvents(clip);

        _clips.Add(clip);
        ShowClipsArea();

        return clip;
    }

    /// <summary>
    /// Removes a clip from the arrangement.
    /// </summary>
    /// <param name="clip">The clip to remove.</param>
    public void RemoveClip(ClipViewModel clip)
    {
        UnsubscribeFromClipEvents(clip);
        _clips.Remove(clip);

        if (_clips.Count == 0)
        {
            HideClipsArea();
        }
    }

    /// <summary>
    /// Clears all clips from the arrangement.
    /// </summary>
    public void ClearClips()
    {
        foreach (var clip in _clips)
        {
            UnsubscribeFromClipEvents(clip);
        }
        _clips.Clear();
        HideClipsArea();
    }

    private void SubscribeToClipEvents(ClipViewModel clip)
    {
        clip.DeleteRequested += Clip_DeleteRequested;
        clip.DuplicateRequested += Clip_DuplicateRequested;
        clip.SplitRequested += Clip_SplitRequested;
        clip.PropertyChanged += Clip_PropertyChanged;
    }

    private void UnsubscribeFromClipEvents(ClipViewModel clip)
    {
        clip.DeleteRequested -= Clip_DeleteRequested;
        clip.DuplicateRequested -= Clip_DuplicateRequested;
        clip.SplitRequested -= Clip_SplitRequested;
        clip.PropertyChanged -= Clip_PropertyChanged;
    }

    private void Clip_DeleteRequested(object? sender, ClipViewModel clip)
    {
        RemoveClip(clip);
    }

    private void Clip_DuplicateRequested(object? sender, ClipViewModel newClip)
    {
        SubscribeToClipEvents(newClip);
        _clips.Add(newClip);
    }

    private void Clip_SplitRequested(object? sender, ClipSplitEventArgs e)
    {
        SplitClipAt(e.OriginalClip, e.SplitPosition);
    }

    private void Clip_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ClipViewModel.StartPosition) or nameof(ClipViewModel.Length) or nameof(ClipViewModel.TrackIndex))
        {
            RefreshClips();
        }
    }

    /// <summary>
    /// Splits a clip at the specified position.
    /// </summary>
    /// <param name="clip">The clip to split.</param>
    /// <param name="position">The position in beats.</param>
    public void SplitClipAt(ClipViewModel clip, double position)
    {
        if (position <= clip.StartPosition || position >= clip.EndPosition)
            return;

        var splitOffset = position - clip.StartPosition;

        // Create the second clip
        var secondClip = new ClipViewModel(clip.ClipType)
        {
            Name = clip.Name + " (2)",
            StartPosition = position,
            Length = clip.Length - splitOffset,
            TrackIndex = clip.TrackIndex,
            Color = clip.Color,
            SourceOffset = clip.SourceOffset + splitOffset,
            OriginalLength = clip.OriginalLength,
            FilePath = clip.FilePath,
            WaveformData = clip.WaveformData,
            NoteData = clip.NoteData,
            IsLooping = clip.IsLooping,
            LoopLength = clip.LoopLength,
            FadeOutDuration = clip.FadeOutDuration // Transfer fade out to second clip
        };

        // Modify the first clip
        clip.Length = splitOffset;
        clip.FadeOutDuration = 0; // Remove fade out from first clip

        SubscribeToClipEvents(secondClip);
        _clips.Add(secondClip);
    }

    /// <summary>
    /// Refreshes the clips display.
    /// </summary>
    public void RefreshClips()
    {
        // Clear existing clip elements
        foreach (var element in _clipElements.Values)
        {
            ClipsCanvas.Children.Remove(element);
        }
        _clipElements.Clear();

        if (_viewModel == null || ClipsCanvas.ActualWidth <= 0 || !IsClipsAreaVisible)
            return;

        var pixelsPerBeat = ClipsCanvas.ActualWidth / _viewModel.VisibleBeats;

        // Group clips by track
        var clipsByTrack = new Dictionary<int, List<ClipViewModel>>();
        foreach (var clip in _clips)
        {
            if (!clipsByTrack.ContainsKey(clip.TrackIndex))
            {
                clipsByTrack[clip.TrackIndex] = [];
            }
            clipsByTrack[clip.TrackIndex].Add(clip);
        }

        var trackHeight = 60.0;
        var trackY = 0.0;

        foreach (var trackClips in clipsByTrack.Values)
        {
            foreach (var clip in trackClips)
            {
                var element = CreateClipElement(clip);
                _clipElements[clip.Guid] = element;
                ClipsCanvas.Children.Add(element);
                PositionClipElement(clip, element, pixelsPerBeat, trackY, trackHeight);
            }
            trackY += trackHeight;
        }

        // Update canvas height
        ClipsCanvas.Height = Math.Max(80, trackY);
    }

    private UIElement CreateClipElement(ClipViewModel clip)
    {
        if (clip.ClipType == ClipType.Audio)
        {
            var audioClipControl = new AudioClipControl
            {
                Clip = clip,
                IsSelected = clip.IsSelected,
                PlayheadPosition = _viewModel?.PlaybackPosition ?? 0
            };

            audioClipControl.ClipSelected += ClipControl_ClipSelected;
            audioClipControl.ClipMoved += ClipControl_ClipMoved;
            audioClipControl.ClipResized += ClipControl_ClipResized;
            audioClipControl.SplitRequested += ClipControl_SplitRequested;

            if (clip.WaveformData != null)
            {
                audioClipControl.WaveformData = clip.WaveformData;
            }

            return audioClipControl;
        }
        else
        {
            var midiClipControl = new MidiClipControl
            {
                Clip = clip,
                IsSelected = clip.IsSelected,
                PlayheadPosition = _viewModel?.PlaybackPosition ?? 0
            };

            midiClipControl.ClipSelected += ClipControl_ClipSelected;
            midiClipControl.ClipMoved += ClipControl_ClipMoved;
            midiClipControl.ClipResized += ClipControl_ClipResized;
            midiClipControl.SplitRequested += ClipControl_SplitRequested;

            if (clip.NoteData != null)
            {
                midiClipControl.NoteData = clip.NoteData;
            }

            return midiClipControl;
        }
    }

    private void PositionClipElement(ClipViewModel clip, UIElement element, double pixelsPerBeat, double trackY, double trackHeight)
    {
        var x = (clip.StartPosition - (_viewModel?.ScrollOffset ?? 0)) * pixelsPerBeat;
        var width = clip.Length * pixelsPerBeat;

        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, trackY);

        if (element is FrameworkElement fe)
        {
            fe.Width = Math.Max(20, width);
            fe.Height = trackHeight - 4;
        }
    }

    private void ClipControl_ClipSelected(object? sender, ClipViewModel clip)
    {
        // Deselect all other clips
        foreach (var c in _clips)
        {
            c.IsSelected = c == clip;
        }

        _selectedClip = clip;
        ClipSelected?.Invoke(this, clip);
    }

    private void ClipControl_ClipMoved(object? sender, ClipMovedEventArgs e)
    {
        RefreshClips();
    }

    private void ClipControl_ClipResized(object? sender, ClipResizedEventArgs e)
    {
        RefreshClips();
    }

    private void ClipControl_SplitRequested(object? sender, ClipSplitRequestedEventArgs e)
    {
        SplitClipAt(e.Clip, e.SplitPosition);
    }

    private void ClipsCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Double-click to seek
        if (e.ClickCount == 2)
        {
            var position = PositionToBeatsInClips(e.GetPosition(ClipsCanvas).X);
            SeekRequested?.Invoke(this, position);
            return;
        }

        // Single click - deselect clips if clicking in empty area
        var hitClip = GetClipAtPosition(e.GetPosition(ClipsCanvas));
        if (hitClip == null)
        {
            foreach (var clip in _clips)
            {
                clip.IsSelected = false;
            }
            _selectedClip = null;
        }
    }

    private void ClipsCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isClipDragging = false;
        _isClipResizingLeft = false;
        _isClipResizingRight = false;
        _draggingClip = null;
    }

    private void ClipsCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingClip != null && e.LeftButton == MouseButtonState.Pressed && !_draggingClip.IsLocked)
        {
            var currentPoint = e.GetPosition(ClipsCanvas);
            var deltaX = currentPoint.X - _clipDragStartPoint.X;

            if (!_isClipDragging && Math.Abs(deltaX) > 5)
            {
                _isClipDragging = true;
            }

            if (_isClipDragging)
            {
                var pixelsPerBeat = ClipsCanvas.ActualWidth / (_viewModel?.VisibleBeats ?? 64);
                var deltaBeat = deltaX / pixelsPerBeat;

                if (_isClipResizingLeft)
                {
                    var newStart = _clipDragStartPosition + deltaBeat;
                    newStart = Math.Round(newStart * 4) / 4; // Snap to quarter
                    _draggingClip.ResizeLeft(Math.Max(0, newStart));
                }
                else if (_isClipResizingRight)
                {
                    var newLength = _clipDragStartLength + deltaBeat;
                    newLength = Math.Round(newLength * 4) / 4;
                    _draggingClip.Resize(Math.Max(0.25, newLength));
                }
                else
                {
                    var newPosition = _clipDragStartPosition + deltaBeat;
                    newPosition = Math.Round(newPosition * 4) / 4;
                    _draggingClip.StartPosition = Math.Max(0, newPosition);
                }

                RefreshClips();
            }
        }
    }

    private void ClipsCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _clipContextMenuPosition = PositionToBeatsInClips(e.GetPosition(ClipsCanvas).X);
    }

    private double PositionToBeatsInClips(double x)
    {
        if (_viewModel == null || ClipsCanvas.ActualWidth <= 0)
            return 0;

        var pixelsPerBeat = ClipsCanvas.ActualWidth / _viewModel.VisibleBeats;
        return (x / pixelsPerBeat) + _viewModel.ScrollOffset;
    }

    private ClipViewModel? GetClipAtPosition(Point point)
    {
        var beat = PositionToBeatsInClips(point.X);

        foreach (var clip in _clips)
        {
            if (beat >= clip.StartPosition && beat < clip.EndPosition)
            {
                return clip;
            }
        }

        return null;
    }

    private void AddAudioClip_Click(object sender, RoutedEventArgs e)
    {
        var position = Math.Round(_clipContextMenuPosition * 4) / 4;
        var clip = AddAudioClip("Audio Clip", position, 4.0);

        // In a full implementation, show a file dialog to load audio
    }

    private void AddMidiClip_Click(object sender, RoutedEventArgs e)
    {
        var position = Math.Round(_clipContextMenuPosition * 4) / 4;
        var clip = AddMidiClip("MIDI Clip", position, 4.0);

        // In a full implementation, open piano roll editor
    }

    private void PasteClip_Click(object sender, RoutedEventArgs e)
    {
        // In a full implementation, paste clip from clipboard
    }

    private void ArrangementMarkerTrack_PlayheadRequested(object? sender, double position)
    {
        SeekRequested?.Invoke(this, position);
    }

    #endregion

    #region Drag and Drop Audio Files

    // Preview rectangle for drop location
    private System.Windows.Shapes.Rectangle? _dropPreviewRect;

    /// <summary>
    /// Checks if a file path is a supported audio format.
    /// </summary>
    private static bool IsAudioFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".wav" or ".mp3" or ".flac" or ".ogg" or ".aiff" or ".aif";
    }

    private void ClipsCanvas_DragEnter(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            if (files.Any(IsAudioFile))
            {
                e.Effects = System.Windows.DragDropEffects.Copy;
                ShowDropPreview(e.GetPosition(ClipsCanvas));
                ShowClipsArea();
            }
            else
            {
                e.Effects = System.Windows.DragDropEffects.None;
            }
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void ClipsCanvas_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            if (files.Any(IsAudioFile))
            {
                e.Effects = System.Windows.DragDropEffects.Copy;
                UpdateDropPreview(e.GetPosition(ClipsCanvas));
            }
            else
            {
                e.Effects = System.Windows.DragDropEffects.None;
            }
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void ClipsCanvas_DragLeave(object sender, System.Windows.DragEventArgs e)
    {
        HideDropPreview();
        e.Handled = true;
    }

    private async void ClipsCanvas_Drop(object sender, System.Windows.DragEventArgs e)
    {
        HideDropPreview();

        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            return;

        var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
        var audioFiles = files.Where(IsAudioFile).ToArray();

        if (audioFiles.Length == 0)
            return;

        var dropPosition = e.GetPosition(ClipsCanvas);
        var beatPosition = PositionToBeatsInClips(dropPosition.X);
        beatPosition = Math.Round(beatPosition * 4) / 4; // Snap to quarter note

        // Create clips from dropped files
        foreach (var file in audioFiles)
        {
            await CreateClipFromDroppedFileAsync(file, beatPosition);
            beatPosition += 4; // Stack subsequent files 4 beats apart
        }

        e.Handled = true;
    }

    /// <summary>
    /// Creates an audio clip from a dropped file.
    /// </summary>
    private async Task CreateClipFromDroppedFileAsync(string filePath, double beatPosition)
    {
        try
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);

            // Try to get duration from file
            double lengthInBeats = 4.0; // Default
            try
            {
                using var reader = new NAudio.Wave.AudioFileReader(filePath);
                var duration = reader.TotalTime.TotalSeconds;
                var bpm = _viewModel?.Arrangement?.Bpm ?? 120;
                lengthInBeats = Math.Max(1.0, duration * (bpm / 60.0));
                lengthInBeats = Math.Round(lengthInBeats * 4) / 4; // Snap to quarter note
            }
            catch
            {
                // If we can't read the file, use default length
            }

            // Create the clip
            var clip = AddAudioClip(fileName, beatPosition, lengthInBeats, filePath);

            // Load waveform data asynchronously
            try
            {
                var waveformData = await WaveformService.LoadFromFileAsync(filePath);
                clip.WaveformData = waveformData.Samples;
                RefreshClips();
            }
            catch
            {
                // Waveform loading failed, clip is still usable
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to create clip from {filePath}: {ex.Message}");
        }
    }

    private void ShowDropPreview(Point position)
    {
        if (_dropPreviewRect == null)
        {
            _dropPreviewRect = new System.Windows.Shapes.Rectangle
            {
                Fill = new SolidColorBrush(Color.FromArgb(64, 74, 158, 255)),
                Stroke = new SolidColorBrush(Color.FromRgb(74, 158, 255)),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                RadiusX = 4,
                RadiusY = 4,
                IsHitTestVisible = false,
                Height = 56
            };
            ClipsCanvas.Children.Add(_dropPreviewRect);
        }

        UpdateDropPreview(position);
        _dropPreviewRect.Visibility = Visibility.Visible;
    }

    private void UpdateDropPreview(Point position)
    {
        if (_dropPreviewRect == null || _viewModel == null)
            return;

        var beatPosition = PositionToBeatsInClips(position.X);
        beatPosition = Math.Round(beatPosition * 4) / 4; // Snap to quarter

        var pixelsPerBeat = ClipsCanvas.ActualWidth / _viewModel.VisibleBeats;
        var x = (beatPosition - _viewModel.ScrollOffset) * pixelsPerBeat;
        var width = 4 * pixelsPerBeat; // Default 4-beat preview

        Canvas.SetLeft(_dropPreviewRect, x);
        Canvas.SetTop(_dropPreviewRect, 2);
        _dropPreviewRect.Width = Math.Max(20, width);
    }

    private void HideDropPreview()
    {
        if (_dropPreviewRect != null)
        {
            _dropPreviewRect.Visibility = Visibility.Collapsed;
        }
    }

    #endregion
}
