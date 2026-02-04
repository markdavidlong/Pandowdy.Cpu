// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Xunit;

namespace Pandowdy.Cpu.Tests;

/// <summary>
/// Core instruction tests that should pass on all CPU variants.
/// Derived classes specify the variant to test.
/// </summary>
public abstract class CoreInstructionTests : CpuTestBase
{
    #region LDA Tests

    [Fact]
    public void LDA_Immediate_Takes2Cycles()
    {
        LoadAndReset([0xA9, 0x42]);
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
        Assert.Equal(0x42, CurrentState.A);
    }

    [Fact]
    public void LDA_ZeroPage_Takes3Cycles()
    {
        LoadAndReset([0xA5, 0x10]);
        SetZeroPage(0x10, 0x42);
        int cycles = StepInstruction();
        Assert.Equal(3, cycles);
        Assert.Equal(0x42, CurrentState.A);
    }

    [Fact]
    public void LDA_ZeroPageX_Takes4Cycles()
    {
        LoadAndReset([0xB5, 0x10]);
        CurrentState.X = 5;
        CurrentState.X = 5;
        SetZeroPage(0x15, 0x42);
        int cycles = StepInstruction();
        Assert.Equal(4, cycles);
        Assert.Equal(0x42, CurrentState.A);
    }

    [Fact]
    public void LDA_Absolute_Takes4Cycles()
    {
        LoadAndReset([0xAD, 0x34, 0x12]);
        SetMemory(0x1234, 0x42);
        int cycles = StepInstruction();
        Assert.Equal(4, cycles);
        Assert.Equal(0x42, CurrentState.A);
    }

    [Fact]
    public void LDA_AbsoluteX_Takes4Cycles_NoPageCross()
    {
        LoadAndReset([0xBD, 0x00, 0x12]);
        CurrentState.X = 0x10;
        CurrentState.X = 0x10;
        SetMemory(0x1210, 0x42);
        int cycles = StepInstruction();
        Assert.Equal(4, cycles);
        Assert.Equal(0x42, CurrentState.A);
    }

    [Fact]
    public void LDA_AbsoluteX_Takes5Cycles_WithPageCross()
    {
        LoadAndReset([0xBD, 0xF0, 0x12]);
        CurrentState.X = 0x20;
        CurrentState.X = 0x20;
        SetMemory(0x1310, 0x42);
        int cycles = StepInstruction();
        Assert.Equal(5, cycles);
        Assert.Equal(0x42, CurrentState.A);
    }

    [Fact]
    public void LDA_AbsoluteY_Takes4Cycles_NoPageCross()
    {
        LoadAndReset([0xB9, 0x00, 0x12]);
        CurrentState.Y = 0x10;
        CurrentState.Y = 0x10;
        SetMemory(0x1210, 0x42);
        int cycles = StepInstruction();
        Assert.Equal(4, cycles);
    }

    [Fact]
    public void LDA_AbsoluteY_Takes5Cycles_WithPageCross()
    {
        LoadAndReset([0xB9, 0xF0, 0x12]);
        CurrentState.Y = 0x20;
        CurrentState.Y = 0x20;
        SetMemory(0x1310, 0x42);
        int cycles = StepInstruction();
        Assert.Equal(5, cycles);
    }

    [Fact]
    public void LDA_IndexedIndirectX_Takes6Cycles()
    {
        LoadAndReset([0xA1, 0x10]);
        CurrentState.X = 0x05;
        CurrentState.X = 0x05;
        SetZeroPagePointer(0x15, 0x1234);
        SetMemory(0x1234, 0x42);
        int cycles = StepInstruction();
        Assert.Equal(6, cycles);
        Assert.Equal(0x42, CurrentState.A);
    }

    [Fact]
    public void LDA_IndirectIndexedY_Takes5Cycles_NoPageCross()
    {
        LoadAndReset([0xB1, 0x10]);
        CurrentState.Y = 0x10;
        CurrentState.Y = 0x10;
        SetZeroPagePointer(0x10, 0x1200);
        SetMemory(0x1210, 0x42);
        int cycles = StepInstruction();
        Assert.Equal(5, cycles);
    }

    [Fact]
    public void LDA_IndirectIndexedY_Takes6Cycles_WithPageCross()
    {
        LoadAndReset([0xB1, 0x10]);
        CurrentState.Y = 0x20;
        CurrentState.Y = 0x20;
        SetZeroPagePointer(0x10, 0x12F0);
        SetMemory(0x1310, 0x42);
        int cycles = StepInstruction();
        Assert.Equal(6, cycles);
    }

    [Fact]
    public void LDA_SetsZeroFlag()
    {
        LoadAndReset([0xA9, 0x00]);
        StepInstruction();
        Assert.True(CurrentState.ZeroFlag);
        Assert.False(CurrentState.NegativeFlag);
    }

    [Fact]
    public void LDA_SetsNegativeFlag()
    {
        LoadAndReset([0xA9, 0x80]);
        StepInstruction();
        Assert.False(CurrentState.ZeroFlag);
        Assert.True(CurrentState.NegativeFlag);
    }

    #endregion

    #region LDX Tests

    [Fact]
    public void LDX_Immediate_Takes2Cycles()
    {
        LoadAndReset([0xA2, 0x42]);
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
        Assert.Equal(0x42, CurrentState.X);
    }

    [Fact]
    public void LDX_ZeroPage_Takes3Cycles()
    {
        LoadAndReset([0xA6, 0x10]);
        SetZeroPage(0x10, 0x42);
        int cycles = StepInstruction();
        Assert.Equal(3, cycles);
        Assert.Equal(0x42, CurrentState.X);
    }

    [Fact]
    public void LDX_Absolute_Takes4Cycles()
    {
        LoadAndReset([0xAE, 0x34, 0x12]);
        SetMemory(0x1234, 0x42);
        int cycles = StepInstruction();
        Assert.Equal(4, cycles);
    }

    #endregion

    #region LDY Tests

    [Fact]
    public void LDY_Immediate_Takes2Cycles()
    {
        LoadAndReset([0xA0, 0x42]);
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
        Assert.Equal(0x42, CurrentState.Y);
    }

    [Fact]
    public void LDY_ZeroPage_Takes3Cycles()
    {
        LoadAndReset([0xA4, 0x10]);
        SetZeroPage(0x10, 0x42);
        int cycles = StepInstruction();
        Assert.Equal(3, cycles);
    }

    [Fact]
    public void LDY_Absolute_Takes4Cycles()
    {
        LoadAndReset([0xAC, 0x34, 0x12]);
        SetMemory(0x1234, 0x42);
        int cycles = StepInstruction();
        Assert.Equal(4, cycles);
    }

    #endregion

    #region STA Tests

    [Fact]
    public void STA_ZeroPage_Takes3Cycles()
    {
        LoadAndReset([0x85, 0x10]);
        CurrentState.A = 0x42;
        CurrentState.A = 0x42;
        int cycles = StepInstruction();
        Assert.Equal(3, cycles);
        Assert.Equal(0x42, Bus.Memory[0x10]);
    }

    [Fact]
    public void STA_Absolute_Takes4Cycles()
    {
        LoadAndReset([0x8D, 0x34, 0x12]);
        CurrentState.A = 0x42;
        CurrentState.A = 0x42;
        int cycles = StepInstruction();
        Assert.Equal(4, cycles);
        Assert.Equal(0x42, Bus.Memory[0x1234]);
    }

    [Fact]
    public void STA_AbsoluteX_Takes5Cycles_AlwaysNoPenalty()
    {
        LoadAndReset([0x9D, 0xF0, 0x12]);
        CurrentState.A = 0x42;
        CurrentState.A = 0x42;
        CurrentState.X = 0x20;
        CurrentState.X = 0x20;
        int cycles = StepInstruction();
        Assert.Equal(5, cycles);
    }

    [Fact]
    public void STA_AbsoluteY_Takes5Cycles()
    {
        LoadAndReset([0x99, 0x00, 0x12]);
        CurrentState.A = 0x42;
        CurrentState.A = 0x42;
        CurrentState.Y = 0x10;
        CurrentState.Y = 0x10;
        int cycles = StepInstruction();
        Assert.Equal(5, cycles);
    }

    [Fact]
    public void STA_IndexedIndirectX_Takes6Cycles()
    {
        LoadAndReset([0x81, 0x10]);
        CurrentState.A = 0x42;
        CurrentState.A = 0x42;
        CurrentState.X = 0x05;
        CurrentState.X = 0x05;
        SetZeroPagePointer(0x15, 0x1234);
        int cycles = StepInstruction();
        Assert.Equal(6, cycles);
    }

    [Fact]
    public void STA_IndirectIndexedY_Takes6Cycles()
    {
        LoadAndReset([0x91, 0x10]);
        CurrentState.A = 0x42;
        CurrentState.A = 0x42;
        CurrentState.Y = 0x20;
        CurrentState.Y = 0x20;
        SetZeroPagePointer(0x10, 0x12F0);
        int cycles = StepInstruction();
        Assert.Equal(6, cycles);
    }

    #endregion

    #region STX/STY Tests

    [Fact]
    public void STX_ZeroPage_Takes3Cycles()
    {
        LoadAndReset([0x86, 0x10]);
        CurrentState.X = 0x42;
        CurrentState.X = 0x42;
        int cycles = StepInstruction();
        Assert.Equal(3, cycles);
    }

    [Fact]
    public void STY_ZeroPage_Takes3Cycles()
    {
        LoadAndReset([0x84, 0x10]);
        CurrentState.Y = 0x42;
        CurrentState.Y = 0x42;
        int cycles = StepInstruction();
        Assert.Equal(3, cycles);
    }

    #endregion

    #region ADC Tests

    [Fact]
    public void ADC_Immediate_Takes2Cycles()
    {
        LoadAndReset([0x69, 0x10]);
        CurrentState.A = 0x05;
        CurrentState.A = 0x05;
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
        Assert.Equal(0x15, CurrentState.A);
    }

    [Fact]
    public void ADC_ZeroPage_Takes3Cycles()
    {
        LoadAndReset([0x65, 0x10]);
        CurrentState.A = 0x05;
        CurrentState.A = 0x05;
        SetZeroPage(0x10, 0x10);
        int cycles = StepInstruction();
        Assert.Equal(3, cycles);
    }

    [Fact]
    public void ADC_Absolute_Takes4Cycles()
    {
        LoadAndReset([0x6D, 0x34, 0x12]);
        CurrentState.A = 0x05;
        CurrentState.A = 0x05;
        SetMemory(0x1234, 0x10);
        int cycles = StepInstruction();
        Assert.Equal(4, cycles);
    }

    [Fact]
    public void ADC_SetsCarryOnOverflow()
    {
        LoadAndReset([0x69, 0x02]);
        CurrentState.A = 0xFF;
        CurrentState.A = 0xFF;
        StepInstruction();
        Assert.True(CurrentState.CarryFlag);
        Assert.Equal(0x01, CurrentState.A);
    }

    [Fact]
    public void ADC_SetsOverflowOnSignedOverflow()
    {
        LoadAndReset([0x69, 0x01]);
        CurrentState.A = 0x7F;
        CurrentState.A = 0x7F;
        StepInstruction();
        Assert.True(CurrentState.OverflowFlag);
    }

    [Fact]
    public void ADC_BCD_CorrectResult()
    {
        LoadAndReset([0x69, 0x27]);
        CurrentState.A = 0x15;
        CurrentState.A = 0x15;
        CurrentState.DecimalFlag = true;
        CurrentState.DecimalFlag = true;
        StepInstruction();
        Assert.Equal(0x42, CurrentState.A);
    }

    #endregion

    #region SBC Tests

    [Fact]
    public void SBC_Immediate_Takes2Cycles()
    {
        LoadAndReset([0xE9, 0x05]);
        CurrentState.A = 0x10;
        CurrentState.A = 0x10;
        CurrentState.CarryFlag = true;
        CurrentState.CarryFlag = true;
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
        Assert.Equal(0x0B, CurrentState.A);
    }

    [Fact]
    public void SBC_ZeroPage_Takes3Cycles()
    {
        LoadAndReset([0xE5, 0x10]);
        CurrentState.A = 0x15;
        CurrentState.A = 0x15;
        CurrentState.CarryFlag = true;
        CurrentState.CarryFlag = true;
        SetZeroPage(0x10, 0x05);
        int cycles = StepInstruction();
        Assert.Equal(3, cycles);
    }

    [Fact]
    public void SBC_ClearsCarryOnBorrow()
    {
        LoadAndReset([0xE9, 0x10]);
        CurrentState.A = 0x05;
        CurrentState.A = 0x05;
        CurrentState.CarryFlag = true;
        CurrentState.CarryFlag = true;
        StepInstruction();
        Assert.False(CurrentState.CarryFlag);
    }

    #endregion

    #region Logic Operations

    [Fact]
    public void AND_Immediate_Takes2Cycles()
    {
        LoadAndReset([0x29, 0x0F]);
        CurrentState.A = 0xFF;
        CurrentState.A = 0xFF;
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
        Assert.Equal(0x0F, CurrentState.A);
    }

    [Fact]
    public void ORA_Immediate_Takes2Cycles()
    {
        LoadAndReset([0x09, 0xF0]);
        CurrentState.A = 0x0F;
        CurrentState.A = 0x0F;
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
        Assert.Equal(0xFF, CurrentState.A);
    }

    [Fact]
    public void EOR_Immediate_Takes2Cycles()
    {
        LoadAndReset([0x49, 0xFF]);
        CurrentState.A = 0xAA;
        CurrentState.A = 0xAA;
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
        Assert.Equal(0x55, CurrentState.A);
    }

    #endregion

    #region Compare Operations

    [Fact]
    public void CMP_Immediate_Takes2Cycles()
    {
        LoadAndReset([0xC9, 0x10]);
        CurrentState.A = 0x20;
        CurrentState.A = 0x20;
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
        Assert.True(CurrentState.CarryFlag);
    }

    [Fact]
    public void CMP_SetsZeroWhenEqual()
    {
        LoadAndReset([0xC9, 0x20]);
        CurrentState.A = 0x20;
        CurrentState.A = 0x20;
        StepInstruction();
        Assert.True(CurrentState.ZeroFlag);
        Assert.True(CurrentState.CarryFlag);
    }

    [Fact]
    public void CPX_Immediate_Takes2Cycles()
    {
        LoadAndReset([0xE0, 0x10]);
        CurrentState.X = 0x20;
        CurrentState.X = 0x20;
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
    }

    [Fact]
    public void CPY_Immediate_Takes2Cycles()
    {
        LoadAndReset([0xC0, 0x10]);
        CurrentState.Y = 0x20;
        CurrentState.Y = 0x20;
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
    }

    #endregion

    #region BIT Tests

    [Fact]
    public void BIT_ZeroPage_Takes3Cycles()
    {
        LoadAndReset([0x24, 0x10]);
        CurrentState.A = 0xFF;
        CurrentState.A = 0xFF;
        SetZeroPage(0x10, 0xC0);
        int cycles = StepInstruction();
        Assert.Equal(3, cycles);
    }

    [Fact]
    public void BIT_SetsNVFromMemory()
    {
        LoadAndReset([0x24, 0x10]);
        CurrentState.A = 0x00;
        CurrentState.A = 0x00;
        SetZeroPage(0x10, 0xC0);
        StepInstruction();
        Assert.True(CurrentState.NegativeFlag);
        Assert.True(CurrentState.OverflowFlag);
        Assert.True(CurrentState.ZeroFlag);
    }

    #endregion

    #region Branch Tests

    [Fact]
    public void BEQ_NotTaken_Takes2Cycles()
    {
        LoadAndReset([0xF0, 0x10]);
        CurrentState.ZeroFlag = false;
        CurrentState.ZeroFlag = false;
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
    }

    [Fact]
    public void BEQ_Taken_NoPageCross_Takes3Cycles()
    {
        LoadAndReset([0xF0, 0x10]);
        CurrentState.ZeroFlag = true;
        CurrentState.ZeroFlag = true;
        int cycles = StepInstruction();
        Assert.Equal(3, cycles);
    }

    [Fact]
    public void BNE_NotTaken_Takes2Cycles()
    {
        LoadAndReset([0xD0, 0x10]);
        CurrentState.ZeroFlag = true;
        CurrentState.ZeroFlag = true;
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
    }

    [Fact]
    public void BCS_Taken_Takes3Cycles()
    {
        LoadAndReset([0xB0, 0x10]);
        CurrentState.CarryFlag = true;
        CurrentState.CarryFlag = true;
        int cycles = StepInstruction();
        Assert.Equal(3, cycles);
    }

    [Fact]
    public void BCC_Taken_Takes3Cycles()
    {
        LoadAndReset([0x90, 0x10]);
        CurrentState.CarryFlag = false;
        CurrentState.CarryFlag = false;
        int cycles = StepInstruction();
        Assert.Equal(3, cycles);
    }

    [Fact]
    public void BMI_Taken_Takes3Cycles()
    {
        LoadAndReset([0x30, 0x10]);
        CurrentState.NegativeFlag = true;
        CurrentState.NegativeFlag = true;
        int cycles = StepInstruction();
        Assert.Equal(3, cycles);
    }

    [Fact]
    public void BPL_Taken_Takes3Cycles()
    {
        LoadAndReset([0x10, 0x10]);
        CurrentState.NegativeFlag = false;
        CurrentState.NegativeFlag = false;
        int cycles = StepInstruction();
        Assert.Equal(3, cycles);
    }

    [Fact]
    public void BVS_Taken_Takes3Cycles()
    {
        LoadAndReset([0x70, 0x10]);
        CurrentState.OverflowFlag = true;
        CurrentState.OverflowFlag = true;
        int cycles = StepInstruction();
        Assert.Equal(3, cycles);
    }

    [Fact]
    public void BVC_Taken_Takes3Cycles()
    {
        LoadAndReset([0x50, 0x10]);
        CurrentState.OverflowFlag = false;
        CurrentState.OverflowFlag = false;
        int cycles = StepInstruction();
        Assert.Equal(3, cycles);
    }

    #endregion

    #region JMP Tests

    [Fact]
    public void JMP_Absolute_Takes3Cycles()
    {
        LoadAndReset([0x4C, 0x34, 0x12]);
        int cycles = StepInstruction();
        Assert.Equal(3, cycles);
        Assert.Equal(0x1234, CurrentState.PC);
    }

    // Note: JMP_Indirect cycle count differs between variants:
    // - NMOS 6502: 5 cycles (has page boundary bug)
    // - 65C02 variants: 6 cycles (bug fixed)
    // See variant-specific test classes for JMP_Indirect tests.

    #endregion

    #region Stack Operations

    [Fact]
    public void PHA_Takes3Cycles()
    {
        LoadAndReset([0x48]);
        CurrentState.A = 0x42;
        CurrentState.A = 0x42;
        CurrentState.SP = 0xFF;
        CurrentState.SP = 0xFF;
        int cycles = StepInstruction();
        Assert.Equal(3, cycles);
        Assert.Equal(0x42, Bus.Memory[0x01FF]);
    }

    [Fact]
    public void PLA_Takes4Cycles()
    {
        LoadAndReset([0x68]);
        Bus.Memory[0x01FF] = 0x42;
        CurrentState.SP = 0xFE;
        CurrentState.SP = 0xFE;
        int cycles = StepInstruction();
        Assert.Equal(4, cycles);
        Assert.Equal(0x42, CurrentState.A);
    }

    [Fact]
    public void PHP_Takes3Cycles()
    {
        LoadAndReset([0x08]);
        CurrentState.SP = 0xFF;
        CurrentState.SP = 0xFF;
        int cycles = StepInstruction();
        Assert.Equal(3, cycles);
    }

    [Fact]
    public void PLP_Takes4Cycles()
    {
        LoadAndReset([0x28]);
        Bus.Memory[0x01FF] = 0xFF;
        CurrentState.SP = 0xFE;
        CurrentState.SP = 0xFE;
        int cycles = StepInstruction();
        Assert.Equal(4, cycles);
    }

    #endregion

    #region Subroutine Operations

    [Fact]
    public void JSR_Takes6Cycles()
    {
        LoadAndReset([0x20, 0x34, 0x12]);
        CurrentState.SP = 0xFF;
        CurrentState.SP = 0xFF;
        int cycles = StepInstruction();
        Assert.Equal(6, cycles);
        Assert.Equal(0x1234, CurrentState.PC);
    }

    [Fact]
    public void RTS_Takes6Cycles()
    {
        LoadAndReset([0x60]);
        CurrentState.SP = 0xFD;
        CurrentState.SP = 0xFD;
        Bus.Memory[0x01FE] = 0x02;
        Bus.Memory[0x01FF] = 0x04;
        int cycles = StepInstruction();
        Assert.Equal(6, cycles);
    }

    [Fact]
    public void RTI_Takes6Cycles()
    {
        LoadAndReset([0x40]);
        CurrentState.SP = 0xFC;
        CurrentState.SP = 0xFC;
        Bus.Memory[0x01FD] = 0x00;
        Bus.Memory[0x01FE] = 0x00;
        Bus.Memory[0x01FF] = 0x80;
        int cycles = StepInstruction();
        Assert.Equal(6, cycles);
    }

    [Fact]
    public void BRK_Takes7Cycles()
    {
        LoadAndReset([0x00, 0x00]);
        CurrentState.SP = 0xFF;
        CurrentState.SP = 0xFF;
        Bus.SetIrqVector(0x8000);
        int cycles = StepInstruction();
        Assert.Equal(7, cycles);
    }

    #endregion

    #region RMW Operations

    [Fact]
    public void INC_ZeroPage_Takes5Cycles()
    {
        LoadAndReset([0xE6, 0x10]);
        SetZeroPage(0x10, 0x05);
        int cycles = StepInstruction();
        Assert.Equal(5, cycles);
        Assert.Equal(0x06, Bus.Memory[0x10]);
    }

    [Fact]
    public void DEC_ZeroPage_Takes5Cycles()
    {
        LoadAndReset([0xC6, 0x10]);
        SetZeroPage(0x10, 0x05);
        int cycles = StepInstruction();
        Assert.Equal(5, cycles);
        Assert.Equal(0x04, Bus.Memory[0x10]);
    }

    [Fact]
    public void INC_Absolute_Takes6Cycles()
    {
        LoadAndReset([0xEE, 0x34, 0x12]);
        SetMemory(0x1234, 0x05);
        int cycles = StepInstruction();
        Assert.Equal(6, cycles);
    }

    [Fact]
    public void INC_AbsoluteX_Takes7Cycles()
    {
        LoadAndReset([0xFE, 0x00, 0x12]);
        CurrentState.X = 0x10;
        CurrentState.X = 0x10;
        SetMemory(0x1210, 0x05);
        int cycles = StepInstruction();
        Assert.Equal(7, cycles);
    }

    [Fact]
    public void ASL_Accumulator_Takes2Cycles()
    {
        LoadAndReset([0x0A]);
        CurrentState.A = 0x40;
        CurrentState.A = 0x40;
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
        Assert.Equal(0x80, CurrentState.A);
    }

    [Fact]
    public void ASL_ZeroPage_Takes5Cycles()
    {
        LoadAndReset([0x06, 0x10]);
        SetZeroPage(0x10, 0x40);
        int cycles = StepInstruction();
        Assert.Equal(5, cycles);
    }

    [Fact]
    public void LSR_Accumulator_Takes2Cycles()
    {
        LoadAndReset([0x4A]);
        CurrentState.A = 0x02;
        CurrentState.A = 0x02;
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
        Assert.Equal(0x01, CurrentState.A);
    }

    [Fact]
    public void ROL_Accumulator_Takes2Cycles()
    {
        LoadAndReset([0x2A]);
        CurrentState.A = 0x40;
        CurrentState.A = 0x40;
        CurrentState.CarryFlag = true;
        CurrentState.CarryFlag = true;
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
        Assert.Equal(0x81, CurrentState.A);
    }

    [Fact]
    public void ROR_Accumulator_Takes2Cycles()
    {
        LoadAndReset([0x6A]);
        CurrentState.A = 0x02;
        CurrentState.A = 0x02;
        CurrentState.CarryFlag = true;
        CurrentState.CarryFlag = true;
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
        Assert.Equal(0x81, CurrentState.A);
    }

    #endregion

    #region Transfer Operations

    [Fact]
    public void TAX_Takes2Cycles()
    {
        LoadAndReset([0xAA]);
        CurrentState.A = 0x42;
        CurrentState.A = 0x42;
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
        Assert.Equal(0x42, CurrentState.X);
    }

    [Fact]
    public void TXA_Takes2Cycles()
    {
        LoadAndReset([0x8A]);
        CurrentState.X = 0x42;
        CurrentState.X = 0x42;
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
        Assert.Equal(0x42, CurrentState.A);
    }

    [Fact]
    public void TAY_Takes2Cycles()
    {
        LoadAndReset([0xA8]);
        CurrentState.A = 0x42;
        CurrentState.A = 0x42;
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
        Assert.Equal(0x42, CurrentState.Y);
    }

    [Fact]
    public void TYA_Takes2Cycles()
    {
        LoadAndReset([0x98]);
        CurrentState.Y = 0x42;
        CurrentState.Y = 0x42;
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
        Assert.Equal(0x42, CurrentState.A);
    }

    [Fact]
    public void TSX_Takes2Cycles()
    {
        LoadAndReset([0xBA]);
        CurrentState.SP = 0x42;
        CurrentState.SP = 0x42;
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
        Assert.Equal(0x42, CurrentState.X);
    }

    [Fact]
    public void TXS_Takes2Cycles()
    {
        LoadAndReset([0x9A]);
        CurrentState.X = 0x42;
        CurrentState.X = 0x42;
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
        Assert.Equal(0x42, CurrentState.SP);
    }

    [Fact]
    public void INX_Takes2Cycles()
    {
        LoadAndReset([0xE8]);
        CurrentState.X = 0x05;
        CurrentState.X = 0x05;
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
        Assert.Equal(0x06, CurrentState.X);
    }

    [Fact]
    public void INY_Takes2Cycles()
    {
        LoadAndReset([0xC8]);
        CurrentState.Y = 0x05;
        CurrentState.Y = 0x05;
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
        Assert.Equal(0x06, CurrentState.Y);
    }

    [Fact]
    public void DEX_Takes2Cycles()
    {
        LoadAndReset([0xCA]);
        CurrentState.X = 0x05;
        CurrentState.X = 0x05;
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
        Assert.Equal(0x04, CurrentState.X);
    }

    [Fact]
    public void DEY_Takes2Cycles()
    {
        LoadAndReset([0x88]);
        CurrentState.Y = 0x05;
        CurrentState.Y = 0x05;
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
        Assert.Equal(0x04, CurrentState.Y);
    }

    #endregion

    #region Flag Operations

    [Fact]
    public void CLC_Takes2Cycles()
    {
        LoadAndReset([0x18]);
        CurrentState.CarryFlag = true;
        CurrentState.CarryFlag = true;
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
        Assert.False(CurrentState.CarryFlag);
    }

    [Fact]
    public void SEC_Takes2Cycles()
    {
        LoadAndReset([0x38]);
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
        Assert.True(CurrentState.CarryFlag);
    }

    [Fact]
    public void CLD_Takes2Cycles()
    {
        LoadAndReset([0xD8]);
        CurrentState.DecimalFlag = true;
        CurrentState.DecimalFlag = true;
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
        Assert.False(CurrentState.DecimalFlag);
    }

    [Fact]
    public void SED_Takes2Cycles()
    {
        LoadAndReset([0xF8]);
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
        Assert.True(CurrentState.DecimalFlag);
    }

    [Fact]
    public void CLI_Takes2Cycles()
    {
        LoadAndReset([0x58]);
        CurrentState.InterruptDisableFlag = true;
        CurrentState.InterruptDisableFlag = true;
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
        Assert.False(CurrentState.InterruptDisableFlag);
    }

    [Fact]
    public void SEI_Takes2Cycles()
    {
        LoadAndReset([0x78]);
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
        Assert.True(CurrentState.InterruptDisableFlag);
    }

    [Fact]
    public void CLV_Takes2Cycles()
    {
        LoadAndReset([0xB8]);
        CurrentState.OverflowFlag = true;
        CurrentState.OverflowFlag = true;
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
        Assert.False(CurrentState.OverflowFlag);
    }

    [Fact]
    public void NOP_Takes2Cycles()
    {
        LoadAndReset([0xEA]);
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
    }

    #endregion
}
