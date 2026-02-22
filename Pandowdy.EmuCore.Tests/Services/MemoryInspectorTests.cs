// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Messages;
using Pandowdy.EmuCore.Services;

namespace Pandowdy.EmuCore.Tests.Services;

/// <summary>
/// Comprehensive tests for the MemoryInspector class, covering all memory region access methods.
/// </summary>
public class MemoryInspectorTests
{
    #region Test Helpers and Mocks

    private class MockMemoryPoolReader : IDirectMemoryPoolReader
    {
        private readonly byte[] _mainRam = new byte[0x10000];
        private readonly byte[] _auxRam = new byte[0x10000];

        public MockMemoryPoolReader(byte mainFill = 0x00, byte auxFill = 0x00)
        {
            Array.Fill(_mainRam, mainFill);
            Array.Fill(_auxRam, auxFill);
        }

        public void WriteMain(int address, byte value) => _mainRam[address & 0xFFFF] = value;
        public void WriteAux(int address, byte value) => _auxRam[address & 0xFFFF] = value;
        public byte ReadRawMain(int address) => _mainRam[address & 0xFFFF];
        public byte ReadRawAux(int address) => _auxRam[address & 0xFFFF];
    }

    private class MockSystemRomProvider : ISystemRomProvider
    {
        private readonly byte[] _rom = new byte[0x4000];

        public MockSystemRomProvider(byte fillPattern = 0xFF)
        {
            Array.Fill(_rom, fillPattern);
        }

        public void Write(ushort address, byte value)
        {
            if (address < _rom.Length)
            {
                _rom[address] = value;
            }
        }

        public int Size => 0x4000;
        public byte Read(ushort address) => _rom[address % _rom.Length];
        public byte Peek(ushort address) => _rom[address % _rom.Length];
        public void LoadRomFile(string filePath) { }
    }

    private class MockLanguageCard(byte fillValue = 0xCC) : ILanguageCard
    {
        private readonly byte _fillValue = fillValue;

        public int Size => 0x3000;
        public byte Read(ushort address) => _fillValue;
        public byte Peek(ushort address) => _fillValue;
        public void Write(ushort address, byte val) { }
        public static string GetMetadata() => string.Empty;
        public static bool ApplyMetadata(string _) => true;
        public static void Reset() { }
        public void Restart() { }
    }

    private class MockCard(int id, string name, byte romFill = 0, byte extRomFill = 0) : ICard
    {
        private readonly byte _romFill = romFill;
        private readonly byte _extRomFill = extRomFill;

        public SlotNumber Slot { get; private set; } = SlotNumber.Unslotted;
        public string Name { get; } = name;
        public string Description => $"Mock card: {Name}";
        public int Id { get; } = id;

        public void OnInstalled(SlotNumber slot) => Slot = slot;
        public byte? ReadIO(byte offset) => null;
        public void WriteIO(byte offset, byte value) { }
        public byte? ReadRom(byte offset) => _romFill;
        public void WriteRom(byte offset, byte value) { }
        public byte? ReadExtendedRom(ushort offset) => _extRomFill;
        public void WriteExtendedRom(ushort offset, byte value) { }
        public string GetMetadata() => string.Empty;
        public bool ApplyMetadata(string metadata) => true;
        public void Reset() { }
        public void HandleMessage(ICardMessage message) { }
        public ICard Clone() => new MockCard(Id, Name, _romFill, _extRomFill);
    }

    private class MockSlots : ISlots
    {
        private readonly ICard[] _cards = new ICard[8];

        public MockSlots()
        {
            var nullCard = new MockCard(0, "NullCard");
            for (int i = 0; i < 8; i++)
            {
                _cards[i] = nullCard;
            }
        }

        public void InstallCardDirect(ICard card, SlotNumber slot)
        {
            _cards[(int)slot] = card;
            card.OnInstalled(slot);
        }

        public int Size => 0x1000;
        public byte Read(ushort address) => 0x00;
        public byte Peek(ushort address) => 0x00;
        public void Write(ushort address, byte val) { }
        public void InstallCard(int id, SlotNumber slot) { }
        public void InstallCard(string name, SlotNumber slot) { }
        public void RemoveCard(SlotNumber slot) { }
        public ICard GetCardIn(SlotNumber slot) => _cards[(int)slot];
        public bool IsEmpty(SlotNumber slot) => _cards[(int)slot].Id == 0;
        public string GetMetadata() => string.Empty;
        public bool ApplyMetadata(string metadata) => true;
        public void Reset() { }
    }

    private static MemoryInspector CreateMemoryInspector(
        out MockMemoryPoolReader memoryPool,
        out MockSystemRomProvider systemRom,
        out MockSlots slots,
        out MockLanguageCard languageCard,
        out SystemStatusProvider status)
    {
        memoryPool = new MockMemoryPoolReader(0xAA, 0xBB);
        systemRom = new MockSystemRomProvider(0xFF);
        slots = new MockSlots();
        languageCard = new MockLanguageCard(0xCC);
        status = new SystemStatusProvider();

        return new MemoryInspector(memoryPool, systemRom, slots, languageCard, status);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidArguments_CreatesInstance()
    {
        var inspector = CreateMemoryInspector(out _, out _, out _, out _, out _);
        Assert.NotNull(inspector);
    }

    [Fact]
    public void Constructor_WithNullMemoryPool_ThrowsArgumentNullException()
    {
        var rom = new MockSystemRomProvider();
        var slots = new MockSlots();
        var lc = new MockLanguageCard();
        var status = new SystemStatusProvider();

        Assert.Throws<ArgumentNullException>(() =>
            new MemoryInspector(null!, rom, slots, lc, status));
    }

    [Fact]
    public void Constructor_WithNullSystemRom_ThrowsArgumentNullException()
    {
        var pool = new MockMemoryPoolReader();
        var slots = new MockSlots();
        var lc = new MockLanguageCard();
        var status = new SystemStatusProvider();

        Assert.Throws<ArgumentNullException>(() =>
            new MemoryInspector(pool, null!, slots, lc, status));
    }

    [Fact]
    public void Constructor_WithNullSlots_ThrowsArgumentNullException()
    {
        var pool = new MockMemoryPoolReader();
        var rom = new MockSystemRomProvider();
        var lc = new MockLanguageCard();
        var status = new SystemStatusProvider();

        Assert.Throws<ArgumentNullException>(() =>
            new MemoryInspector(pool, rom, null!, lc, status));
    }

    [Fact]
    public void Constructor_WithNullLanguageCard_ThrowsArgumentNullException()
    {
        var pool = new MockMemoryPoolReader();
        var rom = new MockSystemRomProvider();
        var slots = new MockSlots();
        var status = new SystemStatusProvider();

        Assert.Throws<ArgumentNullException>(() =>
            new MemoryInspector(pool, rom, slots, null!, status));
    }

    [Fact]
    public void Constructor_WithNullStatus_ThrowsArgumentNullException()
    {
        var pool = new MockMemoryPoolReader();
        var rom = new MockSystemRomProvider();
        var slots = new MockSlots();
        var lc = new MockLanguageCard();

        Assert.Throws<ArgumentNullException>(() =>
            new MemoryInspector(pool, rom, slots, lc, null!));
    }

    #endregion

    #region ReadRawMain / ReadRawAux Tests

    [Fact]
    public void ReadRawMain_ReturnsMainMemoryValue()
    {
        var inspector = CreateMemoryInspector(out var pool, out _, out _, out _, out _);
        pool.WriteMain(0x1234, 0x42);

        byte result = inspector.ReadRawMain(0x1234);

        Assert.Equal(0x42, result);
    }

    [Fact]
    public void ReadRawMain_WithAddressWraparound_ReadsCorrectly()
    {
        var inspector = CreateMemoryInspector(out var pool, out _, out _, out _, out _);
        pool.WriteMain(0x0000, 0x99);

        byte result = inspector.ReadRawMain(0x10000);

        Assert.Equal(0x99, result);
    }

    [Fact]
    public void ReadRawAux_ReturnsAuxMemoryValue()
    {
        var inspector = CreateMemoryInspector(out var pool, out _, out _, out _, out _);
        pool.WriteAux(0x5678, 0x88);

        byte result = inspector.ReadRawAux(0x5678);

        Assert.Equal(0x88, result);
    }

    [Fact]
    public void ReadRawAux_WithAddressWraparound_ReadsCorrectly()
    {
        var inspector = CreateMemoryInspector(out var pool, out _, out _, out _,out _);
        pool.WriteAux(0xFFFF, 0x77);

        byte result = inspector.ReadRawAux(0x1FFFF);

        Assert.Equal(0x77, result);
    }

    #endregion

    #region ReadSystemRom Tests

    [Fact]
    public void ReadSystemRom_ValidAddress_ReturnsRomValue()
    {
        var inspector = CreateMemoryInspector(out _, out var rom, out _, out _, out _);
        rom.Write(0x0100, 0xF0);

        byte result = inspector.ReadSystemRom(0xC100);

        Assert.Equal(0xF0, result);
    }

    [Fact]
    public void ReadSystemRom_BelowC100_ReturnsZero()
    {
        var inspector = CreateMemoryInspector(out _, out _, out _, out _, out _);

        Assert.Equal(0, inspector.ReadSystemRom(0xC000));
        Assert.Equal(0, inspector.ReadSystemRom(0xC0FF));
        Assert.Equal(0, inspector.ReadSystemRom(0x0000));
    }

    [Fact]
    public void ReadSystemRom_AboveFFFF_ReturnsZero()
    {
        var inspector = CreateMemoryInspector(out _, out _, out _, out _, out _);

        byte result = inspector.ReadSystemRom(0x10000);

        Assert.Equal(0, result);
    }

    [Theory]
    [InlineData(0xC100)]
    [InlineData(0xC7FF)]
    [InlineData(0xC800)]
    [InlineData(0xCFFF)]
    [InlineData(0xD000)]
    [InlineData(0xFFFF)]
    public void ReadSystemRom_ValidAddressRanges_ReadsFromRom(int address)
    {
        var inspector = CreateMemoryInspector(out _, out var rom, out _, out _, out _);
        ushort offset = (ushort)(address - 0xC000);
        rom.Write(offset, 0xAB);

        byte result = inspector.ReadSystemRom(address);

        Assert.Equal(0xAB, result);
    }

    #endregion

    #region ReadActiveHighMemory Tests - Language Card

    [Fact]
    public void ReadActiveHighMemory_LanguageCardArea_ReadsFromLanguageCard()
    {
        var inspector = CreateMemoryInspector(out _, out var rom, out _, out _, out var status);
        rom.Write(0x1000, 0xF0);
        status.SetHighRead(true);

        byte result = inspector.ReadActiveHighMemory(0xD000);

        Assert.Equal(0xCC, result);
    }

    [Fact]
    public void ReadActiveHighMemory_AtFFFF_ReadsFromLanguageCard()
    {
        var inspector = CreateMemoryInspector(out _, out _, out _, out _, out var status);
        status.SetHighRead(true);

        byte result = inspector.ReadActiveHighMemory(0xFFFF);

        Assert.Equal(0xCC, result);
    }

    #endregion

    #region ReadActiveHighMemory Tests - ROM Banking

    [Fact]
    public void ReadActiveHighMemory_SlotRom_WithIntCxRomTrue_ReadsFromSystemRom()
    {
        var inspector = CreateMemoryInspector(out _, out var rom, out var slots, out _, out var status);
        var card = new MockCard(10, "Test Card", romFill: 0x55);
        slots.InstallCardDirect(card, SlotNumber.Slot6);
        rom.Write(0x0600, 0xAA);
        status.SetIntCxRom(true);

        byte result = inspector.ReadActiveHighMemory(0xC600);

        Assert.Equal(0xAA, result);
    }

    [Fact]
    public void ReadActiveHighMemory_SlotRom_WithIntCxRomFalse_ReadsFromCardRom()
    {
        var inspector = CreateMemoryInspector(out _, out _, out var slots, out _, out var status);
        var card = new MockCard(10, "Test Card", romFill: 0x55);
        slots.InstallCardDirect(card, SlotNumber.Slot6);
        status.SetIntCxRom(false);

        byte result = inspector.ReadActiveHighMemory(0xC600);

        Assert.Equal(0x55, result);
    }

    [Fact]
    public void ReadActiveHighMemory_EmptySlot_FallsBackToSystemRom()
    {
        var inspector = CreateMemoryInspector(out _, out var rom, out _, out _, out var status);
        rom.Write(0x0600, 0xBB);
        status.SetIntCxRom(false);

        byte result = inspector.ReadActiveHighMemory(0xC600);

        Assert.Equal(0xBB, result);
    }

    [Fact]
    public void ReadActiveHighMemory_Slot3_WithSlotC3RomOff_ReadsFromSystemRom()
    {
        var inspector = CreateMemoryInspector(out _, out var rom, out var slots, out _, out var status);
        var card = new MockCard(10, "80-Column Card", romFill: 0x80);
        slots.InstallCardDirect(card, SlotNumber.Slot3);
        rom.Write(0x0300, 0xC3);
        status.SetIntCxRom(false);
        status.SetSlotC3Rom(false);

        byte result = inspector.ReadActiveHighMemory(0xC300);

        Assert.Equal(0xC3, result);
    }

    [Fact]
    public void ReadActiveHighMemory_Slot3_WithSlotC3RomOn_ReadsFromCardRom()
    {
        var inspector = CreateMemoryInspector(out _, out _, out var slots, out _, out var status);
        var card = new MockCard(10, "80-Column Card", romFill: 0x80);
        slots.InstallCardDirect(card, SlotNumber.Slot3);
        status.SetIntCxRom(false);
        status.SetSlotC3Rom(true);

        byte result = inspector.ReadActiveHighMemory(0xC300);

        Assert.Equal(0x80, result);
    }

    #endregion

    #region ReadActiveHighMemory Tests - Extended ROM

    [Fact]
    public void ReadActiveHighMemory_ExtendedRom_WithIntCxRomTrue_ReadsFromSystemRom()
    {
        var inspector = CreateMemoryInspector(out _, out var rom, out var slots, out _, out var status);
        var card = new MockCard(10, "Test Card", extRomFill: 0x88);
        slots.InstallCardDirect(card, SlotNumber.Slot6);
        rom.Write(0x0800, 0xCC);
        status.SetIntCxRom(true);

        byte result = inspector.ReadActiveHighMemory(0xC800);

        Assert.Equal(0xCC, result);
    }

    [Fact]
    public void ReadActiveHighMemory_ExtendedRom_WithIntC8RomTrue_ReadsFromSystemRom()
    {
        var inspector = CreateMemoryInspector(out _, out var rom, out var slots, out _, out var status);
        var card = new MockCard(10, "Test Card", extRomFill: 0x88);
        slots.InstallCardDirect(card, SlotNumber.Slot6);
        rom.Write(0x0800, 0xCC);
        status.SetIntCxRom(false);
        status.SetIntC8Rom(true);

        byte result = inspector.ReadActiveHighMemory(0xC800);

        Assert.Equal(0xCC, result);
    }

    [Fact]
    public void ReadActiveHighMemory_ExtendedRom_WithSlotActive_ReadsFromCardRom()
    {
        var inspector = CreateMemoryInspector(out _, out _, out var slots, out _, out var status);
        var card = new MockCard(10, "Test Card", extRomFill: 0x88);
        slots.InstallCardDirect(card, SlotNumber.Slot6);
        status.SetIntCxRom(false);
        status.SetIntC8Rom(false);
        status.SetIntC8RomSlot(6);

        byte result = inspector.ReadActiveHighMemory(0xC800);

        Assert.Equal(0x88, result);
    }

    [Fact]
    public void ReadActiveHighMemory_ExtendedRom_NoSlotOwner_ReadsFromSystemRom()
    {
        var inspector = CreateMemoryInspector(out _, out var rom, out _, out _, out var status);
        rom.Write(0x0800, 0xDD);
        status.SetIntCxRom(false);
        status.SetIntC8Rom(false);
        status.SetIntC8RomSlot(0);

        byte result = inspector.ReadActiveHighMemory(0xC800);

        Assert.Equal(0xDD, result);
    }

    [Fact]
    public void ReadActiveHighMemory_BelowC100_ReturnsZero()
    {
        var inspector = CreateMemoryInspector(out _, out _, out _, out _, out _);

        Assert.Equal(0, inspector.ReadActiveHighMemory(0x0000));
        Assert.Equal(0, inspector.ReadActiveHighMemory(0xC000));
        Assert.Equal(0, inspector.ReadActiveHighMemory(0xC0FF));
    }

    #endregion

    #region ReadSlotRom Tests

    [Fact]
    public void ReadSlotRom_ValidSlotAndOffset_ReadsFromCard()
    {
        var inspector = CreateMemoryInspector(out _, out _, out var slots, out _, out _);
        var card = new MockCard(10, "Test Card", romFill: 0x66);
        slots.InstallCardDirect(card, SlotNumber.Slot4);

        byte result = inspector.ReadSlotRom(4, 0x50);

        Assert.Equal(0x66, result);
    }

    [Fact]
    public void ReadSlotRom_EmptySlot_ReturnsZero()
    {
        var inspector = CreateMemoryInspector(out _, out _, out _, out _, out _);

        byte result = inspector.ReadSlotRom(3, 0x00);

        Assert.Equal(0, result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(-1)]
    public void ReadSlotRom_InvalidSlot_ReturnsZero(int slot)
    {
        var inspector = CreateMemoryInspector(out _, out _, out _, out _, out _);

        byte result = inspector.ReadSlotRom(slot, 0x00);

        Assert.Equal(0, result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0x100)]
    [InlineData(0x200)]
    public void ReadSlotRom_InvalidOffset_ReturnsZero(int offset)
    {
        var inspector = CreateMemoryInspector(out _, out _, out var slots, out _, out _);
        var card = new MockCard(10, "Test Card", romFill: 0x66);
        slots.InstallCardDirect(card, SlotNumber.Slot1);

        byte result = inspector.ReadSlotRom(1, offset);

        Assert.Equal(0, result);
    }

    [Theory]
    [InlineData(1, 0x00)]
    [InlineData(2, 0x80)]
    [InlineData(7, 0xFF)]
    public void ReadSlotRom_AllSlots_ReadsCorrectly(int slot, int offset)
    {
        var inspector = CreateMemoryInspector(out _, out _, out var slots, out _, out _);
        var card = new MockCard(10 + slot, $"Card {slot}", romFill: (byte)slot);
        slots.InstallCardDirect(card, (SlotNumber)slot);

        byte result = inspector.ReadSlotRom(slot, offset);

        Assert.Equal((byte)slot, result);
    }

    #endregion

    #region ReadSlotExtendedRom Tests

    [Fact]
    public void ReadSlotExtendedRom_ValidSlotAndOffset_ReadsFromCard()
    {
        var inspector = CreateMemoryInspector(out _, out _, out var slots, out _, out _);
        var card = new MockCard(10, "Test Card", extRomFill: 0x99);
        slots.InstallCardDirect(card, SlotNumber.Slot6);

        byte result = inspector.ReadSlotExtendedRom(6, 0x400);

        Assert.Equal(0x99, result);
    }

    [Fact]
    public void ReadSlotExtendedRom_EmptySlot_ReturnsZero()
    {
        var inspector = CreateMemoryInspector(out _, out _, out _, out _, out _);

        byte result = inspector.ReadSlotExtendedRom(5, 0x000);

        Assert.Equal(0, result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(-1)]
    public void ReadSlotExtendedRom_InvalidSlot_ReturnsZero(int slot)
    {
        var inspector = CreateMemoryInspector(out _, out _, out _, out _, out _);

        byte result = inspector.ReadSlotExtendedRom(slot, 0x000);

        Assert.Equal(0, result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0x800)]
    [InlineData(0x1000)]
    public void ReadSlotExtendedRom_InvalidOffset_ReturnsZero(int offset)
    {
        var inspector = CreateMemoryInspector(out _, out _, out var slots, out _, out _);
        var card = new MockCard(10, "Test Card", extRomFill: 0x99);
        slots.InstallCardDirect(card, SlotNumber.Slot1);

        byte result = inspector.ReadSlotExtendedRom(1, offset);

        Assert.Equal(0, result);
    }

    [Theory]
    [InlineData(1, 0x000)]
    [InlineData(3, 0x400)]
    [InlineData(7, 0x7FF)]
    public void ReadSlotExtendedRom_AllSlots_ReadsCorrectly(int slot, int offset)
    {
        var inspector = CreateMemoryInspector(out _, out _, out var slots, out _, out _);
        var card = new MockCard(10 + slot, $"Card {slot}", extRomFill: (byte)(slot * 16));
        slots.InstallCardDirect(card, (SlotNumber)slot);

        byte result = inspector.ReadSlotExtendedRom(slot, offset);

        Assert.Equal((byte)(slot * 16), result);
    }

    #endregion

    #region ReadMainBlock / ReadAuxBlock Tests

    [Fact]
    public void ReadMainBlock_ValidRange_ReturnsCorrectData()
    {
        var inspector = CreateMemoryInspector(out var pool, out _, out _, out _, out _);
        pool.WriteMain(0x1000, 0x11);
        pool.WriteMain(0x1001, 0x22);
        pool.WriteMain(0x1002, 0x33);

        byte[] result = inspector.ReadMainBlock(0x1000, 3);

        Assert.Equal(3, result.Length);
        Assert.Equal(0x11, result[0]);
        Assert.Equal(0x22, result[1]);
        Assert.Equal(0x33, result[2]);
    }

    [Fact]
    public void ReadMainBlock_ZeroLength_ReturnsEmptyArray()
    {
        var inspector = CreateMemoryInspector(out _, out _, out _, out _, out _);

        byte[] result = inspector.ReadMainBlock(0x0000, 0);

        Assert.Empty(result);
    }

    [Fact]
    public void ReadMainBlock_NegativeLength_ReturnsEmptyArray()
    {
        var inspector = CreateMemoryInspector(out _, out _, out _, out _, out _);

        byte[] result = inspector.ReadMainBlock(0x0000, -10);

        Assert.Empty(result);
    }

    [Fact]
    public void ReadMainBlock_AcrossPageBoundary_ReadsCorrectly()
    {
        var inspector = CreateMemoryInspector(out var pool, out _, out _, out _, out _);
        pool.WriteMain(0x01FF, 0xAA);
        pool.WriteMain(0x0200, 0xBB);

        byte[] result = inspector.ReadMainBlock(0x01FF, 2);

        Assert.Equal(2, result.Length);
        Assert.Equal(0xAA, result[0]);
        Assert.Equal(0xBB, result[1]);
    }

    [Fact]
    public void ReadMainBlock_WithWraparound_ReadsCorrectly()
    {
        var inspector = CreateMemoryInspector(out var pool, out _, out _, out _, out _);
        pool.WriteMain(0xFFFF, 0x99);
        pool.WriteMain(0x0000, 0x88);

        byte[] result = inspector.ReadMainBlock(0xFFFF, 2);

        Assert.Equal(2, result.Length);
        Assert.Equal(0x99, result[0]);
        Assert.Equal(0x88, result[1]);
    }

    [Fact]
    public void ReadAuxBlock_ValidRange_ReturnsCorrectData()
    {
        var inspector = CreateMemoryInspector(out var pool, out _, out _, out _, out _);
        pool.WriteAux(0x2000, 0x44);
        pool.WriteAux(0x2001, 0x55);
        pool.WriteAux(0x2002, 0x66);

        byte[] result = inspector.ReadAuxBlock(0x2000, 3);

        Assert.Equal(3, result.Length);
        Assert.Equal(0x44, result[0]);
        Assert.Equal(0x55, result[1]);
        Assert.Equal(0x66, result[2]);
    }

    [Fact]
    public void ReadAuxBlock_ZeroLength_ReturnsEmptyArray()
    {
        var inspector = CreateMemoryInspector(out _, out _, out _, out _, out _);

        byte[] result = inspector.ReadAuxBlock(0x0000, 0);

        Assert.Empty(result);
    }

    [Fact]
    public void ReadAuxBlock_NegativeLength_ReturnsEmptyArray()
    {
        var inspector = CreateMemoryInspector(out _, out _, out _, out _, out _);

        byte[] result = inspector.ReadAuxBlock(0x0000, -5);

        Assert.Empty(result);
    }

    [Fact]
    public void ReadAuxBlock_WithWraparound_ReadsCorrectly()
    {
        var inspector = CreateMemoryInspector(out var pool, out _, out _, out _, out _);
        pool.WriteAux(0xFFFE, 0x77);
        pool.WriteAux(0xFFFF, 0x88);
        pool.WriteAux(0x0000, 0x99);

        byte[] result = inspector.ReadAuxBlock(0xFFFE, 3);

        Assert.Equal(3, result.Length);
        Assert.Equal(0x77, result[0]);
        Assert.Equal(0x88, result[1]);
        Assert.Equal(0x99, result[2]);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void Integration_AllMemoryTypes_AccessibleThroughSingleInterface()
    {
        var inspector = CreateMemoryInspector(out var pool, out var rom, out var slots, out _, out var status);
        pool.WriteMain(0x0800, 0x01);
        pool.WriteAux(0x0800, 0x02);
        rom.Write(0x0C00, 0x03);
        var card = new MockCard(10, "Test", romFill: 0x04, extRomFill: 0x05);
        slots.InstallCardDirect(card, SlotNumber.Slot6);
        status.SetIntCxRom(false);
        status.SetIntC8Rom(false);
        status.SetIntC8RomSlot(6);

        Assert.Equal(0x01, inspector.ReadRawMain(0x0800));
        Assert.Equal(0x02, inspector.ReadRawAux(0x0800));
        Assert.Equal(0x03, inspector.ReadSystemRom(0xCC00));
        Assert.Equal(0x04, inspector.ReadSlotRom(6, 0x00));
        Assert.Equal(0x05, inspector.ReadSlotExtendedRom(6, 0x000));
    }

    [Fact]
    public void Integration_ReadActiveHighMemory_RespectsAllSoftSwitches()
    {
        var inspector = CreateMemoryInspector(out _, out var rom, out var slots, out _, out var status);
        rom.Write(0x0600, 0xAA);
        var card = new MockCard(10, "Test", romFill: 0xBB);
        slots.InstallCardDirect(card, SlotNumber.Slot6);

        status.SetIntCxRom(false);
        Assert.Equal(0xBB, inspector.ReadActiveHighMemory(0xC600));

        status.SetIntCxRom(true);
        Assert.Equal(0xAA, inspector.ReadActiveHighMemory(0xC600));
    }

    #endregion
}
