# Disk II Integration Plan

---

## 📊 Current State Summary

| Status | Details |
|--------|---------|
| **Progress** | All Phases Complete ✅ |
| **Tests** | 1224 total (259 Disk II tests) |
| **Branch** | `re_disking` (was `piecemealing`) |
| **Next Step** | Integration complete! See Next Steps section. |

**What's Done:**
- ✅ Phase 1: Foundation (VBlankOccurred event, DiskIIConstants, telemetry payloads)
- ✅ Phase 2: Interfaces (IDiskImageProvider, IDiskIIDrive, IDiskImageFactory, IDiskIIFactory)
- ✅ Phase 3: Disk Image Providers (GcrEncoder, NibDiskImageProvider, SectorDiskImageProvider, InternalWozDiskImageProvider, WozDiskImageProvider, DiskImageFactory)
- ✅ Phase 4: Drive Implementation (NullDiskIIDrive, DiskIIDrive, DiskIIDebugDecorator, DiskIIFactory, MockTelemetryAggregator)
- ✅ Phase 5: Controller Card (DiskIIControllerCard base, 16-sector, 13-sector variants + 53 tests)
- ✅ Phase 6: Factory Registration (DI registrations in Program.cs, Slot 6 initialization)
- ✅ Phase 7: Integration Tests (28 tests covering telemetry flow, motor timeout, multi-drive)

**What's Next:**
- See "Next Steps (Post-Integration)" section for future improvements

**Key Decision Made:**
- `DiskIIStatusDecorator` was **NOT imported** - its functionality was replaced by direct telemetry publishing in `DiskIIDrive`. See Issue #2 in Integration Issues section.

---

## Overview

This document provides a comprehensive plan for integrating the Disk II emulation code from `Pandowdy.DiskImportCode` into `Pandowdy.EmuCore`. The code implements full Disk II controller card and drive emulation, supporting multiple disk image formats (NIB, WOZ, DSK/DO/PO).

**Source Project:** `Pandowdy.DiskImportCode` (temporary staging project, **READ-ONLY**)  
**Target Project:** `Pandowdy.EmuCore`  
**Branch:** `re_disking`

### Import Process

> **IMPORTANT:** The `Pandowdy.DiskImportCode` project is a **read-only staging area** containing
> reference code. Files are **NOT modified in place**. Instead:
>
> 1. **Read** source file from `Pandowdy.DiskImportCode\{file}.cs` as reference
> 2. **Create new file** in `Pandowdy.EmuCore\{target-folder}\{file}.cs`
> 3. **Apply transformations** during creation:
>    - Update namespace to match target location
>    - Replace `IDiskStatusMutator` with `ITelemetryAggregator`
>    - Use `DiskIIConstants` instead of magic numbers
>    - Fix code style per project standards
> 4. **Verify build** after each file import
>
> Some files (like `DiskIIStatusDecorator.cs`) are **not imported at all** because their
> functionality is replaced by the telemetry pattern.

---

## Table of Contents

1. [Source Code Inventory](#source-code-inventory)
2. [Architecture Overview](#architecture-overview)
3. [Integration Issues & Resolutions](#integration-issues--resolutions)
4. [Target File Structure](#target-file-structure)
5. [Phase 1: Foundation](#phase-1-foundation) ✅ COMPLETED
6. [Phase 2: Interfaces](#phase-2-interfaces) ✅ COMPLETED
7. [Phase 3: Disk Image Providers](#phase-3-disk-image-providers) ✅ COMPLETED
8. [Phase 4: Drive Implementation](#phase-4-drive-implementation) ✅ COMPLETED
9. [Phase 5: Controller Card](#phase-5-controller-card) ✅ COMPLETED
10. [Phase 6: Factory Registration](#phase-6-factory-registration) ✅ COMPLETED
11. [Phase 7: Tests](#phase-7-tests) ✅ COMPLETED
12. [Telemetry Integration](#telemetry-integration)
13. [Code Style Corrections](#code-style-corrections)
14. [Verification Checklist](#verification-checklist)
15. [Next Steps (Post-Integration)](#next-steps-post-integration)

---

### Quick Reference: Key Files Created

**For Phase 5, you may want to examine these existing implementations for patterns:**

| File | Purpose |
|------|---------|
| `Pandowdy.EmuCore\DiskII\DiskIIDrive.cs` | Telemetry integration pattern (constructor, publishing) |
| `Pandowdy.EmuCore\DiskII\DiskIIFactory.cs` | How drives are created with telemetry |
| `Pandowdy.EmuCore\Interfaces\ICard.cs` | ICard interface the controller must implement |
| `Pandowdy.EmuCore\DataTypes\CpuClockingCounters.cs` | VBlankOccurred event to subscribe to |
| `Pandowdy.EmuCore.Tests\Helpers\MockTelemetryAggregator.cs` | Mock for testing telemetry |

**Source file for Phase 5:**
- `Pandowdy.DiskImportCode\DiskIIControllerCards.cs` (~1150 lines, split into 3 target files)

---

## Source Code Inventory

### Files in `Pandowdy.DiskImportCode`

| File | Lines | Purpose | Integration Notes |
|------|-------|---------|-------------------|
| `DiskIIControllerCards.cs` | ~1150 | Controller card base + 13/16-sector variants | Split into 3 files |
| `DiskIIDrive.cs` | ~310 | Physical drive mechanics | Minor refactoring |
| `DiskIIDebugDecorator.cs` | ~125 | Debug logging decorator | Refactor for telemetry |
| `DiskIIStatusDecorator.cs` | ~200 | Status provider integration | **Replace with telemetry** |
| `DiskIIFactory.cs` | ~125 | Drive factory with decorator chain | Update for telemetry |
| `DiskImageFactory.cs` | ~72 | Disk format detection & provider creation | Minor cleanup |
| `GcrEncoder.cs` | ~205 | GCR track synthesis from sectors | Clean import |
| `NibDiskImageProvider.cs` | ~320 | .nib format provider | Clean import |
| `SectorDiskImageProvider.cs` | ~294 | .dsk/.do/.po format provider | Clean import |
| `InternalWozDiskImageProvider.cs` | ~610 | Native .woz parser | Clean import |
| `WozDiskImageProvider.cs` | ~200 | CiderPress2-based .woz provider | Clean import |
| `NullDiskIIDrive.cs` | ~90 | Null object pattern for testing | Clean import |
| `IDiskIIDrive.cs` | ~32 | Drive interface | Clean import |
| `IDiskImageProvider.cs` | ~60 | Disk image abstraction | Clean import |
| `IDiskImageFactory.cs` | ~26 | Factory interface | Clean import |

### Interfaces Defined Inline (to extract)

| Interface | Current Location | Notes |
|-----------|-----------------|-------|
| `IDiskIIFactory` | `DiskIIControllerCards.cs:7-10` | Move to `Interfaces/` |

---

## Architecture Overview

### Component Relationships

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                          DiskIIControllerCard                                    │
│  - Manages I/O addresses ($C0x0-$C0xF)                                          │
│  - Stepper motor phase control (quarter-track positioning)                       │
│  - Q6/Q7 mode switching (read/write/protect-sense)                              │
│  - Shift register for bit accumulation                                          │
│  - VBlank subscription for motor-off timeout                                     │
└────────────────────────────────────────┬────────────────────────────────────────┘
                                         │ contains
                                         ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│                    IDiskIIDrive (Drive 1, Drive 2)                               │
│  - Physical mechanics: motor on/off, quarter-track stepping                      │
│  - Bit read/write operations delegated to IDiskImageProvider                     │
│  - Disk insert/eject operations                                                  │
└────────────────────────────────────────┬────────────────────────────────────────┘
                                         │ uses
                                         ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│                          IDiskImageProvider                                      │
│  - Format-specific disk data access                                             │
│  - Track/sector encoding/decoding                                               │
│  - Cycle-based bit timing for accurate rotation                                  │
│                                                                                  │
│  Implementations:                                                                │
│  - NibDiskImageProvider (.nib - raw GCR nibbles)                                │
│  - InternalWozDiskImageProvider (.woz - flux timing)                            │
│  - WozDiskImageProvider (.woz via CiderPress2)                                  │
│  - SectorDiskImageProvider (.dsk/.do/.po - synthesized GCR)                     │
└─────────────────────────────────────────────────────────────────────────────────┘
```

### Decorator Chain (Current - TO BE CHANGED)

```
Current:    DiskIIDebugDecorator → DiskIIStatusDecorator → DiskIIDrive
                   │                        │
                   ▼                        ▼
            Debug.WriteLine()      IDiskStatusMutator (doesn't exist!)
```

### Decorator Chain (New - Using Telemetry)

```
New:        DiskIIDebugDecorator → DiskIIDrive
                                       │
                                       ▼
                              ITelemetryAggregator
                                       │
                              Publishes: TelemetryMessage
                              Category: "DiskII"
```

---

## Integration Issues & Resolutions

### Issue 1: Missing VBlankOccurred Event

**Problem:** `DiskIIControllerCard` subscribes to `_clocking.VBlankOccurred` (line 141) but this event doesn't exist in `CpuClockingCounters`.

**Resolution:** ✅ **COMPLETED** - Added `VBlankOccurred` event to `CpuClockingCounters` that fires when `CheckAndAdvanceVBlank()` returns true.

**Changes Made:**
1. Added `VBlankOccurred` event declaration to `CpuClockingCounters`
2. Invoke event in `CheckAndAdvanceVBlank()` before returning true
3. Added `ClockCounters` property to `VA2MBus` to expose the counters
4. Marked `VA2MBus.VBlank` as `[Obsolete]` - use `ClockCounters.VBlankOccurred` instead
5. Added pragma suppressions in VA2M.cs and VA2MBus.cs for backward compatibility

**File:** `Pandowdy.EmuCore\DataTypes\CpuClockingCounters.cs`

```csharp
// Event declaration (added)
public event Action? VBlankOccurred;

// In CheckAndAdvanceVBlank() - fires before returning true
VBlankOccurred?.Invoke();
```

**File:** `Pandowdy.EmuCore\VA2MBus.cs`

```csharp
// New property to expose ClockCounters
public CpuClockingCounters ClockCounters => _clockCounters;

// VBlank event marked obsolete
[Obsolete("Use ClockCounters.VBlankOccurred instead.")]
public event EventHandler? VBlank;
```

**Architecture:** `CpuClockingCounters` is now the single source of truth for VBlank timing.
New components should subscribe to `ClockCounters.VBlankOccurred`.

---

### Issue 2: IDiskStatusMutator Doesn't Exist

**Problem:** The original code in `Pandowdy.DiskImportCode` uses `IDiskStatusMutator` for status updates, but this interface doesn't exist in `Pandowdy.EmuCore` and we don't want to create it.

**Resolution:** Replace with `ITelemetryAggregator` pattern during import. Files are copied from the read-only `Pandowdy.DiskImportCode` staging area and modified during import into `Pandowdy.EmuCore`.

**Files Affected During Import:**

| Source File | Action | Changes During Import |
|-------------|--------|----------------------|
| `DiskIIStatusDecorator.cs` | **DO NOT IMPORT** | Entire file skipped - pattern replaced by telemetry |
| `DiskIIDrive.cs` | Import + Modify | Add `ITelemetryAggregator` dependency, publish telemetry |
| `DiskIIFactory.cs` | Import + Modify | Remove `IDiskStatusMutator`, remove decorator from chain |
| `DiskIIControllerCards.cs` | Import + Modify | Replace `_diskStatusMutator` with `_telemetry` |

**Old Pattern (in DiskIIStatusDecorator - will not be imported):**
```csharp
_statusMutator.MutateDrive(_slotNumber, _driveNumber, builder =>
{
    builder.MotorOn = value;
});
```

**New Pattern (in DiskIIDrive after import to Pandowdy.EmuCore):**
```csharp
// Simple debug messages during development
_telemetry.Publish(new TelemetryMessage(_telemetryId, "debug", 
    new DiskIIMessage("Disk Motor is now ON")));
_telemetry.Publish(new TelemetryMessage(_telemetryId, "debug", 
    new DiskIIMessage($"Seeking to Track {Track:F2}")));
```

**Import Workflow:**
1. Read source file from `Pandowdy.DiskImportCode\{filename}.cs` (reference only)
2. Create new file in `Pandowdy.EmuCore\DiskII\{filename}.cs`
3. Apply namespace changes, telemetry integration, style fixes
4. Verify build after each file

**Telemetry Payload Type for DiskII (Simplified):**

During development, use `DiskIIMessage` with descriptive text:
```csharp
new DiskIIMessage("Disk Motor is now ON")
new DiskIIMessage($"Seeking to Track {Track:F2}, Sector {Sector}")
new DiskIIMessage($"Disk inserted: {filename}")
```

See Telemetry Integration section for future typed payload approach.

---

### Issue 3: Duplicate IDiskIIFactory Interface

**Problem:** `IDiskIIFactory` defined twice (in `DiskIIControllerCards.cs:7-10` and as separate concept).

**Resolution:** Keep only one definition in `Interfaces/IDiskIIFactory.cs`. Remove inline definition.

---

### Issue 4: SlotNumber Enum Reference

**Problem:** Code uses `SlotNumber` enum that may not be accessible.

**Resolution:** `SlotNumber` is defined in `Pandowdy.EmuCore.Interfaces` (verified in `ICard.cs`). Ensure proper using directive.

---

### Issue 5: Shared Constants Duplication

**Problem:** `CYCLES_PER_BIT = 45.0 / 11.0` is duplicated in multiple files.

**Resolution:** Create `DiskIIConstants.cs` with shared timing constants.

```csharp
namespace Pandowdy.EmuCore.DiskII;

/// <summary>
/// Shared constants for Disk II emulation timing.
/// </summary>
public static class DiskIIConstants
{
    /// <summary>
    /// Cycles per bit for accurate Apple II Disk II timing.
    /// The disk reads at 250 kHz (4μs per bit) while the CPU runs at 1.023 MHz.
    /// This gives exactly 45/11 cycles per bit ≈ 4.090909 cycles/bit.
    /// </summary>
    public const double CyclesPerBit = 45.0 / 11.0; // 4.090909...
    
    /// <summary>
    /// Number of tracks on a standard 5.25" disk.
    /// </summary>
    public const int TrackCount = 35;
    
    /// <summary>
    /// Bytes per track in NIB format.
    /// </summary>
    public const int BytesPerNibTrack = 6656;
    
    /// <summary>
    /// Bits per track (6656 * 8 = 53,248).
    /// </summary>
    public const int BitsPerTrack = BytesPerNibTrack * 8;
    
    /// <summary>
    /// Maximum quarter-track position (35 tracks * 4 quarters + 1).
    /// </summary>
    public const int MaxQuarterTracks = 35 * 4 + 1; // 141
    
    /// <summary>
    /// Sectors per track for 16-sector format.
    /// </summary>
    public const int SectorsPerTrack = 16;
    
    /// <summary>
    /// Bytes per sector.
    /// </summary>
    public const int BytesPerSector = 256;
    
    /// <summary>
    /// Motor-off delay in CPU cycles (~1 second at 1.023 MHz).
    /// </summary>
    public const ulong MotorOffDelayCycles = 1_000_000;
    
    /// <summary>
    /// Telemetry category identifier for Disk II devices.
    /// </summary>
    public const string TelemetryCategory = "DiskII";
}
```

---

## Target File Structure

```
Pandowdy.EmuCore/
├── Interfaces/
│   ├── IDiskIIDrive.cs               (NEW - from DiskImportCode)
│   ├── IDiskIIFactory.cs             (NEW - extracted from DiskIIControllerCards)
│   ├── IDiskImageFactory.cs          (NEW - from DiskImportCode)
│   └── IDiskImageProvider.cs         (NEW - from DiskImportCode)
│
├── DataTypes/
│   ├── CpuClockingCounters.cs        (MODIFY - add VBlankOccurred event)
│   └── DiskIITelemetryPayloads.cs    (NEW - telemetry payload records)
│
├── Cards/
│   ├── DiskIIControllerCard.cs       (NEW - base class extracted)
│   ├── DiskIIControllerCard16Sector.cs (NEW - 16-sector ROM variant)
│   └── DiskIIControllerCard13Sector.cs (NEW - 13-sector ROM variant)
│
├── DiskII/
│   ├── DiskIIConstants.cs            (NEW - shared constants)
│   ├── DiskIIDrive.cs                (NEW - modified for telemetry)
│   ├── DiskIIFactory.cs              (NEW - simplified, no status decorator)
│   ├── DiskIIDebugDecorator.cs       (NEW - from DiskImportCode)
│   ├── NullDiskIIDrive.cs            (NEW - from DiskImportCode)
│   ├── GcrEncoder.cs                 (NEW - from DiskImportCode)
│   └── Providers/
│       ├── DiskImageFactory.cs           (NEW - from DiskImportCode)
│       ├── NibDiskImageProvider.cs       (NEW - from DiskImportCode)
│       ├── SectorDiskImageProvider.cs    (NEW - from DiskImportCode)
│       ├── InternalWozDiskImageProvider.cs (NEW - from DiskImportCode)
│       └── WozDiskImageProvider.cs       (NEW - optional CiderPress2 version)
│
├── Services/
│   └── CardFactory.cs                (MODIFY - register Disk II cards)
│
└── ... (existing files unchanged)

Pandowdy.EmuCore.Tests/
├── DiskII/
│   ├── DiskIIDriveTests.cs           (NEW)
│   ├── DiskIIControllerCardTests.cs  (NEW)
│   ├── GcrEncoderTests.cs            (NEW)
│   └── Providers/
│       ├── NibDiskImageProviderTests.cs   (NEW)
│       ├── SectorDiskImageProviderTests.cs (NEW)
│       └── InternalWozDiskImageProviderTests.cs (NEW)
└── ...
```

---

## Phase 1: Foundation ✅ COMPLETED

**Goal:** Add prerequisite infrastructure to EmuCore.

### Step 1.1: Add VBlankOccurred Event ✅ COMPLETED

**File:** `Pandowdy.EmuCore\DataTypes\CpuClockingCounters.cs`

**Changes:**
1. Add event declaration
2. Invoke event in `CheckAndAdvanceVBlank()`

```csharp
// Add after line 11 (_nextVblankCycle field)
/// <summary>
/// Event fired when a VBlank transition occurs.
/// Subscribers can use this for timing-dependent operations (e.g., motor-off delays).
/// </summary>
public event Action? VBlankOccurred;

// Modify CheckAndAdvanceVBlank() - add after line 159
VBlankOccurred?.Invoke();
```

### Step 1.2: Create DiskIIConstants ✅ COMPLETED

**File:** `Pandowdy.EmuCore\DiskII\DiskIIConstants.cs`

**Constants Created:**

| Constant | Value | Description |
|----------|-------|-------------|
| `CyclesPerBit` | 45.0/11.0 | CPU cycles per disk bit (~4.09) |
| `TrackCount` | 35 | Tracks on 5.25" disk |
| `BytesPerNibTrack` | 6656 | Bytes per NIB track |
| `BitsPerTrack` | 53,248 | Bits per track |
| `MaxQuarterTracks` | 140 | Max quarter-track position |
| `SectorsPerTrack16` | 16 | 16-sector format |
| `SectorsPerTrack13` | 13 | 13-sector format |
| `BytesPerSector` | 256 | Bytes per sector |
| `TotalBytes16Sector` | 143,360 | Total disk bytes (16-sector) |
| `TotalBytes13Sector` | 116,480 | Total disk bytes (13-sector) |
| `MotorOffDelayFrames` | 60 | ~1 second at 60 Hz |
| `TelemetryCategory` | "DiskII" | Telemetry device type |

### Step 1.3: Create Telemetry Payload Types ✅ COMPLETED (Simplified)

**File:** `Pandowdy.EmuCore\DataTypes\DiskIITelemetryPayloads.cs`

**Simplified for Development:**
During initial development, we use a single simple message type for debugging:

```csharp
public readonly record struct DiskIIMessage(string Message)
{
    public override string ToString() => Message;
}
```

**Usage Example:**
```csharp
// In DiskIIDrive:
_telemetry.Publish(new TelemetryMessage(_telemetryId, "debug", 
    new DiskIIMessage("Disk Motor is now ON")));
_telemetry.Publish(new TelemetryMessage(_telemetryId, "debug", 
    new DiskIIMessage($"Seeking to Track {Track:F2}")));
```

**Future Enhancement:**
When the GUI is ready to consume telemetry, this will be replaced with typed message records
(DiskIIMotorMessage, DiskIITrackMessage, etc.) that allow pattern matching and structured data access.
The typed approach is documented in the Telemetry Integration section below for reference.

### Step 1.4: Verify Build ✅ COMPLETED

After Phase 1 changes, run build to ensure no regressions.

### Step 1.5: Add Unit Tests for VBlankOccurred ✅ COMPLETED

**File:** `Pandowdy.EmuCore.Tests\CpuClockingCountersTests.cs`

**Tests Created (10 total):**

| Test | Description |
|------|-------------|
| `VBlankOccurred_DoesNotFire_BeforeVBlankCycle` | Event doesn't fire before threshold |
| `VBlankOccurred_Fires_WhenVBlankCycleReached` | Event fires at correct cycle |
| `VBlankOccurred_FiresOncePerFrame` | Event fires exactly once per 17,030 cycles |
| `VBlankOccurred_FiresOnlyOnce_WhenMultipleFramesSkipped` | Catch-up logic works (no spam) |
| `VBlankOccurred_NoSubscribers_DoesNotThrow` | Safe when no handlers attached |
| `CheckAndAdvanceVBlank_ReturnsFalse_BeforeVBlankCycle` | Returns false before threshold |
| `CheckAndAdvanceVBlank_ReturnsTrue_WhenVBlankCycleReached` | Returns true at threshold |
| `CheckAndAdvanceVBlank_AdvancesNextVBlankCycle` | NextVBlankCycle advances correctly |
| `Reset_ClearsEventSubscription_DoesNotAffectSubscribers` | Reset preserves event handlers |
| `Reset_ResetsNextVBlankCycle_ToVBlankStartCycle` | Reset restores initial state |

---

## Phase 2: Interfaces ✅ COMPLETED

**Goal:** Add interface files to `Interfaces/` folder.

### Step 2.1: Copy IDiskImageProvider.cs ✅ COMPLETED

**Source:** `Pandowdy.DiskImportCode\IDiskImageProvider.cs`  
**Target:** `Pandowdy.EmuCore\Interfaces\IDiskImageProvider.cs`

**Changes:** Enhanced XML documentation with implementation references.

### Step 2.2: Copy IDiskIIDrive.cs ✅ COMPLETED

**Source:** `Pandowdy.DiskImportCode\IDiskIIDrive.cs`  
**Target:** `Pandowdy.EmuCore\Interfaces\IDiskIIDrive.cs`

**Changes:** Added comprehensive XML documentation for all members.

### Step 2.3: Copy IDiskImageFactory.cs ✅ COMPLETED

**Source:** `Pandowdy.DiskImportCode\IDiskImageFactory.cs`  
**Target:** `Pandowdy.EmuCore\Interfaces\IDiskImageFactory.cs`

**Changes:** Minor documentation cleanup.

### Step 2.4: Create IDiskIIFactory.cs ✅ COMPLETED

**Target:** `Pandowdy.EmuCore\Interfaces\IDiskIIFactory.cs`

**Created with:**
- Drive naming convention documentation
- Telemetry integration remarks
- Full XML documentation

### Step 2.5: Verify Build ✅ COMPLETED

All interfaces compile correctly.

---

## Phase 3: Disk Image Providers ✅ COMPLETED

**Goal:** Add disk format providers and supporting classes with tests.

> **Testing Strategy:** Tests are created alongside each implementation step (not deferred to Phase 7).
> This enables regression detection during iterative development.

### Step 3.1: Create GcrEncoder ✅ COMPLETED

**Target:** `Pandowdy.EmuCore\DiskII\GcrEncoder.cs`

**Source:** `Pandowdy.DiskImportCode\GcrEncoder.cs`

**Changes:**
- Update namespace to `Pandowdy.EmuCore.DiskII`
- Fix code style (braces, indentation)
- Use collection expression syntax for static array

**Tests:** `Pandowdy.EmuCore.Tests\DiskII\GcrEncoderTests.cs` (15 tests)
- Address field encoding produces valid prologue/epilogue
- Data field 6&2 encoding produces 342 bytes from 256
- Sync gaps write correct byte values
- Checksum calculation is correct
- Offset handling works correctly

### Step 3.2: Create Providers subfolder structure ✅ COMPLETED

Directories created:
- `Pandowdy.EmuCore\DiskII\Providers\`
- `Pandowdy.EmuCore.Tests\DiskII\`
- `Pandowdy.EmuCore.Tests\DiskII\Providers\`

### Step 3.3: Copy DiskImageFactory ✅ COMPLETED (implementation)

**Source:** `Pandowdy.DiskImportCode\DiskImageFactory.cs`  
**Target:** `Pandowdy.EmuCore\DiskII\Providers\DiskImageFactory.cs`

**Changes:**
- Update namespace to `Pandowdy.EmuCore.DiskII.Providers`
- Fix indentation on `IsFormatSupported` method

**Tests:** `Pandowdy.EmuCore.Tests\DiskII\Providers\DiskImageFactoryTests.cs` (25 tests) ✅
- Returns correct provider type for each extension (.nib, .woz, .dsk, .do, .po, .2mg)
- Throws FileNotFoundException for missing files
- Throws NotSupportedException for unsupported extensions
- IsFormatSupported returns correct values

### Step 3.4: Copy NibDiskImageProvider ✅ COMPLETED (implementation)

**Source:** `Pandowdy.DiskImportCode\NibDiskImageProvider.cs`  
**Target:** `Pandowdy.EmuCore\DiskII\Providers\NibDiskImageProvider.cs`

**Changes:**
- Update namespace to `Pandowdy.EmuCore.DiskII.Providers`
- Replace `CYCLES_PER_BIT` constant with `DiskIIConstants.CyclesPerBit`
- Replace `TRACK_COUNT` with `DiskIIConstants.TrackCount`
- Replace `BYTES_PER_TRACK` with `DiskIIConstants.BytesPerNibTrack`
- Replace `BITS_PER_TRACK` with `DiskIIConstants.BitsPerTrack`
- Fix indentation inconsistencies (lines 153-166, 298-329 in source)

**Tests:** `Pandowdy.EmuCore.Tests\DiskII\Providers\NibDiskImageProviderTests.cs` (15 tests) ✅
- Loads valid .nib file (232,960 bytes)
- Throws InvalidDataException for wrong file size
- SetQuarterTrack updates CurrentQuarterTrack
- GetBit returns bits from correct track
- Out-of-bounds tracks return random bits (MC3470 simulation)
- WriteBit writes to correct position
- Dispose flushes changes

### Step 3.5: Copy SectorDiskImageProvider ✅ COMPLETED (implementation)

**Source:** `Pandowdy.DiskImportCode\SectorDiskImageProvider.cs`  
**Target:** `Pandowdy.EmuCore\DiskII\Providers\SectorDiskImageProvider.cs`

**Changes:**
- Update namespace to `Pandowdy.EmuCore.DiskII.Providers`
- Replace constants with `DiskIIConstants.*`
- Fix code style

**Tests:** `Pandowdy.EmuCore.Tests\DiskII\Providers\SectorDiskImageProviderTests.cs` (13 tests) ✅
- Loads valid sector-based disk image
- Track synthesis produces valid GCR data
- Caches synthesized tracks
- SetQuarterTrack updates position
- GetBit returns synthesized bits

### Step 3.6: Copy InternalWozDiskImageProvider ✅ COMPLETED (implementation)

**Source:** `Pandowdy.DiskImportCode\InternalWozDiskImageProvider.cs`  
**Target:** `Pandowdy.EmuCore\DiskII\Providers\InternalWozDiskImageProvider.cs`

**Changes:**
- Update namespace to `Pandowdy.EmuCore.DiskII.Providers`
- Replace `CYCLES_PER_BIT` with `DiskIIConstants.CyclesPerBit`

**Tests:** `Pandowdy.EmuCore.Tests\DiskII\Providers\InternalWozDiskImageProviderTests.cs` (14 tests) ✅
- Loads WOZ 1.0 files correctly
- Loads WOZ 2.0 files correctly
- Validates CRC32 checksum
- Parses INFO chunk metadata
- TMAP quarter-track mapping works
- GetBit returns bits from correct track
- Unmapped tracks return random bits

### Step 3.7: Copy WozDiskImageProvider (CiderPress2) ✅ COMPLETED (implementation)

**Source:** `Pandowdy.DiskImportCode\WozDiskImageProvider.cs`  
**Target:** `Pandowdy.EmuCore\DiskII\Providers\WozDiskImageProvider.cs`

> **Note:** Both WOZ providers are imported. `InternalWozDiskImageProvider` is the default
> (no CiderPress2 dependency). `WozDiskImageProvider` uses CiderPress2's `Woz` class.
> `DiskImageFactory` uses `InternalWozDiskImageProvider` by default.

**Changes:**
- Update namespace to `Pandowdy.EmuCore.DiskII.Providers`
- Replace `CYCLES_PER_BIT` with `DiskIIConstants.CyclesPerBit`

**Tests:** `Pandowdy.EmuCore.Tests\DiskII\Providers\WozDiskImageProviderTests.cs` *(not created - optional CiderPress2 provider)*
- Loads valid WOZ file via CiderPress2
- Quarter-track access works
- Track caching works
- GetBit returns correct values

### Step 3.8: Verify Build and Run Tests ✅ COMPLETED

Build successful. CiderPress2 project references added to Pandowdy.EmuCore.csproj:
- `CommonUtil.csproj`
- `DiskArc.csproj`

---

## Phase 4: Drive Implementation

**Goal:** Add drive implementation with telemetry integration.

### Step 4.1: Copy NullDiskIIDrive ✅ COMPLETED

**Source:** `Pandowdy.DiskImportCode\NullDiskIIDrive.cs`  
**Target:** `Pandowdy.EmuCore\DiskII\NullDiskIIDrive.cs`

**Changes:**
- Update namespace to `Pandowdy.EmuCore.DiskII`
- Fix brace style and indentation
- Use `DiskIIConstants.MaxQuarterTracks` instead of magic number
- Enhanced XML documentation

**Tests:** `Pandowdy.EmuCore.Tests\DiskII\NullDiskIIDriveTests.cs` (21 tests) ✅
- Constructor initializes name and state correctly
- Reset restores track 17 and motor off
- Motor state can be toggled
- StepToHigherTrack/StepToLowerTrack work with clamping
- Disk operations are no-ops (InsertDisk, EjectDisk)
- GetBit always returns null, SetBit always returns false
- HasDisk always returns false

### Step 4.2: Create DiskIIDrive with Telemetry ✅ COMPLETED

**Source:** `Pandowdy.DiskImportCode\DiskIIDrive.cs`  
**Target:** `Pandowdy.EmuCore\DiskII\DiskIIDrive.cs`

**Major Changes:**
1. Update namespace to `Pandowdy.EmuCore.DiskII`
2. Add `ITelemetryAggregator` dependency (required parameter)
3. Add `slotNumber` and `driveNumber` parameters for telemetry identification
4. Create telemetry ID in constructor
5. Publish telemetry on motor, track, disk insert/eject state changes
6. Subscribe to resend requests for full state publishing
7. Use `DiskIIConstants.MaxQuarterTracks` instead of local constant
8. Call `IDiskImageProvider.Dispose()` directly (interface now extends IDisposable)

**Tests:** `Pandowdy.EmuCore.Tests\DiskII\DiskIIDriveTests.cs` (33 tests) ✅
- Constructor tests: name, null handling, telemetry ID creation, initial state
- Reset tests: track restoration, motor off
- Motor tests: telemetry publishing on state change, no publish on same value
- Track stepping tests: increment/decrement, clamping, telemetry, provider notification
- Disk operations tests: HasDisk, InsertDisk throws without factory, EjectDisk disposes/flushes
- Bit operations tests: GetBit/SetBit delegation, null/off handling
- Resend request tests: full state published on request

**Helper Created:** `Pandowdy.EmuCore.Tests\Helpers\MockTelemetryAggregator.cs`
- Mock implementation of ITelemetryAggregator for unit testing
- Records all published messages and resend requests
- Provides helper methods for test assertions

### Step 4.3: Copy DiskIIDebugDecorator ✅ COMPLETED

**Source:** `Pandowdy.DiskImportCode\DiskIIDebugDecorator.cs`  
**Target:** `Pandowdy.EmuCore\DiskII\DiskIIDebugDecorator.cs`

**Changes:**
- Update namespace to `Pandowdy.EmuCore.DiskII`
- Fix brace style and indentation
- Use `DiskIIConstants.TrackCount` instead of magic number 35
- Enhanced XML documentation with usage examples
- Renamed interface references from `IDiskDrive` to `IDiskIIDrive` in comments

**Tests:** `Pandowdy.EmuCore.Tests\DiskII\DiskIIDebugDecoratorTests.cs` (17 tests) ✅
- Constructor tests: null rejection, wrapping
- Property delegation tests: Name, Track, QuarterTrack, MotorOn (get/set), HasDisk
- Method delegation tests: Reset, StepToHigherTrack, StepToLowerTrack, GetBit, SetBit, IsWriteProtected, InsertDisk, EjectDisk
- Decorator chain tests: can wrap another decorator

### Step 4.4: Remove DiskIIStatusDecorator ✅ COMPLETED

**Action:** Do NOT copy this file. Functionality replaced by telemetry in DiskIIDrive.

### Step 4.5: Create DiskIIFactory ✅ COMPLETED

**Source:** `Pandowdy.DiskImportCode\DiskIIFactory.cs`  
**Target:** `Pandowdy.EmuCore\DiskII\DiskIIFactory.cs`

**Major Changes:**
1. Update namespace to `Pandowdy.EmuCore.DiskII`
2. Remove `IDiskStatusMutator` parameter - replaced with `ITelemetryAggregator`
3. Remove `DiskIIStatusDecorator` from decorator chain
4. Pass telemetry, slot, and drive numbers to DiskIIDrive constructor
5. Added CreateDriveWithDisk method for loading disks at creation
6. Improved ParseDriveName with TryParse for robustness
7. Made ParseDriveName internal for test access

**Tests:** `Pandowdy.EmuCore.Tests\DiskII\DiskIIFactoryTests.cs` (25 tests) ✅
- Constructor tests: null parameter rejection
- CreateDrive tests: returns wrapped drive, sets name, creates empty, registers telemetry
- CreateDriveWithDisk tests: loads disk, sets name, returns wrapped drive
- ParseDriveName tests: valid formats, invalid formats, null handling

### Step 4.6: Verify Build ✅ COMPLETED

All drive components compile correctly. Full test suite passes (1089 tests).

---

## Phase 4: Drive Implementation ✅ COMPLETED

**Summary:** All drive implementation steps complete. Phase 4 added:
- NullDiskIIDrive (21 tests)
- DiskIIDrive with telemetry (33 tests)
- DiskIIDebugDecorator (17 tests)
- DiskIIFactory (25 tests)
- MockTelemetryAggregator helper

---

## Phase 5: Controller Card ✅ COMPLETED

**Goal:** Add controller card implementations.

### Step 5.1: Create Cards folder ✅ COMPLETED

Created directory: `Pandowdy.EmuCore\Cards\`

### Step 5.2: Extract DiskIIControllerCard Base Class ✅ COMPLETED

**Source:** `Pandowdy.DiskImportCode\DiskIIControllerCards.cs` (lines 32-1070)  
**Target:** `Pandowdy.EmuCore\Cards\DiskIIControllerCard.cs`

**Changes Applied:**
1. Updated namespace to `Pandowdy.EmuCore.Cards`
2. Removed `IDiskStatusMutator` dependency
3. Added `ITelemetryAggregator` dependency (required in constructor)
4. Created telemetry ID for controller-level events
5. Replaced `_diskStatusMutator.MutateDrive(...)` calls with telemetry publishes using `DiskIIMessage`
6. Added `Slot` property implementation (required by ICard interface)
7. Used `DiskIIConstants.CyclesPerBit` for timing values
8. Fixed brace style throughout
9. Extracted large nested logic in `ProcessBits()` into helper methods:
   - `ProcessAddressFieldByte()`
   - `ProcessDataFieldByte()`
   - `CheckForPrologues()`

**Key Telemetry Changes:**

```csharp
// In UpdatePhaseState():
private void UpdatePhaseState()
{
    int driveNumber = _selectedDriveIndex + 1;
    _telemetry.Publish(new TelemetryMessage(
        _telemetryId,
        "phase",
        new DiskIIMessage($"Drive {driveNumber}: Phase={Convert.ToString(_currentPhase, 2).PadLeft(4, '0')}")));
}

// In UpdateMotorOffScheduledStatus():
private void UpdateMotorOffScheduledStatus(bool scheduled)
{
    int driveNumber = _selectedDriveIndex + 1;
    _telemetry.Publish(new TelemetryMessage(
        _telemetryId,
        "motor-off-scheduled",
        new DiskIIMessage($"Drive {driveNumber}: Motor-off {(scheduled ? "scheduled" : "cancelled/completed")}")));
}

// In UpdateTrackAndSector():
private void UpdateTrackAndSector(double track, int sector)
{
    int driveNumber = _selectedDriveIndex + 1;
    _telemetry.Publish(new TelemetryMessage(
        _telemetryId,
        "sector",
        new DiskIIMessage($"Drive {driveNumber}: Track={track:F2}, Sector={sector}")));
}
```

### Step 5.3: Extract DiskIIControllerCard16Sector ✅ COMPLETED

**Source:** `Pandowdy.DiskImportCode\DiskIIControllerCards.cs` (lines 1072-1113)  
**Target:** `Pandowdy.EmuCore\Cards\DiskIIControllerCard16Sector.cs`

**Changes Applied:**
- Separate file with 16-sector boot ROM (256 bytes)
- Updated constructor signature with telemetry parameter
- Id = 10
- Name = "Disk II"
- Description = "Disk II Controller - 16-Sector ROM"

### Step 5.4: Extract DiskIIControllerCard13Sector ✅ COMPLETED

**Source:** `Pandowdy.DiskImportCode\DiskIIControllerCards.cs` (lines 1115-1152)  
**Target:** `Pandowdy.EmuCore\Cards\DiskIIControllerCard13Sector.cs`

**Changes Applied:**
- Separate file with 13-sector boot ROM (256 bytes)
- Updated constructor signature with telemetry parameter
- Id = 11
- Name = "Disk II (13-Sector)"
- Description = "Disk II Controller - 13-Sector ROM"

### Step 5.5: Add ICard.Slot Property ✅ COMPLETED

Implemented `Slot` property in base class `DiskIIControllerCard`:
- Property returns `_slotNumber` field
- Initially `SlotNumber.Unslotted`
- Set to actual slot in `OnInstalled(SlotNumber slot)`

### Step 5.6: Verify Build ✅ COMPLETED

All card components compile correctly.

### Step 5.7: Add Unit Tests ✅ COMPLETED

**File:** `Pandowdy.EmuCore.Tests\Cards\DiskIIControllerCardTests.cs` (53 tests)

**Test Categories:**
- Constructor tests (5 tests): null parameter rejection, valid construction
- ICard interface tests - 16 Sector (5 tests): Name, Description, Id, Slot, Clone
- ICard interface tests - 13 Sector (4 tests): Name, Description, Id, Clone
- OnInstalled tests (5 tests): slot assignment, drive creation, naming
- ROM tests - 16 Sector (3 tests): valid bytes, boot signature
- ROM tests - 13 Sector (2 tests): valid bytes, differs from 16-sector
- I/O Read tests (12 tests): phase, motor, drive select, Q6/Q7
- I/O Write tests (3 tests): phase, motor, ROM write protection
- Reset tests (3 tests): motor off, cancel schedule, drive selection
- Extended ROM tests (2 tests): returns null, write is no-op
- Metadata tests (3 tests): get/apply metadata
- Motor off delay tests (3 tests): delayed off, VBlank timing, cancel
- Stepper motor tests (1 test): phase control moves head

**Test Helpers Created:**
- `MockDiskIIFactory` - Mock factory for creating test drives
- `MockDiskIIDrive` - Mock drive for controller testing

---

## Phase 6: Factory Registration ✅ COMPLETED

**Goal:** Register Disk II cards with CardFactory.

### Step 6.1: Register DI Services ✅ COMPLETED

**File:** `Pandowdy\Program.cs`

**Changes Applied:**

1. Added using statements:
   ```csharp
   using Pandowdy.EmuCore.Cards;
   using Pandowdy.EmuCore.DiskII;
   using Pandowdy.EmuCore.DiskII.Providers;
   ```

2. Registered Disk II subsystem services:
   ```csharp
   // Disk II subsystem
   services.AddSingleton<IDiskImageFactory, DiskImageFactory>();
   services.AddSingleton<IDiskIIFactory, DiskIIFactory>();
   ```

3. Registered Disk II controller cards:
   ```csharp
   // Cards
   services.AddTransient<ICard, NullCard>();
   services.AddTransient<ICard, DiskIIControllerCard16Sector>();
   services.AddTransient<ICard, DiskIIControllerCard13Sector>();
   ```

**DI Resolution Chain:**
- `DiskImageFactory` - no dependencies (simple factory)
- `DiskIIFactory` - depends on `IDiskImageFactory`, `ITelemetryAggregator`
- `DiskIIControllerCard16Sector` - depends on `CpuClockingCounters`, `IDiskIIFactory`, `ITelemetryAggregator`
- `DiskIIControllerCard13Sector` - depends on `CpuClockingCounters`, `IDiskIIFactory`, `ITelemetryAggregator`

### Step 6.2: Verify Build ✅ COMPLETED

Build successful. All 1196 tests pass.

**Cards Now Available in CardFactory:**
| Id | Name | Description |
|----|------|-------------|
| 0 | (Empty Slot) | NullCard |
| 10 | Disk II | Disk II Controller - 16-Sector ROM |
| 11 | Disk II (13-Sector) | Disk II Controller - 13-Sector ROM |

---

## Phase 7: Integration Tests and Test Utilities ✅ COMPLETED

**Goal:** Create integration tests and shared test utilities.

> **Note:** Unit tests were created alongside each implementation step (Phases 3-6).
> This phase added integration tests and verified the complete system.

### Step 7.1: Test Directory Structure ✅ COMPLETED

Tests created incrementally with each phase. Final structure:

```
Pandowdy.EmuCore.Tests/
├── DiskII/
│   ├── GcrEncoderTests.cs              (Phase 3.1)
│   ├── DiskIIDriveTests.cs             (Phase 4.2)
│   ├── DiskIIFactoryTests.cs           (Phase 4.5)
│   ├── DiskIIControllerCardTests.cs    (Phase 5.7)
│   ├── DiskIIIntegrationTests.cs       (Phase 7.3) ← NEW
│   └── Providers/
│       ├── DiskImageFactoryTests.cs           (Phase 3.3)
│       ├── NibDiskImageProviderTests.cs       (Phase 3.4)
│       ├── SectorDiskImageProviderTests.cs    (Phase 3.5)
│       ├── InternalWozDiskImageProviderTests.cs (Phase 3.6)
│       └── WozDiskImageProviderTests.cs       (Phase 3.7)
├── Cards/
│   └── DiskIIControllerCardTests.cs    (Phase 5.7)
├── Helpers/
│   └── MockTelemetryAggregator.cs      (Phase 4.2)
```

### Step 7.2: Create Mock Telemetry Aggregator ✅ COMPLETED

Created in Phase 4.2 (DiskIIDrive with telemetry).

### Step 7.3: Integration Tests ✅ COMPLETED

**File:** `Pandowdy.EmuCore.Tests\\DiskII\\DiskIIIntegrationTests.cs` (28 tests)

**Test Categories:**

| Category | Tests | Description |
|----------|-------|-------------|
| Controller + Drive Integration | 6 | Verify controller creates drives, motor control |
| Telemetry Flow | 4 | Verify telemetry messages flow from drives to aggregator |
| Motor Timeout (VBlank) | 4 | Test 60-VBlank motor-off delay, cancellation |
| Multi-Drive | 4 | Test drive selection, motor state preservation |
| Phase Control | 2 | Test stepper motor phase activation |
| Q6/Q7 Mode | 2 | Test read mode, write protect sensing |
| Factory Integration | 3 | Test factory creates wrapped drives |
| Full Stack | 3 | End-to-end tests of complete system |

**Total Disk II Tests:** 259

---

## Telemetry Integration

### Current Approach (Simplified for Development)

During initial development, we use a single simple `DiskIIMessage` type for debug output:

```csharp
// Publisher:
_telemetry.Publish(new TelemetryMessage(_telemetryId, "debug", 
    new DiskIIMessage("Disk Motor is now ON")));

// Subscriber (debug logging):
telemetry.Stream
    .Where(m => m.SourceId.Category == "DiskII")
    .Subscribe(m => Debug.WriteLine($"[{m.SourceId}] {m.Payload}"));
```

### Future Approach (Typed Payloads)

When the GUI is ready to consume telemetry, replace `DiskIIMessage` with typed records:

| Future Payload Type | Key | Value(s) | Description |
|---------------------|-----|----------|-------------|
| `DiskIIMotorMessage` | "motor" | `bool Value` | Motor on/off |
| `DiskIITrackMessage` | "track" | `double Value` | Track position (0-34.75) |
| `DiskIITrackSectorMessage` | "track-sector" | `double Track, int Sector` | Track + sector |
| `DiskIIDiskInsertedMessage` | "disk-inserted" | `string Value` | Disk filename |
| `DiskIIDiskEjectedMessage` | "disk-ejected" | (none) | Disk removed |
| `DiskIIWriteProtectMessage` | "write-protect" | `bool Value` | Write protection |
| `DiskIIPhaseMessage` | "phase" | `int DriveIndex, byte Value` | Stepper phases |
| `DiskIIMotorOffScheduledMessage` | "motor-off-scheduled" | `int DriveIndex, bool Value` | Motor-off pending |
| `DiskIIStateMessage` | "state" | (full state snapshot) | Complete state |

### Future UI Subscription Pattern

```csharp
// In DiskStatusViewModel (future):
private void HandleDiskTelemetry(TelemetryMessage msg)
{
    // Pattern match on payload type - no string switching needed!
    switch (msg.Payload)
    {
        case DiskIIMotorMessage motor:
            UpdateMotorState(msg.SourceId.Id, motor.Value);
            break;
        case DiskIITrackMessage track:
            UpdateTrack(msg.SourceId.Id, track.Value);
            break;
        case DiskIIStateMessage state:
            UpdateFullState(state);
            break;
        // ... etc
    }
}
```

---

## Code Style Corrections

The following style issues need to be fixed during import (per `.github\copilot-instructions.md`):

### 1. Always Use Braces

**Before:**
```csharp
if (condition)
    DoSomething();
```

**After:**
```csharp
if (condition)
{
    DoSomething();
}
```

### 2. Multi-line Properties with Logic

**Before:**
```csharp
public bool MotorOn { get => _motorOn; set { _motorOn = value; Debug.WriteLine("..."); } }
```

**After:**
```csharp
public bool MotorOn
{
    get => _motorOn;
    set
    {
        _motorOn = value;
        Debug.WriteLine("...");
    }
}
```

### 3. Consistent Indentation

Ensure 4-space indentation throughout.

---

## Verification Checklist

### After Each Phase

- [ ] Solution builds without errors
- [ ] No new warnings introduced
- [ ] Existing tests pass

### After Complete Integration

- [ ] DiskII controller cards appear in CardFactory
- [ ] Card can be installed in slot 6
- [ ] NIB disk images can be loaded
- [ ] WOZ disk images can be loaded
- [ ] DSK/DO/PO disk images can be loaded
- [ ] Motor control works (on/off with 1-sec delay)
- [ ] Track stepping works (0-34.75 quarter-tracks)
- [ ] Telemetry messages are published
- [ ] Telemetry resend requests work
- [ ] Boot from disk works (test with DOS 3.3 or ProDOS)

### Test Disk Images

Store test disk images in `assets/test-disks/`:
- `dos33-master.dsk` - DOS 3.3 system master
- `prodos.po` - ProDOS system disk
- `test.nib` - NIB format test
- `test.woz` - WOZ format test

---

## Notes & Reminders

1. **Git Operations:** Use `git mv` for any file moves to preserve history
2. **Build Often:** Run build after each file addition to catch errors early
3. **Incremental Commits:** Commit after each phase for easy rollback
4. **VBlankOccurred Event:** Critical foundation - must be added first
5. **Remove DiskIIStatusDecorator:** This pattern is replaced by telemetry
6. **Test Coverage:** Don't skip Phase 7 - these are critical components

---

## Next Steps (Post-Integration)

After the Disk II integration is complete, the following refactoring tasks should be addressed:

### NS-1: Migrate VA2M to CpuClockingCounters.VBlankOccurred

**Current State:**
- `VA2M` subscribes to `VA2MBus.VBlank` event (now marked `[Obsolete]`)
- `VA2MBus.VBlank` uses `EventHandler` signature `(object? sender, EventArgs e)`
- `CpuClockingCounters.VBlankOccurred` uses simpler `Action` signature

**Problem:**
- Two VBlank events fire on every frame (redundant)
- `VA2MBus.VBlank` is deprecated but still in use
- Inconsistent event patterns

**Proposed Solution:**

1. **Option A: Change OnVBlank signature** (Breaking change for tests)
   ```csharp
   // Change from:
   private void OnVBlank(object? sender, EventArgs e)
   
   // To:
   private void OnVBlank()
   ```
   Then subscribe via: `vb.ClockCounters.VBlankOccurred += OnVBlank;`

2. **Option B: Use lambda adapter** (Non-breaking)
   ```csharp
   if (Bus is VA2MBus vb)
   {
       vb.ClockCounters.VBlankOccurred += () => OnVBlank(null, EventArgs.Empty);
   }
   ```

3. **Option C: Inject CpuClockingCounters directly into VA2M** (Cleanest long-term)
   - Add `CpuClockingCounters` as a constructor parameter
   - Subscribe directly: `clockCounters.VBlankOccurred += OnVBlank;`
   - Remove `VA2MBus.VBlank` entirely

**Recommended:** Option C for cleanest architecture, but requires updating VA2M's 11 constructor parameters to 12.

**Files to Modify:**
- `Pandowdy.EmuCore\VA2M.cs` - Update constructor and subscription
- `Pandowdy.EmuCore\VA2MBus.cs` - Remove `VBlank` event entirely
- `Pandowdy.EmuCore.Tests\Helpers\VA2MTestHelpers.cs` - Update test factory
- Any tests that mock/verify VBlank behavior

**Priority:** Low - Current pragma suppressions work fine. Address after Disk II integration is stable.

---

### NS-2: Remove VA2MBus.VBlank Event

**Prerequisite:** NS-1 must be completed first.

**Steps:**
1. Remove `[Obsolete]` attribute and event declaration from `VA2MBus.cs`
2. Remove event invocation in `Clock()` method
3. Remove null assignment in `Dispose()` method
4. Update documentation to remove VBlank references
5. Run full test suite to verify no regressions

**Files to Modify:**
- `Pandowdy.EmuCore\VA2MBus.cs`

---

### NS-3: Consider IAppleIIBus Interface Update

**Current State:**
- `IAppleIIBus` doesn't expose `ClockCounters`
- Components that need timing must cast to `VA2MBus`

**Proposed:**
- Add `CpuClockingCounters ClockCounters { get; }` to `IAppleIIBus` interface
- Or create a new interface `ITimingProvider` that components can request

**Impact:** Would allow Disk II controller to receive `IAppleIIBus` instead of needing direct `CpuClockingCounters` injection.

---

### NS-4: Look at why mixed HGR flickers when 80-Column text is active. 

**Crrent State:**
- Apparent bug with rendering when 80-col mode is on and HGR is active.  Might exist on full-screen HGR too.
- Need to investiate.
- Probably a timing issue related to VBlank or rendering.
- It actually looks like maybe a race condition. This happens with GR mode too.  The flicker is a quick swap where aux memory's contents are being drawn.


---

## Appendix: File Mapping Summary

> **Note:** "Import" means reading from the read-only source and creating a new file in the target.
> Files are transformed during import (namespace changes, telemetry integration, style fixes).

| Source File | Target Location | Action | Notes |
|-------------|-----------------|--------|-------|
| `IDiskImageProvider.cs` | `Interfaces/` | Import | Namespace only |
| `IDiskIIDrive.cs` | `Interfaces/` | Import | Namespace + docs |
| `IDiskImageFactory.cs` | `Interfaces/` | Import | Namespace only |
| `IDiskIIFactory` (inline) | `Interfaces/IDiskIIFactory.cs` | Extract | New file from inline def |
| `DiskIIControllerCards.cs` | `Cards/DiskIIControllerCard*.cs` | Split + Modify | 3 files, add telemetry |
| `DiskIIDrive.cs` | `DiskII/` | Import + Modify | Add `ITelemetryAggregator` |
| `DiskIIFactory.cs` | `DiskII/` | Import + Modify | Remove status decorator |
| `DiskIIDebugDecorator.cs` | `DiskII/` | Import | Style fixes only |
| `DiskIIStatusDecorator.cs` | N/A | **SKIP** | Replaced by telemetry |
| `NullDiskIIDrive.cs` | `DiskII/` | Import | Style fixes only |
| `GcrEncoder.cs` | `DiskII/` | Import | Use `DiskIIConstants` |
| `DiskImageFactory.cs` | `DiskII/Providers/` | Import | Namespace change |
| `NibDiskImageProvider.cs` | `DiskII/Providers/` | Import | Use `DiskIIConstants` |
| `SectorDiskImageProvider.cs` | `DiskII/Providers/` | Import | Use `DiskIIConstants` |
| `InternalWozDiskImageProvider.cs` | `DiskII/Providers/` | Import | Use `DiskIIConstants` |
| `WozDiskImageProvider.cs` | `DiskII/Providers/` | Import | Use `DiskIIConstants` |

---

*Document Created: 2025*  
*Last Updated: All Phases Complete - Disk II Integration Finished (259 Disk II tests, 1224 total)*
