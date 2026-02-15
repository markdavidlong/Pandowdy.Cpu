/*
using Pandowdy.EmuCore.DiskII;
using Pandowdy.EmuCore.DiskII.Exporters;
using Pandowdy.EmuCore.DiskII.Importers;
using Xunit;
using Xunit.Abstractions;

namespace Pandowdy.EmuCore.Tests.DiskII.Exporters;

public class WozExporterDebugTest(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    [Trait("Category", "FullDiskTests")]
    public void DebugWozStructure()
    {
        using var sourceCopy = TempDiskImageCopy.TryCreate(TestDiskImages.BlankNib);
        if (sourceCopy == null)
        {
            _output.WriteLine("Test images not available");
            return;
        }

        var nibImporter = new NibImporter();
        var disk = nibImporter.Import(sourceCopy.FilePath);

        // Export to temp output file - automatically cleaned up on dispose
        var exporter = new WozExporter();
        using var tempFile = new TempOutputFile(".woz");

        exporter.Export(disk, tempFile.FilePath);

        byte[] data = File.ReadAllBytes(tempFile.FilePath);
        _output.WriteLine($"Total file size: {data.Length} bytes");
        _output.WriteLine("");

        // Header
        _output.WriteLine("=== HEADER (bytes 0-11) ===");
        _output.WriteLine($"Signature: {(char)data[0]}{(char)data[1]}{(char)data[2]}{(char)data[3]}");
        _output.WriteLine("");

        // INFO chunk
        int infoStart = 12;
        _output.WriteLine($"=== INFO CHUNK (starts at byte {infoStart}) ===");
        string infoId = $"{(char)data[infoStart]}{(char)data[infoStart+1]}{(char)data[infoStart+2]}{(char)data[infoStart+3]}";
        uint infoSize = BitConverter.ToUInt32(data, infoStart + 4);
        _output.WriteLine($"ID: {infoId}");
        _output.WriteLine($"Size: {infoSize} bytes");
        _output.WriteLine($"Chunk ends at byte: {infoStart + 8 + infoSize}");
        _output.WriteLine("");

        // TMAP chunk (contiguous, immediately after INFO payload)
        int tmapStart = infoStart + 8 + (int)infoSize;
        _output.WriteLine($"=== TMAP CHUNK (starts at byte {tmapStart}) ===");
        string tmapId = $"{(char)data[tmapStart]}{(char)data[tmapStart+1]}{(char)data[tmapStart+2]}{(char)data[tmapStart+3]}";
        uint tmapSize = BitConverter.ToUInt32(data, tmapStart + 4);
        _output.WriteLine($"ID: {tmapId}");
        _output.WriteLine($"Size: {tmapSize} bytes");
        _output.WriteLine($"Chunk ends at byte: {tmapStart + 8 + tmapSize}");
        _output.WriteLine("");

        // TRKS chunk (contiguous, immediately after TMAP payload)
        int trksStart = tmapStart + 8 + (int)tmapSize;
        _output.WriteLine($"=== TRKS CHUNK (starts at byte {trksStart}) ===");
        string trksId = $"{(char)data[trksStart]}{(char)data[trksStart+1]}{(char)data[trksStart+2]}{(char)data[trksStart+3]}";
        uint trksSize = BitConverter.ToUInt32(data, trksStart + 4);
        _output.WriteLine($"ID: {trksId}");
        _output.WriteLine($"Size: {trksSize} bytes");
        _output.WriteLine($"Payload starts at byte: {trksStart + 8}");
        _output.WriteLine("");

        // Track descriptors
        _output.WriteLine("=== TRACK DESCRIPTORS ===");
        int descriptorStart = trksStart + 8;
        for (int i = 0; i < Math.Min(5, disk.TrackCount); i++)
        {
            int offset = descriptorStart + (i * 8);
            ushort startBlock = BitConverter.ToUInt16(data, offset);
            ushort blockCount = BitConverter.ToUInt16(data, offset + 2);
            uint bitCount = BitConverter.ToUInt32(data, offset + 4);

            int fileByteOffset = startBlock * 512;
            _output.WriteLine($"Track {i}:");
            _output.WriteLine($"  Start Block: {startBlock} (file byte {fileByteOffset})");
            _output.WriteLine($"  Block Count: {blockCount}");
            _output.WriteLine($"  Bit Count: {bitCount}");
            _output.WriteLine($"  Expected at file byte 1536? {fileByteOffset == 1536}");
        }
        _output.WriteLine("");

        _output.WriteLine($"Byte 1536 should be start of track 0 data");
        _output.WriteLine($"Track 0 should start at block 3: {1536 / 512} = 3");
    }
}
*/
