# CPU Architecture Refactoring Plan

## Status: ✅ Complete

**Refactoring completed successfully with:**
- 2148/2148 CPU tests passing ✅
- 1152/1152 EmuCore tests passing ✅
- Harte-SST-Tests migrated ✅
- Dormann-Tests migrated ✅
- 0 build errors
- Clean architecture with direct state ownership
- **Performance improvement: 74.75 µs → 45.37 µs (+39% faster)**

## Performance Results

| Metric | Original | After Refactoring | Improvement |
|--------|----------|-------------------|-------------|
| Benchmark | 74.75 µs | 45.37 µs | **+39.3%** |
| Allocations | 0 B | 0 B | - |
| Est. Throughput | ~4.2 MHz | ~7 MHz | **+67%** |

## Overview

Refactored from `CpuStateBuffer` double-buffering to a simpler direct-state architecture where:
- `CpuBase` receives a `CpuState` via dependency injection (provided to `CpuFactory`)
- `CpuStateBuffer` is deprecated (marked `[Obsolete]`)
- `DebugCpu` wrapper provides state tracking and debugging helpers

## Goals

1. **Simplify architecture**: ✅ Removed indirection through `CpuStateBuffer`
2. **Improve performance**: ✅ Eliminated buffer-related overhead (+39% faster)
3. **Maintain debugging capability**: ✅ `DebugCpu` wrapper implemented with 26 tests
4. **Clean API**: ✅ `cpu.State` instead of `cpu.Buffer.Current`

## Old Architecture (Before)

```
┌─────────────────────────────────────────┐
│            CpuStateBuffer               │
│  ┌───────────────┐  ┌───────────────┐   │
│  │ Prev (opt.)   │  │   Current     │   │
│  │  CpuState     │  │   CpuState    │   │
│  └───────────────┘  └───────────────┘   │
└─────────────────────────────────────────┘
                ▲
                │
┌───────────────┴─────────────────────────┐
│             CpuBase                      │
│  _buffer: CpuStateBuffer                 │
└──────────────────────────────────────────┘
```

## New Architecture (After)

```
┌──────────────────────────────────────────┐
│              CpuBase                      │
│  _state: CpuState                         │
│  State { get; set; }                      │
└──────────────────────────────────────────┘
                ▲
                │ wraps (optional)
┌───────────────┴──────────────────────────┐
│           DebugCpu (Facade)               │
│  _cpu: CpuBase                            │
│  _prevState: CpuState (snapshot)          │
│  PcChanged, BranchOccurred, etc.          │
└───────────────────────────────────────────┘
```

## Key Discovery: `prev` Parameter is Unused

The `MicroOp` delegate signature includes a `prev` parameter:
```csharp
delegate void MicroOp(CpuState prev, CpuState current, IPandowdyCpuBus bus);
```

However, analysis shows:
- Only one micro-op (`CheckPageCrossing`) uses `prev`
- `CheckPageCrossing` is **never used in any pipeline**
- All active micro-ops only use `current`

This means we can safely pass `current` for both parameters.

## Files Changed

### Phase 1: Core CPU Changes (✅ Complete)

| File | Change |
|------|--------|
| `CpuBase.cs` | ✅ Replace `_buffer` with `_state`, add `State` property, update `Clock()` |
| `IPandowdyCpu.cs` | ✅ Replace `Buffer` property with `State` property |
| `Cpu6502.cs` | ✅ Update constructors to use `CpuState` |
| `Cpu65C02.cs` | ✅ Update constructors to use `CpuState` |
| `Cpu6502Simple.cs` | ✅ Update constructors to use `CpuState` |
| `Cpu65C02Rockwell.cs` | ✅ Update constructors to use `CpuState` |
| `CpuFactory.cs` | ✅ Update factory methods |

### Phase 2: Test Updates (✅ Complete)

| File | Change |
|------|--------|
| `CpuTestBase.cs` | ✅ Remove `CpuBuffer`, use `Cpu.State` |
| `CpuInstanceTests.cs` | ✅ Remove `CpuStateBuffer` usage |
| `CpuModuleTests.cs` | ✅ Replace `CpuBuffer.Current/Prev` with `CurrentState` |
| `CpuStateTests.cs` | ✅ Update factory calls |
| `CpuInterruptHandlingTests.cs` | ✅ Update factory calls |
| `CpuStateBufferTests.cs` | ✅ Removed (tested deprecated class) |
| `CpuStateBufferDebuggerTests.cs` | ✅ Removed (tested deprecated class) |

### Phase 3: EmuCore Updates (✅ Complete)

| File | Change |
|------|--------|
| `VA2M.cs` | ✅ Replace `Buffer.Current` with `State` |
| `VA2MBusTests.cs` | ✅ Update factory calls |
| `VA2MTestHelpers.cs` | ✅ Update factory calls |

### Phase 4: Main Application (✅ Complete)

| File | Change |
|------|--------|
| `Program.cs` | ✅ Remove `CpuStateBuffer` DI registration |

### Phase 5: Bug Fixes Found During Refactoring (✅ Complete)

| File | Fix |
|------|-----|
| `Pipelines.Branch.cs` | ✅ Fixed BBR/BBS page-crossing penalty - was modifying base `Pipeline` instead of `WorkingPipeline` |
| `MicroOps.cs` | ✅ Added `AppendToWorkingPipeline()` helper method |

### Phase 6: DebugCpu Implementation (✅ Complete)

| File | Action |
|------|--------|
| `DebugCpu.cs` | ✅ Created - debugging wrapper with state tracking |
| `DebugCpuTests.cs` | ✅ Created - 26 tests |
| `CpuFactory.cs` | ✅ Added `CreateDebug()` factory methods |

### Phase 7: Cleanup (✅ Complete)

| File | Action |
|------|--------|
| `CpuStateBuffer.cs` | ✅ Marked as `[Obsolete]` with message pointing to `DebugCpu` |
| `CpuState.cs` | ✅ Updated XML docs to reference `DebugCpu` instead of `CpuStateBuffer` |
| `CpuInterruptHandlingTests.cs` | ✅ Removed `CpuStateBuffer` usage |
| `CpuStateTests.cs` | ✅ Removed `CpuStateBuffer` usage |
| `Dormann-Tests/Program.cs` | ✅ Removed `CpuStateBuffer` usage |
| `Harte-SST-Tests/HarteTestRunner.cs` | ✅ Migrated to use `cpu.State` directly |

## Performance Summary

| Metric | Original | After Refactoring | Improvement |
|--------|----------|-------------------|-------------|
| Benchmark | 74.75 µs | 45.37 µs | **+39.3%** |
| Allocations | 0 B | 0 B | - |
| Est. Throughput | ~4.2 MHz | ~7 MHz | **+67%** |

## Test Summary

| Suite | Count | Status |
|-------|-------|--------|
| CPU Tests | 2148 | ✅ All passing |
| EmuCore Tests | 1152 | ✅ All passing |
| DebugCpu Tests | 26 | ✅ All passing |

## Files Created/Modified

### New Files
- `Pandowdy.Cpu/DebugCpu.cs` - Debugging wrapper with state tracking
- `Pandowdy.Cpu.Tests/DebugCpuTests.cs` - 26 tests for DebugCpu

### Modified Files
- `CpuBase.cs` - Now receives `CpuState` via dependency injection
- `IPandowdyCpu.cs` - `State` property instead of `Buffer`
- `CpuFactory.cs` - Added `CreateDebug()` methods
- `CpuStateBuffer.cs` - Marked `[Obsolete]`
- Various test files - Updated to use new API

## Migration Patterns

### Pattern 1: Factory Calls
```csharp
// Before
var buffer = new CpuStateBuffer();
var cpu = CpuFactory.Create(variant, buffer);

// After
var cpu = CpuFactory.Create(variant);
```

### Pattern 2: State Access
```csharp
// Before
cpu.Buffer.Current.PC
cpu.Buffer.Current.A
cpu.Buffer.Prev.PC  // For comparison

// After
cpu.State.PC
cpu.State.A
// Use DebugCpu for Prev state
```

### Pattern 3: Test Base Class
```csharp
// Before
protected CpuStateBuffer CpuBuffer { get; private set; }
protected CpuState CurrentState => CpuBuffer.Current;

// After
protected CpuState CurrentState => Cpu.State;
```

## DebugCpu Design (Future)

```csharp
public class DebugCpu : IPandowdyCpu
{
    private readonly CpuBase _cpu;
    private CpuState? _prevState;
    
    public CpuState State => _cpu.State;
    public CpuState? PrevState => _prevState;
    
    public bool Clock(IPandowdyCpuBus bus)
    {
        // Snapshot before new instruction
        if (_cpu.State.InstructionComplete || _cpu.State.PipelineIndex == 0)
        {
            _prevState = _cpu.State.Clone();
        }
        return _cpu.Clock(bus);
    }
    
    // Debugging helpers
    public bool PcChanged => _prevState != null && _prevState.PC != State.PC;
    public bool BranchOccurred => ...;
    // etc.
}
```

## Verification Results

| Test Suite | Result |
|------------|--------|
| CPU Tests | 2143/2143 ✅ |
| EmuCore Tests | 1152/1152 ✅ |
| Build | Successful ✅ |
| Benchmark | 45.37 µs (was 74.75 µs) ✅ |

## DebugCpu Implementation (✅ Complete)

The `DebugCpu` class provides debugging capabilities by wrapping any `IPandowdyCpu`:

```csharp
var cpu = new Cpu65C02();
var debugCpu = new DebugCpu(cpu);

debugCpu.Reset(bus);
debugCpu.Step(bus);

// Debugging helpers
if (debugCpu.BranchOccurred)
    Console.WriteLine("Branch taken!");

foreach (var reg in debugCpu.ChangedRegisters)
    Console.WriteLine($"{reg} changed");
```

**Features:**
- `PrevState` - State snapshot before the instruction
- `PcChanged` - True if PC changed
- `BranchOccurred` - True if a branch instruction was taken (opcode-aware)
- `ReturnOccurred` - True if RTS/RTI executed
- `InterruptOccurred` - True if interrupt handler entered
- `PageCrossed` - True if page boundary crossed during addressing
- `StackActivityOccurred` - True if SP changed
- `StackDelta` - Change in stack pointer (-1 = push, +1 = pull)
- `ChangedRegisters` - Enumerable of register names that changed

## Bug Found and Fixed

During refactoring, a latent bug was discovered in the BBR/BBS (Branch on Bit Reset/Set) instructions:

**Problem**: When a page-crossing penalty was needed, the code did:
```csharp
MicroOps.InsertAfterCurrentOp(current, penaltyT5);
current.Pipeline = [..current.Pipeline, penaltyT6];  // BUG: Modifies base, not working
```

The second line modified the base `Pipeline` array instead of the `WorkingPipeline`. This worked by accident in the old architecture but failed with the new direct-state model.

**Fix**: Added `AppendToWorkingPipeline()` helper method:
```csharp
MicroOps.InsertAfterCurrentOp(current, penaltyT5);
MicroOps.AppendToWorkingPipeline(current, penaltyT6);  // FIXED: Uses working pipeline
```

## Rollback

Not needed - refactoring was successful. `CpuStateBuffer` is kept for reference but no longer used.
