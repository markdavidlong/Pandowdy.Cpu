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
    protected override CpuVariant Variant => CpuVariant.WDC65C02;

    #region Clock Tests

    [Fact]
    public void Clock_ReturnsTrue_WhenInstructionCompletes()
    {
        // NOP is a 2-cycle instruction
        LoadAndReset([0xEA]);
        
        bool complete1 = Cpu.Clock(Variant, CpuBuffer, Bus);
        bool complete2 = Cpu.Clock(Variant, CpuBuffer, Bus);
        
        Assert.False(complete1); // First cycle - not complete
        Assert.True(complete2);  // Second cycle - complete
    }

    [Fact]
    public void Clock_ReturnsFalse_WhenInstructionInProgress()
    {
        // LDA Absolute is a 4-cycle instruction
        LoadAndReset([0xAD, 0x34, 0x12]);
        SetMemory(0x1234, 0x42);
        
        bool complete1 = Cpu.Clock(Variant, CpuBuffer, Bus);
        bool complete2 = Cpu.Clock(Variant, CpuBuffer, Bus);
        bool complete3 = Cpu.Clock(Variant, CpuBuffer, Bus);
        
        Assert.False(complete1);
        Assert.False(complete2);
        Assert.False(complete3);
    }

    [Fact]
    public void Clock_ReturnsTrue_WhenCpuIsStopped()
    {
        LoadAndReset([0xEA]);
        CpuBuffer.Current.Status = CpuStatus.Stopped;
        CpuBuffer.Prev.Status = CpuStatus.Stopped;
        
        bool complete = Cpu.Clock(Variant, CpuBuffer, Bus);
        
        Assert.True(complete);
    }

    [Fact]
    public void Clock_ReturnsTrue_WhenCpuIsJammed()
    {
        LoadAndReset([0xEA]);
        CpuBuffer.Current.Status = CpuStatus.Jammed;
        CpuBuffer.Prev.Status = CpuStatus.Jammed;
        
        bool complete = Cpu.Clock(Variant, CpuBuffer, Bus);
        
        Assert.True(complete);
    }

    [Fact]
    public void Clock_ReturnsTrue_WhenCpuIsWaiting()
    {
        LoadAndReset([0xEA]);
        CpuBuffer.Current.Status = CpuStatus.Waiting;
        CpuBuffer.Prev.Status = CpuStatus.Waiting;
        
        bool complete = Cpu.Clock(Variant, CpuBuffer, Bus);
        
        Assert.True(complete);
    }

    [Fact]
    public void Clock_ContinuesExecution_WhenStatusIsBypassed()
    {
        // Bypassed means the CPU continues running
        LoadAndReset([0xEA]); // NOP
        CpuBuffer.Current.Status = CpuStatus.Bypassed;
        CpuBuffer.Prev.Status = CpuStatus.Bypassed;
        
        bool complete1 = Cpu.Clock(Variant, CpuBuffer, Bus);
        bool complete2 = Cpu.Clock(Variant, CpuBuffer, Bus);
        
        Assert.False(complete1);
        Assert.True(complete2);
    }

    [Fact]
    public void Clock_ContinuesExecution_WhenStatusIsRunning()
    {
        LoadAndReset([0xEA]); // NOP
        
        bool complete1 = Cpu.Clock(Variant, CpuBuffer, Bus);
        bool complete2 = Cpu.Clock(Variant, CpuBuffer, Bus);
        
        Assert.False(complete1);
        Assert.True(complete2);
    }

    [Fact]
    public void Clock_DoesNotAdvancePC_WhenHalted()
    {
        LoadAndReset([0xEA]);
        CpuBuffer.Current.Status = CpuStatus.Stopped;
        CpuBuffer.Prev.Status = CpuStatus.Stopped;
        ushort originalPC = CpuBuffer.Current.PC;
        
        Cpu.Clock(Variant, CpuBuffer, Bus);
        
        Assert.Equal(originalPC, CpuBuffer.Current.PC);
    }

    #endregion

    #region Step Tests

    [Fact]
    public void Step_ReturnsCorrectCycleCount_ForImmediate()
    {
        LoadAndReset([0xA9, 0x42]); // LDA #$42
        
        int cycles = Cpu.Step(Variant, CpuBuffer, Bus);
        
        Assert.Equal(2, cycles);
    }

    [Fact]
    public void Step_ReturnsCorrectCycleCount_ForZeroPage()
    {
        LoadAndReset([0xA5, 0x10]); // LDA $10
        SetZeroPage(0x10, 0x42);
        
        int cycles = Cpu.Step(Variant, CpuBuffer, Bus);
        
        Assert.Equal(3, cycles);
    }

    [Fact]
    public void Step_ReturnsCorrectCycleCount_ForAbsolute()
    {
        LoadAndReset([0xAD, 0x34, 0x12]); // LDA $1234
        SetMemory(0x1234, 0x42);
        
        int cycles = Cpu.Step(Variant, CpuBuffer, Bus);
        
        Assert.Equal(4, cycles);
    }

    [Fact]
    public void Step_CompletesFullInstruction()
    {
        LoadAndReset([0xA9, 0x42]); // LDA #$42
        
        Cpu.Step(Variant, CpuBuffer, Bus);
        
        Assert.Equal(0x42, CurrentState.A);
    }

    [Fact]
    public void Step_Returns1Cycle_WhenCpuIsStopped()
    {
        LoadAndReset([0xEA]);
        CpuBuffer.Current.Status = CpuStatus.Stopped;
        CpuBuffer.Prev.Status = CpuStatus.Stopped;
        
        int cycles = Cpu.Step(Variant, CpuBuffer, Bus);
        
        Assert.Equal(1, cycles);
    }

    [Fact]
    public void Step_Returns1Cycle_WhenCpuIsJammed()
    {
        LoadAndReset([0xEA]);
        CpuBuffer.Current.Status = CpuStatus.Jammed;
        CpuBuffer.Prev.Status = CpuStatus.Jammed;
        
        int cycles = Cpu.Step(Variant, CpuBuffer, Bus);
        
        Assert.Equal(1, cycles);
    }

    [Fact]
    public void Step_Returns1Cycle_WhenCpuIsWaiting()
    {
        LoadAndReset([0xEA]);
        CpuBuffer.Current.Status = CpuStatus.Waiting;
        CpuBuffer.Prev.Status = CpuStatus.Waiting;
        
        int cycles = Cpu.Step(Variant, CpuBuffer, Bus);
        
        Assert.Equal(1, cycles);
    }

    #endregion

    #region Run Tests

    [Fact]
    public void Run_ExecutesSpecifiedNumberOfCycles()
    {
        LoadAndReset([0xEA, 0xEA, 0xEA, 0xEA, 0xEA]); // 5 NOPs
        
        int cycles = Cpu.Run(Variant, CpuBuffer, Bus, 10);
        
        Assert.Equal(10, cycles);
    }

    [Fact]
    public void Run_StopsAtMaxCycles()
    {
        LoadAndReset([0xEA, 0xEA, 0xEA]); // NOPs
        
        int cycles = Cpu.Run(Variant, CpuBuffer, Bus, 5);
        
        Assert.Equal(5, cycles);
    }

    [Fact]
    public void Run_ExecutesMultipleInstructions()
    {
        // LDA #$42, LDX #$10
        LoadAndReset([0xA9, 0x42, 0xA2, 0x10]);
        
        Cpu.Run(Variant, CpuBuffer, Bus, 4); // 2 cycles each
        
        Assert.Equal(0x42, CpuBuffer.Current.A);
        Assert.Equal(0x10, CpuBuffer.Current.X);
    }

    [Fact]
    public void Run_ContinuesWhenCpuIsStopped()
    {
        // Run still consumes cycles even when stopped
        LoadAndReset([0xEA]);
        CpuBuffer.Current.Status = CpuStatus.Stopped;
        CpuBuffer.Prev.Status = CpuStatus.Stopped;
        
        int cycles = Cpu.Run(Variant, CpuBuffer, Bus, 10);
        
        Assert.Equal(10, cycles);
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_LoadsPCFromResetVector()
    {
        SetupCpu();
        Bus.SetResetVector(0xC000);
        
        Cpu.Reset(CpuBuffer, Bus);
        
        Assert.Equal(0xC000, CpuBuffer.Current.PC);
        Assert.Equal(0xC000, CpuBuffer.Prev.PC);
    }

    [Fact]
    public void Reset_ClearsAllRegisters()
    {
        SetupCpu();
        CpuBuffer.Current.A = 0xFF;
        CpuBuffer.Current.X = 0xFF;
        CpuBuffer.Current.Y = 0xFF;
        
        Cpu.Reset(CpuBuffer, Bus);
        
        Assert.Equal(0, CpuBuffer.Current.A);
        Assert.Equal(0, CpuBuffer.Current.X);
        Assert.Equal(0, CpuBuffer.Current.Y);
    }

    [Fact]
    public void Reset_SetsSPTo0xFD()
    {
        SetupCpu();
        CpuBuffer.Current.SP = 0x00;
        
        Cpu.Reset(CpuBuffer, Bus);
        
        Assert.Equal(0xFD, CpuBuffer.Current.SP);
    }

    [Fact]
    public void Reset_SetsStatusToRunning()
    {
        SetupCpu();
        CpuBuffer.Current.Status = CpuStatus.Stopped;
        
        Cpu.Reset(CpuBuffer, Bus);
        
        Assert.Equal(CpuStatus.Running, CpuBuffer.Current.Status);
    }

    [Fact]
    public void Reset_ClearsPendingInterrupts()
    {
        SetupCpu();
        CpuBuffer.Current.SignalNmi();
        
        Cpu.Reset(CpuBuffer, Bus);
        
        Assert.Equal(PendingInterrupt.None, CpuBuffer.Current.PendingInterrupt);
    }

    [Fact]
    public void Reset_ClearsPipeline()
    {
        SetupCpu();
        // Execute partial instruction to set up pipeline
        Cpu.Clock(Variant, CpuBuffer, Bus);
        
        Cpu.Reset(CpuBuffer, Bus);
        
        Assert.Empty(CpuBuffer.Current.Pipeline);
        Assert.Equal(0, CpuBuffer.Current.PipelineIndex);
    }

    #endregion

    #region CurrentOpcode Tests

    [Fact]
    public void CurrentOpcode_ReturnsOpcodeAtPC_WhenPipelineEmpty()
    {
        LoadAndReset([0xA9, 0x42]); // LDA #$42
        
        byte opcode = Cpu.CurrentOpcode(CpuBuffer, Bus);
        
        Assert.Equal(0xA9, opcode);
    }

    [Fact]
    public void CurrentOpcode_ReturnsOpcodeFromPrevPC_DuringExecution()
    {
        LoadAndReset([0xA9, 0x42, 0xA2, 0x10]); // LDA #$42, LDX #$10
        
        // Start executing first instruction (advances PC)
        Cpu.Clock(Variant, CpuBuffer, Bus);
        
        byte opcode = Cpu.CurrentOpcode(CpuBuffer, Bus);
        
        // Should return opcode of instruction being executed
        Assert.Equal(0xA9, opcode);
    }

    [Fact]
    public void CurrentOpcode_ReturnsNextOpcode_AfterInstructionComplete()
    {
        LoadAndReset([0xEA, 0xA9, 0x42]); // NOP, LDA #$42
        
        // Complete first instruction
        Cpu.Step(Variant, CpuBuffer, Bus);
        
        byte opcode = Cpu.CurrentOpcode(CpuBuffer, Bus);
        
        Assert.Equal(0xA9, opcode);
    }

    #endregion

    #region CyclesRemaining Tests

    [Fact]
    public void CyclesRemaining_ReturnsZero_WhenPipelineEmpty()
    {
        LoadAndReset([0xEA]);
        
        int remaining = Cpu.CyclesRemaining(CpuBuffer);
        
        Assert.Equal(0, remaining);
    }

    [Fact]
    public void CyclesRemaining_ReturnsCorrectCount_DuringExecution()
    {
        // LDA Absolute is 4 cycles
        LoadAndReset([0xAD, 0x34, 0x12]);
        
        // Execute first cycle
        Cpu.Clock(Variant, CpuBuffer, Bus);
        
        int remaining = Cpu.CyclesRemaining(CpuBuffer);
        
        Assert.Equal(3, remaining); // 4 total - 1 executed = 3 remaining
    }

    [Fact]
    public void CyclesRemaining_DecreasesEachCycle()
    {
        // LDA Absolute is 4 cycles
        LoadAndReset([0xAD, 0x34, 0x12]);
        
        Cpu.Clock(Variant, CpuBuffer, Bus);
        int after1 = Cpu.CyclesRemaining(CpuBuffer);
        
        Cpu.Clock(Variant, CpuBuffer, Bus);
        int after2 = Cpu.CyclesRemaining(CpuBuffer);
        
        Cpu.Clock(Variant, CpuBuffer, Bus);
        int after3 = Cpu.CyclesRemaining(CpuBuffer);
        
        Assert.Equal(3, after1);
        Assert.Equal(2, after2);
        Assert.Equal(1, after3);
    }

    [Fact]
    public void CyclesRemaining_ReturnsZero_AfterInstructionComplete()
    {
        LoadAndReset([0xEA, 0xEA]); // Two NOPs
        
        // Complete first instruction
        Cpu.Step(Variant, CpuBuffer, Bus);
        
        int remaining = Cpu.CyclesRemaining(CpuBuffer);
        
        Assert.Equal(0, remaining);
    }

    #endregion

    #region Variant Tests

    public static IEnumerable<object[]> AllVariants =>
    [
        [CpuVariant.NMOS6502],
        [CpuVariant.NMOS6502_NO_ILLEGAL],
        [CpuVariant.WDC65C02],
        [CpuVariant.ROCKWELL65C02]
    ];

    [Theory]
    [MemberData(nameof(AllVariants))]
    public void Step_WorksWithAllVariants(CpuVariant variant)
    {
        LoadAndReset([0xA9, 0x42]); // LDA #$42

        int cycles = Cpu.Step(variant, CpuBuffer, Bus);

        Assert.Equal(2, cycles);
        Assert.Equal(0x42, CurrentState.A);
    }

    [Theory]
    [MemberData(nameof(AllVariants))]
    public void Run_WorksWithAllVariants(CpuVariant variant)
    {
        LoadAndReset([0xEA, 0xEA]); // Two NOPs

        int cycles = Cpu.Run(variant, CpuBuffer, Bus, 4);

        Assert.Equal(4, cycles);
    }

    #endregion
}
