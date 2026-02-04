// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Xunit;

namespace Pandowdy.Cpu.Tests;

/// <summary>
/// Tests for interrupt handling: IRQ, NMI, Reset, and interrupt priorities.
/// Uses WDC65C02 as the default variant for interrupt testing.
/// </summary>
public class InterruptTests : CpuTestBase
{
    protected override CpuVariant Variant => CpuVariant.Wdc65C02;

    #region Reset Tests

    [Fact]
    public void Reset_LoadsPCFromResetVector()
    {
        SetupCpu();
        Bus.SetResetVector(0x8000);

        Cpu.Reset(Bus);

        Assert.Equal(0x8000, CurrentState.PC);
        Assert.Equal(0x8000, CurrentState.PC);
    }

    [Fact]
    public void Reset_InitializesRegistersToDefault()
    {
        SetupCpu();
        CurrentState.A = 0xFF;
        CurrentState.X = 0xFF;
        CurrentState.Y = 0xFF;

        Cpu.Reset(Bus);

        Assert.Equal(0, CurrentState.A);
        Assert.Equal(0, CurrentState.X);
        Assert.Equal(0, CurrentState.Y);
    }

    [Fact]
    public void Reset_SetsStackPointerTo0xFD()
    {
        SetupCpu();
        CurrentState.SP = 0x00;

        Cpu.Reset(Bus);

        Assert.Equal(0xFD, CurrentState.SP);
    }

    [Fact]
    public void Reset_SetsInterruptDisableFlag()
    {
        SetupCpu();
        CurrentState.InterruptDisableFlag = false;

        Cpu.Reset(Bus);

        Assert.True(CurrentState.InterruptDisableFlag);
    }

    [Fact]
    public void Reset_ClearsPendingInterrupts()
    {
        SetupCpu();
        Cpu.SignalNmi();

        Cpu.Reset(Bus);

        Assert.Equal(PendingInterrupt.None, CurrentState.PendingInterrupt);
    }

    [Fact]
    public void Reset_SetsStatusToRunning()
    {
        SetupCpu();
        CurrentState.Status = CpuStatus.Stopped;

        Cpu.Reset(Bus);

        Assert.Equal(CpuStatus.Running, CurrentState.Status);
    }

    #endregion

    #region IRQ Tests

    [Fact]
    public void IRQ_IsIgnoredWhenInterruptDisabled()
    {
        SetupCpu();
        Bus.SetIrqVector(0x8000);
        Bus.LoadProgram(ProgramStart, [0xEA, 0xEA]); // NOP NOP
        CurrentState.InterruptDisableFlag = true;
        CurrentState.InterruptDisableFlag = true;
        Cpu.SignalIrq();

        StepInstruction();

        Assert.NotEqual(0x8000, CurrentState.PC);
    }

    [Fact]
    public void IRQ_IsHandledWhenInterruptEnabled()
    {
        SetupCpu();
        Bus.SetIrqVector(0x8000);
        Bus.LoadProgram(ProgramStart, [0xEA, 0xEA]); // NOP NOP
        Bus.Memory[0x8000] = 0xEA; // NOP at handler

        CurrentState.InterruptDisableFlag = false;
        CurrentState.InterruptDisableFlag = false;

        StepInstruction();
        Cpu.SignalIrq();

        bool handled = Cpu.HandlePendingInterrupt(Bus);

        Assert.True(handled);
        Assert.Equal(0x8000, CurrentState.PC);
    }

    [Fact]
    public void IRQ_PushesReturnAddressAndStatus()
    {
        SetupCpu();
        Bus.SetIrqVector(0x8000);
        CurrentState.PC = 0x1234;
        CurrentState.P = 0x00;
        CurrentState.SP = 0xFF;
        CurrentState.InterruptDisableFlag = false;
        Cpu.SignalIrq();

        Cpu.HandlePendingInterrupt(Bus);

        Assert.Equal(0xFC, CurrentState.SP);
        Assert.Equal(0x12, Bus.Memory[0x01FF]);
        Assert.Equal(0x34, Bus.Memory[0x01FE]);
    }

    [Fact]
    public void IRQ_SetsInterruptDisableFlag()
    {
        SetupCpu();
        Bus.SetIrqVector(0x8000);
        CurrentState.SP = 0xFF;
        CurrentState.InterruptDisableFlag = false;
        Cpu.SignalIrq();

        Cpu.HandlePendingInterrupt(Bus);

        Assert.True(CurrentState.InterruptDisableFlag);
    }

    [Fact]
    public void IRQ_PushesStatusWithBFlagClear()
    {
        SetupCpu();
        Bus.SetIrqVector(0x8000);
        CurrentState.SP = 0xFF;
        CurrentState.P = 0xFF;
        CurrentState.InterruptDisableFlag = false;
        Cpu.SignalIrq();

        Cpu.HandlePendingInterrupt(Bus);

        byte pushedP = Bus.Memory[0x01FD];
        Assert.True((pushedP & CpuState.FlagU) != 0);
        Assert.False((pushedP & CpuState.FlagB) != 0);
    }

    [Fact]
    public void ClearIrq_ClearsPendingIrq()
    {
        SetupCpu();
        Cpu.SignalIrq();

        Cpu.ClearIrq();

        Assert.Equal(PendingInterrupt.None, CurrentState.PendingInterrupt);
    }

    [Fact]
    public void IRQ_HandledInWaitingState_EvenIfDisabled()
    {
        SetupCpu();
        Bus.SetIrqVector(0x8000);
        CurrentState.SP = 0xFF;
        CurrentState.Status = CpuStatus.Waiting;
        CurrentState.InterruptDisableFlag = true;
        Cpu.SignalIrq();

        bool handled = Cpu.HandlePendingInterrupt(Bus);

        Assert.True(handled);
        Assert.Equal(CpuStatus.Running, CurrentState.Status);
    }

    #endregion

    #region NMI Tests

    [Fact]
    public void NMI_IsHandledEvenWhenInterruptDisabled()
    {
        SetupCpu();
        Bus.SetNmiVector(0x9000);
        CurrentState.PC = 0x1234;
        CurrentState.SP = 0xFF;
        CurrentState.InterruptDisableFlag = true;
        Cpu.SignalNmi();

        bool handled = Cpu.HandlePendingInterrupt(Bus);

        Assert.True(handled);
        Assert.Equal(0x9000, CurrentState.PC);
    }

    [Fact]
    public void NMI_PushesReturnAddressAndStatus()
    {
        SetupCpu();
        Bus.SetNmiVector(0x9000);
        CurrentState.PC = 0x1234;
        CurrentState.P = 0x00;
        CurrentState.SP = 0xFF;
        Cpu.SignalNmi();

        Cpu.HandlePendingInterrupt(Bus);

        Assert.Equal(0xFC, CurrentState.SP);
        Assert.Equal(0x12, Bus.Memory[0x01FF]);
        Assert.Equal(0x34, Bus.Memory[0x01FE]);
    }

    [Fact]
    public void NMI_SetsInterruptDisableFlag()
    {
        SetupCpu();
        Bus.SetNmiVector(0x9000);
        CurrentState.SP = 0xFF;
        CurrentState.InterruptDisableFlag = false;
        Cpu.SignalNmi();

        Cpu.HandlePendingInterrupt(Bus);

        Assert.True(CurrentState.InterruptDisableFlag);
    }

    [Fact]
    public void NMI_ResumesFromWaitingState()
    {
        SetupCpu();
        Bus.SetNmiVector(0x9000);
        CurrentState.SP = 0xFF;
        CurrentState.Status = CpuStatus.Waiting;
        Cpu.SignalNmi();

        Cpu.HandlePendingInterrupt(Bus);

        Assert.Equal(CpuStatus.Running, CurrentState.Status);
    }

    [Fact]
    public void NMI_PushesStatusWithBFlagClear()
    {
        SetupCpu();
        Bus.SetNmiVector(0x9000);
        CurrentState.SP = 0xFF;
        CurrentState.P = 0xFF;
        Cpu.SignalNmi();

        Cpu.HandlePendingInterrupt(Bus);

        byte pushedP = Bus.Memory[0x01FD];
        Assert.False((pushedP & CpuState.FlagB) != 0);
    }

    #endregion

    #region Interrupt Priority Tests

    [Fact]
    public void Reset_HasHighestPriority()
    {
        SetupCpu();
        Bus.SetResetVector(0xA000);
        Bus.SetNmiVector(0x9000);
        Bus.SetIrqVector(0x8000);

        CurrentState.SP = 0xFF;
        Cpu.SignalIrq();
        Cpu.SignalNmi();
        Cpu.SignalReset();

        Cpu.HandlePendingInterrupt(Bus);

        Assert.Equal(0xA000, CurrentState.PC);
    }

    [Fact]
    public void NMI_HasPriorityOverIRQ()
    {
        SetupCpu();
        Bus.SetNmiVector(0x9000);
        Bus.SetIrqVector(0x8000);

        CurrentState.SP = 0xFF;
        CurrentState.InterruptDisableFlag = false;
        Cpu.SignalIrq();
        Cpu.SignalNmi();

        Cpu.HandlePendingInterrupt(Bus);

        Assert.Equal(0x9000, CurrentState.PC);
    }

    [Fact]
    public void SignalNmi_OverridesReset()
    {
        SetupCpu();
        Cpu.SignalReset();
        Cpu.SignalNmi();

        Assert.Equal(PendingInterrupt.Nmi, CurrentState.PendingInterrupt);
    }

    [Fact]
    public void SignalIrq_DoesNotOverrideNmi()
    {
        SetupCpu();
        Cpu.SignalNmi();
        Cpu.SignalIrq();

        Assert.Equal(PendingInterrupt.Nmi, CurrentState.PendingInterrupt);
    }

    [Fact]
    public void SignalReset_OverridesNmi()
    {
        SetupCpu();
        Cpu.SignalNmi();
        Cpu.SignalReset();

        Assert.Equal(PendingInterrupt.Reset, CurrentState.PendingInterrupt);
    }

    [Fact]
    public void SignalNmi_OverridesIrq()
    {
        SetupCpu();
        Cpu.SignalIrq();
        Cpu.SignalNmi();

        Assert.Equal(PendingInterrupt.Nmi, CurrentState.PendingInterrupt);
    }

    #endregion

    #region RTI Tests

    [Fact]
    public void RTI_RestoresPC()
    {
        LoadAndReset([0x40]);
        CurrentState.SP = 0xFC;
        CurrentState.SP = 0xFC;
        Bus.Memory[0x01FD] = 0x00;
        Bus.Memory[0x01FE] = 0x34;
        Bus.Memory[0x01FF] = 0x12;

        StepInstruction();

        Assert.Equal(0x1234, CurrentState.PC);
    }

    [Fact]
    public void RTI_RestoresFlags()
    {
        LoadAndReset([0x40]);
        CurrentState.SP = 0xFC;
        CurrentState.SP = 0xFC;
        CurrentState.CarryFlag = false;
        CurrentState.CarryFlag = false;
        Bus.Memory[0x01FD] = CpuState.FlagC | CpuState.FlagZ;
        Bus.Memory[0x01FE] = 0x00;
        Bus.Memory[0x01FF] = 0x00;

        StepInstruction();

        Assert.True(CurrentState.CarryFlag);
        Assert.True(CurrentState.ZeroFlag);
    }

    [Fact]
    public void RTI_ClearsInterruptDisable()
    {
        LoadAndReset([0x40]);
        CurrentState.SP = 0xFC;
        CurrentState.SP = 0xFC;
        CurrentState.InterruptDisableFlag = true;
        CurrentState.InterruptDisableFlag = true;
        Bus.Memory[0x01FD] = 0x00;
        Bus.Memory[0x01FE] = 0x00;
        Bus.Memory[0x01FF] = 0x00;

        StepInstruction();

        Assert.False(CurrentState.InterruptDisableFlag);
    }

    #endregion

    #region WAI/STP Tests (65C02)

    [Fact]
    public void STP_SetsStoppedStatus()
    {
        // STP (0xDB) - 65C02 only
        LoadAndReset([0xDB]);

        StepInstruction(CpuVariant.Wdc65C02);

        Assert.Equal(CpuStatus.Stopped, CurrentState.Status);
    }

    [Fact]
    public void WAI_SetsWaitingStatus()
    {
        // WAI (0xCB) - 65C02 only
        LoadAndReset([0xCB]);

        StepInstruction(CpuVariant.Wdc65C02);

        Assert.Equal(CpuStatus.Waiting, CurrentState.Status);
    }

    [Fact]
    public void STP_Takes3Cycles()
    {
        LoadAndReset([0xDB]);

        int cycles = StepInstruction(CpuVariant.Wdc65C02);

        Assert.Equal(3, cycles);
    }

    [Fact]
    public void WAI_Takes3Cycles()
    {
        LoadAndReset([0xCB]);

        int cycles = StepInstruction(CpuVariant.Wdc65C02);

        Assert.Equal(3, cycles);
    }

    [Fact]
    public void WAI_Integration_CpuStaysHalted_UntilIRQ_ThenResumes()
    {
        // WAI ($CB) followed by LDA #$42 at the next instruction
        // ISR at $8000: LDA #$99, RTI
        LoadAndReset([0xCB, 0xA9, 0x42]);
        Bus.SetIrqVector(0x8000);
        Bus.Memory[0x8000] = 0xA9; // LDA #$99
        Bus.Memory[0x8001] = 0x99;
        Bus.Memory[0x8002] = 0x40; // RTI
        CurrentState.SP = 0xFF;
        CurrentState.SP = 0xFF;
        CurrentState.InterruptDisableFlag = false;
        CurrentState.InterruptDisableFlag = false;

        // Step 1: Execute WAI - CPU should enter Waiting state
        Cpu.Step(Bus);
        Assert.Equal(CpuStatus.Waiting, CurrentState.Status);
        ushort pcAfterWai = CurrentState.PC;

        // Step 2: Clock several times - CPU should stay halted, PC unchanged
        for (int i = 0; i < 10; i++)
        {
            Cpu.Clock(Bus);
        }
        Assert.Equal(CpuStatus.Waiting, CurrentState.Status);
        Assert.Equal(pcAfterWai, CurrentState.PC);

        // Step 3: Signal IRQ and handle it (emulator host responsibility)
        Cpu.SignalIrq();
        bool handled = Cpu.HandlePendingInterrupt(Bus);
        Assert.True(handled);

        // CPU should now be running and PC should be at ISR
        Assert.Equal(CpuStatus.Running, CurrentState.Status);
        Assert.Equal(0x8000, CurrentState.PC);

        // Execute ISR: LDA #$99
        Cpu.Step(Bus);
        Assert.Equal(0x99, CurrentState.A);

        // Execute RTI - should return to instruction after WAI
        Cpu.Step(Bus);

        // Now execute the LDA #$42 that was after WAI
        Cpu.Step(Bus);
        Assert.Equal(0x42, CurrentState.A);
    }

    [Fact]
    public void WAI_Integration_NMI_WakesCpu()
    {
        // WAI ($CB) followed by NOP
        LoadAndReset([0xCB, 0xEA]);
        Bus.SetNmiVector(0x9000);
        Bus.Memory[0x9000] = 0xA9; // LDA #$77
        Bus.Memory[0x9001] = 0x77;
        Bus.Memory[0x9002] = 0x40; // RTI
        CurrentState.SP = 0xFF;
        CurrentState.SP = 0xFF;

        // Execute WAI
        Cpu.Step(Bus);
        Assert.Equal(CpuStatus.Waiting, CurrentState.Status);

        // Signal NMI and handle it (emulator host responsibility)
        Cpu.SignalNmi();
        bool handled = Cpu.HandlePendingInterrupt(Bus);
        Assert.True(handled);

        // CPU should be running and at NMI handler
        Assert.Equal(CpuStatus.Running, CurrentState.Status);
        Assert.Equal(0x9000, CurrentState.PC);

        // Execute NMI handler: LDA #$77
        Cpu.Step(Bus);
        Assert.Equal(0x77, CurrentState.A);
    }

    [Fact]
    public void STP_Integration_CpuStaysHalted_Reset_Required()
    {
        // STP ($DB) followed by instructions that should never execute
        LoadAndReset([0xDB, 0xA9, 0x42]);

        // Execute STP
        Cpu.Step(Bus);
        Assert.Equal(CpuStatus.Stopped, CurrentState.Status);
        ushort pcAfterStp = CurrentState.PC;

        // Clock many times - CPU should stay stopped
        for (int i = 0; i < 20; i++)
        {
            Cpu.Clock(Bus);
        }
        Assert.Equal(CpuStatus.Stopped, CurrentState.Status);
        Assert.Equal(pcAfterStp, CurrentState.PC);

        // IRQ should NOT wake from STP
        Cpu.SignalIrq();
        Cpu.Step(Bus);
        Assert.Equal(CpuStatus.Stopped, CurrentState.Status);

        // NMI should NOT wake from STP
        Cpu.SignalNmi();
        Cpu.Step(Bus);
        Assert.Equal(CpuStatus.Stopped, CurrentState.Status);

        // Only Reset can recover from STP
        Bus.SetResetVector(0x0400);
        Cpu.Reset(Bus);
        Assert.Equal(CpuStatus.Running, CurrentState.Status);
    }

    #endregion

    #region BRK vs IRQ Difference

    [Fact]
    public void BRK_PushesBreakFlagSet()
    {
        LoadAndReset([0x00, 0x00]);
        CurrentState.SP = 0xFF;
        CurrentState.SP = 0xFF;
        CurrentState.P = 0x00;
        CurrentState.P = 0x00;
        Bus.SetIrqVector(0x8000);

        StepInstruction();

        byte pushedP = Bus.Memory[0x01FD];
        Assert.True((pushedP & CpuState.FlagB) != 0);
    }

    [Fact]
    public void IRQ_PusheBreakFlagClear()
    {
        SetupCpu();
        Bus.SetIrqVector(0x8000);
        CurrentState.SP = 0xFF;
        CurrentState.P = 0xFF;
        CurrentState.InterruptDisableFlag = false;
        Cpu.SignalIrq();

        Cpu.HandlePendingInterrupt(Bus);

        byte pushedP = Bus.Memory[0x01FD];
        Assert.False((pushedP & CpuState.FlagB) != 0);
    }

    #endregion
}
