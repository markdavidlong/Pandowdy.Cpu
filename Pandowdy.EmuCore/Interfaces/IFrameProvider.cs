using Pandowdy.EmuCore.DataTypes;

namespace Pandowdy.EmuCore.Interfaces;

/// <summary>
/// Provides access to the emulator's frame buffer for rendering with double-buffering support.
/// </summary>
/// <remarks>
/// This interface manages the frame buffers used for rendering Apple IIe video output.
/// It implements a double-buffering pattern where:
/// <list type="bullet">
/// <item>The <strong>back buffer</strong> is used for composing the next frame (writing)</item>
/// <item>The <strong>front buffer</strong> is used for display (reading)</item>
/// <item>Buffers are swapped atomically when the frame is complete</item>
/// </list>
/// This prevents tearing and ensures smooth animation by keeping the display buffer stable
/// while the next frame is being rendered. The <see cref="FrameAvailable"/> event notifies
/// consumers (typically the UI layer) when a new frame is ready for display.
/// </remarks>
public interface IFrameProvider
{

    /// <summary>
    /// Gets the width of the frame in pixels.
    /// </summary>
    /// <value>
    /// The frame width in pixels. For Apple IIe standard modes, this is typically 560 pixels
    /// (280 pixels doubled for HGR, individual for DHGR, 14 (or 7) pixels per
    /// character Ã— 40 (or 80) columns). 
    /// </value>
    /// <remarks>
    /// Currently, this value is determined by the default implementation of
    /// <see cref="BitmapDataArray"/>. In a future refactoring, the frame provider may
    /// dictate dimensions at creation time to support additional graphics modes.
    /// The pixel width represents the final rendered output width, which may include
    /// horizontal doubling to achieve proper aspect ratio on square-pixel displays.
    /// </remarks>
    int Width { get; }

    /// <summary>
    /// Gets the height of the frame in scanlines.
    /// </summary>
    /// <value>
    /// The frame height in scanlines. For Apple IIe, this is 192 scanlines (24 text rows
    /// Ã— 8 scanlines per row).
    /// </value>
    int Height { get; }
    
    /// <summary>
    /// Gets or sets whether the emulator is in graphics mode (as opposed to text mode).
    /// </summary>
    /// <value>
    /// True if displaying graphics (lo-res or hi-res); false if displaying text mode.
    /// </value>
    /// <remarks>
    /// This property reflects the state of the TEXT soft switch. When false (text mode),
    /// the display shows 40-column or 80-column text. When true (graphics mode), the
    /// display shows lo-res (40Ã—48 color blocks) or hi-res (280Ã—192 pixels) graphics.
    /// The primary purpose of this flag is to give the UI renderer a hint to use NTSC
    /// color generation and fringing when applicable.
    /// </remarks>
    bool IsGraphics { get; set; }
    
    /// <summary>
    /// Gets or sets whether the emulator is in mixed text/graphics mode.
    /// </summary>
    /// <value>
    /// True if displaying mixed mode (graphics on top 20 rows, text on bottom 4 rows);
    /// false if displaying pure text or pure graphics.
    /// </value>
    /// <remarks>
    /// This property reflects the state of the MIXED soft switch. When true, the top
    /// 160 scanlines display graphics content and the bottom 32 scanlines (4 text rows)
    /// display text. This mode is commonly used in Apple II games to show graphics with
    /// a text status line at the bottom.
    /// The primary purpose of this flag is to give the UI renderer a hint that the bottom
    /// 4 lines of the display are text and can optionally be excluded from NTSC color generation
    /// to reduce fringing of the text.
    /// </remarks>
    bool IsMixed { get; set; }
    
    /// <summary>
    /// Raised after a new frame has been committed and is available for display.
    /// </summary>
    /// <remarks>
    /// This event is fired by <see cref="CommitWritable"/> after the front and back buffers
    /// have been swapped. Subscribers (typically the UI layer) should respond by reading
    /// the front buffer via <see cref="GetFrame"/> and updating the display. This event
    /// is typically raised at the video refresh rate (approximately 60 Hz for Apple IIe).
    /// </remarks>
    event EventHandler? FrameAvailable;
    
    /// <summary>
    /// Gets the current front buffer for reading and displaying.
    /// </summary>
    /// <returns>
    /// A <see cref="BitmapDataArray"/> containing the most recently committed frame,
    /// ready for display. This buffer is stable and will not change until the next
    /// call to <see cref="CommitWritable"/>.
    /// </returns>
    /// <remarks>
    /// This method is typically called by the UI layer in response to the
    /// <see cref="FrameAvailable"/> event to retrieve the completed frame for display.
    /// The returned buffer should be treated as read-only to prevent race conditions
    /// with the rendering thread.
    /// </remarks>
    BitmapDataArray GetFrame();
    
    /// <summary>
    /// Borrows the writable back buffer for rendering the next frame.
    /// </summary>
    /// <returns>
    /// A <see cref="BitmapDataArray"/> representing the back buffer, ready for rendering.
    /// In multi-buffer architecture, may return null if all buffers are currently in use.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Usage Pattern:</strong>
    /// <code>
    /// var buffer = frameProvider.BorrowWritable();
    /// if (buffer != null)
    /// {
    ///     // Render frame into buffer
    ///     frameProvider.CommitWritable(buffer);
    /// }
    /// // else: skip frame (all buffers busy)
    /// </code>
    /// </para>
    /// <para>
    /// <strong>Multi-Buffer Architecture:</strong> With 4-buffer circular pool, returns null
    /// only when all 3 renderable buffers are in use (display + 2 renderers). This is rare
    /// even at 700 FPS.
    /// </para>
    /// </remarks>
    BitmapDataArray? BorrowWritable();
    
    /// <summary>
    /// Commits the rendered buffer, making it available for display.
    /// </summary>
    /// <param name="renderedBuffer">
    /// The buffer that was borrowed and rendered. Must be the same buffer returned by
    /// <see cref="BorrowWritable"/>.
    /// </param>
    /// <remarks>
    /// <para>
    /// <strong>Atomicity:</strong> The buffer swap is atomic. After this method returns,
    /// calls to <see cref="GetFrame"/> will return the newly committed buffer.
    /// </para>
    /// <para>
    /// <strong>Event Notification:</strong> Raises <see cref="FrameAvailable"/> to notify
    /// the UI layer that a new frame is ready.
    /// </para>
    /// </remarks>
    void CommitWritable(BitmapDataArray renderedBuffer);
}
