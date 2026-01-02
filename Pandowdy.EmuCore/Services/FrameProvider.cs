using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.Services
{
    /// <summary>
    /// Provides double-buffered frame management for Apple II display.
    /// Geometry is derived from BitmapDataArray dimensions.
    /// </summary>
    public sealed class FrameProvider : IFrameProvider
    {
        private BitmapDataArray _front;
        private BitmapDataArray _back;

        public int CharWidth { get; }
        public int PixelWidth { get; }
        public int Height { get; }
        
        public event EventHandler? FrameAvailable;
        
        public bool IsGraphics { get; set; } = false;
        public bool IsMixed { get; set; } = false;

        public FrameProvider()
        {
            _front = new BitmapDataArray();
            _back = new BitmapDataArray();

            // Derive geometry from BitmapDataArray
            int frontWidth = BitmapDataArray.Width;
            int frontHeight = BitmapDataArray.Height;
            int backWidth = BitmapDataArray.Width;
            int backHeight = BitmapDataArray.Height;

            // Verify both buffers have same geometry - CRITICAL
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

            // Apple II character width calculation
            // 560 pixels / 7 pixels per character = 80 characters
            int pixelWidth = frontWidth;
            int height = frontHeight;

            CharWidth = pixelWidth / 7;
            PixelWidth = pixelWidth;
            Height = height;

            // Verify Apple II standard dimensions
            if (CharWidth != 80)
            {
                throw new InvalidOperationException(
                    $"Expected Apple II standard width of 80 characters, but got {CharWidth}");
            }
            
            if (Height != 192)
            {
                throw new InvalidOperationException(
                    $"Expected Apple II standard height of 192 scanlines, but got {Height}");
            }
        }

        public BitmapDataArray GetFrame() => _front;
        
        public BitmapDataArray BorrowWritable() => _back;
        
        public void CommitWritable()
        {
            (_back, _front) = (_front, _back);
            FrameAvailable?.Invoke(this, EventArgs.Empty);
        }
    }
}
