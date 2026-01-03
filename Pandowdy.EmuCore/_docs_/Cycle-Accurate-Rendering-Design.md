# Cycle-Accurate Rendering Architecture (Not Implemented)

## Status: Design Document for Future Implementation

**Date**: 2025-01-02  
**Current State**: Not implemented - "Good enough" throttling architecture in use  
**Purpose**: Document design approach for cycle-accurate rendering if/when needed

---

## Why Cycle-Accurate Rendering Isn't Currently Needed

The current architecture intentionally uses **simple throttling** rather than cycle-accurate frame synchronization:

### Current "Good Enough" Approach Works Because:

1. **Throttled mode** (99% of use cases):
   - Emulator runs at ~1 MHz, VBlank coincidentally aligns with GUI at ~60 Hz
   - Result: Smooth rendering that "looks right"
   - Side effect: Not cycle-accurate, but visually indistinguishable

2. **Fast-forward mode** (desired behavior):
   - Emulator runs at full speed, GUI samples at 60 Hz
   - Result: GUI shows latest state, works perfectly
   - Users expect this behavior in fast-forward

3. **Debug mode** (explicit control):
   - Manual stepping, render on-demand
   - Result: Frame rendering when needed
   - Cycle accuracy irrelevant when paused/stepping

## When Would Cycle-Accurate Rendering Be Needed?

Potential future scenarios that might require cycle-accurate rendering:

### 1. Copy Protection Schemes
**Likelihood**: Low  
**Reason**: Most Apple IIe copy protection relies on disk timing, not VBlank timing  
**Mitigation**: Switch to `CycleAccurateClockingController` if specific titles require it

### 2. Raster Effects (Mid-Frame Mode Changes)
**Likelihood**: Medium  
**Examples**: 
- Split-screen graphics (hi-res top, text bottom)
- Games that change video modes during active display
- Demo scene effects with mode switching per scanline

**Current Limitation**: Entire frame rendered at once with single mode  
**Mitigation**: Requires both cycle-accurate clocking AND per-pixel metadata (see below)

### 3. Timing-Sensitive Games
**Likelihood**: Low  
**Reason**: Work fine with throttled mode (coincidental sync is sufficient)  
**Mitigation**: If specific titles show issues, switch to cycle-accurate mode

### 4. Demo Scene Effects or Homebrew
**Likelihood**: Medium  
**Reason**: Community-developed software may test exact VBlank behavior  
**Mitigation**: Switch to cycle-accurate mode for these specific cases

---

## Architecture for Cycle-Accurate Rendering

### Overview

Cycle-accurate rendering requires **two components**:

1. **Clocking Strategy**: Synchronize frame rendering with emulated VBlank
2. **Per-Pixel Metadata**: Track which modes were active when each pixel was rendered

See: `Clocking-Architecture-Strategy-Pattern.md` for clocking strategies

### Component 1: Cycle-Accurate Clocking Strategies

#### Option A: New IClockingController Implementation

Create a dedicated controller that renders frames at VBlank:

```csharp
/// <summary>
/// Clocking strategy that synchronizes frame rendering with emulated VBlank.
/// </summary>
public class CycleAccurateClockingController : IClockingController
{
    private readonly SystemClock _systemClock;
    private readonly IFrameGenerator _frameGenerator;
    private readonly IFrameProvider _frameProvider;
    
    public ClockResult ExecuteCycle(Action clockAction)
    {
        bool wasInVBlank = _systemClock.IsInVBlank;
        
        // Tick system clock
        _systemClock.Tick();
        
        // Execute clock action (Bus.Clock)
        clockAction();
        
        // If VBlank just occurred, trigger frame render synchronously
        if (!wasInVBlank && _systemClock.IsInVBlank)
        {
            // Render frame at exact VBlank timing
            var context = _frameGenerator.AllocateRenderContext();
            _frameGenerator.RenderFrame(context);
            _frameProvider.CommitWritable(); // Swap buffers immediately
        }
        
        return new ClockResult
        {
            VBlankOccurred = !wasInVBlank && _systemClock.IsInVBlank,
            TotalCycles = _systemClock.Cycles
        };
    }
    
    // ... rest of implementation ...
}
```

**Integration**: Swap via DI:
```csharp
services.AddSingleton<IClockingController, CycleAccurateClockingController>();
```

#### Option B: Decorator Pattern (Recommended)

Wrap existing controller to add cycle-accurate frame sync:

```csharp
/// <summary>
/// Decorator that adds cycle-accurate frame sync to any clocking controller.
/// </summary>
public class FrameSyncDecorator : IClockingController
{
    private readonly IClockingController _inner;
    private readonly IFrameGenerator _frameGenerator;
    private readonly IFrameProvider _frameProvider;
    
    public FrameSyncDecorator(
        IClockingController inner,
        IFrameGenerator frameGenerator,
        IFrameProvider frameProvider)
    {
        _inner = inner;
        _frameGenerator = frameGenerator;
        _frameProvider = frameProvider;
    }
    
    public SystemClock SystemClock => _inner.SystemClock;
    
    public ClockResult ExecuteCycle(Action clockAction)
    {
        var result = _inner.ExecuteCycle(clockAction);
        
        // If VBlank occurred, render frame synchronously
        if (result.VBlankOccurred)
        {
            var context = _frameGenerator.AllocateRenderContext();
            _frameGenerator.RenderFrame(context);
            _frameProvider.CommitWritable();
        }
        
        return result;
    }
    
    // Delegate other methods to inner controller
    public Task RunAsync(Action clockAction, CancellationToken ct, double ticksPerSecond)
        => _inner.RunAsync(clockAction, ct, ticksPerSecond);
    
    public DebugRunResult RunUntil(Action clockAction, Func<bool> breakCondition, int maxCycles)
        => _inner.RunUntil(clockAction, breakCondition, maxCycles);
    
    public bool ThrottleEnabled
    {
        get => _inner.ThrottleEnabled;
        set => _inner.ThrottleEnabled = value;
    }
    
    public double TargetHz
    {
        get => _inner.TargetHz;
        set => _inner.TargetHz = value;
    }
    
    public void Reset() => _inner.Reset();
    public void Dispose() => _inner.Dispose();
}
```

**Integration**: Wrap existing controller:
```csharp
var baseController = new AutomaticClockingController();
var syncedController = new FrameSyncDecorator(
    baseController, 
    frameGenerator, 
    frameProvider);
    
va2m.SetClockingStrategy(syncedController);
```

**Why Decorator is Recommended**:
- ✅ Works with any existing controller (Automatic, Debug, future strategies)
- ✅ No code duplication
- ✅ Can be toggled on/off at runtime
- ✅ Preserves throttling and other behaviors from wrapped controller

#### Option C: Per-Instruction Timing

For maximum accuracy (each 6502 instruction takes specific cycle counts):

```csharp
public class InstructionAccurateClockingController : IClockingController
{
    private readonly SystemClock _systemClock;
    private readonly ICpu _cpu;
    
    public ClockResult ExecuteCycle(Action clockAction)
    {
        bool wasInVBlank = _systemClock.IsInVBlank;
        
        // Query CPU for how many cycles the next instruction will take
        int instructionCycles = _cpu.GetNextInstructionCycles();
        
        // Advance system clock by exact instruction cycle count
        for (int i = 0; i < instructionCycles; i++)
        {
            _systemClock.Tick();
            
            // Check for VBlank during instruction execution
            if (!wasInVBlank && _systemClock.IsInVBlank)
            {
                // VBlank occurred mid-instruction
                // Could render frame here or defer to end of instruction
            }
        }
        
        // Execute the instruction
        clockAction();
        
        return new ClockResult
        {
            VBlankOccurred = _systemClock.IsInVBlank,
            TotalCycles = _systemClock.Cycles
        };
    }
}
```

**When to use**: Very rare - only if per-cycle timing during instruction execution matters

---

### Component 2: Per-Pixel Mode Metadata (BitField32)

For raster effects that change modes mid-frame, we need to track **which mode was active for each pixel**.

#### Current Limitation

The `FrameProvider` stores mode information as frame-level flags:

```csharp
public class FrameProvider
{
    public bool IsMixedMode { get; set; }
    public bool IsTextMode { get; set; }
    // These apply to the entire frame
}
```

**Problem**: Can't represent mode changes that happen mid-frame (scanline effects, split-screen graphics, etc.)

#### Solution: Extend BitField16 to BitField32

**BitField16 Architecture** (existing, lower 16 bits):

```
Bit  0: Composite on-screen pixel (what actually displays)
Bits 1-15: Individual mode outputs (multi-bitplane architecture)

Bitplane breakdown:
- Bitplane 0:  Composite (final pixel: ON or OFF)
- Bitplane 1:  40-column text output for this pixel
- Bitplane 2:  80-column text output for this pixel
- Bitplane 3:  Lo-res graphics output for this pixel
- Bitplane 4:  Hi-res graphics output for this pixel
- Bitplane 5:  Double hi-res graphics output for this pixel
- Bitplanes 6-15: Additional modes or reserved
```

**Why BitField16 uses bitplanes:**
- Render all possible modes simultaneously in one pass
- Store each mode's "opinion" of what the pixel should be
- Let NTSC post-processing choose or blend bitplanes
- Support mode transitions without re-rendering

**BitField32 Addition** (upper 16 bits = mode metadata):

```csharp
/// <summary>
/// 32-bit field for pixel data with embedded mode metadata.
/// Used for cycle-accurate NTSC rendering with mid-frame mode changes.
/// </summary>
/// <remarks>
/// Extends BitField16's multi-bitplane architecture by adding mode metadata in the upper 16 bits.
/// 
/// <para>
/// <strong>BitField16 Architecture (Lower 16 bits):</strong>
/// The lower 16 bits represent 16 different bitplanes, each storing potential output from different
/// graphics modes. Each bitplane is a separate "view" of what that pixel would look like in that mode.
/// This allows post-processing (NTSC artifacts, mode blending) to choose or combine bitplanes.
/// </para>
/// 
/// <para>
/// <strong>Mode Metadata (Upper 16 bits):</strong>
/// The upper 16 bits (2 bytes) encode which modes were active when this pixel was rendered, providing
/// context for interpreting the bitplanes during NTSC artifact generation.
/// </para>
/// </remarks>
public readonly struct BitField32
{
    private readonly uint _value;
    
    // Lower 16 bits: BitField16 bitplane data
    // - Bit 0: Composite on-screen pixel (final output)
    // - Bits 1-15: Individual mode outputs (40-col text, 80-col, lo-res, hi-res, etc.)
    public BitField16 Bitplanes => new BitField16((ushort)(_value & 0xFFFF));
    
    // Direct access to composite (bitplane 0)
    public bool CompositePixel => ((_value & 0x0001) != 0);
    
    // Upper 16 bits: Mode metadata for this pixel (2 bytes of metadata)
    // Bits 16-19: Active video mode flags (4 bits = 16 possible mode combinations)
    public bool IsTextMode => (_value & 0x10000) != 0;      // Bit 16
    public bool IsMixedMode => (_value & 0x20000) != 0;     // Bit 17
    public bool IsHiRes => (_value & 0x40000) != 0;         // Bit 18
    public bool Is80Column => (_value & 0x80000) != 0;      // Bit 19
    
    // Bits 20-23: Additional mode flags (4 bits)
    public bool IsPage2 => (_value & 0x100000) != 0;        // Bit 20
    public bool IsAltCharSet => (_value & 0x200000) != 0;   // Bit 21
    public bool IsDoubleHiRes => (_value & 0x400000) != 0;  // Bit 22
    // Bit 23: Reserved
    
    // Bits 24-31: Timing/context metadata (8 bits)
    // Could encode: Scanline phase, color burst timing, etc.
    public byte TimingMetadata => (byte)((_value >> 24) & 0xFF);
    
    public BitField32(BitField16 bitplanes, ushort modeMetadata)
    {
        _value = bitplanes.RawValue | ((uint)modeMetadata << 16);
    }
    
    public BitField32(uint value)
    {
        _value = value;
    }
    
    /// <summary>
    /// Gets the raw 32-bit value (bitplanes + metadata).
    /// </summary>
    public uint RawValue => _value;
}
```

#### Example: Pixel at (100, 50)

```csharp
// BitField16 bitplanes (lower 16 bits):
Bitplane 0:  1  // Composite shows WHITE
Bitplane 1:  0  // 40-col text: background
Bitplane 2:  1  // 80-col text: character foreground
Bitplane 3:  0  // Lo-res: color 0
Bitplane 4:  1  // Hi-res: pixel ON
Bitplane 5:  1  // Double hi-res: color group 1

// Mode metadata (upper 16 bits):
IsTextMode:   0  // Not in text mode
IsMixedMode:  0  // Not in mixed mode
IsHiRes:      1  // Hi-res mode active
Is80Column:   0  // 40-column mode
```

NTSC renderer sees:
- Metadata says "Hi-res mode active"
- Reads bitplane 4 (hi-res output)
- Applies hi-res color artifacts based on neighbor pixels

---

### Renderer Implementation

#### Cycle-Accurate Renderer with Per-Pixel Metadata

```csharp
public class CycleAccurateRenderer : IDisplayBitmapRenderer
{
    public void Render(RenderContext context)
    {
        var snapshot = context.SystemStatus;
        
        for (int scanline = 0; scanline < 192; scanline++)
        {
            // Encode current mode metadata at start of scanline
            // (Mode could change mid-frame via soft switch writes)
            ushort modeMetadata = EncodeCurrentMode(snapshot);
            
            for (int x = 0; x < 280; x++)
            {
                // Render pixel across all bitplanes
                // Each graphics mode writes to its own bitplane
                var bitplanes = RenderAllModesForPixel(scanline, x, context);
                
                // Combine bitplanes + mode metadata
                var pixel = new BitField32(bitplanes, modeMetadata);
                
                // Store in frame buffer with metadata
                context.FrameBuffer.SetPixel(scanline, x, pixel);
            }
        }
    }
    
    private BitField16 RenderAllModesForPixel(int scanline, int x, RenderContext context)
    {
        ushort bitplanes = 0;
        
        // Bitplane 0: Composite (based on active mode)
        bool composite = GetCompositePixel(scanline, x, context);
        if (composite) bitplanes |= 0x0001;
        
        // Bitplane 1: 40-column text
        bool text40 = Get40ColumnTextPixel(scanline, x, context);
        if (text40) bitplanes |= 0x0002;
        
        // Bitplane 2: 80-column text
        bool text80 = Get80ColumnTextPixel(scanline, x, context);
        if (text80) bitplanes |= 0x0004;
        
        // Bitplane 3: Lo-res graphics
        bool lores = GetLoResPixel(scanline, x, context);
        if (lores) bitplanes |= 0x0008;
        
        // Bitplane 4: Hi-res graphics
        bool hires = GetHiResPixel(scanline, x, context);
        if (hires) bitplanes |= 0x0010;
        
        // Bitplane 5: Double hi-res
        bool dhires = GetDoubleHiResPixel(scanline, x, context);
        if (dhires) bitplanes |= 0x0020;
        
        // Bitplanes 6-15: Additional modes or reserved
        
        return new BitField16(bitplanes);
    }
    
    private ushort EncodeCurrentMode(SystemStatusSnapshot snapshot)
    {
        ushort metadata = 0;
        
        // Bits 0-3: Active mode flags
        if (snapshot.StateTextMode) metadata |= 0x0001;
        if (snapshot.StateMixed) metadata |= 0x0002;
        if (snapshot.StateHiRes) metadata |= 0x0004;
        if (snapshot.StateShow80Col) metadata |= 0x0008;
        
        // Bits 4-7: Additional mode flags
        if (snapshot.StatePage2) metadata |= 0x0010;
        if (snapshot.StateAltCharSet) metadata |= 0x0020;
        if (!snapshot.StateAnn3_DGR) metadata |= 0x0040; // DHGR enabled when Ann3 is OFF
        
        // Bits 8-15: Timing/context (could encode color burst phase, etc.)
        
        return metadata;
    }
}
```

#### NTSC Artifact Renderer Using Metadata

The UI's NTSC shader uses **both bitplanes and mode metadata** for accurate artifact generation:

```csharp
public class NTSCArtifactRenderer
{
    public void ApplyNTSCEffects(BitField32[,] pixels)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var pixel = pixels[y, x];
                
                // Use mode metadata to determine which bitplane is active
                if (pixel.IsTextMode && pixel.Is80Column)
                {
                    // Use bitplane 2 (80-column text) for artifact generation
                    bool isOn = pixel.Bitplanes.GetBitplane(2);
                    ApplyTextModeArtifacts(isOn, x, y);
                }
                else if (pixel.IsHiRes && pixel.IsDoubleHiRes)
                {
                    // Use bitplane 5 (double hi-res) for color fringing
                    bool isOn = pixel.Bitplanes.GetBitplane(5);
                    ApplyDoubleHiResArtifacts(isOn, x, y);
                }
                else
                {
                    // Use bitplane 0 (composite) as fallback
                    bool isOn = pixel.CompositePixel;
                    ApplyGenericArtifacts(isOn, x, y);
                }
                
                // Handle mode transitions mid-scanline
                if (x > 0 && pixels[y, x-1].IsTextMode != pixel.IsTextMode)
                {
                    // Can compare bitplanes across mode boundary for transition artifacts
                    ApplyModeTransitionArtifact(pixels[y, x-1], pixel, x, y);
                }
            }
        }
    }
    
    private void ApplyModeTransitionArtifact(BitField32 prevPixel, BitField32 currPixel, int x, int y)
    {
        // Example: Blend bitplanes at mode boundary
        // Previous scanline was text, current is hi-res
        bool textOutput = prevPixel.Bitplanes.GetBitplane(1);
        bool hiresOutput = currPixel.Bitplanes.GetBitplane(4);
        
        // Apply transition artifact (color bleeding, fringing, etc.)
        ApplyTransitionBlend(textOutput, hiresOutput, x, y);
    }
}
```

---

## Benefits of BitField32 Architecture

### Multi-Bitplane Design (BitField16)

1. **No re-rendering**: All modes rendered once, NTSC shader chooses bitplane
2. **Mode blending**: Can blend bitplanes at transition boundaries for smooth artifacts
3. **Historical context**: Previous scanline's bitplanes available for artifact generation
4. **Efficiency**: Single render pass, multiple outputs

### Adding Mode Metadata (Upper 16 bits)

1. **Active mode tracking**: Which bitplane should dominate this pixel
2. **Mode transitions**: Detect when mode changed mid-frame
3. **NTSC accuracy**: Color burst phase, timing information
4. **Context preservation**: State at time of rendering

### Example Use Case: Split-Screen Game

Game that switches from hi-res to text mid-frame:

```
Scanlines 0-159:   Hi-res graphics (top portion)
                   Metadata: IsHiRes=1, use bitplane 4
                   
Scanlines 160-191: Text mode (bottom status area)
                   Metadata: IsTextMode=1, use bitplane 1
                   
Scanline 160:      Transition
                   Metadata changes, blend bitplanes 1 & 4
```

NTSC renderer behavior:
- **Scanlines 0-159**: Read bitplane 4 (hi-res), apply hi-res color artifacts
- **Scanline 160**: Detect metadata change, blend bitplanes 1 & 4, render transition artifact
- **Scanlines 161-191**: Read bitplane 1 (text), apply text mode rendering

**Current `BitField16` alone**: Bitplanes are there, but no metadata about which is active  
**With `BitField32`**: Metadata guides NTSC renderer to correct bitplane

---

## Implementation Path

When cycle-accurate rendering is needed:

### Step 1: Create BitField32 Struct
```csharp
// File: Pandowdy.EmuCore/BitField32.cs
public readonly struct BitField32
{
    // ... implementation shown above ...
}
```

### Step 2: Update BitmapDataArray
```csharp
// Change from BitField16[] to BitField32[]
public class BitmapDataArray
{
    private readonly BitField32[] _data; // Changed from BitField16[]
    
    public void SetPixel(int scanline, int x, BitField32 pixel)
    {
        _data[scanline * 560 + x] = pixel;
    }
    
    public BitField32 GetPixel(int scanline, int x)
    {
        return _data[scanline * 560 + x];
    }
}
```

### Step 3: Modify Renderer
```csharp
// Update renderer to encode mode flags per-pixel
public class CycleAccurateRenderer : IDisplayBitmapRenderer
{
    public void Render(RenderContext context)
    {
        for (int scanline = 0; scanline < 192; scanline++)
        {
            ushort metadata = EncodeCurrentMode(context.SystemStatus);
            
            for (int x = 0; x < 280; x++)
            {
                var bitplanes = RenderAllModesForPixel(scanline, x, context);
                var pixel = new BitField32(bitplanes, metadata); // NEW
                context.FrameBuffer.SetPixel(scanline, x, pixel);
            }
        }
    }
}
```

### Step 4: Update NTSC Shader
```csharp
// NTSC renderer reads and uses mode metadata
public void ApplyNTSCEffects(BitField32[,] pixels)
{
    for (int y = 0; y < height; y++)
    {
        for (int x = 0; x < width; x++)
        {
            var pixel = pixels[y, x];
            
            // Use metadata to select correct bitplane
            if (pixel.IsHiRes)
            {
                bool hiresPixel = pixel.Bitplanes.GetBitplane(4);
                ApplyHiResArtifacts(hiresPixel, x, y);
            }
            // ... other modes ...
        }
    }
}
```

### Step 5: No Changes to IFrameProvider Interface
Internal change only - interface remains the same

### Backward Compatibility

- `BitField32.Bitplanes` exposes original `BitField16` data
- Renderers that ignore metadata just use bitplane 0 (composite)
- Existing code continues to work

---

## Memory Impact

| Configuration | Memory per Frame | Details |
|--------------|------------------|---------|
| **Current (BitField16)** | ~210 KB | 560×192 × 2 bytes = 215,040 bytes |
| **With BitField32** | ~420 KB | 560×192 × 4 bytes = 430,080 bytes |
| **Increase** | ~210 KB | Acceptable for modern systems |

**What we gain**: 
- Mode metadata without re-rendering
- Accurate NTSC artifacts at mode boundaries
- Support for raster effects and mid-frame mode changes

---

## Design Rationale

### Why Not Implemented Now

- ✅ **Simpler implementation** - Less code, easier to test
- ✅ **Better performance** - No synchronization overhead between threads
- ✅ **Flexible** - Easy to add accuracy later if needed
- ✅ **Pragmatic** - Solves 99% of use cases without over-engineering
- ✅ **Maintainable** - Clean separation via Strategy pattern

### Extension Hooks in Place

- ✅ `IClockingController` - Swap strategies at runtime
- ✅ `ExecuteCycle()` - Override to add frame sync
- ✅ Decorator pattern - Wrap existing behavior
- ✅ DI injection - No code changes to VA2M
- ✅ **BitField extensibility** - `BitField16` → `BitField32` for per-pixel metadata

### When to Implement

Revisit this design if:
1. Copy protection schemes require exact VBlank timing
2. Raster effects or split-screen games show visual artifacts
3. Timing-sensitive software shows issues
4. Demo scene effects or homebrew require exact timing
5. Community requests cycle-accurate rendering

---

## Related Documentation

- **Clocking Strategies**: See `Clocking-Architecture-Strategy-Pattern.md`
- **BitField16 Architecture**: Current implementation in `Pandowdy.EmuCore/BitField16.cs`
- **Frame Rendering**: See interface documentation for `IFrameGenerator`, `IFrameProvider`

---

**Decision Date**: 2025-01-02  
**Status**: Design documented, not implemented  
**Review Trigger**: Visual artifacts in raster effects or timing-sensitive software  
**Path Forward**: Extension points ready to use if needed
