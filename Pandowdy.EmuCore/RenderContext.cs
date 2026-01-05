//------------------------------------------------------------------------------
// RenderContext.cs
//
// Provides a one-shot context for rendering a single video frame, encapsulating
// the frame buffer, memory access, and system status required for rendering.
//
// DESIGN PATTERN: One-Shot Object
// RenderContext follows a "use once and discard" pattern. Once a context is used
// to render a frame (via FrameGenerator.RenderFrame), it is automatically invalidated
// to prevent accidental reuse. This design prevents several categories of bugs:
//
// 1. **Buffer Recycling Errors:**
///    After RenderFrame commits the buffer, it may be recycled for the next frame.
///    Using the old context would write to a buffer that's now in use elsewhere.
///
/// 2. **State Consistency:**
///    The context captures soft switch states at allocation time. Reusing it after
///    switches change would produce incorrect rendering.
///
/// 3. **Clear Ownership:**
///    One-shot semantics make it obvious when a context is "consumed" and who owns
///    the underlying resources.
///
/// THREAD SAFETY:
/// Not thread-safe. Each context should be used by a single thread from allocation
/// through commit. Multiple threads can allocate separate contexts concurrently.
///
/// PERFORMANCE:
/// The invalidation checks add minimal overhead (single bool check per access).
/// This cost is negligible compared to rendering work and provides strong safety
/// guarantees.
//------------------------------------------------------------------------------

using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore;

/// <summary>
/// Context for rendering a single video frame, containing frame buffer, memory access, and system status.
/// </summary>
/// <remarks>
/// <para>
/// <strong>One-Shot Lifetime:</strong> A RenderContext is valid only until it is committed via
/// <see cref="FrameGenerator.RenderFrame"/>. After commit, the context is automatically invalidated
/// and attempting to use it will throw <see cref="InvalidOperationException"/>.
/// </para>
/// <para>
/// <strong>Usage Pattern:</strong>
/// <code>
/// var context = frameGenerator.AllocateRenderContext();  // Valid
/// frameGenerator.RenderFrame(context);                    // Commits and invalidates
/// // context is now invalid - do not reuse!
/// </code>
/// </para>
/// <para>
/// <strong>Reference Type:</strong> RenderContext is a class (reference type), so invalidation state
/// is naturally shared across all references to the same instance.
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> Not thread-safe. A context should be used by a single thread
/// from allocation through commit.
/// </para>
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="RenderContext"/> class.
/// </remarks>
/// <param name="frameBuffer">The frame buffer where pixels will be written during rendering.</param>
/// <param name="memory">Direct memory access for reading video RAM (main and auxiliary banks).</param>
/// <param name="status">System status provider for reading soft switch states.</param>
/// <exception cref="ArgumentNullException">
/// Thrown if any parameter is null. All dependencies are required.
/// </exception>
public class RenderContext(
    BitmapDataArray frameBuffer,
    IDirectMemoryPoolReader memory,
    ISystemStatusProvider status)
{
    private BitmapDataArray _frameBuffer = frameBuffer ?? throw new ArgumentNullException(nameof(frameBuffer));
    private IDirectMemoryPoolReader _memory = memory ?? throw new ArgumentNullException(nameof(memory));
    private ISystemStatusProvider _systemStatus = status ?? throw new ArgumentNullException(nameof(status));
    private bool _isInvalidated = false;

    /// <summary>
    /// Frame buffer where pixels are written during rendering.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when accessing the frame buffer after the context has been invalidated (committed).
    /// </exception>
    public BitmapDataArray FrameBuffer
    {
        get
        {
            ThrowIfInvalidated();
            return _frameBuffer;
        }
    }

    /// <summary>
    /// Direct memory access for reading video RAM (main and auxiliary banks).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when accessing memory after the context has been invalidated (committed).
    /// </exception>
    public IDirectMemoryPoolReader Memory
    {
        get
        {
            ThrowIfInvalidated();
            return _memory;
        }
    }

    /// <summary>
    /// System status provider for reading soft switch states (video mode, page selection, etc.).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when accessing system status after the context has been invalidated (committed).
    /// </exception>
    public ISystemStatusProvider SystemStatus
    {
        get
        {
            ThrowIfInvalidated();
            return _systemStatus;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this context has been invalidated (committed).
    /// </summary>
    /// <remarks>
    /// Once a context is invalidated, it cannot be used for rendering or buffer access.
    /// A new context must be allocated via <see cref="IFrameGenerator.AllocateRenderContext"/>.
    /// </remarks>
    public bool IsInvalidated => _isInvalidated;

    /// <summary>
    /// Gets whether the current video mode is text mode.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when accessing after the context has been invalidated (committed).
    /// </exception>
    public bool IsTextMode
    {
        get
        {
            ThrowIfInvalidated();
            return _systemStatus.StateTextMode;
        }
    }

    /// <summary>
    /// Gets whether mixed mode is active (text bottom, graphics top).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when accessing after the context has been invalidated (committed).
    /// </exception>
    public bool IsMixed
    {
        get
        {
            ThrowIfInvalidated();
            return _systemStatus.StateMixed;
        }
    }

    /// <summary>
    /// Gets whether hi-res graphics mode is active.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when accessing after the context has been invalidated (committed).
    /// </exception>
    public bool IsHiRes
    {
        get
        {
            ThrowIfInvalidated();
            return _systemStatus.StateHiRes;
        }
    }

    /// <summary>
    /// Gets whether page 2 is active (affects which video memory page is displayed).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when accessing after the context has been invalidated (committed).
    /// </exception>
    public bool IsPage2
    {
        get
        {
            ThrowIfInvalidated();
            return _systemStatus.StatePage2;
        }
    }

    /// <summary>
    /// Clears the frame buffer (resets all pixels to black/background).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when called after the context has been invalidated (committed).
    /// </exception>
    public void ClearBuffer()
    {
        ThrowIfInvalidated();
        _frameBuffer.Clear();
    }

    /// <summary>
    /// Invalidates this context, preventing further use.
    /// </summary>
    /// <remarks>
    /// This method is called internally by <see cref="FrameGenerator.RenderFrame"/> after
    /// committing the frame buffer. Once invalidated, the context cannot be reused.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the context has already been invalidated.
    /// </exception>
    internal void Invalidate()
    {
        if (_isInvalidated)
        {
            throw new InvalidOperationException(
                "RenderContext has already been invalidated. This indicates a programming error " +
                "where the same context is being committed multiple times.");
        }
        _isInvalidated = true;
    }

    /// <summary>
    /// Throws an exception if this context has been invalidated.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the context has been invalidated (committed).
    /// </exception>
    private void ThrowIfInvalidated()
    {
        if (_isInvalidated)
        {
            throw new InvalidOperationException(
                "RenderContext has been invalidated after commit and cannot be reused. " +
                "Allocate a new context via IFrameGenerator.AllocateRenderContext().");
        }
    }
}
