// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Xunit;

namespace Pandowdy.Cpu.Tests;

/// <summary>
/// Tests for the new CPU instance API.
/// </summary>
public class CpuInstanceTests
{
    private const ushort ProgramStart = 0x0400;
    private const ushort IrqHandler = 0x0500;
    private const ushort NmiHandler = 0x0600;

    [Theory]
    [InlineData(CpuVariant.Nmos6502, typeof(Cpu6502))]
    [InlineData(CpuVariant.Nmos6502Simple, typeof(Cpu6502Simple))]
    [InlineData(CpuVariant.Wdc65C02, typeof(Cpu65C02))]
    [InlineData(CpuVariant.Rockwell65C02, typeof(Cpu65C02Rockwell))]
    public void FactoryCreatesExpectedType(CpuVariant variant, Type expectedType)
    {
        var cpu = CpuFactory.Create(variant);

        Assert.IsType(expectedType, cpu);
    }

    [Theory]
    [InlineData(CpuVariant.Nmos6502)]
    [InlineData(CpuVariant.Nmos6502Simple)]
    [InlineData(CpuVariant.Wdc65C02)]
    [InlineData(CpuVariant.Rockwell65C02)]
    public void VariantPropertyMatchesFactory(CpuVariant variant)
    {
        var cpu = CpuFactory.Create(variant);

        Assert.Equal(variant, cpu.Variant);
    }

    [Theory]
    [InlineData(CpuVariant.Nmos6502)]
    [InlineData(CpuVariant.Nmos6502Simple)]
    [InlineData(CpuVariant.Wdc65C02)]
    [InlineData(CpuVariant.Rockwell65C02)]
    public void ResetLoadsResetVector(CpuVariant variant)
    {
        var bus = CreateBus();
        var cpu = CpuFactory.Create(variant);

        cpu.Reset(bus);

        Assert.Equal(ProgramStart, cpu.State.PC);
    }

    [Theory]
    [InlineData(CpuVariant.Nmos6502)]
    [InlineData(CpuVariant.Nmos6502Simple)]
    [InlineData(CpuVariant.Wdc65C02)]
    [InlineData(CpuVariant.Rockwell65C02)]
    public void StepExecutesInstruction(CpuVariant variant)
    {
        var bus = CreateBus();
        var cpu = CpuFactory.Create(variant);
        bus.LoadProgram(ProgramStart, [0xA9, 0x42]);

        cpu.Reset(bus);
        int cycles = cpu.Step(bus);

        Assert.Equal(2, cycles);
        Assert.Equal(0x42, cpu.State.A);
    }

    [Theory]
    [InlineData(CpuVariant.Nmos6502)]
    [InlineData(CpuVariant.Nmos6502Simple)]
    [InlineData(CpuVariant.Wdc65C02)]
    [InlineData(CpuVariant.Rockwell65C02)]
    public void StepPopulatesOpcodeTracking(CpuVariant variant)
    {
        var bus = CreateBus();
        var cpu = CpuFactory.Create(variant);
        bus.LoadProgram(ProgramStart, [0xA9, 0x42]);

        cpu.Reset(bus);
        cpu.Step(bus);

        Assert.Equal(0xA9, cpu.State.CurrentOpcode);
        Assert.Equal(ProgramStart, cpu.State.OpcodeAddress);
    }

    [Fact]
    public void ClockCompletesInstruction()
    {
        var bus = CreateBus();
        var cpu = CpuFactory.Create(CpuVariant.Nmos6502);
        bus.LoadProgram(ProgramStart, [0xEA]);

        cpu.Reset(bus);

        int cycles = 0;
        bool complete = false;
        while (!complete)
        {
            complete = cpu.Clock(bus);
            cycles++;
        }

        Assert.Equal(2, cycles);
    }

    [Fact]
    public void StatePropertyAllowsSwap()
    {
        var cpu = CpuFactory.Create(CpuVariant.Nmos6502);
        var newState = new CpuState();

        cpu.State = newState;

        Assert.Same(newState, cpu.State);
    }

    [Fact]
    public void InterruptSignalsUpdatePendingInterrupt()
    {
        var cpu = CpuFactory.Create(CpuVariant.Nmos6502);

        cpu.SignalIrq();
        Assert.Equal(PendingInterrupt.Irq, cpu.State.PendingInterrupt);

        cpu.ClearIrq();
        Assert.Equal(PendingInterrupt.None, cpu.State.PendingInterrupt);

        cpu.SignalNmi();
        Assert.Equal(PendingInterrupt.Nmi, cpu.State.PendingInterrupt);

        cpu.SignalReset();
        Assert.Equal(PendingInterrupt.Reset, cpu.State.PendingInterrupt);
    }

    private static TestRamBus CreateBus()
    {
        var bus = new TestRamBus();
        bus.SetResetVector(ProgramStart);
        bus.SetIrqVector(IrqHandler);
        bus.SetNmiVector(NmiHandler);
        return bus;
    }
}
