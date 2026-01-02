namespace Pandowdy.EmuCore.Interfaces;

/// <summary>
/// Provides access to the emulator's frame buffer for rendering.
/// Supports double-buffering with front/back buffer swap.
/// </summary>
public interface IFrameProvider
{
    /// <summary>
    /// Width of the frame in bytes (80 columns for Apple II).
    /// </summary>
    int Width { get; }
    
    /// <summary>
    /// Height of the frame in scanlines (192 for Apple II).
    /// </summary>
    int Height { get; }
    
    /// <summary>
    /// True if in graphics mode.
    /// </summary>
    bool IsGraphics { get; set; }
    
    /// <summary>
    /// True if in mixed text/graphics mode.
    /// </summary>
    bool IsMixed { get; set; }
    
    /// <summary>
    /// Raised after a new frame has been committed and is available for display.
    /// </summary>
    event EventHandler? FrameAvailable;
    
    /// <summary>
    /// Gets the current front buffer for reading/displaying.
    /// </summary>
    BitmapDataArray GetFrame();
    
    /// <summary>
    /// Gets the back buffer for writing/composing the next frame.
    /// </summary>
    BitmapDataArray BorrowWritable();
    
    /// <summary>
    /// Swaps front and back buffers and raises the FrameAvailable event.
    /// </summary>
    void CommitWritable();
}
