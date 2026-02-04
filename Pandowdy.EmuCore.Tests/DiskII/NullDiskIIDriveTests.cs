// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.DiskII;

namespace Pandowdy.EmuCore.Tests.DiskII;

/// <summary>
/// Tests for NullDiskIIDrive - null object pattern implementation.
/// </summary>
public class NullDiskIIDriveTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithDefaultName_SetsNameToNullDrive()
    {
        // Act
        var drive = new NullDiskIIDrive();

        // Assert
        Assert.Equal("NullDrive", drive.Name);
    }

    [Fact]
    public void Constructor_WithCustomName_SetsName()
    {
        // Act
        var drive = new NullDiskIIDrive("TestDrive");

        // Assert
        Assert.Equal("TestDrive", drive.Name);
    }

    [Fact]
    public void Constructor_InitializesAtTrack17()
    {
        // Act
        var drive = new NullDiskIIDrive();

        // Assert - track 17 is common boot track
        Assert.Equal(17.0, drive.Track);
        Assert.Equal(68, drive.QuarterTrack); // 17 * 4 = 68
    }

    [Fact]
    public void Constructor_InitializesMotorOff()
    {
        // Act
        var drive = new NullDiskIIDrive();

        // Assert
        Assert.False(drive.MotorOn);
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_RestoresTrackTo17()
    {
        // Arrange
        var drive = new NullDiskIIDrive();
        drive.StepToHigherTrack();
        drive.StepToHigherTrack();

        // Act
        drive.Reset();

        // Assert
        Assert.Equal(17.0, drive.Track);
    }

    [Fact]
    public void Reset_TurnsMotorOff()
    {
        // Arrange
        var drive = new NullDiskIIDrive
        {
            MotorOn = true
        };

        // Act
        drive.Reset();

        // Assert
        Assert.False(drive.MotorOn);
    }

    #endregion

    #region Motor Tests

    [Fact]
    public void MotorOn_CanBeSetToTrue()
    {
        // Arrange
        var drive = new NullDiskIIDrive
        {
            // Act
            MotorOn = true
        };

        // Assert
        Assert.True(drive.MotorOn);
    }

    [Fact]
    public void MotorOn_CanBeSetToFalse()
    {
        // Arrange
        var drive = new NullDiskIIDrive
        {
            MotorOn = true
        };

        // Act
        drive.MotorOn = false;

        // Assert
        Assert.False(drive.MotorOn);
    }

    #endregion

    #region Track Stepping Tests

    [Fact]
    public void StepToHigherTrack_IncrementsQuarterTrack()
    {
        // Arrange
        var drive = new NullDiskIIDrive();
        int initialQuarterTrack = drive.QuarterTrack;

        // Act
        drive.StepToHigherTrack();

        // Assert
        Assert.Equal(initialQuarterTrack + 1, drive.QuarterTrack);
    }

    [Fact]
    public void StepToLowerTrack_DecrementsQuarterTrack()
    {
        // Arrange
        var drive = new NullDiskIIDrive();
        int initialQuarterTrack = drive.QuarterTrack;

        // Act
        drive.StepToLowerTrack();

        // Assert
        Assert.Equal(initialQuarterTrack - 1, drive.QuarterTrack);
    }

    [Fact]
    public void StepToHigherTrack_ClampsAtMaxQuarterTracks()
    {
        // Arrange
        var drive = new NullDiskIIDrive();

        // Act - step way beyond max
        for (int i = 0; i < 200; i++)
        {
            drive.StepToHigherTrack();
        }

        // Assert - should be clamped at MaxQuarterTracks (140)
        Assert.Equal(DiskIIConstants.MaxQuarterTracks, drive.QuarterTrack);
    }

    [Fact]
    public void StepToLowerTrack_ClampsAtZero()
    {
        // Arrange
        var drive = new NullDiskIIDrive();

        // Act - step way below zero
        for (int i = 0; i < 200; i++)
        {
            drive.StepToLowerTrack();
        }

        // Assert - should be clamped at 0
        Assert.Equal(0, drive.QuarterTrack);
    }

    [Fact]
    public void Track_ReturnsQuarterTrackDividedBy4()
    {
        // Arrange
        var drive = new NullDiskIIDrive();
        drive.Reset();

        // Step to quarter track 70 (track 17.5)
        drive.StepToHigherTrack();
        drive.StepToHigherTrack();

        // Assert
        Assert.Equal(70, drive.QuarterTrack);
        Assert.Equal(17.5, drive.Track);
    }

    #endregion

    #region Disk Operations Tests

    [Fact]
    public void HasDisk_AlwaysReturnsFalse()
    {
        // Arrange
        var drive = new NullDiskIIDrive();

        // Assert
        Assert.False(drive.HasDisk);
    }

    [Fact]
    public void InsertDisk_DoesNotThrow()
    {
        // Arrange
        var drive = new NullDiskIIDrive();

        // Act & Assert - should not throw
        drive.InsertDisk("test.dsk");
        Assert.False(drive.HasDisk); // Still no disk
    }

    [Fact]
    public void EjectDisk_DoesNotThrow()
    {
        // Arrange
        var drive = new NullDiskIIDrive();

        // Act & Assert - should not throw
        drive.EjectDisk();
    }

    [Fact]
    public void IsWriteProtected_AlwaysReturnsFalse()
    {
        // Arrange
        var drive = new NullDiskIIDrive();

        // Assert
        Assert.False(drive.IsWriteProtected());
    }

    #endregion

    #region Bit Operations Tests

    [Fact]
    public void GetBit_AlwaysReturnsNull()
    {
        // Arrange
        var drive = new NullDiskIIDrive
        {
            MotorOn = true
        };

        // Act
        bool? bit = drive.GetBit(12345);

        // Assert
        Assert.Null(bit);
    }

    [Fact]
    public void GetBit_ReturnsNull_AtDifferentCycles()
    {
        // Arrange
        var drive = new NullDiskIIDrive();

        // Act & Assert - always null regardless of cycle
        Assert.Null(drive.GetBit(0));
        Assert.Null(drive.GetBit(1000000));
        Assert.Null(drive.GetBit(ulong.MaxValue));
    }

    [Fact]
    public void SetBit_AlwaysReturnsFalse()
    {
        // Arrange
        var drive = new NullDiskIIDrive();

        // Act & Assert
        Assert.False(drive.SetBit(true));
        Assert.False(drive.SetBit(false));
    }

    [Fact]
    public void BitPosition_AlwaysReturnsZero()
    {
        // Assert
        Assert.Equal(0, NullDiskIIDrive.BitPosition);
    }

    #endregion
}
