# Pandowdy.CPU Emulator

A cycle-accurate 6502/65C02 CPU emulator written in C# and F# for .NET 8.

## Features

- **Cycle-Accurate Emulation** — Micro-op pipeline architecture provides true cycle-level timing
- **Stateless Core** — CPU execution engine is stateless; all state lives in `CpuStateBuffer`, enabling save states, multiple instances, and easy testing
- **Multiple CPU Variants** — NMOS 6502, 65C02, and Rockwell 65C02 with bit manipulation instructions
- **Double-Buffered State** — Clean instruction boundaries for debugging, single-stepping, and state comparison
- **Interrupt Support** — IRQ, NMI, and Reset with proper priority handling
- **Extensible Bus Interface** — Simple `IPandowdyCpuBus` interface for custom memory maps and I/O

## Supported CPU Variants

| Variant | Description |
|---------|-------------|
| `NMOS6502` | Original NMOS 6502 with undocumented opcodes |
| `NMOS6502_NO_UNDOC` | NMOS 6502 with undefined opcodes as NOPs |
| `CMOS65C02` | WDC 65C02 with new instructions (STZ, PHX, BRA, etc.) |
| `ROCKWELL65C02` | Rockwell 65C02 with bit manipulation (RMB, SMB, BBR, BBS) |

## Quick Start

```csharp
using Pandowdy.Cpu;

// Create memory bus and CPU state
var bus = new RamBus();
var cpuBuffer = new CpuStateBuffer();

// Load program and set reset vector
byte[] program = { 0xA9, 0x42, 0x8D, 0x00, 0x02 }; // LDA #$42, STA $0200
bus.LoadProgram(0x0400, program);
bus.SetResetVector(0x0400);

// Reset and execute
Cpu.Reset(cpuBuffer, bus);
int cycles = Cpu.Step(CpuVariant.CMOS65C02, cpuBuffer, bus);

Console.WriteLine($"A = ${cpuBuffer.Current.A:X2}"); // A = $42
```

## API Overview

| Function | Description |
|----------|-------------|
| `Cpu.Clock()` | Execute one CPU cycle |
| `Cpu.Step()` | Execute one complete instruction |
| `Cpu.Run()` | Execute for a specified number of cycles |
| `Cpu.Reset()` | Reset CPU and load PC from reset vector |

## Project Structure

```
Pandowdy.Cpu/          # Core types (CpuState, CpuStateBuffer, IPandowdyCpuBus)
Pandowdy.Cpu.Core/     # F# CPU implementation (micro-ops, pipelines)
Pandowdy.Cpu.Tests/    # xUnit test suite
Pandowdy.Cpu.Example/  # Example usage application
```

## Documentation

For detailed usage instructions, API reference, and examples, see the [CPU Usage Guide](docs/CpuUsageGuide.md).

## Building

```bash
dotnet build
dotnet test
```

## License

Licensed under the Apache License, Version 2.0. See [LICENSE](LICENSE) for details.

## Author

Copyright 2026 Mark D. Long
