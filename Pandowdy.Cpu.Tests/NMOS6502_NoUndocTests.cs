using Xunit;

namespace Pandowdy.Cpu.Tests;

/// <summary>
/// Tests for NMOS 6502 CPU without undocumented opcodes.
/// Inherits all core instruction tests.
/// This variant treats undefined opcodes as NOPs.
/// </summary>
public class NMOS6502_NoUndocTests : CoreInstructionTests
{
    protected override CpuVariant Variant => CpuVariant.NMOS6502_NO_UNDOC;

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

    #region Undefined Opcodes Treated as NOPs

    [Fact]
    public void Opcode0x1A_IsNOP()
    {
        LoadAndReset([0x1A]);
        CpuBuffer.Current.A = 0x42;
        CpuBuffer.Prev.A = 0x42;

        int cycles = StepInstruction();

        Assert.Equal(2, cycles);
        Assert.Equal(0x42, CurrentState.A);
    }

    [Fact]
    public void Opcode0x3A_IsNOP()
    {
        LoadAndReset([0x3A]);
        CpuBuffer.Current.A = 0x42;
        CpuBuffer.Prev.A = 0x42;

        StepInstruction();

        Assert.Equal(0x42, CurrentState.A);
    }

    [Fact]
    public void Opcode0x5A_IsNOP()
    {
        LoadAndReset([0x5A]);
        CpuBuffer.Current.Y = 0x42;
        CpuBuffer.Prev.Y = 0x42;
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Prev.SP = 0xFF;

        StepInstruction();

        Assert.Equal(0xFF, CurrentState.SP);
    }

    [Fact]
    public void Opcode0x7A_IsNOP()
    {
        LoadAndReset([0x7A]);
        CpuBuffer.Current.Y = 0x42;
        CpuBuffer.Prev.Y = 0x42;

        StepInstruction();

        Assert.Equal(0x42, CurrentState.Y);
    }

    [Fact]
    public void Opcode0xDA_IsNOP()
    {
        LoadAndReset([0xDA]);
        CpuBuffer.Current.X = 0x42;
        CpuBuffer.Prev.X = 0x42;
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Prev.SP = 0xFF;

        StepInstruction();

        Assert.Equal(0xFF, CurrentState.SP);
    }

    [Fact]
    public void Opcode0xFA_IsNOP()
    {
        LoadAndReset([0xFA]);
        CpuBuffer.Current.X = 0x42;
        CpuBuffer.Prev.X = 0x42;

        StepInstruction();

        Assert.Equal(0x42, CurrentState.X);
    }

    [Fact]
    public void Opcode0x80_IsNOP_Immediate()
    {
        // 0x80 skips one byte (NOP immediate)
        LoadAndReset(0x80, 0x10);

        int cycles = StepInstruction();

        Assert.Equal(2, cycles);
        Assert.Equal((ushort)(ProgramStart + 2), CurrentState.PC);
    }

    [Fact]
    public void Opcode0x04_IsNOP_ZeroPage()
    {
        // 0x04 is 3-cycle NOP (reads zero page)
        LoadAndReset(0x04, 0x10);

        int cycles = StepInstruction();

        Assert.Equal(3, cycles);
    }

    [Fact]
    public void Opcode0x0C_IsNOP_Absolute()
    {
        // 0x0C is 4-cycle NOP (reads absolute)
        LoadAndReset(0x0C, 0x34, 0x12);

        int cycles = StepInstruction();

        Assert.Equal(4, cycles);
    }

    #endregion

    #region Undefined Opcodes Don't Execute as Undocumented Operations

    [Fact]
    public void Opcode0xA7_DoesNotExecuteLAX()
    {
        // On NO_UNDOC variant, 0xA7 should NOT load both A and X
        LoadAndReset(0xA7, 0x10);
        CpuBuffer.Current.A = 0x00;
        CpuBuffer.Prev.A = 0x00;
        CpuBuffer.Current.X = 0x00;
        CpuBuffer.Prev.X = 0x00;
        SetZeroPage(0x10, 0x42);

        StepInstruction();

        // In NO_UNDOC mode, this should be treated as something else
        // (likely unimplemented/NOP behavior)
        Assert.NotEqual((0x42, 0x42), (CurrentState.A, CurrentState.X));
        }

        [Fact]
        public void Opcode0x87_DoesNotExecuteSAX()
        {
            // On NO_UNDOC variant, 0x87 should NOT store A AND X
            LoadAndReset(0x87, 0x10);
            CpuBuffer.Current.A = 0xF0;
            CpuBuffer.Prev.A = 0xF0;
            CpuBuffer.Current.X = 0x0F;
            CpuBuffer.Prev.X = 0x0F;
            Bus.Memory[0x10] = 0xFF;

            StepInstruction();

            // Should NOT have stored A AND X (0x00)
            Assert.NotEqual(0x00, Bus.Memory[0x10]);
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

                // Every opcode should execute without crashing (NO_UNDOC treats undefined as NOPs)
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
