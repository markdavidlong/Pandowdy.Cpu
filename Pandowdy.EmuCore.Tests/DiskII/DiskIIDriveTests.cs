// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.DiskII;
using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.Tests.DiskII;

/// <summary>
/// Tests for DiskIIDrive - core drive implementation.
/// </summary>
/// <remarks>
/// <para>
/// After the telemetry removal refactoring (Phase 8C), <see cref="DiskIIDrive"/> is now a
/// pure drive implementation without status publishing. Status updates are handled by
/// <see cref="DiskIIStatusDecorator"/> which wraps the drive.
/// </para>
/// </remarks>
public class DiskIIDriveTests
{
    private DiskIIDrive? _drive;

    #region Constructor Tests

    [Fact]
    public void Constructor_SetsName()
    {
        // Act
        _drive = new DiskIIDrive("TestDrive");

        // Assert
        Assert.Equal("TestDrive", _drive.Name);
    }

    [Fact]
    public void Constructor_WithNullName_SetsUnnamed()
    {
        // Act
        _drive = new DiskIIDrive(null!);

        // Assert
        Assert.Equal("Unnamed", _drive.Name);
    }

    [Fact]
    public void Constructor_InitializesAtTrack17()
    {
        // Act
        _drive = new DiskIIDrive("TestDrive");

        // Assert
        Assert.Equal(17.0, _drive.Track);
        Assert.Equal(68, _drive.QuarterTrack); // 17 * 4
    }

    // PHASE 5: Motor tests removed - motor state is now controller-level, not drive-level
    // Drive only handles mechanical operations (track positioning, disk insertion)

    [Fact]
    public void Constructor_WithImageProvider_SetsQuarterTrack()
    {
        // Arrange
        var mockProvider = new MockDiskImageProvider();

        // Act
        _drive = new DiskIIDrive("TestDrive", mockProvider);

        // Assert - provider should have been notified of initial track
        Assert.Equal(68, mockProvider.CurrentQuarterTrack); // Track 17
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_PreservesHeadPosition()
    {
        // Arrange - start at track 17 (default), step twice to track 17.5
        _drive = new DiskIIDrive("TestDrive");
        Assert.Equal(17.0, _drive.Track); // Verify initial position
        _drive.StepToHigherTrack();
        _drive.StepToHigherTrack();
        Assert.Equal(17.5, _drive.Track); // Verify stepped position

        // Act
        _drive.Reset();

        // Assert - head position should be preserved per interface contract
        Assert.Equal(17.5, _drive.Track);
    }

    // PHASE 5: Reset motor test removed - motor state is controller-level

    #endregion

    // PHASE 5: Motor Tests section removed - motor state is now controller-level
    // Motor control tests should be in DiskIIControllerCardTests instead

    #region Track Stepping Tests

    [Fact]
    public void StepToHigherTrack_IncrementsQuarterTrack()
    {
        // Arrange
        _drive = new DiskIIDrive("TestDrive");
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
        _drive = new DiskIIDrive("TestDrive");
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
        _drive = new DiskIIDrive("TestDrive");

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
        _drive = new DiskIIDrive("TestDrive");

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
        _drive = new DiskIIDrive("TestDrive", mockProvider);
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
        _drive = new DiskIIDrive("TestDrive");

        // Assert
        Assert.False(_drive.HasDisk);
    }

    [Fact]
    public void HasDisk_ReturnsTrue_WhenDiskProvided()
    {
        // Arrange
        var mockProvider = new MockDiskImageProvider();
        _drive = new DiskIIDrive("TestDrive", mockProvider);

        // Assert
        Assert.True(_drive.HasDisk);
    }

    [Fact]
    public void InsertDisk_ThrowsWithoutFactory()
    {
        // Arrange
        _drive = new DiskIIDrive("TestDrive");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            _drive.InsertDisk("test.dsk"));
    }

    [Fact]
    public void EjectDisk_DisposesProvider()
    {
        // Arrange
        var mockProvider = new MockDiskImageProvider();
        _drive = new DiskIIDrive("TestDrive", mockProvider);

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
        _drive = new DiskIIDrive("TestDrive");

        // Assert
        Assert.False(_drive.IsWriteProtected());
    }

    [Fact]
    public void IsWriteProtected_DelegatesToProvider()
    {
        // Arrange
        var mockProvider = new MockDiskImageProvider { IsWriteProtected = true };
        _drive = new DiskIIDrive("TestDrive", mockProvider);

        // Assert
        Assert.True(_drive.IsWriteProtected());
    }

    #endregion

    #region Bit Operations Tests

    // PHASE 5: GetBit_ReturnsNull_WhenMotorOff test removed - motor is controller-level
    // Controller ensures motor is running before calling GetBit()

    [Fact]
    public void GetBit_ReturnsNull_WhenNoDisk()
    {
        // Arrange
        _drive = new DiskIIDrive("TestDrive");
        // PHASE 5: Motor control is controller-level

        // Act
        bool? bit = _drive.GetBit(1000);

        // Assert
        Assert.Null(bit);
    }

    [Fact]
    public void GetBit_DelegatesToProvider()
    {
        // Arrange
        var mockProvider = new MockDiskImageProvider { NextBitValue = true };
        _drive = new DiskIIDrive("TestDrive", mockProvider);
        // PHASE 5: Motor control is controller-level, drive always delegates

        // Act
        bool? bit = _drive.GetBit(1000);

        // Assert
        Assert.True(bit);
        Assert.Equal(1000UL, mockProvider.LastGetBitCycle);
    }

    // PHASE 5: SetBit_ReturnsFalse_WhenMotorOff test removed - motor is controller-level
    // Controller ensures motor is running before calling SetBit()

    [Fact]
    public void SetBit_ReturnsFalse_WhenNoDisk()
    {
        // Arrange
        _drive = new DiskIIDrive("TestDrive");
        // PHASE 5: Motor control is controller-level

        // Act
        bool result = _drive.SetBit(true);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void SetBit_DelegatesToProvider()
    {
        // Arrange
        var mockProvider = new MockDiskImageProvider { WriteBitReturnValue = true };
        _drive = new DiskIIDrive("TestDrive", mockProvider);
        // PHASE 5: Motor control is controller-level, drive always delegates

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
