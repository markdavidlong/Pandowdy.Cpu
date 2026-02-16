// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.DiskII;
using Pandowdy.EmuCore.DiskII.Importers;
using Xunit;

namespace Pandowdy.EmuCore.Tests.DiskII.Importers;

/// <summary>
/// Unit tests for the NibImporter class.
/// </summary>
public class NibImporterTests
{
    #region Constructor and Properties Tests

    [Fact]
    public void SupportedExtensions_ContainsNib()
    {
        // Arrange
        var importer = new NibImporter();

        // Act & Assert
        Assert.Contains(".nib", importer.SupportedExtensions);
        Assert.Single(importer.SupportedExtensions);
    }

    #endregion

    #region Import Tests

    [Fact]
    public void Import_NullFilePath_ThrowsArgumentNullException()
    {
        // Arrange
        var importer = new NibImporter();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => importer.Import(null!));
        Assert.Throws<ArgumentNullException>(() => importer.Import(""));
        Assert.Throws<ArgumentNullException>(() => importer.Import("   "));
    }

    [Fact]
    public void Import_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var importer = new NibImporter();

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => importer.Import("nonexistent.nib"));
    }

    /*
    [Fact]
    [Trait("Category", "FullDiskTests")]
    public void Import_ValidNibFile_ReturnsInternalDiskImage()
    {
        // Arrange
        var importer = new NibImporter();

        // Skip test if test file doesn't exist

        using var sourceCopy = TempDiskImageCopy.TryCreate(TestDiskImages.TestNib);
        if (sourceCopy == null)
        {
            return;
        }

        // Act
        var diskImage = importer.Import(sourceCopy.FilePath);

        // Assert
        Assert.NotNull(diskImage);
        Assert.Equal(35, diskImage.TrackCount);
        Assert.Equal(DiskFormat.Nib, diskImage.OriginalFormat);
        Assert.Equal(sourceCopy.FilePath, diskImage.SourceFilePath);
        Assert.Equal(32, diskImage.OptimalBitTiming); // Standard timing
        Assert.False(diskImage.IsWriteProtected);
        Assert.False(diskImage.IsDirty);
    }

    [Fact]
    [Trait("Category", "FullDiskTests")]
    public void Import_ValidNibFile_CreatesCorrectTrackBitCounts()
    {
        // Arrange
        var importer = new NibImporter();

        // Skip test if test file doesn't exist
        using var sourceCopy = TempDiskImageCopy.TryCreate(TestDiskImages.TestNib);
        if (sourceCopy == null)
        {
            return;
        }

        // Act
        var diskImage = importer.Import(sourceCopy.FilePath);

        // Assert - All tracks should have standard bit count (6656 * 8 = 53248)
        Assert.All(diskImage.TrackBitCounts, bitCount => Assert.Equal(53248, bitCount));
    }

    [Fact]
    [Trait("Category", "FullDiskTests")]
    public void Import_ValidNibFile_TracksAreReadable()
    {
        // Arrange
        var importer = new NibImporter();

        // Skip test if test file doesn't exist
        using var sourceCopy = TempDiskImageCopy.TryCreate(TestDiskImages.TestNib);
        if (sourceCopy == null)
        {
            return;
        }

        // Act
        var diskImage = importer.Import(sourceCopy.FilePath);

        // Assert - Should be able to read bits from tracks
        for (int track = 0; track < diskImage.TrackCount; track++)
        {
            var trackBuffer = diskImage.Tracks[track];
            Assert.NotNull(trackBuffer);

            // Read a few bits to verify track is accessible
            trackBuffer.BitPosition = 0;
            for (int i = 0; i < 100; i++)
            {
                byte bit = trackBuffer.ReadNextBit();
                Assert.True(bit == 0 || bit == 1);
            }
        }
    }
    */

    [Fact]
    public void Import_InvalidFileSize_ThrowsInvalidDataException()
    {
        // Arrange
        var importer = new NibImporter();
        var tempFile = Path.GetTempFileName();

        try
        {
            // Create file with wrong size
            File.WriteAllBytes(tempFile, new byte[1000]);

            // Act & Assert
            Assert.Throws<InvalidDataException>(() => importer.Import(tempFile));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    #endregion

    #region Stream Import Tests

    
    [Fact]
    public void ImportStream_NullStream_ThrowsArgumentNullException()
    {
        // Arrange
        var importer = new NibImporter();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => importer.Import((Stream)null!, DiskFormat.Nib));
    }

    [Fact]
    public void ImportStream_WrongFormat_ThrowsArgumentException()
    {
        // Arrange
        var importer = new NibImporter();
        using var stream = new MemoryStream();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => importer.Import(stream, DiskFormat.Woz));
        Assert.Throws<ArgumentException>(() => importer.Import(stream, DiskFormat.Dsk));
    }

    [Fact]
    public void ImportStream_ValidNibStream_ReturnsInternalDiskImage()
    {
        // Arrange
        var importer = new NibImporter();

        // Skip test if test file doesn't exist
        if (!File.Exists(TestDiskImages.TestNib))
        {
            return;
        }

        // Act
        using var stream = File.OpenRead(TestDiskImages.TestNib);
        var diskImage = importer.Import(stream, DiskFormat.Nib);

        // Assert
        Assert.NotNull(diskImage);
        Assert.Equal(35, diskImage.PhysicalTrackCount);
        Assert.Equal(DiskFormat.Nib, diskImage.OriginalFormat);
        Assert.Null(diskImage.SourceFilePath); // Stream import has no file path
        Assert.Equal(32, diskImage.OptimalBitTiming);
        Assert.False(diskImage.IsWriteProtected);
    }

    [Fact]
    public void ImportStream_InvalidSize_ThrowsInvalidDataException()
    {
        // Arrange
        var importer = new NibImporter();
        using var stream = new MemoryStream(new byte[1000]); // Wrong size

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => importer.Import(stream, DiskFormat.Nib));
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void Import_MultipleFiles_CreatesIndependentImages()
    {
        // Arrange
        var importer = new NibImporter();

        // Skip test if test files don't exist
        if (!File.Exists(TestDiskImages.TestNib) || !File.Exists(TestDiskImages.BlankNib))
        {
            return;
        }

        // Act
        var diskImage1 = importer.Import(TestDiskImages.TestNib);
        var diskImage2 = importer.Import(TestDiskImages.BlankNib);

        // Assert - Images should be independent
        Assert.NotSame(diskImage1, diskImage2);
        Assert.NotSame(diskImage1.QuarterTracks, diskImage2.QuarterTracks);
        Assert.NotEqual(diskImage1.SourceFilePath, diskImage2.SourceFilePath);
    }

    [Fact]
    public void Import_SameFileTwice_CreatesIndependentImages()
    {
        // Arrange
        var importer = new NibImporter();

        // Skip test if test file doesn't exist
        if (!File.Exists(TestDiskImages.TestNib))
        {
            return;
        }

        // Act
        var diskImage1 = importer.Import(TestDiskImages.TestNib);
        var diskImage2 = importer.Import(TestDiskImages.TestNib);

        // Assert - Images should be independent (separate memory)
        Assert.NotSame(diskImage1, diskImage2);
        Assert.NotSame(diskImage1.QuarterTracks, diskImage2.QuarterTracks);
        Assert.Equal(diskImage1.SourceFilePath, diskImage2.SourceFilePath);
    }

    #endregion
}
