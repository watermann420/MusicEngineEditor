using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls;

/// <summary>
/// True Peak meter display with dBTP scale and inter-sample peak detection indication.
/// Features clip warning at 0 dBTP and configurable hold time.
/// </summary>
public partial class TruePeakMeter : UserControl
{
    #region Constants

    private const double MinDbtp = -60.0;
    private const double MaxDbtp = 3.0; // Allow slight overshoot display
    private const double ClipThreshold = 0.0; // 0 dBTP

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty LeftPeakProperty =
        DependencyProperty.Register(nameof(LeftPeak), typeof(float), typeof(TruePeakMeter),
            new PropertyMetadata(0f, OnPeakChanged));

    public static readonly DependencyProperty RightPeakProperty =
        DependencyProperty.Register(nameof(RightPeak), typeof(float), typeof(TruePeakMeter),
            new PropertyMetadata(0f, OnPeakChanged));

    public static readonly DependencyProperty LeftMaxPeakProperty =
        DependencyProperty.Register(nameof(LeftMaxPeak), typeof(float), typeof(TruePeakMeter),
            new PropertyMetadata(0f));

    public static readonly DependencyProperty RightMaxPeakProperty =
        DependencyProperty.Register(nameof(RightMaxPeak), typeof(float), typeof(TruePeakMeter),
            new PropertyMetadata(0f));

    public static readonly DependencyProperty MaxTruePeakDbtpProperty =
        DependencyProperty.Register(nameof(MaxTruePeakDbtp), typeof(float), typeof(TruePeakMeter),
            new PropertyMetadata(-60f, OnMaxPeakChanged));

    public static readonly DependencyProperty ShowPeakHoldProperty =
        DependencyProperty.Register(nameof(ShowPeakHold), typeof(bool), typeof(TruePeakMeter),
            new PropertyMetadata(true));

    public static readonly DependencyProperty PeakHoldTimeProperty =
        DependencyProperty.Register(nameof(PeakHoldTime), typeof(TimeSpan), typeof(TruePeakMeter),
            new PropertyMetadata(TimeSpan.FromSeconds(2)));

    public static readonly DependencyProperty HasClippedProperty =
        DependencyProperty.Register(nameof(HasClipped), typeof(bool), typeof(TruePeakMeter),
            new PropertyMetadata(false, OnClipStateChanged));

    /// <summary>
    /// Gets or sets the left channel peak level (linear scale).
    /// </summary>
    public float LeftPeak
    {
        get => (float)GetValue(LeftPeakProperty);
        set => SetValue(LeftPeakProperty, value);
    }

    /// <summary>
    /// Gets or sets the right channel peak level (linear scale).
    /// </summary>
    public float RightPeak
    {
        get => (float)GetValue(RightPeakProperty);
        set => SetValue(RightPeakProperty, value);
    }

    /// <summary>
    /// Gets or sets the left channel maximum peak (linear scale).
    /// </summary>
    public float LeftMaxPeak
    {
        get => (float)GetValue(LeftMaxPeakProperty);
        set => SetValue(LeftMaxPeakProperty, value);
    }

    /// <summary>
    /// Gets or sets the right channel maximum peak (linear scale).
    /// </summary>
    public float RightMaxPeak
    {
        get => (float)GetValue(RightMaxPeakProperty);
        set => SetValue(RightMaxPeakProperty, value);
    }

    /// <summary>
    /// Gets or sets the maximum true peak in dBTP.
    /// </summary>
    public float MaxTruePeakDbtp
    {
        get => (float)GetValue(MaxTruePeakDbtpProperty);
        set => SetValue(MaxTruePeakDbtpProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to show peak hold indicators.
    /// </summary>
    public bool ShowPeakHold
    {
        get => (bool)GetValue(ShowPeakHoldProperty);
        set => SetValue(ShowPeakHoldProperty, value);
    }

    /// <summary>
    /// Gets or sets the peak hold time.
    /// </summary>
    public TimeSpan PeakHoldTime
    {
        get => (TimeSpan)GetValue(PeakHoldTimeProperty);
        set => SetValue(PeakHoldTimeProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the signal has clipped.
    /// </summary>
    public bool HasClipped
    {
        get => (bool)GetValue(HasClippedProperty);
        set => SetValue(HasClippedProperty, value);
    }

    #endregion

    #region Private Fields

    private double _displayedLeftPeak;
    private double _displayedRightPeak;
    private double _leftPeakHold = MinDbtp;
    private double _rightPeakHold = MinDbtp;
    private DateTime _leftPeakHoldTime;
    private DateTime _rightPeakHoldTime;
    private bool _leftClipped;
    private bool _rightClipped;
    private DispatcherTimer? _updateTimer;
    private bool _isInitialized;

    #endregion

    #region Constructor

    public TruePeakMeter()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    #endregion

    #region Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        DrawDbScale();
        StartUpdateTimer();
        _isInitialized = true;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopUpdateTimer();
        _isInitialized = false;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitialized)
        {
            DrawDbScale();
            UpdateMeters();
        }
    }

    private static void OnPeakChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TruePeakMeter meter && meter._isInitialized)
        {
            meter.UpdatePeakHold();
            meter.CheckClipping();
        }
    }

    private static void OnMaxPeakChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TruePeakMeter meter && meter._isInitialized)
        {
            meter.UpdatePeakValueDisplay();
        }
    }

    private static void OnClipStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TruePeakMeter meter && meter._isInitialized)
        {
            meter.UpdateClipIndicators();
        }
    }

    #endregion

    #region Drawing

    private void DrawDbScale()
    {
        DbScaleCanvas.Children.Clear();

        Brush textBrush = FindResource("TextBrush") as Brush ?? Brushes.Gray;
        Brush gridBrush = FindResource("BorderBrush") as Brush ?? Brushes.DarkGray;

        double height = DbScaleCanvas.ActualHeight;
        if (height <= 0) return;

        // dBTP markings
        double[] dbMarks = { 0, -3, -6, -12, -18, -24, -36, -48, -60 };

        foreach (var db in dbMarks)
        {
            double normalizedLevel = (db - MinDbtp) / (MaxDbtp - MinDbtp);
            double y = height * (1 - normalizedLevel);

            // Tick mark
            var tick = new Shapes.Line
            {
                X1 = 18,
                Y1 = y,
                X2 = 22,
                Y2 = y,
                Stroke = gridBrush,
                StrokeThickness = 1
            };
            DbScaleCanvas.Children.Add(tick);

            // Label
            var label = new TextBlock
            {
                Text = db == 0 ? "0" : db.ToString(),
                Foreground = db == 0 ? FindResource("ClipBrush") as Brush ?? Brushes.Red : textBrush,
                FontSize = 8,
                FontWeight = db == 0 ? FontWeights.Bold : FontWeights.Normal,
                TextAlignment = TextAlignment.Right
            };

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetRight(label, 6);
            Canvas.SetTop(label, y - label.DesiredSize.Height / 2);
            DbScaleCanvas.Children.Add(label);
        }

        // Clip zone indicator (0 dBTP line)
        double clipY = height * (1 - (ClipThreshold - MinDbtp) / (MaxDbtp - MinDbtp));
        var clipLine = new Shapes.Line
        {
            X1 = 0,
            Y1 = clipY,
            X2 = DbScaleCanvas.ActualWidth,
            Y2 = clipY,
            Stroke = FindResource("ClipBrush") as Brush ?? Brushes.Red,
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 2, 2 }
        };
        DbScaleCanvas.Children.Add(clipLine);
    }

    private void UpdateMeters()
    {
        // Get meter heights
        double meterHeight = LeftLevelBar.Parent is Grid grid ? grid.ActualHeight : 100;
        if (meterHeight <= 0) return;

        // Convert linear to dB and normalize
        double leftDb = LinearToDbtp(LeftPeak);
        double rightDb = LinearToDbtp(RightPeak);

        // Smooth the display
        _displayedLeftPeak = _displayedLeftPeak * 0.7 + leftDb * 0.3;
        _displayedRightPeak = _displayedRightPeak * 0.7 + rightDb * 0.3;

        // Update left bar
        double leftNormalized = Math.Clamp((_displayedLeftPeak - MinDbtp) / (MaxDbtp - MinDbtp), 0, 1);
        LeftLevelBar.Height = meterHeight * leftNormalized;

        // Update right bar
        double rightNormalized = Math.Clamp((_displayedRightPeak - MinDbtp) / (MaxDbtp - MinDbtp), 0, 1);
        RightLevelBar.Height = meterHeight * rightNormalized;

        // Update peak hold indicators
        if (ShowPeakHold)
        {
            double leftPeakNormalized = Math.Clamp((_leftPeakHold - MinDbtp) / (MaxDbtp - MinDbtp), 0, 1);
            LeftPeakIndicator.Margin = new Thickness(0, 0, 0, meterHeight * leftPeakNormalized);
            LeftPeakIndicator.Visibility = Visibility.Visible;

            double rightPeakNormalized = Math.Clamp((_rightPeakHold - MinDbtp) / (MaxDbtp - MinDbtp), 0, 1);
            RightPeakIndicator.Margin = new Thickness(0, 0, 0, meterHeight * rightPeakNormalized);
            RightPeakIndicator.Visibility = Visibility.Visible;
        }
        else
        {
            LeftPeakIndicator.Visibility = Visibility.Collapsed;
            RightPeakIndicator.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdatePeakHold()
    {
        var now = DateTime.UtcNow;
        double leftDb = LinearToDbtp(LeftPeak);
        double rightDb = LinearToDbtp(RightPeak);

        // Update left peak hold
        if (leftDb > _leftPeakHold)
        {
            _leftPeakHold = leftDb;
            _leftPeakHoldTime = now;
        }
        else if ((now - _leftPeakHoldTime) > PeakHoldTime)
        {
            _leftPeakHold = Math.Max(MinDbtp, _leftPeakHold - 30 * 0.016); // Fall 30 dB/s
        }

        // Update right peak hold
        if (rightDb > _rightPeakHold)
        {
            _rightPeakHold = rightDb;
            _rightPeakHoldTime = now;
        }
        else if ((now - _rightPeakHoldTime) > PeakHoldTime)
        {
            _rightPeakHold = Math.Max(MinDbtp, _rightPeakHold - 30 * 0.016);
        }
    }

    private void CheckClipping()
    {
        if (LeftPeak >= 1.0f && !_leftClipped)
        {
            _leftClipped = true;
            UpdateClipIndicators();
        }

        if (RightPeak >= 1.0f && !_rightClipped)
        {
            _rightClipped = true;
            UpdateClipIndicators();
        }
    }

    private void UpdateClipIndicators()
    {
        var clipOnBrush = FindResource("ClipBrush") as Brush ?? Brushes.Red;
        var clipOffBrush = FindResource("ClipOffBrush") as Brush ?? Brushes.DarkRed;
        var glowEffect = FindResource("ClipGlowEffect") as Effect;

        // Left clip indicator
        if (_leftClipped || HasClipped)
        {
            LeftClipIndicator.Fill = clipOnBrush;
            LeftClipIndicator.Effect = glowEffect;
        }
        else
        {
            LeftClipIndicator.Fill = clipOffBrush;
            LeftClipIndicator.Effect = null;
        }

        // Right clip indicator
        if (_rightClipped || HasClipped)
        {
            RightClipIndicator.Fill = clipOnBrush;
            RightClipIndicator.Effect = glowEffect;
        }
        else
        {
            RightClipIndicator.Fill = clipOffBrush;
            RightClipIndicator.Effect = null;
        }
    }

    private void UpdatePeakValueDisplay()
    {
        float dbtp = MaxTruePeakDbtp;

        if (dbtp <= -60)
        {
            PeakValueText.Text = "-inf dBTP";
            PeakValueText.Foreground = FindResource("TextBrush") as Brush ?? Brushes.Gray;
        }
        else
        {
            PeakValueText.Text = $"{dbtp:F1} dBTP";

            // Color based on level
            if (dbtp >= 0)
            {
                PeakValueText.Foreground = FindResource("ClipBrush") as Brush ?? Brushes.Red;
            }
            else if (dbtp >= -3)
            {
                PeakValueText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x88, 0x00));
            }
            else if (dbtp >= -6)
            {
                PeakValueText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0x00));
            }
            else
            {
                PeakValueText.Foreground = FindResource("ValueTextBrush") as Brush ?? Brushes.White;
            }
        }
    }

    #endregion

    #region Timer

    private void StartUpdateTimer()
    {
        _updateTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
        };
        _updateTimer.Tick += OnUpdateTimerTick;
        _updateTimer.Start();
    }

    private void StopUpdateTimer()
    {
        if (_updateTimer != null)
        {
            _updateTimer.Stop();
            _updateTimer.Tick -= OnUpdateTimerTick;
            _updateTimer = null;
        }
    }

    private void OnUpdateTimerTick(object? sender, EventArgs e)
    {
        UpdateMeters();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Resets the meter and clears all peaks.
    /// </summary>
    public void Reset()
    {
        _displayedLeftPeak = MinDbtp;
        _displayedRightPeak = MinDbtp;
        _leftPeakHold = MinDbtp;
        _rightPeakHold = MinDbtp;
        _leftClipped = false;
        _rightClipped = false;
        HasClipped = false;
        MaxTruePeakDbtp = -60f;
        UpdateMeters();
        UpdateClipIndicators();
        UpdatePeakValueDisplay();
    }

    /// <summary>
    /// Resets only the clip indicators.
    /// </summary>
    public void ResetClipIndicators()
    {
        _leftClipped = false;
        _rightClipped = false;
        HasClipped = false;
        UpdateClipIndicators();
    }

    /// <summary>
    /// Resets only the peak hold values.
    /// </summary>
    public void ResetPeakHold()
    {
        _leftPeakHold = MinDbtp;
        _rightPeakHold = MinDbtp;
        MaxTruePeakDbtp = -60f;
        UpdatePeakValueDisplay();
    }

    #endregion

    #region Helper Methods

    private static double LinearToDbtp(float linear)
    {
        if (linear <= 0)
            return MinDbtp;

        double dbtp = 20.0 * Math.Log10(linear);
        return Math.Clamp(dbtp, MinDbtp, MaxDbtp);
    }

    #endregion
}
