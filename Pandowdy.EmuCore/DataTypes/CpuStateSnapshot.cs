// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.EmuCore.DataTypes;

/// <summary>
/// Execution status of the CPU.
/// </summary>
/// <remarks>
/// Mirrors <c>Pandowdy.Cpu.CpuStatus</c> to avoid exposing the CPU library to the UI layer.
/// </remarks>
public enum CpuExecutionStatus : byte
{
    /// <summary>Normal execution mode - CPU is actively processing instructions.</summary>
    Running = 0,

    /// <summary>CPU halted by STP instruction. Only a hardware reset can resume execution.</summary>
    Stopped = 1,

    /// <summary>CPU frozen by executing an illegal JAM/KIL opcode (NMOS 6502 only).</summary>
    Jammed = 2,

    /// <summary>CPU suspended by WAI instruction, waiting for an interrupt.</summary>
    Waiting = 3,

    /// <summary>A halt instruction was encountered but bypassed (IgnoreHaltStopWait was true).</summary>
    Bypassed = 4
}

/// <summary>
/// Immutable snapshot of CPU register state for display and debugging.
/// </summary>
/// <remarks>
/// <para>
/// This struct provides a clean abstraction over the CPU's internal state, exposing only
/// what the UI/debugger needs without requiring a reference to the Pandowdy.Cpu library.
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> This is a readonly struct with value semantics. Each access
/// to <see cref="IEmulatorCoreInterface.CpuState"/> returns an independent snapshot copy.
/// </para>
/// <para>
/// <strong>Flag Accessors:</strong> Individual status flags (N, V, B, D, I, Z, C) are extracted
/// from the processor status byte (<see cref="P"/>) for convenience.
/// </para>
/// </remarks>
public readonly struct CpuStateSnapshot
{
    /// <summary>Accumulator register (A).</summary>
    public byte A { get; init; }

    /// <summary>X Index register.</summary>
    public byte X { get; init; }

    /// <summary>Y Index register.</summary>
    public byte Y { get; init; }

    /// <summary>Stack Pointer register (SP). Stack is at $0100-$01FF.</summary>
    public byte SP { get; init; }

    /// <summary>Program Counter (PC). Points to next instruction to fetch.</summary>
    public ushort PC { get; init; }

    /// <summary>Processor Status register (P). Contains all status flags.</summary>
    public byte P { get; init; }

    /// <summary>Current CPU execution status (Running, Stopped, Jammed, Waiting, Bypassed).</summary>
    public CpuExecutionStatus Status { get; init; }

    /// <summary>Cycles remaining in current instruction (0 = at instruction boundary).</summary>
    public int CyclesRemaining { get; init; }

    #region Status Flag Accessors

    // Flag bit positions in P register:
    // Bit 7: N (Negative)
    // Bit 6: V (Overflow)
    // Bit 5: U (Unused, always 1)
    // Bit 4: B (Break, only meaningful when pushed to stack)
    // Bit 3: D (Decimal mode)
    // Bit 2: I (Interrupt disable)
    // Bit 1: Z (Zero)
    // Bit 0: C (Carry)

    /// <summary>Negative flag (N). Set if result has bit 7 set.</summary>
    public bool FlagN => (P & 0x80) != 0;

    /// <summary>Overflow flag (V). Set on signed arithmetic overflow.</summary>
    public bool FlagV => (P & 0x40) != 0;

    /// <summary>Break flag (B). Only meaningful when P is pushed to stack by BRK.</summary>
    public bool FlagB => (P & 0x10) != 0;

    /// <summary>Decimal mode flag (D). When set, ADC/SBC use BCD arithmetic.</summary>
    public bool FlagD => (P & 0x08) != 0;

    /// <summary>Interrupt disable flag (I). When set, IRQ is ignored.</summary>
    public bool FlagI => (P & 0x04) != 0;

    /// <summary>Zero flag (Z). Set if result is zero.</summary>
    public bool FlagZ => (P & 0x02) != 0;

    /// <summary>Carry flag (C). Set on unsigned arithmetic overflow/borrow.</summary>
    public bool FlagC => (P & 0x01) != 0;

    #endregion

    /// <summary>
    /// Returns true if the CPU is at an instruction boundary (ready to fetch next opcode).
    /// </summary>
    public bool AtInstructionBoundary => CyclesRemaining == 0;

    /// <summary>
    /// Returns true if the CPU is actively running (not halted, jammed, or waiting).
    /// </summary>
    public bool IsRunning => Status == CpuExecutionStatus.Running;

    /// <summary>
    /// Formats the status flags as a string (e.g., "NV-BDIZC" with set flags uppercase).
    /// </summary>
    public string FlagsString => string.Create(8, P, static (span, p) =>
    {
        span[0] = (p & 0x80) != 0 ? 'N' : 'n';
        span[1] = (p & 0x40) != 0 ? 'V' : 'v';
        span[2] = '-'; // Unused bit (always 1, but we show as separator)
        span[3] = (p & 0x10) != 0 ? 'B' : 'b';
        span[4] = (p & 0x08) != 0 ? 'D' : 'd';
        span[5] = (p & 0x04) != 0 ? 'I' : 'i';
        span[6] = (p & 0x02) != 0 ? 'Z' : 'z';
        span[7] = (p & 0x01) != 0 ? 'C' : 'c';
    });

    /// <summary>
    /// Returns a formatted string representation of the CPU state.
    /// </summary>
    public override string ToString() =>
        $"PC=${PC:X4} A=${A:X2} X=${X:X2} Y=${Y:X2} SP=${SP:X2} P={FlagsString} [{Status}]";
}
