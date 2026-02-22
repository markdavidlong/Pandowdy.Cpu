// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.EmuCore.Interfaces;

/// <summary>
/// Defines the core control interface for emulator operations accessible from external threads (primarily UI).
/// </summary>
/// <remarks>
/// <para>
/// <strong>Purpose:</strong> This interface serves as the primary contract between the UI and the emulator core,
/// providing thread-safe command queueing (Reset, EnqueueKey, etc.), execution control (RunAsync, Clock, ThrottleEnabled),
/// and observable accessors for system state. It represents the **complete control surface** that the UI needs to 
/// interact with the emulator - a firm seam between GUI and emulator layers.
/// </para>
/// <para>
/// <strong>The Firm Seam:</strong> This interface defines the explicit boundary between the UI and emulator core.
/// The UI depends only on this interface and never accesses concrete VA2M implementation details like Bus or MemoryPool.
/// This provides:
/// <list type="bullet">
/// <item><strong>Clear Contract:</strong> Everything the UI needs is explicitly defined here</item>
/// <item><strong>Thread Safety:</strong> Explicit guarantees about which methods are thread-safe</item>
/// <item><strong>Testability:</strong> Single interface to mock for UI testing</item>
/// <item><strong>Encapsulation:</strong> Implementation details hidden from UI</item>
/// </list>
/// </para>
/// <para>
/// <strong>Thread Safety Contract:</strong>
/// <list type="bullet">
/// <item><strong>Reset/EnqueueKey/SetPushButton:</strong> Thread-safe command queueing.
/// Commands are enqueued and executed at instruction boundaries on the emulator thread, respecting
/// 6502 atomic instruction guarantees.</item>
/// <item><strong>RunAsync:</strong> Thread-safe. Starts async execution loop on a background thread
/// (typically called via Task.Run from UI thread).</item>
/// <item><strong>Clock:</strong> Single-step execution. Should be called from the emulator thread
/// (used for debugging/testing).</item>
/// <item><strong>ThrottleEnabled:</strong> Thread-safe property. Can be set from any thread
/// (typically UI thread via data binding).</item>
/// <item><strong>EmulatorState/FrameProvider/SystemStatus:</strong> Thread-safe read-only observable
/// accessors. These provide reactive streams that can be subscribed to from any thread.</item>
/// </list>
/// </para>
/// <para>
/// <strong>Architecture Benefits:</strong>
/// <list type="bullet">
/// <item><strong>Single Seam:</strong> UI depends only on this interface, not concrete VA2M type</item>
/// <item><strong>Decoupling:</strong> UI has no knowledge of implementation details (Bus, MemoryPool, etc.)</item>
/// <item><strong>Testability:</strong> Interface can be mocked for UI testing without full emulator</item>
/// <item><strong>Thread Safety:</strong> Provides explicit contract preventing accidental cross-thread calls</item>
/// <item><strong>Interface Segregation:</strong> UI only sees what it needs (11 members vs 1,200+ lines of implementation)</item>
/// <item><strong>Observable Pattern:</strong> State changes flow through reactive streams, not property polling</item>
/// </list>
/// </para>
/// <para>
/// <strong>Implementation:</strong> VA2M implements this interface, providing the UI with a clean
/// abstraction for emulator control without exposing internal implementation details like bus coordination,
/// memory management, or CPU internals.
/// </para>
/// <para>
/// <strong>Naming Rationale:</strong> "EmulatorCoreInterface" emphasizes that this is the **core control interface**
/// for the emulator, not just a collection of queueable commands. It includes command queueing, execution control,
/// and observable state accessors - representing the complete UI control surface.
/// </para>
/// </remarks>
public interface IEmulatorCoreInterface : IKeyboardSetter
{
    #region Command Queueing (Thread-Safe)

    /// <summary>
    /// Queues a full system reset (power cycle).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Thread Safety:</strong> Thread-safe. Can be called from any thread (typically UI thread).
    /// Performs a complete hardware reset equivalent to powering off and on the Apple IIe.
    /// Executed at instruction boundary.
    /// </para>
    /// <para>
    /// <strong>Operations Performed:</strong> Clears keyboard latch and pending keystrokes,
    /// resets CPU registers (PC loaded from $FFFC/$FFFD, SP = $FF), resets all soft switches
    /// to power-on state, resets memory bank mappings, resets cycle counter to zero, restarts
    /// throttle stopwatch, and resets performance measurement counters.
    /// </para>
    /// </remarks>
    void DoReset();


    /// <summary>
    /// Queues a full cold boot (power cycle) restoring every registered
    /// <see cref="IRestartable"/> component to its initial power-on state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Thread Safety:</strong> Thread-safe. Can be called from any thread.
    /// The restart is enqueued for execution on the emulator thread at the next
    /// instruction boundary, respecting 6502 atomic instruction guarantees.
    /// </para>
    /// <para>
    /// <strong>Execution Sequence:</strong> Performs a warm reset first (keyboard clear,
    /// CPU reset vector, throttle state), then iterates all registered
    /// <see cref="IRestartable"/> components in priority order — clearing RAM, resetting
    /// soft switches, cold-initializing cards, and finally resetting the CPU.
    /// </para>
    /// <para>
    /// <strong>Difference from <see cref="DoReset"/>:</strong> <see cref="DoReset"/> is a
    /// warm reset (Ctrl+Reset equivalent) that preserves RAM, soft switches, and card state.
    /// <see cref="DoRestart"/> is a cold boot that resets everything to power-on defaults.
    /// </para>
    /// </remarks>
    void DoRestart();

    /// <summary>
    /// Queues a pushbutton state change for game controller.
    /// </summary>
    /// <param name="num">Button number (0-2). Button 0-2 map to $C061-$C063.</param>
    /// <param name="pressed">True if button is pressed, false if released.</param>
    /// <remarks>
    /// <para>
    /// <strong>Thread Safety:</strong> Thread-safe. Can be called from any thread (typically UI thread
    /// from input event handlers). The button state change will be executed at the next instruction boundary.
    /// </para>
    /// <para>
    /// <strong>Event-Driven Architecture:</strong> The change triggers ButtonChanged events that
    /// SystemStatusProvider observes automatically, ensuring SystemStatus stays synchronized without
    /// manual polling.
    /// </para>
    /// </remarks>
    void SetPushButton(byte num, bool pressed);

    /// <summary>
    /// Sends a message to a specific card or broadcasts to all cards.
    /// </summary>
    /// <param name="slot">Target slot (Slot1–Slot7), or <c>null</c> to broadcast to all slots.</param>
    /// <param name="message">The card message to deliver.</param>
    /// <returns>A task that completes when the message(s) have been processed on the emulator thread.</returns>
    /// <exception cref="Exceptions.CardMessageException">
    /// Thrown (on the returned Task) if a targeted card rejects the message. Not thrown for
    /// broadcast messages — individual card failures are silently ignored during broadcast.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <strong>Thread Safety:</strong> Thread-safe. The message is enqueued on the emulator
    /// thread's command queue and executed at the next instruction boundary, identical to
    /// DoReset() and EnqueueKey(). The returned Task allows the caller to await completion
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
    System.Threading.Tasks.Task SendCardMessageAsync(SlotNumber? slot, ICardMessage message);

    #endregion
    
    #region Execution Control
    
    /// <summary>
    /// Starts asynchronous emulator execution loop.
    /// </summary>
    /// <param name="ct">Cancellation token to stop execution.</param>
    /// <param name="ticksPerSecond">Time slice frequency (e.g., 60 for frame pacing, 1000 for 1ms slices).</param>
    /// <returns>Task that completes when cancellation is requested or execution stops.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Thread Context:</strong> This method should be called from a background thread
    /// (typically via Task.Run from UI thread). It will execute the emulator at the specified
    /// frequency until the cancellation token is triggered.
    /// </para>
    /// <para>
    /// <strong>Throttling:</strong> When <see cref="ThrottleEnabled"/> is true, runs at ~1.023 MHz
    /// (Apple IIe speed) using PID-based adaptive throttling. When false, runs as fast as possible
    /// (useful for loading programs quickly or testing).
    /// </para>
    /// <para>
    /// <strong>Batch Execution:</strong> Executes cycles in batches (e.g., ~1,023 cycles per 1ms tick)
    /// to reduce overhead. Fractional cycle accumulation prevents timing drift over time.
    /// </para>
    /// </remarks>
    System.Threading.Tasks.Task RunAsync(System.Threading.CancellationToken ct, double ticksPerSecond = 1000d);
    
    /// <summary>
    /// Executes one CPU clock cycle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Thread Context:</strong> Should be called from the emulator thread. Processes pending
    /// commands, executes one bus clock cycle, applies throttling if enabled, and publishes state.
    /// </para>
    /// <para>
    /// <strong>Use Case:</strong> Single-step debugging, testing, or simple synchronous execution loops.
    /// For continuous operation, use <see cref="RunAsync"/> instead as it batches cycles for efficiency.
    /// </para>
    /// <para>
    /// <strong>Performance Note:</strong> Clock() has per-cycle overhead from command processing
    /// and state publishing. RunAsync() batches these operations for better performance.
    /// </para>
    /// </remarks>
    void Clock();
    
    /// <summary>
    /// Gets or sets whether throttling is enabled to maintain Apple IIe speed.
    /// </summary>
    /// <value>True to run at ~1.023 MHz (Apple IIe speed); false to run as fast as possible.</value>
    /// <remarks>
    /// <para>
    /// <strong>Thread Safety:</strong> Thread-safe property. Can be set from any thread (typically
    /// UI thread via data binding). Setting this property resets throttling state (cycle counters,
    /// error accumulators, adaptive parameters) to prevent timing issues when switching modes.
    /// </para>
    /// <para>
    /// <strong>Fast Mode (false):</strong> Runs as fast as possible (typically 10-20 MHz on modern CPUs).
    /// Useful for loading programs quickly, running tests, or when maximum speed is desired.
    /// </para>
    /// <para>
    /// <strong>Throttled Mode (true):</strong> Uses PID-based adaptive throttling to maintain
    /// ~1.023 MHz within 0.05% error (~500 PPM). Provides authentic Apple IIe speed for gameplay
    /// and software compatibility.
    /// </para>
    /// </remarks>
    bool ThrottleEnabled { get; set; }
    
    #endregion
    
    #region Observable State Accessors (Read-Only)
    
    /// <summary>
    /// Gets the emulator state observable for monitoring CPU status.
    /// </summary>
    /// <value>Observable that publishes CPU state snapshots (PC, SP, cycles, BASIC line).</value>
    /// <remarks>
    /// <para>
    /// <strong>Thread Safety:</strong> Thread-safe read-only property. The returned <see cref="IEmulatorState"/>
    /// interface provides reactive streams that can be subscribed to from any thread.
    /// </para>
    /// <para>
    /// <strong>Observable Pattern:</strong> State changes are pushed through reactive streams rather than
    /// polled. Subscribe to <see cref="IEmulatorState.Stream"/> to receive real-time CPU state updates.
    /// </para>
    /// <para>
    /// <strong>Use Cases:</strong> Display PC/SP in debugger, show cycle counter, detect BASIC line changes,
    /// monitor running/paused state for UI updates.
    /// </para>
    /// </remarks>
    IEmulatorState EmulatorState { get; }
    
    /// <summary>
    /// Gets the frame provider observable for receiving rendered video frames.
    /// </summary>
    /// <value>Observable that publishes rendered video frames (560Ã—192 pixels with soft switch state).</value>
    /// <remarks>
    /// <para>
    /// <strong>Thread Safety:</strong> Thread-safe read-only property. The returned <see cref="IFrameProvider"/>
    /// interface provides reactive streams for frame updates from the threaded rendering service.
    /// </para>
    /// <para>
    /// <strong>Observable Pattern:</strong> Frames are rendered on a separate thread and published through
    /// reactive streams. Subscribe to <see cref="IFrameProvider.FrameAvailable"/> to receive frames at ~60 Hz.
    /// </para>
    /// <para>
    /// <strong>Use Cases:</strong> Display Apple IIe video output, capture screenshots, record video.
    /// Frames include both the bitmap data (560Ã—192) and soft switch state for mode-accurate rendering.
    /// </para>
    /// </remarks>
    IFrameProvider FrameProvider { get; }
    
    /// <summary>
    /// Gets the system status observable for monitoring soft switches and system state.
    /// </summary>
    /// <value>Observable that publishes system status snapshots (all 20+ soft switches and I/O states).</value>
    /// <remarks>
    /// <para>
    /// <strong>Thread Safety:</strong> Thread-safe read-only property. The returned <see cref="ISystemStatusProvider"/>
    /// interface provides reactive streams for system state changes.
    /// </para>
    /// <para>
    /// <strong>Observable Pattern:</strong> System status changes (soft switches, button states, paddle values)
    /// are published through reactive streams. Subscribe to <see cref="ISystemStatusProvider.Stream"/> or
    /// <see cref="ISystemStatusProvider.Changed"/> event to receive updates.
    /// </para>
        /// <para>
        /// <strong>Use Cases:</strong> Display soft switch status panel, show video mode indicators, monitor
        /// memory configuration, display game controller state, debug system behavior.
        /// </para>
        /// </remarks>
        ISystemStatusProvider SystemStatus { get; }

        /// <summary>
        /// Gets the disk status provider for monitoring disk drive states.
        /// </summary>
        /// <value>Read-only stream of disk drive status snapshots.</value>
        /// <remarks>
        /// <para>
        /// <strong>Thread Safety:</strong> Thread-safe read-only property. The returned <see cref="Services.IDiskStatusProvider"/>
        /// interface provides a reactive stream for disk drive status that can be subscribed to from any thread.
        /// </para>
        /// <para>
        /// <strong>Observable Pattern:</strong> Disk drives publish status changes (motor state, track position,
        /// disk insertion, etc.) through the provider. Subscribe to <see cref="Services.IDiskStatusProvider.Stream"/>
        /// to receive drive status updates.
        /// </para>
        /// <para>
        /// <strong>Thread Marshaling:</strong> Subscribers must use <c>ObserveOn(RxApp.MainThreadScheduler)</c>
        /// to marshal callbacks to the UI thread before updating bound properties.
        /// </para>
        /// <para>
        /// <strong>Use Cases:</strong> Display drive activity indicators (motor on, track number), show disk
        /// insertion/ejection notifications, implement drive status panel.
        /// </para>
        /// </remarks>
        Services.IDiskStatusProvider DiskStatus { get; }

        #endregion

        #region Direct State Inspection (Read-Only, Thread-Safe)

        /// <summary>
        /// Gets comprehensive read-only access to all memory regions for display and debugging.
        /// </summary>
        /// <value>Access to main RAM, auxiliary RAM, system ROM, slot ROM, and active memory mapping.</value>
        /// <remarks>
        /// <para>
        /// <strong>Thread Safety:</strong> Thread-safe for reads. Byte reads from fixed-size arrays
        /// are atomic. May show mid-instruction memory state (acceptable for display purposes).
        /// </para>
        /// <para>
        /// <strong>Memory Regions:</strong>
        /// <list type="bullet">
        /// <item><strong>Main/Aux RAM:</strong> <see cref="IDirectMemoryPoolReader.ReadRawMain"/> and <see cref="IDirectMemoryPoolReader.ReadRawAux"/></item>
        /// <item><strong>System ROM:</strong> <see cref="IMemoryInspector.ReadSystemRom"/> for $C000-$FFFF ROM</item>
        /// <item><strong>Slot ROM:</strong> <see cref="IMemoryInspector.ReadSlotRom"/> for specific slot's ROM</item>
        /// <item><strong>Active Mapping:</strong> <see cref="IMemoryInspector.ReadActiveHighMemory"/> for current $C100-$FFFF mapping</item>
        /// </list>
        /// </para>
        /// <para>
        /// <strong>Use Cases:</strong> Memory viewer/hex dump panel, debugger memory inspection,
        /// watch expressions, memory search, ROM disassembly, video memory debugging.
        /// </para>
        /// <para>
        /// <strong>No Secondary Loop Required:</strong> Since memory arrays are fixed-size and byte
        /// reads are atomic, GUI can safely read at any time without requiring a timer-based service loop.
        /// </para>
        /// </remarks>
        IMemoryInspector MemoryInspector { get; }

        /// <summary>
        /// Gets a snapshot of the current CPU state for display and debugging.
        /// </summary>
        /// <value>Immutable snapshot of CPU registers, flags, and execution status.</value>
        /// <remarks>
        /// <para>
        /// <strong>Thread Safety:</strong> Thread-safe. Returns a readonly struct copy, so the caller
        /// receives a consistent snapshot. Changes to the returned struct do not affect CPU state.
        /// </para>
        /// <para>
        /// <strong>Contents:</strong> Includes all programmer-visible registers (A, X, Y, SP, PC, P),
        /// individual flag accessors (N, V, B, D, I, Z, C), execution status, and cycles remaining.
        /// </para>
        /// <para>
        /// <strong>Mid-Instruction State:</strong> May show intermediate register values during
        /// multi-cycle instruction execution. For display purposes this is acceptable; use
        /// <see cref="DataTypes.CpuStateSnapshot.AtInstructionBoundary"/> to check if at a safe point.
        /// </para>
        /// <para>
        /// <strong>Use Cases:</strong> CPU register panel in debugger, watch expressions for A/X/Y/SP,
        /// disassembly view showing PC, status flag indicators (N/V/B/D/I/Z/C).
        /// </para>
        /// </remarks>
        DataTypes.CpuStateSnapshot CpuState { get; }

        /// <summary>
        /// Gets the total number of CPU cycles executed since the last reset.
        /// </summary>
        /// <value>Total cycle count (64-bit, sufficient for years of continuous execution).</value>
        /// <remarks>
        /// <para>
        /// <strong>Thread Safety:</strong> Thread-safe read of a 64-bit counter.
        /// </para>
        /// <para>
        /// <strong>Use Cases:</strong> Cycle counter display in debugger, performance timing,
        /// profiling instruction execution, timing-based breakpoints.
        /// </para>
        /// </remarks>
        ulong TotalCycles { get; }

        #endregion
    }
