// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Machine;
using Pandowdy.EmuCore.Slots;

namespace Pandowdy.EmuCore.Tests;

/// <summary>
/// Tests for NullCard - empty slot placeholder.
/// </summary>
public class NullCardTests
{
    #region Test Helpers

    /// <summary>
    /// Mock ICardResponseEmitter for testing - ignores all emitted responses.
    /// </summary>
    private class MockCardResponseEmitter : ICardResponseEmitter
    {
        public void Emit(SlotNumber slot, int cardId, ICardResponsePayload payload) { }
    }

    private static readonly ICardResponseEmitter MockEmitter = new MockCardResponseEmitter();

    #endregion

    #region Constructor and Properties

    [Fact]
    public void Constructor_CreatesInstanceWithCorrectProperties()
    {
        // Act
        var card = new NullCard(MockEmitter);

        // Assert
        Assert.Equal(0, card.Id);
        Assert.Equal("Empty Slot", card.Name);
        Assert.Equal("No card", card.Description);
        Assert.Equal(SlotNumber.Unslotted, card.Slot);
    }

    #endregion

    #region Read Operations (All return null)

    [Fact]
    public void ReadIO_ReturnsNull()
    {
        // Arrange
        var card = new NullCard(MockEmitter);

        // Act
        var result = card.ReadIO(0x50);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData(0x00)]
    [InlineData(0x50)]
    [InlineData(0xFF)]
    public void ReadIO_WithVariousOffsets_AlwaysReturnsNull(byte offset)
    {
        // Arrange
        var card = new NullCard(MockEmitter);

        // Act
        var result = card.ReadIO(offset);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ReadRom_ReturnsNull()
    {
        // Arrange
        var card = new NullCard(MockEmitter);

        // Act
        var result = card.ReadRom(0x80);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData(0x00)]
    [InlineData(0x80)]
    [InlineData(0xFF)]
    public void ReadRom_WithVariousOffsets_AlwaysReturnsNull(byte offset)
    {
        // Arrange
        var card = new NullCard(MockEmitter);

        // Act
        var result = card.ReadRom(offset);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ReadExtendedRom_ReturnsNull()
    {
        // Arrange
        var card = new NullCard(MockEmitter);

        // Act
        var result = card.ReadExtendedRom(0x0400);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData(0x0000)]
    [InlineData(0x0400)]
    [InlineData(0x07FF)]
    public void ReadExtendedRom_WithVariousOffsets_AlwaysReturnsNull(ushort offset)
    {
        // Arrange
        var card = new NullCard(MockEmitter);

        // Act
        var result = card.ReadExtendedRom(offset);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Write Operations (All no-ops)

    [Fact]
    public void WriteIO_DoesNotThrowException()
    {
        // Arrange
        var card = new NullCard(MockEmitter);

        // Act & Assert (should not throw)
        card.WriteIO(0x50, 0xAA);
    }

    [Theory]
    [InlineData(0x00, 0x00)]
    [InlineData(0x50, 0xAA)]
    [InlineData(0xFF, 0xFF)]
    public void WriteIO_WithVariousValues_DoesNotThrowException(byte offset, byte value)
    {
        // Arrange
        var card = new NullCard(MockEmitter);

        // Act & Assert (should not throw)
        card.WriteIO(offset, value);
    }

    [Fact]
    public void WriteRom_DoesNotThrowException()
    {
        // Arrange
        var card = new NullCard(MockEmitter);

        // Act & Assert (should not throw)
        card.WriteRom(0x80, 0xBB);
    }

    [Theory]
    [InlineData(0x00, 0x00)]
    [InlineData(0x80, 0xBB)]
    [InlineData(0xFF, 0xFF)]
    public void WriteRom_WithVariousValues_DoesNotThrowException(byte offset, byte value)
    {
        // Arrange
        var card = new NullCard(MockEmitter);

        // Act & Assert (should not throw)
        card.WriteRom(offset, value);
    }

    [Fact]
    public void WriteExtendedRom_DoesNotThrowException()
    {
        // Arrange
        var card = new NullCard(MockEmitter);

        // Act & Assert (should not throw)
        card.WriteExtendedRom(0x0400, 0xCC);
    }

    [Theory]
    [InlineData(0x0000, 0x00)]
    [InlineData(0x0400, 0xCC)]
    [InlineData(0x07FF, 0xFF)]
    public void WriteExtendedRom_WithVariousValues_DoesNotThrowException(ushort offset, byte value)
    {
        // Arrange
        var card = new NullCard(MockEmitter);

        // Act & Assert (should not throw)
        card.WriteExtendedRom(offset, value);
    }

    #endregion

    #region Clone

    [Fact]
    public void Clone_CreatesNewInstance()
    {
        // Arrange
        var original = new NullCard(MockEmitter);

        // Act
        var clone = original.Clone();

        // Assert
        Assert.NotNull(clone);
        Assert.NotSame(original, clone); // Different instances
        Assert.IsType<NullCard>(clone);
    }

    [Fact]
    public void Clone_CreatesInstanceWithSameProperties()
    {
        // Arrange
        var original = new NullCard(MockEmitter);

        // Act
        var clone = original.Clone();

        // Assert
        Assert.Equal(original.Id, clone.Id);
        Assert.Equal(original.Name, clone.Name);
        Assert.Equal(original.Description, clone.Description);
    }

    #endregion

    #region Configuration (IConfigurable)

    [Fact]
    public void GetMetadata_ReturnsEmptyString()
    {
        // Arrange
        var card = new NullCard(MockEmitter);

        // Act
        var metadata = card.GetMetadata();

        // Assert
        Assert.NotNull(metadata);
        Assert.Empty(metadata);
    }

    [Fact]
    public void ApplyMetadata_AlwaysReturnsTrue()
    {
        // Arrange
        var card = new NullCard(MockEmitter);

        // Act
        var result = card.ApplyMetadata("some metadata");

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("valid metadata")]
    [InlineData("{ \"config\": \"value\" }")]
    [InlineData(null)]
    public void ApplyMetadata_WithVariousInputs_AlwaysReturnsTrue(string? metadata)
    {
        // Arrange
        var card = new NullCard(MockEmitter);

        // Act
        var result = card.ApplyMetadata(metadata);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region OnInstalled

    [Fact]
    public void OnInstalled_SetsSlotNumber()
    {
        // Arrange
        var card = new NullCard(MockEmitter);
        Assert.Equal(SlotNumber.Unslotted, card.Slot);

        // Act
        card.OnInstalled(SlotNumber.Slot3);

        // Assert
        Assert.Equal(SlotNumber.Slot3, card.Slot);
    }

    [Theory]
    [InlineData(SlotNumber.Slot1)]
    [InlineData(SlotNumber.Slot2)]
    [InlineData(SlotNumber.Slot3)]
    [InlineData(SlotNumber.Slot4)]
    [InlineData(SlotNumber.Slot5)]
    [InlineData(SlotNumber.Slot6)]
    [InlineData(SlotNumber.Slot7)]
    public void OnInstalled_WithAllSlots_SetsSlotNumberCorrectly(SlotNumber slot)
    {
        // Arrange
        var card = new NullCard(MockEmitter);

        // Act
        card.OnInstalled(slot);

        // Assert
        Assert.Equal(slot, card.Slot);
    }

    [Fact]
    public void OnInstalled_MultipleCallsWithDifferentSlots_UpdatesSlot()
    {
        // Arrange
        var card = new NullCard(MockEmitter);

        // Act & Assert
        card.OnInstalled(SlotNumber.Slot1);
        Assert.Equal(SlotNumber.Slot1, card.Slot);

        card.OnInstalled(SlotNumber.Slot6);
        Assert.Equal(SlotNumber.Slot6, card.Slot);

        card.OnInstalled(SlotNumber.Unslotted);
        Assert.Equal(SlotNumber.Unslotted, card.Slot);
    }

    #endregion

    #region Reset

    [Fact]
    public void Reset_DoesNotThrowException()
    {
        // Arrange
        var card = new NullCard(MockEmitter);

        // Act & Assert (should not throw)
        card.Reset();
    }

    [Fact]
    public void Reset_DoesNotChangeSlotNumber()
    {
        // Arrange
        var card = new NullCard(MockEmitter);
        card.OnInstalled(SlotNumber.Slot5);

        // Act
        card.Reset();

        // Assert
        Assert.Equal(SlotNumber.Slot5, card.Slot); // Slot should remain unchanged
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void Integration_FullLifecycle_InstallReadWriteReset()
    {
        // Arrange
        var card = new NullCard(MockEmitter);

        // Act - Install
        card.OnInstalled(SlotNumber.Slot2);

        // Act - Read operations (all should return null)
        var ioRead = card.ReadIO(0x50);
        var romRead = card.ReadRom(0x80);
        var extRomRead = card.ReadExtendedRom(0x400);

        // Act - Write operations (all should be no-ops)
        card.WriteIO(0x60, 0xAA);
        card.WriteRom(0x90, 0xBB);
        card.WriteExtendedRom(0x500, 0xCC);

        // Act - Reset
        card.Reset();

        // Assert
        Assert.Equal(SlotNumber.Slot2, card.Slot);
        Assert.Null(ioRead);
        Assert.Null(romRead);
        Assert.Null(extRomRead);
    }

    [Fact]
    public void Integration_CloneLifecycle_IndependentInstances()
    {
        // Arrange
        var original = new NullCard(MockEmitter);
        original.OnInstalled(SlotNumber.Slot3);

        // Act
        var clone = original.Clone();
        Assert.IsType<NullCard>(clone);
        var clonedCard = (NullCard)clone;
        clonedCard.OnInstalled(SlotNumber.Slot7);

        // Assert - Original and clone are independent
        Assert.Equal(SlotNumber.Slot3, original.Slot);
        Assert.Equal(SlotNumber.Slot7, clonedCard.Slot);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void EdgeCase_MultipleReadsAndWrites_NeverChangeState()
    {
        // Arrange
        var card = new NullCard(MockEmitter);
        card.OnInstalled(SlotNumber.Slot4);

        // Act - Perform many operations
        for (int i = 0; i < 100; i++)
        {
            card.WriteIO((byte)i, (byte)(i * 2));
            card.WriteRom((byte)i, (byte)(i * 3));
            card.WriteExtendedRom((ushort)i, (byte)(i * 4));

            var ioResult = card.ReadIO((byte)i);
            var romResult = card.ReadRom((byte)i);
            var extRomResult = card.ReadExtendedRom((ushort)i);

            // Assert - All reads return null, slot unchanged
            Assert.Null(ioResult);
            Assert.Null(romResult);
            Assert.Null(extRomResult);
        }

        // Assert - Slot number unchanged after many operations
        Assert.Equal(SlotNumber.Slot4, card.Slot);
    }

    #endregion
}
