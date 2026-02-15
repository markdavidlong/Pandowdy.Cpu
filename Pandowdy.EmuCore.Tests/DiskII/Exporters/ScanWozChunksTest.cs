/*
using Pandowdy.EmuCore.DiskII;
using Xunit;
using Xunit.Abstractions;

namespace Pandowdy.EmuCore.Tests.DiskII.Exporters;

public class ScanWozChunksTest(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    [Trait("Category", "FullDiskTests")]
    public void ScanAllChunks()
    {
        using var sourceCopy = TempDiskImageCopy.TryCreate(TestDiskImages.TestWoz);
        if (sourceCopy == null)
        {
            _output.WriteLine("Test WOZ file not available");
            return;
        }

        byte[] data = File.ReadAllBytes(sourceCopy.FilePath);
        
        // Check signature
        string sig = $"{(char)data[0]}{(char)data[1]}{(char)data[2]}{(char)data[3]}";
        _output.WriteLine($"Signature: {sig}");
        _output.WriteLine($"File size: {data.Length} bytes");
        _output.WriteLine("");
        
        // Scan all chunks starting after 12-byte header
        int offset = 12;
        int chunkNum = 1;
        
        while (offset + 8 <= data.Length)
        {
            // Read chunk ID and size
            string chunkId = $"{(char)data[offset]}{(char)data[offset+1]}{(char)data[offset+2]}{(char)data[offset+3]}";
            uint chunkSize = BitConverter.ToUInt32(data, offset + 4);
            
            _output.WriteLine($"Chunk #{chunkNum} at byte {offset}:");
            _output.WriteLine($"  ID: {chunkId}");
            _output.WriteLine($"  Size: {chunkSize} bytes");
            _output.WriteLine($"  Payload: bytes {offset + 8} to {offset + 8 + chunkSize - 1}");
            _output.WriteLine($"  Total chunk: {8 + chunkSize} bytes (no per-chunk CRC)");
            
            // Special handling for TRKS to show first track descriptor
            if (chunkId == "TRKS")
            {
                int descriptorOffset = offset + 8;
                ushort startBlock = BitConverter.ToUInt16(data, descriptorOffset);
                ushort blockCount = BitConverter.ToUInt16(data, descriptorOffset + 2);
                uint bitCount = BitConverter.ToUInt32(data, descriptorOffset + 4);
                _output.WriteLine($"  Track 0: block {startBlock}, {blockCount} blocks, {bitCount} bits");
                _output.WriteLine($"  Track 0 data at file byte: {startBlock * 512}");
            }
            
            _output.WriteLine("");
            
            // Move to next chunk (ID + size + payload, no per-chunk CRC)
            offset += 8 + (int)chunkSize;
            chunkNum++;
            
            if (offset >= data.Length)
            {
                break;
            }
        }
        
        _output.WriteLine($"End of file at byte {data.Length}");
    }
}
*/
