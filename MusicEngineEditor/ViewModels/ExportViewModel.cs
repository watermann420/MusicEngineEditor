using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngine.Core;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// ViewModel for the Export dialog with platform presets and loudness normalization.
/// </summary>
public partial class ExportViewModel : ViewModelBase
{
    private CancellationTokenSource? _cancellationTokenSource;

    // Preset selection
    [ObservableProperty]
    private ExportPreset _selectedPreset;

    [ObservableProperty]
    private bool _isCustomPreset;

    // Format settings
    [ObservableProperty]
    private AudioFormat _selectedFormat = AudioFormat.Wav;

    [ObservableProperty]
    private int _selectedSampleRate = 44100;

    [ObservableProperty]
    private int _selectedBitDepth = 24;

    [ObservableProperty]
    private int _selectedBitRate = 320;

    // Loudness settings
    [ObservableProperty]
    private bool _normalizeLoudness = true;

    [ObservableProperty]
    private float _targetLufs = -14f;

    [ObservableProperty]
    private float _maxTruePeak = -1f;

    // File paths
    [ObservableProperty]
    private string _inputPath = string.Empty;

    [ObservableProperty]
    private string _outputPath = string.Empty;

    [ObservableProperty]
    private string _outputFileName = string.Empty;

    // Progress
    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _progressText = "Ready";

    [ObservableProperty]
    private bool _isExporting;

    [ObservableProperty]
    private bool _canExport;

    // Results
    [ObservableProperty]
    private string _sourceLoudnessText = "--";

    [ObservableProperty]
    private string _outputLoudnessText = "--";

    [ObservableProperty]
    private string _gainAppliedText = "--";

    [ObservableProperty]
    private ExportResult? _lastResult;

    // Collections
    public ObservableCollection<ExportPreset> Presets { get; } = new();
    public ObservableCollection<AudioFormat> Formats { get; } = new();
    public ObservableCollection<int> SampleRates { get; } = new();
    public ObservableCollection<int> BitDepths { get; } = new();
    public ObservableCollection<int> BitRates { get; } = new();

    /// <summary>
    /// Event raised when export is complete and dialog should close.
    /// </summary>
    public event EventHandler? ExportCompleted;

    /// <summary>
    /// Event raised when dialog should be cancelled.
    /// </summary>
    public event EventHandler? CancelRequested;

    public ExportViewModel()
    {
        _selectedPreset = ExportPresets.YouTube;

        InitializeCollections();
        ApplyPreset(SelectedPreset);
    }

    private void InitializeCollections()
    {
        // Add all presets
        foreach (var preset in ExportPresets.All)
        {
            Presets.Add(preset);
        }
        // Add custom option
        Presets.Add(ExportPresets.Custom("Custom"));

        // Formats
        Formats.Add(AudioFormat.Wav);
        Formats.Add(AudioFormat.Mp3);
        Formats.Add(AudioFormat.Flac);
        Formats.Add(AudioFormat.Ogg);

        // Sample rates
        SampleRates.Add(44100);
        SampleRates.Add(48000);
        SampleRates.Add(88200);
        SampleRates.Add(96000);

        // Bit depths
        BitDepths.Add(16);
        BitDepths.Add(24);
        BitDepths.Add(32);

        // Bit rates
        BitRates.Add(128);
        BitRates.Add(192);
        BitRates.Add(256);
        BitRates.Add(320);
    }

    partial void OnSelectedPresetChanged(ExportPreset value)
    {
        IsCustomPreset = value.Name == "Custom";
        if (!IsCustomPreset)
        {
            ApplyPreset(value);
        }
    }

    partial void OnInputPathChanged(string value)
    {
        UpdateCanExport();
        if (File.Exists(value) && string.IsNullOrEmpty(OutputFileName))
        {
            OutputFileName = Path.GetFileNameWithoutExtension(value) + "_export";
        }
    }

    partial void OnOutputPathChanged(string value)
    {
        UpdateCanExport();
    }

    partial void OnOutputFileNameChanged(string value)
    {
        UpdateCanExport();
    }

    partial void OnSelectedFormatChanged(AudioFormat value)
    {
        if (IsCustomPreset)
        {
            UpdateCustomPreset();
        }
    }

    partial void OnNormalizeLoudnessChanged(bool value)
    {
        if (IsCustomPreset)
        {
            UpdateCustomPreset();
        }
    }

    private void ApplyPreset(ExportPreset preset)
    {
        SelectedFormat = preset.Format;
        SelectedSampleRate = preset.SampleRate;
        SelectedBitDepth = preset.BitDepth;
        SelectedBitRate = preset.BitRate ?? 320;
        NormalizeLoudness = preset.NormalizeLoudness;
        TargetLufs = preset.TargetLufs ?? -14f;
        MaxTruePeak = preset.MaxTruePeak ?? -1f;
    }

    private void UpdateCustomPreset()
    {
        // Update the custom preset with current settings
        SelectedPreset = ExportPresets.Custom(
            format: SelectedFormat,
            sampleRate: SelectedSampleRate,
            bitDepth: SelectedBitDepth,
            bitRate: SelectedFormat == AudioFormat.Wav ? null : SelectedBitRate,
            targetLufs: NormalizeLoudness ? TargetLufs : null,
            maxTruePeak: MaxTruePeak,
            normalizeLoudness: NormalizeLoudness);
    }

    private void UpdateCanExport()
    {
        CanExport = !IsExporting &&
                    File.Exists(InputPath) &&
                    !string.IsNullOrWhiteSpace(OutputPath) &&
                    !string.IsNullOrWhiteSpace(OutputFileName) &&
                    Directory.Exists(OutputPath);
    }

    /// <summary>
    /// Gets the effective export preset with current settings.
    /// </summary>
    public ExportPreset GetEffectivePreset()
    {
        if (IsCustomPreset)
        {
            return ExportPresets.Custom(
                format: SelectedFormat,
                sampleRate: SelectedSampleRate,
                bitDepth: SelectedBitDepth,
                bitRate: SelectedFormat == AudioFormat.Wav || SelectedFormat == AudioFormat.Flac ? null : SelectedBitRate,
                targetLufs: NormalizeLoudness ? TargetLufs : null,
                maxTruePeak: MaxTruePeak,
                normalizeLoudness: NormalizeLoudness);
        }

        // Return selected preset with any overrides
        return SelectedPreset with
        {
            TargetLufs = NormalizeLoudness ? TargetLufs : null,
            MaxTruePeak = MaxTruePeak,
            NormalizeLoudness = NormalizeLoudness
        };
    }

    [RelayCommand]
    private void BrowseInput()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Audio File to Export",
            Filter = "Audio Files (*.wav;*.mp3;*.flac;*.ogg)|*.wav;*.mp3;*.flac;*.ogg|WAV Files (*.wav)|*.wav|All Files (*.*)|*.*",
            DefaultExt = ".wav"
        };

        if (dialog.ShowDialog() == true)
        {
            InputPath = dialog.FileName;
        }
    }

    [RelayCommand]
    private void BrowseOutput()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select Output Directory",
            SelectedPath = string.IsNullOrEmpty(OutputPath) ? Environment.GetFolderPath(Environment.SpecialFolder.MyMusic) : OutputPath
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            OutputPath = dialog.SelectedPath;
        }
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (!CanExport) return;

        IsExporting = true;
        IsBusy = true;
        Progress = 0;
        ProgressText = "Starting export...";
        SourceLoudnessText = "--";
        OutputLoudnessText = "--";
        GainAppliedText = "--";

        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            var preset = GetEffectivePreset();
            string fullOutputPath = Path.Combine(OutputPath, OutputFileName + preset.FileExtension);

            var progressHandler = new Progress<ExportProgress>(p =>
            {
                Progress = p.Progress * 100;
                ProgressText = p.Message;

                // Update loudness display during analysis
                if (p.Phase == ExportPhase.Analyzing && p.Message.Contains("Source:"))
                {
                    SourceLoudnessText = p.Message.Replace("Source: ", "");
                }
            });

            LastResult = await AudioRecorder.ExportWithPresetAsync(
                InputPath,
                fullOutputPath,
                preset,
                progressHandler,
                _cancellationTokenSource.Token);

            if (LastResult.Success)
            {
                Progress = 100;
                ProgressText = "Export complete!";

                if (LastResult.SourceMeasurement != null)
                {
                    SourceLoudnessText = $"{LastResult.SourceMeasurement.IntegratedLoudness:F1} LUFS / {LastResult.SourceMeasurement.TruePeak:F1} dBTP";
                }

                if (LastResult.OutputMeasurement != null)
                {
                    OutputLoudnessText = $"{LastResult.OutputMeasurement.IntegratedLoudness:F1} LUFS / {LastResult.OutputMeasurement.TruePeak:F1} dBTP";

                    if (LastResult.SourceMeasurement != null)
                    {
                        double gainApplied = LastResult.OutputMeasurement.IntegratedLoudness - LastResult.SourceMeasurement.IntegratedLoudness;
                        GainAppliedText = $"{gainApplied:+0.0;-0.0;0.0} dB";
                    }
                }

                StatusMessage = "Export completed successfully";
            }
            else
            {
                ProgressText = $"Export failed: {LastResult.ErrorMessage}";
                StatusMessage = LastResult.ErrorMessage;
                MessageBox.Show($"Export failed: {LastResult.ErrorMessage}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (OperationCanceledException)
        {
            ProgressText = "Export cancelled";
            StatusMessage = "Export cancelled by user";
        }
        catch (Exception ex)
        {
            ProgressText = $"Error: {ex.Message}";
            StatusMessage = ex.Message;
            MessageBox.Show($"Export error: {ex.Message}", "Export Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsExporting = false;
            IsBusy = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            UpdateCanExport();
        }
    }

    [RelayCommand]
    private void CancelExport()
    {
        if (IsExporting)
        {
            _cancellationTokenSource?.Cancel();
            ProgressText = "Cancelling...";
        }
        else
        {
            CancelRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    private void OpenOutputFolder()
    {
        if (Directory.Exists(OutputPath))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = OutputPath,
                UseShellExecute = true
            });
        }
    }

    [RelayCommand]
    private void Close()
    {
        if (LastResult?.Success == true)
        {
            ExportCompleted?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            CancelRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Sets the input file path and updates the output filename.
    /// </summary>
    public void SetInputFile(string path)
    {
        InputPath = path;
        if (File.Exists(path))
        {
            OutputFileName = Path.GetFileNameWithoutExtension(path) + "_export";
            if (string.IsNullOrEmpty(OutputPath))
            {
                OutputPath = Path.GetDirectoryName(path) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            }
        }
    }
}
