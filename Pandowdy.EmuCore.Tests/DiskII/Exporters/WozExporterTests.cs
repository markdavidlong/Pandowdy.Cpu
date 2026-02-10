// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using CommonUtil;
using Pandowdy.EmuCore.DiskII;
using Pandowdy.EmuCore.DiskII.Exporters;
using Pandowdy.EmuCore.DiskII.Importers;
using Xunit;

namespace Pandowdy.EmuCore.Tests.DiskII.Exporters;

/// <summary>
/// Unit tests for the WozExporter class.
/// </summary>
public class WozExporterTests
{
    [Fact]
    public void OutputFormat_ReturnsWoz()
    {
        var exporter = new WozExporter();
        Assert.Equal(DiskFormat.Woz, exporter.OutputFormat);
    }

    [Fact]
    public void Export_NullDisk_ThrowsArgumentNullException()
    {
        var exporter = new WozExporter();
        Assert.Throws<ArgumentNullException>(() => exporter.Export(null!, "test.woz"));
    }

    [Fact]
    public void Export_NullFilePath_ThrowsArgumentNullException()
    {
        var exporter = new WozExporter();
        var disk = new InternalDiskImage();
        Assert.Throws<ArgumentNullException>(() => exporter.Export(disk, (string)null!));
        Assert.Throws<ArgumentNullException>(() => exporter.Export(disk, ""));
    }

    [Fact]
    public void ExportStream_NullDisk_ThrowsArgumentNullException()
    {
        var exporter = new WozExporter();
        using var stream = new MemoryStream();
        Assert.Throws<ArgumentNullException>(() => exporter.Export(null!, stream));
    }

    [Fact]
    public void ExportStream_NullStream_ThrowsArgumentNullException()
    {
        var exporter = new WozExporter();
        var disk = new InternalDiskImage();
        Assert.Throws<ArgumentNullException>(() => exporter.Export(disk, (Stream)null!));
    }

    [Fact]
    [Trait("Category", "FullDiskTests")]
    public void Export_CreatesValidWozFile()
    {
        var exporter = new WozExporter();

        // Use temp copy of blank.nib as source
        using var sourceCopy = TempDiskImageCopy.TryCreate(TestDiskImages.BlankNib);
        if (sourceCopy == null)
        {
            return; // Skip if test images not available
        }

        var importer = new NibImporter();
        var disk = importer.Import(sourceCopy.FilePath);

        // Export to temp output file - automatically cleaned up on dispose
        using var tempFile = new TempOutputFile(".woz");

        exporter.Export(disk, tempFile.FilePath);
        Assert.True(File.Exists(tempFile.FilePath));

        // Verify file has reasonable size (header + chunks)
        var fileInfo = new FileInfo(tempFile.FilePath);
        Assert.True(fileInfo.Length > 0, "WOZ file should not be empty");

        // Verify file starts with WOZ2 signature
        byte[] header = new byte[8];
        using (var fs = File.OpenRead(tempFile.FilePath))
        {
            fs.Read(header, 0, 8);
        }
        Assert.Equal((byte)'W', header[0]);
        Assert.Equal((byte)'O', header[1]);
        Assert.Equal((byte)'Z', header[2]);
        Assert.Equal((byte)'2', header[3]);
    }

    [Fact]
    [Trait("Category", "FullDiskTests")]
    public void ExportStream_WritesValidWozData()
    {
        var exporter = new WozExporter();

        using var sourceCopy = TempDiskImageCopy.TryCreate(TestDiskImages.BlankNib);
        if (sourceCopy == null)
        {
            return; // Skip if test images not available
        }

        var importer = new NibImporter();
        var disk = importer.Import(sourceCopy.FilePath);

        using var stream = new MemoryStream();

        exporter.Export(disk, stream);

        Assert.True(stream.Length > 0, "WOZ stream should not be empty");

        // Verify stream starts with WOZ2 signature
        stream.Position = 0;
        byte[] header = new byte[8];
        stream.Read(header, 0, 8);
        Assert.Equal((byte)'W', header[0]);
        Assert.Equal((byte)'O', header[1]);
        Assert.Equal((byte)'Z', header[2]);
        Assert.Equal((byte)'2', header[3]);
    }

    [Fact]
    [Trait("Category", "FullDiskTests")]
    public void Export_RoundTrip_PreservesTrackData()
    {
        using var sourceCopy = TempDiskImageCopy.TryCreate(TestDiskImages.BlankNib);
        if (sourceCopy == null)
        {
            return; // Skip if test images not available
        }

        // Create a disk with known data from temp copy
        var nibImporter = new NibImporter();
        var original = nibImporter.Import(sourceCopy.FilePath);

        // Export to WOZ - automatically cleaned up on dispose
        var exporter = new WozExporter();
        using var tempFile = new TempOutputFile(".woz");

        exporter.Export(original, tempFile.FilePath);

        // Re-import the WOZ file
        var wozImporter = new WozImporter();
        var reloaded = wozImporter.Import(tempFile.FilePath);

        // Verify track counts match
        Assert.Equal(original.TrackCount, reloaded.TrackCount);

        // Verify track bit counts match
        for (int i = 0; i < original.TrackCount; i++)
        {
            Assert.Equal(original.TrackBitCounts[i], reloaded.TrackBitCounts[i]);
        }

        // Verify actual track byte data is identical
        for (int i = 0; i < original.TrackCount; i++)
        {
            CircularBitBuffer origTrack = original.Tracks[i];
            CircularBitBuffer reloadedTrack = reloaded.Tracks[i];

            origTrack.BitPosition = 0;
            reloadedTrack.BitPosition = 0;

            int byteCount = (original.TrackBitCounts[i] + 7) / 8;
            for (int b = 0; b < byteCount; b++)
            {
                byte origByte = origTrack.ReadOctet();
                byte reloadedByte = reloadedTrack.ReadOctet();
                Assert.True(origByte == reloadedByte,
                    $"Track {i} byte {b}: expected 0x{origByte:X2}, got 0x{reloadedByte:X2}");
            }
        }

        // Verify metadata
        Assert.Equal(original.IsWriteProtected, reloaded.IsWriteProtected);
        Assert.Equal(original.OptimalBitTiming, reloaded.OptimalBitTiming);
    }

    // Future tests for WOZ export:
    // - Export_PreservesWriteProtection
    // - Export_HandlesVariableLengthTracks
    // - Export_FormatConversion_DskToWoz_Works
    // - Export_FormatConversion_NibToWoz_Works
}
