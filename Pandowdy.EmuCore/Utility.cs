// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Machine;
using Pandowdy.EmuCore.Memory;

namespace Pandowdy.EmuCore;

/// <summary>
/// Common utility methods used throughout the emulator core.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Validation Helpers:</strong> Provides memory size validation for constructor
/// argument checking, ensuring memory subsystems are properly sized.
/// </para>
/// <para>
/// <strong>Bit Manipulation:</strong> Provides helpers for building Apple IIe-style
/// status bytes where bit 7 indicates switch state and bits 0-6 contain keyboard latch.
/// </para>
/// </remarks>
public static class Utility
{
    /// <summary>
    /// Validates that an IPandowdyMemory instance is a given size.
    /// </summary>
    /// <typeparam name="T">The type implementing IPandowdyMemory.</typeparam>
    /// <param name="memory">The memory instance to validate.</param>
    /// <param name="paramName">The parameter name for exception messages.</param>
    /// <param name="expectedSize">The expected size of the memory in bytes.</param>
    /// <returns>The validated memory instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown if memory is null.</exception>
    /// <exception cref="ArgumentException">Thrown if memory size does not match expected size.</exception>
    public static T ValidateIPandowdyMemorySize<T>(T memory, string paramName, ushort expectedSize) where T : IPandowdyMemory
    {
        ArgumentNullException.ThrowIfNull(memory, paramName);

        if (memory.Size != expectedSize)
        {
            throw new ArgumentException(
                $"Memory size must be exactly {expectedSize} bytes (0x{expectedSize:X}). " +
                $"Actual size: {memory.Size} (0x{memory.Size:X})",
                paramName);
        }

        return memory;
    }


    /// <summary>
    /// Builds a byte value with a soft switch state in bit 7.
    /// </summary>
    /// <param name="state">Soft switch state (true = bit 7 set, false = bit 7 clear).</param>
    /// <param name="other">Base byte value (typically keyboard latch), bits 0-6 preserved.</param>
    /// <returns>Byte with bit 7 set according to <paramref name="state"/>, bits 0-6 from <paramref name="other"/>.</returns>
    /// <remarks>
    /// Apple IIe soft switch status reads typically return the keyboard latch value (bits 0-6)
    /// with the switch state encoded in bit 7.
    /// </remarks>
    public static byte BuildHiBitVal(bool state, byte other = 0x00)
    {
        return (byte) ((state ? 0x80 : 0x00) | (other & 0x7f));
    }
}
