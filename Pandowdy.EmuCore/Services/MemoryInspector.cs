// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.Services;

/// <summary>
/// Provides comprehensive read-only access to all memory regions for debugging and display.
/// </summary>
/// <remarks>
/// <para>
/// This class implements <see cref="IMemoryInspector"/> to provide unified access to RAM, ROM,
/// and slot card memory for debuggers, memory viewers, and diagnostic tools.
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> All read operations are thread-safe. ROM data is immutable
/// after loading, and RAM reads are atomic byte operations. May show mid-instruction state
/// which is acceptable for display purposes.
/// </para>
/// </remarks>
public sealed class MemoryInspector(
    IDirectMemoryPoolReader memoryPool,
    ISystemRomProvider systemRom,
    ISlots slots,
    ILanguageCard languageCard,
    ISystemStatusProvider systemStatus) : IMemoryInspector
{
    private readonly IDirectMemoryPoolReader _memoryPool = memoryPool ?? throw new ArgumentNullException(nameof(memoryPool));
    private readonly ISystemRomProvider _systemRom = systemRom ?? throw new ArgumentNullException(nameof(systemRom));
    private readonly ISlots _slots = slots ?? throw new ArgumentNullException(nameof(slots));
    private readonly ILanguageCard _languageCard = languageCard ?? throw new ArgumentNullException(nameof(languageCard));
    private readonly ISystemStatusProvider _status = systemStatus ?? throw new ArgumentNullException(nameof(systemStatus));

    #region IDirectMemoryPoolReader Implementation (Delegation)

    /// <inheritdoc />
    public byte ReadRawMain(int address) => _memoryPool.ReadRawMain(address);

    /// <inheritdoc />
    public byte ReadRawAux(int address) => _memoryPool.ReadRawAux(address);

    #endregion

    #region System ROM Access

    /// <inheritdoc />
    public byte ReadSystemRom(int address)
    {
        // ROM readable from $C100-$FFFF only
        // $C000-$C0FF is I/O space and cannot be read through this method
        if (address < 0xC100 || address > 0xFFFF)
        {
            return 0;
        }

        // ROM stored at offsets 0x0000-0x3FFF (representing $C000-$FFFF)
        return _systemRom.Read((ushort)(address - 0xC000));
    }

    /// <inheritdoc />
    public byte ReadActiveHighMemory(int address)
    {
        // Only handle $C100-$FFFF
        // $C000-$C0FF is I/O space and cannot be read through this method
        if (address < 0xC100 || address > 0xFFFF)
        {
            return 0;
        }

        // $D000-$FFFF: Language Card area (RAM or ROM based on HIGHREAD)
        if (address >= 0xD000)
        {
            return _languageCard.Read((ushort)address);
        }

        // $C100-$CFFF: Internal ROM or slot ROM based on soft switches
        return ReadActiveSlotRomArea(address);
    }

    /// <summary>
    /// Reads from the $C100-$CFFF area based on current soft switch settings.
    /// </summary>
    /// <remarks>
    /// Uses direct card ROM access to avoid side effects (e.g., $CFFF clearing IntC8Rom ownership).
    /// Falls back to internal ROM when cards don't provide ROM at the requested address.
    /// </remarks>
    private byte ReadActiveSlotRomArea(int address)
    {
        // $C800-$CFFF: Extended ROM area
        if (address >= 0xC800)
        {
            if (_status.StateIntCxRom || _status.StateIntC8Rom)
            {
                // Internal extended ROM
                return _systemRom.Read((ushort)(address - 0xC000));
            }
            else
            {
                // Slot extended ROM - read directly from card to avoid side effects
                // StateIntC8RomSlot tells us which slot owns the C8 space
                int ownerSlot = _status.StateIntC8RomSlot;
                if (ownerSlot >= 1 && ownerSlot <= 7)
                {
                    var card = _slots.GetCardIn((SlotNumber)(ownerSlot - 1));
                    if (card.Id != 0)
                    {
                        // Use direct ROM read - offset is $C800-$CFFF → 0x000-0x7FF
                        var value = card.ReadExtendedRom((ushort)(address - 0xC800));
                        if (value.HasValue)
                        {
                            return value.Value;
                        }
                    }
                }
                // No valid slot owns C8 or card returned null, fall back to internal ROM
                return _systemRom.Read((ushort)(address - 0xC000));
            }
        }

        // $C300-$C3FF: Special handling for slot 3 (80-column card)
        if (address >= 0xC300 && address < 0xC400)
        {
            if (_status.StateIntCxRom || !_status.StateSlotC3Rom)
            {
                // Internal ROM at $C300-$C3FF
                return _systemRom.Read((ushort)(address - 0xC000));
            }
            else
            {
                // Slot 3 ROM - read directly from card, fall back to internal ROM
                var card = _slots.GetCardIn(SlotNumber.Slot3);
                if (card.Id != 0)
                {
                    var value = card.ReadRom((byte)(address & 0xFF));
                    if (value.HasValue)
                    {
                        return value.Value;
                    }
                }
                // Card empty or returned null, use internal ROM
                return _systemRom.Read((ushort)(address - 0xC000));
            }
        }

        // $C100-$C2FF, $C400-$C7FF: INTCXROM controls
        if (_status.StateIntCxRom)
        {
            // Internal ROM
            return _systemRom.Read((ushort)(address - 0xC000));
        }
        else
        {
            // Slot ROM - read directly from card to avoid side effects
            int slot = (address >> 8) & 0x07; // Extract slot number from address
            if (slot >= 1 && slot <= 7)
            {
                var card = _slots.GetCardIn((SlotNumber)(slot - 1));
                if (card.Id != 0)
                {
                    var value = card.ReadRom((byte)(address & 0xFF));
                    if (value.HasValue)
                    {
                        return value.Value;
                    }
                }
            }
            // Card empty or returned null, fall back to internal ROM
            return _systemRom.Read((ushort)(address - 0xC000));
        }
    }

    #endregion

    #region Slot ROM Access

    /// <inheritdoc />
    /// <remarks>
    /// Uses direct card ROM access via <see cref="ICard.ReadRom"/> to avoid triggering
    /// any side effects that would occur through the normal bus read path.
    /// </remarks>
    public byte ReadSlotRom(int slot, int offset)
    {
        // Validate slot number (1-7)
        if (slot < 1 || slot > 7)
        {
            return 0;
        }

        // Validate offset (0x00-0xFF for 256-byte slot ROM)
        if (offset < 0 || offset > 0xFF)
        {
            return 0;
        }

        // Read directly from the card to avoid any side effects
        var card = _slots.GetCardIn((SlotNumber)(slot - 1));

        // If slot is empty, return 0
        if (card.Id == 0)
        {
            return 0;
        }

        // Direct ROM read bypasses bus mapping and side effects
        var value = card.ReadRom((byte)offset);
        return value ?? 0;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Uses direct card ROM access via <see cref="ICard.ReadExtendedRom"/> to avoid triggering
    /// any side effects that would occur through the normal bus read path.
    /// </remarks>
    public byte ReadSlotExtendedRom(int slot, int offset)
    {
        // Validate slot number (1-7)
        if (slot < 1 || slot > 7)
        {
            return 0;
        }

        // Validate offset (0x000-0x7FF for 2KB extended ROM)
        if (offset < 0 || offset > 0x7FF)
        {
            return 0;
        }

        // Read directly from the card to avoid any side effects
        var card = _slots.GetCardIn((SlotNumber)(slot - 1));

        // If slot is empty, return 0
        if (card.Id == 0)
        {
            return 0;
        }

        // Direct extended ROM read bypasses bus mapping and side effects
        var value = card.ReadExtendedRom((ushort)offset);
        return value ?? 0;
    }

    #endregion

    #region Bulk Read Operations

    /// <inheritdoc />
    public byte[] ReadMainBlock(int startAddress, int length)
    {
        if (length <= 0)
        {
            return [];
        }

        var result = new byte[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = _memoryPool.ReadRawMain((startAddress + i) & 0xFFFF);
        }
        return result;
    }

    /// <inheritdoc />
    public byte[] ReadAuxBlock(int startAddress, int length)
    {
        if (length <= 0)
        {
            return [];
        }

        var result = new byte[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = _memoryPool.ReadRawAux((startAddress + i) & 0xFFFF);
        }
        return result;
    }

    #endregion
}
