// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

//------------------------------------------------------------------------------
// VA2M.cs
//
// "Virtual Apple II Machine" - Main emulator orchestrator that coordinates
// the CPU, bus, memory, and timing systems to emulate an Apple IIe computer.
//
// RESPONSIBILITIES:
//
// 1. **Emulator Lifecycle:**
//    - Construction and dependency injection (11 required dependencies)
//    - Resource loading (embedded ROM)
//    - Disposal and cleanup (flash timer, rendering service)
//
// 2. **Execution Control:**
//    - Clock() - Single-cycle execution for simple loops
//    - RunAsync() - Async batched execution for continuous operation
//    - PID-based adaptive throttling to maintain ~1.023 MHz Apple IIe speed
//
// 3. **Reset Handling:**
//    - Reset() - Full system reset  
//
// 4. **External Input Delegation:**
//    - EnqueueKey() - Keyboard input injection (delegates to IKeyboardSetter)
//    - SetPushButton() - Pushbutton state (delegates to IGameControllerStatus)
//    - Cross-thread command queueing with instruction boundary respect
//
// 5. **State Publishing:**
//    - Emulator state snapshots (PC, SP, cycles, BASIC line)
//    - Performance metrics (effective MHz, accuracy, error PPM)
//    - Frame rendering coordination (threaded, non-blocking)
//
// 6. **Timing & Synchronization:**
//    - PID-based adaptive throttling (Kp=0.8, Ki=0.15, Kd=0.02)
//    - Flash timer (~2.1 Hz for cursor/mode indicators)
//    - VBlank event handling for frame rendering (~60 Hz)
//    - Performance reporting every 5 seconds
//
// DESIGN PATTERN: FaÃ§ade + Coordinator
// VA2M acts as a faÃ§ade over the emulator subsystems (Bus, Memory, CPU) and
// coordinates their interactions. Input handling is delegated to specialized
// subsystems (SingularKeyHandler for keyboard, SimpleGameController for buttons),
// ensuring single source of truth and event-driven state updates.
//
// THREADING MODEL:
// - **Emulator Thread:** Runs Clock() or RunAsync() loop
// - **External Threads:** Enqueue commands via EnqueueKey(), SetPushButton(), etc.
// - **Flash Timer Thread:** Toggles flash state at ~2.1 Hz
// - **Render Thread:** Processes video memory snapshots asynchronously
// - **Synchronization:** Commands processed at instruction boundaries (6502 atomicity)
//
// THROTTLING MECHANISM:
// PID-based adaptive throttling for accurate timing:
// 1. Proportional (Kp=0.8) - Corrects current timing error
// 2. Integral (Ki=0.15) - Corrects accumulated drift over time
// 3. Derivative (Kd=0.02) - Anticipates error trends
// 4. Sleep for whole milliseconds (OS scheduler, efficient)
// 5. Adaptive SpinWait for sub-millisecond precision (5-200 iterations)
// Achieves ~1.023 MHz within 0.05% error (~500 PPM).
//------------------------------------------------------------------------------

using System.Diagnostics;
using System.Collections.Concurrent;
using Pandowdy.EmuCore.Machine;
using Pandowdy.EmuCore.Input;
using Pandowdy.EmuCore.IO;
using Pandowdy.EmuCore.Memory;
using Pandowdy.EmuCore.Slots;
using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.Video;

using Pandowdy.EmuCore.DiskII;
namespace Pandowdy.EmuCore.Machine;

/// <summary>
/// Main Apple IIe emulator orchestrator coordinating CPU, bus, memory, and timing.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Name Origin:</strong> "VA2M" = "Virtual Apple II Machine" - the machine
/// orchestrator that brings together all emulator subsystems.
/// </para>
/// <para>
/// <strong>Dependencies (15):</strong>
/// <list type="number">
/// <item><strong>IEmulatorState:</strong> State snapshot sink for UI display</item>
/// <item><strong>IFrameProvider:</strong> Frame sink for video rendering</item>
/// <item><strong>ISystemStatusMutator:</strong> System status provider (soft switches)</item>
/// <item><strong>IAppleIIBus:</strong> System bus (CPU, memory, I/O coordination)</item>
/// <item><strong>AddressSpaceController:</strong> 128KB Apple IIe memory management</item>
/// <item><strong>IFrameGenerator:</strong> Video frame rendering</item>
/// <item><strong>RenderingService:</strong> Threaded rendering with auto frame-skipping</item>
/// <item><strong>VideoMemorySnapshotPool:</strong> Memory-efficient snapshot pooling</item>
/// <item><strong>IKeyboardSetter:</strong> Keyboard input injection (SingularKeyHandler)</item>
/// <item><strong>IGameControllerStatus:</strong> Game controller state (SimpleGameController)</item>
/// <item><strong>IDiskStatusProvider:</strong> Disk drive status observable (singleton)</item>
/// <item><strong>CpuClockingCounters:</strong> CPU cycle counting and VBlank timing</item>
/// <item><strong>IMemoryInspector:</strong> Read-only access to all memory regions</item>
/// <item><strong>ISlots:</strong> Expansion slot manager for card message routing</item>
/// <item><strong>RestartCollection:</strong> Registered restartable components for cold boot</item>
/// </list>
/// </para>
/// <para>
/// <strong>Threading Model:</strong>
/// <list type="bullet">
/// <item><strong>Emulator Thread:</strong> Single-threaded CPU execution (Clock/RunAsync loop)</item>
/// <item><strong>External Threads:</strong> UI/input threads enqueue commands via EnqueueKey(), etc.</item>
/// <item><strong>Flash Timer:</strong> Separate timer thread toggles cursor at ~2.1 Hz</item>
/// <item><strong>Render Thread:</strong> Processes video memory snapshots asynchronously</item>
/// <item><strong>Synchronization:</strong> Commands processed at instruction boundaries (6502 atomicity)</item>
/// </list>
/// </para>
/// <para>
/// <strong>Throttling:</strong> PID-based adaptive control achieves ~1.023 MHz within 0.05% error:
/// <list type="bullet">
/// <item><strong>Proportional (Kp=0.8):</strong> Corrects current timing error</item>
/// <item><strong>Integral (Ki=0.15):</strong> Corrects accumulated drift</item>
/// <item><strong>Derivative (Kd=0.02):</strong> Anticipates error trends</item>
/// <item><strong>Adaptive SpinWait:</strong> Self-tuning (5-200 iterations)</item>
/// <item><strong>Performance Reporting:</strong> Publishes effective MHz to UI every second</item>
/// </list>
/// </para>
/// <para>
/// <strong>Subsystem Delegation:</strong>
/// <list type="bullet">
/// <item><strong>Keyboard:</strong> Delegates to IKeyboardSetter (SingularKeyHandler, 26 tests)</item>
/// <item><strong>Game Controller:</strong> Delegates to IGameControllerStatus (SimpleGameController, 32 tests)</item>
/// <item><strong>Rendering:</strong> Threaded via RenderingService (non-blocking, ~1-3Î¼s capture)</item>
/// <item><strong>State Updates:</strong> Event-driven via SystemStatusProvider (no manual polling)</item>
/// </list>
/// </para>
/// <para>
/// <strong>Execution Modes:</strong>
/// <list type="bullet">
/// <item><strong>Clock():</strong> Single-cycle execution, useful for debugging/stepping</item>
/// <item><strong>RunAsync():</strong> Continuous batched execution, normal operation mode</item>
/// </list>
/// </para>
/// </remarks>
public class VA2M : IDisposable,  IEmulatorCoreInterface
{
    /// <summary>
    /// Gets the memory pool managing the 128KB Apple IIe memory space.
    /// </summary>
    public AddressSpaceController MemoryPool { get; }

    /// <summary>
    /// Gets the system bus coordinating CPU, memory, and I/O access.
    /// </summary>
    public IAppleIIBus Bus { get; }
  
    /// <summary>
    /// Stopwatch for throttling the emulator to match Apple IIe speed.
    /// </summary>
    private readonly Stopwatch _throttleSw = Stopwatch.StartNew();
    
    /// <summary>
    /// Count of CPU cycles executed for throttling calculations.
    /// </summary>
    private long _throttleCycles;
    
    /// <summary>
    /// Accumulated error term for adaptive throttling (integral component).
    /// Tracks cumulative timing drift to correct for systematic bias.
    /// </summary>
    private double _throttleErrorAccumulator;
    
    /// <summary>
    /// Last measured timing error for derivative calculation.
    /// Used to detect rate of change in timing drift.
    /// </summary>
    private double _throttleLastError;
    
    /// <summary>
    /// Adaptive SpinWait iterations based on measured performance.
    /// Dynamically adjusted to compensate for system timing variations.
    /// </summary>
    private int _adaptiveSpinWaitIterations = 100;
    
    /// <summary>
    /// Stopwatch for measuring effective MHz performance.
    /// </summary>
    private readonly Stopwatch _perfSw = Stopwatch.StartNew();
    
    /// <summary>
    /// Cycle count at last performance measurement.
    /// </summary>
    private long _perfLastCycles;
    
    /// <summary>
    /// Last time performance was reported (in ticks).
    /// </summary>
    private long _perfLastReportTicks;
    
    /// <summary>
    /// Declared performance reporting interval (5 seconds). Not currently used by
    /// <see cref="ReportPerformanceMetricsAsNeeded"/>, which compares directly against
    /// <see cref="System.Diagnostics.Stopwatch.Frequency"/> (1 second).
    /// </summary>
    private static readonly TimeSpan PerfReportInterval = TimeSpan.FromSeconds(5);
    
    /// <summary>
    /// Backing field for ThrottleEnabled property.
    /// </summary>
    private bool _throttleEnabled = true;
    
    /// <summary>
    /// Gets or sets whether throttling is enabled to maintain Apple IIe speed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When true, the emulator runs at ~1.023 MHz (Apple IIe speed).
    /// When false, runs as fast as possible (useful for loading programs, debugging).
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> The setter enqueues the state change to execute on
    /// the emulator thread via the command queue. This prevents race conditions where the
    /// UI thread modifying throttle state could cause infinite loops in the SpinWait-based
    /// throttling logic (e.g., resetting _throttleSw while emulator is in a SpinWait loop
    /// waiting for a target time based on old stopwatch values).
    /// </para>
    /// </remarks>
    public bool ThrottleEnabled
    {
        get => Volatile.Read(ref _throttleEnabled);
        set
        {
            // Enqueue the state change to execute on the emulator thread.
            // This prevents race conditions where resetting _throttleSw from the UI thread
            // could cause infinite SpinWait loops in ThrottleOneCycle().
            Enqueue(() => SetThrottleEnabledInternal(value));
        }
    }

    /// <summary>
    /// Internal method to set throttle state. Must be called on emulator thread only.
    /// </summary>
    /// <param name="value">New throttle enabled state.</param>
    private void SetThrottleEnabledInternal(bool value)
    {
        if (_throttleEnabled != value)
        {
            _throttleEnabled = value;

            // Reset throttling state when switching modes to prevent
            // infinite loop from trying to "catch up" to accumulated cycles
            if (value) // Switching TO throttled mode
            {
                _throttleCycles = 0;
                _throttleSw.Restart();
                _throttleErrorAccumulator = 0;
                _throttleLastError = 0;
                _adaptiveSpinWaitIterations = 100;

                // Reset performance tracking to prevent garbage values in next report
                _perfLastCycles = 0;
                _perfLastReportTicks = _perfSw.ElapsedTicks;

                Debug.WriteLine("[VA2M] Throttling ENABLED - Reset timing and performance tracking state");
            }
            else // Switching FROM throttled mode
            {
                // Also reset performance tracking when disabling throttle
                // so the first unthrottled report shows accurate "turbo" speed
                _perfLastCycles = _throttleCycles;
                _perfLastReportTicks = _perfSw.ElapsedTicks;

                Debug.WriteLine("[VA2M] Throttling DISABLED - Reset performance baseline");
            }
        }
    }
    
    /// <summary>
    /// Gets or sets the target CPU frequency in Hz.
    /// </summary>
    /// <remarks>
    /// Default is 1,023,000 Hz (1.023 MHz), the Apple IIe's clock speed.
    /// Can be adjusted for testing or to match different Apple II models.
    /// </remarks>
    public double TargetHz { get; set; } = 1_023_000d;
    
    /// <summary>
    /// Gets the total number of CPU cycles executed since last reset.
    /// </summary>
    public ulong SystemClock => Bus.SystemClockCounter;

    #region IEmulatorCoreInterface Observable Accessors
    
    /// <summary>
    /// Gets the emulator state observable for monitoring CPU status.
    /// </summary>
    /// <value>Observable that publishes CPU state snapshots (PC, SP, cycles, BASIC line).</value>
    /// <remarks>
    /// <para>
    /// <strong>Observable Pattern:</strong> State changes are pushed through reactive streams.
    /// The UI can subscribe to <see cref="IEmulatorState.Stream"/> to receive real-time updates.
    /// </para>
    /// </remarks>
    public IEmulatorState EmulatorState => _stateSink;
    
    /// <summary>
    /// Gets the frame provider observable for receiving rendered video frames.
    /// </summary>
    /// <value>Observable that publishes rendered video frames (560Ã—192 pixels with soft switch state).</value>
    /// <remarks>
    /// <para>
    /// <strong>Implementation:</strong> Returns the injected <see cref="IFrameProvider"/> instance
    /// (FrameProvider) that receives frames from the threaded <see cref="RenderingService"/>.
    /// </para>
    /// <para>
    /// <strong>Threading:</strong> Frames are rendered on a separate thread and published at ~60 Hz.
    /// The UI subscribes to <see cref="IFrameProvider.FrameAvailable"/> to receive frames asynchronously.
    /// </para>
    /// </remarks>
    public IFrameProvider FrameProvider => _frameSink;
    
    /// <summary>
    /// Gets the system status observable for monitoring soft switches and system state.
    /// </summary>
    /// <value>Observable that publishes system status snapshots (all 20+ soft switches and I/O states).</value>
    /// <remarks>
    /// <para>
    /// <strong>Implementation:</strong> Returns the injected <see cref="ISystemStatusMutator"/> as
    /// read-only <see cref="ISystemStatusProvider"/> interface. VA2M uses the mutator interface
    /// internally (e.g., SetFlashOn) but exposes only read-only access to the UI.
    /// </para>
    /// <para>
    /// <strong>Event-Driven:</strong> System status changes (soft switches, button states) trigger
    /// the <see cref="ISystemStatusProvider.Changed"/> event automatically via the game controller
    /// and soft switch subsystems.
    /// </para>
    /// </remarks>
    public ISystemStatusProvider SystemStatus => _sysStatusSink;

    /// <summary>
    /// Gets the disk status provider for monitoring disk drive states.
    /// </summary>
    /// <value>Read-only stream of disk drive status snapshots.</value>
    /// <remarks>
    /// <para>
    /// <strong>Implementation:</strong> Returns the injected <see cref="IDiskStatusProvider"/> singleton.
    /// Disk drives and controllers update status through the mutator interface, while the UI subscribes
    /// to the provider for status updates.
    /// </para>
    /// </remarks>
    public IDiskStatusProvider DiskStatus => _diskStatusProvider;

    #endregion

    #region IEmulatorCoreInterface Direct State Inspection

    /// <summary>
    /// Gets comprehensive read-only access to all memory regions for display and debugging.
    /// </summary>
    /// <value>Access to main/aux RAM, system ROM, slot ROM, and active memory mapping.</value>
    /// <remarks>
    /// Thread-safe for reads (atomic byte access). Provides RAM, ROM, and current mapping inspection.
    /// </remarks>
    public IMemoryInspector MemoryInspector => _memoryInspector;

    /// <summary>
    /// Gets a snapshot of the current CPU state for display and debugging.
    /// </summary>
    /// <value>Immutable snapshot of CPU registers, flags, and execution status.</value>
    /// <remarks>
    /// Thread-safe. Returns readonly struct copy for consistent snapshot.
    /// </remarks>
    public CpuStateSnapshot CpuState
    {
        get
        {
            var state = Bus.Cpu.State;
            return new CpuStateSnapshot
            {
                A = state.A,
                X = state.X,
                Y = state.Y,
                P = state.P,
                SP = state.SP,
                PC = state.PC,
                Status = (CpuExecutionStatus)state.Status,
                CyclesRemaining = state.CyclesRemaining,
                CurrentOpcode = state.CurrentOpcode,
                OpcodeAddress = state.OpcodeAddress
            };
        }
    }

    /// <summary>
    /// Gets total CPU cycles executed since last reset.
    /// </summary>
    public ulong TotalCycles => Bus.SystemClockCounter;

    #endregion

    /// <summary>
    /// Emulator state sink for publishing CPU state snapshots.
    /// </summary>
    private readonly IEmulatorState _stateSink;

    /// <summary>
    /// Frame provider for publishing rendered video frames.
    /// </summary>
    private readonly IFrameProvider _frameSink; 

    /// <summary>
    /// System status sink for publishing and mutating soft switch states.
    /// </summary>
    private readonly ISystemStatusMutator _sysStatusSink;

    /// <summary>
    /// Disk status provider for monitoring disk drive states.
    /// </summary>
    /// <remarks>
    /// Singleton injected via DI. Disk drives register and publish state changes through
    /// the mutator interface. The UI subscribes to the provider for status updates.
    /// </remarks>
    private readonly IDiskStatusProvider _diskStatusProvider;

    /// <summary>
    /// Frame generator for rendering Apple IIe video output.
    /// </summary>
    private readonly IFrameGenerator _frameGenerator;
    
    /// <summary>
    /// Rendering service for threaded frame rendering with automatic frame skipping.
    /// </summary>
    private readonly RenderingService _renderingService;
    
    /// <summary>
    /// Pool for reusing video memory snapshots to avoid GC pressure.
    /// </summary>
    private readonly VideoMemorySnapshotPool _snapshotPool;
    
    /// <summary>
    /// Keyboard setter for injecting key events from UI thread into the emulator.
    /// </summary>
    /// <remarks>
    /// This is the same instance that SystemIoHandler uses (as IKeyboardReader) to read
    /// keyboard state, ensuring single source of truth for keyboard emulation. VA2M uses
    /// the setter interface to enqueue keys from the UI thread via thread-safe command queue.
    /// </remarks>
    private readonly IKeyboardSetter _keyboardSetter;

    /// <summary>
    /// CPU clocking counters for VBlank timing and cycle tracking.
    /// </summary>
    /// <remarks>
    /// Provides access to VBlankOccurred event for frame rendering synchronization.
    /// This is the single source of truth for VBlank timing in the emulator.
    /// </remarks>
    private readonly CpuClockingCounters _clockCounters;

    /// <summary>
    /// Collection of all registered <see cref="IRestartable"/> components, iterated during cold boot.
    /// </summary>
    private readonly RestartCollection _restarters;

    /// <summary>
    /// Frame counter for snapshot debugging and diagnostics.
    /// </summary>
    private ulong _frameCounter = 0;

    /// <summary>
    /// Flash timer that toggles StateFlashOn at ~2.1 Hz (Apple IIe cursor blink rate).
    /// </summary>
    private Timer? _flashTimer;

    /// <summary>
    /// Flash period matching Apple IIe cursor blink rate (~2.1 Hz = 476ms period).
    /// </summary>
    private static readonly TimeSpan FlashPeriod = TimeSpan.FromMilliseconds(1000/2.1);

    /// <summary>
    /// Interlocked flag set by flash timer, consumed at VBlank to toggle flash state.
    /// </summary>
    /// <remarks>
    /// Using interlocked exchange ensures thread-safe communication between
    /// the flash timer thread and the emulator thread. Toggle is applied at
    /// frame boundaries to prevent flicker.
    /// </remarks>
    private int _pendingFlashToggle; // 0/1 flag set by timer, consumed on VBlank

    /// <summary>
    /// Maximum frame rendering rate when running unthrottled (FPS cap).
    /// </summary>
    /// <remarks>
    /// When running unthrottled, the emulator can easily exceed 700+ FPS, but rendering
    /// that many frames is wasteful since displays typically run at 60 Hz. This cap
    /// prevents excessive snapshot captures and rendering work while still providing
    /// smooth display at high speeds.
    /// </remarks>
    private const double MaxUnthrottledFps = 61.0;

    /// <summary>
    /// Stopwatch for tracking VBlank call frequency to limit rendering FPS.
    /// </summary>
    private readonly Stopwatch _vblankSw = Stopwatch.StartNew();

    /// <summary>
    /// Last VBlank timestamp (ticks) for FPS calculation.
    /// </summary>
    private long _lastVBlankTicks = 0;

    /// <summary>
    /// Minimum ticks between VBlank renders (based on MaxUnthrottledFps).
    /// </summary>
    private long _minVBlankTicks = 0;

    /// <summary>
    /// Memory inspector for comprehensive read-only access to all memory regions.
    /// </summary>
    private readonly IMemoryInspector _memoryInspector;

    /// <summary>
    /// Slots manager for accessing expansion cards by slot number.
    /// </summary>
    private readonly ISlots _slots;

    /// <summary>
    /// Game controller for pushbutton and paddle state, shared with <see cref="SystemIoHandler"/>.
    /// </summary>
    private IGameControllerStatus _gameController;

    /// <summary>
    /// Initializes a new instance of the VA2M emulator.
    /// </summary>
    /// <param name="stateSink">State provider for publishing emulator state snapshots.</param>
    /// <param name="frameSink">Frame provider for publishing rendered video frames.</param>
    /// <param name="statusProvider">System status mutator for soft switch states (read and write access).</param>
    /// <param name="bus">System bus coordinating CPU, memory, and I/O.</param>
    /// <param name="memoryPool">Address space controller managing 128KB Apple IIe memory.</param>
    /// <param name="frameGenerator">Frame generator for rendering video output.</param>
    /// <param name="renderingService">Rendering service for threaded frame rendering with automatic frame skipping.</param>
    /// <param name="snapshotPool">Pool for reusing video memory snapshots to reduce GC pressure.</param>
    /// <param name="keyboardSetter">Keyboard setter for injecting key events from UI thread (shared with SystemIoHandler).</param>
    /// <param name="gameController">Game controller for pushbutton and paddle state (fires events to SystemStatusProvider).</param>
    /// <param name="diskStatusProvider">Disk status provider for monitoring disk drive states (singleton, shared with UI).</param>
    /// <param name="clockCounters">CPU clocking counters for VBlank timing and cycle tracking.</param>
    /// <param name="memoryInspector">Memory inspector for read-only access to all memory regions (RAM, ROM, slots).</param>
    /// <param name="slots">Slots manager for accessing expansion cards by slot number.</param>
    /// <param name="restarters">Collection of restartable components iterated during cold boot.</param>
    /// <exception cref="ArgumentNullException">Thrown if any parameter is null.</exception>
    /// <remarks>
    /// <para>
    /// <strong>Initialization Sequence:</strong>
    /// <list type="number">
    /// <item>Store dependency references (all 15 required)</item>
    /// <item>Load embedded Apple IIe ROM from resources</item>
    /// <item>Subscribe to VBlank event via <paramref name="clockCounters"/>.<see cref="CpuClockingCounters.VBlankOccurred"/></item>
    /// <item>Start flash timer for cursor blinking (~2.1 Hz)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Subsystem Integration:</strong>
    /// <list type="bullet">
    /// <item><strong>Keyboard:</strong> The injected <paramref name="keyboardSetter"/> (SingularKeyHandler)
    /// is shared with SystemIoHandler, ensuring single source of truth. VA2M uses the setter interface
    /// to enqueue keys from UI thread via thread-safe command queue.</item>
    /// <item><strong>Game Controller:</strong> The injected <paramref name="gameController"/> (SimpleGameController)
    /// fires ButtonChanged and PaddleChanged events that SystemStatusProvider observes directly.
    /// VA2M delegates SetPushButton() calls to this controller.</item>
    /// <item><strong>Rendering:</strong> The <paramref name="renderingService"/> runs on a separate thread,
    /// processing snapshots allocated from <paramref name="snapshotPool"/>. Capture is non-blocking (~1-3μs).</item>
    /// <item><strong>Slots:</strong> The injected <paramref name="slots"/> (Slots) provides access to
    /// expansion cards for message routing via SendCardMessageAsync.</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>ROM Loading:</strong> The Apple IIe Enhanced ROM (16KB) is embedded
    /// as a resource and automatically loaded during construction. This ROM contains
    /// the Monitor, Applesoft BASIC, and peripheral firmware.
    /// </para>
    /// <para>
    /// <strong>VBlank Event:</strong> Subscribes to <see cref="CpuClockingCounters.VBlankOccurred"/>
    /// event directly to trigger frame rendering and flash state updates at ~60 Hz.
    /// </para>
    /// </remarks>
    public VA2M(
        IEmulatorState stateSink, 
        IFrameProvider frameSink, 
        ISystemStatusMutator statusProvider, 
        IAppleIIBus bus, 
        AddressSpaceController memoryPool, 
        IFrameGenerator frameGenerator,
        RenderingService renderingService,
        VideoMemorySnapshotPool snapshotPool,
        IKeyboardSetter keyboardSetter,
        IGameControllerStatus gameController,
        IDiskStatusProvider diskStatusProvider,
        CpuClockingCounters clockCounters,
        IMemoryInspector memoryInspector,
        ISlots slots,
        RestartCollection restarters)
    {
        ArgumentNullException.ThrowIfNull(stateSink);
        ArgumentNullException.ThrowIfNull(frameSink);
        ArgumentNullException.ThrowIfNull(statusProvider);
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(memoryPool);
        ArgumentNullException.ThrowIfNull(frameGenerator);
        ArgumentNullException.ThrowIfNull(renderingService);
        ArgumentNullException.ThrowIfNull(snapshotPool);
        ArgumentNullException.ThrowIfNull(keyboardSetter);
        ArgumentNullException.ThrowIfNull(gameController);
        ArgumentNullException.ThrowIfNull(diskStatusProvider);
        ArgumentNullException.ThrowIfNull(clockCounters);
        ArgumentNullException.ThrowIfNull(memoryInspector);
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(restarters);


        _stateSink = stateSink;
        _frameSink = frameSink;
        _sysStatusSink = statusProvider;
        _diskStatusProvider = diskStatusProvider;
        _frameGenerator = frameGenerator;
        _renderingService = renderingService;
        _snapshotPool = snapshotPool;
        _keyboardSetter = keyboardSetter;
        _gameController = gameController;
        _clockCounters = clockCounters;
        _memoryInspector = memoryInspector;
        _slots = slots;
        _restarters = restarters;
        Bus = bus;
        MemoryPool = memoryPool;

        // Calculate minimum ticks between VBlank renders for FPS cap
        _minVBlankTicks = (long)(Stopwatch.Frequency / MaxUnthrottledFps);
        _lastVBlankTicks = _vblankSw.ElapsedTicks;

        // Subscribe to VBlank for frame rendering
        _clockCounters.VBlankOccurred += OnVBlank;

        // Start flash timer
        _flashTimer = new Timer(_ =>
        {
            try
            {
                Interlocked.Exchange(ref _pendingFlashToggle, 1);
            }
            catch { }
        }, null, FlashPeriod, FlashPeriod);
    }

    
    /// <summary>
    /// Thread-safe queue for cross-thread command execution on the emulator thread.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Purpose:</strong> Allows external threads (UI, input handlers) to safely
    /// interact with the single-threaded emulator core. Commands are enqueued and executed
    /// at the next frame boundary.
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> ConcurrentQueue provides lock-free thread-safe
    /// enqueueing. Commands are dequeued and executed only on the emulator thread.
    /// </para>
    /// <para>
    /// <strong>Example:</strong>
    /// <code>
    /// // From UI thread:
    /// va2m.InjectKey(0x41);  // Enqueues keyboard 'A'
    /// 
    /// // On emulator thread:
    /// ProcessPending();  // Dequeues and executes InjectKey command
    /// </code>
    /// </para>
    /// </remarks>
    private readonly ConcurrentQueue<Action> _pending = new();

    /// <summary>
    /// Counter tracking how many times ProcessAnyPendingActions() has been called
    /// while waiting for CyclesRemaining to reach 0. Used to detect and log if pending
    /// commands are delayed for extended periods. Reset to 0 at each instruction boundary.
    /// </summary>
    private int _pendingWaitCounter = 0;

    /// <summary>
    /// Enqueues an action to be executed on the emulator thread at the next opportunity.
    /// </summary>
    /// <param name="action">Action to execute. Null actions are ignored.</param>
    /// <remarks>
    /// Thread-safe. Can be called from any thread. Action will be executed during
    /// the next ProcessPending() call on the emulator thread.
    /// </remarks>
    private void Enqueue(Action action)
    {
        if (action != null)
        {
            _pending.Enqueue(action);
        }
    }

    /// <summary>
    /// Processes all pending actions enqueued from external threads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>When Called:</strong>
    /// <list type="bullet">
    /// <item>Before each CPU clock cycle in RunAsync()</item>
    /// <item>Before each Clock() call</item>
    /// <item>At frame boundaries (VBlank)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Instruction Atomicity:</strong> On the 6502, instruction execution is guaranteed
    /// to be atomic - no interrupts or external events can occur in the middle of an instruction.
    /// This method respects this guarantee by checking <c>Bus.Cpu.State.CyclesRemaining</c>
    /// before processing pending actions. If an instruction is in progress (CyclesRemaining > 0),
    /// pending actions are ALWAYS deferred until the instruction completes - they are never
    /// processed mid-instruction.
    /// </para>
    /// <para>
    /// <strong>Why This Matters:</strong> Without this check, a reset or interrupt command
    /// enqueued from the UI thread could execute while the CPU is mid-instruction, leaving
    /// registers in an undefined state and causing unpredictable behavior. The 6502 hardware
    /// ensures this never happens, and our emulation must preserve this guarantee.
    /// </para>
    /// <para>
    /// <strong>Delay Tracking:</strong> The method tracks how many consecutive calls occurred
    /// while waiting for CyclesRemaining to reach 0 (instruction boundary). If the delay exceeds
    /// 10,000 cycles (~10ms at 1MHz), a diagnostic message is logged. This helps identify if
    /// pending commands are being delayed excessively, but they will still only be processed
    /// at the next instruction boundary to maintain atomicity.
    /// </para>
    /// <para>
    /// <strong>Exception Handling:</strong> Exceptions in actions are caught and logged
    /// to prevent one bad command from crashing the emulator.
    /// </para>
    /// </remarks>
    private void ProcessAnyPendingActions()
    {
        // Always wait for instruction boundary to maintain 6502 atomicity.
        // NEVER process pending commands mid-instruction, even for critical operations
        // like Reset(). The hardware reset signal would also wait for the current
        // instruction to complete before taking effect.
        if (Bus.Cpu != null && Bus.Cpu.State.CyclesRemaining > 0)
        {
            // Track how long we've been waiting for an instruction boundary
            if (!_pending.IsEmpty)
            {
                _pendingWaitCounter++;

                // Log diagnostic if we've been waiting a long time (one-time log at 10,000 cycles)
                // This indicates the queue has items but we're waiting for instruction completion
                if (_pendingWaitCounter == 10_000)
                {
                    Debug.WriteLine($"[VA2M] Pending actions delayed for {_pendingWaitCounter} cycles, waiting for instruction boundary (CyclesRemaining={Bus.Cpu.State.CyclesRemaining})");
                }
            }
            return; // Always return - never process mid-instruction
        }

        // At instruction boundary (CyclesRemaining == 0) - safe to process pending actions

        // Log if processing was delayed significantly
        if (_pendingWaitCounter > 5_000)
        {
            Debug.WriteLine($"[VA2M] Processing pending actions after {_pendingWaitCounter} cycle delay at instruction boundary");
        }

        // Reset wait counter
        _pendingWaitCounter = 0;

        // Process all pending actions
        while (_pending.TryDequeue(out var act))
        {
            try 
            { 
                act(); 
            } 
            catch (Exception ex)
            { 
                Debug.WriteLine($"[VA2M] Exception during ProcessPending(): {ex.Message}"); 
            }
        }
    }

    /// <summary>
    /// Handles VBlank event, triggering frame rendering and flash toggle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>VBlank Timing:</strong> Fires every 17,030 cycles (~60 Hz at 1.023 MHz), matching the
    /// Apple IIe's vertical blanking interval. During VBlank, the video scanner is not
    /// drawing visible scanlines.
    /// </para>
    /// <para>
    /// <strong>FPS Limiting (Unthrottled Mode):</strong> When running unthrottled, the emulator can
    /// exceed 700+ FPS, but rendering that many frames is wasteful. This method limits rendering to
    /// ~100 FPS by checking elapsed time since last render and exiting early if called too frequently.
    /// Flash toggle still occurs at full rate to maintain cursor blink accuracy.
    /// </para>
    /// <para>
    /// <strong>Operations Performed:</strong>
    /// <list type="number">
    /// <item>Toggle flash state if timer has set the flag (cursor blinking)</item>
    /// <item>Check elapsed time since last render (FPS limiting)</item>
    /// <item>Exit early if FPS > 100 in unthrottled mode</item>
    /// <item>Capture memory snapshot (~1-3 microseconds)</item>
    /// <item>Attempt to enqueue snapshot for rendering (non-blocking)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Frame Skipping:</strong> If the rendering thread is still busy with the
    /// previous frame, the new snapshot is returned to the pool and the frame is skipped.
    /// This prevents blocking the emulator thread and allows it to run at full speed.
    /// </para>
    /// <para>
    /// <strong>Thread Context:</strong> Called on the emulator thread via 
    /// <see cref="CpuClockingCounters.VBlankOccurred"/> event. Never blocks - snapshot capture is ~1-3 Î¼s.
    /// </para>
    /// </remarks>
    private void OnVBlank()
    {


        // When running unthrottled, limit rendering to ~100 FPS to avoid wasteful work
        // (emulator can easily hit 700+ FPS unthrottled, but rendering that fast is pointless)
        if (!ThrottleEnabled)
        {
            long currentTicks = _vblankSw.ElapsedTicks;
            long ticksSinceLastRender = currentTicks - _lastVBlankTicks;

            // Exit early if not enough time has passed (rendering too fast)
            if (ticksSinceLastRender < _minVBlankTicks)
            {
                return; // Skip this frame - rendering faster than 100 FPS
            }

            // Update last render time for next check
            _lastVBlankTicks = currentTicks;
        }
        // Note: In throttled mode, VBlank fires at ~60 Hz naturally, no limiting needed
        // Always apply pending flash toggle at frame boundary for consistent rendering
        if (Interlocked.Exchange(ref _pendingFlashToggle, 0) != 0)
        {
            _sysStatusSink.SetFlashOn(!_sysStatusSink.StateFlashOn);
        }
        // Capture video memory snapshot (fast - ~1-3 microseconds)
        var snapshot = CaptureVideoMemorySnapshot();

        // Try to enqueue for rendering (non-blocking check)
        bool _ = _renderingService.TryEnqueueSnapshot(snapshot);

        // If frame was skipped, that's fine - rendering couldn't keep up
        // Emulator continues at full speed, display shows last completed frame
    }
    
    /// <summary>
    /// Captures a snapshot of video memory and soft switch states for threaded rendering.
    /// </summary>
    /// <returns>VideoMemorySnapshot containing full RAM banks and system status.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Performance:</strong> Uses efficient bulk copy operations via Span&lt;byte&gt;
    /// to copy 96KB of RAM (48KB main + 48KB aux) in ~2-4 microseconds (at 25-50 GB/s memory
    /// bandwidth on modern CPUs). This is negligible compared to the 16.67ms frame budget.
    /// </para>
    /// <para>
    /// <strong>Simplified Strategy:</strong> Instead of slicing and copying individual video
    /// memory regions (text pages, hi-res pages), we copy the entire 48KB RAM banks directly
    /// into the snapshot. This eliminates 8 slice operations and reduces capture time by ~50%.
    /// The renderer then indexes directly into these arrays for video memory access.
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> Includes memory barrier to ensure all pending CPU writes
    /// to video memory are visible before copying. This prevents race conditions at extreme speeds
    /// (13+ MHz unthrottled) where CPU writes might not be visible to the snapshot yet.
    /// </para>
    /// </remarks>
    private VideoMemorySnapshot CaptureVideoMemorySnapshot()
    {
        var snapshot = _snapshotPool.Rent();
        var systemRam = MemoryPool.SystemRam;
        
        // Memory barrier: Ensure all CPU writes to RAM are visible before snapshot
        // This prevents race conditions at extreme speeds (700+ FPS) where we might
        // capture partially-written memory (manifests as flickering inverse @ signs)
        System.Threading.Thread.MemoryBarrier();
        
        // Direct bulk copy of entire 48KB RAM banks (very fast - single operation per bank!)
        // No slicing needed - renderer will index directly into these arrays
        systemRam.CopyMainMemoryIntoSpan(snapshot.MainRam);
        systemRam.CopyAuxMemoryIntoSpan(snapshot.AuxRam);
        
        // Capture soft switch state
        snapshot.SoftSwitches = _sysStatusSink.Current;
        snapshot.FrameNumber = _frameCounter++;
        
        return snapshot;
    }


    /// <summary>
    /// Advance one system clock (one CPU/bus cycle). Optimized for single-stepping with
    /// maximum responsiveness to pending commands. No throttling or performance tracking -
    /// this is purely for debugging and manual execution control.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Single-Step Optimization:</strong> This method is specifically optimized for
    /// single-stepping scenarios (debugging, testing, manual stepping). It checks the pending
    /// command queue twice per cycle:
    /// <list type="number">
    /// <item><strong>Before cycle:</strong> Always checks pending queue (may defer if mid-instruction)</item>
    /// <item><strong>Execute cycle:</strong> Runs one CPU/bus cycle</item>
    /// <item><strong>After cycle:</strong> Checks pending queue again IF at instruction boundary (CyclesRemaining == 0)</item>
    /// </list>
    /// This ensures maximum responsiveness to commands like Reset() when single-stepping,
    /// processing them within 1 cycle of reaching an instruction boundary.
    /// </para>
    /// <para>
    /// <strong>Why Two Checks?</strong> When single-stepping, each Clock() call may complete
    /// part of a multi-cycle instruction. The post-cycle check ensures that as soon as an
    /// instruction completes (CyclesRemaining reaches 0), any pending commands are immediately
    /// processed before the next instruction begins. This provides instant response for debugging.
    /// </para>
    /// <para>
    /// <strong>No Throttling:</strong> This method does NOT apply throttling delays or track
    /// performance metrics. Single-stepping inherently runs at human speed (button clicks),
    /// making throttling meaningless. All throttling and performance tracking is handled by
    /// <see cref="RunAsync"/> instead.
    /// </para>
    /// <para>
    /// <strong>Use Cases:</strong>
    /// <list type="bullet">
    /// <item>Single-step debugging (execute one cycle at a time)</item>
    /// <item>Manual instruction stepping in debugger</item>
    /// <item>Testing and verification scenarios</item>
    /// <item>Any scenario where responsiveness > performance</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>For Normal Emulation:</strong> Use <see cref="RunAsync"/> instead. RunAsync is
    /// optimized for continuous high-speed execution with batching, throttling, and performance
    /// tracking. It maintains accurate Apple IIe timing (~1.023 MHz) while Clock() is purely
    /// for manual stepping without speed constraints.
    /// </para>
    /// </remarks>
    public void Clock()
    {
        // BEFORE cycle: Check for pending commands (may defer if mid-instruction)
        ProcessAnyPendingActions();

        // Execute one cycle
        Bus.Clock();

        // AFTER cycle: If we just completed an instruction (reached boundary),
        // check pending commands again for maximum single-step responsiveness
        if (Bus.Cpu != null && Bus.Cpu.State.CyclesRemaining == 0)
        {
            ProcessAnyPendingActions();
        }
        ReportPerformanceMetricsAsNeeded();
        // Note: No throttling or cycle counting.
        // Clock() is for single-stepping at human speed, not maintaining accurate timing.
        // Use RunAsync() for continuous operation with throttling and performance tracking.
    }


    /// <summary>
    /// Applies adaptive two-phase throttling delay to maintain target CPU frequency.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Adaptive Throttling Algorithm:</strong>
    /// Uses a PID-inspired control system that adjusts delays based on measured performance:
    /// <list type="number">
    /// <item><strong>Proportional:</strong> Corrects based on current timing error</item>
    /// <item><strong>Integral:</strong> Corrects accumulated drift over time</item>
    /// <item><strong>Derivative:</strong> Anticipates trends in timing error</item>
    /// <item><strong>Sleep Phase:</strong> Thread.Sleep() for whole milliseconds (CPU-efficient)</item>
    /// <item><strong>SpinWait Phase:</strong> Adaptive busy-wait for sub-millisecond precision</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Rationale:</strong> Thread.Sleep() has millisecond granularity but is efficient
    /// (yields CPU to other threads). SpinWait provides sub-millisecond accuracy but uses CPU
    /// cycles. The adaptive algorithm measures actual vs target frequency and adjusts SpinWait
    /// iterations to compensate for system timing variations.
    /// </para>
    /// <para>
    /// <strong>Timing Calculation:</strong>
    /// <code>
    /// expectedTime = cyclesExecuted / TargetHz
    /// actualTime = elapsed
    /// error = expectedTime - actualTime
    /// 
    /// // PID-inspired adjustment
    /// correction = Kp * error + Ki * accumulated_error + Kd * (error - last_error)
    /// adaptedDelay = baseDelay + correction
    /// </code>
    /// </para>
    /// <para>
    /// <strong>Accuracy:</strong> Achieves ~1.023 MHz within 0.05% error under normal
    /// system load by continuously adapting to system timing characteristics. Handles
    /// variations from background processes, power management, and OS scheduler latency.
    /// </para>
    /// <para>
    /// <strong>Performance Impact:</strong> Adaptive adjustment adds ~2-3 floating point
    /// operations per cycle, negligible compared to the timing accuracy benefit.
    /// </para>
    /// </remarks>
    private void ThrottleOneCycle()
    {
        // Expected elapsed time in seconds for executed cycles
        double expectedSec = _throttleCycles / TargetHz;
        double elapsedSec = _throttleSw.Elapsed.TotalSeconds;
        double leadSec = expectedSec - elapsedSec; // >0 means we are ahead (need to wait)
        
        if (leadSec <= 0)
        {
            // We're behind schedule - no throttling needed
            // Update error tracking for adaptive control
            double currentError = leadSec; // Negative error = running too slow
            _throttleErrorAccumulator += currentError * 0.5; // Reduce integral contribution when behind
            
            // Clamp accumulator to prevent windup
            _throttleErrorAccumulator = Math.Clamp(_throttleErrorAccumulator, -0.01, 0.01);
            
            _throttleLastError = currentError;
            return;
        }
        
        // PID-inspired adaptive adjustment with tuned gains for better accuracy
        // Kp (proportional): Respond to current error
        // Ki (integral): Correct accumulated drift
        // Kd (derivative): Anticipate error trend
        const double Kp = 0.8;    // Reduced from 1.0 to prevent overshoot
        const double Ki = 0.15;   // Increased from 0.1 for better drift correction
        const double Kd = 0.02;   // Reduced from 0.05 to reduce noise sensitivity
        
        double error = leadSec;
        double derivative = error - _throttleLastError;
        
        // Calculate adaptive correction
        double correction = (Kp * error) + (Ki * _throttleErrorAccumulator) + (Kd * derivative);
        
        // Apply correction with limits
        double adaptedLeadSec = leadSec + correction;
        adaptedLeadSec = Math.Max(0, Math.Min(adaptedLeadSec, 0.05)); // Max 50ms correction (reduced from 100ms)
        
        // Sleep for the whole milliseconds part
        int sleepMs = (int)(adaptedLeadSec * 1000.0);
        if (sleepMs > 0)
        {
            Thread.Sleep(sleepMs);
        }
        
        // Adaptive SpinWait for sub-millisecond precision
        double targetTime = expectedSec;
        while (_throttleSw.Elapsed.TotalSeconds < targetTime)
        {
            Thread.SpinWait(_adaptiveSpinWaitIterations);
        }
        
        // Measure actual timing error after throttling
        double actualElapsed = _throttleSw.Elapsed.TotalSeconds;
        double actualError = expectedSec - actualElapsed;
        
        // Update error tracking for next cycle
        _throttleErrorAccumulator += error * 0.01; // Small integral accumulation
        _throttleErrorAccumulator = Math.Clamp(_throttleErrorAccumulator, -0.005, 0.005); // Tighter clamp
        _throttleLastError = error;
        
        // Periodically adjust SpinWait iterations based on actual performance
        if (_throttleCycles % 5000 == 0) // Every 5,000 cycles (~5ms) for faster adaptation
        {
            // Adjust based on the actual error we just measured
            if (Math.Abs(actualError) > 0.0000005) // 0.5 microsecond threshold
            {
                if (actualError < 0) // We're behind - spin faster (fewer iterations per wait)
                {
                    _adaptiveSpinWaitIterations = Math.Max(5, _adaptiveSpinWaitIterations - 2);
                }
                else // We're ahead - spin slower (more iterations per wait)
                {
                    _adaptiveSpinWaitIterations = Math.Min(200, _adaptiveSpinWaitIterations + 2);
                }
            }
        }
    }

    /// <summary>
    /// Reset machine and system clock.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Warm Reset (Ctrl+Reset):</strong> Enqueues a warm reset via
    /// <see cref="InternalReset"/> on the emulator thread. This clears the keyboard,
    /// resets the CPU (loading the reset vector), and resets throttle/performance state
    /// — but does <strong>not</strong> clear RAM, soft switches, or card state.
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> Thread-safe. Can be called from any thread.
    /// Enqueues the reset operation for execution on the emulator thread at the next
    /// instruction boundary, respecting 6502 atomic instruction guarantees.
    /// </para>
    /// </remarks>
    public void DoReset()
    {
        Enqueue(() =>
        {
            InternalReset();
        });
    }

    /// <summary>
    /// Performs the core warm-reset sequence on the emulator thread.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Shared by DoReset and DoRestart:</strong> This private method contains the
    /// warm-reset operations common to both <see cref="DoReset"/> (warm reset) and
    /// <see cref="DoRestart"/> (cold boot). <c>DoRestart</c> calls <c>InternalReset()</c>
    /// first, then follows with <see cref="RestartCollection.RestartAll"/> to clear RAM,
    /// soft switches, and card state.
    /// </para>
    /// <para>
    /// <strong>Overlap Note:</strong> Some operations performed here (e.g., keyboard reset)
    /// are also performed by individual <see cref="IRestartable"/> components during
    /// <c>RestartAll()</c>. This duplication is intentional and harmless — <c>InternalReset</c>
    /// ensures the warm-reset baseline is established before the cold-boot pass runs.
    /// </para>
    /// <para>
    /// <strong>Operations Performed:</strong>
    /// <list type="bullet">
    /// <item>Clear keyboard latch and pending keystrokes</item>
    /// <item>Reset bus (CPU reset vector, address space, cycle counters)</item>
    /// <item>Reset throttle cycle counter and stopwatch</item>
    /// <item>Reset performance measurement counters</item>
    /// <item>Reset adaptive throttling state (error accumulator, SpinWait iterations)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Thread Context:</strong> Must be called on the emulator thread only
    /// (always invoked inside an <see cref="Enqueue"/> action).
    /// </para>
    /// </remarks>
    private void InternalReset()
    {
        // Reset keyboard state (clear pending keystrokes and strobe)
        _keyboardSetter.ResetKeyboard();

        Bus.Reset();
        _throttleCycles = 0;
        _throttleSw.Restart();

        // Reset performance tracking
        _perfLastCycles = 0;
        _perfLastReportTicks = _perfSw.ElapsedTicks;

        // Reset adaptive throttling state
        _throttleErrorAccumulator = 0;
        _throttleLastError = 0;
        _adaptiveSpinWaitIterations = 100;
    }

    /// <summary>
    /// Queues a full cold boot (power cycle) that restores every registered
    /// <see cref="IRestartable"/> component to its initial power-on state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Execution Sequence:</strong> First calls <see cref="InternalReset"/> to
    /// establish the warm-reset baseline (keyboard clear, CPU reset vector, throttle state),
    /// then calls <see cref="RestartCollection.RestartAll"/> to iterate all registered
    /// <see cref="IRestartable"/> components in priority order — clearing RAM, resetting
    /// soft switches to power-on defaults, cold-initializing cards, and finally resetting
    /// the CPU (via <see cref="VA2MBus"/> at priority 100).
    /// </para>
    /// <para>
    /// <strong>Overlap:</strong> Some operations in <c>InternalReset()</c> (e.g., keyboard
    /// reset via <see cref="IKeyboardSetter.ResetKeyboard"/>) overlap with individual
    /// <c>IRestartable.Restart()</c> implementations. This is intentional — the warm-reset
    /// baseline is established first, then the cold-boot pass ensures all components reach
    /// their power-on default state regardless of <c>InternalReset</c>'s coverage.
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> Thread-safe. Can be called from any thread.
    /// The restart is enqueued for execution on the emulator thread at the next
    /// instruction boundary, respecting 6502 atomic instruction guarantees.
    /// </para>
    /// <para>
    /// <strong>Flat Collection:</strong> Unlike the hierarchical tree approach, all
    /// restartable components are iterated by the <see cref="RestartCollection"/>.
    /// Components tagged with <c>[Capability(typeof(IRestartable))]</c> are discovered
    /// via DI; factory-generated cards are registered dynamically by <see cref="CardFactory"/>.
    /// </para>
    /// </remarks>
    public void DoRestart()
    {
        Enqueue(() =>
        {
            InternalReset();  
            _restarters.RestartAll();
        });
    }

    /// <summary>
    /// Run the emulator asynchronously with batched cycles - optimized for high-performance
    /// continuous execution. Batches cycles per tick (e.g., 1 ms or 60 Hz) to minimize
    /// per-cycle overhead. This is the primary execution mode for normal emulation.
    /// </summary>
    /// <param name="ct">Cancellation token to stop the runner.</param>
    /// <param name="ticksPerSecond">Time slice frequency. Use 1000 for 1ms slices or 60 for video-frame pacing.</param>
    /// <returns>A task that completes when the cancellation token is triggered or the emulator stops.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="ticksPerSecond"/> is zero or negative.</exception>
    /// <remarks>
    /// <para>
    /// <strong>Batch Operation Optimization:</strong> This method is specifically optimized for
    /// high-speed continuous execution by batching CPU cycles and checking pending commands
    /// periodically (every 100 cycles) rather than every cycle. This reduces overhead by ~50%
    /// compared to per-cycle checking. For single-step debugging, use <see cref="Clock"/> instead.
    /// </para>
    /// <para>
    /// <strong>Pending Command Responsiveness:</strong> Pending commands (Reset, keyboard input, etc.)
    /// are checked every 100 cycles (~0.1ms at 1MHz) which provides excellent responsiveness
    /// (sub-millisecond) while maintaining high performance. Commands are always processed at
    /// instruction boundaries to maintain 6502 atomicity.
    /// </para>
    /// <para>
    /// <strong>Throttled Mode (ThrottleEnabled = true):</strong>
    /// <list type="bullet">
    /// <item>Executes calculated number of cycles per tick (TargetHz / ticksPerSecond)</item>
    /// <item>Accumulates fractional cycles to prevent drift over time</item>
    /// <item>Applies adaptive PID-based throttling to maintain ~1.023 MHz</item>
    /// <item>Processes pending commands every 100 cycles for balance of performance/responsiveness</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Fast Mode (ThrottleEnabled = false):</strong>
    /// <list type="bullet">
    /// <item>Executes 10,000 cycle batches as fast as possible</item>
    /// <item>No timing delays (runs at maximum CPU speed, typically 20-50+ MHz)</item>
    /// <item>Still checks pending commands every 100 cycles for responsiveness</item>
    /// <item>Yields with Task.Delay(0) to remain responsive to cancellation</item>
    /// <item>Useful for fast-forwarding through disk operations or long BASIC programs</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Recommended ticksPerSecond Values:</strong>
    /// <list type="bullet">
    /// <item><strong>1000:</strong> 1ms time slices, ~1,023 cycles/tick (balanced, recommended)</item>
    /// <item><strong>60:</strong> ~16.67ms slices, ~17,050 cycles/tick (matches VBlank for frame-sync)</item>
    /// <item><strong>100:</strong> 10ms slices, ~10,230 cycles/tick (lower frequency, slightly less accurate)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Fractional Cycle Accumulation:</strong> The algorithm tracks fractional
    /// cycles with a carry variable to prevent cumulative drift. For example, with
    /// ticksPerSecond=1000, each tick should execute 1023 cycles, but the fractional
    /// 0.0 carry is accumulated so over time the average is exactly 1,023,000 Hz.
    /// </para>
    /// <para>
    /// <strong>Cancellation:</strong> The loop checks the cancellation token after each
    /// batch and breaks cleanly. OperationCanceledException is caught and handled gracefully.
    /// </para>
    /// <para>
    /// <strong>ConfigureAwait(false):</strong> Used to avoid capturing synchronization
    /// context, improving performance and preventing potential deadlocks in UI scenarios.
    /// </para>
    /// </remarks>
    public async Task RunAsync(CancellationToken ct, double ticksPerSecond = 1000d)
    {
        Thread.CurrentThread.Name = "Apple IIe Emulator";

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ticksPerSecond);
        
        // Calculate cycles per batch for adaptive throttling
        double cyclesPerTick = TargetHz / ticksPerSecond;
        double carry = 0.0;
        
        // Check for pending commands every N cycles to reduce input latency
        const int PendingCheckInterval = 100; // Check every ~0.1ms at 1MHz
        
        while (!ct.IsCancellationRequested)
        {
            if (ThrottleEnabled)
            {
                // Calculate target cycles for this batch
                double want = cyclesPerTick + carry;
                int cycles = (int)want;
                carry = want - cycles;
                
                // Execute the batch of cycles with periodic pending checks
                for (int i = 0; i < cycles; i++)
                {
                    // Check for pending commands periodically to reduce input latency
                    if (i % PendingCheckInterval == 0)
                    {
                        ProcessAnyPendingActions();
                    }
                    
                    Bus.Clock();
                    _throttleCycles++;
                    
                    if (ct.IsCancellationRequested)
                    {
                        break;
                    }
                }
                
                // Final pending check at end of batch
                ProcessAnyPendingActions();
                
                // Apply adaptive throttling AFTER executing the batch
                // This allows the PID controller to adjust delays based on actual execution time
                ThrottleOneCycle();

                ReportPerformanceMetricsAsNeeded();
            }
            else
            {
                const int FastBatch = 10_000;
                
                for (int i = 0; i < FastBatch; i++)
                {
                    // Check for pending commands periodically in fast mode too
                    if (i % PendingCheckInterval == 0)
                    {
                        ProcessAnyPendingActions();
                    }
                    
                    Bus.Clock();
                    _throttleCycles++;
                    
                    if (ct.IsCancellationRequested)
                    {
                        break;
                    }
                }
                
                // Final pending check
                ProcessAnyPendingActions();

                ReportPerformanceMetricsAsNeeded();

                try
                {
                    await Task.Delay(0, ct);
                }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    /// <summary>
    /// Reports effective MHz performance metrics every second.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Metrics Calculated:</strong>
    /// <list type="bullet">
    /// <item><strong>Effective MHz:</strong> Actual CPU cycles executed per second</item>
    /// <item><strong>Target MHz:</strong> Configured target frequency (normally 1.023 MHz)</item>
    /// <item><strong>Accuracy:</strong> Percentage of target frequency achieved (throttled mode)</item>
    /// <item><strong>Error PPM:</strong> Parts-per-million timing error (lower is better)</item>
    /// <item><strong>Throttle State:</strong> Whether throttling is currently enabled</item>
    /// <item><strong>Adaptive Parameters:</strong> Current SpinWait iterations and error accumulator</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Use Cases:</strong>
    /// <list type="bullet">
    /// <item>Monitor throttling accuracy in normal operation</item>
    /// <item>Measure maximum performance in fast mode</item>
    /// <item>Detect performance issues or system load problems</item>
    /// <item>Verify timing calibration and adaptive control effectiveness</item>
    /// <item>Debug adaptive throttling algorithm behavior</item>
    /// </list>
    /// </para>
    /// </remarks>
    private void ReportPerformanceMetricsAsNeeded()
    {
        long currentTicks = _perfSw.ElapsedTicks;
        long ticksSinceLastReport = currentTicks - _perfLastReportTicks;

        // Check if 1 second have elapsed (1000ms, 1 Hz update rate)
        if (ticksSinceLastReport < Stopwatch.Frequency)
        {
            return;
        }
        
        // Calculate effective MHz
        long currentCycles = _throttleCycles;
        long cyclesSinceLastReport = currentCycles - _perfLastCycles;
        double secondsElapsed = (double)ticksSinceLastReport / Stopwatch.Frequency;
        double effectiveMhz = (cyclesSinceLastReport / secondsElapsed) / 1_000_000.0;

        // Calculate accuracy percentage if throttled
#if DebugMhz
        string accuracyInfo;

        if (ThrottleEnabled)
        {
            double targetMhz = TargetHz / 1_000_000.0;
            double accuracyPercent = (effectiveMhz / targetMhz) * 100.0;
            double errorPercent = Math.Abs(100.0 - accuracyPercent);
            double errorPpm = errorPercent * 10000.0; // Parts per million


            accuracyInfo = $" (Target: {targetMhz:F3} MHz, Accuracy: {accuracyPercent:F2}%, Error: {errorPpm:F0} ppm)";
            // Add adaptive throttling diagnostics
            accuracyInfo += $" [SpinWait: {_adaptiveSpinWaitIterations}, ErrorAccum: {_throttleErrorAccumulator:F6}]";

        }

        // Include timestamp and actual interval for debugging timing issues
        Debug.WriteLine(
            $"[VA2M Performance @ {DateTime.Now:HH:mm:ss.fff}] " +
            $"Interval: {secondsElapsed:F2}s | " +
            $"Effective MHz: {effectiveMhz:F3}{accuracyInfo} | " +
            $"Throttle: {(ThrottleEnabled ? "ON" : "OFF")} | " +
            $"Total Cycles: {currentCycles:N0}"
        );
#endif
        _sysStatusSink.SetCurrentMhz(effectiveMhz);

        // Update for next report
        _perfLastReportTicks = currentTicks;
        _perfLastCycles = currentCycles;
    }
    
    /// <summary>
    /// Enqueues a keyboard character to be injected into the emulator as if a key was pressed.
    /// </summary>
    /// <param name="ascii">ASCII character code (0-127). High bit will be set automatically by keyboard handler.</param>
    /// <inheritdoc cref="IKeyboardSetter.EnqueueKey" path="/remarks"/>
    /// <remarks>
    /// <para>
    /// <strong>Architecture:</strong> This method delegates to the injected <see cref="IKeyboardSetter"/>
    /// instance (SingularKeyHandler), which is shared with SystemIoHandler. This ensures single source
    /// of truth for keyboard state - VA2M injects keys (write), SystemIoHandler reads keys (read).
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> This method is thread-safe. It enqueues the key injection
    /// command which will be executed on the emulator thread at the next instruction boundary
    /// (via ProcessAnyPendingActions). This allows UI threads to inject keyboard input without
    /// race conditions while respecting 6502 instruction atomicity.
    /// </para>
    /// <para>
    /// <strong>Renamed from InjectKey:</strong> This method was renamed from InjectKey() to
    /// EnqueueKey() to better reflect that the key is enqueued for later processing, not
    /// immediately injected. The name matches the IKeyboardSetter.EnqueueKey() method it delegates to.
    /// </para>
    /// <para>
    /// <strong>Apple IIe Keyboard Format:</strong> The keyboard handler automatically sets bit 7
    /// (OR with 0x80) to match Apple II keyboard hardware format. The key becomes available at
    /// $C000 with strobe set, and is cleared when software reads $C010 (KBDSTRB).
    /// </para>
    /// </remarks>
    public void EnqueueKey(byte ascii)
    {
        // Enqueue to run on emulator thread - now calls keyboard handler directly
        Enqueue(() => _keyboardSetter.EnqueueKey(ascii));
    }

    /// <summary>
    /// Resets the keyboard state to power-on defaults, clearing pending keystrokes and strobe.
    /// </summary>
    /// <inheritdoc cref="IKeyboardSetter.ResetKeyboard" path="/remarks"/>
    /// <remarks>
    /// <para>
    /// <strong>Architecture:</strong> This method delegates to the injected <see cref="IKeyboardSetter"/>
    /// instance (SingularKeyHandler). Like <see cref="EnqueueKey"/>, this ensures single source of truth
    /// for keyboard state management.
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> This method is thread-safe. It enqueues the keyboard reset
    /// command which will be executed on the emulator thread at the next instruction boundary
    /// (via ProcessAnyPendingActions). This is typically called internally by <see cref="DoReset"/>
    /// during full system reset.
    /// </para>
    /// <para>
    /// <strong>Note:</strong> For full system reset (power cycle), use <see cref="DoReset"/> instead,
    /// which resets the entire emulator including CPU, memory, and all peripherals.
    /// </para>
    /// </remarks>
    public void ResetKeyboard()
    {
        // Enqueue to run on emulator thread - delegates to keyboard handler
        Enqueue(() => _keyboardSetter.ResetKeyboard());
    }

    /// <summary>
    /// Explicit implementation of <see cref="IRestartable.Restart"/> inherited through
    /// <see cref="IKeyboardSetter"/> → <see cref="IEmulatorCoreInterface"/>.
    /// Delegates to <see cref="DoRestart"/> which iterates the <see cref="RestartCollection"/>.
    /// </summary>
    void IRestartable.Restart() => DoRestart();

    /// <summary>
    /// Sets the state of an Apple IIe pushbutton (game controller button).
    /// </summary>
    /// <param name="num">Button number (0-2). Button 0 is typically button 0, buttons 1-2 are paddle buttons.</param>
    /// <param name="pressed">True if button is pressed, false if released.</param>
    /// <remarks>
    /// <para>
    /// <strong>Architecture:</strong> This method delegates to the injected <see cref="IGameControllerStatus"/>
    /// instance (SimpleGameController). The controller fires ButtonChanged events that SystemStatusProvider
    /// observes directly, ensuring automatic synchronization of button state to SystemStatus without
    /// manual polling. This is event-driven architecture - VA2M doesn't manage button state directly.
    /// </para>
    /// <para>
    /// <strong>Apple IIe Pushbutton Hardware:</strong> The Apple IIe has three pushbutton
    /// inputs typically used for game controllers (joysticks/paddles). These are read from
    /// I/O addresses $C061-$C063.
    /// </para>
    /// <para>
    /// <strong>Button Mapping:</strong>
    /// <list type="bullet">
    /// <item><strong>Button 0 ($C061):</strong> Typically joystick/paddle button 0</item>
    /// <item><strong>Button 1 ($C062):</strong> Typically joystick/paddle button 1</item>
    /// <item><strong>Button 2 ($C063):</strong> Typically joystick/paddle button 2</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Reading Buttons:</strong> Software reads the button state by checking bit 7
    /// of the corresponding I/O address. If bit 7 is set, the button is pressed.
    /// </para>
    /// <para>
    /// <strong>Change Detection:</strong> The SimpleGameController implementation only fires
    /// ButtonChanged events when the button state actually changes, preventing event spam.
    /// This ensures efficient updates to SystemStatus and observers.
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> This method is thread-safe. It enqueues the button
    /// state change which will be executed on the emulator thread at the next instruction
    /// boundary (via ProcessAnyPendingActions). This respects 6502 atomic instruction guarantees.
    /// </para>
    /// </remarks>
    public void SetPushButton(byte num, bool pressed)
    {
        Enqueue(() =>  _gameController.SetButton(num,pressed));
    }

    /// <summary>
    /// Sends a message to a specific card or broadcasts to all cards.
    /// </summary>
    /// <param name="slot">Target slot (Slot1–Slot7), or <c>null</c> to broadcast to all slots.</param>
    /// <param name="message">The card message to deliver.</param>
    /// <returns>A task that completes when the message(s) have been processed on the emulator thread.</returns>
    /// <exception cref="Pandowdy.EmuCore.Slots.CardMessageException">
    /// Thrown (on the returned Task) if a targeted card rejects the message. Not thrown for
    /// broadcast messages — individual card failures are silently ignored during broadcast.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <strong>Thread Safety:</strong> Thread-safe. The message is enqueued on the emulator
    /// thread's command queue and executed at the next instruction boundary, identical to
    /// Reset() and EnqueueKey(). The returned Task allows the caller to await completion
    /// and observe any errors.
    /// </para>
    /// <para>
    /// <strong>Broadcast Mode:</strong> When <paramref name="slot"/> is <c>null</c>, the
    /// message is delivered to all 7 slots (Slot1–Slot7). This is useful for discovery
    /// messages like <see cref="Messages.IdentifyCardMessage"/> where all cards should respond.
    /// During broadcast, individual card exceptions are caught and ignored — the broadcast
    /// completes successfully as long as delivery is attempted to all slots.
    /// </para>
    /// <para>
    /// <strong>Responses:</strong> Cards respond via <see cref="ICardResponseProvider"/> —
    /// subscribe to <see cref="ICardResponseProvider.Responses"/> to receive responses.
    /// </para>
    /// <para>
    /// <strong>Generic Design:</strong> This method is intentionally card-type-agnostic.
    /// IEmulatorCoreInterface has no knowledge of disk drives, printers, or any specific
    /// card type. It simply routes the message to the card(s) in the requested slot(s).
    /// </para>
    /// </remarks>
    public System.Threading.Tasks.Task SendCardMessageAsync(SlotNumber? slot, ICardMessage message)
    {
        var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>(
            System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);

        Enqueue(() =>
        {
            try
            {
                if (slot.HasValue)
                {
                    // Targeted: send to specific slot, propagate exceptions
                    var card = _slots.GetCardIn(slot.Value);
                    card.HandleMessage(message);
                }
                else
                {
                    // Broadcast: send to all slots, ignore individual failures
                    foreach (SlotNumber s in Enum.GetValues<SlotNumber>())
                    {
                        if (s == SlotNumber.Unslotted)
                        {
                            continue;
                        }

                        try
                        {
                            var card = _slots.GetCardIn(s);
                            card.HandleMessage(message);
                        }
                        catch (Pandowdy.EmuCore.Slots.CardMessageException)
                        {
                            // Ignore individual card failures during broadcast
                        }
                    }
                }
                tcs.SetResult(true);
            }
            catch (Pandowdy.EmuCore.Slots.CardMessageException ex)
            {
                tcs.SetException(ex);
            }
            catch (Exception ex)
            {
                tcs.SetException(new Pandowdy.EmuCore.Slots.CardMessageException(
                    $"Unexpected error sending message to slot {slot}: {ex.Message}", ex));
            }
        });

        return tcs.Task;
    }



    /// <summary>
    /// Releases all resources used by the VA2M emulator instance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Cleanup Sequence:</strong>
    /// <list type="number">
    /// <item>Stop and dispose flash timer (~2.1 Hz cursor blink timer)</item>
    /// <item>Stop and dispose rendering service (stops render thread, releases snapshots)</item>
    /// <item>Clear pending command queue to release enqueued actions</item>
    /// <item>Dispose bus (includes VBlank event cleanup if VA2MBus)</item>
    /// <item>Suppress finalization (no unmanaged resources)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> Should be called from the thread that owns the emulator
    /// instance (typically the UI thread after stopping emulation). Do not call while RunAsync()
    /// is executing - cancel the CancellationToken and await the task first.
    /// </para>
    /// <para>
    /// <strong>Rendering Service:</strong> Disposing the RenderingService stops the render thread
    /// and waits for any in-progress rendering to complete. Any pooled snapshots are released
    /// back to the VideoMemorySnapshotPool.
    /// </para>
    /// <para>
    /// <strong>AddressSpaceController Note:</strong> MemoryPool (AddressSpaceController) disposal
    /// is currently commented out as it may be shared across multiple emulator instances or have
    /// external ownership. This should be revisited if lifetime semantics are clarified.
    /// </para>
    /// <para>
    /// <strong>CPU Note:</strong> The legacy 6502.NET CPU library does not implement IDisposable,
    /// so no explicit CPU cleanup is needed. The CPU instance is managed by the Bus.
    /// </para>
    /// <para>
    /// <strong>Subsystems:</strong> The keyboard (IKeyboardSetter) and game controller
    /// (IGameControllerStatus) are not disposed here as they may be shared or have external
    /// lifetime management. If they implement IDisposable, they should be disposed by their owner.
    /// </para>
    /// </remarks>
    public void Dispose()
    {
        // Dispose flash timer
        _flashTimer?.Dispose();
        _flashTimer = null;
        
        // Dispose rendering service (stops render thread)
        _renderingService?.Dispose();
        
        // Clear pending queue
        while (_pending.TryDequeue(out _)) { }
        
        // Dispose bus (which handles VBlank event cleanup)
        if (Bus is IDisposable disposableBus)
        {
            disposableBus.Dispose();
        }
        
        // Dispose memory pool
      //  MemoryPool?.Dispose();
        
        // Note: _cpu doesn't implement IDisposable in legacy 6502.NET library
        
        // Suppress finalization as per CA1816
        GC.SuppressFinalize(this);
    }



}
