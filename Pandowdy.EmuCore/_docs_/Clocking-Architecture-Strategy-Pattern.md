# Clocking Architecture - Strategy Pattern Design

## Overview

The emulator requires two fundamentally different clocking modes:
1. **Automatic Running** - Continuous execution with throttling (normal play)
2. **Debug Stepping** - Manual single-step or breakpoint-based execution

Rather than embedding both modes in `VA2M`, we extract clocking strategies behind an `IClockingController` interface.

## Critical Architectural Separation

**⚠️ IMPORTANT: VBlank and Frame Rendering Are Decoupled**

The emulator's VBlank event is **completely independent** from the GUI's frame rendering:

- **Emulator VBlank** (SystemClock): 
  - Cycles at the emulated Apple IIe rate (~60 Hz in emulated time)
  - Fires every 17,063 cycles regardless of real-world time
  - Makes $C019 readable switch accurate
  - The emulator "hallucinates" it's drawing to a CRT display
  - Runs at whatever speed throttling/debug mode allows

- **GUI Frame Rendering** (IFrameProvider/FrameGenerator):
  - Samples frames from emulator at real-time 60 Hz
  - Driven by UI's `IRefreshTicker` (~60 Hz wall-clock time)
  - Completely asynchronous from emulator cycles
  - Works whether emulator runs fast, slow, throttled, or paused

**The emulator doesn't know it's headless.** It tracks VBlank cycles as if it's driving a real CRT, but the GUI independently samples the current frame buffer at its own rate.

### Why This Matters

In **throttled mode** (coincidentally synchronized):
- Emulator VBlank ≈ 60 Hz emulated time
- GUI sampling ≈ 60 Hz real time
- **They coincidentally stay in sync because emulator is throttled to ~1 MHz**
- The $C019 VBlank flag happens to align with GUI refresh timing
- This is a **side effect** of throttling, not a requirement

In **fast-forward mode** (completely independent):
- Emulator VBlank >> 60 Hz (thousands per second)
- GUI sampling = 60 Hz real time
- **No relationship between VBlank and GUI rendering**
- GUI sees many "frames" have passed, samples current state
- The emulator has gone through many VBlanks, but that's irrelevant to GUI

In **debug stepping mode** (manual control):
- Emulator VBlank = manual (cycles when you step 17,063 times)
- GUI sampling = 60 Hz real time or on-demand
- **Completely decoupled** - VBlank is just a counter reaching 17,063
- Render frames whenever needed for debugging
- VBlank flag is accurate based on cycle count, not time

### Key Point: VBlank is Pure Cycle Counting

The emulator's VBlank logic is **purely mathematical**:
```
if (cycles >= 17,063) {
    Fire VBlank event
    Set $C019 readable for next 4,550 cycles
}
```

This happens whether the emulator runs at:
- 1 MHz (throttled, ~60 Hz VBlank) → coincidentally synchronized with GUI
- 1000 MHz (fast-forward) → 60,000 Hz VBlank
- 1 cycle/second (stepping) → VBlank every 4.75 hours of real time
- Paused → VBlank never fires

**The GUI doesn't care.** It samples the frame buffer at 60 Hz wall-clock time regardless of emulator speed.

## Architecture

```
VA2M (Orchestrator)
  |
  +-- IClockingController (Strategy Interface)
  |    |
  |    +-- AutomaticClockingController (continuous execution)
  |    |    └── Handles throttling, batch execution
  |    |
  |    +-- DebugClockingController (step-based execution)
  |         └── Handles single-step, breakpoints
  |
  +-- SystemClock (VBlank event generator - emulated timing)
  |    └── Fires VBlank every 17,063 cycles (emulated ~60 Hz)
  |
  +-- FrameGenerator (GUI-driven, real-time sampling)
       └── Renders frames when GUI requests them
```

### Timing Flow Comparison

**Automatic Mode (Throttled)**:
```
Emulator Thread              GUI Thread
----------------             ----------
SystemClock.Tick()           IRefreshTicker (60 Hz wall-clock)
  └─> Every 17063 cycles         |
      VBlank event (emulated)    v
           |                 Sample current frame buffer
           |                 Render to display
           v                     |
      $C019 readable            v
                             Display updated
```

**Fast-Forward Mode**:
```
Emulator Thread              GUI Thread
----------------             ----------
SystemClock.Tick() × 1000    IRefreshTicker (60 Hz wall-clock)
  └─> VBlank fires many          |
      times per real second      v
           |                 Sample current frame buffer
           v                 (many VBlanks have passed)
      $C019 readable            |
                               v
                           Display shows latest state
```

**Debug Mode**:
```
Emulator Thread              GUI Thread (or debugger)
----------------             ----------------------
[Manual step]                [Render on demand]
SystemClock.Tick() × 1           |
  └─> VBlank after 17063         v
      manual steps           FrameGenerator.RenderFrame()
           |                     |
           v                     v
      $C019 readable        Display shows current state
```

## Interface Design

### IClockingController

```csharp
namespace Pandowdy.EmuCore.Interfaces;

/// <summary>
/// Strategy interface for controlling emulator clock execution.
/// </summary>
/// <remarks>
/// Implementations determine how the emulator advances through cycles:
/// continuous automatic execution, single-step debugging, or other strategies.
/// 
/// <para>
/// <strong>Important:</strong> The SystemClock's VBlank event represents emulated
/// timing (every 17,063 cycles) and is completely decoupled from the GUI's frame
/// rendering, which samples frames asynchronously at real-time 60 Hz via IRefreshTicker.
/// </para>
/// </remarks>
public interface IClockingController : IDisposable
{
    /// <summary>
    /// Gets the system clock that tracks elapsed cycles and VBlank timing.
    /// </summary>
    /// <remarks>
    /// The SystemClock's VBlank event fires every 17,063 cycles in emulated time,
    /// regardless of real-world execution speed. This makes the $C019 VBlank readable
    /// switch accurate from the emulated Apple IIe's perspective.
    /// </remarks>
    SystemClock SystemClock { get; }
    
    /// <summary>
    /// Gets or sets whether throttling is enabled (limits speed to target Hz).
    /// </summary>
    /// <remarks>
    /// Only applicable to automatic clocking strategies. Debug strategies ignore this.
    /// When enabled, attempts to keep emulated time synchronized with real time.
    /// When disabled (fast-forward), emulator runs as fast as possible.
    /// </remarks>
    bool ThrottleEnabled { get; set; }
    
    /// <summary>
    /// Gets or sets the target CPU frequency in Hz (default: 1,023,000 for Apple IIe).
    /// </summary>
    double TargetHz { get; set; }
    
    /// <summary>
    /// Execute a single clock cycle.
    /// </summary>
    /// <param name="clockAction">The action to execute (typically Bus.Clock())</param>
    /// <returns>Metadata about the cycle execution (VBlank occurred, etc.)</returns>
    ClockResult ExecuteCycle(Action clockAction);
    
    /// <summary>
    /// Start continuous automatic execution (for automatic strategies).
    /// </summary>
    /// <param name="clockAction">The action to execute each cycle</param>
    /// <param name="ct">Cancellation token to stop execution</param>
    /// <param name="ticksPerSecond">Time slice frequency (1000 = 1ms, 60 = frame-based)</param>
    /// <returns>Task that completes when execution stops</returns>
    Task RunAsync(Action clockAction, CancellationToken ct, double ticksPerSecond = 1000d);
    
    /// <summary>
    /// Execute cycles until a condition is met (for debug strategies).
    /// </summary>
    /// <param name="clockAction">The action to execute each cycle</param>
    /// <param name="breakCondition">Predicate that returns true when execution should stop</param>
    /// <param name="maxCycles">Maximum cycles to execute before giving up</param>
    /// <returns>Information about why execution stopped and how many cycles ran</returns>
    DebugRunResult RunUntil(Action clockAction, Func<bool> breakCondition, int maxCycles = 1_000_000);
    
    /// <summary>
    /// Reset the clock state.
    /// </summary>
    void Reset();
}

/// <summary>
/// Result of executing a single clock cycle.
/// </summary>
public readonly struct ClockResult
{
    /// <summary>
    /// True if an emulated VBlank occurred during this cycle.
    /// This does NOT mean a GUI frame was rendered.
    /// </summary>
    public bool VBlankOccurred { get; init; }
    
    public ulong TotalCycles { get; init; }
}

/// <summary>
/// Result of a debug run-until operation.
/// </summary>
public class DebugRunResult
{
    public StopReason StopReason { get; set; }
    public int CyclesExecuted { get; set; }
    public ulong TotalCycles { get; set; }
}

public enum StopReason
{
    BreakpointHit,
    MaxCyclesReached,
    Cancelled
}
```

### SystemClock

```csharp
namespace Pandowdy.EmuCore;

/// <summary>
/// Tracks emulated system cycles and generates VBlank events at Apple IIe timing.
/// </summary>
/// <remarks>
/// This clock represents emulated time, not real time. VBlank events fire every
/// 17,063 cycles regardless of how fast the emulator actually runs. This keeps
/// the $C019 VBlank readable switch accurate from the emulated machine's perspective.
/// 
/// <para>
/// <strong>Decoupled from GUI rendering:</strong> The VBlank event here is purely
/// for emulation accuracy (readable I/O switch, timing-sensitive software). The GUI
/// independently samples frames at real-time 60 Hz via IRefreshTicker, completely
/// asynchronous from these emulated VBlank events.
/// </para>
/// </remarks>
public class SystemClock
{
    private ulong _cycles;
    private const ulong CyclesPerVBlank = 17063; // Apple IIe NTSC timing
    private const long VBlankBlackoutCycles = 4550;
    
    private ulong _nextVblankCycle = CyclesPerVBlank;
    private long _vblankBlackoutCounter = 0;
    
    /// <summary>
    /// Raised when VBlank occurs in emulated time (every 17,063 cycles).
    /// This does NOT indicate GUI frame rendering occurred.
    /// </summary>
    public event EventHandler? VBlank;
    
    public ulong Cycles => _cycles;
    
    /// <summary>
    /// True when in VBlank period (readable via $C019).
    /// Stays true for ~4550 cycles after VBlank event.
    /// This is emulated timing, not related to actual GUI rendering.
    /// </summary>
    public bool IsInVBlank => _vblankBlackoutCounter > 0;
    
    /// <summary>
    /// Advance the system clock by one cycle.
    /// Fires VBlank event and sets blackout period at appropriate emulated times.
    /// </summary>
    public void Tick()
    {
        _cycles++;
        
        // Check for VBlank (emulated timing)
        if (_cycles >= _nextVblankCycle)
        {
            _nextVblankCycle += CyclesPerVBlank;
            _vblankBlackoutCounter = VBlankBlackoutCycles;
            
            // Fire emulated VBlank event
            VBlank?.Invoke(this, EventArgs.Empty);
        }
        
        // Count down blackout period (affects $C019 reads)
        if (_vblankBlackoutCounter > 0)
        {
            _vblankBlackoutCounter--;
        }
    }
    
    public void Reset()
    {
        _cycles = 0;
        _nextVblankCycle = CyclesPerVBlank;
        _vblankBlackoutCounter = 0;
    }
}
```

## Implementation: AutomaticClockingController

```csharp
namespace Pandowdy.EmuCore;

/// <summary>
/// Clocking strategy for continuous automatic execution with optional throttling.
/// </summary>
public sealed class AutomaticClockingController : IClockingController
{
    private readonly SystemClock _systemClock;
    private readonly Stopwatch _throttleSw = Stopwatch.StartNew();
    private long _throttleCycles;
    
    public SystemClock SystemClock => _systemClock;
    public bool ThrottleEnabled { get; set; } = true;
    public double TargetHz { get; set; } = 1_023_000d;
    
    public AutomaticClockingController()
    {
        _systemClock = new SystemClock();
    }
    
    public ClockResult ExecuteCycle(Action clockAction)
    {
        bool wasInVBlank = _systemClock.IsInVBlank;
        
        // Tick system clock (tracks cycles, generates VBlank)
        _systemClock.Tick();
        
        // Execute the actual clock action (Bus.Clock())
        clockAction();
        
        // Track for throttling
        _throttleCycles++;
        
        if (ThrottleEnabled)
        {
            ThrottleOneCycle();
        }
        
        return new ClockResult
        {
            VBlankOccurred = !wasInVBlank && _systemClock.IsInVBlank,
            TotalCycles = _systemClock.Cycles
        };
    }
    
    public async Task RunAsync(Action clockAction, CancellationToken ct, double ticksPerSecond = 1000d)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ticksPerSecond);
        
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1.0 / ticksPerSecond));
        double cyclesPerTick = TargetHz / ticksPerSecond;
        double carry = 0.0;
        
        while (!ct.IsCancellationRequested)
        {
            if (ThrottleEnabled)
            {
                // Throttled execution - wait for timer
                try
                {
                    if (!await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                        break;
                }
                catch (OperationCanceledException) { break; }
                
                // Execute batch of cycles
                double want = cyclesPerTick + carry;
                int cycles = (int)want;
                carry = want - cycles;
                
                for (int i = 0; i < cycles; i++)
                {
                    ExecuteCycle(clockAction);
                }
            }
            else
            {
                // Fast-forward mode - no throttling
                const int FastBatch = 10_000;
                
                for (int i = 0; i < FastBatch; i++)
                {
                    ExecuteCycle(clockAction);
                    
                    if (ct.IsCancellationRequested)
                        break;
                }
                
                // Yield to prevent CPU starvation
                try
                {
                    await Task.Delay(0, ct);
                }
                catch (OperationCanceledException) { break; }
            }
        }
    }
    
    public DebugRunResult RunUntil(Action clockAction, Func<bool> breakCondition, int maxCycles = 1_000_000)
    {
        // Not typically used by automatic controller, but support it
        int cyclesExecuted = 0;
        
        while (cyclesExecuted < maxCycles)
        {
            ExecuteCycle(clockAction);
            cyclesExecuted++;
            
            if (breakCondition())
            {
                return new DebugRunResult
                {
                    StopReason = StopReason.BreakpointHit,
                    CyclesExecuted = cyclesExecuted,
                    TotalCycles = _systemClock.Cycles
                };
            }
        }
        
        return new DebugRunResult
        {
            StopReason = StopReason.MaxCyclesReached,
            CyclesExecuted = cyclesExecuted,
            TotalCycles = _systemClock.Cycles
        };
    }
    
    public void Reset()
    {
        _systemClock.Reset();
        _throttleCycles = 0;
        _throttleSw.Restart();
    }
    
    private void ThrottleOneCycle()
    {
        double expectedSec = _throttleCycles / TargetHz;
        double elapsedSec = _throttleSw.Elapsed.TotalSeconds;
        double leadSec = expectedSec - elapsedSec;
        
        if (leadSec <= 0) return;
        
        // Sleep for whole milliseconds
        int sleepMs = (int)(leadSec * 1000.0);
        if (sleepMs > 0)
        {
            Thread.Sleep(sleepMs);
        }
        
        // Spin-wait for sub-millisecond precision
        while (_throttleSw.Elapsed.TotalSeconds < expectedSec)
        {
            Thread.SpinWait(100);
        }
    }
    
    public void Dispose()
    {
        // Cleanup if needed
    }
}
```

## Implementation: DebugClockingController

```csharp
namespace Pandowdy.EmuCore;

/// <summary>
/// Clocking strategy for manual step-by-step debugging.
/// </summary>
public sealed class DebugClockingController : IClockingController
{
    private readonly SystemClock _systemClock;
    
    public SystemClock SystemClock => _systemClock;
    
    // Throttling not applicable in debug mode
    public bool ThrottleEnabled { get; set; } = false;
    public double TargetHz { get; set; } = 1_023_000d; // Unused but interface requires it
    
    public DebugClockingController()
    {
        _systemClock = new SystemClock();
    }
    
    public ClockResult ExecuteCycle(Action clockAction)
    {
        bool wasInVBlank = _systemClock.IsInVBlank;
        
        // Tick system clock
        _systemClock.Tick();
        
        // Execute the clock action
        clockAction();
        
        return new ClockResult
        {
            VBlankOccurred = !wasInVBlank && _systemClock.IsInVBlank,
            TotalCycles = _systemClock.Cycles
        };
    }
    
    public Task RunAsync(Action clockAction, CancellationToken ct, double ticksPerSecond = 1000d)
    {
        // Debug controller doesn't support continuous async execution
        throw new NotSupportedException(
            "DebugClockingController does not support continuous execution. " +
            "Use ExecuteCycle() for single-step or RunUntil() for breakpoint execution.");
    }
    
    public DebugRunResult RunUntil(Action clockAction, Func<bool> breakCondition, int maxCycles = 1_000_000)
    {
        int cyclesExecuted = 0;
        
        while (cyclesExecuted < maxCycles)
        {
            var result = ExecuteCycle(clockAction);
            cyclesExecuted++;
            
            if (breakCondition())
            {
                return new DebugRunResult
                {
                    StopReason = StopReason.BreakpointHit,
                    CyclesExecuted = cyclesExecuted,
                    TotalCycles = result.TotalCycles
                };
            }
        }
        
        return new DebugRunResult
        {
            StopReason = StopReason.MaxCyclesReached,
            CyclesExecuted = cyclesExecuted,
            TotalCycles = _systemClock.Cycles
        };
    }
    
    public void Reset()
    {
        _systemClock.Reset();
    }
    
    public void Dispose()
    {
        // Cleanup if needed
    }
}
```

## VA2M Integration

```csharp
public class VA2M : IDisposable
{
    private readonly IAppleIIBus _bus;
    private IClockingController _clockingController;
    private readonly IFrameGenerator _frameGenerator;
    private readonly ConcurrentQueue<Action> _pending = new();
    
    // ... other fields ...
    
    public VA2M(
        IEmulatorState stateSink,
        IFrameProvider frameSink,
        ISystemStatusProvider statusProvider,
        IAppleIIBus bus,
        MemoryPool memoryPool,
        IFrameGenerator frameGenerator,
        IClockingController clockingController) // NEW: Inject strategy
    {
        // ... validation ...
        
        _bus = bus;
        _clockingController = clockingController;
        _frameGenerator = frameGenerator;
        
        // Subscribe to emulated VBlank (for any logic that needs it)
        // Note: This does NOT trigger GUI frame rendering
        _clockingController.SystemClock.VBlank += OnEmulatedVBlank;
        
        // ... rest of initialization ...
    }
    
    /// <summary>
    /// Called when emulated VBlank occurs (every 17,063 cycles).
    /// This is NOT when GUI frames are rendered.
    /// </summary>
    private void OnEmulatedVBlank(object? sender, EventArgs e)
    {
        // Handle any emulated-time VBlank logic here
        // (e.g., flash timer toggle, timing-sensitive state updates)
        
        // Apply pending flash toggle at frame boundary for consistent rendering
        if (System.Threading.Interlocked.Exchange(ref _pendingFlashToggle, 0) != 0)
        {
            _sysStatusSink.Mutate(s => s.StateFlashOn = !s.StateFlashOn);
        }
        
        // Note: We do NOT render frames here
        // Frame rendering is GUI-driven via IRefreshTicker calling RenderFrameNow()
    }
    
    #region Clocking Control
    
    /// <summary>
    /// Switch to a different clocking strategy at runtime.
    /// </summary>
    public void SetClockingStrategy(IClockingController newController)
    {
        // Unsubscribe from old controller
        _clockingController.SystemClock.VBlank -= OnEmulatedVBlank;
        _clockingController.Dispose();
        
        // Switch to new controller
        _clockingController = newController;
        _clockingController.SystemClock.VBlank += OnEmulatedVBlank;
    }
    
    /// <summary>
    /// Execute a single clock cycle (delegates to strategy).
    /// </summary>
    public ClockResult Clock()
    {
        ProcessPending();
        
        var result = _clockingController.ExecuteCycle(() => _bus.Clock());
        
        PublishState();
        
        return result;
    }
    
    /// <summary>
    /// Run continuously (automatic mode).
    /// </summary>
    public Task RunAsync(CancellationToken ct, double ticksPerSecond = 1000d)
    {
        return _clockingController.RunAsync(
            clockAction: () =>
            {
                ProcessPending();
                _bus.Clock();
                PublishState();
            },
            ct: ct,
            ticksPerSecond: ticksPerSecond);
    }
    
    /// <summary>
    /// Run until breakpoint (debug mode).
    /// </summary>
    public DebugRunResult RunUntil(Func<bool> breakCondition, int maxCycles = 1_000_000)
    {
        return _clockingController.RunUntil(
            clockAction: () =>
            {
                ProcessPending();
                _bus.Clock();
                PublishState();
            },
            breakCondition: breakCondition,
            maxCycles: maxCycles);
    }
    
    /// <summary>
    /// Render a frame on-demand (called by GUI thread via IRefreshTicker).
    /// This is completely independent from emulated VBlank events.
    /// </summary>
    public void RenderFrameNow()
    {
        var renderContext = _frameGenerator.AllocateRenderContext();
        _frameGenerator.RenderFrame(renderContext);
    }
    
    #endregion
    
    public ulong SystemClock => _clockingController.SystemClock.Cycles;
    
    /// <summary>
    /// True when emulated VBlank is active (for $C019 reads).
    /// This does NOT mean a GUI frame is being rendered.
    /// </summary>
    public bool IsInVBlank => _clockingController.SystemClock.IsInVBlank;
    
    public bool ThrottleEnabled
    {
        get => _clockingController.ThrottleEnabled;
        set => _clockingController.ThrottleEnabled = value;
    }
    
    // ... rest of VA2M ...
}
```

## GUI Integration - IRefreshTicker

The GUI independently triggers frame rendering at real-time 60 Hz:

```csharp
// In MainWindowViewModel or similar:
public class MainWindowViewModel : ReactiveObject
{
    private readonly VA2M _emulator;
    private readonly IRefreshTicker _refreshTicker;
    
    public MainWindowViewModel(VA2M emulator, IRefreshTicker refreshTicker)
    {
        _emulator = emulator;
        _refreshTicker = refreshTicker;
        
        // GUI-driven frame rendering at real-time 60 Hz
        // Completely independent from emulator's VBlank events
        _refreshTicker.Stream
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ =>
            {
                // Sample current emulator state and render
                _emulator.RenderFrameNow();
            });
    }
}
```

### How It All Works Together

In **throttled mode** (normal play):
1. Emulator runs at ~1.023 MHz (throttled to real-time)
2. Emulated VBlank fires every 17,063 cycles (~60 Hz emulated time)
3. GUI samples frames every ~16.67ms (60 Hz real time)
4. They stay roughly synchronized because emulator is throttled

In **fast-forward mode**:
1. Emulator runs as fast as CPU allows (millions of cycles/sec)
2. Emulated VBlank fires thousands of times per real second
3. GUI still samples frames at 60 Hz real time
4. GUI sees "many frames have passed", renders current state

In **debug mode**:
1. Emulator advances only when stepped manually
2. Emulated VBlank fires only after stepping 17,063 times
3. GUI samples at 60 Hz or renders on-demand
4. Completely decoupled - render whenever needed

## Dependency Injection Setup

```csharp
// In Program.cs ConfigureServices:

// Emulator-side timing (decoupled from GUI)
services.AddSingleton<IClockingController, AutomaticClockingController>();

// GUI-side timing (decoupled from emulator)
services.AddSingleton<IRefreshTicker, AvaloniaRefreshTicker>(); // Lives in Pandowdy.UI

// VA2M uses IClockingController for emulated timing
// MainWindow uses IRefreshTicker for real-time rendering
// They communicate asynchronously via frame buffer sampling
```

## Benefits

### 1. Clean Separation
- VA2M doesn't know about throttling logic
- VA2M doesn't know about breakpoint logic
- Each strategy is self-contained

### 2. Easy Mode Switching
```csharp
// Switch to debug mode
var debugController = new DebugClockingController();
va2m.SetClockingStrategy(debugController);

// Single step
va2m.Clock();

// Run to breakpoint
var result = va2m.RunUntil(() => va2m.Bus.Cpu.PC == 0x1234);

// Switch back to automatic
var autoController = new AutomaticClockingController();
va2m.SetClockingStrategy(autoController);
await va2m.RunAsync(cancellationToken);
```

### 3. Testability
```csharp
// Mock clocking for tests
public class MockClockingController : IClockingController
{
    public List<Action> ExecutedCycles { get; } = new();
    
    public ClockResult ExecuteCycle(Action clockAction)
    {
        ExecutedCycles.Add(clockAction);
        clockAction();
        return new ClockResult { TotalCycles = (ulong)ExecutedCycles.Count };
    }
    
    // ... etc ...
}
```

### 4. Future Extensibility
```csharp
// Cycle-accurate clocking (per-instruction timing)
public class CycleAccurateClockingController : IClockingController { }

// Replay clocking (from saved state)
public class ReplayClockingController : IClockingController { }

// Network multiplayer clocking (synchronized)
public class NetworkSyncedClockingController : IClockingController { }
```

## Testing Strategy

Each controller can be tested independently:

```csharp
[Fact]
public void AutomaticController_Throttles_ToTargetHz()
{
    var controller = new AutomaticClockingController
    {
        ThrottleEnabled = true,
        TargetHz = 1_000_000 // 1 MHz for easy math
    };
    
    var sw = Stopwatch.StartNew();
    int executed = 0;
    
    for (int i = 0; i < 10_000; i++)
    {
        controller.ExecuteCycle(() => executed++);
    }
    
    sw.Stop();
    double expectedMs = (10_000.0 / 1_000_000.0) * 1000.0; // Should be ~10ms
    Assert.InRange(sw.ElapsedMilliseconds, expectedMs * 0.9, expectedMs * 1.1);
}

[Fact]
public void DebugController_StepsSingleCycle()
{
    var controller = new DebugClockingController();
    int executed = 0;
    
    var result = controller.ExecuteCycle(() => executed++);
    
    Assert.Equal(1, executed);
    Assert.Equal(1UL, result.TotalCycles);
}
```

## Files to Create

1. `Pandowdy.EmuCore/Interfaces/IClockingController.cs`
2. `Pandowdy.EmuCore/AutomaticClockingController.cs`
3. `Pandowdy.EmuCore/DebugClockingController.cs`
4. `Pandowdy.EmuCore/SystemClock.cs` (extracted from VA2MBus)
5. `Pandowdy.EmuCore.Tests/AutomaticClockingControllerTests.cs`
6. `Pandowdy.EmuCore.Tests/DebugClockingControllerTests.cs`

## Migration Path

1. Extract `SystemClock` from `VA2MBus`
2. Create `IClockingController` interface
3. Implement `AutomaticClockingController` (refactor existing VA2M logic)
4. Test automatic controller thoroughly
5. Refactor VA2M to use `IClockingController`
6. Implement `DebugClockingController` when debugger is ready

## Future Extensibility: Cycle-Accurate Rendering (Not Implemented)

### Current Design: "Good Enough" Architecture

The current architecture intentionally uses **simple throttling** rather than cycle-accurate frame synchronization, which is sufficient for 99% of use cases. The architecture provides extension points if cycle-accurate rendering becomes necessary in the future.

**Why the current approach is sufficient:**

1. **Throttled mode**: Emulator runs at ~1 MHz, VBlank coincidentally aligns with GUI at ~60 Hz
   - Result: Smooth rendering that "looks right"
   - Side effect: Not cycle-accurate, but visually indistinguishable

2. **Fast-forward mode**: Emulator runs at full speed, GUI samples at 60 Hz
   - Result: GUI shows latest state, works perfectly
   - Users expect this behavior in fast-forward

3. **Debug mode**: Manual stepping, render on-demand
   - Result: Frame rendering when needed
   - Cycle accuracy irrelevant when paused/stepping

### Extension Hooks for Future Cycle-Accurate Rendering

The Strategy pattern and DI architecture provide ready-to-use extension points:

#### Clocking Strategy Hooks

**Option 1: New `IClockingController` Implementation**

Create a dedicated cycle-accurate controller:
```csharp
public class CycleAccurateClockingController : IClockingController
{
    // Renders frame synchronously at VBlank
}
```

**Option 2: Decorator Pattern** (Recommended)

Wrap existing controller to add frame sync:
```csharp
public class FrameSyncDecorator : IClockingController
{
    private readonly IClockingController _inner;
    
    public ClockResult ExecuteCycle(Action clockAction)
    {
        var result = _inner.ExecuteCycle(clockAction);
        
        // If VBlank occurred, render frame synchronously
        if (result.VBlankOccurred)
        {
            var context = _frameGenerator.AllocateRenderContext();
            _frameGenerator.RenderFrame(context);
            _frameProvider.CommitWritable();
        }
        
        return result;
    }
}
```

Integration:
```csharp
var baseController = new AutomaticClockingController();
var syncedController = new FrameSyncDecorator(baseController, frameGenerator, frameProvider);
va2m.SetClockingStrategy(syncedController);
```

### Detailed Design Documentation

For complete implementation details, see:

**📄 `Cycle-Accurate-Rendering-Design.md`**

Comprehensive documentation covering:
- When cycle-accurate rendering would be needed
- Three clocking strategy options (new controller, decorator, per-instruction)
- Per-pixel metadata design
- Renderer implementation examples
- NTSC artifact generation with mode metadata
- Step-by-step implementation path
- Memory impact analysis (~210 KB increase per frame)
- Benefits and tradeoffs

### Design Decision Summary

**Date**: 2025-01-02  
**Status**: Current design deemed sufficient for foreseeable needs  
**Extension Points**: Documented and ready to use if needed  
- ✅ `IClockingController` - Swap strategies at runtime  
- ✅ `ExecuteCycle()` - Override to add frame sync  
- ✅ Decorator pattern - Wrap existing behavior  
- ✅ DI injection - No code changes to VA2M  
- ✅ **BitField extensibility** - `BitField16` → `BitField32` for per-pixel metadata  

**Review Trigger**: Visual artifacts in raster effects or timing-sensitive software  
**Path Forward**: See `Cycle-Accurate-Rendering-Design.md` for implementation guide

---

*This design provides a clean, extensible foundation for both normal operation and future debugging features.*
