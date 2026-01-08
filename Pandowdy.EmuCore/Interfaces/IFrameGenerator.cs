namespace Pandowdy.EmuCore.Interfaces;

using Pandowdy.EmuCore.DataTypes;

/// <summary>
/// Coordinates the generation of video frames from Apple IIe system state.
/// </summary>
/// <remarks>
/// This interface is responsible for orchestrating the frame rendering process by:
/// <list type="bullet">
/// <item>Allocating render contexts with necessary dependencies (memory, status, frame buffer)</item>
/// <item>Invoking the appropriate renderer to convert video memory into displayable pixels</item>
/// <item>Managing the rendering pipeline from system state to final frame buffer</item>
/// </list>
/// <para>
/// The frame generator acts as a coordinator between the emulator core (which provides
/// system state and memory) and the renderer (which produces pixels). It abstracts
/// the details of render context creation and renderer invocation, providing a simple
/// interface for the UI or display subsystem to request rendered frames.
/// </para>
/// <para>
/// Typical usage pattern:
/// <code>
/// var context = frameGenerator.AllocateRenderContext();
/// frameGenerator.RenderFrame(context);
/// // context.FrameBuffer now contains the rendered frame
/// </code>
/// </para>
/// </remarks>
public interface IFrameGenerator
{
    /// <summary>
    /// Allocates and initializes a render context for frame generation.
    /// </summary>
    /// <returns>
    /// A <see cref="RenderContext"/> containing the frame buffer, memory access,
    /// and system status required for rendering a frame.
    /// </returns>
    /// <remarks>
    /// This method creates a render context that packages together all the dependencies
    /// needed for frame rendering:
    /// <list type="bullet">
    /// <item><strong>Frame buffer</strong> - The writable bitmap where pixels will be rendered</item>
    /// <item><strong>Memory access</strong> - Direct access to video memory (main and auxiliary)</item>
    /// <item><strong>System status</strong> - Current soft switch states (video mode, page selection, etc.)</item>
    /// </list>
    /// <para>
    /// The allocated context is typically short-lived, used for rendering a single frame
    /// and then discarded. For performance, the underlying frame buffer may be reused
    /// through a double-buffering mechanism managed by the <see cref="IFrameProvider"/>.
    /// </para>
    /// <para>
    /// This method should be called once per frame, typically in response to a VBlank
    /// event or refresh timer tick (~60 Hz).
    /// </para>
    /// </remarks>
    RenderContext AllocateRenderContext();
    
    /// <summary>
    /// Renders the current Apple IIe video state into the provided context's frame buffer.
    /// </summary>
    /// <param name="context">
    /// The <see cref="RenderContext"/> containing the frame buffer, memory access,
    /// and system status. Typically obtained from <see cref="AllocateRenderContext"/>.
    /// As a reference type, the context's invalidation state is shared across all references.
    /// </param>
    /// <remarks>
    /// This method invokes the configured renderer (typically <see cref="IDisplayBitmapRenderer"/>)
    /// to convert the current video memory contents and mode settings into a displayable
    /// bitmap. The rendering process:
    /// <list type="number">
    /// <item>Examines the system status to determine active video mode (text/graphics, mixed, etc.)</item>
    /// <item>Reads video memory from the appropriate pages (main/auxiliary, page 1/2)</item>
    /// <item>Applies character ROM lookups for text modes or color generation for graphics modes</item>
    /// <item>Writes the resulting pixels to the context's frame buffer</item>
    /// <item>Invalidates the context to prevent reuse (implementation detail)</item>
    /// </list>
    /// <para>
    /// This method should complete quickly (ideally under 16ms for 60 fps) to maintain
    /// smooth frame rates. The actual rendering work is delegated to the renderer
    /// implementation, which may employ various optimizations (dirty rectangle tracking,
    /// scanline caching, etc.).
    /// </para>
    /// <para>
    /// <strong>Context Lifetime:</strong> After this method completes, the provided context
    /// is invalidated and cannot be reused. Attempting to access properties or call methods
    /// on the context after this method returns will throw <see cref="InvalidOperationException"/>.
    /// Allocate a new context for each frame via <see cref="AllocateRenderContext"/>.
    /// </para>
    /// </remarks>
    void RenderFrame(RenderContext context);

    /// <summary>
    /// Renders an Apple IIe video frame from a memory snapshot (threaded rendering).
    /// </summary>
    /// <param name="snapshot">
    /// The <see cref="VideoMemorySnapshot"/> containing captured video memory and system status.
    /// </param>
    /// <remarks>
    /// <para>
    /// <strong>Threaded Rendering:</strong> This method is designed to be called from a
    /// separate rendering thread. It renders from a snapshot of video memory captured at
    /// VBlank time, allowing the emulator thread to continue execution without blocking.
    /// </para>
    /// <para>
    /// <strong>Rendering Process:</strong>
    /// <list type="number">
    /// <item>Allocates a render context with a frame buffer</item>
    /// <item>Reads video data from the snapshot (not live memory)</item>
    /// <item>Applies rendering based on snapshot's soft switch states</item>
    /// <item>Publishes the rendered frame to the frame provider</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> This method may take several milliseconds to complete
    /// (typically 5-15ms depending on video mode and system). Running on a separate thread
    /// prevents this from blocking the emulator's CPU emulation, allowing it to run at
    /// full speed (11-13 MHz unthrottled).
    /// </para>
    /// </remarks>
    void RenderFrameFromSnapshot(VideoMemorySnapshot snapshot);
}
