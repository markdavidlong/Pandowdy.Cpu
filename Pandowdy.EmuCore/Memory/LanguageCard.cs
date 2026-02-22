// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Runtime.CompilerServices;
using Pandowdy.EmuCore.IO;
using Pandowdy.EmuCore.Machine;

namespace Pandowdy.EmuCore.Memory;

/// <summary>
/// Manages the Apple IIe Language Card memory banking for the $D000-$FFFF address space.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Purpose:</strong> Implements the Language Card banking mechanism that allows
/// 16KB of RAM to be mapped into the normally ROM-occupied address space ($D000-$FFFF),
/// with two switchable 4KB banks for $D000-$DFFF and a shared 8KB region for $E000-$FFFF.
/// </para>
/// <para>
/// <strong>Memory Layout:</strong>
/// <code>
/// Apple IIe Address Space:
/// $D000-$DFFF (4KB) - Bank 1 or Bank 2 (switchable)
/// $E000-$FFFF (8KB) - Common area (shared between banks)
/// 
/// Physical RAM Layout (16KB total):
/// Bank 1: $0000-$0FFF (4KB) + $2000-$3FFF (8KB) = 12KB
/// Bank 2: $1000-$3FFF (12KB)
/// </code>
/// </para>
/// <para>
/// <strong>Banking Control:</strong> The Language Card reads state from
/// <see cref="ISystemStatusProvider"/> to determine:
/// <list type="bullet">
/// <item><see cref="ISystemStatusProvider.StateHighRead"/> - Read from RAM vs ROM</item>
/// <item><see cref="ISystemStatusProvider.StateHighWrite"/> - RAM write enable</item>
/// <item><see cref="ISystemStatusProvider.StateUseBank1"/> - Bank 1 vs Bank 2 selection</item>
/// <item><see cref="ISystemStatusProvider.StateRamRd"/> - Main vs auxiliary memory for reads</item>
/// <item><see cref="ISystemStatusProvider.StateRamWrt"/> - Main vs auxiliary memory for writes</item>
/// </list>
/// </para>
/// <para>
/// <strong>Design Philosophy:</strong> This class is a pure memory accessor that queries
/// current state flags but does not manage state transitions. State management is handled
/// by the soft switch controller, which updates the <see cref="ISystemStatusProvider"/>.
/// This separation provides clean architecture, optimal performance, and excellent testability.
/// </para>
/// <para>
/// <strong>Performance:</strong> All read/write methods use aggressive inlining for maximum
/// performance in this hot path (called hundreds of thousands of times per second).
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> Not thread-safe. Assumes single-threaded CPU execution.
/// The <see cref="ISystemStatusProvider"/> should handle any necessary synchronization.
/// </para>
/// </remarks>
[Capability(typeof(IRestartable))]
public class LanguageCard(
    ISystemRam mainRam,
    ISystemRam? auxRam,
    ISystemRomProvider systemRom, 
    IFloatingBusProvider floatingBus,
    ISystemStatusProvider status) : ILanguageCard
{
    /// <summary>
    /// Required size for RAM banks (16KB total).
    /// </summary>
    private const int RequiredRamSize = 0x4000; // 16KB
    
    /// <summary>
    /// Required size for system ROM (16KB, $C000-$FFFF).
    /// </summary>
    private const int RequiredRomSize = 0x4000; // 16KB

    private readonly ISystemRam _mainRam = Utility.ValidateIPandowdyMemorySize(mainRam, nameof(mainRam), RequiredRamSize);
    private readonly ISystemRam? _auxRam = auxRam != null ? Utility.ValidateIPandowdyMemorySize(auxRam, nameof(auxRam), RequiredRamSize) : null;
    private readonly ISystemRomProvider _systemRom = Utility.ValidateIPandowdyMemorySize(systemRom, nameof(systemRom), RequiredRomSize);
    private readonly IFloatingBusProvider _floatingBus = floatingBus ?? throw new ArgumentNullException(nameof(floatingBus));
    private readonly ISystemStatusProvider _status = status ?? throw new ArgumentNullException(nameof(status));

    /// <summary>
    /// Gets the size of the Language Card address space (12KB).
    /// </summary>
    /// <value>
    /// Always returns 0x3000 (12,288 bytes), representing the $D000-$FFFF address range.
    /// </value>
    /// <remarks>
    /// Note: The Language Card uses 16KB of physical RAM, but only 12KB is addressable
    /// at any given time due to bank switching. The returned size matches the accessible
    /// address space ($D000-$FFFF).
    /// </remarks>
    public int Size => 0x3000; // 12KB addressable space

    /// <summary>
    /// Restores the Language Card RAM to its initial power-on state by clearing both banks.
    /// </summary>
    /// <remarks>
    /// Clears main 16KB and auxiliary 16KB Language Card RAM to zero. Banking state
    /// (HighRead, HighWrite, Bank1, PreWrite) is reset by <see cref="SoftSwitches.ResetAllSwitches"/>
    /// which sets all switches to false — HighRead=false means ROM is active (correct power-on default).
    /// </remarks>
    public void Restart()
    {
        _mainRam.Clear();
        _auxRam?.Clear();
    }

    /// <summary>
    /// Maps a Language Card address ($D000-$FFFF) to the physical RAM address (0x0000-0x3FFF).
    /// </summary>
    /// <param name="address">
    /// Address in the Language Card space, typically $D000-$FFFF but accepts $C000-$FFFF
    /// for calculation convenience.
    /// </param>
    /// <returns>Physical address within the 16KB RAM array (0x0000-0x3FFF).</returns>
    /// <remarks>
    /// <para>
    /// <strong>Bank 2 (default):</strong> Maps $D000-$FFFF to $1000-$3FFF (12KB contiguous).
    /// </para>
    /// <para>
    /// <strong>Bank 1:</strong> Maps $D000-$DFFF to $0000-$0FFF (4KB) and $E000-$FFFF 
    /// to $2000-$3FFF (8KB), leaving a 4KB gap at $1000-$1FFF.
    /// </para>
    /// <para>
    /// <strong>Algorithm:</strong>
    /// <code>
    /// 1. Subtract $C000 from address (assuming Bank 2)
    /// 2. If Bank 1 and address &lt; $E000, subtract additional $1000
    /// </code>
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> Aggressively inlined for zero overhead in read/write paths.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ushort DetermineMappedRamAddress(ushort address)
    {
        ushort actualAddress = (ushort)(address - 0xC000);

        if (_status.StateUseBank1 && address < 0xE000)
        {
            actualAddress -= 0x1000;
        }
        return actualAddress;
    }




    /// <summary>
    /// Maps a Language Card address ($D000-$FFFF) to the ROM address (0x1000-0x3FFF).
    /// </summary>
    /// <param name="address">Address in the $D000-$FFFF range.</param>
    /// <returns>ROM address offset (0x1000-0x3FFF).</returns>
    /// <remarks>
    /// <para>
    /// <strong>Mapping:</strong> The ROM provider spans $C000-$FFFF (16KB), so $D000
    /// maps to offset 0x1000 within the ROM.
    /// </para>
    /// <para>
    /// <strong>Calculation:</strong> romOffset = address - $C000
    /// <code>
    /// $D000 -> 0x1000 (offset within 16KB ROM)
    /// $E000 -> 0x2000
    /// $FFFF -> 0x3FFF
    /// </code>
    /// </para>
    /// <para>
    /// <strong>ROM Layout:</strong>
    /// <code>
    /// ROM $C000-$CFFF (0x0000-0x0FFF) - I/O firmware, peripheral ROM
    /// ROM $D000-$DFFF (0x1000-0x1FFF) - Monitor ROM / Language Card bank area
    /// ROM $E000-$FFFF (0x2000-0x3FFF) - Applesoft BASIC + reset vector
    /// </code>
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> Static method, aggressively inlined for optimal performance.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort DetermineMappedRomAddress(ushort address)
    {
        return (ushort)(address - 0xC000);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte Peek(ushort address) => Read(address);

    /// <summary>
    /// Reads a byte from the Language Card address space.
    /// </summary>
    /// <param name="address">
    /// Address to read from, typically in the $D000-$FFFF range (though any 16-bit
    /// address is accepted for calculation purposes).
    /// </param>
    /// <returns>Byte value from RAM, ROM, or floating bus, depending on system state.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Read Decision Logic:</strong>
    /// <code>
    /// If StateHighRead is true:
    ///   If StateRamRd is true:
    ///     Read from auxiliary RAM (or floating bus if aux doesn't exist)
    ///   Else:
    ///     Read from main RAM
    /// Else:
    ///   Read from system ROM
    /// </code>
    /// </para>
    /// <para>
    /// <strong>Floating Bus:</strong> If auxiliary memory is accessed but doesn't exist,
    /// returns the floating bus value instead. This models Apple IIe hardware behavior
    /// where unmapped memory reads return the last value on the data bus.
    /// </para>
    /// <para>
    /// <strong>Bank Selection:</strong> The <see cref="ISystemStatusProvider.StateUseBank1"/> flag determines
    /// which 4KB bank is accessed for $D000-$DFFF. The $E000-$FFFF region is shared.
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> Aggressively inlined. Read from main RAM with ROM
    /// fallback is the common path and executes in ~7-10 cycles after inlining.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte Read(ushort address)
    {
        if (_status.StateHighRead)
        {
            IPandowdyMemory? targetMemory =  _status.StateAltZp ? _auxRam : _mainRam;

            return targetMemory != null 
                ? targetMemory.Read(DetermineMappedRamAddress(address))
                : _floatingBus.Read();
        }

        return _systemRom.Read(DetermineMappedRomAddress(address));
    }

    /// <summary>
    /// Writes a byte to the Language Card address space.
    /// </summary>
    /// <param name="address">
    /// Address to write to, typically in the $D000-$FFFF range (though any 16-bit
    /// address is accepted for calculation purposes).
    /// </param>
    /// <param name="data">Byte value to write.</param>
    /// <remarks>
    /// <para>
    /// <strong>Write Protection:</strong> Writes are only executed if
    /// <see cref="ISystemStatusProvider.StateHighWrite"/> is true. The Language Card
    /// uses a two-access sequence to enable writing, preventing accidental ROM overwrites.
    /// </para>
    /// <para>
    /// <strong>Write Decision Logic:</strong>
    /// <code>
    /// If StateHighWrite is false:
    ///   Return (no-op, ROM is write-protected)
    /// 
    /// If StateRamWrt is true:
    ///   Write to auxiliary RAM (or ignore if aux doesn't exist)
    /// Else:
    ///   Write to main RAM
    /// </code>
    /// </para>
    /// <para>
    /// <strong>Bank Selection:</strong> The <see cref="ISystemStatusProvider.StateUseBank1"/> flag determines
    /// which 4KB bank is written to for $D000-$DFFF. The $E000-$FFFF region is shared.
    /// </para>
    /// <para>
    /// <strong>ROM Write Attempts:</strong> Writes when StateHighWrite is false are
    /// silently ignored (no-op), matching Apple IIe hardware behavior where ROM writes
    /// have no effect.
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> Aggressively inlined. Write protection check is
    /// the fast path (early return), keeping overhead minimal.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(ushort address, byte data)
    {
        if (!_status.StateHighWrite)
        {
            return;
        }
        
        IPandowdyMemory? targetMemory = _status.StateAltZp ? _auxRam : _mainRam;

        if (targetMemory != null)
        {
            targetMemory.Write(DetermineMappedRamAddress(address), data);
        }
    }
}
