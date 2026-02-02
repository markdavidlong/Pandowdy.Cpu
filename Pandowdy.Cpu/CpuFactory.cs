// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Cpu;

/// <summary>
/// Factory for creating CPU instances by variant.
/// </summary>
public static class CpuFactory
{
    /// <summary>
    /// Creates a CPU instance for the specified variant with a new state.
    /// </summary>
    /// <param name="variant">The CPU variant to emulate.</param>
    /// <returns>A CPU instance for the specified variant.</returns>
    public static IPandowdyCpu Create(CpuVariant variant)
    {
        return variant switch
        {
            CpuVariant.Nmos6502 => new Cpu6502(),
            CpuVariant.Nmos6502Simple => new Cpu6502Simple(),
            CpuVariant.Wdc65C02 => new Cpu65C02(),
            CpuVariant.Rockwell65C02 => new Cpu65C02Rockwell(),
            _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, "Unsupported CPU variant.")
        };
    }

    /// <summary>
    /// Creates a CPU instance for the specified variant with an existing state.
    /// </summary>
    /// <param name="variant">The CPU variant to emulate.</param>
    /// <param name="state">The CPU state to use.</param>
    /// <returns>A CPU instance for the specified variant.</returns>
    public static IPandowdyCpu Create(CpuVariant variant, CpuState state)
    {
        return variant switch
        {
            CpuVariant.Nmos6502 => new Cpu6502(state),
            CpuVariant.Nmos6502Simple => new Cpu6502Simple(state),
            CpuVariant.Wdc65C02 => new Cpu65C02(state),
            CpuVariant.Rockwell65C02 => new Cpu65C02Rockwell(state),
            _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, "Unsupported CPU variant.")
        };
    }
}
