using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Control for visual stretch point editing on waveform.
/// Allows adding, removing, and dragging stretch markers to time-stretch audio.
/// </summary>
public partial class StretchMarkersControl : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty AudioDataProperty =
        DependencyProperty.Register(nameof(AudioData), typeof(float[]), typeof(StretchMarkersControl),
            new PropertyMetadata(null, OnAudioDataChanged));

    public static readonly DependencyProperty SampleRateProperty =
        DependencyProperty.Register(nameof(SampleRate), typeof(int), typeof(StretchMarkersControl),
            new PropertyMetadata(44100, OnDisplayPropertyChanged));

    public static readonly DependencyProperty BpmProperty =
        DependencyProperty.Register(nameof(Bpm), typeof(double), typeof(StretchMarkersControl),
            new PropertyMetadata(120.0, OnDisplayPropertyChanged));

    public static readonly DependencyProperty PixelsPerSecondProperty =
        DependencyProperty.Register(nameof(PixelsPerSecond), typeof(double), typeof(StretchMarkersControl),
            new PropertyMetadata(100.0, OnDisplayPropertyChanged));

    public static readonly DependencyProperty IsStretchEnabledProperty =
        DependencyProperty.Register(nameof(IsStretchEnabled), typeof(bool), typeof(StretchMarkersControl),
            new PropertyMetadata(false, OnIsStretchEnabledChanged));

    public static readonly DependencyProperty MarkersProperty =
        DependencyProperty.Register(nameof(Markers), typeof(ObservableCollection<StretchMarker>), typeof(StretchMarkersControl),
            new PropertyMetadata(null, OnMarkersChanged));

    #endregion

    #region Properties

    public float[]? AudioData
    {
        get => (float[]?)GetValue(AudioDataProperty);
        set => SetValue(AudioDataProperty, value);
    }

    public int SampleRate
    {
        get => (int)GetValue(SampleRateProperty);
        set => SetValue(SampleRateProperty, value);
    }

    public double Bpm
    {
        get => (double)GetValue(BpmProperty);
        set => SetValue(BpmProperty, value);
    }

    public double PixelsPerSecond
    {
        get => (double)GetValue(PixelsPerSecondProperty);
        set => SetValue(PixelsPerSecondProperty, value);
    }

    public bool IsStretchEnabled
    {
        get => (bool)GetValue(IsStretchEnabledProperty);
        set => SetValue(IsStretchEnabledProperty, value);
    }

    public ObservableCollection<StretchMarker>? Markers
    {
        get => (ObservableCollection<StretchMarker>?)GetValue(MarkersProperty);
        set => SetValue(MarkersProperty, value);
    }

    #endregion

    #region Events

    public event EventHandler<StretchMarker>? MarkerAdded;
    public event EventHandler<StretchMarker>? MarkerRemoved;
    public event EventHandler<StretchMarker>? MarkerMoved;
    public event EventHandler? PreviewRequested;
    public event EventHandler? MarkersReset;

    #endregion

    #region Fields

    private readonly List<MarkerVisual> _markerVisuals = new();
    private MarkerVisual? _selectedMarker;
    private MarkerVisual? _hoveredMarker;
    private bool _isDragging;
    private Point _dragStartPoint;
    private double _dragStartTime;
    private double _gridSize = 0.125; // 1/32 note at default BPM
    private bool _isAddingMarker;
    private Point _contextMenuPosition;

    #endregion

    public StretchMarkersControl()
    {
        InitializeComponent();

        Markers = new ObservableCollection<StretchMarker>();
        SizeChanged += OnSizeChanged;
    }

    #region Property Changed Callbacks

    private static void OnAudioDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StretchMarkersControl control)
        {
            control.UpdateWaveform();
            control.UpdateDisplay();
        }
    }

    private static void OnDisplayPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StretchMarkersControl control)
        {
            control.UpdateDisplay();
        }
    }

    private static void OnIsStretchEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StretchMarkersControl control)
        {
            control.StretchEnabledToggle.IsChecked = (bool)e.NewValue;
            control.UpdateStatus();
        }
    }

    private static void OnMarkersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StretchMarkersControl control)
        {
            if (e.OldValue is ObservableCollection<StretchMarker> oldCollection)
            {
                oldCollection.CollectionChanged -= control.OnMarkersCollectionChanged;
            }

            if (e.NewValue is ObservableCollection<StretchMarker> newCollection)
            {
                newCollection.CollectionChanged += control.OnMarkersCollectionChanged;
            }

            control.RebuildMarkerVisuals();
        }
    }

    private void OnMarkersCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        RebuildMarkerVisuals();
    }

    #endregion

    #region Event Handlers

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateDisplay();
    }

    private void StretchEnabledToggle_Changed(object sender, RoutedEventArgs e)
    {
        IsStretchEnabled = StretchEnabledToggle.IsChecked == true;
    }

    private void AddMarkerBtn_Click(object sender, RoutedEventArgs e)
    {
        _isAddingMarker = true;
        StatusText.Text = "Click on waveform to add stretch marker...";
        Cursor = Cursors.Cross;
    }

    private void RemoveMarkerBtn_Click(object sender, RoutedEventArgs e)
    {
        RemoveSelectedMarker();
    }

    private void ResetAllBtn_Click(object sender, RoutedEventArgs e)
    {
        ResetAllMarkers();
    }

    private void PreviewBtn_Click(object sender, RoutedEventArgs e)
    {
        PreviewRequested?.Invoke(this, EventArgs.Empty);
    }

    private void GridSizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridSizeCombo.SelectedItem is ComboBoxItem item && item.Tag is string tagStr)
        {
            if (double.TryParse(tagStr, out var beats))
            {
                _gridSize = (60.0 / Bpm) * beats;
                UpdateGridLines();
            }
        }
    }

    private void MarkersCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(MarkersCanvas);

        if (_isAddingMarker)
        {
            AddMarkerAtPosition(pos.X);
            _isAddingMarker = false;
            Cursor = Cursors.Arrow;
            UpdateStatus();
            return;
        }

        // Check if clicking on a marker
        var clickedMarker = FindMarkerAtPosition(pos.X);
        if (clickedMarker != null)
        {
            SelectMarker(clickedMarker);
            _isDragging = true;
            _dragStartPoint = pos;
            _dragStartTime = clickedMarker.Marker.TargetTime;
            MarkersCanvas.CaptureMouse();
        }
        else
        {
            SelectMarker(null);
        }

        e.Handled = true;
    }

    private void MarkersCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(MarkersCanvas);

        if (_isDragging && _selectedMarker != null)
        {
            var deltaX = pos.X - _dragStartPoint.X;
            var deltaTime = deltaX / PixelsPerSecond;
            var newTime = _dragStartTime + deltaTime;

            // Snap to grid if enabled
            if (SnapToGridToggle.IsChecked == true && _gridSize > 0)
            {
                newTime = Math.Round(newTime / _gridSize) * _gridSize;
            }

            // Clamp to valid range
            newTime = Math.Max(0, Math.Min(newTime, GetAudioDuration()));

            _selectedMarker.Marker.TargetTime = newTime;
            UpdateMarkerVisual(_selectedMarker);
            UpdateStretchRatioDisplay();
            UpdateStretchRegions();

            MarkerMoved?.Invoke(this, _selectedMarker.Marker);
        }
        else
        {
            // Hover detection
            var hovered = FindMarkerAtPosition(pos.X);
            if (hovered != _hoveredMarker)
            {
                if (_hoveredMarker != null)
                {
                    _hoveredMarker.SetHovered(false);
                }
                _hoveredMarker = hovered;
                if (_hoveredMarker != null)
                {
                    _hoveredMarker.SetHovered(true);
                }
            }
        }
    }

    private void MarkersCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        MarkersCanvas.ReleaseMouseCapture();
    }

    private void MarkersCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _contextMenuPosition = e.GetPosition(MarkersCanvas);
    }

    private void AddMarkerHereMenuItem_Click(object sender, RoutedEventArgs e)
    {
        AddMarkerAtPosition(_contextMenuPosition.X);
    }

    private void RemoveMarkerMenuItem_Click(object sender, RoutedEventArgs e)
    {
        RemoveSelectedMarker();
    }

    private void ResetMarkerMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMarker != null)
        {
            _selectedMarker.Marker.TargetTime = _selectedMarker.Marker.OriginalTime;
            UpdateMarkerVisual(_selectedMarker);
            UpdateStretchRatioDisplay();
            UpdateStretchRegions();
        }
    }

    private void ResetAllMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ResetAllMarkers();
    }

    private void SnapMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SnapToGridToggle.IsChecked = SnapMenuItem.IsChecked;
    }

    #endregion

    #region Public Methods

    public void AddMarker(double originalTime)
    {
        var marker = new StretchMarker
        {
            Id = Guid.NewGuid().ToString(),
            OriginalTime = originalTime,
            TargetTime = originalTime
        };

        Markers?.Add(marker);
        MarkerAdded?.Invoke(this, marker);
        UpdateMarkerInfo();
    }

    public void RemoveMarker(StretchMarker marker)
    {
        Markers?.Remove(marker);
        MarkerRemoved?.Invoke(this, marker);
        UpdateMarkerInfo();
    }

    public void ResetAllMarkers()
    {
        if (Markers == null) return;

        foreach (var marker in Markers)
        {
            marker.TargetTime = marker.OriginalTime;
        }

        RebuildMarkerVisuals();
        UpdateStretchRegions();
        MarkersReset?.Invoke(this, EventArgs.Empty);
    }

    public double GetTotalStretchRatio()
    {
        if (Markers == null || Markers.Count < 2) return 1.0;

        var sorted = Markers.OrderBy(m => m.OriginalTime).ToList();
        var originalDuration = sorted.Last().OriginalTime - sorted.First().OriginalTime;
        var targetDuration = sorted.Last().TargetTime - sorted.First().TargetTime;

        if (originalDuration <= 0) return 1.0;
        return targetDuration / originalDuration;
    }

    #endregion

    #region Private Methods

    private void AddMarkerAtPosition(double x)
    {
        var time = x / PixelsPerSecond;

        // Snap to grid if enabled
        if (SnapToGridToggle.IsChecked == true && _gridSize > 0)
        {
            time = Math.Round(time / _gridSize) * _gridSize;
        }

        time = Math.Max(0, Math.Min(time, GetAudioDuration()));
        AddMarker(time);
    }

    private void RemoveSelectedMarker()
    {
        if (_selectedMarker != null)
        {
            RemoveMarker(_selectedMarker.Marker);
            SelectMarker(null);
        }
    }

    private MarkerVisual? FindMarkerAtPosition(double x)
    {
        const double hitRadius = 8;
        return _markerVisuals.FirstOrDefault(mv =>
            Math.Abs(mv.Marker.TargetTime * PixelsPerSecond - x) < hitRadius);
    }

    private void SelectMarker(MarkerVisual? marker)
    {
        if (_selectedMarker != null)
        {
            _selectedMarker.SetSelected(false);
        }

        _selectedMarker = marker;

        if (_selectedMarker != null)
        {
            _selectedMarker.SetSelected(true);
        }

        RemoveMarkerBtn.IsEnabled = _selectedMarker != null;
        RemoveMarkerMenuItem.IsEnabled = _selectedMarker != null;
        ResetMarkerMenuItem.IsEnabled = _selectedMarker != null;

        UpdateStretchRatioDisplay();
    }

    private void RebuildMarkerVisuals()
    {
        MarkersCanvas.Children.Clear();
        _markerVisuals.Clear();
        _selectedMarker = null;
        _hoveredMarker = null;

        if (Markers == null) return;

        foreach (var marker in Markers)
        {
            var visual = new MarkerVisual(marker);
            _markerVisuals.Add(visual);
            MarkersCanvas.Children.Add(visual.Line);
            MarkersCanvas.Children.Add(visual.Handle);
            MarkersCanvas.Children.Add(visual.Label);
            UpdateMarkerVisual(visual);
        }

        UpdateMarkerInfo();
        UpdateStretchRegions();
    }

    private void UpdateMarkerVisual(MarkerVisual visual)
    {
        var x = visual.Marker.TargetTime * PixelsPerSecond;
        var height = MarkersCanvas.ActualHeight;

        Canvas.SetLeft(visual.Line, x);
        Canvas.SetTop(visual.Line, 0);
        visual.Line.Y2 = height;

        Canvas.SetLeft(visual.Handle, x - 6);
        Canvas.SetTop(visual.Handle, 0);

        Canvas.SetLeft(visual.Label, x + 4);
        Canvas.SetTop(visual.Label, height - 20);

        var ratio = visual.Marker.OriginalTime > 0
            ? visual.Marker.TargetTime / visual.Marker.OriginalTime
            : 1.0;
        visual.Label.Text = $"{ratio:F2}x";
    }

    private void UpdateDisplay()
    {
        UpdateTimeRuler();
        UpdateGridLines();
        UpdateWaveform();
        foreach (var visual in _markerVisuals)
        {
            UpdateMarkerVisual(visual);
        }
        UpdateStretchRegions();
        UpdateDurationDisplay();
    }

    private void UpdateTimeRuler()
    {
        TimeRuler.Children.Clear();
        var width = TimeRuler.ActualWidth;
        var duration = GetAudioDuration();

        if (duration <= 0 || width <= 0) return;

        var interval = 1.0; // 1 second intervals
        if (PixelsPerSecond < 50) interval = 5.0;
        if (PixelsPerSecond < 20) interval = 10.0;

        for (double t = 0; t <= duration; t += interval)
        {
            var x = t * PixelsPerSecond;
            if (x > width) break;

            var line = new Line
            {
                X1 = x,
                X2 = x,
                Y1 = 16,
                Y2 = 24,
                Stroke = Brushes.White,
                StrokeThickness = 1
            };
            TimeRuler.Children.Add(line);

            var label = new TextBlock
            {
                Text = FormatTime(t),
                FontSize = 10,
                Foreground = Brushes.White
            };
            Canvas.SetLeft(label, x + 2);
            Canvas.SetTop(label, 2);
            TimeRuler.Children.Add(label);
        }
    }

    private void UpdateGridLines()
    {
        GridCanvas.Children.Clear();
        var width = GridCanvas.ActualWidth;
        var height = GridCanvas.ActualHeight;
        var duration = GetAudioDuration();

        if (duration <= 0 || _gridSize <= 0) return;

        var gridBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));

        for (double t = 0; t <= duration; t += _gridSize)
        {
            var x = t * PixelsPerSecond;
            if (x > width) break;

            var line = new Line
            {
                X1 = x,
                X2 = x,
                Y1 = 0,
                Y2 = height,
                Stroke = gridBrush,
                StrokeThickness = 0.5
            };
            GridCanvas.Children.Add(line);
        }
    }

    private void UpdateWaveform()
    {
        if (AudioData == null || AudioData.Length == 0)
        {
            WaveformPath.Data = null;
            return;
        }

        var width = WaveformCanvas.ActualWidth;
        var height = WaveformCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        var duration = GetAudioDuration();
        var samplesPerPixel = (int)Math.Max(1, AudioData.Length / (duration * PixelsPerSecond));
        var geometry = new StreamGeometry();

        using (var ctx = geometry.Open())
        {
            var centerY = height / 2;
            var halfHeight = height / 2 * 0.9;
            var pixelCount = (int)Math.Min(width, duration * PixelsPerSecond);

            for (var x = 0; x < pixelCount; x++)
            {
                var sampleIndex = (int)(x * samplesPerPixel);
                if (sampleIndex >= AudioData.Length) break;

                float min = 0, max = 0;
                for (var i = 0; i < samplesPerPixel && sampleIndex + i < AudioData.Length; i++)
                {
                    var sample = AudioData[sampleIndex + i];
                    if (sample < min) min = sample;
                    if (sample > max) max = sample;
                }

                var y1 = centerY - max * halfHeight;
                var y2 = centerY - min * halfHeight;

                if (x == 0)
                {
                    ctx.BeginFigure(new Point(x, y1), false, false);
                }
                ctx.LineTo(new Point(x, y1), true, false);
                ctx.LineTo(new Point(x, y2), true, false);
            }
        }

        geometry.Freeze();
        WaveformPath.Data = geometry;
    }

    private void UpdateStretchRegions()
    {
        StretchRegionCanvas.Children.Clear();

        if (Markers == null || Markers.Count < 2) return;

        var sorted = _markerVisuals.OrderBy(mv => mv.Marker.OriginalTime).ToList();
        var height = StretchRegionCanvas.ActualHeight;

        for (var i = 0; i < sorted.Count - 1; i++)
        {
            var startMarker = sorted[i];
            var endMarker = sorted[i + 1];

            var originalDuration = endMarker.Marker.OriginalTime - startMarker.Marker.OriginalTime;
            var targetDuration = endMarker.Marker.TargetTime - startMarker.Marker.TargetTime;

            if (originalDuration <= 0) continue;

            var ratio = targetDuration / originalDuration;
            var x = startMarker.Marker.TargetTime * PixelsPerSecond;
            var width = targetDuration * PixelsPerSecond;

            Brush fill;
            if (ratio > 1.05)
            {
                // Stretched (green)
                fill = new SolidColorBrush(Color.FromArgb(0x40, 0x4C, 0xAF, 0x50));
            }
            else if (ratio < 0.95)
            {
                // Compressed (red)
                fill = new SolidColorBrush(Color.FromArgb(0x40, 0xF4, 0x43, 0x36));
            }
            else
            {
                continue; // No significant change
            }

            var rect = new Rectangle
            {
                Width = Math.Max(1, width),
                Height = height,
                Fill = fill
            };
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, 0);
            StretchRegionCanvas.Children.Add(rect);
        }
    }

    private void UpdateStretchRatioDisplay()
    {
        if (_selectedMarker != null)
        {
            var ratio = _selectedMarker.Marker.OriginalTime > 0
                ? _selectedMarker.Marker.TargetTime / _selectedMarker.Marker.OriginalTime
                : 1.0;
            StretchRatioText.Text = $"{ratio:F2}x";
            StretchRatioText.Foreground = ratio > 1.0
                ? new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))
                : ratio < 1.0
                    ? new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36))
                    : new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
        }
        else
        {
            var totalRatio = GetTotalStretchRatio();
            StretchRatioText.Text = $"{totalRatio:F2}x";
        }
    }

    private void UpdateDurationDisplay()
    {
        var duration = GetAudioDuration();
        DurationText.Text = FormatTime(duration);
    }

    private void UpdateMarkerInfo()
    {
        var count = Markers?.Count ?? 0;
        MarkerInfoText.Text = $"{count} marker{(count != 1 ? "s" : "")}";
    }

    private void UpdateStatus()
    {
        StatusText.Text = IsStretchEnabled ? "Stretch mode enabled" : "Ready";
    }

    private double GetAudioDuration()
    {
        if (AudioData == null || AudioData.Length == 0) return 0;
        return (double)AudioData.Length / SampleRate;
    }

    private static string FormatTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.TotalMinutes >= 1
            ? $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}.{ts.Milliseconds:D3}"
            : $"{ts.Seconds}.{ts.Milliseconds:D3}";
    }

    #endregion
}

/// <summary>
/// Represents a stretch marker with original and target time positions.
/// </summary>
public class StretchMarker : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private double _originalTime;
    private double _targetTime;
    private bool _isLocked;

    public string Id
    {
        get => _id;
        set { _id = value; OnPropertyChanged(); }
    }

    public double OriginalTime
    {
        get => _originalTime;
        set { _originalTime = value; OnPropertyChanged(); }
    }

    public double TargetTime
    {
        get => _targetTime;
        set { _targetTime = value; OnPropertyChanged(); OnPropertyChanged(nameof(StretchRatio)); }
    }

    public bool IsLocked
    {
        get => _isLocked;
        set { _isLocked = value; OnPropertyChanged(); }
    }

    public double StretchRatio => OriginalTime > 0 ? TargetTime / OriginalTime : 1.0;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Visual representation of a stretch marker.
/// </summary>
internal class MarkerVisual
{
    public StretchMarker Marker { get; }
    public Line Line { get; }
    public Ellipse Handle { get; }
    public TextBlock Label { get; }

    private static readonly Brush NormalBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x99, 0x00));
    private static readonly Brush SelectedBrush = Brushes.White;
    private static readonly Brush HoverBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xCC, 0x00));

    public MarkerVisual(StretchMarker marker)
    {
        Marker = marker;

        Line = new Line
        {
            Stroke = NormalBrush,
            StrokeThickness = 2,
            Y1 = 0
        };

        Handle = new Ellipse
        {
            Width = 12,
            Height = 12,
            Fill = NormalBrush,
            Stroke = Brushes.White,
            StrokeThickness = 1,
            Cursor = Cursors.SizeWE
        };

        Label = new TextBlock
        {
            FontSize = 9,
            Foreground = NormalBrush
        };
    }

    public void SetSelected(bool selected)
    {
        var brush = selected ? SelectedBrush : NormalBrush;
        Line.Stroke = brush;
        Handle.Fill = brush;
        Label.Foreground = brush;
        Line.StrokeThickness = selected ? 3 : 2;
    }

    public void SetHovered(bool hovered)
    {
        if (Line.Stroke == SelectedBrush) return; // Don't override selection
        var brush = hovered ? HoverBrush : NormalBrush;
        Line.Stroke = brush;
        Handle.Fill = brush;
    }
}
