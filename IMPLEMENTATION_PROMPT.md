# MusicEngine Editor - Complete UI Implementation Prompt

> **Copy this entire file and paste it into Claude Code to implement all missing UI features.**

---

## PROJECT CONTEXT

I'm working on **MusicEngineEditor**, a WPF desktop DAW application that uses the **MusicEngine** core library. The editor has a modern dark theme (deep blacks with cyan accents) but many engine features lack dedicated UI controls.

### Project Paths
```
Engine:  C:/Users/null/RiderProjects/MusicEngine/MusicEngine.csproj
Editor:  C:/Users/null/RiderProjects/MusicEditor/MusicEngineEditor/MusicEngineEditor.csproj
Solution: C:/Users/null/RiderProjects/MusicEditor/MusicEngineEditor.sln
```

### Build Commands
```bash
# Build editor (includes engine)
dotnet build "C:/Users/null/RiderProjects/MusicEditor/MusicEngineEditor/MusicEngineEditor.csproj"

# Run editor
dotnet run --project "C:/Users/null/RiderProjects/MusicEditor/MusicEngineEditor/MusicEngineEditor.csproj"

# Run tests
dotnet test "C:/Users/null/RiderProjects/MusicEngine/MusicEngine.Tests/MusicEngine.Tests.csproj"
```

---

## CRITICAL WORKFLOW RULES

### 1. BUILD AFTER EACH TIER
After completing each tier, you MUST:
1. Run `dotnet build` and fix ALL errors
2. Run the editor to visually verify
3. Update documentation (CLAUDE.md, CLAUDE_CONTEXT.md)
4. Only then proceed to next tier

### 2. THEME REQUIREMENTS
Use these DarkTheme.xaml resources consistently:

| Purpose | Resource | Hex Value |
|---------|----------|-----------|
| Main Background | `{DynamicResource BackgroundBrush}` | #0D0D0D |
| Panel Background | `{DynamicResource PanelBackgroundBrush}` | #181818 |
| Editor Background | `{DynamicResource EditorBackgroundBrush}` | #121212 |
| Toolbar Background | `{DynamicResource ToolbarBackgroundBrush}` | #141414 |
| Hover State | hardcoded | #252525 |
| Accent Color | `{DynamicResource AccentBrush}` | #00D9FF |
| Accent Hover | `{DynamicResource AccentHoverBrush}` | #33E1FF |
| Success/Green | `{DynamicResource SuccessBrush}` | #00FF88 |
| Error/Red | `{DynamicResource ErrorBrush}` | #FF4757 |
| Warning/Yellow | `{DynamicResource WarningBrush}` | #FFB800 |
| Primary Text | `{DynamicResource ForegroundBrush}` | #E0E0E0 |
| Secondary Text | `{DynamicResource SecondaryForegroundBrush}` | #808080 |
| Disabled Text | `{DynamicResource DisabledForegroundBrush}` | #505050 |
| Bright Text | `{DynamicResource BrightForegroundBrush}` | #FFFFFF |
| Border | `{DynamicResource BorderBrush}` | #2A2A2A |
| Subtle Border | `{DynamicResource SubtleBorderBrush}` | #333333 |

### 3. CODE CONVENTIONS
- File-scoped namespaces: `namespace MusicEngineEditor.Controls;`
- MVVM with CommunityToolkit.Mvvm: `[ObservableProperty]`, `[RelayCommand]`
- No emojis in code or comments
- WPF Shapes alias: `using Shapes = System.Windows.Shapes;`
- Color converter: `System.Windows.Media.ColorConverter` (avoid ambiguity)
- Track type: `Models.TrackType` (avoid ambiguity)

### 4. FILE STRUCTURE
```
MusicEngineEditor/
├── Views/           # Full views (SessionView, PerformanceModeView, etc.)
├── ViewModels/      # MVVM ViewModels
├── Controls/        # Reusable controls
│   ├── Synths/      # Synthesizer-specific controls
│   ├── Effects/     # Effect-specific controls
│   ├── Analysis/    # Analysis visualization controls
│   ├── MIDI/        # MIDI feature controls
│   └── Performance/ # Live performance controls
├── Services/        # Business logic services
├── Models/          # Data models
└── Themes/          # DarkTheme.xaml, TouchTheme.xaml
```

---

## TIER 1 - CRITICAL FEATURES (Implement First)

### 1.1 Session View / Clip Launcher (Ableton-style)

**Files to create:**
- `Views/SessionView.xaml` + `.xaml.cs`
- `ViewModels/SessionViewModel.cs`
- `Controls/ClipLauncherGrid.xaml` + `.xaml.cs`
- `Controls/ClipSlotControl.xaml` + `.xaml.cs`

**Features:**
- 8x8 grid of clip slots (tracks x scenes)
- Clip slot states: Empty, Loaded, Playing, Recording, Queued
- Click to launch clip, double-click to edit
- Scene launch buttons (right column)
- Track stop buttons (bottom row)
- Global stop button
- Quantize selector: 1 bar, 1/2 bar, 1/4 bar, 1/8, None
- Clip colors (user-assignable)
- Playing indicator (pulsing border)
- Recording indicator (red pulsing)

**Engine integration:** `MusicEngine.Core.ClipLauncher`

**XAML Template:**
```xml
<UserControl x:Class="MusicEngineEditor.Controls.ClipSlotControl"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Background="{DynamicResource PanelBackgroundBrush}">
    <Border x:Name="SlotBorder"
            BorderBrush="{DynamicResource BorderBrush}"
            BorderThickness="1"
            CornerRadius="4"
            Background="#1A1A1A"
            Margin="2">
        <!-- Clip content here -->
    </Border>
</UserControl>
```

---

### 1.2 Step Sequencer / Drum Machine

**Files to create:**
- `Controls/StepSequencerControl.xaml` + `.xaml.cs`
- `ViewModels/StepSequencerViewModel.cs`
- `Controls/StepButton.xaml` + `.xaml.cs`

**Features:**
- 16/32/64 step grid (configurable)
- Multiple rows (8-16 drum sounds)
- Per-step velocity (vertical bar height or color intensity)
- Per-step probability (opacity: 100%=always, 50%=half, etc.)
- Pattern length selector
- Swing/shuffle knob (-50% to +50%)
- Direction modes: Forward, Reverse, Ping-Pong, Random
- Step highlighting during playback
- Mute/Solo per row
- Copy/paste patterns

**Engine integration:**
- `MusicEngine.Core.StepSequencer`
- `MusicEngine.Core.ProbabilitySequencer`

---

### 1.3 Modular Synth Patch Editor

**Files to create:**
- `Views/ModularSynthView.xaml` + `.xaml.cs`
- `ViewModels/ModularSynthViewModel.cs`
- `Controls/Synths/ModuleControl.xaml` + `.xaml.cs`
- `Controls/Synths/PatchCableControl.xaml` + `.xaml.cs`
- `Controls/Synths/ModulePortControl.xaml` + `.xaml.cs`

**Features:**
- Canvas workspace for module placement
- Draggable modules: VCO, VCF, VCA, LFO, ADSR, Mixer, Noise, S&H
- Input/Output ports on modules
- Patch cables drawn as bezier curves
- Cable colors by type:
  - Audio: Cyan (#00D9FF)
  - Control Voltage: Orange (#FF8C00)
  - Gate/Trigger: Green (#00FF88)
- Module palette (left sidebar)
- Right-click context menu: Add module, Delete, Duplicate
- Cable routing with click-drag from port to port
- Module parameter knobs

**Engine integration:** `MusicEngine.Core.Synthesizers.ModularSynth`

---

### 1.4 Polyphonic Pitch Editor (Melodyne DNA-style)

**Files to create:**
- `Views/PolyphonicPitchView.xaml` + `.xaml.cs`
- `ViewModels/PolyphonicPitchViewModel.cs`
- `Controls/PitchBlobControl.xaml` + `.xaml.cs`

**Features:**
- Piano roll background (pitch axis)
- Timeline (time axis)
- Note "blobs" representing detected pitches
- Blob properties:
  - Position = pitch and time
  - Width = duration
  - Height = amplitude/confidence
  - Color = voice separation
- Vertical drag = pitch correction
- Horizontal drag = timing adjustment
- Blob resize = duration change
- Formant preservation toggle
- Pitch drift/vibrato visualization (wavy line inside blob)
- Pitch snap to scale
- Undo/redo support

**Engine integration:** `MusicEngine.Core.Analysis.PolyphonicPitchEdit`

---

### 1.5 Spectral Editor

**Files to create:**
- `Views/SpectralEditorView.xaml` + `.xaml.cs`
- `ViewModels/SpectralEditorViewModel.cs`

**Features:**
- Spectrogram display (X=time, Y=frequency, Color=amplitude)
- Color maps: Grayscale, Heat, Rainbow, Cyan
- Selection tools:
  - Rectangle select
  - Lasso/freehand select
  - Magic wand (similar frequencies)
  - Brush tool
- Operations on selection:
  - Cut (remove frequencies)
  - Copy/Paste
  - Attenuate (reduce level)
  - Boost (increase level)
  - Fill (paint frequencies)
- Zoom controls (time and frequency)
- Playback with selection preview
- Undo/redo stack

**Engine integration:** `MusicEngine.Core.Analysis.SpectralEditor`

---

### 1.6 AI Features Panel

**Files to create:**
- `Controls/AIFeaturesPanel.xaml` + `.xaml.cs`
- `ViewModels/AIFeaturesViewModel.cs`

**Sections:**

**AI Denoiser:**
- Threshold slider (0-100%)
- Learn Noise Profile button
- Noise profile indicator
- Preview toggle
- Quality mode: Fast, Balanced, Quality

**AI Declip:**
- Sensitivity slider
- Quality mode selector
- Before/After preview

**Chord Suggestion:**
- Current chord display
- Suggested next chords (4-6 options)
- Click to insert chord
- Style selector: Pop, Jazz, Classical, EDM

**Melody Generator:**
- Seed pattern input (draw or play)
- Temperature slider (0.1-2.0)
- Length selector (1-8 bars)
- Generate button
- Preview and Insert buttons

**Mix Assistant:**
- Analyze Mix button
- EQ suggestions display
- Compression suggestions
- Apply Selected button

**Mastering Assistant:**
- Target loudness (LUFS)
- Style: Transparent, Warm, Punchy, Loud
- One-click Master button
- A/B preview toggle

**Stem Separation:**
- Separate button
- Progress bar
- Stem toggles: Drums, Bass, Vocals, Other
- Export stems button

**Engine integration:** All `MusicEngine.Core.AI.*` classes

---

### 1.7 Modulation Matrix

**Files to create:**
- `Controls/ModulationMatrixControl.xaml` + `.xaml.cs`
- `ViewModels/ModulationMatrixViewModel.cs`

**Features:**
- Grid layout: Sources (rows) x Destinations (columns)
- Sources: LFO1-4, Env1-4, Velocity, Aftertouch, ModWheel, Expression
- Destinations: Filter Cutoff, Resonance, Pitch, Pan, Volume, etc.
- Cell click: Opens amount slider (-100% to +100%)
- Cell color intensity = modulation depth
- Positive = blue gradient, Negative = orange gradient
- Clear row/column buttons
- Preset save/load

**Engine integration:** `MusicEngine.Core.ModulationMatrix`

---

### 1.8 Sidechain Matrix

**Files to create:**
- `Controls/SidechainMatrixControl.xaml` + `.xaml.cs`
- `ViewModels/SidechainMatrixViewModel.cs`

**Features:**
- Grid: Source tracks (rows) x Target effects (columns)
- Checkbox to enable sidechain routing
- Visual connection lines
- Pre/Post fader selector per source
- Solo sidechain listen button

**Engine integration:** `MusicEngine.Core.Routing.SidechainMatrix`

---

### TIER 1 CHECKPOINT

```bash
# After completing all Tier 1 features:

# 1. Build
dotnet build "C:/Users/null/RiderProjects/MusicEditor/MusicEngineEditor/MusicEngineEditor.csproj"

# 2. Fix any errors (DO NOT PROCEED until 0 errors)

# 3. Run and test
dotnet run --project "C:/Users/null/RiderProjects/MusicEditor/MusicEngineEditor/MusicEngineEditor.csproj"

# 4. Update CLAUDE.md - add under new section:
# ### Phase O: Tier 1 Critical UI Features (COMPLETE)
# - Session View / Clip Launcher
# - Step Sequencer
# - Modular Synth Patch Editor
# - Polyphonic Pitch Editor
# - Spectral Editor
# - AI Features Panel
# - Modulation Matrix
# - Sidechain Matrix

# 5. Proceed to Tier 2
```

---

## TIER 2 - SYNTHESIZER UIs

### 2.1 FM Synth Editor
**File:** `Controls/Synths/FMSynthControl.xaml`
- 6-operator matrix display
- Algorithm selector (20 presets with visual diagram)
- Per-operator: Ratio, Level, Feedback, Envelope (ADSR)
- Operator enable/disable toggles
- Modulation depth between operators

**Engine:** `MusicEngine.Core.Synthesizers.FMSynth`

---

### 2.2 Granular Synth Editor
**File:** `Controls/Synths/GranularSynthControl.xaml`
- Sample waveform display
- Grain position marker (draggable)
- Position randomness range display
- Grain size slider (1ms - 500ms)
- Grain density (1-100 grains/sec)
- Pitch spread (+/- semitones)
- Grain envelope shape: Gaussian, Triangle, Rectangle, Hann

**Engine:** `MusicEngine.Core.Synthesizers.GranularSynth`

---

### 2.3 Wavetable Synth Editor
**File:** `Controls/Synths/WavetableSynthControl.xaml`
- 3D wavetable visualization (stacked waveforms)
- Wavetable position slider with morph preview
- Wavetable browser/loader
- Warp modes: Sync, Bend, Mirror
- Sub-oscillator level

**Engine:** `MusicEngine.Core.Synthesizers.WavetableSynth`

---

### 2.4 Drum Synth Editor
**File:** `Controls/Synths/DrumSynthControl.xaml`
- Drum type tabs: Kick, Snare, Hi-Hat, Clap, Tom, Cymbal
- **Kick:** Pitch, Pitch Decay, Click, Tone, Decay
- **Snare:** Pitch, Tone, Noise Level, Snap, Decay
- **Hi-Hat:** Tone, Noise Color, Decay, Open/Closed
- **Clap:** Spread, Decay, Room
- Preset buttons: 808, 909, LinnDrum, Custom

**Engine:** `MusicEngine.Core.Synthesizers.DrumSynth`

---

### 2.5 PadSynth Editor
**File:** `Controls/Synths/PadSynthControl.xaml`
- Harmonic profile bar graph (32-64 harmonics)
- Bandwidth control (how much frequencies spread)
- Detune/Unison spread
- Evolution speed (modulation over time)
- Render button (generates wavetable)

**Engine:** `MusicEngine.Core.Synthesizers.PadSynth`

---

### 2.6 Vector Synth Editor
**File:** `Controls/Synths/VectorSynthControl.xaml`
- Central XY pad (joystick-style)
- 4 corner oscillator displays (A, B, C, D)
- Per-corner: Waveform, Pitch, Level
- Vector envelope path editor (draw movement over time)
- Loop mode toggle

**Engine:** `MusicEngine.Core.Synthesizers.VectorSynth`

---

### 2.7 Additive Synth Editor
**File:** `Controls/Synths/AdditiveSynthControl.xaml`
- Harmonic bar sliders (64 harmonics)
- Hammond drawbar mode (9 drawbars)
- Per-harmonic envelope toggle
- Harmonic presets: Saw, Square, Organ, Bell
- Even/Odd harmonic balance

**Engine:** `MusicEngine.Core.Synthesizers.AdditiveSynth`

---

### TIER 2 CHECKPOINT

```bash
dotnet build "C:/Users/null/RiderProjects/MusicEditor/MusicEngineEditor/MusicEngineEditor.csproj"
# Fix all errors, test, then update CLAUDE.md:
# ### Phase P: Tier 2 Synthesizer UIs (COMPLETE)
```

---

## TIER 3 - EFFECT UIs

### 3.1 Convolution Reverb Editor
**File:** `Controls/Effects/ConvolutionReverbControl.xaml`
- IR waveform display
- IR file browser with preview
- Pre-delay (0-200ms)
- Decay time multiplier (0.5x-2x)
- Hi-cut / Lo-cut filters
- Dry/Wet mix
- Stereo width

**Engine:** `MusicEngine.Core.Effects.ConvolutionReverb`

---

### 3.2 Multiband Compressor Editor
**File:** `Controls/Effects/MultibandCompressorControl.xaml`
- Frequency spectrum display
- Draggable band split points (3-4 bands)
- Per-band controls: Threshold, Ratio, Attack, Release, Makeup Gain
- Gain reduction meters per band
- Solo band button
- Bypass per band

**Engine:** `MusicEngine.Core.Effects.Dynamics.MultibandCompressor`

---

### 3.3 Vocoder Editor
**File:** `Controls/Effects/VocoderControl.xaml`
- Band visualization (16-32 bands)
- Carrier source selector: Internal synth, External input, Noise
- Formant shift (+/- 12 semitones)
- Band count slider
- Modulator input level
- Unvoiced detector threshold

**Engine:** `MusicEngine.Core.Effects.EnhancedVocoder`

---

### 3.4 Spectral Gate Editor
**File:** `Controls/Effects/SpectralGateControl.xaml`
- Frequency spectrum with threshold curve (drawable)
- Per-frequency threshold
- Attack/Release per band
- Hold time
- Range (maximum attenuation)
- Gate activity visualization

**Engine:** `MusicEngine.Core.Effects.Dynamics.SpectralGate`

---

### TIER 3 CHECKPOINT

```bash
dotnet build "C:/Users/null/RiderProjects/MusicEditor/MusicEngineEditor/MusicEngineEditor.csproj"
# Fix all errors, test, then update CLAUDE.md:
# ### Phase Q: Tier 3 Effect UIs (COMPLETE)
```

---

## TIER 4 - ANALYSIS UIs

### 4.1 3D Spectrogram View
**File:** `Controls/Analysis/Spectrogram3DControl.xaml`
- 3D waterfall display (time flowing backward)
- Color maps: Grayscale, Heat, Rainbow, Ice, Magma, Viridis, Plasma
- Rotation controls (mouse drag)
- Zoom (scroll wheel)
- Frequency range selector
- History length (seconds)

**Engine:** `MusicEngine.Core.Analysis.Spectrogram3D`

---

### 4.2 Frequency Collision Detector
**File:** `Controls/Analysis/FrequencyCollisionControl.xaml`
- Multi-track spectrum overlay (different colors per track)
- Collision zones highlighted in red
- Collision severity indicator
- Suggested EQ cuts list (click to apply)
- Track selector checkboxes

**Engine:** `MusicEngine.Core.Analysis.FrequencyCollisionDetector`

---

### 4.3 Mix Radar View
**File:** `Controls/Analysis/MixRadarControl.xaml`
- Radar/spider chart (8 frequency bands)
- Current mix curve (cyan fill)
- Reference curve overlay (white outline)
- Band labels: Sub, Bass, Low-Mid, Mid, High-Mid, Presence, Brilliance, Air
- Balance indicator (center dot)

**Engine:** `MusicEngine.Core.Analysis.MixRadarAnalyzer`

---

### 4.4 Phase Analyzer View
**File:** `Controls/Analysis/PhaseAnalyzerControl.xaml`
- Phase correlation vs frequency graph
- Mono compatibility percentage
- Problem areas highlighted (phase issues)
- Mid/Side balance meter
- Correlation history

**Engine:** `MusicEngine.Core.Analysis.PhaseAnalyzer`

---

### TIER 4 CHECKPOINT

```bash
dotnet build "C:/Users/null/RiderProjects/MusicEditor/MusicEngineEditor/MusicEngineEditor.csproj"
# Fix all errors, test, then update CLAUDE.md:
# ### Phase R: Tier 4 Analysis UIs (COMPLETE)
```

---

## TIER 5 - MIDI FEATURE UIs

### 5.1 MPE Editor
**File:** `Controls/MIDI/MPEControl.xaml`
- Note display with per-note pitch bend curves
- Pressure/Aftertouch lane (color intensity)
- Slide/Timbre lane
- MPE zone configuration (Lower/Upper/Global)
- Pitch bend range setting

**Engine:** `MusicEngine.Core.Midi.MPESupport`

---

### 5.2 Expression Maps Editor
**File:** `Controls/MIDI/ExpressionMapControl.xaml`
- Articulation list (Sustain, Staccato, Pizzicato, etc.)
- Keyswitch note assignment per articulation
- Velocity/CC trigger options
- Output action (Note, CC, Program Change)
- Map import/export

**Engine:** `MusicEngine.Core.Midi.ExpressionMaps`

---

### 5.3 Probability Sequencer Grid
**File:** `Controls/MIDI/ProbabilitySequencerControl.xaml`
- Step grid with probability indicator per step
- Probability slider (0-100%) per step
- Ratchet count (1-8 repeats)
- Ratchet speed (1/1 - 1/32)
- Conditions: Always, Every 2nd, Every 4th, Random, Fill

**Engine:** `MusicEngine.Core.ProbabilitySequencer`

---

### TIER 5 CHECKPOINT

```bash
dotnet build "C:/Users/null/RiderProjects/MusicEditor/MusicEngineEditor/MusicEngineEditor.csproj"
# Fix all errors, test, then update CLAUDE.md:
# ### Phase S: Tier 5 MIDI UIs (COMPLETE)
```

---

## TIER 6 - PERFORMANCE UIs

### 6.1 Live Looper Control
**File:** `Controls/Performance/LiveLooperControl.xaml`
- Layer stack (4-8 layers)
- Per-layer: Waveform, Volume, Pan, Mute, Record, Play, Stop
- Overdub mode toggle
- Undo last layer
- Clear all button
- Loop length indicator
- Input monitor

**Engine:** `MusicEngine.Core.LiveLooper`

---

### 6.2 Performance Mode / Scene Manager
**File:** `Views/PerformanceModeView.xaml`
- Scene list with names and colors
- Active scene highlight
- Crossfade slider between scenes
- MIDI trigger assignment per scene
- Scene edit mode
- Transition time setting

**Engine:** `MusicEngine.Core.PerformanceMode`

---

### 6.3 DJ Effects Panel
**File:** `Controls/Performance/DJEffectsControl.xaml`
- Filter sweep XY pad (X=cutoff, Y=resonance)
- Beat repeat buttons (1/4, 1/8, 1/16, 1/32)
- Brake button (vinyl stop effect)
- Spinback button
- Echo out button
- Flanger on/off
- Effect wet/dry

**Engine:** `MusicEngine.Core.DJEffects`

---

### 6.4 GrooveBox Control
**File:** `Controls/Performance/GrooveBoxControl.xaml`
- Drum pads (4x4 grid)
- Pattern selector (A1-H8)
- Kit browser
- Swing knob
- Bass synth controls
- Pattern chain mode
- Tempo lock toggle

**Engine:** `MusicEngine.Core.GrooveBox`

---

### TIER 6 CHECKPOINT (FINAL)

```bash
dotnet build "C:/Users/null/RiderProjects/MusicEditor/MusicEngineEditor/MusicEngineEditor.csproj"
# Fix all errors, test, then update CLAUDE.md:
# ### Phase T: Tier 6 Performance UIs (COMPLETE)
```

---

## FINAL VERIFICATION

After ALL tiers are complete:

```bash
# Full solution build
dotnet build "C:/Users/null/RiderProjects/MusicEditor/MusicEngineEditor.sln"

# Run all tests
dotnet test "C:/Users/null/RiderProjects/MusicEngine/MusicEngine.Tests/MusicEngine.Tests.csproj"

# Final run and test all features
dotnet run --project "C:/Users/null/RiderProjects/MusicEditor/MusicEngineEditor/MusicEngineEditor.csproj"
```

### Update CLAUDE.md Final Summary:

```markdown
## UI Implementation Complete (Phase O-T)

### Statistics
- **New Views Created:** X
- **New Controls Created:** Y
- **New ViewModels Created:** Z
- **Engine Integrations:** W

### Tier Summary
| Tier | Category | Controls | Status |
|------|----------|----------|--------|
| 1 | Critical | 8 | Complete |
| 2 | Synthesizers | 7 | Complete |
| 3 | Effects | 4 | Complete |
| 4 | Analysis | 4 | Complete |
| 5 | MIDI | 3 | Complete |
| 6 | Performance | 4 | Complete |
```

---

## ERROR HANDLING GUIDE

### Common Build Errors and Fixes:

**1. Missing namespace:**
```csharp
// Add at top of file:
using MusicEngine.Core;
using MusicEngine.Core.Synthesizers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
```

**2. XAML resource not found:**
```xml
<!-- Ensure DarkTheme.xaml is merged in App.xaml -->
<ResourceDictionary Source="Themes/DarkTheme.xaml"/>
```

**3. ColorConverter ambiguity:**
```csharp
// Use full namespace:
var converter = new System.Windows.Media.ColorConverter();
```

**4. TrackType ambiguity:**
```csharp
// Use explicit namespace:
Models.TrackType trackType = Models.TrackType.Audio;
```

**5. Shapes conflict:**
```csharp
// Add alias at top:
using Shapes = System.Windows.Shapes;
// Then use:
var rect = new Shapes.Rectangle();
```

**6. DynamicResource in code-behind:**
```csharp
// Get resource from XAML:
var brush = (SolidColorBrush)FindResource("AccentBrush");
```

---

## QUICK REFERENCE: CONTROL TEMPLATE

```xml
<UserControl x:Class="MusicEngineEditor.Controls.[ControlName]"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d"
             d:DesignHeight="300" d:DesignWidth="400"
             Background="{DynamicResource PanelBackgroundBrush}">

    <Border BorderBrush="{DynamicResource BorderBrush}"
            BorderThickness="1"
            CornerRadius="6"
            Padding="12">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/> <!-- Header -->
                <RowDefinition Height="*"/>    <!-- Content -->
            </Grid.RowDefinitions>

            <!-- Header -->
            <TextBlock Text="[Control Name]"
                       FontWeight="SemiBold"
                       FontSize="12"
                       Foreground="{DynamicResource BrightForegroundBrush}"
                       Margin="0,0,0,8"/>

            <!-- Content -->
            <Grid Grid.Row="1">
                <!-- Your content here -->
            </Grid>
        </Grid>
    </Border>
</UserControl>
```

---

## QUICK REFERENCE: VIEWMODEL TEMPLATE

```csharp
namespace MusicEngineEditor.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class [ViewModelName] : ObservableObject
{
    [ObservableProperty]
    private bool _isEnabled = true;

    [ObservableProperty]
    private double _value = 0.5;

    [RelayCommand]
    private void DoSomething()
    {
        // Command implementation
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        // Async command implementation
    }
}
```

---

**START IMPLEMENTATION NOW. Begin with Tier 1.1 (Session View / Clip Launcher).**
