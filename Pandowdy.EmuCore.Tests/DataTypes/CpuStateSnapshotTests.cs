// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.DataTypes;

using Pandowdy.EmuCore.Machine;
namespace Pandowdy.EmuCore.Tests.DataTypes;

/// <summary>
/// Tests for CpuStateSnapshot - immutable CPU register state for debugging.
/// </summary>
public class CpuStateSnapshotTests
{
    #region Constructor and Properties

    [Fact]
    public void InitProperties_AllPropertiesSetCorrectly()
    {
        // Arrange & Act
        var snapshot = new CpuStateSnapshot
        {
            A = 0x42,
            X = 0x10,
            Y = 0x20,
            SP = 0xFF,
            PC = 0xC000,
            P = 0xA5, // 10100101 binary
            Status = CpuExecutionStatus.Running,
            CyclesRemaining = 3,
            CurrentOpcode = 0xEA, // NOP
            OpcodeAddress = 0xBFFE
        };

        // Assert
        Assert.Equal(0x42, snapshot.A);
        Assert.Equal(0x10, snapshot.X);
        Assert.Equal(0x20, snapshot.Y);
        Assert.Equal(0xFF, snapshot.SP);
        Assert.Equal(0xC000, snapshot.PC);
        Assert.Equal(0xA5, snapshot.P);
        Assert.Equal(CpuExecutionStatus.Running, snapshot.Status);
        Assert.Equal(3, snapshot.CyclesRemaining);
        Assert.Equal(0xEA, snapshot.CurrentOpcode);
        Assert.Equal(0xBFFE, snapshot.OpcodeAddress);
    }

    #endregion

    #region Flag Accessor Tests

    [Fact]
    public void FlagN_WhenBit7Set_ReturnsTrue()
    {
        // Arrange
        var snapshot = new CpuStateSnapshot { P = 0x80 }; // 10000000

        // Act & Assert
        Assert.True(snapshot.FlagN);
    }

    [Fact]
    public void FlagN_WhenBit7Clear_ReturnsFalse()
    {
        // Arrange
        var snapshot = new CpuStateSnapshot { P = 0x7F }; // 01111111

        // Act & Assert
        Assert.False(snapshot.FlagN);
    }

    [Fact]
    public void FlagV_WhenBit6Set_ReturnsTrue()
    {
        // Arrange
        var snapshot = new CpuStateSnapshot { P = 0x40 }; // 01000000

        // Act & Assert
        Assert.True(snapshot.FlagV);
    }

    [Fact]
    public void FlagV_WhenBit6Clear_ReturnsFalse()
    {
        // Arrange
        var snapshot = new CpuStateSnapshot { P = 0xBF }; // 10111111

        // Act & Assert
        Assert.False(snapshot.FlagV);
    }

    [Fact]
    public void FlagB_WhenBit4Set_ReturnsTrue()
    {
        // Arrange
        var snapshot = new CpuStateSnapshot { P = 0x10 }; // 00010000

        // Act & Assert
        Assert.True(snapshot.FlagB);
    }

    [Fact]
    public void FlagB_WhenBit4Clear_ReturnsFalse()
    {
        // Arrange
        var snapshot = new CpuStateSnapshot { P = 0xEF }; // 11101111

        // Act & Assert
        Assert.False(snapshot.FlagB);
    }

    [Fact]
    public void FlagD_WhenBit3Set_ReturnsTrue()
    {
        // Arrange
        var snapshot = new CpuStateSnapshot { P = 0x08 }; // 00001000

        // Act & Assert
        Assert.True(snapshot.FlagD);
    }

    [Fact]
    public void FlagD_WhenBit3Clear_ReturnsFalse()
    {
        // Arrange
        var snapshot = new CpuStateSnapshot { P = 0xF7 }; // 11110111

        // Act & Assert
        Assert.False(snapshot.FlagD);
    }

    [Fact]
    public void FlagI_WhenBit2Set_ReturnsTrue()
    {
        // Arrange
        var snapshot = new CpuStateSnapshot { P = 0x04 }; // 00000100

        // Act & Assert
        Assert.True(snapshot.FlagI);
    }

    [Fact]
    public void FlagI_WhenBit2Clear_ReturnsFalse()
    {
        // Arrange
        var snapshot = new CpuStateSnapshot { P = 0xFB }; // 11111011

        // Act & Assert
        Assert.False(snapshot.FlagI);
    }

    [Fact]
    public void FlagZ_WhenBit1Set_ReturnsTrue()
    {
        // Arrange
        var snapshot = new CpuStateSnapshot { P = 0x02 }; // 00000010

        // Act & Assert
        Assert.True(snapshot.FlagZ);
    }

    [Fact]
    public void FlagZ_WhenBit1Clear_ReturnsFalse()
    {
        // Arrange
        var snapshot = new CpuStateSnapshot { P = 0xFD }; // 11111101

        // Act & Assert
        Assert.False(snapshot.FlagZ);
    }

    [Fact]
    public void FlagC_WhenBit0Set_ReturnsTrue()
    {
        // Arrange
        var snapshot = new CpuStateSnapshot { P = 0x01 }; // 00000001

        // Act & Assert
        Assert.True(snapshot.FlagC);
    }

    [Fact]
    public void FlagC_WhenBit0Clear_ReturnsFalse()
    {
        // Arrange
        var snapshot = new CpuStateSnapshot { P = 0xFE }; // 11111110

        // Act & Assert
        Assert.False(snapshot.FlagC);
    }

    [Theory]
    [InlineData(0xFF, true, true, true, true, true, true, true)] // All flags set
    [InlineData(0x00, false, false, false, false, false, false, false)] // All flags clear
    [InlineData(0xA5, true, false, false, false, true, false, true)] // 10100101 = bit7=1(N), bit6=0(V), bit4=0(B), bit3=0(D), bit2=1(I), bit1=0(Z), bit0=1(C)
    [InlineData(0x5A, false, true, true, true, false, true, false)] // 01011010 = bit7=0(N), bit6=1(V), bit4=1(B), bit3=1(D), bit2=0(I), bit1=1(Z), bit0=0(C)
    public void AllFlags_WithVariousPValues_CorrectlyExtracted(
        byte p, bool n, bool v, bool b, bool d, bool i, bool z, bool c)
    {
        // Arrange
        var snapshot = new CpuStateSnapshot { P = p };

        // Assert
        Assert.Equal(n, snapshot.FlagN);
        Assert.Equal(v, snapshot.FlagV);
        Assert.Equal(b, snapshot.FlagB);
        Assert.Equal(d, snapshot.FlagD);
        Assert.Equal(i, snapshot.FlagI);
        Assert.Equal(z, snapshot.FlagZ);
        Assert.Equal(c, snapshot.FlagC);
    }

    #endregion

    #region AtInstructionBoundary Tests

    [Fact]
    public void AtInstructionBoundary_WhenCyclesRemainingIsZero_ReturnsTrue()
    {
        // Arrange
        var snapshot = new CpuStateSnapshot { CyclesRemaining = 0 };

        // Act & Assert
        Assert.True(snapshot.AtInstructionBoundary);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(10)]
    public void AtInstructionBoundary_WhenCyclesRemainingIsNonZero_ReturnsFalse(int cycles)
    {
        // Arrange
        var snapshot = new CpuStateSnapshot { CyclesRemaining = cycles };

        // Act & Assert
        Assert.False(snapshot.AtInstructionBoundary);
    }

    #endregion

    #region IsRunning Tests

    [Fact]
    public void IsRunning_WhenStatusIsRunning_ReturnsTrue()
    {
        // Arrange
        var snapshot = new CpuStateSnapshot { Status = CpuExecutionStatus.Running };

        // Act & Assert
        Assert.True(snapshot.IsRunning);
    }

    [Theory]
    [InlineData(CpuExecutionStatus.Stopped)]
    [InlineData(CpuExecutionStatus.Jammed)]
    [InlineData(CpuExecutionStatus.Waiting)]
    [InlineData(CpuExecutionStatus.Bypassed)]
    public void IsRunning_WhenStatusIsNotRunning_ReturnsFalse(CpuExecutionStatus status)
    {
        // Arrange
        var snapshot = new CpuStateSnapshot { Status = status };

        // Act & Assert
        Assert.False(snapshot.IsRunning);
    }

    #endregion

    #region FlagsString Tests

    [Fact]
    public void FlagsString_WithAllFlagsSet_ReturnsUppercase()
    {
        // Arrange
        var snapshot = new CpuStateSnapshot { P = 0xFF }; // All flags set

        // Act
        var flagsString = snapshot.FlagsString;

        // Assert
        Assert.Equal("NV-BDIZC", flagsString);
    }

    [Fact]
    public void FlagsString_WithAllFlagsClear_ReturnsLowercase()
    {
        // Arrange
        var snapshot = new CpuStateSnapshot { P = 0x00 }; // All flags clear

        // Act
        var flagsString = snapshot.FlagsString;

        // Assert
        Assert.Equal("nv-bdizc", flagsString);
    }

    [Theory]
    [InlineData(0x80, "Nv-bdizc")] // Only N set (bit 7)
    [InlineData(0x40, "nV-bdizc")] // Only V set (bit 6)
    [InlineData(0x10, "nv-Bdizc")] // Only B set (bit 4)
    [InlineData(0x08, "nv-bDizc")] // Only D set (bit 3)
    [InlineData(0x04, "nv-bdIzc")] // Only I set (bit 2)
    [InlineData(0x02, "nv-bdiZc")] // Only Z set (bit 1)
    [InlineData(0x01, "nv-bdizC")] // Only C set (bit 0)
    [InlineData(0xA5, "Nv-bdIzC")] // N, I, C set (10100101: bits 7,2,0)
    [InlineData(0x5A, "nV-BDiZc")] // V, B, D, Z set (01011010: bits 6,4,3,1)
    public void FlagsString_WithVariousPValues_FormatsCorrectly(byte p, string expected)
    {
        // Arrange
        var snapshot = new CpuStateSnapshot { P = p };

        // Act
        var flagsString = snapshot.FlagsString;

        // Assert
        Assert.Equal(expected, flagsString);
    }

    [Fact]
    public void FlagsString_AlwaysHasDashAtPosition2()
    {
        // Arrange
        var snapshot1 = new CpuStateSnapshot { P = 0x00 };
        var snapshot2 = new CpuStateSnapshot { P = 0xFF };

        // Act
        var flags1 = snapshot1.FlagsString;
        var flags2 = snapshot2.FlagsString;

        // Assert
        Assert.Equal('-', flags1[2]);
        Assert.Equal('-', flags2[2]);
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_FormatsCorrectly()
    {
        // Arrange
        var snapshot = new CpuStateSnapshot
        {
            PC = 0xC000,
            A = 0x42,
            X = 0x10,
            Y = 0x20,
            SP = 0xFF,
            P = 0xA5, // Nv-bdIzC (10100101: N=1, I=1, C=1)
            Status = CpuExecutionStatus.Running
        };

        // Act
        var result = snapshot.ToString();

        // Assert
        Assert.Contains("PC=$C000", result);
        Assert.Contains("A=$42", result);
        Assert.Contains("X=$10", result);
        Assert.Contains("Y=$20", result);
        Assert.Contains("SP=$FF", result);
        Assert.Contains("Nv-bdIzC", result);
        Assert.Contains("Running", result);
    }

    [Theory]
    [InlineData(CpuExecutionStatus.Running, "Running")]
    [InlineData(CpuExecutionStatus.Stopped, "Stopped")]
    [InlineData(CpuExecutionStatus.Jammed, "Jammed")]
    [InlineData(CpuExecutionStatus.Waiting, "Waiting")]
    [InlineData(CpuExecutionStatus.Bypassed, "Bypassed")]
    public void ToString_IncludesStatusCorrectly(CpuExecutionStatus status, string expectedStatus)
    {
        // Arrange
        var snapshot = new CpuStateSnapshot { Status = status };

        // Act
        var result = snapshot.ToString();

        // Assert
        Assert.Contains(expectedStatus, result);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void Integration_TypicalDebuggerSnapshot_AllPropertiesAccessible()
    {
        // Arrange - Simulate CPU state mid-execution
        var snapshot = new CpuStateSnapshot
        {
            PC = 0xC030,
            A = 0xFF,
            X = 0x00,
            Y = 0x05,
            SP = 0xFD,
            P = 0x82, // N and Z flags set
            Status = CpuExecutionStatus.Running,
            CyclesRemaining = 2,
            CurrentOpcode = 0xA9, // LDA immediate
            OpcodeAddress = 0xC030
        };

        // Assert - All properties readable
        Assert.Equal(0xC030, snapshot.PC);
        Assert.Equal(0xFF, snapshot.A);
        Assert.Equal(0x00, snapshot.X);
        Assert.Equal(0x05, snapshot.Y);
        Assert.Equal(0xFD, snapshot.SP);
        Assert.Equal(0x82, snapshot.P);
        Assert.Equal(CpuExecutionStatus.Running, snapshot.Status);
        Assert.Equal(2, snapshot.CyclesRemaining);
        Assert.Equal(0xA9, snapshot.CurrentOpcode);
        Assert.Equal(0xC030, snapshot.OpcodeAddress);

        // Assert - Computed properties
        Assert.True(snapshot.FlagN);
        Assert.True(snapshot.FlagZ);
        Assert.False(snapshot.FlagC);
        Assert.False(snapshot.AtInstructionBoundary);
        Assert.True(snapshot.IsRunning);
        Assert.Equal("Nv-bdiZc", snapshot.FlagsString);
    }

    [Fact]
    public void Integration_InstructionBoundarySnapshot_CyclesZero()
    {
        // Arrange - CPU ready to fetch next instruction
        var snapshot = new CpuStateSnapshot
        {
            PC = 0xC100,
            A = 0x00,
            X = 0x00,
            Y = 0x00,
            SP = 0xFF,
            P = 0x02, // Z flag set
            Status = CpuExecutionStatus.Running,
            CyclesRemaining = 0, // At instruction boundary
            CurrentOpcode = 0x00,
            OpcodeAddress = 0xC0FF
        };

        // Assert
        Assert.True(snapshot.AtInstructionBoundary);
        Assert.True(snapshot.IsRunning);
        Assert.Equal("nv-bdiZc", snapshot.FlagsString);
        Assert.Contains("$C100", snapshot.ToString());
    }

    [Fact]
    public void Integration_HaltedCpuSnapshot_NotRunning()
    {
        // Arrange - CPU stopped by STP instruction
        var snapshot = new CpuStateSnapshot
        {
            PC = 0xD000,
            Status = CpuExecutionStatus.Stopped,
            CyclesRemaining = 0
        };

        // Assert
        Assert.False(snapshot.IsRunning);
        Assert.True(snapshot.AtInstructionBoundary);
        Assert.Contains("Stopped", snapshot.ToString());
    }

    #endregion

    #region Immutability and Value Semantics

    [Fact]
    public void Immutability_SnapshotCannotBeModified()
    {
        // Arrange
        var snapshot = new CpuStateSnapshot
        {
            A = 0x42,
            PC = 0xC000,
            P = 0xFF
        };

        // Act - Try to modify (this should not compile if truly readonly)
        // snapshot.A = 0x50; // This would not compile

        // Assert - Verify original values unchanged
        Assert.Equal(0x42, snapshot.A);
        Assert.Equal(0xC000, snapshot.PC);
        Assert.Equal(0xFF, snapshot.P);
    }

    [Fact]
    public void ValueSemantics_TwoSnapshotsWithSameValues_AreEqual()
    {
        // Arrange
        var snapshot1 = new CpuStateSnapshot { A = 0x42, X = 0x10, PC = 0xC000 };
        var snapshot2 = new CpuStateSnapshot { A = 0x42, X = 0x10, PC = 0xC000 };

        // Assert - Structs with same values are equal
        Assert.Equal(snapshot1.A, snapshot2.A);
        Assert.Equal(snapshot1.X, snapshot2.X);
        Assert.Equal(snapshot1.PC, snapshot2.PC);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void EdgeCase_MaximumValues_AllPropertiesSet()
    {
        // Arrange
        var snapshot = new CpuStateSnapshot
        {
            A = 0xFF,
            X = 0xFF,
            Y = 0xFF,
            SP = 0xFF,
            PC = 0xFFFF,
            P = 0xFF,
            CyclesRemaining = int.MaxValue,
            CurrentOpcode = 0xFF,
            OpcodeAddress = 0xFFFF
        };

        // Assert
        Assert.Equal(0xFF, snapshot.A);
        Assert.Equal(0xFF, snapshot.X);
        Assert.Equal(0xFF, snapshot.Y);
        Assert.Equal(0xFF, snapshot.SP);
        Assert.Equal(0xFFFF, snapshot.PC);
        Assert.Equal(0xFF, snapshot.P);
        Assert.Equal(int.MaxValue, snapshot.CyclesRemaining);
        Assert.Equal(0xFF, snapshot.CurrentOpcode);
        Assert.Equal(0xFFFF, snapshot.OpcodeAddress);
    }

    [Fact]
    public void EdgeCase_MinimumValues_AllPropertiesZero()
    {
        // Arrange
        var snapshot = new CpuStateSnapshot
        {
            A = 0x00,
            X = 0x00,
            Y = 0x00,
            SP = 0x00,
            PC = 0x0000,
            P = 0x00,
            CyclesRemaining = 0,
            CurrentOpcode = 0x00,
            OpcodeAddress = 0x0000
        };

        // Assert
        Assert.Equal(0x00, snapshot.A);
        Assert.Equal(0x00, snapshot.X);
        Assert.Equal(0x00, snapshot.Y);
        Assert.Equal(0x00, snapshot.SP);
        Assert.Equal(0x0000, snapshot.PC);
        Assert.Equal(0x00, snapshot.P);
        Assert.Equal(0, snapshot.CyclesRemaining);
        Assert.Equal(0x00, snapshot.CurrentOpcode);
        Assert.Equal(0x0000, snapshot.OpcodeAddress);
        Assert.Equal("nv-bdizc", snapshot.FlagsString);
    }

    #endregion
}
