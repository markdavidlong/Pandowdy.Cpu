# PowerCycle Blueprint — VA2M Power Lifecycle

**Status:** Design  
**Branch:** `PowerCycle` (to be created from `skillet`)  
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

3. **The flow tree is identical.** `Restart()` follows the same call chain as
   `Reset()` — `VA2M` → `Bus` → `AddressSpaceController` → subsystems.

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

## 3. Flow Tree

```
Startup (Program.cs / MainWindow)
  └─ _isPoweredOn starts as false       // Machine is OFF until user/GUI triggers PowerOn()
     (GUI has time to wire subscriptions before first PowerOn message)

VA2M.Restart()                          // Enqueued on emulator thread
  ├─ _keyboardSetter.Restart()          // Clear latch + queue
  ├─ Bus.Restart()
  │   ├─ _addressSpace.Restart()
  │   │   ├─ _systemRam.Restart()       // Clear main 48K + aux 48K
  │   │   ├─ _io.Restart()              // Delegates to SoftSwitches.ResetAllSwitches()
  │   │   ├─ _slots.Restart()           // Cold-init each installed card
  │   │   │   └─ each card.Restart()    // Card-specific cold init
  │   │   └─ _langCard.Restart()        // Clear main 16K + aux 16K, banking → ROM
  │   ├─ _cpu.Reset(this)               // Load reset vector (same as warm reset)
  │   └─ _clockCounters.Reset()         // Zero cycle counters + VBlank state
  ├─ Reset throttle/perf state          // Same as current Reset()
  └─ _gameController.Restart()          // Release all buttons

VA2M.PowerOff()                         // Effectively Pause + publish
  ├─ _emuState.RequestPause()           // Reuse existing pause mechanism
  ├─ _isPoweredOn = false               // Private flag
  └─ _powerState.SetPoweredOn(false)    // Publish via BehaviorSubject → GUI reacts

VA2M.PowerOn()                          // Restart + publish + resume
  ├─ Restart()                          // Full cold boot (clears everything)
  ├─ _isPoweredOn = true                // Private flag
  ├─ _powerState.SetPoweredOn(true)     // Publish via BehaviorSubject → GUI reacts
  └─ _emuState.RequestContinue()        // Resume execution via existing mechanism
```

---

## 4. Subsystem Changes

### 4.0 IRestartable (New Interface)

All subsystems that participate in the cold-boot `Restart()` tree implement a
common `IRestartable` interface. This provides a uniform contract and enables
batch restart operations (e.g., `Slots` iterating over cards without casting).

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

**Change:** None needed. `ResetAllSwitches()` is already the correct cold boot
behavior. `SystemIoHandler.Reset()` already calls it, and `Restart()` will too.

### 4.5 SystemIoHandler

**Current state:** Has `Reset()` that calls `_softSwitches.ResetAllSwitches()`.

**Change:** Add `Restart()`:

```csharp
public void Restart()
{
    // Soft switches to power-on defaults (same as Reset)
    Reset();
    // SystemIoHandler has no additional state to clear —
    // keyboard and game controller are reset by their owners.
}
```

For `SystemIoHandler`, `Restart()` and `Reset()` are equivalent because the
handler is stateless beyond the soft switches. The keyboard and game controller
state is managed by VA2M (which calls their `Restart()` separately).

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

**Change:** Add `Restart()` that does everything:

```csharp
public void Restart()
{
    _systemRam.Restart();    // Clear 48K main + 48K aux
    _io.Restart();           // Soft switches → power-on defaults
    _slots.Restart();        // Cold-init each card
    _langCard.Restart();     // Clear 16K main + 16K aux
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

### 4.12 IDiskIIDrive Implementations

**Change:** Add `Restart()` that resets mechanical state:

```csharp
public void Restart()
{
    _currentTrack = 0;
    _headPosition = 0;
    _phaseMagnets = 0;
    // Disk media stays inserted (if any)
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

### 4.15 VA2MBus

**Current state:** Has `Reset()` that calls `_addressSpace.Reset()` +
`_cpu.Reset()` + `_clockCounters.Reset()`.

**Change:** Add `Restart()`:

```csharp
public void Restart()
{
    ThrowIfDisposed();
    _addressSpace.Restart();    // RAM + switches + cards + lang card
    _cpu.Reset(this);           // CPU always uses same reset mechanism
    _clockCounters.Reset();     // Zero counters (same as warm reset)
}
```

Note: The CPU doesn't need a separate `Restart()` — `_cpu.Reset()` loads the
reset vector, which is the correct behavior for both warm and cold boot. The ROM
initialization code handles the rest.

### 4.16 VA2M

**Current state:** Has `Reset()` that enqueues keyboard reset + `Bus.Reset()` +
throttle/perf counter reset. Starts running immediately when `RunAsync` is called.

**Change:** Add `Restart()`, `PowerOff()`, `PowerOn()`. Start powered-off.
Publish power state via `IPowerStateMutator` (never polled by GUI):

```csharp
// Private flag — NOT polled by GUI. Read-only accessor for utility/debug use only.
private volatile bool _isPoweredOn = false; // Starts OFF

/// <summary>
/// Gets whether the emulator is in the powered-on state.
/// </summary>
/// <remarks>
/// This is a read-only utility accessor for internal use and tests.
/// The GUI must NOT poll this property — it subscribes to
/// <see cref="IPowerStateProvider.Stream"/> for push-based notification.
/// </remarks>
internal bool IsPoweredOn => Volatile.Read(ref _isPoweredOn);

private readonly IPowerStateMutator _powerState;

public void Restart()
{
    Enqueue(() =>
    {
        _keyboardSetter.Restart();
        _gameController.Restart();
        Bus.Restart();

        // Reset throttle and performance state (same as Reset)
        _throttleCycles = 0;
        _throttleSw.Restart();
        _perfLastCycles = 0;
        _perfLastReportTicks = _perfSw.ElapsedTicks;
        _throttleErrorAccumulator = 0;
        _throttleLastError = 0;
        _adaptiveSpinWaitIterations = 100;
    });
}

public void PowerOff()
{
    _emuState.RequestPause();                // Reuse existing pause mechanism
    Volatile.Write(ref _isPoweredOn, false); // Private flag
    _powerState.SetPoweredOn(false);         // Push notification → GUI reacts
}

public void PowerOn()
{
    Restart();                               // Full cold boot
    Volatile.Write(ref _isPoweredOn, true);  // Private flag
    _powerState.SetPoweredOn(true);          // Push notification → GUI reacts
    _emuState.RequestContinue();             // Resume via existing mechanism
}
```

**Startup sequence:** The emulator starts powered-off (`_isPoweredOn = false`).
The `PowerStateProvider` is initialized with `false`. The GUI subscribes to
`IPowerStateProvider.Stream` during construction and immediately receives `false`
via the `BehaviorSubject`. When the user (or `InitialStartup()`) calls
`PowerOn()`, the provider publishes `true` and the GUI reacts — starts showing
the display, enables status panels, etc.

This solves a startup ordering problem: previously `RunAsync` started immediately
in `MainWindow.InitialStartup()` before the GUI might be fully wired. Now the
GUI is guaranteed to be subscribed before the first `PowerOn` message.

**No RunAsync/Clock gate changes needed.** PowerOff reuses the existing
pause mechanism (`IEmulatorState.RequestPause()`), which already causes
`RunAsync` to exit its execution loop. PowerOn reuses `RequestContinue()`.

**GUI receives push notifications** via `IPowerStateProvider.Stream`:
- `false` → blank/dim the Apple II display, suppress motor indicators, show "Power Off"
- `true` → restore normal display, enable status panels

The GUI never polls `IsPoweredOn`. The only exits from powered-off state are:
1. `PowerOn()` — calls `Restart()` which clears all stale state
2. New/Open project — calls `PowerOn()` after project setup
3. Application exit — no state matters

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

**Change:** Add `Restart()`, `PowerOff()`, `PowerOn()` to the interface.
Remove `IsPoweredOn` property — the GUI subscribes to `IPowerStateProvider`
instead of reading from the core interface:

```csharp
public interface IEmulatorCoreInterface
{
    // ... existing members ...

    /// <summary>
    /// Cold boot: clears all RAM, resets soft switches, cold-inits cards,
    /// and loads the CPU reset vector. Equivalent to a power cycle.
    /// </summary>
    void Restart();

    /// <summary>
    /// Pauses emulation and publishes PoweredOff via IPowerStateProvider.
    /// Reuses existing pause mechanism. State preserved for inspection.
    /// </summary>
    void PowerOff();

    /// <summary>
    /// Triggers Restart(), publishes PoweredOn via IPowerStateProvider,
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
| `ISystemIoHandler` | `ISystemIoHandler : IRestartable` — delegates to SoftSwitches |
| `IAppleIIBus` | `IAppleIIBus : IRestartable` — flows to AddressSpace + CPU + counters |
| `ISystemRamSelector` | Extends `IRestartable` — clears both 48K banks |
| `ILanguageCard` | Extends `IRestartable` — clears both 16K banks |
| `IKeyboardSetter` | Extends `IRestartable` — delegates to `ResetKeyboard()` |
| `IGameControllerStatus` | Extends `IRestartable` — releases all buttons |

Other existing interface changes (not via `IRestartable`):

| Interface | New Method | Notes |
|-----------|-----------|-------|
| `ISystemRam` | `Clear()` | Zero the backing array (leaf operation, not `IRestartable`) |
| `IEmulatorCoreInterface` | `Restart()`, `PowerOff()`, `PowerOn()` | Top-level API (not `IRestartable` — different contract) |

Classes that implement `IRestartable` directly (no interface indirection):

| Class | Notes |
|-------|-------|
| `AddressSpaceController` | `IRestartable` on class — not exposed via a restartable interface |

Classes that implement `IRestartable` through their interface:

| Class | Notes |
|-------|-------|
| `MemoryBlock` | Implements `ISystemRam.Clear()` (leaf — no `IRestartable`) |
| `SystemRamSelector` | Via `ISystemRamSelector : IRestartable` |
| `LanguageCard` | Via `ILanguageCard : IRestartable` |
| `SystemIoHandler` | Via `ISystemIoHandler : IRestartable` |
| `Slots` | Via `ISlots : IRestartable` |
| `NullCard` | Via `ICard : IRestartable` — no-op |
| `DiskIIControllerCard` (both variants) | Via `ICard : IRestartable` — full cold init |
| `QueuedKeyHandler` | Via `IKeyboardSetter : IRestartable` |
| `SimpleGameController` | Via `IGameControllerStatus : IRestartable` |
| `VA2MBus` | Via `IAppleIIBus : IRestartable` |
| `VA2M` | `Restart()` + `PowerOff()` + `PowerOn()` (publishes via `IPowerStateMutator`) |
| `PowerStateProvider` | Implements `IPowerStateMutator` (NEW class) |

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

### Phase 1: Foundation + Leaf Subsystems (no dependencies)

1. `IRestartable` interface (`Pandowdy.EmuCore/Interfaces/IRestartable.cs`)
2. `ISystemRam.Clear()` + `MemoryBlock.Clear()`
3. `IKeyboardSetter : IRestartable` + `QueuedKeyHandler.Restart()`
4. `IGameControllerStatus : IRestartable` + `SimpleGameController.Restart()`

### Phase 2: Memory Subsystems

5. `ISystemRamSelector : IRestartable` + `SystemRamSelector.Restart()`  
6. `ILanguageCard : IRestartable` + `LanguageCard.Restart()`

### Phase 3: I/O and Cards

7. `ISystemIoHandler : IRestartable` + `SystemIoHandler.Restart()`
8. `ICard : IRestartable` + `NullCard.Restart()`
9. `DiskIIControllerCard.Restart()` (both 13-sector and 16-sector)
10. `IDiskIIDrive` Restart implementations
11. `ISlots : IRestartable` + `Slots.Restart()`

### Phase 4: Bus and Controller

12. `AddressSpaceController : IRestartable`
13. `IAppleIIBus : IRestartable` + `VA2MBus.Restart()`

### Phase 5: Power State Service + Top Level

14. `IPowerStateProvider` + `IPowerStateMutator` + `PowerStateProvider` (NEW)
15. DI registration for `PowerStateProvider` (split read/write interfaces)
16. `VA2M.Restart()` + `PowerOff()` + `PowerOn()` (publishes via `IPowerStateMutator`)
17. `VA2M._isPoweredOn` starts as `false` (machine OFF at construction)
18. `IEmulatorCoreInterface` additions (`Restart`, `PowerOff`, `PowerOn`)

### Phase 6: DI Wiring Fix

19. `DiskImageStoreFacade` + DI registration change

### Phase 7: Integration

20. Update `MainWindow.InitialStartup()`: subscribe to `IPowerStateProvider.Stream`, then call `PowerOn()` instead of `Reset()` + `OnEmuStartClicked()`
21. Update `CloseProjectInternalAsync()` to call `PowerOff()`
22. Update `OpenProjectAsync()` / `NewProjectAsync()` to call `PowerOn()`
23. Update `OnClosingAsync()` exit path
24. GUI cosmetic handling: subscribe `Apple2Display` / `MainWindowViewModel` to `IPowerStateProvider.Stream`

### Phase 8: Testing

Each phase should include unit tests:

- Phase 1: Verify `IRestartable` contract, `Clear()` zeros memory, `Restart()` clears keyboard/buttons
- Phase 2: Verify `Restart()` zeros both banks (48K + 48K, 16K + 16K)
- Phase 3: Verify soft switches at power-on defaults, card cold init
- Phase 4: Verify full bus restart clears everything
- Phase 5: Verify `PowerStateProvider` publishes correctly, `PowerOff` pauses + publishes `false`, `PowerOn` triggers `Restart` + publishes `true` + continues
- Phase 6: Verify facade delegates to current project
- Phase 7: Integration tests for startup sequence (machine starts off, `PowerOn` triggers first `true`), project switching (off → swap → on)

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
