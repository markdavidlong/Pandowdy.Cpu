// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Cpu.Internals;

internal static partial class Pipelines
{
    // ========================================
    // INX, INY, DEX, DEY Pipelines (Register)
    // ========================================

    /// <summary>INX (0xE8) - 2 cycles</summary>
    private static readonly MicroOp[] Inx_Impl =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.DummyReadPC(prev, current, bus);
            MicroOps.Inx(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>INY (0xC8) - 2 cycles</summary>
    private static readonly MicroOp[] Iny_Impl =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.DummyReadPC(prev, current, bus);
            MicroOps.Iny(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>DEX (0xCA) - 2 cycles</summary>
    private static readonly MicroOp[] Dex_Impl =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.DummyReadPC(prev, current, bus);
            MicroOps.Dex(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>DEY (0x88) - 2 cycles</summary>
    private static readonly MicroOp[] Dey_Impl =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.DummyReadPC(prev, current, bus);
            MicroOps.Dey(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // INC A, DEC A Pipelines (65C02 only)
    // ========================================

    /// <summary>INC A (0x1A) - 2 cycles - 65C02</summary>
    private static readonly MicroOp[] Inc_A =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.DummyReadPC(prev, current, bus);
            MicroOps.IncA(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>DEC A (0x3A) - 2 cycles - 65C02</summary>
    private static readonly MicroOp[] Dec_A =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.DummyReadPC(prev, current, bus);
            MicroOps.DecA(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // INC Memory Pipelines - NMOS
    // ========================================

    /// <summary>INC zp (0xE6) - 5 cycles - NMOS</summary>
    private static readonly MicroOp[] Inc_Zp =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteZeroPage(prev, current, bus);
            MicroOps.IncMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>INC zp,X (0xF6) - 6 cycles - NMOS</summary>
    private static readonly MicroOp[] Inc_ZpX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteZeroPage(prev, current, bus);
            MicroOps.IncMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>INC abs (0xEE) - 6 cycles - NMOS</summary>
    private static readonly MicroOp[] Inc_Abs =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.IncMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>INC abs,X (0xFE) - 7 cycles - NMOS</summary>
    private static readonly MicroOp[] Inc_AbsX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.AddXWithDummyRead,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.IncMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // INC Memory Pipelines - 65C02 (dummy READ)
    // ========================================

    /// <summary>INC zp (0xE6) - 5 cycles - 65C02</summary>
    private static readonly MicroOp[] Inc_Zp_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            bus.CpuRead((ushort)(current.TempAddress & 0x00FF));
            MicroOps.IncMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>INC zp,X (0xF6) - 6 cycles - 65C02</summary>
    private static readonly MicroOp[] Inc_ZpX_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            bus.CpuRead((ushort)(current.TempAddress & 0x00FF));
            MicroOps.IncMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>INC abs (0xEE) - 6 cycles - 65C02</summary>
    private static readonly MicroOp[] Inc_Abs_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            bus.CpuRead(current.TempAddress);
            MicroOps.IncMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>INC abs,X (0xFE) - ALWAYS 7 cycles - 65C02</summary>
    /// <remarks>Unlike ASL/LSR/ROL/ROR abs,X which are 6+1, INC/DEC abs,X are always 7 cycles</remarks>
    private static readonly MicroOp[] Inc_AbsX_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            // Fetch high byte and save this address for T3 dummy read
            ushort highByteAddr = current.PC;
            byte hi = bus.CpuRead(current.PC);
            current.PC++;
            current.TempAddress = (ushort)(current.TempAddress | (hi << 8));
            current.TempValue = highByteAddr;
        },
        (prev, current, bus) =>
        {
            // T3: Dummy read at high byte address, then add X
            bus.CpuRead(current.TempValue);
            current.TempAddress = (ushort)(current.TempAddress + current.X);
        },
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            bus.CpuRead(current.TempAddress);
            MicroOps.IncMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // DEC Memory Pipelines - NMOS
    // ========================================

    /// <summary>DEC zp (0xC6) - 5 cycles - NMOS</summary>
    private static readonly MicroOp[] Dec_Zp =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteZeroPage(prev, current, bus);
            MicroOps.DecMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>DEC zp,X (0xD6) - 6 cycles - NMOS</summary>
    private static readonly MicroOp[] Dec_ZpX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteZeroPage(prev, current, bus);
            MicroOps.DecMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>DEC abs (0xCE) - 6 cycles - NMOS</summary>
    private static readonly MicroOp[] Dec_Abs =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.DecMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>DEC abs,X (0xDE) - 7 cycles - NMOS</summary>
    private static readonly MicroOp[] Dec_AbsX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.AddXWithDummyRead,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.DecMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // DEC Memory Pipelines - 65C02 (dummy READ)
    // ========================================

    /// <summary>DEC zp (0xC6) - 5 cycles - 65C02</summary>
    private static readonly MicroOp[] Dec_Zp_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            bus.CpuRead((ushort)(current.TempAddress & 0x00FF));
            MicroOps.DecMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>DEC zp,X (0xD6) - 6 cycles - 65C02</summary>
    private static readonly MicroOp[] Dec_ZpX_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            bus.CpuRead((ushort)(current.TempAddress & 0x00FF));
            MicroOps.DecMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>DEC abs (0xCE) - 6 cycles - 65C02</summary>
    private static readonly MicroOp[] Dec_Abs_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            bus.CpuRead(current.TempAddress);
            MicroOps.DecMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>DEC abs,X (0xDE) - ALWAYS 7 cycles - 65C02</summary>
    /// <remarks>Unlike ASL/LSR/ROL/ROR abs,X which are 6+1, INC/DEC abs,X are always 7 cycles</remarks>
    private static readonly MicroOp[] Dec_AbsX_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            // Fetch high byte and save this address for T3 dummy read
            ushort highByteAddr = current.PC;
            byte hi = bus.CpuRead(current.PC);
            current.PC++;
            current.TempAddress = (ushort)(current.TempAddress | (hi << 8));
            current.TempValue = highByteAddr;
        },
        (prev, current, bus) =>
        {
            // T3: Dummy read at high byte address, then add X
            bus.CpuRead(current.TempValue);
            current.TempAddress = (ushort)(current.TempAddress + current.X);
        },
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            bus.CpuRead(current.TempAddress);
            MicroOps.DecMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];
}
