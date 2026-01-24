using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MusicEngine.Core;
using MusicEngineEditor.Controls;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Views;

/// <summary>
/// Main editor view for audio clips with waveform display, selection, and editing capabilities.
/// </summary>
public partial class AudioClipEditorView : UserControl
{
    #region Fields

    private AudioClipEditorViewModel? _viewModel;
    private bool _isDraggingStartHandle;
    private bool _isDraggingEndHandle;
    private Point _dragStartPoint;
    private double _initialSelectionValue;
    private bool _isUpdatingFades;

    #endregion

    #region Constructor

    public AudioClipEditorView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    #endregion

    #region Event Handlers

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is AudioClipEditorViewModel oldVm)
        {
            oldVm.PropertyChanged -= ViewModel_PropertyChanged;
        }

        if (e.NewValue is AudioClipEditorViewModel newVm)
        {
            _viewModel = newVm;
            newVm.PropertyChanged += ViewModel_PropertyChanged;
            UpdateSelectionHandles();
            UpdateFadeOverlays();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        DrawTimeRuler();
        UpdateSelectionHandles();
        UpdateFadeOverlays();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawTimeRuler();
        UpdateSelectionHandles();
        UpdateFadeOverlays();
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AudioClipEditorViewModel.SelectionStart):
            case nameof(AudioClipEditorViewModel.SelectionEnd):
            case nameof(AudioClipEditorViewModel.HasValidSelection):
                UpdateSelectionHandles();
                break;

            case nameof(AudioClipEditorViewModel.FadeInDuration):
            case nameof(AudioClipEditorViewModel.FadeOutDuration):
            case nameof(AudioClipEditorViewModel.FadeInType):
            case nameof(AudioClipEditorViewModel.FadeOutType):
            case nameof(AudioClipEditorViewModel.ClipLength):
                UpdateFadeOverlays();
                break;

            case nameof(AudioClipEditorViewModel.ZoomLevelX):
                DrawTimeRuler();
                UpdateSelectionHandles();
                UpdateFadeOverlays();
                break;
        }
    }

    #endregion

    #region Time Ruler

    private void DrawTimeRuler()
    {
        TimeRulerCanvas.Children.Clear();

        var width = TimeRulerCanvas.ActualWidth;
        var height = TimeRulerCanvas.ActualHeight;

        if (width <= 0 || height <= 0 || _viewModel == null) return;

        var clipLength = _viewModel.ClipLength;
        if (clipLength <= 0) clipLength = 16; // Default

        var zoomLevel = _viewModel.ZoomLevelX;
        var pixelsPerBeat = width / clipLength * zoomLevel;

        // Determine tick interval based on zoom
        double tickInterval;
        if (pixelsPerBeat > 100) tickInterval = 0.25;
        else if (pixelsPerBeat > 50) tickInterval = 0.5;
        else if (pixelsPerBeat > 25) tickInterval = 1.0;
        else if (pixelsPerBeat > 10) tickInterval = 2.0;
        else tickInterval = 4.0;

        var textBrush = (SolidColorBrush)FindResource("ForegroundBrush") ?? new SolidColorBrush(Colors.White);
        var lineBrush = (SolidColorBrush)FindResource("SubtleBorderBrush") ?? new SolidColorBrush(Color.FromRgb(0x43, 0x45, 0x4A));

        var scrollOffset = _viewModel.ScrollOffset;
        var startBeat = scrollOffset / pixelsPerBeat;
        var endBeat = (scrollOffset + width) / pixelsPerBeat;

        for (var beat = Math.Floor(startBeat / tickInterval) * tickInterval; beat <= endBeat; beat += tickInterval)
        {
            var x = (beat - startBeat) * pixelsPerBeat;

            if (x < 0 || x > width) continue;

            // Draw tick line
            var isMajorTick = Math.Abs(beat % 1.0) < 0.001;
            var line = new Line
            {
                X1 = x,
                X2 = x,
                Y1 = isMajorTick ? 0 : height * 0.5,
                Y2 = height,
                Stroke = lineBrush,
                StrokeThickness = 1
            };
            TimeRulerCanvas.Children.Add(line);

            // Draw beat number for major ticks
            if (isMajorTick && beat >= 0)
            {
                var text = new TextBlock
                {
                    Text = ((int)beat + 1).ToString(),
                    FontSize = 10,
                    Foreground = textBrush
                };
                Canvas.SetLeft(text, x + 3);
                Canvas.SetTop(text, 2);
                TimeRulerCanvas.Children.Add(text);
            }
        }
    }

    #endregion

    #region Selection Handles

    private void UpdateSelectionHandles()
    {
        if (_viewModel == null || !_viewModel.HasValidSelection)
        {
            SelectionStartHandle.Visibility = Visibility.Collapsed;
            SelectionEndHandle.Visibility = Visibility.Collapsed;
            return;
        }

        var width = SelectionHandlesCanvas.ActualWidth;
        var height = SelectionHandlesCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        var clipLength = _viewModel.ClipLength;
        if (clipLength <= 0) return;

        var pixelsPerBeat = width / clipLength * _viewModel.ZoomLevelX;
        var scrollOffset = _viewModel.ScrollOffset;

        // Calculate handle positions
        var startX = (_viewModel.SelectionStart * pixelsPerBeat) - scrollOffset - 4;
        var endX = (_viewModel.SelectionEnd * pixelsPerBeat) - scrollOffset - 4;

        // Update start handle
        Canvas.SetLeft(SelectionStartHandle, startX);
        Canvas.SetTop(SelectionStartHandle, 0);
        SelectionStartHandle.Height = height;
        SelectionStartHandle.Visibility = startX >= -8 && startX <= width ? Visibility.Visible : Visibility.Collapsed;

        // Update end handle
        Canvas.SetLeft(SelectionEndHandle, endX);
        Canvas.SetTop(SelectionEndHandle, 0);
        SelectionEndHandle.Height = height;
        SelectionEndHandle.Visibility = endX >= -8 && endX <= width ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SelectionHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel == null) return;

        var handle = sender as Border;
        _dragStartPoint = e.GetPosition(SelectionHandlesCanvas);

        if (handle == SelectionStartHandle)
        {
            _isDraggingStartHandle = true;
            _initialSelectionValue = _viewModel.SelectionStart;
        }
        else if (handle == SelectionEndHandle)
        {
            _isDraggingEndHandle = true;
            _initialSelectionValue = _viewModel.SelectionEnd;
        }

        handle?.CaptureMouse();
        e.Handled = true;
    }

    private void SelectionHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (_viewModel == null || (!_isDraggingStartHandle && !_isDraggingEndHandle)) return;

        var currentPoint = e.GetPosition(SelectionHandlesCanvas);
        var deltaX = currentPoint.X - _dragStartPoint.X;

        var width = SelectionHandlesCanvas.ActualWidth;
        var clipLength = _viewModel.ClipLength;
        if (width <= 0 || clipLength <= 0) return;

        var pixelsPerBeat = width / clipLength * _viewModel.ZoomLevelX;
        var deltaBeat = deltaX / pixelsPerBeat;

        if (_isDraggingStartHandle)
        {
            var newStart = Math.Max(0, Math.Min(_viewModel.SelectionEnd - 0.01, _initialSelectionValue + deltaBeat));
            _viewModel.SetSelection(newStart, _viewModel.SelectionEnd);
        }
        else if (_isDraggingEndHandle)
        {
            var newEnd = Math.Min(clipLength, Math.Max(_viewModel.SelectionStart + 0.01, _initialSelectionValue + deltaBeat));
            _viewModel.SetSelection(_viewModel.SelectionStart, newEnd);
        }
    }

    private void SelectionHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingStartHandle = false;
        _isDraggingEndHandle = false;
        (sender as Border)?.ReleaseMouseCapture();
    }

    #endregion

    #region Waveform Events

    private void WaveformDisplay_SelectionChanged(object? sender, WaveformSelectionEventArgs e)
    {
        if (_viewModel == null) return;

        if (e.HasSelection)
        {
            // Convert sample positions to beats (simplified - would need sample rate info)
            var startBeat = e.Start / 44100.0 * 2.0; // Assuming 44.1kHz and 120 BPM
            var endBeat = e.End / 44100.0 * 2.0;
            _viewModel.SetSelection(startBeat, endBeat);
        }
        else
        {
            _viewModel.ClearSelection();
        }
    }

    private void WaveformDisplay_PlayheadRequested(object? sender, WaveformPositionEventArgs e)
    {
        if (_viewModel == null) return;

        // Convert sample position to beats (simplified)
        var beat = e.SamplePosition / 44100.0 * 2.0; // Assuming 44.1kHz and 120 BPM
        _viewModel.PlayheadPosition = beat;
    }

    #endregion

    #region Fade Overlays

    private void UpdateFadeOverlays()
    {
        if (_viewModel == null || _isUpdatingFades) return;

        var width = WaveformDisplay.ActualWidth;
        var height = WaveformDisplay.ActualHeight;

        if (width <= 0 || height <= 0) return;

        var clipLength = _viewModel.ClipLength;
        if (clipLength <= 0) clipLength = 16;

        var pixelsPerBeat = width / clipLength * _viewModel.ZoomLevelX;

        // Update fade-in overlay
        UpdateFadeOverlay(
            FadeInOverlay,
            FadeInPath,
            _viewModel.FadeInDuration * pixelsPerBeat,
            height,
            _viewModel.FadeInType,
            true);

        // Update fade-out overlay
        UpdateFadeOverlay(
            FadeOutOverlay,
            FadeOutPath,
            _viewModel.FadeOutDuration * pixelsPerBeat,
            height,
            _viewModel.FadeOutType,
            false);
    }

    private static void UpdateFadeOverlay(Canvas canvas, Path path, double fadeWidth, double height, FadeType fadeType, bool isFadeIn)
    {
        if (fadeWidth <= 0)
        {
            path.Data = null;
            canvas.Width = 0;
            return;
        }

        canvas.Width = fadeWidth;
        canvas.Height = height;

        // Create fade path
        const int segments = 30;
        var geometry = new StreamGeometry();

        using (var context = geometry.Open())
        {
            if (isFadeIn)
            {
                // Fade-in: starts at top, curves down to bottom-right
                context.BeginFigure(new Point(0, 0), true, true);

                for (var i = 0; i <= segments; i++)
                {
                    var t = (double)i / segments;
                    var curveValue = CalculateFadeCurve(t, fadeType);
                    var x = t * fadeWidth;
                    var y = (1.0 - curveValue) * height;
                    context.LineTo(new Point(x, y), true, false);
                }

                context.LineTo(new Point(0, height), true, false);
            }
            else
            {
                // Fade-out: starts at bottom-left, curves up to top-right
                context.BeginFigure(new Point(fadeWidth, 0), true, true);

                for (var i = segments; i >= 0; i--)
                {
                    var t = (double)i / segments;
                    var curveValue = CalculateFadeCurve(t, fadeType);
                    var x = t * fadeWidth;
                    var y = curveValue * height;
                    context.LineTo(new Point(x, y), true, false);
                }

                context.LineTo(new Point(fadeWidth, height), true, false);
            }
        }

        geometry.Freeze();
        path.Data = geometry;
    }

    private static double CalculateFadeCurve(double t, FadeType fadeType)
    {
        t = Math.Clamp(t, 0, 1);

        return fadeType switch
        {
            FadeType.Linear => t,
            FadeType.Exponential => t * t,
            FadeType.Logarithmic => Math.Sqrt(t),
            FadeType.SCurve => t * t * (3 - 2 * t),
            FadeType.EqualPower => Math.Sin(t * Math.PI / 2),
            _ => t
        };
    }

    #endregion

    #region Fade Editor Events

    private void FadeEditor_FadeTypeChanged(object? sender, FadeType e)
    {
        UpdateFadeOverlays();
    }

    private void FadeEditor_DurationChanged(object? sender, double e)
    {
        UpdateFadeOverlays();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Loads an audio clip for editing.
    /// </summary>
    /// <param name="clip">The audio clip to edit.</param>
    public void LoadClip(AudioClip clip)
    {
        if (_viewModel != null)
        {
            _viewModel.LoadClip(clip);

            // Update fade editors
            _isUpdatingFades = true;
            FadeInEditor.SetFade(clip.FadeInType, clip.FadeInDuration, true);
            FadeOutEditor.SetFade(clip.FadeOutType, clip.FadeOutDuration, false);
            _isUpdatingFades = false;

            DrawTimeRuler();
            UpdateFadeOverlays();
        }
    }

    /// <summary>
    /// Gets the current view model.
    /// </summary>
    public AudioClipEditorViewModel? ViewModel => _viewModel;

    #endregion
}
