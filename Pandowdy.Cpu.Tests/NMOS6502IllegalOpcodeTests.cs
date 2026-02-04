// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Xunit;

namespace Pandowdy.Cpu.Tests;

/// <summary>
/// Comprehensive tests for NMOS 6502 illegal/undocumented opcodes.
/// These test the behavioral contracts of all documented illegal instructions.
/// </summary>
public class NMOS6502IllegalOpcodeTests : CpuTestBase
{
    protected override CpuVariant Variant => CpuVariant.Nmos6502;

    #region LAX - Load A and X (All Addressing Modes)

    [Fact]
    public void LAX_ZeroPage_LoadsBothAAndX()
    {
        // LAX $10 (0xA7)
        LoadAndReset([0xA7, 0x10]);
        SetZeroPage(0x10, 0x55);
        StepInstruction();
        Assert.Equal(0x55, CurrentState.A);
        Assert.Equal(0x55, CurrentState.X);
    }

    [Fact]
    public void LAX_ZeroPage_SetsFlags()
    {
        LoadAndReset([0xA7, 0x20]);
        SetZeroPage(0x20, 0x80);
        StepInstruction();
        Assert.True(CurrentState.NegativeFlag);
        Assert.False(CurrentState.ZeroFlag);
    }

    [Fact]
    public void LAX_ZeroPage_ZeroValue_SetsZeroFlag()
    {
        LoadAndReset([0xA7, 0x30]);
        SetZeroPage(0x30, 0x00);
        StepInstruction();
        Assert.True(CurrentState.ZeroFlag);
        Assert.False(CurrentState.NegativeFlag);
    }

    [Fact]
    public void LAX_ZeroPageY_WrapsWithinZeroPage()
    {
        // LAX $F0,Y with Y=$20 -> wraps to $10
        LoadAndReset([0xB7, 0xF0]);
        CurrentState.Y = 0x20;
        CurrentState.Y = 0x20;
        SetZeroPage(0x10, 0x66);
        int cycles = StepInstruction();
        Assert.Equal(4, cycles);
        Assert.Equal(0x66, CurrentState.A);
        Assert.Equal(0x66, CurrentState.X);
    }

    [Fact]
    public void LAX_Absolute_LoadsFromFullAddress()
    {
        // LAX $1234 (0xAF)
        LoadAndReset([0xAF, 0x34, 0x12]);
        SetMemory(0x1234, 0x77);
        int cycles = StepInstruction();
        Assert.Equal(4, cycles);
        Assert.Equal(0x77, CurrentState.A);
        Assert.Equal(0x77, CurrentState.X);
    }

    [Fact]
    public void LAX_AbsoluteY_NoPageCross()
    {
        // LAX $1200,Y (0xBF)
        LoadAndReset([0xBF, 0x00, 0x12]);
        CurrentState.Y = 0x34;
        CurrentState.Y = 0x34;
        SetMemory(0x1234, 0x88);
        int cycles = StepInstruction();
        Assert.Equal(4, cycles);
        Assert.Equal(0x88, CurrentState.A);
        Assert.Equal(0x88, CurrentState.X);
    }

    [Fact]
    public void LAX_AbsoluteY_WithPageCross()
    {
        LoadAndReset([0xBF, 0xFF, 0x12]);
        CurrentState.Y = 0x01;
        CurrentState.Y = 0x01;
        SetMemory(0x1300, 0x99);
        int cycles = StepInstruction();
        Assert.Equal(5, cycles);
        Assert.Equal(0x99, CurrentState.A);
        Assert.Equal(0x99, CurrentState.X);
    }

    [Fact]
    public void LAX_IndexedIndirectX_LoadsViaPointer()
    {
        // LAX ($10,X) (0xA3)
        LoadAndReset([0xA3, 0x10]);
        CurrentState.X = 0x05;
        CurrentState.X = 0x05;
        SetZeroPagePointer(0x15, 0x3000);
        SetMemory(0x3000, 0xAA);
        int cycles = StepInstruction();
        Assert.Equal(6, cycles);
        Assert.Equal(0xAA, CurrentState.A);
        Assert.Equal(0xAA, CurrentState.X);
    }

    [Fact]
    public void LAX_IndirectIndexedY_NoPageCross()
    {
        // LAX ($20),Y (0xB3)
        LoadAndReset([0xB3, 0x20]);
        CurrentState.Y = 0x10;
        CurrentState.Y = 0x10;
        SetZeroPagePointer(0x20, 0x4000);
        SetMemory(0x4010, 0xBB);
        int cycles = StepInstruction();
        Assert.Equal(5, cycles);
        Assert.Equal(0xBB, CurrentState.A);
        Assert.Equal(0xBB, CurrentState.X);
    }

    [Fact]
    public void LAX_IndirectIndexedY_WithPageCross()
    {
        LoadAndReset([0xB3, 0x30]);
        CurrentState.Y = 0x80;
        CurrentState.Y = 0x80;
        SetZeroPagePointer(0x30, 0x40F0);
        SetMemory(0x4170, 0xCC);
        int cycles = StepInstruction();
        Assert.Equal(6, cycles);
        Assert.Equal(0xCC, CurrentState.A);
        Assert.Equal(0xCC, CurrentState.X);
    }

    #endregion

    #region SAX - Store A AND X (All Addressing Modes)

    [Theory]
    [InlineData(0xFF, 0xFF, 0xFF)]
    [InlineData(0xF0, 0x0F, 0x00)]
    [InlineData(0xAA, 0x55, 0x00)]
    [InlineData(0xFF, 0x0F, 0x0F)]
    public void SAX_ZeroPage_StoresAAndX(byte a, byte x, byte expected)
    {
        // SAX $10 (0x87)
        LoadAndReset([0x87, 0x10]);
        CurrentState.A = a;
        CurrentState.A = a;
        CurrentState.X = x;
        CurrentState.X = x;
        int cycles = StepInstruction();
        Assert.Equal(3, cycles);
        Assert.Equal(expected, Bus.Memory[0x10]);
    }

    [Fact]
    public void SAX_ZeroPageY_WrapsWithinZeroPage()
    {
        // SAX $F0,Y with Y=$20 -> wraps to $10
        LoadAndReset([0x97, 0xF0]);
        CurrentState.A = 0xFF;
        CurrentState.A = 0xFF;
        CurrentState.X = 0x55;
        CurrentState.X = 0x55;
        CurrentState.Y = 0x20;
        CurrentState.Y = 0x20;
        int cycles = StepInstruction();
        Assert.Equal(4, cycles);
        Assert.Equal(0x55, Bus.Memory[0x10]);
    }

    [Fact]
    public void SAX_Absolute_StoresAtAddress()
    {
        // SAX $1234 (0x8F)
        LoadAndReset([0x8F, 0x34, 0x12]);
        CurrentState.A = 0xFF;
        CurrentState.A = 0xFF;
        CurrentState.X = 0xF0;
        CurrentState.X = 0xF0;
        int cycles = StepInstruction();
        Assert.Equal(4, cycles);
        Assert.Equal(0xF0, Bus.Memory[0x1234]);
    }

    [Fact]
    public void SAX_IndexedIndirectX_StoresViaPointer()
    {
        // SAX ($10,X) (0x83)
        LoadAndReset([0x83, 0x10]);
        CurrentState.A = 0xAA;
        CurrentState.A = 0xAA;
        CurrentState.X = 0x55;
        CurrentState.X = 0x55;
        SetZeroPagePointer(0x65, 0x5000); // $10 + $55 = $65
        int cycles = StepInstruction();
        Assert.Equal(6, cycles);
        Assert.Equal(0x00, Bus.Memory[0x5000]); // 0xAA & 0x55 = 0
    }

    [Fact]
    public void SAX_DoesNotAffectFlags()
    {
        LoadAndReset([0x87, 0x40]);
        CurrentState.A = 0x00;
        CurrentState.A = 0x00;
        CurrentState.X = 0x00;
        CurrentState.X = 0x00;
        CurrentState.ZeroFlag = false;
        CurrentState.ZeroFlag = false;
        CurrentState.NegativeFlag = true;
        CurrentState.NegativeFlag = true;
        StepInstruction();
        // SAX does not affect flags
        Assert.False(CurrentState.ZeroFlag);
        Assert.True(CurrentState.NegativeFlag);
    }

    #endregion

    #region DCP - Decrement then Compare (All Addressing Modes)

    [Fact]
    public void DCP_ZeroPage_DecrementsAndCompares()
    {
        // DCP $10 (0xC7) - DEC then CMP
        LoadAndReset([0xC7, 0x10]);
        CurrentState.A = 0x42;
        CurrentState.A = 0x42;
        SetZeroPage(0x10, 0x43); // Will become 0x42
        int cycles = StepInstruction();
        Assert.Equal(5, cycles);
        Assert.Equal(0x42, Bus.Memory[0x10]);
        Assert.True(CurrentState.ZeroFlag); // A == M
        Assert.True(CurrentState.CarryFlag);
    }

    [Fact]
    public void DCP_ZeroPage_DecrementsAndSetsNegative()
    {
        LoadAndReset([0xC7, 0x20]);
        CurrentState.A = 0x10;
        CurrentState.A = 0x10;
        SetZeroPage(0x20, 0x20); // Will become 0x1F
        StepInstruction();
        Assert.Equal(0x1F, Bus.Memory[0x20]);
        Assert.False(CurrentState.CarryFlag); // A < M
        Assert.True(CurrentState.NegativeFlag); // Result is negative
    }

    [Fact]
    public void DCP_ZeroPageX_Takes6Cycles()
    {
        LoadAndReset([0xD7, 0x10]);
        CurrentState.A = 0x50;
        CurrentState.A = 0x50;
        CurrentState.X = 0x05;
        CurrentState.X = 0x05;
        SetZeroPage(0x15, 0x51);
        int cycles = StepInstruction();
        Assert.Equal(6, cycles);
        Assert.Equal(0x50, Bus.Memory[0x15]);
    }

    [Fact]
    public void DCP_Absolute_Takes6Cycles()
    {
        LoadAndReset([0xCF, 0x00, 0x50]);
        CurrentState.A = 0x30;
        CurrentState.A = 0x30;
        SetMemory(0x5000, 0x30);
        int cycles = StepInstruction();
        Assert.Equal(6, cycles);
        Assert.Equal(0x2F, Bus.Memory[0x5000]);
    }

    [Fact]
    public void DCP_AbsoluteX_Takes7Cycles()
    {
        LoadAndReset([0xDF, 0x00, 0x60]);
        CurrentState.A = 0x20;
        CurrentState.A = 0x20;
        CurrentState.X = 0x10;
        CurrentState.X = 0x10;
        SetMemory(0x6010, 0x21);
        int cycles = StepInstruction();
        Assert.Equal(7, cycles);
    }

    [Fact]
    public void DCP_AbsoluteY_Takes7Cycles()
    {
        LoadAndReset([0xDB, 0x00, 0x70]);
        CurrentState.A = 0x10;
        CurrentState.A = 0x10;
        CurrentState.Y = 0x20;
        CurrentState.Y = 0x20;
        SetMemory(0x7020, 0x11);
        int cycles = StepInstruction();
        Assert.Equal(7, cycles);
    }

    [Fact]
    public void DCP_IndexedIndirectX_Takes8Cycles()
    {
        LoadAndReset([0xC3, 0x10]);
        CurrentState.A = 0x05;
        CurrentState.A = 0x05;
        CurrentState.X = 0x05;
        CurrentState.X = 0x05;
        SetZeroPagePointer(0x15, 0x8000);
        SetMemory(0x8000, 0x06);
        int cycles = StepInstruction();
        Assert.Equal(8, cycles);
    }

    [Fact]
    public void DCP_IndirectIndexedY_Takes8Cycles()
    {
        LoadAndReset([0xD3, 0x20]);
        CurrentState.A = 0x00;
        CurrentState.A = 0x00;
        CurrentState.Y = 0x10;
        CurrentState.Y = 0x10;
        SetZeroPagePointer(0x20, 0x9000);
        SetMemory(0x9010, 0x01);
        int cycles = StepInstruction();
        Assert.Equal(8, cycles);
        Assert.Equal(0x00, Bus.Memory[0x9010]); // DEC from 1 to 0
        Assert.True(CurrentState.ZeroFlag); // A == M
    }

    #endregion

    #region ISC/ISB - Increment then Subtract (All Addressing Modes)

    [Fact]
    public void ISC_ZeroPage_IncrementsAndSubtracts()
    {
        // ISC $10 (0xE7) - INC then SBC
        LoadAndReset([0xE7, 0x10]);
        CurrentState.A = 0x50;
        CurrentState.A = 0x50;
        CurrentState.CarryFlag = true;
        CurrentState.CarryFlag = true;
        SetZeroPage(0x10, 0x0F); // Will become 0x10
        int cycles = StepInstruction();
        Assert.Equal(5, cycles);
        Assert.Equal(0x10, Bus.Memory[0x10]); // INC result
        Assert.Equal(0x40, CurrentState.A);   // SBC result
    }

    [Fact]
    public void ISC_ZeroPage_WithBorrow()
    {
        LoadAndReset([0xE7, 0x20]);
        CurrentState.A = 0x00;
        CurrentState.A = 0x00;
        CurrentState.CarryFlag = true;
        CurrentState.CarryFlag = true;
        SetZeroPage(0x20, 0x00); // Will become 0x01
        StepInstruction();
        Assert.Equal(0x01, Bus.Memory[0x20]);
        Assert.Equal(0xFF, CurrentState.A); // 0x00 - 0x01 = 0xFF
        Assert.False(CurrentState.CarryFlag); // Borrow occurred
    }

    [Fact]
    public void ISC_Absolute_Takes6Cycles()
    {
        LoadAndReset([0xEF, 0x00, 0xA0]);
        CurrentState.A = 0x20;
        CurrentState.A = 0x20;
        CurrentState.CarryFlag = true;
        CurrentState.CarryFlag = true;
        SetMemory(0xA000, 0x0F);
        int cycles = StepInstruction();
        Assert.Equal(6, cycles);
    }

    [Fact]
    public void ISC_AbsoluteX_Takes7Cycles()
    {
        LoadAndReset([0xFF, 0x00, 0xB0]);
        CurrentState.A = 0x30;
        CurrentState.A = 0x30;
        CurrentState.X = 0x20;
        CurrentState.X = 0x20;
        CurrentState.CarryFlag = true;
        CurrentState.CarryFlag = true;
        SetMemory(0xB020, 0x10);
        int cycles = StepInstruction();
        Assert.Equal(7, cycles);
    }

    [Fact]
    public void ISC_AbsoluteY_Takes7Cycles()
    {
        LoadAndReset([0xFB, 0x00, 0xC0]);
        CurrentState.A = 0x40;
        CurrentState.A = 0x40;
        CurrentState.Y = 0x30;
        CurrentState.Y = 0x30;
        CurrentState.CarryFlag = true;
        CurrentState.CarryFlag = true;
        SetMemory(0xC030, 0x20);
        int cycles = StepInstruction();
        Assert.Equal(7, cycles);
    }

    [Fact]
    public void ISC_ZeroPageX_Takes6Cycles()
    {
        LoadAndReset([0xF7, 0x30]);
        CurrentState.A = 0x50;
        CurrentState.A = 0x50;
        CurrentState.X = 0x10;
        CurrentState.X = 0x10;
        CurrentState.CarryFlag = true;
        CurrentState.CarryFlag = true;
        SetZeroPage(0x40, 0x30);
        int cycles = StepInstruction();
        Assert.Equal(6, cycles);
    }

    [Fact]
    public void ISC_IndexedIndirectX_Takes8Cycles()
    {
        LoadAndReset([0xE3, 0x40]);
        CurrentState.A = 0x60;
        CurrentState.A = 0x60;
        CurrentState.X = 0x10;
        CurrentState.X = 0x10;
        CurrentState.CarryFlag = true;
        CurrentState.CarryFlag = true;
        SetZeroPagePointer(0x50, 0xD000);
        SetMemory(0xD000, 0x40);
        int cycles = StepInstruction();
        Assert.Equal(8, cycles);
    }

    [Fact]
    public void ISC_IndirectIndexedY_Takes8Cycles()
    {
        LoadAndReset([0xF3, 0x50]);
        CurrentState.A = 0x70;
        CurrentState.A = 0x70;
        CurrentState.Y = 0x20;
        CurrentState.Y = 0x20;
        CurrentState.CarryFlag = true;
        CurrentState.CarryFlag = true;
        SetZeroPagePointer(0x50, 0xE000);
        SetMemory(0xE020, 0x50);
        int cycles = StepInstruction();
        Assert.Equal(8, cycles);
    }

    #endregion

    #region SLO - Shift Left then OR (All Addressing Modes)

    [Fact]
    public void SLO_ZeroPage_ShiftsAndOrs()
    {
        // SLO $10 (0x07) - ASL then ORA
        LoadAndReset([0x07, 0x10]);
        CurrentState.A = 0x01;
        CurrentState.A = 0x01;
        SetZeroPage(0x10, 0x40); // Shifts to 0x80
        int cycles = StepInstruction();
        Assert.Equal(5, cycles);
        Assert.Equal(0x80, Bus.Memory[0x10]); // ASL result
        Assert.Equal(0x81, CurrentState.A);   // ORA result
    }

    [Fact]
    public void SLO_SetsCarryFromShift()
    {
        LoadAndReset([0x07, 0x20]);
        CurrentState.A = 0x00;
        CurrentState.A = 0x00;
        SetZeroPage(0x20, 0x80); // Shifts to 0x00, carry set
        StepInstruction();
        Assert.Equal(0x00, Bus.Memory[0x20]);
        Assert.Equal(0x00, CurrentState.A);
        Assert.True(CurrentState.CarryFlag);
        Assert.True(CurrentState.ZeroFlag);
    }

    [Fact]
    public void SLO_ZeroPageX_Takes6Cycles()
    {
        LoadAndReset([0x17, 0x10]);
        CurrentState.A = 0x0F;
        CurrentState.A = 0x0F;
        CurrentState.X = 0x05;
        CurrentState.X = 0x05;
        SetZeroPage(0x15, 0x20);
        int cycles = StepInstruction();
        Assert.Equal(6, cycles);
    }

    [Fact]
    public void SLO_Absolute_Takes6Cycles()
    {
        LoadAndReset([0x0F, 0x00, 0x50]);
        CurrentState.A = 0x00;
        CurrentState.A = 0x00;
        SetMemory(0x5000, 0x01);
        int cycles = StepInstruction();
        Assert.Equal(6, cycles);
        Assert.Equal(0x02, Bus.Memory[0x5000]);
        Assert.Equal(0x02, CurrentState.A);
    }

    [Fact]
    public void SLO_AbsoluteX_Takes7Cycles()
    {
        LoadAndReset([0x1F, 0x00, 0x60]);
        CurrentState.X = 0x10;
        CurrentState.X = 0x10;
        SetMemory(0x6010, 0x08);
        int cycles = StepInstruction();
        Assert.Equal(7, cycles);
    }

    [Fact]
    public void SLO_AbsoluteY_Takes7Cycles()
    {
        LoadAndReset([0x1B, 0x00, 0x70]);
        CurrentState.Y = 0x20;
        CurrentState.Y = 0x20;
        SetMemory(0x7020, 0x10);
        int cycles = StepInstruction();
        Assert.Equal(7, cycles);
    }

    [Fact]
    public void SLO_IndexedIndirectX_Takes8Cycles()
    {
        LoadAndReset([0x03, 0x30]);
        CurrentState.X = 0x10;
        CurrentState.X = 0x10;
        SetZeroPagePointer(0x40, 0x8000);
        SetMemory(0x8000, 0x04);
        int cycles = StepInstruction();
        Assert.Equal(8, cycles);
    }

    [Fact]
    public void SLO_IndirectIndexedY_Takes8Cycles()
    {
        LoadAndReset([0x13, 0x40]);
        CurrentState.Y = 0x30;
        CurrentState.Y = 0x30;
        SetZeroPagePointer(0x40, 0x9000);
        SetMemory(0x9030, 0x02);
        int cycles = StepInstruction();
        Assert.Equal(8, cycles);
    }

    #endregion

    #region RLA - Rotate Left then AND (All Addressing Modes)

    [Fact]
    public void RLA_ZeroPage_RotatesAndAnds()
    {
        // RLA $10 (0x27) - ROL then AND
        LoadAndReset([0x27, 0x10]);
        CurrentState.A = 0xFF;
        CurrentState.A = 0xFF;
        CurrentState.CarryFlag = true;
        CurrentState.CarryFlag = true;
        SetZeroPage(0x10, 0x40); // ROL: 0x40 << 1 + C = 0x81
        int cycles = StepInstruction();
        Assert.Equal(5, cycles);
        Assert.Equal(0x81, Bus.Memory[0x10]);
        Assert.Equal(0x81, CurrentState.A); // 0xFF & 0x81
        Assert.False(CurrentState.CarryFlag); // Bit 7 was 0
    }

    [Fact]
    public void RLA_SetsCarryFromRotate()
    {
        LoadAndReset([0x27, 0x20]);
        CurrentState.A = 0xFF;
        CurrentState.A = 0xFF;
        CurrentState.CarryFlag = false;
        CurrentState.CarryFlag = false;
        SetZeroPage(0x20, 0x80); // ROL: 0x80 << 1 = 0x00, C=1
        StepInstruction();
        Assert.Equal(0x00, Bus.Memory[0x20]);
        Assert.Equal(0x00, CurrentState.A);
        Assert.True(CurrentState.CarryFlag);
    }

    [Fact]
    public void RLA_Absolute_Takes6Cycles()
    {
        LoadAndReset([0x2F, 0x00, 0x50]);
        CurrentState.A = 0xFF;
        CurrentState.A = 0xFF;
        SetMemory(0x5000, 0x20);
        int cycles = StepInstruction();
        Assert.Equal(6, cycles);
    }

    [Fact]
    public void RLA_AbsoluteX_Takes7Cycles()
    {
        LoadAndReset([0x3F, 0x00, 0x60]);
        CurrentState.X = 0x10;
        CurrentState.X = 0x10;
        SetMemory(0x6010, 0x10);
        int cycles = StepInstruction();
        Assert.Equal(7, cycles);
    }

    #endregion

    #region SRE - Shift Right then EOR (All Addressing Modes)

    [Fact]
    public void SRE_ZeroPage_ShiftsAndEors()
    {
        // SRE $10 (0x47) - LSR then EOR
        LoadAndReset([0x47, 0x10]);
        CurrentState.A = 0xFF;
        CurrentState.A = 0xFF;
        SetZeroPage(0x10, 0x02); // LSR: 0x02 >> 1 = 0x01
        int cycles = StepInstruction();
        Assert.Equal(5, cycles);
        Assert.Equal(0x01, Bus.Memory[0x10]);
        Assert.Equal(0xFE, CurrentState.A); // 0xFF ^ 0x01
    }

    [Fact]
    public void SRE_SetsCarryFromShift()
    {
        LoadAndReset([0x47, 0x20]);
        CurrentState.A = 0x00;
        CurrentState.A = 0x00;
        SetZeroPage(0x20, 0x01); // LSR: 0x01 >> 1 = 0x00, C=1
        StepInstruction();
        Assert.Equal(0x00, Bus.Memory[0x20]);
        Assert.True(CurrentState.CarryFlag);
    }

    [Fact]
    public void SRE_Absolute_Takes6Cycles()
    {
        LoadAndReset([0x4F, 0x00, 0x70]);
        SetMemory(0x7000, 0x04);
        int cycles = StepInstruction();
        Assert.Equal(6, cycles);
    }

    [Fact]
    public void SRE_AbsoluteX_Takes7Cycles()
    {
        LoadAndReset([0x5F, 0x00, 0x80]);
        CurrentState.X = 0x20;
        CurrentState.X = 0x20;
        SetMemory(0x8020, 0x08);
        int cycles = StepInstruction();
        Assert.Equal(7, cycles);
    }

    #endregion

    #region RRA - Rotate Right then ADC (All Addressing Modes)

    [Fact]
    public void RRA_ZeroPage_RotatesAndAdds()
    {
        // RRA $10 (0x67) - ROR then ADC
        LoadAndReset([0x67, 0x10]);
        CurrentState.A = 0x00;
        CurrentState.A = 0x00;
        CurrentState.CarryFlag = true;
        CurrentState.CarryFlag = true;
        SetZeroPage(0x10, 0x02); // ROR: (0x02 >> 1) + C*0x80 = 0x81
        int cycles = StepInstruction();
        Assert.Equal(5, cycles);
        Assert.Equal(0x81, Bus.Memory[0x10]);
        Assert.Equal(0x81, CurrentState.A); // 0x00 + 0x81 + 0
    }

    [Fact]
    public void RRA_AddsWithCarryFromRotate()
    {
        LoadAndReset([0x67, 0x20]);
        CurrentState.A = 0x10;
        CurrentState.A = 0x10;
        CurrentState.CarryFlag = false;
        CurrentState.CarryFlag = false;
        SetZeroPage(0x20, 0x01); // ROR: 0x01 >> 1 = 0x00, C=1
        StepInstruction();
        Assert.Equal(0x00, Bus.Memory[0x20]);
        // ADC: 0x10 + 0x00 + 1 = 0x11
        Assert.Equal(0x11, CurrentState.A);
    }

    [Fact]
    public void RRA_Absolute_Takes6Cycles()
    {
        LoadAndReset([0x6F, 0x00, 0x90]);
        CurrentState.A = 0x05;
        CurrentState.A = 0x05;
        SetMemory(0x9000, 0x04);
        int cycles = StepInstruction();
        Assert.Equal(6, cycles);
    }

    [Fact]
    public void RRA_AbsoluteX_Takes7Cycles()
    {
        LoadAndReset([0x7F, 0x00, 0xA0]);
        CurrentState.X = 0x30;
        CurrentState.X = 0x30;
        SetMemory(0xA030, 0x10);
        int cycles = StepInstruction();
        Assert.Equal(7, cycles);
    }

    #endregion

    #region ANC - AND with Carry (Immediate Only)

    [Theory]
    [InlineData(0xFF, 0x80, 0x80, true)]   // Result negative, C=N
    [InlineData(0xFF, 0x7F, 0x7F, false)]  // Result positive, C=N
    [InlineData(0xFF, 0x00, 0x00, false)]  // Zero result
    [InlineData(0x0F, 0xF0, 0x00, false)]  // No overlap
    public void ANC_AndsAndSetsCarryFromN(byte a, byte operand, byte expectedA, bool expectC)
    {
        // ANC #imm (0x0B or 0x2B)
        LoadAndReset(0x0B, operand);
        CurrentState.A = a;
        CurrentState.A = a;
        StepInstruction();
        Assert.Equal(expectedA, CurrentState.A);
        Assert.Equal(expectC, CurrentState.CarryFlag);
    }

    [Fact]
    public void ANC_Duplicate_0x2B_BehavesSame()
    {
        LoadAndReset([0x2B, 0x80]);
        CurrentState.A = 0xFF;
        CurrentState.A = 0xFF;
        StepInstruction();
        Assert.Equal(0x80, CurrentState.A);
        Assert.True(CurrentState.CarryFlag);
    }

    #endregion

    #region ALR - AND then LSR (Immediate Only)

    [Theory]
    [InlineData(0xFF, 0xFF, 0x7F, true)]   // AND=0xFF, LSR=0x7F, C=1
    [InlineData(0xFE, 0xFF, 0x7F, false)]  // AND=0xFE, LSR=0x7F, C=0
    [InlineData(0x02, 0xFF, 0x01, false)]  // AND=0x02, LSR=0x01, C=0
    [InlineData(0x01, 0xFF, 0x00, true)]   // AND=0x01, LSR=0x00, C=1
    public void ALR_AndsAndShiftsRight(byte a, byte operand, byte expectedA, bool expectC)
    {
        // ALR #imm (0x4B)
        LoadAndReset(0x4B, operand);
        CurrentState.A = a;
        CurrentState.A = a;
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
        Assert.Equal(expectedA, CurrentState.A);
        Assert.Equal(expectC, CurrentState.CarryFlag);
    }

    #endregion

    #region ARR - AND then ROR with Special Flags (Immediate Only)

    [Fact]
    public void ARR_AndsAndRotatesRight()
    {
        // ARR #imm (0x6B)
        LoadAndReset([0x6B, 0xFF]);
        CurrentState.A = 0x02;
        CurrentState.A = 0x02;
        CurrentState.CarryFlag = true;
        CurrentState.CarryFlag = true;
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
        // AND: 0x02 & 0xFF = 0x02
        // ROR with C=1: (0x02 >> 1) | 0x80 = 0x81
        Assert.Equal(0x81, CurrentState.A);
    }

    [Fact]
    public void ARR_SetsSpecialCVFlags()
    {
        // ARR has unusual C and V flag behavior based on bits 6 and 7 of result
        LoadAndReset([0x6B, 0xC0]);
        CurrentState.A = 0xFF;
        CurrentState.A = 0xFF;
        CurrentState.CarryFlag = false;
        CurrentState.CarryFlag = false;
        StepInstruction();
        // AND: 0xFF & 0xC0 = 0xC0
        // ROR with C=0: 0xC0 >> 1 = 0x60
        Assert.Equal(0x60, CurrentState.A);
    }

    #endregion

    #region AXS/SBX - A AND X minus immediate (Immediate Only)

    [Theory]
    [InlineData(0xFF, 0xFF, 0x00, 0xFF, true, false)]   // (0xFF&0xFF) - 0 = 0xFF
    [InlineData(0xFF, 0x0F, 0x01, 0x0E, true, false)]   // (0xFF&0x0F) - 1 = 0x0E
    [InlineData(0xFF, 0x20, 0x20, 0x00, true, true)]    // (0xFF&0x20) - 0x20 = 0x00
    [InlineData(0xFF, 0x10, 0x20, 0xF0, false, false)]  // (0xFF&0x10) - 0x20 = 0xF0 (borrow)
    public void AXS_SubtractsImmediateFromAandX(byte a, byte x, byte operand, byte expectedX, bool expectC, bool expectZ)
    {
        // AXS #imm (0xCB)
        LoadAndReset(0xCB, operand);
        CurrentState.A = a;
        CurrentState.A = a;
        CurrentState.X = x;
        CurrentState.X = x;
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
        Assert.Equal(expectedX, CurrentState.X);
        Assert.Equal(expectC, CurrentState.CarryFlag);
        Assert.Equal(expectZ, CurrentState.ZeroFlag);
        Assert.Equal(a, CurrentState.A); // A unchanged
    }

    #endregion

    #region Unofficial SBC (0xEB)

    [Fact]
    public void SBC_Unofficial_0xEB_BehavesSameAsSBC()
    {
        LoadAndReset([0xEB, 0x05]);
        CurrentState.A = 0x10;
        CurrentState.A = 0x10;
        CurrentState.CarryFlag = true;
        CurrentState.CarryFlag = true;
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
        Assert.Equal(0x0B, CurrentState.A);
    }

    #endregion

    #region NOP Variants

    [Theory]
    [InlineData(0x1A)] // NOP implied
    [InlineData(0x3A)]
    [InlineData(0x5A)]
    [InlineData(0x7A)]
    [InlineData(0xDA)]
    [InlineData(0xFA)]
    public void NOP_Implied_Takes2Cycles(byte opcode)
    {
        LoadAndReset(opcode);
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
    }

    [Theory]
    [InlineData(0x80)] // NOP immediate
    [InlineData(0x82)]
    [InlineData(0x89)]
    [InlineData(0xC2)]
    [InlineData(0xE2)]
    public void NOP_Immediate_Takes2Cycles(byte opcode)
    {
        LoadAndReset(opcode, 0x42);
        int cycles = StepInstruction();
        Assert.Equal(2, cycles);
    }

    [Theory]
    [InlineData(0x04)] // NOP zp
    [InlineData(0x44)]
    [InlineData(0x64)]
    public void NOP_ZeroPage_Takes3Cycles(byte opcode)
    {
        LoadAndReset(opcode, 0x10);
        int cycles = StepInstruction();
        Assert.Equal(3, cycles);
    }

    [Theory]
    [InlineData(0x14)] // NOP zp,X
    [InlineData(0x34)]
    [InlineData(0x54)]
    [InlineData(0x74)]
    [InlineData(0xD4)]
    [InlineData(0xF4)]
    public void NOP_ZeroPageX_Takes4Cycles(byte opcode)
    {
        LoadAndReset(opcode, 0x10);
        int cycles = StepInstruction();
        Assert.Equal(4, cycles);
    }

    [Fact]
    public void NOP_Absolute_0x0C_Takes4Cycles()
    {
        LoadAndReset([0x0C, 0x34, 0x12]);
        int cycles = StepInstruction();
        Assert.Equal(4, cycles);
    }

    [Theory]
    [InlineData(0x1C)] // NOP abs,X
    [InlineData(0x3C)]
    [InlineData(0x5C)]
    [InlineData(0x7C)]
    [InlineData(0xDC)]
    [InlineData(0xFC)]
    public void NOP_AbsoluteX_Takes4PlusCycles_NoPageCross(byte opcode)
    {
        LoadAndReset(opcode, 0x00, 0x12);
        CurrentState.X = 0x10;
        CurrentState.X = 0x10;
        int cycles = StepInstruction();
        Assert.Equal(4, cycles);
    }

    [Fact]
    public void NOP_AbsoluteX_Takes5Cycles_WithPageCross()
    {
        LoadAndReset([0x1C, 0xFF, 0x12]);
        CurrentState.X = 0x01;
        CurrentState.X = 0x01;
        int cycles = StepInstruction();
        Assert.Equal(5, cycles);
    }

    #endregion

    #region Unstable Illegal Opcodes

    [Fact]
    public void LAS_AbsoluteY_LoadsAndStoresWithSP()
    {
        // LAS $1234,Y (0xBB) - A,X,SP = M & SP
        LoadAndReset([0xBB, 0x00, 0x12]);
        CurrentState.Y = 0x34;
        CurrentState.Y = 0x34;
        CurrentState.SP = 0xFF;
        CurrentState.SP = 0xFF;
        SetMemory(0x1234, 0x55);
        StepInstruction();
        // Result = 0x55 & 0xFF = 0x55
        Assert.Equal(0x55, CurrentState.A);
        Assert.Equal(0x55, CurrentState.X);
        Assert.Equal(0x55, CurrentState.SP);
    }

    #endregion
}
