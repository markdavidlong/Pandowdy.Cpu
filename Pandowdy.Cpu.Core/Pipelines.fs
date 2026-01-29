// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Cpu

open Pandowdy.Cpu.MicroOps

/// Opcode pipeline definitions for 65C02
module Pipelines =

    let private IrqBrkVector = 0xFFFEus

    // NMOS 6502 BRK - does NOT clear decimal flag
    let brk : MicroOp[] = [|
        fetchOpcode
        fetchImmediate
        pushPCH
        pushPCL
        pushP
        readVectorLow IrqBrkVector
        microOp (fun prev next bus ->
            (readVectorHigh IrqBrkVector).Invoke(prev, next, bus)
            setInterruptDisable.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // 65C02 BRK - clears decimal flag (65C02 behavior)
    let brk_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchImmediate
        pushPCH
        pushPCL
        pushP
        readVectorLow IrqBrkVector
        microOp (fun prev next bus ->
            (readVectorHigh IrqBrkVector).Invoke(prev, next, bus)
            setInterruptDisable.Invoke(prev, next, bus)
            cld.Invoke(prev, next, bus)  // 65C02 clears decimal mode on interrupt
            markComplete.Invoke(prev, next, bus))
    |]

    let lda_imm : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            fetchImmediate.Invoke(prev, next, bus)
            loadA.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let lda_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            loadA.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // LDA zp - Zero Page (0xA5) - 3 cycles
    let lda_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            readZeroPage.Invoke(prev, next, bus)
            loadA.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // LDA zp,X - Zero Page Indexed X (0xB5) - 4 cycles
    let lda_zpx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage  // Dummy cycle + add X with ZP wrap
        microOp (fun prev next bus ->
            readZeroPage.Invoke(prev, next, bus)
            loadA.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // LDA abs,X - Absolute Indexed X (0xBD) - 4 cycles (+1 if page cross) - NMOS
    let lda_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            fetchAddressHigh.Invoke(prev, next, bus)
            addXCheckPage.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            loadA.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // LDA abs,X - 65C02 version (page crossing reads from high byte address)
    let lda_absx_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            let highByteAddr = next.PC
            fetchAddressHigh.Invoke(prev, next, bus)
            next.TempValue <- highByteAddr
            addXCheckPage65C02.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            loadA.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // LDA abs,Y - Absolute Indexed Y (0xB9) - 4 cycles (+1 if page cross) - NMOS
    let lda_absy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            fetchAddressHigh.Invoke(prev, next, bus)
            addYCheckPage.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            loadA.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // LDA abs,Y - 65C02 version (page crossing reads from high byte address)
    let lda_absy_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            let highByteAddr = next.PC
            fetchAddressHigh.Invoke(prev, next, bus)
            next.TempValue <- highByteAddr
            addYCheckPage65C02.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            loadA.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // LDA (zp,X) - Indexed Indirect (0xA1) - 6 cycles
    let lda_izx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage  // Add X with ZP wrap
        readPointerLowZP  // Read low byte of pointer
        readPointerHighZP  // Read high byte of pointer
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            loadA.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // LDA (zp),Y - Indirect Indexed (0xB1) - 5 cycles (+1 if page cross) - NMOS
    let lda_izy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP  // Read low byte of pointer
        readPointerHighZPAddY  // Read high byte, add Y with page check
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            loadA.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // LDA (zp),Y - Indirect Indexed (0xB1) - 5 cycles (+1 if page cross) - 65C02
    // Page crossing penalty reads from operand address (T1), not wrong effective address
    let lda_izy_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP  // Read low byte of pointer
        readPointerHighZPAddY65C02  // Read high byte, add Y with 65C02 page check
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            loadA.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // LDA (zp) - Zero Page Indirect (65C02 only) (0xB2) - 5 cycles
    let lda_izp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP  // Read low byte of pointer
        readPointerHighZP  // Read high byte of pointer
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            loadA.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ========================================
    // NMOS ADC pipelines (N/Z flags from binary result in BCD mode)
    // ========================================

    let adc_imm_nmos : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            fetchImmediate.Invoke(prev, next, bus)
            adcNmos.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let adc_zp_nmos : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            readZeroPage.Invoke(prev, next, bus)
            adcNmos.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let adc_zpx_nmos : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        microOp (fun prev next bus ->
            readZeroPage.Invoke(prev, next, bus)
            adcNmos.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let adc_abs_nmos : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            adcNmos.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let adc_absx_nmos : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            fetchAddressHigh.Invoke(prev, next, bus)
            addXCheckPage.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            adcNmos.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let adc_absy_nmos : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            fetchAddressHigh.Invoke(prev, next, bus)
            addYCheckPage.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            adcNmos.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let adc_izx_nmos : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readPointerLowZP
        readPointerHighZP
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            adcNmos.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let adc_izy_nmos : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        readPointerHighZPAddY
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            adcNmos.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ========================================
    // CMOS ADC pipelines (N/Z flags from BCD result in BCD mode)
    // ========================================

    // 65C02 ADC/SBC pipelines add an extra cycle in decimal mode
    // This helper checks D flag and adds a penalty cycle followed by completion
    // If D flag is not set, it just marks completion
    // The penalty cycle re-reads from the operand address (TempAddress)
    let addDecimalPenaltyAndComplete (state: CpuState) =
        if state.DecimalFlag then
            // Insert a penalty cycle that re-reads from the operand address
            let operandAddr = state.TempAddress
            let penaltyOp = microOp (fun _ n b ->
                b.CpuRead(operandAddr) |> ignore
                n.InstructionComplete <- true)
            insertAfterCurrentOp state penaltyOp
        else
            state.InstructionComplete <- true

    let adc_imm_cmos : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            fetchImmediate.Invoke(prev, next, bus)
            adcCmos.Invoke(prev, next, bus)
            // For immediate mode, decimal penalty reads from $007F (WDC)
            next.TempAddress <- 0x007Fus
            addDecimalPenaltyAndComplete next)
    |]

    // Rockwell 65C02 uses different penalty address for ADC immediate
    let adc_imm_rockwell : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            fetchImmediate.Invoke(prev, next, bus)
            adcCmos.Invoke(prev, next, bus)
            // For immediate mode, decimal penalty reads from $0059 (Rockwell)
            next.TempAddress <- 0x0059us
            addDecimalPenaltyAndComplete next)
    |]

    let adc_zp_cmos : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            readZeroPage.Invoke(prev, next, bus)
            adcCmos.Invoke(prev, next, bus)
            addDecimalPenaltyAndComplete next)
    |]

    let adc_zpx_cmos : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        microOp (fun prev next bus ->
            readZeroPage.Invoke(prev, next, bus)
            adcCmos.Invoke(prev, next, bus)
            addDecimalPenaltyAndComplete next)
    |]

    let adc_abs_cmos : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            adcCmos.Invoke(prev, next, bus)
            addDecimalPenaltyAndComplete next)
    |]

    let adc_absx_cmos : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            fetchAddressHigh.Invoke(prev, next, bus)
            addXCheckPage.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            adcCmos.Invoke(prev, next, bus)
            addDecimalPenaltyAndComplete next)
    |]

    let adc_absx_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            let highByteAddr = next.PC
            fetchAddressHigh.Invoke(prev, next, bus)
            next.TempValue <- highByteAddr
            addXCheckPage65C02.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            adcCmos.Invoke(prev, next, bus)
            addDecimalPenaltyAndComplete next)
    |]

    let adc_absy_cmos : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            fetchAddressHigh.Invoke(prev, next, bus)
            addYCheckPage.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            adcCmos.Invoke(prev, next, bus)
            addDecimalPenaltyAndComplete next)
    |]

    let adc_absy_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            let highByteAddr = next.PC
            fetchAddressHigh.Invoke(prev, next, bus)
            next.TempValue <- highByteAddr
            addYCheckPage65C02.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            adcCmos.Invoke(prev, next, bus)
            addDecimalPenaltyAndComplete next)
    |]

    let adc_izx_cmos : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readPointerLowZP
        readPointerHighZP
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            adcCmos.Invoke(prev, next, bus)
            addDecimalPenaltyAndComplete next)
    |]

    let adc_izy_cmos : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        readPointerHighZPAddY
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            adcCmos.Invoke(prev, next, bus)
            addDecimalPenaltyAndComplete next)
    |]

    let adc_izp_cmos : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        readPointerHighZP
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            adcCmos.Invoke(prev, next, bus)
            addDecimalPenaltyAndComplete next)
    |]

    // ========================================
    // NMOS SBC pipelines (N/Z flags from binary result in BCD mode)
    // ========================================

    let sbc_imm_nmos : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            fetchImmediate.Invoke(prev, next, bus)
            sbcNmos.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let sbc_zp_nmos : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            readZeroPage.Invoke(prev, next, bus)
            sbcNmos.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let sbc_zpx_nmos : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        microOp (fun prev next bus ->
            readZeroPage.Invoke(prev, next, bus)
            sbcNmos.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let sbc_abs_nmos : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            sbcNmos.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let sbc_absx_nmos : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            fetchAddressHigh.Invoke(prev, next, bus)
            addXCheckPage.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            sbcNmos.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let sbc_absy_nmos : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            fetchAddressHigh.Invoke(prev, next, bus)
            addYCheckPage.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            sbcNmos.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let sbc_izx_nmos : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readPointerLowZP
        readPointerHighZP
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            sbcNmos.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let sbc_izy_nmos : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        readPointerHighZPAddY
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            sbcNmos.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    /// Unofficial SBC opcode 0xEB (NMOS only) - duplicate of SBC immediate
    let sbc_imm_unofficial_nmos : MicroOp[] = sbc_imm_nmos

    // ========================================
    // CMOS SBC pipelines (N/Z flags from BCD result in BCD mode)
    // 65C02 adds an extra cycle in decimal mode
    // ========================================

    let sbc_imm_cmos : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            fetchImmediate.Invoke(prev, next, bus)
            sbcCmos.Invoke(prev, next, bus)
            addDecimalPenaltyAndComplete next)
    |]

    let sbc_zp_cmos : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            readZeroPage.Invoke(prev, next, bus)
            sbcCmos.Invoke(prev, next, bus)
            addDecimalPenaltyAndComplete next)
    |]

    let sbc_zpx_cmos : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        microOp (fun prev next bus ->
            readZeroPage.Invoke(prev, next, bus)
            sbcCmos.Invoke(prev, next, bus)
            addDecimalPenaltyAndComplete next)
    |]

    let sbc_abs_cmos : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            sbcCmos.Invoke(prev, next, bus)
            addDecimalPenaltyAndComplete next)
    |]

    let sbc_absx_cmos : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            fetchAddressHigh.Invoke(prev, next, bus)
            addXCheckPage.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            sbcCmos.Invoke(prev, next, bus)
            addDecimalPenaltyAndComplete next)
    |]

    let sbc_absy_cmos : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            fetchAddressHigh.Invoke(prev, next, bus)
            addYCheckPage.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            sbcCmos.Invoke(prev, next, bus)
            addDecimalPenaltyAndComplete next)
    |]

    let sbc_absx_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            let highByteAddr = next.PC
            fetchAddressHigh.Invoke(prev, next, bus)
            next.TempValue <- highByteAddr
            addXCheckPage65C02.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            sbcCmos.Invoke(prev, next, bus)
            addDecimalPenaltyAndComplete next)
    |]

    let sbc_absy_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            let highByteAddr = next.PC
            fetchAddressHigh.Invoke(prev, next, bus)
            next.TempValue <- highByteAddr
            addYCheckPage65C02.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            sbcCmos.Invoke(prev, next, bus)
            addDecimalPenaltyAndComplete next)
    |]

    let sbc_izx_cmos : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readPointerLowZP
        readPointerHighZP
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            sbcCmos.Invoke(prev, next, bus)
            addDecimalPenaltyAndComplete next)
    |]

    let sbc_izy_cmos : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        readPointerHighZPAddY
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            sbcCmos.Invoke(prev, next, bus)
            addDecimalPenaltyAndComplete next)
    |]

    let sbc_izp_cmos : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        readPointerHighZP
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            sbcCmos.Invoke(prev, next, bus)
            addDecimalPenaltyAndComplete next)
    |]

    // 65C02 (zp),Y instructions - page crossing reads from operand address (T1)
    let adc_izy_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        readPointerHighZPAddY65C02
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            adcCmos.Invoke(prev, next, bus)
            addDecimalPenaltyAndComplete next)
    |]

    let sbc_izy_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        readPointerHighZPAddY65C02
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            sbcCmos.Invoke(prev, next, bus)
            addDecimalPenaltyAndComplete next)
    |]

    // ========================================
    // Phase 2: AND (all addressing modes)
    // ========================================

    let and_imm : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            fetchImmediate.Invoke(prev, next, bus)
            andOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let and_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            readZeroPage.Invoke(prev, next, bus)
            andOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let and_zpx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        microOp (fun prev next bus ->
            readZeroPage.Invoke(prev, next, bus)
            andOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let and_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            andOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let and_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            fetchAddressHigh.Invoke(prev, next, bus)
            addXCheckPage.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            andOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let and_absx_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            let highByteAddr = next.PC
            fetchAddressHigh.Invoke(prev, next, bus)
            next.TempValue <- highByteAddr
            addXCheckPage65C02.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            andOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let and_absy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            fetchAddressHigh.Invoke(prev, next, bus)
            addYCheckPage.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            andOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let and_absy_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            let highByteAddr = next.PC
            fetchAddressHigh.Invoke(prev, next, bus)
            next.TempValue <- highByteAddr
            addYCheckPage65C02.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            andOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let and_izx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readPointerLowZP
        readPointerHighZP
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            andOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let and_izy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        readPointerHighZPAddY
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            andOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let and_izy_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        readPointerHighZPAddY65C02
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            andOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let and_izp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        readPointerHighZP
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            andOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ========================================
    // Phase 2: ORA (all addressing modes)
    // ========================================

    let ora_imm : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            fetchImmediate.Invoke(prev, next, bus)
            oraOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let ora_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            readZeroPage.Invoke(prev, next, bus)
            oraOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let ora_zpx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        microOp (fun prev next bus ->
            readZeroPage.Invoke(prev, next, bus)
            oraOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let ora_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            oraOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let ora_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            fetchAddressHigh.Invoke(prev, next, bus)
            addXCheckPage.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            oraOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let ora_absx_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            let highByteAddr = next.PC
            fetchAddressHigh.Invoke(prev, next, bus)
            next.TempValue <- highByteAddr
            addXCheckPage65C02.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            oraOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let ora_absy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            fetchAddressHigh.Invoke(prev, next, bus)
            addYCheckPage.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            oraOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let ora_absy_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            let highByteAddr = next.PC
            fetchAddressHigh.Invoke(prev, next, bus)
            next.TempValue <- highByteAddr
            addYCheckPage65C02.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            oraOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let ora_izx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPageWithDummyRead  // Dummy read at base address while adding X
        readPointerLowZP
        readPointerHighZP
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            oraOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let ora_izy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        readPointerHighZPAddY
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            oraOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let ora_izy_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        readPointerHighZPAddY65C02
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            oraOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let ora_izp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        readPointerHighZP
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            oraOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ========================================
    // Phase 2: EOR (all addressing modes)
    // ========================================

    let eor_imm : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            fetchImmediate.Invoke(prev, next, bus)
            eorOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let eor_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            readZeroPage.Invoke(prev, next, bus)
            eorOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let eor_zpx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        microOp (fun prev next bus ->
            readZeroPage.Invoke(prev, next, bus)
            eorOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let eor_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            eorOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let eor_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            fetchAddressHigh.Invoke(prev, next, bus)
            addXCheckPage.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            eorOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let eor_absx_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            let highByteAddr = next.PC
            fetchAddressHigh.Invoke(prev, next, bus)
            next.TempValue <- highByteAddr
            addXCheckPage65C02.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            eorOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let eor_absy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            fetchAddressHigh.Invoke(prev, next, bus)
            addYCheckPage.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            eorOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let eor_absy_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            let highByteAddr = next.PC
            fetchAddressHigh.Invoke(prev, next, bus)
            next.TempValue <- highByteAddr
            addYCheckPage65C02.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            eorOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let eor_izx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readPointerLowZP
        readPointerHighZP
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            eorOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let eor_izy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        readPointerHighZPAddY
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            eorOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let eor_izy_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        readPointerHighZPAddY65C02
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            eorOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let eor_izp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        readPointerHighZP
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            eorOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ========================================
    // Phase 2: CMP (all addressing modes)
    // ========================================

    let cmp_imm : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            fetchImmediate.Invoke(prev, next, bus)
            cmpOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let cmp_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            readZeroPage.Invoke(prev, next, bus)
            cmpOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let cmp_zpx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        microOp (fun prev next bus ->
            readZeroPage.Invoke(prev, next, bus)
            cmpOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let cmp_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            cmpOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let cmp_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            fetchAddressHigh.Invoke(prev, next, bus)
            addXCheckPage.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            cmpOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let cmp_absx_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            let highByteAddr = next.PC
            fetchAddressHigh.Invoke(prev, next, bus)
            next.TempValue <- highByteAddr
            addXCheckPage65C02.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            cmpOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let cmp_absy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            fetchAddressHigh.Invoke(prev, next, bus)
            addYCheckPage.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            cmpOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let cmp_absy_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            let highByteAddr = next.PC
            fetchAddressHigh.Invoke(prev, next, bus)
            next.TempValue <- highByteAddr
            addYCheckPage65C02.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            cmpOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let cmp_izx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readPointerLowZP
        readPointerHighZP
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            cmpOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let cmp_izy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        readPointerHighZPAddY
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            cmpOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let cmp_izy_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        readPointerHighZPAddY65C02
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            cmpOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let cmp_izp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        readPointerHighZP
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            cmpOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ========================================
    // Phase 2: CPX (all addressing modes)
    // ========================================

    let cpx_imm : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            fetchImmediate.Invoke(prev, next, bus)
            cpxOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let cpx_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            readZeroPage.Invoke(prev, next, bus)
            cpxOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let cpx_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            cpxOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ========================================
    // Phase 2: CPY (all addressing modes)
    // ========================================

    let cpy_imm : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            fetchImmediate.Invoke(prev, next, bus)
            cpyOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let cpy_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            readZeroPage.Invoke(prev, next, bus)
            cpyOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let cpy_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            cpyOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ========================================
    // Phase 2: BIT (all addressing modes)
    // ========================================

    let bit_imm : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            fetchImmediate.Invoke(prev, next, bus)
            bitImmOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let bit_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            readZeroPage.Invoke(prev, next, bus)
            bitOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let bit_zpx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        microOp (fun prev next bus ->
            readZeroPage.Invoke(prev, next, bus)
            bitOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let bit_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            bitOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let bit_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            fetchAddressHigh.Invoke(prev, next, bus)
            addXCheckPage.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            bitOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let bit_absx_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            let highByteAddr = next.PC
            fetchAddressHigh.Invoke(prev, next, bus)
            next.TempValue <- highByteAddr
            addXCheckPage65C02.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            bitOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ========================================
    // Phase 5: Branch Instructions
    // ========================================

    // BEQ - Branch if Equal (Z=1) (0xF0) - 2 cycles (+1 if taken, +1 if page cross)
    let beq : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            fetchImmediate.Invoke(prev, next, bus)
            branchIfZeroSet.Invoke(prev, next, bus))
    |]

    // BNE - Branch if Not Equal (Z=0) (0xD0) - 2 cycles (+1 if taken, +1 if page cross)
    let bne : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            fetchImmediate.Invoke(prev, next, bus)
            branchIfZeroClear.Invoke(prev, next, bus))
    |]

    // BCS - Branch if Carry Set (C=1) (0xB0) - 2 cycles (+1 if taken, +1 if page cross)
    let bcs : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            fetchImmediate.Invoke(prev, next, bus)
            branchIfCarrySet.Invoke(prev, next, bus))
    |]

    // BCC - Branch if Carry Clear (C=0) (0x90) - 2 cycles (+1 if taken, +1 if page cross)
    let bcc : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            fetchImmediate.Invoke(prev, next, bus)
            branchIfCarryClear.Invoke(prev, next, bus))
    |]

    // BMI - Branch if Minus (N=1) (0x30) - 2 cycles (+1 if taken, +1 if page cross)
    let bmi : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            fetchImmediate.Invoke(prev, next, bus)
            branchIfNegative.Invoke(prev, next, bus))
    |]

    // BPL - Branch if Plus (N=0) (0x10) - 2 cycles (+1 if taken, +1 if page cross)
    let bpl : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            fetchImmediate.Invoke(prev, next, bus)
            branchIfPositive.Invoke(prev, next, bus))
    |]

    // BVS - Branch if Overflow Set (V=1) (0x70) - 2 cycles (+1 if taken, +1 if page cross)
    let bvs : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            fetchImmediate.Invoke(prev, next, bus)
            branchIfOverflowSet.Invoke(prev, next, bus))
    |]

    // BVC - Branch if Overflow Clear (V=0) (0x50) - 2 cycles (+1 if taken, +1 if page cross)
    let bvc : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            fetchImmediate.Invoke(prev, next, bus)
            branchIfOverflowClear.Invoke(prev, next, bus))
    |]

    // BRA - Branch Always (0x80) - 3 cycles (+1 if page cross) (65C02 only)
    let bra : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            fetchImmediate.Invoke(prev, next, bus)
            branchAlways.Invoke(prev, next, bus))
    |]

    // ========================================
    // Phase 8: Flag Operations
    // ========================================

    // CLC - Clear Carry (0x18) - 2 cycles
    let clc_impl : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            dummyReadPC.Invoke(prev, next, bus)
            clc.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // SEC - Set Carry (0x38) - 2 cycles
    let sec_impl : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            dummyReadPC.Invoke(prev, next, bus)
            sec.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // CLD - Clear Decimal (0xD8) - 2 cycles
    let cld_impl : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            dummyReadPC.Invoke(prev, next, bus)
            cld.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // SED - Set Decimal (0xF8) - 2 cycles
    let sed_impl : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            dummyReadPC.Invoke(prev, next, bus)
            sed.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // CLI - Clear Interrupt Disable (0x58) - 2 cycles
    let cli_impl : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            dummyReadPC.Invoke(prev, next, bus)
            cli.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // SEI - Set Interrupt Disable (0x78) - 2 cycles
    let sei_impl : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            dummyReadPC.Invoke(prev, next, bus)
            sei.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // CLV - Clear Overflow (0xB8) - 2 cycles
    let clv_impl : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            dummyReadPC.Invoke(prev, next, bus)
            clv.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ========================================
    // Phase 6: Stack & Subroutines
    // ========================================

    // PHA - Push Accumulator (0x48) - 3 cycles
    let pha_impl : MicroOp[] = [|
        fetchOpcode
        dummyReadPC  // Dummy read at PC (cycle 2)
        microOp (fun prev next bus ->
            pushA.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // PLA - Pull Accumulator (0x68) - 4 cycles
    let pla_impl : MicroOp[] = [|
        fetchOpcode
        dummyReadPC  // Dummy read at PC (cycle 2)
        dummyStackRead  // Increment SP
        microOp (fun prev next bus ->
            pullA.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // PHP - Push Processor Status (0x08) - 3 cycles
    let php_impl : MicroOp[] = [|
        fetchOpcode
        dummyReadPC  // Dummy read at PC (cycle 2)
        microOp (fun prev next bus ->
            pushP.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // PLP - Pull Processor Status (0x28) - 4 cycles
    let plp_impl : MicroOp[] = [|
        fetchOpcode
        dummyReadPC  // Dummy read at PC (cycle 2)
        dummyStackRead  // Increment SP
        microOp (fun prev next bus ->
            pullP.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // PHX - Push X Register (0xDA) - 3 cycles (65C02)
    let phx_impl : MicroOp[] = [|
        fetchOpcode
        dummyReadPC  // Dummy read at PC (cycle 2)
        microOp (fun prev next bus ->
            pushX.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // PLX - Pull X Register (0xFA) - 4 cycles (65C02)
    let plx_impl : MicroOp[] = [|
        fetchOpcode
        dummyReadPC  // Dummy read at PC (cycle 2)
        dummyStackRead  // Increment SP
        microOp (fun prev next bus ->
            pullX.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // PHY - Push Y Register (0x5A) - 3 cycles (65C02)
    let phy_impl : MicroOp[] = [|
        fetchOpcode
        dummyReadPC  // Dummy read at PC (cycle 2)
        microOp (fun prev next bus ->
            pushY.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // PLY - Pull Y Register (0x7A) - 4 cycles (65C02)
    let ply_impl : MicroOp[] = [|
        fetchOpcode
        dummyReadPC  // Dummy read at PC (cycle 2)
        dummyStackRead  // Increment SP
        microOp (fun prev next bus ->
            pullY.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // JSR - Jump to Subroutine (0x20) - 6 cycles
    let jsr : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        dummyStackRead  // Internal operation
        pushPCH
        pushPCL
        microOp (fun prev next bus ->
            fetchAddressHigh.Invoke(prev, next, bus)
            jumpToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // RTS - Return from Subroutine (0x60) - 6 cycles
    let rts : MicroOp[] = [|
        fetchOpcode
        dummyReadPC  // Dummy read at PC (cycle 2)
        dummyStackRead  // Increment SP (cycle 3)
        pullPCL  // Pull low byte of return address (cycle 4)
        pullPCH  // Pull high byte of return address (cycle 5)
        microOp (fun prev next bus ->
            // Cycle 6: Read from current PC (return address), THEN increment
            bus.CpuRead(next.PC) |> ignore
            next.PC <- next.PC + 1us
            markComplete.Invoke(prev, next, bus))
    |]

    // RTI - Return from Interrupt (0x40) - 6 cycles
    let rti : MicroOp[] = [|
        fetchOpcode
        dummyReadPC  // Dummy read at PC
        dummyStackRead  // Increment SP
        microOp (fun prev next bus ->
            pullP.Invoke(prev, next, bus))
        pullPCL
        microOp (fun prev next bus ->
            pullPCH.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let sta_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            storeA.Invoke(prev, next, bus)
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // STA zp,X (0x95) - 4 cycles
    let sta_zpx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        microOp (fun prev next bus ->
            storeA.Invoke(prev, next, bus)
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // STA abs (0x8D) - 4 cycles
    let sta_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        microOp (fun prev next bus ->
            storeA.Invoke(prev, next, bus)
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // STA abs,X (0x9D) - 5 cycles (NMOS - dummy read at wrong effective address)
    let sta_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addXWithDummyRead  // Dummy read at incomplete address, then add X
        microOp (fun prev next bus ->
            storeA.Invoke(prev, next, bus)
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // STA abs,X (0x9D) - 5 cycles (65C02 - dummy read at high byte address)
    // T0: Fetch opcode
    // T1: Fetch low byte
    // T2: Fetch high byte (save address for T3)
    // T3: Dummy read at high byte address (same as T2)
    // T4: Write to effective address
    let sta_absx_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            // Fetch high byte and save this address for T3 dummy read
            let highByteAddr = next.PC
            let hi = bus.CpuRead(next.PC)
            next.PC <- next.PC + 1us
            next.TempAddress <- next.TempAddress ||| (uint16 hi <<< 8)
            next.TempValue <- highByteAddr)  // Save high byte address for T3
        microOp (fun prev next bus ->
            // T3: Dummy read at high byte address, then add X
            bus.CpuRead(next.TempValue) |> ignore
            next.TempAddress <- next.TempAddress + uint16 next.X)
        microOp (fun prev next bus ->
            storeA.Invoke(prev, next, bus)
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // STA abs,Y (0x99) - 5 cycles (NMOS - dummy read at wrong effective address)
    let sta_absy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addYWithDummyRead  // Dummy read at incomplete address, then add Y
        microOp (fun prev next bus ->
            storeA.Invoke(prev, next, bus)
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // STA abs,Y (0x99) - 5 cycles (65C02 - dummy read at high byte address)
    // T0: Fetch opcode
    // T1: Fetch low byte
    // T2: Fetch high byte (save address for T3)
    // T3: Dummy read at high byte address (same as T2)
    // T4: Write to effective address
    let sta_absy_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            // Fetch high byte and save this address for T3 dummy read
            let highByteAddr = next.PC
            let hi = bus.CpuRead(next.PC)
            next.PC <- next.PC + 1us
            next.TempAddress <- next.TempAddress ||| (uint16 hi <<< 8)
            next.TempValue <- highByteAddr)  // Save high byte address for T3
        microOp (fun prev next bus ->
            // T3: Dummy read at high byte address, then add Y
            bus.CpuRead(next.TempValue) |> ignore
            next.TempAddress <- next.TempAddress + uint16 next.Y)
        microOp (fun prev next bus ->
            storeA.Invoke(prev, next, bus)
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // STA (zp,X) (0x81) - 6 cycles
    let sta_izx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readPointerLowZP
        readPointerHighZP
        microOp (fun prev next bus ->
            storeA.Invoke(prev, next, bus)
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // STA (zp),Y (0x91) - 6 cycles (NMOS behavior - dummy read at wrong effective address)
    let sta_izy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        readPointerHighZP
        addYWithDummyRead  // Dummy read at incomplete address, then add Y
        microOp (fun prev next bus ->
            storeA.Invoke(prev, next, bus)
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // STA (zp),Y (0x91) - 6 cycles (65C02 behavior - dummy read at operand address)
    // 65C02 does dummy read at the ZP operand address (PC after T0), not at wrong effective address
    let sta_izy_65c02 : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            // T1: Fetch ZP operand and save operand PC for later dummy read
            let operandAddr = next.PC
            let zpAddr = bus.CpuRead(next.PC)
            next.PC <- next.PC + 1us
            next.TempAddress <- uint16 zpAddr  // ZP address for pointer reads
            next.TempValue <- operandAddr)     // Operand address for T4 dummy read
        microOp (fun prev next bus ->
            // T2: Read pointer low from zp
            let lo = bus.CpuRead(next.TempAddress &&& 0x00FFus)
            // Store ZP addr in upper bits, pointer low in lower bits temporarily
            let zpAddr = next.TempAddress
            next.TempAddress <- uint16 lo ||| (zpAddr <<< 8))
        microOp (fun prev next bus ->
            // T3: Read pointer high from zp+1, form base address, add Y
            let zpAddr = uint16 (byte (next.TempAddress >>> 8))
            let ptrLo = byte next.TempAddress
            let zpHiAddr = (zpAddr + 1us) &&& 0x00FFus
            let ptrHi = bus.CpuRead(zpHiAddr)
            let baseAddr = uint16 ptrLo ||| (uint16 ptrHi <<< 8)
            next.TempAddress <- baseAddr + uint16 next.Y)
        microOp (fun prev next bus ->
            // T4: Dummy read at operand address (where ZP addr was fetched from)
            bus.CpuRead(next.TempValue) |> ignore)
        microOp (fun prev next bus ->
            // T5: Write A to effective address
            storeA.Invoke(prev, next, bus)
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // STA (zp) (0x92) - 5 cycles (65C02)
    let sta_izp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        readPointerHighZP
        microOp (fun prev next bus ->
            storeA.Invoke(prev, next, bus)
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let jmp_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            fetchAddressHigh.Invoke(prev, next, bus)
            jumpToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // JMP (ind) (0x6C) - 6 cycles (65C02 fixes page boundary bug)
    // When pointer is at $xxFF:
    //   T3: Read low byte from $xxFF
    //   T4: Dummy read at $xx00 (where NMOS would incorrectly read high byte)
    //   T5: Read high byte from correct address (pointer+1, which crosses page)
    // When pointer is not at $xxFF:
    //   T3: Read low byte from pointer
    //   T4: Read high byte from pointer+1
    //   T5: Dummy read at pointer+1
    let jmp_ind : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        readPointerLowAbs  // T3: Read low byte, store pointer in TempValue
        microOp (fun prev next bus ->
            // T4: Check if pointer low byte is $FF (page boundary case)
            let pointerAddr = next.TempValue
            if (pointerAddr &&& 0xFFus) = 0xFFus then
                // Page boundary: dummy read at buggy wrap address ($xx00)
                let buggyAddr = pointerAddr &&& 0xFF00us
                bus.CpuRead(buggyAddr) |> ignore
            else
                // Normal case: read high byte from pointer+1
                let hi = bus.CpuRead(pointerAddr + 1us)
                next.TempAddress <- next.TempAddress ||| (uint16 hi <<< 8))
        microOp (fun prev next bus ->
            // T5: Check if we need to read high byte (page boundary) or do dummy read
            let pointerAddr = next.TempValue
            if (pointerAddr &&& 0xFFus) = 0xFFus then
                // Page boundary case: read high byte from correct address
                let correctAddr = pointerAddr + 1us  // This correctly crosses the page
                let hi = bus.CpuRead(correctAddr)
                next.TempAddress <- next.TempAddress ||| (uint16 hi <<< 8)
            else
                // Normal case: dummy read at pointer+1
                bus.CpuRead(pointerAddr + 1us) |> ignore
            next.PC <- next.TempAddress
            next.InstructionComplete <- true)
    |]

    // JMP (abs,X) (0x7C) - 6 cycles (65C02 only)
    // T0: Fetch opcode
    // T1: Fetch low byte (save address for T3)
    // T2: Fetch high byte
    // T3: Dummy read at low byte address (same as T1)
    // T4: Read pointer low from (abs+X)
    // T5: Read pointer high and jump
    let jmp_absx_ind : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            // Fetch low byte and save this address for T3 dummy read
            let lowByteAddr = next.PC
            let lo = bus.CpuRead(next.PC)
            next.PC <- next.PC + 1us
            next.TempAddress <- uint16 lo
            next.TempValue <- lowByteAddr)  // Save low byte address for T3
        microOp (fun prev next bus ->
            // Fetch high byte
            let hi = bus.CpuRead(next.PC)
            next.PC <- next.PC + 1us
            next.TempAddress <- next.TempAddress ||| (uint16 hi <<< 8))
        microOp (fun prev next bus ->
            // Dummy read at low byte address (T3)
            bus.CpuRead(next.TempValue) |> ignore
            // Now add X to form effective pointer address
            next.TempAddress <- next.TempAddress + uint16 next.X)
        readPointerLowAbs
        microOp (fun prev next bus ->
            readPointerHighAbs.Invoke(prev, next, bus)
            jumpToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // JMP (ind) with NMOS page boundary bug (0x6C) - 5 cycles
    // When pointer is at $xxFF, high byte wraps to $xx00
    let jmp_ind_nmos : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        readPointerLowAbs
        microOp (fun prev next bus ->
            readPointerHighAbsNMOS.Invoke(prev, next, bus)
            jumpToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // Standard NOP - 2 cycles (fetch opcode + dummy read)
    let nop : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            dummyReadPC.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // 1-cycle NOP for 65C02 undefined opcodes ($x3, $xB patterns)
    // Only fetches opcode, no dummy read
    let nop_1cycle : MicroOp[] = [|
        microOp (fun prev next bus ->
            bus.CpuRead(next.PC) |> ignore  // Fetch opcode
            next.PC <- next.PC + 1us
            next.InstructionComplete <- true)
    |]

    // Multi-cycle NOPs for undefined opcodes (65C02)
    let nop_3cycle : MicroOp[] = [|
        fetchOpcode
        dummyReadPC
        microOp (fun prev next bus ->
            markComplete.Invoke(prev, next, bus))
    |]

    let nop_4cycle : MicroOp[] = [|
        fetchOpcode
        dummyReadPC
        dummyReadPC
        microOp (fun prev next bus ->
            markComplete.Invoke(prev, next, bus))
    |]

    // ========================================
    // TRB/TSB - Test and Reset/Set Bits (65C02)
    // 65C02 RMW instructions do a dummy READ (not write) before the final write
    // ========================================

    // TRB zp (0x14) - 5 cycles
    let trb_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        microOp (fun prev next bus ->
            // 65C02: dummy READ (not write) before modification
            bus.CpuRead(next.TempAddress &&& 0x00FFus) |> ignore
            trbOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // TRB abs (0x1C) - 6 cycles
    let trb_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        readFromTempAddress
        microOp (fun prev next bus ->
            // 65C02: dummy READ (not write) before modification
            bus.CpuRead(next.TempAddress) |> ignore
            trbOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // TSB zp (0x04) - 5 cycles
    let tsb_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        microOp (fun prev next bus ->
            // 65C02: dummy READ (not write) before modification
            bus.CpuRead(next.TempAddress &&& 0x00FFus) |> ignore
            tsbOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // TSB abs (0x0C) - 6 cycles
    let tsb_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        readFromTempAddress
        microOp (fun prev next bus ->
            // 65C02: dummy READ (not write) before modification
            bus.CpuRead(next.TempAddress) |> ignore
            tsbOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ========================================
    // Phase 3: Increment/Decrement & Transfers
    // ========================================

    // INX (0xE8) - 2 cycles
    let inx_impl : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            dummyReadPC.Invoke(prev, next, bus)
            inx.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // INY (0xC8) - 2 cycles
    let iny_impl : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            dummyReadPC.Invoke(prev, next, bus)
            iny.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // DEX (0xCA) - 2 cycles
    let dex_impl : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            dummyReadPC.Invoke(prev, next, bus)
            dex.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // DEY (0x88) - 2 cycles
    let dey_impl : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            dummyReadPC.Invoke(prev, next, bus)
            dey.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // INC A (0x1A) - 2 cycles (65C02)
    let inc_a : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            dummyReadPC.Invoke(prev, next, bus)
            incA.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // DEC A (0x3A) - 2 cycles (65C02)
    let dec_a : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            dummyReadPC.Invoke(prev, next, bus)
            decA.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // TAX (0xAA) - 2 cycles
    let tax_impl : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            dummyReadPC.Invoke(prev, next, bus)
            tax.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // TXA (0x8A) - 2 cycles
    let txa_impl : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            dummyReadPC.Invoke(prev, next, bus)
            txa.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // TAY (0xA8) - 2 cycles
    let tay_impl : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            dummyReadPC.Invoke(prev, next, bus)
            tay.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // TYA (0x98) - 2 cycles
    let tya_impl : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            dummyReadPC.Invoke(prev, next, bus)
            tya.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // TSX (0xBA) - 2 cycles
    let tsx_impl : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            dummyReadPC.Invoke(prev, next, bus)
            tsx.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // TXS (0x9A) - 2 cycles
    let txs_impl : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            dummyReadPC.Invoke(prev, next, bus)
            txs.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // INC zp (0xE6) - 5 cycles
    let inc_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        microOp (fun prev next bus ->
            dummyWriteZeroPage.Invoke(prev, next, bus)  // Write original value back
            incMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // INC zp,X (0xF6) - 6 cycles
    let inc_zpx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readZeroPage
        microOp (fun prev next bus ->
            dummyWriteZeroPage.Invoke(prev, next, bus)  // Write original value back
            incMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // INC abs (0xEE) - 6 cycles
    let inc_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        readFromTempAddress
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            incMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // INC abs,X (0xFE) - 7 cycles
    let inc_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addXWithDummyRead  // Dummy read at base address, then add X
        readFromTempAddress
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            incMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // DEC zp (0xC6) - 5 cycles
    let dec_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        microOp (fun prev next bus ->
            dummyWriteZeroPage.Invoke(prev, next, bus)
            decMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // DEC zp,X (0xD6) - 6 cycles
    let dec_zpx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readZeroPage
        microOp (fun prev next bus ->
            dummyWriteZeroPage.Invoke(prev, next, bus)
            decMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // DEC abs (0xCE) - 6 cycles
    let dec_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        readFromTempAddress
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            decMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // DEC abs,X (0xDE) - 7 cycles
    let dec_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addXWithDummyRead  // Dummy read at base address, then add X
        readFromTempAddress
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            decMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ========================================
    // Phase 4: Shifts & Rotates
    // ========================================

    // ASL A (0x0A) - 2 cycles
    let asl_a : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            dummyReadPC.Invoke(prev, next, bus)  // Dummy read at PC
            aslA.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ASL zp (0x06) - 5 cycles
    let asl_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        microOp (fun prev next bus ->
            dummyWriteZeroPage.Invoke(prev, next, bus)
            aslMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ASL zp,X (0x16) - 6 cycles
    let asl_zpx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readZeroPage
        microOp (fun prev next bus ->
            dummyWriteZeroPage.Invoke(prev, next, bus)
            aslMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ASL abs (0x0E) - 6 cycles
    let asl_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        readFromTempAddress
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            aslMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ASL abs,X (0x1E) - 7 cycles
    let asl_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addXWithDummyRead  // Dummy read at base address, then add X
        readFromTempAddress
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            aslMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // LSR A (0x4A) - 2 cycles
    let lsr_a : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            dummyReadPC.Invoke(prev, next, bus)
            lsrA.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // LSR zp (0x46) - 5 cycles
    let lsr_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        microOp (fun prev next bus ->
            dummyWriteZeroPage.Invoke(prev, next, bus)
            lsrMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // LSR zp,X (0x56) - 6 cycles
    let lsr_zpx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readZeroPage
        microOp (fun prev next bus ->
            dummyWriteZeroPage.Invoke(prev, next, bus)
            lsrMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // LSR abs (0x4E) - 6 cycles
    let lsr_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        readFromTempAddress
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            lsrMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // LSR abs,X (0x5E) - 7 cycles
    let lsr_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addXWithDummyRead  // Dummy read at base address, then add X
        readFromTempAddress
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            lsrMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ROL A (0x2A) - 2 cycles
    let rol_a : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            dummyReadPC.Invoke(prev, next, bus)
            rolA.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ROL zp (0x26) - 5 cycles
    let rol_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        microOp (fun prev next bus ->
            dummyWriteZeroPage.Invoke(prev, next, bus)
            rolMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ROL zp,X (0x36) - 6 cycles
    let rol_zpx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readZeroPage
        microOp (fun prev next bus ->
            dummyWriteZeroPage.Invoke(prev, next, bus)
            rolMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ROL abs (0x2E) - 6 cycles
    let rol_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        readFromTempAddress
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            rolMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ROL abs,X (0x3E) - 7 cycles
    let rol_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addXWithDummyRead  // Dummy read at base address, then add X
        readFromTempAddress
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            rolMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ROR A (0x6A) - 2 cycles
    let ror_a : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            dummyReadPC.Invoke(prev, next, bus)
            rorA.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ROR zp (0x66) - 5 cycles
    let ror_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        microOp (fun prev next bus ->
            dummyWriteZeroPage.Invoke(prev, next, bus)
            rorMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ROR zp,X (0x76) - 6 cycles
    let ror_zpx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readZeroPage
        microOp (fun prev next bus ->
            dummyWriteZeroPage.Invoke(prev, next, bus)
            rorMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ROR abs (0x6E) - 6 cycles
    let ror_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        readFromTempAddress
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            rorMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ROR abs,X (0x7E) - 7 cycles
    let ror_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addXWithDummyRead  // Dummy read at base address, then add X
        readFromTempAddress
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            rorMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ========================================
    // Phase 7: LDX, LDY, STX, STY, STZ
    // ========================================

    // LDX #imm (0xA2) - 2 cycles
    let ldx_imm : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            fetchImmediate.Invoke(prev, next, bus)
            loadX.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // LDX zp (0xA6) - 3 cycles
    let ldx_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            readZeroPage.Invoke(prev, next, bus)
            loadX.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // LDX zp,Y (0xB6) - 4 cycles
    let ldx_zpy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addYZeroPage
        microOp (fun prev next bus ->
            readZeroPage.Invoke(prev, next, bus)
            loadX.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // LDX abs (0xAE) - 4 cycles
    let ldx_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            loadX.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // LDX abs,Y (0xBE) - 4 cycles (+1 if page cross) - NMOS
    let ldx_absy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            fetchAddressHigh.Invoke(prev, next, bus)
            addYCheckPage.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            loadX.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // LDX abs,Y - 65C02 version
    let ldx_absy_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            let highByteAddr = next.PC
            fetchAddressHigh.Invoke(prev, next, bus)
            next.TempValue <- highByteAddr
            addYCheckPage65C02.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            loadX.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // LDY #imm (0xA0) - 2 cycles
    let ldy_imm : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            fetchImmediate.Invoke(prev, next, bus)
            loadY.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // LDY zp (0xA4) - 3 cycles
    let ldy_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            readZeroPage.Invoke(prev, next, bus)
            loadY.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // LDY zp,X (0xB4) - 4 cycles
    let ldy_zpx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        microOp (fun prev next bus ->
            readZeroPage.Invoke(prev, next, bus)
            loadY.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // LDY abs (0xAC) - 4 cycles
    let ldy_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            loadY.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // LDY abs,X (0xBC) - 4 cycles (+1 if page cross) - NMOS
    let ldy_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            fetchAddressHigh.Invoke(prev, next, bus)
            addXCheckPage.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            loadY.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // LDY abs,X - 65C02 version
    let ldy_absx_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            let highByteAddr = next.PC
            fetchAddressHigh.Invoke(prev, next, bus)
            next.TempValue <- highByteAddr
            addXCheckPage65C02.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            loadY.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // STX zp (0x86) - 3 cycles
    let stx_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            storeX.Invoke(prev, next, bus)
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // STX zp,Y (0x96) - 4 cycles
    let stx_zpy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addYZeroPage
        microOp (fun prev next bus ->
            storeX.Invoke(prev, next, bus)
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // STX abs (0x8E) - 4 cycles
    let stx_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        microOp (fun prev next bus ->
            storeX.Invoke(prev, next, bus)
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // STY zp (0x84) - 3 cycles
    let sty_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            storeY.Invoke(prev, next, bus)
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // STY zp,X (0x94) - 4 cycles
    let sty_zpx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        microOp (fun prev next bus ->
            storeY.Invoke(prev, next, bus)
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // STY abs (0x8C) - 4 cycles
    let sty_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        microOp (fun prev next bus ->
            storeY.Invoke(prev, next, bus)
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // STZ zp (0x64) - 3 cycles (65C02)
    let stz_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            storeZ.Invoke(prev, next, bus)
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // STZ zp,X (0x74) - 4 cycles (65C02)
    let stz_zpx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        microOp (fun prev next bus ->
            storeZ.Invoke(prev, next, bus)
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // STZ abs (0x9C) - 4 cycles (65C02)
    let stz_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        microOp (fun prev next bus ->
            storeZ.Invoke(prev, next, bus)
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // STZ abs,X (0x9E) - 5 cycles (65C02 - dummy read at high byte address)
    // T0: Fetch opcode
    // T1: Fetch low byte
    // T2: Fetch high byte (save address for T3)
    // T3: Dummy read at high byte address (same as T2)
    // T4: Write zero to effective address
    let stz_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            // Fetch high byte and save this address for T3 dummy read
            let highByteAddr = next.PC
            let hi = bus.CpuRead(next.PC)
            next.PC <- next.PC + 1us
            next.TempAddress <- next.TempAddress ||| (uint16 hi <<< 8)
            next.TempValue <- highByteAddr)  // Save high byte address for T3
        microOp (fun prev next bus ->
            // T3: Dummy read at high byte address, then add X
            bus.CpuRead(next.TempValue) |> ignore
            next.TempAddress <- next.TempAddress + uint16 next.X)
        microOp (fun prev next bus ->
            storeZ.Invoke(prev, next, bus)
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ========================================
    // 65C02 RMW Instructions (use dummy READ, not write)
    // The 65C02 does a dummy read before the final write, unlike NMOS which does a dummy write
    // ========================================

    // ASL zp - 5 cycles (65C02)
    let asl_zp_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        microOp (fun prev next bus ->
            bus.CpuRead(next.TempAddress &&& 0x00FFus) |> ignore  // 65C02: dummy READ
            aslMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ASL zp,X - 6 cycles (65C02)
    let asl_zpx_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readZeroPage
        microOp (fun prev next bus ->
            bus.CpuRead(next.TempAddress &&& 0x00FFus) |> ignore  // 65C02: dummy READ
            aslMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ASL abs - 6 cycles (65C02)
    let asl_abs_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        readFromTempAddress
        microOp (fun prev next bus ->
            bus.CpuRead(next.TempAddress) |> ignore  // 65C02: dummy READ
            aslMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ASL abs,X - 6 cycles (65C02), +1 for page cross
    // On page cross, penalty cycle reads from high byte address (same as T2)
    let asl_absx_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            let highByteAddr = next.PC
            fetchAddressHigh.Invoke(prev, next, bus)
            next.TempValue <- highByteAddr
            addXCheckPage65C02.Invoke(prev, next, bus))
        readFromTempAddress
        microOp (fun prev next bus ->
            bus.CpuRead(next.TempAddress) |> ignore  // 65C02: dummy READ
            aslMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // LSR zp - 5 cycles (65C02)
    let lsr_zp_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        microOp (fun prev next bus ->
            bus.CpuRead(next.TempAddress &&& 0x00FFus) |> ignore
            lsrMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // LSR zp,X - 6 cycles (65C02)
    let lsr_zpx_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readZeroPage
        microOp (fun prev next bus ->
            bus.CpuRead(next.TempAddress &&& 0x00FFus) |> ignore
            lsrMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // LSR abs - 6 cycles (65C02)
    let lsr_abs_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        readFromTempAddress
        microOp (fun prev next bus ->
            bus.CpuRead(next.TempAddress) |> ignore
            lsrMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // LSR abs,X - 6 cycles (65C02), +1 for page cross
    let lsr_absx_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            let highByteAddr = next.PC
            fetchAddressHigh.Invoke(prev, next, bus)
            next.TempValue <- highByteAddr
            addXCheckPage65C02.Invoke(prev, next, bus))
        readFromTempAddress
        microOp (fun prev next bus ->
            bus.CpuRead(next.TempAddress) |> ignore
            lsrMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ROL zp - 5 cycles (65C02)
    let rol_zp_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        microOp (fun prev next bus ->
            bus.CpuRead(next.TempAddress &&& 0x00FFus) |> ignore
            rolMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ROL zp,X - 6 cycles (65C02)
    let rol_zpx_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readZeroPage
        microOp (fun prev next bus ->
            bus.CpuRead(next.TempAddress &&& 0x00FFus) |> ignore
            rolMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ROL abs - 6 cycles (65C02)
    let rol_abs_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        readFromTempAddress
        microOp (fun prev next bus ->
            bus.CpuRead(next.TempAddress) |> ignore
            rolMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ROL abs,X - 6 cycles (65C02), +1 for page cross
    let rol_absx_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            let highByteAddr = next.PC
            fetchAddressHigh.Invoke(prev, next, bus)
            next.TempValue <- highByteAddr
            addXCheckPage65C02.Invoke(prev, next, bus))
        readFromTempAddress
        microOp (fun prev next bus ->
            bus.CpuRead(next.TempAddress) |> ignore
            rolMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ROR zp - 5 cycles (65C02)
    let ror_zp_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        microOp (fun prev next bus ->
            bus.CpuRead(next.TempAddress &&& 0x00FFus) |> ignore
            rorMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ROR zp,X - 6 cycles (65C02)
    let ror_zpx_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readZeroPage
        microOp (fun prev next bus ->
            bus.CpuRead(next.TempAddress &&& 0x00FFus) |> ignore
            rorMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ROR abs - 6 cycles (65C02)
    let ror_abs_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        readFromTempAddress
        microOp (fun prev next bus ->
            bus.CpuRead(next.TempAddress) |> ignore
            rorMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ROR abs,X - 6 cycles (65C02), +1 for page cross
    let ror_absx_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            let highByteAddr = next.PC
            fetchAddressHigh.Invoke(prev, next, bus)
            next.TempValue <- highByteAddr
            addXCheckPage65C02.Invoke(prev, next, bus))
        readFromTempAddress
        microOp (fun prev next bus ->
            bus.CpuRead(next.TempAddress) |> ignore
            rorMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // INC zp - 5 cycles (65C02)
    let inc_zp_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        microOp (fun prev next bus ->
            bus.CpuRead(next.TempAddress &&& 0x00FFus) |> ignore
            incMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // INC zp,X - 6 cycles (65C02)
    let inc_zpx_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readZeroPage
        microOp (fun prev next bus ->
            bus.CpuRead(next.TempAddress &&& 0x00FFus) |> ignore
            incMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // INC abs - 6 cycles (65C02)
    let inc_abs_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        readFromTempAddress
        microOp (fun prev next bus ->
            bus.CpuRead(next.TempAddress) |> ignore
            incMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // INC abs,X - ALWAYS 7 cycles on 65C02
    // Unlike ASL/LSR/ROL/ROR abs,X which are 6+1, INC/DEC abs,X are always 7 cycles
    // T0: Fetch opcode
    // T1: Fetch low byte
    // T2: Fetch high byte (save address for T3)
    // T3: Dummy read at high byte address (same as T2)
    // T4: Read from effective address
    // T5: Dummy read at effective address
    // T6: Write to effective address
    let inc_absx_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            // Fetch high byte and save this address for T3 dummy read
            let highByteAddr = next.PC
            let hi = bus.CpuRead(next.PC)
            next.PC <- next.PC + 1us
            next.TempAddress <- next.TempAddress ||| (uint16 hi <<< 8)
            next.TempValue <- highByteAddr)  // Save high byte address for T3
        microOp (fun prev next bus ->
            // T3: Dummy read at high byte address, then add X
            bus.CpuRead(next.TempValue) |> ignore
            next.TempAddress <- next.TempAddress + uint16 next.X)
        readFromTempAddress
        microOp (fun prev next bus ->
            bus.CpuRead(next.TempAddress) |> ignore
            incMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // DEC zp - 5 cycles (65C02)
    let dec_zp_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        microOp (fun prev next bus ->
            bus.CpuRead(next.TempAddress &&& 0x00FFus) |> ignore
            decMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // DEC zp,X - 6 cycles (65C02)
    let dec_zpx_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readZeroPage
        microOp (fun prev next bus ->
            bus.CpuRead(next.TempAddress &&& 0x00FFus) |> ignore
            decMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // DEC abs - 6 cycles (65C02)
    let dec_abs_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        readFromTempAddress
        microOp (fun prev next bus ->
            bus.CpuRead(next.TempAddress) |> ignore
            decMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // DEC abs,X - ALWAYS 7 cycles on 65C02
    // Unlike ASL/LSR/ROL/ROR abs,X which are 6+1, INC/DEC abs,X are always 7 cycles
    // T0: Fetch opcode
    // T1: Fetch low byte
    // T2: Fetch high byte (save address for T3)
    // T3: Dummy read at high byte address (same as T2)
    // T4: Read from effective address
    // T5: Dummy read at effective address
    // T6: Write to effective address
    let dec_absx_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            // Fetch high byte and save this address for T3 dummy read
            let highByteAddr = next.PC
            let hi = bus.CpuRead(next.PC)
            next.PC <- next.PC + 1us
            next.TempAddress <- next.TempAddress ||| (uint16 hi <<< 8)
            next.TempValue <- highByteAddr)  // Save high byte address for T3
        microOp (fun prev next bus ->
            // T3: Dummy read at high byte address, then add X
            bus.CpuRead(next.TempValue) |> ignore
            next.TempAddress <- next.TempAddress + uint16 next.X)
        readFromTempAddress
        microOp (fun prev next bus ->
            bus.CpuRead(next.TempAddress) |> ignore
            decMem.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ========================================
    // Rockwell 65C02 Extensions
    // ========================================

    // RMB0-RMB7 - Reset Memory Bit (0x07, 0x17, 0x27, 0x37, 0x47, 0x57, 0x67, 0x77) - 5 cycles
    // T0: Fetch opcode
    // T1: Fetch ZP address
    // T2: Read from ZP
    // T3: Dummy read from ZP while modifying
    // T4: Write to ZP
    let rmb (bit: int) : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        microOp (fun prev next bus ->
            // Dummy read at ZP address while modifying
            bus.CpuRead(next.TempAddress &&& 0x00FFus) |> ignore
            rmbOp bit |> fun op -> op.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let rmb0 = rmb 0
    let rmb1 = rmb 1
    let rmb2 = rmb 2
    let rmb3 = rmb 3
    let rmb4 = rmb 4
    let rmb5 = rmb 5
    let rmb6 = rmb 6
    let rmb7 = rmb 7

    // SMB0-SMB7 - Set Memory Bit (0x87, 0x97, 0xA7, 0xB7, 0xC7, 0xD7, 0xE7, 0xF7) - 5 cycles
    let smb (bit: int) : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        microOp (fun prev next bus ->
            // Dummy read at ZP address while modifying
            bus.CpuRead(next.TempAddress &&& 0x00FFus) |> ignore
            smbOp bit |> fun op -> op.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let smb0 = smb 0
    let smb1 = smb 1
    let smb2 = smb 2
    let smb3 = smb 3
    let smb4 = smb 4
    let smb5 = smb 5
    let smb6 = smb 6
    let smb7 = smb 7

    // BBR0-BBR7 - Branch on Bit Reset (0x0F, 0x1F, 0x2F, 0x3F, 0x4F, 0x5F, 0x6F, 0x7F)
    // Not taken: 5 cycles, Taken (no page cross): 6 cycles, Taken (page cross): 7 cycles
    // T0: Fetch opcode
    // T1: Fetch ZP address
    // T2: Read from ZP
    // T3: Dummy read from ZP (same address)
    // T4: Fetch offset
    // T5: Dummy read at oldPC (if taken)
    // T6: Dummy read at oldPC again (if page cross)
    let bbr (bit: int) : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            // Read ZP and store the value in TempValue for later use
            let zpAddr = next.TempAddress &&& 0x00FFus
            let zpValue = bus.CpuRead(zpAddr)
            next.TempValue <- uint16 zpValue)
        microOp (fun prev next bus ->
            // Dummy read at ZP address (same as T2)
            let zpAddr = next.TempAddress &&& 0x00FFus
            bus.CpuRead(zpAddr) |> ignore)
        microOp (fun prev next bus ->
            // Fetch branch offset
            let offset = int8 (bus.CpuRead(next.PC))
            next.PC <- next.PC + 1us
            // Decide whether to branch
            let zpValue = byte next.TempValue
            let mask = 1uy <<< bit
            if (zpValue &&& mask) = 0uy then
                let oldPC = next.PC
                let newPC = uint16 (int next.PC + int offset)
                next.PC <- newPC
                if (oldPC >>> 8) <> (newPC >>> 8) then
                    // Page crossing: T5 dummy read at oldPC, T6 dummy read at oldPC + complete
                    let penaltyT5 = microOp (fun _ n b ->
                        b.CpuRead(oldPC) |> ignore)
                    let penaltyT6 = microOp (fun _ n b ->
                        b.CpuRead(oldPC) |> ignore
                        n.InstructionComplete <- true)
                    insertAfterCurrentOp next penaltyT5
                    next.Pipeline <- Array.append next.Pipeline [| penaltyT6 |]
                else
                    // No page cross: just T5 dummy read at oldPC + complete
                    let penaltyOp = microOp (fun _ n b ->
                        b.CpuRead(oldPC) |> ignore
                        n.InstructionComplete <- true)
                    insertAfterCurrentOp next penaltyOp
            else
                next.InstructionComplete <- true)
    |]

    let bbr0 = bbr 0
    let bbr1 = bbr 1
    let bbr2 = bbr 2
    let bbr3 = bbr 3
    let bbr4 = bbr 4
    let bbr5 = bbr 5
    let bbr6 = bbr 6
    let bbr7 = bbr 7

    // BBS0-BBS7 - Branch on Bit Set (0x8F, 0x9F, 0xAF, 0xBF, 0xCF, 0xDF, 0xEF, 0xFF)
    // Not taken: 5 cycles, Taken (no page cross): 6 cycles, Taken (page cross): 7 cycles
    // T0: Fetch opcode
    // T1: Fetch ZP address
    // T2: Read from ZP
    // T3: Dummy read from ZP (same address)
    // T4: Fetch offset
    // T5: Dummy read at oldPC (if taken)
    // T6: Dummy read at oldPC again (if page cross)
    let bbs (bit: int) : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            // Read ZP and store the value in TempValue for later use
            let zpAddr = next.TempAddress &&& 0x00FFus
            let zpValue = bus.CpuRead(zpAddr)
            next.TempValue <- uint16 zpValue)
        microOp (fun prev next bus ->
            // Dummy read at ZP address (same as T2)
            let zpAddr = next.TempAddress &&& 0x00FFus
            bus.CpuRead(zpAddr) |> ignore)
        microOp (fun prev next bus ->
            // Fetch branch offset
            let offset = int8 (bus.CpuRead(next.PC))
            next.PC <- next.PC + 1us
            // Decide whether to branch
            let zpValue = byte next.TempValue
            let mask = 1uy <<< bit
            if (zpValue &&& mask) <> 0uy then
                let oldPC = next.PC
                let newPC = uint16 (int next.PC + int offset)
                next.PC <- newPC
                if (oldPC >>> 8) <> (newPC >>> 8) then
                    // Page crossing: T5 dummy read at oldPC, T6 dummy read at oldPC + complete
                    let penaltyT5 = microOp (fun _ n b ->
                        b.CpuRead(oldPC) |> ignore)
                    let penaltyT6 = microOp (fun _ n b ->
                        b.CpuRead(oldPC) |> ignore
                        n.InstructionComplete <- true)
                    insertAfterCurrentOp next penaltyT5
                    next.Pipeline <- Array.append next.Pipeline [| penaltyT6 |]
                else
                    // No page cross: just T5 dummy read at oldPC + complete
                    let penaltyOp = microOp (fun _ n b ->
                        b.CpuRead(oldPC) |> ignore
                        n.InstructionComplete <- true)
                    insertAfterCurrentOp next penaltyOp
            else
                next.InstructionComplete <- true)
    |]

    let bbs0 = bbs 0
    let bbs1 = bbs 1
    let bbs2 = bbs 2
    let bbs3 = bbs 3
    let bbs4 = bbs 4
    let bbs5 = bbs 5
    let bbs6 = bbs 6
    let bbs7 = bbs 7

    // ========================================
    // Illegal 6502 Opcodes
    // ========================================

    // JAM/KIL - Freeze CPU (11 cycles of specific bus activity, then CPU is jammed)
    // The real 6502 gets stuck in an internal loop reading from the interrupt vector.
    // Cycle pattern observed on hardware:
    //   T0: Fetch opcode (PC increments to PC+1)
    //   T1: Read from PC (does NOT increment - CPU is stuck)
    //   T2: Read $FFFF
    //   T3: Read $FFFE
    //   T4: Read $FFFE
    //   T5-T10: Read $FFFF (6 times)
    let jam : MicroOp[] = [|
        fetchOpcode
        dummyReadPC  // T1: Read from PC but don't increment (CPU is jammed)
        microOp (fun prev next bus -> bus.CpuRead(0xFFFFus) |> ignore)  // T2
        microOp (fun prev next bus -> bus.CpuRead(0xFFFEus) |> ignore)  // T3
        microOp (fun prev next bus -> bus.CpuRead(0xFFFEus) |> ignore)  // T4
        microOp (fun prev next bus -> bus.CpuRead(0xFFFFus) |> ignore)  // T5
        microOp (fun prev next bus -> bus.CpuRead(0xFFFFus) |> ignore)  // T6
        microOp (fun prev next bus -> bus.CpuRead(0xFFFFus) |> ignore)  // T7
        microOp (fun prev next bus -> bus.CpuRead(0xFFFFus) |> ignore)  // T8
        microOp (fun prev next bus -> bus.CpuRead(0xFFFFus) |> ignore)  // T9
        microOp (fun prev next bus ->                                   // T10
            bus.CpuRead(0xFFFFus) |> ignore
            jamOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // LAX - Load A and X (same timing as LDA)
    let lax_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            readZeroPage.Invoke(prev, next, bus)
            laxOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let lax_zpy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addYZeroPage
        microOp (fun prev next bus ->
            readZeroPage.Invoke(prev, next, bus)
            laxOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let lax_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            laxOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let lax_absy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            fetchAddressHigh.Invoke(prev, next, bus)
            addYCheckPage.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            laxOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let lax_izx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readPointerLowZP
        readPointerHighZP
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            laxOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let lax_izy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        readPointerHighZPAddY
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            laxOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // SAX - Store A AND X (same timing as STA)
    let sax_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            saxOp.Invoke(prev, next, bus)
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let sax_zpy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addYZeroPage
        microOp (fun prev next bus ->
            saxOp.Invoke(prev, next, bus)
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let sax_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        microOp (fun prev next bus ->
            saxOp.Invoke(prev, next, bus)
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let sax_izx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readPointerLowZP
        readPointerHighZP
        microOp (fun prev next bus ->
            saxOp.Invoke(prev, next, bus)
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // DCP - Decrement then Compare (RMW timing - requires dummy write)
    let dcp_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        microOp (fun prev next bus ->
            dummyWriteZeroPage.Invoke(prev, next, bus)
            dcpOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let dcp_zpx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readZeroPage
        microOp (fun prev next bus ->
            dummyWriteZeroPage.Invoke(prev, next, bus)
            dcpOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let dcp_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        readFromTempAddress
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            dcpOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let dcp_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addXWithDummyRead  // RMW always takes the extra cycle
        readFromTempAddress
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            dcpOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let dcp_absy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addYWithDummyRead  // RMW always takes the extra cycle
        readFromTempAddress
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            dcpOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let dcp_izx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readPointerLowZP
        readPointerHighZP
        readFromTempAddress
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            dcpOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let dcp_izy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        readPointerHighZPSetupRMWIZY  // T3: read high byte, store wrong/correct addresses
        readWrongAddressFixRMWIZY     // T4: read from wrong address, fix TempAddress
        readFromTempAddress           // T5: read from correct address
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            dcpOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ISC/ISB - Increment then Subtract (RMW timing - requires dummy write)
    let isc_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        microOp (fun prev next bus ->
            dummyWriteZeroPage.Invoke(prev, next, bus)
            iscOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let isc_zpx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readZeroPage
        microOp (fun prev next bus ->
            dummyWriteZeroPage.Invoke(prev, next, bus)
            iscOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let isc_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        readFromTempAddress
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            iscOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let isc_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addXWithDummyRead  // RMW always takes the extra cycle
        readFromTempAddress
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            iscOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let isc_absy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addYWithDummyRead  // RMW always takes the extra cycle
        readFromTempAddress
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            iscOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let isc_izx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readPointerLowZP
        readPointerHighZP
        readFromTempAddress
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            iscOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let isc_izy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        readPointerHighZPSetupRMWIZY  // T3: read high byte, store wrong/correct addresses
        readWrongAddressFixRMWIZY     // T4: read from wrong address, fix TempAddress
        readFromTempAddress           // T5: read from correct address
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            iscOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // SLO - Shift Left then OR (RMW timing - requires dummy write)
    let slo_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        microOp (fun prev next bus ->
            dummyWriteZeroPage.Invoke(prev, next, bus)
            sloOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let slo_zpx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readZeroPage
        microOp (fun prev next bus ->
            dummyWriteZeroPage.Invoke(prev, next, bus)
            sloOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let slo_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        readFromTempAddress
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            sloOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let slo_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addXWithDummyRead  // RMW always takes the extra cycle
        readFromTempAddress
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            sloOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let slo_absy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addYWithDummyRead  // RMW always takes the extra cycle
        readFromTempAddress
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            sloOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let slo_izx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readPointerLowZP
        readPointerHighZP
        readFromTempAddress
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            sloOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let slo_izy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        readPointerHighZPSetupRMWIZY  // T3: read high byte, store wrong/correct addresses
        readWrongAddressFixRMWIZY     // T4: read from wrong address, fix TempAddress
        readFromTempAddress           // T5: read from correct address
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            sloOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // RLA - Rotate Left then AND (RMW timing - requires dummy write)
    let rla_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        microOp (fun prev next bus ->
            dummyWriteZeroPage.Invoke(prev, next, bus)
            rlaOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let rla_zpx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readZeroPage
        microOp (fun prev next bus ->
            dummyWriteZeroPage.Invoke(prev, next, bus)
            rlaOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let rla_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        readFromTempAddress
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            rlaOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let rla_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addXWithDummyRead  // RMW always takes the extra cycle
        readFromTempAddress
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            rlaOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let rla_absy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addYWithDummyRead  // RMW always takes the extra cycle
        readFromTempAddress
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            rlaOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let rla_izx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readPointerLowZP
        readPointerHighZP
        readFromTempAddress
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            rlaOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let rla_izy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        readPointerHighZPSetupRMWIZY  // T3: read high byte, store wrong/correct addresses
        readWrongAddressFixRMWIZY     // T4: read from wrong address, fix TempAddress
        readFromTempAddress           // T5: read from correct address
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            rlaOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // SRE - Shift Right then EOR (RMW timing - requires dummy write)
    let sre_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        microOp (fun prev next bus ->
            dummyWriteZeroPage.Invoke(prev, next, bus)
            sreOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let sre_zpx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readZeroPage
        microOp (fun prev next bus ->
            dummyWriteZeroPage.Invoke(prev, next, bus)
            sreOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let sre_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        readFromTempAddress
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            sreOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let sre_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addXWithDummyRead  // RMW always takes the extra cycle
        readFromTempAddress
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            sreOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let sre_absy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addYWithDummyRead  // RMW always takes the extra cycle
        readFromTempAddress
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            sreOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let sre_izx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readPointerLowZP
        readPointerHighZP
        readFromTempAddress
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            sreOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let sre_izy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        readPointerHighZPSetupRMWIZY  // T3: read high byte, store wrong/correct addresses
        readWrongAddressFixRMWIZY     // T4: read from wrong address, fix TempAddress
        readFromTempAddress           // T5: read from correct address
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            sreOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // RRA - Rotate Right then ADC (RMW timing - requires dummy write)
    let rra_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        microOp (fun prev next bus ->
            dummyWriteZeroPage.Invoke(prev, next, bus)
            rraOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let rra_zpx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readZeroPage
        microOp (fun prev next bus ->
            dummyWriteZeroPage.Invoke(prev, next, bus)
            rraOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let rra_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        readFromTempAddress
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            rraOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let rra_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addXWithDummyRead  // RMW always takes the extra cycle
        readFromTempAddress
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            rraOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let rra_absy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addYWithDummyRead  // RMW always takes the extra cycle
        readFromTempAddress
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            rraOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let rra_izx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readPointerLowZP
        readPointerHighZP
        readFromTempAddress
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            rraOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let rra_izy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        readPointerHighZPSetupRMWIZY  // T3: read high byte, store wrong/correct addresses
        readWrongAddressFixRMWIZY     // T4: read from wrong address, fix TempAddress
        readFromTempAddress           // T5: read from correct address
        microOp (fun prev next bus ->
            dummyWriteTempAddress.Invoke(prev, next, bus)
            rraOp.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ANC - AND immediate, set C from bit 7
    let anc_imm : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            fetchImmediate.Invoke(prev, next, bus)
            ancOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ALR - AND immediate then LSR
    let alr_imm : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            fetchImmediate.Invoke(prev, next, bus)
            alrOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ARR - AND immediate then ROR
    let arr_imm : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            fetchImmediate.Invoke(prev, next, bus)
            arrOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // AXS/SBX - X = (A AND X) - immediate
    let axs_imm : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            fetchImmediate.Invoke(prev, next, bus)
            axsOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ========================================
    // Unstable/Unreliable Illegal Opcodes
    // ========================================

    // ANE/XAA - A = (A | const) & X & imm (0x8B) - 2 cycles
    let ane_imm : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            fetchImmediate.Invoke(prev, next, bus)
            aneOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // LXA/LAX imm - A = X = (A | const) & imm (0xAB) - 2 cycles
    let lxa_imm : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            fetchImmediate.Invoke(prev, next, bus)
            lxaOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // LAS - A = X = SP = SP & mem (0xBB) - 4+ cycles (abs,Y with page crossing)
    let las_absy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            fetchAddressHigh.Invoke(prev, next, bus)
            addYCheckPage.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            lasOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // SHA/AHX (indirect),Y - Store (A & X & (high+1)) (0x93) - 6 cycles
    // Uses special address calculation: on page cross, write addr high = base_high & value
    let sha_izy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        microOp (fun prev next bus ->
            // Read pointer high byte and combine to get base address
            let zpAddr = (next.TempValue + 1us) &&& 0x00FFus
            let hi = bus.CpuRead(zpAddr)
            let baseAddr = next.TempAddress ||| (uint16 hi <<< 8)
            let baseHigh = byte (baseAddr >>> 8)
            // Calculate final address (with Y added)
            let finalAddr = baseAddr + uint16 next.Y
            let finalHigh = byte (finalAddr >>> 8)
            let finalLow = byte (finalAddr &&& 0xFFus)
            // Store baseHigh in upper byte of TempValue, finalAddr in TempAddress
            // Also store finalLow in lower byte of TempValue for the wrong address calculation
            next.TempValue <- (uint16 baseHigh <<< 8) ||| uint16 finalLow
            next.TempAddress <- finalAddr)
        microOp (fun prev next bus ->
            // Dummy read from WRONG address (base_high : final_low)
            let baseHigh = byte (next.TempValue >>> 8)
            let finalLow = byte (next.TempValue &&& 0xFFus)
            let wrongAddr = (uint16 baseHigh <<< 8) ||| uint16 finalLow
            bus.CpuRead(wrongAddr) |> ignore)
        microOp (fun prev next bus ->
            // Calculate value and write
            let baseHigh = byte (next.TempValue >>> 8)
            let finalHigh = byte (next.TempAddress >>> 8)
            let finalLow = byte (next.TempAddress &&& 0xFFus)
            let value = next.A &&& next.X &&& (baseHigh + 1uy)
            // If page crossed, write addr high = value, else final_high
            let writeHigh = if baseHigh <> finalHigh then value else finalHigh
            let writeAddr = (uint16 writeHigh <<< 8) ||| uint16 finalLow
            bus.Write(writeAddr, value)
            next.InstructionComplete <- true)
    |]

    // SHA/AHX abs,Y - Store (A & X & (high+1)) (0x9F) - 5 cycles
    let sha_absy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            fetchAddressHigh.Invoke(prev, next, bus)
            // Store base address high and low in TempValue
            let baseHigh = byte (next.TempAddress >>> 8)
            let baseLow = byte (next.TempAddress &&& 0xFFus)
            next.TempValue <- next.TempAddress)
        microOp (fun prev next bus ->
            // Add Y and do dummy read from WRONG address
            let baseHigh = byte (next.TempValue >>> 8)
            let baseLow = byte (next.TempValue &&& 0xFFus)
            let finalAddr = next.TempValue + uint16 next.Y
            let finalLow = byte (finalAddr &&& 0xFFus)
            next.TempAddress <- finalAddr
            // Wrong address = base_high : final_low
            let wrongAddr = (uint16 baseHigh <<< 8) ||| uint16 finalLow
            bus.CpuRead(wrongAddr) |> ignore)
        microOp (fun prev next bus ->
            // Calculate value and write
            let baseHigh = byte (next.TempValue >>> 8)
            let finalHigh = byte (next.TempAddress >>> 8)
            let finalLow = byte (next.TempAddress &&& 0xFFus)
            let value = next.A &&& next.X &&& (baseHigh + 1uy)
            // If page crossed, write addr high = value, else final_high
            let writeHigh = if baseHigh <> finalHigh then value else finalHigh
            let writeAddr = (uint16 writeHigh <<< 8) ||| uint16 finalLow
            bus.Write(writeAddr, value)
            next.InstructionComplete <- true)
    |]

    // SHX - Store (X & (high+1)) abs,Y (0x9E) - 5 cycles
    let shx_absy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            fetchAddressHigh.Invoke(prev, next, bus)
            next.TempValue <- next.TempAddress)
        microOp (fun prev next bus ->
            let baseHigh = byte (next.TempValue >>> 8)
            let finalAddr = next.TempValue + uint16 next.Y
            let finalLow = byte (finalAddr &&& 0xFFus)
            next.TempAddress <- finalAddr
            let wrongAddr = (uint16 baseHigh <<< 8) ||| uint16 finalLow
            bus.CpuRead(wrongAddr) |> ignore)
        microOp (fun prev next bus ->
            let baseHigh = byte (next.TempValue >>> 8)
            let finalHigh = byte (next.TempAddress >>> 8)
            let finalLow = byte (next.TempAddress &&& 0xFFus)
            let value = next.X &&& (baseHigh + 1uy)
            // If page crossed, write addr high = value, else final_high
            let writeHigh = if baseHigh <> finalHigh then value else finalHigh
            let writeAddr = (uint16 writeHigh <<< 8) ||| uint16 finalLow
            bus.Write(writeAddr, value)
            next.InstructionComplete <- true)
    |]

    // SHY - Store (Y & (high+1)) abs,X (0x9C) - 5 cycles
    let shy_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            fetchAddressHigh.Invoke(prev, next, bus)
            next.TempValue <- next.TempAddress)
        microOp (fun prev next bus ->
            let baseHigh = byte (next.TempValue >>> 8)
            let finalAddr = next.TempValue + uint16 next.X
            let finalLow = byte (finalAddr &&& 0xFFus)
            next.TempAddress <- finalAddr
            let wrongAddr = (uint16 baseHigh <<< 8) ||| uint16 finalLow
            bus.CpuRead(wrongAddr) |> ignore)
        microOp (fun prev next bus ->
            let baseHigh = byte (next.TempValue >>> 8)
            let finalHigh = byte (next.TempAddress >>> 8)
            let finalLow = byte (next.TempAddress &&& 0xFFus)
            let value = next.Y &&& (baseHigh + 1uy)
            // If page crossed, write addr high = value, else final_high
            let writeHigh = if baseHigh <> finalHigh then value else finalHigh
            let writeAddr = (uint16 writeHigh <<< 8) ||| uint16 finalLow
            bus.Write(writeAddr, value)
            next.InstructionComplete <- true)
    |]

    // TAS/SHS - SP = A & X, Store (A & X & (high+1)) abs,Y (0x9B) - 5 cycles
    let tas_absy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            fetchAddressHigh.Invoke(prev, next, bus)
            next.TempValue <- next.TempAddress)
        microOp (fun prev next bus ->
            let baseHigh = byte (next.TempValue >>> 8)
            let finalAddr = next.TempValue + uint16 next.Y
            let finalLow = byte (finalAddr &&& 0xFFus)
            next.TempAddress <- finalAddr
            let wrongAddr = (uint16 baseHigh <<< 8) ||| uint16 finalLow
            bus.CpuRead(wrongAddr) |> ignore)
        microOp (fun prev next bus ->
            next.SP <- next.A &&& next.X
            let baseHigh = byte (next.TempValue >>> 8)
            let finalHigh = byte (next.TempAddress >>> 8)
            let finalLow = byte (next.TempAddress &&& 0xFFus)
            let value = next.A &&& next.X &&& (baseHigh + 1uy)
            // If page crossed, write addr high = value, else final_high
            let writeHigh = if baseHigh <> finalHigh then value else finalHigh
            let writeAddr = (uint16 writeHigh <<< 8) ||| uint16 finalLow
            bus.Write(writeAddr, value)
            next.InstructionComplete <- true)
    |]

    // Unofficial SBC duplicate (NMOS only)
    let sbc_imm_unofficial : MicroOp[] = sbc_imm_nmos

    // NOP variants that skip bytes
    let nop_imm : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            fetchImmediate.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // NOP zp - 3 cycles (reads from zero page but discards result)
    let nop_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            readZeroPage.Invoke(prev, next, bus)  // Read but discard
            markComplete.Invoke(prev, next, bus))
    |]

    // NOP zp,X - 4 cycles (reads from zero page + X but discards result)
    let nop_zpx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        microOp (fun prev next bus ->
            readZeroPage.Invoke(prev, next, bus)  // Read but discard
            markComplete.Invoke(prev, next, bus))
    |]

    // NOP abs ($0C) - 4 cycles (NMOS illegal opcode)
    // T0: Fetch opcode
    // T1: Fetch low byte
    // T2: Fetch high byte
    // T3: Read from effective address (then discard)
    let nop_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        microOp (fun prev next bus ->
            // Read from effective address but discard
            bus.CpuRead(next.TempAddress) |> ignore
            markComplete.Invoke(prev, next, bus))
    |]

    // NOP abs ($5C, $DC, $FC) - 4 cycles (65C02)
    // T0: Fetch opcode
    // T1: Fetch low byte
    // T2: Fetch high byte
    // T3: Dummy read at high byte address (same as T2)
    let nop_abs_65c02 : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            let highByteAddr = next.PC
            fetchAddressHigh.Invoke(prev, next, bus)
            next.TempValue <- highByteAddr)
        microOp (fun prev next bus ->
            bus.CpuRead(next.TempValue) |> ignore
            markComplete.Invoke(prev, next, bus))
    |]

    // NOP abs,X - 4+ cycles (reads from absolute + X but discards result)
    let nop_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            fetchAddressHigh.Invoke(prev, next, bus)
            addXCheckPage.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)  // Read but discard
            markComplete.Invoke(prev, next, bus))
    |]

    // STP - Stop the processor (0xDB) - 3 cycles (65C02)
    let stp : MicroOp[] = [|
        fetchOpcode
        dummyReadPC
        microOp (fun prev next bus ->
            stpOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // WAI - Wait for interrupt (0xCB) - 3 cycles (65C02)
    let wai : MicroOp[] = [|
        fetchOpcode
        dummyReadPC
        microOp (fun prev next bus ->
            waiOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let unimplemented : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            markComplete.Invoke(prev, next, bus))
    |]

  

    /// Base pipeline table with all instructions common to 6502 and 65C02
    /// Uses NMOS JMP indirect (with page boundary bug) as default
    let pipelinesBase6502 : MicroOp[][] =
        let table = Array.create 256 unimplemented

        // Core instructions shared by all variants

        table.[0x00] <- brk
        table.[0x01] <- ora_izx
        //02
        //03
        //04
        table.[0x05] <- ora_zp
        table.[0x06] <- asl_zp
        //07
        table.[0x08] <- php_impl
        table.[0x09] <- ora_imm
        table.[0x0A] <- asl_a
        //0B
        //0C
        table.[0x0D] <- ora_abs
        table.[0x0E] <- asl_abs
        //0F
        table.[0x10] <- bpl
        table.[0x11] <- ora_izy
        //12
        //13
        //14
        table.[0x15] <- ora_zpx
        table.[0x16] <- asl_zpx
        //17
        table.[0x18] <- clc_impl
        table.[0x19] <- ora_absy
        //1A
        //1B
        //1C
        table.[0x1D] <- ora_absx
        table.[0x1E] <- asl_absx
        //1F
        table.[0x20] <- jsr
        table.[0x21] <- and_izx
        //22
        //23
        table.[0x24] <- bit_zp
        table.[0x25] <- and_zp
        table.[0x26] <- rol_zp
        //27
        table.[0x28] <- plp_impl
        table.[0x29] <- and_imm
        table.[0x2A] <- rol_a
        //2B
        table.[0x2C] <- bit_abs
        table.[0x2D] <- and_abs
        table.[0x2E] <- rol_abs
        //2F
        table.[0x30] <- bmi
        table.[0x31] <- and_izy
        //32
        //33
        //34
        table.[0x35] <- and_zpx
        table.[0x36] <- rol_zpx
        //37
        table.[0x38] <- sec_impl
        table.[0x39] <- and_absy
        //3A
        //3B
        //3C
        table.[0x3D] <- and_absx
        table.[0x3E] <- rol_absx
        //3F
        table.[0x40] <- rti
        table.[0x41] <- eor_izx
        //42
        //43
        //44
        table.[0x45] <- eor_zp
        table.[0x46] <- lsr_zp
        //47
        table.[0x48] <- pha_impl
        table.[0x49] <- eor_imm
        table.[0x4A] <- lsr_a
        //4B
        table.[0x4C] <- jmp_abs
        table.[0x4D] <- eor_abs
        table.[0x4E] <- lsr_abs
        //4F
        table.[0x50] <- bvc
        table.[0x51] <- eor_izy
        //52
        //53
        //54
        table.[0x55] <- eor_zpx
        table.[0x56] <- lsr_zpx
        //57
        table.[0x58] <- cli_impl
        table.[0x59] <- eor_absy
        //5A
        //5B
        //5C
        table.[0x5D] <- eor_absx
        table.[0x5E] <- lsr_absx
        //5F
        table.[0x60] <- rts
        table.[0x61] <- adc_izx_nmos
        //62
        //63
        //64
        table.[0x65] <- adc_zp_nmos
        table.[0x66] <- ror_zp
        //67
        table.[0x68] <- pla_impl
        table.[0x69] <- adc_imm_nmos
        table.[0x6A] <- ror_a
        //6B
        table.[0x6C] <- jmp_ind_nmos
        table.[0x6D] <- adc_abs_nmos
        table.[0x6E] <- ror_abs
        //6F
        table.[0x70] <- bvs
        table.[0x71] <- adc_izy_nmos
        //72
        //73
        //74
        table.[0x75] <- adc_zpx_nmos
        table.[0x76] <- ror_zpx
        //77
        table.[0x78] <- sei_impl
        table.[0x79] <- adc_absy_nmos
        //7A
        //7B
        //7C
        table.[0x7D] <- adc_absx_nmos
        table.[0x7E] <- ror_absx
        //7F
        //80
        table.[0x81] <- sta_izx
        //82
        //83
        table.[0x84] <- sty_zp
        table.[0x85] <- sta_zp
        table.[0x86] <- stx_zp
        //87
        table.[0x88] <- dey_impl
        //89
        table.[0x8A] <- txa_impl
        //8B
        table.[0x8C] <- sty_abs
        table.[0x8D] <- sta_abs
        table.[0x8E] <- stx_abs
        //8F
        table.[0x90] <- bcc
        table.[0x91] <- sta_izy
        //92
        //93
        table.[0x94] <- sty_zpx
        table.[0x95] <- sta_zpx
        table.[0x96] <- stx_zpy
        //97
        table.[0x98] <- tya_impl
        table.[0x99] <- sta_absy
        table.[0x9A] <- txs_impl
        //9B
        //9C
        table.[0x9D] <- sta_absx
        //9E
        //9F
        table.[0xA0] <- ldy_imm
        table.[0xA1] <- lda_izx
        table.[0xA2] <- ldx_imm
        //A3
        table.[0xA4] <- ldy_zp
        table.[0xA5] <- lda_zp
        table.[0xA6] <- ldx_zp
        //A7
        table.[0xA8] <- tay_impl
        table.[0xA9] <- lda_imm
        table.[0xAA] <- tax_impl
        //AB
        table.[0xAC] <- ldy_abs
        table.[0xAD] <- lda_abs
        table.[0xAE] <- ldx_abs
        //AF
        table.[0xB0] <- bcs
        table.[0xB1] <- lda_izy
        //B2
        //B3
        table.[0xB4] <- ldy_zpx
        table.[0xB5] <- lda_zpx
        table.[0xB6] <- ldx_zpy
        //B7
        table.[0xB8] <- clv_impl
        table.[0xB9] <- lda_absy
        table.[0xBA] <- tsx_impl
        //BB
        table.[0xBC] <- ldy_absx
        table.[0xBD] <- lda_absx
        table.[0xBE] <- ldx_absy
        //BF
        table.[0xC0] <- cpy_imm
        table.[0xC1] <- cmp_izx
        //C2
        //C3
        table.[0xC4] <- cpy_zp
        table.[0xC5] <- cmp_zp
        table.[0xC6] <- dec_zp
        //C7
        table.[0xC8] <- iny_impl
        table.[0xC9] <- cmp_imm
        table.[0xCA] <- dex_impl
        //CB
        table.[0xCC] <- cpy_abs
        table.[0xCD] <- cmp_abs
        table.[0xCE] <- dec_abs
        //CF
        table.[0xD0] <- bne
        table.[0xD1] <- cmp_izy
        //D2
        //D3
        //D4
        table.[0xD5] <- cmp_zpx
        table.[0xD6] <- dec_zpx
        //D7
        table.[0xD8] <- cld_impl
        table.[0xD9] <- cmp_absy
        //DA
        //DB
        //DC
        table.[0xDD] <- cmp_absx
        table.[0xDE] <- dec_absx
        //DF
        table.[0xE0] <- cpx_imm
        table.[0xE1] <- sbc_izx_nmos
        //E2
        //E3
        table.[0xE4] <- cpx_zp
        table.[0xE5] <- sbc_zp_nmos
        table.[0xE6] <- inc_zp
        //E7
        table.[0xE8] <- inx_impl
        table.[0xE9] <- sbc_imm_nmos
        table.[0xEA] <- nop
        //EB
        table.[0xEC] <- cpx_abs
        table.[0xED] <- sbc_abs_nmos
        table.[0xEE] <- inc_abs
        //EF
        table.[0xF0] <- beq
        table.[0xF1] <- sbc_izy_nmos
        //F2
        //F3
        //F4
        table.[0xF5] <- sbc_zpx_nmos
        table.[0xF6] <- inc_zpx
        //F7
        table.[0xF8] <- sed_impl
        table.[0xF9] <- sbc_absy_nmos
        //FA
        //FB
        //FC
        table.[0xFD] <- sbc_absx_nmos
        table.[0xFE] <- inc_absx
        //FF
        table

    /// NMOS 6502 without illegal opcodes - treats undefined opcodes as NOPs
    let pipelines6502NoIllegal : MicroOp[][] = 
        let table = Array.copy pipelinesBase6502

        // NOP variants (skip bytes) - same behavior as NMOS6502 but without illegal instructions
        table.[0x1A] <- nop          // NOP implied
        table.[0x3A] <- nop
        table.[0x5A] <- nop
        table.[0x7A] <- nop
        table.[0xDA] <- nop
        table.[0xFA] <- nop

        table.[0x80] <- nop_imm      // NOP #imm
        table.[0x82] <- nop_imm
        table.[0x89] <- nop_imm
        table.[0xC2] <- nop_imm
        table.[0xE2] <- nop_imm

        table.[0x04] <- nop_zp       // NOP zp
        table.[0x44] <- nop_zp
        table.[0x64] <- nop_zp

        table.[0x14] <- nop_zpx      // NOP zp,X
        table.[0x34] <- nop_zpx
        table.[0x54] <- nop_zpx
        table.[0x74] <- nop_zpx
        table.[0xD4] <- nop_zpx
        table.[0xF4] <- nop_zpx

        table.[0x0C] <- nop_abs      // NOP abs

        table.[0x1C] <- nop_absx     // NOP abs,X
        table.[0x3C] <- nop_absx
        table.[0x5C] <- nop_absx
        table.[0x7C] <- nop_absx
        table.[0xDC] <- nop_absx
        table.[0xFC] <- nop_absx

        table

    /// NMOS 6502 with illegal opcodes
    let pipelines6502 : MicroOp[][] = 
        let table = Array.copy pipelinesBase6502

        // JAM/KIL - Freeze CPU
        table.[0x02] <- jam
        table.[0x12] <- jam
        table.[0x22] <- jam
        table.[0x32] <- jam
        table.[0x42] <- jam
        table.[0x52] <- jam
        table.[0x62] <- jam
        table.[0x72] <- jam
        table.[0x92] <- jam
        table.[0xB2] <- jam
        table.[0xD2] <- jam
        table.[0xF2] <- jam

        // LAX - Load A and X
        table.[0xA7] <- lax_zp
        table.[0xB7] <- lax_zpy
        table.[0xAF] <- lax_abs
        table.[0xBF] <- lax_absy
        table.[0xA3] <- lax_izx
        table.[0xB3] <- lax_izy

        // SAX - Store A AND X
        table.[0x87] <- sax_zp
        table.[0x97] <- sax_zpy
        table.[0x8F] <- sax_abs
        table.[0x83] <- sax_izx

        // DCP - Decrement then Compare
        table.[0xC7] <- dcp_zp
        table.[0xD7] <- dcp_zpx
        table.[0xCF] <- dcp_abs
        table.[0xDF] <- dcp_absx
        table.[0xDB] <- dcp_absy
        table.[0xC3] <- dcp_izx
        table.[0xD3] <- dcp_izy

        // ISC/ISB - Increment then Subtract
        table.[0xE7] <- isc_zp
        table.[0xF7] <- isc_zpx
        table.[0xEF] <- isc_abs
        table.[0xFF] <- isc_absx
        table.[0xFB] <- isc_absy
        table.[0xE3] <- isc_izx
        table.[0xF3] <- isc_izy

        // SLO - Shift Left then OR
        table.[0x07] <- slo_zp
        table.[0x17] <- slo_zpx
        table.[0x0F] <- slo_abs
        table.[0x1F] <- slo_absx
        table.[0x1B] <- slo_absy
        table.[0x03] <- slo_izx
        table.[0x13] <- slo_izy

        // RLA - Rotate Left then AND
        table.[0x27] <- rla_zp
        table.[0x37] <- rla_zpx
        table.[0x2F] <- rla_abs
        table.[0x3F] <- rla_absx
        table.[0x3B] <- rla_absy
        table.[0x23] <- rla_izx
        table.[0x33] <- rla_izy

        // SRE - Shift Right then EOR
        table.[0x47] <- sre_zp
        table.[0x57] <- sre_zpx
        table.[0x4F] <- sre_abs
        table.[0x5F] <- sre_absx
        table.[0x5B] <- sre_absy
        table.[0x43] <- sre_izx
        table.[0x53] <- sre_izy

        // RRA - Rotate Right then ADC
        table.[0x67] <- rra_zp
        table.[0x77] <- rra_zpx
        table.[0x6F] <- rra_abs
        table.[0x7F] <- rra_absx
        table.[0x7B] <- rra_absy
        table.[0x63] <- rra_izx
        table.[0x73] <- rra_izy

        // Immediate illegal ops
        table.[0x0B] <- anc_imm      // ANC
        table.[0x2B] <- anc_imm      // ANC (duplicate)
        table.[0x4B] <- alr_imm      // ALR
        table.[0x6B] <- arr_imm      // ARR
        table.[0xCB] <- axs_imm      // AXS/SBX
        table.[0xEB] <- sbc_imm_unofficial  // SBC (unofficial duplicate)

        // NOP variants (skip bytes)
        table.[0x1A] <- nop          // NOP implied
        table.[0x3A] <- nop
        table.[0x5A] <- nop
        table.[0x7A] <- nop
        table.[0xDA] <- nop
        table.[0xFA] <- nop

        table.[0x80] <- nop_imm      // NOP #imm
        table.[0x82] <- nop_imm
        table.[0x89] <- nop_imm
        table.[0xC2] <- nop_imm
        table.[0xE2] <- nop_imm

        table.[0x04] <- nop_zp       // NOP zp
        table.[0x44] <- nop_zp
        table.[0x64] <- nop_zp

        table.[0x14] <- nop_zpx      // NOP zp,X
        table.[0x34] <- nop_zpx
        table.[0x54] <- nop_zpx
        table.[0x74] <- nop_zpx
        table.[0xD4] <- nop_zpx
        table.[0xF4] <- nop_zpx

        table.[0x0C] <- nop_abs      // NOP abs

        table.[0x1C] <- nop_absx     // NOP abs,X
        table.[0x3C] <- nop_absx
        table.[0x5C] <- nop_absx
        table.[0x7C] <- nop_absx
        table.[0xDC] <- nop_absx
        table.[0xFC] <- nop_absx

        // Unstable/unreliable illegal opcodes
        table.[0x8B] <- ane_imm      // ANE/XAA
        table.[0xAB] <- lxa_imm      // LXA/LAX imm
        table.[0xBB] <- las_absy     // LAS
        table.[0x93] <- sha_izy      // SHA/AHX (indirect),Y
        table.[0x9F] <- sha_absy     // SHA/AHX abs,Y
        table.[0x9E] <- shx_absy     // SHX
        table.[0x9C] <- shy_absx     // SHY
        table.[0x9B] <- tas_absy     // TAS/SHS

        table

    /// WDC 65C02 - adds 65C02-only instructions and fixes JMP indirect bug
    let pipelines65C02 : MicroOp[][] =
        let table = Array.copy pipelinesBase6502

        // 65C02 BRK clears decimal flag
        table.[0x00] <- brk_65c02

        // Fix JMP indirect page boundary bug
        table.[0x6C] <- jmp_ind

        // 65C02 uses CMOS ADC/SBC (N/Z flags from BCD result in decimal mode)
        table.[0x61] <- adc_izx_cmos
        table.[0x65] <- adc_zp_cmos
        table.[0x69] <- adc_imm_cmos
        table.[0x6D] <- adc_abs_cmos
        table.[0x71] <- adc_izy_cmos
        table.[0x75] <- adc_zpx_cmos
        table.[0x79] <- adc_absy_cmos
        table.[0x7D] <- adc_absx_cmos
        table.[0xE1] <- sbc_izx_cmos
        table.[0xE5] <- sbc_zp_cmos
        table.[0xE9] <- sbc_imm_cmos
        table.[0xED] <- sbc_abs_cmos
        table.[0xF1] <- sbc_izy_cmos
        table.[0xF5] <- sbc_zpx_cmos
        table.[0xF9] <- sbc_absy_cmos
        table.[0xFD] <- sbc_absx_cmos

        // 65C02-only addressing modes: (zp) indirect
        table.[0xB2] <- lda_izp
        table.[0x92] <- sta_izp
        table.[0x72] <- adc_izp_cmos
        table.[0xF2] <- sbc_izp_cmos
        table.[0x32] <- and_izp
        table.[0x12] <- ora_izp
        table.[0x52] <- eor_izp
        table.[0xD2] <- cmp_izp

        // 65C02 STA modes use dummy read at high byte address (not wrong effective address)
        table.[0x91] <- sta_izy_65c02
        table.[0x99] <- sta_absy_65c02
        table.[0x9D] <- sta_absx_65c02

        // 65C02-only BIT modes
        table.[0x89] <- bit_imm
        table.[0x34] <- bit_zpx
        table.[0x3C] <- bit_absx

        // 65C02-only INC/DEC accumulator
        table.[0x1A] <- inc_a
        table.[0x3A] <- dec_a

        // 65C02-only stack operations
        table.[0xDA] <- phx_impl
        table.[0xFA] <- plx_impl
        table.[0x5A] <- phy_impl
        table.[0x7A] <- ply_impl

        // 65C02-only STZ
        table.[0x64] <- stz_zp
        table.[0x74] <- stz_zpx
        table.[0x9C] <- stz_abs
        table.[0x9E] <- stz_absx

        // 65C02-only TRB/TSB
        table.[0x14] <- trb_zp
        table.[0x1C] <- trb_abs
        table.[0x04] <- tsb_zp
        table.[0x0C] <- tsb_abs

        // 65C02-only BRA and JMP (abs,X)
        table.[0x80] <- bra
        table.[0x7C] <- jmp_absx_ind

        // 65C02-only STP and WAI
        table.[0xDB] <- stp
        table.[0xCB] <- wai

        // 65C02 RMW instructions use dummy READ (not write) before final write
        table.[0x06] <- asl_zp_65c02
        table.[0x16] <- asl_zpx_65c02
        table.[0x0E] <- asl_abs_65c02
        table.[0x1E] <- asl_absx_65c02
        table.[0x46] <- lsr_zp_65c02
        table.[0x56] <- lsr_zpx_65c02
        table.[0x4E] <- lsr_abs_65c02
        table.[0x5E] <- lsr_absx_65c02
        table.[0x26] <- rol_zp_65c02
        table.[0x36] <- rol_zpx_65c02
        table.[0x2E] <- rol_abs_65c02
        table.[0x3E] <- rol_absx_65c02
        table.[0x66] <- ror_zp_65c02
        table.[0x76] <- ror_zpx_65c02
        table.[0x6E] <- ror_abs_65c02
        table.[0x7E] <- ror_absx_65c02
        table.[0xE6] <- inc_zp_65c02
        table.[0xF6] <- inc_zpx_65c02
        table.[0xEE] <- inc_abs_65c02
        table.[0xFE] <- inc_absx_65c02
        table.[0xC6] <- dec_zp_65c02
        table.[0xD6] <- dec_zpx_65c02
        table.[0xCE] <- dec_abs_65c02
        table.[0xDE] <- dec_absx_65c02

        // 65C02 indexed instructions use different page crossing penalty address
        // (zp),Y instructions
        table.[0xB1] <- lda_izy_65c02
        table.[0x11] <- ora_izy_65c02
        table.[0x31] <- and_izy_65c02
        table.[0x51] <- eor_izy_65c02
        table.[0x71] <- adc_izy_65c02
        table.[0xD1] <- cmp_izy_65c02
        table.[0xF1] <- sbc_izy_65c02

        // abs,Y instructions
        table.[0xB9] <- lda_absy_65c02
        table.[0x19] <- ora_absy_65c02
        table.[0x39] <- and_absy_65c02
        table.[0x59] <- eor_absy_65c02
        table.[0x79] <- adc_absy_65c02
        table.[0xD9] <- cmp_absy_65c02
        table.[0xF9] <- sbc_absy_65c02
        table.[0xBE] <- ldx_absy_65c02

        // abs,X instructions
        table.[0xBD] <- lda_absx_65c02
        table.[0x1D] <- ora_absx_65c02
        table.[0x3D] <- and_absx_65c02
        table.[0x5D] <- eor_absx_65c02
        table.[0x7D] <- adc_absx_65c02
        table.[0xDD] <- cmp_absx_65c02
        table.[0xFD] <- sbc_absx_65c02
        table.[0xBC] <- ldy_absx_65c02
        table.[0x3C] <- bit_absx_65c02

        // 65C02 undefined opcodes treated as 1-byte 1-cycle NOPs
        // $x3 pattern
        table.[0x03] <- nop_1cycle
        table.[0x13] <- nop_1cycle
        table.[0x23] <- nop_1cycle
        table.[0x33] <- nop_1cycle
        table.[0x43] <- nop_1cycle
        table.[0x53] <- nop_1cycle
        table.[0x63] <- nop_1cycle
        table.[0x73] <- nop_1cycle
        table.[0x83] <- nop_1cycle
        table.[0x93] <- nop_1cycle
        table.[0xA3] <- nop_1cycle
        table.[0xB3] <- nop_1cycle
        table.[0xC3] <- nop_1cycle
        table.[0xD3] <- nop_1cycle
        table.[0xE3] <- nop_1cycle
        table.[0xF3] <- nop_1cycle

        // $x7 pattern - RMB/SMB instructions (WDC 65C02 includes these)
        table.[0x07] <- rmb0
        table.[0x17] <- rmb1
        table.[0x27] <- rmb2
        table.[0x37] <- rmb3
        table.[0x47] <- rmb4
        table.[0x57] <- rmb5
        table.[0x67] <- rmb6
        table.[0x77] <- rmb7
        table.[0x87] <- smb0
        table.[0x97] <- smb1
        table.[0xA7] <- smb2
        table.[0xB7] <- smb3
        table.[0xC7] <- smb4
        table.[0xD7] <- smb5
        table.[0xE7] <- smb6
        table.[0xF7] <- smb7

        // $xB pattern - 1-byte 1-cycle NOPs
        table.[0x0B] <- nop_1cycle
        table.[0x1B] <- nop_1cycle
        table.[0x2B] <- nop_1cycle
        table.[0x3B] <- nop_1cycle
        table.[0x4B] <- nop_1cycle
        table.[0x5B] <- nop_1cycle
        table.[0x6B] <- nop_1cycle
        table.[0x7B] <- nop_1cycle
        table.[0x8B] <- nop_1cycle
        table.[0x9B] <- nop_1cycle
        table.[0xAB] <- nop_1cycle
        table.[0xBB] <- nop_1cycle
        table.[0xEB] <- nop_1cycle
        table.[0xFB] <- nop_1cycle

        // $xF pattern - BBR/BBS instructions (WDC 65C02 includes these)
        table.[0x0F] <- bbr0
        table.[0x1F] <- bbr1
        table.[0x2F] <- bbr2
        table.[0x3F] <- bbr3
        table.[0x4F] <- bbr4
        table.[0x5F] <- bbr5
        table.[0x6F] <- bbr6
        table.[0x7F] <- bbr7
        table.[0x8F] <- bbs0
        table.[0x9F] <- bbs1
        table.[0xAF] <- bbs2
        table.[0xBF] <- bbs3
        table.[0xCF] <- bbs4
        table.[0xDF] <- bbs5
        table.[0xEF] <- bbs6
        table.[0xFF] <- bbs7

        // 2-byte NOPs (immediate-style: $x2 pattern)
        table.[0x02] <- nop_imm
        table.[0x22] <- nop_imm
        table.[0x42] <- nop_imm
        table.[0x62] <- nop_imm
        table.[0x82] <- nop_imm
        table.[0xC2] <- nop_imm
        table.[0xE2] <- nop_imm

        // 2-byte NOPs (zp-style: $x4 pattern not already used)
        table.[0x44] <- nop_zp

        // 2-byte NOPs (zp,x-style: $x4 pattern)
        table.[0x54] <- nop_zpx
        table.[0xD4] <- nop_zpx
        table.[0xF4] <- nop_zpx

        // 3-byte 8-cycle NOPs (abs-style) - 65C02 specific
        table.[0x5C] <- nop_abs_65c02
        table.[0xDC] <- nop_abs_65c02
        table.[0xFC] <- nop_abs_65c02

        table

    /// Rockwell 65C02 - adds RMB/SMB/BBR/BBS instructions
    let pipelines65C02rockwell : MicroOp[][] =
        let table = Array.copy pipelines65C02

        // RMB0-RMB7
        table.[0x07] <- rmb0
        table.[0x17] <- rmb1
        table.[0x27] <- rmb2
        table.[0x37] <- rmb3
        table.[0x47] <- rmb4
        table.[0x57] <- rmb5
        table.[0x67] <- rmb6
        table.[0x77] <- rmb7

        // SMB0-SMB7
        table.[0x87] <- smb0
        table.[0x97] <- smb1
        table.[0xA7] <- smb2
        table.[0xB7] <- smb3
        table.[0xC7] <- smb4
        table.[0xD7] <- smb5
        table.[0xE7] <- smb6
        table.[0xF7] <- smb7

        // BBR0-BBR7
        table.[0x0F] <- bbr0
        table.[0x1F] <- bbr1
        table.[0x2F] <- bbr2
        table.[0x3F] <- bbr3
        table.[0x4F] <- bbr4
        table.[0x5F] <- bbr5
        table.[0x6F] <- bbr6
        table.[0x7F] <- bbr7

        // BBS0-BBS7
        table.[0x8F] <- bbs0
        table.[0x9F] <- bbs1
        table.[0xAF] <- bbs2
        table.[0xBF] <- bbs3
        table.[0xCF] <- bbs4
        table.[0xDF] <- bbs5
        table.[0xEF] <- bbs6
        table.[0xFF] <- bbs7

        // Rockwell does NOT have WAI ($CB) and STP ($DB) - they are NOPs
        // $CB is a 1-byte 2-cycle NOP (standard implied timing)
        table.[0xCB] <- nop
        // $DB is a 2-byte 4-cycle NOP (ZP,X style timing)
        table.[0xDB] <- nop_zpx

        // Rockwell uses different decimal penalty address for ADC immediate
        table.[0x69] <- adc_imm_rockwell

        table

    let getPipelines (variant: CpuVariant) : MicroOp[][] =
        match variant with
        | CpuVariant.NMOS6502 -> pipelines6502
        | CpuVariant.NMOS6502_NO_ILLEGAL -> pipelines6502NoIllegal
        | CpuVariant.WDC65C02 -> pipelines65C02
        | CpuVariant.ROCKWELL65C02 -> pipelines65C02rockwell
