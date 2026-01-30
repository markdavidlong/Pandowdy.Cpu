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
├── Cpu.cs                    # Clock, Step, Run, Reset functions
├── CpuState.cs               # CPU state and registers
├── CpuStateBuffer.cs         # Double-buffered state
├── CpuVariant.cs             # CPU variant and status enums
├── IPandowdyCpuBus.cs        # Bus interface
│
└── Internals/                # Internal implementation (not part of public API)
    ├── MicroOp.cs            # Micro-operation delegate
    ├── MicroOps.cs           # Micro-operation implementations
    ├── Pipelines.cs          # Opcode pipeline definitions
    └── Pipelines.*.cs        # Pipeline partial classes (Alu, Branch, etc.)

Pandowdy.Cpu.Tests/            # Unit tests (xUnit)
├── *OpcodeTests.cs           # Opcode-specific tests
├── InterruptTests.cs         # Interrupt handling tests
└── CpuTestBase.cs            # Test base class

Pandowdy.Cpu.Dormann-Tests/    # Klaus Dormann test runner
Pandowdy.Cpu.Harte-SST-Tests/  # Tom Harte SST test runner

docs/                          # Documentation
├── CpuUsageGuide.md          # Usage guide with examples
└── ApiReference.md           # Complete API reference
```

## NuGet Package

To create a NuGet package:

```bash
dotnet pack -c Release
```

The package will be created in `bin/Release/`.

## Troubleshooting

### Build Errors: ENC0097

If you see "Applying source changes while the application is running" errors:

1. Stop any debugging sessions (Shift+F5)
2. Check Task Manager for orphaned processes
3. Close and reopen Visual Studio
4. Run `dotnet clean && dotnet build`

### Test Discovery Issues

If tests aren't discovered:

```bash
dotnet clean
dotnet build
dotnet test --no-build
```
