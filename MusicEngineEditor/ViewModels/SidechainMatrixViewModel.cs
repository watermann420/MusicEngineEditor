// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for sidechain routing matrix control.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngine.Core.Routing;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// Represents a cell in the sidechain routing matrix.
/// </summary>
public partial class SidechainMatrixCell : ObservableObject
{
    [ObservableProperty]
    private string _sourceTrackName = string.Empty;

    [ObservableProperty]
    private string _destinationTrackName = string.Empty;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private float _gain = 1.0f;

    [ObservableProperty]
    private Guid? _routeId;

    /// <summary>
    /// Gets or sets whether this is a diagonal cell (same source and destination).
    /// </summary>
    [ObservableProperty]
    private bool _isDiagonal;

    /// <summary>
    /// Gets the display tooltip for this cell.
    /// </summary>
    public string Tooltip => IsDiagonal
        ? "Cannot route track to itself"
        : IsActive
            ? $"{SourceTrackName} -> {DestinationTrackName} (Gain: {Gain:F1}x)"
            : $"Click to route {SourceTrackName} -> {DestinationTrackName}";
}

/// <summary>
/// Represents a sidechain routing preset.
/// </summary>
public partial class SidechainPreset : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    /// <summary>
    /// Gets or sets the routing pairs for this preset (source pattern -> destination pattern).
    /// </summary>
    public List<(string SourcePattern, string DestinationPattern)> RoutingPatterns { get; set; } = new();
}

/// <summary>
/// ViewModel for the sidechain matrix control.
/// Provides a grid-based interface for configuring sidechain routing between tracks.
/// </summary>
public partial class SidechainMatrixViewModel : ViewModelBase
{
    private SidechainMatrix? _sidechainMatrix;
    private readonly List<string> _trackNames = new();

    #region Observable Properties

    [ObservableProperty]
    private int _trackCount;

    [ObservableProperty]
    private int _activeRoutingCount;

    [ObservableProperty]
    private string _statusText = "No tracks loaded";

    [ObservableProperty]
    private SidechainMatrixCell? _selectedCell;

    [ObservableProperty]
    private SidechainPreset? _selectedPreset;

    [ObservableProperty]
    private float _selectedCellGain = 1.0f;

    [ObservableProperty]
    private bool _isGainEditorVisible;

    #endregion

    /// <summary>
    /// Collection of track names for row/column headers.
    /// </summary>
    public ObservableCollection<string> TrackNames { get; } = new();

    /// <summary>
    /// Collection of matrix cells representing all routing possibilities.
    /// </summary>
    public ObservableCollection<SidechainMatrixCell> MatrixCells { get; } = new();

    /// <summary>
    /// Collection of available presets.
    /// </summary>
    public ObservableCollection<SidechainPreset> Presets { get; } = new();

    /// <summary>
    /// Event raised when a routing changes.
    /// </summary>
    public event EventHandler<SidechainMatrixCell>? RoutingChanged;

    /// <summary>
    /// Creates a new SidechainMatrixViewModel.
    /// </summary>
    public SidechainMatrixViewModel()
    {
        InitializePresets();
    }

    /// <summary>
    /// Initializes the view model with a sidechain matrix and track names.
    /// </summary>
    /// <param name="sidechainMatrix">The sidechain matrix to manage.</param>
    /// <param name="trackNames">The list of track names.</param>
    public void Initialize(SidechainMatrix sidechainMatrix, IEnumerable<string> trackNames)
    {
        if (_sidechainMatrix != null)
        {
            _sidechainMatrix.RouteCreated -= OnRouteCreated;
            _sidechainMatrix.RouteRemoved -= OnRouteRemoved;
            _sidechainMatrix.RouteModified -= OnRouteModified;
        }

        _sidechainMatrix = sidechainMatrix;
        _trackNames.Clear();
        _trackNames.AddRange(trackNames);

        if (_sidechainMatrix != null)
        {
            _sidechainMatrix.RouteCreated += OnRouteCreated;
            _sidechainMatrix.RouteRemoved += OnRouteRemoved;
            _sidechainMatrix.RouteModified += OnRouteModified;
        }

        RefreshMatrix();
    }

    /// <summary>
    /// Updates the track list and refreshes the matrix.
    /// </summary>
    /// <param name="trackNames">The new list of track names.</param>
    public void UpdateTracks(IEnumerable<string> trackNames)
    {
        _trackNames.Clear();
        _trackNames.AddRange(trackNames);
        RefreshMatrix();
    }

    #region Commands

    [RelayCommand]
    private void ToggleRouting(SidechainMatrixCell? cell)
    {
        if (cell == null || cell.IsDiagonal || _sidechainMatrix == null)
            return;

        if (cell.IsActive)
        {
            // Remove the routing
            if (cell.RouteId.HasValue)
            {
                _sidechainMatrix.RemoveRoute(cell.RouteId.Value);
            }
            cell.IsActive = false;
            cell.RouteId = null;
        }
        else
        {
            // Create new routing
            var route = _sidechainMatrix.CreateRoute(cell.SourceTrackName, cell.DestinationTrackName);
            cell.IsActive = true;
            cell.RouteId = route.Id;
            cell.Gain = route.Gain;
        }

        UpdateActiveRoutingCount();
        RoutingChanged?.Invoke(this, cell);
    }

    [RelayCommand]
    private void ClearAllRoutings()
    {
        if (_sidechainMatrix == null)
            return;

        _sidechainMatrix.Clear();

        foreach (var cell in MatrixCells)
        {
            cell.IsActive = false;
            cell.RouteId = null;
            cell.Gain = 1.0f;
        }

        UpdateActiveRoutingCount();
        StatusText = "All routings cleared";
    }

    [RelayCommand]
    private void ApplyPreset(SidechainPreset? preset)
    {
        if (preset == null || _sidechainMatrix == null)
            return;

        // Clear existing routings first
        ClearAllRoutings();

        // Apply preset patterns
        foreach (var (sourcePattern, destPattern) in preset.RoutingPatterns)
        {
            // Find matching source tracks
            var sourceTracks = _trackNames.Where(t =>
                t.Contains(sourcePattern, StringComparison.OrdinalIgnoreCase)).ToList();

            // Find matching destination tracks
            var destTracks = _trackNames.Where(t =>
                t.Contains(destPattern, StringComparison.OrdinalIgnoreCase)).ToList();

            // Create routings for all matching combinations
            foreach (var source in sourceTracks)
            {
                foreach (var dest in destTracks)
                {
                    if (source != dest)
                    {
                        var cell = MatrixCells.FirstOrDefault(c =>
                            c.SourceTrackName == source && c.DestinationTrackName == dest);

                        if (cell != null && !cell.IsActive)
                        {
                            var route = _sidechainMatrix.CreateRoute(source, dest);
                            cell.IsActive = true;
                            cell.RouteId = route.Id;
                        }
                    }
                }
            }
        }

        UpdateActiveRoutingCount();
        StatusText = $"Applied preset: {preset.Name}";
        SelectedPreset = preset;
    }

    [RelayCommand]
    private void ShowGainEditor(SidechainMatrixCell? cell)
    {
        if (cell == null || !cell.IsActive)
        {
            IsGainEditorVisible = false;
            return;
        }

        SelectedCell = cell;
        SelectedCellGain = cell.Gain;
        IsGainEditorVisible = true;
    }

    [RelayCommand]
    private void ApplyGain()
    {
        if (SelectedCell == null || _sidechainMatrix == null || !SelectedCell.RouteId.HasValue)
            return;

        var route = _sidechainMatrix.GetRoute(SelectedCell.RouteId.Value);
        if (route != null)
        {
            _sidechainMatrix.SetRouteGain(route, SelectedCellGain);
            SelectedCell.Gain = SelectedCellGain;
        }

        IsGainEditorVisible = false;
        RoutingChanged?.Invoke(this, SelectedCell);
    }

    [RelayCommand]
    private void CancelGainEdit()
    {
        IsGainEditorVisible = false;
        SelectedCell = null;
    }

    [RelayCommand]
    private void Refresh()
    {
        RefreshMatrix();
    }

    #endregion

    #region Private Methods

    private void InitializePresets()
    {
        Presets.Clear();

        Presets.Add(new SidechainPreset
        {
            Name = "Kick to Bass",
            Description = "Classic kick drum sidechain compression on bass",
            RoutingPatterns = new List<(string, string)>
            {
                ("Kick", "Bass"),
                ("Kick", "Sub")
            }
        });

        Presets.Add(new SidechainPreset
        {
            Name = "Kick to Synths",
            Description = "Pump effect on synthesizers",
            RoutingPatterns = new List<(string, string)>
            {
                ("Kick", "Synth"),
                ("Kick", "Pad"),
                ("Kick", "Lead")
            }
        });

        Presets.Add(new SidechainPreset
        {
            Name = "Vocals Duck Music",
            Description = "Duck music when vocals are present",
            RoutingPatterns = new List<(string, string)>
            {
                ("Vocal", "Music"),
                ("Vocal", "Inst"),
                ("Vocal", "Mix")
            }
        });

        Presets.Add(new SidechainPreset
        {
            Name = "Drums to All",
            Description = "Full drum bus sidechaining to all other tracks",
            RoutingPatterns = new List<(string, string)>
            {
                ("Drum", "Bass"),
                ("Drum", "Synth"),
                ("Drum", "Pad"),
                ("Drum", "Guitar"),
                ("Drum", "Keys")
            }
        });

        Presets.Add(new SidechainPreset
        {
            Name = "Voice-Over",
            Description = "Duck background music for voice-over",
            RoutingPatterns = new List<(string, string)>
            {
                ("Voice", "Music"),
                ("VO", "Music"),
                ("Narr", "Music")
            }
        });
    }

    private void RefreshMatrix()
    {
        TrackNames.Clear();
        MatrixCells.Clear();

        foreach (var name in _trackNames)
        {
            TrackNames.Add(name);
        }

        TrackCount = _trackNames.Count;

        // Create cells for the matrix (sources as rows, destinations as columns)
        for (int sourceIndex = 0; sourceIndex < _trackNames.Count; sourceIndex++)
        {
            for (int destIndex = 0; destIndex < _trackNames.Count; destIndex++)
            {
                var sourceName = _trackNames[sourceIndex];
                var destName = _trackNames[destIndex];

                var cell = new SidechainMatrixCell
                {
                    SourceTrackName = sourceName,
                    DestinationTrackName = destName,
                    IsDiagonal = sourceIndex == destIndex
                };

                // Check if there's an existing route
                if (_sidechainMatrix != null)
                {
                    var routes = _sidechainMatrix.GetRoutesForSource(sourceName);
                    var existingRoute = routes.FirstOrDefault(r =>
                        r.TargetEffectName.Equals(destName, StringComparison.OrdinalIgnoreCase));

                    if (existingRoute != null)
                    {
                        cell.IsActive = existingRoute.IsActive;
                        cell.RouteId = existingRoute.Id;
                        cell.Gain = existingRoute.Gain;
                    }
                }

                MatrixCells.Add(cell);
            }
        }

        UpdateActiveRoutingCount();
    }

    private void UpdateActiveRoutingCount()
    {
        ActiveRoutingCount = MatrixCells.Count(c => c.IsActive);
        StatusText = ActiveRoutingCount > 0
            ? $"{ActiveRoutingCount} active routing{(ActiveRoutingCount == 1 ? "" : "s")}"
            : "No active routings";
    }

    private void OnRouteCreated(object? sender, SidechainRoute route)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var cell = MatrixCells.FirstOrDefault(c =>
                c.SourceTrackName == route.SourceName &&
                c.DestinationTrackName == route.TargetEffectName);

            if (cell != null)
            {
                cell.IsActive = true;
                cell.RouteId = route.Id;
                cell.Gain = route.Gain;
            }

            UpdateActiveRoutingCount();
        });
    }

    private void OnRouteRemoved(object? sender, SidechainRoute route)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var cell = MatrixCells.FirstOrDefault(c => c.RouteId == route.Id);
            if (cell != null)
            {
                cell.IsActive = false;
                cell.RouteId = null;
                cell.Gain = 1.0f;
            }

            UpdateActiveRoutingCount();
        });
    }

    private void OnRouteModified(object? sender, SidechainRoute route)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var cell = MatrixCells.FirstOrDefault(c => c.RouteId == route.Id);
            if (cell != null)
            {
                cell.IsActive = route.IsActive;
                cell.Gain = route.Gain;
            }

            UpdateActiveRoutingCount();
        });
    }

    #endregion

    /// <summary>
    /// Cleans up resources and event handlers.
    /// </summary>
    public void Shutdown()
    {
        if (_sidechainMatrix != null)
        {
            _sidechainMatrix.RouteCreated -= OnRouteCreated;
            _sidechainMatrix.RouteRemoved -= OnRouteRemoved;
            _sidechainMatrix.RouteModified -= OnRouteModified;
        }
    }
}
