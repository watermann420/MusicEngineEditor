// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code-behind for the FM Synthesizer Editor control.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using MusicEngineEditor.ViewModels.Synths;

namespace MusicEngineEditor.Controls.Synths;

/// <summary>
/// Interaction logic for FMSynthControl.xaml.
/// </summary>
public partial class FMSynthControl : UserControl
{
    /// <summary>
    /// Converter for FM algorithm descriptions.
    /// </summary>
    public static AlgorithmDescriptionConverter AlgorithmDescriptionConverter { get; } = new();

    /// <summary>
    /// Converter for Color to Brush.
    /// </summary>
    public static ColorToBrushConverter ColorToBrushConverter { get; } = new();

    /// <summary>
    /// Converter for carrier/modulator text.
    /// </summary>
    public static CarrierTextConverter CarrierTextConverter { get; } = new();

    /// <summary>
    /// Converter for null to visibility.
    /// </summary>
    public static NullToVisibilityConverter NullToVisibilityConverter { get; } = new();

    /// <summary>
    /// Converter for carrier/modulator background color.
    /// </summary>
    public static CarrierBackgroundConverter CarrierBackgroundConverter { get; } = new();

    /// <summary>
    /// Creates a new FMSynthControl.
    /// </summary>
    public FMSynthControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private FMSynthViewModel? ViewModel => DataContext as FMSynthViewModel;

    private void Operator_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is FMOperatorViewModel operatorVm)
        {
            ViewModel?.SelectOperatorCommand.Execute(operatorVm);
        }
    }

    /// <summary>
    /// Called when the control is loaded to draw the initial algorithm diagram.
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        DrawAlgorithmDiagram();

        if (DataContext is FMSynthViewModel vm)
        {
            vm.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is FMSynthViewModel vm)
        {
            vm.PropertyChanged -= ViewModel_PropertyChanged;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FMSynthViewModel.SelectedAlgorithm) ||
            e.PropertyName == nameof(FMSynthViewModel.Operators))
        {
            DrawAlgorithmDiagram();
        }
    }

    /// <summary>
    /// Draws the algorithm diagram showing operator connections.
    /// </summary>
    private void DrawAlgorithmDiagram()
    {
        if (AlgorithmCanvas == null || ViewModel == null) return;

        AlgorithmCanvas.Children.Clear();

        double canvasWidth = AlgorithmCanvas.ActualWidth > 0 ? AlgorithmCanvas.ActualWidth : 260;
        double canvasHeight = AlgorithmCanvas.ActualHeight > 0 ? AlgorithmCanvas.ActualHeight : 200;

        // Operator positions in a 2x3 grid pattern
        var opPositions = new System.Windows.Point[]
        {
            new(canvasWidth * 0.17, canvasHeight * 0.7),  // OP1 (bottom-left)
            new(canvasWidth * 0.5, canvasHeight * 0.7),   // OP2 (bottom-center)
            new(canvasWidth * 0.83, canvasHeight * 0.7),  // OP3 (bottom-right)
            new(canvasWidth * 0.17, canvasHeight * 0.3),  // OP4 (top-left)
            new(canvasWidth * 0.5, canvasHeight * 0.3),   // OP5 (top-center)
            new(canvasWidth * 0.83, canvasHeight * 0.3),  // OP6 (top-right)
        };

        // Output position (bottom center)
        var outputPos = new System.Windows.Point(canvasWidth * 0.5, canvasHeight * 0.95);

        // Operator colors matching the theme
        var opColors = new System.Windows.Media.Color[]
        {
            System.Windows.Media.Color.FromRgb(0xFF, 0x6B, 0x6B),  // Red
            System.Windows.Media.Color.FromRgb(0xFF, 0xA5, 0x00),  // Orange
            System.Windows.Media.Color.FromRgb(0xFF, 0xD9, 0x3D),  // Yellow
            System.Windows.Media.Color.FromRgb(0x6B, 0xFF, 0x6B),  // Green
            System.Windows.Media.Color.FromRgb(0x00, 0xD9, 0xFF),  // Cyan
            System.Windows.Media.Color.FromRgb(0xBB, 0x6B, 0xFF),  // Purple
        };

        // Draw connections first (behind operators)
        foreach (var conn in ViewModel.AlgorithmConnections)
        {
            if (conn.FromOperator < 0 || conn.FromOperator >= 6) continue;

            var fromPos = opPositions[conn.FromOperator];
            System.Windows.Point toPos;

            if (conn.ToOperator == -1)
            {
                // Connection to output
                toPos = outputPos;
            }
            else if (conn.ToOperator >= 0 && conn.ToOperator < 6)
            {
                toPos = opPositions[conn.ToOperator];
            }
            else
            {
                continue;
            }

            // Draw connection line
            var line = new System.Windows.Shapes.Line
            {
                X1 = fromPos.X,
                Y1 = fromPos.Y,
                X2 = toPos.X,
                Y2 = toPos.Y,
                Stroke = conn.IsFeedback
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x88, 0x00))
                    : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x60, 0x60, 0x60)),
                StrokeThickness = conn.IsFeedback ? 2 : 1.5,
                StrokeDashArray = conn.IsFeedback ? new System.Windows.Media.DoubleCollection { 4, 2 } : null
            };
            AlgorithmCanvas.Children.Add(line);

            // Draw arrowhead for non-output connections
            if (conn.ToOperator != -1)
            {
                DrawArrowhead(fromPos, toPos, line.Stroke);
            }
        }

        // Draw operators
        for (int i = 0; i < 6; i++)
        {
            var pos = opPositions[i];
            var op = ViewModel.Operators.Count > i ? ViewModel.Operators[i] : null;
            bool isCarrier = op?.IsCarrier ?? false;
            bool isEnabled = op?.Level > 0;

            // Operator circle
            double radius = 18;
            var ellipse = new System.Windows.Shapes.Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Fill = isEnabled
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x40, opColors[i].R, opColors[i].G, opColors[i].B))
                    : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x20, 0x20, 0x20)),
                Stroke = new System.Windows.Media.SolidColorBrush(opColors[i]),
                StrokeThickness = isCarrier ? 3 : 2
            };
            System.Windows.Controls.Canvas.SetLeft(ellipse, pos.X - radius);
            System.Windows.Controls.Canvas.SetTop(ellipse, pos.Y - radius);
            AlgorithmCanvas.Children.Add(ellipse);

            // Carrier indicator (inner ring)
            if (isCarrier)
            {
                var carrierRing = new System.Windows.Shapes.Ellipse
                {
                    Width = radius * 2 + 8,
                    Height = radius * 2 + 8,
                    Fill = System.Windows.Media.Brushes.Transparent,
                    Stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0xFF, 0x88)),
                    StrokeThickness = 1.5,
                    StrokeDashArray = new System.Windows.Media.DoubleCollection { 3, 2 }
                };
                System.Windows.Controls.Canvas.SetLeft(carrierRing, pos.X - radius - 4);
                System.Windows.Controls.Canvas.SetTop(carrierRing, pos.Y - radius - 4);
                AlgorithmCanvas.Children.Add(carrierRing);
            }

            // Operator number text
            var text = new System.Windows.Controls.TextBlock
            {
                Text = (i + 1).ToString(),
                Foreground = new System.Windows.Media.SolidColorBrush(opColors[i]),
                FontWeight = System.Windows.FontWeights.Bold,
                FontSize = 14,
                TextAlignment = System.Windows.TextAlignment.Center
            };
            text.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            System.Windows.Controls.Canvas.SetLeft(text, pos.X - text.DesiredSize.Width / 2);
            System.Windows.Controls.Canvas.SetTop(text, pos.Y - text.DesiredSize.Height / 2);
            AlgorithmCanvas.Children.Add(text);

            // Level indicator bar below operator
            if (op != null)
            {
                double barWidth = 30;
                double barHeight = 4;
                double barY = pos.Y + radius + 4;

                // Background bar
                var bgBar = new System.Windows.Shapes.Rectangle
                {
                    Width = barWidth,
                    Height = barHeight,
                    Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x30, 0x30, 0x30)),
                    RadiusX = 2,
                    RadiusY = 2
                };
                System.Windows.Controls.Canvas.SetLeft(bgBar, pos.X - barWidth / 2);
                System.Windows.Controls.Canvas.SetTop(bgBar, barY);
                AlgorithmCanvas.Children.Add(bgBar);

                // Level bar
                var levelBar = new System.Windows.Shapes.Rectangle
                {
                    Width = barWidth * op.Level,
                    Height = barHeight,
                    Fill = new System.Windows.Media.SolidColorBrush(opColors[i]),
                    RadiusX = 2,
                    RadiusY = 2
                };
                System.Windows.Controls.Canvas.SetLeft(levelBar, pos.X - barWidth / 2);
                System.Windows.Controls.Canvas.SetTop(levelBar, barY);
                AlgorithmCanvas.Children.Add(levelBar);
            }
        }

        // Draw output symbol
        var outputEllipse = new System.Windows.Shapes.Ellipse
        {
            Width = 16,
            Height = 16,
            Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0xFF, 0x88)),
            Stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0xAA, 0x55)),
            StrokeThickness = 2
        };
        System.Windows.Controls.Canvas.SetLeft(outputEllipse, outputPos.X - 8);
        System.Windows.Controls.Canvas.SetTop(outputEllipse, outputPos.Y - 8);
        AlgorithmCanvas.Children.Add(outputEllipse);

        // Output label
        var outputText = new System.Windows.Controls.TextBlock
        {
            Text = "OUT",
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x80, 0x80, 0x80)),
            FontSize = 9
        };
        outputText.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        System.Windows.Controls.Canvas.SetLeft(outputText, outputPos.X + 12);
        System.Windows.Controls.Canvas.SetTop(outputText, outputPos.Y - outputText.DesiredSize.Height / 2);
        AlgorithmCanvas.Children.Add(outputText);
    }

    private void DrawArrowhead(System.Windows.Point from, System.Windows.Point to, System.Windows.Media.Brush stroke)
    {
        double headLength = 8;
        double headAngle = Math.PI / 6; // 30 degrees

        double angle = Math.Atan2(to.Y - from.Y, to.X - from.X);

        // Shorten the arrow to not overlap the operator circle
        double shortenBy = 20;
        double distance = Math.Sqrt(Math.Pow(to.X - from.X, 2) + Math.Pow(to.Y - from.Y, 2));
        if (distance < shortenBy * 2) return;

        var arrowTip = new System.Windows.Point(
            to.X - shortenBy * Math.Cos(angle),
            to.Y - shortenBy * Math.Sin(angle)
        );

        var point1 = new System.Windows.Point(
            arrowTip.X - headLength * Math.Cos(angle - headAngle),
            arrowTip.Y - headLength * Math.Sin(angle - headAngle)
        );
        var point2 = new System.Windows.Point(
            arrowTip.X - headLength * Math.Cos(angle + headAngle),
            arrowTip.Y - headLength * Math.Sin(angle + headAngle)
        );

        var arrowHead = new System.Windows.Shapes.Polygon
        {
            Points = new System.Windows.Media.PointCollection { arrowTip, point1, point2 },
            Fill = stroke
        };
        AlgorithmCanvas.Children.Add(arrowHead);
    }

    private void AlgorithmCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawAlgorithmDiagram();
    }
}

/// <summary>
/// Converts Color to SolidColorBrush.
/// </summary>
public class ColorToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is System.Windows.Media.Color color)
        {
            return new System.Windows.Media.SolidColorBrush(color);
        }
        if (value is string colorString)
        {
            try
            {
                var color2 = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorString);
                return new System.Windows.Media.SolidColorBrush(color2);
            }
            catch
            {
                return System.Windows.Media.Brushes.Gray;
            }
        }
        return System.Windows.Media.Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts FM algorithm to description text.
/// </summary>
public class AlgorithmDescriptionConverter : IValueConverter
{
    private static readonly Dictionary<MusicEngine.Core.FMAlgorithm, string> AlgorithmDescriptions = new()
    {
        { MusicEngine.Core.FMAlgorithm.Stack6, "Algorithm 1: 6->5->4->3->2->1 (Classic series stack)" },
        { MusicEngine.Core.FMAlgorithm.Split2_4, "Algorithm 2: (6->5) + (4->3->2->1) (Split 2+4)" },
        { MusicEngine.Core.FMAlgorithm.Split3_3, "Algorithm 3: (6->5->4) + (3->2->1) (Dual 3-op stacks)" },
        { MusicEngine.Core.FMAlgorithm.Triple, "Algorithm 4: (6->5) + (4->3) + (2->1) (Three pairs)" },
        { MusicEngine.Core.FMAlgorithm.ModSplit, "Algorithm 5: 6->5->(4->3->2->1) (Mod split)" },
        { MusicEngine.Core.FMAlgorithm.Split4_2, "Algorithm 6: (6->5->4->3) + (2->1) (Split 4+2)" },
        { MusicEngine.Core.FMAlgorithm.TripleMod, "Algorithm 7: 6->(5+4+3)->2->1 (Triple mod)" },
        { MusicEngine.Core.FMAlgorithm.DualPath, "Algorithm 8: 4->3->2->1, 6->5->1 (Dual path)" },
        { MusicEngine.Core.FMAlgorithm.AllParallel, "Algorithm 9: All parallel (6 carriers, organ-style)" },
        { MusicEngine.Core.FMAlgorithm.DualStack, "Algorithm 10: (6->5->4) + (3->2->1) (Dual stack)" },
        { MusicEngine.Core.FMAlgorithm.StackWithFB, "Algorithm 11: (6->5)->4->3->2->1 (Stack with FB)" },
        { MusicEngine.Core.FMAlgorithm.OneToThree, "Algorithm 12: 6->5->4->(3+2+1) (One to three)" },
        { MusicEngine.Core.FMAlgorithm.TwoToThree, "Algorithm 13: 6->(5+4)->(3+2+1) (Two to three)" },
        { MusicEngine.Core.FMAlgorithm.TwoByTwo, "Algorithm 14: (6+5)->(4+3)->(2+1) (Two by two)" },
        { MusicEngine.Core.FMAlgorithm.ThreePairs, "Algorithm 15: (6->5) + (4->3) + (2->1) (Three pairs)" },
        { MusicEngine.Core.FMAlgorithm.EPiano, "Algorithm 16: Electric Piano (warm tines)" },
        { MusicEngine.Core.FMAlgorithm.Brass, "Algorithm 17: Brass (bright and punchy)" },
        { MusicEngine.Core.FMAlgorithm.Bells, "Algorithm 18: Bells/Chimes (metallic tones)" },
        { MusicEngine.Core.FMAlgorithm.Organ, "Algorithm 19: Organ (drawbar-style)" },
        { MusicEngine.Core.FMAlgorithm.Bass, "Algorithm 20: Bass (solid low-end)" }
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is MusicEngine.Core.FMAlgorithm algorithm && AlgorithmDescriptions.TryGetValue(algorithm, out var desc))
        {
            return desc;
        }
        if (value is int index)
        {
            var alg = (MusicEngine.Core.FMAlgorithm)index;
            if (AlgorithmDescriptions.TryGetValue(alg, out var desc2))
            {
                return desc2;
            }
        }
        return "Select an algorithm";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts IsCarrier boolean to Carrier/Modulator text.
/// </summary>
public class CarrierTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isCarrier)
        {
            return isCarrier ? "Carrier" : "Modulator";
        }
        return "Modulator";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts null to Collapsed, non-null to Visible.
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value != null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts IsCarrier boolean to background brush.
/// </summary>
public class CarrierBackgroundConverter : IValueConverter
{
    private static readonly System.Windows.Media.SolidColorBrush CarrierBrush =
        new(System.Windows.Media.Color.FromRgb(0x1A, 0x3A, 0x1A)); // Dark green for carriers
    private static readonly System.Windows.Media.SolidColorBrush ModulatorBrush =
        new(System.Windows.Media.Color.FromRgb(0x1A, 0x1A, 0x2A)); // Dark blue for modulators

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isCarrier)
        {
            return isCarrier ? CarrierBrush : ModulatorBrush;
        }
        return ModulatorBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
