using Xunit;

namespace Pandowdy.Cpu.Tests;

/// <summary>
/// Tests for NMOS 6502 CPU with undocumented opcodes.
/// Inherits all core instruction tests and adds NMOS-specific tests.
/// </summary>
public class NMOS6502Tests : CoreInstructionTests
{
    protected override CpuVariant Variant => CpuVariant.NMOS6502;

    #region JMP Indirect Page Boundary Bug (NMOS specific)

    [Fact]
    public void JMP_Indirect_HasPageBoundaryBug()
    {
        // On NMOS, JMP ($12FF) reads high byte from $1200 instead of $1300
        LoadAndReset(0x6C, 0xFF, 0x12);
        SetMemory(0x12FF, 0x00);
        SetMemory(0x1200, 0x90);
        SetMemory(0x1300, 0x80);

        StepInstruction();

        Assert.Equal(0x9000, CurrentState.PC);
    }

    #endregion

    #region Undocumented Opcodes

    [Fact]
    public void LAX_ZeroPage_LoadsAAndX()
    {
        // LAX $10 (0xA7)
        LoadAndReset(0xA7, 0x10);
        SetZeroPage(0x10, 0x42);

        int cycles = StepInstruction();

        Assert.Equal(3, cycles);
        Assert.Equal(0x42, CurrentState.A);
        Assert.Equal(0x42, CurrentState.X);
    }

    [Fact]
    public void LAX_ZeroPageY_LoadsAAndX()
    {
        // LAX $10,Y (0xB7)
        LoadAndReset(0xB7, 0x10);
        CpuBuffer.Current.Y = 0x05;
        CpuBuffer.Prev.Y = 0x05;
        SetZeroPage(0x15, 0x42);

        int cycles = StepInstruction();

        Assert.Equal(4, cycles);
        Assert.Equal(0x42, CurrentState.A);
        Assert.Equal(0x42, CurrentState.X);
    }

    [Fact]
    public void LAX_Absolute_LoadsAAndX()
    {
        // LAX $1234 (0xAF)
        LoadAndReset(0xAF, 0x34, 0x12);
        SetMemory(0x1234, 0x42);

        int cycles = StepInstruction();

        Assert.Equal(4, cycles);
        Assert.Equal(0x42, CurrentState.A);
        Assert.Equal(0x42, CurrentState.X);
    }

    [Fact]
    public void SAX_ZeroPage_StoresAAndX()
    {
        // SAX $10 (0x87) - Store A AND X
        LoadAndReset(0x87, 0x10);
        CpuBuffer.Current.A = 0xF0;
        CpuBuffer.Prev.A = 0xF0;
        CpuBuffer.Current.X = 0x0F;
        CpuBuffer.Prev.X = 0x0F;

        int cycles = StepInstruction();

        Assert.Equal(3, cycles);
        Assert.Equal(0x00, Bus.Memory[0x10]);
    }

    [Fact]
    public void SAX_Absolute_StoresAAndX()
    {
        // SAX $1234 (0x8F)
        LoadAndReset(0x8F, 0x34, 0x12);
        CpuBuffer.Current.A = 0xFF;
        CpuBuffer.Prev.A = 0xFF;
        CpuBuffer.Current.X = 0x0F;
        CpuBuffer.Prev.X = 0x0F;

        int cycles = StepInstruction();

        Assert.Equal(4, cycles);
        Assert.Equal(0x0F, Bus.Memory[0x1234]);
    }

    [Fact]
    public void DCP_ZeroPage_DecrementsAndCompares()
    {
        // DCP $10 (0xC7)
        LoadAndReset(0xC7, 0x10);
        CpuBuffer.Current.A = 0x42;
        CpuBuffer.Prev.A = 0x42;
        SetZeroPage(0x10, 0x43);

        int cycles = StepInstruction();

        Assert.Equal(5, cycles);
        Assert.Equal(0x42, Bus.Memory[0x10]);
        Assert.True(CurrentState.ZeroFlag);
        Assert.True(CurrentState.CarryFlag);
    }

    [Fact]
    public void ISC_ZeroPage_IncrementsAndSubtracts()
    {
        // ISC/ISB $10 (0xE7)
        LoadAndReset(0xE7, 0x10);
        CpuBuffer.Current.A = 0x50;
        CpuBuffer.Prev.A = 0x50;
        CpuBuffer.Current.CarryFlag = true;
        CpuBuffer.Prev.CarryFlag = true;
        SetZeroPage(0x10, 0x0F);

        int cycles = StepInstruction();

        Assert.Equal(5, cycles);
        Assert.Equal(0x10, Bus.Memory[0x10]);
        Assert.Equal(0x40, CurrentState.A);
    }

    [Fact]
    public void SLO_ZeroPage_ShiftsLeftAndOrs()
    {
        // SLO $10 (0x07)
        LoadAndReset(0x07, 0x10);
        CpuBuffer.Current.A = 0x00;
        CpuBuffer.Prev.A = 0x00;
        SetZeroPage(0x10, 0x40);

        int cycles = StepInstruction();

        Assert.Equal(5, cycles);
        Assert.Equal(0x80, Bus.Memory[0x10]);
        Assert.Equal(0x80, CurrentState.A);
    }

    [Fact]
    public void RLA_ZeroPage_RotatesLeftAndAnds()
    {
        // RLA $10 (0x27)
        LoadAndReset(0x27, 0x10);
        CpuBuffer.Current.A = 0xFF;
        CpuBuffer.Prev.A = 0xFF;
        CpuBuffer.Current.CarryFlag = false;
        CpuBuffer.Prev.CarryFlag = false;
        SetZeroPage(0x10, 0x40);

        int cycles = StepInstruction();

        Assert.Equal(5, cycles);
        Assert.Equal(0x80, Bus.Memory[0x10]);
        Assert.Equal(0x80, CurrentState.A);
    }

    [Fact]
    public void SRE_ZeroPage_ShiftsRightAndEors()
    {
        // SRE $10 (0x47)
        LoadAndReset(0x47, 0x10);
        CpuBuffer.Current.A = 0x00;
        CpuBuffer.Prev.A = 0x00;
        SetZeroPage(0x10, 0x02);

        int cycles = StepInstruction();

        Assert.Equal(5, cycles);
        Assert.Equal(0x01, Bus.Memory[0x10]);
        Assert.Equal(0x01, CurrentState.A);
    }

    [Fact]
    public void RRA_ZeroPage_RotatesRightAndAdds()
    {
        // RRA $10 (0x67)
        LoadAndReset(0x67, 0x10);
        CpuBuffer.Current.A = 0x00;
        CpuBuffer.Prev.A = 0x00;
        CpuBuffer.Current.CarryFlag = true;
        CpuBuffer.Prev.CarryFlag = true;
        SetZeroPage(0x10, 0x02);

        int cycles = StepInstruction();

        Assert.Equal(5, cycles);
        Assert.Equal(0x81, Bus.Memory[0x10]);
    }

    [Fact]
    public void ANC_Immediate_AndsAndSetsCarryFromN()
    {
        // ANC #$80 (0x0B)
        LoadAndReset(0x0B, 0x80);
        CpuBuffer.Current.A = 0xFF;
        CpuBuffer.Prev.A = 0xFF;

        int cycles = StepInstruction();

        Assert.Equal(2, cycles);
        Assert.Equal(0x80, CurrentState.A);
        Assert.True(CurrentState.CarryFlag);
        Assert.True(CurrentState.NegativeFlag);
    }

    [Fact]
    public void ALR_Immediate_AndsAndShiftsRight()
    {
        // ALR #$FF (0x4B)
        LoadAndReset(0x4B, 0xFF);
        CpuBuffer.Current.A = 0x02;
        CpuBuffer.Prev.A = 0x02;

        int cycles = StepInstruction();

        Assert.Equal(2, cycles);
        Assert.Equal(0x01, CurrentState.A);
        Assert.False(CurrentState.CarryFlag);
    }

    [Fact]
    public void ARR_Immediate_AndsAndRotatesRight()
    {
        // ARR #$FF (0x6B)
        LoadAndReset(0x6B, 0xFF);
        CpuBuffer.Current.A = 0x02;
        CpuBuffer.Prev.A = 0x02;
        CpuBuffer.Current.CarryFlag = true;
        CpuBuffer.Prev.CarryFlag = true;

        int cycles = StepInstruction();

        Assert.Equal(2, cycles);
        Assert.Equal(0x81, CurrentState.A);
    }

    [Fact]
    public void AXS_Immediate_SubtractsFromAAndX()
    {
        // AXS #$10 (0xCB)
        LoadAndReset(0xCB, 0x10);
        CpuBuffer.Current.A = 0xFF;
        CpuBuffer.Prev.A = 0xFF;
        CpuBuffer.Current.X = 0x20;
        CpuBuffer.Prev.X = 0x20;

        int cycles = StepInstruction();

        Assert.Equal(2, cycles);
        Assert.Equal(0x10, CurrentState.X);
    }

    [Theory]
    [InlineData(0x02)]
    [InlineData(0x12)]
    [InlineData(0x22)]
    [InlineData(0x32)]
    [InlineData(0x42)]
    [InlineData(0x52)]
    [InlineData(0x62)]
    [InlineData(0x72)]
    [InlineData(0x92)]
    [InlineData(0xB2)]
    [InlineData(0xD2)]
    [InlineData(0xF2)]
    public void JAM_FreezesProcessor(byte opcode)
    {
        // All JAM/KIL opcodes should freeze the processor
        LoadAndReset([opcode]);

        StepInstruction();

        Assert.Equal(CpuStatus.Jammed, CurrentState.Status);
    }

    [Fact]
    public void JAM_DoesNotAdvancePC_WhileJammed()
    {
        // JAM (0x02) followed by NOP
        LoadAndReset([0x02, 0xEA]);

        StepInstruction();
        ushort pcAfterJam = CurrentState.PC;

        // Step again while jammed - PC should not advance
        StepInstruction();

        Assert.Equal(CpuStatus.Jammed, CurrentState.Status);
        Assert.Equal(pcAfterJam, CurrentState.PC);
    }

    [Fact]
    public void JAM_ActsAsNOP_WhenIgnoreHaltStopWaitIsTrue()
    {
        // JAM (0x02) followed by LDA #$42
        LoadAndReset([0x02, 0xA9, 0x42]);
        CpuBuffer.Current.IgnoreHaltStopWait = true;
        CpuBuffer.Prev.IgnoreHaltStopWait = true;

        StepInstruction();

        // Should be Bypassed (JAM was encountered but bypassed)
        Assert.Equal(CpuStatus.Bypassed, CurrentState.Status);

        // PC should have advanced past the JAM instruction
        StepInstruction();
        Assert.Equal(0x42, CurrentState.A);
    }

    #endregion

    #region Cycle Count Tests for Undocumented Addressing Modes

    [Fact]
    public void DCP_Absolute_Takes6Cycles()
    {
        LoadAndReset([0xCF, 0x34, 0x12]);
        CpuBuffer.Current.A = 0x42;
        CpuBuffer.Prev.A = 0x42;
        SetMemory(0x1234, 0x43);

        int cycles = StepInstruction();

        Assert.Equal(6, cycles);
    }

    [Fact]
    public void DCP_AbsoluteX_Takes7Cycles()
    {
        LoadAndReset([0xDF, 0x00, 0x12]);
        CpuBuffer.Current.A = 0x42;
        CpuBuffer.Prev.A = 0x42;
        CpuBuffer.Current.X = 0x10;
        CpuBuffer.Prev.X = 0x10;
        SetMemory(0x1210, 0x43);

        int cycles = StepInstruction();

        Assert.Equal(7, cycles);
    }

    [Fact]
    public void ISC_Absolute_Takes6Cycles()
    {
        LoadAndReset(0xEF, 0x34, 0x12);
        CpuBuffer.Current.A = 0x50;
        CpuBuffer.Prev.A = 0x50;
        CpuBuffer.Current.CarryFlag = true;
        CpuBuffer.Prev.CarryFlag = true;
        SetMemory(0x1234, 0x0F);

        int cycles = StepInstruction();

        Assert.Equal(6, cycles);
    }

    [Fact]
    public void SLO_Absolute_Takes6Cycles()
    {
        LoadAndReset(0x0F, 0x34, 0x12);
        SetMemory(0x1234, 0x40);

        int cycles = StepInstruction();

        Assert.Equal(6, cycles);
    }

    [Fact]
    public void RLA_Absolute_Takes6Cycles()
    {
        LoadAndReset(0x2F, 0x34, 0x12);
        CpuBuffer.Current.A = 0xFF;
        CpuBuffer.Prev.A = 0xFF;
        SetMemory(0x1234, 0x40);

        int cycles = StepInstruction();

        Assert.Equal(6, cycles);
    }

    [Fact]
    public void SRE_Absolute_Takes6Cycles()
    {
        LoadAndReset(0x4F, 0x34, 0x12);
        SetMemory(0x1234, 0x02);

        int cycles = StepInstruction();

        Assert.Equal(6, cycles);
    }

    [Fact]
    public void RRA_Absolute_Takes6Cycles()
    {
        LoadAndReset(0x6F, 0x34, 0x12);
        CpuBuffer.Current.CarryFlag = true;
        CpuBuffer.Prev.CarryFlag = true;
        SetMemory(0x1234, 0x02);

        int cycles = StepInstruction();

        Assert.Equal(6, cycles);
    }

    #endregion

    #region NOP Variants (NMOS treats some as multi-byte NOPs)

    [Fact]
    public void NOP_0x1A_IsNOP_OnNMOS()
    {
        // 0x1A is NOP on NMOS
        LoadAndReset(0x1A);
        CpuBuffer.Current.A = 0x42;
        CpuBuffer.Prev.A = 0x42;

        int cycles = StepInstruction();

        Assert.Equal(2, cycles);
        Assert.Equal(0x42, CurrentState.A);
    }

        [Fact]
        public void NOP_0x3A_IsNOP_OnNMOS()
        {
            // 0x3A is NOP on NMOS
                    LoadAndReset([0x3A]);
                    CpuBuffer.Current.A = 0x42;
                    CpuBuffer.Prev.A = 0x42;

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

                    // Every opcode should either execute normally, jam, stop, wait, or be bypassed
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
