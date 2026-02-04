// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Cpu.Dormann_Tests;

/// <summary>
/// RAM bus implementation with interrupt feedback register for Klaus Dormann's interrupt test.
/// </summary>
/// <remarks>
/// The interrupt test uses a feedback register at $BFFC to control IRQ and NMI signals:
/// - Bit 0: IRQ (active HIGH in open collector mode without DDR)
/// - Bit 1: NMI (active HIGH in open collector mode without DDR, edge-triggered)
/// - Bit 7: Filtered out (reserved for diagnostic stop)
/// 
/// The test uses open collector mode (I_drive = 1) with I_ddr = 0 and I_filter = $7F.
/// In this configuration, writing a 1 to a bit asserts the corresponding interrupt.
/// </remarks>
public class InterruptTestBus : IPandowdyCpuBus
{
    private const ushort FeedbackPortAddress = 0xBFFC;
    private const byte IrqBit = 0;   // Bit 0 controls IRQ
    private const byte NmiBit = 1;   // Bit 1 controls NMI
    private const byte Filter = 0x7F; // Bit 7 is filtered out

    private readonly byte[] _memory = new byte[65536];
    private byte _feedbackRegister;

    /// <summary>
    /// Gets direct access to the memory array for initialization.
    /// </summary>
    public byte[] Memory => _memory;

    /// <summary>
    /// Gets the feedback register value.
    /// </summary>
    public byte FeedbackRegister => _feedbackRegister;

    /// <summary>
    /// Sets interrupt bits in the feedback register for WAI test automation.
    /// </summary>
    /// <param name="irq">If true, sets IRQ bit (bit 0).</param>
    /// <param name="nmi">If true, sets NMI bit (bit 1).</param>
    public void SetInterruptBits(bool irq, bool nmi)
    {
        byte value = _feedbackRegister;
        if (irq)
        {
            value |= (byte)(1 << IrqBit);
        }

        if (nmi)
        {
            value |= (byte)(1 << NmiBit);
        }

        _feedbackRegister = (byte)(value & Filter);
    }

    /// <summary>
    /// Clears interrupt bits in the feedback register for WAI test automation.
    /// </summary>
    /// <param name="irq">If true, clears IRQ bit (bit 0).</param>
    /// <param name="nmi">If true, clears NMI bit (bit 1).</param>
    public void ClearInterruptBits(bool irq, bool nmi)
    {
        byte value = _feedbackRegister;
        if (irq)
        {
            value = (byte)(value & ~(1 << IrqBit));
        }

        if (nmi)
        {
            value = (byte)(value & ~(1 << NmiBit));
        }

        _feedbackRegister = value;
    }

    /// <summary>
    /// Gets whether IRQ should be asserted (bit 0 is HIGH in open collector mode without DDR).
    /// </summary>
    public bool IrqActive => (_feedbackRegister & (1 << IrqBit)) != 0;

    /// <summary>
    /// Gets whether NMI should be asserted (bit 1 is HIGH in open collector mode without DDR).
    /// </summary>
    public bool NmiActive => (_feedbackRegister & (1 << NmiBit)) != 0;

    /// <summary>
    /// Reads a byte from the specified address.
    /// </summary>
    public byte CpuRead(ushort address)
    {
        if (address == FeedbackPortAddress)
        {
            return _feedbackRegister;
        }
        return _memory[address];
    }

    /// <summary>
    /// Peeks at a byte from the specified address without side effects.
    /// </summary>
    public byte Peek(ushort address)
    {
        if (address == FeedbackPortAddress)
        {
            return _feedbackRegister;
        }
        return _memory[address];
    }

    /// <summary>
    /// Writes a byte to the specified address.
    /// </summary>
    public void Write(ushort address, byte value)
    {
        if (address == FeedbackPortAddress)
        {
            // Apply filter (bit 7 is reserved for diagnostic stop)
            _feedbackRegister = (byte)(value & Filter);
        }
        else
        {
            _memory[address] = value;
        }
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
    /// Sets the NMI vector to point to the specified address.
    /// </summary>
    public void SetNmiVector(ushort address)
    {
        _memory[0xFFFA] = (byte)(address & 0xFF);
        _memory[0xFFFB] = (byte)(address >> 8);
    }

    /// <summary>
    /// Clears all memory to zero and resets the feedback register.
    /// </summary>
    public void Clear()
    {
        Array.Clear(_memory);
        _feedbackRegister = 0x00; // All bits low = no interrupts asserted (open collector without DDR)
    }

    /// <summary>
    /// Initializes the feedback register to a safe state (no interrupts).
    /// </summary>
    public void InitializeFeedback()
    {
        _feedbackRegister = 0x00; // All bits low = no interrupts asserted
    }
}
