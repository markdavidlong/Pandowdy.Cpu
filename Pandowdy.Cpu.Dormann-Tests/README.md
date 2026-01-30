# Pandowdy 65C02 CPU - Dormann Test Suite Runner

A test runner for Klaus Dormann's 6502/65C02 functional tests and Bruce Clark's decimal mode test.

## Overview

This project runs external validation tests against the Pandowdy CPU emulator to verify correct instruction execution across all supported CPU variants:

- **NMOS6502** - Original 6502 with undocumented opcodes
- **NMOS6502_NO_ILLEGAL** - 6502 with illegal opcodes treated as NOPs
- **WDC65C02** - Western Design Center 65C02
- **ROCKWELL65C02** - Rockwell 65C02 (includes BBR/BBS/RMB/SMB instructions)

## Required Test Files

Place the following Intel HEX files in your test data directory:

| File Name | CPU Variants | Description |
|-----------|--------------|-------------|
| `6502_functional_test.hex` | All | Tests all documented 6502 opcodes and addressing modes |
| `6502_decimal_test.hex` | NMOS6502, NMOS6502_NO_ILLEGAL | Tests BCD arithmetic (NMOS behavior) |
| `65c02_decimal_test.hex` | WDC65C02, ROCKWELL65C02 | Tests BCD arithmetic (CMOS behavior) |
| `65c02_extended_opcodes_test.hex` | WDC65C02, ROCKWELL65C02 | Tests 65C02-specific opcodes + BBR/BBS/RMB/SMB |

## Building Test Files

### Prerequisites

Source files and assembler from Klaus Dormann's 6502 test suite:
- Repository: https://github.com/Klaus2m5/6502_65C02_functional_tests
- `as65` - AS65 assembler (included in Dormann's repository)
- `6502_functional_test.a65` - Main functional test
- `6502_decimal_test.a65` - Bruce Clark's decimal mode test
- `65C02_extended_opcodes_test.a65c` - 65C02 extended opcodes test

### Functional Test (6502_functional_test.hex)

Source file: `6502_functional_test.a65`

```bash
as65 -l -m -s2 -w -h0 6502_functional_test.a65
# Keep the .lst file for debugging test failures
```

**Success address:** `$3469`

### Decimal Test

Source file: `6502_decimal_test.a65`

The decimal test requires different settings for NMOS vs CMOS because they handle
BCD arithmetic differently (especially flags and invalid BCD inputs).

For NMOS 6502 (`6502_decimal_test.hex`):
```asm
; Assembly settings for NMOS 6502
cputype = 0         ; 6502
vld_bcd = 0         ; allow invalid BCD (test quirky NMOS behavior)
chk_a   = 1         ; check accumulator
chk_n   = 1         ; check N flag (set from binary result on NMOS)
chk_v   = 0         ; skip V flag (undefined on NMOS in decimal mode)
chk_z   = 1         ; check Z flag (set from binary result on NMOS)
chk_c   = 1         ; check carry flag
```
```bash
as65 -l -m -s2 -w -h0 6502_decimal_test.a65
# Rename output files:
#   6502_decimal_test.hex (keep this name)
#   6502_decimal_test.lst (keep for debugging)
```

For 65C02 (`65c02_decimal_test.hex`):
```asm
; Assembly settings for 65C02
cputype = 1         ; 65C02
vld_bcd = 0         ; allow invalid BCD (65C02 handles gracefully)
chk_a   = 1         ; check accumulator
chk_n   = 1         ; check N flag (set from BCD result on 65C02)
chk_v   = 1         ; check V flag (defined on 65C02)
chk_z   = 1         ; check Z flag (set from BCD result on 65C02)
chk_c   = 1         ; check carry flag
```
```bash
as65 -l -m -s2 -w -h0 6502_decimal_test.a65
# Rename output files:
#   6502_decimal_test.hex -> 65c02_decimal_test.hex
#   6502_decimal_test.lst -> 65c02_decimal_test.lst (keep for debugging)
```

**Key differences between NMOS and CMOS decimal mode:**

| Behavior | NMOS 6502 | 65C02 |
|----------|-----------|-------|
| N, Z flags | Based on binary result | Based on BCD-corrected result |
| V flag | Undefined (skip test) | Defined (test it) |
| Invalid BCD | Quirky but deterministic | Gracefully corrected |

**Success:** Memory location `$000B` = `$00`

### Extended Opcodes Test

Source file: `65C02_extended_opcodes_test.a65c`

The extended opcodes test has configuration options for different 65C02 variants:

- `wdc_op` - WAI & STP instructions (`0` = test as NOPs, `>0` = skip entirely)
- `rkwl_wdc_op` - BBR/BBS/RMB/SMB bit instructions (`0` = test as NOPs, `1` = full test, `>1` = skip)
- `skip_nop` - Whether to test undefined opcodes as NOPs

For WDC and Rockwell 65C02 (`65c02_extended_opcodes_test.hex`):

Both WDC and Rockwell 65C02 variants use the same test file since they share the
BBR/BBS/RMB/SMB instructions and we skip WAI/STP tests (verified separately by
unit tests and Tom Harte's SingleStepTests).

```asm
; Assembly settings for WDC and Rockwell 65C02
wdc_op      = 1    ; Skip WAI & STP tests (verified by unit tests and Harte SST)
rkwl_wdc_op = 1    ; Full test of BBR/BBS/RMB/SMB
skip_nop    = 0    ; Test undefined opcodes as NOPs
```
```bash
as65 -l -m -s2 -w -x -h0 65C02_extended_opcodes_test.a65c
# Output files (no renaming needed):
#   65C02_extended_opcodes_test.hex (used by both WDC and Rockwell tests)
#   65C02_extended_opcodes_test.lst (keep for debugging)
```

**Key differences between WDC and Rockwell 65C02:**

| Feature | WDC 65C02 | Rockwell 65C02 |
|---------|-----------|----------------|
| WAI (Wait for Interrupt) | Yes | No (NOP) |
| STP (Stop the Clock) | Yes | No (NOP) |

| BBR/BBS (Branch on Bit) | Yes | Yes |
| RMB/SMB (Reset/Set Memory Bit) | Yes | Yes |

## Configuration File

The test runner uses a JSON configuration file (`dormann-tests.json`) to specify:
- Test data directory path
- Hex file names for each test
- Start addresses and success criteria

This allows you to customize settings if you build the tests with different assembly options.

### Success Criteria

Different tests use different methods to determine success:

| Test | Success Criteria | Config Property |
|------|------------------|-----------------|
| Functional Test | PC loops at address | `successAddress` |
| Decimal Tests | Memory location = $00 | `errorAddress` |
| Extended Opcodes Tests | PC loops at address | `successAddress` |

**Finding success addresses:** For tests that use `successAddress`, look at the end of the
`.lst` file after assembling for the final `jmp` instruction that loops on success.
The success address depends on which assembly options were enabled.

**Error address:** For decimal tests, the test writes $00 to the error address on success,
or $01 on failure. This address is typically $000B and doesn't change with assembly options.

### Default Configuration

The default configuration file is created with `--create-config`:

```bash
Pandowdy.Cpu.Dormann-Tests --create-config
```

### Configuration File Format

```json
{
  "testDataPath": "./testdata",
  "functionalTest": {
    "hexFile": "6502_functional_test.hex",
    "startAddress": "0400",
    "successAddress": "3469"
  },
  "nmosDecimalTest": {
    "hexFile": "6502_decimal_test.hex",
    "startAddress": "0200",
    "errorAddress": "000B"
  },
  "cmosDecimalTest": {
    "hexFile": "65c02_decimal_test.hex",
    "startAddress": "0200",
    "errorAddress": "000B"
  },
  "wdcExtendedTest": {
    "hexFile": "65c02_extended_opcodes_test.hex",
    "startAddress": "0400",
    "successAddress": "24F1"
  },
  "rockwellExtendedTest": {
    "hexFile": "65c02_extended_opcodes_test.hex",
    "startAddress": "0400",
    "successAddress": "24F1"
  }
}
```

### Using a Custom Configuration

```bash
# Use a specific config file
Pandowdy.Cpu.Dormann-Tests --config my-config.json

# Combine with test data path
Pandowdy.Cpu.Dormann-Tests C:\testdata --config my-config.json
```

### When to Edit the Configuration

Edit the configuration file if:
- Your test files have different names
- You assembled the functional or extended tests with different settings (which changes the success address)
- Your test data is in a non-standard location

**Note:** The `errorAddress` for decimal tests ($000B) is fixed in the test source and
doesn't need to be changed unless you modify the test source code itself.

## Usage

### Command Line

```bash
# Use default test data path
Pandowdy.Cpu.Dormann-Tests

# Specify test data directory
Pandowdy.Cpu.Dormann-Tests C:\path\to\testdata

# Use custom configuration file
Pandowdy.Cpu.Dormann-Tests --config my-config.json

# Create default configuration file
Pandowdy.Cpu.Dormann-Tests --create-config

# Or use environment variable
set DORMANN_TEST_PATH=C:\path\to\testdata
Pandowdy.Cpu.Dormann-Tests
```

### Interactive Menu

```
╔═══════════════════════════════════════════════════════╗
║  Available Tests:                                     ║
║  1. 6502 Functional Test                              ║
║  2. 6502 Decimal Test                                 ║
║  3. 65C02 Extended Opcodes Test (WDC or Rockwell)     ║
║  A. Run All Tests                                     ║
║  V. Select CPU Variant                                ║
║  M. Show Menu                                         ║
║  Q. Quit                                              ║
╚═══════════════════════════════════════════════════════╝
```

## Test Details

### 6502 Functional Test

- **Author:** Klaus Dormann
- **Start address:** `$0400`
- **Success:** PC loops at `$3469`
- **Failure:** PC loops at any other address (look up in `.lst` file)

Tests all documented 6502 instructions including:
- All addressing modes
- Flag behavior
- Stack operations
- Branching
- Arithmetic (binary mode)

### Decimal Test

- **Author:** Bruce Clark
- **Start address:** `$0200`
- **Success:** Memory `$000B` = `$00`
- **Failure:** Memory `$000B` = `$01`

Exhaustively tests all 131,072 combinations of:
- ADC in decimal mode
- SBC in decimal mode
- All flag states

**Important:** NMOS 6502 and 65C02 have different BCD behavior. Use the correct test file for your CPU variant.

### Extended Opcodes Test

- **Author:** Klaus Dormann
- **Prerequisite:** 6502 Functional Test must pass first
- **Start address:** `$0400`

Tests 65C02-specific instructions common to all variants:
- STZ, TRB, TSB
- BRA (branch always)
- PHX, PHY, PLX, PLY
- New addressing modes (e.g., `(zp)` without index)

Additional instructions tested by variant:

| Instruction | WDC 65C02 | Rockwell 65C02 |
|-------------|-----------|----------------|
| BBR (Branch on Bit Reset) | Tested | Tested |
| BBS (Branch on Bit Set) | Tested | Tested |
| RMB (Reset Memory Bit) | Tested | Tested |
| SMB (Set Memory Bit) | Tested | Tested |
| WAI (Wait for Interrupt) | Skipped* | Skipped* |
| STP (Stop the Clock) | Skipped* | Skipped* |

*WAI and STP are verified by unit tests and Tom Harte's SingleStepTests instead.

## Troubleshooting

### Test fails at unexpected address

1. Look up the failure address in the corresponding `.lst` file
2. The listing shows which specific test case failed
3. Check your CPU implementation for that instruction/addressing mode

### Decimal test fails immediately

This usually means you're using the wrong test binary for your CPU variant:
- NMOS variants need `6502_decimal_test.hex` (cputype=0)
- CMOS variants need `65c02_decimal_test.hex` (cputype=1)

### Test runs forever

The test runner has safety limits (50M-200M instructions). If hit:
- Check that your CPU is making progress (PC should change)
- Verify the test binary loaded correctly
- Ensure your bus implementation returns valid data

## License

This test runner is part of the Pandowdy.Cpu project.

The Dormann test suites are Copyright (c) Klaus Dormann and distributed under their own license terms.
