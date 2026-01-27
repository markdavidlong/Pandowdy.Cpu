namespace Pandowdy.Cpu.Tests;

/// <summary>
/// Base class for CPU instruction tests providing common setup and helper methods.
/// Each CPU variant should have its own derived test class.
/// </summary>
public abstract class CpuTestBase
{
    protected const ushort ProgramStart = 0x0400;
    protected const ushort IrqHandler = 0x0500;
    protected const ushort NmiHandler = 0x0600;

    /// <summary>
    /// The CPU variant to test. Override in derived classes.
    /// </summary>
    protected abstract CpuVariant Variant { get; }

    protected TestRamBus Bus { get; private set; } = null!;
    protected CpuStateBuffer CpuBuffer { get; private set; } = null!;

    protected void SetupCpu()
    {
        Bus = new TestRamBus();
        CpuBuffer = new CpuStateBuffer();
        Bus.SetResetVector(ProgramStart);
        Bus.SetIrqVector(IrqHandler);
        Bus.SetNmiVector(NmiHandler);
        Cpu.Reset(CpuBuffer, Bus);
    }

    /// <summary>
    /// Loads a program and sets up the CPU to execute from ProgramStart.
    /// </summary>
    protected void LoadAndReset(params byte[] program)
    {
        SetupCpu();
        Bus.LoadProgram(ProgramStart, program);
        Cpu.Reset(CpuBuffer, Bus);
    }

    /// <summary>
    /// Steps through one complete instruction and returns the number of cycles taken.
    /// Uses the variant specified by the derived class.
    /// </summary>
    protected int StepInstruction()
    {
        return Cpu.Step(Variant, CpuBuffer, Bus);
    }

    /// <summary>
    /// Steps through one complete instruction with an explicit variant override.
    /// </summary>
    protected int StepInstruction(CpuVariant variant)
    {
        return Cpu.Step(variant, CpuBuffer, Bus);
    }

    /// <summary>
    /// Executes cycles until the instruction completes, returning the cycle count.
    /// </summary>
    protected int ExecuteInstruction()
    {
        return ExecuteInstruction(Variant);
    }

    /// <summary>
    /// Executes cycles until the instruction completes, returning the cycle count.
    /// </summary>
    protected int ExecuteInstruction(CpuVariant variant)
    {
        int cycles = 0;
        bool complete = false;
        while (!complete)
        {
            complete = Cpu.Clock(variant, CpuBuffer, Bus);
            cycles++;
        }
        return cycles;
    }

    /// <summary>
    /// Gets the current CPU state (after instruction completes, this is in Prev).
    /// </summary>
    protected CpuState CurrentState => CpuBuffer.Prev;

    /// <summary>
    /// Helper to set a value in zero page.
    /// </summary>
    protected void SetZeroPage(byte address, byte value)
    {
        Bus.Memory[address] = value;
    }

    /// <summary>
    /// Helper to set a 16-bit pointer in zero page (little-endian).
    /// </summary>
    protected void SetZeroPagePointer(byte address, ushort pointer)
    {
        Bus.Memory[address] = (byte)(pointer & 0xFF);
        Bus.Memory[(byte)(address + 1)] = (byte)(pointer >> 8);
    }

    /// <summary>
    /// Helper to set a value at an absolute address.
    /// </summary>
    protected void SetMemory(ushort address, byte value)
    {
        Bus.Memory[address] = value;
    }
}
