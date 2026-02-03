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
        var state = new CpuState();
        var cpu = CpuFactory.Create(variant, state);

        Assert.IsType(expectedType, cpu);
    }

    [Theory]
    [InlineData(CpuVariant.Nmos6502)]
    [InlineData(CpuVariant.Nmos6502Simple)]
    [InlineData(CpuVariant.Wdc65C02)]
    [InlineData(CpuVariant.Rockwell65C02)]
    public void VariantPropertyMatchesFactory(CpuVariant variant)
    {
        var state = new CpuState();
        var cpu = CpuFactory.Create(variant, state);

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
        var state = new CpuState();
        var cpu = CpuFactory.Create(variant, state);

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
        var state = new CpuState();
        var cpu = CpuFactory.Create(variant, state);
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
        var state = new CpuState();
        var cpu = CpuFactory.Create(variant, state);
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
        var state = new CpuState();
        var cpu = CpuFactory.Create(CpuVariant.Nmos6502, state);
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
        var state = new CpuState();
        var cpu = CpuFactory.Create(CpuVariant.Nmos6502, state);
        var newState = new CpuState();

        cpu.State = newState;

        Assert.Same(newState, cpu.State);
    }

    [Fact]
    public void InterruptSignalsUpdatePendingInterrupt()
    {
        var state = new CpuState();
        var cpu = CpuFactory.Create(CpuVariant.Nmos6502, state);

        cpu.SignalIrq();
        Assert.Equal(PendingInterrupt.Irq, cpu.State.PendingInterrupt);

        cpu.ClearIrq();
        Assert.Equal(PendingInterrupt.None, cpu.State.PendingInterrupt);

        cpu.SignalNmi();
        Assert.Equal(PendingInterrupt.Nmi, cpu.State.PendingInterrupt);

        cpu.SignalReset();
        Assert.Equal(PendingInterrupt.Reset, cpu.State.PendingInterrupt);
    }

    #region CpuFactory Tests

    [Fact]
    public void Factory_Create_WithInvalidVariant_ThrowsArgumentOutOfRangeException()
    {
        var state = new CpuState();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CpuFactory.Create((CpuVariant)999, state));
    }

    [Fact]
    public void Factory_Create_WithNullState_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CpuFactory.Create(CpuVariant.Wdc65C02, null!));
    }

    [Fact]
    public void Factory_CreateDebug_WithInvalidVariant_ThrowsArgumentOutOfRangeException()
    {
        var state = new CpuState();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CpuFactory.CreateDebug((CpuVariant)999, state));
    }

    [Fact]
    public void Factory_CreateDebug_WithNullCpu_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CpuFactory.CreateDebug(null!));
    }

    #endregion

    #region CPU Class Direct Construction Tests

    [Fact]
    public void Cpu6502_ParameterlessConstructor_CreatesValidInstance()
    {
        var cpu = new Cpu6502();

        Assert.NotNull(cpu);
        Assert.NotNull(cpu.State);
        Assert.Equal(CpuVariant.Nmos6502, cpu.Variant);
    }

    [Fact]
    public void Cpu6502Simple_ParameterlessConstructor_CreatesValidInstance()
    {
        var cpu = new Cpu6502Simple();

        Assert.NotNull(cpu);
        Assert.NotNull(cpu.State);
        Assert.Equal(CpuVariant.Nmos6502Simple, cpu.Variant);
    }

    [Fact]
    public void Cpu65C02_ParameterlessConstructor_CreatesValidInstance()
    {
        var cpu = new Cpu65C02();

        Assert.NotNull(cpu);
        Assert.NotNull(cpu.State);
        Assert.Equal(CpuVariant.Wdc65C02, cpu.Variant);
    }

    [Fact]
    public void Cpu65C02Rockwell_ParameterlessConstructor_CreatesValidInstance()
    {
        var cpu = new Cpu65C02Rockwell();

        Assert.NotNull(cpu);
        Assert.NotNull(cpu.State);
        Assert.Equal(CpuVariant.Rockwell65C02, cpu.Variant);
    }

    [Theory]
    [InlineData(CpuVariant.Nmos6502)]
    [InlineData(CpuVariant.Nmos6502Simple)]
    public void NmosCpu_DoesNotClearDecimalOnInterrupt(CpuVariant variant)
    {
        var bus = CreateBus();
        var state = new CpuState();
        var cpu = CpuFactory.Create(variant, state);
        cpu.Reset(bus);

        // Set decimal flag and enable IRQ
        cpu.State.DecimalFlag = true;
        cpu.State.InterruptDisableFlag = false;

        // Trigger IRQ
        cpu.SignalIrq();
        cpu.HandlePendingInterrupt(bus);

        // NMOS 6502 does NOT clear D flag on interrupt
        Assert.True(cpu.State.DecimalFlag);
    }

    [Theory]
    [InlineData(CpuVariant.Wdc65C02)]
    [InlineData(CpuVariant.Rockwell65C02)]
    public void CmosCpu_ClearsDecimalOnInterrupt(CpuVariant variant)
    {
        var bus = CreateBus();
        var state = new CpuState();
        var cpu = CpuFactory.Create(variant, state);
        cpu.Reset(bus);

        // Set decimal flag and enable IRQ
        cpu.State.DecimalFlag = true;
        cpu.State.InterruptDisableFlag = false;

        // Trigger IRQ
        cpu.SignalIrq();
        cpu.HandlePendingInterrupt(bus);

        // 65C02 DOES clear D flag on interrupt
        Assert.False(cpu.State.DecimalFlag);
    }

    [Fact]
    public void Cpu6502_StateConstructor_UsesProvidedState()
    {
        var state = new CpuState { A = 0x42 };
        var cpu = new Cpu6502(state);

        Assert.Same(state, cpu.State);
        Assert.Equal(0x42, cpu.State.A);
    }

    [Fact]
    public void Cpu6502Simple_StateConstructor_UsesProvidedState()
    {
        var state = new CpuState { A = 0x42 };
        var cpu = new Cpu6502Simple(state);

        Assert.Same(state, cpu.State);
        Assert.Equal(0x42, cpu.State.A);
    }

    [Fact]
    public void Cpu65C02_StateConstructor_UsesProvidedState()
    {
        var state = new CpuState { A = 0x42 };
        var cpu = new Cpu65C02(state);

        Assert.Same(state, cpu.State);
        Assert.Equal(0x42, cpu.State.A);
    }

    [Fact]
    public void Cpu65C02Rockwell_StateConstructor_UsesProvidedState()
    {
        var state = new CpuState { A = 0x42 };
        var cpu = new Cpu65C02Rockwell(state);

        Assert.Same(state, cpu.State);
        Assert.Equal(0x42, cpu.State.A);
    }

    #endregion

    private static TestRamBus CreateBus()
    {
        var bus = new TestRamBus();
        bus.SetResetVector(ProgramStart);
        bus.SetIrqVector(IrqHandler);
        bus.SetNmiVector(NmiHandler);
        return bus;
    }
}
