// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code-behind for the Vector Synthesizer Editor control.

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MusicEngineEditor.ViewModels.Synths;

namespace MusicEngineEditor.Controls.Synths;

/// <summary>
/// Interaction logic for VectorSynthControl.xaml.
/// </summary>
public partial class VectorSynthControl : UserControl
{
    private bool _isDraggingXY;
    private bool _isDraggingPath;
    private int _selectedPathPointIndex = -1;

    // Colors for corner displays
    private static readonly SolidColorBrush CornerABrushStatic = new(Color.FromRgb(0x00, 0xD9, 0xFF));
    private static readonly SolidColorBrush CornerBBrushStatic = new(Color.FromRgb(0xFF, 0x6B, 0x6B));
    private static readonly SolidColorBrush CornerCBrushStatic = new(Color.FromRgb(0x00, 0xFF, 0x88));
    private static readonly SolidColorBrush CornerDBrushStatic = new(Color.FromRgb(0xFF, 0xA5, 0x00));
    private static readonly SolidColorBrush AccentBrushStatic = new(Color.FromRgb(0x00, 0xD9, 0xFF));

    /// <summary>
    /// Creates a new VectorSynthControl.
    /// </summary>
    public VectorSynthControl()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
        SizeChanged += OnSizeChanged;
    }

    private VectorSynthViewModel? ViewModel => DataContext as VectorSynthViewModel;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        InitializeWaveformCombo();
        DrawGridLines();
        UpdatePositionIndicator();
        UpdateMixDisplay();
        UpdateOscillatorDisplays();
        DrawPathOnCanvas();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is VectorSynthViewModel oldVm)
        {
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
            oldVm.VectorPositionChanged -= OnVectorPositionChanged;
        }

        if (e.NewValue is VectorSynthViewModel newVm)
        {
            newVm.PropertyChanged += OnViewModelPropertyChanged;
            newVm.VectorPositionChanged += OnVectorPositionChanged;
            UpdateOscillatorDisplays();
            UpdatePositionIndicator();
            UpdateMixDisplay();
            DrawPathOnCanvas();
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawGridLines();
        UpdatePositionIndicator();
        DrawPathOnCanvas();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(VectorSynthViewModel.VectorX):
            case nameof(VectorSynthViewModel.VectorY):
                UpdatePositionIndicator();
                UpdateMixDisplay();
                break;
            case nameof(VectorSynthViewModel.OscillatorA):
            case nameof(VectorSynthViewModel.OscillatorB):
            case nameof(VectorSynthViewModel.OscillatorC):
            case nameof(VectorSynthViewModel.OscillatorD):
                UpdateOscillatorDisplays();
                break;
            case nameof(VectorSynthViewModel.SelectedOscillator):
                UpdateSelectedOscillatorUI();
                break;
            case nameof(VectorSynthViewModel.PathPoints):
            case nameof(VectorSynthViewModel.VectorEnvelopeTime):
                DrawPathOnCanvas();
                break;
            case nameof(VectorSynthViewModel.IsRecordingPath):
                UpdateRecordButtonState();
                break;
        }
    }

    private void OnVectorPositionChanged(object? sender, (float X, float Y) position)
    {
        Dispatcher.Invoke(() =>
        {
            UpdatePositionIndicator();
            UpdateMixDisplay();
        });
    }

    private void InitializeWaveformCombo()
    {
        if (WaveformCombo != null)
        {
            WaveformCombo.Items.Clear();
            WaveformCombo.Items.Add("Sine");
            WaveformCombo.Items.Add("Saw");
            WaveformCombo.Items.Add("Square");
            WaveformCombo.Items.Add("Triangle");
            WaveformCombo.Items.Add("Noise");
        }
    }

    #region XY Pad Handlers

    private void XYPad_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement pad)
        {
            _isDraggingXY = true;
            pad.CaptureMouse();
            UpdateXYPosition(e.GetPosition(pad), pad);
        }
    }

    private void XYPad_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDraggingXY && sender is FrameworkElement pad)
        {
            UpdateXYPosition(e.GetPosition(pad), pad);
        }
    }

    private void XYPad_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingXY = false;
        if (sender is FrameworkElement pad)
        {
            pad.ReleaseMouseCapture();
        }
    }

    private void XYPad_MouseLeave(object sender, MouseEventArgs e)
    {
        // Don't stop dragging on leave, allow continued drag with capture
    }

    private void UpdateXYPosition(Point position, FrameworkElement pad)
    {
        if (ViewModel == null) return;

        var x = Math.Clamp(position.X / pad.ActualWidth, 0, 1);
        var y = Math.Clamp(1.0 - position.Y / pad.ActualHeight, 0, 1); // Invert Y (0 is top)

        ViewModel.VectorX = (float)x;
        ViewModel.VectorY = (float)y;
    }

    private void UpdatePositionIndicator()
    {
        if (ViewModel == null || XYPadCanvas == null || PositionIndicator == null) return;
        if (XYPadCanvas.ActualWidth <= 0 || XYPadCanvas.ActualHeight <= 0) return;

        double x = ViewModel.VectorX * XYPadCanvas.ActualWidth;
        double y = (1.0 - ViewModel.VectorY) * XYPadCanvas.ActualHeight; // Invert Y

        // Center the indicator on the position
        Canvas.SetLeft(PositionIndicator, x - PositionIndicator.Width / 2);
        Canvas.SetTop(PositionIndicator, y - PositionIndicator.Height / 2);
    }

    private void UpdateMixDisplay()
    {
        if (ViewModel == null) return;

        var (a, b, c, d) = ViewModel.GetMixGains();

        MixAText.Text = $"{a * 100:F0}%";
        MixBText.Text = $"{b * 100:F0}%";
        MixCText.Text = $"{c * 100:F0}%";
        MixDText.Text = $"{d * 100:F0}%";
    }

    private void DrawGridLines()
    {
        if (GridLinesCanvas == null || XYPadCanvas == null) return;
        if (XYPadCanvas.ActualWidth <= 0 || XYPadCanvas.ActualHeight <= 0) return;

        GridLinesCanvas.Children.Clear();

        double width = XYPadCanvas.ActualWidth;
        double height = XYPadCanvas.ActualHeight;

        var gridBrush = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128));

        // Draw grid lines (4x4 grid)
        for (int i = 1; i < 4; i++)
        {
            double x = width * i / 4;
            double y = height * i / 4;

            // Vertical line
            var vLine = new Line
            {
                X1 = x, Y1 = 0, X2 = x, Y2 = height,
                Stroke = gridBrush, StrokeThickness = 1
            };
            GridLinesCanvas.Children.Add(vLine);

            // Horizontal line
            var hLine = new Line
            {
                X1 = 0, Y1 = y, X2 = width, Y2 = y,
                Stroke = gridBrush, StrokeThickness = 1
            };
            GridLinesCanvas.Children.Add(hLine);
        }

        // Draw center crosshair
        var centerBrush = new SolidColorBrush(Color.FromArgb(80, 0, 217, 255));
        var centerX = width / 2;
        var centerY = height / 2;

        var centerH = new Line
        {
            X1 = centerX - 10, Y1 = centerY, X2 = centerX + 10, Y2 = centerY,
            Stroke = centerBrush, StrokeThickness = 1
        };
        GridLinesCanvas.Children.Add(centerH);

        var centerV = new Line
        {
            X1 = centerX, Y1 = centerY - 10, X2 = centerX, Y2 = centerY + 10,
            Stroke = centerBrush, StrokeThickness = 1
        };
        GridLinesCanvas.Children.Add(centerV);
    }

    #endregion

    #region Path Editor

    private void PathEditor_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Canvas canvas)
        {
            _isDraggingPath = true;
            canvas.CaptureMouse();
            // Future: implement point selection/addition
        }
    }

    private void PathEditor_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDraggingPath && sender is Canvas canvas && _selectedPathPointIndex >= 0)
        {
            // Future: implement point dragging
        }
    }

    private void PathEditor_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingPath = false;
        _selectedPathPointIndex = -1;
        if (sender is Canvas canvas)
        {
            canvas.ReleaseMouseCapture();
        }
    }

    private void DrawPathOnCanvas()
    {
        if (PathEditorCanvas == null || ViewModel == null) return;
        if (PathEditorCanvas.ActualWidth <= 0 || PathEditorCanvas.ActualHeight <= 0) return;

        PathEditorCanvas.Children.Clear();

        double width = PathEditorCanvas.ActualWidth;
        double height = PathEditorCanvas.ActualHeight;
        double envelopeTime = ViewModel.VectorEnvelopeTime;

        if (envelopeTime <= 0) return;

        var pathPoints = ViewModel.PathPoints;
        if (pathPoints.Count < 2) return;

        // Draw X path (cyan)
        var xPath = new Polyline
        {
            Stroke = AccentBrushStatic,
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round
        };

        // Draw Y path (green)
        var yPath = new Polyline
        {
            Stroke = CornerCBrushStatic,
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round
        };

        foreach (var point in pathPoints)
        {
            double px = (point.Time / envelopeTime) * width;
            double pyX = (1.0 - point.X) * height; // X value shown as top-to-bottom
            double pyY = (1.0 - point.Y) * height; // Y value shown as top-to-bottom

            xPath.Points.Add(new Point(px, pyX));
            yPath.Points.Add(new Point(px, pyY));
        }

        PathEditorCanvas.Children.Add(xPath);
        PathEditorCanvas.Children.Add(yPath);

        // Draw points
        foreach (var point in pathPoints)
        {
            double px = (point.Time / envelopeTime) * width;
            double pyX = (1.0 - point.X) * height;
            double pyY = (1.0 - point.Y) * height;

            // X point
            var xEllipse = new Ellipse
            {
                Width = 6, Height = 6,
                Fill = AccentBrushStatic,
                Stroke = Brushes.White,
                StrokeThickness = 1
            };
            Canvas.SetLeft(xEllipse, px - 3);
            Canvas.SetTop(xEllipse, pyX - 3);
            PathEditorCanvas.Children.Add(xEllipse);

            // Y point
            var yEllipse = new Ellipse
            {
                Width = 6, Height = 6,
                Fill = CornerCBrushStatic,
                Stroke = Brushes.White,
                StrokeThickness = 1
            };
            Canvas.SetLeft(yEllipse, px - 3);
            Canvas.SetTop(yEllipse, pyY - 3);
            PathEditorCanvas.Children.Add(yEllipse);
        }
    }

    #endregion

    #region Control Handlers

    private void RecordPath_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            if (ViewModel.IsRecordingPath)
            {
                ViewModel.StopRecordingPathCommand.Execute(null);
            }
            else
            {
                ViewModel.StartRecordingPathCommand.Execute(null);
            }
        }
    }

    private void UpdateRecordButtonState()
    {
        if (ViewModel == null || RecordPathButton == null) return;

        RecordPathButton.Content = ViewModel.IsRecordingPath ? "Stop" : "Record";
    }

    private void OscBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is string corner && ViewModel != null)
        {
            ViewModel.SelectOscillatorCommand.Execute(corner);
            UpdateSelectedOscillatorBorders(corner);

            // Show the oscillator parameters panel
            if (OscillatorParamsPanel != null)
            {
                OscillatorParamsPanel.Visibility = Visibility.Visible;
            }
        }
    }

    private void UpdateSelectedOscillatorBorders(string selectedCorner)
    {
        // Reset all borders
        OscABorder.BorderThickness = new Thickness(1);
        OscBBorder.BorderThickness = new Thickness(1);
        OscCBorder.BorderThickness = new Thickness(1);
        OscDBorder.BorderThickness = new Thickness(1);

        // Highlight selected
        switch (selectedCorner.ToUpperInvariant())
        {
            case "A":
                OscABorder.BorderThickness = new Thickness(2);
                break;
            case "B":
                OscBBorder.BorderThickness = new Thickness(2);
                break;
            case "C":
                OscCBorder.BorderThickness = new Thickness(2);
                break;
            case "D":
                OscDBorder.BorderThickness = new Thickness(2);
                break;
        }
    }

    private void OscButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string corner && ViewModel != null)
        {
            ViewModel.SelectOscillatorCommand.Execute(corner);
        }
    }

    private void UpdateSelectedOscillatorUI()
    {
        if (ViewModel?.SelectedOscillator == null) return;

        var osc = ViewModel.SelectedOscillator;

        // Update the selected label and panel visibility
        if (SelectedOscLabel != null)
        {
            SelectedOscLabel.Text = $"OSCILLATOR {osc.Label}";

            // Update label color based on corner
            SelectedOscLabel.Foreground = osc.Label switch
            {
                "A" => CornerABrushStatic,
                "B" => CornerBBrushStatic,
                "C" => CornerCBrushStatic,
                "D" => CornerDBrushStatic,
                _ => AccentBrushStatic
            };
        }

        if (OscillatorParamsPanel != null)
        {
            OscillatorParamsPanel.Visibility = Visibility.Visible;
        }

        // Update waveform combo
        if (WaveformCombo != null)
        {
            WaveformCombo.SelectedIndex = (int)osc.Waveform;
        }

        // Update sliders
        if (DetuneSlider != null)
        {
            DetuneSlider.Value = osc.Detune;
            DetuneValue.Text = $"{osc.Detune:F0}ct";
        }

        if (OctaveSlider != null)
        {
            OctaveSlider.Value = osc.Octave;
            OctaveValue.Text = $"{osc.Octave:+0;-0;0}";
        }

        if (LevelSlider != null)
        {
            LevelSlider.Value = osc.Level;
            LevelValue.Text = $"{osc.Level * 100:F0}%";
        }

        UpdateSelectedOscillatorBorders(osc.Label);
    }

    private void UpdateOscillatorDisplays()
    {
        if (ViewModel == null) return;

        // Update Oscillator A display
        if (ViewModel.OscillatorA != null)
        {
            OscAWaveformText.Text = ViewModel.OscillatorA.Waveform.ToString();
            OscAOctaveText.Text = $"Oct:{ViewModel.OscillatorA.Octave:+0;-0;0}";
            OscALevelText.Text = $"{ViewModel.OscillatorA.Level * 100:F0}%";
        }

        // Update Oscillator B display
        if (ViewModel.OscillatorB != null)
        {
            OscBWaveformText.Text = ViewModel.OscillatorB.Waveform.ToString();
            OscBOctaveText.Text = $"Oct:{ViewModel.OscillatorB.Octave:+0;-0;0}";
            OscBLevelText.Text = $"{ViewModel.OscillatorB.Level * 100:F0}%";
        }

        // Update Oscillator C display
        if (ViewModel.OscillatorC != null)
        {
            OscCWaveformText.Text = ViewModel.OscillatorC.Waveform.ToString();
            OscCOctaveText.Text = $"Oct:{ViewModel.OscillatorC.Octave:+0;-0;0}";
            OscCLevelText.Text = $"{ViewModel.OscillatorC.Level * 100:F0}%";
        }

        // Update Oscillator D display
        if (ViewModel.OscillatorD != null)
        {
            OscDWaveformText.Text = ViewModel.OscillatorD.Waveform.ToString();
            OscDOctaveText.Text = $"Oct:{ViewModel.OscillatorD.Octave:+0;-0;0}";
            OscDLevelText.Text = $"{ViewModel.OscillatorD.Level * 100:F0}%";
        }
    }

    private void WaveformCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel?.SelectedOscillator == null || WaveformCombo == null) return;
        if (WaveformCombo.SelectedIndex < 0) return;

        ViewModel.SelectedOscillator.Waveform = (MusicEngine.Core.VectorWaveform)WaveformCombo.SelectedIndex;
        UpdateOscillatorDisplays();
    }

    private void DetuneSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ViewModel?.SelectedOscillator != null)
        {
            ViewModel.SelectedOscillator.Detune = (float)e.NewValue;
            if (DetuneValue != null)
            {
                DetuneValue.Text = $"{e.NewValue:F0}ct";
            }
            UpdateOscillatorDisplays();
        }
    }

    private void OctaveSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ViewModel?.SelectedOscillator != null)
        {
            ViewModel.SelectedOscillator.Octave = (int)e.NewValue;
            if (OctaveValue != null)
            {
                OctaveValue.Text = $"{(int)e.NewValue:+0;-0;0}";
            }
            UpdateOscillatorDisplays();
        }
    }

    private void LevelSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ViewModel?.SelectedOscillator != null)
        {
            ViewModel.SelectedOscillator.Level = (float)e.NewValue;
            if (LevelValue != null)
            {
                LevelValue.Text = $"{e.NewValue * 100:F0}%";
            }
            UpdateOscillatorDisplays();
        }
    }

    #endregion
}
