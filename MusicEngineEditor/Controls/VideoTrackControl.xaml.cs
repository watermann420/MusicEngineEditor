// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Shapes = System.Windows.Shapes;
using Microsoft.Win32;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Video track control for displaying video thumbnails and timecode on a timeline.
/// Supports scrubbing, offset adjustment, and frame preview.
/// </summary>
public partial class VideoTrackControl : UserControl, INotifyPropertyChanged
{
    #region Constants

    private const double DefaultBeatWidth = 40.0;
    private const int BeatsPerBar = 4;
    private const double DefaultFrameRate = 24.0;
    private const int DefaultThumbnailWidth = 80;

    #endregion

    #region Private Fields

    private string? _videoPath;
    private double _frameRate = DefaultFrameRate;
    private int _videoWidth;
    private int _videoHeight;
    private bool _isScrubbing;
    private Point _scrubStartPoint;
    private readonly List<BitmapSource> _thumbnails = [];
    private readonly List<System.Windows.Controls.Image> _thumbnailImages = [];

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty VideoPathProperty =
        DependencyProperty.Register(nameof(VideoPath), typeof(string), typeof(VideoTrackControl),
            new PropertyMetadata(null, OnVideoPathChanged));

    public static readonly DependencyProperty DurationProperty =
        DependencyProperty.Register(nameof(Duration), typeof(double), typeof(VideoTrackControl),
            new PropertyMetadata(0.0, OnDurationChanged));

    public static readonly DependencyProperty ThumbnailWidthProperty =
        DependencyProperty.Register(nameof(ThumbnailWidth), typeof(int), typeof(VideoTrackControl),
            new PropertyMetadata(DefaultThumbnailWidth, OnLayoutPropertyChanged));

    public static readonly DependencyProperty OffsetProperty =
        DependencyProperty.Register(nameof(Offset), typeof(double), typeof(VideoTrackControl),
            new PropertyMetadata(0.0, OnOffsetChanged));

    public static readonly DependencyProperty PixelsPerBeatProperty =
        DependencyProperty.Register(nameof(PixelsPerBeat), typeof(double), typeof(VideoTrackControl),
            new PropertyMetadata(DefaultBeatWidth, OnLayoutPropertyChanged));

    public static readonly DependencyProperty ScrollOffsetProperty =
        DependencyProperty.Register(nameof(ScrollOffset), typeof(double), typeof(VideoTrackControl),
            new PropertyMetadata(0.0, OnScrollChanged));

    public static readonly DependencyProperty PlayheadPositionProperty =
        DependencyProperty.Register(nameof(PlayheadPosition), typeof(double), typeof(VideoTrackControl),
            new PropertyMetadata(0.0, OnPlayheadChanged));

    public static readonly DependencyProperty TempoProperty =
        DependencyProperty.Register(nameof(Tempo), typeof(double), typeof(VideoTrackControl),
            new PropertyMetadata(120.0, OnLayoutPropertyChanged));

    /// <summary>
    /// Gets or sets the path to the video file.
    /// </summary>
    public string? VideoPath
    {
        get => (string?)GetValue(VideoPathProperty);
        set => SetValue(VideoPathProperty, value);
    }

    /// <summary>
    /// Gets or sets the video duration in seconds.
    /// </summary>
    public double Duration
    {
        get => (double)GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    /// <summary>
    /// Gets or sets the thumbnail width in pixels.
    /// </summary>
    public int ThumbnailWidth
    {
        get => (int)GetValue(ThumbnailWidthProperty);
        set => SetValue(ThumbnailWidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the video offset in seconds (positive = video starts later).
    /// </summary>
    public double Offset
    {
        get => (double)GetValue(OffsetProperty);
        set => SetValue(OffsetProperty, value);
    }

    /// <summary>
    /// Gets or sets the pixels per beat for horizontal scaling.
    /// </summary>
    public double PixelsPerBeat
    {
        get => (double)GetValue(PixelsPerBeatProperty);
        set => SetValue(PixelsPerBeatProperty, value);
    }

    /// <summary>
    /// Gets or sets the horizontal scroll offset.
    /// </summary>
    public double ScrollOffset
    {
        get => (double)GetValue(ScrollOffsetProperty);
        set => SetValue(ScrollOffsetProperty, value);
    }

    /// <summary>
    /// Gets or sets the playhead position in beats.
    /// </summary>
    public double PlayheadPosition
    {
        get => (double)GetValue(PlayheadPositionProperty);
        set => SetValue(PlayheadPositionProperty, value);
    }

    /// <summary>
    /// Gets or sets the project tempo in BPM.
    /// </summary>
    public double Tempo
    {
        get => (double)GetValue(TempoProperty);
        set => SetValue(TempoProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Occurs when a video frame is requested at a specific time.
    /// </summary>
    public event EventHandler<FrameRequestedEventArgs>? FrameRequested;

    /// <summary>
    /// Occurs when the offset is changed.
    /// </summary>
    public event EventHandler<double>? OffsetChanged;

    /// <summary>
    /// Occurs when a video is loaded.
    /// </summary>
    public event EventHandler<string>? VideoLoaded;

    /// <summary>
    /// Occurs when the video is removed.
    /// </summary>
    public event EventHandler? VideoRemoved;

    #endregion

    #region Constructor

    public VideoTrackControl()
    {
        InitializeComponent();
        DataContext = this;

        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
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

    private static void OnVideoPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VideoTrackControl control)
        {
            control.LoadVideo(e.NewValue as string);
        }
    }

    private static void OnDurationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VideoTrackControl control)
        {
            control.UpdateDurationDisplay();
            control.RenderThumbnails();
        }
    }

    private static void OnOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VideoTrackControl control)
        {
            control.UpdateOffsetDisplay();
            control.RenderThumbnails();
            control.OffsetChanged?.Invoke(control, (double)e.NewValue);
        }
    }

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VideoTrackControl control)
        {
            control.RenderAll();
        }
    }

    private static void OnScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VideoTrackControl control)
        {
            control.ApplyScrollTransform();
        }
    }

    private static void OnPlayheadChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VideoTrackControl control)
        {
            control.UpdatePlayhead();
            control.UpdateCurrentTimecode();
        }
    }

    #endregion

    #region Video Loading

    private void LoadVideo(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            ClearVideo();
            return;
        }

        _videoPath = path;

        // Update UI
        FileNameText.Text = Path.GetFileName(path);
        RemoveVideoButton.IsEnabled = true;
        NoVideoPlaceholder.Visibility = Visibility.Collapsed;

        // Get video info (in a real implementation, this would use FFmpeg or similar)
        // For now, we'll use placeholder values
        _frameRate = 24.0;
        _videoWidth = 1920;
        _videoHeight = 1080;
        Duration = 120.0; // Placeholder: 2 minutes

        UpdateVideoInfo();
        GenerateThumbnails();
        RenderThumbnails();

        VideoLoaded?.Invoke(this, path);
    }

    private void ClearVideo()
    {
        _videoPath = null;
        _thumbnails.Clear();
        _thumbnailImages.Clear();
        ThumbnailsCanvas.Children.Clear();

        FileNameText.Text = "(No video loaded)";
        RemoveVideoButton.IsEnabled = false;
        NoVideoPlaceholder.Visibility = Visibility.Visible;
        Duration = 0;
        Offset = 0;

        UpdateVideoInfo();
    }

    private void UpdateVideoInfo()
    {
        FrameRateText.Text = _videoPath != null ? $"{_frameRate:F2}" : "--";
        ResolutionText.Text = _videoPath != null ? $"{_videoWidth}x{_videoHeight}" : "--";
        UpdateDurationDisplay();
        UpdateOffsetDisplay();
    }

    private void GenerateThumbnails()
    {
        _thumbnails.Clear();

        if (string.IsNullOrEmpty(_videoPath) || Duration <= 0)
            return;

        // Calculate number of thumbnails needed
        var height = ThumbnailsCanvas.ActualHeight > 0 ? ThumbnailsCanvas.ActualHeight : 60;
        var aspectRatio = _videoWidth > 0 && _videoHeight > 0 ? (double)_videoWidth / _videoHeight : 16.0 / 9.0;
        var thumbnailHeight = height - 4;
        var thumbnailWidth = thumbnailHeight * aspectRatio;

        var secondsPerThumbnail = thumbnailWidth / (PixelsPerBeat * (Tempo / 60.0));
        var thumbnailCount = (int)Math.Ceiling(Duration / secondsPerThumbnail) + 1;

        // Generate placeholder thumbnails (in real implementation, extract from video)
        for (int i = 0; i < thumbnailCount; i++)
        {
            var thumbnail = GeneratePlaceholderThumbnail((int)thumbnailWidth, (int)thumbnailHeight, i);
            _thumbnails.Add(thumbnail);
        }
    }

    private BitmapSource GeneratePlaceholderThumbnail(int width, int height, int index)
    {
        // Create a simple gradient placeholder
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            // Background gradient based on position
            var hue = (index * 30) % 360;
            var color = HsvToRgb(hue, 0.3, 0.3);
            context.DrawRectangle(
                new SolidColorBrush(color),
                null,
                new Rect(0, 0, width, height));

            // Draw frame number
            var formattedText = new FormattedText(
                $"Frame {index}",
                System.Globalization.CultureInfo.CurrentCulture,
                System.Windows.FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                10,
                Brushes.White,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            context.DrawText(formattedText, new Point(4, height - 16));
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();

        return bitmap;
    }

    private static Color HsvToRgb(double h, double s, double v)
    {
        var hi = (int)(h / 60) % 6;
        var f = (h / 60) - hi;
        var p = v * (1 - s);
        var q = v * (1 - f * s);
        var t = v * (1 - (1 - f) * s);

        double r, g, b;
        switch (hi)
        {
            case 0: r = v; g = t; b = p; break;
            case 1: r = q; g = v; b = p; break;
            case 2: r = p; g = v; b = t; break;
            case 3: r = p; g = q; b = v; break;
            case 4: r = t; g = p; b = v; break;
            default: r = v; g = p; b = q; break;
        }

        return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    #endregion

    #region Rendering

    private void RenderAll()
    {
        if (!IsLoaded) return;

        RenderGrid();
        RenderThumbnails();
        UpdatePlayhead();
        ApplyScrollTransform();
    }

    private void RenderGrid()
    {
        GridCanvas.Children.Clear();

        var height = ThumbnailsCanvas.ActualHeight > 0 ? ThumbnailsCanvas.ActualHeight : 60;
        var totalBeats = Duration * (Tempo / 60.0);
        var totalWidth = totalBeats * PixelsPerBeat;

        GridCanvas.Width = totalWidth;
        GridCanvas.Height = height;

        // Draw bar lines
        for (int beat = 0; beat <= totalBeats; beat++)
        {
            var x = beat * PixelsPerBeat;
            var isBarLine = beat % BeatsPerBar == 0;

            var line = new Shapes.Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = height,
                Stroke = new SolidColorBrush(isBarLine ? Color.FromRgb(0x3A, 0x3A, 0x3A) : Color.FromRgb(0x2A, 0x2A, 0x2A)),
                StrokeThickness = isBarLine ? 1 : 0.5,
                Opacity = 0.5
            };
            GridCanvas.Children.Add(line);
        }
    }

    private void RenderThumbnails()
    {
        ThumbnailsCanvas.Children.Clear();
        _thumbnailImages.Clear();

        if (_thumbnails.Count == 0) return;

        var height = ThumbnailsCanvas.ActualHeight > 0 ? ThumbnailsCanvas.ActualHeight : 60;
        var totalBeats = Duration * (Tempo / 60.0);
        var totalWidth = totalBeats * PixelsPerBeat;
        var offsetPixels = Offset * (Tempo / 60.0) * PixelsPerBeat;

        ThumbnailsCanvas.Width = totalWidth + Math.Abs(offsetPixels);
        ThumbnailsCanvas.Height = height;

        // Calculate thumbnail dimensions
        var aspectRatio = _videoWidth > 0 && _videoHeight > 0 ? (double)_videoWidth / _videoHeight : 16.0 / 9.0;
        var thumbnailHeight = height - 4;
        var thumbnailWidth = thumbnailHeight * aspectRatio;

        // Position thumbnails
        double x = Math.Max(0, offsetPixels);
        for (int i = 0; i < _thumbnails.Count && x < totalWidth + Math.Abs(offsetPixels); i++)
        {
            var image = new System.Windows.Controls.Image
            {
                Source = _thumbnails[i],
                Width = thumbnailWidth,
                Height = thumbnailHeight,
                Stretch = Stretch.Fill,
                Opacity = 0.9
            };

            Canvas.SetLeft(image, x);
            Canvas.SetTop(image, 2);

            ThumbnailsCanvas.Children.Add(image);
            _thumbnailImages.Add(image);

            x += thumbnailWidth;
        }

        // Draw video clip border
        var clipBorder = new Shapes.Rectangle
        {
            Width = totalWidth - Math.Abs(offsetPixels),
            Height = height - 4,
            Stroke = new SolidColorBrush(Color.FromRgb(0x4A, 0x9E, 0xFF)),
            StrokeThickness = 2,
            Fill = null,
            RadiusX = 4,
            RadiusY = 4
        };
        Canvas.SetLeft(clipBorder, Math.Max(0, offsetPixels));
        Canvas.SetTop(clipBorder, 2);
        ThumbnailsCanvas.Children.Add(clipBorder);
    }

    private void UpdatePlayhead()
    {
        var height = ThumbnailsCanvas.ActualHeight > 0 ? ThumbnailsCanvas.ActualHeight : 60;
        var x = PlayheadPosition * PixelsPerBeat - ScrollOffset;

        if (x >= 0 && x <= ActualWidth)
        {
            Playhead.Visibility = Visibility.Visible;
            Playhead.Height = height;
            Canvas.SetLeft(Playhead, x);
        }
        else
        {
            Playhead.Visibility = Visibility.Collapsed;
        }
    }

    private void ApplyScrollTransform()
    {
        var transform = new TranslateTransform(-ScrollOffset, 0);
        GridCanvas.RenderTransform = transform;
        ThumbnailsCanvas.RenderTransform = transform;
    }

    private void UpdateDurationDisplay()
    {
        DurationText.Text = FormatTimecode(Duration, _frameRate);
    }

    private void UpdateOffsetDisplay()
    {
        OffsetText.Text = $"{Offset:+0.00;-0.00;0.00}s";
    }

    private void UpdateCurrentTimecode()
    {
        var seconds = PlayheadPosition * (60.0 / Tempo) - Offset;
        CurrentTimecodeText.Text = FormatTimecode(Math.Max(0, seconds), _frameRate);
    }

    private static string FormatTimecode(double seconds, double fps)
    {
        if (seconds < 0) seconds = 0;

        var totalFrames = (int)(seconds * fps);
        var frames = totalFrames % (int)fps;
        var totalSeconds = (int)seconds;
        var secs = totalSeconds % 60;
        var mins = (totalSeconds / 60) % 60;
        var hours = totalSeconds / 3600;

        return $"{hours:D2}:{mins:D2}:{secs:D2}:{frames:D2}";
    }

    #endregion

    #region Mouse Event Handlers

    private void ThumbnailsCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (string.IsNullOrEmpty(_videoPath)) return;

        _isScrubbing = true;
        _scrubStartPoint = e.GetPosition(ThumbnailsCanvas);
        ThumbnailsCanvas.CaptureMouse();

        UpdateScrubPreview(_scrubStartPoint);
    }

    private void ThumbnailsCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isScrubbing) return;

        var position = e.GetPosition(ThumbnailsCanvas);
        UpdateScrubPreview(position);
    }

    private void ThumbnailsCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isScrubbing) return;

        _isScrubbing = false;
        ThumbnailsCanvas.ReleaseMouseCapture();
        ScrubPreview.Visibility = Visibility.Collapsed;

        // Request the final frame
        var position = e.GetPosition(ThumbnailsCanvas);
        var time = PositionToTime(position.X + ScrollOffset);
        FrameRequested?.Invoke(this, new FrameRequestedEventArgs(time));
    }

    private void ThumbnailsCanvas_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_isScrubbing && e.LeftButton != MouseButtonState.Pressed)
        {
            _isScrubbing = false;
            ThumbnailsCanvas.ReleaseMouseCapture();
            ScrubPreview.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateScrubPreview(Point position)
    {
        var adjustedX = position.X + ScrollOffset;
        var time = PositionToTime(adjustedX);

        // Update timecode
        ScrubTimecode.Text = FormatTimecode(time, _frameRate);

        // Position preview
        var previewX = Math.Min(position.X + 10, ActualWidth - 170);
        var previewY = Math.Max(position.Y - 100, 0);
        ScrubPreview.Margin = new Thickness(previewX, previewY, 0, 0);
        ScrubPreview.Visibility = Visibility.Visible;

        // Request frame for preview (in real implementation)
        FrameRequested?.Invoke(this, new FrameRequestedEventArgs(time, true));
    }

    private double PositionToTime(double x)
    {
        var beat = x / PixelsPerBeat;
        var seconds = beat * (60.0 / Tempo);
        return Math.Max(0, seconds - Offset);
    }

    #endregion

    #region Button Event Handlers

    private void LoadVideoButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Video Files|*.mp4;*.avi;*.mov;*.mkv;*.wmv;*.webm|All Files|*.*",
            Title = "Select Video File"
        };

        if (dialog.ShowDialog() == true)
        {
            VideoPath = dialog.FileName;
        }
    }

    private void RemoveVideoButton_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Remove video from track?", "Remove Video",
            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            ClearVideo();
            VideoRemoved?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OffsetSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateOffsetDisplay();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Refreshes the video track display.
    /// </summary>
    public void Refresh()
    {
        RenderAll();
    }

    /// <summary>
    /// Sets the preview frame image.
    /// </summary>
    public void SetPreviewFrame(BitmapSource? frame)
    {
        ScrubPreviewImage.Source = frame;
    }

    /// <summary>
    /// Gets the video time at a specific beat position.
    /// </summary>
    public double GetTimeAtBeat(double beat)
    {
        var seconds = beat * (60.0 / Tempo);
        return Math.Max(0, seconds - Offset);
    }

    /// <summary>
    /// Gets the beat position for a specific video time.
    /// </summary>
    public double GetBeatAtTime(double time)
    {
        var seconds = time + Offset;
        return seconds * (Tempo / 60.0);
    }

    #endregion

    #region INotifyPropertyChanged

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}

/// <summary>
/// Event arguments for frame request events.
/// </summary>
public class FrameRequestedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the time in seconds at which the frame is requested.
    /// </summary>
    public double Time { get; }

    /// <summary>
    /// Gets whether this is a preview request (for scrubbing).
    /// </summary>
    public bool IsPreview { get; }

    public FrameRequestedEventArgs(double time, bool isPreview = false)
    {
        Time = time;
        IsPreview = isPreview;
    }
}
