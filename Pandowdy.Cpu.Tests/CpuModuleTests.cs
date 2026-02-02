// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Collections.Generic;
using Xunit;

namespace Pandowdy.Cpu.Tests;

/// <summary>
/// Tests for the Cpu module functions: Clock, Step, Run, Reset, CurrentOpcode, CyclesRemaining.
/// </summary>
public class CpuModuleTests : CpuTestBase
{
    protected override CpuVariant Variant => CpuVariant.Wdc65C02;

    #region Clock Tests

    [Fact]
    public void Clock_ReturnsTrue_WhenInstructionCompletes()
    {
        // NOP is a 2-cycle instruction
        LoadAndReset([0xEA]);
        
        bool complete1 = Cpu.Clock(Bus);
        bool complete2 = Cpu.Clock(Bus);
        
        Assert.False(complete1); // First cycle - not complete
        Assert.True(complete2);  // Second cycle - complete
    }

    [Fact]
    public void Clock_ReturnsFalse_WhenInstructionInProgress()
    {
        // LDA Absolute is a 4-cycle instruction
        LoadAndReset([0xAD, 0x34, 0x12]);
        SetMemory(0x1234, 0x42);
        
        bool complete1 = Cpu.Clock(Bus);
        bool complete2 = Cpu.Clock(Bus);
        bool complete3 = Cpu.Clock(Bus);
        
        Assert.False(complete1);
        Assert.False(complete2);
        Assert.False(complete3);
    }

    [Fact]
    public void Clock_ReturnsTrue_WhenCpuIsStopped()
    {
        LoadAndReset([0xEA]);
        CurrentState.Status = CpuStatus.Stopped;

        bool complete = Cpu.Clock(Bus);

        Assert.True(complete);
    }

    [Fact]
    public void Clock_ReturnsTrue_WhenCpuIsJammed()
    {
        LoadAndReset([0xEA]);
        CurrentState.Status = CpuStatus.Jammed;

        bool complete = Cpu.Clock(Bus);

        Assert.True(complete);
    }

    [Fact]
    public void Clock_ReturnsTrue_WhenCpuIsWaiting()
    {
        LoadAndReset([0xEA]);
        CurrentState.Status = CpuStatus.Waiting;
        
        bool complete = Cpu.Clock(Bus);
        
        Assert.True(complete);
    }

    [Fact]
    public void Clock_ContinuesExecution_WhenStatusIsBypassed()
    {
        // Bypassed means the CPU continues running
        LoadAndReset([0xEA]); // NOP
        CurrentState.Status = CpuStatus.Bypassed;

        bool complete1 = Cpu.Clock(Bus);
        bool complete2 = Cpu.Clock(Bus);

        Assert.False(complete1);
        Assert.True(complete2);
    }

    [Fact]
    public void Clock_ContinuesExecution_WhenStatusIsRunning()
    {
        LoadAndReset([0xEA]); // NOP

        bool complete1 = Cpu.Clock(Bus);
        bool complete2 = Cpu.Clock(Bus);

        Assert.False(complete1);
        Assert.True(complete2);
    }

    [Fact]
    public void Clock_DoesNotAdvancePC_WhenHalted()
    {
        LoadAndReset([0xEA]);
        CurrentState.Status = CpuStatus.Stopped;
        ushort originalPC = CurrentState.PC;
        
        Cpu.Clock(Bus);
        
        Assert.Equal(originalPC, CurrentState.PC);
    }

    #endregion

    #region Step Tests

    [Fact]
    public void Step_ReturnsCorrectCycleCount_ForImmediate()
    {
        LoadAndReset([0xA9, 0x42]); // LDA #$42
        
        int cycles = Cpu.Step(Bus);
        
        Assert.Equal(2, cycles);
    }

    [Fact]
    public void Step_ReturnsCorrectCycleCount_ForZeroPage()
    {
        LoadAndReset([0xA5, 0x10]); // LDA $10
        SetZeroPage(0x10, 0x42);
        
        int cycles = Cpu.Step(Bus);
        
        Assert.Equal(3, cycles);
    }

    [Fact]
    public void Step_ReturnsCorrectCycleCount_ForAbsolute()
    {
        LoadAndReset([0xAD, 0x34, 0x12]); // LDA $1234
        SetMemory(0x1234, 0x42);
        
        int cycles = Cpu.Step(Bus);
        
        Assert.Equal(4, cycles);
    }

    [Fact]
    public void Step_CompletesFullInstruction()
    {
        LoadAndReset([0xA9, 0x42]); // LDA #$42
        
        Cpu.Step(Bus);
        
        Assert.Equal(0x42, CurrentState.A);
    }

    [Fact]
    public void Step_Returns1Cycle_WhenCpuIsStopped()
    {
        LoadAndReset([0xEA]);
        CurrentState.Status = CpuStatus.Stopped;

        int cycles = Cpu.Step(Bus);

        Assert.Equal(1, cycles);
    }

    [Fact]
    public void Step_Returns1Cycle_WhenCpuIsJammed()
    {
        LoadAndReset([0xEA]);
        CurrentState.Status = CpuStatus.Jammed;

        int cycles = Cpu.Step(Bus);

        Assert.Equal(1, cycles);
    }

    [Fact]
    public void Step_Returns1Cycle_WhenCpuIsWaiting()
    {
        LoadAndReset([0xEA]);
        CurrentState.Status = CpuStatus.Waiting;

        int cycles = Cpu.Step(Bus);

        Assert.Equal(1, cycles);
    }

    #endregion

    #region Run Tests

    [Fact]
    public void Run_ExecutesSpecifiedNumberOfCycles()
    {
        LoadAndReset([0xEA, 0xEA, 0xEA, 0xEA, 0xEA]); // 5 NOPs
        
        int cycles = Cpu.Run(Bus, 10);
        
        Assert.Equal(10, cycles);
    }

    [Fact]
    public void Run_StopsAtMaxCycles()
    {
        LoadAndReset([0xEA, 0xEA, 0xEA]); // NOPs
        
        int cycles = Cpu.Run(Bus, 5);
        
        Assert.Equal(5, cycles);
    }

    [Fact]
    public void Run_ExecutesMultipleInstructions()
    {
        // LDA #$42, LDX #$10
        LoadAndReset([0xA9, 0x42, 0xA2, 0x10]);
        
        Cpu.Run(Bus, 4); // 2 cycles each
        
        Assert.Equal(0x42, CurrentState.A);
        Assert.Equal(0x10, CurrentState.X);
    }

    [Fact]
    public void Run_ContinuesWhenCpuIsStopped()
    {
        // Run still consumes cycles even when stopped
        LoadAndReset([0xEA]);
        CurrentState.Status = CpuStatus.Stopped;

        int cycles = Cpu.Run(Bus, 10);

        Assert.Equal(10, cycles);
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_LoadsPCFromResetVector()
    {
        SetupCpu();
        Bus.SetResetVector(0xC000);

        Cpu.Reset(Bus);

        Assert.Equal(0xC000, CurrentState.PC);
    }

    [Fact]
    public void Reset_ClearsAllRegisters()
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
    public void Reset_SetsSPTo0xFD()
    {
        SetupCpu();
        CurrentState.SP = 0x00;
        
        Cpu.Reset(Bus);
        
        Assert.Equal(0xFD, CurrentState.SP);
    }

    [Fact]
    public void Reset_SetsStatusToRunning()
    {
        SetupCpu();
        CurrentState.Status = CpuStatus.Stopped;
        
        Cpu.Reset(Bus);
        
        Assert.Equal(CpuStatus.Running, CurrentState.Status);
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
    public void Reset_ClearsPipeline()
    {
        SetupCpu();
        // Execute partial instruction to set up pipeline
        Cpu.Clock(Bus);
        
        Cpu.Reset(Bus);
        
        Assert.Empty(CurrentState.Pipeline);
        Assert.Equal(0, CurrentState.PipelineIndex);
    }

    #endregion

    #region CurrentOpcode Tests

    [Fact]
    public void CurrentOpcode_ReturnsOpcodeAtPC_WhenPipelineEmpty()
    {
        LoadAndReset([0xA9, 0x42]); // LDA #$42

        Cpu.Clock(Bus);

        Assert.Equal(0xA9, CurrentState.CurrentOpcode);
        Assert.Equal(ProgramStart, CurrentState.OpcodeAddress);
    }

    [Fact]
    public void CurrentOpcode_ReturnsOpcodeFromPrevPC_DuringExecution()
    {
        LoadAndReset([0xA9, 0x42, 0xA2, 0x10]); // LDA #$42, LDX #$10
        
        // Start executing first instruction (advances PC)
        Cpu.Clock(Bus);

        // Should return opcode of instruction being executed
        Assert.Equal(0xA9, CurrentState.CurrentOpcode);
        Assert.Equal(ProgramStart, CurrentState.OpcodeAddress);
    }

    [Fact]
    public void CurrentOpcode_ReturnsNextOpcode_AfterInstructionComplete()
    {
        LoadAndReset([0xEA, 0xA9, 0x42]); // NOP, LDA #$42
        
        // Complete first instruction
        Cpu.Step(Bus);

        Cpu.Clock(Bus);

        Assert.Equal(0xA9, CurrentState.CurrentOpcode);
        Assert.Equal((ushort)(ProgramStart + 1), CurrentState.OpcodeAddress);
    }

    #endregion

    #region CyclesRemaining Tests

    [Fact]
    public void CyclesRemaining_ReturnsZero_WhenPipelineEmpty()
    {
        LoadAndReset([0xEA]);
        
        int remaining = CurrentState.CyclesRemaining;

        Assert.Equal(0, remaining);
    }

    [Fact]
    public void CyclesRemaining_ReturnsCorrectCount_DuringExecution()
    {
        // LDA Absolute is 4 cycles
        LoadAndReset([0xAD, 0x34, 0x12]);
        
        // Execute first cycle
        Cpu.Clock(Bus);

        int remaining = CurrentState.CyclesRemaining;

        Assert.Equal(3, remaining); // 4 total - 1 executed = 3 remaining
    }

    [Fact]
    public void CyclesRemaining_DecreasesEachCycle()
    {
        // LDA Absolute is 4 cycles
        LoadAndReset([0xAD, 0x34, 0x12]);
        
        Cpu.Clock(Bus);
        int after1 = CurrentState.CyclesRemaining;

        Cpu.Clock(Bus);
        int after2 = CurrentState.CyclesRemaining;

        Cpu.Clock(Bus);
        int after3 = CurrentState.CyclesRemaining;
        
        Assert.Equal(3, after1);
        Assert.Equal(2, after2);
        Assert.Equal(1, after3);
    }

    [Fact]
    public void CyclesRemaining_ReturnsZero_AfterInstructionComplete()
    {
        LoadAndReset([0xEA, 0xEA]); // Two NOPs
        
        // Complete first instruction
        Cpu.Step(Bus);

        int remaining = CurrentState.CyclesRemaining;

        Assert.Equal(0, remaining);
    }

    #endregion

    #region Variant Tests

    public static IEnumerable<object[]> AllVariants =>
    [
        [CpuVariant.Nmos6502],
        [CpuVariant.Nmos6502Simple],
        [CpuVariant.Wdc65C02],
        [CpuVariant.Rockwell65C02]
    ];

    [Theory]
    [MemberData(nameof(AllVariants))]
    public void Step_WorksWithAllVariants(CpuVariant variant)
    {
        var bus = new TestRamBus();
        bus.SetResetVector(ProgramStart);
        bus.SetIrqVector(IrqHandler);
        bus.SetNmiVector(NmiHandler);

        var cpu = CpuFactory.Create(variant);
        bus.LoadProgram(ProgramStart, [0xA9, 0x42]);

        cpu.Reset(bus);
        int cycles = cpu.Step(bus);

        Assert.Equal(2, cycles);
        Assert.Equal(0x42, cpu.State.A);
    }

    [Theory]
    [MemberData(nameof(AllVariants))]
    public void Run_WorksWithAllVariants(CpuVariant variant)
    {
        var bus = new TestRamBus();
        bus.SetResetVector(ProgramStart);
        bus.SetIrqVector(IrqHandler);
        bus.SetNmiVector(NmiHandler);

        var cpu = CpuFactory.Create(variant);
        bus.LoadProgram(ProgramStart, [0xEA, 0xEA]);

        cpu.Reset(bus);
        int cycles = cpu.Run(bus, 4);

        Assert.Equal(4, cycles);
    }

    #endregion
}
