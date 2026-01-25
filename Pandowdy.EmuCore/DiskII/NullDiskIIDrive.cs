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
/// <item>Tracks motor and position state for testing purposes</item>
/// </list>
/// </para>
/// </remarks>
public class NullDiskIIDrive : IDiskIIDrive
{
    private bool _motor;
    private int _quarterSteps;

    /// <inheritdoc />
    public string Name { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NullDiskIIDrive"/> class.
    /// </summary>
    /// <param name="name">The display name for this drive. Defaults to "NullDrive".</param>
    public NullDiskIIDrive(string name = "NullDrive")
    {
        Name = name;
        Reset();
    }

    /// <inheritdoc />
    public void Reset()
    {
        // Start at track 17 (middle of disk) - common boot track
        _quarterSteps = 4 * 17;
        MotorOn = false;
    }

    /// <inheritdoc />
    public bool MotorOn
    {
        get => _motor;
        set => _motor = value;
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
