// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Cpu.Internals;

internal static partial class Pipelines
{
    // ========================================
    // Branch Pipelines
    // ========================================

    /// <summary>BEQ - Branch if Equal (Z=1) (0xF0) - 2 cycles (+1 if taken, +1 if page cross)</summary>
    private static readonly MicroOp[] Beq =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.FetchImmediate(prev, current, bus);
            MicroOps.BranchIfZeroSet(prev, current, bus);
        }
    ];

    /// <summary>BNE - Branch if Not Equal (Z=0) (0xD0) - 2 cycles (+1 if taken, +1 if page cross)</summary>
    private static readonly MicroOp[] Bne =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.FetchImmediate(prev, current, bus);
            MicroOps.BranchIfZeroClear(prev, current, bus);
        }
    ];

    /// <summary>BCS - Branch if Carry Set (C=1) (0xB0) - 2 cycles (+1 if taken, +1 if page cross)</summary>
    private static readonly MicroOp[] Bcs =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.FetchImmediate(prev, current, bus);
            MicroOps.BranchIfCarrySet(prev, current, bus);
        }
    ];

    /// <summary>BCC - Branch if Carry Clear (C=0) (0x90) - 2 cycles (+1 if taken, +1 if page cross)</summary>
    private static readonly MicroOp[] Bcc =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.FetchImmediate(prev, current, bus);
            MicroOps.BranchIfCarryClear(prev, current, bus);
        }
    ];

    /// <summary>BMI - Branch if Minus (N=1) (0x30) - 2 cycles (+1 if taken, +1 if page cross)</summary>
    private static readonly MicroOp[] Bmi =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.FetchImmediate(prev, current, bus);
            MicroOps.BranchIfNegative(prev, current, bus);
        }
    ];

    /// <summary>BPL - Branch if Plus (N=0) (0x10) - 2 cycles (+1 if taken, +1 if page cross)</summary>
    private static readonly MicroOp[] Bpl =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.FetchImmediate(prev, current, bus);
            MicroOps.BranchIfPositive(prev, current, bus);
        }
    ];

    /// <summary>BVS - Branch if Overflow Set (V=1) (0x70) - 2 cycles (+1 if taken, +1 if page cross)</summary>
    private static readonly MicroOp[] Bvs =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.FetchImmediate(prev, current, bus);
            MicroOps.BranchIfOverflowSet(prev, current, bus);
        }
    ];

    /// <summary>BVC - Branch if Overflow Clear (V=0) (0x50) - 2 cycles (+1 if taken, +1 if page cross)</summary>
    private static readonly MicroOp[] Bvc =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.FetchImmediate(prev, current, bus);
            MicroOps.BranchIfOverflowClear(prev, current, bus);
        }
    ];

    /// <summary>BRA - Branch Always (0x80) - 3 cycles (+1 if page cross) - 65C02 only</summary>
    private static readonly MicroOp[] Bra =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.FetchImmediate(prev, current, bus);
            MicroOps.BranchAlways(prev, current, bus);
        }
    ];

    // ========================================
    // Rockwell BBR/BBS Pipelines
    // ========================================

    private static MicroOp[] CreateBbr(int bit)
    {
        return
        [
            MicroOps.FetchOpcode,
            MicroOps.FetchAddressLow,
            (prev, current, bus) =>
            {
                // Read ZP and store the value in TempValue for later use
                ushort zpAddr = (ushort)(current.TempAddress & 0x00FF);
                byte zpValue = bus.CpuRead(zpAddr);
                current.TempValue = zpValue;
            },
            (prev, current, bus) =>
            {
                // Dummy read at ZP address (same as T2)
                ushort zpAddr = (ushort)(current.TempAddress & 0x00FF);
                bus.CpuRead(zpAddr);
            },
            (prev, current, bus) =>
            {
                // Fetch branch offset
                sbyte offset = (sbyte)bus.CpuRead(current.PC);
                current.PC++;
                // Decide whether to branch
                byte zpValue = (byte)current.TempValue;
                byte mask = (byte)(1 << bit);
                if ((zpValue & mask) == 0)
                {
                    ushort oldPC = current.PC;
                    ushort newPC = (ushort)(current.PC + offset);
                    current.PC = newPC;
                    if ((oldPC >> 8) != (newPC >> 8))
                    {
                        // Page crossing: T5 dummy read at oldPC, T6 dummy read at oldPC + complete
                        MicroOp penaltyT5 = (p, n, b) => b.CpuRead(oldPC);
                        MicroOp penaltyT6 = (p, n, b) =>
                        {
                            b.CpuRead(oldPC);
                            n.InstructionComplete = true;
                        };
                        MicroOps.InsertAfterCurrentOp(current, penaltyT5);
                        MicroOps.AppendToWorkingPipeline(current, penaltyT6);
                    }
                    else
                    {
                        // No page cross: just T5 dummy read at oldPC + complete
                        MicroOp penaltyOp = (p, n, b) =>
                        {
                            b.CpuRead(oldPC);
                            n.InstructionComplete = true;
                        };
                        MicroOps.InsertAfterCurrentOp(current, penaltyOp);
                    }
                }
                else
                {
                    current.InstructionComplete = true;
                }
            }
        ];
    }

    private static MicroOp[] CreateBbs(int bit)
    {
        return
        [
            MicroOps.FetchOpcode,
            MicroOps.FetchAddressLow,
            (prev, current, bus) =>
            {
                // Read ZP and store the value in TempValue for later use
                ushort zpAddr = (ushort)(current.TempAddress & 0x00FF);
                byte zpValue = bus.CpuRead(zpAddr);
                current.TempValue = zpValue;
            },
            (prev, current, bus) =>
            {
                // Dummy read at ZP address (same as T2)
                ushort zpAddr = (ushort)(current.TempAddress & 0x00FF);
                bus.CpuRead(zpAddr);
            },
            (prev, current, bus) =>
            {
                // Fetch branch offset
                sbyte offset = (sbyte)bus.CpuRead(current.PC);
                current.PC++;
                // Decide whether to branch
                byte zpValue = (byte)current.TempValue;
                byte mask = (byte)(1 << bit);
                if ((zpValue & mask) != 0)
                {
                    ushort oldPC = current.PC;
                    ushort newPC = (ushort)(current.PC + offset);
                    current.PC = newPC;
                    if ((oldPC >> 8) != (newPC >> 8))
                    {
                        // Page crossing: T5 dummy read at oldPC, T6 dummy read at oldPC + complete
                        MicroOp penaltyT5 = (p, n, b) => b.CpuRead(oldPC);
                        MicroOp penaltyT6 = (p, n, b) =>
                        {
                            b.CpuRead(oldPC);
                            n.InstructionComplete = true;
                        };
                        MicroOps.InsertAfterCurrentOp(current, penaltyT5);
                        MicroOps.AppendToWorkingPipeline(current, penaltyT6);
                    }
                    else
                    {
                        // No page cross: just T5 dummy read at oldPC + complete
                        MicroOp penaltyOp = (p, n, b) =>
                        {
                            b.CpuRead(oldPC);
                            n.InstructionComplete = true;
                        };
                        MicroOps.InsertAfterCurrentOp(current, penaltyOp);
                    }
                }
                else
                {
                    current.InstructionComplete = true;
                }
            }
        ];
    }

    // Pre-created BBR/BBS pipelines
    private static readonly MicroOp[] Bbr0 = CreateBbr(0);
    private static readonly MicroOp[] Bbr1 = CreateBbr(1);
    private static readonly MicroOp[] Bbr2 = CreateBbr(2);
    private static readonly MicroOp[] Bbr3 = CreateBbr(3);
    private static readonly MicroOp[] Bbr4 = CreateBbr(4);
    private static readonly MicroOp[] Bbr5 = CreateBbr(5);
    private static readonly MicroOp[] Bbr6 = CreateBbr(6);
    private static readonly MicroOp[] Bbr7 = CreateBbr(7);

    private static readonly MicroOp[] Bbs0 = CreateBbs(0);
    private static readonly MicroOp[] Bbs1 = CreateBbs(1);
    private static readonly MicroOp[] Bbs2 = CreateBbs(2);
    private static readonly MicroOp[] Bbs3 = CreateBbs(3);
    private static readonly MicroOp[] Bbs4 = CreateBbs(4);
    private static readonly MicroOp[] Bbs5 = CreateBbs(5);
    private static readonly MicroOp[] Bbs6 = CreateBbs(6);
    private static readonly MicroOp[] Bbs7 = CreateBbs(7);
}
