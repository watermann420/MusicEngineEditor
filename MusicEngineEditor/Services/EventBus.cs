using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using MusicEngineEditor.Controls;

namespace MusicEngineEditor.Services;

/// <summary>
/// Thread-safe singleton Event Bus / Mediator for real-time synchronization
/// between the Sequencer (audio thread) and UI components.
/// Uses WeakReferences to prevent memory leaks from event subscriptions.
/// </summary>
public sealed class EventBus : IDisposable
{
    #region Singleton

    private static readonly Lazy<EventBus> _instance = new(() => new EventBus(), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Gets the singleton instance of the EventBus.
    /// </summary>
    public static EventBus Instance => _instance.Value;

    private EventBus()
    {
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
    }

    #endregion

    #region Private Fields

    private readonly Dispatcher _dispatcher;
    private readonly object _lock = new();
    private bool _disposed;

    // Subscription dictionaries using WeakReferences
    private readonly ConcurrentDictionary<Guid, WeakReference<Action<BeatChangedEventArgs>>> _beatChangedSubscribers = new();
    private readonly ConcurrentDictionary<Guid, WeakReference<Action<PatternTriggeredEventArgs>>> _patternTriggeredSubscribers = new();
    private readonly ConcurrentDictionary<Guid, WeakReference<Action<NoteOnEventArgs>>> _noteOnSubscribers = new();
    private readonly ConcurrentDictionary<Guid, WeakReference<Action<NoteOffEventArgs>>> _noteOffSubscribers = new();
    private readonly ConcurrentDictionary<Guid, WeakReference<Action<PlaybackStartedEventArgs>>> _playbackStartedSubscribers = new();
    private readonly ConcurrentDictionary<Guid, WeakReference<Action<PlaybackStoppedEventArgs>>> _playbackStoppedSubscribers = new();
    private readonly ConcurrentDictionary<Guid, WeakReference<Action<BpmChangedEventArgs>>> _bpmChangedSubscribers = new();
    private readonly ConcurrentDictionary<Guid, WeakReference<Action<PatternAddedEventArgs>>> _patternAddedSubscribers = new();
    private readonly ConcurrentDictionary<Guid, WeakReference<Action<PatternRemovedEventArgs>>> _patternRemovedSubscribers = new();

    #endregion

    #region Initialization

    static EventBus()
    {
    }

    #endregion

    #region Event Args Classes

    /// <summary>
    /// Event arguments for BeatChanged event.
    /// </summary>
    public sealed class BeatChangedEventArgs : EventArgs
    {
        public double CurrentBeat { get; }
        public DateTime Timestamp { get; }

        public BeatChangedEventArgs(double currentBeat)
        {
            CurrentBeat = currentBeat;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Event arguments for PatternTriggered event.
    /// </summary>
    public sealed class PatternTriggeredEventArgs : EventArgs
    {
        public Pattern Pattern { get; }
        public int NoteIndex { get; }
        public DateTime Timestamp { get; }

        public PatternTriggeredEventArgs(Pattern pattern, int noteIndex)
        {
            Pattern = pattern;
            NoteIndex = noteIndex;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Event arguments for NoteOn event.
    /// </summary>
    public sealed class NoteOnEventArgs : EventArgs
    {
        public string NoteName { get; }
        public int Velocity { get; }
        public double Beat { get; }
        public int? MidiChannel { get; }
        public DateTime Timestamp { get; }

        public NoteOnEventArgs(string noteName, int velocity, double beat, int? midiChannel = null)
        {
            NoteName = noteName;
            Velocity = velocity;
            Beat = beat;
            MidiChannel = midiChannel;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Event arguments for NoteOff event.
    /// </summary>
    public sealed class NoteOffEventArgs : EventArgs
    {
        public string NoteName { get; }
        public double Beat { get; }
        public int? MidiChannel { get; }
        public DateTime Timestamp { get; }

        public NoteOffEventArgs(string noteName, double beat, int? midiChannel = null)
        {
            NoteName = noteName;
            Beat = beat;
            MidiChannel = midiChannel;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Event arguments for PlaybackStarted event.
    /// </summary>
    public sealed class PlaybackStartedEventArgs : EventArgs
    {
        public double StartBeat { get; }
        public double Bpm { get; }
        public DateTime Timestamp { get; }

        public PlaybackStartedEventArgs(double startBeat = 0, double bpm = 120)
        {
            StartBeat = startBeat;
            Bpm = bpm;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Event arguments for PlaybackStopped event.
    /// </summary>
    public sealed class PlaybackStoppedEventArgs : EventArgs
    {
        public double StoppedAtBeat { get; }
        public StopReason Reason { get; }
        public DateTime Timestamp { get; }

        public PlaybackStoppedEventArgs(double stoppedAtBeat = 0, StopReason reason = StopReason.UserRequested)
        {
            StoppedAtBeat = stoppedAtBeat;
            Reason = reason;
            Timestamp = DateTime.UtcNow;
        }

        public enum StopReason
        {
            UserRequested,
            EndOfSequence,
            Error
        }
    }

    /// <summary>
    /// Event arguments for BpmChanged event.
    /// </summary>
    public sealed class BpmChangedEventArgs : EventArgs
    {
        public double OldBpm { get; }
        public double NewBpm { get; }
        public DateTime Timestamp { get; }

        public BpmChangedEventArgs(double newBpm, double oldBpm = 0)
        {
            NewBpm = newBpm;
            OldBpm = oldBpm;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Event arguments for PatternAdded event.
    /// </summary>
    public sealed class PatternAddedEventArgs : EventArgs
    {
        public Pattern Pattern { get; }
        public int Index { get; }
        public DateTime Timestamp { get; }

        public PatternAddedEventArgs(Pattern pattern, int index = -1)
        {
            Pattern = pattern;
            Index = index;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Event arguments for PatternRemoved event.
    /// </summary>
    public sealed class PatternRemovedEventArgs : EventArgs
    {
        public Pattern Pattern { get; }
        public int FormerIndex { get; }
        public DateTime Timestamp { get; }

        public PatternRemovedEventArgs(Pattern pattern, int formerIndex = -1)
        {
            Pattern = pattern;
            FormerIndex = formerIndex;
            Timestamp = DateTime.UtcNow;
        }
    }

    #endregion

    #region Subscription Token

    /// <summary>
    /// Represents a subscription that can be disposed to unsubscribe.
    /// </summary>
    public sealed class SubscriptionToken : IDisposable
    {
        private readonly Action _unsubscribeAction;
        private bool _disposed;

        internal SubscriptionToken(Action unsubscribeAction)
        {
            _unsubscribeAction = unsubscribeAction;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _unsubscribeAction?.Invoke();
        }
    }

    #endregion

    #region Subscribe Methods

    /// <summary>
    /// Subscribes to BeatChanged events.
    /// </summary>
    /// <param name="handler">The callback action to invoke when the event fires.</param>
    /// <param name="dispatchToUiThread">Whether to dispatch the callback to the UI thread. Default is true.</param>
    /// <returns>A subscription token that can be disposed to unsubscribe.</returns>
    public SubscriptionToken SubscribeBeatChanged(Action<BeatChangedEventArgs> handler, bool dispatchToUiThread = true)
    {
        ThrowIfDisposed();
        var id = Guid.NewGuid();
        var wrappedHandler = dispatchToUiThread ? WrapForDispatcher(handler) : handler;
        _beatChangedSubscribers[id] = new WeakReference<Action<BeatChangedEventArgs>>(wrappedHandler);
        return new SubscriptionToken(() => _beatChangedSubscribers.TryRemove(id, out _));
    }

    /// <summary>
    /// Subscribes to PatternTriggered events.
    /// </summary>
    public SubscriptionToken SubscribePatternTriggered(Action<PatternTriggeredEventArgs> handler, bool dispatchToUiThread = true)
    {
        ThrowIfDisposed();
        var id = Guid.NewGuid();
        var wrappedHandler = dispatchToUiThread ? WrapForDispatcher(handler) : handler;
        _patternTriggeredSubscribers[id] = new WeakReference<Action<PatternTriggeredEventArgs>>(wrappedHandler);
        return new SubscriptionToken(() => _patternTriggeredSubscribers.TryRemove(id, out _));
    }

    /// <summary>
    /// Subscribes to NoteOn events.
    /// </summary>
    public SubscriptionToken SubscribeNoteOn(Action<NoteOnEventArgs> handler, bool dispatchToUiThread = true)
    {
        ThrowIfDisposed();
        var id = Guid.NewGuid();
        var wrappedHandler = dispatchToUiThread ? WrapForDispatcher(handler) : handler;
        _noteOnSubscribers[id] = new WeakReference<Action<NoteOnEventArgs>>(wrappedHandler);
        return new SubscriptionToken(() => _noteOnSubscribers.TryRemove(id, out _));
    }

    /// <summary>
    /// Subscribes to NoteOff events.
    /// </summary>
    public SubscriptionToken SubscribeNoteOff(Action<NoteOffEventArgs> handler, bool dispatchToUiThread = true)
    {
        ThrowIfDisposed();
        var id = Guid.NewGuid();
        var wrappedHandler = dispatchToUiThread ? WrapForDispatcher(handler) : handler;
        _noteOffSubscribers[id] = new WeakReference<Action<NoteOffEventArgs>>(wrappedHandler);
        return new SubscriptionToken(() => _noteOffSubscribers.TryRemove(id, out _));
    }

    /// <summary>
    /// Subscribes to PlaybackStarted events.
    /// </summary>
    public SubscriptionToken SubscribePlaybackStarted(Action<PlaybackStartedEventArgs> handler, bool dispatchToUiThread = true)
    {
        ThrowIfDisposed();
        var id = Guid.NewGuid();
        var wrappedHandler = dispatchToUiThread ? WrapForDispatcher(handler) : handler;
        _playbackStartedSubscribers[id] = new WeakReference<Action<PlaybackStartedEventArgs>>(wrappedHandler);
        return new SubscriptionToken(() => _playbackStartedSubscribers.TryRemove(id, out _));
    }

    /// <summary>
    /// Subscribes to PlaybackStopped events.
    /// </summary>
    public SubscriptionToken SubscribePlaybackStopped(Action<PlaybackStoppedEventArgs> handler, bool dispatchToUiThread = true)
    {
        ThrowIfDisposed();
        var id = Guid.NewGuid();
        var wrappedHandler = dispatchToUiThread ? WrapForDispatcher(handler) : handler;
        _playbackStoppedSubscribers[id] = new WeakReference<Action<PlaybackStoppedEventArgs>>(wrappedHandler);
        return new SubscriptionToken(() => _playbackStoppedSubscribers.TryRemove(id, out _));
    }

    /// <summary>
    /// Subscribes to BpmChanged events.
    /// </summary>
    public SubscriptionToken SubscribeBpmChanged(Action<BpmChangedEventArgs> handler, bool dispatchToUiThread = true)
    {
        ThrowIfDisposed();
        var id = Guid.NewGuid();
        var wrappedHandler = dispatchToUiThread ? WrapForDispatcher(handler) : handler;
        _bpmChangedSubscribers[id] = new WeakReference<Action<BpmChangedEventArgs>>(wrappedHandler);
        return new SubscriptionToken(() => _bpmChangedSubscribers.TryRemove(id, out _));
    }

    /// <summary>
    /// Subscribes to PatternAdded events.
    /// </summary>
    public SubscriptionToken SubscribePatternAdded(Action<PatternAddedEventArgs> handler, bool dispatchToUiThread = true)
    {
        ThrowIfDisposed();
        var id = Guid.NewGuid();
        var wrappedHandler = dispatchToUiThread ? WrapForDispatcher(handler) : handler;
        _patternAddedSubscribers[id] = new WeakReference<Action<PatternAddedEventArgs>>(wrappedHandler);
        return new SubscriptionToken(() => _patternAddedSubscribers.TryRemove(id, out _));
    }

    /// <summary>
    /// Subscribes to PatternRemoved events.
    /// </summary>
    public SubscriptionToken SubscribePatternRemoved(Action<PatternRemovedEventArgs> handler, bool dispatchToUiThread = true)
    {
        ThrowIfDisposed();
        var id = Guid.NewGuid();
        var wrappedHandler = dispatchToUiThread ? WrapForDispatcher(handler) : handler;
        _patternRemovedSubscribers[id] = new WeakReference<Action<PatternRemovedEventArgs>>(wrappedHandler);
        return new SubscriptionToken(() => _patternRemovedSubscribers.TryRemove(id, out _));
    }

    #endregion

    #region Publish Methods

    /// <summary>
    /// Publishes a BeatChanged event to all subscribers.
    /// Thread-safe: can be called from any thread (including audio thread).
    /// </summary>
    /// <param name="currentBeat">The current beat position.</param>
    public void PublishBeatChanged(double currentBeat)
    {
        if (_disposed) return;
        var args = new BeatChangedEventArgs(currentBeat);
        InvokeSubscribers(_beatChangedSubscribers, args);
    }

    /// <summary>
    /// Publishes a PatternTriggered event to all subscribers.
    /// Thread-safe: can be called from any thread (including audio thread).
    /// </summary>
    /// <param name="pattern">The pattern that was triggered.</param>
    /// <param name="noteIndex">The index of the note within the pattern.</param>
    public void PublishPatternTriggered(Pattern pattern, int noteIndex)
    {
        if (_disposed) return;
        var args = new PatternTriggeredEventArgs(pattern, noteIndex);
        InvokeSubscribers(_patternTriggeredSubscribers, args);
    }

    /// <summary>
    /// Publishes a NoteOn event to all subscribers.
    /// Thread-safe: can be called from any thread (including audio thread).
    /// </summary>
    /// <param name="noteName">The name of the note (e.g., "C4", "A#3").</param>
    /// <param name="velocity">The velocity (0-127).</param>
    /// <param name="beat">The beat at which the note occurred.</param>
    /// <param name="midiChannel">Optional MIDI channel (1-16).</param>
    public void PublishNoteOn(string noteName, int velocity, double beat, int? midiChannel = null)
    {
        if (_disposed) return;
        var args = new NoteOnEventArgs(noteName, velocity, beat, midiChannel);
        InvokeSubscribers(_noteOnSubscribers, args);
    }

    /// <summary>
    /// Publishes a NoteOff event to all subscribers.
    /// Thread-safe: can be called from any thread (including audio thread).
    /// </summary>
    /// <param name="noteName">The name of the note (e.g., "C4", "A#3").</param>
    /// <param name="beat">The beat at which the note-off occurred.</param>
    /// <param name="midiChannel">Optional MIDI channel (1-16).</param>
    public void PublishNoteOff(string noteName, double beat, int? midiChannel = null)
    {
        if (_disposed) return;
        var args = new NoteOffEventArgs(noteName, beat, midiChannel);
        InvokeSubscribers(_noteOffSubscribers, args);
    }

    /// <summary>
    /// Publishes a PlaybackStarted event to all subscribers.
    /// Thread-safe: can be called from any thread.
    /// </summary>
    /// <param name="startBeat">The beat position where playback started.</param>
    /// <param name="bpm">The current BPM.</param>
    public void PublishPlaybackStarted(double startBeat = 0, double bpm = 120)
    {
        if (_disposed) return;
        var args = new PlaybackStartedEventArgs(startBeat, bpm);
        InvokeSubscribers(_playbackStartedSubscribers, args);
    }

    /// <summary>
    /// Publishes a PlaybackStopped event to all subscribers.
    /// Thread-safe: can be called from any thread.
    /// </summary>
    /// <param name="stoppedAtBeat">The beat position where playback stopped.</param>
    /// <param name="reason">The reason playback stopped.</param>
    public void PublishPlaybackStopped(double stoppedAtBeat = 0, PlaybackStoppedEventArgs.StopReason reason = PlaybackStoppedEventArgs.StopReason.UserRequested)
    {
        if (_disposed) return;
        var args = new PlaybackStoppedEventArgs(stoppedAtBeat, reason);
        InvokeSubscribers(_playbackStoppedSubscribers, args);
    }

    /// <summary>
    /// Publishes a BpmChanged event to all subscribers.
    /// Thread-safe: can be called from any thread.
    /// </summary>
    /// <param name="newBpm">The new BPM value.</param>
    /// <param name="oldBpm">The previous BPM value.</param>
    public void PublishBpmChanged(double newBpm, double oldBpm = 0)
    {
        if (_disposed) return;
        var args = new BpmChangedEventArgs(newBpm, oldBpm);
        InvokeSubscribers(_bpmChangedSubscribers, args);
    }

    /// <summary>
    /// Publishes a PatternAdded event to all subscribers.
    /// Thread-safe: can be called from any thread.
    /// </summary>
    /// <param name="pattern">The pattern that was added.</param>
    /// <param name="index">The index at which the pattern was added.</param>
    public void PublishPatternAdded(Pattern pattern, int index = -1)
    {
        if (_disposed) return;
        var args = new PatternAddedEventArgs(pattern, index);
        InvokeSubscribers(_patternAddedSubscribers, args);
    }

    /// <summary>
    /// Publishes a PatternRemoved event to all subscribers.
    /// Thread-safe: can be called from any thread.
    /// </summary>
    /// <param name="pattern">The pattern that was removed.</param>
    /// <param name="formerIndex">The former index of the pattern.</param>
    public void PublishPatternRemoved(Pattern pattern, int formerIndex = -1)
    {
        if (_disposed) return;
        var args = new PatternRemovedEventArgs(pattern, formerIndex);
        InvokeSubscribers(_patternRemovedSubscribers, args);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Wraps a handler to dispatch it to the UI thread.
    /// </summary>
    private Action<T> WrapForDispatcher<T>(Action<T> handler) where T : EventArgs
    {
        return args =>
        {
            if (_dispatcher.CheckAccess())
            {
                // Already on UI thread
                try
                {
                    handler(args);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[EventBus] Handler exception: {ex.Message}");
                }
            }
            else
            {
                // Dispatch to UI thread asynchronously to avoid blocking audio thread
                _dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
                {
                    try
                    {
                        handler(args);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[EventBus] Handler exception: {ex.Message}");
                    }
                });
            }
        };
    }

    /// <summary>
    /// Invokes all subscribers for a given event type, handling dead weak references.
    /// </summary>
    private void InvokeSubscribers<T>(ConcurrentDictionary<Guid, WeakReference<Action<T>>> subscribers, T args) where T : EventArgs
    {
        var deadReferences = new List<Guid>();

        foreach (var kvp in subscribers)
        {
            if (kvp.Value.TryGetTarget(out var handler))
            {
                try
                {
                    handler(args);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[EventBus] Handler exception for {typeof(T).Name}: {ex.Message}");
                }
            }
            else
            {
                // Handler has been garbage collected
                deadReferences.Add(kvp.Key);
            }
        }

        // Clean up dead references
        foreach (var id in deadReferences)
        {
            subscribers.TryRemove(id, out _);
        }
    }

    /// <summary>
    /// Performs periodic cleanup of all dead weak references.
    /// </summary>
    private void CleanupDeadReferences(object? state)
    {
        if (_disposed) return;

        CleanupDictionary(_beatChangedSubscribers);
        CleanupDictionary(_patternTriggeredSubscribers);
        CleanupDictionary(_noteOnSubscribers);
        CleanupDictionary(_noteOffSubscribers);
        CleanupDictionary(_playbackStartedSubscribers);
        CleanupDictionary(_playbackStoppedSubscribers);
        CleanupDictionary(_bpmChangedSubscribers);
        CleanupDictionary(_patternAddedSubscribers);
        CleanupDictionary(_patternRemovedSubscribers);
    }

    /// <summary>
    /// Cleans up dead weak references from a dictionary.
    /// </summary>
    private static void CleanupDictionary<T>(ConcurrentDictionary<Guid, WeakReference<Action<T>>> dictionary)
    {
        var deadReferences = new List<Guid>();

        foreach (var kvp in dictionary)
        {
            if (!kvp.Value.TryGetTarget(out _))
            {
                deadReferences.Add(kvp.Key);
            }
        }

        foreach (var id in deadReferences)
        {
            dictionary.TryRemove(id, out _);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(EventBus));
        }
    }

    #endregion

    #region Statistics and Debugging

    /// <summary>
    /// Gets the current subscriber count for debugging purposes.
    /// </summary>
    public EventBusStatistics GetStatistics()
    {
        return new EventBusStatistics
        {
            BeatChangedSubscribers = CountActiveSubscribers(_beatChangedSubscribers),
            PatternTriggeredSubscribers = CountActiveSubscribers(_patternTriggeredSubscribers),
            NoteOnSubscribers = CountActiveSubscribers(_noteOnSubscribers),
            NoteOffSubscribers = CountActiveSubscribers(_noteOffSubscribers),
            PlaybackStartedSubscribers = CountActiveSubscribers(_playbackStartedSubscribers),
            PlaybackStoppedSubscribers = CountActiveSubscribers(_playbackStoppedSubscribers),
            BpmChangedSubscribers = CountActiveSubscribers(_bpmChangedSubscribers),
            PatternAddedSubscribers = CountActiveSubscribers(_patternAddedSubscribers),
            PatternRemovedSubscribers = CountActiveSubscribers(_patternRemovedSubscribers)
        };
    }

    private static int CountActiveSubscribers<T>(ConcurrentDictionary<Guid, WeakReference<Action<T>>> dictionary)
    {
        int count = 0;
        foreach (var kvp in dictionary)
        {
            if (kvp.Value.TryGetTarget(out _))
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Statistics about EventBus subscribers.
    /// </summary>
    public sealed class EventBusStatistics
    {
        public int BeatChangedSubscribers { get; init; }
        public int PatternTriggeredSubscribers { get; init; }
        public int NoteOnSubscribers { get; init; }
        public int NoteOffSubscribers { get; init; }
        public int PlaybackStartedSubscribers { get; init; }
        public int PlaybackStoppedSubscribers { get; init; }
        public int BpmChangedSubscribers { get; init; }
        public int PatternAddedSubscribers { get; init; }
        public int PatternRemovedSubscribers { get; init; }

        public int TotalSubscribers =>
            BeatChangedSubscribers + PatternTriggeredSubscribers + NoteOnSubscribers +
            NoteOffSubscribers + PlaybackStartedSubscribers + PlaybackStoppedSubscribers +
            BpmChangedSubscribers + PatternAddedSubscribers + PatternRemovedSubscribers;

        public override string ToString()
        {
            return $"EventBus Statistics: {TotalSubscribers} total subscribers\n" +
                   $"  BeatChanged: {BeatChangedSubscribers}\n" +
                   $"  PatternTriggered: {PatternTriggeredSubscribers}\n" +
                   $"  NoteOn: {NoteOnSubscribers}\n" +
                   $"  NoteOff: {NoteOffSubscribers}\n" +
                   $"  PlaybackStarted: {PlaybackStartedSubscribers}\n" +
                   $"  PlaybackStopped: {PlaybackStoppedSubscribers}\n" +
                   $"  BpmChanged: {BpmChangedSubscribers}\n" +
                   $"  PatternAdded: {PatternAddedSubscribers}\n" +
                   $"  PatternRemoved: {PatternRemovedSubscribers}";
        }
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Clears all subscriptions and disposes the EventBus.
    /// Note: As a singleton, this should typically only be called during application shutdown.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;

            _beatChangedSubscribers.Clear();
            _patternTriggeredSubscribers.Clear();
            _noteOnSubscribers.Clear();
            _noteOffSubscribers.Clear();
            _playbackStartedSubscribers.Clear();
            _playbackStoppedSubscribers.Clear();
            _bpmChangedSubscribers.Clear();
            _patternAddedSubscribers.Clear();
            _patternRemovedSubscribers.Clear();
        }
    }

    #endregion
}

#region Extension Methods

/// <summary>
/// Extension methods for easier EventBus usage.
/// </summary>
public static class EventBusExtensions
{
    /// <summary>
    /// Subscribes to multiple event types with a single disposable.
    /// </summary>
    public static IDisposable SubscribeAll(
        this EventBus eventBus,
        Action<EventBus.BeatChangedEventArgs>? onBeatChanged = null,
        Action<EventBus.PatternTriggeredEventArgs>? onPatternTriggered = null,
        Action<EventBus.NoteOnEventArgs>? onNoteOn = null,
        Action<EventBus.NoteOffEventArgs>? onNoteOff = null,
        Action<EventBus.PlaybackStartedEventArgs>? onPlaybackStarted = null,
        Action<EventBus.PlaybackStoppedEventArgs>? onPlaybackStopped = null,
        Action<EventBus.BpmChangedEventArgs>? onBpmChanged = null,
        Action<EventBus.PatternAddedEventArgs>? onPatternAdded = null,
        Action<EventBus.PatternRemovedEventArgs>? onPatternRemoved = null,
        bool dispatchToUiThread = true)
    {
        var subscriptions = new List<EventBus.SubscriptionToken>();

        if (onBeatChanged != null)
            subscriptions.Add(eventBus.SubscribeBeatChanged(onBeatChanged, dispatchToUiThread));
        if (onPatternTriggered != null)
            subscriptions.Add(eventBus.SubscribePatternTriggered(onPatternTriggered, dispatchToUiThread));
        if (onNoteOn != null)
            subscriptions.Add(eventBus.SubscribeNoteOn(onNoteOn, dispatchToUiThread));
        if (onNoteOff != null)
            subscriptions.Add(eventBus.SubscribeNoteOff(onNoteOff, dispatchToUiThread));
        if (onPlaybackStarted != null)
            subscriptions.Add(eventBus.SubscribePlaybackStarted(onPlaybackStarted, dispatchToUiThread));
        if (onPlaybackStopped != null)
            subscriptions.Add(eventBus.SubscribePlaybackStopped(onPlaybackStopped, dispatchToUiThread));
        if (onBpmChanged != null)
            subscriptions.Add(eventBus.SubscribeBpmChanged(onBpmChanged, dispatchToUiThread));
        if (onPatternAdded != null)
            subscriptions.Add(eventBus.SubscribePatternAdded(onPatternAdded, dispatchToUiThread));
        if (onPatternRemoved != null)
            subscriptions.Add(eventBus.SubscribePatternRemoved(onPatternRemoved, dispatchToUiThread));

        return new CompositeDisposable(subscriptions);
    }

    /// <summary>
    /// A disposable that disposes multiple subscriptions at once.
    /// </summary>
    private sealed class CompositeDisposable : IDisposable
    {
        private readonly List<EventBus.SubscriptionToken> _subscriptions;
        private bool _disposed;

        public CompositeDisposable(List<EventBus.SubscriptionToken> subscriptions)
        {
            _subscriptions = subscriptions;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var subscription in _subscriptions)
            {
                subscription.Dispose();
            }
            _subscriptions.Clear();
        }
    }
}

#endregion
