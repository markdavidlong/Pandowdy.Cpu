// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Moq;
using Pandowdy.EmuCore.DiskII.Messages;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Services;
using Pandowdy.Project.Interfaces;
using Pandowdy.UI.Interfaces;
using Pandowdy.UI.ViewModels;
using Xunit;

namespace Pandowdy.UI.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="DiskCardPanelViewModel"/>.
/// </summary>
public class DiskCardPanelViewModelTests
{
    private readonly Mock<IEmulatorCoreInterface> _mockEmulator;
    private readonly Mock<IDiskFileDialogService> _mockFileDialogService;
    private readonly Mock<IMessageBoxService> _mockMessageBoxService;
    private readonly Mock<ISkilletProjectManager> _mockProjectManager;

    public DiskCardPanelViewModelTests()
    {
        _mockEmulator = new Mock<IEmulatorCoreInterface>();
        _mockFileDialogService = new Mock<IDiskFileDialogService>();
        _mockMessageBoxService = new Mock<IMessageBoxService>();
        _mockProjectManager = new Mock<ISkilletProjectManager>();

        // Setup default project manager behavior (empty library)
        var mockProject = new Mock<ISkilletProject>();
        mockProject.Setup(p => p.GetAllDiskImagesAsync())
            .ReturnsAsync(new List<Pandowdy.Project.Models.DiskImageRecord>());
        _mockProjectManager.Setup(pm => pm.CurrentProject).Returns(mockProject.Object);
    }

    private DiskStatusWidgetViewModel CreateMockDriveViewModel(
        IEmulatorCoreInterface emulator,
        int slotNumber,
        int driveNumber,
        bool hasDisk = false)
    {
        var snapshot = new DiskDriveStatusSnapshot(
            SlotNumber: slotNumber,
            DriveNumber: driveNumber,
            DiskImagePath: hasDisk ? $"E:\\test{driveNumber}.woz" : "",
            DiskImageFilename: hasDisk ? $"test{driveNumber}.woz" : "",
            IsReadOnly: false,
            Track: 17.0,
            Sector: 0,
            MotorOn: false,
            MotorOffScheduled: false,
            PhaseState: 0,
            HasValidTrackData: true,
            IsDirty: false,
            HasDestinationPath: hasDisk
        );
        return new DiskStatusWidgetViewModel(emulator, _mockFileDialogService.Object, _mockMessageBoxService.Object, _mockProjectManager.Object, snapshot);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_InitializesWithProvidedParameters()
    {
        // Arrange
        var drives = new ObservableCollection<DiskStatusWidgetViewModel>
        {
            CreateMockDriveViewModel(_mockEmulator.Object, 6, 1),
            CreateMockDriveViewModel(_mockEmulator.Object, 6, 2)
        };

        // Act
        var vm = new DiskCardPanelViewModel(_mockEmulator.Object, _mockMessageBoxService.Object, SlotNumber.Slot6,
            "Disk II Controller",
            drives);

        // Assert
        Assert.Equal(SlotNumber.Slot6, vm.Slot);
        Assert.Equal("Disk II Controller", vm.CardName);
        Assert.Equal("Disk II Controller — Slot 6", vm.CardTitle);
        Assert.Equal(2, vm.Drives.Count);
        Assert.NotNull(vm.SwapDrivesCommand);
    }

    [Fact]
    public void CardTitle_FormatsSlotNumberCorrectly()
    {
        // Arrange
        var drives = new ObservableCollection<DiskStatusWidgetViewModel>
        {
            CreateMockDriveViewModel(_mockEmulator.Object, 5, 1)
        };

        // Act
        var vm = new DiskCardPanelViewModel(_mockEmulator.Object, _mockMessageBoxService.Object, SlotNumber.Slot5,
            "Test Card",
            drives);

        // Assert
        Assert.Equal("Test Card — Slot 5", vm.CardTitle);
    }

    #endregion

    #region SwapDrivesCommand Tests

    [Fact]
    public async Task SwapDrivesCommand_DisabledWithSingleDrive()
    {
        // Arrange
        var drives = new ObservableCollection<DiskStatusWidgetViewModel>
        {
            CreateMockDriveViewModel(_mockEmulator.Object, 6, 1, hasDisk: true)
        };

        var vm = new DiskCardPanelViewModel(_mockEmulator.Object, _mockMessageBoxService.Object, SlotNumber.Slot6,
            "Disk II Controller",
            drives);

        // Act
        var canExecute = await vm.SwapDrivesCommand.CanExecute.FirstAsync();

        // Assert
        Assert.False(canExecute);
    }

    [Fact]
    public async Task SwapDrivesCommand_DisabledWithTwoEmptyDrives()
    {
        // Arrange
        var drives = new ObservableCollection<DiskStatusWidgetViewModel>
        {
            CreateMockDriveViewModel(_mockEmulator.Object, 6, 1, hasDisk: false),
            CreateMockDriveViewModel(_mockEmulator.Object, 6, 2, hasDisk: false)
        };

        var vm = new DiskCardPanelViewModel(_mockEmulator.Object, _mockMessageBoxService.Object, SlotNumber.Slot6,
            "Disk II Controller",
            drives);

        // Act
        var canExecute = await vm.SwapDrivesCommand.CanExecute.FirstAsync();

        // Assert
        Assert.False(canExecute);
    }

    [Fact]
    public async Task SwapDrivesCommand_EnabledWithTwoDrivesOneHasDisk()
    {
        // Arrange
        var drives = new ObservableCollection<DiskStatusWidgetViewModel>
        {
            CreateMockDriveViewModel(_mockEmulator.Object, 6, 1, hasDisk: true),
            CreateMockDriveViewModel(_mockEmulator.Object, 6, 2, hasDisk: false)
        };

        var vm = new DiskCardPanelViewModel(_mockEmulator.Object, _mockMessageBoxService.Object, SlotNumber.Slot6,
            "Disk II Controller",
            drives);

        // Act
        var canExecute = await vm.SwapDrivesCommand.CanExecute.FirstAsync();

        // Assert
        Assert.True(canExecute);
    }

    [Fact]
    public async Task SwapDrivesCommand_EnabledWithTwoDrivesBothHaveDisk()
    {
        // Arrange
        var drives = new ObservableCollection<DiskStatusWidgetViewModel>
        {
            CreateMockDriveViewModel(_mockEmulator.Object, 6, 1, hasDisk: true),
            CreateMockDriveViewModel(_mockEmulator.Object, 6, 2, hasDisk: true)
        };

        var vm = new DiskCardPanelViewModel(_mockEmulator.Object, _mockMessageBoxService.Object, SlotNumber.Slot6,
            "Disk II Controller",
            drives);

        // Act
        var canExecute = await vm.SwapDrivesCommand.CanExecute.FirstAsync();

        // Assert
        Assert.True(canExecute);
    }

    [Fact]
    public async Task SwapDrivesCommand_SendsSwapDrivesMessageToCorrectSlot()
    {
        // Arrange
        var drives = new ObservableCollection<DiskStatusWidgetViewModel>
        {
            CreateMockDriveViewModel(_mockEmulator.Object, 6, 1, hasDisk: true),
            CreateMockDriveViewModel(_mockEmulator.Object, 6, 2, hasDisk: false)
        };

        var vm = new DiskCardPanelViewModel(_mockEmulator.Object, _mockMessageBoxService.Object, SlotNumber.Slot6,
            "Disk II Controller",
            drives);

        _mockEmulator
            .Setup(e => e.SendCardMessageAsync(SlotNumber.Slot6, It.IsAny<SwapDrivesMessage>()))
            .Returns(Task.CompletedTask);

        // Act
        await vm.SwapDrivesCommand.Execute().FirstAsync();

        // Assert
        _mockEmulator.Verify(
            e => e.SendCardMessageAsync(SlotNumber.Slot6, It.IsAny<SwapDrivesMessage>()),
            Times.Once);
    }

    #endregion
}
