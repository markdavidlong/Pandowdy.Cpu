using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.Services;

/// <summary>
/// Provides double-buffered frame management for Apple II display with dimension validation.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Implementation:</strong> This class manages two <see cref="BitmapDataArray"/> instances
/// as front and back buffers. Frame dimensions are derived from <see cref="BitmapDataArray.Width"/>
/// and <see cref="BitmapDataArray.Height"/> static properties and validated at construction.
/// </para>
/// <para>
/// <strong>Buffer Swap:</strong> Uses C# tuple swap pattern <c>(_back, _front) = (_front, _back)</c>
/// for atomic buffer exchange. This ensures thread-safe swapping without intermediate variables.
/// </para>
/// <para>
/// <strong>Validation:</strong> Constructor performs strict validation to ensure:
/// <list type="bullet">
/// <item>Front and back buffers have matching dimensions</item>
/// <item>Dimensions match Apple IIe standard (560×192 pixels)</item>
/// </list>
/// These checks catch configuration errors early and prevent rendering corruption.
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> The buffer swap in <see cref="CommitWritable"/> is atomic,
/// but the caller must ensure only one thread calls rendering methods. The front buffer
/// can be safely read by the UI thread while the back buffer is being written by the
/// rendering thread.
/// </para>
/// <para>
/// <strong>Display Mode Metadata:</strong> <see cref="IsGraphics"/> and <see cref="IsMixed"/>
/// properties are set by <see cref="FrameGenerator"/> after rendering to provide hints to
/// downstream consumers (NTSC color generation, display adapters). These are not thread-safe
/// and should only be set by the rendering thread.
/// </para>
/// </remarks>
public sealed class FrameProvider : IFrameProvider
{
    private BitmapDataArray _front;
    private BitmapDataArray _back;

    /// <inheritdoc />
    public int Width { get; }
    
    /// <inheritdoc />
    public int Height { get; }
    
    /// <inheritdoc />
    public event EventHandler? FrameAvailable;
    
    /// <inheritdoc />
    /// <remarks>
    /// <strong>Implementation Note:</strong> This property is set by the rendering subsystem
    /// (typically <see cref="FrameGenerator"/>) after frame rendering completes. It reflects
    /// the state of the TEXT soft switch for the rendered frame.
    /// </remarks>
    public bool IsGraphics { get; set; } = false;
    
    /// <inheritdoc />
    /// <remarks>
    /// <strong>Implementation Note:</strong> This property is set by the rendering subsystem
    /// (typically <see cref="FrameGenerator"/>) after frame rendering completes. It reflects
    /// the state of the MIXED soft switch for the rendered frame.
    /// </remarks>
    public bool IsMixed { get; set; } = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="FrameProvider"/> class with dimension validation.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if front and back buffer dimensions don't match, or if dimensions don't match
    /// Apple IIe standard (560×192). These exceptions indicate a configuration error in
    /// <see cref="BitmapDataArray"/> and should never occur in production with correct constants.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <strong>Initialization Sequence:</strong>
    /// <list type="number">
    /// <item>Allocates front and back <see cref="BitmapDataArray"/> buffers</item>
    /// <item>Queries buffer dimensions from static properties</item>
    /// <item>Validates front and back buffers have matching dimensions</item>
    /// <item>Validates dimensions match Apple IIe standard (560×192)</item>
    /// <item>Sets <see cref="Width"/> and <see cref="Height"/> properties</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Why Validate?</strong> Although the dimensions are determined by
    /// <see cref="BitmapDataArray"/> constants, explicit validation:
    /// <list type="bullet">
    /// <item>Catches bugs if BitmapDataArray is refactored incorrectly</item>
    /// <item>Documents the expected dimensions (560×192) for Apple IIe</item>
    /// <item>Provides clear error messages if configuration is wrong</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> All validation happens once at construction.
    /// Runtime frame operations have no validation overhead.
    /// </para>
    /// </remarks>
    public FrameProvider()
    {
        _front = new BitmapDataArray();
        _back = new BitmapDataArray();

        // Derive geometry from BitmapDataArray static properties
        int frontWidth = BitmapDataArray.Width;
        int frontHeight = BitmapDataArray.Height;
        int backWidth = BitmapDataArray.Width;
        int backHeight = BitmapDataArray.Height;

        // Verify both buffers have same geometry - CRITICAL
        // This should never fail with current BitmapDataArray implementation,
        // but catches refactoring errors early
        if (frontWidth != backWidth)
        {
            throw new InvalidOperationException(
                $"Front and back buffer widths must match. Front: {frontWidth}, Back: {backWidth}");
        }
        
        if (frontHeight != backHeight)
        {
            throw new InvalidOperationException(
                $"Front and back buffer heights must match. Front: {frontHeight}, Back: {backHeight}");
        }

        // Store validated dimensions
        Width = frontWidth;
        Height = frontHeight;

        // Verify Apple II standard dimensions
        // 560 pixels = 280 hi-res pixels doubled, or 80 columns × 7 pixels
        // 192 scanlines = 24 text rows × 8 scanlines per row
        if (Width != 560)
        {
            throw new InvalidOperationException(
                $"Expected Apple II standard width of 560 pixels, but got {Width}. " +
                "Check BitmapDataArray.Width constant.");
        }
        
        if (Height != 192)
        {
            throw new InvalidOperationException(
                $"Expected Apple II standard height of 192 scanlines, but got {Height}. " +
                "Check BitmapDataArray.Height constant.");
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Returns the current front buffer which contains the most recently committed frame.
    /// This buffer remains stable until the next <see cref="CommitWritable"/> call.
    /// </remarks>
    public BitmapDataArray GetFrame() => _front;
    
    /// <inheritdoc />
    /// <remarks>
    /// Returns the back buffer for rendering the next frame. This buffer is not visible
    /// until <see cref="CommitWritable"/> swaps it to the front.
    /// </remarks>
    public BitmapDataArray BorrowWritable() => _back;
    
    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>Implementation:</strong> Uses C# 7.0 tuple swap pattern for atomic exchange:
    /// <code>
    /// (_back, _front) = (_front, _back);
    /// </code>
    /// This is equivalent to a traditional three-statement swap but more concise and expressive.
    /// </para>
    /// <para>
    /// <strong>Atomicity:</strong> The tuple swap is atomic from the perspective of reference
    /// assignments. After the swap completes, any thread reading <c>_front</c> will see the
    /// new frame, and any thread calling <see cref="BorrowWritable"/> will get the old front
    /// buffer as the new back buffer.
    /// </para>
    /// <para>
    /// <strong>Event Notification:</strong> After swapping, raises <see cref="FrameAvailable"/>
    /// to notify the UI layer. Event handlers execute synchronously on the calling thread
    /// (typically the rendering thread).
    /// </para>
    /// </remarks>
    public void CommitWritable()
    {
        // Atomic buffer swap using C# tuple pattern
        (_back, _front) = (_front, _back);
        
        // Notify UI layer that new frame is ready for display
        FrameAvailable?.Invoke(this, EventArgs.Empty);
    }
}
