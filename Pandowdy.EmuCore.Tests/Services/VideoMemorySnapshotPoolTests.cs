// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.IO;
using Pandowdy.EmuCore.Video;
using Pandowdy.EmuCore.Machine;
using Xunit;

namespace Pandowdy.EmuCore.Tests.Services;

/// <summary>
/// Tests for <see cref="VideoMemorySnapshotPool"/> - object pool for VideoMemorySnapshot instances.
/// </summary>
public class VideoMemorySnapshotPoolTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_Default_CreatesPool()
    {
        // Act
        var pool = new VideoMemorySnapshotPool();

        // Assert
        Assert.NotNull(pool);
    }

    [Fact]
    public void Constructor_WithMaxPoolSize_CreatesPool()
    {
        // Act
        var pool = new VideoMemorySnapshotPool(maxPoolSize: 4);

        // Assert
        Assert.NotNull(pool);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    public void Constructor_WithVariousMaxPoolSizes_CreatesPool(int maxPoolSize)
    {
        // Act
        var pool = new VideoMemorySnapshotPool(maxPoolSize);

        // Assert
        Assert.NotNull(pool);
    }

    [Fact]
    public void Constructor_WithZeroMaxPoolSize_CreatesPool()
    {
        // Act - Zero max size means pool never retains items
        var pool = new VideoMemorySnapshotPool(0);

        // Assert
        Assert.NotNull(pool);
    }

    #endregion

    #region Rent Tests - Basic Behavior

    [Fact]
    public void Rent_EmptyPool_CreatesNewSnapshot()
    {
        // Arrange
        var pool = new VideoMemorySnapshotPool();

        // Act
        var snapshot = pool.Rent();

        // Assert
        Assert.NotNull(snapshot);
        Assert.NotNull(snapshot.MainRam);
        Assert.NotNull(snapshot.AuxRam);
        Assert.Equal(0xC000, snapshot.MainRam.Length); // 48KB
        Assert.Equal(0xC000, snapshot.AuxRam.Length);  // 48KB
    }

    [Fact]
    public void Rent_EmptyPool_SnapshotIsCleared()
    {
        // Arrange
        var pool = new VideoMemorySnapshotPool();

        // Act
        var snapshot = pool.Rent();

        // Assert - New snapshot should be zeroed
        Assert.All(snapshot.MainRam, b => Assert.Equal(0, b));
        Assert.All(snapshot.AuxRam, b => Assert.Equal(0, b));
        Assert.Null(snapshot.SoftSwitches);
        Assert.Equal(0UL, snapshot.FrameNumber);
    }

    [Fact]
    public void Rent_MultipleTimes_CreatesMultipleSnapshots()
    {
        // Arrange
        var pool = new VideoMemorySnapshotPool();

        // Act
        var snapshot1 = pool.Rent();
        var snapshot2 = pool.Rent();
        var snapshot3 = pool.Rent();

        // Assert - All different instances
        Assert.NotNull(snapshot1);
        Assert.NotNull(snapshot2);
        Assert.NotNull(snapshot3);
        Assert.NotSame(snapshot1, snapshot2);
        Assert.NotSame(snapshot1, snapshot3);
        Assert.NotSame(snapshot2, snapshot3);
    }

    #endregion

    #region Return Tests - Basic Behavior

    [Fact]
    public void Return_WithSnapshot_DoesNotThrow()
    {
        // Arrange
        var pool = new VideoMemorySnapshotPool();
        var snapshot = pool.Rent();

        // Act & Assert
        var exception = Record.Exception(() => pool.Return(snapshot));
        Assert.Null(exception);
    }

    [Fact]
    public void Return_WithNull_DoesNotThrow()
    {
        // Arrange
        var pool = new VideoMemorySnapshotPool();

        // Act & Assert
        var exception = Record.Exception(() => pool.Return(null));
        Assert.Null(exception);
    }

    [Fact]
    public void Return_WithMultipleNulls_DoesNotThrow()
    {
        // Arrange
        var pool = new VideoMemorySnapshotPool();

        // Act & Assert
        pool.Return(null);
        pool.Return(null);
        pool.Return(null);
        // No exception expected
    }

    #endregion

    #region Rent/Return Cycle Tests - Reuse Behavior

    [Fact]
    public void RentReturnRent_ReusesSnapshot()
    {
        // Arrange
        var pool = new VideoMemorySnapshotPool();

        // Act
        var snapshot1 = pool.Rent();
        pool.Return(snapshot1);
        var snapshot2 = pool.Rent();

        // Assert - Same instance reused
        Assert.Same(snapshot1, snapshot2);
    }

    [Fact]
    public void RentReturnRent_SnapshotIsCleared()
    {
        // Arrange
        var pool = new VideoMemorySnapshotPool();

        // Act
        var snapshot1 = pool.Rent();
        
        // Modify the snapshot
        snapshot1.MainRam[0] = 0x42;
        snapshot1.MainRam[100] = 0xFF;
        snapshot1.AuxRam[0] = 0xAA;
        snapshot1.AuxRam[100] = 0xBB;
        snapshot1.FrameNumber = 123;

        pool.Return(snapshot1);
        var snapshot2 = pool.Rent();

        // Assert - Reused snapshot should be cleared
        Assert.Same(snapshot1, snapshot2);
        Assert.Equal(0, snapshot2.MainRam[0]);
        Assert.Equal(0, snapshot2.MainRam[100]);
        Assert.Equal(0, snapshot2.AuxRam[0]);
        Assert.Equal(0, snapshot2.AuxRam[100]);
        Assert.Equal(0UL, snapshot2.FrameNumber);
        Assert.Null(snapshot2.SoftSwitches);
    }

    [Fact]
    public void RentReturnRent_MultipleSnapshots_AllReused()
    {
        // Arrange
        var pool = new VideoMemorySnapshotPool(maxPoolSize: 4);

        // Act - Rent 3 snapshots
        var snapshot1 = pool.Rent();
        var snapshot2 = pool.Rent();
        var snapshot3 = pool.Rent();

        // Return all 3
        pool.Return(snapshot1);
        pool.Return(snapshot2);
        pool.Return(snapshot3);

        // Rent 3 again
        var reused1 = pool.Rent();
        var reused2 = pool.Rent();
        var reused3 = pool.Rent();

        // Assert - All reused (order may vary due to ConcurrentBag LIFO behavior)
        var originalSet = new HashSet<VideoMemorySnapshot> { snapshot1, snapshot2, snapshot3 };
        Assert.Contains(reused1, originalSet);
        Assert.Contains(reused2, originalSet);
        Assert.Contains(reused3, originalSet);
    }

    #endregion

    #region Pool Capacity Tests

    [Fact]
    public void Return_BeyondMaxPoolSize_DiscardsExcess()
    {
        // Arrange
        var pool = new VideoMemorySnapshotPool(maxPoolSize: 2);

        // Act - Rent 3 snapshots and return all
        var snapshot1 = pool.Rent();
        var snapshot2 = pool.Rent();
        var snapshot3 = pool.Rent();

        pool.Return(snapshot1);
        pool.Return(snapshot2);
        pool.Return(snapshot3); // This one should be discarded (pool full)

        // Rent 2 - should get the pooled ones
        var reused1 = pool.Rent();
        var reused2 = pool.Rent();

        // Rent 3rd - should create new (3rd was discarded)
        var new3 = pool.Rent();

        // Assert
        Assert.True(ReferenceEquals(snapshot1, reused1) || ReferenceEquals(snapshot1, reused2));
        Assert.True(ReferenceEquals(snapshot2, reused1) || ReferenceEquals(snapshot2, reused2));
        Assert.NotSame(snapshot3, new3); // 3rd was discarded, so we get a new one
    }

    [Fact]
    public void Return_WithZeroMaxPoolSize_DiscardsAll()
    {
        // Arrange
        var pool = new VideoMemorySnapshotPool(0);

        // Act
        var snapshot1 = pool.Rent();
        pool.Return(snapshot1);
        var snapshot2 = pool.Rent();

        // Assert - With maxPoolSize=0, nothing is pooled
        Assert.NotSame(snapshot1, snapshot2);
    }

    [Fact]
    public void Return_ExactlyAtMaxPoolSize_PoolsAll()
    {
        // Arrange
        var pool = new VideoMemorySnapshotPool(3);

        // Act - Rent and return exactly maxPoolSize snapshots
        var snapshot1 = pool.Rent();
        var snapshot2 = pool.Rent();
        var snapshot3 = pool.Rent();

        pool.Return(snapshot1);
        pool.Return(snapshot2);
        pool.Return(snapshot3);

        // Rent again
        var reused1 = pool.Rent();
        var reused2 = pool.Rent();
        var reused3 = pool.Rent();

        // Assert - All should be reused
        var originalSet = new HashSet<VideoMemorySnapshot> { snapshot1, snapshot2, snapshot3 };
        Assert.Contains(reused1, originalSet);
        Assert.Contains(reused2, originalSet);
        Assert.Contains(reused3, originalSet);
    }

    #endregion

    #region Clearing Behavior Tests

    [Fact]
    public void Rent_AfterReturn_ClearsMainRamCompletely()
    {
        // Arrange
        var pool = new VideoMemorySnapshotPool();
        var snapshot = pool.Rent();

        // Fill with non-zero data
        for (int i = 0; i < snapshot.MainRam.Length; i++)
        {
            snapshot.MainRam[i] = (byte)(i % 256);
        }

        // Act
        pool.Return(snapshot);
        var reused = pool.Rent();

        // Assert - All zeroed
        Assert.All(reused.MainRam, b => Assert.Equal(0, b));
    }

    [Fact]
    public void Rent_AfterReturn_ClearsAuxRamCompletely()
    {
        // Arrange
        var pool = new VideoMemorySnapshotPool();
        var snapshot = pool.Rent();

        // Fill with non-zero data
        for (int i = 0; i < snapshot.AuxRam.Length; i++)
        {
            snapshot.AuxRam[i] = (byte)((i + 128) % 256);
        }

        // Act
        pool.Return(snapshot);
        var reused = pool.Rent();

        // Assert - All zeroed
        Assert.All(reused.AuxRam, b => Assert.Equal(0, b));
    }

    [Fact]
    public void Rent_AfterReturn_ClearsFrameNumber()
    {
        // Arrange
        var pool = new VideoMemorySnapshotPool();
        var snapshot = pool.Rent();
        snapshot.FrameNumber = 999999;

        // Act
        pool.Return(snapshot);
        var reused = pool.Rent();

        // Assert
        Assert.Equal(0UL, reused.FrameNumber);
    }

    [Fact]
    public void Rent_AfterReturn_ClearsSoftSwitches()
    {
        // Arrange
        var pool = new VideoMemorySnapshotPool();
        var snapshot = pool.Rent();

        // Create a SystemStatusSnapshot with all defaults
        snapshot.SoftSwitches = new SystemStatusSnapshot(
            State80Store: false, StateRamRd: false, StateRamWrt: false, StateIntCxRom: false,
            StateIntC8Rom: false, StateAltZp: false, StateSlotC3Rom: false, StatePb0: false,
            StatePb1: false, StatePb2: false, StateAnn0: false, StateAnn1: false,
            StateAnn2: false, StateAnn3_DGR: false, StatePage2: false, StateHiRes: false,
            StateMixed: false, StateTextMode: false, StateShow80Col: false, StateAltCharSet: false,
            StateFlashOn: false, StatePrewrite: false, StateUseBank1: false, StateHighRead: false,
            StateHighWrite: false, StateVBlank: false, StatePdl0: 0, StatePdl1: 0,
            StatePdl2: 0, StatePdl3: 0, StateIntC8RomSlot: 0, StateCurrentMhz: 1.0
        );

        // Act
        pool.Return(snapshot);
        var reused = pool.Rent();

        // Assert
        Assert.Null(reused.SoftSwitches);
    }

    #endregion

    #region Multiple Return Tests

    [Fact]
    public void Return_SameSnapshotTwice_AddsToPoolTwice()
    {
        // Arrange
        var pool = new VideoMemorySnapshotPool(maxPoolSize: 4);
        var snapshot = pool.Rent();

        // Act - Return same snapshot twice (unusual but should handle)
        pool.Return(snapshot);
        pool.Return(snapshot);

        // Rent twice - might get same instance twice
        var reused1 = pool.Rent();
        var reused2 = pool.Rent();

        // Assert - Both rents should succeed (may or may not be same instance)
        Assert.NotNull(reused1);
        Assert.NotNull(reused2);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void Pool_TypicalUsagePattern_WorksCorrectly()
    {
        // Arrange
        var pool = new VideoMemorySnapshotPool(8);

        // Act - Simulate typical rendering cycle
        var snapshots = new List<VideoMemorySnapshot>();
        
        // Rent 5 snapshots for rendering
        for (int i = 0; i < 5; i++)
        {
            var snapshot = pool.Rent();
            snapshot.FrameNumber = (ulong)i;
            snapshot.MainRam[0] = (byte)i;
            snapshots.Add(snapshot);
        }

        // Return all after rendering complete
        foreach (var snapshot in snapshots)
        {
            pool.Return(snapshot);
        }

        // Rent 5 again for next cycle
        var reusedSnapshots = new List<VideoMemorySnapshot>();
        for (int i = 0; i < 5; i++)
        {
            var snapshot = pool.Rent();
            Assert.Equal(0UL, snapshot.FrameNumber); // Cleared
            Assert.Equal(0, snapshot.MainRam[0]);    // Cleared
            reusedSnapshots.Add(snapshot);
        }

        // Assert - All reused
        foreach (var reused in reusedSnapshots)
        {
            Assert.Contains(reused, snapshots);
        }
    }

    [Fact]
    public void Pool_HighFrequencyRentReturn_RemainsStable()
    {
        // Arrange
        var pool = new VideoMemorySnapshotPool(4);

        // Act - Simulate high-frequency rent/return (like 60 FPS rendering)
        for (int frame = 0; frame < 100; frame++)
        {
            var snapshot = pool.Rent();
            snapshot.FrameNumber = (ulong)frame;
            pool.Return(snapshot);
        }

        // Assert - Pool should still work correctly
        var finalSnapshot = pool.Rent();
        Assert.NotNull(finalSnapshot);
        Assert.Equal(0UL, finalSnapshot.FrameNumber);
    }

    [Fact]
    public void Pool_MixedRentReturnPatterns_HandlesCorrectly()
    {
        // Arrange
        var pool = new VideoMemorySnapshotPool(4);

        // Act - Various patterns
        var s1 = pool.Rent();
        var s2 = pool.Rent();
        
        pool.Return(s1);
        
        var s3 = pool.Rent(); // Reuses s1
        
        pool.Return(s2);
        pool.Return(s3);
        
        var s4 = pool.Rent();
        var s5 = pool.Rent();

        // Assert - No crashes, all work correctly
        Assert.NotNull(s4);
        Assert.NotNull(s5);
        Assert.All(s4.MainRam, b => Assert.Equal(0, b));
        Assert.All(s5.MainRam, b => Assert.Equal(0, b));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Pool_LargeMaxPoolSize_WorksCorrectly()
    {
        // Arrange
        var pool = new VideoMemorySnapshotPool(100);

        // Act - Rent and return many snapshots
        var snapshots = new List<VideoMemorySnapshot>();
        for (int i = 0; i < 50; i++)
        {
            snapshots.Add(pool.Rent());
        }

        foreach (var snapshot in snapshots)
        {
            pool.Return(snapshot);
        }

        // Rent again
        var reused = pool.Rent();

        // Assert - Should reuse one of the returned snapshots
        Assert.Contains(reused, snapshots);
    }

    [Fact]
    public void Pool_ReturnWithoutRent_WorksCorrectly()
    {
        // Arrange
        var pool = new VideoMemorySnapshotPool();
        var externalSnapshot = new VideoMemorySnapshot();

        // Act - Return snapshot not rented from this pool
        pool.Return(externalSnapshot);

        // Rent - should get the external snapshot we returned
        var rented = pool.Rent();

        // Assert
        Assert.Same(externalSnapshot, rented);
    }

    #endregion

    #region Performance Characteristics

    [Fact]
    public void Pool_ReducesAllocations_ComparedToAlwaysCreatingNew()
    {
        // Arrange
        var pool = new VideoMemorySnapshotPool(4);

        // Act - Rent and return same snapshots repeatedly
        var rentedSnapshots = new HashSet<VideoMemorySnapshot>();
        
        for (int cycle = 0; cycle < 20; cycle++)
        {
            var snapshot = pool.Rent();
            rentedSnapshots.Add(snapshot);
            pool.Return(snapshot);
        }

        // Assert - Should have created far fewer than 20 snapshots (ideally 1)
        Assert.True(rentedSnapshots.Count <= 4, 
            $"Expected <= 4 unique snapshots (pool reuse), got {rentedSnapshots.Count}");
    }

    #endregion

    #region Thread Safety Notes

    // Note: Thread safety is provided by ConcurrentBag, but explicit multi-threaded
    // tests are not included here as they would require complex synchronization
    // and would be non-deterministic. The use of ConcurrentBag ensures thread-safe
    // operations at the implementation level.

    #endregion
}
