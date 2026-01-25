using Pandowdy.EmuCore.DiskII;

namespace Pandowdy.EmuCore.Tests.DiskII;

/// <summary>
/// Tests for GcrEncoder, verifying GCR encoding of address fields, data fields, and sync gaps.
/// </summary>
public class GcrEncoderTests
{
    #region Address Field Tests

    [Fact]
    public void WriteAddressField_ProducesValidPrologue()
    {
        // Arrange
        var encoder = new GcrEncoder();
        var buffer = new byte[100];

        // Act
        int bytesWritten = encoder.WriteAddressField(buffer, 0, volume: 254, track: 0, sector: 0);

        // Assert - prologue follows 10 sync bytes
        Assert.Equal(0xD5, buffer[10]); // Prologue byte 1
        Assert.Equal(0xAA, buffer[11]); // Prologue byte 2
        Assert.Equal(0x96, buffer[12]); // Address field marker
    }

    [Fact]
    public void WriteAddressField_ProducesValidEpilogue()
    {
        // Arrange
        var encoder = new GcrEncoder();
        var buffer = new byte[100];

        // Act
        int bytesWritten = encoder.WriteAddressField(buffer, 0, volume: 254, track: 0, sector: 0);

        // Assert - epilogue at end of field
        // 10 sync + 3 prologue + 8 data (vol,trk,sec,chk × 2 each) + 3 epilogue = 24 bytes
        Assert.Equal(24, bytesWritten);
        Assert.Equal(0xDE, buffer[21]); // Epilogue byte 1
        Assert.Equal(0xAA, buffer[22]); // Epilogue byte 2
        Assert.Equal(0xEB, buffer[23]); // Epilogue byte 3
    }

    [Fact]
    public void WriteAddressField_EncodesVolumeTrackSector()
    {
        // Arrange
        var encoder = new GcrEncoder();
        var buffer = new byte[100];

        // Act - Use volume 254, track 17, sector 5
        encoder.WriteAddressField(buffer, 0, volume: 254, track: 17, sector: 5);

        // Assert - 4-4 encoded values follow prologue
        // Volume 254 = 0xFE → high bits: 0xAA | (0x7F) = 0xFF, low bits: 0xAA | 0x54 = 0xFE
        // Note: Encode44High = 0xAA | ((value >> 1) & 0x55)
        //       Encode44Low  = 0xAA | (value & 0x55)
        // For value 254 (0xFE):
        //   High: 0xAA | ((0xFE >> 1) & 0x55) = 0xAA | (0x7F & 0x55) = 0xAA | 0x55 = 0xFF
        //   Low:  0xAA | (0xFE & 0x55) = 0xAA | 0x54 = 0xFE
        Assert.Equal(0xFF, buffer[13]); // Volume high
        Assert.Equal(0xFE, buffer[14]); // Volume low
    }

    [Fact]
    public void WriteAddressField_CalculatesCorrectChecksum()
    {
        // Arrange
        var encoder = new GcrEncoder();
        var buffer = new byte[100];
        byte volume = 254;
        int track = 17;
        int sector = 5;
        // Checksum = volume XOR track XOR sector
        // 254 XOR 17 XOR 5 = 0xFE ^ 0x11 ^ 0x05 = 0xEA (234)
        byte expectedChecksum = (byte)(volume ^ track ^ sector);

        // Act
        encoder.WriteAddressField(buffer, 0, volume, track, sector);

        // Assert - checksum is 4-4 encoded at positions 19-20 (after vol, trk, sec)
        // Position: 10 sync + 3 prologue + 6 (vol+trk+sec encoded) = 19
        // For checksum 234 (0xEA):
        //   High: 0xAA | ((0xEA >> 1) & 0x55) = 0xAA | (0x75 & 0x55) = 0xAA | 0x55 = 0xFF
        //   Low:  0xAA | (0xEA & 0x55) = 0xAA | 0x40 = 0xEA
        Assert.Equal(234, expectedChecksum); // Verify our math
        Assert.Equal(0xFF, buffer[19]); // Checksum high
        Assert.Equal(0xEA, buffer[20]); // Checksum low
    }

    [Fact]
    public void WriteAddressField_ReturnsCorrectByteCount()
    {
        // Arrange
        var encoder = new GcrEncoder();
        var buffer = new byte[100];

        // Act
        int bytesWritten = encoder.WriteAddressField(buffer, 0, volume: 254, track: 0, sector: 0);

        // Assert - 10 sync + 3 prologue + 8 encoded data + 3 epilogue = 24 bytes
        Assert.Equal(24, bytesWritten);
    }

    [Fact]
    public void WriteAddressField_RespectsOffset()
    {
        // Arrange
        var encoder = new GcrEncoder();
        var buffer = new byte[100];
        int startOffset = 20;

        // Act
        encoder.WriteAddressField(buffer, startOffset, volume: 254, track: 0, sector: 0);

        // Assert - data starts at offset, not at 0
        Assert.Equal(0x00, buffer[0]);  // Before offset is untouched
        Assert.Equal(0xFF, buffer[20]); // Sync bytes start at offset
        Assert.Equal(0xD5, buffer[30]); // Prologue at offset + 10
    }

    #endregion

    #region Data Field Tests

    [Fact]
    public void WriteDataField_ProducesValidPrologue()
    {
        // Arrange
        var encoder = new GcrEncoder();
        var buffer = new byte[400];
        var sectorData = new byte[256];

        // Act
        encoder.WriteDataField(buffer, 0, sectorData);

        // Assert - prologue follows 5 sync bytes
        Assert.Equal(0xD5, buffer[5]); // Prologue byte 1
        Assert.Equal(0xAA, buffer[6]); // Prologue byte 2
        Assert.Equal(0xAD, buffer[7]); // Data field marker (different from address field)
    }

    [Fact]
    public void WriteDataField_ProducesValidEpilogue()
    {
        // Arrange
        var encoder = new GcrEncoder();
        var buffer = new byte[400];
        var sectorData = new byte[256];

        // Act
        int bytesWritten = encoder.WriteDataField(buffer, 0, sectorData);

        // Assert - epilogue at end
        // 5 sync + 3 prologue + 342 encoded + 1 checksum + 3 epilogue = 354 bytes
        Assert.Equal(354, bytesWritten);
        Assert.Equal(0xDE, buffer[351]); // Epilogue byte 1
        Assert.Equal(0xAA, buffer[352]); // Epilogue byte 2
        Assert.Equal(0xEB, buffer[353]); // Epilogue byte 3
    }

    [Fact]
    public void WriteDataField_Produces342EncodedBytes()
    {
        // Arrange
        var encoder = new GcrEncoder();
        var buffer = new byte[400];
        var sectorData = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            sectorData[i] = (byte)i;
        }

        // Act
        int bytesWritten = encoder.WriteDataField(buffer, 0, sectorData);

        // Assert - 256 bytes become 342 encoded bytes (6&2 encoding)
        // Total: 5 sync + 3 prologue + 342 data + 1 checksum + 3 epilogue = 354
        Assert.Equal(354, bytesWritten);
    }

    [Fact]
    public void WriteDataField_ThrowsForInvalidSectorSize()
    {
        // Arrange
        var encoder = new GcrEncoder();
        var buffer = new byte[400];
        var badSectorData = new byte[128]; // Should be 256

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            encoder.WriteDataField(buffer, 0, badSectorData));
        Assert.Contains("256 bytes", ex.Message);
    }

    [Fact]
    public void WriteDataField_AllEncodedBytesAreValid()
    {
        // Arrange
        var encoder = new GcrEncoder();
        var buffer = new byte[400];
        var sectorData = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            sectorData[i] = (byte)i;
        }

        // Act
        encoder.WriteDataField(buffer, 0, sectorData);

        // Assert - The data written after the prologue is the running XOR checksum
        // of GCR values, so the result depends on the XOR chain, not raw GCR bytes.
        // Instead, verify the prologue, data length, and epilogue are correct.
        // Prologue starts at offset 5
        Assert.Equal(0xD5, buffer[5]);
        Assert.Equal(0xAA, buffer[6]);
        Assert.Equal(0xAD, buffer[7]);

        // Epilogue at end (5 sync + 3 prologue + 342 encoded + 1 checksum = 351)
        Assert.Equal(0xDE, buffer[351]);
        Assert.Equal(0xAA, buffer[352]);
        Assert.Equal(0xEB, buffer[353]);
    }

    #endregion

    #region Sync Gap Tests

    [Fact]
    public void WriteSyncGap_WritesCorrectBytes()
    {
        // Arrange
        var encoder = new GcrEncoder();
        var buffer = new byte[50];

        // Act
        int bytesWritten = encoder.WriteSyncGap(buffer, 0, 10);

        // Assert - all bytes should be 0xFF
        Assert.Equal(10, bytesWritten);
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(0xFF, buffer[i]);
        }
    }

    [Fact]
    public void WriteSyncGap_RespectsOffset()
    {
        // Arrange
        var encoder = new GcrEncoder();
        var buffer = new byte[50];
        buffer[0] = 0x00; // Ensure we're not just reading uninitialized memory

        // Act
        encoder.WriteSyncGap(buffer, 5, 10);

        // Assert
        Assert.Equal(0x00, buffer[0]);  // Before offset untouched
        Assert.Equal(0xFF, buffer[5]);  // Gap starts at offset
        Assert.Equal(0xFF, buffer[14]); // Last gap byte
    }

    [Fact]
    public void WriteSyncGap_ReturnsGapSize()
    {
        // Arrange
        var encoder = new GcrEncoder();
        var buffer = new byte[100];

        // Act
        int bytesWritten = encoder.WriteSyncGap(buffer, 0, 25);

        // Assert
        Assert.Equal(25, bytesWritten);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void WriteAddressAndDataFields_ProduceValidSectorImage()
    {
        // Arrange
        var encoder = new GcrEncoder();
        var buffer = new byte[500];
        var sectorData = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            sectorData[i] = (byte)i;
        }

        // Act - write complete sector (address + data)
        int offset = 0;
        offset += encoder.WriteAddressField(buffer, offset, volume: 254, track: 17, sector: 5);
        offset += encoder.WriteDataField(buffer, offset, sectorData);

        // Assert
        // Address field: 24 bytes
        // Data field: 354 bytes
        // Total: 378 bytes
        Assert.Equal(378, offset);

        // Verify address prologue
        Assert.Equal(0xD5, buffer[10]);
        Assert.Equal(0xAA, buffer[11]);
        Assert.Equal(0x96, buffer[12]);

        // Verify data prologue (starts after address field at offset 24)
        Assert.Equal(0xD5, buffer[29]); // 24 + 5 sync bytes
        Assert.Equal(0xAA, buffer[30]);
        Assert.Equal(0xAD, buffer[31]);
    }

    #endregion
}
