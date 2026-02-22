// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Machine;
using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.IO;
using Pandowdy.EmuCore.Memory;

namespace Pandowdy.EmuCore.Video;

/// <summary>
/// Generates Apple II video frames by coordinating bitmap rendering,
/// memory access, and system status. Produces annotated frames for
/// downstream consumers (e.g., NTSC post-processing, GUI display).
/// </summary>
/// <remarks>
/// <para>
/// <strong>Architecture:</strong> This implementation acts as a facade/coordinator,
/// delegating the actual pixel rendering work to an <see cref="IDisplayBitmapRenderer"/>
/// while managing the lifecycle and annotation of frame buffers.
/// </para>
/// <para>
/// <strong>Frame Annotation:</strong> After rendering, this class annotates the frame
/// with display mode metadata (IsGraphics, IsMixed) by setting properties on
/// <see cref="IFrameProvider"/>. This allows downstream consumers (NTSC renderers, display
/// adapters) to access mode information without requiring their own <see cref="ISystemStatusProvider"/>
/// reference.
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> Not inherently thread-safe. Frame generation should be
/// called from a single thread (typically the emulator thread or a dedicated rendering thread).
/// The underlying <see cref="IFrameProvider"/> handles thread-safe buffer swapping between
/// renderer and consumer.
/// </para>
/// </remarks>
public class FrameGenerator : IFrameGenerator
{
    private readonly IFrameProvider _frameProvider;
    private readonly IDirectMemoryPoolReader _memReader;
    private readonly ISystemStatusProvider _statusProvider;
    private readonly IDisplayBitmapRenderer _renderer;

    /// <summary>
    /// Initializes a new instance of the <see cref="FrameGenerator"/> class.
    /// </summary>
    /// <param name="frameProvider">
    /// Provider for double-buffered frame storage. Supplies writable frame buffers
    /// and manages buffer swapping after rendering completes.
    /// </param>
    /// <param name="memReader">
    /// Direct memory access for reading video RAM (main and auxiliary banks).
    /// Used by the renderer to fetch character data, graphics pixels, etc.
    /// </param>
    /// <param name="statusProvider">
    /// System status provider for reading soft switch states (video mode, page selection,
    /// character set, etc.). Used to determine rendering behavior.
    /// </param>
    /// <param name="renderer">
    /// The bitmap renderer implementation that converts video memory into displayable pixels.
    /// Typically <see cref="LegacyBitmapRenderer"/> or a future optimized/NTSC renderer.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if any parameter is null. All dependencies are required for frame generation.
    /// </exception>
    public FrameGenerator(
        IFrameProvider frameProvider, 
        IDirectMemoryPoolReader memReader, 
        ISystemStatusProvider statusProvider, 
        IDisplayBitmapRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(frameProvider);
        ArgumentNullException.ThrowIfNull(memReader);
        ArgumentNullException.ThrowIfNull(statusProvider);
        ArgumentNullException.ThrowIfNull(renderer);

        _frameProvider = frameProvider;
        _memReader = memReader;
        _statusProvider = statusProvider;
        _renderer = renderer;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>Implementation:</strong> Borrows a writable frame buffer from the
    /// <see cref="IFrameProvider"/> and packages it with memory access and system status
    /// into a <see cref="RenderContext"/> struct.
    /// </para>
    /// <para>
    /// <strong>Buffer Ownership:</strong> The returned context holds a reference to a
    /// writable frame buffer that is "borrowed" until <see cref="RenderFrame"/> commits it.
    /// Do not reuse the context after calling <see cref="RenderFrame"/>.
    /// </para>
    /// </remarks>
    public RenderContext AllocateRenderContext()
    {
        var buffer = _frameProvider.BorrowWritable() ?? throw new InvalidOperationException("No frame buffer available from provider");

        var context = new RenderContext(
            buffer,
            _memReader,
            _statusProvider);

        return context;
    }
    
    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>Rendering Pipeline:</strong>
    /// <list type="number">
    /// <item><strong>Borrow buffer</strong> - Gets pre-cleared buffer from pool (lock-free)</item>
    /// <item><strong>Clear buffer</strong> - Resets all pixels to black (via context.ClearBuffer())</item>
    /// <item><strong>Invoke renderer</strong> - Delegates to <see cref="IDisplayBitmapRenderer.Render"/> 
    ///       which reads video memory and draws pixels based on current video mode</item>
    /// <item><strong>Annotate frame</strong> - Sets display mode metadata on the frame provider 
    ///       (IsGraphics, IsMixed) for downstream consumers</item>
    /// <item><strong>Commit buffer</strong> - Swaps the buffer to display, making it 
    ///       available to GUI/display consumers</item>
    /// <item><strong>Invalidate context</strong> - Marks the context as invalid to prevent accidental reuse</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> Rendering time depends on the selected video mode and
    /// renderer implementation. Typical frame times on modern hardware:
    /// <list type="bullet">
    /// <item>Text mode: 1-2ms</item>
    /// <item>Lo-res/Hi-res graphics: 2-4ms</item>
    /// <item>Mixed mode: 3-5ms</item>
    /// </list>
    /// Well within the 16.67ms budget for 60 fps rendering.
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> Multiple threads can call this method simultaneously
    /// with multi-buffer architecture. Each gets its own buffer from the pool.
    /// </para>
    /// <para>
    /// <strong>Context Invalidation:</strong> After this method completes, the provided context
    /// is automatically invalidated and cannot be reused. Attempting to use the context after
    /// this method returns will throw <see cref="InvalidOperationException"/>. As a reference type,
    /// the invalidation is visible to all references to the context instance.
    /// </para>
    /// </remarks>
    public void RenderFrame(RenderContext context)
    {
        // Legacy method - not used with snapshot-based rendering
        // Kept for backward compatibility
        throw new NotSupportedException(
            "RenderFrame(context) is not supported in multi-buffer architecture. " +
            "Use RenderFrameFromSnapshot(snapshot) instead.");
    }

    /// <inheritdoc />
    public void RenderFrameFromSnapshot(VideoMemorySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(snapshot.SoftSwitches);
        
        // Try to borrow a buffer from the pool (lock-free)
        var buffer = _frameProvider.BorrowWritable();
        if (buffer == null)
        {
            // All buffers in use - skip this frame
            // Acceptable at high speeds (700 FPS)
            return;
        }
        
        // Create render context with borrowed buffer
        // Buffer is pre-cleared, ready for rendering
        var context = new RenderContext(
            buffer,
            new SnapshotMemoryReader(snapshot),
            new SnapshotStatusProvider(snapshot.SoftSwitches));
        
        // Render using snapshot data
        _renderer.Render(context);
        
        // Annotate frame with display mode metadata
        _frameProvider.IsGraphics = !snapshot.SoftSwitches.StateTextMode;
        _frameProvider.IsMixed = snapshot.SoftSwitches.StateMixed;
        
        // Commit the rendered buffer (swaps display, returns old buffer to pool)
        _frameProvider.CommitWritable(buffer);
    }
}

/// <summary>
/// Snapshot-based memory reader for rendering from captured video memory.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Direct RAM Access:</strong> With simplified snapshot architecture, this reader
/// indexes directly into the full 48KB main/aux RAM arrays instead of switching between
/// separate text/hi-res page arrays. This simplifies logic and improves cache locality.
/// </para>
/// </remarks>
internal sealed class SnapshotMemoryReader(VideoMemorySnapshot snapshot) : IDirectMemoryPoolReader
{
    private readonly VideoMemorySnapshot _snapshot = snapshot;
    
    public byte ReadRawMain(int address)
    {
        // Direct index into full 48KB main RAM array
        // Video memory is naturally at correct offsets:
        // - Text Page 1: $0400-$07FF
        // - Text Page 2: $0800-$0BFF
        // - Hi-Res Page 1: $2000-$3FFF
        // - Hi-Res Page 2: $4000-$5FFF
        if (address >= 0 && address < 0xC000)
        {
            return _snapshot.MainRam[address];
        }
        return 0x00;  // Outside 48KB range
    }
    
    public byte ReadRawAux(int address)
    {
        // Direct index into full 48KB auxiliary RAM array
        if (address >= 0 && address < 0xC000)
        {
            return _snapshot.AuxRam[address];
        }
        return 0x00;  // Outside 48KB range
    }
}

/// <summary>
/// Snapshot-based status provider for rendering from captured soft switch states.
/// </summary>
internal sealed class SnapshotStatusProvider(SystemStatusSnapshot snapshot) : ISystemStatusProvider
{
    private readonly SystemStatusSnapshot _snapshot = snapshot;
    
    // Implement ISystemStatusProvider by returning snapshot values
    public bool State80Store => _snapshot.State80Store;
    public bool StateRamRd => _snapshot.StateRamRd;
    public bool StateRamWrt => _snapshot.StateRamWrt;
    public bool StateIntCxRom => _snapshot.StateIntCxRom;
    public bool StateIntC8Rom => _snapshot.StateIntC8Rom;
    public byte StateIntC8RomSlot => _snapshot.StateIntC8RomSlot;
    public bool StateAltZp => _snapshot.StateAltZp;
    public bool StateSlotC3Rom => _snapshot.StateSlotC3Rom;
    public bool StatePb0 => _snapshot.StatePb0;
    public bool StatePb1 => _snapshot.StatePb1;
    public bool StatePb2 => _snapshot.StatePb2;
    public bool StateFlashOn => _snapshot.StateFlashOn;
    public bool StateTextMode => _snapshot.StateTextMode;
    public bool StateMixed => _snapshot.StateMixed;
    public bool StatePage2 => _snapshot.StatePage2;
    public bool StateHiRes => _snapshot.StateHiRes;
    public bool StateAltCharSet => _snapshot.StateAltCharSet;
    public bool StateShow80Col => _snapshot.StateShow80Col;
    public bool StateAnn3_DGR => _snapshot.StateAnn3_DGR;
    public bool StateAnn0 => _snapshot.StateAnn0;
    public bool StateAnn1 => _snapshot.StateAnn1;
    public bool StateAnn2 => _snapshot.StateAnn2;
    public bool StateUseBank1 => _snapshot.StateUseBank1;
    public bool StateHighRead => _snapshot.StateHighRead;
    public bool StateHighWrite => _snapshot.StateHighWrite;
    public bool StateVBlank => _snapshot.StateVBlank;
    public double StateCurrentMhz => _snapshot.StateCurrentMhz;

    // Properties not captured in snapshot (not needed for rendering)
    public bool StatePreWrite => false;
    public byte CurrentKey => 0;
    public byte Pdl0 => 0;
    public byte Pdl1 => 0;
    public byte Pdl2 => 0;
    public byte Pdl3 => 0;


    
    public SystemStatusSnapshot Current => _snapshot;
    
    // Events are not used during rendering from snapshot - no-op implementations
    public event EventHandler<SystemStatusSnapshot>? Changed { add { } remove { } }
    public event EventHandler<SystemStatusSnapshot>? MemoryMappingChanged { add { } remove { } }
    
    public IObservable<SystemStatusSnapshot> Stream => throw new NotSupportedException("Snapshot provider doesn't support streaming");
    public void Mutate(Action<SystemStatusSnapshotBuilder> mutator) => throw new NotSupportedException("Snapshot provider is read-only");
}
