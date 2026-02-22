// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Diagnostics;
using Pandowdy.EmuCore.IO;
using Pandowdy.EmuCore.Machine;
using Pandowdy.EmuCore.Memory;
using System.Runtime.CompilerServices;

namespace Pandowdy.EmuCore.Slots;

/// <summary>
/// Implements the Apple IIe expansion slot system, managing peripheral cards and their I/O and ROM access.
/// </summary>

public class Slots : ISlots
{
    /// <summary>
    /// Cached JsonSerializerOptions for configuration serialization to avoid allocation overhead.
    /// </summary>
    private static readonly System.Text.Json.JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true
    };


    /// <summary>
    /// Array of installed cards indexed by slot number (0-7).
    /// </summary>
    /// <remarks>
    /// Slot 0 is reserved for system use. Slots 1-7 correspond to the physical expansion slots.
    /// Empty slots contain <see cref="NullCard"/> instances.
    /// </remarks>
    private ICard[] _cards;

    /// <summary>
    /// Factory for creating card instances.
    /// </summary>
    private ICardFactory _factory;
    
    /// <summary>
    /// Provider for system ROM data at $C000-$CFFF.
    /// </summary>
    private ISystemRomProvider _rom;
    
    /// <summary>
    /// Provider for floating bus values when no device responds.
    /// </summary>
    private IFloatingBusProvider _floatingBus;
    
    /// <summary>
    /// Provider for soft switch states (INTCXROM, SLOTCXROM, SLOTC3ROM, etc.).
    /// </summary>
    private ISystemStatusMutator _status;

    /// <summary>
    /// Optional restart collection for registering cards that implement <see cref="IRestartable"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Set via <see cref="SetRestartCollection"/> after DI construction to avoid a circular
    /// dependency (RestartCollection → IRestartable singletons → VA2MBus → AddressSpaceController
    /// → ISlots → RestartCollection). When set, cards that implement <see cref="IRestartable"/>
    /// are automatically registered on install and unregistered on remove.
    /// </para>
    /// </remarks>
    private RestartCollection? _restartCollection;

    /// <summary>
    /// Initializes a new instance of the <see cref="Slots"/> class with all slots empty.
    /// </summary>
    /// <param name="factory">The card factory for creating card instances.</param>
    /// <param name="rom">The system ROM provider for $C000-$CFFF range.</param>
    /// <param name="floatingBus">The floating bus provider for unresponsive addresses.</param>
    /// <param name="status">The system status provider for soft switch states.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if any parameter is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the card factory cannot create a NullCard for initialization.
    /// </exception>
    /// <remarks>
    /// <para>
    /// All seven expansion slots (1-7) are initialized with <see cref="NullCard"/> instances,
    /// representing empty slots. Slot 0 is also filled with a NullCard since it's reserved
    /// for system use and should never be accessed.
    /// </para>
    /// <para>
    /// The constructor creates independent NullCard instances via <see cref="ICard.Clone"/>
    /// to ensure each slot has its own instance, even though NullCards are stateless.
    /// </para>
    /// </remarks>
    public Slots(ICardFactory factory, ISystemRomProvider rom, IFloatingBusProvider floatingBus, ISystemStatusMutator status)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(rom);
        ArgumentNullException.ThrowIfNull(floatingBus);
        ArgumentNullException.ThrowIfNull(status);

        _factory = factory;
        _rom = rom;
        _floatingBus = floatingBus;
        _status = status;

        ICard nullcard = _factory.GetNullCard() ?? throw new InvalidOperationException("Could not create a Null Card");
        _cards = [
            nullcard, // Slot 0 is reserved for system
            nullcard.Clone(),
            nullcard.Clone(),
            nullcard.Clone(),
            nullcard.Clone(),
            nullcard.Clone(),
            nullcard.Clone(),
            nullcard.Clone()
         ];

        for (int i = 1; i <= 7; i++) // Register the default null cards above.
        {
            InstallExistingCard(_cards[i], (SlotNumber) i);
        }
    }

    /// <summary>
    /// Gets the size of the address space managed by this slots system.
    /// </summary>
    /// <value>
    /// Always returns 0x1000 (4096 bytes), representing the $C000-$CFFF range.
    /// </value>
    /// <remarks>
    /// <para>
    /// Although the value indicates $C000-$CFFF (4KB), the slots system only handles
    /// $C090-$CFFF (3952 bytes). The range $C000-$C08F contains soft switches and other
    /// system I/O that's handled elsewhere (typically by <see cref="VA2MBus"/>).
    /// </para>
    /// <para>
    /// If <see cref="Read"/> or <see cref="Write"/> is called with an address in the
    /// $C000-$C08F range (0x0000-0x008F when offset), an <see cref="InvalidOperationException"/>
    /// will be thrown.
    /// </para>
    /// </remarks>
    public int Size { get => 0x1000; }

    /// <summary>
    /// Sets the <see cref="RestartCollection"/> for automatic card registration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Called after DI construction (in <c>InitializeCoreAsync</c>) to avoid a circular
    /// dependency. Once set, <see cref="InstallCard(int, SlotNumber)"/> and
    /// <see cref="RemoveCard"/> automatically register/unregister cards that implement
    /// <see cref="IRestartable"/>.
    /// </para>
    /// </remarks>
    public void SetRestartCollection(RestartCollection restartCollection)
    {
        ArgumentNullException.ThrowIfNull(restartCollection);
        _restartCollection = restartCollection;
    }

 
    /// <inheritdoc/>
    public void InstallCard(int id, SlotNumber slot)
    {
        if (slot == SlotNumber.Unslotted)
        {
            throw new ArgumentException("Slot must be in the range Slot1-Slot7");
        }
        UnregisterRestartable(slot);
        var card = _factory.GetCardWithId(id) ?? throw new InvalidOperationException($"Could not create a card with id {id} for slot {((int) slot)}");
        card.OnInstalled(slot);
        _cards[(int) slot] = card;
        RegisterRestartable(card);
    }
    
    /// <inheritdoc/>
    public void InstallCard(string name, SlotNumber slot)
    {
        if (slot == SlotNumber.Unslotted)
        {
            throw new ArgumentException("Slot must be in the range Slot1-Slot7");
        }
        UnregisterRestartable(slot);
        var card = _factory.GetCardWithName(name) ?? throw new InvalidOperationException($"Could not create a card with name {name} for slot {((int) slot)}");
        card.OnInstalled(slot);
        _cards[(int) slot] = card;
        RegisterRestartable(card);
    }

    private void InstallExistingCard(ICard card, SlotNumber slot)
    {
        if (slot == SlotNumber.Unslotted)
        {
            throw new ArgumentException("Slot must be in the range Slot1-Slot7");
        }
        Debug.WriteLine($"Assigning {card.Name} (Id {card.Id}) into slot {slot}.");
        card.OnInstalled(slot);
        _cards[(int) slot] = card;
    }

    /// <inheritdoc/>
    public void RemoveCard(SlotNumber slot)
    {
        UnregisterRestartable(slot);
        _cards[(int) slot] = _factory.GetNullCard() ?? throw new InvalidOperationException($"Could not create a Null Card while removing a card in slot {((int) slot)}");
    }

    /// <inheritdoc/>
    public ICard GetCardIn(SlotNumber slot)
    {
        return _cards[(int) slot];
    }

    /// <inheritdoc/>
    public bool IsEmpty(SlotNumber slot)
    {
        return GetCardIn(slot).Id == 0;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ManageC800(byte slot)
    {
        if (slot >= 1 && slot <= 7)
        {
            if (_status.StateIntCxRom)
            {
                return;
            }

            // Force C8Rom on if We're accessing slot 3 and SlotC3Rom is off
            if (slot == 3 && !_status.StateSlotC3Rom)
            {
                if (!_status.StateIntC8Rom)
                {
                    _status.SetIntC8Rom(true);
                    _status.SetIntC8RomSlot(255);
                }
            }

            // If C800Slot is zero, then possibly set it to first card accessed
            if (_status.StateIntC8RomSlot == 0)
            {
                // Only switch C8 space if slot has extended ROM
                // In a real system, not all cards respond to /IOSTROBE
                // Do a test read to see if it returns data or null.
                if (_cards[slot].ReadExtendedRom(0) != null)
                {
                    _status.SetIntC8RomSlot(slot);
                }
            }

        }
        else
        {
            // if slot == 255 then it's from a $CFFF access
            // so reset INTC8ROM to peripheral ROM
            _status.SetIntC8Rom(false);
            _status.SetIntC8RomSlot(0);
        } 
    }

    /// <summary>
    /// Reads a byte from the slots address space ($C090-$CFFF) without affecting IO Status.
    /// </summary>
    /// <param name="address">
    /// The address to read, offset by $C000. For example, to read from $C600, pass 0x0600.
    /// Valid range: 0x0090-0x0FFF.
    /// </param>

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte Peek(ushort address)
    {
        return ReadWithConditionalUpdatedIo(address, false);
    }

    /// <summary>
    /// Reads a byte from the slots address space ($C090-$CFFF).
    /// </summary>
    /// <param name="address">
    /// The address to read, offset by $C000. For example, to read from $C600, pass 0x0600.
    /// Valid range: 0x0090-0x0FFF.
    /// </param>

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte Read(ushort address)
    {
        return ReadWithConditionalUpdatedIo(address, true);
    }
    /// <summary>
    /// Reads a byte from the slots address space ($C090-$CFFF).
    /// </summary>
    /// <param name="address">
    /// The address to read, offset by $C000. For example, to read from $C600, pass 0x0600.
    /// Valid range: 0x0090-0x0FFF.
    /// </param>
    /// <param name="readAffectsIo"></param>
    /// If true, then the read affects internal state, such as INTC8ROM
    /// If false, then the read only returns a value without affecting the state

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte ReadWithConditionalUpdatedIo(ushort address, bool readAffectsIo)
    {
        // $C090-$C0FF: Card I/O space
        if (address >= 0x0090 && address <= 0x00FF)
        {
            // Determine slot from address: $C090-$C09F=slot1, $C0A0-$C0AF=slot2, etc.
            int slot = ((address >> 4) & 0x07);
            byte offset = (byte) (address & 0x0F);

            byte? cardByte = _cards[slot].ReadIO(offset);
            return cardByte ?? _floatingBus.Read();
        }

        // $C100-$C7FF: Card ROM or System ROM
        if (address >= 0x0100 && address <= 0x07FF)
        {
            
            int slot = (address >> 8) & 0x07;
            byte offset = (byte) (address & 0xFF);
            if (readAffectsIo)
            {
                ManageC800((byte) (slot));
            }

            // INTCXROM overrides SLOTCXROM and SLOTC3ROM
            if (_status.StateIntCxRom)
            {
                return _rom.Read(address);
            }

            // Determine if this slot should use card ROM or system ROM
            bool useCardRom = (slot != 3); // !_status.StateIntCxRom;

            // Special case: Slot 3 is controlled by SLOTC3ROM
            if (slot == 3)
            {
                // SLOTC3ROM = false: use internal 80-col ROM (default)
                // SLOTC3ROM = true: use card ROM (if peripheral card installed)
                useCardRom = _status.StateSlotC3Rom;
            }

            if (useCardRom)
            {
                byte? cardByte = _cards[slot].ReadRom(offset);
                return cardByte ?? _floatingBus.Read();
            }
            else
            {
                return _rom.Read(address);
            }
        }

        // $C800-$CFFF: Extended ROM 
        if (address >= 0x0800 && address <= 0x0FFF)
        {
            if (address == 0x0FFF && readAffectsIo)
            {
                ManageC800(255); // Reset C8Rom to peripheral ROM
            }

            if (_status.StateIntCxRom || _status.StateIntC8Rom)
            // INTCXROM overrides extended ROM
            //if (_status.StateIntCxRom || (BankSelect == 3 && !_status.StateSlotC3Rom))
            {
                return _rom.Read(address);
            }

            ushort offset = (ushort) (address - 0x0800);

            if (_status.StateIntC8RomSlot != 0)
            {
                byte? cardByte = _cards[_status.StateIntC8RomSlot].ReadExtendedRom(offset);
                return cardByte ?? _floatingBus.Read();
            }

            return _floatingBus.Read();
        }

        // Shouldn't reach here in normal operation ($C000-$C08F handled elsewhere)
        throw new InvalidOperationException($"ISlots.Read() called with invalid address: ${address + 0xC000:X4}");
    }

    /// <summary>
    /// Writes a byte to the slots address space ($C090-$CFFF).
    /// </summary>
    /// <param name="address">
    /// The address to write, offset by $C000. For example, to write to $C600, pass 0x0600.
    /// Valid range: 0x0090-0x0FFF.
    /// </param>
    /// <param name="val">The byte value to write.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the address is outside the valid range ($C090-$CFFF / 0x0090-0x0FFF).
    /// </exception>
  
    /// <seealso cref="Read"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(ushort address, byte val)
    {
        // $C090-$C0FF: Card I/O space
        if (address >= 0x0090 && address <= 0x00FF)
        {
            // INTCXROM overrides all card I/O and ROM
            //if (_status.StateIntCxRom)
            //{
            //    // Internal ROM enabled, write to system ROM (usually no-op)
            //    _rom.Write(address, val);
            //    return;
            //}

            // Determine slot from address: $C090-$C09F=slot1, $C0A0-$C0AF=slot2, etc.
            int slot = ((address >> 4) & 0x07);
            byte offset = (byte) (address & 0x0F);


            _cards[slot].WriteIO(offset, val);
            return;
        }

        // $C100-$C7FF: Card ROM or System ROM writes
        if (address >= 0x0100 && address <= 0x07FF)
        {
            int slot = (address >> 8) & 0x07;
            byte offset = (byte) (address & 0xFF);
            ManageC800((byte)(slot));

            // INTCXROM overrides SLOTCXROM and SLOTC3ROM
            if (_status.StateIntCxRom)
            {
                // Internal ROM enabled for entire $C100-$CFFF range
                _rom.Write(address, val);
                return;
            }

            
            // Determine if this slot should use card ROM or system ROM
            bool useCardRom = !_status.StateIntCxRom;

            // Special case: Slot 3 is controlled by SLOTC3ROM
            if (slot == 3)
            {
                useCardRom = _status.StateSlotC3Rom;
            }

            if (useCardRom)
            {
                // Card ROM enabled: ACTIVATE extended ROM and write to card
                _cards[slot].WriteRom(offset, val);
            }
            else
            {
                // System ROM enabled: DON'T change BankSelect, write to ROM (usually no-op)
                _rom.Write(address, val);
            }
            return;
        }

        // $C800-$CFFF: Extended ROM writes
        if (address >= 0x0800 && address <= 0x0FFF)
        {
            if (address == 0x0FFF)
            {
                ManageC800(255); // Reset C8Rom to peripheral ROM
            }

            // We never write to interal ROM. Pointless.
            if (_status.StateIntCxRom || _status.StateIntC8Rom)
            {
                return;
            }

            ushort offset = (ushort) (address - 0x0800);

            if (_status.StateIntC8RomSlot != 0)
            {
                _cards[_status.StateIntC8RomSlot].WriteExtendedRom(offset, val);
            }
            return;
        }

        // Shouldn't reach here in normal operation ($C000-$C08F handled elsewhere)
        throw new InvalidOperationException($"ISlots.Write() called with invalid address: ${address + 0xC000:X4}");
    }

    /// <summary>
    /// Gets the current slot configuration as JSON metadata.
    /// </summary>
    /// <returns>
    /// A JSON string containing the configuration of all installed cards and their metadata,
    /// or an empty string if serialization fails.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method captures the current state of all expansion slots, including which cards
    /// are installed and their individual configurations. The metadata uses JSON format with
    /// the following structure:
    /// </para>
    /// <code>
    /// {
    ///   "version": 1,
    ///   "slots": [
    ///     {
    ///       "slotNumber": 6,
    ///       "cardId": 1,
    ///       "cardName": "Disk II Controller",
    ///       "metadata": "{...card-specific configuration...}"
    ///     }
    ///   ]
    /// }
    /// </code>
    /// <para>
    /// <strong>Hierarchical Configuration:</strong><br/>
    /// The slots system uses the inclusive approach to hierarchical configuration. Each
    /// installed card's metadata (obtained via <see cref="IConfigurable.GetMetadata"/>) is embedded
    /// within the slot configuration. This keeps the entire peripheral configuration
    /// self-contained and portable.
    /// </para>
    /// <para>
    /// <strong>Empty Slots:</strong><br/>
    /// Empty slots (containing <see cref="NullCard"/>) are omitted from the metadata to
    /// keep it concise. On restoration, any slot not mentioned in the metadata is left empty.
    /// </para>
    /// </remarks>
    public string GetMetadata()
    {
        try
        {
            var config = new
            {
                version = 1,
                slots = Enumerable.Range(1, 7)
                    .Select(i => (SlotNumber)(i))
                    .Where(slot => !IsEmpty(slot))
                    .Select(slot =>
                    {
                        var card = GetCardIn(slot);
                        return new
                        {
                            slotNumber = (int)slot,
                            cardId = card.Id,
                            cardName = card.Name,
                            metadata = card.GetMetadata()
                        };
                    })
                    .ToArray()
            };

            return System.Text.Json.JsonSerializer.Serialize(config, s_jsonOptions);
        }
        catch
        {
            // Fail-safe: return empty string on any serialization error
            return string.Empty;
        }
    }

    /// <summary>
    /// Applies slot configuration from JSON metadata, restoring installed cards and their configurations.
    /// </summary>
    /// <param name="metadata">
    /// A JSON metadata string previously obtained from <see cref="GetMetadata"/>, or an empty
    /// string to clear all slots.
    /// </param>
    /// <returns>
    /// <c>true</c> if the configuration was successfully applied; <c>false</c> if the metadata
    /// was invalid or any card failed to apply its configuration.
    /// </returns>
    
    public bool ApplyMetadata(string metadata)
    {
        // Empty string = clear all slots
        if (string.IsNullOrWhiteSpace(metadata))
        {
            for (int i = 1; i <= 7; i++)
            {
                RemoveCard((SlotNumber)(i - 1));
            }
            return true;
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(metadata);
            var root = doc.RootElement;

            // Clear all slots first
            for (int i = 1; i <= 7; i++)
            {
                RemoveCard((SlotNumber)(i));
            }

            // Track overall success
            bool allSucceeded = true;

            // Restore each slot
            if (root.TryGetProperty("slots", out var slotsArray))
            {
                foreach (var slotConfig in slotsArray.EnumerateArray())
                {
                    if (!slotConfig.TryGetProperty("slotNumber", out var slotNumElement) ||
                        !slotConfig.TryGetProperty("cardId", out var cardIdElement))
                    {
                        allSucceeded = false;
                        continue;
                    }

                    int slotNumber = slotNumElement.GetInt32();
                    int cardId = cardIdElement.GetInt32();

                    if (slotNumber < 1 || slotNumber > 7)
                    {
                        allSucceeded = false;
                        continue;
                    }

                    var slot = (SlotNumber)(slotNumber);

                    try
                    {
                        // Install the card by ID
                        InstallCard(cardId, slot);

                        // Apply card-specific metadata if present
                        if (slotConfig.TryGetProperty("metadata", out var metadataElement))
                        {
                            string cardMetadata = metadataElement.GetString() ?? string.Empty;
                            var card = GetCardIn(slot);

                            if (!card.ApplyMetadata(cardMetadata))
                            {
                                allSucceeded = false;
                            }
                        }
                    }
                    catch
                    {
                        allSucceeded = false;
                    }
                }
            }


            return allSucceeded;
        }
        catch
        {
            // JSON parsing or other error - return false
            return false;
        }
    }

    public void Reset()
    {
        _status.SetIntC8Rom(false);
        _status.SetIntC8RomSlot(0);

        foreach (var card in _cards)
        {
            card.Reset();
        }
    }

    /// <summary>
    /// Registers a card in the <see cref="RestartCollection"/> if it implements
    /// <see cref="IRestartable"/>.
    /// </summary>
    private void RegisterRestartable(ICard card)
    {
        if (_restartCollection != null && card is IRestartable restartable)
        {
            _restartCollection.Register(restartable);
        }
    }

    /// <summary>
    /// Unregisters the card currently in <paramref name="slot"/> from the
    /// <see cref="RestartCollection"/> if it implements <see cref="IRestartable"/>.
    /// </summary>
    private void UnregisterRestartable(SlotNumber slot)
    {
        var existing = _cards[(int)slot];
        if (_restartCollection != null && existing is IRestartable restartable)
        {
            _restartCollection.Unregister(restartable);
        }
    }

}
