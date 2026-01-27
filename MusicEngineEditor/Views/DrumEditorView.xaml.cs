// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: View implementation.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MusicEngineEditor.Controls;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Views;

/// <summary>
/// Grid-based drum programming view with multiple lanes.
/// </summary>
public partial class DrumEditorView : UserControl
{
    private readonly List<DrumLaneControl> _laneControls = new();
    private readonly DispatcherTimer _playbackTimer;
    private readonly Random _random = new();

    private int _currentStep = -1;
    private int _stepCount = 16;
    private double _swingAmount;
#pragma warning disable CS0414 // Field is assigned but its value is never used
    private bool _isPlaying;
#pragma warning restore CS0414
    private double _bpm = 120;

    /// <summary>
    /// Gets or sets the pattern step count.
    /// </summary>
    public int Steps
    {
        get => _stepCount;
        set
        {
            _stepCount = value;
            UpdateAllLanesStepCount();
            DrawStepNumbers();
            UpdatePatternInfo();
        }
    }

    /// <summary>
    /// Gets or sets the swing amount (0-100).
    /// </summary>
    public double SwingAmount
    {
        get => _swingAmount;
        set
        {
            _swingAmount = value;
            SwingSlider.Value = value;
            SwingValueText.Text = $"{value:F0}%";
        }
    }

    /// <summary>
    /// Gets or sets the tempo in BPM.
    /// </summary>
    public double BPM
    {
        get => _bpm;
        set
        {
            _bpm = value;
            BpmText.Text = $"{value:F0} BPM";
            UpdateTimerInterval();
        }
    }

    /// <summary>
    /// Event raised when pattern changes.
    /// </summary>
    public event EventHandler? PatternChanged;

    /// <summary>
    /// Event raised when a step is triggered during playback.
    /// </summary>
    public event EventHandler<(int laneIndex, int midiNote, int velocity)>? StepTriggered;

    public DrumEditorView()
    {
        InitializeComponent();

        _playbackTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(125) // 120 BPM, 16th notes
        };
        _playbackTimer.Tick += PlaybackTimer_Tick;

        Loaded += (_, _) =>
        {
            LoadKit(DrumLane.StandardKit);
            DrawStepNumbers();
            UpdatePatternInfo();
        };

        SizeChanged += (_, _) => DrawStepNumbers();
    }

    private void LoadKit(DrumLane[] kit)
    {
        _laneControls.Clear();
        LanesItemsControl.Items.Clear();

        foreach (var lane in kit.Take(8)) // Limit to 8 lanes for now
        {
            var laneControl = new DrumLaneControl
            {
                Lane = lane,
                StepCount = _stepCount,
                Height = 32,
                Margin = new Thickness(0)
            };

            laneControl.StepToggled += LaneControl_StepToggled;
            laneControl.VelocityChanged += LaneControl_VelocityChanged;
            laneControl.MuteChanged += LaneControl_MuteChanged;
            laneControl.SoloChanged += LaneControl_SoloChanged;

            _laneControls.Add(laneControl);
            LanesItemsControl.Items.Add(laneControl);
        }

        UpdatePatternInfo();
    }

    private void UpdateAllLanesStepCount()
    {
        foreach (var lane in _laneControls)
        {
            lane.StepCount = _stepCount;
        }
    }

    private void DrawStepNumbers()
    {
        StepNumbersCanvas.Children.Clear();

        if (StepNumbersCanvas.ActualWidth <= 0 || _stepCount == 0)
            return;

        double stepWidth = StepNumbersCanvas.ActualWidth / _stepCount;
        var brush = new SolidColorBrush(Color.FromRgb(0x6F, 0x73, 0x7A));
        var accentBrush = new SolidColorBrush(Color.FromRgb(0xBC, 0xBE, 0xC4));

        for (int i = 0; i < _stepCount; i++)
        {
            var isDownbeat = i % 4 == 0;
            var text = new TextBlock
            {
                Text = (i + 1).ToString(),
                FontSize = 9,
                Foreground = isDownbeat ? accentBrush : brush,
                FontWeight = isDownbeat ? FontWeights.SemiBold : FontWeights.Normal,
                TextAlignment = TextAlignment.Center
            };

            Canvas.SetLeft(text, i * stepWidth + stepWidth / 2 - 6);
            Canvas.SetTop(text, 5);
            StepNumbersCanvas.Children.Add(text);
        }
    }

    private void UpdatePatternInfo()
    {
        PatternInfoText.Text = $"Pattern: {_stepCount} steps | {_laneControls.Count} lanes";
    }

    private void UpdateTimerInterval()
    {
        // Calculate interval for 16th notes at current BPM
        // 16th note = 1 beat / 4 = (60 / BPM) / 4 seconds
        double secondsPerSixteenth = 60.0 / _bpm / 4.0;
        _playbackTimer.Interval = TimeSpan.FromSeconds(secondsPerSixteenth);
    }

    private void AdvanceStep()
    {
        _currentStep = (_currentStep + 1) % _stepCount;
        CurrentStepText.Text = $"Step: {_currentStep + 1}";

        // Update all lanes with current step
        foreach (var lane in _laneControls)
        {
            lane.CurrentStep = _currentStep;
        }

        // Trigger active steps
        for (int i = 0; i < _laneControls.Count; i++)
        {
            var lane = _laneControls[i];
            if (lane.Lane == null || lane.Lane.IsMuted) continue;

            // Check for solo
            bool hasSolo = _laneControls.Any(l => l.Lane?.IsSolo == true);
            if (hasSolo && !lane.Lane.IsSolo) continue;

            if (lane.Steps.Count > _currentStep && lane.Steps[_currentStep].IsActive)
            {
                var step = lane.Steps[_currentStep];
                StepTriggered?.Invoke(this, (i, lane.Lane.MidiNote, step.Velocity));
            }
        }
    }

    #region Event Handlers

    private void LaneControl_StepToggled(object? sender, DrumStepEventArgs e)
    {
        PatternChanged?.Invoke(this, EventArgs.Empty);
    }

    private void LaneControl_VelocityChanged(object? sender, DrumStepEventArgs e)
    {
        PatternChanged?.Invoke(this, EventArgs.Empty);
    }

    private void LaneControl_MuteChanged(object? sender, bool e)
    {
        // Handle mute state change
    }

    private void LaneControl_SoloChanged(object? sender, bool e)
    {
        // Handle solo state change
    }

    private void PlaybackTimer_Tick(object? sender, EventArgs e)
    {
        AdvanceStep();
    }

    private void StepsCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StepsCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            if (int.TryParse(tag, out int steps))
            {
                Steps = steps;
            }
        }
    }

    private void SwingSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _swingAmount = e.NewValue;
        SwingValueText.Text = $"{_swingAmount:F0}%";
    }

    private void KitCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (KitCombo.SelectedIndex == 0)
        {
            LoadKit(DrumLane.StandardKit);
        }
        else if (KitCombo.SelectedIndex == 1)
        {
            LoadKit(DrumLane.Kit808);
        }
        else
        {
            // Minimal kit - just kick, snare, hihat
            var minimalKit = new[]
            {
                new DrumLane("Kick", 36, "#DC143C"),
                new DrumLane("Snare", 38, "#FFD700"),
                new DrumLane("HiHat", 42, "#4169E1"),
                new DrumLane("Clap", 39, "#FF69B4")
            };
            LoadKit(minimalKit);
        }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        _isPlaying = false;
        _playbackTimer.Stop();
        _currentStep = -1;
        CurrentStepText.Text = "Step: -";

        // Clear all step highlights
        foreach (var lane in _laneControls)
        {
            lane.CurrentStep = -1;
        }
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        _isPlaying = true;
        _currentStep = -1;
        UpdateTimerInterval();
        _playbackTimer.Start();
    }

    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var lane in _laneControls)
        {
            lane.ClearAllSteps();
        }
        PatternChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Randomize_Click(object sender, RoutedEventArgs e)
    {
        foreach (var lane in _laneControls)
        {
            var activeSteps = new List<int>();
            // Random density based on lane position (kick less dense than hihat)
            double density = lane.Lane?.MidiNote switch
            {
                36 => 0.25, // Kick
                38 or 39 => 0.2, // Snare/Clap
                42 or 46 => 0.5, // HiHat
                _ => 0.15
            };

            for (int i = 0; i < _stepCount; i++)
            {
                if (_random.NextDouble() < density)
                {
                    activeSteps.Add(i);
                }
            }
            lane.SetActiveSteps(activeSteps.ToArray());
        }
        PatternChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Euclidean_Click(object sender, RoutedEventArgs e)
    {
        // Apply Euclidean rhythms to each lane
        foreach (var lane in _laneControls)
        {
            // Determine number of hits based on lane type
            int hits = lane.Lane?.MidiNote switch
            {
                36 => 4, // Kick - 4 hits
                38 or 39 => 2, // Snare - 2 hits
                42 => 8, // Closed HiHat - 8 hits
                46 => 2, // Open HiHat - 2 hits
                _ => 3
            };

            var pattern = GenerateEuclideanRhythm(hits, _stepCount);
            lane.SetActiveSteps(pattern);
        }
        PatternChanged?.Invoke(this, EventArgs.Empty);
    }

    private int[] GenerateEuclideanRhythm(int hits, int steps)
    {
        if (hits >= steps)
            return Enumerable.Range(0, steps).ToArray();

        if (hits == 0)
            return Array.Empty<int>();

        // Bjorklund algorithm
        var pattern = new bool[steps];
        var bucket = 0.0;
        var increment = (double)hits / steps;

        for (int i = 0; i < steps; i++)
        {
            bucket += increment;
            if (bucket >= 1)
            {
                bucket -= 1;
                pattern[i] = true;
            }
        }

        return pattern.Select((active, index) => active ? index : -1)
                      .Where(i => i >= 0)
                      .ToArray();
    }

    #endregion

    /// <summary>
    /// Gets the current pattern as a dictionary of lane MIDI notes to active step indices.
    /// </summary>
    public Dictionary<int, int[]> GetPattern()
    {
        var pattern = new Dictionary<int, int[]>();

        foreach (var lane in _laneControls)
        {
            if (lane.Lane != null)
            {
                pattern[lane.Lane.MidiNote] = lane.GetActiveSteps();
            }
        }

        return pattern;
    }

    /// <summary>
    /// Sets the pattern from a dictionary of lane MIDI notes to active step indices.
    /// </summary>
    public void SetPattern(Dictionary<int, int[]> pattern)
    {
        foreach (var lane in _laneControls)
        {
            if (lane.Lane != null && pattern.TryGetValue(lane.Lane.MidiNote, out var steps))
            {
                lane.SetActiveSteps(steps);
            }
        }
    }
}
