// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Services;

namespace Pandowdy.EmuCore.Tests;

/// <summary>
/// Tests for QueuedKeyHandler - buffered keyboard handler with automatic timed key feeding.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Test Coverage:</strong>
/// <list type="bullet">
/// <item>Constructor validation and initialization</item>
/// <item>Basic queue operations (enqueue, dequeue via timer)</item>
/// <item>Strobe bit management</item>
/// <item>Automatic key feeding after ClearStrobe</item>
/// <item>Timer behavior and timing</item>
/// <item>Paste scenario (multiple keys)</item>
/// <item>Thread safety (concurrent enqueue)</item>
/// <item>Disposal cleanup</item>
/// </list>
/// </para>
/// <para>
/// <strong>Timing Tests:</strong> Tests involving timer delays use Task.Delay with generous
/// margins (2-3x expected delay) to account for thread scheduling variability in test environment.
/// </para>
/// </remarks>
public class QueuedKeyHandlerTests
{
    #region Constructor Tests (4 tests)

    [Fact]
    public void Constructor_DefaultDelay_Initializes()
    {
        // Arrange & Act
        using var handler = new QueuedKeyHandler();

        // Assert
        Assert.Equal(0, handler.NumKeysPending());
        Assert.False(handler.StrobePending());
    }

    [Fact]
    public void Constructor_CustomDelay_Initializes()
    {
        // Arrange & Act
        using var handler = new QueuedKeyHandler(delayMilliseconds: 100);

        // Assert
        Assert.Equal(0, handler.NumKeysPending());
        Assert.False(handler.StrobePending());
    }

    [Fact]
    public void Constructor_DelayTooSmall_ThrowsArgumentOutOfRangeException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new QueuedKeyHandler(delayMilliseconds: 0));
    }

    [Fact]
    public void Constructor_DelayTooLarge_ThrowsArgumentOutOfRangeException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new QueuedKeyHandler(delayMilliseconds: 1001));
    }

    #endregion

    #region Basic Enqueue Tests (5 tests)

    [Fact]
    public void EnqueueKey_FirstKey_LoadsImmediatelyWithStrobe()
    {
        // Arrange
        using var handler = new QueuedKeyHandler();

        // Act
        handler.EnqueueKey(0x41); // 'A'

        // Assert
        Assert.True(handler.StrobePending());
        Assert.Equal(0xC1, handler.PeekCurrentKeyAndStrobe());
        Assert.Equal(0x41, handler.PeekCurrentKeyValue());
        Assert.Equal(1, handler.NumKeysPending()); // Current key with strobe set
    }

    [Fact]
    public void EnqueueKey_SecondKeyWhileStrobeSet_Queues()
    {
        // Arrange
        using var handler = new QueuedKeyHandler();
        handler.EnqueueKey(0x41); // 'A' - loaded immediately

        // Act
        handler.EnqueueKey(0x42); // 'B' - should queue

        // Assert
        Assert.True(handler.StrobePending()); // Still 'A'
        Assert.Equal(0xC1, handler.PeekCurrentKeyAndStrobe());
        Assert.Equal(2, handler.NumKeysPending()); // 'A' in latch + 'B' in queue
    }

    [Fact]
    public void EnqueueKey_MultipleKeys_AllQueue()
    {
        // Arrange
        using var handler = new QueuedKeyHandler();
        handler.EnqueueKey(0x41); // 'A' - loaded

        // Act
        handler.EnqueueKey(0x42); // 'B' - queued
        handler.EnqueueKey(0x43); // 'C' - queued
        handler.EnqueueKey(0x44); // 'D' - queued

        // Assert
        Assert.Equal(0xC1, handler.PeekCurrentKeyAndStrobe()); // Still 'A'
        Assert.Equal(4, handler.NumKeysPending()); // A in latch + B,C,D queued
    }

    [Fact]
    public void EnqueueKey_KeyWithStrobeBitSet_ClearsStrobeBeforeStore()
    {
        // Arrange
        using var handler = new QueuedKeyHandler();

        // Act - Pass 0xC1 (strobe already set)
        handler.EnqueueKey(0xC1);

        // Assert - Should store as 0x41, then set strobe when loaded
        Assert.Equal(0xC1, handler.PeekCurrentKeyAndStrobe());
        Assert.Equal(0x41, handler.PeekCurrentKeyValue());
    }

    [Fact]
    public void EnqueueKey_AfterStrobeCleared_LoadsImmediately()
    {
        // Arrange
        using var handler = new QueuedKeyHandler();
        handler.EnqueueKey(0x41); // 'A'
        handler.ClearStrobe();

        // Act
        handler.EnqueueKey(0x42); // 'B' - should load immediately (strobe cleared)

        // Assert
        Assert.True(handler.StrobePending());
        Assert.Equal(0xC2, handler.PeekCurrentKeyAndStrobe());
        Assert.Equal(1, handler.NumKeysPending()); // Current key with strobe
    }

    #endregion

    #region Strobe Management Tests (5 tests)

    [Fact]
    public void StrobePending_NoKey_ReturnsFalse()
    {
        // Arrange
        using var handler = new QueuedKeyHandler();

        // Act & Assert
        Assert.False(handler.StrobePending());
    }

    [Fact]
    public void StrobePending_KeyEnqueued_ReturnsTrue()
    {
        // Arrange
        using var handler = new QueuedKeyHandler();

        // Act
        handler.EnqueueKey(0x41);

        // Assert
        Assert.True(handler.StrobePending());
    }

    [Fact]
    public void ClearStrobe_RemovesStrobeBit()
    {
        // Arrange
        using var handler = new QueuedKeyHandler();
        handler.EnqueueKey(0x41); // 0xC1 with strobe

        // Act
        var result = handler.ClearStrobe();

        // Assert
        Assert.Equal(0x41, result); // Strobe cleared
        Assert.False(handler.StrobePending());
        Assert.Equal(0x41, handler.PeekCurrentKeyAndStrobe());
    }

    [Fact]
    public void PeekCurrentKeyValue_AlwaysReturnsClearedStrobe()
    {
        // Arrange
        using var handler = new QueuedKeyHandler();
        handler.EnqueueKey(0x41);

        // Act - Before and after clearing strobe
        var valueBefore = handler.PeekCurrentKeyValue();
        handler.ClearStrobe();
        var valueAfter = handler.PeekCurrentKeyValue();

        // Assert
        Assert.Equal(0x41, valueBefore);
        Assert.Equal(0x41, valueAfter);
    }

    [Fact]
    public void PeekCurrentKeyAndStrobe_ReflectsStrobeState()
    {
        // Arrange
        using var handler = new QueuedKeyHandler();
        handler.EnqueueKey(0x41);

        // Act
        var withStrobe = handler.PeekCurrentKeyAndStrobe();
        handler.ClearStrobe();
        var withoutStrobe = handler.PeekCurrentKeyAndStrobe();

        // Assert
        Assert.Equal(0xC1, withStrobe);
        Assert.Equal(0x41, withoutStrobe);
    }

    #endregion

    #region Automatic Key Feeding Tests (6 tests)

    [Fact]
    public async Task ClearStrobe_WithQueuedKeys_FeedsNextKeyAfterDelay()
    {
        // Arrange
        using var handler = new QueuedKeyHandler(delayMilliseconds: 20);
        handler.EnqueueKey(0x41); // 'A' - loaded
        handler.EnqueueKey(0x42); // 'B' - queued

        // Act
        handler.ClearStrobe(); // Should trigger timer
        await Task.Delay(50); // Wait for timer (2.5x delay for safety)

        // Assert
        Assert.True(handler.StrobePending()); // 'B' loaded with strobe
        Assert.Equal(0xC2, handler.PeekCurrentKeyAndStrobe());
        Assert.Equal(1, handler.NumKeysPending()); // 'B' in latch with strobe
    }

    [Fact]
    public async Task ClearStrobe_NoQueuedKeys_DoesNotFeedKey()
    {
        // Arrange
        using var handler = new QueuedKeyHandler(delayMilliseconds: 20);
        handler.EnqueueKey(0x41); // 'A' - loaded

        // Act
        handler.ClearStrobe(); // No keys queued
        await Task.Delay(50);

        // Assert
        Assert.False(handler.StrobePending()); // Still cleared
        Assert.Equal(0x41, handler.PeekCurrentKeyAndStrobe());
        Assert.Equal(0, handler.NumKeysPending()); // No keys pending
    }

    [Fact]
    public async Task AutoFeed_MultipleKeys_FeedsSequentially()
    {
        // Arrange
        using var handler = new QueuedKeyHandler(delayMilliseconds: 20);
        handler.EnqueueKey(0x41); // 'A'
        handler.EnqueueKey(0x42); // 'B'
        handler.EnqueueKey(0x43); // 'C'

        // Act & Assert - 'A' loaded immediately
        Assert.Equal(0xC1, handler.PeekCurrentKeyAndStrobe());
        Assert.Equal(3, handler.NumKeysPending()); // A in latch + B,C in queue

        // Clear 'A' and wait for 'B'
        handler.ClearStrobe();
        await Task.Delay(50);
        Assert.Equal(0xC2, handler.PeekCurrentKeyAndStrobe());
        Assert.Equal(2, handler.NumKeysPending()); // B in latch + C in queue

        // Clear 'B' and wait for 'C'
        handler.ClearStrobe();
        await Task.Delay(50);
        Assert.Equal(0xC3, handler.PeekCurrentKeyAndStrobe());
        Assert.Equal(1, handler.NumKeysPending()); // C in latch, queue empty
    }

    [Fact]
    public async Task AutoFeed_RespectsConfiguredDelay()
    {
        // Arrange - 100ms delay
        using var handler = new QueuedKeyHandler(delayMilliseconds: 100);
        handler.EnqueueKey(0x41); // 'A'
        handler.EnqueueKey(0x42); // 'B'

        // Act
        handler.ClearStrobe();
        await Task.Delay(50); // Half the delay

        // Assert - 'B' should NOT be loaded yet
        Assert.Equal(0x41, handler.PeekCurrentKeyAndStrobe()); // Still 'A' (cleared)
        Assert.False(handler.StrobePending());
        Assert.Equal(1, handler.NumKeysPending()); // Only 'B' in queue

        // Wait for full delay
        await Task.Delay(100);

        // Assert - Now 'B' should be loaded
        Assert.Equal(0xC2, handler.PeekCurrentKeyAndStrobe());
        Assert.True(handler.StrobePending());
        Assert.Equal(1, handler.NumKeysPending()); // 'B' in latch with strobe
    }

    [Fact]
    public async Task AutoFeed_ConcurrentEnqueue_ThreadSafe()
    {
        // Arrange
        using var handler = new QueuedKeyHandler(delayMilliseconds: 10);
        handler.EnqueueKey(0x41); // 'A' - loaded

        // Act - Enqueue many keys from multiple threads concurrently
        var tasks = new List<Task>();
        for (byte i = 0x42; i < 0x52; i++) // 'B' through 'Q' (16 keys)
        {
            byte key = i;
            tasks.Add(Task.Run(() => handler.EnqueueKey(key)));
        }
        await Task.WhenAll(tasks);

        // Assert - All 17 keys should be counted (A in latch + 16 queued)
        Assert.Equal(17, handler.NumKeysPending());

        // Trigger auto-feed and verify keys feed in SOME order (may not be sequential due to concurrent enqueue)
        handler.ClearStrobe();
        await Task.Delay(30);
        
        // Just verify a key was fed (don't check exact value due to concurrency)
        var firstFed = handler.PeekCurrentKeyValue();
        Assert.InRange(firstFed, (byte)0x42, (byte)0x51); // Should be one of B-Q
        Assert.True(handler.StrobePending()); // Should have strobe
        Assert.Equal(16, handler.NumKeysPending()); // One key in latch, 15 in queue

        handler.ClearStrobe();
        await Task.Delay(30);
        
        var secondFed = handler.PeekCurrentKeyValue();
        Assert.InRange(secondFed, (byte)0x42, (byte)0x51); // Should be another B-Q
        Assert.True(handler.StrobePending());
        Assert.Equal(15, handler.NumKeysPending()); // One key in latch, 14 in queue
    }

    [Fact]
    public async Task AutoFeed_ManualEnqueueDuringAutoFeed_IntegrationTest()
    {
        // Arrange - Simulate user typing while paste is active
        using var handler = new QueuedKeyHandler(delayMilliseconds: 20);
        handler.EnqueueKey(0x41); // 'A' - paste
        handler.EnqueueKey(0x42); // 'B' - paste

        // Act - Clear 'A', start auto-feed
        handler.ClearStrobe();
        await Task.Delay(30); // 'B' should be loaded

        // User types 'C' manually while 'B' is unread
        handler.EnqueueKey(0x43); // 'C' - should queue

        // Assert
        Assert.Equal(0xC2, handler.PeekCurrentKeyAndStrobe()); // 'B' current
        Assert.Equal(2, handler.NumKeysPending()); // 'B' in latch + 'C' queued

        // Clear 'B', 'C' should feed
        handler.ClearStrobe();
        await Task.Delay(30);
        Assert.Equal(0xC3, handler.PeekCurrentKeyAndStrobe());
        Assert.Equal(1, handler.NumKeysPending()); // 'C' in latch with strobe
    }

    #endregion

    #region Paste Scenario Tests (3 tests)

    [Fact]
    public async Task PasteScenario_ShortString_FeedsAllKeys()
    {
        // Arrange
        using var handler = new QueuedKeyHandler(delayMilliseconds: 10);
        string pasteText = "HI\r"; // 'H', 'I', Return

        // Act - Paste operation
        foreach (char c in pasteText)
        {
            handler.EnqueueKey((byte)c);
        }

        // Assert - First key loaded, rest queued
        Assert.Equal(0xC8, handler.PeekCurrentKeyAndStrobe()); // 'H'
        Assert.Equal(3, handler.NumKeysPending()); // H in latch + I,Return in queue

        // Clear and verify auto-feed
        handler.ClearStrobe();
        await Task.Delay(30);
        Assert.Equal(0xC9, handler.PeekCurrentKeyAndStrobe()); // 'I'
        Assert.Equal(2, handler.NumKeysPending()); // I in latch + Return in queue

        handler.ClearStrobe();
        await Task.Delay(30);
        Assert.Equal(0x8D, handler.PeekCurrentKeyAndStrobe()); // Return
        Assert.Equal(1, handler.NumKeysPending()); // Return in latch
    }

    [Fact]
    public async Task PasteScenario_LongString_AllKeysQueue()
    {
        // Arrange
        using var handler = new QueuedKeyHandler(delayMilliseconds: 10);
        string pasteText = "HELLO WORLD";

        // Act
        foreach (char c in pasteText)
        {
            handler.EnqueueKey((byte)c);
        }

        // Assert
        Assert.Equal(0xC8, handler.PeekCurrentKeyAndStrobe()); // 'H' loaded
        Assert.Equal(11, handler.NumKeysPending()); // H in latch + 10 in queue

        // Verify first few characters feed correctly
        handler.ClearStrobe(); await Task.Delay(30);
        Assert.Equal(0xC5, handler.PeekCurrentKeyAndStrobe()); // 'E'
        Assert.Equal(10, handler.NumKeysPending()); // E in latch + 9 in queue

        handler.ClearStrobe(); await Task.Delay(30);
        Assert.Equal(0xCC, handler.PeekCurrentKeyAndStrobe()); // 'L'
        Assert.Equal(9, handler.NumKeysPending()); // L in latch + 8 in queue

        handler.ClearStrobe(); await Task.Delay(30);
        Assert.Equal(0xCC, handler.PeekCurrentKeyAndStrobe()); // 'L'
        Assert.Equal(8, handler.NumKeysPending()); // L in latch + 7 in queue
    }

    [Fact]
    public void PasteScenario_EmptyBuffer_CanPaste()
    {
        // Arrange
        using var handler = new QueuedKeyHandler();

        // Act - Paste into empty buffer
        handler.EnqueueKey(0x41);
        handler.EnqueueKey(0x42);

        // Assert
        Assert.True(handler.StrobePending());
        Assert.Equal(2, handler.NumKeysPending()); // A in latch + B in queue
    }

    #endregion

    #region NumKeysPending Tests (3 tests)

    [Fact]
    public void NumKeysPending_EmptyQueue_ReturnsZero()
    {
        // Arrange
        using var handler = new QueuedKeyHandler();

        // Act & Assert
        Assert.Equal(0, handler.NumKeysPending());
    }

    [Fact]
    public void NumKeysPending_OneKeyLoaded_ReturnsOne()
    {
        // Arrange
        using var handler = new QueuedKeyHandler();

        // Act
        handler.EnqueueKey(0x41); // Loaded immediately with strobe

        // Assert
        Assert.Equal(1, handler.NumKeysPending()); // Current key with strobe
    }

    [Fact]
    public void NumKeysPending_MultipleKeysQueued_ReturnsCount()
    {
        // Arrange
        using var handler = new QueuedKeyHandler();
        handler.EnqueueKey(0x41); // Loaded

        // Act
        handler.EnqueueKey(0x42);
        handler.EnqueueKey(0x43);
        handler.EnqueueKey(0x44);

        // Assert
        Assert.Equal(4, handler.NumKeysPending()); // A in latch + B,C,D in queue
    }

    #endregion

    #region Disposal Tests (4 tests)

    [Fact]
    public void Dispose_StopsTimer()
    {
        // Arrange
        var handler = new QueuedKeyHandler(delayMilliseconds: 10);
        handler.EnqueueKey(0x41);
        handler.EnqueueKey(0x42);
        handler.ClearStrobe(); // Start timer

        // Act
        handler.Dispose();

        // Assert - No exception, timer stopped
        Assert.Equal(0, handler.NumKeysPending()); // Queue cleared
    }

    [Fact]
    public void Dispose_ClearsQueue()
    {
        // Arrange
        var handler = new QueuedKeyHandler();
        handler.EnqueueKey(0x41);
        handler.EnqueueKey(0x42);
        handler.EnqueueKey(0x43);

        // Act
        handler.Dispose();

        // Assert
        Assert.Equal(0, handler.NumKeysPending());
    }

    [Fact]
    public void Dispose_Idempotent()
    {
        // Arrange
        var handler = new QueuedKeyHandler();

        // Act
        handler.Dispose();
        handler.Dispose(); // Should not throw

        // Assert - No exception
        Assert.Equal(0, handler.NumKeysPending());
    }

    [Fact]
    public async Task Dispose_PreventsTimerCallback()
    {
        // Arrange
        var handler = new QueuedKeyHandler(delayMilliseconds: 10);
        handler.EnqueueKey(0x41);
        handler.EnqueueKey(0x42);
        handler.ClearStrobe(); // Start timer

        // Act
        handler.Dispose(); // Dispose before timer fires
        await Task.Delay(30); // Wait for timer window

        // Assert - Should not have fed 'B'
        // (Can't easily assert this, but Dispose should prevent callback)
        Assert.Equal(0, handler.NumKeysPending());
    }

    #endregion

    #region Edge Case Tests (4 tests)

    [Fact]
    public void EnqueueKey_ZeroValue_HandlesCorrectly()
    {
        // Arrange
        using var handler = new QueuedKeyHandler();

        // Act
        handler.EnqueueKey(0x00);

        // Assert
        Assert.Equal(0x80, handler.PeekCurrentKeyAndStrobe()); // 0x00 | 0x80
    }

    [Fact]
    public void EnqueueKey_MaxASCII_HandlesCorrectly()
    {
        // Arrange
        using var handler = new QueuedKeyHandler();

        // Act
        handler.EnqueueKey(0x7F); // DEL

        // Assert
        Assert.Equal(0xFF, handler.PeekCurrentKeyAndStrobe()); // 0x7F | 0x80
    }

    [Fact]
    public async Task ClearStrobe_CalledMultipleTimes_OnlyOneTimerActive()
    {
        // Arrange
        using var handler = new QueuedKeyHandler(delayMilliseconds: 20);
        handler.EnqueueKey(0x41);
        handler.EnqueueKey(0x42);
        handler.EnqueueKey(0x43);

        // Act - Clear multiple times rapidly
        handler.ClearStrobe();
        handler.ClearStrobe(); // Redundant
        handler.ClearStrobe(); // Redundant

        await Task.Delay(50);

        // Assert - Only 'B' should have been fed once
        Assert.Equal(0xC2, handler.PeekCurrentKeyAndStrobe());
        Assert.Equal(2, handler.NumKeysPending()); // 'B' in latch + 'C' queued
    }

    [Fact]
    public void EnqueueKey_AfterDisposal_SafelyIgnored()
    {
        // Arrange
        var handler = new QueuedKeyHandler();
        handler.Dispose();

        // Act - Should not throw
        handler.EnqueueKey(0x41);

        // Assert
        Assert.Equal(0, handler.NumKeysPending());
    }

        #endregion

        #region Reset Tests (6 tests)

        [Fact]
        public void Reset_ClearsStrobeBit_PreservesKeyValue()
        {
            // Arrange
            using var handler = new QueuedKeyHandler();
            handler.EnqueueKey(0x41); // 'A' with strobe set (0xC1)

            // Act
            handler.Reset();

            // Assert
            Assert.False(handler.StrobePending()); // Strobe cleared
            Assert.Equal(0x41, handler.PeekCurrentKeyValue()); // Key value preserved
            Assert.Equal(0x41, handler.PeekCurrentKeyAndStrobe()); // Full value without strobe
        }

        [Fact]
        public void Reset_ClearsPendingQueue()
        {
            // Arrange
            using var handler = new QueuedKeyHandler();
            handler.EnqueueKey(0x41); // 'A' - loads with strobe
            handler.EnqueueKey(0x42); // 'B' - queued
            handler.EnqueueKey(0x43); // 'C' - queued
            Assert.Equal(3, handler.NumKeysPending()); // 1 in latch + 2 in queue

            // Act
            handler.Reset();

            // Assert
            Assert.Equal(0, handler.NumKeysPending()); // Queue cleared
            Assert.False(handler.StrobePending()); // Strobe cleared
            Assert.Equal(0x41, handler.PeekCurrentKeyValue()); // Current key preserved (no strobe)
        }

        [Fact]
        public async Task Reset_CancelsTimerCallback()
        {
            // Arrange
            using var handler = new QueuedKeyHandler(delayMilliseconds: 10);
            handler.EnqueueKey(0x41); // 'A' - loads with strobe
            handler.EnqueueKey(0x42); // 'B' - queued
            handler.ClearStrobe(); // Start timer to feed 'B' after 10ms

            // Act - Reset before timer fires
            handler.Reset();
            await Task.Delay(30); // Wait past timer window

            // Assert - 'B' should not have been fed
            Assert.False(handler.StrobePending()); // Strobe still clear
            Assert.Equal(0x41, handler.PeekCurrentKeyValue()); // Still 'A', not 'B'
            Assert.Equal(0, handler.NumKeysPending()); // Queue cleared
        }

        [Fact]
        public void Reset_WhenNoKeyPresent_DoesNotThrow()
        {
            // Arrange
            using var handler = new QueuedKeyHandler();

            // Act & Assert - Should not throw
            handler.Reset();
            Assert.False(handler.StrobePending());
            Assert.Equal(0, handler.NumKeysPending());
        }

        [Fact]
        public void Reset_AllowsNewKeysAfterReset()
        {
            // Arrange
            using var handler = new QueuedKeyHandler();
            handler.EnqueueKey(0x41); // 'A'
            handler.EnqueueKey(0x42); // 'B'
            handler.Reset();

            // Act - Enqueue new keys after reset
            handler.EnqueueKey(0x43); // 'C'
            handler.EnqueueKey(0x44); // 'D'

            // Assert - New keys loaded normally
            Assert.True(handler.StrobePending());
            Assert.Equal(0x43, handler.PeekCurrentKeyValue()); // 'C' in latch
            Assert.Equal(2, handler.NumKeysPending()); // 'C' in latch + 'D' queued
            }

            [Fact]
            public async Task Reset_WithActiveTimer_PreventsFeedingQueuedKeys()
            {
                // Arrange - Use longer delay to ensure timer doesn't fire before reset
                using var handler = new QueuedKeyHandler(delayMilliseconds: 100);
                handler.EnqueueKey(0x41); // 'A'
                handler.EnqueueKey(0x42); // 'B' - queued
                handler.EnqueueKey(0x43); // 'C' - queued
                handler.ClearStrobe(); // Start timer

                // Act - Reset immediately (well before 100ms timer fires)
                handler.Reset();
                await Task.Delay(150); // Wait past timer window

                // Assert - Timer was canceled, 'B' not fed despite waiting past delay
                Assert.False(handler.StrobePending());
                Assert.Equal(0x41, handler.PeekCurrentKeyValue()); // Still 'A', not 'B'
                Assert.Equal(0, handler.NumKeysPending()); // Queue cleared
            }

            #endregion
        }
