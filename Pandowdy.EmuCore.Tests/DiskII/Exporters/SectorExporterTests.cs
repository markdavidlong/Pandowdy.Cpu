// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.DiskII;
using Pandowdy.EmuCore.DiskII.Exporters;
using Pandowdy.EmuCore.DiskII.Importers;
using Xunit;

namespace Pandowdy.EmuCore.Tests.DiskII.Exporters;

/// <summary>
/// Unit tests for the SectorExporter class.
/// </summary>
public class SectorExporterTests
{
    [Fact]
    public void Constructor_ValidFormat_CreatesExporter()
    {
        var dskExporter = new SectorExporter(DiskFormat.Dsk);
        Assert.Equal(DiskFormat.Dsk, dskExporter.OutputFormat);

        var doExporter = new SectorExporter(DiskFormat.Do);
        Assert.Equal(DiskFormat.Do, doExporter.OutputFormat);

        var poExporter = new SectorExporter(DiskFormat.Po);
        Assert.Equal(DiskFormat.Po, poExporter.OutputFormat);
    }

    [Fact]
    public void Constructor_InvalidFormat_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new SectorExporter(DiskFormat.Woz));
        Assert.Throws<ArgumentException>(() => new SectorExporter(DiskFormat.Nib));
        Assert.Throws<ArgumentException>(() => new SectorExporter(DiskFormat.Unknown));
    }

    [Fact]
    public void Export_NullDisk_ThrowsArgumentNullException()
    {
        var exporter = new SectorExporter(DiskFormat.Dsk);
        Assert.Throws<ArgumentNullException>(() => exporter.Export(null!, "test.dsk"));
    }

    [Fact]
    public void Export_NullFilePath_ThrowsArgumentNullException()
    {
        var exporter = new SectorExporter(DiskFormat.Dsk);
        var disk = new InternalDiskImage();
        Assert.Throws<ArgumentNullException>(() => exporter.Export(disk, (string)null!));
        Assert.Throws<ArgumentNullException>(() => exporter.Export(disk, ""));
    }

    [Fact]
    public void ExportStream_NullDisk_ThrowsArgumentNullException()
    {
        var exporter = new SectorExporter(DiskFormat.Dsk);
        using var stream = new MemoryStream();
        Assert.Throws<ArgumentNullException>(() => exporter.Export(null!, stream));
    }

    [Fact]
    public void ExportStream_NullStream_ThrowsArgumentNullException()
    {
        var exporter = new SectorExporter(DiskFormat.Dsk);
        var disk = new InternalDiskImage();
        Assert.Throws<ArgumentNullException>(() => exporter.Export(disk, (Stream)null!));
    }
    /*
    [Fact]
    [Trait("Category", "FullDiskTests")]
    public void Export_DskFormat_CreatesCorrectFileSize()
    {
        // Create temp copy of source disk to avoid file locking issues with parallel tests
        using var sourceCopy = TempDiskImageCopy.TryCreate(TestDiskImages.DosDsk);
        if (sourceCopy == null)
        {
            return; // Skip if test image not available
        }

        // Import from temp copy
        var importer = new SectorImporter();
        var disk = importer.Import(sourceCopy.FilePath);

        // Export to temp output file - automatically cleaned up on dispose
        var exporter = new SectorExporter(DiskFormat.Dsk);
        using var tempFile = new TempOutputFile(".dsk");

        exporter.Export(disk, tempFile.FilePath);

        // Verify file was created with correct size
        Assert.True(File.Exists(tempFile.FilePath));
        var fileInfo = new FileInfo(tempFile.FilePath);
        Assert.Equal(143360, fileInfo.Length); // 35 tracks * 16 sectors * 256 bytes
    }

    [Fact]
    [Trait("Category", "FullDiskTests")]
    public void ExportStream_DskFormat_WritesCorrectSize()
    {
        using var sourceCopy = TempDiskImageCopy.TryCreate(TestDiskImages.DosDsk);
        if (sourceCopy == null)
        {
            return;
        }

        // Import from temp copy
        var importer = new SectorImporter();
        var disk = importer.Import(sourceCopy.FilePath);

        // Export to stream
        var exporter = new SectorExporter(DiskFormat.Dsk);
        using var stream = new MemoryStream();
        exporter.Export(disk, stream);

        // Verify stream has correct size
        Assert.Equal(143360, stream.Length); // 35 tracks * 16 sectors * 256 bytes
    }

    [Fact]
    [Trait("Category", "FullDiskTests")]
    public void Export_RoundTrip_PreservesData()
    {
        using var sourceCopy = TempDiskImageCopy.TryCreate(TestDiskImages.DosDsk);
        if (sourceCopy == null)
        {
            return;
        }

        // Import from temp copy
        var importer = new SectorImporter();
        var originalDisk = importer.Import(sourceCopy.FilePath);

        // Export to temp output file - automatically cleaned up on dispose
        var exporter = new SectorExporter(DiskFormat.Dsk);
        using var tempFile = new TempOutputFile(".dsk");

        exporter.Export(originalDisk, tempFile.FilePath);

        // Re-import the exported file
        var reimportedDisk = importer.Import(tempFile.FilePath);

        // Verify track counts match
        Assert.Equal(originalDisk.TrackCount, reimportedDisk.TrackCount);

        // NOTE: We cannot compare GCR bit streams directly because sector export/import
        // is lossy - it decodes sectors and re-synthesizes tracks with different gaps
        // and sync patterns. Instead, we verify the exported file size is correct.
        var fileInfo = new FileInfo(tempFile.FilePath);
        Assert.Equal(143360, fileInfo.Length); // 35 tracks * 16 sectors * 256 bytes

        // Verify the disk is readable and has valid track structure
        for (int track = 0; track < reimportedDisk.TrackCount; track++)
        {
            var buffer = reimportedDisk.Tracks[track];
            Assert.NotNull(buffer);
            Assert.True(reimportedDisk.TrackBitCounts[track] > 0);
        }
    }

    [Fact]
    [Trait("Category", "FullDiskTests")]
    public void Export_PoFormat_CreatesProDOSOrder()
    {
        using var sourceCopy = TempDiskImageCopy.TryCreate(TestDiskImages.DosDsk);
        if (sourceCopy == null)
        {
            return;
        }

        // Import from temp copy (DOS order)
        var importer = new SectorImporter();
        var disk = importer.Import(sourceCopy.FilePath);

        // Export as ProDOS order - automatically cleaned up on dispose
        var exporter = new SectorExporter(DiskFormat.Po);
        using var tempFile = new TempOutputFile(".po");

        exporter.Export(disk, tempFile.FilePath);

        // Verify file was created
        Assert.True(File.Exists(tempFile.FilePath));
        var fileInfo = new FileInfo(tempFile.FilePath);
        Assert.Equal(143360, fileInfo.Length);

        // The actual sector order verification would require comparing
        // with a known ProDOS-order file, which we don't have in tests
    }

    [Fact]
    [Trait("Category", "FullDiskTests")]
    public void Export_DoFormat_CreatesDOSOrder()
    {
        using var sourceCopy = TempDiskImageCopy.TryCreate(TestDiskImages.DosDsk);
        if (sourceCopy == null)
        {
            return;
        }

        // Import from temp copy
        var importer = new SectorImporter();
        var disk = importer.Import(sourceCopy.FilePath);

        // Export as .DO (DOS order explicit) - automatically cleaned up on dispose
        var exporter = new SectorExporter(DiskFormat.Do);
        using var tempFile = new TempOutputFile(".do");

        exporter.Export(disk, tempFile.FilePath);

        // Verify file was created
        Assert.True(File.Exists(tempFile.FilePath));
        var fileInfo = new FileInfo(tempFile.FilePath);
        Assert.Equal(143360, fileInfo.Length);
    }

    [Fact]
    [Trait("Category", "FullDiskTests")]
    public void Export_FormatConversion_DskToPo_Works()
    {
        using var sourceCopy = TempDiskImageCopy.TryCreate(TestDiskImages.DosDsk);
        if (sourceCopy == null)
        {
            return;
        }

        // Import from temp copy (DOS order)
        var importer = new SectorImporter();
        var disk = importer.Import(sourceCopy.FilePath);

        // Export as PO (ProDOS order) - automatically cleaned up on dispose
        var exporter = new SectorExporter(DiskFormat.Po);
        using var tempPoFile = new TempOutputFile(".po");

        exporter.Export(disk, tempPoFile.FilePath);

        // Re-import as PO
        var poImporter = new SectorImporter();
        var reimportedDisk = poImporter.Import(tempPoFile.FilePath);

        // Should have same number of tracks
        Assert.Equal(disk.TrackCount, reimportedDisk.TrackCount);
    }

    */
}
