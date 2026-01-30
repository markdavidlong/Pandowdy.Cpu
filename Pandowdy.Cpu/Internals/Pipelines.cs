// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Cpu.Internals;

/// <summary>
/// Provides opcode pipeline definitions for 6502/65C02 CPU variants.
/// </summary>
/// <remarks>
/// Each pipeline is an array of <see cref="MicroOp"/> delegates representing the
/// micro-operations executed during each clock cycle of an instruction.
/// The first micro-op is always <see cref="MicroOps.FetchOpcode"/>.
/// </remarks>
internal static partial class Pipelines
{
    // ========================================
    // Constants
    // ========================================

    private const ushort IrqBrkVector = 0xFFFE;

    // ========================================
    // Infrastructure
    // ========================================

    /// <summary>
    /// Gets the pipeline table for the specified CPU variant.
    /// </summary>
    /// <param name="variant">The CPU variant.</param>
    /// <returns>An array of 256 pipeline arrays, one for each opcode.</returns>
    public static MicroOp[][] GetPipelines(CpuVariant variant) => variant switch
    {
        CpuVariant.NMOS6502 => Pipelines6502,
        CpuVariant.NMOS6502_NO_ILLEGAL => Pipelines6502NoIllegal,
        CpuVariant.WDC65C02 => Pipelines65C02,
        CpuVariant.ROCKWELL65C02 => Pipelines65C02Rockwell,
        _ => Pipelines6502
    };

    /// <summary>
    /// Unimplemented opcode placeholder - just fetches and completes.
    /// </summary>
    private static readonly MicroOp[] Unimplemented =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) => current.InstructionComplete = true
    ];

    // ========================================
    // LDA Pipelines
    // ========================================

    /// <summary>LDA #imm (0xA9) - 2 cycles</summary>
    private static readonly MicroOp[] Lda_Imm =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.FetchImmediate(prev, current, bus);
            MicroOps.LoadA(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>LDA zp (0xA5) - 3 cycles</summary>
    private static readonly MicroOp[] Lda_Zp =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            MicroOps.ReadZeroPage(prev, current, bus);
            MicroOps.LoadA(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>LDA zp,X (0xB5) - 4 cycles</summary>
    private static readonly MicroOp[] Lda_ZpX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.ReadZeroPage(prev, current, bus);
            MicroOps.LoadA(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>LDA abs (0xAD) - 4 cycles</summary>
    private static readonly MicroOp[] Lda_Abs =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.LoadA(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>LDA abs,X (0xBD) - 4 cycles (+1 if page cross) - NMOS</summary>
    private static readonly MicroOp[] Lda_AbsX =
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
            MicroOps.LoadA(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>LDA abs,X (0xBD) - 4 cycles (+1 if page cross) - 65C02</summary>
    private static readonly MicroOp[] Lda_AbsX_65C02 =
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
            MicroOps.LoadA(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>LDA abs,Y (0xB9) - 4 cycles (+1 if page cross) - NMOS</summary>
    private static readonly MicroOp[] Lda_AbsY =
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
            MicroOps.LoadA(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>LDA abs,Y (0xB9) - 4 cycles (+1 if page cross) - 65C02</summary>
    private static readonly MicroOp[] Lda_AbsY_65C02 =
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
            MicroOps.LoadA(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>LDA (zp,X) (0xA1) - 6 cycles</summary>
    private static readonly MicroOp[] Lda_IzX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZP,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.LoadA(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>LDA (zp),Y (0xB1) - 5 cycles (+1 if page cross) - NMOS</summary>
    private static readonly MicroOp[] Lda_IzY =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZPAddY,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.LoadA(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>LDA (zp),Y (0xB1) - 5 cycles (+1 if page cross) - 65C02</summary>
    private static readonly MicroOp[] Lda_IzY_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZPAddY65C02,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.LoadA(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>LDA (zp) (0xB2) - 5 cycles - 65C02 only</summary>
    private static readonly MicroOp[] Lda_Izp =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZP,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.LoadA(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // LDX Pipelines
    // ========================================

    /// <summary>LDX #imm (0xA2) - 2 cycles</summary>
    private static readonly MicroOp[] Ldx_Imm =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.FetchImmediate(prev, current, bus);
            MicroOps.LoadX(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>LDX zp (0xA6) - 3 cycles</summary>
    private static readonly MicroOp[] Ldx_Zp =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            MicroOps.ReadZeroPage(prev, current, bus);
            MicroOps.LoadX(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>LDX zp,Y (0xB6) - 4 cycles</summary>
    private static readonly MicroOp[] Ldx_ZpY =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddYZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.ReadZeroPage(prev, current, bus);
            MicroOps.LoadX(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>LDX abs (0xAE) - 4 cycles</summary>
    private static readonly MicroOp[] Ldx_Abs =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.LoadX(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>LDX abs,Y (0xBE) - 4 cycles (+1 if page cross) - NMOS</summary>
    private static readonly MicroOp[] Ldx_AbsY =
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
            MicroOps.LoadX(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>LDX abs,Y (0xBE) - 4 cycles (+1 if page cross) - 65C02</summary>
    private static readonly MicroOp[] Ldx_AbsY_65C02 =
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
            MicroOps.LoadX(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // LDY Pipelines
    // ========================================

    /// <summary>LDY #imm (0xA0) - 2 cycles</summary>
    private static readonly MicroOp[] Ldy_Imm =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.FetchImmediate(prev, current, bus);
            MicroOps.LoadY(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>LDY zp (0xA4) - 3 cycles</summary>
    private static readonly MicroOp[] Ldy_Zp =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            MicroOps.ReadZeroPage(prev, current, bus);
            MicroOps.LoadY(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>LDY zp,X (0xB4) - 4 cycles</summary>
    private static readonly MicroOp[] Ldy_ZpX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.ReadZeroPage(prev, current, bus);
            MicroOps.LoadY(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>LDY abs (0xAC) - 4 cycles</summary>
    private static readonly MicroOp[] Ldy_Abs =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.LoadY(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>LDY abs,X (0xBC) - 4 cycles (+1 if page cross) - NMOS</summary>
    private static readonly MicroOp[] Ldy_AbsX =
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
            MicroOps.LoadY(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>LDY abs,X (0xBC) - 4 cycles (+1 if page cross) - 65C02</summary>
    private static readonly MicroOp[] Ldy_AbsX_65C02 =
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
            MicroOps.LoadY(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // STA Pipelines
    // ========================================

    /// <summary>STA zp (0x85) - 3 cycles</summary>
    private static readonly MicroOp[] Sta_Zp =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            MicroOps.StoreA(prev, current, bus);
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>STA zp,X (0x95) - 4 cycles</summary>
    private static readonly MicroOp[] Sta_ZpX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.StoreA(prev, current, bus);
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>STA abs (0x8D) - 4 cycles</summary>
    private static readonly MicroOp[] Sta_Abs =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        (prev, current, bus) =>
        {
            MicroOps.StoreA(prev, current, bus);
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>STA abs,X (0x9D) - 5 cycles - NMOS (dummy read at wrong effective address)</summary>
    private static readonly MicroOp[] Sta_AbsX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.AddXWithDummyRead,
        (prev, current, bus) =>
        {
            MicroOps.StoreA(prev, current, bus);
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>STA abs,X (0x9D) - 5 cycles - 65C02 (dummy read at high byte address)</summary>
    private static readonly MicroOp[] Sta_AbsX_65C02 =
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
        (prev, current, bus) =>
        {
            MicroOps.StoreA(prev, current, bus);
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>STA abs,Y (0x99) - 5 cycles - NMOS (dummy read at wrong effective address)</summary>
    private static readonly MicroOp[] Sta_AbsY =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.AddYWithDummyRead,
        (prev, current, bus) =>
        {
            MicroOps.StoreA(prev, current, bus);
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>STA abs,Y (0x99) - 5 cycles - 65C02 (dummy read at high byte address)</summary>
    private static readonly MicroOp[] Sta_AbsY_65C02 =
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
            // T3: Dummy read at high byte address, then add Y
            bus.CpuRead(current.TempValue);
            current.TempAddress = (ushort)(current.TempAddress + current.Y);
        },
        (prev, current, bus) =>
        {
            MicroOps.StoreA(prev, current, bus);
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>STA (zp,X) (0x81) - 6 cycles</summary>
    private static readonly MicroOp[] Sta_IzX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZP,
        (prev, current, bus) =>
        {
            MicroOps.StoreA(prev, current, bus);
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>STA (zp),Y (0x91) - 6 cycles - NMOS (dummy read at wrong effective address)</summary>
    private static readonly MicroOp[] Sta_IzY =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZP,
        MicroOps.AddYWithDummyRead,
        (prev, current, bus) =>
        {
            MicroOps.StoreA(prev, current, bus);
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>STA (zp),Y (0x91) - 6 cycles - 65C02 (dummy read at operand address)</summary>
    private static readonly MicroOp[] Sta_IzY_65C02 =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            // T1: Fetch ZP operand and save operand PC for later dummy read
            ushort operandAddr = current.PC;
            byte zpAddr = bus.CpuRead(current.PC);
            current.PC++;
            current.TempAddress = zpAddr;
            current.TempValue = operandAddr;
        },
        (prev, current, bus) =>
        {
            // T2: Read pointer low from zp
            byte lo = bus.CpuRead((ushort)(current.TempAddress & 0x00FF));
            // Store ZP addr in upper bits, pointer low in lower bits temporarily
            ushort zpAddr = current.TempAddress;
            current.TempAddress = (ushort)(lo | (zpAddr << 8));
        },
        (prev, current, bus) =>
        {
            // T3: Read pointer high from zp+1, form base address, add Y
            ushort zpAddr = (ushort)(current.TempAddress >> 8);
            byte ptrLo = (byte)current.TempAddress;
            ushort zpHiAddr = (ushort)((zpAddr + 1) & 0x00FF);
            byte ptrHi = bus.CpuRead(zpHiAddr);
            ushort baseAddr = (ushort)(ptrLo | (ptrHi << 8));
            current.TempAddress = (ushort)(baseAddr + current.Y);
        },
        (prev, current, bus) =>
        {
            // T4: Dummy read at operand address (where ZP addr was fetched from)
            bus.CpuRead(current.TempValue);
        },
        (prev, current, bus) =>
        {
            // T5: Write A to effective address
            MicroOps.StoreA(prev, current, bus);
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>STA (zp) (0x92) - 5 cycles - 65C02 only</summary>
    private static readonly MicroOp[] Sta_Izp =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZP,
        (prev, current, bus) =>
        {
            MicroOps.StoreA(prev, current, bus);
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // STX Pipelines
    // ========================================

    /// <summary>STX zp (0x86) - 3 cycles</summary>
    private static readonly MicroOp[] Stx_Zp =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            MicroOps.StoreX(prev, current, bus);
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>STX zp,Y (0x96) - 4 cycles</summary>
    private static readonly MicroOp[] Stx_ZpY =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddYZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.StoreX(prev, current, bus);
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>STX abs (0x8E) - 4 cycles</summary>
    private static readonly MicroOp[] Stx_Abs =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        (prev, current, bus) =>
        {
            MicroOps.StoreX(prev, current, bus);
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // STY Pipelines
    // ========================================

    /// <summary>STY zp (0x84) - 3 cycles</summary>
    private static readonly MicroOp[] Sty_Zp =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            MicroOps.StoreY(prev, current, bus);
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>STY zp,X (0x94) - 4 cycles</summary>
    private static readonly MicroOp[] Sty_ZpX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.StoreY(prev, current, bus);
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>STY abs (0x8C) - 4 cycles</summary>
    private static readonly MicroOp[] Sty_Abs =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        (prev, current, bus) =>
        {
            MicroOps.StoreY(prev, current, bus);
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // STZ Pipelines (65C02 only)
    // ========================================

    /// <summary>STZ zp (0x64) - 3 cycles - 65C02</summary>
    private static readonly MicroOp[] Stz_Zp =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            MicroOps.StoreZ(prev, current, bus);
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>STZ zp,X (0x74) - 4 cycles - 65C02</summary>
    private static readonly MicroOp[] Stz_ZpX =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.StoreZ(prev, current, bus);
            MicroOps.WriteZeroPage(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>STZ abs (0x9C) - 4 cycles - 65C02</summary>
    private static readonly MicroOp[] Stz_Abs =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        (prev, current, bus) =>
        {
            MicroOps.StoreZ(prev, current, bus);
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>STZ abs,X (0x9E) - 5 cycles - 65C02 (dummy read at high byte address)</summary>
    private static readonly MicroOp[] Stz_AbsX =
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
        (prev, current, bus) =>
        {
            MicroOps.StoreZ(prev, current, bus);
            MicroOps.WriteToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // Pipeline Tables
    // ========================================

    /// <summary>
    /// Pipeline table for NMOS 6502 with illegal opcodes.
    /// </summary>
    private static readonly Lazy<MicroOp[][]> LazyPipelines6502 = new(BuildPipelines6502);
    private static MicroOp[][] Pipelines6502 => LazyPipelines6502.Value;

    /// <summary>
    /// Pipeline table for NMOS 6502 without illegal opcodes (treat as NOP).
    /// </summary>
    private static readonly Lazy<MicroOp[][]> LazyPipelines6502NoIllegal = new(BuildPipelines6502NoIllegal);
    private static MicroOp[][] Pipelines6502NoIllegal => LazyPipelines6502NoIllegal.Value;

    /// <summary>
    /// Pipeline table for WDC 65C02.
    /// </summary>
    private static readonly Lazy<MicroOp[][]> LazyPipelines65C02 = new(BuildPipelines65C02);
    private static MicroOp[][] Pipelines65C02 => LazyPipelines65C02.Value;

    /// <summary>
    /// Pipeline table for Rockwell 65C02.
    /// </summary>
    private static readonly Lazy<MicroOp[][]> LazyPipelines65C02Rockwell = new(BuildPipelines65C02Rockwell);
    private static MicroOp[][] Pipelines65C02Rockwell => LazyPipelines65C02Rockwell.Value;

    private static MicroOp[][] BuildPipelines6502()
    {
        var table = new MicroOp[256][];
        for (int i = 0; i < 256; i++)
        {
            table[i] = Unimplemented;
        }

        // Load instructions
        table[0xA9] = Lda_Imm;      // LDA #imm
        table[0xA5] = Lda_Zp;       // LDA zp
        table[0xB5] = Lda_ZpX;      // LDA zp,X
        table[0xAD] = Lda_Abs;      // LDA abs
        table[0xBD] = Lda_AbsX;     // LDA abs,X
        table[0xB9] = Lda_AbsY;     // LDA abs,Y
        table[0xA1] = Lda_IzX;      // LDA (zp,X)
        table[0xB1] = Lda_IzY;      // LDA (zp),Y

        table[0xA2] = Ldx_Imm;      // LDX #imm
        table[0xA6] = Ldx_Zp;       // LDX zp
        table[0xB6] = Ldx_ZpY;      // LDX zp,Y
        table[0xAE] = Ldx_Abs;      // LDX abs
        table[0xBE] = Ldx_AbsY;     // LDX abs,Y

        table[0xA0] = Ldy_Imm;      // LDY #imm
        table[0xA4] = Ldy_Zp;       // LDY zp
        table[0xB4] = Ldy_ZpX;      // LDY zp,X
        table[0xAC] = Ldy_Abs;      // LDY abs
        table[0xBC] = Ldy_AbsX;     // LDY abs,X

        // Store instructions
        table[0x85] = Sta_Zp;       // STA zp
        table[0x95] = Sta_ZpX;      // STA zp,X
        table[0x8D] = Sta_Abs;      // STA abs
        table[0x9D] = Sta_AbsX;     // STA abs,X
        table[0x99] = Sta_AbsY;     // STA abs,Y
        table[0x81] = Sta_IzX;      // STA (zp,X)
        table[0x91] = Sta_IzY;      // STA (zp),Y

        table[0x86] = Stx_Zp;       // STX zp
        table[0x96] = Stx_ZpY;      // STX zp,Y
        table[0x8E] = Stx_Abs;      // STX abs

        table[0x84] = Sty_Zp;       // STY zp
        table[0x94] = Sty_ZpX;      // STY zp,X
        table[0x8C] = Sty_Abs;      // STY abs

        // ADC - NMOS
        table[0x69] = Adc_Imm_Nmos;     // ADC #imm
        table[0x65] = Adc_Zp_Nmos;      // ADC zp
        table[0x75] = Adc_ZpX_Nmos;     // ADC zp,X
        table[0x6D] = Adc_Abs_Nmos;     // ADC abs
        table[0x7D] = Adc_AbsX_Nmos;    // ADC abs,X
        table[0x79] = Adc_AbsY_Nmos;    // ADC abs,Y
        table[0x61] = Adc_IzX_Nmos;     // ADC (zp,X)
        table[0x71] = Adc_IzY_Nmos;     // ADC (zp),Y

        // SBC - NMOS
        table[0xE9] = Sbc_Imm_Nmos;     // SBC #imm
        table[0xE5] = Sbc_Zp_Nmos;      // SBC zp
        table[0xF5] = Sbc_ZpX_Nmos;     // SBC zp,X
        table[0xED] = Sbc_Abs_Nmos;     // SBC abs
        table[0xFD] = Sbc_AbsX_Nmos;    // SBC abs,X
        table[0xF9] = Sbc_AbsY_Nmos;    // SBC abs,Y
        table[0xE1] = Sbc_IzX_Nmos;     // SBC (zp,X)
        table[0xF1] = Sbc_IzY_Nmos;     // SBC (zp),Y

        // AND
        table[0x29] = And_Imm;          // AND #imm
        table[0x25] = And_Zp;           // AND zp
        table[0x35] = And_ZpX;          // AND zp,X
        table[0x2D] = And_Abs;          // AND abs
        table[0x3D] = And_AbsX;         // AND abs,X
        table[0x39] = And_AbsY;         // AND abs,Y
        table[0x21] = And_IzX;          // AND (zp,X)
        table[0x31] = And_IzY;          // AND (zp),Y

        // ORA
        table[0x09] = Ora_Imm;          // ORA #imm
        table[0x05] = Ora_Zp;           // ORA zp
        table[0x15] = Ora_ZpX;          // ORA zp,X
        table[0x0D] = Ora_Abs;          // ORA abs
        table[0x1D] = Ora_AbsX;         // ORA abs,X
        table[0x19] = Ora_AbsY;         // ORA abs,Y
        table[0x01] = Ora_IzX;          // ORA (zp,X)
        table[0x11] = Ora_IzY;          // ORA (zp),Y

        // EOR
        table[0x49] = Eor_Imm;          // EOR #imm
        table[0x45] = Eor_Zp;           // EOR zp
        table[0x55] = Eor_ZpX;          // EOR zp,X
        table[0x4D] = Eor_Abs;          // EOR abs
        table[0x5D] = Eor_AbsX;         // EOR abs,X
        table[0x59] = Eor_AbsY;         // EOR abs,Y
        table[0x41] = Eor_IzX;          // EOR (zp,X)
        table[0x51] = Eor_IzY;          // EOR (zp),Y

        // CMP
        table[0xC9] = Cmp_Imm;          // CMP #imm
        table[0xC5] = Cmp_Zp;           // CMP zp
        table[0xD5] = Cmp_ZpX;          // CMP zp,X
        table[0xCD] = Cmp_Abs;          // CMP abs
        table[0xDD] = Cmp_AbsX;         // CMP abs,X
        table[0xD9] = Cmp_AbsY;         // CMP abs,Y
        table[0xC1] = Cmp_IzX;          // CMP (zp,X)
        table[0xD1] = Cmp_IzY;          // CMP (zp),Y

        // CPX
        table[0xE0] = Cpx_Imm;          // CPX #imm
        table[0xE4] = Cpx_Zp;           // CPX zp
        table[0xEC] = Cpx_Abs;          // CPX abs

        // CPY
        table[0xC0] = Cpy_Imm;          // CPY #imm
        table[0xC4] = Cpy_Zp;           // CPY zp
        table[0xCC] = Cpy_Abs;          // CPY abs

        // BIT
        table[0x24] = Bit_Zp;           // BIT zp
        table[0x2C] = Bit_Abs;          // BIT abs

        // ASL - NMOS
        table[0x0A] = Asl_A;            // ASL A
        table[0x06] = Asl_Zp;           // ASL zp
        table[0x16] = Asl_ZpX;          // ASL zp,X
        table[0x0E] = Asl_Abs;          // ASL abs
        table[0x1E] = Asl_AbsX;         // ASL abs,X

        // LSR - NMOS
        table[0x4A] = Lsr_A;            // LSR A
        table[0x46] = Lsr_Zp;           // LSR zp
        table[0x56] = Lsr_ZpX;          // LSR zp,X
        table[0x4E] = Lsr_Abs;          // LSR abs
        table[0x5E] = Lsr_AbsX;         // LSR abs,X

        // ROL - NMOS
        table[0x2A] = Rol_A;            // ROL A
        table[0x26] = Rol_Zp;           // ROL zp
        table[0x36] = Rol_ZpX;          // ROL zp,X
        table[0x2E] = Rol_Abs;          // ROL abs
        table[0x3E] = Rol_AbsX;         // ROL abs,X

        // ROR - NMOS
        table[0x6A] = Ror_A;            // ROR A
        table[0x66] = Ror_Zp;           // ROR zp
        table[0x76] = Ror_ZpX;          // ROR zp,X
        table[0x6E] = Ror_Abs;          // ROR abs
        table[0x7E] = Ror_AbsX;         // ROR abs,X

        // INC - NMOS
        table[0xE6] = Inc_Zp;           // INC zp
        table[0xF6] = Inc_ZpX;          // INC zp,X
        table[0xEE] = Inc_Abs;          // INC abs
        table[0xFE] = Inc_AbsX;         // INC abs,X

        // DEC - NMOS
        table[0xC6] = Dec_Zp;           // DEC zp
        table[0xD6] = Dec_ZpX;          // DEC zp,X
        table[0xCE] = Dec_Abs;          // DEC abs
        table[0xDE] = Dec_AbsX;         // DEC abs,X

        // Register Inc/Dec
        table[0xE8] = Inx_Impl;         // INX
        table[0xC8] = Iny_Impl;         // INY
        table[0xCA] = Dex_Impl;         // DEX
        table[0x88] = Dey_Impl;         // DEY

        // Branches
        table[0xF0] = Beq;              // BEQ
        table[0xD0] = Bne;              // BNE
        table[0xB0] = Bcs;              // BCS
        table[0x90] = Bcc;              // BCC
        table[0x30] = Bmi;              // BMI
        table[0x10] = Bpl;              // BPL
        table[0x70] = Bvs;              // BVS
        table[0x50] = Bvc;              // BVC

        // Jump/Subroutine
        table[0x4C] = Jmp_Abs;          // JMP abs
        table[0x6C] = Jmp_Ind_Nmos;     // JMP (ind) - NMOS bug
        table[0x20] = Jsr;              // JSR
        table[0x60] = Rts;              // RTS
        table[0x40] = Rti;              // RTI

        // Stack
        table[0x48] = Pha_Impl;         // PHA
        table[0x68] = Pla_Impl;         // PLA
        table[0x08] = Php_Impl;         // PHP
        table[0x28] = Plp_Impl;         // PLP

        // Transfers
        table[0xAA] = Tax_Impl;         // TAX
        table[0x8A] = Txa_Impl;         // TXA
        table[0xA8] = Tay_Impl;         // TAY
        table[0x98] = Tya_Impl;         // TYA
        table[0xBA] = Tsx_Impl;         // TSX
        table[0x9A] = Txs_Impl;         // TXS

        // Flags
        table[0x18] = Clc_Impl;         // CLC
        table[0x38] = Sec_Impl;         // SEC
        table[0xD8] = Cld_Impl;         // CLD
        table[0xF8] = Sed_Impl;         // SED
        table[0x58] = Cli_Impl;         // CLI
        table[0x78] = Sei_Impl;         // SEI
        table[0xB8] = Clv_Impl;         // CLV

        // Special
        table[0x00] = Brk_Nmos;         // BRK
        table[0xEA] = Nop;              // NOP

        // ========================================
        // Illegal Opcodes (NMOS only)
        // ========================================

        // LAX - Load A and X
        table[0xA7] = Lax_Zp;           // LAX zp
        table[0xB7] = Lax_ZpY;          // LAX zp,Y
        table[0xAF] = Lax_Abs;          // LAX abs
        table[0xBF] = Lax_AbsY;         // LAX abs,Y
        table[0xA3] = Lax_IzX;          // LAX (zp,X)
        table[0xB3] = Lax_IzY;          // LAX (zp),Y

        // SAX - Store A AND X
        table[0x87] = Sax_Zp;           // SAX zp
        table[0x97] = Sax_ZpY;          // SAX zp,Y
        table[0x8F] = Sax_Abs;          // SAX abs
        table[0x83] = Sax_IzX;          // SAX (zp,X)

        // DCP - Decrement then Compare
        table[0xC7] = Dcp_Zp;           // DCP zp
        table[0xD7] = Dcp_ZpX;          // DCP zp,X
        table[0xCF] = Dcp_Abs;          // DCP abs
        table[0xDF] = Dcp_AbsX;         // DCP abs,X
        table[0xDB] = Dcp_AbsY;         // DCP abs,Y
        table[0xC3] = Dcp_IzX;          // DCP (zp,X)
        table[0xD3] = Dcp_IzY;          // DCP (zp),Y

        // ISC/ISB - Increment then Subtract
        table[0xE7] = Isc_Zp;           // ISC zp
        table[0xF7] = Isc_ZpX;          // ISC zp,X
        table[0xEF] = Isc_Abs;          // ISC abs
        table[0xFF] = Isc_AbsX;         // ISC abs,X
        table[0xFB] = Isc_AbsY;         // ISC abs,Y
        table[0xE3] = Isc_IzX;          // ISC (zp,X)
        table[0xF3] = Isc_IzY;          // ISC (zp),Y

        // SLO - ASL then ORA
        table[0x07] = Slo_Zp;           // SLO zp
        table[0x17] = Slo_ZpX;          // SLO zp,X
        table[0x0F] = Slo_Abs;          // SLO abs
        table[0x1F] = Slo_AbsX;         // SLO abs,X
        table[0x1B] = Slo_AbsY;         // SLO abs,Y
        table[0x03] = Slo_IzX;          // SLO (zp,X)
        table[0x13] = Slo_IzY;          // SLO (zp),Y

        // RLA - Rotate Left then AND
        table[0x27] = Rla_Zp;           // RLA zp
        table[0x37] = Rla_ZpX;          // RLA zp,X
        table[0x2F] = Rla_Abs;          // RLA abs
        table[0x3F] = Rla_AbsX;         // RLA abs,X
        table[0x3B] = Rla_AbsY;         // RLA abs,Y
        table[0x23] = Rla_IzX;          // RLA (zp,X)
        table[0x33] = Rla_IzY;          // RLA (zp),Y

        // SRE - Shift Right then EOR
        table[0x47] = Sre_Zp;           // SRE zp
        table[0x57] = Sre_ZpX;          // SRE zp,X
        table[0x4F] = Sre_Abs;          // SRE abs
        table[0x5F] = Sre_AbsX;         // SRE abs,X
        table[0x5B] = Sre_AbsY;         // SRE abs,Y
        table[0x43] = Sre_IzX;          // SRE (zp,X)
        table[0x53] = Sre_IzY;          // SRE (zp),Y

        // RRA - Rotate Right then ADC
        table[0x67] = Rra_Zp;           // RRA zp
        table[0x77] = Rra_ZpX;          // RRA zp,X
        table[0x6F] = Rra_Abs;          // RRA abs
        table[0x7F] = Rra_AbsX;         // RRA abs,X
        table[0x7B] = Rra_AbsY;         // RRA abs,Y
        table[0x63] = Rra_IzX;          // RRA (zp,X)
        table[0x73] = Rra_IzY;          // RRA (zp),Y

        // ANC - AND immediate, set C from bit 7
        table[0x0B] = Anc_Imm;          // ANC #imm
        table[0x2B] = Anc_Imm;          // ANC #imm (duplicate)

        // ALR - AND immediate then LSR
        table[0x4B] = Alr_Imm;          // ALR #imm

        // ARR - AND immediate then ROR
        table[0x6B] = Arr_Imm;          // ARR #imm

        // AXS/SBX - X = (A AND X) - immediate
        table[0xCB] = Axs_Imm;          // AXS #imm

        // ANE/XAA - unstable
        table[0x8B] = Ane_Imm;          // ANE #imm

        // LXA/LAX #imm - unstable
        table[0xAB] = Lxa_Imm;          // LXA #imm

        // LAS - A = X = SP = SP & mem
        table[0xBB] = Las_AbsY;         // LAS abs,Y

        // SHA/AHX - unstable store ops
        table[0x93] = Sha_IzY;          // SHA (zp),Y
        table[0x9F] = Sha_AbsY;         // SHA abs,Y

        // SHX - Store X & (high+1)
        table[0x9E] = Shx_AbsY;         // SHX abs,Y

        // SHY - Store Y & (high+1)
        table[0x9C] = Shy_AbsX;         // SHY abs,X

        // TAS/SHS - SP = A & X, store
        table[0x9B] = Tas_AbsY;         // TAS abs,Y

        // SBC unofficial duplicate
        table[0xEB] = Sbc_Imm_Unofficial; // SBC #imm (unofficial)

        // NOP variants (multi-byte NOPs)
        table[0x80] = Nop_Imm;          // NOP #imm
        table[0x82] = Nop_Imm;          // NOP #imm
        table[0x89] = Nop_Imm;          // NOP #imm
        table[0xC2] = Nop_Imm;          // NOP #imm
        table[0xE2] = Nop_Imm;          // NOP #imm
        table[0x04] = Nop_Zp;           // NOP zp
        table[0x44] = Nop_Zp;           // NOP zp
        table[0x64] = Nop_Zp;           // NOP zp
        table[0x14] = Nop_ZpX;          // NOP zp,X
        table[0x34] = Nop_ZpX;          // NOP zp,X
        table[0x54] = Nop_ZpX;          // NOP zp,X
        table[0x74] = Nop_ZpX;          // NOP zp,X
        table[0xD4] = Nop_ZpX;          // NOP zp,X
        table[0xF4] = Nop_ZpX;          // NOP zp,X
        table[0x0C] = Nop_Abs;          // NOP abs
        table[0x1C] = Nop_AbsX;         // NOP abs,X
        table[0x3C] = Nop_AbsX;         // NOP abs,X
        table[0x5C] = Nop_AbsX;         // NOP abs,X
        table[0x7C] = Nop_AbsX;         // NOP abs,X
        table[0xDC] = Nop_AbsX;         // NOP abs,X
        table[0xFC] = Nop_AbsX;         // NOP abs,X
        table[0x1A] = Nop;              // NOP (1-byte)
        table[0x3A] = Nop;              // NOP (1-byte)
        table[0x5A] = Nop;              // NOP (1-byte)
        table[0x7A] = Nop;              // NOP (1-byte)
        table[0xDA] = Nop;              // NOP (1-byte)
        table[0xFA] = Nop;              // NOP (1-byte)

        // JAM - CPU halt (multiple opcodes)
        table[0x02] = Jam;
        table[0x12] = Jam;
        table[0x22] = Jam;
        table[0x32] = Jam;
        table[0x42] = Jam;
        table[0x52] = Jam;
        table[0x62] = Jam;
        table[0x72] = Jam;
        table[0x92] = Jam;
        table[0xB2] = Jam;
        table[0xD2] = Jam;
        table[0xF2] = Jam;

        return table;
    }

    private static MicroOp[][] BuildPipelines6502NoIllegal()
    {
        // Start with same base as 6502
        var table = BuildPipelines6502();

        // Override illegal opcodes with Unimplemented (2-cycle NOP)
        // LAX
        table[0xA7] = Unimplemented;
        table[0xB7] = Unimplemented;
        table[0xAF] = Unimplemented;
        table[0xBF] = Unimplemented;
        table[0xA3] = Unimplemented;
        table[0xB3] = Unimplemented;
        // SAX
        table[0x87] = Unimplemented;
        table[0x97] = Unimplemented;
        table[0x8F] = Unimplemented;
        table[0x83] = Unimplemented;
        // DCP
        table[0xC7] = Unimplemented;
        table[0xD7] = Unimplemented;
        table[0xCF] = Unimplemented;
        table[0xDF] = Unimplemented;
        table[0xDB] = Unimplemented;
        table[0xC3] = Unimplemented;
        table[0xD3] = Unimplemented;
        // ISC
        table[0xE7] = Unimplemented;
        table[0xF7] = Unimplemented;
        table[0xEF] = Unimplemented;
        table[0xFF] = Unimplemented;
        table[0xFB] = Unimplemented;
        table[0xE3] = Unimplemented;
        table[0xF3] = Unimplemented;
        // SLO
        table[0x07] = Unimplemented;
        table[0x17] = Unimplemented;
        table[0x0F] = Unimplemented;
        table[0x1F] = Unimplemented;
        table[0x1B] = Unimplemented;
        table[0x03] = Unimplemented;
        table[0x13] = Unimplemented;
        // RLA
        table[0x27] = Unimplemented;
        table[0x37] = Unimplemented;
        table[0x2F] = Unimplemented;
        table[0x3F] = Unimplemented;
        table[0x3B] = Unimplemented;
        table[0x23] = Unimplemented;
        table[0x33] = Unimplemented;
        // SRE
        table[0x47] = Unimplemented;
        table[0x57] = Unimplemented;
        table[0x4F] = Unimplemented;
        table[0x5F] = Unimplemented;
        table[0x5B] = Unimplemented;
        table[0x43] = Unimplemented;
        table[0x53] = Unimplemented;
        // RRA
        table[0x67] = Unimplemented;
        table[0x77] = Unimplemented;
        table[0x6F] = Unimplemented;
        table[0x7F] = Unimplemented;
        table[0x7B] = Unimplemented;
        table[0x63] = Unimplemented;
        table[0x73] = Unimplemented;
        // ANC
        table[0x0B] = Unimplemented;
        table[0x2B] = Unimplemented;
        // ALR
        table[0x4B] = Unimplemented;
        // ARR
        table[0x6B] = Unimplemented;
        // AXS/SBX
        table[0xCB] = Unimplemented;
        // ANE/XAA
        table[0x8B] = Unimplemented;
        // LXA/LAX #imm
        table[0xAB] = Unimplemented;
        // LAS
        table[0xBB] = Unimplemented;
        // SHA/AHX
        table[0x93] = Unimplemented;
        table[0x9F] = Unimplemented;
        // SHX
        table[0x9E] = Unimplemented;
        // SHY
        table[0x9C] = Unimplemented;
        // TAS/SHS
        table[0x9B] = Unimplemented;
        // SBC unofficial
        table[0xEB] = Unimplemented;
        // JAM
        table[0x02] = Unimplemented;
        table[0x12] = Unimplemented;
        table[0x22] = Unimplemented;
        table[0x32] = Unimplemented;
        table[0x42] = Unimplemented;
        table[0x52] = Unimplemented;
        table[0x62] = Unimplemented;
        table[0x72] = Unimplemented;
        table[0x92] = Unimplemented;
        table[0xB2] = Unimplemented;
        table[0xD2] = Unimplemented;
        table[0xF2] = Unimplemented;

        return table;
    }

    private static MicroOp[][] BuildPipelines65C02()
    {
        var table = new MicroOp[256][];
        for (int i = 0; i < 256; i++)
        {
            table[i] = Unimplemented;
        }

        // Load instructions - 65C02 uses different page crossing behavior
        table[0xA9] = Lda_Imm;          // LDA #imm
        table[0xA5] = Lda_Zp;           // LDA zp
        table[0xB5] = Lda_ZpX;          // LDA zp,X
        table[0xAD] = Lda_Abs;          // LDA abs
        table[0xBD] = Lda_AbsX_65C02;   // LDA abs,X
        table[0xB9] = Lda_AbsY_65C02;   // LDA abs,Y
        table[0xA1] = Lda_IzX;          // LDA (zp,X)
        table[0xB1] = Lda_IzY_65C02;    // LDA (zp),Y
        table[0xB2] = Lda_Izp;          // LDA (zp) - 65C02 only

        table[0xA2] = Ldx_Imm;          // LDX #imm
        table[0xA6] = Ldx_Zp;           // LDX zp
        table[0xB6] = Ldx_ZpY;          // LDX zp,Y
        table[0xAE] = Ldx_Abs;          // LDX abs
        table[0xBE] = Ldx_AbsY_65C02;   // LDX abs,Y

        table[0xA0] = Ldy_Imm;          // LDY #imm
        table[0xA4] = Ldy_Zp;           // LDY zp
        table[0xB4] = Ldy_ZpX;          // LDY zp,X
        table[0xAC] = Ldy_Abs;          // LDY abs
        table[0xBC] = Ldy_AbsX_65C02;   // LDY abs,X

        // Store instructions - 65C02 uses different dummy read behavior
        table[0x85] = Sta_Zp;           // STA zp
        table[0x95] = Sta_ZpX;          // STA zp,X
        table[0x8D] = Sta_Abs;          // STA abs
        table[0x9D] = Sta_AbsX_65C02;   // STA abs,X
        table[0x99] = Sta_AbsY_65C02;   // STA abs,Y
        table[0x81] = Sta_IzX;          // STA (zp,X)
        table[0x91] = Sta_IzY_65C02;    // STA (zp),Y
        table[0x92] = Sta_Izp;          // STA (zp) - 65C02 only

        table[0x86] = Stx_Zp;           // STX zp
        table[0x96] = Stx_ZpY;          // STX zp,Y
        table[0x8E] = Stx_Abs;          // STX abs

        table[0x84] = Sty_Zp;           // STY zp
        table[0x94] = Sty_ZpX;          // STY zp,X
        table[0x8C] = Sty_Abs;          // STY abs

        // STZ - 65C02 only
        table[0x64] = Stz_Zp;           // STZ zp
        table[0x74] = Stz_ZpX;          // STZ zp,X
        table[0x9C] = Stz_Abs;          // STZ abs
        table[0x9E] = Stz_AbsX;         // STZ abs,X

        // ADC - 65C02 (with decimal mode penalty cycle)
        table[0x69] = Adc_Imm_Cmos;     // ADC #imm
        table[0x65] = Adc_Zp_Cmos;      // ADC zp
        table[0x75] = Adc_ZpX_Cmos;     // ADC zp,X
        table[0x6D] = Adc_Abs_Cmos;     // ADC abs
        table[0x7D] = Adc_AbsX_65C02;   // ADC abs,X
        table[0x79] = Adc_AbsY_65C02;   // ADC abs,Y
        table[0x61] = Adc_IzX_Cmos;     // ADC (zp,X)
        table[0x71] = Adc_IzY_65C02;    // ADC (zp),Y
        table[0x72] = Adc_Izp_Cmos;     // ADC (zp) - 65C02 only

        // SBC - 65C02 (with decimal mode penalty cycle)
        table[0xE9] = Sbc_Imm_Cmos;     // SBC #imm
        table[0xE5] = Sbc_Zp_Cmos;      // SBC zp
        table[0xF5] = Sbc_ZpX_Cmos;     // SBC zp,X
        table[0xED] = Sbc_Abs_Cmos;     // SBC abs
        table[0xFD] = Sbc_AbsX_65C02;   // SBC abs,X
        table[0xF9] = Sbc_AbsY_65C02;   // SBC abs,Y
        table[0xE1] = Sbc_IzX_Cmos;     // SBC (zp,X)
        table[0xF1] = Sbc_IzY_65C02;    // SBC (zp),Y
        table[0xF2] = Sbc_Izp_Cmos;     // SBC (zp) - 65C02 only

        // AND - 65C02
        table[0x29] = And_Imm;          // AND #imm
        table[0x25] = And_Zp;           // AND zp
        table[0x35] = And_ZpX;          // AND zp,X
        table[0x2D] = And_Abs;          // AND abs
        table[0x3D] = And_AbsX_65C02;   // AND abs,X
        table[0x39] = And_AbsY_65C02;   // AND abs,Y
        table[0x21] = And_IzX;          // AND (zp,X)
        table[0x31] = And_IzY_65C02;    // AND (zp),Y
        table[0x32] = And_Izp;          // AND (zp) - 65C02 only

        // ORA - 65C02
        table[0x09] = Ora_Imm;          // ORA #imm
        table[0x05] = Ora_Zp;           // ORA zp
        table[0x15] = Ora_ZpX;          // ORA zp,X
        table[0x0D] = Ora_Abs;          // ORA abs
        table[0x1D] = Ora_AbsX_65C02;   // ORA abs,X
        table[0x19] = Ora_AbsY_65C02;   // ORA abs,Y
        table[0x01] = Ora_IzX;          // ORA (zp,X)
        table[0x11] = Ora_IzY_65C02;    // ORA (zp),Y
        table[0x12] = Ora_Izp;          // ORA (zp) - 65C02 only

        // EOR - 65C02
        table[0x49] = Eor_Imm;          // EOR #imm
        table[0x45] = Eor_Zp;           // EOR zp
        table[0x55] = Eor_ZpX;          // EOR zp,X
        table[0x4D] = Eor_Abs;          // EOR abs
        table[0x5D] = Eor_AbsX_65C02;   // EOR abs,X
        table[0x59] = Eor_AbsY_65C02;   // EOR abs,Y
        table[0x41] = Eor_IzX;          // EOR (zp,X)
        table[0x51] = Eor_IzY_65C02;    // EOR (zp),Y
        table[0x52] = Eor_Izp;          // EOR (zp) - 65C02 only

        // CMP - 65C02
        table[0xC9] = Cmp_Imm;          // CMP #imm
        table[0xC5] = Cmp_Zp;           // CMP zp
        table[0xD5] = Cmp_ZpX;          // CMP zp,X
        table[0xCD] = Cmp_Abs;          // CMP abs
        table[0xDD] = Cmp_AbsX_65C02;   // CMP abs,X
        table[0xD9] = Cmp_AbsY_65C02;   // CMP abs,Y
        table[0xC1] = Cmp_IzX;          // CMP (zp,X)
        table[0xD1] = Cmp_IzY_65C02;    // CMP (zp),Y
        table[0xD2] = Cmp_Izp;          // CMP (zp) - 65C02 only

        // CPX
        table[0xE0] = Cpx_Imm;          // CPX #imm
        table[0xE4] = Cpx_Zp;           // CPX zp
        table[0xEC] = Cpx_Abs;          // CPX abs

        // CPY
        table[0xC0] = Cpy_Imm;          // CPY #imm
        table[0xC4] = Cpy_Zp;           // CPY zp
        table[0xCC] = Cpy_Abs;          // CPY abs

        // BIT - 65C02 (includes new addressing modes)
        table[0x89] = Bit_Imm;          // BIT #imm - 65C02 only
        table[0x24] = Bit_Zp;           // BIT zp
        table[0x34] = Bit_ZpX;          // BIT zp,X - 65C02 only
        table[0x2C] = Bit_Abs;          // BIT abs
        table[0x3C] = Bit_AbsX_65C02;   // BIT abs,X - 65C02 only

        // ASL - 65C02 (dummy READ instead of write)
        table[0x0A] = Asl_A;            // ASL A
        table[0x06] = Asl_Zp_65C02;     // ASL zp
        table[0x16] = Asl_ZpX_65C02;    // ASL zp,X
        table[0x0E] = Asl_Abs_65C02;    // ASL abs
        table[0x1E] = Asl_AbsX_65C02;   // ASL abs,X

        // LSR - 65C02 (dummy READ instead of write)
        table[0x4A] = Lsr_A;            // LSR A
        table[0x46] = Lsr_Zp_65C02;     // LSR zp
        table[0x56] = Lsr_ZpX_65C02;    // LSR zp,X
        table[0x4E] = Lsr_Abs_65C02;    // LSR abs
        table[0x5E] = Lsr_AbsX_65C02;   // LSR abs,X

        // ROL - 65C02 (dummy READ instead of write)
        table[0x2A] = Rol_A;            // ROL A
        table[0x26] = Rol_Zp_65C02;     // ROL zp
        table[0x36] = Rol_ZpX_65C02;    // ROL zp,X
        table[0x2E] = Rol_Abs_65C02;    // ROL abs
        table[0x3E] = Rol_AbsX_65C02;   // ROL abs,X

        // ROR - 65C02 (dummy READ instead of write)
        table[0x6A] = Ror_A;            // ROR A
        table[0x66] = Ror_Zp_65C02;     // ROR zp
        table[0x76] = Ror_ZpX_65C02;    // ROR zp,X
        table[0x6E] = Ror_Abs_65C02;    // ROR abs
        table[0x7E] = Ror_AbsX_65C02;   // ROR abs,X

        // INC - 65C02 (dummy READ instead of write)
        table[0xE6] = Inc_Zp_65C02;     // INC zp
        table[0xF6] = Inc_ZpX_65C02;    // INC zp,X
        table[0xEE] = Inc_Abs_65C02;    // INC abs
        table[0xFE] = Inc_AbsX_65C02;   // INC abs,X
        table[0x1A] = Inc_A;            // INC A - 65C02 only

        // DEC - 65C02 (dummy READ instead of write)
        table[0xC6] = Dec_Zp_65C02;     // DEC zp
        table[0xD6] = Dec_ZpX_65C02;    // DEC zp,X
        table[0xCE] = Dec_Abs_65C02;    // DEC abs
        table[0xDE] = Dec_AbsX_65C02;   // DEC abs,X
        table[0x3A] = Dec_A;            // DEC A - 65C02 only

        // Register Inc/Dec
        table[0xE8] = Inx_Impl;         // INX
        table[0xC8] = Iny_Impl;         // INY
        table[0xCA] = Dex_Impl;         // DEX
        table[0x88] = Dey_Impl;         // DEY

        // Branches
        table[0xF0] = Beq;              // BEQ
        table[0xD0] = Bne;              // BNE
        table[0xB0] = Bcs;              // BCS
        table[0x90] = Bcc;              // BCC
        table[0x30] = Bmi;              // BMI
        table[0x10] = Bpl;              // BPL
        table[0x70] = Bvs;              // BVS
        table[0x50] = Bvc;              // BVC
        table[0x80] = Bra;              // BRA - 65C02 only

        // Jump/Subroutine
        table[0x4C] = Jmp_Abs;          // JMP abs
        table[0x6C] = Jmp_Ind_65C02;    // JMP (ind) - 65C02 fixes bug
        table[0x7C] = Jmp_AbsX_Ind;     // JMP (abs,X) - 65C02 only
        table[0x20] = Jsr;              // JSR
        table[0x60] = Rts;              // RTS
        table[0x40] = Rti;              // RTI

        // Stack
        table[0x48] = Pha_Impl;         // PHA
        table[0x68] = Pla_Impl;         // PLA
        table[0x08] = Php_Impl;         // PHP
        table[0x28] = Plp_Impl;         // PLP
        table[0xDA] = Phx_Impl;         // PHX - 65C02 only
        table[0xFA] = Plx_Impl;         // PLX - 65C02 only
        table[0x5A] = Phy_Impl;         // PHY - 65C02 only
        table[0x7A] = Ply_Impl;         // PLY - 65C02 only

        // Transfers
        table[0xAA] = Tax_Impl;         // TAX
        table[0x8A] = Txa_Impl;         // TXA
        table[0xA8] = Tay_Impl;         // TAY
        table[0x98] = Tya_Impl;         // TYA
        table[0xBA] = Tsx_Impl;         // TSX
        table[0x9A] = Txs_Impl;         // TXS

        // Flags
        table[0x18] = Clc_Impl;         // CLC
        table[0x38] = Sec_Impl;         // SEC
        table[0xD8] = Cld_Impl;         // CLD
        table[0xF8] = Sed_Impl;         // SED
        table[0x58] = Cli_Impl;         // CLI
        table[0x78] = Sei_Impl;         // SEI
        table[0xB8] = Clv_Impl;         // CLV

        // Special
        table[0x00] = Brk_65C02;        // BRK - 65C02 clears D
        table[0xEA] = Nop;              // NOP
        table[0xDB] = Stp;              // STP - 65C02 only
        table[0xCB] = Wai;              // WAI - 65C02 only

        // TRB/TSB - 65C02 only
        table[0x14] = Trb_Zp;           // TRB zp
        table[0x1C] = Trb_Abs;          // TRB abs
        table[0x04] = Tsb_Zp;           // TSB zp
        table[0x0C] = Tsb_Abs;          // TSB abs

        // 65C02 1-cycle NOPs (undefined opcodes)
        // $x3 pattern (1-cycle)
        table[0x03] = Nop_1Cycle;
        table[0x13] = Nop_1Cycle;
        table[0x23] = Nop_1Cycle;
        table[0x33] = Nop_1Cycle;
        table[0x43] = Nop_1Cycle;
        table[0x53] = Nop_1Cycle;
        table[0x63] = Nop_1Cycle;
        table[0x73] = Nop_1Cycle;
        table[0x83] = Nop_1Cycle;
        table[0x93] = Nop_1Cycle;
        table[0xA3] = Nop_1Cycle;
        table[0xB3] = Nop_1Cycle;
        table[0xC3] = Nop_1Cycle;
        table[0xD3] = Nop_1Cycle;
        table[0xE3] = Nop_1Cycle;
        table[0xF3] = Nop_1Cycle;
        // $xB pattern (1-cycle, except $CB=WAI, $DB=STP)
        table[0x0B] = Nop_1Cycle;
        table[0x1B] = Nop_1Cycle;
        table[0x2B] = Nop_1Cycle;
        table[0x3B] = Nop_1Cycle;
        table[0x4B] = Nop_1Cycle;
        table[0x5B] = Nop_1Cycle;
        table[0x6B] = Nop_1Cycle;
        table[0x7B] = Nop_1Cycle;
        table[0x8B] = Nop_1Cycle;
        table[0x9B] = Nop_1Cycle;
        table[0xAB] = Nop_1Cycle;
        table[0xBB] = Nop_1Cycle;
        table[0xEB] = Nop_1Cycle;
        table[0xFB] = Nop_1Cycle;
        // RMB - Reset Memory Bit (65C02)
        table[0x07] = Rmb0;
        table[0x17] = Rmb1;
        table[0x27] = Rmb2;
        table[0x37] = Rmb3;
        table[0x47] = Rmb4;
        table[0x57] = Rmb5;
        table[0x67] = Rmb6;
        table[0x77] = Rmb7;
        // SMB - Set Memory Bit (65C02)
        table[0x87] = Smb0;
        table[0x97] = Smb1;
        table[0xA7] = Smb2;
        table[0xB7] = Smb3;
        table[0xC7] = Smb4;
        table[0xD7] = Smb5;
        table[0xE7] = Smb6;
        table[0xF7] = Smb7;
        // BBR - Branch on Bit Reset (65C02)
        table[0x0F] = Bbr0;
        table[0x1F] = Bbr1;
        table[0x2F] = Bbr2;
        table[0x3F] = Bbr3;
        table[0x4F] = Bbr4;
        table[0x5F] = Bbr5;
        table[0x6F] = Bbr6;
        table[0x7F] = Bbr7;
        // BBS - Branch on Bit Set (65C02)
        table[0x8F] = Bbs0;
        table[0x9F] = Bbs1;
        table[0xAF] = Bbs2;
        table[0xBF] = Bbs3;
        table[0xCF] = Bbs4;
        table[0xDF] = Bbs5;
        table[0xEF] = Bbs6;
        table[0xFF] = Bbs7;
        // $x2 pattern (2-cycle immediate style - skips next byte)
        table[0x02] = Nop_Imm;
        table[0x22] = Nop_Imm;
        table[0x42] = Nop_Imm;
        table[0x62] = Nop_Imm;
        table[0x82] = Nop_Imm;
        table[0xC2] = Nop_Imm;
        table[0xE2] = Nop_Imm;
        // $44 (3-cycle zero page style)
        table[0x44] = Nop_Zp;
        // $x4 pattern for ZP,X style (4-cycle)
        table[0x54] = Nop_ZpX;
        table[0xD4] = Nop_ZpX;
        table[0xF4] = Nop_ZpX;
        // $xC pattern for absolute style (4-cycle) - reads high byte address twice
        table[0x5C] = Nop_Abs_65C02;
        table[0xDC] = Nop_Abs_65C02;
        table[0xFC] = Nop_Abs_65C02;

        return table;
    }

    private static MicroOp[][] BuildPipelines65C02Rockwell()
    {
        // Start with WDC 65C02 base (which already has RMB/SMB/BBR/BBS)
        var table = BuildPipelines65C02();

        // Rockwell uses different penalty address for ADC immediate
        table[0x69] = Adc_Imm_Rockwell;

        // Rockwell does NOT have WAI ($CB) and STP ($DB) - they are NOPs
        table[0xCB] = Nop;              // $CB is a 2-cycle NOP on Rockwell
        table[0xDB] = Nop_ZpX;          // $DB is a 4-cycle NOP on Rockwell (ZP,X style)

        return table;
    }
}
