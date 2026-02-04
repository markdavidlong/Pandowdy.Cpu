# Documentation Update TODO

The following documentation files need updates to reflect the architecture refactoring from `CpuStateBuffer` to direct `cpu.State` access with `DebugCpu` for debugging.

There are implementation notes in ARCHITECTURAL_REFACTORING.md

## Completed Updates ✅

### README.md
- ✅ Updated features list (removed "Double-Buffered State", added "Direct State Access" and "Debugging Wrapper")
- ✅ Updated Quick Start example to use `cpu.State` instead of `cpuBuffer.Current`
- ✅ Added DebugCpu example
- ✅ Updated project structure (removed CpuStateBuffer reference)
- ✅ Updated documentation links

### CpuUsageGuide.md
- ✅ Updated Table of Contents - Replaced "Using CpuStateBuffer for Debugging" with "Using DebugCpu for Debugging"
- ✅ Updated Overview section - Changed `CpuStateBuffer` to `DebugCpu` with new description
- ✅ Replaced Architecture Diagram with new architecture showing CpuFactory injecting CpuState into CPU
- ✅ Updated Quick Start code example to use `CpuFactory.Create(variant)` and `cpu.State`
- ✅ Updated Working with CPU State section to use `cpu.State`
- ✅ Replaced "Using CpuStateBuffer for Debugging" section with "Using DebugCpu for Debugging"
- ✅ Updated all code examples throughout the file
- ✅ Updated API Reference section at bottom - Replaced CpuStateBuffer with DebugCpu

### ApiReference.md
- ✅ Updated Table of Contents - Replaced `CpuStateBuffer Class` with `DebugCpu Class`
- ✅ Updated IPandowdyCpu Interface - Added `State` property, removed `Buffer` property
- ✅ Updated CpuFactory Class - Added `CreateDebug` methods
- ✅ Replaced CpuStateBuffer Class section with DebugCpu documentation
- ✅ Updated usage examples

### MicroOps-Architecture.md
- ✅ Updated MicroOp Delegate section - Explained that `prev` and `current` now reference the same instance
- ✅ Updated Clock method example to use `_state` directly
- ✅ Updated Debugging Tips to reference DebugCpu instead of prev/current comparison

## Remaining Updates ⏳

### BUILDING.md
- ⏳ Update test count if different from documented value

## Key Changes Summary

| Old Pattern | New Pattern |
|-------------|-------------|
| `new CpuStateBuffer()` | Not needed |
| `CpuFactory.Create(variant, buffer)` | `CpuFactory.Create(variant)` |
| `cpuBuffer.Current.A` | `cpu.State.A` |
| `cpuBuffer.Prev` for debugging | `debugCpu.PrevState` |
| `cpuBuffer.PcChanged` | `debugCpu.PcChanged` |
| `cpuBuffer.BranchOccurred` | `debugCpu.BranchOccurred` |
| `cpuBuffer.ChangedRegisters` | `debugCpu.ChangedRegisters` |

## New DebugCpu Properties

| Property | Description |
|----------|-------------|
| `UnderlyingCpu` | The wrapped CPU instance |
| `PrevState` | Snapshot of state before the instruction |
| `PcChanged` | True if PC changed |
| `BranchOccurred` | True if a branch instruction was taken |
| `JumpOccurred` | True if a non-sequential jump occurred |
| `ReturnOccurred` | True if RTS/RTI executed |
| `InterruptOccurred` | True if interrupt handler entered |
| `PageCrossed` | True if page boundary crossed |
| `StackActivityOccurred` | True if SP changed |
| `StackDelta` | Change in SP |
| `ChangedRegisters` | Enumerable of register names that changed |
