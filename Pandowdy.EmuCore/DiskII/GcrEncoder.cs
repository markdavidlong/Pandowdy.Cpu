namespace Pandowdy.EmuCore.DiskII;

/// <summary>
/// Encodes logical sector data into Apple II GCR (Group Code Recording) format.
/// </summary>
/// <remarks>
/// <para>
/// This class synthesizes disk tracks in the 6&amp;2 GCR format used by 16-sector Disk II disks.
/// It's used when emulating sector-based disk images (DSK, DO, PO) that don't contain
/// pre-encoded GCR data.
/// </para>
/// <para>
/// <strong>6&amp;2 Encoding:</strong><br/>
/// Each 256-byte sector is split into:
/// - 256 6-bit values (high 6 bits of each byte)
/// - 86 6-bit values (packed from low 2 bits of each byte)
/// = 342 total 6-bit values, mapped to valid GCR disk bytes
/// </para>
/// </remarks>
public class GcrEncoder
{
    // 6&2 encoding table: translates 6-bit values (0-63) to valid GCR disk bytes
    // Valid GCR bytes have odd bit density to ensure self-clocking
    private static readonly byte[] Encode62Table =
    [
        0x96, 0x97, 0x9A, 0x9B, 0x9D, 0x9E, 0x9F, 0xA6,
        0xA7, 0xAB, 0xAC, 0xAD, 0xAE, 0xAF, 0xB2, 0xB3,
        0xB4, 0xB5, 0xB6, 0xB7, 0xB9, 0xBA, 0xBB, 0xBC,
        0xBD, 0xBE, 0xBF, 0xCB, 0xCD, 0xCE, 0xCF, 0xD3,
        0xD6, 0xD7, 0xD9, 0xDA, 0xDB, 0xDC, 0xDD, 0xDE,
        0xDF, 0xE5, 0xE6, 0xE7, 0xE9, 0xEA, 0xEB, 0xEC,
        0xED, 0xEE, 0xEF, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6,
        0xF7, 0xF9, 0xFA, 0xFB, 0xFC, 0xFD, 0xFE, 0xFF
    ];

    /// <summary>
    /// Writes a GCR-encoded address field to the buffer.
    /// </summary>
    /// <param name="buffer">Track buffer to write to.</param>
    /// <param name="offset">Starting offset in buffer.</param>
    /// <param name="volume">Volume number (0-254).</param>
    /// <param name="track">Track number (0-34).</param>
    /// <param name="sector">Sector number (0-15).</param>
    /// <returns>Number of bytes written.</returns>
    public int WriteAddressField(byte[] buffer, int offset, byte volume, int track, int sector)
    {
        int start = offset;

        // Gap: self-sync bytes (10 is typical)
        for (int i = 0; i < 10; i++)
        {
            buffer[offset++] = 0xFF;
        }

        // Address field prologue
        buffer[offset++] = 0xD5;  // Prologue byte 1
        buffer[offset++] = 0xAA;  // Prologue byte 2
        buffer[offset++] = 0x96;  // Prologue byte 3 (address field marker)

        // 4-4 encoded fields (volume, track, sector, checksum)
        buffer[offset++] = Encode44High(volume);
        buffer[offset++] = Encode44Low(volume);
        buffer[offset++] = Encode44High((byte)track);
        buffer[offset++] = Encode44Low((byte)track);
        buffer[offset++] = Encode44High((byte)sector);
        buffer[offset++] = Encode44Low((byte)sector);

        // Checksum (XOR of volume, track, sector)
        byte checksum = (byte)(volume ^ track ^ sector);
        buffer[offset++] = Encode44High(checksum);
        buffer[offset++] = Encode44Low(checksum);

        // Address field epilogue
        buffer[offset++] = 0xDE;  // Epilogue byte 1
        buffer[offset++] = 0xAA;  // Epilogue byte 2
        buffer[offset++] = 0xEB;  // Epilogue byte 3

        return offset - start;
    }

    /// <summary>
    /// Writes a GCR-encoded data field to the buffer.
    /// </summary>
    /// <param name="buffer">Track buffer to write to.</param>
    /// <param name="offset">Starting offset in buffer.</param>
    /// <param name="sectorData">256 bytes of logical sector data.</param>
    /// <returns>Number of bytes written.</returns>
    public int WriteDataField(byte[] buffer, int offset, byte[] sectorData)
    {
        if (sectorData.Length != 256)
        {
            throw new ArgumentException("Sector data must be exactly 256 bytes", nameof(sectorData));
        }

        int start = offset;

        // Gap: self-sync bytes (5 is typical)
        for (int i = 0; i < 5; i++)
        {
            buffer[offset++] = 0xFF;
        }

        // Data field prologue
        buffer[offset++] = 0xD5;  // Prologue byte 1
        buffer[offset++] = 0xAA;  // Prologue byte 2
        buffer[offset++] = 0xAD;  // Prologue byte 3 (data field marker)

        // 6&2 encode the 256-byte sector into 342 bytes
        byte[] encoded = Encode62Sector(sectorData);

        // Write encoded bytes with running checksum
        byte checksum = 0;
        for (int i = 0; i < encoded.Length; i++)
        {
            checksum ^= encoded[i];
            buffer[offset++] = checksum;
        }

        // Write final checksum byte
        buffer[offset++] = checksum;

        // Data field epilogue
        buffer[offset++] = 0xDE;  // Epilogue byte 1
        buffer[offset++] = 0xAA;  // Epilogue byte 2
        buffer[offset++] = 0xEB;  // Epilogue byte 3

        return offset - start;
    }

    /// <summary>
    /// Writes a gap of self-sync bytes.
    /// </summary>
    /// <param name="buffer">Track buffer to write to.</param>
    /// <param name="offset">Starting offset in buffer.</param>
    /// <param name="gapSize">Number of sync bytes to write.</param>
    /// <returns>Number of bytes written.</returns>
    public int WriteSyncGap(byte[] buffer, int offset, int gapSize)
    {
        for (int i = 0; i < gapSize; i++)
        {
            buffer[offset++] = 0xFF;
        }
        return gapSize;
    }

    /// <summary>
    /// 4-4 encodes the high nibble of a byte.
    /// </summary>
    private static byte Encode44High(byte value)
    {
        return (byte)(0xAA | ((value >> 1) & 0x55));
    }

    /// <summary>
    /// 4-4 encodes the low nibble of a byte.
    /// </summary>
    private static byte Encode44Low(byte value)
    {
        return (byte)(0xAA | (value & 0x55));
    }

    /// <summary>
    /// Performs 6&amp;2 encoding on a 256-byte sector.
    /// </summary>
    /// <param name="sectorData">256 bytes of logical sector data.</param>
    /// <returns>342 bytes of 6&amp;2 encoded data.</returns>
    private static byte[] Encode62Sector(byte[] sectorData)
    {
        byte[] encoded = new byte[342];

        // Split each byte: high 6 bits and low 2 bits
        // First pass: encode the high 6 bits (reverse order for proper XOR checksumming)
        for (int i = 0; i < 256; i++)
        {
            encoded[255 - i] = Encode62Table[sectorData[i] >> 2];
        }

        // Second pass: pack low 2 bits from 3 bytes into each 6-bit value
        // 3 bytes (6 low bits total) pack into 1 6-bit value
        for (int i = 0; i < 86; i++)
        {
            byte bits = 0;

            // Extract 2 low bits from up to 3 bytes
            if (i * 3 < 256)
            {
                bits |= (byte)((sectorData[i * 3] & 0x03) << 0);
            }
            if (i * 3 + 1 < 256)
            {
                bits |= (byte)((sectorData[i * 3 + 1] & 0x03) << 2);
            }
            if (i * 3 + 2 < 256)
            {
                bits |= (byte)((sectorData[i * 3 + 2] & 0x03) << 4);
            }

            encoded[255 + i + 1] = Encode62Table[bits];
        }

        return encoded;
    }
}
