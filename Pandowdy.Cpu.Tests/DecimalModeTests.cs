// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Xunit;

namespace Pandowdy.Cpu.Tests;

/// <summary>
/// Comprehensive tests for decimal (BCD) mode arithmetic.
/// Tests both ADC and SBC in decimal mode across NMOS and CMOS variants.
/// </summary>
public class DecimalModeTests : CpuTestBase
{
    protected override CpuVariant Variant => CpuVariant.Wdc65C02;

    #region ADC Decimal Mode - Basic Operations

    [Fact]
    public void ADC_Decimal_SimpleAddition()
    {
        // 0x15 + 0x27 = 0x42 in BCD
        LoadAndReset([0x69, 0x27]); // ADC #$27
        CurrentState.A = 0x15;
        CurrentState.A = 0x15;
        CurrentState.DecimalFlag = true;
        CurrentState.DecimalFlag = true;
        CurrentState.CarryFlag = false;
        CurrentState.CarryFlag = false;

        StepInstruction();

        Assert.Equal(0x42, CurrentState.A);
        Assert.False(CurrentState.CarryFlag);
    }

    [Fact]
    public void ADC_Decimal_WithCarryIn()
    {
        // 0x15 + 0x27 + 1 = 0x43 in BCD
        LoadAndReset([0x69, 0x27]); // ADC #$27
        CurrentState.A = 0x15;
        CurrentState.A = 0x15;
        CurrentState.DecimalFlag = true;
        CurrentState.DecimalFlag = true;
        CurrentState.CarryFlag = true;
        CurrentState.CarryFlag = true;

        StepInstruction();

        Assert.Equal(0x43, CurrentState.A);
        Assert.False(CurrentState.CarryFlag);
    }

    [Fact]
    public void ADC_Decimal_LowNibbleWrap()
    {
        // 0x09 + 0x01 = 0x10 in BCD (low nibble wraps)
        LoadAndReset([0x69, 0x01]); // ADC #$01
        CurrentState.A = 0x09;
        CurrentState.A = 0x09;
        CurrentState.DecimalFlag = true;
        CurrentState.DecimalFlag = true;
        CurrentState.CarryFlag = false;
        CurrentState.CarryFlag = false;

        StepInstruction();

        Assert.Equal(0x10, CurrentState.A);
        Assert.False(CurrentState.CarryFlag);
    }

    [Fact]
    public void ADC_Decimal_HighNibbleWrap()
    {
        // 0x90 + 0x10 = 0x00 with carry in BCD
        LoadAndReset([0x69, 0x10]); // ADC #$10
        CurrentState.A = 0x90;
        CurrentState.A = 0x90;
        CurrentState.DecimalFlag = true;
        CurrentState.DecimalFlag = true;
        CurrentState.CarryFlag = false;
        CurrentState.CarryFlag = false;

        StepInstruction();

        Assert.Equal(0x00, CurrentState.A);
        Assert.True(CurrentState.CarryFlag);
    }

    [Fact]
    public void ADC_Decimal_BothNibblesWrap()
    {
        // 0x99 + 0x01 = 0x00 with carry in BCD
        LoadAndReset([0x69, 0x01]); // ADC #$01
        CurrentState.A = 0x99;
        CurrentState.A = 0x99;
        CurrentState.DecimalFlag = true;
        CurrentState.DecimalFlag = true;
        CurrentState.CarryFlag = false;
        CurrentState.CarryFlag = false;

        StepInstruction();

        Assert.Equal(0x00, CurrentState.A);
        Assert.True(CurrentState.CarryFlag);
    }

    [Fact]
    public void ADC_Decimal_MaxPlusMax()
    {
        // 0x99 + 0x99 + 1 = 0x99 with carry in BCD (199 in decimal)
        LoadAndReset([0x69, 0x99]); // ADC #$99
        CurrentState.A = 0x99;
        CurrentState.A = 0x99;
        CurrentState.DecimalFlag = true;
        CurrentState.DecimalFlag = true;
        CurrentState.CarryFlag = true;
        CurrentState.CarryFlag = true;

        StepInstruction();

        Assert.Equal(0x99, CurrentState.A);
        Assert.True(CurrentState.CarryFlag);
    }

    [Fact]
    public void ADC_Decimal_ZeroPlusZero()
    {
        // 0x00 + 0x00 = 0x00 in BCD
        LoadAndReset([0x69, 0x00]); // ADC #$00
        CurrentState.A = 0x00;
        CurrentState.A = 0x00;
        CurrentState.DecimalFlag = true;
        CurrentState.DecimalFlag = true;
        CurrentState.CarryFlag = false;
        CurrentState.CarryFlag = false;

        StepInstruction();

        Assert.Equal(0x00, CurrentState.A);
        Assert.False(CurrentState.CarryFlag);
        Assert.True(CurrentState.ZeroFlag);
    }

    #endregion

    #region SBC Decimal Mode - Basic Operations

    [Fact]
    public void SBC_Decimal_SimpleSubtraction()
    {
        // 0x42 - 0x15 = 0x27 in BCD
        LoadAndReset([0xE9, 0x15]); // SBC #$15
        CurrentState.A = 0x42;
        CurrentState.A = 0x42;
        CurrentState.DecimalFlag = true;
        CurrentState.DecimalFlag = true;
        CurrentState.CarryFlag = true; // No borrow
        CurrentState.CarryFlag = true;

        StepInstruction();

        Assert.Equal(0x27, CurrentState.A);
        Assert.True(CurrentState.CarryFlag); // No borrow
    }

    [Fact]
    public void SBC_Decimal_WithBorrow()
    {
        // 0x42 - 0x15 - 1 = 0x26 in BCD (C=0 means borrow)
        LoadAndReset([0xE9, 0x15]); // SBC #$15
        CurrentState.A = 0x42;
        CurrentState.A = 0x42;
        CurrentState.DecimalFlag = true;
        CurrentState.DecimalFlag = true;
        CurrentState.CarryFlag = false; // Borrow in
        CurrentState.CarryFlag = false;

        StepInstruction();

        Assert.Equal(0x26, CurrentState.A);
        Assert.True(CurrentState.CarryFlag);
    }

    [Fact]
    public void SBC_Decimal_LowNibbleBorrow()
    {
        // 0x10 - 0x01 = 0x09 in BCD
        LoadAndReset([0xE9, 0x01]); // SBC #$01
        CurrentState.A = 0x10;
        CurrentState.A = 0x10;
        CurrentState.DecimalFlag = true;
        CurrentState.DecimalFlag = true;
        CurrentState.CarryFlag = true;
        CurrentState.CarryFlag = true;

        StepInstruction();

        Assert.Equal(0x09, CurrentState.A);
        Assert.True(CurrentState.CarryFlag);
    }

    [Fact]
    public void SBC_Decimal_HighNibbleBorrow()
    {
        // 0x00 - 0x01 = 0x99 with borrow in BCD
        LoadAndReset([0xE9, 0x01]); // SBC #$01
        CurrentState.A = 0x00;
        CurrentState.A = 0x00;
        CurrentState.DecimalFlag = true;
        CurrentState.DecimalFlag = true;
        CurrentState.CarryFlag = true;
        CurrentState.CarryFlag = true;

        StepInstruction();

        Assert.Equal(0x99, CurrentState.A);
        Assert.False(CurrentState.CarryFlag); // Borrow occurred
    }

    [Fact]
    public void SBC_Decimal_ZeroMinusZero()
    {
        // 0x00 - 0x00 = 0x00 in BCD
        LoadAndReset([0xE9, 0x00]); // SBC #$00
        CurrentState.A = 0x00;
        CurrentState.A = 0x00;
        CurrentState.DecimalFlag = true;
        CurrentState.DecimalFlag = true;
        CurrentState.CarryFlag = true;
        CurrentState.CarryFlag = true;

        StepInstruction();

        Assert.Equal(0x00, CurrentState.A);
        Assert.True(CurrentState.CarryFlag);
    }

    [Fact]
    public void SBC_Decimal_SameMinusSame()
    {
        // 0x50 - 0x50 = 0x00 in BCD
        LoadAndReset([0xE9, 0x50]); // SBC #$50
        CurrentState.A = 0x50;
        CurrentState.A = 0x50;
        CurrentState.DecimalFlag = true;
        CurrentState.DecimalFlag = true;
        CurrentState.CarryFlag = true;
        CurrentState.CarryFlag = true;

        StepInstruction();

        Assert.Equal(0x00, CurrentState.A);
        Assert.True(CurrentState.CarryFlag);
    }

    #endregion

    #region NMOS vs CMOS Flag Differences

    [Fact]
    public void ADC_Decimal_NMOS_ZeroFlag_BasedOnBinarySum()
    {
        // NMOS 6502: Z flag is based on binary sum, not BCD result
        // 0x99 + 0x01 = 0x00 BCD, but binary 0x99 + 0x01 = 0x9A (not zero)
        LoadAndReset([0x69, 0x01]); // ADC #$01
        CurrentState.A = 0x99;
        CurrentState.A = 0x99;
        CurrentState.DecimalFlag = true;
        CurrentState.DecimalFlag = true;
        CurrentState.CarryFlag = false;
        CurrentState.CarryFlag = false;

        StepInstruction(CpuVariant.Nmos6502);

        Assert.Equal(0x00, CurrentState.A);
        Assert.False(CurrentState.ZeroFlag, "NMOS: Z flag should be based on binary sum (0x9A != 0)");
    }

    [Fact]
    public void ADC_Decimal_CMOS_ZeroFlag_BasedOnBCDResult()
    {
        // CMOS 65C02: Z flag is based on BCD result
        // 0x99 + 0x01 = 0x00 BCD, Z should be true
        LoadAndReset([0x69, 0x01]); // ADC #$01
        CurrentState.A = 0x99;
        CurrentState.A = 0x99;
        CurrentState.DecimalFlag = true;
        CurrentState.DecimalFlag = true;
        CurrentState.CarryFlag = false;
        CurrentState.CarryFlag = false;

        StepInstruction(CpuVariant.Wdc65C02);

        Assert.Equal(0x00, CurrentState.A);
        Assert.True(CurrentState.ZeroFlag, "CMOS: Z flag should be based on BCD result (0x00 is zero)");
    }

    [Fact]
    public void ADC_Decimal_NMOS_NegativeFlag_BasedOnIntermediate()
    {
        // NMOS: N flag based on intermediate result (after low nibble adjust)
        // 0x79 + 0x10 = 0x89 BCD
        LoadAndReset([0x69, 0x10]); // ADC #$10
        CurrentState.A = 0x79;
        CurrentState.A = 0x79;
        CurrentState.DecimalFlag = true;
        CurrentState.DecimalFlag = true;
        CurrentState.CarryFlag = false;
        CurrentState.CarryFlag = false;

        StepInstruction(CpuVariant.Nmos6502);

        Assert.Equal(0x89, CurrentState.A);
        Assert.True(CurrentState.NegativeFlag, "NMOS: N flag should be based on intermediate result");
    }

    [Fact]
    public void ADC_Decimal_CMOS_NegativeFlag_BasedOnBCDResult()
    {
        // CMOS: N flag based on BCD result
        // 0x79 + 0x10 = 0x89 BCD (bit 7 set)
        LoadAndReset([0x69, 0x10]); // ADC #$10
        CurrentState.A = 0x79;
        CurrentState.A = 0x79;
        CurrentState.DecimalFlag = true;
        CurrentState.DecimalFlag = true;
        CurrentState.CarryFlag = false;
        CurrentState.CarryFlag = false;

        StepInstruction(CpuVariant.Wdc65C02);

        Assert.Equal(0x89, CurrentState.A);
        Assert.True(CurrentState.NegativeFlag, "CMOS: N flag should be based on BCD result (0x89 has bit 7 set)");
    }

    [Fact]
    public void SBC_Decimal_NMOS_Flags_BasedOnBinaryResult()
    {
        // NMOS: N and Z flags based on binary subtraction result
        // 0x01 - 0x01 = 0x00 (both BCD and binary are zero)
        LoadAndReset([0xE9, 0x01]); // SBC #$01
        CurrentState.A = 0x01;
        CurrentState.A = 0x01;
        CurrentState.DecimalFlag = true;
        CurrentState.DecimalFlag = true;
        CurrentState.CarryFlag = true;
        CurrentState.CarryFlag = true;

        StepInstruction(CpuVariant.Nmos6502);

        Assert.Equal(0x00, CurrentState.A);
        Assert.True(CurrentState.ZeroFlag);
        Assert.False(CurrentState.NegativeFlag);
    }

    [Fact]
    public void SBC_Decimal_CMOS_Flags_BasedOnBCDResult()
    {
        // CMOS: N and Z flags based on BCD result
        LoadAndReset([0xE9, 0x01]); // SBC #$01
        CurrentState.A = 0x01;
        CurrentState.A = 0x01;
        CurrentState.DecimalFlag = true;
        CurrentState.DecimalFlag = true;
        CurrentState.CarryFlag = true;
        CurrentState.CarryFlag = true;

        StepInstruction(CpuVariant.Wdc65C02);

        Assert.Equal(0x00, CurrentState.A);
        Assert.True(CurrentState.ZeroFlag);
    }

    #endregion

    #region Overflow Flag Tests in Decimal Mode

    [Fact]
    public void ADC_Decimal_OverflowSet_WhenSignChanges()
    {
        // In decimal mode, V flag is calculated on intermediate value
        // 0x79 + 0x10 = 0x89 (positive + positive = "negative" in signed interpretation)
        LoadAndReset([0x69, 0x10]); // ADC #$10
        CurrentState.A = 0x79;
        CurrentState.A = 0x79;
        CurrentState.DecimalFlag = true;
        CurrentState.DecimalFlag = true;
        CurrentState.CarryFlag = false;
        CurrentState.CarryFlag = false;

        StepInstruction();

        Assert.True(CurrentState.OverflowFlag);
    }

    [Fact]
    public void ADC_Decimal_OverflowClear_WhenNoSignChange()
    {
        // 0x15 + 0x27 = 0x42 (no sign change)
        LoadAndReset([0x69, 0x27]); // ADC #$27
        CurrentState.A = 0x15;
        CurrentState.A = 0x15;
        CurrentState.DecimalFlag = true;
        CurrentState.DecimalFlag = true;
        CurrentState.CarryFlag = false;
        CurrentState.CarryFlag = false;

        StepInstruction();

        Assert.False(CurrentState.OverflowFlag);
    }

    [Fact]
    public void SBC_Decimal_OverflowSet_WhenSignChanges()
    {
        // Subtracting negative from positive can cause overflow
        // 0x80 - 0x01 = 0x79 ("negative" - positive = positive)
        LoadAndReset([0xE9, 0x01]); // SBC #$01
        CurrentState.A = 0x80;
        CurrentState.A = 0x80;
        CurrentState.DecimalFlag = true;
        CurrentState.DecimalFlag = true;
        CurrentState.CarryFlag = true;
        CurrentState.CarryFlag = true;

        StepInstruction();

        Assert.True(CurrentState.OverflowFlag);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ADC_Decimal_InvalidBCD_StillCalculatesCorrectly()
    {
        // Invalid BCD values (nibbles > 9) are corrected
        // 0x0A + 0x01 = 0x11 (0A is invalid, but CPU corrects it)
        LoadAndReset([0x69, 0x01]); // ADC #$01
        CurrentState.A = 0x0A;
        CurrentState.A = 0x0A;
        CurrentState.DecimalFlag = true;
        CurrentState.DecimalFlag = true;
        CurrentState.CarryFlag = false;
        CurrentState.CarryFlag = false;

        StepInstruction();

        // The CPU corrects invalid BCD
        Assert.Equal(0x11, CurrentState.A);
    }

    [Fact]
    public void SBC_Decimal_FromZeroWithBorrow()
    {
        // 0x00 - 0x01 with borrow = 0x98 (100 - 1 - 1 = 98)
        LoadAndReset([0xE9, 0x01]); // SBC #$01
        CurrentState.A = 0x00;
        CurrentState.A = 0x00;
        CurrentState.DecimalFlag = true;
        CurrentState.DecimalFlag = true;
        CurrentState.CarryFlag = false; // Borrow in
        CurrentState.CarryFlag = false;

        StepInstruction();

        Assert.Equal(0x98, CurrentState.A);
        Assert.False(CurrentState.CarryFlag); // Borrow occurred
    }

    [Fact]
    public void ADC_Decimal_ChainedOperation()
    {
        // Test chained BCD addition: 0x56 + 0x78 = 0x34 with carry
        // Then add carry to next byte
        LoadAndReset([0x69, 0x78, 0x69, 0x00]); // ADC #$78, ADC #$00
        CurrentState.A = 0x56;
        CurrentState.A = 0x56;
        CurrentState.DecimalFlag = true;
        CurrentState.DecimalFlag = true;
        CurrentState.CarryFlag = false;
        CurrentState.CarryFlag = false;

        StepInstruction(); // First ADC

        Assert.Equal(0x34, CurrentState.A);
        Assert.True(CurrentState.CarryFlag);

        // Continue with second ADC (adds carry)
        StepInstruction();

        Assert.Equal(0x35, CurrentState.A); // 0x34 + 0x00 + carry = 0x35
    }

    #endregion

    #region All Variants Produce Correct BCD Result

    [Theory]
    [InlineData(0x12, 0x34, 0x46, false)] // Simple addition
    [InlineData(0x99, 0x01, 0x00, true)]  // Wrap with carry
    [InlineData(0x50, 0x50, 0x00, true)]  // 50 + 50 = 100
    [InlineData(0x09, 0x01, 0x10, false)] // Low nibble wrap
    public void ADC_Decimal_ProducesCorrectResult_AllVariants(byte a, byte m, byte expected, bool expectedCarry)
    {
        foreach (var variant in new[] { CpuVariant.Nmos6502, CpuVariant.Nmos6502Simple, CpuVariant.Wdc65C02, CpuVariant.Rockwell65C02 })
        {
            LoadAndReset([0x69, m]); // ADC #m
            CurrentState.A = a;
            CurrentState.A = a;
            CurrentState.DecimalFlag = true;
            CurrentState.DecimalFlag = true;
            CurrentState.CarryFlag = false;
            CurrentState.CarryFlag = false;

            StepInstruction(variant);

            Assert.Equal(expected, CurrentState.A);
            Assert.Equal(expectedCarry, CurrentState.CarryFlag);
        }
    }

    [Theory]
    [InlineData(0x46, 0x12, 0x34, true)]  // Simple subtraction
    [InlineData(0x00, 0x01, 0x99, false)] // Wrap with borrow
    [InlineData(0x50, 0x50, 0x00, true)]  // Same minus same
    [InlineData(0x10, 0x01, 0x09, true)]  // Low nibble borrow
    public void SBC_Decimal_ProducesCorrectResult_AllVariants(byte a, byte m, byte expected, bool expectedCarry)
    {
        foreach (var variant in new[] { CpuVariant.Nmos6502, CpuVariant.Nmos6502Simple, CpuVariant.Wdc65C02, CpuVariant.Rockwell65C02 })
        {
            LoadAndReset([0xE9, m]); // SBC #m
            CurrentState.A = a;
            CurrentState.A = a;
            CurrentState.DecimalFlag = true;
            CurrentState.DecimalFlag = true;
            CurrentState.CarryFlag = true; // No borrow
            CurrentState.CarryFlag = true;

            StepInstruction(variant);

            Assert.Equal(expected, CurrentState.A);
            Assert.Equal(expectedCarry, CurrentState.CarryFlag);
        }
    }

    #endregion
}
