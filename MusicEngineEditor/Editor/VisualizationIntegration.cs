// VisualizationIntegration.cs
// Helper class to easily integrate all visualization components with MainWindow.
// This provides a simple API to connect the MusicEngine to editor highlighting,
// punchcard visualization, and live parameter updates.

using System;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using MusicEngine.Core;
using MusicEngineEditor.Controls;
using MusicEngineEditor.Services;

namespace MusicEngineEditor.Editor;

/// <summary>
/// Easy integration helper for all visualization components.
/// Use this class in MainWindow to connect everything together.
/// </summary>
public class VisualizationIntegration : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private VisualizationBridge? _bridge;
    private PlaybackHighlightService? _highlightService;
    private LiveParameterSystem? _liveParameters;
    private bool _isDisposed;

    // Connected components
    private TextEditor? _editor;
    private PunchcardVisualization? _punchcard;
    private Sequencer? _sequencer;

    /// <summary>Event fired when a visualization error occurs (audio continues).</summary>
    public event EventHandler<string>? VisualizationError;

    /// <summary>Event fired when visualization state changes.</summary>
    public event EventHandler<VisualizationStateEventArgs>? StateChanged;

    public VisualizationIntegration(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _bridge = new VisualizationBridge(dispatcher);
        _bridge.VisualizationError += (s, e) =>
            VisualizationError?.Invoke(this, $"{e.Message}: {e.Exception?.Message}");
        _bridge.StateChanged += (s, e) => StateChanged?.Invoke(this, e);
    }

    /// <summary>
    /// Sets up all visualization for a TextEditor.
    /// Call this once when the editor is loaded.
    /// </summary>
    public void SetupEditor(TextEditor editor)
    {
        _editor = editor;

        // Create highlight service for the editor
        _highlightService = new PlaybackHighlightService(editor);

        // Create live parameter system
        _liveParameters = new LiveParameterSystem(editor);

        // Connect to bridge
        _bridge?.ConnectEditor(editor);
    }

    /// <summary>
    /// Sets up the punchcard visualization.
    /// Call this once when the punchcard is loaded.
    /// </summary>
    public void SetupPunchcard(PunchcardVisualization punchcard)
    {
        _punchcard = punchcard;
        _bridge?.ConnectPunchcard(punchcard);
    }

    /// <summary>
    /// Connects to a Sequencer for live synchronization.
    /// Call this after the engine is initialized.
    /// </summary>
    public void ConnectToSequencer(Sequencer sequencer)
    {
        _sequencer = sequencer;

        // Connect all services to the sequencer
        _bridge?.ConnectSequencer(sequencer);
        _highlightService?.BindToSequencer(sequencer);
        _liveParameters?.BindToSequencer(sequencer);
    }

    /// <summary>
    /// Call this before executing a script to analyze the code.
    /// </summary>
    public void OnBeforeExecute(string code)
    {
        try
        {
            // Analyze code for instrument definitions and note source locations
            _bridge?.AnalyzeCode(code);

            // Start live parameter tracking
            _liveParameters?.AnalyzeAndBindParameters(code);
        }
        catch (Exception ex)
        {
            VisualizationError?.Invoke(this, $"Pre-execution analysis failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Call this after a script executes successfully.
    /// </summary>
    public void OnAfterExecute(bool success)
    {
        if (!success) return;

        try
        {
            // Attach source info to patterns for precise highlighting
            _bridge?.AttachSourceInfoToPatterns();

            // Update punchcard with patterns
            _bridge?.SyncPunchcardPatterns();

            // Start live parameter system
            _liveParameters?.Start();
        }
        catch (Exception ex)
        {
            VisualizationError?.Invoke(this, $"Post-execution setup failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Call this when playback starts.
    /// </summary>
    public void OnPlaybackStarted()
    {
        try
        {
            _highlightService?.Start();
            _punchcard?.StartSync();
        }
        catch (Exception ex)
        {
            VisualizationError?.Invoke(this, $"Playback start visualization failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Call this when playback stops.
    /// </summary>
    public void OnPlaybackStopped()
    {
        try
        {
            _highlightService?.Stop();
            _punchcard?.StopSync();
            _liveParameters?.Stop();
        }
        catch (Exception ex)
        {
            VisualizationError?.Invoke(this, $"Playback stop visualization failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Call this when the code text changes to update live parameter bindings.
    /// </summary>
    public void OnCodeChanged(string code)
    {
        try
        {
            if (_liveParameters?.IsActive == true)
            {
                _liveParameters.AnalyzeAndBindParameters(code);
            }
        }
        catch
        {
            // Ignore code change errors - they're frequent and non-critical
        }
    }

    /// <summary>
    /// Updates a parameter value live (for slider interactions).
    /// </summary>
    public void UpdateParameter(int codeOffset, double newValue)
    {
        try
        {
            _liveParameters?.UpdateParameterAtOffset(codeOffset, newValue);
        }
        catch (Exception ex)
        {
            VisualizationError?.Invoke(this, $"Parameter update failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the current playback highlight service.
    /// </summary>
    public PlaybackHighlightService? HighlightService => _highlightService;

    /// <summary>
    /// Gets the live parameter system.
    /// </summary>
    public LiveParameterSystem? LiveParameters => _liveParameters;

    /// <summary>
    /// Gets the visualization bridge.
    /// </summary>
    public VisualizationBridge? Bridge => _bridge;

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _liveParameters?.Dispose();
        _highlightService?.Dispose();
        _bridge?.Dispose();
    }
}

/// <summary>
/// Extension methods for easy MainWindow integration.
/// </summary>
public static class MainWindowVisualizationExtensions
{
    /// <summary>
    /// Creates and configures a VisualizationIntegration for a window.
    /// </summary>
    public static VisualizationIntegration CreateVisualizationIntegration(
        this System.Windows.Window window,
        TextEditor editor,
        PunchcardVisualization? punchcard = null)
    {
        var integration = new VisualizationIntegration(window.Dispatcher);
        integration.SetupEditor(editor);

        if (punchcard != null)
        {
            integration.SetupPunchcard(punchcard);
        }

        return integration;
    }
}
