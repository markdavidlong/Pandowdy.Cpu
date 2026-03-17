// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Machine;
using Pandowdy.EmuCore.Video;

namespace Pandowdy.EmuCore.Tests.Services;

/// <summary>
/// Tests for FrameProvider - manages double-buffered frame rendering for Apple II display.
/// Provides front/back buffer management with frame synchronization events.
/// </summary>
public class FrameProviderTests
{
    [Fact]
    public void Dimensions_DerivedFromBitmapDataArray()
    {
        // Arrange & Act
        var provider = new FrameProvider();

        // Assert - Dimensions should match BitmapDataArray
        
        Assert.Equal(560, provider.Width);         // BitmapDataArray.Width
        Assert.Equal(192, provider.Height);              // BitmapDataArray.Height
    }



    [Fact]
    public void Constructor_VerifiesGeometry()
    {
        // Arrange & Act - Constructor should verify geometry
        var provider = new FrameProvider();

        // Assert - If we get here, geometry validation passed
        Assert.Equal(560, provider.Width);
        Assert.Equal(192, provider.Height);
        
        // Note: Constructor throws InvalidOperationException if:
        // - Front/back buffer dimensions don't match
        // - Width is not 560 pixels (80 chars * 7 pixels/char)
        // - Height is not 192 scanlines
    }

    [Fact]
    public void DefaultState_NotGraphics()
    {
        // Arrange & Act
        var provider = new FrameProvider();

        // Assert
        Assert.False(provider.IsGraphics);
        Assert.False(provider.IsMixed);
    }

    [Fact]
    public void IsGraphics_CanBeSet()
    {
        // Arrange
        var provider = new FrameProvider
        {
            // Act
            IsGraphics = true
        };

        // Assert
        Assert.True(provider.IsGraphics);
    }

    [Fact]
    public void IsMixed_CanBeSet()
    {
        // Arrange
        var provider = new FrameProvider
        {
            // Act
            IsMixed = true
        };

        // Assert
        Assert.True(provider.IsMixed);
    }

    [Fact]
    public void GetFrame_ReturnsValidBitmap()
    {
        // Arrange
        var provider = new FrameProvider();

        // Act
        var frame = provider.GetFrame();

        // Assert
        Assert.NotNull(frame);
    }

    [Fact]
    public void BorrowWritable_ReturnsValidBitmap()
    {
        // Arrange
        var provider = new FrameProvider();

        // Act
        var writable = provider.BorrowWritable();

        // Assert
        Assert.NotNull(writable);
    }

    [Fact]
    public void DoubleBuffering_FrontAndBackAreDifferent()
    {
        // Arrange
        var provider = new FrameProvider();

        // Act
        var front = provider.GetFrame();
        var back = provider.BorrowWritable();

        // Assert - Should be different instances (double buffering)
        Assert.NotSame(front, back);
    }

    [Fact]
    public void CommitWritable_SwapsBuffers()
    {
        // Arrange
        var provider = new FrameProvider();
        var originalFront = provider.GetFrame();
        var originalBack = provider.BorrowWritable();

        // Mark the back buffer to identify it
        originalBack!.SetPixel(10, 10, 0);

        // Act
        provider.CommitWritable(originalBack);

        // Assert - Front should now be what was back
        var newFront = provider.GetFrame();
        Assert.True(newFront.GetPixel(10, 10, 0));
    }

    [Fact]
    public void CommitWritable_RaisesFrameAvailableEvent()
    {
        // Arrange
        var provider = new FrameProvider();
        bool eventRaised = false;
        provider.FrameAvailable += (sender, args) => eventRaised = true;
        var buffer = provider.BorrowWritable();

        // Act
        provider.CommitWritable(buffer!);

        // Assert
        Assert.True(eventRaised);
    }

    [Fact]
    public void CommitWritable_EventSenderIsProvider()
    {
        // Arrange
        var provider = new FrameProvider();
        object? capturedSender = null;
        provider.FrameAvailable += (sender, args) => capturedSender = sender;
        var buffer = provider.BorrowWritable();

        // Act
        provider.CommitWritable(buffer!);

        // Assert
        Assert.Same(provider, capturedSender);
    }

    [Fact]
    public void MultipleCommits_WorkCorrectly()
    {
        // Arrange
        var provider = new FrameProvider();
        int eventCount = 0;
        provider.FrameAvailable += (sender, args) => eventCount++;

        // Act
        var buffer1 = provider.BorrowWritable();
        provider.CommitWritable(buffer1!);
        var buffer2 = provider.BorrowWritable();
        provider.CommitWritable(buffer2!);
        var buffer3 = provider.BorrowWritable();
        provider.CommitWritable(buffer3!);

        // Assert
        Assert.Equal(3, eventCount);
    }

    [Fact]
    public void TypicalUsageScenario()
    {
        // Arrange
        var provider = new FrameProvider();
        bool frameReceived = false;
        provider.FrameAvailable += (sender, args) => frameReceived = true;

        // Act - Simulate rendering workflow
        provider.IsGraphics = true;
        provider.IsMixed = false;

        var backBuffer = provider.BorrowWritable();
        backBuffer!.SetPixel(100, 100, 0);
        backBuffer.SetPixel(200, 100, 1);

        provider.CommitWritable(backBuffer);

        var frontBuffer = provider.GetFrame();

        // Assert
        Assert.True(frameReceived);
        Assert.True(provider.IsGraphics);
        Assert.False(provider.IsMixed);
        Assert.True(frontBuffer.GetPixel(100, 100, 0));
        Assert.True(frontBuffer.GetPixel(200, 100, 1));
    }

    [Fact]
    public void BufferSwap_PreservesData()
    {
        // Arrange
        var provider = new FrameProvider();
        
        // Write pattern to back buffer
        var back = provider.BorrowWritable();
        back!.SetPixel(50, 50, 0);
        back.SetPixel(100, 100, 1);

        // Act - Commit (swap)
        provider.CommitWritable(back);

        // Assert - Pattern now in front buffer
        var front = provider.GetFrame();
        Assert.True(front.GetPixel(50, 50, 0));
        Assert.True(front.GetPixel(100, 100, 1));
    }

    [Fact]
    public void ClearBackBuffer_DoesNotAffectFront()
    {
        // Arrange
        var provider = new FrameProvider();
        
        // Set data in front buffer
        var back = provider.BorrowWritable();
        back!.SetPixel(50, 50, 0);
        provider.CommitWritable(back);
        
        var front = provider.GetFrame();
        Assert.True(front.GetPixel(50, 50, 0));

        // Act - Clear new back buffer
        var newBack = provider.BorrowWritable();
        newBack!.Clear();

        // Assert - Front buffer unchanged (hasn't been swapped yet)
        Assert.True(front.GetPixel(50, 50, 0));
    }
}
