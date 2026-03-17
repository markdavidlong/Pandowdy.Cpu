// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Diagnostics;
using Pandowdy.EmuCore.Machine;

namespace Pandowdy.EmuCore.DiskII;

/// <summary>
/// Decorator for <see cref="IDiskIIDrive"/> that adds debug logging to all operations.
/// </summary>
/// <remarks>
/// <para>
/// This decorator wraps any <see cref="IDiskIIDrive"/> implementation and logs all
/// method calls and property accesses to the debug output. Useful for debugging
/// disk access patterns during development.
/// </para>
/// <para>
/// <strong>Usage:</strong> Wrap the core drive in the factory:
/// <code>
/// var coreDrive = new DiskIIDrive(name, telemetry, slot, drive);
/// return new DiskIIDebugDecorator(coreDrive);
/// </code>
/// </para>
/// <para>
/// <strong>Performance Note:</strong> GetBit logging is disabled by default to avoid
/// excessive output during disk reads. Uncomment the logging in GetBit if detailed
/// bit-level debugging is needed.
/// </para>
/// </remarks>
public class DiskIIDebugDecorator : IDiskIIDrive
{
    private readonly IDiskIIDrive _inner;

    /// <summary>
    /// Gets the inner drive for unwrapping decorator chains (internal use only).
    /// </summary>
    internal IDiskIIDrive InnerDrive => _inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiskIIDebugDecorator"/> class.
    /// </summary>
    /// <param name="inner">The drive implementation to wrap.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inner"/> is null.</exception>
    public DiskIIDebugDecorator(IDiskIIDrive inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
        Debug.WriteLine($"Creating DiskIIDebugDecorator for drive '{Name}'");
    }

    /// <inheritdoc />
    public string Name => _inner.Name;

    /// <inheritdoc />
    public int QuarterTrack => _inner.QuarterTrack;

    /// <inheritdoc />
    public double Track
    {
        get
        {
            var retval = _inner.Track;
            // Debug.WriteLine($"IDiskIIDrive ({Name}) Track = {retval}");
            return retval;
        }
    }

    /// <inheritdoc />
    public bool HasDisk => _inner.HasDisk;

    /// <inheritdoc />
    public void Reset()
    {
        Debug.WriteLine($"IDiskIIDrive ({Name}): Reset()");
        _inner.Reset();
    }

    /// <inheritdoc />
    public void Restart()
    {
        Debug.WriteLine($"IDiskIIDrive ({Name}): Restart()");
        _inner.Restart();
    }

    /// <inheritdoc />
    public void StepToHigherTrack()
    {
        Debug.WriteLine($"IDiskIIDrive ({Name}) StepToHigherTrack()");
        _inner.StepToHigherTrack();
        var track = _inner.Track;
        if ((int)track >= DiskIIConstants.TrackCount)
        {
            Debug.WriteLine($" (Head hit max range at {track})");
        }
    }

    /// <inheritdoc />
    public void StepToLowerTrack()
    {
        Debug.WriteLine($"IDiskIIDrive ({Name}) StepToLowerTrack()");
        _inner.StepToLowerTrack();
        if (_inner.QuarterTrack == 0)
        {
            Debug.WriteLine(" (Head hit min range at track 0)");
        }
    }

    /// <inheritdoc />
    public bool? GetBit(ulong currentCycle)
    {
        var val = _inner.GetBit(currentCycle);
        // Disabled to reduce spam - uncomment if needed for detailed bit debugging
        // if (val == null)
        // {
        //     Debug.WriteLine($"IDiskIIDrive ({Name}) GetBit(cycle={currentCycle}) => NULL (Motor:{_inner.MotorOn}, Track:{_inner.Track})");
        // }
        // else
        // {
        //     Debug.WriteLine($"IDiskIIDrive ({Name}) GetBit(cycle={currentCycle}) => {(val.Value ? "1" : "0")}");
        // }
        return val;
    }

    /// <inheritdoc />
    public bool SetBit(bool value)
    {
        Debug.WriteLine($"IDiskIIDrive ({Name}) SetBit({value})");
        return _inner.SetBit(value);
    }

    /// <inheritdoc />
    public bool IsWriteProtected()
    {
        Debug.WriteLine($"IDiskIIDrive ({Name}) IsWriteProtected()");
        return _inner.IsWriteProtected();
    }

    /// <inheritdoc />
    public void NotifyMotorStateChanged(bool motorOn, ulong cycleCount)
    {
        Debug.WriteLine($"IDiskIIDrive ({Name}) NotifyMotorStateChanged(motorOn={motorOn}, cycle={cycleCount})");
        _inner.NotifyMotorStateChanged(motorOn, cycleCount);
    }

    /// <inheritdoc />
    public int AdvanceAndReadBits(double elapsedCycles, Span<bool> bits)
    {
        // Disabled to reduce spam - uncomment if needed for detailed bit debugging
        // Debug.WriteLine($"IDiskIIDrive ({Name}) AdvanceAndReadBits(cycles={elapsedCycles:F2}, bufferSize={bits.Length})");
        return _inner.AdvanceAndReadBits(elapsedCycles, bits);
    }

    /// <inheritdoc />
    public byte OptimalBitTiming => _inner.OptimalBitTiming;

    /// <inheritdoc />
    public void InsertDisk(string diskImagePath)
    {
        Debug.WriteLine($"IDiskIIDrive ({Name}) InsertDisk('{diskImagePath}')");
        _inner.InsertDisk(diskImagePath);
    }

    /// <inheritdoc />
    public void EjectDisk()
    {
        Debug.WriteLine($"IDiskIIDrive ({Name}) EjectDisk()");
        _inner.EjectDisk();
    }

    /// <inheritdoc />
    public string? CurrentDiskPath
    {
        get
        {
            var path = _inner.CurrentDiskPath;
            // Debug.WriteLine($"IDiskIIDrive ({Name}) CurrentDiskPath => {path ?? "(none)"}");
            return path;
        }
    }

    /// <summary>
    /// Gets or sets the internal disk image provider.
    /// </summary>
    /// <remarks>
    /// Delegates to the inner drive's provider.
    /// </remarks>
    public IDiskImageProvider? ImageProvider
    {
        get => _inner.ImageProvider;
        set
        {
            Debug.WriteLine($"IDiskIIDrive ({Name}) Setting ImageProvider to {(value != null ? "non-null" : "null")}");
            _inner.ImageProvider = value;
        }
    }

    /// <summary>
    /// Gets the internal disk image.
    /// </summary>
    /// <remarks>
    /// Delegates to the inner drive's internal image.
    /// </remarks>
    public InternalDiskImage? InternalImage => _inner.InternalImage;
}
