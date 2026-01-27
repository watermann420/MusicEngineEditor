// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code-behind for the Granular Synthesizer Editor control.

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
/// Interaction logic for GranularSynthControl.xaml.
/// </summary>
public partial class GranularSynthControl : UserControl
{
    private bool _isDraggingPosition;

    /// <summary>
    /// Creates a new GranularSynthControl.
    /// </summary>
    public GranularSynthControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private GranularSynthViewModel? ViewModel => DataContext as GranularSynthViewModel;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is GranularSynthViewModel oldVm)
        {
            oldVm.WaveformChanged -= OnWaveformChanged;
            oldVm.ParameterChanged -= OnParameterChanged;
        }

        if (e.NewValue is GranularSynthViewModel newVm)
        {
            newVm.WaveformChanged += OnWaveformChanged;
            newVm.ParameterChanged += OnParameterChanged;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateWaveformDisplay();
        UpdatePositionIndicator();
        UpdatePositionRangeIndicator();
        UpdateEnvelopePreview();
        UpdateCenterLine();
    }

    private void OnWaveformChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateWaveformDisplay();
            UpdatePositionIndicator();
            UpdatePositionRangeIndicator();
        });
    }

    private void OnParameterChanged(object? sender, string parameterName)
    {
        Dispatcher.Invoke(() =>
        {
            switch (parameterName)
            {
                case nameof(GranularSynthViewModel.Position):
                    UpdatePositionIndicator();
                    UpdatePositionRangeIndicator();
                    break;
                case nameof(GranularSynthViewModel.PositionRandom):
                    UpdatePositionRangeIndicator();
                    break;
                case nameof(GranularSynthViewModel.SelectedEnvelope):
                    UpdateEnvelopePreview();
                    break;
            }
        });
    }

    #region Waveform Container Handlers

    private void WaveformContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel == null || !ViewModel.HasLoadedSample) return;

        _isDraggingPosition = true;
        WaveformContainer.CaptureMouse();
        UpdatePlaybackPosition(e.GetPosition(WaveformContainer));
        e.Handled = true;
    }

    private void WaveformContainer_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDraggingPosition)
        {
            UpdatePlaybackPosition(e.GetPosition(WaveformContainer));
        }
    }

    private void WaveformContainer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingPosition = false;
        WaveformContainer.ReleaseMouseCapture();
    }

    private void WaveformContainer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.WaveformWidth = e.NewSize.Width;
            ViewModel.WaveformHeight = e.NewSize.Height;
        }

        UpdateWaveformDisplay();
        UpdatePositionIndicator();
        UpdatePositionRangeIndicator();
        UpdateCenterLine();
    }

    private void UpdatePlaybackPosition(Point position)
    {
        if (ViewModel == null) return;

        var width = WaveformContainer.ActualWidth;
        if (width > 0)
        {
            var normalizedPosition = Math.Clamp(position.X / width, 0, 1);
            ViewModel.Position = (float)normalizedPosition;
        }
    }

    #endregion

    #region Visualization Updates

    /// <summary>
    /// Updates the waveform display based on source data.
    /// </summary>
    private void UpdateWaveformDisplay()
    {
        WaveformCanvas.Children.Clear();

        var waveform = ViewModel?.SourceWaveform;
        if (waveform == null || waveform.Length == 0) return;

        var width = WaveformCanvas.ActualWidth;
        var height = WaveformCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        var centerY = height / 2;
        var samplesPerPixel = Math.Max(1, waveform.Length / (int)width);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(0, centerY), false, false);

            // Draw top half (max values)
            for (int x = 0; x < (int)width; x++)
            {
                int startSample = x * samplesPerPixel;
                int endSample = Math.Min(startSample + samplesPerPixel, waveform.Length);

                float maxVal = float.MinValue;
                for (int i = startSample; i < endSample; i++)
                {
                    maxVal = Math.Max(maxVal, waveform[i]);
                }

                double yMax = centerY - (maxVal * centerY * 0.9);
                ctx.LineTo(new Point(x, yMax), true, false);
            }

            // Draw bottom half (min values) in reverse
            for (int x = (int)width - 1; x >= 0; x--)
            {
                int startSample = x * samplesPerPixel;
                int endSample = Math.Min(startSample + samplesPerPixel, waveform.Length);

                float minVal = float.MaxValue;
                for (int i = startSample; i < endSample; i++)
                {
                    minVal = Math.Min(minVal, waveform[i]);
                }

                double yMin = centerY - (minVal * centerY * 0.9);
                ctx.LineTo(new Point(x, yMin), true, false);
            }
        }

        geometry.Freeze();

        // Create waveform path with gradient fill
        var path = new Path
        {
            Data = geometry,
            Fill = (Brush)FindResource("WaveformBrush"),
            Stroke = new SolidColorBrush(Color.FromRgb(0x00, 0xD9, 0xFF)),
            StrokeThickness = 0.5,
            Opacity = 0.8
        };

        WaveformCanvas.Children.Add(path);
    }

    /// <summary>
    /// Updates the grain position indicator.
    /// </summary>
    private void UpdatePositionIndicator()
    {
        if (ViewModel == null) return;

        var width = WaveformContainer.ActualWidth;
        var height = WaveformContainer.ActualHeight;

        if (width <= 0 || height <= 0) return;

        double x = ViewModel.Position * width;
        Canvas.SetLeft(PositionIndicator, x - 1.5); // Center the 3px wide indicator
        PositionIndicator.Height = height;
    }

    /// <summary>
    /// Updates the position randomness range indicator.
    /// </summary>
    private void UpdatePositionRangeIndicator()
    {
        if (ViewModel == null) return;

        var width = WaveformContainer.ActualWidth;
        var height = WaveformContainer.ActualHeight;

        if (width <= 0 || height <= 0) return;

        // Calculate the range based on position and position random
        float position = ViewModel.Position;
        float randomRange = ViewModel.PositionRandom;

        double rangeStart = Math.Max(0, position - randomRange) * width;
        double rangeEnd = Math.Min(1, position + randomRange) * width;
        double rangeWidth = rangeEnd - rangeStart;

        Canvas.SetLeft(PositionRangeIndicator, rangeStart);
        PositionRangeIndicator.Width = Math.Max(1, rangeWidth);
        PositionRangeIndicator.Height = height;
    }

    /// <summary>
    /// Updates the center line position.
    /// </summary>
    private void UpdateCenterLine()
    {
        var height = WaveformContainer.ActualHeight;
        if (height > 0)
        {
            CenterLine.Y1 = height / 2;
            CenterLine.Y2 = height / 2;
        }
    }

    /// <summary>
    /// Updates the envelope shape preview canvas.
    /// </summary>
    private void UpdateEnvelopePreview()
    {
        EnvelopePreviewCanvas.Children.Clear();

        if (ViewModel == null) return;

        var width = EnvelopePreviewCanvas.ActualWidth;
        var height = EnvelopePreviewCanvas.ActualHeight;

        if (width <= 0 || height <= 0)
        {
            // Schedule for later when size is known
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (EnvelopePreviewCanvas.ActualWidth > 0)
                {
                    UpdateEnvelopePreview();
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
            return;
        }

        var envelope = ViewModel.SelectedEnvelope;
        var points = new PointCollection();

        // Generate envelope curve points
        int numPoints = (int)width;
        for (int i = 0; i <= numPoints; i++)
        {
            double t = (double)i / numPoints;
            double value = GetEnvelopeValue(envelope, t);

            double x = t * width;
            double y = height - (value * (height - 4)); // 4px padding

            points.Add(new Point(x, y));
        }

        // Add filled area below curve
        var filledPoints = new PointCollection(points);
        filledPoints.Add(new Point(width, height));
        filledPoints.Add(new Point(0, height));

        var polygon = new Polygon
        {
            Points = filledPoints,
            Fill = new SolidColorBrush(Color.FromArgb(0x40, 0x00, 0xD9, 0xFF))
        };

        EnvelopePreviewCanvas.Children.Add(polygon);

        // Create polyline for envelope shape
        var polyline = new Polyline
        {
            Points = points,
            Stroke = new SolidColorBrush(Color.FromRgb(0x00, 0xD9, 0xFF)),
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round
        };

        EnvelopePreviewCanvas.Children.Add(polyline);
    }

    /// <summary>
    /// Gets the envelope value at a given position (0-1).
    /// </summary>
    private static double GetEnvelopeValue(MusicEngine.Core.GrainEnvelope envelope, double position)
    {
        return envelope switch
        {
            MusicEngine.Core.GrainEnvelope.Gaussian =>
                Math.Exp(-18 * Math.Pow(position - 0.5, 2)),

            MusicEngine.Core.GrainEnvelope.Hann =>
                0.5 * (1 - Math.Cos(2 * Math.PI * position)),

            MusicEngine.Core.GrainEnvelope.Trapezoid =>
                position < 0.1 ? position * 10 :
                position > 0.9 ? (1 - position) * 10 : 1,

            MusicEngine.Core.GrainEnvelope.Triangle =>
                position < 0.5 ? position * 2 : (1 - position) * 2,

            MusicEngine.Core.GrainEnvelope.Rectangle => 1,

            _ => 1
        };
    }

    #endregion
}

/// <summary>
/// Converts boolean to inverse visibility for Granular Synth control.
/// </summary>
public class GranularInverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
