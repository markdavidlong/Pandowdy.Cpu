# Pandowdy 6502/65C02 CPU Emulator Usage Guide

This guide demonstrates how to use the `IPandowdyCpu`, `CpuFactory`, `CpuState`, and `DebugCpu` classes to emulate 6502-family processors.

## Table of Contents

- [Overview](#overview)
- [Quick Start](#quick-start)
- [CPU Variants](#cpu-variants)
- [Validation](#validation)
- [Setting Up the Bus](#setting-up-the-bus)
- [CPU Execution](#cpu-execution)
- [Working with CPU State](#working-with-cpu-state)
- [Using DebugCpu for Debugging](#using-debugcpu-for-debugging)
- [Interrupt Handling](#interrupt-handling)
- [Halt Instructions and IgnoreHaltStopWait](#halt-instructions-and-ignorehaltstopwait)
- [Complete Example](#complete-example)

---

## Overview

The CPU emulator uses a micro-op pipeline architecture for cycle-accurate emulation. The key components are:

| Component | Description |
|-----------|-------------|
| `IPandowdyCpu` | Interface for CPU instances with `Clock`, `Step`, `Run`, and `Reset` methods |
| `CpuFactory` | Factory for creating CPU instances by variant |
| `CpuState` | Complete CPU state including registers, flags, and execution status |
| `DebugCpu` | Debugging wrapper that tracks state changes between instructions |
| `IPandowdyCpuBus` | Interface for memory read/write operations |
| `CpuVariant` | Enum specifying which CPU variant to emulate |

### Architecture Diagram

```
┌─────────────────────────────────────────────────────────┐
│                      CpuFactory                         │
│  - Receives CpuState and injects it into the CPU        │
│  - Create(variant) / Create(variant, state)             │
│  - CreateDebug(variant) for debugging scenarios         │
└───────────────────────────┬─────────────────────────────┘
                            │ injects
                            ▼
┌─────────────────────────────────────────────────────────┐
│                    IPandowdyCpu                         │
│  ┌─────────────────────────────────────────────────┐    │
│  │            CpuState (injected)                  │    │
│  │                                                 │    │
│  │   A, X, Y, SP, PC, P (flags), Status            │    │
│  │   CurrentOpcode, OpcodeAddress, Pipeline        │    │
│  │                                                 │    │
│  └─────────────────────────────────────────────────┘    │
│                                                         │
│  cpu.Clock(), cpu.Step(), cpu.Run(), cpu.Reset()        │
└───────────────────────────┬─────────────────────────────┘
                            │
                            ▼
                  ┌─────────────────┐
                  │ IPandowdyCpuBus │
                  │  (Memory I/O)   │
                  └─────────────────┘


┌─────────────────────────────────────────────────────────┐
│                    DebugCpu (optional)                  │
│                                                         │
│  Wraps IPandowdyCpu to provide debugging features:      │
│  - PrevState: Owned snapshot before each instruction    │
│  - State: Delegates to wrapped CPU's injected state     │
│  - BranchOccurred, JumpOccurred, ReturnOccurred         │
│  - ChangedRegisters, StackDelta, etc.                   │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

The `CpuState` must be provided to `CpuFactory.Create()` and is injected into the CPU. Access the state via `cpu.State`. For debugging scenarios where you need to compare state before and after an instruction, use `DebugCpu` which maintains its own `PrevState` snapshot while delegating `State` to the wrapped CPU.

---

## Quick Start

```csharp
using Pandowdy.Cpu;

// 1. Create a memory bus implementation
var bus = new RamBus();

// 2. Load your program into memory
byte[] program = { 0xA9, 0x42, 0x8D, 0x00, 0x02 }; // LDA #$42, STA $0200
bus.LoadProgram(0x0400, program);
bus.SetResetVector(0x0400);

// 3. Create the CPU state and instance
var state = new CpuState();
var cpu = CpuFactory.Create(CpuVariant.Wdc65C02, state);

// 4. Reset the CPU (loads PC from reset vector)
cpu.Reset(bus);

// 5. Execute instructions
int cycles = cpu.Step(bus);
Console.WriteLine($"Instruction took {cycles} cycles");
Console.WriteLine($"A = ${cpu.State.A:X2}");
```

---

## CPU Variants

The emulator supports four CPU variants:

```csharp
public enum CpuVariant
{
    Nmos6502,        // Original NMOS 6502 with illegal opcodes
    Nmos6502Simple,  // NMOS 6502, undefined opcodes treated as NOPs
    Wdc65C02,        // Later WDC 65C02 with all CMOS instructions including RMB/SMB/BBR/BBS
    Rockwell65C02    // Rockwell 65C02 (same as WDC but NOPs for WAI/STP)
}
```

### Variant Differences

| Feature | Nmos6502 | Nmos6502Simple | Wdc65C02 | Rockwell65C02 |
|---------|----------|----------------|----------|---------------|
| Illegal opcodes | ✅ | ❌ (NOPs) | ❌ | ❌ |
| JMP ($xxFF) bug | ✅ | ✅ | ❌ (fixed) | ❌ (fixed) |
| STZ, PHX, PHY, PLX, PLY | ❌ | ❌ | ✅ | ✅ |
| INC A, DEC A | ❌ | ❌ | ✅ | ✅ |
| BRA (branch always) | ❌ | ❌ | ✅ | ✅ |
| TRB, TSB | ❌ | ❌ | ✅ | ✅ |
| STP, WAI | ❌ | ❌ | ✅ | ❌ (NOPs) |
| RMB, SMB, BBR, BBS | ❌ | ❌ | ✅ | ✅ |
| JAM/KIL opcodes | ✅ (freezes) | ❌ | ❌ | ❌ |
| D flag cleared on IRQ/NMI | ❌ | ❌ | ✅ | ✅ |

> **Note:** The `Wdc65C02` variant models later WDC 65C02 chips (W65C02S) which include
> the Rockwell bit manipulation instructions (RMB, SMB, BBR, BBS). The `Rockwell65C02`
> variant is functionally identical except that WAI and STP are treated as NOPs since
> original Rockwell chips did not implement these instructions.

---

## Validation

All CPU variants pass [Klaus Dormann's 6502/65C02 Functional Tests](https://github.com/Klaus2m5/6502_65C02_functional_tests), a standard test suite for 6502 emulator validation.

| Test | Description | Variants |
|------|-------------|----------|
| **6502 Functional Test** | Comprehensive test of all documented 6502 instructions | All |
| **6502 Decimal Test** | BCD arithmetic validation | All |
| **6502 Interrupt Test** | IRQ/NMI interrupt handling | All |
| **65C02 Extended Opcodes Test (WDC)** | 65C02-specific instructions | Wdc65C02 |
| **65C02 Extended Opcodes Test (Rockwell)** | Includes RMB/SMB/BBR/BBS | Rockwell65C02 |

---

## Setting Up the Bus

Implement the `IPandowdyCpuBus` interface to connect the CPU to your memory system:

```csharp
public interface IPandowdyCpuBus
{
    byte CpuRead(ushort address);
    byte Peek(ushort address);  // Read without bus cycle tracking
    void Write(ushort address, byte value);
}
```

### Simple RAM Implementation

```csharp
public class RamBus : IPandowdyCpuBus
{
    public byte[] Memory { get; } = new byte[65536];

    public byte CpuRead(ushort address) => Memory[address];

    // For simple RAM, Peek is the same as CpuRead.
    // For cycle-accurate emulation, Peek should not trigger I/O or cycle tracking.
    public byte Peek(ushort address) => Memory[address];

    public void Write(ushort address, byte value) => Memory[address] = value;

    public void SetResetVector(ushort address)
    {
        Memory[0xFFFC] = (byte)(address & 0xFF);
        Memory[0xFFFD] = (byte)(address >> 8);
    }

    public void SetIrqVector(ushort address)
    {
        Memory[0xFFFE] = (byte)(address & 0xFF);
        Memory[0xFFFF] = (byte)(address >> 8);
    }

    public void SetNmiVector(ushort address)
    {
        Memory[0xFFFA] = (byte)(address & 0xFF);
        Memory[0xFFFB] = (byte)(address >> 8);
    }

    public void LoadProgram(ushort startAddress, byte[] program)
    {
        Array.Copy(program, 0, Memory, startAddress, program.Length);
    }
}
```

### Memory-Mapped I/O Example

```csharp
public class SystemBus : IPandowdyCpuBus
{
    private readonly byte[] _ram = new byte[0x8000];  // 32KB RAM
    private readonly byte[] _rom = new byte[0x8000];  // 32KB ROM
    private byte _ioPortA;
    private byte _ioPortB;

    public byte CpuRead(ushort address)
    {
        return address switch
        {
            < 0x8000 => _ram[address],                    // RAM: $0000-$7FFF
            0xD000 => _ioPortA,                           // I/O Port A
            0xD001 => _ioPortB,                           // I/O Port B
            >= 0x8000 => _rom[address - 0x8000]           // ROM: $8000-$FFFF
        };
    }

    public void Write(ushort address, byte value)
    {
        switch (address)
        {
            case < 0x8000:
                _ram[address] = value;
                break;
            case 0xD000:
                _ioPortA = value;
                OnPortAWrite(value);  // Trigger side effects
                break;
            case 0xD001:
                _ioPortB = value;
                break;
            // ROM writes are ignored
        }
    }

    private void OnPortAWrite(byte value)
    {
        Console.WriteLine($"Output to Port A: ${value:X2}");
    }
}
```

---

## CPU Execution

### Single Cycle: `cpu.Clock()`

Executes one CPU clock cycle. Returns `true` when an instruction completes.

```csharp
var cpu = CpuFactory.Create(CpuVariant.Wdc65C02);

bool instructionComplete = cpu.Clock(bus);

if (instructionComplete)
{
    Console.WriteLine("Instruction finished!");
}
```

### Single Instruction: `cpu.Step()`

Executes cycles until one instruction completes. Returns the cycle count.

```csharp
int cycles = cpu.Step(bus);
Console.WriteLine($"Instruction took {cycles} cycles");
```

### Multiple Cycles: `cpu.Run()`

Executes a specified number of cycles. Useful for running at a target frequency.

```csharp
// Run for approximately 1MHz (1,000,000 cycles per second)
// At 60 FPS, that's ~16,667 cycles per frame
int cyclesPerFrame = 16667;
int actualCycles = cpu.Run(bus, cyclesPerFrame);
```

### Reset: `cpu.Reset()`

Initializes the CPU and loads PC from the reset vector.

```csharp
cpu.Reset(bus);
// PC is now loaded from $FFFC-$FFFD
```

### Accessing Opcode Information

```csharp
// Get the opcode of the current instruction (set during fetch)
byte opcode = cpu.State.CurrentOpcode;

// Get the address where the opcode was fetched from
ushort opcodeAddress = cpu.State.OpcodeAddress;

// Get remaining cycles in current instruction pipeline
int remaining = cpu.State.CyclesRemaining;
```

---

## Working with CPU State

### Accessing Registers

```csharp
CpuState state = cpu.State;

// Read registers
byte accumulator = state.A;
byte xIndex = state.X;
byte yIndex = state.Y;
byte stackPointer = state.SP;
ushort programCounter = state.PC;
byte processorStatus = state.P;

// Write registers (useful for test setup)
state.A = 0x42;
state.X = 0x10;
state.Y = 0x20;
state.SP = 0xFF;
state.PC = 0x0400;
```

### Working with Status Flags

```csharp
// Using convenience properties
bool carry = state.CarryFlag;
bool zero = state.ZeroFlag;
bool interrupt = state.InterruptDisableFlag;
bool decimal = state.DecimalFlag;
bool overflow = state.OverflowFlag;
bool negative = state.NegativeFlag;

// Setting flags
state.CarryFlag = true;
state.DecimalFlag = false;

// Using bit masks directly
bool isNegative = state.GetFlag(CpuState.FlagN);
state.SetFlag(CpuState.FlagC, true);

// Flag bit layout in P register:
// Bit 7: N - Negative
// Bit 6: V - Overflow
// Bit 5: U - Unused (always 1)
// Bit 4: B - Break
// Bit 3: D - Decimal
// Bit 2: I - Interrupt Disable
// Bit 1: Z - Zero
// Bit 0: C - Carry
```

### CPU Status

```csharp
CpuStatus status = state.Status;

switch (status)
{
    case CpuStatus.Running:
        Console.WriteLine("CPU is executing normally");
        break;
    case CpuStatus.Stopped:
        Console.WriteLine("CPU halted by STP instruction (requires reset)");
        break;
        case CpuStatus.Jammed:
            Console.WriteLine("CPU frozen by illegal JAM opcode (requires reset)");
            break;
        case CpuStatus.Waiting:
            Console.WriteLine("CPU suspended by WAI, waiting for interrupt");
            break;
        case CpuStatus.Bypassed:
            Console.WriteLine("A halt instruction was bypassed (IgnoreHaltStopWait is true)");
            break;
    }
    ```

    ---

    ## Using DebugCpu for Debugging

    The `DebugCpu` wrapper provides powerful debugging capabilities by automatically tracking state changes between instructions.

    ### Creating a Debug CPU

        ```csharp
        // Create a debug CPU with state
        var state = new CpuState();
        var debugCpu = CpuFactory.CreateDebug(CpuVariant.Wdc65C02, state);

        // Or wrap an existing CPU
        var cpu = CpuFactory.Create(CpuVariant.Wdc65C02, state);
        var debugCpu = CpuFactory.CreateDebug(cpu);
        ```

        ### Comparing State Changes

        ```csharp
        // Execute one instruction
        debugCpu.Step(bus);

        // Compare before and after
        CpuState before = debugCpu.PrevState!;
        CpuState after = debugCpu.State;

        if (before.A != after.A)
        {
            Console.WriteLine($"A changed: ${before.A:X2} -> ${after.A:X2}");
        }

    // Use the built-in helper
    foreach (string reg in debugCpu.ChangedRegisters)
    {
        Console.WriteLine($"Register {reg} was modified");
    }
    ```

    ### Detecting Instruction Types

    ```csharp
    debugCpu.Step(bus);

    if (debugCpu.JumpOccurred)
    {
        Console.WriteLine($"Jump to ${debugCpu.State.PC:X4}");
    }

    if (debugCpu.BranchOccurred)
    {
        Console.WriteLine("Branch was taken");
    }

    if (debugCpu.ReturnOccurred)
    {
        Console.WriteLine("Returned from subroutine");
    }

    if (debugCpu.InterruptOccurred)
    {
        Console.WriteLine("Interrupt was serviced");
    }

    if (debugCpu.PageCrossed)
    {
        Console.WriteLine("Page boundary crossed (possible extra cycle)");
    }
    ```

    ### Stack Monitoring

    ```csharp
    if (debugCpu.StackActivityOccurred)
    {
        int delta = debugCpu.StackDelta;
        if (delta < 0)
        {
            Console.WriteLine($"Pushed {-delta} byte(s) to stack");
        }
        else
        {
            Console.WriteLine($"Pulled {delta} byte(s) from stack");
        }
    }
    ```

    ### Building a Simple Debugger

    ```csharp
    public class SimpleDebugger
    {
        private readonly DebugCpu _debugCpu;
        private readonly IPandowdyCpuBus _bus;
        private readonly HashSet<ushort> _breakpoints = new();

        public SimpleDebugger(CpuVariant variant, IPandowdyCpuBus bus)
        {
            _bus = bus;
            _debugCpu = CpuFactory.CreateDebug(variant);
        }

        public void AddBreakpoint(ushort address) => _breakpoints.Add(address);
        public void RemoveBreakpoint(ushort address) => _breakpoints.Remove(address);

        public void Step()
        {
            int cycles = _debugCpu.Step(_bus);
            PrintState(cycles);
        }

        public void RunUntilBreakpoint()
        {
            do
            {
                _debugCpu.Step(_bus);
            } while (!_breakpoints.Contains(_debugCpu.State.PC) &&
                     _debugCpu.State.Status == CpuStatus.Running);
        }

        private void PrintState(int cycles)
        {
            var s = _debugCpu.State;
            Console.WriteLine($"PC:${s.PC:X4} A:${s.A:X2} X:${s.X:X2} Y:${s.Y:X2} " +
                             $"SP:${s.SP:X2} P:{FlagsToString(s.P)} Cycles:{cycles}");
        }

        private static string FlagsToString(byte p)
        {
            return string.Concat(
                (p & 0x80) != 0 ? 'N' : '-',
                (p & 0x40) != 0 ? 'V' : '-',
                '-',  // Unused
                (p & 0x10) != 0 ? 'B' : '-',
                (p & 0x08) != 0 ? 'D' : '-',
                (p & 0x04) != 0 ? 'I' : '-',
                (p & 0x02) != 0 ? 'Z' : '-',
                (p & 0x01) != 0 ? 'C' : '-'
            );
        }
    }
    ```

    ---

    ## Interrupt Handling

    ### Signaling Interrupts

    ```csharp
    // Create CPU instance
    var cpu = CpuFactory.Create(CpuVariant.Wdc65C02);

// Signal an IRQ (handled if I flag is clear)
cpu.SignalIrq();

// Signal an NMI (non-maskable, always handled)
cpu.SignalNmi();

// Signal a Reset (highest priority)
cpu.SignalReset();

// Clear a pending IRQ (for level-triggered behavior)
cpu.ClearIrq();

// Handle pending interrupt at instruction boundary
bool handled = cpu.HandlePendingInterrupt(bus);
```

### Interrupt Priority

Interrupts are checked at instruction boundaries in this order:
1. **Reset** - Highest priority, reinitializes CPU
2. **NMI** - Non-maskable, cannot be disabled
3. **IRQ** - Lowest priority, ignored when I flag is set

### Interrupt Vectors

| Vector | Address | Description |
|--------|---------|-------------|
| NMI | $FFFA-$FFFB | Non-Maskable Interrupt |
| Reset | $FFFC-$FFFD | Hardware Reset |
| IRQ/BRK | $FFFE-$FFFF | Interrupt Request / Break |

### D-Flag Behavior on Interrupts

The D (decimal) flag behavior on interrupts differs by CPU variant:
- **NMOS 6502** (`Nmos6502`, `Nmos6502Simple`): D flag is **unchanged** on IRQ/NMI/BRK
- **65C02** (`Wdc65C02`, `Rockwell65C02`): D flag is **cleared** on IRQ/NMI/BRK

This is automatic based on CPU variant - no configuration needed.

### Example: Timer Interrupt

```csharp
public class TimerSystem
{
    private readonly IPandowdyCpu _cpu;
    private readonly IPandowdyCpuBus _bus;
    private int _timerCounter;
    private const int TimerPeriod = 1000; // cycles

    public TimerSystem(CpuVariant variant, IPandowdyCpuBus bus)
    {
        _bus = bus;
        _cpu = CpuFactory.Create(variant);
    }

    public void RunFrame(int cyclesPerFrame)
    {
        int cyclesRun = 0;
        while (cyclesRun < cyclesPerFrame)
        {
            int cycles = _cpu.Step(_bus);
            cyclesRun += cycles;
            _timerCounter += cycles;

            if (_timerCounter >= TimerPeriod)
            {
                _timerCounter -= TimerPeriod;
                _cpu.SignalIrq();
            }

            // Handle pending interrupt at instruction boundary
            _cpu.HandlePendingInterrupt(_bus);
        }
    }
}
```

### WAI Instruction Behavior

The WAI (Wait for Interrupt) instruction suspends the CPU until an interrupt occurs:

```csharp
// After executing WAI
if (cpu.State.Status == CpuStatus.Waiting)
{
    // CPU is suspended, won't execute instructions
    // Signal an interrupt to wake it
    cpu.SignalIrq();
    cpu.HandlePendingInterrupt(bus);

    // Or signal NMI
    cpu.SignalNmi();
    cpu.HandlePendingInterrupt(bus);
}

// Note: WAI allows IRQ to wake the CPU even if I flag is set
// (but the interrupt handler won't be called if I is set)
```

---

## Halt Instructions and IgnoreHaltStopWait

### Halt Instructions

Three instruction types can halt the CPU:

| Instruction | Status | Recovery | Availability |
|-------------|--------|----------|--------------|
| JAM/KIL | `Jammed` | Reset only | NMOS 6502 |
| STP | `Stopped` | Reset only | 65C02+ |
| WAI | `Waiting` | Interrupt | 65C02+ |

### IgnoreHaltStopWait Property

For debugging or special scenarios, you can make halt instructions continue execution:

```csharp
// Enable bypass mode
cpu.State.IgnoreHaltStopWait = true;

// Now JAM, STP, and WAI will set status to Bypassed
// PC advances normally, CPU continues executing
cpu.Step(bus);

// Check if a halt was bypassed
if (cpu.State.Status == CpuStatus.Bypassed)
{
    Console.WriteLine("A halt instruction was bypassed!");
}

// Reset clears the Bypassed status
cpu.Reset(bus);

// Or manually reset to Running
cpu.State.Status = CpuStatus.Running;

// Disable bypass mode
cpu.State.IgnoreHaltStopWait = false;
```

### The Bypassed Status

When `IgnoreHaltStopWait` is `true` and a halt instruction is encountered:
- The CPU status is set to `Bypassed` (not `Running`)
- The PC advances past the instruction
- Execution continues normally
- The `Bypassed` status persists until `Cpu.Reset()` is called or manually cleared

This allows you to detect when halt instructions were encountered without actually stopping execution.

### Use Cases for IgnoreHaltStopWait

1. **Disassembly/Analysis** - Step through all code paths without halting
2. **Testing** - Verify instruction decoding without side effects
3. **Debugging** - Continue past halt points during investigation
4. **ROM Analysis** - Examine code that uses halt instructions for copy protection
5. **Bypass Detection** - Monitor when halt instructions are encountered

---

## Complete Example

```csharp
using Pandowdy.Cpu;

public class EmulatorExample
{
    public static void Main()
    {
        // Create memory bus
        var bus = new RamBus();

        // Load a simple program: Count from 0 to 10, store at $0200
        // 0400: LDX #$00      ; X = 0
        // 0402: STX $0200     ; Store X at $0200
        // 0405: INX           ; X++
        // 0406: CPX #$0B      ; Compare X with 11
        // 0408: BNE $0402     ; If not equal, loop
        // 040A: STP           ; Stop processor
        byte[] program = {
            0xA2, 0x00,             // LDX #$00
            0x8E, 0x00, 0x02,       // STX $0200
            0xE8,                   // INX
            0xE0, 0x0B,             // CPX #$0B
            0xD0, 0xF8,             // BNE $0402 (-8)
            0xDB                    // STP
        };

        bus.LoadProgram(0x0400, program);
        bus.SetResetVector(0x0400);

        // Create debug CPU for state tracking
        var state = new CpuState();
        var debugCpu = CpuFactory.CreateDebug(CpuVariant.Wdc65C02, state);

        // Reset CPU
        debugCpu.Reset(bus);

        Console.WriteLine("Starting emulation...\n");

        // Run until stopped
        int totalCycles = 0;
        int instructionCount = 0;

        while (debugCpu.State.Status == CpuStatus.Running)
        {
            // Get current PC before execution
            ushort pc = debugCpu.State.PC;

            // Execute one instruction
            int cycles = debugCpu.Step(bus);
            totalCycles += cycles;
            instructionCount++;

            // Print state
            var s = debugCpu.State;
            Console.WriteLine($"${pc:X4}: A=${s.A:X2} X=${s.X:X2} Y=${s.Y:X2} " +
                            $"SP=${s.SP:X2} Cycles={cycles}");

            // Show changed registers
            foreach (var reg in debugCpu.ChangedRegisters)
            {
                Console.WriteLine($"  -> {reg} changed");
            }
        }

        Console.WriteLine($"\nCPU Status: {debugCpu.State.Status}");
        Console.WriteLine($"Total instructions: {instructionCount}");
        Console.WriteLine($"Total cycles: {totalCycles}");
        Console.WriteLine($"Value at $0200: ${bus.Memory[0x0200]:X2}");
    }
}

// Simple RAM bus implementation
public class RamBus : IPandowdyCpuBus
{
    public byte[] Memory { get; } = new byte[65536];

    public byte CpuRead(ushort address) => Memory[address];
    public byte Peek(ushort address) => Memory[address];
    public void Write(ushort address, byte value) => Memory[address] = value;

    public void LoadProgram(ushort start, byte[] program)
    {
        Array.Copy(program, 0, Memory, start, program.Length);
    }

    public void SetResetVector(ushort address)
    {
        Memory[0xFFFC] = (byte)(address & 0xFF);
        Memory[0xFFFD] = (byte)(address >> 8);
    }
}
```

### Expected Output

```
Starting emulation...

$0400: A=$00 X=$00 Y=$00 SP=$FD Cycles=2
  -> X changed
$0402: A=$00 X=$00 Y=$00 SP=$FD Cycles=4
$0405: A=$00 X=$01 Y=$00 SP=$FD Cycles=2
  -> X changed
  -> P changed
$0406: A=$00 X=$01 Y=$00 SP=$FD Cycles=2
  -> P changed
$0408: A=$00 X=$01 Y=$00 SP=$FD Cycles=3
  -> PC changed
...
$040A: A=$00 X=$0A Y=$00 SP=$FD Cycles=3

CPU Status: Stopped
Total instructions: 56
Total cycles: 142
Value at $0200: $0A
```

---

## Additional Resources

- [6502 Instruction Set Reference](http://www.obelisk.me.uk/6502/reference.html)
- [65C02 Datasheet](https://www.westerndesigncenter.com/wdc/documentation/w65c02s.pdf)
- [Rockwell R65C02 Datasheet](https://archive.org/details/R65C02_Datasheet)

---

## API Reference

### IPandowdyCpu Interface

| Method/Property | Signature | Description |
|-----------------|-----------|-------------|
| `Clock` | `bool Clock(IPandowdyCpuBus)` | Execute one cycle, returns true when instruction completes |
| `Step` | `int Step(IPandowdyCpuBus)` | Execute one instruction, returns cycles |
| `Run` | `int Run(IPandowdyCpuBus, int)` | Execute up to n cycles |
| `Reset` | `void Reset(IPandowdyCpuBus)` | Reset CPU and load PC from reset vector |
| `SignalIrq` | `void SignalIrq()` | Signal an IRQ interrupt |
| `SignalNmi` | `void SignalNmi()` | Signal an NMI interrupt |
| `SignalReset` | `void SignalReset()` | Signal a hardware reset |
| `ClearIrq` | `void ClearIrq()` | Clear pending IRQ |
| `HandlePendingInterrupt` | `bool HandlePendingInterrupt(IPandowdyCpuBus)` | Handle pending interrupt, returns true if handled |
| `Variant` | `CpuVariant` | The CPU variant this instance emulates |
| `State` | `CpuState` | Get/set the CPU state (injected via factory) |

### CpuFactory Class

| Method | Signature | Description |
|--------|-----------|-------------|
| `Create` | `IPandowdyCpu Create(CpuVariant, CpuState)` | Create a CPU instance with the provided state |
| `CreateDebug` | `DebugCpu CreateDebug(CpuVariant, CpuState)` | Create a debug CPU wrapper with the provided state |
| `CreateDebug` | `DebugCpu CreateDebug(IPandowdyCpu)` | Wrap an existing CPU in a debug wrapper |

### CPU Classes

| Class | Variant | Description |
|-------|---------|-------------|
| `Cpu6502` | `Nmos6502` | NMOS 6502 with illegal opcodes |
| `Cpu6502Simple` | `Nmos6502Simple` | NMOS 6502, illegal opcodes as NOPs |
| `Cpu65C02` | `Wdc65C02` | WDC 65C02 |
| `Cpu65C02Rockwell` | `Rockwell65C02` | Rockwell 65C02 |

### CpuState Class

| Property | Type | Description |
|----------|------|-------------|
| `A`, `X`, `Y` | `byte` | Registers |
| `SP` | `byte` | Stack Pointer |
| `PC` | `ushort` | Program Counter |
| `P` | `byte` | Processor Status |
| `Status` | `CpuStatus` | Execution status |
| `CurrentOpcode` | `byte` | Opcode being executed |
| `OpcodeAddress` | `ushort` | Address where opcode was fetched |
| `CyclesRemaining` | `int` | Remaining cycles in current instruction |
| `PendingInterrupt` | `PendingInterrupt` | Pending interrupt signal |
| `IgnoreHaltStopWait` | `bool` | Bypass halt instructions |

| Method | Signature | Description |
|--------|-----------|-------------|
| `Clone` | `CpuState Clone()` | Create a deep copy of this state |
| `CopyFrom` | `void CopyFrom(CpuState)` | Copy values from another state (no allocation) |
| `Reset` | `void Reset()` | Reset to power-on defaults (no bus interaction) |

### DebugCpu Class

A debugging wrapper that tracks state changes between instructions.

| Property | Type | Description |
|----------|------|-------------|
| `UnderlyingCpu` | `IPandowdyCpu` | The wrapped CPU instance |
| `State` | `CpuState` | Current CPU state (same as underlying CPU) |
| `PrevState` | `CpuState?` | State snapshot before the last instruction |
| `PcChanged` | `bool` | PC changed this instruction |
| `JumpOccurred` | `bool` | Non-sequential PC change |
| `BranchOccurred` | `bool` | Branch was taken |
| `ReturnOccurred` | `bool` | RTS/RTI executed |
| `InterruptOccurred` | `bool` | Interrupt serviced |
| `PageCrossed` | `bool` | Page boundary crossed during addressing |
| `StackActivityOccurred` | `bool` | Stack pointer changed |
| `StackDelta` | `int` | Stack pointer change (negative=push, positive=pull) |
| `ChangedRegisters` | `IEnumerable<string>` | Names of modified registers |

