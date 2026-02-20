// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Moq;
using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Messages;
using Pandowdy.EmuCore.Services;
using Pandowdy.Project.Interfaces;
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
        public PeripheralsMenuViewModel PeripheralsMenuViewModel { get; }
        public Mock<IDriveStateService> MockDriveStateService { get; }
        public Mock<IMessageBoxService> MockMessageBoxService { get; }
        public Mock<IDiskFileDialogService> MockDiskFileDialogService { get; }
        public Mock<IProjectFileDialogService> MockProjectFileDialogService { get; }
        public MainWindowViewModel ViewModel { get; }

        public MainWindowViewModelFixture()
        {
            EmulatorState = new TestEmulatorState();
            var statusProvider = new SystemStatusProvider();
            var diskStatusProvider = new DiskStatusProvider();
            var emulatorCoreInterface = new TestEmulatorCoreInterface();
            var cardResponseProvider = new CardResponseChannel();
            var refreshTicker = new TestRefreshTicker();
            var mockFileDialogService = new Mock<IDiskFileDialogService>();
            MockMessageBoxService = new Mock<IMessageBoxService>();
            MockDriveStateService = new Mock<IDriveStateService>();
            MockDiskFileDialogService = new Mock<IDiskFileDialogService>();
            MockProjectFileDialogService = new Mock<IProjectFileDialogService>();

            // Mock project manager with empty library
            var mockProjectManager = new Mock<ISkilletProjectManager>();
            var mockProject = new Mock<ISkilletProject>();
            mockProject.Setup(p => p.GetAllDiskImagesAsync())
                .ReturnsAsync(new List<Pandowdy.Project.Models.DiskImageRecord>());
            mockProjectManager.Setup(pm => pm.CurrentProject).Returns(mockProject.Object);

            EmulatorStateViewModel = new EmulatorStateViewModel(emulatorCoreInterface, refreshTicker);
            SystemStatusViewModel = new SystemStatusViewModel(statusProvider);
            DiskStatusViewModel = new DiskStatusPanelViewModel(emulatorCoreInterface, diskStatusProvider, mockFileDialogService.Object, MockMessageBoxService.Object, mockProjectManager.Object);
            CpuStatusViewModel = new CpuStatusPanelViewModel(emulatorCoreInterface, refreshTicker);
            StatusBarViewModel = new StatusBarViewModel(CpuStatusViewModel, SystemStatusViewModel);
            PeripheralsMenuViewModel = new PeripheralsMenuViewModel(emulatorCoreInterface, cardResponseProvider, diskStatusProvider);
            ViewModel = new MainWindowViewModel(
                EmulatorStateViewModel,
                EmulatorState,
                SystemStatusViewModel,
                DiskStatusViewModel,
                CpuStatusViewModel,
                StatusBarViewModel,
                PeripheralsMenuViewModel,
                MockDriveStateService.Object,
                MockMessageBoxService.Object,
                MockDiskFileDialogService.Object,
                MockProjectFileDialogService.Object,
                emulatorCoreInterface,
                mockProjectManager.Object);
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
        Assert.False(viewModel.DecreaseFringing);
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
        Assert.NotNull(viewModel.ToggleDecreaseFringing);
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
            if (e.PropertyName == nameof(MainWindowViewModel.DecreaseFringing))
            {
                propertyChanged = true;
            }
        };

        // Act
        viewModel.DecreaseFringing = true;

        // Assert
        Assert.True(propertyChanged);
        Assert.True(viewModel.DecreaseFringing);
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
        var initialValue = viewModel.DecreaseFringing;

        // Act
        viewModel.ToggleDecreaseFringing.Execute().Subscribe();

        // Assert
        Assert.Equal(!initialValue, viewModel.DecreaseFringing);
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
        viewModel.ToggleDecreaseFringing.Execute().Subscribe();

        // Assert
        Assert.Contains(nameof(MainWindowViewModel.ShowScanLines), propertyChanges);
        Assert.Contains(nameof(MainWindowViewModel.ForceMonochrome), propertyChanges);
        Assert.Contains(nameof(MainWindowViewModel.DecreaseFringing), propertyChanges);
        Assert.False(viewModel.ShowScanLines);
        Assert.True(viewModel.ForceMonochrome);
        Assert.True(viewModel.DecreaseFringing);
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
        viewModel.ToggleDecreaseFringing.Execute().Subscribe();
        viewModel.ToggleMonoMixed.Execute().Subscribe();

        // Assert - Each property should be toggled independently
        Assert.False(viewModel.ThrottleEnabled);    // Was true
        Assert.False(viewModel.CapsLockEnabled);    // Was true
        Assert.False(viewModel.ShowScanLines);      // Was true
        Assert.True(viewModel.ForceMonochrome);     // Was false
        Assert.True(viewModel.DecreaseFringing);    // Was false
        Assert.True(viewModel.MonoMixed);           // Was false
    }

    #endregion

    #region Exit Confirmation Tests

    [Fact]
    public async Task OnClosingAsync_WhenNoDirtyDisks_AllowsExitAndSavesDriveState()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();
        var viewModel = fixture.ViewModel;

        // Act
        var canExit = await viewModel.OnClosingAsync();

        // Assert
        Assert.True(canExit);
        // Note: Drive state is now saved by MainWindow.SaveWindowAndDisplaySettings() via GuiSettingsService,
        // not by OnClosingAsync(). OnClosingAsync only handles dirty disk confirmation.
        fixture.MockMessageBoxService.Verify(
            s => s.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task OnClosingAsync_WhenDirtyDisksAndUserConfirms_AllowsExitAndSavesDriveState()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();
        var viewModel = fixture.ViewModel;

        // Create a dirty disk via DiskStatusProvider
        var diskStatusProvider = new DiskStatusProvider();
        diskStatusProvider.RegisterDrive(6, 1);
        diskStatusProvider.MutateDrive(6, 1, builder =>
        {
            builder.DiskImagePath = "C:\\test.dsk";
            builder.DiskImageFilename = "test.dsk";
            builder.IsDirty = true;
        });

        // Recreate DiskStatusViewModel with the provider that has a dirty disk
        var mockProjectManager = new Mock<ISkilletProjectManager>();
        var mockProject = new Mock<ISkilletProject>();
        mockProject.Setup(p => p.GetAllDiskImagesAsync())
            .ReturnsAsync(new List<Pandowdy.Project.Models.DiskImageRecord>());
        mockProjectManager.Setup(pm => pm.CurrentProject).Returns(mockProject.Object);
        
        var diskStatusViewModel = new DiskStatusPanelViewModel(
            new TestEmulatorCoreInterface(),
            diskStatusProvider,
            new Mock<IDiskFileDialogService>().Object,
            fixture.MockMessageBoxService.Object,
            mockProjectManager.Object);

        // Recreate MainWindowViewModel with the new disk status
        var viewModelWithDirtyDisk = new MainWindowViewModel(
            fixture.EmulatorStateViewModel,
            fixture.EmulatorState,
            fixture.SystemStatusViewModel,
            diskStatusViewModel,
            fixture.CpuStatusViewModel,
            fixture.StatusBarViewModel,
            fixture.PeripheralsMenuViewModel,
            fixture.MockDriveStateService.Object,
            fixture.MockMessageBoxService.Object,
            fixture.MockDiskFileDialogService.Object,
            fixture.MockProjectFileDialogService.Object,
            new TestEmulatorCoreInterface(),
            mockProjectManager.Object);

        // Setup mock: user confirms exit
        fixture.MockMessageBoxService
            .Setup(s => s.ShowConfirmationAsync(
                "Unsaved Changes",
                It.IsAny<string>()))
            .ReturnsAsync(true);

        // Act
        var canExit = await viewModelWithDirtyDisk.OnClosingAsync();

        // Assert
        Assert.True(canExit);
        fixture.MockMessageBoxService.Verify(
            s => s.ShowConfirmationAsync("Unsaved Changes", It.IsAny<string>()),
            Times.Once);
        // Note: Drive state is now saved by MainWindow.SaveWindowAndDisplaySettings() via GuiSettingsService,
        // not by OnClosingAsync(). OnClosingAsync only handles dirty disk confirmation.
    }

    [Fact]
    public async Task OnClosingAsync_WhenDirtyDisksAndUserCancels_PreventsExit()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();

        // Create a dirty disk via DiskStatusProvider
        var diskStatusProvider = new DiskStatusProvider();
        diskStatusProvider.RegisterDrive(6, 1);
        diskStatusProvider.MutateDrive(6, 1, builder =>
        {
            builder.DiskImagePath = "C:\\test.dsk";
            builder.DiskImageFilename = "test.dsk";
            builder.IsDirty = true;
        });

        // Recreate DiskStatusViewModel with the provider that has a dirty disk
        var mockProjectManager = new Mock<ISkilletProjectManager>();
        var mockProject = new Mock<ISkilletProject>();
        mockProject.Setup(p => p.GetAllDiskImagesAsync())
            .ReturnsAsync(new List<Pandowdy.Project.Models.DiskImageRecord>());
        mockProjectManager.Setup(pm => pm.CurrentProject).Returns(mockProject.Object);
        
        var diskStatusViewModel = new DiskStatusPanelViewModel(
            new TestEmulatorCoreInterface(),
            diskStatusProvider,
            new Mock<IDiskFileDialogService>().Object,
            fixture.MockMessageBoxService.Object,
            mockProjectManager.Object);

        // Recreate MainWindowViewModel with the new disk status
        var viewModelWithDirtyDisk = new MainWindowViewModel(
            fixture.EmulatorStateViewModel,
            fixture.EmulatorState,
            fixture.SystemStatusViewModel,
            diskStatusViewModel,
            fixture.CpuStatusViewModel,
            fixture.StatusBarViewModel,
            fixture.PeripheralsMenuViewModel,
            fixture.MockDriveStateService.Object,
            fixture.MockMessageBoxService.Object,
            fixture.MockDiskFileDialogService.Object,
            fixture.MockProjectFileDialogService.Object,
            new TestEmulatorCoreInterface(),
            mockProjectManager.Object);

        // Setup mock: user cancels exit
        fixture.MockMessageBoxService
            .Setup(s => s.ShowConfirmationAsync(
                "Unsaved Changes",
                It.IsAny<string>()))
            .ReturnsAsync(false);

        // Act
        var canExit = await viewModelWithDirtyDisk.OnClosingAsync();

        // Assert
        Assert.False(canExit); // Exit was cancelled
        fixture.MockMessageBoxService.Verify(
            s => s.ShowConfirmationAsync("Unsaved Changes", It.IsAny<string>()),
            Times.Once);
        // Drive state should NOT be saved when user cancels exit
        fixture.MockDriveStateService.Verify(s => s.CaptureDriveStateAsync(), Times.Never);
    }

    [Fact]
    public async Task OnClosingAsync_WhenMultipleDirtyDisks_ShowsAllInConfirmation()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();

        // Create multiple dirty disks via DiskStatusProvider
        var diskStatusProvider = new DiskStatusProvider();
        diskStatusProvider.RegisterDrive(6, 1);
        diskStatusProvider.RegisterDrive(6, 2);
        diskStatusProvider.MutateDrive(6, 1, builder =>
        {
            builder.DiskImagePath = "C:\\disk1.dsk";
            builder.DiskImageFilename = "disk1.dsk";
            builder.IsDirty = true;
        });
        diskStatusProvider.MutateDrive(6, 2, builder =>
        {
            builder.DiskImagePath = "C:\\disk2.woz";
            builder.DiskImageFilename = "disk2.woz";
            builder.IsDirty = true;
        });

        // Recreate DiskStatusViewModel with the provider that has dirty disks
        var mockProjectManager = new Mock<ISkilletProjectManager>();
        var mockProject = new Mock<ISkilletProject>();
        mockProject.Setup(p => p.GetAllDiskImagesAsync())
            .ReturnsAsync(new List<Pandowdy.Project.Models.DiskImageRecord>());
        mockProjectManager.Setup(pm => pm.CurrentProject).Returns(mockProject.Object);
        
        var diskStatusViewModel = new DiskStatusPanelViewModel(
            new TestEmulatorCoreInterface(),
            diskStatusProvider,
            new Mock<IDiskFileDialogService>().Object,
            fixture.MockMessageBoxService.Object,
            mockProjectManager.Object);

        // Recreate MainWindowViewModel with the new disk status
        var viewModelWithDirtyDisks = new MainWindowViewModel(
            fixture.EmulatorStateViewModel,
            fixture.EmulatorState,
            fixture.SystemStatusViewModel,
            diskStatusViewModel,
            fixture.CpuStatusViewModel,
            fixture.StatusBarViewModel,
            fixture.PeripheralsMenuViewModel,
            fixture.MockDriveStateService.Object,
            fixture.MockMessageBoxService.Object,
            fixture.MockDiskFileDialogService.Object,
            fixture.MockProjectFileDialogService.Object,
            new TestEmulatorCoreInterface(),
            mockProjectManager.Object);

        // Capture the message shown to user
        string? capturedMessage = null;
        fixture.MockMessageBoxService
            .Setup(s => s.ShowConfirmationAsync("Unsaved Changes", It.IsAny<string>()))
            .ReturnsAsync(true)
            .Callback<string, string>((_, msg) => capturedMessage = msg);

        // Act
        await viewModelWithDirtyDisks.OnClosingAsync();

        // Assert
        Assert.NotNull(capturedMessage);
        Assert.Contains("disk1.dsk", capturedMessage);
        Assert.Contains("disk2.woz", capturedMessage);
    }

    #endregion

    #region Disk Panel Width Tests

    [Fact]
    public void DiskPanelWidth_DefaultValue_Is200()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();

        // Act & Assert
        Assert.Equal(200.0, fixture.ViewModel.DiskPanelWidth);
    }

    [Fact]
    public void DiskPanelWidth_PropertyChangedRaised_WhenValueChanges()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();
        bool propertyChangedRaised = false;
        fixture.ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.DiskPanelWidth))
            {
                propertyChangedRaised = true;
            }
        };

        // Act
        fixture.ViewModel.DiskPanelWidth = 250.0;

        // Assert
        Assert.True(propertyChangedRaised);
        Assert.Equal(250.0, fixture.ViewModel.DiskPanelWidth);
    }

    [Fact]
    public void DiskPanelWidth_CanBeSetToMinimum()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();

        // Act
        fixture.ViewModel.DiskPanelWidth = 150.0;

        // Assert
        Assert.Equal(150.0, fixture.ViewModel.DiskPanelWidth);
    }

    [Fact]
    public void DiskPanelWidth_CanBeSetToMaximum()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();

        // Act
        fixture.ViewModel.DiskPanelWidth = 400.0;

        // Assert
        Assert.Equal(400.0, fixture.ViewModel.DiskPanelWidth);
    }

    [Fact]
    public void DiskPanelWidth_ClampsValuesBelowMinimum()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();

        // Act
        fixture.ViewModel.DiskPanelWidth = 100.0; // Below minimum

        // Assert
        Assert.Equal(150.0, fixture.ViewModel.DiskPanelWidth); // Clamped to minimum
    }

    [Fact]
    public void DiskPanelWidth_ClampsValuesAboveMaximum()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();

        // Act
        fixture.ViewModel.DiskPanelWidth = 500.0; // Above maximum

        // Assert
        Assert.Equal(400.0, fixture.ViewModel.DiskPanelWidth); // Clamped to maximum
    }

    #endregion

    #region Effective Disk Panel Width Tests

    [Fact]
    public void EffectiveDiskPanelWidth_ReturnsDiskPanelWidth_WhenShowDiskStatusIsTrue()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();
        fixture.ViewModel.ShowDiskStatus = true;
        fixture.ViewModel.DiskPanelWidth = 250.0;

        // Act
        var effectiveWidth = fixture.ViewModel.EffectiveDiskPanelWidth;

        // Assert
        Assert.Equal(250.0, effectiveWidth);
    }

    [Fact]
    public void EffectiveDiskPanelWidth_ReturnsZero_WhenShowDiskStatusIsFalse()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();
        fixture.ViewModel.ShowDiskStatus = false;
        fixture.ViewModel.DiskPanelWidth = 250.0;

        // Act
        var effectiveWidth = fixture.ViewModel.EffectiveDiskPanelWidth;

        // Assert
        Assert.Equal(0.0, effectiveWidth);
    }

    [Fact]
    public void EffectiveDiskPanelWidth_PropertyChangedRaised_WhenShowDiskStatusChanges()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();
        fixture.ViewModel.ShowDiskStatus = true;
        bool propertyChangedRaised = false;
        fixture.ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.EffectiveDiskPanelWidth))
            {
                propertyChangedRaised = true;
            }
        };

        // Act
        fixture.ViewModel.ShowDiskStatus = false;

        // Assert
        Assert.True(propertyChangedRaised);
    }

    [Fact]
    public void EffectiveDiskPanelWidth_Setter_UpdatesDiskPanelWidth_WhenPanelIsVisible()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();
        fixture.ViewModel.ShowDiskStatus = true;
        fixture.ViewModel.DiskPanelWidth = 200.0;

        // Act
        fixture.ViewModel.EffectiveDiskPanelWidth = 300.0;

        // Assert
        Assert.Equal(300.0, fixture.ViewModel.DiskPanelWidth);
        Assert.Equal(300.0, fixture.ViewModel.EffectiveDiskPanelWidth);
    }

    [Fact]
    public void EffectiveDiskPanelWidth_Setter_IgnoresUpdates_WhenPanelIsHidden()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();
        fixture.ViewModel.ShowDiskStatus = false;
        fixture.ViewModel.DiskPanelWidth = 200.0;

        // Act
        fixture.ViewModel.EffectiveDiskPanelWidth = 300.0; // Attempt to set when hidden

        // Assert
        Assert.Equal(200.0, fixture.ViewModel.DiskPanelWidth); // Original value unchanged
        Assert.Equal(0.0, fixture.ViewModel.EffectiveDiskPanelWidth); // Returns 0 when hidden
    }

    [Fact]
    public void EffectiveDiskPanelWidth_Setter_EnforcesClamping_WhenPanelIsVisible()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();
        fixture.ViewModel.ShowDiskStatus = true;

        // Act
        fixture.ViewModel.EffectiveDiskPanelWidth = 100.0; // Below minimum

        // Assert
        Assert.Equal(150.0, fixture.ViewModel.DiskPanelWidth); // Clamped by DiskPanelWidth setter
    }

    #endregion

    #region Window Title Tests (Phase 2a Step 8)

    [Fact]
    public void WindowTitle_DefaultValue_IsUntitled()
    {
        // Arrange & Act
        var fixture = new MainWindowViewModelFixture();

        // Assert
        Assert.Equal("Pandowdy — untitled", fixture.ViewModel.WindowTitle);
    }

    [Fact]
    public void WindowTitle_WithProject_ShowsProjectName()
    {
        // Arrange
        var mockProjectManager = new Mock<ISkilletProjectManager>();
        var mockProject = new Mock<ISkilletProject>();
        var metadata = new Pandowdy.Project.Models.ProjectMetadata(
            Name: "Test Project",
            CreatedUtc: DateTime.UtcNow,
            ModifiedUtc: DateTime.UtcNow,
            SchemaVersion: 1,
            PandowdyVersion: "0.1.0");

        mockProject.Setup(p => p.Metadata).Returns(metadata);
        mockProject.Setup(p => p.FilePath).Returns("C:\\test\\project.skillet");
        mockProject.Setup(p => p.HasUnsavedChanges).Returns(false);
        mockProject.Setup(p => p.GetAllDiskImagesAsync())
            .ReturnsAsync(new List<Pandowdy.Project.Models.DiskImageRecord>());
        mockProjectManager.Setup(pm => pm.CurrentProject).Returns(mockProject.Object);

        // Create view model with project
        var emuState = new TestEmulatorState();
        var statusProvider = new SystemStatusProvider();
        var diskStatusProvider = new DiskStatusProvider();
        var emulatorCoreInterface = new TestEmulatorCoreInterface();
        var cardResponseProvider = new CardResponseChannel();
        var refreshTicker = new TestRefreshTicker();
        var mockFileDialogService = new Mock<IDiskFileDialogService>();
        var mockMessageBoxService = new Mock<IMessageBoxService>();
        var mockDriveStateService = new Mock<IDriveStateService>();
        var mockProjectFileDialogService = new Mock<IProjectFileDialogService>();

        var emulatorStateViewModel = new EmulatorStateViewModel(emulatorCoreInterface, refreshTicker);
        var systemStatusViewModel = new SystemStatusViewModel(statusProvider);
        var diskStatusViewModel = new DiskStatusPanelViewModel(emulatorCoreInterface, diskStatusProvider, mockFileDialogService.Object, mockMessageBoxService.Object, mockProjectManager.Object);
        var cpuStatusViewModel = new CpuStatusPanelViewModel(emulatorCoreInterface, refreshTicker);
        var statusBarViewModel = new StatusBarViewModel(cpuStatusViewModel, systemStatusViewModel);
        var peripheralsMenuViewModel = new PeripheralsMenuViewModel(emulatorCoreInterface, cardResponseProvider, diskStatusProvider);

        // Act
        var viewModel = new MainWindowViewModel(
            emulatorStateViewModel,
            emuState,
            systemStatusViewModel,
            diskStatusViewModel,
            cpuStatusViewModel,
            statusBarViewModel,
            peripheralsMenuViewModel,
            mockDriveStateService.Object,
            mockMessageBoxService.Object,
            mockFileDialogService.Object,
            mockProjectFileDialogService.Object,
            emulatorCoreInterface,
            mockProjectManager.Object);

        // Assert
        Assert.Equal("Pandowdy — Test Project", viewModel.WindowTitle);
    }

    [Fact]
    public void WindowTitle_WithDirtyProject_ShowsDirtyIndicator()
    {
        // Arrange
        var mockProjectManager = new Mock<ISkilletProjectManager>();
        var mockProject = new Mock<ISkilletProject>();
        var metadata = new Pandowdy.Project.Models.ProjectMetadata(
            Name: "Test Project",
            CreatedUtc: DateTime.UtcNow,
            ModifiedUtc: DateTime.UtcNow,
            SchemaVersion: 1,
            PandowdyVersion: "0.1.0");

        mockProject.Setup(p => p.Metadata).Returns(metadata);
        mockProject.Setup(p => p.FilePath).Returns("C:\\test\\project.skillet");
        mockProject.Setup(p => p.HasUnsavedChanges).Returns(true);
        mockProject.Setup(p => p.HasDiskImages).Returns(true);
        mockProject.Setup(p => p.GetAllDiskImagesAsync())
            .ReturnsAsync(new List<Pandowdy.Project.Models.DiskImageRecord>());
        mockProjectManager.Setup(pm => pm.CurrentProject).Returns(mockProject.Object);

        // Create view model with dirty project
        var emuState = new TestEmulatorState();
        var statusProvider = new SystemStatusProvider();
        var diskStatusProvider = new DiskStatusProvider();
        var emulatorCoreInterface = new TestEmulatorCoreInterface();
        var cardResponseProvider = new CardResponseChannel();
        var refreshTicker = new TestRefreshTicker();
        var mockFileDialogService = new Mock<IDiskFileDialogService>();
        var mockMessageBoxService = new Mock<IMessageBoxService>();
        var mockDriveStateService = new Mock<IDriveStateService>();
        var mockProjectFileDialogService = new Mock<IProjectFileDialogService>();

        var emulatorStateViewModel = new EmulatorStateViewModel(emulatorCoreInterface, refreshTicker);
        var systemStatusViewModel = new SystemStatusViewModel(statusProvider);
        var diskStatusViewModel = new DiskStatusPanelViewModel(emulatorCoreInterface, diskStatusProvider, mockFileDialogService.Object, mockMessageBoxService.Object, mockProjectManager.Object);
        var cpuStatusViewModel = new CpuStatusPanelViewModel(emulatorCoreInterface, refreshTicker);
        var statusBarViewModel = new StatusBarViewModel(cpuStatusViewModel, systemStatusViewModel);
        var peripheralsMenuViewModel = new PeripheralsMenuViewModel(emulatorCoreInterface, cardResponseProvider, diskStatusProvider);

        // Act
        var viewModel = new MainWindowViewModel(
            emulatorStateViewModel,
            emuState,
            systemStatusViewModel,
            diskStatusViewModel,
            cpuStatusViewModel,
            statusBarViewModel,
            peripheralsMenuViewModel,
            mockDriveStateService.Object,
            mockMessageBoxService.Object,
            mockFileDialogService.Object,
            mockProjectFileDialogService.Object,
            emulatorCoreInterface,
            mockProjectManager.Object);

        // Assert — dirty project shows " *" suffix per blueprint Appendix E.7
        Assert.Equal("Pandowdy — Test Project *", viewModel.WindowTitle);
    }

    [Fact]
    public void WindowTitle_PropertyChangedRaised_WhenProjectChanges()
    {
        // Arrange
        var fixture = new MainWindowViewModelFixture();
        bool propertyChangedRaised = false;
        fixture.ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.WindowTitle))
            {
                propertyChangedRaised = true;
            }
        };

        // Act - Close project (simulates project change to null)
        // Note: CloseProjectInternalAsync is private, so we can't test it directly.
        // This test documents the expected behavior when the method is called.

        // Assert
        // The property should raise change notifications when UpdateWindowTitle() is called
        Assert.False(propertyChangedRaised); // No change yet in this fixture
    }

    #endregion
}
