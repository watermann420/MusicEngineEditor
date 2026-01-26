//MusicEngineEditor - Tempo List Editor View
// copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicEngineEditor.Views;

/// <summary>
/// Defines the tempo curve type for transitions between tempo points.
/// </summary>
public enum TempoCurveType
{
    /// <summary>Linear interpolation between points.</summary>
    Linear,
    /// <summary>S-curve (ease in/out) interpolation.</summary>
    SCurve,
    /// <summary>Immediate step change (no interpolation).</summary>
    Step
}

/// <summary>
/// Represents a tempo change point on the tempo track.
/// </summary>
public partial class TempoPoint : ObservableObject
{
    private static int _nextId = 1;

    /// <summary>
    /// Gets the unique ID.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Gets or sets the position in beats.
    /// </summary>
    [ObservableProperty]
    private double _position;

    /// <summary>
    /// Gets or sets the tempo in BPM.
    /// </summary>
    [ObservableProperty]
    private double _tempo = 120.0;

    /// <summary>
    /// Gets or sets the curve type for transition to next point.
    /// </summary>
    [ObservableProperty]
    private TempoCurveType _curveType = TempoCurveType.Linear;

    /// <summary>
    /// Gets or sets the duration to next point (calculated).
    /// </summary>
    [ObservableProperty]
    private double _durationBeats;

    /// <summary>
    /// Gets the position display string (bars:beats).
    /// </summary>
    public string PositionDisplay
    {
        get
        {
            int bar = (int)(Position / 4) + 1;
            int beat = (int)(Position % 4) + 1;
            return $"{bar}:{beat}";
        }
    }

    /// <summary>
    /// Gets the curve type display string.
    /// </summary>
    public string CurveTypeDisplay => CurveType switch
    {
        TempoCurveType.Linear => "Linear",
        TempoCurveType.SCurve => "S-Curve",
        TempoCurveType.Step => "Step",
        _ => "Linear"
    };

    /// <summary>
    /// Gets the duration display string.
    /// </summary>
    public string DurationDisplay
    {
        get
        {
            if (DurationBeats <= 0)
                return "-";

            int bars = (int)(DurationBeats / 4);
            int beats = (int)(DurationBeats % 4);
            return bars > 0 ? $"{bars} bars {beats} beats" : $"{beats} beats";
        }
    }

    /// <summary>
    /// Creates a new tempo point.
    /// </summary>
    public TempoPoint()
    {
        Id = _nextId++;
    }

    /// <summary>
    /// Creates a new tempo point with specified values.
    /// </summary>
    public TempoPoint(double position, double tempo, TempoCurveType curveType = TempoCurveType.Linear) : this()
    {
        Position = position;
        Tempo = tempo;
        CurveType = curveType;
    }

    partial void OnPositionChanged(double value)
    {
        OnPropertyChanged(nameof(PositionDisplay));
    }

    partial void OnCurveTypeChanged(TempoCurveType value)
    {
        OnPropertyChanged(nameof(CurveTypeDisplay));
    }

    partial void OnDurationBeatsChanged(double value)
    {
        OnPropertyChanged(nameof(DurationDisplay));
    }
}

/// <summary>
/// Editor view for managing tempo changes in the project.
/// </summary>
public partial class TempoListEditor : UserControl, INotifyPropertyChanged
{
    private readonly ObservableCollection<TempoPoint> _tempoPoints = [];
    private int _beatsPerBar = 4;

    /// <summary>
    /// Gets the tempo points collection.
    /// </summary>
    public ObservableCollection<TempoPoint> TempoPoints => _tempoPoints;

    /// <summary>
    /// Gets whether there is a selection.
    /// </summary>
    public bool HasSelection => TempoGrid.SelectedItem != null;

    /// <summary>
    /// Gets the number of tempo points.
    /// </summary>
    public int PointCount => _tempoPoints.Count;

    /// <summary>
    /// Gets the minimum tempo.
    /// </summary>
    public double MinTempo => _tempoPoints.Count > 0 ? _tempoPoints.Min(p => p.Tempo) : 120.0;

    /// <summary>
    /// Gets the maximum tempo.
    /// </summary>
    public double MaxTempo => _tempoPoints.Count > 0 ? _tempoPoints.Max(p => p.Tempo) : 120.0;

    /// <summary>
    /// Gets or sets the beats per bar for position display.
    /// </summary>
    public int BeatsPerBar
    {
        get => _beatsPerBar;
        set
        {
            _beatsPerBar = value;
            OnPropertyChanged(nameof(BeatsPerBar));
            UpdateDurations();
        }
    }

    /// <summary>
    /// Event raised when tempo points are modified.
    /// </summary>
    public event EventHandler? TempoChanged;

    /// <summary>
    /// PropertyChanged event for INotifyPropertyChanged.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Creates a new TempoListEditor.
    /// </summary>
    public TempoListEditor()
    {
        InitializeComponent();
        DataContext = this;

        TempoGrid.ItemsSource = _tempoPoints;
        _tempoPoints.CollectionChanged += (s, e) =>
        {
            UpdateDurations();
            OnPropertyChanged(nameof(PointCount));
            OnPropertyChanged(nameof(MinTempo));
            OnPropertyChanged(nameof(MaxTempo));
        };

        InitializeSampleTempoPoints();
    }

    /// <summary>
    /// Initializes sample tempo points for demonstration.
    /// </summary>
    private void InitializeSampleTempoPoints()
    {
        _tempoPoints.Add(new TempoPoint(0, 120, TempoCurveType.Step));
        _tempoPoints.Add(new TempoPoint(64, 128, TempoCurveType.Linear));
        _tempoPoints.Add(new TempoPoint(96, 140, TempoCurveType.SCurve));
        _tempoPoints.Add(new TempoPoint(128, 120, TempoCurveType.Linear));
        UpdateDurations();
    }

    /// <summary>
    /// Adds a new tempo point.
    /// </summary>
    /// <param name="position">The position in beats.</param>
    /// <param name="tempo">The tempo in BPM.</param>
    /// <param name="curveType">The curve type.</param>
    /// <returns>The created tempo point.</returns>
    public TempoPoint AddTempoPoint(double position, double tempo, TempoCurveType curveType = TempoCurveType.Linear)
    {
        var point = new TempoPoint(position, tempo, curveType);
        _tempoPoints.Add(point);
        SortPoints();
        UpdateDurations();
        TempoChanged?.Invoke(this, EventArgs.Empty);
        return point;
    }

    /// <summary>
    /// Removes a tempo point.
    /// </summary>
    /// <param name="point">The point to remove.</param>
    public void RemoveTempoPoint(TempoPoint point)
    {
        // Don't allow removing the first point (always need at least one)
        if (_tempoPoints.Count <= 1)
        {
            MessageBox.Show("Cannot remove the last tempo point.", "Tempo Editor", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (point.Position == 0)
        {
            MessageBox.Show("Cannot remove the initial tempo point at position 0.", "Tempo Editor", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_tempoPoints.Remove(point))
        {
            UpdateDurations();
            TempoChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Gets the tempo at a specific position.
    /// </summary>
    /// <param name="position">The position in beats.</param>
    /// <returns>The tempo at that position.</returns>
    public double GetTempoAtPosition(double position)
    {
        if (_tempoPoints.Count == 0)
            return 120.0;

        var sorted = _tempoPoints.OrderBy(p => p.Position).ToList();

        // Find the surrounding points
        TempoPoint? before = null;
        TempoPoint? after = null;

        foreach (var point in sorted)
        {
            if (point.Position <= position)
            {
                before = point;
            }
            else if (after == null)
            {
                after = point;
                break;
            }
        }

        if (before == null)
            return sorted[0].Tempo;

        if (after == null || before.CurveType == TempoCurveType.Step)
            return before.Tempo;

        // Interpolate between points
        double t = (position - before.Position) / (after.Position - before.Position);

        if (before.CurveType == TempoCurveType.SCurve)
        {
            // S-curve (ease in/out)
            t = t * t * (3 - 2 * t);
        }

        return before.Tempo + (after.Tempo - before.Tempo) * t;
    }

    /// <summary>
    /// Gets the selected tempo point.
    /// </summary>
    public TempoPoint? GetSelectedPoint()
    {
        return TempoGrid.SelectedItem as TempoPoint;
    }

    private void SortPoints()
    {
        var sorted = _tempoPoints.OrderBy(p => p.Position).ToList();
        _tempoPoints.Clear();
        foreach (var point in sorted)
        {
            _tempoPoints.Add(point);
        }
    }

    private void UpdateDurations()
    {
        var sorted = _tempoPoints.OrderBy(p => p.Position).ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            if (i < sorted.Count - 1)
            {
                sorted[i].DurationBeats = sorted[i + 1].Position - sorted[i].Position;
            }
            else
            {
                sorted[i].DurationBeats = 0; // Last point has no duration
            }
        }

        OnPropertyChanged(nameof(MinTempo));
        OnPropertyChanged(nameof(MaxTempo));
    }

    private void AddPoint_Click(object sender, RoutedEventArgs e)
    {
        // Add at a reasonable position (after last point or at cursor position)
        double newPosition = _tempoPoints.Count > 0
            ? _tempoPoints.Max(p => p.Position) + 16 // 4 bars after last point
            : 0;

        double lastTempo = _tempoPoints.Count > 0
            ? _tempoPoints.OrderByDescending(p => p.Position).First().Tempo
            : 120.0;

        AddTempoPoint(newPosition, lastTempo, TempoCurveType.Linear);
    }

    private void RemovePoint_Click(object sender, RoutedEventArgs e)
    {
        if (TempoGrid.SelectedItem is TempoPoint point)
        {
            RemoveTempoPoint(point);
        }
    }

    private void TempoGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        // Re-sort and update after editing
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
        {
            SortPoints();
            UpdateDurations();
            TempoChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    private void TempoGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasSelection));
    }

    /// <summary>
    /// Raises the PropertyChanged event.
    /// </summary>
    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
