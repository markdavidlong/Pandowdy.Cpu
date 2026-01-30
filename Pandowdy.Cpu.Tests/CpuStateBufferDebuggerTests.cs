// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Linq;
using Pandowdy.Cpu.Internals;
using Xunit;

namespace Pandowdy.Cpu.Tests;

/// <summary>
/// Tests for CpuStateBuffer debugger helper properties.
/// These properties help detect what changed during instruction execution.
/// Note: These properties compare Prev vs Current, so we test them by directly
/// manipulating state rather than executing instructions (which resets state after completion).
/// </summary>
public class CpuStateBufferDebuggerTests : CpuTestBase
{
    protected override CpuVariant Variant => CpuVariant.WDC65C02;

    #region PcChanged Tests

    [Fact]
    public void PcChanged_ReturnsTrue_WhenPCDiffers()
    {
        SetupCpu();
        CpuBuffer.Prev.PC = 0x0400;
        CpuBuffer.Current.PC = 0x0402;

        Assert.True(CpuBuffer.PcChanged);
    }

    [Fact]
    public void PcChanged_ReturnsFalse_WhenPCSame()
    {
        SetupCpu();
        CpuBuffer.Prev.PC = 0x0400;
        CpuBuffer.Current.PC = 0x0400;

        Assert.False(CpuBuffer.PcChanged);
    }

    [Fact]
    public void PcChanged_DuringExecution_ReturnsTrue()
    {
        // Execute partial instruction to see PC change during execution
        LoadAndReset([0xAD, 0x34, 0x12]); // LDA $1234 (4 cycles)
        Cpu.Clock(Variant, CpuBuffer, Bus); // First cycle advances PC

        Assert.True(CpuBuffer.PcChanged);
    }

    #endregion

    #region JumpOccurred Tests

    [Fact]
    public void JumpOccurred_ReturnsFalse_WhenInstructionNotComplete()
    {
        LoadAndReset([0xAD, 0x34, 0x12]); // LDA $1234 (4 cycles)
        Cpu.Clock(Variant, CpuBuffer, Bus); // Execute 1 cycle

        Assert.False(CpuBuffer.JumpOccurred);
    }

    [Fact]
    public void JumpOccurred_ReturnsFalse_ForSequentialInstruction()
    {
        SetupCpu();
        CpuBuffer.Prev.PC = 0x0400;
        CpuBuffer.Current.PC = 0x0402; // Sequential (2-byte instruction)
        CpuBuffer.Current.InstructionComplete = true;
        CpuBuffer.Current.Pipeline = new MicroOp[2];

        Assert.False(CpuBuffer.JumpOccurred);
    }

    [Fact]
    public void JumpOccurred_ReturnsTrue_ForNonSequentialPC()
    {
        SetupCpu();
        CpuBuffer.Prev.PC = 0x0400;
        CpuBuffer.Current.PC = 0x0800; // Non-sequential jump
        CpuBuffer.Current.InstructionComplete = true;
        CpuBuffer.Current.Pipeline = new MicroOp[3]; // JMP is 3 bytes

        Assert.True(CpuBuffer.JumpOccurred);
    }

    [Fact]
    public void JumpOccurred_ReturnsTrue_ForJSRTarget()
    {
        SetupCpu();
        CpuBuffer.Prev.PC = 0x0400;
        CpuBuffer.Current.PC = 0x1000; // JSR target
        CpuBuffer.Current.InstructionComplete = true;
        CpuBuffer.Current.Pipeline = new MicroOp[6]; // JSR is 6 cycles

        Assert.True(CpuBuffer.JumpOccurred);
    }

    #endregion

    #region BranchOccurred Tests

    [Fact]
    public void BranchOccurred_ReturnsFalse_WhenInstructionNotComplete()
    {
        SetupCpu();
        CpuBuffer.Current.InstructionComplete = false;

        Assert.False(CpuBuffer.BranchOccurred);
    }

    [Fact]
    public void BranchOccurred_ReturnsFalse_ForNoBranch()
    {
        SetupCpu();
        CpuBuffer.Prev.PC = 0x0400;
        CpuBuffer.Current.PC = 0x0400; // PC unchanged
        CpuBuffer.Current.InstructionComplete = true;

        Assert.False(CpuBuffer.BranchOccurred);
    }

    [Fact]
    public void BranchOccurred_ReturnsTrue_ForSmallForwardJump()
    {
        SetupCpu();
        CpuBuffer.Prev.PC = 0x0400;
        CpuBuffer.Current.PC = 0x0412; // +18 bytes (within branch range)
        CpuBuffer.Current.InstructionComplete = true;

        Assert.True(CpuBuffer.BranchOccurred);
    }

    [Fact]
    public void BranchOccurred_ReturnsTrue_ForSmallBackwardJump()
    {
        SetupCpu();
        CpuBuffer.Prev.PC = 0x0420;
        CpuBuffer.Current.PC = 0x0410; // -16 bytes (within branch range)
        CpuBuffer.Current.InstructionComplete = true;

        Assert.True(CpuBuffer.BranchOccurred);
    }

    [Fact]
    public void BranchOccurred_ReturnsFalse_ForLargeJump()
    {
        // Jumps > 129 bytes are not branch instructions
        SetupCpu();
        CpuBuffer.Prev.PC = 0x0400;
        CpuBuffer.Current.PC = 0x0500; // +256 bytes (outside branch range)
        CpuBuffer.Current.InstructionComplete = true;

        Assert.False(CpuBuffer.BranchOccurred);
    }

    #endregion

    #region ReturnOccurred Tests

    [Fact]
    public void ReturnOccurred_ReturnsFalse_WhenInstructionNotComplete()
    {
        SetupCpu();
        CpuBuffer.Current.InstructionComplete = false;

        Assert.False(CpuBuffer.ReturnOccurred);
    }

    [Fact]
    public void ReturnOccurred_ReturnsFalse_WhenSPNotIncreased()
    {
        SetupCpu();
        CpuBuffer.Prev.SP = 0xFD;
        CpuBuffer.Current.SP = 0xFD;
        CpuBuffer.Prev.PC = 0x0400;
        CpuBuffer.Current.PC = 0x0800;
        CpuBuffer.Current.InstructionComplete = true;

        Assert.False(CpuBuffer.ReturnOccurred);
    }

    [Fact]
    public void ReturnOccurred_ReturnsFalse_WhenPCNotChanged()
    {
        SetupCpu();
        CpuBuffer.Prev.SP = 0xFC;
        CpuBuffer.Current.SP = 0xFD; // SP increased
        CpuBuffer.Prev.PC = 0x0400;
        CpuBuffer.Current.PC = 0x0400; // PC not changed
        CpuBuffer.Current.InstructionComplete = true;

        Assert.False(CpuBuffer.ReturnOccurred);
    }

    [Fact]
    public void ReturnOccurred_ReturnsTrue_ForRTSPattern()
    {
        // RTS: SP increases by 2, PC changes
        SetupCpu();
        CpuBuffer.Prev.SP = 0xFC;
        CpuBuffer.Current.SP = 0xFE; // +2
        CpuBuffer.Prev.PC = 0x0400;
        CpuBuffer.Current.PC = 0x0800; // Return address
        CpuBuffer.Current.InstructionComplete = true;

        Assert.True(CpuBuffer.ReturnOccurred);
    }

    [Fact]
    public void ReturnOccurred_ReturnsTrue_ForRTIPattern()
    {
        // RTI: SP increases by 3, PC changes
        SetupCpu();
        CpuBuffer.Prev.SP = 0xFC;
        CpuBuffer.Current.SP = 0xFF; // +3
        CpuBuffer.Prev.PC = 0x0400;
        CpuBuffer.Current.PC = 0x0800; // Return address
        CpuBuffer.Current.InstructionComplete = true;

        Assert.True(CpuBuffer.ReturnOccurred);
    }

    #endregion

    #region InterruptOccurred Tests

    [Fact]
    public void InterruptOccurred_ReturnsFalse_WhenInstructionNotComplete()
    {
        SetupCpu();
        CpuBuffer.Current.InstructionComplete = false;

        Assert.False(CpuBuffer.InterruptOccurred);
    }

    [Fact]
    public void InterruptOccurred_ReturnsFalse_WhenStackNotDecremented()
    {
        SetupCpu();
        CpuBuffer.Prev.SP = 0xFD;
        CpuBuffer.Current.SP = 0xFD;
        CpuBuffer.Current.InterruptDisableFlag = true;
        CpuBuffer.Current.InstructionComplete = true;

        Assert.False(CpuBuffer.InterruptOccurred);
    }

    [Fact]
    public void InterruptOccurred_ReturnsFalse_WhenIFlagNotSet()
    {
        SetupCpu();
        CpuBuffer.Prev.SP = 0xFF;
        CpuBuffer.Current.SP = 0xFC; // -3
        CpuBuffer.Current.InterruptDisableFlag = false;
        CpuBuffer.Current.InstructionComplete = true;

        Assert.False(CpuBuffer.InterruptOccurred);
    }

    [Fact]
    public void InterruptOccurred_ReturnsTrue_ForInterruptPattern()
    {
        // Interrupt: SP decreases by 3, I flag set
        SetupCpu();
        CpuBuffer.Prev.SP = 0xFF;
        CpuBuffer.Current.SP = 0xFC; // -3
        CpuBuffer.Current.InterruptDisableFlag = true;
        CpuBuffer.Current.InstructionComplete = true;

        Assert.True(CpuBuffer.InterruptOccurred);
    }

    [Fact]
    public void InterruptOccurred_ReturnsTrue_ForBRKPattern()
    {
        // BRK also pushes 3 bytes and sets I flag
        SetupCpu();
        CpuBuffer.Prev.SP = 0xFD;
        CpuBuffer.Current.SP = 0xFA; // -3
        CpuBuffer.Current.InterruptDisableFlag = true;
        CpuBuffer.Current.InstructionComplete = true;

        Assert.True(CpuBuffer.InterruptOccurred);
    }

    #endregion

    #region PageCrossed Tests

    [Fact]
    public void PageCrossed_ReturnsFalse_WhenSamePage()
    {
        SetupCpu();
        CpuBuffer.Prev.TempAddress = 0x1200;
        CpuBuffer.Current.TempAddress = 0x12FF;

        Assert.False(CpuBuffer.PageCrossed);
    }

    [Fact]
    public void PageCrossed_ReturnsTrue_WhenDifferentPage()
    {
        SetupCpu();
        CpuBuffer.Prev.TempAddress = 0x12FF;
        CpuBuffer.Current.TempAddress = 0x1300;

        Assert.True(CpuBuffer.PageCrossed);
    }

    [Fact]
    public void PageCrossed_ReturnsTrue_ForPageBoundary()
    {
        SetupCpu();
        CpuBuffer.Prev.TempAddress = 0x00FF;
        CpuBuffer.Current.TempAddress = 0x0100;

        Assert.True(CpuBuffer.PageCrossed);
    }

    [Fact]
    public void PageCrossed_ReturnsTrue_ForLargePageChange()
    {
        SetupCpu();
        CpuBuffer.Prev.TempAddress = 0x1000;
        CpuBuffer.Current.TempAddress = 0x2000;

        Assert.True(CpuBuffer.PageCrossed);
    }

    #endregion

    #region StackActivityOccurred Tests

    [Fact]
    public void StackActivityOccurred_ReturnsFalse_WhenSPUnchanged()
    {
        SetupCpu();
        CpuBuffer.Prev.SP = 0xFD;
        CpuBuffer.Current.SP = 0xFD;

        Assert.False(CpuBuffer.StackActivityOccurred);
    }

    [Fact]
    public void StackActivityOccurred_ReturnsTrue_WhenSPDecreased()
    {
        SetupCpu();
        CpuBuffer.Prev.SP = 0xFD;
        CpuBuffer.Current.SP = 0xFC;

        Assert.True(CpuBuffer.StackActivityOccurred);
    }

    [Fact]
    public void StackActivityOccurred_ReturnsTrue_WhenSPIncreased()
    {
        SetupCpu();
        CpuBuffer.Prev.SP = 0xFC;
        CpuBuffer.Current.SP = 0xFD;

        Assert.True(CpuBuffer.StackActivityOccurred);
    }

    #endregion

    #region StackDelta Tests

    [Fact]
    public void StackDelta_ReturnsZero_WhenSPUnchanged()
    {
        SetupCpu();
        CpuBuffer.Prev.SP = 0xFD;
        CpuBuffer.Current.SP = 0xFD;

        Assert.Equal(0, CpuBuffer.StackDelta);
    }

    [Fact]
    public void StackDelta_ReturnsNegative1_ForPush()
    {
        SetupCpu();
        CpuBuffer.Prev.SP = 0xFD;
        CpuBuffer.Current.SP = 0xFC;

        Assert.Equal(-1, CpuBuffer.StackDelta);
    }

    [Fact]
    public void StackDelta_ReturnsPositive1_ForPull()
    {
        SetupCpu();
        CpuBuffer.Prev.SP = 0xFC;
        CpuBuffer.Current.SP = 0xFD;

        Assert.Equal(1, CpuBuffer.StackDelta);
    }

    [Fact]
    public void StackDelta_ReturnsNegative2_ForJSR()
    {
        SetupCpu();
        CpuBuffer.Prev.SP = 0xFD;
        CpuBuffer.Current.SP = 0xFB;

        Assert.Equal(-2, CpuBuffer.StackDelta);
    }

    [Fact]
    public void StackDelta_ReturnsPositive2_ForRTS()
    {
        SetupCpu();
        CpuBuffer.Prev.SP = 0xFB;
        CpuBuffer.Current.SP = 0xFD;

        Assert.Equal(2, CpuBuffer.StackDelta);
    }

    [Fact]
    public void StackDelta_ReturnsNegative3_ForInterrupt()
    {
        SetupCpu();
        CpuBuffer.Prev.SP = 0xFF;
        CpuBuffer.Current.SP = 0xFC;

        Assert.Equal(-3, CpuBuffer.StackDelta);
    }

    [Fact]
    public void StackDelta_ReturnsPositive3_ForRTI()
    {
        SetupCpu();
        CpuBuffer.Prev.SP = 0xFC;
        CpuBuffer.Current.SP = 0xFF;

        Assert.Equal(3, CpuBuffer.StackDelta);
    }

    #endregion

    #region ChangedRegisters Tests

    [Fact]
    public void ChangedRegisters_ReturnsEmpty_WhenNothingChanged()
    {
        SetupCpu();
        CpuBuffer.Current.CopyFrom(CpuBuffer.Prev);

        var changed = CpuBuffer.ChangedRegisters.ToList();

        Assert.Empty(changed);
    }

    [Fact]
    public void ChangedRegisters_ReturnsA_WhenAccumulatorChanged()
    {
        SetupCpu();
        CpuBuffer.Prev.A = 0x00;
        CpuBuffer.Current.A = 0x42;

        var changed = CpuBuffer.ChangedRegisters.ToList();

        Assert.Contains("A", changed);
    }

    [Fact]
    public void ChangedRegisters_ReturnsX_WhenXChanged()
    {
        SetupCpu();
        CpuBuffer.Prev.X = 0x00;
        CpuBuffer.Current.X = 0x10;

        var changed = CpuBuffer.ChangedRegisters.ToList();

        Assert.Contains("X", changed);
    }

    [Fact]
    public void ChangedRegisters_ReturnsY_WhenYChanged()
    {
        SetupCpu();
        CpuBuffer.Prev.Y = 0x00;
        CpuBuffer.Current.Y = 0x20;

        var changed = CpuBuffer.ChangedRegisters.ToList();

        Assert.Contains("Y", changed);
    }

    [Fact]
    public void ChangedRegisters_ReturnsP_WhenStatusChanged()
    {
        SetupCpu();
        CpuBuffer.Prev.P = 0x00;
        CpuBuffer.Current.P = 0x01;

        var changed = CpuBuffer.ChangedRegisters.ToList();

        Assert.Contains("P", changed);
    }

    [Fact]
    public void ChangedRegisters_ReturnsSP_WhenStackPointerChanged()
    {
        SetupCpu();
        CpuBuffer.Prev.SP = 0xFF;
        CpuBuffer.Current.SP = 0xFE;

        var changed = CpuBuffer.ChangedRegisters.ToList();

        Assert.Contains("SP", changed);
    }

    [Fact]
    public void ChangedRegisters_ReturnsPC_WhenProgramCounterChanged()
    {
        SetupCpu();
        CpuBuffer.Prev.PC = 0x0400;
        CpuBuffer.Current.PC = 0x0402;

        var changed = CpuBuffer.ChangedRegisters.ToList();

        Assert.Contains("PC", changed);
    }

    [Fact]
    public void ChangedRegisters_ReturnsMultiple_WhenMultipleChanged()
    {
        SetupCpu();
        CpuBuffer.Prev.A = 0x00;
        CpuBuffer.Current.A = 0x42;
        CpuBuffer.Prev.PC = 0x0400;
        CpuBuffer.Current.PC = 0x0402;
        CpuBuffer.Prev.P = 0x00;
        CpuBuffer.Current.P = 0x82; // N flag set

        var changed = CpuBuffer.ChangedRegisters.ToList();

        Assert.Contains("A", changed);
        Assert.Contains("PC", changed);
        Assert.Contains("P", changed);
        Assert.Equal(3, changed.Count);
    }

    [Fact]
    public void ChangedRegisters_ReturnsAll_WhenAllChanged()
    {
        SetupCpu();
        CpuBuffer.Prev.A = 0x00;
        CpuBuffer.Current.A = 0x01;
        CpuBuffer.Prev.X = 0x00;
        CpuBuffer.Current.X = 0x01;
        CpuBuffer.Prev.Y = 0x00;
        CpuBuffer.Current.Y = 0x01;
        CpuBuffer.Prev.P = 0x00;
        CpuBuffer.Current.P = 0x01;
        CpuBuffer.Prev.SP = 0xFF;
        CpuBuffer.Current.SP = 0xFE;
        CpuBuffer.Prev.PC = 0x0400;
        CpuBuffer.Current.PC = 0x0402;

        var changed = CpuBuffer.ChangedRegisters.ToList();

        Assert.Equal(6, changed.Count);
        Assert.Contains("A", changed);
        Assert.Contains("X", changed);
        Assert.Contains("Y", changed);
        Assert.Contains("P", changed);
        Assert.Contains("SP", changed);
        Assert.Contains("PC", changed);
    }

    #endregion

    #region Integration Tests - During Instruction Execution

    [Fact]
    public void DuringExecution_CanDetectChanges()
    {
        // Execute partial instruction to observe state differences
        LoadAndReset([0xA9, 0x42]); // LDA #$42
        Cpu.Clock(Variant, CpuBuffer, Bus); // First cycle - fetch opcode

        // During execution, PC should have changed
        Assert.True(CpuBuffer.PcChanged);
    }

    [Fact]
    public void DuringExecution_PageCrossDetection()
    {
        // Set up for page crossing detection
        LoadAndReset([0xBD, 0xF0, 0x12]); // LDA $12F0,X
        CpuBuffer.Current.X = 0x20;
        CpuBuffer.Prev.X = 0x20;
        SetMemory(0x1310, 0x42);

        // Execute through the address calculation cycles
        Cpu.Clock(Variant, CpuBuffer, Bus); // Cycle 1: Fetch opcode
        Cpu.Clock(Variant, CpuBuffer, Bus); // Cycle 2: Fetch low address
        Cpu.Clock(Variant, CpuBuffer, Bus); // Cycle 3: Fetch high address + add X

        // At this point, TempAddress should show page crossing
        Assert.True(CpuBuffer.PageCrossed);
    }

    #endregion
}

