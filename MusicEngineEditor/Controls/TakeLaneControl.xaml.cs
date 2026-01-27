// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MusicEngine.Core;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Control for displaying and editing take lanes with comping functionality.
/// Allows recording multiple takes and selecting the best parts for the final comp.
/// </summary>
public partial class TakeLaneControl : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty TakeLaneProperty =
        DependencyProperty.Register(nameof(TakeLane), typeof(TakeLane), typeof(TakeLaneControl),
            new PropertyMetadata(null, OnTakeLaneChanged));

    public static readonly DependencyProperty BeatsPerMeasureProperty =
        DependencyProperty.Register(nameof(BeatsPerMeasure), typeof(int), typeof(TakeLaneControl),
            new PropertyMetadata(4, OnDisplayPropertyChanged));

    public static readonly DependencyProperty BpmProperty =
        DependencyProperty.Register(nameof(Bpm), typeof(double), typeof(TakeLaneControl),
            new PropertyMetadata(120.0, OnDisplayPropertyChanged));

    public static readonly DependencyProperty PixelsPerBeatProperty =
        DependencyProperty.Register(nameof(PixelsPerBeat), typeof(double), typeof(TakeLaneControl),
            new PropertyMetadata(40.0, OnDisplayPropertyChanged));

    public static readonly DependencyProperty TakeHeightProperty =
        DependencyProperty.Register(nameof(TakeHeight), typeof(double), typeof(TakeLaneControl),
            new PropertyMetadata(60.0, OnDisplayPropertyChanged));

    public static readonly DependencyProperty SampleRateProperty =
        DependencyProperty.Register(nameof(SampleRate), typeof(int), typeof(TakeLaneControl),
            new PropertyMetadata(44100));

    public static readonly DependencyProperty IsExpandedProperty =
        DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(TakeLaneControl),
            new PropertyMetadata(true, OnIsExpandedChanged));

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the TakeLane being displayed.
    /// </summary>
    public TakeLane? TakeLane
    {
        get => (TakeLane?)GetValue(TakeLaneProperty);
        set => SetValue(TakeLaneProperty, value);
    }

    /// <summary>
    /// Gets or sets the beats per measure for display.
    /// </summary>
    public int BeatsPerMeasure
    {
        get => (int)GetValue(BeatsPerMeasureProperty);
        set => SetValue(BeatsPerMeasureProperty, value);
    }

    /// <summary>
    /// Gets or sets the tempo in BPM.
    /// </summary>
    public double Bpm
    {
        get => (double)GetValue(BpmProperty);
        set => SetValue(BpmProperty, value);
    }

    /// <summary>
    /// Gets or sets the horizontal zoom (pixels per beat).
    /// </summary>
    public double PixelsPerBeat
    {
        get => (double)GetValue(PixelsPerBeatProperty);
        set => SetValue(PixelsPerBeatProperty, value);
    }

    /// <summary>
    /// Gets or sets the height of each take lane.
    /// </summary>
    public double TakeHeight
    {
        get => (double)GetValue(TakeHeightProperty);
        set => SetValue(TakeHeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the sample rate for audio processing.
    /// </summary>
    public int SampleRate
    {
        get => (int)GetValue(SampleRateProperty);
        set => SetValue(SampleRateProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the take lanes are expanded.
    /// </summary>
    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Event raised when a take is selected.
    /// </summary>
    public event EventHandler<TakeViewModel?>? TakeSelected;

    /// <summary>
    /// Event raised when the comp is flattened.
    /// </summary>
    public event EventHandler<AudioClip>? CompFlattened;

    /// <summary>
    /// Event raised when a comp region is added or modified.
    /// </summary>
    public event EventHandler<CompRegion>? CompRegionChanged;

    #endregion

    #region Fields

    private readonly ObservableCollection<TakeViewModel> _takeViewModels = new();
    private bool _isDragging;
    private Point _dragStartPoint;
    private double _dragStartBeat;
#pragma warning disable CS0414 // Field is assigned but its value is never used
    private CompRegion? _currentDragRegion;
#pragma warning restore CS0414
    private TakeViewModel? _dragSourceTake;
    private readonly Dictionary<string, Path> _waveformPaths = new();
    private readonly List<Rectangle> _compRegionRects = new();

    #endregion

    public TakeLaneControl()
    {
        InitializeComponent();

        TakeListBox.ItemsSource = _takeViewModels;
        SizeChanged += OnSizeChanged;
    }

    #region Property Changed Callbacks

    private static void OnTakeLaneChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TakeLaneControl control)
        {
            if (e.OldValue is TakeLane oldLane)
            {
                oldLane.TakeAdded -= control.OnTakeAdded;
                oldLane.TakeRemoved -= control.OnTakeRemoved;
                oldLane.CompChanged -= control.OnCompChanged;
                oldLane.Changed -= control.OnLaneChanged;
            }

            if (e.NewValue is TakeLane newLane)
            {
                newLane.TakeAdded += control.OnTakeAdded;
                newLane.TakeRemoved += control.OnTakeRemoved;
                newLane.CompChanged += control.OnCompChanged;
                newLane.Changed += control.OnLaneChanged;

                control.LaneNameText.Text = newLane.Name;
                control.RefreshTakeList();
                control.UpdateDisplay();
            }
            else
            {
                control._takeViewModels.Clear();
                control.UpdateDisplay();
            }
        }
    }

    private static void OnDisplayPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TakeLaneControl control)
        {
            control.UpdateDisplay();
        }
    }

    private static void OnIsExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TakeLaneControl control)
        {
            control.ExpandCollapseButton.Content = (bool)e.NewValue ? "-" : "+";
            control.MainContentGrid.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    #endregion

    #region Event Handlers

    private void OnTakeAdded(object? sender, TakeAddedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var vm = new TakeViewModel(e.Take);
            _takeViewModels.Add(vm);
            UpdateTakeCount();
            UpdateDisplay();
        });
    }

    private void OnTakeRemoved(object? sender, TakeRemovedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var vm = _takeViewModels.FirstOrDefault(t => t.Take.Id == e.Take.Id);
            if (vm != null)
            {
                _takeViewModels.Remove(vm);
            }
            UpdateTakeCount();
            UpdateDisplay();
        });
    }

    private void OnCompChanged(object? sender, CompRegionChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateCompDisplay();
            UpdateCompStatus();
        });
    }

    private void OnLaneChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(UpdateDisplay);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateDisplay();
    }

    private void TakeListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = TakeListBox.SelectedItem as TakeViewModel;
        TakeSelected?.Invoke(this, selected);
        UpdateWaveformHighlight(selected);
    }

    private void StarButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton button && button.Tag is string tagStr)
        {
            var listItem = FindParent<ListBoxItem>(button);
            if (listItem?.DataContext is TakeViewModel vm)
            {
                if (int.TryParse(tagStr, out var rating))
                {
                    vm.SetRating(rating);
                }
            }
        }
    }

    private void UseTakeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            var listItem = FindParent<ListBoxItem>(button);
            if (listItem?.DataContext is TakeViewModel vm && TakeLane != null)
            {
                TakeLane.CompFromTake(vm.Take);
            }
        }
    }

    private void DeleteTakeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            var listItem = FindParent<ListBoxItem>(button);
            if (listItem?.DataContext is TakeViewModel vm && TakeLane != null)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to delete '{vm.Name}'?",
                    "Delete Take",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    TakeLane.RemoveTake(vm.Take);
                }
            }
        }
    }

    private void AutoCompButton_Click(object sender, RoutedEventArgs e)
    {
        TakeLane?.AutoComp();
    }

    private void FlattenButton_Click(object sender, RoutedEventArgs e)
    {
        if (TakeLane != null)
        {
            var clip = TakeLane.FlattenComp(SampleRate, Bpm);
            CompFlattened?.Invoke(this, clip);
        }
    }

    private void ExpandCollapseButton_Click(object sender, RoutedEventArgs e)
    {
        IsExpanded = !IsExpanded;
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        PixelsPerBeat = 40.0 * e.NewValue;
    }

    private void TakeLanesCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(TakeLanesCanvas);
        _dragStartPoint = pos;
        _isDragging = true;

        // Determine which take we're clicking on
        var takeIndex = (int)(pos.Y / TakeHeight);
        if (takeIndex >= 0 && takeIndex < _takeViewModels.Count)
        {
            _dragSourceTake = _takeViewModels[takeIndex];
            _dragStartBeat = PixelsToBeat(pos.X);
        }

        TakeLanesCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void TakeLanesCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging && _dragSourceTake != null && TakeLane != null)
        {
            var pos = e.GetPosition(TakeLanesCanvas);
            var endBeat = PixelsToBeat(pos.X);

            // Create comp region if we dragged a meaningful distance
            if (Math.Abs(endBeat - _dragStartBeat) > 0.125) // Minimum 1/8 beat
            {
                var startBeat = Math.Min(_dragStartBeat, endBeat);
                var regionEndBeat = Math.Max(_dragStartBeat, endBeat);

                // Clamp to take bounds
                startBeat = Math.Max(startBeat, _dragSourceTake.Take.StartBeat);
                regionEndBeat = Math.Min(regionEndBeat, _dragSourceTake.Take.EndBeat);

                if (regionEndBeat > startBeat)
                {
                    var region = TakeLane.AddCompRegion(_dragSourceTake.Take, startBeat, regionEndBeat);
                    CompRegionChanged?.Invoke(this, region);
                }
            }
        }

        _isDragging = false;
        _dragSourceTake = null;
        _currentDragRegion = null;
        TakeLanesCanvas.ReleaseMouseCapture();
    }

    private void TakeLanesCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging && _dragSourceTake != null)
        {
            var pos = e.GetPosition(TakeLanesCanvas);
            UpdateDragVisual(pos);
        }
    }

    private void NewTakeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        TakeLane?.CreateTake();
    }

    private void DuplicateTakeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (TakeListBox.SelectedItem is TakeViewModel vm && TakeLane != null)
        {
            var clone = vm.Take.Clone();
            clone.Name = vm.Take.Name + " (Copy)";
            TakeLane.AddTake(clone);
        }
    }

    private void RenameTakeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // In a real implementation, this would show a rename dialog
        if (TakeListBox.SelectedItem is TakeViewModel vm)
        {
            // For now, just append "(renamed)"
            vm.Name = vm.Name.Replace(" (renamed)", "") + " (renamed)";
        }
    }

    private void MuteTakeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (TakeListBox.SelectedItem is TakeViewModel vm)
        {
            vm.Take.IsMuted = !vm.Take.IsMuted;
            UpdateDisplay();
        }
    }

    private void SoloTakeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (TakeListBox.SelectedItem is TakeViewModel vm)
        {
            // Mute all other takes
            foreach (var takeVm in _takeViewModels)
            {
                takeVm.Take.IsMuted = takeVm != vm;
            }
            UpdateDisplay();
        }
    }

    private void ClearCompMenuItem_Click(object sender, RoutedEventArgs e)
    {
        TakeLane?.ClearCompRegions();
    }

    #endregion

    #region Display Methods

    private void RefreshTakeList()
    {
        _takeViewModels.Clear();

        if (TakeLane != null)
        {
            foreach (var take in TakeLane.GetTakes())
            {
                _takeViewModels.Add(new TakeViewModel(take));
            }
        }

        UpdateTakeCount();
    }

    private void UpdateTakeCount()
    {
        TakeCountText.Text = $" ({_takeViewModels.Count} takes)";
        EmptyStateText.Visibility = _takeViewModels.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateDisplay()
    {
        UpdateTimeline();
        UpdateTakeWaveforms();
        UpdateCompDisplay();
        UpdateCompStatus();
    }

    private void UpdateTimeline()
    {
        TimelineRuler.Children.Clear();

        if (TakeLane == null || _takeViewModels.Count == 0) return;

        var (startBeat, endBeat) = TakeLane.GetTakeRange();
        var width = BeatToPixels(endBeat - startBeat + 4);

        // Draw measure lines and labels
        for (var beat = (int)startBeat; beat <= endBeat + 4; beat++)
        {
            var x = BeatToPixels(beat - startBeat);
            var isMeasure = beat % BeatsPerMeasure == 0;

            var line = new Line
            {
                X1 = x,
                X2 = x,
                Y1 = isMeasure ? 0 : 12,
                Y2 = 20,
                Stroke = isMeasure ? Brushes.White : new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60)),
                StrokeThickness = isMeasure ? 1 : 0.5
            };
            TimelineRuler.Children.Add(line);

            if (isMeasure)
            {
                var measure = beat / BeatsPerMeasure + 1;
                var label = new TextBlock
                {
                    Text = measure.ToString(),
                    FontSize = 10,
                    Foreground = Brushes.White
                };
                Canvas.SetLeft(label, x + 2);
                Canvas.SetTop(label, 2);
                TimelineRuler.Children.Add(label);
            }
        }
    }

    private void UpdateTakeWaveforms()
    {
        TakeLanesCanvas.Children.Clear();
        _waveformPaths.Clear();

        if (TakeLane == null) return;

        var takes = TakeLane.GetTakes();
        var (startBeat, _) = TakeLane.GetTakeRange();

        for (var i = 0; i < takes.Count; i++)
        {
            var take = takes[i];
            var y = i * TakeHeight;

            // Background for the take lane
            var background = new Rectangle
            {
                Width = BeatToPixels(take.LengthBeats),
                Height = TakeHeight - 4,
                Fill = take.IsMuted
                    ? new SolidColorBrush(Color.FromArgb(0x40, 0x80, 0x80, 0x80))
                    : new SolidColorBrush(Color.FromArgb(0x40, take.Color.R, take.Color.G, take.Color.B)),
                RadiusX = 3,
                RadiusY = 3
            };
            Canvas.SetLeft(background, BeatToPixels(take.StartBeat - startBeat));
            Canvas.SetTop(background, y + 2);
            TakeLanesCanvas.Children.Add(background);

            // Border
            var border = new Rectangle
            {
                Width = BeatToPixels(take.LengthBeats),
                Height = TakeHeight - 4,
                Stroke = new SolidColorBrush(Color.FromRgb(take.Color.R, take.Color.G, take.Color.B)),
                StrokeThickness = 1,
                Fill = Brushes.Transparent,
                RadiusX = 3,
                RadiusY = 3
            };
            Canvas.SetLeft(border, BeatToPixels(take.StartBeat - startBeat));
            Canvas.SetTop(border, y + 2);
            TakeLanesCanvas.Children.Add(border);

            // Waveform visualization
            if (take.AudioData.Length > 0)
            {
                var waveform = CreateWaveformPath(take, BeatToPixels(take.LengthBeats), TakeHeight - 8);
                Canvas.SetLeft(waveform, BeatToPixels(take.StartBeat - startBeat));
                Canvas.SetTop(waveform, y + 4);
                TakeLanesCanvas.Children.Add(waveform);
                _waveformPaths[take.Id] = waveform;
            }

            // Take label
            var label = new TextBlock
            {
                Text = take.Name,
                FontSize = 10,
                Foreground = Brushes.White,
                Opacity = take.IsMuted ? 0.5 : 1
            };
            Canvas.SetLeft(label, BeatToPixels(take.StartBeat - startBeat) + 4);
            Canvas.SetTop(label, y + 4);
            TakeLanesCanvas.Children.Add(label);
        }

        // Update canvas size
        var totalHeight = takes.Count * TakeHeight;
        TakeLanesCanvas.Height = Math.Max(totalHeight, TakeLanesScrollViewer.ActualHeight);
        var (_, endBeat) = TakeLane.GetTakeRange();
        TakeLanesCanvas.Width = Math.Max(BeatToPixels(endBeat - startBeat + 4), TakeLanesScrollViewer.ActualWidth);
    }

    private Path CreateWaveformPath(Take take, double width, double height)
    {
        var data = take.AudioData;
        var channels = take.Channels;

        if (data.Length == 0)
        {
            return new Path { Fill = Brushes.Transparent };
        }

        var samplesPerChannel = data.Length / channels;
        var samplesPerPixel = Math.Max(1, (int)(samplesPerChannel / width));
        var centerY = height / 2;
        var halfHeight = height / 2 * 0.9;

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var pixelCount = Math.Min((int)width, samplesPerChannel / samplesPerPixel);
            if (pixelCount <= 0) return new Path { Fill = Brushes.Transparent };

            var topPoints = new Point[pixelCount];
            var bottomPoints = new Point[pixelCount];

            for (var x = 0; x < pixelCount; x++)
            {
                var sampleIndex = x * samplesPerPixel * channels;
                float min = 0, max = 0;

                for (var i = 0; i < samplesPerPixel * channels && sampleIndex + i < data.Length; i += channels)
                {
                    var sample = data[sampleIndex + i];
                    if (sample < min) min = sample;
                    if (sample > max) max = sample;
                }

                topPoints[x] = new Point(x, centerY - max * halfHeight);
                bottomPoints[x] = new Point(x, centerY - min * halfHeight);
            }

            context.BeginFigure(topPoints[0], true, true);
            for (var i = 1; i < pixelCount; i++)
            {
                context.LineTo(topPoints[i], true, false);
            }
            for (var i = pixelCount - 1; i >= 0; i--)
            {
                context.LineTo(bottomPoints[i], true, false);
            }
        }

        geometry.Freeze();

        return new Path
        {
            Data = geometry,
            Fill = new SolidColorBrush(Color.FromRgb(take.Color.R, take.Color.G, take.Color.B)),
            Opacity = take.IsMuted ? 0.3 : 0.7
        };
    }

    private void UpdateCompDisplay()
    {
        CompOverlayCanvas.Children.Clear();
        _compRegionRects.Clear();

        if (TakeLane == null) return;

        var regions = TakeLane.GetCompRegions();
        var takes = TakeLane.GetTakes();
        var (startBeat, _) = TakeLane.GetTakeRange();

        foreach (var region in regions)
        {
            if (!region.IsActive) continue;

            // Find the take index for this region
            var takeIndex = takes.ToList().FindIndex(t => t.Id == region.SourceTake?.Id);
            if (takeIndex < 0) continue;

            var y = takeIndex * TakeHeight;
            var x = BeatToPixels(region.StartBeat - startBeat);
            var width = BeatToPixels(region.LengthBeats);

            // Comp region highlight
            var highlight = new Rectangle
            {
                Width = width,
                Height = TakeHeight - 4,
                Fill = new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xEB, 0x3B)),
                Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0xEB, 0x3B)),
                StrokeThickness = 2,
                RadiusX = 3,
                RadiusY = 3
            };
            Canvas.SetLeft(highlight, x);
            Canvas.SetTop(highlight, y + 2);
            CompOverlayCanvas.Children.Add(highlight);
            _compRegionRects.Add(highlight);
        }
    }

    private void UpdateCompStatus()
    {
        if (TakeLane == null)
        {
            CompStatusText.Text = "No take lane loaded";
            return;
        }

        var regions = TakeLane.GetCompRegions();
        if (regions.Count == 0)
        {
            CompStatusText.Text = "No comp regions selected";
        }
        else
        {
            var totalBeats = regions.Sum(r => r.LengthBeats);
            CompStatusText.Text = $"{regions.Count} comp region(s), {totalBeats:F1} beats total";
        }
    }

    private void UpdateWaveformHighlight(TakeViewModel? selected)
    {
        foreach (var kvp in _waveformPaths)
        {
            kvp.Value.Opacity = 0.5;
        }

        if (selected != null && _waveformPaths.TryGetValue(selected.Take.Id, out var path))
        {
            path.Opacity = 1.0;
        }
    }

    private void UpdateDragVisual(Point currentPos)
    {
        // Visual feedback during drag would be implemented here
        // For now, the actual region is created on mouse up
    }

    #endregion

    #region Utility Methods

    private double BeatToPixels(double beats) => beats * PixelsPerBeat;
    private double PixelsToBeat(double pixels) => pixels / PixelsPerBeat;

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent != null && parent is not T)
        {
            parent = VisualTreeHelper.GetParent(parent);
        }
        return parent as T;
    }

    #endregion
}

/// <summary>
/// ViewModel for displaying takes in the list.
/// </summary>
public class TakeViewModel : INotifyPropertyChanged
{
    public Take Take { get; }

    public string Id => Take.Id;

    public string Name
    {
        get => Take.Name;
        set
        {
            if (Take.Name != value)
            {
                Take.Name = value;
                OnPropertyChanged();
            }
        }
    }

    public int Rating
    {
        get => Take.Rating;
        set
        {
            if (Take.Rating != value)
            {
                Take.Rating = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Star1));
                OnPropertyChanged(nameof(Star2));
                OnPropertyChanged(nameof(Star3));
                OnPropertyChanged(nameof(Star4));
                OnPropertyChanged(nameof(Star5));
            }
        }
    }

    public bool Star1 => Rating >= 1;
    public bool Star2 => Rating >= 2;
    public bool Star3 => Rating >= 3;
    public bool Star4 => Rating >= 4;
    public bool Star5 => Rating >= 5;

    public void SetRating(int stars)
    {
        Rating = Rating == stars ? stars - 1 : stars;
    }

    public string TimeInfo => $"{Take.RecordedAt:HH:mm} - {Take.DurationSeconds:F1}s";

    public SolidColorBrush ColorBrush => new(
        Color.FromRgb(Take.Color.R, Take.Color.G, Take.Color.B));

    public TakeViewModel(Take take)
    {
        Take = take;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
