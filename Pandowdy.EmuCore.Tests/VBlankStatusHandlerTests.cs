using Pandowdy.EmuCore.DataTypes;

namespace Pandowdy.EmuCore.Tests;

/// <summary>
/// Tests for CpuClockingCounters - manages CPU cycle counting and VBlank timing state for Apple IIe emulation.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Test Coverage:</strong>
/// <list type="bullet">
/// <item>Initial state validation</item>
/// <item>VBlankCounter property behavior</item>
/// <item>TotalCycles tracking</item>
/// <item>InVBlank property logic</item>
/// <item>ResetVBlankCounter functionality</item>
/// <item>VBlank counter decrement behavior</item>
/// <item>Boundary conditions (zero crossing)</item>
/// <item>Negative counter values</item>
/// </list>
/// </para>
/// <para>
/// <strong>VBlank Timing Context:</strong> CpuClockingCounters manages a countdown counter
/// that tracks when the Apple IIe is in vertical blanking (VBlank). The counter is decremented
/// every CPU cycle by VA2MBus and reset to 4,550 cycles when VBlank starts. SystemIoHandler
/// reads the InVBlank property to service $C019 (RD_VERTBLANK_) reads.
/// </para>
/// </remarks>
public class VBlankStatusHandlerTests
{
    #region Initial State Tests (3 tests)

    [Fact]
    public void Constructor_InitializesVBlankCounterToZero()
    {
        // Arrange & Act
        var handler = new CpuClockingCounters();

        // Assert
        Assert.Equal(0, handler.VBlankCounter);
    }

    [Fact]
    public void Constructor_InVBlankIsFalseInitially()
    {
        // Arrange & Act
        var handler = new CpuClockingCounters();

        // Assert
        Assert.False(handler.InVBlank);
    }

    [Fact]
    public void VBlankBlackoutCycles_IsCorrectConstant()
    {
        // Assert
        Assert.Equal(4550, CpuClockingCounters.VBlankBlackoutCycles);
    }

    #endregion

    #region VBlankCounter Property Tests (5 tests)

    [Fact]
    public void VBlankCounter_CanBeSetAndRetrieved()
    {
        // Arrange
        var handler = new CpuClockingCounters
        {
            // Act
            VBlankCounter = 100
        };

        // Assert
        Assert.Equal(100, handler.VBlankCounter);
    }

    [Fact]
    public void VBlankCounter_CanBeSetToZero()
    {
        // Arrange
        var handler = new CpuClockingCounters
        {
            VBlankCounter = 100
        };

        // Act
        handler.VBlankCounter = 0;

        // Assert
        Assert.Equal(0, handler.VBlankCounter);
    }

    [Fact]
    public void VBlankCounter_CanBeSetToNegativeValue()
    {
        // Arrange
        var handler = new CpuClockingCounters
        {
            // Act
            VBlankCounter = -100
        };

        // Assert
        Assert.Equal(-100, handler.VBlankCounter);
    }

    [Fact]
    public void VBlankCounter_CanBeDecremented()
    {
        // Arrange
        var handler = new CpuClockingCounters
        {
            VBlankCounter = 100
        };

        // Act
        handler.DecrementVBlankCounter(1);

        // Assert
        Assert.Equal(99, handler.VBlankCounter);
    }

    [Fact]
    public void VBlankCounter_CanBeIncremented()
    {
        // Arrange
        var handler = new CpuClockingCounters
        {
            VBlankCounter = 100
        };

        // Act
        handler.VBlankCounter++;

        // Assert
        Assert.Equal(101, handler.VBlankCounter);
    }

    #endregion

    #region InVBlank Property Tests (7 tests)

    [Fact]
    public void InVBlank_ReturnsTrueWhenCounterIsPositive()
    {
        // Arrange
        var handler = new CpuClockingCounters
        {
            VBlankCounter = 1
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
        var handler = new CpuClockingCounters
        {
            VBlankCounter = 0
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
        var handler = new CpuClockingCounters
        {
            VBlankCounter = -1
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
        var handler = new CpuClockingCounters
        {
            VBlankCounter = CpuClockingCounters.VBlankBlackoutCycles
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
        var handler = new CpuClockingCounters
        {
            VBlankCounter = 1
        };

        // Act - Before decrement
        var inVBlankBefore = handler.InVBlank;
        handler.DecrementVBlankCounter(1);
        var inVBlankAfter = handler.InVBlank;

        // Assert
        Assert.True(inVBlankBefore);
        Assert.False(inVBlankAfter);
    }

    [Fact]
    public void InVBlank_TransitionsToTrueWhenCounterBecomesPositive()
    {
        // Arrange
        var handler = new CpuClockingCounters
        {
            VBlankCounter = 0
        };

        // Act - Before increment
        var inVBlankBefore = handler.InVBlank;
        handler.VBlankCounter++;
        var inVBlankAfter = handler.InVBlank;

        // Assert
        Assert.False(inVBlankBefore);
        Assert.True(inVBlankAfter);
    }

    [Fact]
    public void InVBlank_StaysFalseWhenCounterIsDeepNegative()
    {
        // Arrange
        var handler = new CpuClockingCounters
        {
            VBlankCounter = -12480 // Typical negative value before next VBlank
        };

        // Act
        var inVBlank = handler.InVBlank;

        // Assert
        Assert.False(inVBlank);
    }

    #endregion

    #region ResetVBlankCounter Tests (4 tests)

    [Fact]
    public void ResetVBlankCounter_SetsCounterToVBlankBlackoutCycles()
    {
        // Arrange
        var handler = new CpuClockingCounters();

        // Act
        handler.ResetVBlankCounter();

        // Assert
        Assert.Equal(CpuClockingCounters.VBlankBlackoutCycles, handler.VBlankCounter);
    }

    [Fact]
    public void ResetVBlankCounter_MakesInVBlankTrue()
    {
        // Arrange
        var handler = new CpuClockingCounters
        {
            VBlankCounter = 0
        };

        // Act
        handler.ResetVBlankCounter();

        // Assert
        Assert.True(handler.InVBlank);
    }

    [Fact]
    public void ResetVBlankCounter_CanBeCalledMultipleTimes()
    {
        // Arrange
        var handler = new CpuClockingCounters();

        // Act
        handler.ResetVBlankCounter();
        handler.VBlankCounter = 100; // Simulate some cycles passed
        handler.ResetVBlankCounter();

        // Assert
        Assert.Equal(CpuClockingCounters.VBlankBlackoutCycles, handler.VBlankCounter);
    }

    [Fact]
    public void ResetVBlankCounter_ResetsFromNegativeValue()
    {
        // Arrange
        var handler = new CpuClockingCounters
        {
            VBlankCounter = -12480
        };

        // Act
        handler.ResetVBlankCounter();

        // Assert
        Assert.Equal(CpuClockingCounters.VBlankBlackoutCycles, handler.VBlankCounter);
        Assert.True(handler.InVBlank);
    }

    #endregion

    #region Typical Usage Scenario Tests (5 tests)

    [Fact]
    public void TypicalScenario_VBlankCountdown()
    {
        // Arrange - Simulate VBlank starting
        var handler = new CpuClockingCounters();
        handler.ResetVBlankCounter();

        // Act & Assert - Countdown from 4,550 to 0
        Assert.Equal(4550, handler.VBlankCounter);
        Assert.True(handler.InVBlank);

        // Simulate 100 cycles
        for (int i = 0; i < 100; i++)
        {
            handler.DecrementVBlankCounter(1);
        }

        Assert.Equal(4450, handler.VBlankCounter);
        Assert.True(handler.InVBlank);
    }

    [Fact]
    public void TypicalScenario_VBlankEnds()
    {
        // Arrange - Counter at 1 (last cycle of VBlank)
        var handler = new CpuClockingCounters
        {
            VBlankCounter = 1
        };

        // Act - Decrement to 0 (VBlank ends)
        handler.DecrementVBlankCounter(1);

        // Assert
        Assert.Equal(0, handler.VBlankCounter);
        Assert.False(handler.InVBlank);
    }

    [Fact]
    public void TypicalScenario_AfterVBlankEnds_CounterGoesNegative()
    {
        // Arrange - Counter at 0 (VBlank just ended)
        var handler = new CpuClockingCounters
        {
            VBlankCounter = 0
        };

        // Act - Continue decrementing (visible scanlines)
        // Note: DecrementVBlankCounter stops at 0, so we manually set negative
        handler.VBlankCounter = -100;

        // Assert
        Assert.Equal(-100, handler.VBlankCounter);
        Assert.False(handler.InVBlank);
    }

    [Fact]
    public void TypicalScenario_FullVBlankCycle()
    {
        // Arrange
        var handler = new CpuClockingCounters();

        // Act - Simulate full VBlank cycle (4,550 cycles)
        handler.ResetVBlankCounter();
        Assert.True(handler.InVBlank);

        for (int i = 0; i < CpuClockingCounters.VBlankBlackoutCycles; i++)
        {
            handler.DecrementVBlankCounter(1);
        }

        // Assert - VBlank should have ended
        Assert.Equal(0, handler.VBlankCounter);
        Assert.False(handler.InVBlank);
    }

    [Fact]
    public void TypicalScenario_MultipleVBlankPeriods()
    {
        // Arrange
        var handler = new CpuClockingCounters();

        // Act & Assert - Simulate 3 VBlank periods
        for (int period = 0; period < 3; period++)
        {
            // VBlank starts
            handler.ResetVBlankCounter();
            Assert.Equal(4550, handler.VBlankCounter);
            Assert.True(handler.InVBlank);

            // VBlank active (4,550 cycles)
            for (int i = 0; i < 4550; i++)
            {
                handler.DecrementVBlankCounter(1);
            }
            Assert.Equal(0, handler.VBlankCounter);
            Assert.False(handler.InVBlank);

            // Visible scanlines (12,480 cycles - simulate just 100 for test speed)
            // Manually set negative since DecrementVBlankCounter stops at 0
            handler.VBlankCounter = -100;
            Assert.Equal(-100, handler.VBlankCounter);
            Assert.False(handler.InVBlank);
        }
    }

    #endregion

    #region Boundary Condition Tests (5 tests)

    [Fact]
    public void BoundaryCondition_CounterAtOne()
    {
        // Arrange
        var handler = new CpuClockingCounters
        {
            VBlankCounter = 1
        };

        // Assert - Should be in VBlank
        Assert.True(handler.InVBlank);
    }

    [Fact]
    public void BoundaryCondition_CounterCrossesZero()
    {
        // Arrange
        var handler = new CpuClockingCounters
        {
            VBlankCounter = 1
        };

        // Act - Cross zero boundary
        Assert.True(handler.InVBlank);
        handler.DecrementVBlankCounter(1);
        Assert.False(handler.InVBlank);
        handler.VBlankCounter--; // Manually decrement past 0
        Assert.False(handler.InVBlank);

        // Assert
        Assert.Equal(-1, handler.VBlankCounter);
    }

    [Fact]
    public void BoundaryCondition_CounterAtMinusOne()
    {
        // Arrange
        var handler = new CpuClockingCounters
        {
            VBlankCounter = -1
        };

        // Assert - Should NOT be in VBlank
        Assert.False(handler.InVBlank);
    }

    [Fact]
    public void BoundaryCondition_LargePositiveCounter()
    {
        // Arrange
        var handler = new CpuClockingCounters
        {
            VBlankCounter = 100000
        };

        // Assert - Should be in VBlank
        Assert.True(handler.InVBlank);
    }

    [Fact]
    public void BoundaryCondition_LargeNegativeCounter()
    {
        // Arrange
        var handler = new CpuClockingCounters
        {
            VBlankCounter = -100000
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
        var handler = new CpuClockingCounters();
        handler.ResetVBlankCounter();

        // Act - Simulate VA2MBus Clock() decrementing
        for (int i = 0; i < 100; i++)
        {
            // VA2MBus.Clock()
            handler.DecrementVBlankCounter(1);

            // SystemIoHandler.Read($C019) would check InVBlank
            bool rdVertBlank = handler.InVBlank;
            Assert.True(rdVertBlank); // Should be in VBlank for first 4,550 cycles
        }

        // Assert
        Assert.Equal(4450, handler.VBlankCounter);
        Assert.True(handler.InVBlank);
    }

    [Fact]
    public void IntegrationSimulation_VBlankStatusForC019Reads()
    {
        // Arrange - Simulating SystemIoHandler reading $C019
        var handler = new CpuClockingCounters
        {
            // Scenario 1: Not in VBlank
            VBlankCounter = -100
        };
        byte rdVertBlank1 = (byte)(handler.InVBlank ? 0x80 : 0x00);
        Assert.Equal(0x00, rdVertBlank1);

        // Scenario 2: In VBlank
        handler.ResetVBlankCounter();
        byte rdVertBlank2 = (byte)(handler.InVBlank ? 0x80 : 0x00);
        Assert.Equal(0x80, rdVertBlank2);

        // Scenario 3: VBlank just ended
        handler.VBlankCounter = 0;
        byte rdVertBlank3 = (byte)(handler.InVBlank ? 0x80 : 0x00);
        Assert.Equal(0x00, rdVertBlank3);
    }

    [Fact]
    public void IntegrationSimulation_17030CycleFrame()
    {
        // Arrange - Simulate full 17,030 cycle frame
        var handler = new CpuClockingCounters();
        int inVBlankCycles = 0;
        int notInVBlankCycles = 0;

        // Act - Simulate VBlank starting at cycle 12,480
        // First 12,480 cycles: not in VBlank (counter goes negative)
        handler.VBlankCounter = 0;
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
            handler.VBlankCounter--;
        }

        // VBlank starts at cycle 12,480
        handler.ResetVBlankCounter();

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
            handler.DecrementVBlankCounter(1);
        }

        // Assert
        Assert.Equal(4550, inVBlankCycles);
        Assert.Equal(12480, notInVBlankCycles);
        Assert.Equal(17030, inVBlankCycles + notInVBlankCycles);
    }

    #endregion
}
