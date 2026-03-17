// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Machine;
using Pandowdy.EmuCore.Memory;
using Pandowdy.EmuCore.Slots;

namespace Pandowdy.EmuCore.IO;

/// <summary>
/// Handles Apple IIe system I/O space ($C000-$C08F) including soft switches,
/// keyboard, game controller, and language card banking.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Address Range:</strong> This handler manages the first 144 bytes of I/O space
/// ($C000-$C08F), which contains:
/// <list type="bullet">
/// <item><strong>$C000-$C00F:</strong> Keyboard and memory soft switches (80STORE, RAMRD, RAMWRT, etc.)</item>
/// <item><strong>$C010-$C01F:</strong> Keyboard strobe and read status switches</item>
/// <item><strong>$C020-$C02F:</strong> Cassette output (not commonly used)</item>
/// <item><strong>$C030-$C03F:</strong> Speaker toggle</item>
/// <item><strong>$C040-$C04F:</strong> Game controller strobe</item>
/// <item><strong>$C050-$C05F:</strong> Video mode switches (TEXT, MIXED, PAGE2, HIRES, annunciators)</item>
/// <item><strong>$C060-$C06F:</strong> Cassette input and pushbuttons</item>
/// <item><strong>$C070-$C07F:</strong> Paddle trigger and IOU control</item>
/// <item><strong>$C080-$C08F:</strong> Language card banking switches</item>
/// </list>
/// </para>
/// <para>
/// <strong>Slot I/O ($C090-$C0FF):</strong> The remaining I/O space is handled by the
/// <see cref="ISlots"/> implementation for peripheral card I/O.
/// </para>
/// <para>
/// <strong>Decorator Pattern:</strong> This interface extends <see cref="IPandowdyMemory"/> allowing
/// implementations to be composed (e.g., wrapping with No-Slot Clock interceptor).
/// </para>
/// </remarks>
/// <seealso cref="ISlots"/>
/// <seealso cref="IPandowdyMemory"/>
public interface ISystemIoHandler : IPandowdyMemory, IRestartable
{
    /// <summary>
    /// Resets all soft switches and I/O state to power-on defaults.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Reset Behavior:</strong>
    /// <list type="bullet">
    /// <item>Soft switches reset to default state (INTCXROM on, others off)</item>
    /// <item>Keyboard latch cleared</item>
    /// <item>Language card set to default banking mode</item>
    /// <item>Video mode switches reset (TEXT mode, PAGE1)</item>
    /// </list>
    /// </para>
    /// <para>
    /// This method is called by the bus during system reset to ensure consistent
    /// initial I/O state matching Apple IIe power-on behavior.
    /// </para>
    /// </remarks>
    public void Reset();
}
