// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using CommonUtil;

namespace Pandowdy.EmuCore.DiskII;

/// <summary>
/// Unified internal representation of a 5.25" floppy disk with quarter-track resolution.
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
/// <strong>Quarter-Track Model:</strong><br/>
/// The disk models physical quarter-track positions that the drive head can move to.
/// For a disk with N physical tracks, there are (N-1)*4+1 quarter-track positions.
/// For example, a 35-track disk has 137 quarter-track positions (0-136), representing
/// positions 0.00, 0.25, 0.50, 0.75, 1.00, ... up to 34.00. The outermost track has
/// no fractional positions beyond it.
/// </para>
/// <para>
/// <strong>Track Data:</strong><br/>
/// Quarter tracks are stored as nullable circular bit buffers. A null entry indicates
/// an unwritten quarter track (returns random noise when read, simulating MC3470 behavior).
/// Track lengths can vary (especially for WOZ images), so each quarter track has an
/// associated bit count. Standard NIB/synthesized tracks have 51,200 bits (6,400 bytes).
/// </para>
/// <para>
/// <strong>Import Behavior:</strong><br/>
/// When importing from whole-track formats (NIB, DSK, DO, PO), only quarter-track
/// positions 0, 4, 8, 12... are populated. When importing from WOZ format, all
/// quarter-track positions with data are populated. The emulator can write to any
/// quarter-track position at runtime.
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
    /// Number of quarter-track positions per physical track.
    /// </summary>
    public const int QuartersPerTrack = 4;

    /// <summary>
    /// Bit-level data for each quarter-track position using DiskArc's CircularBitBuffer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Array is indexed directly by quarter-track number (0 to QuarterTrackCount-1).
    /// For a 35-track disk, indices 0-136 represent positions 0.00 to 34.00.
    /// </para>
    /// <para>
    /// A null entry indicates an unwritten quarter track. When reading from a null
    /// quarter track, the drive returns random noise (MC3470 behavior).
    /// </para>
    /// <para>
    /// To convert between track numbers and quarter-track indices:
    /// <list type="bullet">
    /// <item>Quarter-track index for whole track N: N * 4</item>
    /// <item>Physical track from quarter-track index: index / 4</item>
    /// <item>Quarter fraction (0-3) from index: index % 4</item>
    /// </list>
    /// </para>
    /// </remarks>
    public CircularBitBuffer?[] QuarterTracks { get; }

    /// <summary>
    /// Bit count for each quarter-track (varies for WOZ, typically fixed 51200 for NIB/synthesized).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Standard Apple II tracks: 51,200 bits (6,400 bytes × 8)
    /// WOZ tracks: Variable, typically 50,000-52,000 bits
    /// </para>
    /// <para>
    /// Bit count is 0 for unwritten (null) quarter tracks.
    /// </para>
    /// </remarks>
    public int[] QuarterTrackBitCounts { get; }

    /// <summary>
    /// Number of physical tracks (typically 35 for DOS 3.3, up to 40 for some disks).
    /// </summary>
    public int PhysicalTrackCount { get; }

    /// <summary>
    /// Total number of quarter-track positions: (PhysicalTrackCount - 1) * 4 + 1.
    /// </summary>
    /// <remarks>
    /// For a 35-track disk: (35-1)*4+1 = 137 quarter-track positions (0-136).
    /// For a 40-track disk: (40-1)*4+1 = 157 quarter-track positions (0-156).
    /// </remarks>
    public int QuarterTrackCount => QuarterTracks.Length;

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
    /// Lock object acquired by the emulator write path and by the serializer.
    /// The emulator acquires this on every write bit; the serializer acquires it
    /// briefly to snapshot quarter-track data before compressing outside the lock.
    /// </summary>
    public object SerializationLock { get; } = new object();

    /// <summary>
    /// Display name for this disk image (e.g., "DOS 3.3 System Master").
    /// </summary>
    /// <remarks>
    /// Set when a disk is checked out from the project store. This is the primary
    /// user-visible identifier for project-based disks and does not depend on
    /// any filesystem path. Null for legacy filesystem-loaded disks (which use
    /// <see cref="SourceFilePath"/> instead).
    /// </remarks>
    public string? DiskImageName { get; set; }

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
    /// Calculates the number of quarter-track positions for a given physical track count.
    /// </summary>
    /// <param name="physicalTrackCount">Number of physical tracks (e.g., 35 or 40).</param>
    /// <returns>Number of quarter-track positions: (physicalTrackCount - 1) * 4 + 1.</returns>
    public static int CalculateQuarterTrackCount(int physicalTrackCount)
    {
        return (physicalTrackCount - 1) * QuartersPerTrack + 1;
    }

    /// <summary>
    /// Converts a physical track number to the quarter-track index for the whole track position.
    /// </summary>
    /// <param name="track">Physical track number (0-based).</param>
    /// <returns>Quarter-track index (track * 4).</returns>
    public static int TrackToQuarterTrackIndex(int track)
    {
        return track * QuartersPerTrack;
    }

    /// <summary>
    /// Converts a physical track and quarter fraction to a quarter-track index.
    /// </summary>
    /// <param name="track">Physical track number (0-based).</param>
    /// <param name="quarter">Quarter fraction (0-3, where 0=.00, 1=.25, 2=.50, 3=.75).</param>
    /// <returns>Quarter-track index.</returns>
    public static int TrackAndQuarterToIndex(int track, int quarter)
    {
        return track * QuartersPerTrack + quarter;
    }

    /// <summary>
    /// Extracts the physical track number from a quarter-track index.
    /// </summary>
    /// <param name="quarterTrackIndex">Quarter-track index.</param>
    /// <returns>Physical track number (quarterTrackIndex / 4).</returns>
    public static int QuarterTrackIndexToTrack(int quarterTrackIndex)
    {
        return quarterTrackIndex / QuartersPerTrack;
    }

    /// <summary>
    /// Extracts the quarter fraction (0-3) from a quarter-track index.
    /// </summary>
    /// <param name="quarterTrackIndex">Quarter-track index.</param>
    /// <returns>Quarter fraction (0-3).</returns>
    public static int QuarterTrackIndexToQuarter(int quarterTrackIndex)
    {
        return quarterTrackIndex % QuartersPerTrack;
    }

    /// <summary>
    /// Initializes a new internal disk image with the specified physical track count.
    /// </summary>
    /// <param name="physicalTrackCount">Number of physical tracks (typically 35 or 40).</param>
    /// <param name="standardTrackBitCount">Bit count for standard tracks (default 51200).</param>
    /// <remarks>
    /// Creates quarter-track storage with only the whole-track positions (0, 4, 8, 12...)
    /// initialized. Fractional quarter-track positions are left null (unwritten).
    /// </remarks>
    public InternalDiskImage(int physicalTrackCount = 35, int standardTrackBitCount = 51200)
    {
        if (physicalTrackCount <= 0 || physicalTrackCount > 40)
        {
            throw new ArgumentOutOfRangeException(nameof(physicalTrackCount), "Physical track count must be between 1 and 40.");
        }

        if (standardTrackBitCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(standardTrackBitCount), "Bit count must be positive.");
        }

        PhysicalTrackCount = physicalTrackCount;
        int quarterTrackCount = CalculateQuarterTrackCount(physicalTrackCount);

        QuarterTracks = new CircularBitBuffer?[quarterTrackCount];
        QuarterTrackBitCounts = new int[quarterTrackCount];

        // Initialize only whole-track positions (indices 0, 4, 8, 12...)
        // Fractional positions remain null (unwritten)
        for (int track = 0; track < physicalTrackCount; track++)
        {
            int quarterIndex = TrackToQuarterTrackIndex(track);
            QuarterTrackBitCounts[quarterIndex] = standardTrackBitCount;

            // Create backing byte array for track
            int byteCount = (standardTrackBitCount + 7) / 8; // Round up to nearest byte
            byte[] trackData = new byte[byteCount];
            QuarterTracks[quarterIndex] = new CircularBitBuffer(
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
    /// Initializes a new internal disk image with pre-allocated quarter tracks.
    /// </summary>
    /// <param name="physicalTrackCount">Number of physical tracks.</param>
    /// <param name="quarterTracks">Quarter-track data buffers (nullable for unwritten positions).</param>
    /// <param name="quarterTrackBitCounts">Bit counts for each quarter track.</param>
    /// <exception cref="ArgumentException">Thrown when arrays don't match expected length.</exception>
    public InternalDiskImage(int physicalTrackCount, CircularBitBuffer?[] quarterTracks, int[] quarterTrackBitCounts)
    {
        if (physicalTrackCount <= 0 || physicalTrackCount > 40)
        {
            throw new ArgumentOutOfRangeException(nameof(physicalTrackCount), "Physical track count must be between 1 and 40.");
        }

        int expectedQuarterTrackCount = CalculateQuarterTrackCount(physicalTrackCount);

        if (quarterTracks.Length != expectedQuarterTrackCount)
        {
            throw new ArgumentException(
                $"Quarter tracks array length ({quarterTracks.Length}) must match expected count ({expectedQuarterTrackCount}) for {physicalTrackCount} physical tracks.",
                nameof(quarterTracks));
        }

        if (quarterTrackBitCounts.Length != expectedQuarterTrackCount)
        {
            throw new ArgumentException(
                $"Bit counts array length ({quarterTrackBitCounts.Length}) must match expected count ({expectedQuarterTrackCount}) for {physicalTrackCount} physical tracks.",
                nameof(quarterTrackBitCounts));
        }

        PhysicalTrackCount = physicalTrackCount;
        QuarterTracks = quarterTracks;
        QuarterTrackBitCounts = quarterTrackBitCounts;
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
