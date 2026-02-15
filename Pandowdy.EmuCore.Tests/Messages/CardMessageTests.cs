// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Exceptions;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Messages;

namespace Pandowdy.EmuCore.Tests.Messages;

/// <summary>
/// Tests for card message routing and handling.
/// Validates message delivery, error handling, and basic message infrastructure.
/// </summary>
/// <remarks>
/// These tests validate Phase 1 message infrastructure. Full VA2M integration
/// tests (targeted/broadcast delivery) will be added when VA2MBuilder supports
/// custom ISlots injection in Phase 2.
/// </remarks>
public class CardMessageTests
{
    #region Test Helpers

    private class TrackingCard : ICard
    {
        private readonly List<ICardMessage> _receivedMessages = [];

        public SlotNumber Slot { get; private set; } = SlotNumber.Unslotted;
        public string Name => "Tracking Card";
        public string Description => "Card that tracks received messages";
        public int Id => 100;
        public IReadOnlyList<ICardMessage> ReceivedMessages => _receivedMessages;

        public void OnInstalled(SlotNumber slot)
        {
            Slot = slot;
        }

        public void HandleMessage(ICardMessage message)
        {
            _receivedMessages.Add(message);
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
        public ICard Clone() => new TrackingCard();
    }

    private class ThrowingCard : ICard
    {
        public SlotNumber Slot { get; private set; } = SlotNumber.Unslotted;
        public string Name => "Throwing Card";
        public string Description => "Card that throws exceptions";
        public int Id => 101;

        public void OnInstalled(SlotNumber slot)
        {
            Slot = slot;
        }

        public void HandleMessage(ICardMessage message)
        {
            throw new CardMessageException("Card cannot handle message");
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
        public ICard Clone() => new ThrowingCard();
    }

    #endregion

    #region Message Delivery Tests

    [Fact]
    public void Card_ReceivesMessage_ViaHandleMessage()
    {
        // Arrange
        var card = new TrackingCard();
        card.OnInstalled(SlotNumber.Slot6);
        var message = new IdentifyCardMessage();

        // Act
        card.HandleMessage(message);

        // Assert
        Assert.Single(card.ReceivedMessages);
        Assert.Same(message, card.ReceivedMessages[0]);
    }

    [Fact]
    public void Card_ReceivesMultipleMessages_TracksAll()
    {
        // Arrange
        var card = new TrackingCard();
        var message1 = new IdentifyCardMessage();
        var message2 = new EnumerateDevicesMessage();
        var message3 = new RefreshStatusMessage();

        // Act
        card.HandleMessage(message1);
        card.HandleMessage(message2);
        card.HandleMessage(message3);

        // Assert
        Assert.Equal(3, card.ReceivedMessages.Count);
        Assert.IsType<IdentifyCardMessage>(card.ReceivedMessages[0]);
        Assert.IsType<EnumerateDevicesMessage>(card.ReceivedMessages[1]);
        Assert.IsType<RefreshStatusMessage>(card.ReceivedMessages[2]);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void Card_ThrowsCardMessageException_PropagatesException()
    {
        // Arrange
        var card = new ThrowingCard();
        var message = new IdentifyCardMessage();

        // Act & Assert
        var exception = Assert.Throws<CardMessageException>(() => card.HandleMessage(message));
        Assert.Equal("Card cannot handle message", exception.Message);
    }

    [Fact]
    public void CardMessageException_ConstructorWithMessage_CreatesException()
    {
        // Act
        var exception = new CardMessageException("Test error");

        // Assert
        Assert.Equal("Test error", exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void CardMessageException_ConstructorWithInnerException_CreatesException()
    {
        // Arrange
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var exception = new CardMessageException("Outer error", innerException);

        // Assert
        Assert.Equal("Outer error", exception.Message);
        Assert.Same(innerException, exception.InnerException);
    }

    #endregion

    #region Message Type Tests

#pragma warning disable xUnit2032 // IsAssignableFrom is the correct assertion for interface implementation checks
    [Fact]
    public void IdentifyCardMessage_ImplementsICardMessage()
    {
        var message = new IdentifyCardMessage();
        Assert.IsAssignableFrom<ICardMessage>(message);
    }

    [Fact]
    public void EnumerateDevicesMessage_ImplementsICardMessage()
    {
        var message = new EnumerateDevicesMessage();
        Assert.IsAssignableFrom<ICardMessage>(message);
    }

    [Fact]
    public void RefreshStatusMessage_ImplementsICardMessage()
    {
        var message = new RefreshStatusMessage();
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

    [Fact]
    public void EnumerateDevicesMessage_IsRecord_SupportsValueEquality()
    {
        var message1 = new EnumerateDevicesMessage();
        var message2 = new EnumerateDevicesMessage();

        Assert.Equal(message1, message2);
    }

    [Fact]
    public void RefreshStatusMessage_IsRecord_SupportsValueEquality()
    {
        var message1 = new RefreshStatusMessage();
        var message2 = new RefreshStatusMessage();

        Assert.Equal(message1, message2);
    }

    #endregion
}

