// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Services;

namespace Pandowdy.EmuCore.Tests;

/// <summary>
/// Tests for SimpleGameController game controller emulation.
/// </summary>
/// <remarks>
/// These tests verify:
/// - Button state management (3 buttons)
/// - Paddle analog value management (4 paddles)
/// - Change event firing with proper change detection
/// - Event args correctness
/// - Out of range handling
/// </remarks>
public class SimpleGameControllerTests
{
    #region Button State Tests

    [Fact]
    public void Constructor_InitializesAllButtonsAsNotPressed()
    {
        // Arrange & Act
        var controller = new SimpleGameController();

        // Assert
        Assert.False(controller.GetButton(0));
        Assert.False(controller.GetButton(1));
        Assert.False(controller.GetButton(2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void SetButton_UpdatesButtonState(int buttonNum)
    {
        // Arrange
        var controller = new SimpleGameController();

        // Act
        controller.SetButton(buttonNum, true);

        // Assert
        Assert.True(controller.GetButton(buttonNum));
    }

    [Fact]
    public void SetButton_MultipleButtons_IndependentStates()
    {
        // Arrange
        var controller = new SimpleGameController();

        // Act
        controller.SetButton(0, true);
        controller.SetButton(1, false);
        controller.SetButton(2, true);

        // Assert
        Assert.True(controller.GetButton(0));
        Assert.False(controller.GetButton(1));
        Assert.True(controller.GetButton(2));
    }

    [Fact]
    public void GetButton_OutOfRange_ReturnsFalse()
    {
        // Arrange
        var controller = new SimpleGameController();

        // Act & Assert
        Assert.False(controller.GetButton(-1));
        Assert.False(controller.GetButton(3));
        Assert.False(controller.GetButton(100));
    }

    [Fact]
    public void SetButton_OutOfRange_NoOp()
    {
        // Arrange
        var controller = new SimpleGameController();

        // Act - Should not throw
        controller.SetButton(-1, true);
        controller.SetButton(3, true);
        controller.SetButton(100, true);

        // Assert - All buttons still in default state
        Assert.False(controller.GetButton(0));
        Assert.False(controller.GetButton(1));
        Assert.False(controller.GetButton(2));
    }

    #endregion

    #region Paddle Value Tests

    [Fact]
    public void Constructor_InitializesAllPaddlesToZero()
    {
        // Arrange & Act
        var controller = new SimpleGameController();

        // Assert
        Assert.Equal(0, controller.GetPaddle(0));
        Assert.Equal(0, controller.GetPaddle(1));
        Assert.Equal(0, controller.GetPaddle(2));
        Assert.Equal(0, controller.GetPaddle(3));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 127)]
    [InlineData(0, 255)]
    [InlineData(1, 64)]
    [InlineData(2, 128)]
    [InlineData(3, 192)]
    public void SetPaddle_UpdatesPaddleValue(int paddleNum, byte value)
    {
        // Arrange
        var controller = new SimpleGameController();

        // Act
        controller.SetPaddle(paddleNum, value);

        // Assert
        Assert.Equal(value, controller.GetPaddle(paddleNum));
    }

    [Fact]
    public void SetPaddle_MultiplePaddles_IndependentValues()
    {
        // Arrange
        var controller = new SimpleGameController();

        // Act
        controller.SetPaddle(0, 10);
        controller.SetPaddle(1, 20);
        controller.SetPaddle(2, 30);
        controller.SetPaddle(3, 40);

        // Assert
        Assert.Equal(10, controller.GetPaddle(0));
        Assert.Equal(20, controller.GetPaddle(1));
        Assert.Equal(30, controller.GetPaddle(2));
        Assert.Equal(40, controller.GetPaddle(3));
    }

    [Fact]
    public void GetPaddle_OutOfRange_ReturnsZero()
    {
        // Arrange
        var controller = new SimpleGameController();

        // Act & Assert
        Assert.Equal(0, controller.GetPaddle(-1));
        Assert.Equal(0, controller.GetPaddle(4));
        Assert.Equal(0, controller.GetPaddle(100));
    }

    [Fact]
    public void SetPaddle_OutOfRange_NoOp()
    {
        // Arrange
        var controller = new SimpleGameController();
        controller.SetPaddle(0, 100); // Set valid paddle first

        // Act - Should not throw
        controller.SetPaddle(-1, 50);
        controller.SetPaddle(4, 50);
        controller.SetPaddle(100, 50);

        // Assert - Paddle 0 unchanged, others still zero
        Assert.Equal(100, controller.GetPaddle(0));
        Assert.Equal(0, controller.GetPaddle(1));
    }

    #endregion

    #region Button Change Event Tests

    [Fact]
    public void ButtonChanged_FiresWhenButtonPressed()
    {
        // Arrange
        var controller = new SimpleGameController();
        GameControllerButtonChangedEventArgs? eventArgs = null;
        controller.ButtonChanged += (sender, e) => eventArgs = e;

        // Act
        controller.SetButton(1, true);

        // Assert
        Assert.NotNull(eventArgs);
        Assert.Equal(1, eventArgs.ButtonNumber);
        Assert.True(eventArgs.IsPressed);
    }

    [Fact]
    public void ButtonChanged_FiresWhenButtonReleased()
    {
        // Arrange
        var controller = new SimpleGameController();
        controller.SetButton(0, true); // Press first
        
        GameControllerButtonChangedEventArgs? eventArgs = null;
        controller.ButtonChanged += (sender, e) => eventArgs = e;

        // Act
        controller.SetButton(0, false);

        // Assert
        Assert.NotNull(eventArgs);
        Assert.Equal(0, eventArgs.ButtonNumber);
        Assert.False(eventArgs.IsPressed);
    }

    [Fact]
    public void ButtonChanged_DoesNotFireWhenValueUnchanged()
    {
        // Arrange
        var controller = new SimpleGameController();
        controller.SetButton(0, true); // Set initial state
        
        int eventCount = 0;
        controller.ButtonChanged += (sender, e) => eventCount++;

        // Act - Set same value multiple times
        controller.SetButton(0, true);
        controller.SetButton(0, true);
        controller.SetButton(0, true);

        // Assert - Event should NOT fire (change detection)
        Assert.Equal(0, eventCount);
    }

    [Fact]
    public void ButtonChanged_FiresForEachActualChange()
    {
        // Arrange
        var controller = new SimpleGameController();
        int eventCount = 0;
        controller.ButtonChanged += (sender, e) => eventCount++;

        // Act - Toggle button multiple times
        controller.SetButton(0, true);  // Change 1: false -> true
        controller.SetButton(0, true);  // No change
        controller.SetButton(0, false); // Change 2: true -> false
        controller.SetButton(0, false); // No change
        controller.SetButton(0, true);  // Change 3: false -> true

        // Assert - Should fire 3 times (only actual changes)
        Assert.Equal(3, eventCount);
    }

    [Fact]
    public void ButtonChanged_MultipleButtons_IndependentEvents()
    {
        // Arrange
        var controller = new SimpleGameController();
        var button0Events = new List<GameControllerButtonChangedEventArgs>();
        var button1Events = new List<GameControllerButtonChangedEventArgs>();
        
        controller.ButtonChanged += (sender, e) =>
        {
            if (e.ButtonNumber == 0)
            {
                button0Events.Add(e);
            }
            if (e.ButtonNumber == 1)
            {
                button1Events.Add(e);
            }
        };

        // Act
        controller.SetButton(0, true);
        controller.SetButton(1, true);
        controller.SetButton(0, false);

        // Assert
        Assert.Equal(2, button0Events.Count); // Press and release
        Assert.Single(button1Events);         // Only press
    }

    [Fact]
    public void ButtonChanged_OutOfRange_NoEventFired()
    {
        // Arrange
        var controller = new SimpleGameController();
        int eventCount = 0;
        controller.ButtonChanged += (sender, e) => eventCount++;

        // Act
        controller.SetButton(-1, true);
        controller.SetButton(3, true);

        // Assert
        Assert.Equal(0, eventCount);
    }

    #endregion

    #region Paddle Change Event Tests

    [Fact]
    public void PaddleChanged_FiresWhenPaddleValueChanges()
    {
        // Arrange
        var controller = new SimpleGameController();
        GameControllerPaddleChangedEventArgs? eventArgs = null;
        controller.PaddleChanged += (sender, e) => eventArgs = e;

        // Act
        controller.SetPaddle(2, 150);

        // Assert
        Assert.NotNull(eventArgs);
        Assert.Equal(2, eventArgs.PaddleNumber);
        Assert.Equal(150, eventArgs.Value);
    }

    [Fact]
    public void PaddleChanged_DoesNotFireWhenValueUnchanged()
    {
        // Arrange
        var controller = new SimpleGameController();
        controller.SetPaddle(1, 100); // Set initial value
        
        int eventCount = 0;
        controller.PaddleChanged += (sender, e) => eventCount++;

        // Act - Set same value multiple times
        controller.SetPaddle(1, 100);
        controller.SetPaddle(1, 100);
        controller.SetPaddle(1, 100);

        // Assert - Event should NOT fire (change detection)
        Assert.Equal(0, eventCount);
    }

    [Fact]
    public void PaddleChanged_FiresForEachActualChange()
    {
        // Arrange
        var controller = new SimpleGameController();
        int eventCount = 0;
        controller.PaddleChanged += (sender, e) => eventCount++;

        // Act
        controller.SetPaddle(0, 50);  // Change 1: 0 -> 50
        controller.SetPaddle(0, 50);  // No change
        controller.SetPaddle(0, 100); // Change 2: 50 -> 100
        controller.SetPaddle(0, 100); // No change
        controller.SetPaddle(0, 0);   // Change 3: 100 -> 0

        // Assert - Should fire 3 times (only actual changes)
        Assert.Equal(3, eventCount);
    }

    [Fact]
    public void PaddleChanged_MultiplePaddles_IndependentEvents()
    {
        // Arrange
        var controller = new SimpleGameController();
        var paddle0Events = new List<GameControllerPaddleChangedEventArgs>();
        var paddle1Events = new List<GameControllerPaddleChangedEventArgs>();
        
        controller.PaddleChanged += (sender, e) =>
        {
            if (e.PaddleNumber == 0)
            {
                paddle0Events.Add(e);
            }
            if (e.PaddleNumber == 1)
            {
                paddle1Events.Add(e);
            }
        };

        // Act
        controller.SetPaddle(0, 10);
        controller.SetPaddle(0, 20);
        controller.SetPaddle(1, 30);

        // Assert
        Assert.Equal(2, paddle0Events.Count);
        Assert.Single(paddle1Events);
    }

    [Fact]
    public void PaddleChanged_OutOfRange_NoEventFired()
    {
        // Arrange
        var controller = new SimpleGameController();
        int eventCount = 0;
        controller.PaddleChanged += (sender, e) => eventCount++;

        // Act
        controller.SetPaddle(-1, 100);
        controller.SetPaddle(4, 100);

        // Assert
        Assert.Equal(0, eventCount);
    }

    [Fact]
    public void PaddleChanged_FullRange_FiresCorrectly()
    {
        // Arrange
        var controller = new SimpleGameController();
        var events = new List<GameControllerPaddleChangedEventArgs>();
        controller.PaddleChanged += (sender, e) => events.Add(e);

        // Act - Test extreme values (skip 0 since it's the default)
        controller.SetPaddle(0, 127);   // Change 1: 0 -> 127
        controller.SetPaddle(0, 255);   // Change 2: 127 -> 255
        controller.SetPaddle(0, 0);     // Change 3: 255 -> 0

        // Assert
        Assert.Equal(3, events.Count);
        Assert.Equal(127, events[0].Value);
        Assert.Equal(255, events[1].Value);
        Assert.Equal(0, events[2].Value);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void MixedChanges_BothEventsFire()
    {
        // Arrange
        var controller = new SimpleGameController();
        int buttonEvents = 0;
        int paddleEvents = 0;
        
        controller.ButtonChanged += (sender, e) => buttonEvents++;
        controller.PaddleChanged += (sender, e) => paddleEvents++;

        // Act
        controller.SetButton(0, true);
        controller.SetPaddle(0, 100);
        controller.SetButton(1, true);
        controller.SetPaddle(1, 200);

        // Assert
        Assert.Equal(2, buttonEvents);
        Assert.Equal(2, paddleEvents);
    }

    [Fact]
    public void Scenario_JoystickMovement_FiresCorrectEvents()
    {
        // Arrange
        var controller = new SimpleGameController();
        var paddleEvents = new List<GameControllerPaddleChangedEventArgs>();
        controller.PaddleChanged += (sender, e) => paddleEvents.Add(e);

        // Act - Simulate joystick movement (X=paddle0, Y=paddle1)
        controller.SetPaddle(0, 127); // Center X
        controller.SetPaddle(1, 127); // Center Y
        controller.SetPaddle(0, 255); // Right
        controller.SetPaddle(1, 0);   // Up

        // Assert
        Assert.Equal(4, paddleEvents.Count);
        Assert.Equal(0, paddleEvents[0].PaddleNumber); // X center
        Assert.Equal(1, paddleEvents[1].PaddleNumber); // Y center
        Assert.Equal(0, paddleEvents[2].PaddleNumber); // X right
        Assert.Equal(1, paddleEvents[3].PaddleNumber); // Y up
    }

    [Fact]
    public void Scenario_GamePaddle_ButtonAndAxisChange()
    {
        // Arrange
        var controller = new SimpleGameController();
        var buttonEvents = new List<GameControllerButtonChangedEventArgs>();
        var paddleEvents = new List<GameControllerPaddleChangedEventArgs>();
        
        controller.ButtonChanged += (sender, e) => buttonEvents.Add(e);
        controller.PaddleChanged += (sender, e) => paddleEvents.Add(e);

        // Act - Simulate game paddle (1 paddle + 1 button)
        controller.SetPaddle(0, 50);   // Turn paddle left
        controller.SetButton(0, true);  // Press fire button
        controller.SetPaddle(0, 200);  // Turn paddle right
        controller.SetButton(0, false); // Release fire button

        // Assert
        Assert.Equal(2, buttonEvents.Count);  // Press and release
        Assert.Equal(2, paddleEvents.Count);  // Two position changes
    }

    #endregion

    #region Interface Implementation Tests

    [Fact]
    public void ImplementsIGameControllerStatus()
    {
        // Arrange - Testing interface implementation
        var controller = new SimpleGameController();

        // Act & Assert - Interface methods work
        controller.SetButton(0, true);
        Assert.True(controller.GetButton(0));

        controller.SetPaddle(0, 100);
        Assert.Equal(100, controller.GetPaddle(0));
    }

    [Fact]
    public void EventsAccessibleThroughInterface()
    {
        // Arrange - Testing that events work via interface
        var controller = new SimpleGameController();
        int eventCount = 0;

        controller.ButtonChanged += (sender, e) => eventCount++;
        controller.PaddleChanged += (sender, e) => eventCount++;

        // Act
        controller.SetButton(0, true);
        controller.SetPaddle(0, 100);

        // Assert
        Assert.Equal(2, eventCount);
    }

    #endregion
}
