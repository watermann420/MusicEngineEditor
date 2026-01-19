using System;
using System.Threading.Tasks;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Services;

/// <summary>
/// Service for executing MusicEngine scripts
/// </summary>
public interface IScriptExecutionService
{
    bool IsRunning { get; }

    event EventHandler<string>? OutputReceived;
    event EventHandler<CompilationError>? ErrorOccurred;
    event EventHandler? ExecutionStarted;
    event EventHandler? ExecutionStopped;

    Task<CompilationResult> CompileProjectAsync(MusicProject project);
    Task RunAsync(MusicProject project);
    Task StopAsync();
}

/// <summary>
/// Result of compilation
/// </summary>
public class CompilationResult
{
    public bool Success { get; set; }
    public System.Collections.Generic.List<CompilationError> Errors { get; set; } = new();
}

/// <summary>
/// Compilation or runtime error
/// </summary>
public class CompilationError
{
    public string Message { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public int Line { get; set; }
    public int Column { get; set; }
    public ErrorSeverity Severity { get; set; } = ErrorSeverity.Error;
}

public enum ErrorSeverity
{
    Info,
    Warning,
    Error
}
