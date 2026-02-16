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
internal static class DiskBlobStore
{
    private static readonly byte[] s_magic = "PIDI"u8.ToArray(); // Pandowdy Internal Disk Image
    private const ushort FormatVersion = 1;
    private const byte CompressionMethodDeflate = 1;

    /// <summary>
    /// Serializes an <see cref="InternalDiskImage"/> to a compressed PIDI blob.
    /// </summary>
    public static byte[] Serialize(InternalDiskImage diskImage)
    {
        using var ms = new MemoryStream();

        // Write uncompressed header (10 bytes)
        ms.Write(s_magic);
        ms.Write(BitConverter.GetBytes(FormatVersion));
        ms.WriteByte(CompressionMethodDeflate);
        ms.WriteByte((byte)diskImage.TrackCount);
        ms.WriteByte(diskImage.OptimalBitTiming);
        ms.WriteByte(diskImage.IsWriteProtected ? (byte)1 : (byte)0);

        // Compress payload
        var payloadStart = (int)ms.Position;
        using (var deflateStream = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true))
        {
            for (int i = 0; i < diskImage.TrackCount; i++)
            {
                var bitCount = diskImage.TrackBitCounts[i];
                var byteCount = (bitCount + 7) / 8;

                deflateStream.Write(BitConverter.GetBytes(bitCount));
                deflateStream.Write(BitConverter.GetBytes(byteCount));

                // TODO: Access CircularBitBuffer underlying byte array
                // For Phase 1, this is a placeholder that will be completed when disk import is implemented in Phase 2
                throw new NotImplementedException("DiskBlobStore.Serialize requires CircularBitBuffer byte array access (Phase 2)");
            }
        }

        // Calculate CRC-32 over header + compressed payload
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
        // Read and validate header
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
        var trackCount = (byte)stream.ReadByte();
        var optimalBitTiming = (byte)stream.ReadByte();
        var isWriteProtected = stream.ReadByte() != 0;

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

        // Validate CRC-32 (last 4 bytes)
        var storedCrc = blobData[^4..];
        var dataForCrc = blobData[..^4];
        var computedCrc = Crc32.Hash(dataForCrc);
        if (!storedCrc.SequenceEqual(computedCrc))
        {
            throw new InvalidDataException("PIDI blob CRC-32 validation failed.");
        }

        // Decompress payload
        var compressedPayloadLength = (int)(blobData.Length - compressedPayloadStart - 4);
        var tracks = new CircularBitBuffer[trackCount];
        var trackBitCounts = new int[trackCount];

        using (var compressedStream = new MemoryStream(blobData, (int)compressedPayloadStart, compressedPayloadLength))
        {
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

                for (int i = 0; i < trackCount; i++)
                {
                    decompressStream.ReadExactly(intBuffer);
                    var bitCount = BitConverter.ToInt32(intBuffer);

                    decompressStream.ReadExactly(intBuffer);
                    var byteCount = BitConverter.ToInt32(intBuffer);

                    var trackData = new byte[byteCount];
                    decompressStream.ReadExactly(trackData);

                    trackBitCounts[i] = bitCount;
                    tracks[i] = new CircularBitBuffer(trackData, 0, 0, bitCount);
                }
            }
        }

        return new InternalDiskImage(trackCount)
        {
            OptimalBitTiming = optimalBitTiming,
            IsWriteProtected = isWriteProtected
            // TODO: Set Tracks and TrackBitCounts arrays - requires InternalDiskImage API review (Phase 2)
        };
    }
}

