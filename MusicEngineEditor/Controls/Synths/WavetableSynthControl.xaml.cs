// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code-behind for the Wavetable Synthesizer Editor control.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MusicEngineEditor.ViewModels.Synths;

namespace MusicEngineEditor.Controls.Synths;

/// <summary>
/// Interaction logic for WavetableSynthControl.xaml.
/// </summary>
public partial class WavetableSynthControl : UserControl
{
    private bool _isDragging;
    private Point _lastMousePosition;

    /// <summary>
    /// Creates a new WavetableSynthControl.
    /// </summary>
    public WavetableSynthControl()
    {
        InitializeComponent();
    }

    private WavetableSynthViewModel? ViewModel => DataContext as WavetableSynthViewModel;

    private void WavetableSynthControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.WaveformDataChanged += OnWaveformDataChanged;
            ViewModel.CurrentWaveformChanged += OnCurrentWaveformChanged;
            ViewModel.UpdateVisualization();
        }
    }

    private void OnWaveformDataChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(RenderWavetable3D);
    }

    private void OnCurrentWaveformChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(RenderCurrentWaveform);
    }

    private void WavetableCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ViewModel != null)
        {
            RenderWavetable3D();
            RenderCurrentWaveform();
        }
    }

    private void WavetableCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Canvas canvas)
        {
            _isDragging = true;
            _lastMousePosition = e.GetPosition(canvas);
            canvas.CaptureMouse();
        }
    }

    private void WavetableCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || ViewModel == null) return;

        if (sender is Canvas canvas)
        {
            var position = e.GetPosition(canvas);

            // Calculate position change for wavetable position control
            var deltaX = position.X - _lastMousePosition.X;
            var canvasWidth = canvas.ActualWidth;

            if (canvasWidth > 0)
            {
                var positionDelta = deltaX / canvasWidth;
                ViewModel.Position = Math.Clamp(ViewModel.Position + (float)positionDelta, 0f, 1f);
            }

            _lastMousePosition = position;
        }
    }

    private void WavetableCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        if (sender is Canvas canvas)
        {
            canvas.ReleaseMouseCapture();
        }
    }

    private void WavetableCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (ViewModel == null) return;

        // Adjust zoom via the VisualizationZoom property
        var zoomFactor = e.Delta > 0 ? 1.1 : 0.9;
        ViewModel.VisualizationZoom = Math.Clamp(ViewModel.VisualizationZoom * zoomFactor, 0.5, 5.0);
        RenderWavetable3D();
    }

    /// <summary>
    /// Renders the 3D stacked waveform visualization.
    /// </summary>
    private void RenderWavetable3D()
    {
        if (ViewModel == null || WavetableCanvas.ActualWidth <= 0 || WavetableCanvas.ActualHeight <= 0)
            return;

        WavetableCanvas.Children.Clear();

        var frames = ViewModel.WaveformFrames;
        if (frames.Count == 0) return;

        double canvasWidth = WavetableCanvas.ActualWidth;
        double canvasHeight = WavetableCanvas.ActualHeight;
        double zoom = ViewModel.VisualizationZoom;
        bool is3D = ViewModel.Is3DViewEnabled;

        // 3D perspective parameters
        double depthOffset = is3D ? canvasHeight * 0.6 / frames.Count : 0;
        double horizontalSkew = is3D ? canvasWidth * 0.15 / frames.Count : 0;
        double baseY = canvasHeight * 0.75;
        double waveHeight = (canvasHeight * 0.25) * zoom;

        // Draw frames from back to front
        for (int frameIndex = frames.Count - 1; frameIndex >= 0; frameIndex--)
        {
            var frame = frames[frameIndex];
            var samples = frame.Samples;
            if (samples.Length == 0) continue;

            // Calculate 3D offset
            double yOffset = baseY - (frames.Count - 1 - frameIndex) * depthOffset;
            double xOffset = (frames.Count - 1 - frameIndex) * horizontalSkew;

            // Calculate alpha for depth fading
            double alpha = is3D ? 0.3 + 0.7 * (1.0 - (double)frameIndex / frames.Count) : 1.0;

            // Create the waveform path
            var pathFigure = new PathFigure();
            bool started = false;

            for (int i = 0; i < samples.Length; i++)
            {
                double x = xOffset + (double)i / (samples.Length - 1) * (canvasWidth - xOffset * 2);
                double y = yOffset - samples[i] * waveHeight;

                if (!started)
                {
                    pathFigure.StartPoint = new Point(x, y);
                    started = true;
                }
                else
                {
                    pathFigure.Segments.Add(new LineSegment(new Point(x, y), true));
                }
            }

            var pathGeometry = new PathGeometry();
            pathGeometry.Figures.Add(pathFigure);

            var color = frame.Color;
            var strokeColor = Color.FromArgb((byte)(alpha * 255), color.R, color.G, color.B);

            var path = new Path
            {
                Data = pathGeometry,
                Stroke = new SolidColorBrush(strokeColor),
                StrokeThickness = 1.0,
                StrokeLineJoin = PenLineJoin.Round
            };

            WavetableCanvas.Children.Add(path);
        }

        // Draw position indicator line
        if (ViewModel.ShowPositionIndicator)
        {
            int currentFrameIndex = (int)(ViewModel.Position * (frames.Count - 1));
            double indicatorY = baseY - (frames.Count - 1 - currentFrameIndex) * depthOffset;

            var indicatorLine = new Line
            {
                X1 = 0,
                Y1 = indicatorY,
                X2 = canvasWidth,
                Y2 = indicatorY - (is3D ? depthOffset * frames.Count * 0.1 : 0),
                Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0x57, 0x22)),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                Opacity = 0.7
            };
            WavetableCanvas.Children.Add(indicatorLine);
        }

        // Draw grid lines
        DrawGridLines(canvasWidth, canvasHeight);
    }

    private void DrawGridLines(double canvasWidth, double canvasHeight)
    {
        var gridBrush = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));

        // Horizontal grid lines
        for (int i = 1; i < 4; i++)
        {
            double y = canvasHeight * i / 4;
            var line = new Line
            {
                X1 = 0,
                Y1 = y,
                X2 = canvasWidth,
                Y2 = y,
                Stroke = gridBrush,
                StrokeThickness = 1
            };
            WavetableCanvas.Children.Add(line);
        }

        // Vertical grid lines
        for (int i = 1; i < 4; i++)
        {
            double x = canvasWidth * i / 4;
            var line = new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = canvasHeight,
                Stroke = gridBrush,
                StrokeThickness = 1
            };
            WavetableCanvas.Children.Add(line);
        }
    }

    /// <summary>
    /// Renders the current waveform at the selected position.
    /// </summary>
    private void RenderCurrentWaveform()
    {
        if (ViewModel == null || CurrentWaveformCanvas.ActualWidth <= 0 || CurrentWaveformCanvas.ActualHeight <= 0)
            return;

        CurrentWaveformCanvas.Children.Clear();

        var samples = ViewModel.CurrentWaveform;
        if (samples == null || samples.Length == 0) return;

        double canvasWidth = CurrentWaveformCanvas.ActualWidth;
        double canvasHeight = CurrentWaveformCanvas.ActualHeight;
        double centerY = canvasHeight / 2;
        double amplitude = canvasHeight * 0.4;

        // Draw center line
        var centerLine = new Line
        {
            X1 = 0,
            Y1 = centerY,
            X2 = canvasWidth,
            Y2 = centerY,
            Stroke = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
            StrokeThickness = 1
        };
        CurrentWaveformCanvas.Children.Add(centerLine);

        // Create the waveform path
        var pathFigure = new PathFigure();
        bool started = false;

        for (int i = 0; i < samples.Length; i++)
        {
            double x = (double)i / (samples.Length - 1) * canvasWidth;
            double y = centerY - samples[i] * amplitude;

            if (!started)
            {
                pathFigure.StartPoint = new Point(x, y);
                started = true;
            }
            else
            {
                pathFigure.Segments.Add(new LineSegment(new Point(x, y), true));
            }
        }

        var pathGeometry = new PathGeometry();
        pathGeometry.Figures.Add(pathFigure);

        // Create gradient for current position waveform
        var gradientBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0)
        };
        gradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0x00, 0xD9, 0xFF), 0.0));
        gradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0x00, 0xFF, 0xB8), 0.5));
        gradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0x00, 0xFF, 0x88), 1.0));

        var path = new Path
        {
            Data = pathGeometry,
            Stroke = gradientBrush,
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round
        };

        CurrentWaveformCanvas.Children.Add(path);

        // Draw position label
        var positionText = new TextBlock
        {
            Text = $"Position: {ViewModel.Position:P0}",
            Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80)),
            FontSize = 10
        };
        Canvas.SetLeft(positionText, 4);
        Canvas.SetTop(positionText, 2);
        CurrentWaveformCanvas.Children.Add(positionText);
    }
}

/// <summary>
/// Converts a boolean to a color brush for status indication.
/// </summary>
public class WavetableBoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return new SolidColorBrush(boolValue
                ? Color.FromRgb(0x00, 0xFF, 0x88)  // Green for playing
                : Color.FromRgb(0x33, 0x33, 0x33)); // Gray for idle
        }
        return new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a boolean to a status text string.
/// </summary>
public class WavetableBoolToStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? "Playing" : "Idle";
        }
        return "Idle";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
