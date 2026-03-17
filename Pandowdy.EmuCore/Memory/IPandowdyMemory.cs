// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details


namespace Pandowdy.EmuCore.Memory;

/// <summary>
/// Defines the interface for memory access in the Pandowdy emulator.
/// </summary>
/// <remarks>
/// <para>
/// This interface provides both normal memory access (with side effects) and
/// side-effect-free inspection capabilities for debugging and GUI purposes.
/// </para>
/// <para>
/// <strong>Read vs. Peek:</strong>
/// <list type="bullet">
/// <item><strong>Read:</strong> Normal CPU memory access that may trigger I/O operations,
/// soft switch changes, or other side effects (e.g., reading $C000 returns keyboard state,
/// reading $C010 clears keyboard strobe).</item>
/// <item><strong>Peek:</strong> Side-effect-free inspection that returns memory contents
/// without triggering any hardware behavior. Used by debuggers, memory viewers, and GUI
/// displays to observe system state without disturbing emulation.</item>
/// </list>
/// </para>
/// </remarks>
public interface IPandowdyMemory
{
    /// <summary>
    /// Gets the size of the memory region in bytes.
    /// </summary>
    int Size { get; }

    /// <summary>
    /// Reads a byte from the specified address with normal CPU semantics.
    /// </summary>
    /// <param name="address">The memory address to read from.</param>
    /// <returns>The byte value at the specified address.</returns>
    /// <remarks>
    /// <para>
    /// This method performs a normal CPU read operation that may have side effects:
    /// <list type="bullet">
    /// <item>Reading from I/O addresses ($C000-$C0FF) may trigger hardware operations
    /// (e.g., clearing keyboard strobe, accessing disk controller, reading paddles)</item>
    /// <item>Reading from soft switch addresses may change memory bank mappings or video modes</item>
    /// <item>Reading from peripheral cards may trigger card-specific behavior</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Use For:</strong> Normal CPU emulation, instruction execution
    /// </para>
    /// </remarks>
    byte Read(ushort address);

    /// <summary>
    /// Reads a byte from the specified address without side effects (out-of-band inspection).
    /// </summary>
    /// <param name="address">The memory address to peek at.</param>
    /// <returns>The byte value at the specified address, or a safe default if unavailable.</returns>
    /// <remarks>
    /// <para>
    /// This method provides side-effect-free memory inspection for debugging, GUI displays,
    /// and other external observers. Unlike <see cref="Read"/>, this method:
    /// <list type="bullet">
    /// <item>Does NOT trigger I/O operations</item>
    /// <item>Does NOT modify soft switch states</item>
    /// <item>Does NOT affect peripheral card behavior</item>
    /// <item>Does NOT clear strobes or latches</item>
    /// <item>Does NOT advance hardware state machines</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Implementation Guidelines:</strong>
    /// <list type="bullet">
    /// <item><strong>RAM/ROM:</strong> Return the byte value directly (same as Read)</item>
    /// <item><strong>I/O Space ($C000-$C0FF):</strong> Return current state without side effects
    /// (e.g., for keyboard at $C000, return current latch value without clearing strobe)</item>
    /// <item><strong>Peripheral Cards:</strong> Call card's peek/inspect method if available,
    /// or return safe default value (typically 0x00 or last bus value)</item>
    /// <item><strong>Unmapped Space:</strong> Return 0x00 or safe default</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Use Cases:</strong>
    /// <list type="bullet">
    /// <item>Memory viewer/hex dump panels in debugger</item>
    /// <item>Watch expressions monitoring memory addresses</item>
    /// <item>GUI status panels displaying I/O state (disk status, keyboard latch)</item>
    /// <item>Disassembly views reading instruction bytes</item>
    /// <item>Memory search/compare operations</item>
    /// <item>State snapshot/save operations</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> Implementations should be thread-safe for reads
    /// from external threads (e.g., UI thread inspecting memory while emulator thread runs).
    /// </para>
    /// <para>
    /// <strong>Example:</strong>
    /// <code>
    /// // Debugger hex viewer - safe to call repeatedly without side effects
    /// for (ushort addr = 0x0800; addr &lt; 0x0C00; addr++)
    /// {
    ///     byte value = memory.Peek(addr);
    ///     Console.Write($"{value:X2} ");
    /// }
    /// 
    /// // CPU execution - may have side effects
    /// byte keyboardState = memory.Read(0xC000); // Reads keyboard, strobe remains
    /// byte clearStrobe = memory.Read(0xC010);   // Clears keyboard strobe (side effect!)
    /// </code>
    /// </para>
    /// </remarks>
    byte Peek(ushort address);

    /// <summary>
    /// Writes a byte to the specified address.
    /// </summary>
    /// <param name="address">The memory address to write to.</param>
    /// <param name="data">The byte value to write.</param>
    /// <remarks>
    /// This method performs a normal CPU write operation that may trigger hardware side effects
    /// such as soft switch changes, I/O operations, or peripheral card behavior.
    /// </remarks>
    void Write(ushort address, byte data);
}

