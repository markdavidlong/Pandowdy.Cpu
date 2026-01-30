// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Cpu.Internals;

internal static partial class Pipelines
{
    // ========================================
    // ASL Pipelines - NMOS
    // ========================================

    /// <summary>ASL A (0x0A) - 2 cycles</summary>
    private static readonly MicroOp[] Asl_A =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.DummyReadPC(prev, current, bus);
            MicroOps.AslA(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ASL zp (0x06) - 5 cycles - NMOS</summary>
    private static readonly MicroOp[] Asl_Zp =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteZeroPage(prev, current, bus);
            MicroOps.AslMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ASL zp,X (0x16) - 6 cycles - NMOS</summary>
    private static readonly MicroOp[] Asl_ZpX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteZeroPage(prev, current, bus);
            MicroOps.AslMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ASL abs (0x0E) - 6 cycles - NMOS</summary>
    private static readonly MicroOp[] Asl_Abs =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.AslMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ASL abs,X (0x1E) - 7 cycles - NMOS</summary>
    private static readonly MicroOp[] Asl_AbsX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.AddXWithDummyRead,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.AslMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // ASL Pipelines - 65C02 (dummy READ instead of write)
    // ========================================

    /// <summary>ASL zp (0x06) - 5 cycles - 65C02</summary>
    private static readonly MicroOp[] Asl_Zp_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            bus.CpuRead((ushort)(current.TempAddress & 0x00FF)); // 65C02: dummy READ
            MicroOps.AslMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ASL zp,X (0x16) - 6 cycles - 65C02</summary>
    private static readonly MicroOp[] Asl_ZpX_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            bus.CpuRead((ushort)(current.TempAddress & 0x00FF)); // 65C02: dummy READ
            MicroOps.AslMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ASL abs (0x0E) - 6 cycles - 65C02</summary>
    private static readonly MicroOp[] Asl_Abs_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            bus.CpuRead(current.TempAddress); // 65C02: dummy READ
            MicroOps.AslMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ASL abs,X (0x1E) - 6 cycles (+1 if page cross) - 65C02</summary>
    private static readonly MicroOp[] Asl_AbsX_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            ushort highByteAddr = current.PC;
            MicroOps.FetchAddressHigh(prev, current, bus);
            current.TempValue = highByteAddr;
            MicroOps.AddXCheckPage65C02(prev, current, bus);
        },
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            bus.CpuRead(current.TempAddress); // 65C02: dummy READ
            MicroOps.AslMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // LSR Pipelines - NMOS
    // ========================================

    /// <summary>LSR A (0x4A) - 2 cycles</summary>
    private static readonly MicroOp[] Lsr_A =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.DummyReadPC(prev, current, bus);
            MicroOps.LsrA(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>LSR zp (0x46) - 5 cycles - NMOS</summary>
    private static readonly MicroOp[] Lsr_Zp =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteZeroPage(prev, current, bus);
            MicroOps.LsrMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>LSR zp,X (0x56) - 6 cycles - NMOS</summary>
    private static readonly MicroOp[] Lsr_ZpX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteZeroPage(prev, current, bus);
            MicroOps.LsrMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>LSR abs (0x4E) - 6 cycles - NMOS</summary>
    private static readonly MicroOp[] Lsr_Abs =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.LsrMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>LSR abs,X (0x5E) - 7 cycles - NMOS</summary>
    private static readonly MicroOp[] Lsr_AbsX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.AddXWithDummyRead,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.LsrMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // LSR Pipelines - 65C02 (dummy READ instead of write)
    // ========================================

    /// <summary>LSR zp (0x46) - 5 cycles - 65C02</summary>
    private static readonly MicroOp[] Lsr_Zp_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            bus.CpuRead((ushort)(current.TempAddress & 0x00FF));
            MicroOps.LsrMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>LSR zp,X (0x56) - 6 cycles - 65C02</summary>
    private static readonly MicroOp[] Lsr_ZpX_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            bus.CpuRead((ushort)(current.TempAddress & 0x00FF));
            MicroOps.LsrMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>LSR abs (0x4E) - 6 cycles - 65C02</summary>
    private static readonly MicroOp[] Lsr_Abs_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            bus.CpuRead(current.TempAddress);
            MicroOps.LsrMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>LSR abs,X (0x5E) - 6 cycles (+1 if page cross) - 65C02</summary>
    private static readonly MicroOp[] Lsr_AbsX_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            ushort highByteAddr = current.PC;
            MicroOps.FetchAddressHigh(prev, current, bus);
            current.TempValue = highByteAddr;
            MicroOps.AddXCheckPage65C02(prev, current, bus);
        },
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            bus.CpuRead(current.TempAddress);
            MicroOps.LsrMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // ROL Pipelines - NMOS
    // ========================================

    /// <summary>ROL A (0x2A) - 2 cycles</summary>
    private static readonly MicroOp[] Rol_A =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.DummyReadPC(prev, current, bus);
            MicroOps.RolA(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ROL zp (0x26) - 5 cycles - NMOS</summary>
    private static readonly MicroOp[] Rol_Zp =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteZeroPage(prev, current, bus);
            MicroOps.RolMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ROL zp,X (0x36) - 6 cycles - NMOS</summary>
    private static readonly MicroOp[] Rol_ZpX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteZeroPage(prev, current, bus);
            MicroOps.RolMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ROL abs (0x2E) - 6 cycles - NMOS</summary>
    private static readonly MicroOp[] Rol_Abs =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.RolMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ROL abs,X (0x3E) - 7 cycles - NMOS</summary>
    private static readonly MicroOp[] Rol_AbsX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.AddXWithDummyRead,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.RolMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // ROL Pipelines - 65C02 (dummy READ instead of write)
    // ========================================

    /// <summary>ROL zp (0x26) - 5 cycles - 65C02</summary>
    private static readonly MicroOp[] Rol_Zp_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            bus.CpuRead((ushort)(current.TempAddress & 0x00FF));
            MicroOps.RolMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ROL zp,X (0x36) - 6 cycles - 65C02</summary>
    private static readonly MicroOp[] Rol_ZpX_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            bus.CpuRead((ushort)(current.TempAddress & 0x00FF));
            MicroOps.RolMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ROL abs (0x2E) - 6 cycles - 65C02</summary>
    private static readonly MicroOp[] Rol_Abs_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            bus.CpuRead(current.TempAddress);
            MicroOps.RolMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ROL abs,X (0x3E) - 6 cycles (+1 if page cross) - 65C02</summary>
    private static readonly MicroOp[] Rol_AbsX_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            ushort highByteAddr = current.PC;
            MicroOps.FetchAddressHigh(prev, current, bus);
            current.TempValue = highByteAddr;
            MicroOps.AddXCheckPage65C02(prev, current, bus);
        },
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            bus.CpuRead(current.TempAddress);
            MicroOps.RolMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // ROR Pipelines - NMOS
    // ========================================

    /// <summary>ROR A (0x6A) - 2 cycles</summary>
    private static readonly MicroOp[] Ror_A =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.DummyReadPC(prev, current, bus);
            MicroOps.RorA(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ROR zp (0x66) - 5 cycles - NMOS</summary>
    private static readonly MicroOp[] Ror_Zp =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteZeroPage(prev, current, bus);
            MicroOps.RorMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ROR zp,X (0x76) - 6 cycles - NMOS</summary>
    private static readonly MicroOp[] Ror_ZpX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteZeroPage(prev, current, bus);
            MicroOps.RorMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ROR abs (0x6E) - 6 cycles - NMOS</summary>
    private static readonly MicroOp[] Ror_Abs =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.RorMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ROR abs,X (0x7E) - 7 cycles - NMOS</summary>
    private static readonly MicroOp[] Ror_AbsX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.AddXWithDummyRead,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.RorMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // ROR Pipelines - 65C02 (dummy READ instead of write)
    // ========================================

    /// <summary>ROR zp (0x66) - 5 cycles - 65C02</summary>
    private static readonly MicroOp[] Ror_Zp_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            bus.CpuRead((ushort)(current.TempAddress & 0x00FF));
            MicroOps.RorMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ROR zp,X (0x76) - 6 cycles - 65C02</summary>
    private static readonly MicroOp[] Ror_ZpX_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            bus.CpuRead((ushort)(current.TempAddress & 0x00FF));
            MicroOps.RorMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ROR abs (0x6E) - 6 cycles - 65C02</summary>
    private static readonly MicroOp[] Ror_Abs_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            bus.CpuRead(current.TempAddress);
            MicroOps.RorMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ROR abs,X (0x7E) - 6 cycles (+1 if page cross) - 65C02</summary>
    private static readonly MicroOp[] Ror_AbsX_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            ushort highByteAddr = current.PC;
            MicroOps.FetchAddressHigh(prev, current, bus);
            current.TempValue = highByteAddr;
            MicroOps.AddXCheckPage65C02(prev, current, bus);
        },
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            bus.CpuRead(current.TempAddress);
            MicroOps.RorMem(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];
}
