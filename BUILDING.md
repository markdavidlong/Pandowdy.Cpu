# Building Pandowdy.Cpu

Instructions for building, testing, and running the Pandowdy.Cpu emulator.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- (Optional) Visual Studio 2022 or VS Code with C# extension

## Building

### Command Line

```bash
# Navigate to the solution directory
cd Pandowdy.Cpu

# Restore dependencies and build
dotnet build

# Build in Release mode
dotnet build -c Release

# Clean build artifacts
dotnet clean

# Full clean rebuild
dotnet clean && dotnet build
```

### Visual Studio

1. Open `Pandowdy.Cpu.slnx` in Visual Studio 2022
2. Build → Build Solution (Ctrl+Shift+B)

## Running Tests

### Unit Tests

The project includes over 2,000 unit tests covering all CPU variants and opcodes:

```bash
# Run all tests
dotnet test

# Run tests with verbose output
dotnet test --verbosity normal

# Run tests for a specific class
dotnet test --filter "FullyQualifiedName~WDC65C02Tests"

# Run a specific test
dotnet test --filter "WAI_Integration"
```

### External Test Suites

#### Klaus Dormann Functional Tests

Runs comprehensive validation tests for all CPU variants:

```bash
# Run the Dormann test suite
dotnet run --project Pandowdy.Cpu.Dormann-Tests
```

**Requirements:** Download the test binaries from [Klaus Dormann's 6502 test suite](https://github.com/Klaus2m5/6502_65C02_functional_tests) and configure the path in `dormann-tests.json`.

See [Pandowdy.Cpu.Dormann-Tests/README.md](Pandowdy.Cpu.Dormann-Tests/README.md) for detailed setup instructions.

#### Tom Harte SingleStepTests

Validates cycle-accurate bus activity for every opcode:

```bash
# Run the Harte SST suite
dotnet run --project Pandowdy.Cpu.Harte-SST-Tests
```

**Requirements:** Clone the [65x02 SingleStepTests repository](https://github.com/SingleStepTests/65x02) and set the path via:
- Command line argument: `dotnet run --project Pandowdy.Cpu.Harte-SST-Tests -- "path/to/65x02"`
- Environment variable: `HARTE_SST_PATH`

See [Pandowdy.Cpu.Harte-SST-Tests/README.md](Pandowdy.Cpu.Harte-SST-Tests/README.md) for detailed setup instructions.


## Project Structure

```
Pandowdy.Cpu/                  # Core library (C#)
├── IPandowdyCpu.cs           # Public CPU interface
├── IPandowdyCpuBus.cs        # Bus interface
├── CpuBase.cs                # Base class with Clock, Step, Run, Reset
├── Cpu6502.cs                # NMOS 6502 implementation
├── Cpu6502Simple.cs          # NMOS 6502 without illegal opcodes
├── Cpu65C02.cs               # WDC 65C02 implementation
├── Cpu65C02Rockwell.cs       # Rockwell 65C02 with BBR/BBS/RMB/SMB
├── CpuFactory.cs             # Factory for creating CPU instances
├── CpuState.cs               # CPU state and registers
├── DebugCpu.cs               # Debugging wrapper with state tracking
├── CpuVariant.cs             # CPU variant and status enums
│
└── Internals/                # Internal implementation (not part of public API)
    ├── MicroOp.cs            # Micro-operation delegate
    ├── MicroOps.cs           # Micro-operation implementations
    ├── Pipelines.cs          # Opcode pipeline definitions and LDA/LDX/LDY/STA/STX/STY
    ├── Pipelines.Alu.cs      # ADC, SBC, AND, ORA, EOR, CMP, CPX, CPY, BIT
    ├── Pipelines.Branch.cs   # Branch instructions (BCC, BCS, BEQ, etc.)
    ├── Pipelines.Illegal.cs  # NMOS illegal/undocumented opcodes
    ├── Pipelines.IncDec.cs   # INC, DEC, INX, INY, DEX, DEY
    ├── Pipelines.Jump.cs     # JMP, JSR, RTS, RTI
    ├── Pipelines.Logic.cs    # Transfer and flag instructions
    ├── Pipelines.Shift.cs    # ASL, LSR, ROL, ROR
    ├── Pipelines.Special.cs  # NOP, BRK, interrupts, 65C02 extensions
    └── Pipelines.Stack.cs    # PHA, PLA, PHP, PLP, PHX, PHY, PLX, PLY

Pandowdy.Cpu.Tests/            # Unit tests (xUnit)
├── CpuTestBase.cs            # Test base class with helper methods
├── TestRamBus.cs             # Simple RAM bus for testing
├── CoreInstructionTests.cs   # Core instruction behavior tests
├── CpuInstanceTests.cs       # CPU instance and factory tests
├── CpuModuleTests.cs         # Module-level integration tests
├── CpuStateTests.cs          # CPU state tests
├── DebugCpuTests.cs          # Debug wrapper tests
├── DecimalModeTests.cs       # BCD arithmetic tests
├── InterruptTests.cs         # Interrupt handling tests
├── InterruptEdgeCaseTests.cs # Edge cases for interrupts
├── NMOS6502Tests.cs          # NMOS 6502 variant tests
├── NMOS6502OpcodeTests.cs    # NMOS opcode behavior tests
├── NMOS6502IllegalOpcodeTests.cs # Illegal opcode tests
├── NMOS6502_NoIllegalTests.cs # Simple variant tests
├── WDC65C02Tests.cs          # WDC 65C02 tests
└── Rockwell65C02Tests.cs     # Rockwell extensions tests

Pandowdy.Cpu.Dormann-Tests/    # Klaus Dormann test runner
Pandowdy.Cpu.Harte-SST-Tests/  # Tom Harte SST test runner

docs/                          # Documentation
├── ApiReference.md           # Complete API reference
├── CpuUsageGuide.md          # Usage guide with examples
└── MicroOps-Architecture.md  # Micro-ops pipeline architecture
```

