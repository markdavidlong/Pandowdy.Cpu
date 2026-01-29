// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Xunit;

namespace Pandowdy.Cpu.Tests;

/// <summary>
/// Comprehensive opcode contract tests for NMOS 6502.
/// These tests verify that each opcode's behavioral contract is correctly implemented.
/// Unlike the Harte SST tests, these focus on functional correctness rather than cycle-level accuracy.
/// </summary>
public class NMOS6502OpcodeTests : CpuTestBase
{
    protected override CpuVariant Variant => CpuVariant.NMOS6502;

    #region LDA - Load Accumulator (All Addressing Modes)

    [Theory]
    [InlineData(0x00, true, false)]    // Zero value - Z set, N clear
    [InlineData(0x01, false, false)]   // Positive value - Z clear, N clear
    [InlineData(0x7F, false, false)]   // Max positive - Z clear, N clear
    [InlineData(0x80, false, true)]    // Min negative - Z clear, N set
    [InlineData(0xFF, false, true)]    // Max negative - Z clear, N set
    public void LDA_Immediate_SetsFlags(byte value, bool expectZ, bool expectN)
    {
        LoadAndReset(0xA9, value);
        StepInstruction();
        Assert.Equal(value, CurrentState.A);
        Assert.Equal(expectZ, CurrentState.ZeroFlag);
        Assert.Equal(expectN, CurrentState.NegativeFlag);
    }

    [Fact]
    public void LDA_ZeroPage_LoadsCorrectValue()
    {
        LoadAndReset([0xA5, 0x42]);
        SetZeroPage(0x42, 0xAB);
        StepInstruction();
        Assert.Equal(0xAB, CurrentState.A);
    }

    [Fact]
    public void LDA_ZeroPageX_WrapsWithinZeroPage()
    {
        // When ZP + X > 0xFF, it wraps within zero page
        LoadAndReset([0xB5, 0xF0]);
        CpuBuffer.Current.X = 0x20; // 0xF0 + 0x20 = 0x110 -> wraps to 0x10
        CpuBuffer.Prev.X = 0x20;
        SetZeroPage(0x10, 0xCD);
        StepInstruction();
        Assert.Equal(0xCD, CurrentState.A);
    }

    [Fact]
    public void LDA_Absolute_LoadsFromFullAddress()
    {
        LoadAndReset([0xAD, 0x78, 0x56]); // LDA $5678
        SetMemory(0x5678, 0xEF);
        StepInstruction();
        Assert.Equal(0xEF, CurrentState.A);
    }

    [Fact]
    public void LDA_AbsoluteX_NoPageCross()
    {
        LoadAndReset([0xBD, 0x00, 0x12]); // LDA $1200,X
        CpuBuffer.Current.X = 0x34;
        CpuBuffer.Prev.X = 0x34;
        SetMemory(0x1234, 0x77);
        int cycles = StepInstruction();
        Assert.Equal(4, cycles);
        Assert.Equal(0x77, CurrentState.A);
    }

    [Fact]
    public void LDA_AbsoluteX_WithPageCross()
    {
        LoadAndReset([0xBD, 0xFF, 0x12]); // LDA $12FF,X
        CpuBuffer.Current.X = 0x01; // Crosses to 0x1300
        CpuBuffer.Prev.X = 0x01;
        SetMemory(0x1300, 0x88);
        int cycles = StepInstruction();
        Assert.Equal(5, cycles);
        Assert.Equal(0x88, CurrentState.A);
    }

    [Fact]
    public void LDA_AbsoluteY_NoPageCross()
    {
        LoadAndReset([0xB9, 0x00, 0x20]); // LDA $2000,Y
        CpuBuffer.Current.Y = 0x10;
        CpuBuffer.Prev.Y = 0x10;
        SetMemory(0x2010, 0x99);
        int cycles = StepInstruction();
        Assert.Equal(4, cycles);
        Assert.Equal(0x99, CurrentState.A);
    }

    [Fact]
    public void LDA_AbsoluteY_WithPageCross()
    {
        LoadAndReset([0xB9, 0xF0, 0x20]); // LDA $20F0,Y
        CpuBuffer.Current.Y = 0x20; // Crosses to 0x2110
        CpuBuffer.Prev.Y = 0x20;
        SetMemory(0x2110, 0xAA);
        int cycles = StepInstruction();
        Assert.Equal(5, cycles);
        Assert.Equal(0xAA, CurrentState.A);
    }

    [Fact]
    public void LDA_IndexedIndirectX_LoadsViaPointer()
    {
        // LDA ($10,X) where X=5, so pointer at $15
        LoadAndReset([0xA1, 0x10]);
        CpuBuffer.Current.X = 0x05;
        CpuBuffer.Prev.X = 0x05;
        SetZeroPagePointer(0x15, 0x3000); // Pointer at $15 -> $3000
        SetMemory(0x3000, 0xBB);
        int cycles = StepInstruction();
        Assert.Equal(6, cycles);
        Assert.Equal(0xBB, CurrentState.A);
    }

    [Fact]
    public void LDA_IndexedIndirectX_WrapsPointerInZeroPage()
    {
        // LDA ($FF,X) where X=2 -> pointer at $01 (wraps)
        LoadAndReset([0xA1, 0xFF]);
        CpuBuffer.Current.X = 0x02;
        CpuBuffer.Prev.X = 0x02;
        SetZeroPagePointer(0x01, 0x4000);
        SetMemory(0x4000, 0xCC);
        StepInstruction();
        Assert.Equal(0xCC, CurrentState.A);
    }

    [Fact]
    public void LDA_IndirectIndexedY_NoPageCross()
    {
        // LDA ($20),Y - pointer at $20, add Y
        LoadAndReset([0xB1, 0x20]);
        CpuBuffer.Current.Y = 0x10;
        CpuBuffer.Prev.Y = 0x10;
        SetZeroPagePointer(0x20, 0x5000);
        SetMemory(0x5010, 0xDD);
        int cycles = StepInstruction();
        Assert.Equal(5, cycles);
        Assert.Equal(0xDD, CurrentState.A);
    }

    [Fact]
    public void LDA_IndirectIndexedY_WithPageCross()
    {
        // LDA ($30),Y with page cross
        LoadAndReset([0xB1, 0x30]);
        CpuBuffer.Current.Y = 0x80;
        CpuBuffer.Prev.Y = 0x80;
        SetZeroPagePointer(0x30, 0x50F0); // 0x50F0 + 0x80 = 0x5170 (page cross)
        SetMemory(0x5170, 0xEE);
        int cycles = StepInstruction();
        Assert.Equal(6, cycles);
        Assert.Equal(0xEE, CurrentState.A);
    }

    #endregion

    #region LDX - Load X Register (All Addressing Modes)

    [Theory]
    [InlineData(0x00, true, false)]
    [InlineData(0x7F, false, false)]
    [InlineData(0x80, false, true)]
    [InlineData(0xFF, false, true)]
    public void LDX_Immediate_SetsFlags(byte value, bool expectZ, bool expectN)
    {
        LoadAndReset(0xA2, value);
        StepInstruction();
        Assert.Equal(value, CurrentState.X);
        Assert.Equal(expectZ, CurrentState.ZeroFlag);
        Assert.Equal(expectN, CurrentState.NegativeFlag);
    }

    [Fact]
    public void LDX_ZeroPage_LoadsCorrectValue()
    {
        LoadAndReset([0xA6, 0x50]);
        SetZeroPage(0x50, 0x12);
        StepInstruction();
        Assert.Equal(0x12, CurrentState.X);
    }

    [Fact]
    public void LDX_ZeroPageY_WrapsWithinZeroPage()
    {
        LoadAndReset([0xB6, 0xF0]);
        CpuBuffer.Current.Y = 0x20; // Wraps to 0x10
        CpuBuffer.Prev.Y = 0x20;
        SetZeroPage(0x10, 0x34);
        StepInstruction();
        Assert.Equal(0x34, CurrentState.X);
    }

    [Fact]
    public void LDX_Absolute_LoadsFromFullAddress()
    {
        LoadAndReset([0xAE, 0xCD, 0xAB]); // LDX $ABCD
        SetMemory(0xABCD, 0x56);
        StepInstruction();
        Assert.Equal(0x56, CurrentState.X);
    }

    [Fact]
    public void LDX_AbsoluteY_WithPageCross()
    {
        LoadAndReset([0xBE, 0xFF, 0x30]); // LDX $30FF,Y
        CpuBuffer.Current.Y = 0x02;
        CpuBuffer.Prev.Y = 0x02;
        SetMemory(0x3101, 0x78);
        int cycles = StepInstruction();
        Assert.Equal(5, cycles);
        Assert.Equal(0x78, CurrentState.X);
    }

    #endregion

    #region LDY - Load Y Register (All Addressing Modes)

    [Theory]
    [InlineData(0x00, true, false)]
    [InlineData(0x7F, false, false)]
    [InlineData(0x80, false, true)]
    public void LDY_Immediate_SetsFlags(byte value, bool expectZ, bool expectN)
    {
        LoadAndReset(0xA0, value);
        StepInstruction();
        Assert.Equal(value, CurrentState.Y);
        Assert.Equal(expectZ, CurrentState.ZeroFlag);
        Assert.Equal(expectN, CurrentState.NegativeFlag);
    }

    [Fact]
    public void LDY_ZeroPageX_WrapsWithinZeroPage()
    {
        LoadAndReset([0xB4, 0xFE]);
        CpuBuffer.Current.X = 0x10; // Wraps to 0x0E
        CpuBuffer.Prev.X = 0x10;
        SetZeroPage(0x0E, 0x9A);
        StepInstruction();
        Assert.Equal(0x9A, CurrentState.Y);
    }

    [Fact]
    public void LDY_AbsoluteX_WithPageCross()
    {
        LoadAndReset([0xBC, 0xF0, 0x40]); // LDY $40F0,X
        CpuBuffer.Current.X = 0x20;
        CpuBuffer.Prev.X = 0x20;
        SetMemory(0x4110, 0xBC);
        int cycles = StepInstruction();
        Assert.Equal(5, cycles);
        Assert.Equal(0xBC, CurrentState.Y);
    }

    #endregion

    #region STA - Store Accumulator (All Addressing Modes)

    [Fact]
    public void STA_ZeroPage_StoresValue()
    {
        LoadAndReset([0x85, 0x40]);
        CpuBuffer.Current.A = 0xAB;
        CpuBuffer.Prev.A = 0xAB;
        StepInstruction();
        Assert.Equal(0xAB, Bus.Memory[0x40]);
    }

    [Fact]
    public void STA_ZeroPageX_WrapsWithinZeroPage()
    {
        LoadAndReset([0x95, 0xF0]);
        CpuBuffer.Current.A = 0xCD;
        CpuBuffer.Prev.A = 0xCD;
        CpuBuffer.Current.X = 0x20; // Wraps to 0x10
        CpuBuffer.Prev.X = 0x20;
        StepInstruction();
        Assert.Equal(0xCD, Bus.Memory[0x10]);
    }

    [Fact]
    public void STA_Absolute_StoresAtAddress()
    {
        LoadAndReset([0x8D, 0x00, 0x80]); // STA $8000
        CpuBuffer.Current.A = 0xEF;
        CpuBuffer.Prev.A = 0xEF;
        StepInstruction();
        Assert.Equal(0xEF, Bus.Memory[0x8000]);
    }

    [Fact]
    public void STA_AbsoluteX_AlwaysTakes5Cycles()
    {
        // Store instructions don't have page-cross penalty optimization
        LoadAndReset([0x9D, 0x00, 0x70]);
        CpuBuffer.Current.A = 0x11;
        CpuBuffer.Prev.A = 0x11;
        CpuBuffer.Current.X = 0x10;
        CpuBuffer.Prev.X = 0x10;
        int cycles = StepInstruction();
        Assert.Equal(5, cycles);
        Assert.Equal(0x11, Bus.Memory[0x7010]);
    }

    [Fact]
    public void STA_AbsoluteY_AlwaysTakes5Cycles()
    {
        LoadAndReset([0x99, 0x00, 0x60]);
        CpuBuffer.Current.A = 0x22;
        CpuBuffer.Prev.A = 0x22;
        CpuBuffer.Current.Y = 0x05;
        CpuBuffer.Prev.Y = 0x05;
        int cycles = StepInstruction();
        Assert.Equal(5, cycles);
        Assert.Equal(0x22, Bus.Memory[0x6005]);
    }

    [Fact]
    public void STA_IndexedIndirectX_StoresViaPointer()
    {
        LoadAndReset([0x81, 0x20]);
        CpuBuffer.Current.A = 0x33;
        CpuBuffer.Prev.A = 0x33;
        CpuBuffer.Current.X = 0x05;
        CpuBuffer.Prev.X = 0x05;
        SetZeroPagePointer(0x25, 0x9000);
        StepInstruction();
        Assert.Equal(0x33, Bus.Memory[0x9000]);
    }

    [Fact]
    public void STA_IndirectIndexedY_AlwaysTakes6Cycles()
    {
        LoadAndReset([0x91, 0x40]);
        CpuBuffer.Current.A = 0x44;
        CpuBuffer.Prev.A = 0x44;
        CpuBuffer.Current.Y = 0x10;
        CpuBuffer.Prev.Y = 0x10;
        SetZeroPagePointer(0x40, 0xA000);
        int cycles = StepInstruction();
        Assert.Equal(6, cycles);
        Assert.Equal(0x44, Bus.Memory[0xA010]);
    }

    #endregion

    #region STX - Store X Register

    [Fact]
    public void STX_ZeroPage_StoresValue()
    {
        LoadAndReset([0x86, 0x50]);
        CpuBuffer.Current.X = 0x55;
        CpuBuffer.Prev.X = 0x55;
        StepInstruction();
        Assert.Equal(0x55, Bus.Memory[0x50]);
    }

    [Fact]
    public void STX_ZeroPageY_WrapsWithinZeroPage()
    {
        LoadAndReset([0x96, 0xF5]);
        CpuBuffer.Current.X = 0x66;
        CpuBuffer.Prev.X = 0x66;
        CpuBuffer.Current.Y = 0x15; // Wraps to 0x0A
        CpuBuffer.Prev.Y = 0x15;
        StepInstruction();
        Assert.Equal(0x66, Bus.Memory[0x0A]);
    }

    [Fact]
    public void STX_Absolute_StoresAtAddress()
    {
        LoadAndReset([0x8E, 0x00, 0xB0]); // STX $B000
        CpuBuffer.Current.X = 0x77;
        CpuBuffer.Prev.X = 0x77;
        StepInstruction();
        Assert.Equal(0x77, Bus.Memory[0xB000]);
    }

    #endregion

    #region STY - Store Y Register

    [Fact]
    public void STY_ZeroPage_StoresValue()
    {
        LoadAndReset([0x84, 0x60]);
        CpuBuffer.Current.Y = 0x88;
        CpuBuffer.Prev.Y = 0x88;
        StepInstruction();
        Assert.Equal(0x88, Bus.Memory[0x60]);
    }

    [Fact]
    public void STY_ZeroPageX_WrapsWithinZeroPage()
    {
        LoadAndReset([0x94, 0xFA]);
        CpuBuffer.Current.Y = 0x99;
        CpuBuffer.Prev.Y = 0x99;
        CpuBuffer.Current.X = 0x10; // Wraps to 0x0A
        CpuBuffer.Prev.X = 0x10;
        StepInstruction();
        Assert.Equal(0x99, Bus.Memory[0x0A]);
    }

    [Fact]
    public void STY_Absolute_StoresAtAddress()
    {
        LoadAndReset([0x8C, 0x00, 0xC0]); // STY $C000
        CpuBuffer.Current.Y = 0xAA;
        CpuBuffer.Prev.Y = 0xAA;
        StepInstruction();
        Assert.Equal(0xAA, Bus.Memory[0xC000]);
    }

    #endregion

    #region ADC - Add with Carry (Comprehensive Tests)

    [Theory]
    [InlineData(0x00, 0x00, false, 0x00, false, true, false)]   // 0 + 0 = 0
    [InlineData(0x00, 0x01, false, 0x01, false, false, false)]  // 0 + 1 = 1
    [InlineData(0x7F, 0x01, false, 0x80, false, false, true)]   // 127 + 1 = -128 (overflow)
    [InlineData(0xFF, 0x01, false, 0x00, true, true, false)]    // 255 + 1 = 0 (carry)
    [InlineData(0x80, 0x80, false, 0x00, true, true, true)]     // -128 + -128 = 0 (carry + overflow)
    [InlineData(0x50, 0x50, false, 0xA0, false, false, true)]   // 80 + 80 = 160 (overflow)
    [InlineData(0x00, 0x00, true, 0x01, false, false, false)]   // 0 + 0 + C = 1
    [InlineData(0xFF, 0x00, true, 0x00, true, true, false)]     // 255 + 0 + C = 0 (carry)
    public void ADC_Immediate_ComputesCorrectly(byte a, byte operand, bool carryIn,
        byte expectedA, bool expectedC, bool expectedZ, bool expectedV)
    {
        LoadAndReset(0x69, operand);
        CpuBuffer.Current.A = a;
        CpuBuffer.Prev.A = a;
        CpuBuffer.Current.CarryFlag = carryIn;
        CpuBuffer.Prev.CarryFlag = carryIn;
        StepInstruction();
        Assert.Equal(expectedA, CurrentState.A);
        Assert.Equal(expectedC, CurrentState.CarryFlag);
        Assert.Equal(expectedZ, CurrentState.ZeroFlag);
        Assert.Equal(expectedV, CurrentState.OverflowFlag);
    }

    [Fact]
    public void ADC_ZeroPage_AddsFromMemory()
    {
        LoadAndReset([0x65, 0x30]);
        CpuBuffer.Current.A = 0x10;
        CpuBuffer.Prev.A = 0x10;
        SetZeroPage(0x30, 0x20);
        StepInstruction();
        Assert.Equal(0x30, CurrentState.A);
    }

    [Fact]
    public void ADC_Absolute_Takes4Cycles()
    {
        LoadAndReset([0x6D, 0x00, 0x50]);
        CpuBuffer.Current.A = 0x05;
        CpuBuffer.Prev.A = 0x05;
        SetMemory(0x5000, 0x03);
        int cycles = StepInstruction();
        Assert.Equal(4, cycles);
        Assert.Equal(0x08, CurrentState.A);
    }

    [Fact]
    public void ADC_IndexedIndirectX_Takes6Cycles()
    {
        LoadAndReset([0x61, 0x10]);
        CpuBuffer.Current.A = 0x01;
        CpuBuffer.Prev.A = 0x01;
        CpuBuffer.Current.X = 0x05;
        CpuBuffer.Prev.X = 0x05;
        SetZeroPagePointer(0x15, 0x6000);
        SetMemory(0x6000, 0x02);
        int cycles = StepInstruction();
        Assert.Equal(6, cycles);
        Assert.Equal(0x03, CurrentState.A);
    }

    #endregion

    #region SBC - Subtract with Carry (Comprehensive Tests)

    [Theory]
    [InlineData(0x10, 0x05, true, 0x0B, true, false, false)]    // 16 - 5 = 11
    [InlineData(0x05, 0x05, true, 0x00, true, true, false)]     // 5 - 5 = 0
    [InlineData(0x00, 0x01, true, 0xFF, false, false, false)]   // 0 - 1 = -1 (borrow)
    [InlineData(0x80, 0x01, true, 0x7F, true, false, true)]     // -128 - 1 = 127 (overflow)
    [InlineData(0x7F, 0xFF, true, 0x80, false, false, true)]    // 127 - (-1) = -128 (overflow)
    [InlineData(0x10, 0x05, false, 0x0A, true, false, false)]   // 16 - 5 - 1 = 10
    public void SBC_Immediate_ComputesCorrectly(byte a, byte operand, bool carryIn,
        byte expectedA, bool expectedC, bool expectedZ, bool expectedV)
    {
        LoadAndReset(0xE9, operand);
        CpuBuffer.Current.A = a;
        CpuBuffer.Prev.A = a;
        CpuBuffer.Current.CarryFlag = carryIn;
        CpuBuffer.Prev.CarryFlag = carryIn;
        StepInstruction();
        Assert.Equal(expectedA, CurrentState.A);
        Assert.Equal(expectedC, CurrentState.CarryFlag);
        Assert.Equal(expectedZ, CurrentState.ZeroFlag);
        Assert.Equal(expectedV, CurrentState.OverflowFlag);
    }

    [Fact]
    public void SBC_ZeroPage_SubtractsFromMemory()
    {
        LoadAndReset([0xE5, 0x40]);
        CpuBuffer.Current.A = 0x30;
        CpuBuffer.Prev.A = 0x30;
        CpuBuffer.Current.CarryFlag = true;
        CpuBuffer.Prev.CarryFlag = true;
        SetZeroPage(0x40, 0x10);
        StepInstruction();
        Assert.Equal(0x20, CurrentState.A);
    }

    #endregion

    #region AND - Logical AND (All Addressing Modes)

    [Theory]
    [InlineData(0xFF, 0x0F, 0x0F)]
    [InlineData(0xF0, 0x0F, 0x00)]
    [InlineData(0xAA, 0x55, 0x00)]
    [InlineData(0xFF, 0xFF, 0xFF)]
    [InlineData(0x00, 0xFF, 0x00)]
    public void AND_Immediate_ComputesCorrectly(byte a, byte operand, byte expected)
    {
        LoadAndReset(0x29, operand);
        CpuBuffer.Current.A = a;
        CpuBuffer.Prev.A = a;
        StepInstruction();
        Assert.Equal(expected, CurrentState.A);
        Assert.Equal(expected == 0, CurrentState.ZeroFlag);
        Assert.Equal((expected & 0x80) != 0, CurrentState.NegativeFlag);
    }

    [Fact]
    public void AND_ZeroPage_Takes3Cycles()
    {
        LoadAndReset([0x25, 0x50]);
        CpuBuffer.Current.A = 0xFF;
        CpuBuffer.Prev.A = 0xFF;
        SetZeroPage(0x50, 0xF0);
        int cycles = StepInstruction();
        Assert.Equal(3, cycles);
        Assert.Equal(0xF0, CurrentState.A);
    }

    [Fact]
    public void AND_AbsoluteX_WithPageCross()
    {
        LoadAndReset([0x3D, 0xFF, 0x10]);
        CpuBuffer.Current.A = 0xFF;
        CpuBuffer.Prev.A = 0xFF;
        CpuBuffer.Current.X = 0x01;
        CpuBuffer.Prev.X = 0x01;
        SetMemory(0x1100, 0x55);
        int cycles = StepInstruction();
        Assert.Equal(5, cycles);
        Assert.Equal(0x55, CurrentState.A);
    }

    #endregion

    #region ORA - Logical OR (All Addressing Modes)

    [Theory]
    [InlineData(0x00, 0x00, 0x00)]
    [InlineData(0xF0, 0x0F, 0xFF)]
    [InlineData(0xAA, 0x55, 0xFF)]
    [InlineData(0x80, 0x00, 0x80)]
    public void ORA_Immediate_ComputesCorrectly(byte a, byte operand, byte expected)
    {
        LoadAndReset(0x09, operand);
        CpuBuffer.Current.A = a;
        CpuBuffer.Prev.A = a;
        StepInstruction();
        Assert.Equal(expected, CurrentState.A);
        Assert.Equal(expected == 0, CurrentState.ZeroFlag);
        Assert.Equal((expected & 0x80) != 0, CurrentState.NegativeFlag);
    }

    [Fact]
    public void ORA_IndirectIndexedY_WithPageCross()
    {
        LoadAndReset([0x11, 0x60]);
        CpuBuffer.Current.A = 0x0F;
        CpuBuffer.Prev.A = 0x0F;
        CpuBuffer.Current.Y = 0x80;
        CpuBuffer.Prev.Y = 0x80;
        SetZeroPagePointer(0x60, 0x70F0);
        SetMemory(0x7170, 0xF0);
        int cycles = StepInstruction();
        Assert.Equal(6, cycles);
        Assert.Equal(0xFF, CurrentState.A);
    }

    #endregion

    #region EOR - Exclusive OR (All Addressing Modes)

    [Theory]
    [InlineData(0xFF, 0xFF, 0x00)]
    [InlineData(0xAA, 0x55, 0xFF)]
    [InlineData(0xFF, 0x00, 0xFF)]
    [InlineData(0x00, 0x00, 0x00)]
    [InlineData(0x80, 0x80, 0x00)]
    public void EOR_Immediate_ComputesCorrectly(byte a, byte operand, byte expected)
    {
        LoadAndReset(0x49, operand);
        CpuBuffer.Current.A = a;
        CpuBuffer.Prev.A = a;
        StepInstruction();
        Assert.Equal(expected, CurrentState.A);
    }

    #endregion

    #region CMP - Compare Accumulator

    [Theory]
    [InlineData(0x20, 0x10, true, false, false)]  // A > M: C=1, Z=0, N=0
    [InlineData(0x10, 0x10, true, true, false)]   // A = M: C=1, Z=1, N=0
    [InlineData(0x10, 0x20, false, false, true)]  // A < M: C=0, Z=0, N=1
    [InlineData(0x00, 0xFF, false, false, false)] // 0x00 - 0xFF = 0x01
    [InlineData(0xFF, 0x00, true, false, true)]   // 0xFF - 0x00 = 0xFF (N=1)
    public void CMP_Immediate_SetsFlags(byte a, byte operand, bool expectC, bool expectZ, bool expectN)
    {
        LoadAndReset(0xC9, operand);
        CpuBuffer.Current.A = a;
        CpuBuffer.Prev.A = a;
        StepInstruction();
        Assert.Equal(expectC, CurrentState.CarryFlag);
        Assert.Equal(expectZ, CurrentState.ZeroFlag);
        Assert.Equal(expectN, CurrentState.NegativeFlag);
        Assert.Equal(a, CurrentState.A); // A unchanged
    }

    [Fact]
    public void CMP_AllAddressingModes_DoNotModifyA()
    {
        // Zero page
        LoadAndReset([0xC5, 0x10]);
        CpuBuffer.Current.A = 0x42;
        CpuBuffer.Prev.A = 0x42;
        SetZeroPage(0x10, 0x00);
        StepInstruction();
        Assert.Equal(0x42, CurrentState.A);
    }

    #endregion

    #region CPX - Compare X Register

    [Theory]
    [InlineData(0x30, 0x20, true, false, false)]
    [InlineData(0x20, 0x20, true, true, false)]
    [InlineData(0x10, 0x20, false, false, true)]
    public void CPX_Immediate_SetsFlags(byte x, byte operand, bool expectC, bool expectZ, bool expectN)
    {
        LoadAndReset(0xE0, operand);
        CpuBuffer.Current.X = x;
        CpuBuffer.Prev.X = x;
        StepInstruction();
        Assert.Equal(expectC, CurrentState.CarryFlag);
        Assert.Equal(expectZ, CurrentState.ZeroFlag);
        Assert.Equal(expectN, CurrentState.NegativeFlag);
        Assert.Equal(x, CurrentState.X);
    }

    #endregion

    #region CPY - Compare Y Register

    [Theory]
    [InlineData(0x30, 0x20, true, false, false)]
    [InlineData(0x20, 0x20, true, true, false)]
    [InlineData(0x10, 0x20, false, false, true)]
    public void CPY_Immediate_SetsFlags(byte y, byte operand, bool expectC, bool expectZ, bool expectN)
    {
        LoadAndReset(0xC0, operand);
        CpuBuffer.Current.Y = y;
        CpuBuffer.Prev.Y = y;
        StepInstruction();
        Assert.Equal(expectC, CurrentState.CarryFlag);
        Assert.Equal(expectZ, CurrentState.ZeroFlag);
        Assert.Equal(expectN, CurrentState.NegativeFlag);
        Assert.Equal(y, CurrentState.Y);
    }

    #endregion

    #region BIT - Bit Test

    [Theory]
    [InlineData(0xFF, 0xC0, false, true, true)]   // All bits set: Z=0, N=1, V=1
    [InlineData(0x00, 0xC0, true, true, true)]    // A=0, M=$C0: Z=1, N=1, V=1
    [InlineData(0xFF, 0x00, true, false, false)]  // M=0: Z=1, N=0, V=0
    [InlineData(0xFF, 0x80, false, true, false)]  // M=$80: N=1, V=0
    [InlineData(0xFF, 0x40, false, false, true)]  // M=$40: N=0, V=1
    public void BIT_ZeroPage_SetsFlags(byte a, byte memory, bool expectZ, bool expectN, bool expectV)
    {
        LoadAndReset([0x24, 0x20]);
        CpuBuffer.Current.A = a;
        CpuBuffer.Prev.A = a;
        SetZeroPage(0x20, memory);
        StepInstruction();
        Assert.Equal(expectZ, CurrentState.ZeroFlag);
        Assert.Equal(expectN, CurrentState.NegativeFlag);
        Assert.Equal(expectV, CurrentState.OverflowFlag);
        Assert.Equal(a, CurrentState.A); // A unchanged
    }

    [Fact]
    public void BIT_Absolute_Takes4Cycles()
    {
        LoadAndReset([0x2C, 0x00, 0x80]);
        CpuBuffer.Current.A = 0xFF;
        CpuBuffer.Prev.A = 0xFF;
        SetMemory(0x8000, 0xC0);
        int cycles = StepInstruction();
        Assert.Equal(4, cycles);
    }

    #endregion

    #region ASL - Arithmetic Shift Left

    [Theory]
    [InlineData(0x01, 0x02, false, false, false)]
    [InlineData(0x80, 0x00, true, true, false)]
    [InlineData(0x40, 0x80, false, false, true)]
    [InlineData(0xC0, 0x80, true, false, true)]
    public void ASL_Accumulator_ShiftsCorrectly(byte input, byte expected, bool expectC, bool expectZ, bool expectN)
    {
        LoadAndReset([0x0A]);
        CpuBuffer.Current.A = input;
        CpuBuffer.Prev.A = input;
        StepInstruction();
        Assert.Equal(expected, CurrentState.A);
        Assert.Equal(expectC, CurrentState.CarryFlag);
        Assert.Equal(expectZ, CurrentState.ZeroFlag);
        Assert.Equal(expectN, CurrentState.NegativeFlag);
    }

    [Fact]
    public void ASL_ZeroPage_ModifiesMemory()
    {
        LoadAndReset([0x06, 0x30]);
        SetZeroPage(0x30, 0x41);
        StepInstruction();
        Assert.Equal(0x82, Bus.Memory[0x30]);
    }

    [Fact]
    public void ASL_Absolute_Takes6Cycles()
    {
        LoadAndReset([0x0E, 0x00, 0x90]);
        SetMemory(0x9000, 0x20);
        int cycles = StepInstruction();
        Assert.Equal(6, cycles);
        Assert.Equal(0x40, Bus.Memory[0x9000]);
    }

    [Fact]
    public void ASL_AbsoluteX_Takes7Cycles()
    {
        LoadAndReset([0x1E, 0x00, 0x90]);
        CpuBuffer.Current.X = 0x10;
        CpuBuffer.Prev.X = 0x10;
        SetMemory(0x9010, 0x20);
        int cycles = StepInstruction();
        Assert.Equal(7, cycles);
    }

    #endregion

    #region LSR - Logical Shift Right

    [Theory]
    [InlineData(0x02, 0x01, false, false)]
    [InlineData(0x01, 0x00, true, true)]
    [InlineData(0x80, 0x40, false, false)]
    [InlineData(0xFF, 0x7F, true, false)]
    public void LSR_Accumulator_ShiftsCorrectly(byte input, byte expected, bool expectC, bool expectZ)
    {
        LoadAndReset([0x4A]);
        CpuBuffer.Current.A = input;
        CpuBuffer.Prev.A = input;
        StepInstruction();
        Assert.Equal(expected, CurrentState.A);
        Assert.Equal(expectC, CurrentState.CarryFlag);
        Assert.Equal(expectZ, CurrentState.ZeroFlag);
        Assert.False(CurrentState.NegativeFlag); // LSR always clears N
    }

    [Fact]
    public void LSR_ZeroPage_ModifiesMemory()
    {
        LoadAndReset([0x46, 0x40]);
        SetZeroPage(0x40, 0x82);
        StepInstruction();
        Assert.Equal(0x41, Bus.Memory[0x40]);
        Assert.False(CurrentState.CarryFlag);
    }

    #endregion

    #region ROL - Rotate Left

    [Theory]
    [InlineData(0x00, false, 0x00, false)]
    [InlineData(0x00, true, 0x01, false)]
    [InlineData(0x80, false, 0x00, true)]
    [InlineData(0x80, true, 0x01, true)]
    [InlineData(0x40, false, 0x80, false)]
    [InlineData(0x40, true, 0x81, false)]
    public void ROL_Accumulator_RotatesCorrectly(byte input, bool carryIn, byte expected, bool carryOut)
    {
        LoadAndReset([0x2A]);
        CpuBuffer.Current.A = input;
        CpuBuffer.Prev.A = input;
        CpuBuffer.Current.CarryFlag = carryIn;
        CpuBuffer.Prev.CarryFlag = carryIn;
        StepInstruction();
        Assert.Equal(expected, CurrentState.A);
        Assert.Equal(carryOut, CurrentState.CarryFlag);
    }

    [Fact]
    public void ROL_ZeroPage_RotatesMemory()
    {
        LoadAndReset([0x26, 0x50]);
        CpuBuffer.Current.CarryFlag = true;
        CpuBuffer.Prev.CarryFlag = true;
        SetZeroPage(0x50, 0x40);
        StepInstruction();
        Assert.Equal(0x81, Bus.Memory[0x50]);
        Assert.False(CurrentState.CarryFlag);
    }

    #endregion

    #region ROR - Rotate Right

    [Theory]
    [InlineData(0x00, false, 0x00, false)]
    [InlineData(0x00, true, 0x80, false)]
    [InlineData(0x01, false, 0x00, true)]
    [InlineData(0x01, true, 0x80, true)]
    [InlineData(0x02, false, 0x01, false)]
    [InlineData(0x02, true, 0x81, false)]
    public void ROR_Accumulator_RotatesCorrectly(byte input, bool carryIn, byte expected, bool carryOut)
    {
        LoadAndReset([0x6A]);
        CpuBuffer.Current.A = input;
        CpuBuffer.Prev.A = input;
        CpuBuffer.Current.CarryFlag = carryIn;
        CpuBuffer.Prev.CarryFlag = carryIn;
        StepInstruction();
        Assert.Equal(expected, CurrentState.A);
        Assert.Equal(carryOut, CurrentState.CarryFlag);
    }

    #endregion

    #region INC/DEC - Memory Increment/Decrement

    [Theory]
    [InlineData(0x00, 0x01, false, false)]
    [InlineData(0xFF, 0x00, true, false)]
    [InlineData(0x7F, 0x80, false, true)]
    [InlineData(0xFE, 0xFF, false, true)]
    public void INC_ZeroPage_IncrementsMemory(byte input, byte expected, bool expectZ, bool expectN)
    {
        LoadAndReset([0xE6, 0x60]);
        SetZeroPage(0x60, input);
        StepInstruction();
        Assert.Equal(expected, Bus.Memory[0x60]);
        Assert.Equal(expectZ, CurrentState.ZeroFlag);
        Assert.Equal(expectN, CurrentState.NegativeFlag);
    }

    [Theory]
    [InlineData(0x02, 0x01, false, false)]
    [InlineData(0x01, 0x00, true, false)]
    [InlineData(0x00, 0xFF, false, true)]
    [InlineData(0x80, 0x7F, false, false)]
    public void DEC_ZeroPage_DecrementsMemory(byte input, byte expected, bool expectZ, bool expectN)
    {
        LoadAndReset([0xC6, 0x70]);
        SetZeroPage(0x70, input);
        StepInstruction();
        Assert.Equal(expected, Bus.Memory[0x70]);
        Assert.Equal(expectZ, CurrentState.ZeroFlag);
        Assert.Equal(expectN, CurrentState.NegativeFlag);
    }

    [Fact]
    public void INC_ZeroPageX_Takes6Cycles()
    {
        LoadAndReset([0xF6, 0x10]);
        CpuBuffer.Current.X = 0x05;
        CpuBuffer.Prev.X = 0x05;
        SetZeroPage(0x15, 0x00);
        int cycles = StepInstruction();
        Assert.Equal(6, cycles);
        Assert.Equal(0x01, Bus.Memory[0x15]);
    }

    [Fact]
    public void DEC_AbsoluteX_Takes7Cycles()
    {
        LoadAndReset([0xDE, 0x00, 0xA0]);
        CpuBuffer.Current.X = 0x20;
        CpuBuffer.Prev.X = 0x20;
        SetMemory(0xA020, 0x10);
        int cycles = StepInstruction();
        Assert.Equal(7, cycles);
        Assert.Equal(0x0F, Bus.Memory[0xA020]);
    }

    #endregion

    #region INX/INY/DEX/DEY - Register Increment/Decrement

    [Theory]
    [InlineData(0x00, 0x01)]
    [InlineData(0xFF, 0x00)]
    [InlineData(0x7F, 0x80)]
    public void INX_IncrementsX(byte input, byte expected)
    {
        LoadAndReset([0xE8]);
        CpuBuffer.Current.X = input;
        CpuBuffer.Prev.X = input;
        StepInstruction();
        Assert.Equal(expected, CurrentState.X);
    }

    [Theory]
    [InlineData(0x00, 0x01)]
    [InlineData(0xFF, 0x00)]
    public void INY_IncrementsY(byte input, byte expected)
    {
        LoadAndReset([0xC8]);
        CpuBuffer.Current.Y = input;
        CpuBuffer.Prev.Y = input;
        StepInstruction();
        Assert.Equal(expected, CurrentState.Y);
    }

    [Theory]
    [InlineData(0x01, 0x00)]
    [InlineData(0x00, 0xFF)]
    public void DEX_DecrementsX(byte input, byte expected)
    {
        LoadAndReset([0xCA]);
        CpuBuffer.Current.X = input;
        CpuBuffer.Prev.X = input;
        StepInstruction();
        Assert.Equal(expected, CurrentState.X);
    }

    [Theory]
    [InlineData(0x01, 0x00)]
    [InlineData(0x00, 0xFF)]
    public void DEY_DecrementsY(byte input, byte expected)
    {
        LoadAndReset([0x88]);
        CpuBuffer.Current.Y = input;
        CpuBuffer.Prev.Y = input;
        StepInstruction();
        Assert.Equal(expected, CurrentState.Y);
    }

    #endregion

    #region Branch Instructions - Page Crossing

    [Fact]
    public void BEQ_Taken_PageCross_Takes4Cycles()
    {
        // Place BEQ near end of page to trigger page cross
        SetupCpu();
        Bus.SetResetVector(0x04F0); // Near end of page
        Bus.Memory[0x04F0] = 0xF0; // BEQ
        Bus.Memory[0x04F1] = 0x20; // +32 -> crosses to 0x0512
        Cpu.Reset(CpuBuffer, Bus);
        CpuBuffer.Current.ZeroFlag = true;
        CpuBuffer.Prev.ZeroFlag = true;
        int cycles = StepInstruction();
        Assert.Equal(4, cycles);
        Assert.Equal(0x0512, CurrentState.PC);
    }

    [Fact]
    public void BNE_Taken_BackwardBranch_PageCross_Takes4Cycles()
    {
        SetupCpu();
        Bus.SetResetVector(0x0500);
        Bus.Memory[0x0500] = 0xD0; // BNE
        Bus.Memory[0x0501] = 0xFE; // -2 (but signed, goes back)
        Cpu.Reset(CpuBuffer, Bus);
        CpuBuffer.Current.ZeroFlag = false;
        CpuBuffer.Prev.ZeroFlag = false;

        // -2 signed from 0x0502 = 0x0500, same page
        int cycles = StepInstruction();
        Assert.Equal(3, cycles); // No page cross
    }

    [Fact]
    public void BNE_Taken_BackwardBranch_CrossPage_Takes4Cycles()
    {
        SetupCpu();
        Bus.SetResetVector(0x0500);
        Bus.Memory[0x0500] = 0xD0; // BNE
        Bus.Memory[0x0501] = 0x80; // -128 signed
        Cpu.Reset(CpuBuffer, Bus);
        CpuBuffer.Current.ZeroFlag = false;
        CpuBuffer.Prev.ZeroFlag = false;

        // -128 signed from 0x0502 = 0x0482, crosses page
        int cycles = StepInstruction();
        Assert.Equal(4, cycles);
        Assert.Equal(0x0482, CurrentState.PC);
    }

    [Theory]
    [InlineData(0x90, false)]  // BCC - branch if carry clear
    [InlineData(0xB0, true)]   // BCS - branch if carry set
    public void BCC_BCS_ConditionChecking(byte opcode, bool carryForBranch)
    {
        LoadAndReset(opcode, 0x10);
        CpuBuffer.Current.CarryFlag = carryForBranch;
        CpuBuffer.Prev.CarryFlag = carryForBranch;
        int cycles = StepInstruction();
        Assert.Equal(3, cycles); // Taken, no page cross
    }

    [Theory]
    [InlineData(0x10, false)]  // BPL - branch if positive
    [InlineData(0x30, true)]   // BMI - branch if minus
    public void BPL_BMI_ConditionChecking(byte opcode, bool negativeForBranch)
    {
        LoadAndReset(opcode, 0x10);
        CpuBuffer.Current.NegativeFlag = negativeForBranch;
        CpuBuffer.Prev.NegativeFlag = negativeForBranch;
        int cycles = StepInstruction();
        Assert.Equal(3, cycles);
    }

    [Theory]
    [InlineData(0x50, false)]  // BVC - branch if overflow clear
    [InlineData(0x70, true)]   // BVS - branch if overflow set
    public void BVC_BVS_ConditionChecking(byte opcode, bool overflowForBranch)
    {
        LoadAndReset(opcode, 0x10);
        CpuBuffer.Current.OverflowFlag = overflowForBranch;
        CpuBuffer.Prev.OverflowFlag = overflowForBranch;
        int cycles = StepInstruction();
        Assert.Equal(3, cycles);
    }

    #endregion

    #region JMP - Jump Instructions

    [Fact]
    public void JMP_Absolute_SetsPC()
    {
        LoadAndReset([0x4C, 0xCD, 0xAB]);
        StepInstruction();
        Assert.Equal(0xABCD, CurrentState.PC);
    }

    [Fact]
    public void JMP_Indirect_NMOS_PageBoundaryBug()
    {
        // NMOS bug: JMP ($10FF) reads high byte from $1000, not $1100
        LoadAndReset([0x6C, 0xFF, 0x10]);
        SetMemory(0x10FF, 0x34); // Low byte
        SetMemory(0x1000, 0x12); // High byte (wrong page due to bug)
        SetMemory(0x1100, 0x99); // What 65C02 would use
        StepInstruction();
        Assert.Equal(0x1234, CurrentState.PC); // Uses buggy behavior
    }

    [Fact]
    public void JMP_Indirect_NoPageBoundary_WorksNormally()
    {
        LoadAndReset([0x6C, 0x50, 0x20]);
        SetMemory(0x2050, 0x00);
        SetMemory(0x2051, 0x80);
        StepInstruction();
        Assert.Equal(0x8000, CurrentState.PC);
    }

    #endregion

    #region Stack Operations

    [Fact]
    public void PHA_PushesA_DecrementsSP()
    {
        LoadAndReset([0x48]);
        CpuBuffer.Current.A = 0x42;
        CpuBuffer.Prev.A = 0x42;
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Prev.SP = 0xFF;
        StepInstruction();
        Assert.Equal(0xFE, CurrentState.SP);
        Assert.Equal(0x42, Bus.Memory[0x01FF]);
    }

    [Fact]
    public void PLA_PopsToA_IncrementsSP()
    {
        LoadAndReset([0x68]);
        Bus.Memory[0x01FF] = 0xAB;
        CpuBuffer.Current.SP = 0xFE;
        CpuBuffer.Prev.SP = 0xFE;
        StepInstruction();
        Assert.Equal(0xFF, CurrentState.SP);
        Assert.Equal(0xAB, CurrentState.A);
    }

    [Fact]
    public void PLA_SetsFlags()
    {
        LoadAndReset([0x68]);
        Bus.Memory[0x01FF] = 0x80; // Negative value
        CpuBuffer.Current.SP = 0xFE;
        CpuBuffer.Prev.SP = 0xFE;
        StepInstruction();
        Assert.True(CurrentState.NegativeFlag);
        Assert.False(CurrentState.ZeroFlag);
    }

    [Fact]
    public void PHP_PushesStatusWithBAndUnused()
    {
        LoadAndReset([0x08]);
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Prev.SP = 0xFF;
        CpuBuffer.Current.CarryFlag = true;
        CpuBuffer.Prev.CarryFlag = true;
        CpuBuffer.Current.ZeroFlag = true;
        CpuBuffer.Prev.ZeroFlag = true;
        StepInstruction();
        // PHP always sets B flag (bit 4) and unused (bit 5)
        byte pushed = Bus.Memory[0x01FF];
        Assert.True((pushed & 0x10) != 0, "B flag should be set");
        Assert.True((pushed & 0x20) != 0, "Unused flag should be set");
        Assert.True((pushed & 0x01) != 0, "Carry should be set");
        Assert.True((pushed & 0x02) != 0, "Zero should be set");
    }

    [Fact]
    public void PLP_RestoresStatus_IgnoresBAndUnused()
    {
        LoadAndReset([0x28]);
        Bus.Memory[0x01FF] = 0xFF; // All flags set
        CpuBuffer.Current.SP = 0xFE;
        CpuBuffer.Prev.SP = 0xFE;
        StepInstruction();
        Assert.True(CurrentState.CarryFlag);
        Assert.True(CurrentState.ZeroFlag);
        Assert.True(CurrentState.InterruptDisableFlag);
        Assert.True(CurrentState.DecimalFlag);
        Assert.True(CurrentState.OverflowFlag);
        Assert.True(CurrentState.NegativeFlag);
    }

    [Fact]
    public void Stack_WrapsAt0x0100()
    {
        // Stack wraps within page 1
        LoadAndReset([0x48]);
        CpuBuffer.Current.A = 0x55;
        CpuBuffer.Prev.A = 0x55;
        CpuBuffer.Current.SP = 0x00;
        CpuBuffer.Prev.SP = 0x00;
        StepInstruction();
        Assert.Equal(0xFF, CurrentState.SP); // Wraps to FF
        Assert.Equal(0x55, Bus.Memory[0x0100]); // Written to $0100
    }

    #endregion

    #region JSR/RTS - Subroutine Calls

    [Fact]
    public void JSR_PushesReturnAddressMinus1()
    {
        LoadAndReset([0x20, 0x00, 0x80]); // JSR $8000
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Prev.SP = 0xFF;
        StepInstruction();
        Assert.Equal(0x8000, CurrentState.PC);
        Assert.Equal(0xFD, CurrentState.SP);
        // Return address is PC-1 (0x0402, pointing to last byte of JSR)
        Assert.Equal(0x02, Bus.Memory[0x01FE]); // Low byte
        Assert.Equal(0x04, Bus.Memory[0x01FF]); // High byte
    }

    [Fact]
    public void RTS_ReturnsToAddressPlus1()
    {
        LoadAndReset([0x60]);
        CpuBuffer.Current.SP = 0xFD;
        CpuBuffer.Prev.SP = 0xFD;
        Bus.Memory[0x01FE] = 0x02; // Low byte of return - 1
        Bus.Memory[0x01FF] = 0x04; // High byte
        StepInstruction();
        Assert.Equal(0x0403, CurrentState.PC); // Returns to pushed + 1
        Assert.Equal(0xFF, CurrentState.SP);
    }

    [Fact]
    public void JSR_RTS_RoundTrip()
    {
        // Set up JSR at $0400, target at $8000, RTS there
        SetupCpu();
        Bus.SetResetVector(0x0400);
        Bus.Memory[0x0400] = 0x20; // JSR
        Bus.Memory[0x0401] = 0x00;
        Bus.Memory[0x0402] = 0x80; // Target: $8000
        Bus.Memory[0x0403] = 0xEA; // NOP (next instruction)
        Bus.Memory[0x8000] = 0x60; // RTS
        Cpu.Reset(CpuBuffer, Bus);
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Prev.SP = 0xFF;

        StepInstruction(); // Execute JSR
        Assert.Equal(0x8000, CurrentState.PC);

        StepInstruction(); // Execute RTS
        Assert.Equal(0x0403, CurrentState.PC);
    }

    #endregion

    #region RTI - Return from Interrupt

    [Fact]
    public void RTI_RestoresStatusAndPC()
    {
        LoadAndReset([0x40]);
        CpuBuffer.Current.SP = 0xFC;
        CpuBuffer.Prev.SP = 0xFC;
        Bus.Memory[0x01FD] = 0xFF; // Status
        Bus.Memory[0x01FE] = 0x00; // PC low
        Bus.Memory[0x01FF] = 0x90; // PC high
        StepInstruction();
        Assert.Equal(0x9000, CurrentState.PC);
        Assert.Equal(0xFF, CurrentState.SP);
        Assert.True(CurrentState.CarryFlag);
        Assert.True(CurrentState.NegativeFlag);
    }

    [Fact]
    public void RTI_DoesNotAddOneToPC()
    {
        // Unlike RTS, RTI returns to exact address
        LoadAndReset([0x40]);
        CpuBuffer.Current.SP = 0xFC;
        CpuBuffer.Prev.SP = 0xFC;
        Bus.Memory[0x01FD] = 0x00; // Status
        Bus.Memory[0x01FE] = 0x50; // PC low
        Bus.Memory[0x01FF] = 0x70; // PC high
        StepInstruction();
        Assert.Equal(0x7050, CurrentState.PC); // Exact address
    }

    #endregion

    #region BRK - Break

    [Fact]
    public void BRK_PushesStatusWithBFlag()
    {
        LoadAndReset([0x00, 0x00]);
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Prev.SP = 0xFF;
        Bus.SetIrqVector(0x8000);
        StepInstruction();
        // Check B flag is set in pushed status
        byte pushedStatus = Bus.Memory[0x01FD];
        Assert.True((pushedStatus & 0x10) != 0, "B flag should be set");
    }

    [Fact]
    public void BRK_SetsInterruptDisable()
    {
        LoadAndReset([0x00, 0x00]);
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Prev.SP = 0xFF;
        CpuBuffer.Current.InterruptDisableFlag = false;
        CpuBuffer.Prev.InterruptDisableFlag = false;
        Bus.SetIrqVector(0x8000);
        StepInstruction();
        Assert.True(CurrentState.InterruptDisableFlag);
    }

    [Fact]
    public void BRK_NMOS_DoesNotClearDecimalFlag()
    {
        // NMOS 6502 does NOT clear D flag on BRK (unlike 65C02)
        LoadAndReset([0x00, 0x00]);
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Prev.SP = 0xFF;
        CpuBuffer.Current.DecimalFlag = true;
        CpuBuffer.Prev.DecimalFlag = true;
        Bus.SetIrqVector(0x8000);
        StepInstruction();
        Assert.True(CurrentState.DecimalFlag, "NMOS: D flag should remain set after BRK");
    }

    [Fact]
    public void BRK_PushesPCPlus2()
    {
        // BRK pushes PC+2 (skipping the signature byte)
        LoadAndReset([0x00, 0xAB]); // BRK with signature byte
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Prev.SP = 0xFF;
        Bus.SetIrqVector(0x8000);
        StepInstruction();
        // PC was 0x0400, so pushed should be 0x0402
        Assert.Equal(0x02, Bus.Memory[0x01FE]); // Low
        Assert.Equal(0x04, Bus.Memory[0x01FF]); // High
    }

    #endregion

    #region Transfer Instructions

    [Fact]
    public void TAX_TransfersAToX_SetsFlags()
    {
        LoadAndReset([0xAA]);
        CpuBuffer.Current.A = 0x80;
        CpuBuffer.Prev.A = 0x80;
        StepInstruction();
        Assert.Equal(0x80, CurrentState.X);
        Assert.True(CurrentState.NegativeFlag);
        Assert.False(CurrentState.ZeroFlag);
    }

    [Fact]
    public void TXA_TransfersXToA_SetsFlags()
    {
        LoadAndReset([0x8A]);
        CpuBuffer.Current.X = 0x00;
        CpuBuffer.Prev.X = 0x00;
        StepInstruction();
        Assert.Equal(0x00, CurrentState.A);
        Assert.True(CurrentState.ZeroFlag);
    }

    [Fact]
    public void TAY_TransfersAToY_SetsFlags()
    {
        LoadAndReset([0xA8]);
        CpuBuffer.Current.A = 0x7F;
        CpuBuffer.Prev.A = 0x7F;
        StepInstruction();
        Assert.Equal(0x7F, CurrentState.Y);
        Assert.False(CurrentState.NegativeFlag);
        Assert.False(CurrentState.ZeroFlag);
    }

    [Fact]
    public void TYA_TransfersYToA_SetsFlags()
    {
        LoadAndReset([0x98]);
        CpuBuffer.Current.Y = 0xFF;
        CpuBuffer.Prev.Y = 0xFF;
        StepInstruction();
        Assert.Equal(0xFF, CurrentState.A);
        Assert.True(CurrentState.NegativeFlag);
    }

    [Fact]
    public void TSX_TransfersSPToX_SetsFlags()
    {
        LoadAndReset([0xBA]);
        CpuBuffer.Current.SP = 0x80;
        CpuBuffer.Prev.SP = 0x80;
        StepInstruction();
        Assert.Equal(0x80, CurrentState.X);
        Assert.True(CurrentState.NegativeFlag);
    }

    [Fact]
    public void TXS_TransfersXToSP_NoFlags()
    {
        LoadAndReset([0x9A]);
        CpuBuffer.Current.X = 0x00;
        CpuBuffer.Prev.X = 0x00;
        CpuBuffer.Current.ZeroFlag = false;
        CpuBuffer.Prev.ZeroFlag = false;
        StepInstruction();
        Assert.Equal(0x00, CurrentState.SP);
        Assert.False(CurrentState.ZeroFlag); // TXS doesn't affect flags
    }

    #endregion

    #region Flag Instructions

    [Fact]
    public void CLC_ClearsCarry()
    {
        LoadAndReset([0x18]);
        CpuBuffer.Current.CarryFlag = true;
        CpuBuffer.Prev.CarryFlag = true;
        StepInstruction();
        Assert.False(CurrentState.CarryFlag);
    }

    [Fact]
    public void SEC_SetsCarry()
    {
        LoadAndReset([0x38]);
        CpuBuffer.Current.CarryFlag = false;
        CpuBuffer.Prev.CarryFlag = false;
        StepInstruction();
        Assert.True(CurrentState.CarryFlag);
    }

    [Fact]
    public void CLD_ClearsDecimal()
    {
        LoadAndReset([0xD8]);
        CpuBuffer.Current.DecimalFlag = true;
        CpuBuffer.Prev.DecimalFlag = true;
        StepInstruction();
        Assert.False(CurrentState.DecimalFlag);
    }

    [Fact]
    public void SED_SetsDecimal()
    {
        LoadAndReset([0xF8]);
        CpuBuffer.Current.DecimalFlag = false;
        CpuBuffer.Prev.DecimalFlag = false;
        StepInstruction();
        Assert.True(CurrentState.DecimalFlag);
    }

    [Fact]
    public void CLI_ClearsInterruptDisable()
    {
        LoadAndReset([0x58]);
        CpuBuffer.Current.InterruptDisableFlag = true;
        CpuBuffer.Prev.InterruptDisableFlag = true;
        StepInstruction();
        Assert.False(CurrentState.InterruptDisableFlag);
    }

    [Fact]
    public void SEI_SetsInterruptDisable()
    {
        LoadAndReset([0x78]);
        CpuBuffer.Current.InterruptDisableFlag = false;
        CpuBuffer.Prev.InterruptDisableFlag = false;
        StepInstruction();
        Assert.True(CurrentState.InterruptDisableFlag);
    }

    [Fact]
    public void CLV_ClearsOverflow()
    {
        LoadAndReset([0xB8]);
        CpuBuffer.Current.OverflowFlag = true;
        CpuBuffer.Prev.OverflowFlag = true;
        StepInstruction();
        Assert.False(CurrentState.OverflowFlag);
    }

    #endregion

    #region NOP

    [Fact]
    public void NOP_DoesNothing()
    {
        LoadAndReset([0xEA]);
        CpuBuffer.Current.A = 0x42;
        CpuBuffer.Prev.A = 0x42;
        CpuBuffer.Current.X = 0x33;
        CpuBuffer.Prev.X = 0x33;
        CpuBuffer.Current.Y = 0x22;
        CpuBuffer.Prev.Y = 0x22;
        ushort pcBefore = (ushort)(CpuBuffer.Current.PC + 1); // After fetch

        StepInstruction();

        Assert.Equal(0x42, CurrentState.A);
        Assert.Equal(0x33, CurrentState.X);
        Assert.Equal(0x22, CurrentState.Y);
        Assert.Equal(pcBefore, CurrentState.PC);
    }

    #endregion
}
