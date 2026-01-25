using System.Diagnostics;
using DiskArc;
using DiskArc.Disk;
using Pandowdy.EmuCore.Interfaces;
using static DiskArc.Defs;

namespace Pandowdy.EmuCore.DiskII.Providers;

/// <summary>
/// Provides disk image data from sector-based formats (DSK, DO, PO, 2MG).
/// </summary>
/// <remarks>
/// <para>
/// This provider uses CiderPress2's DiskArc library to read logical sectors from
/// sector-based disk images. Since these formats don't contain GCR-encoded data,
/// it synthesizes disk tracks on demand using <see cref="GcrEncoder"/>.
/// </para>
/// <para>
/// <strong>Track Synthesis:</strong><br/>
/// When software accesses a track, this provider generates GCR-encoded data that matches
/// what would appear on a physical disk. The synthesized tracks are cached to avoid
/// repeated encoding overhead. Each track contains 16 sectors with proper address/data
/// fields, gaps, and sync bytes.
/// </para>
/// <para>
/// <strong>Performance:</strong><br/>
/// Track synthesis is relatively fast (~1ms per track), and caching ensures each track
/// is only synthesized once. This allows the ROM boot code to operate normally while
/// providing good performance.
/// </para>
/// </remarks>
public class SectorDiskImageProvider : IDiskImageProvider, IDisposable
{
    private readonly Stream _stream;
    private readonly IDiskImage _diskImage;
    private readonly IChunkAccess _chunkAccess;
    private readonly GcrEncoder _encoder;
    private readonly Dictionary<int, SynthesizedTrack> _trackCache;

    private int _currentQuarterTrack;
    private bool _isWriteProtected;
    private bool _disposed;

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

        // Open the file with CiderPress2 - use ReadWrite sharing for concurrent test access
        _stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

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
            if (!_chunkAccess.HasSectors || _chunkAccess.NumTracks != DiskIIConstants.TrackCount)
            {
                throw new InvalidDataException(
                    $"Expected {DiskIIConstants.TrackCount}-track 5.25\" disk, got {_chunkAccess.NumTracks} tracks");
            }

            if (_chunkAccess.NumSectorsPerTrack != DiskIIConstants.SectorsPerTrack16)
            {
                throw new InvalidDataException(
                    $"Expected {DiskIIConstants.SectorsPerTrack16} sectors per track, got {_chunkAccess.NumSectorsPerTrack}");
            }
        }
        catch
        {
            _stream?.Dispose();
            throw;
        }

        _encoder = new GcrEncoder();
        _trackCache = [];

        Debug.WriteLine($"Loaded sector disk image: {filePath} ({DiskIIConstants.TrackCount} tracks, {DiskIIConstants.SectorsPerTrack16} sectors/track)");
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
        if (track < 0 || track >= DiskIIConstants.TrackCount)
        {
            return null;
        }

        // Get or synthesize the track
        if (!_trackCache.TryGetValue(track, out SynthesizedTrack? synthTrack))
        {
            synthTrack = SynthesizeTrack(track);
            _trackCache[track] = synthTrack;
        }

        // Read the next bit
        return synthTrack.ReadNextBit();
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
    /// Flushes any pending writes to disk.
    /// </summary>
    public void Flush()
    {
        // Currently read-only, nothing to flush
    }

    /// <summary>
    /// Synthesizes a GCR-encoded track from logical sectors.
    /// </summary>
    private SynthesizedTrack SynthesizeTrack(int track)
    {
        byte[] trackData = new byte[DiskIIConstants.BytesPerNibTrack];
        int offset = 0;

        // Synthesize all 16 sectors with proper GCR encoding
        for (int sector = 0; sector < DiskIIConstants.SectorsPerTrack16; sector++)
        {
            // Check if we have enough space for this sector
            // Address field (~24 bytes) + Data field (~354 bytes) + small gap = ~400 bytes
            if (offset + 400 > DiskIIConstants.BytesPerNibTrack)
            {
                Debug.WriteLine($"Warning: Track {track} synthesis ran out of space at sector {sector}");
                break;
            }

            // Read logical sector from disk image
            byte[] sectorData = new byte[DiskIIConstants.BytesPerSector];
            try
            {
                _chunkAccess.ReadSector((uint)track, (uint)sector, sectorData, 0);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading track {track} sector {sector}: {ex.Message}");
                // Fill with zeros on error
                Array.Clear(sectorData, 0, DiskIIConstants.BytesPerSector);
            }

            // Write address field
            offset += _encoder.WriteAddressField(trackData, offset,
                volume: 254, track, sector);

            // Write data field (6&2 encoded)
            offset += _encoder.WriteDataField(trackData, offset, sectorData);

            // Write gap between sectors (if not the last sector)
            if (sector < DiskIIConstants.SectorsPerTrack16 - 1)
            {
                int remainingSpace = DiskIIConstants.BytesPerNibTrack - offset;
                int sectorsLeft = DiskIIConstants.SectorsPerTrack16 - sector - 1;
                int gapSize = remainingSpace / (sectorsLeft + 1);  // Distribute remaining space
                gapSize = Math.Min(Math.Max(gapSize, 10), 50);  // Clamp between 10-50 bytes
                offset += _encoder.WriteSyncGap(trackData, offset, gapSize);
            }
        }

        // Fill remaining space with sync bytes
        while (offset < DiskIIConstants.BytesPerNibTrack)
        {
            trackData[offset++] = 0xFF;
        }

        return new SynthesizedTrack(trackData);
    }

    /// <summary>
    /// Disposes resources used by this provider.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _stream?.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Represents a synthesized GCR-encoded track with bit-level access.
    /// </summary>
    private class SynthesizedTrack(byte[] data)
    {
        private readonly byte[] _data = data;
        private int _bitPosition;
        private readonly int _totalBits = data.Length * 8;

        public bool ReadNextBit()
        {
            int byteIndex = _bitPosition / 8;
            int bitIndex = 7 - (_bitPosition % 8); // MSB first

            bool bit = (_data[byteIndex] & (1 << bitIndex)) != 0;

            // Advance position (circular)
            _bitPosition = (_bitPosition + 1) % _totalBits;

            return bit;
        }
    }
}
