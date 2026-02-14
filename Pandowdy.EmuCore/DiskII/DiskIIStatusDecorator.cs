// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Services;

namespace Pandowdy.EmuCore.DiskII;

/// <summary>
/// Decorator that synchronizes disk drive state changes with the global disk status provider.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Decorator Pattern:</strong> This class wraps an <see cref="IDiskIIDrive"/> implementation
/// and intercepts all state-changing operations to automatically update the
/// <see cref="IDiskStatusMutator"/> with current drive state.
/// </para>
/// <para>
/// <strong>Layering:</strong> Typically sits between the actual drive implementation and the
/// <see cref="DiskIIDebugDecorator"/>:
/// <code>
/// DiskIIDebugDecorator â†’ DiskIIStatusDecorator â†’ DiskIIDrive
/// </code>
/// This ensures status updates happen before debug logging.
/// </para>
/// <para>
/// <strong>Update Strategy:</strong> Updates occur immediately after the wrapped drive's state
/// changes, ensuring the status provider always reflects current reality. This is safe because
/// all drive operations execute on the emulator thread (single-threaded execution model).
/// </para>
/// </remarks>
public class DiskIIStatusDecorator : IDiskIIDrive
{
    private readonly IDiskIIDrive _innerDrive;
    private readonly IDiskStatusMutator _statusMutator;
    private readonly int _slotNumber;
    private readonly int _driveNumber;

    /// <summary>
    /// Gets the inner drive for unwrapping decorator chains (internal use only).
    /// </summary>
    internal IDiskIIDrive InnerDrive => _innerDrive;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiskIIStatusDecorator"/> class.
    /// </summary>
    /// <param name="innerDrive">The actual drive implementation to wrap.</param>
    /// <param name="statusMutator">Status provider for updating drive state.</param>
    /// <param name="slotNumber">Slot number (1-7) where the controller resides.</param>
    /// <param name="driveNumber">Drive number (1-2) on the controller.</param>
    public DiskIIStatusDecorator(
        IDiskIIDrive innerDrive,
        IDiskStatusMutator statusMutator,
        int slotNumber,
        int driveNumber)
    {
        _innerDrive = innerDrive;
        _statusMutator = statusMutator;
        _slotNumber = slotNumber;
        _driveNumber = driveNumber;

        // Register this drive with the status provider
        _statusMutator.RegisterDrive(slotNumber, driveNumber);

        // Initialize status with current drive state
        SyncStatus();
    }

    #region IDiskIIDrive - Delegated Properties

    /// <inheritdoc />
    public string Name => _innerDrive.Name;

    /// <inheritdoc />
    public bool HasDisk => _innerDrive.HasDisk;

    /// <inheritdoc />
    public double Track => _innerDrive.Track;

    /// <inheritdoc />
    public int QuarterTrack => _innerDrive.QuarterTrack;

    #endregion

    #region IDiskIIDrive - State-Changing Methods with Status Updates

    /// <inheritdoc />
    public void InsertDisk(string diskImagePath)
    {
        _innerDrive.InsertDisk(diskImagePath);

        // Update status after disk insertion
        _statusMutator.MutateDrive(_slotNumber, _driveNumber, builder =>
        {
            builder.DiskImagePath = diskImagePath;
            builder.DiskImageFilename = System.IO.Path.GetFileName(diskImagePath);
            builder.IsReadOnly = _innerDrive.IsWriteProtected();
            builder.HasValidTrackData = true; // Assume valid after insertion

            // Get dirty/destination state from internal image
            if (_innerDrive is DiskIIDrive concreteDrive)
            {
                var internalImage = concreteDrive.InternalImage;
                builder.IsDirty = internalImage?.IsDirty ?? false;
                builder.HasDestinationPath = !string.IsNullOrEmpty(internalImage?.DestinationFilePath);
            }
        });
    }

    /// <inheritdoc />
    public void EjectDisk()
    {
        _innerDrive.EjectDisk();

        // Clear disk info in status
        _statusMutator.MutateDrive(_slotNumber, _driveNumber, builder =>
        {
            builder.DiskImagePath = string.Empty;
            builder.DiskImageFilename = string.Empty;
            builder.IsReadOnly = false;
            builder.HasValidTrackData = false;
            builder.IsDirty = false;
            builder.HasDestinationPath = false;
        });
    }

    /// <inheritdoc />
    public void Reset()
    {
        _innerDrive.Reset();

        // Sync full state after reset
        SyncStatus();
    }

    /// <inheritdoc />
    public void StepToHigherTrack()
    {
        _innerDrive.StepToHigherTrack();

        // Update track position in status
        _statusMutator.MutateDrive(_slotNumber, _driveNumber, builder =>
        {
            builder.Track = _innerDrive.Track;
        });
    }

    /// <inheritdoc />
    public void StepToLowerTrack()
    {
        _innerDrive.StepToLowerTrack();

        // Update track position in status
        _statusMutator.MutateDrive(_slotNumber, _driveNumber, builder =>
        {
            builder.Track = _innerDrive.Track;
        });
    }

    /// <inheritdoc />
    public bool? GetBit(ulong currentCycle)
    {
        // Read-only operation, no status update needed
        return _innerDrive.GetBit(currentCycle);
    }

    /// <inheritdoc />
    public bool SetBit(bool value)
    {
        // Write operation - delegates to inner drive
        return _innerDrive.SetBit(value);
    }

    /// <inheritdoc />
    public bool IsWriteProtected()
    {
        // Read-only operation, no status update needed
        return _innerDrive.IsWriteProtected();
    }

    /// <inheritdoc />
    public void NotifyMotorStateChanged(bool motorOn, ulong cycleCount)
    {
        // Forward to inner drive - no status update needed (motor state is controller-level)
        _innerDrive.NotifyMotorStateChanged(motorOn, cycleCount);
    }

    /// <inheritdoc />
    public int AdvanceAndReadBits(double elapsedCycles, Span<bool> bits)
    {
        // Read-only operation, no status update needed
        return _innerDrive.AdvanceAndReadBits(elapsedCycles, bits);
    }

    /// <inheritdoc />
    public byte OptimalBitTiming => _innerDrive.OptimalBitTiming;

    /// <inheritdoc />
    public string? CurrentDiskPath => _innerDrive.CurrentDiskPath;

    /// <summary>
    /// Gets or sets the internal disk image provider.
    /// </summary>
    /// <remarks>
    /// Delegates to the inner drive's provider.
    /// </remarks>
    public IDiskImageProvider? ImageProvider
    {
        get => _innerDrive.ImageProvider;
        set => _innerDrive.ImageProvider = value;
    }

    /// <summary>
    /// Gets the internal disk image.
    /// </summary>
    /// <remarks>
    /// Delegates to the inner drive's internal image.
    /// </remarks>
    public InternalDiskImage? InternalImage => _innerDrive.InternalImage;

    #endregion

    /// <summary>
    /// Synchronizes the full drive state with the status provider.
    /// Called during initialization and after reset.
    /// </summary>
    private void SyncStatus()
    {
        _statusMutator.MutateDrive(_slotNumber, _driveNumber, builder =>
        {
            // PHASE 4: MotorOn removed - motor state is controller-level, not drive-level
            builder.Track = _innerDrive.Track;
            builder.IsReadOnly = _innerDrive.IsWriteProtected();
            builder.HasValidTrackData = _innerDrive.HasDisk;

            // Get dirty/destination state from internal image (via interface)
            var internalImage = _innerDrive.InternalImage;
            builder.IsDirty = internalImage?.IsDirty ?? false;
            builder.HasDestinationPath = !string.IsNullOrEmpty(internalImage?.DestinationFilePath);

            // Note: DiskImagePath/Filename are not exposed by IDiskIIDrive interface
            // These will be set when InsertDisk() is called
            // Phase state and MotorOffScheduled are managed by the controller, not the drive
        });
    }
}
