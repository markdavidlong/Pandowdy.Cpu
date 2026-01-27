using Xunit;

namespace Pandowdy.Cpu.Tests;

/// <summary>
/// Tests for interrupt handling: IRQ, NMI, Reset, and interrupt priorities.
/// Uses CMOS65C02 as the default variant for interrupt testing.
/// </summary>
public class InterruptTests : CpuTestBase
{
    protected override CpuVariant Variant => CpuVariant.CMOS65C02;

    #region Reset Tests

    [Fact]
    public void Reset_LoadsPCFromResetVector()
    {
        SetupCpu();
        Bus.SetResetVector(0x8000);

        Cpu.Reset(CpuBuffer, Bus);

        Assert.Equal(0x8000, CpuBuffer.Current.PC);
        Assert.Equal(0x8000, CpuBuffer.Prev.PC);
    }

    [Fact]
    public void Reset_InitializesRegistersToDefault()
    {
        SetupCpu();
        CpuBuffer.Current.A = 0xFF;
        CpuBuffer.Current.X = 0xFF;
        CpuBuffer.Current.Y = 0xFF;

        Cpu.Reset(CpuBuffer, Bus);

        Assert.Equal(0, CpuBuffer.Current.A);
        Assert.Equal(0, CpuBuffer.Current.X);
        Assert.Equal(0, CpuBuffer.Current.Y);
    }

    [Fact]
    public void Reset_SetsStackPointerTo0xFD()
    {
        SetupCpu();
        CpuBuffer.Current.SP = 0x00;

        Cpu.Reset(CpuBuffer, Bus);

        Assert.Equal(0xFD, CpuBuffer.Current.SP);
    }

    [Fact]
    public void Reset_SetsInterruptDisableFlag()
    {
        SetupCpu();
        CpuBuffer.Current.InterruptDisableFlag = false;

        Cpu.Reset(CpuBuffer, Bus);

        Assert.True(CpuBuffer.Current.InterruptDisableFlag);
    }

    [Fact]
    public void Reset_ClearsPendingInterrupts()
    {
        SetupCpu();
        CpuBuffer.Current.SignalNmi();

        Cpu.Reset(CpuBuffer, Bus);

        Assert.Equal(PendingInterrupt.None, CpuBuffer.Current.PendingInterrupt);
    }

    [Fact]
    public void Reset_SetsStatusToRunning()
    {
        SetupCpu();
        CpuBuffer.Current.Status = CpuStatus.Stopped;

        Cpu.Reset(CpuBuffer, Bus);

        Assert.Equal(CpuStatus.Running, CpuBuffer.Current.Status);
    }

    #endregion

    #region IRQ Tests

    [Fact]
    public void IRQ_IsIgnoredWhenInterruptDisabled()
    {
        SetupCpu();
        Bus.SetIrqVector(0x8000);
        Bus.LoadProgram(ProgramStart, [0xEA, 0xEA]); // NOP NOP
        CpuBuffer.Current.InterruptDisableFlag = true;
        CpuBuffer.Prev.InterruptDisableFlag = true;
        CpuBuffer.Current.SignalIrq();
        CpuBuffer.Prev.SignalIrq();

        StepInstruction();

        Assert.NotEqual(0x8000, CurrentState.PC);
    }

    [Fact]
    public void IRQ_IsHandledWhenInterruptEnabled()
    {
        SetupCpu();
        Bus.SetIrqVector(0x8000);
        Bus.LoadProgram(ProgramStart, [0xEA, 0xEA]); // NOP NOP
        Bus.Memory[0x8000] = 0xEA; // NOP at handler

        CpuBuffer.Current.InterruptDisableFlag = false;
        CpuBuffer.Prev.InterruptDisableFlag = false;

        StepInstruction();
        CpuBuffer.Current.SignalIrq();

        bool handled = CpuBuffer.Current.HandlePendingInterrupt(Bus);

        Assert.True(handled);
        Assert.Equal(0x8000, CpuBuffer.Current.PC);
    }

    [Fact]
    public void IRQ_PushesReturnAddressAndStatus()
    {
        SetupCpu();
        Bus.SetIrqVector(0x8000);
        CpuBuffer.Current.PC = 0x1234;
        CpuBuffer.Current.P = 0x00;
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Current.InterruptDisableFlag = false;
        CpuBuffer.Current.SignalIrq();

        CpuBuffer.Current.HandlePendingInterrupt(Bus);

        Assert.Equal(0xFC, CpuBuffer.Current.SP);
        Assert.Equal(0x12, Bus.Memory[0x01FF]);
        Assert.Equal(0x34, Bus.Memory[0x01FE]);
    }

    [Fact]
    public void IRQ_SetsInterruptDisableFlag()
    {
        SetupCpu();
        Bus.SetIrqVector(0x8000);
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Current.InterruptDisableFlag = false;
        CpuBuffer.Current.SignalIrq();

        CpuBuffer.Current.HandlePendingInterrupt(Bus);

        Assert.True(CpuBuffer.Current.InterruptDisableFlag);
    }

    [Fact]
    public void IRQ_PushesStatusWithBFlagClear()
    {
        SetupCpu();
        Bus.SetIrqVector(0x8000);
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Current.P = 0xFF;
        CpuBuffer.Current.InterruptDisableFlag = false;
        CpuBuffer.Current.SignalIrq();

        CpuBuffer.Current.HandlePendingInterrupt(Bus);

        byte pushedP = Bus.Memory[0x01FD];
        Assert.True((pushedP & CpuState.FlagU) != 0);
        Assert.False((pushedP & CpuState.FlagB) != 0);
    }

    [Fact]
    public void ClearIrq_ClearsPendingIrq()
    {
        SetupCpu();
        CpuBuffer.Current.SignalIrq();

        CpuBuffer.Current.ClearIrq();

        Assert.Equal(PendingInterrupt.None, CpuBuffer.Current.PendingInterrupt);
    }

    [Fact]
    public void IRQ_HandledInWaitingState_EvenIfDisabled()
    {
        SetupCpu();
        Bus.SetIrqVector(0x8000);
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Current.Status = CpuStatus.Waiting;
        CpuBuffer.Current.InterruptDisableFlag = true;
        CpuBuffer.Current.SignalIrq();

        bool handled = CpuBuffer.Current.HandlePendingInterrupt(Bus);

        Assert.True(handled);
        Assert.Equal(CpuStatus.Running, CpuBuffer.Current.Status);
    }

    #endregion

    #region NMI Tests

    [Fact]
    public void NMI_IsHandledEvenWhenInterruptDisabled()
    {
        SetupCpu();
        Bus.SetNmiVector(0x9000);
        CpuBuffer.Current.PC = 0x1234;
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Current.InterruptDisableFlag = true;
        CpuBuffer.Current.SignalNmi();

        bool handled = CpuBuffer.Current.HandlePendingInterrupt(Bus);

        Assert.True(handled);
        Assert.Equal(0x9000, CpuBuffer.Current.PC);
    }

    [Fact]
    public void NMI_PushesReturnAddressAndStatus()
    {
        SetupCpu();
        Bus.SetNmiVector(0x9000);
        CpuBuffer.Current.PC = 0x1234;
        CpuBuffer.Current.P = 0x00;
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Current.SignalNmi();

        CpuBuffer.Current.HandlePendingInterrupt(Bus);

        Assert.Equal(0xFC, CpuBuffer.Current.SP);
        Assert.Equal(0x12, Bus.Memory[0x01FF]);
        Assert.Equal(0x34, Bus.Memory[0x01FE]);
    }

    [Fact]
    public void NMI_SetsInterruptDisableFlag()
    {
        SetupCpu();
        Bus.SetNmiVector(0x9000);
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Current.InterruptDisableFlag = false;
        CpuBuffer.Current.SignalNmi();

        CpuBuffer.Current.HandlePendingInterrupt(Bus);

        Assert.True(CpuBuffer.Current.InterruptDisableFlag);
    }

    [Fact]
    public void NMI_ResumesFromWaitingState()
    {
        SetupCpu();
        Bus.SetNmiVector(0x9000);
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Current.Status = CpuStatus.Waiting;
        CpuBuffer.Current.SignalNmi();

        CpuBuffer.Current.HandlePendingInterrupt(Bus);

        Assert.Equal(CpuStatus.Running, CpuBuffer.Current.Status);
    }

    [Fact]
    public void NMI_PushesStatusWithBFlagClear()
    {
        SetupCpu();
        Bus.SetNmiVector(0x9000);
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Current.P = 0xFF;
        CpuBuffer.Current.SignalNmi();

        CpuBuffer.Current.HandlePendingInterrupt(Bus);

        byte pushedP = Bus.Memory[0x01FD];
        Assert.False((pushedP & CpuState.FlagB) != 0);
    }

    #endregion

    #region Interrupt Priority Tests

    [Fact]
    public void Reset_HasHighestPriority()
    {
        SetupCpu();
        Bus.SetResetVector(0xA000);
        Bus.SetNmiVector(0x9000);
        Bus.SetIrqVector(0x8000);

        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Current.SignalIrq();
        CpuBuffer.Current.SignalNmi();
        CpuBuffer.Current.SignalReset();

        CpuBuffer.Current.HandlePendingInterrupt(Bus);

        Assert.Equal(0xA000, CpuBuffer.Current.PC);
    }

    [Fact]
    public void NMI_HasPriorityOverIRQ()
    {
        SetupCpu();
        Bus.SetNmiVector(0x9000);
        Bus.SetIrqVector(0x8000);

        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Current.InterruptDisableFlag = false;
        CpuBuffer.Current.SignalIrq();
        CpuBuffer.Current.SignalNmi();

        CpuBuffer.Current.HandlePendingInterrupt(Bus);

        Assert.Equal(0x9000, CpuBuffer.Current.PC);
    }

    [Fact]
    public void SignalNmi_DoesNotOverrideReset()
    {
        SetupCpu();
        CpuBuffer.Current.SignalReset();
        CpuBuffer.Current.SignalNmi();

        Assert.Equal(PendingInterrupt.Reset, CpuBuffer.Current.PendingInterrupt);
    }

    [Fact]
    public void SignalIrq_DoesNotOverrideNmi()
    {
        SetupCpu();
        CpuBuffer.Current.SignalNmi();
        CpuBuffer.Current.SignalIrq();

        Assert.Equal(PendingInterrupt.Nmi, CpuBuffer.Current.PendingInterrupt);
    }

    [Fact]
    public void SignalReset_OverridesNmi()
    {
        SetupCpu();
        CpuBuffer.Current.SignalNmi();
        CpuBuffer.Current.SignalReset();

        Assert.Equal(PendingInterrupt.Reset, CpuBuffer.Current.PendingInterrupt);
    }

    [Fact]
    public void SignalNmi_OverridesIrq()
    {
        SetupCpu();
        CpuBuffer.Current.SignalIrq();
        CpuBuffer.Current.SignalNmi();

        Assert.Equal(PendingInterrupt.Nmi, CpuBuffer.Current.PendingInterrupt);
    }

    #endregion

    #region RTI Tests

    [Fact]
    public void RTI_RestoresPC()
    {
        LoadAndReset([0x40]);
        CpuBuffer.Current.SP = 0xFC;
        CpuBuffer.Prev.SP = 0xFC;
        Bus.Memory[0x01FD] = 0x00;
        Bus.Memory[0x01FE] = 0x34;
        Bus.Memory[0x01FF] = 0x12;

        StepInstruction();

        Assert.Equal(0x1234, CurrentState.PC);
    }

    [Fact]
    public void RTI_RestoresFlags()
    {
        LoadAndReset([0x40]);
        CpuBuffer.Current.SP = 0xFC;
        CpuBuffer.Prev.SP = 0xFC;
        CpuBuffer.Current.CarryFlag = false;
        CpuBuffer.Prev.CarryFlag = false;
        Bus.Memory[0x01FD] = CpuState.FlagC | CpuState.FlagZ;
        Bus.Memory[0x01FE] = 0x00;
        Bus.Memory[0x01FF] = 0x00;

        StepInstruction();

        Assert.True(CurrentState.CarryFlag);
        Assert.True(CurrentState.ZeroFlag);
    }

    [Fact]
    public void RTI_ClearsInterruptDisable()
    {
        LoadAndReset([0x40]);
        CpuBuffer.Current.SP = 0xFC;
        CpuBuffer.Prev.SP = 0xFC;
        CpuBuffer.Current.InterruptDisableFlag = true;
        CpuBuffer.Prev.InterruptDisableFlag = true;
        Bus.Memory[0x01FD] = 0x00;
        Bus.Memory[0x01FE] = 0x00;
        Bus.Memory[0x01FF] = 0x00;

        StepInstruction();

        Assert.False(CurrentState.InterruptDisableFlag);
    }

    #endregion

    #region WAI/STP Tests (65C02)

    [Fact]
    public void STP_SetsStoppedStatus()
    {
        // STP (0xDB) - 65C02 only
        LoadAndReset(0xDB);

        StepInstruction(CpuVariant.CMOS65C02);

        Assert.Equal(CpuStatus.Stopped, CurrentState.Status);
    }

    [Fact]
    public void WAI_SetsWaitingStatus()
    {
        // WAI (0xCB) - 65C02 only
        LoadAndReset(0xCB);

        StepInstruction(CpuVariant.CMOS65C02);

        Assert.Equal(CpuStatus.Waiting, CurrentState.Status);
    }

    [Fact]
    public void STP_Takes3Cycles()
    {
        LoadAndReset(0xDB);

        int cycles = StepInstruction(CpuVariant.CMOS65C02);

        Assert.Equal(3, cycles);
    }

    [Fact]
    public void WAI_Takes3Cycles()
    {
        LoadAndReset(0xCB);

        int cycles = StepInstruction(CpuVariant.CMOS65C02);

        Assert.Equal(3, cycles);
    }

    #endregion

    #region BRK vs IRQ Difference

    [Fact]
    public void BRK_PushesBreakFlagSet()
    {
        LoadAndReset(0x00, 0x00);
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Prev.SP = 0xFF;
        CpuBuffer.Current.P = 0x00;
        CpuBuffer.Prev.P = 0x00;
        Bus.SetIrqVector(0x8000);

        StepInstruction();

        byte pushedP = Bus.Memory[0x01FD];
        Assert.True((pushedP & CpuState.FlagB) != 0);
    }

    [Fact]
    public void IRQ_PusheBreakFlagClear()
    {
        SetupCpu();
        Bus.SetIrqVector(0x8000);
        CpuBuffer.Current.SP = 0xFF;
        CpuBuffer.Current.P = 0xFF;
        CpuBuffer.Current.InterruptDisableFlag = false;
        CpuBuffer.Current.SignalIrq();

        CpuBuffer.Current.HandlePendingInterrupt(Bus);

        byte pushedP = Bus.Memory[0x01FD];
        Assert.False((pushedP & CpuState.FlagB) != 0);
    }

    #endregion
}
