// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Diagnostics;
using CommonUtil;
using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.DiskII.Providers;

/// <summary>
/// Unified disk image provider that operates on the internal disk format.
/// Replaces format-specific providers (WOZ, NIB, Sector) with a single implementation.
/// </summary>
/// <remarks>
/// <para>
/// This provider operates solely on <see cref="InternalDiskImage"/>, the canonical
/// representation that all external formats are converted to during import.
/// This eliminates code duplication and ensures consistent behavior across all formats.
/// </para>
/// <para>
/// <strong>Timing Model:</strong><br/>
/// Uses cycle-accurate bit timing at configurable speeds (default 4µs per bit = 250 kHz).
/// The Apple II CPU runs at 1.023 MHz, giving 4.090909 cycles per bit at standard speed.
/// Disk position is calculated from absolute CPU cycle count, modeling a continuously
/// spinning disk where reads occur at the current rotational position.
/// </para>
/// <para>
/// <strong>Quarter Track Handling:</strong><br/>
/// Quarter tracks map to physical tracks via integer division (qTrack / 4).
/// When switching tracks with different bit counts, the bit position is scaled
/// proportionally using the Applesauce formula: newPos = oldPos × (newBitCount / oldBitCount).
/// This maintains cross-track synchronization for copy-protected disks.
/// </para>
/// </remarks>
public class UnifiedDiskImageProvider : IDiskImageProvider, IDisposable
{
    private readonly InternalDiskImage _diskImage;
    private int _currentQuarterTrack;
    private bool _disposed;
    private readonly HashSet<int> _missingTracks = []; // Quarter-tracks with no data

    // Per-provider cycle tracking (each drive maintains independent rotational position)
    private ulong _cycleOffsetAtFirstAccess = 0;
    private bool _hasBeenAccessed = false;

    // Random bit pattern for simulating MC3470 behavior when no track data exists
    // Approximately 30% ones, matching the TypeScript reference implementation
    private static readonly byte[] RandBits =
        [0, 1, 1, 0, 1, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 1, 0, 1, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1];
    private int _randPos;

    /// <summary>
    /// Gets the internal disk image (for dirty/destination tracking).
    /// </summary>
    /// <remarks>
    /// Exposed for <see cref="DiskIIDrive"/> to provide access to dirty state
    /// and destination path information through its internal accessor.
    /// </remarks>
    internal InternalDiskImage InternalImage => _diskImage;

    /// <summary>
    /// Gets the file path of the disk image.
    /// </summary>
    public string FilePath => _diskImage.SourceFilePath ?? "(internal)";

    /// <summary>
    /// Gets whether this image supports write operations.
    /// </summary>
    /// <remarks>
    /// Internal format is always writable in memory. Write-protection is a separate flag.
    /// </remarks>
    public bool IsWritable => true;

    /// <summary>
    /// Gets or sets whether the disk is write-protected.
    /// </summary>
    public bool IsWriteProtected
    {
        get => _diskImage.IsWriteProtected;
        set => _diskImage.IsWriteProtected = value;
    }

    /// <summary>
    /// Gets the optimal bit timing for this disk image.
    /// </summary>
    public byte OptimalBitTiming => _diskImage.OptimalBitTiming;

    /// <summary>
    /// Gets the number of bits on the current track.
    /// </summary>
    public int CurrentTrackBitCount
    {
        get
        {
            int track = _currentQuarterTrack / 4;
            if (track < 0 || track >= _diskImage.TrackCount)
            {
                return 51200; // Default standard track length
            }
            return _diskImage.TrackBitCounts[track];
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
            if (track < 0 || track >= _diskImage.TrackCount)
            {
                return 0;
            }
            return _diskImage.Tracks[track].BitPosition;
        }
    }

    /// <summary>
    /// Gets the current quarter-track position.
    /// </summary>
    public int CurrentQuarterTrack => _currentQuarterTrack;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnifiedDiskImageProvider"/> class.
    /// </summary>
    /// <param name="diskImage">The internal disk image to provide.</param>
    public UnifiedDiskImageProvider(InternalDiskImage diskImage)
    {
        _diskImage = diskImage ?? throw new ArgumentNullException(nameof(diskImage));
        Debug.WriteLine($"UnifiedDiskImageProvider: Created for {FilePath} ({_diskImage.TrackCount} tracks, format: {_diskImage.OriginalFormat})");
    }

    /// <summary>
    /// Generates a random bit to simulate MC3470 controller behavior when no track data exists.
    /// </summary>
    private bool RandBit()
    {
        _randPos++;
        return RandBits[_randPos & 0x1F] == 1;
    }

    /// <summary>
    /// Notifies the provider of motor state changes.
    /// </summary>
    /// <param name="motorOn">True if motor is turning on, false if turning off.</param>
    /// <param name="cycleCount">Current CPU cycle count when state changed.</param>
    public void NotifyMotorStateChanged(bool motorOn, ulong cycleCount)
    {
        if (motorOn)
        {
            Debug.WriteLine($"UnifiedDiskImageProvider NotifyMotorStateChanged: Motor ON at cycle {cycleCount}");
            _cycleOffsetAtFirstAccess = cycleCount;
            _hasBeenAccessed = true;
        }
    }

    /// <summary>
    /// Sets the current track position based on the quarter-track value.
    /// </summary>
    /// <param name="qTrack">Quarter-track position (0-139 for 35 tracks, 0-159 for 40 tracks).</param>
    public void SetQuarterTrack(int qTrack)
    {
        if (_currentQuarterTrack != qTrack)
        {
            int oldTrack = _currentQuarterTrack / 4;
            int newTrack = qTrack / 4;

            // Apply Applesauce cross-track scaling formula when changing tracks
            if (oldTrack != newTrack &&
                oldTrack >= 0 && oldTrack < _diskImage.TrackCount &&
                newTrack >= 0 && newTrack < _diskImage.TrackCount)
            {
                int oldBitCount = _diskImage.TrackBitCounts[oldTrack];
                int newBitCount = _diskImage.TrackBitCounts[newTrack];
                int oldPosition = _diskImage.Tracks[oldTrack].BitPosition;

                if (oldBitCount != newBitCount)
                {
                    // Scale position: newPos = oldPos × (newBitCount / oldBitCount)
                    int newPosition = (int)((long)oldPosition * newBitCount / oldBitCount);
                    _diskImage.Tracks[newTrack].BitPosition = newPosition;
                    Debug.WriteLine($"UnifiedDiskImageProvider: Track {oldTrack}→{newTrack}, position scaled {oldPosition}→{newPosition} (bits {oldBitCount}→{newBitCount})");
                }
            }

            _currentQuarterTrack = qTrack;
            Debug.WriteLine($"UnifiedDiskImageProvider: Disk head moved to track {newTrack} (quarter-track {qTrack})");
        }
    }

    /// <summary>
    /// Reads the next bit from the current track.
    /// </summary>
    /// <param name="cycleCount">Current CPU cycle count used to calculate rotational position.</param>
    /// <returns>The next bit (true or false) at the current rotational position.</returns>
    public bool? GetBit(ulong cycleCount)
    {
        // Initialize cycle offset on first access
        if (!_hasBeenAccessed)
        {
            Debug.WriteLine($"UnifiedDiskImageProvider GetBit: WARNING - GetBit called before NotifyMotorStateChanged! cycleCount={cycleCount}");
            _cycleOffsetAtFirstAccess = cycleCount;
            _hasBeenAccessed = true;
        }

        // Handle cycle count going backwards (system reset)
        if (cycleCount < _cycleOffsetAtFirstAccess)
        {
            Debug.WriteLine($"UnifiedDiskImageProvider GetBit: Cycle count went backwards (reset). Reinitializing offset. cycleCount={cycleCount}, oldOffset={_cycleOffsetAtFirstAccess}");
            _cycleOffsetAtFirstAccess = cycleCount;
        }

        // Convert quarter-track to full track
        int track = _currentQuarterTrack / 4;

        // Check if we've already determined this quarter-track is out of bounds
        if (_missingTracks.Contains(_currentQuarterTrack))
        {
            return RandBit(); // Silently return random noise for known out-of-bounds tracks
        }

        // Clamp to valid track range and mark as missing if out of bounds
        if (track < 0 || track >= _diskImage.TrackCount)
        {
            Debug.WriteLine($"UnifiedDiskImageProvider: Quarter-track {_currentQuarterTrack} (track {track}) is out of bounds");
            _missingTracks.Add(_currentQuarterTrack);
            return RandBit(); // Return random noise (matches MC3470 hardware behavior)
        }

        // Get current track buffer
        CircularBitBuffer currentTrackBuffer = _diskImage.Tracks[track];
        int trackBitCount = _diskImage.TrackBitCounts[track];

        // Cycle-based position: disk continuously spinning tied to system clock
        ulong relativeCycles = cycleCount - _cycleOffsetAtFirstAccess;
        double cyclesPerBit = _diskImage.OptimalBitTiming / 8.0; // Convert 125ns units to cycles
        int bitPosition = (int)((relativeCycles / cyclesPerBit) % trackBitCount);
        currentTrackBuffer.BitPosition = bitPosition;

        byte bitValue = currentTrackBuffer.ReadNextBit();
        return bitValue == 1;
    }

    /// <summary>
    /// Advances the disk by the specified number of CPU cycles and returns bits read.
    /// </summary>
    /// <param name="elapsedCycles">CPU cycles elapsed since last call.</param>
    /// <param name="bits">Buffer to receive the bits read.</param>
    /// <returns>Number of bits actually read.</returns>
    public int AdvanceAndReadBits(double elapsedCycles, Span<bool> bits)
    {
        double cyclesPerBit = _diskImage.OptimalBitTiming / 8.0;
        int bitsToRead = (int)(elapsedCycles / cyclesPerBit);
        bitsToRead = Math.Min(bitsToRead, bits.Length);

        int track = _currentQuarterTrack / 4;
        if (track < 0 || track >= _diskImage.TrackCount)
        {
            // Out of bounds - return random bits
            for (int i = 0; i < bitsToRead; i++)
            {
                bits[i] = RandBit();
            }
            return bitsToRead;
        }

        CircularBitBuffer currentTrackBuffer = _diskImage.Tracks[track];
        for (int i = 0; i < bitsToRead; i++)
        {
            byte bitValue = currentTrackBuffer.ReadNextBit();
            bits[i] = bitValue == 1;
        }

        return bitsToRead;
    }

    /// <summary>
    /// Writes a bit to the current track position.
    /// </summary>
    /// <param name="bit">Bit value to write (true = 1, false = 0).</param>
    /// <param name="cycleCount">Current CPU cycle count (for timing).</param>
    /// <returns>True if write succeeded, false if write-protected or out of bounds.</returns>
    public bool WriteBit(bool bit, ulong cycleCount)
    {
        if (IsWriteProtected)
        {
            return false; // Write-protected
        }

        int track = _currentQuarterTrack / 4;
        if (track < 0 || track >= _diskImage.TrackCount)
        {
            return false; // Out of bounds
        }

        CircularBitBuffer currentTrackBuffer = _diskImage.Tracks[track];
        currentTrackBuffer.WriteBit(bit ? 1 : 0);
        _diskImage.MarkDirty();
        return true;
    }

    /// <summary>
    /// Flushes any pending writes to disk (no-op for internal format).
    /// </summary>
    public void Flush()
    {
        // Internal format is in-memory only, nothing to flush
        // Flushing is handled by exporters when saving to external formats
    }

    /// <summary>
    /// Disposes resources used by the provider.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            Debug.WriteLine($"UnifiedDiskImageProvider: Disposed for {FilePath}");
        }
    }
}
