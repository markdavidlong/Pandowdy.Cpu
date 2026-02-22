// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Input;
using Pandowdy.EmuCore.Machine;

namespace Pandowdy.EmuCore.Tests;

/// <summary>
/// Tests for SingularKeyHandler keyboard emulation.
/// </summary>
/// <remarks>
/// These tests verify the Apple IIe keyboard hardware emulation including:
/// - Single-key latch behavior (no buffering)
/// - Strobe bit mechanism (bit 7)
/// - Key overwrite semantics (authentic Apple IIe behavior)
/// - Interface segregation (IKeyboardReader vs IKeyboardSetter)
/// </remarks>
public class SingularKeyHandlerTests
{
    #region Basic Keyboard State Tests

    [Fact]
    public void Constructor_InitializesWithNoKeyPending()
    {
        // Arrange & Act
        var handler = new SingularKeyHandler();

        // Assert
        Assert.False(handler.StrobePending());
        Assert.Equal(0, handler.PeekCurrentKeyValue());
        Assert.Equal(0, handler.PeekCurrentKeyAndStrobe());
    }

    [Fact]
    public void EnqueueKey_SetsStrobeBit()
    {
        // Arrange
        var handler = new SingularKeyHandler();

        // Act
        handler.EnqueueKey(0x41); // 'A' key

        // Assert
        Assert.True(handler.StrobePending());
    }

    [Fact]
    public void EnqueueKey_StoresKeyValue()
    {
        // Arrange
        var handler = new SingularKeyHandler();

        // Act
        handler.EnqueueKey(0x41); // 'A'

        // Assert
        Assert.Equal(0x41, handler.PeekCurrentKeyValue()); // 7-bit value
        Assert.Equal(0xC1, handler.PeekCurrentKeyAndStrobe()); // With strobe bit
    }

    #endregion

    #region Strobe Bit Tests

    [Fact]
    public void PeekCurrentKeyAndStrobe_ReturnsValueWithStrobeBit()
    {
        // Arrange
        var handler = new SingularKeyHandler();
        handler.EnqueueKey(0x41); // 'A'

        // Act
        byte result = handler.PeekCurrentKeyAndStrobe();

        // Assert
        Assert.Equal(0xC1, result); // 0x41 | 0x80
    }

    [Fact]
    public void PeekCurrentKeyValue_Returns7BitValueOnly()
    {
        // Arrange
        var handler = new SingularKeyHandler();
        handler.EnqueueKey(0x41); // 'A'

        // Act
        byte result = handler.PeekCurrentKeyValue();

        // Assert
        Assert.Equal(0x41, result); // Strobe bit masked off
    }

    [Fact]
    public void StrobePending_ReturnsTrueWhenKeyEnqueued()
    {
        // Arrange
        var handler = new SingularKeyHandler();

        // Act
        handler.EnqueueKey(0x41);

        // Assert
        Assert.True(handler.StrobePending());
    }

    [Fact]
    public void StrobePending_ReturnsFalseAfterClear()
    {
        // Arrange
        var handler = new SingularKeyHandler();
        handler.EnqueueKey(0x41);

        // Act
        handler.ClearStrobe();

        // Assert
        Assert.False(handler.StrobePending());
    }

    #endregion

    #region FetchPendingAndClearStrobe Tests

    [Fact]
    public void ClearStrobe_ReturnsKeyWhenStrobePending()
    {
        // Arrange
        var handler = new SingularKeyHandler();
        handler.EnqueueKey(0x41); // 'A'

        // Act
        byte result = handler.ClearStrobe();

        // Assert
        Assert.Equal(0x41, result); // Returns 7-bit value after clearing strobe
    }

    [Fact]
    public void ClearStrobe_ClearsStrobeBit()
    {
        // Arrange
        var handler = new SingularKeyHandler();
        handler.EnqueueKey(0x41);

        // Act
        handler.ClearStrobe();

        // Assert
        Assert.False(handler.StrobePending());
        Assert.Equal(0x41, handler.PeekCurrentKeyAndStrobe()); // No strobe bit
    }

    [Fact]
    public void ClearStrobe_ReturnsKeyValueWhenNoStrobePending()
    {
        // Arrange
        var handler = new SingularKeyHandler();
        handler.EnqueueKey(0x41);
        handler.ClearStrobe(); // Clear strobe

        // Act
        byte result = handler.ClearStrobe(); // Second call

        // Assert
        Assert.Equal(0x41, result); // Returns key value even when strobe already clear
    }

    [Fact]
    public void ClearStrobe_IsIdempotent()
    {
        // Arrange
        var handler = new SingularKeyHandler();
        handler.EnqueueKey(0x41);

        // Act - Multiple calls
        byte first = handler.ClearStrobe();
        byte second = handler.ClearStrobe();
        byte third = handler.ClearStrobe();

        // Assert - All return same key value
        Assert.Equal(0x41, first);
        Assert.Equal(0x41, second);
        Assert.Equal(0x41, third);
        Assert.False(handler.StrobePending()); // Strobe stays cleared
    }

    #endregion

    #region Key Overwrite Tests (Apple IIe Authentic Behavior)

    [Fact]
    public void EnqueueKey_OverwritesUnreadKey()
    {
        // Arrange
        var handler = new SingularKeyHandler();
        handler.EnqueueKey(0x41); // 'A'

        // Act - Enqueue another key before reading first
        handler.EnqueueKey(0x42); // 'B'

        // Assert - Only 'B' should be present
        Assert.Equal(0x42, handler.PeekCurrentKeyValue());
        Assert.True(handler.StrobePending());
    }

    [Fact]
    public void EnqueueKey_MultipleOverwrites_OnlyLastKeyRemains()
    {
        // Arrange
        var handler = new SingularKeyHandler();

        // Act - Rapid keypresses without reading
        handler.EnqueueKey(0x41); // 'A'
        handler.EnqueueKey(0x42); // 'B'
        handler.EnqueueKey(0x43); // 'C'
        handler.EnqueueKey(0x44); // 'D'

        // Assert - Only 'D' remains (Apple IIe has no key buffer)
        Assert.Equal(0x44, handler.PeekCurrentKeyValue());
        Assert.True(handler.StrobePending());
    }

    [Fact]
    public void EnqueueKey_AfterStrobeCleared_OverwritesKey()
    {
        // Arrange
        var handler = new SingularKeyHandler();
        handler.EnqueueKey(0x41); // 'A'
        handler.ClearStrobe(); // Clear strobe

        // Act - Enqueue new key
        handler.EnqueueKey(0x42); // 'B'

        // Assert
        Assert.Equal(0x42, handler.PeekCurrentKeyValue());
        Assert.True(handler.StrobePending()); // Strobe set again
    }

    #endregion

    #region Control Character Tests

    [Fact]
    public void EnqueueKey_AcceptsControlCharacters()
    {
        // Arrange
        var handler = new SingularKeyHandler();

        // Act
        handler.EnqueueKey(0x0D); // Return key

        // Assert
        Assert.Equal(0x0D, handler.PeekCurrentKeyValue());
        Assert.Equal(0x8D, handler.PeekCurrentKeyAndStrobe());
    }

    [Fact]
    public void EnqueueKey_AcceptsEscapeKey()
    {
        // Arrange
        var handler = new SingularKeyHandler();

        // Act
        handler.EnqueueKey(0x1B); // Escape

        // Assert
        Assert.Equal(0x1B, handler.PeekCurrentKeyValue());
        Assert.True(handler.StrobePending());
    }

    [Theory]
    [InlineData(0x00)] // Ctrl+@
    [InlineData(0x01)] // Ctrl+A
    [InlineData(0x0D)] // Return
    [InlineData(0x1B)] // Escape
    [InlineData(0x20)] // Space
    [InlineData(0x41)] // 'A'
    [InlineData(0x7F)] // DEL
    public void EnqueueKey_Handles7BitRange(byte keyCode)
    {
        // Arrange
        var handler = new SingularKeyHandler();

        // Act
        handler.EnqueueKey(keyCode);

        // Assert
        Assert.Equal(keyCode, handler.PeekCurrentKeyValue());
        Assert.True(handler.StrobePending());
    }

    #endregion

    #region Strobe Bit Force-Set Tests

    [Fact]
    public void EnqueueKey_ForcesStrobeBitOn_EvenIfAlreadySet()
    {
        // Arrange
        var handler = new SingularKeyHandler();

        // Act - Enqueue with strobe already set
        handler.EnqueueKey(0xC1); // 'A' with strobe already on

        // Assert - Should still have strobe set
        Assert.Equal(0x41, handler.PeekCurrentKeyValue());
        Assert.Equal(0xC1, handler.PeekCurrentKeyAndStrobe());
        Assert.True(handler.StrobePending());
    }

    #endregion

    #region Peek vs Fetch Tests

    [Fact]
    public void PeekOperations_DoNotModifyState()
    {
        // Arrange
        var handler = new SingularKeyHandler();
        handler.EnqueueKey(0x41);

        // Act - Multiple peeks
        byte peek1 = handler.PeekCurrentKeyAndStrobe();
        byte peek2 = handler.PeekCurrentKeyAndStrobe();
        byte value1 = handler.PeekCurrentKeyValue();
        byte value2 = handler.PeekCurrentKeyValue();

        // Assert - State unchanged
        Assert.Equal(0xC1, peek1);
        Assert.Equal(0xC1, peek2);
        Assert.Equal(0x41, value1);
        Assert.Equal(0x41, value2);
        Assert.True(handler.StrobePending());
    }

    [Fact]
    public void FetchOperation_ModifiesState()
    {
        // Arrange
        var handler = new SingularKeyHandler();
        handler.EnqueueKey(0x41);

        // Act
        byte valueBefore = handler.PeekCurrentKeyValue();
        byte cleared = handler.ClearStrobe();
        byte valueAfter = handler.PeekCurrentKeyValue();

        // Assert
        Assert.Equal(0x41, valueBefore);
        Assert.Equal(0x41, cleared);
        Assert.Equal(0x41, valueAfter); // Value preserved
        Assert.False(handler.StrobePending()); // Strobe cleared
    }

    #endregion

    #region Interface Segregation Tests

    [Fact]
    public void IKeyboardReader_ExposesReadOnlyOperations()
    {
        // Arrange - Testing that the interface works correctly
        var handler = new SingularKeyHandler();

        // Act - Use handler directly (implements IKeyboardSetter)
        handler.EnqueueKey(0x41);

        // Assert - Reader methods work via concrete type (implements IKeyboardReader)
        Assert.Equal(0x41, handler.PeekCurrentKeyValue());
        Assert.True(handler.StrobePending());
    }

    [Fact]
    public void IKeyboardSetter_ExposesWriteOnlyOperations()
    {
        // Arrange - Testing that the interface works correctly
        var handler = new SingularKeyHandler();

        // Act - Use handler directly (implements IKeyboardSetter)
        handler.EnqueueKey(0x42);

        // Assert - Verify via reader methods (implements IKeyboardReader)
        Assert.Equal(0x42, handler.PeekCurrentKeyValue());
    }

    [Fact]
    public void BothInterfaces_ShareSameState()
    {
        // Arrange - Verify shared state across interface views
        var handler = new SingularKeyHandler();

        // Act - Use setter method
        handler.EnqueueKey(0x43);

        // Assert - Reader methods see the same state
        Assert.Equal(0x43, handler.PeekCurrentKeyValue());
        Assert.Equal(0xC3, handler.PeekCurrentKeyAndStrobe());
    }

    #endregion

    #region Apple IIe Software Pattern Tests

    [Fact]
    public void AppleIIe_WaitForKeyPattern()
    {
        // Arrange - Simulate Apple IIe GETKEY routine
        var handler = new SingularKeyHandler();
        
        // Simulate: Loop while no key (bit 7 clear)
        Assert.False(handler.StrobePending()); // No key yet
        
        // Act - User presses 'A'
        handler.EnqueueKey(0x41);
        
        // Simulate: LDA $C000 (check for key)
        Assert.True(handler.StrobePending()); // Key available
        
        // Simulate: STA $C010 (clear strobe)
        byte key = handler.ClearStrobe();
        
        // Assert
        Assert.Equal(0x41, key);
        Assert.False(handler.StrobePending()); // Strobe cleared
    }

    [Fact]
    public void AppleIIe_PollingPattern_NoBlockingOnRead()
    {
        // Arrange
        var handler = new SingularKeyHandler();

        // Act - Poll keyboard when no key pressed
        bool hasKey1 = handler.StrobePending();
        byte value1 = handler.ClearStrobe(); // Safe to call even when no key
        
        // Press key
        handler.EnqueueKey(0x41);
        
        bool hasKey2 = handler.StrobePending();
        byte value2 = handler.ClearStrobe();

        // Assert
        Assert.False(hasKey1);
        Assert.Equal(0, value1); // No key pressed, returns 0
        Assert.True(hasKey2);
        Assert.Equal(0x41, value2);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void EnqueueKey_WithZeroValue_StillSetsStrobe()
    {
        // Arrange
        var handler = new SingularKeyHandler();

        // Act
        handler.EnqueueKey(0x00); // Ctrl+@

        // Assert
        Assert.Equal(0x00, handler.PeekCurrentKeyValue());
        Assert.Equal(0x80, handler.PeekCurrentKeyAndStrobe()); // Strobe set
        Assert.True(handler.StrobePending());
    }

    [Fact]
    public void ClearStrobe_PreservesKeyValue()
    {
        // Arrange
        var handler = new SingularKeyHandler();
        handler.EnqueueKey(0x45); // 'E'

        // Act
        handler.ClearStrobe();

        // Assert - Key value preserved, only strobe cleared
        Assert.Equal(0x45, handler.PeekCurrentKeyValue());
        Assert.Equal(0x45, handler.PeekCurrentKeyAndStrobe()); // No strobe bit
    }

        #endregion

        #region Reset Tests

        [Fact]
        public void Reset_ClearsStrobeBit_PreservesKeyValue()
        {
            // Arrange
            var handler = new SingularKeyHandler();
            handler.EnqueueKey(0x41); // 'A' with strobe set (0xC1)

            // Act
            handler.ResetKeyboard();

            // Assert
            Assert.False(handler.StrobePending()); // Strobe cleared
            Assert.Equal(0x41, handler.PeekCurrentKeyValue()); // Key value preserved
            Assert.Equal(0x41, handler.PeekCurrentKeyAndStrobe()); // Full value without strobe
        }

        [Fact]
        public void Reset_WhenNoKeyPresent_DoesNotThrow()
        {
            // Arrange
            var handler = new SingularKeyHandler();

            // Act & Assert - Should not throw
            handler.ResetKeyboard();
            Assert.False(handler.StrobePending());
            Assert.Equal(0, handler.PeekCurrentKeyValue());
        }

        [Fact]
        public void Reset_AfterClearStrobe_LeavesKeyUnchanged()
        {
            // Arrange
            var handler = new SingularKeyHandler();
            handler.EnqueueKey(0x42); // 'B'
            handler.ClearStrobe(); // Already cleared

            // Act
            handler.ResetKeyboard();

            // Assert - Key still there, strobe still clear
            Assert.False(handler.StrobePending());
            Assert.Equal(0x42, handler.PeekCurrentKeyValue());
        }

        [Fact]
        public void Reset_MultipleTimes_IsIdempotent()
        {
            // Arrange
            var handler = new SingularKeyHandler();
            handler.EnqueueKey(0x43); // 'C'

            // Act - Reset multiple times
            handler.ResetKeyboard();
            handler.ResetKeyboard();
            handler.ResetKeyboard();

            // Assert - Same result as single reset
            Assert.False(handler.StrobePending());
            Assert.Equal(0x43, handler.PeekCurrentKeyValue());
        }

        [Fact]
        public void Reset_AllowsNewKeyAfterReset()
        {
            // Arrange
            var handler = new SingularKeyHandler();
            handler.EnqueueKey(0x41); // 'A'
            handler.ResetKeyboard();

            // Act - Enqueue new key after reset
            handler.EnqueueKey(0x42); // 'B'

            // Assert - New key loaded with strobe
            Assert.True(handler.StrobePending());
            Assert.Equal(0x42, handler.PeekCurrentKeyValue());
            Assert.Equal(0xC2, handler.PeekCurrentKeyAndStrobe());
        }

        #endregion
    }
