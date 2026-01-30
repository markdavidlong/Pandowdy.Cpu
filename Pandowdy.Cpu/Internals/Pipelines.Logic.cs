// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Cpu.Internals;

internal static partial class Pipelines
{
    // ========================================
    // AND Pipelines
    // ========================================

    /// <summary>AND #imm (0x29) - 2 cycles</summary>
    private static readonly MicroOp[] And_Imm =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.FetchImmediate(prev, current, bus);
            MicroOps.AndOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>AND zp (0x25) - 3 cycles</summary>
    private static readonly MicroOp[] And_Zp =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            MicroOps.ReadZeroPage(prev, current, bus);
            MicroOps.AndOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>AND zp,X (0x35) - 4 cycles</summary>
    private static readonly MicroOp[] And_ZpX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.ReadZeroPage(prev, current, bus);
            MicroOps.AndOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>AND abs (0x2D) - 4 cycles</summary>
    private static readonly MicroOp[] And_Abs =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.AndOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>AND abs,X (0x3D) - 4 cycles (+1 if page cross) - NMOS</summary>
    private static readonly MicroOp[] And_AbsX =
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
            MicroOps.AndOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>AND abs,X (0x3D) - 4 cycles (+1 if page cross) - 65C02</summary>
    private static readonly MicroOp[] And_AbsX_65C02 =
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
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.AndOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>AND abs,Y (0x39) - 4 cycles (+1 if page cross) - NMOS</summary>
    private static readonly MicroOp[] And_AbsY =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            MicroOps.FetchAddressHigh(prev, current, bus);
            MicroOps.AddYCheckPage(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.AndOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>AND abs,Y (0x39) - 4 cycles (+1 if page cross) - 65C02</summary>
    private static readonly MicroOp[] And_AbsY_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            ushort highByteAddr = current.PC;
            MicroOps.FetchAddressHigh(prev, current, bus);
            current.TempValue = highByteAddr;
            MicroOps.AddYCheckPage65C02(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.AndOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>AND (zp,X) (0x21) - 6 cycles</summary>
    private static readonly MicroOp[] And_IzX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZP,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.AndOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>AND (zp),Y (0x31) - 5 cycles (+1 if page cross) - NMOS</summary>
    private static readonly MicroOp[] And_IzY =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZPAddY,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.AndOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>AND (zp),Y (0x31) - 5 cycles (+1 if page cross) - 65C02</summary>
    private static readonly MicroOp[] And_IzY_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZPAddY65C02,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.AndOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>AND (zp) (0x32) - 5 cycles - 65C02 only</summary>
    private static readonly MicroOp[] And_Izp =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZP,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.AndOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // ORA Pipelines
    // ========================================

    /// <summary>ORA #imm (0x09) - 2 cycles</summary>
    private static readonly MicroOp[] Ora_Imm =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.FetchImmediate(prev, current, bus);
            MicroOps.OraOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ORA zp (0x05) - 3 cycles</summary>
    private static readonly MicroOp[] Ora_Zp =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            MicroOps.ReadZeroPage(prev, current, bus);
            MicroOps.OraOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ORA zp,X (0x15) - 4 cycles</summary>
    private static readonly MicroOp[] Ora_ZpX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.ReadZeroPage(prev, current, bus);
            MicroOps.OraOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ORA abs (0x0D) - 4 cycles</summary>
    private static readonly MicroOp[] Ora_Abs =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.OraOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ORA abs,X (0x1D) - 4 cycles (+1 if page cross) - NMOS</summary>
    private static readonly MicroOp[] Ora_AbsX =
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
            MicroOps.OraOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ORA abs,X (0x1D) - 4 cycles (+1 if page cross) - 65C02</summary>
    private static readonly MicroOp[] Ora_AbsX_65C02 =
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
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.OraOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ORA abs,Y (0x19) - 4 cycles (+1 if page cross) - NMOS</summary>
    private static readonly MicroOp[] Ora_AbsY =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            MicroOps.FetchAddressHigh(prev, current, bus);
            MicroOps.AddYCheckPage(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.OraOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ORA abs,Y (0x19) - 4 cycles (+1 if page cross) - 65C02</summary>
    private static readonly MicroOp[] Ora_AbsY_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            ushort highByteAddr = current.PC;
            MicroOps.FetchAddressHigh(prev, current, bus);
            current.TempValue = highByteAddr;
            MicroOps.AddYCheckPage65C02(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.OraOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ORA (zp,X) (0x01) - 6 cycles</summary>
    private static readonly MicroOp[] Ora_IzX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPageWithDummyRead,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZP,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.OraOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ORA (zp),Y (0x11) - 5 cycles (+1 if page cross) - NMOS</summary>
    private static readonly MicroOp[] Ora_IzY =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZPAddY,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.OraOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ORA (zp),Y (0x11) - 5 cycles (+1 if page cross) - 65C02</summary>
    private static readonly MicroOp[] Ora_IzY_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZPAddY65C02,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.OraOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ORA (zp) (0x12) - 5 cycles - 65C02 only</summary>
    private static readonly MicroOp[] Ora_Izp =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZP,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.OraOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // EOR Pipelines
    // ========================================

    /// <summary>EOR #imm (0x49) - 2 cycles</summary>
    private static readonly MicroOp[] Eor_Imm =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.FetchImmediate(prev, current, bus);
            MicroOps.EorOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>EOR zp (0x45) - 3 cycles</summary>
    private static readonly MicroOp[] Eor_Zp =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            MicroOps.ReadZeroPage(prev, current, bus);
            MicroOps.EorOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>EOR zp,X (0x55) - 4 cycles</summary>
    private static readonly MicroOp[] Eor_ZpX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.ReadZeroPage(prev, current, bus);
            MicroOps.EorOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>EOR abs (0x4D) - 4 cycles</summary>
    private static readonly MicroOp[] Eor_Abs =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.EorOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>EOR abs,X (0x5D) - 4 cycles (+1 if page cross) - NMOS</summary>
    private static readonly MicroOp[] Eor_AbsX =
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
            MicroOps.EorOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>EOR abs,X (0x5D) - 4 cycles (+1 if page cross) - 65C02</summary>
    private static readonly MicroOp[] Eor_AbsX_65C02 =
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
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.EorOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>EOR abs,Y (0x59) - 4 cycles (+1 if page cross) - NMOS</summary>
    private static readonly MicroOp[] Eor_AbsY =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            MicroOps.FetchAddressHigh(prev, current, bus);
            MicroOps.AddYCheckPage(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.EorOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>EOR abs,Y (0x59) - 4 cycles (+1 if page cross) - 65C02</summary>
    private static readonly MicroOp[] Eor_AbsY_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            ushort highByteAddr = current.PC;
            MicroOps.FetchAddressHigh(prev, current, bus);
            current.TempValue = highByteAddr;
            MicroOps.AddYCheckPage65C02(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.EorOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>EOR (zp,X) (0x41) - 6 cycles</summary>
    private static readonly MicroOp[] Eor_IzX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZP,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.EorOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>EOR (zp),Y (0x51) - 5 cycles (+1 if page cross) - NMOS</summary>
    private static readonly MicroOp[] Eor_IzY =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZPAddY,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.EorOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>EOR (zp),Y (0x51) - 5 cycles (+1 if page cross) - 65C02</summary>
    private static readonly MicroOp[] Eor_IzY_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZPAddY65C02,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.EorOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>EOR (zp) (0x52) - 5 cycles - 65C02 only</summary>
    private static readonly MicroOp[] Eor_Izp =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZP,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.EorOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // CMP Pipelines
    // ========================================

    /// <summary>CMP #imm (0xC9) - 2 cycles</summary>
    private static readonly MicroOp[] Cmp_Imm =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.FetchImmediate(prev, current, bus);
            MicroOps.CmpOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>CMP zp (0xC5) - 3 cycles</summary>
    private static readonly MicroOp[] Cmp_Zp =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            MicroOps.ReadZeroPage(prev, current, bus);
            MicroOps.CmpOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>CMP zp,X (0xD5) - 4 cycles</summary>
    private static readonly MicroOp[] Cmp_ZpX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.ReadZeroPage(prev, current, bus);
            MicroOps.CmpOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>CMP abs (0xCD) - 4 cycles</summary>
    private static readonly MicroOp[] Cmp_Abs =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.CmpOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>CMP abs,X (0xDD) - 4 cycles (+1 if page cross) - NMOS</summary>
    private static readonly MicroOp[] Cmp_AbsX =
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
            MicroOps.CmpOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>CMP abs,X (0xDD) - 4 cycles (+1 if page cross) - 65C02</summary>
    private static readonly MicroOp[] Cmp_AbsX_65C02 =
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
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.CmpOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>CMP abs,Y (0xD9) - 4 cycles (+1 if page cross) - NMOS</summary>
    private static readonly MicroOp[] Cmp_AbsY =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            MicroOps.FetchAddressHigh(prev, current, bus);
            MicroOps.AddYCheckPage(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.CmpOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>CMP abs,Y (0xD9) - 4 cycles (+1 if page cross) - 65C02</summary>
    private static readonly MicroOp[] Cmp_AbsY_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            ushort highByteAddr = current.PC;
            MicroOps.FetchAddressHigh(prev, current, bus);
            current.TempValue = highByteAddr;
            MicroOps.AddYCheckPage65C02(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.CmpOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>CMP (zp,X) (0xC1) - 6 cycles</summary>
    private static readonly MicroOp[] Cmp_IzX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZP,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.CmpOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>CMP (zp),Y (0xD1) - 5 cycles (+1 if page cross) - NMOS</summary>
    private static readonly MicroOp[] Cmp_IzY =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZPAddY,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.CmpOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>CMP (zp),Y (0xD1) - 5 cycles (+1 if page cross) - 65C02</summary>
    private static readonly MicroOp[] Cmp_IzY_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZPAddY65C02,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.CmpOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>CMP (zp) (0xD2) - 5 cycles - 65C02 only</summary>
    private static readonly MicroOp[] Cmp_Izp =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZP,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.CmpOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // CPX Pipelines
    // ========================================

    /// <summary>CPX #imm (0xE0) - 2 cycles</summary>
    private static readonly MicroOp[] Cpx_Imm =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.FetchImmediate(prev, current, bus);
            MicroOps.CpxOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>CPX zp (0xE4) - 3 cycles</summary>
    private static readonly MicroOp[] Cpx_Zp =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            MicroOps.ReadZeroPage(prev, current, bus);
            MicroOps.CpxOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>CPX abs (0xEC) - 4 cycles</summary>
    private static readonly MicroOp[] Cpx_Abs =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.CpxOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // CPY Pipelines
    // ========================================

    /// <summary>CPY #imm (0xC0) - 2 cycles</summary>
    private static readonly MicroOp[] Cpy_Imm =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.FetchImmediate(prev, current, bus);
            MicroOps.CpyOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>CPY zp (0xC4) - 3 cycles</summary>
    private static readonly MicroOp[] Cpy_Zp =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            MicroOps.ReadZeroPage(prev, current, bus);
            MicroOps.CpyOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>CPY abs (0xCC) - 4 cycles</summary>
    private static readonly MicroOp[] Cpy_Abs =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.CpyOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // BIT Pipelines
    // ========================================

    /// <summary>BIT #imm (0x89) - 2 cycles - 65C02 only (only sets Z flag)</summary>
    private static readonly MicroOp[] Bit_Imm =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.FetchImmediate(prev, current, bus);
            MicroOps.BitImmOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>BIT zp (0x24) - 3 cycles</summary>
    private static readonly MicroOp[] Bit_Zp =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            MicroOps.ReadZeroPage(prev, current, bus);
            MicroOps.BitOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>BIT zp,X (0x34) - 4 cycles - 65C02 only</summary>
    private static readonly MicroOp[] Bit_ZpX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.ReadZeroPage(prev, current, bus);
            MicroOps.BitOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>BIT abs (0x2C) - 4 cycles</summary>
    private static readonly MicroOp[] Bit_Abs =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.BitOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>BIT abs,X (0x3C) - 4 cycles (+1 if page cross) - NMOS</summary>
    private static readonly MicroOp[] Bit_AbsX =
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
            MicroOps.BitOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>BIT abs,X (0x3C) - 4 cycles (+1 if page cross) - 65C02</summary>
    private static readonly MicroOp[] Bit_AbsX_65C02 =
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
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.BitOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];
}
