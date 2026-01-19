using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MusicEngine.Core;
using MusicEngine.Scripting;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Services;

/// <summary>
/// Service for executing MusicEngine scripts
/// </summary>
public class ScriptExecutionService : IScriptExecutionService, IDisposable
{
    private AudioEngine? _audioEngine;
    private Sequencer? _sequencer;
    private ScriptHost? _scriptHost;
    private CancellationTokenSource? _executionCts;
    private bool _disposed;

    public bool IsRunning { get; private set; }
    public bool IsInitialized { get; private set; }

    public event EventHandler<string>? OutputReceived;
    public event EventHandler<CompilationError>? ErrorOccurred;
    public event EventHandler? ExecutionStarted;
    public event EventHandler? ExecutionStopped;

    public async Task InitializeAsync()
    {
        if (IsInitialized) return;

        await Task.Run(() =>
        {
            _audioEngine = new AudioEngine();
            _audioEngine.Initialize();

            _sequencer = new Sequencer();
            _sequencer.Start();

            _scriptHost = new ScriptHost(_audioEngine, _sequencer);
            IsInitialized = true;
        });

        OutputReceived?.Invoke(this, "Audio engine initialized.");
    }

    public async Task<CompilationResult> CompileProjectAsync(MusicProject project)
    {
        var result = new CompilationResult();

        try
        {
            // Find entry point script
            var entryScript = project.Scripts.FirstOrDefault(s => s.IsEntryPoint);
            if (entryScript == null)
            {
                result.Success = false;
                result.Errors.Add(new CompilationError
                {
                    Message = "No entry point script found. Mark a script with 'entryPoint: true' in the project.",
                    Severity = ErrorSeverity.Error
                });
                return result;
            }

            // For now, we'll just validate the scripts exist
            foreach (var script in project.Scripts)
            {
                if (string.IsNullOrWhiteSpace(script.Content))
                {
                    result.Errors.Add(new CompilationError
                    {
                        Message = $"Script '{script.FileName}' is empty.",
                        FilePath = script.FilePath,
                        Severity = ErrorSeverity.Warning
                    });
                }
            }

            result.Success = !result.Errors.Any(e => e.Severity == ErrorSeverity.Error);
            OutputReceived?.Invoke(this, $"Compilation {(result.Success ? "successful" : "failed")}: {project.Name}");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add(new CompilationError
            {
                Message = $"Compilation failed: {ex.Message}",
                Severity = ErrorSeverity.Error
            });
        }

        return result;
    }

    public async Task RunAsync(MusicProject project)
    {
        if (!IsInitialized)
        {
            await InitializeAsync();
        }

        if (IsRunning)
        {
            await StopAsync();
        }

        var compilationResult = await CompileProjectAsync(project);

        if (!compilationResult.Success)
        {
            foreach (var error in compilationResult.Errors)
            {
                ErrorOccurred?.Invoke(this, error);
            }
            return;
        }

        _executionCts = new CancellationTokenSource();
        IsRunning = true;
        ExecutionStarted?.Invoke(this, EventArgs.Empty);

        try
        {
            // Clear previous state
            _scriptHost?.ClearState();

            // Find and execute entry point script
            var entryScript = project.Scripts.FirstOrDefault(s => s.IsEntryPoint);
            if (entryScript == null)
            {
                OutputReceived?.Invoke(this, "No entry point script found.");
                return;
            }

            // Set BPM
            if (_sequencer != null)
            {
                _sequencer.Bpm = project.Settings.DefaultBpm;
            }

            OutputReceived?.Invoke(this, $"Starting: {project.Name} @ {project.Settings.DefaultBpm} BPM");

            // Build full script from all project scripts
            var fullCode = new System.Text.StringBuilder();

            // Add non-entry scripts first (dependencies)
            foreach (var script in project.Scripts.Where(s => !s.IsEntryPoint))
            {
                fullCode.AppendLine(StripHeader(script.Content));
                fullCode.AppendLine();
            }

            // Add entry point
            fullCode.AppendLine(StripHeader(entryScript.Content));

            // Execute via ScriptHost
            if (_scriptHost != null)
            {
                await _scriptHost.ExecuteScriptAsync(fullCode.ToString());
                OutputReceived?.Invoke(this, "Script executed successfully.");
            }
        }
        catch (Microsoft.CodeAnalysis.Scripting.CompilationErrorException compEx)
        {
            OutputReceived?.Invoke(this, "Compilation errors:");
            foreach (var diagnostic in compEx.Diagnostics)
            {
                var lineSpan = diagnostic.Location.GetLineSpan();
                var error = new CompilationError
                {
                    Message = diagnostic.GetMessage(),
                    Line = lineSpan.StartLinePosition.Line + 1,
                    Column = lineSpan.StartLinePosition.Character + 1,
                    Severity = diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error
                        ? ErrorSeverity.Error
                        : ErrorSeverity.Warning
                };
                ErrorOccurred?.Invoke(this, error);
                OutputReceived?.Invoke(this, $"  Line {error.Line}: {error.Message}");
            }
        }
        catch (OperationCanceledException)
        {
            OutputReceived?.Invoke(this, "Execution cancelled.");
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, new CompilationError
            {
                Message = $"Runtime error: {ex.Message}",
                Severity = ErrorSeverity.Error
            });
            OutputReceived?.Invoke(this, $"Runtime error: {ex.Message}");
        }
    }

    public async Task StopAsync()
    {
        if (!IsRunning) return;

        _executionCts?.Cancel();
        _scriptHost?.ClearState();

        // Wait for clean shutdown
        await Task.Delay(100);

        IsRunning = false;
        ExecutionStopped?.Invoke(this, EventArgs.Empty);
        OutputReceived?.Invoke(this, "Stopped.");
    }

    private static string StripHeader(string content)
    {
        var lines = content.Split('\n');
        var startIndex = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("//") || line.StartsWith("#") || string.IsNullOrEmpty(line))
            {
                startIndex = i + 1;
            }
            else
            {
                break;
            }
        }

        return string.Join('\n', lines.Skip(startIndex));
    }

    public void Dispose()
    {
        if (_disposed) return;

        _executionCts?.Cancel();
        _executionCts?.Dispose();
        _sequencer?.Stop();
        _audioEngine?.Dispose();

        _disposed = true;
    }
}
