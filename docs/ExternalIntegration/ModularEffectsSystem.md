# MusicEngine Modular Effects System

The modular effects system enables creating thousands of effect combinations through code - similar to VCV Rack, but fully programmatic without visual cables.

## Concept

The system is based on three main components:

1. **Effect Nodes** - Individual processing units (oscillators, filters, delays, etc.)
2. **Effect Graph** - Connects nodes into a signal chain
3. **Patch Manager** - Saves and loads presets

## Quick Start

```csharp
using MusicEngineEditor.Effects;

// Create graph
var graph = new EffectGraph { Name = "My Synth" };

// Create nodes
var osc = EffectNodeFactory.CreateNode("Oscillator");
var filter = EffectNodeFactory.CreateNode("Filter");
var output = EffectNodeFactory.CreateNode("AudioOutput");

// Add nodes to graph
graph.AddNode(osc);
graph.AddNode(filter);
graph.AddNode(output);

// Connect nodes (Oscillator -> Filter -> Output)
graph.Connect(osc.Id, 0, filter.Id, 0);
graph.Connect(filter.Id, 0, output.Id, 0);

// Process audio
float[] buffer = new float[1024];
graph.Process(buffer, buffer.Length);
```

## Available Node Types

### Generators
| Node | Description | Key Features |
|------|-------------|--------------|
| `Oscillator` | Band-limited waveform generator | PolyBLEP anti-aliasing, Sine/Saw/Square/Triangle/Noise, PWM, Sync |
| `NoiseGenerator` | Multi-type noise generator | White (uniform), Pink (Paul Kellet -3dB/oct), Brown (-6dB/oct) |
| `LFO` | Low Frequency Oscillator | Multi-waveform outputs, Phase offset, Rate CV |

### Filters
| Node | Description | Key Features |
|------|-------------|--------------|
| `Filter` | Moog-style 4-pole ladder | 24dB/oct LP, Self-oscillation, Drive/Saturation, Oversampled |
| `EQ3Band` | 3-band parametric EQ | Biquad filters, Low/High shelf, Mid peaking, Proper Q control |
| `FormantFilter` | Vowel formant filter | 5 vowels (A/E/I/O/U), 4 parallel resonators, Morphing |

### Dynamics
| Node | Description | Key Features |
|------|-------------|--------------|
| `Gain` | Smooth gain control | dB scaling, CV modulation, Smoothing |
| `Compressor` | Professional compressor | Look-ahead, Soft knee, RMS/Peak detection, Sidechain |
| `Limiter` | True-peak brickwall limiter | Look-ahead, Smooth gain reduction |
| `Gate` | Noise gate | Hysteresis, Hold time, Sidechain, Range control |

### Effects
| Node | Description | Key Features |
|------|-------------|--------------|
| `Delay` | Stereo delay | Hermite interpolation, Modulation, Ping-pong, Feedback filter |
| `Reverb` | Freeverb algorithm | 8 comb + 4 allpass filters, Pre-delay, Width, Damping |
| `Chorus` | Multi-voice chorus | Up to 3 voices, Stereo spread, BBD-style |
| `Flanger` | Through-zero flanger | Manual control, Negative feedback |
| `Phaser` | Analog multi-stage phaser | 2-12 stages, Frequency range control |
| `Bitcrusher` | Lo-Fi effect | Bit depth, Sample rate reduction, Noise shaping |
| `Distortion` | Oversampled waveshaper | 4x oversampling, 5 types (Soft/Hard/Tube/Foldback/Asymmetric) |
| `RingMod` | Ring modulator | Internal oscillator, External carrier input |

### Modulators
| Node | Description | Key Features |
|------|-------------|--------------|
| `Envelope` | ADSR envelope | Curve shaping, Velocity, End-of-cycle trigger |
| `SampleAndHold` | Sample & Hold | Internal noise, Glide |
| `Slew` | Portamento/Glide | Separate rise/fall, Shape control |
| `Quantizer` | Scale quantizer | 15 scales, Root note, Trigger output |

### Utilities
| Node | Description | Key Features |
|------|-------------|--------------|
| `Mixer` | 4-channel mixer | Per-channel pan, Equal power panning |
| `VCA` | Voltage Controlled Amp | Linear/Exponential response |
| `Split` | Signal splitter | 4 buffered outputs |
| `Merge` | Signal merger | Sum, Average, Max outputs |
| `Inverter` | Signal inverter | Offset control |
| `Offset` | DC offset & scale | Bipolar offset, Scale |
| `Rectifier` | Signal rectifier | Full/Half+ /Half- outputs |
| `Crossfade` | A/B crossfader | Linear, Equal power, S-curve |

### Analyzers
| Node | Description | Key Features |
|------|-------------|--------------|
| `Follower` | Envelope follower | Attack/Release, Gate output |
| `PitchDetect` | Pitch detector | Autocorrelation, Confidence output |

### Sequencing
| Node | Description | Key Features |
|------|-------------|--------------|
| `Clock` | Master clock | BPM, Swing, Multiple divisions |
| `ClockDiv` | Clock divider | /2, /4, /8, /16 outputs |
| `StepSequencer` | 8-step sequencer | Direction control, Gate length |

### I/O
| Node | Description | Key Features |
|------|-------------|--------------|
| `AudioInput` | Audio input | Stereo + Mono outputs, Gain |
| `AudioOutput` | Audio output | Stereo input, Gain |
| `MidiInput` | MIDI input | Pitch, Gate, Velocity, Aftertouch |
| `MidiOutput` | MIDI output | Channel selection |

## Professional Algorithm Details

### Oscillator - PolyBLEP Anti-Aliasing

The oscillator uses PolyBLEP (Polynomial Band-Limited Step) to eliminate aliasing artifacts:

```csharp
// PolyBLEP correction smooths discontinuities in waveforms
private static float PolyBlep(double t, double dt)
{
    if (t < dt)
    {
        t /= dt;
        return (float)(t + t - t * t - 1);
    }
    else if (t > 1 - dt)
    {
        t = (t - 1) / dt;
        return (float)(t * t + t + t + 1);
    }
    return 0f;
}
```

**Result**: Clean waveforms even at high frequencies without audible aliasing.

### Filter - Moog Ladder Topology

4-pole cascade with feedback saturation for classic analog sound:

- **24dB/octave** low-pass slope
- **Self-oscillation** at high resonance
- **Soft saturation** in feedback path for stability
- **2x oversampling** for improved high-frequency response

### Reverb - Freeverb/Schroeder-Moorer

Industry-standard algorithmic reverb:

- **8 parallel comb filters** with damping
- **4 series allpass filters** for diffusion
- **Pre-delay** for room simulation
- **Stereo width** control

### Compressor - Professional Features

- **Look-ahead** buffer (up to 20ms)
- **Soft knee** for transparent compression
- **RMS or Peak** detection modes
- **Sidechain** input for ducking
- **Gain reduction metering** output

### Distortion - 4x Oversampling

Oversampling eliminates aliasing from non-linear waveshaping:

```
Input -> Upsample 4x -> Waveshape -> Downsample -> Output
```

Five waveshaping algorithms:
1. **Soft clip** - Tanh saturation
2. **Hard clip** - Digital clipping
3. **Tube** - Asymmetric soft clip
4. **Foldback** - Wave folding
5. **Asymmetric** - Different positive/negative response

## Advanced Examples

### Subtractive Synthesizer

```csharp
var graph = new EffectGraph { Name = "Subtractive Synth" };

// Oscillator with sawtooth
var osc = EffectNodeFactory.CreateNode("Oscillator");
osc.Parameters.First(p => p.Name == "Waveform").Value = 1f; // Sawtooth
osc.Parameters.First(p => p.Name == "Frequency").Value = 440f;

// Moog-style filter with envelope modulation
var filter = EffectNodeFactory.CreateNode("Filter");
filter.Parameters.First(p => p.Name == "Cutoff").Value = 2000f;
filter.Parameters.First(p => p.Name == "Resonance").Value = 0.7f;

// ADSR Envelope
var env = EffectNodeFactory.CreateNode("Envelope");
env.Parameters.First(p => p.Name == "Attack").Value = 10f;
env.Parameters.First(p => p.Name == "Decay").Value = 200f;
env.Parameters.First(p => p.Name == "Sustain").Value = 0.5f;
env.Parameters.First(p => p.Name == "Release").Value = 300f;

// VCA for amplitude
var vca = EffectNodeFactory.CreateNode("VCA");

// Output
var output = EffectNodeFactory.CreateNode("AudioOutput");

// Add all nodes
graph.AddNode(osc);
graph.AddNode(filter);
graph.AddNode(env);
graph.AddNode(vca);
graph.AddNode(output);

// Connections
graph.Connect(osc.Id, 0, filter.Id, 0);      // Osc -> Filter
graph.Connect(filter.Id, 0, vca.Id, 0);       // Filter -> VCA Audio
graph.Connect(env.Id, 0, vca.Id, 1);          // Envelope -> VCA CV
graph.Connect(vca.Id, 0, output.Id, 0);       // VCA -> Output
```

### Guitar Effects Chain

```csharp
var graph = new EffectGraph { Name = "Guitar Rig" };

var input = EffectNodeFactory.CreateNode("AudioInput");
var eq1 = EffectNodeFactory.CreateNode("EQ3Band");
var dist = EffectNodeFactory.CreateNode("Distortion");
var eq2 = EffectNodeFactory.CreateNode("EQ3Band");
var delay = EffectNodeFactory.CreateNode("Delay");
var reverb = EffectNodeFactory.CreateNode("Reverb");
var output = EffectNodeFactory.CreateNode("AudioOutput");

// Configure distortion (tube-style)
dist.Parameters.First(p => p.Name == "Drive").Value = 4f;
dist.Parameters.First(p => p.Name == "Type").Value = 2f; // Tube
dist.Parameters.First(p => p.Name == "Tone").Value = 0.6f;

// Configure delay
delay.Parameters.First(p => p.Name == "Time").Value = 350f;
delay.Parameters.First(p => p.Name == "Feedback").Value = 0.35f;
delay.Parameters.First(p => p.Name == "Mix").Value = 0.25f;

// Add and connect all nodes
foreach (var node in new[] { input, eq1, dist, eq2, delay, reverb, output })
    graph.AddNode(node);

graph.Connect(input.Id, 0, eq1.Id, 0);
graph.Connect(eq1.Id, 0, dist.Id, 0);
graph.Connect(dist.Id, 0, eq2.Id, 0);
graph.Connect(eq2.Id, 0, delay.Id, 0);
graph.Connect(delay.Id, 0, reverb.Id, 0);
graph.Connect(reverb.Id, 0, output.Id, 0);
```

### Auto-Wah with Envelope Follower

```csharp
var graph = new EffectGraph { Name = "Auto-Wah" };

var input = EffectNodeFactory.CreateNode("AudioInput");
var follower = EffectNodeFactory.CreateNode("Follower");
var filter = EffectNodeFactory.CreateNode("Filter");
var output = EffectNodeFactory.CreateNode("AudioOutput");

// Fast envelope follower
follower.Parameters.First(p => p.Name == "Attack").Value = 5f;
follower.Parameters.First(p => p.Name == "Release").Value = 50f;

// Filter in bandpass mode with high resonance
filter.Parameters.First(p => p.Name == "Mode").Value = 2f; // Bandpass
filter.Parameters.First(p => p.Name == "Resonance").Value = 0.8f;

graph.AddNode(input);
graph.AddNode(follower);
graph.AddNode(filter);
graph.AddNode(output);

// Audio goes through filter
graph.Connect(input.Id, 0, filter.Id, 0);
// Envelope controls filter cutoff
graph.Connect(input.Id, 0, follower.Id, 0);
graph.Connect(follower.Id, 0, filter.Id, 1);
// Output
graph.Connect(filter.Id, 0, output.Id, 0);
```

### Generative Music with Sequencer

```csharp
var graph = new EffectGraph { Name = "Generative Patch" };

var clock = EffectNodeFactory.CreateNode("Clock");
var seq = EffectNodeFactory.CreateNode("StepSequencer");
var quantizer = EffectNodeFactory.CreateNode("Quantizer");
var osc = EffectNodeFactory.CreateNode("Oscillator");
var filter = EffectNodeFactory.CreateNode("Filter");
var env = EffectNodeFactory.CreateNode("Envelope");
var vca = EffectNodeFactory.CreateNode("VCA");
var reverb = EffectNodeFactory.CreateNode("Reverb");
var output = EffectNodeFactory.CreateNode("AudioOutput");

// Clock at 120 BPM with swing
clock.Parameters.First(p => p.Name == "BPM").Value = 120f;
clock.Parameters.First(p => p.Name == "Swing").Value = 0.2f;

// Sequencer steps (random-ish pattern)
seq.Parameters.First(p => p.Name == "Step 1").Value = 0.3f;
seq.Parameters.First(p => p.Name == "Step 2").Value = 0.5f;
seq.Parameters.First(p => p.Name == "Step 3").Value = 0.4f;
seq.Parameters.First(p => p.Name == "Step 4").Value = 0.7f;
seq.Parameters.First(p => p.Name == "Step 5").Value = 0.35f;
seq.Parameters.First(p => p.Name == "Step 6").Value = 0.6f;
seq.Parameters.First(p => p.Name == "Step 7").Value = 0.45f;
seq.Parameters.First(p => p.Name == "Step 8").Value = 0.8f;

// Quantize to minor pentatonic
quantizer.Parameters.First(p => p.Name == "Scale").Value = 10f; // Minor Pentatonic
quantizer.Parameters.First(p => p.Name == "Root").Value = 0f;   // C

// Atmospheric reverb
reverb.Parameters.First(p => p.Name == "Size").Value = 0.85f;
reverb.Parameters.First(p => p.Name == "Mix").Value = 0.45f;

foreach (var node in new[] { clock, seq, quantizer, osc, filter, env, vca, reverb, output })
    graph.AddNode(node);

graph.Connect(clock.Id, 0, seq.Id, 0);        // Clock -> Sequencer
graph.Connect(seq.Id, 0, quantizer.Id, 0);    // Sequencer -> Quantizer
graph.Connect(quantizer.Id, 0, osc.Id, 0);    // Quantizer -> Osc Frequency
graph.Connect(osc.Id, 0, filter.Id, 0);       // Osc -> Filter
graph.Connect(clock.Id, 0, env.Id, 0);        // Clock -> Envelope Gate
graph.Connect(filter.Id, 0, vca.Id, 0);       // Filter -> VCA
graph.Connect(env.Id, 0, vca.Id, 1);          // Envelope -> VCA CV
graph.Connect(vca.Id, 0, reverb.Id, 0);       // VCA -> Reverb
graph.Connect(reverb.Id, 0, output.Id, 0);    // Reverb -> Output
```

## Presets / Patches

### Save Patch

```csharp
var patchManager = EffectPatchManager.Instance;
patchManager.SetPatchDirectory("C:/MusicEngine/Patches");

// Convert graph to patch
var patch = patchManager.CreatePatchFromGraph(graph, "Synths");
patch.Author = "Your Name";
patch.Tags.Add("bass");
patch.Tags.Add("analog");

// Save
await patchManager.SavePatchAsync(patch);
```

### Load Patch

```csharp
// Load patches
await patchManager.LoadPatchesAsync();

// Browse by category
var synthPatches = patchManager.GetPatchesByCategory("Synths");

// Convert patch to graph
var loadedGraph = patchManager.CreateGraphFromPatch(synthPatches.First());
```

### Factory Presets

These presets are available by default:

| Name | Category | Description |
|------|----------|-------------|
| Clean Amp | Basics | Simple amplifier |
| Simple Delay | Basics | Basic delay |
| Basic Reverb | Basics | Simple reverb |
| Basic Synth | Synths | Subtractive synth |
| Dual Osc Synth | Synths | Two-oscillator synth |
| FM Synth | Synths | FM synthesis |
| Guitar Amp | Guitar | Distortion with cab sim |
| Vocal Chain | Vocals | Compression + reverb |
| Drum Bus | Drums | Parallel compression |
| Auto-Wah | Modulation | Envelope-controlled filter |
| Tremolo | Modulation | LFO volume |
| Ring Mod Madness | Experimental | Ring modulation |
| Bitcrusher Lo-Fi | Experimental | Bit crushing |
| Generative | Experimental | Self-generating music |
| Master Bus | Mastering | Mastering chain |

## Creating Custom Nodes

```csharp
public class MyCustomNode : EffectNodeBase
{
    public override string NodeType => "MyCustomEffect";
    public override string Category => "Custom";
    public override string Description => "My custom audio effect";

    // Private state
    private float _state;

    protected override void InitializePorts()
    {
        // Define inputs and outputs
        AddInput("Audio In", PortDataType.Audio);
        AddInput("CV In", PortDataType.Control);
        AddOutput("Audio Out", PortDataType.Audio);
    }

    protected override void InitializeParameters()
    {
        // Define parameters
        AddParameter("Amount", 0.5f, 0f, 1f, "");
        AddParameter("Mode", 0f, 0f, 3f, "");
    }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        float amount = GetParam("Amount");
        float input = GetInput(0);
        float cv = GetInput(1);

        // Your signal processing here
        float output = input * (amount + cv * 0.5f);

        SetOutput(0, output);
    }
}

// Register node
EffectNodeFactory.Register<MyCustomNode>("MyCustomEffect");
```

## Performance Tips

1. **Topological Sorting**: The graph automatically sorts nodes for optimal processing order
2. **Buffer Reuse**: Nodes share buffers to save memory
3. **Bypass**: Disabled nodes (`IsEnabled = false`) are skipped
4. **Batch Processing**: Process larger buffers (512-2048 samples) for better performance
5. **Oversampling**: Only effects that need it (distortion, filter at high resonance) use oversampling

## Integration with Game Engines

The effect system can be combined with ExternalControlService:

```csharp
// Register variable for filter cutoff
ExternalControlService.Instance.RegisterVariable(
    "FilterCutoff",
    VariableType.Float,
    1000f,
    value => {
        var filter = graph.Nodes.Values
            .FirstOrDefault(n => n.NodeType == "Filter");
        if (filter != null)
            filter.Parameters.First(p => p.Name == "Cutoff").Value = (float)value;
    }
);

// Set from game engine
ExternalControlService.Instance.SetVariable("FilterCutoff", 2000f);
```

See also: [GameEngineIntegration.md](GameEngineIntegration.md) for more details on game engine integration.

## Available Scales (Quantizer)

| Index | Scale Name |
|-------|------------|
| 0 | Major / Ionian |
| 1 | Natural Minor / Aeolian |
| 2 | Dorian |
| 3 | Phrygian |
| 4 | Lydian |
| 5 | Mixolydian |
| 6 | Locrian |
| 7 | Harmonic Minor |
| 8 | Melodic Minor |
| 9 | Major Pentatonic |
| 10 | Minor Pentatonic |
| 11 | Blues |
| 12 | Whole Tone |
| 13 | Diminished |
| 14 | Chromatic |
