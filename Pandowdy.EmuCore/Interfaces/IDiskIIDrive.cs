// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.EmuCore.Interfaces;

/// <summary>
/// Represents a physical Disk II drive with head positioning and disk I/O.
/// </summary>
/// <remarks>
/// <para>
/// This interface models the mechanical behavior of a Disk II drive unit.
/// The drive handles head stepping and bit-level I/O delegated to an
/// <see cref="IDiskImageProvider"/> for format-specific data access.
/// </para>
/// <para>
/// <strong>Motor State:</strong> Motor control is managed by the <see cref="Cards.DiskIIControllerCard"/>,
/// not individual drives. The controller has a single motor line that powers the currently selected drive.
/// Drives are passive mechanical devices that respond to head positioning commands and I/O operations
/// when the controller's motor is running.
/// </para>
/// <para>
/// <strong>Coordinate System:</strong> Track positions use quarter-track granularity (0-139)
/// where tracks 0-3 represent physical track 0, tracks 4-7 represent track 1, etc.
/// The <see cref="Track"/> property returns the fractional track (e.g., 17.25 for quarter-track 69).
/// </para>
/// </remarks>
public interface IDiskIIDrive : IRestartable
{
    /// <summary>
    /// Gets the drive identification name for debugging and logging.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Resets the drive state (head position preserved, motor state managed by controller).
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
    /// <remarks>
    /// <strong>Deprecated:</strong> Prefer <see cref="AdvanceAndReadBits"/> for proper timing.
    /// </remarks>
    bool? GetBit(ulong currentCycle);

    /// <summary>
    /// Advances the disk by the specified number of CPU cycles and returns bits read.
    /// </summary>
    /// <param name="elapsedCycles">CPU cycles elapsed since last call.</param>
    /// <param name="bits">Buffer to receive the bits read.</param>
    /// <returns>Number of bits actually read (may be 0 if not enough cycles for a full bit).</returns>
    /// <remarks>
    /// This method delegates to the disk image provider's incremental timing model.
    /// The provider maintains cycle remainder and track position state.
    /// </remarks>
    int AdvanceAndReadBits(double elapsedCycles, Span<bool> bits);

    /// <summary>
    /// Gets the optimal bit timing for the current disk (default 32 = 4µs/bit).
    /// </summary>
    /// <remarks>
    /// Returns 32 if no disk is inserted. For WOZ files, this value comes from
    /// the INFO chunk and may be 31, 32, or other values for copy-protected disks.
    /// </remarks>
    byte OptimalBitTiming { get; }

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
    /// Notifies the drive of motor state changes from the controller.
    /// </summary>
    /// <param name="motorOn">True if motor is turning on, false if turning off.</param>
    /// <param name="cycleCount">Current CPU cycle count when state changed.</param>
    /// <remarks>
    /// This allows the drive to notify its disk image provider to synchronize timing.
    /// Critical for per-provider cycle tracking to maintain independent rotational positions.
    /// </remarks>
    void NotifyMotorStateChanged(bool motorOn, ulong cycleCount);

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

    /// <summary>
    /// Gets the file path of the currently inserted disk image, or null if no disk is inserted.
    /// </summary>
    /// <remarks>
    /// This property allows the controller to coordinate disk swapping and other disk management
    /// operations by querying which disk is currently in each drive.
    /// </remarks>
    string? CurrentDiskPath { get; }

    /// <summary>
    /// Gets or sets the internal disk image provider for this drive.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Internal API:</strong> This property is intended for use by
    /// <see cref="Cards.DiskIIControllerCard"/> for operations that require direct
    /// provider access (e.g., swapping disk media between drives).
    /// </para>
    /// <para>
    /// External code should use <see cref="InsertDisk"/> and <see cref="EjectDisk"/> instead
    /// of manipulating the provider directly.
    /// </para>
    /// </remarks>
    IDiskImageProvider? ImageProvider { get; set; }

    /// <summary>
    /// Gets the internal disk image, if available.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Internal API:</strong> This property is intended for use by
    /// <see cref="Cards.DiskIIControllerCard"/> to access dirty flags and destination
    /// paths for status reporting.
    /// </para>
    /// <para>
    /// Returns null if no disk is inserted, or if the drive is using a provider that doesn't
    /// wrap an <see cref="DiskII.InternalDiskImage"/> (e.g., direct file-based providers).
    /// </para>
    /// </remarks>
    DiskII.InternalDiskImage? InternalImage { get; }
}
