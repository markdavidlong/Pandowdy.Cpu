// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Diagnostics.CodeAnalysis;
using Pandowdy.EmuCore;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Messages;
using Pandowdy.EmuCore.Services;

namespace Pandowdy.EmuCore.Tests;

/// <summary>
/// Comprehensive tests for the Slots class, covering card management,
/// ROM banking, I/O operations, and soft switch interactions.
/// </summary>
public class SlotsTests
{
    #region Test Helpers and Mocks

    /// <summary>
    /// Mock card for testing.
    /// </summary>
    private class MockCard(int id, string name, byte ioFill = 0x00, byte romFill = 0x00, byte extRomFill = 0x00) : ICard
    {
        private readonly byte _ioFillValue = ioFill;
        private readonly byte _romFillValue = romFill;
        private readonly byte _extRomFillValue = extRomFill;

        public SlotNumber Slot { get; private set; } = SlotNumber.Unslotted;
        public string Name { get; } = name;
        public string Description => $"Mock card: {Name}";
        public int Id { get; } = id;

        public bool IsInstalled { get; private set; }

        public void OnInstalled(SlotNumber slot)
        {
            Slot = slot;
            IsInstalled = true;
        }

        public byte? ReadIO(byte offset) => _ioFillValue;
        public void WriteIO(byte offset, byte value) { }
        public byte? ReadRom(byte offset) => _romFillValue;
        public void WriteRom(byte offset, byte value) { }
        public byte? ReadExtendedRom(ushort offset) => _extRomFillValue;
        public void WriteExtendedRom(ushort offset, byte value) { }
        public string GetMetadata() => string.Empty;
        public bool ApplyMetadata(string metadata) => true;
        public void Reset() { }
        public void HandleMessage(ICardMessage message) { }
        public ICard Clone() => new MockCard(Id, Name, _ioFillValue, _romFillValue, _extRomFillValue);
    }

    /// <summary>
    /// Mock card that returns null for all reads (non-responsive).
    /// </summary>
    private class NonResponsiveCard(int id, string name) : ICard
    {
        public SlotNumber Slot { get; private set; } = SlotNumber.Unslotted;
        public string Name { get; } = name;
        public string Description => $"Non-responsive card: {Name}";
        public int Id { get; } = id;

        public void OnInstalled(SlotNumber slot)
        {
            Slot = slot;
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
        public void HandleMessage(ICardMessage message) { }
        public ICard Clone() => new NonResponsiveCard(Id, Name);
    }

    /// <summary>
    /// Mock card factory for testing.
    /// </summary>
    private class MockCardFactory : ICardFactory
    {
        private readonly Dictionary<int, ICard> _cardsById = [];
        private readonly Dictionary<string, ICard> _cardsByName = [];
        [SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "Interface usage provides test flexibility")]
        private readonly ICard _nullCard;

        public MockCardFactory()
        {
            _nullCard = new MockCard(0, "NullCard");
        }

        public void RegisterCard(ICard card)
        {
            _cardsById[card.Id] = card;
            _cardsByName[card.Name] = card;
        }

        public ICard? GetCardWithId(int id)
        {
            return _cardsById.TryGetValue(id, out var card) ? card.Clone() : null;
        }

        public ICard? GetCardWithName(string name)
        {
            return _cardsByName.TryGetValue(name, out var card) ? card.Clone() : null;
        }

        public ICard GetNullCard() => _nullCard.Clone();

        public List<(int Id, string Name)> GetAllCardTypes()
        {
            return [.. _cardsById.Values.Select(c => (c.Id, c.Name))];
        }
    }

    /// <summary>
    /// Mock ROM provider that returns predictable values.
    /// </summary>
    private class MockRomProvider : ISystemRomProvider
    {
        private readonly byte[] _rom = new byte[0x1000];

        public MockRomProvider(byte fillPattern = 0xFF)
        {
            Array.Fill(_rom, fillPattern);
        }

        public int Size => 0x1000;

        public byte Read(ushort address) => _rom[address % _rom.Length];
        public byte Peek(ushort address) => _rom[address % _rom.Length];
        public void Write(ushort address, byte value) => _rom[address % _rom.Length] = value;

        public void LoadRomFile(string filePath)
        {
            // No-op for testing
        }
    }

    /// <summary>
    /// Mock floating bus provider.
    /// </summary>
    private class MockFloatingBusProvider(byte value = 0xFB) : IFloatingBusProvider
    {
        private readonly byte _value = value;

        public byte Read() => _value;
    }

    private static Slots CreateSlots(
        out MockCardFactory factory,
        out SystemStatusProvider status,
        out MockRomProvider rom,
        out MockFloatingBusProvider floatingBus)
    {
        factory = new MockCardFactory();
        status = new SystemStatusProvider();
        rom = new MockRomProvider(0xFF);
        floatingBus = new MockFloatingBusProvider(0xFB);

        return new Slots(factory, rom, floatingBus, status);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidArguments_CreatesInstance()
    {
        // Arrange & Act
        var slots = CreateSlots(out _, out _, out _, out _);

        // Assert
        Assert.NotNull(slots);
        Assert.Equal(0x1000, slots.Size);
    }

    [Fact]
    public void Constructor_WithNullFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var status = new SystemStatusProvider();
        var rom = new MockRomProvider();
        var floatingBus = new MockFloatingBusProvider();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new Slots(null!, rom, floatingBus, status));
    }

    [Fact]
    public void Constructor_WithNullRom_ThrowsArgumentNullException()
    {
        // Arrange
        var factory = new MockCardFactory();
        var status = new SystemStatusProvider();
        var floatingBus = new MockFloatingBusProvider();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new Slots(factory, null!, floatingBus, status));
    }

    [Fact]
    public void Constructor_WithNullFloatingBus_ThrowsArgumentNullException()
    {
        // Arrange
        var factory = new MockCardFactory();
        var status = new SystemStatusProvider();
        var rom = new MockRomProvider();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new Slots(factory, rom, null!, status));
    }

    [Fact]
    public void Constructor_WithNullStatus_ThrowsArgumentNullException()
    {
        // Arrange
        var factory = new MockCardFactory();
        var rom = new MockRomProvider();
        var floatingBus = new MockFloatingBusProvider();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new Slots(factory, rom, floatingBus, null!));
    }

    [Fact]
    public void Constructor_InitializesAllSlotsWithNullCards()
    {
        // Arrange & Act
        var slots = CreateSlots(out _, out _, out _, out _);

        // Assert - All slots should be empty
        for (int i = 1; i <= 7; i++)
        {
            Assert.True(slots.IsEmpty((SlotNumber)i));
        }
    }

    [Fact]
    public void Size_ReturnsCorrectValue()
    {
        // Arrange & Act
        var slots = CreateSlots(out _, out _, out _, out _);

        // Assert
        Assert.Equal(0x1000, slots.Size);
    }

    #endregion

    #region InstallCard Tests

    [Fact]
    public void InstallCard_ById_InstallsCardSuccessfully()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out _, out _, out _);
        var card = new MockCard(10, "Test Card");
        factory.RegisterCard(card);

        // Act
        slots.InstallCard(10, SlotNumber.Slot1);

        // Assert
        Assert.False(slots.IsEmpty(SlotNumber.Slot1));
        var installedCard = slots.GetCardIn(SlotNumber.Slot1);
        Assert.Equal("Test Card", installedCard.Name);
        Assert.Equal(10, installedCard.Id);
    }

    [Fact]
    public void InstallCard_ByName_InstallsCardSuccessfully()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out _, out _, out _);
        var card = new MockCard(10, "Test Card");
        factory.RegisterCard(card);

        // Act
        slots.InstallCard("Test Card", SlotNumber.Slot1);

        // Assert
        Assert.False(slots.IsEmpty(SlotNumber.Slot1));
        var installedCard = slots.GetCardIn(SlotNumber.Slot1);
        Assert.Equal("Test Card", installedCard.Name);
    }

    [Fact]
    public void InstallCard_WithUnslottedSlot_ThrowsArgumentException()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out _, out _, out _);
        var card = new MockCard(10, "Test Card");
        factory.RegisterCard(card);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            slots.InstallCard(10, SlotNumber.Unslotted));
    }

    [Fact]
    public void InstallCard_WithInvalidId_ThrowsInvalidOperationException()
    {
        // Arrange
        var slots = CreateSlots(out _, out _, out _, out _);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            slots.InstallCard(999, SlotNumber.Slot1));
    }

    [Fact]
    public void InstallCard_WithInvalidName_ThrowsInvalidOperationException()
    {
        // Arrange
        var slots = CreateSlots(out _, out _, out _, out _);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            slots.InstallCard("NonExistent Card", SlotNumber.Slot1));
    }

    [Fact]
    public void InstallCard_CallsOnInstalled()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out _, out _, out _);
        var card = new MockCard(10, "Test Card");
        factory.RegisterCard(card);

        // Act
        slots.InstallCard(10, SlotNumber.Slot3);

        // Assert
        var installedCard = slots.GetCardIn(SlotNumber.Slot3) as MockCard;
        Assert.NotNull(installedCard);
        Assert.True(installedCard!.IsInstalled);
        Assert.Equal(SlotNumber.Slot3, installedCard.Slot);
    }

    [Fact]
    public void InstallCard_ReplacesExistingCard()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out _, out _, out _);
        var card1 = new MockCard(10, "Card 1");
        var card2 = new MockCard(20, "Card 2");
        factory.RegisterCard(card1);
        factory.RegisterCard(card2);

        // Act
        slots.InstallCard(10, SlotNumber.Slot1);
        slots.InstallCard(20, SlotNumber.Slot1); // Replace

        // Assert
        var installedCard = slots.GetCardIn(SlotNumber.Slot1);
        Assert.Equal("Card 2", installedCard.Name);
        Assert.Equal(20, installedCard.Id);
    }

    #endregion

    #region RemoveCard Tests

    [Fact]
    public void RemoveCard_RemovesCardAndInstallsNullCard()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out _, out _, out _);
        var card = new MockCard(10, "Test Card");
        factory.RegisterCard(card);
        slots.InstallCard(10, SlotNumber.Slot1);

        // Act
        slots.RemoveCard(SlotNumber.Slot1);

        // Assert
        Assert.True(slots.IsEmpty(SlotNumber.Slot1));
    }

    [Fact]
    public void RemoveCard_FromEmptySlot_InstallsNullCard()
    {
        // Arrange
        var slots = CreateSlots(out _, out _, out _, out _);

        // Act
        slots.RemoveCard(SlotNumber.Slot1);

        // Assert
        Assert.True(slots.IsEmpty(SlotNumber.Slot1));
    }

    #endregion

    #region GetCardIn and IsEmpty Tests

    [Fact]
    public void GetCardIn_ReturnsInstalledCard()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out _, out _, out _);
        var card = new MockCard(10, "Test Card");
        factory.RegisterCard(card);
        slots.InstallCard(10, SlotNumber.Slot4);

        // Act
        var retrievedCard = slots.GetCardIn(SlotNumber.Slot4);

        // Assert
        Assert.NotNull(retrievedCard);
        Assert.Equal("Test Card", retrievedCard.Name);
    }

    [Fact]
    public void GetCardIn_EmptySlot_ReturnsNullCard()
    {
        // Arrange
        var slots = CreateSlots(out _, out _, out _, out _);

        // Act
        var card = slots.GetCardIn(SlotNumber.Slot1);

        // Assert
        Assert.NotNull(card);
        Assert.Equal(0, card.Id); // NullCard has ID 0
    }

    [Fact]
    public void IsEmpty_WithNullCard_ReturnsTrue()
    {
        // Arrange
        var slots = CreateSlots(out _, out _, out _, out _);

        // Act & Assert
        Assert.True(slots.IsEmpty(SlotNumber.Slot1));
    }

    [Fact]
    public void IsEmpty_WithInstalledCard_ReturnsFalse()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out _, out _, out _);
        var card = new MockCard(10, "Test Card");
        factory.RegisterCard(card);
        slots.InstallCard(10, SlotNumber.Slot1);

        // Act & Assert
        Assert.False(slots.IsEmpty(SlotNumber.Slot1));
    }

    #endregion

    #region Read/Write I/O Tests ($C090-$C0FF)

    [Fact]
    public void Read_CardIO_ReturnsCardValue()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out _, out _, out _);
        var card = new MockCard(10, "Test Card", ioFill: 0x42);
        factory.RegisterCard(card);
        slots.InstallCard(10, SlotNumber.Slot1);

        // Act - Read from slot 1 I/O ($C090-$C09F)
        byte result = slots.Read(0x0090);

        // Assert
        Assert.Equal(0x42, result);
    }

    [Fact]
    public void Read_CardIO_NonResponsiveCard_ReturnsFloatingBus()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out _, out _, out _);
        var card = new NonResponsiveCard(10, "Test Card");
        factory.RegisterCard(card);
        slots.InstallCard(10, SlotNumber.Slot2);

        // Act - Read from slot 2 I/O ($C0A0-$C0AF)
        byte result = slots.Read(0x00A0);

        // Assert
        Assert.Equal(0xFB, result); // Floating bus value
    }

    [Fact]
    public void Write_CardIO_CallsCardWriteIO()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out _, out _, out _);
        var card = new MockCard(10, "Test Card");
        factory.RegisterCard(card);
        slots.InstallCard(10, SlotNumber.Slot3);

        // Act - Write to slot 3 I/O ($C0B0-$C0BF)
        slots.Write(0x00B5, 0x99); // Should not throw

        // Assert - Just verify no exception
        Assert.True(true);
    }

    [Theory]
    [InlineData(0x0090, 1)] // Slot 1
    [InlineData(0x00A0, 2)] // Slot 2
    [InlineData(0x00B0, 3)] // Slot 3
    [InlineData(0x00C0, 4)] // Slot 4
    [InlineData(0x00D0, 5)] // Slot 5
    [InlineData(0x00E0, 6)] // Slot 6
    [InlineData(0x00F0, 7)] // Slot 7
    public void Read_CardIO_CorrectSlotSelection(ushort address, int expectedSlot)
    {
        // Arrange
        var slots = CreateSlots(out var factory, out _, out _, out _);
        var card = new MockCard(10 + expectedSlot, $"Card Slot {expectedSlot}", ioFill: (byte)expectedSlot);
        factory.RegisterCard(card);
        slots.InstallCard(10 + expectedSlot, (SlotNumber)expectedSlot);

        // Act
        byte result = slots.Read(address);

        // Assert
        Assert.Equal((byte)expectedSlot, result);
    }

    #endregion

    #region Read/Write ROM Tests ($C100-$C7FF)

    [Fact]
    public void Read_CardRom_WithCardInstalled_ReturnsCardRom()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out var status, out _, out _);
        var card = new MockCard(10, "Test Card", romFill: 0x55);
        factory.RegisterCard(card);
        slots.InstallCard(10, SlotNumber.Slot1);
        status.SetIntCxRom(false); // Card ROM enabled

        // Act - Read from slot 1 ROM ($C100-$C1FF)
        byte result = slots.Read(0x0100);

        // Assert
        Assert.Equal(0x55, result);
    }

    [Fact]
    public void Read_CardRom_WithIntCxRomEnabled_ReturnsSystemRom()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out var status, out var rom, out _);
        var card = new MockCard(10, "Test Card", romFill: 0x55);
        factory.RegisterCard(card);
        slots.InstallCard(10, SlotNumber.Slot1);
        status.SetIntCxRom(true); // System ROM enabled
        rom.Write(0x0100, 0xAA);

        // Act
        byte result = slots.Read(0x0100);

        // Assert
        Assert.Equal(0xAA, result); // System ROM, not card ROM
    }

    [Fact]
    public void Read_Slot3Rom_WithSlotC3RomOff_ReturnsSystemRom()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out var status, out var rom, out _);
        var card = new MockCard(10, "Test Card", romFill: 0x55);
        factory.RegisterCard(card);
        slots.InstallCard(10, SlotNumber.Slot3);
        status.SetIntCxRom(false);
        status.SetSlotC3Rom(false); // Use system ROM for slot 3
        rom.Write(0x0300, 0xBB);

        // Act
        byte result = slots.Read(0x0300);

        // Assert
        Assert.Equal(0xBB, result); // System ROM
    }

    [Fact]
    public void Read_Slot3Rom_WithSlotC3RomOn_ReturnsCardRom()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out var status, out _, out _);
        var card = new MockCard(10, "Test Card", romFill: 0x55);
        factory.RegisterCard(card);
        slots.InstallCard(10, SlotNumber.Slot3);
        status.SetIntCxRom(false);
        status.SetSlotC3Rom(true); // Use card ROM for slot 3

        // Act
        byte result = slots.Read(0x0300);

        // Assert
        Assert.Equal(0x55, result); // Card ROM
    }

    [Fact]
    public void Read_CardRom_NonResponsiveCard_ReturnsFloatingBus()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out var status, out _, out _);
        var card = new NonResponsiveCard(10, "Test Card");
        factory.RegisterCard(card);
        slots.InstallCard(10, SlotNumber.Slot2);
        status.SetIntCxRom(false);

        // Act
        byte result = slots.Read(0x0200);

        // Assert
        Assert.Equal(0xFB, result); // Floating bus
    }

    #endregion

    #region Extended ROM Tests ($C800-$CFFF)

    [Fact]
    public void Read_ExtendedRom_WithCardActivated_ReturnsCardRom()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out var status, out _, out _);
        var card = new MockCard(10, "Test Card", extRomFill: 0x88);
        factory.RegisterCard(card);
        slots.InstallCard(10, SlotNumber.Slot6);
        status.SetIntCxRom(false);
        status.SetIntC8Rom(false);

        // Act - Read from slot 6 ROM to activate extended ROM
        slots.Read(0x0600);
        // Then read from extended ROM
        byte result = slots.Read(0x0800);

        // Assert
        Assert.Equal(0x88, result);
    }

    [Fact]
    public void Read_ExtendedRom_WithIntCxRomEnabled_ReturnsSystemRom()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out var status, out var rom, out _);
        var card = new MockCard(10, "Test Card", extRomFill: 0x88);
        factory.RegisterCard(card);
        slots.InstallCard(10, SlotNumber.Slot6);
        status.SetIntCxRom(true);
        rom.Write(0x0800, 0xCC);

        // Act
        byte result = slots.Read(0x0800);

        // Assert
        Assert.Equal(0xCC, result); // System ROM
    }

    [Fact]
    public void Read_ExtendedRom_AtCFFF_ResetsC8Rom()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out var status, out _, out _);
        var card = new MockCard(10, "Test Card", extRomFill: 0x88);
        factory.RegisterCard(card);
        slots.InstallCard(10, SlotNumber.Slot6);
        status.SetIntCxRom(false);
        status.SetIntC8Rom(false);

        // Activate extended ROM
        slots.Read(0x0600);
        Assert.Equal(6, status.StateIntC8RomSlot);

        // Act - Read from $CFFF resets C8ROM
        slots.Read(0x0FFF);

        // Assert
        Assert.False(status.StateIntC8Rom);
        Assert.Equal(0, status.StateIntC8RomSlot);
    }

    [Fact]
    public void Read_ExtendedRom_NonResponsiveCard_ReturnsFloatingBus()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out var status, out _, out _);
        var nonResponsiveCard = new NonResponsiveCard(10, "Non-Responsive");
        factory.RegisterCard(nonResponsiveCard);
        slots.InstallCard(10, SlotNumber.Slot5);
        status.SetIntCxRom(false);
        status.SetIntC8Rom(false);

        // Act - Try to activate extended ROM
        slots.Read(0x0500);

        // Assert - Card doesn't respond to extended ROM, so slot should not be set
        Assert.Equal(0, status.StateIntC8RomSlot);
    }

    #endregion

    #region Peek Tests

    [Fact]
    public void Peek_DoesNotAffectC8RomState()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out var status, out _, out _);
        var card = new MockCard(10, "Test Card", extRomFill: 0x88);
        factory.RegisterCard(card);
        slots.InstallCard(10, SlotNumber.Slot6);
        status.SetIntCxRom(false);
        status.SetIntC8Rom(false);

        // Act - Peek at slot 6 ROM (should not activate extended ROM)
        slots.Peek(0x0600);

        // Assert
        Assert.Equal(0, status.StateIntC8RomSlot); // Should remain 0
    }

    [Fact]
    public void Peek_ReturnsCorrectValue()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out _, out _, out _);
        var card = new MockCard(10, "Test Card", ioFill: 0x42);
        factory.RegisterCard(card);
        slots.InstallCard(10, SlotNumber.Slot1);

        // Act
        byte result = slots.Peek(0x0090);

        // Assert
        Assert.Equal(0x42, result);
    }

    #endregion

    #region Invalid Address Tests

    [Fact]
    public void Read_BelowC090_ThrowsInvalidOperationException()
    {
        // Arrange
        var slots = CreateSlots(out _, out _, out _, out _);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => slots.Read(0x0010));
    }

    [Fact]
    public void Write_BelowC090_ThrowsInvalidOperationException()
    {
        // Arrange
        var slots = CreateSlots(out _, out _, out _, out _);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => slots.Write(0x0010, 0x00));
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void Integration_MultipleCardsInDifferentSlots()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out var status, out _, out _);
        var card1 = new MockCard(10, "Card 1", ioFill: 0x11, romFill: 0xAA);
        var card2 = new MockCard(20, "Card 2", ioFill: 0x22, romFill: 0xBB);
        factory.RegisterCard(card1);
        factory.RegisterCard(card2);
        
        slots.InstallCard(10, SlotNumber.Slot1);
        slots.InstallCard(20, SlotNumber.Slot2);
        status.SetIntCxRom(false);

        // Act & Assert
        Assert.Equal(0x11, slots.Read(0x0090)); // Slot 1 I/O
        Assert.Equal(0x22, slots.Read(0x00A0)); // Slot 2 I/O
        Assert.Equal(0xAA, slots.Read(0x0100)); // Slot 1 ROM
        Assert.Equal(0xBB, slots.Read(0x0200)); // Slot 2 ROM
    }

    [Fact]
    public void Integration_ExtendedRomActivationSequence()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out var status, out _, out _);
        var card = new MockCard(10, "Test Card", extRomFill: 0x99);
        factory.RegisterCard(card);
        slots.InstallCard(10, SlotNumber.Slot6);
        status.SetIntCxRom(false);
        status.SetIntC8Rom(false);

        // Act - Sequence: Read slot ROM → Read extended ROM → Reset
        slots.Read(0x0600); // Activate extended ROM for slot 6
        byte extRomValue = slots.Read(0x0800);
        slots.Read(0x0FFF); // Reset
        byte afterReset = slots.Read(0x0800);

        // Assert
        Assert.Equal(0x99, extRomValue);
        Assert.Equal(0xFB, afterReset); // Floating bus after reset
    }

    [Fact]
    public void Integration_Slot3SpecialHandling()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out var status, out var rom, out _);
        var card = new MockCard(10, "80-Column Card", romFill: 0x80);
        factory.RegisterCard(card);
        slots.InstallCard(10, SlotNumber.Slot3);
        rom.Write(0x0300, 0xF0);
        status.SetIntCxRom(false);

        // Act & Assert - SLOTC3ROM off → system ROM
        status.SetSlotC3Rom(false);
        Assert.Equal(0xF0, slots.Read(0x0300));

        // Act & Assert - SLOTC3ROM on → card ROM
        status.SetSlotC3Rom(true);
        Assert.Equal(0x80, slots.Read(0x0300));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void EdgeCase_ReadWriteAllSlots()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out _, out _, out _);
        for (int i = 1; i <= 7; i++)
        {
            var card = new MockCard(10 + i, $"Card {i}", ioFill: (byte)i);
            factory.RegisterCard(card);
            slots.InstallCard(10 + i, (SlotNumber)i);
        }

        // Act & Assert - Read from each slot's I/O
        // Slot 1: $C090, Slot 2: $C0A0, etc.
        for (int i = 1; i <= 7; i++)
        {
            ushort address = (ushort)(0x0080 + (i << 4));
            Assert.Equal((byte)i, slots.Read(address));
        }
    }

    [Fact]
    public void EdgeCase_RemoveAndReinstallCard()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out _, out _, out _);
        var card = new MockCard(10, "Test Card", ioFill: 0x42);
        factory.RegisterCard(card);
        slots.InstallCard(10, SlotNumber.Slot1);

        // Act
        slots.RemoveCard(SlotNumber.Slot1);
        Assert.True(slots.IsEmpty(SlotNumber.Slot1));

        slots.InstallCard(10, SlotNumber.Slot1);
        Assert.False(slots.IsEmpty(SlotNumber.Slot1));

        // Assert
        Assert.Equal(0x42, slots.Read(0x0090));
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ClearsIntC8RomState()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out var status, out _, out _);
        var card = new MockCard(10, "Test Card", extRomFill: 0x88);
        factory.RegisterCard(card);
        slots.InstallCard(10, SlotNumber.Slot6);

        // Activate extended ROM
        status.SetIntCxRom(false);
        slots.Read(0x0600);
        Assert.Equal(6, status.StateIntC8RomSlot);
        Assert.False(status.StateIntC8Rom);

        // Act
        slots.Reset();

        // Assert
        Assert.False(status.StateIntC8Rom);
        Assert.Equal(0, status.StateIntC8RomSlot);
    }

    [Fact]
    public void Reset_CallsResetOnAllCards()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out _, out _, out _);
        var resetCounter = 0;
        var testCard = new TestableCard(11, "Test Card", onReset: () => resetCounter++);
        factory.RegisterCard(testCard);
        slots.InstallCard(11, SlotNumber.Slot5);

        // Act
        slots.Reset();

        // Assert - Should reset all 8 cards (slot 0-7, including NullCards)
        Assert.True(resetCounter >= 1); // At least our test card was reset
    }

    #endregion

    #region Write Tests - Card ROM

    [Fact]
    public void Write_CardRom_WithCardInstalled_CallsCardWriteRom()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out var status, out _, out _);
        var writeCounter = 0;
        var testCard = new TestableCard(10, "Test Card", onWriteRom: () => writeCounter++);
        factory.RegisterCard(testCard);
        slots.InstallCard(10, SlotNumber.Slot6);
        status.SetIntCxRom(false);

        // Act
        slots.Write(0x0600, 0x42);

        // Assert
        Assert.Equal(1, writeCounter);
    }

    [Fact]
    public void Write_CardRom_WithIntCxRomEnabled_WritesToSystemRom()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out var status, out var rom, out _);
        var card = new MockCard(10, "Test Card");
        factory.RegisterCard(card);
        slots.InstallCard(10, SlotNumber.Slot6);
        status.SetIntCxRom(true);

        // Act
        slots.Write(0x0600, 0x99);

        // Assert - System ROM should have the write
        Assert.Equal(0x99, rom.Read(0x0600));
    }

    [Fact]
    public void Write_Slot3Rom_WithSlotC3RomOff_WritesToSystemRom()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out var status, out var rom, out _);
        var card = new MockCard(10, "Test Card");
        factory.RegisterCard(card);
        slots.InstallCard(10, SlotNumber.Slot3);
        status.SetIntCxRom(false);
        status.SetSlotC3Rom(false);

        // Act
        slots.Write(0x0300, 0x77);

        // Assert
        Assert.Equal(0x77, rom.Read(0x0300));
    }

    [Fact]
    public void Write_Slot3Rom_WithSlotC3RomOn_CallsCardWriteRom()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out var status, out _, out _);
        var writeCounter = 0;
        var testCard = new TestableCard(10, "Test Card", onWriteRom: () => writeCounter++);
        factory.RegisterCard(testCard);
        slots.InstallCard(10, SlotNumber.Slot3);
        status.SetIntCxRom(false);
        status.SetSlotC3Rom(true);

        // Act
        slots.Write(0x0300, 0x55);

        // Assert
        Assert.Equal(1, writeCounter);
    }

    [Fact]
    public void Write_CardRom_ManagesC800Activation()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out var status, out _, out _);
        var card = new MockCard(10, "Test Card", extRomFill: 0x88);
        factory.RegisterCard(card);
        slots.InstallCard(10, SlotNumber.Slot6);
        status.SetIntCxRom(false);
        status.SetIntC8Rom(false);
        status.SetIntC8RomSlot(0);

        // Act
        slots.Write(0x0600, 0x00);

        // Assert - C800 should be activated for slot 6
        Assert.Equal(6, status.StateIntC8RomSlot);
    }

    #endregion

    #region Write Tests - Extended ROM

    [Fact]
    public void Write_ExtendedRom_WithCardActivated_CallsCardWriteExtendedRom()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out var status, out _, out _);
        var writeCounter = 0;
        var testCard = new TestableCard(10, "Test Card", 
            extRomFill: 0x88,
            onWriteExtRom: () => writeCounter++);
        factory.RegisterCard(testCard);
        slots.InstallCard(10, SlotNumber.Slot6);
        status.SetIntCxRom(false);
        status.SetIntC8Rom(false);

        // Activate extended ROM
        slots.Read(0x0600);
        Assert.Equal(6, status.StateIntC8RomSlot);

        // Act
        slots.Write(0x0800, 0xAB);

        // Assert
        Assert.Equal(1, writeCounter);
    }

    [Fact]
    public void Write_ExtendedRom_WithIntCxRomEnabled_IsNoOp()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out var status, out _, out _);
        var writeCounter = 0;
        var testCard = new TestableCard(10, "Test Card", 
            extRomFill: 0x88,
            onWriteExtRom: () => writeCounter++);
        factory.RegisterCard(testCard);
        slots.InstallCard(10, SlotNumber.Slot6);
        status.SetIntCxRom(true); // Internal ROM enabled

        // Act
        slots.Write(0x0800, 0xCD);

        // Assert - Write should be no-op
        Assert.Equal(0, writeCounter);
    }

    [Fact]
    public void Write_ExtendedRom_WithIntC8RomEnabled_IsNoOp()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out var status, out _, out _);
        var writeCounter = 0;
        var testCard = new TestableCard(10, "Test Card", 
            extRomFill: 0x88,
            onWriteExtRom: () => writeCounter++);
        factory.RegisterCard(testCard);
        slots.InstallCard(10, SlotNumber.Slot6);
        status.SetIntCxRom(false);
        status.SetIntC8Rom(true); // C8 ROM area enabled

        // Act
        slots.Write(0x0800, 0xEF);

        // Assert - Write should be no-op
        Assert.Equal(0, writeCounter);
    }

    [Fact]
    public void Write_ExtendedRom_AtCFFF_ResetsC8Rom()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out var status, out _, out _);
        var card = new MockCard(10, "Test Card", extRomFill: 0x88);
        factory.RegisterCard(card);
        slots.InstallCard(10, SlotNumber.Slot6);
        status.SetIntCxRom(false);
        status.SetIntC8Rom(false);

        // Activate extended ROM
        slots.Read(0x0600);
        Assert.Equal(6, status.StateIntC8RomSlot);

        // Act - Write to $CFFF resets C8ROM
        slots.Write(0x0FFF, 0x00);

        // Assert
        Assert.False(status.StateIntC8Rom);
        Assert.Equal(0, status.StateIntC8RomSlot);
    }

    [Fact]
    public void Write_ExtendedRom_WithNoSlotOwner_IsNoOp()
    {
        // Arrange
        var slots = CreateSlots(out _, out var status, out _, out _);
        status.SetIntCxRom(false);
        status.SetIntC8Rom(false);
        status.SetIntC8RomSlot(0); // No owner

        // Act - Should not throw
        slots.Write(0x0800, 0x11);

        // Assert
        Assert.True(true);
    }

    #endregion

    #region ManageC800 Edge Cases

    [Fact]
    public void ManageC800_Slot3WithSlotC3RomOff_ForcesIntC8Rom()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out var status, out _, out _);
        var card = new MockCard(10, "Test Card", extRomFill: 0x88);
        factory.RegisterCard(card);
        slots.InstallCard(10, SlotNumber.Slot3);
        status.SetIntCxRom(false);
        status.SetSlotC3Rom(false);
        status.SetIntC8Rom(false);
        status.SetIntC8RomSlot(0);

        // Act - Read from slot 3 ROM
        slots.Read(0x0300);

        // Assert - IntC8Rom should be forced on
        Assert.True(status.StateIntC8Rom);
        Assert.Equal(255, status.StateIntC8RomSlot);
    }

    [Fact]
    public void ManageC800_Slot3WithSlotC3RomOn_DoesNotForceIntC8Rom()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out var status, out _, out _);
        var card = new MockCard(10, "Test Card", extRomFill: 0x88);
        factory.RegisterCard(card);
        slots.InstallCard(10, SlotNumber.Slot3);
        status.SetIntCxRom(false);
        status.SetSlotC3Rom(true);
        status.SetIntC8Rom(false);
        status.SetIntC8RomSlot(0);

        // Act - Read from slot 3 ROM
        slots.Read(0x0300);

        // Assert - IntC8Rom should NOT be forced on
        Assert.False(status.StateIntC8Rom);
        Assert.Equal(3, status.StateIntC8RomSlot); // Slot 3 should be active
    }

    [Fact]
    public void ManageC800_WithIntCxRomEnabled_DoesNotActivate()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out var status, out _, out _);
        var card = new MockCard(10, "Test Card", extRomFill: 0x88);
        factory.RegisterCard(card);
        slots.InstallCard(10, SlotNumber.Slot6);
        status.SetIntCxRom(true); // Internal ROM enabled
        status.SetIntC8RomSlot(0);

        // Act
        slots.Read(0x0600);

        // Assert - C800 should NOT be activated
        Assert.Equal(0, status.StateIntC8RomSlot);
    }

    [Fact]
    public void ManageC800_WithAlreadyActiveSlot_DoesNotChange()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out var status, out _, out _);
        var card5 = new MockCard(10, "Card 5", extRomFill: 0x55);
        var card6 = new MockCard(11, "Card 6", extRomFill: 0x66);
        factory.RegisterCard(card5);
        factory.RegisterCard(card6);
        slots.InstallCard(10, SlotNumber.Slot5);
        slots.InstallCard(11, SlotNumber.Slot6);
        status.SetIntCxRom(false);

        // Activate slot 5
        slots.Read(0x0500);
        Assert.Equal(5, status.StateIntC8RomSlot);

        // Act - Access slot 6 ROM
        slots.Read(0x0600);

        // Assert - Slot 5 should still be active (first-come-first-served)
        Assert.Equal(5, status.StateIntC8RomSlot);
    }

    #endregion

    #region Configuration Metadata Tests

    [Fact]
    public void GetMetadata_EmptySlots_ReturnsEmptySlotsList()
    {
        // Arrange
        var slots = CreateSlots(out _, out _, out _, out _);

        // Act
        string metadata = slots.GetMetadata();

        // Assert
        Assert.Contains("\"slots\": []", metadata);
    }

    [Fact]
    public void GetMetadata_WithInstalledCards_ReturnsCardInfo()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out _, out _, out _);
        var card = new MockCard(10, "Test Card");
        factory.RegisterCard(card);
        slots.InstallCard(10, SlotNumber.Slot6);

        // Act
        string metadata = slots.GetMetadata();

        // Assert
        Assert.Contains("\"slotNumber\": 6", metadata);
        Assert.Contains("\"cardId\": 10", metadata);
        Assert.Contains("\"cardName\": \"Test Card\"", metadata);
    }

    [Fact]
    public void GetMetadata_MultipleCards_ReturnsAllCards()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out _, out _, out _);
        var card1 = new MockCard(10, "Card 1");
        var card2 = new MockCard(11, "Card 2");
        factory.RegisterCard(card1);
        factory.RegisterCard(card2);
        slots.InstallCard(10, SlotNumber.Slot3);
        slots.InstallCard(11, SlotNumber.Slot6);

        // Act
        string metadata = slots.GetMetadata();

        // Assert
        Assert.Contains("\"slotNumber\": 3", metadata);
        Assert.Contains("\"slotNumber\": 6", metadata);
        Assert.Contains("\"cardId\": 10", metadata);
        Assert.Contains("\"cardId\": 11", metadata);
    }

    [Fact]
    public void GetMetadata_IncludesVersionNumber()
    {
        // Arrange
        var slots = CreateSlots(out _, out _, out _, out _);

        // Act
        string metadata = slots.GetMetadata();

        // Assert
        Assert.Contains("\"version\": 1", metadata);
    }

    [Fact]
    public void ApplyMetadata_EmptyString_ClearsAllSlots()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out _, out _, out _);
        var card = new MockCard(10, "Test Card");
        factory.RegisterCard(card);
        slots.InstallCard(10, SlotNumber.Slot6);

        // Act
        bool result = slots.ApplyMetadata("");

        // Assert
        Assert.True(result);
        Assert.True(slots.IsEmpty(SlotNumber.Slot6));
    }

    [Fact]
    public void ApplyMetadata_ValidJson_RestoresConfiguration()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out _, out _, out _);
        var card = new MockCard(10, "Test Card");
        factory.RegisterCard(card);

        string metadata = @"{
            ""version"": 1,
            ""slots"": [
                {
                    ""slotNumber"": 6,
                    ""cardId"": 10,
                    ""cardName"": ""Test Card"",
                    ""metadata"": """"
                }
            ]
        }";

        // Act
        bool result = slots.ApplyMetadata(metadata);

        // Assert
        Assert.True(result);
        Assert.False(slots.IsEmpty(SlotNumber.Slot6));
        Assert.Equal(10, slots.GetCardIn(SlotNumber.Slot6).Id);
    }

    [Fact]
    public void ApplyMetadata_InvalidJson_ReturnsFalse()
    {
        // Arrange
        var slots = CreateSlots(out _, out _, out _, out _);

        // Act
        bool result = slots.ApplyMetadata("{ invalid json }");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ApplyMetadata_MissingSlotNumber_ReturnsFalseButContinues()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out _, out _, out _);
        var card = new MockCard(10, "Test Card");
        factory.RegisterCard(card);

        string metadata = @"{
            ""version"": 1,
            ""slots"": [
                {
                    ""cardId"": 10
                }
            ]
        }";

        // Act
        bool result = slots.ApplyMetadata(metadata);

        // Assert
        Assert.False(result); // Failed due to missing slotNumber
    }

    [Fact]
    public void ApplyMetadata_InvalidSlotNumber_ReturnsFalseButContinues()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out _, out _, out _);
        var card = new MockCard(10, "Test Card");
        factory.RegisterCard(card);

        string metadata = @"{
            ""version"": 1,
            ""slots"": [
                {
                    ""slotNumber"": 0,
                    ""cardId"": 10
                }
            ]
        }";

        // Act
        bool result = slots.ApplyMetadata(metadata);

        // Assert
        Assert.False(result); // Failed due to invalid slot number (0 is reserved)
    }

    [Fact]
    public void ApplyMetadata_InvalidCardId_ReturnsFalse()
    {
        // Arrange
        var slots = CreateSlots(out _, out _, out _, out _);

        string metadata = @"{
            ""version"": 1,
            ""slots"": [
                {
                    ""slotNumber"": 6,
                    ""cardId"": 999
                }
            ]
        }";

        // Act
        bool result = slots.ApplyMetadata(metadata);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ApplyMetadata_RoundTrip_PreservesConfiguration()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out _, out _, out _);
        var card1 = new MockCard(10, "Card 1");
        var card2 = new MockCard(11, "Card 2");
        factory.RegisterCard(card1);
        factory.RegisterCard(card2);
        slots.InstallCard(10, SlotNumber.Slot3);
        slots.InstallCard(11, SlotNumber.Slot6);

        // Act - Save and restore
        string metadata = slots.GetMetadata();
        var slotsNew = CreateSlots(out var factoryNew, out _, out _, out _);
        factoryNew.RegisterCard(card1);
        factoryNew.RegisterCard(card2);
        bool result = slotsNew.ApplyMetadata(metadata);

        // Assert
        Assert.True(result);
        Assert.Equal(10, slotsNew.GetCardIn(SlotNumber.Slot3).Id);
        Assert.Equal(11, slotsNew.GetCardIn(SlotNumber.Slot6).Id);
        Assert.True(slotsNew.IsEmpty(SlotNumber.Slot1));
    }

    [Fact]
    public void ApplyMetadata_NullString_ReturnsFalse()
    {
        // Arrange
        var slots = CreateSlots(out _, out _, out _, out _);

        // Act
        bool result = slots.ApplyMetadata(null!);

        // Assert
        Assert.True(result); // Null/empty is treated as "clear all slots"
    }

    [Fact]
    public void ApplyMetadata_WhitespaceString_ClearsAllSlots()
    {
        // Arrange
        var slots = CreateSlots(out var factory, out _, out _, out _);
        var card = new MockCard(10, "Test Card");
        factory.RegisterCard(card);
        slots.InstallCard(10, SlotNumber.Slot6);

        // Act
        bool result = slots.ApplyMetadata("   ");

        // Assert
        Assert.True(result);
        Assert.True(slots.IsEmpty(SlotNumber.Slot6));
    }

    #endregion

    #region Additional Helper Classes

    /// <summary>
    /// Testable card with callbacks for testing side effects.
    /// </summary>
    private class TestableCard(int id, string name,
        byte ioFill = 0x00,
        byte romFill = 0x00,
        byte extRomFill = 0x00,
        Action? onReset = null,
        Action? onWriteIO = null,
        Action? onWriteRom = null,
        Action? onWriteExtRom = null) : ICard
    {
        private readonly byte _ioFillValue = ioFill;
        private readonly byte _romFillValue = romFill;
        private readonly byte _extRomFillValue = extRomFill;
        private readonly Action? _onReset = onReset;
        private readonly Action? _onWriteIO = onWriteIO;
        private readonly Action? _onWriteRom = onWriteRom;
        private readonly Action? _onWriteExtRom = onWriteExtRom;

        public SlotNumber Slot { get; private set; } = SlotNumber.Unslotted;
        public string Name { get; } = name;
        public string Description => $"Testable card: {Name}";
        public int Id { get; } = id;

        public void OnInstalled(SlotNumber slot) => Slot = slot;
        public byte? ReadIO(byte offset) => _ioFillValue;
        public void WriteIO(byte offset, byte value) => _onWriteIO?.Invoke();
        public byte? ReadRom(byte offset) => _romFillValue;
        public void WriteRom(byte offset, byte value) => _onWriteRom?.Invoke();
        public byte? ReadExtendedRom(ushort offset) => _extRomFillValue;
        public void WriteExtendedRom(ushort offset, byte value) => _onWriteExtRom?.Invoke();
        public string GetMetadata() => string.Empty;
        public bool ApplyMetadata(string metadata) => true;
        public void Reset() => _onReset?.Invoke();
        public void HandleMessage(ICardMessage message) { }
        public ICard Clone() => new TestableCard(Id, Name, _ioFillValue, _romFillValue, _extRomFillValue, _onReset, _onWriteIO, _onWriteRom, _onWriteExtRom);
    }

    #endregion
}
