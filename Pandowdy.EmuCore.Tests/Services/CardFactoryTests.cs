// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.DiskII;
using Pandowdy.EmuCore.Slots;
using Pandowdy.EmuCore.Machine;
using Pandowdy.EmuCore.Tests.Mocks;

namespace Pandowdy.EmuCore.Tests.Services;

/// <summary>
/// Tests for CardFactory - card prototype registration and cloning.
/// </summary>
public class CardFactoryTests
{
    #region Helper Classes

    /// <summary>
    /// Mock ICardResponseEmitter for testing.
    /// </summary>
    private class MockCardResponseEmitter : ICardResponseEmitter
    {
        public void Emit(SlotNumber slot, int cardId, ICardResponsePayload payload) { }
    }

    private static readonly ICardResponseEmitter MockEmitter = new MockCardResponseEmitter();
    private static readonly IDiskImageStore MockStore = new MockDiskImageStore();

    /// <summary>
    /// Mock card for testing with configurable properties.
    /// </summary>
    private class MockCard : ICard
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Description => $"Mock card {Id}";
        public SlotNumber Slot { get; private set; } = SlotNumber.Unslotted;

        public byte? ReadIO(byte offset) => null;
        public void WriteIO(byte offset, byte value) { }
        public byte? ReadRom(byte offset) => null;
        public void WriteRom(byte offset, byte value) { }
        public byte? ReadExtendedRom(ushort offset) => null;
        public void WriteExtendedRom(ushort offset, byte value) { }
        public ICard Clone() => new MockCard { Id = Id, Name = Name };
        public string GetMetadata() => string.Empty;
        public bool ApplyMetadata(string metadata) => true;
        public void OnInstalled(SlotNumber slot) { Slot = slot; }
        public void Reset() { }
        public void HandleMessage(ICardMessage message) { }
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullCards_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new CardFactory(null!, MockStore));
        Assert.Equal("cards", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithEmptyList_Succeeds()
    {
        // Arrange & Act
        var factory = new CardFactory([], MockStore);

        // Assert
        Assert.NotNull(factory);
    }

    [Fact]
    public void Constructor_WithValidCards_Succeeds()
    {
        // Arrange
        var cards = new ICard[]
        {
            new MockCard { Id = 0, Name = "Empty Slot" },
            new MockCard { Id = 1, Name = "Disk II" },
            new MockCard { Id = 2, Name = "Serial Card" }
        };

        // Act
        var factory = new CardFactory(cards, MockStore);

        // Assert
        Assert.NotNull(factory);
    }

    [Fact]
    public void Constructor_WithDuplicateIds_ThrowsInvalidOperationException()
    {
        // Arrange
        var cards = new ICard[]
        {
            new MockCard { Id = 1, Name = "Card A" },
            new MockCard { Id = 1, Name = "Card B" }
        };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => new CardFactory(cards, MockStore));
        Assert.Contains("Duplicate card IDs detected", ex.Message);
        Assert.Contains("Card ID 1", ex.Message);
    }

    [Fact]
    public void Constructor_WithDuplicateNames_ThrowsInvalidOperationException()
    {
        // Arrange
        var cards = new ICard[]
        {
            new MockCard { Id = 1, Name = "Disk II" },
            new MockCard { Id = 2, Name = "disk ii" } // Case-insensitive duplicate
        };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => new CardFactory(cards, MockStore));
        Assert.Contains("Duplicate card names detected", ex.Message);
        Assert.Contains("Disk II", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_WithMultipleDuplicateIds_IncludesAllInErrorMessage()
    {
        // Arrange
        var cards = new ICard[]
        {
            new MockCard { Id = 1, Name = "Card A" },
            new MockCard { Id = 1, Name = "Card B" },
            new MockCard { Id = 2, Name = "Card C" },
            new MockCard { Id = 2, Name = "Card D" }
        };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => new CardFactory(cards, MockStore));
        Assert.Contains("Card ID 1", ex.Message);
        Assert.Contains("Card ID 2", ex.Message);
    }

    #endregion

    #region GetCardWithId Tests

    [Fact]
    public void GetCardWithId_WithValidId_ReturnsClonedCard()
    {
        // Arrange
        var originalCard = new MockCard { Id = 5, Name = "Test Card" };
        var factory = new CardFactory([originalCard], MockStore);

        // Act
        var clonedCard = factory.GetCardWithId(5);

        // Assert
        Assert.NotNull(clonedCard);
        Assert.Equal(5, clonedCard.Id);
        Assert.Equal("Test Card", clonedCard.Name);
        Assert.NotSame(originalCard, clonedCard); // Should be a clone, not the original
    }

    [Fact]
    public void GetCardWithId_WithInvalidId_ReturnsNull()
    {
        // Arrange
        var factory = new CardFactory([new MockCard { Id = 1, Name = "Card 1" }], MockStore);

        // Act
        var card = factory.GetCardWithId(999);

        // Assert
        Assert.Null(card);
    }

    [Fact]
    public void GetCardWithId_MultipleCallsWithSameId_ReturnsIndependentClones()
    {
        // Arrange
        var factory = new CardFactory([new MockCard { Id = 3, Name = "Card 3" }], MockStore);

        // Act
        var card1 = factory.GetCardWithId(3);
        var card2 = factory.GetCardWithId(3);

        // Assert
        Assert.NotNull(card1);
        Assert.NotNull(card2);
        Assert.NotSame(card1, card2); // Should be independent clones
        Assert.Equal(card1.Id, card2.Id);
        Assert.Equal(card1.Name, card2.Name);
    }

    [Fact]
    public void GetCardWithId_WithNullCard_ReturnsClonedNullCard()
    {
        // Arrange
        var factory = new CardFactory([new NullCard(MockEmitter)], MockStore);

        // Act
        var card = factory.GetCardWithId(0);

        // Assert
        Assert.NotNull(card);
        Assert.Equal(0, card.Id);
        Assert.IsType<NullCard>(card);
    }

    #endregion

    #region GetCardWithName Tests

    [Fact]
    public void GetCardWithName_WithValidName_ReturnsClonedCard()
    {
        // Arrange
        var originalCard = new MockCard { Id = 10, Name = "SuperSerial Card" };
        var factory = new CardFactory([originalCard], MockStore);

        // Act
        var clonedCard = factory.GetCardWithName("SuperSerial Card");

        // Assert
        Assert.NotNull(clonedCard);
        Assert.Equal(10, clonedCard.Id);
        Assert.Equal("SuperSerial Card", clonedCard.Name);
        Assert.NotSame(originalCard, clonedCard);
    }

    [Fact]
    public void GetCardWithName_WithInvalidName_ReturnsNull()
    {
        // Arrange
        var factory = new CardFactory([new MockCard { Id = 1, Name = "Card 1" }], MockStore);

        // Act
        var card = factory.GetCardWithName("Nonexistent Card");

        // Assert
        Assert.Null(card);
    }

    [Fact]
    public void GetCardWithName_IsCaseSensitive()
    {
        // Arrange
        var factory = new CardFactory([new MockCard { Id = 1, Name = "Disk II" }], MockStore);

        // Act
        var exactMatch = factory.GetCardWithName("Disk II");
        var wrongCase = factory.GetCardWithName("disk ii");

        // Assert
        Assert.NotNull(exactMatch);
        Assert.Null(wrongCase); // Case-sensitive match, should be null
    }

    [Fact]
    public void GetCardWithName_MultipleCallsWithSameName_ReturnsIndependentClones()
    {
        // Arrange
        var factory = new CardFactory([new MockCard { Id = 7, Name = "Test Card" }], MockStore);

        // Act
        var card1 = factory.GetCardWithName("Test Card");
        var card2 = factory.GetCardWithName("Test Card");

        // Assert
        Assert.NotNull(card1);
        Assert.NotNull(card2);
        Assert.NotSame(card1, card2); // Should be independent clones
    }

    [Fact]
    public void GetCardWithName_WithNullOrEmptyName_ReturnsNull()
    {
        // Arrange
        var factory = new CardFactory([new MockCard { Id = 1, Name = "Card 1" }], MockStore);

        // Act
        var nullResult = factory.GetCardWithName(null!);
        var emptyResult = factory.GetCardWithName(string.Empty);

        // Assert
        Assert.Null(nullResult);
        Assert.Null(emptyResult);
    }

    #endregion

    #region GetNullCard Tests

    [Fact]
    public void GetNullCard_WithNullCardRegistered_ReturnsNullCard()
    {
        // Arrange
        var factory = new CardFactory([new NullCard(MockEmitter)], MockStore);

        // Act
        var card = factory.GetNullCard();

        // Assert
        Assert.NotNull(card);
        Assert.Equal(0, card.Id);
        Assert.IsType<NullCard>(card);
    }

    [Fact]
    public void GetNullCard_WithoutNullCardRegistered_ReturnsNull()
    {
        // Arrange
        var factory = new CardFactory([new MockCard { Id = 1, Name = "Card 1" }], MockStore);

        // Act
        var card = factory.GetNullCard();

        // Assert
        Assert.Null(card); // No card with ID 0 registered
    }

    [Fact]
    public void GetNullCard_MultipleCallsWithNullCardRegistered_ReturnsIndependentClones()
    {
        // Arrange
        var factory = new CardFactory([new NullCard(MockEmitter)], MockStore);

        // Act
        var card1 = factory.GetNullCard();
        var card2 = factory.GetNullCard();

        // Assert
        Assert.NotNull(card1);
        Assert.NotNull(card2);
        Assert.NotSame(card1, card2); // Should be independent clones
    }

    #endregion

    #region GetAllCardTypes Tests

    [Fact]
    public void GetAllCardTypes_WithMultipleCards_ReturnsSortedList()
    {
        // Arrange
        var cards = new ICard[]
        {
            new MockCard { Id = 3, Name = "Card C" },
            new MockCard { Id = 1, Name = "Card A" },
            new MockCard { Id = 2, Name = "Card B" }
        };
        var factory = new CardFactory(cards, MockStore);

        // Act
        var cardTypes = factory.GetAllCardTypes();

        // Assert
        Assert.NotNull(cardTypes);
        Assert.Equal(3, cardTypes.Count);
        // Should be sorted by ID
        Assert.Equal((1, "Card A"), cardTypes[0]);
        Assert.Equal((2, "Card B"), cardTypes[1]);
        Assert.Equal((3, "Card C"), cardTypes[2]);
    }

    [Fact]
    public void GetAllCardTypes_WithEmptyFactory_ReturnsEmptyList()
    {
        // Arrange
        var factory = new CardFactory([], MockStore);

        // Act
        var cardTypes = factory.GetAllCardTypes();

        // Assert
        Assert.NotNull(cardTypes);
        Assert.Empty(cardTypes);
    }

    [Fact]
    public void GetAllCardTypes_WithSingleCard_ReturnsSingleItemList()
    {
        // Arrange
        var factory = new CardFactory([new MockCard { Id = 5, Name = "Solo Card" }], MockStore);

        // Act
        var cardTypes = factory.GetAllCardTypes();

        // Assert
        Assert.NotNull(cardTypes);
        Assert.Single(cardTypes);
        Assert.Equal((5, "Solo Card"), cardTypes[0]);
    }

    [Fact]
    public void GetAllCardTypes_IncludesNullCard_WhenRegistered()
    {
        // Arrange
        var cards = new ICard[]
        {
            new NullCard(MockEmitter),
            new MockCard { Id = 1, Name = "Disk II" }
        };
        var factory = new CardFactory(cards, MockStore);

        // Act
        var cardTypes = factory.GetAllCardTypes();

        // Assert
        Assert.Equal(2, cardTypes.Count);
        Assert.Equal((0, "Empty Slot"), cardTypes[0]); // NullCard has ID 0
        Assert.Equal((1, "Disk II"), cardTypes[1]);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void Integration_FullLifecycle_RegisterAndRetrieveMultipleCards()
    {
        // Arrange
        var cards = new ICard[]
        {
            new NullCard(MockEmitter),
            new MockCard { Id = 1, Name = "Disk II 13-Sector" },
            new MockCard { Id = 2, Name = "Disk II 16-Sector" },
            new MockCard { Id = 3, Name = "80-Column Card" }
        };
        var factory = new CardFactory(cards, MockStore);

        // Act - Get by ID
        var card1 = factory.GetCardWithId(1);
        var card2 = factory.GetCardWithId(2);
        var card3 = factory.GetCardWithId(3);
        var nullCard = factory.GetNullCard();

        // Act - Get by name
        var cardByName1 = factory.GetCardWithName("Disk II 13-Sector");
        var cardByName3 = factory.GetCardWithName("80-Column Card");

        // Act - Get all types
        var allTypes = factory.GetAllCardTypes();

        // Assert - Retrieved cards
        Assert.NotNull(card1);
        Assert.NotNull(card2);
        Assert.NotNull(card3);
        Assert.NotNull(nullCard);
        Assert.Equal(1, card1.Id);
        Assert.Equal(2, card2.Id);
        Assert.Equal(3, card3.Id);
        Assert.Equal(0, nullCard.Id);

        // Assert - By name retrieval
        Assert.NotNull(cardByName1);
        Assert.NotNull(cardByName3);
        Assert.Equal(1, cardByName1.Id);
        Assert.Equal(3, cardByName3.Id);

        // Assert - All types
        Assert.Equal(4, allTypes.Count);
        Assert.Equal((0, "Empty Slot"), allTypes[0]);
        Assert.Equal((1, "Disk II 13-Sector"), allTypes[1]);
        Assert.Equal((2, "Disk II 16-Sector"), allTypes[2]);
        Assert.Equal((3, "80-Column Card"), allTypes[3]);

        // Assert - Clones are independent
        Assert.NotSame(card1, cardByName1);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void EdgeCase_GetCardWithId_WithZeroId_ReturnsNullCardIfRegistered()
    {
        // Arrange
        var factory = new CardFactory([new NullCard(MockEmitter)], MockStore);

        // Act
        var card = factory.GetCardWithId(0);

        // Assert
        Assert.NotNull(card);
        Assert.IsType<NullCard>(card);
    }

    [Fact]
    public void EdgeCase_GetCardWithId_WithNegativeId_ReturnsNull()
    {
        // Arrange
        var factory = new CardFactory([new MockCard { Id = 1, Name = "Card 1" }], MockStore);

        // Act
        var card = factory.GetCardWithId(-1);

        // Assert
        Assert.Null(card);
    }

    [Fact]
    public void EdgeCase_Constructor_WithLargeNumberOfCards_Succeeds()
    {
        // Arrange
        var cards = Enumerable.Range(0, 100)
            .Select(i => (ICard)new MockCard { Id = i, Name = $"Card {i}" })
            .ToArray();

        // Act
        var factory = new CardFactory(cards, MockStore);
        var allTypes = factory.GetAllCardTypes();

        // Assert
        Assert.NotNull(factory);
        Assert.Equal(100, allTypes.Count);
        Assert.Equal((0, "Card 0"), allTypes[0]);
        Assert.Equal((99, "Card 99"), allTypes[99]);
    }

    #endregion
}
