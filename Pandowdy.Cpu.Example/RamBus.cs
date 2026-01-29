// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.Cpu;

namespace Pandowdy.Cpu.Example;

/// <summary>
/// Simple RAM-backed bus implementation for testing.
/// Provides 64KB of addressable memory.
/// </summary>
public class RamBus : IPandowdyCpuBus
{
    private readonly byte[] _memory = new byte[65536];

    /// <summary>
    /// Gets direct access to the memory array for initialization.
    /// </summary>
    public byte[] Memory => _memory;

    /// <summary>
    /// Reads a byte from the specified address.
    /// </summary>
    public byte CpuRead(ushort address)
    {
        return _memory[address];
    }

    /// <summary>
    /// Peeks at a byte from the specified address without side effects.
    /// For simple RAM, this is the same as CpuRead.
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
        _memory[address] = value;
    }

    /// <summary>
    /// Loads a program into memory at the specified address.
    /// </summary>
    public void LoadProgram(ushort startAddress, byte[] program)
    {
        Array.Copy(program, 0, _memory, startAddress, program.Length);
    }

    /// <summary>
    /// Sets the reset vector to point to the specified address.
    /// </summary>
    public void SetResetVector(ushort address)
    {
        _memory[0xFFFC] = (byte)(address & 0xFF);
        _memory[0xFFFD] = (byte)(address >> 8);
    }

    /// <summary>
    /// Sets the IRQ/BRK vector to point to the specified address.
    /// </summary>
    public void SetIrqVector(ushort address)
    {
        _memory[0xFFFE] = (byte)(address & 0xFF);
        _memory[0xFFFF] = (byte)(address >> 8);
    }

    /// <summary>
    /// Clears all memory to zero.
    /// </summary>
    public void Clear()
    {
        Array.Clear(_memory);
    }
}
