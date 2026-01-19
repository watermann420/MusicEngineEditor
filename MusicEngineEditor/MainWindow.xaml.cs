using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using MusicEngineEditor.Editor;
using MusicEngineEditor.Models;
using MusicEngineEditor.Services;
using MusicEngineEditor.Views.Dialogs;

namespace MusicEngineEditor;

public partial class MainWindow : Window
{
    private readonly EngineService _engineService;
    private readonly IProjectService _projectService;
    private readonly DispatcherTimer _statusTimer;
    private MusicProject? _currentProject;
    private readonly Dictionary<string, TabItem> _openTabs = new();
    private readonly Dictionary<TabItem, MusicScript> _tabScripts = new();
    private bool _hasUnsavedChanges;

    public MainWindow()
    {
        InitializeComponent();

        // Get services from DI
        _engineService = App.Services.GetRequiredService<EngineService>();
        _projectService = App.Services.GetRequiredService<IProjectService>();

        // Load syntax highlighting
        EditorSetup.Configure(CodeEditor);

        // Start status update timer
        _statusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _statusTimer.Tick += StatusTimer_Tick;

        // Initialize engine async
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;

        // Track changes
        CodeEditor.TextChanged += (s, e) => _hasUnsavedChanges = true;
        CodeEditor.TextArea.Caret.PositionChanged += Caret_PositionChanged;

        // Set initial content
        CodeEditor.Text = GetDefaultScript();
        _hasUnsavedChanges = false;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Initializing engine...";
        OutputLine("MusicEngine Editor starting...");

        try
        {
            await _engineService.InitializeAsync();
            _statusTimer.Start();
            StatusText.Text = "Ready";
            OutputLine("Engine initialized successfully!");
            OutputLine("Create a new project or open an existing one to get started.");
            OutputLine("");
        }
        catch (Exception ex)
        {
            StatusText.Text = "Engine initialization failed";
            OutputLine($"ERROR: Failed to initialize engine: {ex.Message}");
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_hasUnsavedChanges)
        {
            var result = MessageBox.Show(
                "You have unsaved changes. Do you want to save before closing?",
                "Unsaved Changes",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
                return;
            }

            if (result == MessageBoxResult.Yes)
            {
                SaveAll_Click(this, new RoutedEventArgs());
            }
        }

        _statusTimer.Stop();
        _engineService.Dispose();
    }

    private void StatusTimer_Tick(object? sender, EventArgs e)
    {
        BeatDisplay.Text = _engineService.CurrentBeat.ToString("F2");
        PatternCountDisplay.Text = _engineService.PatternCount.ToString();
    }

    private void Caret_PositionChanged(object? sender, EventArgs e)
    {
        CaretPositionDisplay.Text = $"Ln {CodeEditor.TextArea.Caret.Line}, Col {CodeEditor.TextArea.Caret.Column}";
    }

    #region Project Management

    private async void NewProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new NewProjectDialog { Owner = this };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                StatusText.Text = "Creating project...";
                _currentProject = await _projectService.CreateProjectAsync(dialog.ProjectName, dialog.ProjectLocation);
                UpdateProjectExplorer();
                ProjectNameDisplay.Text = _currentProject.Name;
                StatusText.Text = $"Created project: {_currentProject.Name}";
                OutputLine($"Created new project: {_currentProject.Name}");

                // Open entry point script
                var entryScript = _currentProject.Scripts[0];
                OpenScriptInTab(entryScript);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create project: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void OpenProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "MusicEngine Projects (*.meproj)|*.meproj|All Files (*.*)|*.*",
            DefaultExt = ".meproj"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                StatusText.Text = "Loading project...";
                _currentProject = await _projectService.OpenProjectAsync(dialog.FileName);
                UpdateProjectExplorer();
                ProjectNameDisplay.Text = _currentProject.Name;
                StatusText.Text = $"Loaded: {_currentProject.Name}";
                OutputLine($"Loaded project: {_currentProject.Name}");

                // Open entry point script
                foreach (var script in _currentProject.Scripts)
                {
                    if (script.IsEntryPoint)
                    {
                        OpenScriptInTab(script);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open project: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void UpdateProjectExplorer()
    {
        ProjectTree.Items.Clear();

        if (_currentProject == null) return;

        // Project root
        var projectItem = new TreeViewItem
        {
            Header = _currentProject.Name,
            IsExpanded = true
        };

        // Scripts folder
        var scriptsFolder = new TreeViewItem
        {
            Header = "Scripts",
            IsExpanded = true
        };

        foreach (var script in _currentProject.Scripts)
        {
            var scriptItem = new TreeViewItem
            {
                Header = script.IsEntryPoint ? $"{script.FileName} (Entry)" : script.FileName,
                Tag = script
            };
            scriptsFolder.Items.Add(scriptItem);
        }

        projectItem.Items.Add(scriptsFolder);

        // Audio folder
        var audioFolder = new TreeViewItem
        {
            Header = "Audio",
            IsExpanded = true
        };

        foreach (var asset in _currentProject.AudioAssets)
        {
            var assetItem = new TreeViewItem
            {
                Header = $"{asset.Alias} ({asset.FileName})",
                Tag = asset
            };
            audioFolder.Items.Add(assetItem);
        }

        projectItem.Items.Add(audioFolder);

        // References folder (if any)
        if (_currentProject.References.Count > 0)
        {
            var refsFolder = new TreeViewItem
            {
                Header = "References",
                IsExpanded = true
            };

            foreach (var reference in _currentProject.References)
            {
                var refItem = new TreeViewItem
                {
                    Header = reference.Alias,
                    Tag = reference
                };
                refsFolder.Items.Add(refItem);
            }

            projectItem.Items.Add(refsFolder);
        }

        ProjectTree.Items.Add(projectItem);
    }

    private void ProjectTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ProjectTree.SelectedItem is TreeViewItem item && item.Tag is MusicScript script)
        {
            OpenScriptInTab(script);
        }
    }

    #endregion

    #region Tab Management

    private void OpenScriptInTab(MusicScript script)
    {
        // Check if already open
        if (_openTabs.TryGetValue(script.FilePath, out var existingTab))
        {
            EditorTabs.SelectedItem = existingTab;
            CodeEditor.Text = script.Content;
            return;
        }

        // Create new tab
        var tab = new TabItem
        {
            Header = script.FileName,
            Tag = script
        };

        _openTabs[script.FilePath] = tab;
        _tabScripts[tab] = script;
        EditorTabs.Items.Add(tab);
        EditorTabs.SelectedItem = tab;

        CodeEditor.Text = script.Content;
        FileNameDisplay.Text = script.FileName;
    }

    private void EditorTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EditorTabs.SelectedItem is TabItem tab && _tabScripts.TryGetValue(tab, out var script))
        {
            // Save current content to previous script
            SaveCurrentEditorContent();

            // Load selected script
            CodeEditor.Text = script.Content;
            FileNameDisplay.Text = script.FileName;
        }
    }

    private void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TabItem tab)
        {
            if (_tabScripts.TryGetValue(tab, out var script))
            {
                if (script.IsDirty)
                {
                    var result = MessageBox.Show(
                        $"Save changes to {script.FileName}?",
                        "Unsaved Changes",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Cancel) return;
                    if (result == MessageBoxResult.Yes)
                    {
                        script.Content = CodeEditor.Text;
                        _ = _projectService.SaveScriptAsync(script);
                    }
                }

                _openTabs.Remove(script.FilePath);
                _tabScripts.Remove(tab);
            }

            EditorTabs.Items.Remove(tab);

            if (EditorTabs.Items.Count == 0)
            {
                CodeEditor.Text = "";
                FileNameDisplay.Text = "";
            }
        }
    }

    private void SaveCurrentEditorContent()
    {
        if (EditorTabs.SelectedItem is TabItem tab && _tabScripts.TryGetValue(tab, out var script))
        {
            if (script.Content != CodeEditor.Text)
            {
                script.Content = CodeEditor.Text;
                script.IsDirty = true;
            }
        }
    }

    #endregion

    #region File Operations

    private void NewFile_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject == null)
        {
            MessageBox.Show("Please create or open a project first.", "No Project",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Simple input dialog for script name
        var name = InputDialog.Show("Enter script name:", "New Script", "NewScript", this);

        if (!string.IsNullOrWhiteSpace(name))
        {
            var script = _projectService.CreateScript(_currentProject, name);
            _currentProject.Scripts.Add(script);
            UpdateProjectExplorer();
            OpenScriptInTab(script);
        }
    }

    private async void SaveScript_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentEditorContent();

        if (EditorTabs.SelectedItem is TabItem tab && _tabScripts.TryGetValue(tab, out var script))
        {
            await _projectService.SaveScriptAsync(script);
            StatusText.Text = $"Saved: {script.FileName}";
        }
        else if (_currentProject == null)
        {
            // Legacy mode - save as single file
            var dialog = new SaveFileDialog
            {
                Filter = "C# Script Files (*.csx)|*.csx|MusicEngine Scripts (*.me)|*.me|All Files (*.*)|*.*",
                DefaultExt = ".csx"
            };

            if (dialog.ShowDialog() == true)
            {
                File.WriteAllText(dialog.FileName, CodeEditor.Text);
                StatusText.Text = $"Saved: {Path.GetFileName(dialog.FileName)}";
            }
        }

        _hasUnsavedChanges = false;
    }

    private async void SaveAll_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentEditorContent();

        foreach (var kvp in _tabScripts)
        {
            if (kvp.Value.IsDirty)
            {
                await _projectService.SaveScriptAsync(kvp.Value);
            }
        }

        if (_currentProject != null)
        {
            await _projectService.SaveProjectAsync(_currentProject);
        }

        _hasUnsavedChanges = false;
        StatusText.Text = "All files saved";
    }

    #endregion

    #region Script Execution

    private async void RunScript_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteScript();
    }

    private async Task ExecuteScript()
    {
        SaveCurrentEditorContent();
        var code = CodeEditor.Text;

        if (string.IsNullOrWhiteSpace(code))
        {
            OutputLine("No code to execute.");
            return;
        }

        StatusText.Text = "Executing...";
        RunButton.IsEnabled = false;

        OutputLine("----------------------------------------");
        OutputLine($"[{DateTime.Now:HH:mm:ss}] Executing script...");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await _engineService.ExecuteScriptAsync(code);
            stopwatch.Stop();

            if (result.Success)
            {
                StatusText.Text = $"Executed ({stopwatch.ElapsedMilliseconds}ms)";
                OutputLine($"Script executed successfully ({stopwatch.ElapsedMilliseconds}ms)");

                if (!string.IsNullOrEmpty(result.Output))
                {
                    OutputLine(result.Output);
                }
            }
            else
            {
                StatusText.Text = "Script error";
                OutputLine($"ERROR: {result.ErrorMessage}");

                foreach (var error in result.Errors)
                {
                    OutputLine($"  Line {error.Line}: {error.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            StatusText.Text = "Execution failed";
            OutputLine($"EXCEPTION: {ex.Message}");
        }

        RunButton.IsEnabled = true;
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        Panic_Click(sender, e);
    }

    private void Panic_Click(object sender, RoutedEventArgs e)
    {
        _engineService.AllNotesOff();
        StatusText.Text = "All notes stopped";
        OutputLine("All notes off (panic)");
    }

    private void BpmBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (double.TryParse(BpmBox.Text, out var bpm) && bpm > 0 && bpm < 999)
            {
                _engineService.SetBpm(bpm);
                StatusText.Text = $"BPM set to {bpm}";
            }
        }
    }

    #endregion

    #region Menu Handlers

    private void AddScript_Click(object sender, RoutedEventArgs e)
    {
        NewFile_Click(sender, e);
    }

    private void ImportAudio_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject == null)
        {
            MessageBox.Show("Please create or open a project first.", "No Project",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = "Audio Files (*.wav;*.mp3;*.ogg;*.flac)|*.wav;*.mp3;*.ogg;*.flac|All Files (*.*)|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var file in dialog.FileNames)
            {
                var alias = Path.GetFileNameWithoutExtension(file);
                _ = _projectService.ImportAudioAsync(_currentProject, file, alias);
                OutputLine($"Imported audio: {alias}");
            }

            UpdateProjectExplorer();
        }
    }

    private void AddReference_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Project references are not yet implemented.", "Coming Soon",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ProjectSettings_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Project settings dialog is not yet implemented.", "Coming Soon",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Find_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement find
    }

    private void Replace_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement replace
    }

    private void ClearOutput_Click(object sender, RoutedEventArgs e)
    {
        OutputBox.Clear();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Documentation_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/watermann420/MusicEngine",
            UseShellExecute = true
        });
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "MusicEngine Editor v1.0\n\n" +
            "A professional IDE for MusicEngine live coding.\n\n" +
            "Shortcuts:\n" +
            "  F5 - Run script\n" +
            "  Shift+F5 - Stop\n" +
            "  Ctrl+S - Save\n" +
            "  Ctrl+Shift+S - Save All\n" +
            "  Ctrl+Shift+N - New Project\n" +
            "  Ctrl+Shift+O - Open Project\n" +
            "  Esc - All notes off (panic)",
            "About MusicEngine Editor",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    #endregion

    #region Helpers

    private void OutputLine(string text)
    {
        Dispatcher.Invoke(() =>
        {
            OutputBox.AppendText(text + Environment.NewLine);
            OutputBox.ScrollToEnd();
        });
    }

    private static string GetDefaultScript()
    {
        return """
            // MusicEngine Script
            // Press F5 to execute or create a new project to get started

            // Set BPM
            SetBpm(120);
            Start();

            // Create a simple synth
            var synth = CreateSynth();
            synth.Waveform = WaveType.Sawtooth;
            synth.SetParameter("cutoff", 0.6f);

            // Route MIDI input (device 0) to the synth
            midi.device(0).route(synth);

            // Map full keyboard range
            midi.playablekeys.range(21, 108).low.to.high.map(synth);

            // Map modulation wheel to filter cutoff
            midi.device(0).cc(1).to(synth, "cutoff");

            Print("Synth ready! Play your MIDI keyboard.");

            // Or load a VST plugin:
            // var vital = vst.load("Vital");
            // vital?.from(0);
            """;
    }

    #endregion
}
