// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.DiskII;
using Pandowdy.EmuCore.DiskII.Importers;
using Xunit;

namespace Pandowdy.EmuCore.Tests.DiskII.Importers;

/// <summary>
/// Unit tests for the SectorImporter class.
/// </summary>
public class SectorImporterTests
{
    [Fact]
    public void SupportedExtensions_ContainsSectorFormats()
    {
        var importer = new SectorImporter();
        Assert.Contains(".dsk", importer.SupportedExtensions);
        Assert.Contains(".do", importer.SupportedExtensions);
        Assert.Contains(".po", importer.SupportedExtensions);
        Assert.Contains(".2mg", importer.SupportedExtensions);
    }

    [Fact]
    public void Import_NullFilePath_ThrowsArgumentNullException()
    {
        var importer = new SectorImporter();
        Assert.Throws<ArgumentNullException>(() => importer.Import(null!));
    }

    [Fact]
    public void Import_NonExistentFile_ThrowsFileNotFoundException()
    {
        var importer = new SectorImporter();
        Assert.Throws<FileNotFoundException>(() => importer.Import("nonexistent.dsk"));
    }

    /*

    [Fact]
    [Trait("Category", "FullDiskTests")]
    public void Import_ValidDskFile_ReturnsInternalDiskImage()
    {
        var importer = new SectorImporter();
        using var sourceCopy = TempDiskImageCopy.TryCreate(TestDiskImages.DosDsk);
        if (sourceCopy == null)

        {
            return;
        }


        var diskImage = importer.Import(sourceCopy.FilePath);

        Assert.NotNull(diskImage);
        Assert.Equal(35, diskImage.TrackCount);
        Assert.Equal(DiskFormat.Dsk, diskImage.OriginalFormat);
        Assert.Equal(sourceCopy.FilePath, diskImage.SourceFilePath);
        Assert.Equal(32, diskImage.OptimalBitTiming); // Synthesized tracks use standard timing
        Assert.False(diskImage.IsDirty);
    }

    [Fact]

    [Trait("Category", "FullDiskTests")]
    public void Import_ValidDskFile_SynthesizesGcrTracks()
    {
        var importer = new SectorImporter();
        using var sourceCopy = TempDiskImageCopy.TryCreate(TestDiskImages.DosDsk);
        if (sourceCopy == null)
        {
            return;
        }

        var diskImage = importer.Import(sourceCopy.FilePath);

        // Verify tracks are synthesized and readable
        for (int track = 0; track < diskImage.TrackCount; track++)
        {
            var trackBuffer = diskImage.Tracks[track];
            Assert.NotNull(trackBuffer);
            Assert.True(diskImage.TrackBitCounts[track] > 0);

            // Read some bits to verify track contains GCR data
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
    public void ImportStream_NullStream_ThrowsArgumentNullException()
    {
        var importer = new SectorImporter();
        Assert.Throws<ArgumentNullException>(() => importer.Import((Stream)null!, DiskFormat.Dsk));
    }

    [Fact]
    public void ImportStream_WrongFormat_ThrowsArgumentException()
    {
        var importer = new SectorImporter();
        using var stream = new MemoryStream();
        Assert.Throws<ArgumentException>(() => importer.Import(stream, DiskFormat.Woz));
        Assert.Throws<ArgumentException>(() => importer.Import(stream, DiskFormat.Nib));
    }

    /*
    [Fact]
    [Trait("Category", "FullDiskTests")]
    public void ImportStream_ValidDskStream_ReturnsInternalDiskImage()
    {
        var importer = new SectorImporter();
        using var sourceCopy = TempDiskImageCopy.TryCreate(TestDiskImages.DosDsk);
        if (sourceCopy == null)
        {
            return;
        }


        using var stream = File.OpenRead(sourceCopy.FilePath);
        var diskImage = importer.Import(stream, DiskFormat.Dsk);

        Assert.NotNull(diskImage);
        Assert.Equal(35, diskImage.TrackCount);
        Assert.Equal(DiskFormat.Dsk, diskImage.OriginalFormat);
        Assert.Null(diskImage.SourceFilePath); // Stream import
    }


    [Fact]
    [Trait("Category", "FullDiskTests")]
    public void Import_DifferentFormats_PreservesOriginalFormat()
    {
        var importer = new SectorImporter();

        // Test with DSK format
        using var sourceCopy = TempDiskImageCopy.TryCreate(TestDiskImages.DosDsk);
        if (sourceCopy != null)
        {
            var dskImage = importer.Import(sourceCopy.FilePath);
            Assert.Equal(DiskFormat.Dsk, dskImage.OriginalFormat);
        }

        // Format distinction preserved even though internal representation is the same
    }

    [Fact]
    [Trait("Category", "FullDiskTests")]
    public void Import_SynthesizesTracks_WithProperGcrStructure()
    {
        var importer = new SectorImporter();
        using var sourceCopy = TempDiskImageCopy.TryCreate(TestDiskImages.DosDsk);
        if (sourceCopy == null)
        {
            return;
        }

        var diskImage = importer.Import(sourceCopy.FilePath);

        // Verify track 0 has reasonable GCR structure
        var track0 = diskImage.Tracks[0];
        track0.BitPosition = 0;

        // Look for sync bytes (0xFF) at start - synthesized tracks begin with sync
        // Read first byte using ReadNextBit (8 bits)
        byte firstByte = 0;
        for (int bit = 0; bit < 8; bit++)
        {
            firstByte = (byte)((firstByte << 1) | track0.ReadNextBit());
        }
        // Should be 0xFF (sync byte) at the start of synthesized track
        Assert.True(firstByte == 0xFF || firstByte == 0xFE, $"Expected sync byte at track start, got 0x{firstByte:X2}");
    }
    */
}
