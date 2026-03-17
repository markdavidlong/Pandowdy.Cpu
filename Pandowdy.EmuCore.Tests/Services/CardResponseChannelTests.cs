// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Reactive.Linq;
using Pandowdy.EmuCore.Slots;
using Pandowdy.EmuCore.Machine;

namespace Pandowdy.EmuCore.Tests.Services;

/// <summary>
/// Tests for CardResponseChannel - validates response stream publishing and subscription behavior.
/// </summary>
public class CardResponseChannelTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_CreatesInstance()
    {
        var channel = new CardResponseChannel();

        Assert.NotNull(channel);
        Assert.NotNull(channel.Responses);
    }

    #endregion

    #region Emit Tests

    [Fact]
    public void Emit_PublishesToResponseStream()
    {
        // Arrange
        var channel = new CardResponseChannel();
        CardResponse? receivedResponse = null;
        using var subscription = channel.Responses.Subscribe(response => receivedResponse = response);
        var payload = new CardIdentityPayload("Test Card");

        // Act
        channel.Emit(SlotNumber.Slot6, 10, payload);

        // Assert
        Assert.NotNull(receivedResponse);
        Assert.Equal(SlotNumber.Slot6, receivedResponse.Slot);
        Assert.Equal(10, receivedResponse.CardId);
        Assert.Same(payload, receivedResponse.Payload);
    }

    [Fact]
    public void Emit_MultipleResponses_AllPublished()
    {
        // Arrange
        var channel = new CardResponseChannel();
        var receivedResponses = new List<CardResponse>();
        using var subscription = channel.Responses.Subscribe(receivedResponses.Add);

        // Act
        channel.Emit(SlotNumber.Slot1, 1, new CardIdentityPayload("Card 1"));
        channel.Emit(SlotNumber.Slot2, 2, new CardIdentityPayload("Card 2"));
        channel.Emit(SlotNumber.Slot3, 3, new CardIdentityPayload("Card 3"));

        // Assert
        Assert.Equal(3, receivedResponses.Count);
        Assert.Equal(SlotNumber.Slot1, receivedResponses[0].Slot);
        Assert.Equal(SlotNumber.Slot2, receivedResponses[1].Slot);
        Assert.Equal(SlotNumber.Slot3, receivedResponses[2].Slot);
    }

    #endregion

    #region Subscription Tests

    [Fact]
    public void Responses_SupportsMultipleSubscribers()
    {
        // Arrange
        var channel = new CardResponseChannel();
        var subscriber1Responses = new List<CardResponse>();
        var subscriber2Responses = new List<CardResponse>();
        using var subscription1 = channel.Responses.Subscribe(subscriber1Responses.Add);
        using var subscription2 = channel.Responses.Subscribe(subscriber2Responses.Add);
        var payload = new CardIdentityPayload("Test Card");

        // Act
        channel.Emit(SlotNumber.Slot6, 10, payload);

        // Assert
        Assert.Single(subscriber1Responses);
        Assert.Single(subscriber2Responses);
        Assert.Equal(SlotNumber.Slot6, subscriber1Responses[0].Slot);
        Assert.Equal(SlotNumber.Slot6, subscriber2Responses[0].Slot);
    }

    [Fact]
    public void Responses_SubscriberUnsubscribes_NoLongerReceivesResponses()
    {
        // Arrange
        var channel = new CardResponseChannel();
        var receivedResponses = new List<CardResponse>();
        var subscription = channel.Responses.Subscribe(receivedResponses.Add);

        // Act - Emit, unsubscribe, emit again
        channel.Emit(SlotNumber.Slot1, 1, new CardIdentityPayload("Card 1"));
        subscription.Dispose();
        channel.Emit(SlotNumber.Slot2, 2, new CardIdentityPayload("Card 2"));

        // Assert - Only first response received
        Assert.Single(receivedResponses);
        Assert.Equal(SlotNumber.Slot1, receivedResponses[0].Slot);
    }

    [Fact]
    public void Responses_NewSubscriberAfterEmit_DoesNotReceivePreviousResponses()
    {
        // Arrange
        var channel = new CardResponseChannel();
        channel.Emit(SlotNumber.Slot1, 1, new CardIdentityPayload("Card 1"));

        // Act - Subscribe after emit
        var receivedResponses = new List<CardResponse>();
        using var subscription = channel.Responses.Subscribe(receivedResponses.Add);
        channel.Emit(SlotNumber.Slot2, 2, new CardIdentityPayload("Card 2"));

        // Assert - Only second response received
        Assert.Single(receivedResponses);
        Assert.Equal(SlotNumber.Slot2, receivedResponses[0].Slot);
    }

    #endregion

    #region Payload Type Tests

    [Fact]
    public void Emit_WithCardIdentityPayload_PublishesCorrectly()
    {
        // Arrange
        var channel = new CardResponseChannel();
        CardResponse? receivedResponse = null;
        using var subscription = channel.Responses.Subscribe(response => receivedResponse = response);
        var payload = new CardIdentityPayload("Disk II Controller");

        // Act
        channel.Emit(SlotNumber.Slot6, 10, payload);

        // Assert
        Assert.NotNull(receivedResponse);
        Assert.IsType<CardIdentityPayload>(receivedResponse.Payload);
        var identityPayload = (CardIdentityPayload)receivedResponse.Payload;
        Assert.Equal("Disk II Controller", identityPayload.CardName);
    }

    [Fact]
    public void Emit_WithDeviceListPayload_PublishesCorrectly()
    {
        // Arrange
        var channel = new CardResponseChannel();
        CardResponse? receivedResponse = null;
        using var subscription = channel.Responses.Subscribe(response => receivedResponse = response);
        var devices = new List<PeripheralType> { PeripheralType.Floppy525, PeripheralType.Floppy525 };
        var payload = new DeviceListPayload(devices);

        // Act
        channel.Emit(SlotNumber.Slot6, 10, payload);

        // Assert
        Assert.NotNull(receivedResponse);
        Assert.IsType<DeviceListPayload>(receivedResponse.Payload);
        var deviceListPayload = (DeviceListPayload)receivedResponse.Payload;
        Assert.Equal(2, deviceListPayload.Devices.Count);
        Assert.Equal(PeripheralType.Floppy525, deviceListPayload.Devices[0]);
    }

    #endregion
}
