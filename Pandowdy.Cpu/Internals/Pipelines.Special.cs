// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Cpu.Internals;

internal static partial class Pipelines
{
    // ========================================
    // BRK Pipelines
    // ========================================

    /// <summary>BRK (0x00) - 7 cycles - NMOS (does NOT clear decimal flag)</summary>
    private static readonly MicroOp[] Brk_Nmos =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchImmediate,
        MicroOps.PushPCH,
        MicroOps.PushPCL,
        MicroOps.PushP,
        MicroOps.ReadVectorLow(0xFFFE),
        (prev, current, bus) =>
        {
            MicroOps.ReadVectorHigh(0xFFFE)(prev, current, bus);
            MicroOps.SetInterruptDisable(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>BRK (0x00) - 7 cycles - 65C02 (clears decimal flag)</summary>
    private static readonly MicroOp[] Brk_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchImmediate,
        MicroOps.PushPCH,
        MicroOps.PushPCL,
        MicroOps.PushP,
        MicroOps.ReadVectorLow(0xFFFE),
        (prev, current, bus) =>
        {
            MicroOps.ReadVectorHigh(0xFFFE)(prev, current, bus);
            MicroOps.SetInterruptDisable(prev, current, bus);
            MicroOps.Cld(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // NOP Pipelines
    // ========================================

    /// <summary>NOP (0xEA) - 2 cycles</summary>
    private static readonly MicroOp[] Nop =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.DummyReadPC(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>NOP 1-cycle for 65C02 undefined opcodes</summary>
    private static readonly MicroOp[] Nop_1Cycle =
    [
        (prev, current, bus) =>
        {
            bus.CpuRead(current.PC);
            current.PC++;
            current.InstructionComplete = true;
        }
    ];

    /// <summary>NOP 3-cycle for undefined opcodes</summary>
    private static readonly MicroOp[] Nop_3Cycle =
    [
        MicroOps.FetchOpcode,
        MicroOps.DummyReadPC,
        (prev, current, bus) => current.InstructionComplete = true
    ];

    /// <summary>NOP 4-cycle for undefined opcodes</summary>
    private static readonly MicroOp[] Nop_4Cycle =
    [
        MicroOps.FetchOpcode,
        MicroOps.DummyReadPC,
        MicroOps.DummyReadPC,
        (prev, current, bus) => current.InstructionComplete = true
    ];

    /// <summary>NOP #imm - 2 cycles (skips 1 byte)</summary>
    private static readonly MicroOp[] Nop_Imm =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.FetchImmediate(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>NOP zp - 3 cycles</summary>
    private static readonly MicroOp[] Nop_Zp =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            MicroOps.ReadZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>NOP zp,X - 4 cycles</summary>
    private static readonly MicroOp[] Nop_ZpX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.ReadZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>NOP abs - 4 cycles</summary>
    private static readonly MicroOp[] Nop_Abs =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>NOP abs - 5 cycles (65C02 with dummy read)</summary>
    private static readonly MicroOp[] Nop_Abs_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            ushort highByteAddr = current.PC;
            MicroOps.FetchAddressHigh(prev, current, bus);
            current.TempValue = highByteAddr;
        },
        (prev, current, bus) =>
        {
            bus.CpuRead(current.TempValue);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>NOP abs,X - 4+ cycles (reads from absolute + X)</summary>
    private static readonly MicroOp[] Nop_AbsX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            MicroOps.FetchAddressHigh(prev, current, bus);
            MicroOps.AddXCheckPage(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // JAM/STP/WAI Pipelines
    // ========================================

    /// <summary>JAM/KIL - Freeze CPU - 11 cycles then jammed - NMOS illegal</summary>
    private static readonly MicroOp[] Jam =
    [
        MicroOps.FetchOpcode,
        MicroOps.DummyReadPC,
        (prev, current, bus) => bus.CpuRead(0xFFFF),
        (prev, current, bus) => bus.CpuRead(0xFFFE),
        (prev, current, bus) => bus.CpuRead(0xFFFE),
        (prev, current, bus) => bus.CpuRead(0xFFFF),
        (prev, current, bus) => bus.CpuRead(0xFFFF),
        (prev, current, bus) => bus.CpuRead(0xFFFF),
        (prev, current, bus) => bus.CpuRead(0xFFFF),
        (prev, current, bus) => bus.CpuRead(0xFFFF),
        (prev, current, bus) =>
        {
            bus.CpuRead(0xFFFF);
            MicroOps.JamOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>STP - Stop the processor (0xDB) - 3 cycles - 65C02</summary>
    private static readonly MicroOp[] Stp =
    [
        MicroOps.FetchOpcode,
        MicroOps.DummyReadPC,
        (prev, current, bus) =>
        {
            MicroOps.StpOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>WAI - Wait for interrupt (0xCB) - 3 cycles - 65C02</summary>
    private static readonly MicroOp[] Wai =
    [
        MicroOps.FetchOpcode,
        MicroOps.DummyReadPC,
        (prev, current, bus) =>
        {
            MicroOps.WaiOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // TRB/TSB Pipelines (65C02)
    // ========================================

    /// <summary>TRB zp (0x14) - 5 cycles - 65C02</summary>
    private static readonly MicroOp[] Trb_Zp =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            bus.CpuRead((ushort)(current.TempAddress & 0x00FF));
            MicroOps.TrbOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>TRB abs (0x1C) - 6 cycles - 65C02</summary>
    private static readonly MicroOp[] Trb_Abs =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            bus.CpuRead(current.TempAddress);
            MicroOps.TrbOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>TSB zp (0x04) - 5 cycles - 65C02</summary>
    private static readonly MicroOp[] Tsb_Zp =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            bus.CpuRead((ushort)(current.TempAddress & 0x00FF));
            MicroOps.TsbOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>TSB abs (0x0C) - 6 cycles - 65C02</summary>
    private static readonly MicroOp[] Tsb_Abs =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            bus.CpuRead(current.TempAddress);
            MicroOps.TsbOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // RMB/SMB Pipelines (Rockwell 65C02)
    // ========================================

    private static MicroOp[] CreateRmb(int bit)
    {
        MicroOp rmbOp = MicroOps.RmbOp(bit);
        return
        [
            MicroOps.FetchOpcode,
            MicroOps.FetchAddressLow,
            MicroOps.ReadZeroPage,
            (prev, current, bus) =>
            {
                bus.CpuRead((ushort)(current.TempAddress & 0x00FF));
                rmbOp(prev, current, bus);
            },
            (prev, current, bus) =>
            {
                MicroOps.WriteZeroPage(prev, current, bus);
                current.InstructionComplete = true;
            }
        ];
    }

    private static MicroOp[] CreateSmb(int bit)
    {
        MicroOp smbOp = MicroOps.SmbOp(bit);
        return
        [
            MicroOps.FetchOpcode,
            MicroOps.FetchAddressLow,
            MicroOps.ReadZeroPage,
            (prev, current, bus) =>
            {
                bus.CpuRead((ushort)(current.TempAddress & 0x00FF));
                smbOp(prev, current, bus);
            },
            (prev, current, bus) =>
            {
                MicroOps.WriteZeroPage(prev, current, bus);
                current.InstructionComplete = true;
            }
        ];
    }

    // Pre-created RMB/SMB pipelines
    private static readonly MicroOp[] Rmb0 = CreateRmb(0);
    private static readonly MicroOp[] Rmb1 = CreateRmb(1);
    private static readonly MicroOp[] Rmb2 = CreateRmb(2);
    private static readonly MicroOp[] Rmb3 = CreateRmb(3);
    private static readonly MicroOp[] Rmb4 = CreateRmb(4);
    private static readonly MicroOp[] Rmb5 = CreateRmb(5);
    private static readonly MicroOp[] Rmb6 = CreateRmb(6);
    private static readonly MicroOp[] Rmb7 = CreateRmb(7);

    private static readonly MicroOp[] Smb0 = CreateSmb(0);
    private static readonly MicroOp[] Smb1 = CreateSmb(1);
    private static readonly MicroOp[] Smb2 = CreateSmb(2);
    private static readonly MicroOp[] Smb3 = CreateSmb(3);
    private static readonly MicroOp[] Smb4 = CreateSmb(4);
    private static readonly MicroOp[] Smb5 = CreateSmb(5);
    private static readonly MicroOp[] Smb6 = CreateSmb(6);
    private static readonly MicroOp[] Smb7 = CreateSmb(7);
}
