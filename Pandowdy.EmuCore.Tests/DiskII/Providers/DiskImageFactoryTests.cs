using Pandowdy.EmuCore.DiskII.Providers;
using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.Tests.DiskII.Providers;

/// <summary>
/// Tests for DiskImageFactory - format detection and provider creation.
/// </summary>
[Collection("DiskImageProviders")]
public class DiskImageFactoryTests
{
    private readonly DiskImageFactory _factory = new();

    #region IsFormatSupported Tests

    [Theory]
    [InlineData(".nib", true)]
    [InlineData(".NIB", true)]
    [InlineData(".woz", true)]
    [InlineData(".WOZ", true)]
    [InlineData(".dsk", true)]
    [InlineData(".DSK", true)]
    [InlineData(".do", true)]
    [InlineData(".DO", true)]
    [InlineData(".po", true)]
    [InlineData(".PO", true)]
    [InlineData(".2mg", true)]
    [InlineData(".2img", true)]
    [InlineData(".txt", false)]
    [InlineData(".exe", false)]
    [InlineData("", false)]
    public void IsFormatSupported_ReturnsCorrectValue(string extension, bool expected)
    {
        // Arrange
        string testPath = $"test{extension}";

        // Act
        bool result = _factory.IsFormatSupported(testPath);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsFormatSupported_ReturnsFalse_ForNullPath()
    {
        // Act
        bool result = _factory.IsFormatSupported(null!);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsFormatSupported_ReturnsFalse_ForEmptyPath()
    {
        // Act
        bool result = _factory.IsFormatSupported("");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsFormatSupported_ReturnsFalse_ForWhitespacePath()
    {
        // Act
        bool result = _factory.IsFormatSupported("   ");

        // Assert
        Assert.False(result);
    }

    #endregion

    #region CreateProvider Error Cases

    [Fact]
    public void CreateProvider_ThrowsArgumentNullException_ForNullPath()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _factory.CreateProvider(null!));
    }

    [Fact]
    public void CreateProvider_ThrowsArgumentNullException_ForEmptyPath()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _factory.CreateProvider(""));
    }

    [Fact]
    public void CreateProvider_ThrowsFileNotFoundException_ForMissingFile()
    {
        // Act & Assert
        var ex = Assert.Throws<FileNotFoundException>(() =>
            _factory.CreateProvider("nonexistent.nib"));
        Assert.Contains("nonexistent.nib", ex.Message);
    }

    [Fact]
    public void CreateProvider_ThrowsNotSupportedException_ForUnsupportedFormat()
    {
        // Arrange - create a temp file with unsupported extension
        string tempFile = Path.GetTempFileName();
        string unsupportedFile = Path.ChangeExtension(tempFile, ".xyz");
        try
        {
            File.Move(tempFile, unsupportedFile);

            // Act & Assert
            var ex = Assert.Throws<NotSupportedException>(() =>
                _factory.CreateProvider(unsupportedFile));
            Assert.Contains(".xyz", ex.Message);
            Assert.Contains("Supported formats", ex.Message);
        }
        finally
        {
            if (File.Exists(unsupportedFile))
            {
                File.Delete(unsupportedFile);
            }
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    #endregion

    #region CreateProvider Success Cases

    [Fact]
    public void CreateProvider_ReturnsNibProvider_ForNibFile()
    {
        // Arrange
        if (!TestDiskImages.TestImagesAvailable)
        {
            Assert.True(true, "Test images not available - test skipped");
            return;
        }

        // Act
        using IDiskImageProvider provider = _factory.CreateProvider(TestDiskImages.TestNib);

        // Assert
        Assert.IsType<NibDiskImageProvider>(provider);
        Assert.Equal(TestDiskImages.TestNib, provider.FilePath);
    }

    [Fact]
    public void CreateProvider_ReturnsInternalWozProvider_ForWozFile()
    {
        // Arrange
        if (!TestDiskImages.TestImagesAvailable)
        {
            Assert.True(true, "Test images not available - test skipped");
            return;
        }

        // Act
        using IDiskImageProvider provider = _factory.CreateProvider(TestDiskImages.TestWoz);

        // Assert
        Assert.IsType<InternalWozDiskImageProvider>(provider);
        Assert.Equal(TestDiskImages.TestWoz, provider.FilePath);
    }

    [Fact]
    public void CreateProvider_ReturnsSectorProvider_ForDskFile()
    {
        // Arrange
        if (!File.Exists(TestDiskImages.DosDsk))
        {
            Assert.True(true, "dos.dsk not available - test skipped");
            return;
        }

        // Act
        using IDiskImageProvider provider = _factory.CreateProvider(TestDiskImages.DosDsk);

        // Assert
        Assert.IsType<SectorDiskImageProvider>(provider);
        Assert.Equal(TestDiskImages.DosDsk, provider.FilePath);
    }

    #endregion
}
