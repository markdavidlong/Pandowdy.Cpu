// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Avalonia.Media;
using Moq;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Services;
using Pandowdy.Project.Interfaces;
using Pandowdy.UI.Interfaces;
using Pandowdy.UI.ViewModels;

namespace Pandowdy.UI.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="DiskStatusWidgetViewModel"/>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Test Coverage:</strong> Verifies all properties derived from
/// <see cref="DiskDriveStatusSnapshot"/>, including display text formatting,
/// color coding, snapshot update behavior, and command enablement.
/// </para>
/// </remarks>
public class DiskStatusWidgetViewModelTests
{
    private readonly Mock<IEmulatorCoreInterface> _mockEmulator;
    private readonly Mock<IDiskFileDialogService> _mockFileDialogService;
    private readonly Mock<IMessageBoxService> _mockMessageBoxService;
    private readonly Mock<ISkilletProjectManager> _mockProjectManager;

    public DiskStatusWidgetViewModelTests()
    {
        _mockEmulator = new Mock<IEmulatorCoreInterface>();
        _mockFileDialogService = new Mock<IDiskFileDialogService>();
        _mockMessageBoxService = new Mock<IMessageBoxService>();
        _mockProjectManager = new Mock<ISkilletProjectManager>();

        // Setup default project manager behavior (empty library by default)
        var mockProject = new Mock<ISkilletProject>();
        mockProject.Setup(p => p.GetAllDiskImagesAsync())
            .ReturnsAsync(new List<Pandowdy.Project.Models.DiskImageRecord>());
        _mockProjectManager.Setup(pm => pm.CurrentProject).Returns(mockProject.Object);
    }

    #region Test Fixture

    /// <summary>
    /// Helper to create a DiskDriveStatusSnapshot with specified parameters.
    /// </summary>
    private static DiskDriveStatusSnapshot CreateSnapshot(
        int slotNumber = 6,
        int driveNumber = 1,
        string diskImagePath = "",
        string diskImageFilename = "",
        bool isReadOnly = false,
        double track = 0.0,
        int sector = -1,
        bool motorOn = false,
        bool motorOffScheduled = false,
        byte phaseState = 0b0000,
        bool hasValidTrackData = false,
        bool isDirty = false,
        bool hasDestinationPath = false)
    {
        return new DiskDriveStatusSnapshot(
            slotNumber,
            driveNumber,
            diskImagePath,
            diskImageFilename,
            isReadOnly,
            track,
            sector,
            motorOn,
            motorOffScheduled,
            phaseState,
            hasValidTrackData,
            isDirty,
            hasDestinationPath);
    }

    #endregion

    #region Constructor and Initialization Tests

    [Fact]
    public void Constructor_WithEmptySnapshot_InitializesProperties()
    {
        // Arrange
        var snapshot = CreateSnapshot(slotNumber: 6, driveNumber: 1);

        // Act
        var viewModel = new DiskStatusWidgetViewModel(_mockEmulator.Object, _mockFileDialogService.Object, _mockMessageBoxService.Object, _mockProjectManager.Object, snapshot);

        // Assert
        Assert.Equal("S6D1", viewModel.DiskId);
        Assert.False(viewModel.HasDisk);
        Assert.False(viewModel.IsDirty);
        Assert.False(viewModel.HasDestinationPath);
        Assert.Equal("(empty)", viewModel.Filename);
        Assert.Equal("No disk inserted", viewModel.DiskImagePathTooltip);
        Assert.Equal(Brushes.White, viewModel.FilenameForeground);
        Assert.Equal("T-- S--", viewModel.TrackSectorText);
        Assert.Equal(Brushes.White, viewModel.TrackSectorForeground);
        Assert.Equal("ϕ:----", viewModel.PhaseText);
        Assert.Equal("", viewModel.MotorText);
    }

    [Fact]
    public void Constructor_WithDiskInserted_InitializesProperties()
    {
        // Arrange
        var snapshot = CreateSnapshot(
            slotNumber: 6,
            driveNumber: 2,
            diskImagePath: @"C:\Disks\test.dsk",
            diskImageFilename: "test.dsk",
            isReadOnly: false,
            track: 17.5,
            sector: 10,
            motorOn: true,
            motorOffScheduled: false,
            phaseState: 0b0101, // bit pattern: p0=1, p1=0, p2=1, p3=0
            isDirty: true,
            hasDestinationPath: true);

        // Act
        var viewModel = new DiskStatusWidgetViewModel(_mockEmulator.Object, _mockFileDialogService.Object, _mockMessageBoxService.Object, _mockProjectManager.Object, snapshot);

        // Assert
        Assert.Equal("S6D2", viewModel.DiskId);
        Assert.True(viewModel.HasDisk);
        Assert.True(viewModel.IsDirty);
        Assert.True(viewModel.HasDestinationPath);
        Assert.Equal("test.dsk", viewModel.Filename);
        Assert.Equal(@"C:\Disks\test.dsk", viewModel.DiskImagePathTooltip);
        Assert.Equal(Brushes.White, viewModel.FilenameForeground);
        Assert.Equal("T:17.50 S:10", viewModel.TrackSectorText);
        Assert.Equal(Brushes.White, viewModel.TrackSectorForeground);
        Assert.Equal("ϕ:+-+-", viewModel.PhaseText); // p0=+, p1=-, p2=+, p3=-
        Assert.Equal("⚡", viewModel.MotorText);
    }

    [Fact]
    public void Constructor_WithReadOnlyDisk_UsesRedForeground()
    {
        // Arrange
        var snapshot = CreateSnapshot(
            diskImagePath: @"C:\Disks\readonly.dsk",
            diskImageFilename: "readonly.dsk",
            isReadOnly: true);

        // Act
        var viewModel = new DiskStatusWidgetViewModel(_mockEmulator.Object, _mockFileDialogService.Object, _mockMessageBoxService.Object, _mockProjectManager.Object, snapshot);

        // Assert
        Assert.Equal(Brushes.Red, viewModel.FilenameForeground);
        Assert.Equal(Brushes.Red, viewModel.TrackSectorForeground);
    }

    [Fact]
    public void Constructor_WithMotorOffScheduled_ShowsClockIcon()
    {
        // Arrange
        var snapshot = CreateSnapshot(motorOffScheduled: true, motorOn: false);

        // Act
        var viewModel = new DiskStatusWidgetViewModel(_mockEmulator.Object, _mockFileDialogService.Object, _mockMessageBoxService.Object, _mockProjectManager.Object, snapshot);

        // Assert
        Assert.Equal("⌚", viewModel.MotorText);
    }

    [Fact]
    public void Constructor_WithBothMotorStates_ShowsBothIcons()
    {
        // Arrange
        var snapshot = CreateSnapshot(motorOn: true, motorOffScheduled: true);

        // Act
        var viewModel = new DiskStatusWidgetViewModel(_mockEmulator.Object, _mockFileDialogService.Object, _mockMessageBoxService.Object, _mockProjectManager.Object, snapshot);

        // Assert
        Assert.Equal("⌚⚡", viewModel.MotorText);
    }

    [Fact]
    public void Constructor_InitializesCommands()
    {
        // Arrange
        var snapshot = CreateSnapshot();

        // Act
        var viewModel = new DiskStatusWidgetViewModel(_mockEmulator.Object, _mockFileDialogService.Object, _mockMessageBoxService.Object, _mockProjectManager.Object, snapshot);

        // Assert
        Assert.NotNull(viewModel.InsertDiskCommand);
        Assert.NotNull(viewModel.InsertBlankDiskCommand);
        Assert.NotNull(viewModel.EjectDiskCommand);
        Assert.NotNull(viewModel.ExportDiskCommand);
        Assert.NotNull(viewModel.ToggleWriteProtectCommand);
    }

    #endregion

    #region DiskId Property Tests

    [Theory]
    [InlineData(1, 1, "S1D1")]
    [InlineData(6, 1, "S6D1")]
    [InlineData(6, 2, "S6D2")]
    [InlineData(7, 2, "S7D2")]
    public void DiskId_ReturnsCorrectFormat(int slot, int drive, string expected)
    {
        // Arrange
        var snapshot = CreateSnapshot(slotNumber: slot, driveNumber: drive);
        var viewModel = new DiskStatusWidgetViewModel(_mockEmulator.Object, _mockFileDialogService.Object, _mockMessageBoxService.Object, _mockProjectManager.Object, snapshot);

        // Act
        var result = viewModel.DiskId;

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region HasDisk Property Tests

    [Fact]
    public void HasDisk_WithEmptyFilename_ReturnsFalse()
    {
        // Arrange
        var snapshot = CreateSnapshot(diskImageFilename: "");
        var viewModel = new DiskStatusWidgetViewModel(_mockEmulator.Object, _mockFileDialogService.Object, _mockMessageBoxService.Object, _mockProjectManager.Object, snapshot);

        // Act & Assert
        Assert.False(viewModel.HasDisk);
    }

    [Fact]
    public void HasDisk_WithFilename_ReturnsTrue()
    {
        // Arrange
        var snapshot = CreateSnapshot(diskImageFilename: "test.dsk");
        var viewModel = new DiskStatusWidgetViewModel(_mockEmulator.Object, _mockFileDialogService.Object, _mockMessageBoxService.Object, _mockProjectManager.Object, snapshot);

        // Act & Assert
        Assert.True(viewModel.HasDisk);
    }

    #endregion

    #region Filename Property Tests

    [Fact]
    public void Filename_WithNoDisk_ReturnsEmpty()
    {
        // Arrange
        var snapshot = CreateSnapshot(diskImageFilename: "");
        var viewModel = new DiskStatusWidgetViewModel(_mockEmulator.Object, _mockFileDialogService.Object, _mockMessageBoxService.Object, _mockProjectManager.Object, snapshot);

        // Act
        var result = viewModel.Filename;

        // Assert
        Assert.Equal("(empty)", result);
    }

    [Fact]
    public void Filename_WithDisk_ReturnsFilename()
    {
        // Arrange
        var snapshot = CreateSnapshot(diskImageFilename: "myDisk.dsk");
        var viewModel = new DiskStatusWidgetViewModel(_mockEmulator.Object, _mockFileDialogService.Object, _mockMessageBoxService.Object, _mockProjectManager.Object, snapshot);

        // Act
        var result = viewModel.Filename;

        // Assert
        Assert.Equal("myDisk.dsk", result);
    }

    [Theory]
    [InlineData("game.dsk")]
    [InlineData("prodos.po")]
    [InlineData("disk.nib")]
    [InlineData("image.woz")]
    public void Filename_WithVariousExtensions_ReturnsExactFilename(string filename)
    {
        // Arrange
        var snapshot = CreateSnapshot(diskImageFilename: filename);
        var viewModel = new DiskStatusWidgetViewModel(_mockEmulator.Object, _mockFileDialogService.Object, _mockMessageBoxService.Object, _mockProjectManager.Object, snapshot);

        // Act
        var result = viewModel.Filename;

        // Assert
        Assert.Equal(filename, result);
    }

    #endregion

    #region DiskImagePathTooltip Property Tests

    [Fact]
    public void DiskImagePathTooltip_WithNoDisk_ReturnsMessage()
    {
        // Arrange
        var snapshot = CreateSnapshot(diskImagePath: "");
        var viewModel = new DiskStatusWidgetViewModel(_mockEmulator.Object, _mockFileDialogService.Object, _mockMessageBoxService.Object, _mockProjectManager.Object, snapshot);

        // Act
        var result = viewModel.DiskImagePathTooltip;

        // Assert
        Assert.Equal("No disk inserted", result);
    }

    [Fact]
    public void DiskImagePathTooltip_WithDisk_ReturnsFullPath()
    {
        // Arrange
        var snapshot = CreateSnapshot(diskImagePath: @"C:\Apple II\Disks\game.dsk");
        var viewModel = new DiskStatusWidgetViewModel(_mockEmulator.Object, _mockFileDialogService.Object, _mockMessageBoxService.Object, _mockProjectManager.Object, snapshot);

        // Act
        var result = viewModel.DiskImagePathTooltip;

        // Assert
        Assert.Equal(@"C:\Apple II\Disks\game.dsk", result);
    }

    #endregion

    #region FilenameForeground Property Tests

    [Fact]
    public void FilenameForeground_WithWritableDisk_ReturnsWhite()
    {
        // Arrange
        var snapshot = CreateSnapshot(isReadOnly: false);
        var viewModel = new DiskStatusWidgetViewModel(_mockEmulator.Object, _mockFileDialogService.Object, _mockMessageBoxService.Object, _mockProjectManager.Object, snapshot);

        // Act
        var result = viewModel.FilenameForeground;

        // Assert
        Assert.Equal(Brushes.White, result);
    }

    [Fact]
    public void FilenameForeground_WithReadOnlyDisk_ReturnsRed()
    {
        // Arrange
        var snapshot = CreateSnapshot(isReadOnly: true);
        var viewModel = new DiskStatusWidgetViewModel(_mockEmulator.Object, _mockFileDialogService.Object, _mockMessageBoxService.Object, _mockProjectManager.Object, snapshot);

        // Act
        var result = viewModel.FilenameForeground;

        // Assert
        Assert.Equal(Brushes.Red, result);
    }

    #endregion

    #region TrackSectorText Property Tests

    [Fact]
    public void TrackSectorText_WithNoDisk_ReturnsPlaceholder()
    {
        // Arrange
        var snapshot = CreateSnapshot(diskImageFilename: "");
        var viewModel = new DiskStatusWidgetViewModel(_mockEmulator.Object, _mockFileDialogService.Object, _mockMessageBoxService.Object, _mockProjectManager.Object, snapshot);

        // Act
        var result = viewModel.TrackSectorText;

        // Assert
        Assert.Equal("T-- S--", result);
    }

    [Theory]
    [InlineData(0.0, 0, "T:0.00 S:00")]
    [InlineData(17.0, 10, "T:17.00 S:10")]
    [InlineData(34.75, 15, "T:34.75 S:15")]
    [InlineData(10.25, 5, "T:10.25 S:05")]
    public void TrackSectorText_WithValidTrackAndSector_ReturnsFormattedString(
        double track, int sector, string expected)
    {
        // Arrange
        var snapshot = CreateSnapshot(
            diskImageFilename: "test.dsk",
            track: track,
            sector: sector);
        var viewModel = new DiskStatusWidgetViewModel(_mockEmulator.Object, _mockFileDialogService.Object, _mockMessageBoxService.Object, _mockProjectManager.Object, snapshot);

        // Act
        var result = viewModel.TrackSectorText;

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TrackSectorText_WithNegativeSector_ShowsPlaceholder()
    {
        // Arrange
        var snapshot = CreateSnapshot(
            diskImageFilename: "test.dsk",
            track: 17.5,
            sector: -1);
        var viewModel = new DiskStatusWidgetViewModel(_mockEmulator.Object, _mockFileDialogService.Object, _mockMessageBoxService.Object, _mockProjectManager.Object, snapshot);

        // Act
        var result = viewModel.TrackSectorText;

        // Assert
        Assert.Equal("T:17.50 S:--", result);
    }

    #endregion

    #region TrackSectorForeground Property Tests

    [Fact]
    public void TrackSectorForeground_WithWritableDisk_ReturnsWhite()
    {
        // Arrange
        var snapshot = CreateSnapshot(isReadOnly: false);
        var viewModel = new DiskStatusWidgetViewModel(_mockEmulator.Object, _mockFileDialogService.Object, _mockMessageBoxService.Object, _mockProjectManager.Object, snapshot);

        // Act
        var result = viewModel.TrackSectorForeground;

        // Assert
        Assert.Equal(Brushes.White, result);
    }

    [Fact]
    public void TrackSectorForeground_WithReadOnlyDisk_ReturnsRed()
    {
        // Arrange
        var snapshot = CreateSnapshot(isReadOnly: true);
        var viewModel = new DiskStatusWidgetViewModel(_mockEmulator.Object, _mockFileDialogService.Object, _mockMessageBoxService.Object, _mockProjectManager.Object, snapshot);

        // Act
        var result = viewModel.TrackSectorForeground;

        // Assert
        Assert.Equal(Brushes.Red, result);
    }

    #endregion

    #region PhaseText Property Tests

    [Theory]
    [InlineData(0b0000, "ϕ:----")] // All phases off
    [InlineData(0b0001, "ϕ:+---")] // Phase 0 on
    [InlineData(0b0010, "ϕ:-+--")] // Phase 1 on
    [InlineData(0b0100, "ϕ:--+-")] // Phase 2 on
    [InlineData(0b1000, "ϕ:---+")] // Phase 3 on
    [InlineData(0b1111, "ϕ:++++")] // All phases on
    [InlineData(0b0101, "ϕ:+-+-")] // Phases 0 and 2 on
    [InlineData(0b1010, "ϕ:-+-+")] // Phases 1 and 3 on
    [InlineData(0b0011, "ϕ:++--")] // Phases 0 and 1 on
    [InlineData(0b1100, "ϕ:--++")] // Phases 2 and 3 on
    public void PhaseText_WithVariousPhaseStates_ReturnsCorrectPattern(
        byte phaseState, string expected)
    {
        // Arrange
        var snapshot = CreateSnapshot(phaseState: phaseState);
        var viewModel = new DiskStatusWidgetViewModel(_mockEmulator.Object, _mockFileDialogService.Object, _mockMessageBoxService.Object, _mockProjectManager.Object, snapshot);

        // Act
        var result = viewModel.PhaseText;

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void PhaseText_IgnoresHighNibble()
    {
        // Arrange - high nibble should be ignored
        var snapshot = CreateSnapshot(phaseState: 0b11110101);
        var viewModel = new DiskStatusWidgetViewModel(_mockEmulator.Object, _mockFileDialogService.Object, _mockMessageBoxService.Object, _mockProjectManager.Object, snapshot);

        // Act
        var result = viewModel.PhaseText;

        // Assert
        Assert.Equal("ϕ:+-+-", result); // Only low nibble (0101) matters: p0=1, p1=0, p2=1, p3=0
    }

    #endregion

    #region MotorText Property Tests

    [Fact]
    public void MotorText_WithMotorOff_ReturnsEmpty()
    {
        // Arrange
        var snapshot = CreateSnapshot(motorOn: false, motorOffScheduled: false);
        var viewModel = new DiskStatusWidgetViewModel(_mockEmulator.Object, _mockFileDialogService.Object, _mockMessageBoxService.Object, _mockProjectManager.Object, snapshot);

        // Act
        var result = viewModel.MotorText;

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public void MotorText_WithMotorOn_ReturnsLightningIcon()
    {
        // Arrange
        var snapshot = CreateSnapshot(motorOn: true, motorOffScheduled: false);
        var viewModel = new DiskStatusWidgetViewModel(_mockEmulator.Object, _mockFileDialogService.Object, _mockMessageBoxService.Object, _mockProjectManager.Object, snapshot);

        // Act
        var result = viewModel.MotorText;

        // Assert
        Assert.Equal("⚡", result);
    }

    [Fact]
    public void MotorText_WithMotorOffScheduled_ReturnsClockIcon()
    {
        // Arrange
        var snapshot = CreateSnapshot(motorOn: false, motorOffScheduled: true);
        var viewModel = new DiskStatusWidgetViewModel(_mockEmulator.Object, _mockFileDialogService.Object, _mockMessageBoxService.Object, _mockProjectManager.Object, snapshot);

        // Act
        var result = viewModel.MotorText;

        // Assert
        Assert.Equal("⌚", result);
    }

    [Fact]
    public void MotorText_WithBothStates_ReturnsBothIcons()
    {
        // Arrange
        var snapshot = CreateSnapshot(motorOn: true, motorOffScheduled: true);
        var viewModel = new DiskStatusWidgetViewModel(_mockEmulator.Object, _mockFileDialogService.Object, _mockMessageBoxService.Object, _mockProjectManager.Object, snapshot);

        // Act
        var result = viewModel.MotorText;

        // Assert
        Assert.Equal("⌚⚡", result);
    }

    #endregion

    #region UpdateSnapshot Tests

    [Fact]
    public void UpdateSnapshot_WithNewDisk_UpdatesAllProperties()
    {
        // Arrange
        var initialSnapshot = CreateSnapshot();
        var viewModel = new DiskStatusWidgetViewModel(_mockEmulator.Object, _mockFileDialogService.Object, _mockMessageBoxService.Object, _mockProjectManager.Object, initialSnapshot);

        var newSnapshot = CreateSnapshot(
            slotNumber: 5,
            driveNumber: 2,
            diskImagePath: @"C:\newDisk.dsk",
            diskImageFilename: "newDisk.dsk",
            isReadOnly: true,
            track: 20.25,
            sector: 7,
            motorOn: true,
            motorOffScheduled: true,
            phaseState: 0b1010); // p0=0, p1=1, p2=0, p3=1

        // Act
        viewModel.UpdateSnapshot(newSnapshot);

        // Assert
        Assert.Equal("S5D2", viewModel.DiskId);
        Assert.Equal("newDisk.dsk", viewModel.Filename);
        Assert.Equal(@"C:\newDisk.dsk", viewModel.DiskImagePathTooltip);
        Assert.Equal(Brushes.Red, viewModel.FilenameForeground);
        Assert.Equal("T:20.25 S:07", viewModel.TrackSectorText);
        Assert.Equal(Brushes.Red, viewModel.TrackSectorForeground);
        Assert.Equal("ϕ:-+-+", viewModel.PhaseText); // p0=0, p1=1, p2=0, p3=1
        Assert.Equal("⌚⚡", viewModel.MotorText);
    }

    [Fact]
    public void UpdateSnapshot_FromDiskToEmpty_UpdatesProperties()
    {
        // Arrange
        var initialSnapshot = CreateSnapshot(
            diskImagePath: @"C:\disk.dsk",
            diskImageFilename: "disk.dsk",
            track: 17.0,
            sector: 10,
            motorOn: true);
        var viewModel = new DiskStatusWidgetViewModel(_mockEmulator.Object, _mockFileDialogService.Object, _mockMessageBoxService.Object, _mockProjectManager.Object, initialSnapshot);

        var emptySnapshot = CreateSnapshot();

        // Act
        viewModel.UpdateSnapshot(emptySnapshot);

        // Assert
        Assert.Equal("(empty)", viewModel.Filename);
        Assert.Equal("No disk inserted", viewModel.DiskImagePathTooltip);
        Assert.Equal("T-- S--", viewModel.TrackSectorText);
        Assert.Equal("", viewModel.MotorText);
    }

    [Fact]
    public void UpdateSnapshot_TrackChanges_UpdatesTrackSectorText()
    {
        // Arrange
        var initialSnapshot = CreateSnapshot(
            diskImageFilename: "test.dsk",
            track: 10.0,
            sector: 5);
        var viewModel = new DiskStatusWidgetViewModel(_mockEmulator.Object, _mockFileDialogService.Object, _mockMessageBoxService.Object, _mockProjectManager.Object, initialSnapshot);

        var updatedSnapshot = CreateSnapshot(
            diskImageFilename: "test.dsk",
            track: 15.75,
            sector: 12);

        // Act
        viewModel.UpdateSnapshot(updatedSnapshot);

        // Assert
        Assert.Equal("T:15.75 S:12", viewModel.TrackSectorText);
    }

    [Fact]
    public void UpdateSnapshot_PhaseChanges_UpdatesPhaseText()
    {
        // Arrange
        var initialSnapshot = CreateSnapshot(phaseState: 0b0001);
        var viewModel = new DiskStatusWidgetViewModel(_mockEmulator.Object, _mockFileDialogService.Object, _mockMessageBoxService.Object, _mockProjectManager.Object, initialSnapshot);

        var updatedSnapshot = CreateSnapshot(phaseState: 0b1000);

        // Act
        viewModel.UpdateSnapshot(updatedSnapshot);

        // Assert
        Assert.Equal("ϕ:---+", viewModel.PhaseText);
    }

    [Fact]
    public void UpdateSnapshot_MotorStateChanges_UpdatesMotorText()
    {
        // Arrange
        var initialSnapshot = CreateSnapshot(motorOn: false, motorOffScheduled: false);
        var viewModel = new DiskStatusWidgetViewModel(_mockEmulator.Object, _mockFileDialogService.Object, _mockMessageBoxService.Object, _mockProjectManager.Object, initialSnapshot);

        var updatedSnapshot = CreateSnapshot(motorOn: true, motorOffScheduled: true);

        // Act
        viewModel.UpdateSnapshot(updatedSnapshot);

        // Assert
        Assert.Equal("⌚⚡", viewModel.MotorText);
    }

    [Fact]
    public void UpdateSnapshot_ReadOnlyChanges_UpdatesColors()
    {
        // Arrange
        var initialSnapshot = CreateSnapshot(
            diskImageFilename: "test.dsk",
            isReadOnly: false);
        var viewModel = new DiskStatusWidgetViewModel(_mockEmulator.Object, _mockFileDialogService.Object, _mockMessageBoxService.Object, _mockProjectManager.Object, initialSnapshot);

        var readOnlySnapshot = CreateSnapshot(
            diskImageFilename: "test.dsk",
            isReadOnly: true);

        // Act
        viewModel.UpdateSnapshot(readOnlySnapshot);

        // Assert
        Assert.Equal(Brushes.Red, viewModel.FilenameForeground);
        Assert.Equal(Brushes.Red, viewModel.TrackSectorForeground);
    }

    #endregion

    #region PropertyChanged Event Tests

    [Fact]
    public void UpdateSnapshot_RaisesPropertyChangedForAllProperties()
    {
        // Arrange
        var initialSnapshot = CreateSnapshot();
        var viewModel = new DiskStatusWidgetViewModel(_mockEmulator.Object, _mockFileDialogService.Object, _mockMessageBoxService.Object, _mockProjectManager.Object, initialSnapshot);

        var propertyChangedEvents = new List<string>();
        viewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName != null)
            {
                propertyChangedEvents.Add(args.PropertyName);
            }
        };

        var newSnapshot = CreateSnapshot(
            slotNumber: 5,
            driveNumber: 2,
            diskImagePath: @"C:\test.dsk",
            diskImageFilename: "test.dsk");

        // Act
        viewModel.UpdateSnapshot(newSnapshot);

        // Assert
        Assert.Contains(nameof(DiskStatusWidgetViewModel.DiskId), propertyChangedEvents);
        Assert.Contains(nameof(DiskStatusWidgetViewModel.Filename), propertyChangedEvents);
        Assert.Contains(nameof(DiskStatusWidgetViewModel.DiskImagePathTooltip), propertyChangedEvents);
        Assert.Contains(nameof(DiskStatusWidgetViewModel.FilenameForeground), propertyChangedEvents);
        Assert.Contains(nameof(DiskStatusWidgetViewModel.TrackSectorText), propertyChangedEvents);
        Assert.Contains(nameof(DiskStatusWidgetViewModel.TrackSectorForeground), propertyChangedEvents);
        Assert.Contains(nameof(DiskStatusWidgetViewModel.PhaseText), propertyChangedEvents);
        Assert.Contains(nameof(DiskStatusWidgetViewModel.MotorText), propertyChangedEvents);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void IntegrationTest_SimulateDiskInsertionAndAccess()
    {
        // Arrange - Start with empty drive
        var emptySnapshot = CreateSnapshot(slotNumber: 6, driveNumber: 1);
        var viewModel = new DiskStatusWidgetViewModel(_mockEmulator.Object, _mockFileDialogService.Object, _mockMessageBoxService.Object, _mockProjectManager.Object, emptySnapshot);

        Assert.Equal("(empty)", viewModel.Filename);
        Assert.Equal("T-- S--", viewModel.TrackSectorText);

        // Act 1 - Insert disk and turn on motor
        var diskInsertedSnapshot = CreateSnapshot(
            slotNumber: 6,
            driveNumber: 1,
            diskImagePath: @"C:\Disks\game.dsk",
            diskImageFilename: "game.dsk",
            isReadOnly: false,
            track: 0.0,
            sector: 0,
            motorOn: true,
            phaseState: 0b0001);

        viewModel.UpdateSnapshot(diskInsertedSnapshot);

        // Assert 1
        Assert.Equal("game.dsk", viewModel.Filename);
        Assert.Equal("T:0.00 S:00", viewModel.TrackSectorText);
        Assert.Equal("⚡", viewModel.MotorText);
        Assert.Equal("ϕ:+---", viewModel.PhaseText);

        // Act 2 - Seek to track 17
        var seekSnapshot = CreateSnapshot(
            slotNumber: 6,
            driveNumber: 1,
            diskImagePath: @"C:\Disks\game.dsk",
            diskImageFilename: "game.dsk",
            isReadOnly: false,
            track: 17.0,
            sector: 5,
            motorOn: true,
            phaseState: 0b0100);

        viewModel.UpdateSnapshot(seekSnapshot);

        // Assert 2
        Assert.Equal("T:17.00 S:05", viewModel.TrackSectorText);
        Assert.Equal("ϕ:--+-", viewModel.PhaseText);

        // Act 3 - Schedule motor off
        var motorOffScheduledSnapshot = CreateSnapshot(
            slotNumber: 6,
            driveNumber: 1,
            diskImagePath: @"C:\Disks\game.dsk",
            diskImageFilename: "game.dsk",
            isReadOnly: false,
            track: 17.0,
            sector: 5,
            motorOn: true,
            motorOffScheduled: true,
            phaseState: 0b0000);

        viewModel.UpdateSnapshot(motorOffScheduledSnapshot);

        // Assert 3
        Assert.Equal("⌚⚡", viewModel.MotorText);

        // Act 4 - Motor stops
        var motorOffSnapshot = CreateSnapshot(
            slotNumber: 6,
            driveNumber: 1,
            diskImagePath: @"C:\Disks\game.dsk",
            diskImageFilename: "game.dsk",
            isReadOnly: false,
            track: 17.0,
            sector: -1,
            motorOn: false,
            motorOffScheduled: false,
            phaseState: 0b0000);

        viewModel.UpdateSnapshot(motorOffSnapshot);

        // Assert 4
        Assert.Equal("", viewModel.MotorText);
        Assert.Equal("T:17.00 S:--", viewModel.TrackSectorText);
    }

    [Fact]
    public void IntegrationTest_WriteProtectedDiskHandling()
    {
        // Arrange - Insert write-protected disk
        var writeProtectedSnapshot = CreateSnapshot(
            slotNumber: 6,
            driveNumber: 1,
            diskImagePath: @"C:\Disks\readonly.dsk",
            diskImageFilename: "readonly.dsk",
            isReadOnly: true,
            track: 0.0,
            sector: 0);

        var viewModel = new DiskStatusWidgetViewModel(_mockEmulator.Object, _mockFileDialogService.Object, _mockMessageBoxService.Object, _mockProjectManager.Object, writeProtectedSnapshot);

        // Assert - Red color coding
        Assert.Equal(Brushes.Red, viewModel.FilenameForeground);
        Assert.Equal(Brushes.Red, viewModel.TrackSectorForeground);

        // Act - Switch to writable disk
        var writableSnapshot = CreateSnapshot(
            slotNumber: 6,
            driveNumber: 1,
            diskImagePath: @"C:\Disks\writable.dsk",
            diskImageFilename: "writable.dsk",
            isReadOnly: false,
            track: 0.0,
            sector: 0);

        viewModel.UpdateSnapshot(writableSnapshot);

        // Assert - White color coding
        Assert.Equal(Brushes.White, viewModel.FilenameForeground);
        Assert.Equal(Brushes.White, viewModel.TrackSectorForeground);
    }

    #endregion
}
