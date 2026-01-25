using Pandowdy.EmuCore.DiskII;
using Pandowdy.EmuCore.DiskII.Providers;

namespace Pandowdy.EmuCore.Tests.DiskII.Providers;

/// <summary>
/// Tests for InternalWozDiskImageProvider - native WOZ format parsing.
/// </summary>
[Collection("DiskImageProviders")]
public class InternalWozDiskImageProviderTests : IDisposable
{
    private InternalWozDiskImageProvider? _provider;

    public void Dispose()
    {
        _provider?.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ThrowsArgumentNullException_ForNullPath()
    {
        Assert.Throws<ArgumentNullException>(() => new InternalWozDiskImageProvider(null!));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_ForEmptyPath()
    {
        Assert.Throws<ArgumentNullException>(() => new InternalWozDiskImageProvider(""));
    }

    [Fact]
    public void Constructor_ThrowsFileNotFoundException_ForMissingFile()
    {
        var ex = Assert.Throws<FileNotFoundException>(() =>
            new InternalWozDiskImageProvider("nonexistent.woz"));
        Assert.Contains("nonexistent.woz", ex.Message);
    }

    [Fact]
    public void Constructor_ThrowsInvalidDataException_ForInvalidSignature()
    {
        // Arrange - create a temp file with invalid signature
        string tempFile = Path.Combine(Path.GetTempPath(), $"invalid_sig_{Guid.NewGuid()}.woz");
        try
        {
            File.WriteAllBytes(tempFile, new byte[1000]); // No WOZ signature

            // Act & Assert
            var ex = Assert.Throws<InvalidDataException>(() =>
                new InternalWozDiskImageProvider(tempFile));
            Assert.Contains("Invalid WOZ signature", ex.Message);
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
    public void Constructor_LoadsValidWozFile()
    {
        if (!TestDiskImages.TestImagesAvailable)
        {
            return;
        }

        // Act
        _provider = new InternalWozDiskImageProvider(TestDiskImages.TestWoz);

        // Assert
        Assert.NotNull(_provider);
        Assert.Equal(TestDiskImages.TestWoz, _provider.FilePath);
    }

    [Fact]
    public void Constructor_LoadsProdosWoz()
    {
        if (!File.Exists(TestDiskImages.ProdosWoz))
        {
            return;
        }

        // Act
        _provider = new InternalWozDiskImageProvider(TestDiskImages.ProdosWoz);

        // Assert
        Assert.NotNull(_provider);
        Assert.Equal(TestDiskImages.ProdosWoz, _provider.FilePath);
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
        _provider = new InternalWozDiskImageProvider(TestDiskImages.TestWoz);

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
        _provider = new InternalWozDiskImageProvider(TestDiskImages.TestWoz);

        // Act & Assert - various quarter track positions
        for (int qt = 0; qt < 140; qt += 10)
        {
            _provider.SetQuarterTrack(qt);
            Assert.Equal(qt, _provider.CurrentQuarterTrack);
        }
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
        _provider = new InternalWozDiskImageProvider(TestDiskImages.TestWoz);
        _provider.SetQuarterTrack(0); // Track 0

        // Act
        bool? bit = _provider.GetBit(0);

        // Assert - should return a valid bit (true or false)
        Assert.NotNull(bit);
    }

    [Fact]
    public void GetBit_ReturnsRandomBits_ForUnmappedTrack()
    {
        if (!TestDiskImages.TestImagesAvailable)
        {
            return;
        }

        // Arrange
        _provider = new InternalWozDiskImageProvider(TestDiskImages.TestWoz);
        _provider.SetQuarterTrack(159); // Last possible quarter track (may be unmapped)

        // Act
        bool? bit = _provider.GetBit(0);

        // Assert - should return a value (either mapped data or random noise)
        Assert.NotNull(bit);
    }

    [Fact]
    public void GetBit_ReturnsConsistentBits_AtSamePosition()
    {
        if (!TestDiskImages.TestImagesAvailable)
        {
            return;
        }

        // Arrange
        _provider = new InternalWozDiskImageProvider(TestDiskImages.TestWoz);
        _provider.SetQuarterTrack(0);

        // Act - read same position twice
        bool? bit1 = _provider.GetBit(1000);
        bool? bit2 = _provider.GetBit(1000);

        // Assert - same cycle should return same bit
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
        _provider = new InternalWozDiskImageProvider(TestDiskImages.TestWoz)
        {
            IsWriteProtected = true
        };

        // Act
        bool result = _provider.WriteBit(true, 0);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Property Tests

    [Fact]
    public void IsWriteProtected_CanBeSet()
    {
        if (!TestDiskImages.TestImagesAvailable)
        {
            return;
        }

        // Arrange
        _provider = new InternalWozDiskImageProvider(TestDiskImages.TestWoz)
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
        if (!TestDiskImages.TestImagesAvailable)
        {
            return;
        }

        // Arrange & Act
        _provider = new InternalWozDiskImageProvider(TestDiskImages.TestWoz);

        // Assert
        Assert.Equal(0, _provider.CurrentQuarterTrack);
    }

    #endregion
}
