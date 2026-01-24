// MusicEngineEditor - Input Monitor Panel Control
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System.Windows;
using System.Windows.Controls;
using MusicEngine.Core;
using MusicEngineEditor.Services;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Controls;

/// <summary>
/// A panel control for audio input monitoring.
/// Includes device selection, level meters, monitoring enable, and volume control.
/// </summary>
public partial class InputMonitorPanel : UserControl
{
    private readonly InputMonitorViewModel _viewModel;

    #region Dependency Properties

    /// <summary>
    /// Dependency property for the InputMonitor instance.
    /// </summary>
    public static readonly DependencyProperty InputMonitorProperty =
        DependencyProperty.Register(
            nameof(InputMonitor),
            typeof(InputMonitor),
            typeof(InputMonitorPanel),
            new PropertyMetadata(null, OnInputMonitorChanged));

    /// <summary>
    /// Dependency property for the MonitoringSampleProvider instance.
    /// </summary>
    public static readonly DependencyProperty MonitoringProviderProperty =
        DependencyProperty.Register(
            nameof(MonitoringProvider),
            typeof(MonitoringSampleProvider),
            typeof(InputMonitorPanel),
            new PropertyMetadata(null, OnMonitoringProviderChanged));

    /// <summary>
    /// Gets or sets the InputMonitor to use.
    /// </summary>
    public InputMonitor? InputMonitor
    {
        get => (InputMonitor?)GetValue(InputMonitorProperty);
        set => SetValue(InputMonitorProperty, value);
    }

    /// <summary>
    /// Gets or sets the MonitoringSampleProvider to use.
    /// </summary>
    public MonitoringSampleProvider? MonitoringProvider
    {
        get => (MonitoringSampleProvider?)GetValue(MonitoringProviderProperty);
        set => SetValue(MonitoringProviderProperty, value);
    }

    private static void OnInputMonitorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is InputMonitorPanel panel)
        {
            panel.UpdateInitialization();
        }
    }

    private static void OnMonitoringProviderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is InputMonitorPanel panel)
        {
            panel.UpdateInitialization();
        }
    }

    #endregion

    /// <summary>
    /// Creates a new InputMonitorPanel.
    /// </summary>
    public InputMonitorPanel()
    {
        InitializeComponent();

        _viewModel = new InputMonitorViewModel();
        DataContext = _viewModel;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// Gets the view model for data binding.
    /// </summary>
    public InputMonitorViewModel ViewModel => _viewModel;

    /// <summary>
    /// Initializes the panel with an InputMonitor and optional MonitoringSampleProvider.
    /// </summary>
    /// <param name="inputMonitor">The input monitor to use.</param>
    /// <param name="monitoringProvider">Optional monitoring provider for direct monitoring.</param>
    public void Initialize(InputMonitor? inputMonitor, MonitoringSampleProvider? monitoringProvider = null)
    {
        InputMonitor = inputMonitor;
        MonitoringProvider = monitoringProvider;
    }

    /// <summary>
    /// Initializes the panel using the InputMonitorService singleton.
    /// </summary>
    public void InitializeFromService()
    {
        var service = InputMonitorService.Instance;
        if (!service.IsInitialized)
        {
            service.Initialize();
        }

        _viewModel.Initialize(service.InputMonitor, service.MonitoringProvider);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Auto-initialize from service if not already initialized via dependency properties
        if (InputMonitor == null)
        {
            InitializeFromService();
        }
        else
        {
            UpdateInitialization();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _viewModel.Shutdown();
    }

    private void UpdateInitialization()
    {
        _viewModel.Initialize(InputMonitor, MonitoringProvider);
    }
}
