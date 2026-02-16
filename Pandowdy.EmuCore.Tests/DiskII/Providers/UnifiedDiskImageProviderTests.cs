// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using CommonUtil;
using Pandowdy.EmuCore.DiskII;
using Pandowdy.EmuCore.DiskII.Providers;
using Xunit;

namespace Pandowdy.EmuCore.Tests.DiskII.Providers;

/// <summary>
/// Unit tests for the UnifiedDiskImageProvider class.
/// </summary>
public class UnifiedDiskImageProviderTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_NullDiskImage_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new UnifiedDiskImageProvider(null!));
    }

    [Fact]
    public void Constructor_ValidDiskImage_InitializesProperties()
    {
        // Arrange
        var diskImage = new InternalDiskImage
        {
            SourceFilePath = "test.woz",
            OriginalFormat = DiskFormat.Woz,
            OptimalBitTiming = 32,
            IsWriteProtected = false
        };

        // Act
        var provider = new UnifiedDiskImageProvider(diskImage);

        // Assert
        Assert.Equal("test.woz", provider.FilePath);
        Assert.Equal(32, provider.OptimalBitTiming);
        Assert.False(provider.IsWriteProtected);
        Assert.True(provider.IsWritable);
        Assert.Equal(0, provider.CurrentQuarterTrack);
    }

    [Fact]
    public void Constructor_DiskImageWithoutSourcePath_ReturnsInternalAsFilePath()
    {
        // Arrange
        var diskImage = new InternalDiskImage();

        // Act
        var provider = new UnifiedDiskImageProvider(diskImage);

        // Assert
        Assert.Equal("(internal)", provider.FilePath);
    }

    #endregion

    #region Property Tests

    [Fact]
    public void IsWriteProtected_ReflectsDiskImageState()
    {
        // Arrange
        var diskImage = new InternalDiskImage { IsWriteProtected = true };
        var provider = new UnifiedDiskImageProvider(diskImage);

        // Assert
        Assert.True(provider.IsWriteProtected);

        // Act - Change via provider
        provider.IsWriteProtected = false;

        // Assert - Both provider and disk image updated
        Assert.False(provider.IsWriteProtected);
        Assert.False(diskImage.IsWriteProtected);
    }

    [Fact]
    public void OptimalBitTiming_ReturnsDiskImageValue()
    {
        // Arrange
        var diskImage = new InternalDiskImage { OptimalBitTiming = 31 };
        var provider = new UnifiedDiskImageProvider(diskImage);

        // Act & Assert
        Assert.Equal(31, provider.OptimalBitTiming);
    }

    [Fact]
    public void CurrentTrackBitCount_ReturnsCorrectValueForCurrentTrack()
    {
        // Arrange
        var diskImage = new InternalDiskImage();
        diskImage.QuarterTrackBitCounts[0] = 51200;
        diskImage.QuarterTrackBitCounts[4] = 50000;  // Track 1 = quarter-track 4
        var provider = new UnifiedDiskImageProvider(diskImage);

        // Act & Assert - Track 0 (quarter-tracks 0-3)
        Assert.Equal(51200, provider.CurrentTrackBitCount);

        // Move to track 1 (quarter-track 4)
        provider.SetQuarterTrack(4);
        Assert.Equal(50000, provider.CurrentTrackBitCount);
    }

    [Fact]
    public void CurrentTrackBitCount_OutOfBounds_ReturnsDefaultValue()
    {
        // Arrange
        var diskImage = new InternalDiskImage(physicalTrackCount: 35);
        var provider = new UnifiedDiskImageProvider(diskImage);

        // Act - Move to invalid track
        provider.SetQuarterTrack(200);

        // Assert
        Assert.Equal(51200, provider.CurrentTrackBitCount); // Default standard track length
    }

    [Fact]
    public void TrackBitPosition_ReturnsCurrentPositionInTrack()
    {
        // Arrange
        var diskImage = new InternalDiskImage();
        var provider = new UnifiedDiskImageProvider(diskImage);

        // Act - Set position manually on track 0
        diskImage.QuarterTracks[0]!.BitPosition = 1000;

        // Assert
        Assert.Equal(1000, provider.TrackBitPosition);
    }

    [Fact]
    public void IsWritable_AlwaysReturnsTrue()
    {
        // Arrange
        var diskImage = new InternalDiskImage();
        var provider = new UnifiedDiskImageProvider(diskImage);

        // Act & Assert
        Assert.True(provider.IsWritable);
    }

    #endregion

    #region Quarter Track Tests

    [Fact]
    public void SetQuarterTrack_UpdatesCurrentQuarterTrack()
    {
        // Arrange
        var diskImage = new InternalDiskImage();
        var provider = new UnifiedDiskImageProvider(diskImage);

        // Act
        provider.SetQuarterTrack(8);

        // Assert
        Assert.Equal(8, provider.CurrentQuarterTrack);
    }

    [Fact]
    public void SetQuarterTrack_MapsToCorrectPhysicalTrack()
    {
        // Arrange
        var diskImage = new InternalDiskImage();
        var provider = new UnifiedDiskImageProvider(diskImage);

        // Act & Assert - Quarter tracks map to tracks via division by 4
        provider.SetQuarterTrack(0);  // Track 0
        Assert.Equal(0, provider.CurrentQuarterTrack);

        provider.SetQuarterTrack(3);  // Track 0
        Assert.Equal(3, provider.CurrentQuarterTrack);

        provider.SetQuarterTrack(4);  // Track 1
        Assert.Equal(4, provider.CurrentQuarterTrack);

        provider.SetQuarterTrack(7);  // Track 1
        Assert.Equal(7, provider.CurrentQuarterTrack);

        provider.SetQuarterTrack(8);  // Track 2
        Assert.Equal(8, provider.CurrentQuarterTrack);
    }

    [Fact]
    public void SetQuarterTrack_ScalesPositionWhenChangingTracks()
    {
        // Arrange - Create disk with different track lengths
        var diskImage = new InternalDiskImage();
        diskImage.QuarterTrackBitCounts[0] = 50000;
        diskImage.QuarterTrackBitCounts[4] = 52000;  // Track 1 = quarter-track 4
        diskImage.QuarterTracks[0]!.BitPosition = 25000; // Halfway through track 0
        var provider = new UnifiedDiskImageProvider(diskImage);

        // Act - Move from track 0 to track 1
        provider.SetQuarterTrack(0);  // Start at track 0
        provider.SetQuarterTrack(4);  // Move to track 1

        // Assert - Position should scale: 25000 * (52000 / 50000) = 26000
        Assert.Equal(26000, diskImage.QuarterTracks[4]!.BitPosition);
    }

    [Fact]
    public void SetQuarterTrack_SameTrack_DoesNotScalePosition()
    {
        // Arrange
        var diskImage = new InternalDiskImage();
        diskImage.QuarterTracks[0]!.BitPosition = 1000;
        var provider = new UnifiedDiskImageProvider(diskImage);

        // Act - Stay on track 0
        provider.SetQuarterTrack(0);
        provider.SetQuarterTrack(1);  // Still track 0
        provider.SetQuarterTrack(2);  // Still track 0

        // Assert - Position unchanged
        Assert.Equal(1000, diskImage.QuarterTracks[0]!.BitPosition);
    }

    #endregion

    #region Bit Reading Tests

    [Fact]
    public void GetBit_InitializesOnFirstAccess()
    {
        // Arrange
        var diskImage = new InternalDiskImage();
        var provider = new UnifiedDiskImageProvider(diskImage);

        // Act - Read bit without NotifyMotorStateChanged
        var bit = provider.GetBit(cycleCount: 1000);

        // Assert - Should not throw, returns a bit
        Assert.NotNull(bit);
    }

    [Fact]
    public void GetBit_ReadsBitsFromCurrentTrack()
    {
        // Arrange
        var diskImage = new InternalDiskImage();
        // Write pattern to track 0
        diskImage.QuarterTracks[0]!.BitPosition = 0;
        for (int i = 0; i < 100; i++)
        {
            diskImage.QuarterTracks[0]!.WriteBit(i % 2);
        }
        var provider = new UnifiedDiskImageProvider(diskImage);
        provider.NotifyMotorStateChanged(true, 0);

        // Act - Read bits
        var bits = new List<bool?>();
        for (ulong cycle = 0; cycle < 100 * 4; cycle += 4)
        {
            bits.Add(provider.GetBit(cycle));
        }

        // Assert - Should read alternating pattern
        Assert.Equal(100, bits.Count);
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(i % 2 == 1, bits[i]);
        }
    }

    [Fact]
    public void GetBit_OutOfBoundsTrack_ReturnsRandomBits()
    {
        // Arrange
        var diskImage = new InternalDiskImage(physicalTrackCount: 35);
        var provider = new UnifiedDiskImageProvider(diskImage);
        provider.NotifyMotorStateChanged(true, 0);

        // Act - Move to invalid track
        provider.SetQuarterTrack(200);
        var bit = provider.GetBit(cycleCount: 100);

        // Assert - Should return a bit (random noise)
        Assert.NotNull(bit);
    }

    [Fact]
    public void GetBit_HandlesCycleCountGoingBackwards()
    {
        // Arrange
        var diskImage = new InternalDiskImage();
        var provider = new UnifiedDiskImageProvider(diskImage);
        provider.NotifyMotorStateChanged(true, 1000);

        // Act - Read at later cycle, then earlier cycle (simulates reset)
        provider.GetBit(cycleCount: 2000);
        var bit = provider.GetBit(cycleCount: 500);

        // Assert - Should not throw
        Assert.NotNull(bit);
    }

    [Fact]
    public void NotifyMotorStateChanged_MotorOn_SetsStartingPosition()
    {
        // Arrange
        var diskImage = new InternalDiskImage();
        var provider = new UnifiedDiskImageProvider(diskImage);

        // Act
        provider.NotifyMotorStateChanged(true, 12345);

        // Read a bit and verify no warning
        var bit = provider.GetBit(cycleCount: 12350);

        // Assert
        Assert.NotNull(bit);
    }

    #endregion

    #region Write Tests

    [Fact]
    public void WriteBit_WritesToCurrentTrack()
    {
        // Arrange
        var diskImage = new InternalDiskImage();
        var provider = new UnifiedDiskImageProvider(diskImage);
        diskImage.QuarterTracks[0]!.BitPosition = 100;

        // Act
        provider.WriteBit(true, cycleCount: 0);
        provider.WriteBit(false, cycleCount: 4);
        provider.WriteBit(true, cycleCount: 8);

        // Assert - Read back written bits
        diskImage.QuarterTracks[0]!.BitPosition = 100;
        Assert.Equal(1, diskImage.QuarterTracks[0]!.ReadNextBit());
        Assert.Equal(0, diskImage.QuarterTracks[0]!.ReadNextBit());
        Assert.Equal(1, diskImage.QuarterTracks[0]!.ReadNextBit());
    }

    [Fact]
    public void WriteBit_MarksDiskAsDirty()
    {
        // Arrange
        var diskImage = new InternalDiskImage();
        var provider = new UnifiedDiskImageProvider(diskImage);
        Assert.False(diskImage.IsDirty); // Precondition

        // Act
        provider.WriteBit(true, cycleCount: 0);

        // Assert
        Assert.True(diskImage.IsDirty);
    }

    [Fact]
    public void WriteBit_WriteProtected_DoesNotWrite()
    {
        // Arrange
        var diskImage = new InternalDiskImage { IsWriteProtected = true };
        var provider = new UnifiedDiskImageProvider(diskImage);
        diskImage.QuarterTracks[0]!.BitPosition = 100;
        diskImage.QuarterTracks[0]!.WriteBit(0); // Write known value

        // Act - Try to write
        diskImage.QuarterTracks[0]!.BitPosition = 100;
        bool writeResult = provider.WriteBit(true, cycleCount: 0);

        // Assert - Value should be unchanged
        Assert.False(writeResult); // Write should fail
        diskImage.QuarterTracks[0]!.BitPosition = 100;
        Assert.Equal(0, diskImage.QuarterTracks[0]!.ReadNextBit());
        Assert.False(diskImage.IsDirty); // Should not be marked dirty
    }

    [Fact]
    public void WriteBit_OutOfBoundsTrack_DoesNotThrow()
    {
        // Arrange
        var diskImage = new InternalDiskImage(physicalTrackCount: 35);
        var provider = new UnifiedDiskImageProvider(diskImage);

        // Act - Move to invalid track and try to write
        provider.SetQuarterTrack(200);

        // Assert - Should not throw, returns false
        bool result = provider.WriteBit(true, cycleCount: 0);
        Assert.False(result);
    }

    #endregion

    #region AdvanceAndReadBits Tests

    [Fact]
    public void AdvanceAndReadBits_ReadsCorrectNumberOfBits()
    {
        // Arrange
        var diskImage = new InternalDiskImage();
        var provider = new UnifiedDiskImageProvider(diskImage);
        Span<bool> buffer = stackalloc bool[10];

        // Act - Read with enough cycles for multiple bits
        double cyclesPerBit = 32.0 / 8.0; // 4 cycles per bit
        int bitsRead = provider.AdvanceAndReadBits(elapsedCycles: cyclesPerBit * 5, buffer);

        // Assert
        Assert.Equal(5, bitsRead);
    }

    [Fact]
    public void AdvanceAndReadBits_RespectsBufferSize()
    {
        // Arrange
        var diskImage = new InternalDiskImage();
        var provider = new UnifiedDiskImageProvider(diskImage);
        Span<bool> buffer = stackalloc bool[3];

        // Act - Request more bits than buffer can hold
        double cyclesPerBit = 32.0 / 8.0;
        int bitsRead = provider.AdvanceAndReadBits(elapsedCycles: cyclesPerBit * 10, buffer);

        // Assert - Should only read buffer size
        Assert.Equal(3, bitsRead);
    }

    [Fact]
    public void AdvanceAndReadBits_OutOfBoundsTrack_ReturnsRandomBits()
    {
        // Arrange
        var diskImage = new InternalDiskImage(physicalTrackCount: 35);
        var provider = new UnifiedDiskImageProvider(diskImage);
        provider.SetQuarterTrack(200); // Out of bounds
        Span<bool> buffer = stackalloc bool[10];

        // Act
        double cyclesPerBit = 32.0 / 8.0;
        int bitsRead = provider.AdvanceAndReadBits(elapsedCycles: cyclesPerBit * 5, buffer);

        // Assert - Should return bits (random noise)
        Assert.Equal(5, bitsRead);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var diskImage = new InternalDiskImage();
        var provider = new UnifiedDiskImageProvider(diskImage);

        // Act & Assert - Should not throw
        provider.Dispose();
        provider.Dispose();
        provider.Dispose();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void CompleteReadWriteWorkflow()
    {
        // Arrange - Create disk and provider
        var diskImage = new InternalDiskImage
        {
            SourceFilePath = "test.dsk",
            OriginalFormat = DiskFormat.Dsk
        };
        var provider = new UnifiedDiskImageProvider(diskImage);
        provider.NotifyMotorStateChanged(true, 0);

        // Act - Write pattern to track 0 using provider
        diskImage.QuarterTracks[0]!.BitPosition = 0;
        for (int i = 0; i < 100; i++)
        {
            provider.WriteBit(i % 2 == 1, cycleCount: (ulong)(i * 4));
        }

        // Read back pattern using GetBit
        var readBits = new List<bool?>();
        diskImage.QuarterTracks[0]!.BitPosition = 0; // Reset position for reading
        for (int i = 0; i < 100; i++)
        {
            readBits.Add(diskImage.QuarterTracks[0]!.ReadNextBit() == 1);
        }

        // Assert - Verify pattern
        Assert.Equal(100, readBits.Count);
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(i % 2 == 1, readBits[i]);
        }
        Assert.True(diskImage.IsDirty);
    }

    [Fact]
    public void TrackSwitch_WithScaling_PreservesRelativePosition()
    {
        // Arrange - Create disk with variable track lengths
        var diskImage = new InternalDiskImage();
        diskImage.QuarterTrackBitCounts[0] = 50000;
        diskImage.QuarterTrackBitCounts[4] = 52000;  // Track 1 = quarter-track 4
        diskImage.QuarterTracks[0]!.BitPosition = 10000; // 20% through track 0
        var provider = new UnifiedDiskImageProvider(diskImage);

        // Act - Switch to track 1
        provider.SetQuarterTrack(0);  // Start at track 0
        provider.SetQuarterTrack(4);  // Move to track 1

        // Assert - Should be approximately 20% through track 1
        // 10000 * (52000 / 50000) = 10400
        Assert.Equal(10400, diskImage.QuarterTracks[4]!.BitPosition);
    }

    #endregion
}
