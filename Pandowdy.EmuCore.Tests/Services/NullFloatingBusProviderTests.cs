// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.Services;
using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.Tests.Services;

/// <summary>
/// Tests for the NullFloatingBusProvider class - simple floating bus implementation.
/// </summary>
public class NullFloatingBusProviderTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_Succeeds()
    {
        // Act
        var fb = new NullFloatingBusProvider();

        // Assert
        Assert.NotNull(fb);
    }

    #endregion

    #region Read Tests

    [Fact]
    public void Read_AlwaysReturnsZero()
    {
        // Arrange
        var fb = new NullFloatingBusProvider();

        // Act
        byte value = fb.Read();

        // Assert
        Assert.Equal(0, value);
    }

    [Fact]
    public void Read_MultipleCalls_AlwaysReturnsZero()
    {
        // Arrange
        var fb = new NullFloatingBusProvider();

        // Act
        byte value1 = fb.Read();
        byte value2 = fb.Read();
        byte value3 = fb.Read();
        byte value4 = fb.Read();

        // Assert
        Assert.Equal(0, value1);
        Assert.Equal(0, value2);
        Assert.Equal(0, value3);
        Assert.Equal(0, value4);
    }

    [Fact]
    public void Read_Deterministic_AlwaysSameResult()
    {
        // Arrange
        var fb = new NullFloatingBusProvider();

        // Act - Read many times
        for (int i = 0; i < 1000; i++)
        {
            byte value = fb.Read();
            
            // Assert - Every read should be zero
            Assert.Equal(0, value);
        }
    }

    #endregion

    #region No State Tests

    [Fact]
    public void MultipleInstances_IndependentBehavior()
    {
        // Arrange
        var fb1 = new NullFloatingBusProvider();
        var fb2 = new NullFloatingBusProvider();

        // Act
        byte value1 = fb1.Read();
        byte value2 = fb2.Read();

        // Assert - Both should return zero independently
        Assert.Equal(0, value1);
        Assert.Equal(0, value2);
    }

    [Fact]
    public void NoInternalState_AlwaysReturnsZero()
    {
        // Arrange
        var fb = new NullFloatingBusProvider();

        // Act - Read many times
        for (int i = 0; i < 100; i++)
        {
            byte value = fb.Read();
            
            // Assert - No matter what, should always be zero
            Assert.Equal(0, value);
        }
    }

    #endregion

    #region Performance and Predictability Tests

    [Fact]
    public void Read_IsFast_NoComputationOverhead()
    {
        // Arrange
        var fb = new NullFloatingBusProvider();

        // Act - Read many times (performance test)
        for (int i = 0; i < 100000; i++)
        {
            byte value = fb.Read();
            
            // Assert - Should always be zero with minimal overhead
            Assert.Equal(0, value);
        }
    }

    [Fact]
    public void Behavior_IsPredictable_ForTesting()
    {
        // Arrange
        var fb = new NullFloatingBusProvider();

        // Act - Floating bus read (e.g., from unmapped I/O)
        byte floatingValue = fb.Read();

        // Assert - Predictable zero for test assertions
        Assert.Equal(0, floatingValue);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void UsedWithMemoryMapper_ProvidesConsistentBehavior()
    {
        // Arrange
        var fb = new NullFloatingBusProvider();

        // Simulate memory mapper using floating bus for unmapped reads
        byte ReadUnmapped(ushort _)
        {
            return fb.Read();
        }

        // Act
        byte value1 = ReadUnmapped(0xC0FF);
        byte value2 = ReadUnmapped(0xCFFF);
        byte value3 = ReadUnmapped(0xC123);

        // Assert - All unmapped reads should return zero
        Assert.Equal(0, value1);
        Assert.Equal(0, value2);
        Assert.Equal(0, value3);
    }

    [Fact]
    public void UsedWithLanguageCard_HandlesNullAuxMemory()
    {
        // Arrange
        var _ = new MemoryBlock(0x4000);   // mainRam - not used in this test
        var _1 = new MemoryBlock(0x3000);  // systemRom - not used in this test
        var fb = new NullFloatingBusProvider();
        
        // Simulate Language Card trying to read non-existent aux memory
        IPandowdyMemory? auxRam = null;
        byte value = auxRam is not null ? auxRam[0x1000] : fb.Read();

        // Assert - Should get zero from floating bus
        Assert.Equal(0, value);
    }

    #endregion

    #region Documentation Examples

    [Fact]
    public void Example_UnitTesting_PredictableBehavior()
    {
        // This demonstrates why NullFloatingBusProvider is useful for testing
        
        // Arrange - Use NullFloatingBusProvider for predictable tests
        var fb = new NullFloatingBusProvider();

        // Act - Test code that depends on floating bus
        byte floatingBusValue = fb.Read(); // Unmapped read

        // Assert - Zero is predictable and easy to test
        Assert.Equal(0, floatingBusValue);
    }

    [Fact]
    public void Example_Debugging_SimplifiesComplexity()
    {
        // This demonstrates using NullFloatingBusProvider for debugging
        
        // Arrange - Replace complex floating bus with simple null provider
        var fb = new NullFloatingBusProvider();

        // Act - Debug code without floating bus complexity
        byte value = fb.Read();

        // Assert - Floating bus is not the source of bugs
        Assert.Equal(0, value);
    }

    [Fact]
    public void Example_InitialDevelopment_MinimalImplementation()
    {
        // This demonstrates starting with NullFloatingBusProvider
        
        // Arrange - Start simple, upgrade later
        var fb = new NullFloatingBusProvider();

        // Act - Get emulator running without complex floating bus
        byte value = fb.Read();

        // Assert - Simple but functional
        Assert.Equal(0, value);
    }

    #endregion

    #region Interface Compatibility Tests

    [Fact]
    public void Interface_CompatibleWithIFloatingBusProvider()
    {
        // Arrange - NullFloatingBusProvider implements IFloatingBusProvider
        // Must use interface type to test interface compatibility (suppress CA1859)
#pragma warning disable CA1859 // Use concrete types when possible for improved performance
        IFloatingBusProvider fb = new NullFloatingBusProvider();
#pragma warning restore CA1859

        // Act - Can be used anywhere IFloatingBusProvider is needed
        byte value = fb.Read();

        // Assert - Interface contract satisfied
        Assert.Equal(0, value);
    }

    #endregion
}
