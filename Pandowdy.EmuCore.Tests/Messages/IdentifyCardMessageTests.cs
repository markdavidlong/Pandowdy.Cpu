// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Reactive.Linq;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Messages;
using Pandowdy.EmuCore.Services;

namespace Pandowdy.EmuCore.Tests.Messages;

/// <summary>
/// Tests for IdentifyCardMessage - validates card identification broadcast and response handling.
/// </summary>
public class IdentifyCardMessageTests
{
    #region Test Helpers

    private class MockCard(int id, string name, ICardResponseEmitter emitter) : ICard
    {
        private readonly ICardResponseEmitter _emitter = emitter;

        public SlotNumber Slot { get; private set; } = SlotNumber.Unslotted;
        public string Name { get; } = name;
        public string Description => $"Mock card: {Name}";
        public int Id { get; } = id;

        public void OnInstalled(SlotNumber slot)
        {
            Slot = slot;
        }

        public void HandleMessage(ICardMessage message)
        {
            if (message is IdentifyCardMessage)
            {
                _emitter.Emit(Slot, Id, new CardIdentityPayload(Name));
            }
        }

        public byte? ReadIO(byte offset) => null;
        public void WriteIO(byte offset, byte value) { }
        public byte? ReadRom(byte offset) => null;
        public void WriteRom(byte offset, byte value) { }
        public byte? ReadExtendedRom(ushort offset) => null;
        public void WriteExtendedRom(ushort offset, byte value) { }
        public string GetMetadata() => string.Empty;
        public bool ApplyMetadata(string metadata) => true;
        public void Reset() { }
        public ICard Clone() => new MockCard(Id, Name, _emitter);
    }

    #endregion

    #region Message Handling Tests

    [Fact]
    public void NullCard_HandlesIdentifyCardMessage_EmitsIdentityPayload()
    {
        // Arrange
        var channel = new CardResponseChannel();
        var card = new NullCard(channel);
        card.OnInstalled(SlotNumber.Slot1);
        CardResponse? response = null;
        using var subscription = channel.Responses.Subscribe(r => response = r);

        // Act
        card.HandleMessage(new IdentifyCardMessage());

        // Assert
        Assert.NotNull(response);
        Assert.Equal(SlotNumber.Slot1, response.Slot);
        Assert.Equal(0, response.CardId);
        Assert.IsType<CardIdentityPayload>(response.Payload);
        var payload = (CardIdentityPayload)response.Payload;
        Assert.Equal("Empty Slot", payload.CardName);
    }

    [Fact]
    public void MockCard_HandlesIdentifyCardMessage_EmitsCorrectIdentity()
    {
        // Arrange
        var channel = new CardResponseChannel();
        var card = new MockCard(10, "Disk II Controller", channel);
        card.OnInstalled(SlotNumber.Slot6);
        CardResponse? response = null;
        using var subscription = channel.Responses.Subscribe(r => response = r);

        // Act
        card.HandleMessage(new IdentifyCardMessage());

        // Assert
        Assert.NotNull(response);
        Assert.Equal(SlotNumber.Slot6, response.Slot);
        Assert.Equal(10, response.CardId);
        Assert.IsType<CardIdentityPayload>(response.Payload);
        var payload = (CardIdentityPayload)response.Payload;
        Assert.Equal("Disk II Controller", payload.CardName);
    }

    [Fact]
    public void MultipleCards_HandleIdentifyCardMessage_EachEmitsResponse()
    {
        // Arrange
        var channel = new CardResponseChannel();
        var card1 = new MockCard(1, "Card 1", channel);
        var card2 = new MockCard(2, "Card 2", channel);
        var card3 = new NullCard(channel);
        card1.OnInstalled(SlotNumber.Slot1);
        card2.OnInstalled(SlotNumber.Slot2);
        card3.OnInstalled(SlotNumber.Slot3);

        var responses = new List<CardResponse>();
        using var subscription = channel.Responses.Subscribe(responses.Add);
        var message = new IdentifyCardMessage();

        // Act
        card1.HandleMessage(message);
        card2.HandleMessage(message);
        card3.HandleMessage(message);

        // Assert
        Assert.Equal(3, responses.Count);
        Assert.Equal(SlotNumber.Slot1, responses[0].Slot);
        Assert.Equal("Card 1", ((CardIdentityPayload)responses[0].Payload).CardName);
        Assert.Equal(SlotNumber.Slot2, responses[1].Slot);
        Assert.Equal("Card 2", ((CardIdentityPayload)responses[1].Payload).CardName);
        Assert.Equal(SlotNumber.Slot3, responses[2].Slot);
        Assert.Equal("Empty Slot", ((CardIdentityPayload)responses[2].Payload).CardName);
    }

    #endregion

    #region Message Properties Tests

    #pragma warning disable xUnit2032 // IsAssignableFrom is the correct assertion for interface implementation checks
        [Fact]
        public void IdentifyCardMessage_ImplementsICardMessage()
        {
            var message = new IdentifyCardMessage();

            Assert.IsAssignableFrom<ICardMessage>(message);
        }
    #pragma warning restore xUnit2032

        [Fact]
        public void IdentifyCardMessage_IsRecord_SupportsValueEquality()
    {
        var message1 = new IdentifyCardMessage();
        var message2 = new IdentifyCardMessage();

        Assert.Equal(message1, message2);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void Integration_CardIdentificationWorkflow_EndToEnd()
    {
        // Arrange - Create channel and cards
        var channel = new CardResponseChannel();
        var nullCard = new NullCard(channel);
        var diskCard = new MockCard(10, "Disk II Controller", channel);
        nullCard.OnInstalled(SlotNumber.Slot1);
        diskCard.OnInstalled(SlotNumber.Slot6);

        // Collect all responses
        var responses = new List<CardResponse>();
        using var subscription = channel.Responses.Subscribe(responses.Add);

        // Act - Simulate broadcast (as VA2M would do)
        var message = new IdentifyCardMessage();
        nullCard.HandleMessage(message);  // Slot 1
        diskCard.HandleMessage(message);  // Slot 6

        // Assert - Verify both cards responded
        Assert.Equal(2, responses.Count);
        
        // Verify Slot 1 (NullCard)
        var slot1Response = responses.First(r => r.Slot == SlotNumber.Slot1);
        Assert.Equal(0, slot1Response.CardId);
        Assert.IsType<CardIdentityPayload>(slot1Response.Payload);
        Assert.Equal("Empty Slot", ((CardIdentityPayload)slot1Response.Payload).CardName);

        // Verify Slot 6 (Disk II)
        var slot6Response = responses.First(r => r.Slot == SlotNumber.Slot6);
        Assert.Equal(10, slot6Response.CardId);
        Assert.IsType<CardIdentityPayload>(slot6Response.Payload);
        Assert.Equal("Disk II Controller", ((CardIdentityPayload)slot6Response.Payload).CardName);
    }

    #endregion
}
