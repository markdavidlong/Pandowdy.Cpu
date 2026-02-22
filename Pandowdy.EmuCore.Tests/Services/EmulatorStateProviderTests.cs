// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Machine;
using System.Reactive.Linq;

namespace Pandowdy.EmuCore.Tests.Services;

/// <summary>
/// Comprehensive tests for EmulatorStateProvider - manages emulator state
/// snapshots using a reactive BehaviorSubject.
/// 
/// Tests verify state updates, observable streams, and the reactive behavior
/// of the state provider.
/// </summary>
public class EmulatorStateProviderTests
{
    #region Constructor and Initialization Tests (3 tests)

    [Fact]
    public void Constructor_InitializesWithDefaultState()
    {
        // Arrange & Act
        var provider = new EmulatorStateProvider();

        // Assert
        var current = provider.GetCurrent();
        Assert.Equal(0, current.PC);
        Assert.Equal(0, current.SP);
        Assert.Equal(0UL, current.Cycles);
        Assert.Null(current.LineNumber);
        Assert.False(current.IsRunning);
        Assert.False(current.IsPaused);
    }

    [Fact]
    public void Stream_IsNotNull()
    {
        // Arrange
        var provider = new EmulatorStateProvider();

        // Act
        var stream = provider.Stream;

        // Assert
        Assert.NotNull(stream);
    }

    [Fact]
    public void Stream_EmitsInitialValue()
    {
        // Arrange
        var provider = new EmulatorStateProvider();
        StateSnapshot? receivedState = null;

        // Act
        using var subscription = provider.Stream.Subscribe(state => receivedState = state);

        // Assert
        Assert.NotNull(receivedState);
        Assert.Equal(0, receivedState.PC);
    }

    #endregion

    #region GetCurrent Tests (5 tests)

    [Fact]
    public void GetCurrent_ReturnsDefaultStateInitially()
    {
        // Arrange
        var provider = new EmulatorStateProvider();

        // Act
        var state = provider.GetCurrent();

        // Assert
        Assert.Equal(0, state.PC);
        Assert.Equal(0, state.SP);
        Assert.Equal(0UL, state.Cycles);
    }

    [Fact]
    public void GetCurrent_ReturnsLastUpdatedState()
    {
        // Arrange
        var provider = new EmulatorStateProvider();
        var newState = new StateSnapshot(0x1234, 0xFD, 1000, null, true, false);

        // Act
        provider.Update(newState);
        var current = provider.GetCurrent();

        // Assert
        Assert.Equal(0x1234, current.PC);
        Assert.Equal(0xFD, current.SP);
        Assert.Equal(1000UL, current.Cycles);
    }

    [Fact]
    public void GetCurrent_ReflectsMultipleUpdates()
    {
        // Arrange
        var provider = new EmulatorStateProvider();

        // Act
        provider.Update(new StateSnapshot(0x1000, 0xFF, 100, null, false, false));
        provider.Update(new StateSnapshot(0x2000, 0xFE, 200, null, true, false));
        provider.Update(new StateSnapshot(0x3000, 0xFD, 300, null, true, true));
        var current = provider.GetCurrent();

        // Assert - Should have last update
        Assert.Equal(0x3000, current.PC);
        Assert.Equal(0xFD, current.SP);
        Assert.Equal(300UL, current.Cycles);
        Assert.True(current.IsRunning);
        Assert.True(current.IsPaused);
    }

    [Fact]
    public void GetCurrent_WithLineNumber_PreservesValue()
    {
        // Arrange
        var provider = new EmulatorStateProvider();
        var newState = new StateSnapshot(0x1234, 0xFD, 1000, 42, true, false);

        // Act
        provider.Update(newState);
        var current = provider.GetCurrent();

        // Assert
        Assert.Equal(42, current.LineNumber);
    }

    [Fact]
    public void GetCurrent_CalledMultipleTimes_ReturnsSameState()
    {
        // Arrange
        var provider = new EmulatorStateProvider();
        provider.Update(new StateSnapshot(0x1234, 0xFD, 1000, null, true, false));

        // Act
        var state1 = provider.GetCurrent();
        var state2 = provider.GetCurrent();
        var state3 = provider.GetCurrent();

        // Assert
        Assert.Equal(state1, state2);
        Assert.Equal(state2, state3);
    }

    #endregion

    #region Update Tests (8 tests)

    [Fact]
    public void Update_ChangesCurrentState()
    {
        // Arrange
        var provider = new EmulatorStateProvider();
        var newState = new StateSnapshot(0x5678, 0xAB, 5000, null, false, true);

        // Act
        provider.Update(newState);
        var current = provider.GetCurrent();

        // Assert
        Assert.Equal(0x5678, current.PC);
        Assert.Equal(0xAB, current.SP);
        Assert.Equal(5000UL, current.Cycles);
    }

    [Fact]
    public void Update_EmitsToStream()
    {
        // Arrange
        var provider = new EmulatorStateProvider();
        StateSnapshot? receivedState = null;
        using var subscription = provider.Stream.Skip(1).Subscribe(state => receivedState = state);

        var newState = new StateSnapshot(0x9999, 0x88, 9999, null, true, true);

        // Act
        provider.Update(newState);

        // Assert
        Assert.NotNull(receivedState);
        Assert.Equal(0x9999, receivedState.PC);
        Assert.Equal(0x88, receivedState.SP);
    }

    [Fact]
    public void Update_MultipleSubscribers_AllReceiveUpdate()
    {
        // Arrange
        var provider = new EmulatorStateProvider();
        StateSnapshot? received1 = null;
        StateSnapshot? received2 = null;
        StateSnapshot? received3 = null;

        using var sub1 = provider.Stream.Skip(1).Subscribe(state => received1 = state);
        using var sub2 = provider.Stream.Skip(1).Subscribe(state => received2 = state);
        using var sub3 = provider.Stream.Skip(1).Subscribe(state => received3 = state);

        var newState = new StateSnapshot(0x1111, 0x22, 1111, null, false, false);

        // Act
        provider.Update(newState);

        // Assert
        Assert.NotNull(received1);
        Assert.NotNull(received2);
        Assert.NotNull(received3);
        Assert.Equal(0x1111, received1.PC);
        Assert.Equal(0x1111, received2.PC);
        Assert.Equal(0x1111, received3.PC);
    }

    [Fact]
    public void Update_RapidUpdates_AllEmitted()
    {
        // Arrange
        var provider = new EmulatorStateProvider();
        var receivedStates = new List<StateSnapshot>();
        using var subscription = provider.Stream.Skip(1).Subscribe(state => receivedStates.Add(state));

        // Act - Rapid updates
        for (ushort i = 0; i < 100; i++)
        {
            provider.Update(new StateSnapshot(i, (byte)i, i, null, i % 2 == 0, i % 3 == 0));
        }

        // Assert
        Assert.Equal(100, receivedStates.Count);
        Assert.Equal(99, receivedStates[99].PC);
    }

    [Fact]
    public void Update_WithRunningState_PreservesFlag()
    {
        // Arrange
        var provider = new EmulatorStateProvider();
        var runningState = new StateSnapshot(0x1000, 0xFF, 100, null, true, false);

        // Act
        provider.Update(runningState);
        var current = provider.GetCurrent();

        // Assert
        Assert.True(current.IsRunning);
        Assert.False(current.IsPaused);
    }

    [Fact]
    public void Update_WithPausedState_PreservesFlag()
    {
        // Arrange
        var provider = new EmulatorStateProvider();
        var pausedState = new StateSnapshot(0x1000, 0xFF, 100, null, false, true);

        // Act
        provider.Update(pausedState);
        var current = provider.GetCurrent();

        // Assert
        Assert.False(current.IsRunning);
        Assert.True(current.IsPaused);
    }

    [Fact]
    public void Update_IncrementingCycles_TracksCorrectly()
    {
        // Arrange
        var provider = new EmulatorStateProvider();

        // Act - Simulate execution with incrementing cycles
        for (ulong i = 0; i < 1000; i++)
        {
            provider.Update(new StateSnapshot(0x1000, 0xFF, i, null, true, false));
        }
        var current = provider.GetCurrent();

        // Assert
        Assert.Equal(999UL, current.Cycles);
    }

    [Fact]
    public void Update_StateWithLineNumber_TracksCorrectly()
    {
        // Arrange
        var provider = new EmulatorStateProvider();

        // Act
        provider.Update(new StateSnapshot(0x1000, 0xFF, 100, 10, true, false));
        provider.Update(new StateSnapshot(0x1010, 0xFE, 200, 20, true, false));
        provider.Update(new StateSnapshot(0x1020, 0xFD, 300, 30, true, false));
        var current = provider.GetCurrent();

        // Assert
        Assert.Equal(30, current.LineNumber);
    }

    #endregion

    #region Stream Observable Tests (6 tests)

    [Fact]
    public void Stream_BehaviorSubject_EmitsCurrentValueToNewSubscriber()
    {
        // Arrange
        var provider = new EmulatorStateProvider();
        provider.Update(new StateSnapshot(0xAAAA, 0xBB, 5555, null, true, false));

        StateSnapshot? receivedState = null;

        // Act - Subscribe AFTER update
        using var subscription = provider.Stream.Subscribe(state => receivedState = state);

        // Assert - New subscriber gets current value immediately
        Assert.NotNull(receivedState);
        Assert.Equal(0xAAAA, receivedState.PC);
    }

    [Fact]
    public void Stream_CanBeQueriedMultipleTimes()
    {
        // Arrange
        var provider = new EmulatorStateProvider();

        // Act
        var stream1 = provider.Stream;
        var stream2 = provider.Stream;
        var stream3 = provider.Stream;

        // Assert - Should be same observable
        Assert.Same(stream1, stream2);
        Assert.Same(stream2, stream3);
    }

    [Fact]
    public void Stream_SupportsLinqOperators()
    {
        // Arrange
        var provider = new EmulatorStateProvider();
        var highCycleStates = new List<StateSnapshot>();

        using var subscription = provider.Stream
            .Where(state => state.Cycles > 500)
            .Subscribe(state => highCycleStates.Add(state));

        // Act
        provider.Update(new StateSnapshot(0x1000, 0xFF, 100, null, true, false));
        provider.Update(new StateSnapshot(0x1001, 0xFF, 600, null, true, false));
        provider.Update(new StateSnapshot(0x1002, 0xFF, 200, null, true, false));
        provider.Update(new StateSnapshot(0x1003, 0xFF, 700, null, true, false));

        // Assert - Only states with cycles > 500
        Assert.Equal(2, highCycleStates.Count);
        Assert.Equal(600UL, highCycleStates[0].Cycles);
        Assert.Equal(700UL, highCycleStates[1].Cycles);
    }

    [Fact]
    public void Stream_SupportsDistinctUntilChanged()
    {
        // Arrange
        var provider = new EmulatorStateProvider();
        var distinctStates = new List<StateSnapshot>();

        using var subscription = provider.Stream
            .Skip(1) // Skip initial
            .DistinctUntilChanged(state => state.PC)
            .Subscribe(state => distinctStates.Add(state));

        // Act - Update with same PC multiple times
        provider.Update(new StateSnapshot(0x1000, 0xFF, 100, null, true, false));
        provider.Update(new StateSnapshot(0x1000, 0xFE, 200, null, true, false)); // Same PC
        provider.Update(new StateSnapshot(0x1000, 0xFD, 300, null, true, false)); // Same PC
        provider.Update(new StateSnapshot(0x2000, 0xFC, 400, null, true, false)); // Different PC

        // Assert - Should only get distinct PC values
        Assert.Equal(2, distinctStates.Count);
        Assert.Equal(0x1000, distinctStates[0].PC);
        Assert.Equal(0x2000, distinctStates[1].PC);
    }

    [Fact]
    public void Stream_Unsubscribe_StopsReceivingUpdates()
    {
        // Arrange
        var provider = new EmulatorStateProvider();
        var receivedCount = 0;

        var subscription = provider.Stream.Skip(1).Subscribe(_ => receivedCount++);

        // Act
        provider.Update(new StateSnapshot(0x1000, 0xFF, 100, null, true, false));
        provider.Update(new StateSnapshot(0x2000, 0xFF, 200, null, true, false));
        
        subscription.Dispose(); // Unsubscribe
        
        provider.Update(new StateSnapshot(0x3000, 0xFF, 300, null, true, false));
        provider.Update(new StateSnapshot(0x4000, 0xFF, 400, null, true, false));

        // Assert - Should only have received first 2 updates
        Assert.Equal(2, receivedCount);
    }

    [Fact]
    public void Stream_MultipleUnsubscribes_DoesNotAffectOthers()
    {
        // Arrange
        var provider = new EmulatorStateProvider();
        var count1 = 0;
        var count2 = 0;
        var count3 = 0;

        var sub1 = provider.Stream.Skip(1).Subscribe(_ => count1++);
        var sub2 = provider.Stream.Skip(1).Subscribe(_ => count2++);
        var sub3 = provider.Stream.Skip(1).Subscribe(_ => count3++);

        // Act
        provider.Update(new StateSnapshot(0x1000, 0xFF, 100, null, true, false));
        sub2.Dispose(); // Unsubscribe subscriber 2
        provider.Update(new StateSnapshot(0x2000, 0xFF, 200, null, true, false));

        // Assert
        Assert.Equal(2, count1); // Still subscribed
        Assert.Equal(1, count2); // Unsubscribed after first update
        Assert.Equal(2, count3); // Still subscribed
    }

    #endregion

    #region Request Methods Tests (3 tests)

    [Fact]
    public void RequestPause_DoesNotThrow()
    {
        // Arrange
        var provider = new EmulatorStateProvider();

        // Act & Assert - Should not throw (placeholder implementation)
        provider.RequestPause();
    }

    [Fact]
    public void RequestContinue_DoesNotThrow()
    {
        // Arrange
        var provider = new EmulatorStateProvider();

        // Act & Assert - Should not throw (placeholder implementation)
        provider.RequestContinue();
    }

    [Fact]
    public void RequestStep_DoesNotThrow()
    {
        // Arrange
        var provider = new EmulatorStateProvider();

        // Act & Assert - Should not throw (placeholder implementation)
        provider.RequestStep();
    }

    #endregion

    #region StateSnapshot Record Tests (5 tests)

    [Fact]
    public void StateSnapshot_Equality_WorksCorrectly()
    {
        // Arrange
        var state1 = new StateSnapshot(0x1234, 0xFD, 1000, 10, true, false);
        var state2 = new StateSnapshot(0x1234, 0xFD, 1000, 10, true, false);
        var state3 = new StateSnapshot(0x5678, 0xFD, 1000, 10, true, false);

        // Act & Assert
        Assert.Equal(state1, state2);
        Assert.NotEqual(state1, state3);
    }

    [Fact]
    public void StateSnapshot_WithExpression_CreatesModifiedCopy()
    {
        // Arrange
        var original = new StateSnapshot(0x1234, 0xFD, 1000, null, true, false);

        // Act
        var modified = original with { PC = 0x5678, Cycles = 2000 };

        // Assert
        Assert.Equal(0x1234, original.PC);
        Assert.Equal(1000UL, original.Cycles);
        Assert.Equal(0x5678, modified.PC);
        Assert.Equal(2000UL, modified.Cycles);
        Assert.Equal(original.SP, modified.SP);
    }

    [Fact]
    public void StateSnapshot_Deconstruction_WorksCorrectly()
    {
        // Arrange
        var state = new StateSnapshot(0x1234, 0xFD, 1000, 42, true, false);

        // Act
        var (pc, sp, cycles, lineNumber, isRunning, isPaused) = state;

        // Assert
        Assert.Equal(0x1234, pc);
        Assert.Equal(0xFD, sp);
        Assert.Equal(1000UL, cycles);
        Assert.Equal(42, lineNumber);
        Assert.True(isRunning);
        Assert.False(isPaused);
    }

    [Fact]
    public void StateSnapshot_ToString_ReturnsRepresentation()
    {
        // Arrange
        var state = new StateSnapshot(0x1234, 0xFD, 1000, null, true, false);

        // Act
        var result = state.ToString();

        // Assert
        Assert.NotNull(result);
        // PC 0x1234 = 4660 in decimal, ToString uses decimal representation
        Assert.Contains("4660", result);
    }

    [Fact]
    public void StateSnapshot_HashCode_ConsistentForEqualStates()
    {
        // Arrange
        var state1 = new StateSnapshot(0x1234, 0xFD, 1000, null, true, false);
        var state2 = new StateSnapshot(0x1234, 0xFD, 1000, null, true, false);

        // Act
        var hash1 = state1.GetHashCode();
        var hash2 = state2.GetHashCode();

        // Assert
        Assert.Equal(hash1, hash2);
    }

    #endregion

    #region Integration Tests (3 tests)

    [Fact]
    public void Integration_SimulatedExecution_TracksStateCorrectly()
    {
        // Arrange
        var provider = new EmulatorStateProvider();
        var stateHistory = new List<StateSnapshot>();
        using var subscription = provider.Stream.Skip(1).Subscribe(state => stateHistory.Add(state));

        // Act - Simulate 100 cycles of execution
        ushort pc = 0x1000;
        for (ulong cycle = 0; cycle < 100; cycle++)
        {
            var state = new StateSnapshot(pc, 0xFD, cycle, null, true, false);
            provider.Update(state);
            pc++;
        }

        // Assert
        Assert.Equal(100, stateHistory.Count);
        Assert.Equal(0x1000, stateHistory[0].PC);
        Assert.Equal(0x1063, stateHistory[99].PC);
        Assert.Equal(99UL, stateHistory[99].Cycles);
    }

    [Fact]
    public void Integration_PauseAndResume_TracksStateChanges()
    {
        // Arrange
        var provider = new EmulatorStateProvider();
        var states = new List<StateSnapshot>();
        using var subscription = provider.Stream.Skip(1).Subscribe(state => states.Add(state));

        // Act - Simulate pause/resume
        provider.Update(new StateSnapshot(0x1000, 0xFF, 100, null, true, false)); // Running
        provider.Update(new StateSnapshot(0x1001, 0xFF, 101, null, false, true)); // Paused
        provider.Update(new StateSnapshot(0x1002, 0xFF, 102, null, true, false)); // Running again

        // Assert
        Assert.Equal(3, states.Count);
        Assert.True(states[0].IsRunning);
        Assert.True(states[1].IsPaused);
        Assert.True(states[2].IsRunning);
    }

    [Fact]
    public void Integration_ObservablePatterns_WorkCorrectly()
    {
        // Arrange
        var provider = new EmulatorStateProvider();
        var runningStates = new List<StateSnapshot>();

        using var subscription = provider.Stream
            .Skip(1)
            .Where(state => state.IsRunning)
            .Take(5)
            .Subscribe(state => runningStates.Add(state));

        // Act - Mix of running and paused states
        for (int i = 0; i < 10; i++)
        {
            provider.Update(new StateSnapshot((ushort)(0x1000 + i), 0xFF, (ulong)i, null, i % 2 == 0, i % 2 == 1));
        }

        // Assert - Should only have 5 running states
        Assert.Equal(5, runningStates.Count);
        Assert.All(runningStates, state => Assert.True(state.IsRunning));
    }

    #endregion
}
