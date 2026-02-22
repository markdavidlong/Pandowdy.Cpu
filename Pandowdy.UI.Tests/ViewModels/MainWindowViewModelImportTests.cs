// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Moq;
using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Services;
using Pandowdy.Project.Interfaces;
using Pandowdy.UI.Interfaces;
using Pandowdy.UI.ViewModels;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Pandowdy.UI.Tests.ViewModels;

/// <summary>
/// Tests for MainWindowViewModel Import Disk Image functionality (Phase 2a Step 4).
/// </summary>
public class MainWindowViewModelImportTests
{
    #region Test Helpers

    /// <summary>
    /// Test stub for IEmulatorState.
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
        public void RequestPause() { }
        public void RequestContinue() { }
        public void RequestStep() { }
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
        public void DoReset() { }
        public void DoRestart() { }
        public void Restart() { }
    }

    /// <summary>
    /// Helper fixture to create MainWindowViewModel with dependencies for import tests.
    /// </summary>
    private class ImportTestFixture
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
        public Mock<ISkilletProject> MockProject { get; }
        public MainWindowViewModel ViewModel { get; }

        public ImportTestFixture()
        {
            EmulatorState = new TestEmulatorState();
            var statusProvider = new SystemStatusProvider();
            var diskStatusProvider = new DiskStatusProvider();
            var emulatorCoreInterface = new TestEmulatorCoreInterface();
            var cardResponseProvider = new CardResponseChannel();
            var refreshTicker = new TestRefreshTicker();
            MockDiskFileDialogService = new Mock<IDiskFileDialogService>();
            MockMessageBoxService = new Mock<IMessageBoxService>();
            MockDriveStateService = new Mock<IDriveStateService>();
            MockProjectFileDialogService = new Mock<IProjectFileDialogService>();
            MockProject = new Mock<ISkilletProject>();

            // Mock project with empty library by default
            MockProject.Setup(p => p.GetAllDiskImagesAsync())
                .ReturnsAsync(new List<Pandowdy.Project.Models.DiskImageRecord>());
            MockProject.Setup(p => p.FilePath).Returns("C:\\test\\project.skillet");

            var mockProjectManager = new Mock<ISkilletProjectManager>();
            mockProjectManager.Setup(pm => pm.CurrentProject).Returns(MockProject.Object);

            EmulatorStateViewModel = new EmulatorStateViewModel(emulatorCoreInterface, refreshTicker);
            SystemStatusViewModel = new SystemStatusViewModel(statusProvider);
            DiskStatusViewModel = new DiskStatusPanelViewModel(
                emulatorCoreInterface,
                diskStatusProvider,
                MockDiskFileDialogService.Object,
                MockMessageBoxService.Object,
                mockProjectManager.Object);
            CpuStatusViewModel = new CpuStatusPanelViewModel(emulatorCoreInterface, refreshTicker);
            StatusBarViewModel = new StatusBarViewModel(CpuStatusViewModel, SystemStatusViewModel);
            PeripheralsMenuViewModel = new PeripheralsMenuViewModel(emulatorCoreInterface, cardResponseProvider, diskStatusProvider);

            // Create ViewModel with a mocked project
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

    #region Import Command Execution Tests

    [Fact]
    public async Task ImportDiskImageAsync_WhenUserCancels_DoesNotImport()
    {
        // Arrange
        var fixture = new ImportTestFixture();
        fixture.MockDiskFileDialogService
            .Setup(s => s.ShowOpenFileDialogAsync())
            .ReturnsAsync((string?)null); // User canceled

        // Act
        await fixture.ViewModel.ImportDiskImageCommand.Execute().FirstAsync();

        // Assert
        fixture.MockProject.Verify(
            p => p.ImportDiskImageAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        fixture.MockMessageBoxService.Verify(
            s => s.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ImportDiskImageAsync_WhenFileSelected_CallsImportWithCorrectName()
    {
        // Arrange
        var fixture = new ImportTestFixture();
        var testFilePath = "C:\\disks\\loderunner.woz";
        
        fixture.MockDiskFileDialogService
            .Setup(s => s.ShowOpenFileDialogAsync())
            .ReturnsAsync(testFilePath);
        
        fixture.MockProject
            .Setup(p => p.ImportDiskImageAsync(testFilePath, "loderunner"))
            .ReturnsAsync(42L); // Return disk image ID

        // Act
        await fixture.ViewModel.ImportDiskImageCommand.Execute().FirstAsync();

        // Assert
        fixture.MockProject.Verify(
            p => p.ImportDiskImageAsync(testFilePath, "loderunner"),
            Times.Once);
    }

    [Fact]
    public async Task ImportDiskImageAsync_WhenImportSucceeds_ShowsSuccessMessage()
    {
        // Arrange
        var fixture = new ImportTestFixture();
        var testFilePath = "C:\\disks\\karateka.nib";
        var diskImageId = 123L;
        
        fixture.MockDiskFileDialogService
            .Setup(s => s.ShowOpenFileDialogAsync())
            .ReturnsAsync(testFilePath);
        
        fixture.MockProject
            .Setup(p => p.ImportDiskImageAsync(testFilePath, "karateka"))
            .ReturnsAsync(diskImageId);

        // Act
        await fixture.ViewModel.ImportDiskImageCommand.Execute().FirstAsync();

        // Assert
        fixture.MockMessageBoxService.Verify(
            s => s.ShowErrorAsync(
                "Import Successful",
                It.Is<string>(msg => msg.Contains("karateka") && msg.Contains("123"))),
            Times.Once);
    }

    [Fact]
    public async Task ImportDiskImageAsync_WhenImportFails_ShowsErrorMessage()
    {
        // Arrange
        var fixture = new ImportTestFixture();
        var testFilePath = "C:\\disks\\corrupt.dsk";
        var errorMessage = "Invalid disk image format";
        
        fixture.MockDiskFileDialogService
            .Setup(s => s.ShowOpenFileDialogAsync())
            .ReturnsAsync(testFilePath);
        
        fixture.MockProject
            .Setup(p => p.ImportDiskImageAsync(testFilePath, "corrupt"))
            .ThrowsAsync(new ArgumentException(errorMessage));

        // Act
        await fixture.ViewModel.ImportDiskImageCommand.Execute().FirstAsync();

        // Assert
        fixture.MockMessageBoxService.Verify(
            s => s.ShowErrorAsync(
                "Import Failed",
                It.Is<string>(msg => msg.Contains(errorMessage))),
            Times.Once);
    }

    [Fact]
    public async Task ImportDiskImageAsync_ExtractsFilenameWithoutExtension()
    {
        // Arrange
        var fixture = new ImportTestFixture();
        var testFilePath = "C:\\apple2\\games\\prince-of-persia.woz";
        
        fixture.MockDiskFileDialogService
            .Setup(s => s.ShowOpenFileDialogAsync())
            .ReturnsAsync(testFilePath);
        
        fixture.MockProject
            .Setup(p => p.ImportDiskImageAsync(testFilePath, "prince-of-persia"))
            .ReturnsAsync(1L);

        // Act
        await fixture.ViewModel.ImportDiskImageCommand.Execute().FirstAsync();

        // Assert
        fixture.MockProject.Verify(
            p => p.ImportDiskImageAsync(testFilePath, "prince-of-persia"),
            Times.Once);
    }

    [Fact]
    public async Task ImportDiskImageAsync_HandlesComplexFilePaths()
    {
        // Arrange
        var fixture = new ImportTestFixture();
        var testFilePath = "C:\\My Documents\\Apple II Software\\DOS 3.3 System Master.dsk";
        
        fixture.MockDiskFileDialogService
            .Setup(s => s.ShowOpenFileDialogAsync())
            .ReturnsAsync(testFilePath);
        
        fixture.MockProject
            .Setup(p => p.ImportDiskImageAsync(testFilePath, "DOS 3.3 System Master"))
            .ReturnsAsync(99L);

        // Act
        await fixture.ViewModel.ImportDiskImageCommand.Execute().FirstAsync();

        // Assert
        fixture.MockProject.Verify(
            p => p.ImportDiskImageAsync(testFilePath, "DOS 3.3 System Master"),
            Times.Once);
    }

    [Fact]
    public void ImportDiskImageCommand_IsCreatedSuccessfully()
    {
        // Arrange
        var fixture = new ImportTestFixture();

        // Act & Assert - Command should be created
        Assert.NotNull(fixture.ViewModel.ImportDiskImageCommand);
    }

    #endregion
}
