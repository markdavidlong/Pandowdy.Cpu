// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.Services;

namespace Pandowdy.EmuCore.Tests;

/// <summary>
/// Comprehensive tests for SystemIoHandler - the central I/O routing coordinator.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Test Scope:</strong> SystemIoHandler is responsible for routing I/O operations
/// between VA2MBus and the subsystems (keyboard, game controller, soft switches). These tests
/// verify that reads and writes to I/O addresses are correctly routed to the appropriate handlers.
/// </para>
/// <para>
/// <strong>What SystemIoHandler Does:</strong>
/// <list type="bullet">
/// <item>Route keyboard I/O ($C000 KBD, $C010 KEYSTRB) to IKeyboardReader</item>
/// <item>Route game controller I/O ($C061-$C063) to IGameControllerStatus</item>
/// <item>Route soft switch reads/writes to SoftSwitches</item>
/// <item>Synchronize VBlank counter for RD_VERTBLANK reads</item>
/// <item>Return correct values from I/O addresses</item>
/// </list>
/// </para>
/// <para>
/// <strong>Architecture Context:</strong>
/// SystemIoHandler sits between VA2MBus and the I/O subsystems, providing a clean
/// separation of concerns. It owns the I/O address space ($C000-$C0FF) and delegates
/// to specialized handlers for each I/O type.
/// </para>
/// </remarks>
public class SystemIoHandlerTests
{
    #region Test Helpers

    /// <summary>
    /// Test fixture for SystemIoHandler with all required dependencies.
    /// </summary>
    private class SystemIoHandlerFixture
    {
        public SoftSwitches Switches { get; }
        public SingularKeyHandler Keyboard { get; }
        public SimpleGameController GameController { get; }
        public SystemStatusProvider StatusProvider { get; }
        public CpuClockingCounters VBlank { get; }
        public SystemIoHandler IoHandler { get; }

        public SystemIoHandlerFixture()
        {
            // Create dependencies in correct order
            GameController = new SimpleGameController();
            StatusProvider = new SystemStatusProvider(GameController);
            Keyboard = new SingularKeyHandler();
            Switches = new SoftSwitches(StatusProvider);
            VBlank = new CpuClockingCounters();
            
            // Create SystemIoHandler
            IoHandler = new SystemIoHandler(Switches, Keyboard, GameController, VBlank);
        }
    }

    #endregion

    #region Constructor Tests (4 tests)

    [Fact]
    public void Constructor_WithValidParameters_InitializesSuccessfully()
    {
        // Arrange
        var gameController = new SimpleGameController();
        var statusProvider = new SystemStatusProvider(gameController);
        var keyboard = new SingularKeyHandler();
        var switches = new SoftSwitches(statusProvider);
        var vblank = new CpuClockingCounters();

        // Act
        var ioHandler = new SystemIoHandler(switches, keyboard, gameController, vblank);

        // Assert
        Assert.NotNull(ioHandler);
    }

    [Fact]
    public void Constructor_NullSoftSwitches_ThrowsArgumentNullException()
    {
        // Arrange
        var gameController = new SimpleGameController();
        var keyboard = new SingularKeyHandler();
        var vblank = new CpuClockingCounters();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new SystemIoHandler(null!, keyboard, gameController, vblank));
    }

    [Fact]
    public void Constructor_NullKeyboard_ThrowsArgumentNullException()
    {
        // Arrange
        var gameController = new SimpleGameController();
        var statusProvider = new SystemStatusProvider(gameController);
        var switches = new SoftSwitches(statusProvider);
        var vblank = new CpuClockingCounters();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new SystemIoHandler(switches, null!, gameController, vblank));
    }

    [Fact]
    public void Constructor_NullGameController_ThrowsArgumentNullException()
    {
        // Arrange
        var gameController = new SimpleGameController();
        var statusProvider = new SystemStatusProvider(gameController);
        var switches = new SoftSwitches(statusProvider);
        var keyboard = new SingularKeyHandler();
        var vblank = new CpuClockingCounters();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new SystemIoHandler(switches, keyboard, null!, vblank));
    }

    #endregion

    #region Keyboard I/O Routing Tests (8 tests)

    [Fact]
    public void Read_KBD_RoutesToKeyboard()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();
        fixture.Keyboard.EnqueueKey(0xC1); // 'A' with high bit

        // Act
        var value = fixture.IoHandler.Read((ushort)(SystemIoHandler.KBD_ & 0xFF));

        // Assert
        Assert.Equal(0xC1, value);
    }

    [Fact]
    public void Read_KBD_WithoutKey_ReturnsZero()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();

        // Act
        var value = fixture.IoHandler.Read((ushort)(SystemIoHandler.KBD_ & 0xFF));

        // Assert
        Assert.Equal(0x00, value);
    }

    [Fact]
    public void Read_KEYSTRB_ClearsStrobeAndReturnsValue()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();
        fixture.Keyboard.EnqueueKey(0xC1); // 'A' with high bit

        // Act
        var valueBefore = fixture.IoHandler.Read((ushort)(SystemIoHandler.KBD_ & 0xFF));
        var valueStrobe = fixture.IoHandler.Read((ushort)(SystemIoHandler.KEYSTRB_ & 0xFF));
        var valueAfter = fixture.IoHandler.Read((ushort)(SystemIoHandler.KBD_ & 0xFF));

        // Assert
        Assert.Equal(0xC1, valueBefore);  // Strobe set
        Assert.Equal(0x41, valueStrobe);  // Strobe cleared by read
        Assert.Equal(0x41, valueAfter);   // Strobe stays clear
    }

    [Fact]
    public void Write_KEYSTRB_ClearsStrobe()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();
        fixture.Keyboard.EnqueueKey(0xC1);

        // Act
        fixture.IoHandler.Write((ushort)(SystemIoHandler.KEYSTRB_ & 0xFF), 0); // Write clears strobe
        var value = fixture.IoHandler.Read((ushort)(SystemIoHandler.KBD_ & 0xFF));

        // Assert
        Assert.Equal(0x41, value); // Strobe cleared
    }

    [Fact]
    public void Read_KBD_MultipleKeys_ReturnsLatest()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();

        // Act - Set multiple keys
        fixture.Keyboard.EnqueueKey(0xC1); // 'A'
        var value1 = fixture.IoHandler.Read((ushort)(SystemIoHandler.KBD_ & 0xFF));
        
        fixture.Keyboard.EnqueueKey(0xC2); // 'B'
        var value2 = fixture.IoHandler.Read((ushort)(SystemIoHandler.KBD_ & 0xFF));

        // Assert
        Assert.Equal(0xC1, value1);
        Assert.Equal(0xC2, value2);
    }

    [Fact]
    public void KeyboardIO_Sequence_WorksCorrectly()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();

        // Act - Simulate keyboard input sequence
        fixture.Keyboard.EnqueueKey(0xC8); // 'H'
        Assert.Equal(0xC8, fixture.IoHandler.Read((ushort)(SystemIoHandler.KBD_ & 0xFF)));
        fixture.IoHandler.Read((ushort)(SystemIoHandler.KEYSTRB_ & 0xFF)); // Clear strobe

        fixture.Keyboard.EnqueueKey(0xC9); // 'I'
        Assert.Equal(0xC9, fixture.IoHandler.Read((ushort)(SystemIoHandler.KBD_ & 0xFF)));
        fixture.IoHandler.Read((ushort)(SystemIoHandler.KEYSTRB_ & 0xFF));

        // Assert - Last key present with strobe cleared
        Assert.Equal(0x49, fixture.IoHandler.Read((ushort)(SystemIoHandler.KBD_ & 0xFF)));
    }

    [Fact]
    public void Read_KBD_BeforeAndAfterKeySet_ShowsDifference()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();
        var beforeKey = fixture.IoHandler.Read((ushort)(SystemIoHandler.KBD_ & 0xFF));

        // Act
        fixture.Keyboard.EnqueueKey(0xC5);
        var afterKey = fixture.IoHandler.Read((ushort)(SystemIoHandler.KBD_ & 0xFF));

        // Assert
        Assert.Equal(0x00, beforeKey); // No key initially
        Assert.Equal(0xC5, afterKey);  // Key present after set
    }

    [Fact]
    public void Keyboard_StrobeBitBehavior_MatchesAppleIIe()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();
        fixture.Keyboard.EnqueueKey(0x41); // 'A' without high bit (becomes 0xC1)

        // Act
        var withStrobe = fixture.IoHandler.Read((ushort)(SystemIoHandler.KBD_ & 0xFF));
        fixture.IoHandler.Read((ushort)(SystemIoHandler.KEYSTRB_ & 0xFF)); // Clear
        var withoutStrobe = fixture.IoHandler.Read((ushort)(SystemIoHandler.KBD_ & 0xFF));

        // Assert
        Assert.Equal(0xC1, withStrobe);    // High bit set (strobe active)
        Assert.Equal(0x41, withoutStrobe); // High bit clear (strobe cleared)
    }

    #endregion

    #region Game Controller I/O Routing Tests (10 tests)

    [Fact]
    public void Read_BUTTON0_ReturnsButtonState()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();
        fixture.GameController.SetButton(0, true);

        // Act
        var pressed = fixture.IoHandler.Read((ushort)(SystemIoHandler.BUTTON0_ & 0xFF));
        fixture.GameController.SetButton(0, false);
        var released = fixture.IoHandler.Read((ushort)(SystemIoHandler.BUTTON0_ & 0xFF));

        // Assert
        Assert.Equal(0x80, pressed);   // High bit set when pressed
        Assert.Equal(0x00, released);  // High bit clear when released
    }

    [Fact]
    public void Read_BUTTON1_ReturnsButtonState()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();
        fixture.GameController.SetButton(1, true);

        // Act
        var pressed = fixture.IoHandler.Read((ushort)(SystemIoHandler.BUTTON1_ & 0xFF));

        // Assert
        Assert.Equal(0x80, pressed);
    }

    [Fact]
    public void Read_BUTTON2_ReturnsButtonState()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();
        fixture.GameController.SetButton(2, true);

        // Act
        var pressed = fixture.IoHandler.Read((ushort)(SystemIoHandler.BUTTON2_ & 0xFF));

        // Assert
        Assert.Equal(0x80, pressed);
    }

    [Fact]
    public void Read_Buttons_IndependentStates()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();
        fixture.GameController.SetButton(0, true);
        fixture.GameController.SetButton(1, false);
        fixture.GameController.SetButton(2, true);

        // Act
        var button0 = fixture.IoHandler.Read((ushort)(SystemIoHandler.BUTTON0_ & 0xFF));
        var button1 = fixture.IoHandler.Read((ushort)(SystemIoHandler.BUTTON1_ & 0xFF));
        var button2 = fixture.IoHandler.Read((ushort)(SystemIoHandler.BUTTON2_ & 0xFF));

        // Assert
        Assert.Equal(0x80, button0); // Pressed
        Assert.Equal(0x00, button1); // Released
        Assert.Equal(0x80, button2); // Pressed
    }

    [Fact]
    public void Read_Buttons_DefaultState_AllReleased()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();

        // Act
        var button0 = fixture.IoHandler.Read((ushort)(SystemIoHandler.BUTTON0_ & 0xFF));
        var button1 = fixture.IoHandler.Read((ushort)(SystemIoHandler.BUTTON1_ & 0xFF));
        var button2 = fixture.IoHandler.Read((ushort)(SystemIoHandler.BUTTON2_ & 0xFF));

        // Assert
        Assert.Equal(0x00, button0);
        Assert.Equal(0x00, button1);
        Assert.Equal(0x00, button2);
    }

    [Fact]
    public void GameController_PressReleaseSequence_TracksCorrectly()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();

        // Act - Press, check, release, check
        fixture.GameController.SetButton(0, true);
        var pressed = fixture.IoHandler.Read((ushort)(SystemIoHandler.BUTTON0_ & 0xFF));
        
        fixture.GameController.SetButton(0, false);
        var released = fixture.IoHandler.Read((ushort)(SystemIoHandler.BUTTON0_ & 0xFF));
        
        fixture.GameController.SetButton(0, true);
        var pressedAgain = fixture.IoHandler.Read((ushort)(SystemIoHandler.BUTTON0_ & 0xFF));

        // Assert
        Assert.Equal(0x80, pressed);
        Assert.Equal(0x00, released);
        Assert.Equal(0x80, pressedAgain);
    }

    [Fact]
    public void GameController_MultipleButtonPresses_Simultaneous()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();

        // Act - Press all buttons simultaneously
        fixture.GameController.SetButton(0, true);
        fixture.GameController.SetButton(1, true);
        fixture.GameController.SetButton(2, true);

        // Assert - All should read as pressed
        Assert.Equal(0x80, fixture.IoHandler.Read((ushort)(SystemIoHandler.BUTTON0_ & 0xFF)));
        Assert.Equal(0x80, fixture.IoHandler.Read((ushort)(SystemIoHandler.BUTTON1_ & 0xFF)));
        Assert.Equal(0x80, fixture.IoHandler.Read((ushort)(SystemIoHandler.BUTTON2_ & 0xFF)));
    }

    [Fact]
    public void GameController_RapidButtonToggle_TracksAccurately()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();

        // Act & Assert - Rapid toggle
        for (int i = 0; i < 5; i++)
        {
            fixture.GameController.SetButton(0, true);
            Assert.Equal(0x80, fixture.IoHandler.Read((ushort)(SystemIoHandler.BUTTON0_ & 0xFF)));
            
            fixture.GameController.SetButton(0, false);
            Assert.Equal(0x00, fixture.IoHandler.Read((ushort)(SystemIoHandler.BUTTON0_ & 0xFF)));
        }
    }

    [Fact]
    public void GameController_ReadButtonWithoutSet_ReturnsZero()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();

        // Act - Read buttons without setting them
        var button0 = fixture.IoHandler.Read((ushort)(SystemIoHandler.BUTTON0_ & 0xFF));
        var button1 = fixture.IoHandler.Read((ushort)(SystemIoHandler.BUTTON1_ & 0xFF));
        var button2 = fixture.IoHandler.Read((ushort)(SystemIoHandler.BUTTON2_ & 0xFF));

        // Assert - All should be zero (released)
        Assert.Equal(0x00, button0);
        Assert.Equal(0x00, button1);
        Assert.Equal(0x00, button2);
    }

    [Fact]
    public void GameController_ButtonStateChanges_ReflectImmediately()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();

        // Act - Change button 1 multiple times
        fixture.GameController.SetButton(1, true);
        var state1 = fixture.IoHandler.Read((ushort)(SystemIoHandler.BUTTON1_ & 0xFF));
        
        fixture.GameController.SetButton(1, false);
        var state2 = fixture.IoHandler.Read((ushort)(SystemIoHandler.BUTTON1_ & 0xFF));
        
        fixture.GameController.SetButton(1, true);
        var state3 = fixture.IoHandler.Read((ushort)(SystemIoHandler.BUTTON1_ & 0xFF));

        // Assert - Each state change reflects immediately
        Assert.Equal(0x80, state1);
        Assert.Equal(0x00, state2);
        Assert.Equal(0x80, state3);
    }

    #endregion

    #region Soft Switch Read Routing Tests (8 tests)

    [Fact]
    public void Read_RD_TEXT_RouteToSoftSwitches()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();
        fixture.Switches.Set(SoftSwitches.SoftSwitchId.Text, true);

        // Act
        var value = fixture.IoHandler.Read((ushort)(SystemIoHandler.RD_TEXT_ & 0xFF));

        // Assert
        Assert.Equal(0x80, value & 0x80); // High bit set
    }

    [Fact]
    public void Read_RD_MIXED_RoutesToSoftSwitches()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();
        fixture.Switches.Set(SoftSwitches.SoftSwitchId.Mixed, true);

        // Act
        var value = fixture.IoHandler.Read((ushort)(SystemIoHandler.RD_MIXED_ & 0xFF));

        // Assert
        Assert.Equal(0x80, value & 0x80);
    }

    [Fact]
    public void Read_RD_PAGE2_RoutesToSoftSwitches()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();
        fixture.Switches.Set(SoftSwitches.SoftSwitchId.Page2, true);

        // Act
        var value = fixture.IoHandler.Read((ushort)(SystemIoHandler.RD_PAGE2_ & 0xFF));

        // Assert
        Assert.Equal(0x80, value & 0x80);
    }

    [Fact]
    public void Read_RD_HIRES_RoutesToSoftSwitches()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();
        fixture.Switches.Set(SoftSwitches.SoftSwitchId.HiRes, true);

        // Act
        var value = fixture.IoHandler.Read((ushort)(SystemIoHandler.RD_HIRES_ & 0xFF));

        // Assert
        Assert.Equal(0x80, value & 0x80);
    }

    [Fact]
    public void Read_SoftSwitchStatus_ReflectsCurrentState()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();
        
        // Act - Toggle switch and read
        fixture.Switches.Set(SoftSwitches.SoftSwitchId.Text, true);
        var valueOn = fixture.IoHandler.Read((ushort)(SystemIoHandler.RD_TEXT_ & 0xFF));
        
        fixture.Switches.Set(SoftSwitches.SoftSwitchId.Text, false);
        var valueOff = fixture.IoHandler.Read((ushort)(SystemIoHandler.RD_TEXT_ & 0xFF));

        // Assert
        Assert.Equal(0x80, valueOn & 0x80);   // High bit set when on
        Assert.Equal(0x00, valueOff & 0x80);  // High bit clear when off
    }

    [Fact]
    public void Read_MultipleSoftSwitches_IndependentStates()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();
        fixture.Switches.Set(SoftSwitches.SoftSwitchId.Text, true);
        fixture.Switches.Set(SoftSwitches.SoftSwitchId.Mixed, false);
        fixture.Switches.Set(SoftSwitches.SoftSwitchId.HiRes, true);

        // Act
        var text = fixture.IoHandler.Read((ushort)(SystemIoHandler.RD_TEXT_ & 0xFF));
        var mixed = fixture.IoHandler.Read((ushort)(SystemIoHandler.RD_MIXED_ & 0xFF));
        var hires = fixture.IoHandler.Read((ushort)(SystemIoHandler.RD_HIRES_ & 0xFF));

        // Assert
        Assert.Equal(0x80, text & 0x80);   // On
        Assert.Equal(0x00, mixed & 0x80);  // Off
        Assert.Equal(0x80, hires & 0x80);  // On
    }

    [Fact]
    public void Read_SoftSwitchStatus_WithKeyboardValue_PreservesLowBits()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();
        fixture.Keyboard.EnqueueKey(0x55); // Pattern in low bits (becomes 0xD5 with strobe)
        fixture.Switches.Set(SoftSwitches.SoftSwitchId.Text, true);

        // Act
        var value = fixture.IoHandler.Read((ushort)(SystemIoHandler.RD_TEXT_ & 0xFF));

        // Assert - High bit from switch, low bits from keyboard
        Assert.Equal(0x80, value & 0x80);  // Switch state in high bit
        Assert.Equal(0x55, value & 0x7F);  // Keyboard value in low bits
    }

    [Fact]
    public void Read_AllSoftSwitchStatuses_ReturnValues()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();

        // Act - Read all status addresses
        var reads = new Dictionary<string, byte>
        {
            ["RD_TEXT"] = fixture.IoHandler.Read((ushort)(SystemIoHandler.RD_TEXT_ & 0xFF)),
            ["RD_MIXED"] = fixture.IoHandler.Read((ushort)(SystemIoHandler.RD_MIXED_ & 0xFF)),
            ["RD_PAGE2"] = fixture.IoHandler.Read((ushort)(SystemIoHandler.RD_PAGE2_ & 0xFF)),
            ["RD_HIRES"] = fixture.IoHandler.Read((ushort)(SystemIoHandler.RD_HIRES_ & 0xFF)),
            ["RD_80STORE"] = fixture.IoHandler.Read((ushort)(SystemIoHandler.RD_80STORE_ & 0xFF)),
            ["RD_RAMRD"] = fixture.IoHandler.Read((ushort)(SystemIoHandler.RD_RAMRD_ & 0xFF)),
            ["RD_RAMWRT"] = fixture.IoHandler.Read((ushort)(SystemIoHandler.RD_RAMWRT_ & 0xFF)),
            ["RD_ALTZP"] = fixture.IoHandler.Read((ushort)(SystemIoHandler.RD_ALTZP_ & 0xFF))
        };

        // Assert - All should return valid values
        Assert.All(reads, kvp => Assert.True(kvp.Value >= 0 && kvp.Value <= 0xFF));
    }

    #endregion

    #region Soft Switch Write Routing Tests (6 tests)

    [Fact]
    public void Write_SETTXT_RoutesToSoftSwitches()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();

        // Act
        fixture.IoHandler.Write((ushort)(SystemIoHandler.SETTXT_ & 0xFF), 0);

        // Assert
        Assert.True(fixture.StatusProvider.StateTextMode);
    }

    [Fact]
    public void Write_CLRTXT_RoutesToSoftSwitches()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();
        fixture.IoHandler.Write((ushort)(SystemIoHandler.SETTXT_ & 0xFF), 0);

        // Act
        fixture.IoHandler.Write((ushort)(SystemIoHandler.CLRTXT_ & 0xFF), 0);

        // Assert
        Assert.False(fixture.StatusProvider.StateTextMode);
    }

    [Fact]
    public void Write_VideoSwitches_UpdateCorrectly()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();

        // Act
        fixture.IoHandler.Write((ushort)(SystemIoHandler.SETTXT_ & 0xFF), 0);
        fixture.IoHandler.Write((ushort)(SystemIoHandler.SETMIXED_ & 0xFF), 0);
        fixture.IoHandler.Write((ushort)(SystemIoHandler.SETHIRES_ & 0xFF), 0);
        fixture.IoHandler.Write((ushort)(SystemIoHandler.SETPAGE2_ & 0xFF), 0);

        // Assert
        Assert.True(fixture.StatusProvider.StateTextMode);
        Assert.True(fixture.StatusProvider.StateMixed);
        Assert.True(fixture.StatusProvider.StateHiRes);
        Assert.True(fixture.StatusProvider.StatePage2);
    }

    [Fact]
    public void Write_MemorySwitches_UpdateCorrectly()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();

        // Act
        fixture.IoHandler.Write((ushort)(SystemIoHandler.SET80STORE_ & 0xFF), 0);
        fixture.IoHandler.Write((ushort)(SystemIoHandler.RDCARDRAM_ & 0xFF), 0);
        fixture.IoHandler.Write((ushort)(SystemIoHandler.WRCARDRAM_ & 0xFF), 0);
        fixture.IoHandler.Write((ushort)(SystemIoHandler.ALTZP_ & 0xFF), 0);

        // Assert
        Assert.True(fixture.StatusProvider.State80Store);
        Assert.True(fixture.StatusProvider.StateRamRd);
        Assert.True(fixture.StatusProvider.StateRamWrt);
        Assert.True(fixture.StatusProvider.StateAltZp);
    }

    [Fact]
    public void Write_SoftSwitches_ToggleSequence()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();

        // Act & Assert - Toggle TEXT mode
        fixture.IoHandler.Write((ushort)(SystemIoHandler.SETTXT_ & 0xFF), 0);
        Assert.True(fixture.StatusProvider.StateTextMode);
        
        fixture.IoHandler.Write((ushort)(SystemIoHandler.CLRTXT_ & 0xFF), 0);
        Assert.False(fixture.StatusProvider.StateTextMode);
        
        fixture.IoHandler.Write((ushort)(SystemIoHandler.SETTXT_ & 0xFF), 0);
        Assert.True(fixture.StatusProvider.StateTextMode);
    }

    [Fact]
    public void Write_ComplexSwitchSequence_TracksCorrectly()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();

        // Act - Simulate typical graphics mode setup
        fixture.IoHandler.Write((ushort)(SystemIoHandler.CLRTXT_ & 0xFF), 0);     // Graphics
        fixture.IoHandler.Write((ushort)(SystemIoHandler.SETHIRES_ & 0xFF), 0);   // Hi-res
        fixture.IoHandler.Write((ushort)(SystemIoHandler.SETMIXED_ & 0xFF), 0);   // Mixed
        fixture.IoHandler.Write((ushort)(SystemIoHandler.CLRPAGE2_ & 0xFF), 0);   // Page 1

        // Assert
        Assert.False(fixture.StatusProvider.StateTextMode);
        Assert.True(fixture.StatusProvider.StateHiRes);
        Assert.True(fixture.StatusProvider.StateMixed);
        Assert.False(fixture.StatusProvider.StatePage2);
    }

    #endregion

    #region VBlank Counter Synchronization Tests (4 tests)

    [Fact]
    public void VBlankCounter_UpdatesInternalState()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();

        // Act
        fixture.VBlank.VBlankCounter = 4550; // In VBlank
        var inVBlank = fixture.IoHandler.Read((ushort)(SystemIoHandler.RD_VERTBLANK_ & 0xFF));
        
        fixture.VBlank.VBlankCounter = 0;    // Out of VBlank
        var outOfVBlank = fixture.IoHandler.Read((ushort)(SystemIoHandler.RD_VERTBLANK_ & 0xFF));

        // Assert
        Assert.Equal(0x80, inVBlank & 0x80);    // High bit set (in VBlank)
        Assert.Equal(0x00, outOfVBlank & 0x80); // High bit clear (not in VBlank)
    }

    [Fact]
    public void Read_RD_VERTBLANK_ReflectsVBlankCounter()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();

        // Act - Test various counter values
        fixture.VBlank.VBlankCounter = 1000;
        var value1 = fixture.IoHandler.Read((ushort)(SystemIoHandler.RD_VERTBLANK_ & 0xFF));
        
        fixture.VBlank.VBlankCounter = 1;
        var value2 = fixture.IoHandler.Read((ushort)(SystemIoHandler.RD_VERTBLANK_ & 0xFF));
        
        fixture.VBlank.VBlankCounter = 0;
        var value3 = fixture.IoHandler.Read((ushort)(SystemIoHandler.RD_VERTBLANK_ & 0xFF));
        
        fixture.VBlank.VBlankCounter = -100;
        var value4 = fixture.IoHandler.Read((ushort)(SystemIoHandler.RD_VERTBLANK_ & 0xFF));

        // Assert - Bit 7 set when counter > 0, clear when counter <= 0
        Assert.Equal(0x80, value1 & 0x80); // Counter > 0
        Assert.Equal(0x80, value2 & 0x80); // Counter > 0
        Assert.Equal(0x00, value3 & 0x80); // Counter = 0
        Assert.Equal(0x00, value4 & 0x80); // Counter < 0
    }

    [Fact]
    public void Read_RD_VERTBLANK_TransitionBehavior()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();

        // Act & Assert - Start not in VBlank
        fixture.VBlank.VBlankCounter = 0; // Start not in VBlank
        var value1 = fixture.IoHandler.Read((ushort)(SystemIoHandler.RD_VERTBLANK_ & 0xFF));
        Assert.Equal(0x00, value1 & 0x80);
        
        // Enter VBlank
        fixture.VBlank.VBlankCounter = 4550; // Enter VBlank
        var value2 = fixture.IoHandler.Read((ushort)(SystemIoHandler.RD_VERTBLANK_ & 0xFF));
        Assert.Equal(0x80, value2 & 0x80);
        
        // Exit VBlank
        fixture.VBlank.VBlankCounter = 0; // Exit VBlank
        var value3 = fixture.IoHandler.Read((ushort)(SystemIoHandler.RD_VERTBLANK_ & 0xFF));
        Assert.Equal(0x00, value3 & 0x80);
    }

    [Fact]
    public void Read_RD_VERTBLANK_AllPossibleCounterValues()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();

        // Act & Assert - Test all typical counter values
        for (int i = -1000; i <= 5000; i++)
        {
            fixture.VBlank.VBlankCounter = i;
            var value = fixture.IoHandler.Read((ushort)(SystemIoHandler.RD_VERTBLANK_ & 0xFF));
            
            if (i > 0)
            {
                Assert.Equal(0x80, value & 0x80); // Should be in VBlank
            }
            else
            {
                Assert.Equal(0x00, value & 0x80); // Should NOT be in VBlank
            }
        }
    }

    #endregion

    #region Integration Tests (6 tests)

    [Fact]
    public void Integration_MixedIO_KeyboardAndButtons()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();

        // Act - Mix keyboard and button operations
        fixture.Keyboard.EnqueueKey(0xC1);
        fixture.GameController.SetButton(0, true);
        fixture.GameController.SetButton(1, true);

        // Assert - All I/O types work independently
        Assert.Equal(0xC1, fixture.IoHandler.Read((ushort)(SystemIoHandler.KBD_ & 0xFF)));
        Assert.Equal(0x80, fixture.IoHandler.Read((ushort)(SystemIoHandler.BUTTON0_ & 0xFF)));
        Assert.Equal(0x80, fixture.IoHandler.Read((ushort)(SystemIoHandler.BUTTON1_ & 0xFF)));
    }

    [Fact]
    public void Integration_MixedIO_SoftSwitchesAndKeyboard()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();

        // Act
        fixture.IoHandler.Write((ushort)(SystemIoHandler.SETTXT_ & 0xFF), 0);
        fixture.Keyboard.EnqueueKey(0xC5);

        // Assert
        Assert.True(fixture.StatusProvider.StateTextMode);
        Assert.Equal(0xC5, fixture.IoHandler.Read((ushort)(SystemIoHandler.KBD_ & 0xFF)));
    }

    [Fact]
    public void Integration_AllIOTypes_WorkSimultaneously()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();

        // Act - Set all I/O types
        fixture.Keyboard.EnqueueKey(0xC1);
        fixture.GameController.SetButton(0, true);
        fixture.GameController.SetButton(2, true);
        fixture.IoHandler.Write((ushort)(SystemIoHandler.SETHIRES_ & 0xFF), 0);
        fixture.IoHandler.Write((ushort)(SystemIoHandler.SETMIXED_ & 0xFF), 0);
        fixture.VBlank.VBlankCounter = 4550;

        // Assert - All I/O types readable
        Assert.Equal(0xC1, fixture.IoHandler.Read((ushort)(SystemIoHandler.KBD_ & 0xFF)));
        Assert.Equal(0x80, fixture.IoHandler.Read((ushort)(SystemIoHandler.BUTTON0_ & 0xFF)));
        Assert.Equal(0x00, fixture.IoHandler.Read((ushort)(SystemIoHandler.BUTTON1_ & 0xFF)));
        Assert.Equal(0x80, fixture.IoHandler.Read((ushort)(SystemIoHandler.BUTTON2_ & 0xFF)));
        Assert.True(fixture.StatusProvider.StateHiRes);
        Assert.True(fixture.StatusProvider.StateMixed);
        Assert.Equal(0x80, fixture.IoHandler.Read((ushort)(SystemIoHandler.RD_VERTBLANK_ & 0xFF)) & 0x80);
    }

    [Fact]
    public void Integration_RapidIOSwitching_HandlesCorrectly()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();

        // Act - Rapidly switch between I/O types
        for (int i = 0; i < 10; i++)
        {
            fixture.Keyboard.EnqueueKey((byte)(0xC0 + i));
            fixture.GameController.SetButton(i % 3, i % 2 == 0);
            fixture.IoHandler.Write((ushort)((i % 2 == 0 ? SystemIoHandler.SETTXT_ : SystemIoHandler.CLRTXT_) & 0xFF), 0);
            
            // Verify each operation
            Assert.Equal((byte)(0xC0 + i), fixture.IoHandler.Read((ushort)(SystemIoHandler.KBD_ & 0xFF)));
            Assert.Equal(fixture.StatusProvider.StateTextMode, i % 2 == 0);
        }
    }

    [Fact]
    public void Integration_GameplayScenario_ButtonsAndSwitches()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();

        // Act - Simulate game startup: set graphics mode, use controller
        fixture.IoHandler.Write((ushort)(SystemIoHandler.CLRTXT_ & 0xFF), 0);     // Graphics
        fixture.IoHandler.Write((ushort)(SystemIoHandler.SETHIRES_ & 0xFF), 0);   // Hi-res
        fixture.IoHandler.Write((ushort)(SystemIoHandler.CLRPAGE2_ & 0xFF), 0);   // Page 1
        
        fixture.GameController.SetButton(0, true);  // Fire button
        fixture.GameController.SetButton(1, true);  // Jump button

        // Assert
        Assert.False(fixture.StatusProvider.StateTextMode);
        Assert.True(fixture.StatusProvider.StateHiRes);
        Assert.False(fixture.StatusProvider.StatePage2);
        Assert.Equal(0x80, fixture.IoHandler.Read((ushort)(SystemIoHandler.BUTTON0_ & 0xFF)));
        Assert.Equal(0x80, fixture.IoHandler.Read((ushort)(SystemIoHandler.BUTTON1_ & 0xFF)));
    }

    [Fact]
    public void Integration_CompleteIOCoverage_AllAddresses()
    {
        // Arrange
        var fixture = new SystemIoHandlerFixture();

        // Act - Exercise all major I/O address categories
        fixture.Keyboard.EnqueueKey(0xC1);
        fixture.GameController.SetButton(0, true);
        fixture.GameController.SetButton(1, false);
        fixture.GameController.SetButton(2, true);
        fixture.IoHandler.Write((ushort)(SystemIoHandler.SETHIRES_ & 0xFF), 0);
        fixture.VBlank.VBlankCounter = 1000;

        // Assert - Spot check key addresses
        var kbdRead = fixture.IoHandler.Read((ushort)(SystemIoHandler.KBD_ & 0xFF));
        var button0Read = fixture.IoHandler.Read((ushort)(SystemIoHandler.BUTTON0_ & 0xFF));
        var button1Read = fixture.IoHandler.Read((ushort)(SystemIoHandler.BUTTON1_ & 0xFF));
        var button2Read = fixture.IoHandler.Read((ushort)(SystemIoHandler.BUTTON2_ & 0xFF));
        var hiresRead = fixture.IoHandler.Read((ushort)(SystemIoHandler.RD_HIRES_ & 0xFF));
        var vblankRead = fixture.IoHandler.Read((ushort)(SystemIoHandler.RD_VERTBLANK_ & 0xFF));

        Assert.Equal(0xC1, kbdRead);
        Assert.Equal(0x80, button0Read);
        Assert.Equal(0x00, button1Read);
        Assert.Equal(0x80, button2Read);
        Assert.Equal(0x80, hiresRead & 0x80);
        Assert.Equal(0x80, vblankRead & 0x80);
    }

    #endregion
}
