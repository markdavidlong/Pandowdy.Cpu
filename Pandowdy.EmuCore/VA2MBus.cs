// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.Cpu;
using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore;

/// <summary>
/// VA2M-specific system bus that coordinates CPU, memory, and timing for Apple IIe emulation.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Responsibilities:</strong>
/// <list type="bullet">
/// <item>CPU connection and cycle execution</item>
/// <item>Memory read/write routing to AddressSpaceController</item>
/// <item>CPU cycle counting and VBlank timing (via CpuClockingCounters)</item>
/// </list>
/// </para>
/// <para>
/// <strong>Architecture:</strong>
/// <list type="bullet">
/// <item>Memory operations delegated to AddressSpaceController</item>
/// <item>Timing and VBlank management delegated to CpuClockingCounters</item>
/// <item>I/O handling managed by AddressSpaceController's routing to cards/handlers</item>
/// <item>Simplified to pure bus coordination role</item>
/// </list>
/// </para>
/// <para>
/// <strong>Threading Model:</strong> All public methods (CpuRead/Write, Clock, Reset)
/// must be called ONLY from the emulator worker thread.
/// </para>
/// <para>
/// <strong>VBlank Timing:</strong> CpuClockingCounters manages VBlank timing and emits
/// <see cref="CpuClockingCounters.VBlankOccurred"/> event every 17,030 cycles (~60.06 Hz at 1.023 MHz),
/// matching the Apple IIe NTSC vertical blanking interval. The VBlank blackout period lasts
/// 4,550 cycles.
/// </para>
/// <para>
/// <strong>Disposal:</strong> After disposal, Clock() becomes a no-op.
/// Proper disposal ensures clean shutdown.
/// </para>
/// </remarks>
[Capability(typeof(Interfaces.IRestartable), priority: 100)]
public sealed class VA2MBus : IAppleIIBus, IDisposable
{
    /// <summary>
    /// Address space controller managing the 128KB Apple IIe memory space.
    /// </summary>
    /// <remarks>
    /// Handles all memory operations including RAM, ROM, memory banking, and I/O routing.
    /// </remarks>
    private readonly AddressSpaceController _addressSpace;
    /// <summary>
    /// CPU instance (65C02 emulator) connected to this bus.
    /// </summary>
    private readonly IPandowdyCpu _cpu;

    /// <summary>
    /// Gets the CPU instance connected to this bus.
    /// </summary>
    public IPandowdyCpu Cpu { get => _cpu; }

    /// <summary>
    /// CPU clocking and VBlank timing counters.
    /// </summary>
    /// <remarks>
    /// Manages total CPU cycle count, VBlank counter, and VBlank timing logic.
    /// Provides global access to accurate timing information for all components.
    /// </remarks>
    private readonly CpuClockingCounters _clockCounters;

    /// <summary>
    /// Gets the CPU clocking counters for timing-dependent components.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Components that need VBlank timing (e.g., Disk II controller for motor-off delay)
    /// should subscribe to <see cref="CpuClockingCounters.VBlankOccurred"/> via this property.
    /// </para>
    /// <para>
    /// <strong>Single Source of Truth:</strong> CpuClockingCounters is the authoritative
    /// source for all timing information including cycle counts and VBlank events.
    /// </para>
    /// </remarks>
    public CpuClockingCounters ClockCounters => _clockCounters;

    /// <summary>
    /// Gets the AddressSpaceController for direct memory access.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Primarily used for testing and direct memory inspection. Normal CPU access
    /// should go through CpuRead/CpuWrite.
    /// </para>
    /// <para>
    /// <strong>Note:</strong> This exposes the full address space controller, not just
    /// RAM. It includes memory banking, ROM mapping, and I/O routing.
    /// The IPandowdyMemory interface is used for compatibility with existing code.
    /// </para>
    /// </remarks>
    public IPandowdyMemory RAM => _addressSpace;

    /// <summary>
    /// Gets the total number of CPU cycles executed since last reset.
    /// </summary>
    public ulong SystemClockCounter => _clockCounters.TotalCycles;

    /// <summary>
    /// Flag indicating whether this bus has been disposed.
    /// </summary>
    private bool _disposed;

    /// <summary>Start of system I/O space ($C000).</summary>
    public const ushort SYSTEM_IO_START = 0xC000;
    /// <summary>End of Apple IIe internal system I/O address space ($C08F).</summary>
    public const ushort IO_SYSTEM_AREA_END = 0xC08F;

    /// <summary>
    /// Initializes a new instance of the VA2MBus class.
    /// </summary>
    /// <param name="addressSpace">Address space controller managing 128KB Apple IIe memory space (RAM, ROM, banking, I/O routing).</param>
    /// <param name="cpu">CPU instance (6502 emulator) to connect to this bus.</param>
    /// <param name="clockCounters">CPU clocking counters for cycle counting and VBlank timing.</param>
    /// <exception cref="ArgumentNullException">Thrown if any parameter is null.</exception>
    /// <remarks>
    /// <para>
    /// <strong>Initialization Sequence:</strong>
    /// <list type="number">
    /// <item>Validate all dependencies (null checks)</item>
    /// <item>Store AddressSpaceController reference for memory operations</item>
    /// <item>Store CPU reference for instruction execution</item>
    /// <item>Store CpuClockingCounters reference for timing management</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Simplified Architecture:</strong> This bus focuses purely on coordination.
    /// Memory operations route through AddressSpaceController, which handles I/O card
    /// routing, soft switches, and memory banking. Timing is managed by CpuClockingCounters.
    /// </para>
    /// <para>
    /// <strong>Address Routing:</strong> All memory and I/O addresses are routed through
    /// AddressSpaceController, which delegates to appropriate handlers (RAM, ROM, I/O cards).
    /// </para>
    /// </remarks>
    public VA2MBus(AddressSpaceController addressSpace, IPandowdyCpu cpu, CpuClockingCounters clockCounters)
    {
        ArgumentNullException.ThrowIfNull(addressSpace);
        ArgumentNullException.ThrowIfNull(cpu);
        ArgumentNullException.ThrowIfNull(clockCounters);
        _addressSpace = addressSpace;
        _cpu = cpu;
        _clockCounters = clockCounters;
    }


    /// <summary>
    /// Reads a byte from memory or I/O space.
    /// </summary>
    /// <param name="address">16-bit address to read from ($0000-$FFFF).</param>
    /// <param name="readOnly">If true, indicates a non-mutating read (not currently used).</param>
    /// <returns>Byte value from memory or I/O space.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the bus has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// <strong>Routing:</strong> All reads are routed through AddressSpaceController, which
    /// handles memory banking, ROM mapping, and I/O card routing automatically.
    /// </para>
    /// <para>
    /// <strong>Thread Context:</strong> Must be called from emulator thread only.
    /// </para>
    /// </remarks>
    public byte CpuRead(ushort address, bool readOnly = false)
    {
        ThrowIfDisposed();
        return _addressSpace.Read(address);
    }

    /// <summary>
    /// Writes a byte to memory or I/O space.
    /// </summary>
    /// <param name="address">16-bit address to write to ($0000-$FFFF).</param>
    /// <param name="data">Byte value to write.</param>
    /// <exception cref="ObjectDisposedException">Thrown if the bus has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// <strong>Routing:</strong> All writes are routed through AddressSpaceController, which
    /// handles memory banking, ROM write protection, and I/O card routing automatically.
    /// </para>
    /// <para>
    /// <strong>Thread Context:</strong> Must be called from emulator thread only.
    /// </para>
    /// </remarks>
    public void CpuWrite(ushort address, byte data)
    {
        ThrowIfDisposed();
        _addressSpace.Write(address, data);
    }

    /// <summary>
    /// Executes one CPU clock cycle and updates timing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Execution Sequence:</strong>
    /// <list type="number">
    /// <item>Execute one CPU instruction (CPU.Clock)</item>
    /// <item>Increment total cycle counter (via CpuClockingCounters)</item>
    /// <item>Decrement VBlank blackout counter</item>
    /// <item>Check if VBlank event should fire</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>VBlank Timing:</strong> CpuClockingCounters manages all timing logic including:
    /// <list type="bullet">
    /// <item>Total CPU cycle counting (TotalCycles property)</item>
    /// <item>VBlank blackout countdown (4,550 cycles)</item>
    /// <item>VBlank event scheduling (every 17,030 cycles)</item>
    /// <item>Catch-up logic for fast emulation (unthrottled batches)</item>
    /// </list>
    /// VA2MBus simply calls CheckAndAdvanceVBlank() which fires <see cref="CpuClockingCounters.VBlankOccurred"/>.
    /// </para>
    /// <para>
    /// <strong>Disposal Safety:</strong> If the bus has been disposed, Clock() returns immediately
    /// without executing CPU or firing events.
    /// </para>
    /// <para>
    /// <strong>Thread Context:</strong> Must be called from emulator thread only.
    /// </para>
    /// </remarks>
    public void Clock()
    {
        if (_disposed)
        {
            return;
        }

        // Execute a single CPU cycle
        _cpu.Clock(this);
        _clockCounters.IncrementCycles(1);
        _clockCounters.DecrementVBlankCounter(1);

        // Check if VBlank should fire (handles catch-up automatically)
        // CpuClockingCounters.VBlankOccurred is invoked inside CheckAndAdvanceVBlank
        _clockCounters.CheckAndAdvanceVBlank();
    }

    /// <summary>
    /// Performs a full system reset (power cycle).
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if the bus has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// <strong>Reset Operations:</strong>
    /// <list type="number">
    /// <item>Reset address space controller (memory ranges, banking, I/O handlers)</item>
    /// <item>Reset CPU (PC loaded from $FFFC/$FFFD, SP = $FF)</item>
    /// <item>Reset timing counters (TotalCycles = 0, VBlankCounter = 0, NextVBlankCycle = 12,480)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Thread Context:</strong> Must be called from emulator thread only.
    /// </para>
    /// </remarks>
    public void Reset()
    {
        ThrowIfDisposed();

        _addressSpace.Reset();
        _cpu!.Reset(this);
        _clockCounters.Reset();
    }

    /// <summary>
    /// Restores the bus to its initial power-on state (cold boot).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Resets the CPU by loading the reset vector ($FFFC/$FFFD). The CPU is not an
    /// <see cref="IRestartable"/> participant — it is an external dependency that uses
    /// the same <c>Reset(bus)</c> call for both warm and cold boot. The bus must perform
    /// this step because the CPU needs a bus reference to read the reset vector, and this
    /// read is order-dependent (soft switches must already be at power-on defaults so that
    /// ROM is active for the vector read).
    /// </para>
    /// <para>
    /// All other subsystems (AddressSpaceController, CpuClockingCounters, etc.) are
    /// independently restartable via <see cref="DataTypes.RestartCollection"/>.
    /// </para>
    /// </remarks>
    public void Restart()
    {
        ThrowIfDisposed();
        _cpu.Reset(this);
    }



    /// <summary>
    /// Disposes the bus, cleaning up resources and preventing further operation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Cleanup Operations:</strong>
    /// <list type="bullet">
    /// <item>Set _disposed flag to true</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Post-Disposal Behavior:</strong>
    /// <list type="bullet">
    /// <item>Clock() becomes a no-op (returns immediately)</item>
    /// <item>CpuRead/CpuWrite throw ObjectDisposedException</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Idempotent:</strong> Multiple calls to Dispose() are safe (no-op after first call).
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> Should be called from the thread that owns the bus instance
    /// (typically after stopping emulation).
    /// </para>
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
    }

    /// <summary>
    /// Throws ObjectDisposedException if the bus has been disposed.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if _disposed is true.</exception>
    /// <remarks>
    /// Called at the start of methods that should not execute after disposal (CpuRead, CpuWrite,
    /// Reset, UserReset).
    /// </remarks>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(VA2MBus));
    }
}
