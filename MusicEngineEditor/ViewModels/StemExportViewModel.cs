using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngine.Core;
using NAudio.Wave;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// Represents a stem item in the UI with selection state.
/// </summary>
public partial class StemItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isEnabled = true;

    [ObservableProperty]
    private string _status = "Ready";

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isExporting;

    [ObservableProperty]
    private bool _isComplete;

    [ObservableProperty]
    private bool _hasFailed;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _outputLoudness;

    /// <summary>
    /// The underlying audio source for this stem.
    /// </summary>
    public ISampleProvider? Source { get; set; }

    /// <summary>
    /// Creates a StemDefinition from this view model.
    /// </summary>
    public StemDefinition ToDefinition()
    {
        if (Source == null)
        {
            throw new InvalidOperationException($"Stem '{Name}' has no audio source assigned.");
        }
        return new StemDefinition(Name, Source, IsEnabled);
    }
}

/// <summary>
/// ViewModel for the Stem Export dialog.
/// </summary>
public partial class StemExportViewModel : ViewModelBase
{
    private readonly StemExporter _exporter;
    private CancellationTokenSource? _cancellationTokenSource;

    // Stem list
    public ObservableCollection<StemItemViewModel> Stems { get; } = new();

    // Preset selection
    [ObservableProperty]
    private ExportPreset _selectedPreset;

    // Output settings
    [ObservableProperty]
    private string _outputDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);

    [ObservableProperty]
    private string _projectName = "Project";

    [ObservableProperty]
    private bool _createSubfolder = true;

    // Duration settings
    [ObservableProperty]
    private double _durationMinutes = 5.0;

    [ObservableProperty]
    private double _durationSeconds = 0.0;

    // Progress
    [ObservableProperty]
    private double _overallProgress;

    [ObservableProperty]
    private string _progressText = "Ready";

    [ObservableProperty]
    private string _currentStemName = string.Empty;

    [ObservableProperty]
    private int _currentStemIndex;

    [ObservableProperty]
    private int _totalStems;

    [ObservableProperty]
    private bool _isExporting;

    [ObservableProperty]
    private bool _canExport;

    // Results
    [ObservableProperty]
    private StemExportResult? _lastResult;

    [ObservableProperty]
    private string _resultSummary = string.Empty;

    // Collections
    public ObservableCollection<ExportPreset> Presets { get; } = new();

    /// <summary>
    /// Event raised when export is complete and dialog should close.
    /// </summary>
    public event EventHandler? ExportCompleted;

    /// <summary>
    /// Event raised when dialog should be cancelled.
    /// </summary>
    public event EventHandler? CancelRequested;

    public StemExportViewModel() : this(new StemExporter()) { }

    public StemExportViewModel(StemExporter exporter)
    {
        _exporter = exporter;
        _selectedPreset = ExportPresets.YouTube;

        InitializeCollections();

        // Subscribe to collection changes
        Stems.CollectionChanged += OnStemsCollectionChanged;

        UpdateCanExport();
    }

    private void InitializeCollections()
    {
        // Add all presets
        foreach (var preset in ExportPresets.All)
        {
            Presets.Add(preset);
        }
    }

    partial void OnOutputDirectoryChanged(string value)
    {
        UpdateCanExport();
    }

    private void OnStemsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        UpdateCanExport();
    }

    private void UpdateCanExport()
    {
        CanExport = !IsExporting &&
                    !string.IsNullOrWhiteSpace(OutputDirectory) &&
                    Directory.Exists(OutputDirectory) &&
                    Stems.Any(s => s.IsEnabled && s.Source != null);
    }

    /// <summary>
    /// Gets the duration as a TimeSpan.
    /// </summary>
    public TimeSpan Duration => TimeSpan.FromMinutes(DurationMinutes) + TimeSpan.FromSeconds(DurationSeconds);

    /// <summary>
    /// Gets the effective output directory (with optional subfolder).
    /// </summary>
    public string EffectiveOutputDirectory
    {
        get
        {
            if (CreateSubfolder && !string.IsNullOrWhiteSpace(ProjectName))
            {
                return Path.Combine(OutputDirectory, $"{ProjectName}_Stems");
            }
            return OutputDirectory;
        }
    }

    /// <summary>
    /// Adds a stem to the export list.
    /// </summary>
    public void AddStem(string name, ISampleProvider source, bool enabled = true)
    {
        var stem = new StemItemViewModel
        {
            Name = name,
            Source = source,
            IsEnabled = enabled
        };

        // Subscribe to property changes
        stem.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(StemItemViewModel.IsEnabled))
            {
                UpdateCanExport();
            }
        };

        Stems.Add(stem);
        UpdateCanExport();
    }

    /// <summary>
    /// Clears all stems from the export list.
    /// </summary>
    public void ClearStems()
    {
        Stems.Clear();
        UpdateCanExport();
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var stem in Stems)
        {
            stem.IsEnabled = true;
        }
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var stem in Stems)
        {
            stem.IsEnabled = false;
        }
    }

    [RelayCommand]
    private void InvertSelection()
    {
        foreach (var stem in Stems)
        {
            stem.IsEnabled = !stem.IsEnabled;
        }
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
    private async Task ExportAsync()
    {
        if (!CanExport) return;

        var enabledStems = Stems.Where(s => s.IsEnabled && s.Source != null).ToList();
        if (enabledStems.Count == 0)
        {
            MessageBox.Show("No stems selected for export.", "Stem Export",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsExporting = true;
        IsBusy = true;
        OverallProgress = 0;
        ProgressText = "Starting stem export...";
        TotalStems = enabledStems.Count;

        // Reset all stem states
        foreach (var stem in Stems)
        {
            stem.Status = stem.IsEnabled ? "Pending" : "Skipped";
            stem.Progress = 0;
            stem.IsExporting = false;
            stem.IsComplete = false;
            stem.HasFailed = false;
            stem.ErrorMessage = null;
            stem.OutputLoudness = null;
        }

        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            // Create output directory
            string outputDir = EffectiveOutputDirectory;
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            var stemDefinitions = enabledStems.Select(s => s.ToDefinition()).ToList();

            var progressHandler = new Progress<StemExportProgress>(p =>
            {
                OverallProgress = p.OverallProgress * 100;
                CurrentStemName = p.StemName;
                CurrentStemIndex = p.StemIndex;
                ProgressText = p.Message;

                // Update individual stem progress
                if (p.StemIndex < enabledStems.Count)
                {
                    var stemVm = enabledStems[p.StemIndex];
                    stemVm.Progress = p.StemProgress * 100;
                    stemVm.IsExporting = p.Phase == StemExportPhase.Rendering || p.Phase == StemExportPhase.Normalizing;
                    stemVm.Status = p.Message;
                }
            });

            LastResult = await _exporter.ExportStemsAsync(
                stemDefinitions,
                outputDir,
                SelectedPreset,
                Duration,
                progressHandler,
                _cancellationTokenSource.Token);

            // Update stem states from results
            for (int i = 0; i < LastResult.StemResults.Count; i++)
            {
                var result = LastResult.StemResults[i];
                var stemVm = enabledStems.FirstOrDefault(s => s.Name == result.StemName);

                if (stemVm != null)
                {
                    stemVm.IsExporting = false;
                    stemVm.IsComplete = result.Success;
                    stemVm.HasFailed = !result.Success;
                    stemVm.Progress = 100;
                    stemVm.Status = result.Success ? "Complete" : "Failed";
                    stemVm.ErrorMessage = result.ErrorMessage;

                    if (result.Measurement != null)
                    {
                        stemVm.OutputLoudness = $"{result.Measurement.IntegratedLoudness:F1} LUFS";
                    }
                }
            }

            OverallProgress = 100;
            ResultSummary = LastResult.Summary;
            ProgressText = LastResult.Success ? "Stem export complete!" : "Stem export completed with errors";
            StatusMessage = ResultSummary;
        }
        catch (OperationCanceledException)
        {
            ProgressText = "Export cancelled";
            StatusMessage = "Stem export cancelled by user";
            ResultSummary = "Export cancelled";
        }
        catch (Exception ex)
        {
            ProgressText = $"Error: {ex.Message}";
            StatusMessage = ex.Message;
            ResultSummary = $"Export failed: {ex.Message}";
            MessageBox.Show($"Stem export error: {ex.Message}", "Export Error",
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
        string dir = EffectiveOutputDirectory;
        if (Directory.Exists(dir))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true
            });
        }
        else if (Directory.Exists(OutputDirectory))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = OutputDirectory,
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
    /// Sets the project name (used for subfolder naming).
    /// </summary>
    public void SetProjectName(string name)
    {
        ProjectName = name;
    }

    /// <summary>
    /// Loads stems from an audio engine.
    /// Note: The engine does not currently expose individual channel outputs.
    /// Use LoadFromSources with your own ISampleProvider sources instead.
    /// </summary>
    public void LoadFromEngine(AudioEngine engine)
    {
        ClearStems();

        // The engine does not currently expose individual channel outputs.
        // Users should manually configure stems using LoadFromSources.
        // This method is provided for future extension when channel access is available.
    }

    /// <summary>
    /// Loads stems from a dictionary of named sources.
    /// </summary>
    public void LoadFromSources(System.Collections.Generic.IDictionary<string, ISampleProvider> sources)
    {
        ClearStems();

        foreach (var kvp in sources)
        {
            AddStem(kvp.Key, kvp.Value, true);
        }
    }
}
