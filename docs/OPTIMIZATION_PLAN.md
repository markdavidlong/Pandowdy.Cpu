# CPU Emulator Performance Optimization Plan

## Overview

This document outlines the plan to optimize the 6502/65C02 CPU emulator from a delegate-based micro-op architecture to a switch-based interpreter. **Cycle accuracy is non-negotiable** - the emulator must execute exactly one bus cycle per `Clock()` call.

## Current State (Baseline)

- **Architecture**: Delegate-based micro-ops stored in `MicroOp[]` pipeline arrays
- **Benchmark**: **63.32 µs** for 10,000 clock cycles (zero allocations)
- **Real-world**: ~5 MHz in the emulator
- **vs Original**: +15.3% improvement from initial 74.75 µs
- **Target**: Match or exceed the "old CPU" performance (~6+ MHz)

## Constraint: Cycle Accuracy

The CPU must maintain cycle-accurate behavior:
- One `Clock()` call = one bus cycle
- Page-crossing penalty cycles must occur at correct times
- Dummy reads/writes must happen (hardware depends on them)
- No skipping or batching cycles

This constraint eliminates hybrid approaches that would skip the pipeline for "simple" instructions.

## Failed Approach: Hybrid Fast-Path

**Status**: ❌ Attempted and reverted

**What We Tried**:
- Add `TryExecuteFastPath(opcode)` for common simple instructions
- Execute inline switch code instead of delegates
- Track cycle state via `PipelineIndex`

**Why It Failed**:
1. Detecting "are we in fast-path mode?" on every `Clock()` call added more overhead than delegates
2. Benchmark program used `LDA abs,X` (not in fast-path), so most execution still used delegates
3. Caused memory allocations (122 KB) that weren't present before
4. Performance regression: 64 µs → 102 µs (-37%)

**Lesson**: Any hybrid approach that checks "which path" per cycle adds overhead. It's all-or-nothing.

## The Only Path Forward: Full Switch-Based Conversion

To eliminate delegate overhead while maintaining cycle accuracy, we must replace the entire delegate-based pipeline system with byte-based pipelines and switch execution.

### Architecture Comparison

| Component | Current (Delegates) | Target (Switch) |
|-----------|---------------------|-----------------|
| Micro-op type | `delegate void MicroOp(CpuState, CpuState, IPandowdyCpuBus)` | `enum MicroOpCode : byte` |
| Pipeline storage | `MicroOp[][] _pipelines` (256 arrays of delegates) | `byte[][] _pipelines` (256 arrays of bytes) |
| Working pipeline | `MicroOp[] WorkingPipeline` | `byte[] WorkingPipeline` |
| Execution | `microOp(prev, current, bus)` | `ExecuteMicroOp(opCode, prev, current, bus)` |
| Penalty insertion | `InsertAfterCurrentOp(MicroOp)` | `InsertAfterCurrentOp(MicroOpCode)` |

### Expected Performance

- **Delegate overhead**: ~5-10 ns per invocation
- **Cycles per instruction**: 2-7 average
- **10,000 cycles**: ~3,000 instructions × ~3 delegate calls = ~9,000 delegate invocations
- **Estimated savings**: 9,000 × 7 ns = ~63 µs overhead eliminated
- **Projected result**: ~50-54 µs (15-25% improvement)

## Implementation Plan

### Step 1: Create MicroOpCode Enum

**File**: `Pandowdy.Cpu/Pandowdy.Cpu/Internals/MicroOpCode.cs`

Create byte-backed enum with all micro-operation codes:

```csharp
internal enum MicroOpCode : byte
{
    // Fetch operations
    FetchOpcode = 0,
    FetchImmediate = 1,
    FetchAddressLow = 2,
    FetchAddressHigh = 3,

    // Memory operations
    ReadFromTempAddress = 10,
    WriteToTempAddress = 11,
    ReadZeroPage = 12,
    WriteZeroPage = 13,

    // Index operations
    AddX = 20,
    AddY = 21,
    AddXCheckPage = 22,
    AddYCheckPage = 23,
    AddXZeroPage = 24,
    AddYZeroPage = 25,
    AddXWithDummyRead = 26,
    AddYWithDummyRead = 27,

    // Load/Store completions
    LoadA = 30,
    LoadX = 31,
    LoadY = 32,
    StoreA = 33,
    StoreX = 34,
    StoreY = 35,

    // ALU operations
    AdcNmos = 40,
    AdcCmos = 41,
    SbcNmos = 42,
    SbcCmos = 43,
    And = 44,
    Or = 45,
    Eor = 46,

    // ... (continue for all ~80 micro-ops)

    // Instruction completion markers
    Complete = 250,
    CompleteWithNZ = 251,
}
```

### Step 2: Create Byte Pipeline Tables

**File**: `Pandowdy.Cpu/Pandowdy.Cpu/Internals/BytePipelines.cs`

Convert each `MicroOp[]` to `byte[]`:

```csharp
internal static class BytePipelines
{
    // LDA #imm (0xA9) - 2 cycles
    private static readonly byte[] Lda_Imm = 
    [
        (byte)MicroOpCode.FetchOpcode,
        (byte)MicroOpCode.FetchImmediateLoadA  // Combined fetch + load + complete
    ];

    // LDA zp (0xA5) - 3 cycles
    private static readonly byte[] Lda_Zp =
    [
        (byte)MicroOpCode.FetchOpcode,
        (byte)MicroOpCode.FetchAddressLow,
        (byte)MicroOpCode.ReadZeroPageLoadA  // Combined read + load + complete
    ];

    // ... (convert all ~256 opcode pipelines)
}
```

### Step 3: Update CpuState for Byte Pipelines

**File**: `Pandowdy.Cpu/Pandowdy.Cpu/CpuState.cs`

Add byte-based pipeline storage alongside or replacing delegate storage:

```csharp
// Replace delegate pipeline with byte pipeline
internal byte[] BytePipeline { get; set; } = [];
internal byte[] WorkingBytePipeline { get; } = new byte[WorkingPipelineCapacity];
```

### Step 4: Implement Switch-Based Executor

**File**: `Pandowdy.Cpu/Pandowdy.Cpu/CpuBase.cs`

Create the switch executor and update `Clock()`:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private void ExecuteMicroOp(MicroOpCode op, CpuState prev, CpuState current, IPandowdyCpuBus bus)
{
    switch (op)
    {
        case MicroOpCode.FetchOpcode:
            current.OpcodeAddress = current.PC;
            current.CurrentOpcode = bus.CpuRead(current.PC);
            current.TempValue = current.CurrentOpcode;
            current.PC++;
            break;

        case MicroOpCode.FetchImmediate:
            current.TempValue = bus.CpuRead(current.PC);
            current.PC++;
            break;

        case MicroOpCode.LoadA:
            current.A = (byte)current.TempValue;
            SetNZ(current, current.A);
            break;

        // ... (~80 cases)

        case MicroOpCode.Complete:
            current.InstructionComplete = true;
            break;
    }
}
```

### Step 5: Handle Penalty Cycle Insertion

The penalty cycle system (`InsertAfterCurrentOp`) must work with byte arrays:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
internal void InsertAfterCurrentOp(CpuState state, MicroOpCode op)
{
    int insertIdx = state.PipelineIndex + 1;

    if (state.WorkingPipelineLength == 0)
    {
        Array.Copy(state.BytePipeline, state.WorkingBytePipeline, state.BytePipeline.Length);
        state.WorkingPipelineLength = state.BytePipeline.Length;
    }

    // Shift and insert
    for (int i = state.WorkingPipelineLength - 1; i >= insertIdx; i--)
    {
        state.WorkingBytePipeline[i + 1] = state.WorkingBytePipeline[i];
    }

    state.WorkingBytePipeline[insertIdx] = (byte)op;
    state.WorkingPipelineLength++;
}
```

## Validation Strategy

### Automated Testing
- All **1,152 existing tests** must pass after conversion
- Run: `dotnet test Pandowdy.EmuCore.Tests\Pandowdy.EmuCore.Tests.csproj`

### Benchmark Validation
- Benchmark: `CpuClockBenchmark.Clock10000Cycles`
- Baseline: **63.32 µs**
- Target: **~50-54 µs** (15-25% improvement)
- Run: Use `run_benchmark` tool

### Manual Validation
- Run the actual emulator and verify MHz reading
- Current: ~5 MHz
- Target: ≥6 MHz

## Progress Tracking

| Phase | Status | Time | Allocations | Change |
|-------|--------|------|-------------|--------|
| Original | Baseline | 74.75 µs | 0 B | - |
| Initial optimizations | ✅ Complete | 63.32 µs | 0 B | +15.3% |
| Hybrid fast-path | ❌ Failed | 102 µs | 122 KB | -37% |
| Switch conversion | ❌ Failed | 74.53 µs | 0 B | -16% |
| **Final (Delegate-based)** | ✅ **Accepted** | **62.51 µs** | **0 B** | **+16.4%** |

## Final Decision: Accept Delegate-Based Architecture

**Date**: Optimization effort concluded

### Why Switch-Based Approach Failed

The switch-based executor (74.53 µs) was **slower** than delegates (62.51 µs) because:

1. **Property accessor overhead**: Switch cases still call `current.TempAddress`, `current.X`, etc. - these property getters added ~10% overhead visible in profiler.

2. **Method call overhead**: `MicroOpExecutor.Execute()` as a separate static method adds call overhead that negates switch benefits.

3. **JIT optimization**: .NET's JIT is highly efficient at delegate invocation for static readonly delegates. The delegates in `MicroOps.cs` are pre-allocated, so there's minimal dispatch overhead.

### Comparison with Legacy Emulator

The legacy `Emulator` project achieves higher throughput (~8-10 MHz) by:
- **Not being cycle-accurate**: Executes entire instruction in one call, counts cycles with a counter
- **Using unmanaged function pointers**: `delegate*<CPU, bool>` has lower overhead than managed delegates
- **Direct field access**: Public fields instead of properties

**Pandowdy.Cpu prioritizes correctness over speed**:
- Cycle-accurate execution (one `Clock()` = one bus cycle)
- Accurate dummy reads/writes
- Correct page-crossing penalty timing
- Mid-instruction interrupt sampling capability

### Final Performance

| Metric | Value |
|--------|-------|
| Benchmark | 62.51 µs / 10,000 cycles |
| Allocations | 0 bytes |
| Estimated throughput | ~5-6 MHz |
| Improvement from original | +16.4% |

### Lessons Learned

1. **Measure before optimizing**: The switch approach seemed theoretically faster but benchmarked slower.

2. **.NET delegate optimization**: Modern .NET JIT handles static readonly delegates very efficiently.

3. **Cycle accuracy has a cost**: The ~3-4x throughput difference vs the legacy emulator is the price of accuracy.

4. **Property access matters**: In hot paths, property getters add measurable overhead vs direct field access.

5. **Don't assume switches are faster**: Large switch statements have their own dispatch overhead.

## References

- `MicroOps.cs` - Micro-op implementations
- `Pipelines.cs` - Instruction pipeline definitions
- `CpuBase.cs` - Clock() implementation
- `CpuClockBenchmark.cs` - Performance benchmark

## Estimated Effort

| Task | Complexity | Est. Lines | Est. Time |
|------|------------|------------|-----------|
| Create `MicroOpCode` enum | Low | ~100 | 15 min |
| Create byte pipeline tables | High | ~2000 | 2-3 hours |
| Update `CpuState` | Low | ~20 | 10 min |
| Implement switch executor | High | ~400 | 1-2 hours |
| Update penalty cycle insertion | Medium | ~50 | 20 min |
| Testing & debugging | High | - | 1-2 hours |
| **Total** | | ~2,500 | **4-8 hours** |

## Rollback Strategy

If the conversion causes issues:
1. The work is in new files (`MicroOpCode.cs`, `BytePipelines.cs`)
2. Can keep delegate-based system as fallback
3. Tests provide comprehensive regression detection
4. Git provides full history for revert

## Files Summary

### New Files
- `Pandowdy.Cpu/Pandowdy.Cpu/Internals/MicroOpCode.cs` - Enum definition
- `Pandowdy.Cpu/Pandowdy.Cpu/Internals/BytePipelines.cs` - Byte-based pipeline tables

### Modified Files
- `Pandowdy.Cpu/Pandowdy.Cpu/CpuBase.cs` - Switch executor, updated Clock()
- `Pandowdy.Cpu/Pandowdy.Cpu/CpuState.cs` - Byte pipeline storage
- `Pandowdy.Cpu/Pandowdy.Cpu/CpuStateBuffer.cs` - Handle byte pipeline reset

## Decision Required

This is a significant undertaking (4-8 hours). Alternatives:

1. **Proceed with conversion** - Potential 15-25% improvement
2. **Accept current performance** - 63 µs / ~5 MHz is already 15% better than original
3. **Explore other bottlenecks** - Profile full emulator for non-CPU issues

## References

- `MicroOps.cs` - Current micro-op implementations (reference for switch cases)
- `Pipelines.cs` - Current pipeline definitions (reference for byte arrays)
- `CpuBase.cs` - Current Clock() implementation
- `CpuClockBenchmark.cs` - Performance benchmark
