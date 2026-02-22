// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.Cpu;

namespace Pandowdy.EmuCore.Interfaces;

/// <summary>
/// Apple II specific bus interface that extends the base IBus with
/// keyboard input and pushbutton (game controller) functionality.
/// </summary>
/// <remarks>
/// <para>
/// This interface represents the Apple IIe system bus, coordinating communication
/// between the CPU, memory, and I/O devices. It handles keyboard input, game controller
/// buttons, and provides access to the system clock counter for timing-sensitive operations.
/// </para>
/// </remarks>
public interface IAppleIIBus : IPandowdyCpuBus, IRestartable
{
    /// <summary>
    /// Gets the memory pool representing the Apple IIe's 64k addressable memory space.
    /// </summary>
    /// <value>
    /// The memory instance that provides read/write access to main RAM, auxiliary RAM,
    /// ROM, and language card memory banks.
    /// </value>
    IPandowdyMemory RAM { get; }

    /// <summary>
    /// Gets the 65C02 CPU instance connected to this bus.
    /// </summary>
    /// <value>
    /// The CPU that executes instructions and communicates with memory and I/O
    /// devices through this bus interface.
    /// </value>
    IPandowdyCpu Cpu { get; }

    /// <summary>
    /// Gets the system clock counter tracking elapsed cycles since reset.
    /// </summary>
    /// <value>
    /// The total number of clock cycles elapsed. This counter increments with each
    /// call to <see cref="Clock"/> and is used for timing VBlank events and other
    /// time-sensitive operations. Runs at approximately 1.023 MHz.
    /// </value>
    ulong SystemClockCounter { get; }

    /// <summary>
    /// Reads a byte from the specified memory address as the CPU would.
    /// </summary>
    /// <param name="address">The 16-bit memory address to read from ($0000-$FFFF)</param>
    /// <param name="readOnly">If true, performs a read without side effects (for debugging).
    /// If false, may trigger side effects like clearing soft switches or strobing addresses.</param>
    /// <returns>The byte value at the specified address</returns>
    /// <remarks>
    /// <para>
    /// This method handles I/O address decoding for the $C000-$CFFF range, routing
    /// reads to appropriate handlers for keyboard, soft switches, and peripheral cards.
    /// Regular memory reads ($0000-$BFFF, $D000-$FFFF) are routed to the memory pool
    /// with bank switching and auxiliary memory selection applied.
    /// </para>
    /// <para>
    /// <strong>IPandowdyCpuBus:</strong> When called with <paramref name="readOnly"/> = false,
    /// this satisfies <see cref="IPandowdyCpuBus.CpuRead"/>. When called with 
    /// <paramref name="readOnly"/> = true, this satisfies <see cref="IPandowdyCpuBus.Peek"/>.
    /// </para>
    /// </remarks>
    byte CpuRead(ushort address, bool readOnly = false);

    /// <summary>
    /// Reads a byte from the specified memory address as the CPU would (with side effects).
    /// </summary>
    /// <param name="address">The 16-bit memory address to read from ($0000-$FFFF)</param>
    /// <returns>The byte value at the specified address</returns>
    /// <remarks>
    /// This is the <see cref="IPandowdyCpuBus.CpuRead"/> implementation.
    /// Equivalent to calling <see cref="CpuRead(ushort, bool)"/> with readOnly = false.
    /// </remarks>
    byte IPandowdyCpuBus.CpuRead(ushort address) => CpuRead(address, readOnly: false);

    /// <summary>
    /// Peeks at a byte from the specified address without triggering side effects.
    /// </summary>
    /// <param name="address">The 16-bit address to peek at.</param>
    /// <returns>The byte value at the specified address.</returns>
    /// <remarks>
    /// This is the <see cref="IPandowdyCpuBus.Peek"/> implementation.
    /// Equivalent to calling <see cref="CpuRead(ushort, bool)"/> with readOnly = true.
    /// </remarks>
    byte IPandowdyCpuBus.Peek(ushort address) => CpuRead(address, readOnly: true);

    /// <summary>
    /// Writes a byte to the specified memory address as the CPU would.
    /// </summary>
    /// <param name="address">The 16-bit memory address to write to ($0000-$FFFF)</param>
    /// <param name="data">The byte value to write</param>
    /// <remarks>
    /// This method handles I/O address decoding for the $C000-$CFFF range, routing
    /// writes to appropriate handlers for soft switches, language card banking,
    /// and peripheral cards. Regular memory writes are routed to the memory pool
    /// with bank switching, write protection, and auxiliary memory selection applied.
    /// Many Apple II soft switches are write-triggered, changing system state based
    /// on the write address rather than the data value.
    /// </remarks>
    void CpuWrite(ushort address, byte data);

    /// <summary>
    /// Writes a byte to the specified memory address as the CPU would.
    /// </summary>
    /// <param name="address">The 16-bit memory address to write to ($0000-$FFFF)</param>
    /// <param name="value">The byte value to write</param>
    /// <remarks>
    /// This is the <see cref="IPandowdyCpuBus.Write"/> implementation.
    /// Equivalent to calling <see cref="CpuWrite"/>.
    /// </remarks>
    void IPandowdyCpuBus.Write(ushort address, byte value) => CpuWrite(address, value);

    /// <summary>
    /// Advances the system clock by one cycle.
    /// </summary>
    /// <remarks>
    /// Increments the <see cref="SystemClockCounter"/> and checks for VBlank timing.
    /// Should be called once per CPU instruction cycle. The Apple IIe runs at
    /// approximately 1.023 MHz, so this method is typically called about 1 million
    /// times per second during emulation.
    /// </remarks>
    void Clock();

    /// <summary>
    /// Resets the bus and connected devices to their initial power-on state.
    /// </summary>
    /// <remarks>
    /// Resets the system clock counter to zero, clears the keyboard latch,
    /// resets soft switches to their default values, and triggers a CPU reset.
    /// This simulates powering on the Apple IIe or pressing the reset button.
    /// </remarks>
    void Reset();
}

