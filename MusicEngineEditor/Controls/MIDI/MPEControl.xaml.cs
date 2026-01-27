// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: MPE (MIDI Polyphonic Expression) editor control.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Shapes = System.Windows.Shapes;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Controls.MIDI;

/// <summary>
/// MPE (MIDI Polyphonic Expression) Editor Control.
/// Provides per-note expression editing for pitch bend, pressure (aftertouch), and slide (CC74).
/// Supports MPE zone configuration (Lower/Upper/Global) and pitch bend range settings.
/// </summary>
public partial class MPEControl : UserControl, INotifyPropertyChanged
{
    #region Constants

    private const double DefaultBeatWidth = 40.0;
    private const int PitchBendMin = -8192;
    private const int PitchBendMax = 8191;
    private const int PressureMin = 0;
    private const int PressureMax = 127;
    private const int SlideMin = 0;
    private const int SlideMax = 127;
    private const double PointRadius = 4;
    private const double HandleRadius = 6;
    private const double NoteHeight = 16;
    private const int BeatsPerBar = 4;

    #endregion

    #region Private Fields

    private MPEZoneType _activeZone = MPEZoneType.Lower;
    private int _memberChannelCount = 15;
    private int _pitchBendRange = 48;

    private readonly Dictionary<Guid, List<MPEExpressionPoint>> _pitchBendData = [];
    private readonly Dictionary<Guid, List<MPEExpressionPoint>> _pressureData = [];
    private readonly Dictionary<Guid, List<MPEExpressionPoint>> _slideData = [];

    private PianoRollNote? _selectedNote;
    private MPEDrawTool _currentTool = MPEDrawTool.Draw;
    private MPEExpressionLane _activeLane = MPEExpressionLane.PitchBend;

    private bool _isDrawing;
    private Point _lastDrawPoint;
    private Point? _lineStart;
    private double _scrollX;

    // Colors
    private static readonly Color PitchBendColor = Color.FromRgb(0x00, 0xD9, 0xFF);
    private static readonly Color PressureColor = Color.FromRgb(0xFF, 0x98, 0x00);
    private static readonly Color SlideColor = Color.FromRgb(0x9C, 0x27, 0xB0);
    private static readonly Color NoteColor = Color.FromRgb(0x00, 0xCC, 0x66);
    private static readonly Color SelectedNoteColor = Color.FromRgb(0x00, 0xFF, 0x88);

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty NotesProperty =
        DependencyProperty.Register(nameof(Notes), typeof(ObservableCollection<PianoRollNote>), typeof(MPEControl),
            new PropertyMetadata(null, OnNotesChanged));

    public static readonly DependencyProperty SelectedNoteProperty =
        DependencyProperty.Register(nameof(SelectedNote), typeof(PianoRollNote), typeof(MPEControl),
            new PropertyMetadata(null, OnSelectedNoteChanged));

    public static readonly DependencyProperty TotalBeatsProperty =
        DependencyProperty.Register(nameof(TotalBeats), typeof(double), typeof(MPEControl),
            new PropertyMetadata(16.0, OnLayoutPropertyChanged));

    public static readonly DependencyProperty PixelsPerBeatProperty =
        DependencyProperty.Register(nameof(PixelsPerBeat), typeof(double), typeof(MPEControl),
            new PropertyMetadata(DefaultBeatWidth, OnLayoutPropertyChanged));

    public static readonly DependencyProperty ScrollOffsetProperty =
        DependencyProperty.Register(nameof(ScrollOffset), typeof(double), typeof(MPEControl),
            new PropertyMetadata(0.0, OnScrollChanged));

    public static readonly DependencyProperty ZoomXProperty =
        DependencyProperty.Register(nameof(ZoomX), typeof(double), typeof(MPEControl),
            new PropertyMetadata(1.0, OnLayoutPropertyChanged));

    /// <summary>
    /// Gets or sets the notes collection to display.
    /// </summary>
    public ObservableCollection<PianoRollNote>? Notes
    {
        get => (ObservableCollection<PianoRollNote>?)GetValue(NotesProperty);
        set => SetValue(NotesProperty, value);
    }

    /// <summary>
    /// Gets or sets the currently selected note.
    /// </summary>
    public PianoRollNote? SelectedNote
    {
        get => (PianoRollNote?)GetValue(SelectedNoteProperty);
        set => SetValue(SelectedNoteProperty, value);
    }

    /// <summary>
    /// Gets or sets the total number of beats to display.
    /// </summary>
    public double TotalBeats
    {
        get => (double)GetValue(TotalBeatsProperty);
        set => SetValue(TotalBeatsProperty, value);
    }

    /// <summary>
    /// Gets or sets the pixels per beat.
    /// </summary>
    public double PixelsPerBeat
    {
        get => (double)GetValue(PixelsPerBeatProperty);
        set => SetValue(PixelsPerBeatProperty, value);
    }

    /// <summary>
    /// Gets or sets the scroll offset.
    /// </summary>
    public double ScrollOffset
    {
        get => (double)GetValue(ScrollOffsetProperty);
        set => SetValue(ScrollOffsetProperty, value);
    }

    /// <summary>
    /// Gets or sets the horizontal zoom factor.
    /// </summary>
    public double ZoomX
    {
        get => (double)GetValue(ZoomXProperty);
        set => SetValue(ZoomXProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Occurs when MPE expression data changes.
    /// </summary>
    public event EventHandler<MPEExpressionChangedEventArgs>? ExpressionChanged;

    /// <summary>
    /// Occurs when the MPE zone configuration changes.
    /// </summary>
    public event EventHandler<MPEZoneChangedEventArgs>? ZoneConfigurationChanged;

    #endregion

    #region Constructor

    public MPEControl()
    {
        InitializeComponent();
        DataContext = this;

        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;

        // Wire up tool radio buttons
        DrawToolButton.Checked += (_, _) => _currentTool = MPEDrawTool.Draw;
        LineToolButton.Checked += (_, _) => _currentTool = MPEDrawTool.Line;
        EraseToolButton.Checked += (_, _) => _currentTool = MPEDrawTool.Erase;
    }

    #endregion

    #region Lifecycle

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RenderAll();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RenderAll();
    }

    #endregion

    #region Dependency Property Callbacks

    private static void OnNotesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MPEControl control)
        {
            control.RenderAll();
        }
    }

    private static void OnSelectedNoteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MPEControl control)
        {
            control._selectedNote = e.NewValue as PianoRollNote;
            control.UpdateStatusText();
            control.RenderAll();
        }
    }

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MPEControl control)
        {
            control.RenderAll();
        }
    }

    private static void OnScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MPEControl control)
        {
            control._scrollX = (double)e.NewValue;
            control.ApplyScrollTransform();
        }
    }

    #endregion

    #region UI Event Handlers

    private void ZoneButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender == LowerZoneButton && LowerZoneButton.IsChecked == true)
        {
            _activeZone = MPEZoneType.Lower;
            UpperZoneButton.IsChecked = false;
            GlobalButton.IsChecked = false;
        }
        else if (sender == UpperZoneButton && UpperZoneButton.IsChecked == true)
        {
            _activeZone = MPEZoneType.Upper;
            LowerZoneButton.IsChecked = false;
            GlobalButton.IsChecked = false;
        }
        else if (sender == GlobalButton && GlobalButton.IsChecked == true)
        {
            _activeZone = MPEZoneType.Global;
            LowerZoneButton.IsChecked = false;
            UpperZoneButton.IsChecked = false;
        }

        RaiseZoneConfigurationChanged();
    }

    private void ChannelCountCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ChannelCountCombo.SelectedItem is ComboBoxItem item &&
            int.TryParse(item.Content?.ToString(), out int count))
        {
            _memberChannelCount = count;
            RaiseZoneConfigurationChanged();
        }
    }

    private void PitchBendRangeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _pitchBendRange = (int)e.NewValue;
        PitchBendRangeText.Text = $"{_pitchBendRange} st";
        RaiseZoneConfigurationChanged();
    }

    private void ClearAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedNote == null) return;

        var noteId = _selectedNote.Id;
        _pitchBendData.Remove(noteId);
        _pressureData.Remove(noteId);
        _slideData.Remove(noteId);

        RenderExpressionLanes();
        RaiseExpressionChanged(_selectedNote, MPEExpressionLane.PitchBend, []);
    }

    #endregion

    #region Note Display Canvas Event Handlers

    private void NoteDisplayCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(NoteDisplayCanvas);
        position.X += _scrollX;

        // Find note at position
        var note = FindNoteAtPosition(position);
        if (note != null)
        {
            SelectedNote = note;
        }

        e.Handled = true;
    }

    private void NoteDisplayCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // Not used for note selection
    }

    private void NoteDisplayCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(NoteDisplayCanvas);
        position.X += _scrollX;

        var note = FindNoteAtPosition(position);
        if (note != null)
        {
            ShowNoteTooltip(position, note);
        }
        else
        {
            NoteTooltip.Visibility = Visibility.Collapsed;
        }
    }

    private void NoteDisplayCanvas_MouseLeave(object sender, MouseEventArgs e)
    {
        NoteTooltip.Visibility = Visibility.Collapsed;
    }

    #endregion

    #region Pitch Bend Canvas Event Handlers

    private void PitchBendCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_selectedNote == null) return;

        _activeLane = MPEExpressionLane.PitchBend;
        _isDrawing = true;
        _lastDrawPoint = e.GetPosition(PitchBendCurveCanvas);
        _lastDrawPoint.X += _scrollX;

        if (_currentTool == MPEDrawTool.Line)
        {
            _lineStart = _lastDrawPoint;
        }
        else
        {
            AddPitchBendPoint(_lastDrawPoint);
        }

        PitchBendCurveCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void PitchBendCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(PitchBendCurveCanvas);
        position.X += _scrollX;

        var height = PitchBendCurveCanvas.ActualHeight;
        var value = (int)YToValue(position.Y, height, PitchBendMin, PitchBendMax);
        ShowPitchBendTooltip(e.GetPosition(PitchBendCurveCanvas), value);

        if (_isDrawing && _selectedNote != null && e.LeftButton == MouseButtonState.Pressed)
        {
            if (_currentTool == MPEDrawTool.Draw)
            {
                var distance = Math.Sqrt(
                    Math.Pow(position.X - _lastDrawPoint.X, 2) +
                    Math.Pow(position.Y - _lastDrawPoint.Y, 2));

                if (distance > 5)
                {
                    AddPitchBendPoint(position);
                    _lastDrawPoint = position;
                }
            }
            else if (_currentTool == MPEDrawTool.Erase)
            {
                ErasePitchBendPointsAt(position);
            }
        }
    }

    #endregion

    #region Pressure Canvas Event Handlers

    private void PressureCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_selectedNote == null) return;

        _activeLane = MPEExpressionLane.Pressure;
        _isDrawing = true;
        _lastDrawPoint = e.GetPosition(PressureCurveCanvas);
        _lastDrawPoint.X += _scrollX;

        if (_currentTool == MPEDrawTool.Line)
        {
            _lineStart = _lastDrawPoint;
        }
        else
        {
            AddPressurePoint(_lastDrawPoint);
        }

        PressureCurveCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void PressureCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(PressureCurveCanvas);
        position.X += _scrollX;

        var height = PressureCurveCanvas.ActualHeight;
        var value = (int)YToValue(position.Y, height, PressureMin, PressureMax);
        ShowPressureTooltip(e.GetPosition(PressureCurveCanvas), value);

        if (_isDrawing && _selectedNote != null && e.LeftButton == MouseButtonState.Pressed)
        {
            if (_currentTool == MPEDrawTool.Draw)
            {
                var distance = Math.Sqrt(
                    Math.Pow(position.X - _lastDrawPoint.X, 2) +
                    Math.Pow(position.Y - _lastDrawPoint.Y, 2));

                if (distance > 5)
                {
                    AddPressurePoint(position);
                    _lastDrawPoint = position;
                }
            }
            else if (_currentTool == MPEDrawTool.Erase)
            {
                ErasePressurePointsAt(position);
            }
        }
    }

    #endregion

    #region Slide Canvas Event Handlers

    private void SlideCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_selectedNote == null) return;

        _activeLane = MPEExpressionLane.Slide;
        _isDrawing = true;
        _lastDrawPoint = e.GetPosition(SlideCurveCanvas);
        _lastDrawPoint.X += _scrollX;

        if (_currentTool == MPEDrawTool.Line)
        {
            _lineStart = _lastDrawPoint;
        }
        else
        {
            AddSlidePoint(_lastDrawPoint);
        }

        SlideCurveCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void SlideCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(SlideCurveCanvas);
        position.X += _scrollX;

        var height = SlideCurveCanvas.ActualHeight;
        var value = (int)YToValue(position.Y, height, SlideMin, SlideMax);
        ShowSlideTooltip(e.GetPosition(SlideCurveCanvas), value);

        if (_isDrawing && _selectedNote != null && e.LeftButton == MouseButtonState.Pressed)
        {
            if (_currentTool == MPEDrawTool.Draw)
            {
                var distance = Math.Sqrt(
                    Math.Pow(position.X - _lastDrawPoint.X, 2) +
                    Math.Pow(position.Y - _lastDrawPoint.Y, 2));

                if (distance > 5)
                {
                    AddSlidePoint(position);
                    _lastDrawPoint = position;
                }
            }
            else if (_currentTool == MPEDrawTool.Erase)
            {
                EraseSlidePointsAt(position);
            }
        }
    }

    #endregion

    #region Common Expression Canvas Event Handlers

    private void ExpressionCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDrawing)
        {
            if (_currentTool == MPEDrawTool.Line && _lineStart.HasValue && _selectedNote != null)
            {
                var endPoint = e.GetPosition((Canvas)sender);
                endPoint.X += _scrollX;
                DrawLineToExpression(_lineStart.Value, endPoint);
            }

            _isDrawing = false;
            _lineStart = null;

            if (sender is Canvas canvas)
            {
                canvas.ReleaseMouseCapture();
            }

            SaveExpressionData();
        }

        HideAllTooltips();
    }

    private void ExpressionCanvas_MouseLeave(object sender, MouseEventArgs e)
    {
        HideAllTooltips();
    }

    #endregion

    #region Expression Point Management

    private void AddPitchBendPoint(Point position)
    {
        if (_selectedNote == null) return;

        var noteId = _selectedNote.Id;
        if (!_pitchBendData.ContainsKey(noteId))
        {
            _pitchBendData[noteId] = [];
        }

        var effectiveBeatWidth = PixelsPerBeat * ZoomX;
        var height = PitchBendCurveCanvas.ActualHeight;

        var relativePosition = XToNotePosition(position.X, _selectedNote, effectiveBeatWidth);
        var value = YToValue(position.Y, height, PitchBendMin, PitchBendMax);

        AddOrUpdatePoint(_pitchBendData[noteId], relativePosition, value, PitchBendMin, PitchBendMax);
        RenderPitchBendLane();
    }

    private void AddPressurePoint(Point position)
    {
        if (_selectedNote == null) return;

        var noteId = _selectedNote.Id;
        if (!_pressureData.ContainsKey(noteId))
        {
            _pressureData[noteId] = [];
        }

        var effectiveBeatWidth = PixelsPerBeat * ZoomX;
        var height = PressureCurveCanvas.ActualHeight;

        var relativePosition = XToNotePosition(position.X, _selectedNote, effectiveBeatWidth);
        var value = YToValue(position.Y, height, PressureMin, PressureMax);

        AddOrUpdatePoint(_pressureData[noteId], relativePosition, value, PressureMin, PressureMax);
        RenderPressureLane();
    }

    private void AddSlidePoint(Point position)
    {
        if (_selectedNote == null) return;

        var noteId = _selectedNote.Id;
        if (!_slideData.ContainsKey(noteId))
        {
            _slideData[noteId] = [];
        }

        var effectiveBeatWidth = PixelsPerBeat * ZoomX;
        var height = SlideCurveCanvas.ActualHeight;

        var relativePosition = XToNotePosition(position.X, _selectedNote, effectiveBeatWidth);
        var value = YToValue(position.Y, height, SlideMin, SlideMax);

        AddOrUpdatePoint(_slideData[noteId], relativePosition, value, SlideMin, SlideMax);
        RenderSlideLane();
    }

    private static void AddOrUpdatePoint(List<MPEExpressionPoint> points, double position, double value, int minValue, int maxValue)
    {
        position = Math.Clamp(position, 0, 1);
        value = Math.Clamp(value, minValue, maxValue);

        var existing = points.FirstOrDefault(p => Math.Abs(p.Position - position) < 0.02);
        if (existing != null)
        {
            existing.Value = value;
        }
        else
        {
            points.Add(new MPEExpressionPoint
            {
                Position = position,
                Value = value
            });
        }
    }

    private void ErasePitchBendPointsAt(Point position)
    {
        if (_selectedNote == null || !_pitchBendData.TryGetValue(_selectedNote.Id, out var points))
            return;

        var effectiveBeatWidth = PixelsPerBeat * ZoomX;
        var relativePosition = XToNotePosition(position.X, _selectedNote, effectiveBeatWidth);

        var toRemove = points.Where(p => Math.Abs(p.Position - relativePosition) < 0.05).ToList();
        foreach (var point in toRemove)
        {
            points.Remove(point);
        }

        RenderPitchBendLane();
    }

    private void ErasePressurePointsAt(Point position)
    {
        if (_selectedNote == null || !_pressureData.TryGetValue(_selectedNote.Id, out var points))
            return;

        var effectiveBeatWidth = PixelsPerBeat * ZoomX;
        var relativePosition = XToNotePosition(position.X, _selectedNote, effectiveBeatWidth);

        var toRemove = points.Where(p => Math.Abs(p.Position - relativePosition) < 0.05).ToList();
        foreach (var point in toRemove)
        {
            points.Remove(point);
        }

        RenderPressureLane();
    }

    private void EraseSlidePointsAt(Point position)
    {
        if (_selectedNote == null || !_slideData.TryGetValue(_selectedNote.Id, out var points))
            return;

        var effectiveBeatWidth = PixelsPerBeat * ZoomX;
        var relativePosition = XToNotePosition(position.X, _selectedNote, effectiveBeatWidth);

        var toRemove = points.Where(p => Math.Abs(p.Position - relativePosition) < 0.05).ToList();
        foreach (var point in toRemove)
        {
            points.Remove(point);
        }

        RenderSlideLane();
    }

    private void DrawLineToExpression(Point start, Point end)
    {
        if (_selectedNote == null) return;

        Canvas canvas;
        List<MPEExpressionPoint> points;
        int minValue, maxValue;

        switch (_activeLane)
        {
            case MPEExpressionLane.PitchBend:
                canvas = PitchBendCurveCanvas;
                if (!_pitchBendData.ContainsKey(_selectedNote.Id))
                    _pitchBendData[_selectedNote.Id] = [];
                points = _pitchBendData[_selectedNote.Id];
                minValue = PitchBendMin;
                maxValue = PitchBendMax;
                break;
            case MPEExpressionLane.Pressure:
                canvas = PressureCurveCanvas;
                if (!_pressureData.ContainsKey(_selectedNote.Id))
                    _pressureData[_selectedNote.Id] = [];
                points = _pressureData[_selectedNote.Id];
                minValue = PressureMin;
                maxValue = PressureMax;
                break;
            case MPEExpressionLane.Slide:
                canvas = SlideCurveCanvas;
                if (!_slideData.ContainsKey(_selectedNote.Id))
                    _slideData[_selectedNote.Id] = [];
                points = _slideData[_selectedNote.Id];
                minValue = SlideMin;
                maxValue = SlideMax;
                break;
            default:
                return;
        }

        var effectiveBeatWidth = PixelsPerBeat * ZoomX;
        var height = canvas.ActualHeight;

        var startPos = XToNotePosition(start.X, _selectedNote, effectiveBeatWidth);
        var endPos = XToNotePosition(end.X, _selectedNote, effectiveBeatWidth);
        var startVal = YToValue(start.Y, height, minValue, maxValue);
        var endVal = YToValue(end.Y, height, minValue, maxValue);

        // Interpolate line
        const int numPoints = 10;
        for (int i = 0; i <= numPoints; i++)
        {
            var t = i / (double)numPoints;
            var pos = startPos + t * (endPos - startPos);
            var val = startVal + t * (endVal - startVal);
            AddOrUpdatePoint(points, pos, val, minValue, maxValue);
        }

        RenderExpressionLanes();
    }

    private void SaveExpressionData()
    {
        if (_selectedNote == null) return;

        var noteId = _selectedNote.Id;

        if (_pitchBendData.TryGetValue(noteId, out var pitchBendPoints))
        {
            RaiseExpressionChanged(_selectedNote, MPEExpressionLane.PitchBend, pitchBendPoints);
        }

        if (_pressureData.TryGetValue(noteId, out var pressurePoints))
        {
            RaiseExpressionChanged(_selectedNote, MPEExpressionLane.Pressure, pressurePoints);
        }

        if (_slideData.TryGetValue(noteId, out var slidePoints))
        {
            RaiseExpressionChanged(_selectedNote, MPEExpressionLane.Slide, slidePoints);
        }
    }

    #endregion

    #region Rendering

    private void RenderAll()
    {
        if (!IsLoaded) return;

        RenderNoteDisplay();
        RenderExpressionLanes();
        UpdateCenterLines();
        ApplyScrollTransform();
    }

    private void RenderNoteDisplay()
    {
        NoteGridCanvas.Children.Clear();
        NoteDisplayCanvas.Children.Clear();
        PitchBendCurvesCanvas.Children.Clear();
        SelectionOverlayCanvas.Children.Clear();

        var effectiveBeatWidth = PixelsPerBeat * ZoomX;
        var height = NoteDisplayCanvas.ActualHeight > 0 ? NoteDisplayCanvas.ActualHeight : 100;
        var totalWidth = TotalBeats * effectiveBeatWidth;

        // Set canvas widths
        NoteGridCanvas.Width = totalWidth;
        NoteDisplayCanvas.Width = totalWidth;
        PitchBendCurvesCanvas.Width = totalWidth;

        // Draw grid
        RenderGrid(NoteGridCanvas, totalWidth, height);

        if (Notes == null || Notes.Count == 0) return;

        // Calculate note range for vertical positioning
        var minNote = Notes.Min(n => n.Note);
        var maxNote = Notes.Max(n => n.Note);
        var noteRange = Math.Max(maxNote - minNote + 1, 12);
        var noteHeight = Math.Min(NoteHeight, (height - 20) / noteRange);

        // Draw notes
        foreach (var note in Notes)
        {
            var x = note.StartBeat * effectiveBeatWidth;
            var width = note.Duration * effectiveBeatWidth;
            var y = height - ((note.Note - minNote + 1) * noteHeight) - 10;

            var isSelected = note == _selectedNote;
            var noteColor = isSelected ? SelectedNoteColor : NoteColor;

            // Draw note rectangle
            var noteRect = new Shapes.Rectangle
            {
                Width = Math.Max(width, 2),
                Height = noteHeight - 1,
                Fill = new SolidColorBrush(Color.FromArgb(0x80, noteColor.R, noteColor.G, noteColor.B)),
                Stroke = new SolidColorBrush(noteColor),
                StrokeThickness = isSelected ? 2 : 1,
                RadiusX = 2,
                RadiusY = 2,
                Tag = note,
                Cursor = Cursors.Hand
            };

            Canvas.SetLeft(noteRect, x);
            Canvas.SetTop(noteRect, y);
            NoteDisplayCanvas.Children.Add(noteRect);

            // Draw per-note pitch bend curve on top of note
            if (_pitchBendData.TryGetValue(note.Id, out var pitchBendPoints) && pitchBendPoints.Count > 0)
            {
                RenderNotePitchBendCurve(note, pitchBendPoints, x, y, width, noteHeight);
            }

            // Draw note name
            var noteText = new TextBlock
            {
                Text = PianoRollNote.GetNoteName(note.Note),
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 9,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(noteText, x + 3);
            Canvas.SetTop(noteText, y + (noteHeight - 12) / 2);
            NoteDisplayCanvas.Children.Add(noteText);
        }
    }

    private void RenderNotePitchBendCurve(PianoRollNote note, List<MPEExpressionPoint> points, double noteX, double noteY, double noteWidth, double noteHeight)
    {
        if (points.Count == 0) return;

        var sortedPoints = points.OrderBy(p => p.Position).ToList();

        // Draw pitch bend curve as an overlay on the note
        var pathFigure = new PathFigure();
        var firstPoint = sortedPoints[0];

        // Map pitch bend to vertical offset from note center
        var centerY = noteY + noteHeight / 2;
        var maxOffset = noteHeight / 2;

        var firstX = noteX + firstPoint.Position * noteWidth;
        var firstYOffset = (firstPoint.Value / (double)PitchBendMax) * maxOffset;
        pathFigure.StartPoint = new Point(firstX, centerY - firstYOffset);

        foreach (var point in sortedPoints.Skip(1))
        {
            var px = noteX + point.Position * noteWidth;
            var pyOffset = (point.Value / (double)PitchBendMax) * maxOffset;
            pathFigure.Segments.Add(new LineSegment(new Point(px, centerY - pyOffset), true));
        }

        var pathGeometry = new PathGeometry { Figures = { pathFigure } };
        var path = new Shapes.Path
        {
            Stroke = new SolidColorBrush(PitchBendColor),
            StrokeThickness = 1.5,
            Data = pathGeometry,
            IsHitTestVisible = false,
            Opacity = 0.8
        };

        PitchBendCurvesCanvas.Children.Add(path);
    }

    private void RenderExpressionLanes()
    {
        RenderPitchBendLane();
        RenderPressureLane();
        RenderSlideLane();
    }

    private void RenderPitchBendLane()
    {
        PitchBendGridCanvas.Children.Clear();
        PitchBendCurveCanvas.Children.Clear();

        var effectiveBeatWidth = PixelsPerBeat * ZoomX;
        var totalWidth = TotalBeats * effectiveBeatWidth;
        var height = PitchBendCurveCanvas.ActualHeight > 0 ? PitchBendCurveCanvas.ActualHeight : 80;

        PitchBendGridCanvas.Width = totalWidth;
        PitchBendCurveCanvas.Width = totalWidth;

        // Draw grid
        RenderGrid(PitchBendGridCanvas, totalWidth, height);

        // Draw selected note background
        if (_selectedNote != null)
        {
            var noteX = _selectedNote.StartBeat * effectiveBeatWidth;
            var noteWidth = _selectedNote.Duration * effectiveBeatWidth;

            var noteBackground = new Shapes.Rectangle
            {
                Width = noteWidth,
                Height = height,
                Fill = new SolidColorBrush(Color.FromArgb(0x20, PitchBendColor.R, PitchBendColor.G, PitchBendColor.B))
            };
            Canvas.SetLeft(noteBackground, noteX);
            PitchBendGridCanvas.Children.Add(noteBackground);

            // Draw pitch bend curve
            if (_pitchBendData.TryGetValue(_selectedNote.Id, out var points) && points.Count > 0)
            {
                RenderExpressionCurve(PitchBendCurveCanvas, points, PitchBendColor, height, PitchBendMin, PitchBendMax,
                    _selectedNote.StartBeat, _selectedNote.Duration, effectiveBeatWidth);
            }
        }
    }

    private void RenderPressureLane()
    {
        PressureGridCanvas.Children.Clear();
        PressureCurveCanvas.Children.Clear();

        var effectiveBeatWidth = PixelsPerBeat * ZoomX;
        var totalWidth = TotalBeats * effectiveBeatWidth;
        var height = PressureCurveCanvas.ActualHeight > 0 ? PressureCurveCanvas.ActualHeight : 60;

        PressureGridCanvas.Width = totalWidth;
        PressureCurveCanvas.Width = totalWidth;

        // Draw grid
        RenderGrid(PressureGridCanvas, totalWidth, height);

        // Draw selected note background and curve
        if (_selectedNote != null)
        {
            var noteX = _selectedNote.StartBeat * effectiveBeatWidth;
            var noteWidth = _selectedNote.Duration * effectiveBeatWidth;

            var noteBackground = new Shapes.Rectangle
            {
                Width = noteWidth,
                Height = height,
                Fill = new SolidColorBrush(Color.FromArgb(0x20, PressureColor.R, PressureColor.G, PressureColor.B))
            };
            Canvas.SetLeft(noteBackground, noteX);
            PressureGridCanvas.Children.Add(noteBackground);

            // Draw pressure as color intensity bars
            if (_pressureData.TryGetValue(_selectedNote.Id, out var points) && points.Count > 0)
            {
                RenderPressureIntensity(PressureCurveCanvas, points, height,
                    _selectedNote.StartBeat, _selectedNote.Duration, effectiveBeatWidth);
            }
        }
    }

    private void RenderSlideLane()
    {
        SlideGridCanvas.Children.Clear();
        SlideCurveCanvas.Children.Clear();

        var effectiveBeatWidth = PixelsPerBeat * ZoomX;
        var totalWidth = TotalBeats * effectiveBeatWidth;
        var height = SlideCurveCanvas.ActualHeight > 0 ? SlideCurveCanvas.ActualHeight : 60;

        SlideGridCanvas.Width = totalWidth;
        SlideCurveCanvas.Width = totalWidth;

        // Draw grid
        RenderGrid(SlideGridCanvas, totalWidth, height);

        // Draw selected note background and curve
        if (_selectedNote != null)
        {
            var noteX = _selectedNote.StartBeat * effectiveBeatWidth;
            var noteWidth = _selectedNote.Duration * effectiveBeatWidth;

            var noteBackground = new Shapes.Rectangle
            {
                Width = noteWidth,
                Height = height,
                Fill = new SolidColorBrush(Color.FromArgb(0x20, SlideColor.R, SlideColor.G, SlideColor.B))
            };
            Canvas.SetLeft(noteBackground, noteX);
            SlideGridCanvas.Children.Add(noteBackground);

            // Draw slide curve
            if (_slideData.TryGetValue(_selectedNote.Id, out var points) && points.Count > 0)
            {
                RenderExpressionCurve(SlideCurveCanvas, points, SlideColor, height, SlideMin, SlideMax,
                    _selectedNote.StartBeat, _selectedNote.Duration, effectiveBeatWidth);
            }
        }
    }

    private void RenderGrid(Canvas canvas, double totalWidth, double height)
    {
        var effectiveBeatWidth = PixelsPerBeat * ZoomX;
        var gridLineColor = Color.FromRgb(0x25, 0x25, 0x25);
        var barLineColor = Color.FromRgb(0x3A, 0x3A, 0x3A);

        // Vertical beat lines
        var beatsToShow = (int)Math.Ceiling(TotalBeats);
        for (int beat = 0; beat <= beatsToShow; beat++)
        {
            var x = beat * effectiveBeatWidth;
            var isBarLine = beat % BeatsPerBar == 0;

            var line = new Shapes.Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = height,
                Stroke = new SolidColorBrush(isBarLine ? barLineColor : gridLineColor),
                StrokeThickness = isBarLine ? 1 : 0.5
            };
            canvas.Children.Add(line);
        }

        // Horizontal reference lines
        foreach (var fraction in new[] { 0.25, 0.5, 0.75 })
        {
            var y = height * fraction;
            var line = new Shapes.Line
            {
                X1 = 0,
                Y1 = y,
                X2 = totalWidth,
                Y2 = y,
                Stroke = new SolidColorBrush(gridLineColor),
                StrokeThickness = 0.5
            };
            canvas.Children.Add(line);
        }
    }

    private void RenderExpressionCurve(Canvas canvas, List<MPEExpressionPoint> points, Color color,
        double height, int minValue, int maxValue, double noteStartBeat, double noteDuration, double effectiveBeatWidth)
    {
        if (points.Count == 0) return;

        var sortedPoints = points.OrderBy(p => p.Position).ToList();

        // Create fill area
        var fillFigure = new PathFigure();
        var startX = noteStartBeat * effectiveBeatWidth + sortedPoints[0].Position * noteDuration * effectiveBeatWidth;
        fillFigure.StartPoint = new Point(startX, height);

        // First point
        var firstY = ValueToY(sortedPoints[0].Value, height, minValue, maxValue);
        fillFigure.Segments.Add(new LineSegment(new Point(startX, firstY), true));

        // Curve
        var lineFigure = new PathFigure { StartPoint = new Point(startX, firstY) };

        foreach (var point in sortedPoints.Skip(1))
        {
            var x = noteStartBeat * effectiveBeatWidth + point.Position * noteDuration * effectiveBeatWidth;
            var y = ValueToY(point.Value, height, minValue, maxValue);
            lineFigure.Segments.Add(new LineSegment(new Point(x, y), true));
            fillFigure.Segments.Add(new LineSegment(new Point(x, y), true));
        }

        // Close fill
        var lastX = noteStartBeat * effectiveBeatWidth + sortedPoints.Last().Position * noteDuration * effectiveBeatWidth;
        fillFigure.Segments.Add(new LineSegment(new Point(lastX, height), true));
        fillFigure.IsClosed = true;

        // Draw fill
        var fillGeometry = new PathGeometry { Figures = { fillFigure } };
        var fillPath = new Shapes.Path
        {
            Data = fillGeometry,
            Fill = new SolidColorBrush(Color.FromArgb(0x40, color.R, color.G, color.B))
        };
        canvas.Children.Add(fillPath);

        // Draw curve
        var lineGeometry = new PathGeometry { Figures = { lineFigure } };
        var linePath = new Shapes.Path
        {
            Data = lineGeometry,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 2
        };
        canvas.Children.Add(linePath);

        // Draw handles
        foreach (var point in sortedPoints)
        {
            var x = noteStartBeat * effectiveBeatWidth + point.Position * noteDuration * effectiveBeatWidth;
            var y = ValueToY(point.Value, height, minValue, maxValue);

            var handle = new Shapes.Ellipse
            {
                Width = HandleRadius * 2,
                Height = HandleRadius * 2,
                Fill = new SolidColorBrush(color),
                Stroke = Brushes.White,
                StrokeThickness = 1,
                Cursor = Cursors.Hand
            };
            Canvas.SetLeft(handle, x - HandleRadius);
            Canvas.SetTop(handle, y - HandleRadius);
            canvas.Children.Add(handle);
        }
    }

    private void RenderPressureIntensity(Canvas canvas, List<MPEExpressionPoint> points, double height,
        double noteStartBeat, double noteDuration, double effectiveBeatWidth)
    {
        if (points.Count == 0) return;

        var sortedPoints = points.OrderBy(p => p.Position).ToList();
        var barWidth = Math.Max(2, noteDuration * effectiveBeatWidth / Math.Max(points.Count, 1) / 2);

        foreach (var point in sortedPoints)
        {
            var x = noteStartBeat * effectiveBeatWidth + point.Position * noteDuration * effectiveBeatWidth;
            var normalizedValue = point.Value / (double)PressureMax;
            var barHeight = normalizedValue * height;

            // Color intensity based on pressure value
            var alpha = (byte)(normalizedValue * 255);
            var barColor = Color.FromArgb(alpha, PressureColor.R, PressureColor.G, PressureColor.B);

            var bar = new Shapes.Rectangle
            {
                Width = barWidth,
                Height = barHeight,
                Fill = new SolidColorBrush(barColor),
                RadiusX = 1,
                RadiusY = 1
            };
            Canvas.SetLeft(bar, x - barWidth / 2);
            Canvas.SetTop(bar, height - barHeight);
            canvas.Children.Add(bar);
        }
    }

    private void UpdateCenterLines()
    {
        if (PitchBendCurveCanvas.ActualWidth > 0)
        {
            PitchBendCenterLine.X2 = PitchBendCurveCanvas.ActualWidth;
            PitchBendCenterLine.Y1 = PitchBendCurveCanvas.ActualHeight / 2;
            PitchBendCenterLine.Y2 = PitchBendCurveCanvas.ActualHeight / 2;
        }
    }

    private void ApplyScrollTransform()
    {
        var transform = new TranslateTransform(-_scrollX, 0);

        NoteGridCanvas.RenderTransform = transform;
        NoteDisplayCanvas.RenderTransform = transform;
        PitchBendCurvesCanvas.RenderTransform = transform;
        SelectionOverlayCanvas.RenderTransform = transform;

        PitchBendGridCanvas.RenderTransform = transform;
        PitchBendCurveCanvas.RenderTransform = transform;

        PressureGridCanvas.RenderTransform = transform;
        PressureCurveCanvas.RenderTransform = transform;

        SlideGridCanvas.RenderTransform = transform;
        SlideCurveCanvas.RenderTransform = transform;
    }

    #endregion

    #region Helper Methods

    private PianoRollNote? FindNoteAtPosition(Point position)
    {
        if (Notes == null || Notes.Count == 0) return null;

        var effectiveBeatWidth = PixelsPerBeat * ZoomX;
        var height = NoteDisplayCanvas.ActualHeight > 0 ? NoteDisplayCanvas.ActualHeight : 100;

        var minNote = Notes.Min(n => n.Note);
        var maxNote = Notes.Max(n => n.Note);
        var noteRange = Math.Max(maxNote - minNote + 1, 12);
        var noteHeight = Math.Min(NoteHeight, (height - 20) / noteRange);

        foreach (var note in Notes)
        {
            var x = note.StartBeat * effectiveBeatWidth;
            var width = note.Duration * effectiveBeatWidth;
            var y = height - ((note.Note - minNote + 1) * noteHeight) - 10;

            if (position.X >= x && position.X <= x + width &&
                position.Y >= y && position.Y <= y + noteHeight)
            {
                return note;
            }
        }

        return null;
    }

    private double XToNotePosition(double x, PianoRollNote note, double effectiveBeatWidth)
    {
        var noteStartX = note.StartBeat * effectiveBeatWidth;
        var noteWidth = note.Duration * effectiveBeatWidth;
        var relativeX = x - noteStartX;
        return Math.Clamp(relativeX / noteWidth, 0, 1);
    }

    private static double ValueToY(double value, double height, int minValue, int maxValue)
    {
        var normalized = (value - minValue) / (maxValue - minValue);
        return height * (1 - normalized);
    }

    private static double YToValue(double y, double height, int minValue, int maxValue)
    {
        var normalized = 1 - (y / height);
        return minValue + normalized * (maxValue - minValue);
    }

    private void UpdateStatusText()
    {
        if (_selectedNote != null)
        {
            StatusText.Text = $"Editing: {PianoRollNote.GetNoteName(_selectedNote.Note)} " +
                              $"({_selectedNote.StartBeat:F2} - {_selectedNote.GetEndBeat():F2})";
        }
        else
        {
            StatusText.Text = "Select a note to edit MPE data";
        }
    }

    #endregion

    #region Tooltip Methods

    private void ShowNoteTooltip(Point position, PianoRollNote note)
    {
        NoteTooltipText.Text = $"{PianoRollNote.GetNoteName(note.Note)} | " +
                               $"Beat: {note.StartBeat:F2} | Vel: {note.Velocity}";
        NoteTooltip.Margin = new Thickness(position.X - _scrollX + 70, position.Y + 10, 0, 0);
        NoteTooltip.Visibility = Visibility.Visible;
    }

    private void ShowPitchBendTooltip(Point position, int value)
    {
        PitchBendTooltipText.Text = $"{value:+0;-0;0}";
        PitchBendTooltip.Margin = new Thickness(position.X + 70, position.Y - 25, 0, 0);
        PitchBendTooltip.Visibility = Visibility.Visible;
    }

    private void ShowPressureTooltip(Point position, int value)
    {
        PressureTooltipText.Text = $"{value}";
        PressureTooltip.Margin = new Thickness(position.X + 70, position.Y - 25, 0, 0);
        PressureTooltip.Visibility = Visibility.Visible;
    }

    private void ShowSlideTooltip(Point position, int value)
    {
        SlideTooltipText.Text = $"{value}";
        SlideTooltip.Margin = new Thickness(position.X + 70, position.Y - 25, 0, 0);
        SlideTooltip.Visibility = Visibility.Visible;
    }

    private void HideAllTooltips()
    {
        NoteTooltip.Visibility = Visibility.Collapsed;
        PitchBendTooltip.Visibility = Visibility.Collapsed;
        PressureTooltip.Visibility = Visibility.Collapsed;
        SlideTooltip.Visibility = Visibility.Collapsed;
    }

    #endregion

    #region Event Raisers

    private void RaiseExpressionChanged(PianoRollNote note, MPEExpressionLane lane, List<MPEExpressionPoint> points)
    {
        ExpressionChanged?.Invoke(this, new MPEExpressionChangedEventArgs(note, lane, points.ToList()));
    }

    private void RaiseZoneConfigurationChanged()
    {
        ZoneConfigurationChanged?.Invoke(this, new MPEZoneChangedEventArgs(_activeZone, _memberChannelCount, _pitchBendRange));
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Refreshes the control display.
    /// </summary>
    public void Refresh()
    {
        RenderAll();
    }

    /// <summary>
    /// Sets the pitch bend data for a note.
    /// </summary>
    public void SetPitchBendData(Guid noteId, List<MPEExpressionPoint> points)
    {
        _pitchBendData[noteId] = points.ToList();
        RenderAll();
    }

    /// <summary>
    /// Sets the pressure data for a note.
    /// </summary>
    public void SetPressureData(Guid noteId, List<MPEExpressionPoint> points)
    {
        _pressureData[noteId] = points.ToList();
        RenderAll();
    }

    /// <summary>
    /// Sets the slide data for a note.
    /// </summary>
    public void SetSlideData(Guid noteId, List<MPEExpressionPoint> points)
    {
        _slideData[noteId] = points.ToList();
        RenderAll();
    }

    /// <summary>
    /// Gets the current MPE zone configuration.
    /// </summary>
    public (MPEZoneType Zone, int ChannelCount, int PitchBendRange) GetZoneConfiguration()
    {
        return (_activeZone, _memberChannelCount, _pitchBendRange);
    }

    #endregion
}

#region Enums

/// <summary>
/// MPE Zone type enumeration.
/// </summary>
public enum MPEZoneType
{
    /// <summary>
    /// Lower Zone (Master Channel 1, Member Channels 2-15).
    /// </summary>
    Lower,

    /// <summary>
    /// Upper Zone (Master Channel 16, Member Channels 1-15).
    /// </summary>
    Upper,

    /// <summary>
    /// Global (All 16 channels).
    /// </summary>
    Global
}

/// <summary>
/// MPE expression lane type.
/// </summary>
public enum MPEExpressionLane
{
    /// <summary>
    /// Pitch Bend (-8192 to +8191).
    /// </summary>
    PitchBend,

    /// <summary>
    /// Pressure/Aftertouch (0-127).
    /// </summary>
    Pressure,

    /// <summary>
    /// Slide/Timbre CC74 (0-127).
    /// </summary>
    Slide
}

/// <summary>
/// MPE drawing tool enumeration.
/// </summary>
public enum MPEDrawTool
{
    /// <summary>
    /// Free draw mode.
    /// </summary>
    Draw,

    /// <summary>
    /// Line drawing mode.
    /// </summary>
    Line,

    /// <summary>
    /// Erase mode.
    /// </summary>
    Erase
}

#endregion

#region Data Classes

/// <summary>
/// Represents a point on an MPE expression curve.
/// </summary>
public class MPEExpressionPoint
{
    /// <summary>
    /// Unique identifier.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Position within the note (0.0 = start, 1.0 = end).
    /// </summary>
    public double Position { get; set; }

    /// <summary>
    /// Expression value.
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    /// Indicates whether this point is selected.
    /// </summary>
    public bool IsSelected { get; set; }
}

/// <summary>
/// Event arguments for MPE expression changed events.
/// </summary>
public class MPEExpressionChangedEventArgs : EventArgs
{
    /// <summary>
    /// The note whose expression was changed.
    /// </summary>
    public PianoRollNote Note { get; }

    /// <summary>
    /// The expression lane that changed.
    /// </summary>
    public MPEExpressionLane Lane { get; }

    /// <summary>
    /// The new expression points.
    /// </summary>
    public List<MPEExpressionPoint> Points { get; }

    public MPEExpressionChangedEventArgs(PianoRollNote note, MPEExpressionLane lane, List<MPEExpressionPoint> points)
    {
        Note = note;
        Lane = lane;
        Points = points;
    }
}

/// <summary>
/// Event arguments for MPE zone configuration changed events.
/// </summary>
public class MPEZoneChangedEventArgs : EventArgs
{
    /// <summary>
    /// The active zone type.
    /// </summary>
    public MPEZoneType Zone { get; }

    /// <summary>
    /// Number of member channels.
    /// </summary>
    public int MemberChannelCount { get; }

    /// <summary>
    /// Pitch bend range in semitones.
    /// </summary>
    public int PitchBendRange { get; }

    public MPEZoneChangedEventArgs(MPEZoneType zone, int memberChannelCount, int pitchBendRange)
    {
        Zone = zone;
        MemberChannelCount = memberChannelCount;
        PitchBendRange = pitchBendRange;
    }
}

#endregion

#region Converters

/// <summary>
/// Converts pressure value (0-127) to color intensity.
/// </summary>
public class MPEPressureToColorConverter : IValueConverter
{
    private static readonly Color PressureColor = Color.FromRgb(0xFF, 0x98, 0x00);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intValue)
        {
            var alpha = (byte)(intValue * 255 / 127);
            return new SolidColorBrush(Color.FromArgb(alpha, PressureColor.R, PressureColor.G, PressureColor.B));
        }
        return Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts pitch bend value to visual height offset.
/// </summary>
public class MPEPitchBendToHeightConverter : IValueConverter
{
    private const int PitchBendMax = 8191;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intValue && parameter is double maxHeight)
        {
            var normalized = intValue / (double)PitchBendMax;
            return normalized * maxHeight / 2;
        }
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

#endregion
