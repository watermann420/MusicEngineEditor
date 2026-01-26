// MusicEngineEditor - Batch Processor Dialog
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngine.Core;
using MusicEngineEditor.ViewModels;
using NAudio.Wave;
using Microsoft.Win32;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Represents a file in the batch processing queue.
/// </summary>
public partial class BatchFileItem : ObservableObject
{
    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _fullPath = string.Empty;

    [ObservableProperty]
    private string _directoryPath = string.Empty;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private bool _isComplete;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _outputPath;
}

/// <summary>
/// Represents an effect chain preset.
/// </summary>
public class EffectChainPreset
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Effects { get; set; } = new();
    public Func<ISampleProvider, ISampleProvider>? ProcessFunc { get; set; }
}

/// <summary>
/// ViewModel for the Batch Processor dialog.
/// </summary>
public partial class BatchProcessorViewModel : ViewModelBase
{
    private CancellationTokenSource? _cancellationTokenSource;

    // File list
    public ObservableCollection<BatchFileItem> Files { get; } = new();

    [ObservableProperty]
    private BatchFileItem? _selectedFile;

    // Effect chain
    public ObservableCollection<EffectChainPreset> AvailableChains { get; } = new();

    [ObservableProperty]
    private EffectChainPreset? _selectedChain;

    // Output format
    public ObservableCollection<string> OutputFormats { get; } = new()
    {
        "WAV",
        "MP3",
        "FLAC",
        "OGG",
        "AIFF"
    };

    public ObservableCollection<string> BitDepths { get; } = new()
    {
        "16-bit",
        "24-bit",
        "32-bit Float"
    };

    public ObservableCollection<string> SampleRates { get; } = new()
    {
        "44100 Hz",
        "48000 Hz",
        "88200 Hz",
        "96000 Hz"
    };

    [ObservableProperty]
    private string _selectedFormat = "WAV";

    [ObservableProperty]
    private string _selectedBitDepth = "24-bit";

    [ObservableProperty]
    private string _selectedSampleRate = "44100 Hz";

    // Naming
    [ObservableProperty]
    private string _filePrefix = string.Empty;

    [ObservableProperty]
    private string _fileSuffix = "_processed";

    [ObservableProperty]
    private bool _overwriteExisting;

    // Output
    [ObservableProperty]
    private string _outputDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);

    [ObservableProperty]
    private bool _useSourceDirectory;

    // Progress
    [ObservableProperty]
    private double _overallProgress;

    [ObservableProperty]
    private string _progressText = "Ready";

    [ObservableProperty]
    private string _currentFileName = string.Empty;

    [ObservableProperty]
    private int _processedCount;

    [ObservableProperty]
    private int _successCount;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private bool _isComplete;

    [ObservableProperty]
    private bool _canProcess;

    // Log
    [ObservableProperty]
    private bool _showLog;

    [ObservableProperty]
    private string _logOutput = string.Empty;

    private readonly StringBuilder _logBuilder = new();

    /// <summary>
    /// Gets a preview of the output file name.
    /// </summary>
    public string FileNamePreview
    {
        get
        {
            var sampleName = Files.FirstOrDefault()?.FileName ?? "example.wav";
            var baseName = Path.GetFileNameWithoutExtension(sampleName);
            var prefix = !string.IsNullOrEmpty(FilePrefix) ? $"{FilePrefix}_" : "";
            var suffix = !string.IsNullOrEmpty(FileSuffix) ? FileSuffix : "";
            var extension = GetExtensionForFormat(SelectedFormat);
            return $"{prefix}{baseName}{suffix}{extension}";
        }
    }

    public event EventHandler? ProcessingCompleted;

    public BatchProcessorViewModel()
    {
        InitializeEffectChains();
        UpdateCanProcess();
        Files.CollectionChanged += (s, e) => UpdateCanProcess();
    }

    private void InitializeEffectChains()
    {
        AvailableChains.Add(new EffectChainPreset
        {
            Name = "No Processing (Format Convert Only)",
            Description = "Just convert format without applying effects",
            Effects = new List<string> { "Pass-through" },
            ProcessFunc = source => source
        });

        AvailableChains.Add(new EffectChainPreset
        {
            Name = "Normalize",
            Description = "Normalize audio to 0 dB peak",
            Effects = new List<string> { "Peak Normalize" },
            ProcessFunc = source =>
            {
                // Simple peak normalization would be applied here
                return source;
            }
        });

        AvailableChains.Add(new EffectChainPreset
        {
            Name = "Master (Compression + Limiter)",
            Description = "Light compression and limiting for mastering",
            Effects = new List<string> { "Compressor (3:1, -12dB)", "Limiter (-0.3dB)" },
            ProcessFunc = source =>
            {
                // Would chain Compressor and Limiter here
                return source;
            }
        });

        AvailableChains.Add(new EffectChainPreset
        {
            Name = "Reverb",
            Description = "Add hall reverb",
            Effects = new List<string> { "Reverb (Hall, 30% wet)" },
            ProcessFunc = source =>
            {
                // Would apply reverb here
                return source;
            }
        });

        AvailableChains.Add(new EffectChainPreset
        {
            Name = "Loudness Normalize (LUFS)",
            Description = "Normalize to -14 LUFS for streaming",
            Effects = new List<string> { "LUFS Normalize (-14 LUFS)" },
            ProcessFunc = source => source
        });

        SelectedChain = AvailableChains[0];
    }

    partial void OnFilePrefixChanged(string value) => OnPropertyChanged(nameof(FileNamePreview));
    partial void OnFileSuffixChanged(string value) => OnPropertyChanged(nameof(FileNamePreview));
    partial void OnSelectedFormatChanged(string value) => OnPropertyChanged(nameof(FileNamePreview));

    private void UpdateCanProcess()
    {
        CanProcess = !IsProcessing && Files.Count > 0 && SelectedChain != null &&
                     (UseSourceDirectory || (!string.IsNullOrWhiteSpace(OutputDirectory) && Directory.Exists(OutputDirectory)));
    }

    partial void OnIsProcessingChanged(bool value) => UpdateCanProcess();
    partial void OnUseSourceDirectoryChanged(bool value) => UpdateCanProcess();
    partial void OnOutputDirectoryChanged(string value) => UpdateCanProcess();

    [RelayCommand]
    private void AddFiles()
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "Audio Files|*.wav;*.mp3;*.flac;*.ogg;*.aiff;*.aif|All Files|*.*",
            Title = "Select Audio Files"
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var file in dialog.FileNames)
            {
                AddFile(file);
            }
        }
    }

    [RelayCommand]
    private void AddFolder()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select Folder Containing Audio Files"
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var extensions = new[] { ".wav", ".mp3", ".flac", ".ogg", ".aiff", ".aif" };
            var files = Directory.GetFiles(dialog.SelectedPath)
                .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()));

            foreach (var file in files)
            {
                AddFile(file);
            }
        }
    }

    private void AddFile(string path)
    {
        if (Files.Any(f => f.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase)))
            return;

        Files.Add(new BatchFileItem
        {
            FileName = Path.GetFileName(path),
            FullPath = path,
            DirectoryPath = Path.GetDirectoryName(path) ?? ""
        });
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        if (SelectedFile != null)
        {
            Files.Remove(SelectedFile);
        }
    }

    [RelayCommand]
    private void ClearAll()
    {
        Files.Clear();
    }

    [RelayCommand]
    private void BrowseOutput()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select Output Directory",
            SelectedPath = OutputDirectory
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            OutputDirectory = dialog.SelectedPath;
        }
    }

    [RelayCommand]
    private async Task ProcessAsync()
    {
        if (!CanProcess) return;

        IsProcessing = true;
        IsComplete = false;
        ProcessedCount = 0;
        SuccessCount = 0;
        OverallProgress = 0;
        _logBuilder.Clear();
        LogOutput = string.Empty;

        _cancellationTokenSource = new CancellationTokenSource();

        Log($"Starting batch processing of {Files.Count} files...");
        Log($"Effect chain: {SelectedChain?.Name}");
        Log($"Output format: {SelectedFormat} {SelectedBitDepth} @ {SelectedSampleRate}");
        Log("");

        // Reset all file states
        foreach (var file in Files)
        {
            file.IsProcessing = false;
            file.IsComplete = false;
            file.HasError = false;
            file.ErrorMessage = null;
            file.Progress = 0;
        }

        try
        {
            for (int i = 0; i < Files.Count; i++)
            {
                if (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    Log("Processing cancelled by user.");
                    break;
                }

                var file = Files[i];
                CurrentFileName = file.FileName;
                file.IsProcessing = true;

                try
                {
                    await ProcessFileAsync(file, _cancellationTokenSource.Token);
                    file.IsComplete = true;
                    SuccessCount++;
                    Log($"[OK] {file.FileName}");
                }
                catch (OperationCanceledException)
                {
                    file.HasError = true;
                    file.ErrorMessage = "Cancelled";
                    Log($"[CANCELLED] {file.FileName}");
                    break;
                }
                catch (Exception ex)
                {
                    file.HasError = true;
                    file.ErrorMessage = ex.Message;
                    Log($"[ERROR] {file.FileName}: {ex.Message}");
                }
                finally
                {
                    file.IsProcessing = false;
                    file.Progress = 100;
                }

                ProcessedCount = i + 1;
                OverallProgress = ((i + 1) / (double)Files.Count) * 100;
            }

            Log("");
            Log($"Batch processing complete. {SuccessCount}/{Files.Count} files processed successfully.");

            ProgressText = $"Complete: {SuccessCount}/{Files.Count} files";
            IsComplete = true;
        }
        catch (Exception ex)
        {
            Log($"Batch processing failed: {ex.Message}");
            ProgressText = "Error occurred";
        }
        finally
        {
            IsProcessing = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            ProcessingCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task ProcessFileAsync(BatchFileItem file, CancellationToken cancellationToken)
    {
        // Determine output path
        string outputDir = UseSourceDirectory ? file.DirectoryPath : OutputDirectory;
        string baseName = Path.GetFileNameWithoutExtension(file.FileName);
        string prefix = !string.IsNullOrEmpty(FilePrefix) ? $"{FilePrefix}_" : "";
        string suffix = !string.IsNullOrEmpty(FileSuffix) ? FileSuffix : "";
        string extension = GetExtensionForFormat(SelectedFormat);
        string outputFileName = $"{prefix}{baseName}{suffix}{extension}";
        string outputPath = Path.Combine(outputDir, outputFileName);

        file.OutputPath = outputPath;

        // Check if file exists
        if (File.Exists(outputPath) && !OverwriteExisting)
        {
            throw new IOException($"Output file already exists: {outputFileName}");
        }

        // Process the file
        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Read input file
            using var reader = CreateReader(file.FullPath);
            var sampleProvider = reader.ToSampleProvider();

            // Apply effect chain
            if (SelectedChain?.ProcessFunc != null)
            {
                sampleProvider = SelectedChain.ProcessFunc(sampleProvider);
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Get output settings
            int sampleRate = ParseSampleRate(SelectedSampleRate);
            int bitDepth = ParseBitDepth(SelectedBitDepth);

            // Write output file
            WriteOutputFile(outputPath, sampleProvider, sampleRate, bitDepth, reader.TotalTime, cancellationToken, file);

        }, cancellationToken);
    }

    private static WaveStream CreateReader(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".mp3" => new Mp3FileReader(path),
            ".wav" => new WaveFileReader(path),
            ".aiff" or ".aif" => new AiffFileReader(path),
            _ => new AudioFileReader(path)
        };
    }

    private void WriteOutputFile(string path, ISampleProvider source, int sampleRate, int bitDepth, TimeSpan duration, CancellationToken ct, BatchFileItem file)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();

        // For simplicity, write as WAV and let NAudio handle format
        var format = bitDepth switch
        {
            16 => new WaveFormat(sampleRate, 16, source.WaveFormat.Channels),
            24 => new WaveFormat(sampleRate, 24, source.WaveFormat.Channels),
            _ => WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, source.WaveFormat.Channels)
        };

        // Estimate total samples
        long totalSamples = (long)(duration.TotalSeconds * sampleRate * source.WaveFormat.Channels);
        long samplesWritten = 0;

        using var writer = new WaveFileWriter(path, format);
        var buffer = new float[sampleRate * source.WaveFormat.Channels]; // 1 second buffer
        int read;

        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            ct.ThrowIfCancellationRequested();

            // Write samples
            if (bitDepth == 16)
            {
                var bytes = new byte[read * 2];
                for (int i = 0; i < read; i++)
                {
                    short sample = (short)(buffer[i] * 32767);
                    bytes[i * 2] = (byte)(sample & 0xFF);
                    bytes[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
                }
                writer.Write(bytes, 0, bytes.Length);
            }
            else if (bitDepth == 24)
            {
                var bytes = new byte[read * 3];
                for (int i = 0; i < read; i++)
                {
                    int sample = (int)(buffer[i] * 8388607);
                    bytes[i * 3] = (byte)(sample & 0xFF);
                    bytes[i * 3 + 1] = (byte)((sample >> 8) & 0xFF);
                    bytes[i * 3 + 2] = (byte)((sample >> 16) & 0xFF);
                }
                writer.Write(bytes, 0, bytes.Length);
            }
            else
            {
                writer.WriteSamples(buffer, 0, read);
            }

            samplesWritten += read;

            // Update file progress
            if (totalSamples > 0)
            {
                file.Progress = Math.Min(99, (samplesWritten / (double)totalSamples) * 100);
            }
        }
    }

    private static string GetExtensionForFormat(string format)
    {
        return format.ToUpperInvariant() switch
        {
            "WAV" => ".wav",
            "MP3" => ".mp3",
            "FLAC" => ".flac",
            "OGG" => ".ogg",
            "AIFF" => ".aiff",
            _ => ".wav"
        };
    }

    private static int ParseSampleRate(string sampleRate)
    {
        return sampleRate switch
        {
            "44100 Hz" => 44100,
            "48000 Hz" => 48000,
            "88200 Hz" => 88200,
            "96000 Hz" => 96000,
            _ => 44100
        };
    }

    private static int ParseBitDepth(string bitDepth)
    {
        return bitDepth switch
        {
            "16-bit" => 16,
            "24-bit" => 24,
            "32-bit Float" => 32,
            _ => 24
        };
    }

    private void Log(string message)
    {
        _logBuilder.AppendLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        LogOutput = _logBuilder.ToString();
    }

    [RelayCommand]
    private void Cancel()
    {
        if (IsProcessing)
        {
            _cancellationTokenSource?.Cancel();
            ProgressText = "Cancelling...";
        }
    }

    [RelayCommand]
    private void OpenOutputFolder()
    {
        string dir = UseSourceDirectory
            ? (Files.FirstOrDefault()?.DirectoryPath ?? OutputDirectory)
            : OutputDirectory;

        if (Directory.Exists(dir))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true
            });
        }
    }
}

/// <summary>
/// Batch audio processor dialog for processing multiple files.
/// </summary>
public partial class BatchProcessorDialog : Window
{
    private readonly BatchProcessorViewModel _viewModel;

    public BatchProcessorDialog()
    {
        InitializeComponent();
        _viewModel = new BatchProcessorViewModel();
        DataContext = _viewModel;
    }

    /// <summary>
    /// Gets the view model.
    /// </summary>
    public BatchProcessorViewModel ViewModel => _viewModel;

    /// <summary>
    /// Adds files to the batch queue.
    /// </summary>
    public void AddFiles(IEnumerable<string> filePaths)
    {
        foreach (var path in filePaths)
        {
            if (File.Exists(path))
            {
                _viewModel.Files.Add(new BatchFileItem
                {
                    FileName = Path.GetFileName(path),
                    FullPath = path,
                    DirectoryPath = Path.GetDirectoryName(path) ?? ""
                });
            }
        }
    }

    private void OnFileDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            var audioExtensions = new[] { ".wav", ".mp3", ".flac", ".ogg", ".aiff", ".aif" };

            foreach (var file in files)
            {
                if (Directory.Exists(file))
                {
                    // Add all audio files from directory
                    var dirFiles = Directory.GetFiles(file)
                        .Where(f => audioExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));
                    foreach (var f in dirFiles)
                    {
                        AddFile(f);
                    }
                }
                else if (audioExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                {
                    AddFile(file);
                }
            }
        }
    }

    private void AddFile(string path)
    {
        if (!_viewModel.Files.Any(f => f.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase)))
        {
            _viewModel.Files.Add(new BatchFileItem
            {
                FileName = Path.GetFileName(path),
                FullPath = path,
                DirectoryPath = Path.GetDirectoryName(path) ?? ""
            });
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }
}
