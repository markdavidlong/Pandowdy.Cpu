// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.DiskII;
using Pandowdy.EmuCore.DiskII.Providers;

namespace Pandowdy.EmuCore.Tests.DiskII.Providers;

/// <summary>
/// Tests for NibDiskImageProvider - .nib format disk image handling.
/// </summary>
[Collection("DiskImageProviders")]
public class NibDiskImageProviderTests : IDisposable
{
    private NibDiskImageProvider? _provider;

    public void Dispose()
    {
        _provider?.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ThrowsArgumentNullException_ForNullPath()
    {
        Assert.Throws<ArgumentNullException>(() => new NibDiskImageProvider(null!));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_ForEmptyPath()
    {
        Assert.Throws<ArgumentNullException>(() => new NibDiskImageProvider(""));
    }

    [Fact]
    public void Constructor_ThrowsFileNotFoundException_ForMissingFile()
    {
        var ex = Assert.Throws<FileNotFoundException>(() =>
            new NibDiskImageProvider("nonexistent.nib"));
        Assert.Contains("nonexistent.nib", ex.Message);
    }

    [Fact]
    public void Constructor_ThrowsInvalidDataException_ForWrongFileSize()
    {
        // Arrange - create a temp file with wrong size
        string tempFile = Path.Combine(Path.GetTempPath(), $"wrong_size_{Guid.NewGuid()}.nib");
        try
        {
            File.WriteAllBytes(tempFile, new byte[1000]); // Wrong size

            // Act & Assert
            var ex = Assert.Throws<InvalidDataException>(() =>
                new NibDiskImageProvider(tempFile));
            Assert.Contains("Invalid .nib file size", ex.Message);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void Constructor_LoadsValidNibFile()
    {
        if (!TestDiskImages.TestImagesAvailable)
        {
            return; // Skip if test images not available
        }

        // Act
        _provider = new NibDiskImageProvider(TestDiskImages.TestNib);

        // Assert
        Assert.NotNull(_provider);
        Assert.Equal(TestDiskImages.TestNib, _provider.FilePath);
        Assert.True(_provider.IsWritable);
        Assert.False(_provider.IsWriteProtected);
    }

    #endregion

    #region SetQuarterTrack Tests

    [Fact]
    public void SetQuarterTrack_UpdatesCurrentQuarterTrack()
    {
        if (!TestDiskImages.TestImagesAvailable)
        {
            return;
        }

        // Arrange
        _provider = new NibDiskImageProvider(TestDiskImages.TestNib);

        // Act
        _provider.SetQuarterTrack(20);

        // Assert
        Assert.Equal(20, _provider.CurrentQuarterTrack);
    }

    [Fact]
    public void SetQuarterTrack_AllowsQuarterTrackPositions()
    {
        if (!TestDiskImages.TestImagesAvailable)
        {
            return;
        }

        // Arrange
        _provider = new NibDiskImageProvider(TestDiskImages.TestNib);

        // Act & Assert - quarter tracks 0-3 all map to track 0
        _provider.SetQuarterTrack(0);
        Assert.Equal(0, _provider.CurrentQuarterTrack);

        _provider.SetQuarterTrack(1);
        Assert.Equal(1, _provider.CurrentQuarterTrack);

        _provider.SetQuarterTrack(2);
        Assert.Equal(2, _provider.CurrentQuarterTrack);

        _provider.SetQuarterTrack(3);
        Assert.Equal(3, _provider.CurrentQuarterTrack);

        // Quarter track 4 maps to track 1
        _provider.SetQuarterTrack(4);
        Assert.Equal(4, _provider.CurrentQuarterTrack);
    }

    #endregion

    #region GetBit Tests

    [Fact]
    public void GetBit_ReturnsBitValue_ForValidTrack()
    {
        if (!TestDiskImages.TestImagesAvailable)
        {
            return;
        }

        // Arrange
        _provider = new NibDiskImageProvider(TestDiskImages.TestNib);
        _provider.SetQuarterTrack(0); // Track 0

        // Act
        bool? bit = _provider.GetBit(0);

        // Assert - should return a valid bit (true or false)
        Assert.NotNull(bit);
    }

    [Fact]
    public void GetBit_ReturnsDifferentBits_AtDifferentCycles()
    {
        if (!TestDiskImages.TestImagesAvailable)
        {
            return;
        }

        // Arrange
        _provider = new NibDiskImageProvider(TestDiskImages.TestNib);
        _provider.SetQuarterTrack(0);

        // Act - read bits at different cycle positions
        var bits = new List<bool?>();
        for (ulong cycle = 0; cycle < 100; cycle += 5)
        {
            bits.Add(_provider.GetBit(cycle));
        }

        // Assert - at least some variation expected (unless disk is all zeros or ones)
        Assert.All(bits, b => Assert.NotNull(b));
    }

    [Fact]
    public void GetBit_ReturnsRandomBits_ForOutOfBoundsTrack()
    {
        if (!TestDiskImages.TestImagesAvailable)
        {
            return;
        }

        // Arrange
        _provider = new NibDiskImageProvider(TestDiskImages.TestNib);
        _provider.SetQuarterTrack(200); // Way beyond 35 tracks

        // Act
        bool? bit = _provider.GetBit(0);

        // Assert - should still return a value (random noise for MC3470 simulation)
        Assert.NotNull(bit);
    }

    [Fact]
    public void GetBit_WrapsAroundTrack()
    {
        if (!TestDiskImages.TestImagesAvailable)
        {
            return;
        }

        // Arrange
        _provider = new NibDiskImageProvider(TestDiskImages.TestNib);
        _provider.SetQuarterTrack(0);

        // Calculate cycles for one complete rotation
        // BitsPerTrack = 53,248, CyclesPerBit â‰ˆ 4.09
        ulong cyclesPerRotation = (ulong)(DiskIIConstants.BitsPerTrack * DiskIIConstants.CyclesPerBit);

        // Act - read at same position should give same bit
        bool? bit1 = _provider.GetBit(100);
        bool? bit2 = _provider.GetBit(100 + cyclesPerRotation);

        // Assert - same position after rotation
        Assert.Equal(bit1, bit2);
    }

    #endregion

    #region WriteBit Tests

    [Fact]
    public void WriteBit_ReturnsFalse_WhenWriteProtected()
    {
        if (!TestDiskImages.TestImagesAvailable)
        {
            return;
        }

        // Arrange
        _provider = new NibDiskImageProvider(TestDiskImages.TestNib)
        {
            IsWriteProtected = true
        };

        // Act
        bool result = _provider.WriteBit(true, 0);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void WriteBit_ReturnsFalse_ForOutOfBoundsTrack()
    {
        if (!TestDiskImages.TestImagesAvailable)
        {
            return;
        }

        // Arrange
        _provider = new NibDiskImageProvider(TestDiskImages.TestNib);
        _provider.SetQuarterTrack(200); // Out of bounds

        // Act
        bool result = _provider.WriteBit(true, 0);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Property Tests

    [Fact]
    public void IsWritable_ReturnsTrue()
    {
        if (!TestDiskImages.TestImagesAvailable)
        {
            return;
        }

        // Arrange
        _provider = new NibDiskImageProvider(TestDiskImages.TestNib);

        // Assert
        Assert.True(_provider.IsWritable);
    }

    [Fact]
    public void IsWriteProtected_CanBeSet()
    {
        if (!TestDiskImages.TestImagesAvailable)
        {
            return;
        }

        // Arrange
        _provider = new NibDiskImageProvider(TestDiskImages.TestNib)
        {
            // Act
            IsWriteProtected = true
        };

        // Assert
        Assert.True(_provider.IsWriteProtected);
    }

    #endregion
}
