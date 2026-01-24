using Xunit;
using Pandowdy.EmuCore;
using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Services;

namespace Pandowdy.EmuCore.Tests;

/// <summary>
/// Tests for SystemIoHandler game controller event synchronization with SystemStatus.
/// </summary>
/// <remarks>
/// These tests verify that game controller changes propagate to SystemStatus via event handlers,
/// ensuring UI and debuggers receive consistent state updates.
/// </remarks>
public class SystemIoHandlerGameControllerSyncTests
{
    /// <summary>
    /// Helper method to create test fixtures with proper dependency injection.
    /// </summary>
    /// <returns>Tuple of (SystemIoHandler, SimpleGameController, SystemStatusProvider, VBlankStatusHandler) for testing.</returns>
    private static (SystemIoHandler ioHandler, SimpleGameController controller, SystemStatusProvider status, CpuClockingCounters vblank) CreateTestFixture()
    {
        var controller = new SimpleGameController();
        var status = new SystemStatusProvider(controller);  // Direct integration!
        var switches = new SoftSwitches(status);
        var keyboard = new SingularKeyHandler();
        var vblank = new CpuClockingCounters();
        
        var ioHandler = new SystemIoHandler(switches, keyboard, controller, vblank);
        
        return (ioHandler, controller, status, vblank);
    }

    #region Button Change Synchronization Tests

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ButtonChanged_SynchronizesToSystemStatus(int buttonNum)
    {
        // Arrange
        var (_, controller, status, _) = CreateTestFixture();

        // Act - Simulate button press
        controller.SetButton(buttonNum, true);

        // Assert - SystemStatus should reflect button state
        bool expected = true;
        bool actual = buttonNum switch
        {
            0 => status.StatePb0,
            1 => status.StatePb1,
            2 => status.StatePb2,
            _ => false
        };
        
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ButtonChanged_AllButtons_SynchronizeIndependently()
    {
        // Arrange
        var (_, controller, status, _) = CreateTestFixture();

        // Act - Set different states for each button
        controller.SetButton(0, true);
        controller.SetButton(1, false);
        controller.SetButton(2, true);

        // Assert - Each button state synchronized correctly
        Assert.True(status.StatePb0);
        Assert.False(status.StatePb1);
        Assert.True(status.StatePb2);
    }

    [Fact]
    public void ButtonChanged_Toggle_SystemStatusUpdates()
    {
        // Arrange
        var (_, controller, status, _) = CreateTestFixture();

        // Act - Toggle button 0
        controller.SetButton(0, true);
        Assert.True(status.StatePb0, "Button should be pressed");

        controller.SetButton(0, false);
        Assert.False(status.StatePb0, "Button should be released");

        controller.SetButton(0, true);
        Assert.True(status.StatePb0, "Button should be pressed again");
    }

    [Fact]
    public void ButtonChanged_MultipleChanges_AllSynchronized()
    {
        // Arrange
        var (_, controller, status, _) = CreateTestFixture();

        // Act - Rapid button changes
        controller.SetButton(0, true);
        controller.SetButton(1, true);
        controller.SetButton(0, false);
        controller.SetButton(2, true);
        controller.SetButton(1, false);

        // Assert - Final state synchronized
        Assert.False(status.StatePb0); // Released
        Assert.False(status.StatePb1); // Released
        Assert.True(status.StatePb2);  // Pressed
    }

    #endregion

    #region Paddle Change Synchronization Tests

    [Theory]
    [InlineData(0, 50)]
    [InlineData(1, 127)]
    [InlineData(2, 200)]
    [InlineData(3, 255)]
    public void PaddleChanged_SynchronizesToSystemStatus(int paddleNum, byte value)
    {
        // Arrange
        var (_, controller, status, _) = CreateTestFixture();

        // Act - Set paddle value
        controller.SetPaddle(paddleNum, value);

        // Assert - SystemStatus should reflect paddle value
        byte actual = paddleNum switch
        {
            0 => status.Pdl0,
            1 => status.Pdl1,
            2 => status.Pdl2,
            3 => status.Pdl3,
            _ => 0
        };
        
        Assert.Equal(value, actual);
    }

    [Fact]
    public void PaddleChanged_AllPaddles_SynchronizeIndependently()
    {
        // Arrange
        var (_, controller, status, _) = CreateTestFixture();

        // Act - Set different values for each paddle
        controller.SetPaddle(0, 10);
        controller.SetPaddle(1, 20);
        controller.SetPaddle(2, 30);
        controller.SetPaddle(3, 40);

        // Assert - Each paddle value synchronized correctly
        Assert.Equal(10, status.Pdl0);
        Assert.Equal(20, status.Pdl1);
        Assert.Equal(30, status.Pdl2);
        Assert.Equal(40, status.Pdl3);
    }

    [Fact]
    public void PaddleChanged_MultipleUpdates_SystemStatusTracksLatest()
    {
        // Arrange
        var (_, controller, status, _) = CreateTestFixture();

        // Act - Update paddle 0 multiple times
        controller.SetPaddle(0, 50);
        Assert.Equal(50, status.Pdl0);

        controller.SetPaddle(0, 100);
        Assert.Equal(100, status.Pdl0);

        controller.SetPaddle(0, 255);
        Assert.Equal(255, status.Pdl0);
    }

    [Fact]
    public void PaddleChanged_FullRange_AllValuesSynchronized()
    {
        // Arrange
        var (_, controller, status, _) = CreateTestFixture();

        // Act & Assert - Test extreme values
        controller.SetPaddle(0, 0);
        Assert.Equal(0, status.Pdl0);

        controller.SetPaddle(0, 127);
        Assert.Equal(127, status.Pdl0);

        controller.SetPaddle(0, 255);
        Assert.Equal(255, status.Pdl0);
    }

    #endregion

    #region Mixed Button and Paddle Synchronization Tests

    [Fact]
    public void MixedChanges_ButtonsAndPaddles_AllSynchronized()
    {
        // Arrange
        var (_, controller, status, _) = CreateTestFixture();

        // Act - Mix button and paddle changes
        controller.SetButton(0, true);
        controller.SetPaddle(0, 100);
        controller.SetButton(1, true);
        controller.SetPaddle(1, 200);

        // Assert - All changes synchronized
        Assert.True(status.StatePb0);
        Assert.Equal(100, status.Pdl0);
        Assert.True(status.StatePb1);
        Assert.Equal(200, status.Pdl1);
    }

    [Fact]
    public void Scenario_JoystickInput_CompleteStateSynchronized()
    {
        // Arrange
        var (_, controller, status, _) = CreateTestFixture();

        // Act - Simulate joystick movement with button press
        controller.SetPaddle(0, 255); // X-axis right
        controller.SetPaddle(1, 0);   // Y-axis up
        controller.SetButton(0, true); // Fire button

        // Assert - Complete joystick state synchronized
        Assert.Equal(255, status.Pdl0); // X position
        Assert.Equal(0, status.Pdl1);   // Y position
        Assert.True(status.StatePb0);         // Button state
    }

    [Fact]
    public void Scenario_GamePaddle_StateChangesTracked()
    {
        // Arrange
        var (_, controller, status, _) = CreateTestFixture();

        // Act - Simulate game paddle sequence
        controller.SetPaddle(0, 50);   // Turn paddle left
        Assert.Equal(50, status.Pdl0);
        
        controller.SetButton(0, true);  // Press fire
        Assert.True(status.StatePb0);
        
        controller.SetPaddle(0, 200);  // Turn paddle right
        Assert.Equal(200, status.Pdl0);
        
        controller.SetButton(0, false); // Release fire
        Assert.False(status.StatePb0);
    }

    #endregion

    #region SystemStatus Event Propagation Tests

    [Fact]
    public void ButtonChanged_TriggersSystemStatusChangedEvent()
    {
        // Arrange
        var (ioHandler, controller, status, vblank) = CreateTestFixture();

        int eventCount = 0;
        status.Changed += (sender, snapshot) => eventCount++;

        // Act - Change button state
        controller.SetButton(0, true);

        // Assert - SystemStatus.Changed event should fire
        Assert.True(eventCount > 0, "SystemStatus.Changed event should fire when button changes");
    }

    [Fact]
    public void PaddleChanged_TriggersSystemStatusChangedEvent()
    {
        // Arrange
        var (ioHandler, controller, status, vblank) = CreateTestFixture();

        int eventCount = 0;
        status.Changed += (sender, snapshot) => eventCount++;

        // Act - Change paddle value
        controller.SetPaddle(0, 100);

        // Assert - SystemStatus.Changed event should fire
        Assert.True(eventCount > 0, "SystemStatus.Changed event should fire when paddle changes");
    }

    [Fact]
    public void MixedChanges_MultipleSystemStatusEvents()
    {
        // Arrange
        var (ioHandler, controller, status, vblank) = CreateTestFixture();

        int eventCount = 0;
        status.Changed += (sender, snapshot) => eventCount++;

        // Act - Multiple changes
        controller.SetButton(0, true);   // Event 1
        controller.SetPaddle(0, 100);    // Event 2
        controller.SetButton(1, true);   // Event 3

        // Assert - Each change triggers event
        Assert.True(eventCount >= 3, $"Expected at least 3 events, got {eventCount}");
    }

    #endregion

    #region Initial State Tests

    [Fact]
    public void Constructor_InitialButtonStatesMatch()
    {
        // Arrange & Act
        var (_, _, status, _) = CreateTestFixture();

        // Assert - Initial states should match (all buttons released)
        Assert.False(status.StatePb0);
        Assert.False(status.StatePb1);
        Assert.False(status.StatePb2);
    }

    [Fact]
    public void Constructor_InitialPaddleValuesMatch()
    {
        // Arrange & Act
        var (_, _, status, _) = CreateTestFixture();

        // Assert - Initial paddle values should match (all zero)
        Assert.Equal(0, status.Pdl0);
        Assert.Equal(0, status.Pdl1);
        Assert.Equal(0, status.Pdl2);
        Assert.Equal(0, status.Pdl3);
    }

    #endregion
}
