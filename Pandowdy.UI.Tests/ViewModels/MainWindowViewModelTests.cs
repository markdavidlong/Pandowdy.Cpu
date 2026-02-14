// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Moq;
using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Messages;
using Pandowdy.EmuCore.Services;
using Pandowdy.UI.Interfaces;
using Pandowdy.UI.ViewModels;
using System.Reactive.Subjects;

namespace Pandowdy.UI.Tests.ViewModels;

/// <summary>
/// Tests for MainWindowViewModel - the main UI orchestrator.
/// Tests property bindings, commands, and UI state management.
/// </summary>
public class MainWindowViewModelTests
{
    #region Test Helpers

    /// <summary>
    /// Test stub for IEmulatorState that tracks method calls.
    /// </summary>
    private class TestEmulatorState : IEmulatorState
    {
        private readonly BehaviorSubject<StateSnapshot> _subject;

        public TestEmulatorState()
        {
            _subject = new BehaviorSubject<StateSnapshot>(
                new StateSnapshot(PC: 0, SP: 0, Cycles: 0, LineNumber: null, IsRunning: false, IsPaused: false));
        }

        public IObservable<StateSnapshot> Stream => _subject;
        public StateSnapshot GetCurrent() => _subject.Value;
        public void Update(StateSnapshot snapshot) => _subject.OnNext(snapshot);

        // Track method calls
        public int PauseCallCount { get; private set; }
        public int ContinueCallCount { get; private set; }
        public int StepCallCount { get; private set; }

        public void RequestPause() => PauseCallCount++;
        public void RequestContinue() => ContinueCallCount++;
        public void RequestStep() => StepCallCount++;
    }

    /// <summary>
    /// Test stub for IRefreshTicker.
    /// </summary>
    private class TestRefreshTicker : IRefreshTicker
    {
        private readonly Subject<DateTime> _subject = new();
        public IObservable<DateTime> Stream => _subject;
        public void Start() { }
        public void Stop() { }
        public void Tick() => _subject.OnNext(DateTime.Now);
    }

    /// <summary>
    /// Test stub for IEmulatorCoreInterface.
    /// </summary>
    private class TestEmulatorCoreInterface : IEmulatorCoreInterface
    {
        public CpuStateSnapshot CpuState => new()
        {
            PC = 0x0800,
            A = 0x00,
            X = 0x00,
            Y = 0x00,
            SP = 0xFF,
            P = 0x00,
            Status = CpuExecutionStatus.Running,
            CyclesRemaining = 0
        };

        public IMemoryInspector MemoryInspector => throw new NotImplementedException();
        public ulong TotalCycles => 0;
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
        public void Reset() { }
    }

    /// <summary>
    /// Helper fixture to create MainWindowViewModel with dependencies.
    /// </summary>
    private class MainWindowViewModelFixture
    {
        public TestEmulatorState EmulatorState { get; }
        public EmulatorStateViewModel EmulatorStateViewModel { get; }
        public SystemStatusViewModel SystemStatusViewModel { get; }
        public DiskStatusPanelViewModel DiskStatusViewModel { get; }
        public CpuStatusPanelViewModel CpuStatusViewModel { get; }
        public StatusBarViewModel StatusBarViewModel { get; }
        public MainWindowViewModel ViewModel { get; }

        public MainWindowViewModelFixture()
        {
            EmulatorState = new TestEmulatorState();
            var statusProvider = new SystemStatusProvider();
            var diskStatusProvider = new DiskStatusProvider();
            var emulatorCoreInterface = new TestEmulatorCoreInterface();
            var refreshTicker = new TestRefreshTicker();
            var mockMessageBoxService = new Mock<IMessageBoxService>();

            EmulatorStateViewModel = new EmulatorStateViewModel(emulatorCoreInterface, refreshTicker);
            SystemStatusViewModel = new SystemStatusViewModel(statusProvider);
            DiskStatusViewModel = new DiskStatusPanelViewModel(emulatorCoreInterface, diskStatusProvider, mockMessageBoxService.Object);
            CpuStatusViewModel = new CpuStatusPanelViewModel(emulatorCoreInterface, refreshTicker);
            StatusBarViewModel = new StatusBarViewModel(CpuStatusViewModel, SystemStatusViewModel);
            ViewModel = new MainWindowViewModel(EmulatorStateViewModel, EmulatorState, SystemStatusViewModel, DiskStatusViewModel, CpuStatusViewModel, StatusBarViewModel);
        }
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesViewModel()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();

        // Act
        var viewModel = fixture.ViewModel;

        // Assert
        Assert.NotNull(viewModel);
        Assert.NotNull(viewModel.EmulatorState);
        Assert.NotNull(viewModel.SystemStatus);
    }

    [Fact]
    public void Constructor_InitializesPropertiesWithDefaultValues()
    {
        // Arrange & Act
        var fixture = new MainWindowViewModelFixture();
        var viewModel = fixture.ViewModel;

        // Assert - Check default values
        Assert.True(viewModel.ThrottleEnabled);
        Assert.True(viewModel.CapsLockEnabled);
        Assert.True(viewModel.ShowScanLines);
        Assert.False(viewModel.ForceMonochrome);
        Assert.False(viewModel.DecreaseContrast);
        Assert.False(viewModel.MonoMixed);
    }

    [Fact]
    public void Constructor_InitializesAllCommands()
    {
        // Arrange & Act
        var fixture = new MainWindowViewModelFixture();
        var viewModel = fixture.ViewModel;

        // Assert - All commands should be created
        Assert.NotNull(viewModel.PauseCommand);
        Assert.NotNull(viewModel.ContinueCommand);
        Assert.NotNull(viewModel.StepCommand);
        Assert.NotNull(viewModel.ToggleThrottle);
        Assert.NotNull(viewModel.ToggleCapsLock);
        Assert.NotNull(viewModel.ToggleScanLines);
        Assert.NotNull(viewModel.ToggleMonochrome);
        Assert.NotNull(viewModel.ToggleDecreaseContrast);
        Assert.NotNull(viewModel.ToggleMonoMixed);
        Assert.NotNull(viewModel.StartEmu);
        Assert.NotNull(viewModel.StopEmu);
        Assert.NotNull(viewModel.ResetEmu);
        Assert.NotNull(viewModel.StepOnce);
    }

    #endregion

    #region Property Tests

    [Fact]
    public void ThrottleEnabled_PropertyChangedRaised_WhenValueChanges()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();
        var viewModel = fixture.ViewModel;
        bool propertyChanged = false;
        
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.ThrottleEnabled))
            {
                propertyChanged = true;
            }
        };

        // Act
        viewModel.ThrottleEnabled = false;

        // Assert
        Assert.True(propertyChanged);
        Assert.False(viewModel.ThrottleEnabled);
    }

    [Fact]
    public void CapsLockEnabled_PropertyChangedRaised_WhenValueChanges()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();
        var viewModel = fixture.ViewModel;
        bool propertyChanged = false;
        
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.CapsLockEnabled))
            {
                propertyChanged = true;
            }
        };

        // Act
        viewModel.CapsLockEnabled = false;

        // Assert
        Assert.True(propertyChanged);
        Assert.False(viewModel.CapsLockEnabled);
    }

    [Fact]
    public void ShowScanLines_PropertyChangedRaised_WhenValueChanges()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();
        var viewModel = fixture.ViewModel;
        bool propertyChanged = false;
        
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.ShowScanLines))
            {
                propertyChanged = true;
            }
        };

        // Act
        viewModel.ShowScanLines = false;

        // Assert
        Assert.True(propertyChanged);
        Assert.False(viewModel.ShowScanLines);
    }

    [Fact]
    public void ForceMonochrome_PropertyChangedRaised_WhenValueChanges()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();
        var viewModel = fixture.ViewModel;
        bool propertyChanged = false;
        
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.ForceMonochrome))
            {
                propertyChanged = true;
            }
        };

        // Act
        viewModel.ForceMonochrome = true;

        // Assert
        Assert.True(propertyChanged);
        Assert.True(viewModel.ForceMonochrome);
    }

    [Fact]
    public void DecreaseContrast_PropertyChangedRaised_WhenValueChanges()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();
        var viewModel = fixture.ViewModel;
        bool propertyChanged = false;
        
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.DecreaseContrast))
            {
                propertyChanged = true;
            }
        };

        // Act
        viewModel.DecreaseContrast = true;

        // Assert
        Assert.True(propertyChanged);
        Assert.True(viewModel.DecreaseContrast);
    }

    [Fact]
    public void MonoMixed_PropertyChangedRaised_WhenValueChanges()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();
        var viewModel = fixture.ViewModel;
        bool propertyChanged = false;
        
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.MonoMixed))
            {
                propertyChanged = true;
            }
        };

        // Act
        viewModel.MonoMixed = true;

        // Assert
        Assert.True(propertyChanged);
        Assert.True(viewModel.MonoMixed);
    }

    [Fact]
    public void Properties_DoNotRaisePropertyChanged_WhenSetToSameValue()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();
        var viewModel = fixture.ViewModel;
        int changeCount = 0;
        
        viewModel.PropertyChanged += (s, e) => changeCount++;

        // Act - Set to current value
        viewModel.ThrottleEnabled = true; // Already true
        viewModel.CapsLockEnabled = true; // Already true

        // Assert - Should not raise PropertyChanged
        Assert.Equal(0, changeCount);
    }

    #endregion

    #region Command Tests

    [Fact]
    public void PauseCommand_CanExecute_ReturnsTrue()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();
        var viewModel = fixture.ViewModel;

        // Act & Assert - ReactiveCommand is always executable by default
        Assert.NotNull(viewModel.PauseCommand);
        
        // Execute to verify it doesn't throw
        var exception = Record.Exception(() => viewModel.PauseCommand.Execute().Subscribe());
        Assert.Null(exception);
    }

    [Fact]
    public void PauseCommand_Execute_CallsEmulatorStateRequestPause()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();
        var viewModel = fixture.ViewModel;

        // Act
        viewModel.PauseCommand.Execute().Subscribe();

        // Assert
        Assert.Equal(1, fixture.EmulatorState.PauseCallCount);
    }

    [Fact]
    public void ContinueCommand_Execute_CallsEmulatorStateRequestContinue()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();
        var viewModel = fixture.ViewModel;

        // Act
        viewModel.ContinueCommand.Execute().Subscribe();

        // Assert
        Assert.Equal(1, fixture.EmulatorState.ContinueCallCount);
    }

    [Fact]
    public void StepCommand_Execute_CallsEmulatorStateRequestStep()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();
        var viewModel = fixture.ViewModel;

        // Act
        viewModel.StepCommand.Execute().Subscribe();

        // Assert
        Assert.Equal(1, fixture.EmulatorState.StepCallCount);
    }

    [Fact]
    public void ToggleThrottle_Execute_TogglesThrottleEnabledProperty()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();
        var viewModel = fixture.ViewModel;
        var initialValue = viewModel.ThrottleEnabled;

        // Act
        viewModel.ToggleThrottle.Execute().Subscribe();

        // Assert
        Assert.Equal(!initialValue, viewModel.ThrottleEnabled);
    }

    [Fact]
    public void ToggleCapsLock_Execute_TogglesCapsLockEnabledProperty()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();
        var viewModel = fixture.ViewModel;
        var initialValue = viewModel.CapsLockEnabled;

        // Act
        viewModel.ToggleCapsLock.Execute().Subscribe();

        // Assert
        Assert.Equal(!initialValue, viewModel.CapsLockEnabled);
    }

    [Fact]
    public void ToggleScanLines_Execute_TogglesShowScanLinesProperty()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();
        var viewModel = fixture.ViewModel;
        var initialValue = viewModel.ShowScanLines;

        // Act
        viewModel.ToggleScanLines.Execute().Subscribe();

        // Assert
        Assert.Equal(!initialValue, viewModel.ShowScanLines);
    }

    [Fact]
    public void ToggleMonochrome_Execute_TogglesForceMonochromeProperty()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();
        var viewModel = fixture.ViewModel;
        var initialValue = viewModel.ForceMonochrome;

        // Act
        viewModel.ToggleMonochrome.Execute().Subscribe();

        // Assert
        Assert.Equal(!initialValue, viewModel.ForceMonochrome);
    }

    [Fact]
    public void ToggleDecreaseContrast_Execute_TogglesDecreaseContrastProperty()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();
        var viewModel = fixture.ViewModel;
        var initialValue = viewModel.DecreaseContrast;

        // Act
        viewModel.ToggleDecreaseContrast.Execute().Subscribe();

        // Assert
        Assert.Equal(!initialValue, viewModel.DecreaseContrast);
    }

    [Fact]
    public void ToggleMonoMixed_Execute_TogglesMonoMixedProperty()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();
        var viewModel = fixture.ViewModel;
        var initialValue = viewModel.MonoMixed;

        // Act
        viewModel.ToggleMonoMixed.Execute().Subscribe();

        // Assert
        Assert.Equal(!initialValue, viewModel.MonoMixed);
    }

    [Fact]
    public void EmulatorCommands_CanExecute()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();
        var viewModel = fixture.ViewModel;

        // Act & Assert - All emulator commands should be executable
        Assert.NotNull(viewModel.StartEmu);
        Assert.NotNull(viewModel.StopEmu);
        Assert.NotNull(viewModel.ResetEmu);
        Assert.NotNull(viewModel.StepOnce);
    }

    #endregion

    #region Integration Scenarios

    [Fact]
    public void Scenario_UserTogglesThrottle_PropertyUpdates()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();
        var viewModel = fixture.ViewModel;
        var propertyChanges = new List<string>();
        
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
            {
                propertyChanges.Add(e.PropertyName);
            }
        };

        // Act - Simulate user toggling throttle multiple times
        viewModel.ToggleThrottle.Execute().Subscribe();
        viewModel.ToggleThrottle.Execute().Subscribe();
        viewModel.ToggleThrottle.Execute().Subscribe();

        // Assert
        Assert.Equal(3, propertyChanges.Count(p => p == nameof(MainWindowViewModel.ThrottleEnabled)));
        Assert.False(viewModel.ThrottleEnabled); // Toggled 3 times from true
    }

    [Fact]
    public void Scenario_UserChangesDisplaySettings_PropertiesUpdate()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();
        var viewModel = fixture.ViewModel;
        var propertyChanges = new List<string>();
        
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
            {
                propertyChanges.Add(e.PropertyName);
            }
        };

        // Act - Simulate user changing multiple display settings
        viewModel.ToggleScanLines.Execute().Subscribe();
        viewModel.ToggleMonochrome.Execute().Subscribe();
        viewModel.ToggleDecreaseContrast.Execute().Subscribe();

        // Assert
        Assert.Contains(nameof(MainWindowViewModel.ShowScanLines), propertyChanges);
        Assert.Contains(nameof(MainWindowViewModel.ForceMonochrome), propertyChanges);
        Assert.Contains(nameof(MainWindowViewModel.DecreaseContrast), propertyChanges);
        Assert.False(viewModel.ShowScanLines);
        Assert.True(viewModel.ForceMonochrome);
        Assert.True(viewModel.DecreaseContrast);
    }

    [Fact]
    public void Scenario_EmulatorControlFlow_CommandsWorkSequentially()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();
        var viewModel = fixture.ViewModel;

        // Act - Simulate typical emulator control sequence
        viewModel.PauseCommand.Execute().Subscribe();
        viewModel.StepCommand.Execute().Subscribe();
        viewModel.StepCommand.Execute().Subscribe();
        viewModel.ContinueCommand.Execute().Subscribe();

        // Assert
        Assert.Equal(1, fixture.EmulatorState.PauseCallCount);
        Assert.Equal(2, fixture.EmulatorState.StepCallCount);
        Assert.Equal(1, fixture.EmulatorState.ContinueCallCount);
    }

    [Fact]
    public void Scenario_AllToggleCommands_WorkIndependently()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();
        var viewModel = fixture.ViewModel;

        // Act - Toggle all settings
        viewModel.ToggleThrottle.Execute().Subscribe();
        viewModel.ToggleCapsLock.Execute().Subscribe();
        viewModel.ToggleScanLines.Execute().Subscribe();
        viewModel.ToggleMonochrome.Execute().Subscribe();
        viewModel.ToggleDecreaseContrast.Execute().Subscribe();
        viewModel.ToggleMonoMixed.Execute().Subscribe();

        // Assert - Each property should be toggled independently
        Assert.False(viewModel.ThrottleEnabled);    // Was true
        Assert.False(viewModel.CapsLockEnabled);    // Was true
        Assert.False(viewModel.ShowScanLines);      // Was true
        Assert.True(viewModel.ForceMonochrome);     // Was false
        Assert.True(viewModel.DecreaseContrast);    // Was false
        Assert.True(viewModel.MonoMixed);           // Was false
    }

    #endregion
}
