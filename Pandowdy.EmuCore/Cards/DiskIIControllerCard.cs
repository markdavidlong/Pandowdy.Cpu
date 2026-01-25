using System.Diagnostics;
using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.DiskII;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Services;

namespace Pandowdy.EmuCore.Cards;

/// <summary>
/// Abstract base class for Disk II controller cards.
/// </summary>
/// <remarks>
/// <para>
/// The Disk II controller uses Q6 and Q7 control lines to select operating modes:
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
/// publish phase changes, motor-off scheduling, and sector detection to the UI.
/// Drives publish their own motor/track/disk state through the <see cref="DiskIIStatusDecorator"/>.
/// </para>
/// </remarks>
public abstract class DiskIIControllerCard : ICard
{
    protected readonly CpuClockingCounters _clocking;
    protected IDiskIIDrive[] _drives = [];
    protected readonly IDiskIIFactory _diskIIFactory;
    protected readonly IDiskStatusMutator _statusMutator;
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

    protected IDiskIIDrive? SelectedDrive =>
        _selectedDriveIndex < _drives.Length ? _drives[_selectedDriveIndex] : null;

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
        //   N  NE   E  SE   S  SW   W  NW
        //   0   1   2   3   4   5   6   7
        [  0,  1,  2,  3,  0, -3, -2, -1 ], // 0 N
        [ -1,  0,  1,  2,  3,  0, -3, -2 ], // 1 NE
        [ -2, -1,  0,  1,  2,  3,  0, -3 ], // 2 E
        [ -3, -2, -1,  0,  1,  2,  3,  0 ], // 3 SE
        [  0, -3, -2, -1,  0,  1,  2,  3 ], // 4 S
        [  3,  0, -3, -2, -1,  0,  1,  2 ], // 5 SW
        [  2,  3,  0, -3, -2, -1,  0,  1 ], // 6 W
        [  1,  2,  3,  0, -3, -2, -1,  0 ], // 7 NW
    ];

    // Diagnostic counters for troubleshooting
    private static ulong _totalReads = 0;
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
    private byte _dataChecksum = 0;

    // Bit timing for shift register
    private double _lastBitShiftCycle = 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiskIIControllerCard"/> class.
    /// </summary>
    /// <param name="cpuClocking">The CPU clocking counters for timing operations.</param>
    /// <param name="diskIIFactory">Factory for creating drive instances.</param>
    /// <param name="statusMutator">Status mutator for publishing controller state changes.</param>
    protected DiskIIControllerCard(
        CpuClockingCounters cpuClocking,
        IDiskIIFactory diskIIFactory,
        IDiskStatusMutator statusMutator)
    {
        ArgumentNullException.ThrowIfNull(cpuClocking);
        ArgumentNullException.ThrowIfNull(diskIIFactory);
        ArgumentNullException.ThrowIfNull(statusMutator);

        _clocking = cpuClocking;
        _diskIIFactory = diskIIFactory;
        _statusMutator = statusMutator;

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
            if (ioAddr == 0x8 && SelectedDrive != null && SelectedDrive.MotorOn && !_q7)
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
        if (SelectedDrive != null && SelectedDrive.MotorOn && position >= 0)
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
            Debug.WriteLine($"[{_clocking.TotalCycles}] ðŸ”„ Head moved: Position {lastPosition}â†’{targetPosition}, Direction={direction:+0;-0}, QuarterTrack={SelectedDrive.QuarterTrack}, Track={SelectedDrive.Track:F2}");
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
                    Debug.WriteLine($"[{_clocking.TotalCycles}] â¹ï¸ MOTOR-OFF CANCELLED (was scheduled for cycle {_motorOffScheduledCycle})");
#endif
                    _motorOffScheduledCycle = 0;

                    // Update status: motor-off no longer scheduled
                    UpdateMotorOffScheduledStatus(false);
                }

                if (!SelectedDrive.MotorOn)
                {
#if ControllerDebug
                Debug.WriteLine($"[{_clocking.TotalCycles}] ðŸ”µ MOTOR ON - Drive {_selectedDriveIndex + 1}, Track {SelectedDrive.Track:F2}");
#endif
                    _lastBitShiftCycle = _clocking.TotalCycles;  // Only reset timing (matches TypeScript cycleRemainder = 0)
                    // DON'T clear _shiftRegister - it must maintain state across motor cycles!
                    _diagnosticShiftReg = 0; // Clear diagnostic shift register for fresh tracking
                    _diagnosticByteCount = 0; // Reset diagnostic counters
                    _latchedReadCount = 0;
                    _addressFieldState = AddressFieldState.Idle; // Reset address field state
                    _dataFieldState = DataFieldState.Idle; // Reset data field state

                    SelectedDrive.MotorOn = true;
                    // Motor on status is updated by DiskIIDrive.MotorOn setter
                }
            }
            else
            {
                // Motor OFF: Schedule motor-off for 1 second from now (matches TypeScript setTimeout)
                if (_motorOffScheduledCycle == 0)
                {
                    _motorOffScheduledCycle = _clocking.TotalCycles + MotorOffDelayCycles;
#if ControllerDebug
                Debug.WriteLine($"[{_clocking.TotalCycles}] â±ï¸ MOTOR-OFF SCHEDULED for cycle {_motorOffScheduledCycle} (~1 sec delay)");
#endif
                    // Update status: motor-off now scheduled
                    UpdateMotorOffScheduledStatus(true);
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
            Debug.WriteLine($"[{_clocking.TotalCycles}] ðŸ”´ MOTOR OFF (delayed) - Drive {_selectedDriveIndex + 1}, Track {SelectedDrive.Track:F2}");
#endif

            // Clear the schedule before turning motor off
            _motorOffScheduledCycle = 0;

            // Update status: motor-off no longer scheduled (motor is now actually off)
            UpdateMotorOffScheduledStatus(false);

            // Turn off the motor (this updates MotorOn status via DiskIIDrive)
            SelectedDrive.MotorOn = false;

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
    /// Granularity: Â±16.6ms (1.7% of 1-second motor-off delay) - acceptable for mechanical timing.
    /// </remarks>
    private void OnVBlankTick()
    {
        CheckPendingMotorOff();
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
    /// <item>OLD drive: Motor is scheduled to turn off (1-second delay), phases are cleared</item>
    /// <item>NEW drive: Phases are cleared (controller resets phases during switch)</item>
    /// </list>
    /// This matches real Disk II hardware where the controller typically turns off all phases
    /// during a drive selection change, and software must reactivate them as needed.
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
                    Debug.WriteLine($"[{_clocking.TotalCycles}] ðŸ’¿ DRIVE SELECT: Drive {_selectedDriveIndex + 1} (was Drive {oldDriveIndex + 1})");
        #endif

                    // Handle OLD drive: schedule motor-off and clear phases
                    var oldDrive = _drives[oldDriveIndex];
                    if (oldDrive != null && oldDrive.MotorOn)
                    {
                        // Schedule motor-off for the deselected drive (1-second delay)
                        _motorOffScheduledCycle = _clocking.TotalCycles + MotorOffDelayCycles;
        #if ControllerDebug
                        Debug.WriteLine($"[{_clocking.TotalCycles}] â±ï¸ MOTOR-OFF SCHEDULED for Drive {oldDriveIndex + 1} at cycle {_motorOffScheduledCycle}");
        #endif

                        // Update status for old drive's motor-off scheduling
                        int slotNumber = (int)_slotNumber;
                        _statusMutator.MutateDrive(slotNumber, oldDriveIndex + 1, builder =>
                        {
                            builder.MotorOffScheduled = true;
                        });
                    }

                    // Clear controller phases during drive switch (common hardware behavior)
                    _currentPhase = 0;

                    // Handle NEW drive: phases start at ---- (software will activate as needed)
                    UpdatePhaseState();
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
            if (ioAddr == 0x0C && SelectedDrive != null && SelectedDrive.MotorOn)
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
    /// It processes one bit for every 45/11 CPU cycles (â‰ˆ4.09 cycles) elapsed since the last call.
    /// </para>
    /// <para>
    /// The cycle-based disk position in NibDiskImageProvider already handles
    /// continuous disk rotation - the position is calculated from the absolute
    /// cycle count, not incremented per read. This means we just need to read
    /// one bit per 45/11 cycles elapsed, and the disk position naturally advances.
    /// </para>
    /// </remarks>
    protected virtual void ProcessBits(ulong currentCycle)
    {
        IDiskIIDrive? drive = SelectedDrive;
        if (drive == null || !drive.MotorOn)
        {
            _lastBitShiftCycle = currentCycle;
            return;
        }

        // Calculate how many bits should have been processed based on elapsed cycles
        double elapsedCycles = currentCycle - _lastBitShiftCycle;
        int bitsToProcess = (int)(elapsedCycles / DiskIIConstants.CyclesPerBit);

        // Process each bit sequentially
        for (int i = 0; i < bitsToProcess; i++)
        {
            // Advance to the cycle when this bit should be read (maintaining fractional precision)
            _lastBitShiftCycle += DiskIIConstants.CyclesPerBit;

            // Get the bit at this specific cycle time
            // The disk position is calculated from the cycle count in GetBit()
            bool? bit = drive.GetBit((ulong)_lastBitShiftCycle);

            if (bit.HasValue)
            {
                // CRITICAL: Only shift if byte is not yet complete (MSB not set)
                // Once a byte is latched (MSB = 1), preserve it until software reads it
                // This matches real Disk II hardware behavior
                if ((_shiftRegister & 0x80) == 0)
                {
                    // Shift in the new bit
                    _shiftRegister = (byte)((_shiftRegister << 1) | (bit.Value ? 1 : 0));
                }

                // DIAGNOSTIC tracking - always processes every bit
                _diagnosticShiftReg = (byte)((_diagnosticShiftReg << 1) | (bit.Value ? 1 : 0));
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
            }

            // CRITICAL: Stop when main register has a valid byte ready for ROM
            // Break AFTER diagnostic has processed this bit
            if ((_shiftRegister & 0x80) != 0)
            {
                break;
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
                    string checksumStatus = (checksum == calculatedChecksum) ? "âœ“" : "âœ— FAIL";

                    Debug.WriteLine($"     ðŸ“ Address Field: Vol={volume:D3} Track={track:D2} Sector={sector:D2} Checksum={checksum:X2} {checksumStatus}");
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
                    Debug.WriteLine($"     âš ï¸ Address epilogue error: Expected DE, got {detectedByte:X2}");
                }
                else if (_addressFieldIndex == 1 && detectedByte != 0xAA)
                {
                    Debug.WriteLine($"     âš ï¸ Address epilogue error: Expected AA, got {detectedByte:X2}");
                }
                else if (_addressFieldIndex == 2 && detectedByte != 0xEB)
                {
                    Debug.WriteLine($"     âš ï¸ Address epilogue error: Expected EB, got {detectedByte:X2}");
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
                    Debug.WriteLine($"     âš ï¸ Data epilogue error: Expected DE, got {detectedByte:X2}");
                }
                else if (_dataFieldIndex == 1 && detectedByte != 0xAA)
                {
                    Debug.WriteLine($"     âš ï¸ Data epilogue error: Expected AA, got {detectedByte:X2}");
                }
                else if (_dataFieldIndex == 2 && detectedByte != 0xEB)
                {
                    Debug.WriteLine($"     âš ï¸ Data epilogue error: Expected EB, got {detectedByte:X2}");
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

        if (drive == null || !drive.MotorOn || drive.IsWriteProtected())
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
        Debug.WriteLine($"DiskIIControllerCard installed in {slot}: Created {_drives.Length} drives");
    }

    /// <inheritdoc />
    public virtual string GetMetadata() => string.Empty;

    /// <inheritdoc />
    public virtual bool ApplyMetadata(string metadata) => true;

    /// <inheritdoc />
    public void Reset()
    {
        // CRITICAL: Cancel any pending motor-off FIRST to prevent stale scheduled shutdowns
        _motorOffScheduledCycle = 0;

        // IMMEDIATE motor shutdown - reset is emergency stop, no delay
        // Must happen before clearing shift register to prevent state corruption
        foreach (var drive in _drives)
        {
            if (drive != null && drive.MotorOn)
            {
#if ControllerDebug
                Debug.WriteLine($"[{_clocking.TotalCycles}] ðŸ”´ RESET: Immediate motor-off on {drive.Name}");
#endif
                drive.MotorOn = false;
            }
        }

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

        // Reset to Drive 1
        _selectedDriveIndex = 0;

        // Update status to reflect reset state
        UpdateMotorOffScheduledStatus(false); // No motor-off scheduled
        UpdatePhaseState(); // All phases off
    }
}
