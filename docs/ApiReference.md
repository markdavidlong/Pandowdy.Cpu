# Pandowdy.Cpu API Reference

This document provides a comprehensive reference for the Pandowdy.Cpu library API.

## Table of Contents

- [Cpu Module](#cpu-module)
- [CpuState Class](#cpustate-class)
- [CpuStateBuffer Class](#cpustatebuffer-class)
- [IPandowdyCpuBus Interface](#ipandowdycpubus-interface)
- [Enumerations](#enumerations)

---

## Cpu Module

The `Cpu` module (defined in F#) provides the main execution engine for the CPU emulator.

### Methods

#### Clock

```csharp
bool Clock(CpuVariant variant, CpuStateBuffer buffer, IPandowdyCpuBus bus)
```

Executes a single CPU clock cycle.

**Parameters:**
- `variant` — The CPU variant to emulate (NMOS6502, WDC65C02, etc.)
- `buffer` — The double-buffered CPU state
- `bus` — The memory/IO bus interface

**Returns:** `true` if an instruction completed on this cycle; `false` otherwise.

**Remarks:**
- If the CPU is halted (Stopped, Jammed, or Waiting), returns `true` immediately without advancing.
- Each call represents one clock cycle of the 6502.
- Use this for cycle-accurate timing in systems that need per-cycle synchronization.

---

#### Step

```csharp
int Step(CpuVariant variant, CpuStateBuffer buffer, IPandowdyCpuBus bus)
```

Executes a single complete instruction.

**Parameters:**
- `variant` — The CPU variant to emulate
- `buffer` — The double-buffered CPU state
- `bus` — The memory/IO bus interface

**Returns:** The number of cycles consumed by the instruction.

**Remarks:**
- Calls `Clock()` repeatedly until an instruction completes.
- Includes a safety limit of 100 cycles to prevent infinite loops.
- Preferred method for debuggers and single-step execution.

---

#### Run

```csharp
int Run(CpuVariant variant, CpuStateBuffer buffer, IPandowdyCpuBus bus, int maxCycles)
```

Executes for a specified number of clock cycles.

**Parameters:**
- `variant` — The CPU variant to emulate
- `buffer` — The double-buffered CPU state
- `bus` — The memory/IO bus interface
- `maxCycles` — Maximum number of cycles to execute

**Returns:** The actual number of cycles executed (always equals `maxCycles`).

**Remarks:**
- Does not stop at instruction boundaries; may stop mid-instruction.
- Use for bulk execution when precise cycle counting is needed.

---

#### Reset

```csharp
void Reset(CpuStateBuffer buffer, IPandowdyCpuBus bus)
```

Performs a hardware reset of the CPU.

**Parameters:**
- `buffer` — The double-buffered CPU state
- `bus` — The memory/IO bus interface (for reading reset vector)

**Remarks:**
- Resets all registers to power-on state.
- Loads PC from the reset vector at $FFFC-$FFFD.
- Sets SP to $FD and P to $24 (U and I flags set).

---

#### CurrentOpcode

```csharp
byte CurrentOpcode(CpuStateBuffer buffer, IPandowdyCpuBus bus)
```

Gets the opcode of the currently executing instruction.

**Parameters:**
- `buffer` — The double-buffered CPU state
- `bus` — The memory/IO bus interface

**Returns:** The opcode byte at the current instruction's PC.

---

#### CyclesRemaining

```csharp
int CyclesRemaining(CpuStateBuffer buffer)
```

Gets the number of cycles remaining in the current instruction's pipeline.

**Parameters:**
- `buffer` — The double-buffered CPU state

**Returns:** The count of micro-ops yet to execute.

---

## CpuState Class

Represents the complete state of a 6502/65C02 CPU.

### Registers

| Property | Type | Description |
|----------|------|-------------|
| `A` | `byte` | Accumulator register |
| `X` | `byte` | X index register |
| `Y` | `byte` | Y index register |
| `SP` | `byte` | Stack pointer ($00-$FF, actual address is $0100+SP) |
| `PC` | `ushort` | Program counter (16-bit) |
| `P` | `byte` | Processor status register (flags) |

### Status Flag Properties

Convenience properties for accessing individual flags in the P register:

| Property | Flag | Bit | Description |
|----------|------|-----|-------------|
| `CarryFlag` | C | 0 | Set on carry/borrow from arithmetic |
| `ZeroFlag` | Z | 1 | Set when result is zero |
| `InterruptDisableFlag` | I | 2 | When set, IRQ is ignored |
| `DecimalFlag` | D | 3 | When set, ADC/SBC use BCD mode |
| `BreakFlag` | B | 4 | Set in P pushed by BRK instruction |
| `OverflowFlag` | V | 6 | Set on signed overflow |
| `NegativeFlag` | N | 7 | Set when result bit 7 is set |

### Status Flag Constants

| Constant | Value | Description |
|----------|-------|-------------|
| `FlagC` | 0x01 | Carry flag bit mask |
| `FlagZ` | 0x02 | Zero flag bit mask |
| `FlagI` | 0x04 | Interrupt disable bit mask |
| `FlagD` | 0x08 | Decimal mode bit mask |
| `FlagB` | 0x10 | Break flag bit mask |
| `FlagU` | 0x20 | Unused flag bit mask (always 1) |
| `FlagV` | 0x40 | Overflow flag bit mask |
| `FlagN` | 0x80 | Negative flag bit mask |

### Execution State Properties

| Property | Type | Description |
|----------|------|-------------|
| `Status` | `CpuStatus` | Current execution status (Running, Stopped, etc.) |
| `PendingInterrupt` | `PendingInterrupt` | Interrupt awaiting service |
| `InstructionComplete` | `bool` | True when current instruction has finished |
| `Pipeline` | `Action[]` | Array of micro-ops for current instruction |
| `PipelineIndex` | `int` | Index of next micro-op to execute |
| `IgnoreHaltStopWait` | `bool` | When true, JAM/STP/WAI are treated as NOPs |

### Temporary State Properties

| Property | Type | Description |
|----------|------|-------------|
| `TempAddress` | `ushort` | Temporary address during addressing mode computation |
| `TempValue` | `ushort` | Temporary data value during instruction execution |

### Methods

#### Reset

```csharp
void Reset()
```

Resets the CPU state to power-on defaults (A=X=Y=0, SP=$FD, P=$24, etc.).

---

#### CopyFrom

```csharp
void CopyFrom(CpuState other)
```

Copies all state from another CpuState instance.

---

#### GetFlag / SetFlag

```csharp
bool GetFlag(byte flag)
void SetFlag(byte flag, bool value)
```

Low-level methods to get/set individual status flags using bit masks.

---

### Interrupt Signal Methods

#### SignalIrq

```csharp
void SignalIrq()
```

Signals an IRQ (Interrupt Request). The interrupt will be serviced at the next instruction boundary if the I flag is clear.

---

#### SignalNmi

```csharp
void SignalNmi()
```

Signals an NMI (Non-Maskable Interrupt). Cannot be disabled by the I flag. Higher priority than IRQ.

---

#### SignalReset

```csharp
void SignalReset()
```

Signals a hardware Reset. Highest priority; reinitializes the CPU.

---

#### ClearIrq

```csharp
void ClearIrq()
```

Clears a pending IRQ signal. Used for level-triggered IRQ behavior.

---

#### HandlePendingInterrupt

```csharp
bool HandlePendingInterrupt(IPandowdyCpuBus bus)
```

Checks for and handles any pending interrupt.

**Parameters:**
- `bus` — The memory/IO bus interface (for reading vectors and pushing to stack)

**Returns:** `true` if an interrupt was handled; `false` if none pending or IRQ was masked.

**Remarks:**
- Should be called by the emulator host at instruction boundaries.
- Handles interrupts in priority order: Reset > NMI > IRQ.
- For IRQ, respects the I flag unless CPU is in Waiting state (WAI).
- Pushes PC and P to stack, loads PC from appropriate vector.

---

## CpuStateBuffer Class

Provides double-buffered CPU state for clean instruction boundaries and debugging.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Prev` | `CpuState` | Committed state at start of current instruction |
| `Current` | `CpuState` | Working state being modified during execution |

### Methods

#### PrepareNextCycle

```csharp
void PrepareNextCycle()
```

Copies Prev state to Current, preparing for the next instruction.

---

#### SwapIfComplete

```csharp
void SwapIfComplete()
```

If the current instruction is complete, atomically commits by swapping Prev and Current.

---

#### Reset

```csharp
void Reset()
```

Resets both Prev and Current to power-on defaults.

---

#### LoadResetVector

```csharp
void LoadResetVector(IPandowdyCpuBus bus)
```

Reads the reset vector from $FFFC-$FFFD and sets PC in both states.

---

### Debugger Helper Properties

These properties help detect what happened during an instruction by comparing Prev vs Current:

| Property | Type | Description |
|----------|------|-------------|
| `PcChanged` | `bool` | True if PC changed during the instruction |
| `JumpOccurred` | `bool` | True if a JMP-style instruction executed |
| `BranchOccurred` | `bool` | True if a branch was taken |
| `ReturnOccurred` | `bool` | True if RTS/RTI executed |
| `InterruptOccurred` | `bool` | True if an interrupt was triggered |
| `PageCrossed` | `bool` | True if addressing crossed a page boundary |
| `StackActivityOccurred` | `bool` | True if SP changed |
| `StackDelta` | `int` | Change in SP (negative=push, positive=pull) |
| `ChangedRegisters` | `IEnumerable<string>` | Names of registers that changed |

---

## IPandowdyCpuBus Interface

Interface for memory and I/O access.

### Methods

#### CpuRead

```csharp
byte CpuRead(ushort address)
```

Reads a byte from the specified address. Represents an actual CPU bus cycle.

---

#### Peek

```csharp
byte Peek(ushort address)
```

Reads a byte without triggering bus cycle tracking or side effects. Used internally for instruction decoding.

---

#### Write

```csharp
void Write(ushort address, byte value)
```

Writes a byte to the specified address.

---

## Enumerations

### CpuVariant

| Value | Description |
|-------|-------------|
| `NMOS6502` | Original NMOS 6502 with illegal/undocumented opcodes |
| `NMOS6502_NO_ILLEGAL` | NMOS 6502 with undefined opcodes as NOPs |
| `WDC65C02` | WDC 65C02 with new instructions (STZ, PHX, BRA, etc.) |
| `ROCKWELL65C02` | Rockwell 65C02 with bit manipulation (RMB, SMB, BBR, BBS) |

### CpuStatus

| Value | Description |
|-------|-------------|
| `Running` | Normal execution mode |
| `Stopped` | Halted by STP instruction (requires reset) |
| `Jammed` | Frozen by illegal JAM/KIL opcode (NMOS only) |
| `Waiting` | Suspended by WAI, waiting for interrupt |
| `Bypassed` | Halt instruction was bypassed (IgnoreHaltStopWait=true) |

### PendingInterrupt

| Value | Description |
|-------|-------------|
| `None` | No interrupt pending |
| `Irq` | IRQ pending (vector at $FFFE) |
| `Nmi` | NMI pending (vector at $FFFA) |
| `Reset` | Reset pending (vector at $FFFC) |

---

## Typical Usage Pattern

```csharp
// Setup
var bus = new MyBusImplementation();
var buffer = new CpuStateBuffer();
Cpu.Reset(buffer, bus);

// Main loop
while (running)
{
    // Execute one instruction
    int cycles = Cpu.Step(CpuVariant.WDC65C02, buffer, bus);
    
    // Check for pending interrupts (emulator host responsibility)
    buffer.Current.HandlePendingInterrupt(bus);
    
    // Update system timing
    systemCycles += cycles;
}
```

---

## Interrupt Handling Example

```csharp
// Signal an IRQ from hardware
buffer.Current.SignalIrq();

// At instruction boundary, handle it
if (buffer.Current.HandlePendingInterrupt(bus))
{
    // Interrupt was serviced, PC now at ISR
}

// For WAI instruction, CPU halts until interrupt
if (buffer.Current.Status == CpuStatus.Waiting)
{
    // CPU is waiting - signal interrupt to wake it
    buffer.Current.SignalIrq();
    buffer.Current.HandlePendingInterrupt(bus);
    // CPU is now Running and at ISR
}
```
