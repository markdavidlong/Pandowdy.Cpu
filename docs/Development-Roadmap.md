# Pandowdy Development Roadmap

---

## 📊 Current State Summary

| Status | Details |
|--------|---------|
| **Branch** | `develop` |
| **Tests** | 1224 tests passing ✅ |
| **Last Milestone** | Disk II Integration Complete (Phase 8E) |
| **Next Focus** | GUI Disk Management Features (Task 5) |

---

## Table of Contents

1. [Active Tasks](#active-tasks)
2. [Backlog](#backlog)
3. [Completed Tasks](#completed-tasks)
4. [Code Style Guidelines](#code-style-guidelines)
5. [Git Best Practices](#git-best-practices)
6. [Testing Guidelines](#testing-guidelines)

---

## Active Tasks

### Task 5: GUI Disk Management Features (High Priority)

**Goal:** Add user-facing disk management capabilities to the GUI.

**Status:** ⏳ NOT STARTED

**Prerequisites:** Phase 8E Complete ✅

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

### ✅ Disk II Integration (Phases 1-8E)

**Completed:** Phase 8E Complete - 1224 tests passing

**Summary:**
- VBlankOccurred event infrastructure
- DiskII interfaces and constants
- Disk image providers (NIB, WOZ, DSK/DO/PO)
- Drive implementation with DiskIIStatusDecorator
- Controller card (16-sector and 13-sector variants)
- DI registration and initialization
- Integration tests (259 Disk II tests)
- DiskStatusServices pattern (replaced telemetry approach)
- GUI status display panel

**Documentation:** See `docs/DiskII-Integration-Plan.md` (archived)

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

**This document is a living reference.** Update it as:
- Tasks are completed
- New tasks are identified
- Priorities change
- Architectural decisions are made

**Status Indicators:**
- ✅ COMPLETED
- ⏳ IN PROGRESS
- ⏳ NOT STARTED
- ⚠️ BLOCKED
- 🔍 NEEDS INVESTIGATION

---

*Document Created: 2025-01-XX*  
*Last Updated: Initial creation - migrated from DiskII-Integration-Plan.md*
