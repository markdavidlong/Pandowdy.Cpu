# PowerCycle Blueprint — VA2M Power Lifecycle

**Status:** Phases 1–6 complete (IRestartable + RestartCollection + flat priority ordering); Phases 7–9 TODO (PowerState, DI facade, integration)
**Branch:** `CoreUpdates` (to be created from `skillet`)  
**Date:** 2026-01  
**Depends on:** Skillet project system (mount/eject/store already implemented)  
**Blocked by this:** Skillet project switching (New/Open/Close project requires cold boot)

---

## 1. Problem Statement

The emulator currently has only a warm `Reset()` that mimics the CPU reset line
(Ctrl+Reset). It clears the keyboard, loads the reset vector, and resets
throttle counters — but **nothing else**:

- 128KB RAM retains previous contents
- Soft switches retain previous state (memory mapping, video modes)
- Language Card banking state persists
- Expansion card internal state persists (motor state, head position, mounted disk tracking)
- Game controller button states persist

This is correct for Ctrl+Reset but insufficient for **project switching**, where
opening a new `.skillet` file must present the user with a clean Apple IIe as if
the power switch was cycled. Without a cold boot, stale RAM, soft switch settings,
and card state from the previous project bleed through.

### 1.1 IDiskImageStore Stale Reference

A secondary issue: the DI container captures `IDiskImageStore` as a singleton
resolved from `projectManager.CurrentProject` at construction time (Program.cs
line 90–94). When the user opens a new project, controller cards still hold a
reference to the **old** project's store. This must be fixed for project switching
to work, but is an orthogonal DI wiring problem documented in §7.

---

## 2. Design: Three Operations

The design introduces three distinct operations that flow through the same
subsystem tree, with increasing scope:

| Operation | Analogy | Memory | CPU | Soft Switches | Cards | Clock Cycles |
|-----------|---------|--------|-----|---------------|-------|--------------|
| **Reset()** | Ctrl+Reset | Untouched | Reset vector | Untouched | Notified (warm) | Continue |
| **Restart()** | Power cycle | Cleared to 0 | Reset vector | Power-on defaults | Cold init | Continue |
| **PowerOff()** | Flip switch off | Frozen (inspectable) | Frozen | Frozen | Frozen | Paused (reuses F5) |

**PowerOn()** triggers `Restart()` then resumes via existing continue mechanism.

> **Note:** PowerOff is functionally identical to Pause. The `_isPoweredOn` flag
> is a cosmetic hint for the GUI (blank display, suppress motor indicators).
> Since every exit from powered-off state goes through `PowerOn()` →
> `Restart()`, stale subsystem state can never leak into active emulation.

### 2.1 Key Design Principles

1. **PowerOff is effectively Pause with a state flag.** Subsystems don't know
   about it — VA2M simply stops accepting clock cycles (identical to the
   existing F5 pause). The only difference is a `_isPoweredOn` flag that the
   GUI can observe to apply cosmetic effects (blank screen, suppress drive
   motor indicators). The subsystem state is preserved for debugger inspection.

   **Rationale:** Since the only exits from powered-off state are `PowerOn()`
   (which calls `Restart()`, clearing everything), loading a new project
   (which also calls `PowerOn()`), or exiting the application, there is no
   scenario where stale motor/drive/switch state from the powered-off machine
   leaks into active emulation. The GUI simply ignores hardware status when
   `_isPoweredOn` is false.

2. **Restart() is a superset of Reset().** Every subsystem that has `Reset()`
   also gets `Restart()`. `Restart()` does everything `Reset()` does **plus**
   clears memory/state to power-on defaults.

3. **Flat collection, not a tree.** Unlike `Reset()` which uses tree-based
   dispatch (`VA2M` → `Bus` → `AddressSpaceController` → subsystems),
   `Restart()` uses a flat `RestartCollection` that iterates all registered
   `IRestartable` components ordered by `[Capability]` priority. Components
   tagged with `[Capability(typeof(IRestartable))]` are auto-discovered by
   `CapabilityAwareServiceCollection`; factory-created objects (e.g., cards)
   are registered dynamically by `CardFactory`. Order-sensitive components
   (e.g., `VA2MBus` which must read the reset vector after soft switches are
   at power-on defaults) use `priority:` to run later.

4. **Disk images are NOT ejected by Restart().** `Restart()` is a hardware
   power cycle — disks stay in drives. Eject is a project-level operation
   handled by `CloseProjectInternalAsync()` before `Restart()` is called.

5. **The emulator core starts powered-off.** The preferred and canonical
   initial state is `_isPoweredOn = false`. The GUI (or any orchestrating
   layer) is responsible for calling `PowerOn()` when it is ready — either
   directly (e.g., the user clicks a power switch or the window finishes
   loading) or indirectly (e.g., opening a `.skillet` project file triggers
   `PowerOn()` as part of its lifecycle). This guarantees that:
   - All GUI subscriptions (`IPowerStateProvider.Stream`, status panels,
     display control) are fully wired before the first clock cycle executes.
   - The emulator never silently starts running in the background while the
     GUI is still initializing.
   - Future features (e.g., a physical "power switch" UI element, or
     command-line `--no-autostart` flag) work naturally without special-casing.
   - The startup sequence is deterministic: the first `true` published on
     `IPowerStateProvider.Stream` always corresponds to an explicit user or
     system action, never an implicit side effect of construction.

---

## 3. Restart Flow

### 3.1 RestartCollection — Flat Priority-Ordered Iteration

`VA2M.DoRestart()` calls `RestartCollection.RestartAll()`, which iterates **all**
registered `IRestartable` components ordered by the `priority` parameter of their
`[Capability(typeof(IRestartable), priority: N)]` attribute. Lower numbers run
first (negatives allowed). Components without the attribute default to priority 0.

```
VA2M.DoRestart()                         // Enqueued on emulator thread
  └─ _restarters.RestartAll()            // Flat iteration, ordered by priority
       │
       │  Priority 0 (default) — all run before VA2MBus:
       ├─ SoftSwitches.Restart()         // All switches → false (ROM active)
       ├─ SystemRamSelector.Restart()    // Clear main 48K + aux 48K
       ├─ LanguageCard.Restart()         // Clear main 16K + aux 16K
       ├─ QueuedKeyHandler.Restart()     // Clear latch + queue
       ├─ SimpleGameController.Restart() // Release all buttons
       ├─ CpuClockingCounters.Restart()  // Zero cycle counters + VBlank state
       ├─ Slots.Restart()               // Cold-init each installed card
       │   └─ each card.Restart()        // Card-specific cold init
       ├─ AddressSpaceController.Restart()  // No-op (children are independent)
       ├─ SystemIoHandler.Restart()      // No-op (SoftSwitches is independent)
       ├─ (factory-registered cards)     // DiskIIControllerCard, etc.
       │
       │  Priority 100 — runs after soft switches ensure ROM is active:
       └─ VA2MBus.Restart()             // _cpu.Reset(this) — reads reset vector

  └─ Reset throttle/perf state           // VA2M-internal, not IRestartable
```

### 3.2 Discovery and Registration

- **DI auto-discovery:** Classes tagged with `[Capability(typeof(IRestartable))]`
  are automatically registered as `IRestartable` services by
  `CapabilityAwareServiceCollection`. The `RestartCollection` constructor
  receives `IEnumerable<IRestartable>` from DI.
- **Factory registration:** `CardFactory` accepts `RestartCollection` and calls
  `Register()` for each card instance it creates (since DI only knows about
  prototype instances, not the cloned/installed instances).
- **Dynamic unregistration:** `Unregister()` is available for card removal.

### 3.3 PowerOff / PowerOn (Unchanged)

```
VA2M.PowerOff()                         // Effectively Pause + publish
  ├─ _emuState.RequestPause()           // Reuse existing pause mechanism
  ├─ _isPoweredOn = false               // Private flag
  └─ _powerState.SetPoweredOn(false)    // Publish via BehaviorSubject → GUI reacts

VA2M.PowerOn()                          // Restart + publish + resume
  ├─ Restart()                          // Full cold boot (clears everything)
  ├─ _isPoweredOn = true                // Private flag
  ├─ _powerState.SetPoweredOn(true)     // Publish via BehaviorSubject → GUI reacts
  └─ _emuState.RequestContinue()        // Resume execution via existing mechanism

Startup (Program.cs / MainWindow)
  └─ _isPoweredOn starts as false       // Machine is OFF until user/GUI triggers PowerOn()
     (GUI has time to wire subscriptions before first PowerOn message)
```

---

## 4. Subsystem Changes

### 4.0 IRestartable, CapabilityAttribute, and RestartCollection

All subsystems that participate in cold-boot `Restart()` implement a common
`IRestartable` interface. Components are discovered via DI using the
`[Capability(typeof(IRestartable))]` attribute and collected into a
`RestartCollection` that provides priority-ordered batch restart.

The `CapabilityAttribute` accepts an optional `priority` parameter (default 0).
`RestartCollection.RestartAll()` sorts by ascending priority — lower numbers
first, negatives allowed. This replaces tree-based dispatch with declarative
ordering: order-sensitive components simply declare a higher priority.

```csharp
// CapabilityAttribute — drives DI auto-discovery and restart ordering
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class CapabilityAttribute(Type interfaceType, int priority = 0) : Attribute
{
    public int Priority { get; } = priority;
    public Type InterfaceType { get; } = interfaceType;
}
```

**Priority assignments:**

| Priority | Components | Rationale |
|----------|-----------|----------|
| 0 (default) | SoftSwitches, SystemRamSelector, LanguageCard, Slots, QueuedKeyHandler, SimpleGameController, CpuClockingCounters, AddressSpaceController, SystemIoHandler | No ordering dependencies among themselves |
| 100 | VA2MBus | Must run after SoftSwitches (HighRead=false → ROM active) so `_cpu.Reset(this)` reads the correct reset vector |

```csharp
/// <summary>
/// Defines a component that can be restored to its initial power-on state.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Contract:</strong> <see cref="Restart"/> restores the component to the
/// same state it had immediately after construction — all mutable state cleared to
/// power-on defaults. This is more thorough than <c>Reset()</c>, which performs a
/// warm reset (Ctrl+Reset) and may preserve memory contents, switch states, etc.
/// </para>
/// <para>
/// <strong>Relationship to Reset():</strong> Components that have both <c>Reset()</c>
/// and <c>Restart()</c> follow this rule: <c>Restart()</c> is a superset of
/// <c>Reset()</c>. Everything <c>Reset()</c> does, <c>Restart()</c> also does, plus
/// clearing memory and restoring power-on defaults.
/// </para>
/// <para>
/// <strong>Batch Usage:</strong> Container components (e.g., <see cref="Slots"/>)
/// can iterate over <c>IRestartable</c> children without knowing their concrete types.
/// </para>
/// </remarks>
public interface IRestartable
{
    /// <summary>
    /// Restores this component to its initial power-on state (cold boot).
    /// </summary>
    void Restart();
}
```

**Location:** `Pandowdy.EmuCore/Interfaces/IRestartable.cs`

**Adoption:** Existing interfaces that gain `Restart()` should extend
`IRestartable` rather than declaring `Restart()` independently:

```csharp
// ICard already has Reset(), now also extends IRestartable
public interface ICard : IConfigurable, IRestartable { ... }

// ISlots gains Restart() via IRestartable
public interface ISlots : IPandowdyMemory, IConfigurable, IRestartable { ... }

// ISystemIoHandler gains Restart() via IRestartable
public interface ISystemIoHandler : IRestartable { ... }

// IAppleIIBus gains Restart() via IRestartable
public interface IAppleIIBus : IRestartable { ... }

// IDiskIIDrive gains Restart() via IRestartable
public interface IDiskIIDrive : IRestartable { ... }

// Standalone subsystem interfaces
public interface ISystemRamSelector : IPandowdyMemory, IRestartable { ... }
public interface ILanguageCard : IPandowdyMemory, IRestartable { ... }
public interface IKeyboardSetter : IRestartable { ... }
public interface IGameControllerStatus : IRestartable { ... }
```

Classes that are not exposed through a restartable interface but still
participate in the tree (e.g., `AddressSpaceController`) implement
`IRestartable` directly on the class.

### 4.1 ISystemRam / MemoryBlock

**Current state:** No `Restart()` or `Clear()` method.

**Change:** Add `Clear()` to `ISystemRam` interface and implement in `MemoryBlock`:

```csharp
// ISystemRam (new method)
void Clear();

// MemoryBlock implementation
public void Clear() => Array.Clear(_data);
```

### 4.2 SystemRamSelector

**Current state:** No `Reset()` or `Restart()`. Pure router — reads soft switch
state from `ISystemStatusProvider` on every access.

**Change:** Add `Restart()` that clears both 48KB RAM banks:

```csharp
public void Restart()
{
    _mainRam.Clear();
    _auxRam?.Clear();
}
```

No soft switch reset needed here — `SystemIoHandler.Restart()` handles that via
`SoftSwitches.ResetAllSwitches()`, and the router reads state dynamically.

### 4.3 LanguageCard

**Current state:** No `Reset()` or `Restart()`. Pure router that reads banking
state from `ISystemStatusProvider`.

**Change:** Add `Restart()` that clears both 16KB RAM banks:

```csharp
public void Restart()
{
    _mainRam.Clear();
    _auxRam?.Clear();
}
```

Banking state (HighRead, HighWrite, Bank1, PreWrite) is reset by
`SoftSwitches.ResetAllSwitches()` which sets all switches to false — this means
HighRead=false → ROM active, which is the correct power-on default.

### 4.4 SoftSwitches

**Current state:** Already has `ResetAllSwitches()` that clears all switches to
false and flows through `SystemStatusProvider`. This IS the power-on default.

**Change:** Implements `IRestartable` directly with
`[Capability(typeof(IRestartable))]`. `Restart()` delegates to
`ResetAllSwitches()`. SoftSwitches is a first-class citizen in the flat
`RestartCollection` — it is not called through `SystemIoHandler`'s tree:

```csharp
[Capability(typeof(IRestartable))]
public sealed class SoftSwitches : IRestartable
{
    public void Restart() => ResetAllSwitches();
}
```

This ensures ROM is active (HighRead=false) before `VA2MBus` (priority 100)
resets the CPU and reads the reset vector.

### 4.5 SystemIoHandler

**Current state:** Has `Reset()` that calls `_softSwitches.ResetAllSwitches()`.

**Change:** `Restart()` is a **no-op**. `SoftSwitches` is independently
restartable via `RestartCollection`, so `SystemIoHandler` does not need to
delegate to it during cold boot. The handler is stateless beyond the soft
switches it wraps. Keyboard and game controller are also independently
restartable.

```csharp
public void Restart()
{
    // No-op: SoftSwitches is independently restartable via RestartCollection.
    // SystemIoHandler has no additional state to clear.
}
```

**Note:** `Reset()` still calls `SoftSwitches.ResetAllSwitches()` for warm-reset
(Ctrl+Reset) which uses the tree-based call path. Cold boot uses the flat
`RestartCollection` path instead.

### 4.6 ISystemIoHandler Interface

**Change:** Extend `IRestartable` — `Restart()` comes from the interface:

```csharp
public interface ISystemIoHandler : IRestartable
{
    // ... existing members ...
    void Reset();
    // Restart() inherited from IRestartable
}
```

### 4.7 AddressSpaceController

**Current state:** Has `Reset()` that calls `_slots.Reset()` + `_io.Reset()`.
Does NOT clear RAM or language card.

**Change:** `Restart()` is a **no-op**. All subsystems (SystemRamSelector,
SoftSwitches, Slots, LanguageCard) are independently restartable via
`RestartCollection`. AddressSpaceController is stateless beyond references to
its children.

```csharp
public void Restart()
{
    // No-op: children are independently restartable via RestartCollection.
}
```

### 4.8 ISlots / Slots

**Current state:** Has `Reset()` that calls `card.Reset()` on each slot.

**Change:** `ISlots` extends `IRestartable`. Implementation iterates cards
via `IRestartable` — no casting or card-specific knowledge needed:

```csharp
// ISlots extends IRestartable
public interface ISlots : IPandowdyMemory, IConfigurable, IRestartable { ... }

// Slots implementation
public void Restart()
{
    foreach (SlotNumber s in Enum.GetValues<SlotNumber>())
    {
        if (s == SlotNumber.Unslotted) { continue; }
        // ICard extends IRestartable, so this works without casting
        GetCardIn(s).Restart();
    }
}
```

### 4.9 ICard

**Current state:** Has `Reset()` with contract: "preserve mounted disk images."

**Change:** `ICard` extends `IRestartable`. The `Restart()` contract is
inherited from `IRestartable`; the card-specific docs clarify disk behavior:

```csharp
public interface ICard : IConfigurable, IRestartable
{
    // ... existing members (Reset, ReadIO, WriteIO, etc.) ...

    // Restart() inherited from IRestartable.
    // Card-specific contract: clears all internal state (controller registers,
    // motor state, head position). Mounted disk images are NOT ejected —
    // disk ejection is a project-level operation managed by the UI layer
    // before Restart() is called.
}
```

### 4.10 NullCard

**Change:** `Restart()` is a no-op (same as `Reset()`).

### 4.11 DiskIIControllerCard

**Current state:** `Reset()` presumably stops motor and resets some controller
state but preserves mounted disks and head position.

**Change:** `Restart()` does full cold init:

```csharp
public void Restart()
{
    // Stop motor immediately (no delayed-off)
    _motorState = MotorState.Off;
    
    // Reset head to track 0 for all drives
    foreach (var drive in _drives)
    {
        drive.Restart();  // Head → track 0, clear phase magnets
    }
    
    // Reset controller registers
    _selectedDrive = 0;
    _dataLatch = 0;
    _writeMode = false;
    
    // Clear sequencer state
    _sequencerState = 0;
    
    // Refresh drive status to reflect cold state
    RefreshAllDriveStatus();
}
```

**Note:** Mounted disks (`_mountedDiskImageIds`) are NOT cleared. Disk ejection
happens at the project level via `EjectAllDisksMessage` before `Restart()`.

### 4.12 IDiskIIDrive and Implementations

**`IDiskIIDrive`** already declares `Reset()`. It now also extends `IRestartable`:

```csharp
public interface IDiskIIDrive : IRestartable { ... }
```

Four classes implement `IDiskIIDrive` and each needs `Restart()`:

**`DiskIIDrive`** (concrete drive) — resets mechanical state to power-on:

```csharp
public void Restart()
{
    _currentTrack = 0;
    _headPosition = 0;
    _phaseMagnets = 0;
    // Disk media stays inserted (if any)
}
```

**`NullDiskIIDrive`** (empty slot placeholder) — no-op:

```csharp
public void Restart() { } // No state to clear
```

**`DiskIIStatusDecorator`** (publishes drive state to `IDiskStatusMutator`) —
delegates to inner drive then syncs status to reflect cold state. The existing
`SyncStatus()` call is already correct:

```csharp
public void Restart()
{
    _innerDrive.Restart(); // Resets head to track 0, clears phase magnets
    SyncStatus();          // Republishes cleared state to IDiskStatusMutator
}
```

**`DiskIIDebugDecorator`** (debug logging wrapper) — delegates transparently:

```csharp
public void Restart() => _innerDrive.Restart();
```

**Decorator call chain** — `DiskIIControllerCard` holds the outermost decorator.
Calling `drive.Restart()` on the outermost layer propagates inward:
```
DiskIIDebugDecorator.Restart()
  → DiskIIStatusDecorator.Restart()   // syncs status after
      → DiskIIDrive.Restart()          // resets track 0, phases
```

### 4.12a NoSlotClockIoHandler

**Current state:** `NoSlotClockIoHandler` is a decorator on `ISystemIoHandler`
that implements the Dallas DS1216 No-Slot Clock protocol. Its `Reset()` resets
the NSC state machine (pattern-match state, bit index, clock data accumulator)
but deliberately **preserves `_timeOffsetTicks`** — modelling the battery-backed
clock hardware that retains the time across power cycles.

**Change:** `Restart()` is identical in behaviour to `Reset()` — the time offset
survives both warm reset and power cycle, just as real battery-backed hardware
would:

```csharp
public void Restart()
{
    _downstream.Restart();          // Restart the wrapped ISystemIoHandler
    _state = NscState.Matching;
    _bitIndex = 0;
    _patternAccumulator = 0;
    _clockData = 0;
    // _timeOffsetTicks preserved intentionally — battery-backed RTC
}
```

### 4.13 IKeyboardSetter / QueuedKeyHandler

**Current state:** Has `ResetKeyboard()` that clears latch and queue.

**Change:** Add `Restart()` that delegates to `ResetKeyboard()` (equivalent for
this component):

```csharp
public void Restart() => ResetKeyboard();
```

### 4.14 IGameControllerStatus / SimpleGameController

**Current state:** Has `SetButton()` for individual buttons. No bulk reset.

**Change:** Add `Restart()` that releases all buttons:

```csharp
public void Restart()
{
    SetButton(0, false);
    SetButton(1, false);
    SetButton(2, false);
}
```

### 4.14a CpuClockingCounters

**Current state:** Has `Reset()` that zeros cycle counters and VBlank state.

**Change:** Implements `IRestartable` with `[Capability(typeof(IRestartable))]`.
`Restart()` delegates to `Reset()` — the counter state is identical for warm
and cold boot:

```csharp
[Capability(typeof(IRestartable))]
public sealed class CpuClockingCounters : IRestartable
{
    public void Restart() => Reset();
}
```

### 4.14b CardFactory + RestartCollection

**Problem:** DI auto-discovers `IRestartable` on **prototype** card instances
(registered at startup). But `CardFactory.GetCardWithId()` clones prototypes
to create the **installed** instances — DI never sees those clones.

**Solution:** `CardFactory` accepts `RestartCollection` and calls `Register()`
for each factory-created card that implements `IRestartable`:

```csharp
public class CardFactory(
    IEnumerable<ICard> cards,
    IDiskImageStore diskImageStore,
    RestartCollection restartCollection) : ICardFactory
{
    private ICard CreateCardInstance(ICard prototype)
    {
        var card = prototype switch
        {
            DiskIIControllerCard diskCard => diskCard.CreateWithStore(_diskImageStore),
            _ => prototype.Clone()
        };

        if (card is IRestartable restartable)
        {
            _restartCollection.Register(restartable);
        }

        return card;
    }
}
```

This ensures factory-created cards participate in `RestartCollection.RestartAll()`
without manual registration in `Program.cs`.

### 4.15 VA2MBus

**Current state:** Has `Reset()` that calls `_addressSpace.Reset()` +
`_cpu.Reset()` + `_clockCounters.Reset()`.

**Change:** `Restart()` **only resets the CPU**. All other subsystems
(AddressSpaceController, CpuClockingCounters) are independently restartable
via `RestartCollection`. The bus uses `priority: 100` to ensure it runs after
all default-priority components (especially SoftSwitches, which must set
HighRead=false so ROM is active for the reset vector read):

```csharp
[Capability(typeof(IRestartable), priority: 100)]
public sealed class VA2MBus : IAppleIIBus, IDisposable
{
    public void Restart()
    {
        ThrowIfDisposed();
        _cpu.Reset(this);  // Load reset vector from ROM ($FFFC/$FFFD)
    }
}
```

The CPU is not an `IRestartable` participant — it is an external dependency
that uses the same `Reset(bus)` call for both warm and cold boot. The bus must
perform this step because the CPU needs a bus reference to read the reset
vector.

### 4.16 VA2M

**Current state:** Has `Reset()` that enqueues keyboard reset + `Bus.Reset()` +
throttle/perf counter reset. Starts running immediately when `RunAsync` is called.

**Change:** Add `DoRestart()`, `PowerOff()`, `PowerOn()`. Start powered-off.
Publish power state via `IPowerStateMutator` (never polled by GUI).

`DoRestart()` delegates to `RestartCollection.RestartAll()`, which iterates
all registered `IRestartable` components in priority order. VA2M itself
implements `IRestartable.Restart()` as an explicit interface implementation
that delegates to `DoRestart()` (needed because `IKeyboardSetter : IRestartable`
propagates up through `IEmulatorCoreInterface`).

```csharp
private readonly RestartCollection _restarters;

public void DoRestart()
{
    Enqueue(() =>
    {
        _restarters.RestartAll();  // Flat iteration, priority-ordered

        // Reset throttle and performance state (VA2M-internal, not IRestartable)
        _throttleCycles = 0;
        _throttleSw.Restart();
        _perfLastCycles = 0;
        _perfLastReportTicks = _perfSw.ElapsedTicks;
        _throttleErrorAccumulator = 0;
        _throttleLastError = 0;
        _adaptiveSpinWaitIterations = 100;
    });
}

void IRestartable.Restart() => DoRestart();
```

`IEmulatorCoreInterface` exposes `DoRestart()` (not `Restart()` directly)
to avoid ambiguity with the `IRestartable.Restart()` inherited through the
interface chain.

**PowerOff / PowerOn / Startup:**

```csharp
private volatile bool _isPoweredOn = false; // Starts OFF

internal bool IsPoweredOn => Volatile.Read(ref _isPoweredOn);

public void PowerOff()
{
    _emuState.RequestPause();                // Reuse existing pause mechanism
    Volatile.Write(ref _isPoweredOn, false);
    _powerState.SetPoweredOn(false);         // Push notification → GUI reacts
}

public void PowerOn()
{
    DoRestart();                             // Full cold boot via RestartCollection
    Volatile.Write(ref _isPoweredOn, true);
    _powerState.SetPoweredOn(true);          // Push notification → GUI reacts
    _emuState.RequestContinue();             // Resume via existing mechanism
}
```

The emulator starts powered-off (`_isPoweredOn = false`). The GUI subscribes to
`IPowerStateProvider.Stream` during construction and immediately receives `false`
via the `BehaviorSubject`. When the user (or `InitialStartup()`) calls
`PowerOn()`, the provider publishes `true` and the GUI reacts.

**No RunAsync/Clock gate changes needed.** PowerOff reuses the existing
pause mechanism. PowerOn reuses `RequestContinue()`.

**GUI receives push notifications** via `IPowerStateProvider.Stream`:
- `false` → blank/dim the Apple II display, suppress motor indicators
- `true` → restore normal display, enable status panels

### 4.17 PowerStateProvider (New Service)

**Purpose:** Push-based power state notification, following the established
`DiskStatusProvider` pattern (`BehaviorSubject<T>` + split read/write interfaces).

**Interfaces:**

```csharp
/// <summary>
/// Read-only interface for observing emulator power state.
/// </summary>
/// <remarks>
/// The GUI subscribes to <see cref="Stream"/> during construction.
/// BehaviorSubject guarantees the subscriber immediately receives the current
/// state (false at startup), then all subsequent transitions.
/// </remarks>
public interface IPowerStateProvider
{
    /// <summary>
    /// Gets the current power state synchronously (utility/debug use only).
    /// The GUI should subscribe to <see cref="Stream"/> instead of polling.
    /// </summary>
    bool IsPoweredOn { get; }

    /// <summary>
    /// Observable stream of power state changes. Emits true on PowerOn,
    /// false on PowerOff. Uses BehaviorSubject — new subscribers immediately
    /// receive the current state.
    /// </summary>
    IObservable<bool> Stream { get; }
}

/// <summary>
/// Write interface for updating power state (used only by VA2M).
/// </summary>
public interface IPowerStateMutator : IPowerStateProvider
{
    /// <summary>
    /// Publishes a power state change to all subscribers.
    /// </summary>
    void SetPoweredOn(bool value);
}
```

**Implementation:**

```csharp
public sealed class PowerStateProvider : IPowerStateMutator
{
    private readonly BehaviorSubject<bool> _subject;

    public PowerStateProvider()
    {
        _subject = new BehaviorSubject<bool>(false); // Starts powered OFF
    }

    public bool IsPoweredOn => _subject.Value;
    public IObservable<bool> Stream => _subject;

    public void SetPoweredOn(bool value) => _subject.OnNext(value);
}
```

**DI registration (Program.cs):**

```csharp
services.AddSingleton<PowerStateProvider>();
services.AddSingleton<IPowerStateProvider>(sp => sp.GetRequiredService<PowerStateProvider>());
services.AddSingleton<IPowerStateMutator>(sp => sp.GetRequiredService<PowerStateProvider>());
```

**GUI subscription pattern (e.g., MainWindowViewModel or Apple2Display):**

```csharp
// In constructor or WhenActivated:
_powerState.Stream
    .ObserveOn(RxApp.MainThreadScheduler)
    .Subscribe(poweredOn =>
    {
        if (!poweredOn)
        {
            // Blank display, suppress motor indicators, show "Power Off"
        }
        else
        {
            // Restore normal display, enable status panels
        }
    });
```

### 4.18 IEmulatorCoreInterface

**Change:** Add `DoRestart()`, `PowerOff()`, `PowerOn()` to the interface.
`DoRestart()` is used instead of `Restart()` because the interface inherits
`IRestartable.Restart()` through `IKeyboardSetter`, and the two have different
semantics (`DoRestart` enqueues a full `RestartCollection.RestartAll()` batch;
`IRestartable.Restart()` is the per-component callback).
Remove `IsPoweredOn` property — the GUI subscribes to `IPowerStateProvider`
instead of reading from the core interface:

```csharp
public interface IEmulatorCoreInterface : IKeyboardSetter
{
    // ... existing members ...

    /// <summary>
    /// Cold boot: iterates RestartCollection in priority order, clearing all
    /// RAM, resetting soft switches, cold-initing cards, and loading the CPU
    /// reset vector. Equivalent to a power cycle.
    /// </summary>
    void DoRestart();

    /// <summary>
    /// Pauses emulation and publishes PoweredOff via IPowerStateProvider.
    /// Reuses existing pause mechanism. State preserved for inspection.
    /// </summary>
    void PowerOff();

    /// <summary>
    /// Triggers DoRestart(), publishes PoweredOn via IPowerStateProvider,
    /// then resumes emulation. Reuses existing continue mechanism.
    /// </summary>
    void PowerOn();
}
```

---

## 5. Interface Summary

New interfaces:

| Interface | Method | Notes |
|-----------|--------|-------|
| `IRestartable` | `Restart()` | Common contract for cold-boot reset (NEW) |
| `IPowerStateProvider` | `IsPoweredOn`, `Stream` | Read-only power state (NEW) |
| `IPowerStateMutator` | `SetPoweredOn(bool)` | Write access (NEW, extends provider) |

Existing interfaces that now extend `IRestartable`:

| Interface | Notes |
|-----------|-------|
| `ICard` | `ICard : IConfigurable, IRestartable` — card-specific cold init |
| `ISlots` | `ISlots : IPandowdyMemory, IConfigurable, IRestartable` — iterates cards |
| `ISystemIoHandler` | `ISystemIoHandler : IRestartable` — no-op (SoftSwitches is independent) |
| `IAppleIIBus` | `IAppleIIBus : IRestartable` — CPU reset only (priority 100) |
| `IDiskIIDrive` | `IDiskIIDrive : IRestartable` — mechanical state reset (track 0, phases) |
| `ISystemRamSelector` | Extends `IRestartable` — clears both 48K banks |
| `ILanguageCard` | Extends `IRestartable` — clears both 16K banks |
| `IKeyboardSetter` | Extends `IRestartable` — delegates to `ResetKeyboard()` |
| `IGameControllerStatus` | Extends `IRestartable` — releases all buttons |

Other existing interface changes (not via `IRestartable`):

| Interface | New Method | Notes |
|-----------|-----------|-------|
| `ISystemRam` | `Clear()` | Zero the backing array (leaf operation, not `IRestartable`) |
| `IEmulatorCoreInterface` | `DoRestart()`, `PowerOff()`, `PowerOn()` | Top-level API (`DoRestart` not `Restart` due to interface chain) |

Classes that implement `IRestartable` directly (no interface indirection):

| Class | Priority | Notes |
|-------|----------|-------|
| `AddressSpaceController` | 0 | No-op — children independently restartable |
| `SoftSwitches` | 0 | First-class participant; delegates to `ResetAllSwitches()` |
| `CpuClockingCounters` | 0 | Zeros cycle counters + VBlank state |

Classes that implement `IRestartable` through their interface:

| Class | Priority | Notes |
|-------|----------|-------|
| `MemoryBlock` | — | Implements `ISystemRam.Clear()` (leaf — no `IRestartable`) |
| `SystemRamSelector` | 0 | Via `ISystemRamSelector : IRestartable` |
| `LanguageCard` | 0 | Via `ILanguageCard : IRestartable` |
| `SystemIoHandler` | 0 | Via `ISystemIoHandler : IRestartable` — no-op |
| `NoSlotClockIoHandler` | 0 | Via `ISystemIoHandler : IRestartable` — same as `Reset()`; `_timeOffsetTicks` preserved |
| `Slots` | 0 | Via `ISlots : IRestartable` |
| `NullCard` | — | Via `ICard : IRestartable` — no-op (factory-registered) |
| `DiskIIControllerCard` (both) | — | Via `ICard : IRestartable` — full cold init (factory-registered) |
| `DiskIIDrive` | — | Via `IDiskIIDrive : IRestartable` — resets track 0, phase magnets |
| `NullDiskIIDrive` | — | Via `IDiskIIDrive : IRestartable` — no-op |
| `DiskIIStatusDecorator` | — | Via `IDiskIIDrive : IRestartable` — delegates + `SyncStatus()` |
| `DiskIIDebugDecorator` | — | Via `IDiskIIDrive : IRestartable` — transparent delegation |
| `QueuedKeyHandler` | 0 | Via `IKeyboardSetter : IRestartable` |
| `SimpleGameController` | 0 | Via `IGameControllerStatus : IRestartable` |
| `VA2MBus` | **100** | Via `IAppleIIBus : IRestartable` — CPU reset only, runs last |
| `VA2M` | — | `DoRestart()` + `PowerOff()` + `PowerOn()` (orchestrator, not in collection) |
| `PowerStateProvider` | — | Implements `IPowerStateMutator` (NEW class) |

---

## 6. Project Switching Integration

With PowerCycle implemented, the project lifecycle in `MainWindowViewModel`
becomes:

```
CloseProjectInternalAsync():
  1. SaveMountConfigurationAsync()      // Persist drive→disk mappings
  2. SaveAsync() (if file-based)        // Flush dirty blobs
  3. EjectAllDisksMessage               // Return all images to store
  4. _emulatorCore.PowerOff()           // Stop accepting cycles
  5. _projectManager.CloseAsync()       // Dispose old project, create ad hoc

OpenProjectAsync():
  1. CloseProjectInternalAsync()        // Steps 1-5 above
  2. _projectManager.OpenAsync()        // Open new .skillet
  3. _emulatorCore.PowerOn()            // Restart() + resume cycles
  4. AutoMountFromConfigurationAsync()  // Re-mount saved drives
```

The `PowerOff()` / `PowerOn()` pair ensures:
- All emulator state is clean for the new project
- No stale RAM, soft switches, or card state bleeds through
- PowerOff reuses existing pause — no RunAsync loop modifications needed
- The debugger can inspect the powered-off state (it's just paused)
- The GUI receives push notifications — never polls power state

### 6.1 Initial Startup Sequence (Changed)

**Before (current):**
```
Program.Main()
  → InitializeCoreAsync()              // Install cards, create ad hoc project
  → BuildAvaloniaApp().Start()         // Launch GUI
MainWindow.OnOpened()
  → InitialStartup()
      → _machine.Reset()              // Warm reset
      → OnEmuStartClicked()           // Start RunAsync immediately
```

**After (PowerCycle):**
```
Program.Main()
  → InitializeCoreAsync()              // Install cards, create ad hoc project
  → BuildAvaloniaApp().Start()         // Launch GUI
MainWindow.OnOpened()
  → InitialStartup()
      → Subscribe to IPowerStateProvider.Stream  // Wire GUI first
      → _machine.PowerOn()            // Restart() + publish true + Continue
                                       // GUI receives PoweredOn, shows display
```

**Key difference:** The machine starts powered-off. The GUI has time to fully
initialize and subscribe to `IPowerStateProvider.Stream` before `PowerOn()`
publishes the first `true`. This eliminates the startup race condition where
`RunAsync` could execute before the GUI was ready to render frames or process
status updates.

**User-initiated power toggle (future):** The architecture also enables a
"power switch" UI element that calls `PowerOff()` / `PowerOn()` directly,
giving users the physical experience of cycling the Apple IIe power switch.

---

## 7. IDiskImageStore Stale Reference (Separate Fix)

### Problem

`Program.cs` line 90–94 registers `IDiskImageStore` as a singleton resolved from
`projectManager.CurrentProject` at DI construction time. When the project changes,
controller cards still reference the old project's store.

### Solution: Facade on ISkilletProjectManager

Create a `DiskImageStoreFacade` that delegates to `projectManager.CurrentProject`:

```csharp
public class DiskImageStoreFacade(ISkilletProjectManager projectManager) : IDiskImageStore
{
    public Task<InternalDiskImage> CheckOutAsync(long diskImageId)
        => projectManager.CurrentProject?.CheckOutAsync(diskImageId)
           ?? throw new InvalidOperationException("No project loaded");

    public Task ReturnAsync(long diskImageId, InternalDiskImage image)
        => projectManager.CurrentProject?.ReturnAsync(diskImageId, image)
           ?? throw new InvalidOperationException("No project loaded");
}
```

Register in DI:

```csharp
services.AddSingleton<IDiskImageStore, DiskImageStoreFacade>();
```

This ensures `CheckOutAsync` / `ReturnAsync` always route to the **current**
project, regardless of when the controller card captured its `IDiskImageStore`
reference.

**This fix can be implemented on the `PowerCycle` branch since it's required for
project switching to work correctly.**

---

## 8. Implementation Order

### Phase 1: Foundation (✅ Complete)

1. `IRestartable` interface (`Pandowdy.EmuCore/Interfaces/IRestartable.cs`)
2. `CapabilityAttribute` with `priority` parameter
3. `RestartCollection` with priority-ordered `RestartAll()`
4. `CapabilityAwareServiceCollection` auto-discovery of `[Capability]` attributes
5. `ISystemRam.Clear()` + `MemoryBlock.Clear()`

### Phase 2: Leaf Subsystems (✅ Complete)

6. `IKeyboardSetter : IRestartable` + `QueuedKeyHandler.Restart()` + `SingularKeyHandler.Restart()`
7. `IGameControllerStatus : IRestartable` + `SimpleGameController.Restart()`
8. `SoftSwitches : IRestartable` with `[Capability(typeof(IRestartable))]`
9. `CpuClockingCounters : IRestartable` with `[Capability(typeof(IRestartable))]`

### Phase 3: Memory Subsystems (✅ Complete)

10. `ISystemRamSelector : IRestartable` + `SystemRamSelector.Restart()` with `[Capability]`
11. `ILanguageCard : IRestartable` + `LanguageCard.Restart()` with `[Capability]`

### Phase 4: I/O, Cards, and Factory (✅ Complete)

12. `ISystemIoHandler : IRestartable` + `SystemIoHandler.Restart()` (no-op) with `[Capability]`
13. `NoSlotClockIoHandler.Restart()` (same as `Reset()`; `_timeOffsetTicks` preserved)
14. `ICard : IRestartable` + `NullCard.Restart()`
15. `IDiskIIDrive : IRestartable` + `DiskIIDrive.Restart()` + `NullDiskIIDrive.Restart()`
16. `DiskIIStatusDecorator.Restart()` + `DiskIIDebugDecorator.Restart()`
17. `DiskIIControllerCard.Restart()` (both 13-sector and 16-sector)
18. `ISlots : IRestartable` + `Slots.Restart()` with `[Capability]`
19. `CardFactory` accepts `RestartCollection`, registers factory-created cards

### Phase 5: Bus and Controller (✅ Complete)

20. `AddressSpaceController : IRestartable` with `[Capability]` (no-op)
21. `IAppleIIBus : IRestartable` + `VA2MBus.Restart()` with `[Capability(priority: 100)]`

### Phase 6: Top Level (✅ Complete)

22. `VA2M.DoRestart()` using `RestartCollection.RestartAll()`
23. `IEmulatorCoreInterface.DoRestart()`
24. `RestartCollection` DI registration in `Program.cs`

### Phase 7: Power State Service (TODO)

25. `IPowerStateProvider` + `IPowerStateMutator` + `PowerStateProvider` (NEW)
26. DI registration for `PowerStateProvider` (split read/write interfaces)
27. `VA2M.PowerOff()` + `PowerOn()` (publishes via `IPowerStateMutator`)
28. `VA2M._isPoweredOn` starts as `false` (machine OFF at construction)
29. `IEmulatorCoreInterface` additions (`PowerOff`, `PowerOn`)

### Phase 8: DI Wiring Fix (TODO)

30. `DiskImageStoreFacade` + DI registration change

### Phase 9: Integration (TODO)

31. Update `MainWindow.InitialStartup()`: subscribe to `IPowerStateProvider.Stream`, then call `PowerOn()` instead of `Reset()` + `OnEmuStartClicked()`
32. Update `CloseProjectInternalAsync()` to call `PowerOff()`
33. Update `OpenProjectAsync()` / `NewProjectAsync()` to call `PowerOn()`
34. Update `OnClosingAsync()` exit path
35. GUI cosmetic handling: subscribe `Apple2Display` / `MainWindowViewModel` to `IPowerStateProvider.Stream`

### Phase 10: Testing (✅ RestartCollection tests complete; others per-phase)

- `RestartCollectionTests` — 21 tests covering priority ordering, registration,
  unregistration, production component priorities (VA2MBus > SoftSwitches)
- Phase 2–6 tests: Verify `IRestartable` contract, `Clear()` zeros memory,
  `Restart()` clears keyboard/buttons, zeros both banks, soft switches at
  power-on defaults, card cold init, bus restart order
- Phase 7: Verify `PowerStateProvider` publishes correctly, `PowerOff` pauses +
  publishes `false`, `PowerOn` triggers `DoRestart` + publishes `true` + continues
- Phase 8: Verify facade delegates to current project
- Phase 9: Integration tests for startup sequence, project switching

---

## 9. What This Does NOT Change

- **RunAsync loop structure** — completely untouched; PowerOff reuses existing Pause
- **Clock() method** — no gate check added; PowerOff uses Pause to stop cycling
- **VBlank timing** — `CpuClockingCounters.Reset()` already handles this
- **Rendering pipeline** — `RenderingService` and `VideoMemorySnapshotPool` are unaffected
- **UI subscriptions** — all singleton observables stay valid (no DI rebuild)
- **ROM loading** — ROM is loaded once at construction, never changes
- **Existing Reset()** — warm reset behavior is completely preserved
- **Test infrastructure** — existing 2,332 tests should continue to pass
