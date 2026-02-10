using Pandowdy.EmuCore.DiskII;
using Pandowdy.EmuCore.DiskII.Importers;
using Xunit;
using Xunit.Abstractions;

namespace Pandowdy.EmuCore.Tests.DiskII.Exporters;

public class CompareWozStructureTest
{
    private readonly ITestOutputHelper _output;

    public CompareWozStructureTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void CompareRealWozStructure()
    {
        if (!TestDiskImages.TestImagesAvailable || !File.Exists(TestDiskImages.TestWoz))
        {
            _output.WriteLine("Test WOZ file not available");
            return;
        }

        byte[] data = File.ReadAllBytes(TestDiskImages.TestWoz);
        _output.WriteLine($"Real WOZ file size: {data.Length} bytes");
        _output.WriteLine("");

        // Check WOZ version
        string signature = $"{(char)data[0]}{(char)data[1]}{(char)data[2]}{(char)data[3]}";
        _output.WriteLine($"=== HEADER ===");
        _output.WriteLine($"Signature: {signature}");

        if (signature != "WOZ1" && signature != "WOZ2")
        {
            _output.WriteLine("ERROR: Not a valid WOZ file!");
            return;
        }

        bool isWoz2 = (signature == "WOZ2");
        _output.WriteLine($"Format: WOZ {(isWoz2 ? "2.0" : "1.0")}");
        _output.WriteLine("");

        if (!isWoz2)
        {
            _output.WriteLine("NOTE: This is WOZ 1.0 format - TRKS structure is completely different!");
            _output.WriteLine("WOZ 1.0 uses fixed 6656-byte track entries");
            _output.WriteLine("WOZ 2.0 uses 8-byte track descriptors + variable-length track data");
            return;
        }

        // WOZ 2.0 format
        // TRKS chunk starts at byte 256 per spec
        int trksStart = 256;
        _output.WriteLine($"=== TRKS CHUNK (starts at byte {trksStart}) ===");
        string trksId = $"{(char)data[trksStart]}{(char)data[trksStart+1]}{(char)data[trksStart+2]}{(char)data[trksStart+3]}";
        uint trksSize = BitConverter.ToUInt32(data, trksStart + 4);
        _output.WriteLine($"ID: {trksId}");
        _output.WriteLine($"Size: {trksSize} bytes");
        _output.WriteLine($"Payload starts at byte: {trksStart + 8}");
        _output.WriteLine("");

        // Track descriptors
        _output.WriteLine("=== TRACK DESCRIPTORS (first 5) ===");
        int descriptorStart = trksStart + 8;
        for (int i = 0; i < 5; i++)
        {
            int offset = descriptorStart + (i * 8);
            ushort startBlock = BitConverter.ToUInt16(data, offset);
            ushort blockCount = BitConverter.ToUInt16(data, offset + 2);
            uint bitCount = BitConverter.ToUInt32(data, offset + 4);

            if (startBlock == 0 && blockCount == 0)
            {
                _output.WriteLine($"Track {i}: EMPTY");
                continue;
            }

            int fileByteOffset = startBlock * 512;
            _output.WriteLine($"Track {i}:");
            _output.WriteLine($"  Start Block: {startBlock} (file byte {fileByteOffset})");
            _output.WriteLine($"  Block Count: {blockCount}");
            _output.WriteLine($"  Bit Count: {bitCount}");
        }
    }
}
