// MusicEngineEditor - Audio Engine Service
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MusicEngine.Core;

namespace MusicEngineEditor.Services;

/// <summary>
/// Singleton service for managing the AudioEngine, including initialization,
/// synth management, effects, and mixer channels.
/// </summary>
public sealed class AudioEngineService : IDisposable
{
    #region Singleton

    private static readonly Lazy<AudioEngineService> _instance = new(
        () => new AudioEngineService(),
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Gets the singleton instance of the AudioEngineService.
    /// </summary>
    public static AudioEngineService Instance => _instance.Value;

    #endregion

    #region Private Fields

    private AudioEngine? _audioEngine;
    private Sequencer? _sequencer;
    private readonly object _lock = new();
    private bool _disposed;
    private bool _isInitialized;

    private int _sampleRate = 44100;
    private int _bufferSize = 512;
    private string? _selectedOutputDevice;

    // Track created synths for management
    private readonly Dictionary<string, object> _synths = new();
    private readonly Dictionary<string, object> _effects = new();

    #endregion

    #region Properties

    /// <summary>
    /// Gets whether the audio engine has been initialized.
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// Gets the AudioEngine instance.
    /// </summary>
    public AudioEngine? AudioEngine => _audioEngine;

    /// <summary>
    /// Gets the Sequencer instance.
    /// </summary>
    public Sequencer? Sequencer => _sequencer;

    /// <summary>
    /// Gets or sets the sample rate.
    /// </summary>
    public int SampleRate
    {
        get => _sampleRate;
        set
        {
            if (value != _sampleRate && (value == 44100 || value == 48000 || value == 88200 || value == 96000))
            {
                _sampleRate = value;
                SampleRateChanged?.Invoke(this, value);
            }
        }
    }

    /// <summary>
    /// Gets or sets the buffer size.
    /// </summary>
    public int BufferSize
    {
        get => _bufferSize;
        set
        {
            if (value != _bufferSize && value >= 64 && value <= 4096)
            {
                _bufferSize = value;
                BufferSizeChanged?.Invoke(this, value);
            }
        }
    }

    /// <summary>
    /// Gets or sets the selected audio output device.
    /// </summary>
    public string? SelectedOutputDevice
    {
        get => _selectedOutputDevice;
        set
        {
            if (value != _selectedOutputDevice)
            {
                _selectedOutputDevice = value;
                OutputDeviceChanged?.Invoke(this, value);
            }
        }
    }

    /// <summary>
    /// Gets the current master volume (0.0 to 1.0).
    /// </summary>
    public float MasterVolume { get; private set; } = 1.0f;

    /// <summary>
    /// Gets the list of available audio output devices.
    /// </summary>
    public IReadOnlyList<AudioDeviceInfo> AvailableOutputDevices => _availableDevices;

    private readonly List<AudioDeviceInfo> _availableDevices = new();

    #endregion

    #region Events

    /// <summary>
    /// Raised when the audio engine is initialized.
    /// </summary>
    public event EventHandler? Initialized;

    /// <summary>
    /// Raised when the audio engine is disposed.
    /// </summary>
    public event EventHandler? Disposed;

    /// <summary>
    /// Raised when the sample rate changes.
    /// </summary>
    public event EventHandler<int>? SampleRateChanged;

    /// <summary>
    /// Raised when the buffer size changes.
    /// </summary>
    public event EventHandler<int>? BufferSizeChanged;

    /// <summary>
    /// Raised when the output device changes.
    /// </summary>
    public event EventHandler<string?>? OutputDeviceChanged;

    /// <summary>
    /// Raised when an audio device error occurs.
    /// </summary>
    public event EventHandler<AudioDeviceErrorEventArgs>? DeviceError;

    #endregion

    #region Constructor

    private AudioEngineService()
    {
        // Private constructor for singleton
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Initializes the audio engine asynchronously.
    /// </summary>
    /// <param name="sampleRate">Optional sample rate override.</param>
    /// <returns>A task representing the initialization operation.</returns>
    public async Task InitializeAsync(int? sampleRate = null)
    {
        ThrowIfDisposed();

        // Check for Safe Mode - skip audio initialization
        if (App.SafeMode)
        {
            System.Diagnostics.Debug.WriteLine("AudioEngineService: Safe Mode - skipping initialization");
            return;
        }

        if (_isInitialized)
        {
            return;
        }

        await Task.Run(() =>
        {
            lock (_lock)
            {
                if (_isInitialized)
                {
                    return;
                }

                try
                {
                    // Set sample rate if provided
                    if (sampleRate.HasValue)
                    {
                        _sampleRate = sampleRate.Value;
                    }

                    // Create and initialize the audio engine
                    _audioEngine = new AudioEngine(sampleRate: _sampleRate, logger: null);
                    _audioEngine.Initialize();

                    // Create the sequencer
                    _sequencer = new Sequencer();
                    _sequencer.Start();

                    // Enumerate available devices
                    EnumerateAudioDevices();

                    _isInitialized = true;
                }
                catch (Exception ex)
                {
                    DeviceError?.Invoke(this, new AudioDeviceErrorEventArgs(
                        "Initialization failed",
                        ex.Message,
                        ex));
                    throw;
                }
            }
        });

        // Initialize PlaybackService with our engine
        PlaybackService.Instance.Initialize(_audioEngine!, _sequencer!);

        Initialized?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Reinitializes the audio engine with new settings.
    /// </summary>
    /// <param name="sampleRate">The new sample rate.</param>
    /// <param name="bufferSize">The new buffer size.</param>
    public async Task ReinitializeAsync(int? sampleRate = null, int? bufferSize = null)
    {
        ThrowIfDisposed();

        // Stop playback
        PlaybackService.Instance.Stop();

        // Dispose current engine
        lock (_lock)
        {
            _sequencer?.Stop();
            _audioEngine?.Dispose();

            _audioEngine = null;
            _sequencer = null;
            _isInitialized = false;

            if (sampleRate.HasValue)
            {
                _sampleRate = sampleRate.Value;
            }

            if (bufferSize.HasValue)
            {
                _bufferSize = bufferSize.Value;
            }
        }

        // Reinitialize
        await InitializeAsync(_sampleRate);
    }

    #endregion

    #region Device Management

    /// <summary>
    /// Enumerates available audio output devices.
    /// </summary>
    public void EnumerateAudioDevices()
    {
        _availableDevices.Clear();

        try
        {
            // Use NAudio for device enumeration
            for (int i = 0; i < NAudio.Wave.WaveOut.DeviceCount; i++)
            {
                var caps = NAudio.Wave.WaveOut.GetCapabilities(i);
                _availableDevices.Add(new AudioDeviceInfo
                {
                    Id = i.ToString(),
                    Name = caps.ProductName,
                    Channels = caps.Channels,
                    IsDefault = i == 0
                });
            }

            // Add default device if list is empty
            if (_availableDevices.Count == 0)
            {
                _availableDevices.Add(new AudioDeviceInfo
                {
                    Id = "-1",
                    Name = "Default Audio Device",
                    Channels = 2,
                    IsDefault = true
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error enumerating audio devices: {ex.Message}");

            // Add fallback device
            _availableDevices.Add(new AudioDeviceInfo
            {
                Id = "-1",
                Name = "Default Audio Device",
                Channels = 2,
                IsDefault = true
            });
        }
    }

    /// <summary>
    /// Selects an audio output device by ID.
    /// </summary>
    /// <param name="deviceId">The device ID to select.</param>
    /// <returns>True if the device was selected successfully.</returns>
    public bool SelectOutputDevice(string deviceId)
    {
        ThrowIfDisposed();

        var device = _availableDevices.Find(d => d.Id == deviceId);
        if (device == null)
        {
            return false;
        }

        SelectedOutputDevice = device.Name;

        // Note: Changing output device typically requires reinitializing the engine
        // This would need to be handled based on the specific AudioEngine implementation

        return true;
    }

    #endregion

    #region Synth Management

    /// <summary>
    /// Creates a new synth instance.
    /// </summary>
    /// <param name="name">Optional name for the synth.</param>
    /// <returns>The created synth, or null if creation failed.</returns>
    public object? CreateSynth(string? name = null)
    {
        ThrowIfDisposed();

        if (_audioEngine == null)
        {
            throw new InvalidOperationException("AudioEngine is not initialized.");
        }

        try
        {
            // AudioEngine doesn't have a built-in CreateSynth method.
            // Use LoadVstPlugin to load a VST instrument instead.
            // For now, return null as no default synth is available.
            System.Diagnostics.Debug.WriteLine("CreateSynth: No built-in synth available. Use LoadVstPlugin to load a VST instrument.");
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error creating synth: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets a synth by name.
    /// </summary>
    /// <param name="name">The synth name.</param>
    /// <returns>The synth, or null if not found.</returns>
    public object? GetSynth(string name)
    {
        return _synths.TryGetValue(name, out var synth) ? synth : null;
    }

    /// <summary>
    /// Removes a synth by name.
    /// </summary>
    /// <param name="name">The synth name.</param>
    /// <returns>True if the synth was removed.</returns>
    public bool RemoveSynth(string name)
    {
        return _synths.Remove(name);
    }

    /// <summary>
    /// Gets all registered synth names.
    /// </summary>
    public IEnumerable<string> GetSynthNames()
    {
        return _synths.Keys;
    }

    #endregion

    #region Volume Control

    /// <summary>
    /// Sets the master volume.
    /// </summary>
    /// <param name="volume">Volume level (0.0 to 1.0).</param>
    public void SetMasterVolume(float volume)
    {
        ThrowIfDisposed();

        MasterVolume = Math.Clamp(volume, 0f, 1f);

        // Apply to engine if available
        // This depends on the AudioEngine implementation
    }

    #endregion

    #region Note Playback (Preview)

    /// <summary>
    /// Plays a note for preview purposes.
    /// </summary>
    /// <param name="midiNote">The MIDI note number (0-127).</param>
    /// <param name="velocity">The velocity (0-127).</param>
    /// <param name="synthName">Optional synth name to use.</param>
    public void PlayNotePreview(int midiNote, int velocity, string? synthName = null)
    {
        ThrowIfDisposed();

        // Get or create a preview synth
        object? synth = null;

        if (!string.IsNullOrEmpty(synthName))
        {
            synth = GetSynth(synthName);
        }

        if (synth == null && _synths.Count > 0)
        {
            // Use first available synth
            foreach (var kvp in _synths)
            {
                synth = kvp.Value;
                break;
            }
        }

        if (synth == null)
        {
            // Create a preview synth
            synth = CreateSynth("_preview");
        }

        // Send note on - this depends on the synth interface
        // Assuming it has a NoteOn method
        var synthType = synth?.GetType();
        var noteOnMethod = synthType?.GetMethod("NoteOn");
        noteOnMethod?.Invoke(synth, new object[] { midiNote, velocity });
    }

    /// <summary>
    /// Stops a preview note.
    /// </summary>
    /// <param name="midiNote">The MIDI note number to stop.</param>
    /// <param name="synthName">Optional synth name.</param>
    public void StopNotePreview(int midiNote, string? synthName = null)
    {
        ThrowIfDisposed();

        object? synth = null;

        if (!string.IsNullOrEmpty(synthName))
        {
            synth = GetSynth(synthName);
        }

        if (synth == null)
        {
            synth = GetSynth("_preview");
        }

        if (synth == null && _synths.Count > 0)
        {
            foreach (var kvp in _synths)
            {
                synth = kvp.Value;
                break;
            }
        }

        // Send note off
        var synthType = synth?.GetType();
        var noteOffMethod = synthType?.GetMethod("NoteOff");
        noteOffMethod?.Invoke(synth, new object[] { midiNote });
    }

    /// <summary>
    /// Stops all notes (panic/all notes off).
    /// </summary>
    public void AllNotesOff()
    {
        ThrowIfDisposed();

        foreach (var synth in _synths.Values)
        {
            try
            {
                var synthType = synth.GetType();
                var allNotesOffMethod = synthType.GetMethod("AllNotesOff");
                allNotesOffMethod?.Invoke(synth, null);
            }
            catch
            {
                // Ignore errors during panic
            }
        }
    }

    #endregion

    #region Helper Methods

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AudioEngineService));
        }
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes the AudioEngineService.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Stop sequencer
            _sequencer?.Stop();

            // Dispose audio engine
            _audioEngine?.Dispose();

            // Clear collections
            _synths.Clear();
            _effects.Clear();
            _availableDevices.Clear();

            _audioEngine = null;
            _sequencer = null;
            _isInitialized = false;

            Disposed?.Invoke(this, EventArgs.Empty);
        }
    }

    #endregion
}

#region Supporting Classes

/// <summary>
/// Information about an audio device.
/// </summary>
public class AudioDeviceInfo
{
    /// <summary>
    /// Gets or sets the device ID.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the device name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of channels.
    /// </summary>
    public int Channels { get; set; }

    /// <summary>
    /// Gets or sets whether this is the default device.
    /// </summary>
    public bool IsDefault { get; set; }

    public override string ToString() => Name;
}

/// <summary>
/// Event arguments for audio device errors.
/// </summary>
public class AudioDeviceErrorEventArgs : EventArgs
{
    /// <summary>
    /// Gets the error type.
    /// </summary>
    public string ErrorType { get; }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the exception if available.
    /// </summary>
    public Exception? Exception { get; }

    public AudioDeviceErrorEventArgs(string errorType, string message, Exception? exception = null)
    {
        ErrorType = errorType;
        Message = message;
        Exception = exception;
    }
}

#endregion
