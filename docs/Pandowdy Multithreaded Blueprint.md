# Pandowdy Multithreaded / Hybrid UI Blueprint

## 1) High-Level Goals & Scope
**Goal:** Transition from a single-threaded Avalonia application to a hybrid architecture: the CRT display remains imperative (polling) for maximal performance; auxiliary UI (debug state, logs, disassembly, emulator status) adopts ReactiveUI with DI-managed services. Prepare for a future move of the emulator core to a worker thread while keeping current behavior intact.

**Non-Goals:** Audio, theme/styling overhaul, replacing the CRT rendering loop with reactive patterns, premature full multithreading.

## 2) Architecture Overview (Current vs Proposed)
**Current State:**
- Single-threaded UI; emulator executes on UI thread.
- CRT control renders a `byte[]` framebuffer directly.
- Debug/log information likely written via `Debug.WriteLine` or ad-hoc mechanisms.
- Minimal or no DI; possible static access patterns.

**Proposed Component Map:**
- Core Emulator (still UI thread initially; later optional worker thread).
- Services (Singleton DI registrations):
  - `IFrameProvider` – holds current emulated video frame.
  - `IErrorProvider` / `ILogProvider` – emits structured log/error events.
  - `IEmulatorState` – exposes high-level execution state (PC, SP, cycles, current BASIC line, run/pause flags).
  - `IDisassemblyProvider` – provides decoded listing and supports range queries.
- CRT Control – polls `IFrameProvider.GetFrame()` imperatively.
- Reactive Panels – subscribe to service observables (`ReactiveObject`-based ViewModels).

**Data Flow:**
```
Emulator Core ? (updates) FrameProvider / ErrorProvider / StateProvider / DisassemblyProvider
          CRT Control ? (polls) FrameProvider
          Reactive Panels ? (subscribe) Providers (ObserveOn UI scheduler)
User Input ? ViewModels (Commands) ? Services (intents) ? Emulator Core (acts next cycle)
```

## 3) Services & DI Plan
Define interfaces and register them as Singletons.

### Interface Sketches (Illustrative Only)
```csharp
public interface IFrameProvider {
    byte[] GetFrame();       // returns copy or stable snapshot
    void UpdateFrame(byte[] source); // emulator pushes new frame
}

public interface IErrorProvider {
    IObservable<LogEvent> Events { get; }
    void Publish(LogEvent evt);
}

public interface IEmulatorState {
    IObservable<StateSnapshot> Stream { get; }
    StateSnapshot GetCurrent();
    void Update(StateSnapshot snapshot);
    void RequestPause();
    void RequestContinue();
    void RequestStep();
}

public interface IDisassemblyProvider {
    IObservable<DisassemblyUpdate> Updates { get; }
    Task<Line[]> QueryRange(AddressRange range);
    void Invalidate(AddressRange range);
    void SetHighlight(ushort pc);
}
```
**Lifetime Recommendations:**
- Providers: Singleton.
- ViewModels: Transient (per view) or Singleton for global dashboard.

**Thread-Safety (Future-Ready):**
- Frame: double-buffer or atomic reference swap (`Interlocked.Exchange`); `GetFrame()` returns a copy or stable snapshot.
- Streams: `BehaviorSubject` / `ReplaySubject` as needed; ensure publication marshals to UI thread in subscribers.
- Avoid coarse locks; use small critical sections for buffer swaps.

## 4) CRT Display Path (Imperative Polling)
- Keep current render timer / `OnRender` pattern.
- Replace direct framebuffer access with `IFrameProvider.GetFrame()`.
- Steps:
  1. Poll frame (fast copy).
  2. Write into `WriteableBitmap` or memory surface.
  3. Invalidate control for repaint.
- No reactive subscription for frames (prevents high-frequency observable overhead).
- Optimize frame copy (ArrayPool or persistent scratch array).

## 5) ReactiveUI Migration for State Panels
Target panels:
- Error/Log output.
- Disassembly listing (BASIC & machine-level hybrid later).
- Emulator status (PC, SP, cycles, current BASIC line, run/pause).
- Performance metrics (FPS, instructions per second).

### ViewModel Patterns
- Use `ReactiveObject` with `ObservableAsPropertyHelper` for derived values.
- Subscriptions inside `WhenActivated` blocks; dispose automatically.

Examples (conceptual):
```csharp
public class ErrorLogViewModel : ReactiveObject {
    public ReadOnlyObservableCollection<LogEvent> Logs { get; }
    // Internally: source list updated via buffered subscription:
    // errorProvider.Events.Buffer(TimeSpan.FromMilliseconds(100))...
}

public class EmulatorStateViewModel : ReactiveObject {
    public ushort PC { get; private set; }
    public int StackDepth { get; private set; }
    public ulong Cycles { get; private set; }
    public int? LineNumber { get; private set; }
}
```

**Schedulers:** Always `ObserveOn(AvaloniaScheduler.Instance)` before mutating UI-bound collections.
**Throttling:** PC & cycle updates – sample every 30–50 ms to reduce UI churn.
**Virtualization:** Disassembly listing uses virtualized panel (`ItemsRepeater`, DataGrid, or custom control) for large datasets.

## 6) Threading & Synchronization Strategy (Future-Ready)
- When moving emulator to a worker thread:
  - Providers receive updates from background ? UI subscribers apply `ObserveOn`.
  - FrameProvider: `UpdateFrame` executed by worker; `GetFrame` used by UI thread without blocking.
  - State updates atomic: new `StateSnapshot` replaces old; publish via `BehaviorSubject`.
- Avoid long blocking operations (e.g., disassembly refresh) on UI thread; perform asynchronously then publish results.
- Guarantee order: logs timestamped; state snapshots monotonic in `Cycles`.

## 7) Input Flow Integration
- Convert keyboard/menu interactions into `ReactiveCommand`s on a MainWindow or EmulatorControl ViewModel.
- Examples:
  - `PauseCommand` ? `emulatorState.RequestPause()`.
  - `StepCommand` ? `emulatorState.RequestStep()`.
  - `SetBreakpointCommand` ? modifies a breakpoint collection inside a BreakpointService (optional future service).
- UI toggles (verbosity level) update providers’ properties (e.g., hook table TargetLevel) and produce reactive side-effects.

## 8) Migration Sequence (Low-Risk Order)
1. Introduce DI container (e.g., `IServiceCollection` + build provider) in `App.axaml.cs`. Register empty provider implementations returning existing data.
2. Wrap framebuffer behind `IFrameProvider`; modify CRT control to call `GetFrame()`. Behavior unchanged.
3. Implement `IErrorProvider`; redirect existing logging (duplicate `Debug.WriteLine`) to `Publish()`. Add Reactive error panel.
4. Add `IEmulatorState`; update each clock to push a snapshot; create status panel ViewModel.
5. Implement `IDisassemblyProvider`; load initial listing; reactive highlight of current PC.
6. Harden providers for future multithreading (atomic swaps, buffering, throttling). App still single-threaded.
7. Optional: shift emulator loop to background `Task.Run`; verify CRT polling & reactive panels remain stable.

## 9) Interface Shapes (Illustrative – No Code Change)
```csharp
public record StateSnapshot(ushort PC, byte SP, ulong Cycles, int? LineNumber, bool IsRunning, bool IsPaused);
public record LogEvent(DateTime Timestamp, string Severity, string Message, ushort? PC = null);
public record DisassemblyUpdate(AddressRange Range, IReadOnlyList<Line> Lines);
public record Line(ushort Address, string BytesHex, string Mnemonic, string Comment);
public record AddressRange(ushort Start, ushort End);
```

## 10) Testing & Validation Plan
**Unit Tests:**
- FrameProvider: update & snapshot correctness; no mutation after retrieval.
- ErrorProvider: Publish emits; buffering logic preserves ordering.
- EmulatorState: sequential `Update` produces correct stream sequence.
- DisassemblyProvider: range queries & highlight updates.

**UI Smoke Tests:**
- CRT renders at existing FPS (measure before/after).
- Error panel updates on new log events.
- Status panel reflects PC changes & pause/resume.
- Disassembly highlight moves with PC.

**Performance Checks:**
- Frame copy < 1 ms.
- Log bursts (100 events) processed without UI stutter (buffering interval tuning).
- Virtualized listing maintains scroll performance.

## 11) Risks & Mitigations
| Risk | Mitigation |
|------|------------|
| UI churn from high-frequency logs | Buffer/throttle; severity filtering & adjustable verbosity |
| Race conditions when multithreaded | Atomic swaps; immutable snapshots; `ObserveOn` UI scheduler |
| DI misconfiguration (multiple instances) | Integration tests ensuring singletons | 
| Large disassembly memory/CPU footprint | Virtualization & lazy range decoding |
| Debug verbosity impacting performance | Adjustable `TargetLevel`; degrade gracefully |

## 12) Milestones & Acceptance Criteria
**Milestone A:** DI scaffolding + FrameProvider in place. CRT unchanged, pulls through provider.  
Acceptance: No regression; identical visual output.

**Milestone B:** ErrorProvider + reactive log panel live; stable performance.  
Acceptance: Logs visible; UI responsive under burst.

**Milestone C:** EmulatorState reactive; status panel updates smoothly.  
Acceptance: Indicators accurate; no flicker.

**Milestone D:** Disassembly panel reactive with highlight; scroll remains fluid.  
Acceptance: Highlight tracks execution; list virtualization confirmed.

**Milestone E:** Providers hardened for thread-safety; still single-threaded.  
Acceptance: Tests pass; no race indications.

**Milestone F (Optional):** Emulator worker thread introduced; CRT & panels intact.  
Acceptance: Stable multi-thread behavior; UI thread not blocked.

**Overall Acceptance:** CRT maintains target FPS; reactive UI panels provide accurate, timely data; architecture ready for future audio & deeper debugging features.

## Additional Notes
- Keep CRT path lean; avoid reactive overhead in rendering loop.
- Place provider implementations in `Pandowdy.Core` (logic-only); ViewModels & Views in `Pandowdy.UI`.
- Use `AvaloniaScheduler.Instance` (or `RxApp.MainThreadScheduler`) for UI marshaling.
- Audio service (future): follow same pattern (`ISoundBufferProvider`). Not in current scope.

## Future Extension Placeholders
- BreakpointService (`IBreakpointService`) integrating with emulator state and disassembly provider.
- TraceLevel adjustments exposed via settings panel binding to provider `TargetLevel`.
- Profiling hooks for cycle timing once emulator is off UI thread.

---
**Implementation Reminder:** This document is a blueprint only. Do not alter existing functional CRT code until the specified migration step calls for wrapping it via `IFrameProvider`. Preserve legacy commented hook regions and instrumentation notes during all refactors.
