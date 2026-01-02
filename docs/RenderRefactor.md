# Rendering Refactoring Plan

## Overview

This document outlines the refactoring of the VA2M rendering system from a monolithic `VA2M.Render.cs` partial class into a modular, multi-plane rendering architecture using dependency injection.

## Architecture Goals

1. **Separation of Concerns**: Extract rendering logic from VA2M into dedicated renderer classes
2. **Multi-Plane Rendering**: Always render all video modes to separate bitplanes for debugging
3. **Compositor Pattern**: Master renderer composites active display from bitplanes based on soft switches
4. **DI Integration**: Leverage `IEnumerable<IRenderer>` pattern for extensibility
5. **Debug Support**: Enable viewing any video mode regardless of current soft switch state

---

## Key Architectural Decisions

### Display Resolution: 560×192

- **Apple II resolution**: 560×192 pixels
- **Rationale**: 
  - Apple II pixels are effectively 2 display pixels horizontally except in DHGR/DGR/80Col Text
  - Even in standard HiRes, the high-bit shift shifts a 2-bit-wide pixel by 1 "half pixel", thus there is effectively a width of 560 potential pixels
  - Enables accurate NTSC color artifact simulation
  - Proper representation of color fringing between adjacent pixels
  - No vertical scaling (maintains 192 scanlines)

### Memory Footprint

```
Single bitplane: 560 × 192 × 1 bit = 107520 bits (13440 bytes)
Five bitplanes: 430,080 × 5 = 2,150,400 bytes ≈ 2.05 MB
Total frame buffer: ~2 MB (reasonable for modern systems)
```

---

## Core Components

### 1. MultiPlaneFrameBuffer

The fundamental data structure holding all video mode bitplanes.

```csharp
// Pandowdy.EmuCore/Rendering/MultiPlaneFrameBuffer.cs
namespace Pandowdy.EmuCore.Rendering;

public class MultiPlaneFrameBuffer<T>(int width, int height)
{
    public const int Width = 560;
    public const int Height = 192;
    public const int PlanesAvailable = // Bit width of T
    
    // Bitplane 0: Active display (composited based on soft switches)
    // Bitplane 1: TEXT40 mode (always rendered)
    // Bitplane 2: TEXT40P2 mode (always rendered)
    // Bitplane 3: TEXT80 mode (always rendered)
    // Bitplane 4: LORES mode (always rendered)
    // Bitplane 5: LORESP2 mode (always rendered)
    // Bitplane 6: LORES80 mode (always rendered)
    // Bitplane 7: HIRESP1 (always rendered)
    // Bitplane 8: HIRESP2 (always rendered)
    // Bitplane 9: HIRESP1AUX (always rendered)
    // Bitplane 10: HIRESP2AUX (always rendered)
    // Bitplane 11: DHIRES (always rendered)
    // Bitplanes 12-(PlanesAvailble-1) Reserved

    private T[] backingStore = new <T>[Width*Height]

    public void Clear() { ... Clear all bitplanes ... }

    public void SetPixel(uint x, uint y, uint bitplane, bool value=true) 
    { 
        ... bit shift pixel into proper plane at x + y*width 
        ... optional "value" can be used to set a particular value instead of having to use set vs clear methods separately
    }

    public void ClearPixel(uint x, uint y, uint bitplane) { ... clear pixel at proper plane ... }

    public bool PixelAt(uint x, uint y, uint bitplane) { ... get pixel from proper plane ... }

    public T GetBitPlaneValuesAt(uint x, uint y) { ... return the entire value at x,y ... }

    public T[] GetBitPlane(int planeNum) { ... should return all values ANDed with proper mask ... non-zero = on ... }

    public T[] GetAllPlanes() { ... return the backing store ... used for manual decoding if necessary ... }

    public void ClearBitPlane(int planenum) { ... zeroes all pixels in given bitplane layer ... }

    public void CopyLinesBetweenPlanes(int startLine, int endLine, int sourcePlane, int destPlane) { ... }

    public void CopyRegionBetweenPlanes(int left, int top, int right, int bottom, int sourcePlane, int destPlane) { ... }

    public void CopyPixelBetweenPlanes(int x, int y, int sourcePlane, int destPlane) { ... }
    
}
```

---

### 2. RenderContext

Encapsulates all data needed for rendering.

```csharp
// Pandowdy.EmuCore/Rendering/RenderContext.cs
namespace Pandowdy.EmuCore.Rendering;

public class RenderContext
{
    /// <summary>
    /// Multi-plane frame buffer with all bitplanes
    /// </summary>
    public required MultiPlaneFrameBuffer<Uint16> FrameBuffer { get; init; }
    
    /// <summary>
    /// Direct access to Apple II memory
    /// </summary>
    public required IDirectMemoryPoolReader Memory { get; init; }
    
    /// <summary>
    /// System status for soft switch state
    /// </summary>
    public required ISystemStatusProvider Status { get; init; }
    
    /// <summary>
    /// Character ROM for text rendering
    /// </summary>
    public required ReadOnlySpan<byte> CharacterRom { get; init; }
}
```

---

### 3. IRenderer Interface

Base interface for all video mode renderers.

```csharp
// Pandowdy.EmuCore/Rendering/IRenderer.cs
namespace Pandowdy.EmuCore.Rendering;

public interface IRenderer
{
    /// <summary>
    /// The bitplane this renderer writes to (1-N, 0 reserved for active display)
    /// </summary>
    int TargetBitplane { get; }
    
    /// <summary>
    /// Render this video mode to its dedicated bitplane
    /// </summary>
    void Render(RenderContext context);
}
```

---

### 4. Individual Renderers

#### Text Mode Renderer

```csharp
// Pandowdy.EmuCore/Rendering/TextModeRenderer.cs
namespace Pandowdy.EmuCore.Rendering;

public class TextModeRenderer : IRenderer
{
    public int TargetBitplane => 1;  // Always render to bitplane 1
    
    private static readonly int[] TextRowOffsets = 
    {
        0x000, 0x080, 0x100, 0x180, 0x200, 0x280, 0x300, 0x380,
        0x028, 0x0A8, 0x128, 0x1A8, 0x228, 0x2A8, 0x328, 0x3A8,
        0x050, 0x0D0, 0x150, 0x1D0, 0x250, 0x2D0, 0x350, 0x3D0
    };
    
    public void Render(RenderContext ctx)
    {
        var plane = ctx.FrameBuffer.GetPlane(TargetBitplane);
        
        // Render TEXT screen (40x24) to bitplane 1
        // This ALWAYS runs, regardless of soft switches
        for (int row = 0; row < 24; row++)
        {
            for (int col = 0; col < 40; col++)
            {
                byte ch = GetCharAtPosition(ctx.Memory, row, col, ctx.Status.StatePage2);
                RenderCharacter(plane, ch, row, col, ctx.CharacterRom, ctx.Status.StateAltCharSet);
            }
        }
    }
    
    private byte GetCharAtPosition(IDirectMemoryPoolReader mem, int row, int col, bool page2)
    {
        int baseAddr = page2 ? 0x0800 : 0x0400;
        int offset = TextRowOffsets[row] + col;
        return mem.ReadRawMain(baseAddr + offset);
    }
    
    private void RenderCharacter(Span<uint> plane, byte ch, int row, int col, 
                                 ReadOnlySpan<byte> charRom, bool altCharSet)
    {
        int charIndex = ch & 0x7F;
        bool inverse = (ch & 0x80) == 0 && (ch & 0x40) == 0;
        bool flash = (ch & 0xC0) == 0x40;
        
        int romOffset = (altCharSet ? 0x400 : 0) + (charIndex * 8);
        
        int destX = col * 14;  // 14 display pixels per character (7 Apple pixels × 2)
        int destY = row * 8;   // 8 scanlines per character
        
        for (int y = 0; y < 8; y++)
        {
            byte charLine = charRom[romOffset + y];
            if (inverse) charLine = (byte)~charLine;
            
            int pixelIndex = (destY + y) * MultiPlaneFrameBuffer.DisplayWidth + destX;
            
            for (int x = 0; x < 7; x++)  // 7 Apple II pixels
            {
                ... Render Character Pixels ...
            }
        }
    }
}
```

#### HiRes Renderer

```csharp
// Pandowdy.EmuCore/Rendering/HiResRenderer.cs
namespace Pandowdy.EmuCore.Rendering;

public class HiResRenderer : IRenderer
{
    private readonly int _page;
    
    private static readonly int[] HiResLineOffsets = new int[192];
    
    static HiResRenderer()
    {
        // Initialize line offset table
        for (int y = 0; y < 192; y++)
        {
            int section = y / 64;
            int row = (y % 8);
            int group = (y % 64) / 8;
            HiResLineOffsets[y] = (section * 0x28) + (row * 0x400) + (group * 0x80);
        }
    }
    
    public HiResRenderer(int page)
    {
        if (page != 1 && page != 2)
            throw new ArgumentException("Page must be 1 or 2", nameof(page));
        _page = page;
    }
    
    public int TargetBitplane => _page == 1 ? 3 : 4;
    
    public void Render(RenderContext ctx)
    {
        var plane = ctx.FrameBuffer.GetPlane(TargetBitplane);
        int baseAddr = _page == 1 ? 0x2000 : 0x4000;
        
        for (int y = 0; y < 192; y++)
        {
            int lineAddr = baseAddr + HiResLineOffsets[y];
            
            for (int byteX = 0; byteX < 40; byteX++)  // 40 bytes per line
            {
                byte b = ctx.Memory.ReadRawMain(lineAddr + byteX);
                RenderHiResByte(plane, b, y, byteX);
            }
        }
    }
    
    private void RenderHiResByte(Span<uint> plane, byte b, int y, int byteX)
    {
        int destX = byteX * 14;  // 14 display pixels per byte (7 Apple pixels × 2)
        int pixelIndex = y * MultiPlaneFrameBuffer.DisplayWidth + destX;
        
        bool highBit = (b & 0x80) != 0;  // Color palette selector
        
        for (int bit = 0; bit < 7; bit++)  // 7 Apple II pixels per byte
        {
            bool pixelOn = (b & (1 << bit)) != 0;
            
            ... Render HiRes Pixels ...
        }
    }
}
```

#### LoRes Renderer

```csharp
// Pandowdy.EmuCore/Rendering/LoResRenderer.cs
namespace Pandowdy.EmuCore.Rendering;

public class LoResRenderer : IRenderer
{
    public int TargetBitplane => 2;
    
    private static readonly int[] TextRowOffsets = 
    {
        0x000, 0x080, 0x100, 0x180, 0x200, 0x280, 0x300, 0x380,
        0x028, 0x0A8, 0x128, 0x1A8, 0x228, 0x2A8, 0x328, 0x3A8,
        0x050, 0x0D0, 0x150, 0x1D0, 0x250, 0x2D0, 0x350, 0x3D0
    };
    
    public void Render(RenderContext ctx)
    {
        var plane = ctx.FrameBuffer.GetPlane(TargetBitplane);
        
        // 40 blocks × 48 rows (each block is 14×4 display pixels)
        for (int blockRow = 0; blockRow < 48; blockRow++)
        {
            for (int blockCol = 0; blockCol < 40; blockCol++)
            {
                byte nibbles = GetLoResNibbles(ctx.Memory, blockRow, blockCol, ctx.Status.StatePage2);
                RenderLoResBlock(plane, nibbles, blockRow, blockCol);
            }
        }
    }
    
    private byte GetLoResNibbles(IDirectMemoryPoolReader mem, int blockRow, int blockCol, bool page2)
    {
        int textRow = blockRow / 2;
        int baseAddr = page2 ? 0x0800 : 0x0400;
        int offset = TextRowOffsets[textRow] + blockCol;
        return mem.ReadRawMain(baseAddr + offset);
    }
    
    private void RenderLoResBlock(Span<uint> plane, byte nibbles, int blockRow, int blockCol)
    {

    }
}
```

---

### 5. MasterRenderer (Compositor)

Orchestrates all renderers and composites the active display.

```csharp
// Pandowdy.EmuCore/Rendering/MasterRenderer.cs
namespace Pandowdy.EmuCore.Rendering;

public class MasterRenderer
{
    private readonly IEnumerable<IRenderer> _renderers;
    
    public MasterRenderer(IEnumerable<IRenderer> renderers)
    {
        _renderers = renderers;
    }
    
    public void Render(RenderContext context)
    {
        // Step 1: Render ALL modes to their bitplanes (always!)
        foreach (var renderer in _renderers)
        {
            renderer.Render(context);
        }
        
        // Step 2: Composite active display based on soft switches
        CompositeActiveDisplay(context);
    }
    
    private void CompositeActiveDisplay(RenderContext ctx)
    {
        var fb = ctx.FrameBuffer;
        var status = ctx.Status;
        
        if (status.StateTextMode)
        {
            // Pure text mode - copy TEXT bitplane to active
            fb.TextMode.CopyTo(fb.ActiveDisplay, 0);
        }
        else if (status.StateMixed)
        {
            // Mixed mode - graphics + text bottom 4 lines
            CompositeMixedMode(ctx);
        }
        else if (status.StateHiRes)
        {
            // Pure hires - copy appropriate HIRES bitplane
            var source = status.StatePage2 ? fb.HiRes2Mode : fb.HiRes1Mode;
            source.CopyTo(fb.ActiveDisplay, 0);
        }
        else
        {
            // Pure lores
            fb.LoResMode.CopyTo(fb.ActiveDisplay, 0);
        }
    }
    
    private void CompositeMixedMode(RenderContext ctx)
    {
        var fb = ctx.FrameBuffer;
        var status = ctx.Status;
        
        const int MixedSplitLine = 160;  // Line 20 * 8
        const int PixelsAboveSplit = MixedSplitLine * MultiPlaneFrameBuffer.DisplayWidth;
        const int PixelsBelowSplit = (MultiPlaneFrameBuffer.Height - MixedSplitLine) 
                                     * MultiPlaneFrameBuffer.DisplayWidth;
        
        // Top: Graphics (HIRES or LORES)
        var graphicsSource = status.StateHiRes 
            ? (status.StatePage2 ? fb.HiRes2Mode : fb.HiRes1Mode)
            : fb.LoResMode;
        
        Array.Copy(graphicsSource, 0, fb.ActiveDisplay, 0, PixelsAboveSplit);
        
        // Bottom: TEXT
        Array.Copy(fb.TextMode, PixelsAboveSplit, 
                   fb.ActiveDisplay, PixelsAboveSplit, 
                   PixelsBelowSplit);
    }
}
```

---

### 6. Enhanced FrameProvider

The service that owns the frame buffer and coordinates with renderers.

```csharp
// Pandowdy.EmuCore/Services/FrameProvider.cs
using System;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Rendering;

namespace Pandowdy.EmuCore.Services;

public class FrameProvider : IFrameProvider, IDebugFrameProvider
{
    private readonly MultiPlaneFrameBuffer _frameBuffer;
    private readonly object _lock = new();
    
    // Properties for NTSC renderer hints
    public bool IsGraphics { get; set; }
    public bool IsMixed { get; set; }
    
    public FrameProvider()
    {
        _frameBuffer = new MultiPlaneFrameBuffer();
    }
    
    // ===== IFrameProvider (for active display) =====
    
    /// <summary>
    /// Get the multi-plane frame buffer for rendering
    /// </summary>
    public MultiPlaneFrameBuffer BorrowWritableFrameBuffer()
    {
        lock (_lock)
        {
            return _frameBuffer;
        }
    }
    
    /// <summary>
    /// Get the active display buffer for UI rendering
    /// </summary>
    public ReadOnlySpan<uint> BorrowReadable()
    {
        lock (_lock)
        {
            return _frameBuffer.ActiveDisplay;
        }
    }
    
    /// <summary>
    /// Signal that rendering is complete and UI can read
    /// </summary>
    public void CommitWritable()
    {
        lock (_lock)
        {
            FrameReady?.Invoke(this, EventArgs.Empty);
        }
    }
    
    /// <summary>
    /// Event raised when a new frame is ready
    /// </summary>
    public event EventHandler? FrameReady;
    
    // ===== IDebugFrameProvider (for debug views) =====
    
    /// <summary>
    /// Get a specific bitplane for debugging
    /// </summary>
    public ReadOnlySpan<uint> GetBitplane(int planeIndex)
    {
        lock (_lock)
        {
            return _frameBuffer.GetPlane(planeIndex);
        }
    }
    
    /// <summary>
    /// Get names of available bitplanes
    /// </summary>
    public IReadOnlyList<string> GetBitplaneNames()
    {
        return new[] 
        { 
            "Active Display", 
            "TEXT Mode", 
            "LORES Mode", 
            "HIRES Page 1", 
            "HIRES Page 2" 
        };
    }
}
```

#### Updated Interfaces

```csharp
// Pandowdy.EmuCore/Interfaces/IFrameProvider.cs
namespace Pandowdy.EmuCore.Interfaces;

public interface IFrameProvider
{
    /// <summary>
    /// Borrow the multi-plane frame buffer for rendering
    /// </summary>
    MultiPlaneFrameBuffer BorrowWritableFrameBuffer();
    
    /// <summary>
    /// Get the active display for UI rendering
    /// </summary>
    ReadOnlySpan<uint> BorrowReadable();
    
    /// <summary>
    /// Signal that a new frame is ready
    /// </summary>
    void CommitWritable();
    
    /// <summary>
    /// Event raised when a new frame is ready
    /// </summary>
    event EventHandler? FrameReady;
    
    /// <summary>
    /// Hint: Is the display in graphics mode?
    /// </summary>
    bool IsGraphics { get; set; }
    
    /// <summary>
    /// Hint: Is the display in mixed mode?
    /// </summary>
    bool IsMixed { get; set; }
}

public interface IDebugFrameProvider
{
    /// <summary>
    /// Get a specific bitplane for debugging (0-4)
    /// </summary>
    ReadOnlySpan<uint> GetBitplane(int planeIndex);
    
    /// <summary>
    /// Get names of available bitplanes
    /// </summary>
    IReadOnlyList<string> GetBitplaneNames();
}
```

---

### 7. Updated VA2M Integration

```csharp
// Pandowdy.EmuCore/VA2M.cs (OnVBlank method)
public partial class VA2M : IDisposable
{
    private readonly IFrameProvider _frameSink;
    private readonly MasterRenderer _renderer;
    private readonly ISystemStatusProvider _sysStatusSink;
    private readonly MemoryPool _memoryPool;
    private ReadOnlySpan<byte> _characterRom;
    
    public VA2M(
        IEmulatorState stateSink, 
        IFrameProvider frameSink, 
        ISystemStatusProvider statusProvider, 
        IAppleIIBus bus, 
        MemoryPool memoryPool,
        MasterRenderer renderer)  // ← Injected
    {
        ArgumentNullException.ThrowIfNull(stateSink);
        ArgumentNullException.ThrowIfNull(frameSink);
        ArgumentNullException.ThrowIfNull(statusProvider);
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(memoryPool);
        ArgumentNullException.ThrowIfNull(renderer);
        
        _stateSink = stateSink;
        _frameSink = frameSink;
        _sysStatusSink = statusProvider;
        Bus = bus;
        MemoryPool = memoryPool;
        _renderer = renderer;
        
        TryLoadEmbeddedRom("Pandowdy.EmuCore.Resources.a2e_enh_c-f.rom");
        LoadCharacterRom();
        
        if (Bus is VA2MBus vb)
        {
            vb.VBlank += OnVBlank;
        }
        
        _flashTimer = new Timer(_ =>
        {
            try
            {
                Interlocked.Exchange(ref _pendingFlashToggle, 1);
            }
            catch { }
        }, null, FlashPeriod, FlashPeriod);
    }
    
    private void OnVBlank(object? sender, EventArgs e)
    {
        // Apply pending flash toggle
        if (Interlocked.Exchange(ref _pendingFlashToggle, 0) != 0)
        {
            _sysStatusSink.Mutate(s => s.StateFlashOn = !s.StateFlashOn);
        }
        
        // Borrow the multi-plane frame buffer
        var frameBuffer = _frameSink.BorrowWritableFrameBuffer();
        frameBuffer.Clear();
        
        // Create render context
        var context = new RenderContext
        {
            FrameBuffer = frameBuffer,
            Memory = MemoryPool,
            Status = _sysStatusSink,
            CharacterRom = _characterRom
        };
        
        // Render all bitplanes and composite active display
        _renderer.Render(context);
        
        // Update hints for UI
        _frameSink.IsGraphics = !_sysStatusSink.StateTextMode;
        _frameSink.IsMixed = _sysStatusSink.StateMixed;
        
        // Signal frame ready
        _frameSink.CommitWritable();
    }
    
    private void LoadCharacterRom()
    {
        // Load character ROM from embedded resource
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream("Pandowdy.EmuCore.Resources.a2e_enh_video.rom");
        if (stream != null)
        {
            byte[] buffer = new byte[stream.Length];
            stream.Read(buffer, 0, buffer.Length);
            _characterRom = buffer;
        }
    }
}
```

---

## Dependency Injection Setup

```csharp
// Pandowdy/Program.cs
.ConfigureServices((context, services) =>
{
    // Core services
    services.AddSingleton<MemoryPool>();
    services.AddSingleton<ICpu, CPUAdapter>();
    services.AddSingleton<IAppleIIBus, VA2MBus>();
    
    // Frame provider (owns multi-plane buffer)
    services.AddSingleton<FrameProvider>();
    services.AddSingleton<IFrameProvider>(sp => sp.GetRequiredService<FrameProvider>());
    services.AddSingleton<IDebugFrameProvider>(sp => sp.GetRequiredService<FrameProvider>());
    
    // State providers
    services.AddSingleton<IEmulatorState, EmulatorStateProvider>();
    services.AddSingleton<SystemStatusProvider>();
    services.AddSingleton<ISystemStatusProvider>(sp => sp.GetRequiredService<SystemStatusProvider>());
    services.AddSingleton<ISoftSwitchResponder>(sp => sp.GetRequiredService<SystemStatusProvider>());
    
    // Renderers (in order of bitplane assignment)
    services.AddSingleton<IRenderer, TextModeRenderer>();
    services.AddSingleton<IRenderer, LoResRenderer>();
    services.AddSingleton<IRenderer>(sp => new HiResRenderer(page: 1));
    services.AddSingleton<IRenderer>(sp => new HiResRenderer(page: 2));
    
    // Master compositor
    services.AddSingleton<MasterRenderer>();
    
    // Main emulator
    services.AddSingleton<VA2M>();
    
    // UI services
    services.AddSingleton<IRefreshTicker, AvaloniaRefreshTicker>();
    services.AddSingleton<IMainWindowFactory, MainWindowFactory>();
    
    // ViewModels
    services.AddTransient<MainWindowViewModel>();
    services.AddTransient<EmulatorStateViewModel>();
    services.AddTransient<SystemStatusViewModel>();
});
```

---

## Rendering Flow

```
60 Hz VBlank Event
    ↓
VA2M.OnVBlank()
    ↓
    ├─ Apply flash toggle (2.1 Hz cursor blink)
    ├─ Borrow MultiPlaneFrameBuffer from FrameProvider
    ├─ Clear all bitplanes to black
    ├─ Create RenderContext (buffer + memory + status + charRom)
    ├─ Call MasterRenderer.Render(context)
    │   ↓
    │   ├─ TextModeRenderer.Render() → Bitplane 1
    │   ├─ LoResRenderer.Render() → Bitplane 2
    │   ├─ HiRes1Renderer.Render() → Bitplane 3
    │   ├─ HiRes2Renderer.Render() → Bitplane 4
    │   └─ Composite based on soft switches → Bitplane 0 (active)
    │       ↓
    │       if (StateTextMode)
    │           Copy Bitplane 1 → Bitplane 0
    │       else if (StateMixed)
    │           Copy graphics (top 160 lines) → Bitplane 0
    │           Copy text (bottom 32 lines) → Bitplane 0
    │       else if (StateHiRes)
    │           Copy Bitplane 3 or 4 → Bitplane 0
    │       else
    │           Copy Bitplane 2 → Bitplane 0
    │
    ├─ Set IsGraphics, IsMixed hints
    └─ FrameProvider.CommitWritable()
        ↓
        Raises FrameReady event
            ↓
        Apple2Display.OnFrameReady() (UI thread)
            ↓
        Copy ActiveDisplay to WriteableBitmap
            ↓
        InvalidateVisual()
            ↓
        Avalonia renders to screen
```

---

## Benefits

| Benefit | Description |
|---------|-------------|
| **Separation of Concerns** | Each renderer handles one video mode |
| **Always-On Rendering** | All video modes rendered every frame for debugging |
| **Debug Visibility** | View any video mode anytime via `IDebugFrameProvider` |
| **Hot-Swapping** | Change displayed mode without re-rendering |
| **Testability** | Test each renderer independently |
| **Extensibility** | Add new renderers (80-col, DHR) without touching existing code |
| **DI Integration** | Clean `IEnumerable<IRenderer>` pattern |
| **Performance** | Single allocation, Span-based rendering, optional parallel rendering |
| **Thread-Safe** | FrameProvider uses locking for cross-thread access |

---

## Future Enhancements

### 1. Double Hi-Res (DHR)

```csharp
public class DoubleHiResRenderer : IRenderer
{
    public int TargetBitplane => 5;  // New bitplane for DHR
    
    public void Render(RenderContext ctx)
    {
        // Render 560×192 double hi-res
        // Interleave main and aux memory
    }
}
```

### 2. 80-Column Text

```csharp
public class Text80ColRenderer : IRenderer
{
    public int TargetBitplane => 6;  // New bitplane for 80-col
    
    public void Render(RenderContext ctx)
    {
        // Render 80×24 text mode
        // Interleave main and aux memory
    }
}
```

### 3. Parallel Rendering

```csharp
public void Render(RenderContext context)
{
    // Render all bitplanes in parallel
    Parallel.ForEach(_renderers, renderer =>
    {
        renderer.Render(context);
    });
    
    // Composite on main thread (fast operation)
    CompositeActiveDisplay(context);
}
```

### 4. Dirty Region Tracking

```csharp
public class DirtyRegionTracker
{
    private bool[] _dirtyPlanes = new bool[5];
    
    public void MarkDirty(int plane) => _dirtyPlanes[plane] = true;
    public bool IsDirty(int plane) => _dirtyPlanes[plane];
    public void ClearDirty(int plane) => _dirtyPlanes[plane] = false;
}
```

### 5. Recording/Playback

```csharp
public class BitplaneRecorder
{
    public void RecordFrame(MultiPlaneFrameBuffer frame) { }
    public void PlaybackFrame(int frameNumber) { }
}
```

---

## Performance Optimization

### Span-Based Rendering

```csharp
private void RenderCharacter(Span<uint> plane, ...)
{
    // Direct span access - no bounds checking overhead
    int pixelIndex = (destY + y) * 560 + destX;
    
    // Unrolled loop for 7 Apple II pixels → 14 display pixels
    plane[pixelIndex + 0] = color0; plane[pixelIndex + 1] = color0;
    plane[pixelIndex + 2] = color1; plane[pixelIndex + 3] = color1;
    plane[pixelIndex + 4] = color2; plane[pixelIndex + 5] = color2;
    plane[pixelIndex + 6] = color3; plane[pixelIndex + 7] = color3;
    plane[pixelIndex + 8] = color4; plane[pixelIndex + 9] = color4;
    plane[pixelIndex + 10] = color5; plane[pixelIndex + 11] = color5;
    plane[pixelIndex + 12] = color6; plane[pixelIndex + 13] = color6;
}
```

### Fast Memory Copy

```csharp
// Using Span<T>.CopyTo for efficient memory copy
private void CompositeActiveDisplay(RenderContext ctx)
{
    var source = ctx.FrameBuffer.GetPlane(sourcePlane);
    var dest = ctx.FrameBuffer.ActiveDisplay.AsSpan();
    source.CopyTo(dest);
}
```

---

## Testing Strategy

### Unit Tests

```csharp
[Fact]
public void TextModeRenderer_Renders40x24Characters()
{
    // Arrange
    var renderer = new TextModeRenderer();
    var frameBuffer = new MultiPlaneFrameBuffer();
    var mockMemory = CreateMockMemory();
    var mockStatus = CreateMockStatus();
    var charRom = LoadCharacterRom();
    
    var context = new RenderContext
    {
        FrameBuffer = frameBuffer,
        Memory = mockMemory,
        Status = mockStatus,
        CharacterRom = charRom
    };
    
    // Act
    renderer.Render(context);
    
    // Assert
    var textPlane = frameBuffer.TextMode;
    Assert.NotEqual(0xFF000000, textPlane[0]);  // Not all black
}
```

### Integration Tests

```csharp
[Fact]
public void MasterRenderer_CompositesCorrectly_WhenInTextMode()
{
    // Arrange
    var renderers = new List<IRenderer>
    {
        new TextModeRenderer(),
        new LoResRenderer(),
        new HiResRenderer(1),
        new HiResRenderer(2)
    };
    var masterRenderer = new MasterRenderer(renderers);
    
    var frameBuffer = new MultiPlaneFrameBuffer();
    var mockMemory = CreateMockMemory();
    var mockStatus = CreateMockStatus(textMode: true);
    
    var context = new RenderContext { /*...*/ };
    
    // Act
    masterRenderer.Render(context);
    
    // Assert
    Assert.Equal(frameBuffer.TextMode[0], frameBuffer.ActiveDisplay[0]);
}
```

---

## Implementation Checklist

### Phase 1: Infrastructure
- [NA] Create `Pandowdy.EmuCore/Rendering` directory - Not necessary
- [X] Implement `MultiPlaneFrameBuffer.cs`
- [X] Implement `RenderContext.cs` (This is in VideoSubsystem.cs)
- [X] Implement `IRenderer.cs` interface (This is called IDisplayBitmapRenderer.cs)
- [X] Update `IFrameProvider.cs` interface
- [NA] Create `IDebugFrameProvider.cs` interface - Not necessary

### Phase 2: Renderers
- [X] Implement `LegacyBitmapRenderer.cs` to transition old rendering code to new paradigm
- [ ] Implement `TextModeRenderer.cs`
- [ ] Implement `LoResRenderer.cs`
- [ ] Implement `HiResRenderer.cs` 
- [ ] Implenent other renderers as needed.
- [X] Extract character ROM loading logic
- [?] Extract color palette constants - TBD

### Phase 3: Compositor
- [ ] Implement `MasterRenderer.cs` - This will be called MainRenderer not MasterRenderer
- [ ] Implement `CompositeActiveDisplay()` logic
- [ ] Implement `CompositeMixedMode()` logic
- [ ] Add soft switch state handling

### Phase 4: Integration
- [X] Update `FrameProvider.cs` to own `MultiPlaneFrameBuffer` - Frame provider doesn't own. It's DI.
- [X] Update `VA2M.OnVBlank()` to use new rendering system - Done
- [X] Update DI registration in `Program.cs` - Ongoing as needed
- [X] Remove old `VA2M.Render.cs` (after migration) - Code is commented out. Will remove file later.

### Phase 5: Testing
- [ ] Unit tests for each renderer
- [ ] Integration tests for compositor
- [ ] Performance benchmarks
- [ ] Visual regression tests

### Phase 6: Debug Features
- [ ] Implement debug bitplane viewer window
- [ ] Add bitplane selection UI
- [ ] Add real-time bitplane switching

---

## Migration Strategy

1. **Create new rendering infrastructure** alongside existing code
2. **Implement renderers** one at a time, testing each
3. **Wire up `MasterRenderer`** with all renderers
4. **Update `VA2M.OnVBlank()`** to use new system
5. **Test thoroughly** to ensure visual parity
6. **Remove old `VA2M.Render.cs`** once verified
7. **Add debug features** after core system stable

---

## Notes

- All renderers run every frame regardless of soft switches
- Bitplane 0 (active display) is always composited fresh
- 560×192 resolution chosen for accurate NTSC simulation
- FrameProvider owns the buffer, renderers just write to it
- Thread safety handled by FrameProvider locking
- Character ROM loaded once at startup, reused every frame

---

## Future thoughts to look at more closely:

- Should probably create a static CLUT for the 16 potential colors (and perhaps 16 in-between values as needed) for RGB display of NTSC signals
- Can use the formula:
```
R = Y + C(0.956 Cos 𝜙 + 0.621 Sin 𝜙)
B = Y + C(-0.272 Cos 𝜙 - 0.657 Sin 𝜙)
G = Y + C(-1.105 Cos 𝜙 - 1.702 Sin 𝜙)

And clamp R, G, & B to [0,100] before scaling to 0-255.
```

*Document created: 2025-12-29*  
*Last updated: 2025-12-30*
