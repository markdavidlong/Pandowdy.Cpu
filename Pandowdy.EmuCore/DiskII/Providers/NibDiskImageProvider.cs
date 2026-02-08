// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Diagnostics;
using CommonUtil;
using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.DiskII.Providers;

/// <summary>
/// Provides disk image data from a .nib format disk image file.
/// </summary>
/// <remarks>
/// <para>
/// <strong>.NIB Format:</strong><br/>
/// The .nib format stores raw 6-and-2 GCR-encoded disk data exactly as it appears on the physical
/// disk. Each track contains 6656 bytes (53,248 bits), and a standard disk has 35 tracks.
/// Unlike flux-based formats (WOZ), NIB files contain no timing information.
/// </para>
/// <para>
/// <strong>Quarter Track Handling:</strong><br/>
/// Real Disk II drives can position the head at quarter-track intervals, but .nib files only
/// store full tracks. This implementation rounds quarter tracks to the nearest full track,
/// with half-tracks rounding down:
/// <list type="bullet">
/// <item>Quarter tracks 0, 1, 2, 3 â†’ Track 0</item>
/// <item>Quarter tracks 4, 5, 6, 7 â†’ Track 1</item>
/// <item>Quarter tracks 8, 9, 10, 11 â†’ Track 2</item>
/// <item>etc.</item>
/// </list>
/// </para>
/// <para>
/// <strong>Bit Streaming:</strong><br/>
/// This provider simply returns the next bit in sequence each time <see cref="GetBit"/> is called,
/// simulating a hardware shift register that continuously reads from the spinning disk. The disk
/// effectively "spins" as fast as the software reads it, which matches the behavior of Apple II
/// ROM boot code that polls the data register in tight loops.
/// </para>
/// </remarks>
public class NibDiskImageProvider : IDiskImageProvider, IDisposable
{
    private readonly byte[] _diskData;
    private readonly CircularBitBuffer[] _tracks;
    private readonly Stream _stream;  // Keep stream open to prevent IO errors (matches InternalWozDiskImageProvider)
    private int _currentQuarterTrack;
    private bool _isWriteProtected;
    private bool _disposed;
    private readonly HashSet<int> _missingTracks = []; // Track quarter-tracks with no data

    // Per-provider cycle tracking (fixes drive switching bug)
    // Each provider instance maintains its own rotational position independent of other drives
    private ulong _cycleOffsetAtFirstAccess = 0;
    private bool _hasBeenAccessed = false;

    // Random bit pattern for simulating MC3470 behavior when no track data exists
    // Approximately 30% ones, matching the TypeScript reference implementation
    private static readonly byte[] RandBits =
        [0, 1, 1, 0, 1, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 1, 0, 1, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1];
    private int _randPos;

    /// <summary>
    /// Gets the file path of the loaded disk image.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets whether this image supports write operations (NIB format is writable).
    /// </summary>
    public bool IsWritable => true;

    /// <summary>
    /// Gets or sets whether the disk is write-protected (simulates physical write-protect tab).
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
    /// Initializes a new instance of the <see cref="NibDiskImageProvider"/> class.
    /// </summary>
    /// <param name="filePath">Full path to the .nib disk image file.</param>
    /// <exception cref="ArgumentNullException">Thrown if filePath is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown if the file doesn't exist.</exception>
    /// <exception cref="InvalidDataException">Thrown if the file is not a valid .nib format.</exception>
    public NibDiskImageProvider(string filePath)
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

        // Open stream with ReadWrite access and ReadWrite sharing for concurrent test access
        _stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

        try
        {
            // Load the entire disk image into memory
            _diskData = new byte[_stream.Length];
            _stream.Read(_diskData, 0, _diskData.Length);

            // Validate file size (should be 35 tracks Ã— 6656 bytes = 232,960 bytes)
            int expectedSize = DiskIIConstants.TrackCount * DiskIIConstants.BytesPerNibTrack;
            if (_diskData.Length != expectedSize)
            {
                throw new InvalidDataException(
                    $"Invalid .nib file size. Expected {expectedSize} bytes, got {_diskData.Length} bytes.");
            }

            // Create CircularBitBuffer for each track
            _tracks = new CircularBitBuffer[DiskIIConstants.TrackCount];
            for (int track = 0; track < DiskIIConstants.TrackCount; track++)
            {
                int byteOffset = track * DiskIIConstants.BytesPerNibTrack;
                _tracks[track] = new CircularBitBuffer(
                    _diskData,
                    byteOffset,
                    bitOffset: 0,
                    bitCount: DiskIIConstants.BitsPerTrack,
                    new GroupBool(),
                    isReadOnly: false
                );
            }

            Debug.WriteLine($"Loaded .nib disk image: {filePath} ({DiskIIConstants.TrackCount} tracks, {DiskIIConstants.BytesPerNibTrack} bytes per track)");
        }
        catch
        {
            // Clean up stream on error
            _stream?.Dispose();
            throw;
        }
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
    /// Sets the current track position based on the quarter-track value.
    /// </summary>
    /// <param name="qTrack">Quarter-track position (0-139 for 35 tracks).</param>
    /// <remarks>
    /// <para>
    /// Quarter tracks are converted to full tracks using integer division (qTrack / 4).
    /// This means half-tracks round down:
    /// <list type="bullet">
    /// <item>qTrack 0-3 â†’ track 0</item>
    /// <item>qTrack 4-7 â†’ track 1</item>
    /// <item>qTrack 8-11 â†’ track 2</item>
    /// </list>
    /// </para>
    /// <para>
    /// If the quarter-track value exceeds the available tracks, it is clamped to the
    /// last track (track 34).
    /// </para>
    /// </remarks>
    public void SetQuarterTrack(int qTrack)
    {
        if (_currentQuarterTrack != qTrack)
        {
            _currentQuarterTrack = qTrack;
            int track = qTrack / 4;
            Debug.WriteLine($"NibDiskImageProvider: Disk head moved to track {track} (quarter-track {qTrack})");
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
    /// <strong>Cycle-Based Position Model:</strong> This implementation models a continuously
    /// spinning disk where position is tied to the system clock, not the number of reads.
    /// The disk "spins" at a constant rate (45/11 cycles per bit â‰ˆ 4.090909), and when you read,
    /// you get whatever bit happens to be under the head at that moment.
    /// </para>
    /// <para>
    /// <strong>Why This is Correct:</strong> Real Apple II floppy disks are continuous loops
    /// with no "start" position. When the motor turns on and you start reading, you begin
    /// wherever the disk happens to be. The Apple II ROM code searches for sync bytes and
    /// prologues (D5 AA 96, D5 AA AD) to align with the data, not relying on any absolute
    /// starting position.
    /// </para>
    /// <para>
    /// <strong>Calculation:</strong> Position = (cycleCount / CYCLES_PER_BIT) % BITS_PER_TRACK
    /// <list type="bullet">
    /// <item>Divide by 45/11: Each bit takes 45/11 CPU cycles (â‰ˆ4.09) at 1.023 MHz = 4Î¼s per bit</item>
    /// <item>Modulo BITS_PER_TRACK: Wrap at track boundary (disk is a loop)</item>
    /// <item>Result: Disk position tied to time, continues spinning between reads</item>
    /// </list>
    /// </para>
    /// </remarks>
    public bool? GetBit(ulong cycleCount)
    {
        // Initialize cycle offset on first access (simulates motor start)
        if (!_hasBeenAccessed)
        {
            _cycleOffsetAtFirstAccess = cycleCount;
            _hasBeenAccessed = true;
        }

        // Convert quarter-track to full track
        int track = _currentQuarterTrack / 4;

        // Check if we've already determined this quarter-track is out of bounds
        if (_missingTracks.Contains(_currentQuarterTrack))
        {
            return RandBit(); // Silently return random noise for known out-of-bounds tracks
        }

        // Clamp to valid track range and mark as missing if out of bounds
        if (track < 0 || track >= DiskIIConstants.TrackCount)
        {
            Debug.WriteLine($"NibDiskImageProvider: Quarter-track {_currentQuarterTrack} (track {track}) is out of bounds");
            _missingTracks.Add(_currentQuarterTrack);
            return RandBit(); // Return random noise (matches MC3470 hardware behavior)
        }

        // Get current track buffer
        CircularBitBuffer currentTrackBuffer = _tracks[track];

        // Cycle-based position: disk is continuously spinning tied to system clock
        // This models real hardware where the disk spins at constant speed and you
        // read wherever the disk happens to be at any given moment
        // Use relative cycles so each provider instance maintains independent rotational position
        ulong relativeCycles = cycleCount - _cycleOffsetAtFirstAccess;
        int bitPosition = (int)((relativeCycles / DiskIIConstants.CyclesPerBit) % DiskIIConstants.BitsPerTrack);
        currentTrackBuffer.BitPosition = bitPosition;

        byte bitValue = currentTrackBuffer.ReadNextBit();

        return bitValue == 1;
    }

    /// <summary>
    /// Writes a bit to the current track.
    /// </summary>
    /// <param name="bit">The bit value to write.</param>
    /// <param name="cycleCount">Current CPU cycle count used to calculate write position.</param>
    /// <returns>True if write succeeded, false if write-protected.</returns>
    /// <remarks>
    /// Writes use the same cycle-based position calculation as reads (45/11 cycles per bit).
    /// The write occurs at the current rotational position of the disk.
    /// </remarks>
    public bool WriteBit(bool bit, ulong cycleCount)
    {
        // Initialize cycle offset on first access (simulates motor start)
        if (!_hasBeenAccessed)
        {
            _cycleOffsetAtFirstAccess = cycleCount;
            _hasBeenAccessed = true;
        }

        if (_isWriteProtected)
        {
            return false;
        }

        // Convert quarter-track to full track
        int track = _currentQuarterTrack / 4;

        // Clamp to valid track range
        if (track < 0 || track >= DiskIIConstants.TrackCount)
        {
            return false;
        }

        // Cycle-based position (same as reads)
        // Use relative cycles so each provider instance maintains independent rotational position
        ulong relativeCycles = cycleCount - _cycleOffsetAtFirstAccess;
        int bitPosition = (int)((relativeCycles / DiskIIConstants.CyclesPerBit) % DiskIIConstants.BitsPerTrack);

        // Write the bit to the current track buffer
        CircularBitBuffer currentTrackBuffer = _tracks[track];
        currentTrackBuffer.BitPosition = bitPosition;
        currentTrackBuffer.WriteBit(bit ? 1 : 0);

        return true;
    }

    /// <summary>
    /// Flushes any pending writes to the disk image file.
    /// </summary>
    /// <remarks>
    /// Uses the open stream (matches InternalWozDiskImageProvider pattern) to avoid IO errors
    /// from file locking, permission changes, or media removal.
    /// </remarks>
    public void Flush()
    {
        if (!_isWriteProtected && _stream != null)
        {
            // Write the entire disk data back to stream (already open - no IO errors)
            _stream.Seek(0, SeekOrigin.Begin);
            _stream.Write(_diskData, 0, _diskData.Length);
            _stream.Flush();
            Debug.WriteLine($"NibDiskImageProvider: Flushed changes to {FilePath}");
        }
    }

    /// <summary>
    /// Disposes resources used by this provider.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            Flush();
            _stream?.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
