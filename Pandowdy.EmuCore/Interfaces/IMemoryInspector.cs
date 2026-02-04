// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.EmuCore.Interfaces;

/// <summary>
/// Provides comprehensive read-only access to all memory regions for debugging and display.
/// </summary>
/// <remarks>
/// <para>
/// This interface extends <see cref="IDirectMemoryPoolReader"/> to provide access to ROM regions
/// in addition to RAM. It serves as the primary memory inspection interface for debuggers,
/// memory viewers, and other diagnostic tools.
/// </para>
/// <para>
/// <strong>Memory Regions Accessible:</strong>
/// <list type="bullet">
/// <item><strong>Main RAM ($0000-$FFFF):</strong> Full 64KB main memory bank via <see cref="IDirectMemoryPoolReader.ReadRawMain"/></item>
/// <item><strong>Aux RAM ($0000-$FFFF):</strong> Full 64KB auxiliary memory bank via <see cref="IDirectMemoryPoolReader.ReadRawAux"/></item>
/// <item><strong>System ROM ($C100-$FFFF):</strong> Internal firmware, Monitor ROM, and Applesoft BASIC</item>
/// <item><strong>Active High Memory ($C100-$FFFF):</strong> Whatever is currently mapped based on soft switches</item>
/// </list>
/// </para>
/// <para>
/// <strong>Language Card RAM ($C000-$FFFF in RAM):</strong> The language card RAM is stored within the 
/// main and auxiliary RAM banks. Use <see cref="IDirectMemoryPoolReader.ReadRawMain"/> or
/// <see cref="IDirectMemoryPoolReader.ReadRawAux"/> with addresses $C000-$FFFF to access it directly.
/// <list type="bullet">
/// <item>$C000-$CFFF in RAM: Physical storage for Language Card bank 1 ($D000-$DFFF when bank 1 is active)</item>
/// <item>$D000-$DFFF in RAM: Physical storage for Language Card bank 2 ($D000-$DFFF when bank 2 is active)</item>
/// <item>$E000-$FFFF in RAM: Common area shared by both banks</item>
/// </list>
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> All read operations are thread-safe. ROM data is immutable
/// after loading, and RAM reads are atomic byte operations.
/// </para>
/// <para>
/// <strong>Excluded Regions ($C000-$C0FF) for ROM methods:</strong> The I/O space cannot be read through
/// <see cref="ReadSystemRom"/> or <see cref="ReadActiveHighMemory"/>. Reading $C000-$C0FF would trigger
/// soft switch side effects or require hardware interaction. Current soft switch state can be obtained
/// via <see cref="ISystemStatusProvider"/>. Note: <see cref="IDirectMemoryPoolReader.ReadRawMain"/> and
/// <see cref="IDirectMemoryPoolReader.ReadRawAux"/> <em>can</em> read from $C000-$C0FF as they access 
/// the physical RAM (Language Card bank 1 storage), not the I/O space.
/// </para>
/// </remarks>
public interface IMemoryInspector : IDirectMemoryPoolReader
{
    #region System ROM Access

    /// <summary>
    /// Reads a byte from the system ROM.
    /// </summary>
    /// <param name="address">Absolute address in ROM space ($C100-$FFFF).</param>
    /// <returns>Byte value from ROM, or 0 if address is outside $C100-$FFFF.</returns>
    /// <remarks>
    /// <para>
    /// Reads directly from the loaded system ROM image, bypassing all soft switch mapping.
    /// This always returns the ROM contents regardless of language card or slot ROM settings.
    /// </para>
    /// <para>
    /// <strong>Valid Address Ranges:</strong>
    /// <list type="bullet">
    /// <item>$C100-$C7FF: Internal peripheral ROM (1792 bytes, 7 × 256 bytes)</item>
    /// <item>$C800-$CFFF: Extended internal ROM (2KB)</item>
    /// <item>$D000-$DFFF: Monitor ROM (4KB)</item>
    /// <item>$E000-$FFFF: Applesoft BASIC + vectors (8KB)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Returns 0 for:</strong> Addresses below $C100 or above $FFFF. The $C000-$C0FF
    /// range is I/O space and cannot be read through this method.
    /// </para>
    /// </remarks>
    byte ReadSystemRom(int address);

    /// <summary>
    /// Reads a byte from the currently active ROM/RAM at the specified high memory address.
    /// </summary>
    /// <param name="address">Absolute address ($C100-$FFFF).</param>
    /// <returns>Byte value from the active ROM/RAM based on current soft switch settings.</returns>
    /// <remarks>
    /// <para>
    /// Returns what the CPU would read if it accessed this address, considering:
    /// <list type="bullet">
    /// <item><strong>$C100-$CFFF:</strong> Internal ROM or slot ROM based on INTCXROM/SLOTC3ROM</item>
    /// <item><strong>$D000-$FFFF:</strong> Language Card RAM or system ROM based on HIGHREAD</item>
    /// </list>
    /// </para>
    /// <para>
    /// For addresses below $C100, returns 0 (use MemoryInspector for RAM, SystemStatus for I/O).
    /// </para>
    /// </remarks>
    byte ReadActiveHighMemory(int address);

    #endregion

    #region Slot ROM Access

    /// <summary>
    /// Reads a byte from a specific slot's ROM area ($Cn00-$CnFF).
    /// </summary>
    /// <param name="slot">Slot number (1-7).</param>
    /// <param name="offset">Offset within the slot's 256-byte ROM space (0x00-0xFF).</param>
    /// <returns>Byte value from the slot ROM, or 0 if slot is empty or has no ROM.</returns>
    /// <remarks>
    /// <para>
    /// Reads from the slot's dedicated ROM space, bypassing INTCXROM settings.
    /// Always reads from the slot card's ROM if present, regardless of current mapping.
    /// </para>
    /// <para>
    /// Returns 0 for empty slots, invalid slot numbers, or cards without ROM.
    /// </para>
    /// </remarks>
    byte ReadSlotRom(int slot, int offset);

    /// <summary>
    /// Reads a byte from a specific slot's extended ROM area ($C800-$CFFF).
    /// </summary>
    /// <param name="slot">Slot number (1-7).</param>
    /// <param name="offset">Offset within the 2KB extended ROM space (0x000-0x7FF).</param>
    /// <returns>Byte value from the slot's extended ROM, or 0 if not available.</returns>
    /// <remarks>
    /// <para>
    /// Reads from the slot's extended ROM, bypassing the current C8 ROM ownership tracking.
    /// This allows inspecting any slot's extended ROM regardless of which slot currently
    /// owns the $C800-$CFFF space.
    /// </para>
    /// <para>
    /// Returns 0 for empty slots, invalid slot numbers, cards without extended ROM,
    /// or if offset is outside the valid range (0x000-0x7FF).
    /// </para>
    /// </remarks>
    byte ReadSlotExtendedRom(int slot, int offset);

    #endregion

    #region Bulk Read Operations

    /// <summary>
    /// Reads a contiguous block of bytes from main memory.
    /// </summary>
    /// <param name="startAddress">Starting address (0x0000-0xFFFF).</param>
    /// <param name="length">Number of bytes to read.</param>
    /// <returns>Array containing the requested bytes. Addresses wrap at 64KB boundary.</returns>
    byte[] ReadMainBlock(int startAddress, int length);

    /// <summary>
    /// Reads a contiguous block of bytes from auxiliary memory.
    /// </summary>
    /// <param name="startAddress">Starting address (0x0000-0xFFFF).</param>
    /// <param name="length">Number of bytes to read.</param>
    /// <returns>Array containing the requested bytes. Addresses wrap at 64KB boundary.</returns>
    byte[] ReadAuxBlock(int startAddress, int length);

    #endregion
}
