using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngineEditor.Models;
using MusicEngineEditor.Services;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// Main ViewModel for the IDE
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly IProjectService _projectService;
    private readonly IScriptExecutionService _executionService;

    [ObservableProperty]
    private MusicProject? _currentProject;

    [ObservableProperty]
    private EditorTabViewModel? _activeDocument;

    [ObservableProperty]
    private ProjectExplorerViewModel? _projectExplorer;

    [ObservableProperty]
    private OutputViewModel? _output;

    [ObservableProperty]
    private bool _isProjectExplorerVisible = true;

    [ObservableProperty]
    private bool _isOutputVisible = true;

    [ObservableProperty]
    private bool _isPropertiesVisible = true;

    [ObservableProperty]
    private int _currentBpm = 120;

    [ObservableProperty]
    private string _playbackStatus = "Stopped";

    [ObservableProperty]
    private string _caretPosition = "1:1";

    [ObservableProperty]
    private string _encoding = "UTF-8";

    [ObservableProperty]
    private ObservableCollection<string> _audioDevices = new();

    [ObservableProperty]
    private string? _selectedAudioDevice;

    public ObservableCollection<EditorTabViewModel> OpenDocuments { get; } = new();
    public ObservableCollection<string> RecentProjects { get; } = new();

    public MainViewModel(IProjectService projectService, IScriptExecutionService executionService)
    {
        _projectService = projectService;
        _executionService = executionService;

        ProjectExplorer = new ProjectExplorerViewModel();
        Output = new OutputViewModel();

        // Subscribe to events
        _projectService.ProjectLoaded += OnProjectLoaded;
        _executionService.OutputReceived += OnOutputReceived;
        _executionService.ExecutionStarted += OnExecutionStarted;
        _executionService.ExecutionStopped += OnExecutionStopped;
    }

    [RelayCommand]
    private async Task NewProjectAsync()
    {
        // TODO: Show NewProjectDialog
        StatusMessage = "Creating new project...";
    }

    [RelayCommand]
    private async Task OpenProjectAsync()
    {
        // TODO: Show OpenFileDialog for .meproj files
        StatusMessage = "Opening project...";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (ActiveDocument?.Script != null)
        {
            await _projectService.SaveScriptAsync(ActiveDocument.Script);
            ActiveDocument.IsDirty = false;
            StatusMessage = $"Saved: {ActiveDocument.Title}";
        }
    }

    [RelayCommand]
    private async Task SaveAllAsync()
    {
        foreach (var doc in OpenDocuments)
        {
            if (doc.IsDirty && doc.Script != null)
            {
                await _projectService.SaveScriptAsync(doc.Script);
                doc.IsDirty = false;
            }
        }

        if (CurrentProject != null)
        {
            await _projectService.SaveProjectAsync(CurrentProject);
        }

        StatusMessage = "All files saved";
    }

    [RelayCommand]
    private void NewFile()
    {
        // TODO: Show NewFileDialog
    }

    [RelayCommand]
    private async Task RunAsync()
    {
        if (CurrentProject == null)
        {
            Output?.AppendLine("No project loaded.");
            return;
        }

        await SaveAllAsync();
        PlaybackStatus = "Running";
        await _executionService.RunAsync(CurrentProject);
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        await _executionService.StopAsync();
        PlaybackStatus = "Stopped";
    }

    [RelayCommand]
    private void Find()
    {
        // TODO: Show Find dialog
    }

    [RelayCommand]
    private void Replace()
    {
        // TODO: Show Replace dialog
    }

    [RelayCommand]
    private void AddScript()
    {
        // TODO: Show AddScriptDialog
    }

    [RelayCommand]
    private void AddExistingFile()
    {
        // TODO: Show file picker
    }

    [RelayCommand]
    private void ImportAudio()
    {
        // TODO: Show ImportAudioDialog
    }

    [RelayCommand]
    private void AddReference()
    {
        // TODO: Show AddReferenceDialog
    }

    [RelayCommand]
    private void ProjectSettings()
    {
        // TODO: Show ProjectSettingsDialog
    }

    [RelayCommand]
    private void Debug()
    {
        // TODO: Implement debugging
    }

    [RelayCommand]
    private void Documentation()
    {
        // Open documentation URL
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://github.com/watermann420/MusicEngine",
            UseShellExecute = true
        });
    }

    [RelayCommand]
    private void About()
    {
        // TODO: Show About dialog
    }

    [RelayCommand]
    private void Exit()
    {
        // TODO: Check for unsaved changes
        System.Windows.Application.Current.Shutdown();
    }

    public void OpenScript(MusicScript script)
    {
        // Check if already open
        foreach (var doc in OpenDocuments)
        {
            if (doc.Script?.FilePath == script.FilePath)
            {
                ActiveDocument = doc;
                return;
            }
        }

        // Create new tab
        var tab = new EditorTabViewModel(script);
        OpenDocuments.Add(tab);
        ActiveDocument = tab;
    }

    public void CloseDocument(EditorTabViewModel document)
    {
        if (document.IsDirty)
        {
            // TODO: Ask to save
        }

        OpenDocuments.Remove(document);

        if (ActiveDocument == document)
        {
            ActiveDocument = OpenDocuments.Count > 0 ? OpenDocuments[0] : null;
        }
    }

    private void OnProjectLoaded(object? sender, MusicProject project)
    {
        CurrentProject = project;
        ProjectExplorer?.LoadProject(project);
        StatusMessage = $"Loaded: {project.Name}";

        // Open entry point script
        foreach (var script in project.Scripts)
        {
            if (script.IsEntryPoint)
            {
                OpenScript(script);
                break;
            }
        }
    }

    private void OnOutputReceived(object? sender, string message)
    {
        Output?.AppendLine(message);
    }

    private void OnExecutionStarted(object? sender, EventArgs e)
    {
        PlaybackStatus = "Running";
    }

    private void OnExecutionStopped(object? sender, EventArgs e)
    {
        PlaybackStatus = "Stopped";
    }
}
