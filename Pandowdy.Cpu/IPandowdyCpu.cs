// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Cpu;

/// <summary>
/// Defines the public API for Pandowdy CPU instances.
/// </summary>
public interface IPandowdyCpu
{
    /// <summary>
    /// Executes a single clock cycle.
    /// </summary>
    /// <param name="bus">The memory/IO bus.</param>
    /// <returns><c>true</c> if an instruction completed this cycle; otherwise, <c>false</c>.</returns>
    bool Clock(IPandowdyCpuBus bus);

    /// <summary>
    /// Executes micro-ops until an instruction completes.
    /// </summary>
    /// <param name="bus">The memory/IO bus.</param>
    /// <returns>The number of clock cycles consumed.</returns>
    int Step(IPandowdyCpuBus bus);

    /// <summary>
    /// Runs the CPU for a specified number of clock cycles.
    /// </summary>
    /// <param name="bus">The memory/IO bus.</param>
    /// <param name="maxCycles">The maximum number of cycles to execute.</param>
    /// <returns>The number of clock cycles consumed.</returns>
    int Run(IPandowdyCpuBus bus, int maxCycles);

    /// <summary>
    /// Resets the CPU to its initial state and loads the reset vector.
    /// </summary>
    /// <param name="bus">The memory/IO bus.</param>
    void Reset(IPandowdyCpuBus bus);

    /// <summary>
    /// Signals an IRQ (Interrupt Request).
    /// </summary>
    void SignalIrq();

    /// <summary>
    /// Signals an NMI (Non-Maskable Interrupt).
    /// </summary>
    void SignalNmi();

    /// <summary>
    /// Signals a hardware Reset.
    /// </summary>
    void SignalReset();

    /// <summary>
    /// Clears a pending IRQ signal.
    /// </summary>
    void ClearIrq();

    /// <summary>
    /// Checks for and handles any pending interrupt.
    /// </summary>
    /// <param name="bus">The bus interface for reading vectors and writing to the stack.</param>
    /// <returns><c>true</c> if an interrupt was handled; otherwise, <c>false</c>.</returns>
    bool HandlePendingInterrupt(IPandowdyCpuBus bus);

    /// <summary>
    /// Gets the CPU variant this instance emulates.
    /// </summary>
    CpuVariant Variant { get; }

    /// <summary>
    /// Gets or sets the CPU state.
    /// </summary>
    CpuState State { get; set; }
}
