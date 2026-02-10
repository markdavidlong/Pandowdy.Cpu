// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.DiskII;
using Pandowdy.EmuCore.DiskII.Exporters;
using Pandowdy.EmuCore.DiskII.Importers;
using Pandowdy.EmuCore.Tests.DiskII;
using Xunit;
using Xunit.Abstractions;

namespace Pandowdy.EmuCore.Tests.DiskII.Exporters;

/// <summary>
/// Debug test to scan the structure of a newly exported WOZ file.
/// </summary>
public class ScanExportedWozTest
{
    private readonly ITestOutputHelper _output;

    public ScanExportedWozTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    [Trait("Category", "FullDiskTests")]
    public void ScanNewlyExportedWozFile()
    {
        // Import from temp copy of test image
        using var sourceCopy = TempDiskImageCopy.TryCreate(TestDiskImages.BlankNib);
        if (sourceCopy == null)
        {
            return; // Skip if test images not available
        }

        var importer = new NibImporter();
        var disk = importer.Import(sourceCopy.FilePath);

        // Export to temp output file - automatically cleaned up on dispose
        var exporter = new WozExporter();
        using var tempFile = new TempOutputFile(".woz");

        exporter.Export(disk, tempFile.FilePath);

        // Read the file
        byte[] data = File.ReadAllBytes(tempFile.FilePath);
        _output.WriteLine($"Exported file size: {data.Length} bytes");
        _output.WriteLine("");

        // Check header
        string sig = System.Text.Encoding.ASCII.GetString(data, 0, 4);
        _output.WriteLine($"Signature: {sig}");
        _output.WriteLine("");

        // Scan chunks
        int posn = 12; // Start after header
        int chunkNum = 0;

        while (posn < data.Length - 8)
        {
            chunkNum++;

            // Read chunk ID and size
            string id = System.Text.Encoding.ASCII.GetString(data, posn, 4);
            uint size = BitConverter.ToUInt32(data, posn + 4);

            _output.WriteLine($"Chunk #{chunkNum} at byte {posn}:");
            _output.WriteLine($"  ID: {id}");
            _output.WriteLine($"  Size: {size} bytes (payload only)");

            int totalChunkSize = 8 + (int)size; // ID + size field + payload (no per-chunk CRC)
            _output.WriteLine($"  Total chunk: {totalChunkSize} bytes (header + payload)");
            _output.WriteLine($"  Payload: bytes {posn + 8} to {posn + 8 + size - 1}");
            _output.WriteLine("");

            // Special handling for standard chunks
            if (id == "INFO")
            {
                _output.WriteLine($"  INFO chunk should be at byte 12, actual: {posn}");
                _output.WriteLine($"  INFO payload should start at byte 20, actual: {posn + 8}");
            }
            else if (id == "TMAP")
            {
                _output.WriteLine($"  TMAP chunk should be at byte 80, actual: {posn}");
                _output.WriteLine($"  TMAP payload should start at byte 88, actual: {posn + 8}");
            }
            else if (id == "TRKS")
            {
                _output.WriteLine($"  TRKS chunk should be at byte 248, actual: {posn}");
                _output.WriteLine($"  Track data should start at byte 1536");
            }

            // Move to next chunk (skip header + payload)
            posn += totalChunkSize;

            if (chunkNum > 10)
            {
                break; // Safety limit
            }
        }
    }
}
