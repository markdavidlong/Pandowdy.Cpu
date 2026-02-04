// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Services;

namespace Pandowdy.EmuCore.Tests.Services;

/// <summary>
/// Unit tests for SystemStatusProvider, which manages Apple II system state
/// and implements both ISystemStatusProvider (read-only) and ISystemStatusMutator (read-write).
/// </summary>
public class SystemStatusProviderTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_InitializesWithDefaultState()
    {
        // Arrange & Act
        var provider = new SystemStatusProvider();

        // Assert - Check default Apple II power-on state
        Assert.False(provider.State80Store, "80STORE should default to false");
        Assert.False(provider.StateRamRd, "RAMRD should default to false");
        Assert.False(provider.StateRamWrt, "RAMWRT should default to false");
        Assert.False(provider.StateIntCxRom, "INTCXROM should default to false");
        Assert.False(provider.StateAltZp, "ALTZP should default to false");
        Assert.False(provider.StateSlotC3Rom, "SLOTC3ROM should default to false");
        
        // Video mode defaults
        Assert.True(provider.StateTextMode, "TEXT mode should be on by default");
        Assert.False(provider.StateHiRes, "HIRES should default to false");
        Assert.False(provider.StateMixed, "MIXED should default to false");
        Assert.False(provider.StatePage2, "PAGE2 should default to false");
        Assert.False(provider.StateShow80Col, "80COL should default to false");
        Assert.False(provider.StateAltCharSet, "ALTCHAR should default to false");
        
        // Annunciators default to false
        Assert.False(provider.StateAnn0, "AN0 should default to false");
        Assert.False(provider.StateAnn1, "AN1 should default to false");
        Assert.False(provider.StateAnn2, "AN2 should default to false");
        Assert.False(provider.StateAnn3_DGR, "AN3 should default to false");
        
        // Language card defaults
        Assert.False(provider.StateUseBank1, "BANK1 should default to false");
        Assert.False(provider.StateHighRead, "HIGHREAD should default to false");
        Assert.False(provider.StateHighWrite, "HIGHWRITE should default to false");
        Assert.False(provider.StatePreWrite, "PREWRITE should default to false");
        
        // Flash state
        Assert.False(provider.StateFlashOn, "FLASHON should default to false");
    }

    [Fact]
    public void Constructor_InitializesCurrentSnapshot()
    {
        // Arrange & Act
        var provider = new SystemStatusProvider();

        // Assert
        Assert.NotNull(provider.Current);
        Assert.False(provider.Current.StateIntCxRom);
        Assert.True(provider.Current.StateTextMode);
    }

    [Fact]
    public void Constructor_InitializesStream()
    {
        // Arrange & Act
        var provider = new SystemStatusProvider();

        // Assert
        Assert.NotNull(provider.Stream);
    }

    #endregion

    #region ISystemStatusMutator Memory Configuration Tests

    [Fact]
    public void Set80Store_UpdatesStateCorrectly()
    {
        // Arrange
        var provider = new SystemStatusProvider();

        // Act
        provider.Set80Store(true);

        // Assert
        Assert.True(provider.State80Store);
        Assert.True(provider.Current.State80Store);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetRamRd_TogglesCorrectly(bool expectedState)
    {
        // Arrange
        var provider = new SystemStatusProvider();

        // Act
        provider.SetRamRd(expectedState);

        // Assert
        Assert.Equal(expectedState, provider.StateRamRd);
        Assert.Equal(expectedState, provider.Current.StateRamRd);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetRamWrt_TogglesCorrectly(bool expectedState)
    {
        // Arrange
        var provider = new SystemStatusProvider();

        // Act
        provider.SetRamWrt(expectedState);

        // Assert
        Assert.Equal(expectedState, provider.StateRamWrt);
        Assert.Equal(expectedState, provider.Current.StateRamWrt);
    }

    [Fact]
    public void SetIntCxRom_CanToggleFromDefaultFalse()
    {
        // Arrange
        var provider = new SystemStatusProvider();
        Assert.False(provider.StateIntCxRom); // Verify default

        // Act
        provider.SetIntCxRom(true);

        // Assert
        Assert.True(provider.StateIntCxRom);
        Assert.True(provider.Current.StateIntCxRom);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetAltZp_TogglesCorrectly(bool expectedState)
    {
        // Arrange
        var provider = new SystemStatusProvider();

        // Act
        provider.SetAltZp(expectedState);

        // Assert
        Assert.Equal(expectedState, provider.StateAltZp);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetSlotC3Rom_TogglesCorrectly(bool expectedState)
    {
        // Arrange
        var provider = new SystemStatusProvider();

        // Act
        provider.SetSlotC3Rom(expectedState);

        // Assert
        Assert.Equal(expectedState, provider.StateSlotC3Rom);
    }

    #endregion

    #region ISystemStatusMutator Video Mode Tests

    [Fact]
    public void Set80Vid_UpdatesShow80Col()
    {
        // Arrange
        var provider = new SystemStatusProvider();

        // Act
        provider.Set80Vid(true);

        // Assert
        Assert.True(provider.StateShow80Col);
    }

    [Fact]
    public void SetAltChar_UpdatesAltCharSet()
    {
        // Arrange
        var provider = new SystemStatusProvider();

        // Act
        provider.SetAltChar(true);

        // Assert
        Assert.True(provider.StateAltCharSet);
    }

    [Fact]
    public void SetText_CanToggleTextMode()
    {
        // Arrange
        var provider = new SystemStatusProvider();
        Assert.True(provider.StateTextMode); // Default

        // Act - Turn off text mode
        provider.SetText(false);

        // Assert
        Assert.False(provider.StateTextMode);

        // Act - Turn back on
        provider.SetText(true);

        // Assert
        Assert.True(provider.StateTextMode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetMixed_TogglesCorrectly(bool expectedState)
    {
        // Arrange
        var provider = new SystemStatusProvider();

        // Act
        provider.SetMixed(expectedState);

        // Assert
        Assert.Equal(expectedState, provider.StateMixed);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetPage2_TogglesCorrectly(bool expectedState)
    {
        // Arrange
        var provider = new SystemStatusProvider();

        // Act
        provider.SetPage2(expectedState);

        // Assert
        Assert.Equal(expectedState, provider.StatePage2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetHiRes_TogglesCorrectly(bool expectedState)
    {
        // Arrange
        var provider = new SystemStatusProvider();

        // Act
        provider.SetHiRes(expectedState);

        // Assert
        Assert.Equal(expectedState, provider.StateHiRes);
    }

    #endregion

    #region ISystemStatusMutator Annunciator Tests

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetAn0_TogglesCorrectly(bool expectedState)
    {
        // Arrange
        var provider = new SystemStatusProvider();

        // Act
        provider.SetAn0(expectedState);

        // Assert
        Assert.Equal(expectedState, provider.StateAnn0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetAn1_TogglesCorrectly(bool expectedState)
    {
        // Arrange
        var provider = new SystemStatusProvider();

        // Act
        provider.SetAn1(expectedState);

        // Assert
        Assert.Equal(expectedState, provider.StateAnn1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetAn2_TogglesCorrectly(bool expectedState)
    {
        // Arrange
        var provider = new SystemStatusProvider();

        // Act
        provider.SetAn2(expectedState);

        // Assert
        Assert.Equal(expectedState, provider.StateAnn2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetAn3_TogglesCorrectly(bool expectedState)
    {
        // Arrange
        var provider = new SystemStatusProvider();

        // Act
        provider.SetAn3(expectedState);

        // Assert
        Assert.Equal(expectedState, provider.StateAnn3_DGR);
    }

    #endregion

    #region ISystemStatusMutator Language Card Tests

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetBank1_TogglesCorrectly(bool expectedState)
    {
        // Arrange
        var provider = new SystemStatusProvider();

        // Act
        provider.SetBank1(expectedState);

        // Assert
        Assert.Equal(expectedState, provider.StateUseBank1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetHighWrite_TogglesCorrectly(bool expectedState)
    {
        // Arrange
        var provider = new SystemStatusProvider();

        // Act
        provider.SetHighWrite(expectedState);

        // Assert
        Assert.Equal(expectedState, provider.StateHighWrite);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetHighRead_TogglesCorrectly(bool expectedState)
    {
        // Arrange
        var provider = new SystemStatusProvider();

        // Act
        provider.SetHighRead(expectedState);

        // Assert
        Assert.Equal(expectedState, provider.StateHighRead);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetPreWrite_TogglesCorrectly(bool expectedState)
    {
        // Arrange
        var provider = new SystemStatusProvider();

        // Act
        provider.SetPreWrite(expectedState);

        // Assert
        Assert.Equal(expectedState, provider.StatePreWrite);
    }

    #endregion

    #region Mutate Tests

    [Fact]
    public void Mutate_UpdatesSingleState()
    {
        // Arrange
        var provider = new SystemStatusProvider();

        // Act
        provider.Mutate(b => b.StateHiRes = true);

        // Assert
        Assert.True(provider.StateHiRes);
        Assert.True(provider.Current.StateHiRes);
    }

    [Fact]
    public void Mutate_UpdatesMultipleStatesAtomically()
    {
        // Arrange
        var provider = new SystemStatusProvider();

        // Act
        provider.Mutate(b =>
        {
            b.StateTextMode = false;
            b.StateHiRes = true;
            b.StatePage2 = true;
            b.StateMixed = false;
        });

        // Assert
        Assert.False(provider.StateTextMode);
        Assert.True(provider.StateHiRes);
        Assert.True(provider.StatePage2);
        Assert.False(provider.StateMixed);
    }

    [Fact]
    public void Mutate_CreatesNewSnapshot()
    {
        // Arrange
        var provider = new SystemStatusProvider();
        var originalSnapshot = provider.Current;

        // Act
        provider.Mutate(b => b.StatePage2 = true);

        // Assert
        var newSnapshot = provider.Current;
        Assert.NotEqual(originalSnapshot, newSnapshot);
        Assert.False(originalSnapshot.StatePage2);
        Assert.True(newSnapshot.StatePage2);
    }

    [Fact]
    public void Mutate_CanSetFlashOn()
    {
        // Arrange
        var provider = new SystemStatusProvider();

        // Act
        provider.Mutate(b => b.StateFlashOn = true);

        // Assert
        Assert.True(provider.StateFlashOn);
        Assert.True(provider.Current.StateFlashOn);
    }

    [Fact]
    public void Mutate_CanSetPushbuttons()
    {
        // Arrange
        var provider = new SystemStatusProvider();

        // Act
        provider.Mutate(b =>
        {
            b.StatePb0 = true;
            b.StatePb1 = false;
            b.StatePb2 = true;
        });

        // Assert
        Assert.True(provider.StatePb0);
        Assert.False(provider.StatePb1);
        Assert.True(provider.StatePb2);
    }

    #endregion

    #region Event Tests

    [Fact]
    public void Changed_EventRaisedWhenStateChanges()
    {
        // Arrange
        var provider = new SystemStatusProvider();
        var eventRaised = false;
        SystemStatusSnapshot? capturedSnapshot = null;

        provider.Changed += (sender, snapshot) =>
        {
            eventRaised = true;
            capturedSnapshot = snapshot;
        };

        // Act
        provider.SetText(false);

        // Assert
        Assert.True(eventRaised, "Changed event should be raised");
        Assert.NotNull(capturedSnapshot);
        Assert.False(capturedSnapshot.StateTextMode);
    }

    [Fact]
    public void Changed_EventRaisedMultipleTimes()
    {
        // Arrange
        var provider = new SystemStatusProvider();
        var eventCount = 0;

        provider.Changed += (sender, snapshot) => eventCount++;

        // Act
        provider.SetHiRes(true);
        provider.SetPage2(true);
        provider.SetMixed(true);

        // Assert
        Assert.Equal(3, eventCount);
    }

    [Fact]
    public void Changed_EventIncludesCorrectSender()
    {
        // Arrange
        var provider = new SystemStatusProvider();
        object? capturedSender = null;

        provider.Changed += (sender, snapshot) => capturedSender = sender;

        // Act
        provider.SetHiRes(true);

        // Assert
        Assert.Same(provider, capturedSender);
    }

    #endregion

    #region Stream (IObservable) Tests

    [Fact]
    public void Stream_EmitsInitialValue()
    {
        // Arrange
        var provider = new SystemStatusProvider();
        SystemStatusSnapshot? receivedSnapshot = null;

        // Act
        var subscription = provider.Stream.Subscribe(snapshot => receivedSnapshot = snapshot);

        // Assert
        Assert.NotNull(receivedSnapshot);
        Assert.True(receivedSnapshot.StateTextMode); // Default value
    }

    [Fact]
    public void Stream_EmitsUpdatesOnStateChange()
    {
        // Arrange
        var provider = new SystemStatusProvider();
        var receivedSnapshots = new List<SystemStatusSnapshot>();

        provider.Stream.Subscribe(snapshot => receivedSnapshots.Add(snapshot));

        // Act
        provider.SetHiRes(true);
        provider.SetPage2(true);

        // Assert
        Assert.True(receivedSnapshots.Count >= 3); // Initial + 2 updates
        Assert.True(receivedSnapshots[^1].StateHiRes);
        Assert.True(receivedSnapshots[^1].StatePage2);
    }

    [Fact]
    public void Stream_EmitsOnMutate()
    {
        // Arrange
        var provider = new SystemStatusProvider();
        SystemStatusSnapshot? lastSnapshot = null;

        provider.Stream.Subscribe(snapshot => lastSnapshot = snapshot);

        // Act
        provider.Mutate(b =>
        {
            b.StateHiRes = true;
            b.StateMixed = true;
        });

        // Assert
        Assert.NotNull(lastSnapshot);
        Assert.True(lastSnapshot.StateHiRes);
        Assert.True(lastSnapshot.StateMixed);
    }

    #endregion

    #region Integration Scenarios

    [Fact]
    public void Scenario_EnterHiResMode()
    {
        // Arrange
        var provider = new SystemStatusProvider();

        // Act - Simulate entering Hi-Res graphics mode
        provider.SetText(false);  // Turn off text
        provider.SetHiRes(true);  // Turn on hi-res
        provider.SetPage2(false); // Use page 1

        // Assert
        Assert.False(provider.StateTextMode);
        Assert.True(provider.StateHiRes);
        Assert.False(provider.StatePage2);
        Assert.False(provider.StateMixed);
    }

    [Fact]
    public void Scenario_EnterMixedMode()
    {
        // Arrange
        var provider = new SystemStatusProvider();

        // Act - Simulate entering mixed text/graphics mode
        provider.SetText(false);  // Turn off full text
        provider.SetHiRes(true);  // Graphics portion is hi-res
        provider.SetMixed(true);  // Enable mixed mode

        // Assert
        Assert.False(provider.StateTextMode);
        Assert.True(provider.StateHiRes);
        Assert.True(provider.StateMixed);
    }

    [Fact]
    public void Scenario_Enable80ColumnTextMode()
    {
        // Arrange
        var provider = new SystemStatusProvider();

        // Act - Simulate Apple IIe 80-column text mode
        provider.SetText(true);
        provider.Set80Vid(true);
        provider.Set80Store(true);

        // Assert
        Assert.True(provider.StateTextMode);
        Assert.True(provider.StateShow80Col);
        Assert.True(provider.State80Store);
    }

    [Fact]
    public void Scenario_LanguageCardBank1WriteEnable()
    {
        // Arrange
        var provider = new SystemStatusProvider();

        // Act - Simulate language card bank 1 write sequence
        provider.SetBank1(true);
        provider.SetHighWrite(true);
        provider.SetHighRead(true);

        // Assert
        Assert.True(provider.StateUseBank1);
        Assert.True(provider.StateHighWrite);
        Assert.True(provider.StateHighRead);
    }

    [Fact]
    public void Scenario_AllAnnunciatorsOn()
    {
        // Arrange
        var provider = new SystemStatusProvider();

        // Act
        provider.SetAn0(true);
        provider.SetAn1(true);
        provider.SetAn2(true);
        provider.SetAn3(true);

        // Assert
        Assert.True(provider.StateAnn0);
        Assert.True(provider.StateAnn1);
        Assert.True(provider.StateAnn2);
        Assert.True(provider.StateAnn3_DGR);
    }

    #endregion

    #region Snapshot Immutability Tests

    [Fact]
    public void Current_ReturnsNewSnapshotAfterMutation()
    {
        // Arrange
        var provider = new SystemStatusProvider();
        var snapshot1 = provider.Current;

        // Act
        provider.SetHiRes(true);
        var snapshot2 = provider.Current;

        // Assert
        Assert.NotEqual(snapshot1, snapshot2);
        Assert.False(snapshot1.StateHiRes);
        Assert.True(snapshot2.StateHiRes);
    }

    [Fact]
    public void SnapshotBuilder_PreservesUnchangedValues()
    {
        // Arrange
        var provider = new SystemStatusProvider();
        provider.SetHiRes(true);
        provider.SetPage2(true);

        // Act - Only change one value
        provider.Mutate(b => b.StateMixed = true);

        // Assert - Previous values preserved
        Assert.True(provider.StateHiRes);
        Assert.True(provider.StatePage2);
        Assert.True(provider.StateMixed);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void SetSameValue_StillRaisesEvent()
    {
        // Arrange
        var provider = new SystemStatusProvider();
        provider.SetHiRes(true);
        var eventCount = 0;

        provider.Changed += (sender, snapshot) => eventCount++;

        // Act - Set same value again
        provider.SetHiRes(true);

        // Assert - Event still raised (this is the current behavior)
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void MultipleSubscribers_AllReceiveUpdates()
    {
        // Arrange
        var provider = new SystemStatusProvider();
        var subscriber1Received = false;
        var subscriber2Received = false;

        provider.Changed += (s, snap) => subscriber1Received = true;
        provider.Changed += (s, snap) => subscriber2Received = true;

        // Act
        provider.SetHiRes(true);

        // Assert
        Assert.True(subscriber1Received);
        Assert.True(subscriber2Received);
    }

    [Fact]
    public void EmptyMutate_StillPublishesEvent()
    {
        // Arrange
        var provider = new SystemStatusProvider();
        var eventRaised = false;

        provider.Changed += (s, snap) => eventRaised = true;

        // Act - Mutate with no changes
        provider.Mutate(b => { });

        // Assert - Event is still raised even with no changes
        Assert.True(eventRaised);
    }

    #endregion

    #region MemoryMappingChanged Event Tests

    [Fact]
    public void MemoryMappingChanged_FiresWhenRamRdChanges()
    {
        // Arrange
        var provider = new SystemStatusProvider();
        var eventRaised = false;
        SystemStatusSnapshot? capturedSnapshot = null;

        provider.MemoryMappingChanged += (sender, snapshot) =>
        {
            eventRaised = true;
            capturedSnapshot = snapshot;
        };

        // Act
        provider.SetRamRd(true);

        // Assert
        Assert.True(eventRaised, "MemoryMappingChanged should fire when RAMRD changes");
        Assert.NotNull(capturedSnapshot);
        Assert.True(capturedSnapshot.StateRamRd);
    }

    [Fact]
    public void MemoryMappingChanged_FiresWhenRamWrtChanges()
    {
        // Arrange
        var provider = new SystemStatusProvider();
        var eventRaised = false;

        provider.MemoryMappingChanged += (sender, snapshot) => eventRaised = true;

        // Act
        provider.SetRamWrt(true);

        // Assert
        Assert.True(eventRaised, "MemoryMappingChanged should fire when RAMWRT changes");
    }

    [Fact]
    public void MemoryMappingChanged_FiresWhenAltZpChanges()
    {
        // Arrange
        var provider = new SystemStatusProvider();
        var eventRaised = false;

        provider.MemoryMappingChanged += (sender, snapshot) => eventRaised = true;

        // Act
        provider.SetAltZp(true);

        // Assert
        Assert.True(eventRaised, "MemoryMappingChanged should fire when ALTZP changes");
    }

    [Fact]
    public void MemoryMappingChanged_FiresWhen80StoreChanges()
    {
        // Arrange
        var provider = new SystemStatusProvider();
        var eventRaised = false;

        provider.MemoryMappingChanged += (sender, snapshot) => eventRaised = true;

        // Act
        provider.Set80Store(true);

        // Assert
        Assert.True(eventRaised, "MemoryMappingChanged should fire when 80STORE changes");
    }

    [Fact]
    public void MemoryMappingChanged_FiresWhenHiResChanges()
    {
        // Arrange
        var provider = new SystemStatusProvider();
        var eventRaised = false;

        provider.MemoryMappingChanged += (sender, snapshot) => eventRaised = true;

        // Act
        provider.SetHiRes(true);

        // Assert
        Assert.True(eventRaised, "MemoryMappingChanged should fire when HIRES changes");
    }

    [Fact]
    public void MemoryMappingChanged_FiresWhenPage2Changes()
    {
        // Arrange
        var provider = new SystemStatusProvider();
        var eventRaised = false;

        provider.MemoryMappingChanged += (sender, snapshot) => eventRaised = true;

        // Act
        provider.SetPage2(true);

        // Assert
        Assert.True(eventRaised, "MemoryMappingChanged should fire when PAGE2 changes");
    }

    [Fact]
    public void MemoryMappingChanged_FiresWhenIntCxRomChanges()
    {
        // Arrange
        var provider = new SystemStatusProvider();
        var eventRaised = false;

        provider.MemoryMappingChanged += (sender, snapshot) => eventRaised = true;

        // Act
        provider.SetIntCxRom(true); // Default is false, change to true

        // Assert
        Assert.True(eventRaised, "MemoryMappingChanged should fire when INTCXROM changes");
    }

    [Fact]
    public void MemoryMappingChanged_FiresWhenSlotC3RomChanges()
    {
        // Arrange
        var provider = new SystemStatusProvider();
        var eventRaised = false;

        provider.MemoryMappingChanged += (sender, snapshot) => eventRaised = true;

        // Act
        provider.SetSlotC3Rom(true);

        // Assert
        Assert.True(eventRaised, "MemoryMappingChanged should fire when SLOTC3ROM changes");
    }

    [Fact]
    public void MemoryMappingChanged_FiresWhenHighWriteChanges()
    {
        // Arrange
        var provider = new SystemStatusProvider();
        var eventRaised = false;

        provider.MemoryMappingChanged += (sender, snapshot) => eventRaised = true;

        // Act
        provider.SetHighWrite(true);

        // Assert
        Assert.True(eventRaised, "MemoryMappingChanged should fire when HIGHWRITE changes");
    }

    [Fact]
    public void MemoryMappingChanged_FiresWhenBank1Changes()
    {
        // Arrange
        var provider = new SystemStatusProvider();
        var eventRaised = false;

        provider.MemoryMappingChanged += (sender, snapshot) => eventRaised = true;

        // Act
        provider.SetBank1(true);

        // Assert
        Assert.True(eventRaised, "MemoryMappingChanged should fire when BANK1 changes");
    }

    [Fact]
    public void MemoryMappingChanged_FiresWhenHighReadChanges()
    {
        // Arrange
        var provider = new SystemStatusProvider();
        var eventRaised = false;

        provider.MemoryMappingChanged += (sender, snapshot) => eventRaised = true;

        // Act
        provider.SetHighRead(true);

        // Assert
        Assert.True(eventRaised, "MemoryMappingChanged should fire when HIGHREAD changes");
    }

    [Fact]
    public void MemoryMappingChanged_DoesNotFireWhenTextChanges()
    {
        // Arrange
        var provider = new SystemStatusProvider();
        var eventRaised = false;

        provider.MemoryMappingChanged += (sender, snapshot) => eventRaised = true;

        // Act
        provider.SetText(false);

        // Assert
        Assert.False(eventRaised, "MemoryMappingChanged should NOT fire for TEXT (display-only)");
    }

    [Fact]
    public void MemoryMappingChanged_DoesNotFireWhenMixedChanges()
    {
        // Arrange
        var provider = new SystemStatusProvider();
        var eventRaised = false;

        provider.MemoryMappingChanged += (sender, snapshot) => eventRaised = true;

        // Act
        provider.SetMixed(true);

        // Assert
        Assert.False(eventRaised, "MemoryMappingChanged should NOT fire for MIXED (display-only)");
    }

    [Fact]
    public void MemoryMappingChanged_DoesNotFireWhenAltCharChanges()
    {
        // Arrange
        var provider = new SystemStatusProvider();
        var eventRaised = false;

        provider.MemoryMappingChanged += (sender, snapshot) => eventRaised = true;

        // Act
        provider.SetAltChar(true);

        // Assert
        Assert.False(eventRaised, "MemoryMappingChanged should NOT fire for ALTCHAR (display-only)");
    }

    [Fact]
    public void MemoryMappingChanged_DoesNotFireWhen80VidChanges()
    {
        // Arrange
        var provider = new SystemStatusProvider();
        var eventRaised = false;

        provider.MemoryMappingChanged += (sender, snapshot) => eventRaised = true;

        // Act
        provider.Set80Vid(true);

        // Assert
        Assert.False(eventRaised, "MemoryMappingChanged should NOT fire for 80VID (display-only)");
    }

    [Fact]
    public void MemoryMappingChanged_DoesNotFireWhenAnnunciatorChanges()
    {
        // Arrange
        var provider = new SystemStatusProvider();
        var eventRaised = false;

        provider.MemoryMappingChanged += (sender, snapshot) => eventRaised = true;

        // Act
        provider.SetAn0(true);
        provider.SetAn1(true);
        provider.SetAn2(true);
        provider.SetAn3(true);

        // Assert
        Assert.False(eventRaised, "MemoryMappingChanged should NOT fire for annunciators");
    }

    [Fact]
    public void MemoryMappingChanged_DoesNotFireWhenPreWriteChanges()
    {
        // Arrange
        var provider = new SystemStatusProvider();
        var eventRaised = false;

        provider.MemoryMappingChanged += (sender, snapshot) => eventRaised = true;

        // Act
        provider.SetPreWrite(true);

        // Assert
        Assert.False(eventRaised, "MemoryMappingChanged should NOT fire for PREWRITE (intermediate state)");
    }

    [Fact]
    public void MemoryMappingChanged_FiresOnceForMultipleMemorySwitches()
    {
        // Arrange
        var provider = new SystemStatusProvider();
        var eventCount = 0;

        provider.MemoryMappingChanged += (sender, snapshot) => eventCount++;

        // Act - Change multiple memory-affecting switches
        provider.SetRamRd(true);
        provider.SetRamWrt(true);
        provider.SetAltZp(true);

        // Assert
        Assert.Equal(3, eventCount);
    }

    [Fact]
    public void MemoryMappingChanged_IncludesFullSnapshot()
    {
        // Arrange
        var provider = new SystemStatusProvider();
        provider.SetText(false); // Set a non-memory switch first
        provider.SetRamRd(true); // Set a memory switch
        
        SystemStatusSnapshot? capturedSnapshot = null;
        provider.MemoryMappingChanged += (sender, snapshot) => capturedSnapshot = snapshot;

        // Act
        provider.SetHighRead(true);

        // Assert
        Assert.NotNull(capturedSnapshot);
        Assert.True(capturedSnapshot.StateHighRead, "Should have the changed memory switch");
        Assert.True(capturedSnapshot.StateRamRd, "Should have previous memory switch");
        Assert.False(capturedSnapshot.StateTextMode, "Should have non-memory switch too");
    }

    [Fact]
    public void MemoryMappingChanged_BothEventsFireForMemorySwitch()
    {
        // Arrange
        var provider = new SystemStatusProvider();
        var changedFired = false;
        var memoryMappingChangedFired = false;

        provider.Changed += (sender, snapshot) => changedFired = true;
        provider.MemoryMappingChanged += (sender, snapshot) => memoryMappingChangedFired = true;

        // Act
        provider.SetRamRd(true);

        // Assert
        Assert.True(changedFired, "Changed event should fire for memory switches");
        Assert.True(memoryMappingChangedFired, "MemoryMappingChanged should also fire for memory switches");
    }

    [Fact]
    public void MemoryMappingChanged_OnlyChangedFiresForDisplaySwitch()
    {
        // Arrange
        var provider = new SystemStatusProvider();
        var changedFired = false;
        var memoryMappingChangedFired = false;

        provider.Changed += (sender, snapshot) => changedFired = true;
        provider.MemoryMappingChanged += (sender, snapshot) => memoryMappingChangedFired = true;

        // Act
        provider.SetText(false);

        // Assert
        Assert.True(changedFired, "Changed event should fire for display switches");
        Assert.False(memoryMappingChangedFired, "MemoryMappingChanged should NOT fire for display switches");
    }

    [Fact]
    public void MemoryMappingChanged_MixedMutate_OnlyFiresIfMemorySwitchChanges()
    {
        // Arrange
        var provider = new SystemStatusProvider();
        var eventCount = 0;

        provider.MemoryMappingChanged += (sender, snapshot) => eventCount++;

        // Act 1 - Change only display switches
        provider.Mutate(b =>
        {
            b.StateTextMode = false;
            b.StateMixed = true;
        });

        // Assert 1
        Assert.Equal(0, eventCount);

        // Act 2 - Change both display and memory switches
        provider.Mutate(b =>
        {
            b.StateHiRes = true;  // Display
            b.StateRamRd = true;  // Memory
        });

        // Assert 2
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void MemoryMappingChanged_IncludesCorrectSender()
    {
        // Arrange
        var provider = new SystemStatusProvider();
        object? capturedSender = null;

        provider.MemoryMappingChanged += (sender, snapshot) => capturedSender = sender;

        // Act
        provider.SetRamRd(true);

        // Assert
        Assert.Same(provider, capturedSender);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MemoryMappingChanged_FiresOnToggle(bool newValue)
    {
        // Arrange
        var provider = new SystemStatusProvider();
        provider.SetRamRd(!newValue); // Set opposite first
        var eventRaised = false;

        provider.MemoryMappingChanged += (sender, snapshot) => eventRaised = true;

        // Act
        provider.SetRamRd(newValue);

        // Assert
        Assert.True(eventRaised);
    }

    #endregion
}
