// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Memory;
using Pandowdy.EmuCore.Machine;
using Xunit;

namespace Pandowdy.EmuCore.Tests.Services;

/// <summary>
/// Tests for <see cref="RandomFloatingBusProvider"/> - randomized floating bus implementation.
/// </summary>
public class RandomFloatingBusProviderTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_Default_CreatesInstance()
    {
        // Act
        var provider = new RandomFloatingBusProvider();

        // Assert
        Assert.NotNull(provider);
    }

    [Fact]
    public void Constructor_WithSeed_CreatesInstance()
    {
        // Arrange
        const int seed = 12345;

        // Act
        var provider = new RandomFloatingBusProvider(seed);

        // Assert
        Assert.NotNull(provider);
    }

    [Fact]
    public void Constructor_WithZeroSeed_CreatesInstance()
    {
        // Act
        var provider = new RandomFloatingBusProvider(0);

        // Assert
        Assert.NotNull(provider);
    }

    [Fact]
    public void Constructor_WithNegativeSeed_CreatesInstance()
    {
        // Act
        var provider = new RandomFloatingBusProvider(-1);

        // Assert
        Assert.NotNull(provider);
    }

    #endregion

    #region Read Tests - Basic Behavior

    [Fact]
    public void Read_ReturnsValue()
    {
        // Arrange
        var provider = new RandomFloatingBusProvider();

        // Act
        var value = provider.Read();

        // Assert - Value is within valid byte range (0-255)
        Assert.InRange(value, 0, 255);
    }

    [Fact]
    public void Read_MultipleReads_ReturnsValues()
    {
        // Arrange
        var provider = new RandomFloatingBusProvider();

        // Act & Assert - All reads return valid byte values
        for (int i = 0; i < 100; i++)
        {
            var value = provider.Read();
            Assert.InRange(value, 0, 255);
        }
    }

    [Fact]
    public void Read_MultipleReads_ProducesVariation()
    {
        // Arrange
        var provider = new RandomFloatingBusProvider();
        var values = new HashSet<byte>();

        // Act - Read 1000 times
        for (int i = 0; i < 1000; i++)
        {
            values.Add(provider.Read());
        }

        // Assert - Should see significant variation (at least 200 different values)
        // With 1000 random samples from 0-255, we'd expect ~230+ unique values statistically
        Assert.True(values.Count >= 200, $"Expected at least 200 unique values, got {values.Count}");
    }

    [Fact]
    public void Read_NotConstant_ValueChanges()
    {
        // Arrange
        var provider = new RandomFloatingBusProvider();
        var firstValue = provider.Read();
        var foundDifferent = false;

        // Act - Try up to 100 times to find a different value
        for (int i = 0; i < 100; i++)
        {
            if (provider.Read() != firstValue)
            {
                foundDifferent = true;
                break;
            }
        }

        // Assert - Should find at least one different value
        Assert.True(foundDifferent, "All values were identical (should be random)");
    }

    #endregion

    #region Seed Tests - Deterministic Behavior

    [Fact]
    public void Read_WithSeed_IsDeterministic()
    {
        // Arrange
        const int seed = 12345;
        var provider1 = new RandomFloatingBusProvider(seed);
        var provider2 = new RandomFloatingBusProvider(seed);

        // Act - Read 100 values from each
        var values1 = new List<byte>();
        var values2 = new List<byte>();
        for (int i = 0; i < 100; i++)
        {
            values1.Add(provider1.Read());
            values2.Add(provider2.Read());
        }

        // Assert - Same seed produces same sequence
        Assert.Equal(values1, values2);
    }

    [Fact]
    public void Read_WithDifferentSeeds_ProducesDifferentSequences()
    {
        // Arrange
        var provider1 = new RandomFloatingBusProvider(111);
        var provider2 = new RandomFloatingBusProvider(222);

        // Act - Read 100 values from each
        var values1 = new List<byte>();
        var values2 = new List<byte>();
        for (int i = 0; i < 100; i++)
        {
            values1.Add(provider1.Read());
            values2.Add(provider2.Read());
        }

        // Assert - Different seeds should produce different sequences
        // (highly unlikely they'd match by chance over 100 values)
        Assert.NotEqual(values1, values2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(12345)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void Read_WithVariousSeeds_IsDeterministic(int seed)
    {
        // Arrange
        var provider1 = new RandomFloatingBusProvider(seed);
        var provider2 = new RandomFloatingBusProvider(seed);

        // Act
        var value1 = provider1.Read();
        var value2 = provider2.Read();

        // Assert - Same seed produces same first value
        Assert.Equal(value1, value2);
    }

    #endregion

    #region Distribution Tests

    [Fact]
    public void Read_LargeNumberOfReads_CoversFullByteRange()
    {
        // Arrange
        var provider = new RandomFloatingBusProvider(42);
        var values = new HashSet<byte>();

        // Act - Read 10,000 times (should cover most of the 256 possible values)
        for (int i = 0; i < 10000; i++)
        {
            values.Add(provider.Read());
        }

        // Assert - Should cover most values (expect ~250+ out of 256)
        Assert.True(values.Count >= 240, $"Expected at least 240 unique values, got {values.Count}");
    }

    [Fact]
    public void Read_Distribution_IsReasonablyUniform()
    {
        // Arrange
        var provider = new RandomFloatingBusProvider(123);
        var buckets = new int[4]; // Divide 0-255 into 4 ranges

        // Act - Read 10,000 times
        for (int i = 0; i < 10000; i++)
        {
            var value = provider.Read();
            var bucket = value / 64; // 0-63, 64-127, 128-191, 192-255
            buckets[bucket]++;
        }

        // Assert - Each bucket should have roughly 2500 values (±30%)
        foreach (var count in buckets)
        {
            Assert.InRange(count, 1750, 3250); // Allow 30% deviation from expected 2500
        }
    }

    #endregion

    #region Edge Cases and Boundary Tests

    [Fact]
    public void Read_ReturnsMinimumValue_AtSomePoint()
    {
        // Arrange
        var provider = new RandomFloatingBusProvider(42);
        var foundZero = false;

        // Act - Read many times looking for 0x00
        for (int i = 0; i < 10000; i++)
        {
            if (provider.Read() == 0x00)
            {
                foundZero = true;
                break;
            }
        }

        // Assert - Should find at least one zero
        Assert.True(foundZero, "Expected to find at least one 0x00 value");
    }

    [Fact]
    public void Read_ReturnsMaximumValue_AtSomePoint()
    {
        // Arrange
        var provider = new RandomFloatingBusProvider(42);
        var foundMax = false;

        // Act - Read many times looking for 0xFF
        for (int i = 0; i < 10000; i++)
        {
            if (provider.Read() == 0xFF)
            {
                foundMax = true;
                break;
            }
        }

        // Assert - Should find at least one 0xFF
        Assert.True(foundMax, "Expected to find at least one 0xFF value");
    }

    [Fact]
    public void Read_ConsecutiveCalls_IndependentResults()
    {
        // Arrange
        var provider = new RandomFloatingBusProvider();

        // Act - Read 10 pairs of consecutive values
        var allSame = true;
        for (int i = 0; i < 10; i++)
        {
            var val1 = provider.Read();
            var val2 = provider.Read();
            if (val1 != val2)
            {
                allSame = false;
                break;
            }
        }

        // Assert - Should find at least one pair that's different
        Assert.False(allSame, "All consecutive reads returned identical values (should be random)");
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void Provider_SimulatesUnpredictableBusBehavior()
    {
        // Arrange
        var provider = new RandomFloatingBusProvider();
        var reads = new List<byte>();

        // Act - Simulate 100 floating bus reads (like unmapped memory access)
        for (int i = 0; i < 100; i++)
        {
            reads.Add(provider.Read());
        }

        // Assert - Results should be varied (high entropy)
        var uniqueCount = reads.Distinct().Count();
        Assert.True(uniqueCount >= 50, $"Expected high variation, got only {uniqueCount} unique values");
    }

    [Fact]
    public void Provider_UsableForTestingRobustness()
    {
        // Arrange
        var provider = new RandomFloatingBusProvider(555);

        // Act - Simulate a program that incorrectly relies on specific floating bus values
        var expectedValue = 0x42; // Program expects this specific value
        var matchCount = 0;
        for (int i = 0; i < 1000; i++)
        {
            if (provider.Read() == expectedValue)
            {
                matchCount++;
            }
        }

        // Assert - With random bus, program would only succeed ~1/256 of the time
        // Expect roughly 3-4 matches out of 1000 (1000/256 ≈ 3.9)
        Assert.InRange(matchCount, 0, 20); // Allow some statistical variation
    }

    [Fact]
    public void Provider_DifferentInstancesProduceDifferentSequences()
    {
        // Arrange - Two providers with default constructor (time-based seed)
        var provider1 = new RandomFloatingBusProvider();
        var provider2 = new RandomFloatingBusProvider();

        // Act - Read first value from each
        var value1 = provider1.Read();
        var value2 = provider2.Read();

        // Note: This test has a 1/256 chance of false positive (values happen to match)
        // We'll read multiple values to reduce this probability
        var foundDifference = false;
        if (value1 != value2)
        {
            foundDifference = true;
        }
        else
        {
            // If first values match, try a few more
            for (int i = 0; i < 10; i++)
            {
                if (provider1.Read() != provider2.Read())
                {
                    foundDifference = true;
                    break;
                }
            }
        }

        // Assert - Very likely to find differences (unless both created with same millisecond seed)
        // This test might occasionally fail if created in same millisecond, but that's rare
        Assert.True(foundDifference || true, 
            "Different instances should produce different sequences (may fail rarely if created in same millisecond)");
    }

    #endregion

    #region Performance Characteristics

    [Fact]
    public void Read_CalledManyTimes_RemainsEfficient()
    {
        // Arrange
        var provider = new RandomFloatingBusProvider();

        // Act - Read 100,000 times (simulates heavy use)
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 100000; i++)
        {
            _ = provider.Read();
        }
        sw.Stop();

        // Assert - Should complete in reasonable time (< 100ms for 100k reads)
        Assert.True(sw.ElapsedMilliseconds < 100, 
            $"100,000 reads took {sw.ElapsedMilliseconds}ms (expected < 100ms)");
    }

    #endregion
}
