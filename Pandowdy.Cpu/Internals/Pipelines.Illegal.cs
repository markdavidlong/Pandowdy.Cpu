// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Cpu.Internals;

/// <summary>
/// NMOS 6502 illegal/undocumented opcode pipelines.
/// </summary>
internal static partial class Pipelines
{
    // ========================================
    // JAM - Halt CPU (Multiple opcodes)
    // Already defined in Pipelines.Special.cs
    // ========================================

    // ========================================
    // LAX - Load A and X (LDA + LDX)
    // ========================================

    /// <summary>LAX zp (0xA7) - 3 cycles</summary>
    private static readonly MicroOp[] Lax_Zp =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            MicroOps.ReadZeroPage(prev, current, bus);
            MicroOps.LaxOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>LAX zp,Y (0xB7) - 4 cycles</summary>
    private static readonly MicroOp[] Lax_ZpY =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddYZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.ReadZeroPage(prev, current, bus);
            MicroOps.LaxOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>LAX abs (0xAF) - 4 cycles</summary>
    private static readonly MicroOp[] Lax_Abs =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.LaxOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>LAX abs,Y (0xBF) - 4+ cycles</summary>
    private static readonly MicroOp[] Lax_AbsY =
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
            MicroOps.LaxOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>LAX (zp,X) (0xA3) - 6 cycles</summary>
    private static readonly MicroOp[] Lax_IzX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZP,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.LaxOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>LAX (zp),Y (0xB3) - 5+ cycles</summary>
    private static readonly MicroOp[] Lax_IzY =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZPAddY,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.LaxOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // SAX - Store A AND X
    // ========================================

    /// <summary>SAX zp (0x87) - 3 cycles</summary>
    private static readonly MicroOp[] Sax_Zp =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            MicroOps.SaxOp(prev, current, bus);
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>SAX zp,Y (0x97) - 4 cycles</summary>
    private static readonly MicroOp[] Sax_ZpY =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddYZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.SaxOp(prev, current, bus);
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>SAX abs (0x8F) - 4 cycles</summary>
    private static readonly MicroOp[] Sax_Abs =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        (prev, current, bus) =>
        {
            MicroOps.SaxOp(prev, current, bus);
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>SAX (zp,X) (0x83) - 6 cycles</summary>
    private static readonly MicroOp[] Sax_IzX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZP,
        (prev, current, bus) =>
        {
            MicroOps.SaxOp(prev, current, bus);
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // DCP - Decrement then Compare (RMW)
    // ========================================

    /// <summary>DCP zp (0xC7) - 5 cycles</summary>
    private static readonly MicroOp[] Dcp_Zp =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteZeroPage(prev, current, bus);
            MicroOps.DcpOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>DCP zp,X (0xD7) - 6 cycles</summary>
    private static readonly MicroOp[] Dcp_ZpX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteZeroPage(prev, current, bus);
            MicroOps.DcpOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>DCP abs (0xCF) - 6 cycles</summary>
    private static readonly MicroOp[] Dcp_Abs =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.DcpOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>DCP abs,X (0xDF) - 7 cycles</summary>
    private static readonly MicroOp[] Dcp_AbsX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.AddXWithDummyRead,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.DcpOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>DCP abs,Y (0xDB) - 7 cycles</summary>
    private static readonly MicroOp[] Dcp_AbsY =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.AddYWithDummyRead,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.DcpOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>DCP (zp,X) (0xC3) - 8 cycles</summary>
    private static readonly MicroOp[] Dcp_IzX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZP,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.DcpOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>DCP (zp),Y (0xD3) - 8 cycles</summary>
    private static readonly MicroOp[] Dcp_IzY =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZPSetupRMWIZY,
        MicroOps.ReadWrongAddressFixRMWIZY,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.DcpOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // ISC/ISB - Increment then Subtract (RMW)
    // ========================================

    /// <summary>ISC zp (0xE7) - 5 cycles</summary>
    private static readonly MicroOp[] Isc_Zp =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteZeroPage(prev, current, bus);
            MicroOps.IscOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ISC zp,X (0xF7) - 6 cycles</summary>
    private static readonly MicroOp[] Isc_ZpX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteZeroPage(prev, current, bus);
            MicroOps.IscOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ISC abs (0xEF) - 6 cycles</summary>
    private static readonly MicroOp[] Isc_Abs =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.IscOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ISC abs,X (0xFF) - 7 cycles</summary>
    private static readonly MicroOp[] Isc_AbsX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.AddXWithDummyRead,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.IscOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ISC abs,Y (0xFB) - 7 cycles</summary>
    private static readonly MicroOp[] Isc_AbsY =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.AddYWithDummyRead,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.IscOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ISC (zp,X) (0xE3) - 8 cycles</summary>
    private static readonly MicroOp[] Isc_IzX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZP,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.IscOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ISC (zp),Y (0xF3) - 8 cycles</summary>
    private static readonly MicroOp[] Isc_IzY =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZPSetupRMWIZY,
        MicroOps.ReadWrongAddressFixRMWIZY,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.IscOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // SLO - ASL then ORA (RMW)
    // ========================================

    /// <summary>SLO zp (0x07) - 5 cycles</summary>
    private static readonly MicroOp[] Slo_Zp =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteZeroPage(prev, current, bus);
            MicroOps.SloOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>SLO zp,X (0x17) - 6 cycles</summary>
    private static readonly MicroOp[] Slo_ZpX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteZeroPage(prev, current, bus);
            MicroOps.SloOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>SLO abs (0x0F) - 6 cycles</summary>
    private static readonly MicroOp[] Slo_Abs =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.SloOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>SLO abs,X (0x1F) - 7 cycles</summary>
    private static readonly MicroOp[] Slo_AbsX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.AddXWithDummyRead,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.SloOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>SLO abs,Y (0x1B) - 7 cycles</summary>
    private static readonly MicroOp[] Slo_AbsY =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.AddYWithDummyRead,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.SloOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>SLO (zp,X) (0x03) - 8 cycles</summary>
    private static readonly MicroOp[] Slo_IzX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZP,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.SloOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>SLO (zp),Y (0x13) - 8 cycles</summary>
    private static readonly MicroOp[] Slo_IzY =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZPSetupRMWIZY,
        MicroOps.ReadWrongAddressFixRMWIZY,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.SloOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // Unofficial SBC duplicate (0xEB)
    // ========================================

    /// <summary>SBC #imm (0xEB) - Unofficial duplicate - 2 cycles</summary>
    private static readonly MicroOp[] Sbc_Imm_Unofficial = Sbc_Imm_Nmos;

    // ========================================
    // RLA - Rotate Left then AND (RMW)
    // ========================================

    /// <summary>RLA zp (0x27) - 5 cycles</summary>
    private static readonly MicroOp[] Rla_Zp =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteZeroPage(prev, current, bus);
            MicroOps.RlaOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>RLA zp,X (0x37) - 6 cycles</summary>
    private static readonly MicroOp[] Rla_ZpX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteZeroPage(prev, current, bus);
            MicroOps.RlaOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>RLA abs (0x2F) - 6 cycles</summary>
    private static readonly MicroOp[] Rla_Abs =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.RlaOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>RLA abs,X (0x3F) - 7 cycles</summary>
    private static readonly MicroOp[] Rla_AbsX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.AddXWithDummyRead,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.RlaOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>RLA abs,Y (0x3B) - 7 cycles</summary>
    private static readonly MicroOp[] Rla_AbsY =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.AddYWithDummyRead,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.RlaOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>RLA (zp,X) (0x23) - 8 cycles</summary>
    private static readonly MicroOp[] Rla_IzX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZP,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.RlaOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>RLA (zp),Y (0x33) - 8 cycles</summary>
    private static readonly MicroOp[] Rla_IzY =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZPSetupRMWIZY,
        MicroOps.ReadWrongAddressFixRMWIZY,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.RlaOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // SRE - Shift Right then EOR (RMW)
    // ========================================

    /// <summary>SRE zp (0x47) - 5 cycles</summary>
    private static readonly MicroOp[] Sre_Zp =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteZeroPage(prev, current, bus);
            MicroOps.SreOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>SRE zp,X (0x57) - 6 cycles</summary>
    private static readonly MicroOp[] Sre_ZpX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteZeroPage(prev, current, bus);
            MicroOps.SreOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>SRE abs (0x4F) - 6 cycles</summary>
    private static readonly MicroOp[] Sre_Abs =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.SreOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>SRE abs,X (0x5F) - 7 cycles</summary>
    private static readonly MicroOp[] Sre_AbsX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.AddXWithDummyRead,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.SreOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>SRE abs,Y (0x5B) - 7 cycles</summary>
    private static readonly MicroOp[] Sre_AbsY =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.AddYWithDummyRead,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.SreOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>SRE (zp,X) (0x43) - 8 cycles</summary>
    private static readonly MicroOp[] Sre_IzX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZP,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.SreOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>SRE (zp),Y (0x53) - 8 cycles</summary>
    private static readonly MicroOp[] Sre_IzY =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZPSetupRMWIZY,
        MicroOps.ReadWrongAddressFixRMWIZY,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.SreOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // RRA - Rotate Right then ADC (RMW)
    // ========================================

    /// <summary>RRA zp (0x67) - 5 cycles</summary>
    private static readonly MicroOp[] Rra_Zp =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteZeroPage(prev, current, bus);
            MicroOps.RraOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>RRA zp,X (0x77) - 6 cycles</summary>
    private static readonly MicroOp[] Rra_ZpX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        MicroOps.ReadZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteZeroPage(prev, current, bus);
            MicroOps.RraOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>RRA abs (0x6F) - 6 cycles</summary>
    private static readonly MicroOp[] Rra_Abs =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.RraOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>RRA abs,X (0x7F) - 7 cycles</summary>
    private static readonly MicroOp[] Rra_AbsX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.AddXWithDummyRead,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.RraOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>RRA abs,Y (0x7B) - 7 cycles</summary>
    private static readonly MicroOp[] Rra_AbsY =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.AddYWithDummyRead,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.RraOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>RRA (zp,X) (0x63) - 8 cycles</summary>
    private static readonly MicroOp[] Rra_IzX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZP,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.RraOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>RRA (zp),Y (0x73) - 8 cycles</summary>
    private static readonly MicroOp[] Rra_IzY =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZPSetupRMWIZY,
        MicroOps.ReadWrongAddressFixRMWIZY,
        MicroOps.ReadFromTempAddress,
        (prev, current, bus) =>
        {
            MicroOps.DummyWriteTempAddress(prev, current, bus);
            MicroOps.RraOp(prev, current, bus);
        },
        (prev, current, bus) =>
        {
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // ANC - AND immediate, set C from bit 7
    // ========================================

    /// <summary>ANC #imm (0x0B, 0x2B) - 2 cycles</summary>
    private static readonly MicroOp[] Anc_Imm =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.FetchImmediate(prev, current, bus);
            MicroOps.AncOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // ALR - AND immediate then LSR
    // ========================================

    /// <summary>ALR #imm (0x4B) - 2 cycles</summary>
    private static readonly MicroOp[] Alr_Imm =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.FetchImmediate(prev, current, bus);
            MicroOps.AlrOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // ARR - AND immediate then ROR
    // ========================================

    /// <summary>ARR #imm (0x6B) - 2 cycles</summary>
    private static readonly MicroOp[] Arr_Imm =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.FetchImmediate(prev, current, bus);
            MicroOps.ArrOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // AXS/SBX - X = (A AND X) - immediate
    // ========================================

    /// <summary>AXS #imm (0xCB) - 2 cycles</summary>
    private static readonly MicroOp[] Axs_Imm =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.FetchImmediate(prev, current, bus);
            MicroOps.AxsOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // Unstable/Unreliable Illegal Opcodes
    // ========================================

    /// <summary>ANE/XAA #imm (0x8B) - A = (A | const) &amp; X &amp; imm - 2 cycles</summary>
    private static readonly MicroOp[] Ane_Imm =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.FetchImmediate(prev, current, bus);
            MicroOps.AneOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>LXA/LAX #imm (0xAB) - A = X = (A | const) &amp; imm - 2 cycles</summary>
    private static readonly MicroOp[] Lxa_Imm =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.FetchImmediate(prev, current, bus);
            MicroOps.LxaOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>LAS abs,Y (0xBB) - A = X = SP = SP &amp; mem - 4+ cycles</summary>
    private static readonly MicroOp[] Las_AbsY =
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
            MicroOps.LasOp(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>SHA/AHX (zp),Y (0x93) - Store (A &amp; X &amp; (high+1)) - 6 cycles</summary>
    private static readonly MicroOp[] Sha_IzY =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadPointerLowZP,
        (prev, current, bus) =>
        {
            // Read pointer high byte and combine to get base address
            ushort zpAddr = (ushort)((current.TempValue + 1) & 0x00FF);
            byte hi = bus.CpuRead(zpAddr);
            ushort baseAddr = (ushort)(current.TempAddress | (hi << 8));
            byte baseHigh = (byte)(baseAddr >> 8);
            // Calculate final address (with Y added)
            ushort finalAddr = (ushort)(baseAddr + current.Y);
            byte finalLow = (byte)(finalAddr & 0xFF);
            // Store baseHigh in upper byte of TempValue, finalAddr in TempAddress
            // Also store finalLow in lower byte of TempValue for the wrong address calculation
            current.TempValue = (ushort)((baseHigh << 8) | finalLow);
            current.TempAddress = finalAddr;
        },
        (prev, current, bus) =>
        {
            // Dummy read from WRONG address (base_high : final_low)
            byte baseHigh = (byte)(current.TempValue >> 8);
            byte finalLow = (byte)(current.TempValue & 0xFF);
            ushort wrongAddr = (ushort)((baseHigh << 8) | finalLow);
            bus.CpuRead(wrongAddr);
        },
        (prev, current, bus) =>
        {
            // Calculate value and write
            byte baseHigh = (byte)(current.TempValue >> 8);
            byte finalHigh = (byte)(current.TempAddress >> 8);
            byte finalLow = (byte)(current.TempAddress & 0xFF);
            byte value = (byte)(current.A & current.X & (baseHigh + 1));
            // If page crossed, write addr high = value, else final_high
            byte writeHigh = (baseHigh != finalHigh) ? value : finalHigh;
            ushort writeAddr = (ushort)((writeHigh << 8) | finalLow);
            bus.Write(writeAddr, value);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>SHA/AHX abs,Y (0x9F) - Store (A &amp; X &amp; (high+1)) - 5 cycles</summary>
    private static readonly MicroOp[] Sha_AbsY =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            MicroOps.FetchAddressHigh(prev, current, bus);
            // Store base address in TempValue
            current.TempValue = current.TempAddress;
        },
        (prev, current, bus) =>
        {
            // Add Y and do dummy read from WRONG address
            byte baseHigh = (byte)(current.TempValue >> 8);
            ushort finalAddr = (ushort)(current.TempValue + current.Y);
            byte finalLow = (byte)(finalAddr & 0xFF);
            current.TempAddress = finalAddr;
            // Wrong address = base_high : final_low
            ushort wrongAddr = (ushort)((baseHigh << 8) | finalLow);
            bus.CpuRead(wrongAddr);
        },
        (prev, current, bus) =>
        {
            // Calculate value and write
            byte baseHigh = (byte)(current.TempValue >> 8);
            byte finalHigh = (byte)(current.TempAddress >> 8);
            byte finalLow = (byte)(current.TempAddress & 0xFF);
            byte value = (byte)(current.A & current.X & (baseHigh + 1));
            // If page crossed, write addr high = value, else final_high
            byte writeHigh = (baseHigh != finalHigh) ? value : finalHigh;
            ushort writeAddr = (ushort)((writeHigh << 8) | finalLow);
            bus.Write(writeAddr, value);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>SHX abs,Y (0x9E) - Store (X &amp; (high+1)) - 5 cycles</summary>
    private static readonly MicroOp[] Shx_AbsY =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            MicroOps.FetchAddressHigh(prev, current, bus);
            current.TempValue = current.TempAddress;
        },
        (prev, current, bus) =>
        {
            byte baseHigh = (byte)(current.TempValue >> 8);
            ushort finalAddr = (ushort)(current.TempValue + current.Y);
            byte finalLow = (byte)(finalAddr & 0xFF);
            current.TempAddress = finalAddr;
            ushort wrongAddr = (ushort)((baseHigh << 8) | finalLow);
            bus.CpuRead(wrongAddr);
        },
        (prev, current, bus) =>
        {
            byte baseHigh = (byte)(current.TempValue >> 8);
            byte finalHigh = (byte)(current.TempAddress >> 8);
            byte finalLow = (byte)(current.TempAddress & 0xFF);
            byte value = (byte)(current.X & (baseHigh + 1));
            // If page crossed, write addr high = value, else final_high
            byte writeHigh = (baseHigh != finalHigh) ? value : finalHigh;
            ushort writeAddr = (ushort)((writeHigh << 8) | finalLow);
            bus.Write(writeAddr, value);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>SHY abs,X (0x9C) - Store (Y &amp; (high+1)) - 5 cycles</summary>
    private static readonly MicroOp[] Shy_AbsX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            MicroOps.FetchAddressHigh(prev, current, bus);
            current.TempValue = current.TempAddress;
        },
        (prev, current, bus) =>
        {
            byte baseHigh = (byte)(current.TempValue >> 8);
            ushort finalAddr = (ushort)(current.TempValue + current.X);
            byte finalLow = (byte)(finalAddr & 0xFF);
            current.TempAddress = finalAddr;
            ushort wrongAddr = (ushort)((baseHigh << 8) | finalLow);
            bus.CpuRead(wrongAddr);
        },
        (prev, current, bus) =>
        {
            byte baseHigh = (byte)(current.TempValue >> 8);
            byte finalHigh = (byte)(current.TempAddress >> 8);
            byte finalLow = (byte)(current.TempAddress & 0xFF);
            byte value = (byte)(current.Y & (baseHigh + 1));
            // If page crossed, write addr high = value, else final_high
            byte writeHigh = (baseHigh != finalHigh) ? value : finalHigh;
            ushort writeAddr = (ushort)((writeHigh << 8) | finalLow);
            bus.Write(writeAddr, value);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>TAS/SHS abs,Y (0x9B) - SP = A &amp; X, Store (A &amp; X &amp; (high+1)) - 5 cycles</summary>
    private static readonly MicroOp[] Tas_AbsY =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            MicroOps.FetchAddressHigh(prev, current, bus);
            current.TempValue = current.TempAddress;
        },
        (prev, current, bus) =>
        {
            byte baseHigh = (byte)(current.TempValue >> 8);
            ushort finalAddr = (ushort)(current.TempValue + current.Y);
            byte finalLow = (byte)(finalAddr & 0xFF);
            current.TempAddress = finalAddr;
            ushort wrongAddr = (ushort)((baseHigh << 8) | finalLow);
            bus.CpuRead(wrongAddr);
        },
        (prev, current, bus) =>
        {
            current.SP = (byte)(current.A & current.X);
            byte baseHigh = (byte)(current.TempValue >> 8);
            byte finalHigh = (byte)(current.TempAddress >> 8);
            byte finalLow = (byte)(current.TempAddress & 0xFF);
            byte value = (byte)(current.A & current.X & (baseHigh + 1));
            // If page crossed, write addr high = value, else final_high
            byte writeHigh = (baseHigh != finalHigh) ? value : finalHigh;
            ushort writeAddr = (ushort)((writeHigh << 8) | finalLow);
            bus.Write(writeAddr, value);
            current.InstructionComplete = true;
        }
    ];
}
