# Pandowdy.CPU Emulator

A cycle-accurate 6502/65C02 CPU emulator written in C# for .NET 8.

## Features

- **Cycle-Accurate Emulation** — Micro-op pipeline architecture provides true cycle-level timing
- **Instance-Based API** — Create CPU instances via `CpuFactory.Create()` with direct state access
- **Multiple CPU Variants** — NMOS 6502, 65C02, and Rockwell 65C02 with bit manipulation instructions
- **Direct State Access** — Simple `cpu.State` property for register access and debugging
- **Debugging Wrapper** — `DebugCpu` wrapper tracks state changes between instructions
- **Interrupt Support** — IRQ, NMI, and Reset with proper priority handling and variant-specific D-flag behavior
- **Extensible Bus Interface** — Simple `IPandowdyCpuBus` interface for custom memory maps and I/O
- **Pure C#** — No external dependencies beyond .NET 8

## Validation

All CPU variants pass [Klaus Dormann's 6502/65C02 Functional Tests](https://github.com/Klaus2m5/6502_65C02_functional_tests), a standard test suite for 6502 emulator validation:

| Test | Nmos6502 | Nmos6502Simple | Wdc65C02 | Rockwell65C02 |
|------|----------|----------------|----------|---------------|
| 6502 Functional Test | ✓ | ✓ | ✓ | ✓ |
| 6502 Decimal Test | ✓ | ✓ | ✓ | ✓ |
| 6502 Interrupt Test | ✓ | ✓ | ✓ | ✓ |
| 65C02 Extended Opcodes Test | — | — | ✓ | ✓ |


### Cycle-Accurate Validation

All CPU variants are **cycle-accurate** and pass the [Tom Harte SingleStepTests](https://github.com/SingleStepTests/65x02), which validate not only final register state but also cycle-by-cycle bus activity for every opcode:

| Variant | Pass Rate | Coverage |
|---------|-----------|-------|
| Nmos6502 | 100% (256 opcodes × 10,000 tests each) | All opcodes tested |
| Nmos6502Simple | 100% (151 opcodes × 10,000 tests each) | Only documented opcodes tested | 
| Wdc65C02 | 100% (254 opcodes × 10,000 tests each) | All opcodes except WAI & STP tested |
| Rockwell65C02 | 100% (256 opcodes × 10,000 tests each) | All opcodes tested |

See [Pandowdy.Cpu.Harte-SST-Tests/README.md](Pandowdy.Cpu.Harte-SST-Tests/README.md) for instructions on running these tests.

## Supported CPU Variants

| Variant | Class | Description |
|---------|-------|-------------|
| `Nmos6502` | `Cpu6502` | Original NMOS 6502 with illegal opcodes |
| `Nmos6502Simple` | `Cpu6502Simple` | NMOS 6502 with undefined opcodes as NOPs |
| `Wdc65C02` | `Cpu65C02` | Later WDC 65C02 (W65C02S) with all CMOS instructions including RMB/SMB/BBR/BBS |
| `Rockwell65C02` | `Cpu65C02Rockwell` | Rockwell 65C02 (same as WDC but WAI/STP are NOPs) |

## Quick Start

```csharp
using Pandowdy.Cpu;

// Create memory bus
var bus = new RamBus();

// Create CPU state and instance
var state = new CpuState();
var cpu = CpuFactory.Create(CpuVariant.Wdc65C02, state);

// Load program and set reset vector
byte[] program = { 0xA9, 0x42, 0x8D, 0x00, 0x02 }; // LDA #$42, STA $0200
bus.LoadProgram(0x0400, program);
bus.SetResetVector(0x0400);

// Reset and execute
cpu.Reset(bus);
int cycles = cpu.Step(bus);

Console.WriteLine($"A = ${cpu.State.A:X2}"); // A = $42
```

### Debugging with DebugCpu

For debugging, use `DebugCpu` which tracks state changes between instructions:

```csharp
// Create a debugging wrapper
var state = new CpuState();
var debugCpu = CpuFactory.CreateDebug(CpuVariant.Wdc65C02, state);

debugCpu.Reset(bus);
debugCpu.Step(bus);

// Check what changed
if (debugCpu.BranchOccurred)
    Console.WriteLine("Branch was taken!");

foreach (var reg in debugCpu.ChangedRegisters)
    Console.WriteLine($"  {reg} changed");
```

## Project Structure

```
Pandowdy.Cpu/              # Core library (CPU classes, MicroOps, Pipelines, CpuState, DebugCpu)
Pandowdy.Cpu.Tests/        # xUnit test suite (2,183 tests)
Pandowdy.Cpu.Dormann-Tests/ # Klaus Dormann functional test runner
Pandowdy.Cpu.Harte-SST-Tests/ # Tom Harte SingleStepTests runner
```

## Documentation

- [CPU Usage Guide](docs/CpuUsageGuide.md) — Detailed usage instructions and examples
- [API Reference](docs/ApiReference.md) — Complete API documentation for IPandowdyCpu, CpuFactory, CpuState, and DebugCpu
- [Micro-Ops Architecture](docs/MicroOps-Architecture.md) — How the cycle-accurate micro-op pipeline works
- [Building](BUILDING.md) — Build instructions, running tests, and project structure

## Quick Build

```bash
dotnet build
dotnet test
```

For detailed build instructions, external test suites, and troubleshooting, see [BUILDING.md](BUILDING.md).

## License

Licensed under the Apache License, Version 2.0. See [LICENSE](LICENSE) for details.

---

## Author

Copyright 2026 Mark D. Long
