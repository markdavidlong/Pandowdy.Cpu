# Pandowdy 6502/65C02 CPU Emulator Usage Guide

This guide demonstrates how to use the `Cpu`, `CpuState`, and `CpuStateBuffer` classes to emulate 6502-family processors.

## Table of Contents

- [Overview](#overview)
- [Quick Start](#quick-start)
- [CPU Variants](#cpu-variants)
- [Setting Up the Bus](#setting-up-the-bus)
- [CPU Execution](#cpu-execution)
- [Working with CPU State](#working-with-cpu-state)
- [Using CpuStateBuffer for Debugging](#using-cpustatebuffer-for-debugging)
- [Interrupt Handling](#interrupt-handling)
- [Halt Instructions and IgnoreHaltStopWait](#halt-instructions-and-ignorehaltstopwait)
- [Complete Example](#complete-example)

---

## Overview

The CPU emulator uses a micro-op pipeline architecture for cycle-accurate emulation. The key components are:

| Component | Description |
|-----------|-------------|
| `Cpu` | Static execution engine with `Clock`, `Step`, `Run`, and `Reset` functions |
| `CpuState` | Complete CPU state including registers, flags, and execution status |
| `CpuStateBuffer` | Double-buffered state for clean instruction boundaries and debugging |
| `IPandowdyCpuBus` | Interface for memory read/write operations |
| `CpuVariant` | Enum specifying which CPU variant to emulate |

### Architecture Diagram

```
┌────────────────────────────────────────────────────────┐
│                    CpuStateBuffer                      │
│  ┌─────────────────┐    ┌─────────────────┐            │
│  │      Prev       │    │     Current     │            │
│  │   (committed)   │--->│   (working)     │            │
│  │                 │    │                 │            │
│  │ A, X, Y, SP, PC │    │ A, X, Y, SP, PC │            │
│  │ P (flags)       │    │ P (flags)       │            │
│  │ Status          │    │ Status          │            │
│  └─────────────────┘    └─────────────────┘            │
└────────────────────────────────────────────────────────┘
         │                        │
         │                        ▼
         │              ┌─────────────────┐
         │              │   Cpu.Clock()   │
         │              │   Cpu.Step()    │
         │              │   Cpu.Run()     │
         │              └────────┬────────┘
         │                       │
         │                       ▼
         │              ┌─────────────────┐
         └─────────────>│ IPandowdyCpuBus │
                        │  (Memory I/O)   │
                        └─────────────────┘
```

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

// 3. Create the CPU state buffer
var cpuBuffer = new CpuStateBuffer();

// 4. Reset the CPU (loads PC from reset vector)
Cpu.Reset(cpuBuffer, bus);

// 5. Execute instructions
int cycles = Cpu.Step(CpuVariant.CMOS65C02, cpuBuffer, bus);
Console.WriteLine($"Instruction took {cycles} cycles");
Console.WriteLine($"A = ${cpuBuffer.Current.A:X2}");
```

---

## CPU Variants

The emulator supports four CPU variants:

```csharp
public enum CpuVariant
{
    NMOS6502,           // Original NMOS 6502 with undocumented opcodes
    NMOS6502_NO_UNDOC,  // NMOS 6502, undefined opcodes treated as NOPs
    CMOS65C02,          // WDC 65C02 with new instructions (STZ, PHX, etc.)
    ROCKWELL65C02       // Rockwell 65C02 with bit manipulation (RMB, SMB, BBR, BBS)
}
```

### Variant Differences

| Feature | NMOS6502 | NMOS6502_NO_UNDOC | CMOS65C02 | ROCKWELL65C02 |
|---------|----------|-------------------|-----------|---------------|
| Undocumented opcodes | ✅ | ❌ (NOPs) | ❌ | ❌ |
| JMP ($xxFF) bug | ✅ | ✅ | ❌ (fixed) | ❌ (fixed) |
| STZ, PHX, PHY, PLX, PLY | ❌ | ❌ | ✅ | ✅ |
| INC A, DEC A | ❌ | ❌ | ✅ | ✅ |
| BRA (branch always) | ❌ | ❌ | ✅ | ✅ |
| TRB, TSB | ❌ | ❌ | ✅ | ✅ |
| STP, WAI | ❌ | ❌ | ✅ | ✅ |
| RMB, SMB, BBR, BBS | ❌ | ❌ | ❌ | ✅ |
| JAM/KIL opcodes | ✅ (freezes) | ❌ | ❌ | ❌ |

---

## Setting Up the Bus

Implement the `IPandowdyCpuBus` interface to connect the CPU to your memory system:

```csharp
public interface IPandowdyCpuBus
{
    byte CpuRead(ushort address);
    void Write(ushort address, byte value);
}
```

### Simple RAM Implementation

```csharp
public class RamBus : IPandowdyCpuBus
{
    public byte[] Memory { get; } = new byte[65536];

    public byte CpuRead(ushort address) => Memory[address];

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

### Single Cycle: `Cpu.Clock()`

Executes one CPU clock cycle. Returns `true` when an instruction completes.

```csharp
bool instructionComplete = Cpu.Clock(CpuVariant.CMOS65C02, cpuBuffer, bus);

if (instructionComplete)
{
    Console.WriteLine("Instruction finished!");
}
```

### Single Instruction: `Cpu.Step()`

Executes cycles until one instruction completes. Returns the cycle count.

```csharp
int cycles = Cpu.Step(CpuVariant.CMOS65C02, cpuBuffer, bus);
Console.WriteLine($"Instruction took {cycles} cycles");
```

### Multiple Cycles: `Cpu.Run()`

Executes a specified number of cycles. Useful for running at a target frequency.

```csharp
// Run for approximately 1MHz (1,000,000 cycles per second)
// At 60 FPS, that's ~16,667 cycles per frame
int cyclesPerFrame = 16667;
int actualCycles = Cpu.Run(CpuVariant.CMOS65C02, cpuBuffer, bus, cyclesPerFrame);
```

### Reset: `Cpu.Reset()`

Initializes the CPU and loads PC from the reset vector.

```csharp
Cpu.Reset(cpuBuffer, bus);
// PC is now loaded from $FFFC-$FFFD
```

### Helper Functions

```csharp
// Get the opcode of the current/previous instruction
byte opcode = Cpu.CurrentOpcode(cpuBuffer, bus);

// Get remaining cycles in current instruction pipeline
int remaining = Cpu.CyclesRemaining(cpuBuffer);
```

---

## Working with CPU State

### Accessing Registers

```csharp
CpuState state = cpuBuffer.Current;

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

    ## Using CpuStateBuffer for Debugging

The double-buffer architecture enables powerful debugging capabilities.

### Comparing State Changes

```csharp
// Execute one instruction
Cpu.Step(variant, cpuBuffer, bus);

// Compare before and after
CpuState before = cpuBuffer.Prev;
CpuState after = cpuBuffer.Current;

if (before.A != after.A)
{
    Console.WriteLine($"A changed: ${before.A:X2} -> ${after.A:X2}");
}

// Use the built-in helper
foreach (string reg in cpuBuffer.ChangedRegisters)
{
    Console.WriteLine($"Register {reg} was modified");
}
```

### Detecting Instruction Types

```csharp
Cpu.Step(variant, cpuBuffer, bus);

if (cpuBuffer.JumpOccurred)
{
    Console.WriteLine($"Jump to ${cpuBuffer.Current.PC:X4}");
}

if (cpuBuffer.BranchOccurred)
{
    Console.WriteLine("Branch was taken");
}

if (cpuBuffer.ReturnOccurred)
{
    Console.WriteLine("Returned from subroutine");
}

if (cpuBuffer.InterruptOccurred)
{
    Console.WriteLine("Interrupt was serviced");
}

if (cpuBuffer.PageCrossed)
{
    Console.WriteLine("Page boundary crossed (possible extra cycle)");
}
```

### Stack Monitoring

```csharp
if (cpuBuffer.StackActivityOccurred)
{
    int delta = cpuBuffer.StackDelta;
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
    private readonly CpuVariant _variant;
    private readonly CpuStateBuffer _buffer;
    private readonly IPandowdyCpuBus _bus;
    private readonly HashSet<ushort> _breakpoints = new();

    public SimpleDebugger(CpuVariant variant, CpuStateBuffer buffer, IPandowdyCpuBus bus)
    {
        _variant = variant;
        _buffer = buffer;
        _bus = bus;
    }

    public void AddBreakpoint(ushort address) => _breakpoints.Add(address);
    public void RemoveBreakpoint(ushort address) => _breakpoints.Remove(address);

    public void Step()
    {
        int cycles = Cpu.Step(_variant, _buffer, _bus);
        PrintState(cycles);
    }

    public void RunUntilBreakpoint()
    {
        do
        {
            Cpu.Step(_variant, _buffer, _bus);
        } while (!_breakpoints.Contains(_buffer.Current.PC) &&
                 _buffer.Current.Status == CpuStatus.Running);
    }

    private void PrintState(int cycles)
    {
        var s = _buffer.Current;
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
CpuState state = cpuBuffer.Current;

// Signal an IRQ (handled if I flag is clear)
state.SignalIrq();

// Signal an NMI (non-maskable, always handled)
state.SignalNmi();

// Signal a Reset (highest priority)
state.SignalReset();

// Clear a pending IRQ (for level-triggered behavior)
state.ClearIrq();
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

### Example: Timer Interrupt

```csharp
public class TimerSystem
{
    private readonly CpuStateBuffer _buffer;
    private readonly IPandowdyCpuBus _bus;
    private int _timerCounter;
    private const int TimerPeriod = 1000; // cycles

    public void RunFrame(CpuVariant variant, int cyclesPerFrame)
    {
        int cyclesRun = 0;
        while (cyclesRun < cyclesPerFrame)
        {
            int cycles = Cpu.Step(variant, _buffer, _bus);
            cyclesRun += cycles;
            _timerCounter += cycles;

            if (_timerCounter >= TimerPeriod)
            {
                _timerCounter -= TimerPeriod;
                _buffer.Current.SignalIrq();
            }
        }
    }
}
```

### WAI Instruction Behavior

The WAI (Wait for Interrupt) instruction suspends the CPU until an interrupt occurs:

```csharp
// After executing WAI
if (cpuBuffer.Current.Status == CpuStatus.Waiting)
{
    // CPU is suspended, won't execute instructions
    // Signal an interrupt to wake it
    cpuBuffer.Current.SignalIrq();
    
    // Or signal NMI
    cpuBuffer.Current.SignalNmi();
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
cpuBuffer.Current.IgnoreHaltStopWait = true;
cpuBuffer.Prev.IgnoreHaltStopWait = true;

// Now JAM, STP, and WAI will set status to Bypassed
// PC advances normally, CPU continues executing
Cpu.Step(variant, cpuBuffer, bus);

// Check if a halt was bypassed
if (cpuBuffer.Current.Status == CpuStatus.Bypassed)
{
    Console.WriteLine("A halt instruction was bypassed!");
}

// Reset clears the Bypassed status
Cpu.Reset(cpuBuffer, bus);

// Or manually reset to Running
cpuBuffer.Current.Status = CpuStatus.Running;

// Disable bypass mode
cpuBuffer.Current.IgnoreHaltStopWait = false;
cpuBuffer.Prev.IgnoreHaltStopWait = false;
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

        // Create CPU state buffer
        var cpuBuffer = new CpuStateBuffer();

        // Choose CPU variant
        var variant = CpuVariant.CMOS65C02;

        // Reset CPU
        Cpu.Reset(cpuBuffer, bus);

        Console.WriteLine("Starting emulation...\n");

        // Run until stopped
        int totalCycles = 0;
        int instructionCount = 0;

        while (cpuBuffer.Current.Status == CpuStatus.Running)
        {
            // Get current PC before execution
            ushort pc = cpuBuffer.Current.PC;

            // Execute one instruction
            int cycles = Cpu.Step(variant, cpuBuffer, bus);
            totalCycles += cycles;
            instructionCount++;

            // Print state
            var s = cpuBuffer.Current;
            Console.WriteLine($"${pc:X4}: A=${s.A:X2} X=${s.X:X2} Y=${s.Y:X2} " +
                            $"SP=${s.SP:X2} Cycles={cycles}");

            // Show changed registers
            foreach (var reg in cpuBuffer.ChangedRegisters)
            {
                Console.WriteLine($"  -> {reg} changed");
            }
        }

        Console.WriteLine($"\nCPU Status: {cpuBuffer.Current.Status}");
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

### Cpu Module (F#)

| Function | Signature | Description |
|----------|-----------|-------------|
| `Clock` | `CpuVariant -> CpuStateBuffer -> IPandowdyCpuBus -> bool` | Execute one cycle |
| `Step` | `CpuVariant -> CpuStateBuffer -> IPandowdyCpuBus -> int` | Execute one instruction, returns cycles |
| `Run` | `CpuVariant -> CpuStateBuffer -> IPandowdyCpuBus -> int -> int` | Execute n cycles |
| `Reset` | `CpuStateBuffer -> IPandowdyCpuBus -> unit` | Reset CPU |
| `CurrentOpcode` | `CpuStateBuffer -> IPandowdyCpuBus -> byte` | Get current opcode |
| `CyclesRemaining` | `CpuStateBuffer -> int` | Get remaining pipeline cycles |

### CpuState Class

| Property | Type | Description |
|----------|------|-------------|
| `A`, `X`, `Y` | `byte` | Registers |
| `SP` | `byte` | Stack Pointer |
| `PC` | `ushort` | Program Counter |
| `P` | `byte` | Processor Status |
| `Status` | `CpuStatus` | Execution status |
| `IgnoreHaltStopWait` | `bool` | Bypass halt instructions |

### CpuStateBuffer Class

| Property | Type | Description |
|----------|------|-------------|
| `Prev` | `CpuState` | Committed state |
| `Current` | `CpuState` | Working state |
| `PcChanged` | `bool` | PC changed this instruction |
| `JumpOccurred` | `bool` | Non-sequential PC change |
| `BranchOccurred` | `bool` | Branch was taken |
| `ReturnOccurred` | `bool` | RTS/RTI executed |
| `InterruptOccurred` | `bool` | Interrupt serviced |
| `StackDelta` | `int` | Stack pointer change |
| `ChangedRegisters` | `IEnumerable<string>` | Modified registers |
