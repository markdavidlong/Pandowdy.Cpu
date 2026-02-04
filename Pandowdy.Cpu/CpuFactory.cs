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
    /// Creates a CPU instance for the specified variant with the provided state.
    /// </summary>
    /// <param name="variant">The CPU variant to emulate.</param>
    /// <param name="state">The CPU state to inject into the CPU.</param>
    /// <returns>A CPU instance for the specified variant.</returns>
    /// <remarks>
    /// The provided state is injected into the CPU via dependency injection.
    /// The state may be shared with other components or pre-configured before injection.
    /// </remarks>
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

    /// <summary>
    /// Creates a debugging CPU wrapper for the specified variant with the provided state.
    /// </summary>
    /// <param name="variant">The CPU variant to emulate.</param>
    /// <param name="state">The CPU state to inject into the CPU.</param>
    /// <returns>A <see cref="DebugCpu"/> wrapping a CPU instance for the specified variant.</returns>
    /// <remarks>
    /// <para>
    /// The returned <see cref="DebugCpu"/> provides debugging features such as:
    /// </para>
    /// <list type="bullet">
    ///   <item><description><see cref="DebugCpu.PrevState"/> - State before the last instruction</description></item>
    ///   <item><description><see cref="DebugCpu.BranchOccurred"/> - Whether a branch was taken</description></item>
    ///   <item><description><see cref="DebugCpu.ChangedRegisters"/> - Registers modified by the last instruction</description></item>
    ///   <item><description><see cref="DebugCpu.StackDelta"/> - Stack pointer change</description></item>
    /// </list>
    /// <para>
    /// For production use where debugging features are not needed, use <see cref="Create(CpuVariant, CpuState)"/> instead.
    /// </para>
    /// </remarks>
    public static DebugCpu CreateDebug(CpuVariant variant, CpuState state)
    {
        return new DebugCpu(Create(variant, state));
    }

    /// <summary>
    /// Wraps an existing CPU instance in a debugging wrapper.
    /// </summary>
    /// <param name="cpu">The CPU to wrap.</param>
    /// <returns>A <see cref="DebugCpu"/> wrapping the specified CPU.</returns>
    /// <remarks>
    /// Use this when you already have a CPU instance and want to add debugging capabilities.
    /// The wrapper delegates all operations to the underlying CPU while tracking state changes.
    /// </remarks>
    public static DebugCpu CreateDebug(IPandowdyCpu cpu)
    {
        return new DebugCpu(cpu);
    }
}
