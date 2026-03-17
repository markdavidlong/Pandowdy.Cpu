// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore;
using Pandowdy.EmuCore.IO;
using Pandowdy.EmuCore.Machine;

namespace Pandowdy.EmuCore.Tests;

/// <summary>
/// Comprehensive tests for the SoftSwitches class.
/// Tests cover all 23 soft switches, initialization, state changes,
/// and integration with SystemStatusProvider.
/// </summary>
public class SoftSwitchesTests
{
    #region Test Helpers

    private static SoftSwitches CreateSoftSwitches(out SystemStatusProvider status)
    {
        status = new SystemStatusProvider();
        return new SoftSwitches(status);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullStatus_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SoftSwitches(null!));
    }

    [Fact]
    public void Constructor_WithValidStatus_InitializesAllSwitchesToFalse()
    {
        // Arrange & Act
        var switches = CreateSoftSwitches(out var status);

        // Assert - Verify all switches are false (off) by default
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.Store80));
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.RamRd));
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.RamWrt));
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.IntCxRom));
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.AltZp));
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.SlotC3Rom));
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.Vid80));
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.AltChar));
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.Text));
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.Mixed));
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.Page2));
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.HiRes));
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.An0));
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.An1));
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.An2));
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.An3));
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.Bank1));
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.HighWrite));
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.HighRead));
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.PreWrite));
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.VBlank));
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.IntC8Rom));
    }

    [Fact]
    public void Constructor_InitializesStatusProviderCorrectly()
    {
        // Arrange & Act
        var switches = CreateSoftSwitches(out var status);

        // Assert - Verify status provider has correct initial state
        Assert.False(status.State80Store);
        Assert.False(status.StateRamRd);
        Assert.False(status.StateRamWrt);
        Assert.False(status.StateIntCxRom);
        Assert.False(status.StateAltZp);
        Assert.False(status.StateSlotC3Rom);
        Assert.False(status.StateShow80Col);
        Assert.False(status.StateAltCharSet);
        Assert.False(status.StateTextMode);
        Assert.False(status.StateMixed);
        Assert.False(status.StatePage2);
        Assert.False(status.StateHiRes);
    }

    #endregion

    #region ResetAllSwitches Tests

    [Fact]
    public void ResetAllSwitches_ResetsAllSwitchesToFalse()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);
        
        // Set some switches to true
        switches.Set(SoftSwitches.SoftSwitchId.Text, true);
        switches.Set(SoftSwitches.SoftSwitchId.HiRes, true);
        switches.Set(SoftSwitches.SoftSwitchId.Page2, true);
        
        // Act
        switches.ResetAllSwitches();

        // Assert - All should be false
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.Text));
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.HiRes));
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.Page2));
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.IntCxRom));
    }

    [Fact]
    public void ResetAllSwitches_NotifiesStatusProvider()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);
        switches.Set(SoftSwitches.SoftSwitchId.Text, true);
        switches.Set(SoftSwitches.SoftSwitchId.HiRes, true);

        // Act
        switches.ResetAllSwitches();

        // Assert - Status provider should be reset
        Assert.False(status.StateTextMode);
        Assert.False(status.StateHiRes);
    }

    #endregion

    #region Set and Get Tests - Memory Mapping Switches

    [Fact]
    public void Set_Store80_UpdatesSwitchAndStatus()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);

        // Act
        switches.Set(SoftSwitches.SoftSwitchId.Store80, true);

        // Assert
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.Store80));
        Assert.True(status.State80Store);
    }

    [Fact]
    public void Set_Store80_ToFalse_UpdatesSwitchAndStatus()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);
        switches.Set(SoftSwitches.SoftSwitchId.Store80, true);

        // Act
        switches.Set(SoftSwitches.SoftSwitchId.Store80, false);

        // Assert
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.Store80));
        Assert.False(status.State80Store);
    }

    [Fact]
    public void Set_RamRd_UpdatesSwitchAndStatus()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);

        // Act
        switches.Set(SoftSwitches.SoftSwitchId.RamRd, true);

        // Assert
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.RamRd));
        Assert.True(status.StateRamRd);
    }

    [Fact]
    public void Set_RamWrt_UpdatesSwitchAndStatus()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);

        // Act
        switches.Set(SoftSwitches.SoftSwitchId.RamWrt, true);

        // Assert
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.RamWrt));
        Assert.True(status.StateRamWrt);
    }

    [Fact]
    public void Set_IntCxRom_UpdatesSwitchAndStatus()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);

        // Act
        switches.Set(SoftSwitches.SoftSwitchId.IntCxRom, true);

        // Assert
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.IntCxRom));
        Assert.True(status.StateIntCxRom);
    }

    [Fact]
    public void Set_AltZp_UpdatesSwitchAndStatus()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);

        // Act
        switches.Set(SoftSwitches.SoftSwitchId.AltZp, true);

        // Assert
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.AltZp));
        Assert.True(status.StateAltZp);
    }

    [Fact]
    public void Set_SlotC3Rom_UpdatesSwitchAndStatus()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);

        // Act
        switches.Set(SoftSwitches.SoftSwitchId.SlotC3Rom, true);

        // Assert
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.SlotC3Rom));
        Assert.True(status.StateSlotC3Rom);
    }

    #endregion

    #region Set and Get Tests - Video Mode Switches

    [Fact]
    public void Set_Vid80_UpdatesSwitchAndStatus()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);

        // Act
        switches.Set(SoftSwitches.SoftSwitchId.Vid80, true);

        // Assert
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.Vid80));
        Assert.True(status.StateShow80Col);
    }

    [Fact]
    public void Set_AltChar_UpdatesSwitchAndStatus()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);

        // Act
        switches.Set(SoftSwitches.SoftSwitchId.AltChar, true);

        // Assert
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.AltChar));
        Assert.True(status.StateAltCharSet);
    }

    [Fact]
    public void Set_Text_UpdatesSwitchAndStatus()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);

        // Act
        switches.Set(SoftSwitches.SoftSwitchId.Text, true);

        // Assert
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.Text));
        Assert.True(status.StateTextMode);
    }

    [Fact]
    public void Set_Mixed_UpdatesSwitchAndStatus()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);

        // Act
        switches.Set(SoftSwitches.SoftSwitchId.Mixed, true);

        // Assert
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.Mixed));
        Assert.True(status.StateMixed);
    }

    [Fact]
    public void Set_Page2_UpdatesSwitchAndStatus()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);

        // Act
        switches.Set(SoftSwitches.SoftSwitchId.Page2, true);

        // Assert
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.Page2));
        Assert.True(status.StatePage2);
    }

    [Fact]
    public void Set_HiRes_UpdatesSwitchAndStatus()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);

        // Act
        switches.Set(SoftSwitches.SoftSwitchId.HiRes, true);

        // Assert
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.HiRes));
        Assert.True(status.StateHiRes);
    }

    #endregion

    #region Set and Get Tests - Annunciators

    [Fact]
    public void Set_An0_UpdatesSwitchAndStatus()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);

        // Act
        switches.Set(SoftSwitches.SoftSwitchId.An0, true);

        // Assert
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.An0));
        Assert.True(status.StateAnn0);
    }

    [Fact]
    public void Set_An1_UpdatesSwitchAndStatus()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);

        // Act
        switches.Set(SoftSwitches.SoftSwitchId.An1, true);

        // Assert
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.An1));
        Assert.True(status.StateAnn1);
    }

    [Fact]
    public void Set_An2_UpdatesSwitchAndStatus()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);

        // Act
        switches.Set(SoftSwitches.SoftSwitchId.An2, true);

        // Assert
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.An2));
        Assert.True(status.StateAnn2);
    }

    [Fact]
    public void Set_An3_UpdatesSwitchAndStatus()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);

        // Act
        switches.Set(SoftSwitches.SoftSwitchId.An3, true);

        // Assert
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.An3));
        Assert.True(status.StateAnn3_DGR);
    }

    #endregion

    #region Set and Get Tests - Language Card Switches

    [Fact]
    public void Set_Bank1_UpdatesSwitchAndStatus()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);

        // Act
        switches.Set(SoftSwitches.SoftSwitchId.Bank1, true);

        // Assert
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.Bank1));
        Assert.True(status.StateUseBank1);
    }

    [Fact]
    public void Set_HighWrite_UpdatesSwitchAndStatus()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);

        // Act
        switches.Set(SoftSwitches.SoftSwitchId.HighWrite, true);

        // Assert
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.HighWrite));
        Assert.True(status.StateHighWrite);
    }

    [Fact]
    public void Set_HighRead_UpdatesSwitchAndStatus()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);

        // Act
        switches.Set(SoftSwitches.SoftSwitchId.HighRead, true);

        // Assert
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.HighRead));
        Assert.True(status.StateHighRead);
    }

    [Fact]
    public void Set_PreWrite_UpdatesSwitchAndStatus()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);

        // Act
        switches.Set(SoftSwitches.SoftSwitchId.PreWrite, true);

        // Assert
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.PreWrite));
        Assert.True(status.StatePreWrite);
    }

    #endregion

    #region Set and Get Tests - Special Switches

    [Fact]
    public void Set_VBlank_UpdatesSwitchAndStatus()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);

        // Act
        switches.Set(SoftSwitches.SoftSwitchId.VBlank, true);

        // Assert
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.VBlank));
        Assert.True(status.StateVBlank);
    }

    [Fact]
    public void Set_IntC8Rom_UpdatesSwitchAndStatus()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);

        // Act
        switches.Set(SoftSwitches.SoftSwitchId.IntC8Rom, true);

        // Assert
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.IntC8Rom));
        Assert.True(status.StateIntC8Rom);
    }

    #endregion

    #region QuietlySet Tests

    [Fact]
    public void QuietlySet_UpdatesSwitchWithoutNotifyingStatus()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);

        // Act
        bool result = switches.QuietlySet(SoftSwitches.SoftSwitchId.Text, true);

        // Assert
        Assert.True(result);
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.Text));
        
        // Status should still be false since QuietlySet doesn't notify
        Assert.False(status.StateTextMode);
    }

    [Fact]
    public void QuietlySet_ReturnsTrueForValidSwitch()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);

        // Act
        bool result = switches.QuietlySet(SoftSwitches.SoftSwitchId.HiRes, true);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region Multiple Switch Changes Tests

    [Fact]
    public void Set_MultipleVideoModeSwitches_AllUpdateCorrectly()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);

        // Act
        switches.Set(SoftSwitches.SoftSwitchId.Text, true);
        switches.Set(SoftSwitches.SoftSwitchId.Mixed, true);
        switches.Set(SoftSwitches.SoftSwitchId.Page2, true);
        switches.Set(SoftSwitches.SoftSwitchId.HiRes, true);

        // Assert
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.Text));
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.Mixed));
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.Page2));
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.HiRes));
        
        Assert.True(status.StateTextMode);
        Assert.True(status.StateMixed);
        Assert.True(status.StatePage2);
        Assert.True(status.StateHiRes);
    }

    [Fact]
    public void Set_MultipleMemorySwitches_AllUpdateCorrectly()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);

        // Act
        switches.Set(SoftSwitches.SoftSwitchId.Store80, true);
        switches.Set(SoftSwitches.SoftSwitchId.RamRd, true);
        switches.Set(SoftSwitches.SoftSwitchId.RamWrt, true);
        switches.Set(SoftSwitches.SoftSwitchId.AltZp, true);

        // Assert
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.Store80));
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.RamRd));
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.RamWrt));
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.AltZp));
        
        Assert.True(status.State80Store);
        Assert.True(status.StateRamRd);
        Assert.True(status.StateRamWrt);
        Assert.True(status.StateAltZp);
    }

    [Fact]
    public void Set_AllAnnunciators_UpdateCorrectly()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);

        // Act
        switches.Set(SoftSwitches.SoftSwitchId.An0, true);
        switches.Set(SoftSwitches.SoftSwitchId.An1, true);
        switches.Set(SoftSwitches.SoftSwitchId.An2, true);
        switches.Set(SoftSwitches.SoftSwitchId.An3, true);

        // Assert
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.An0));
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.An1));
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.An2));
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.An3));
        
        Assert.True(status.StateAnn0);
        Assert.True(status.StateAnn1);
        Assert.True(status.StateAnn2);
        Assert.True(status.StateAnn3_DGR);
    }

    #endregion

    #region Toggle Tests

    [Fact]
    public void Set_ToggleSwitchOnAndOff_UpdatesCorrectly()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);

        // Act & Assert - Turn on
        switches.Set(SoftSwitches.SoftSwitchId.Text, true);
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.Text));
        Assert.True(status.StateTextMode);

        // Act & Assert - Turn off
        switches.Set(SoftSwitches.SoftSwitchId.Text, false);
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.Text));
        Assert.False(status.StateTextMode);

        // Act & Assert - Turn on again
        switches.Set(SoftSwitches.SoftSwitchId.Text, true);
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.Text));
        Assert.True(status.StateTextMode);
    }

    [Fact]
    public void Set_SettingSameSwitchTwice_DoesNotCauseIssues()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);

        // Act
        switches.Set(SoftSwitches.SoftSwitchId.HiRes, true);
        switches.Set(SoftSwitches.SoftSwitchId.HiRes, true);

        // Assert
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.HiRes));
        Assert.True(status.StateHiRes);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void Set_ComplexVideoModeSequence_UpdatesCorrectly()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);

        // Act - Simulate switching from text mode to hi-res graphics
        switches.Set(SoftSwitches.SoftSwitchId.Text, true);   // Text mode on
        switches.Set(SoftSwitches.SoftSwitchId.Text, false);  // Text mode off (graphics)
        switches.Set(SoftSwitches.SoftSwitchId.HiRes, true);  // Hi-res mode
        switches.Set(SoftSwitches.SoftSwitchId.Page2, true);  // Page 2

        // Assert
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.Text));
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.HiRes));
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.Page2));
        
        Assert.False(status.StateTextMode);
        Assert.True(status.StateHiRes);
        Assert.True(status.StatePage2);
    }

    [Fact]
    public void Set_80ColumnModeConfiguration_UpdatesCorrectly()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);

        // Act - Enable 80-column mode with store and alternate character set
        switches.Set(SoftSwitches.SoftSwitchId.Store80, true);
        switches.Set(SoftSwitches.SoftSwitchId.Vid80, true);
        switches.Set(SoftSwitches.SoftSwitchId.AltChar, true);

        // Assert
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.Store80));
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.Vid80));
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.AltChar));
        
        Assert.True(status.State80Store);
        Assert.True(status.StateShow80Col);
        Assert.True(status.StateAltCharSet);
    }

    [Fact]
    public void Set_LanguageCardSequence_UpdatesCorrectly()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);

        // Act - Enable language card bank 1 with write enabled
        switches.Set(SoftSwitches.SoftSwitchId.Bank1, true);
        switches.Set(SoftSwitches.SoftSwitchId.HighRead, true);
        switches.Set(SoftSwitches.SoftSwitchId.PreWrite, true);
        switches.Set(SoftSwitches.SoftSwitchId.HighWrite, true);

        // Assert
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.Bank1));
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.HighRead));
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.PreWrite));
        Assert.True(switches.Get(SoftSwitches.SoftSwitchId.HighWrite));
        
        Assert.True(status.StateUseBank1);
        Assert.True(status.StateHighRead);
        Assert.True(status.StatePreWrite);
        Assert.True(status.StateHighWrite);
    }

    [Fact]
    public void ResetAllSwitches_AfterComplexConfiguration_ResetsEverything()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);
        
        // Set up complex configuration
        switches.Set(SoftSwitches.SoftSwitchId.Text, true);
        switches.Set(SoftSwitches.SoftSwitchId.Mixed, true);
        switches.Set(SoftSwitches.SoftSwitchId.HiRes, true);
        switches.Set(SoftSwitches.SoftSwitchId.Page2, true);
        switches.Set(SoftSwitches.SoftSwitchId.Store80, true);
        switches.Set(SoftSwitches.SoftSwitchId.RamRd, true);
        switches.Set(SoftSwitches.SoftSwitchId.RamWrt, true);
        switches.Set(SoftSwitches.SoftSwitchId.An0, true);
        switches.Set(SoftSwitches.SoftSwitchId.An1, true);

        // Act
        switches.ResetAllSwitches();

        // Assert - Everything should be reset
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.Text));
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.Mixed));
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.HiRes));
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.Page2));
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.Store80));
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.RamRd));
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.RamWrt));
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.An0));
        Assert.False(switches.Get(SoftSwitches.SoftSwitchId.An1));
        
        Assert.False(status.StateTextMode);
        Assert.False(status.StateMixed);
        Assert.False(status.StateHiRes);
        Assert.False(status.StatePage2);
        Assert.False(status.State80Store);
        Assert.False(status.StateRamRd);
        Assert.False(status.StateRamWrt);
        Assert.False(status.StateAnn0);
        Assert.False(status.StateAnn1);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Get_NonExistentSwitch_ReturnsFalse()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);

        // Act - Try to get a switch with invalid enum value (cast from int)
        bool result = switches.Get((SoftSwitches.SoftSwitchId)999);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void QuietlySet_NonExistentSwitch_ReturnsFalse()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);

        // Act - Try to set a switch with invalid enum value
        bool result = switches.QuietlySet((SoftSwitches.SoftSwitchId)999, true);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Get_NegativeIndex_ReturnsFalse()
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);

        // Act
        bool result = switches.Get((SoftSwitches.SoftSwitchId)(-1));

        // Assert
        Assert.False(result);
    }

    #endregion

    #region All Switches Comprehensive Test

    [Theory]
    [InlineData(SoftSwitches.SoftSwitchId.Store80)]
    [InlineData(SoftSwitches.SoftSwitchId.RamRd)]
    [InlineData(SoftSwitches.SoftSwitchId.RamWrt)]
    [InlineData(SoftSwitches.SoftSwitchId.IntCxRom)]
    [InlineData(SoftSwitches.SoftSwitchId.AltZp)]
    [InlineData(SoftSwitches.SoftSwitchId.SlotC3Rom)]
    [InlineData(SoftSwitches.SoftSwitchId.Vid80)]
    [InlineData(SoftSwitches.SoftSwitchId.AltChar)]
    [InlineData(SoftSwitches.SoftSwitchId.Text)]
    [InlineData(SoftSwitches.SoftSwitchId.Mixed)]
    [InlineData(SoftSwitches.SoftSwitchId.Page2)]
    [InlineData(SoftSwitches.SoftSwitchId.HiRes)]
    [InlineData(SoftSwitches.SoftSwitchId.An0)]
    [InlineData(SoftSwitches.SoftSwitchId.An1)]
    [InlineData(SoftSwitches.SoftSwitchId.An2)]
    [InlineData(SoftSwitches.SoftSwitchId.An3)]
    [InlineData(SoftSwitches.SoftSwitchId.Bank1)]
    [InlineData(SoftSwitches.SoftSwitchId.HighWrite)]
    [InlineData(SoftSwitches.SoftSwitchId.HighRead)]
    [InlineData(SoftSwitches.SoftSwitchId.PreWrite)]
    [InlineData(SoftSwitches.SoftSwitchId.VBlank)]
    [InlineData(SoftSwitches.SoftSwitchId.IntC8Rom)]
    public void Set_EachSwitch_CanBeSetAndRetrieved(SoftSwitches.SoftSwitchId switchId)
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);

        // Act
        switches.Set(switchId, true);

        // Assert
        Assert.True(switches.Get(switchId));
    }

    [Theory]
    [InlineData(SoftSwitches.SoftSwitchId.Store80)]
    [InlineData(SoftSwitches.SoftSwitchId.RamRd)]
    [InlineData(SoftSwitches.SoftSwitchId.RamWrt)]
    [InlineData(SoftSwitches.SoftSwitchId.IntCxRom)]
    [InlineData(SoftSwitches.SoftSwitchId.AltZp)]
    [InlineData(SoftSwitches.SoftSwitchId.SlotC3Rom)]
    [InlineData(SoftSwitches.SoftSwitchId.Vid80)]
    [InlineData(SoftSwitches.SoftSwitchId.AltChar)]
    [InlineData(SoftSwitches.SoftSwitchId.Text)]
    [InlineData(SoftSwitches.SoftSwitchId.Mixed)]
    [InlineData(SoftSwitches.SoftSwitchId.Page2)]
    [InlineData(SoftSwitches.SoftSwitchId.HiRes)]
    [InlineData(SoftSwitches.SoftSwitchId.An0)]
    [InlineData(SoftSwitches.SoftSwitchId.An1)]
    [InlineData(SoftSwitches.SoftSwitchId.An2)]
    [InlineData(SoftSwitches.SoftSwitchId.An3)]
    [InlineData(SoftSwitches.SoftSwitchId.Bank1)]
    [InlineData(SoftSwitches.SoftSwitchId.HighWrite)]
    [InlineData(SoftSwitches.SoftSwitchId.HighRead)]
    [InlineData(SoftSwitches.SoftSwitchId.PreWrite)]
    [InlineData(SoftSwitches.SoftSwitchId.VBlank)]
    [InlineData(SoftSwitches.SoftSwitchId.IntC8Rom)]
    public void Set_EachSwitch_CanBeSetToFalse(SoftSwitches.SoftSwitchId switchId)
    {
        // Arrange
        var switches = CreateSoftSwitches(out var status);
        switches.Set(switchId, true);

        // Act
        switches.Set(switchId, false);

        // Assert
        Assert.False(switches.Get(switchId));
    }

    #endregion
}
