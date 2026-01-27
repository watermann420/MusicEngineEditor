// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Audio-related commands for the editor.

using System;
using MusicEngine.Core;
using MusicEngine.Core.UndoRedo;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Commands;

/// <summary>
/// Command for reversing an audio clip in place.
/// </summary>
public sealed class ReverseAudioCommand : IUndoableCommand
{
    private readonly ClipViewModel _clip;
    private readonly float[]? _originalWaveformData;
#pragma warning disable CS0414 // Field is assigned but its value is never used
    private readonly bool _wasReversed;
#pragma warning restore CS0414

    /// <inheritdoc/>
    public string Description => $"Reverse Audio Clip \"{_clip.Name}\"";

    /// <summary>
    /// Creates a new ReverseAudioCommand.
    /// </summary>
    /// <param name="clip">The audio clip to reverse.</param>
    public ReverseAudioCommand(ClipViewModel clip)
    {
        _clip = clip ?? throw new ArgumentNullException(nameof(clip));

        if (clip.ClipType != ClipType.Audio)
        {
            throw new ArgumentException("Only audio clips can be reversed.", nameof(clip));
        }

        // Store original state for undo
        _originalWaveformData = clip.WaveformData != null
            ? (float[])clip.WaveformData.Clone()
            : null;
        _wasReversed = false; // Assume initial state is not reversed
    }

    /// <inheritdoc/>
    public void Execute()
    {
        ReverseWaveformData();
    }

    /// <inheritdoc/>
    public void Undo()
    {
        // Restore original waveform data
        if (_originalWaveformData != null)
        {
            _clip.WaveformData = (float[])_originalWaveformData.Clone();
        }
    }

    /// <summary>
    /// Reverses the waveform data of the clip in place.
    /// </summary>
    private void ReverseWaveformData()
    {
        if (_clip.WaveformData == null || _clip.WaveformData.Length == 0)
            return;

        var data = _clip.WaveformData;
        int length = data.Length;

        // Reverse the array in place
        for (int i = 0; i < length / 2; i++)
        {
            int j = length - 1 - i;
            (data[i], data[j]) = (data[j], data[i]);
        }

        // Trigger property change notification by reassigning
        _clip.WaveformData = data;
    }
}

/// <summary>
/// Command for normalizing an audio clip to a target peak level.
/// </summary>
public sealed class NormalizeAudioCommand : IUndoableCommand
{
    private readonly ClipViewModel _clip;
    private readonly double _oldGainDb;
    private readonly double _newGainDb;

    /// <inheritdoc/>
    public string Description => $"Normalize Audio Clip \"{_clip.Name}\"";

    /// <summary>
    /// Creates a new NormalizeAudioCommand.
    /// </summary>
    /// <param name="clip">The audio clip to normalize.</param>
    /// <param name="currentPeakDb">The current peak level in dB.</param>
    /// <param name="targetPeakDb">The target peak level in dB.</param>
    public NormalizeAudioCommand(ClipViewModel clip, double currentPeakDb, double targetPeakDb)
    {
        _clip = clip ?? throw new ArgumentNullException(nameof(clip));

        if (clip.ClipType != ClipType.Audio)
        {
            throw new ArgumentException("Only audio clips can be normalized.", nameof(clip));
        }

        _oldGainDb = clip.GainDb;
        // Calculate the gain adjustment needed to reach target peak
        _newGainDb = _oldGainDb + (targetPeakDb - currentPeakDb);
    }

    /// <inheritdoc/>
    public void Execute()
    {
        _clip.GainDb = _newGainDb;
    }

    /// <inheritdoc/>
    public void Undo()
    {
        _clip.GainDb = _oldGainDb;
    }
}

/// <summary>
/// Command for changing the gain of an audio clip.
/// </summary>
public sealed class AudioGainCommand : IUndoableCommand
{
    private readonly ClipViewModel _clip;
    private readonly double _oldGainDb;
    private readonly double _newGainDb;
    private readonly DateTime _timestamp;
    private static readonly TimeSpan MergeWindow = TimeSpan.FromMilliseconds(500);

    /// <inheritdoc/>
    public string Description => $"Change Audio Clip Gain to {_newGainDb:F1} dB";

    /// <summary>
    /// Creates a new AudioGainCommand.
    /// </summary>
    /// <param name="clip">The audio clip to modify.</param>
    /// <param name="newGainDb">The new gain value in dB.</param>
    public AudioGainCommand(ClipViewModel clip, double newGainDb)
    {
        _clip = clip ?? throw new ArgumentNullException(nameof(clip));

        if (clip.ClipType != ClipType.Audio)
        {
            throw new ArgumentException("Only audio clips have gain.", nameof(clip));
        }

        _oldGainDb = clip.GainDb;
        _newGainDb = newGainDb;
        _timestamp = DateTime.UtcNow;
    }

    private AudioGainCommand(ClipViewModel clip, double oldGainDb, double newGainDb, DateTime timestamp)
    {
        _clip = clip;
        _oldGainDb = oldGainDb;
        _newGainDb = newGainDb;
        _timestamp = timestamp;
    }

    /// <inheritdoc/>
    public void Execute()
    {
        _clip.GainDb = _newGainDb;
    }

    /// <inheritdoc/>
    public void Undo()
    {
        _clip.GainDb = _oldGainDb;
    }

    /// <inheritdoc/>
    public bool CanMergeWith(IUndoableCommand other)
    {
        return other is AudioGainCommand otherGain &&
               otherGain._clip == _clip &&
               DateTime.UtcNow - otherGain._timestamp < MergeWindow;
    }

    /// <inheritdoc/>
    public IUndoableCommand MergeWith(IUndoableCommand other)
    {
        if (other is AudioGainCommand otherGain)
        {
            return new AudioGainCommand(_clip, _oldGainDb, otherGain._newGainDb, _timestamp);
        }
        return this;
    }
}

/// <summary>
/// Command for setting fade-in on an audio clip.
/// </summary>
public sealed class AudioFadeInCommand : IUndoableCommand
{
    private readonly ClipViewModel _clip;
    private readonly double _oldFadeIn;
    private readonly double _newFadeIn;

    /// <inheritdoc/>
    public string Description => $"Set Fade In to {_newFadeIn:F2} beats";

    /// <summary>
    /// Creates a new AudioFadeInCommand.
    /// </summary>
    /// <param name="clip">The audio clip to modify.</param>
    /// <param name="newFadeInDuration">The new fade-in duration in beats.</param>
    public AudioFadeInCommand(ClipViewModel clip, double newFadeInDuration)
    {
        _clip = clip ?? throw new ArgumentNullException(nameof(clip));

        if (clip.ClipType != ClipType.Audio)
        {
            throw new ArgumentException("Only audio clips have fades.", nameof(clip));
        }

        _oldFadeIn = clip.FadeInDuration;
        _newFadeIn = Math.Max(0, Math.Min(newFadeInDuration, clip.Length / 2));
    }

    /// <inheritdoc/>
    public void Execute()
    {
        _clip.FadeInDuration = _newFadeIn;
    }

    /// <inheritdoc/>
    public void Undo()
    {
        _clip.FadeInDuration = _oldFadeIn;
    }
}

/// <summary>
/// Command for setting fade-out on an audio clip.
/// </summary>
public sealed class AudioFadeOutCommand : IUndoableCommand
{
    private readonly ClipViewModel _clip;
    private readonly double _oldFadeOut;
    private readonly double _newFadeOut;

    /// <inheritdoc/>
    public string Description => $"Set Fade Out to {_newFadeOut:F2} beats";

    /// <summary>
    /// Creates a new AudioFadeOutCommand.
    /// </summary>
    /// <param name="clip">The audio clip to modify.</param>
    /// <param name="newFadeOutDuration">The new fade-out duration in beats.</param>
    public AudioFadeOutCommand(ClipViewModel clip, double newFadeOutDuration)
    {
        _clip = clip ?? throw new ArgumentNullException(nameof(clip));

        if (clip.ClipType != ClipType.Audio)
        {
            throw new ArgumentException("Only audio clips have fades.", nameof(clip));
        }

        _oldFadeOut = clip.FadeOutDuration;
        _newFadeOut = Math.Max(0, Math.Min(newFadeOutDuration, clip.Length / 2));
    }

    /// <inheritdoc/>
    public void Execute()
    {
        _clip.FadeOutDuration = _newFadeOut;
    }

    /// <inheritdoc/>
    public void Undo()
    {
        _clip.FadeOutDuration = _oldFadeOut;
    }
}

/// <summary>
/// Command for splitting an audio clip at a specified position.
/// </summary>
public sealed class SplitAudioClipCommand : IUndoableCommand
{
    private readonly ClipViewModel _originalClip;
    private readonly double _splitPosition;
    private readonly double _originalLength;
    private readonly double _originalSourceOffset;
    private ClipViewModel? _rightClip;

    /// <summary>
    /// Event raised when the split creates a new clip that should be added to the arrangement.
    /// </summary>
    public event EventHandler<ClipViewModel>? ClipCreated;

    /// <inheritdoc/>
    public string Description => $"Split Audio Clip at {_splitPosition:F2}";

    /// <summary>
    /// Creates a new SplitAudioClipCommand.
    /// </summary>
    /// <param name="clip">The audio clip to split.</param>
    /// <param name="splitPosition">The position within the clip to split at (in beats from clip start).</param>
    public SplitAudioClipCommand(ClipViewModel clip, double splitPosition)
    {
        _originalClip = clip ?? throw new ArgumentNullException(nameof(clip));

        if (clip.ClipType != ClipType.Audio)
        {
            throw new ArgumentException("Only audio clips can be split.", nameof(clip));
        }

        if (splitPosition <= 0 || splitPosition >= clip.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(splitPosition),
                "Split position must be within the clip bounds.");
        }

        _splitPosition = splitPosition;
        _originalLength = clip.Length;
        _originalSourceOffset = clip.SourceOffset;
    }

    /// <inheritdoc/>
    public void Execute()
    {
        // Create the right clip (portion after split)
        _rightClip = new ClipViewModel(ClipType.Audio)
        {
            Name = _originalClip.Name + " (R)",
            StartPosition = _originalClip.StartPosition + _splitPosition,
            Length = _originalLength - _splitPosition,
            TrackIndex = _originalClip.TrackIndex,
            Color = _originalClip.Color,
            FilePath = _originalClip.FilePath,
            SourceOffset = _originalSourceOffset + _splitPosition,
            GainDb = _originalClip.GainDb,
            FadeOutDuration = _originalClip.FadeOutDuration // Keep original fade-out on right clip
        };

        // Trim the original clip (becomes the left portion)
        _originalClip.Length = _splitPosition;
        _originalClip.FadeOutDuration = 0; // Remove fade-out from left clip

        // Notify that a new clip was created
        ClipCreated?.Invoke(this, _rightClip);
    }

    /// <inheritdoc/>
    public void Undo()
    {
        // Restore original clip properties
        _originalClip.Length = _originalLength;
        _originalClip.SourceOffset = _originalSourceOffset;
        _originalClip.FadeOutDuration = _rightClip?.FadeOutDuration ?? 0;

        // The right clip should be removed by the arrangement view
        _rightClip = null;
    }

    /// <summary>
    /// Gets the right clip created by the split operation.
    /// </summary>
    public ClipViewModel? RightClip => _rightClip;
}
