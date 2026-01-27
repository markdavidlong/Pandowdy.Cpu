using Xunit;

namespace Pandowdy.Cpu.Tests;

/// <summary>
/// Tests for CpuStateBuffer class functionality.
/// </summary>
public class CpuStateBufferTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_CreatesBothStates()
    {
        var buffer = new CpuStateBuffer();

        Assert.NotNull(buffer.Prev);
        Assert.NotNull(buffer.Current);
        Assert.NotSame(buffer.Prev, buffer.Current);
    }

    [Fact]
    public void Constructor_BothStatesAreReset()
    {
        var buffer = new CpuStateBuffer();

        Assert.Equal(0, buffer.Prev.A);
        Assert.Equal(0, buffer.Current.A);
        Assert.Equal(0xFD, buffer.Prev.SP);
        Assert.Equal(0xFD, buffer.Current.SP);
    }

    #endregion

    #region PrepareNextCycle Tests

    [Fact]
    public void PrepareNextCycle_CopiesPrevToCurrent()
    {
        var buffer = new CpuStateBuffer();
        buffer.Prev.A = 0x42;
        buffer.Prev.X = 0x10;
        buffer.Current.A = 0x00;
        buffer.Current.X = 0x00;

        buffer.PrepareNextCycle();

        Assert.Equal(0x42, buffer.Current.A);
        Assert.Equal(0x10, buffer.Current.X);
    }

    [Fact]
    public void PrepareNextCycle_PreservesCurrentAsSeparateObject()
    {
        var buffer = new CpuStateBuffer();
        buffer.Prev.A = 0x42;

        buffer.PrepareNextCycle();
        buffer.Current.A = 0xFF;

        Assert.Equal(0x42, buffer.Prev.A);
    }

    #endregion

    #region SwapIfComplete Tests

    [Fact]
    public void SwapIfComplete_WhenNotComplete_DoesNothing()
    {
        var buffer = new CpuStateBuffer();
        var originalPrev = buffer.Prev;
        var originalCurrent = buffer.Current;
        buffer.Current.InstructionComplete = false;

        buffer.SwapIfComplete();

        Assert.Same(originalPrev, buffer.Prev);
        Assert.Same(originalCurrent, buffer.Current);
    }

    [Fact]
    public void SwapIfComplete_WhenComplete_SwapsReferences()
    {
        var buffer = new CpuStateBuffer();
        var originalPrev = buffer.Prev;
        var originalCurrent = buffer.Current;
        buffer.Current.InstructionComplete = true;

        buffer.SwapIfComplete();

        Assert.Same(originalCurrent, buffer.Prev);
        Assert.Same(originalPrev, buffer.Current);
    }

    [Fact]
    public void SwapIfComplete_ResetsNewCurrentState()
    {
        var buffer = new CpuStateBuffer();
        buffer.Current.A = 0x42;
        buffer.Current.PipelineIndex = 5;
        buffer.Current.Pipeline = new Action<CpuState, CpuState, IPandowdyCpuBus>[10];
        buffer.Current.InstructionComplete = true;

        buffer.SwapIfComplete();

        Assert.False(buffer.Current.InstructionComplete);
        Assert.Equal(0, buffer.Current.PipelineIndex);
        Assert.Empty(buffer.Current.Pipeline);
    }

    [Fact]
    public void SwapIfComplete_PreservesCompletedStateInPrev()
    {
        var buffer = new CpuStateBuffer();
        buffer.Current.A = 0x42;
        buffer.Current.PC = 0x1234;
        buffer.Current.InstructionComplete = true;

        buffer.SwapIfComplete();

        Assert.Equal(0x42, buffer.Prev.A);
        Assert.Equal(0x1234, buffer.Prev.PC);
    }

    [Fact]
    public void SwapIfComplete_CopiesPrevToNewCurrent()
    {
        var buffer = new CpuStateBuffer();
        buffer.Current.A = 0x42;
        buffer.Current.X = 0x10;
        buffer.Current.InstructionComplete = true;

        buffer.SwapIfComplete();

        Assert.Equal(0x42, buffer.Current.A);
        Assert.Equal(0x10, buffer.Current.X);
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ResetsBothStates()
    {
        var buffer = new CpuStateBuffer();
        buffer.Prev.A = 0xFF;
        buffer.Prev.X = 0xFF;
        buffer.Current.A = 0xEE;
        buffer.Current.X = 0xEE;

        buffer.Reset();

        Assert.Equal(0, buffer.Prev.A);
        Assert.Equal(0, buffer.Prev.X);
        Assert.Equal(0, buffer.Current.A);
        Assert.Equal(0, buffer.Current.X);
    }

    [Fact]
    public void Reset_SetsStackPointerCorrectly()
    {
        var buffer = new CpuStateBuffer();
        buffer.Prev.SP = 0x00;
        buffer.Current.SP = 0x00;

        buffer.Reset();

        Assert.Equal(0xFD, buffer.Prev.SP);
        Assert.Equal(0xFD, buffer.Current.SP);
    }

    #endregion

    #region LoadResetVector Tests

    [Fact]
    public void LoadResetVector_SetsPCFromVector()
    {
        var buffer = new CpuStateBuffer();
        var bus = new TestRamBus();
        bus.SetResetVector(0x8000);

        buffer.LoadResetVector(bus);

        Assert.Equal(0x8000, buffer.Prev.PC);
        Assert.Equal(0x8000, buffer.Current.PC);
    }

    [Fact]
    public void LoadResetVector_HandlesLittleEndian()
    {
        var buffer = new CpuStateBuffer();
        var bus = new TestRamBus();
        bus.Memory[0xFFFC] = 0x34;
        bus.Memory[0xFFFD] = 0x12;

        buffer.LoadResetVector(bus);

        Assert.Equal(0x1234, buffer.Prev.PC);
    }

    #endregion

    #region Debugger Helper Tests

    [Fact]
    public void PcChanged_ReturnsFalse_WhenSame()
    {
        var buffer = new CpuStateBuffer();
        buffer.Prev.PC = 0x1000;
        buffer.Current.PC = 0x1000;

        Assert.False(buffer.PcChanged);
    }

    [Fact]
    public void PcChanged_ReturnsTrue_WhenDifferent()
    {
        var buffer = new CpuStateBuffer();
        buffer.Prev.PC = 0x1000;
        buffer.Current.PC = 0x1002;

        Assert.True(buffer.PcChanged);
    }

    [Fact]
    public void JumpOccurred_ReturnsFalse_WhenNotComplete()
    {
        var buffer = new CpuStateBuffer();
        buffer.Current.InstructionComplete = false;

        Assert.False(buffer.JumpOccurred);
    }

    #endregion
}
