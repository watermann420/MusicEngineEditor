using System;
using System.Windows;

namespace MusicEngineEditor.Views;

public partial class VstPluginWindow : Window
{
    private readonly string _pluginName;
    private readonly string _variableName;
    private readonly object? _vstPlugin;
    private bool _isBypassed;
    private bool _keepRunning = true;

    public string PluginName => _pluginName;
    public string VariableName => _variableName;
    public bool KeepRunning => _keepRunning;

    public VstPluginWindow(string pluginName, string variableName, object? vstPlugin = null)
    {
        InitializeComponent();

        _pluginName = pluginName;
        _variableName = variableName;
        _vstPlugin = vstPlugin;

        Title = $"{pluginName} - VST Plugin";
        PluginNameText.Text = pluginName;
        VariableNameText.Text = variableName;

        // Determine plugin type based on name
        if (pluginName.EndsWith(".vst3", StringComparison.OrdinalIgnoreCase))
        {
            PluginTypeText.Text = "(VST3)";
        }
        else
        {
            PluginTypeText.Text = "(VST2)";
        }

        // Try to initialize plugin UI
        InitializePluginUI();
    }

    private void InitializePluginUI()
    {
        if (_vstPlugin != null)
        {
            // Try to get the plugin editor window handle
            try
            {
                // This is where you would hook into the actual VST plugin UI
                // For now, show a placeholder
                PluginStatusText.Text = "Plugin loaded - UI available";
                PlaceholderPanel.Visibility = Visibility.Visible;
                VstHost.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                PluginStatusText.Text = $"UI not available: {ex.Message}";
            }
        }
        else
        {
            PluginStatusText.Text = "No plugin instance - using variable reference";
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        // Don't actually close - just hide the window
        // The plugin keeps running in the background
        Hide();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_keepRunning)
        {
            // Instead of closing, just hide
            e.Cancel = true;
            Hide();
        }
        else
        {
            base.OnClosing(e);
        }
    }

    public void ForceClose()
    {
        _keepRunning = false;
        Close();
    }

    public void ShowWindow()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
    }

    private void BypassButton_Click(object sender, RoutedEventArgs e)
    {
        _isBypassed = !_isBypassed;
        BypassButton.Content = _isBypassed ? "Enable" : "Bypass";

        // Update visual feedback
        if (_isBypassed)
        {
            PluginHostBorder.Opacity = 0.5;
        }
        else
        {
            PluginHostBorder.Opacity = 1.0;
        }

        // TODO: Actually bypass the plugin in the audio engine
    }

    private void PresetsButton_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Show preset browser/manager
        MessageBox.Show("Preset management is not yet implemented.",
            "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
