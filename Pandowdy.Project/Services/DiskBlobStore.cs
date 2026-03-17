// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.IO.Compression;
using System.IO.Hashing;
using CommonUtil;
using Pandowdy.EmuCore.DiskII;

namespace Pandowdy.Project.Services;

/// <summary>
/// Serializes and deserializes <see cref="InternalDiskImage"/> to/from compressed PIDI blob format.
/// </summary>
/// <remarks>
/// <para>
/// PIDI format (Pandowdy Internal Disk Image):
/// </para>
/// <para>
/// [Header] (uncompressed, 12 bytes)
///   4 bytes: magic ("PIDI")
///   2 bytes: format version (1)
///   1 byte:  compression method (0 = none, 1 = Deflate)
///   1 byte:  whole track count (typically 35; maps to InternalDiskImage.PhysicalTrackCount)
///   1 byte:  optimal bit timing
///   1 byte:  write protected flag
///   2 bytes: quarter-track count (little-endian uint16)
/// </para>
/// <para>
/// [Presence Bitmap] (uncompressed, ceil(quarter_track_count / 8) bytes)
///   1 bit per quarter-track position, LSB-first within each byte.
///   Bit = 1: quarter-track has data (non-null CircularBitBuffer).
///   Bit = 0: quarter-track is unwritten (null).
/// </para>
/// <para>
/// [Payload] (compressed via method specified in header)
///   Per non-null quarter-track, in index order:
///     4 bytes: bit count (little-endian int32)
///     4 bytes: byte count (little-endian int32)
///     N bytes: raw quarter-track data
/// </para>
/// <para>
/// [Footer] (uncompressed)
///   4 bytes: CRC-32 of header + presence bitmap + compressed payload
/// </para>
/// </remarks>
internal static class DiskBlobStore
{
    private static readonly byte[] s_magic = "PIDI"u8.ToArray(); // Pandowdy Internal Disk Image
    private const ushort FormatVersion = 1;
    private const byte CompressionMethodNone = 0;
    private const byte CompressionMethodDeflate = 1;
    private const int HeaderSize = 12;

    /// <summary>
    /// Serializes an <see cref="InternalDiskImage"/> to a compressed PIDI blob.
    /// </summary>
    public static byte[] Serialize(InternalDiskImage diskImage)
    {
        ArgumentNullException.ThrowIfNull(diskImage);

        var quarterTrackCount = diskImage.QuarterTrackCount;

        using var ms = new MemoryStream();

        // Write uncompressed header (12 bytes)
        ms.Write(s_magic);                                                       // 4 bytes: magic
        ms.Write(BitConverter.GetBytes(FormatVersion));                           // 2 bytes: version
        ms.WriteByte(CompressionMethodDeflate);                                  // 1 byte:  compression
        ms.WriteByte((byte)diskImage.PhysicalTrackCount);                        // 1 byte:  whole track count
        ms.WriteByte(diskImage.OptimalBitTiming);                                // 1 byte:  timing
        ms.WriteByte(diskImage.IsWriteProtected ? (byte)1 : (byte)0);           // 1 byte:  write protect
        ms.Write(BitConverter.GetBytes((ushort)quarterTrackCount));              // 2 bytes: quarter-track count

        // Write presence bitmap (uncompressed)
        var bitmapByteCount = (quarterTrackCount + 7) / 8;
        var presenceBitmap = new byte[bitmapByteCount];
        for (int i = 0; i < quarterTrackCount; i++)
        {
            if (diskImage.QuarterTracks[i] is not null)
            {
                presenceBitmap[i / 8] |= (byte)(1 << (i % 8));  // LSB-first
            }
        }
        ms.Write(presenceBitmap);

        // Compress payload (only non-null quarter-tracks)
        using (var deflateStream = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true))
        {
            for (int i = 0; i < quarterTrackCount; i++)
            {
                if (diskImage.QuarterTracks[i] is null)
                {
                    continue;
                }

                var bitCount = diskImage.QuarterTrackBitCounts[i];
                var byteCount = (bitCount + 7) / 8;

                deflateStream.Write(BitConverter.GetBytes(bitCount));
                deflateStream.Write(BitConverter.GetBytes(byteCount));

                // Extract raw bytes from CircularBitBuffer using public API only
                // Save the current position, read all bytes, then restore position
                var cbb = diskImage.QuarterTracks[i]!;
                int savedPosition = cbb.BitPosition;
                cbb.BitPosition = 0;

                var trackBytes = new byte[byteCount];
                for (int j = 0; j < byteCount; j++)
                {
                    trackBytes[j] = cbb.ReadOctet();
                }

                cbb.BitPosition = savedPosition;  // Restore original position

                deflateStream.Write(trackBytes);
            }
        }

        // Calculate CRC-32 over header + presence bitmap + compressed payload
        var dataForCrc = ms.ToArray();
        var crc = Crc32.Hash(dataForCrc);
        ms.Write(crc);

        return ms.ToArray();
    }

    /// <summary>
    /// Serializes a mounted <see cref="InternalDiskImage"/> using a snapshot-under-lock strategy.
    /// Acquires <see cref="InternalDiskImage.SerializationLock"/> briefly to copy raw quarter-track
    /// byte arrays (~1ms for a 35-track disk), then releases the lock and compresses the snapshot
    /// on the calling thread with no contention against the emulator write path.
    /// </summary>
    public static byte[] SerializeSnapshot(InternalDiskImage diskImage)
    {
        ArgumentNullException.ThrowIfNull(diskImage);

        var quarterTrackCount = diskImage.QuarterTrackCount;

        // Snapshot data under lock — fast memcpy, no compression
        byte physicalTrackCount;
        byte optimalBitTiming;
        byte writeProtected;
        var presenceBitmap = new byte[(quarterTrackCount + 7) / 8];
        var snapshotBitCounts = new int[quarterTrackCount];
        var snapshotData = new byte[quarterTrackCount][];

        lock (diskImage.SerializationLock)
        {
            physicalTrackCount = (byte)diskImage.PhysicalTrackCount;
            optimalBitTiming = diskImage.OptimalBitTiming;
            writeProtected = diskImage.IsWriteProtected ? (byte)1 : (byte)0;

            for (int i = 0; i < quarterTrackCount; i++)
            {
                var cbb = diskImage.QuarterTracks[i];
                if (cbb is null)
                {
                    continue;
                }

                presenceBitmap[i / 8] |= (byte)(1 << (i % 8));  // LSB-first
                snapshotBitCounts[i] = diskImage.QuarterTrackBitCounts[i];

                // Copy raw bytes from CircularBitBuffer
                var byteCount = (snapshotBitCounts[i] + 7) / 8;
                var trackBytes = new byte[byteCount];
                int savedPosition = cbb.BitPosition;
                cbb.BitPosition = 0;
                for (int j = 0; j < byteCount; j++)
                {
                    trackBytes[j] = cbb.ReadOctet();
                }
                cbb.BitPosition = savedPosition;
                snapshotData[i] = trackBytes;
            }
        }

        // Lock released — compress the snapshot at leisure
        using var ms = new MemoryStream();

        // Write uncompressed header (12 bytes)
        ms.Write(s_magic);
        ms.Write(BitConverter.GetBytes(FormatVersion));
        ms.WriteByte(CompressionMethodDeflate);
        ms.WriteByte(physicalTrackCount);
        ms.WriteByte(optimalBitTiming);
        ms.WriteByte(writeProtected);
        ms.Write(BitConverter.GetBytes((ushort)quarterTrackCount));

        // Write presence bitmap (uncompressed)
        ms.Write(presenceBitmap);

        // Compress payload from snapshot data
        using (var deflateStream = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true))
        {
            for (int i = 0; i < quarterTrackCount; i++)
            {
                if (snapshotData[i] is null)
                {
                    continue;
                }

                deflateStream.Write(BitConverter.GetBytes(snapshotBitCounts[i]));
                deflateStream.Write(BitConverter.GetBytes(snapshotData[i].Length));
                deflateStream.Write(snapshotData[i]);
            }
        }

        var dataForCrc = ms.ToArray();
        var crc = Crc32.Hash(dataForCrc);
        ms.Write(crc);

        return ms.ToArray();
    }

    /// <summary>
    /// Deserializes a PIDI blob to an <see cref="InternalDiskImage"/>.
    /// </summary>
    public static InternalDiskImage Deserialize(byte[] blob)
    {
        using var ms = new MemoryStream(blob);
        return Deserialize(ms);
    }

    /// <summary>
    /// Deserializes a PIDI blob stream to an <see cref="InternalDiskImage"/>.
    /// </summary>
    public static InternalDiskImage Deserialize(Stream stream)
    {
        // Read and validate header (12 bytes)
        Span<byte> magic = stackalloc byte[4];
        stream.ReadExactly(magic);
        if (!magic.SequenceEqual(s_magic))
        {
            throw new InvalidDataException("Invalid PIDI magic bytes.");
        }

        Span<byte> versionBytes = stackalloc byte[2];
        stream.ReadExactly(versionBytes);
        var version = BitConverter.ToUInt16(versionBytes);
        if (version != FormatVersion)
        {
            throw new InvalidDataException($"Unsupported PIDI format version: {version}");
        }

        var compressionMethod = (byte)stream.ReadByte();
        var wholeTrackCount = (byte)stream.ReadByte();
        var optimalBitTiming = (byte)stream.ReadByte();
        var isWriteProtected = stream.ReadByte() != 0;

        Span<byte> qtCountBytes = stackalloc byte[2];
        stream.ReadExactly(qtCountBytes);
        var quarterTrackCount = BitConverter.ToUInt16(qtCountBytes);

        // Read presence bitmap (uncompressed)
        var bitmapByteCount = (quarterTrackCount + 7) / 8;
        var presenceBitmap = new byte[bitmapByteCount];
        stream.ReadExactly(presenceBitmap);

        // Read compressed payload + CRC footer
        var compressedPayloadStart = stream.Position;
        var remainingBytes = stream.Length - compressedPayloadStart;
        if (remainingBytes < 4)
        {
            throw new InvalidDataException("PIDI blob is too short to contain CRC footer.");
        }

        // Read entire blob for CRC validation
        stream.Position = 0;
        var blobData = new byte[stream.Length];
        stream.ReadExactly(blobData);

        // Validate CRC-32 (last 4 bytes are CRC, everything before is covered)
        var storedCrc = blobData[^4..];
        var dataForCrc = blobData[..^4];
        var computedCrc = Crc32.Hash(dataForCrc);
        if (!storedCrc.SequenceEqual(computedCrc))
        {
            throw new InvalidDataException("PIDI blob CRC-32 validation failed.");
        }

        // Decompress payload and reconstruct quarter-tracks
        var compressedPayloadLength = (int)(blobData.Length - compressedPayloadStart - 4);

        using var compressedStream = new MemoryStream(blobData, (int)compressedPayloadStart, compressedPayloadLength);
        Stream decompressStream;
        if (compressionMethod == CompressionMethodDeflate)
        {
            decompressStream = new DeflateStream(compressedStream, CompressionMode.Decompress);
        }
        else
        {
            // method 0 = uncompressed (reserved for diagnostics)
            decompressStream = compressedStream;
        }

        using (decompressStream)
        {
            Span<byte> intBuffer = stackalloc byte[4];

            // Allocate quarter-track arrays for reconstruction
            var quarterTracks = new CircularBitBuffer?[quarterTrackCount];
            var quarterTrackBitCounts = new int[quarterTrackCount];

            for (int i = 0; i < quarterTrackCount; i++)
            {
                // Check presence bitmap (LSB-first): bit = 0 means unwritten (null)
                bool isPresent = (presenceBitmap[i / 8] & (1 << (i % 8))) != 0;
                if (!isPresent)
                {
                    continue;
                }

                decompressStream.ReadExactly(intBuffer);
                var bitCount = BitConverter.ToInt32(intBuffer);

                decompressStream.ReadExactly(intBuffer);
                var byteCount = BitConverter.ToInt32(intBuffer);

                var trackData = new byte[byteCount];
                decompressStream.ReadExactly(trackData);

                quarterTrackBitCounts[i] = bitCount;
                quarterTracks[i] = new CircularBitBuffer(trackData, 0, 0, bitCount);
            }

            // Reconstruct InternalDiskImage with wholeTrackCount mapped to PhysicalTrackCount
            return new InternalDiskImage(wholeTrackCount, quarterTracks, quarterTrackBitCounts)
            {
                OptimalBitTiming = optimalBitTiming,
                IsWriteProtected = isWriteProtected
            };
        }
    }
}

