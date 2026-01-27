// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Main application window.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using System.Xml;
using ICSharpCode.AvalonEdit.Document;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using MusicEngineEditor.Editor;
using MusicEngineEditor.Models;
using MusicEngineEditor.Services;
using MusicEngineEditor.Controls;
using MusicEngineEditor.ViewModels;
using MusicEngineEditor.Views;
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
    private bool _outputVisible = true;
    private bool _isRunning = false;
    private bool _showingOutput = true;
    private CompletionProvider? _completionProvider;
    private InlineSliderService? _inlineSliderService;
    private VisualizationIntegration? _visualization;

    // VST Plugin Windows
    private readonly Dictionary<string, VstPluginWindow> _vstWindows = new();

    // Transport ViewModel
    private TransportViewModel? _transportViewModel;

    // Performance Monitoring
    private readonly PerformanceMonitorService _performanceMonitorService;
    private readonly PerformanceViewModel _performanceViewModel;

    // Problems/Errors
    public ObservableCollection<ProblemItem> Problems { get; } = new();

    // Active Instruments Display
    public ObservableCollection<ActiveInstrumentInfo> ActiveInstruments { get; } = new();
    private readonly DispatcherTimer _animationTimer;

    // Data for right panel lists
    public ObservableCollection<MidiDeviceInfo> MidiDevices { get; } = new();
    public ObservableCollection<AudioFileInfo> AudioFiles { get; } = new();

    // Track Management
    public ObservableCollection<TrackInfo> Tracks { get; } = new();
    private readonly Dictionary<int, FreezeTrackData> _frozenTrackData = new();

    public MainWindow()
    {
        InitializeComponent();

        // Get services from DI
        _engineService = App.Services.GetRequiredService<EngineService>();
        _projectService = App.Services.GetRequiredService<IProjectService>();

        // Load syntax highlighting and configure editor
        EditorSetup.Configure(CodeEditor);

        // Setup autocomplete using the new CompletionProvider
        // Triggers on Ctrl+Space and automatically on dot (.)
        _completionProvider = EditorSetup.SetupCompletion(CodeEditor);

        // Handle Ctrl+Enter for run and other keyboard shortcuts
        CodeEditor.PreviewKeyDown += CodeEditor_PreviewKeyDown;

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

        // Bind data to lists
        MidiDevicesList.ItemsSource = MidiDevices;
        AudioFilesList.ItemsSource = AudioFiles;
        ProblemsListView.ItemsSource = Problems;

        // Wire up VstPluginPanel events
        VstPluginsPanel.OnOpenPluginEditor += VstPluginsPanel_OnOpenPluginEditor;
        VstPluginsPanel.OnPluginDoubleClick += VstPluginsPanel_OnPluginDoubleClick;
        VstPluginsPanel.OnScanCompleted += VstPluginsPanel_OnScanCompleted;

        // Attach Find/Replace control to editor
        FindReplaceBar.AttachToEditor(CodeEditor);

        // Setup hover tooltips for code
        var tooltipService = new Editor.CodeTooltipService(CodeEditor);

        // Setup inline sliders for numeric literals (like Strudel.cc)
        // Hover over a number to see a slider popup
        _inlineSliderService = EditorSetup.SetupInlineSliders(CodeEditor);
        _inlineSliderService.ValueChanged += InlineSlider_ValueChanged;
        _inlineSliderService.ValueChangeCompleted += InlineSlider_ValueChangeCompleted;

        // Setup visualization integration for real-time playback highlighting
        _visualization = this.CreateVisualizationIntegration(CodeEditor);
        _visualization.VisualizationError += (s, msg) => OutputLine($"[Visualization] {msg}");

        // Setup context menu for code editor
        SetupEditorContextMenu();

        // Animation timer for pulsing active instruments
        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _animationTimer.Tick += AnimationTimer_Tick;

        // Set initial content
        CodeEditor.Text = GetDefaultScript();
        _hasUnsavedChanges = false;

        // Wire up WorkshopPanel events
        WorkshopPanel.OnRunCode += WorkshopPanel_OnRunCode;
        WorkshopPanel.OnCopyCode += WorkshopPanel_OnCopyCode;
        WorkshopPanel.OnInsertCode += WorkshopPanel_OnInsertCode;

        // Initialize Performance Monitoring
        _performanceMonitorService = new PerformanceMonitorService();
        _performanceViewModel = new PerformanceViewModel(_performanceMonitorService);
        PerformanceMeterControl.ConnectToViewModel(_performanceViewModel);
    }

    private void CodeEditor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Handle Ctrl+Enter to run script
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            _ = ExecuteScript();
        }
        // Handle Escape to stop or close find/replace
        else if (e.Key == Key.Escape)
        {
            if (FindReplaceBar.Visibility == Visibility.Visible)
            {
                FindReplaceBar.Hide();
            }
            else
            {
                _engineService.AllNotesOff();
                _isRunning = false;
                _visualization?.OnPlaybackStopped();
                StatusText.Text = "Stopped";
                OutputLine("Stopped (Escape pressed)");
            }
            e.Handled = true;
        }
        // Handle Ctrl+F to find
        else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            FindReplaceBar.ShowFind();
        }
        // Handle Ctrl+H to find and replace
        else if (e.Key == Key.H && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            FindReplaceBar.ShowReplace();
        }
        // Handle F3 for find next
        else if (e.Key == Key.F3 && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            if (FindReplaceBar.Visibility == Visibility.Visible)
            {
                // Find next is handled inside the control
            }
        }
        // Handle Ctrl+P for command palette
        else if (e.Key == Key.P && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            ShowCommandPalette();
        }
    }

    #region Context Menu

    private void SetupEditorContextMenu()
    {
        var contextMenu = new System.Windows.Controls.ContextMenu
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2B, 0x2D, 0x30)),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xBC, 0xBE, 0xC4)),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3C, 0x3F, 0x41)),
        };

        // Standard edit commands
        var cutItem = new System.Windows.Controls.MenuItem { Header = "Cut", InputGestureText = "Ctrl+X" };
        cutItem.Click += (s, e) => CodeEditor.Cut();
        contextMenu.Items.Add(cutItem);

        var copyItem = new System.Windows.Controls.MenuItem { Header = "Copy", InputGestureText = "Ctrl+C" };
        copyItem.Click += (s, e) => CodeEditor.Copy();
        contextMenu.Items.Add(copyItem);

        var pasteItem = new System.Windows.Controls.MenuItem { Header = "Paste", InputGestureText = "Ctrl+V" };
        pasteItem.Click += (s, e) => CodeEditor.Paste();
        contextMenu.Items.Add(pasteItem);

        contextMenu.Items.Add(new Separator());

        // Find/Replace
        var findItem = new System.Windows.Controls.MenuItem { Header = "Find...", InputGestureText = "Ctrl+F" };
        findItem.Click += (s, e) => FindReplaceBar.ShowFind();
        contextMenu.Items.Add(findItem);

        var replaceItem = new System.Windows.Controls.MenuItem { Header = "Replace...", InputGestureText = "Ctrl+H" };
        replaceItem.Click += (s, e) => FindReplaceBar.ShowReplace();
        contextMenu.Items.Add(replaceItem);

        contextMenu.Items.Add(new Separator());

        // VST specific option (dynamically enabled)
        var openVstItem = new System.Windows.Controls.MenuItem { Header = "Open VST Editor", IsEnabled = false };
        openVstItem.Click += ContextMenu_OpenVstEditor;
        contextMenu.Items.Add(openVstItem);

        // Run selection
        var runItem = new System.Windows.Controls.MenuItem { Header = "Run Script", InputGestureText = "Ctrl+Enter" };
        runItem.Click += (s, e) => _ = ExecuteScript();
        contextMenu.Items.Add(runItem);

        contextMenu.Opened += (s, e) =>
        {
            // Check if we're on a VST variable
            var vstName = GetVstNameAtCursor();
            openVstItem.IsEnabled = vstName != null;
            openVstItem.Tag = vstName;
        };

        CodeEditor.ContextMenu = contextMenu;

        // Double-click handler for VST names
        CodeEditor.TextArea.TextView.MouseLeftButtonDown += TextView_MouseLeftButtonDown;
    }

    private string? GetVstNameAtCursor()
    {
        var position = CodeEditor.TextArea.Caret.Position;
        var line = CodeEditor.Document.GetLineByNumber(position.Line);
        var lineText = CodeEditor.Document.GetText(line.Offset, line.Length);

        // Find word at cursor
        var column = position.Column - 1;
        if (column < 0 || column >= lineText.Length) return null;

        int start = column;
        int end = column;

        while (start > 0 && (char.IsLetterOrDigit(lineText[start - 1]) || lineText[start - 1] == '_'))
            start--;

        while (end < lineText.Length && (char.IsLetterOrDigit(lineText[end]) || lineText[end] == '_'))
            end++;

        if (start >= end) return null;

        var word = lineText.Substring(start, end - start);

        // Check if this word is a VST variable by looking for vst.load patterns
        var vstPattern = new System.Text.RegularExpressions.Regex($@"var\s+{word}\s*=\s*vst\.load\s*\([""']([^""']+)[""']\)");
        var match = vstPattern.Match(CodeEditor.Text);
        if (match.Success)
        {
            return word;
        }

        // Also check if the word itself matches a known VST plugin
        foreach (var plugin in VstPluginsPanel.Plugins)
        {
            if (plugin.Name.Equals(word, StringComparison.OrdinalIgnoreCase))
            {
                return word;
            }
        }

        return null;
    }

    private void ContextMenu_OpenVstEditor(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem item && item.Tag is string vstName)
        {
            OpenVstWindowByName(vstName);
        }
    }

    private void OpenVstWindowByName(string name)
    {
        // Try to find or create VST window
        if (_vstWindows.TryGetValue(name, out var existingWindow))
        {
            existingWindow.Show();
            existingWindow.WindowState = System.Windows.WindowState.Normal;
            existingWindow.Activate();
        }
        else
        {
            // Find the VST plugin in the panel's list
            var plugin = VstPluginsPanel.Plugins.FirstOrDefault(p =>
                p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (plugin != null)
            {
                var window = new VstPluginWindow(plugin.Name, plugin.FullPath);
                _vstWindows[name] = window;
                window.Show();
                OutputLine($"Opened VST window: {name}");
            }
            else
            {
                // Plugin not found in panel, open with just the name
                OpenVstPluginWindow(name, name);
            }
        }
    }

    private DateTime _lastClickTime = DateTime.MinValue;
    private int _lastClickOffset = -1;

    private void TextView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Check for double-click
        var now = DateTime.Now;
        var position = CodeEditor.GetPositionFromPoint(e.GetPosition(CodeEditor));

        if (position == null) return;

        var offset = CodeEditor.Document.GetOffset(position.Value.Location);

        if ((now - _lastClickTime).TotalMilliseconds < 300 && Math.Abs(offset - _lastClickOffset) < 5)
        {
            // Double-click detected - check if on VST name
            var vstName = GetVstNameAtCursor();
            if (vstName != null)
            {
                OpenVstWindowByName(vstName);
                e.Handled = true;
            }
        }

        _lastClickTime = now;
        _lastClickOffset = offset;
    }

    #endregion

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Initializing engine...";
        OutputLine("MusicEngine Editor starting...");
        OutputLine("");

        try
        {
            await _engineService.InitializeAsync();
            _statusTimer.Start();

            // Show device enumeration output from engine initialization
            if (!string.IsNullOrEmpty(_engineService.InitializationOutput))
            {
                OutputLine("=== Audio/MIDI Devices ===");
                OutputLine(_engineService.InitializationOutput);
                OutputLine("==========================");
                OutputLine("");
            }

            // Populate MIDI devices list
            RefreshMidiDevices();

            // Connect visualization to the sequencer
            if (_engineService.Sequencer != null)
            {
                _visualization?.ConnectToSequencer(_engineService.Sequencer);
            }

            // Initialize Transport ViewModel
            _transportViewModel = new TransportViewModel();

            // Start Performance Monitoring
            _performanceMonitorService.Start();

            StatusText.Text = "Ready";
            OutputLine("Engine initialized successfully!");
            OutputLine("Press Ctrl+Enter to run the script, Escape to stop.");
            OutputLine("");
        }
        catch (Exception ex)
        {
            StatusText.Text = "Engine initialization failed";
            OutputLine($"ERROR: Failed to initialize engine: {ex.Message}");
            OutputLine("Check if audio devices are available and not in use by another application.");
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
        _sliderHotReloadTimer?.Stop();
        _inlineSliderService?.Dispose();
        _visualization?.Dispose();
        _transportViewModel?.Dispose();
        _performanceMonitorService.Dispose();
        CloseAllVstWindows();
        _engineService.Dispose();

        // Mark session as cleanly closed (no crash recovery needed)
        try
        {
            RecoveryService.Instance.MarkSessionClosed();
            AutoSaveService.Instance.Dispose();
        }
        catch
        {
            // Ignore errors during shutdown
        }
    }

    private void StatusTimer_Tick(object? sender, EventArgs e)
    {
        // Update Pattern count from engine
        PatternCountDisplay.Text = _engineService.PatternCount.ToString();

        // Sync BPM display with engine (in case script changed it)
        var engineBpm = _engineService.Bpm;
        if (Math.Abs(engineBpm - double.Parse(BpmBox.Text)) > 0.1)
        {
            BpmBox.Text = engineBpm.ToString("F0");
        }

        // Update status indicator based on running state
        if (_isRunning)
        {
            StatusIndicator.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6A, 0xAB, 0x73)); // Green
        }
        else
        {
            StatusIndicator.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6F, 0x73, 0x7A)); // Gray
        }
    }

    private void Caret_PositionChanged(object? sender, EventArgs e)
    {
        CaretPositionDisplay.Text = $"Ln {CodeEditor.TextArea.Caret.Line}, Col {CodeEditor.TextArea.Caret.Column}";
    }

    #region Inline Slider Events

    private void InlineSlider_ValueChanged(object? sender, SliderValueChangedEventArgs e)
    {
        // Mark as having unsaved changes
        _hasUnsavedChanges = true;

        // If script is running, trigger hot-reload
        if (_isRunning)
        {
            // Debounce hot-reload to avoid too many re-evaluations
            _sliderHotReloadTimer?.Stop();
            _sliderHotReloadTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _sliderHotReloadTimer.Tick += async (s, args) =>
            {
                _sliderHotReloadTimer.Stop();
                await TriggerHotReload();
            };
            _sliderHotReloadTimer.Start();
        }
    }

    private void InlineSlider_ValueChangeCompleted(object? sender, SliderValueChangedEventArgs e)
    {
        // Final update when slider is released
        _hasUnsavedChanges = true;

        // If script is running, do a final hot-reload
        if (_isRunning)
        {
            _sliderHotReloadTimer?.Stop();
            _ = TriggerHotReload();
        }

        // Show feedback in status
        var context = e.Number.Context ?? e.Number.SliderConfig?.Label ?? "value";
        StatusText.Text = $"Changed {context}: {e.OldValue:F2} -> {e.NewValue:F2}";
    }

    private DispatcherTimer? _sliderHotReloadTimer;

    private async Task TriggerHotReload()
    {
        try
        {
            var code = CodeEditor.Text;
            var result = await _engineService.ExecuteScriptAsync(code);

            if (!result.Success)
            {
                // Don't interrupt with errors during slider manipulation
                // Just log to output
                OutputLine($"[Hot-reload] Error: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            OutputLine($"[Hot-reload] Exception: {ex.Message}");
        }
    }

    #endregion

    #region Project Management

    private async void NewProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new NewProjectDialog { Owner = this };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                // Validate dialog inputs before proceeding
                if (string.IsNullOrWhiteSpace(dialog.ProjectName))
                {
                    MessageBox.Show("Project name cannot be empty.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(dialog.ProjectLocation))
                {
                    MessageBox.Show("Project location cannot be empty.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                StatusText.Text = "Creating project...";
                _currentProject = await _projectService.CreateProjectAsync(dialog.ProjectName, dialog.ProjectLocation);

                // Verify project was created successfully
                if (_currentProject == null)
                {
                    StatusText.Text = "Failed to create project";
                    OutputLine("ERROR: Project creation returned null");
                    MessageBox.Show("Failed to create project: Project creation returned null.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                UpdateProjectExplorer();
                UpdateAudioFilesList();
                ProjectNameDisplay.Text = _currentProject.Name;
                StatusText.Text = $"Created project: {_currentProject.Name}";
                OutputLine($"Created new project: {_currentProject.Name}");

                // Mark session as active for crash recovery
                RecoveryService.Instance.MarkSessionActive(_currentProject);

                // Initialize auto-save for this project
                AutoSaveService.Instance.Initialize(_projectService);

                // Open entry point script (with null checks)
                if (_currentProject.Scripts != null && _currentProject.Scripts.Count > 0)
                {
                    var entryScript = _currentProject.Scripts[0];
                    if (entryScript != null)
                    {
                        OpenScriptInTab(entryScript);
                    }
                    else
                    {
                        OutputLine("Warning: Entry script is null.");
                    }
                }
                else
                {
                    OutputLine("Warning: No scripts were created with the project.");
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "Project creation failed";
                OutputLine($"ERROR: Failed to create project: {ex.Message}");
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

                // Verify project was loaded successfully
                if (_currentProject == null)
                {
                    StatusText.Text = "Failed to load project";
                    OutputLine("ERROR: Project loading returned null");
                    MessageBox.Show("Failed to load project: Project file could not be parsed.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                UpdateProjectExplorer();
                UpdateAudioFilesList();
                ProjectNameDisplay.Text = _currentProject.Name;
                StatusText.Text = $"Loaded: {_currentProject.Name}";
                OutputLine($"Loaded project: {_currentProject.Name}");

                // Mark session as active for crash recovery
                RecoveryService.Instance.MarkSessionActive(_currentProject);

                // Initialize auto-save for this project
                AutoSaveService.Instance.Initialize(_projectService);

                // Open entry point script (with null checks)
                if (_currentProject.Scripts != null && _currentProject.Scripts.Count > 0)
                {
                    foreach (var script in _currentProject.Scripts)
                    {
                        if (script != null && script.IsEntryPoint)
                        {
                            OpenScriptInTab(script);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "Project loading failed";
                OutputLine($"ERROR: Failed to open project: {ex.Message}");
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
        // Null check for script parameter
        if (script == null)
        {
            OutputLine("Warning: Attempted to open a null script.");
            return;
        }

        // Validate script has required properties
        if (string.IsNullOrEmpty(script.FilePath))
        {
            OutputLine("Warning: Script has no file path.");
            return;
        }

        // Check if already open
        if (_openTabs.TryGetValue(script.FilePath, out var existingTab))
        {
            // Save current tab's content before switching
            SaveCurrentEditorContent();
            EditorTabs.SelectedItem = existingTab;
            // Note: Content will be loaded by SelectionChanged event
            return;
        }

        // Save current tab's content before opening new tab
        SaveCurrentEditorContent();

        // Create new tab
        var tab = new TabItem
        {
            Header = script.FileName ?? "Untitled",
            Tag = script
        };

        _openTabs[script.FilePath] = tab;
        _tabScripts[tab] = script;
        EditorTabs.Items.Add(tab);
        EditorTabs.SelectedItem = tab;

        CodeEditor.Text = script.Content ?? string.Empty;
        FileNameDisplay.Text = script.FileName ?? "Untitled";
    }

    private void EditorTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // First, save content of the PREVIOUS tab (the one being switched away from)
        if (e.RemovedItems.Count > 0 && e.RemovedItems[0] is TabItem previousTab)
        {
            if (_tabScripts.TryGetValue(previousTab, out var previousScript))
            {
                if (previousScript.Content != CodeEditor.Text)
                {
                    previousScript.Content = CodeEditor.Text;
                    previousScript.IsDirty = true;
                }
            }
        }

        // Then load the NEW tab's content
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is TabItem newTab)
        {
            if (_tabScripts.TryGetValue(newTab, out var script))
            {
                CodeEditor.Text = script.Content ?? string.Empty;
                FileNameDisplay.Text = script.FileName ?? "Untitled";
            }
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

    private async void RunStopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            StopExecution();
        }
        else
        {
            await ExecuteScript();
        }
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

        _isRunning = true;
        StatusText.Text = "Executing...";
        UpdateRunStopButton();

        // Clear previous problems
        Problems.Clear();
        UpdateErrorBadge();

        OutputLine("----------------------------------------");
        OutputLine($"[{DateTime.Now:HH:mm:ss}] Executing script...");

        // Notify visualization system before execution
        _visualization?.OnBeforeExecute(code);

        var stopwatch = Stopwatch.StartNew();
        var currentFileName = GetCurrentFileName();

        try
        {
            var result = await _engineService.ExecuteScriptAsync(code);
            stopwatch.Stop();

            if (result.Success)
            {
                StatusText.Text = $"Running ({stopwatch.ElapsedMilliseconds}ms)";
                OutputLine($"Script executed successfully ({stopwatch.ElapsedMilliseconds}ms)");

                if (!string.IsNullOrEmpty(result.Output))
                {
                    OutputLine(result.Output);
                }

                // Notify visualization system after successful execution
                _visualization?.OnAfterExecute(true);
                _visualization?.OnPlaybackStarted();

                // Parse code to extract instruments and start animation
                ExtractInstrumentsFromCode(code);
                _animationTimer.Start();
            }
            else
            {
                _isRunning = false;
                UpdateRunStopButton();
                StatusText.Text = "Script error";
                OutputLine($"ERROR: {result.ErrorMessage}");

                foreach (var error in result.Errors)
                {
                    OutputLine($"  Line {error.Line}: {error.Message}");

                    // Add to Problems panel
                    Problems.Add(new ProblemItem
                    {
                        Severity = error.Severity == "Error" ? ProblemSeverity.Error : ProblemSeverity.Warning,
                        Message = error.Message,
                        FileName = currentFileName,
                        FilePath = GetCurrentFilePath(),
                        Line = error.Line,
                        Column = error.Column
                    });
                }

                UpdateErrorBadge();

                // Switch to Problems tab if there are errors
                if (Problems.Count > 0)
                {
                    SwitchOutputTab(false);
                }
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _isRunning = false;
            StatusText.Text = "Execution failed";
            OutputLine($"EXCEPTION: {ex.Message}");

            // Add exception to Problems
            Problems.Add(new ProblemItem
            {
                Severity = ProblemSeverity.Error,
                Message = ex.Message,
                FileName = currentFileName,
                FilePath = GetCurrentFilePath(),
                Line = 1,
                Column = 1
            });
            UpdateErrorBadge();
        }

        UpdateRunStopButton();
    }

    private string GetCurrentFileName()
    {
        if (EditorTabs.SelectedItem is TabItem tab && _tabScripts.TryGetValue(tab, out var script))
        {
            return script.FileName;
        }
        return "Script";
    }

    private string GetCurrentFilePath()
    {
        if (EditorTabs.SelectedItem is TabItem tab && _tabScripts.TryGetValue(tab, out var script))
        {
            return script.FilePath;
        }
        return "";
    }

    private void UpdateErrorBadge()
    {
        var errorCount = Problems.Count(p => p.Severity == ProblemSeverity.Error);
        if (errorCount > 0)
        {
            ErrorCountBadge.Visibility = Visibility.Visible;
            ErrorCountText.Text = errorCount.ToString();
        }
        else
        {
            ErrorCountBadge.Visibility = Visibility.Collapsed;
        }
    }

    private void ExtractInstrumentsFromCode(string code)
    {
        ClearActiveInstruments();

        // Find synth declarations: var name = CreateSynth();
        var synthPattern = new System.Text.RegularExpressions.Regex(@"var\s+(\w+)\s*=\s*CreateSynth\s*\(");
        foreach (System.Text.RegularExpressions.Match match in synthPattern.Matches(code))
        {
            AddActiveInstrument(match.Groups[1].Value, "synth");
        }

        // Find VST declarations: var name = vst.load("...")
        var vstPattern = new System.Text.RegularExpressions.Regex(@"var\s+(\w+)\s*=\s*vst\.load\s*\(");
        foreach (System.Text.RegularExpressions.Match match in vstPattern.Matches(code))
        {
            AddActiveInstrument(match.Groups[1].Value, "vst");
        }

        // Find pattern declarations: var name = CreatePattern(...)
        var patternPattern = new System.Text.RegularExpressions.Regex(@"var\s+(\w+)\s*=\s*CreatePattern\s*\(");
        foreach (System.Text.RegularExpressions.Match match in patternPattern.Matches(code))
        {
            AddActiveInstrument(match.Groups[1].Value, "pattern");
        }
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        StopExecution();
    }

    private void StopExecution()
    {
        _engineService.AllNotesOff();
        _isRunning = false;
        _animationTimer.Stop();

        // Notify visualization system that playback stopped
        _visualization?.OnPlaybackStopped();

        // Update button to show Run state
        UpdateRunStopButton();

        ClearActiveInstruments();
        StatusText.Text = "Stopped";
        OutputLine("Stopped");
    }

    private void UpdateRunStopButton()
    {
        if (_isRunning)
        {
            // Show Stop state (red)
            RunStopButton.Background = new SolidColorBrush(Color.FromRgb(0x8B, 0x2D, 0x2D));
            RunStopIcon.Text = "\u25A0"; // Square (stop icon)
            RunStopText.Text = "Stop";

            // Update glow color for hover effect
            if (RunStopButton.Template.FindName("glowEffect", RunStopButton) is DropShadowEffect glow)
            {
                glow.Color = Color.FromRgb(0xD3, 0x2F, 0x2F);
            }
        }
        else
        {
            // Show Run state (green)
            RunStopButton.Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x5A, 0x2D));
            RunStopIcon.Text = "\u25B6"; // Triangle (play icon)
            RunStopText.Text = "Run";

            // Update glow color for hover effect
            if (RunStopButton.Template.FindName("glowEffect", RunStopButton) is DropShadowEffect glow)
            {
                glow.Color = Color.FromRgb(0x4C, 0xAF, 0x50);
            }
        }
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

    #region Panel Toggle Methods

    private void ToggleProjectExplorer_Click(object sender, RoutedEventArgs e)
    {
        ProjectExplorerPanel.Visibility = ProjectExplorerMenuItem.IsChecked
            ? Visibility.Visible
            : Visibility.Collapsed;
        LeftPanelColumn.Width = ProjectExplorerMenuItem.IsChecked
            ? new GridLength(240)
            : new GridLength(0);

        // Hide workshop panel when showing project explorer
        if (ProjectExplorerMenuItem.IsChecked)
        {
            WorkshopPanel.Visibility = Visibility.Collapsed;
            WorkshopMenuItem.IsChecked = false;
        }
    }

    private void ToggleWorkshop_Click(object sender, RoutedEventArgs e)
    {
        var showWorkshop = !WorkshopMenuItem.IsChecked;
        WorkshopMenuItem.IsChecked = showWorkshop;

        if (showWorkshop)
        {
            // Show workshop panel, hide project explorer
            WorkshopPanel.Visibility = Visibility.Visible;
            ProjectExplorerPanel.Visibility = Visibility.Collapsed;
            ProjectExplorerMenuItem.IsChecked = false;
            LeftPanelColumn.Width = new GridLength(500);
            LeftPanelColumn.MinWidth = 400;
        }
        else
        {
            // Hide workshop panel, restore project explorer
            WorkshopPanel.Visibility = Visibility.Collapsed;
            ProjectExplorerPanel.Visibility = Visibility.Visible;
            ProjectExplorerMenuItem.IsChecked = true;
            LeftPanelColumn.Width = new GridLength(240);
            LeftPanelColumn.MinWidth = 180;
        }
    }

    private void ToggleOutput_Click(object sender, RoutedEventArgs e)
    {
        _outputVisible = !_outputVisible;
        OutputMenuItem.IsChecked = _outputVisible;
        OutputPanel.Visibility = _outputVisible ? Visibility.Visible : Visibility.Collapsed;
        OutputSplitter.Visibility = _outputVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ToggleMidiPanel_Click(object sender, RoutedEventArgs e)
    {
        ShowRightPanel("midi");
        MidiPanelMenuItem.IsChecked = RightPanel.Visibility == Visibility.Visible;
    }

    private void ToggleVstPanel_Click(object sender, RoutedEventArgs e)
    {
        ShowRightPanel("vst");
        VstPanelMenuItem.IsChecked = RightPanel.Visibility == Visibility.Visible;
    }

    private void ToggleAudioPanel_Click(object sender, RoutedEventArgs e)
    {
        ShowRightPanel("audio");
        AudioPanelMenuItem.IsChecked = RightPanel.Visibility == Visibility.Visible;
    }

    private string? _currentRightPanelTab = null;

    private void ShowRightPanel(string tab)
    {
        // If panel is visible and same tab is requested, toggle it off
        if (RightPanel.Visibility == Visibility.Visible && _currentRightPanelTab == tab)
        {
            HideRightPanel();
            return;
        }

        // If panel is hidden, show it
        if (RightPanel.Visibility == Visibility.Collapsed)
        {
            RightPanel.Visibility = Visibility.Visible;
            RightSplitter.Visibility = Visibility.Visible;
            RightPanelColumn.Width = new GridLength(280);
            RightPanelColumn.MinWidth = 200;
        }

        // Switch to the requested tab
        _currentRightPanelTab = tab;
        SwitchRightPanelTab(tab);
    }

    private void HideRightPanel()
    {
        RightPanel.Visibility = Visibility.Collapsed;
        RightSplitter.Visibility = Visibility.Collapsed;
        RightPanelColumn.Width = new GridLength(0);
        RightPanelColumn.MinWidth = 0;
        _currentRightPanelTab = null;

        // Update menu checkboxes
        MidiPanelMenuItem.IsChecked = false;
        VstPanelMenuItem.IsChecked = false;
        AudioPanelMenuItem.IsChecked = false;
        UndoHistoryMenuItem.IsChecked = false;
    }

    private void SwitchRightPanelTab(string tab)
    {
        // Reset all tab headers
        MidiTabHeader.Background = System.Windows.Media.Brushes.Transparent;
        VstTabHeader.Background = System.Windows.Media.Brushes.Transparent;
        AudioTabHeader.Background = System.Windows.Media.Brushes.Transparent;
        ((TextBlock)MidiTabHeader.Child).Foreground = (System.Windows.Media.Brush)FindResource("SecondaryForegroundBrush");
        ((TextBlock)VstTabHeader.Child).Foreground = (System.Windows.Media.Brush)FindResource("SecondaryForegroundBrush");
        ((TextBlock)AudioTabHeader.Child).Foreground = (System.Windows.Media.Brush)FindResource("SecondaryForegroundBrush");

        // Hide all panels
        MidiDevicesPanel.Visibility = Visibility.Collapsed;
        VstPluginsPanel.Visibility = Visibility.Collapsed;
        AudioFilesPanel.Visibility = Visibility.Collapsed;
        TrackPropertiesPanel.Visibility = Visibility.Collapsed;
        UndoHistoryPanel.Visibility = Visibility.Collapsed;

        // Show selected tab
        switch (tab)
        {
            case "midi":
                MidiTabHeader.Background = (System.Windows.Media.Brush)FindResource("AccentBrush");
                ((TextBlock)MidiTabHeader.Child).Foreground = System.Windows.Media.Brushes.White;
                ((TextBlock)MidiTabHeader.Child).FontWeight = FontWeights.SemiBold;
                MidiDevicesPanel.Visibility = Visibility.Visible;
                break;
            case "vst":
                VstTabHeader.Background = (System.Windows.Media.Brush)FindResource("AccentBrush");
                ((TextBlock)VstTabHeader.Child).Foreground = System.Windows.Media.Brushes.White;
                ((TextBlock)VstTabHeader.Child).FontWeight = FontWeights.SemiBold;
                VstPluginsPanel.Visibility = Visibility.Visible;
                break;
            case "audio":
                AudioTabHeader.Background = (System.Windows.Media.Brush)FindResource("AccentBrush");
                ((TextBlock)AudioTabHeader.Child).Foreground = System.Windows.Media.Brushes.White;
                ((TextBlock)AudioTabHeader.Child).FontWeight = FontWeights.SemiBold;
                AudioFilesPanel.Visibility = Visibility.Visible;
                break;
            case "trackproperties":
                // Track properties panel is standalone (no tab header in the tabbed area)
                TrackPropertiesPanel.Visibility = Visibility.Visible;
                break;
            case "undohistory":
                // Undo history panel is standalone (no tab header in the tabbed area)
                UndoHistoryPanel.Visibility = Visibility.Visible;
                break;
        }
    }

    private void CloseRightPanel_Click(object sender, RoutedEventArgs e)
    {
        RightPanel.Visibility = Visibility.Collapsed;
        RightSplitter.Visibility = Visibility.Collapsed;
        RightPanelColumn.Width = new GridLength(0);
        RightPanelColumn.MinWidth = 0;

        MidiPanelMenuItem.IsChecked = false;
        VstPanelMenuItem.IsChecked = false;
        AudioPanelMenuItem.IsChecked = false;
    }

    private void MidiTab_Click(object sender, MouseButtonEventArgs e)
    {
        SwitchRightPanelTab("midi");
    }

    private void VstTab_Click(object sender, MouseButtonEventArgs e)
    {
        SwitchRightPanelTab("vst");
    }

    private void AudioTab_Click(object sender, MouseButtonEventArgs e)
    {
        SwitchRightPanelTab("audio");
    }

    private void ToggleTrackPropertiesPanel_Click(object sender, RoutedEventArgs e)
    {
        ShowRightPanel("trackproperties");
    }

    private void ToggleUndoHistory_Click(object sender, RoutedEventArgs e)
    {
        ShowRightPanel("undohistory");
        UndoHistoryMenuItem.IsChecked = RightPanel.Visibility == Visibility.Visible && _currentRightPanelTab == "undohistory";
    }

    private void TrackPropertiesPanel_CloseRequested(object? sender, EventArgs e)
    {
        HideRightPanel();
    }

    private void TrackPropertiesPanel_TrackPropertyChanged(object? sender, TrackPropertyChangedEventArgs e)
    {
        // Handle track property changes - update any connected views
        OutputLine($"Track '{e.Track.Name}' property '{e.PropertyName}' changed: {e.OldValue} -> {e.NewValue}");
    }

    private void TrackPropertiesPanel_TrackDuplicateRequested(object? sender, TrackEventArgs e)
    {
        // Create a duplicate of the track
        var duplicate = e.Track.Duplicate();

        // Find the index of the original track and insert the duplicate after it
        int originalIndex = -1;
        for (int i = 0; i < Tracks.Count; i++)
        {
            if (Tracks[i].Id == e.Track.Id)
            {
                originalIndex = i;
                break;
            }
        }

        // Add the duplicate track to the track list
        if (originalIndex >= 0 && originalIndex < Tracks.Count - 1)
        {
            Tracks.Insert(originalIndex + 1, duplicate);
        }
        else
        {
            Tracks.Add(duplicate);
        }

        // Update the duplicate's order property
        duplicate.Order = originalIndex + 1;

        // Update order for all subsequent tracks
        for (int i = duplicate.Order + 1; i < Tracks.Count; i++)
        {
            Tracks[i].Order = i;
        }

        OutputLine($"Duplicated track '{e.Track.Name}' -> '{duplicate.Name}'");

        // Select the duplicate in the properties panel
        TrackPropertiesPanel.SelectedTrack = duplicate;
        StatusText.Text = $"Track '{e.Track.Name}' duplicated";
    }

    private void TrackPropertiesPanel_TrackDeleteRequested(object? sender, TrackEventArgs e)
    {
        var result = MessageBox.Show(
            $"Are you sure you want to delete track '{e.Track.Name}'?",
            "Delete Track",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            // Check if the track is frozen and clean up frozen data
            if (e.Track.IsFrozen && _frozenTrackData.TryGetValue(e.Track.Id, out var freezeData))
            {
                // Delete the frozen audio file if it exists
                if (!string.IsNullOrEmpty(freezeData.FrozenAudioFilePath) && File.Exists(freezeData.FrozenAudioFilePath))
                {
                    try
                    {
                        File.Delete(freezeData.FrozenAudioFilePath);
                        OutputLine($"Deleted frozen audio file: {freezeData.FrozenAudioFilePath}");
                    }
                    catch (Exception ex)
                    {
                        OutputLine($"Warning: Could not delete frozen audio file: {ex.Message}");
                    }
                }

                _frozenTrackData.Remove(e.Track.Id);
            }

            // Remove the track from the tracks collection
            TrackInfo? trackToRemove = null;
            foreach (var track in Tracks)
            {
                if (track.Id == e.Track.Id)
                {
                    trackToRemove = track;
                    break;
                }
            }

            if (trackToRemove != null)
            {
                Tracks.Remove(trackToRemove);

                // Update order for remaining tracks
                for (int i = 0; i < Tracks.Count; i++)
                {
                    Tracks[i].Order = i;
                }
            }

            OutputLine($"Deleted track: {e.Track.Name}");
            TrackPropertiesPanel.ClearSelection();
            StatusText.Text = $"Track '{e.Track.Name}' deleted";
        }
    }

    private async void TrackPropertiesPanel_TrackFreezeRequested(object? sender, TrackEventArgs e)
    {
        // Note: IsFrozen is toggled before this event is raised, so:
        // - IsFrozen == true means we need to freeze (track was just set to frozen)
        // - IsFrozen == false means we need to unfreeze (track was just set to unfrozen)
        if (e.Track.IsFrozen)
        {
            await FreezeTrackAsync(e.Track);
        }
        else
        {
            UnfreezeTrack(e.Track);
        }
    }

    /// <summary>
    /// Freezes a track by rendering it to an audio file and storing the original state.
    /// </summary>
    /// <param name="track">The track to freeze.</param>
    private async Task FreezeTrackAsync(TrackInfo track)
    {
        OutputLine($"Freezing track: {track.Name}...");
        StatusText.Text = $"Freezing track '{track.Name}'...";

        try
        {
            // Create freeze data directory if it doesn't exist
            var frozenTracksDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MusicEngineEditor",
                "FrozenTracks");

            if (!Directory.Exists(frozenTracksDir))
            {
                Directory.CreateDirectory(frozenTracksDir);
            }

            // Generate a unique filename for the frozen audio
            var frozenFileName = $"frozen_{track.Id}_{DateTime.Now:yyyyMMdd_HHmmss}.wav";
            var frozenFilePath = Path.Combine(frozenTracksDir, frozenFileName);

            // Store the original track data for unfreezing
            var freezeData = new FreezeTrackData
            {
                TrackId = track.Id,
                OriginalName = track.Name,
                OriginalInstrumentName = track.InstrumentName,
                OriginalInstrumentPath = track.InstrumentPath,
                OriginalTrackType = track.TrackType,
                FrozenAudioFilePath = frozenFilePath,
                FrozenAt = DateTime.Now
            };

            // Simulate freeze operation (render track to audio)
            // In a full implementation, this would use FreezeManager from MusicEngine.Core.Freeze
            await Task.Run(async () =>
            {
                // Simulate rendering time
                await Task.Delay(500);

                // In a real implementation, we would:
                // 1. Get the track's pattern from the sequencer
                // 2. Use TrackRenderer to render the pattern to audio
                // 3. Save the rendered audio to the file path
                // 4. Store the freeze data

                // For now, create an empty placeholder file to indicate the track is frozen
                File.WriteAllText(frozenFilePath + ".freeze", $"Frozen track: {track.Name}\nFrozen at: {freezeData.FrozenAt}");
            });

            // Calculate duration (placeholder - would come from actual rendered audio)
            freezeData.DurationSeconds = 30.0; // Placeholder duration

            // Store the freeze data
            _frozenTrackData[track.Id] = freezeData;

            // Update the track display to indicate it's frozen
            track.Name = $"[Frozen] {freezeData.OriginalName}";

            OutputLine($"Track '{freezeData.OriginalName}' frozen successfully");
            OutputLine($"  Frozen audio path: {frozenFilePath}");
            StatusText.Text = $"Track '{freezeData.OriginalName}' frozen";
        }
        catch (Exception ex)
        {
            OutputLine($"Error freezing track: {ex.Message}");
            StatusText.Text = $"Failed to freeze track '{track.Name}'";

            // Revert the frozen state on error
            track.IsFrozen = false;
        }
    }

    /// <summary>
    /// Unfreezes a track by restoring its original state.
    /// </summary>
    /// <param name="track">The track to unfreeze.</param>
    private void UnfreezeTrack(TrackInfo track)
    {
        OutputLine($"Unfreezing track: {track.Name}...");
        StatusText.Text = $"Unfreezing track '{track.Name}'...";

        try
        {
            // Check if we have freeze data for this track
            if (!_frozenTrackData.TryGetValue(track.Id, out var freezeData))
            {
                OutputLine($"Warning: No freeze data found for track {track.Id}. Resetting frozen state.");
                track.IsFrozen = false;
                StatusText.Text = $"Track unfrozen (no previous state to restore)";
                return;
            }

            // Delete the frozen audio file if it exists
            if (!string.IsNullOrEmpty(freezeData.FrozenAudioFilePath))
            {
                // Delete the actual audio file
                if (File.Exists(freezeData.FrozenAudioFilePath))
                {
                    File.Delete(freezeData.FrozenAudioFilePath);
                }

                // Delete the freeze metadata file
                var freezeMetaFile = freezeData.FrozenAudioFilePath + ".freeze";
                if (File.Exists(freezeMetaFile))
                {
                    File.Delete(freezeMetaFile);
                }
            }

            // Restore original track name
            track.Name = freezeData.OriginalName;

            // Restore original instrument info
            track.InstrumentName = freezeData.OriginalInstrumentName;
            track.InstrumentPath = freezeData.OriginalInstrumentPath;

            // Remove the freeze data
            _frozenTrackData.Remove(track.Id);

            OutputLine($"Track '{freezeData.OriginalName}' unfrozen successfully");
            StatusText.Text = $"Track '{freezeData.OriginalName}' unfrozen";
        }
        catch (Exception ex)
        {
            OutputLine($"Error unfreezing track: {ex.Message}");
            StatusText.Text = $"Failed to unfreeze track '{track.Name}'";

            // Keep the track frozen on error
            track.IsFrozen = true;
        }
    }

    #endregion

    #region Right Panel Data Methods

    private void RefreshMidiDevices_Click(object sender, RoutedEventArgs e)
    {
        RefreshMidiDevices();
    }

    private void RefreshMidiDevices()
    {
        MidiDevices.Clear();

        try
        {
            // Get MIDI input devices from engine
            var inputCount = _engineService.GetMidiInputCount();
            for (int i = 0; i < inputCount; i++)
            {
                var deviceName = _engineService.GetMidiInputName(i);
                MidiDevices.Add(new MidiDeviceInfo
                {
                    Name = deviceName,
                    Type = "Input",
                    DeviceIndex = i,
                    ChannelInfo = "Ch 1-16"  // MIDI inputs typically receive on all channels
                });
            }

            // Get MIDI output devices from engine
            var outputCount = _engineService.GetMidiOutputCount();
            for (int i = 0; i < outputCount; i++)
            {
                var deviceName = _engineService.GetMidiOutputName(i);
                MidiDevices.Add(new MidiDeviceInfo
                {
                    Name = deviceName,
                    Type = "Output",
                    DeviceIndex = i,
                    ChannelInfo = "Ch 1-16"  // MIDI outputs can send on all channels
                });
            }

            // Show message if no devices found
            if (MidiDevices.Count == 0)
            {
                OutputLine("No MIDI devices found. Connect a MIDI device and click Refresh.");
            }
            else
            {
                OutputLine($"Found {inputCount} MIDI input(s) and {outputCount} MIDI output(s).");
            }
        }
        catch (Exception ex)
        {
            // If engine methods don't exist yet or error occurs, add placeholder
            MidiDevices.Add(new MidiDeviceInfo
            {
                Name = "No devices found",
                Type = "-",
                DeviceIndex = -1,
                ChannelInfo = "-"
            });
            OutputLine($"Error enumerating MIDI devices: {ex.Message}");
        }
    }

    private async void ScanVstPlugins_Click(object sender, RoutedEventArgs e)
    {
        // The new VstPluginPanel handles scanning internally
        await VstPluginsPanel.ScanPluginsAsync();
    }

    // VstPluginPanel Event Handlers
    private void VstPluginsPanel_OnOpenPluginEditor(object? sender, VstPluginEventArgs e)
    {
        OpenVstPluginWindow(e.Plugin.Name, e.Plugin.Name);
    }

    private void VstPluginsPanel_OnPluginDoubleClick(object? sender, VstPluginEventArgs e)
    {
        OpenVstPluginWindow(e.Plugin.Name, e.Plugin.Name);
    }

    private void VstPluginsPanel_OnScanCompleted(object? sender, VstScanCompletedEventArgs e)
    {
        OutputLine($"VST scan completed: Found {e.PluginCount} plugins");
    }

    private void UpdateAudioFilesList()
    {
        AudioFiles.Clear();

        if (_currentProject == null) return;

        foreach (var asset in _currentProject.AudioAssets)
        {
            AudioFiles.Add(new AudioFileInfo
            {
                Alias = asset.Alias,
                Duration = "0:00", // Would need to read from file
                Format = Path.GetExtension(asset.FileName).TrimStart('.')
            });
        }
    }

    private void AudioFile_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (AudioFilesList.SelectedItem is AudioFileInfo audio)
        {
            // Insert code to load this audio file
            var code = $"var {audio.Alias} = LoadAudio(\"{audio.Alias}\");";
            CodeEditor.Document.Insert(CodeEditor.CaretOffset, code);
        }
    }

    private void AudioFilesPanel_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            var audioExtensions = new[] { ".wav", ".mp3", ".ogg", ".flac", ".aiff" };

            bool hasAudioFile = files.Any(f =>
                audioExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));

            e.Effects = hasAudioFile ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void AudioFilesPanel_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (_currentProject == null)
        {
            MessageBox.Show("Please create or open a project first.", "No Project",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            var audioExtensions = new[] { ".wav", ".mp3", ".ogg", ".flac", ".aiff" };

            foreach (var file in files)
            {
                if (audioExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                {
                    var alias = Path.GetFileNameWithoutExtension(file);
                    _ = _projectService.ImportAudioAsync(_currentProject, file, alias);
                    OutputLine($"Imported audio: {alias}");
                }
            }

            UpdateProjectExplorer();
            UpdateAudioFilesList();
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
            UpdateAudioFilesList();
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

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var settingsService = App.Services.GetRequiredService<ISettingsService>();
        var dialog = new SettingsDialog(settingsService)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            StatusText.Text = "Settings saved";
            OutputLine("Settings saved successfully.");
        }
    }

    private void Find_Click(object sender, RoutedEventArgs e)
    {
        FindReplaceBar.ShowFind();
    }

    private void Replace_Click(object sender, RoutedEventArgs e)
    {
        FindReplaceBar.ShowReplace();
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
            "  Ctrl+Enter - Run script\n" +
            "  Escape - Stop / All notes off\n" +
            "  Ctrl+S - Save\n" +
            "  Ctrl+Shift+S - Save All\n" +
            "  Ctrl+Shift+N - New Project\n" +
            "  Ctrl+Shift+O - Open Project",
            "About MusicEngine Editor",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    #endregion

    #region Custom Title Bar

    private bool _isMaximized = true; // Start maximized
    private bool _isDragging = false;
    private Point _dragStartPoint;
    private Point _windowStartPosition;

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // Double-click to toggle maximize
            ToggleMaximize();
        }
        else
        {
            // Start drag
            _isDragging = true;
            _dragStartPoint = e.GetPosition(this);
            _windowStartPosition = new Point(Left, Top);
            ((UIElement)sender).CaptureMouse();
        }
    }

    private void TitleBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        ((UIElement)sender).ReleaseMouseCapture();
    }

    private void TitleBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
        {
            // If maximized and dragging, restore first
            if (_isMaximized)
            {
                // Calculate proportional position
                var mousePos = e.GetPosition(this);
                var screenPos = PointToScreen(mousePos);

                _isMaximized = false;
                WindowState = System.Windows.WindowState.Normal;

                // Position window so the mouse is still over it proportionally
                Left = screenPos.X - (Width / 2);
                Top = screenPos.Y - 16; // Center of title bar

                _dragStartPoint = new Point(Width / 2, 16);
            }
            else
            {
                var currentPos = e.GetPosition(this);
                var delta = currentPos - _dragStartPoint;
                Left = _windowStartPosition.X + delta.X;
                Top = _windowStartPosition.Y + delta.Y;
                _windowStartPosition = new Point(Left, Top);
            }
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = System.Windows.WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleMaximize()
    {
        if (_isMaximized)
        {
            WindowState = System.Windows.WindowState.Normal;
            _isMaximized = false;
        }
        else
        {
            WindowState = System.Windows.WindowState.Maximized;
            _isMaximized = true;
        }
    }

    // Update maximize button icon when window state changes
    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        _isMaximized = WindowState == System.Windows.WindowState.Maximized;
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
            // Press Ctrl+Enter to execute, Escape to stop

            // Set BPM and start the sequencer
            Sequencer.Bpm = 120;
            Sequencer.Start();

            // Create a simple synth with sawtooth waveform
            var synth = CreateSynth();
            synth.SetParameter("waveform", 2);  // 0=Sine, 1=Square, 2=Sawtooth, 3=Triangle, 4=Noise
            synth.SetParameter("cutoff", 0.6f);

            // === TEST: Play a chord directly (no MIDI keyboard needed) ===
            Print("Playing test chord...");
            synth.NoteOn(60, 100);  // C4 (Middle C)
            synth.NoteOn(64, 100);  // E4
            synth.NoteOn(67, 100);  // G4

            Print("You should hear a C major chord now!");
            Print("Press Escape to stop all notes.");
            Print("");

            // === MIDI Setup (only works if you have a MIDI keyboard connected) ===
            // Check the MIDI panel on the right for available devices
            // Engine.RouteMidiInput(0, synth);
            // Engine.MapRange(0, 21, 108, synth, false);
            // Print("MIDI keyboard routed to synth.");

            // Or load a VST plugin:
            // var vital = vst.load("Vital");
            // vital?.from(0);
            """;
    }

    #endregion

    #region Output/Problems Panel

    private void OutputTab_Click(object sender, MouseButtonEventArgs e)
    {
        SwitchOutputTab(true);
    }

    private void ProblemsTab_Click(object sender, MouseButtonEventArgs e)
    {
        SwitchOutputTab(false);
    }

    private void SwitchOutputTab(bool showOutput)
    {
        _showingOutput = showOutput;

        if (showOutput)
        {
            OutputTabHeader.Background = (System.Windows.Media.Brush)FindResource("AccentBrush");
            ((TextBlock)OutputTabHeader.Child).Foreground = System.Windows.Media.Brushes.White;
            ((TextBlock)OutputTabHeader.Child).FontWeight = FontWeights.SemiBold;

            ProblemsTabHeader.Background = System.Windows.Media.Brushes.Transparent;
            var problemsStack = (StackPanel)ProblemsTabHeader.Child;
            ((TextBlock)problemsStack.Children[0]).Foreground = (System.Windows.Media.Brush)FindResource("SecondaryForegroundBrush");
            ((TextBlock)problemsStack.Children[0]).FontWeight = FontWeights.Normal;

            OutputBox.Visibility = Visibility.Visible;
            ProblemsListView.Visibility = Visibility.Collapsed;
        }
        else
        {
            OutputTabHeader.Background = System.Windows.Media.Brushes.Transparent;
            ((TextBlock)OutputTabHeader.Child).Foreground = (System.Windows.Media.Brush)FindResource("SecondaryForegroundBrush");
            ((TextBlock)OutputTabHeader.Child).FontWeight = FontWeights.Normal;

            ProblemsTabHeader.Background = (System.Windows.Media.Brush)FindResource("AccentBrush");
            var problemsStack = (StackPanel)ProblemsTabHeader.Child;
            ((TextBlock)problemsStack.Children[0]).Foreground = System.Windows.Media.Brushes.White;
            ((TextBlock)problemsStack.Children[0]).FontWeight = FontWeights.SemiBold;

            OutputBox.Visibility = Visibility.Collapsed;
            ProblemsListView.Visibility = Visibility.Visible;
        }
    }

    private void ProblemsListView_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ProblemsListView.SelectedItem is ProblemItem problem)
        {
            // Navigate to the error location
            NavigateToError(problem);
        }
    }

    private void NavigateToError(ProblemItem problem)
    {
        // If the file is open in a tab, switch to it
        if (!string.IsNullOrEmpty(problem.FilePath) && _openTabs.TryGetValue(problem.FilePath, out var tab))
        {
            EditorTabs.SelectedItem = tab;
        }

        // Navigate to the line and column
        try
        {
            var line = Math.Max(1, problem.Line);
            var column = Math.Max(1, problem.Column);

            if (line <= CodeEditor.Document.LineCount)
            {
                var offset = CodeEditor.Document.GetOffset(line, column);
                CodeEditor.CaretOffset = offset;
                CodeEditor.ScrollToLine(line);
                CodeEditor.TextArea.Focus();

                // Select the line for visibility
                var lineInfo = CodeEditor.Document.GetLineByNumber(line);
                CodeEditor.Select(lineInfo.Offset, lineInfo.Length);
            }
        }
        catch (Exception ex)
        {
            OutputLine($"Could not navigate to error: {ex.Message}");
        }
    }

    #endregion

    #region VST Plugin Windows

    public void OpenVstPluginWindow(string pluginName, string variableName)
    {
        var key = $"{variableName}_{pluginName}";

        if (_vstWindows.TryGetValue(key, out var existingWindow))
        {
            // Window already exists, show it
            existingWindow.ShowWindow();
        }
        else
        {
            // Create new VST window
            var window = new VstPluginWindow(pluginName, variableName, null)
            {
                Owner = this
            };

            _vstWindows[key] = window;

            // Remove from dictionary when force-closed
            window.Closed += (s, e) =>
            {
                if (!window.KeepRunning)
                {
                    _vstWindows.Remove(key);
                }
            };

            window.Show();
            OutputLine($"Opened VST plugin window: {pluginName} (variable: {variableName})");
        }
    }

    public void CloseAllVstWindows()
    {
        foreach (var window in _vstWindows.Values.ToList())
        {
            window.ForceClose();
        }
        _vstWindows.Clear();
    }

    #endregion

    #region Active Instruments Animation

    private void AnimationTimer_Tick(object? sender, EventArgs e)
    {
        foreach (var instrument in ActiveInstruments)
        {
            instrument.UpdateAnimation();
        }
    }

    public void AddActiveInstrument(string name, string type)
    {
        Dispatcher.Invoke(() =>
        {
            // Check if already exists
            var existing = ActiveInstruments.FirstOrDefault(i => i.Name == name);
            if (existing == null)
            {
                ActiveInstruments.Add(new ActiveInstrumentInfo
                {
                    Name = name,
                    InstrumentType = type,
                    IsActive = true
                });
                UpdateNoInstrumentsVisibility();
            }
        });
    }

    public void RemoveActiveInstrument(string name)
    {
        Dispatcher.Invoke(() =>
        {
            var instrument = ActiveInstruments.FirstOrDefault(i => i.Name == name);
            if (instrument != null)
            {
                ActiveInstruments.Remove(instrument);
                UpdateNoInstrumentsVisibility();
            }
        });
    }

    public void TriggerNoteOn(string instrumentName, int note, int velocity)
    {
        Dispatcher.Invoke(() =>
        {
            var instrument = ActiveInstruments.FirstOrDefault(i => i.Name == instrumentName);
            if (instrument != null)
            {
                instrument.TriggerNote(note, velocity);
            }
        });
    }

    public void TriggerNoteOff(string instrumentName, int note)
    {
        Dispatcher.Invoke(() =>
        {
            var instrument = ActiveInstruments.FirstOrDefault(i => i.Name == instrumentName);
            if (instrument != null)
            {
                instrument.ReleaseNote(note);
            }
        });
    }

    public void ClearActiveInstruments()
    {
        Dispatcher.Invoke(() =>
        {
            ActiveInstruments.Clear();
            UpdateNoInstrumentsVisibility();
        });
    }

    private void UpdateNoInstrumentsVisibility()
    {
        // Active instruments display has been removed from toolbar
    }

    #endregion

    #region Workshop Panel Event Handlers

    private async void WorkshopPanel_OnRunCode(object? sender, WorkshopCodeEventArgs e)
    {
        // Execute the code example from the workshop via the EngineService
        if (string.IsNullOrWhiteSpace(e.Code))
        {
            OutputLine("No code to execute.");
            return;
        }

        _isRunning = true;
        StatusText.Text = "Executing workshop example...";
        UpdateRunStopButton();

        // Clear previous problems
        Problems.Clear();
        UpdateErrorBadge();

        OutputLine("----------------------------------------");
        OutputLine($"[{DateTime.Now:HH:mm:ss}] Running workshop example...");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await _engineService.ExecuteScriptAsync(e.Code);
            stopwatch.Stop();

            if (result.Success)
            {
                StatusText.Text = $"Running ({stopwatch.ElapsedMilliseconds}ms)";
                OutputLine($"Workshop example executed successfully ({stopwatch.ElapsedMilliseconds}ms)");

                if (!string.IsNullOrEmpty(result.Output))
                {
                    OutputLine(result.Output);
                }

                // Parse code to extract instruments and start animation
                ExtractInstrumentsFromCode(e.Code);
                _animationTimer.Start();
            }
            else
            {
                _isRunning = false;
                UpdateRunStopButton();
                StatusText.Text = "Workshop example error";
                OutputLine($"ERROR: {result.ErrorMessage}");

                foreach (var error in result.Errors)
                {
                    OutputLine($"  Line {error.Line}: {error.Message}");

                    // Add to Problems panel
                    Problems.Add(new ProblemItem
                    {
                        Severity = error.Severity == "Error" ? ProblemSeverity.Error : ProblemSeverity.Warning,
                        Message = error.Message,
                        FileName = "Workshop Example",
                        FilePath = "",
                        Line = error.Line,
                        Column = error.Column
                    });
                }

                UpdateErrorBadge();

                // Switch to Problems tab if there are errors
                if (Problems.Count > 0)
                {
                    SwitchOutputTab(false);
                }
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _isRunning = false;
            StatusText.Text = "Workshop execution failed";
            OutputLine($"EXCEPTION: {ex.Message}");

            // Add exception to Problems
            Problems.Add(new ProblemItem
            {
                Severity = ProblemSeverity.Error,
                Message = ex.Message,
                FileName = "Workshop Example",
                FilePath = "",
                Line = 1,
                Column = 1
            });
            UpdateErrorBadge();
        }

        UpdateRunStopButton();
    }

    private void WorkshopPanel_OnCopyCode(object? sender, WorkshopCodeEventArgs e)
    {
        // Code is already copied to clipboard by the WorkshopPanel itself
        // Just show feedback in the output
        OutputLine($"[{DateTime.Now:HH:mm:ss}] Code copied to clipboard.");
        StatusText.Text = "Code copied to clipboard";
    }

    private void WorkshopPanel_OnInsertCode(object? sender, WorkshopCodeEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Code))
        {
            return;
        }

        // Insert code at the current cursor position in the editor
        // If there's a selection, replace it; otherwise, insert at cursor
        var textArea = CodeEditor.TextArea;
        var document = CodeEditor.Document;

        if (textArea.Selection.Length > 0)
        {
            // Replace selection with the code
            var selectionStart = textArea.Selection.SurroundingSegment.Offset;
            var selectionLength = textArea.Selection.SurroundingSegment.Length;
            document.Replace(selectionStart, selectionLength, e.Code);
            CodeEditor.CaretOffset = selectionStart + e.Code.Length;
        }
        else
        {
            // Insert at cursor position
            var insertPosition = CodeEditor.CaretOffset;

            // Check if we should add a newline before the inserted code
            if (insertPosition > 0)
            {
                var charBefore = document.GetCharAt(insertPosition - 1);
                if (charBefore != '\n' && charBefore != '\r')
                {
                    // Add newline before the code if not at the start of a line
                    document.Insert(insertPosition, Environment.NewLine);
                    insertPosition += Environment.NewLine.Length;
                }
            }

            document.Insert(insertPosition, e.Code);
            CodeEditor.CaretOffset = insertPosition + e.Code.Length;
        }

        // Mark as having unsaved changes
        _hasUnsavedChanges = true;

        // Show feedback
        OutputLine($"[{DateTime.Now:HH:mm:ss}] Code inserted into editor.");
        StatusText.Text = "Code inserted into editor";

        // Focus the editor
        CodeEditor.Focus();
    }

    #endregion

    #region Cloud, Collaboration, and Network MIDI

    /// <summary>
    /// Opens the Cloud Storage dialog.
    /// </summary>
    private void CloudStorage_Click(object sender, RoutedEventArgs e)
    {
        CloudStorageDialog.ShowDialog(this);
    }

    /// <summary>
    /// Opens the Collaboration dialog.
    /// </summary>
    private void Collaboration_Click(object sender, RoutedEventArgs e)
    {
        CollaborationDialog.ShowDialog(this);
    }

    /// <summary>
    /// Opens the Network MIDI dialog.
    /// </summary>
    private void NetworkMidi_Click(object sender, RoutedEventArgs e)
    {
        NetworkMidiDialog.ShowDialog(this);
    }

    #endregion

    #region Command Palette

    /// <summary>
    /// Shows the command palette dialog.
    /// </summary>
    private void ShowCommandPalette()
    {
        // Register commands if not already done
        RegisterCommandPaletteCommands();

        // Show the palette
        var selectedCommand = CommandPaletteDialog.ShowPalette(this);

        if (selectedCommand != null)
        {
            OutputLine($"Executed: {selectedCommand.Category}: {selectedCommand.Name}");
        }
    }

    /// <summary>
    /// Registers commands with the command palette service.
    /// </summary>
    private void RegisterCommandPaletteCommands()
    {
        var service = CommandPaletteService.Instance;

        // Only register once
        if (service.Commands.Count > 0)
            return;

        // File commands
        service.RegisterCommand("New Project", "File", () => NewProject_Click(this, new RoutedEventArgs()), "Ctrl+Shift+N", "Create a new project");
        service.RegisterCommand("Open Project", "File", () => OpenProject_Click(this, new RoutedEventArgs()), "Ctrl+Shift+O", "Open an existing project");
        service.RegisterCommand("New File", "File", () => NewFile_Click(this, new RoutedEventArgs()), "Ctrl+N", "Create a new script file");
        service.RegisterCommand("Save", "File", () => SaveScript_Click(this, new RoutedEventArgs()), "Ctrl+S", "Save current file");
        service.RegisterCommand("Save All", "File", () => SaveAll_Click(this, new RoutedEventArgs()), "Ctrl+Shift+S", "Save all open files");
        service.RegisterCommand("Settings", "File", () => Settings_Click(this, new RoutedEventArgs()), "Ctrl+,", "Open settings");
        service.RegisterCommand("Exit", "File", () => Exit_Click(this, new RoutedEventArgs()), null, "Exit the application");

        // Edit commands
        service.RegisterCommand("Undo", "Edit", () => CodeEditor.Undo(), "Ctrl+Z", "Undo last action");
        service.RegisterCommand("Redo", "Edit", () => CodeEditor.Redo(), "Ctrl+Y", "Redo last undone action");
        service.RegisterCommand("Cut", "Edit", () => CodeEditor.Cut(), "Ctrl+X", "Cut selection");
        service.RegisterCommand("Copy", "Edit", () => CodeEditor.Copy(), "Ctrl+C", "Copy selection");
        service.RegisterCommand("Paste", "Edit", () => CodeEditor.Paste(), "Ctrl+V", "Paste from clipboard");
        service.RegisterCommand("Select All", "Edit", () => CodeEditor.SelectAll(), "Ctrl+A", "Select all text");
        service.RegisterCommand("Find", "Edit", () => FindReplaceBar.ShowFind(), "Ctrl+F", "Find text");
        service.RegisterCommand("Replace", "Edit", () => FindReplaceBar.ShowReplace(), "Ctrl+H", "Find and replace text");

        // Transport commands
        service.RegisterCommand("Run Script", "Transport", () => _ = ExecuteScript(), "Ctrl+Enter", "Run the current script", ["play", "execute", "start"]);
        service.RegisterCommand("Stop", "Transport", () =>
        {
            _engineService.AllNotesOff();
            _isRunning = false;
            _visualization?.OnPlaybackStopped();
            StatusText.Text = "Stopped";
            OutputLine("Stopped");
        }, "Escape", "Stop playback", ["pause", "halt"]);

        // View commands
        service.RegisterCommand("Toggle Output", "View", () =>
        {
            if (_outputVisible)
            {
                OutputPanel.Visibility = Visibility.Collapsed;
                OutputSplitter.Visibility = Visibility.Collapsed;
                _outputVisible = false;
            }
            else
            {
                OutputPanel.Visibility = Visibility.Visible;
                OutputSplitter.Visibility = Visibility.Visible;
                _outputVisible = true;
            }
        }, null, "Toggle output panel visibility");

        service.RegisterCommand("Clear Output", "View", () => OutputBox.Clear(), null, "Clear the output panel");

        // Help commands
        service.RegisterCommand("About", "Help", () =>
        {
            var dialog = new AboutDialog { Owner = this };
            dialog.ShowDialog();
        }, null, "About MusicEngine Editor");

        service.RegisterCommand("Keyboard Shortcuts", "Help", () =>
        {
            var dialog = new ShortcutsDialog(App.Services.GetRequiredService<IShortcutService>()) { Owner = this };
            dialog.ShowDialog();
        }, null, "Show keyboard shortcuts");

        // Tools
        service.RegisterCommand("Quantize", "Tools", () =>
        {
            var dialog = new QuantizeDialog { Owner = this };
            dialog.ShowDialog();
        }, "Q", "Quantize selected notes", ["snap", "grid"]);

        service.RegisterCommand("Export Audio", "Tools", () =>
        {
            var dialog = new ExportDialog { Owner = this };
            dialog.ShowDialog();
        }, null, "Export project to audio file", ["render", "bounce"]);

        service.RegisterCommand("Metronome Settings", "Tools", () =>
        {
            var dialog = new MetronomeSettingsDialog(App.Services.GetRequiredService<MetronomeService>()) { Owner = this };
            dialog.ShowDialog();
        }, null, "Configure metronome");

        service.RegisterCommand("Recording Setup", "Tools", () =>
        {
            var dialog = new RecordingSetupDialog { Owner = this };
            dialog.ShowDialog();
        }, null, "Configure recording settings");

        service.RegisterCommand("Stem Export", "Tools", () =>
        {
            var dialog = new StemExportDialog { Owner = this };
            dialog.ShowDialog();
        }, null, "Export individual stems");

        // Cloud & Collaboration commands
        service.RegisterCommand("Cloud Storage", "Cloud", () =>
        {
            CloudStorageDialog.ShowDialog(this);
        }, null, "Open cloud storage manager", ["sync", "upload", "download"]);

        service.RegisterCommand("Collaboration", "Cloud", () =>
        {
            CollaborationDialog.ShowDialog(this);
        }, null, "Start or join collaboration session", ["collab", "share", "realtime"]);

        service.RegisterCommand("Network MIDI", "Cloud", () =>
        {
            NetworkMidiDialog.ShowDialog(this);
        }, null, "Configure network MIDI (RTP-MIDI)", ["rtpmidi", "network", "remote"]);
    }

    #endregion
}

// Data classes for the right panel lists
public class MidiDeviceInfo
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";  // "Input" or "Output"
    public int DeviceIndex { get; set; } = -1;
    public string ChannelInfo { get; set; } = "";  // e.g., "Ch 1-16" or "Omni"

    // Display string combining all info for the UI
    public string DisplayName => $"{Name} ({Type})";
    public string DisplayChannel => string.IsNullOrEmpty(ChannelInfo) ? "Ch 1-16" : ChannelInfo;

    // Icon based on type
    public string TypeIcon => Type == "Input" ? "\u2B05" : "\u27A1";  // Left arrow for input, right arrow for output

    // Color for the type indicator
    public System.Windows.Media.Brush TypeColor => Type == "Input"
        ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6A, 0xAB, 0x73))  // Green for input
        : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4B, 0x6E, 0xAF)); // Blue for output
}

public class VstPluginInfo
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Path { get; set; } = "";
}

public class AudioFileInfo
{
    public string Alias { get; set; } = "";
    public string Duration { get; set; } = "";
    public string Format { get; set; } = "";
}

// Problem/Error item for the Problems panel
public enum ProblemSeverity
{
    Error,
    Warning,
    Info
}

public class ProblemItem
{
    public ProblemSeverity Severity { get; set; } = ProblemSeverity.Error;
    public string Message { get; set; } = "";
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public int Line { get; set; }
    public int Column { get; set; }

    // For display in ListView
    public string Icon => Severity switch
    {
        ProblemSeverity.Error => "\u26A0",   // Warning sign (using this since error icon is not standard)
        ProblemSeverity.Warning => "\u26A0",
        ProblemSeverity.Info => "\u2139",    // Info icon
        _ => "\u26A0"
    };

    public System.Windows.Media.Brush IconColor => Severity switch
    {
        ProblemSeverity.Error => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF7, 0x54, 0x64)),
        ProblemSeverity.Warning => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE8, 0xB3, 0x39)),
        ProblemSeverity.Info => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4B, 0x6E, 0xAF)),
        _ => System.Windows.Media.Brushes.White
    };
}

// Active Instrument display item with animation support
public class ActiveInstrumentInfo : System.ComponentModel.INotifyPropertyChanged
{
    private string _name = "";
    private string _instrumentType = "synth";
    private bool _isActive;
    private bool _isPlaying;
    private int _currentNoteValue;
    private int _velocity;
    private double _pulsePhase;
    private DateTime _lastNoteTime;
    private readonly HashSet<int> _activeNotes = new();

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(nameof(Name)); }
    }

    public string InstrumentType
    {
        get => _instrumentType;
        set { _instrumentType = value; OnPropertyChanged(nameof(Icon)); OnPropertyChanged(nameof(IconColor)); }
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            _isActive = value;
            OnPropertyChanged(nameof(IsActive));
            OnPropertyChanged(nameof(TextColor));
            OnPropertyChanged(nameof(BackgroundBrush));
            OnPropertyChanged(nameof(BorderBrush));
            OnPropertyChanged(nameof(FontWeight));
        }
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            _isPlaying = value;
            OnPropertyChanged(nameof(IsPlaying));
            OnPropertyChanged(nameof(TextColor));
            OnPropertyChanged(nameof(BackgroundBrush));
            OnPropertyChanged(nameof(BorderBrush));
            OnPropertyChanged(nameof(NoteVisibility));
            OnPropertyChanged(nameof(NoteColor));
        }
    }

    // Icon based on instrument type
    public string Icon => InstrumentType.ToLower() switch
    {
        "synth" => "\u266B",      // Musical note
        "vst" => "\u2699",        // Gear for VST
        "sampler" => "\u25B6",    // Play triangle for sampler
        "pattern" => "\u2630",    // Trigram for pattern
        _ => "\u266A"             // Default music note
    };

    public System.Windows.Media.Brush IconColor
    {
        get
        {
            if (IsPlaying)
            {
                // Pulsing bright color when playing
                var intensity = (byte)(180 + 75 * Math.Sin(_pulsePhase));
                return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(intensity, 0xFF, intensity));
            }
            if (IsActive)
            {
                return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6A, 0xAB, 0x73)); // Green
            }
            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6F, 0x73, 0x7A)); // Gray
        }
    }

    public System.Windows.Media.Brush TextColor
    {
        get
        {
            if (IsPlaying)
            {
                // Bright pulsing white when playing
                var intensity = (byte)(220 + 35 * Math.Sin(_pulsePhase));
                return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(intensity, intensity, intensity));
            }
            if (IsActive)
            {
                return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xDF, 0xE1, 0xE5)); // Bright
            }
            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6F, 0x73, 0x7A)); // Dim
        }
    }

    public System.Windows.Media.Brush BackgroundBrush
    {
        get
        {
            if (IsPlaying)
            {
                // Glowing background when playing
                var alpha = (byte)(40 + 30 * Math.Sin(_pulsePhase));
                return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, 0x6A, 0xAB, 0x73));
            }
            if (IsActive)
            {
                return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x33, 0x4B, 0x6E, 0xAF));
            }
            return System.Windows.Media.Brushes.Transparent;
        }
    }

    public System.Windows.Media.Brush BorderBrush
    {
        get
        {
            if (IsPlaying)
            {
                // Bright green border when playing
                var intensity = (byte)(100 + 55 * Math.Sin(_pulsePhase));
                return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(intensity, 0xAB, intensity));
            }
            if (IsActive)
            {
                return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4B, 0x6E, 0xAF));
            }
            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x39, 0x3B, 0x40));
        }
    }

    public System.Windows.FontWeight FontWeight => IsPlaying ? System.Windows.FontWeights.Bold : (IsActive ? System.Windows.FontWeights.SemiBold : System.Windows.FontWeights.Normal);

    // Currently playing note display
    public string CurrentNote
    {
        get
        {
            if (!IsPlaying || _activeNotes.Count == 0) return "";
            var note = _activeNotes.First();
            var noteNames = new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            var octave = (note / 12) - 1;
            var noteName = noteNames[note % 12];
            return $"{noteName}{octave}";
        }
    }

    public System.Windows.Media.Brush NoteColor
    {
        get
        {
            if (!IsPlaying) return System.Windows.Media.Brushes.Transparent;
            // Velocity-based color intensity
            var intensity = (byte)(150 + (_velocity * 105 / 127));
            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, intensity, 0x50));
        }
    }

    public System.Windows.Visibility NoteVisibility => IsPlaying && _activeNotes.Count > 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public void TriggerNote(int note, int velocity)
    {
        _activeNotes.Add(note);
        _currentNoteValue = note;
        _velocity = velocity;
        _lastNoteTime = DateTime.Now;
        IsPlaying = true;
        OnPropertyChanged(nameof(CurrentNote));
    }

    public void ReleaseNote(int note)
    {
        _activeNotes.Remove(note);
        if (_activeNotes.Count == 0)
        {
            IsPlaying = false;
        }
        OnPropertyChanged(nameof(CurrentNote));
        OnPropertyChanged(nameof(NoteVisibility));
    }

    public void UpdateAnimation()
    {
        if (IsPlaying)
        {
            _pulsePhase += 0.3; // Speed of pulse
            if (_pulsePhase > Math.PI * 2) _pulsePhase -= Math.PI * 2;

            // Notify all visual properties to update
            OnPropertyChanged(nameof(IconColor));
            OnPropertyChanged(nameof(TextColor));
            OnPropertyChanged(nameof(BackgroundBrush));
            OnPropertyChanged(nameof(BorderBrush));
            OnPropertyChanged(nameof(NoteColor));
        }
        else if (_pulsePhase > 0)
        {
            // Fade out animation
            _pulsePhase = 0;
            OnPropertyChanged(nameof(IconColor));
            OnPropertyChanged(nameof(TextColor));
            OnPropertyChanged(nameof(BackgroundBrush));
            OnPropertyChanged(nameof(BorderBrush));
        }
    }

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Stores data for a frozen track to enable unfreezing.
/// </summary>
public class FreezeTrackData
{
    /// <summary>
    /// Gets or sets the track ID.
    /// </summary>
    public int TrackId { get; set; }

    /// <summary>
    /// Gets or sets the original track name.
    /// </summary>
    public string OriginalName { get; set; } = "";

    /// <summary>
    /// Gets or sets the original instrument name.
    /// </summary>
    public string? OriginalInstrumentName { get; set; }

    /// <summary>
    /// Gets or sets the original instrument path (for VST plugins).
    /// </summary>
    public string? OriginalInstrumentPath { get; set; }

    /// <summary>
    /// Gets or sets the path to the frozen audio file.
    /// </summary>
    public string? FrozenAudioFilePath { get; set; }

    /// <summary>
    /// Gets or sets the original track type.
    /// </summary>
    public Models.TrackType OriginalTrackType { get; set; }

    /// <summary>
    /// Gets or sets when the track was frozen.
    /// </summary>
    public DateTime FrozenAt { get; set; }

    /// <summary>
    /// Gets or sets the duration of the frozen audio in seconds.
    /// </summary>
    public double DurationSeconds { get; set; }
}
