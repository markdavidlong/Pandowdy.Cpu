// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.DiskII;

/// <summary>
/// Null object implementation of <see cref="IDiskIIDrive"/>.
/// Provides a functional but no-op disk drive for testing and empty slots.
/// </summary>
/// <remarks>
/// <para>
/// This class implements the Null Object pattern, providing a safe default
/// drive implementation that can be used when no physical drive is present.
/// All operations are safe to call but have no effect on disk data.
/// </para>
/// <para>
/// The null drive:
/// <list type="bullet">
/// <item>Never has a disk inserted (<see cref="HasDisk"/> always returns false)</item>
/// <item>Always returns null from <see cref="GetBit"/> (no data available)</item>
/// <item>Ignores write operations (<see cref="SetBit"/> always returns false)</item>
/// <item>Tracks position state for testing purposes</item>
/// </list>
/// </para>
/// <para>
/// <strong>Motor Control:</strong> Motor state is managed by the <see cref="Cards.DiskIIControllerCard"/>,
/// not individual drives. This null drive is a passive device like all drives.
/// </para>
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="NullDiskIIDrive"/> class.
/// </remarks>
/// <param name="name">The display name for this drive. Defaults to "NullDrive".</param>
public class NullDiskIIDrive(string name = "NullDrive") : IDiskIIDrive
{
    private int _quarterSteps = 4 * 17;

    /// <inheritdoc />
    public string Name { get; } = name;

    /// <inheritdoc />
    public void Reset()
    {
        // Per interface contract: head position preserved
        // Head position (_quarterSteps) is intentionally NOT reset
        // Motor control is now handled at controller level
    }

    /// <inheritdoc />
    public double Track => _quarterSteps / 4.0;

    /// <inheritdoc />
    public int QuarterTrack => _quarterSteps;

    /// <summary>
    /// Gets the current bit position within the track.
    /// Always returns 0 for the null drive.
    /// </summary>
    public static int BitPosition => 0;

    /// <inheritdoc />
    public void StepToHigherTrack()
    {
        _quarterSteps++;
        if (_quarterSteps > DiskIIConstants.MaxQuarterTracks)
        {
            _quarterSteps = DiskIIConstants.MaxQuarterTracks;
        }
    }

    /// <inheritdoc />
    public void StepToLowerTrack()
    {
        _quarterSteps--;
        if (_quarterSteps < 0)
        {
            _quarterSteps = 0;
        }
    }

    /// <inheritdoc />
    /// <returns>Always returns null since no disk is present.</returns>
    public bool? GetBit(ulong currentCycle)
    {
        return null;
    }

    /// <inheritdoc />
    /// <returns>Always returns false since no disk is present to write to.</returns>
    public bool SetBit(bool value)
    {
        return false;
    }

    /// <inheritdoc />
    /// <returns>Always returns false since no disk is present.</returns>
    public bool IsWriteProtected()
    {
        return false;
    }

    /// <inheritdoc />
    /// <remarks>No-op: Null drive doesn't support disk insertion.</remarks>
    public void InsertDisk(string diskImagePath)
    {
        // No-op: Null drive doesn't support disk insertion
    }

    /// <inheritdoc />
    /// <remarks>No-op: Null drive doesn't have a disk to eject.</remarks>
    public void EjectDisk()
    {
        // No-op: Null drive doesn't have a disk to eject
    }

    /// <inheritdoc />
    /// <returns>Always returns false since no disk is ever present.</returns>
    public bool HasDisk => false;
}
