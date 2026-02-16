// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Diagnostics;
using CommonUtil;
using DiskArc;
using DiskArc.Disk;
using static DiskArc.Defs;

namespace Pandowdy.EmuCore.DiskII.Importers;

/// <summary>
/// Imports sector-based disk images (DSK, DO, PO, 2MG) to internal format.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Sector Formats:</strong><br/>
/// Sector-based formats store logical 256-byte sectors without GCR encoding. These formats
/// don't contain the low-level disk structure, so it must be synthesized during import.
/// </para>
/// <para>
/// <strong>Import Process:</strong><br/>
/// Uses CiderPress2's sector access to read logical sectors, then synthesizes GCR-encoded
/// tracks using DiskArc's SectorCodec. The result is bit-perfect standard Apple II tracks
/// with proper address fields, data fields, gaps, and sync bytes.
/// </para>
/// <para>
/// <strong>Format Detection:</strong><br/>
/// Supports DOS sector order (.dsk, .do) and ProDOS sector order (.po). DiskArc automatically
/// handles sector ordering based on file extension or content analysis.
/// </para>
/// </remarks>
public class SectorImporter : IDiskImageImporter
{
    private const int NumTracks = 35;
    private const int SectorsPerTrack = 16;
    private const byte DefaultVolume = 254;
    private const int DefaultTrackLength = 6336; // Standard track length in bytes
    private const int Gap2Len = 5;  // Sync bytes between address and data fields
    private const int Gap3Len = 20; // Sync bytes between sectors

    /// <summary>
    /// DOS 3.3 physical-to-logical sector interleave table.
    /// Physical position P contains data from logical sector Dos33PhysicalToLogical[P].
    /// </summary>
    /// <remarks>
    /// This is the standard DOS 3.3 software interleave pattern. When synthesizing a track,
    /// we iterate through physical sector positions 0-15 and look up which logical sector's
    /// data belongs at that physical position.
    /// </remarks>
    private static readonly byte[] Dos33PhysicalToLogical =
        { 0, 7, 14, 6, 13, 5, 12, 4, 11, 3, 10, 2, 9, 1, 8, 15 };

    /// <summary>
    /// Supported file extensions for sector formats.
    /// </summary>
    public IReadOnlyList<string> SupportedExtensions { get; } = new[] { ".dsk", ".do", ".po", ".2mg" };

    /// <summary>
    /// Import a sector-based disk image file to internal format.
    /// </summary>
    /// <param name="filePath">Path to the disk image file.</param>
    /// <returns>Internal disk image representation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if filePath is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown if the file doesn't exist.</exception>
    /// <exception cref="InvalidDataException">Thrown if file format is invalid.</exception>
    public InternalDiskImage Import(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentNullException(nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Disk image file not found: {filePath}", filePath);
        }

        // Determine format from file extension
        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        DiskFormat format = extension switch
        {
            ".dsk" => DiskFormat.Dsk,
            ".do" => DiskFormat.Do,
            ".po" => DiskFormat.Po,
            ".2mg" => DiskFormat.Dsk, // 2MG is typically DOS order
            _ => DiskFormat.Unknown
        };

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return ImportFromStream(stream, format, filePath);
    }

    /// <summary>
    /// Import from a stream (for embedded disk images).
    /// </summary>
    /// <param name="stream">Stream containing disk image data.</param>
    /// <param name="format">Format of the disk image (Dsk, Do, or Po).</param>
    /// <returns>Internal disk image representation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if stream is null.</exception>
    /// <exception cref="InvalidDataException">Thrown if stream format is invalid.</exception>
    public InternalDiskImage Import(Stream stream, DiskFormat format)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (format != DiskFormat.Dsk && format != DiskFormat.Do && format != DiskFormat.Po)
        {
            throw new ArgumentException($"SectorImporter can only import DSK/DO/PO formats, got {format}", nameof(format));
        }

        return ImportFromStream(stream, format, sourcePath: null);
    }

    /// <summary>
    /// Internal import implementation shared by file and stream imports.
    /// </summary>
    private InternalDiskImage ImportFromStream(Stream stream, DiskFormat format, string? sourcePath)
    {
        // Open the disk image with CiderPress2
        IDiskImage diskImage;
        try
        {
            diskImage = UnadornedSector.OpenDisk(stream, new AppHook(new NullMessageLog()));
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"Failed to open disk image: {ex.Message}", ex);
        }

        try
        {
            // Analyze the disk to initialize ChunkAccess
            // Use DOS_Sector for .do/.dsk (matches legacy SectorDiskImageProvider behavior)
            // Use ProDOS_Block for .po
            SectorOrder sectorOrder = format == DiskFormat.Po ? SectorOrder.ProDOS_Block : SectorOrder.DOS_Sector;
            if (!diskImage.AnalyzeDisk(null, sectorOrder, IDiskImage.AnalysisDepth.ChunksOnly))
            {
                throw new InvalidDataException("Failed to analyze disk image");
            }

            if (diskImage.ChunkAccess == null)
            {
                throw new InvalidDataException("Unable to access disk sectors");
            }

            IChunkAccess chunkAccess = diskImage.ChunkAccess;

            // Verify it's a 5.25" disk with sectors
            if (!chunkAccess.HasSectors || chunkAccess.NumTracks != NumTracks)
            {
                throw new InvalidDataException(
                    $"Expected {NumTracks}-track 5.25\" disk, got {chunkAccess.NumTracks} tracks");
            }

            if (chunkAccess.NumSectorsPerTrack != SectorsPerTrack)
            {
                throw new InvalidDataException(
                    $"Expected {SectorsPerTrack} sectors per track, got {chunkAccess.NumSectorsPerTrack}");
            }

            // Get DiskArc's standard 16-sector codec for GCR encoding
            SectorCodec codec = StdSectorCodec.GetCodec(StdSectorCodec.CodecIndex525.Std_525_16);

            // Synthesize GCR tracks from sectors
            // Sector formats only have whole-track data, so we populate quarter-track indices 0, 4, 8, 12...
            int quarterTrackCount = InternalDiskImage.CalculateQuarterTrackCount(NumTracks);
            var quarterTracks = new CircularBitBuffer?[quarterTrackCount];
            var quarterTrackBitCounts = new int[quarterTrackCount];

            for (int track = 0; track < NumTracks; track++)
            {
                var (trackBuffer, bitCount) = SynthesizeTrack(track, chunkAccess, codec, format);

                // Store at quarter-track index (track * 4)
                int quarterIndex = InternalDiskImage.TrackToQuarterTrackIndex(track);
                quarterTracks[quarterIndex] = trackBuffer;
                quarterTrackBitCounts[quarterIndex] = bitCount;
            }

            Debug.WriteLine($"SectorImporter: Imported {format} disk image from {sourcePath ?? "(stream)"} ({NumTracks} tracks, {SectorsPerTrack} sectors/track)");

            return new InternalDiskImage(NumTracks, quarterTracks, quarterTrackBitCounts)
            {
                SourceFilePath = sourcePath,
                OriginalFormat = format,
                OptimalBitTiming = 32, // Standard timing for synthesized tracks
                IsWriteProtected = false // Can be changed after import
            };
        }
        finally
        {
            // Clean up DiskArc resources
            if (diskImage is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    /// <summary>
    /// Synthesizes a GCR-encoded track from logical sectors.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method generates a GCR track that matches what a real Disk II controller would see.
    /// The DOS 3.3 software interleave pattern is applied: physical sector positions on the
    /// track contain data from different logical sectors according to the Dos33PhysicalToLogical
    /// mapping.
    /// </para>
    /// <para>
    /// The address field in each sector contains the PHYSICAL sector number (what the controller
    /// reads to identify the sector), while the data field contains the LOGICAL sector's data
    /// (what the software requested).
    /// </para>
    /// </remarks>
    /// <returns>Tuple of (CircularBitBuffer track, bit count).</returns>
    private (CircularBitBuffer, int) SynthesizeTrack(int track, IChunkAccess chunkAccess, SectorCodec codec, DiskFormat format)
    {
        byte[] trackData = new byte[DefaultTrackLength];
        var buffer = new CircularBitBuffer(trackData, 0, 0, trackData.Length * 8);

        // Fill track with sync bytes first (gap 1 filler)
        // This fills the entire track with 0xFF, including the gap1 between last sector and wrap point
        buffer.Fill(0xFF, 8); // Byte-aligned (8 bits per byte)

        // Write sectors in physical order, applying DOS 3.3 interleave
        // Physical position P contains data from logical sector Dos33PhysicalToLogical[P]
        for (byte physicalSector = 0; physicalSector < SectorsPerTrack; physicalSector++)
        {
            byte logicalSector = Dos33PhysicalToLogical[physicalSector];

            // Gap 3 (inter-sector gap)
            for (int i = 0; i < Gap3Len; i++)
            {
                buffer.WriteByte(0xFF, 8);
            }

            // Address field - write the PHYSICAL sector number (what the controller reads)
            codec.WriteAddressField_525(buffer, DefaultVolume, (byte)track, physicalSector);

            // Gap 2 (address-to-data gap)
            for (int i = 0; i < Gap2Len; i++)
            {
                buffer.WriteByte(0xFF, 8);
            }

            // Data field prolog
            buffer.WriteOctets(codec.DataProlog);

            // Read the LOGICAL sector data from disk image
            byte[] sectorData = new byte[256];
            try
            {
                chunkAccess.ReadSector((uint)track, logicalSector, sectorData, 0);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading track {track} logical sector {logicalSector} (physical {physicalSector}): {ex.Message}");
                // Leave as zeros on error
            }

            // Encode sector data (6&2 encoding: 256 bytes -> 343 bytes including checksum)
            codec.EncodeSector62_256(buffer, buffer.BitPosition, sectorData, 0);

            // Data field epilog
            buffer.WriteOctets(codec.DataEpilog);
        }

        // Note: buffer.BitPosition is now ~49664 (16 sectors × ~3104 bits each)
        // The remaining bits (49664 to 50688) stay as 0xFF from the initial fill.
        // This trailing gap represents "gap1" on a real disk - the space between
        // the last sector and where the track wraps. This is intentional and required.

        // CRITICAL: Return the BUFFER's bitCount (50688), not buffer.BitPosition (~49664).
        // The CircularBitBuffer wraps at its construction-time bitCount, so TrackBitCounts
        // must match this value for consistent wrap behavior. This matches how NibImporter
        // works - it returns the buffer's BitCount, not a smaller "written data" count.
        int bitCount = buffer.BitCount;

        // Reset buffer position to start for reading
        buffer.BitPosition = 0;

        return (buffer, bitCount);
    }
}
