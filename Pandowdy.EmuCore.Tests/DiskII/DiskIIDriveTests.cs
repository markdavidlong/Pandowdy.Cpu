using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.DiskII;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Tests.Helpers;

namespace Pandowdy.EmuCore.Tests.DiskII;

/// <summary>
/// Tests for DiskIIDrive - core drive implementation with telemetry.
/// </summary>
public class DiskIIDriveTests : IDisposable
{
    private readonly MockTelemetryAggregator _telemetry = new();
    private DiskIIDrive? _drive;

    public void Dispose()
    {
        _telemetry.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_SetsName()
    {
        // Act
        _drive = new DiskIIDrive("TestDrive", _telemetry, 6, 1);

        // Assert
        Assert.Equal("TestDrive", _drive.Name);
    }

    [Fact]
    public void Constructor_WithNullName_SetsUnnamed()
    {
        // Act
        _drive = new DiskIIDrive(null!, _telemetry, 6, 1);

        // Assert
        Assert.Equal("Unnamed", _drive.Name);
    }

    [Fact]
    public void Constructor_ThrowsOnNullTelemetry()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DiskIIDrive("Test", null!, 6, 1));
    }


    [Fact]
    public void Constructor_InitializesAtTrack17()
    {
        // Act
        _drive = new DiskIIDrive("TestDrive", _telemetry, 6, 1);

        // Assert
        Assert.Equal(17.0, _drive.Track);
        Assert.Equal(68, _drive.QuarterTrack); // 17 * 4
    }

    [Fact]
    public void Constructor_InitializesMotorOff()
    {
        // Act
        _drive = new DiskIIDrive("TestDrive", _telemetry, 6, 1);

        // Assert
        Assert.False(_drive.MotorOn);
    }

    [Fact]
    public void Constructor_WithImageProvider_SetsQuarterTrack()
    {
        // Arrange
        var mockProvider = new MockDiskImageProvider();

        // Act
        _drive = new DiskIIDrive("TestDrive", _telemetry, 6, 1, mockProvider);

        // Assert - provider should have been notified of initial track
        Assert.Equal(68, mockProvider.CurrentQuarterTrack); // Track 17
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_RestoresTrackTo17()
    {
        // Arrange
        _drive = new DiskIIDrive("TestDrive", _telemetry, 6, 1);
        _drive.StepToHigherTrack();
        _drive.StepToHigherTrack();

        // Act
        _drive.Reset();

        // Assert
        Assert.Equal(17.0, _drive.Track);
    }

    [Fact]
    public void Reset_TurnsMotorOff()
    {
        // Arrange
        _drive = new DiskIIDrive("TestDrive", _telemetry, 6, 1);
        _drive.MotorOn = true;
        _telemetry.Clear();

        // Act
        _drive.Reset();

        // Assert
        Assert.False(_drive.MotorOn);
    }

    #endregion

    #region Motor Tests

    [Fact]
    public void MotorOn_SameValue_DoesNotPublishTelemetry()
    {
        // Arrange
        _drive = new DiskIIDrive("TestDrive", _telemetry, 6, 1);
        _drive.MotorOn = true;
        _telemetry.Clear();

        // Act - set to same value
        _drive.MotorOn = true;

        // Assert - no new messages
        Assert.Empty(_telemetry.GetMessagesByType("motor"));
    }

    #endregion

    #region Track Stepping Tests

    [Fact]
    public void StepToHigherTrack_IncrementsQuarterTrack()
    {
        // Arrange
        _drive = new DiskIIDrive("TestDrive", _telemetry, 6, 1);
        int initialQuarterTrack = _drive.QuarterTrack;

        // Act
        _drive.StepToHigherTrack();

        // Assert
        Assert.Equal(initialQuarterTrack + 1, _drive.QuarterTrack);
    }


    [Fact]
    public void StepToLowerTrack_DecrementsQuarterTrack()
    {
        // Arrange
        _drive = new DiskIIDrive("TestDrive", _telemetry, 6, 1);
        int initialQuarterTrack = _drive.QuarterTrack;

        // Act
        _drive.StepToLowerTrack();

        // Assert
        Assert.Equal(initialQuarterTrack - 1, _drive.QuarterTrack);
    }



    [Fact]
    public void StepToHigherTrack_ClampsAtMax()
    {
        // Arrange
        _drive = new DiskIIDrive("TestDrive", _telemetry, 6, 1);

        // Act - step way beyond max
        for (int i = 0; i < 200; i++)
        {
            _drive.StepToHigherTrack();
        }

        // Assert
        Assert.Equal(DiskIIConstants.MaxQuarterTracks, _drive.QuarterTrack);
    }

    [Fact]
    public void StepToLowerTrack_ClampsAtZero()
    {
        // Arrange
        _drive = new DiskIIDrive("TestDrive", _telemetry, 6, 1);

        // Act - step way below zero
        for (int i = 0; i < 200; i++)
        {
            _drive.StepToLowerTrack();
        }

        // Assert
        Assert.Equal(0, _drive.QuarterTrack);
    }

    [Fact]
    public void StepToHigherTrack_NotifiesImageProvider()
    {
        // Arrange
        var mockProvider = new MockDiskImageProvider();
        _drive = new DiskIIDrive("TestDrive", _telemetry, 6, 1, mockProvider);
        int initialTrack = mockProvider.CurrentQuarterTrack;

        // Act
        _drive.StepToHigherTrack();

        // Assert
        Assert.Equal(initialTrack + 1, mockProvider.CurrentQuarterTrack);
    }

    #endregion

    #region Disk Operations Tests

    [Fact]
    public void HasDisk_ReturnsFalse_WhenNoDisk()
    {
        // Arrange
        _drive = new DiskIIDrive("TestDrive", _telemetry, 6, 1);

        // Assert
        Assert.False(_drive.HasDisk);
    }

    [Fact]
    public void HasDisk_ReturnsTrue_WhenDiskProvided()
    {
        // Arrange
        var mockProvider = new MockDiskImageProvider();
        _drive = new DiskIIDrive("TestDrive", _telemetry, 6, 1, mockProvider);

        // Assert
        Assert.True(_drive.HasDisk);
    }

    [Fact]
    public void InsertDisk_ThrowsWithoutFactory()
    {
        // Arrange
        _drive = new DiskIIDrive("TestDrive", _telemetry, 6, 1);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            _drive.InsertDisk("test.dsk"));
    }

    [Fact]
    public void EjectDisk_DisposesProvider()
    {
        // Arrange
        var mockProvider = new MockDiskImageProvider();
        _drive = new DiskIIDrive("TestDrive", _telemetry, 6, 1, mockProvider);
        _telemetry.Clear();

        // Act
        _drive.EjectDisk();

        // Assert
        Assert.False(_drive.HasDisk);
        Assert.True(mockProvider.WasDisposed);
        Assert.True(mockProvider.WasFlushed);
    }


    [Fact]
    public void IsWriteProtected_ReturnsFalse_WhenNoDisk()
    {
        // Arrange
        _drive = new DiskIIDrive("TestDrive", _telemetry, 6, 1);

        // Assert
        Assert.False(_drive.IsWriteProtected());
    }

    [Fact]
    public void IsWriteProtected_DelegatesToProvider()
    {
        // Arrange
        var mockProvider = new MockDiskImageProvider { IsWriteProtected = true };
        _drive = new DiskIIDrive("TestDrive", _telemetry, 6, 1, mockProvider);

        // Assert
        Assert.True(_drive.IsWriteProtected());
    }

    #endregion

    #region Bit Operations Tests

    [Fact]
    public void GetBit_ReturnsNull_WhenMotorOff()
    {
        // Arrange
        var mockProvider = new MockDiskImageProvider();
        _drive = new DiskIIDrive("TestDrive", _telemetry, 6, 1, mockProvider);
        _drive.MotorOn = false;

        // Act
        bool? bit = _drive.GetBit(1000);

        // Assert
        Assert.Null(bit);
    }

    [Fact]
    public void GetBit_ReturnsNull_WhenNoDisk()
    {
        // Arrange
        _drive = new DiskIIDrive("TestDrive", _telemetry, 6, 1);
        _drive.MotorOn = true;

        // Act
        bool? bit = _drive.GetBit(1000);

        // Assert
        Assert.Null(bit);
    }

    [Fact]
    public void GetBit_DelegatesToProvider_WhenMotorOn()
    {
        // Arrange
        var mockProvider = new MockDiskImageProvider { NextBitValue = true };
        _drive = new DiskIIDrive("TestDrive", _telemetry, 6, 1, mockProvider);
        _drive.MotorOn = true;

        // Act
        bool? bit = _drive.GetBit(1000);

        // Assert
        Assert.True(bit);
        Assert.Equal(1000UL, mockProvider.LastGetBitCycle);
    }

    [Fact]
    public void SetBit_ReturnsFalse_WhenMotorOff()
    {
        // Arrange
        var mockProvider = new MockDiskImageProvider();
        _drive = new DiskIIDrive("TestDrive", _telemetry, 6, 1, mockProvider);
        _drive.MotorOn = false;

        // Act
        bool result = _drive.SetBit(true);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void SetBit_ReturnsFalse_WhenNoDisk()
    {
        // Arrange
        _drive = new DiskIIDrive("TestDrive", _telemetry, 6, 1);
        _drive.MotorOn = true;

        // Act
        bool result = _drive.SetBit(true);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void SetBit_DelegatesToProvider_WhenMotorOn()
    {
        // Arrange
        var mockProvider = new MockDiskImageProvider { WriteBitReturnValue = true };
        _drive = new DiskIIDrive("TestDrive", _telemetry, 6, 1, mockProvider);
        _drive.MotorOn = true;

        // Act
        bool result = _drive.SetBit(true);

        // Assert
        Assert.True(result);
        Assert.True(mockProvider.LastWrittenBit);
    }

    #endregion


    #region Helper Classes

    /// <summary>
    /// Mock disk image provider for testing.
    /// </summary>
    private class MockDiskImageProvider : IDiskImageProvider
    {
        public string FilePath => "mock.dsk";
        public bool IsWritable => true;
        public bool IsWriteProtected { get; set; }
        public int CurrentQuarterTrack { get; private set; }
        public bool WasDisposed { get; private set; }
        public bool WasFlushed { get; private set; }
        public bool? NextBitValue { get; set; }
        public ulong LastGetBitCycle { get; private set; }
        public bool LastWrittenBit { get; private set; }
        public bool WriteBitReturnValue { get; set; }

        public void SetQuarterTrack(int qTrack) => CurrentQuarterTrack = qTrack;

        public bool? GetBit(ulong cycleCount)
        {
            LastGetBitCycle = cycleCount;
            return NextBitValue;
        }

        public bool WriteBit(bool bit, ulong cycleCount)
        {
            LastWrittenBit = bit;
            return WriteBitReturnValue;
        }

        public void Flush() => WasFlushed = true;

        public void Dispose() => WasDisposed = true;
    }

    #endregion
}
