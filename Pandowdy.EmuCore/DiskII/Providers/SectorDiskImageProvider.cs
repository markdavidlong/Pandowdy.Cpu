// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Diagnostics;
using CommonUtil;
using DiskArc;
using DiskArc.Disk;
using Pandowdy.EmuCore.Machine;
using static DiskArc.Defs;

namespace Pandowdy.EmuCore.DiskII.Providers;

/// <summary>
/// Provides disk image data from sector-based formats (DSK, DO, PO, 2MG).
/// </summary>
/// <remarks>
/// <para>
/// This provider uses CiderPress2's DiskArc library to read logical sectors from
/// sector-based disk images. Since these formats don't contain GCR-encoded data,
/// it synthesizes disk tracks on demand using DiskArc's <see cref="TrackInit"/> and
/// <see cref="SectorCodec"/> classes.
/// </para>
/// <para>
/// <strong>Track Synthesis:</strong><br/>
/// When software accesses a track, this provider generates GCR-encoded data that matches
/// what would appear on a physical disk. The synthesized tracks are cached to avoid
/// repeated encoding overhead. Each track contains 16 sectors with proper address/data
/// fields, gaps, and sync bytes encoded by DiskArc's battle-tested encoder.
/// </para>
/// <para>
/// <strong>Bit-Level Access:</strong><br/>
/// Track data is accessed via <see cref="CircularBitBuffer"/>, providing the same
/// interface as <see cref="WozDiskImageProvider"/> and <see cref="NibDiskImageProvider"/>.
/// </para>
/// </remarks>
public class SectorDiskImageProvider : IDiskImageProvider, IDisposable
{
    private readonly Stream _stream;
    private readonly IDiskImage _diskImage;
    private readonly IChunkAccess _chunkAccess;
    private readonly SectorCodec _codec;
    private readonly CircularBitBuffer?[] _trackCache;
    private readonly int[] _trackBitCounts;

    private int _currentQuarterTrack;
    private bool _isWriteProtected;
    private bool _disposed;

    private const int NumTracks = 35;
    private const int SectorsPerTrack = 16;
    private const byte DefaultVolume = 254;

    /// <summary>
    /// DOS 3.3 physical-to-logical sector interleave table.
    /// Physical position P contains data from logical sector PhysicalToLogical[P].
    /// </summary>
    private static readonly byte[] Dos33PhysicalToLogical =
        { 0, 7, 14, 6, 13, 5, 12, 4, 11, 3, 10, 2, 9, 1, 8, 15 };

    /// <summary>
    /// Gets the file path of the disk image.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets whether this image supports write operations.
    /// </summary>
    public bool IsWritable => !_chunkAccess.IsReadOnly;

    /// <summary>
    /// Gets or sets whether the disk is write-protected.
    /// </summary>
    public bool IsWriteProtected
    {
        get => _isWriteProtected;
        set => _isWriteProtected = value;
    }

    /// <summary>
    /// Gets the current quarter-track position.
    /// </summary>
    public int CurrentQuarterTrack => _currentQuarterTrack;

    /// <summary>
    /// Gets the optimal bit timing for this disk image.
    /// Sector-based formats have no timing information, so always returns the default 32 (4µs/bit).
    /// </summary>
    public byte OptimalBitTiming => 32;

    /// <summary>
    /// Gets the number of bits on the current track.
    /// </summary>
    public int CurrentTrackBitCount
    {
        get
        {
            int track = _currentQuarterTrack / 4;
            if (track >= 0 && track < NumTracks && _trackBitCounts[track] > 0)
            {
                return _trackBitCounts[track];
            }
            return DiskIIConstants.BitsPerTrack;
        }
    }

    /// <summary>
    /// Gets the current bit position within the track.
    /// </summary>
    public int TrackBitPosition
    {
        get
        {
            int track = _currentQuarterTrack / 4;
            if (track >= 0 && track < NumTracks)
            {
                var buffer = _trackCache[track];
                return buffer?.BitPosition ?? 0;
            }
            return 0;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SectorDiskImageProvider"/> class.
    /// </summary>
    /// <param name="filePath">Path to the disk image file (DSK, DO, PO, 2MG).</param>
    public SectorDiskImageProvider(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentNullException(nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Disk image file not found: {filePath}", filePath);
        }

        FilePath = filePath;

        // Open the file with CiderPress2 - use Read access with ReadWrite sharing for concurrent test access
        // FileAccess.ReadWrite can cause locking issues during parallel test execution
        _stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        try
        {
            // Use UnadornedSector for simple DSK/DO/PO images
            _diskImage = UnadornedSector.OpenDisk(_stream, new CommonUtil.AppHook(null!));

            // Analyze the disk to initialize ChunkAccess
            // For DOS 3.3 disks, use DOS sector order
            if (!_diskImage.AnalyzeDisk(null, SectorOrder.DOS_Sector, IDiskImage.AnalysisDepth.ChunksOnly))
            {
                throw new InvalidDataException("Failed to analyze disk image");
            }

            if (_diskImage.ChunkAccess == null)
            {
                throw new InvalidDataException("Unable to access disk sectors");
            }

            _chunkAccess = _diskImage.ChunkAccess;

            // Verify it's a 5.25" disk with sectors
            if (!_chunkAccess.HasSectors || _chunkAccess.NumTracks != NumTracks)
            {
                throw new InvalidDataException(
                    $"Expected {NumTracks}-track 5.25\" disk, got {_chunkAccess.NumTracks} tracks");
            }

            if (_chunkAccess.NumSectorsPerTrack != SectorsPerTrack)
            {
                throw new InvalidDataException(
                    $"Expected {SectorsPerTrack} sectors per track, got {_chunkAccess.NumSectorsPerTrack}");
            }
        }
        catch
        {
            _stream?.Dispose();
            throw;
        }

        // Use DiskArc's standard 16-sector codec for GCR encoding
        _codec = StdSectorCodec.GetCodec(StdSectorCodec.CodecIndex525.Std_525_16);
        _trackCache = new CircularBitBuffer?[NumTracks];
        _trackBitCounts = new int[NumTracks];

        Debug.WriteLine($"Loaded sector disk image: {filePath} ({NumTracks} tracks, {SectorsPerTrack} sectors/track)");
    }

    /// <summary>
    /// Notifies the provider of motor state changes.
    /// </summary>
    /// <param name="motorOn">True if motor is turning on, false if turning off.</param>
    /// <param name="cycleCount">Current CPU cycle count when state changed.</param>
    /// <remarks>
    /// SectorDiskImageProvider doesn't track rotational position, so this is a no-op.
    /// </remarks>
    public void NotifyMotorStateChanged(bool motorOn, ulong cycleCount)
    {
        // Sector provider doesn't use cycle-based timing - no action needed
    }

    /// <summary>
    /// Sets the current quarter-track position.
    /// </summary>
    public void SetQuarterTrack(int qTrack)
    {
        if (_currentQuarterTrack != qTrack)
        {
            _currentQuarterTrack = qTrack;
            int track = qTrack / 4;
            Debug.WriteLine($"SectorDiskImageProvider: Disk head moved to track {track} (quarter-track {qTrack})");
        }
    }

    /// <summary>
    /// Reads the next bit from the current track.
    /// </summary>
    public bool? GetBit(ulong cycleCount)
    {
        // Convert quarter-track to full track
        int track = _currentQuarterTrack / 4;
        if (track < 0 || track >= NumTracks)
        {
            return null;
        }

        // Get or synthesize the track
        CircularBitBuffer? buffer = GetOrSynthesizeTrack(track);
        if (buffer == null)
        {
            return null;
        }

        // Read the next bit
        byte bit = buffer.LatchNextByte();
        return (bit & 0x80) != 0; // Return high bit of latched byte
    }

    /// <summary>
    /// Writes a bit to the current track (not yet implemented).
    /// </summary>
    public bool WriteBit(bool bit, ulong cycleCount)
    {
        // Writing to sector-based images requires:
        // 1. Capturing written bits into a track buffer
        // 2. Decoding GCR back to sectors
        // 3. Writing sectors back via IChunkAccess
        // This is complex - start with read-only support
        return false;
    }

    /// <summary>
    /// Advances the disk by the specified number of CPU cycles and returns bits read.
    /// </summary>
    /// <param name="elapsedCycles">CPU cycles elapsed since last call.</param>
    /// <param name="bits">Buffer to receive the bits read.</param>
    /// <returns>Number of bits actually read.</returns>
    public int AdvanceAndReadBits(double elapsedCycles, Span<bool> bits)
    {
        const double cyclesPerBit = 32.0 / 8.0; // Default timing (4µs/bit at 1MHz)
        int bitsToRead = (int)(elapsedCycles / cyclesPerBit);
        bitsToRead = Math.Min(bitsToRead, bits.Length);

        int track = _currentQuarterTrack / 4;
        if (track < 0 || track >= NumTracks)
        {
            return 0;
        }

        // Get or synthesize the track
        CircularBitBuffer? buffer = GetOrSynthesizeTrack(track);
        if (buffer == null)
        {
            return 0;
        }

        for (int i = 0; i < bitsToRead; i++)
        {
            bits[i] = buffer.ReadNextBit() == 1;
        }

        return bitsToRead;
    }

    /// <summary>
    /// Flushes any pending writes to disk.
    /// </summary>
    public void Flush()
    {
        // Currently read-only, nothing to flush
    }

    /// <summary>
    /// Gets or synthesizes a track, caching the result.
    /// </summary>
    private CircularBitBuffer? GetOrSynthesizeTrack(int track)
    {
        if (track < 0 || track >= NumTracks)
        {
            return null;
        }

        if (_trackCache[track] == null)
        {
            SynthesizeTrack(track);
        }

        return _trackCache[track];
    }

    /// <summary>
    /// Synthesizes a GCR-encoded track from logical sectors using DiskArc's encoder.
    /// </summary>
    /// <remarks>
    /// This method generates a GCR track structure directly, writing the actual sector data
    /// during construction rather than filling with zeros and patching afterward.
    /// The structure matches what TrackInit.GenerateTrack525_16 produces:
    /// - Gap 3 (20 self-sync bytes) before each sector
    /// - Address field (14 bytes)
    /// - Gap 2 (5 self-sync bytes)
    /// - Data field prolog (3 bytes) + encoded data (343 bytes) + epilog (3 bytes)
    /// </remarks>
    private void SynthesizeTrack(int track)
    {
        const int DefaultTrackLength = 6336; // Standard track length in bytes
        const int Gap2Len = 5;
        const int Gap3Len = 20;

        byte[] trackData = new byte[DefaultTrackLength];
        var buffer = new CircularBitBuffer(trackData, 0, 0, trackData.Length * 8);

        // Fill track with sync bytes first (gap 1 filler)
        buffer.Fill(0xFF, 8); // Byte-aligned (8 bits per byte)

        // Write sectors in physical order, applying DOS 3.3 interleave
        // Physical position P contains data from logical sector PhysicalToLogical[P]
        for (byte physicalSector = 0; physicalSector < SectorsPerTrack; physicalSector++)
        {
            byte logicalSector = Dos33PhysicalToLogical[physicalSector];

            // Gap 3 (inter-sector gap)
            for (int i = 0; i < Gap3Len; i++)
            {
                buffer.WriteByte(0xFF, 8);
            }

            // Address field uses PHYSICAL sector number (what the drive head sees)
            _codec.WriteAddressField_525(buffer, DefaultVolume, (byte)track, physicalSector);

            // Gap 2 (address-to-data gap)
            for (int i = 0; i < Gap2Len; i++)
            {
                buffer.WriteByte(0xFF, 8);
            }

            // Data field prolog
            buffer.WriteOctets(_codec.DataProlog);

            // Read the LOGICAL sector data from disk image
            byte[] sectorData = new byte[256];
            try
            {
                _chunkAccess.ReadSector((uint)track, logicalSector, sectorData, 0);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading track {track} logical sector {logicalSector} (physical {physicalSector}): {ex.Message}");
                // Leave as zeros on error
            }

            // Encode sector data (6&2 encoding: 256 bytes -> 343 bytes including checksum)
            _codec.EncodeSector62_256(buffer, buffer.BitPosition, sectorData, 0);

            // Data field epilog
            buffer.WriteOctets(_codec.DataEpilog);
        }

        int bitCount = buffer.BitPosition;

        // Reset buffer position to start for reading
        buffer.BitPosition = 0;

        _trackCache[track] = buffer;
        _trackBitCounts[track] = bitCount;

        Debug.WriteLine($"Synthesized track {track}: {bitCount} bits ({trackData.Length} bytes)");
    }

    /// <summary>
    /// Disposes resources used by this provider.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            // Dispose the disk image first (CiderPress2 requires this before stream disposal)
            if (_diskImage is IDisposable disposableDisk)
            {
                disposableDisk.Dispose();
            }

            _stream?.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
