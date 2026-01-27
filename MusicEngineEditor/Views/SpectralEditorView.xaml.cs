// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code-behind for the Spectral Editor view.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Views;

/// <summary>
/// Interaction logic for SpectralEditorView.xaml
/// </summary>
public partial class SpectralEditorView : UserControl
{
    private SpectralEditorViewModel? _viewModel;
    private WriteableBitmap? _spectrogramBitmap;
    private bool _isSelecting;
    private bool _isPainting;
    private Point _selectionStart;
    private Point _lastPaintPoint;

    // Cached colors for spectrogram rendering
    private readonly Color _backgroundColor = Color.FromRgb(0x0D, 0x0D, 0x0D);

    public SpectralEditorView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.SpectrogramUpdated -= OnSpectrogramUpdated;
            _viewModel.SelectionChanged -= OnSelectionChanged;
        }

        _viewModel = e.NewValue as SpectralEditorViewModel;

        if (_viewModel != null)
        {
            _viewModel.SpectrogramUpdated += OnSpectrogramUpdated;
            _viewModel.SelectionChanged += OnSelectionChanged;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        InitializeBitmap();
        DrawTimeRuler();
        DrawFrequencyAxis();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        InitializeBitmap();
        UpdateSpectrogram();
        DrawTimeRuler();
        DrawFrequencyAxis();
        UpdateSelectionVisual();
    }

    private void OnSpectrogramUpdated(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            UpdateSpectrogram();
            DrawTimeRuler();
            DrawFrequencyAxis();
        });
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            UpdateSelectionVisual();
        });
    }

    #region Bitmap Initialization and Rendering

    private void InitializeBitmap()
    {
        int width = (int)Math.Max(1, SelectionCanvas.ActualWidth);
        int height = (int)Math.Max(1, SelectionCanvas.ActualHeight);

        if (width <= 0 || height <= 0) return;

        _spectrogramBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        SpectrogramImage.Source = _spectrogramBitmap;
    }

    private void UpdateSpectrogram()
    {
        if (_spectrogramBitmap == null || _viewModel == null || !_viewModel.IsAudioLoaded)
        {
            ClearSpectrogram();
            return;
        }

        var spectrogramData = _viewModel.GetSpectrogramData();
        if (spectrogramData == null) return;

        int width = _spectrogramBitmap.PixelWidth;
        int height = _spectrogramBitmap.PixelHeight;
        int frameCount = spectrogramData.GetLength(0);
        int binCount = spectrogramData.GetLength(1);

        byte[] pixels = new byte[width * height * 4];

        double timeScale = (double)frameCount / width * _viewModel.ZoomLevelX;
        double freqScale = (double)binCount / height * _viewModel.ZoomLevelY;
        double timeOffset = _viewModel.ScrollOffsetX / 100.0 * frameCount;
        double freqOffset = _viewModel.ScrollOffsetY / 100.0 * binCount;

        for (int y = 0; y < height; y++)
        {
            int binIndex = (int)((height - 1 - y) * freqScale / _viewModel.ZoomLevelY + freqOffset);
            if (binIndex < 0 || binIndex >= binCount)
            {
                int pixelIndex = y * width * 4;
                for (int x = 0; x < width; x++)
                {
                    pixels[pixelIndex] = _backgroundColor.B;
                    pixels[pixelIndex + 1] = _backgroundColor.G;
                    pixels[pixelIndex + 2] = _backgroundColor.R;
                    pixels[pixelIndex + 3] = 255;
                    pixelIndex += 4;
                }
                continue;
            }

            for (int x = 0; x < width; x++)
            {
                int frameIndex = (int)(x * timeScale / _viewModel.ZoomLevelX + timeOffset);

                Color color;
                if (frameIndex < 0 || frameIndex >= frameCount)
                {
                    color = _backgroundColor;
                }
                else
                {
                    float magnitude = spectrogramData[frameIndex, binIndex];
                    color = _viewModel.GetColorForMagnitude(magnitude);
                }

                int pixelIndex = (y * width + x) * 4;
                pixels[pixelIndex] = color.B;
                pixels[pixelIndex + 1] = color.G;
                pixels[pixelIndex + 2] = color.R;
                pixels[pixelIndex + 3] = 255;
            }
        }

        _spectrogramBitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);
    }

    private void ClearSpectrogram()
    {
        if (_spectrogramBitmap == null) return;

        int width = _spectrogramBitmap.PixelWidth;
        int height = _spectrogramBitmap.PixelHeight;
        byte[] pixels = new byte[width * height * 4];

        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = _backgroundColor.B;
            pixels[i + 1] = _backgroundColor.G;
            pixels[i + 2] = _backgroundColor.R;
            pixels[i + 3] = 255;
        }

        _spectrogramBitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);
    }

    #endregion

    #region Ruler Drawing

    private void DrawTimeRuler()
    {
        TimeRulerCanvas.Children.Clear();

        if (_viewModel == null || !_viewModel.IsAudioLoaded) return;

        double width = TimeRulerCanvas.ActualWidth;
        double duration = _viewModel.Duration;
        double zoomX = _viewModel.ZoomLevelX;
        double offsetX = _viewModel.ScrollOffsetX / 100.0 * duration;

        double visibleDuration = duration / zoomX;
        double interval = CalculateTimeInterval(visibleDuration, width);

        double startTime = Math.Floor(offsetX / interval) * interval;
        double endTime = offsetX + visibleDuration;

        for (double time = startTime; time <= endTime; time += interval)
        {
            double x = (time - offsetX) / visibleDuration * width;
            if (x < 0 || x > width) continue;

            var line = new Line
            {
                X1 = x,
                Y1 = 18,
                X2 = x,
                Y2 = 24,
                Stroke = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
                StrokeThickness = 1
            };
            TimeRulerCanvas.Children.Add(line);

            var text = new TextBlock
            {
                Text = FormatTime(time),
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA))
            };
            Canvas.SetLeft(text, x + 2);
            Canvas.SetTop(text, 4);
            TimeRulerCanvas.Children.Add(text);
        }
    }

    private void DrawFrequencyAxis()
    {
        FrequencyAxisCanvas.Children.Clear();

        if (_viewModel == null || !_viewModel.IsAudioLoaded) return;

        double height = FrequencyAxisCanvas.ActualHeight;
        double maxFreq = _viewModel.MaxFrequency;
        double zoomY = _viewModel.ZoomLevelY;
        double offsetY = _viewModel.ScrollOffsetY / 100.0 * maxFreq;

        double visibleFreqRange = maxFreq / zoomY;
        double interval = CalculateFrequencyInterval(visibleFreqRange, height);

        double startFreq = Math.Floor(offsetY / interval) * interval;
        double endFreq = offsetY + visibleFreqRange;

        for (double freq = startFreq; freq <= endFreq; freq += interval)
        {
            double y = height - (freq - offsetY) / visibleFreqRange * height;
            if (y < 0 || y > height) continue;

            var line = new Line
            {
                X1 = 54,
                Y1 = y,
                X2 = 60,
                Y2 = y,
                Stroke = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
                StrokeThickness = 1
            };
            FrequencyAxisCanvas.Children.Add(line);

            var text = new TextBlock
            {
                Text = FormatFrequency(freq),
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)),
                TextAlignment = TextAlignment.Right,
                Width = 50
            };
            Canvas.SetLeft(text, 2);
            Canvas.SetTop(text, y - 6);
            FrequencyAxisCanvas.Children.Add(text);
        }
    }

    private static double CalculateTimeInterval(double visibleDuration, double width)
    {
        double targetPixelsPerMark = 80;
        double targetMarks = width / targetPixelsPerMark;
        double rawInterval = visibleDuration / targetMarks;

        double[] intervals = { 0.001, 0.002, 0.005, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5, 1, 2, 5, 10, 30, 60 };
        foreach (double interval in intervals)
        {
            if (interval >= rawInterval) return interval;
        }
        return 60;
    }

    private static double CalculateFrequencyInterval(double visibleRange, double height)
    {
        double targetPixelsPerMark = 40;
        double targetMarks = height / targetPixelsPerMark;
        double rawInterval = visibleRange / targetMarks;

        double[] intervals = { 10, 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000 };
        foreach (double interval in intervals)
        {
            if (interval >= rawInterval) return interval;
        }
        return 20000;
    }

    private static string FormatTime(double seconds)
    {
        if (seconds < 1)
            return $"{seconds * 1000:F0}ms";
        if (seconds < 60)
            return $"{seconds:F2}s";
        int minutes = (int)(seconds / 60);
        double secs = seconds % 60;
        return $"{minutes}:{secs:00.0}";
    }

    private static string FormatFrequency(double freq)
    {
        if (freq >= 1000)
            return $"{freq / 1000:F1}k";
        return $"{freq:F0}";
    }

    #endregion

    #region Selection Handling

    private void SelectionCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel == null || !_viewModel.IsAudioLoaded) return;

        _selectionStart = e.GetPosition(SelectionCanvas);
        SelectionCanvas.CaptureMouse();

        switch (_viewModel.SelectedTool)
        {
            case SpectralSelectionTool.Rectangle:
                _isSelecting = true;
                UpdateSelectionRectangle(_selectionStart, _selectionStart);
                SelectionRect.Visibility = Visibility.Visible;
                break;

            case SpectralSelectionTool.Lasso:
                _isSelecting = true;
                _viewModel.LassoPoints.Clear();
                AddLassoPoint(_selectionStart);
                LassoPath.Visibility = Visibility.Visible;
                break;

            case SpectralSelectionTool.MagicWand:
                var (time, freq) = PointToTimeFreq(_selectionStart);
                _viewModel.MagicWandSelect(time, freq);
                break;

            case SpectralSelectionTool.Paintbrush:
                _isPainting = true;
                _lastPaintPoint = _selectionStart;
                PaintAtPoint(_selectionStart);
                break;
        }
    }

    private void SelectionCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_viewModel == null) return;

        Point currentPoint = e.GetPosition(SelectionCanvas);

        if (_viewModel.SelectedTool == SpectralSelectionTool.Paintbrush)
        {
            UpdatePaintbrushCursor(currentPoint);
        }

        if (_isSelecting)
        {
            switch (_viewModel.SelectedTool)
            {
                case SpectralSelectionTool.Rectangle:
                    UpdateSelectionRectangle(_selectionStart, currentPoint);
                    break;

                case SpectralSelectionTool.Lasso:
                    AddLassoPoint(currentPoint);
                    UpdateLassoPath();
                    break;
            }
        }
        else if (_isPainting && e.LeftButton == MouseButtonState.Pressed)
        {
            double distance = (currentPoint - _lastPaintPoint).Length;
            if (distance >= 5)
            {
                PaintAtPoint(currentPoint);
                _lastPaintPoint = currentPoint;
            }
        }
    }

    private void SelectionCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel == null) return;

        SelectionCanvas.ReleaseMouseCapture();

        if (_isSelecting)
        {
            _isSelecting = false;

            switch (_viewModel.SelectedTool)
            {
                case SpectralSelectionTool.Rectangle:
                    Point endPoint = e.GetPosition(SelectionCanvas);
                    FinalizeRectangleSelection(_selectionStart, endPoint);
                    break;

                case SpectralSelectionTool.Lasso:
                    _viewModel.CompleteLassoSelection();
                    LassoPath.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        if (_isPainting)
        {
            _isPainting = false;
        }
    }

    private void SelectionCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_viewModel == null) return;

        bool ctrlPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
        bool shiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

        if (ctrlPressed)
        {
            if (shiftPressed)
            {
                if (e.Delta > 0)
                    _viewModel.ZoomInYCommand.Execute(null);
                else
                    _viewModel.ZoomOutYCommand.Execute(null);
            }
            else
            {
                if (e.Delta > 0)
                    _viewModel.ZoomInXCommand.Execute(null);
                else
                    _viewModel.ZoomOutXCommand.Execute(null);
            }

            UpdateSpectrogram();
            DrawTimeRuler();
            DrawFrequencyAxis();
            e.Handled = true;
        }
    }

    private void UpdateSelectionRectangle(Point start, Point end)
    {
        double x = Math.Min(start.X, end.X);
        double y = Math.Min(start.Y, end.Y);
        double width = Math.Abs(end.X - start.X);
        double height = Math.Abs(end.Y - start.Y);

        Canvas.SetLeft(SelectionRect, x);
        Canvas.SetTop(SelectionRect, y);
        SelectionRect.Width = Math.Max(1, width);
        SelectionRect.Height = Math.Max(1, height);
    }

    private void FinalizeRectangleSelection(Point start, Point end)
    {
        if (_viewModel == null) return;

        var (startTime, startFreq) = PointToTimeFreq(start);
        var (endTime, endFreq) = PointToTimeFreq(end);

        _viewModel.SetSelection(
            Math.Min(startTime, endTime),
            Math.Max(startTime, endTime),
            Math.Min(startFreq, endFreq),
            Math.Max(startFreq, endFreq));
    }

    private void AddLassoPoint(Point point)
    {
        if (_viewModel == null) return;

        var (time, freq) = PointToTimeFreq(point);
        _viewModel.AddLassoPoint(time, freq);
    }

    private void UpdateLassoPath()
    {
        if (_viewModel == null || _viewModel.LassoPoints.Count < 2) return;

        var geometry = new PathGeometry();
        var figure = new PathFigure();

        var firstPoint = _viewModel.LassoPoints[0];
        figure.StartPoint = TimeFreqToPoint(firstPoint.Time, firstPoint.Frequency);

        for (int i = 1; i < _viewModel.LassoPoints.Count; i++)
        {
            var p = _viewModel.LassoPoints[i];
            figure.Segments.Add(new LineSegment(TimeFreqToPoint(p.Time, p.Frequency), true));
        }

        figure.IsClosed = false;
        geometry.Figures.Add(figure);
        LassoPath.Data = geometry;
    }

    private void UpdateSelectionVisual()
    {
        if (_viewModel == null || !_viewModel.HasValidSelection)
        {
            SelectionRect.Visibility = Visibility.Collapsed;
            return;
        }

        Point topLeft = TimeFreqToPoint(_viewModel.SelectionStartTime, _viewModel.SelectionMaxFrequency);
        Point bottomRight = TimeFreqToPoint(_viewModel.SelectionEndTime, _viewModel.SelectionMinFrequency);

        double x = Math.Min(topLeft.X, bottomRight.X);
        double y = Math.Min(topLeft.Y, bottomRight.Y);
        double width = Math.Abs(bottomRight.X - topLeft.X);
        double height = Math.Abs(bottomRight.Y - topLeft.Y);

        Canvas.SetLeft(SelectionRect, x);
        Canvas.SetTop(SelectionRect, y);
        SelectionRect.Width = Math.Max(1, width);
        SelectionRect.Height = Math.Max(1, height);
        SelectionRect.Visibility = Visibility.Visible;
    }

    private void UpdatePaintbrushCursor(Point position)
    {
        if (_viewModel == null) return;

        double canvasWidth = SelectionCanvas.ActualWidth;
        double canvasHeight = SelectionCanvas.ActualHeight;

        if (canvasWidth <= 0 || canvasHeight <= 0) return;

        double freqRange = _viewModel.MaxFrequency / _viewModel.ZoomLevelY;
        double brushSizePixels = _viewModel.PaintbrushSize / freqRange * canvasHeight;

        PaintbrushCursor.Width = brushSizePixels;
        PaintbrushCursor.Height = brushSizePixels;
        Canvas.SetLeft(PaintbrushCursor, position.X - brushSizePixels / 2);
        Canvas.SetTop(PaintbrushCursor, position.Y - brushSizePixels / 2);
        PaintbrushCursor.Visibility = Visibility.Visible;
    }

    private void PaintAtPoint(Point point)
    {
        if (_viewModel == null) return;

        var (time, freq) = PointToTimeFreq(point);
        _viewModel.PaintAt(time, freq);
    }

    #endregion

    #region Coordinate Conversion

    private (double time, float frequency) PointToTimeFreq(Point point)
    {
        if (_viewModel == null)
            return (0, 0);

        double canvasWidth = SelectionCanvas.ActualWidth;
        double canvasHeight = SelectionCanvas.ActualHeight;

        if (canvasWidth <= 0 || canvasHeight <= 0)
            return (0, 0);

        double duration = _viewModel.Duration;
        double maxFreq = _viewModel.MaxFrequency;
        double zoomX = _viewModel.ZoomLevelX;
        double zoomY = _viewModel.ZoomLevelY;
        double offsetX = _viewModel.ScrollOffsetX / 100.0 * duration;
        double offsetY = _viewModel.ScrollOffsetY / 100.0 * maxFreq;

        double visibleDuration = duration / zoomX;
        double visibleFreqRange = maxFreq / zoomY;

        double time = point.X / canvasWidth * visibleDuration + offsetX;
        double freq = (canvasHeight - point.Y) / canvasHeight * visibleFreqRange + offsetY;

        time = Math.Clamp(time, 0, duration);
        freq = Math.Clamp(freq, 0, maxFreq);

        return (time, (float)freq);
    }

    private Point TimeFreqToPoint(double time, float frequency)
    {
        if (_viewModel == null)
            return new Point(0, 0);

        double canvasWidth = SelectionCanvas.ActualWidth;
        double canvasHeight = SelectionCanvas.ActualHeight;

        if (canvasWidth <= 0 || canvasHeight <= 0)
            return new Point(0, 0);

        double duration = _viewModel.Duration;
        double maxFreq = _viewModel.MaxFrequency;
        double zoomX = _viewModel.ZoomLevelX;
        double zoomY = _viewModel.ZoomLevelY;
        double offsetX = _viewModel.ScrollOffsetX / 100.0 * duration;
        double offsetY = _viewModel.ScrollOffsetY / 100.0 * maxFreq;

        double visibleDuration = duration / zoomX;
        double visibleFreqRange = maxFreq / zoomY;

        double x = (time - offsetX) / visibleDuration * canvasWidth;
        double y = canvasHeight - (frequency - offsetY) / visibleFreqRange * canvasHeight;

        return new Point(x, y);
    }

    #endregion
}
