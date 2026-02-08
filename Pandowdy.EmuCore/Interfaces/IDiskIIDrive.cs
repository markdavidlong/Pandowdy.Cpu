// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.EmuCore.Interfaces;

/// <summary>
/// Represents a physical Disk II drive with motor control, head positioning, and disk I/O.
/// </summary>
/// <remarks>
/// <para>
/// This interface models the mechanical and electrical behavior of a Disk II drive unit.
/// The drive handles motor control, head stepping, and bit-level I/O delegated to an
/// <see cref="IDiskImageProvider"/> for format-specific data access.
/// </para>
/// <para>
/// <strong>Coordinate System:</strong> Track positions use quarter-track granularity (0-139)
/// where tracks 0-3 represent physical track 0, tracks 4-7 represent track 1, etc.
/// The <see cref="Track"/> property returns the fractional track (e.g., 17.25 for quarter-track 69).
/// </para>
/// </remarks>
public interface IDiskIIDrive
{
    /// <summary>
    /// Gets the drive identification name for debugging and logging.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Resets the drive state (motor off, head position preserved).
    /// </summary>
    void Reset();

    // PHASE 4: MotorOn removed - motor state is now controller-level, not drive-level
    // /// <summary>
    // /// Gets or sets the motor state. Motor must be on to read or write data.
    // /// </summary>
    // bool MotorOn { get; set; }

    /// <summary>
    /// Gets the current track position as a fractional value (0.00 to 34.75).
    /// </summary>
    double Track { get; }

    /// <summary>
    /// Gets the raw quarter-track position (0-139) for stepper motor calculations.
    /// </summary>
    int QuarterTrack { get; }

    /// <summary>
    /// Moves the head toward higher track numbers in quarter-track increments.
    /// </summary>
    void StepToHigherTrack();

    /// <summary>
    /// Moves the head toward lower track numbers in quarter-track increments.
    /// </summary>
    void StepToLowerTrack();

    /// <summary>
    /// Reads the next bit from the disk at the current position.
    /// </summary>
    /// <param name="currentCycle">Current CPU cycle count for timing.</param>
    /// <returns>The bit value, or null if no disk is inserted or read fails.</returns>
    bool? GetBit(ulong currentCycle);

    /// <summary>
    /// Writes a bit to the disk at the current position.
    /// </summary>
    /// <param name="value">The bit value to write.</param>
    /// <returns>True if write succeeded, false if disk is write-protected or no disk.</returns>
    bool SetBit(bool value);

    /// <summary>
    /// Checks if the current disk is write-protected.
    /// </summary>
    /// <returns>True if write-protected or no disk inserted.</returns>
    bool IsWriteProtected();

    /// <summary>
    /// Inserts a disk image into the drive.
    /// </summary>
    /// <param name="diskImagePath">Path to the disk image file.</param>
    void InsertDisk(string diskImagePath);

    /// <summary>
    /// Ejects the current disk from the drive.
    /// </summary>
    void EjectDisk();

    /// <summary>
    /// Gets whether a disk is currently inserted in the drive.
    /// </summary>
    bool HasDisk { get; }
}
