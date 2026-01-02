using Pandowdy.EmuCore.Services;

namespace Pandowdy.EmuCore.Tests;

/// <summary>
/// Integration tests for rendering components - verifies that BitField16,
/// BitmapDataArray, and FrameProvider work together correctly in realistic scenarios.
/// </summary>
public class RenderingIntegrationTests
{
    [Fact]
    public void BitField16InBitmapDataArray()
    {
        // Arrange
        var bitmap = new BitmapDataArray();

        // Act - Use multiple bitplanes (simulating Apple II color)
        bitmap.SetPixel(280, 96, 0); // Bitplane 0
        bitmap.SetPixel(280, 96, 1); // Bitplane 1
        bitmap.SetPixel(280, 96, 2); // Bitplane 2
        bitmap.SetPixel(280, 96, 3); // Bitplane 3

        // Get the underlying data
        var rowData = bitmap.GetRowDataSpan(96);
        var bitfield = rowData[280];

        // Assert - All 4 bitplanes should be set
        Assert.True(bitfield.GetBit(0));
        Assert.True(bitfield.GetBit(1));
        Assert.True(bitfield.GetBit(2));
        Assert.True(bitfield.GetBit(3));
        Assert.Equal(0x000F, bitfield.Value);
    }

    [Fact]
    public void FrameProviderWithBitmapDataArray()
    {
        // Arrange
        var provider = new FrameProvider();

        // Act - Render a simple pattern
        var backBuffer = provider.BorrowWritable();
        
        // Draw a horizontal line
        for (int x = 0; x < 560; x += 7)
        {
            backBuffer.SetPixel(x, 96, 0);
        }

        provider.CommitWritable();
        var frontBuffer = provider.GetFrame();

        // Assert - Pattern visible in front buffer
        for (int x = 0; x < 560; x++)
        {
            bool expected = (x % 7 == 0);
            Assert.Equal(expected, frontBuffer.GetPixel(x, 96, 0));
        }
    }

    [Fact]
    public void CompleteRenderingWorkflow()
    {
        // Arrange
        var provider = new FrameProvider();
        int frameCount = 0;
        provider.FrameAvailable += (sender, args) => frameCount++;

        // Act - Simulate 3 frames of rendering
        for (int frame = 0; frame < 3; frame++)
        {
            provider.IsGraphics = (frame % 2 == 0);
            
            var backBuffer = provider.BorrowWritable();
            backBuffer.Clear();
            
            // Draw frame-specific content
            backBuffer.SetPixel(frame * 100, 96, 0);
            
            provider.CommitWritable();
        }

        var finalFrame = provider.GetFrame();

        // Assert
        Assert.Equal(3, frameCount);
        Assert.True(finalFrame.GetPixel(200, 96, 0)); // Last frame's pixel
        Assert.False(finalFrame.GetPixel(0, 96, 0));  // Cleared
        Assert.False(finalFrame.GetPixel(100, 96, 0)); // Cleared
    }
}
