// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using CommonUtil;
using Pandowdy.EmuCore.DiskII;
using Xunit;

namespace Pandowdy.EmuCore.Tests.DiskII;

/// <summary>
/// Unit tests for the InternalDiskImage class.
/// </summary>
public class InternalDiskImageTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_DefaultParameters_Creates35TrackDiskWith137QuarterTracks()
    {
        // Act
        var disk = new InternalDiskImage();

        // Assert
        Assert.Equal(35, disk.PhysicalTrackCount);
        Assert.Equal(137, disk.QuarterTrackCount); // (35-1)*4+1 = 137
        Assert.NotNull(disk.QuarterTracks);
        Assert.NotNull(disk.QuarterTrackBitCounts);
        Assert.Equal(137, disk.QuarterTracks.Length);
        Assert.Equal(137, disk.QuarterTrackBitCounts.Length);
    }

    [Fact]
    public void Constructor_DefaultParameters_InitializesOnlyWholeTrackPositions()
    {
        // Act
        var disk = new InternalDiskImage();

        // Assert - Only whole-track positions (0, 4, 8, 12...) should be populated
        for (int qTrack = 0; qTrack < disk.QuarterTrackCount; qTrack++)
        {
            int quarter = InternalDiskImage.QuarterTrackIndexToQuarter(qTrack);
            if (quarter == 0)
            {
                // Whole-track position - should have data
                Assert.NotNull(disk.QuarterTracks[qTrack]);
                Assert.Equal(51200, disk.QuarterTrackBitCounts[qTrack]);
            }
            else
            {
                // Fractional position - should be null
                Assert.Null(disk.QuarterTracks[qTrack]);
                Assert.Equal(0, disk.QuarterTrackBitCounts[qTrack]);
            }
        }
    }

    [Fact]
    public void Constructor_CustomTrackCount_CreatesCorrectQuarterTrackCount()
    {
        // Act
        var disk = new InternalDiskImage(physicalTrackCount: 40);

        // Assert
        Assert.Equal(40, disk.PhysicalTrackCount);
        Assert.Equal(157, disk.QuarterTrackCount); // (40-1)*4+1 = 157
        Assert.Equal(157, disk.QuarterTracks.Length);
        Assert.Equal(157, disk.QuarterTrackBitCounts.Length);
    }

    [Fact]
    public void Constructor_CustomBitCount_SetsWholeTrackPositionsToCustomBitCount()
    {
        // Arrange
        const int customBitCount = 50000;

        // Act
        var disk = new InternalDiskImage(physicalTrackCount: 35, standardTrackBitCount: customBitCount);

        // Assert - Check whole-track positions (0, 4, 8, 12...)
        for (int track = 0; track < 35; track++)
        {
            int quarterIndex = InternalDiskImage.TrackToQuarterTrackIndex(track);
            Assert.Equal(customBitCount, disk.QuarterTrackBitCounts[quarterIndex]);
        }
    }

    [Fact]
    public void Constructor_InvalidTrackCount_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new InternalDiskImage(physicalTrackCount: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InternalDiskImage(physicalTrackCount: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InternalDiskImage(physicalTrackCount: 41));
    }

    [Fact]
    public void Constructor_InvalidBitCount_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new InternalDiskImage(physicalTrackCount: 35, standardTrackBitCount: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InternalDiskImage(physicalTrackCount: 35, standardTrackBitCount: -1));
    }

    [Fact]
    public void Constructor_WithPreAllocatedQuarterTracks_StoresArrays()
    {
        // Arrange
        int physicalTrackCount = 35;
        int quarterTrackCount = InternalDiskImage.CalculateQuarterTrackCount(physicalTrackCount);
        var quarterTracks = new CircularBitBuffer?[quarterTrackCount];
        var bitCounts = new int[quarterTrackCount];

        // Populate only whole-track positions
        for (int track = 0; track < physicalTrackCount; track++)
        {
            int qIndex = InternalDiskImage.TrackToQuarterTrackIndex(track);
            byte[] trackData = new byte[6400]; // 51200 bits = 6400 bytes
            quarterTracks[qIndex] = new CircularBitBuffer(
                trackData,
                byteOffset: 0,
                bitOffset: 0,
                bitCount: 51200,
                new GroupBool(),
                isReadOnly: false
            );
            bitCounts[qIndex] = 51200;
        }

        // Act
        var disk = new InternalDiskImage(physicalTrackCount, quarterTracks, bitCounts);

        // Assert
        Assert.Same(quarterTracks, disk.QuarterTracks);
        Assert.Same(bitCounts, disk.QuarterTrackBitCounts);
        Assert.Equal(35, disk.PhysicalTrackCount);
        Assert.Equal(137, disk.QuarterTrackCount);
    }

    [Fact]
    public void Constructor_WithMismatchedArrayLengths_ThrowsArgumentException()
    {
        // Arrange
        var quarterTracks = new CircularBitBuffer?[137]; // Correct for 35 tracks
        var bitCounts = new int[140]; // Wrong length

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new InternalDiskImage(35, quarterTracks, bitCounts));
    }

    [Fact]
    public void Constructor_WithWrongQuarterTrackArrayLength_ThrowsArgumentException()
    {
        // Arrange
        var quarterTracks = new CircularBitBuffer?[35]; // Wrong - should be 137 for 35 physical tracks
        var bitCounts = new int[35];

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new InternalDiskImage(35, quarterTracks, bitCounts));
    }

    #endregion

    #region Property Tests

    [Fact]
    public void IsWriteProtected_DefaultValue_IsFalse()
    {
        // Arrange
        var disk = new InternalDiskImage();

        // Act & Assert
        Assert.False(disk.IsWriteProtected);
    }

    [Fact]
    public void IsWriteProtected_CanBeSetAndGet()
    {
        // Arrange
        var disk = new InternalDiskImage();

        // Act
        disk.IsWriteProtected = true;

        // Assert
        Assert.True(disk.IsWriteProtected);

        // Act
        disk.IsWriteProtected = false;

        // Assert
        Assert.False(disk.IsWriteProtected);
    }

    [Fact]
    public void OptimalBitTiming_DefaultValue_Is32()
    {
        // Arrange & Act
        var disk = new InternalDiskImage();

        // Assert
        Assert.Equal(32, disk.OptimalBitTiming);
    }

    [Fact]
    public void OptimalBitTiming_CanBeSetViaInitializer()
    {
        // Act
        var disk = new InternalDiskImage { OptimalBitTiming = 31 };

        // Assert
        Assert.Equal(31, disk.OptimalBitTiming);
    }

    [Fact]
    public void IsDirty_DefaultValue_IsFalse()
    {
        // Arrange & Act
        var disk = new InternalDiskImage();

        // Assert
        Assert.False(disk.IsDirty);
    }

    [Fact]
    public void SourceFilePath_DefaultValue_IsNull()
    {
        // Arrange & Act
        var disk = new InternalDiskImage();

        // Assert
        Assert.Null(disk.SourceFilePath);
    }

    [Fact]
    public void SourceFilePath_CanBeSetViaInitializer()
    {
        // Act
        var disk = new InternalDiskImage { SourceFilePath = "test.dsk" };

        // Assert
        Assert.Equal("test.dsk", disk.SourceFilePath);
    }

    [Fact]
    public void OriginalFormat_DefaultValue_IsUnknown()
    {
        // Arrange & Act
        var disk = new InternalDiskImage();

        // Assert
        Assert.Equal(DiskFormat.Unknown, disk.OriginalFormat);
    }

    [Fact]
    public void OriginalFormat_CanBeSetViaInitializer()
    {
        // Act
        var disk = new InternalDiskImage { OriginalFormat = DiskFormat.Woz };

        // Assert
        Assert.Equal(DiskFormat.Woz, disk.OriginalFormat);
    }

    #endregion

    #region Dirty Tracking Tests

    [Fact]
    public void MarkDirty_SetsDirtyFlagToTrue()
    {
        // Arrange
        var disk = new InternalDiskImage();
        Assert.False(disk.IsDirty); // Precondition

        // Act
        disk.MarkDirty();

        // Assert
        Assert.True(disk.IsDirty);
    }

    [Fact]
    public void MarkDirty_CalledMultipleTimes_RemainsDirty()
    {
        // Arrange
        var disk = new InternalDiskImage();

        // Act
        disk.MarkDirty();
        disk.MarkDirty();
        disk.MarkDirty();

        // Assert
        Assert.True(disk.IsDirty);
    }

    [Fact]
    public void ClearDirty_SetsDirtyFlagToFalse()
    {
        // Arrange
        var disk = new InternalDiskImage();
        disk.MarkDirty();
        Assert.True(disk.IsDirty); // Precondition

        // Act
        disk.ClearDirty();

        // Assert
        Assert.False(disk.IsDirty);
    }

    [Fact]
    public void ClearDirty_WhenNotDirty_RemainsFalse()
    {
        // Arrange
        var disk = new InternalDiskImage();
        Assert.False(disk.IsDirty); // Precondition

        // Act
        disk.ClearDirty();

        // Assert
        Assert.False(disk.IsDirty);
    }

    [Fact]
    public void DirtyFlag_CanBeToggledMultipleTimes()
    {
        // Arrange
        var disk = new InternalDiskImage();

        // Act & Assert - Cycle through dirty states
        Assert.False(disk.IsDirty);

        disk.MarkDirty();
        Assert.True(disk.IsDirty);

        disk.ClearDirty();
        Assert.False(disk.IsDirty);

        disk.MarkDirty();
        Assert.True(disk.IsDirty);

        disk.ClearDirty();
        Assert.False(disk.IsDirty);
    }

    #endregion

    #region Quarter-Track Access Tests

    [Fact]
    public void QuarterTracks_WholeTrackPosition_CanBeAccessed()
    {
        // Arrange
        var disk = new InternalDiskImage();

        // Act - Access whole-track positions (0, 4, 8, 12...)
        int qTrack0 = InternalDiskImage.TrackToQuarterTrackIndex(0);
        int qTrack34 = InternalDiskImage.TrackToQuarterTrackIndex(34);
        var track0 = disk.QuarterTracks[qTrack0];
        var track34 = disk.QuarterTracks[qTrack34];

        // Assert
        Assert.NotNull(track0);
        Assert.NotNull(track34);
    }

    [Fact]
    public void QuarterTracks_FractionalPosition_IsNull()
    {
        // Arrange
        var disk = new InternalDiskImage();

        // Act - Access fractional quarter-track position
        int qTrack1 = InternalDiskImage.TrackAndQuarterToIndex(0, 1); // Track 0.25
        int qTrack2 = InternalDiskImage.TrackAndQuarterToIndex(0, 2); // Track 0.50
        int qTrack3 = InternalDiskImage.TrackAndQuarterToIndex(0, 3); // Track 0.75

        // Assert - Fractional positions should be null
        Assert.Null(disk.QuarterTracks[qTrack1]);
        Assert.Null(disk.QuarterTracks[qTrack2]);
        Assert.Null(disk.QuarterTracks[qTrack3]);
    }

    [Fact]
    public void QuarterTrackBitCounts_WholeTrackPosition_HasStandardBitCount()
    {
        // Arrange
        var disk = new InternalDiskImage();

        // Act
        int qTrack0 = InternalDiskImage.TrackToQuarterTrackIndex(0);
        int qTrack34 = InternalDiskImage.TrackToQuarterTrackIndex(34);

        // Assert
        Assert.Equal(51200, disk.QuarterTrackBitCounts[qTrack0]);
        Assert.Equal(51200, disk.QuarterTrackBitCounts[qTrack34]);
    }

    [Fact]
    public void QuarterTrackBitCounts_CanBeModified()
    {
        // Arrange
        var disk = new InternalDiskImage();
        int qTrack0 = InternalDiskImage.TrackToQuarterTrackIndex(0);

        // Act
        disk.QuarterTrackBitCounts[qTrack0] = 50000;

        // Assert
        Assert.Equal(50000, disk.QuarterTrackBitCounts[qTrack0]);
    }

    [Fact]
    public void QuarterTracks_CircularBitBuffers_CanReadAndWriteBits()
    {
        // Arrange
        var disk = new InternalDiskImage();
        int qTrack0 = InternalDiskImage.TrackToQuarterTrackIndex(0);
        var track0 = disk.QuarterTracks[qTrack0];
        Assert.NotNull(track0);

        // Act - Write pattern to track 0
        track0.BitPosition = 0;
        for (int i = 0; i < 100; i++)
        {
            track0.WriteBit(i % 2); // Alternating 0,1,0,1...
        }

        // Read back pattern
        track0.BitPosition = 0;
        var readBits = new List<byte>();
        for (int i = 0; i < 100; i++)
        {
            readBits.Add(track0.ReadNextBit());
        }

        // Assert
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal((byte)(i % 2), readBits[i]);
        }
    }

    #endregion

    #region Static Helper Method Tests

    [Theory]
    [InlineData(35, 137)]
    [InlineData(40, 157)]
    [InlineData(1, 1)]
    public void CalculateQuarterTrackCount_ReturnsCorrectCount(int physicalTracks, int expectedQuarterTracks)
    {
        // Act
        int result = InternalDiskImage.CalculateQuarterTrackCount(physicalTracks);

        // Assert
        Assert.Equal(expectedQuarterTracks, result);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 4)]
    [InlineData(34, 136)]
    public void TrackToQuarterTrackIndex_ReturnsCorrectIndex(int track, int expectedQuarterIndex)
    {
        // Act
        int result = InternalDiskImage.TrackToQuarterTrackIndex(track);

        // Assert
        Assert.Equal(expectedQuarterIndex, result);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0, 1, 1)]
    [InlineData(0, 2, 2)]
    [InlineData(0, 3, 3)]
    [InlineData(1, 0, 4)]
    [InlineData(1, 1, 5)]
    [InlineData(34, 0, 136)]
    public void TrackAndQuarterToIndex_ReturnsCorrectIndex(int track, int quarter, int expectedIndex)
    {
        // Act
        int result = InternalDiskImage.TrackAndQuarterToIndex(track, quarter);

        // Assert
        Assert.Equal(expectedIndex, result);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 0)]
    [InlineData(4, 1)]
    [InlineData(5, 1)]
    [InlineData(136, 34)]
    public void QuarterTrackIndexToTrack_ReturnsCorrectTrack(int quarterIndex, int expectedTrack)
    {
        // Act
        int result = InternalDiskImage.QuarterTrackIndexToTrack(quarterIndex);

        // Assert
        Assert.Equal(expectedTrack, result);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(4, 0)]
    [InlineData(5, 1)]
    public void QuarterTrackIndexToQuarter_ReturnsCorrectQuarter(int quarterIndex, int expectedQuarter)
    {
        // Act
        int result = InternalDiskImage.QuarterTrackIndexToQuarter(quarterIndex);

        // Assert
        Assert.Equal(expectedQuarter, result);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void CompleteWorkflow_CreateModifySaveReload()
    {
        // Arrange - Create disk with custom properties
        var originalDisk = new InternalDiskImage
        {
            SourceFilePath = "test.woz",
            OriginalFormat = DiskFormat.Woz,
            OptimalBitTiming = 31,
            IsWriteProtected = false
        };

        // Act - Modify disk
        int qTrack0 = InternalDiskImage.TrackToQuarterTrackIndex(0);
        var track0 = originalDisk.QuarterTracks[qTrack0];
        Assert.NotNull(track0);
        track0.BitPosition = 0;
        track0.WriteBit(1);
        originalDisk.MarkDirty();

        // Assert - Verify state
        Assert.True(originalDisk.IsDirty);
        Assert.Equal("test.woz", originalDisk.SourceFilePath);
        Assert.Equal(DiskFormat.Woz, originalDisk.OriginalFormat);
        Assert.Equal(31, originalDisk.OptimalBitTiming);

        // Act - Simulate save (clear dirty)
        originalDisk.ClearDirty();

        // Assert - Verify saved state
        Assert.False(originalDisk.IsDirty);
    }

    [Fact]
    public void VariableTrackLengths_WozStyle_WorksCorrectly()
    {
        // Arrange - Create disk with variable track lengths (like WOZ format)
        int physicalTrackCount = 35;
        int quarterTrackCount = InternalDiskImage.CalculateQuarterTrackCount(physicalTrackCount);
        var quarterTracks = new CircularBitBuffer?[quarterTrackCount];
        var bitCounts = new int[quarterTrackCount];

        for (int track = 0; track < physicalTrackCount; track++)
        {
            // Vary bit counts slightly (50000-52000 bits, typical for WOZ)
            int trackBitCount = 50000 + (track * 50);
            int byteCount = (trackBitCount + 7) / 8; // Round up to nearest byte
            byte[] trackData = new byte[byteCount];

            int qIndex = InternalDiskImage.TrackToQuarterTrackIndex(track);
            quarterTracks[qIndex] = new CircularBitBuffer(
                trackData,
                byteOffset: 0,
                bitOffset: 0,
                bitCount: trackBitCount,
                new GroupBool(),
                isReadOnly: false
            );
            bitCounts[qIndex] = trackBitCount;
        }

        // Act
        var disk = new InternalDiskImage(physicalTrackCount, quarterTracks, bitCounts)
        {
            OriginalFormat = DiskFormat.Woz,
            OptimalBitTiming = 32
        };

        // Assert
        Assert.Equal(35, disk.PhysicalTrackCount);
        Assert.Equal(137, disk.QuarterTrackCount);
        for (int track = 0; track < physicalTrackCount; track++)
        {
            int qIndex = InternalDiskImage.TrackToQuarterTrackIndex(track);
            Assert.Equal(50000 + (track * 50), disk.QuarterTrackBitCounts[qIndex]);
        }
    }

    #endregion
}
