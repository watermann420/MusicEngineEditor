// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Step Sequencer / Drum Editor control implementation.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MusicEngineEditor.ViewModels;

using ContextMenu = System.Windows.Controls.ContextMenu;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Step Sequencer / Drum Editor control with multi-row drum machine functionality.
/// Supports 16/32/64 step grid, velocity per step, probability, swing, and direction modes.
/// </summary>
public partial class StepSequencerControl : UserControl, IDisposable
{
    #region Private Fields

    private StepSequencerViewModel? _viewModel;
    private readonly Dictionary<(int row, int step), Rectangle> _stepElements = new();
    private readonly Dictionary<(int row, int step), Rectangle> _velocityBars = new();
    private bool _isDragging;
    private bool _dragSetActive;
    private int _lastDraggedRow = -1;
    private int _lastDraggedStep = -1;

    // Layout constants
    private const double RowHeight = 36;
    private const double StepMinWidth = 24;
    private const double StepPadding = 2;
    private const double VelocityBarHeight = 4;

    // Colors
    private static readonly Color BackgroundColor = Color.FromRgb(0x0D, 0x0D, 0x0D);
    private static readonly Color DownbeatColor = Color.FromRgb(0x25, 0x25, 0x25);
    private static readonly Color OffbeatColor = Color.FromRgb(0x1A, 0x1A, 0x1A);
    private static readonly Color BorderColor = Color.FromRgb(0x2A, 0x2A, 0x2A);
    private static readonly Color AccentColor = Color.FromRgb(0x00, 0xD9, 0xFF);
    private static readonly Color SuccessColor = Color.FromRgb(0x00, 0xFF, 0x88);
    private static readonly Color PlayingColor = Color.FromRgb(0x00, 0xFF, 0x88);

    #endregion

    #region Dependency Properties

    /// <summary>
    /// Gets or sets the ViewModel for this control.
    /// </summary>
    public StepSequencerViewModel? ViewModel
    {
        get => (StepSequencerViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(StepSequencerViewModel), typeof(StepSequencerControl),
            new PropertyMetadata(null, OnViewModelChanged));

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new StepSequencerControl.
    /// </summary>
    public StepSequencerControl()
    {
        InitializeComponent();

        // Create default ViewModel if not provided
        _viewModel = new StepSequencerViewModel();
        DataContext = _viewModel;

        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
        Unloaded += OnUnloaded;
    }

    #endregion

    #region Initialization

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        InitializeFromViewModel();
        RefreshGrid();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RefreshGrid();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Dispose();
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StepSequencerControl control)
        {
            control._viewModel = e.NewValue as StepSequencerViewModel;
            control.DataContext = control._viewModel;
            control.InitializeFromViewModel();
            control.RefreshGrid();
        }
    }

    private void InitializeFromViewModel()
    {
        if (_viewModel == null) return;

        // Bind row headers
        RowHeadersControl.ItemsSource = _viewModel.Rows;

        // Subscribe to property changes
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        // Subscribe to step triggered events for UI feedback
        _viewModel.StepTriggered += ViewModel_StepTriggered;

        // Initialize UI state
        UpdateLoopControls();
        UpdateStatusText();
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(StepSequencerViewModel.CurrentStep):
                UpdatePlayingStep();
                UpdateCurrentStepText();
                break;
            case nameof(StepSequencerViewModel.StepCount):
                RefreshGrid();
                UpdateLoopControls();
                break;
            case nameof(StepSequencerViewModel.StatusMessage):
                UpdateStatusText();
                break;
            case nameof(StepSequencerViewModel.IsPlaying):
                UpdatePlayButton();
                break;
        }
    }

    private void ViewModel_StepTriggered(object? sender, StepTriggeredEventArgs e)
    {
        // Visual feedback could be added here
    }

    #endregion

    #region Grid Rendering

    /// <summary>
    /// Refreshes the entire step grid.
    /// </summary>
    public void RefreshGrid()
    {
        if (_viewModel == null) return;

        // Clear existing elements
        StepGridCanvas.Children.Clear();
        _stepElements.Clear();
        _velocityBars.Clear();

        // Calculate dimensions
        int stepCount = _viewModel.StepCount;
        int rowCount = _viewModel.Rows.Count;

        double availableWidth = Math.Max(StepGridScrollViewer.ActualWidth - 20, stepCount * StepMinWidth);
        double stepWidth = Math.Max(StepMinWidth, availableWidth / stepCount);

        // Set canvas size
        StepGridCanvas.Width = stepCount * stepWidth;
        StepGridCanvas.Height = rowCount * RowHeight;

        // Draw beat markers at the top
        DrawBeatMarkers(stepCount, stepWidth);

        // Draw steps for each row
        for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            var row = _viewModel.Rows[rowIndex];
            DrawRow(rowIndex, row, stepWidth);
        }
    }

    private void DrawBeatMarkers(int stepCount, double stepWidth)
    {
        // Draw beat number markers
        for (int i = 0; i < stepCount; i++)
        {
            if (i % 4 == 0) // Every beat (assuming 16th notes)
            {
                int beatNumber = (i / 4) + 1;
                var text = new TextBlock
                {
                    Text = beatNumber.ToString(),
                    Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60)),
                    FontSize = 9,
                    TextAlignment = TextAlignment.Center,
                    Width = stepWidth * 4
                };

                Canvas.SetLeft(text, i * stepWidth);
                Canvas.SetTop(text, -14);
                StepGridCanvas.Children.Add(text);
            }
        }
    }

    private void DrawRow(int rowIndex, SequencerRowViewModel row, double stepWidth)
    {
        double y = rowIndex * RowHeight;
        var rowColor = ParseColor(row.Color);

        for (int stepIndex = 0; stepIndex < row.Steps.Count; stepIndex++)
        {
            var step = row.Steps[stepIndex];
            DrawStep(rowIndex, stepIndex, step, stepWidth, y, rowColor);
        }

        // Draw row separator line
        var separator = new Line
        {
            X1 = 0,
            Y1 = y + RowHeight,
            X2 = StepGridCanvas.Width,
            Y2 = y + RowHeight,
            Stroke = new SolidColorBrush(BorderColor),
            StrokeThickness = 1
        };
        StepGridCanvas.Children.Add(separator);
    }

    private void DrawStep(int rowIndex, int stepIndex, SequencerStepViewModel step, double stepWidth, double y, Color rowColor)
    {
        double x = stepIndex * stepWidth;
        double rectWidth = stepWidth - StepPadding * 2;
        double rectHeight = RowHeight - StepPadding * 2 - VelocityBarHeight - 2;

        // Determine background color based on position
        bool isDownbeat = stepIndex % 4 == 0;
        bool isHalfBeat = stepIndex % 2 == 0;
        Color bgColor = isDownbeat ? DownbeatColor : (isHalfBeat ? Color.FromRgb(0x1E, 0x1E, 0x1E) : OffbeatColor);

        // Calculate step color based on state
        Color stepColor;
        if (step.IsActive)
        {
            // Use row color with opacity based on probability if showing probability
            byte alpha = _viewModel?.ShowProbability == true
                ? (byte)(Math.Max(80, step.Probability * 255))
                : (byte)255;
            stepColor = Color.FromArgb(alpha, rowColor.R, rowColor.G, rowColor.B);
        }
        else
        {
            stepColor = bgColor;
        }

        // Create step rectangle
        var rect = new Rectangle
        {
            Width = rectWidth,
            Height = rectHeight,
            Fill = new SolidColorBrush(stepColor),
            Stroke = step.IsPlaying
                ? new SolidColorBrush(PlayingColor)
                : new SolidColorBrush(BorderColor),
            StrokeThickness = step.IsPlaying ? 2 : 1,
            RadiusX = 3,
            RadiusY = 3,
            Tag = (rowIndex, stepIndex),
            Cursor = Cursors.Hand
        };

        Canvas.SetLeft(rect, x + StepPadding);
        Canvas.SetTop(rect, y + StepPadding);
        StepGridCanvas.Children.Add(rect);
        _stepElements[(rowIndex, stepIndex)] = rect;

        // Draw velocity bar if active and showing velocity
        if (step.IsActive && _viewModel?.ShowVelocity == true)
        {
            double velocityWidth = rectWidth * step.NormalizedVelocity;
            var velocityBar = new Rectangle
            {
                Width = velocityWidth,
                Height = VelocityBarHeight,
                Fill = new SolidColorBrush(rowColor),
                RadiusX = 2,
                RadiusY = 2
            };

            Canvas.SetLeft(velocityBar, x + StepPadding);
            Canvas.SetTop(velocityBar, y + RowHeight - StepPadding - VelocityBarHeight);
            StepGridCanvas.Children.Add(velocityBar);
            _velocityBars[(rowIndex, stepIndex)] = velocityBar;
        }

        // Draw accent indicator if step has accent
        if (step.IsActive && step.HasAccent)
        {
            var accent = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = new SolidColorBrush(Colors.White)
            };

            Canvas.SetLeft(accent, x + StepPadding + rectWidth - 10);
            Canvas.SetTop(accent, y + StepPadding + 4);
            StepGridCanvas.Children.Add(accent);
        }

        // Draw retrigger indicator if step has retrigger > 1
        if (step.IsActive && step.Retrigger > 1)
        {
            var retriggerText = new TextBlock
            {
                Text = step.Retrigger.ToString(),
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 8,
                FontWeight = FontWeights.Bold
            };

            Canvas.SetLeft(retriggerText, x + StepPadding + 4);
            Canvas.SetTop(retriggerText, y + StepPadding + 4);
            StepGridCanvas.Children.Add(retriggerText);
        }
    }

    /// <summary>
    /// Updates the visual state of a single step.
    /// </summary>
    private void UpdateStepVisual(int rowIndex, int stepIndex)
    {
        if (_viewModel == null || rowIndex < 0 || rowIndex >= _viewModel.Rows.Count)
            return;

        var row = _viewModel.Rows[rowIndex];
        if (stepIndex < 0 || stepIndex >= row.Steps.Count)
            return;

        var step = row.Steps[stepIndex];
        var rowColor = ParseColor(row.Color);

        if (_stepElements.TryGetValue((rowIndex, stepIndex), out var rect))
        {
            // Determine background color
            bool isDownbeat = stepIndex % 4 == 0;
            bool isHalfBeat = stepIndex % 2 == 0;
            Color bgColor = isDownbeat ? DownbeatColor : (isHalfBeat ? Color.FromRgb(0x1E, 0x1E, 0x1E) : OffbeatColor);

            // Calculate step color
            Color stepColor;
            if (step.IsActive)
            {
                byte alpha = _viewModel.ShowProbability
                    ? (byte)(Math.Max(80, step.Probability * 255))
                    : (byte)255;
                stepColor = Color.FromArgb(alpha, rowColor.R, rowColor.G, rowColor.B);
            }
            else
            {
                stepColor = bgColor;
            }

            rect.Fill = new SolidColorBrush(stepColor);
            rect.Stroke = step.IsPlaying
                ? new SolidColorBrush(PlayingColor)
                : new SolidColorBrush(BorderColor);
            rect.StrokeThickness = step.IsPlaying ? 2 : 1;
        }

        // Update or remove velocity bar
        if (_velocityBars.TryGetValue((rowIndex, stepIndex), out var velocityBar))
        {
            if (step.IsActive && _viewModel.ShowVelocity)
            {
                double stepWidth = StepGridCanvas.Width / _viewModel.StepCount;
                double rectWidth = stepWidth - StepPadding * 2;
                velocityBar.Width = rectWidth * step.NormalizedVelocity;
            }
            else
            {
                StepGridCanvas.Children.Remove(velocityBar);
                _velocityBars.Remove((rowIndex, stepIndex));
            }
        }
    }

    /// <summary>
    /// Updates the playing step highlight.
    /// </summary>
    private void UpdatePlayingStep()
    {
        if (_viewModel == null) return;

        int currentStep = _viewModel.CurrentStep;

        // Update all steps' playing state visuals
        foreach (var kvp in _stepElements)
        {
            var (rowIndex, stepIndex) = kvp.Key;
            var rect = kvp.Value;

            bool isPlaying = stepIndex == currentStep;

            rect.Stroke = isPlaying
                ? new SolidColorBrush(PlayingColor)
                : new SolidColorBrush(BorderColor);
            rect.StrokeThickness = isPlaying ? 2 : 1;
        }
    }

    #endregion

    #region Mouse Event Handlers

    private void StepGridCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(StepGridCanvas);
        var (rowIndex, stepIndex) = GetStepAtPosition(pos);

        if (rowIndex >= 0 && stepIndex >= 0 && _viewModel != null)
        {
            var row = _viewModel.Rows[rowIndex];
            var step = row.Steps[stepIndex];

            // Toggle the step
            step.IsActive = !step.IsActive;
            _viewModel.SelectedRowIndex = rowIndex;
            _viewModel.SelectedStepIndex = stepIndex;

            // If activating, preview the sound
            if (step.IsActive)
            {
                _viewModel.ToggleStepCommand.Execute(step);
            }
            else
            {
                // Just sync to engine without preview
                _viewModel.ToggleStepCommand.Execute(null);
            }

            UpdateStepVisual(rowIndex, stepIndex);

            _isDragging = true;
            _dragSetActive = step.IsActive;
            _lastDraggedRow = rowIndex;
            _lastDraggedStep = stepIndex;
            StepGridCanvas.CaptureMouse();
        }
    }

    private void StepGridCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging && e.LeftButton == MouseButtonState.Pressed && _viewModel != null)
        {
            var pos = e.GetPosition(StepGridCanvas);
            var (rowIndex, stepIndex) = GetStepAtPosition(pos);

            if (rowIndex >= 0 && stepIndex >= 0 &&
                (rowIndex != _lastDraggedRow || stepIndex != _lastDraggedStep))
            {
                var row = _viewModel.Rows[rowIndex];
                var step = row.Steps[stepIndex];

                if (step.IsActive != _dragSetActive)
                {
                    step.IsActive = _dragSetActive;
                    UpdateStepVisual(rowIndex, stepIndex);
                }

                _lastDraggedRow = rowIndex;
                _lastDraggedStep = stepIndex;
            }
        }
    }

    private void StepGridCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        _lastDraggedRow = -1;
        _lastDraggedStep = -1;
        StepGridCanvas.ReleaseMouseCapture();
    }

    private void StepGridCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel == null) return;

        var pos = e.GetPosition(StepGridCanvas);
        var (rowIndex, stepIndex) = GetStepAtPosition(pos);

        if (rowIndex >= 0 && stepIndex >= 0)
        {
            var row = _viewModel.Rows[rowIndex];
            var step = row.Steps[stepIndex];

            if (step.IsActive)
            {
                ShowStepContextMenu(step, rowIndex, stepIndex, e.GetPosition(this));
            }
        }
    }

    private (int rowIndex, int stepIndex) GetStepAtPosition(Point pos)
    {
        if (_viewModel == null || StepGridCanvas.Width <= 0 || StepGridCanvas.Height <= 0)
            return (-1, -1);

        double stepWidth = StepGridCanvas.Width / _viewModel.StepCount;
        int stepIndex = (int)(pos.X / stepWidth);
        int rowIndex = (int)(pos.Y / RowHeight);

        if (rowIndex >= 0 && rowIndex < _viewModel.Rows.Count &&
            stepIndex >= 0 && stepIndex < _viewModel.StepCount)
        {
            return (rowIndex, stepIndex);
        }

        return (-1, -1);
    }

    private void ShowStepContextMenu(SequencerStepViewModel step, int rowIndex, int stepIndex, Point position)
    {
        var contextMenu = new ContextMenu
        {
            Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x25)),
            BorderBrush = new SolidColorBrush(BorderColor)
        };

        // Velocity submenu
        var velocityMenu = new MenuItem { Header = $"Velocity: {step.Velocity}" };
        var velocitySlider = new Slider
        {
            Minimum = 1,
            Maximum = 127,
            Value = step.Velocity,
            Width = 100
        };
        velocitySlider.ValueChanged += (s, e) =>
        {
            step.Velocity = (int)e.NewValue;
            velocityMenu.Header = $"Velocity: {step.Velocity}";
            UpdateStepVisual(rowIndex, stepIndex);
        };
        velocityMenu.Items.Add(velocitySlider);
        contextMenu.Items.Add(velocityMenu);

        // Probability submenu
        var probabilityMenu = new MenuItem { Header = $"Probability: {step.Probability:P0}" };
        var probabilitySlider = new Slider
        {
            Minimum = 0,
            Maximum = 1,
            Value = step.Probability,
            Width = 100
        };
        probabilitySlider.ValueChanged += (s, e) =>
        {
            step.Probability = e.NewValue;
            probabilityMenu.Header = $"Probability: {step.Probability:P0}";
            UpdateStepVisual(rowIndex, stepIndex);
        };
        probabilityMenu.Items.Add(probabilitySlider);
        contextMenu.Items.Add(probabilityMenu);

        contextMenu.Items.Add(new Separator());

        // Accent toggle
        var accentItem = new MenuItem
        {
            Header = step.HasAccent ? "Remove Accent" : "Add Accent",
            IsCheckable = true,
            IsChecked = step.HasAccent
        };
        accentItem.Click += (s, e) =>
        {
            step.HasAccent = !step.HasAccent;
            RefreshGrid();
        };
        contextMenu.Items.Add(accentItem);

        // Retrigger
        var retriggerMenu = new MenuItem { Header = $"Retrigger: {step.Retrigger}x" };
        for (int r = 1; r <= 4; r++)
        {
            int retriggerValue = r;
            var retriggerItem = new MenuItem
            {
                Header = $"{r}x",
                IsCheckable = true,
                IsChecked = step.Retrigger == r
            };
            retriggerItem.Click += (s, e) =>
            {
                step.Retrigger = retriggerValue;
                RefreshGrid();
            };
            retriggerMenu.Items.Add(retriggerItem);
        }
        contextMenu.Items.Add(retriggerMenu);

        contextMenu.Items.Add(new Separator());

        // Clear step
        var clearItem = new MenuItem { Header = "Clear Step" };
        clearItem.Click += (s, e) =>
        {
            step.IsActive = false;
            step.Velocity = 100;
            step.Probability = 1.0;
            step.HasAccent = false;
            step.Retrigger = 1;
            UpdateStepVisual(rowIndex, stepIndex);
        };
        contextMenu.Items.Add(clearItem);

        contextMenu.IsOpen = true;
    }

    #endregion

    #region Scroll Synchronization

    private void StepGridScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // Sync row header scroll with step grid scroll
        RowHeaderScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);
    }

    #endregion

    #region UI Event Handlers

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.PlayCommand.Execute(null);
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.StopCommand.Execute(null);
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.ResetCommand.Execute(null);
    }

    private void StepCountCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel == null || StepCountCombo.SelectedItem == null) return;

        var selectedItem = (ComboBoxItem)StepCountCombo.SelectedItem;
        if (int.TryParse(selectedItem.Content?.ToString(), out int stepCount))
        {
            _viewModel.StepCount = stepCount;
        }
    }

    private void DirectionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel == null || DirectionCombo.SelectedIndex < 0) return;

        _viewModel.Direction = (PlaybackDirection)DirectionCombo.SelectedIndex;
    }

    private void SwingSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_viewModel == null) return;

        _viewModel.Swing = e.NewValue;
        SwingValueText.Text = $"{(int)(e.NewValue * 100)}%";
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.ClearAllCommand.Execute(null);
        RefreshGrid();
    }

    private void RandomButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.RandomizeCommand.Execute(null);
        RefreshGrid();
    }

    private void BasicBeatButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.LoadBasicBeatCommand.Execute(null);
        RefreshGrid();
    }

    private void AddRowButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.AddRowCommand.Execute(null);
        RefreshGrid();
    }

    private void LoopCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.LoopEnabled = LoopCheckBox.IsChecked == true;
        }
    }

    private void LoopStartTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_viewModel != null && int.TryParse(LoopStartTextBox.Text, out int start))
        {
            _viewModel.LoopStart = Math.Max(0, start - 1); // Convert to 0-based
        }
    }

    private void LoopEndTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_viewModel != null && int.TryParse(LoopEndTextBox.Text, out int end))
        {
            _viewModel.LoopEnd = Math.Min(_viewModel.StepCount, end);
        }
    }

    private void DisplayOptionsChanged(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.ShowVelocity = ShowVelocityCheckBox.IsChecked == true;
            _viewModel.ShowProbability = ShowProbabilityCheckBox.IsChecked == true;
            RefreshGrid();
        }
    }

    #endregion

    #region UI Update Methods

    private void UpdateLoopControls()
    {
        if (_viewModel == null) return;

        LoopStartTextBox.Text = (_viewModel.LoopStart + 1).ToString(); // Display as 1-based
        LoopEndTextBox.Text = _viewModel.LoopEnd.ToString();
    }

    private void UpdateCurrentStepText()
    {
        if (_viewModel == null) return;

        int step = _viewModel.CurrentStep;
        CurrentStepText.Text = step >= 0 ? $"Step: {step + 1}" : "Step: --";
    }

    private void UpdateStatusText()
    {
        if (_viewModel == null) return;
        StatusText.Text = _viewModel.StatusMessage ?? "";
    }

    private void UpdatePlayButton()
    {
        if (_viewModel == null) return;
        PlayButton.Content = _viewModel.IsPlaying ? "Pause" : "Play";
    }

    #endregion

    #region Helper Methods

    private static Color ParseColor(string hex)
    {
        try
        {
            return (Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return AccentColor;
        }
    }

    #endregion

    #region IDisposable

    private bool _disposed;

    /// <summary>
    /// Disposes resources used by this control.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            _viewModel.StepTriggered -= ViewModel_StepTriggered;
            _viewModel.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    #endregion
}
