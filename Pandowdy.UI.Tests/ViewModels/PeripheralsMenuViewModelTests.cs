// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Moq;
using Pandowdy.EmuCore.Cards;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Messages;
using Pandowdy.EmuCore.Services;
using Pandowdy.UI.ViewModels;
using Xunit;

namespace Pandowdy.UI.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="PeripheralsMenuViewModel"/>.
/// </summary>
public class PeripheralsMenuViewModelTests
{
    #region Test Helpers

    /// <summary>
    /// Test fixture for setting up PeripheralsMenuViewModel dependencies.
    /// </summary>
    private class PeripheralsMenuViewModelFixture
    {
        public Mock<IEmulatorCoreInterface> MockEmulatorCore { get; }
        public CardResponseChannel CardResponseChannel { get; }
        public DiskStatusProvider DiskStatusProvider { get; }
        public PeripheralsMenuViewModel ViewModel { get; }

        public PeripheralsMenuViewModelFixture()
        {
            MockEmulatorCore = new Mock<IEmulatorCoreInterface>();
            CardResponseChannel = new CardResponseChannel();
            DiskStatusProvider = new DiskStatusProvider();

            // Register two drives (slot 6, drives 1-2)
            DiskStatusProvider.RegisterDrive(6, 1);
            DiskStatusProvider.RegisterDrive(6, 2);

            ViewModel = new PeripheralsMenuViewModel(
                MockEmulatorCore.Object,
                CardResponseChannel,
                DiskStatusProvider);
        }
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesViewModel()
    {
        // Arrange & Act
        var fixture = new PeripheralsMenuViewModelFixture();

        // Assert
        Assert.NotNull(fixture.ViewModel);
        Assert.NotNull(fixture.ViewModel.DiskMenuItems);
        Assert.Empty(fixture.ViewModel.DiskMenuItems); // No cards discovered yet
    }

    #endregion

    #region Card Discovery Tests

    [Fact]
    public async Task CardDiscovery_WithDiskIIController_AddsControllerToMenu()
    {
        // Arrange
        var fixture = new PeripheralsMenuViewModelFixture();

        // Act - Simulate card identity response
        fixture.CardResponseChannel.Emit(
            SlotNumber.Slot6,
            1001, // Disk II card ID
            new CardIdentityPayload("Disk II Controller"));

        // Give observable time to process
        await Task.Delay(100);

        // Assert
        Assert.Single(fixture.ViewModel.DiskMenuItems);
        var controllerItem = fixture.ViewModel.DiskMenuItems[0];
        Assert.Equal("Slot 6 — Disk II Controller", controllerItem.Header);
        Assert.Equal(SlotNumber.Slot6, controllerItem.Slot);
        Assert.Equal(2, controllerItem.Drives.Count); // Two drives
    }

    [Fact]
    public async Task CardDiscovery_WithNullCard_DoesNotAddToMenu()
    {
        // Arrange
        var fixture = new PeripheralsMenuViewModelFixture();

        // Act - Simulate NullCard response (CardId = 0)
        fixture.CardResponseChannel.Emit(
            SlotNumber.Slot5,
            0, // NullCard
            new CardIdentityPayload("Empty Slot"));

        await Task.Delay(100);

        // Assert
        Assert.Empty(fixture.ViewModel.DiskMenuItems); // Should filter out NullCard
    }

    [Fact]
    public async Task CardDiscovery_WithMultipleControllers_AddsBothToMenu()
    {
        // Arrange
        var fixture = new PeripheralsMenuViewModelFixture();

        // Register drives for slot 5
        fixture.DiskStatusProvider.RegisterDrive(5, 1);
        fixture.DiskStatusProvider.RegisterDrive(5, 2);

        // Act - Simulate two disk controllers
        fixture.CardResponseChannel.Emit(
            SlotNumber.Slot5,
            1001,
            new CardIdentityPayload("Disk II Controller"));

        fixture.CardResponseChannel.Emit(
            SlotNumber.Slot6,
            1001,
            new CardIdentityPayload("Disk II Controller"));

        await Task.Delay(100);

        // Assert
        Assert.Equal(2, fixture.ViewModel.DiskMenuItems.Count);
        Assert.Equal("Slot 5 — Disk II Controller", fixture.ViewModel.DiskMenuItems[0].Header);
        Assert.Equal("Slot 6 — Disk II Controller", fixture.ViewModel.DiskMenuItems[1].Header);
    }

    #endregion

    #region Drive Label Tests

    [Fact]
    public async Task DriveLabels_WhenNoDiskInserted_ShowEmpty()
    {
        // Arrange
        var fixture = new PeripheralsMenuViewModelFixture();

        // Act - Add controller
        fixture.CardResponseChannel.Emit(
            SlotNumber.Slot6,
            1001,
            new CardIdentityPayload("Disk II Controller"));

        await Task.Delay(100);

        // Assert
        var controller = fixture.ViewModel.DiskMenuItems[0];
        Assert.Equal("S6D1 - (empty)", controller.Drives[0].Header);
        Assert.Equal("S6D2 - (empty)", controller.Drives[1].Header);
    }

    [Fact]
    public async Task DriveLabels_WhenDiskInserted_ShowFilename()
    {
        // Arrange
        var fixture = new PeripheralsMenuViewModelFixture();

        // Add controller
        fixture.CardResponseChannel.Emit(
            SlotNumber.Slot6,
            1001,
            new CardIdentityPayload("Disk II Controller"));

        await Task.Delay(100);

        // Act - Insert disk
        fixture.DiskStatusProvider.MutateDrive(6, 1, builder =>
        {
            builder.DiskImagePath = "C:\\test\\game.woz";
            builder.DiskImageFilename = "game.woz";
        });

        await Task.Delay(100);

        // Assert
        var controller = fixture.ViewModel.DiskMenuItems[0];
        Assert.Equal("S6D1 - game.woz", controller.Drives[0].Header);
        Assert.Equal("S6D2 - (empty)", controller.Drives[1].Header); // Drive 2 still empty
    }

    [Fact]
    public async Task DriveLabels_WhenDiskEjected_ShowEmpty()
    {
        // Arrange
        var fixture = new PeripheralsMenuViewModelFixture();

        // Add controller and insert disk
        fixture.CardResponseChannel.Emit(
            SlotNumber.Slot6,
            1001,
            new CardIdentityPayload("Disk II Controller"));

        fixture.DiskStatusProvider.MutateDrive(6, 1, builder =>
        {
            builder.DiskImagePath = "C:\\test\\game.woz";
            builder.DiskImageFilename = "game.woz";
        });

        await Task.Delay(100);

        // Act - Eject disk
        fixture.DiskStatusProvider.MutateDrive(6, 1, builder =>
        {
            builder.DiskImagePath = "";
            builder.DiskImageFilename = "";
        });

        await Task.Delay(100);

        // Assert
        var controller = fixture.ViewModel.DiskMenuItems[0];
        Assert.Equal("S6D1 - (empty)", controller.Drives[0].Header);
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_DisposesSubscriptions()
    {
        // Arrange
        var fixture = new PeripheralsMenuViewModelFixture();

        // Act
        fixture.ViewModel.Dispose();

        // Assert - Should not throw
        // (Subscriptions are disposed, so further updates won't crash)
    }

    #endregion
}
