// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Runtime.CompilerServices;

namespace Pandowdy.EmuCore.DataTypes;

/// <summary>
/// Provides bit-level access to a 16-bit unsigned integer value.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Purpose:</strong> This struct wraps a <see cref="ushort"/> and provides
/// methods to get and set individual bits by index (0-15). This is useful for
/// managing packed bit flags, hardware registers, or bitmap data where individual
/// bits have specific meanings.
/// </para>
/// <para>
/// <strong>Value Type Semantics:</strong> As a struct, <see cref="BitField16"/> is
/// a value type with copy-by-value semantics. Each assignment creates an independent
/// copy. This is appropriate for small data structures like this where reference
/// overhead would be wasteful.
/// </para>
/// <para>
/// <strong>Bit Indexing:</strong> Bits are indexed from 0 (LSB, least significant bit)
/// to 15 (MSB, most significant bit), matching standard binary digit positions:
/// <code>
/// Bit:   15 14 13 12 11 10  9  8  7  6  5  4  3  2  1  0
/// Value:  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  1  = 0x0001
/// </code>
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> Not thread-safe for writes. If multiple threads
/// need to modify the same <see cref="BitField16"/> instance, external synchronization
/// is required. Reads via <see cref="GetBit"/> are safe as long as no concurrent writes occur.
/// </para>
/// <para>
/// <strong>Usage in Pandowdy:</strong> Used by <see cref="Video.BitmapDataArray"/> to
/// manage per-pixel flags across 16 bitplanes, enabling efficient packed storage
/// of multiple boolean attributes per pixel (e.g., color, intensity, visibility).
/// </para>
/// </remarks>
public struct BitField16
{
    /// <summary>
    /// The underlying 16-bit unsigned integer value.
    /// </summary>
    private ushort _value;

    /// <summary>
    /// Gets or sets the complete 16-bit value.
    /// </summary>
    /// <value>
    /// The current 16-bit value with all bits packed together.
    /// </value>
    /// <remarks>
    /// <para>
    /// <strong>Direct Access:</strong> This property allows direct manipulation of
    /// the entire 16-bit value without going through bit-by-bit operations. Useful
    /// when initializing, copying, or comparing entire bitfields.
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> More efficient than setting 16 individual bits
    /// when you need to initialize or bulk-update the entire value.
    /// </para>
    /// </remarks>
    public ushort Value
    {
        readonly get => _value;
        set => _value = value;
    }

    /// <summary>
    /// Gets the bit width of the underlying value (always 16).
    /// </summary>
    /// <remarks>
    /// Static property that returns <c>sizeof(ushort) * 8 = 16</c>. Used internally
    /// for bounds checking in <see cref="CheckIndex"/>. Calculating via sizeof()
    /// allows the code to be self-documenting and portable (though ushort is always 16 bits).
    /// </remarks>
    private static int BitWidth => sizeof(ushort) * 8;

    /// <summary>
    /// Validates that a bit index is within the valid range (0-15).
    /// </summary>
    /// <param name="index">The bit index to validate.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if <paramref name="index"/> is less than 0 or greater than 15.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <strong>Bounds Checking:</strong> Uses unsigned comparison <c>(uint)index >= BitWidth</c>
    /// which simultaneously checks both lower bound (negative becomes large unsigned) and
    /// upper bound (>= 16). This is a common optimization pattern in .NET for single-comparison
    /// bounds checking.
    /// </para>
    /// <para>
    /// <strong>Error Message:</strong> Provides clear error message indicating the invalid
    /// index and the valid range (0-15 bits).
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CheckIndex(int index)
    {
        if ((uint)index >= BitWidth)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"Bit index {index} is outside the width of ({BitWidth} bits.");
        }
    }

    /// <summary>
    /// Gets the value of a specific bit.
    /// </summary>
    /// <param name="index">The zero-based index of the bit to retrieve (0-15).</param>
    /// <returns>
    /// <c>true</c> if the bit at <paramref name="index"/> is set (1);
    /// <c>false</c> if the bit is clear (0).
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if <paramref name="index"/> is less than 0 or greater than 15.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <strong>Algorithm:</strong>
    /// <list type="number">
    /// <item>Validates index is in range 0-15</item>
    /// <item>Creates a mask with only the target bit set: <c>1 &lt;&lt; index</c></item>
    /// <item>Performs bitwise AND with the value: <c>_value &amp; mask</c></item>
    /// <item>Returns true if result is non-zero (bit is set)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Example:</strong>
    /// <code>
    /// var bf = new BitField16 { Value = 0b0000_0000_0000_1010 }; // bits 1 and 3 set
    /// bool bit1 = bf.GetBit(1);  // true  (bit 1 is set)
    /// bool bit2 = bf.GetBit(2);  // false (bit 2 is clear)
    /// bool bit3 = bf.GetBit(3);  // true  (bit 3 is set)
    /// </code>
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool GetBit(int index)
    {
        CheckIndex(index);
        ushort mask = (ushort)(1 << index);
        return (_value & mask) != 0;
    }

    /// <summary>
    /// Sets or clears a specific bit.
    /// </summary>
    /// <param name="index">The zero-based index of the bit to modify (0-15).</param>
    /// <param name="state">
    /// <c>true</c> to set the bit to 1; <c>false</c> to clear the bit to 0.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if <paramref name="index"/> is less than 0 or greater than 15.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <strong>Algorithm:</strong>
    /// <list type="number">
    /// <item>Validates index is in range 0-15</item>
    /// <item>Creates a mask with only the target bit set: <c>1 &lt;&lt; index</c></item>
    /// <item>If <paramref name="state"/> is true: performs OR to set bit: <c>_value |= mask</c></item>
    /// <item>If <paramref name="state"/> is false: performs AND with inverted mask to clear bit: <c>_value &amp;= ~mask</c></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Bit Manipulation:</strong>
    /// <list type="bullet">
    /// <item><strong>Set bit:</strong> OR operation (<c>|=</c>) turns on the target bit without affecting others</item>
    /// <item><strong>Clear bit:</strong> AND with complement (<c>&amp;= ~mask</c>) turns off the target bit without affecting others</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Example:</strong>
    /// <code>
    /// var bf = new BitField16 { Value = 0b0000_0000_0000_0000 };
    /// bf.SetBit(1, true);   // Value is now 0b0000_0000_0000_0010
    /// bf.SetBit(3, true);   // Value is now 0b0000_0000_0000_1010
    /// bf.SetBit(1, false);  // Value is now 0b0000_0000_0000_1000 (bit 1 cleared)
    /// </code>
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> Uses direct bit manipulation (shift, OR, AND) which
    /// compiles to a handful of CPU instructions. Highly efficient for managing packed flags.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBit(int index, bool state)
    {
        CheckIndex(index);
        ushort mask = (ushort)(1 << index);

        if (state)
        {
            // Set bit: OR with mask
            _value |= mask;
        }
        else
        {
            // Clear bit: AND with complement of mask
            _value &= (ushort)~mask;
        }
    }
}
