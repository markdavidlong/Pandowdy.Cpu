// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using CommonUtil;

namespace Pandowdy.EmuCore.DiskII;

/// <summary>
/// Unified internal representation of a 5.25" floppy disk.
/// All external formats convert to/from this format.
/// </summary>
/// <remarks>
/// <para>
/// This class provides a canonical in-memory representation of a disk image that all
/// format-specific providers convert to during import. It uses DiskArc's
/// <see cref="CircularBitBuffer"/> for bit-level track data, providing consistent
/// behavior across all disk formats.
/// </para>
/// <para>
/// <strong>Track Data:</strong><br/>
/// Tracks are stored as circular bit buffers, allowing efficient wraparound access.
/// Track lengths can vary (especially for WOZ images), so each track has an associated
/// bit count. Standard NIB/synthesized tracks have 51,200 bits (6,400 bytes).
/// </para>
/// <para>
/// <strong>Write Protection:</strong><br/>
/// The write protection state can be changed at runtime, simulating the physical
/// write-protect tab on a floppy disk.
/// </para>
/// <para>
/// <strong>Dirty Tracking:</strong><br/>
/// Modifications to track data are tracked via the <see cref="IsDirty"/> flag.
/// This enables save-on-close prompts and determines when disk writes are needed.
/// </para>
/// </remarks>
public class InternalDiskImage
{
    /// <summary>
    /// Bit-level track data using DiskArc's CircularBitBuffer.
    /// </summary>
    /// <remarks>
    /// Index corresponds to quarter-track / 4 (integer division).
    /// Quarter tracks 0-3 → Tracks[0], quarter tracks 4-7 → Tracks[1], etc.
    /// </remarks>
    public CircularBitBuffer[] Tracks { get; }

    /// <summary>
    /// Bit count per track (varies for WOZ, typically fixed 51200 for NIB/synthesized).
    /// </summary>
    /// <remarks>
    /// Standard Apple II tracks: 51,200 bits (6,400 bytes × 8)
    /// WOZ tracks: Variable, typically 50,000-52,000 bits
    /// </remarks>
    public int[] TrackBitCounts { get; }

    /// <summary>
    /// Number of tracks (typically 35 for DOS 3.3, up to 40 for some disks).
    /// </summary>
    public int TrackCount => Tracks.Length;

    /// <summary>
    /// Write protection state.
    /// </summary>
    /// <remarks>
    /// This can be changed at runtime to simulate the physical write-protect tab.
    /// </remarks>
    public bool IsWriteProtected { get; set; }

    /// <summary>
    /// Optimal bit timing in 125ns units (from WOZ, default 32 = 4µs).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Standard Apple II disks use 32 (meaning 32 × 125ns = 4µs per bit).
    /// Some copy-protected disks use non-standard timing like 31 or 33.
    /// </para>
    /// <para>
    /// The default value of 32 corresponds to a 250 kHz bit rate, which is the
    /// standard speed for Apple II 5.25" drives.
    /// </para>
    /// </remarks>
    public byte OptimalBitTiming { get; init; } = 32;

    /// <summary>
    /// True if disk has been modified since load.
    /// </summary>
    public bool IsDirty { get; private set; }

    /// <summary>
    /// Original source file path (null for new/embedded disks).
    /// </summary>
    public string? SourceFilePath { get; init; }

    /// <summary>
    /// Original format this disk was imported from.
    /// </summary>
    public DiskFormat OriginalFormat { get; init; }

    /// <summary>
    /// Destination file path for save operations (null until set by import derivation or Save As).
    /// </summary>
    /// <remarks>
    /// <para>
    /// When a disk is imported, this is derived from <see cref="SourceFilePath"/> with a "_new"
    /// suffix (e.g., "game.woz" → "game_new.woz"). If the derived path already exists on disk,
    /// the suffix is auto-incremented: "game_new2.woz", "game_new3.woz", etc.
    /// When created blank, this is null until the user performs a "Save As".
    /// </para>
    /// <para>
    /// This is always a separate path from <see cref="SourceFilePath"/> — the original
    /// source file is never overwritten.
    /// </para>
    /// </remarks>
    public string? DestinationFilePath { get; set; }

    /// <summary>
    /// Format to use when saving to <see cref="DestinationFilePath"/>.
    /// Inferred from the file extension, or set explicitly.
    /// </summary>
    public DiskFormat DestinationFormat { get; set; } = DiskFormat.Unknown;

    /// <summary>
    /// Initializes a new internal disk image with the specified track count.
    /// </summary>
    /// <param name="trackCount">Number of tracks (typically 35 or 40).</param>
    /// <param name="standardTrackBitCount">Bit count for standard tracks (default 51200).</param>
    public InternalDiskImage(int trackCount = 35, int standardTrackBitCount = 51200)
    {
        if (trackCount <= 0 || trackCount > 40)
        {
            throw new ArgumentOutOfRangeException(nameof(trackCount), "Track count must be between 1 and 40.");
        }

        if (standardTrackBitCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(standardTrackBitCount), "Bit count must be positive.");
        }

        Tracks = new CircularBitBuffer[trackCount];
        TrackBitCounts = new int[trackCount];

        // Initialize all tracks with standard bit count
        for (int i = 0; i < trackCount; i++)
        {
            TrackBitCounts[i] = standardTrackBitCount;
            // Create backing byte array for track
            int byteCount = (standardTrackBitCount + 7) / 8; // Round up to nearest byte
            byte[] trackData = new byte[byteCount];
            Tracks[i] = new CircularBitBuffer(
                trackData,
                byteOffset: 0,
                bitOffset: 0,
                bitCount: standardTrackBitCount,
                new GroupBool(),
                isReadOnly: false
            );
        }
    }

    /// <summary>
    /// Initializes a new internal disk image with pre-allocated tracks.
    /// </summary>
    /// <param name="tracks">Track data buffers.</param>
    /// <param name="trackBitCounts">Bit counts for each track.</param>
    /// <exception cref="ArgumentException">Thrown when arrays don't match in length.</exception>
    public InternalDiskImage(CircularBitBuffer[] tracks, int[] trackBitCounts)
    {
        if (tracks.Length != trackBitCounts.Length)
        {
            throw new ArgumentException("Track and bit count arrays must have the same length.");
        }

        if (tracks.Length == 0 || tracks.Length > 40)
        {
            throw new ArgumentException("Track count must be between 1 and 40.");
        }

        Tracks = tracks;
        TrackBitCounts = trackBitCounts;
    }

    /// <summary>
    /// Mark disk as modified.
    /// </summary>
    public void MarkDirty()
    {
        IsDirty = true;
    }

    /// <summary>
    /// Clear dirty flag (after save).
    /// </summary>
    public void ClearDirty()
    {
        IsDirty = false;
    }
}
