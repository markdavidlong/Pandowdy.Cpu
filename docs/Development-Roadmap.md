# Pandowdy Development Roadmap

> **📌 IMPORTANT:** This document is the **single source of truth** for all planned updates, scheduled tasks, and development priorities in the Pandowdy project. All AI assistants and team members should consult this document when planning or implementing features.

---

## 📊 Current State Summary

| Status | Details |
|--------|---------|
| **Branch** | `tasks` |
| **Tests** | 2039 tests (1766 EmuCore + 126 Disassembler + 147 UI) passing ✅ |
| **Last Milestone** | Disk II Motor State Refactoring (Task 25) ✅ COMPLETE |
| **Current Focus** | Task 22 (Intermediate Debugger Implementation) ⏳ NOT STARTED |

---

## Table of Contents

1. [In Progress Tasks](#in-progress-tasks)
   - None
2. [Active Tasks](#active-tasks)
   - [Task 22: Intermediate Debugger Implementation](#task-22-intermediate-debugger-implementation-high-priority)
   - [Task 5: GUI Disk Management Features](#task-5-gui-disk-management-features-high-priority)
   - [Task 26: WozDiskImageProvider Debugging](#task-26-wozdiskimageprovider-debugging-high-priority)
   - [Task 27: NibDiskImageProvider Debugging](#task-27-nibdiskimageprovider-debugging-high-priority)
   - [Task 13: Audio Emulation Implementation](#task-13-audio-emulation-implementation-medium-priority)
3. [Backlog](#backlog)
   - [Task 4: HGR Flicker Investigation](#task-4-hgr-flicker-investigation-medium-priority)
   - [Task 7: Handle BRK Loops in Interrupt Handler](#task-7-handle-brk-loops-in-interrupt-handler-low-priority)
   - [Task 10: SectorDiskImageProvider Debugging](#task-10-sectordiskimageprovider-debugging-medium-priority)
   - [Task 9: Multi-Drive Operation Deep Dive](#task-9-multi-drive-operation-deep-dive-low-priority)
   - [Task 11: Conditional Compilation for Disk Provider Debug Output](#task-11-conditional-compilation-for-disk-provider-debug-output-medium-priority)
   - [Task 12: Flexible Window Docking System](#task-12-flexible-window-docking-system-medium-priority)
   - [Task 14: Speed-Proportional Key Feeding](#task-14-speed-proportional-key-feeding-low-priority)
   - [Task 15: Authentic Apple II Keyboard Repeat](#task-15-authentic-apple-ii-keyboard-repeat-low-priority)
   - [Task 16: Research Cross-Platform Shader Rendering for Avalonia](#task-16-research-cross-platform-shader-rendering-for-avalonia-low-priority)
   - [Task 17: Research Compute-Shader Toolkits for Bitplane Processing](#task-17-research-compute-shader-toolkits-for-bitplane-processing-medium-priority)
   - [Task 20: Advanced Debugger Features](#task-20-advanced-debugger-features-low-priority)
   - [Task 21: Peripheral Discovery and Enumeration API](#task-21-peripheral-discovery-and-enumeration-api-medium-priority)
   - [Task 23: Split IKeyboardSetter into IKeyboardSetter and IKeyboardResetter](#task-23-split-ikeyboardsetter-into-ikeyboardsetter-and-ikeyboardresetter-low-priority)
4. [Completed Tasks](#completed-tasks)
   - [Task 1: Migrate VA2M to CpuClockingCounters.VBlankOccurred](#task-1-migrate-va2m-to-cpuclockingcountersvblankoccurred)
   - [Task 2: Remove VA2MBus.VBlank Event](#task-2-remove-va2mbusvblank-event)
   - [Task 3: Removed](#task-3-removed)
   - [Task 6: Clear Pending Keystrokes on Reset](#task-6-clear-pending-keystrokes-on-reset)
   - [Task 8: Check for Race Conditions at High Speeds](#task-8-check-for-race-conditions-at-high-speeds)
   - [Task 18: Migrate to Pandowdy.Cpu](#task-18-migrate-to-pandowdycpu-critical-priority)
   - [Task 19: Basic Debugger Foundation](#task-19-basic-debugger-foundation)
   - [Task 24: Fix DiskII Motor-Off Behavior on Drive Switching](#task-24-fix-diskii-motor-off-behavior-on-drive-switching)
   - [Task 25: Disk II Motor State Refactoring - Move to Controller Level](#task-25-disk-ii-motor-state-refactoring---move-to-controller-level)
5. [Code Style Guidelines](#code-style-guidelines)
6. [Git Best Practices](#git-best-practices)
7. [Testing Guidelines](#testing-guidelines)
8. [Other notes](#other-notes)

---

## In Progress Tasks

None

---

## Active Tasks

---

### Task 19: Basic Debugger Foundation (High Priority)

**Goal:** Implement a rudimentary debugger for Pandowdy using Pandowdy.Cpu's introspection capabilities, enabling breakpoints, watches, and stepping through code.

**Status:** ⏳ IN PROGRESS

**Progress:**
- ✅ `CpuStateSnapshot` wrapper struct created (encapsulates CPU state without exposing Pandowdy.Cpu types)
- ✅ `CpuExecutionStatus` enum created (Running, Stopped, Jammed, Waiting, Bypassed)
- ✅ `IMemoryInspector` interface created (extends IDirectMemoryPoolReader with ROM access)
- ✅ `MemoryInspector` implementation created (side-effect-free reads from RAM, ROM, slot cards)
- ✅ `IEmulatorCoreInterface.CpuState` property returns `CpuStateSnapshot`
- ✅ `IEmulatorCoreInterface.MemoryInspector` property returns `IMemoryInspector`
- ✅ `CpuStatusPanel` UI control created (displays PC, A, X, Y, SP, flags, execution status)
- ✅ `CpuStatusPanelViewModel` created (60Hz updates via IRefreshTicker)
- ✅ CPU Status Panel integrated into MainWindow below Apple II display
- ✅ Debugger core - stepping
- ⏳ Debugger core - breakpoints - NOT STARTED
- ⏳ Debugger UI panels (disassembly, watches) - NOT STARTED

**Prerequisites:**
- ✅ Task 18 (Migrate to Pandowdy.Cpu) - COMPLETED
- ✅ Read-only state inspection via `IEmulatorCoreInterface` - COMPLETED
- Pandowdy.Cpu provides the state introspection, breakpoint hooks, and single-step capabilities needed

**Current State:**
- Pandowdy.Cpu integrated with cycle-accurate execution ✅
- CPU state accessible via `IEmulatorCoreInterface.CpuState` (A, X, Y, SP, PC, P, flags, status) ✅
- Memory accessible via `IEmulatorCoreInterface.MemoryInspector` (main/aux RAM, system ROM, slot ROM, active mapping) ✅
- Cycle count accessible via `IEmulatorCoreInterface.TotalCycles` ✅
- CPU status panel displays real-time state at 60Hz ✅
- Ready to implement debugging infrastructure

**Infrastructure Available:
```csharp
// CPU state snapshot (thread-safe, readonly struct)
var cpu = emulator.CpuState;
Console.WriteLine($"PC=${cpu.PC:X4} A=${cpu.A:X2} X=${cpu.X:X2} Y=${cpu.Y:X2}");
Console.WriteLine($"Flags: {cpu.FlagsString}"); // e.g., "Nv-bdiZc"
Console.WriteLine($"Status: {cpu.Status}"); // Running, Stopped, Jammed, Waiting

// Individual flag access
if (cpu.FlagC) Console.WriteLine("Carry is set");
if (cpu.AtInstructionBoundary) Console.WriteLine("Safe to inspect state");

// Memory inspection (thread-safe reads)
var mem = emulator.MemoryInspector;
byte mainValue = mem.ReadRawMain(0x0400);      // TEXT page 1 (main)
byte auxValue = mem.ReadRawAux(0x0400);        // TEXT page 1 (aux)
byte romValue = mem.ReadSystemRom(0xFFFE);     // Reset vector (always ROM)
byte activeValue = mem.ReadActiveHighMemory(0xD000); // ROM or LC RAM based on soft switches
byte slotRom = mem.ReadSlotRom(6, 0x00);       // First byte of slot 6 ROM
byte[] block = mem.ReadMainBlock(0x2000, 0x2000); // Bulk read HGR page 1

// Timing info
ulong totalCycles = emulator.TotalCycles;
```

**Future Consideration: Nullable Returns for IMemoryInspector**

The current `IMemoryInspector` methods return `byte` (with 0 for unavailable addresses). A future enhancement could change ROM methods to return `byte?` to distinguish between:
- `null` = address not available/not readable (e.g., $C000-$C0FF I/O space)
- `0` = address is readable and contains the value zero

This would match the pattern already used in `ICard.ReadRom()`, `ICard.ReadIO()`, and `ICard.ReadExtendedRom()` which all return `byte?`. The change would be:
```csharp
// Current
byte ReadSystemRom(int address);        // Returns 0 for unavailable
byte ReadActiveHighMemory(int address); // Returns 0 for unavailable
byte ReadSlotRom(int slot, int offset); // Returns 0 for empty slot

// Future (more semantically correct)
byte? ReadSystemRom(int address);        // Returns null for unavailable
byte? ReadActiveHighMemory(int address); // Returns null for unavailable  
byte? ReadSlotRom(int slot, int offset); // Returns null for empty slot
```

**Scope: Basic Debugger (Not Full IDE)**
This is a foundational debugger to aid development, not a full-featured IDE debugger. Focus on essential features.

**Features to Implement:**

1. **Breakpoints**
   - Address breakpoints (break when PC reaches address)
   - Conditional breakpoints (break when condition met, e.g., A == $FF)
   - Enable/disable breakpoints without removing
   - Breakpoint list management (add, remove, clear all)

2. **Watches**
   - Memory watches (monitor specific addresses)
   - Register watches (A, X, Y, SP, PC, Status)
   - Watch display in UI panel
   - Optional: break on memory write to watched address

3. **Stepping Modes**
   - **Step Into** - Execute one instruction, follow JSR/JMP
   - **Step Over** - Execute one instruction, treat JSR as single step (run until RTS)
   - **Step Out** - Run until current subroutine returns (RTS/RTI)
   - **Run Until** - Run until specific address reached
   - **Note:** Favor instruction stepping over cycle stepping (even though Pandowdy.Cpu is cycle-accurate)

4. **Execution Control**
   - Pause/Resume execution
   - Single instruction step (default)
   - Single cycle step (optional, for advanced debugging)
   - Run to cursor/address

5. **CPU State Display**
   - Registers: A, X, Y, SP, PC
   - Status flags: N, V, B, D, I, Z, C (with visual indicators)
   - Current instruction disassembly
   - Stack contents (top N entries)

**Technical Approach:**

**Instruction vs Cycle Stepping:**
```csharp
// Prefer instruction stepping (cleaner UX)
public void StepInstruction()
{
    do
    {
        _cpu.Clock(bus);
    } while (!_cpu.IsInstructionComplete());
}

// Cycle stepping available for advanced use
public void StepCycle()
{
    _cpu.Clock(bus);
}
```

**Breakpoint Check Pattern:**
```csharp
public void RunWithBreakpoints()
{
    while (_running)
    {
        if (_breakpoints.Contains(_cpu.PC))
        {
            _running = false;
            OnBreakpointHit?.Invoke(_cpu.PC);
            break;
        }

        _cpu.Clock(bus);

        if (_cpu.IsInstructionComplete())
        {
            CheckConditionalBreakpoints();
            CheckWatchBreakpoints();
        }
    }
}
```

**Step Over Implementation:**
```csharp
public void StepOver()
{
    ushort currentPC = _cpu.PC;
    byte opcode = bus.CpuRead(currentPC);

    if (IsJsrInstruction(opcode))
    {
        // Set temporary breakpoint at next instruction
        ushort returnAddress = (ushort)(currentPC + 3); // JSR is 3 bytes
        RunUntil(returnAddress);
    }
    else
    {
        StepInstruction();
    }
}
```

**Files to Create:**

*Core Debugger:*
- `Pandowdy.EmuCore\Debugging\IDebugger.cs` - Debugger interface
- `Pandowdy.EmuCore\Debugging\Debugger.cs` - Main debugger implementation
- `Pandowdy.EmuCore\Debugging\Breakpoint.cs` - Breakpoint model
- `Pandowdy.EmuCore\Debugging\Watch.cs` - Watch model
- `Pandowdy.EmuCore\Debugging\DebuggerState.cs` - Debugger state enum (Running, Paused, Stepping)

*UI Components:*
- `Pandowdy.UI\Controls\DebuggerPanel.axaml` - Main debugger UI panel
- `Pandowdy.UI\ViewModels\DebuggerPanelViewModel.cs` - Debugger panel view model
- `Pandowdy.UI\Controls\RegistersPanel.axaml` - CPU registers display
- `Pandowdy.UI\Controls\BreakpointListPanel.axaml` - Breakpoint management
- `Pandowdy.UI\Controls\WatchPanel.axaml` - Watch variables display

*Tests:*
- `Pandowdy.EmuCore.Tests\Debugging\DebuggerTests.cs` - Unit tests for debugger
- `Pandowdy.EmuCore.Tests\Debugging\BreakpointTests.cs` - Breakpoint logic tests

**Modified Files:**
- `Pandowdy.EmuCore\VA2M.cs` - Integrate debugger into main emulation loop
- `Pandowdy.UI\MainWindow.axaml` - Add debugger panel/menu
- `Pandowdy.UI\ViewModels\MainWindowViewModel.cs` - Debugger commands
- `Pandowdy\Program.cs` - Register debugger in DI

**Architecture Notes:**
- Debugger is injected via DI (follows project guidelines)
- Debugger wraps or coordinates with CPU execution
- UI binds to debugger state via ReactiveUI
- Breakpoint checks happen at instruction boundaries (not every cycle)
- Stepping defaults to instruction-level granularity

**UI Integration:**
- Debugger panel dockable (integrates with future Task 12)
- Keyboard shortcuts: F5 (Run), F10 (Step Over), F11 (Step Into), Shift+F11 (Step Out)
- Toolbar buttons for common operations
- Status bar shows debugger state (Running/Paused/Stepping)

**Testing Strategy:**
- Unit tests for breakpoint matching
- Unit tests for step over/into/out logic
- Integration tests with simple 6502 programs
- Verify stepping doesn't affect cycle timing

**Benefits:**
- ✅ Enables debugging of disk loading issues (Task 5)
- ✅ Essential for GCR/sector debugging (Task 10)
- ✅ Helps debug audio timing (Task 13)
- ✅ General development productivity improvement
- ✅ Foundation for future advanced debugging features

**Priority:** High (unblocks Tasks 5, 10, 13)

**Dependencies:**
- **Requires:** Task 18 (Pandowdy.Cpu migration)
- **Enables:** Tasks 5, 10, 13 (debugging support)
- **Related:** Task 12 (docking system for debugger panels)

---

## Active Tasks

### Task 5: GUI Disk Management Features (High Priority)

**Goal:** Add user-facing disk management capabilities to the GUI.

**Status:** ⏳ NOT STARTED

**Features to Implement:**

1. **Insert Disk Image from GUI**
   - File open dialog to select disk images
   - Support all formats: .dsk, .do, .po, .nib, .woz, .2mg
   - Menu item: File → Insert Disk → Drive 1 / Drive 2
   - Keyboard shortcuts (e.g., Ctrl+1, Ctrl+2)

2. **Eject Disk from GUI**
   - Context menu on drive status panel
   - Menu item: File → Eject Disk → Drive 1 / Drive 2
   - Confirmation if disk has unsaved changes (write support)

3. **Swap Drive 1 and Drive 2 Disk Images**
   - Quick swap button/menu item
   - Useful for programs that expect different disk in different drive
   - Keyboard shortcut (e.g., Ctrl+Shift+S for swap)

4. **Drag and Drop Support**
   - Drop disk image file onto drive status panel
   - Drop onto main window defaults to Drive 1

5. **Recent Disk Images**
   - Track recently used disk images
   - Quick access submenu

**Files to Modify:**
- `Pandowdy.UI\MainWindow.axaml` - Add menu items
- `Pandowdy.UI\ViewModels\MainWindowViewModel.cs` - Add command handlers
- `Pandowdy.UI\Controls\DiskStatusPanel.axaml` - Add context menu, drag/drop support
- `Pandowdy.UI\ViewModels\DiskStatusPanelViewModel.cs` - Add insert/eject/swap logic
- `Pandowdy\Program.cs` - Remove hardcoded disk inserts (lines 181-204)

**Priority:** High

**Notes:**
- Hardcoded test images in `Program.cs` are intentional for development
- Location on E:\ drive is intentional for development

**Dependencies:**
- Would benefit from debugger (Task 19) for troubleshooting disk load/format issues

---

### Task 26: WozDiskImageProvider Debugging (High Priority)

**Goal:** Debug and fix random Disk I/O errors occurring with WOZ format disk images in DOS 3.3.

**Status:** ⏳ NOT STARTED

**Current Issue:**
- Random Disk I/O errors occurring in DOS 3.3 with WOZ images
- Suspect subtle bugs in WOZ provider implementation
- Currently using InternalWozDiskImageProvider (primary focus)
- May also need to verify external WozDiskImageProvider

**Areas to Investigate:**
- Bit timing and synchronization
- Track data decoding (WOZ stores raw flux transitions)
- Self-sync byte detection
- Address field parsing
- Data field CRC validation
- Track wrapping/overflow handling
- Edge cases in flux transition timing

**Test Strategy:**
- Test with known-good DOS 3.3 system disks
- Compare behavior with real hardware or other emulators
- Add comprehensive unit tests for edge cases
- Test with various WOZ images (WOZ1, WOZ2 formats)
- Verify behavior with copy-protected disks

**Debugging Approach:**
- Enable detailed bit-level logging
- Compare bit stream output with expected patterns
- Verify timing matches WOZ specification
- Check for off-by-one errors in track positioning
- Validate CRC calculations

**Files to Focus On:**
- `Pandowdy.EmuCore\DiskII\Providers\InternalWozDiskImageProvider.cs` (primary)
- `Pandowdy.EmuCore\DiskII\Providers\WozDiskImageProvider.cs` (external wrapper)
- `Pandowdy.EmuCore.Tests\DiskII\Providers\WozDiskImageProviderTests.cs`
- `Pandowdy.EmuCore\DiskII\GcrEncoder.cs` (if issues with encoding)

**Priority:** High (blocking reliable DOS 3.3 usage)

**Dependencies:**
- Would greatly benefit from debugger (Task 19) for stepping through bit-level decoding
- May require Task 11 (conditional debug output) to reduce noise while debugging

---

### Task 27: NibDiskImageProvider Debugging (High Priority)

**Goal:** Verify and debug NIB format disk image provider to ensure reliable operation.

**Status:** ⏳ NOT STARTED

**Current Issue:**
- Potential subtle bugs in NIB provider (not yet confirmed)
- Need to verify NIB provider is not contributing to DOS 3.3 I/O errors
- NIB format is simpler than WOZ but still requires careful bit-level handling

**Areas to Investigate:**
- NIB format parsing (6-and-2 encoding)
- Track offset calculations
- Self-sync byte handling
- Address field decoding
- Data field checksum validation
- Track length handling (NIB tracks are 6656 bytes)
- Bit alignment and synchronization

**Test Strategy:**
- Test with known-good DOS 3.3 NIB images
- Compare behavior with WOZ provider for same disk
- Verify all 35 tracks read correctly
- Test sector interleaving patterns
- Validate checksums on all sectors

**Debugging Approach:**
- Compare NIB bit patterns with expected DOS 3.3 format
- Verify 6-and-2 decoding tables
- Check track offset arithmetic
- Validate sector header and data field parsing
- Test boundary conditions (track 0, track 34)

**Files to Focus On:**
- `Pandowdy.EmuCore\DiskII\Providers\NibDiskImageProvider.cs`
- `Pandowdy.EmuCore.Tests\DiskII\Providers\NibDiskImageProviderTests.cs`
- `Pandowdy.EmuCore\DiskII\GcrEncoder.cs` (6-and-2 encoding tables)

**Priority:** High (ensure reliable NIB format support)

**Dependencies:**
- Should be debugged after Task 26 (WOZ debugging) to compare behavior
- Would benefit from debugger (Task 19) for bit-level inspection
- May require Task 11 (conditional debug output) for cleaner debugging

---

### Task 13: Audio Emulation Implementation (Medium Priority)

**Goal:** Research and implement cross-platform audio emulation for Apple IIe speaker with future Mockingboard support.

**Status:** ⏳ NOT STARTED

**Current State:**
- No audio emulation currently implemented
- Apple IIe speaker output not functional
- Need cross-platform audio solution for Windows, macOS, Linux

**Phase 1: Research & Audio Library Selection**

**Requirements:**
- Cross-platform support (.NET 8, Windows/macOS/Linux)
- Low latency for accurate speaker emulation
- Support for both speaker clicks and Mockingboard PCM audio
- Integration with Avalonia UI lifecycle

**Candidate Libraries:**
- **Silk.NET.OpenAL** is probably the most likely candidate. - Modern .NET 8 OpenAL bindings


**Phase 2: Apple IIe Speaker Emulation**

**Implementation Requirements:**
- Emulate Apple IIe speaker hardware ($C030 toggle)
- Generate audio waveform from speaker state changes
- Buffer and output audio stream at appropriate sample rate (44.1kHz or 48kHz)
- Synchronize audio with CPU cycle timing
- Handle audio thread safely from emulator thread

**Technical Approach:**
- Track speaker state (on/off) per CPU cycle
- Generate square wave audio from state transitions
- Use circular buffer for audio samples
- Apply appropriate filtering to reduce aliasing
- Implement volume control in UI

**Audio Disable Threshold:**
- Disable audio above 2.8MHz (Apple IIgs secondary speed) - configurable
- At high speeds, audio becomes unintelligible noise and wastes CPU cycles
- Threshold check uses existing `SystemStatusProvider.CurrentMhz` property

**Sample Rate Selection:**
- **Recommended: 48kHz** over 44.1kHz
- 48kHz is more common in modern systems
- Better divisibility: 48000 ÷ 60 = 800 samples per frame (clean integer)
- 44.1kHz gives 735 samples per frame (non-integer, more complex)
- Target latency: ~20-40ms (960-1920 samples @ 48kHz)
- Too low latency = buffer underruns (clicks/pops)
- Too high latency = noticeable audio lag

**Fractional Sample Tracking:**
- **Critical for quality** - prevents drift and periodic clicks
- 1.023MHz CPU ÷ 48kHz audio = 21.3125 cycles per sample (non-integer!)
- Must use accumulator pattern to track fractional samples:
  ```csharp
  // Pseudo-code for fractional tracking
  double samplesPerCycle = sampleRate / cpuFrequency; // e.g., 48000 / 1023000
  double sampleAccumulator = 0.0;

  foreach (var cycle in cpuCycles)
  {
      sampleAccumulator += samplesPerCycle;
      while (sampleAccumulator >= 1.0)
      {
          EmitAudioSample(currentSpeakerState);
          sampleAccumulator -= 1.0;
      }
  }
  ```
- Without fractional tracking, sample timing drifts and causes audio artifacts
- Accumulator must persist across VBlank frames to maintain phase coherence

**Cassette Audio Export ($C020):**
- Same toggle-based mechanism as speaker ($C030)
- Can reuse speaker audio generation routines (at 1.023MHz only)
- Export to WAV/OGG for "saving" programs to tape format
- Authentic to original hardware workflow
- Cassette save bypasses the "disable at high speed" threshold possibly
- May want separate audio routing: cassette → file, speaker → output device
- Future: Could enable loading cassette images (input side)

**Files to Create/Modify:**
- `Pandowdy.EmuCore\Interfaces\IAudioProvider.cs` - Audio output interface
- `Pandowdy.EmuCore\Services\AudioGenerator.cs` - Generates audio samples from speaker state
- `Pandowdy.EmuCore\Services\SpeakerHandler.cs` - Handles $C030 speaker toggle
- `Pandowdy.UI\Services\AudioOutputService.cs` - Cross-platform audio output
- `Pandowdy.UI\ViewModels\MainWindowViewModel.cs` - Volume control
- `Pandowdy\Program.cs` - Register audio services in DI

**Phase 3: Future Mockingboard Support (Low Priority)**

**Deferred Features:**
- Mockingboard card implementation (AY-3-8910 sound chip emulation)
- Stereo output for dual-chip Mockingboard
- Mockingboard interrupt handling
- Save/load Mockingboard state

**Notes:**
- Focus on speaker emulation first (simpler, more universally needed)
- Mockingboard is optional enhancement for later
- Architecture should allow easy addition of card-based audio

**Priority:** Medium

**Dependencies:**
- May benefit from debugger (Task 19) for debugging cycle-timing and audio sync issues

---

## Backlog

### Task 3: Removed

---

### Task 4: HGR Flicker Investigation (Medium Priority)

**Current State:**
- Apparent bug with rendering when 80-col mode is on and HGR is active
- Might exist on full-screen HGR too
- Also happens with GR mode
- The flicker is a quick swap where aux memory's contents are being drawn

**Problem:**
- Rendering corruption when 80-Column text is active with HGR/GR modes
- Probably a timing issue related to VBlank or rendering
- Looks like a race condition

**Areas to Investigate:**
- Video memory access during rendering
- Memory bank switching during render
- VBlank event handling
- Frame buffer synchronization

**Files to Focus On:**
- `Pandowdy.EmuCore\Services\LegacyBitmapRenderer.cs`
- `Pandowdy.EmuCore\Services\FrameGenerator.cs`
- `Pandowdy.EmuCore\Services\FrameProvider.cs`
- `Pandowdy.UI\Services\RenderingService.cs`

**Priority:** Medium

**Related:** See Task 8 (Race Conditions at High Speeds) ✅ COMPLETED

---

### Task 7: Handle BRK Loops in Interrupt Handler (Low Priority)

**Problem:** If there's a BRK instruction inside the BRK interrupt handler itself, it causes an infinite CPU loop.

**Current Behavior:** CPU loops indefinitely.

**Expected Behavior:** Detect and break out of BRK loops, possibly with a cycle limit or pattern detection.

**Investigation Needed:**
- Determine how real hardware handles this scenario
- Consider adding a "runaway detection" feature

**Files to Focus On:**
- `Pandowdy.EmuCore\CPUAdapter.cs`
- `legacy\6502.NET\src\Core\Emulator.cs`

**Priority:** Low

---

### Task 9: Multi-Drive Operation Deep Dive (Low Priority)

**Goal:** Verify multi-drive operation matches real Disk II hardware behavior.

**Status:** ✅ MOSTLY COMPLETE - Real-world testing recommended

**Implementation Summary:**

| Feature | Status | Notes |
|---------|--------|-------|
| 2 independent drives | ✅ COMPLETE | Each DiskIIDrive has own QuarterTrack, MotorOn, disk media |
| Single motor at a time | ✅ COMPLETE | Drive switch immediately turns off old drive's motor |
| Motor-off timing with drive switch | ✅ COMPLETE | Scheduled motor-off cancelled on switch |
| Per-drive head position | ✅ COMPLETE | Each drive maintains own QuarterTrack (0-139) - preserved across switches |
| Controller phase reset on switch | ✅ COMPLETE | `_currentPhase` cleared on drive switch |

**Architecture Notes:**

The implementation correctly separates controller state from drive state:

| State | Location | Behavior on Drive Switch |
|-------|----------|--------------------------|
| `_currentPhase` | Controller | **Cleared** - stepper coils are shared controller hardware |
| `QuarterTrack` | Each Drive | **Preserved** - models physical head position that stays where it was |
| `MotorOn` | Each Drive | Old drive turned **OFF** immediately (hardware can only power one motor) |

This matches real Disk II hardware:
- The stepper motor magnets are energized by the controller, so phases must be re-established after switching
- Each drive's head physically stays at whatever track it was on
- Software must re-activate phases to seek on the newly selected drive

**Existing Tests:**
- `MultiDrive_Drive1Selected_ByDefault`
- `MultiDrive_SelectDrive2_AffectsMotorCommands`
- `MultiDrive_SwitchingDrives_TurnsOffOldDriveMotor`
- `MultiDrive_SelectDrive1_AfterDrive2`

**Remaining Verification:**
- [ ] Test with real-world copy utilities (Copy II Plus, Locksmith, etc.)
- [ ] Optional: Add explicit test for head position preservation across drive switches

**Files:**
- `Pandowdy.EmuCore\Cards\DiskIIControllerCard.cs` (HandleDriveSelection, lines 538-571)
- `Pandowdy.EmuCore\DiskII\DiskIIDrive.cs` (QuarterTrack property preserved per-drive)
- `Pandowdy.EmuCore.Tests\DiskII\DiskIIIntegrationTests.cs` (Multi-Drive Tests region)

**Priority:** Low (core implementation complete and architecturally correct)

---

### Task 10: SectorDiskImageProvider Debugging (Medium Priority)

**Goal:** Thorough testing and debugging of sector-based disk image provider (DSK/DO/PO formats that require GCR synthesis).

**Status:** ⏳ NOT STARTED

**Problem:**
- Potential issues with DSK/DO/PO format support
- GCR synthesis from sector data needs validation
- Lower priority than WOZ/NIB debugging (Tasks 26-27) since those formats are more commonly used

**Areas to Investigate:**
- GCR encoding correctness
- Track synthesis timing
- Sector interleaving (DOS 3.3 vs ProDOS)
- Address field generation
- Checksum calculation
- Volume number handling

**Test Strategy:**
- Compare synthesized NIB output with known-good NIB files
- Test with DOS 3.3 and ProDOS system disks
- Verify sector reads return correct data
- Test both .dsk (DOS order) and .po (ProDOS order) formats
- Verify .do (DOS order) format support

**Files to Focus On:**
- `Pandowdy.EmuCore\DiskII\Providers\SectorDiskImageProvider.cs`
- `Pandowdy.EmuCore\DiskII\GcrEncoder.cs`
- `Pandowdy.EmuCore.Tests\DiskII\Providers\SectorDiskImageProviderTests.cs` (13 tests exist)

**Priority:** Medium (blocks DSK/DO/PO format support, but less urgent than WOZ/NIB fixes)

**Dependencies:**
- Should be addressed after Tasks 26-27 (WOZ/NIB debugging)
- Would benefit from debugger (Task 19) for stepping through GCR encoding and sector synthesis
- May benefit from Task 11 (conditional debug output) for cleaner debugging

---

### Task 11: Conditional Compilation for Disk Provider Debug Output (Medium Priority)

**Goal:** Reduce performance impact of debugging output in disk provider classes.

**Status:** ⏳ NOT STARTED

**Current State:**
- Disk provider classes output excessive debugging information
- Debug output causes performance degradation during normal operation
- No way to selectively enable/disable disk provider debugging

**Problem:**
- Performance is impacted by always-on informative debug output
- Too much noise in debug console during normal operation
- Need conditional compilation to control routine disk-related debugging
- Warning/error messages about unexpected conditions should remain active

**Proposed Solution:**
Wrap **informative** debugging output with conditional compilation directives. Keep warning/error messages active:

```csharp
// Wrap informative messages (routine operation status):
#if DebugDiskProviders
    Debug.WriteLine($"Track {track}, Sector {sector}, reading...");
    Debug.WriteLine($"Motor on, seeking to track {quarterTrack}");
#endif

// Keep warning/error messages active (unexpected conditions):
Debug.WriteLine($"WARNING: Invalid track number {track}, clamping to valid range");
Debug.WriteLine($"ERROR: Checksum mismatch in sector {sector}");
Debug.WriteLine($"WARNING: Disk image size {size} does not match expected {expected}");
```

**Criteria for Wrapping:**
- ✅ **Wrap:** Routine status, progress updates, normal operation flow
- ✅ **Wrap:** Verbose bit/byte-level read/write logging
- ✅ **Wrap:** Track/sector position updates during normal seeks
- ❌ **Keep Active:** Warnings about unexpected/invalid data
- ❌ **Keep Active:** Error conditions or data corruption detection
- ❌ **Keep Active:** Clamping/recovery from invalid states

**Files to Modify:**
- `Pandowdy.EmuCore\DiskII\Providers\NibDiskImageProvider.cs`
- `Pandowdy.EmuCore\DiskII\Providers\SectorDiskImageProvider.cs`
- `Pandowdy.EmuCore\DiskII\Providers\InternalWozDiskImageProvider.cs`
- `Pandowdy.EmuCore\DiskII\Providers\WozDiskImageProvider.cs`
- `Pandowdy.EmuCore\DiskII\GcrEncoder.cs` (if debug output exists)
- `Pandowdy.EmuCore\DiskII\DiskIIDrive.cs` (if debug output exists)
- `Pandowdy.EmuCore\DiskII\DiskIIDebugDecorator.cs` (may need special handling)

**Implementation Notes:**
- Use `#if DebugDiskProviders` directive (not `DEBUG` which is too broad)
- Keep debug code in place for future troubleshooting
- Consider adding documentation on how to enable disk debugging
- Verify performance improvement after changes

**Priority:** Medium (performance impact, needed sooner than later)

**Related:** See Task 8 (Race Conditions at High Speeds)

---

### Task 12: Flexible Window Docking System (Medium Priority)

**Goal:** Integrate Avalonia Dock library to provide Visual Studio-like flexible window docking for UI panels.

**Status:** ⏳ NOT STARTED

**Current State:**
- UI panels are fixed in layout (DockPanel-based)
- No ability to rearrange, float, or dock panels
- Limited workspace customization

**Problem:**
- Users cannot customize workspace layout to their preferences
- Debug panels, disk status, soft switches all in fixed positions
- No ability to maximize viewing area for specific panels
- Cannot save/restore workspace layouts

**Proposed Solution:**

Integrate the **Avalonia Dock** library (https://github.com/wieslawsoltes/Dock) to provide flexible docking capabilities.

**Features to Implement:**

1. **Dockable Panels:**
   - Apple II Display (main/center, dockable)
   - Disk Status Panel (dockable/floatable)
   - Soft Switch Status Panel (dockable/floatable)
   - Debug panels (future: CPU state, memory viewer, disassembler)
   - Performance metrics panel (future)

2. **Docking Capabilities:**
   - Drag panels to dock edges (top, bottom, left, right, center)
   - Create tabbed document groups
   - Float panels as separate windows
   - Resize docked panels with splitters
   - Auto-hide panels to maximize space

3. **Workspace Persistence:**
   - Save workspace layout on exit
   - Restore layout on startup
   - Multiple named workspace profiles (optional)
   - Reset to default layout command

4. **User Experience:**
   - Visual docking guides during drag operations
   - Right-click panel titles for close/float/dock options
   - Keyboard shortcuts for panel visibility
   - Menu: View → Panels → [Panel Name] to show/hide

**Implementation Steps:**

1. Add Avalonia.Controls.Dock NuGet package
2. Create dock factory and layout definitions
3. Migrate existing panels to dockable tool windows
4. Implement layout persistence (JSON or binary)
5. Add menu items for panel management
6. Create default layout configuration
7. Test on Windows/macOS/Linux

**Files to Create/Modify:**
- `Pandowdy.UI\Services\DockFactory.cs` - Dock layout factory
- `Pandowdy.UI\ViewModels\DockViewModel.cs` - Dock layout view model
- `Pandowdy.UI\MainWindow.axaml` - Replace DockPanel with Dock controls
- `Pandowdy.UI\ViewModels\MainWindowViewModel.cs` - Manage dock layout
- `Pandowdy\Program.cs` - Register dock services
- `Pandowdy.UI\Layouts\DefaultLayout.json` - Default workspace layout

**Technical Considerations:**
- Dock library may require specific Avalonia version compatibility
- Layout persistence format (JSON vs binary)
- Performance impact of docking system
- Integration with existing ReactiveUI patterns
- Keyboard navigation and accessibility

**Benefits:**
- Professional IDE-like user experience
- Improved workflow for debugging
- Better screen space utilization
- User customization and personalization

**Priority:** Medium

**Related:**
- Will improve GUI flexibility for future debug panels
- May benefit from Task 5 (GUI Disk Management) being completed first

---

### Task 14: Speed-Proportional Key Feeding (Low Priority)

**Goal:** Make QueuedKeyHandler adjust key feeding rate proportionally to current emulator speed.

**Status:** ⏳ NOT STARTED

**Current State:**
- QueuedKeyHandler uses fixed delay (30ms default) between keys
- Delay doesn't adapt to emulator speed changes
- When running unthrottled or at high speeds, keys still feed at normal rate
- Emulator speed available via `SystemStatusProvider.CurrentMhz`

**Problem:**
- At 1.023 MHz (normal speed), 30ms delay works well for most software
- At 20x speed (~20 MHz), keys still feed at 30ms intervals
- This creates perceived slowdown: emulator is fast but keyboard input is still slow
- Paste operations take same wall-clock time regardless of emulator speed

**Proposed Solution:**

Make QueuedKeyHandler speed-aware by adjusting timer delay proportionally to emulator speed:

```csharp
// Calculate proportional delay based on emulator speed
double speedRatio = currentMhz / 1.023; // e.g., 20.0 for 20x speed
double adjustedDelay = baseDelayMs / speedRatio; // e.g., 30ms / 20 = 1.5ms

// Apply adjusted delay to timer
_feedTimer.Change((int)adjustedDelay, Timeout.Infinite);
```

**Implementation Approach:**

1. **Inject SystemStatusProvider:**
   - Add `ISystemStatusProvider` to QueuedKeyHandler constructor
   - Read `CurrentMhz` property when starting timer

2. **Speed-Proportional Timer:**
   - Calculate speed ratio: `currentMhz / TargetMhz` (e.g., 20.46 / 1.023)
   - Adjust base delay: `baseDelay / speedRatio`
   - Apply minimum delay cap (1ms) to prevent too-fast feeding
   - Recalculate delay each time timer fires (handles throttle changes)

3. **Optional: Speed Change Detection:**
   - Subscribe to `SystemStatusProvider.Changed` event
   - Restart timer with new delay when speed changes significantly (>10% delta)
   - Provides immediate response to throttle on/off

**Example Scenarios:**

| Emulator Speed | Speed Ratio | Base Delay | Adjusted Delay | Keys/Second |
|----------------|-------------|------------|----------------|-------------|
| 1.023 MHz (1x) | 1.0 | 30ms | 30ms | ~33 keys/sec |
| 10.23 MHz (10x) | 10.0 | 30ms | 3ms | ~333 keys/sec |
| 20.46 MHz (20x) | 20.0 | 30ms | 1.5ms | ~667 keys/sec |
| Unthrottled (~700 MHz) | ~684.0 | 30ms | 1ms (capped) | 1000 keys/sec |

**Rationale:**

If 30ms delay is sufficient to feed keys at 1.023 MHz without overwhelming software, then proportionate wall-time reductions maintain the same emulated-time spacing between keys. A 20x speed increase means the emulator processes 20x more cycles in the same wall time, so keys should also arrive 20x faster in wall time to maintain the same cycle spacing.

**Files to Modify:**
- `Pandowdy.EmuCore\Services\QueuedKeyHandler.cs` - Add speed awareness
- `Pandowdy.EmuCore.Tests\QueuedKeyHandlerTests.cs` - Test speed scaling
- `Pandowdy\Program.cs` - Inject SystemStatusProvider into QueuedKeyHandler

**Technical Considerations:**
- Minimum delay cap (1ms) prevents timer instability
- Recalculate delay each timer fire handles throttle changes
- Thread safety: SystemStatusProvider.CurrentMhz is read-only, thread-safe
- Backward compatible: still works if SystemStatusProvider is null (fallback to fixed delay)

**Benefits:**
- Paste operations complete proportionally faster at high speeds
- Maintains consistent keyboard behavior across all speeds
- Better user experience when loading programs at high speed

**Priority:** Low

**Related:**
- Depends on SystemStatusProvider.CurrentMhz (already implemented)
- Complements Task 1 (throttling improvements)
- May interact with Task 8 (race conditions at high speeds)

---

### Task 15: Authentic Apple II Keyboard Repeat (Low Priority)

**Goal:** Implement authentic Apple II keyboard repeat behavior instead of relying on host OS keyboard repeat.

**Status:** ⏳ NOT STARTED

**Current State:**
- Current keyboard handling relies on host OS key repeat
- Host OS repeat rates vary by platform and user settings
- No control over repeat timing or behavior
- Not authentic to Apple IIe hardware behavior

**Problem:**
- Host OS repeat timing doesn't match Apple II repeat behavior
- Different platforms have different default repeat rates
- Some platforms may disable key repeat entirely
- Software expecting Apple II repeat timing may behave incorrectly
- No way to ensure consistent keyboard behavior across platforms

**Apple II Keyboard Repeat Specification:**

The Apple IIe has built-in keyboard repeat with specific timing:
1. **Initial Delay:** 12-18 VBlanks (~200-300ms at 60Hz) before first repeat
2. **Repeat Rate:** Every 4 VBlanks (~67ms at 60Hz, ~15 Hz repeat rate)
3. **VBlank-Synchronized:** Tied to video frame timing for consistency

**Proposed Solution:**

Implement keyboard repeat in the emulator tied to VBlank timing:

**Phase 1: Key Down/Up Detection**

1. **Capture Raw Key Events:**
   - Detect `KeyDown` events (key pressed)
   - Detect `KeyUp` events (key released)
   - Ignore host OS repeat events (detect by timestamp delta)

2. **Track Key State:**
   - Maintain currently held key
   - Track VBlank count since key pressed
   - Clear state on KeyUp

**Phase 2: VBlank-Based Repeat**

1. **Subscribe to VBlank:**
   - QueuedKeyHandler subscribes to `CpuClockingCounters.VBlankOccurred`
   - Increment VBlank counter each frame

2. **Repeat Logic:**
   ```csharp
   private byte? _heldKey;
   private int _vblanksSinceKeyDown;
   private const int REPEAT_INITIAL_DELAY = 15; // VBlanks (~250ms)
   private const int REPEAT_RATE = 4; // VBlanks (~67ms)

   void OnVBlank()
   {
       if (_heldKey == null) return;

       _vblanksSinceKeyDown++;

       if (_vblanksSinceKeyDown == REPEAT_INITIAL_DELAY)
       {
           // First repeat after initial delay
           EnqueueKey(_heldKey.Value);
       }
       else if (_vblanksSinceKeyDown > REPEAT_INITIAL_DELAY &&
                (_vblanksSinceKeyDown - REPEAT_INITIAL_DELAY) % REPEAT_RATE == 0)
       {
           // Subsequent repeats every 4 VBlanks
           EnqueueKey(_heldKey.Value);
       }
   }

   void OnKeyDown(byte key)
   {
       _heldKey = key;
       _vblanksSinceKeyDown = 0;
       EnqueueKey(key); // Initial keypress
   }

   void OnKeyUp(byte key)
   {
       if (_heldKey == key)
       {
           _heldKey = null;
           _vblanksSinceKeyDown = 0;
       }
   }
   ```

**Phase 3: Host OS Repeat Filtering**

1. **Detect OS Repeat Events:**
   - Track timestamp of last KeyDown event
   - If KeyDown arrives < 10ms after previous, likely OS repeat
   - Ignore OS repeat events (don't update _heldKey)

2. **Single Key Tracking:**
   - Only track one held key at a time (matches Apple II hardware)
   - New KeyDown replaces currently held key

**Implementation Steps:**

1. Add VBlank subscription to QueuedKeyHandler
2. Add key state tracking (held key, VBlank counter)
3. Implement OnKeyDown/OnKeyUp handlers
4. Implement VBlank-based repeat logic
5. Filter out host OS repeat events
6. Add configuration option to enable/disable (default: enabled)
7. Test with various software expecting Apple II repeat timing

**Files to Modify:**
- `Pandowdy.EmuCore\Services\QueuedKeyHandler.cs` - Add VBlank-based repeat
- `Pandowdy.EmuCore\Interfaces\IKeyboardSetter.cs` - Add OnKeyUp method
- `Pandowdy.UI\ViewModels\MainWindowViewModel.cs` - Handle KeyDown/KeyUp events
- `Pandowdy\Program.cs` - Inject CpuClockingCounters or VBlank event source
- `Pandowdy.EmuCore.Tests\QueuedKeyHandlerTests.cs` - Test repeat behavior

**Technical Considerations:**
- VBlank timing is ~60 Hz (16.67ms per frame)
- Repeat rate of 4 VBlanks = ~15 Hz repeat rate (matches Apple II)
- Must handle key changes mid-repeat (new key replaces held key)
- Must clear state on KeyUp to stop repeating
- Thread safety: VBlank fires on emulator thread, KeyDown/KeyUp on UI thread

**Benefits:**
- Authentic Apple II keyboard behavior
- Consistent repeat timing across all platforms
- Software expecting specific repeat rates works correctly
- Independent of host OS keyboard settings
- More accurate emulation

**Priority:** Low

**Related:**
- Works alongside Task 14 (speed-proportional feeding still applies)
- Requires VBlank event access (Task 1/2 VBlank migration)
- Complements existing QueuedKeyHandler architecture

---

### Task 16: Research Cross-Platform Shader Rendering for Avalonia (Low Priority)

**Goal:** Investigate modern, cross-platform GPU rendering libraries that integrate cleanly with Avalonia UI for shader-based rendering.

**Status:** ⏳ NOT STARTED

**Current State:**
- Current rendering uses CPU-based bitmap generation
- No GPU acceleration for video rendering
- Limited visual effects capabilities
- Potential performance bottleneck at high resolutions

**Problem:**
- CPU rendering may not scale well for advanced visual effects
- Future features (CRT simulation, scanlines, phosphor glow) would benefit from GPU shaders
- Cross-platform shader support required (Windows, macOS, Linux)
- Need clean integration with Avalonia UI controls

**Research Objective:**

Evaluate modern GPU rendering technologies that can be embedded in Avalonia controls and support vertex/fragment shaders for emulator display effects.

**Technologies to Research:**

1. **WebGPU.NET (Primary Candidate)**
   - Cross-platform GPU API (Windows, macOS, Linux)
   - Modern successor to WebGL
   - Native WGSL shader language
   - Strong cross-platform support
   - Repository: https://github.com/amerkoleci/Vortice.WebGPU

2. **Silk.NET WebGPU Bindings**
   - .NET 8 bindings for WebGPU
   - Part of Silk.NET ecosystem
   - Cross-platform support
   - Repository: https://github.com/dotnet/Silk.NET

3. **Silk.NET OpenGL/Vulkan (Fallback Options)**
   - Mature APIs with broad support
   - OpenGL: Simpler but aging
   - Vulkan: More complex but powerful
   - May serve as fallback or interop layer

**Research Requirements:**

1. **Avalonia Integration:**
   - Can be embedded in Avalonia control (Image, Border, or custom control)
   - Works with Avalonia's rendering pipeline
   - Handles UI thread vs render thread properly
   - Compatible with Avalonia 11.x

2. **Shader Support:**
   - Supports modern shader languages (WGSL preferred, GLSL/SPIR-V acceptable)
   - Vertex and fragment shader stages
   - Texture sampling and filtering
   - Shader uniform/constant buffers

3. **Cross-Platform:**
   - Works on Windows (D3D12, Vulkan, OpenGL backends)
   - Works on macOS (Metal backend)
   - Works on Linux (Vulkan, OpenGL backends)
   - Single API surface across platforms

4. **.NET 8 Compatibility:**
   - Works with .NET 8
   - NuGet package available
   - Active maintenance
   - Good documentation

5. **UI-Agnostic Pipeline:**
   - Doesn't require full-screen exclusive mode
   - Can render to texture/framebuffer
   - Swapchain integration
   - Vsync control

**Research Tasks:**

1. Evaluate WebGPU.NET feasibility
   - Create proof-of-concept Avalonia control
   - Test WGSL shader compilation
   - Verify cross-platform support
   - Assess performance characteristics

2. Compare Silk.NET options
   - Test WebGPU bindings
   - Compare with OpenGL/Vulkan fallbacks
   - Evaluate API ergonomics
   - Check documentation quality

3. Integration Prototyping
   - Embed in Avalonia Image control
   - Implement basic triangle rendering
   - Test texture upload/sampling
   - Measure rendering latency

4. Document Findings
   - API comparison matrix
   - Performance benchmarks
   - Integration complexity assessment
   - Recommendation for production use

**Use Cases:**

- **CRT Simulation:** Scanlines, curvature, phosphor glow
- **Color Correction:** NTSC color simulation, palette effects
- **Upscaling:** Sharp bilinear, xBR, or custom filters
- **Visual Effects:** Bloom, screen glare, monitor bezel

**Files to Create (Post-Research):**
- `docs/ShaderRenderingResearch.md` - Research findings
- `Pandowdy.UI/Services/GpuRenderer.cs` - Prototype renderer (if viable)
- `Pandowdy.UI/Controls/ShaderDisplayControl.axaml` - Custom control (if viable)

**Priority:** Low

**Related:**
- May complement Task 17 (compute shader research)
- Would enhance visual quality beyond current bitmap rendering
- Consider for future enhancement after core emulation stable

---

### Task 17: Research Compute-Shader Toolkits for Bitplane Processing (Medium Priority)

**Goal:** Evaluate GPU compute frameworks suitable for running compute shaders in a pure .NET worker thread to accelerate bitplane merging and preprocessing.

**Status:** ⏳ NOT STARTED

**Current State:**
- Bitplane merging done on CPU
- Rendering preprocessing happens in emulator thread
- No GPU compute acceleration
- Potential performance bottleneck for complex video modes

**Problem:**
- CPU-based bitplane processing may not scale for advanced features
- Complex video modes (double hi-res, interlaced) require heavy preprocessing
- Emulator thread contention between CPU emulation and rendering prep
- Could benefit from parallel GPU compute pipelines

**Research Objective:**

Evaluate GPU compute technologies that can run headless (no UI dependency) in a .NET worker thread to accelerate rendering data stream preprocessing.

**Technologies to Research:**

1. **WebGPU.NET Compute Pipelines (Primary Candidate)**
   - Modern GPU compute API
   - Cross-platform support
   - WGSL compute shader language
   - Headless compute device creation
   - Repository: https://github.com/amerkoleci/Vortice.WebGPU

2. **Silk.NET WebGPU Compute Bindings**
   - .NET 8 bindings for WebGPU compute
   - Part of Silk.NET ecosystem
   - Cross-platform compute support
   - Repository: https://github.com/dotnet/Silk.NET

3. **Silk.NET Vulkan Compute (Optional Comparison)**
   - More complex but powerful
   - Broader hardware support (older GPUs)
   - SPIR-V compute shaders
   - May serve as fallback option

**Research Requirements:**

1. **Headless Compute Execution:**
   - No UI window or swapchain required
   - Can run in background worker thread
   - Independent of Avalonia UI thread
   - Pure compute pipeline (no graphics)

2. **Shader Language Support:**
   - WGSL compute shaders (preferred)
   - SPIR-V compute shaders (acceptable fallback)
   - Shader compilation at runtime or compile-time
   - Compute shader debugging capabilities

3. **Cross-Platform:**
   - Windows (D3D12 compute, Vulkan compute)
   - macOS (Metal compute)
   - Linux (Vulkan compute)
   - Consistent API across platforms

4. **.NET 8 Worker Thread Architecture:**
   - Compatible with Task-based async patterns
   - Thread-safe buffer management
   - Efficient CPU↔GPU data transfer
   - Minimal GC pressure

5. **Performance Characteristics:**
   - Low overhead for small compute jobs
   - Efficient buffer mapping
   - Async compute queue support
   - Batch processing capabilities

**Research Tasks:**

1. **Evaluate WebGPU.NET Compute:**
   - Create headless compute device
   - Implement test compute shader (bitplane merge)
   - Measure CPU↔GPU transfer overhead
   - Test on all target platforms

2. **Compare Alternative Options:**
   - Test Silk.NET WebGPU compute bindings
   - Evaluate Vulkan compute as fallback
   - Compare API ergonomics
   - Assess documentation quality

3. **Prototype Bitplane Merging:**
   - Implement WGSL compute shader for bitplane operations
   - Test with realistic emulator data (48KB main + 48KB aux)
   - Measure end-to-end latency
   - Compare with CPU baseline performance

4. **Worker Thread Integration:**
   - Test compute in background thread
   - Measure synchronization overhead
   - Evaluate buffer pooling strategies
   - Test under high load scenarios

5. **Document Findings:**
   - Performance comparison (CPU vs GPU compute)
   - API complexity assessment
   - Cross-platform compatibility report
   - Recommendation for production use

**Use Cases:**

- **Bitplane Merging:** Combine main + aux memory for 80-column mode
- **Mode Switching:** Fast conversion between text/lores/hires modes
- **Color Processing:** Apple II color artifact simulation
- **Interlaced Modes:** Field merging for hi-res interlaced
- **Future Features:** Filter chains, post-processing effects

**Bitplane Processing Example:**

```wgsl
// WGSL compute shader for bitplane merging
@group(0) @binding(0) var<storage, read> mainMem: array<u32>;
@group(0) @binding(1) var<storage, read> auxMem: array<u32>;
@group(0) @binding(2) var<storage, write> output: array<u32>;

@compute @workgroup_size(64)
fn merge_bitplanes(@builtin(global_invocation_id) id: vec3<u32>) {
    let idx = id.x;
    if (idx >= arrayLength(&output)) {
        return;
    }

    // Merge main and aux memory bitplanes
    let main = mainMem[idx];
    let aux = auxMem[idx];
    output[idx] = interleave_bits(main, aux);
}
```

**Files to Create (Post-Research):**
- `docs/ComputeShaderResearch.md` - Research findings and benchmarks
- `Pandowdy.EmuCore/Services/GpuComputeService.cs` - Compute service (if viable)
- `Pandowdy.EmuCore/Shaders/BitplaneMerge.wgsl` - Example compute shader

**Priority:** Medium

**Related:**
- Complements Task 16 (shader rendering research)
- Would offload work from emulator thread
- May improve performance for complex video modes
- Consider after core emulation stable

---

### Task 20: Advanced Debugger Features (Low Priority)

**Goal:** Expand the basic debugger (Task 19) with advanced features for comprehensive debugging and development support.

**Status:** ⏳ NOT STARTED

**Prerequisites:**
- Task 19 (Basic Debugger Implementation) must be completed first
- This builds on the foundation established in Task 19

**Current State (Post-Task 19):**
- Basic breakpoints, watches, and stepping implemented
- CPU state display working
- Need additional features for advanced debugging scenarios

**Scope: Advanced Features**
This task adds features beyond the essential debugging capabilities, providing a more comprehensive development experience.

**Features to Implement:**

**1. Memory Viewer/Editor**
- Hex dump view of memory ranges ($0000-$FFFF)
- ASCII representation alongside hex
- Edit memory values directly
- Jump to address
- Memory search (pattern, string, value)
- Memory compare (find differences between snapshots)
- Highlight recently changed bytes

**2. Disassembly View**
- Real-time disassembly of memory
- Scrollable disassembly window
- Show labels/symbols if available
- Highlight current PC location
- Click to set breakpoints on instructions
- Show branch targets and jump destinations
- Optional: cycle count per instruction

**3. Enhanced Breakpoints**
- Data breakpoints (break on memory read/write)
- Range breakpoints (break when PC in range)
- Hardware breakpoints (break on I/O access, e.g., $C0xx)
- Breakpoint hit counts (break after N hits)
- Breakpoint groups (enable/disable groups)
- Breakpoint import/export (save/load breakpoint sets)
- Log breakpoints (log message without stopping)

**4. Execution History/Trace**
- Instruction trace buffer (last N instructions executed)
- Trace to file for offline analysis
- Trace filtering (e.g., only JSR/RTS, only memory writes)
- Reverse debugging (step backward through trace)
- Trace visualization (call graph, execution flow)

**5. Symbol Support**
- Load symbol files (various formats)
- Display symbols in disassembly
- Symbol-aware breakpoints (break on function name)
- Symbol browser panel
- Auto-detect common ROM entry points

**6. Stack Visualization**
- Graphical stack view
- Show return addresses
- Stack frame boundaries
- JSR/RTS matching
- Stack depth indicator

**7. I/O and Hardware Debugging**
- Soft switch state panel (expanded view)
- I/O access logging
- Peripheral card state inspection
- Disk II head position/motor state
- Video mode state visualization
- Memory bank switching visualization

**8. Scripting/Automation**
- Debugger scripting (simple command language or C# scripting)
- Automated test scripts
- Breakpoint actions (run script on break)
- Memory validation scripts
- Batch debugging operations

**9. Performance Profiling**
- Instruction execution counts
- Hotspot detection (most executed addresses)
- Time spent in routines
- Call frequency analysis
- Memory access patterns

**10. State Snapshots**
- Save/load complete emulator state
- Quick save slots
- State comparison (diff two states)
- State annotations/notes
- Auto-save on breakpoint hit

**Files to Create:**

*Core Components:*
- `Pandowdy.EmuCore\Debugging\MemoryViewer.cs` - Memory view/edit logic
- `Pandowdy.EmuCore\Debugging\Disassembler.cs` - Disassembly engine
- `Pandowdy.EmuCore\Debugging\TraceBuffer.cs` - Execution trace buffer
- `Pandowdy.EmuCore\Debugging\SymbolTable.cs` - Symbol management
- `Pandowdy.EmuCore\Debugging\DataBreakpoint.cs` - Data breakpoint support
- `Pandowdy.EmuCore\Debugging\Profiler.cs` - Execution profiler

*UI Components:*
- `Pandowdy.UI\Controls\MemoryViewerPanel.axaml` - Memory hex viewer
- `Pandowdy.UI\Controls\DisassemblyPanel.axaml` - Disassembly view
- `Pandowdy.UI\Controls\TracePanel.axaml` - Execution trace view
- `Pandowdy.UI\Controls\StackPanel.axaml` - Stack visualization
- `Pandowdy.UI\Controls\ProfilerPanel.axaml` - Profiling results

**Technical Considerations:**
- Memory viewer must be efficient for large address spaces
- Trace buffer needs ring buffer for bounded memory usage
- Disassembler should cache results for performance
- Symbol loading should be lazy/async
- Consider performance impact of data breakpoints (every memory access)
- Profiling should be toggleable to avoid performance overhead

**UI Integration:**
- All panels dockable (integrates with Task 12)
- Consistent keyboard shortcuts across panels
- Context menus for common operations
- Toolbar for panel visibility

**Priority:** Low (nice-to-have after core debugger works)

**Dependencies:**
- **Requires:** Task 19 (Basic Debugger Implementation)
- **Related:** Task 12 (Flexible Window Docking System)

---

### Task 21: Peripheral Discovery and Enumeration API (Medium Priority)

**Goal:** Add a method to `IEmulatorCoreInterface` that allows the GUI to query what peripherals (disk drives, printers, serial cards, etc.) are present in the emulated system, enabling dynamic UI configuration.

**Status:** ⏳ NOT STARTED

**Current State:**
- GUI is currently hardcoded to display specific panels (Disk Status, Soft Switch Status, CPU Status)
- No way for GUI to discover what peripherals are actually installed in emulator
- Adding new peripheral cards requires manual UI code changes
- UI cannot adapt to different machine configurations

**Problem:**
- When peripherals are added/removed from emulator configuration, UI must be manually updated
- No discoverability mechanism for what hardware is present
- GUI cannot dynamically create appropriate panels/controls for installed peripherals
- Difficult to support user-configurable hardware configurations
- Future peripheral additions (printer, serial card, modem, etc.) require UI refactoring

**Proposed Solution:**

Add peripheral discovery API to `IEmulatorCoreInterface` that returns a structured description of installed hardware:

```csharp
public interface IEmulatorCoreInterface
{
    // ... existing members ...

    /// <summary>
    /// Gets the collection of peripherals installed in the emulated system.
    /// </summary>
    /// <returns>Read-only collection of peripheral descriptors.</returns>
    IReadOnlyList<IPeripheralDescriptor> GetInstalledPeripherals();
}

/// <summary>
/// Describes a peripheral device installed in the emulated system.
/// </summary>
public interface IPeripheralDescriptor
{
    /// <summary>Gets the peripheral type (DiskDrive, Printer, SerialCard, etc.).</summary>
    PeripheralType Type { get; }

    /// <summary>Gets the slot number (1-7) or 0 for motherboard peripherals.</summary>
    int Slot { get; }

    /// <summary>Gets a human-readable name for UI display.</summary>
    string DisplayName { get; }

    /// <summary>Gets the unique identifier for this peripheral instance.</summary>
    string Id { get; }

    /// <summary>Gets peripheral-specific metadata (drive count, baud rate, etc.).</summary>
    IReadOnlyDictionary<string, object> Metadata { get; }
}

public enum PeripheralType
{
    DiskController,     // Disk II controller (2 drives)
    Printer,            // Parallel printer interface
    SerialCard,         // Serial/modem card
    MockingboardCard,   // Mockingboard audio card
    MouseCard,          // Mouse interface
    ClockCard,          // Real-time clock
    RamCard,            // Expansion RAM card
    // Future expansion...
}
```

**Implementation Approach:**

**1. Core Infrastructure:**
- Create `IPeripheralDescriptor` interface and implementation
- Create `PeripheralType` enum with common peripheral types
- Add `GetInstalledPeripherals()` to `IEmulatorCoreInterface`

**2. Card Enumeration:**
- Iterate through all 7 expansion slots
- Query each slot via `ICard` interface
- Build descriptor from card metadata
- Include motherboard peripherals (built-in keyboard, display, speaker)

**3. Metadata Examples:**
```csharp
// Disk II Controller
{
    Type = PeripheralType.DiskController,
    Slot = 6,
    DisplayName = "Disk II Controller",
    Id = "diskii-slot6",
    Metadata = {
        ["DriveCount"] = 2,
        ["Drive1ImagePath"] = "C:\\disks\\dos33.dsk",
        ["Drive2ImagePath"] = null,
        ["SupportedFormats"] = new[] { "dsk", "do", "po", "nib", "woz" }
    }
}

// Future: Printer Card
{
    Type = PeripheralType.Printer,
    Slot = 1,
    DisplayName = "Parallel Printer Interface",
    Id = "printer-slot1",
    Metadata = {
        ["OutputPath"] = "C:\\output\\printout.txt",
        ["Emulation"] = "TextFile"
    }
}
```

**4. GUI Adaptation:**
- MainWindow queries `GetInstalledPeripherals()` on startup
- Dynamically creates appropriate status panels based on installed hardware
- Disk Status Panel only shown if DiskController present
- Future panels (Printer Status, Serial Status) created dynamically
- Panel visibility saved per peripheral type

**Use Cases:**

**Current:**
- Discover Disk II controller in slot 6 → show Disk Status Panel
- Discover no printer → don't show Printer Status Panel

**Future:**
- User configures different slot assignments → UI adapts automatically
- User adds Mockingboard card → Audio Status Panel appears
- User adds printer card → Printer Status Panel appears with control buttons
- Support for multiple disk controllers (rare but possible)

**Benefits:**
- ✅ GUI automatically adapts to emulator configuration
- ✅ No manual UI updates needed when adding peripherals
- ✅ Supports user-configurable hardware setups
- ✅ Foundation for future peripheral additions
- ✅ Clean separation between core and UI concerns
- ✅ Makes future Task 5 (GUI Disk Management) more flexible

**Files to Create:**
- `Pandowdy.EmuCore\Interfaces\IPeripheralDescriptor.cs` - Peripheral descriptor interface
- `Pandowdy.EmuCore\DataTypes\PeripheralDescriptor.cs` - Descriptor implementation
- `Pandowdy.EmuCore\DataTypes\PeripheralType.cs` - Peripheral type enum

**Files to Modify:**
- `Pandowdy.EmuCore\Interfaces\IEmulatorCoreInterface.cs` - Add GetInstalledPeripherals()
- `Pandowdy.EmuCore\VA2M.cs` - Implement peripheral enumeration
- `Pandowdy.EmuCore\Interfaces\ICard.cs` - Add metadata properties (optional)
- `Pandowdy.UI\MainWindow.axaml.cs` - Query peripherals and create panels dynamically
- `Pandowdy.UI\ViewModels\MainWindowViewModel.cs` - Dynamic panel view model creation

**Technical Considerations:**
- Thread safety: Query should be safe from UI thread
- Performance: Cache peripheral list, invalidate only on configuration change
- Extensibility: Metadata dictionary allows arbitrary peripheral-specific data
- Backward compatibility: Existing cards work without metadata (return defaults)
- Observable pattern: Optional `IObservable<IPeripheralDescriptor>` for hot-plug support (future)

**Priority:** Medium

**Dependencies:**
- None (pure addition to existing interface)

**Related:**
- **Enables:** Dynamic panel creation for future peripherals
- **Complements:** Task 5 (GUI Disk Management Features)
- **Complements:** Task 12 (Flexible Window Docking System)
- **Foundation for:** Future printer, serial, audio peripheral support

---

### Task 23: Split IKeyboardSetter into IKeyboardSetter and IKeyboardResetter (Low Priority)

**Goal:** Refactor `IKeyboardSetter` interface to follow Interface Segregation Principle by splitting keyboard input injection and keyboard reset into separate interfaces.

**Status:** ⏳ NOT STARTED

**Current State:**
- `IKeyboardSetter` contains both `EnqueueKey()` and `ResetKeyboard()` methods
- `IEmulatorCoreInterface` inherits from `IKeyboardSetter`, causing method name collision with its own `Reset()` method
- Currently resolved by renaming keyboard reset to `ResetKeyboard()` to avoid collision
- Interface mixing two concerns: input injection and state reset

**Problem:**
- Interface Segregation Principle violation: clients that only need key injection must also implement reset
- Name collision between `IEmulatorCoreInterface.Reset()` (full system reset) and `IKeyboardSetter.ResetKeyboard()` (keyboard state only)
- Future implementations might only need one interface or the other
- Mixed responsibility makes the interface less focused

**Proposed Solution:**

Split `IKeyboardSetter` into two focused interfaces:

```csharp
/// <summary>
/// Interface for injecting keyboard input into the emulator.
/// </summary>
public interface IKeyboardSetter
{
    /// <summary>
    /// Enqueues a raw key value with strobe bit automatically set.
    /// </summary>
    void EnqueueKey(byte key);
}

/// <summary>
/// Interface for resetting keyboard state to power-on defaults.
/// </summary>
public interface IKeyboardResetter
{
    /// <summary>
    /// Resets the keyboard state to power-on defaults, clearing pending keystrokes and strobe.
    /// </summary>
    void ResetKeyboard();
}
```

**Implementation Changes:**

1. **Split Interface:**
   - Create new `IKeyboardResetter` interface with `ResetKeyboard()` method
   - Keep `IKeyboardSetter` with only `EnqueueKey()` method
   - Update documentation for both interfaces

2. **Update Implementations:**
   - `SingularKeyHandler` implements both `IKeyboardSetter` and `IKeyboardResetter`
   - `QueuedKeyHandler` implements both interfaces (if exists)
   - No behavior changes, just interface compliance

3. **Update Consumers:**
   - `IEmulatorCoreInterface` inherits only from `IKeyboardSetter` (removes `ResetKeyboard()` from interface contract)
   - `VA2M` takes both `IKeyboardSetter` and `IKeyboardResetter` as constructor parameters (or single instance implementing both)
   - `VA2M.Reset()` calls injected `IKeyboardResetter.ResetKeyboard()`
   - `VA2M.EnqueueKey()` delegates to injected `IKeyboardSetter.EnqueueKey()`
   - DI container registers `SingularKeyHandler` for both interface types

4. **Remove Explicit ResetKeyboard() from IEmulatorCoreInterface:**
   - Since `IEmulatorCoreInterface.Reset()` internally calls keyboard reset, the explicit `ResetKeyboard()` method exposure becomes optional
   - Simplifies the interface - clients call `Reset()` for full system reset (including keyboard)

**Benefits:**
- ✅ Follows Interface Segregation Principle (single responsibility per interface)
- ✅ Eliminates method name collision concerns
- ✅ Clients that only need input injection don't need reset capability
- ✅ Future test fixtures can mock just the interface they need
- ✅ Cleaner separation of concerns (input vs. state management)
- ✅ Better encapsulation - reset is internal implementation detail

**Files to Create:**
- `Pandowdy.EmuCore\Interfaces\IKeyboardResetter.cs` - New reset interface

**Files to Modify:**
- `Pandowdy.EmuCore\Interfaces\IKeyboardSetter.cs` - Remove `ResetKeyboard()` method
- `Pandowdy.EmuCore\Interfaces\IEmulatorCoreInterface.cs` - Remove inheritance from full `IKeyboardSetter`, add targeted inheritance or remove `ResetKeyboard()` exposure
- `Pandowdy.EmuCore\Services\SingularKeyHandler.cs` - Implement both interfaces
- `Pandowdy.EmuCore\VA2M.cs` - Update constructor to accept both interfaces, update delegation
- `Pandowdy\Program.cs` - Update DI registration for both interface types
- `Pandowdy.EmuCore.Tests\SingularKeyHandlerTests.cs` - Update test assertions for split interfaces

**Technical Considerations:**
- DI container can register same instance (`SingularKeyHandler`) for both interface types
- No runtime behavior changes - purely structural refactoring
- Backward compatible if new interface added before old one removed
- Can be done incrementally (add `IKeyboardResetter`, then refactor consumers, then remove from `IKeyboardSetter`)

**Alternative Considered:**
- Rename `IKeyboardSetter.ResetKeyboard()` to `ResetKeyboard()` (current solution)
- **Chosen Alternative:** Current workaround with `ResetKeyboard()` name is acceptable for now, but proper split is cleaner long-term

**Priority:** Low (current workaround is functional, this is architectural cleanup)

**Dependencies:**
- None (pure refactoring)

**Related:**
- Improves architecture established in Task 6 (Clear Pending Keystrokes on Reset)
- Makes interface design more consistent with SOLID principles

---

## Completed Tasks

### ✅ Task 1: Migrate VA2M to CpuClockingCounters.VBlankOccurred

---

### ✅ Task 2: Remove VA2MBus.VBlank Event

---

### ✅ Task 3: Removed

---

### ✅ Task 6: Clear Pending Keystrokes on Reset

**Completed:** 2025-01-25 - All 1235 tests passing

---

### ✅ Task 8: Check for Race Conditions at High Speeds

**Completed:** 2025-02-04 - All 3378 tests passing

---

### ✅ Task 18: Migrate to Pandowdy.Cpu

**Completed:** 2025-01-27 - All 1206 tests passing

---

### ✅ Task 19: Basic Debugger Foundation

**Completed:** 2025-02-04 - All 3378 tests passing

---

### ✅ Task 24: Fix DiskII Motor-Off Behavior on Drive Switching

**Completed:** 2025-01-28 - All 53 DiskII tests passing

---

## Code Style Guidelines
1. `StopEmulator()` cancelled the token and set `IsRunning = false`
2. Cleanup of `_emuCts` and `_emuTask` happened asynchronously via `Dispatcher.UIThread.Post()`
3. If `OnEmuStartClicked()` was called before cleanup finished, it saw `_emuCts != null` (the old cancelled one) and returned early
4. Eventually the continuation ran and set `IsRunning = false` again, but no new emulator thread was started
5. **Result:** UI showed "paused" but no emulator thread existed - the emulator was in a "zombie" state

**Fix Applied:**
1. **Added lock object** (`_emuStateLock`) to synchronize start/stop operations
2. **`OnEmuStartClicked` now:**
   - Checks if there's a pending task that's still running (not just checking CTS)
   - Cleans up completed task state synchronously before creating new CTS
   - Uses lock to prevent race conditions
3. **`StopEmulator` now:**
   - Uses lock to safely get CTS/Task references
   - Waits up to 100ms for the task to acknowledge cancellation (should be near-instant)
   - Cleans up state immediately instead of relying on async continuation
   - Ensures the emulator is fully stopped before returning

**Architecture Improvement:**
Removed reactive push pattern from `VA2M.PublishState()` that was causing 1000+ Hz state updates in unthrottled mode:
- GUI now runs at 60 Hz polling rate (controlled by `IRefreshTicker`)
- ViewModels poll at 20 Hz (sampled from the 60 Hz ticker)
- Emulator runs at any speed without pushing state updates
- MHz calculated at 4 Hz (every 0.25s) and stored for query
- Eliminates boolean boxing from flag bindings at emulation speed

**Files Modified:**
- `Pandowdy.UI\MainWindow.axaml.cs` - Added lock synchronization for start/stop
- `Pandowdy.EmuCore\VA2M.cs` - Removed PublishState(), changed MHz reporting to 0.25s
- `Pandowdy.UI\ViewModels\EmulatorStateViewModel.cs` - Refactored to pull architecture, removed LineNumber
- `Pandowdy.UI.Tests\ViewModels\EmulatorStateViewModelTests.cs` - Cleaned up (TODO comments for rewrite)
- `Pandowdy.UI.Tests\ViewModels\MainWindowViewModelTests.cs` - Fixed constructor call

**Benefits:**
- Eliminates lockup when rapidly toggling F5 (pause/continue) and F9 (throttle toggle)
- Prevents emulator "zombie" state where UI shows paused but no thread exists
- Robust against rapid key combinations like F5-F9-F5-F9...
- Cleaner architecture with pull-based state management
- Significantly reduced GC pressure in unthrottled mode
- Boolean boxing only occurs at UI refresh rate (20 Hz) instead of emulation speed (700+ Hz)

**Related Issues:**
- Task 4 (HGR Flicker Investigation) may also benefit from this fix
- This was the root cause of reported freezing in Release mode unthrottled

---

### ✅ Task 25: Disk II Motor State Refactoring - Move to Controller Level

**Completed:** 2026-01 - All 2039 tests passing

**Goal:** Move motor state management from individual drive implementations to controller level, reflecting the hardware reality that the controller has a single motor line powering only one drive at a time.

**Architecture Change:**
- **Before:** Each drive tracked its own `MotorOn` property; controller read/wrote this property
- **After:** Controller owns motor state (Off/On/ScheduledOff) and active drive index; drives are passive mechanical devices

**Motivation:**
- Hardware accuracy: Real Disk II controller has ONE motor line that powers the selected drive
- Simplified drive implementation: Drives are now purely mechanical (head position, disk I/O)
- Clearer separation of concerns: Controller manages electrical/motor control, drives manage mechanical operations
- Eliminated conceptual confusion: No more "motor on Drive 1" vs "motor on Drive 2" - there's ONE motor

**Implementation:**
Completed in 8 phases over multiple sessions (see `docs/DiskII-Motor-Refactoring-Plan.md`):
1. **Phase 1:** Added shadow motor state to controller (additive only)
2. **Phase 2:** Dual-track writes to both old and new state (backward compatible)
3. **Phase 3:** Switched all reads to use controller state (drive state no longer consulted)
4. **Phase 4:** Removed `MotorOn` property from `IDiskIIDrive` interface and implementations
5. **Phase 5:** Updated all test infrastructure (removed 14 drive motor tests, fixed 9 integration tests)
6. **Phase 6:** Integrated into Phase 5 (all test fixes completed)
7. **Phase 7:** Completed in Phase 4 (all drive motor synchronization removed)
8. **Phase 8:** Documentation updates (XML comments, Copilot instructions, this roadmap)

**Key Changes:**
- `DiskIIControllerCard`: Added `DiskIIMotorState` enum and `_motorState` field
- `DiskIIControllerCard.IsMotorRunning`: Internal property for motor state checks (exposed to tests)
- `IDiskIIDrive`: Removed `MotorOn` property
- `DiskIIDrive` / `NullDiskIIDrive`: Removed motor state tracking
- `DiskIIStatusDecorator`: Removed motor state forwarding (only publishes mechanical state)
- Tests: Removed per-drive motor tests, updated to check `controller.IsMotorRunning`

**Behavior Changes:**
- Drive switching with motor on: Motor stays ON (just powers newly selected drive)
- Previously: Appeared as if each drive had independent motor (incorrect hardware model)
- Now: Single motor line accurately modeled

**Files Modified:**
Production code (9 files):
- `Pandowdy.EmuCore/Interfaces/IDiskIIDrive.cs` - Updated documentation, removed MotorOn
- `Pandowdy.EmuCore/Cards/DiskIIControllerCard.cs` - Added motor state management
- `Pandowdy.EmuCore/DiskII/DiskIIDrive.cs` - Removed motor tracking
- `Pandowdy.EmuCore/DiskII/NullDiskIIDrive.cs` - Removed motor tracking
- `Pandowdy.EmuCore/DiskII/DiskIIDebugDecorator.cs` - Removed motor passthrough
- `Pandowdy.EmuCore/DiskII/DiskIIStatusDecorator.cs` - Removed motor forwarding
- `Pandowdy.EmuCore/Pandowdy.EmuCore.csproj` - Added InternalsVisibleTo for tests
- `.github/copilot-instructions.md` - Added Disk II motor architecture section
- `docs/Development-Roadmap.md` - This entry

Test code (6 files):
- `Pandowdy.EmuCore.Tests/DiskII/DiskIIDriveTests.cs` - Removed 7 motor tests
- `Pandowdy.EmuCore.Tests/DiskII/NullDiskIIDriveTests.cs` - Removed 4 motor tests
- `Pandowdy.EmuCore.Tests/DiskII/DiskIIDebugDecoratorTests.cs` - Removed 3 motor tests
- `Pandowdy.EmuCore.Tests/Cards/DiskIIControllerCardTests.cs` - Fixed 2 tests
- `Pandowdy.EmuCore.Tests/DiskII/DiskIISpecificationTests.cs` - Fixed 1 test
- `Pandowdy.EmuCore.Tests/DiskII/DiskIIIntegrationTests.cs` - Fixed 6 tests

**Verification:**
- All 2039 tests passing (1766 EmuCore + 126 Disassembler + 147 UI)
- Build successful
- No skipped tests
- Hardware-accurate behavior validated

**Benefits:**
- More accurate hardware emulation
- Simplified drive implementations (passive mechanical devices)
- Clearer code - motor state checks are explicitly controller-level
- Better testability - single source of truth for motor state
- Documentation accurately reflects implementation

**Reference:**
See `docs/DiskII-Motor-Refactoring-Plan.md` for detailed phase-by-phase execution plan and notes.

---

## Code Style Guidelines

> **Source:** `.github/copilot-instructions.md`

### 1. Always Use Curly Braces

**Required for:** `if`, `else`, `for`, `foreach`, `while`, `do-while`, `using`, `lock`

```csharp
// Correct: Always use braces
if (condition)
{
    DoSomething();
}

foreach (var item in collection)
{
    ProcessItem(item);
}

// Incorrect: Missing braces (will cause IDE0011 warning)
if (condition)
    DoSomething();
```

### 2. Property Formatting

**Multi-line format for properties with:**
- Non-default accessors (getters/setters with logic or access modifiers)
- Private setters
- Logic in get/set

```csharp
// Correct: Multi-line for properties with logic
public bool ThrottleEnabled
{
    get => _throttleEnabled;
    set => this.RaiseAndSetIfChanged(ref _throttleEnabled, value);
}

// Correct: Single-line for simple auto-properties
public string Title { get; set; }
public int Count { get; init; }
public bool IsEnabled { get; set; } = true;
```

### 3. Other Style Guidelines

- Prefer expression-bodied members for simple one-liners
- Use nullable reference types (`string?`, `object?`)
- Follow naming conventions: PascalCase for public members, camelCase for private fields with `_` prefix
- 4-space indentation
- Prefer using Primary Constructors when class initialization is straightforward
- Prefer field default initializers for small, read‑only arrays and collections; use new[] { ... } for literal content and Array.Empty<T>() for empty arrays to avoid unnecessary allocations.
- Use `var` for other local variables when type is obvious

### 4. Dependency Injection and Architecture

**Prefer Dependency Injection over other patterns:**
- ✅ **Use Constructor Injection** for required dependencies
- ✅ **Depend on interfaces** rather than concrete implementations
- ✅ **Register services in DI container** (`Program.cs`) with appropriate lifetimes
- ❌ **Avoid Chain of Command** patterns where classes pass references through multiple layers
- ❌ **Avoid internal `new` instantiation** of dependencies (hard to test, tight coupling)
- ❌ **Avoid Service Locator** pattern (anti-pattern in modern .NET)

**Benefits:**
- **Testability:** Easy to inject mocks/fakes in unit tests
- **Separation of Concerns:** Classes don't manage creation of dependencies
- **Flexibility:** Easy to swap implementations without changing dependent code
- **Explicit Dependencies:** Constructor parameters document what a class needs

**Examples:**

```csharp
// ✅ Correct: Dependencies injected via constructor
public class AudioGenerator(
    IAudioProvider audioProvider,
    ISystemStatusProvider statusProvider)
{
    private readonly IAudioProvider _audioProvider = audioProvider;
    private readonly ISystemStatusProvider _statusProvider = statusProvider;

    public void GenerateSamples()
    {
        // Use injected dependencies
        if (_statusProvider.CurrentMhz > 2.8)
        {
            return; // Audio disabled at high speeds
        }
        _audioProvider.QueueSamples(...);
    }
}

// ✅ Register in Program.cs
services.AddSingleton<IAudioProvider, OpenALAudioProvider>();
services.AddSingleton<ISystemStatusProvider, SystemStatusProvider>();
services.AddSingleton<AudioGenerator>();

// ❌ Incorrect: Chain of Command - passing through multiple layers
public class VA2M
{
    public VA2M(ISystemStatusProvider statusProvider)
    {
        var renderer = new Renderer(statusProvider); // Don't do this!
    }
}

// ❌ Incorrect: Internal instantiation
public class AudioGenerator
{
    private readonly IAudioProvider _audioProvider = new OpenALAudioProvider(); // Hard to test!

    public void GenerateSamples()
    {
        _audioProvider.QueueSamples(...);
    }
}

// ❌ Incorrect: Service Locator (anti-pattern)
public class AudioGenerator
{
    public void GenerateSamples()
    {
        var provider = ServiceLocator.Get<IAudioProvider>(); // Hidden dependency!
        provider.QueueSamples(...);
    }
}
```

**Constructor Guidelines:**
- For classes with many dependencies (>5 parameters), consider:
  - Grouping related dependencies into facade interfaces
  - Using primary constructors for cleaner syntax (when appropriate)
  - Reviewing whether class has too many responsibilities (SRP violation)
- Document constructor parameters when dependency purpose isn't obvious
- Prefer readonly fields for injected dependencies

**Lifetime Guidelines:**
- **Singleton:** Stateless services, global state providers (`ISystemStatusProvider`)
- **Scoped:** Per-request or per-window services (Avalonia windows)
- **Transient:** Lightweight, stateless services created per use
- Avoid Singleton for services that hold mutable state accessed by multiple threads

---

## Git Best Practices

### File Operations

**ALWAYS use `git mv` when moving or renaming files to preserve Git history**

```bash
# Correct: Preserves history
git mv old/path/File.cs new/path/File.cs

# Incorrect: Loses history
# create_file(new/path/File.cs)
# remove_file(old/path/File.cs)
```

### Commit Strategy

- Check for `.git` directory before file operations
- Incremental commits after each major change
- Descriptive commit messages
- Prefer Git-aware commands in version-controlled workspaces

---

## Testing Guidelines

### Avalonia Headless Testing

- Use `[Fact]` for standard tests that don't need UI thread
- Use `[AvaloniaFact]` for tests requiring Avalonia dispatcher/threading
- Use `[Fact(Skip = "reason")]` for tests blocked by technical limitations
- Keep skipped tests with good documentation - they serve as design specs

### Test Organization

- Mirror production code structure in test projects
- Use test fixtures for complex setup
- Group tests with `#region` blocks
- Name tests: `MethodName_Scenario_ExpectedOutcome`

### What Works in Headless Mode

- ✅ ViewModels (ReactiveObject)
- ✅ ReactiveUI properties and commands
- ✅ DispatcherTimer operations
- ✅ Observable streams
- ✅ ReactiveWindow activation lifecycle
- ✅ Full window rendering

---

## Other Notes

### Considerations for cross-platform development

- Check the Control/Cmd mappings in the GUI to make sure things work as expected (Cmd toggles options, Ctrl passes to Emulator Core)

---

## Document Maintenance

**⚠️ AUTHORITATIVE SOURCE:** This document is the **single source of truth** for all planned updates, scheduled tasks, and development priorities. Do not maintain parallel task lists elsewhere.

**This document is a living reference.** Update it as:
- Tasks are completed
- New tasks are identified
- Priorities change
- Architectural decisions are made

**Table of Contents Formatting:**
- Top-level sections numbered (1, 2, 3...) and are left-justified with no indentation
- Task entries indented under their parent section
- - Always use proper indentation (3 spaces + dash) for task entries
- Verify all anchor links work correctly

**Status Indicators:**
- ✅ COMPLETED
- ⏳ IN PROGRESS
- ⏳ NOT STARTED
- ⚠️ BLOCKED
- 🔍 NEEDS INVESTIGATION

---

*Document Created: 2025-01-XX*  
*Last Updated: Initial creation - migrated from DiskII-Integration-Plan.md*
