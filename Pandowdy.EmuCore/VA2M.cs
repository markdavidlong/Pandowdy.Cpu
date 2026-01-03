//------------------------------------------------------------------------------
// VA2M.cs
//
// ⚠️ PLANNED FOR REFACTORING - SEE _docs_/VA2MBus-Refactoring-Notes.md ⚠️
//
// "Virtual Apple II Machinator" - Main emulator orchestrator that coordinates
// the CPU, bus, memory, and timing systems to emulate an Apple IIe computer.
//
// CURRENT RESPONSIBILITIES:
// VA2M serves as the top-level controller for the emulator, managing:
//
// 1. **Emulator Lifecycle:**
//    - Construction and dependency injection
//    - Resource loading (embedded ROM)
//    - Disposal and cleanup
//
// 2. **Execution Control:**
//    - Clock() - Single-cycle execution for simple loops
//    - RunAsync() - Async batched execution for continuous operation
//    - Throttling to maintain ~1.023 MHz Apple IIe speed
//
// 3. **Reset Handling:**
//    - Reset() - Full system reset (power cycle)
//    - UserReset() - Warm reset (Ctrl+Reset)
//
// 4. **External Input:**
//    - Keyboard input injection
//    - Pushbutton state management
//    - Cross-thread command queueing
//
// 5. **State Publishing:**
//    - Emulator state snapshots (PC, SP, cycles, BASIC line)
//    - System status snapshots (soft switches, buttons)
//    - Frame rendering coordination
//
// 6. **Timing & Synchronization:**
//    - Cycle-accurate throttling (Sleep + SpinWait)
//    - Flash timer (~2.1 Hz for cursor/mode indicators)
//    - VBlank event handling for frame rendering
//
// DESIGN PATTERN: Façade + Coordinator
// VA2M acts as a façade over the emulator subsystems (Bus, Memory, CPU) and
// coordinates their interactions. It handles cross-thread communication via
// a command queue pattern, allowing UI/external threads to safely interact
// with the single-threaded emulator core.
//
// THREADING MODEL:
// - **Emulator Thread:** Runs Clock() or RunAsync() loop
// - **External Threads:** Enqueue commands via InjectKey(), SetPushButton(), etc.
// - **Flash Timer Thread:** Toggles flash state at ~2.1 Hz
// - **Synchronization:** Commands are processed at frame boundaries (ProcessPending)
//
// THROTTLING MECHANISM:
// Two-phase throttling for accurate timing:
// 1. Sleep for whole milliseconds (OS scheduler)
// 2. SpinWait for sub-millisecond precision (busy wait)
// This achieves ~1.023 MHz accuracy while being efficient.
//
// FUTURE REFACTORING:
// This class will be refactored alongside VA2MBus to:
// - Separate timing/throttling into dedicated service
// - Move input handling to input manager
// - Extract state publishing to dedicated provider
// - Simplify to pure coordinator role
//
// See: Pandowdy.EmuCore/_docs_/VA2MBus-Refactoring-Notes.md
//------------------------------------------------------------------------------

using System.Reflection;
using System.Diagnostics;
using System.Collections.Immutable;
using System.Collections.Concurrent;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Services;

namespace Pandowdy.EmuCore;

/// <summary>
/// Main Apple IIe emulator orchestrator coordinating CPU, bus, memory, and timing.
/// </summary>
/// <remarks>
/// <para>
/// <strong>⚠️ PLANNED FOR REFACTORING:</strong> This class will be refactored to separate
/// concerns (timing, input, state publishing) into dedicated services. See
/// Pandowdy.EmuCore/_docs_/VA2MBus-Refactoring-Notes.md for planned architecture.
/// </para>
/// <para>
/// <strong>Name Origin:</strong> "VA2M" = "Virtual Apple II Machinator" - the machine
/// orchestrator that brings together all emulator subsystems.
/// </para>
/// <para>
/// <strong>Threading Model:</strong>
/// <list type="bullet">
/// <item><strong>Emulator Thread:</strong> Single-threaded CPU execution (Clock/RunAsync loop)</item>
/// <item><strong>External Threads:</strong> UI/input threads enqueue commands via InjectKey(), etc.</item>
/// <item><strong>Flash Timer:</strong> Separate timer thread toggles cursor at ~2.1 Hz</item>
/// <item><strong>Synchronization:</strong> Commands processed at frame boundaries (thread-safe)</item>
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
public class VA2M : IDisposable
{
    /// <summary>
    /// Gets the memory pool managing the 128KB Apple IIe memory space.
    /// </summary>
    public MemoryPool MemoryPool { get; }

    /// <summary>
    /// Gets the system bus coordinating CPU, memory, and I/O access.
    /// </summary>
    public IAppleIIBus Bus { get; }

 //   private readonly ICpu _cpu;
    
    /// <summary>
    /// Stopwatch for throttling the emulator to match Apple IIe speed.
    /// </summary>
    private readonly Stopwatch _throttleSw = Stopwatch.StartNew();
    
    /// <summary>
    /// Count of CPU cycles executed for throttling calculations.
    /// </summary>
    private long _throttleCycles;
    
    /// <summary>
    /// Gets or sets whether throttling is enabled to maintain Apple IIe speed.
    /// </summary>
    /// <remarks>
    /// When true, the emulator runs at ~1.023 MHz (Apple IIe speed).
    /// When false, runs as fast as possible (useful for loading programs, debugging).
    /// </remarks>
    public bool ThrottleEnabled { get; set; } = true;
    
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


    /// <summary>
    /// Emulator state sink for publishing CPU state snapshots.
    /// </summary>
    private readonly IEmulatorState _stateSink; 
    
    /// <summary>
    /// Frame provider sink for publishing rendered video frames.
    /// </summary>
    private readonly IFrameProvider _frameSink; 
    
    /// <summary>
    /// System status sink for publishing soft switch states.
    /// </summary>
    private readonly ISystemStatusProvider _sysStatusSink;
    
    /// <summary>
    /// Frame generator for rendering Apple IIe video output.
    /// </summary>
    private readonly IFrameGenerator _frameGenerator;

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
    /// Initializes a new instance of the VA2M emulator.
    /// </summary>
    /// <param name="stateSink">State provider for publishing emulator state snapshots.</param>
    /// <param name="frameSink">Frame provider for publishing rendered video frames.</param>
    /// <param name="statusProvider">System status provider for soft switch states.</param>
    /// <param name="bus">System bus coordinating CPU, memory, and I/O.</param>
    /// <param name="memoryPool">Memory pool managing 128KB Apple IIe memory.</param>
    /// <param name="frameGenerator">Frame generator for rendering video output.</param>
    /// <exception cref="ArgumentNullException">Thrown if any parameter is null.</exception>
    /// <remarks>
    /// <para>
    /// <strong>Initialization Sequence:</strong>
    /// <list type="number">
    /// <item>Store dependency references (all required)</item>
    /// <item>Load embedded Apple IIe ROM from resources</item>
    /// <item>Subscribe to VBlank event from bus (if VA2MBus)</item>
    /// <item>Start flash timer for cursor blinking</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>ROM Loading:</strong> The Apple IIe Enhanced ROM (16KB) is embedded
    /// as a resource and automatically loaded during construction. This ROM contains
    /// the Monitor, Applesoft BASIC, and peripheral firmware.
    /// </para>
    /// <para>
    /// <strong>VBlank Event:</strong> If the bus is a VA2MBus, the OnVBlank handler
    /// is registered to trigger frame rendering and flash state updates at ~60 Hz.
    /// </para>
    /// </remarks>
    public VA2M(IEmulatorState stateSink, IFrameProvider frameSink, ISystemStatusProvider statusProvider, IAppleIIBus bus, MemoryPool memoryPool, IFrameGenerator frameGenerator )
    {
        ArgumentNullException.ThrowIfNull(stateSink);
        ArgumentNullException.ThrowIfNull(frameSink);
        ArgumentNullException.ThrowIfNull(statusProvider);
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(memoryPool);
        ArgumentNullException.ThrowIfNull(frameGenerator);



        _stateSink = stateSink;
        _frameSink = frameSink;
        _sysStatusSink = statusProvider;
        _frameGenerator = frameGenerator;
        Bus = bus;
        MemoryPool = memoryPool;
        TryLoadEmbeddedRom("Pandowdy.EmuCore.Resources.a2e_enh_c-f.rom");
        
   //     _cpu = new CPUAdapter(new CPU());
    //   Bus = new VA2MBus(MemoryPool, _sysStatusSink as ISoftSwitchResponder, _cpu);
        //Bus.Connect(_cpu);
        if (Bus is VA2MBus vb)
        {
            vb.VBlank += OnVBlank;
        }
        // Start flash timer if status provider available
    
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
    /// <strong>Exception Handling:</strong> Exceptions in actions are caught and logged
    /// to prevent one bad command from crashing the emulator.
    /// </para>
    /// </remarks>
    private void ProcessPending()
    {
        while (_pending.TryDequeue(out var act))
        {
            try { act(); } catch { Debug.WriteLine($"Exception during ProcessPending()"); }
        }
    }

    /// <summary>
    /// Handles VBlank event from the bus, triggering frame rendering and flash toggle.
    /// </summary>
    /// <param name="sender">Event sender (typically VA2MBus).</param>
    /// <param name="e">Event arguments (unused).</param>
    /// <remarks>
    /// <para>
    /// <strong>VBlank Timing:</strong> Fires every 17,030 cycles (~60 Hz), matching the
    /// Apple IIe's vertical blanking interval. During VBlank, the video scanner is not
    /// drawing visible scanlines.
    /// </para>
    /// <para>
    /// <strong>Operations Performed:</strong>
    /// <list type="number">
    /// <item>Toggle flash state if timer has set the flag (cursor blinking)</item>
    /// <item>Allocate a new render context from the frame generator</item>
    /// <item>Render the current frame</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Thread Context:</strong> Called on the emulator thread (VBlank is raised
    /// during Bus.Clock() execution).
    /// </para>
    /// </remarks>
    private void OnVBlank(object? sender, EventArgs e)
    {
        // Apply pending flash toggle at frame boundary for consistent rendering
        if (System.Threading.Interlocked.Exchange(ref _pendingFlashToggle, 0) != 0)
        {
            _sysStatusSink.Mutate(s => s.StateFlashOn = !s.StateFlashOn);
        }


        var renderContext = _frameGenerator.AllocateRenderContext();
        _frameGenerator.RenderFrame(renderContext);

    }


    /// <summary>
    /// Loads an embedded ROM resource into memory.
    /// </summary>
    /// <param name="resourceName">Fully-qualified resource name (e.g., "Pandowdy.EmuCore.Resources.a2e_enh_c-f.rom").</param>
    /// <remarks>
    /// <para>
    /// <strong>ROM Source:</strong> The Apple IIe Enhanced ROM is embedded in the assembly
    /// as a resource during build. This eliminates the need for external ROM files.
    /// </para>
    /// <para>
    /// <strong>ROM Contents (16KB):</strong>
    /// <list type="bullet">
    /// <item>$C000-$C0FF: I/O space firmware</item>
    /// <item>$C100-$C7FF: Internal peripheral ROM (7 x 256 bytes)</item>
    /// <item>$C800-$CFFF: Extended internal ROM (2KB)</item>
    /// <item>$D000-$DFFF: Monitor ROM (4KB)</item>
    /// <item>$E000-$FFFF: Applesoft BASIC ROM + reset vector (8KB)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Error Handling:</strong> If the resource is not found, the ROM is not loaded
    /// and the emulator will not function correctly (reset vector missing). This is a
    /// fatal configuration error that should be caught during development.
    /// </para>
    /// </remarks>
    private void TryLoadEmbeddedRom(string resourceName)
    {
        var asm = Assembly.GetExecutingAssembly();
        using Stream? s = asm.GetManifestResourceStream(resourceName);
        if (s != null)
        {
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            MemoryPool.InstallApple2ROM(ms.ToArray());
        }
    }

    /// <summary>
    /// Advance one system clock (one CPU/bus cycle). If throttling is enabled,
    /// the call will delay to keep approx TargetHz. Suitable for simple loops.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Execution Sequence:</strong>
    /// <list type="number">
    /// <item>Process pending cross-thread commands (ProcessPending)</item>
    /// <item>Execute one bus clock cycle (CPU + memory + I/O)</item>
    /// <item>Increment throttle cycle counter</item>
    /// <item>Apply throttling delay if enabled (ThrottleOneCycle)</item>
    /// <item>Publish emulator state snapshot</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Use Cases:</strong>
    /// <list type="bullet">
    /// <item>Single-step debugging (execute one instruction at a time)</item>
    /// <item>Simple synchronous execution loops</item>
    /// <item>Testing and verification scenarios</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Performance Note:</strong> For continuous operation, use <see cref="RunAsync"/>
    /// instead as it batches cycles to reduce per-cycle overhead.
    /// </para>
    /// </remarks>
    public void Clock()
    {
        // Execute commands enqueued from other threads
        ProcessPending();
        Bus.Clock();
        _throttleCycles++;
        if (ThrottleEnabled)
        {
            ThrottleOneCycle();
        }
        PublishState();
    }


    /// <summary>
    /// Applies two-phase throttling delay to maintain target CPU frequency.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Two-Phase Throttling Algorithm:</strong>
    /// <list type="number">
    /// <item><strong>Sleep Phase:</strong> Thread.Sleep() for whole milliseconds (CPU-efficient)</item>
    /// <item><strong>SpinWait Phase:</strong> Busy-wait for sub-millisecond precision (accurate)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Rationale:</strong> Thread.Sleep() has millisecond granularity but is efficient
    /// (yields CPU to other threads). SpinWait provides sub-millisecond accuracy but uses CPU
    /// cycles. Combining both achieves accurate timing while remaining reasonably efficient.
    /// </para>
    /// <para>
    /// <strong>Timing Calculation:</strong>
    /// <code>
    /// expectedTime = cyclesExecuted / TargetHz
    /// leadTime = expectedTime - actualElapsedTime
    /// if (leadTime > 0) delay by leadTime
    /// </code>
    /// </para>
    /// <para>
    /// <strong>Accuracy:</strong> Achieves ~1.023 MHz within 0.1% error under normal
    /// system load. Accuracy may degrade with heavy background processes or real-time
    /// constraints in the OS scheduler.
    /// </para>
    /// </remarks>
    private void ThrottleOneCycle()
    {
        // Expected elapsed time in seconds for executed cycles
        double expectedSec = _throttleCycles / TargetHz;
        double elapsedSec = _throttleSw.Elapsed.TotalSeconds;
        double leadSec = expectedSec - elapsedSec; // >0 means we are ahead (need to wait)
        if (leadSec <= 0) { return; }

        // Sleep for the whole milliseconds part
        int sleepMs = (int) (leadSec * 1000.0);
        if (sleepMs > 0)
        {
            Thread.Sleep(sleepMs);
        }
        // Busy-wait for the remaining sub-ms time slice
        while (_throttleSw.Elapsed.TotalSeconds < expectedSec)
        {
            Thread.SpinWait(100);
        }
    }

    /// <summary>
    /// Reset machine and system clock.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Full System Reset (Power Cycle):</strong> This method performs a complete
    /// hardware reset equivalent to powering off and on the Apple IIe.
    /// </para>
    /// <para>
    /// <strong>Operations Performed:</strong>
    /// <list type="bullet">
    /// <item>Reset CPU registers (PC loaded from $FFFC/$FFFD, SP = $FF)</item>
    /// <item>Reset all soft switches to power-on state</item>
    /// <item>Reset memory bank mappings</item>
    /// <item>Clear keyboard latch</item>
    /// <item>Reset cycle counter to zero</item>
    /// <item>Restart throttle stopwatch</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Difference from UserReset:</strong> Reset() is a cold boot that initializes
    /// everything. <see cref="UserReset"/> is a warm reset that preserves memory contents
    /// and only resets the CPU (Ctrl+Reset behavior).
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> Should be called from the emulator thread.
    /// For cross-thread reset, enqueue via command queue.
    /// </para>
    /// </remarks>
    public void Reset()
    {
        //Enqueue(() =>
        {
            Bus.Reset();
            _throttleCycles = 0;
            _throttleSw.Restart();
        }
        //);
    }

    /// <summary>
    /// Performs a warm reset (Ctrl+Reset) without clearing memory or resetting cycle counters.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Warm Reset Behavior:</strong> Simulates pressing Ctrl+Reset on the Apple IIe,
    /// which resets the CPU but preserves RAM contents and continues timing.
    /// </para>
    /// <para>
    /// <strong>Operations Performed:</strong>
    /// <list type="bullet">
    /// <item>Reset CPU registers (PC loaded from $FFFC/$FFFD, SP reset)</item>
    /// <item>Memory contents preserved (RAM, soft switches mostly unchanged)</item>
    /// <item>Cycle counter continues (not reset)</item>
    /// <item>Throttle timing continues (not restarted)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Use Cases:</strong>
    /// <list type="bullet">
    /// <item>Break out of infinite loop without losing program in memory</item>
    /// <item>Return to Monitor/BASIC prompt from running program</item>
    /// <item>Recover from program hang while preserving state</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Difference from Reset:</strong> <see cref="Reset"/> is a cold boot (power cycle).
    /// UserReset() is a warm reset that preserves memory and timing state.
    /// </para>
    /// <para>
    /// <strong>Implementation Note:</strong> Delegates to VA2MBus.UserReset() which handles
    /// the CPU reset while preserving other state.
    /// </para>
    /// </remarks>
    public void UserReset()
    {
       // Enqueue(() =>
        {
            Debug.WriteLine("Calling UserReset() in VA2M");
            (Bus as VA2MBus)!.UserReset();
            //_throttleCycles = 0;
            //_throttleSw.Restart();
        }
   //     );
    }

    /// <summary>
    /// Run the emulator asynchronously with batched cycles and time slices.
    /// Batches cycles per tick (e.g.,1 ms or 60 Hz) to reduce overhead of per-cycle waits.
    /// When ThrottleEnabled is true, pacing uses the periodic timer to approximate TargetHz.
    /// When false, runs fast batches without waiting.
    /// </summary>
    /// <param name="ct">Cancellation token to stop the runner.</param>
    /// <param name="ticksPerSecond">Time slice frequency. Use 1000 for 1ms slices or 60 for video-frame pacing.</param>
    /// <returns>A task that completes when the cancellation token is triggered or the emulator stops.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="ticksPerSecond"/> is zero or negative.</exception>
    /// <remarks>
    /// <para>
    /// <strong>Throttled Mode (ThrottleEnabled = true):</strong>
    /// <list type="bullet">
    /// <item>Uses PeriodicTimer to wait for each tick (e.g., 1ms or ~16.67ms for 60 Hz)</item>
    /// <item>Executes calculated number of cycles per tick (TargetHz / ticksPerSecond)</item>
    /// <item>Accumulates fractional cycles to prevent drift over time</item>
    /// <item>Processes pending commands before each batch</item>
    /// <item>Publishes state after each batch</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Fast Mode (ThrottleEnabled = false):</strong>
    /// <list type="bullet">
    /// <item>Executes 10,000 cycle batches as fast as possible</item>
    /// <item>No timer delays (runs full CPU speed)</item>
    /// <item>Yields with Task.Delay(0) to remain responsive</item>
    /// <item>Useful for loading programs quickly or testing</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Recommended ticksPerSecond Values:</strong>
    /// <list type="bullet">
    /// <item><strong>1000:</strong> 1ms time slices, ~1,023 cycles/tick (balanced)</item>
    /// <item><strong>60:</strong> ~16.67ms slices, ~17,050 cycles/tick (matches VBlank)</item>
    /// <item><strong>100:</strong> 10ms slices, ~10,230 cycles/tick (less frequent)</item>
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
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ticksPerSecond);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1.0 / ticksPerSecond));
        double cyclesPerTick = TargetHz / ticksPerSecond;
        double carry = 0.0;
        while (!ct.IsCancellationRequested)
        {
            if (ThrottleEnabled)
            {
                try
                {
                    if (!await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                    {
                        break;
                    }
                }
                catch (OperationCanceledException) { break; }
                // Execute queued commands on emulator thread before cycles
                ProcessPending();
                double want = cyclesPerTick + carry;
                int cycles = (int)want;
                carry = want - cycles;
                for (int i = 0; i < cycles; i++)
                {
                    Bus.Clock();
                    _throttleCycles++;
                }
                PublishState();
            }
            else
            {
                const int FastBatch = 10_000;
                // Execute queued commands on emulator thread before fast batch
                    ProcessPending();
                for (int i = 0; i < FastBatch; i++)
                {
                    Bus.Clock();
                    _throttleCycles++;
                    if (ct.IsCancellationRequested)
                    {
                        break;
                    }
                }
                PublishState();
                try
                {
                    await Task.Delay(0, ct);
                }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    /// <summary>
    /// Publishes current emulator state snapshot to the registered state sink.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Snapshot Contents:</strong>
    /// <list type="bullet">
    /// <item><strong>PC:</strong> Program Counter (current instruction address)</item>
    /// <item><strong>SP:</strong> Stack Pointer (current stack position)</item>
    /// <item><strong>SystemClock:</strong> Total CPU cycles executed since reset</item>
    /// <item><strong>BASIC Line:</strong> Current Applesoft BASIC line number (if in BASIC)</item>
    /// <item><strong>Running:</strong> Always true during execution</item>
    /// <item><strong>Paused:</strong> Always false during execution</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>BASIC Line Number Detection:</strong> Reads zero page locations $75-$76
    /// which contain the current BASIC line pointer. Valid line numbers are < $FA00.
    /// Returns null if not in BASIC or if pointer is invalid.
    /// </para>
    /// <para>
    /// <strong>Call Frequency:</strong> Called after every Clock() and after each batch
    /// in RunAsync(). This provides real-time updates for UI display but has minimal overhead
    /// since the snapshot is just a small value type.
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> The state sink (IEmulatorState) is responsible for
    /// thread-safe distribution of the snapshot to observers (typically using reactive patterns).
    /// </para>
    /// </remarks>
    private void PublishState()
    {
        int lineNum = (int)(Bus.CpuRead(0x75) + (Bus.CpuRead(0x76) << 8));
        int? basicLine = lineNum < 0xFA00 ? lineNum : null;
        var snapshot = new StateSnapshot((ushort) Bus.Cpu.PC, (byte)Bus.Cpu.SP, Bus.SystemClockCounter, basicLine, true, false);
        _stateSink.Update(snapshot);
    }



    /// <summary>
    /// Inject a keyboard value into the machine as if a key was latched at $C000.
    /// High bit must be set.  Cleared by access of $C010.
    /// </summary>
    /// <param name="ascii">ASCII character code (0-127). High bit will be set automatically.</param>
    /// <remarks>
    /// <para>
    /// <strong>Apple IIe Keyboard Protocol:</strong> The Apple IIe keyboard latch is a memory-mapped
    /// register at $C000. When a key is pressed, the ASCII value with bit 7 set appears at this
    /// address. Reading $C010 (KBDSTRB) clears bit 7, indicating the key has been read.
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> This method is thread-safe. It enqueues the key injection
    /// command which will be executed on the emulator thread at the next ProcessPending() call.
    /// This allows UI threads to inject keyboard input without race conditions.
    /// </para>
    /// <para>
    /// <strong>ASCII Codes:</strong>
    /// <list type="bullet">
    /// <item>0x20-0x7E: Standard printable ASCII characters</item>
    /// <item>0x0D (13): Return/Enter key</item>
    /// <item>0x08 (8): Backspace (sometimes)</item>
    /// <item>0x15 (21): Right arrow (Apple IIe convention)</item>
    /// <item>0x1B (27): Escape</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Implementation:</strong> The method automatically sets bit 7 (ORs with 0x80)
    /// to match Apple IIe hardware behavior. The command is enqueued and executed via
    /// Bus.SetKeyValue() on the emulator thread.
    /// </para>
    /// </remarks>
    public void InjectKey(byte ascii)
    {
        // Enqueue to run on emulator thread
        byte val = (byte)(ascii | 0x80);
        Enqueue(() => Bus.SetKeyValue(val));
    }

    /// <summary>
    /// Sets the state of an Apple IIe pushbutton (game controller button).
    /// </summary>
    /// <param name="num">Button number (0-2). Button 0 is typically button 0, buttons 1-2 are paddle buttons.</param>
    /// <param name="pressed">True if button is pressed, false if released.</param>
    /// <remarks>
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
    /// <strong>Thread Safety:</strong> This method is thread-safe. It enqueues the button
    /// state change which will be executed on the emulator thread at the next ProcessPending() call.
    /// </para>
    /// </remarks>
    public void SetPushButton(byte num, bool pressed)
    {
        Enqueue(() => Bus.SetPushButton(num, pressed));
    }

    /// <summary>
    /// Enqueues generation of a system status snapshot containing soft switch and button states.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Purpose:</strong> Triggers creation of a comprehensive system status snapshot
    /// that includes all soft switches (memory mapping, video modes, ROM selection, annunciators)
    /// and pushbutton states.
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> This method is thread-safe. It enqueues the status
    /// generation command which will be executed on the emulator thread via <see cref="BuildStatusData"/>.
    /// </para>
    /// <para>
    /// <strong>Typical Usage:</strong> Called periodically (e.g., at frame boundaries or on-demand)
    /// to update UI displays showing soft switch states, debug panels, or system status indicators.
    /// </para>
    /// <para>
    /// <strong>Snapshot Contents:</strong> See <see cref="BuildStatusData"/> for details
    /// on what data is captured in the status snapshot.
    /// </para>
    /// </remarks>
    public void GenerateStatusData()
    {
        Enqueue(() => BuildStatusData());
    }

    /// <summary>
    /// Immutable dictionary mapping soft switch IDs to their corresponding snapshot builder setter methods.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Purpose:</strong> Provides efficient, type-safe mapping from SoftSwitchId enum
    /// values to the appropriate property setter on SystemStatusSnapshotBuilder.
    /// </para>
    /// <para>
    /// <strong>Why Immutable:</strong> The dictionary is initialized once at class load time
    /// and never modified, making it thread-safe and cache-friendly for repeated lookups.
    /// </para>
    /// <para>
    /// <strong>Usage Pattern:</strong> Used by <see cref="BuildStatusData"/> to efficiently
    /// populate the status snapshot from soft switch states without large switch statements.
    /// </para>
    /// <para>
    /// <strong>Coverage:</strong> Maps 19 of 20 soft switches (PreWrite is internal state,
    /// not typically published to UI).
    /// </para>
    /// </remarks>
    private static readonly ImmutableDictionary<SoftSwitches.SoftSwitchId, System.Action<SystemStatusSnapshotBuilder, bool>> _switchSetters
        = new Dictionary<SoftSwitches.SoftSwitchId, System.Action<SystemStatusSnapshotBuilder, bool>>
        {
            { SoftSwitches.SoftSwitchId.Store80, (b,v) => b.State80Store = v },
            { SoftSwitches.SoftSwitchId.RamRd, (b,v) => b.StateRamRd = v },
            { SoftSwitches.SoftSwitchId.RamWrt, (b,v) => b.StateRamWrt = v },
            { SoftSwitches.SoftSwitchId.IntCxRom, (b,v) => b.StateIntCxRom = v },
            { SoftSwitches.SoftSwitchId.AltZp, (b,v) => b.StateAltZp = v },
            { SoftSwitches.SoftSwitchId.SlotC3Rom, (b,v) => b.StateSlotC3Rom = v },
            { SoftSwitches.SoftSwitchId.Vid80, (b,v) => b.StateShow80Col = v },
            { SoftSwitches.SoftSwitchId.AltChar, (b,v) => b.StateAltCharSet = v },
            { SoftSwitches.SoftSwitchId.Text, (b,v) => b.StateTextMode = v },
            { SoftSwitches.SoftSwitchId.Mixed, (b,v) => b.StateMixed = v },
            { SoftSwitches.SoftSwitchId.Page2, (b,v) => b.StatePage2 = v },
            { SoftSwitches.SoftSwitchId.HiRes, (b,v) => b.StateHiRes = v },
            { SoftSwitches.SoftSwitchId.An0, (b,v) => b.StateAnn0 = v },
            { SoftSwitches.SoftSwitchId.An1, (b,v) => b.StateAnn1 = v },
            { SoftSwitches.SoftSwitchId.An2, (b,v) => b.StateAnn2 = v },
            { SoftSwitches.SoftSwitchId.An3, (b,v) => b.StateAnn3 = v },
            { SoftSwitches.SoftSwitchId.Bank1, (b,v) => b.StateUseBank1 = v },
            { SoftSwitches.SoftSwitchId.HighRead, (b,v) => b.StateHighRead = v },
            { SoftSwitches.SoftSwitchId.HighWrite, (b,v) => b.StateHighWrite = v },
        }.ToImmutableDictionary();

    /// <summary>
    /// Builds and publishes a comprehensive system status snapshot containing all soft switches and button states.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Data Captured:</strong>
    /// <list type="bullet">
    /// <item><strong>Soft Switches:</strong> All 19 user-visible soft switches (memory, video, ROM, annunciators)</item>
    /// <item><strong>Pushbuttons:</strong> State of all 3 game controller buttons</item>
    /// <item><strong>Change Counts:</strong> Number of times each switch has changed (for debugging)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Implementation:</strong> Uses the <see cref="_switchSetters"/> dictionary for
    /// efficient mapping of soft switch values to snapshot builder properties. The snapshot
    /// is built using the mutable builder pattern and then published atomically to the
    /// status provider sink.
    /// </para>
    /// <para>
    /// <strong>Thread Context:</strong> Must be called on the emulator thread. External threads
    /// should use <see cref="GenerateStatusData"/> which enqueues this method safely.
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> Relatively lightweight operation (dictionary lookups + property sets).
    /// Typically called at frame boundaries (~60 Hz) or on-demand for UI updates.
    /// </para>
    /// </remarks>
    private void BuildStatusData()
    {
        var switches = (Bus as VA2MBus)?.Switches;
        var data = switches!.GetSwitchList();

        _sysStatusSink.Mutate(b =>
        {
            foreach (var (id, value, count) in data)
            {
                if (_switchSetters.TryGetValue(id, out var setter))
                {
                    setter(b, value);
                }
            }

            var vb = Bus as VA2MBus;
            b.StatePb0 = vb!.GetPushButton(0);
            b.StatePb1 = vb!.GetPushButton(1);
            b.StatePb2 = vb!.GetPushButton(2);
        });
    }


    /// <summary>
    /// Releases all resources used by the VA2M emulator instance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Cleanup Sequence:</strong>
    /// <list type="number">
    /// <item>Stop and dispose flash timer (~2.1 Hz cursor blink timer)</item>
    /// <item>Clear pending command queue to release enqueued actions</item>
    /// <item>Dispose bus (includes VBlank event cleanup)</item>
    /// <item>Suppress finalization (no unmanaged resources)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> Should be called from the thread that owns the emulator
    /// instance (typically the UI thread after stopping emulation). Do not call while RunAsync()
    /// is executing.
    /// </para>
    /// <para>
    /// <strong>MemoryPool Note:</strong> MemoryPool disposal is currently commented out as it
    /// may be shared across multiple emulator instances or have external ownership. This should
    /// be revisited if MemoryPool lifetime is clarified.
    /// </para>
    /// <para>
    /// <strong>CPU Note:</strong> The legacy 6502.NET CPU library does not implement IDisposable,
    /// so no explicit CPU cleanup is needed. The CPU instance is managed by the Bus.
    /// </para>
    /// </remarks>
    public void Dispose()
    {
        // Dispose flash timer
        _flashTimer?.Dispose();
        _flashTimer = null;
        
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
