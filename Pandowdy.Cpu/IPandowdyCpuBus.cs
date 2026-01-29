// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Cpu;

/// <summary>
/// Interface for CPU bus operations (memory and I/O access).
/// </summary>
public interface IPandowdyCpuBus
{
    /// <summary>
    /// Reads a byte from the specified address.
    /// This is the standard bus read that represents an actual CPU bus cycle.
    /// </summary>
    /// <param name="address">The 16-bit address to read from.</param>
    /// <returns>The byte value at the specified address.</returns>
    byte CpuRead(ushort address);

    /// <summary>
    /// Peeks at a byte from the specified address without triggering bus cycle tracking.
    /// Used internally by the CPU to determine instruction decoding without counting as a bus cycle.
    /// </summary>
    /// <param name="address">The 16-bit address to peek at.</param>
    /// <returns>The byte value at the specified address.</returns>
    /// <remarks>
    /// This method should return the same value as <see cref="CpuRead"/> but without
    /// any side effects like cycle counting or I/O triggering. Implementations that
    /// don't need to distinguish between peek and read can simply delegate to CpuRead.
    /// </remarks>
    byte Peek(ushort address);

    /// <summary>
    /// Writes a byte to the specified address.
    /// </summary>
    /// <param name="address">The 16-bit address to write to.</param>
    /// <param name="value">The byte value to write.</param>
    void Write(ushort address, byte value);
}
