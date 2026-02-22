// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.EmuCore.DiskII;

/// <summary>
/// Provides low-level disk image data to emulate Disk II hardware behavior.
/// </summary>
/// <remarks>
/// <para>
/// This interface abstracts the disk image format from the controller card logic.
/// The controller card operates at the hardware level (stepper motors, shift registers),
/// while implementations handle format-specific details (NIB, WOZ, DSK, etc.).
/// </para>
/// <para>
/// <strong>Timing Model:</strong> The provider owns the incremental timing state including
/// track location, cycle remainder, and optimal bit timing. This matches the TypeScript
/// reference implementation where disk state is per-drive, not controller-level.
/// </para>
/// <para>
/// <strong>Implementations:</strong>
/// <list type="bullet">
/// <item><see cref="DiskII.Providers.NibDiskImageProvider"/> - Raw GCR nibble format (.nib)</item>
/// <item><see cref="DiskII.Providers.SectorDiskImageProvider"/> - Sector format (.dsk/.do/.po)</item>
/// <item><see cref="DiskII.Providers.WozDiskImageProvider"/> - WOZ flux timing (.woz)</item>
/// </list>
/// </para>
/// </remarks>
public interface IDiskImageProvider : IDisposable
{
    /// <summary>
    /// Gets the file path of the disk image.
    /// </summary>
    string FilePath { get; }

    /// <summary>
    /// Gets whether this image supports write operations.
    /// </summary>
    bool IsWritable { get; }

    /// <summary>
    /// Gets or sets whether the disk is write-protected (simulates physical write-protect tab).
    /// </summary>
    bool IsWriteProtected { get; set; }

    /// <summary>
    /// Gets the optimal bit timing for this disk image (default 32 = 4µs/bit).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Standard Apple II disks use 32 (meaning 32/8 = 4 CPU cycles per bit at 1.023 MHz).
    /// Some copy-protected disks use non-standard timing like 31 or 33.
    /// </para>
    /// <para>
    /// For WOZ files, this value comes from the INFO chunk byte 40.
    /// For NIB files, always returns the default 32.
    /// </para>
    /// </remarks>
    byte OptimalBitTiming { get; }

    /// <summary>
    /// Gets the number of bits on the current track.
    /// </summary>
    /// <remarks>
    /// Standard tracks have approximately 51,024 bits (6,378 bytes × 8).
    /// WOZ files may have varying bit counts per track.
    /// </remarks>
    int CurrentTrackBitCount { get; }

    /// <summary>
    /// Gets the current bit position within the track (0 to CurrentTrackBitCount-1).
    /// </summary>
    int TrackBitPosition { get; }

    /// <summary>
    /// Sets the current quarter-track position (0-139 for 35 tracks).
    /// </summary>
    /// <param name="qTrack">Quarter-track position (0-3 = track 0, 4-7 = track 1, etc.).</param>
    /// <remarks>
    /// When changing tracks, the provider should apply the Applesauce formula to scale
    /// the bit position proportionally: newPos = oldPos × (newBitCount / oldBitCount).
    /// This maintains cross-track synchronization for copy-protected disks.
    /// </remarks>
    void SetQuarterTrack(int qTrack);

    /// <summary>
    /// Gets the current quarter-track position.
    /// </summary>
    int CurrentQuarterTrack { get; }

    /// <summary>
    /// Advances the disk by the specified number of CPU cycles and returns bits read.
    /// </summary>
    /// <param name="elapsedCycles">CPU cycles elapsed since last call.</param>
    /// <param name="bits">Buffer to receive the bits read (caller provides array).</param>
    /// <returns>Number of bits actually read (may be 0 if not enough cycles for a full bit).</returns>
    /// <remarks>
    /// <para>
    /// The provider maintains an internal cycle remainder to handle fractional bits.
    /// Each bit takes OptimalBitTiming/8 CPU cycles (default: 32/8 = 4 cycles/bit).
    /// </para>
    /// <para>
    /// This method implements weak bit detection: if 4 consecutive 0-bits are seen,
    /// subsequent bits in that window return random values to simulate magnetic ambiguity.
    /// </para>
    /// </remarks>
    int AdvanceAndReadBits(double elapsedCycles, Span<bool> bits);

    /// <summary>
    /// Reads the next bit from the current track at the current rotational position.
    /// </summary>
    /// <param name="cycleCount">Current CPU cycle count (for timing-sensitive formats like WOZ).</param>
    /// <returns>The next bit value, or null if no disk or invalid position.</returns>
    /// <remarks>
    /// <strong>Deprecated:</strong> Prefer <see cref="AdvanceAndReadBits"/> for proper timing.
    /// This method is retained for compatibility but may be removed in future versions.
    /// </remarks>
    bool? GetBit(ulong cycleCount);

    /// <summary>
    /// Writes a bit to the current track (if supported).
    /// </summary>
    /// <param name="bit">The bit value to write.</param>
    /// <param name="cycleCount">Current CPU cycle count.</param>
    /// <returns>True if write succeeded, false if write-protected or unsupported.</returns>
    bool WriteBit(bool bit, ulong cycleCount);

    /// <summary>
    /// Notifies the provider of motor state changes.
    /// </summary>
    /// <param name="motorOn">True if motor is turning on, false if turning off.</param>
    /// <param name="cycleCount">Current CPU cycle count when state changed.</param>
    /// <remarks>
    /// <para>
    /// When motor turns ON, the provider should reset its cycle remainder to 0.
    /// This establishes the starting point for bit timing calculations.
    /// </para>
    /// <para>
    /// Critical for per-provider cycle tracking to maintain independent rotational positions
    /// across multiple drives.
    /// </para>
    /// </remarks>
    void NotifyMotorStateChanged(bool motorOn, ulong cycleCount);

    /// <summary>
    /// Flushes any pending writes to disk.
    /// </summary>
    void Flush();
}
