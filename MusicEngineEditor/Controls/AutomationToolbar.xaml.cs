// MusicEngineEditor - Automation Toolbar Control
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using MusicEngine.Core.Automation;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Recording mode for automation.
/// </summary>
public enum AutomationRecordingMode
{
    /// <summary>
    /// No recording, playback only.
    /// </summary>
    Off,

    /// <summary>
    /// Record only while touching a control, return to existing curve on release.
    /// </summary>
    Touch,

    /// <summary>
    /// Start recording on first touch, continue until playback stops.
    /// </summary>
    Latch,

    /// <summary>
    /// Always overwrite existing automation while playing.
    /// </summary>
    Write
}

/// <summary>
/// Toolbar control for automation recording and editing options.
/// </summary>
public partial class AutomationToolbar : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty RecordingModeProperty =
        DependencyProperty.Register(nameof(RecordingMode), typeof(AutomationRecordingMode), typeof(AutomationToolbar),
            new PropertyMetadata(AutomationRecordingMode.Off, OnRecordingModeChanged));

    public static readonly DependencyProperty IsArmedProperty =
        DependencyProperty.Register(nameof(IsArmed), typeof(bool), typeof(AutomationToolbar),
            new PropertyMetadata(false, OnIsArmedChanged));

    public static readonly DependencyProperty ShowAllLanesProperty =
        DependencyProperty.Register(nameof(ShowAllLanes), typeof(bool), typeof(AutomationToolbar),
            new PropertyMetadata(false));

    public static readonly DependencyProperty ShowUsedLanesOnlyProperty =
        DependencyProperty.Register(nameof(ShowUsedLanesOnly), typeof(bool), typeof(AutomationToolbar),
            new PropertyMetadata(false));

    public static readonly DependencyProperty SnapToGridProperty =
        DependencyProperty.Register(nameof(SnapToGrid), typeof(bool), typeof(AutomationToolbar),
            new PropertyMetadata(true));

    public static readonly DependencyProperty GridSubdivisionProperty =
        DependencyProperty.Register(nameof(GridSubdivision), typeof(double), typeof(AutomationToolbar),
            new PropertyMetadata(0.25));

    public static readonly DependencyProperty DefaultCurveTypeProperty =
        DependencyProperty.Register(nameof(DefaultCurveType), typeof(AutomationCurveType), typeof(AutomationToolbar),
            new PropertyMetadata(AutomationCurveType.Linear));

    /// <summary>
    /// Gets or sets the current recording mode.
    /// </summary>
    public AutomationRecordingMode RecordingMode
    {
        get => (AutomationRecordingMode)GetValue(RecordingModeProperty);
        set => SetValue(RecordingModeProperty, value);
    }

    /// <summary>
    /// Gets or sets whether automation recording is armed.
    /// </summary>
    public bool IsArmed
    {
        get => (bool)GetValue(IsArmedProperty);
        set => SetValue(IsArmedProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to show all automation lanes.
    /// </summary>
    public bool ShowAllLanes
    {
        get => (bool)GetValue(ShowAllLanesProperty);
        set => SetValue(ShowAllLanesProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to show only lanes with automation data.
    /// </summary>
    public bool ShowUsedLanesOnly
    {
        get => (bool)GetValue(ShowUsedLanesOnlyProperty);
        set => SetValue(ShowUsedLanesOnlyProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to snap points to the grid.
    /// </summary>
    public bool SnapToGrid
    {
        get => (bool)GetValue(SnapToGridProperty);
        set => SetValue(SnapToGridProperty, value);
    }

    /// <summary>
    /// Gets or sets the grid subdivision value (fraction of a beat).
    /// </summary>
    public double GridSubdivision
    {
        get => (double)GetValue(GridSubdivisionProperty);
        set => SetValue(GridSubdivisionProperty, value);
    }

    /// <summary>
    /// Gets or sets the default curve type for new points.
    /// </summary>
    public AutomationCurveType DefaultCurveType
    {
        get => (AutomationCurveType)GetValue(DefaultCurveTypeProperty);
        set => SetValue(DefaultCurveTypeProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Fired when the recording mode changes.
    /// </summary>
    public event EventHandler<AutomationRecordingMode>? RecordingModeChanged;

    /// <summary>
    /// Fired when armed state changes.
    /// </summary>
    public event EventHandler<bool>? ArmedChanged;

    /// <summary>
    /// Fired when show all lanes setting changes.
    /// </summary>
    public event EventHandler<bool>? ShowAllLanesChanged;

    /// <summary>
    /// Fired when show used lanes only setting changes.
    /// </summary>
    public event EventHandler<bool>? ShowUsedLanesOnlyChanged;

    /// <summary>
    /// Fired when snap to grid setting changes.
    /// </summary>
    public event EventHandler<bool>? SnapToGridChanged;

    /// <summary>
    /// Fired when grid subdivision changes.
    /// </summary>
    public event EventHandler<double>? GridSubdivisionChanged;

    /// <summary>
    /// Fired when default curve type changes.
    /// </summary>
    public event EventHandler<AutomationCurveType>? DefaultCurveTypeChanged;

    /// <summary>
    /// Fired when clear automation is requested.
    /// </summary>
    public event EventHandler? ClearRequested;

    /// <summary>
    /// Fired when copy is requested.
    /// </summary>
    public event EventHandler? CopyRequested;

    /// <summary>
    /// Fired when paste is requested.
    /// </summary>
    public event EventHandler? PasteRequested;

    #endregion

    #region Private Fields

    private Storyboard? _armPulseAnimation;

    #endregion

    #region Constructor

    public AutomationToolbar()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    #endregion

    #region Lifecycle

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _armPulseAnimation = ArmButton.Resources["ArmPulse"] as Storyboard;
    }

    #endregion

    #region Property Changed Handlers

    private static void OnRecordingModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AutomationToolbar toolbar)
        {
            toolbar.UpdateRecordingModeUI();
            toolbar.RecordingModeChanged?.Invoke(toolbar, (AutomationRecordingMode)e.NewValue);
        }
    }

    private static void OnIsArmedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AutomationToolbar toolbar)
        {
            toolbar.UpdateArmState();
            toolbar.ArmedChanged?.Invoke(toolbar, (bool)e.NewValue);
        }
    }

    private void UpdateRecordingModeUI()
    {
        OffModeButton.IsChecked = RecordingMode == AutomationRecordingMode.Off;
        TouchModeButton.IsChecked = RecordingMode == AutomationRecordingMode.Touch;
        LatchModeButton.IsChecked = RecordingMode == AutomationRecordingMode.Latch;
        WriteModeButton.IsChecked = RecordingMode == AutomationRecordingMode.Write;
    }

    private void UpdateArmState()
    {
        ArmButton.IsChecked = IsArmed;

        if (IsArmed && RecordingMode != AutomationRecordingMode.Off)
        {
            _armPulseAnimation?.Begin(ArmButton, true);
        }
        else
        {
            _armPulseAnimation?.Stop(ArmButton);
            ArmButton.Opacity = 1.0;
        }
    }

    #endregion

    #region UI Event Handlers

    private void RecordingMode_Checked(object sender, RoutedEventArgs e)
    {
        if (sender == OffModeButton)
            RecordingMode = AutomationRecordingMode.Off;
        else if (sender == TouchModeButton)
            RecordingMode = AutomationRecordingMode.Touch;
        else if (sender == LatchModeButton)
            RecordingMode = AutomationRecordingMode.Latch;
        else if (sender == WriteModeButton)
            RecordingMode = AutomationRecordingMode.Write;

        UpdateArmState();
    }

    private void ArmButton_Click(object sender, RoutedEventArgs e)
    {
        IsArmed = ArmButton.IsChecked == true;
    }

    private void ShowAllLanesButton_Click(object sender, RoutedEventArgs e)
    {
        ShowAllLanes = ShowAllLanesButton.IsChecked == true;
        if (ShowAllLanes)
        {
            ShowUsedLanesButton.IsChecked = false;
            ShowUsedLanesOnly = false;
        }
        ShowAllLanesChanged?.Invoke(this, ShowAllLanes);
    }

    private void ShowUsedLanesButton_Click(object sender, RoutedEventArgs e)
    {
        ShowUsedLanesOnly = ShowUsedLanesButton.IsChecked == true;
        if (ShowUsedLanesOnly)
        {
            ShowAllLanesButton.IsChecked = false;
            ShowAllLanes = false;
        }
        ShowUsedLanesOnlyChanged?.Invoke(this, ShowUsedLanesOnly);
    }

    private void SnapToGridButton_Click(object sender, RoutedEventArgs e)
    {
        SnapToGrid = SnapToGridButton.IsChecked == true;
        SnapToGridChanged?.Invoke(this, SnapToGrid);
    }

    private void GridSizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        GridSubdivision = GridSizeCombo.SelectedIndex switch
        {
            0 => 1.0,       // 1/1
            1 => 0.5,       // 1/2
            2 => 0.25,      // 1/4
            3 => 0.125,     // 1/8
            4 => 0.0625,    // 1/16
            5 => 0.03125,   // 1/32
            _ => 0.25
        };
        GridSubdivisionChanged?.Invoke(this, GridSubdivision);
    }

    private void DefaultCurveCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        DefaultCurveType = DefaultCurveCombo.SelectedIndex switch
        {
            0 => AutomationCurveType.Linear,
            1 => AutomationCurveType.Bezier,
            2 => AutomationCurveType.Step,
            3 => AutomationCurveType.Exponential,
            4 => AutomationCurveType.SCurve,
            _ => AutomationCurveType.Linear
        };
        DefaultCurveTypeChanged?.Invoke(this, DefaultCurveType);
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        ClearRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        CopyRequested?.Invoke(this, EventArgs.Empty);
    }

    private void PasteButton_Click(object sender, RoutedEventArgs e)
    {
        PasteRequested?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets the grid subdivision using a note value string.
    /// </summary>
    /// <param name="noteValue">Note value (e.g., "1/4", "1/8").</param>
    public void SetGridSubdivision(string noteValue)
    {
        int index = noteValue switch
        {
            "1/1" or "1" => 0,
            "1/2" => 1,
            "1/4" => 2,
            "1/8" => 3,
            "1/16" => 4,
            "1/32" => 5,
            _ => 2
        };
        GridSizeCombo.SelectedIndex = index;
    }

    /// <summary>
    /// Sets the default curve type.
    /// </summary>
    /// <param name="curveType">The curve type.</param>
    public void SetDefaultCurveType(AutomationCurveType curveType)
    {
        DefaultCurveCombo.SelectedIndex = curveType switch
        {
            AutomationCurveType.Linear => 0,
            AutomationCurveType.Bezier => 1,
            AutomationCurveType.Step => 2,
            AutomationCurveType.Exponential => 3,
            AutomationCurveType.SCurve => 4,
            _ => 0
        };
    }

    #endregion
}
