// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Reactive.Subjects;
using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Messages;
using Pandowdy.EmuCore.Services;
using Pandowdy.UI.Interfaces;
using Pandowdy.UI.ViewModels;

namespace Pandowdy.UI.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="EmulatorStateViewModel"/>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Test Coverage:</strong> Verifies pull-based polling architecture where the
/// ViewModel samples the refresh ticker and pulls emulator state on-demand.
/// </para>
/// <para>
/// <strong>Architecture:</strong> Tests the IActivatableViewModel lifecycle pattern,
/// Observable sampling at high-frequency rate, and UI thread marshaling.
/// </para>
/// </remarks>
public class EmulatorStateViewModelTests
{
    #region Test Fixture

    /// <summary>
    /// Mock implementation of IEmulatorCoreInterface for testing.
    /// </summary>
    private class MockEmulatorCoreInterface : IEmulatorCoreInterface
    {
        public CpuStateSnapshot CpuState { get; set; } = new CpuStateSnapshot
        {
            PC = 0x0000,
            SP = 0xFF,
            A = 0x00,
            X = 0x00,
            Y = 0x00,
            P = 0x00,
            Status = CpuExecutionStatus.Running
        };

        public ulong TotalCycles { get; set; } = 0;

        // IEmulatorCoreInterface members (not used in EmulatorStateViewModel tests)
        public bool ThrottleEnabled { get; set; }
        public IEmulatorState EmulatorState => throw new NotImplementedException();
        public IFrameProvider FrameProvider => throw new NotImplementedException();
        public ISystemStatusProvider SystemStatus => throw new NotImplementedException();
        public IDiskStatusProvider DiskStatus => throw new NotImplementedException();
        public IMemoryInspector MemoryInspector => throw new NotImplementedException();

        public System.Threading.Tasks.Task RunAsync(System.Threading.CancellationToken token, double targetMhz = 1.023)
            => System.Threading.Tasks.Task.CompletedTask;
        public System.Threading.Tasks.Task SendCardMessageAsync(SlotNumber? slot, ICardMessage message)
            => System.Threading.Tasks.Task.CompletedTask;
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
    /// Mock implementation of IRefreshTicker for testing.
    /// </summary>
    private class MockRefreshTicker : IRefreshTicker
    {
        private readonly Subject<DateTime> _tickSubject = new();

        public IObservable<DateTime> Stream => _tickSubject;

        public void Tick()
        {
            _tickSubject.OnNext(DateTime.Now);
        }

        public void Start() { }
        public void Stop() { }

        public void Dispose()
        {
            _tickSubject.Dispose();
        }
    }

    /// <summary>
    /// Test fixture helper to create EmulatorStateViewModel with mocks.
    /// </summary>
    private class TestFixture : IDisposable
    {
        public MockEmulatorCoreInterface Emulator { get; }
        public MockRefreshTicker RefreshTicker { get; }
        public EmulatorStateViewModel ViewModel { get; }

        public TestFixture()
        {
            Emulator = new MockEmulatorCoreInterface();
            RefreshTicker = new MockRefreshTicker();
            ViewModel = new EmulatorStateViewModel(Emulator, RefreshTicker);
        }

        public void Dispose()
        {
            RefreshTicker.Dispose();
        }
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_InitializesProperties()
    {
        // Arrange
        using var fixture = new TestFixture();

        // Act
        var viewModel = fixture.ViewModel;

        // Assert
        Assert.NotNull(viewModel);
        Assert.NotNull(viewModel.Activator);
        Assert.Equal(0, viewModel.PC);
        Assert.Equal(0UL, viewModel.Cycles);
    }

    [Fact]
    public void Constructor_WithNullEmulator_ThrowsArgumentNullException()
    {
        // Arrange
        var refreshTicker = new MockRefreshTicker();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new EmulatorStateViewModel(null!, refreshTicker));
    }

    [Fact]
    public void Constructor_WithNullRefreshTicker_ThrowsArgumentNullException()
    {
        // Arrange
        var emulator = new MockEmulatorCoreInterface();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new EmulatorStateViewModel(emulator, null!));
    }

    #endregion

    #region Property Tests

    [Fact]
    public void PC_InitialValue_IsZero()
    {
        // Arrange
        using var fixture = new TestFixture();

        // Act
        var result = fixture.ViewModel.PC;

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void Cycles_InitialValue_IsZero()
    {
        // Arrange
        using var fixture = new TestFixture();

        // Act
        var result = fixture.ViewModel.Cycles;

        // Assert
        Assert.Equal(0UL, result);
    }

    #endregion

    #region Activation Lifecycle Tests

    [Fact]
    public void Activation_WithRefreshTickerTick_UpdatesPC()
    {
        // Arrange
        using var fixture = new TestFixture();
        fixture.Emulator.CpuState = new CpuStateSnapshot { PC = 0xF666 };

        // Act - Activate and trigger tick
        using (fixture.ViewModel.Activator.Activate())
        {
            fixture.RefreshTicker.Tick();
            System.Threading.Thread.Sleep(50); // Allow time for observable to process
        }

        // Assert
        Assert.Equal(0xF666, fixture.ViewModel.PC);
    }

    [Fact]
    public void Activation_WithRefreshTickerTick_UpdatesCycles()
    {
        // Arrange
        using var fixture = new TestFixture();
        fixture.Emulator.TotalCycles = 1_023_000; // 1 second at 1.023 MHz

        // Act - Activate and trigger tick
        using (fixture.ViewModel.Activator.Activate())
        {
            fixture.RefreshTicker.Tick();
            System.Threading.Thread.Sleep(50); // Allow time for observable to process
        }

        // Assert
        Assert.Equal(1_023_000UL, fixture.ViewModel.Cycles);
    }

    [Fact]
    public void Activation_WithMultipleTicks_UpdatesPropertiesMultipleTimes()
    {
        // Arrange
        using var fixture = new TestFixture();

        // Act - Activate and trigger multiple ticks with changing state
        using (fixture.ViewModel.Activator.Activate())
        {
            // First tick
            fixture.Emulator.CpuState = new CpuStateSnapshot { PC = 0x0800 };
            fixture.Emulator.TotalCycles = 1000;
            fixture.RefreshTicker.Tick();
            System.Threading.Thread.Sleep(50);

            Assert.Equal(0x0800, fixture.ViewModel.PC);
            Assert.Equal(1000UL, fixture.ViewModel.Cycles);

            // Second tick
            fixture.Emulator.CpuState = new CpuStateSnapshot { PC = 0x0803 };
            fixture.Emulator.TotalCycles = 2000;
            fixture.RefreshTicker.Tick();
            System.Threading.Thread.Sleep(50);

            Assert.Equal(0x0803, fixture.ViewModel.PC);
            Assert.Equal(2000UL, fixture.ViewModel.Cycles);

            // Third tick
            fixture.Emulator.CpuState = new CpuStateSnapshot { PC = 0x0806 };
            fixture.Emulator.TotalCycles = 3000;
            fixture.RefreshTicker.Tick();
            System.Threading.Thread.Sleep(50);

            Assert.Equal(0x0806, fixture.ViewModel.PC);
            Assert.Equal(3000UL, fixture.ViewModel.Cycles);
        }
    }

    [Fact]
    public void Deactivation_StopsUpdating()
    {
        // Arrange
        using var fixture = new TestFixture();

        // Act - Activate, update, then deactivate
        var activator = fixture.ViewModel.Activator.Activate();

        fixture.Emulator.CpuState = new CpuStateSnapshot { PC = 0x1000 };
        fixture.RefreshTicker.Tick();
        System.Threading.Thread.Sleep(50);

        Assert.Equal(0x1000, fixture.ViewModel.PC);

        // Deactivate
        activator.Dispose();

        // Change state and tick again
        fixture.Emulator.CpuState = new CpuStateSnapshot { PC = 0x2000 };
        fixture.RefreshTicker.Tick();
        System.Threading.Thread.Sleep(50);

        // Assert - Should still be 0x1000 (not updated after deactivation)
        Assert.Equal(0x1000, fixture.ViewModel.PC);
    }

    [Fact]
    public void Reactivation_ResumesUpdating()
    {
        // Arrange
        using var fixture = new TestFixture();

        // Act - Activate, update, deactivate, reactivate
        var activator1 = fixture.ViewModel.Activator.Activate();

        fixture.Emulator.CpuState = new CpuStateSnapshot { PC = 0x1000 };
        fixture.RefreshTicker.Tick();
        System.Threading.Thread.Sleep(50);

        activator1.Dispose();

        // Reactivate
        var activator2 = fixture.ViewModel.Activator.Activate();

        fixture.Emulator.CpuState = new CpuStateSnapshot { PC = 0x2000 };
        fixture.RefreshTicker.Tick();
        System.Threading.Thread.Sleep(50);

        // Assert - Should update to 0x2000 after reactivation
        Assert.Equal(0x2000, fixture.ViewModel.PC);

        activator2.Dispose();
    }

    #endregion

    #region PropertyChanged Event Tests

    [Fact]
    public void PC_Update_RaisesPropertyChangedEvent()
    {
        // Arrange
        using var fixture = new TestFixture();
        var propertyChangedEvents = new List<string>();

        fixture.ViewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName != null)
            {
                propertyChangedEvents.Add(args.PropertyName);
            }
        };

        fixture.Emulator.CpuState = new CpuStateSnapshot { PC = 0xABCD };

        // Act
        using (fixture.ViewModel.Activator.Activate())
        {
            fixture.RefreshTicker.Tick();
            System.Threading.Thread.Sleep(50);
        }

        // Assert
        Assert.Contains(nameof(EmulatorStateViewModel.PC), propertyChangedEvents);
    }

    [Fact]
    public void Cycles_Update_RaisesPropertyChangedEvent()
    {
        // Arrange
        using var fixture = new TestFixture();
        var propertyChangedEvents = new List<string>();

        fixture.ViewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName != null)
            {
                propertyChangedEvents.Add(args.PropertyName);
            }
        };

        fixture.Emulator.TotalCycles = 123456;

        // Act
        using (fixture.ViewModel.Activator.Activate())
        {
            fixture.RefreshTicker.Tick();
            System.Threading.Thread.Sleep(50);
        }

        // Assert
        Assert.Contains(nameof(EmulatorStateViewModel.Cycles), propertyChangedEvents);
    }

    #endregion

    #region Polling Behavior Tests

    [Fact]
    public void Polling_WithNoActivation_DoesNotUpdate()
    {
        // Arrange
        using var fixture = new TestFixture();
        fixture.Emulator.CpuState = new CpuStateSnapshot { PC = 0x5000 };
        fixture.Emulator.TotalCycles = 5000;

        // Act - Tick without activation
        fixture.RefreshTicker.Tick();
        System.Threading.Thread.Sleep(50);

        // Assert - Should remain at initial values
        Assert.Equal(0, fixture.ViewModel.PC);
        Assert.Equal(0UL, fixture.ViewModel.Cycles);
    }

    [Fact]
    public void Polling_ReadsCurrentEmulatorState()
    {
        // Arrange
        using var fixture = new TestFixture();

        // Act - Set various PC values
        using (fixture.ViewModel.Activator.Activate())
        {
            // Test Monitor ROM address
            fixture.Emulator.CpuState = new CpuStateSnapshot { PC = 0xF666 }; // Monitor prompt
            fixture.RefreshTicker.Tick();
            System.Threading.Thread.Sleep(50);
            Assert.Equal(0xF666, fixture.ViewModel.PC);

            // Test user program space
            fixture.Emulator.CpuState = new CpuStateSnapshot { PC = 0x0800 };
            fixture.RefreshTicker.Tick();
            System.Threading.Thread.Sleep(50);
            Assert.Equal(0x0800, fixture.ViewModel.PC);

            // Test BASIC ROM
            fixture.Emulator.CpuState = new CpuStateSnapshot { PC = 0xE000 };
            fixture.RefreshTicker.Tick();
            System.Threading.Thread.Sleep(50);
            Assert.Equal(0xE000, fixture.ViewModel.PC);
        }
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void PC_WithMaxValue_HandlesCorrectly()
    {
        // Arrange
        using var fixture = new TestFixture();
        fixture.Emulator.CpuState = new CpuStateSnapshot { PC = 0xFFFF };

        // Act
        using (fixture.ViewModel.Activator.Activate())
        {
            fixture.RefreshTicker.Tick();
            System.Threading.Thread.Sleep(50);
        }

        // Assert
        Assert.Equal(0xFFFF, fixture.ViewModel.PC);
    }

    [Fact]
    public void Cycles_WithLargeValue_HandlesCorrectly()
    {
        // Arrange
        using var fixture = new TestFixture();
        // ~1 hour at 1.023 MHz = 3,682,800,000 cycles
        fixture.Emulator.TotalCycles = 3_682_800_000;

        // Act
        using (fixture.ViewModel.Activator.Activate())
        {
            fixture.RefreshTicker.Tick();
            System.Threading.Thread.Sleep(50);
        }

        // Assert
        Assert.Equal(3_682_800_000UL, fixture.ViewModel.Cycles);
    }

    [Fact]
    public void Cycles_WithMaxValue_HandlesCorrectly()
    {
        // Arrange
        using var fixture = new TestFixture();
        fixture.Emulator.TotalCycles = ulong.MaxValue;

        // Act
        using (fixture.ViewModel.Activator.Activate())
        {
            fixture.RefreshTicker.Tick();
            System.Threading.Thread.Sleep(50);
        }

        // Assert
        Assert.Equal(ulong.MaxValue, fixture.ViewModel.Cycles);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void IntegrationTest_SimulateEmulatorExecution()
    {
        // Arrange - Simulate emulator executing instructions
        using var fixture = new TestFixture();

        // Act - Activate and simulate execution
        using (fixture.ViewModel.Activator.Activate())
        {
            // Start at reset vector
            fixture.Emulator.CpuState = new CpuStateSnapshot { PC = 0xFFFC };
            fixture.Emulator.TotalCycles = 0;
            fixture.RefreshTicker.Tick();
            System.Threading.Thread.Sleep(50);

            Assert.Equal(0xFFFC, fixture.ViewModel.PC);
            Assert.Equal(0UL, fixture.ViewModel.Cycles);

            // Jump to Monitor
            fixture.Emulator.CpuState = new CpuStateSnapshot { PC = 0xF666 };
            fixture.Emulator.TotalCycles = 1000;
            fixture.RefreshTicker.Tick();
            System.Threading.Thread.Sleep(50);

            Assert.Equal(0xF666, fixture.ViewModel.PC);
            Assert.Equal(1000UL, fixture.ViewModel.Cycles);

            // Execute user program
            fixture.Emulator.CpuState = new CpuStateSnapshot { PC = 0x0800 };
            fixture.Emulator.TotalCycles = 1_023_000; // ~1 second
            fixture.RefreshTicker.Tick();
            System.Threading.Thread.Sleep(50);

            Assert.Equal(0x0800, fixture.ViewModel.PC);
            Assert.Equal(1_023_000UL, fixture.ViewModel.Cycles);

            // Program finishes
            fixture.Emulator.CpuState = new CpuStateSnapshot { PC = 0x0900 };
            fixture.Emulator.TotalCycles = 2_046_000; // ~2 seconds
            fixture.RefreshTicker.Tick();
            System.Threading.Thread.Sleep(50);

            Assert.Equal(0x0900, fixture.ViewModel.PC);
            Assert.Equal(2_046_000UL, fixture.ViewModel.Cycles);
        }
    }

    [Fact]
    public void IntegrationTest_MultipleActivationCycles()
    {
        // Arrange
        using var fixture = new TestFixture();

        // Act - Multiple activation/deactivation cycles
        // Cycle 1
        using (fixture.ViewModel.Activator.Activate())
        {
            fixture.Emulator.CpuState = new CpuStateSnapshot { PC = 0x1000 };
            fixture.Emulator.TotalCycles = 1000;
            fixture.RefreshTicker.Tick();
            System.Threading.Thread.Sleep(50);

            Assert.Equal(0x1000, fixture.ViewModel.PC);
            Assert.Equal(1000UL, fixture.ViewModel.Cycles);
        }

        // Cycle 2
        using (fixture.ViewModel.Activator.Activate())
        {
            fixture.Emulator.CpuState = new CpuStateSnapshot { PC = 0x2000 };
            fixture.Emulator.TotalCycles = 2000;
            fixture.RefreshTicker.Tick();
            System.Threading.Thread.Sleep(50);

            Assert.Equal(0x2000, fixture.ViewModel.PC);
            Assert.Equal(2000UL, fixture.ViewModel.Cycles);
        }

        // Cycle 3
        using (fixture.ViewModel.Activator.Activate())
        {
            fixture.Emulator.CpuState = new CpuStateSnapshot { PC = 0x3000 };
            fixture.Emulator.TotalCycles = 3000;
            fixture.RefreshTicker.Tick();
            System.Threading.Thread.Sleep(50);

            Assert.Equal(0x3000, fixture.ViewModel.PC);
            Assert.Equal(3000UL, fixture.ViewModel.Cycles);
        }
    }

    #endregion
}

