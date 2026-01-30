// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.Cpu.Internals;
using Xunit;

namespace Pandowdy.Cpu.Tests;

/// <summary>
/// Additional edge case tests for interrupt handling.
/// Covers scenarios like WAI instruction, interrupt timing, and status transitions.
/// </summary>
public class InterruptEdgeCaseTests : CpuTestBase
{
    protected override CpuVariant Variant => CpuVariant.WDC65C02;

    #region IRQ While Waiting Tests

    [Fact]
    public void IRQ_WhileWaiting_WithIFlagSet_StillHandled()
    {
        // WAI allows IRQ to wake CPU even when I flag is set
        SetupCpu();
        Bus.SetIrqVector(0x8000);
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Current.Status = CpuStatus.Waiting;
        CpuBuffer.Current.InterruptDisableFlag = true;
        CpuBuffer.Current.SignalIrq();

        bool handled = CpuBuffer.Current.HandlePendingInterrupt(Bus);

        Assert.True(handled);
        Assert.Equal(0x8000, CpuBuffer.Current.PC);
        Assert.Equal(CpuStatus.Running, CpuBuffer.Current.Status);
    }

    [Fact]
    public void IRQ_WhileWaiting_WithIFlagClear_Handled()
    {
        SetupCpu();
        Bus.SetIrqVector(0x8000);
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Current.Status = CpuStatus.Waiting;
        CpuBuffer.Current.InterruptDisableFlag = false;
        CpuBuffer.Current.SignalIrq();

        bool handled = CpuBuffer.Current.HandlePendingInterrupt(Bus);

        Assert.True(handled);
        Assert.Equal(0x8000, CpuBuffer.Current.PC);
        Assert.Equal(CpuStatus.Running, CpuBuffer.Current.Status);
    }

    [Fact]
    public void IRQ_WhileRunning_WithIFlagSet_NotHandled()
    {
        SetupCpu();
        Bus.SetIrqVector(0x8000);
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Current.Status = CpuStatus.Running;
        CpuBuffer.Current.InterruptDisableFlag = true;
        CpuBuffer.Current.PC = 0x0400;
        CpuBuffer.Current.SignalIrq();

        bool handled = CpuBuffer.Current.HandlePendingInterrupt(Bus);

        Assert.False(handled);
        Assert.Equal(0x0400, CpuBuffer.Current.PC); // PC unchanged
        Assert.Equal(PendingInterrupt.Irq, CpuBuffer.Current.PendingInterrupt); // Still pending
    }

    #endregion

    #region NMI While Waiting Tests

    [Fact]
    public void NMI_WhileWaiting_Handled()
    {
        SetupCpu();
        Bus.SetNmiVector(0x9000);
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Current.Status = CpuStatus.Waiting;
        CpuBuffer.Current.SignalNmi();

        bool handled = CpuBuffer.Current.HandlePendingInterrupt(Bus);

        Assert.True(handled);
        Assert.Equal(0x9000, CpuBuffer.Current.PC);
        Assert.Equal(CpuStatus.Running, CpuBuffer.Current.Status);
    }

    [Fact]
    public void NMI_WhileWaiting_WithIFlagSet_StillHandled()
    {
        // NMI ignores I flag
        SetupCpu();
        Bus.SetNmiVector(0x9000);
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Current.Status = CpuStatus.Waiting;
        CpuBuffer.Current.InterruptDisableFlag = true;
        CpuBuffer.Current.SignalNmi();

        bool handled = CpuBuffer.Current.HandlePendingInterrupt(Bus);

        Assert.True(handled);
        Assert.Equal(0x9000, CpuBuffer.Current.PC);
        Assert.Equal(CpuStatus.Running, CpuBuffer.Current.Status);
    }

    #endregion

    #region Reset While Waiting Tests

    [Fact]
    public void Reset_WhileWaiting_Handled()
    {
        SetupCpu();
        Bus.SetResetVector(0xA000);
        CpuBuffer.Current.Status = CpuStatus.Waiting;
        CpuBuffer.Current.SignalReset();

        bool handled = CpuBuffer.Current.HandlePendingInterrupt(Bus);

        Assert.True(handled);
        Assert.Equal(0xA000, CpuBuffer.Current.PC);
        Assert.Equal(CpuStatus.Running, CpuBuffer.Current.Status);
    }

    #endregion

    #region Interrupt While Stopped/Jammed Tests

    [Fact]
    public void Clock_WhileStopped_DoesNotExecute()
    {
        LoadAndReset([0xA9, 0x42]); // LDA #$42
        CpuBuffer.Current.Status = CpuStatus.Stopped;
        CpuBuffer.Prev.Status = CpuStatus.Stopped;
        ushort originalPC = CpuBuffer.Current.PC;
        byte originalA = CpuBuffer.Current.A;

        Cpu.Clock(Variant, CpuBuffer, Bus);

        Assert.Equal(originalPC, CpuBuffer.Current.PC);
        Assert.Equal(originalA, CpuBuffer.Current.A);
        Assert.Equal(CpuStatus.Stopped, CpuBuffer.Current.Status);
    }

    [Fact]
    public void Clock_WhileJammed_DoesNotExecute()
    {
        LoadAndReset([0xA9, 0x42]); // LDA #$42
        CpuBuffer.Current.Status = CpuStatus.Jammed;
        CpuBuffer.Prev.Status = CpuStatus.Jammed;
        ushort originalPC = CpuBuffer.Current.PC;

        Cpu.Clock(Variant, CpuBuffer, Bus);

        Assert.Equal(originalPC, CpuBuffer.Current.PC);
        Assert.Equal(CpuStatus.Jammed, CpuBuffer.Current.Status);
    }

    [Fact]
    public void Reset_ClearsStoppedStatus()
    {
        SetupCpu();
        CpuBuffer.Current.Status = CpuStatus.Stopped;
        CpuBuffer.Prev.Status = CpuStatus.Stopped;

        Cpu.Reset(CpuBuffer, Bus);

        Assert.Equal(CpuStatus.Running, CpuBuffer.Current.Status);
    }

    [Fact]
    public void Reset_ClearsJammedStatus()
    {
        SetupCpu();
        CpuBuffer.Current.Status = CpuStatus.Jammed;
        CpuBuffer.Prev.Status = CpuStatus.Jammed;

        Cpu.Reset(CpuBuffer, Bus);

        Assert.Equal(CpuStatus.Running, CpuBuffer.Current.Status);
    }

    [Fact]
    public void Reset_ClearsWaitingStatus()
    {
        SetupCpu();
        CpuBuffer.Current.Status = CpuStatus.Waiting;
        CpuBuffer.Prev.Status = CpuStatus.Waiting;

        Cpu.Reset(CpuBuffer, Bus);

        Assert.Equal(CpuStatus.Running, CpuBuffer.Current.Status);
    }

    #endregion

    #region Interrupt Clears Pipeline Tests

    [Fact]
    public void NMI_ClearsPipeline()
    {
        SetupCpu();
        Bus.SetNmiVector(0x9000);
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Current.Pipeline = new MicroOp[5];
        CpuBuffer.Current.PipelineIndex = 3;
        CpuBuffer.Current.SignalNmi();

        CpuBuffer.Current.HandlePendingInterrupt(Bus);

        Assert.Empty(CpuBuffer.Current.Pipeline);
        Assert.Equal(0, CpuBuffer.Current.PipelineIndex);
    }

    [Fact]
    public void IRQ_ClearsPipeline()
    {
        SetupCpu();
        Bus.SetIrqVector(0x8000);
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Current.InterruptDisableFlag = false;
        CpuBuffer.Current.Pipeline = new MicroOp[5];
        CpuBuffer.Current.PipelineIndex = 3;
        CpuBuffer.Current.SignalIrq();

        CpuBuffer.Current.HandlePendingInterrupt(Bus);

        Assert.Empty(CpuBuffer.Current.Pipeline);
        Assert.Equal(0, CpuBuffer.Current.PipelineIndex);
    }

    #endregion

    #region No Interrupt Pending Tests

    [Fact]
    public void HandlePendingInterrupt_ReturnsFlase_WhenNoPendingInterrupt()
    {
        SetupCpu();
        CpuBuffer.Current.PendingInterrupt = PendingInterrupt.None;

        bool handled = CpuBuffer.Current.HandlePendingInterrupt(Bus);

        Assert.False(handled);
    }

    [Fact]
    public void ClearIrq_DoesNothing_WhenNoIrqPending()
    {
        SetupCpu();
        CpuBuffer.Current.PendingInterrupt = PendingInterrupt.None;

        CpuBuffer.Current.ClearIrq();

        Assert.Equal(PendingInterrupt.None, CpuBuffer.Current.PendingInterrupt);
    }

    [Fact]
    public void ClearIrq_DoesNotClearReset()
    {
        SetupCpu();
        CpuBuffer.Current.SignalReset();

        CpuBuffer.Current.ClearIrq();

        Assert.Equal(PendingInterrupt.Reset, CpuBuffer.Current.PendingInterrupt);
    }

    #endregion

    #region Stack Behavior During Interrupts

    [Fact]
    public void IRQ_DecrementsStackBy3()
    {
        SetupCpu();
        Bus.SetIrqVector(0x8000);
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Current.InterruptDisableFlag = false;
        CpuBuffer.Current.SignalIrq();

        CpuBuffer.Current.HandlePendingInterrupt(Bus);

        Assert.Equal(0xFC, CpuBuffer.Current.SP); // 0xFF - 3 = 0xFC
    }

    [Fact]
    public void NMI_DecrementsStackBy3()
    {
        SetupCpu();
        Bus.SetNmiVector(0x9000);
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Current.SignalNmi();

        CpuBuffer.Current.HandlePendingInterrupt(Bus);

        Assert.Equal(0xFC, CpuBuffer.Current.SP);
    }

    [Fact]
    public void IRQ_StackWrapsCorrectly()
    {
        // Test stack wrap when SP is near bottom
        SetupCpu();
        Bus.SetIrqVector(0x8000);
        CpuBuffer.Current.SP = 0x02;
        CpuBuffer.Current.PC = 0x1234;
        CpuBuffer.Current.InterruptDisableFlag = false;
        CpuBuffer.Current.SignalIrq();

        CpuBuffer.Current.HandlePendingInterrupt(Bus);

        // SP wraps: 0x02 -> 0x01 -> 0x00 -> 0xFF
        Assert.Equal(0xFF, CpuBuffer.Current.SP);
        // Verify data was written to correct locations
        Assert.Equal(0x12, Bus.Memory[0x0102]); // PCH
        Assert.Equal(0x34, Bus.Memory[0x0101]); // PCL
    }

    #endregion

    #region Interrupt Vector Reading

    [Fact]
    public void IRQ_ReadsVectorFromCorrectAddress()
    {
        SetupCpu();
        Bus.Memory[0xFFFE] = 0x34;
        Bus.Memory[0xFFFF] = 0x12;
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Current.InterruptDisableFlag = false;
        CpuBuffer.Current.SignalIrq();

        CpuBuffer.Current.HandlePendingInterrupt(Bus);

        Assert.Equal(0x1234, CpuBuffer.Current.PC);
    }

    [Fact]
    public void NMI_ReadsVectorFromCorrectAddress()
    {
        SetupCpu();
        Bus.Memory[0xFFFA] = 0x78;
        Bus.Memory[0xFFFB] = 0x56;
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Current.SignalNmi();

        CpuBuffer.Current.HandlePendingInterrupt(Bus);

        Assert.Equal(0x5678, CpuBuffer.Current.PC);
    }

    [Fact]
    public void Reset_ReadsVectorFromCorrectAddress()
    {
        SetupCpu();
        Bus.Memory[0xFFFC] = 0xBC;
        Bus.Memory[0xFFFD] = 0x9A;
        CpuBuffer.Current.SignalReset();

        CpuBuffer.Current.HandlePendingInterrupt(Bus);

        Assert.Equal(0x9ABC, CpuBuffer.Current.PC);
    }

    #endregion

    #region Status Push Behavior

    [Fact]
    public void IRQ_PushesStatus_WithUFlagSet_BFlagClear()
    {
        SetupCpu();
        Bus.SetIrqVector(0x8000);
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Current.P = 0x00; // Clear all flags
        CpuBuffer.Current.InterruptDisableFlag = false;
        CpuBuffer.Current.SignalIrq();

        CpuBuffer.Current.HandlePendingInterrupt(Bus);

        byte pushedP = Bus.Memory[0x01FD];
        Assert.True((pushedP & CpuState.FlagU) != 0, "U flag should be set in pushed status");
        Assert.False((pushedP & CpuState.FlagB) != 0, "B flag should be clear for hardware interrupt");
    }

    [Fact]
    public void NMI_PushesStatus_WithUFlagSet_BFlagClear()
    {
        SetupCpu();
        Bus.SetNmiVector(0x9000);
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Current.P = 0x00;
        CpuBuffer.Current.SignalNmi();

        CpuBuffer.Current.HandlePendingInterrupt(Bus);

        byte pushedP = Bus.Memory[0x01FD];
        Assert.True((pushedP & CpuState.FlagU) != 0, "U flag should be set");
        Assert.False((pushedP & CpuState.FlagB) != 0, "B flag should be clear for NMI");
    }

    [Fact]
    public void IRQ_PreservesOtherFlagsInPushedStatus()
    {
        SetupCpu();
        Bus.SetIrqVector(0x8000);
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Current.P = CpuState.FlagC | CpuState.FlagZ | CpuState.FlagN; // Set C, Z, N
        CpuBuffer.Current.InterruptDisableFlag = false;
        CpuBuffer.Current.SignalIrq();

        CpuBuffer.Current.HandlePendingInterrupt(Bus);

        byte pushedP = Bus.Memory[0x01FD];
        Assert.True((pushedP & CpuState.FlagC) != 0, "C flag should be preserved");
        Assert.True((pushedP & CpuState.FlagZ) != 0, "Z flag should be preserved");
        Assert.True((pushedP & CpuState.FlagN) != 0, "N flag should be preserved");
    }

    #endregion

    #region Bypassed Status Tests

    [Fact]
    public void Clock_ContinuesExecution_WhenBypassed()
    {
        LoadAndReset([0xA9, 0x42]); // LDA #$42
        CpuBuffer.Current.Status = CpuStatus.Bypassed;
        CpuBuffer.Prev.Status = CpuStatus.Bypassed;

        int cycles = StepInstruction();

        Assert.Equal(2, cycles);
        Assert.Equal(0x42, CurrentState.A);
    }

    [Fact]
    public void Bypassed_StatusPersists_AfterInstruction()
    {
        LoadAndReset([0xA9, 0x42]); // LDA #$42
        CpuBuffer.Current.Status = CpuStatus.Bypassed;
        CpuBuffer.Prev.Status = CpuStatus.Bypassed;

        StepInstruction();

        // Bypassed status should persist (not automatically cleared)
        Assert.Equal(CpuStatus.Bypassed, CpuBuffer.Current.Status);
    }

    #endregion
}
