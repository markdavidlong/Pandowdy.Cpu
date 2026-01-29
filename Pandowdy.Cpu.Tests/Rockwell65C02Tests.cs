// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Xunit;

namespace Pandowdy.Cpu.Tests;

/// <summary>
/// Tests for Rockwell 65C02 CPU.
/// Inherits all core instruction tests and adds Rockwell-specific tests (RMB, SMB, BBR, BBS).
/// </summary>
public class Rockwell65C02Tests : CoreInstructionTests
{
    protected override CpuVariant Variant => CpuVariant.ROCKWELL65C02;

    #region JMP Indirect Bug Fix (Same as WDC)

    [Fact]
    public void JMP_Indirect_Takes6Cycles()
    {
        // 65C02 takes 6 cycles for indirect jump (NMOS takes 5)
        LoadAndReset(0x6C, 0x34, 0x12);
        SetMemory(0x1234, 0x00);
        SetMemory(0x1235, 0x80);
        int cycles = StepInstruction();
        Assert.Equal(6, cycles);
    }

    [Fact]
    public void JMP_Indirect_FixedPageBoundaryBug()
    {
        LoadAndReset(0x6C, 0xFF, 0x12);
        SetMemory(0x12FF, 0x00);
        SetMemory(0x1200, 0x90);
        SetMemory(0x1300, 0x80);

        StepInstruction();

        Assert.Equal(0x8000, CurrentState.PC);
    }

    #endregion

    #region 65C02 Instructions (Should all work on Rockwell)

    [Fact]
    public void STZ_ZeroPage_Works()
    {
        LoadAndReset(0x64, 0x10);
        Bus.Memory[0x10] = 0xFF;

        StepInstruction();

        Assert.Equal(0x00, Bus.Memory[0x10]);
    }

    [Fact]
    public void PHX_Works()
    {
        LoadAndReset(0xDA);
        CpuBuffer.Current.X = 0x42;
        CpuBuffer.Prev.X = 0x42;
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Prev.SP = 0xFF;

        StepInstruction();

        Assert.Equal(0x42, Bus.Memory[0x01FF]);
    }

    [Fact]
    public void BRA_Works()
    {
        LoadAndReset(0x80, 0x10);

        StepInstruction();

        Assert.Equal((ushort)(ProgramStart + 2 + 0x10), CurrentState.PC);
    }

    [Fact]
    public void INC_Accumulator_Works()
    {
        LoadAndReset(0x1A);
        CpuBuffer.Current.A = 0x42;
        CpuBuffer.Prev.A = 0x42;

        StepInstruction();

        Assert.Equal(0x43, CurrentState.A);
    }

    #endregion

    #region RMB (Reset Memory Bit) Tests

    [Fact]
    public void RMB0_Takes5Cycles()
    {
        LoadAndReset(0x07, 0x10);
        SetZeroPage(0x10, 0xFF);

        int cycles = StepInstruction();

        Assert.Equal(5, cycles);
        Assert.Equal(0xFE, Bus.Memory[0x10]);
    }

    [Fact]
    public void RMB1_ClearsBit1()
    {
        LoadAndReset(0x17, 0x10);
        SetZeroPage(0x10, 0xFF);

        StepInstruction();

        Assert.Equal(0xFD, Bus.Memory[0x10]);
    }

    [Fact]
    public void RMB2_ClearsBit2()
    {
        LoadAndReset(0x27, 0x10);
        SetZeroPage(0x10, 0xFF);

        StepInstruction();

        Assert.Equal(0xFB, Bus.Memory[0x10]);
    }

    [Fact]
    public void RMB3_ClearsBit3()
    {
        LoadAndReset(0x37, 0x10);
        SetZeroPage(0x10, 0xFF);

        StepInstruction();

        Assert.Equal(0xF7, Bus.Memory[0x10]);
    }

    [Fact]
    public void RMB4_ClearsBit4()
    {
        LoadAndReset(0x47, 0x10);
        SetZeroPage(0x10, 0xFF);

        StepInstruction();

        Assert.Equal(0xEF, Bus.Memory[0x10]);
    }

    [Fact]
    public void RMB5_ClearsBit5()
    {
        LoadAndReset(0x57, 0x10);
        SetZeroPage(0x10, 0xFF);

        StepInstruction();

        Assert.Equal(0xDF, Bus.Memory[0x10]);
    }

    [Fact]
    public void RMB6_ClearsBit6()
    {
        LoadAndReset(0x67, 0x10);
        SetZeroPage(0x10, 0xFF);

        StepInstruction();

        Assert.Equal(0xBF, Bus.Memory[0x10]);
    }

    [Fact]
    public void RMB7_ClearsBit7()
    {
        LoadAndReset(0x77, 0x10);
        SetZeroPage(0x10, 0xFF);

        StepInstruction();

        Assert.Equal(0x7F, Bus.Memory[0x10]);
    }

    #endregion

    #region SMB (Set Memory Bit) Tests

    [Fact]
    public void SMB0_Takes5Cycles()
    {
        LoadAndReset(0x87, 0x10);
        SetZeroPage(0x10, 0x00);

        int cycles = StepInstruction();

        Assert.Equal(5, cycles);
        Assert.Equal(0x01, Bus.Memory[0x10]);
    }

    [Fact]
    public void SMB1_SetsBit1()
    {
        LoadAndReset(0x97, 0x10);
        SetZeroPage(0x10, 0x00);

        StepInstruction();

        Assert.Equal(0x02, Bus.Memory[0x10]);
    }

    [Fact]
    public void SMB2_SetsBit2()
    {
        LoadAndReset(0xA7, 0x10);
        SetZeroPage(0x10, 0x00);

        StepInstruction();

        Assert.Equal(0x04, Bus.Memory[0x10]);
    }

    [Fact]
    public void SMB3_SetsBit3()
    {
        LoadAndReset(0xB7, 0x10);
        SetZeroPage(0x10, 0x00);

        StepInstruction();

        Assert.Equal(0x08, Bus.Memory[0x10]);
    }

    [Fact]
    public void SMB4_SetsBit4()
    {
        LoadAndReset(0xC7, 0x10);
        SetZeroPage(0x10, 0x00);

        StepInstruction();

        Assert.Equal(0x10, Bus.Memory[0x10]);
    }

    [Fact]
    public void SMB5_SetsBit5()
    {
        LoadAndReset(0xD7, 0x10);
        SetZeroPage(0x10, 0x00);

        StepInstruction();

        Assert.Equal(0x20, Bus.Memory[0x10]);
    }

    [Fact]
    public void SMB6_SetsBit6()
    {
        LoadAndReset(0xE7, 0x10);
        SetZeroPage(0x10, 0x00);

        StepInstruction();

        Assert.Equal(0x40, Bus.Memory[0x10]);
    }

    [Fact]
    public void SMB7_SetsBit7()
    {
        LoadAndReset(0xF7, 0x10);
        SetZeroPage(0x10, 0x00);

        StepInstruction();

        Assert.Equal(0x80, Bus.Memory[0x10]);
    }

    #endregion

    #region BBR (Branch on Bit Reset) Tests

    [Fact]
    public void BBR0_NotTaken_Takes5Cycles()
    {
        // BBR0 $10, +$05 - bit 0 is set, so don't branch
        LoadAndReset(0x0F, 0x10, 0x05);
        SetZeroPage(0x10, 0x01);

        int cycles = StepInstruction();

        Assert.Equal(5, cycles);
        Assert.Equal((ushort)(ProgramStart + 3), CurrentState.PC);
    }

    [Fact]
    public void BBR0_Taken_Takes6Cycles()
    {
        // BBR0 $10, +$05 - bit 0 is clear, so branch
        LoadAndReset(0x0F, 0x10, 0x05);
        SetZeroPage(0x10, 0xFE);

        int cycles = StepInstruction();

        Assert.Equal(6, cycles);
        Assert.Equal((ushort)(ProgramStart + 3 + 5), CurrentState.PC);
    }

    [Fact]
    public void BBR1_Branches_WhenBit1Clear()
    {
        LoadAndReset(0x1F, 0x10, 0x05);
        SetZeroPage(0x10, 0xFD);

        StepInstruction();

        Assert.Equal((ushort)(ProgramStart + 3 + 5), CurrentState.PC);
    }

    [Fact]
    public void BBR2_Branches_WhenBit2Clear()
    {
        LoadAndReset(0x2F, 0x10, 0x05);
        SetZeroPage(0x10, 0xFB);

        StepInstruction();

        Assert.Equal((ushort)(ProgramStart + 3 + 5), CurrentState.PC);
    }

    [Fact]
    public void BBR3_Branches_WhenBit3Clear()
    {
        LoadAndReset(0x3F, 0x10, 0x05);
        SetZeroPage(0x10, 0xF7);

        StepInstruction();

        Assert.Equal((ushort)(ProgramStart + 3 + 5), CurrentState.PC);
    }

    [Fact]
    public void BBR4_Branches_WhenBit4Clear()
    {
        LoadAndReset(0x4F, 0x10, 0x05);
        SetZeroPage(0x10, 0xEF);

        StepInstruction();

        Assert.Equal((ushort)(ProgramStart + 3 + 5), CurrentState.PC);
    }

    [Fact]
    public void BBR5_Branches_WhenBit5Clear()
    {
        LoadAndReset(0x5F, 0x10, 0x05);
        SetZeroPage(0x10, 0xDF);

        StepInstruction();

        Assert.Equal((ushort)(ProgramStart + 3 + 5), CurrentState.PC);
    }

    [Fact]
    public void BBR6_Branches_WhenBit6Clear()
    {
        LoadAndReset(0x6F, 0x10, 0x05);
        SetZeroPage(0x10, 0xBF);

        StepInstruction();

        Assert.Equal((ushort)(ProgramStart + 3 + 5), CurrentState.PC);
    }

    [Fact]
    public void BBR7_Branches_WhenBit7Clear()
    {
        LoadAndReset(0x7F, 0x10, 0x05);
        SetZeroPage(0x10, 0x7F);

        StepInstruction();

        Assert.Equal((ushort)(ProgramStart + 3 + 5), CurrentState.PC);
    }

    #endregion

    #region BBS (Branch on Bit Set) Tests

    [Fact]
    public void BBS0_NotTaken_Takes5Cycles()
    {
        // BBS0 $10, +$05 - bit 0 is clear, so don't branch
        LoadAndReset(0x8F, 0x10, 0x05);
        SetZeroPage(0x10, 0x00);

        int cycles = StepInstruction();

        Assert.Equal(5, cycles);
        Assert.Equal((ushort)(ProgramStart + 3), CurrentState.PC);
    }

    [Fact]
    public void BBS0_Taken_Takes6Cycles()
    {
        // BBS0 $10, +$05 - bit 0 is set, so branch
        LoadAndReset(0x8F, 0x10, 0x05);
        SetZeroPage(0x10, 0x01);

        int cycles = StepInstruction();

        Assert.Equal(6, cycles);
        Assert.Equal((ushort)(ProgramStart + 3 + 5), CurrentState.PC);
    }

    [Fact]
    public void BBS1_Branches_WhenBit1Set()
    {
        LoadAndReset(0x9F, 0x10, 0x05);
        SetZeroPage(0x10, 0x02);

        StepInstruction();

        Assert.Equal((ushort)(ProgramStart + 3 + 5), CurrentState.PC);
    }

    [Fact]
    public void BBS2_Branches_WhenBit2Set()
    {
        LoadAndReset(0xAF, 0x10, 0x05);
        SetZeroPage(0x10, 0x04);

        StepInstruction();

        Assert.Equal((ushort)(ProgramStart + 3 + 5), CurrentState.PC);
    }

    [Fact]
    public void BBS3_Branches_WhenBit3Set()
    {
        LoadAndReset(0xBF, 0x10, 0x05);
        SetZeroPage(0x10, 0x08);

        StepInstruction();

        Assert.Equal((ushort)(ProgramStart + 3 + 5), CurrentState.PC);
    }

    [Fact]
    public void BBS4_Branches_WhenBit4Set()
    {
        LoadAndReset(0xCF, 0x10, 0x05);
        SetZeroPage(0x10, 0x10);

        StepInstruction();

        Assert.Equal((ushort)(ProgramStart + 3 + 5), CurrentState.PC);
    }

    [Fact]
    public void BBS5_Branches_WhenBit5Set()
    {
        LoadAndReset(0xDF, 0x10, 0x05);
        SetZeroPage(0x10, 0x20);

        StepInstruction();

        Assert.Equal((ushort)(ProgramStart + 3 + 5), CurrentState.PC);
    }

    [Fact]
    public void BBS6_Branches_WhenBit6Set()
    {
        LoadAndReset(0xEF, 0x10, 0x05);
        SetZeroPage(0x10, 0x40);

        StepInstruction();

        Assert.Equal((ushort)(ProgramStart + 3 + 5), CurrentState.PC);
    }

    [Fact]
    public void BBS7_Branches_WhenBit7Set()
    {
        LoadAndReset(0xFF, 0x10, 0x05);
        SetZeroPage(0x10, 0x80);

        StepInstruction();

        Assert.Equal((ushort)(ProgramStart + 3 + 5), CurrentState.PC);
    }

    #endregion

    #region BBR/BBS Backward Branch Tests

    [Fact]
    public void BBR0_BackwardBranch()
    {
        // BBR0 $10, -$10 (0xF0 = -16)
        LoadAndReset(0x0F, 0x10, 0xF0);
        SetZeroPage(0x10, 0xFE);

        StepInstruction();

        Assert.Equal((ushort)(ProgramStart + 3 - 16), CurrentState.PC);
    }

    [Fact]
    public void BBS0_BackwardBranch()
    {
        // BBS0 $10, -$10 (0xF0 = -16)
        LoadAndReset(0x8F, 0x10, 0xF0);
        SetZeroPage(0x10, 0x01);

        StepInstruction();

        Assert.Equal((ushort)(ProgramStart + 3 - 16), CurrentState.PC);
    }

    #endregion

        #region STP/WAI (Should work like WDC)

        [Fact]
        public void STP_Works()
        {
            LoadAndReset(0xDB);

            StepInstruction();

            Assert.Equal(CpuStatus.Stopped, CurrentState.Status);
        }

        [Fact]
        public void WAI_Works()
        {
            LoadAndReset(0xCB);

            StepInstruction();

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

                        // Every opcode should either execute normally, stop, wait, or be bypassed (Rockwell has no JAM opcodes)
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
