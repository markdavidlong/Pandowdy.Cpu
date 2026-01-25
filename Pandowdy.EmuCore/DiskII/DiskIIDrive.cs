using System.Diagnostics;
using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.DiskII;

/// <summary>
/// Implements a Disk II floppy disk drive with mechanical head positioning, motor control,
/// and telemetry integration.
/// </summary>
/// <remarks>
/// <para>
/// This class simulates the physical characteristics of a Disk II drive:
/// <list type="bullet">
/// <item>Stepper motor with quarter-track positioning (0-139 quarter-tracks = 0-34.75 whole tracks)</item>
/// <item>Motor on/off control with state change telemetry</item>
/// <item>Read/write operations through an <see cref="IDiskImageProvider"/></item>
/// <item>Disk insert/eject operations with telemetry notifications</item>
/// </list>
/// </para>
/// <para>
/// The drive delegates actual bit reading/writing to the <see cref="IDiskImageProvider"/>,
/// while managing the mechanical aspects (motor state, head position). This separation
/// matches real hardware where the drive mechanism is separate from the disk media.
/// </para>
/// <para>
/// <strong>Telemetry Integration:</strong> This drive publishes telemetry messages for
/// state changes (motor on/off, track changes, disk insert/eject). The UI layer can
/// subscribe to these messages to display drive status without polling.
/// </para>
/// </remarks>
public class DiskIIDrive : IDiskIIDrive
{
    private IDiskImageProvider? _imageProvider;
    private readonly IDiskImageFactory? _diskImageFactory;
    private readonly ITelemetryAggregator _telemetry;
    private readonly TelemetryId _telemetryId;
    private readonly int _slotNumber;
    private readonly int _driveNumber;
    private int _quarterSteps;
    private bool _motorOn;
    private bool _hitMinLogged;
    private bool _hitMaxLogged;

    /// <inheritdoc />
    public string Name { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DiskIIDrive"/> class.
    /// </summary>
    /// <param name="name">Name for the drive (e.g., "Slot6-D1").</param>
    /// <param name="telemetry">The telemetry aggregator for publishing state changes.</param>
    /// <param name="slotNumber">The slot number (1-7) where the controller card is installed.</param>
    /// <param name="driveNumber">The drive number (1 or 2) for this drive.</param>
    /// <param name="imageProvider">Optional disk image provider. If null, the drive behaves as if no disk is inserted.</param>
    /// <param name="diskImageFactory">Optional factory for creating disk image providers when inserting disks.</param>
    public DiskIIDrive(
        string name,
        ITelemetryAggregator telemetry,
        int slotNumber,
        int driveNumber,
        IDiskImageProvider? imageProvider = null,
        IDiskImageFactory? diskImageFactory = null)
    {
        Name = name ?? "Unnamed";
        _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        _slotNumber = slotNumber;
        _driveNumber = driveNumber;
        _imageProvider = imageProvider;
        _diskImageFactory = diskImageFactory;

        // Register with telemetry system
        _telemetryId = telemetry.CreateId(DiskIIConstants.TelemetryCategory);

        // Subscribe to resend requests
        _telemetry.ResendRequests.Subscribe(request =>
        {
            if (request.MatchesProvider(_telemetryId))
            {
                PublishFullState();
            }
        });

        Reset();

        // Notify image provider of initial track position
        _imageProvider?.SetQuarterTrack(_quarterSteps);
    }

    /// <summary>
    /// Publishes the complete current state of the drive.
    /// </summary>
    /// <remarks>
    /// Called in response to resend requests to ensure the UI has current state.
    /// </remarks>
    private void PublishFullState()
    {
        _telemetry.Publish(new TelemetryMessage(
            _telemetryId,
            "state",
            new DiskIIMessage($"Slot {_slotNumber} Drive {_driveNumber}: Motor={(_motorOn ? "ON" : "OFF")}, Track={Track:F2}, Disk={(_imageProvider != null ? Path.GetFileName(_imageProvider.FilePath) : "None")}")));
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
        _telemetry.Publish(new TelemetryMessage(
            _telemetryId,
            "disk-inserted",
            new DiskIIMessage($"Disk inserted: {Path.GetFileName(diskImagePath)}")));
    }

    /// <inheritdoc />
    public void EjectDisk()
    {
        if (_imageProvider != null)
        {
            string fileName = Path.GetFileName(_imageProvider.FilePath);

            // Flush any pending writes and dispose
            _imageProvider.Flush();
            _imageProvider.Dispose();
            _imageProvider = null;

            Debug.WriteLine($"Drive '{Name}': Ejected disk");
            _telemetry.Publish(new TelemetryMessage(
                _telemetryId,
                "disk-ejected",
                new DiskIIMessage($"Disk ejected: {fileName}")));
        }
    }

    /// <inheritdoc />
    public bool HasDisk => _imageProvider != null;

    /// <inheritdoc />
    public void Reset()
    {
        // Start at track 17 (typical boot track area)
        _quarterSteps = 4 * 17;
        MotorOn = false;
        _hitMinLogged = false;
        _hitMaxLogged = false;
    }

    /// <inheritdoc />
    public bool MotorOn
    {
        get => _motorOn;
        set
        {
            if (_motorOn != value)
            {
                _motorOn = value;
                Debug.WriteLine($"Drive '{Name}' motor turned {(value ? "ON" : "OFF")}");
                _telemetry.Publish(new TelemetryMessage(
                    _telemetryId,
                    "motor",
                    new DiskIIMessage($"Motor {(value ? "ON" : "OFF")}")));
            }
        }
    }

    /// <inheritdoc />
    public double Track => _quarterSteps / 4.0;

    /// <inheritdoc />
    public int QuarterTrack => _quarterSteps;

    /// <inheritdoc />
    public void StepToHigherTrack()
    {
        int previousQuarterTrack = _quarterSteps;
        _quarterSteps++;

        if (_quarterSteps > DiskIIConstants.MaxQuarterTracks)
        {
            _quarterSteps = DiskIIConstants.MaxQuarterTracks;
            if (!_hitMaxLogged)
            {
                Debug.WriteLine($"Drive '{Name}' head hit maximum position at quarter-track {_quarterSteps}");
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

        // Publish telemetry if track changed
        if (_quarterSteps != previousQuarterTrack)
        {
            _telemetry.Publish(new TelemetryMessage(
                _telemetryId,
                "track",
                new DiskIIMessage($"Track {Track:F2}")));
        }
    }

    /// <inheritdoc />
    public void StepToLowerTrack()
    {
        int previousQuarterTrack = _quarterSteps;
        _quarterSteps--;

        if (_quarterSteps < 0)
        {
            _quarterSteps = 0;
            if (!_hitMinLogged)
            {
                Debug.WriteLine($"Drive '{Name}' head hit minimum position at quarter-track 0");
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

        // Publish telemetry if track changed
        if (_quarterSteps != previousQuarterTrack)
        {
            _telemetry.Publish(new TelemetryMessage(
                _telemetryId,
                "track",
                new DiskIIMessage($"Track {Track:F2}")));
        }
    }

    /// <inheritdoc />
    public bool? GetBit(ulong currentCycle)
    {
        if (!_motorOn)
        {
            return null;
        }

        if (_imageProvider == null)
        {
            return null;
        }

        return _imageProvider.GetBit(currentCycle);
    }

    /// <inheritdoc />
    public bool SetBit(bool value)
    {
        if (!_motorOn || _imageProvider == null)
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
}
