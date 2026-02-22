// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Reactive.Linq;
using System.Reactive.Subjects;
using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Messages;
using Pandowdy.EmuCore.Services;
using Pandowdy.UI.Interfaces;
using Pandowdy.UI.ViewModels;

namespace Pandowdy.UI.Tests.ViewModels;

/// <summary>
/// Tests for CpuStatusPanelViewModel - displays real-time CPU status in UI.
/// Tests reactive binding to CPU state changes and property updates via refresh ticker.
/// </summary>
public class CpuStatusPanelViewModelTests
{
    #region Test Helpers

    /// <summary>
    /// Mock implementation of IEmulatorCoreInterface for testing.
    /// </summary>
    private class MockEmulatorCoreInterface : IEmulatorCoreInterface
    {
        public CpuStateSnapshot CpuState { get; set; }
        public ulong TotalCycles { get; set; }
        public IMemoryInspector MemoryInspector { get; set; } = new MockMemoryInspector();
        public bool ThrottleEnabled { get; set; }
        public IEmulatorState EmulatorState => throw new NotImplementedException();
        public IFrameProvider FrameProvider => throw new NotImplementedException();
        public ISystemStatusProvider SystemStatus => throw new NotImplementedException();
        public IDiskStatusProvider DiskStatus => throw new NotImplementedException();

        public Task RunAsync(CancellationToken token, double targetMhz = 1.023) => Task.CompletedTask;
        public Task SendCardMessageAsync(SlotNumber? slot, ICardMessage message) => Task.CompletedTask;
        public void Clock() { }
        public static void UserReset() { }
        public void SetPushButton(byte button, bool pressed) { }
        public void EnqueueKey(byte key) { }
        public void ResetKeyboard() { }
        public void DoReset() { }
        public void DoRestart() { }
        public void Restart() { }
    }

    /// <summary>
    /// Mock implementation of IMemoryInspector for testing disassembly.
    /// </summary>
    private class MockMemoryInspector : IMemoryInspector
    {
        private readonly byte[] _memory = new byte[0x10000];

        public byte ReadRawMain(int address) => _memory[address & 0xFFFF];
        public byte ReadRawAux(int address) => 0;
        public byte ReadSystemRom(int address) => 0;
        public byte ReadActiveHighMemory(int address) => _memory[address & 0xFFFF];
        public byte ReadSlotRom(int slot, int offset) => 0;
        public byte ReadSlotExtendedRom(int slot, int offset) => 0;
        public byte[] ReadMainBlock(int startAddress, int length) => new byte[length];
        public byte[] ReadAuxBlock(int startAddress, int length) => new byte[length];

        public void SetMemory(int address, byte value) => _memory[address & 0xFFFF] = value;
    }

    /// <summary>
    /// Mock implementation of IRefreshTicker for testing.
    /// </summary>
    private class MockRefreshTicker : IRefreshTicker
    {
        private readonly Subject<DateTime> _subject = new();
        public IObservable<DateTime> Stream => _subject.AsObservable();
        public void Tick() => _subject.OnNext(DateTime.Now);
        public void Start() { }
        public void Stop() { }
    }

    /// <summary>
    /// Helper fixture to create CpuStatusPanelViewModel with mocked dependencies.
    /// </summary>
    private class CpuStatusPanelFixture
    {
        public MockEmulatorCoreInterface Emulator { get; }
        public MockRefreshTicker Ticker { get; }
        public CpuStatusPanelViewModel ViewModel { get; }

        public CpuStatusPanelFixture()
        {
            Emulator = new MockEmulatorCoreInterface();
            Ticker = new MockRefreshTicker();
            ViewModel = new CpuStatusPanelViewModel(Emulator, Ticker);
        }

        public void SetCpuState(CpuStateSnapshot state)
        {
            Emulator.CpuState = state;
        }

        public void TriggerUpdate()
        {
            Ticker.Tick();
        }
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesViewModel()
    {
        // Arrange
        var emulator = new MockEmulatorCoreInterface();
        var ticker = new MockRefreshTicker();

        // Act
        var viewModel = new CpuStatusPanelViewModel(emulator, ticker);

        // Assert
        Assert.NotNull(viewModel);
        Assert.NotNull(viewModel.Activator);
    }

    [Fact]
    public void Constructor_NullEmulator_ThrowsArgumentNullException()
    {
        // Arrange
        var ticker = new MockRefreshTicker();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CpuStatusPanelViewModel(null!, ticker));
    }

    [Fact]
    public void Constructor_NullTicker_ThrowsArgumentNullException()
    {
        // Arrange
        var emulator = new MockEmulatorCoreInterface();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CpuStatusPanelViewModel(emulator, null!));
    }

    [Fact]
    public void Constructor_InitializesPropertiesWithDefaults()
    {
        // Arrange & Act
        var fixture = new CpuStatusPanelFixture();

        // Assert - Should have default/zero values before activation
        Assert.Equal(0, fixture.ViewModel.PC);
        Assert.Equal(0, fixture.ViewModel.SP);
        Assert.Equal(0, fixture.ViewModel.StackSize);
        Assert.Equal(0, fixture.ViewModel.A);
        Assert.Equal(0, fixture.ViewModel.X);
        Assert.Equal(0, fixture.ViewModel.Y);
        Assert.False(fixture.ViewModel.FlagN);
        Assert.False(fixture.ViewModel.FlagV);
        Assert.False(fixture.ViewModel.FlagB);
        Assert.False(fixture.ViewModel.FlagD);
        Assert.False(fixture.ViewModel.FlagI);
        Assert.False(fixture.ViewModel.FlagZ);
        Assert.False(fixture.ViewModel.FlagC);
        Assert.Equal(CpuExecutionStatus.Running, fixture.ViewModel.Status);
        Assert.Equal("Normal", fixture.ViewModel.StatusText);
        Assert.Equal("", fixture.ViewModel.DisassemblyText);
    }

    #endregion

    #region Register Property Update Tests

    [Fact]
    public void UpdateFromCpuState_UpdatesPC()
    {
        // Arrange
        var fixture = new CpuStatusPanelFixture();
        fixture.ViewModel.Activator.Activate();

        fixture.SetCpuState(new CpuStateSnapshot { PC = 0x1234 });

        // Act
        fixture.TriggerUpdate();

        // Assert
        Assert.Equal(0x1234, fixture.ViewModel.PC);
    }

    [Fact]
    public void UpdateFromCpuState_UpdatesSP()
    {
        // Arrange
        var fixture = new CpuStatusPanelFixture();
        fixture.ViewModel.Activator.Activate();

        fixture.SetCpuState(new CpuStateSnapshot { SP = 0xFE });

        // Act
        fixture.TriggerUpdate();

        // Assert
        Assert.Equal(0xFE, fixture.ViewModel.SP);
    }

    [Fact]
    public void UpdateFromCpuState_CalculatesStackSize()
    {
        // Arrange
        var fixture = new CpuStatusPanelFixture();
        fixture.ViewModel.Activator.Activate();

        // Act & Assert - Empty stack (SP = $FF)
        fixture.SetCpuState(new CpuStateSnapshot { SP = 0xFF });
        fixture.TriggerUpdate();
        Assert.Equal(0, fixture.ViewModel.StackSize);

        // One byte on stack (SP = $FE)
        fixture.SetCpuState(new CpuStateSnapshot { SP = 0xFE });
        fixture.TriggerUpdate();
        Assert.Equal(1, fixture.ViewModel.StackSize);

        // Full stack (SP = $00)
        fixture.SetCpuState(new CpuStateSnapshot { SP = 0x00 });
        fixture.TriggerUpdate();
        Assert.Equal(255, fixture.ViewModel.StackSize);
    }

    [Fact]
    public void UpdateFromCpuState_UpdatesAccumulator()
    {
        // Arrange
        var fixture = new CpuStatusPanelFixture();
        fixture.ViewModel.Activator.Activate();

        fixture.SetCpuState(new CpuStateSnapshot { A = 0x42 });

        // Act
        fixture.TriggerUpdate();

        // Assert
        Assert.Equal(0x42, fixture.ViewModel.A);
    }

    [Fact]
    public void UpdateFromCpuState_UpdatesXRegister()
    {
        // Arrange
        var fixture = new CpuStatusPanelFixture();
        fixture.ViewModel.Activator.Activate();

        fixture.SetCpuState(new CpuStateSnapshot { X = 0x55 });

        // Act
        fixture.TriggerUpdate();

        // Assert
        Assert.Equal(0x55, fixture.ViewModel.X);
    }

    [Fact]
    public void UpdateFromCpuState_UpdatesYRegister()
    {
        // Arrange
        var fixture = new CpuStatusPanelFixture();
        fixture.ViewModel.Activator.Activate();

        fixture.SetCpuState(new CpuStateSnapshot { Y = 0xAA });

        // Act
        fixture.TriggerUpdate();

        // Assert
        Assert.Equal(0xAA, fixture.ViewModel.Y);
    }

    #endregion

    #region Flag Property Update Tests

    [Fact]
    public void UpdateFromCpuState_UpdatesNegativeFlag()
    {
        // Arrange
        var fixture = new CpuStatusPanelFixture();
        fixture.ViewModel.Activator.Activate();

        // Act - Set N flag (bit 7 of P register)
        fixture.SetCpuState(new CpuStateSnapshot { P = 0x80 });
        fixture.TriggerUpdate();

        // Assert
        Assert.True(fixture.ViewModel.FlagN);

        // Act - Clear N flag
        fixture.SetCpuState(new CpuStateSnapshot { P = 0x00 });
        fixture.TriggerUpdate();

        // Assert
        Assert.False(fixture.ViewModel.FlagN);
    }

    [Fact]
    public void UpdateFromCpuState_UpdatesOverflowFlag()
    {
        // Arrange
        var fixture = new CpuStatusPanelFixture();
        fixture.ViewModel.Activator.Activate();

        // Act - Set V flag (bit 6)
        fixture.SetCpuState(new CpuStateSnapshot { P = 0x40 });
        fixture.TriggerUpdate();

        // Assert
        Assert.True(fixture.ViewModel.FlagV);
    }

    [Fact]
    public void UpdateFromCpuState_UpdatesBreakFlag()
    {
        // Arrange
        var fixture = new CpuStatusPanelFixture();
        fixture.ViewModel.Activator.Activate();

        // Act - Set B flag (bit 4)
        fixture.SetCpuState(new CpuStateSnapshot { P = 0x10 });
        fixture.TriggerUpdate();

        // Assert
        Assert.True(fixture.ViewModel.FlagB);
    }

    [Fact]
    public void UpdateFromCpuState_UpdatesDecimalFlag()
    {
        // Arrange
        var fixture = new CpuStatusPanelFixture();
        fixture.ViewModel.Activator.Activate();

        // Act - Set D flag (bit 3)
        fixture.SetCpuState(new CpuStateSnapshot { P = 0x08 });
        fixture.TriggerUpdate();

        // Assert
        Assert.True(fixture.ViewModel.FlagD);
    }

    [Fact]
    public void UpdateFromCpuState_UpdatesInterruptFlag()
    {
        // Arrange
        var fixture = new CpuStatusPanelFixture();
        fixture.ViewModel.Activator.Activate();

        // Act - Set I flag (bit 2)
        fixture.SetCpuState(new CpuStateSnapshot { P = 0x04 });
        fixture.TriggerUpdate();

        // Assert
        Assert.True(fixture.ViewModel.FlagI);
    }

    [Fact]
    public void UpdateFromCpuState_UpdatesZeroFlag()
    {
        // Arrange
        var fixture = new CpuStatusPanelFixture();
        fixture.ViewModel.Activator.Activate();

        // Act - Set Z flag (bit 1)
        fixture.SetCpuState(new CpuStateSnapshot { P = 0x02 });
        fixture.TriggerUpdate();

        // Assert
        Assert.True(fixture.ViewModel.FlagZ);
    }

    [Fact]
    public void UpdateFromCpuState_UpdatesCarryFlag()
    {
        // Arrange
        var fixture = new CpuStatusPanelFixture();
        fixture.ViewModel.Activator.Activate();

        // Act - Set C flag (bit 0)
        fixture.SetCpuState(new CpuStateSnapshot { P = 0x01 });
        fixture.TriggerUpdate();

        // Assert
        Assert.True(fixture.ViewModel.FlagC);
    }

    [Fact]
    public void UpdateFromCpuState_UpdatesMultipleFlagsSimultaneously()
    {
        // Arrange
        var fixture = new CpuStatusPanelFixture();
        fixture.ViewModel.Activator.Activate();

        // Act - Set N, Z, C flags (0x80 | 0x02 | 0x01 = 0x83)
        fixture.SetCpuState(new CpuStateSnapshot { P = 0x83 });
        fixture.TriggerUpdate();

        // Assert
        Assert.True(fixture.ViewModel.FlagN);
        Assert.False(fixture.ViewModel.FlagV);
        Assert.False(fixture.ViewModel.FlagB);
        Assert.False(fixture.ViewModel.FlagD);
        Assert.False(fixture.ViewModel.FlagI);
        Assert.True(fixture.ViewModel.FlagZ);
        Assert.True(fixture.ViewModel.FlagC);
    }

    #endregion

    #region Status Property Update Tests

    [Fact]
    public void UpdateFromCpuState_UpdatesStatus_Running()
    {
        // Arrange
        var fixture = new CpuStatusPanelFixture();
        fixture.ViewModel.Activator.Activate();

        // Act
        fixture.SetCpuState(new CpuStateSnapshot { Status = CpuExecutionStatus.Running });
        fixture.TriggerUpdate();

        // Assert
        Assert.Equal(CpuExecutionStatus.Running, fixture.ViewModel.Status);
        Assert.Equal("Normal", fixture.ViewModel.StatusText);
    }

    [Fact]
    public void UpdateFromCpuState_UpdatesStatus_Stopped()
    {
        // Arrange
        var fixture = new CpuStatusPanelFixture();
        fixture.ViewModel.Activator.Activate();

        // Act
        fixture.SetCpuState(new CpuStateSnapshot { Status = CpuExecutionStatus.Stopped });
        fixture.TriggerUpdate();

        // Assert
        Assert.Equal(CpuExecutionStatus.Stopped, fixture.ViewModel.Status);
        Assert.Equal("Stopped", fixture.ViewModel.StatusText);
    }

    [Fact]
    public void UpdateFromCpuState_UpdatesStatus_Jammed()
    {
        // Arrange
        var fixture = new CpuStatusPanelFixture();
        fixture.ViewModel.Activator.Activate();

        // Act
        fixture.SetCpuState(new CpuStateSnapshot { Status = CpuExecutionStatus.Jammed });
        fixture.TriggerUpdate();

        // Assert
        Assert.Equal(CpuExecutionStatus.Jammed, fixture.ViewModel.Status);
        Assert.Equal("Jammed", fixture.ViewModel.StatusText);
    }

    [Fact]
    public void UpdateFromCpuState_UpdatesStatus_Waiting()
    {
        // Arrange
        var fixture = new CpuStatusPanelFixture();
        fixture.ViewModel.Activator.Activate();

        // Act
        fixture.SetCpuState(new CpuStateSnapshot { Status = CpuExecutionStatus.Waiting });
        fixture.TriggerUpdate();

        // Assert
        Assert.Equal(CpuExecutionStatus.Waiting, fixture.ViewModel.Status);
        Assert.Equal("Waiting", fixture.ViewModel.StatusText);
    }

    [Fact]
    public void UpdateFromCpuState_UpdatesStatus_Bypassed()
    {
        // Arrange
        var fixture = new CpuStatusPanelFixture();
        fixture.ViewModel.Activator.Activate();

        // Act
        fixture.SetCpuState(new CpuStateSnapshot { Status = CpuExecutionStatus.Bypassed });
        fixture.TriggerUpdate();

        // Assert
        Assert.Equal(CpuExecutionStatus.Bypassed, fixture.ViewModel.Status);
        Assert.Equal("Bypassed", fixture.ViewModel.StatusText);
    }

    #endregion

    #region Disassembly Tests

    [Fact]
    public void UpdateFromCpuState_GeneratesDisassembly_NOP()
    {
        // Arrange
        var fixture = new CpuStatusPanelFixture();
        fixture.ViewModel.Activator.Activate();

        // Set up state with NOP instruction (opcode $EA)
        fixture.SetCpuState(new CpuStateSnapshot 
        { 
            CurrentOpcode = 0xEA,
            OpcodeAddress = 0x1000
        });

        // Act
        fixture.TriggerUpdate();

        // Assert - Should contain address and NOP
        Assert.Contains("1000", fixture.ViewModel.DisassemblyText);
        Assert.Contains("NOP", fixture.ViewModel.DisassemblyText);
    }

    [Fact]
    public void UpdateFromCpuState_GeneratesDisassembly_LDAImmediate()
    {
        // Arrange
        var fixture = new CpuStatusPanelFixture();
        fixture.ViewModel.Activator.Activate();

        // Set up memory: LDA #$42 at $2000
        var memInspector = (MockMemoryInspector)fixture.Emulator.MemoryInspector;
        memInspector.SetMemory(0x2000, 0xA9); // LDA immediate
        memInspector.SetMemory(0x2001, 0x42); // Operand

        fixture.SetCpuState(new CpuStateSnapshot 
        { 
            CurrentOpcode = 0xA9,
            OpcodeAddress = 0x2000
        });

        // Act
        fixture.TriggerUpdate();

        // Assert
        Assert.Contains("2000", fixture.ViewModel.DisassemblyText);
        Assert.Contains("LDA", fixture.ViewModel.DisassemblyText);
        Assert.Contains("#$42", fixture.ViewModel.DisassemblyText);
    }

    [Fact]
    public void UpdateFromCpuState_HandlesDisassemblyError()
    {
        // Arrange
        var fixture = new CpuStatusPanelFixture();
        fixture.ViewModel.Activator.Activate();

        // Set up invalid memory inspector that throws
        fixture.Emulator.MemoryInspector = null!;

        fixture.SetCpuState(new CpuStateSnapshot 
        { 
            CurrentOpcode = 0xEA,
            OpcodeAddress = 0x1000
        });

        // Act
        fixture.TriggerUpdate();

        // Assert - Should show error
        Assert.Equal("ERR", fixture.ViewModel.DisassemblyText);
    }

    #endregion

    #region Activation Tests

    [Fact]
    public void Activator_WhenActivated_SubscribesToTicker()
    {
        // Arrange
        var fixture = new CpuStatusPanelFixture();
        fixture.SetCpuState(new CpuStateSnapshot { PC = 0x1234 });

        // Act - Activate the view model
        fixture.ViewModel.Activator.Activate();
        fixture.TriggerUpdate();

        // Assert - Should update from ticker
        Assert.Equal(0x1234, fixture.ViewModel.PC);
    }

    [Fact]
    public void Activator_WhenDeactivated_StopsUpdating()
    {
        // Arrange
        var fixture = new CpuStatusPanelFixture();
        fixture.ViewModel.Activator.Activate();

        fixture.SetCpuState(new CpuStateSnapshot { PC = 0x1234 });
        fixture.TriggerUpdate();
        Assert.Equal(0x1234, fixture.ViewModel.PC);

        // Act - Deactivate
        fixture.ViewModel.Activator.Deactivate();

        fixture.SetCpuState(new CpuStateSnapshot { PC = 0x5678 });
        fixture.TriggerUpdate();

        // Assert - Should not update after deactivation
        Assert.Equal(0x1234, fixture.ViewModel.PC); // Still old value
    }

    #endregion

    #region PropertyChanged Tests

    [Fact]
    public void PropertyChanged_RaisedForAllProperties_WhenStateChanges()
    {
        // Arrange
        var fixture = new CpuStatusPanelFixture();
        fixture.ViewModel.Activator.Activate();

        var changedProperties = new System.Collections.Generic.HashSet<string>();
        fixture.ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
            {
                changedProperties.Add(e.PropertyName);
            }
        };

        // Act - Update all properties
        fixture.SetCpuState(new CpuStateSnapshot 
        { 
            PC = 0x1234,
            SP = 0xFE,
            A = 0x42,
            X = 0x55,
            Y = 0xAA,
            P = 0xFF, // All flags set
            Status = CpuExecutionStatus.Stopped, // Changed from default Running
            CurrentOpcode = 0xEA,
            OpcodeAddress = 0x1000
        });
        fixture.TriggerUpdate();

        // Assert - All properties should have changed
        Assert.Contains(nameof(CpuStatusPanelViewModel.PC), changedProperties);
        Assert.Contains(nameof(CpuStatusPanelViewModel.SP), changedProperties);
        Assert.Contains(nameof(CpuStatusPanelViewModel.StackSize), changedProperties);
        Assert.Contains(nameof(CpuStatusPanelViewModel.A), changedProperties);
        Assert.Contains(nameof(CpuStatusPanelViewModel.X), changedProperties);
        Assert.Contains(nameof(CpuStatusPanelViewModel.Y), changedProperties);
        Assert.Contains(nameof(CpuStatusPanelViewModel.FlagN), changedProperties);
        Assert.Contains(nameof(CpuStatusPanelViewModel.FlagV), changedProperties);
        Assert.Contains(nameof(CpuStatusPanelViewModel.FlagB), changedProperties);
        Assert.Contains(nameof(CpuStatusPanelViewModel.FlagD), changedProperties);
        Assert.Contains(nameof(CpuStatusPanelViewModel.FlagI), changedProperties);
        Assert.Contains(nameof(CpuStatusPanelViewModel.FlagZ), changedProperties);
        Assert.Contains(nameof(CpuStatusPanelViewModel.FlagC), changedProperties);
        Assert.Contains(nameof(CpuStatusPanelViewModel.Status), changedProperties);
        Assert.Contains(nameof(CpuStatusPanelViewModel.StatusText), changedProperties);
        Assert.Contains(nameof(CpuStatusPanelViewModel.DisassemblyText), changedProperties);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void Integration_CompleteStateUpdate_AllPropertiesCorrect()
    {
        // Arrange
        var fixture = new CpuStatusPanelFixture();
        fixture.ViewModel.Activator.Activate();

        var memInspector = (MockMemoryInspector)fixture.Emulator.MemoryInspector;
        memInspector.SetMemory(0x1234, 0xEA); // NOP

        // Act - Set up a complete CPU state
        fixture.SetCpuState(new CpuStateSnapshot 
        { 
            PC = 0x1234,
            SP = 0xF0, // 15 bytes on stack
            A = 0x42,
            X = 0x10,
            Y = 0x20,
            P = 0x81, // N and C flags set
            Status = CpuExecutionStatus.Running,
            CurrentOpcode = 0xEA,
            OpcodeAddress = 0x1234
        });
        fixture.TriggerUpdate();

        // Assert - All properties should be correct
        Assert.Equal(0x1234, fixture.ViewModel.PC);
        Assert.Equal(0xF0, fixture.ViewModel.SP);
        Assert.Equal(15, fixture.ViewModel.StackSize);
        Assert.Equal(0x42, fixture.ViewModel.A);
        Assert.Equal(0x10, fixture.ViewModel.X);
        Assert.Equal(0x20, fixture.ViewModel.Y);
        Assert.True(fixture.ViewModel.FlagN);
        Assert.False(fixture.ViewModel.FlagV);
        Assert.False(fixture.ViewModel.FlagZ);
        Assert.True(fixture.ViewModel.FlagC);
        Assert.Equal(CpuExecutionStatus.Running, fixture.ViewModel.Status);
        Assert.Equal("Normal", fixture.ViewModel.StatusText);
        Assert.Contains("NOP", fixture.ViewModel.DisassemblyText);
    }

    [Fact]
    public void Integration_MultipleUpdates_PropertiesTrackChanges()
    {
        // Arrange
        var fixture = new CpuStatusPanelFixture();
        fixture.ViewModel.Activator.Activate();

        // Act - Update 1
        fixture.SetCpuState(new CpuStateSnapshot { PC = 0x1000, A = 0x00, P = 0x02 }); // Z flag
        fixture.TriggerUpdate();
        Assert.Equal(0x1000, fixture.ViewModel.PC);
        Assert.Equal(0x00, fixture.ViewModel.A);
        Assert.True(fixture.ViewModel.FlagZ);

        // Act - Update 2
        fixture.SetCpuState(new CpuStateSnapshot { PC = 0x1003, A = 0xFF, P = 0x80 }); // N flag
        fixture.TriggerUpdate();
        Assert.Equal(0x1003, fixture.ViewModel.PC);
        Assert.Equal(0xFF, fixture.ViewModel.A);
        Assert.True(fixture.ViewModel.FlagN);
        Assert.False(fixture.ViewModel.FlagZ);

        // Act - Update 3
        fixture.SetCpuState(new CpuStateSnapshot { PC = 0x1006, A = 0x42, P = 0x00 }); // No flags
        fixture.TriggerUpdate();
        Assert.Equal(0x1006, fixture.ViewModel.PC);
        Assert.Equal(0x42, fixture.ViewModel.A);
        Assert.False(fixture.ViewModel.FlagN);
        Assert.False(fixture.ViewModel.FlagZ);
    }

    #endregion
}
