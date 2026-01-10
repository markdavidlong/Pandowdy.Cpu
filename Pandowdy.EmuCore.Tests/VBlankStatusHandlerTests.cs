namespace Pandowdy.EmuCore.Tests;

/// <summary>
/// Tests for VBlankStatusHandler - manages VBlank timing state for Apple IIe emulation.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Test Coverage:</strong>
/// <list type="bullet">
/// <item>Initial state validation</item>
/// <item>Counter property behavior</item>
/// <item>InVBlank property logic</item>
/// <item>ResetCounter functionality</item>
/// <item>Counter decrement behavior</item>
/// <item>Boundary conditions (zero crossing)</item>
/// <item>Negative counter values</item>
/// </list>
/// </para>
/// <para>
/// <strong>VBlank Timing Context:</strong> VBlankStatusHandler manages a countdown counter
/// that tracks when the Apple IIe is in vertical blanking (VBlank). The counter is decremented
/// every CPU cycle by VA2MBus and reset to 4,550 cycles when VBlank starts. SystemIoHandler
/// reads the InVBlank property to service $C019 (RD_VERTBLANK_) reads.
/// </para>
/// </remarks>
public class VBlankStatusHandlerTests
{
    #region Initial State Tests (3 tests)

    [Fact]
    public void Constructor_InitializesCounterToZero()
    {
        // Arrange & Act
        var handler = new VBlankStatusHandler();

        // Assert
        Assert.Equal(0, handler.Counter);
    }

    [Fact]
    public void Constructor_InVBlankIsFalseInitially()
    {
        // Arrange & Act
        var handler = new VBlankStatusHandler();

        // Assert
        Assert.False(handler.InVBlank);
    }

    [Fact]
    public void VBlankBlackoutCycles_IsCorrectConstant()
    {
        // Assert
        Assert.Equal(4550, VBlankStatusHandler.VBlankBlackoutCycles);
    }

    #endregion

    #region Counter Property Tests (5 tests)

    [Fact]
    public void Counter_CanBeSetAndRetrieved()
    {
        // Arrange
        var handler = new VBlankStatusHandler();

        // Act
        handler.Counter = 100;

        // Assert
        Assert.Equal(100, handler.Counter);
    }

    [Fact]
    public void Counter_CanBeSetToZero()
    {
        // Arrange
        var handler = new VBlankStatusHandler
        {
            Counter = 100
        };

        // Act
        handler.Counter = 0;

        // Assert
        Assert.Equal(0, handler.Counter);
    }

    [Fact]
    public void Counter_CanBeSetToNegativeValue()
    {
        // Arrange
        var handler = new VBlankStatusHandler();

        // Act
        handler.Counter = -100;

        // Assert
        Assert.Equal(-100, handler.Counter);
    }

    [Fact]
    public void Counter_CanBeDecremented()
    {
        // Arrange
        var handler = new VBlankStatusHandler
        {
            Counter = 100
        };

        // Act
        handler.Counter--;

        // Assert
        Assert.Equal(99, handler.Counter);
    }

    [Fact]
    public void Counter_CanBeIncremented()
    {
        // Arrange
        var handler = new VBlankStatusHandler
        {
            Counter = 100
        };

        // Act
        handler.Counter++;

        // Assert
        Assert.Equal(101, handler.Counter);
    }

    #endregion

    #region InVBlank Property Tests (7 tests)

    [Fact]
    public void InVBlank_ReturnsTrueWhenCounterIsPositive()
    {
        // Arrange
        var handler = new VBlankStatusHandler
        {
            Counter = 1
        };

        // Act
        var inVBlank = handler.InVBlank;

        // Assert
        Assert.True(inVBlank);
    }

    [Fact]
    public void InVBlank_ReturnsFalseWhenCounterIsZero()
    {
        // Arrange
        var handler = new VBlankStatusHandler
        {
            Counter = 0
        };

        // Act
        var inVBlank = handler.InVBlank;

        // Assert
        Assert.False(inVBlank);
    }

    [Fact]
    public void InVBlank_ReturnsFalseWhenCounterIsNegative()
    {
        // Arrange
        var handler = new VBlankStatusHandler
        {
            Counter = -1
        };

        // Act
        var inVBlank = handler.InVBlank;

        // Assert
        Assert.False(inVBlank);
    }

    [Fact]
    public void InVBlank_ReturnsTrueWhenCounterIsMaxValue()
    {
        // Arrange
        var handler = new VBlankStatusHandler
        {
            Counter = VBlankStatusHandler.VBlankBlackoutCycles
        };

        // Act
        var inVBlank = handler.InVBlank;

        // Assert
        Assert.True(inVBlank);
    }

    [Fact]
    public void InVBlank_TransitionsToFalseWhenCounterReachesZero()
    {
        // Arrange
        var handler = new VBlankStatusHandler
        {
            Counter = 1
        };

        // Act - Before decrement
        var inVBlankBefore = handler.InVBlank;
        handler.Counter--;
        var inVBlankAfter = handler.InVBlank;

        // Assert
        Assert.True(inVBlankBefore);
        Assert.False(inVBlankAfter);
    }

    [Fact]
    public void InVBlank_TransitionsToTrueWhenCounterBecomesPositive()
    {
        // Arrange
        var handler = new VBlankStatusHandler
        {
            Counter = 0
        };

        // Act - Before increment
        var inVBlankBefore = handler.InVBlank;
        handler.Counter++;
        var inVBlankAfter = handler.InVBlank;

        // Assert
        Assert.False(inVBlankBefore);
        Assert.True(inVBlankAfter);
    }

    [Fact]
    public void InVBlank_StaysFalseWhenCounterIsDeepNegative()
    {
        // Arrange
        var handler = new VBlankStatusHandler
        {
            Counter = -12480 // Typical negative value before next VBlank
        };

        // Act
        var inVBlank = handler.InVBlank;

        // Assert
        Assert.False(inVBlank);
    }

    #endregion

    #region ResetCounter Tests (4 tests)

    [Fact]
    public void ResetCounter_SetsCounterToVBlankBlackoutCycles()
    {
        // Arrange
        var handler = new VBlankStatusHandler();

        // Act
        handler.ResetCounter();

        // Assert
        Assert.Equal(VBlankStatusHandler.VBlankBlackoutCycles, handler.Counter);
    }

    [Fact]
    public void ResetCounter_MakesInVBlankTrue()
    {
        // Arrange
        var handler = new VBlankStatusHandler
        {
            Counter = 0
        };

        // Act
        handler.ResetCounter();

        // Assert
        Assert.True(handler.InVBlank);
    }

    [Fact]
    public void ResetCounter_CanBeCalledMultipleTimes()
    {
        // Arrange
        var handler = new VBlankStatusHandler();

        // Act
        handler.ResetCounter();
        handler.Counter = 100; // Simulate some cycles passed
        handler.ResetCounter();

        // Assert
        Assert.Equal(VBlankStatusHandler.VBlankBlackoutCycles, handler.Counter);
    }

    [Fact]
    public void ResetCounter_ResetsFromNegativeValue()
    {
        // Arrange
        var handler = new VBlankStatusHandler
        {
            Counter = -12480
        };

        // Act
        handler.ResetCounter();

        // Assert
        Assert.Equal(VBlankStatusHandler.VBlankBlackoutCycles, handler.Counter);
        Assert.True(handler.InVBlank);
    }

    #endregion

    #region Typical Usage Scenario Tests (5 tests)

    [Fact]
    public void TypicalScenario_VBlankCountdown()
    {
        // Arrange - Simulate VBlank starting
        var handler = new VBlankStatusHandler();
        handler.ResetCounter();

        // Act & Assert - Countdown from 4,550 to 0
        Assert.Equal(4550, handler.Counter);
        Assert.True(handler.InVBlank);

        // Simulate 100 cycles
        for (int i = 0; i < 100; i++)
        {
            handler.Counter--;
        }

        Assert.Equal(4450, handler.Counter);
        Assert.True(handler.InVBlank);
    }

    [Fact]
    public void TypicalScenario_VBlankEnds()
    {
        // Arrange - Counter at 1 (last cycle of VBlank)
        var handler = new VBlankStatusHandler
        {
            Counter = 1
        };

        // Act - Decrement to 0 (VBlank ends)
        handler.Counter--;

        // Assert
        Assert.Equal(0, handler.Counter);
        Assert.False(handler.InVBlank);
    }

    [Fact]
    public void TypicalScenario_AfterVBlankEnds_CounterGoesNegative()
    {
        // Arrange - Counter at 0 (VBlank just ended)
        var handler = new VBlankStatusHandler
        {
            Counter = 0
        };

        // Act - Continue decrementing (visible scanlines)
        for (int i = 0; i < 100; i++)
        {
            handler.Counter--;
        }

        // Assert
        Assert.Equal(-100, handler.Counter);
        Assert.False(handler.InVBlank);
    }

    [Fact]
    public void TypicalScenario_FullVBlankCycle()
    {
        // Arrange
        var handler = new VBlankStatusHandler();

        // Act - Simulate full VBlank cycle (4,550 cycles)
        handler.ResetCounter();
        Assert.True(handler.InVBlank);

        for (int i = 0; i < VBlankStatusHandler.VBlankBlackoutCycles; i++)
        {
            handler.Counter--;
        }

        // Assert - VBlank should have ended
        Assert.Equal(0, handler.Counter);
        Assert.False(handler.InVBlank);
    }

    [Fact]
    public void TypicalScenario_MultipleVBlankPeriods()
    {
        // Arrange
        var handler = new VBlankStatusHandler();

        // Act & Assert - Simulate 3 VBlank periods
        for (int period = 0; period < 3; period++)
        {
            // VBlank starts
            handler.ResetCounter();
            Assert.Equal(4550, handler.Counter);
            Assert.True(handler.InVBlank);

            // VBlank active (4,550 cycles)
            for (int i = 0; i < 4550; i++)
            {
                handler.Counter--;
            }
            Assert.Equal(0, handler.Counter);
            Assert.False(handler.InVBlank);

            // Visible scanlines (12,480 cycles - simulate just 100 for test speed)
            for (int i = 0; i < 100; i++)
            {
                handler.Counter--;
            }
            Assert.Equal(-100, handler.Counter);
            Assert.False(handler.InVBlank);
        }
    }

    #endregion

    #region Boundary Condition Tests (5 tests)

    [Fact]
    public void BoundaryCondition_CounterAtOne()
    {
        // Arrange
        var handler = new VBlankStatusHandler
        {
            Counter = 1
        };

        // Assert - Should be in VBlank
        Assert.True(handler.InVBlank);
    }

    [Fact]
    public void BoundaryCondition_CounterCrossesZero()
    {
        // Arrange
        var handler = new VBlankStatusHandler
        {
            Counter = 1
        };

        // Act - Cross zero boundary
        Assert.True(handler.InVBlank);
        handler.Counter--;
        Assert.False(handler.InVBlank);
        handler.Counter--;
        Assert.False(handler.InVBlank);

        // Assert
        Assert.Equal(-1, handler.Counter);
    }

    [Fact]
    public void BoundaryCondition_CounterAtMinusOne()
    {
        // Arrange
        var handler = new VBlankStatusHandler
        {
            Counter = -1
        };

        // Assert - Should NOT be in VBlank
        Assert.False(handler.InVBlank);
    }

    [Fact]
    public void BoundaryCondition_LargePositiveCounter()
    {
        // Arrange
        var handler = new VBlankStatusHandler
        {
            Counter = 100000
        };

        // Assert - Should be in VBlank
        Assert.True(handler.InVBlank);
    }

    [Fact]
    public void BoundaryCondition_LargeNegativeCounter()
    {
        // Arrange
        var handler = new VBlankStatusHandler
        {
            Counter = -100000
        };

        // Assert - Should NOT be in VBlank
        Assert.False(handler.InVBlank);
    }

    #endregion

    #region Integration Simulation Tests (3 tests)

    [Fact]
    public void IntegrationSimulation_VA2MBusDecrements_SystemIoHandlerReads()
    {
        // Arrange - Shared handler (simulating VA2MBus and SystemIoHandler sharing)
        var handler = new VBlankStatusHandler();
        handler.ResetCounter();

        // Act - Simulate VA2MBus Clock() decrementing
        for (int i = 0; i < 100; i++)
        {
            // VA2MBus.Clock()
            handler.Counter--;

            // SystemIoHandler.Read($C019) would check InVBlank
            bool rdVertBlank = handler.InVBlank;
            Assert.True(rdVertBlank); // Should be in VBlank for first 4,550 cycles
        }

        // Assert
        Assert.Equal(4450, handler.Counter);
        Assert.True(handler.InVBlank);
    }

    [Fact]
    public void IntegrationSimulation_VBlankStatusForC019Reads()
    {
        // Arrange - Simulating SystemIoHandler reading $C019
        var handler = new VBlankStatusHandler();

        // Scenario 1: Not in VBlank
        handler.Counter = -100;
        byte rdVertBlank1 = (byte)(handler.InVBlank ? 0x80 : 0x00);
        Assert.Equal(0x00, rdVertBlank1);

        // Scenario 2: In VBlank
        handler.ResetCounter();
        byte rdVertBlank2 = (byte)(handler.InVBlank ? 0x80 : 0x00);
        Assert.Equal(0x80, rdVertBlank2);

        // Scenario 3: VBlank just ended
        handler.Counter = 0;
        byte rdVertBlank3 = (byte)(handler.InVBlank ? 0x80 : 0x00);
        Assert.Equal(0x00, rdVertBlank3);
    }

    [Fact]
    public void IntegrationSimulation_17030CycleFrame()
    {
        // Arrange - Simulate full 17,030 cycle frame
        var handler = new VBlankStatusHandler();
        int inVBlankCycles = 0;
        int notInVBlankCycles = 0;

        // Act - Simulate VBlank starting at cycle 12,480
        // First 12,480 cycles: not in VBlank (counter goes negative)
        handler.Counter = 0;
        for (int i = 0; i < 12480; i++)
        {
            if (handler.InVBlank)
            {
                inVBlankCycles++;
            }
            else
            {
                notInVBlankCycles++;
            }
            handler.Counter--;
        }

        // VBlank starts at cycle 12,480
        handler.ResetCounter();

        // Next 4,550 cycles: in VBlank
        for (int i = 0; i < 4550; i++)
        {
            if (handler.InVBlank)
            {
                inVBlankCycles++;
            }
            else
            {
                notInVBlankCycles++;
            }
            handler.Counter--;
        }

        // Assert
        Assert.Equal(4550, inVBlankCycles);
        Assert.Equal(12480, notInVBlankCycles);
        Assert.Equal(17030, inVBlankCycles + notInVBlankCycles);
    }

    #endregion
}
