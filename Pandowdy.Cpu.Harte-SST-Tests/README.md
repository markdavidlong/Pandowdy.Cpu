# Harte SingleStepTests (SST) Runner

This project runs the [Tom Harte SingleStepTests](https://github.com/SingleStepTests/65x02) against the Pandowdy.Cpu emulator to validate cycle-accurate 6502/65C02 emulation.

## Test Data Setup

The JSON test files are not included in this repository for licensing reasons. You must download them separately.

### Download Instructions

1. Visit https://github.com/SingleStepTests/65x02
2. Download or clone the repository
3. Copy the processor-specific test folders to a `TestData` directory alongside this project:

```
Pandowdy.Cpu.Harte-SST-Tests/
├── TestData/
│   ├── wdc65c02/           # WDC 65C02 tests
│   │   ├── v1/
│   │   │   ├── 00.json
│   │   │   ├── 01.json
│   │   │   └── ... (256 files, one per opcode)
│   ├── 65c02rockwell/      # Rockwell 65C02 tests
│   │   └── v1/
│   │       └── ...
│   └── 6502/               # NMOS 6502 tests
│       └── v1/
│           └── ...
```

### Quick Setup (Command Line)

```bash
cd Pandowdy.Cpu.Harte-SST-Tests
mkdir TestData
cd TestData

# Clone the test repository
git clone https://github.com/SingleStepTests/65x02.git .

# Or download specific processor tests only
# (check the repository for available processors)
```

## Running Tests

```bash
dotnet run --project Pandowdy.Cpu.Harte-SST-Tests -- <variant> [options]
```

### Variants
- `wdc65c02` - WDC 65C02
- `rockwell65c02` - Rockwell 65C02
- `nmos6502` - NMOS 6502

### Options
- `--opcode XX` - Test only a specific opcode (hex)
- `--verbose` - Show detailed cycle-by-cycle output for failures
- `--max-failures N` - Stop after N failures per opcode

### Examples

```bash
# Run all WDC 65C02 tests
dotnet run -- wdc65c02

# Test only opcode $69 (ADC immediate)
dotnet run -- wdc65c02 --opcode 69

# Run with verbose output
dotnet run -- nmos6502 --verbose
```

## Test Output Format

```
[FAIL] XX: YYYY/10000 passed
       [#N] test case
       Failure: <description>
       Initial: PC=$XXXX A=$XX X=$XX Y=$XX SP=$XX P=$XX [flags]
       Instruction bytes: $XX $XX $XX
       Cycles (Expected vs Actual):
         T0: $ADDR=$VAL (R/W)     | $ADDR=$VAL (R/W)
         T1: ...                   | ...
```

## License

The Harte SingleStepTests are provided under their own license. Please see the [SingleStepTests repository](https://github.com/SingleStepTests/65x02) for licensing details.
