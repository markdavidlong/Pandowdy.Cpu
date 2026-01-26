# Pandowdy Development Roadmap

> **📌 IMPORTANT:** This document is the **single source of truth** for all planned updates, scheduled tasks, and development priorities in the Pandowdy project. All AI assistants and team members should consult this document when planning or implementing features.

---

## 📊 Current State Summary

| Status | Details |
|--------|---------|
| **Branch** | `develop` |
| **Tests** | 1235 tests passing ✅ |
| **Last Milestone** | Keyboard Reset Implementation (Task 6) |
| **Next Focus** | GUI Disk Management Features (Task 5) |

---

## Table of Contents

1. [Active Tasks](#active-tasks)
   - [Task 5: GUI Disk Management Features](#task-5-gui-disk-management-features-high-priority)
   - [Task 10: SectorDiskImageProvider Debugging](#task-10-sectordiskimageprovider-debugging-high-priority)
   - [Task 13: Audio Emulation Implementation](#task-13-audio-emulation-implementation-medium-priority)
2. [Backlog](#backlog)
   - [Task 1: Migrate VA2M to CpuClockingCounters.VBlankOccurred](#task-1-migrate-va2m-to-cpuclockingcountersvblankoccurred-low-priority)
   - [Task 2: Remove VA2MBus.VBlank Event](#task-2-remove-va2mbusvblank-event-low-priority)
   - [Task 3: Consider IAppleIIBus Interface Update](#task-3-consider-iappleiibus-interface-update-low-priority)
   - [Task 4: HGR Flicker Investigation](#task-4-hgr-flicker-investigation-medium-priority)
   - [Task 6: Clear Pending Keystrokes on Reset](#task-6-clear-pending-keystrokes-on-reset-low-priority)
   - [Task 7: Handle BRK Loops in Interrupt Handler](#task-7-handle-brk-loops-in-interrupt-handler-low-priority)
   - [Task 8: Check for Race Conditions at High Speeds](#task-8-check-for-race-conditions-at-high-speeds-medium-priority)
   - [Task 9: Multi-Drive Operation Deep Dive](#task-9-multi-drive-operation-deep-dive-medium-priority)
   - [Task 11: Conditional Compilation for Disk Provider Debug Output](#task-11-conditional-compilation-for-disk-provider-debug-output-medium-priority)
   - [Task 12: Flexible Window Docking System](#task-12-flexible-window-docking-system-medium-priority)
   - [Task 14: Speed-Proportional Key Feeding](#task-14-speed-proportional-key-feeding-low-priority)
   - [Task 15: Authentic Apple II Keyboard Repeat](#task-15-authentic-apple-ii-keyboard-repeat-low-priority)
   - [Task 16: Research Cross-Platform Shader Rendering for Avalonia](#task-16-research-cross-platform-shader-rendering-for-avalonia-low-priority)
   - [Task 17: Research Compute-Shader Toolkits for Bitplane Processing](#task-17-research-compute-shader-toolkits-for-bitplane-processing-medium-priority)
3. [Completed Tasks](#completed-tasks)
   - [Task 6: Clear Pending Keystrokes on Reset](#task-6-clear-pending-keystrokes-on-reset)
4. [Code Style Guidelines](#code-style-guidelines)
5. [Git Best Practices](#git-best-practices)
6. [Testing Guidelines](#testing-guidelines)

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

---

### Task 10: SectorDiskImageProvider Debugging (High Priority)

**Goal:** Thorough testing and debugging of sector-based disk image provider (DSK/DO/PO formats that require GCR synthesis).

**Status:** ⏳ NOT STARTED

**Problem:**
- Potential issues with DSK/DO/PO format support
- GCR synthesis from sector data needs validation

**Areas to Investigate:**
- GCR encoding correctness
- Track synthesis timing
- Sector interleaving
- Address field generation
- Checksum calculation

**Test Strategy:**
- Compare synthesized NIB output with known-good NIB files
- Test with DOS 3.3 and ProDOS system disks
- Verify sector reads return correct data

**Files to Focus On:**
- `Pandowdy.EmuCore\DiskII\Providers\SectorDiskImageProvider.cs`
- `Pandowdy.EmuCore\DiskII\GcrEncoder.cs`
- `Pandowdy.EmuCore.Tests\DiskII\Providers\SectorDiskImageProviderTests.cs` (13 tests exist)

**Priority:** High (blocks DSK/DO/PO format support)

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

---

## Backlog

### Task 1: Migrate VA2M to CpuClockingCounters.VBlankOccurred (Low Priority)

**Current State:**
- `VA2M` subscribes to `VA2MBus.VBlank` event (now marked `[Obsolete]`)
- `VA2MBus.VBlank` uses `EventHandler` signature `(object? sender, EventArgs e)`
- `CpuClockingCounters.VBlankOccurred` uses simpler `Action` signature

**Problem:**
- Two VBlank events fire on every frame (redundant)
- `VA2MBus.VBlank` is deprecated but still in use
- Inconsistent event patterns

**Proposed Solution:**

**Option A: Change OnVBlank signature** (Breaking change for tests)
```csharp
// Change from:
private void OnVBlank(object? sender, EventArgs e)

// To:
private void OnVBlank()
```
Then subscribe via: `vb.ClockCounters.VBlankOccurred += OnVBlank;`

**Option B: Use lambda adapter** (Non-breaking)
```csharp
if (Bus is VA2MBus vb)
{
    vb.ClockCounters.VBlankOccurred += () => OnVBlank(null, EventArgs.Empty);
}
```

**Option C: Inject CpuClockingCounters directly into VA2M** (Cleanest long-term)
- Add `CpuClockingCounters` as a constructor parameter
- Subscribe directly: `clockCounters.VBlankOccurred += OnVBlank;`
- Remove `VA2MBus.VBlank` entirely

**Recommended:** Option C for cleanest architecture, but requires updating VA2M's 11 constructor parameters to 12.

**Files to Modify:**
- `Pandowdy.EmuCore\VA2M.cs` - Update constructor and subscription
- `Pandowdy.EmuCore\VA2MBus.cs` - Remove `VBlank` event entirely (after Task 2)
- `Pandowdy.EmuCore.Tests\Helpers\VA2MTestHelpers.cs` - Update test factory
- Any tests that mock/verify VBlank behavior

**Priority:** Low - Current pragma suppressions work fine. Address after Disk II integration is stable.

---

### Task 2: Remove VA2MBus.VBlank Event (Low Priority)

**Prerequisite:** Task 1 must be completed first.

**Goal:** Clean up deprecated VBlank event from `VA2MBus`.

**Steps:**
1. Remove `[Obsolete]` attribute and event declaration from `VA2MBus.cs`
2. Remove event invocation in `Clock()` method
3. Remove null assignment in `Dispose()` method
4. Update documentation to remove VBlank references
5. Run full test suite to verify no regressions

**Files to Modify:**
- `Pandowdy.EmuCore\VA2MBus.cs`

**Priority:** Low

---

### Task 3: Consider IAppleIIBus Interface Update (Low Priority)

**Current State:**
- `IAppleIIBus` doesn't expose `ClockCounters`
- Components that need timing must cast to `VA2MBus`

**Proposed:**
- Add `CpuClockingCounters ClockCounters { get; }` to `IAppleIIBus` interface
- Or create a new interface `ITimingProvider` that components can request

**Impact:** Would allow Disk II controller to receive `IAppleIIBus` instead of needing direct `CpuClockingCounters` injection.

**Files to Modify:**
- `Pandowdy.EmuCore\Interfaces\IAppleIIBus.cs`
- `Pandowdy.EmuCore\VA2MBus.cs`
- `Pandowdy.EmuCore\Cards\DiskIIControllerCard.cs` - Update constructor

**Priority:** Low

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

**Related:** See Task 8 (Race Conditions at High Speeds)

---

### Task 6: Clear Pending Keystrokes on Reset (Low Priority)

**Status:** ✅ COMPLETED (2025-01-25)

**Problem:** When `Reset()` is called, any pending keystrokes in the `QueuedKeyHandler` buffer should be cleared.

**Solution Implemented:**
- Added `Reset()` method to `IKeyboardSetter` interface
- Implemented in `SingularKeyHandler`: Clears strobe bit while preserving key value
- Implemented in `QueuedKeyHandler`: Cancels timer, clears queue, clears strobe bit
- Called in `VA2M.Reset()` before `Bus.Reset()`
- Added 11 unit tests (5 for SingularKeyHandler, 6 for QueuedKeyHandler)

**Files Modified:**
- `Pandowdy.EmuCore\Interfaces\IKeyboardSetter.cs` - Added Reset() method
- `Pandowdy.EmuCore\Services\SingularKeyHandler.cs` - Implemented Reset()
- `Pandowdy.EmuCore\Services\QueuedKeyHandler.cs` - Implemented Reset() with queue clearing
- `Pandowdy.EmuCore\VA2M.cs` - Calls keyboard Reset() on system reset
- `Pandowdy.EmuCore.Tests\SingularKeyHandlerTests.cs` - Added 5 Reset() tests
- `Pandowdy.EmuCore.Tests\QueuedKeyHandlerTests.cs` - Added 6 Reset() tests

**Test Results:** All 1235 tests passing ✅

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

### Task 8: Check for Race Conditions at High Speeds (Medium Priority)

**Problem:** Potential race conditions when running at unthrottled speeds.

**Areas to Investigate:**
- Video memory access during rendering
- Disk II bit timing at high cycle rates
- VBlank event handling
- DiskStatusProvider message publishing

**Files to Focus On:**
- `Pandowdy.EmuCore\Services\LegacyBitmapRenderer.cs`
- `Pandowdy.EmuCore\Services\FrameProvider.cs`
- `Pandowdy.EmuCore\Cards\DiskIIControllerCard.cs`
- `Pandowdy.EmuCore\DiskII\DiskIIDrive.cs`

**Priority:** Medium

**Related:** See Task 4 (HGR flicker issue)

---

### Task 9: Multi-Drive Operation Deep Dive (Medium Priority)

**Goal:** Thorough testing and debugging of multi-drive operation in the controller card.

**Test Scenarios:**
- Switching between drives during active read
- Both motors running simultaneously
- Motor-off timing with drive switching
- Phase state when switching drives
- Programs that use both drives (e.g., copy utilities)

**Files to Focus On:**
- `Pandowdy.EmuCore\Cards\DiskIIControllerCard.cs`
- `Pandowdy.EmuCore.Tests\DiskII\DiskIIIntegrationTests.cs` (28 tests exist)

**Priority:** Medium

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

## Completed Tasks

### ✅ Task 6: Clear Pending Keystrokes on Reset

**Completed:** 2025-01-25 - All 1235 tests passing

**Summary:**
- Added Reset() method to IKeyboardSetter interface
- Implemented in SingularKeyHandler (clears strobe, preserves key)
- Implemented in QueuedKeyHandler (cancels timer, clears queue, clears strobe)
- Integrated into VA2M.Reset() for system-wide keyboard reset
- Added 11 unit tests verifying Reset() behavior

**Key Behavior:**
- On reset, strobe bit is cleared (bit 7 → 0)
- Key value's low 7 bits are preserved (matches Apple IIe hardware)
- QueuedKeyHandler clears all pending keys from buffer
- QueuedKeyHandler cancels any active timer callbacks
- Prevents stale keystrokes from appearing after system reset

**Files Modified:**
- `IKeyboardSetter.cs` - Interface
- `SingularKeyHandler.cs` - Implementation
- `QueuedKeyHandler.cs` - Implementation with queue clearing
- `VA2M.cs` - Integration
- `SingularKeyHandlerTests.cs` - 5 tests
- `QueuedKeyHandlerTests.cs` - 6 tests



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
