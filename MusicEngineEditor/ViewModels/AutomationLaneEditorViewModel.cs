// MusicEngineEditor - Automation Lane Editor ViewModel
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngine.Core.Automation;
using MusicEngineEditor.Commands;
using MusicEngineEditor.Controls;
using MusicEngineEditor.Services;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// Enhanced ViewModel for automation lane editing with recording support.
/// </summary>
public partial class AutomationLaneEditorViewModel : ViewModelBase
{
    #region Private Fields

    private readonly AutomationRecordingService _recordingService;

    #endregion

    #region Observable Properties

    /// <summary>
    /// Gets or sets the automation lane being edited.
    /// </summary>
    [ObservableProperty]
    private AutomationLane? _lane;

    /// <summary>
    /// Gets or sets the selected parameter to automate.
    /// </summary>
    [ObservableProperty]
    private AutomationParameterInfo? _selectedParameter;

    /// <summary>
    /// Gets or sets the collection of automation points.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<AutomationPointViewModel> _points = [];

    /// <summary>
    /// Gets or sets the collection of selected points.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<AutomationPointViewModel> _selectedPoints = [];

    /// <summary>
    /// Gets or sets the recording mode.
    /// </summary>
    [ObservableProperty]
    private AutomationRecordingMode _recordingMode = AutomationRecordingMode.Off;

    /// <summary>
    /// Gets or sets whether recording is armed.
    /// </summary>
    [ObservableProperty]
    private bool _isArmed;

    /// <summary>
    /// Gets or sets whether the lane is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _isEnabled = true;

    /// <summary>
    /// Gets or sets whether the lane is muted.
    /// </summary>
    [ObservableProperty]
    private bool _isMuted;

    /// <summary>
    /// Gets or sets whether the lane is soloed.
    /// </summary>
    [ObservableProperty]
    private bool _isSoloed;

    /// <summary>
    /// Gets or sets whether the lane is visible.
    /// </summary>
    [ObservableProperty]
    private bool _isVisible = true;

    /// <summary>
    /// Gets or sets whether the lane is expanded.
    /// </summary>
    [ObservableProperty]
    private bool _isExpanded = true;

    /// <summary>
    /// Gets or sets the lane name.
    /// </summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>
    /// Gets or sets the lane color.
    /// </summary>
    [ObservableProperty]
    private string _color = "#4B6EAF";

    /// <summary>
    /// Gets or sets the minimum value.
    /// </summary>
    [ObservableProperty]
    private float _minValue;

    /// <summary>
    /// Gets or sets the maximum value.
    /// </summary>
    [ObservableProperty]
    private float _maxValue = 1f;

    /// <summary>
    /// Gets or sets the default value.
    /// </summary>
    [ObservableProperty]
    private float _defaultValue;

    /// <summary>
    /// Gets or sets the current value at playback position.
    /// </summary>
    [ObservableProperty]
    private float _currentValue;

    /// <summary>
    /// Gets or sets the current playback time.
    /// </summary>
    [ObservableProperty]
    private double _currentTime;

    /// <summary>
    /// Gets or sets whether snap to grid is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _snapToGrid = true;

    /// <summary>
    /// Gets or sets the grid subdivision.
    /// </summary>
    [ObservableProperty]
    private double _gridSubdivision = 0.25;

    /// <summary>
    /// Gets or sets the default curve type.
    /// </summary>
    [ObservableProperty]
    private AutomationCurveType _defaultCurveType = AutomationCurveType.Linear;

    /// <summary>
    /// Gets the unit display string for the parameter.
    /// </summary>
    [ObservableProperty]
    private string _unitDisplay = string.Empty;

    /// <summary>
    /// Gets or sets the value range display string.
    /// </summary>
    [ObservableProperty]
    private string _valueRangeDisplay = "0.0 - 1.0";

    /// <summary>
    /// Gets or sets the point count.
    /// </summary>
    [ObservableProperty]
    private int _pointCount;

    #endregion

    #region Collections

    /// <summary>
    /// Gets the available parameters for automation.
    /// </summary>
    public ObservableCollection<AutomationParameterInfo> AvailableParameters { get; } = [];

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new AutomationLaneEditorViewModel.
    /// </summary>
    public AutomationLaneEditorViewModel()
    {
        _recordingService = AutomationRecordingService.Instance;
    }

    /// <summary>
    /// Creates a new AutomationLaneEditorViewModel with an existing lane.
    /// </summary>
    /// <param name="lane">The automation lane to edit.</param>
    public AutomationLaneEditorViewModel(AutomationLane lane) : this()
    {
        SetLane(lane);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets the automation lane to edit.
    /// </summary>
    /// <param name="lane">The automation lane.</param>
    public void SetLane(AutomationLane lane)
    {
        if (Lane != null)
        {
            Lane.Curve.CurveChanged -= OnCurveChanged;
            Lane.LaneChanged -= OnLaneChanged;
            Lane.ValueApplied -= OnValueApplied;
        }

        Lane = lane;

        if (Lane != null)
        {
            Lane.Curve.CurveChanged += OnCurveChanged;
            Lane.LaneChanged += OnLaneChanged;
            Lane.ValueApplied += OnValueApplied;

            SyncFromLane();
            RefreshPoints();
        }
    }

    /// <summary>
    /// Populates available parameters from an automatable target.
    /// </summary>
    /// <param name="target">The automatable target.</param>
    public void PopulateParameters(IAutomatable target)
    {
        AvailableParameters.Clear();

        foreach (var paramName in target.AutomatableParameters)
        {
            var info = new AutomationParameterInfo
            {
                Name = paramName,
                DisplayName = paramName,
                MinValue = target.GetParameterMinValue(paramName),
                MaxValue = target.GetParameterMaxValue(paramName),
                DefaultValue = target.GetParameterDefaultValue(paramName)
            };
            AvailableParameters.Add(info);
        }

        // Add common parameters
        AddCommonParameters();
    }

    /// <summary>
    /// Adds common mixer parameters (Volume, Pan, etc.).
    /// </summary>
    public void AddCommonParameters()
    {
        var commonParams = new[]
        {
            new AutomationParameterInfo { Name = "Volume", DisplayName = "Volume", MinValue = 0f, MaxValue = 1f, DefaultValue = 0.8f, Unit = "dB" },
            new AutomationParameterInfo { Name = "Pan", DisplayName = "Pan", MinValue = -1f, MaxValue = 1f, DefaultValue = 0f, Unit = "L/R" },
            new AutomationParameterInfo { Name = "Mute", DisplayName = "Mute", MinValue = 0f, MaxValue = 1f, DefaultValue = 0f, Unit = "" },
            new AutomationParameterInfo { Name = "Send1", DisplayName = "Send 1", MinValue = 0f, MaxValue = 1f, DefaultValue = 0f, Unit = "" },
            new AutomationParameterInfo { Name = "Send2", DisplayName = "Send 2", MinValue = 0f, MaxValue = 1f, DefaultValue = 0f, Unit = "" }
        };

        foreach (var param in commonParams)
        {
            if (!AvailableParameters.Any(p => p.Name == param.Name))
            {
                AvailableParameters.Add(param);
            }
        }
    }

    /// <summary>
    /// Updates the current time and value display.
    /// </summary>
    /// <param name="time">The current playback time.</param>
    public void UpdatePlaybackPosition(double time)
    {
        CurrentTime = time;
        if (Lane != null)
        {
            CurrentValue = Lane.GetValueAtTime(time);
        }
    }

    #endregion

    #region Commands

    /// <summary>
    /// Adds a point at the specified time and value.
    /// </summary>
    [RelayCommand]
    private void AddPoint(PointAddRequest request)
    {
        if (Lane == null) return;

        double time = request.Time;
        float value = request.Value;

        if (SnapToGrid && GridSubdivision > 0)
        {
            time = Math.Round(time / GridSubdivision) * GridSubdivision;
        }

        var command = new AutomationPointAddCommand(Lane, time, value, DefaultCurveType);
        EditorUndoService.Instance.Execute(command);
    }

    /// <summary>
    /// Deletes a single point.
    /// </summary>
    [RelayCommand]
    private void DeletePoint(AutomationPointViewModel? pointVm)
    {
        if (Lane == null || pointVm?.Point == null) return;

        var command = new AutomationPointDeleteCommand(Lane, pointVm.Point);
        EditorUndoService.Instance.Execute(command);
    }

    /// <summary>
    /// Deletes all selected points.
    /// </summary>
    [RelayCommand]
    private void DeleteSelectedPoints()
    {
        if (Lane == null || SelectedPoints.Count == 0) return;

        var pointsToDelete = SelectedPoints
            .Where(p => p.Point != null)
            .Select(p => p.Point!)
            .ToList();

        var command = new AutomationPointDeleteCommand(Lane, pointsToDelete);
        EditorUndoService.Instance.Execute(command);

        SelectedPoints.Clear();
    }

    /// <summary>
    /// Clears all points in the lane.
    /// </summary>
    [RelayCommand]
    private void ClearLane()
    {
        if (Lane == null) return;

        var command = new AutomationClearCommand(Lane);
        EditorUndoService.Instance.Execute(command);
    }

    /// <summary>
    /// Selects all points.
    /// </summary>
    [RelayCommand]
    private void SelectAll()
    {
        SelectedPoints.Clear();
        foreach (var point in Points)
        {
            point.IsSelected = true;
            SelectedPoints.Add(point);
        }
    }

    /// <summary>
    /// Clears the selection.
    /// </summary>
    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var point in SelectedPoints)
        {
            point.IsSelected = false;
        }
        SelectedPoints.Clear();
    }

    /// <summary>
    /// Toggles mute state.
    /// </summary>
    [RelayCommand]
    private void ToggleMute()
    {
        IsMuted = !IsMuted;
        if (Lane != null) Lane.IsMuted = IsMuted;
    }

    /// <summary>
    /// Toggles solo state.
    /// </summary>
    [RelayCommand]
    private void ToggleSolo()
    {
        IsSoloed = !IsSoloed;
        if (Lane != null) Lane.IsSoloed = IsSoloed;
    }

    /// <summary>
    /// Toggles armed state.
    /// </summary>
    [RelayCommand]
    private void ToggleArm()
    {
        IsArmed = !IsArmed;
        if (Lane != null) Lane.IsArmed = IsArmed;
    }

    /// <summary>
    /// Resets the lane to default value.
    /// </summary>
    [RelayCommand]
    private void ResetToDefault()
    {
        Lane?.ResetToDefault();
    }

    /// <summary>
    /// Starts recording.
    /// </summary>
    [RelayCommand]
    private void StartRecording()
    {
        if (Lane == null || !IsArmed || RecordingMode == AutomationRecordingMode.Off) return;

        _recordingService.StartRecording(Lane, RecordingMode);
    }

    /// <summary>
    /// Stops recording.
    /// </summary>
    [RelayCommand]
    private void StopRecording()
    {
        _recordingService.StopRecording();
    }

    #endregion

    #region Property Changed Handlers

    partial void OnLaneChanged(AutomationLane? value)
    {
        if (value != null)
        {
            SyncFromLane();
            RefreshPoints();
        }
    }

    partial void OnSelectedParameterChanged(AutomationParameterInfo? value)
    {
        if (value != null && Lane != null)
        {
            Lane.ParameterName = value.Name;
            MinValue = value.MinValue;
            MaxValue = value.MaxValue;
            DefaultValue = value.DefaultValue;
            UnitDisplay = value.Unit;
            UpdateValueRangeDisplay();
        }
    }

    partial void OnIsArmedChanged(bool value)
    {
        if (Lane != null) Lane.IsArmed = value;
    }

    partial void OnIsMutedChanged(bool value)
    {
        if (Lane != null) Lane.IsMuted = value;
    }

    partial void OnIsSoloedChanged(bool value)
    {
        if (Lane != null) Lane.IsSoloed = value;
    }

    partial void OnMinValueChanged(float value)
    {
        UpdateValueRangeDisplay();
    }

    partial void OnMaxValueChanged(float value)
    {
        UpdateValueRangeDisplay();
    }

    #endregion

    #region Private Methods

    private void SyncFromLane()
    {
        if (Lane == null) return;

        Name = Lane.Name;
        Color = Lane.Color;
        IsEnabled = Lane.Enabled;
        IsMuted = Lane.IsMuted;
        IsSoloed = Lane.IsSoloed;
        IsArmed = Lane.IsArmed;
        MinValue = Lane.MinValue;
        MaxValue = Lane.MaxValue;
        DefaultValue = Lane.DefaultValue;
        CurrentValue = Lane.LastAppliedValue;
        PointCount = Lane.Curve.Count;

        UpdateValueRangeDisplay();
    }

    private void RefreshPoints()
    {
        Points.Clear();

        if (Lane == null) return;

        foreach (var point in Lane.Curve.Points)
        {
            var pointVm = new AutomationPointViewModel(point);
            Points.Add(pointVm);
        }

        PointCount = Points.Count;
    }

    private void UpdateValueRangeDisplay()
    {
        ValueRangeDisplay = $"{MinValue:F1} - {MaxValue:F1}";
        if (!string.IsNullOrEmpty(UnitDisplay))
        {
            ValueRangeDisplay += $" {UnitDisplay}";
        }
    }

    private void OnCurveChanged(object? sender, EventArgs e)
    {
        RefreshPoints();
    }

    private void OnLaneChanged(object? sender, EventArgs e)
    {
        SyncFromLane();
    }

    private void OnValueApplied(object? sender, AutomationValueAppliedEventArgs e)
    {
        CurrentValue = e.Value;
    }

    #endregion
}

/// <summary>
/// ViewModel for a single automation point.
/// </summary>
public partial class AutomationPointViewModel : ViewModelBase
{
    [ObservableProperty]
    private AutomationPoint? _point;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private double _time;

    [ObservableProperty]
    private float _value;

    [ObservableProperty]
    private AutomationCurveType _curveType;

    public AutomationPointViewModel()
    {
    }

    public AutomationPointViewModel(AutomationPoint point)
    {
        Point = point;
        SyncFromPoint();
    }

    private void SyncFromPoint()
    {
        if (Point == null) return;

        Time = Point.Time;
        Value = Point.Value;
        CurveType = Point.CurveType;
        IsSelected = Point.IsSelected;
    }
}

/// <summary>
/// Information about an automatable parameter.
/// </summary>
public class AutomationParameterInfo
{
    /// <summary>
    /// Gets or sets the parameter name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the minimum value.
    /// </summary>
    public float MinValue { get; set; }

    /// <summary>
    /// Gets or sets the maximum value.
    /// </summary>
    public float MaxValue { get; set; } = 1f;

    /// <summary>
    /// Gets or sets the default value.
    /// </summary>
    public float DefaultValue { get; set; }

    /// <summary>
    /// Gets or sets the unit string.
    /// </summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the category.
    /// </summary>
    public string Category { get; set; } = "General";

    public override string ToString() => DisplayName;
}

/// <summary>
/// Request data for adding a point.
/// </summary>
public readonly record struct PointAddRequest(double Time, float Value);
