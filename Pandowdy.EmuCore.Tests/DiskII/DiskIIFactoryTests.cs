using Pandowdy.EmuCore.DiskII;
using Pandowdy.EmuCore.DiskII.Providers;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Tests.Helpers;

namespace Pandowdy.EmuCore.Tests.DiskII;

/// <summary>
/// Tests for DiskIIFactory - drive creation with telemetry.
/// </summary>
public class DiskIIFactoryTests : IDisposable
{
    private readonly MockTelemetryAggregator _telemetry = new();
    private readonly DiskImageFactory _imageFactory = new();

    public void Dispose()
    {
        _telemetry.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ThrowsOnNullImageFactory()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DiskIIFactory(null!, _telemetry));
    }

    [Fact]
    public void Constructor_ThrowsOnNullTelemetry()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DiskIIFactory(_imageFactory, null!));
    }

    [Fact]
    public void Constructor_Succeeds_WithValidParameters()
    {
        // Act
        var factory = new DiskIIFactory(_imageFactory, _telemetry);

        // Assert - no exception
        Assert.NotNull(factory);
    }

    #endregion

    #region CreateDrive Tests

    [Fact]
    public void CreateDrive_ReturnsWrappedDrive()
    {
        // Arrange
        var factory = new DiskIIFactory(_imageFactory, _telemetry);

        // Act
        IDiskIIDrive drive = factory.CreateDrive("Slot6-D1");

        // Assert - should be wrapped in debug decorator
        Assert.IsType<DiskIIDebugDecorator>(drive);
    }

    [Fact]
    public void CreateDrive_SetsCorrectName()
    {
        // Arrange
        var factory = new DiskIIFactory(_imageFactory, _telemetry);

        // Act
        IDiskIIDrive drive = factory.CreateDrive("Slot6-D1");

        // Assert
        Assert.Equal("Slot6-D1", drive.Name);
    }

    [Fact]
    public void CreateDrive_CreatesEmptyDrive()
    {
        // Arrange
        var factory = new DiskIIFactory(_imageFactory, _telemetry);

        // Act
        IDiskIIDrive drive = factory.CreateDrive("Slot6-D1");

        // Assert - no disk inserted
        Assert.False(drive.HasDisk);
    }

    [Fact]
    public void CreateDrive_RegistersTelemetryId()
    {
        // Arrange
        var factory = new DiskIIFactory(_imageFactory, _telemetry);
        _telemetry.Clear();

        // Act
        IDiskIIDrive drive = factory.CreateDrive("Slot6-D1");

        // Trigger telemetry by changing motor state
        drive.MotorOn = true;

        // Assert - telemetry was published
        Assert.NotEmpty(_telemetry.PublishedMessages);
        Assert.Equal(DiskIIConstants.TelemetryCategory, _telemetry.PublishedMessages[0].SourceId.Category);
    }

    #endregion

    #region CreateDriveWithDisk Tests

    [Fact]
    public void CreateDriveWithDisk_LoadsDisk()
    {
        // Arrange
        if (!TestDiskImages.TestImagesAvailable)
        {
            return; // Skip if test images not available
        }

        var factory = new DiskIIFactory(_imageFactory, _telemetry);

        // Act
        IDiskIIDrive drive = factory.CreateDriveWithDisk("Slot6-D1", TestDiskImages.TestNib);

        // Assert
        Assert.True(drive.HasDisk);
    }

    [Fact]
    public void CreateDriveWithDisk_SetsCorrectName()
    {
        // Arrange
        if (!TestDiskImages.TestImagesAvailable)
        {
            return; // Skip if test images not available
        }

        var factory = new DiskIIFactory(_imageFactory, _telemetry);

        // Act
        IDiskIIDrive drive = factory.CreateDriveWithDisk("Slot5-D2", TestDiskImages.TestNib);

        // Assert
        Assert.Equal("Slot5-D2", drive.Name);
    }

    [Fact]
    public void CreateDriveWithDisk_ReturnsWrappedDrive()
    {
        // Arrange
        if (!TestDiskImages.TestImagesAvailable)
        {
            return; // Skip if test images not available
        }

        var factory = new DiskIIFactory(_imageFactory, _telemetry);

        // Act
        IDiskIIDrive drive = factory.CreateDriveWithDisk("Slot6-D1", TestDiskImages.TestNib);

        // Assert - should be wrapped in debug decorator
        Assert.IsType<DiskIIDebugDecorator>(drive);
    }

    #endregion

    #region ParseDriveName Tests

    [Theory]
    [InlineData("Slot6-D1", 6, 1)]
    [InlineData("Slot5-D2", 5, 2)]
    [InlineData("Slot1-D1", 1, 1)]
    [InlineData("Slot7-D2", 7, 2)]
    [InlineData("slot6-d1", 6, 1)]  // Case insensitive
    [InlineData("SLOT6-D1", 6, 1)]  // All caps
    public void ParseDriveName_ParsesValidNames(string name, int expectedSlot, int expectedDrive)
    {
        // Act
        var (slot, drive) = DiskIIFactory.ParseDriveName(name);

        // Assert
        Assert.Equal(expectedSlot, slot);
        Assert.Equal(expectedDrive, drive);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Invalid")]
    [InlineData("Drive1")]
    [InlineData("6-1")]
    [InlineData("Slot-D1")]
    [InlineData("SlotX-D1")]
    [InlineData("Slot6-DX")]
    [InlineData("Slot6D1")]  // Missing separator
    public void ParseDriveName_ReturnsDefault_ForInvalidNames(string name)
    {
        // Act
        var (slot, drive) = DiskIIFactory.ParseDriveName(name);

        // Assert - default is (6, 1)
        Assert.Equal(6, slot);
        Assert.Equal(1, drive);
    }

    [Fact]
    public void ParseDriveName_ReturnsDefault_ForNullName()
    {
        // Act - null triggers exception caught by try-catch, returns default
        var (slot, drive) = DiskIIFactory.ParseDriveName(null!);

        // Assert - default is (6, 1)
        Assert.Equal(6, slot);
        Assert.Equal(1, drive);
    }

    #endregion
}
