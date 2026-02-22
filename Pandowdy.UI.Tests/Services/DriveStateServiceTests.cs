// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Moq;
using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.DiskII;
using Pandowdy.EmuCore.DiskII.Providers;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Services;
using Pandowdy.UI.Models;
using Pandowdy.UI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Pandowdy.UI.Tests.Services;

/// <summary>
/// Tests for DriveStateService - disk drive state persistence.
/// </summary>
public class DriveStateServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly Mock<ISlots> _mockSlots;
    private readonly DiskStatusProvider _diskStatusProvider;
    private readonly TestDriveStateService _service;

    public DriveStateServiceTests()
    {
        // Use a random test directory to avoid conflicts
        _testDirectory = Path.Combine(Path.GetTempPath(), "PandowdyTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);

        _mockSlots = new Mock<ISlots>();
        _diskStatusProvider = new DiskStatusProvider();

        // Pass diskStatusProvider as both provider and mutator (it implements both interfaces)
        _service = new TestDriveStateService(_diskStatusProvider, _diskStatusProvider, _mockSlots.Object, _testDirectory);
    }

    public void Dispose()
    {
        // Clean up test directory after each test
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
            catch
            {
                // Ignore cleanup failures - they won't affect test results
            }
        }
    }

    /// <summary>
    /// Test-specific DriveStateService that uses a custom directory instead of %AppData%.
    /// </summary>
    private class TestDriveStateService(
        EmuCore.Services.IDiskStatusProvider diskStatusProvider,
        EmuCore.Services.IDiskStatusMutator diskStatusMutator,
        ISlots slots,
        string testDirectory)
        : DriveStateService(diskStatusProvider, diskStatusMutator, slots)
    {
        public override string GetDriveStateFilePath()
        {
            return Path.Combine(testDirectory, "drive-state.json");
        }
    }

    #region Test Helpers

    /// <summary>
    /// Test stub for IDiskIIDrive to avoid real disk operations.
    /// </summary>
    private class TestDrive : IDiskIIDrive
    {
        public string? InsertedDiskPath { get; private set; }
        public string Name => "TestDrive";
        public double Track => 17.0;
        public int QuarterTrack => 68;
        public bool HasDisk => InsertedDiskPath != null;
        public string? CurrentDiskPath => InsertedDiskPath;
        public IDiskImageProvider? ImageProvider { get; set; }
        public InternalDiskImage? InternalImage => null;
        public byte OptimalBitTiming => 32;

        public void InsertDisk(string diskImagePath)
        {
            InsertedDiskPath = diskImagePath;
        }

        public void Reset() { }
        public void Restart() { }
        public void EjectDisk() => InsertedDiskPath = null;
        public void StepToHigherTrack() { }
        public void StepToLowerTrack() { }
        public bool? GetBit(ulong currentCycle) => null;
        public int AdvanceAndReadBits(double elapsedCycles, Span<bool> bits) => 0;
        public bool SetBit(bool value) => false;
        public bool IsWriteProtected() => false;
        public void NotifyMotorStateChanged(bool motorOn, ulong cycleCount) { }
    }

    /// <summary>
    /// Test wrapper for DiskIIControllerCard that exposes drives directly for testing.
    /// This concrete implementation allows DriveStateService to cast and access Drives.
    /// </summary>
    private class TestDiskController : Pandowdy.EmuCore.Cards.DiskIIControllerCard
    {
        public TestDiskController(params IDiskIIDrive[] drives)
            : base(
                new CpuClockingCounters(),
                Mock.Of<IDiskIIFactory>(),
                Mock.Of<IDiskStatusMutator>(),
                Mock.Of<ICardResponseEmitter>(),
                Mock.Of<IDiskImageStore>())
        {
            _drives = drives; // Set drives directly (protected field)
        }

        public override string Name => "Test Disk II Controller";
        public override string Description => "Test controller for DriveStateService tests";
        public override int Id => 101; // Disk II controller ID
        public override byte? ReadRom(byte offset) => null;
        public override ICard Clone() => new TestDiskController(_drives);

        public override Pandowdy.EmuCore.Cards.DiskIIControllerCard CreateWithStore(IDiskImageStore diskImageStore)
        {
            return new TestDiskController(_drives);
        }
    }

    #endregion

    #region GetDriveStateFilePath Tests

    [Fact]
    public void GetDriveStateFilePath_ReturnsExpectedPath()
    {
        // Act
        var path = _service.GetDriveStateFilePath();

        // Assert
        Assert.NotNull(path);
        Assert.Contains("drive-state.json", path);
    }

    #endregion

    #region LoadDriveStateAsync Tests

    [Fact]
    public async Task LoadDriveStateAsync_WhenFileDoesNotExist_ReturnsEmptyConfig()
    {
        // Act
        var config = await _service.LoadDriveStateAsync();

        // Assert
        Assert.NotNull(config);
        Assert.Equal("1.0", config.Version);
        Assert.Empty(config.Drives);
    }

    [Fact]
    public async Task LoadDriveStateAsync_WhenFileExists_LoadsConfig()
    {
        // Arrange - Save config first
        var originalConfig = new DriveStateConfig
        {
            Version = "1.0",
            Drives =
            [
                new() { Slot = 6, DriveNumber = 1, DiskImagePath = @"C:\test1.dsk" },
                new() { Slot = 6, DriveNumber = 2, DiskImagePath = @"C:\test2.dsk" }
            ]
        };
        await _service.SaveDriveStateAsync(originalConfig);

        // Act
        var loadedConfig = await _service.LoadDriveStateAsync();

        // Assert
        Assert.NotNull(loadedConfig);
        Assert.Equal(2, loadedConfig.Drives.Count);
        Assert.Equal(6, loadedConfig.Drives[0].Slot);
        Assert.Equal(1, loadedConfig.Drives[0].DriveNumber);
        Assert.Equal(@"C:\test1.dsk", loadedConfig.Drives[0].DiskImagePath);
    }

    [Fact]
    public async Task LoadDriveStateAsync_WhenFileIsCorrupt_ReturnsEmptyConfig()
    {
        // Arrange - Write invalid JSON
        var filePath = _service.GetDriveStateFilePath();
        await File.WriteAllTextAsync(filePath, "{ invalid json }");

        // Act
        var config = await _service.LoadDriveStateAsync();

        // Assert - Should return empty config on error
        Assert.NotNull(config);
        Assert.Empty(config.Drives);
    }

    #endregion

    #region SaveDriveStateAsync Tests

    [Fact]
    public async Task SaveDriveStateAsync_CreatesDirectoryIfNotExists()
    {
        // Arrange
        var config = new DriveStateConfig
        {
            Version = "1.0",
            Drives =
            [
                new() { Slot = 6, DriveNumber = 1, DiskImagePath = @"C:\test.dsk" }
            ]
        };

        // Act
        await _service.SaveDriveStateAsync(config);

        // Assert
        var filePath = _service.GetDriveStateFilePath();
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public async Task SaveDriveStateAsync_WritesValidJson()
    {
        // Arrange
        var config = new DriveStateConfig
        {
            Version = "1.0",
            Drives =
            [
                new() { Slot = 5, DriveNumber = 2, DiskImagePath = @"C:\disk.nib" }
            ]
        };

        // Act
        await _service.SaveDriveStateAsync(config);

        // Assert - Read raw file and verify it's valid JSON
        var filePath = _service.GetDriveStateFilePath();
        var json = await File.ReadAllTextAsync(filePath);
        Assert.Contains("\"Version\"", json);
        Assert.Contains("\"Drives\"", json);
        Assert.Contains("\"Slot\"", json);
        Assert.Contains("\"DriveNumber\"", json);
    }

    [Fact]
    public async Task SaveDriveStateAsync_ThenLoad_PreservesAllEntries()
    {
        // Arrange
        var originalConfig = new DriveStateConfig
        {
            Version = "1.0",
            Drives =
            [
                new() { Slot = 6, DriveNumber = 1, DiskImagePath = @"C:\disk1.dsk" },
                new() { Slot = 6, DriveNumber = 2, DiskImagePath = @"C:\disk2.dsk" },
                new() { Slot = 5, DriveNumber = 1, DiskImagePath = @"C:\disk3.nib" }
            ]
        };

        // Act
        await _service.SaveDriveStateAsync(originalConfig);
        var loadedConfig = await _service.LoadDriveStateAsync();

        // Assert
        Assert.Equal(3, loadedConfig.Drives.Count);
        Assert.Equal(originalConfig.Drives[0].Slot, loadedConfig.Drives[0].Slot);
        Assert.Equal(originalConfig.Drives[0].DriveNumber, loadedConfig.Drives[0].DriveNumber);
        Assert.Equal(originalConfig.Drives[0].DiskImagePath, loadedConfig.Drives[0].DiskImagePath);
    }

    #endregion

    #region CaptureDriveStateAsync Tests

    [Fact]
    public async Task CaptureDriveStateAsync_WhenNoDrives_SavesEmptyConfig()
    {
        // Arrange - No drives registered

        // Act
        await _service.CaptureDriveStateAsync();

        // Assert
        var config = await _service.LoadDriveStateAsync();
        Assert.Empty(config.Drives);
    }

    [Fact]
    public async Task CaptureDriveStateAsync_WhenDrivesWithDisks_SavesTheirState()
    {
        // Arrange - Register drives with disks
        _diskStatusProvider.RegisterDrive(6, 1);
        _diskStatusProvider.RegisterDrive(6, 2);

        // Update drive 1 with disk path
        _diskStatusProvider.MutateDrive(6, 1, builder =>
        {
            builder.DiskImagePath = @"C:\test1.dsk";
            builder.DiskImageFilename = "test1.dsk";
        });

        // Update drive 2 with disk path
        _diskStatusProvider.MutateDrive(6, 2, builder =>
        {
            builder.DiskImagePath = @"C:\test2.nib";
            builder.DiskImageFilename = "test2.nib";
        });

        // Act
        await _service.CaptureDriveStateAsync();

        // Assert
        var config = await _service.LoadDriveStateAsync();
        Assert.Equal(2, config.Drives.Count);
        Assert.Contains(config.Drives, d => d.Slot == 6 && d.DriveNumber == 1 && d.DiskImagePath == @"C:\test1.dsk");
        Assert.Contains(config.Drives, d => d.Slot == 6 && d.DriveNumber == 2 && d.DiskImagePath == @"C:\test2.nib");
    }

    [Fact]
    public async Task CaptureDriveStateAsync_WhenDriveWithoutDisk_DoesNotSaveIt()
    {
        // Arrange - Register drive without disk
        _diskStatusProvider.RegisterDrive(6, 1);
        _diskStatusProvider.RegisterDrive(6, 2);

        // Only drive 1 has a disk
        _diskStatusProvider.MutateDrive(6, 1, builder =>
        {
            builder.DiskImagePath = @"C:\test.dsk";
            builder.DiskImageFilename = "test.dsk";
        });

        // Act
        await _service.CaptureDriveStateAsync();

        // Assert
        var config = await _service.LoadDriveStateAsync();
        Assert.Single(config.Drives);
        Assert.Equal(6, config.Drives[0].Slot);
        Assert.Equal(1, config.Drives[0].DriveNumber);
    }

    #endregion

    #region LoadAndRestoreDriveStateAsync Tests

    [Fact]
    public async Task LoadAndRestoreDriveStateAsync_WhenNoStateFile_DoesNotThrow()
    {
        // Act & Assert - Should not throw when file doesn't exist
        var exception = await Record.ExceptionAsync(() => _service.LoadAndRestoreDriveStateAsync());
        Assert.Null(exception);
    }

    [Fact]
    public async Task LoadAndRestoreDriveStateAsync_WhenDiskFileDoesNotExist_SkipsThatDrive()
    {
        // Arrange - Save config with non-existent file
        var config = new DriveStateConfig
        {
            Version = "1.0",
            Drives =
            [
                new() { Slot = 6, DriveNumber = 1, DiskImagePath = @"C:\nonexistent.dsk" }
            ]
        };
        await _service.SaveDriveStateAsync(config);

        // Act & Assert - Should not throw
        var exception = await Record.ExceptionAsync(() => _service.LoadAndRestoreDriveStateAsync());
        Assert.Null(exception);
    }

    [Fact]
    public async Task LoadAndRestoreDriveStateAsync_WhenValidDisksExist_RestoresThem()
    {
        // Arrange - Create test disk files
        var disk1Path = Path.Combine(_testDirectory, "test1.dsk");
        var disk2Path = Path.Combine(_testDirectory, "test2.dsk");
        await File.WriteAllTextAsync(disk1Path, "test disk 1");
        await File.WriteAllTextAsync(disk2Path, "test disk 2");

        // Save config
        var config = new DriveStateConfig
        {
            Version = "1.0",
            Drives =
            [
                new() { Slot = 6, DriveNumber = 1, DiskImagePath = disk1Path },
                new() { Slot = 6, DriveNumber = 2, DiskImagePath = disk2Path }
            ]
        };
        await _service.SaveDriveStateAsync(config);

        // Setup test controller with test drives
        var testDrive1 = new TestDrive();
        var testDrive2 = new TestDrive();
        var testController = new TestDiskController(testDrive1, testDrive2);

        _mockSlots.Setup(s => s.GetCardIn(SlotNumber.Slot6)).Returns(testController);

        // Act
        await _service.LoadAndRestoreDriveStateAsync();

        // Assert
        Assert.Equal(disk1Path, testDrive1.InsertedDiskPath);
        Assert.Equal(disk2Path, testDrive2.InsertedDiskPath);
    }

    [Fact]
    public async Task LoadAndRestoreDriveStateAsync_WhenSlotHasNoCard_SkipsThatEntry()
    {
        // Arrange - Create test disk file
        var diskPath = Path.Combine(_testDirectory, "test.dsk");
        await File.WriteAllTextAsync(diskPath, "test disk");

        var config = new DriveStateConfig
        {
            Version = "1.0",
            Drives =
            [
                new() { Slot = 6, DriveNumber = 1, DiskImagePath = diskPath }
            ]
        };
        await _service.SaveDriveStateAsync(config);

        // Setup: slot 6 has no card
        _mockSlots.Setup(s => s.GetCardIn(SlotNumber.Slot6)).Returns((ICard?)null);

        // Act & Assert - Should not throw
        var exception = await Record.ExceptionAsync(() => _service.LoadAndRestoreDriveStateAsync());
        Assert.Null(exception);
    }

    [Fact]
    public async Task LoadAndRestoreDriveStateAsync_WhenSlotHasNonDiskCard_SkipsThatEntry()
    {
        // Arrange - Create test disk file
        var diskPath = Path.Combine(_testDirectory, "test.dsk");
        await File.WriteAllTextAsync(diskPath, "test disk");

        var config = new DriveStateConfig
        {
            Version = "1.0",
            Drives =
            [
                new() { Slot = 6, DriveNumber = 1, DiskImagePath = diskPath }
            ]
        };
        await _service.SaveDriveStateAsync(config);

        // Setup: slot 6 has a non-disk card
        var mockCard = new Mock<ICard>();
        _mockSlots.Setup(s => s.GetCardIn(SlotNumber.Slot6)).Returns(mockCard.Object);

        // Act & Assert - Should not throw
        var exception = await Record.ExceptionAsync(() => _service.LoadAndRestoreDriveStateAsync());
        Assert.Null(exception);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task Integration_CaptureAndRestore_WorksTogether()
    {
        // Arrange - Register drives and add disks
        _diskStatusProvider.RegisterDrive(6, 1);

        _diskStatusProvider.MutateDrive(6, 1, builder =>
        {
            builder.DiskImagePath = Path.Combine(_testDirectory, "integration.dsk");
            builder.DiskImageFilename = "integration.dsk";
        });

        // Create the test file
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "integration.dsk"), "integration test");

        // Setup test controller with test drive
        var testDrive = new TestDrive();
        var testController = new TestDiskController(testDrive);
        _mockSlots.Setup(s => s.GetCardIn(SlotNumber.Slot6)).Returns(testController);

        // Act - Capture then restore
        await _service.CaptureDriveStateAsync();
        await _service.LoadAndRestoreDriveStateAsync();

        // Assert
        Assert.NotNull(testDrive.InsertedDiskPath);
        Assert.Contains("integration.dsk", testDrive.InsertedDiskPath);
    }

    #endregion

    #region RestoreDriveState Tests

    [Fact]
    public void RestoreDriveState_WithNullSettings_ReturnsWithoutError()
    {
        // Arrange
        DriveStateSettings? settings = null;

        // Act & Assert - should not throw
        _service.RestoreDriveState(settings);
    }

    [Fact]
    public void RestoreDriveState_WithEmptyControllers_ReturnsWithoutError()
    {
        // Arrange
        var settings = new DriveStateSettings
        {
            Controllers = []
        };

        // Act & Assert - should not throw
        _service.RestoreDriveState(settings);
    }

    [Fact]
    public void RestoreDriveState_WithValidSettings_RestoresDisksToCorrectDrives()
    {
        // Arrange
        var disk1Path = Path.Combine(_testDirectory, "test1.dsk");
        var disk2Path = Path.Combine(_testDirectory, "test2.dsk");
        File.WriteAllText(disk1Path, "test disk 1");
        File.WriteAllText(disk2Path, "test disk 2");

        var drive1 = new TestDrive();
        var drive2 = new TestDrive();
        var controller = new TestDiskController(drive1, drive2);
        _mockSlots.Setup(s => s.GetCardIn(SlotNumber.Slot6)).Returns(controller);

        var settings = new DriveStateSettings
        {
            Controllers =
            [
                new DiskControllerEntry
                {
                    Slot = 6,
                    Drives =
                    [
                        new DriveEntry { Drive = 1, ImagePath = disk1Path },
                        new DriveEntry { Drive = 2, ImagePath = disk2Path }
                    ]
                }
            ]
        };

        // Act
        _service.RestoreDriveState(settings);

        // Assert
        Assert.Equal(disk1Path, drive1.InsertedDiskPath);
        Assert.Equal(disk2Path, drive2.InsertedDiskPath);
    }

    [Fact]
    public void RestoreDriveState_WithNonexistentFile_SkipsFilesAndContinues()
    {
        // Arrange
        var existingDisk = Path.Combine(_testDirectory, "exists.dsk");
        var nonexistentDisk = Path.Combine(_testDirectory, "nonexistent.dsk");
        File.WriteAllText(existingDisk, "existing disk");

        var drive1 = new TestDrive();
        var drive2 = new TestDrive();
        var controller = new TestDiskController(drive1, drive2);
        _mockSlots.Setup(s => s.GetCardIn(SlotNumber.Slot6)).Returns(controller);

        var settings = new DriveStateSettings
        {
            Controllers =
            [
                new DiskControllerEntry
                {
                    Slot = 6,
                    Drives =
                    [
                        new DriveEntry { Drive = 1, ImagePath = nonexistentDisk },
                        new DriveEntry { Drive = 2, ImagePath = existingDisk }
                    ]
                }
            ]
        };

        // Act
        _service.RestoreDriveState(settings);

        // Assert
        Assert.Null(drive1.InsertedDiskPath); // Nonexistent file skipped
        Assert.Equal(existingDisk, drive2.InsertedDiskPath); // Existing file loaded
    }

    [Fact]
    public void RestoreDriveState_WithMultipleControllers_RestoresAllControllers()
    {
        // Arrange
        var disk1Path = Path.Combine(_testDirectory, "slot5d1.dsk");
        var disk2Path = Path.Combine(_testDirectory, "slot6d1.dsk");
        File.WriteAllText(disk1Path, "slot 5 disk 1");
        File.WriteAllText(disk2Path, "slot 6 disk 1");

        var drive5 = new TestDrive();
        var controller5 = new TestDiskController(drive5);
        var drive6 = new TestDrive();
        var controller6 = new TestDiskController(drive6);
        _mockSlots.Setup(s => s.GetCardIn(SlotNumber.Slot5)).Returns(controller5);
        _mockSlots.Setup(s => s.GetCardIn(SlotNumber.Slot6)).Returns(controller6);

        var settings = new DriveStateSettings
        {
            Controllers =
            [
                new DiskControllerEntry
                {
                    Slot = 5,
                    Drives = [new DriveEntry { Drive = 1, ImagePath = disk1Path }]
                },
                new DiskControllerEntry
                {
                    Slot = 6,
                    Drives = [new DriveEntry { Drive = 1, ImagePath = disk2Path }]
                }
            ]
        };

        // Act
        _service.RestoreDriveState(settings);

        // Assert
        Assert.Equal(disk1Path, drive5.InsertedDiskPath);
        Assert.Equal(disk2Path, drive6.InsertedDiskPath);
    }

    #endregion
}
