// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.Cpu.Internals;
using Xunit;

namespace Pandowdy.Cpu.Tests;

/// <summary>
/// Tests for CpuState class functionality.
/// </summary>
public class CpuStateTests
{
    #region Constructor and Reset Tests

    [Fact]
    public void Constructor_InitializesToPowerOnDefaults()
    {
        var state = new CpuState();

        Assert.Equal(0, state.A);
        Assert.Equal(0, state.X);
        Assert.Equal(0, state.Y);
        Assert.Equal(0xFD, state.SP);
        Assert.Equal(0x0000, state.PC);
        Assert.Equal(CpuStatus.Running, state.Status);
        Assert.Equal(PendingInterrupt.None, state.PendingInterrupt);
    }

    [Fact]
    public void Constructor_SetsUnusedAndInterruptDisableFlags()
    {
        var state = new CpuState();

        Assert.True(state.GetFlag(CpuState.FlagU));
        Assert.True(state.InterruptDisableFlag);
    }

    [Fact]
    public void Reset_RestoresToPowerOnDefaults()
    {
        var state = new CpuState
        {
            A = 0x42,
            X = 0x10,
            Y = 0x20,
            SP = 0x00,
            PC = 0x8000,
            Status = CpuStatus.Stopped
        };

        state.Reset();

        Assert.Equal(0, state.A);
        Assert.Equal(0, state.X);
        Assert.Equal(0, state.Y);
        Assert.Equal(0xFD, state.SP);
        Assert.Equal(0x0000, state.PC);
        Assert.Equal(CpuStatus.Running, state.Status);
    }

    [Fact]
    public void Reset_ClearsPipeline()
    {
        var state = new CpuState
        {
            Pipeline = new MicroOp[5],
            PipelineIndex = 3,
            InstructionComplete = true
        };

        state.Reset();

        Assert.Empty(state.Pipeline);
        Assert.Equal(0, state.PipelineIndex);
        Assert.False(state.InstructionComplete);
    }

    [Fact]
    public void Reset_ClearsPendingInterrupt()
    {
        var state = new CpuState();
        state.SignalNmi();

        state.Reset();

        Assert.Equal(PendingInterrupt.None, state.PendingInterrupt);
    }

    #endregion

    #region Flag Tests

    [Theory]
    [InlineData(CpuState.FlagC, true)]
    [InlineData(CpuState.FlagZ, true)]
    [InlineData(CpuState.FlagI, true)]
    [InlineData(CpuState.FlagD, true)]
    [InlineData(CpuState.FlagB, true)]
    [InlineData(CpuState.FlagV, true)]
    [InlineData(CpuState.FlagN, true)]
    public void SetFlag_SetsCorrectBit(byte flag, bool value)
    {
        var state = new CpuState
        {
            P = 0
        };

        state.SetFlag(flag, value);

        Assert.True(state.GetFlag(flag));
    }

    [Theory]
    [InlineData(CpuState.FlagC)]
    [InlineData(CpuState.FlagZ)]
    [InlineData(CpuState.FlagI)]
    [InlineData(CpuState.FlagD)]
    [InlineData(CpuState.FlagB)]
    [InlineData(CpuState.FlagV)]
    [InlineData(CpuState.FlagN)]
    public void SetFlag_ClearsCorrectBit(byte flag)
    {
        var state = new CpuState
        {
            P = 0xFF
        };

        state.SetFlag(flag, false);

        Assert.False(state.GetFlag(flag));
    }

    [Fact]
    public void CarryFlag_PropertyWorks()
    {
        var state = new CpuState
        {
            CarryFlag = true
        };
        Assert.True(state.CarryFlag);
        Assert.True(state.GetFlag(CpuState.FlagC));

        state.CarryFlag = false;
        Assert.False(state.CarryFlag);
    }

    [Fact]
    public void ZeroFlag_PropertyWorks()
    {
        var state = new CpuState
        {
            ZeroFlag = true
        };
        Assert.True(state.ZeroFlag);

        state.ZeroFlag = false;
        Assert.False(state.ZeroFlag);
    }

    [Fact]
    public void NegativeFlag_PropertyWorks()
    {
        var state = new CpuState
        {
            NegativeFlag = true
        };
        Assert.True(state.NegativeFlag);

        state.NegativeFlag = false;
        Assert.False(state.NegativeFlag);
    }

    [Fact]
    public void OverflowFlag_PropertyWorks()
    {
        var state = new CpuState
        {
            OverflowFlag = true
        };
        Assert.True(state.OverflowFlag);

        state.OverflowFlag = false;
        Assert.False(state.OverflowFlag);
    }

    [Fact]
    public void DecimalFlag_PropertyWorks()
    {
        var state = new CpuState
        {
            DecimalFlag = true
        };
        Assert.True(state.DecimalFlag);

        state.DecimalFlag = false;
        Assert.False(state.DecimalFlag);
    }

    [Fact]
    public void InterruptDisableFlag_PropertyWorks()
    {
        var state = new CpuState
        {
            InterruptDisableFlag = true
        };
        Assert.True(state.InterruptDisableFlag);

        state.InterruptDisableFlag = false;
        Assert.False(state.InterruptDisableFlag);
    }

    [Fact]
    public void BreakFlag_PropertyWorks()
    {
        var state = new CpuState
        {
            BreakFlag = true
        };
        Assert.True(state.BreakFlag);

        state.BreakFlag = false;
        Assert.False(state.BreakFlag);
    }

    #endregion

    #region CopyFrom Tests

    [Fact]
    public void CopyFrom_CopiesAllRegisters()
    {
        var source = new CpuState
        {
            A = 0x42,
            X = 0x10,
            Y = 0x20,
            P = 0xFF,
            SP = 0x80,
            PC = 0x1234
        };

        var destination = new CpuState();
        destination.CopyFrom(source);

        Assert.Equal(source.A, destination.A);
        Assert.Equal(source.X, destination.X);
        Assert.Equal(source.Y, destination.Y);
        Assert.Equal(source.P, destination.P);
        Assert.Equal(source.SP, destination.SP);
        Assert.Equal(source.PC, destination.PC);
    }

    [Fact]
    public void CopyFrom_CopiesTempValues()
    {
        var source = new CpuState
        {
            TempAddress = 0xABCD,
            TempValue = 0x1234
        };

        var destination = new CpuState();
        destination.CopyFrom(source);

        Assert.Equal(source.TempAddress, destination.TempAddress);
        Assert.Equal(source.TempValue, destination.TempValue);
    }

    [Fact]
    public void CopyFrom_CopiesPipelineState()
    {
        var pipeline = new MicroOp[3];
        var source = new CpuState
        {
            Pipeline = pipeline,
            PipelineIndex = 2,
            InstructionComplete = true
        };

        var destination = new CpuState();
        destination.CopyFrom(source);

        Assert.Same(source.Pipeline, destination.Pipeline);
        Assert.Equal(source.PipelineIndex, destination.PipelineIndex);
        Assert.Equal(source.InstructionComplete, destination.InstructionComplete);
    }

    [Fact]
    public void CopyFrom_CopiesStatus()
    {
        var source = new CpuState
        {
            Status = CpuStatus.Waiting,
            PendingInterrupt = PendingInterrupt.Nmi
        };

        var destination = new CpuState();
        destination.CopyFrom(source);

        Assert.Equal(source.Status, destination.Status);
        Assert.Equal(source.PendingInterrupt, destination.PendingInterrupt);
    }

    #endregion

    #region Interrupt Signal Tests

    [Fact]
    public void SignalIrq_SetsPendingInterrupt()
    {
        var state = new CpuState();

        state.SignalIrq();

        Assert.Equal(PendingInterrupt.Irq, state.PendingInterrupt);
    }

    [Fact]
    public void SignalIrq_DoesNotOverrideNmi()
    {
        var state = new CpuState();
        state.SignalNmi();

        state.SignalIrq();

        Assert.Equal(PendingInterrupt.Nmi, state.PendingInterrupt);
    }

    [Fact]
    public void SignalNmi_SetsPendingInterrupt()
    {
        var state = new CpuState();

        state.SignalNmi();

        Assert.Equal(PendingInterrupt.Nmi, state.PendingInterrupt);
    }

    [Fact]
    public void SignalNmi_OverridesIrq()
    {
        var state = new CpuState();
        state.SignalIrq();

        state.SignalNmi();

        Assert.Equal(PendingInterrupt.Nmi, state.PendingInterrupt);
    }

    [Fact]
    public void SignalNmi_DoesNotOverrideReset()
    {
        var state = new CpuState();
        state.SignalReset();

        state.SignalNmi();

        Assert.Equal(PendingInterrupt.Reset, state.PendingInterrupt);
    }

    [Fact]
    public void SignalReset_SetsPendingInterrupt()
    {
        var state = new CpuState();

        state.SignalReset();

        Assert.Equal(PendingInterrupt.Reset, state.PendingInterrupt);
    }

    [Fact]
    public void SignalReset_OverridesAll()
    {
        var state = new CpuState();
        state.SignalNmi();

        state.SignalReset();

        Assert.Equal(PendingInterrupt.Reset, state.PendingInterrupt);
    }

    [Fact]
    public void ClearIrq_ClearsPendingIrq()
    {
        var state = new CpuState();
        state.SignalIrq();

        state.ClearIrq();

        Assert.Equal(PendingInterrupt.None, state.PendingInterrupt);
    }

    [Fact]
    public void ClearIrq_DoesNotClearNmi()
    {
        var state = new CpuState();
        state.SignalNmi();

        state.ClearIrq();

        Assert.Equal(PendingInterrupt.Nmi, state.PendingInterrupt);
    }

    #endregion

    #region HandlePendingInterrupt Tests

    [Fact]
    public void HandlePendingInterrupt_Reset_LoadsResetVector()
    {
        var state = new CpuState();
        var bus = new TestRamBus();
        bus.SetResetVector(0x8000);
        state.SignalReset();

        bool handled = state.HandlePendingInterrupt(bus);

        Assert.True(handled);
        Assert.Equal(0x8000, state.PC);
        Assert.Equal(PendingInterrupt.None, state.PendingInterrupt);
    }

    [Fact]
    public void HandlePendingInterrupt_Reset_ResetsRegisters()
    {
        var state = new CpuState
        {
            A = 0xFF,
            X = 0xFF,
            Y = 0xFF
        };
        var bus = new TestRamBus();
        state.SignalReset();

        state.HandlePendingInterrupt(bus);

        Assert.Equal(0, state.A);
        Assert.Equal(0, state.X);
        Assert.Equal(0, state.Y);
        Assert.Equal(0xFD, state.SP);
    }

    [Fact]
    public void HandlePendingInterrupt_Nmi_LoadsNmiVector()
    {
        var state = new CpuState
        {
            PC = 0x1234,
            SP = 0xFF
        };
        var bus = new TestRamBus();
        bus.SetNmiVector(0x9000);
        state.SignalNmi();

        bool handled = state.HandlePendingInterrupt(bus);

        Assert.True(handled);
        Assert.Equal(0x9000, state.PC);
    }

    [Fact]
    public void HandlePendingInterrupt_Nmi_PushesStateToStack()
    {
        var state = new CpuState
        {
            PC = 0x1234,
            SP = 0xFF,
            P = 0x00
        };
        var bus = new TestRamBus();
        bus.SetNmiVector(0x9000);
        state.SignalNmi();

        state.HandlePendingInterrupt(bus);

        Assert.Equal(0xFC, state.SP);
        Assert.Equal(0x12, bus.Memory[0x01FF]);
        Assert.Equal(0x34, bus.Memory[0x01FE]);
    }

    [Fact]
    public void HandlePendingInterrupt_Nmi_SetsInterruptDisable()
    {
        var state = new CpuState
        {
            SP = 0xFF,
            InterruptDisableFlag = false
        };
        var bus = new TestRamBus();
        state.SignalNmi();

        state.HandlePendingInterrupt(bus);

        Assert.True(state.InterruptDisableFlag);
    }

    [Fact]
    public void HandlePendingInterrupt_Irq_LoadsIrqVector()
    {
        var state = new CpuState
        {
            PC = 0x1234,
            SP = 0xFF,
            InterruptDisableFlag = false
        };
        var bus = new TestRamBus();
        bus.SetIrqVector(0xA000);
        state.SignalIrq();

        bool handled = state.HandlePendingInterrupt(bus);

        Assert.True(handled);
        Assert.Equal(0xA000, state.PC);
    }

    [Fact]
    public void HandlePendingInterrupt_Irq_IgnoredWhenInterruptDisabled()
    {
        var state = new CpuState
        {
            PC = 0x1234,
            SP = 0xFF,
            InterruptDisableFlag = true
        };
        var bus = new TestRamBus();
        bus.SetIrqVector(0xA000);
        state.SignalIrq();

        bool handled = state.HandlePendingInterrupt(bus);

        Assert.False(handled);
        Assert.Equal(0x1234, state.PC);
    }

    [Fact]
    public void HandlePendingInterrupt_Irq_HandledWhenWaitingEvenIfDisabled()
    {
        var state = new CpuState
        {
            PC = 0x1234,
            SP = 0xFF,
            Status = CpuStatus.Waiting,
            InterruptDisableFlag = true
        };
        var bus = new TestRamBus();
        bus.SetIrqVector(0xA000);
        state.SignalIrq();

        bool handled = state.HandlePendingInterrupt(bus);

        Assert.True(handled);
        Assert.Equal(CpuStatus.Running, state.Status);
    }

    [Fact]
    public void HandlePendingInterrupt_Nmi_ResumesFromWaiting()
    {
        var state = new CpuState
        {
            SP = 0xFF,
            Status = CpuStatus.Waiting
        };
        var bus = new TestRamBus();
        state.SignalNmi();

        state.HandlePendingInterrupt(bus);

        Assert.Equal(CpuStatus.Running, state.Status);
    }

    [Fact]
    public void HandlePendingInterrupt_None_ReturnsFalse()
    {
        var state = new CpuState();
        var bus = new TestRamBus();

        bool handled = state.HandlePendingInterrupt(bus);

        Assert.False(handled);
    }

    #endregion
}
