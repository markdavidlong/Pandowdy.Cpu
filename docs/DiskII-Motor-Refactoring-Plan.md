# Disk II Motor State Refactoring Plan

## Executive Summary
**Goal:** Move motor state management from individual `IDiskIIDrive` implementations to `DiskIIControllerCard`, reflecting the hardware reality that the controller has a single motor line powering only one drive at a time.

**Architecture Change:**
- **Before:** Each drive tracks its own `MotorOn` property; controller reads/writes this property
- **After:** Controller owns motor state (on/scheduled/off) and active drive index; drives are passive and always return data when asked

## Common Terminology

### Motor States
- **OFF**: Motor is stopped, no drive is powered
- **ON**: Motor is running, powering the currently selected drive
- **SCHEDULED_OFF**: Motor is running but scheduled to stop after delay (~1 second)

### Components
- **Controller**: `DiskIIControllerCard` - owns motor state and drive selection
- **Drive**: `IDiskIIDrive` implementations - passive, mechanical only (head position, disk media)
- **Status Decorator**: `DiskIIStatusDecorator` - publishes drive state to UI
- **Status Mutator**: `IDiskStatusMutator` - receives state updates for UI

### Key Files
- **Interface**: `Pandowdy.EmuCore\Interfaces\IDiskIIDrive.cs`
- **Implementations**: `DiskIIDrive.cs`, `NullDiskIIDrive.cs`
- **Controller**: `DiskIIControllerCard.cs`
- **Decorator**: `DiskIIStatusDecorator.cs` (to be found/updated)
- **Tests**: `DiskIIDriveTests.cs`, `NullDiskIIDriveTests.cs`, `DiskIIControllerCardTests.cs`, `DiskIIDebugDecoratorTests.cs`, `DiskIISpecificationTests.cs`

## Refactoring Phases

---

## **Phase 1: Add Controller Motor State (Additive Only)**
**Status:** ✅ **COMPLETE**  
**Goal:** Add new motor state tracking to controller WITHOUT removing drive motor state  
**Duration:** ~30 minutes  
**Risk:** LOW (purely additive, no breaking changes)

### Steps

#### 1.1: Define Motor State Enum ✅
**File:** `Pandowdy.EmuCore\Cards\DiskIIControllerCard.cs`
**Action:** ✅ Added enum definition before class

#### 1.2: Add Controller Motor State Field ✅
**File:** `Pandowdy.EmuCore\Cards\DiskIIControllerCard.cs`
**Action:** ✅ Added `_motorState` field after `_selectedDriveIndex`

#### 1.3: Add Helper Property ✅
**File:** `Pandowdy.EmuCore\Cards\DiskIIControllerCard.cs`
**Action:** ✅ Added `IsMotorRunning` property after `SelectedDrive`

### Verification Criteria
- [x] Project builds successfully
- [x] All existing tests pass (no behavior changes yet) - *Note: 1 test failing from previous motor transfer change, not Phase 1*
- [x] New field is defined but unused (shadow state)

### Rollback Point
**Checkpoint 1A**: ✅ New motor state added, old behavior unchanged

---

## **Phase 2: Dual-Track Motor Control (Transition Phase)**
**Status:** ✅ **COMPLETE**  
**Goal:** Update controller methods to write to BOTH old and new motor state  
**Duration:** ~45 minutes  
**Risk:** MEDIUM (logic changes but maintains backward compatibility)

### Steps

#### 2.1: Update HandleMotorControl() - Motor ON ✅
**File:** `Pandowdy.EmuCore\Cards\DiskIIControllerCard.cs`  
**Method:** `HandleMotorControl()`  
**Action:** ✅ When motor turns ON, now updates both `_motorState = On` and `drive.MotorOn = true`

#### 2.2: Update HandleMotorControl() - Motor OFF Scheduled ✅
**File:** `Pandowdy.EmuCore\Cards\DiskIIControllerCard.cs`  
**Method:** `HandleMotorControl()`  
**Action:** ✅ When motor-off is scheduled, sets `_motorState = ScheduledOff`

#### 2.3: Update CheckPendingMotorOff() ✅
**File:** `Pandowdy.EmuCore\Cards\DiskIIControllerCard.cs`  
**Method:** `CheckPendingMotorOff()`  
**Action:** ✅ When motor turns off, sets both `_motorState = Off` and `drive.MotorOn = false`

#### 2.4: Update HandleDriveSelection() ✅
**File:** `Pandowdy.EmuCore\Cards\DiskIIControllerCard.cs`  
**Method:** `HandleDriveSelection()`  
**Action:** ✅ Now uses `IsMotorRunning` instead of `oldDrive.MotorOn` check

#### 2.5: Update Reset() ✅
**File:** `Pandowdy.EmuCore\Cards\DiskIIControllerCard.cs`  
**Method:** `Reset()`  
**Action:** ✅ Resets `_motorState = Off` alongside drive motor resets

### Verification Criteria
- [x] Project builds successfully
- [x] All existing tests pass (1776/1777 - same as Phase 1)
- [x] Both old and new motor states stay synchronized
- [x] Debug output shows correct motor state transitions

### Rollback Point
**Checkpoint 2A**: ✅ Dual-track motor state working, old API still functional

---

## **Phase 3: Update Drive Reads to Use Controller State**
**Status:** ✅ **COMPLETE**  
**Goal:** Change all controller methods to check controller motor state instead of drive motor state  
**Duration:** ~20 minutes (actual)  
**Risk:** MEDIUM (changes data flow path)

### Steps

#### 3.1: Update ProcessBits() Motor Check ✅
**File:** `Pandowdy.EmuCore\Cards\DiskIIControllerCard.cs`  
**Method:** `ProcessBits()`  
**Action:** ✅ Changed motor state check from `!drive.MotorOn` to `!IsMotorRunning`

#### 3.2: Update HandlePhaseControl() Motor Check ✅
**File:** `Pandowdy.EmuCore\Cards\DiskIIControllerCard.cs`  
**Method:** `HandlePhaseControl()`  
**Action:** ✅ Changed motor state check from `SelectedDrive.MotorOn` to `IsMotorRunning`

#### 3.3: Update ReadIO() Motor Check ✅
**File:** `Pandowdy.EmuCore\Cards\DiskIIControllerCard.cs`  
**Method:** `ReadIO()`  
**Action:** ✅ Changed motor-off read path check from `SelectedDrive.MotorOn` to `IsMotorRunning`

#### 3.4: Update HandleQ6Q7Read() Motor Check ✅
**File:** `Pandowdy.EmuCore\Cards\DiskIIControllerCard.cs`  
**Method:** `HandleQ6Q7Read()`  
**Action:** ✅ Changed motor state check from `SelectedDrive.MotorOn` to `IsMotorRunning`

#### 3.5: Update WriteShiftRegister() Motor Check ✅
**File:** `Pandowdy.EmuCore\Cards\DiskIIControllerCard.cs`  
**Method:** `WriteShiftRegister()`  
**Action:** ✅ Changed motor state check from `!drive.MotorOn` to `!IsMotorRunning`

### Verification Criteria
- [x] Project builds successfully
- [x] All existing tests pass (2049/2050 - same as Phase 2, pre-existing failure)
- [x] Controller now exclusively checks its own motor state
- [x] Drives still maintain motor state but it's not consulted by controller

### Rollback Point
**Checkpoint 3A**: ✅ Controller reads from its own motor state

---

## **Phase 4: Remove MotorOn from IDiskIIDrive Interface**
**Status:** ✅ **COMPLETE** (Production Code)  
**Goal:** Remove `MotorOn` property from interface and implementations  
**Duration:** ~45 minutes (actual)  
**Risk:** HIGH (breaking change, requires test updates)

### Steps

#### 4.1: Comment Out Interface Property ✅
**File:** `Pandowdy.EmuCore\Interfaces\IDiskIIDrive.cs`  
**Action:** ✅ Commented out MotorOn property with Phase 4 marker

#### 4.2: Build and Catalog Compilation Errors ✅
**Action:** ✅ Ran build, documented all compilation errors  
**Errors Found:**
- **DiskIIDebugDecorator.cs**: 2 errors (lines 68, 71) - MotorOn property passthrough
- **DiskIIControllerCard.cs**: 6 errors (lines 436, 451, 496, 595, 598, 1141, 1146) - drive motor assignments
- **DiskIIStatusDecorator.cs**: 4 errors (lines 76, 79, 80, 197) - motor state tracking and sync
- **Production code**: 12 total errors
- **Test files**: 50+ errors (expected, Phase 5-6 will handle)

#### 4.3: Remove MotorOn from DiskIIDrive ✅
**File:** `Pandowdy.EmuCore\DiskII\DiskIIDrive.cs`  
**Actions:** ✅ All complete
1. ✅ Removed `_motorOn` field
2. ✅ Removed `MotorOn` property getter/setter
3. ✅ Updated `Reset()` - removed motor-off call
4. ✅ Removed motor initialization from constructor
5. ✅ Removed motor checks from `GetBit()` and `SetBit()` (controller handles motor state)

#### 4.4: Remove MotorOn from NullDiskIIDrive ✅
**File:** `Pandowdy.EmuCore\DiskII\NullDiskIIDrive.cs`  
**Actions:** ✅ All complete
1. ✅ Removed `_motor` field
2. ✅ Removed `MotorOn` property
3. ✅ Updated `Reset()` - removed motor-off call

#### 4.5: Update DiskIIStatusDecorator ✅
**File:** `Pandowdy.EmuCore\DiskII\DiskIIStatusDecorator.cs`  
**Action:** ✅ Removed motor state forwarding
- ✅ Removed `MotorOn` property passthrough
- ✅ Removed `MotorOn` update from `SyncStatus()`
- Decorator now only forwards mechanical state (track, disk insertion)
- Motor state is now controller-only

#### 4.6: Update DiskIIDebugDecorator ✅
**File:** `Pandowdy.EmuCore\DiskII\DiskIIDebugDecorator.cs`  
**Action:** ✅ Removed motor state logging from drive wrapper
- ✅ Removed `MotorOn` property passthrough
- Motor logging should happen in controller only

#### 4.7: Update DiskIIControllerCard ✅
**File:** `Pandowdy.EmuCore\Cards\DiskIIControllerCard.cs`  
**Actions:** ✅ Removed all drive motor assignments (Phase 7 work done early)
- ✅ HandleMotorControl(): Removed `SelectedDrive.MotorOn = true` assignment
- ✅ CheckPendingMotorOff(): Removed `SelectedDrive.MotorOn = false` assignment
- ✅ HandleDriveSelection(): Removed old/new drive motor transfer logic
- ✅ Reset(): Removed foreach loop turning off drive motors
- Controller now exclusively manages motor state, drives are passive

### Verification Criteria
- [x] Interface no longer declares `MotorOn`
- [x] Drive implementations no longer track motor state
- [x] Project builds with compilation errors only in tests (50+ test errors as expected)
- [x] Production code compiles cleanly ✅

### Rollback Point
**Checkpoint 4A**: ✅ Interface cleaned, production code compiles

---

## **Phase 5: Update Test Infrastructure**
**Status:** NOT STARTED  
**Goal:** Update test helpers, mocks, and fixtures to work without drive motor state  
**Duration:** ~60 minutes  
**Risk:** MEDIUM (tests may fail but production code is stable)

### Steps

#### 5.1: Update DiskIIDriveTests.cs
**File:** `Pandowdy.EmuCore.Tests\DiskII\DiskIIDriveTests.cs`  
**Actions:**
1. Remove tests that directly set/get `MotorOn` on drive
2. Update tests to verify mechanical behavior only (track position, disk insertion)
3. Add comments explaining motor state is controller-level

#### 5.2: Update NullDiskIIDriveTests.cs
**File:** `Pandowdy.EmuCore.Tests\DiskII\NullDiskIIDriveTests.cs`  
**Actions:**
1. Remove motor state tests
2. Verify null drive still handles all other operations

#### 5.3: Update DiskIIControllerCardTests.cs
**File:** `Pandowdy.EmuCore.Tests\Cards\DiskIIControllerCardTests.cs`  
**Actions:**
1. Add tests for new `_motorState` field behavior
2. Add tests for `IsMotorRunning` property
3. Update existing motor control tests to verify controller state instead of drive state
4. Add tests for motor state transitions (Off → On → ScheduledOff → Off)
5. Add tests for motor state persistence across drive selection changes

#### 5.4: Update DiskIIDebugDecoratorTests.cs
**File:** `Pandowdy.EmuCore.Tests\DiskII\DiskIIDebugDecoratorTests.cs`  
**Actions:**
1. Remove motor state logging tests
2. Update to verify only mechanical operation logging

#### 5.5: Update DiskIISpecificationTests.cs
**File:** `Pandowdy.EmuCore.Tests\DiskII\DiskIISpecificationTests.cs`  
**Actions:**
1. Update specification tests to reflect new architecture
2. Add specification test documenting controller motor ownership

### Verification Criteria
- [ ] All test files compile
- [ ] Test infrastructure works without drive motor state
- [ ] No skipped tests (all either pass or have clear TODO for Phase 6)

### Rollback Point
**Checkpoint 5A**: Test infrastructure updated, ready for individual test fixes

---

## **Phase 6: Fix Individual Failing Tests**
**Status:** NOT STARTED  
**Goal:** Fix each failing test case to work with controller motor state  
**Duration:** ~90 minutes (depends on test count)  
**Risk:** LOW (isolated test fixes)

### Approach
For each failing test:
1. Identify what behavior it's testing
2. Determine if test is still valid (mechanical behavior) or obsolete (motor state)
3. Either fix or remove the test
4. Document decision

### Test Categories

#### Category A: Mechanical Tests (Keep & Fix)
Tests verifying:
- Head positioning (StepToHigherTrack, StepToLowerTrack)
- Track/QuarterTrack properties
- Disk insertion/ejection
- Write protection checks
- Reset behavior (excluding motor)

**Action:** Update assertions to not reference motor state

#### Category B: Motor State Tests (Remove or Move)
Tests verifying:
- MotorOn property getter/setter
- Motor state after reset
- Motor state in debug output

**Action:** Remove from drive tests, verify equivalent coverage in controller tests

#### Category C: Integration Tests (Update)
Tests verifying:
- Combined motor + head movement
- Combined motor + bit reading

**Action:** Update to use controller motor state or split into separate tests

### Tracking Template
```
Test: [Test Name]
File: [File Path]
Category: [A/B/C]
Action: [Keep & Fix / Remove / Move to Controller Tests]
Status: [NOT STARTED / IN PROGRESS / COMPLETE]
Notes: [Any important details]
```

### Verification Criteria
- [ ] All tests pass or are documented as removed
- [ ] No skipped tests
- [ ] Test coverage for motor state exists at controller level
- [ ] Test coverage for mechanical operations exists at drive level

### Rollback Point
**Checkpoint 6A**: All tests passing with new architecture

---

## **Phase 7: Remove Drive Motor Synchronization**
**Status:** NOT STARTED  
**Goal:** Remove all `drive.MotorOn = ...` assignments from controller  
**Duration:** ~30 minutes  
**Risk:** LOW (cleanup only, tests already passing)

### Steps

#### 7.1: Remove Motor Assignments from HandleMotorControl()
**File:** `Pandowdy.EmuCore\Cards\DiskIIControllerCard.cs`  
**Method:** `HandleMotorControl()`  
**Action:** Remove `SelectedDrive.MotorOn = true;` line

#### 7.2: Remove Motor Assignments from HandleDriveSelection()
**File:** `Pandowdy.EmuCore\Cards\DiskIIControllerCard.cs`  
**Method:** `HandleDriveSelection()`  
**Action:** Remove old/new drive motor on/off assignments

#### 7.3: Remove Motor Assignments from CheckPendingMotorOff()
**File:** `Pandowdy.EmuCore\Cards\DiskIIControllerCard.cs`  
**Method:** `CheckPendingMotorOff()`  
**Action:** Remove `SelectedDrive.MotorOn = false;` line

#### 7.4: Remove Motor Assignments from Reset()
**File:** `Pandowdy.EmuCore\Cards\DiskIIControllerCard.cs`  
**Method:** `Reset()`  
**Action:** Remove foreach loop that turns off drive motors

### Verification Criteria
- [ ] No references to `drive.MotorOn` remain in controller
- [ ] Project builds
- [ ] All tests still pass

### Rollback Point
**Checkpoint 7A**: Complete motor state migration, old API removed

---

## **Phase 8: Documentation and Cleanup**
**Status:** NOT STARTED  
**Goal:** Update documentation and add inline comments explaining new architecture  
**Duration:** ~45 minutes  
**Risk:** NONE (documentation only)

### Steps

#### 8.1: Update Interface Documentation
**File:** `Pandowdy.EmuCore\Interfaces\IDiskIIDrive.cs`  
**Action:** Update class-level remarks to clarify drive is passive

#### 8.2: Update Controller Documentation
**File:** `Pandowdy.EmuCore\Cards\DiskIIControllerCard.cs`  
**Action:** Update class-level remarks to document motor state ownership

#### 8.3: Update Drive Implementation Documentation
**Files:** `DiskIIDrive.cs`, `NullDiskIIDrive.cs`  
**Action:** Add remarks explaining drives don't track motor state

#### 8.4: Update Copilot Instructions
**File:** `.github\copilot-instructions.md`  
**Action:** Add section documenting Disk II motor architecture

#### 8.5: Update Development Roadmap
**File:** `docs\Development-Roadmap.md`  
**Action:** Mark this refactoring as complete

### Verification Criteria
- [ ] All public APIs have updated XML documentation
- [ ] Copilot instructions reflect new architecture
- [ ] Roadmap updated

### Completion Checklist
- [ ] All phases complete
- [ ] All tests passing
- [ ] Documentation updated
- [ ] Git commit with detailed message

---

## Progress Tracking

### Current Phase: **Phase 2 COMPLETE**
### Last Checkpoint: **Checkpoint 2A - Dual-track motor control working**
### Next Action: **Begin Phase 3, Step 3.1**

### Phase Completion Status
- [x] Phase 1: Add Controller Motor State ✅ **COMPLETE**
- [x] Phase 2: Dual-Track Motor Control ✅ **COMPLETE**
- [ ] Phase 3: Update Drive Reads
- [ ] Phase 4: Remove Interface Property
- [ ] Phase 5: Update Test Infrastructure
- [ ] Phase 6: Fix Individual Tests
- [ ] Phase 7: Remove Synchronization
- [ ] Phase 8: Documentation

---


## Validation Strategy

### After Each Phase
1. **Build:** Must compile cleanly (except Phase 4-5 where test errors are expected)
2. **Tests:** Run full test suite, document pass/fail
3. **Manual Smoke Test:** Load disk, verify motor sounds/UI updates correctly
4. **Git:** Commit with message format: `"Disk II Motor Refactor: Phase X - [Description]"`

### Final Validation (After Phase 8)
1. **Full Test Suite:** All 2,050+ tests must pass
2. **Manual Testing:** Test all motor scenarios:
   - Motor on/off
   - Drive selection with motor running
   - Motor-off delay
   - Reset during motor operation
3. **Performance:** Verify no performance regression in disk I/O
4. **Code Review:** Self-review all changes for clarity and correctness

---

## Risk Mitigation

### High-Risk Areas
1. **ProcessBits()** - Critical path for disk I/O, timing-sensitive
2. **HandleDriveSelection()** - Complex motor state transfer logic
3. **Test Suite** - Large surface area, many tests may need updates

### Mitigation Strategies
1. **Incremental:** Each phase is atomic and testable
2. **Dual-Track:** Phase 2 maintains both old and new state for safety
3. **Checkpoints:** Clear rollback points after each phase
4. **Test-Driven:** Tests must pass before moving to next phase

---

## Notes and Observations

### Implementation Notes
**Phase 1 (Complete):**
- Added `DiskIIMotorState` enum with three states: Off, On, ScheduledOff
- Added `_motorState` field to DiskIIControllerCard (initialized to Off)
- Added `IsMotorRunning` convenience property for state checks
- All changes purely additive - no behavioral changes
- Build successful, shadow state in place

**Phase 2 (Complete):**
- Updated HandleMotorControl() to set both `_motorState` and `drive.MotorOn` for motor ON
- Updated HandleMotorControl() to set `_motorState = ScheduledOff` when motor-off is scheduled
- Updated CheckPendingMotorOff() to set both states when motor actually turns off
- Updated HandleDriveSelection() to use `IsMotorRunning` instead of checking `drive.MotorOn`
- Updated Reset() to set `_motorState = Off` alongside drive resets
- Both old and new states remain synchronized throughout all operations
- All existing functionality preserved (backward compatible)

**Phase 3 (Complete):**
- Updated ProcessBits() to check `IsMotorRunning` instead of `drive.MotorOn` (critical disk I/O path)
- Updated HandlePhaseControl() to check `IsMotorRunning` for head movement authorization
- Updated ReadIO() to check `IsMotorRunning` in motor-off read path (copy-protected disk support)
- Updated HandleQ6Q7Read() to check `IsMotorRunning` for latch operations
- Updated WriteShiftRegister() to check `IsMotorRunning` for write authorization
- Controller now exclusively reads from `_motorState` via `IsMotorRunning` property
- Drives still maintain `MotorOn` property (Phase 2 writes to it) but controller never reads from it
- Read-side migration complete - safe to remove drive motor property in Phase 4
- No new test failures (2049/2050 passing, same pre-existing failure)

**Phase 4 (Complete - Production Code):**
- Commented out `MotorOn` property in `IDiskIIDrive` interface with Phase 4 marker
- Removed `_motorOn` field and property from `DiskIIDrive` and `NullDiskIIDrive`
- Removed motor checks from `GetBit()` and `SetBit()` - controller ensures motor is running
- Updated `Reset()` methods - removed motor-off calls (motor is controller-level)
- Removed `MotorOn` property from both decorators (`DiskIIDebugDecorator`, `DiskIIStatusDecorator`)
- Removed all drive motor assignments from controller (HandleMotorControl, CheckPendingMotorOff, HandleDriveSelection, Reset)
- **Phase 7 work completed early** - all drive motor synchronization removed in Phase 4
- Production code compiles cleanly - motor state fully migrated to controller
- 50+ test errors remaining (expected, will be addressed in Phase 5-6)

### Unexpected Discoveries
**Phase 1:**
- Pre-existing test failure in `DriveSelect_Switching_ShouldImmediatelyTurnOffOldDrive` from previous motor transfer change
- This test failure is NOT from Phase 1, but from an earlier session's motor transfer implementation
- The test expects Drive 2 motor to remain OFF after switching, but current code transfers motor state
- Will need to address this test during Phase 5-6

**Phase 2:**
- No new test failures introduced
- Motor state synchronization working correctly across all scenarios
- The `IsMotorRunning` abstraction makes code more readable and intention-revealing

### Future Improvements
*(To be filled in during refactoring)*
