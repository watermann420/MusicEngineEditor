// MusicEngineEditor - PDC Display Control
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System.Windows;
using System.Windows.Controls;
using MusicEngine.Core.PDC;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Controls;

/// <summary>
/// A compact PDC (Plugin Delay Compensation) indicator control.
/// Displays total latency, compensation status, and provides enable/disable toggle.
/// </summary>
public partial class PdcDisplayControl : UserControl
{
    private readonly PdcDisplayViewModel _viewModel;

    #region Dependency Properties

    /// <summary>
    /// Dependency property for the PDC Manager.
    /// </summary>
    public static readonly DependencyProperty PdcManagerProperty =
        DependencyProperty.Register(
            nameof(PdcManager),
            typeof(PdcManager),
            typeof(PdcDisplayControl),
            new PropertyMetadata(null, OnPdcManagerChanged));

    /// <summary>
    /// Dependency property for the sample rate.
    /// </summary>
    public static readonly DependencyProperty SampleRateProperty =
        DependencyProperty.Register(
            nameof(SampleRate),
            typeof(int),
            typeof(PdcDisplayControl),
            new PropertyMetadata(44100, OnSampleRateChanged));

    /// <summary>
    /// Gets or sets the PDC Manager to display information from.
    /// </summary>
    public PdcManager? PdcManager
    {
        get => (PdcManager?)GetValue(PdcManagerProperty);
        set => SetValue(PdcManagerProperty, value);
    }

    /// <summary>
    /// Gets or sets the sample rate for latency calculations.
    /// </summary>
    public int SampleRate
    {
        get => (int)GetValue(SampleRateProperty);
        set => SetValue(SampleRateProperty, value);
    }

    private static void OnPdcManagerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PdcDisplayControl control && e.NewValue is PdcManager manager)
        {
            control._viewModel.Initialize(manager, control.SampleRate);
        }
    }

    private static void OnSampleRateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PdcDisplayControl control && control.PdcManager != null)
        {
            control._viewModel.Initialize(control.PdcManager, (int)e.NewValue);
        }
    }

    #endregion

    /// <summary>
    /// Creates a new PdcDisplayControl.
    /// </summary>
    public PdcDisplayControl()
    {
        InitializeComponent();

        _viewModel = new PdcDisplayViewModel();
        DataContext = _viewModel;

        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// Initializes the control with a PDC manager.
    /// </summary>
    /// <param name="pdcManager">The PDC manager to monitor.</param>
    /// <param name="sampleRate">The sample rate for calculations.</param>
    public void Initialize(PdcManager pdcManager, int sampleRate = 44100)
    {
        PdcManager = pdcManager;
        SampleRate = sampleRate;
    }

    /// <summary>
    /// Gets the view model for data binding.
    /// </summary>
    public PdcDisplayViewModel ViewModel => _viewModel;

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _viewModel.Shutdown();
    }
}
