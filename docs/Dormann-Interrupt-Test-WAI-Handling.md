# Dormann Interrupt Test - WAI Handling Summary

## Overview

The Klaus Dormann interrupt test (`6502_interrupt_test` / `65C02_interrupt_test`) validates IRQ and NMI interrupt handling. For 65C02 variants, it also includes WAI (Wait for Interrupt) instruction tests. However, the WAI tests in Dormann's original test suite are documented as "manual tests" that require a human operator using a machine language monitor to single-step the CPU and manually control interrupt signals.

## The Problem with WAI Tests

The Dormann test ROM has WAI tests located at addresses $071F-$0750. These tests are **unreachable from normal program execution** - the automated portion of the test completes at address $0719 with an infinite loop (`jmp *`). The WAI tests exist in the ROM but there's no code path that jumps to them.

This is intentional in the original design - Dormann expected a human to:

1. Run the automated tests until $0719
2. Manually set PC to $071F using a monitor
3. Single-step through WAI instructions
4. Manually assert/deassert IRQ to wake the CPU from WAI state
5. Verify correct behavior at each step

## How the Testbed Automates the Manual Work

The test runner (`Program.cs`) and `InterruptTestBus` work together to automate what would normally require a machine language monitor:

### 1. Feedback Register at $BFFC

`InterruptTestBus` implements a hardware feedback register at address $BFFC that the test ROM uses to control interrupts:

- Bit 0: IRQ signal (1 = assert IRQ)
- Bit 1: NMI signal (1 = assert NMI, edge-triggered)
- Bit 7: Filtered out (reserved)

When the test ROM writes to $BFFC, the bus captures the value. The test runner reads this register each cycle and calls `cpu.SignalIrq()`, `cpu.ClearIrq()`, or `cpu.SignalNmi()` accordingly.

### 2. Detecting Automated Test Completion and Jumping to WAI Tests

```csharp
const ushort AutomatedTestSuccessAddress = 0x0719;
const ushort WaiTestStartAddress = 0x071F;
const ushort WaiTestSuccessAddress = 0x0750;

// When PC reaches $0719 (automated tests done), manually jump to WAI tests
if (isCmos && !waiTestsStarted && currentPC == AutomatedTestSuccessAddress)
{
    Console.WriteLine($"    Automated tests passed at ${AutomatedTestSuccessAddress:X4}, jumping to WAI tests...");
    cpuBuffer.Current.PC = WaiTestStartAddress;  // <-- Manual PC modification
    waiTestsStarted = true;
    continue;
}
```

This simulates a monitor user typing something like `G 071F` to jump execution to the WAI test section.

### 3. Waking the CPU from WAI State

When the CPU executes WAI, it enters `CpuStatus.Waiting` and halts until an interrupt arrives. The test runner detects this and automatically signals IRQ:

```csharp
if (cpuBuffer.Current.Status == CpuStatus.Waiting)
{
    cpu.SignalIrq();                    // Wake up from WAI
    cpu.HandlePendingInterrupt(bus);    // Process the interrupt immediately
}
```

This simulates a monitor user manually toggling the IRQ line to wake the CPU.

### 4. Interrupt Signal Processing (Before Each Instruction)

The main loop processes the feedback register before each instruction:

```csharp
byte currentFeedback = bus.FeedbackRegister;

// IRQ is level-triggered
if ((currentFeedback & 0x01) != 0)
    cpu.SignalIrq();
else
    cpu.ClearIrq();

// NMI is edge-triggered (only on rising edge)
if ((prevFeedback & 0x02) == 0 && (currentFeedback & 0x02) != 0)
    cpu.SignalNmi();

prevFeedback = currentFeedback;

// Handle pending interrupts at instruction boundary
cpu.HandlePendingInterrupt(bus);
```

## Success Criteria

| Variant | Success Address | Notes |
|---------|-----------------|-------|
| NMOS 6502 | $06F5 | No WAI tests (NMOS doesn't have WAI instruction) |
| 65C02 variants | $0750 | After both automated tests ($0719) AND WAI tests complete |

## Key Files

| File | Purpose |
|------|---------|
| `Program.cs` | `RunInterruptTest()` method contains all the automation logic |
| `InterruptTestBus.cs` | Custom bus that implements the $BFFC feedback register |
| `TestConfig.cs` | Configuration for test file paths and addresses |

## Debugging Tips After Refactor

1. **WAI tests aren't running**: Check that `waiTestsStarted` flag is being set and PC is being modified to $071F
2. **CPU hangs in WAI state**: Verify `CpuStatus.Waiting` is being detected and `SignalIrq()` is being called
3. **IRQ/NMI aren't firing**: Check `InterruptTestBus.FeedbackRegister` is being read correctly
4. **NMI not triggering**: NMI is edge-triggered - only call `SignalNmi()` on 0->1 transition of bit 1
5. **IRQ behavior incorrect**: IRQ is level-triggered - call `SignalIrq()` while bit 0 is high, `ClearIrq()` when low
