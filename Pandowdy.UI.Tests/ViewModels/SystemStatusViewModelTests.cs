// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.IO;
using Pandowdy.EmuCore.Machine;
using Pandowdy.UI.ViewModels;

namespace Pandowdy.UI.Tests.ViewModels;

/// <summary>
/// Tests for SystemStatusViewModel - displays Apple II system status in UI.
/// Tests reactive binding to system status changes and property updates.
/// </summary>
public class SystemStatusViewModelTests
{
    #region Test Helpers

    /// <summary>
    /// Helper fixture to create SystemStatusViewModel with dependencies.
    /// </summary>
    private class SystemStatusViewModelFixture
    {
        public SystemStatusProvider StatusProvider { get; }
        public SystemStatusViewModel ViewModel { get; }

        public SystemStatusViewModelFixture()
        {
            StatusProvider = new SystemStatusProvider();
            ViewModel = new SystemStatusViewModel(StatusProvider);
        }
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidStatusProvider_CreatesViewModel()
    {
        // Arrange
        var statusProvider = new SystemStatusProvider();

        // Act
        var viewModel = new SystemStatusViewModel(statusProvider);

        // Assert
        Assert.NotNull(viewModel);
    }

    [Fact]
    public void Constructor_InitializesPropertiesFromCurrentSnapshot()
    {
        // Arrange - Create provider and set some known non-default values
        var statusProvider = new SystemStatusProvider();
        statusProvider.Mutate(s =>
        {
            s.State80Store = true;
            s.StateRamRd = true;
            s.StateHiRes = true;
        });

        // Act
        var viewModel = new SystemStatusViewModel(statusProvider);

        // Assert - ViewModel should reflect whatever the provider's current state is,
        // regardless of what the default values are
        Assert.Equal(statusProvider.State80Store, viewModel.State80Store);
        Assert.Equal(statusProvider.StateRamRd, viewModel.StateRamRd);
        Assert.Equal(statusProvider.StateHiRes, viewModel.StateHiRes);
        Assert.Equal(statusProvider.StateTextMode, viewModel.StateTextMode);
        Assert.Equal(statusProvider.StateIntCxRom, viewModel.StateIntCxRom);
    }

    [Fact]
    public void Constructor_SubscribesToStatusStream()
    {
        // Arrange
        var statusProvider = new SystemStatusProvider();

        // Act
        var viewModel = new SystemStatusViewModel(statusProvider);
        
        // Mutate status after construction
        statusProvider.Mutate(s => s.State80Store = true);

        // Assert - Should update automatically
        Assert.True(viewModel.State80Store);
    }

    #endregion

    #region Property Update Tests

    [Fact]
    public void TextMode_PropertyChangedRaised_WhenSystemStatusChanges()
    {
        // Arrange
        var fixture = new SystemStatusViewModelFixture();
        bool propertyChanged = false;
        
        fixture.ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SystemStatusViewModel.StateTextMode))
            {
                propertyChanged = true;
            }
        };

        // Act - Toggle text mode off (it starts as true)
        fixture.StatusProvider.Mutate(s => s.StateTextMode = false);

        // Assert
        Assert.True(propertyChanged);
        Assert.False(fixture.ViewModel.StateTextMode);
    }

    [Fact]
    public void HiResMode_PropertyChangedRaised_WhenSystemStatusChanges()
    {
        // Arrange
        var fixture = new SystemStatusViewModelFixture();
        bool propertyChanged = false;
        
        fixture.ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SystemStatusViewModel.StateHiRes))
            {
                propertyChanged = true;
            }
        };

        // Act
        fixture.StatusProvider.Mutate(s => s.StateHiRes = true);

        // Assert
        Assert.True(propertyChanged);
        Assert.True(fixture.ViewModel.StateHiRes);
    }

    [Fact]
    public void MixedMode_PropertyChangedRaised_WhenSystemStatusChanges()
    {
        // Arrange
        var fixture = new SystemStatusViewModelFixture();
        bool propertyChanged = false;
        
        fixture.ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SystemStatusViewModel.StateMixed))
            {
                propertyChanged = true;
            }
        };

        // Act
        fixture.StatusProvider.Mutate(s => s.StateMixed = true);

        // Assert
        Assert.True(propertyChanged);
        Assert.True(fixture.ViewModel.StateMixed);
    }

    [Fact]
    public void Page2_PropertyChangedRaised_WhenSystemStatusChanges()
    {
        // Arrange
        var fixture = new SystemStatusViewModelFixture();
        bool propertyChanged = false;
        
        fixture.ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SystemStatusViewModel.StatePage2))
            {
                propertyChanged = true;
            }
        };

        // Act
        fixture.StatusProvider.Mutate(s => s.StatePage2 = true);

        // Assert
        Assert.True(propertyChanged);
        Assert.True(fixture.ViewModel.StatePage2);
    }

    [Fact]
    public void Show80Col_PropertyChangedRaised_WhenSystemStatusChanges()
    {
        // Arrange
        var fixture = new SystemStatusViewModelFixture();
        bool propertyChanged = false;
        
        fixture.ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SystemStatusViewModel.StateShow80Col))
            {
                propertyChanged = true;
            }
        };

        // Act
        fixture.StatusProvider.Mutate(s => s.StateShow80Col = true);

        // Assert
        Assert.True(propertyChanged);
        Assert.True(fixture.ViewModel.StateShow80Col);
    }

    [Fact]
    public void MemorySwitches_PropertyChangedRaised_WhenSystemStatusChanges()
    {
        // Arrange
        var fixture = new SystemStatusViewModelFixture();
        var changedProperties = new List<string>();
        
        fixture.ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
            {
                changedProperties.Add(e.PropertyName);
            }
        };

        // Act - Change multiple memory switches
        fixture.StatusProvider.Mutate(s =>
        {
            s.State80Store = true;
            s.StateRamRd = true;
            s.StateRamWrt = true;
            s.StateAltZp = true;
        });

        // Assert
        Assert.Contains(nameof(SystemStatusViewModel.State80Store), changedProperties);
        Assert.Contains(nameof(SystemStatusViewModel.StateRamRd), changedProperties);
        Assert.Contains(nameof(SystemStatusViewModel.StateRamWrt), changedProperties);
        Assert.Contains(nameof(SystemStatusViewModel.StateAltZp), changedProperties);
    }

    #endregion

    #region Integration Scenarios

    [Fact]
    public void Scenario_SystemEntersTextMode_ViewModelUpdates()
    {
        // Arrange
        var fixture = new SystemStatusViewModelFixture();

        // Act - Simulate entering text mode (like hitting Ctrl+C on boot)
        fixture.StatusProvider.Mutate(s => s.StateTextMode = true);

        // Assert
        Assert.True(fixture.ViewModel.StateTextMode);
        Assert.False(fixture.ViewModel.StateHiRes);
        Assert.False(fixture.ViewModel.StateMixed);
    }

    [Fact]
    public void Scenario_SystemEntersHiResGraphicsMode_ViewModelUpdates()
    {
        // Arrange
        var fixture = new SystemStatusViewModelFixture();

        // Act - Simulate entering hi-res graphics mode
        fixture.StatusProvider.Mutate(s =>
        {
            s.StateTextMode = false;
            s.StateHiRes = true;
        });

        // Assert
        Assert.False(fixture.ViewModel.StateTextMode);
        Assert.True(fixture.ViewModel.StateHiRes);
        Assert.False(fixture.ViewModel.StateMixed);
    }

    [Fact]
    public void Scenario_MixedModeGraphics_ViewModelUpdates()
    {
        // Arrange
        var fixture = new SystemStatusViewModelFixture();

        // Act - Simulate mixed mode (graphics with text at bottom)
        fixture.StatusProvider.Mutate(s =>
        {
            s.StateTextMode = false;
            s.StateHiRes = true;
            s.StateMixed = true;
        });

        // Assert
        Assert.False(fixture.ViewModel.StateTextMode);
        Assert.True(fixture.ViewModel.StateHiRes);
        Assert.True(fixture.ViewModel.StateMixed);
    }

    [Fact]
    public void Scenario_LanguageCardBankSwitch_ViewModelReflectsChange()
    {
        // Arrange
        var fixture = new SystemStatusViewModelFixture();

        // Act - Simulate language card bank switch (Bank1 -> Bank2)
        fixture.StatusProvider.Mutate(s =>
        {
            s.StateUseBank1 = false;
            s.StateHighRead = true;
            s.StateHighWrite = false;
        });

        // Assert
        Assert.False(fixture.ViewModel.StateBank1); // Switched to Bank 2
        Assert.True(fixture.ViewModel.StateHighRead);
        Assert.False(fixture.ViewModel.StateHighWrite);
    }

    [Fact]
    public void Scenario_MultipleStatusUpdates_AllPropertiesTracked()
    {
        // Arrange
        var fixture = new SystemStatusViewModelFixture();
        var propertyChanges = new List<string>();
        
        fixture.ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
            {
                propertyChanges.Add(e.PropertyName);
            }
        };

        // Act - Simulate a sequence of system status changes
        // 1. Enable HiRes (text mode starts as true, so toggle it off)
        fixture.StatusProvider.Mutate(s =>
        {
            s.StateTextMode = false;
            s.StateHiRes = true;
        });

        // 2. Switch to Page 2
        fixture.StatusProvider.Mutate(s => s.StatePage2 = true);

        // Assert
        Assert.Contains(nameof(SystemStatusViewModel.StateTextMode), propertyChanges);
        Assert.Contains(nameof(SystemStatusViewModel.StateHiRes), propertyChanges);
        Assert.Contains(nameof(SystemStatusViewModel.StatePage2), propertyChanges);
        Assert.False(fixture.ViewModel.StateTextMode);
        Assert.True(fixture.ViewModel.StateHiRes);
        Assert.True(fixture.ViewModel.StatePage2);
    }

    #endregion
}
