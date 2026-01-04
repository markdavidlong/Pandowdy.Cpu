using Pandowdy.EmuCore.Services;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Tests.Helpers;

namespace Pandowdy.EmuCore.Tests;

/// <summary>
/// Unit tests for VA2M, the main Apple II emulator class.
/// 
/// These tests use mock dependencies to test VA2M functionality in isolation.
/// Integration tests should be added separately for full system testing.
/// </summary>
public class VA2MTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_InitializesSuccessfully()
    {
        // Arrange & Act
        var va2m = VA2MTestHelpers.CreateBuilder().Build();

        // Assert
        Assert.NotNull(va2m);
        Assert.NotNull(va2m.MemoryPool);
        Assert.NotNull(va2m.Bus);
    }

    [Fact]
    public void Constructor_WithNullEmulatorState_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            VA2MTestHelpers.CreateBuilder()
                .WithEmulatorState(null!)
                .Build());
    }

    [Fact]
    public void Constructor_WithNullFrameProvider_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            VA2MTestHelpers.CreateBuilder()
                .WithFrameProvider(null!)
                .Build());
    }

    [Fact]
    public void Constructor_WithNullSystemStatusProvider_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            VA2MTestHelpers.CreateBuilder()
                .WithSystemStatusProvider(null!)
                .Build());
    }

    [Fact]
    public void Constructor_WithNullBus_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            VA2MTestHelpers.CreateBuilder()
                .WithBus(null!)
                .Build());
    }

    [Fact]
    public void Constructor_WithNullMemoryPool_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            VA2MTestHelpers.CreateBuilder()
                .WithMemoryPool(null!)
                .Build());
    }

    [Fact]
    public void Constructor_WithNullFrameGenerator_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            VA2MTestHelpers.CreateBuilder()
                .WithFrameGenerator(null!)
                .Build());
    }

    #endregion

    #region Property Tests

    [Fact]
    public void ThrottleEnabled_DefaultsToTrue()
    {
        // Arrange
        var va2m = VA2MTestHelpers.CreateBuilder().Build();

        // Act & Assert
        Assert.True(va2m.ThrottleEnabled);
    }

    [Fact]
    public void ThrottleEnabled_CanBeSetToFalse()
    {
        // Arrange
        var va2m = VA2MTestHelpers.CreateBuilder().Build();

        // Act
        va2m.ThrottleEnabled = false;

        // Assert
        Assert.False(va2m.ThrottleEnabled);
    }

    [Fact]
    public void TargetHz_DefaultsTo1023000()
    {
        // Arrange
        var va2m = VA2MTestHelpers.CreateBuilder().Build();

        // Act & Assert
        Assert.Equal(1_023_000d, va2m.TargetHz);
    }

    [Fact]
    public void TargetHz_CanBeChanged()
    {
        // Arrange
        var va2m = VA2MTestHelpers.CreateBuilder().Build();

        // Act
        va2m.TargetHz = 2_000_000d;

        // Assert
        Assert.Equal(2_000_000d, va2m.TargetHz);
    }

    [Fact]
    public void SystemClock_ReflectsBusSystemClockCounter()
    {
        // Arrange
        var testBus = new TestAppleIIBus();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithBus(testBus)
            .Build();

        // Act
        testBus.Clock();
        testBus.Clock();
        testBus.Clock();

        // Assert
        Assert.Equal(3UL, va2m.SystemClock);
    }

    #endregion

    #region Clock and Reset Tests

    [Fact]
    public void Clock_IncrementsBusClock()
    {
        // Arrange
        var testBus = new TestAppleIIBus();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithBus(testBus)
            .Build();

        var initialClock = va2m.SystemClock;

        // Act
        va2m.Clock();

        // Assert
        Assert.Equal(initialClock + 1, va2m.SystemClock);
    }

    [Fact]
    public void Clock_PublishesStateToEmulatorState()
    {
        // Arrange
        var testState = new TestEmulatorState();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithEmulatorState(testState)
            .Build();

        var initialCount = testState.UpdateCount;

        // Act
        va2m.Clock();

        // Assert
        Assert.True(testState.UpdateCount > initialCount, "State should be updated after Clock()");
    }

    [Fact]
    public void Reset_CallsBusReset()
    {
        // Arrange
        var testBus = new TestAppleIIBus();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithBus(testBus)
            .Build();

        // Act
        va2m.Reset();
        va2m.Clock(); // Process pending queue

        // Assert
        Assert.Equal(1, testBus.ResetCount);
    }

    [Fact]
    public void Reset_ResetsBusClockToZero()
    {
        // Arrange
        var testBus = new TestAppleIIBus();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithBus(testBus)
            .Build();

        // Advance clock
        va2m.Clock();
        va2m.Clock();
        va2m.Clock();
        Assert.Equal(3UL, va2m.SystemClock); // Verify we're at 3 cycles

        // Act
        va2m.Reset(); // Enqueues reset operation
        
        // Process pending queue - this will execute the reset AND increment bus clock by 1
        // After reset executes: SystemClock = 0
        // After Bus.Clock() in va2m.Clock(): SystemClock = 1
        va2m.Clock();

        // Assert - Clock is at 1 because va2m.Clock() both processes reset and increments
        Assert.Equal(1UL, va2m.SystemClock);
    }

    #endregion

    #region Key Injection Tests

    [Fact]
    public void InjectKey_SetsHighBitAutomatically()
    {
        // Arrange
        var testBus = new TestAppleIIBus();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithBus(testBus)
            .Build();

        // Act - Inject ASCII 'A' (0x41) without high bit
        va2m.InjectKey(0x41);

        // Process pending queue
        va2m.Clock();

        // Assert - High bit should be set (0x41 | 0x80 = 0xC1)
        Assert.Equal(0xC1, testBus.GetKeyValue());
    }

    [Fact]
    public void InjectKey_PreservesHighBitIfAlreadySet()
    {
        // Arrange
        var testBus = new TestAppleIIBus();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithBus(testBus)
            .Build();

        // Act - Inject with high bit already set
        va2m.InjectKey(0xC1); // 'A' with high bit

        // Process pending queue
        va2m.Clock();

        // Assert - Should still be 0xC1
        Assert.Equal(0xC1, testBus.GetKeyValue());
    }

    [Theory]
    [InlineData(0x20, 0xA0)]  // Space
    [InlineData(0x41, 0xC1)]  // 'A'
    [InlineData(0x5A, 0xDA)]  // 'Z'
    [InlineData(0x61, 0xE1)]  // 'a'
    [InlineData(0x7A, 0xFA)]  // 'z'
    [InlineData(0x30, 0xB0)]  // '0'
    [InlineData(0x39, 0xB9)]  // '9'
    public void InjectKey_VariousAsciiCharacters_SetsCorrectValue(byte input, byte expected)
    {
        // Arrange
        var testBus = new TestAppleIIBus();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithBus(testBus)
            .Build();

        // Act
        va2m.InjectKey(input);
        va2m.Clock(); // Process pending queue

        // Assert
        Assert.Equal(expected, testBus.GetKeyValue());
    }

    [Fact]
    public void InjectKey_MultipleTimes_UpdatesKeyValue()
    {
        // Arrange
        var testBus = new TestAppleIIBus();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithBus(testBus)
            .Build();

        // Act - Inject multiple keys
        va2m.InjectKey(0x41); // 'A'
        va2m.Clock();
        var firstKey = testBus.GetKeyValue();

        va2m.InjectKey(0x42); // 'B'
        va2m.Clock();
        var secondKey = testBus.GetKeyValue();

        // Assert
        Assert.Equal(0xC1, firstKey);
        Assert.Equal(0xC2, secondKey);
    }

    #endregion

    #region Push Button Tests

    [Theory]
    [InlineData(0, true)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(2, false)]
    public void SetPushButton_UpdatesButtonState(byte buttonNum, bool pressed)
    {
        // Arrange
        var testBus = new TestAppleIIBus();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithBus(testBus)
            .Build();

        // Act
        va2m.SetPushButton(buttonNum, pressed);
        va2m.Clock(); // Process pending queue

        // Assert
        Assert.Equal(pressed, testBus.GetPushButton(buttonNum));
    }

    [Fact]
    public void SetPushButton_MultipleButtons_IndependentStates()
    {
        // Arrange
        var testBus = new TestAppleIIBus();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithBus(testBus)
            .Build();

        // Act - Set different states for each button
        va2m.SetPushButton(0, true);
        va2m.SetPushButton(1, false);
        va2m.SetPushButton(2, true);
        va2m.Clock(); // Process pending queue

        // Assert
        Assert.True(testBus.GetPushButton(0));
        Assert.False(testBus.GetPushButton(1));
        Assert.True(testBus.GetPushButton(2));
    }

    [Fact]
    public void SetPushButton_Toggle_ChangesState()
    {
        // Arrange
        var testBus = new TestAppleIIBus();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithBus(testBus)
            .Build();

        // Act - Press, release, press again
        va2m.SetPushButton(0, true);
        va2m.Clock();
        Assert.True(testBus.GetPushButton(0));

        va2m.SetPushButton(0, false);
        va2m.Clock();
        Assert.False(testBus.GetPushButton(0));

        va2m.SetPushButton(0, true);
        va2m.Clock();
        Assert.True(testBus.GetPushButton(0));
    }

    #endregion

    #region Memory Pool Tests

    [Fact]
    public void MemoryPool_IsAccessibleAfterConstruction()
    {
        // Arrange
        var statusProvider = new SystemStatusProvider();
        var memoryPool = new MemoryPool(statusProvider, new TestLanguageCard());
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithMemoryPool(memoryPool)
            .Build();

        // Act & Assert
        Assert.Same(memoryPool, va2m.MemoryPool);
    }

    [Fact]
    public void MemoryPool_CanReadAndWrite()
    {
        // Arrange
        var va2m = VA2MTestHelpers.CreateBuilder().Build();

        // Act
        va2m.MemoryPool.Write(0x1000, 0x42);
        var value = va2m.MemoryPool.Read(0x1000);

        // Assert
        Assert.Equal(0x42, value);
    }

    #endregion

    #region Throttling Tests

    [Fact]
    public void Clock_WithThrottleDisabled_ExecutesQuickly()
    {
        // Arrange
        var va2m = VA2MTestHelpers.CreateBuilder().Build();
        va2m.ThrottleEnabled = false;

        // Act - Run many cycles
        var startTime = DateTime.UtcNow;
        for (int i = 0; i < 1000; i++)
        {
            va2m.Clock();
        }
        var elapsed = DateTime.UtcNow - startTime;

        // Assert - Should complete in well under 1 second
        Assert.True(elapsed.TotalMilliseconds < 100, 
            $"1000 unthrottled cycles took {elapsed.TotalMilliseconds}ms (should be < 100ms)");
    }

    // Note: Testing throttling with ThrottleEnabled=true requires timing
    // which can be flaky in CI/CD. Consider integration tests for this.

    #endregion

    #region Bus Interaction Tests

    [Fact]
    public void Bus_IsAccessibleAfterConstruction()
    {
        // Arrange
        var testBus = new TestAppleIIBus();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithBus(testBus)
            .Build();

        // Act & Assert
        Assert.Same(testBus, va2m.Bus);
    }

    [Fact]
    public void SystemClock_UpdatesWithBusClock()
    {
        // Arrange
        var testBus = new TestAppleIIBus();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithBus(testBus)
            .Build();

        // Act
        for (int i = 0; i < 10; i++)
        {
            testBus.Clock();
        }

        // Assert
        Assert.Equal(10UL, va2m.SystemClock);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        // Arrange
        var va2m = VA2MTestHelpers.CreateBuilder().Build();

        // Act & Assert - Should not throw
        va2m.Dispose();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var va2m = VA2MTestHelpers.CreateBuilder().Build();

        // Act & Assert - Should not throw on multiple calls
        va2m.Dispose();
        va2m.Dispose();
        va2m.Dispose();
    }

    #endregion

    #region Integration Scenario Tests

    [Fact]
    public void Scenario_BootSequence_InitializesCorrectly()
    {
        // Arrange
        var testState = new TestEmulatorState();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithEmulatorState(testState)
            .Build();

        // Act - Simulate basic boot
        va2m.Reset();
        for (int i = 0; i < 100; i++)
        {
            va2m.Clock(); // First Clock() processes the Reset, subsequent ones execute normally
        }

        // Assert
        Assert.Equal(100UL, va2m.SystemClock);
        Assert.True(testState.UpdateCount > 0, "State should be updated during execution");
    }

    [Fact]
    public void Scenario_KeyboardInput_ProcessesCorrectly()
    {
        // Arrange
        var testBus = new TestAppleIIBus();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithBus(testBus)
            .Build();

        // Act - Simulate typing "HELLO"
        byte[] keys = [0x48, 0x45, 0x4C, 0x4C, 0x4F]; // H E L L O
        foreach (var key in keys)
        {
            va2m.InjectKey(key);
            va2m.Clock();
        }

        // Assert - Last key should be 'O' with high bit set
        Assert.Equal(0xCF, testBus.GetKeyValue());
    }

    [Fact]
    public void Scenario_GameController_MultipleButtonPresses()
    {
        // Arrange
        var testBus = new TestAppleIIBus();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithBus(testBus)
            .Build();

        // Act - Simulate game controller inputs
        va2m.SetPushButton(0, true);  // Fire button
        va2m.Clock();
        va2m.SetPushButton(1, true);  // Jump button
        va2m.Clock();
        va2m.SetPushButton(0, false); // Release fire
        va2m.Clock();

        // Assert
        Assert.False(testBus.GetPushButton(0)); // Fire released
        Assert.True(testBus.GetPushButton(1));  // Jump still pressed
        Assert.False(testBus.GetPushButton(2)); // Third button not pressed
    }

    #endregion
}
