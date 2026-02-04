# Pandowdy.Cpu API Reference

This document provides a comprehensive reference for the Pandowdy.Cpu library API.

## Table of Contents

- [IPandowdyCpu Interface](#ipandowdycpu-interface)
- [CpuFactory Class](#cpufactory-class)
- [CPU Classes](#cpu-classes)
- [CpuState Class](#cpustate-class)
- [DebugCpu Class](#debugcpu-class)
- [IPandowdyCpuBus Interface](#ipandowdycpubus-interface)
- [Enumerations](#enumerations)

---

## IPandowdyCpu Interface

The `IPandowdyCpu` interface defines the contract for all CPU implementations.

### Execution Methods

#### Clock

```csharp
bool Clock(IPandowdyCpuBus bus)
```

Executes a single CPU clock cycle.

**Parameters:**
- `bus` — The memory/IO bus interface

**Returns:** `true` if an instruction completed on this cycle; `false` otherwise.

**Remarks:**
- If the CPU is halted (Stopped, Jammed, or Waiting), returns `true` immediately without advancing.
- Each call represents one clock cycle of the 6502.
- Use this for cycle-accurate timing in systems that need per-cycle synchronization.

---

#### Step

```csharp
int Step(IPandowdyCpuBus bus)
```

Executes a single complete instruction.

**Parameters:**
- `bus` — The memory/IO bus interface

**Returns:** The number of cycles consumed by the instruction.

**Remarks:**
- Calls `Clock()` repeatedly until an instruction completes.
- Includes a safety limit of 100 cycles to prevent infinite loops.
- Preferred method for debuggers and single-step execution.

---

#### Run

```csharp
int Run(IPandowdyCpuBus bus, int maxCycles)
```

Executes for a specified number of clock cycles.

**Parameters:**
- `bus` — The memory/IO bus interface
- `maxCycles` — Maximum number of cycles to execute

**Returns:** The actual number of cycles executed (always equals `maxCycles`).

**Remarks:**
- Does not stop at instruction boundaries; may stop mid-instruction.
- Use for bulk execution when precise cycle counting is needed.

---

#### Reset

```csharp
void Reset(IPandowdyCpuBus bus)
```

Performs a hardware reset of the CPU.

**Parameters:**
- `bus` — The memory/IO bus interface (for reading reset vector)

**Remarks:**
- Resets all registers to power-on state.
- Loads PC from the reset vector at $FFFC-$FFFD.
- Sets SP to $FD and P to $24 (U and I flags set).

---

### Interrupt Methods

#### SignalIrq

```csharp
void SignalIrq()
```

Signals an IRQ (Interrupt Request).

**Remarks:**
- The IRQ will be serviced at the next instruction boundary if the I flag is clear.
- If the I flag is set, the IRQ remains pending until the flag is cleared.
- IRQ has the lowest priority; if an NMI or Reset is already pending, the IRQ is ignored.

---

#### SignalNmi

```csharp
void SignalNmi()
```

Signals an NMI (Non-Maskable Interrupt).

**Remarks:**
- The NMI will be serviced at the next instruction boundary.
- NMI cannot be disabled by the I flag.
- NMI has higher priority than IRQ but lower than Reset.

---

#### SignalReset

```csharp
void SignalReset()
```

Signals a hardware Reset.

**Remarks:**
- Reset has the highest priority and will always be serviced at the next instruction boundary.
- All other pending interrupts are superseded.

---

#### ClearIrq

```csharp
void ClearIrq()
```

Clears a pending IRQ signal.

**Remarks:**
- Use for level-triggered IRQ behavior.
- Call when the IRQ line goes high (inactive) to clear the pending interrupt before it is serviced.
- Only clears the pending interrupt if it is an IRQ; NMI and Reset signals are not affected.

---

#### HandlePendingInterrupt

```csharp
bool HandlePendingInterrupt(IPandowdyCpuBus bus)
```

Checks for and handles any pending interrupt.

**Parameters:**
- `bus` — The memory/IO bus interface

**Returns:** `true` if an interrupt was handled; `false` if no interrupt was pending or IRQ was masked.

**Remarks:**
- Should be called at instruction boundaries.
- Checks for pending interrupts in priority order: Reset > NMI > IRQ.
- For IRQ, the interrupt is only serviced if the I flag is clear, unless the CPU is in Waiting state.
- D-flag clearing behavior depends on CPU variant:
  - NMOS 6502: D flag is NOT cleared on IRQ/NMI
  - 65C02: D flag IS cleared on IRQ/NMI

---

### Properties

#### Variant

```csharp
CpuVariant Variant { get; }
```

Gets the CPU variant this instance emulates.

---

#### State

```csharp
CpuState State { get; set; }
```

Gets or sets the CPU state.

**Remarks:**
- The state is provided to the CPU via dependency injection through `CpuFactory.Create(variant, state)`.
- The state is settable to allow replacing the entire state at runtime.
- Useful for scenarios like save/restore, testing, state sharing between components, or debugging.

---

## CpuFactory Class

Factory for creating CPU instances by variant.

### Create

```csharp
static IPandowdyCpu Create(CpuVariant variant, CpuState state)
```

Creates a CPU instance for the specified variant with the provided state.

**Parameters:**
- `variant` — The CPU variant to emulate
- `state` — The CPU state to inject into the CPU

**Returns:** An `IPandowdyCpu` instance for the specified variant.

**Remarks:**
- The `state` parameter is required; the caller must provide a `CpuState` instance.
- The provided state is injected into the CPU via dependency injection.
- The state may be shared with other components or pre-configured before injection.

**Example:**
```csharp
var state = new CpuState();
var cpu = CpuFactory.Create(CpuVariant.Wdc65C02, state);
cpu.Reset(bus);
Console.WriteLine($"A = ${cpu.State.A:X2}");

// Pre-configure state before injection:
state.A = 0x42;
var cpu2 = CpuFactory.Create(CpuVariant.Wdc65C02, state);
```

---

### CreateDebug

```csharp
static DebugCpu CreateDebug(CpuVariant variant, CpuState state)
```

Creates a debugging CPU wrapper for the specified variant with the provided state.

**Parameters:**
- `variant` — The CPU variant to emulate
- `state` — The CPU state to inject into the CPU

**Returns:** A `DebugCpu` wrapping a CPU instance for the specified variant.

```csharp
static DebugCpu CreateDebug(IPandowdyCpu cpu)
```

Wraps an existing CPU instance in a debugging wrapper.

**Example:**
```csharp
var state = new CpuState();
var debugCpu = CpuFactory.CreateDebug(CpuVariant.Wdc65C02, state);
debugCpu.Reset(bus);
debugCpu.Step(bus);
if (debugCpu.BranchOccurred)
    Console.WriteLine("Branch was taken!");
```

---

## CPU Classes

Concrete CPU implementations, all implementing `IPandowdyCpu`:

| Class | Variant | Description |
|-------|---------|-------------|
| `Cpu6502` | `Nmos6502` | NMOS 6502 with undocumented/illegal opcodes |
| `Cpu6502Simple` | `Nmos6502Simple` | NMOS 6502 with illegal opcodes treated as NOPs |
| `Cpu65C02` | `Wdc65C02` | WDC 65C02 with all CMOS instructions |
| `Cpu65C02Rockwell` | `Rockwell65C02` | Rockwell 65C02 (WAI/STP are NOPs) |

**D-Flag Behavior on Interrupts:**
- `Cpu6502`, `Cpu6502Simple`: D flag is NOT cleared on IRQ/NMI/BRK
- `Cpu65C02`, `Cpu65C02Rockwell`: D flag IS cleared on IRQ/NMI/BRK

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
| `CyclesRemaining` | `int` | Computed: remaining cycles in current instruction |
| `CurrentOpcode` | `byte` | The opcode byte currently being executed |
| `OpcodeAddress` | `ushort` | The address from which the opcode was read |
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

**Remarks:**
- This is a self-contained state reset with no bus interaction.
- Use `cpu.Reset(bus)` to also load the reset vector.

---

#### Clone

```csharp
CpuState Clone()
```

Creates a deep copy of this CPU state.

**Returns:** A new `CpuState` instance with all values copied.

**Remarks:**
- Use for save states or when you need an independent copy.
- For hot-path updates where you want to avoid allocation, use `CopyFrom()` instead.

---

#### CopyFrom

```csharp
void CopyFrom(CpuState other)
```

Copies all state from another `CpuState` instance (no allocation).

**Remarks:**
- Use this in hot paths to minimize GC pressure.
- More efficient than `Clone()` when you already have a target instance.

---

#### GetFlag / SetFlag

```csharp
bool GetFlag(byte flag)
void SetFlag(byte flag, bool value)
```

Low-level methods to get/set individual status flags using bit masks.

---

## DebugCpu Class

A debugging wrapper around a CPU that tracks state changes between instructions.

### Overview

`DebugCpu` decorates any `IPandowdyCpu` implementation to provide debugging capabilities without impacting the performance of production code. It implements `IPandowdyCpu` and can be used anywhere a regular CPU instance is used.

**Key Features:**
- Automatically snapshots state before each instruction
- Provides comparison helpers (PcChanged, BranchOccurred, etc.)
- Tracks which registers changed during each instruction

### Constructor

```csharp
public DebugCpu(IPandowdyCpu cpu)
```

Creates a debugging wrapper around the specified CPU.

**Parameters:**
- `cpu` — The underlying CPU to wrap

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `UnderlyingCpu` | `IPandowdyCpu` | The wrapped CPU instance |
| `State` | `CpuState` | Current CPU state (delegates to underlying CPU) |
| `PrevState` | `CpuState?` | State snapshot before the last completed instruction |
| `Variant` | `CpuVariant` | The CPU variant (delegates to underlying CPU) |

### Debugger Helper Properties

These properties compare `PrevState` vs `State` to detect what happened during the last instruction:

| Property | Type | Description |
|----------|------|-------------|
| `PcChanged` | `bool` | True if PC changed during the instruction |
| `JumpOccurred` | `bool` | True if a JMP-style instruction executed |
| `BranchOccurred` | `bool` | True if a branch instruction was taken |
| `ReturnOccurred` | `bool` | True if RTS/RTI executed |
| `InterruptOccurred` | `bool` | True if an interrupt was triggered |
| `PageCrossed` | `bool` | True if addressing crossed a page boundary |
| `StackActivityOccurred` | `bool` | True if SP changed |
| `StackDelta` | `int` | Change in SP (negative=push, positive=pull) |
| `ChangedRegisters` | `IEnumerable<string>` | Names of registers that changed |

### Methods

All execution and interrupt methods delegate to the underlying CPU:

- `Clock(IPandowdyCpuBus bus)` — Execute one cycle, with state tracking
- `Step(IPandowdyCpuBus bus)` — Execute one instruction, snapshots state first
- `Run(IPandowdyCpuBus bus, int maxCycles)` — Execute cycles (delegates directly)
- `Reset(IPandowdyCpuBus bus)` — Reset CPU, clears PrevState
- `SignalIrq()` / `SignalNmi()` / `SignalReset()` — Signal interrupts
- `ClearIrq()` — Clear pending IRQ
- `HandlePendingInterrupt(IPandowdyCpuBus bus)` — Handle pending interrupt

### Usage Example

```csharp
var debugCpu = CpuFactory.CreateDebug(CpuVariant.Wdc65C02);
debugCpu.Reset(bus);

while (debugCpu.State.Status == CpuStatus.Running)
{
    debugCpu.Step(bus);
    
    if (debugCpu.BranchOccurred)
        Console.WriteLine("Branch was taken!");
    
    foreach (var reg in debugCpu.ChangedRegisters)
        Console.WriteLine($"  {reg} changed");
}
```

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
| `Nmos6502` | Original NMOS 6502 with illegal/undocumented opcodes |
| `Nmos6502Simple` | NMOS 6502 with undefined opcodes as NOPs |
| `Wdc65C02` | Later WDC 65C02 (W65C02S) with all CMOS instructions including RMB/SMB/BBR/BBS |
| `Rockwell65C02` | Rockwell 65C02 with bit manipulation (same as WDC but WAI/STP are NOPs) |

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
var cpu = CpuFactory.Create(CpuVariant.Wdc65C02);
cpu.Reset(bus);

// Main loop
while (running)
{
    // Execute one instruction
    int cycles = cpu.Step(bus);

    // Check for pending interrupts (emulator host responsibility)
    cpu.HandlePendingInterrupt(bus);

    // Update system timing
    systemCycles += cycles;
}
```

---

## Interrupt Handling Example

```csharp
// Create CPU
var cpu = CpuFactory.Create(CpuVariant.Wdc65C02);

// Signal an IRQ from hardware
cpu.SignalIrq();

// At instruction boundary, handle it
if (cpu.HandlePendingInterrupt(bus))
{
    // Interrupt was serviced, PC now at ISR
}

// For WAI instruction, CPU halts until interrupt
if (cpu.State.Status == CpuStatus.Waiting)
{
    // CPU is waiting - signal interrupt to wake it
    cpu.SignalIrq();
    cpu.HandlePendingInterrupt(bus);
    // CPU is now Running and at ISR
}
```
