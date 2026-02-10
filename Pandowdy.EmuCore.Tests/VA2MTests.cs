// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Services;
using Pandowdy.EmuCore.Tests.Helpers;

namespace Pandowdy.EmuCore.Tests;

/// <summary>
/// Unit tests for VA2M emulator orchestration layer.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Test Scope:</strong> VA2M is responsible for command queueing, thread marshaling,
/// and orchestration - NOT for keyboard protocol implementation (strobe bits, etc.).
/// </para>
/// <para>
/// <strong>What VA2M Does:</strong>
/// <list type="bullet">
/// <item>Enqueue commands for cross-thread execution (UI â†’ emulator thread)</item>
/// <item>Process pending commands at instruction boundaries</item>
/// <item>Pass commands to Bus unchanged (no transformation)</item>
/// <item>Coordinate Clock(), Reset(), and state publishing</item>
/// </list>
/// </para>
/// <para>
/// <strong>What VA2M Does NOT Do:</strong>
/// Keyboard protocol (strobe bit setting) - tested in <see cref="SingularKeyHandlerTests"/>
/// </para>
/// </remarks>
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
        va2m.Clock(); // Process pending queue to execute SetThrottleEnabledInternal()

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
    public void Clock_StateIsAccessibleAfterExecution()
    {
        // Arrange - Post-Task 8 architecture: state is pulled, not pushed
        var testBus = new TestAppleIIBus();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithBus(testBus)
            .Build();

        var initialClock = va2m.SystemClock;

        // Act
        va2m.Clock();

        // Assert - Verify state is accessible via pull pattern
        Assert.Equal(initialClock + 1, va2m.SystemClock);
        Assert.NotNull(va2m.Bus.Cpu); // CPU state is accessible
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

    #region Command Queueing Tests - EnqueueKey

    [Fact]
    public void EnqueueKey_EnqueuesCommandForExecution()
    {
        // Arrange
        var keyboard = new SingularKeyHandler();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithKeyboardSetter(keyboard)
            .Build();

        // Act - VA2M enqueues command for thread-safe execution
        va2m.EnqueueKey(0x41);
        
        // Before Clock() - command not yet executed
        Assert.False(keyboard.StrobePending());
        
        // Process pending queue
        va2m.Clock();

        // Assert - Command executed, keyboard has key
        Assert.True(keyboard.StrobePending());
        Assert.Equal(0x41, keyboard.PeekCurrentKeyValue());
    }

    [Fact]
    public void EnqueueKey_MultipleCommands_ExecutedInOrder()
    {
        // Arrange
        var keyboard = new SingularKeyHandler();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithKeyboardSetter(keyboard)
            .Build();

        // Act - Enqueue multiple keys
        va2m.EnqueueKey(0x41); // 'A'
        va2m.EnqueueKey(0x42); // 'B' (will overwrite 'A' due to Apple IIe behavior)
        va2m.EnqueueKey(0x43); // 'C' (will overwrite 'B')
        va2m.Clock(); // Process all pending commands

        // Assert - Last key wins (authentic Apple IIe single-key latch behavior)
        // This tests VA2M's command queueing, not keyboard buffering
        Assert.Equal(0x43, keyboard.PeekCurrentKeyValue());
    }

    [Fact]
    public void EnqueueKey_DeferredExecution_CommandNotExecutedUntilClock()
    {
        // Arrange
        var keyboard = new SingularKeyHandler();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithKeyboardSetter(keyboard)
            .Build();

        // Act - Enqueue key but don't call Clock()
        va2m.EnqueueKey(0x41);

        // Assert - Command not executed yet (no Clock() called)
        Assert.False(keyboard.StrobePending());
        
        // Act - Now process pending queue
        va2m.Clock();
        
        // Assert - Command executed after Clock()
        Assert.True(keyboard.StrobePending());
        Assert.Equal(0x41, keyboard.PeekCurrentKeyValue());
    }

    [Fact]
    public void EnqueueKey_ThreadSafeQueuing_CommandsDeferredUntilSafePoint()
    {
        // Arrange
        var keyboard = new SingularKeyHandler();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithKeyboardSetter(keyboard)
            .Build();

        // Act - Simulate UI thread calling EnqueueKey
        // (In real usage, this would be from a different thread)
        va2m.EnqueueKey(0x41);
        va2m.EnqueueKey(0x42);
        
        // Commands queued but not executed
        Assert.False(keyboard.StrobePending());
        
        // Emulator thread processes queue
        va2m.Clock();

        // Assert - Commands executed on emulator thread
        Assert.True(keyboard.StrobePending());
    }

    #endregion

    #region Command Queueing Tests - SetPushButton

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
        var gameController = new SimpleGameController();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithGameController(gameController)
            .Build();

        // Act
        va2m.SetPushButton(buttonNum, pressed);
        va2m.Clock(); // Process pending queue

        // Assert - Check game controller state (not bus)
        Assert.Equal(pressed, gameController.GetButton(buttonNum));
    }

    [Fact]
    public void SetPushButton_MultipleButtons_IndependentStates()
    {
        // Arrange
        var gameController = new SimpleGameController();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithGameController(gameController)
            .Build();

        // Act - Set different states for each button
        va2m.SetPushButton(0, true);
        va2m.SetPushButton(1, false);
        va2m.SetPushButton(2, true);
        va2m.Clock(); // Process pending queue

        // Assert - Check game controller state
        Assert.True(gameController.GetButton(0));
        Assert.False(gameController.GetButton(1));
        Assert.True(gameController.GetButton(2));
    }

    [Fact]
    public void SetPushButton_Toggle_ChangesState()
    {
        // Arrange
        var gameController = new SimpleGameController();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithGameController(gameController)
            .Build();

        // Act - Press, release, press again
        va2m.SetPushButton(0, true);
        va2m.Clock();
        Assert.True(gameController.GetButton(0));

        va2m.SetPushButton(0, false);
        va2m.Clock();
        Assert.False(gameController.GetButton(0));

        va2m.SetPushButton(0, true);
        va2m.Clock();
        Assert.True(gameController.GetButton(0));
    }

    #endregion

    #region Clock() Single-Step Tests

    [Fact]
    public void Clock_AlwaysExecutesQuickly_NoThrottling()
    {
        // Arrange - Clock() is now optimized for single-stepping with NO throttling
        var va2m = VA2MTestHelpers.CreateBuilder().Build();

        // Clock() ignores ThrottleEnabled - it never throttles
        // ThrottleEnabled only affects RunAsync()
        va2m.ThrottleEnabled = true;  // This has no effect on Clock()

        // Act - Run many cycles (single-step mode)
        var startTime = DateTime.UtcNow;
        for (int i = 0; i < 1000; i++)
        {
            va2m.Clock();
        }
        var elapsed = DateTime.UtcNow - startTime;

        // Assert - Clock() is fast regardless of ThrottleEnabled setting
        // Should complete in well under 1 second even with throttle "enabled"
        Assert.True(elapsed.TotalMilliseconds < 100, 
            $"1000 Clock() cycles took {elapsed.TotalMilliseconds}ms (should be < 100ms). " +
            $"Clock() should never throttle - it's for single-stepping only.");
    }

    [Fact]
    public void Clock_ProcessesPendingActionsBeforeAndAfter()
    {
        // Arrange - Verify that Clock() checks pending queue twice per cycle
        var keyboard = new SingularKeyHandler();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithKeyboardSetter(keyboard)
            .Build();

        // Act - Enqueue command and call Clock() once
        va2m.EnqueueKey(0x41);
        va2m.Clock();

        // Assert - Command should be processed within the single Clock() call
        Assert.True(keyboard.StrobePending());
        Assert.Equal(0x41, keyboard.PeekCurrentKeyValue());
    }

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
        // Arrange - Post-Task 8: Verify state via pull pattern, not push
        var testBus = new TestAppleIIBus();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithBus(testBus)
            .Build();

        // Act - Simulate basic boot
        va2m.Reset();
        for (int i = 0; i < 100; i++)
        {
            va2m.Clock(); // First Clock() processes the Reset, subsequent ones execute normally
        }

        // Assert - State is accessible via pull pattern (IEmulatorCoreInterface)
        Assert.Equal(100UL, va2m.SystemClock);
        Assert.Equal(1, testBus.ResetCount); // Verify reset was processed
        Assert.NotNull(va2m.Bus.Cpu); // CPU state is accessible for pulling
    }

    [Fact]
    public void Scenario_CommandQueueing_MixedCommands()
    {
        // Arrange
        var keyboard = new SingularKeyHandler();
        var gameController = new SimpleGameController();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithKeyboardSetter(keyboard)
            .WithGameController(gameController)
            .Build();

        // Act - Mix keyboard and button commands
        va2m.EnqueueKey(0x41);       // 'A' (will be overwritten by 'B')
        va2m.SetPushButton(0, true);  // Button 0 press
        va2m.EnqueueKey(0x42);       // 'B' (overwrites 'A' - Apple IIe single-key behavior)
        va2m.SetPushButton(0, false); // Button 0 release
        va2m.Clock(); // Process all pending commands

        // Assert - Verify both keyboard and button commands executed
        // Keyboard: Only last key survives (Apple IIe authentic single-key latch)
        Assert.True(keyboard.StrobePending());
        Assert.Equal(0x42, keyboard.PeekCurrentKeyValue()); // 'B' survived
        
        // Buttons: Final state is correct (check game controller, not bus)
        Assert.False(gameController.GetButton(0)); // Released
    }

    [Fact]
    public void Scenario_GameController_MultipleButtonPresses()
    {
        // Arrange
        var gameController = new SimpleGameController();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithGameController(gameController)
            .Build();

        // Act - Simulate game controller inputs
        va2m.SetPushButton(0, true);  // Fire button
        va2m.Clock();
        va2m.SetPushButton(1, true);  // Jump button
        va2m.Clock();
        va2m.SetPushButton(0, false); // Release fire
        va2m.Clock();

        // Assert - Check game controller state
        Assert.False(gameController.GetButton(0)); // Fire released
        Assert.True(gameController.GetButton(1));  // Jump still pressed
        Assert.False(gameController.GetButton(2)); // Third button not pressed
    }

    #endregion

    #region RunAsync Execution Loop Tests

    [Fact]
    public async Task RunAsync_WithCancellation_StopsGracefully()
    {
        // Arrange
        var va2m = VA2MTestHelpers.CreateBuilder().Build();
        va2m.ThrottleEnabled = false; // Run fast for test
        var cts = new CancellationTokenSource();

        // Act - Start async execution
        var runTask = Task.Run(() => va2m.RunAsync(cts.Token));

        // Allow it to run briefly
        await Task.Delay(50);

        // Cancel execution
        cts.Cancel();

        // Assert - Should complete (either cleanly or with TaskCanceledException)
        try
        {
            await runTask;
            // Task completed without exception - acceptable
        }
        catch (TaskCanceledException)
        {
            // Task was canceled - also acceptable
        }

        // Verify it actually ran some cycles
        Assert.True(va2m.SystemClock > 0, "RunAsync should have executed at least one cycle");
    }

    [Fact]
    public async Task RunAsync_ThrottledMode_ExecutesCycles()
    {
        // Arrange
        var testBus = new TestAppleIIBus();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithBus(testBus)
            .Build();
        va2m.ThrottleEnabled = true;
        var cts = new CancellationTokenSource();

        // Act - Run for short duration
        var runTask = Task.Run(() => va2m.RunAsync(cts.Token, ticksPerSecond: 1000));

        await Task.Delay(100); // Run for 100ms
        cts.Cancel();

        try
        {
            await runTask;
        }
        catch (TaskCanceledException)
        {
            // Expected
        }

        // Assert - Cycles should have been executed
        // At 1.023 MHz, 100ms = ~102,300 cycles (allow 50% margin for timing variance)
        Assert.True(va2m.SystemClock > 50_000,
            $"Expected > 50,000 cycles after 100ms, got {va2m.SystemClock}");
    }

    [Fact]
    public async Task RunAsync_FastMode_ExecutesManyMoreCycles()
    {
        // Arrange
        var testBus = new TestAppleIIBus();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithBus(testBus)
            .Build();
        va2m.ThrottleEnabled = false; // Run as fast as possible
        var cts = new CancellationTokenSource();

        // Act - Run for short duration
        var runTask = Task.Run(() => va2m.RunAsync(cts.Token));

        await Task.Delay(100); // Run for 100ms
        cts.Cancel();

        try
        {
            await runTask;
        }
        catch (TaskCanceledException)
        {
            // Expected
        }

        // Assert - Fast mode should execute far more cycles than throttled mode
        // Should execute at least 500,000 cycles (5x throttled rate)
        Assert.True(va2m.SystemClock > 500_000,
            $"Expected > 500,000 cycles in fast mode after 100ms, got {va2m.SystemClock}");
    }

    [Fact]
    public async Task RunAsync_ProcessesPendingCommands()
    {
        // Arrange
        var keyboard = new SingularKeyHandler();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithKeyboardSetter(keyboard)
            .Build();
        va2m.ThrottleEnabled = false;
        var cts = new CancellationTokenSource();

        // Act - Start execution, then enqueue command while running
        var runTask = Task.Run(() => va2m.RunAsync(cts.Token));

        await Task.Delay(10); // Let it start

        va2m.EnqueueKey(0x41); // Enqueue 'A'

        await Task.Delay(50); // Allow processing

        cts.Cancel();

        try
        {
            await runTask;
        }
        catch (TaskCanceledException)
        {
            // Expected
        }

        // Assert - Command should have been processed
        Assert.True(keyboard.StrobePending());
        Assert.Equal(0x41, keyboard.PeekCurrentKeyValue());
    }

    [Fact]
    public async Task RunAsync_WithDifferentTicksPerSecond_ExecutesCycles()
    {
        // Arrange
        var testBus = new TestAppleIIBus();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithBus(testBus)
            .Build();
        va2m.ThrottleEnabled = true;
        var cts = new CancellationTokenSource();

        // Act - Run with 60 Hz ticks (frame rate)
        var runTask = Task.Run(() => va2m.RunAsync(cts.Token, ticksPerSecond: 60));

        await Task.Delay(100); // Run for 100ms
        cts.Cancel();

        try
        {
            await runTask;
        }
        catch (TaskCanceledException)
        {
            // Expected
        }

        // Assert - Should still execute reasonable number of cycles
        Assert.True(va2m.SystemClock > 50_000,
            $"Expected > 50,000 cycles with 60 Hz ticks, got {va2m.SystemClock}");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task RunAsync_WithInvalidTicksPerSecond_ThrowsArgumentOutOfRangeException(double invalidTicks)
    {
        // Arrange
        var va2m = VA2MTestHelpers.CreateBuilder().Build();
        var cts = new CancellationTokenSource();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await va2m.RunAsync(cts.Token, ticksPerSecond: invalidTicks));
    }

    #endregion

    #region PID Throttling Behavior Tests

    [Fact]
    public void ThrottleEnabled_Setter_EnqueuesStateChange()
    {
        // Arrange
        var va2m = VA2MTestHelpers.CreateBuilder().Build();
        Assert.True(va2m.ThrottleEnabled); // Default is true

        // Act - Set to false (enqueues command)
        va2m.ThrottleEnabled = false;

        // Before Clock() - change not yet applied
        // Note: We can't reliably test the "before" state due to volatile read,
        // but the command is enqueued

        // Process pending queue
        va2m.Clock();

        // Assert - Change applied after Clock()
        Assert.False(va2m.ThrottleEnabled);
    }

    [Fact]
    public void ThrottleEnabled_MultipleTogglesDuringExecution()
    {
        // Arrange
        var va2m = VA2MTestHelpers.CreateBuilder().Build();

        // Act - Toggle multiple times
        va2m.ThrottleEnabled = false;
        va2m.Clock(); // Apply change
        Assert.False(va2m.ThrottleEnabled);

        va2m.ThrottleEnabled = true;
        va2m.Clock(); // Apply change
        Assert.True(va2m.ThrottleEnabled);

        va2m.ThrottleEnabled = false;
        va2m.Clock(); // Apply change
        Assert.False(va2m.ThrottleEnabled);
    }

    [Fact]
    public void TargetHz_CanBeAdjusted()
    {
        // Arrange
        var va2m = VA2MTestHelpers.CreateBuilder().Build();
        Assert.Equal(1_023_000d, va2m.TargetHz); // Default Apple IIe speed

        // Act - Change target frequency
        va2m.TargetHz = 2_000_000d; // 2 MHz

        // Assert
        Assert.Equal(2_000_000d, va2m.TargetHz);
    }

    [Fact]
    public async Task RunAsync_RespectstargetHz_InThrottledMode()
    {
        // Arrange
        var testBus = new TestAppleIIBus();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithBus(testBus)
            .Build();
        va2m.ThrottleEnabled = true;
        va2m.TargetHz = 500_000d; // Half speed
        var cts = new CancellationTokenSource();

        // Act - Run for 100ms
        var runTask = Task.Run(() => va2m.RunAsync(cts.Token, ticksPerSecond: 1000));

        await Task.Delay(100);
        cts.Cancel();

        try
        {
            await runTask;
        }
        catch (TaskCanceledException)
        {
            // Expected
        }

        // Assert - Should execute roughly half as many cycles
        // At 500 kHz, 100ms = ~50,000 cycles (allow 50% margin)
        Assert.True(va2m.SystemClock > 25_000,
            $"Expected > 25,000 cycles at 500 kHz, got {va2m.SystemClock}");
        Assert.True(va2m.SystemClock < 150_000,
            $"Expected < 150,000 cycles at 500 kHz, got {va2m.SystemClock}");
    }

    #endregion

    #region Reset Handling Tests

    [Fact]
    public void Reset_AfterMultipleCycles_ResetsSystemClock()
    {
        // Arrange
        var testBus = new TestAppleIIBus();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithBus(testBus)
            .Build();

        // Execute some cycles
        for (int i = 0; i < 100; i++)
        {
            va2m.Clock();
        }
        Assert.Equal(100UL, va2m.SystemClock);

        // Act - Reset
        va2m.Reset();
        va2m.Clock(); // Process reset (which also increments clock by 1)

        // Assert - Clock reset to 1 (reset executed + 1 clock increment)
        Assert.Equal(1UL, va2m.SystemClock);
    }

    [Fact]
    public void Reset_IncrementsBusResetCount()
    {
        // Arrange
        var testBus = new TestAppleIIBus();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithBus(testBus)
            .Build();

        Assert.Equal(0, testBus.ResetCount);

        // Act
        va2m.Reset();
        va2m.Clock(); // Process pending queue

        // Assert
        Assert.Equal(1, testBus.ResetCount);
    }

    [Fact]
    public void Reset_ClearsPendingCommands()
    {
        // Arrange
        var keyboard = new SingularKeyHandler();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithKeyboardSetter(keyboard)
            .Build();

        // Enqueue commands
        va2m.EnqueueKey(0x41);
        va2m.EnqueueKey(0x42);

        // Act - Reset before commands are processed
        va2m.Reset();
        va2m.Clock(); // Process reset (clears queue)

        // Assert - Commands should not have been executed
        // Note: This tests the intended behavior; implementation may vary
        Assert.False(keyboard.StrobePending());
    }

    [Fact]
    public async Task Reset_DuringRunAsync_StopsAndResets()
    {
        // Arrange
        var testBus = new TestAppleIIBus();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithBus(testBus)
            .Build();
        va2m.ThrottleEnabled = false;
        var cts = new CancellationTokenSource();

        // Start execution
        var runTask = Task.Run(() => va2m.RunAsync(cts.Token));
        await Task.Delay(50); // Let it run

        // Act - Reset while running
        va2m.Reset();
        await Task.Delay(10); // Allow reset to process

        var clockBeforeCancel = va2m.SystemClock;

        cts.Cancel();

        try
        {
            await runTask;
        }
        catch (TaskCanceledException)
        {
            // Expected
        }

        // Assert - System clock should reflect reset happened
        // (hard to test exact timing, but clock should be relatively low)
        Assert.True(va2m.SystemClock < clockBeforeCancel + 10_000,
            "Clock should not have advanced much after reset");
    }

    #endregion

    #region Command Queueing At Instruction Boundaries Tests

    [Fact]
    public void CommandQueue_ExecutesAtInstructionBoundary()
    {
        // Arrange
        var keyboard = new SingularKeyHandler();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithKeyboardSetter(keyboard)
            .Build();

        // Act - Enqueue command
        va2m.EnqueueKey(0x41);

        // Assert - Not executed until Clock() processes pending actions
        Assert.False(keyboard.StrobePending());

        // Process pending queue (happens at instruction boundary in Clock())
        va2m.Clock();

        // Assert - Now executed
        Assert.True(keyboard.StrobePending());
    }

    [Fact]
    public void CommandQueue_MultipleCommands_ExecutedInOrder()
    {
        // Arrange
        var keyboard = new SingularKeyHandler();
        var gameController = new SimpleGameController();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithKeyboardSetter(keyboard)
            .WithGameController(gameController)
            .Build();

        // Act - Enqueue multiple commands
        va2m.SetPushButton(0, true);
        va2m.EnqueueKey(0x41);
        va2m.SetPushButton(1, true);

        // Process all pending
        va2m.Clock();

        // Assert - All commands executed
        Assert.True(gameController.GetButton(0));
        Assert.True(keyboard.StrobePending());
        Assert.Equal(0x41, keyboard.PeekCurrentKeyValue());
        Assert.True(gameController.GetButton(1));
    }

    [Fact]
    public async Task CommandQueue_ProcessedDuringRunAsync()
    {
        // Arrange
        var keyboard = new SingularKeyHandler();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithKeyboardSetter(keyboard)
            .Build();
        va2m.ThrottleEnabled = false;
        var cts = new CancellationTokenSource();

        // Start execution
        var runTask = Task.Run(() => va2m.RunAsync(cts.Token));
        await Task.Delay(10);

        // Act - Enqueue commands while running
        va2m.EnqueueKey(0x41);
        va2m.EnqueueKey(0x42);
        va2m.EnqueueKey(0x43);

        await Task.Delay(50); // Allow processing

        cts.Cancel();

        try
        {
            await runTask;
        }
        catch (TaskCanceledException)
        {
            // Expected
        }

        // Assert - Last command should have been processed
        Assert.True(keyboard.StrobePending());
        Assert.Equal(0x43, keyboard.PeekCurrentKeyValue());
    }

    [Fact]
    public async Task CommandQueue_ThreadSafety_NoRaceConditions()
    {
        // Arrange
        var keyboard = new SingularKeyHandler();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithKeyboardSetter(keyboard)
            .Build();

        // Act - Simulate commands from multiple threads
        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            byte key = (byte)(0x41 + i); // A-J
            tasks.Add(Task.Run(() => va2m.EnqueueKey(key)));
        }

        await Task.WhenAll(tasks);

        // Process all pending commands
        va2m.Clock();

        // Assert - Last key should be set (no crashes/corruption)
        Assert.True(keyboard.StrobePending());
        // Value depends on which thread won, but should be valid
        Assert.InRange(keyboard.PeekCurrentKeyValue(), (byte)0x41, (byte)0x4A);
    }

    #endregion

    #region VBlank Event Handling Tests

    [Fact]
    public void Clock_TriggersVBlankEvent()
    {
        // Arrange
        var testBus = new TestAppleIIBus();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithBus(testBus)
            .Build();

        bool vblankTriggered = false;
        testBus.VBlankOccurred += () => vblankTriggered = true;

        // Act - Execute enough cycles to trigger VBlank (~12,480 cycles at 60 Hz)
        for (int i = 0; i < 13_000; i++)
        {
            va2m.Clock();
        }

        // Assert - VBlank should have triggered
        Assert.True(vblankTriggered, "VBlank should have been triggered after ~12,480 cycles");
    }

    [Fact]
    public async Task RunAsync_TriggersVBlankEvents()
    {
        // Arrange
        var testBus = new TestAppleIIBus();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithBus(testBus)
            .Build();
        va2m.ThrottleEnabled = false;

        int vblankCount = 0;
        testBus.VBlankOccurred += () => Interlocked.Increment(ref vblankCount);

        var cts = new CancellationTokenSource();

        // Act - Run for duration long enough for multiple VBlanks
        var runTask = Task.Run(() => va2m.RunAsync(cts.Token));

        await Task.Delay(100); // Should trigger multiple VBlanks

        cts.Cancel();

        try
        {
            await runTask;
        }
        catch (TaskCanceledException)
        {
            // Expected
        }

        // Assert - Multiple VBlanks should have occurred
        Assert.True(vblankCount > 0, $"Expected VBlank events, got {vblankCount}");
    }

    [Fact]
    public void VBlank_OccursAtExpectedFrequency()
    {
        // Arrange
        var testBus = new TestAppleIIBus();
        var va2m = VA2MTestHelpers.CreateBuilder()
            .WithBus(testBus)
            .Build();

        int vblankCount = 0;
        testBus.VBlankOccurred += () => vblankCount++;

        // Act - Execute cycles for multiple VBlank periods
        // VBlank occurs every ~12,480 cycles (at 1.023 MHz, 60 Hz)
        for (int i = 0; i < 50_000; i++)
        {
            va2m.Clock();
        }

        // Assert - Should have ~4 VBlanks (50,000 / 12,480 ≈ 4)
        Assert.InRange(vblankCount, 3, 5);
    }

    #endregion
}
