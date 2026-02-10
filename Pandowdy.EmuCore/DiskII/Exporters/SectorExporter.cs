// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Diagnostics;
using CommonUtil;
using DiskArc;
using DiskArc.Disk;
using static DiskArc.Defs;

namespace Pandowdy.EmuCore.DiskII.Exporters;

/// <summary>
/// Exports internal disk image format to sector-based formats (DSK, DO, PO).
/// </summary>
/// <remarks>
/// <para>
/// <strong>Sector Export Process:</strong><br/>
/// This exporter decodes GCR-encoded track data back to 256-byte logical sectors.
/// It scans each track for sector address and data fields, decodes the 6&amp;2 encoded
/// data, and writes the sectors in the appropriate order for the target format.
/// </para>
/// <para>
/// <strong>Lossy Export:</strong><br/>
/// This export is LOSSY for copy-protected disks. If a track contains non-standard
/// GCR encoding, missing sectors, or invalid checksums, the export may fail or produce
/// incorrect data. Only standard DOS 3.3/ProDOS formatted disks can be reliably exported.
/// </para>
/// <para>
/// <strong>Sector Ordering:</strong><br/>
/// - <strong>.dsk/.do</strong>: DOS sector order (logical sectors written in physical order with DOS 3.3 interleave)
/// - <strong>.po</strong>: ProDOS sector order (logical sectors 0-15 written sequentially)
/// </para>
/// </remarks>
public class SectorExporter : IDiskImageExporter
{
    private const int NumTracks = 35;
    private const int SectorsPerTrack = 16;
    private const int SectorSize = 256;
    private const int ExpectedFileSize = NumTracks * SectorsPerTrack * SectorSize; // 143,360 bytes

    /// <summary>
    /// DOS 3.3 physical-to-logical sector interleave table.
    /// Physical position P contains data from logical sector Dos33PhysicalToLogical[P].
    /// For export, we need the inverse: LogicalToPhysical.
    /// </summary>
    private static readonly byte[] Dos33PhysicalToLogical =
        { 0, 7, 14, 6, 13, 5, 12, 4, 11, 3, 10, 2, 9, 1, 8, 15 };

    /// <summary>
    /// DOS 3.3 logical-to-physical sector mapping (inverse of PhysicalToLogical).
    /// To find where logical sector L is stored physically, use Dos33LogicalToPhysical[L].
    /// </summary>
    private static readonly byte[] Dos33LogicalToPhysical;

    static SectorExporter()
    {
        // Build inverse mapping: logical sector -> physical position
        Dos33LogicalToPhysical = new byte[16];
        for (byte physical = 0; physical < 16; physical++)
        {
            byte logical = Dos33PhysicalToLogical[physical];
            Dos33LogicalToPhysical[logical] = physical;
        }
    }

    /// <summary>
    /// Format this exporter produces.
    /// </summary>
    public DiskFormat OutputFormat { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SectorExporter"/> class.
    /// </summary>
    /// <param name="outputFormat">Target format (Dsk, Do, or Po).</param>
    /// <exception cref="ArgumentException">Thrown if format is not a sector-based format.</exception>
    public SectorExporter(DiskFormat outputFormat)
    {
        if (outputFormat != DiskFormat.Dsk && outputFormat != DiskFormat.Do && outputFormat != DiskFormat.Po)
        {
            throw new ArgumentException(
                $"SectorExporter only supports Dsk, Do, or Po formats, got {outputFormat}",
                nameof(outputFormat));
        }

        OutputFormat = outputFormat;
    }

    /// <summary>
    /// Export internal format to file.
    /// </summary>
    /// <param name="disk">Internal disk image to export.</param>
    /// <param name="filePath">Path to write the disk image file.</param>
    /// <exception cref="ArgumentNullException">Thrown if disk or filePath is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when GCR decoding fails.</exception>
    /// <exception cref="IOException">Thrown when file write fails.</exception>
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

        // Decode all tracks to sector data
        byte[] sectorData = DecodeDiskToSectors(disk);

        // Write to file
        try
        {
            File.WriteAllBytes(filePath, sectorData);
            Debug.WriteLine($"SectorExporter: Exported {OutputFormat} to {filePath} ({sectorData.Length} bytes)");
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to write disk image to {filePath}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Export to stream (for embedding in project files).
    /// </summary>
    /// <param name="disk">Internal disk image to export.</param>
    /// <param name="stream">Stream to write the disk image data.</param>
    /// <exception cref="ArgumentNullException">Thrown if disk or stream is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when GCR decoding fails.</exception>
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

        // Decode all tracks to sector data
        byte[] sectorData = DecodeDiskToSectors(disk);

        // Write to stream
        try
        {
            stream.Write(sectorData, 0, sectorData.Length);
            Debug.WriteLine($"SectorExporter: Exported {OutputFormat} to stream ({sectorData.Length} bytes)");
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to write disk image to stream: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Decodes the entire disk to sector data.
    /// </summary>
    /// <returns>Byte array containing all sectors in the target format's order.</returns>
    private byte[] DecodeDiskToSectors(InternalDiskImage disk)
    {
        int trackCount = Math.Min(disk.TrackCount, NumTracks);
        byte[] outputData = new byte[ExpectedFileSize];

        // Get DiskArc's standard 16-sector codec for GCR decoding
        SectorCodec codec = StdSectorCodec.GetCodec(StdSectorCodec.CodecIndex525.Std_525_16);

        // Decode each track
        for (int track = 0; track < trackCount; track++)
        {
            DecodeTrack(disk, track, codec, outputData);
        }

        return outputData;
    }

    /// <summary>
    /// Decodes a single track to sector data.
    /// </summary>
    private void DecodeTrack(InternalDiskImage disk, int track, SectorCodec codec, byte[] outputData)
    {
        CircularBitBuffer trackBuffer = disk.Tracks[track];
        trackBuffer.BitPosition = 0; // Start from beginning

        // Decode all 16 sectors from this track
        bool[] sectorsFound = new bool[SectorsPerTrack];
        byte[][] decodedSectors = new byte[SectorsPerTrack][];

        // Scan the track for all sectors
        int maxScans = trackBuffer.BitCount * 2; // Safety limit: scan track twice
        int scansRemaining = maxScans;

        while (sectorsFound.Any(found => !found) && scansRemaining > 0)
        {
            scansRemaining--;

            // Search for address field
            if (!FindAddressField(trackBuffer, codec, out byte foundTrack, out byte foundSector))
            {
                // No more address fields found
                break;
            }

            // Verify track number matches
            if (foundTrack != track)
            {
                Debug.WriteLine($"SectorExporter: Track mismatch at track {track}: found {foundTrack}");
                continue;
            }

            // Verify sector number is valid
            if (foundSector >= SectorsPerTrack)
            {
                Debug.WriteLine($"SectorExporter: Invalid sector number {foundSector} on track {track}");
                continue;
            }

            // Skip if we already decoded this sector
            if (sectorsFound[foundSector])
            {
                continue;
            }

            // Read the data field
            byte[] sectorData = new byte[SectorSize];
            if (ReadDataField(trackBuffer, codec, sectorData))
            {
                // Map physical sector to logical sector using DOS 3.3 interleave
                byte logicalSector = Dos33PhysicalToLogical[foundSector];
                decodedSectors[logicalSector] = sectorData;
                sectorsFound[logicalSector] = true;
                
                Debug.WriteLine($"SectorExporter: Track {track} physical sector {foundSector} -> logical sector {logicalSector} decoded");
            }
            else
            {
                Debug.WriteLine($"SectorExporter: Failed to decode data field for track {track} sector {foundSector}");
            }
        }

        // Check if all sectors were found
        for (byte sector = 0; sector < SectorsPerTrack; sector++)
        {
            if (!sectorsFound[sector])
            {
                Debug.WriteLine($"SectorExporter: WARNING - Track {track} logical sector {sector} not found, filling with zeros");
                decodedSectors[sector] = new byte[SectorSize]; // Fill with zeros
            }
        }

        // Write sectors to output in the correct order based on format
        WriteSectorsToOutput(track, decodedSectors, outputData);
    }

    /// <summary>
    /// Searches for the next address field in the track.
    /// </summary>
    /// <returns>True if address field found and decoded successfully.</returns>
    private bool FindAddressField(CircularBitBuffer buffer, SectorCodec codec, out byte track, out byte sector)
    {
        track = 0;
        sector = 0;

        int maxBytesToSearch = buffer.BitCount / 8;
        int bytesSearched = 0;

        // Address prolog for 5.25" disks: D5 AA 96
        byte[] addressProlog = { 0xD5, 0xAA, 0x96 };

        // Search for address prolog (D5 AA 96)
        while (bytesSearched < maxBytesToSearch)
        {
            // Read next byte
            byte b = buffer.LatchNextByte();
            bytesSearched++;

            // Check if this is the start of address prolog
            if (b == addressProlog[0])
            {
                // Read next two bytes
                byte b1 = buffer.LatchNextByte();
                byte b2 = buffer.LatchNextByte();
                bytesSearched += 2;

                if (b1 == addressProlog[1] && b2 == addressProlog[2])
                {
                    // Found address field prolog, read header
                    byte vol = buffer.LatchNextByte();
                    track = buffer.LatchNextByte();
                    sector = buffer.LatchNextByte();
                    byte checksum = buffer.LatchNextByte();
                    bytesSearched += 4;

                    // Decode using 4&4 encoding
                    // In 4&4 encoding, a byte value VVVVVVVV is encoded as two bytes:
                    // - Odd byte:  1VVV1VVV (bits 7,5,3,1 of original value)
                    // - Even byte: 1VVV1VVV (bits 6,4,2,0 of original value)
                    // To decode: extract lower 2 bits from odd byte, shift left, OR with lower 2 bits from even byte
                    vol = (byte)((vol & 0xAA) | ((vol >> 1) & 0x55));
                    track = (byte)((track & 0xAA) | ((track >> 1) & 0x55));
                    sector = (byte)((sector & 0xAA) | ((sector >> 1) & 0x55));
                    byte decodedChecksum = (byte)((checksum & 0xAA) | ((checksum >> 1) & 0x55));

                    // Verify checksum (XOR of vol, track, sector)
                    byte computedChecksum = (byte)(vol ^ track ^ sector);
                    if (computedChecksum == decodedChecksum)
                    {
                        return true;
                    }

                    Debug.WriteLine($"SectorExporter: Address field checksum mismatch (computed: {computedChecksum:X2}, expected: {decodedChecksum:X2})");
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Reads and decodes a data field from the current position.
    /// </summary>
    private bool ReadDataField(CircularBitBuffer buffer, SectorCodec codec, byte[] sectorData)
    {
        // Search for data prolog (D5 AA AD)
        byte[] dataProlog = codec.DataProlog;
        int maxBytesToSearch = 100; // Gap between address and data should be small

        for (int i = 0; i < maxBytesToSearch; i++)
        {
            byte b = buffer.LatchNextByte();

            // Check if this is the start of data prolog
            if (b == dataProlog[0])
            {
                byte b1 = buffer.LatchNextByte();
                byte b2 = buffer.LatchNextByte();

                if (b1 == dataProlog[1] && b2 == dataProlog[2])
                {
                    // Found data field prolog
                    try
                    {
                        // DiskArc's DecodeSector62_256 reads from current position
                        // It expects the bitPosition to be right after the prolog
                        codec.DecodeSector62_256(buffer, buffer.BitPosition, sectorData, 0);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"SectorExporter: Failed to decode 6&2 data: {ex.Message}");
                        return false;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Writes decoded sectors to the output buffer in the correct order.
    /// </summary>
    private void WriteSectorsToOutput(int track, byte[][] decodedSectors, byte[] outputData)
    {
        for (int logicalSector = 0; logicalSector < SectorsPerTrack; logicalSector++)
        {
            int outputOffset;

            if (OutputFormat == DiskFormat.Po)
            {
                // ProDOS order: logical sectors written sequentially
                outputOffset = (track * SectorsPerTrack + logicalSector) * SectorSize;
            }
            else
            {
                // DOS order (.dsk, .do): need to apply DOS sector ordering
                // In DOS order files, the sectors are stored in physical order
                byte physicalSector = Dos33LogicalToPhysical[logicalSector];
                outputOffset = (track * SectorsPerTrack + physicalSector) * SectorSize;
            }

            // Copy sector data to output
            Array.Copy(decodedSectors[logicalSector], 0, outputData, outputOffset, SectorSize);
        }
    }
}
