// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Diagnostics;
using Pandowdy.EmuCore.DiskII.Providers;
using Pandowdy.EmuCore.Machine;

namespace Pandowdy.EmuCore.DiskII;

/// <summary>
/// Implements a Disk II floppy disk drive with mechanical head positioning.
/// </summary>
/// <remarks>
/// <para>
/// This class simulates the physical/mechanical characteristics of a Disk II drive:
/// <list type="bullet">
/// <item>Stepper motor with quarter-track positioning (0-139 quarter-tracks = 0-34.75 whole tracks)</item>
/// <item>Read/write operations through an <see cref="IDiskImageProvider"/></item>
/// <item>Disk insert/eject operations</item>
/// </list>
/// </para>
/// <para>
/// <strong>Motor Control:</strong> Motor state is managed by the <see cref="DiskIIControllerCard"/>,
/// not individual drives. The controller has a single motor line that powers the currently selected drive.
/// This drive is a passive mechanical device that responds to head positioning and I/O operations
/// when the controller's motor is running.
/// </para>
/// <para>
/// The drive delegates actual bit reading/writing to the <see cref="IDiskImageProvider"/>,
/// while managing the mechanical aspects (head position). This separation
/// matches real hardware where the drive mechanism is separate from the disk media.
/// </para>
/// <para>
/// <strong>Status Updates:</strong> For UI integration, wrap this drive with
/// <see cref="DiskIIStatusDecorator"/> which synchronizes state changes with
/// the <see cref="IDiskStatusMutator"/>.
/// </para>
/// </remarks>
public class DiskIIDrive : IDiskIIDrive
{
    private IDiskImageProvider? _imageProvider;
    private readonly IDiskImageFactory? _diskImageFactory;
    private int _quarterSteps;
    private bool _hitMinLogged;
    private bool _hitMaxLogged;

    /// <summary>
    /// Gets or sets the image provider for this drive (internal accessor for swap support).
    /// </summary>
    /// <remarks>
    /// This property is used by <see cref="DiskIIControllerCard"/> to swap
    /// disk media between drives. The controller manages the swap operation directly
    /// since it owns the drive array.
    /// </remarks>
    public IDiskImageProvider? ImageProvider
    {
        get => _imageProvider;
        set => _imageProvider = value;
    }

    /// <summary>
    /// Gets the internal disk image from the current provider (for dirty/destination tracking).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property provides access to the <see cref="InternalDiskImage"/> for reading
    /// dirty state and destination path information. Used by <see cref="DiskIIStatusDecorator"/>
    /// to propagate these fields to the UI via <see cref="IDiskStatusMutator"/>.
    /// </para>
    /// <para>
    /// Returns null if no disk is inserted or if the provider is not a
    /// <see cref="UnifiedDiskImageProvider"/>.
    /// </para>
    /// </remarks>
    public InternalDiskImage? InternalImage
    {
        get
        {
            if (_imageProvider is UnifiedDiskImageProvider unifiedProvider)
            {
                return unifiedProvider.InternalImage;
            }
            return null;
        }
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DiskIIDrive"/> class.
    /// </summary>
    /// <param name="name">Name for the drive (e.g., "Slot6-D1").</param>
    /// <param name="imageProvider">Optional disk image provider. If null, the drive behaves as if no disk is inserted.</param>
    /// <param name="diskImageFactory">Optional factory for creating disk image providers when inserting disks.</param>
    public DiskIIDrive(
        string name,
        IDiskImageProvider? imageProvider = null,
        IDiskImageFactory? diskImageFactory = null)
    {
        Name = name ?? "Unnamed";
        _imageProvider = imageProvider;
        _diskImageFactory = diskImageFactory;

        // Initialize head to track 17 (typical boot track area)
        // This is only done on drive creation, not on Reset()
        _quarterSteps = 4 * 17;

        // Notify image provider of initial track position
        _imageProvider?.SetQuarterTrack(_quarterSteps);
    }

    /// <inheritdoc />
    public void InsertDisk(string diskImagePath)
    {
        if (_diskImageFactory == null)
        {
            throw new InvalidOperationException("Cannot insert disk: no disk image factory available");
        }

        // Eject current disk if any
        EjectDisk();

        // Load new disk
        _imageProvider = _diskImageFactory.CreateProvider(diskImagePath);
        _imageProvider.SetQuarterTrack(_quarterSteps);

        Debug.WriteLine($"Drive '{Name}': Inserted disk '{diskImagePath}'");
    }

    /// <inheritdoc />
    public void EjectDisk()
    {
        if (_imageProvider != null)
        {
            // Flush any pending writes and dispose
            _imageProvider.Flush();
            _imageProvider.Dispose();
            _imageProvider = null;

            Debug.WriteLine($"Drive '{Name}': Ejected disk");
        }
    }

    /// <inheritdoc />
    public bool HasDisk => _imageProvider != null;

    /// <inheritdoc />
    public void Reset()
    {
        // Per interface contract: head position preserved
        // Head position (_quarterSteps) is intentionally NOT reset - it represents
        // the physical head location which doesn't change on system reset
        // Motor control is now handled at controller level
        _hitMinLogged = false;
        _hitMaxLogged = false;
    }

    /// <summary>
    /// Restores the drive to its initial construction-time state (cold boot).
    /// Head returns to track 17 (construction default), diagnostic flags cleared.
    /// Disk media stays inserted.
    /// </summary>
    public void Restart()
    {
        _quarterSteps = 4 * 17;
        _hitMinLogged = false;
        _hitMaxLogged = false;
        _imageProvider?.SetQuarterTrack(_quarterSteps);
    }

    /// <inheritdoc />
    public double Track => _quarterSteps / 4.0;

    /// <inheritdoc />
    public int QuarterTrack => _quarterSteps;

    /// <inheritdoc />
    public void StepToHigherTrack()
    {
        _quarterSteps++;

        if (_quarterSteps > DiskIIConstants.MaxQuarterTracks)
        {
            _quarterSteps = DiskIIConstants.MaxQuarterTracks;
            if (!_hitMaxLogged)
            {
#if ControllerDebug
                Debug.WriteLine($"Drive '{Name}' head hit maximum position at quarter-track {_quarterSteps}");
#endif
                _hitMaxLogged = true;
            }
        }
        else
        {
            _hitMaxLogged = false;
        }

        _hitMinLogged = false;

        // Notify image provider of track change
        _imageProvider?.SetQuarterTrack(_quarterSteps);
    }

    /// <inheritdoc />
    public void StepToLowerTrack()
    {
        _quarterSteps--;

        if (_quarterSteps < 0)
        {
            _quarterSteps = 0;
            if (!_hitMinLogged)
            {
#if ControllerDebug
                Debug.WriteLine($"Drive '{Name}' head hit minimum position at quarter-track 0");
#endif
                _hitMinLogged = true;
            }
        }
        else
        {
            _hitMinLogged = false;
        }

        _hitMaxLogged = false;

        // Notify image provider of track change
        _imageProvider?.SetQuarterTrack(_quarterSteps);
    }

    /// <inheritdoc />
    public bool? GetBit(ulong currentCycle)
    {
        // Motor control is at controller level - controller only calls this when motor is running
        if (_imageProvider == null)
        {
            return null;
        }

        return _imageProvider.GetBit(currentCycle);
    }

    /// <inheritdoc />
    public bool SetBit(bool value)
    {
        // Motor control is at controller level - controller only calls this when motor is running
        if (_imageProvider == null)
        {
            return false;
        }

        // Delegate to image provider (will return false if write-protected)
        return _imageProvider.WriteBit(value, 0); // cycleCount not used yet
    }

    /// <inheritdoc />
    public bool IsWriteProtected()
    {
        // No disk inserted = not write protected (controller sees no disk)
        if (_imageProvider == null)
        {
            return false;
        }

        // Delegate to image provider
        return _imageProvider.IsWriteProtected;
    }

    /// <inheritdoc />
    public void NotifyMotorStateChanged(bool motorOn, ulong cycleCount)
    {
        // Forward motor state notification to disk image provider
        _imageProvider?.NotifyMotorStateChanged(motorOn, cycleCount);
    }

    /// <inheritdoc />
    public int AdvanceAndReadBits(double elapsedCycles, Span<bool> bits)
    {
        // No disk inserted = no bits read
        if (_imageProvider == null)
        {
            return 0;
        }

        // Delegate to image provider's incremental timing model
        return _imageProvider.AdvanceAndReadBits(elapsedCycles, bits);
    }

    /// <inheritdoc />
    public byte OptimalBitTiming => _imageProvider?.OptimalBitTiming ?? 32;

    /// <inheritdoc />
    public string? CurrentDiskPath
    {
        get
        {
            if (_imageProvider is UnifiedDiskImageProvider unifiedProvider)
            {
                var image = unifiedProvider.InternalImage;
                if (image == null)
                {
                    return null;
                }

                // Prefer DiskImageName (project-based) over SourceFilePath (filesystem-based)
                return image.DiskImageName ?? image.SourceFilePath;
            }
            return null;
        }
    }
}
