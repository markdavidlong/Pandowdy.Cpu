// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using CommonUtil;
using Pandowdy.EmuCore.DiskII;
using Pandowdy.Project.Services;
using Xunit;

namespace Pandowdy.Project.Tests.Services;

/// <summary>
/// Tests for <see cref="DiskBlobStore"/> serialize/deserialize functionality.
/// </summary>
public class DiskBlobStoreTests
{
    #region Serialize/Deserialize Round-Trip

    [Fact]
    public void Serialize_Deserialize_StandardDisk_BinaryIdentical()
    {
        // Arrange: Create a standard 35-track disk with 51,200 bits per track
        var originalImage = CreateStandardDisk(trackCount: 35, bitsPerTrack: 51200);

        // Act: Serialize and deserialize
        var blob = DiskBlobStore.Serialize(originalImage);
        var deserializedImage = DiskBlobStore.Deserialize(blob);

        // Assert: Physical properties match
        Assert.Equal(originalImage.PhysicalTrackCount, deserializedImage.PhysicalTrackCount);
        Assert.Equal(originalImage.QuarterTrackCount, deserializedImage.QuarterTrackCount);
        Assert.Equal(originalImage.OptimalBitTiming, deserializedImage.OptimalBitTiming);
        Assert.Equal(originalImage.IsWriteProtected, deserializedImage.IsWriteProtected);

        // Assert: Track data matches (byte-by-byte comparison)
        for (int i = 0; i < originalImage.QuarterTrackCount; i++)
        {
            if (originalImage.QuarterTracks[i] is null)
            {
                Assert.Null(deserializedImage.QuarterTracks[i]);
            }
            else
            {
                Assert.NotNull(deserializedImage.QuarterTracks[i]);
                Assert.Equal(originalImage.QuarterTrackBitCounts[i], deserializedImage.QuarterTrackBitCounts[i]);

                // Compare track data byte-by-byte
                var originalTrack = originalImage.QuarterTracks[i]!;
                var deserializedTrack = deserializedImage.QuarterTracks[i]!;
                originalTrack.BitPosition = 0;
                deserializedTrack.BitPosition = 0;

                int byteCount = (originalImage.QuarterTrackBitCounts[i] + 7) / 8;
                for (int j = 0; j < byteCount; j++)
                {
                    Assert.Equal(originalTrack.ReadOctet(), deserializedTrack.ReadOctet());
                }
            }
        }
    }

    [Fact]
    public void Serialize_Deserialize_SparseQuarterTracks_PreservesPresenceBitmap()
    {
        // Arrange: Create a disk with only every 4th quarter-track populated (whole tracks only)
        var originalImage = CreateSparseDisk(wholeTrackCount: 35);

        // Act
        var blob = DiskBlobStore.Serialize(originalImage);
        var deserializedImage = DiskBlobStore.Deserialize(blob);

        // Assert: Only the whole-track positions (0, 4, 8, ..., 136) have data
        for (int i = 0; i < deserializedImage.QuarterTrackCount; i++)
        {
            if (i % 4 == 0)
            {
                Assert.NotNull(deserializedImage.QuarterTracks[i]);
                Assert.Equal(51200, deserializedImage.QuarterTrackBitCounts[i]);
            }
            else
            {
                Assert.Null(deserializedImage.QuarterTracks[i]);
                Assert.Equal(0, deserializedImage.QuarterTrackBitCounts[i]);
            }
        }
    }

    [Fact]
    public void Serialize_Deserialize_WriteProtectedDisk_PreservesFlag()
    {
        // Arrange
        var originalImage = CreateStandardDisk(trackCount: 35, bitsPerTrack: 51200, isWriteProtected: true);

        // Act
        var blob = DiskBlobStore.Serialize(originalImage);
        var deserializedImage = DiskBlobStore.Deserialize(blob);

        // Assert
        Assert.True(deserializedImage.IsWriteProtected);
    }

    [Fact]
    public void Serialize_Deserialize_NonStandardTiming_PreservesTiming()
    {
        // Arrange: Copy-protected disk with non-standard timing
        var originalImage = CreateStandardDisk(trackCount: 35, bitsPerTrack: 51200, optimalBitTiming: 31);

        // Act
        var blob = DiskBlobStore.Serialize(originalImage);
        var deserializedImage = DiskBlobStore.Deserialize(blob);

        // Assert
        Assert.Equal(31, deserializedImage.OptimalBitTiming);
    }

    [Fact]
    public void Deserialize_CorruptMagicBytes_ThrowsInvalidDataException()
    {
        // Arrange: Valid blob with corrupted magic bytes
        var originalImage = CreateStandardDisk(trackCount: 35, bitsPerTrack: 51200);
        var blob = DiskBlobStore.Serialize(originalImage);
        blob[0] = 0xFF;  // Corrupt magic byte

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => DiskBlobStore.Deserialize(blob));
    }

    [Fact]
    public void Deserialize_CorruptCrc_ThrowsInvalidDataException()
    {
        // Arrange: Valid blob with corrupted CRC footer
        var originalImage = CreateStandardDisk(trackCount: 35, bitsPerTrack: 51200);
        var blob = DiskBlobStore.Serialize(originalImage);
        blob[^1] ^= 0xFF;  // Flip all bits in last CRC byte

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => DiskBlobStore.Deserialize(blob));
    }

    [Fact]
    public void Serialize_Deserialize_40TrackDisk_RoundTrips()
    {
        // Arrange: Non-standard 40-track disk
        var originalImage = CreateStandardDisk(trackCount: 40, bitsPerTrack: 51200);

        // Act
        var blob = DiskBlobStore.Serialize(originalImage);
        var deserializedImage = DiskBlobStore.Deserialize(blob);

        // Assert
        Assert.Equal(40, deserializedImage.PhysicalTrackCount);
        Assert.Equal(157, deserializedImage.QuarterTrackCount);  // (40-1)*4+1 = 157
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a standard disk image with all whole tracks populated.
    /// </summary>
    private static InternalDiskImage CreateStandardDisk(
        int trackCount,
        int bitsPerTrack,
        byte optimalBitTiming = 32,
        bool isWriteProtected = false)
    {
        int quarterTrackCount = (trackCount - 1) * 4 + 1;
        var quarterTracks = new CircularBitBuffer?[quarterTrackCount];
        var quarterTrackBitCounts = new int[quarterTrackCount];

        // Populate whole tracks only (indices 0, 4, 8, ...)
        for (int i = 0; i < quarterTrackCount; i += 4)
        {
            var trackData = GenerateTrackData(bitsPerTrack);
            quarterTracks[i] = new CircularBitBuffer(trackData, 0, 0, bitsPerTrack);
            quarterTrackBitCounts[i] = bitsPerTrack;
        }

        return new InternalDiskImage(trackCount, quarterTracks, quarterTrackBitCounts)
        {
            OptimalBitTiming = optimalBitTiming,
            IsWriteProtected = isWriteProtected
        };
    }

    /// <summary>
    /// Creates a sparse disk with only whole tracks populated (simulates NIB/DSK import).
    /// </summary>
    private static InternalDiskImage CreateSparseDisk(int wholeTrackCount)
    {
        return CreateStandardDisk(wholeTrackCount, bitsPerTrack: 51200);
    }

    /// <summary>
    /// Generates pseudo-random track data with Apple II GCR patterns.
    /// </summary>
    private static byte[] GenerateTrackData(int bitCount)
    {
        int byteCount = (bitCount + 7) / 8;
        var data = new byte[byteCount];
        var random = new Random(42);  // Fixed seed for reproducibility

        for (int i = 0; i < byteCount; i++)
        {
            // Generate GCR-like bytes (high bit set, no consecutive 0-bits)
            data[i] = (byte)(0x80 | (random.Next(0, 128)));
        }

        return data;
    }

    #endregion
}
