// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Cpu.Internals;

internal static partial class Pipelines
{
    // ========================================
    // Helper for 65C02 Decimal Mode Penalty
    // ========================================

    /// <summary>
    /// Adds a decimal mode penalty cycle for 65C02 ADC/SBC if D flag is set.
    /// </summary>
    private static void AddDecimalPenaltyAndComplete(CpuState state)
    {
        if (state.DecimalFlag)
        {
            ushort operandAddr = state.TempAddress;
            MicroOp penaltyOp = (prev, current, bus) =>
            {
                bus.CpuRead(operandAddr);
                current.InstructionComplete = true;
            };
            MicroOps.InsertAfterCurrentOp(state, penaltyOp);
        }
        else
        {
            state.InstructionComplete = true;
        }
    }

    // ========================================
    // ADC Pipelines - NMOS
    // ========================================

    /// <summary>ADC #imm (0x69) - 2 cycles - NMOS</summary>
    private static readonly MicroOp[] Adc_Imm_Nmos =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.FetchImmediate(prev, current, bus);
            MicroOps.AdcNmos(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ADC zp (0x65) - 3 cycles - NMOS</summary>
    private static readonly MicroOp[] Adc_Zp_Nmos =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            MicroOps.ReadZeroPage(prev, current, bus);
            MicroOps.AdcNmos(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ADC zp,X (0x75) - 4 cycles - NMOS</summary>
    private static readonly MicroOp[] Adc_ZpX_Nmos =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.ReadZeroPage(prev, current, bus);
            MicroOps.AdcNmos(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ADC abs (0x6D) - 4 cycles - NMOS</summary>
    private static readonly MicroOp[] Adc_Abs_Nmos =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.AdcNmos(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ADC abs,X (0x7D) - 4 cycles (+1 if page cross) - NMOS</summary>
    private static readonly MicroOp[] Adc_AbsX_Nmos =
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
            MicroOps.AdcNmos(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ADC abs,Y (0x79) - 4 cycles (+1 if page cross) - NMOS</summary>
    private static readonly MicroOp[] Adc_AbsY_Nmos =
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
            MicroOps.AdcNmos(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ADC (zp,X) (0x61) - 6 cycles - NMOS</summary>
    private static readonly MicroOp[] Adc_IzX_Nmos =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZP,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.AdcNmos(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>ADC (zp),Y (0x71) - 5 cycles (+1 if page cross) - NMOS</summary>
    private static readonly MicroOp[] Adc_IzY_Nmos =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZPAddY,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.AdcNmos(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // ADC Pipelines - CMOS (65C02)
    // ========================================

    /// <summary>ADC #imm (0x69) - 2 cycles (+1 if decimal) - 65C02/WDC</summary>
    private static readonly MicroOp[] Adc_Imm_Cmos =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.FetchImmediate(prev, current, bus);
            MicroOps.AdcCmos(prev, current, bus);
            current.TempAddress = 0x007F; // WDC decimal penalty address
            AddDecimalPenaltyAndComplete(current);
        }
    ];

    /// <summary>ADC #imm (0x69) - 2 cycles (+1 if decimal) - Rockwell</summary>
    private static readonly MicroOp[] Adc_Imm_Rockwell =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.FetchImmediate(prev, current, bus);
            MicroOps.AdcCmos(prev, current, bus);
            current.TempAddress = 0x0059; // Rockwell decimal penalty address
            AddDecimalPenaltyAndComplete(current);
        }
    ];

    /// <summary>ADC zp (0x65) - 3 cycles (+1 if decimal) - 65C02</summary>
    private static readonly MicroOp[] Adc_Zp_Cmos =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            MicroOps.ReadZeroPage(prev, current, bus);
            MicroOps.AdcCmos(prev, current, bus);
            AddDecimalPenaltyAndComplete(current);
        }
    ];

    /// <summary>ADC zp,X (0x75) - 4 cycles (+1 if decimal) - 65C02</summary>
    private static readonly MicroOp[] Adc_ZpX_Cmos =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.ReadZeroPage(prev, current, bus);
            MicroOps.AdcCmos(prev, current, bus);
            AddDecimalPenaltyAndComplete(current);
        }
    ];

    /// <summary>ADC abs (0x6D) - 4 cycles (+1 if decimal) - 65C02</summary>
    private static readonly MicroOp[] Adc_Abs_Cmos =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.AdcCmos(prev, current, bus);
            AddDecimalPenaltyAndComplete(current);
        }
    ];

    /// <summary>ADC abs,X (0x7D) - 4 cycles (+1 if page cross, +1 if decimal) - 65C02</summary>
    private static readonly MicroOp[] Adc_AbsX_65C02 =
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
            MicroOps.AdcCmos(prev, current, bus);
            AddDecimalPenaltyAndComplete(current);
        }
    ];

    /// <summary>ADC abs,Y (0x79) - 4 cycles (+1 if page cross, +1 if decimal) - 65C02</summary>
    private static readonly MicroOp[] Adc_AbsY_65C02 =
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
            MicroOps.AdcCmos(prev, current, bus);
            AddDecimalPenaltyAndComplete(current);
        }
    ];

    /// <summary>ADC (zp,X) (0x61) - 6 cycles (+1 if decimal) - 65C02</summary>
    private static readonly MicroOp[] Adc_IzX_Cmos =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZP,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.AdcCmos(prev, current, bus);
            AddDecimalPenaltyAndComplete(current);
        }
    ];

    /// <summary>ADC (zp),Y (0x71) - 5 cycles (+1 if page cross, +1 if decimal) - 65C02</summary>
    private static readonly MicroOp[] Adc_IzY_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZPAddY65C02,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.AdcCmos(prev, current, bus);
            AddDecimalPenaltyAndComplete(current);
        }
    ];

    /// <summary>ADC (zp) (0x72) - 5 cycles (+1 if decimal) - 65C02 only</summary>
    private static readonly MicroOp[] Adc_Izp_Cmos =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZP,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.AdcCmos(prev, current, bus);
            AddDecimalPenaltyAndComplete(current);
        }
    ];

    // ========================================
    // SBC Pipelines - NMOS
    // ========================================

    /// <summary>SBC #imm (0xE9) - 2 cycles - NMOS</summary>
    private static readonly MicroOp[] Sbc_Imm_Nmos =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.FetchImmediate(prev, current, bus);
            MicroOps.SbcNmos(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>SBC zp (0xE5) - 3 cycles - NMOS</summary>
    private static readonly MicroOp[] Sbc_Zp_Nmos =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            MicroOps.ReadZeroPage(prev, current, bus);
            MicroOps.SbcNmos(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>SBC zp,X (0xF5) - 4 cycles - NMOS</summary>
    private static readonly MicroOp[] Sbc_ZpX_Nmos =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.ReadZeroPage(prev, current, bus);
            MicroOps.SbcNmos(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>SBC abs (0xED) - 4 cycles - NMOS</summary>
    private static readonly MicroOp[] Sbc_Abs_Nmos =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.SbcNmos(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>SBC abs,X (0xFD) - 4 cycles (+1 if page cross) - NMOS</summary>
    private static readonly MicroOp[] Sbc_AbsX_Nmos =
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
            MicroOps.SbcNmos(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>SBC abs,Y (0xF9) - 4 cycles (+1 if page cross) - NMOS</summary>
    private static readonly MicroOp[] Sbc_AbsY_Nmos =
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
            MicroOps.SbcNmos(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>SBC (zp,X) (0xE1) - 6 cycles - NMOS</summary>
    private static readonly MicroOp[] Sbc_IzX_Nmos =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZP,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.SbcNmos(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>SBC (zp),Y (0xF1) - 5 cycles (+1 if page cross) - NMOS</summary>
    private static readonly MicroOp[] Sbc_IzY_Nmos =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZPAddY,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.SbcNmos(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // SBC Pipelines - CMOS (65C02)
    // ========================================

    /// <summary>SBC #imm (0xE9) - 2 cycles (+1 if decimal) - 65C02</summary>
    private static readonly MicroOp[] Sbc_Imm_Cmos =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.FetchImmediate(prev, current, bus);
            MicroOps.SbcCmos(prev, current, bus);
            AddDecimalPenaltyAndComplete(current);
        }
    ];

    /// <summary>SBC zp (0xE5) - 3 cycles (+1 if decimal) - 65C02</summary>
    private static readonly MicroOp[] Sbc_Zp_Cmos =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            MicroOps.ReadZeroPage(prev, current, bus);
            MicroOps.SbcCmos(prev, current, bus);
            AddDecimalPenaltyAndComplete(current);
        }
    ];

    /// <summary>SBC zp,X (0xF5) - 4 cycles (+1 if decimal) - 65C02</summary>
    private static readonly MicroOp[] Sbc_ZpX_Cmos =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        (prev, current, bus) =>
        {
            MicroOps.ReadZeroPage(prev, current, bus);
            MicroOps.SbcCmos(prev, current, bus);
            AddDecimalPenaltyAndComplete(current);
        }
    ];

    /// <summary>SBC abs (0xED) - 4 cycles (+1 if decimal) - 65C02</summary>
    private static readonly MicroOp[] Sbc_Abs_Cmos =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.SbcCmos(prev, current, bus);
            AddDecimalPenaltyAndComplete(current);
        }
    ];

    /// <summary>SBC abs,X (0xFD) - 4 cycles (+1 if page cross, +1 if decimal) - 65C02</summary>
    private static readonly MicroOp[] Sbc_AbsX_65C02 =
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
            MicroOps.SbcCmos(prev, current, bus);
            AddDecimalPenaltyAndComplete(current);
        }
    ];

    /// <summary>SBC abs,Y (0xF9) - 4 cycles (+1 if page cross, +1 if decimal) - 65C02</summary>
    private static readonly MicroOp[] Sbc_AbsY_65C02 =
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
            MicroOps.SbcCmos(prev, current, bus);
            AddDecimalPenaltyAndComplete(current);
        }
    ];

    /// <summary>SBC (zp,X) (0xE1) - 6 cycles (+1 if decimal) - 65C02</summary>
    private static readonly MicroOp[] Sbc_IzX_Cmos =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.AddXZeroPage,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZP,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.SbcCmos(prev, current, bus);
            AddDecimalPenaltyAndComplete(current);
        }
    ];

    /// <summary>SBC (zp),Y (0xF1) - 5 cycles (+1 if page cross, +1 if decimal) - 65C02</summary>
    private static readonly MicroOp[] Sbc_IzY_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZPAddY65C02,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.SbcCmos(prev, current, bus);
            AddDecimalPenaltyAndComplete(current);
        }
    ];

    /// <summary>SBC (zp) (0xF2) - 5 cycles (+1 if decimal) - 65C02 only</summary>
    private static readonly MicroOp[] Sbc_Izp_Cmos =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.ReadPointerLowZP,
        MicroOps.ReadPointerHighZP,
        (prev, current, bus) =>
        {
            MicroOps.ReadFromTempAddress(prev, current, bus);
            MicroOps.SbcCmos(prev, current, bus);
            AddDecimalPenaltyAndComplete(current);
        }
    ];
}
