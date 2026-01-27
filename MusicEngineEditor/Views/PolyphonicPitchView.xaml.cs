// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code-behind for the Polyphonic Pitch Editor View.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using MusicEngineEditor.ViewModels;
using NAudio.Wave;

namespace MusicEngineEditor.Views;

/// <summary>
/// Interaction logic for PolyphonicPitchView.xaml
/// Provides a Melodyne DNA-style polyphonic pitch editor with note blobs.
/// </summary>
public partial class PolyphonicPitchView : UserControl
{
    #region Constants

    private const double NoteHeight = 16.0;
    private const double PixelsPerSecond = 100.0;
    private const int LowestNote = 24;  // C1
    private const int HighestNote = 108; // C8
    private const double MinBlobHeight = 8.0;
    private const double MaxBlobHeight = 24.0;

    // Colors
    private static readonly Color GridLineColor = Color.FromRgb(0x1F, 0x1F, 0x1F);
    private static readonly Color SemitoneLineColor = Color.FromRgb(0x2A, 0x2A, 0x2A);
    private static readonly Color OctaveLineColor = Color.FromRgb(0x3A, 0x3A, 0x3A);
    private static readonly Color WhiteKeyColor = Color.FromRgb(0x28, 0x28, 0x28);
    private static readonly Color BlackKeyColor = Color.FromRgb(0x18, 0x18, 0x18);
    private static readonly Color AccentColor = Color.FromRgb(0x00, 0xD9, 0xFF);
    private static readonly Color AccentSecondaryColor = Color.FromRgb(0x00, 0xFF, 0x88);
    private static readonly Color SelectionColor = Color.FromArgb(0x40, 0x00, 0xD9, 0xFF);
    private static readonly Color ModifiedColor = Color.FromRgb(0xFF, 0xD7, 0x00);
    private static readonly Color DriftColor = Color.FromArgb(0x80, 0x00, 0xD9, 0xFF);

    #endregion

    #region Private Fields

    private PolyphonicPitchViewModel? _viewModel;
    private readonly Dictionary<Guid, UIElement> _noteBlobElements = new();

    // Interaction state
    private bool _isDragging;
    private bool _isSelecting;
    private Point _dragStartPoint;
    private Point _lastMousePosition;
    private NoteBlobViewModel? _draggedNote;
    private List<NoteBlobViewModel>? _draggedNotes;
    private double _dragStartPitch;
    private double _dragStartTime;

    // Cached brushes
    private readonly Brush _gridLineBrush;
    private readonly Brush _semitoneLineBrush;
    private readonly Brush _octaveLineBrush;
    private readonly Brush _whiteKeyBrush;
    private readonly Brush _blackKeyBrush;
    private readonly Brush _accentBrush;
    private readonly Brush _selectionBrush;
    private readonly Brush _driftBrush;

    #endregion

    #region Constructor

    public PolyphonicPitchView()
    {
        InitializeComponent();

        // Initialize brushes
        _gridLineBrush = new SolidColorBrush(GridLineColor);
        _semitoneLineBrush = new SolidColorBrush(SemitoneLineColor);
        _octaveLineBrush = new SolidColorBrush(OctaveLineColor);
        _whiteKeyBrush = new SolidColorBrush(WhiteKeyColor);
        _blackKeyBrush = new SolidColorBrush(BlackKeyColor);
        _accentBrush = new SolidColorBrush(AccentColor);
        _selectionBrush = new SolidColorBrush(SelectionColor);
        _driftBrush = new SolidColorBrush(DriftColor);

        _gridLineBrush.Freeze();
        _semitoneLineBrush.Freeze();
        _octaveLineBrush.Freeze();
        _whiteKeyBrush.Freeze();
        _blackKeyBrush.Freeze();
        _accentBrush.Freeze();
        _selectionBrush.Freeze();
        _driftBrush.Freeze();

        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    #endregion

    #region Event Handlers

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.NoteBlobs.CollectionChanged -= OnNoteBlobsChanged;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as PolyphonicPitchViewModel;

        if (_viewModel != null)
        {
            _viewModel.NoteBlobs.CollectionChanged += OnNoteBlobsChanged;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            UpdateToolButtonStates();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RedrawAll();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RedrawAll();
    }

    private void OnNoteBlobsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(RedrawNoteBlobs));
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PolyphonicPitchViewModel.ZoomX):
            case nameof(PolyphonicPitchViewModel.ZoomY):
            case nameof(PolyphonicPitchViewModel.ScrollX):
            case nameof(PolyphonicPitchViewModel.ScrollY):
            case nameof(PolyphonicPitchViewModel.ShowPitchDrift):
            case nameof(PolyphonicPitchViewModel.ShowPitchGrid):
            case nameof(PolyphonicPitchViewModel.ShowVoiceColors):
            case nameof(PolyphonicPitchViewModel.HighlightModified):
                Dispatcher.BeginInvoke(new Action(RedrawAll));
                break;

            case nameof(PolyphonicPitchViewModel.PlayheadPosition):
                Dispatcher.BeginInvoke(new Action(UpdatePlayhead));
                break;

            case nameof(PolyphonicPitchViewModel.CurrentTool):
                Dispatcher.BeginInvoke(new Action(UpdateToolButtonStates));
                break;
        }
    }

    private void UpdateToolButtonStates()
    {
        if (_viewModel == null)
            return;

        SelectToolButton.IsChecked = _viewModel.CurrentTool == PolyphonicPitchTool.Select;
        PitchToolButton.IsChecked = _viewModel.CurrentTool == PolyphonicPitchTool.PitchCorrect;
        TimeToolButton.IsChecked = _viewModel.CurrentTool == PolyphonicPitchTool.TimeCorrect;
        FormantToolButton.IsChecked = _viewModel.CurrentTool == PolyphonicPitchTool.Formant;
        SplitToolButton.IsChecked = _viewModel.CurrentTool == PolyphonicPitchTool.Split;
    }

    #endregion

    #region Drawing Methods

    private void RedrawAll()
    {
        DrawPianoKeyboard();
        DrawTimeRuler();
        DrawGrid();
        RedrawNoteBlobs();
        UpdatePlayhead();
    }

    private void DrawPianoKeyboard()
    {
        PianoKeyboardCanvas.Children.Clear();

        if (_viewModel == null)
            return;

        double canvasHeight = PianoKeyboardCanvas.ActualHeight;
        if (canvasHeight <= 0)
            return;

        double noteHeight = NoteHeight * _viewModel.ZoomY;
        double scrollY = _viewModel.ScrollY;

        int visibleNoteCount = (int)(canvasHeight / noteHeight) + 2;
        int centerNote = (int)scrollY;
        int startNote = Math.Max(LowestNote, centerNote - visibleNoteCount / 2);
        int endNote = Math.Min(HighestNote, centerNote + visibleNoteCount / 2);

        for (int note = startNote; note <= endNote; note++)
        {
            double y = GetYForPitch(note);

            bool isBlackKey = IsBlackKey(note);
            var brush = isBlackKey ? _blackKeyBrush : _whiteKeyBrush;

            // Key rectangle
            var keyRect = new Rectangle
            {
                Width = PianoKeyboardCanvas.ActualWidth,
                Height = noteHeight,
                Fill = brush,
                Stroke = _gridLineBrush,
                StrokeThickness = 0.5
            };

            Canvas.SetLeft(keyRect, 0);
            Canvas.SetTop(keyRect, y);
            PianoKeyboardCanvas.Children.Add(keyRect);

            // Note name for C notes
            if (note % 12 == 0)
            {
                int octave = note / 12 - 1;
                var label = new TextBlock
                {
                    Text = $"C{octave}",
                    Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80)),
                    FontSize = 9
                };
                Canvas.SetLeft(label, 4);
                Canvas.SetTop(label, y + noteHeight / 2 - 6);
                PianoKeyboardCanvas.Children.Add(label);
            }
        }
    }

    private void DrawTimeRuler()
    {
        TimeRulerCanvas.Children.Clear();

        if (_viewModel == null)
            return;

        double canvasWidth = TimeRulerCanvas.ActualWidth;
        if (canvasWidth <= 0)
            return;

        double pixelsPerSecond = PixelsPerSecond * _viewModel.ZoomX;
        double scrollX = _viewModel.ScrollX;

        double startTime = scrollX;
        double endTime = scrollX + canvasWidth / pixelsPerSecond;

        // Determine tick interval based on zoom
        double tickInterval = 1.0; // 1 second
        if (pixelsPerSecond < 50)
            tickInterval = 5.0;
        else if (pixelsPerSecond < 100)
            tickInterval = 2.0;
        else if (pixelsPerSecond > 200)
            tickInterval = 0.5;
        else if (pixelsPerSecond > 400)
            tickInterval = 0.25;

        double firstTick = Math.Ceiling(startTime / tickInterval) * tickInterval;

        for (double time = firstTick; time <= endTime; time += tickInterval)
        {
            double x = GetXForTime(time);

            // Tick line
            var line = new Line
            {
                X1 = x,
                Y1 = 16,
                X2 = x,
                Y2 = 24,
                Stroke = _gridLineBrush,
                StrokeThickness = 1
            };
            TimeRulerCanvas.Children.Add(line);

            // Time label
            var label = new TextBlock
            {
                Text = FormatTime(time),
                Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80)),
                FontSize = 9
            };
            Canvas.SetLeft(label, x + 2);
            Canvas.SetTop(label, 4);
            TimeRulerCanvas.Children.Add(label);
        }
    }

    private void DrawGrid()
    {
        // Remove existing grid lines
        var gridLines = NoteBlobCanvas.Children.OfType<Line>()
            .Where(l => l.Tag?.ToString() == "grid")
            .ToList();
        foreach (var line in gridLines)
        {
            NoteBlobCanvas.Children.Remove(line);
        }

        if (_viewModel == null || !_viewModel.ShowPitchGrid)
            return;

        double canvasWidth = NoteBlobCanvas.ActualWidth;
        double canvasHeight = NoteBlobCanvas.ActualHeight;

        if (canvasWidth <= 0 || canvasHeight <= 0)
            return;

        double noteHeight = NoteHeight * _viewModel.ZoomY;
        double pixelsPerSecond = PixelsPerSecond * _viewModel.ZoomX;

        // Horizontal lines (pitch grid)
        int centerNote = (int)_viewModel.ScrollY;
        int visibleNotes = (int)(canvasHeight / noteHeight) + 2;

        for (int note = centerNote - visibleNotes / 2; note <= centerNote + visibleNotes / 2; note++)
        {
            if (note < LowestNote || note > HighestNote)
                continue;

            double y = GetYForPitch(note);

            Brush lineBrush = _semitoneLineBrush;
            double thickness = 0.5;

            // Highlight octave lines
            if (note % 12 == 0)
            {
                lineBrush = _octaveLineBrush;
                thickness = 1.0;
            }

            var line = new Line
            {
                X1 = 0,
                Y1 = y,
                X2 = canvasWidth,
                Y2 = y,
                Stroke = lineBrush,
                StrokeThickness = thickness,
                Tag = "grid"
            };
            NoteBlobCanvas.Children.Insert(0, line);
        }

        // Vertical lines (time grid)
        if (_viewModel.ShowTimeGrid)
        {
            double startTime = _viewModel.ScrollX;
            double endTime = startTime + canvasWidth / pixelsPerSecond;

            double gridInterval = 0.5; // Half second
            if (pixelsPerSecond < 50)
                gridInterval = 2.0;
            else if (pixelsPerSecond > 200)
                gridInterval = 0.25;

            double firstGrid = Math.Ceiling(startTime / gridInterval) * gridInterval;

            for (double time = firstGrid; time <= endTime; time += gridInterval)
            {
                double x = GetXForTime(time);

                var line = new Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = canvasHeight,
                    Stroke = _gridLineBrush,
                    StrokeThickness = 0.5,
                    Tag = "grid"
                };
                NoteBlobCanvas.Children.Insert(0, line);
            }
        }
    }

    private void RedrawNoteBlobs()
    {
        // Remove existing note blobs
        var existingBlobs = NoteBlobCanvas.Children.OfType<FrameworkElement>()
            .Where(e => e.Tag?.ToString() == "noteblob" || e.Tag?.ToString() == "drift")
            .ToList();
        foreach (var blob in existingBlobs)
        {
            NoteBlobCanvas.Children.Remove(blob);
        }

        _noteBlobElements.Clear();

        if (_viewModel == null)
            return;

        foreach (var noteBlob in _viewModel.NoteBlobs)
        {
            DrawNoteBlob(noteBlob);
        }
    }

    private void DrawNoteBlob(NoteBlobViewModel noteBlob)
    {
        if (_viewModel == null)
            return;

        double x = GetXForTime(noteBlob.StartTime);
        double y = GetYForPitch(noteBlob.Pitch);
        double width = noteBlob.Duration * PixelsPerSecond * _viewModel.ZoomX;
        double height = NoteHeight * _viewModel.ZoomY;

        // Adjust height based on amplitude if enabled
        if (_viewModel.ShowAmplitudeSize)
        {
            double amplitudeScale = 0.5 + noteBlob.Amplitude * 0.5;
            height *= amplitudeScale;
            height = Math.Clamp(height, MinBlobHeight, MaxBlobHeight * _viewModel.ZoomY);
            y -= (height - NoteHeight * _viewModel.ZoomY) / 2; // Center vertically
        }

        // Draw pitch drift visualization first (behind the blob)
        if (_viewModel.ShowPitchDrift && noteBlob.PitchContour != null && noteBlob.PitchContour.Length > 1)
        {
            DrawPitchDrift(noteBlob, x, width);
        }

        // Create the note blob
        var blob = new Rectangle
        {
            Width = Math.Max(4, width),
            Height = height,
            RadiusX = 4,
            RadiusY = 4,
            Tag = "noteblob"
        };

        // Set fill based on state
        if (noteBlob.IsSelected)
        {
            blob.Fill = CreateSelectedGradient();
            blob.Stroke = new SolidColorBrush(Colors.White);
            blob.StrokeThickness = 2;
        }
        else if (_viewModel.HighlightModified && noteBlob.IsModified)
        {
            blob.Fill = CreateModifiedGradient(noteBlob);
            blob.Stroke = new SolidColorBrush(ModifiedColor);
            blob.StrokeThickness = 1;
        }
        else if (_viewModel.ShowVoiceColors)
        {
            blob.Fill = CreateVoiceGradient(noteBlob.DisplayColor);
            blob.Stroke = new SolidColorBrush(noteBlob.DisplayColor);
            blob.StrokeThickness = 1;
        }
        else
        {
            blob.Fill = CreateDefaultGradient();
            blob.Stroke = _accentBrush;
            blob.StrokeThickness = 1;
        }

        // Set opacity based on confidence
        blob.Opacity = 0.5 + noteBlob.Confidence * 0.5;

        Canvas.SetLeft(blob, x);
        Canvas.SetTop(blob, y);

        // Add event handlers for interaction
        blob.MouseLeftButtonDown += (s, e) => OnNoteBlobMouseDown(noteBlob, e);
        blob.MouseEnter += (s, e) => OnNoteBlobMouseEnter(noteBlob, blob);
        blob.MouseLeave += (s, e) => OnNoteBlobMouseLeave(noteBlob, blob);
        blob.Cursor = Cursors.Hand;

        // Add tooltip
        blob.ToolTip = $"{noteBlob.NoteName}\n" +
                       $"Time: {noteBlob.StartTime:F3}s - {noteBlob.EndTime:F3}s\n" +
                       $"Pitch: {noteBlob.Pitch:F2} (orig: {noteBlob.OriginalPitch:F2})\n" +
                       $"Formant: {noteBlob.Formant:+#.#;-#.#;0} st\n" +
                       $"Amplitude: {noteBlob.Amplitude * 100:F0}%";

        NoteBlobCanvas.Children.Add(blob);
        _noteBlobElements[noteBlob.Id] = blob;
    }

    private void DrawPitchDrift(NoteBlobViewModel noteBlob, double startX, double width)
    {
        if (_viewModel == null || noteBlob.PitchContour == null || noteBlob.PitchContour.Length < 2)
            return;

        var points = new PointCollection();
        int contourLength = noteBlob.PitchContour.Length;

        for (int i = 0; i < contourLength; i++)
        {
            double t = (double)i / (contourLength - 1);
            double x = startX + t * width;
            double y = GetYForPitch(noteBlob.PitchContour[i]);
            points.Add(new Point(x, y));
        }

        var polyline = new Polyline
        {
            Points = points,
            Stroke = _driftBrush,
            StrokeThickness = 2,
            Tag = "drift"
        };

        NoteBlobCanvas.Children.Add(polyline);
    }

    private void UpdatePlayhead()
    {
        if (_viewModel == null)
            return;

        double x = GetXForTime(_viewModel.PlayheadPosition);
        Canvas.SetLeft(PlayheadLine, x);
    }

    #endregion

    #region Gradient Creation

    private LinearGradientBrush CreateDefaultGradient()
    {
        var gradient = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1)
        };
        gradient.GradientStops.Add(new GradientStop(AccentColor, 0));
        gradient.GradientStops.Add(new GradientStop(AccentSecondaryColor, 1));
        gradient.Freeze();
        return gradient;
    }

    private LinearGradientBrush CreateSelectedGradient()
    {
        var gradient = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1)
        };
        gradient.GradientStops.Add(new GradientStop(Colors.White, 0));
        gradient.GradientStops.Add(new GradientStop(AccentColor, 1));
        gradient.Freeze();
        return gradient;
    }

    private LinearGradientBrush CreateModifiedGradient(NoteBlobViewModel noteBlob)
    {
        var gradient = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1)
        };
        gradient.GradientStops.Add(new GradientStop(ModifiedColor, 0));
        gradient.GradientStops.Add(new GradientStop(noteBlob.DisplayColor, 1));
        gradient.Freeze();
        return gradient;
    }

    private LinearGradientBrush CreateVoiceGradient(Color voiceColor)
    {
        var gradient = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1)
        };
        gradient.GradientStops.Add(new GradientStop(voiceColor, 0));
        gradient.GradientStops.Add(new GradientStop(Color.FromArgb(voiceColor.A,
            (byte)(voiceColor.R * 0.7),
            (byte)(voiceColor.G * 0.7),
            (byte)(voiceColor.B * 0.7)), 1));
        gradient.Freeze();
        return gradient;
    }

    #endregion

    #region Coordinate Conversion

    private double GetXForTime(double time)
    {
        if (_viewModel == null)
            return 0;

        return (time - _viewModel.ScrollX) * PixelsPerSecond * _viewModel.ZoomX;
    }

    private double GetTimeForX(double x)
    {
        if (_viewModel == null)
            return 0;

        return x / (PixelsPerSecond * _viewModel.ZoomX) + _viewModel.ScrollX;
    }

    private double GetYForPitch(double pitch)
    {
        if (_viewModel == null)
            return 0;

        double canvasHeight = NoteBlobCanvas.ActualHeight;
        double noteHeight = NoteHeight * _viewModel.ZoomY;
        double centerY = canvasHeight / 2;

        // Y increases downward, higher pitch = lower Y
        return centerY - (pitch - _viewModel.ScrollY) * noteHeight;
    }

    private double GetPitchForY(double y)
    {
        if (_viewModel == null)
            return 60;

        double canvasHeight = NoteBlobCanvas.ActualHeight;
        double noteHeight = NoteHeight * _viewModel.ZoomY;
        double centerY = canvasHeight / 2;

        return _viewModel.ScrollY - (y - centerY) / noteHeight;
    }

    #endregion

    #region Mouse Interaction

    private void OnCanvasMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel == null)
            return;

        var position = e.GetPosition(NoteBlobCanvas);
        _dragStartPoint = position;
        _lastMousePosition = position;

        // Check if clicking on empty space
        var hitTest = VisualTreeHelper.HitTest(NoteBlobCanvas, position);
        if (hitTest?.VisualHit is Rectangle rect && rect.Tag?.ToString() == "noteblob")
        {
            // Handled by note blob click handler
            return;
        }

        // Start selection rectangle
        if (_viewModel.CurrentTool == PolyphonicPitchTool.Select)
        {
            if (!Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
            {
                _viewModel.DeselectAll();
            }

            _isSelecting = true;
            Canvas.SetLeft(SelectionRectangle, position.X);
            Canvas.SetTop(SelectionRectangle, position.Y);
            SelectionRectangle.Width = 0;
            SelectionRectangle.Height = 0;
            SelectionRectangle.Visibility = Visibility.Visible;
            NoteBlobCanvas.CaptureMouse();
        }
    }

    private void OnCanvasMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel == null)
            return;

        if (_isSelecting)
        {
            // Complete selection
            var rect = new Rect(
                Canvas.GetLeft(SelectionRectangle),
                Canvas.GetTop(SelectionRectangle),
                SelectionRectangle.Width,
                SelectionRectangle.Height);

            double startTime = GetTimeForX(rect.Left);
            double endTime = GetTimeForX(rect.Right);
            int lowNote = (int)GetPitchForY(rect.Bottom);
            int highNote = (int)GetPitchForY(rect.Top);

            _viewModel.SelectNotesInRange(startTime, endTime, lowNote, highNote);

            SelectionRectangle.Visibility = Visibility.Collapsed;
            _isSelecting = false;
            NoteBlobCanvas.ReleaseMouseCapture();
        }

        if (_isDragging)
        {
            _isDragging = false;
            _draggedNote = null;
            _draggedNotes = null;
            NoteBlobCanvas.ReleaseMouseCapture();
        }
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        if (_viewModel == null)
            return;

        var position = e.GetPosition(NoteBlobCanvas);

        if (_isSelecting)
        {
            // Update selection rectangle
            double x = Math.Min(position.X, _dragStartPoint.X);
            double y = Math.Min(position.Y, _dragStartPoint.Y);
            double width = Math.Abs(position.X - _dragStartPoint.X);
            double height = Math.Abs(position.Y - _dragStartPoint.Y);

            Canvas.SetLeft(SelectionRectangle, x);
            Canvas.SetTop(SelectionRectangle, y);
            SelectionRectangle.Width = width;
            SelectionRectangle.Height = height;
        }
        else if (_isDragging && _draggedNotes != null)
        {
            // Handle dragging
            double deltaX = position.X - _lastMousePosition.X;
            double deltaY = position.Y - _lastMousePosition.Y;

            switch (_viewModel.CurrentTool)
            {
                case PolyphonicPitchTool.Select:
                case PolyphonicPitchTool.PitchCorrect:
                    // Vertical drag = pitch change
                    double pitchDelta = -deltaY / (NoteHeight * _viewModel.ZoomY);
                    if (_viewModel.SnapToPitch)
                    {
                        pitchDelta = Math.Round(pitchDelta);
                    }
                    if (Math.Abs(pitchDelta) >= 0.01)
                    {
                        _viewModel.TransposeSelected((float)pitchDelta);
                    }
                    break;

                case PolyphonicPitchTool.TimeCorrect:
                    // Horizontal drag = time change
                    double timeDelta = deltaX / (PixelsPerSecond * _viewModel.ZoomX);
                    if (_viewModel.SnapToTime)
                    {
                        timeDelta = Math.Round(timeDelta / _viewModel.TimeGridValue) * _viewModel.TimeGridValue;
                    }
                    if (Math.Abs(timeDelta) >= 0.001)
                    {
                        _viewModel.MoveTimeSelected(timeDelta);
                    }
                    break;

                case PolyphonicPitchTool.Formant:
                    // Vertical drag = formant change
                    float formantDelta = (float)(-deltaY / (NoteHeight * _viewModel.ZoomY * 2));
                    if (Math.Abs(formantDelta) >= 0.1f)
                    {
                        foreach (var note in _draggedNotes)
                        {
                            note.Formant += formantDelta;
                        }
                    }
                    break;
            }

            _lastMousePosition = position;
            RedrawNoteBlobs();
        }
    }

    private void OnCanvasMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_viewModel == null)
            return;

        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        {
            // Zoom
            double zoomDelta = e.Delta > 0 ? 0.1 : -0.1;

            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                // Vertical zoom
                _viewModel.ZoomY = Math.Clamp(_viewModel.ZoomY + zoomDelta, 0.1, 20.0);
            }
            else
            {
                // Horizontal zoom
                _viewModel.ZoomX = Math.Clamp(_viewModel.ZoomX + zoomDelta, 0.1, 20.0);
            }

            e.Handled = true;
        }
        else
        {
            // Scroll
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                // Horizontal scroll
                _viewModel.ScrollX = Math.Max(0, _viewModel.ScrollX - e.Delta / 120.0 * 0.5);
            }
            else
            {
                // Vertical scroll (pitch)
                _viewModel.ScrollY = Math.Clamp(_viewModel.ScrollY + e.Delta / 120.0 * 2, LowestNote, HighestNote);
            }

            e.Handled = true;
        }
    }

    private void OnNoteBlobMouseDown(NoteBlobViewModel noteBlob, MouseButtonEventArgs e)
    {
        if (_viewModel == null)
            return;

        e.Handled = true;

        // Handle selection
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        {
            // Toggle selection
            _viewModel.ToggleNoteSelection(noteBlob);
        }
        else if (!noteBlob.IsSelected)
        {
            // Single selection
            _viewModel.DeselectAll();
            _viewModel.ToggleNoteSelection(noteBlob);
        }

        // Handle tool-specific actions
        switch (_viewModel.CurrentTool)
        {
            case PolyphonicPitchTool.Split:
                var position = e.GetPosition(NoteBlobCanvas);
                double splitTime = GetTimeForX(position.X);
                _viewModel.SplitNote(noteBlob, splitTime);
                break;

            default:
                // Start dragging
                _isDragging = true;
                _draggedNote = noteBlob;
                _draggedNotes = _viewModel.SelectedNotes.ToList();
                _dragStartPitch = noteBlob.Pitch;
                _dragStartTime = noteBlob.StartTime;
                _lastMousePosition = e.GetPosition(NoteBlobCanvas);
                NoteBlobCanvas.CaptureMouse();
                break;
        }
    }

    private void OnNoteBlobMouseEnter(NoteBlobViewModel noteBlob, Rectangle rect)
    {
        if (!noteBlob.IsSelected)
        {
            rect.StrokeThickness = 2;
        }
    }

    private void OnNoteBlobMouseLeave(NoteBlobViewModel noteBlob, Rectangle rect)
    {
        if (!noteBlob.IsSelected)
        {
            rect.StrokeThickness = 1;
        }
    }

    #endregion

    #region File Operations

    private void OnLoadAudioClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null)
            return;

        var dialog = new OpenFileDialog
        {
            Title = "Load Audio File",
            Filter = "Audio Files (*.wav;*.mp3;*.flac;*.ogg)|*.wav;*.mp3;*.flac;*.ogg|All Files (*.*)|*.*",
            FilterIndex = 1
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                LoadAudioFile(dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load audio file:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void LoadAudioFile(string filePath)
    {
        if (_viewModel == null)
            return;

        using var reader = new AudioFileReader(filePath);

        // Convert to mono if needed
        int sampleRate = reader.WaveFormat.SampleRate;
        int channels = reader.WaveFormat.Channels;
        int totalSamples = (int)(reader.Length / reader.WaveFormat.BlockAlign);

        var samples = new float[totalSamples * channels];
        int read = reader.Read(samples, 0, samples.Length);

        float[] monoSamples;
        if (channels > 1)
        {
            monoSamples = new float[read / channels];
            for (int i = 0; i < monoSamples.Length; i++)
            {
                float sum = 0;
                for (int ch = 0; ch < channels; ch++)
                {
                    sum += samples[i * channels + ch];
                }
                monoSamples[i] = sum / channels;
            }
        }
        else
        {
            monoSamples = new float[read];
            Array.Copy(samples, monoSamples, read);
        }

        _viewModel.LoadAudio(monoSamples, sampleRate);
    }

    #endregion

    #region Helper Methods

    private static bool IsBlackKey(int midiNote)
    {
        int noteInOctave = midiNote % 12;
        return noteInOctave == 1 || noteInOctave == 3 || noteInOctave == 6 ||
               noteInOctave == 8 || noteInOctave == 10;
    }

    private static string FormatTime(double seconds)
    {
        if (seconds < 60)
            return $"{seconds:F1}s";

        int minutes = (int)(seconds / 60);
        double remainingSeconds = seconds % 60;
        return $"{minutes}:{remainingSeconds:00.0}";
    }

    #endregion
}
