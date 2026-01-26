using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Control for slip editing audio clips.
/// Shows the full audio content and allows slipping the content within clip boundaries.
/// </summary>
public partial class SlipEditControl : UserControl
{
    #region Dependency Properties

    public static new readonly DependencyProperty ClipProperty =
        DependencyProperty.Register(nameof(Clip), typeof(ClipViewModel), typeof(SlipEditControl),
            new PropertyMetadata(null, OnClipChanged));

    public static readonly DependencyProperty FullWaveformDataProperty =
        DependencyProperty.Register(nameof(FullWaveformData), typeof(float[]), typeof(SlipEditControl),
            new PropertyMetadata(null, OnWaveformDataChanged));

    public static readonly DependencyProperty PixelsPerBeatProperty =
        DependencyProperty.Register(nameof(PixelsPerBeat), typeof(double), typeof(SlipEditControl),
            new PropertyMetadata(50.0, OnLayoutPropertyChanged));

    public static readonly DependencyProperty IsSnapEnabledProperty =
        DependencyProperty.Register(nameof(IsSnapEnabled), typeof(bool), typeof(SlipEditControl),
            new PropertyMetadata(true));

    public static readonly DependencyProperty SnapResolutionProperty =
        DependencyProperty.Register(nameof(SnapResolution), typeof(double), typeof(SlipEditControl),
            new PropertyMetadata(0.25)); // Quarter beat default

    public static readonly DependencyProperty IsSlipModeActiveProperty =
        DependencyProperty.Register(nameof(IsSlipModeActive), typeof(bool), typeof(SlipEditControl),
            new PropertyMetadata(false, OnSlipModeChanged));

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the clip being slip-edited.
    /// </summary>
    public new ClipViewModel? Clip
    {
        get => (ClipViewModel?)GetValue(ClipProperty);
        set => SetValue(ClipProperty, value);
    }

    /// <summary>
    /// Gets or sets the full waveform data (including trimmed portions).
    /// </summary>
    public float[]? FullWaveformData
    {
        get => (float[]?)GetValue(FullWaveformDataProperty);
        set => SetValue(FullWaveformDataProperty, value);
    }

    /// <summary>
    /// Gets or sets the pixels per beat for display.
    /// </summary>
    public double PixelsPerBeat
    {
        get => (double)GetValue(PixelsPerBeatProperty);
        set => SetValue(PixelsPerBeatProperty, value);
    }

    /// <summary>
    /// Gets or sets whether snapping is enabled.
    /// </summary>
    public bool IsSnapEnabled
    {
        get => (bool)GetValue(IsSnapEnabledProperty);
        set => SetValue(IsSnapEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets the snap resolution in beats.
    /// </summary>
    public double SnapResolution
    {
        get => (double)GetValue(SnapResolutionProperty);
        set => SetValue(SnapResolutionProperty, value);
    }

    /// <summary>
    /// Gets or sets whether slip mode is active.
    /// </summary>
    public bool IsSlipModeActive
    {
        get => (bool)GetValue(IsSlipModeActiveProperty);
        set => SetValue(IsSlipModeActiveProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Raised when the slip offset changes.
    /// </summary>
    public event EventHandler<SlipChangedEventArgs>? SlipChanged;

    /// <summary>
    /// Raised when slip editing is completed.
    /// </summary>
    public event EventHandler<SlipCompletedEventArgs>? SlipCompleted;

    /// <summary>
    /// Raised when slip mode is exited.
    /// </summary>
    public event EventHandler? SlipModeExited;

    #endregion

    #region Fields

    private bool _isDragging;
    private Point _dragStartPoint;
    private double _originalSourceOffset;
    private double _currentSlipAmount;
    private bool _showInstructions = true;

    #endregion

    public SlipEditControl()
    {
        InitializeComponent();

        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseMove += OnMouseMove;
        KeyDown += OnKeyDown;
        SizeChanged += OnSizeChanged;

        Focusable = true;
    }

    #region Property Changed Callbacks

    private static void OnClipChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SlipEditControl control)
        {
            if (e.OldValue is ClipViewModel oldClip)
            {
                oldClip.PropertyChanged -= control.OnClipPropertyChanged;
            }

            if (e.NewValue is ClipViewModel newClip)
            {
                newClip.PropertyChanged += control.OnClipPropertyChanged;
                control._originalSourceOffset = newClip.SourceOffset;
            }

            control.UpdateVisualLayout();
        }
    }

    private static void OnWaveformDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SlipEditControl control)
        {
            control.RenderWaveforms();
        }
    }

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SlipEditControl control)
        {
            control.UpdateVisualLayout();
        }
    }

    private static void OnSlipModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SlipEditControl control)
        {
            if ((bool)e.NewValue)
            {
                control.EnterSlipMode();
            }
            else
            {
                control.ExitSlipMode();
            }
        }
    }

    private void OnClipPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ClipViewModel.SourceOffset) ||
            e.PropertyName == nameof(ClipViewModel.Length))
        {
            UpdateVisualLayout();
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateVisualLayout();
    }

    #endregion

    #region Slip Mode

    private void EnterSlipMode()
    {
        if (_showInstructions)
        {
            InstructionsOverlay.Visibility = Visibility.Visible;
            _showInstructions = false;

            // Hide instructions after a delay
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            timer.Tick += (s, e) =>
            {
                InstructionsOverlay.Visibility = Visibility.Collapsed;
                timer.Stop();
            };
            timer.Start();
        }

        ModeIndicator.Visibility = Visibility.Visible;
        Cursor = Cursors.SizeWE;
        Focus();
        UpdateVisualLayout();
    }

    private void ExitSlipMode()
    {
        ModeIndicator.Visibility = Visibility.Collapsed;
        InstructionsOverlay.Visibility = Visibility.Collapsed;
        SlipIndicator.Visibility = Visibility.Collapsed;
        Cursor = Cursors.Arrow;

        if (_isDragging)
        {
            _isDragging = false;
            ReleaseMouseCapture();

            // Commit the slip
            if (Clip != null && Math.Abs(_currentSlipAmount) > 0.001)
            {
                SlipCompleted?.Invoke(this, new SlipCompletedEventArgs(
                    Clip, _originalSourceOffset, Clip.SourceOffset));
            }
        }

        SlipModeExited?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Toggles slip edit mode.
    /// </summary>
    public void ToggleSlipMode()
    {
        IsSlipModeActive = !IsSlipModeActive;
    }

    #endregion

    #region Mouse Handling

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsSlipModeActive || Clip == null) return;

        InstructionsOverlay.Visibility = Visibility.Collapsed;

        _isDragging = true;
        _dragStartPoint = e.GetPosition(this);
        _originalSourceOffset = Clip.SourceOffset;
        _currentSlipAmount = 0;

        SlipIndicator.Visibility = Visibility.Visible;
        UpdateSlipIndicator(0);

        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;

        _isDragging = false;
        ReleaseMouseCapture();

        SlipIndicator.Visibility = Visibility.Collapsed;

        if (Clip != null && Math.Abs(_currentSlipAmount) > 0.001)
        {
            SlipCompleted?.Invoke(this, new SlipCompletedEventArgs(
                Clip, _originalSourceOffset, Clip.SourceOffset));
        }

        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || Clip == null) return;

        var currentPoint = e.GetPosition(this);
        var deltaX = currentPoint.X - _dragStartPoint.X;

        // Convert pixels to beats
        var deltaBeats = deltaX / PixelsPerBeat;

        // Fine adjustment with Ctrl
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            deltaBeats *= 0.1;
        }

        // Apply snapping
        if (IsSnapEnabled && !Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            deltaBeats = Math.Round(deltaBeats / SnapResolution) * SnapResolution;
        }

        // Calculate new source offset
        var newOffset = _originalSourceOffset - deltaBeats;

        // Clamp to valid range (0 to originalLength - clipLength)
        var maxOffset = Clip.OriginalLength - Clip.Length;
        newOffset = Math.Max(0, Math.Min(newOffset, maxOffset));

        // Only update if changed
        if (Math.Abs(newOffset - Clip.SourceOffset) > 0.001)
        {
            _currentSlipAmount = _originalSourceOffset - newOffset;
            Clip.SourceOffset = newOffset;

            UpdateSlipIndicator(_currentSlipAmount);
            UpdateVisualLayout();

            SlipChanged?.Invoke(this, new SlipChangedEventArgs(Clip, _currentSlipAmount));
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (_isDragging)
            {
                // Cancel the slip
                _isDragging = false;
                ReleaseMouseCapture();

                if (Clip != null)
                {
                    Clip.SourceOffset = _originalSourceOffset;
                }

                SlipIndicator.Visibility = Visibility.Collapsed;
                _currentSlipAmount = 0;
            }
            else
            {
                IsSlipModeActive = false;
            }

            e.Handled = true;
        }
        else if (e.Key == Key.S && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            // Toggle snap with S key
            IsSnapEnabled = !IsSnapEnabled;
            e.Handled = true;
        }
    }

    #endregion

    #region Rendering

    private void UpdateVisualLayout()
    {
        if (Clip == null) return;

        var width = ActualWidth;
        var height = ActualHeight;

        if (width <= 0 || height <= 0) return;

        // Calculate boundary positions
        var totalWidth = Clip.OriginalLength * PixelsPerBeat;
        var clipStart = Clip.SourceOffset * PixelsPerBeat;
        var clipEnd = clipStart + (Clip.Length * PixelsPerBeat);

        // Update boundary lines
        LeftBoundaryLine.X1 = clipStart;
        LeftBoundaryLine.X2 = clipStart;
        LeftBoundaryLine.Y2 = height;

        RightBoundaryLine.X1 = clipEnd;
        RightBoundaryLine.X2 = clipEnd;
        RightBoundaryLine.Y2 = height;

        // Update overlays
        Canvas.SetLeft(LeftOverlay, 0);
        Canvas.SetTop(LeftOverlay, 0);
        LeftOverlay.Width = Math.Max(0, clipStart);
        LeftOverlay.Height = height;

        Canvas.SetLeft(RightOverlay, clipEnd);
        Canvas.SetTop(RightOverlay, 0);
        RightOverlay.Width = Math.Max(0, totalWidth - clipEnd);
        RightOverlay.Height = height;

        RenderWaveforms();
    }

    private void RenderWaveforms()
    {
        var data = FullWaveformData;
        if (data == null || data.Length == 0 || Clip == null)
        {
            FullWaveformPath.Data = null;
            ActiveWaveformPath.Data = null;
            return;
        }

        var width = ActualWidth;
        var height = ActualHeight;

        if (width <= 0 || height <= 0) return;

        var totalWidth = Clip.OriginalLength * PixelsPerBeat;
        var clipStart = Clip.SourceOffset * PixelsPerBeat;
        var clipWidth = Clip.Length * PixelsPerBeat;

        // Render full waveform
        FullWaveformPath.Data = CreateWaveformGeometry(data, 0, totalWidth, height);

        // Render active portion
        var startSample = (int)(Clip.SourceOffset / Clip.OriginalLength * data.Length);
        var endSample = (int)((Clip.SourceOffset + Clip.Length) / Clip.OriginalLength * data.Length);
        var activeData = new float[endSample - startSample];
        Array.Copy(data, startSample, activeData, 0, activeData.Length);

        ActiveWaveformPath.Data = CreateWaveformGeometry(activeData, clipStart, clipWidth, height);
    }

    private static Geometry CreateWaveformGeometry(float[] data, double startX, double width, double height)
    {
        if (data.Length == 0 || width <= 0) return Geometry.Empty;

        var centerY = height / 2;
        var halfHeight = height / 2 * 0.9;

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var pixelCount = Math.Min((int)width, data.Length);
            if (pixelCount <= 0) return Geometry.Empty;

            var samplesPerPixel = Math.Max(1, data.Length / pixelCount);
            var topPoints = new Point[pixelCount];
            var bottomPoints = new Point[pixelCount];

            for (int x = 0; x < pixelCount; x++)
            {
                var sampleIndex = x * samplesPerPixel;
                if (sampleIndex >= data.Length) break;

                var min = 0f;
                var max = 0f;
                for (int i = 0; i < samplesPerPixel && sampleIndex + i < data.Length; i++)
                {
                    var sample = data[sampleIndex + i];
                    if (sample < min) min = sample;
                    if (sample > max) max = sample;
                }

                topPoints[x] = new Point(startX + x, centerY - max * halfHeight);
                bottomPoints[x] = new Point(startX + x, centerY - min * halfHeight);
            }

            context.BeginFigure(topPoints[0], true, true);

            for (int i = 1; i < pixelCount; i++)
            {
                context.LineTo(topPoints[i], true, false);
            }

            for (int i = pixelCount - 1; i >= 0; i--)
            {
                context.LineTo(bottomPoints[i], true, false);
            }
        }

        geometry.Freeze();
        return geometry;
    }

    private void UpdateSlipIndicator(double slipAmount)
    {
        var sign = slipAmount >= 0 ? "+" : "";
        SlipAmountText.Text = $"{sign}{slipAmount:F2} beats";
    }

    #endregion
}

/// <summary>
/// Event arguments for slip changed events.
/// </summary>
public class SlipChangedEventArgs : EventArgs
{
    /// <summary>The clip being slipped.</summary>
    public ClipViewModel Clip { get; }

    /// <summary>The slip amount in beats.</summary>
    public double SlipAmount { get; }

    public SlipChangedEventArgs(ClipViewModel clip, double slipAmount)
    {
        Clip = clip;
        SlipAmount = slipAmount;
    }
}

/// <summary>
/// Event arguments for slip completed events.
/// </summary>
public class SlipCompletedEventArgs : EventArgs
{
    /// <summary>The clip that was slipped.</summary>
    public ClipViewModel Clip { get; }

    /// <summary>The original source offset before slipping.</summary>
    public double OriginalSourceOffset { get; }

    /// <summary>The new source offset after slipping.</summary>
    public double NewSourceOffset { get; }

    public SlipCompletedEventArgs(ClipViewModel clip, double originalOffset, double newOffset)
    {
        Clip = clip;
        OriginalSourceOffset = originalOffset;
        NewSourceOffset = newOffset;
    }
}
