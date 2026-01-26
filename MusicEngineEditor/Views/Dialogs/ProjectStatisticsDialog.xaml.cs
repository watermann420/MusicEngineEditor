using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Dialog for displaying project statistics including track counts, plugin usage, and performance metrics.
/// </summary>
public partial class ProjectStatisticsDialog : Window
{
    #region Fields

    private ProjectStatistics? _statistics;

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the project statistics to display.
    /// </summary>
    public ProjectStatistics? Statistics
    {
        get => _statistics;
        set
        {
            _statistics = value;
            if (_statistics != null)
            {
                DisplayStatistics();
            }
        }
    }

    #endregion

    #region Constructor

    public ProjectStatisticsDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Creates a new dialog with pre-populated statistics.
    /// </summary>
    public ProjectStatisticsDialog(ProjectStatistics statistics) : this()
    {
        Statistics = statistics;
    }

    #endregion

    #region Event Handlers

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        OnRefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        ExportReport();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    #endregion

    #region Events

    /// <summary>
    /// Event raised when the user requests a refresh of the statistics.
    /// </summary>
    public event EventHandler? OnRefreshRequested;

    #endregion

    #region Display Methods

    private void DisplayStatistics()
    {
        if (_statistics == null) return;

        // Project name
        ProjectNameText.Text = _statistics.ProjectName ?? "Untitled Project";

        // Track counts
        TotalTracksValue.Text = _statistics.TotalTracks.ToString();
        AudioTracksValue.Text = _statistics.AudioTracks.ToString();
        MidiTracksValue.Text = _statistics.MidiTracks.ToString();
        BusTracksValue.Text = _statistics.BusTracks.ToString();
        FrozenTracksValue.Text = _statistics.FrozenTracks.ToString();

        // Plugins
        TotalPluginsValue.Text = _statistics.TotalPlugins.ToString();
        VstPluginsValue.Text = _statistics.VstPlugins.ToString();
        BuiltinEffectsValue.Text = _statistics.BuiltinEffects.ToString();
        InstrumentsValue.Text = _statistics.Instruments.ToString();

        // Plugin usage list
        PluginUsageList.ItemsSource = _statistics.PluginUsage;

        // Performance
        CpuAverageValue.Text = _statistics.CpuAverage.ToString("F1");
        CpuPeakValue.Text = _statistics.CpuPeak.ToString("F1");
        MemoryUsageValue.Text = _statistics.MemoryUsageMB.ToString("F1");

        // Project details
        DurationValue.Text = FormatDuration(_statistics.Duration);
        SampleRateValue.Text = _statistics.SampleRate.ToString("N0");
        TempoValue.Text = _statistics.Tempo.ToString("F1");
        TimeSignatureValue.Text = _statistics.TimeSignature;

        // File sizes
        ProjectSizeValue.Text = FormatFileSize(_statistics.ProjectFileSizeBytes);
        AudioFilesSizeValue.Text = FormatFileSize(_statistics.AudioFilesSizeBytes);
        TotalSizeValue.Text = FormatFileSize(_statistics.TotalProjectSizeBytes);
    }

    #endregion

    #region Export

    private void ExportReport()
    {
        if (_statistics == null) return;

        var dialog = new SaveFileDialog
        {
            Title = "Export Project Statistics",
            Filter = "Text File|*.txt|CSV File|*.csv|Markdown|*.md",
            DefaultExt = "txt",
            FileName = $"{_statistics.ProjectName ?? "project"}_statistics"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            string report = GenerateReport(Path.GetExtension(dialog.FileName).ToLowerInvariant());
            File.WriteAllText(dialog.FileName, report);

            MessageBox.Show($"Report exported to:\n{dialog.FileName}", "Export Complete",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to export report:\n{ex.Message}", "Export Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private string GenerateReport(string format)
    {
        if (_statistics == null) return string.Empty;

        var sb = new StringBuilder();

        if (format == ".md")
        {
            sb.AppendLine($"# Project Statistics: {_statistics.ProjectName}");
            sb.AppendLine();
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("## Tracks");
            sb.AppendLine($"- Total Tracks: {_statistics.TotalTracks}");
            sb.AppendLine($"- Audio Tracks: {_statistics.AudioTracks}");
            sb.AppendLine($"- MIDI Tracks: {_statistics.MidiTracks}");
            sb.AppendLine($"- Bus Tracks: {_statistics.BusTracks}");
            sb.AppendLine($"- Frozen Tracks: {_statistics.FrozenTracks}");
            sb.AppendLine();
            sb.AppendLine("## Plugins");
            sb.AppendLine($"- Total Plugin Instances: {_statistics.TotalPlugins}");
            sb.AppendLine($"- VST Plugins: {_statistics.VstPlugins}");
            sb.AppendLine($"- Built-in Effects: {_statistics.BuiltinEffects}");
            sb.AppendLine($"- Instruments: {_statistics.Instruments}");
            sb.AppendLine();
            if (_statistics.PluginUsage?.Count > 0)
            {
                sb.AppendLine("### Plugin Usage");
                sb.AppendLine("| Plugin | Count |");
                sb.AppendLine("|--------|-------|");
                foreach (var plugin in _statistics.PluginUsage)
                {
                    sb.AppendLine($"| {plugin.Name} | {plugin.Count} |");
                }
                sb.AppendLine();
            }
            sb.AppendLine("## Performance");
            sb.AppendLine($"- CPU Average: {_statistics.CpuAverage:F1}%");
            sb.AppendLine($"- CPU Peak: {_statistics.CpuPeak:F1}%");
            sb.AppendLine($"- Memory Usage: {_statistics.MemoryUsageMB:F1} MB");
            sb.AppendLine();
            sb.AppendLine("## Project Details");
            sb.AppendLine($"- Duration: {FormatDuration(_statistics.Duration)}");
            sb.AppendLine($"- Sample Rate: {_statistics.SampleRate:N0} Hz");
            sb.AppendLine($"- Tempo: {_statistics.Tempo:F1} BPM");
            sb.AppendLine($"- Time Signature: {_statistics.TimeSignature}");
            sb.AppendLine();
            sb.AppendLine("## File Sizes");
            sb.AppendLine($"- Project File: {FormatFileSize(_statistics.ProjectFileSizeBytes)}");
            sb.AppendLine($"- Audio Files: {FormatFileSize(_statistics.AudioFilesSizeBytes)}");
            sb.AppendLine($"- Total: {FormatFileSize(_statistics.TotalProjectSizeBytes)}");
        }
        else if (format == ".csv")
        {
            sb.AppendLine("Category,Property,Value");
            sb.AppendLine($"Project,Name,{_statistics.ProjectName}");
            sb.AppendLine($"Tracks,Total,{_statistics.TotalTracks}");
            sb.AppendLine($"Tracks,Audio,{_statistics.AudioTracks}");
            sb.AppendLine($"Tracks,MIDI,{_statistics.MidiTracks}");
            sb.AppendLine($"Tracks,Bus,{_statistics.BusTracks}");
            sb.AppendLine($"Tracks,Frozen,{_statistics.FrozenTracks}");
            sb.AppendLine($"Plugins,Total,{_statistics.TotalPlugins}");
            sb.AppendLine($"Plugins,VST,{_statistics.VstPlugins}");
            sb.AppendLine($"Plugins,Built-in,{_statistics.BuiltinEffects}");
            sb.AppendLine($"Plugins,Instruments,{_statistics.Instruments}");
            sb.AppendLine($"Performance,CPU Average,{_statistics.CpuAverage:F1}");
            sb.AppendLine($"Performance,CPU Peak,{_statistics.CpuPeak:F1}");
            sb.AppendLine($"Performance,Memory (MB),{_statistics.MemoryUsageMB:F1}");
            sb.AppendLine($"Details,Duration,{FormatDuration(_statistics.Duration)}");
            sb.AppendLine($"Details,Sample Rate,{_statistics.SampleRate}");
            sb.AppendLine($"Details,Tempo,{_statistics.Tempo:F1}");
            sb.AppendLine($"Details,Time Signature,{_statistics.TimeSignature}");
            sb.AppendLine($"Files,Project Size,{_statistics.ProjectFileSizeBytes}");
            sb.AppendLine($"Files,Audio Size,{_statistics.AudioFilesSizeBytes}");
            sb.AppendLine($"Files,Total Size,{_statistics.TotalProjectSizeBytes}");
        }
        else // Plain text
        {
            sb.AppendLine($"Project Statistics: {_statistics.ProjectName}");
            sb.AppendLine(new string('=', 50));
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("TRACKS");
            sb.AppendLine($"  Total Tracks:  {_statistics.TotalTracks}");
            sb.AppendLine($"  Audio Tracks:  {_statistics.AudioTracks}");
            sb.AppendLine($"  MIDI Tracks:   {_statistics.MidiTracks}");
            sb.AppendLine($"  Bus Tracks:    {_statistics.BusTracks}");
            sb.AppendLine($"  Frozen Tracks: {_statistics.FrozenTracks}");
            sb.AppendLine();
            sb.AppendLine("PLUGINS");
            sb.AppendLine($"  Total Instances:  {_statistics.TotalPlugins}");
            sb.AppendLine($"  VST Plugins:      {_statistics.VstPlugins}");
            sb.AppendLine($"  Built-in Effects: {_statistics.BuiltinEffects}");
            sb.AppendLine($"  Instruments:      {_statistics.Instruments}");
            sb.AppendLine();
            sb.AppendLine("PERFORMANCE");
            sb.AppendLine($"  CPU Average: {_statistics.CpuAverage:F1}%");
            sb.AppendLine($"  CPU Peak:    {_statistics.CpuPeak:F1}%");
            sb.AppendLine($"  Memory:      {_statistics.MemoryUsageMB:F1} MB");
            sb.AppendLine();
            sb.AppendLine("PROJECT DETAILS");
            sb.AppendLine($"  Duration:       {FormatDuration(_statistics.Duration)}");
            sb.AppendLine($"  Sample Rate:    {_statistics.SampleRate:N0} Hz");
            sb.AppendLine($"  Tempo:          {_statistics.Tempo:F1} BPM");
            sb.AppendLine($"  Time Signature: {_statistics.TimeSignature}");
            sb.AppendLine();
            sb.AppendLine("FILE SIZES");
            sb.AppendLine($"  Project File: {FormatFileSize(_statistics.ProjectFileSizeBytes)}");
            sb.AppendLine($"  Audio Files:  {FormatFileSize(_statistics.AudioFilesSizeBytes)}");
            sb.AppendLine($"  Total:        {FormatFileSize(_statistics.TotalProjectSizeBytes)}");
        }

        return sb.ToString();
    }

    #endregion

    #region Helpers

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours}:{duration.Minutes:D2}:{duration.Seconds:D2}";
        return $"{(int)duration.TotalMinutes}:{duration.Seconds:D2}";
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }

    #endregion
}

/// <summary>
/// Contains project statistics data.
/// </summary>
public class ProjectStatistics
{
    // Project info
    public string? ProjectName { get; set; }
    public string? ProjectPath { get; set; }

    // Track counts
    public int TotalTracks { get; set; }
    public int AudioTracks { get; set; }
    public int MidiTracks { get; set; }
    public int BusTracks { get; set; }
    public int FrozenTracks { get; set; }

    // Plugin counts
    public int TotalPlugins { get; set; }
    public int VstPlugins { get; set; }
    public int BuiltinEffects { get; set; }
    public int Instruments { get; set; }
    public List<PluginUsageInfo>? PluginUsage { get; set; }

    // Performance
    public double CpuAverage { get; set; }
    public double CpuPeak { get; set; }
    public double MemoryUsageMB { get; set; }

    // Project details
    public TimeSpan Duration { get; set; }
    public int SampleRate { get; set; }
    public double Tempo { get; set; }
    public string TimeSignature { get; set; } = "4/4";

    // File sizes
    public long ProjectFileSizeBytes { get; set; }
    public long AudioFilesSizeBytes { get; set; }
    public long TotalProjectSizeBytes { get; set; }
}

/// <summary>
/// Information about plugin usage in the project.
/// </summary>
public class PluginUsageInfo
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}
