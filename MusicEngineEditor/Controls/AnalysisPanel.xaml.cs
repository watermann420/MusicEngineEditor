using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using MusicEngine.Core.Analysis;
using MusicEngineEditor.Services;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Combined analysis panel containing spectrum, goniometer, correlation, and true peak displays.
/// Supports tabs/sections, enable/disable toggles per analyzer, and compact/expanded modes.
/// </summary>
public partial class AnalysisPanel : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(AnalysisViewModel), typeof(AnalysisPanel),
            new PropertyMetadata(null, OnViewModelChanged));

    public static readonly DependencyProperty IsCompactModeProperty =
        DependencyProperty.Register(nameof(IsCompactMode), typeof(bool), typeof(AnalysisPanel),
            new PropertyMetadata(false, OnCompactModeChanged));

    /// <summary>
    /// Gets or sets the analysis view model.
    /// </summary>
    public AnalysisViewModel? ViewModel
    {
        get => (AnalysisViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <summary>
    /// Gets or sets whether compact mode is enabled.
    /// </summary>
    public bool IsCompactMode
    {
        get => (bool)GetValue(IsCompactModeProperty);
        set => SetValue(IsCompactModeProperty, value);
    }

    #endregion

    #region Private Fields

    private readonly AnalysisService _analysisService;
    private bool _isInitialized;

    #endregion

    #region Constructor

    public AnalysisPanel()
    {
        // Add the bool to visibility converter before InitializeComponent
        Resources.Add("BoolToVisConverter", new BooleanToVisibilityConverter());

        InitializeComponent();

        _analysisService = AnalysisService.Instance;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    #endregion

    #region Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = true;

        // Subscribe to analysis service events
        _analysisService.SpectrumUpdated += OnSpectrumUpdated;
        _analysisService.CorrelationUpdated += OnCorrelationUpdated;
        _analysisService.PeakUpdated += OnPeakUpdated;
        _analysisService.GoniometerUpdated += OnGoniometerUpdated;

        // Initialize with default ViewModel if none set
        if (ViewModel == null)
        {
            ViewModel = new AnalysisViewModel();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;

        // Unsubscribe from events
        _analysisService.SpectrumUpdated -= OnSpectrumUpdated;
        _analysisService.CorrelationUpdated -= OnCorrelationUpdated;
        _analysisService.PeakUpdated -= OnPeakUpdated;
        _analysisService.GoniometerUpdated -= OnGoniometerUpdated;
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AnalysisPanel panel && panel._isInitialized)
        {
            panel.DataContext = e.NewValue;
        }
    }

    private static void OnCompactModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AnalysisPanel panel && panel._isInitialized)
        {
            panel.UpdateCompactLayout((bool)e.NewValue);
        }
    }

    #endregion

    #region Analysis Event Handlers

    private void OnSpectrumUpdated(object? sender, SpectrumEventArgs e)
    {
        if (!_isInitialized || !SpectrumToggle.IsChecked == true) return;

        SpectrumAnalyzer.Magnitudes = e.Magnitudes;
        SpectrumAnalyzer.PeakMagnitudes = e.Peaks;
        SpectrumAnalyzer.Frequencies = e.Frequencies;
    }

    private void OnCorrelationUpdated(object? sender, CorrelationEventArgs e)
    {
        if (!_isInitialized || !CorrelationToggle.IsChecked == true) return;

        CorrelationMeter.Correlation = e.Correlation;
        CorrelationMeter.MidLevel = e.MidLevel;
        CorrelationMeter.SideLevel = e.SideLevel;
        CorrelationMeter.MsRatio = e.MSRatio;
    }

    private void OnPeakUpdated(object? sender, PeakEventArgs e)
    {
        if (!_isInitialized || !PeakToggle.IsChecked == true) return;

        if (e.CurrentPeaks.Length >= 1)
        {
            TruePeakMeter.LeftPeak = e.CurrentPeaks[0];
        }
        if (e.CurrentPeaks.Length >= 2)
        {
            TruePeakMeter.RightPeak = e.CurrentPeaks[1];
        }
        if (e.MaxPeaks.Length >= 1)
        {
            TruePeakMeter.LeftMaxPeak = e.MaxPeaks[0];
        }
        if (e.MaxPeaks.Length >= 2)
        {
            TruePeakMeter.RightMaxPeak = e.MaxPeaks[1];
        }

        TruePeakMeter.MaxTruePeakDbtp = e.MaxTruePeakDbtp;
        TruePeakMeter.HasClipped = e.MaxTruePeak >= 1.0f;
    }

    private void OnGoniometerUpdated(object? sender, GoniometerEventArgs e)
    {
        if (!_isInitialized || !GoniometerToggle.IsChecked == true) return;

        Goniometer.Points = e.Points;
    }

    #endregion

    #region Button Event Handlers

    private void OnSpectrumToggleClick(object sender, RoutedEventArgs e)
    {
        _analysisService.SpectrumEnabled = SpectrumToggle.IsChecked == true;
        ViewModel?.ToggleSpectrumCommand.Execute(null);
    }

    private void OnGoniometerToggleClick(object sender, RoutedEventArgs e)
    {
        _analysisService.GoniometerEnabled = GoniometerToggle.IsChecked == true;
        ViewModel?.ToggleGoniometerCommand.Execute(null);
    }

    private void OnCorrelationToggleClick(object sender, RoutedEventArgs e)
    {
        _analysisService.CorrelationEnabled = CorrelationToggle.IsChecked == true;
        ViewModel?.ToggleCorrelationCommand.Execute(null);
    }

    private void OnPeakToggleClick(object sender, RoutedEventArgs e)
    {
        _analysisService.PeakEnabled = PeakToggle.IsChecked == true;
        ViewModel?.TogglePeakCommand.Execute(null);
    }

    private void OnCompactModeToggleClick(object sender, RoutedEventArgs e)
    {
        IsCompactMode = CompactModeToggle.IsChecked == true;
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        ResetAll();
    }

    #endregion

    #region Layout Methods

    private void UpdateCompactLayout(bool isCompact)
    {
        if (isCompact)
        {
            // Compact mode: Reduce sizes and hide some elements
            if (ExpandedLayout.ColumnDefinitions.Count > 1)
            {
                // Make goniometer smaller in compact mode
                ExpandedLayout.ColumnDefinitions[1].Width = new GridLength(120);
            }
        }
        else
        {
            // Expanded mode: Restore full sizes
            if (ExpandedLayout.ColumnDefinitions.Count > 1)
            {
                ExpandedLayout.ColumnDefinitions[1].Width = GridLength.Auto;
            }
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Resets all analyzer displays and peak holds.
    /// </summary>
    public void ResetAll()
    {
        _analysisService.Reset();
        SpectrumAnalyzer.Reset();
        Goniometer.Clear();
        CorrelationMeter.Reset();
        TruePeakMeter.Reset();
    }

    /// <summary>
    /// Resets only the peak hold values.
    /// </summary>
    public void ResetPeaks()
    {
        _analysisService.ResetPeaks();
        TruePeakMeter.ResetPeakHold();
        CorrelationMeter.ResetPeak();
    }

    /// <summary>
    /// Starts the analysis.
    /// </summary>
    public void StartAnalysis()
    {
        _analysisService.Start();
    }

    /// <summary>
    /// Stops the analysis.
    /// </summary>
    public void StopAnalysis()
    {
        _analysisService.Stop();
    }

    /// <summary>
    /// Sets the spectrum analyzer band count.
    /// </summary>
    public void SetBandCount(int bandCount)
    {
        SpectrumAnalyzer.BandCount = bandCount;
    }

    /// <summary>
    /// Sets whether to show peak hold indicators.
    /// </summary>
    public void SetShowPeakHold(bool show)
    {
        SpectrumAnalyzer.ShowPeakHold = show;
        TruePeakMeter.ShowPeakHold = show;
        CorrelationMeter.ShowPeakHold = show;
    }

    /// <summary>
    /// Enables or disables all analyzers.
    /// </summary>
    public void SetAllEnabled(bool enabled)
    {
        SpectrumToggle.IsChecked = enabled;
        GoniometerToggle.IsChecked = enabled;
        CorrelationToggle.IsChecked = enabled;
        PeakToggle.IsChecked = enabled;

        _analysisService.SpectrumEnabled = enabled;
        _analysisService.GoniometerEnabled = enabled;
        _analysisService.CorrelationEnabled = enabled;
        _analysisService.PeakEnabled = enabled;
    }

    #endregion
}
