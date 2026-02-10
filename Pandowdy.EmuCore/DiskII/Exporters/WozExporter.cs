// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Diagnostics;
using System.IO.Hashing;
using System.Text;
using CommonUtil;

namespace Pandowdy.EmuCore.DiskII.Exporters;

/// <summary>
/// Exports internal disk image format to WOZ 2.0 format (.woz).
/// </summary>
/// <remarks>
/// <para>
/// <strong>WOZ 2.0 Format:</strong><br/>
/// This exporter creates WOZ 2.0 files manually, writing the format structure directly:
/// - Header (12 bytes): "WOZ2" signature + format marker + newline sequences + CRC placeholder
/// - INFO chunk (80 bytes): Disk metadata (type, write protection, timing, etc.)
/// - TMAP chunk (248 bytes): Track map (maps 160 quarter-tracks to track indices)
/// - TRKS chunk (variable): Track descriptors + raw track data in 512-byte blocks
/// </para>
/// <para>
/// <strong>Lossless Export:</strong><br/>
/// This export is LOSSLESS - all track data, timing information, and write protection
/// state are preserved perfectly. WOZ is the preferred format for archival and
/// copy-protected disks.
/// </para>
/// <para>
/// <strong>Implementation:</strong><br/>
/// Manual WOZ construction avoids DiskArc API limitations. A single file-level
/// CRC-32 is calculated over all data after the 12-byte header, matching the
/// WOZ 2.0 specification. Chunks are contiguous with no per-chunk CRCs.
/// </para>
/// </remarks>
public class WozExporter : IDiskImageExporter
{
    private const int MaxTracks = 40;
    private const int QuarterTracksPerTrack = 4;
    private const int BlockSize = 512;

    /// <summary>
    /// Format this exporter produces.
    /// </summary>
    public DiskFormat OutputFormat => DiskFormat.Woz;

    /// <summary>
    /// Export internal format to file.
    /// </summary>
    public void Export(InternalDiskImage disk, string filePath)
    {
        if (disk == null)
        {
            throw new ArgumentNullException(nameof(disk));
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentNullException(nameof(filePath));
        }

        try
        {
            byte[] wozData = BuildWoz2File(disk);
            File.WriteAllBytes(filePath, wozData);
            Debug.WriteLine($"WozExporter: Exported to {filePath} ({wozData.Length} bytes, {disk.TrackCount} tracks)");
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to write WOZ file to {filePath}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Export to stream (for embedding in project files).
    /// </summary>
    public void Export(InternalDiskImage disk, Stream stream)
    {
        if (disk == null)
        {
            throw new ArgumentNullException(nameof(disk));
        }

        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        try
        {
            byte[] wozData = BuildWoz2File(disk);
            stream.Write(wozData, 0, wozData.Length);
            Debug.WriteLine($"WozExporter: Exported to stream ({wozData.Length} bytes, {disk.TrackCount} tracks)");
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to write WOZ data to stream: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Builds a complete WOZ 2.0 file from internal disk image.
    /// </summary>
    private byte[] BuildWoz2File(InternalDiskImage disk)
    {
        int trackCount = Math.Min(disk.TrackCount, MaxTracks);

        // Calculate track data size (each track padded to 512-byte blocks)
        int[] trackByteLengths = new int[trackCount];
        int[] trackBlockCounts = new int[trackCount];
        int totalTrackBlocks = 0;

        for (int i = 0; i < trackCount; i++)
        {
            int bitCount = disk.TrackBitCounts[i];
            int byteCount = (bitCount + 7) / 8; // Round up to bytes
            trackByteLengths[i] = byteCount;
            trackBlockCounts[i] = (byteCount + BlockSize - 1) / BlockSize; // Round up to blocks
            totalTrackBlocks += trackBlockCounts[i];
        }

        // Calculate file size
        // WOZ 2.0 layout: Header (12) + INFO chunk (68) + TMAP chunk (168) + TRKS chunk
        // Chunks are contiguous: ID (4) + Size (4) + Payload. No per-chunk CRCs.
        // Single file-level CRC-32 stored in header at bytes 8-11.
        // Track data must start at byte 1536 (block 3).
        const int TrackDataStartByte = 1536;
        int infoChunkSize = 4 + 4 + 60;  // ID + size + payload = 68
        int tmapChunkSize = 4 + 4 + 160;  // ID + size + payload = 168
        int trksHeaderSize = 4 + 4;       // ID + size = 8

        // TRKS payload = descriptors (padded to 1536) + track data
        // Descriptors start at byte 256 (TRKS_CHUNK_START), track data at 1536
        int trksDescriptorSize = TrackDataStartByte - (12 + infoChunkSize + tmapChunkSize + trksHeaderSize);
        int trksDataSize = totalTrackBlocks * BlockSize;
        int trksChunkPayloadSize = trksDescriptorSize + trksDataSize;
        int fileSize = TrackDataStartByte + trksDataSize;

        byte[] wozData = new byte[fileSize];
        int offset = 0;

        // Write header
        offset = WriteHeader(wozData, offset);

        // Write INFO chunk
        offset = WriteInfoChunk(wozData, offset, disk);

        // Write TMAP chunk
        offset = WriteTmapChunk(wozData, offset, trackCount);

        // Write TRKS chunk
        offset = WriteTrksChunk(wozData, offset, disk, trackCount, trackByteLengths, trackBlockCounts, totalTrackBlocks);

        // Compute file-level CRC-32 over everything after the 12-byte header
        uint crc = Crc32.HashToUInt32(new ReadOnlySpan<byte>(wozData, 12, fileSize - 12));
        WriteUInt32LE(wozData, 8, crc);

        return wozData;
    }

    /// <summary>
    /// Writes WOZ 2.0 header (12 bytes).
    /// </summary>
    private int WriteHeader(byte[] data, int offset)
    {
        // "WOZ2" (4 bytes)
        data[offset++] = (byte)'W';
        data[offset++] = (byte)'O';
        data[offset++] = (byte)'Z';
        data[offset++] = (byte)'2';

        // 0xFF marker (1 byte)
        data[offset++] = 0xFF;

        // Line feed, carriage return, line feed (3 bytes)
        data[offset++] = 0x0A; // \n
        data[offset++] = 0x0D; // \r  
        data[offset++] = 0x0A; // \n

        // CRC-32 placeholder (4 bytes) - filled after all chunks are written
        offset += 4;

        return offset;
    }

    /// <summary>
    /// Writes INFO chunk (68 bytes total: 4 ID + 4 size + 60 payload).
    /// </summary>
    private int WriteInfoChunk(byte[] data, int offset, InternalDiskImage disk)
    {
        // Chunk ID: "INFO" (4 bytes)
        WriteAscii(data, ref offset, "INFO");

        // Chunk size: payload only (60 bytes)
        WriteUInt32LE(data, ref offset, 60);

        // INFO payload (60 bytes)
        data[offset++] = 2;     // INFO version: 2
        data[offset++] = 1;     // Disk type: 1 = 5.25"
        data[offset++] = (byte)(disk.IsWriteProtected ? 1 : 0);  // Write protected
        data[offset++] = 0;     // Synchronized: 0 = not synchronized
        data[offset++] = 1;     // Cleaned: 1 = MC3470 fake bits removed

        // Creator string (32 bytes, space-padded)
        string creator = "Pandowdy";
        WriteStringPadded(data, ref offset, creator, 32);

        data[offset++] = 1;     // Disk sides: 1
        data[offset++] = 0;     // Boot sector format: 0 = unknown
        data[offset++] = disk.OptimalBitTiming;  // Optimal bit timing
        data[offset++] = 0;     // Compatible hardware: 0 (low byte)
        data[offset++] = 0;     // Compatible hardware: 0 (high byte)
        data[offset++] = 0;     // Required RAM: 0 (low byte)
        data[offset++] = 0;     // Required RAM: 0 (high byte)

        // Largest track: calculate max blocks needed
        int maxBlocks = 0;
        for (int i = 0; i < disk.TrackCount && i < MaxTracks; i++)
        {
            int byteCount = (disk.TrackBitCounts[i] + 7) / 8;
            int blocks = (byteCount + BlockSize - 1) / BlockSize;
            if (blocks > maxBlocks)
            {
                maxBlocks = blocks;
            }
        }
        data[offset++] = (byte)(maxBlocks & 0xFF);  // Largest track (low byte)
        data[offset++] = (byte)(maxBlocks >> 8);    // Largest track (high byte)

        // Reserved (14 bytes)
        offset += 14;

        return offset;
    }

    /// <summary>
    /// Writes TMAP chunk (168 bytes total: 4 ID + 4 size + 160 payload).
    /// </summary>
    private int WriteTmapChunk(byte[] data, int offset, int trackCount)
    {
        // Chunk ID: "TMAP" (4 bytes)
        WriteAscii(data, ref offset, "TMAP");

        // Chunk size: payload only (160 bytes)
        WriteUInt32LE(data, ref offset, 160);

        int payloadStart = offset;

        // TMAP payload: 160 bytes mapping quarter-tracks to track indices
        // Initialize all to 0xFF (no track)
        for (int i = 0; i < 160; i++)
        {
            data[offset++] = 0xFF;
        }

        // Map quarter-tracks around each whole track, matching DiskArc layout:
        // For track N:
        //   - Quarter N*4-1 (if N>0): points to track N (bridge from previous)
        //   - Quarter N*4:   points to track N (whole track)
        //   - Quarter N*4+1: points to track N (adjacent quarter)
        //   - Quarter N*4+2: 0xFF (half-track not present)
        for (int track = 0; track < trackCount; track++)
        {
            int quarterBase = track * QuarterTracksPerTrack;

            // Bridge quarter from previous track
            if (track > 0)
            {
                data[payloadStart + quarterBase - 1] = (byte)track;
            }

            // Whole track and adjacent quarter
            data[payloadStart + quarterBase] = (byte)track;
            data[payloadStart + quarterBase + 1] = (byte)track;

            // Quarter N*4+2 stays 0xFF (half-track not present)
        }

        return offset;
    }

    /// <summary>
    /// Writes TRKS chunk (variable size: descriptors + track data).
    /// </summary>
    private int WriteTrksChunk(byte[] data, int offset, InternalDiskImage disk, int trackCount, 
        int[] trackByteLengths, int[] trackBlockCounts, int totalTrackBlocks)
    {
        // Chunk ID: "TRKS" (4 bytes)
        WriteAscii(data, ref offset, "TRKS");

        // TRKS chunk payload = everything from byte 256 to end of file
        // (descriptor table padded to byte 1536 + track data)
        const int TrackDataStartByte = 1536;
        int payloadStartByte = offset + 4; // After the size field
        int trksPayloadSize = (TrackDataStartByte - payloadStartByte) + (totalTrackBlocks * BlockSize);
        WriteUInt32LE(data, ref offset, (uint)trksPayloadSize);

        int payloadStart = offset;

        // Track table: 8 bytes per track
        // Block numbers are FILE-RELATIVE (measured from start of WOZ file)
        // Per spec: "The actual bit data begins at byte 1536 (block 3) of the WOZ file"
        int currentBlock = TrackDataStartByte / BlockSize; // Block 3 for standard layout

        for (int track = 0; track < trackCount; track++)
        {
            // Starting block (2 bytes, little-endian) - FILE-RELATIVE
            WriteUInt16LE(data, ref offset, (ushort)currentBlock);

            // Block count (2 bytes, little-endian)
            WriteUInt16LE(data, ref offset, (ushort)trackBlockCounts[track]);

            // Bit count (4 bytes, little-endian)
            WriteUInt32LE(data, ref offset, (uint)disk.TrackBitCounts[track]);

            currentBlock += trackBlockCounts[track];
        }

        // Pad track table to ensure track data starts at byte 1536
        int tableActualSize = trackCount * 8;
        int tablePadding = TrackDataStartByte - payloadStartByte - tableActualSize;
        offset += tablePadding;

        // Write track data
        for (int track = 0; track < trackCount; track++)
        {
            CircularBitBuffer trackBuffer = disk.Tracks[track];
            trackBuffer.BitPosition = 0;

            int bytesToWrite = trackByteLengths[track];
            int blocksToWrite = trackBlockCounts[track];

            // Write track bytes using ReadOctet (raw 8-bit read).
            // LatchNextByte must NOT be used here — it applies Apple II hardware
            // latch semantics that consume extra bits when bit 7 is clear,
            // destroying the raw bit stream for non-standard data.
            for (int i = 0; i < bytesToWrite; i++)
            {
                data[offset++] = trackBuffer.ReadOctet();
            }

            // Pad to block boundary
            int padding = (blocksToWrite * BlockSize) - bytesToWrite;
            offset += padding; // Already zeroed
        }

        return offset;
    }

    /// <summary>
    /// Writes ASCII string to buffer.
    /// </summary>
    private void WriteAscii(byte[] data, ref int offset, string text)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(text);
        Array.Copy(bytes, 0, data, offset, bytes.Length);
        offset += bytes.Length;
    }

    /// <summary>
    /// Writes string padded with spaces to fixed length.
    /// </summary>
    private void WriteStringPadded(byte[] data, ref int offset, string text, int length)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(text);
        int copyLen = Math.Min(bytes.Length, length);
        Array.Copy(bytes, 0, data, offset, copyLen);

        // Fill remaining with spaces
        for (int i = copyLen; i < length; i++)
        {
            data[offset + i] = 0x20; // Space
        }

        offset += length;
    }

    /// <summary>
    /// Writes 32-bit unsigned integer in little-endian format.
    /// </summary>
    private void WriteUInt32LE(byte[] data, ref int offset, uint value)
    {
        data[offset++] = (byte)(value & 0xFF);
        data[offset++] = (byte)((value >> 8) & 0xFF);
        data[offset++] = (byte)((value >> 16) & 0xFF);
        data[offset++] = (byte)((value >> 24) & 0xFF);
    }

    /// <summary>
    /// Writes 16-bit unsigned integer in little-endian format.
    /// </summary>
    private void WriteUInt16LE(byte[] data, ref int offset, ushort value)
    {
        data[offset++] = (byte)(value & 0xFF);
        data[offset++] = (byte)((value >> 8) & 0xFF);
    }

    /// <summary>
    /// Writes 32-bit unsigned integer in little-endian format at an absolute offset (non-advancing).
    /// </summary>
    private void WriteUInt32LE(byte[] data, int offset, uint value)
    {
        data[offset] = (byte)(value & 0xFF);
        data[offset + 1] = (byte)((value >> 8) & 0xFF);
        data[offset + 2] = (byte)((value >> 16) & 0xFF);
        data[offset + 3] = (byte)((value >> 24) & 0xFF);
    }
}
