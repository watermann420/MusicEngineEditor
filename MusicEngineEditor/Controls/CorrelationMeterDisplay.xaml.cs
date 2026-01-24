using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Correlation meter display showing stereo phase correlation and M/S ratio.
/// Range: -1 (out of phase) to +1 (mono/in phase).
/// </summary>
public partial class CorrelationMeterDisplay : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty CorrelationProperty =
        DependencyProperty.Register(nameof(Correlation), typeof(double), typeof(CorrelationMeterDisplay),
            new PropertyMetadata(0.0, OnCorrelationChanged));

    public static readonly DependencyProperty MidLevelProperty =
        DependencyProperty.Register(nameof(MidLevel), typeof(double), typeof(CorrelationMeterDisplay),
            new PropertyMetadata(0.0, OnLevelChanged));

    public static readonly DependencyProperty SideLevelProperty =
        DependencyProperty.Register(nameof(SideLevel), typeof(double), typeof(CorrelationMeterDisplay),
            new PropertyMetadata(0.0, OnLevelChanged));

    public static readonly DependencyProperty MsRatioProperty =
        DependencyProperty.Register(nameof(MsRatio), typeof(double), typeof(CorrelationMeterDisplay),
            new PropertyMetadata(0.5, OnLevelChanged));

    public static readonly DependencyProperty ShowPeakHoldProperty =
        DependencyProperty.Register(nameof(ShowPeakHold), typeof(bool), typeof(CorrelationMeterDisplay),
            new PropertyMetadata(true));

    public static readonly DependencyProperty ShowMsRatioProperty =
        DependencyProperty.Register(nameof(ShowMsRatio), typeof(bool), typeof(CorrelationMeterDisplay),
            new PropertyMetadata(true));

    public static readonly DependencyProperty PeakHoldTimeProperty =
        DependencyProperty.Register(nameof(PeakHoldTime), typeof(TimeSpan), typeof(CorrelationMeterDisplay),
            new PropertyMetadata(TimeSpan.FromSeconds(2)));

    /// <summary>
    /// Gets or sets the correlation value (-1.0 to +1.0).
    /// </summary>
    public double Correlation
    {
        get => (double)GetValue(CorrelationProperty);
        set => SetValue(CorrelationProperty, value);
    }

    /// <summary>
    /// Gets or sets the Mid (L+R) level.
    /// </summary>
    public double MidLevel
    {
        get => (double)GetValue(MidLevelProperty);
        set => SetValue(MidLevelProperty, value);
    }

    /// <summary>
    /// Gets or sets the Side (L-R) level.
    /// </summary>
    public double SideLevel
    {
        get => (double)GetValue(SideLevelProperty);
        set => SetValue(SideLevelProperty, value);
    }

    /// <summary>
    /// Gets or sets the M/S ratio (0 = all side, 1 = all mid).
    /// </summary>
    public double MsRatio
    {
        get => (double)GetValue(MsRatioProperty);
        set => SetValue(MsRatioProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to show peak hold indicator.
    /// </summary>
    public bool ShowPeakHold
    {
        get => (bool)GetValue(ShowPeakHoldProperty);
        set => SetValue(ShowPeakHoldProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to show M/S ratio display.
    /// </summary>
    public bool ShowMsRatio
    {
        get => (bool)GetValue(ShowMsRatioProperty);
        set => SetValue(ShowMsRatioProperty, value);
    }

    /// <summary>
    /// Gets or sets the peak hold time.
    /// </summary>
    public TimeSpan PeakHoldTime
    {
        get => (TimeSpan)GetValue(PeakHoldTimeProperty);
        set => SetValue(PeakHoldTimeProperty, value);
    }

    #endregion

    #region Private Fields

    private double _displayedCorrelation;
    private double _peakCorrelation;
    private DateTime _peakTime;
    private bool _isInitialized;

    // Colors for different states
    private static readonly Color InPhaseColor = Color.FromRgb(0x00, 0xFF, 0x00);
    private static readonly Color WarningColor = Color.FromRgb(0xFF, 0xFF, 0x00);
    private static readonly Color OutOfPhaseColor = Color.FromRgb(0xFF, 0x33, 0x33);

    #endregion

    #region Constructor

    public CorrelationMeterDisplay()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    #endregion

    #region Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = true;
        UpdateIndicator();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitialized)
        {
            UpdateIndicator();
        }
    }

    private static void OnCorrelationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CorrelationMeterDisplay display && display._isInitialized)
        {
            display.UpdateCorrelation();
        }
    }

    private static void OnLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CorrelationMeterDisplay display && display._isInitialized)
        {
            display.UpdateMsRatio();
        }
    }

    #endregion

    #region Update Methods

    private void UpdateCorrelation()
    {
        double correlation = Correlation;

        // Smooth the displayed value
        _displayedCorrelation = _displayedCorrelation * 0.7 + correlation * 0.3;

        // Update peak hold (track most extreme value)
        var now = DateTime.UtcNow;
        if (Math.Abs(correlation) > Math.Abs(_peakCorrelation) ||
            (now - _peakTime) > PeakHoldTime)
        {
            _peakCorrelation = correlation;
            _peakTime = now;
        }

        UpdateIndicator();
        UpdateValueDisplay();
    }

    private void UpdateIndicator()
    {
        double canvasWidth = IndicatorCanvas.ActualWidth;
        if (canvasWidth <= 0) return;

        // Map correlation (-1 to +1) to position (0 to width)
        double normalizedPos = (_displayedCorrelation + 1) / 2;
        double indicatorWidth = IndicatorBar.Width;
        double position = normalizedPos * (canvasWidth - indicatorWidth);

        Canvas.SetLeft(IndicatorBar, position);

        // Update indicator color based on correlation
        IndicatorBar.Fill = new SolidColorBrush(GetCorrelationColor(_displayedCorrelation));

        // Update peak indicator
        if (ShowPeakHold)
        {
            double peakNormalized = (_peakCorrelation + 1) / 2;
            double peakPosition = peakNormalized * (canvasWidth - PeakIndicator.Width);
            Canvas.SetLeft(PeakIndicator, peakPosition);
            PeakIndicator.Visibility = Visibility.Visible;
        }
        else
        {
            PeakIndicator.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateValueDisplay()
    {
        // Update correlation value text
        CorrelationValueText.Text = Correlation.ToString("F2");
        CorrelationValueText.Foreground = new SolidColorBrush(GetCorrelationColor(Correlation));

        // Update phase indicator text
        if (Correlation < -0.5)
        {
            PhaseIndicatorText.Text = "OUT OF PHASE";
            PhaseIndicatorText.Foreground = new SolidColorBrush(OutOfPhaseColor);
        }
        else if (Correlation < 0)
        {
            PhaseIndicatorText.Text = "PARTIAL PHASE";
            PhaseIndicatorText.Foreground = new SolidColorBrush(WarningColor);
        }
        else if (Correlation > 0.9)
        {
            PhaseIndicatorText.Text = "MONO";
            PhaseIndicatorText.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0xFF));
        }
        else
        {
            PhaseIndicatorText.Text = "IN PHASE";
            PhaseIndicatorText.Foreground = new SolidColorBrush(InPhaseColor);
        }
    }

    private void UpdateMsRatio()
    {
        if (!ShowMsRatio)
        {
            MsRatioText.Visibility = Visibility.Collapsed;
            return;
        }

        MsRatioText.Visibility = Visibility.Visible;

        // Display as percentage M/S
        int midPercent = (int)(MsRatio * 100);
        int sidePercent = 100 - midPercent;
        MsRatioText.Text = $"{midPercent}/{sidePercent}";

        // Color based on balance
        if (sidePercent > 70)
        {
            MsRatioText.Foreground = new SolidColorBrush(WarningColor);
        }
        else if (midPercent > 90)
        {
            MsRatioText.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0xFF));
        }
        else
        {
            MsRatioText.Foreground = FindResource("ValueTextBrush") as Brush ?? Brushes.White;
        }
    }

    #endregion

    #region Helper Methods

    private static Color GetCorrelationColor(double correlation)
    {
        // Gradient from red (-1) through yellow to green (0) to cyan (+1)
        if (correlation < -0.5)
        {
            // Red zone
            return OutOfPhaseColor;
        }
        else if (correlation < 0)
        {
            // Red to yellow
            double t = (correlation + 0.5) * 2; // 0 to 1
            return Color.FromRgb(
                (byte)(OutOfPhaseColor.R + (WarningColor.R - OutOfPhaseColor.R) * t),
                (byte)(OutOfPhaseColor.G + (WarningColor.G - OutOfPhaseColor.G) * t),
                0);
        }
        else if (correlation < 0.5)
        {
            // Yellow to green
            double t = correlation * 2; // 0 to 1
            return Color.FromRgb(
                (byte)(WarningColor.R * (1 - t)),
                0xFF,
                0);
        }
        else
        {
            // Green to cyan
            double t = (correlation - 0.5) * 2; // 0 to 1
            return Color.FromRgb(
                0,
                0xFF,
                (byte)(0xFF * t));
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Resets the meter.
    /// </summary>
    public void Reset()
    {
        _displayedCorrelation = 0;
        _peakCorrelation = 0;
        UpdateIndicator();
        UpdateValueDisplay();
    }

    /// <summary>
    /// Resets just the peak hold.
    /// </summary>
    public void ResetPeak()
    {
        _peakCorrelation = Correlation;
        _peakTime = DateTime.UtcNow;
        UpdateIndicator();
    }

    #endregion
}
