// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.EmuCore.Services;

/// <summary>
/// Immutable snapshot of a single disk drive's status.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Complete Drive State:</strong> Captures all relevant state for a single
/// disk drive at a specific point in time, including mechanical state (track, motor),
/// disk image information, and phase magnet status.
/// </para>
/// <para>
/// <strong>Phase State Encoding:</strong> The phase byte uses the low nibble (bits 0-3)
/// to represent the four stepper motor phases:
/// <list type="bullet">
/// <item>Bit 0: Phase 0 (outermost track direction)</item>
/// <item>Bit 1: Phase 1</item>
/// <item>Bit 2: Phase 2</item>
/// <item>Bit 3: Phase 3 (innermost track direction)</item>
/// </list>
/// Multiple bits can be set simultaneously, representing the stepper motor's
/// current magnetic field state for precise quarter-track positioning.
/// </para>
/// </remarks>
/// <param name="SlotNumber">Expansion slot number (1-7) where the controller resides.</param>
/// <param name="DriveNumber">Drive number on the controller (1-2).</param>
/// <param name="DiskImagePath">Full path to the loaded disk image file, or empty if no disk.</param>
/// <param name="DiskImageFilename">Filename only of the disk image, or empty if no disk.</param>
/// <param name="IsReadOnly">True if the disk image is write-protected or read-only.</param>
/// <param name="Track">Current track position (0.00-34.75 with quarter-track precision).</param>
/// <param name="Sector">Current sector number (0-15 for 16-sector disks, -1 if unknown).</param>
/// <param name="MotorOn">True if the drive motor is currently running.</param>
/// <param name="MotorOffScheduled">True if motor-off has been requested but delayed (~1 second).</param>
/// <param name="PhaseState">Stepper motor phase state (low nibble: bits 3-0 = phases 3-0).</param>
/// <param name="HasValidTrackData">True if the current track position has valid disk data.</param>
/// <param name="IsDirty">True if the disk has been modified since load or last save.</param>
/// <param name="HasDestinationPath">True if the disk has an attached destination path for Save operations.</param>
public record DiskDriveStatusSnapshot(
    int SlotNumber,
    int DriveNumber,
    string DiskImagePath,
    string DiskImageFilename,
    bool IsReadOnly,
    double Track,
    int Sector,
    bool MotorOn,
    bool MotorOffScheduled,
    byte PhaseState,
    bool HasValidTrackData,
    bool IsDirty,
    bool HasDestinationPath
)
{
    /// <summary>
    /// Gets the disk identifier in the format "SxDx" (e.g., "S6D1" for Slot 6, Drive 1).
    /// </summary>
    /// <remarks>
    /// This computed property provides a concise, human-readable identifier for UI display
    /// and logging. The format matches common Apple II documentation conventions.
    /// </remarks>
    public string DiskId => $"S{SlotNumber}D{DriveNumber}";
}

/// <summary>
/// Immutable snapshot of all disk drives in the system.
/// </summary>
/// <remarks>
/// <para>
/// <strong>System-Wide View:</strong> Provides a complete, consistent snapshot of all
/// disk drives at a single point in time. The array is indexed by a computed drive ID
/// to allow direct lookup: <c>driveId = (slot * 2) + drive</c>.
/// </para>
/// <para>
/// <strong>Empty Drives:</strong> Drives without inserted disks are represented with
/// empty paths and filenames but retain their mechanical state (track position, motor status).
/// This matches real hardware behavior where the drive mechanism maintains state
/// independently of the disk media.
/// </para>
/// </remarks>
/// <param name="Drives">Array of drive statuses, indexed by (slot * 2) + drive.</param>
public record DiskStatusSnapshot(
    DiskDriveStatusSnapshot[] Drives
);

/// <summary>
/// Read-only interface for observing disk status across all drives.
/// </summary>
/// <remarks>
/// Provides direct property access and observable streams for monitoring disk
/// drive state changes. Used by UI, debuggers, and diagnostic tools.
/// </remarks>
public interface IDiskStatusProvider
{
    /// <summary>
    /// Gets the current immutable snapshot of all disk drive states.
    /// </summary>
    DiskStatusSnapshot Current { get; }

    /// <summary>
    /// Gets an observable stream that emits new snapshots when any drive state changes.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="System.Reactive.Subjects.BehaviorSubject{T}"/> to replay
    /// current state to new subscribers.
    /// </remarks>
    IObservable<DiskStatusSnapshot> Stream { get; }

    /// <summary>
    /// Event fired when any drive state changes.
    /// </summary>
    event EventHandler<DiskStatusSnapshot>? Changed;

    /// <summary>
    /// Gets the status of a specific drive by slot and drive number.
    /// </summary>
    /// <param name="slotNumber">Slot number (1-7).</param>
    /// <param name="driveNumber">Drive number (1-2).</param>
    /// <returns>Drive status snapshot, or null if slot/drive doesn't exist.</returns>
    DiskDriveStatusSnapshot? GetDriveStatus(int slotNumber, int driveNumber);
}

/// <summary>
/// Mutation interface for updating disk drive status.
/// </summary>
/// <remarks>
/// Provides methods for disk controller cards and drives to update their state.
/// Mutations create new immutable snapshots and notify all observers.
/// </remarks>
public interface IDiskStatusMutator : IDiskStatusProvider
{
    /// <summary>
    /// Registers a new drive in the system.
    /// </summary>
    /// <param name="slotNumber">Slot number (1-7).</param>
    /// <param name="driveNumber">Drive number (1-2).</param>
    /// <remarks>
    /// Should be called when a drive is created by the factory. Only registered
    /// drives will appear in the status display.
    /// </remarks>
    void RegisterDrive(int slotNumber, int driveNumber);

    /// <summary>
    /// Updates the status of a specific drive using a builder action.
    /// </summary>
    /// <param name="slotNumber">Slot number (1-7).</param>
    /// <param name="driveNumber">Drive number (1-2).</param>
    /// <param name="mutator">Action to modify the drive's status builder.</param>
    void MutateDrive(int slotNumber, int driveNumber, Action<DiskDriveStatusBuilder> mutator);

    /// <summary>
    /// Performs a batch update across multiple drives.
    /// </summary>
    /// <param name="mutator">Action to modify the full status builder.</param>
    void Mutate(Action<DiskStatusSnapshotBuilder> mutator);
}

/// <summary>
/// Provides observable disk status tracking with mutation interface.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Architecture:</strong> Implements both <see cref="IDiskStatusProvider"/> (read-only)
/// and <see cref="IDiskStatusMutator"/> (write access) to provide controlled access to
/// disk drive state. Disk controller cards and drives update state via the mutator interface,
/// while UI and diagnostics observe via the provider interface.
/// </para>
/// <para>
/// <strong>Observable Pattern:</strong> Uses <see cref="System.Reactive.Subjects.BehaviorSubject{T}"/>
/// for reactive updates. New subscribers immediately receive the current state.
/// </para>
/// <para>
/// <strong>Dynamic Registration:</strong> Starts with an empty drive list. Drives are registered
/// dynamically as they are created by the factory. Only registered drives appear in the UI.
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> Mutations are not thread-safe - caller must ensure
/// serialized access. Reads are safe from any thread due to immutable snapshots.
/// </para>
/// </remarks>
public sealed class DiskStatusProvider : IDiskStatusMutator
{
    private const int MAX_SLOTS = 7;
    private const int DRIVES_PER_SLOT = 2;

    // Current disk status snapshot (immutable)
    private DiskStatusSnapshot _current;

    // Reactive subject for observable pattern
    private readonly System.Reactive.Subjects.BehaviorSubject<DiskStatusSnapshot> _subject;

    /// <inheritdoc />
    public event EventHandler<DiskStatusSnapshot>? Changed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiskStatusProvider"/> class.
    /// </summary>
    /// <remarks>
    /// Starts with an empty drive list. Drives are registered dynamically via
    /// <see cref="RegisterDrive"/> when created by the factory.
    /// </remarks>
    public DiskStatusProvider()
    {
        // Start with an empty array - drives will be registered dynamically
        _current = new DiskStatusSnapshot([]);
        _subject = new System.Reactive.Subjects.BehaviorSubject<DiskStatusSnapshot>(_current);
    }

    /// <summary>
    /// Validates slot and drive numbers.
    /// </summary>
    private static bool IsValidSlotAndDrive(int slotNumber, int driveNumber)
    {
        return slotNumber >= 1 && slotNumber <= MAX_SLOTS &&
               driveNumber >= 1 && driveNumber <= DRIVES_PER_SLOT;
    }

    #region IDiskStatusProvider - Read-only access

    /// <inheritdoc />
    public DiskStatusSnapshot Current => _current;

    /// <inheritdoc />
    public IObservable<DiskStatusSnapshot> Stream => _subject;

    /// <inheritdoc />
    public DiskDriveStatusSnapshot? GetDriveStatus(int slotNumber, int driveNumber)
    {
        if (!IsValidSlotAndDrive(slotNumber, driveNumber))
        {
            return null;
        }

        // Find drive in current snapshot by matching slot and drive numbers
        return _current.Drives.FirstOrDefault(d =>
            d.SlotNumber == slotNumber && d.DriveNumber == driveNumber);
    }

    #endregion

    #region IDiskStatusMutator - Mutation interface

    /// <inheritdoc />
    public void RegisterDrive(int slotNumber, int driveNumber)
    {
        if (!IsValidSlotAndDrive(slotNumber, driveNumber))
        {
            return;
        }

        // Check if drive already registered
        if (_current.Drives.Any(d => d.SlotNumber == slotNumber && d.DriveNumber == driveNumber))
        {
            return; // Already registered
        }

        // Create new drive with default state
        var newDrive = new DiskDriveStatusSnapshot(
            SlotNumber: slotNumber,
            DriveNumber: driveNumber,
            DiskImagePath: string.Empty,
            DiskImageFilename: string.Empty,
            IsReadOnly: false,
            Track: 17.0,  // Typical starting position
            Sector: -1,   // Unknown sector
            MotorOn: false,
            MotorOffScheduled: false,
            PhaseState: 0,  // No phases active
            HasValidTrackData: false,
            IsDirty: false,
            HasDestinationPath: false
        );

        // Add to array and sort by slot, then drive
        var newDrives = _current.Drives
            .Append(newDrive)
            .OrderBy(d => d.SlotNumber)
            .ThenBy(d => d.DriveNumber)
            .ToArray();

        _current = new DiskStatusSnapshot(newDrives);

        // Notify observers
        _subject.OnNext(_current);
        Changed?.Invoke(this, _current);
    }

    /// <inheritdoc />
    public void MutateDrive(int slotNumber, int driveNumber, Action<DiskDriveStatusBuilder> mutator)
    {
        if (!IsValidSlotAndDrive(slotNumber, driveNumber))
        {
            return; // Silently ignore invalid slot/drive
        }

        // Find the drive in the current snapshot
        var driveIndex = Array.FindIndex(_current.Drives, d =>
            d.SlotNumber == slotNumber && d.DriveNumber == driveNumber);

        if (driveIndex < 0)
        {
            return; // Drive not registered - silently ignore
        }

        // Create builder from current snapshot
        var builder = new DiskStatusSnapshotBuilder(_current);

        // Get drive builder and apply mutations
        var driveBuilder = builder.GetDriveBuilder(driveIndex);
        mutator(driveBuilder);

        // Build and update
        _current = builder.Build();

        // Notify observers
        _subject.OnNext(_current);
        Changed?.Invoke(this, _current);
    }

    /// <inheritdoc />
    public void Mutate(Action<DiskStatusSnapshotBuilder> mutator)
    {
        // Create builder from current snapshot
        var builder = new DiskStatusSnapshotBuilder(_current);

        // Apply mutations
        mutator(builder);

        // Build and update
        _current = builder.Build();

        // Notify observers
        _subject.OnNext(_current);
        Changed?.Invoke(this, _current);
    }

    #endregion
}

/// <summary>
/// Mutable builder for creating <see cref="DiskDriveStatusSnapshot"/> instances.
/// </summary>
/// <remarks>
/// Provides a mutable workspace for constructing new drive status snapshots.
/// All fields can be freely modified before building the immutable snapshot.
/// </remarks>
/// <remarks>
/// Initializes a new builder from an existing snapshot.
/// </remarks>
public sealed class DiskDriveStatusBuilder(DiskDriveStatusSnapshot snapshot)
{
    /// <summary>Expansion slot number (1-7).</summary>
    public int SlotNumber = snapshot.SlotNumber;

    /// <summary>Drive number on controller (1-2).</summary>
    public int DriveNumber = snapshot.DriveNumber;

    /// <summary>Full path to disk image file.</summary>
    public string DiskImagePath = snapshot.DiskImagePath;

    /// <summary>Filename only of disk image.</summary>
    public string DiskImageFilename = snapshot.DiskImageFilename;

    /// <summary>True if disk is write-protected.</summary>
    public bool IsReadOnly = snapshot.IsReadOnly;

    /// <summary>Current track position (0.00-34.75).</summary>
    public double Track = snapshot.Track;

    /// <summary>Current sector number (0-15, or -1 if unknown).</summary>
    public int Sector = snapshot.Sector;

    /// <summary>True if motor is running.</summary>
    public bool MotorOn = snapshot.MotorOn;

    /// <summary>True if motor-off has been scheduled.</summary>
    public bool MotorOffScheduled = snapshot.MotorOffScheduled;

    /// <summary>Phase state (low nibble: bits 3-0 = phases 3-0).</summary>
    public byte PhaseState = snapshot.PhaseState;

    /// <summary>True if current track has valid data.</summary>
    public bool HasValidTrackData = snapshot.HasValidTrackData;

    /// <summary>True if disk has been modified since load or last save.</summary>
    public bool IsDirty = snapshot.IsDirty;

    /// <summary>True if disk has an attached destination path for Save operations.</summary>
    public bool HasDestinationPath = snapshot.HasDestinationPath;

    /// <summary>
    /// Builds an immutable <see cref="DiskDriveStatusSnapshot"/>.
    /// </summary>
    public DiskDriveStatusSnapshot Build() => new(
        SlotNumber,
        DriveNumber,
        DiskImagePath,
        DiskImageFilename,
        IsReadOnly,
        Track,
        Sector,
        MotorOn,
        MotorOffScheduled,
        PhaseState,
        HasValidTrackData,
        IsDirty,
        HasDestinationPath
    );
}

/// <summary>
/// Mutable builder for creating <see cref="DiskStatusSnapshot"/> instances.
/// </summary>
/// <remarks>
/// Provides batch update capability across multiple drives.
/// </remarks>
public sealed class DiskStatusSnapshotBuilder
{
    private readonly DiskDriveStatusBuilder[] _driveBuilders;

    /// <summary>
    /// Initializes a new builder from an existing snapshot.
    /// </summary>
    public DiskStatusSnapshotBuilder(DiskStatusSnapshot snapshot)
    {
        _driveBuilders = new DiskDriveStatusBuilder[snapshot.Drives.Length];
        for (int i = 0; i < snapshot.Drives.Length; i++)
        {
            _driveBuilders[i] = new DiskDriveStatusBuilder(snapshot.Drives[i]);
        }
    }

    /// <summary>
    /// Gets the builder for a specific drive by index.
    /// </summary>
    /// <param name="index">Array index (0-13).</param>
    public DiskDriveStatusBuilder GetDriveBuilder(int index)
    {
        return _driveBuilders[index];
    }

    /// <summary>
    /// Gets the builder for a specific drive by slot and drive number.
    /// </summary>
    /// <param name="slotNumber">Slot number (1-7).</param>
    /// <param name="driveNumber">Drive number (1-2).</param>
    public DiskDriveStatusBuilder GetDriveBuilder(int slotNumber, int driveNumber)
    {
        int index = ((slotNumber - 1) * 2) + (driveNumber - 1);
        return _driveBuilders[index];
    }

    /// <summary>
    /// Builds an immutable <see cref="DiskStatusSnapshot"/>.
    /// </summary>
    public DiskStatusSnapshot Build()
    {
        var drives = new DiskDriveStatusSnapshot[_driveBuilders.Length];
        for (int i = 0; i < _driveBuilders.Length; i++)
        {
            drives[i] = _driveBuilders[i].Build();
        }
        return new DiskStatusSnapshot(drives);
    }
}
