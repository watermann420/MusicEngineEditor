// MusicEngineEditor - Mixer Effect Service
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using MusicEngine.Core;
using MusicEngine.Infrastructure.DependencyInjection.Interfaces;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Services;

/// <summary>
/// Service for managing effects on mixer channels.
/// Bridges the editor models (MixerChannel, EffectSlot) with the MusicEngine core (IVstHost, EffectChain).
/// </summary>
public class MixerEffectService
{
    private readonly IVstHost _vstHost;
    private readonly object _lock = new();

    /// <summary>
    /// Creates a new MixerEffectService.
    /// </summary>
    /// <param name="vstHost">The VST host for loading plugins.</param>
    /// <exception cref="ArgumentNullException">Thrown if vstHost is null.</exception>
    public MixerEffectService(IVstHost vstHost)
    {
        _vstHost = vstHost ?? throw new ArgumentNullException(nameof(vstHost));
    }

    /// <summary>
    /// Adds a VST effect to a mixer channel.
    /// </summary>
    /// <param name="channel">The mixer channel to add the effect to.</param>
    /// <param name="vstPath">The path to the VST plugin file.</param>
    /// <param name="slotIndex">The slot index to add the effect at. If -1, adds to the first empty slot.</param>
    /// <returns>True if the effect was added successfully; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown if channel or vstPath is null.</exception>
    public async Task<bool> AddVstEffectAsync(MixerChannel channel, string vstPath, int slotIndex = -1)
    {
        if (channel == null)
            throw new ArgumentNullException(nameof(channel));

        if (string.IsNullOrWhiteSpace(vstPath))
            throw new ArgumentNullException(nameof(vstPath));

        return await Task.Run(() =>
        {
            lock (_lock)
            {
                try
                {
                    // Validate the plugin path
                    if (!File.Exists(vstPath))
                    {
                        System.Diagnostics.Debug.WriteLine($"VST plugin file not found: {vstPath}");
                        return false;
                    }

                    // Load the plugin via VstHost
                    IVstPlugin? plugin = _vstHost.LoadPlugin(vstPath);

                    if (plugin == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to load VST plugin: {vstPath}");
                        return false;
                    }

                    // Check if it's an instrument (we need an effect)
                    if (plugin.IsInstrument)
                    {
                        System.Diagnostics.Debug.WriteLine($"Plugin is an instrument, not an effect: {vstPath}");
                        _vstHost.UnloadPlugin(plugin.Name);
                        return false;
                    }

                    // Create the VstEffectAdapter
                    VstEffectAdapter adapter;
                    try
                    {
                        adapter = new VstEffectAdapter(plugin);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to create VstEffectAdapter: {ex.Message}");
                        _vstHost.UnloadPlugin(plugin.Name);
                        return false;
                    }

                    // Determine the VST format
                    string format = plugin.IsVst3 ? "VST3" : "VST2";

                    // Find the target slot
                    EffectSlot? targetSlot = null;

                    if (slotIndex >= 0 && slotIndex < channel.EffectSlots.Count)
                    {
                        targetSlot = channel.EffectSlots[slotIndex];

                        // If the slot is not empty, clear it first
                        if (!targetSlot.IsEmpty)
                        {
                            targetSlot.ClearEffect();
                        }
                    }
                    else
                    {
                        // Find the first empty slot
                        foreach (var slot in channel.EffectSlots)
                        {
                            if (slot.IsEmpty)
                            {
                                targetSlot = slot;
                                break;
                            }
                        }

                        // If no empty slot found, create a new one
                        if (targetSlot == null)
                        {
                            targetSlot = new EffectSlot(channel.EffectSlots.Count);
                            Application.Current?.Dispatcher.Invoke(() =>
                            {
                                channel.EffectSlots.Add(targetSlot);
                            });
                        }
                    }

                    // Load the VST effect into the slot
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        targetSlot.LoadVstEffect(vstPath, plugin.Name, plugin, adapter, format);
                    });

                    System.Diagnostics.Debug.WriteLine($"Successfully added VST effect '{plugin.Name}' to channel '{channel.Name}' slot {targetSlot.Index}");
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error adding VST effect: {ex.Message}");
                    return false;
                }
            }
        });
    }

    /// <summary>
    /// Removes an effect from a mixer channel.
    /// </summary>
    /// <param name="channel">The mixer channel to remove the effect from.</param>
    /// <param name="slotIndex">The slot index of the effect to remove.</param>
    /// <exception cref="ArgumentNullException">Thrown if channel is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if slotIndex is out of range.</exception>
    public void RemoveEffect(MixerChannel channel, int slotIndex)
    {
        if (channel == null)
            throw new ArgumentNullException(nameof(channel));

        if (slotIndex < 0 || slotIndex >= channel.EffectSlots.Count)
            throw new ArgumentOutOfRangeException(nameof(slotIndex), $"Slot index {slotIndex} is out of range.");

        lock (_lock)
        {
            EffectSlot slot = channel.EffectSlots[slotIndex];

            if (slot.IsEmpty)
            {
                return;
            }

            // Dispose VST resources if present
            if (slot.VstAdapter != null)
            {
                try
                {
                    slot.VstAdapter.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error disposing VstAdapter: {ex.Message}");
                }
            }

            // Unload the plugin from the host if it's a VST plugin
            if (slot.IsVstEffect && slot.VstPlugin != null)
            {
                try
                {
                    _vstHost.UnloadPlugin(slot.VstPlugin.Name);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error unloading plugin: {ex.Message}");
                }
            }

            // Clear the slot on the UI thread
            Application.Current?.Dispatcher.Invoke(() =>
            {
                slot.ClearEffect();
            });

            System.Diagnostics.Debug.WriteLine($"Removed effect from channel '{channel.Name}' slot {slotIndex}");
        }
    }

    /// <summary>
    /// Reorders effects within a mixer channel.
    /// </summary>
    /// <param name="channel">The mixer channel containing the effects.</param>
    /// <param name="fromIndex">The current index of the effect to move.</param>
    /// <param name="toIndex">The target index to move the effect to.</param>
    /// <exception cref="ArgumentNullException">Thrown if channel is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if fromIndex or toIndex is out of range.</exception>
    public void ReorderEffects(MixerChannel channel, int fromIndex, int toIndex)
    {
        if (channel == null)
            throw new ArgumentNullException(nameof(channel));

        if (fromIndex < 0 || fromIndex >= channel.EffectSlots.Count)
            throw new ArgumentOutOfRangeException(nameof(fromIndex), $"From index {fromIndex} is out of range.");

        if (toIndex < 0 || toIndex >= channel.EffectSlots.Count)
            throw new ArgumentOutOfRangeException(nameof(toIndex), $"To index {toIndex} is out of range.");

        if (fromIndex == toIndex)
        {
            return;
        }

        lock (_lock)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                // Get the slot being moved
                EffectSlot movingSlot = channel.EffectSlots[fromIndex];

                // Remove from the current position
                channel.EffectSlots.RemoveAt(fromIndex);

                // Insert at the new position
                channel.EffectSlots.Insert(toIndex, movingSlot);

                // Update indices for all slots
                for (int i = 0; i < channel.EffectSlots.Count; i++)
                {
                    // Note: EffectSlot._index is readonly, so we can't update it here.
                    // The index is fixed at creation time. For UI display purposes,
                    // the position in the collection determines the visual order.
                }
            });

            System.Diagnostics.Debug.WriteLine($"Reordered effect in channel '{channel.Name}' from slot {fromIndex} to slot {toIndex}");
        }
    }

    /// <summary>
    /// Sets the bypass state of an effect.
    /// </summary>
    /// <param name="channel">The mixer channel containing the effect.</param>
    /// <param name="slotIndex">The slot index of the effect.</param>
    /// <param name="bypass">True to bypass the effect; false to enable it.</param>
    /// <exception cref="ArgumentNullException">Thrown if channel is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if slotIndex is out of range.</exception>
    public void SetBypass(MixerChannel channel, int slotIndex, bool bypass)
    {
        if (channel == null)
            throw new ArgumentNullException(nameof(channel));

        if (slotIndex < 0 || slotIndex >= channel.EffectSlots.Count)
            throw new ArgumentOutOfRangeException(nameof(slotIndex), $"Slot index {slotIndex} is out of range.");

        lock (_lock)
        {
            EffectSlot slot = channel.EffectSlots[slotIndex];

            if (slot.IsEmpty)
            {
                return;
            }

            // Update the slot's bypass state
            Application.Current?.Dispatcher.Invoke(() =>
            {
                slot.IsBypassed = bypass;
            });

            // Update the underlying VstAdapter's enabled state (bypass = not enabled)
            if (slot.VstAdapter != null)
            {
                slot.VstAdapter.Enabled = !bypass;
            }

            // Also update the underlying plugin's bypass state directly
            if (slot.VstPlugin != null)
            {
                slot.VstPlugin.IsBypassed = bypass;
            }

            System.Diagnostics.Debug.WriteLine($"Set bypass={bypass} for effect in channel '{channel.Name}' slot {slotIndex}");
        }
    }

    /// <summary>
    /// Opens the plugin editor window for a VST effect.
    /// </summary>
    /// <param name="slot">The effect slot containing the VST plugin.</param>
    /// <exception cref="ArgumentNullException">Thrown if slot is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the slot does not contain a VST plugin or the plugin has no editor.</exception>
    public void OpenPluginEditor(EffectSlot slot)
    {
        if (slot == null)
            throw new ArgumentNullException(nameof(slot));

        if (!slot.IsVstEffect || slot.VstPlugin == null)
            throw new InvalidOperationException("The effect slot does not contain a VST plugin.");

        if (!slot.VstPlugin.HasEditor)
            throw new InvalidOperationException($"The plugin '{slot.VstPlugin.Name}' does not have an editor GUI.");

        // Get the editor size
        if (!slot.VstPlugin.GetEditorSize(out int width, out int height))
        {
            // Use default size if not available
            width = 800;
            height = 600;
        }

        // Create a WPF window to host the plugin editor
        var editorWindow = new Window
        {
            Title = $"{slot.DisplayName} - Plugin Editor",
            Width = width + 16,  // Account for window chrome
            Height = height + 39,  // Account for window chrome and title bar
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = true
        };

        // Set the owner to the main window if available
        if (Application.Current?.MainWindow != null && Application.Current.MainWindow.IsLoaded)
        {
            editorWindow.Owner = Application.Current.MainWindow;
        }

        // Get the window handle
        var helper = new WindowInteropHelper(editorWindow);
        helper.EnsureHandle();
        IntPtr hwnd = helper.Handle;

        // Open the plugin editor using the VstPlugin.OpenEditor method
        IntPtr editorHandle = slot.VstPlugin.OpenEditor(hwnd);

        if (editorHandle == IntPtr.Zero)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to open editor for plugin '{slot.VstPlugin.Name}'");
            return;
        }

        // Handle window closing to close the plugin editor
        editorWindow.Closing += (sender, args) =>
        {
            try
            {
                slot.VstPlugin?.CloseEditor();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error closing plugin editor: {ex.Message}");
            }
        };

        // Show the window
        editorWindow.Show();

        System.Diagnostics.Debug.WriteLine($"Opened editor for plugin '{slot.VstPlugin.Name}'");
    }

    /// <summary>
    /// Opens the plugin editor window for a VST effect asynchronously.
    /// </summary>
    /// <param name="slot">The effect slot containing the VST plugin.</param>
    /// <returns>A task representing the async operation.</returns>
    public Task OpenPluginEditorAsync(EffectSlot slot)
    {
        // OpenPluginEditor must run on the UI thread
        if (Application.Current?.Dispatcher.CheckAccess() == true)
        {
            OpenPluginEditor(slot);
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource<bool>();

        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                OpenPluginEditor(slot);
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        return tcs.Task;
    }

    /// <summary>
    /// Saves the state of all VST effects on a channel.
    /// </summary>
    /// <param name="channel">The mixer channel.</param>
    public void SaveChannelEffectStates(MixerChannel channel)
    {
        if (channel == null)
            throw new ArgumentNullException(nameof(channel));

        lock (_lock)
        {
            foreach (var slot in channel.EffectSlots)
            {
                if (!slot.IsEmpty && slot.IsVstEffect && slot.VstAdapter != null)
                {
                    try
                    {
                        slot.SaveVstState();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error saving VST state for slot {slot.Index}: {ex.Message}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Restores the state of all VST effects on a channel.
    /// </summary>
    /// <param name="channel">The mixer channel.</param>
    public void RestoreChannelEffectStates(MixerChannel channel)
    {
        if (channel == null)
            throw new ArgumentNullException(nameof(channel));

        lock (_lock)
        {
            foreach (var slot in channel.EffectSlots)
            {
                if (!slot.IsEmpty && slot.IsVstEffect && slot.VstAdapter != null && slot.VstState != null)
                {
                    try
                    {
                        slot.RestoreVstState();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error restoring VST state for slot {slot.Index}: {ex.Message}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Disposes all effects on a channel, releasing VST resources.
    /// </summary>
    /// <param name="channel">The mixer channel.</param>
    public void DisposeChannelEffects(MixerChannel channel)
    {
        if (channel == null)
            throw new ArgumentNullException(nameof(channel));

        lock (_lock)
        {
            for (int i = 0; i < channel.EffectSlots.Count; i++)
            {
                var slot = channel.EffectSlots[i];

                if (!slot.IsEmpty)
                {
                    try
                    {
                        RemoveEffect(channel, i);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error disposing effect at slot {i}: {ex.Message}");
                    }
                }
            }
        }
    }
}
