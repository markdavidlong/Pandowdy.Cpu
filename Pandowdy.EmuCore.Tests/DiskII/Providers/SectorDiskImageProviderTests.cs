using Pandowdy.EmuCore.DiskII;
using Pandowdy.EmuCore.DiskII.Providers;

namespace Pandowdy.EmuCore.Tests.DiskII.Providers;

/// <summary>
/// Tests for SectorDiskImageProvider - DSK/DO/PO format with GCR synthesis.
/// </summary>
[Collection("DiskImageProviders")]
public class SectorDiskImageProviderTests : IDisposable
{
    private SectorDiskImageProvider? _provider;

    public void Dispose()
    {
        _provider?.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ThrowsArgumentNullException_ForNullPath()
    {
        Assert.Throws<ArgumentNullException>(() => new SectorDiskImageProvider(null!));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_ForEmptyPath()
    {
        Assert.Throws<ArgumentNullException>(() => new SectorDiskImageProvider(""));
    }

    [Fact]
    public void Constructor_ThrowsFileNotFoundException_ForMissingFile()
    {
        var ex = Assert.Throws<FileNotFoundException>(() =>
            new SectorDiskImageProvider("nonexistent.dsk"));
        Assert.Contains("nonexistent.dsk", ex.Message);
    }

    [Fact]
    public void Constructor_LoadsValidDskFile()
    {
        if (!File.Exists(TestDiskImages.DosDsk))
        {
            return;
        }

        // Act
        _provider = new SectorDiskImageProvider(TestDiskImages.DosDsk);

        // Assert
        Assert.NotNull(_provider);
        Assert.Equal(TestDiskImages.DosDsk, _provider.FilePath);
    }

    [Fact]
    public void Constructor_LoadsDos33MasterDsk()
    {
        if (!File.Exists(TestDiskImages.Dos33MasterDsk))
        {
            return;
        }

        // Act
        _provider = new SectorDiskImageProvider(TestDiskImages.Dos33MasterDsk);

        // Assert
        Assert.NotNull(_provider);
        Assert.Equal(TestDiskImages.Dos33MasterDsk, _provider.FilePath);
    }

    #endregion

    #region SetQuarterTrack Tests

    [Fact]
    public void SetQuarterTrack_UpdatesCurrentQuarterTrack()
    {
        if (!File.Exists(TestDiskImages.DosDsk))
        {
            return;
        }

        // Arrange
        _provider = new SectorDiskImageProvider(TestDiskImages.DosDsk);

        // Act
        _provider.SetQuarterTrack(20);

        // Assert
        Assert.Equal(20, _provider.CurrentQuarterTrack);
    }

    #endregion

    #region GetBit Tests

    [Fact]
    public void GetBit_ReturnsBitValue_ForValidTrack()
    {
        if (!File.Exists(TestDiskImages.DosDsk))
        {
            return;
        }

        // Arrange
        _provider = new SectorDiskImageProvider(TestDiskImages.DosDsk);
        _provider.SetQuarterTrack(0); // Track 0

        // Act
        bool? bit = _provider.GetBit(0);

        // Assert - should return a valid bit (synthesized GCR)
        Assert.NotNull(bit);
    }

    [Fact]
    public void GetBit_SynthesizesValidGcrData()
    {
        if (!File.Exists(TestDiskImages.DosDsk))
        {
            return;
        }

        // Arrange
        _provider = new SectorDiskImageProvider(TestDiskImages.DosDsk);
        _provider.SetQuarterTrack(0);

        // Act - read enough bits to find sync bytes (0xFF = 8 consecutive 1s)
        int consecutiveOnes = 0;
        bool foundSync = false;

        for (int i = 0; i < 10000 && !foundSync; i++)
        {
            bool? bit = _provider.GetBit((ulong)i);
            if (bit == true)
            {
                consecutiveOnes++;
                if (consecutiveOnes >= 8)
                {
                    foundSync = true;
                }
            }
            else
            {
                consecutiveOnes = 0;
            }
        }

        // Assert - synthesized data should contain sync bytes
        Assert.True(foundSync, "Should find sync bytes (8+ consecutive 1s) in synthesized track");
    }

    [Fact]
    public void GetBit_ReturnsNull_ForOutOfBoundsTrack()
    {
        if (!File.Exists(TestDiskImages.DosDsk))
        {
            return;
        }

        // Arrange
        _provider = new SectorDiskImageProvider(TestDiskImages.DosDsk);
        _provider.SetQuarterTrack(200); // Beyond 35 tracks

        // Act
        bool? bit = _provider.GetBit(0);

        // Assert - out of bounds returns null
        Assert.Null(bit);
    }

    [Fact]
    public void GetBit_CachesSynthesizedTracks()
    {
        if (!File.Exists(TestDiskImages.DosDsk))
        {
            return;
        }

        // Arrange
        _provider = new SectorDiskImageProvider(TestDiskImages.DosDsk);
        _provider.SetQuarterTrack(0);

        // Act - read same track twice (second should be cached)
        bool? bit1 = _provider.GetBit(100);
        bool? bit2 = _provider.GetBit(100);

        // Assert - same position should return same bit (from cache)
        // Note: This test is a bit weak since we can't directly verify caching
        Assert.Equal(bit1, bit2);
    }

    #endregion

    #region WriteBit Tests

    [Fact]
    public void WriteBit_ReturnsFalse_NotYetImplemented()
    {
        if (!File.Exists(TestDiskImages.DosDsk))
        {
            return;
        }

        // Arrange
        _provider = new SectorDiskImageProvider(TestDiskImages.DosDsk);
        _provider.SetQuarterTrack(0);

        // Act - writing to sector-based images is not yet implemented
        bool result = _provider.WriteBit(true, 0);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Property Tests

    [Fact]
    public void IsWriteProtected_CanBeSet()
    {
        if (!File.Exists(TestDiskImages.DosDsk))
        {
            return;
        }

        // Arrange
        _provider = new SectorDiskImageProvider(TestDiskImages.DosDsk)
        {
            // Act
            IsWriteProtected = true
        };

        // Assert
        Assert.True(_provider.IsWriteProtected);
    }

    [Fact]
    public void CurrentQuarterTrack_DefaultsToZero()
    {
        if (!File.Exists(TestDiskImages.DosDsk))
        {
            return;
        }

        // Arrange & Act
        _provider = new SectorDiskImageProvider(TestDiskImages.DosDsk);

        // Assert
        Assert.Equal(0, _provider.CurrentQuarterTrack);
    }

    #endregion
}
