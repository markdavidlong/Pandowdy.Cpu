// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Xunit;

namespace Pandowdy.Cpu.Tests;

/// <summary>
/// Tests for interrupt handling on CPU instances.
/// </summary>
public class CpuInterruptHandlingTests
{
    private const ushort ProgramStart = 0x0400;
    private const ushort IrqHandler = 0x0500;
    private const ushort NmiHandler = 0x0600;

    [Theory]
    [InlineData(CpuVariant.Nmos6502, true)]
    [InlineData(CpuVariant.Nmos6502Simple, true)]
    [InlineData(CpuVariant.Wdc65C02, false)]
    [InlineData(CpuVariant.Rockwell65C02, false)]
    public void IrqClearsDecimalFlagForCmosVariants(CpuVariant variant, bool expectedDecimal)
    {
        var cpu = CreateCpu(variant, out var bus, out var buffer);
        cpu.State.PC = 0x2000;
        cpu.State.DecimalFlag = true;
        cpu.State.InterruptDisableFlag = false;

        cpu.SignalIrq();
        bool handled = cpu.HandlePendingInterrupt(bus);

        Assert.True(handled);
        Assert.Equal(expectedDecimal, cpu.State.DecimalFlag);
        Assert.Equal(IrqHandler, cpu.State.PC);
        Assert.Equal(PendingInterrupt.None, cpu.State.PendingInterrupt);
    }

    [Theory]
    [InlineData(CpuVariant.Nmos6502, true)]
    [InlineData(CpuVariant.Nmos6502Simple, true)]
    [InlineData(CpuVariant.Wdc65C02, false)]
    [InlineData(CpuVariant.Rockwell65C02, false)]
    public void NmiClearsDecimalFlagForCmosVariants(CpuVariant variant, bool expectedDecimal)
    {
        var cpu = CreateCpu(variant, out var bus, out var buffer);
        cpu.State.PC = 0x2000;
        cpu.State.DecimalFlag = true;

        cpu.SignalNmi();
        bool handled = cpu.HandlePendingInterrupt(bus);

        Assert.True(handled);
        Assert.Equal(expectedDecimal, cpu.State.DecimalFlag);
        Assert.Equal(NmiHandler, cpu.State.PC);
        Assert.Equal(PendingInterrupt.None, cpu.State.PendingInterrupt);
    }

    [Fact]
    public void IrqIsMaskedWhenInterruptDisableSet()
    {
        var cpu = CreateCpu(CpuVariant.Nmos6502, out var bus, out var buffer);
        cpu.State.PC = 0x2000;
        cpu.State.InterruptDisableFlag = true;

        cpu.SignalIrq();
        bool handled = cpu.HandlePendingInterrupt(bus);

        Assert.False(handled);
        Assert.Equal(PendingInterrupt.Irq, cpu.State.PendingInterrupt);
        Assert.Equal(0x2000, cpu.State.PC);
    }

    [Fact]
    public void IrqWakesCpuWhenWaiting()
    {
        var cpu = CreateCpu(CpuVariant.Wdc65C02, out var bus, out var buffer);
        cpu.State.PC = 0x2000;
        cpu.State.InterruptDisableFlag = true;
        cpu.State.Status = CpuStatus.Waiting;

        cpu.SignalIrq();
        bool handled = cpu.HandlePendingInterrupt(bus);

        Assert.True(handled);
        Assert.Equal(CpuStatus.Running, cpu.State.Status);
        Assert.Equal(IrqHandler, cpu.State.PC);
    }

    [Fact]
    public void ResetInterruptResetsRegistersAndLoadsVector()
    {
        var cpu = CreateCpu(CpuVariant.Nmos6502, out var bus, out var buffer);
        bus.SetResetVector(0x1234);
        cpu.State.A = 0x42;
        cpu.State.X = 0x24;
        cpu.State.Y = 0x18;
        cpu.State.PC = 0x2000;

        cpu.SignalReset();
        bool handled = cpu.HandlePendingInterrupt(bus);

        Assert.True(handled);
        Assert.Equal(0, cpu.State.A);
        Assert.Equal(0, cpu.State.X);
        Assert.Equal(0, cpu.State.Y);
        Assert.Equal(0xFD, cpu.State.SP);
        Assert.Equal(0x1234, cpu.State.PC);
        Assert.Equal(CpuState.FlagU | CpuState.FlagI, cpu.State.P);
        Assert.Equal(0, cpu.State.CurrentOpcode);
        Assert.Equal(0, cpu.State.OpcodeAddress);
        Assert.Equal(CpuStatus.Running, cpu.State.Status);
        Assert.Equal(PendingInterrupt.None, cpu.State.PendingInterrupt);
    }

    private static IPandowdyCpu CreateCpu(CpuVariant variant, out TestRamBus bus, out CpuStateBuffer buffer)
    {
        bus = new TestRamBus();
        bus.SetResetVector(ProgramStart);
        bus.SetIrqVector(IrqHandler);
        bus.SetNmiVector(NmiHandler);
        buffer = new CpuStateBuffer();
        var cpu = CpuFactory.Create(variant);
        cpu.Reset(bus);
        return cpu;
    }
}
