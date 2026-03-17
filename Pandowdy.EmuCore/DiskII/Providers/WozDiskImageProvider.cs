// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Diagnostics;
using CommonUtil;
using DiskArc.Disk;
using Pandowdy.EmuCore.Machine;

namespace Pandowdy.EmuCore.DiskII.Providers;

/// <summary>
/// Provides disk image data from a .woz format disk image file using CiderPress2.
/// </summary>
/// <remarks>
/// <para>
/// <strong>.WOZ Format:</strong><br/>
/// The .woz format stores raw flux transitions with timing information from Apple II 5.25" and 3.5"
/// floppy disks. It's the most accurate disk format, supporting variable-length tracks,
/// non-byte-aligned nibble data, quarter tracks, and even copy protection schemes.
/// </para>
/// <para>
/// <strong>Quarter Track Handling:</strong><br/>
/// WOZ files can store actual quarter-track data captured from the physical disk. This provider
/// accesses the appropriate quarter-track data directly, providing the most accurate emulation.
/// </para>
/// <para>
/// <strong>Timing Information:</strong><br/>
/// This implementation uses cycle-accurate bit timing at 4Î¼s per bit cell (250 kHz bit rate).
/// The Apple II CPU runs at 1.023 MHz, giving exactly 45/11 cycles per bit (â‰ˆ4.090909 cycles).
/// Disk position is calculated from absolute CPU cycle count, modeling a continuously spinning
/// disk where reads occur at the current rotational position.
/// </para>
/// </remarks>
public class WozDiskImageProvider : IDiskImageProvider, IDisposable
{
    private readonly Stream _stream;
    private readonly Woz _wozImage;
    private readonly CircularBitBuffer?[] _trackCache;
    private readonly ulong[] _trackBitCounts; // Track lengths in bits
    private readonly HashSet<int> _missingTracks = []; // Track quarter-tracks with no data
    private int _currentQuarterTrack;
    private bool _isWriteProtected;
    private bool _disposed;

    // Random bit pattern for simulating MC3470 behavior when no track data exists
    // Approximately 30% ones, matching the TypeScript reference implementation
    private static readonly byte[] RandBits =
        [0, 1, 1, 0, 1, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 1, 0, 1, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1];
    private int _randPos;

    private const int MaxTracks = 35;
    private const int QuartersPerTrack = 4;

    /// <summary>
    /// Gets the file path of the disk image.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets whether this image supports write operations.
    /// </summary>
    /// <remarks>
    /// WOZ images with flux data are read-only. Otherwise write support depends on
    /// the underlying stream.
    /// </remarks>
    public bool IsWritable => !_wozImage.IsReadOnly;

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
    /// </summary>
    public byte OptimalBitTiming => 32; // Default timing

    /// <summary>
    /// Gets the number of bits on the current track.
    /// </summary>
    public int CurrentTrackBitCount
    {
        get
        {
            if (_currentQuarterTrack >= 0 && _currentQuarterTrack < _trackBitCounts.Length)
            {
                ulong bitCount = _trackBitCounts[_currentQuarterTrack];
                return bitCount > 0 ? (int)bitCount : DiskIIConstants.BitsPerTrack;
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
            if (_currentQuarterTrack >= 0 && _currentQuarterTrack < _trackCache.Length)
            {
                var buffer = _trackCache[_currentQuarterTrack];
                return buffer?.BitPosition ?? 0;
            }
            return 0;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WozDiskImageProvider"/> class.
    /// </summary>
    /// <param name="filePath">Full path to the .woz disk image file.</param>
    /// <exception cref="ArgumentNullException">Thrown if filePath is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown if the file doesn't exist.</exception>
    /// <exception cref="InvalidDataException">Thrown if the file is not a valid .woz format.</exception>
    public WozDiskImageProvider(string filePath)
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

        // Open the WOZ file with CiderPress2 - use ReadWrite sharing for concurrent test access
        _stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

        try
        {
            _wozImage = Woz.OpenDisk(_stream, new AppHook(null!));

            // Check if it's a 5.25" disk
            if (_wozImage.DiskKind != DiskArc.Defs.MediaKind.GCR_525)
            {
                throw new InvalidDataException(
                    $"Only 5.25\" GCR disks are supported. Found: {_wozImage.DiskKind}");
            }

            // Create cache for track buffers (35 tracks Ã— 4 quarter-tracks = 140 positions)
            _trackCache = new CircularBitBuffer?[MaxTracks * QuartersPerTrack];
            _trackBitCounts = new ulong[MaxTracks * QuartersPerTrack];
        }
        catch
        {
            _stream?.Dispose();
            throw;
        }
#if ControllerDebug
        Debug.WriteLine($"Loaded .woz disk image: {filePath} (5.25\" GCR disk, {DiskIIConstants.CyclesPerBit:F6} cycles/bit)");
#endif
    }
    /// <summary>
    /// Generates a random bit to simulate MC3470 controller behavior when no track data exists.
    /// </summary>
    /// <remarks>
    /// Real Disk II hardware with the MC3470 controller chip returns random noise when
    /// reading from tracks with no data. Some copy-protection schemes (like "anti-m")
    /// rely on this behavior to detect if a disk is inserted.
    /// This implements the same ~30% ones pattern as the TypeScript reference.
    /// </remarks>
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
    /// <remarks>
    /// WozDiskImageProvider uses absolute cycle counts, so this is a no-op.
    /// </remarks>
    public void NotifyMotorStateChanged(bool motorOn, ulong cycleCount)
    {
        // This provider uses absolute cycle-based timing - no offset tracking needed
    }

    /// <summary>
    /// Sets the current quarter-track position.
    /// </summary>
    /// <param name="qTrack">Quarter-track position (0-139 for 35 tracks).</param>
    public void SetQuarterTrack(int qTrack)
    {
        if (_currentQuarterTrack != qTrack)
        {
            int printableTrack = qTrack / QuartersPerTrack;
            int printableQuarter = (qTrack % QuartersPerTrack) * 25;
            _currentQuarterTrack = qTrack;
#if ControllerDebug
            Debug.WriteLine($"WozDiskImageProvider: Disk head moved to track {printableTrack}.{printableQuarter} (quarter-track {qTrack})");
#endif
        }
    }

    /// <summary>
    /// Reads the next bit from the current track.
    /// </summary>
    /// <param name="cycleCount">Current CPU cycle count used to calculate rotational position.</param>
    /// <returns>
    /// The next bit (true or false) at the current rotational position.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Cycle-Accurate Timing:</strong> This implementation models a continuously spinning
    /// disk where position is tied to the system clock. The disk "spins" at 250 kHz (4Î¼s per bit),
    /// and with the CPU at 1.023 MHz, each bit takes exactly 45/11 cycles.
    /// </para>
    /// <para>
    /// <strong>Position Calculation:</strong> Position = (cycleCount / CYCLES_PER_BIT) % trackBitCount
    /// <list type="bullet">
    /// <item>Divide by 45/11: Converts CPU cycles to bit positions at correct disk speed</item>
    /// <item>Modulo trackBitCount: Wrap at track boundary (disk is a continuous loop)</item>
    /// <item>Result: Disk position tied to time, continues spinning between reads</item>
    /// </list>
    /// </para>
    /// <para>
    /// WOZ tracks can have variable lengths (not always 6656 bytes like NIB), so we use the
    /// actual track bit count from the circular buffer.
    /// </para>
    /// </remarks>
    public bool? GetBit(ulong cycleCount)
    {
        // Convert quarter-track to track and fraction
        int track = _currentQuarterTrack / QuartersPerTrack;
        int quarter = _currentQuarterTrack % QuartersPerTrack;

        // Clamp to valid range - return random noise if out of bounds
        if (track < 0 || track >= MaxTracks)
        {
            return RandBit();
        }

        // Check if we've already determined this track is missing
        if (_missingTracks.Contains(_currentQuarterTrack))
        {
            return RandBit(); // Silently return random noise for known-missing tracks
        }

        // Get or cache the track buffer
        if (_trackCache[_currentQuarterTrack] == null)
        {
            if (!_wozImage.GetTrackBits((uint)track, (uint)quarter, out CircularBitBuffer? cbb))
            {
                // Track not found - log once, then mark as missing
                Debug.WriteLine($"WozDiskImageProvider: Track {track}.{quarter} not found in image");
                _missingTracks.Add(_currentQuarterTrack);
                return RandBit();
            }

            _trackCache[_currentQuarterTrack] = cbb;
            // Cache the track length
            _trackBitCounts[_currentQuarterTrack] = (ulong)cbb!.BitCount;
        }

        CircularBitBuffer? trackBuffer = _trackCache[_currentQuarterTrack];
        if (trackBuffer == null)
        {
            // Shouldn't happen, but handle gracefully
            return RandBit();
        }

        // Cycle-based position: disk is continuously spinning tied to system clock
        ulong trackBitCount = _trackBitCounts[_currentQuarterTrack];
        int bitPosition = (int)((cycleCount / DiskIIConstants.CyclesPerBit) % trackBitCount);

        // Set buffer position and read the bit
        trackBuffer.BitPosition = bitPosition;
        byte bitValue = trackBuffer.ReadNextBit();

        return bitValue == 1;
    }

    /// <summary>
    /// Writes a bit to the current track.
    /// </summary>
    /// <param name="bit">The bit value to write.</param>
    /// <param name="cycleCount">Current CPU cycle count used to calculate write position.</param>
    /// <returns>True if write succeeded, false if write-protected or unsupported.</returns>
    /// <remarks>
    /// Writes use the same cycle-based position calculation as reads (45/11 cycles per bit).
    /// The write occurs at the current rotational position of the disk.
    /// </remarks>
    public bool WriteBit(bool bit, ulong cycleCount)
    {
        if (_isWriteProtected || !IsWritable)
        {
            return false;
        }

        // Convert quarter-track to track and fraction
        int track = _currentQuarterTrack / QuartersPerTrack;
        int quarter = _currentQuarterTrack % QuartersPerTrack;

        // Clamp to valid range
        if (track < 0 || track >= MaxTracks)
        {
            return false;
        }

        // Get the track buffer
        if (_trackCache[_currentQuarterTrack] == null)
        {
            if (!_wozImage.GetTrackBits((uint)track, (uint)quarter, out CircularBitBuffer? cbb))
            {
                return false;
            }
            _trackCache[_currentQuarterTrack] = cbb;
            _trackBitCounts[_currentQuarterTrack] = (ulong)cbb!.BitCount;
        }

        CircularBitBuffer? trackBuffer = _trackCache[_currentQuarterTrack];
        if (trackBuffer == null || trackBuffer.IsReadOnly)
        {
            return false;
        }

        // Cycle-based position (same as reads)
        ulong trackBitCount = _trackBitCounts[_currentQuarterTrack];
        int bitPosition = (int)((cycleCount / DiskIIConstants.CyclesPerBit) % trackBitCount);

        // Set buffer position and write the bit
        trackBuffer.BitPosition = bitPosition;
        trackBuffer.WriteBit(bit ? 1 : 0);

        return true;
    }

    /// <summary>
    /// Advances the disk by the specified number of CPU cycles and returns bits read.
    /// </summary>
    /// <param name="elapsedCycles">CPU cycles elapsed since last call.</param>
    /// <param name="bits">Buffer to receive the bits read.</param>
    /// <returns>Number of bits actually read.</returns>
    public int AdvanceAndReadBits(double elapsedCycles, Span<bool> bits)
    {
        // Stub implementation - uses sequential reading without proper timing
        const double cyclesPerBit = 32.0 / 8.0;
        int bitsToRead = (int)(elapsedCycles / cyclesPerBit);
        bitsToRead = Math.Min(bitsToRead, bits.Length);

        int track = _currentQuarterTrack / QuartersPerTrack;
        int quarter = _currentQuarterTrack % QuartersPerTrack;

        if (track < 0 || track >= MaxTracks)
        {
            for (int i = 0; i < bitsToRead; i++)
            {
                bits[i] = RandBit();
            }
            return bitsToRead;
        }

        // Get or cache the track buffer
        if (_trackCache[_currentQuarterTrack] == null)
        {
            if (!_wozImage.GetTrackBits((uint)track, (uint)quarter, out CircularBitBuffer? cbb))
            {
                for (int i = 0; i < bitsToRead; i++)
                {
                    bits[i] = RandBit();
                }
                return bitsToRead;
            }
            _trackCache[_currentQuarterTrack] = cbb;
            _trackBitCounts[_currentQuarterTrack] = (ulong)cbb!.BitCount;
        }

        var trackBuffer = _trackCache[_currentQuarterTrack];
        if (trackBuffer == null)
        {
            return 0;
        }

        for (int i = 0; i < bitsToRead; i++)
        {
            bits[i] = trackBuffer.ReadNextBit() == 1;
        }

        return bitsToRead;
    }

    /// <summary>
    /// Flushes any pending writes to the disk image file.
    /// </summary>
    public void Flush()
    {
        if (!_isWriteProtected && IsWritable)
        {
            _wozImage.Flush();
            Debug.WriteLine($"WozDiskImageProvider: Flushed changes to {FilePath}");
        }
    }

    /// <summary>
    /// Disposes resources used by this provider.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _wozImage?.Dispose();
            _stream?.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
