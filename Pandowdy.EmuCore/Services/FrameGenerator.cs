using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.DataTypes;

namespace Pandowdy.EmuCore.Services;

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
        var context = new RenderContext(
            _frameProvider.BorrowWritable(),
            _memReader,
            _statusProvider);

        return context;
    }
    
    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>Rendering Pipeline:</strong>
    /// <list type="number">
    /// <item><strong>Clear buffer</strong> - Resets all pixels to black (via context.ClearBuffer())</item>
    /// <item><strong>Invoke renderer</strong> - Delegates to <see cref="IDisplayBitmapRenderer.Render"/> 
    ///       which reads video memory and draws pixels based on current video mode</item>
    /// <item><strong>Annotate frame</strong> - Sets display mode metadata on the frame provider 
    ///       (IsGraphics, IsMixed) for downstream consumers</item>
    /// <item><strong>Commit buffer</strong> - Swaps the writable buffer to readable, making it 
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
    /// <strong>Thread Safety:</strong> This method should only be called from the rendering thread.
    /// The commit operation is thread-safe (handled by IFrameProvider), allowing the GUI thread
    /// to concurrently read the previously committed frame.
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
        // Step 1: Clear the frame buffer (all pixels to black/background)
        context.ClearBuffer();

        // Step 2: Invoke renderer to draw video memory into frame buffer
        _renderer.Render(context);

        // Step 3: Annotate frame with display mode metadata for downstream consumers
        // (e.g., NTSC renderer) so they don't need ISystemStatusProvider reference
        _frameProvider.IsGraphics = !_statusProvider.StateTextMode;
        _frameProvider.IsMixed = _statusProvider.StateMixed;

        // Step 4: Commit the writable buffer, making it available for display
        _frameProvider.CommitWritable();

        // Step 5: Invalidate the context to prevent accidental reuse
        context.Invalidate();
    }

    /// <inheritdoc />
    public void RenderFrameFromSnapshot(VideoMemorySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(snapshot.SoftSwitches);
        
        // Allocate render context with borrowed frame buffer
        var context = AllocateRenderContext();
        
        // Clear the frame buffer
        context.ClearBuffer();
        
        // Create snapshot-based memory reader for renderer
        var snapshotMemReader = new SnapshotMemoryReader(snapshot);
        
        // Create snapshot-based render context
        var snapshotContext = new RenderContext(
            context.FrameBuffer,
            snapshotMemReader,
            new SnapshotStatusProvider(snapshot.SoftSwitches));
        
        // Render using snapshot data
        _renderer.Render(snapshotContext);
        
        // Annotate frame with display mode metadata
        _frameProvider.IsGraphics = !snapshot.SoftSwitches.StateTextMode;
        _frameProvider.IsMixed = snapshot.SoftSwitches.StateMixed;
        
        // Commit the buffer
        _frameProvider.CommitWritable();
        
        // Invalidate context
        context.Invalidate();
    }
}

/// <summary>
/// Snapshot-based memory reader for rendering from captured video memory.
/// </summary>
internal sealed class SnapshotMemoryReader : IDirectMemoryPoolReader
{
    private readonly VideoMemorySnapshot _snapshot;
    
    public SnapshotMemoryReader(VideoMemorySnapshot snapshot)
    {
        _snapshot = snapshot;
    }
    
    public byte ReadRawMain(int address)
    {
        // Route reads to snapshot arrays based on address
        return address switch
        {
            >= 0x0400 and < 0x0800 => _snapshot.MainPage1Text[address - 0x0400],
            >= 0x0800 and < 0x0C00 => _snapshot.MainPage2Text[address - 0x0800],
            >= 0x2000 and < 0x4000 => _snapshot.MainPage1HiRes[address - 0x2000],
            >= 0x4000 and < 0x6000 => _snapshot.MainPage2HiRes[address - 0x4000],
            _ => 0x00  // Outside video memory regions
        };
    }
    
    public byte ReadRawAux(int address)
    {
        // Route reads to snapshot's auxiliary arrays
        return address switch
        {
            >= 0x0400 and < 0x0800 => _snapshot.AuxPage1Text[address - 0x0400],
            >= 0x0800 and < 0x0C00 => _snapshot.AuxPage2Text[address - 0x0800],
            >= 0x2000 and < 0x4000 => _snapshot.AuxPage1HiRes[address - 0x2000],
            >= 0x4000 and < 0x6000 => _snapshot.AuxPage2HiRes[address - 0x4000],
            _ => 0x00  // Outside video memory regions
        };
    }
}

/// <summary>
/// Snapshot-based status provider for rendering from captured soft switch states.
/// </summary>
internal sealed class SnapshotStatusProvider : ISystemStatusProvider
{
    private readonly SystemStatusSnapshot _snapshot;
    
    public SnapshotStatusProvider(SystemStatusSnapshot snapshot)
    {
        _snapshot = snapshot;
    }
    
    // Implement ISystemStatusProvider by returning snapshot values
    public bool State80Store => _snapshot.State80Store;
    public bool StateRamRd => _snapshot.StateRamRd;
    public bool StateRamWrt => _snapshot.StateRamWrt;
    public bool StateIntCxRom => _snapshot.StateIntCxRom;
    public bool StateAltZp => _snapshot.StateAltZp;
    public bool StateSlotC3Rom => _snapshot.StateSlotC3Rom;
    public bool StatePb0 => _snapshot.StatePb0;
    public bool StatePb1 => _snapshot.StatePb1;
    public bool StatePb2 => _snapshot.StatePb2;
    public bool StateAnn0 => _snapshot.StateAnn0;
    public bool StateAnn1 => _snapshot.StateAnn1;
    public bool StateAnn2 => _snapshot.StateAnn2;
    public bool StateAnn3_DGR => _snapshot.StateAnn3_DGR;
    public byte CurrentKey => _snapshot.StateCurrentKey;
    public byte Pdl0 => _snapshot.StatePdl0;
    public byte Pdl1 => _snapshot.StatePdl1;
    public byte Pdl2 => _snapshot.StatePdl2;
    public byte Pdl3 => _snapshot.StatePdl3;
    public bool StatePage2 => _snapshot.StatePage2;
    public bool StateHiRes => _snapshot.StateHiRes;
    public bool StateMixed => _snapshot.StateMixed;
    public bool StateTextMode => _snapshot.StateTextMode;
    public bool StateShow80Col => _snapshot.StateShow80Col;
    public bool StateAltCharSet => _snapshot.StateAltCharSet;
    public bool StateFlashOn => _snapshot.StateFlashOn;
    public bool StatePreWrite => _snapshot.StatePrewrite;
    public bool StateUseBank1 => _snapshot.StateUseBank1;
    public bool StateHighRead => _snapshot.StateHighRead;
    public bool StateHighWrite => _snapshot.StateHighWrite;
    public bool StateVBlank => _snapshot.StateVBlank;
    public SystemStatusSnapshot Current => _snapshot;
    
    // Events not used during snapshot rendering
#pragma warning disable CS0067
    public event EventHandler<SystemStatusSnapshot>? Changed;
    public event EventHandler<SystemStatusSnapshot>? MemoryMappingChanged;
#pragma warning restore CS0067
    
    public IObservable<SystemStatusSnapshot> Stream => throw new NotSupportedException();
    public void Mutate(Action<SystemStatusSnapshotBuilder> mutator) => throw new NotSupportedException();
}
