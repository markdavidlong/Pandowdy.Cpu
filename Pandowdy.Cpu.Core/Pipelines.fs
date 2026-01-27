namespace Pandowdy.Cpu

open Pandowdy.Cpu.MicroOps

/// Opcode pipeline definitions for 65C02
module Pipelines =

    let private IrqBrkVector = 0xFFFEus

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

    // LDA abs,X - Absolute Indexed X (0xBD) - 4 cycles (+1 if page cross)
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

    // LDA abs,Y - Absolute Indexed Y (0xB9) - 4 cycles (+1 if page cross)
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

    // LDA (zp),Y - Indirect Indexed (0xB1) - 5 cycles (+1 if page cross)
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

    let adc_imm : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            fetchImmediate.Invoke(prev, next, bus)
            adc.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ========================================
    // Phase 2: ADC (remaining addressing modes)
    // ========================================

    let adc_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            readZeroPage.Invoke(prev, next, bus)
            adc.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let adc_zpx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        microOp (fun prev next bus ->
            readZeroPage.Invoke(prev, next, bus)
            adc.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let adc_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            adc.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let adc_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            fetchAddressHigh.Invoke(prev, next, bus)
            addXCheckPage.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            adc.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let adc_absy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            fetchAddressHigh.Invoke(prev, next, bus)
            addYCheckPage.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            adc.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let adc_izx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readPointerLowZP
        readPointerHighZP
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            adc.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let adc_izy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        readPointerHighZPAddY
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            adc.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let adc_izp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        readPointerHighZP
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            adc.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ========================================
    // Phase 2: SBC (all addressing modes)
    // ========================================

    let sbc_imm : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            fetchImmediate.Invoke(prev, next, bus)
            sbc.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let sbc_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            readZeroPage.Invoke(prev, next, bus)
            sbc.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let sbc_zpx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        microOp (fun prev next bus ->
            readZeroPage.Invoke(prev, next, bus)
            sbc.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let sbc_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            sbc.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let sbc_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            fetchAddressHigh.Invoke(prev, next, bus)
            addXCheckPage.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            sbc.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let sbc_absy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            fetchAddressHigh.Invoke(prev, next, bus)
            addYCheckPage.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            sbc.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let sbc_izx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readPointerLowZP
        readPointerHighZP
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            sbc.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let sbc_izy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        readPointerHighZPAddY
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            sbc.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let sbc_izp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        readPointerHighZP
        microOp (fun prev next bus ->
            readFromTempAddress.Invoke(prev, next, bus)
            sbc.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
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

    let ora_izx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
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
            clc.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // SEC - Set Carry (0x38) - 2 cycles
    let sec_impl : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            sec.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // CLD - Clear Decimal (0xD8) - 2 cycles
    let cld_impl : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            cld.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // SED - Set Decimal (0xF8) - 2 cycles
    let sed_impl : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            sed.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // CLI - Clear Interrupt Disable (0x58) - 2 cycles
    let cli_impl : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            cli.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // SEI - Set Interrupt Disable (0x78) - 2 cycles
    let sei_impl : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            sei.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // CLV - Clear Overflow (0xB8) - 2 cycles
    let clv_impl : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            clv.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ========================================
    // Phase 6: Stack & Subroutines
    // ========================================

    // PHA - Push Accumulator (0x48) - 3 cycles
    let pha_impl : MicroOp[] = [|
        fetchOpcode
        noOp  // Dummy cycle
        microOp (fun prev next bus ->
            pushA.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // PLA - Pull Accumulator (0x68) - 4 cycles
    let pla_impl : MicroOp[] = [|
        fetchOpcode
        noOp  // Dummy cycle
        dummyStackRead  // Increment SP
        microOp (fun prev next bus ->
            pullA.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // PHP - Push Processor Status (0x08) - 3 cycles
    let php_impl : MicroOp[] = [|
        fetchOpcode
        noOp  // Dummy cycle
        microOp (fun prev next bus ->
            pushP.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // PLP - Pull Processor Status (0x28) - 4 cycles
    let plp_impl : MicroOp[] = [|
        fetchOpcode
        noOp  // Dummy cycle
        dummyStackRead  // Increment SP
        microOp (fun prev next bus ->
            pullP.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // PHX - Push X Register (0xDA) - 3 cycles (65C02)
    let phx_impl : MicroOp[] = [|
        fetchOpcode
        noOp  // Dummy cycle
        microOp (fun prev next bus ->
            pushX.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // PLX - Pull X Register (0xFA) - 4 cycles (65C02)
    let plx_impl : MicroOp[] = [|
        fetchOpcode
        noOp  // Dummy cycle
        dummyStackRead  // Increment SP
        microOp (fun prev next bus ->
            pullX.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // PHY - Push Y Register (0x5A) - 3 cycles (65C02)
    let phy_impl : MicroOp[] = [|
        fetchOpcode
        noOp  // Dummy cycle
        microOp (fun prev next bus ->
            pushY.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // PLY - Pull Y Register (0x7A) - 4 cycles (65C02)
    let ply_impl : MicroOp[] = [|
        fetchOpcode
        noOp  // Dummy cycle
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
        noOp  // Dummy read
        dummyStackRead  // Increment SP
        pullPCL
        pullPCH
        microOp (fun prev next bus ->
            incrementPC.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // RTI - Return from Interrupt (0x40) - 6 cycles
    let rti : MicroOp[] = [|
        fetchOpcode
        noOp  // Dummy read
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

    // STA abs,X (0x9D) - 5 cycles
    let sta_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addX  // No page penalty for stores
        microOp (fun prev next bus ->
            storeA.Invoke(prev, next, bus)
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // STA abs,Y (0x99) - 5 cycles
    let sta_absy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addY  // No page penalty for stores
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

    // STA (zp),Y (0x91) - 6 cycles
    let sta_izy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        readPointerHighZP
        addY  // Separate cycle for adding Y (no page penalty for stores)
        microOp (fun prev next bus ->
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

    // JMP (ind) (0x6C) - 5 cycles (65C02 fixes page boundary bug)
    let jmp_ind : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        readPointerLowAbs
        microOp (fun prev next bus ->
            readPointerHighAbs.Invoke(prev, next, bus)
            jumpToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // JMP (abs,X) (0x7C) - 6 cycles (65C02 only)
    let jmp_absx_ind : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addX
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

    let nop : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            markComplete.Invoke(prev, next, bus))
    |]

    // Multi-cycle NOPs for undefined opcodes (65C02)
    let nop_3cycle : MicroOp[] = [|
        fetchOpcode
        noOp
        microOp (fun prev next bus ->
            markComplete.Invoke(prev, next, bus))
    |]

    let nop_4cycle : MicroOp[] = [|
        fetchOpcode
        noOp
        noOp
        microOp (fun prev next bus ->
            markComplete.Invoke(prev, next, bus))
    |]

    // ========================================
    // TRB/TSB - Test and Reset/Set Bits (65C02)
    // ========================================

    // TRB zp (0x14) - 5 cycles
    let trb_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        trbOp
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
        trbOp
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // TSB zp (0x04) - 5 cycles
    let tsb_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        tsbOp
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
        tsbOp
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
            inx.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // INY (0xC8) - 2 cycles
    let iny_impl : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            iny.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // DEX (0xCA) - 2 cycles
    let dex_impl : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            dex.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // DEY (0x88) - 2 cycles
    let dey_impl : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            dey.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // INC A (0x1A) - 2 cycles (65C02)
    let inc_a : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            incA.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // DEC A (0x3A) - 2 cycles (65C02)
    let dec_a : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            decA.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // TAX (0xAA) - 2 cycles
    let tax_impl : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            tax.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // TXA (0x8A) - 2 cycles
    let txa_impl : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            txa.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // TAY (0xA8) - 2 cycles
    let tay_impl : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            tay.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // TYA (0x98) - 2 cycles
    let tya_impl : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            tya.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // TSX (0xBA) - 2 cycles
    let tsx_impl : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            tsx.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // TXS (0x9A) - 2 cycles
    let txs_impl : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            txs.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // INC zp (0xE6) - 5 cycles
    let inc_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        incMem
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
        incMem
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
        incMem
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // INC abs,X (0xFE) - 7 cycles
    let inc_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addX  // No page penalty for RMW
        readFromTempAddress
        incMem
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // DEC zp (0xC6) - 5 cycles
    let dec_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        decMem
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
        decMem
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
        decMem
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // DEC abs,X (0xDE) - 7 cycles
    let dec_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addX
        readFromTempAddress
        decMem
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
            aslA.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ASL zp (0x06) - 5 cycles
    let asl_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        aslMem
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
        aslMem
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
        aslMem
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ASL abs,X (0x1E) - 7 cycles
    let asl_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addX
        readFromTempAddress
        aslMem
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // LSR A (0x4A) - 2 cycles
    let lsr_a : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            lsrA.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // LSR zp (0x46) - 5 cycles
    let lsr_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        lsrMem
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
        lsrMem
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
        lsrMem
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // LSR abs,X (0x5E) - 7 cycles
    let lsr_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addX
        readFromTempAddress
        lsrMem
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ROL A (0x2A) - 2 cycles
    let rol_a : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            rolA.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ROL zp (0x26) - 5 cycles
    let rol_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        rolMem
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
        rolMem
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
        rolMem
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ROL abs,X (0x3E) - 7 cycles
    let rol_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addX
        readFromTempAddress
        rolMem
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ROR A (0x6A) - 2 cycles
    let ror_a : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            rorA.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ROR zp (0x66) - 5 cycles
    let ror_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        rorMem
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
        rorMem
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
        rorMem
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ROR abs,X (0x7E) - 7 cycles
    let ror_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addX
        readFromTempAddress
        rorMem
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

    // LDX abs,Y (0xBE) - 4 cycles (+1 if page cross)
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

    // LDY abs,X (0xBC) - 4 cycles (+1 if page cross)
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

    // STZ abs,X (0x9E) - 5 cycles (65C02)
    let stz_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addX
        microOp (fun prev next bus ->
            storeZ.Invoke(prev, next, bus)
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ========================================
    // Rockwell 65C02 Extensions
    // ========================================

    // RMB0-RMB7 - Reset Memory Bit (0x07, 0x17, 0x27, 0x37, 0x47, 0x57, 0x67, 0x77) - 5 cycles
    let rmb (bit: int) : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        rmbOp bit
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
        smbOp bit
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

    // BBR0-BBR7 - Branch on Bit Reset (0x0F, 0x1F, 0x2F, 0x3F, 0x4F, 0x5F, 0x6F, 0x7F) - 5 cycles (+1/+2)
    let bbr (bit: int) : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        noOp  // Internal cycle
        microOp (fun prev next bus ->
            (bbrOp bit).Invoke(prev, next, bus))
    |]

    let bbr0 = bbr 0
    let bbr1 = bbr 1
    let bbr2 = bbr 2
    let bbr3 = bbr 3
    let bbr4 = bbr 4
    let bbr5 = bbr 5
    let bbr6 = bbr 6
    let bbr7 = bbr 7

    // BBS0-BBS7 - Branch on Bit Set (0x8F, 0x9F, 0xAF, 0xBF, 0xCF, 0xDF, 0xEF, 0xFF) - 5 cycles (+1/+2)
    let bbs (bit: int) : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        noOp  // Internal cycle
        microOp (fun prev next bus ->
            (bbsOp bit).Invoke(prev, next, bus))
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
    // Illegal/Undocumented 6502 Opcodes
    // ========================================

    // JAM/KIL - Freeze CPU (2 cycles, then loops forever)
    let jam : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
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

    // DCP - Decrement then Compare (RMW timing)
    let dcp_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        dcpOp
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let dcp_zpx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readZeroPage
        dcpOp
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let dcp_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        readFromTempAddress
        dcpOp
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let dcp_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addX
        readFromTempAddress
        dcpOp
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let dcp_absy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addY
        readFromTempAddress
        dcpOp
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
        dcpOp
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let dcp_izy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        microOp (fun prev next bus ->
            readPointerHighZP.Invoke(prev, next, bus)
            addY.Invoke(prev, next, bus))
        readFromTempAddress
        dcpOp
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // ISC/ISB - Increment then Subtract (RMW timing)
    let isc_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        iscOp
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let isc_zpx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readZeroPage
        iscOp
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let isc_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        readFromTempAddress
        iscOp
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let isc_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addX
        readFromTempAddress
        iscOp
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let isc_absy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addY
        readFromTempAddress
        iscOp
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
        iscOp
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let isc_izy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        microOp (fun prev next bus ->
            readPointerHighZP.Invoke(prev, next, bus)
            addY.Invoke(prev, next, bus))
        readFromTempAddress
        iscOp
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // SLO - Shift Left then OR (RMW timing)
    let slo_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        sloOp
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let slo_zpx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readZeroPage
        sloOp
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let slo_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        readFromTempAddress
        sloOp
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let slo_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addX
        readFromTempAddress
        sloOp
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let slo_absy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addY
        readFromTempAddress
        sloOp
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
        sloOp
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let slo_izy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        microOp (fun prev next bus ->
            readPointerHighZP.Invoke(prev, next, bus)
            addY.Invoke(prev, next, bus))
        readFromTempAddress
        sloOp
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // RLA - Rotate Left then AND (RMW timing)
    let rla_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        rlaOp
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let rla_zpx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readZeroPage
        rlaOp
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let rla_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        readFromTempAddress
        rlaOp
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let rla_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addX
        readFromTempAddress
        rlaOp
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let rla_absy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addY
        readFromTempAddress
        rlaOp
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
        rlaOp
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let rla_izy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        microOp (fun prev next bus ->
            readPointerHighZP.Invoke(prev, next, bus)
            addY.Invoke(prev, next, bus))
        readFromTempAddress
        rlaOp
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // SRE - Shift Right then EOR (RMW timing)
    let sre_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        sreOp
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let sre_zpx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readZeroPage
        sreOp
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let sre_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        readFromTempAddress
        sreOp
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let sre_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addX
        readFromTempAddress
        sreOp
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let sre_absy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addY
        readFromTempAddress
        sreOp
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
        sreOp
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let sre_izy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        microOp (fun prev next bus ->
            readPointerHighZP.Invoke(prev, next, bus)
            addY.Invoke(prev, next, bus))
        readFromTempAddress
        sreOp
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // RRA - Rotate Right then ADC (RMW timing)
    let rra_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readZeroPage
        rraOp
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let rra_zpx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        readZeroPage
        rraOp
        microOp (fun prev next bus ->
            writeZeroPage.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let rra_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        readFromTempAddress
        rraOp
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let rra_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addX
        readFromTempAddress
        rraOp
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let rra_absy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        addY
        readFromTempAddress
        rraOp
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
        rraOp
        microOp (fun prev next bus ->
            writeToTempAddress.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let rra_izy : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        readPointerLowZP
        microOp (fun prev next bus ->
            readPointerHighZP.Invoke(prev, next, bus)
            addY.Invoke(prev, next, bus))
        readFromTempAddress
        rraOp
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

    // Unofficial SBC duplicate
    let sbc_imm_unofficial : MicroOp[] = sbc_imm

    // NOP variants that skip bytes
    let nop_imm : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            fetchImmediate.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let nop_zp : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            markComplete.Invoke(prev, next, bus))
    |]

    let nop_zpx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        addXZeroPage
        microOp (fun prev next bus ->
            markComplete.Invoke(prev, next, bus))
    |]

    let nop_abs : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        fetchAddressHigh
        microOp (fun prev next bus ->
            markComplete.Invoke(prev, next, bus))
    |]

    let nop_absx : MicroOp[] = [|
        fetchOpcode
        fetchAddressLow
        microOp (fun prev next bus ->
            fetchAddressHigh.Invoke(prev, next, bus)
            addXCheckPage.Invoke(prev, next, bus))
        microOp (fun prev next bus ->
            markComplete.Invoke(prev, next, bus))
    |]

    // STP - Stop the processor (0xDB) - 3 cycles (65C02)
    let stp : MicroOp[] = [|
        fetchOpcode
        noOp
        microOp (fun prev next bus ->
            stpOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    // WAI - Wait for interrupt (0xCB) - 3 cycles (65C02)
    let wai : MicroOp[] = [|
        fetchOpcode
        noOp
        microOp (fun prev next bus ->
            waiOp.Invoke(prev, next, bus)
            markComplete.Invoke(prev, next, bus))
    |]

    let unimplemented : MicroOp[] = [|
        fetchOpcode
        microOp (fun prev next bus ->
            markComplete.Invoke(prev, next, bus))
    |]

    //let pipelines65C02 : MicroOp[][] =
    //    let table = Array.create 256 unimplemented
    //    table.[0x00] <- brk
    //    table.[0x85] <- sta_zp
    //    table.[0x95] <- sta_zpx       // STA zp,X
    //    table.[0x8D] <- sta_abs       // STA abs
    //    table.[0x9D] <- sta_absx      // STA abs,X
    //    table.[0x99] <- sta_absy      // STA abs,Y
    //    table.[0x81] <- sta_izx       // STA (zp,X)
    //    table.[0x91] <- sta_izy       // STA (zp),Y
    //    table.[0x92] <- sta_izp       // STA (zp) - 65C02
    //    table.[0x4C] <- jmp_abs
    //    table.[0x6C] <- jmp_ind       // JMP (ind)
    //    table.[0x7C] <- jmp_absx_ind  // JMP (abs,X) - 65C02
    //    table.[0xEA] <- nop

    //    // TRB/TSB (65C02)
    //    table.[0x14] <- trb_zp        // TRB zp
    //    table.[0x1C] <- trb_abs       // TRB abs
    //    table.[0x04] <- tsb_zp        // TSB zp
    //    table.[0x0C] <- tsb_abs       // TSB abs

    //    // LDA - all addressing modes
    //    table.[0xA9] <- lda_imm      // LDA #imm
    //    table.[0xA5] <- lda_zp       // LDA zp
    //    table.[0xB5] <- lda_zpx      // LDA zp,X
    //    table.[0xAD] <- lda_abs      // LDA abs
    //    table.[0xBD] <- lda_absx     // LDA abs,X
    //    table.[0xB9] <- lda_absy     // LDA abs,Y
    //    table.[0xA1] <- lda_izx      // LDA (zp,X)
    //    table.[0xB1] <- lda_izy      // LDA (zp),Y
    //    table.[0xB2] <- lda_izp      // LDA (zp) - 65C02 only

    //    // Phase 2: ADC - all addressing modes
    //    table.[0x69] <- adc_imm      // ADC #imm
    //    table.[0x65] <- adc_zp       // ADC zp
    //    table.[0x75] <- adc_zpx      // ADC zp,X
    //    table.[0x6D] <- adc_abs      // ADC abs
    //    table.[0x7D] <- adc_absx     // ADC abs,X
    //    table.[0x79] <- adc_absy     // ADC abs,Y
    //    table.[0x61] <- adc_izx      // ADC (zp,X)
    //    table.[0x71] <- adc_izy      // ADC (zp),Y
    //    table.[0x72] <- adc_izp      // ADC (zp) - 65C02

    //    // Phase 2: SBC - all addressing modes
    //    table.[0xE9] <- sbc_imm      // SBC #imm
    //    table.[0xE5] <- sbc_zp       // SBC zp
    //    table.[0xF5] <- sbc_zpx      // SBC zp,X
    //    table.[0xED] <- sbc_abs      // SBC abs
    //    table.[0xFD] <- sbc_absx     // SBC abs,X
    //    table.[0xF9] <- sbc_absy     // SBC abs,Y
    //    table.[0xE1] <- sbc_izx      // SBC (zp,X)
    //    table.[0xF1] <- sbc_izy      // SBC (zp),Y
    //    table.[0xF2] <- sbc_izp      // SBC (zp) - 65C02

    //    // Phase 2: AND - all addressing modes
    //    table.[0x29] <- and_imm      // AND #imm
    //    table.[0x25] <- and_zp       // AND zp
    //    table.[0x35] <- and_zpx      // AND zp,X
    //    table.[0x2D] <- and_abs      // AND abs
    //    table.[0x3D] <- and_absx     // AND abs,X
    //    table.[0x39] <- and_absy     // AND abs,Y
    //    table.[0x21] <- and_izx      // AND (zp,X)
    //    table.[0x31] <- and_izy      // AND (zp),Y
    //    table.[0x32] <- and_izp      // AND (zp) - 65C02

    //    // Phase 2: ORA - all addressing modes
    //    table.[0x09] <- ora_imm      // ORA #imm
    //    table.[0x05] <- ora_zp       // ORA zp
    //    table.[0x15] <- ora_zpx      // ORA zp,X
    //    table.[0x0D] <- ora_abs      // ORA abs
    //    table.[0x1D] <- ora_absx     // ORA abs,X
    //    table.[0x19] <- ora_absy     // ORA abs,Y
    //    table.[0x01] <- ora_izx      // ORA (zp,X)
    //    table.[0x11] <- ora_izy      // ORA (zp),Y
    //    table.[0x12] <- ora_izp      // ORA (zp) - 65C02

    //    // Phase 2: EOR - all addressing modes
    //    table.[0x49] <- eor_imm      // EOR #imm
    //    table.[0x45] <- eor_zp       // EOR zp
    //    table.[0x55] <- eor_zpx      // EOR zp,X
    //    table.[0x4D] <- eor_abs      // EOR abs
    //    table.[0x5D] <- eor_absx     // EOR abs,X
    //    table.[0x59] <- eor_absy     // EOR abs,Y
    //    table.[0x41] <- eor_izx      // EOR (zp,X)
    //    table.[0x51] <- eor_izy      // EOR (zp),Y
    //    table.[0x52] <- eor_izp      // EOR (zp) - 65C02

    //    // Phase 2: CMP - all addressing modes
    //    table.[0xC9] <- cmp_imm      // CMP #imm
    //    table.[0xC5] <- cmp_zp       // CMP zp
    //    table.[0xD5] <- cmp_zpx      // CMP zp,X
    //    table.[0xCD] <- cmp_abs      // CMP abs
    //    table.[0xDD] <- cmp_absx     // CMP abs,X
    //    table.[0xD9] <- cmp_absy     // CMP abs,Y
    //    table.[0xC1] <- cmp_izx      // CMP (zp,X)
    //    table.[0xD1] <- cmp_izy      // CMP (zp),Y
    //    table.[0xD2] <- cmp_izp      // CMP (zp) - 65C02

    //    // Phase 2: CPX
    //    table.[0xE0] <- cpx_imm      // CPX #imm
    //    table.[0xE4] <- cpx_zp       // CPX zp
    //    table.[0xEC] <- cpx_abs      // CPX abs

    //    // Phase 2: CPY
    //    table.[0xC0] <- cpy_imm      // CPY #imm
    //    table.[0xC4] <- cpy_zp       // CPY zp
    //    table.[0xCC] <- cpy_abs      // CPY abs

    //    // Phase 2: BIT
    //    table.[0x89] <- bit_imm      // BIT #imm - 65C02
    //    table.[0x24] <- bit_zp       // BIT zp
    //    table.[0x34] <- bit_zpx      // BIT zp,X - 65C02
    //    table.[0x2C] <- bit_abs      // BIT abs
    //    table.[0x3C] <- bit_absx     // BIT abs,X - 65C02

    //    // Phase 3: Increment/Decrement
    //    table.[0xE8] <- inx_impl     // INX
    //    table.[0xC8] <- iny_impl     // INY
    //    table.[0xCA] <- dex_impl     // DEX
    //    table.[0x88] <- dey_impl     // DEY
    //    table.[0x1A] <- inc_a        // INC A (65C02)
    //    table.[0x3A] <- dec_a        // DEC A (65C02)

    //    // Phase 3: Transfers
    //    table.[0xAA] <- tax_impl     // TAX
    //    table.[0x8A] <- txa_impl     // TXA
    //    table.[0xA8] <- tay_impl     // TAY
    //    table.[0x98] <- tya_impl     // TYA
    //    table.[0xBA] <- tsx_impl     // TSX
    //    table.[0x9A] <- txs_impl     // TXS

    //    // Phase 3: INC memory
    //    table.[0xE6] <- inc_zp       // INC zp
    //    table.[0xF6] <- inc_zpx      // INC zp,X
    //    table.[0xEE] <- inc_abs      // INC abs
    //    table.[0xFE] <- inc_absx     // INC abs,X

    //    // Phase 3: DEC memory
    //    table.[0xC6] <- dec_zp       // DEC zp
    //    table.[0xD6] <- dec_zpx      // DEC zp,X
    //    table.[0xCE] <- dec_abs      // DEC abs
    //    table.[0xDE] <- dec_absx     // DEC abs,X

    //    // Phase 4: ASL
    //    table.[0x0A] <- asl_a        // ASL A
    //    table.[0x06] <- asl_zp       // ASL zp
    //    table.[0x16] <- asl_zpx      // ASL zp,X
    //    table.[0x0E] <- asl_abs      // ASL abs
    //    table.[0x1E] <- asl_absx     // ASL abs,X

    //    // Phase 4: LSR
    //    table.[0x4A] <- lsr_a        // LSR A
    //    table.[0x46] <- lsr_zp       // LSR zp
    //    table.[0x56] <- lsr_zpx      // LSR zp,X
    //    table.[0x4E] <- lsr_abs      // LSR abs
    //    table.[0x5E] <- lsr_absx     // LSR abs,X

    //    // Phase 4: ROL
    //    table.[0x2A] <- rol_a        // ROL A
    //    table.[0x26] <- rol_zp       // ROL zp
    //    table.[0x36] <- rol_zpx      // ROL zp,X
    //    table.[0x2E] <- rol_abs      // ROL abs
    //    table.[0x3E] <- rol_absx     // ROL abs,X

    //    // Phase 4: ROR
    //    table.[0x6A] <- ror_a        // ROR A
    //    table.[0x66] <- ror_zp       // ROR zp
    //    table.[0x76] <- ror_zpx      // ROR zp,X
    //    table.[0x6E] <- ror_abs      // ROR abs
    //    table.[0x7E] <- ror_absx     // ROR abs,X

    //    // Phase 7: LDX
    //    table.[0xA2] <- ldx_imm      // LDX #imm
    //    table.[0xA6] <- ldx_zp       // LDX zp
    //    table.[0xB6] <- ldx_zpy      // LDX zp,Y
    //    table.[0xAE] <- ldx_abs      // LDX abs
    //    table.[0xBE] <- ldx_absy     // LDX abs,Y

    //    // Phase 7: LDY
    //    table.[0xA0] <- ldy_imm      // LDY #imm
    //    table.[0xA4] <- ldy_zp       // LDY zp
    //    table.[0xB4] <- ldy_zpx      // LDY zp,X
    //    table.[0xAC] <- ldy_abs      // LDY abs
    //    table.[0xBC] <- ldy_absx     // LDY abs,X

    //    // Phase 7: STX
    //    table.[0x86] <- stx_zp       // STX zp
    //    table.[0x96] <- stx_zpy      // STX zp,Y
    //    table.[0x8E] <- stx_abs      // STX abs

    //    // Phase 7: STY
    //    table.[0x84] <- sty_zp       // STY zp
    //    table.[0x94] <- sty_zpx      // STY zp,X
    //    table.[0x8C] <- sty_abs      // STY abs

    //    // Phase 7: STZ (65C02 only)
    //    table.[0x64] <- stz_zp       // STZ zp
    //    table.[0x74] <- stz_zpx      // STZ zp,X
    //    table.[0x9C] <- stz_abs      // STZ abs
    //    table.[0x9E] <- stz_absx     // STZ abs,X

    //    // Phase 5: Branch Instructions
    //    table.[0xF0] <- beq          // BEQ - Branch if Equal
    //    table.[0xD0] <- bne          // BNE - Branch if Not Equal
    //    table.[0xB0] <- bcs          // BCS - Branch if Carry Set
    //    table.[0x90] <- bcc          // BCC - Branch if Carry Clear
    //    table.[0x30] <- bmi          // BMI - Branch if Minus
    //    table.[0x10] <- bpl          // BPL - Branch if Plus
    //    table.[0x70] <- bvs          // BVS - Branch if Overflow Set
    //    table.[0x50] <- bvc          // BVC - Branch if Overflow Clear
    //    table.[0x80] <- bra          // BRA - Branch Always (65C02)

    //    // Phase 8: Flag Operations
    //    table.[0x18] <- clc_impl     // CLC - Clear Carry
    //    table.[0x38] <- sec_impl     // SEC - Set Carry
    //    table.[0xD8] <- cld_impl     // CLD - Clear Decimal
    //    table.[0xF8] <- sed_impl     // SED - Set Decimal
    //    table.[0x58] <- cli_impl     // CLI - Clear Interrupt Disable
    //    table.[0x78] <- sei_impl     // SEI - Set Interrupt Disable
    //    table.[0xB8] <- clv_impl     // CLV - Clear Overflow

    //    // Phase 6: Stack Operations
    //    table.[0x48] <- pha_impl     // PHA - Push A
    //    table.[0x68] <- pla_impl     // PLA - Pull A
    //    table.[0x08] <- php_impl     // PHP - Push P
    //    table.[0x28] <- plp_impl     // PLP - Pull P
    //    table.[0xDA] <- phx_impl     // PHX - Push X (65C02)
    //    table.[0xFA] <- plx_impl     // PLX - Pull X (65C02)
    //    table.[0x5A] <- phy_impl     // PHY - Push Y (65C02)
    //    table.[0x7A] <- ply_impl     // PLY - Pull Y (65C02)

    //        // Phase 6: Subroutine Operations
    //    table.[0x20] <- jsr          // JSR - Jump to Subroutine
    //    table.[0x60] <- rts          // RTS - Return from Subroutine
    //    table.[0x40] <- rti          // RTI - Return from Interrupt

    //    table

    /// Base pipeline table with all instructions common to 6502 and 65C02
    /// Uses NMOS JMP indirect (with page boundary bug) as default
    let pipelinesBase6502 : MicroOp[][] =
        let table = Array.create 256 unimplemented

        // Core instructions shared by all variants

        table.[0x00] <- brk
        table.[0x01] <- ora_izx
        table.[0x05] <- ora_zp
        table.[0x06] <- asl_zp
        table.[0x08] <- php_impl
        table.[0x09] <- ora_imm
        table.[0x0A] <- asl_a
        table.[0x0D] <- ora_abs
        table.[0x0E] <- asl_abs

        table.[0x10] <- bpl
        table.[0x11] <- ora_izy
        table.[0x15] <- ora_zpx
        table.[0x16] <- asl_zpx
        table.[0x18] <- clc_impl
        table.[0x19] <- ora_absy
        table.[0x1D] <- ora_absx
        table.[0x1E] <- asl_absx

        table.[0x20] <- jsr
        table.[0x21] <- and_izx
        table.[0x24] <- bit_zp
        table.[0x25] <- and_zp
        table.[0x26] <- rol_zp
        table.[0x28] <- plp_impl
        table.[0x29] <- and_imm
        table.[0x2A] <- rol_a
        table.[0x2C] <- bit_abs
        table.[0x2D] <- and_abs
        table.[0x2E] <- rol_abs

        table.[0x30] <- bmi
        table.[0x31] <- and_izy
        table.[0x35] <- and_zpx
        table.[0x36] <- rol_zpx
        table.[0x38] <- sec_impl
        table.[0x39] <- and_absy
        table.[0x3D] <- and_absx
        table.[0x3E] <- rol_absx

        table.[0x40] <- rti
        table.[0x41] <- eor_izx
        table.[0x45] <- eor_zp
        table.[0x46] <- lsr_zp
        table.[0x48] <- pha_impl
        table.[0x49] <- eor_imm
        table.[0x4A] <- lsr_a
        table.[0x4C] <- jmp_abs
        table.[0x4D] <- eor_abs
        table.[0x4E] <- lsr_abs

        table.[0x50] <- bvc
        table.[0x51] <- eor_izy
        table.[0x55] <- eor_zpx
        table.[0x56] <- lsr_zpx
        table.[0x58] <- cli_impl
        table.[0x59] <- eor_absy
        table.[0x5D] <- eor_absx
        table.[0x5E] <- lsr_absx

        table.[0x60] <- rts
        table.[0x61] <- adc_izx
        table.[0x65] <- adc_zp
        table.[0x66] <- ror_zp
        table.[0x68] <- pla_impl
        table.[0x69] <- adc_imm
        table.[0x6A] <- ror_a
        table.[0x6C] <- jmp_ind_nmos
        table.[0x6D] <- adc_abs
        table.[0x6E] <- ror_abs

        table.[0x70] <- bvs
        table.[0x71] <- adc_izy
        table.[0x75] <- adc_zpx
        table.[0x76] <- ror_zpx
        table.[0x78] <- sei_impl
        table.[0x79] <- adc_absy
        table.[0x7D] <- adc_absx
        table.[0x7E] <- ror_absx

        table.[0x81] <- sta_izx
        table.[0x84] <- sty_zp
        table.[0x85] <- sta_zp
        table.[0x86] <- stx_zp
        table.[0x88] <- dey_impl
        table.[0x8A] <- txa_impl
        table.[0x8C] <- sty_abs
        table.[0x8D] <- sta_abs
        table.[0x8E] <- stx_abs

        table.[0x90] <- bcc
        table.[0x91] <- sta_izy
        table.[0x94] <- sty_zpx
        table.[0x95] <- sta_zpx
        table.[0x96] <- stx_zpy
        table.[0x98] <- tya_impl
        table.[0x99] <- sta_absy
        table.[0x9A] <- txs_impl
        table.[0x9D] <- sta_absx

        table.[0xA0] <- ldy_imm
        table.[0xA1] <- lda_izx
        table.[0xA2] <- ldx_imm
        table.[0xA4] <- ldy_zp
        table.[0xA5] <- lda_zp
        table.[0xA6] <- ldx_zp
        table.[0xA8] <- tay_impl
        table.[0xA9] <- lda_imm
        table.[0xAA] <- tax_impl
        table.[0xAC] <- ldy_abs
        table.[0xAD] <- lda_abs
        table.[0xAE] <- ldx_abs

        table.[0xB0] <- bcs
        table.[0xB1] <- lda_izy
        table.[0xB4] <- ldy_zpx
        table.[0xB5] <- lda_zpx
        table.[0xB6] <- ldx_zpy
        table.[0xB8] <- clv_impl
        table.[0xB9] <- lda_absy
        table.[0xBA] <- tsx_impl
        table.[0xBC] <- ldy_absx
        table.[0xBD] <- lda_absx
        table.[0xBE] <- ldx_absy

        table.[0xC0] <- cpy_imm
        table.[0xC1] <- cmp_izx
        table.[0xC4] <- cpy_zp
        table.[0xC5] <- cmp_zp
        table.[0xC6] <- dec_zp
        table.[0xC8] <- iny_impl
        table.[0xC9] <- cmp_imm
        table.[0xCA] <- dex_impl
        table.[0xCC] <- cpy_abs
        table.[0xCD] <- cmp_abs
        table.[0xCE] <- dec_abs
        
        table.[0xD0] <- bne
        table.[0xD1] <- cmp_izy
        table.[0xD5] <- cmp_zpx
        table.[0xD6] <- dec_zpx
        table.[0xD8] <- cld_impl
        table.[0xD9] <- cmp_absy
        table.[0xDD] <- cmp_absx
        table.[0xDE] <- dec_absx
        
        table.[0xE0] <- cpx_imm
        table.[0xE1] <- sbc_izx
        table.[0xE4] <- cpx_zp
        table.[0xE5] <- sbc_zp
        table.[0xE6] <- inc_zp
        table.[0xE8] <- inx_impl
        table.[0xE9] <- sbc_imm
        table.[0xEA] <- nop
        table.[0xEC] <- cpx_abs
        table.[0xED] <- sbc_abs
        table.[0xEE] <- inc_abs

        table.[0xF0] <- beq
        table.[0xF1] <- sbc_izy
        table.[0xF5] <- sbc_zpx
        table.[0xF6] <- inc_zpx
        table.[0xF8] <- sed_impl
        table.[0xF9] <- sbc_absy
        table.[0xFD] <- sbc_absx
        table.[0xFE] <- inc_absx

        table

    /// NMOS 6502 without undocumented opcodes - treats undefined opcodes as NOPs
    let pipelines6502NoUndoc : MicroOp[][] = 
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

    /// NMOS 6502 with undocumented opcodes
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

        table

    /// WDC 65C02 - adds 65C02-only instructions and fixes JMP indirect bug
    let pipelines65C02 : MicroOp[][] =
        let table = Array.copy pipelinesBase6502

        // Fix JMP indirect page boundary bug
        table.[0x6C] <- jmp_ind

        // 65C02-only addressing modes: (zp) indirect
        table.[0xB2] <- lda_izp
        table.[0x92] <- sta_izp
        table.[0x72] <- adc_izp
        table.[0xF2] <- sbc_izp
        table.[0x32] <- and_izp
        table.[0x12] <- ora_izp
        table.[0x52] <- eor_izp
        table.[0xD2] <- cmp_izp

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

        table

    let getPipelines (variant: CpuVariant) : MicroOp[][] =
        match variant with
        | CpuVariant.NMOS6502 -> pipelines6502
        | CpuVariant.NMOS6502_NO_UNDOC -> pipelines6502NoUndoc
        | CpuVariant.CMOS65C02 -> pipelines65C02
        | CpuVariant.ROCKWELL65C02 -> pipelines65C02rockwell
