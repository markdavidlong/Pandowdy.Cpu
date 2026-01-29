// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.Cpu;

namespace Pandowdy.Cpu.Harte_SST_Tests;

/// <summary>
/// Represents a single bus cycle (read or write operation).
/// </summary>
public record BusCycle(ushort Address, byte Value, bool IsWrite);

/// <summary>
/// Simple RAM-backed bus implementation for Harte SingleStepTests.
/// Provides 64KB of addressable memory with optional cycle tracking.
/// </summary>
public class RamBus : IPandowdyCpuBus
{
    private readonly byte[] _memory = new byte[65536];
    private readonly List<BusCycle> _cycles = [];
    private bool _trackCycles;

    /// <summary>
    /// Gets direct access to the memory array for initialization.
    /// </summary>
    public byte[] Memory => _memory;

    /// <summary>
    /// Gets the recorded bus cycles when tracking is enabled.
    /// </summary>
    public IReadOnlyList<BusCycle> RecordedCycles => _cycles;

    /// <summary>
    /// Reads a byte from the specified address.
    /// </summary>
    public byte CpuRead(ushort address)
    {
        var value = _memory[address];
        if (_trackCycles)
        {
            _cycles.Add(new BusCycle(address, value, IsWrite: false));
        }
        return value;
    }

    /// <summary>
    /// Peeks at a byte from the specified address without recording a bus cycle.
    /// </summary>
    public byte Peek(ushort address)
    {
        return _memory[address];
    }

    /// <summary>
    /// Writes a byte to the specified address.
    /// </summary>
    public void Write(ushort address, byte value)
    {
        if (_trackCycles)
        {
            _cycles.Add(new BusCycle(address, value, IsWrite: true));
        }
        _memory[address] = value;
    }

    /// <summary>
    /// Clears all memory to zero and resets cycle tracking.
    /// </summary>
    public void Clear()
    {
        Array.Clear(_memory);
        _cycles.Clear();
        _trackCycles = false;
    }

    /// <summary>
    /// Sets a memory location directly (does not record as a cycle).
    /// </summary>
    public void SetMemory(ushort address, byte value)
    {
        _memory[address] = value;
    }

    /// <summary>
    /// Enables cycle tracking and clears any previously recorded cycles.
    /// </summary>
    public void StartCycleTracking()
    {
        _cycles.Clear();
        _trackCycles = true;
    }

    /// <summary>
    /// Disables cycle tracking.
    /// </summary>
    public void StopCycleTracking()
    {
        _trackCycles = false;
    }
}
