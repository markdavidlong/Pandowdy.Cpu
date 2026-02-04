# Micro-Operations Architecture

This document explains the micro-operations (micro-ops) architecture used in the Pandowdy.Cpu 6502/65C02 emulator and how it achieves cycle-accurate instruction execution.

## Overview

The 6502 CPU emulator uses a **micro-operation pipeline architecture** to achieve cycle-accurate emulation. Each 6502 instruction is decomposed into a sequence of discrete micro-ops, where **each micro-op represents exactly one clock cycle** of CPU execution.

This approach provides:
- **Cycle-accurate timing**: Essential for accurate emulation of hardware that depends on precise timing
- **Clean separation of concerns**: Each micro-op performs a single, well-defined operation
- **Debuggability**: The pipeline can be inspected to see exactly what the CPU is doing each cycle
- **Extensibility**: New instructions or CPU variants can be added by composing existing micro-ops

## Core Components

### The MicroOp Delegate

```csharp
internal delegate void MicroOp(CpuState prev, CpuState current, IPandowdyCpuBus bus);
```

Each micro-op receives three parameters:

| Parameter | Purpose |
|-----------|---------|
| `prev` | A reference to the CPU state (same instance as `current` - historical artifact) |
| `current` | The CPU state being modified during execution |
| `bus` | The memory/IO bus interface for read/write operations |

> **Note:** The `prev` parameter exists for historical reasons. Both `prev` and `current` now reference the same `CpuState` instance owned directly by the CPU. For debugging scenarios where you need to compare state before and after an instruction, use `DebugCpu` which takes its own snapshot before each instruction.

### The Pipeline

Each instruction is defined as an array of micro-ops:

```csharp
private static readonly MicroOp[] Lda_Imm =
[
    MicroOps.FetchOpcode,                    // Cycle 1: Read opcode, increment PC
    (prev, current, bus) =>                  // Cycle 2: Read operand, load A, complete
    {
        MicroOps.FetchImmediate(prev, current, bus);
        MicroOps.LoadA(prev, current, bus);
        current.InstructionComplete = true;
    }
];
```

The first micro-op is **always** `FetchOpcode`, which reads the opcode byte and increments PC.

## Execution Model

### The Clock Method

The `Clock()` method executes exactly one micro-op (one clock cycle):

```csharp
public virtual bool Clock(IPandowdyCpuBus bus)
{
    // If pipeline is empty, start a new instruction
    if (_state.Pipeline.Length == 0 || _state.PipelineIndex >= _state.Pipeline.Length)
    {
        byte opcode = bus.Peek(_state.PC);
        _state.Pipeline = _pipelines[opcode];  // Look up pipeline for this opcode
        _state.PipelineIndex = 0;
        _state.InstructionComplete = false;
    }

    // Execute the current micro-op
    var microOp = _state.Pipeline[_state.PipelineIndex];
    microOp(_state, _state, bus);
    _state.PipelineIndex++;

    return _state.InstructionComplete;
}
```

### Instruction Completion

An instruction signals completion by setting `current.InstructionComplete = true`. This:
- Returns `true` from `Clock()` to indicate an instruction boundary
- Causes the next `Clock()` call to fetch a new opcode

## Micro-Op Categories

### Addressing Mode Micro-Ops

These handle reading operands and computing effective addresses:

| Micro-Op | Description | Bus Activity |
|----------|-------------|--------------|
| `FetchOpcode` | Read opcode at PC, increment PC | Read |
| `FetchImmediate` | Read immediate operand at PC, increment PC | Read |
| `FetchAddressLow` | Read low byte of address at PC, increment PC | Read |
| `FetchAddressHigh` | Read high byte of address at PC, increment PC | Read |
| `ReadFromTempAddress` | Read byte from computed address | Read |
| `WriteToTempAddress` | Write byte to computed address | Write |
| `ReadZeroPage` | Read from zero page (address masked to $00-$FF) | Read |
| `WriteZeroPage` | Write to zero page | Write |

### Index Register Operations

These add index registers to addresses:

| Micro-Op | Description | Bus Activity |
|----------|-------------|--------------|
| `AddX` | Add X to TempAddress (no bus access) | None |
| `AddY` | Add Y to TempAddress (no bus access) | None |
| `AddXZeroPage` | Add X with zero page wrap, dummy read | Read |
| `AddYZeroPage` | Add Y with zero page wrap, dummy read | Read |
| `AddXCheckPage` | Add X, insert penalty cycle if page crossed | Conditional |
| `AddYCheckPage` | Add Y, insert penalty cycle if page crossed | Conditional |
| `AddXWithDummyRead` | Add X with mandatory dummy read (RMW) | Read |

### ALU Micro-Ops

These perform arithmetic and logic operations:

| Micro-Op | Description |
|----------|-------------|
| `AdcBinary` | Add with carry (binary mode) |
| `AdcDecimalNmos` | Add with carry (NMOS decimal mode) |
| `AdcDecimalCmos` | Add with carry (CMOS decimal mode) |
| `SbcBinary` | Subtract with borrow (binary mode) |
| `AndOp` | Logical AND with accumulator |
| `OraOp` | Logical OR with accumulator |
| `EorOp` | Logical XOR with accumulator |
| `CmpOp` | Compare with accumulator |
| `BitOp` | Bit test (sets N, V, Z flags) |

### Shift/Rotate Micro-Ops

| Micro-Op | Description |
|----------|-------------|
| `AslA` | Arithmetic shift left accumulator |
| `AslMem` | Arithmetic shift left memory (via TempValue) |
| `LsrA` | Logical shift right accumulator |
| `LsrMem` | Logical shift right memory |
| `RolA` | Rotate left accumulator |
| `RolMem` | Rotate left memory |
| `RorA` | Rotate right accumulator |
| `RorMem` | Rotate right memory |

### Stack Micro-Ops

| Micro-Op | Description |
|----------|-------------|
| `PushA` | Push accumulator to stack |
| `PullA` | Pull accumulator from stack, set N/Z |
| `PushP` | Push processor status (B and U set) |
| `PullP` | Pull processor status |
| `PushPCH` | Push PC high byte |
| `PushPCL` | Push PC low byte |
| `PullPCL` | Pull PC low byte |
| `PullPCH` | Pull PC high byte |

### Branch Micro-Ops

The `BranchIf` factory creates conditional branch micro-ops:

```csharp
public static MicroOp BranchIf(Func<CpuState, bool> condition)
```

Branch timing:
- **2 cycles**: Branch not taken
- **3 cycles**: Branch taken, no page crossing
- **4 cycles**: Branch taken with page crossing

## Cycle Timing Examples

### LDA #$42 (Immediate) - 2 cycles

```
Cycle 1: FetchOpcode      - Read opcode $A9 from PC, PC++
Cycle 2: FetchImmediate   - Read $42 from PC, PC++, A = $42, set N/Z
```

### LDA $1234 (Absolute) - 4 cycles

```
Cycle 1: FetchOpcode      - Read opcode $AD from PC, PC++
Cycle 2: FetchAddressLow  - Read $34 from PC, PC++, TempAddress = $0034
Cycle 3: FetchAddressHigh - Read $12 from PC, PC++, TempAddress = $1234
Cycle 4: ReadFromTempAddress + LoadA - Read from $1234, load into A
```

### LDA $1234,X (Absolute,X) - 4 or 5 cycles

```
Cycle 1: FetchOpcode      - Read opcode $BD from PC, PC++
Cycle 2: FetchAddressLow  - Read $34 from PC, PC++
Cycle 3: FetchAddressHigh + AddXCheckPage - Read $12, compute $1234+X
         If page crossed: Insert penalty cycle
Cycle 4: (If page crossed) DummyRead at wrong address
Cycle 4/5: ReadFromTempAddress + LoadA - Read from final address
```

### STA $1234,X (Absolute,X Store) - 5 cycles (always)

Store instructions **always** take the extra cycle, even without page crossing:

```
Cycle 1: FetchOpcode
Cycle 2: FetchAddressLow
Cycle 3: FetchAddressHigh
Cycle 4: AddXWithDummyRead - Dummy read at (base & $FF00) | ((base+X) & $FF)
Cycle 5: WriteToTempAddress - Write to correct address
```

### INC $1234 (Read-Modify-Write) - 6 cycles

```
Cycle 1: FetchOpcode
Cycle 2: FetchAddressLow
Cycle 3: FetchAddressHigh
Cycle 4: ReadFromTempAddress - Read original value
Cycle 5: DummyWriteTempAddress - Write original value back (NMOS quirk)
Cycle 6: IncMem + WriteToTempAddress - Increment, write new value
```

## Dynamic Pipeline Modification

Some situations require modifying the pipeline at runtime:

### Page Crossing Penalty

```csharp
public static readonly MicroOp AddXCheckPage = (prev, current, bus) =>
{
    ushort baseAddr = current.TempAddress;
    current.TempAddress = (ushort)(current.TempAddress + current.X);
    
    if ((baseAddr >> 8) != (current.TempAddress >> 8))
    {
        // Page crossed - insert a dummy read cycle
        ushort wrongAddr = (ushort)((baseAddr & 0xFF00) | (current.TempAddress & 0x00FF));
        InsertAfterCurrentOp(current, (_, n, b) => b.CpuRead(wrongAddr));
    }
};
```

The `InsertAfterCurrentOp` helper adds a micro-op to execute on the next cycle.

### Branch Penalty Cycles

Branches can add 1 or 2 penalty cycles:

```csharp
if (condition(prev))
{
    ushort oldPC = current.PC;
    ushort newPC = (ushort)(current.PC + offset);
    current.PC = newPC;

    if ((oldPC >> 8) != (newPC >> 8))
    {
        // Page crossing: 2 penalty cycles
        InsertAfterCurrentOp(current, penaltyT2);
        current.Pipeline = [.. current.Pipeline, penaltyT3WithComplete];
    }
    else
    {
        // Same page: 1 penalty cycle
        InsertAfterCurrentOp(current, penaltyWithComplete);
    }
}
```

## CPU Variant Differences

The emulator supports multiple CPU variants:

| Variant | Description |
|---------|-------------|
| `Nmos6502` | Original NMOS 6502 with all illegal opcodes |
| `Nmos6502Simple` | NMOS 6502 without illegal opcode support |
| `Wdc65C02` | WDC 65C02 with new instructions |
| `Rockwell65C02` | Rockwell 65C02 with BBR/BBS/RMB/SMB |

Differences are handled by:
1. Different pipeline tables for each variant
2. Variant-specific micro-ops (e.g., `AdcDecimalNmos` vs `AdcDecimalCmos`)
3. Different page-crossing behavior (NMOS reads wrong address, CMOS re-reads operand)

## State Management

### TempAddress and TempValue

These registers hold intermediate values during instruction execution:

| Register | Purpose |
|----------|---------|
| `TempAddress` | Effective address being computed |
| `TempValue` | Temporary data (operand value, pointer address, etc.) |

### Helper Methods

```csharp
// Get/set the low byte of TempValue
public static byte TempByte(CpuState state) => (byte)state.TempValue;
public static void SetTempByte(CpuState state, byte value) => state.TempValue = value;

// Set N and Z flags based on a value
public static void SetNZ(CpuState state, byte value)
{
    state.ZeroFlag = value == 0;
    state.NegativeFlag = (value & 0x80) != 0;
}
```

## Adding New Instructions

To add a new instruction:

1. **Define micro-ops** if new behavior is needed
2. **Create a pipeline** combining existing micro-ops
3. **Register the pipeline** in the appropriate opcode table

Example for a hypothetical instruction:

```csharp
// Pipeline for hypothetical "XYZ abs" instruction
private static readonly MicroOp[] Xyz_Abs =
[
    MicroOps.FetchOpcode,           // Cycle 1
    MicroOps.FetchAddressLow,       // Cycle 2
    MicroOps.FetchAddressHigh,      // Cycle 3
    MicroOps.ReadFromTempAddress,   // Cycle 4
    (prev, current, bus) =>         // Cycle 5
    {
        // Custom operation here
        current.InstructionComplete = true;
    }
];
```

## Debugging Tips

1. **Inspect the pipeline**: `cpu.State.Pipeline` shows remaining micro-ops
2. **Check PipelineIndex**: Shows which micro-op is executing
3. **Use DebugCpu**: Wrap the CPU with `DebugCpu` to compare `PrevState` and `State` after each instruction
4. **Watch TempAddress/TempValue**: These show intermediate calculations
5. **Monitor bus activity**: Each micro-op typically performs 0 or 1 bus operations

## Summary

The micro-ops architecture provides:

- **Cycle accuracy** through one-micro-op-per-cycle execution
- **Composability** by combining simple micro-ops into complex instructions
- **Maintainability** with clear separation between addressing modes and operations
- **Variant support** through swappable pipeline tables and variant-specific micro-ops
- **Runtime flexibility** through dynamic pipeline modification for penalty cycles
