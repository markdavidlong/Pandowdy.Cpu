// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Diagnostics;
using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.DiskII;
using Pandowdy.EmuCore.DiskII.Providers;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Services;

namespace Pandowdy.EmuCore.Cards;

/// <summary>
/// Represents the motor state of the Disk II controller.
/// </summary>
/// <remarks>
/// The Disk II controller has a single motor line that can power only one drive at a time.
/// Motor state is controller-level, not per-drive.
/// </remarks>
public enum DiskIIMotorState
{
    /// <summary>Motor is off, no drive is powered.</summary>
    Off,
    /// <summary>Motor is running, powering the currently selected drive.</summary>
    On,
    /// <summary>Motor is running but scheduled to turn off after delay.</summary>
    ScheduledOff
}

/// <summary>
/// Abstract base class for Disk II controller cards.
/// </summary>
/// <remarks>
/// <para>
/// The Disk II controller manages two drives with a single motor line.
/// Motor state is controller-level: when on, the motor powers only the currently selected drive.
/// Drives are passive mechanical devices that respond to head positioning and I/O operations.
/// </para>
/// <para>
/// The controller uses Q6 and Q7 control lines to select operating modes:
/// </para>
/// <para>
/// Q6/Q7 Truth Table:
/// <list type="table">
/// <listheader>
///     <term>Q7</term><term>Q6</term><term>Read Access</term><term>Write Access</term><term>Mode</term>
/// </listheader>
/// <item><term>0</term><term>0</term><term>Read shift register</term><term>N/A</term><term>READ MODE</term></item>
/// <item><term>0</term><term>1</term><term>Read write-protect</term><term>N/A</term><term>SENSE WRITE PROTECT</term></item>
/// <item><term>1</term><term>0</term><term>N/A (timing)</term><term>Prepare shift register</term><term>WRITE LOAD</term></item>
/// <item><term>1</term><term>1</term><term>N/A</term><term>Write shift register</term><term>WRITE MODE</term></item>
/// </list>
/// </para>
/// <para>
/// The shift register continuously accumulates bits from the drive when the motor is running.
/// Software must poll the register and handle timing (approximately 4 CPU cycles per bit).
/// </para>
/// <para>
/// <strong>Status Integration:</strong> This controller uses <see cref="IDiskStatusMutator"/> to
/// publish phase changes, motor state, and sector detection to the UI.
/// Drives publish their own track/disk state through the <see cref="DiskIIStatusDecorator"/>.
/// </para>
/// </remarks>
public abstract class DiskIIControllerCard : ICard
{
    protected readonly CpuClockingCounters _clocking;
    protected IDiskIIDrive[] _drives = [];
    protected readonly IDiskIIFactory _diskIIFactory;
    protected readonly IDiskStatusMutator _statusMutator;
    protected readonly ICardResponseEmitter _responseEmitter;
    protected SlotNumber _slotNumber = SlotNumber.Unslotted;

    /// <inheritdoc />
    public SlotNumber Slot => _slotNumber;

    /// <summary>
    /// Gets the drives managed by this controller (Drive 1 and Drive 2).
    /// </summary>
    /// <remarks>
    /// Exposed for disk insertion/ejection. The controller card itself doesn't
    /// care about the media - that's managed at the drive level.
    /// </remarks>
    public IDiskIIDrive[] Drives => _drives;

    // Controller state
    protected bool _q6;                    // Q6 control line state
    protected bool _q7;                    // Q7 control line state
    protected byte _shiftRegister;         // 8-bit shift register for bit accumulation
    protected int _selectedDriveIndex = 0; // Currently selected drive (0 or 1)
    protected DiskIIMotorState _motorState = DiskIIMotorState.Off; // Controller motor state (NEW: Phase 1)

    protected IDiskIIDrive? SelectedDrive =>
        _selectedDriveIndex < _drives.Length ? _drives[_selectedDriveIndex] : null;

    /// <summary>
    /// Gets whether the controller motor is currently running (On or ScheduledOff).
    /// </summary>
    /// <remarks>
    /// The controller has a single motor line that powers the currently selected drive.
    /// Motor is considered "running" when it's either actively on or scheduled to turn off
    /// (still spinning during the ~1 second delay). Exposed as internal for test access.
    /// </remarks>
    internal bool IsMotorRunning => _motorState != DiskIIMotorState.Off;

    // Phase state for stepper motor control (matching TypeScript algorithm)
    protected byte _currentPhase = 0;        // 4-bit bitfield for phases 0-3 (can have multiple active)

    // Motor-off delay (matches real Disk II hardware behavior)
    private ulong _motorOffScheduledCycle = 0;  // 0 = no pending motor-off, otherwise cycle when motor should turn off

    // Motor-off delay in CPU cycles (~1 second at 1.023 MHz)
    private const ulong MotorOffDelayCycles = 1_000_000;

    // Lookup tables for stepper motor movement (from TypeScript reference)
    private static readonly int[] MagnetToPosition =
    [
        // Bits: 0000 0001 0010 0011 0100 0101 0110 0111 1000 1001 1010 1011 1100 1101 1110 1111
        -1,   0,   2,   1,   4,  -1,   3,  -1,   6,   7,  -1,  -1,   5,  -1,  -1,  -1
    ];

    private static readonly int[][] PositionToDirection =
    [
        //   N-0 NE-1 E-2 SE-3 S-4 SW-5 W-6 NW-7
        [  0,  1,  2,  3,  0, -3, -2, -1 ], // 0 N
        [ -1,  0,  1,  2,  3,  0, -3, -2 ], // 1 NE
        [ -2, -1,  0,  1,  2,  3,  0, -3 ], // 2 E
        [ -3, -2, -1,  0,  1,  2,  3,  0 ], // 3 SE
        [  0, -3, -2, -1,  0,  1,  2,  3 ], // 4 S
        [  3,  0, -3, -2, -1,  0,  1,  2 ], // 5 SW
        [  2,  3,  0, -3, -2, -1,  0,  1 ], // 6 W
        [  1,  2,  3,  0, -3, -2, -1,  0 ], // 7 NW
    ];

    /// <summary>
    /// Tracks last 3 bytes for prologue pattern detection (D5 AA 96 or D5 AA AD).
    /// </summary>
    /// <remarks>
    /// <strong>Known Issue:</strong> This buffer is shared across both drives. When switching
    /// drives, stale bytes from the previous drive's data stream may cause false prologue
    /// detection. Consider clearing this buffer in <see cref="HandleDriveSelection"/> if
    /// cross-drive prologue detection becomes problematic.
    /// </remarks>
    private byte[] _lastThreeBytes = new byte[3];
    private byte _diagnosticShiftReg = 0;         // Independent shift register for logging
    private int _diagnosticByteCount = 0;         // Count bytes since last data prologue
    private int _latchedReadCount = 0;            // Count latched reads by controller

    // Address field decoding state
    private enum AddressFieldState
    {
        Idle,
        ReadingVolume,
        ReadingTrack,
        ReadingSector,
        ReadingChecksum,
        ReadingEpilogue
    }

    private AddressFieldState _addressFieldState = AddressFieldState.Idle;
    private readonly byte[] _addressFieldBytes = new byte[8]; // Volume(2) + Track(2) + Sector(2) + Checksum(2)
    private int _addressFieldIndex = 0;

    // Data field decoding state
    private enum DataFieldState
    {
        Idle,
        ReadingData,      // Reading 343 encoded bytes (256 6-bit + 86 2-bit + 1 checksum)
        ReadingEpilogue   // Reading 3 epilogue bytes (DE AA EB)
    }

    private DataFieldState _dataFieldState = DataFieldState.Idle;
    private readonly byte[] _dataFieldBytes = new byte[343]; // 343 bytes: 256 (6-bit) + 86 (2-bit) + 1 (checksum)
    private int _dataFieldIndex = 0;

    // Bit timing for shift register
    private double _lastBitShiftCycle = 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiskIIControllerCard"/> class.
    /// </summary>
    /// <param name="cpuClocking">The CPU clocking counters for timing operations.</param>
    /// <param name="diskIIFactory">Factory for creating drive instances.</param>
    /// <param name="statusMutator">Status mutator for publishing controller state changes.</param>
    /// <param name="responseEmitter">Card response emitter for publishing card identification and device enumeration responses.</param>
    protected DiskIIControllerCard(
        CpuClockingCounters cpuClocking,
        IDiskIIFactory diskIIFactory,
        IDiskStatusMutator statusMutator,
        ICardResponseEmitter responseEmitter)
    {
        ArgumentNullException.ThrowIfNull(cpuClocking);
        ArgumentNullException.ThrowIfNull(diskIIFactory);
        ArgumentNullException.ThrowIfNull(statusMutator);
        ArgumentNullException.ThrowIfNull(responseEmitter);

        _clocking = cpuClocking;
        _diskIIFactory = diskIIFactory;
        _statusMutator = statusMutator;
        _responseEmitter = responseEmitter;

        // Subscribe to VBlank for periodic motor-off checking
        // This ensures motor-off happens even when no disk I/O is occurring
        _clocking.VBlankOccurred += OnVBlankTick;

        // Drives will be initialized when the card is installed via OnInstalled()
    }

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract string Description { get; }

    /// <inheritdoc />
    public abstract int Id { get; }

    // I/O Address Map ($C0n0-$C0nF where n = slot number):
    // 0x0 = Phase 0 Off
    // 0x1 = Phase 0 On
    // 0x2 = Phase 1 Off
    // 0x3 = Phase 1 On
    // 0x4 = Phase 2 Off
    // 0x5 = Phase 2 On
    // 0x6 = Phase 3 Off
    // 0x7 = Phase 3 On
    // 0x8 = Motor Off
    // 0x9 = Motor On
    // 0xA = Select Drive 1
    // 0xB = Select Drive 2
    // 0xC = Q6L (Q6 = 0)
    // 0xD = Q6H (Q6 = 1)
    // 0xE = Q7L (Q7 = 0)
    // 0xF = Q7H (Q7 = 1)

    private static readonly string[] IoNames =
        ["Ph0-", "Ph0+", "Ph1-", "Ph1+", "Ph2-", "Ph2+", "Ph3-", "Ph3+",
         "MotorOff", "MotorOn", "SelD1", "SelD2", "Q6L", "Q6H", "Q7L", "Q7H"];

    /// <inheritdoc />
    public byte? ReadIO(byte ioAddr)
    {
        // Handle phase control (0x0-0x7)
        if (ioAddr <= 0x7)
        {
            HandlePhaseControl(ioAddr);
            return null; // Phase operations don't return data - Floating bus value
        }

        // Handle motor control (0x8-0x9)
        if (ioAddr == 0x8 || ioAddr == 0x9)
        {
            // CRITICAL: Motor off ($C088) can still read data if motor is running
            // This is required for some copy-protected disks like Mr. Do.woz
            // PHASE 3: Check controller motor state instead of drive state
            if (ioAddr == 0x8 && SelectedDrive != null && IsMotorRunning && !_q7)
            {
                byte? motorOffReadResult = ReadShiftRegister();
                // Don't reset! ProcessBits() maintains position continuity
                HandleMotorControl(ioAddr);  // Still process motor control
                return motorOffReadResult;
            }

            HandleMotorControl(ioAddr);
            return null; // Returns Floating Bus Values
        }

        // Handle drive selection (0xA-0xB)
        if (ioAddr == 0xA || ioAddr == 0xB)
        {
            HandleDriveSelection(ioAddr);
            return null; // Returns Floating Bus Values
        }

        // Handle Q6/Q7 control and data operations (0xC-0xF)
        if (ioAddr >= 0xC && ioAddr <= 0xF)
        {
            byte? result = HandleQ6Q7Read(ioAddr);
            return result;
        }

        return null;
    }

    /// <inheritdoc />
    public void WriteIO(byte ioAddr, byte value)
    {
        // Handle phase control (0x0-0x7)
        if (ioAddr <= 0x7)
        {
            HandlePhaseControl(ioAddr);
            return;
        }

        // Handle motor control (0x8-0x9)
        if (ioAddr == 0x8 || ioAddr == 0x9)
        {
            HandleMotorControl(ioAddr);
            return;
        }

        // Handle drive selection (0xA-0xB)
        if (ioAddr == 0xA || ioAddr == 0xB)
        {
            HandleDriveSelection(ioAddr);
            return;
        }

        // Handle Q6/Q7 control and data operations (0xC-0xF)
        if (ioAddr >= 0xC && ioAddr <= 0xF)
        {
            HandleQ6Q7Write(ioAddr, value);
        }
    }

    /// <summary>
    /// Handles stepper motor phase control using bitfield (allows multiple phases active).
    /// </summary>
    /// <remarks>
    /// The Disk II allows multiple phases to be energized simultaneously for precise positioning.
    /// This matches the TypeScript reference implementation.
    /// </remarks>
    protected virtual void HandlePhaseControl(byte ioAddr)
    {
        ProcessBits(_clocking.TotalCycles);

        int phase = (ioAddr >> 1) & 0x03; // Phase 0-3
        bool turnOn = (ioAddr & 0x01) == 1;

        // Update phase bitfield (allowing multiple phases to be active)
        if (turnOn)
        {
            _currentPhase |= (byte)(1 << phase);  // Set bit for this phase
        }
        else
        {
            _currentPhase &= (byte)~(1 << phase); // Clear bit for this phase
        }

        // Update status with new phase state (do this AFTER setting _currentPhase)
        UpdatePhaseState();

        // Get position from magnet state
        int position = MagnetToPosition[_currentPhase];

        // Only move head if motor is running and we have a valid position
        // PHASE 3: Check controller motor state instead of drive state
        if (SelectedDrive != null && IsMotorRunning && position >= 0)
        {
            DetermineHeadMovement(position);
        }
    }

    /// <summary>
    /// Determines head movement using lookup table algorithm (from TypeScript reference).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This algorithm uses two lookup tables:
    /// 1. MagnetToPosition: Maps 4-bit phase combinations to 8 positions (N, NE, E, SE, S, SW, W, NW)
    /// 2. PositionToDirection: Maps (current_position, target_position) to signed offset
    /// </para>
    /// <para>
    /// The stepper motor moves in quarter-tracks. Each position change represents 1 quarter-track.
    /// Multiple phases can be active simultaneously for precise between-detent positioning.
    /// </para>
    /// </remarks>
    protected virtual void DetermineHeadMovement(int targetPosition)
    {
        if (SelectedDrive == null)
        {
            return;
        }

        // Get current position from quarterTrack (mod 8 for compass position)
        int lastPosition = SelectedDrive.QuarterTrack & 7;

        // Look up the direction offset
        int direction = PositionToDirection[lastPosition][targetPosition];

        if (direction != 0)
        {
            // Move the head by the calculated offset
            MoveHead(direction);

#if ControllerDebug
            Debug.WriteLine($"[{_clocking.TotalCycles}] 🔄 Head moved: Position {lastPosition}→{targetPosition}, Direction={direction:+0;-0}, QuarterTrack={SelectedDrive.QuarterTrack}, Track={SelectedDrive.Track:F2}");
#endif
        }
    }

    /// <summary>
    /// Moves the drive head by the specified quarter-track offset.
    /// </summary>
    /// <param name="offset">Signed offset in quarter-tracks (positive = inward/higher, negative = outward/lower).</param>
    private void MoveHead(int offset)
    {
        if (SelectedDrive == null)
        {
            return;
        }

        // Apply the offset
        for (int i = 0; i < Math.Abs(offset); i++)
        {
            if (offset > 0)
            {
                SelectedDrive.StepToHigherTrack();
            }
            else
            {
                SelectedDrive.StepToLowerTrack();
            }
        }
    }

    /// <summary>
    /// Handles motor on/off control with 1-second delay for motor-off (cycle-based).
    /// </summary>
    /// <remarks>
    /// Real Disk II hardware delays motor-off by approximately 1 second to keep the motor
    /// spinning during brief pauses in I/O. This prevents unnecessary motor start/stop cycles.
    /// Motor-on cancels any pending motor-off delay.
    /// </remarks>
    protected virtual void HandleMotorControl(byte ioAddr)
    {
        ProcessBits(_clocking.TotalCycles);

        bool motorOnRequested = (ioAddr & 0x01) == 1;

        if (SelectedDrive != null)
        {
            if (motorOnRequested)
            {
                // Motor ON: Cancel any pending motor-off and turn motor on immediately
                if (_motorOffScheduledCycle > 0)
                { 
#if ControllerDebug
                    Debug.WriteLine($"[{_clocking.TotalCycles}] ℹ️ MOTOR-OFF CANCELLED (was scheduled for cycle {_motorOffScheduledCycle})");
#endif
                    _motorOffScheduledCycle = 0;

                    // Update status: motor-off no longer scheduled
                    UpdateMotorOffScheduledStatus(false);
                }

                if (_motorState == DiskIIMotorState.Off)  // Only initialize if motor was off
                {
#if ControllerDebug
                Debug.WriteLine($"[{_clocking.TotalCycles}] 🔵 MOTOR ON - Drive {_selectedDriveIndex + 1}, Track {SelectedDrive.Track:F2}");
#endif
                    _lastBitShiftCycle = _clocking.TotalCycles;  // Only reset timing (matches TypeScript cycleRemainder = 0)
                    // DON'T clear _shiftRegister - it must maintain state across motor cycles!
                    _diagnosticShiftReg = 0; // Clear diagnostic shift register for fresh tracking
                    _diagnosticByteCount = 0; // Reset diagnostic counters
                    _latchedReadCount = 0;
                    _addressFieldState = AddressFieldState.Idle; // Reset address field state
                    _dataFieldState = DataFieldState.Idle; // Reset data field state

                    // Set motor state and notify (encapsulated)
                    SetMotorState(DiskIIMotorState.On);
                }
            }
            else
            {
                // Motor OFF: Schedule motor-off for 1 second from now (matches TypeScript setTimeout)
                if (_motorOffScheduledCycle == 0)
                {
                    _motorOffScheduledCycle = _clocking.TotalCycles + MotorOffDelayCycles;
#if ControllerDebug
                Debug.WriteLine($"[{_clocking.TotalCycles}] ⏱️ MOTOR-OFF SCHEDULED for cycle {_motorOffScheduledCycle} (~1 sec delay)");
#endif
                    // Set motor state to ScheduledOff (encapsulated)
                    SetMotorState(DiskIIMotorState.ScheduledOff);
                }
            }
        }
    }

    /// <summary>
    /// Checks and processes pending motor-off if delay has elapsed.
    /// Called periodically via VBlank event (~60 Hz) to ensure motor-off happens
    /// even when no disk I/O is occurring.
    /// </summary>
    private void CheckPendingMotorOff()
    {
        if (_motorOffScheduledCycle > 0 && _clocking.TotalCycles >= _motorOffScheduledCycle && SelectedDrive != null)
        {
#if ControllerDebug
            Debug.WriteLine($"[{_clocking.TotalCycles}] 🔴 MOTOR OFF (delayed) - Drive {_selectedDriveIndex + 1}, Track {SelectedDrive.Track:F2}");
#endif

            // Clear the schedule before turning motor off
            _motorOffScheduledCycle = 0;

            // Set motor state to Off (encapsulated - handles notification)
            SetMotorState(DiskIIMotorState.Off);

            // DON'T clear _shiftRegister - it must maintain state across motor cycles!
            _diagnosticShiftReg = 0; // Clear diagnostic shift register
        }
    }

    /// <summary>
    /// VBlank event handler for periodic operations.
    /// Fires ~60 times per second (every 17,030 cycles) regardless of disk I/O activity.
    /// </summary>
    /// <remarks>
    /// This ensures motor-off countdown happens even when software isn't accessing the disk.
    /// Granularity: ±16.6ms (1.7% of 1-second motor-off delay) - acceptable for mechanical timing.
    /// </remarks>
    private void OnVBlankTick()
    {
        CheckPendingMotorOff();
    }

    /// <summary>
    /// Sets the motor state and automatically updates all related status notifications.
    /// </summary>
    /// <param name="newState">The new motor state to transition to.</param>
    /// <remarks>
    /// <para>
    /// This method encapsulates the coupling between motor state changes and GUI notifications.
    /// It ensures that whenever the controller motor state changes, the appropriate status
    /// updates are sent to the UI for the currently selected drive.
    /// </para>
    /// <para>
    /// State transitions handled:
    /// <list type="bullet">
    /// <item>Off → On: Motor starts, notifies drive, updates MotorOn=true, MotorOffScheduled=false</item>
    /// <item>Off/On → ScheduledOff: Motor still running, updates MotorOffScheduled=true</item>
    /// <item>ScheduledOff → On: Cancels scheduled off, updates MotorOffScheduled=false</item>
    /// <item>On/ScheduledOff → Off: Motor stops, notifies drive, updates MotorOn=false, MotorOffScheduled=false</item>
    /// </list>
    /// </para>
    /// </remarks>
    private void SetMotorState(DiskIIMotorState newState)
    {
        if (_motorState == newState)
        {
            return; // No change needed
        }

        DiskIIMotorState oldState = _motorState;
        _motorState = newState;

        // Determine status flags based on new state
        bool motorOn = (newState == DiskIIMotorState.On || newState == DiskIIMotorState.ScheduledOff);
        bool motorOffScheduled = (newState == DiskIIMotorState.ScheduledOff);

        // Update status flags for currently selected drive
        UpdateMotorOffScheduledStatus(motorOffScheduled);
        UpdateMotorOnStatus(motorOn);

        // Notify drive if motor running state actually changed (Off ↔ On/ScheduledOff)
        if (SelectedDrive != null)
        {
            bool wasRunning = (oldState != DiskIIMotorState.Off);
            bool isRunning = (newState != DiskIIMotorState.Off);

            if (wasRunning != isRunning)
            {
                SelectedDrive.NotifyMotorStateChanged(isRunning, _clocking.TotalCycles);
            }
        }
    }

    /// <summary>
    /// Updates the MotorOffScheduled status flag for the currently selected drive.
    /// </summary>
    /// <param name="scheduled">True if motor-off is scheduled, false if cancelled or completed.</param>
    private void UpdateMotorOffScheduledStatus(bool scheduled)
    {
        int slotNumber = (int)_slotNumber;
        int driveNumber = _selectedDriveIndex + 1; // Convert 0-based to 1-based

        _statusMutator.MutateDrive(slotNumber, driveNumber, builder =>
        {
            builder.MotorOffScheduled = scheduled;
        });
    }

    /// <summary>
    /// Updates the MotorOn status flag for the currently selected drive.
    /// </summary>
    /// <param name="motorOn">True if motor is running, false if motor is off.</param>
    private void UpdateMotorOnStatus(bool motorOn)
    {
        int slotNumber = (int)_slotNumber;
        int driveNumber = _selectedDriveIndex + 1; // Convert 0-based to 1-based

        _statusMutator.MutateDrive(slotNumber, driveNumber, builder =>
        {
            builder.MotorOn = motorOn;
        });
    }

    /// <summary>
    /// Updates the phase state for the currently selected drive.
    /// </summary>
    private void UpdatePhaseState()
    {
        int slotNumber = (int)_slotNumber;
        int driveNumber = _selectedDriveIndex + 1;

        _statusMutator.MutateDrive(slotNumber, driveNumber, builder =>
        {
            builder.PhaseState = _currentPhase;
        });
    }

    /// <summary>
    /// Updates track and sector for the currently selected drive.
    /// </summary>
    private void UpdateTrackAndSector(double track, int sector)
    {
        int slotNumber = (int)_slotNumber;
        int driveNumber = _selectedDriveIndex + 1;

        _statusMutator.MutateDrive(slotNumber, driveNumber, builder =>
        {
            builder.Track = track;
            builder.Sector = sector;
        });
    }

    /// <summary>
    /// Handles drive selection (Drive 1 or Drive 2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Drive Switching Behavior:</strong> When switching from one drive to another:
    /// <list type="bullet">
    /// <item>OLD drive: Motor turns off IMMEDIATELY (hardware can only power one motor), phases are cleared</item>
    /// <item>NEW drive: Phases are cleared (controller resets phases during switch)</item>
    /// </list>
    /// This matches real Disk II hardware where the controller can only supply current to one motor at a time,
    /// and typically turns off all phases during a drive selection change. Software must reactivate them as needed.
    /// </para>
    /// </remarks>
    protected virtual void HandleDriveSelection(byte ioAddr)
    {
        ProcessBits(_clocking.TotalCycles);
                int oldDriveIndex = _selectedDriveIndex;
                _selectedDriveIndex = (ioAddr & 0x01); // 0xA = drive 0, 0xB = drive 1

                if (oldDriveIndex != _selectedDriveIndex)
                {
#if ControllerDebug
                    Debug.WriteLine($"[{_clocking.TotalCycles}] 💿 DRIVE SELECT: Drive {_selectedDriveIndex + 1} (was Drive {oldDriveIndex + 1})");
#endif

                    // Handle motor state during drive switch
                    // PHASE 4: Motor state is controller-level, no drive assignments needed
                    bool motorWasOn = IsMotorRunning;

                    if (motorWasOn)
                    {
                        // Motor stays running - controller motor line switches to new drive
                        // Need to update status: OLD drive motor OFF, NEW drive motor ON

                        // Clear OLD drive's motor status
                        int slotNumber = (int)_slotNumber;
                        int oldDriveNumber = oldDriveIndex + 1;
                        _statusMutator.MutateDrive(slotNumber, oldDriveNumber, builder =>
                        {
                            builder.MotorOn = false;
                            builder.MotorOffScheduled = false;
                        });

                        // Cancel any pending scheduled motor-off if one was active
                        if (_motorOffScheduledCycle > 0)
                        {
                            _motorOffScheduledCycle = 0;
                        }

                        // Set NEW drive's motor status (will be set after _selectedDriveIndex is updated)
                        // This will happen at the end when we call UpdatePhaseState()
                    }

                    // Clear OLD drive's phase state in status display before switching
                    int slotNumber2 = (int)_slotNumber;
                    int oldDriveNumber2 = oldDriveIndex + 1;
                    _statusMutator.MutateDrive(slotNumber2, oldDriveNumber2, builder =>
                    {
                        builder.PhaseState = 0;
                    });

                        // CRITICAL: Reset bit timing when switching drives
                        // The timing state is controller-level, not per-drive, so we must reset it
                        // to prevent stale timing from affecting the new drive
                        _lastBitShiftCycle = _clocking.TotalCycles;

                        // Clear controller phases during drive switch (common hardware behavior)
                        _currentPhase = 0;

                        // Handle NEW drive: phases start at ---- (software will activate as needed)
                        UpdatePhaseState();

                        // If motor was running, update NEW drive's motor status
                        if (motorWasOn)
                        {
                            UpdateMotorOnStatus(true);
                        }
                    }
            }

    /// <summary>
    /// Handles Q6/Q7 control line reads and data operations.
    /// </summary>
    protected virtual byte? HandleQ6Q7Read(byte ioAddr)
    {
        // Update Q6/Q7 state based on address
        UpdateQ6Q7State(ioAddr);

        // Determine operation based on Q6/Q7 combination
        if (!_q6 && !_q7) // Q6=0, Q7=0: READ DATA
        {
            byte result = ReadShiftRegister();
            // Don't reset! ProcessBits() maintains position continuity
            // The frequent resets were breaking prologue detection
            return result;
        }
        else if (_q6 && !_q7) // Q6=1, Q7=0: SENSE WRITE PROTECT
        {
            // Also read shift register for $C08C (LATCH_OFF/SHIFT during read mode)
            // PHASE 3: Check controller motor state instead of drive state
            if (ioAddr == 0x0C && SelectedDrive != null && IsMotorRunning)
            {
                byte result = ReadShiftRegister();
                // Don't reset! ProcessBits() maintains position continuity
                return result;
            }
            return ReadWriteProtectStatus();
        }
        else if (!_q6 && _q7) // Q6=0, Q7=1: WRITE STATUS/TIMING
        {
            // Reset sequencer when entering write prep mode
            _lastBitShiftCycle = _clocking.TotalCycles;
            return 0x00;
        }
        else // Q6=1, Q7=1: WRITE MODE (reading does nothing meaningful)
        {
            return 0x00;
        }
    }

    /// <summary>
    /// Handles Q6/Q7 control line writes and data operations.
    /// </summary>
    protected virtual void HandleQ6Q7Write(byte ioAddr, byte value)
    {
        // Update Q6/Q7 state
        UpdateQ6Q7State(ioAddr);

        // Q6=1, Q7=1: LOAD WRITE LATCH
        if (_q6 && _q7)
        {
            _shiftRegister = value;
            // Reset sequencer clock when loading write latch
            _lastBitShiftCycle = _clocking.TotalCycles;
            // In real hardware, this would start automatically clocking out bits
            // For now, we'll handle writing in a simplified manner
            WriteShiftRegister();
        }
        // Q6=0, Q7=1: WRITE PREP (TypeScript resets sequencer and clears register during read mode)
        else if (!_q6 && _q7)
        {
            // Reset sequencer when transitioning to write mode
            _lastBitShiftCycle = _clocking.TotalCycles;
        }
        // Q7=0: Exiting write mode
        else if (!_q7)
        {
            // Reset sequencer when exiting write mode
            _lastBitShiftCycle = _clocking.TotalCycles;
        }
    }

    /// <summary>
    /// Updates Q6 and Q7 control line states based on I/O address.
    /// </summary>
    protected void UpdateQ6Q7State(byte ioAddr)
    {
        switch (ioAddr)
        {
            case 0x0C: _q6 = false; break; // Q6L
            case 0x0D: _q6 = true;  break; // Q6H
            case 0x0E: _q7 = false; break; // Q7L
            case 0x0F: _q7 = true;  break; // Q7H
        }
    }

    /// <summary>
    /// Processes disk bits proportionally to elapsed time since the last shift.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method ensures the virtual disk "spins" in sync with CPU cycles.
    /// It uses the provider's incremental timing model which respects the WOZ
    /// file's optimalTiming value and maintains proper cycle remainder tracking.
    /// </para>
    /// <para>
    /// <strong>Self-Synchronization Logic (from TypeScript reference):</strong>
    /// GCR encoding requires special handling when a byte is complete (MSB=1):
    /// <list type="bullet">
    /// <item>If byte complete AND incoming bit is 0: DON'T shift (preserve byte for software)</item>
    /// <item>If byte complete AND incoming bit is 1: CLEAR register and start new byte</item>
    /// </list>
    /// This is how sync bytes (FF with no consecutive 0-bits) signal "start fresh".
    /// </para>
    /// </remarks>
    protected virtual void ProcessBits(ulong currentCycle)
    {
        IDiskIIDrive? drive = SelectedDrive;
        // PHASE 3: Check controller motor state instead of drive state
        if (drive == null || !IsMotorRunning)
        {
            _lastBitShiftCycle = currentCycle;
            return;
        }

        // Calculate elapsed cycles since last call
        double elapsedCycles = currentCycle - _lastBitShiftCycle;
        _lastBitShiftCycle = currentCycle;

        // Skip if no time has elapsed
        if (elapsedCycles <= 0)
        {
            return;
        }

        // Get bits from provider using incremental timing model
        // Provider handles cycle remainder and respects optimalTiming from WOZ
        Span<bool> bits = stackalloc bool[64]; // Max bits we'd process in one call
        double cyclesPerBit = drive.OptimalBitTiming / 8.0;
        double cyclesToProcess = elapsedCycles;

        while (true)
        {
            int bitCount = drive.AdvanceAndReadBits(cyclesToProcess, bits);
            cyclesToProcess = 0;

            if (bitCount <= 0)
            {
                return;
            }


            bool stopEarly = false;

            // Process each bit with TypeScript self-sync logic
            for (int i = 0; i < bitCount; i++)
            {
                bool bit = bits[i];
                bool byteComplete = (_shiftRegister & 0x80) != 0;

                // CRITICAL: TypeScript self-sync condition
                // Don't shift if byte is complete AND incoming bit is 0 (preserves completed byte)
                if (!(byteComplete && !bit))
                {
                    if (byteComplete)
                    {
                        // Byte was complete, incoming bit is 1: clear and start fresh
                        _shiftRegister = 0;
                    }
                    // Shift in the new bit
                    _shiftRegister = (byte)((_shiftRegister << 1) | (bit ? 1 : 0));
                }

                // DIAGNOSTIC tracking - always processes every bit
                _diagnosticShiftReg = (byte)((_diagnosticShiftReg << 1) | (bit ? 1 : 0));
                if ((_diagnosticShiftReg & 0x80) != 0)
                {
                    byte detectedByte = _diagnosticShiftReg;
                    _diagnosticShiftReg = 0;
                    _diagnosticByteCount++; // Count every byte detected

                    _lastThreeBytes[0] = _lastThreeBytes[1];
                    _lastThreeBytes[1] = _lastThreeBytes[2];
                    _lastThreeBytes[2] = detectedByte;

                    // Process address field state machine
                    ProcessAddressFieldByte(detectedByte);

                    // Process data field state machine
                    ProcessDataFieldByte(detectedByte);

                    // Check for prologues
                    CheckForPrologues(drive);
                }

                // CRITICAL: Stop when main register has a valid byte ready for ROM
                // Break AFTER diagnostic has processed this bit
                // Only break early if there's not enough time remaining for another bit
                if ((_shiftRegister & 0x80) != 0 && (bitCount - i - 1) * cyclesPerBit <= cyclesPerBit / 2)
                {
                    stopEarly = true;
                    break;
                }
            }

            if (stopEarly || bitCount < bits.Length)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Processes a byte in the address field state machine.
    /// </summary>
    private void ProcessAddressFieldByte(byte detectedByte)
    {
        if (_addressFieldState == AddressFieldState.Idle)
        {
            return;
        }

        switch (_addressFieldState)
        {
            case AddressFieldState.ReadingVolume:
            case AddressFieldState.ReadingTrack:
            case AddressFieldState.ReadingSector:
            case AddressFieldState.ReadingChecksum:
                _addressFieldBytes[_addressFieldIndex++] = detectedByte;
                if (_addressFieldIndex == 8)
                {
                    // All address field bytes collected, decode them
                    byte volume = Decode44(_addressFieldBytes[0], _addressFieldBytes[1]);
                    byte track = Decode44(_addressFieldBytes[2], _addressFieldBytes[3]);
                    byte sector = Decode44(_addressFieldBytes[4], _addressFieldBytes[5]);
                    byte checksum = Decode44(_addressFieldBytes[6], _addressFieldBytes[7]);

                    // Verify checksum
                    byte calculatedChecksum = (byte)(volume ^ track ^ sector);
#if ControllerDebug
                    string checksumStatus = (checksum == calculatedChecksum) ? "✓" : "✗ FAIL";

                    Debug.WriteLine($"     📋 Address Field: Vol={volume:D3} Track={track:D2} Sector={sector:D2} Checksum={checksum:X2} {checksumStatus}");
#endif

                    // Update disk status with current track and sector
                    UpdateTrackAndSector(track, sector);

                    _addressFieldState = AddressFieldState.ReadingEpilogue;
                    _addressFieldIndex = 0; // Reset for epilogue
                }
                break;

            case AddressFieldState.ReadingEpilogue:
                // Expect DE AA EB
                if (_addressFieldIndex == 0 && detectedByte != 0xDE)
                {
                    Debug.WriteLine($"     ⚠️ Address epilogue error: Expected DE, got {detectedByte:X2}");
                }
                else if (_addressFieldIndex == 1 && detectedByte != 0xAA)
                {
                    Debug.WriteLine($"     ⚠️ Address epilogue error: Expected AA, got {detectedByte:X2}");
                }
                else if (_addressFieldIndex == 2 && detectedByte != 0xEB)
                {
                    Debug.WriteLine($"     ⚠️ Address epilogue error: Expected EB, got {detectedByte:X2}");
                }

                _addressFieldIndex++;
                if (_addressFieldIndex >= 3)
                {
                    _addressFieldState = AddressFieldState.Idle;
                }
                break;
        }
    }

    /// <summary>
    /// Processes a byte in the data field state machine.
    /// </summary>
    private void ProcessDataFieldByte(byte detectedByte)
    {
        if (_dataFieldState == DataFieldState.Idle)
        {
            return;
        }

        switch (_dataFieldState)
        {
            case DataFieldState.ReadingData:
                _dataFieldBytes[_dataFieldIndex++] = detectedByte;
                if (_dataFieldIndex >= 343)
                {
                    _dataFieldState = DataFieldState.ReadingEpilogue;
                    _dataFieldIndex = 0;
                }
                break;

            case DataFieldState.ReadingEpilogue:
                // Expect DE AA EB
                if (_dataFieldIndex == 0 && detectedByte != 0xDE)
                {
                    Debug.WriteLine($"     ⚠️ Data epilogue error: Expected DE, got {detectedByte:X2}");
                }
                else if (_dataFieldIndex == 1 && detectedByte != 0xAA)
                {
                    Debug.WriteLine($"     ⚠️ Data epilogue error: Expected AA, got {detectedByte:X2}");
                }
                else if (_dataFieldIndex == 2 && detectedByte != 0xEB)
                {
                    Debug.WriteLine($"     ⚠️ Data epilogue error: Expected EB, got {detectedByte:X2}");
                }

                _dataFieldIndex++;

                // After reading all 3 epilogue bytes, decode and dump the data
                if (_dataFieldIndex >= 3)
                {
                    // Decode the sector data
                    byte[] decoded = new byte[256];
                    Decode62(_dataFieldBytes, decoded);

                    _dataFieldState = DataFieldState.Idle;
                }
                break;
        }
    }

    /// <summary>
    /// Checks for address and data prologues in the byte stream.
    /// </summary>
    // TODO: This takes a drive, which probably should be honored at some point.  This will be dealt with as I deep-dive debug this class.
    private void CheckForPrologues(IDiskIIDrive _)
    {
        if (_lastThreeBytes[0] == 0xD5 && _lastThreeBytes[1] == 0xAA && _lastThreeBytes[2] == 0x96)
        {
            // ADDRESS PROLOGUE (D5 AA 96)
            // Start reading address field
            _addressFieldState = AddressFieldState.ReadingVolume;
            _addressFieldIndex = 0;
        }
        else if (_lastThreeBytes[0] == 0xD5 && _lastThreeBytes[1] == 0xAA && _lastThreeBytes[2] == 0xAD)
        {
            // DATA PROLOGUE (D5 AA AD)
            // Start reading data field
            _dataFieldState = DataFieldState.ReadingData;
            _dataFieldIndex = 0;

            // Reset counters at data prologue
            _diagnosticByteCount = 0;
            _latchedReadCount = 0;
        }
    }

    /// <summary>
    /// Reads the shift register, continuously accumulating bits from the drive.
    /// </summary>
    /// <remarks>
    /// CRITICAL: Uses cycle-accurate timing to determine when to shift the next bit.
    /// Real Disk II hardware shifts bits at ~250 KHz (4 cycles per bit at 1 MHz CPU).
    /// Software polls faster than bits arrive, seeing the same value multiple times.
    /// </remarks>
    protected virtual byte ReadShiftRegister()
    {
        ProcessBits(_clocking.TotalCycles);

        byte result = _shiftRegister;

        // CRITICAL: Reading the data register when bit 7 is set clears the register
        // on real hardware (the MSB remains 1 only until the register is read).
        if ((result & 0x80) != 0)
        {
            _latchedReadCount++; // Count latched reads (when byte is ready)
            _shiftRegister = 0;
        }

        return result;
    }

    /// <summary>
    /// Writes the shift register contents to the drive bit-by-bit.
    /// </summary>
    protected virtual void WriteShiftRegister()
    {
        IDiskIIDrive? drive = SelectedDrive;
        Debug.WriteLineIf(SelectedDrive == null, "SelectedDrive is null!");

        // PHASE 3: Check controller motor state instead of drive state
        if (drive == null || !IsMotorRunning || drive.IsWriteProtected())
        {
            return;
        }

        // Write all 8 bits from shift register to drive
        for (int i = 7; i >= 0; i--)
        {
            bool bit = ((_shiftRegister >> i) & 1) == 1;
            drive.SetBit(bit);
        }
    }

    /// <summary>
    /// Decodes a 4-and-4 encoded byte pair (odd/even encoding).
    /// </summary>
    /// <param name="odd">The odd byte (high bits).</param>
    /// <param name="even">The even byte (low bits).</param>
    /// <returns>The decoded byte value.</returns>
    /// <remarks>
    /// Apple II DOS 3.3 uses 4-and-4 encoding for address fields:
    /// - Odd byte stores: (value >> 1) | 0xAA
    /// - Even byte stores: value | 0xAA
    /// - Decoded: ((odd &lt;&lt; 1) | 0x01) &amp; even
    /// </remarks>
    private static byte Decode44(byte odd, byte even)
    {
        return (byte)(((odd << 1) | 0x01) & even);
    }

    /// <summary>
    /// Decodes 343 6-2 encoded bytes into 256 data bytes using ProDOS 6-2 format.
    /// </summary>
    /// <param name="encoded">The 343 encoded bytes (256 6-bit + 86 2-bit + 1 checksum).</param>
    /// <param name="decoded">Output array for 256 decoded bytes.</param>
    /// <remarks>
    /// ProDOS 6-2 encoding:
    /// - First 256 bytes contain the high 6 bits of each data byte
    /// - Next 86 bytes contain packed 2-bit values (3 per byte, 6 bits used)
    /// - Last byte is checksum
    /// - Each data byte = (high6bits &lt;&lt; 2) | low2bits
    /// - Bytes are stored with 6-2 translation (0x96-0xFF range)
    /// </remarks>
    private static void Decode62(byte[] encoded, byte[] decoded)
    {
        // Reverse the 6-2 encoding translation table
        // Standard disk bytes use 0x96-0xFF (values 0-63)
        byte[] decode62Table = new byte[256];
        for (int i = 0; i < 64; i++)
        {
            decode62Table[0x96 + i] = (byte)i;
        }

        // Decode the first 256 main bytes (6-bit storage)
        byte[] sixes = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            sixes[i] = decode62Table[encoded[i]];
        }

        // Decode the next 86 auxiliary bytes (2-bit storage)
        byte[] twos = new byte[86];
        for (int i = 0; i < 86; i++)
        {
            twos[i] = decode62Table[encoded[256 + i]];
        }

        // Note: encoded[342] is the checksum byte (not decoded here)

        // Combine to form the 256 data bytes
        // The twos array stores low 2 bits: each twos byte has bits for 3 data bytes
        // ProDOS stores them as: [byte2-low2][byte1-low2][byte0-low2] in bits 5-4, 3-2, 1-0
        for (int i = 0; i < 256; i++)
        {
            int twosIndex = i / 3;
            int bitShift = (2 - (i % 3)) * 2;  // 4, 2, 0 (reverse order: bits 5-4, 3-2, 1-0)

            byte high6 = sixes[i];
            byte low2 = (byte)((twos[twosIndex] >> bitShift) & 0x03);

            decoded[i] = (byte)((high6 << 2) | low2);
        }
    }

    /// <summary>
    /// Reads write-protect status from the selected drive.
    /// </summary>
    protected virtual byte ReadWriteProtectStatus()
    {
        IDiskIIDrive? drive = SelectedDrive;
        Debug.WriteLineIf(SelectedDrive == null, "SelectedDrive is null!");
        if (drive == null)
        {
            return 0x00;
        }

        // Bit 7 = 1 if write-protected, 0 if write-enabled
        return (byte)(drive.IsWriteProtected() ? 0x80 : 0x00);
    }

    /// <inheritdoc />
    public abstract byte? ReadRom(byte offset);

    /// <inheritdoc />
    public void WriteRom(byte offset, byte value)
    {
        // NOP - ROM is read-only
    }

    /// <inheritdoc />
    public byte? ReadExtendedRom(ushort address) => null;

    /// <inheritdoc />
    public void WriteExtendedRom(ushort address, byte value)
    {
        // NOP
    }

    /// <inheritdoc />
    public abstract ICard Clone();

    /// <summary>
    /// Called when the card is installed into a slot, creating the drive instances.
    /// </summary>
    /// <param name="slot">The slot number where the card is being installed.</param>
    /// <remarks>
    /// <para>
    /// This deferred initialization allows the card factory to maintain lightweight
    /// prototype instances, and each cloned card creates its drives only when installed.
    /// </para>
    /// <para>
    /// Drives are created via the <see cref="IDiskIIFactory"/>, which provides them
    /// already wrapped with <see cref="DiskIIDebugDecorator"/> for diagnostic logging
    /// with slot-aware naming (e.g., "Slot6-D1", "Slot6-D2").
    /// </para>
    /// </remarks>
    public void OnInstalled(SlotNumber slot)
    {
        _slotNumber = slot;
        string slotName = $"Slot{(int)slot}";
        _drives =
        [
            _diskIIFactory.CreateDrive($"{slotName}-D1"),
            _diskIIFactory.CreateDrive($"{slotName}-D2")
        ];
#if ControllerDebug
        Debug.WriteLine($"DiskIIControllerCard installed in {slot}: Created {_drives.Length} drives");
#endif
    }

    /// <inheritdoc />
    public virtual string GetMetadata() => string.Empty;

    /// <inheritdoc />
    public virtual bool ApplyMetadata(string metadata) => true;

    /// <inheritdoc />
    public void Reset()
    {
        _motorOffScheduledCycle = 0;

        // Set motor state to Off (encapsulated - handles notification)
        SetMotorState(DiskIIMotorState.Off);

#if ControllerDebug
        Debug.WriteLine($"[{_clocking.TotalCycles}] 🔴 RESET: Immediate motor-off on Drive {_selectedDriveIndex + 1}");
#endif

        // NOW it's safe to reset controller state (motors are stopped)
        _shiftRegister = 0;
        _diagnosticShiftReg = 0;

        // Reset Q6/Q7 control lines
        _q6 = false;
        _q7 = false;

        // Reset phase state bitfield
        _currentPhase = 0;

        // Reset timing synchronization
        _lastBitShiftCycle = _clocking.TotalCycles;

        // Reset diagnostic tracking
        _lastThreeBytes = new byte[3];
        _diagnosticByteCount = 0;
        _latchedReadCount = 0;
        _addressFieldState = AddressFieldState.Idle;
        _addressFieldIndex = 0;
        _dataFieldState = DataFieldState.Idle;
        _dataFieldIndex = 0;

        // Update phase state for currently selected drive
        UpdatePhaseState(); // All phases off
    }

    /// <summary>
    /// Handles a message sent to this card.
    /// </summary>
    /// <param name="message">The message to process.</param>
    /// <exception cref="Exceptions.CardMessageException">
    /// Thrown if the message is not recognized or cannot be processed.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <strong>Supported Messages:</strong><br/>
    /// - <see cref="Messages.IdentifyCardMessage"/>: Emits card identity via response channel<br/>
    /// - <see cref="Messages.EnumerateDevicesMessage"/>: Emits device list via response channel<br/>
    /// - <see cref="Messages.RefreshStatusMessage"/>: Pushes current drive status<br/>
    /// - <see cref="DiskII.Messages.InsertDiskMessage"/>: Inserts disk image into drive<br/>
    /// - <see cref="DiskII.Messages.InsertBlankDiskMessage"/>: Creates and inserts blank disk<br/>
    /// - <see cref="DiskII.Messages.EjectDiskMessage"/>: Ejects disk from drive<br/>
    /// - <see cref="DiskII.Messages.SwapDrivesMessage"/>: Swaps disk media between drives<br/>
    /// - <see cref="DiskII.Messages.SaveDiskMessage"/>: Saves disk to attached destination<br/>
    /// - <see cref="DiskII.Messages.SaveDiskAsMessage"/>: Exports disk to user-specified path<br/>
    /// - <see cref="DiskII.Messages.SetWriteProtectMessage"/>: Toggles write protection<br/>
    /// </para>
    /// </remarks>
    public virtual void HandleMessage(ICardMessage message)
    {
        // Phase 2 implementation: Full disk management messages
        switch (message)
        {
            case Messages.IdentifyCardMessage:
                HandleIdentifyCard();
                break;

            case Messages.EnumerateDevicesMessage:
                HandleEnumerateDevices();
                break;

            case Messages.RefreshStatusMessage:
                HandleRefreshStatus();
                break;

            case DiskII.Messages.InsertDiskMessage insert:
                ValidateDriveNumber(insert.DriveNumber);
                _drives[insert.DriveNumber - 1].InsertDisk(insert.DiskImagePath);
                break;

            case DiskII.Messages.InsertBlankDiskMessage blank:
                ValidateDriveNumber(blank.DriveNumber);
                InsertBlankDisk(blank.DriveNumber, blank.FilePath);
                break;

            case DiskII.Messages.EjectDiskMessage eject:
                ValidateDriveNumber(eject.DriveNumber);
                _drives[eject.DriveNumber - 1].EjectDisk(); // no-op if empty
                break;

            case DiskII.Messages.SwapDrivesMessage:
                if (_drives.Length < 2)
                {
                    throw new Exceptions.CardMessageException(
                        "Cannot swap drives: controller has fewer than 2 drives.");
                }
                SwapDriveMedia();
                break;

            case DiskII.Messages.SaveDiskMessage save:
                ValidateDriveNumber(save.DriveNumber);
                SaveDriveImage(save.DriveNumber); // uses attached DestinationFilePath
                break;

            case DiskII.Messages.SaveDiskAsMessage saveAs:
                ValidateDriveNumber(saveAs.DriveNumber);
                ExportDriveImage(saveAs.DriveNumber, saveAs.FilePath);
                break;

            case DiskII.Messages.SetWriteProtectMessage wp:
                ValidateDriveNumber(wp.DriveNumber);
                SetDriveWriteProtect(wp.DriveNumber, wp.WriteProtected);
                break;

            default:
                throw new Exceptions.CardMessageException(
                    $"Disk II controller does not recognize message type '{message.GetType().Name}'.");
        }
    }

    /// <summary>
    /// Validates that a drive number is within the valid range for this controller.
    /// </summary>
    /// <param name="driveNumber">1-based drive number to validate.</param>
    /// <exception cref="Exceptions.CardMessageException">
    /// Thrown if the drive number is out of range.
    /// </exception>
    private void ValidateDriveNumber(int driveNumber)
    {
        if (driveNumber < 1 || driveNumber > _drives.Length)
        {
            throw new Exceptions.CardMessageException(
                $"Invalid drive number {driveNumber}. Valid range: 1-{_drives.Length}.");
        }
    }

    /// <summary>
    /// Handles the IdentifyCardMessage by emitting card identity via the response channel.
    /// </summary>
    private void HandleIdentifyCard()
    {
        _responseEmitter.Emit(_slotNumber, Id, new Messages.CardIdentityPayload(Name));
    }

    /// <summary>
    /// Handles the EnumerateDevicesMessage by emitting device list via the response channel.
    /// </summary>
    private void HandleEnumerateDevices()
    {
        var devices = new Messages.PeripheralType[_drives.Length];
        Array.Fill(devices, Messages.PeripheralType.Floppy525);
        _responseEmitter.Emit(_slotNumber, Id, new Messages.DeviceListPayload(devices));
    }

    /// <summary>
    /// Handles the RefreshStatusMessage by pushing current drive status through IDiskStatusMutator.
    /// </summary>
    private void RefreshAllDriveStatus()
    {
        // Push current status for all drives
        for (int i = 0; i < _drives.Length; i++)
        {
            int driveNumber = i + 1;
            IDiskIIDrive drive = _drives[i];

            _statusMutator.MutateDrive((int)_slotNumber, driveNumber, builder =>
            {
                builder.Track = drive.Track;
                builder.IsReadOnly = drive.IsWriteProtected();
                builder.HasValidTrackData = drive.HasDisk;

                // Get dirty/destination state from internal image (via interface)
                var internalImage = drive.InternalImage;
                builder.IsDirty = internalImage?.IsDirty ?? false;
                builder.HasDestinationPath = !string.IsNullOrEmpty(internalImage?.DestinationFilePath);

                // Update disk image path and filename from CurrentDiskPath
                string? currentPath = drive.CurrentDiskPath;
                builder.DiskImagePath = currentPath ?? string.Empty;
                builder.DiskImageFilename = !string.IsNullOrEmpty(currentPath) 
                    ? System.IO.Path.GetFileName(currentPath) 
                    : string.Empty;
            });
        }
    }

    /// <summary>
    /// Handles the RefreshStatusMessage.
    /// </summary>
    private void HandleRefreshStatus()
    {
        RefreshAllDriveStatus();
    }

    /// <summary>
    /// Inserts a blank disk into the specified drive.
    /// </summary>
    /// <param name="driveNumber">1-based drive number (1 or 2).</param>
    /// <param name="filePath">Optional file path to associate with the blank disk.
    /// If empty, a default destination path is derived.</param>
    /// <exception cref="Exceptions.CardMessageException">
    /// Thrown if the drive number is invalid or disk creation fails.
    /// </exception>
    private void InsertBlankDisk(int driveNumber, string filePath)
    {
        IDiskIIDrive drive = _drives[driveNumber - 1];

        try
        {
            // Eject any existing disk first
            drive.EjectDisk();

            // Derive destination path if not provided
            if (string.IsNullOrEmpty(filePath))
            {
                // TODO: Get last export directory from settings (for now, use current directory)
                string directory = Environment.CurrentDirectory;
                filePath = System.IO.Path.Combine(directory, "blank.nib");

                // Handle collision: blank.nib -> blank_new.nib -> blank_new2.nib, etc.
                filePath = DeriveUniqueDestinationPath(filePath);
            }

            // Create a blank internal disk image (35 tracks, unformatted)
            var blankImage = new InternalDiskImage(
                trackCount: 35,
                standardTrackBitCount: 51200) // Standard NIB bit count
            {
                // Set source and destination paths (blank disk is its own source)
                SourceFilePath = filePath,
                OriginalFormat = DiskFormat.Nib,
                DestinationFilePath = filePath,
                DestinationFormat = DiskFormat.Nib
            };

            // Create provider and insert into drive
            var provider = new UnifiedDiskImageProvider(blankImage);
            drive.ImageProvider = provider;
            provider.SetQuarterTrack(drive.QuarterTrack);

            // Update status
            RefreshAllDriveStatus();

#if ControllerDebug
            Debug.WriteLine($"Inserted blank disk into drive {driveNumber}: {filePath}");
#endif
        }
        catch (Exception ex)
        {
            throw new Exceptions.CardMessageException(
                $"Failed to insert blank disk into drive {driveNumber}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Derives a unique destination path by checking for file existence and incrementing suffix.
    /// </summary>
    /// <param name="proposedPath">The initially proposed file path.</param>
    /// <returns>A unique file path that doesn't exist on disk.</returns>
    /// <remarks>
    /// <para>
    /// If the proposed path already exists, this method increments a suffix:
    /// <code>
    /// blank.nib -> blank_new.nib (if blank.nib exists)
    /// blank.nib -> blank_new2.nib (if both blank.nib and blank_new.nib exist)
    /// blank_new.nib -> blank_new2.nib (strips existing suffix before incrementing)
    /// </code>
    /// </para>
    /// </remarks>
    private static string DeriveUniqueDestinationPath(string proposedPath)
    {
        // If proposed path doesn't exist, use it as-is
        if (!System.IO.File.Exists(proposedPath))
        {
            return proposedPath;
        }

        string directory = System.IO.Path.GetDirectoryName(proposedPath) ?? string.Empty;
        string filenameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(proposedPath);
        string extension = System.IO.Path.GetExtension(proposedPath);

        // Strip existing _new or _newN suffix using regex
#pragma warning disable SYSLIB1045 // Convert to 'GeneratedRegexAttribute'
        var match = System.Text.RegularExpressions.Regex.Match(filenameWithoutExt, @"^(.+?)_new(\d*)$");
#pragma warning restore SYSLIB1045 // Convert to 'GeneratedRegexAttribute'
        string baseName = match.Success ? match.Groups[1].Value : filenameWithoutExt;

        // Try incrementing suffix until we find a non-existing path
        int suffix = 1;
        string candidatePath;
        do
        {
            string newFilename = suffix == 1
                ? $"{baseName}_new{extension}"
                : $"{baseName}_new{suffix}{extension}";
            candidatePath = System.IO.Path.Combine(directory, newFilename);
            suffix++;
        }
        while (System.IO.File.Exists(candidatePath) && suffix < 1000); // Safety limit

        return candidatePath;
    }

    /// <summary>
    /// Swaps the disk media between Drive 1 and Drive 2.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Swap Mechanics:</strong><br/>
    /// - Controller swaps internal image providers between drives<br/>
    /// - Preserves all in-memory modifications (dirty data)<br/>
    /// - Updates providers with new drive's quarter-track position<br/>
    /// - Resets read timing if motor is running<br/>
    /// - Status is updated via IDiskStatusMutator to reflect new filenames<br/>
    /// </para>
    /// <para>
    /// <strong>Implementation:</strong><br/>
    /// Uses internal accessors within the same assembly to swap providers directly,
    /// avoiding reload from disk which would lose unsaved changes.
    /// </para>
    /// </remarks>
    private void SwapDriveMedia()
    {
        if (_drives.Length < 2)
        {
            return; // Already validated by caller, but defensive check
        }

        // Swap internal image providers directly via interface (preserves dirty data)
        IDiskImageProvider? provider1 = _drives[0].ImageProvider;
        IDiskImageProvider? provider2 = _drives[1].ImageProvider;

        _drives[0].ImageProvider = provider2;
        _drives[1].ImageProvider = provider1;

        // Update providers with their new drive's quarter-track position
        provider1?.SetQuarterTrack(_drives[1].QuarterTrack);
        provider2?.SetQuarterTrack(_drives[0].QuarterTrack);

        // If motor is running, reset read positions to avoid corrupt data streams
        if (IsMotorRunning)
        {
            provider1?.NotifyMotorStateChanged(true, _clocking.TotalCycles);
            provider2?.NotifyMotorStateChanged(true, _clocking.TotalCycles);
        }

        // Update status for both drives
        RefreshAllDriveStatus();
#if ControllerDebug
        Debug.WriteLine($"[{_clocking.TotalCycles}] 🔄 Swapped disk media between Drive 1 and Drive 2 (preserving dirty data)");
#endif
    }

    /// <summary>
    /// Saves the disk image in the specified drive to its attached destination path.
    /// </summary>
    /// <param name="driveNumber">1-based drive number (1 or 2).</param>
    /// <exception cref="Exceptions.CardMessageException">
    /// Thrown if no disk is inserted, no destination path is attached, or save fails.
    /// </exception>
    private void SaveDriveImage(int driveNumber)
    {
        IDiskIIDrive drive = _drives[driveNumber - 1];

        if (!drive.HasDisk)
        {
            throw new Exceptions.CardMessageException(
                $"Cannot save: no disk inserted in drive {driveNumber}.");
        }

        // Access internal state via interface
        InternalDiskImage? internalImage = drive.InternalImage ?? throw new Exceptions.CardMessageException(
                "Cannot save: no internal disk image available.");

        if (string.IsNullOrEmpty(internalImage.DestinationFilePath))
        {
            throw new Exceptions.CardMessageException(
                "Cannot save: no destination path attached. Use Save As instead.");
        }

        // Export to destination path
        try
        {
            DiskFormat format = internalImage.DestinationFormat != DiskFormat.Unknown
                ? internalImage.DestinationFormat
                : DiskFormatHelper.GetFormatFromPath(internalImage.DestinationFilePath);

            IDiskImageExporter exporter = DiskFormatHelper.GetExporterForFormat(format);
            exporter.Export(internalImage, internalImage.DestinationFilePath);

            // Clear dirty flag
            internalImage.ClearDirty();

            // Update SourceFilePath to match saved destination
            // (Cannot modify init property - requires new InternalDiskImage instance)
            // TODO: This requires refactoring InternalDiskImage to support SourceFilePath updates
            // For now, just clear dirty and update status

            // Update status to reflect clean state
            RefreshAllDriveStatus();
#if ControllerDebug
            Debug.WriteLine($"Saved disk image from drive {driveNumber} to {internalImage.DestinationFilePath}");
#endif
        }
        catch (Exception ex)
        {
            throw new Exceptions.CardMessageException(
                $"Failed to save disk image: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Exports the disk image in the specified drive to a user-specified path.
    /// </summary>
    /// <param name="driveNumber">1-based drive number (1 or 2).</param>
    /// <param name="filePath">Destination path for the exported disk image.</param>
    /// <exception cref="Exceptions.CardMessageException">
    /// Thrown if no disk is inserted or export fails.
    /// </exception>
    private void ExportDriveImage(int driveNumber, string filePath)
    {
        IDiskIIDrive drive = _drives[driveNumber - 1];

        if (!drive.HasDisk)
        {
            throw new Exceptions.CardMessageException(
                $"Cannot export: no disk inserted in drive {driveNumber}.");
        }

        // Access internal state via interface
        InternalDiskImage? internalImage = drive.InternalImage ?? throw new Exceptions.CardMessageException(
                "Cannot export: no internal disk image available.");

        // Determine format from file extension
        DiskFormat format = DiskFormatHelper.GetFormatFromPath(filePath);
        if (!DiskFormatHelper.IsExportSupported(format))
        {
            throw new Exceptions.CardMessageException(
                $"Unsupported export format: {format}");
        }

        // Export to specified path
        try
        {
            IDiskImageExporter exporter = DiskFormatHelper.GetExporterForFormat(format);
            exporter.Export(internalImage, filePath);

            // Update destination path and format
            internalImage.DestinationFilePath = filePath;
            internalImage.DestinationFormat = format;

            // Clear dirty flag
            internalImage.ClearDirty();

            // Update status to reflect new destination and clean state
            RefreshAllDriveStatus();
#if ControllerDebug
            Debug.WriteLine($"Exported disk image from drive {driveNumber} to {filePath}");
#endif
        }
        catch (Exception ex)
        {
            throw new Exceptions.CardMessageException(
                $"Failed to export disk image: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Sets the write-protect state on the specified drive's disk image.
    /// </summary>
    /// <param name="driveNumber">1-based drive number (1 or 2).</param>
    /// <param name="writeProtected">True to enable write protection, false to disable.</param>
    /// <exception cref="Exceptions.CardMessageException">
    /// Thrown if no disk is inserted.
    /// </exception>
    private void SetDriveWriteProtect(int driveNumber, bool writeProtected)
    {
        IDiskIIDrive drive = _drives[driveNumber - 1];

        if (!drive.HasDisk)
        {
            throw new Exceptions.CardMessageException(
                $"Cannot set write protection: no disk inserted in drive {driveNumber}.");
        }

        // Access provider via interface
        if (drive.ImageProvider == null)
        {
            throw new Exceptions.CardMessageException(
                "Cannot set write protection: image provider not available.");
        }

        drive.ImageProvider.IsWriteProtected = writeProtected;

        // Update status
        RefreshAllDriveStatus();
#if ControllerDebug
        Debug.WriteLine($"Drive {driveNumber} write protection: {(writeProtected ? "ON" : "OFF")}");
#endif
    }
}
