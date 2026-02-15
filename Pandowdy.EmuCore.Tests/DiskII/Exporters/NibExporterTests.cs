// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.DiskII;
using Pandowdy.EmuCore.DiskII.Exporters;
using Pandowdy.EmuCore.DiskII.Importers;
using Xunit;

namespace Pandowdy.EmuCore.Tests.DiskII.Exporters;

/// <summary>
/// Unit tests for the NibExporter class.
/// </summary>
public class NibExporterTests
{
    [Fact]
    public void OutputFormat_ReturnsNib()
    {
        var exporter = new NibExporter();
        Assert.Equal(DiskFormat.Nib, exporter.OutputFormat);
    }

    [Fact]
    public void Export_NullDisk_ThrowsArgumentNullException()
    {
        var exporter = new NibExporter();
        Assert.Throws<ArgumentNullException>(() => exporter.Export(null!, "test.nib"));
    }

    [Fact]
    public void Export_NullFilePath_ThrowsArgumentNullException()
    {
        var exporter = new NibExporter();
        var disk = new InternalDiskImage();
        Assert.Throws<ArgumentNullException>(() => exporter.Export(disk, (string)null!));
        Assert.Throws<ArgumentNullException>(() => exporter.Export(disk, ""));
    }

    [Fact]
    public void ExportStream_NullDisk_ThrowsArgumentNullException()
    {
        var exporter = new NibExporter();
        using var stream = new MemoryStream();
        Assert.Throws<ArgumentNullException>(() => exporter.Export(null!, stream));
    }

    [Fact]
    public void ExportStream_NullStream_ThrowsArgumentNullException()
    {
        var exporter = new NibExporter();
        var disk = new InternalDiskImage();
        Assert.Throws<ArgumentNullException>(() => exporter.Export(disk, (Stream)null!));
    }


    /*
    [Fact]
    [Trait("Category", "FullDiskTests")]
    public void Export_CreatesCorrectFileSize()
    {
        using var sourceCopy = TempDiskImageCopy.TryCreate(TestDiskImages.TestNib);
        if (sourceCopy == null)
        {
            return;
        }

        // Import from temp copy
        var importer = new NibImporter();
        var disk = importer.Import(sourceCopy.FilePath);

        // Export to temp output file - automatically cleaned up on dispose
        var exporter = new NibExporter();
        using var tempFile = new TempOutputFile(".nib");

        exporter.Export(disk, tempFile.FilePath);

        // Verify file was created with correct size
        Assert.True(File.Exists(tempFile.FilePath));
        var fileInfo = new FileInfo(tempFile.FilePath);
        Assert.Equal(232960, fileInfo.Length); // 35 tracks * 6656 bytes
    }

    [Fact]
    [Trait("Category", "FullDiskTests")]
    public void ExportStream_WritesCorrectSize()
    {
        using var sourceCopy = TempDiskImageCopy.TryCreate(TestDiskImages.TestNib);
        if (sourceCopy == null)
        {
            return;
        }

        // Import from temp copy
        var importer = new NibImporter();
        var disk = importer.Import(sourceCopy.FilePath);

        // Export to stream
        var exporter = new NibExporter();
        using var stream = new MemoryStream();
        exporter.Export(disk, stream);

        // Verify stream has correct size
        Assert.Equal(232960, stream.Length); // 35 tracks * 6656 bytes
    }

    [Fact]
    [Trait("Category", "FullDiskTests")]
    public void Export_RoundTrip_PreservesData()
    {
        using var sourceCopy = TempDiskImageCopy.TryCreate(TestDiskImages.TestNib);
        if (sourceCopy == null)
        {
            return;
        }

        // Import from temp copy
        var importer = new NibImporter();
        var originalDisk = importer.Import(sourceCopy.FilePath);

        // Export to temp output file - automatically cleaned up on dispose
        var exporter = new NibExporter();
        using var tempFile = new TempOutputFile(".nib");

        exporter.Export(originalDisk, tempFile.FilePath);

        // Compare exported file with source copy byte-by-byte
        byte[] originalBytes = File.ReadAllBytes(sourceCopy.FilePath);
        byte[] exportedBytes = File.ReadAllBytes(tempFile.FilePath);

        Assert.Equal(originalBytes.Length, exportedBytes.Length);

        // NIB files should be byte-for-byte identical on round-trip
        for (int i = 0; i < originalBytes.Length; i++)
        {
            Assert.Equal(originalBytes[i], exportedBytes[i]);
        }
    }

    [Fact]
    [Trait("Category", "FullDiskTests")]
    public void Export_RoundTripReimport_ProducesIdenticalTracks()
    {
        using var sourceCopy = TempDiskImageCopy.TryCreate(TestDiskImages.TestNib);
        if (sourceCopy == null)
        {
            return;
        }

        // Import from temp copy
        var importer = new NibImporter();
        var originalDisk = importer.Import(sourceCopy.FilePath);

        // Export to temp output file - automatically cleaned up on dispose
        var exporter = new NibExporter();
        using var tempFile = new TempOutputFile(".nib");

        exporter.Export(originalDisk, tempFile.FilePath);

        // Re-import the exported file
        var reimportedDisk = importer.Import(tempFile.FilePath);

        // Compare tracks
        Assert.Equal(originalDisk.TrackCount, reimportedDisk.TrackCount);

        for (int track = 0; track < originalDisk.TrackCount; track++)
        {
            var originalBuffer = originalDisk.Tracks[track];
            var reimportedBuffer = reimportedDisk.Tracks[track];

            originalBuffer.BitPosition = 0;
            reimportedBuffer.BitPosition = 0;

            int bitCount = originalDisk.TrackBitCounts[track];

            // Compare all bits in track
            for (int bit = 0; bit < bitCount; bit++)
            {
                byte originalBit = originalBuffer.ReadNextBit();
                byte reimportedBit = reimportedBuffer.ReadNextBit();
                Assert.Equal(originalBit, reimportedBit);
            }
        }
    }
    */

    [Fact]
    public void Export_EmptyDisk_CreatesValidNibFile()
    {
        // Create an empty disk
        var disk = new InternalDiskImage(trackCount: 35, standardTrackBitCount: 51200);

        // Export to temp output file - automatically cleaned up on dispose
        var exporter = new NibExporter();
        using var tempFile = new TempOutputFile(".nib");

        exporter.Export(disk, tempFile.FilePath);

        // Verify file was created with correct size
        Assert.True(File.Exists(tempFile.FilePath));
        var fileInfo = new FileInfo(tempFile.FilePath);
        Assert.Equal(232960, fileInfo.Length);

        // Verify file is all zeros (empty tracks)
        byte[] data = File.ReadAllBytes(tempFile.FilePath);
        Assert.All(data, b => Assert.Equal(0, b));
    }

    /*
    [Fact]
    [Trait("Category", "FullDiskTests")]
    public void Export_FormatConversion_DskToNib_Works()
    {
        using var sourceCopy = TempDiskImageCopy.TryCreate(TestDiskImages.DosDsk);
        if (sourceCopy == null)
        {
            return;
        }

        // Import from temp copy
        var sectorImporter = new SectorImporter();
        var disk = sectorImporter.Import(sourceCopy.FilePath);

        // Export as NIB - automatically cleaned up on dispose
        var nibExporter = new NibExporter();
        using var tempNibFile = new TempOutputFile(".nib");

        nibExporter.Export(disk, tempNibFile.FilePath);

        // Verify NIB file was created
        Assert.True(File.Exists(tempNibFile.FilePath));
        var fileInfo = new FileInfo(tempNibFile.FilePath);
        Assert.Equal(232960, fileInfo.Length);

        // Re-import as NIB and verify tracks are readable
        var nibImporter = new NibImporter();
        var reimportedDisk = nibImporter.Import(tempNibFile.FilePath);
        Assert.Equal(35, reimportedDisk.TrackCount);
    }

    [Fact]
    [Trait("Category", "FullDiskTests")]
    public void Export_FormatConversion_NibToDsk_Works()
    {
        using var sourceCopy = TempDiskImageCopy.TryCreate(TestDiskImages.TestNib);
        if (sourceCopy == null)
        {
            return;
        }

        // Import from temp copy
        var nibImporter = new NibImporter();
        var disk = nibImporter.Import(sourceCopy.FilePath);

        // Export as DSK - automatically cleaned up on dispose
        var sectorExporter = new SectorExporter(DiskFormat.Dsk);
        using var tempDskFile = new TempOutputFile(".dsk");

        sectorExporter.Export(disk, tempDskFile.FilePath);

        // Verify DSK file was created
        Assert.True(File.Exists(tempDskFile.FilePath));
        var fileInfo = new FileInfo(tempDskFile.FilePath);
        Assert.Equal(143360, fileInfo.Length);
    }
    */
}
