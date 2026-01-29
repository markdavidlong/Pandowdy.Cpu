// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Xunit;

namespace Pandowdy.Cpu.Tests;

/// <summary>
/// Tests for WDC 65C02 CPU.
/// Inherits all core instruction tests and adds 65C02-specific tests.
/// </summary>
public class WDC65C02Tests : CoreInstructionTests
{
    protected override CpuVariant Variant => CpuVariant.WDC65C02;

    #region JMP Indirect Bug Fix

    [Fact]
    public void JMP_Indirect_Takes6Cycles()
    {
        // 65C02 takes 6 cycles for indirect jump (NMOS takes 5)
        LoadAndReset([0x6C, 0x34, 0x12]);
        SetMemory(0x1234, 0x00);
        SetMemory(0x1235, 0x80);
        int cycles = StepInstruction();
        Assert.Equal(6, cycles);
    }

    [Fact]
    public void JMP_Indirect_FixedPageBoundaryBug()
    {
        // On 65C02, JMP ($12FF) correctly reads high byte from $1300
        LoadAndReset([0x6C, 0xFF, 0x12]);
        SetMemory(0x12FF, 0x00);
        SetMemory(0x1200, 0x90);
        SetMemory(0x1300, 0x80);

        StepInstruction();

        Assert.Equal(0x8000, CurrentState.PC);
    }

    #endregion

    #region 65C02-Only Instructions

    [Fact]
    public void STZ_ZeroPage_Takes3Cycles()
    {
        LoadAndReset([0x64, 0x10]);
        Bus.Memory[0x10] = 0xFF;

        int cycles = StepInstruction();

        Assert.Equal(3, cycles);
        Assert.Equal(0x00, Bus.Memory[0x10]);
    }

    [Fact]
    public void STZ_ZeroPageX_Takes4Cycles()
    {
        LoadAndReset([0x74, 0x10]);
        CpuBuffer.Current.X = 5;
        CpuBuffer.Prev.X = 5;
        Bus.Memory[0x15] = 0xFF;

        int cycles = StepInstruction();

        Assert.Equal(4, cycles);
        Assert.Equal(0x00, Bus.Memory[0x15]);
    }

    [Fact]
    public void STZ_Absolute_Takes4Cycles()
    {
        LoadAndReset([0x9C, 0x34, 0x12]);
        Bus.Memory[0x1234] = 0xFF;

        int cycles = StepInstruction();

        Assert.Equal(4, cycles);
        Assert.Equal(0x00, Bus.Memory[0x1234]);
    }

    [Fact]
    public void STZ_AbsoluteX_Takes5Cycles()
    {
        LoadAndReset([0x9E, 0x00, 0x12]);
        CpuBuffer.Current.X = 0x10;
        CpuBuffer.Prev.X = 0x10;
        Bus.Memory[0x1210] = 0xFF;

        int cycles = StepInstruction();

        Assert.Equal(5, cycles);
        Assert.Equal(0x00, Bus.Memory[0x1210]);
    }

    #endregion

    #region 65C02-Only Stack Instructions

    [Fact]
    public void PHX_Takes3Cycles()
    {
        LoadAndReset([0xDA]);
        CpuBuffer.Current.X = 0x42;
        CpuBuffer.Prev.X = 0x42;
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Prev.SP = 0xFF;

        int cycles = StepInstruction();

        Assert.Equal(3, cycles);
        Assert.Equal(0x42, Bus.Memory[0x01FF]);
        Assert.Equal(0xFE, CurrentState.SP);
    }

    [Fact]
    public void PLX_Takes4Cycles()
    {
        LoadAndReset([0xFA]);
        Bus.Memory[0x01FF] = 0x42;
        CpuBuffer.Current.SP = 0xFE;
        CpuBuffer.Prev.SP = 0xFE;

        int cycles = StepInstruction();

        Assert.Equal(4, cycles);
        Assert.Equal(0x42, CurrentState.X);
        Assert.Equal(0xFF, CurrentState.SP);
    }

    [Fact]
    public void PHY_Takes3Cycles()
    {
        LoadAndReset([0x5A]);
        CpuBuffer.Current.Y = 0x42;
        CpuBuffer.Prev.Y = 0x42;
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Prev.SP = 0xFF;

        int cycles = StepInstruction();

        Assert.Equal(3, cycles);
        Assert.Equal(0x42, Bus.Memory[0x01FF]);
    }

    [Fact]
    public void PLY_Takes4Cycles()
    {
        LoadAndReset([0x7A]);
        Bus.Memory[0x01FF] = 0x42;
        CpuBuffer.Current.SP = 0xFE;
        CpuBuffer.Prev.SP = 0xFE;

        int cycles = StepInstruction();

        Assert.Equal(4, cycles);
        Assert.Equal(0x42, CurrentState.Y);
    }

    #endregion

    #region 65C02-Only INC/DEC Accumulator

    [Fact]
    public void INC_Accumulator_Takes2Cycles()
    {
        LoadAndReset([0x1A]);
        CpuBuffer.Current.A = 0x42;
        CpuBuffer.Prev.A = 0x42;

        int cycles = StepInstruction();

        Assert.Equal(2, cycles);
        Assert.Equal(0x43, CurrentState.A);
    }

    [Fact]
    public void INC_Accumulator_SetsFlags()
    {
        LoadAndReset([0x1A]);
        CpuBuffer.Current.A = 0xFF;
        CpuBuffer.Prev.A = 0xFF;

        StepInstruction();

        Assert.Equal(0x00, CurrentState.A);
        Assert.True(CurrentState.ZeroFlag);
    }

    [Fact]
    public void DEC_Accumulator_Takes2Cycles()
    {
        LoadAndReset([0x3A]);
        CpuBuffer.Current.A = 0x42;
        CpuBuffer.Prev.A = 0x42;

        int cycles = StepInstruction();

        Assert.Equal(2, cycles);
        Assert.Equal(0x41, CurrentState.A);
    }

    [Fact]
    public void DEC_Accumulator_SetsFlags()
    {
        LoadAndReset([0x3A]);
        CpuBuffer.Current.A = 0x00;
        CpuBuffer.Prev.A = 0x00;

        StepInstruction();

        Assert.Equal(0xFF, CurrentState.A);
        Assert.True(CurrentState.NegativeFlag);
    }

    #endregion

    #region 65C02-Only BRA (Branch Always)

    [Fact]
    public void BRA_AlwaysTaken_NoPageCross_Takes3Cycles()
    {
        LoadAndReset([0x80, 0x10]);

        int cycles = StepInstruction();

        Assert.Equal(3, cycles);
        Assert.Equal((ushort)(ProgramStart + 2 + 0x10), CurrentState.PC);
    }

    [Fact]
    public void BRA_AlwaysTaken_WithPageCross_Takes4Cycles()
    {
        SetupCpu();
        Bus.Clear();
        Bus.SetResetVector(0x04F0);
        Bus.Memory[0x04F0] = 0x80;
        Bus.Memory[0x04F1] = 0x10;
        Cpu.Reset(CpuBuffer, Bus);

        int cycles = StepInstruction();

        Assert.Equal(4, cycles);
        Assert.Equal(0x0502, CurrentState.PC);
    }

    [Fact]
    public void BRA_BackwardBranch()
    {
        LoadAndReset([0x80, 0xF0]);

        StepInstruction();

        Assert.Equal((ushort)(ProgramStart + 2 - 16), CurrentState.PC);
    }

    #endregion

    #region 65C02-Only TRB/TSB

    [Fact]
    public void TRB_ZeroPage_Takes5Cycles()
    {
        LoadAndReset([0x14, 0x10]);
        CpuBuffer.Current.A = 0x0F;
        CpuBuffer.Prev.A = 0x0F;
        SetZeroPage(0x10, 0xFF);

        int cycles = StepInstruction();

        Assert.Equal(5, cycles);
        Assert.Equal(0xF0, Bus.Memory[0x10]);
    }

    [Fact]
    public void TRB_Absolute_Takes6Cycles()
    {
        LoadAndReset([0x1C, 0x34, 0x12]);
        CpuBuffer.Current.A = 0x0F;
        CpuBuffer.Prev.A = 0x0F;
        SetMemory(0x1234, 0xFF);

        int cycles = StepInstruction();

        Assert.Equal(6, cycles);
        Assert.Equal(0xF0, Bus.Memory[0x1234]);
    }

    [Fact]
    public void TRB_SetsZeroIfNoCommonBits()
    {
        LoadAndReset([0x14, 0x10]);
        CpuBuffer.Current.A = 0xF0;
        CpuBuffer.Prev.A = 0xF0;
        SetZeroPage(0x10, 0x0F);

        StepInstruction();

        Assert.True(CurrentState.ZeroFlag);
    }

    [Fact]
    public void TSB_ZeroPage_Takes5Cycles()
    {
        LoadAndReset([0x04, 0x10]);
        CpuBuffer.Current.A = 0x0F;
        CpuBuffer.Prev.A = 0x0F;
        SetZeroPage(0x10, 0xF0);

        int cycles = StepInstruction();

        Assert.Equal(5, cycles);
        Assert.Equal(0xFF, Bus.Memory[0x10]);
    }

    [Fact]
    public void TSB_Absolute_Takes6Cycles()
    {
        LoadAndReset([0x0C, 0x34, 0x12]);
        CpuBuffer.Current.A = 0x0F;
        CpuBuffer.Prev.A = 0x0F;
        SetMemory(0x1234, 0xF0);

        int cycles = StepInstruction();

        Assert.Equal(6, cycles);
        Assert.Equal(0xFF, Bus.Memory[0x1234]);
    }

    #endregion

    #region 65C02-Only Zero Page Indirect Addressing

    [Fact]
    public void LDA_ZeroPageIndirect_Takes5Cycles()
    {
        LoadAndReset([0xB2, 0x10]);
        SetZeroPagePointer(0x10, 0x1234);
        SetMemory(0x1234, 0x42);

        int cycles = StepInstruction();

        Assert.Equal(5, cycles);
        Assert.Equal(0x42, CurrentState.A);
    }

    [Fact]
    public void STA_ZeroPageIndirect_Takes5Cycles()
    {
        LoadAndReset([0x92, 0x10]);
        CpuBuffer.Current.A = 0x42;
        CpuBuffer.Prev.A = 0x42;
        SetZeroPagePointer(0x10, 0x1234);

        int cycles = StepInstruction();

        Assert.Equal(5, cycles);
        Assert.Equal(0x42, Bus.Memory[0x1234]);
    }

    [Fact]
    public void ADC_ZeroPageIndirect_Takes5Cycles()
    {
        LoadAndReset([0x72, 0x10]);
        CpuBuffer.Current.A = 0x05;
        CpuBuffer.Prev.A = 0x05;
        SetZeroPagePointer(0x10, 0x1234);
        SetMemory(0x1234, 0x10);

        int cycles = StepInstruction();

        Assert.Equal(5, cycles);
        Assert.Equal(0x15, CurrentState.A);
    }

    [Fact]
    public void SBC_ZeroPageIndirect_Takes5Cycles()
    {
        LoadAndReset([0xF2, 0x10]);
        CpuBuffer.Current.A = 0x15;
        CpuBuffer.Prev.A = 0x15;
        CpuBuffer.Current.CarryFlag = true;
        CpuBuffer.Prev.CarryFlag = true;
        SetZeroPagePointer(0x10, 0x1234);
        SetMemory(0x1234, 0x05);

        int cycles = StepInstruction();

        Assert.Equal(5, cycles);
        Assert.Equal(0x10, CurrentState.A);
    }

    [Fact]
    public void AND_ZeroPageIndirect_Takes5Cycles()
    {
        LoadAndReset([0x32, 0x10]);
        CpuBuffer.Current.A = 0xFF;
        CpuBuffer.Prev.A = 0xFF;
        SetZeroPagePointer(0x10, 0x1234);
        SetMemory(0x1234, 0x0F);

        int cycles = StepInstruction();

        Assert.Equal(5, cycles);
        Assert.Equal(0x0F, CurrentState.A);
    }

    [Fact]
    public void ORA_ZeroPageIndirect_Takes5Cycles()
    {
        LoadAndReset([0x12, 0x10]);
        CpuBuffer.Current.A = 0x0F;
        CpuBuffer.Prev.A = 0x0F;
        SetZeroPagePointer(0x10, 0x1234);
        SetMemory(0x1234, 0xF0);

        int cycles = StepInstruction();

        Assert.Equal(5, cycles);
        Assert.Equal(0xFF, CurrentState.A);
    }

    [Fact]
    public void EOR_ZeroPageIndirect_Takes5Cycles()
    {
        LoadAndReset([0x52, 0x10]);
        CpuBuffer.Current.A = 0xAA;
        CpuBuffer.Prev.A = 0xAA;
        SetZeroPagePointer(0x10, 0x1234);
        SetMemory(0x1234, 0xFF);

        int cycles = StepInstruction();

        Assert.Equal(5, cycles);
        Assert.Equal(0x55, CurrentState.A);
    }

    [Fact]
    public void CMP_ZeroPageIndirect_Takes5Cycles()
    {
        LoadAndReset([0xD2, 0x10]);
        CpuBuffer.Current.A = 0x20;
        CpuBuffer.Prev.A = 0x20;
        SetZeroPagePointer(0x10, 0x1234);
        SetMemory(0x1234, 0x10);

        int cycles = StepInstruction();

        Assert.Equal(5, cycles);
        Assert.True(CurrentState.CarryFlag);
    }

    #endregion

    #region 65C02-Only BIT Modes

    [Fact]
    public void BIT_Immediate_Takes2Cycles()
    {
        LoadAndReset([0x89, 0x80]);
        CpuBuffer.Current.A = 0x80;
        CpuBuffer.Prev.A = 0x80;

        int cycles = StepInstruction();

        Assert.Equal(2, cycles);
    }

    [Fact]
    public void BIT_Immediate_OnlyAffectsZeroFlag()
    {
        LoadAndReset([0x89, 0xC0]);
        CpuBuffer.Current.A = 0x00;
        CpuBuffer.Prev.A = 0x00;
        CpuBuffer.Current.NegativeFlag = false;
        CpuBuffer.Prev.NegativeFlag = false;
        CpuBuffer.Current.OverflowFlag = false;
        CpuBuffer.Prev.OverflowFlag = false;

        StepInstruction();

        Assert.True(CurrentState.ZeroFlag);
        Assert.False(CurrentState.NegativeFlag);
        Assert.False(CurrentState.OverflowFlag);
    }

    [Fact]
    public void BIT_ZeroPageX_Takes4Cycles()
    {
        LoadAndReset([0x34, 0x10]);
        CpuBuffer.Current.A = 0xFF;
        CpuBuffer.Prev.A = 0xFF;
        CpuBuffer.Current.X = 5;
        CpuBuffer.Prev.X = 5;
        SetZeroPage(0x15, 0xC0);

        int cycles = StepInstruction();

        Assert.Equal(4, cycles);
    }

    [Fact]
    public void BIT_AbsoluteX_Takes4Cycles_NoPageCross()
    {
        LoadAndReset([0x3C, 0x00, 0x12]);
        CpuBuffer.Current.A = 0xFF;
        CpuBuffer.Prev.A = 0xFF;
        CpuBuffer.Current.X = 0x10;
        CpuBuffer.Prev.X = 0x10;
        SetMemory(0x1210, 0xC0);

        int cycles = StepInstruction();

        Assert.Equal(4, cycles);
    }

    [Fact]
    public void BIT_AbsoluteX_Takes5Cycles_WithPageCross()
    {
        LoadAndReset([0x3C, 0xF0, 0x12]);
        CpuBuffer.Current.A = 0xFF;
        CpuBuffer.Prev.A = 0xFF;
        CpuBuffer.Current.X = 0x20;
        CpuBuffer.Prev.X = 0x20;
        SetMemory(0x1310, 0xC0);

        int cycles = StepInstruction();

        Assert.Equal(5, cycles);
    }

    #endregion

    #region 65C02-Only JMP (abs,X)

    [Fact]
    public void JMP_AbsoluteIndexedIndirect_Takes6Cycles()
    {
        LoadAndReset([0x7C, 0x00, 0x12]);
        CpuBuffer.Current.X = 0x10;
        CpuBuffer.Prev.X = 0x10;
        SetMemory(0x1210, 0x00);
        SetMemory(0x1211, 0x80);

        int cycles = StepInstruction();

        Assert.Equal(6, cycles);
        Assert.Equal(0x8000, CurrentState.PC);
    }

    #endregion

    #region 65C02-Only STP/WAI

    [Fact]
    public void STP_Takes3Cycles()
    {
        LoadAndReset([0xDB]);

        int cycles = StepInstruction();

        Assert.Equal(3, cycles);
        Assert.Equal(CpuStatus.Stopped, CurrentState.Status);
    }

    [Fact]
    public void WAI_Takes3Cycles()
    {
        LoadAndReset([0xCB]);

        int cycles = StepInstruction();

        Assert.Equal(3, cycles);
        Assert.Equal(CpuStatus.Waiting, CurrentState.Status);
    }

    [Fact]
    public void WAI_DoesNotAdvancePC_WhileWaiting()
    {
        // WAI followed by NOP
        LoadAndReset([0xCB, 0xEA]);

        StepInstruction();
        ushort pcAfterWai = CurrentState.PC;

        // Step again while waiting - PC should not advance
        StepInstruction();

        Assert.Equal(CpuStatus.Waiting, CurrentState.Status);
        Assert.Equal(pcAfterWai, CurrentState.PC);
    }

    [Fact]
    public void STP_DoesNotAdvancePC_WhileStopped()
    {
        // STP followed by NOP
        LoadAndReset([0xDB, 0xEA]);

        StepInstruction();
        ushort pcAfterStp = CurrentState.PC;

        // Step again while stopped - PC should not advance
        StepInstruction();

        Assert.Equal(CpuStatus.Stopped, CurrentState.Status);
        Assert.Equal(pcAfterStp, CurrentState.PC);
    }

    [Fact]
    public void WAI_ActsAsNOP_WhenIgnoreHaltStopWaitIsTrue()
    {
        // WAI followed by LDA #$42
        LoadAndReset([0xCB, 0xA9, 0x42]);
        CpuBuffer.Current.IgnoreHaltStopWait = true;
        CpuBuffer.Prev.IgnoreHaltStopWait = true;

        StepInstruction();

        // Should be Bypassed (WAI was encountered but bypassed)
        Assert.Equal(CpuStatus.Bypassed, CurrentState.Status);

        // PC should have advanced past the WAI instruction
        StepInstruction();
        Assert.Equal(0x42, CurrentState.A);
    }

    [Fact]
    public void STP_ActsAsNOP_WhenIgnoreHaltStopWaitIsTrue()
    {
        // STP followed by LDA #$42
        LoadAndReset([0xDB, 0xA9, 0x42]);
        CpuBuffer.Current.IgnoreHaltStopWait = true;
        CpuBuffer.Prev.IgnoreHaltStopWait = true;

        StepInstruction();

        // Should be Bypassed (STP was encountered but bypassed)
        Assert.Equal(CpuStatus.Bypassed, CurrentState.Status);

        // PC should have advanced past the STP instruction
        StepInstruction();
        Assert.Equal(0x42, CurrentState.A);
    }

    #endregion

    #region 65C02 BCD Mode Flag Behavior

    [Fact]
    public void ADC_BCD_NegativeFlag_BasedOnBCDResult_CMOS()
    {
        // CMOS 65C02: N flag is set based on the BCD result, not the binary result
        // 0x79 + 0x10 = 0x89 (BCD result has bit 7 set)
        LoadAndReset([0x69, 0x10]);  // ADC #$10
        CpuBuffer.Current.A = 0x79;
        CpuBuffer.Prev.A = 0x79;
        CpuBuffer.Current.DecimalFlag = true;
        CpuBuffer.Prev.DecimalFlag = true;
        CpuBuffer.Current.CarryFlag = false;
        CpuBuffer.Prev.CarryFlag = false;

        StepInstruction();

        // BCD result: 0x89 (bit 7 set, so N = true)
        Assert.Equal(0x89, CurrentState.A);
        Assert.True(CurrentState.NegativeFlag, "CMOS: N flag should be based on BCD result (0x89 has bit 7 set)");
    }

    [Fact]
    public void ADC_BCD_ZeroFlag_BasedOnBCDResult_CMOS()
    {
        // CMOS 65C02: Z flag is set based on the BCD result
        // 0x99 + 0x01 = 0x00 (BCD with carry), Z should be true for CMOS
        LoadAndReset([0x69, 0x01]);  // ADC #$01
        CpuBuffer.Current.A = 0x99;
        CpuBuffer.Prev.A = 0x99;
        CpuBuffer.Current.DecimalFlag = true;
        CpuBuffer.Prev.DecimalFlag = true;
        CpuBuffer.Current.CarryFlag = false;
        CpuBuffer.Prev.CarryFlag = false;

        StepInstruction();

        // BCD result: 0x00 (zero)
        Assert.Equal(0x00, CurrentState.A);
        Assert.True(CurrentState.CarryFlag);  // Carry set due to BCD overflow
        Assert.True(CurrentState.ZeroFlag, "CMOS: Z flag should be based on BCD result (0x00 is zero)");
    }

    [Fact]
    public void SBC_BCD_NegativeFlag_BasedOnBCDResult_CMOS()
    {
        // CMOS 65C02: N flag is set based on the BCD result
        // 0x00 - 0x01 = 0x99 (BCD with borrow), BCD result 0x99 has bit 7 set
        LoadAndReset([0xE9, 0x01]);  // SBC #$01
        CpuBuffer.Current.A = 0x00;
        CpuBuffer.Prev.A = 0x00;
        CpuBuffer.Current.DecimalFlag = true;
        CpuBuffer.Prev.DecimalFlag = true;
        CpuBuffer.Current.CarryFlag = true;  // No borrow
        CpuBuffer.Prev.CarryFlag = true;

        StepInstruction();

        // BCD result: 0x99 (bit 7 set, so N = true)
        Assert.Equal(0x99, CurrentState.A);
        Assert.True(CurrentState.NegativeFlag, "CMOS: N flag should be based on BCD result (0x99 has bit 7 set)");
    }

    [Fact]
    public void SBC_BCD_ZeroFlag_BasedOnBCDResult_CMOS()
    {
        // CMOS 65C02: Z flag is set based on the BCD result
        // 0x01 - 0x01 = 0x00 (both BCD and binary are zero)
        LoadAndReset([0xE9, 0x01]);  // SBC #$01
        CpuBuffer.Current.A = 0x01;
        CpuBuffer.Prev.A = 0x01;
        CpuBuffer.Current.DecimalFlag = true;
        CpuBuffer.Prev.DecimalFlag = true;
        CpuBuffer.Current.CarryFlag = true;  // No borrow
        CpuBuffer.Prev.CarryFlag = true;

        StepInstruction();

        // BCD result: 0x00 (zero)
        Assert.Equal(0x00, CurrentState.A);
        Assert.True(CurrentState.ZeroFlag, "Z flag should be set when result is zero");
    }

    #endregion

    #region 65C02 NOP Variant Timing Tests

    // 1-cycle NOPs (1-byte) - $x3 pattern
    [Theory]
    [InlineData(0x03)]
    [InlineData(0x13)]
    [InlineData(0x23)]
    [InlineData(0x33)]
    [InlineData(0x43)]
    [InlineData(0x53)]
    [InlineData(0x63)]
    [InlineData(0x73)]
    [InlineData(0x83)]
    [InlineData(0x93)]
    [InlineData(0xA3)]
    [InlineData(0xB3)]
    [InlineData(0xC3)]
    [InlineData(0xD3)]
    [InlineData(0xE3)]
    [InlineData(0xF3)]
    public void NOP_1Cycle_X3Pattern_Takes1Cycle(byte opcode)
    {
        LoadAndReset(opcode);
        ushort expectedPC = (ushort)(ProgramStart + 1);

        int cycles = StepInstruction();

        Assert.Equal(1, cycles);
        Assert.Equal(expectedPC, CurrentState.PC);
    }

    // 1-cycle NOPs (1-byte) - $xB pattern (excluding $CB=WAI, $DB=STP on WDC)
    [Theory]
    [InlineData(0x0B)]
    [InlineData(0x1B)]
    [InlineData(0x2B)]
    [InlineData(0x3B)]
    [InlineData(0x4B)]
    [InlineData(0x5B)]
    [InlineData(0x6B)]
    [InlineData(0x7B)]
    [InlineData(0x8B)]
    [InlineData(0x9B)]
    [InlineData(0xAB)]
    [InlineData(0xBB)]
    [InlineData(0xEB)]
    [InlineData(0xFB)]
    public void NOP_1Cycle_XBPattern_Takes1Cycle(byte opcode)
    {
        LoadAndReset(opcode);
        ushort expectedPC = (ushort)(ProgramStart + 1);

        int cycles = StepInstruction();

        Assert.Equal(1, cycles);
        Assert.Equal(expectedPC, CurrentState.PC);
    }

    // 2-cycle NOPs (2-byte immediate style) - $x2 pattern
    [Theory]
    [InlineData(0x02)]
    [InlineData(0x22)]
    [InlineData(0x42)]
    [InlineData(0x62)]
    [InlineData(0x82)]
    [InlineData(0xC2)]
    [InlineData(0xE2)]
    public void NOP_2Cycle_Immediate_Takes2Cycles(byte opcode)
    {
        LoadAndReset(opcode, 0x00);
        ushort expectedPC = (ushort)(ProgramStart + 2);

        int cycles = StepInstruction();

        Assert.Equal(2, cycles);
        Assert.Equal(expectedPC, CurrentState.PC);
    }

    // 3-cycle NOP (2-byte ZP style) - 0x44
    [Fact]
    public void NOP_3Cycle_ZeroPage_0x44_Takes3Cycles()
    {
        LoadAndReset([0x44, 0x10]);
        ushort expectedPC = (ushort)(ProgramStart + 2);

        int cycles = StepInstruction();

        Assert.Equal(3, cycles);
        Assert.Equal(expectedPC, CurrentState.PC);
    }

    // 4-cycle NOPs (2-byte ZP,X style)
    [Theory]
    [InlineData(0x54)]
    [InlineData(0xD4)]
    [InlineData(0xF4)]
    public void NOP_4Cycle_ZeroPageX_Takes4Cycles(byte opcode)
    {
        LoadAndReset(opcode, 0x10);
        ushort expectedPC = (ushort)(ProgramStart + 2);

        int cycles = StepInstruction();

        Assert.Equal(4, cycles);
        Assert.Equal(expectedPC, CurrentState.PC);
    }

    // 4-cycle NOPs (3-byte absolute style) - 65C02 specific
    // These read the high byte address twice (T2 and T3)
    [Theory]
    [InlineData(0x5C)]
    [InlineData(0xDC)]
    [InlineData(0xFC)]
    public void NOP_4Cycle_Absolute_Takes4Cycles(byte opcode)
    {
        LoadAndReset(opcode, 0x34, 0x12);
        ushort expectedPC = (ushort)(ProgramStart + 3);

        int cycles = StepInstruction();

        Assert.Equal(4, cycles);
        Assert.Equal(expectedPC, CurrentState.PC);
    }

    // Verify NOPs don't affect registers or flags
    [Fact]
    public void NOP_DoesNotAffectRegistersOrFlags()
    {
        LoadAndReset([0x03]); // 1-cycle NOP
        CpuBuffer.Current.A = 0x42;
        CpuBuffer.Prev.A = 0x42;
        CpuBuffer.Current.X = 0x33;
        CpuBuffer.Prev.X = 0x33;
        CpuBuffer.Current.Y = 0x22;
        CpuBuffer.Prev.Y = 0x22;
        CpuBuffer.Current.CarryFlag = true;
        CpuBuffer.Prev.CarryFlag = true;
        CpuBuffer.Current.ZeroFlag = true;
        CpuBuffer.Prev.ZeroFlag = true;

        StepInstruction();

        Assert.Equal(0x42, CurrentState.A);
        Assert.Equal(0x33, CurrentState.X);
        Assert.Equal(0x22, CurrentState.Y);
        Assert.True(CurrentState.CarryFlag);
        Assert.True(CurrentState.ZeroFlag);
    }

    #endregion

        #region Complete Opcode Coverage

        public static IEnumerable<object[]> AllOpcodes()
        {
            for (int i = 0; i < 256; i++)
            {
                yield return [(byte)i];
            }
        }

        [Theory]
        [MemberData(nameof(AllOpcodes))]
        public void AllOpcodesExecuteWithoutCrashing(byte opcode)
        {
            // Provide 3 bytes of operand data to cover all addressing modes
            LoadAndReset([opcode, 0x00, 0x04]);
            // Set up registers to avoid edge cases
            CpuBuffer.Current.A = 0x00;
            CpuBuffer.Prev.A = 0x00;
            CpuBuffer.Current.X = 0x00;
            CpuBuffer.Prev.X = 0x00;
            CpuBuffer.Current.Y = 0x00;
            CpuBuffer.Prev.Y = 0x00;
            CpuBuffer.Current.SP = 0xFF;
            CpuBuffer.Prev.SP = 0xFF;

            int cycles = StepInstruction();

            // Every opcode should either execute normally, stop, wait, or be bypassed (65C02 has no JAM opcodes)
            Assert.True(cycles > 0, $"Opcode 0x{opcode:X2} should take at least 1 cycle");
            Assert.True(
                CurrentState.Status == CpuStatus.Running ||
                CurrentState.Status == CpuStatus.Jammed ||
                CurrentState.Status == CpuStatus.Stopped ||
                CurrentState.Status == CpuStatus.Waiting ||
                CurrentState.Status == CpuStatus.Bypassed,
                $"Opcode 0x{opcode:X2} resulted in unexpected status: {CurrentState.Status}");
        }

        #endregion
    }
