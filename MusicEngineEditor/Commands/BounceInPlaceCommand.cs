// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Bounce in place functionality.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MusicEngine.Core;
using MusicEngine.Core.UndoRedo;
using NAudio.Wave;

namespace MusicEngineEditor.Commands;

/// <summary>
/// Options for the bounce in place operation.
/// </summary>
public class BounceInPlaceOptions
{
    /// <summary>Whether to include effects in the bounce.</summary>
    public bool IncludeEffects { get; set; } = true;

    /// <summary>Whether to include sends in the bounce.</summary>
    public bool IncludeSends { get; set; } = false;

    /// <summary>Tail length in milliseconds for reverb/delay tails.</summary>
    public int TailLengthMs { get; set; } = 1000;

    /// <summary>Output bit depth (16, 24, or 32).</summary>
    public int BitDepth { get; set; } = 24;

    /// <summary>Whether to replace the original clip/track or create a new track.</summary>
    public bool ReplaceOriginal { get; set; } = true;

    /// <summary>Whether to mute the original after bouncing (if not replacing).</summary>
    public bool MuteOriginal { get; set; } = true;

    /// <summary>Whether to normalize the output.</summary>
    public bool Normalize { get; set; } = false;

    /// <summary>Target peak level for normalization in dB.</summary>
    public double NormalizePeakDb { get; set; } = -1.0;

    /// <summary>Sample rate for the bounced audio (0 = use project sample rate).</summary>
    public int SampleRate { get; set; } = 0;

    /// <summary>Start position in beats (null = use selection/clip start).</summary>
    public double? StartPosition { get; set; }

    /// <summary>End position in beats (null = use selection/clip end).</summary>
    public double? EndPosition { get; set; }
}

/// <summary>
/// Result of a bounce in place operation.
/// </summary>
public class BounceResult
{
    /// <summary>Whether the bounce was successful.</summary>
    public bool Success { get; set; }

    /// <summary>Path to the bounced audio file.</summary>
    public string? OutputPath { get; set; }

    /// <summary>Duration of the bounced audio in seconds.</summary>
    public double Duration { get; set; }

    /// <summary>Peak level in dB.</summary>
    public double PeakLevel { get; set; }

    /// <summary>Error message if bounce failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>The created audio clip (if successful).</summary>
    public AudioClip? CreatedClip { get; set; }
}

/// <summary>
/// Progress information for bounce operation.
/// </summary>
public class BounceProgressEventArgs : EventArgs
{
    /// <summary>Current progress (0.0 to 1.0).</summary>
    public double Progress { get; }

    /// <summary>Current operation description.</summary>
    public string Status { get; }

    public BounceProgressEventArgs(double progress, string status)
    {
        Progress = progress;
        Status = status;
    }
}

/// <summary>
/// Command for bouncing (rendering) selected audio region to a new audio file.
/// Supports undo by restoring original state.
/// </summary>
public sealed class BounceInPlaceCommand : IUndoableCommand
{
    private readonly Sequencer _sequencer;
    private readonly BounceInPlaceOptions _options;
    private readonly int _trackIndex;
    private readonly Guid? _clipId;

    // State for undo
    private AudioClip? _originalClip;
    private AudioClip? _bouncedClip;
#pragma warning disable CS0414 // Field is assigned but its value is never used
    private bool _wasTrackMuted;
#pragma warning restore CS0414
    private string? _bouncedFilePath;
    private bool _executed;

    /// <inheritdoc/>
    public string Description => _clipId.HasValue
        ? "Bounce Clip In Place"
        : "Bounce Track In Place";

    /// <summary>
    /// Event raised during bounce progress.
    /// </summary>
    public event EventHandler<BounceProgressEventArgs>? ProgressChanged;

    /// <summary>
    /// Creates a new bounce in place command for a track.
    /// </summary>
    /// <param name="sequencer">The sequencer containing the arrangement.</param>
    /// <param name="trackIndex">Track index to bounce.</param>
    /// <param name="options">Bounce options.</param>
    public BounceInPlaceCommand(Sequencer sequencer, int trackIndex, BounceInPlaceOptions? options = null)
    {
        _sequencer = sequencer ?? throw new ArgumentNullException(nameof(sequencer));
        _trackIndex = trackIndex;
        _options = options ?? new BounceInPlaceOptions();
    }

    /// <summary>
    /// Creates a new bounce in place command for a specific clip.
    /// </summary>
    /// <param name="sequencer">The sequencer containing the arrangement.</param>
    /// <param name="trackIndex">Track index containing the clip.</param>
    /// <param name="clipId">ID of the clip to bounce.</param>
    /// <param name="options">Bounce options.</param>
    public BounceInPlaceCommand(Sequencer sequencer, int trackIndex, Guid clipId, BounceInPlaceOptions? options = null)
        : this(sequencer, trackIndex, options)
    {
        _clipId = clipId;
    }

    /// <inheritdoc/>
    public void Execute()
    {
        if (_executed)
        {
            Redo();
            return;
        }

        ExecuteAsync(CancellationToken.None).GetAwaiter().GetResult();
        _executed = true;
    }

    /// <summary>
    /// Executes the bounce operation asynchronously with progress reporting.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Bounce result.</returns>
    public async Task<BounceResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var result = new BounceResult();

        try
        {
            ReportProgress(0, "Preparing bounce...");

            // Get source information
            var arrangement = _sequencer.Arrangement;
            if (arrangement == null)
            {
                result.ErrorMessage = "No arrangement found.";
                return result;
            }

            // Determine bounce range
            double startBeat = _options.StartPosition ?? 0;
            double endBeat = _options.EndPosition ?? arrangement.TotalLengthWithClips;

            if (_clipId.HasValue)
            {
                var clip = arrangement.GetAudioClip(_clipId.Value);
                if (clip == null)
                {
                    result.ErrorMessage = "Clip not found.";
                    return result;
                }

                _originalClip = clip;
                startBeat = clip.StartPosition;
                endBeat = clip.EndPosition;
            }

            // Calculate duration
            double bpm = _sequencer.Bpm > 0 ? _sequencer.Bpm : 120;
            double durationBeats = endBeat - startBeat;
            double durationSeconds = (durationBeats / bpm) * 60.0;

            // Add tail
            double tailSeconds = _options.TailLengthMs / 1000.0;
            double totalDurationSeconds = durationSeconds + tailSeconds;

            ReportProgress(0.1, "Rendering audio...");

            // Get sample rate
            int sampleRate = _options.SampleRate > 0 ? _options.SampleRate : _sequencer.SampleRate;
            if (sampleRate <= 0) sampleRate = 44100;

            // Create output file path
            string projectDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string audioDir = Path.Combine(projectDir, "MusicEngine", "Audio", "Bounced");
            Directory.CreateDirectory(audioDir);

            string fileName = $"Bounce_{DateTime.Now:yyyyMMdd_HHmmss}_{_trackIndex}.wav";
            _bouncedFilePath = Path.Combine(audioDir, fileName);

            // Render audio
            int totalSamples = (int)(totalDurationSeconds * sampleRate);
            int channels = 2;
            float[] buffer = new float[totalSamples * channels];

            // Store track mute state (track mute state is managed at arrangement level)
            _wasTrackMuted = false; // TODO: Implement track mute state tracking when arrangement supports it

            // Configure render (solo track if needed)
            // Note: Track solo/mute is not yet implemented in the core engine

            // Simulate rendering (in real implementation, this would render from the engine)
            await Task.Run(() =>
            {
                int processed = 0;
                int blockSize = 4096;

                while (processed < totalSamples && !cancellationToken.IsCancellationRequested)
                {
                    int remaining = totalSamples - processed;
                    int toProcess = Math.Min(blockSize, remaining);

                    // In real implementation: _engine.RenderBlock(buffer, processed * channels, toProcess * channels)
                    // For now, fill with silence
                    for (int i = 0; i < toProcess * channels; i++)
                    {
                        buffer[processed * channels + i] = 0f;
                    }

                    processed += toProcess;
                    double progress = 0.1 + (0.6 * processed / totalSamples);
                    ReportProgress(progress, $"Rendering... {(int)(progress * 100)}%");
                }
            }, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                result.ErrorMessage = "Bounce cancelled.";
                return result;
            }

            ReportProgress(0.7, "Normalizing...");

            // Find peak level
            float peak = 0;
            for (int i = 0; i < buffer.Length; i++)
            {
                float abs = Math.Abs(buffer[i]);
                if (abs > peak) peak = abs;
            }

            result.PeakLevel = peak > 0 ? 20 * Math.Log10(peak) : double.NegativeInfinity;

            // Normalize if requested
            if (_options.Normalize && peak > 0)
            {
                float targetPeak = (float)Math.Pow(10, _options.NormalizePeakDb / 20.0);
                float gain = targetPeak / peak;

                for (int i = 0; i < buffer.Length; i++)
                {
                    buffer[i] *= gain;
                }

                result.PeakLevel = _options.NormalizePeakDb;
            }

            ReportProgress(0.85, "Writing file...");

            // Write WAV file
            await WriteWavFileAsync(_bouncedFilePath, buffer, sampleRate, channels, _options.BitDepth);

            ReportProgress(0.95, "Creating clip...");

            // Create new audio clip
            _bouncedClip = new AudioClip(_bouncedFilePath, startBeat, durationBeats, _trackIndex)
            {
                Name = $"Bounced {DateTime.Now:HH:mm:ss}"
            };

            // Apply changes based on options
            if (_options.ReplaceOriginal)
            {
                if (_originalClip != null)
                {
                    arrangement.RemoveAudioClip(_originalClip.Id);
                }
                arrangement.AddAudioClip(_bouncedClip);
            }
            else
            {
                // Add to a new track (use max track index + 1)
                int maxTrackIndex = arrangement.AudioClips.Count > 0
                    ? arrangement.AudioClips.Max(c => c.TrackIndex)
                    : -1;
                _bouncedClip.TrackIndex = maxTrackIndex + 1;
                arrangement.AddAudioClip(_bouncedClip);

                // Note: Track mute functionality would be implemented here when arrangement supports it
            }

            // Note: Track solo state would be restored here when arrangement supports it

            ReportProgress(1.0, "Complete");

            result.Success = true;
            result.OutputPath = _bouncedFilePath;
            result.Duration = totalDurationSeconds;
            result.CreatedClip = _bouncedClip;
            _executed = true;

            return result;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    /// <inheritdoc/>
    public void Undo()
    {
        if (!_executed) return;

        var arrangement = _sequencer.Arrangement;
        if (arrangement == null) return;

        // Remove bounced clip
        if (_bouncedClip != null)
        {
            arrangement.RemoveAudioClip(_bouncedClip.Id);
        }

        // Restore original clip if replaced
        if (_options.ReplaceOriginal && _originalClip != null)
        {
            arrangement.AddAudioClip(_originalClip);
        }

        // Note: Track mute state restoration would be implemented here when arrangement supports it

        // Optionally delete the bounced file
        if (_bouncedFilePath != null && File.Exists(_bouncedFilePath))
        {
            try
            {
                File.Delete(_bouncedFilePath);
            }
            catch
            {
                // Ignore file deletion errors
            }
        }
    }

    /// <inheritdoc/>
    public void Redo()
    {
        var arrangement = _sequencer.Arrangement;
        if (arrangement == null) return;

        // Remove original clip if replacing
        if (_options.ReplaceOriginal && _originalClip != null)
        {
            arrangement.RemoveAudioClip(_originalClip.Id);
        }

        // Re-add bounced clip
        if (_bouncedClip != null)
        {
            arrangement.AddAudioClip(_bouncedClip);
            // Note: Track mute functionality would be implemented here when arrangement supports it
        }
    }

    private void ReportProgress(double progress, string status)
    {
        ProgressChanged?.Invoke(this, new BounceProgressEventArgs(progress, status));
    }

    private static async Task WriteWavFileAsync(string path, float[] samples, int sampleRate, int channels, int bitDepth)
    {
        await Task.Run(() =>
        {
            var format = bitDepth switch
            {
                16 => new WaveFormat(sampleRate, 16, channels),
                24 => new WaveFormat(sampleRate, 24, channels),
                _ => WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels)
            };

            using var writer = new WaveFileWriter(path, format);

            if (bitDepth == 32)
            {
                writer.WriteSamples(samples, 0, samples.Length);
            }
            else
            {
                // Convert to appropriate bit depth
                for (int i = 0; i < samples.Length; i++)
                {
                    float sample = Math.Clamp(samples[i], -1.0f, 1.0f);

                    if (bitDepth == 16)
                    {
                        short s16 = (short)(sample * short.MaxValue);
                        writer.Write(BitConverter.GetBytes(s16), 0, 2);
                    }
                    else if (bitDepth == 24)
                    {
                        int s24 = (int)(sample * 8388607); // 2^23 - 1
                        byte[] bytes = BitConverter.GetBytes(s24);
                        writer.Write(bytes, 0, 3);
                    }
                }
            }
        });
    }
}

/// <summary>
/// Composite command for bouncing multiple clips or tracks.
/// </summary>
public sealed class BounceMultipleCommand : IUndoableCommand
{
    private readonly IUndoableCommand[] _commands;

    /// <inheritdoc/>
    public string Description => $"Bounce {_commands.Length} Items";

    public BounceMultipleCommand(params BounceInPlaceCommand[] commands)
    {
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
    }

    /// <inheritdoc/>
    public void Execute()
    {
        foreach (var cmd in _commands)
        {
            cmd.Execute();
        }
    }

    /// <inheritdoc/>
    public void Undo()
    {
        // Undo in reverse order
        for (int i = _commands.Length - 1; i >= 0; i--)
        {
            _commands[i].Undo();
        }
    }
}
