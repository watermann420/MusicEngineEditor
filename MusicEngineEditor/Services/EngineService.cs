using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using MusicEngine.Core;
using MusicEngine.Scripting;

namespace MusicEngineEditor.Services;

public class EngineService : IDisposable
{
    private AudioEngine? _engine;
    private Sequencer? _sequencer;
    private ScriptHost? _scriptHost;
    private bool _disposed;

    public double Bpm => _sequencer?.Bpm ?? 120;
    public double CurrentBeat => _sequencer?.CurrentBeat ?? 0;
    public int PatternCount { get; private set; }
    public bool IsInitialized { get; private set; }

    public string? InitializationOutput { get; private set; }

    public async Task InitializeAsync()
    {
        // Capture console output during initialization to show device info
        var outputCapture = new StringWriter();
        var originalOut = Console.Out;

        try
        {
            Console.SetOut(outputCapture);

            await Task.Run(() =>
            {
                _engine = new AudioEngine();
                _engine.Initialize();

                _sequencer = new Sequencer();
                _sequencer.Start();

                _scriptHost = new ScriptHost(_engine, _sequencer);
                IsInitialized = true;
            });

            InitializationOutput = outputCapture.ToString();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    public async Task<ScriptResult> ExecuteScriptAsync(string code)
    {
        if (!IsInitialized || _scriptHost == null || _engine == null || _sequencer == null)
        {
            return new ScriptResult
            {
                Success = false,
                ErrorMessage = "Engine not initialized"
            };
        }

        var result = new ScriptResult();
        var outputCapture = new StringWriter();
        var originalOut = Console.Out;

        try
        {
            // Clear previous state (like /S command)
            _scriptHost.ClearState();

            // Capture console output
            Console.SetOut(outputCapture);

            await _scriptHost.ExecuteScriptAsync(code);

            result.Success = true;
            result.Output = outputCapture.ToString();
        }
        catch (Microsoft.CodeAnalysis.Scripting.CompilationErrorException compEx)
        {
            result.Success = false;
            result.ErrorMessage = "Compilation errors";
            result.Errors = new List<ScriptError>();

            foreach (var diagnostic in compEx.Diagnostics)
            {
                var lineSpan = diagnostic.Location.GetLineSpan();
                result.Errors.Add(new ScriptError
                {
                    Line = lineSpan.StartLinePosition.Line + 1,
                    Column = lineSpan.StartLinePosition.Character + 1,
                    Message = diagnostic.GetMessage(),
                    Severity = diagnostic.Severity.ToString()
                });
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Output = outputCapture.ToString();
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        return result;
    }

    public void AllNotesOff()
    {
        if (_scriptHost != null)
        {
            _scriptHost.ClearState();
        }
    }

    public void SetBpm(double bpm)
    {
        if (_sequencer != null)
        {
            _sequencer.Bpm = bpm;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _sequencer?.Stop();
        _engine?.Dispose();

        _disposed = true;
    }
}

public class ScriptResult
{
    public bool Success { get; set; }
    public string? Output { get; set; }
    public string? ErrorMessage { get; set; }
    public List<ScriptError> Errors { get; set; } = new();
}

public class ScriptError
{
    public int Line { get; set; }
    public int Column { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "Error";
}
