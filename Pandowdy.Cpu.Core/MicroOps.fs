// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Cpu

open System

type CpuVariant =
    | NMOS6502
    | NMOS6502_NO_ILLEGAL
    | WDC65C02
    | ROCKWELL65C02

type MicroOp = Action<CpuState, CpuState, IPandowdyCpuBus>

module MicroOps =

    let inline microOp (f: CpuState -> CpuState -> IPandowdyCpuBus -> unit) : MicroOp =
        Action<CpuState, CpuState, IPandowdyCpuBus>(fun prev next bus -> f prev next bus)

    /// Get low byte of TempValue for 8-bit operations
    let inline tempByte (state: CpuState) = byte state.TempValue

    /// Store a byte in TempValue (ensures high byte is zero)
    let inline setTempByte (state: CpuState) (value: byte) =
        state.TempValue <- uint16 value

    let setNZ (state: CpuState) (value: byte) =
        state.ZeroFlag <- (value = 0uy)
        state.NegativeFlag <- ((value &&& 0x80uy) <> 0uy)

    let fetchOpcode : MicroOp = microOp (fun prev next bus ->
        setTempByte next (bus.CpuRead(next.PC))
        next.PC <- next.PC + 1us)

    let fetchImmediate : MicroOp = microOp (fun prev next bus ->
        setTempByte next (bus.CpuRead(next.PC))
        next.PC <- next.PC + 1us)

    let fetchAddressLow : MicroOp = microOp (fun prev next bus ->
        let lo = bus.CpuRead(next.PC)
        next.TempAddress <- uint16 lo
        next.PC <- next.PC + 1us)

    let fetchAddressHigh : MicroOp = microOp (fun prev next bus ->
        let hi = bus.CpuRead(next.PC)
        next.TempAddress <- next.TempAddress ||| (uint16 hi <<< 8)
        next.PC <- next.PC + 1us)

    let readFromTempAddress : MicroOp = microOp (fun prev next bus ->
        setTempByte next (bus.CpuRead(next.TempAddress)))

    let writeToTempAddress : MicroOp = microOp (fun prev next bus ->
        bus.Write(next.TempAddress, tempByte next))

    let readZeroPage : MicroOp = microOp (fun prev next bus ->
        setTempByte next (bus.CpuRead(next.TempAddress &&& 0x00FFus)))

    let writeZeroPage : MicroOp = microOp (fun prev next bus ->
        bus.Write(next.TempAddress &&& 0x00FFus, tempByte next))

    /// Add X to TempAddress (no bus access - for page crossing checks)
    let addX : MicroOp = microOp (fun prev next bus ->
        next.TempAddress <- next.TempAddress + uint16 next.X)

    /// Add X to TempAddress with a dummy read at the partially-computed address
    /// Used for RMW abs,X instructions which always take the extra cycle
    /// The 6502 reads from BAH:(BAL+X) before potentially fixing the high byte for page crossing
    let addXWithDummyRead : MicroOp = microOp (fun prev next bus ->
        // Calculate the "wrong" address: base high byte + (base low byte + X)
        let baseAddr = next.TempAddress
        let lowPlusX = (byte baseAddr + next.X)  // Low byte plus X, may wrap
        let wrongAddr = (baseAddr &&& 0xFF00us) ||| uint16 lowPlusX
        bus.CpuRead(wrongAddr) |> ignore
        // Now set the correct full address
        next.TempAddress <- baseAddr + uint16 next.X)

    /// Add Y to TempAddress (no bus access)
    let addY : MicroOp = microOp (fun prev next bus ->
        next.TempAddress <- next.TempAddress + uint16 next.Y)

    /// Add Y to TempAddress with a dummy read at the partially-computed address
    /// Used for store abs,Y instructions which always read before writing
    let addYWithDummyRead : MicroOp = microOp (fun prev next bus ->
        let baseAddr = next.TempAddress
        let lowPlusY = (byte baseAddr + next.Y)
        let wrongAddr = (baseAddr &&& 0xFF00us) ||| uint16 lowPlusY
        bus.CpuRead(wrongAddr) |> ignore
        next.TempAddress <- baseAddr + uint16 next.Y)

    let noOp : MicroOp = microOp (fun prev next bus -> ())

    /// Dummy read at PC - used for implied/accumulator addressing modes
    /// The 6502 always accesses the bus every cycle, so even "internal" operations
    /// perform a read. This is typically a read at PC that is discarded.
    let dummyReadPC : MicroOp = microOp (fun prev next bus ->
        bus.CpuRead(next.PC) |> ignore)

    /// Dummy read at the current TempAddress - used for read-modify-write instructions
    /// before the final write cycle
    let dummyReadTempAddress : MicroOp = microOp (fun prev next bus ->
        bus.CpuRead(next.TempAddress) |> ignore)

    /// Dummy write to TempAddress - used in read-modify-write instructions
    /// The 6502 writes the original value back before writing the modified value
    let dummyWriteTempAddress : MicroOp = microOp (fun prev next bus ->
        bus.Write(next.TempAddress, tempByte next))

    /// Dummy write to zero page TempAddress - used in read-modify-write instructions
    let dummyWriteZeroPage : MicroOp = microOp (fun prev next bus ->
        bus.Write(next.TempAddress &&& 0x00FFus, tempByte next))

    /// Insert a micro-op after the current operation (making it the next operation to execute after index is incremented)
    /// During micro-op execution, PipelineIndex points to current op. After execution, index is incremented.
    /// So we insert at PipelineIndex + 1 to make the noOp execute next.
    let insertAfterCurrentOp (state: CpuState) (op: MicroOp) =
        let insertIdx = state.PipelineIndex + 1
        let before = state.Pipeline.[..insertIdx-1]
        let after = state.Pipeline.[insertIdx..]
        state.Pipeline <- Array.concat [| before; [| op |]; after |]

    /// Add a penalty cycle by inserting a dummy read after current operation
    /// The 6502 always accesses the bus, so penalty cycles do a read at the incomplete address
    let addPenaltyCycle (state: CpuState) =
        insertAfterCurrentOp state dummyReadTempAddress

    let checkPageCrossing : MicroOp = microOp (fun prev next bus ->
        let basePage = prev.TempAddress >>> 8
        let newPage = next.TempAddress >>> 8
        if basePage <> newPage then
            addPenaltyCycle next)

    let loadA : MicroOp = microOp (fun prev next bus ->
        next.A <- tempByte next
        setNZ next next.A)

    let loadX : MicroOp = microOp (fun prev next bus ->
        next.X <- tempByte next
        setNZ next next.X)

    let loadY : MicroOp = microOp (fun prev next bus ->
        next.Y <- tempByte next
        setNZ next next.Y)

    let storeA : MicroOp = microOp (fun prev next bus ->
        setTempByte next next.A)

    let storeX : MicroOp = microOp (fun prev next bus ->
        setTempByte next next.X)

    let storeY : MicroOp = microOp (fun prev next bus ->
        setTempByte next next.Y)

    /// STZ - Store Zero (65C02)
    let storeZ : MicroOp = microOp (fun prev next bus ->
        setTempByte next 0uy)

    let adcBinary : MicroOp = microOp (fun prev next bus ->
        let a = uint16 next.A
        let m = next.TempValue &&& 0xFFus
        let c = if next.CarryFlag then 1us else 0us
        let sum = a + m + c
        next.CarryFlag <- (sum > 255us)
        let result = byte sum
        next.OverflowFlag <- (((a ^^^ uint16 result) &&& (m ^^^ uint16 result) &&& 0x80us) <> 0us)
        next.A <- result
        setNZ next next.A)

    /// ADC decimal mode for NMOS 6502: N flag based on intermediate ALU result, Z flag on binary sum
    let adcDecimalNmos : MicroOp = microOp (fun prev next bus ->
        let a = next.A
        let m = tempByte next
        let c = if next.CarryFlag then 1 else 0
        let mutable lo = (int a &&& 0x0F) + (int m &&& 0x0F) + c
        if lo > 9 then lo <- lo + 6
        let mutable hi = (int a >>> 4) + (int m >>> 4) + (if lo > 0x0F then 1 else 0)
        // Intermediate result after low nibble adjust, before high nibble adjust (used for N and V)
        let intermediate = byte (((hi &&& 0x0F) <<< 4) ||| (lo &&& 0x0F))
        let binSum = int a + int m + c
        // NMOS: V flag is based on intermediate result (after low adjust, before high adjust)
        next.OverflowFlag <- (((int a ^^^ int intermediate) &&& (int m ^^^ int intermediate) &&& 0x80) <> 0)
        if hi > 9 then hi <- hi + 6
        next.CarryFlag <- (hi > 0x0F)
        let result = byte (((hi &&& 0x0F) <<< 4) ||| (lo &&& 0x0F))
        next.A <- result
        // NMOS: Z flag is based on binary sum, N flag is based on intermediate result
        next.ZeroFlag <- (byte binSum = 0uy)
        next.NegativeFlag <- ((intermediate &&& 0x80uy) <> 0uy))

    /// ADC decimal mode for CMOS 65C02: N, Z, and V flags based on BCD result
    let adcDecimalCmos : MicroOp = microOp (fun prev next bus ->
        let a = next.A
        let m = tempByte next
        let c = if next.CarryFlag then 1 else 0
        let mutable lo = (int a &&& 0x0F) + (int m &&& 0x0F) + c
        if lo > 9 then lo <- lo + 6
        let mutable hi = (int a >>> 4) + (int m >>> 4) + (if lo > 0x0F then 1 else 0)
        // Calculate overflow BEFORE high nibble BCD correction, based on intermediate result
        // This matches the ALU's internal state after low nibble correction
        let intermediate = ((hi &&& 0x0F) <<< 4) ||| (lo &&& 0x0F)
        next.OverflowFlag <- (((int a ^^^ intermediate) &&& (int m ^^^ intermediate) &&& 0x80) <> 0)
        if hi > 9 then hi <- hi + 6
        next.CarryFlag <- (hi > 0x0F)
        let result = byte (((hi &&& 0x0F) <<< 4) ||| (lo &&& 0x0F))
        next.A <- result
        // CMOS: N and Z flags are based on the BCD result
        setNZ next next.A)

    /// ADC for NMOS 6502
    let adcNmos : MicroOp = microOp (fun prev next bus ->
        if next.DecimalFlag then
            adcDecimalNmos.Invoke(prev, next, bus)
        else
            adcBinary.Invoke(prev, next, bus))

    /// ADC for CMOS 65C02
    let adcCmos : MicroOp = microOp (fun prev next bus ->
        if next.DecimalFlag then
            adcDecimalCmos.Invoke(prev, next, bus)
        else
            adcBinary.Invoke(prev, next, bus))

    let sbcBinary : MicroOp = microOp (fun prev next bus ->
        let a = uint16 next.A
        let m = next.TempValue &&& 0xFFus
        let c = if next.CarryFlag then 0us else 1us
        let diff = a - m - c
        next.CarryFlag <- (diff < 256us)
        let result = byte diff
        next.OverflowFlag <- (((a ^^^ uint16 result) &&& ((~~~m) ^^^ uint16 result) &&& 0x80us) <> 0us)
        next.A <- result
        setNZ next next.A)

    /// SBC decimal mode for NMOS 6502: N and Z flags based on binary result
    let sbcDecimalNmos : MicroOp = microOp (fun prev next bus ->
        let a = next.A
        let m = tempByte next
        let c = if next.CarryFlag then 0 else 1
        let mutable lo = (int a &&& 0x0F) - (int m &&& 0x0F) - c
        if lo < 0 then lo <- lo - 6
        let mutable hi = (int a >>> 4) - (int m >>> 4) - (if lo < 0 then 1 else 0)
        if hi < 0 then hi <- hi - 6
        let binDiff = int a - int m - c
        next.CarryFlag <- (binDiff >= 0)
        next.OverflowFlag <- (((int a ^^^ binDiff) &&& ((int a ^^^ int m)) &&& 0x80) <> 0)
        let result = byte (((hi &&& 0x0F) <<< 4) ||| (lo &&& 0x0F))
        next.A <- result
        // NMOS: N and Z flags are based on the binary difference, not the BCD result
        setNZ next (byte binDiff))

    /// SBC decimal mode for CMOS 65C02: N and Z flags based on BCD result
    let sbcDecimalCmos : MicroOp = microOp (fun prev next bus ->
        let a = int next.A
        let m = int (tempByte next)
        let c = if next.CarryFlag then 0 else 1

        // Binary subtraction first
        let binResult = a - m - c

        // BCD correction
        let mutable result = binResult

        // Low nibble correction: if borrow from low nibble occurred
        if (a &&& 0x0F) < ((m &&& 0x0F) + c) then
            result <- result - 6

        // High nibble correction: if borrow from high nibble occurred  
        if binResult < 0 then
            result <- result - 0x60

        next.CarryFlag <- (binResult >= 0)
        next.OverflowFlag <- (((a ^^^ binResult) &&& (a ^^^ m) &&& 0x80) <> 0)
        next.A <- byte result
        // CMOS: N and Z flags are based on the BCD result
        setNZ next next.A)

    /// SBC for NMOS 6502
    let sbcNmos : MicroOp = microOp (fun prev next bus ->
        if next.DecimalFlag then
            sbcDecimalNmos.Invoke(prev, next, bus)
        else
            sbcBinary.Invoke(prev, next, bus))

    /// SBC for CMOS 65C02
    let sbcCmos : MicroOp = microOp (fun prev next bus ->
        if next.DecimalFlag then
            sbcDecimalCmos.Invoke(prev, next, bus)
        else
            sbcBinary.Invoke(prev, next, bus))

    // ========================================
    // Logic Operations
    // ========================================

    /// AND - Logical AND with Accumulator
    let andOp : MicroOp = microOp (fun prev next bus ->
        next.A <- next.A &&& tempByte next
        setNZ next next.A)

    /// ORA - Logical OR with Accumulator
    let oraOp : MicroOp = microOp (fun prev next bus ->
        next.A <- next.A ||| tempByte next
        setNZ next next.A)

    /// EOR - Logical XOR with Accumulator
    let eorOp : MicroOp = microOp (fun prev next bus ->
        next.A <- next.A ^^^ tempByte next
        setNZ next next.A)

    // ========================================
    // Compare Operations
    // ========================================

    /// CMP - Compare with Accumulator
    let cmpOp : MicroOp = microOp (fun prev next bus ->
        let m = tempByte next
        let result = int next.A - int m
        next.CarryFlag <- (next.A >= m)
        setNZ next (byte result))

    /// CPX - Compare with X Register
    let cpxOp : MicroOp = microOp (fun prev next bus ->
        let m = tempByte next
        let result = int next.X - int m
        next.CarryFlag <- (next.X >= m)
        setNZ next (byte result))

    /// CPY - Compare with Y Register
    let cpyOp : MicroOp = microOp (fun prev next bus ->
        let m = tempByte next
        let result = int next.Y - int m
        next.CarryFlag <- (next.Y >= m)
        setNZ next (byte result))

    // ========================================
    // BIT Operation
    // ========================================

    /// BIT - Bit Test (sets N and V from memory, Z from A AND memory)
    let bitOp : MicroOp = microOp (fun prev next bus ->
        let m = tempByte next
        next.ZeroFlag <- ((next.A &&& m) = 0uy)
        next.NegativeFlag <- ((m &&& 0x80uy) <> 0uy)
        next.OverflowFlag <- ((m &&& 0x40uy) <> 0uy))

    /// BIT #imm - Immediate mode only sets Z flag (65C02)
    let bitImmOp : MicroOp = microOp (fun prev next bus ->
        next.ZeroFlag <- ((next.A &&& tempByte next) = 0uy))

    // ========================================
    // TRB/TSB Operations (65C02)
    // ========================================

    /// TRB - Test and Reset Bits (sets Z from A AND memory, clears bits in memory)
    let trbOp : MicroOp = microOp (fun prev next bus ->
        let m = tempByte next
        next.ZeroFlag <- ((next.A &&& m) = 0uy)
        setTempByte next (m &&& ~~~next.A))

    /// TSB - Test and Set Bits (sets Z from A AND memory, sets bits in memory)
    let tsbOp : MicroOp = microOp (fun prev next bus ->
        let m = tempByte next
        next.ZeroFlag <- ((next.A &&& m) = 0uy)
        setTempByte next (m ||| next.A))

    // ========================================
    // Rockwell 65C02 Extensions
    // ========================================

    /// RMB - Reset Memory Bit (clear specific bit in memory)
    let rmbOp (bit: int) : MicroOp = microOp (fun prev next bus ->
        let mask = ~~~(1uy <<< bit)
        setTempByte next ((tempByte next) &&& mask))

    /// SMB - Set Memory Bit (set specific bit in memory)
    let smbOp (bit: int) : MicroOp = microOp (fun prev next bus ->
        let mask = 1uy <<< bit
        setTempByte next ((tempByte next) ||| mask))

    /// BBR - Branch on Bit Reset (branch if specific bit is 0)
    let bbrOp (bit: int) : MicroOp = microOp (fun prev next bus ->
        let mask = 1uy <<< bit
        if ((tempByte next) &&& mask) = 0uy then
            let offset = int8 (bus.CpuRead(next.PC))
            next.PC <- next.PC + 1us
            let oldPC = next.PC
            let newPC = uint16 (int next.PC + int offset)
            next.PC <- newPC
            // Create the completion micro-op
            let completeOp = microOp (fun _ n _ -> n.InstructionComplete <- true)
            if (oldPC >>> 8) <> (newPC >>> 8) then
                // Page crossing: add noOp then completeOp
                insertAfterCurrentOp next noOp
                next.Pipeline <- Array.append next.Pipeline [| completeOp |]
            else
                // No page crossing: just the penalty cycle that also completes
                insertAfterCurrentOp next completeOp
        else
            next.PC <- next.PC + 1us  // Skip the offset byte
            next.InstructionComplete <- true)

    /// BBS - Branch on Bit Set (branch if specific bit is 1)
    let bbsOp (bit: int) : MicroOp = microOp (fun prev next bus ->
        let mask = 1uy <<< bit
        if ((tempByte next) &&& mask) <> 0uy then
            let offset = int8 (bus.CpuRead(next.PC))
            next.PC <- next.PC + 1us
            let oldPC = next.PC
            let newPC = uint16 (int next.PC + int offset)
            next.PC <- newPC
            // Create the completion micro-op
            let completeOp = microOp (fun _ n _ -> n.InstructionComplete <- true)
            if (oldPC >>> 8) <> (newPC >>> 8) then
                // Page crossing: add noOp then completeOp
                insertAfterCurrentOp next noOp
                next.Pipeline <- Array.append next.Pipeline [| completeOp |]
            else
                // No page crossing: just the penalty cycle that also completes
                insertAfterCurrentOp next completeOp
        else
            next.PC <- next.PC + 1us  // Skip the offset byte
            next.InstructionComplete <- true)

    // ========================================
    // Flag Operations
    // ========================================

    /// CLC - Clear Carry Flag
    let clc : MicroOp = microOp (fun prev next bus ->
        next.CarryFlag <- false)

    /// SEC - Set Carry Flag
    let sec : MicroOp = microOp (fun prev next bus ->
        next.CarryFlag <- true)

    /// CLD - Clear Decimal Flag
    let cld : MicroOp = microOp (fun prev next bus ->
        next.DecimalFlag <- false)

    /// SED - Set Decimal Flag
    let sed : MicroOp = microOp (fun prev next bus ->
        next.DecimalFlag <- true)

    /// CLI - Clear Interrupt Disable Flag
    let cli : MicroOp = microOp (fun prev next bus ->
        next.InterruptDisableFlag <- false)

    /// SEI - Set Interrupt Disable Flag
    let sei : MicroOp = microOp (fun prev next bus ->
        next.InterruptDisableFlag <- true)

    /// CLV - Clear Overflow Flag
    let clv : MicroOp = microOp (fun prev next bus ->
        next.OverflowFlag <- false)

    /// Branch if condition is met. Handles penalty cycles for taken branches and page crossings.
    /// When branch is taken, appends penalty cycles and completion to the pipeline.
    /// When branch is not taken, marks complete immediately.
    /// Note: The condition is checked on 'prev' (the state at instruction start) because
    /// flags should not change during branch instruction execution.
    ///
    /// 6502 Branch timing (taken, no page cross - 3 cycles):
    ///   T0: Read opcode from PC, PC++
    ///   T1: Read offset from PC, PC++ (PC now points to address after branch instruction)
    ///   T2: Dummy read from PC (address after branch) while adding offset internally
    ///
    /// 6502 Branch timing (taken, page cross - 4 cycles):
    ///   T0: Read opcode from PC, PC++
    ///   T1: Read offset from PC, PC++ (PC now points to address after branch instruction)
    ///   T2: Dummy read from PC (address after branch) while adding offset to PCL
    ///   T3: Dummy read from "wrong" address (old PCH : new PCL) while fixing PCH
    let branchIf (condition: CpuState -> bool) : MicroOp = microOp (fun prev next bus ->
        let offset = int8 (tempByte next)
        if condition prev then
            let oldPC = next.PC  // PC after operand fetch (address following branch instruction)
            let newPC = uint16 (int next.PC + int offset)
            next.PC <- newPC
            if (oldPC >>> 8) <> (newPC >>> 8) then
                // Page crossing: 
                //   T2: dummy read at oldPC (address after branch instruction)
                //   T3: dummy read at "wrong" address (old PCH : new PCL), then complete
                let wrongAddr = (oldPC &&& 0xFF00us) ||| (newPC &&& 0x00FFus)
                let penaltyT2 = microOp (fun _ n b ->
                    b.CpuRead(oldPC) |> ignore)
                let penaltyT3WithComplete = microOp (fun _ n b ->
                    b.CpuRead(wrongAddr) |> ignore
                    n.InstructionComplete <- true)
                insertAfterCurrentOp next penaltyT2
                next.Pipeline <- Array.append next.Pipeline [| penaltyT3WithComplete |]
            else
                // No page crossing: T2 dummy read at oldPC (address after branch), then complete
                let penaltyWithComplete = microOp (fun _ n b ->
                    b.CpuRead(oldPC) |> ignore
                    n.InstructionComplete <- true)
                insertAfterCurrentOp next penaltyWithComplete
        else
            // Branch not taken - mark complete immediately
            next.InstructionComplete <- true)

    let branchIfCarrySet = branchIf (fun s -> s.CarryFlag)
    let branchIfCarryClear = branchIf (fun s -> not s.CarryFlag)
    let branchIfZeroSet = branchIf (fun s -> s.ZeroFlag)
    let branchIfZeroClear = branchIf (fun s -> not s.ZeroFlag)
    let branchIfNegative = branchIf (fun s -> s.NegativeFlag)
    let branchIfPositive = branchIf (fun s -> not s.NegativeFlag)
    let branchIfOverflowSet = branchIf (fun s -> s.OverflowFlag)
    let branchIfOverflowClear = branchIf (fun s -> not s.OverflowFlag)
    let branchAlways = branchIf (fun _ -> true)

    let push (getValue: CpuState -> byte) : MicroOp = microOp (fun prev next bus ->
        let addr = 0x0100us + uint16 next.SP
        bus.Write(addr, getValue next)
        next.SP <- next.SP - 1uy)

    let pull (setValue: CpuState -> byte -> unit) : MicroOp = microOp (fun prev next bus ->
        next.SP <- next.SP + 1uy
        let addr = 0x0100us + uint16 next.SP
        setValue next (bus.CpuRead(addr)))

    let pushPCH : MicroOp = push (fun s -> byte (s.PC >>> 8))
    let pushPCL : MicroOp = push (fun s -> byte s.PC)
    let pushP : MicroOp = push (fun s -> s.P ||| CpuState.FlagB ||| CpuState.FlagU)
    let pushPForInterrupt : MicroOp = push (fun s -> (s.P ||| CpuState.FlagU) &&& ~~~CpuState.FlagB)
    let pullP : MicroOp = pull (fun s v -> s.P <- (v ||| CpuState.FlagU) &&& ~~~CpuState.FlagB)

    // Stack operations for Phase 6
    let pushA : MicroOp = push (fun s -> s.A)
    let pullA : MicroOp = microOp (fun prev next bus ->
        next.SP <- next.SP + 1uy
        let addr = 0x0100us + uint16 next.SP
        next.A <- bus.CpuRead(addr)
        setNZ next next.A)

    let pushX : MicroOp = push (fun s -> s.X)
    let pullX : MicroOp = microOp (fun prev next bus ->
        next.SP <- next.SP + 1uy
        let addr = 0x0100us + uint16 next.SP
        next.X <- bus.CpuRead(addr)
        setNZ next next.X)

    let pushY : MicroOp = push (fun s -> s.Y)
    let pullY : MicroOp = microOp (fun prev next bus ->
        next.SP <- next.SP + 1uy
        let addr = 0x0100us + uint16 next.SP
        next.Y <- bus.CpuRead(addr)
        setNZ next next.Y)

    /// Pull PCL from stack (for RTS/RTI)
    let pullPCL : MicroOp = microOp (fun prev next bus ->
        next.SP <- next.SP + 1uy
        let addr = 0x0100us + uint16 next.SP
        let lo = bus.CpuRead(addr)
        next.TempAddress <- uint16 lo)

    /// Pull PCH from stack and set PC (for RTS/RTI)
    let pullPCH : MicroOp = microOp (fun prev next bus ->
        next.SP <- next.SP + 1uy
        let addr = 0x0100us + uint16 next.SP
        let hi = bus.CpuRead(addr)
        next.PC <- next.TempAddress ||| (uint16 hi <<< 8))

    /// Increment PC after RTS (since JSR pushes PC-1)
    let incrementPC : MicroOp = microOp (fun prev next bus ->
        next.PC <- next.PC + 1us)

    let dummyStackRead : MicroOp = microOp (fun prev next bus ->
        let addr = 0x0100us + uint16 next.SP
        bus.CpuRead(addr) |> ignore)

    let setInterruptDisable : MicroOp = microOp (fun prev next bus ->
        next.InterruptDisableFlag <- true)

    let readVectorLow (vectorAddr: uint16) : MicroOp = microOp (fun prev next bus ->
        let lo = bus.CpuRead(vectorAddr)
        next.TempAddress <- uint16 lo)

    let readVectorHigh (vectorAddr: uint16) : MicroOp = microOp (fun prev next bus ->
        let hi = bus.CpuRead(vectorAddr + 1us)
        next.PC <- next.TempAddress ||| (uint16 hi <<< 8))

    let jumpToTempAddress : MicroOp = microOp (fun prev next bus ->
        next.PC <- next.TempAddress)

    let incrementPCForReturn : MicroOp = microOp (fun prev next bus ->
        next.PC <- next.PC - 1us)

    let markComplete : MicroOp = microOp (fun prev next bus ->
        next.InstructionComplete <- true)

    // ========================================
    // Indexed Addressing Micro-Ops
    // ========================================

    /// Add X to TempAddress with zero page wrap (result stays in 0x00-0xFF)
    /// Includes a dummy read at the original address (before adding X)
    /// The 6502 always accesses the bus, so the dummy read is cycle-accurate
    let addXZeroPageWithDummyRead : MicroOp = microOp (fun prev next bus ->
        // Dummy read at the base zero page address before adding X
        bus.CpuRead(next.TempAddress &&& 0x00FFus) |> ignore
        next.TempAddress <- (next.TempAddress + uint16 next.X) &&& 0x00FFus)

    /// Add X to TempAddress with zero page wrap and dummy read (for cycle accuracy)
    /// This is the standard version for indexed zero page addressing
    let addXZeroPage : MicroOp = microOp (fun prev next bus ->
        // Dummy read at the base zero page address while adding X
        bus.CpuRead(next.TempAddress &&& 0x00FFus) |> ignore
        next.TempAddress <- (next.TempAddress + uint16 next.X) &&& 0x00FFus)

    /// Add Y to TempAddress with zero page wrap and dummy read (for cycle accuracy)
    let addYZeroPage : MicroOp = microOp (fun prev next bus ->
        // Dummy read at the base zero page address while adding Y
        bus.CpuRead(next.TempAddress &&& 0x00FFus) |> ignore
        next.TempAddress <- (next.TempAddress + uint16 next.Y) &&& 0x00FFus)

    /// Add X to TempAddress and check for page crossing (for abs,X)
    /// On page crossing, inserts a penalty cycle that reads from the "wrong" address
    let addXCheckPage : MicroOp = microOp (fun prev next bus ->
        let baseAddr = next.TempAddress
        next.TempAddress <- next.TempAddress + uint16 next.X
        let basePage = baseAddr >>> 8
        let newPage = next.TempAddress >>> 8
        if basePage <> newPage then
            // Page crossing - insert penalty cycle that reads from "wrong" address
            // Wrong address = base high byte + (low byte after adding X)
            let wrongAddr = (baseAddr &&& 0xFF00us) ||| (next.TempAddress &&& 0x00FFus)
            let penaltyOp = microOp (fun _ n b ->
                b.CpuRead(wrongAddr) |> ignore)
            insertAfterCurrentOp next penaltyOp)

    /// Add X to TempAddress and check for page crossing (65C02 version)
    /// On page crossing, inserts a penalty cycle that reads from the high byte address (T2)
    /// TempValue must contain the high byte fetch address before calling this
    let addXCheckPage65C02 : MicroOp = microOp (fun prev next bus ->
        let baseAddr = next.TempAddress
        let highByteAddr = next.TempValue  // Saved T2 address
        next.TempAddress <- next.TempAddress + uint16 next.X
        let basePage = baseAddr >>> 8
        let newPage = next.TempAddress >>> 8
        if basePage <> newPage then
            // Page crossing - 65C02 reads from high byte address (same as T2)
            let penaltyOp = microOp (fun _ n b ->
                b.CpuRead(highByteAddr) |> ignore)
            insertAfterCurrentOp next penaltyOp)

    /// Add Y to TempAddress and check for page crossing (for abs,Y)
    /// On page crossing, inserts a penalty cycle that reads from the "wrong" address
    let addYCheckPage : MicroOp = microOp (fun prev next bus ->
        let baseAddr = next.TempAddress
        next.TempAddress <- next.TempAddress + uint16 next.Y
        let basePage = baseAddr >>> 8
        let newPage = next.TempAddress >>> 8
        if basePage <> newPage then
            // Page crossing - insert penalty cycle that reads from "wrong" address
            let wrongAddr = (baseAddr &&& 0xFF00us) ||| (next.TempAddress &&& 0x00FFus)
            let penaltyOp = microOp (fun _ n b ->
                b.CpuRead(wrongAddr) |> ignore)
            insertAfterCurrentOp next penaltyOp)

    /// Add Y to TempAddress and check for page crossing (65C02 version)
    /// On page crossing, inserts a penalty cycle that reads from the high byte address (T2)
    /// TempValue must contain the high byte fetch address before calling this
    let addYCheckPage65C02 : MicroOp = microOp (fun prev next bus ->
        let baseAddr = next.TempAddress
        let highByteAddr = next.TempValue  // Saved T2 address
        next.TempAddress <- next.TempAddress + uint16 next.Y
        let basePage = baseAddr >>> 8
        let newPage = next.TempAddress >>> 8
        if basePage <> newPage then
            // Page crossing - 65C02 reads from high byte address (same as T2)
            let penaltyOp = microOp (fun _ n b ->
                b.CpuRead(highByteAddr) |> ignore)
            insertAfterCurrentOp next penaltyOp)

    // ========================================
    // Indirect Addressing Micro-Ops
    // ========================================

    /// Read pointer low byte from zero page TempAddress, store ZP addr in TempValue for next cycle
    let readPointerLowZP : MicroOp = microOp (fun prev next bus ->
        let zpAddr = next.TempAddress &&& 0x00FFus
        let lo = bus.CpuRead(zpAddr)
        next.TempValue <- zpAddr  // Store ZP address for next micro-op
        next.TempAddress <- uint16 lo)  // Start building effective address

    /// Read pointer high byte from (stored ZP + 1) and combine to form full address
    let readPointerHighZP : MicroOp = microOp (fun prev next bus ->
        let zpAddr = (next.TempValue + 1us) &&& 0x00FFus  // ZP wrap using stored addr
        let hi = bus.CpuRead(zpAddr)
        next.TempAddress <- next.TempAddress ||| (uint16 hi <<< 8))

    /// Read pointer high byte and add Y with page crossing check (for (zp),Y - NMOS)
    /// On page crossing, inserts a penalty cycle that reads from the "wrong" address
    let readPointerHighZPAddY : MicroOp = microOp (fun prev next bus ->
        let zpAddr = (next.TempValue + 1us) &&& 0x00FFus
        let hi = bus.CpuRead(zpAddr)
        let baseAddr = next.TempAddress ||| (uint16 hi <<< 8)
        next.TempAddress <- baseAddr + uint16 next.Y
        let basePage = baseAddr >>> 8
        let newPage = next.TempAddress >>> 8
        if basePage <> newPage then
            // Page crossing - insert penalty cycle that reads from "wrong" address
            let wrongAddr = (baseAddr &&& 0xFF00us) ||| (next.TempAddress &&& 0x00FFus)
            let penaltyOp = microOp (fun _ n b ->
                b.CpuRead(wrongAddr) |> ignore)
            insertAfterCurrentOp next penaltyOp)

    /// Read pointer high byte and add Y with page crossing check (for (zp),Y - 65C02)
    /// On page crossing, inserts a penalty cycle that reads from the operand address (T1)
    /// Uses prev.PC - 1 to get the operand address (PC was incremented after T1)
    let readPointerHighZPAddY65C02 : MicroOp = microOp (fun prev next bus ->
        let zpAddr = (next.TempValue + 1us) &&& 0x00FFus
        let hi = bus.CpuRead(zpAddr)
        let baseAddr = next.TempAddress ||| (uint16 hi <<< 8)
        next.TempAddress <- baseAddr + uint16 next.Y
        let basePage = baseAddr >>> 8
        let newPage = next.TempAddress >>> 8
        if basePage <> newPage then
            // Page crossing - 65C02 reads from operand address
            // The operand was fetched at PC-1 (PC is now pointing past instruction)
            let operandAddr = next.PC - 1us
            let penaltyOp = microOp (fun _ n b ->
                b.CpuRead(operandAddr) |> ignore)
            insertAfterCurrentOp next penaltyOp)

    /// Read pointer high byte and add Y for RMW (indirect),Y instructions.
    /// RMW instructions ALWAYS read from the "wrong" address first (regardless of page crossing),
    /// then read from the correct address. This stores the wrong and correct addresses.
    /// T3: Read high byte, calculate addresses, store wrong addr in TempAddress, correct in TempValue
    let readPointerHighZPSetupRMWIZY : MicroOp = microOp (fun prev next bus ->
        let zpAddr = (next.TempValue + 1us) &&& 0x00FFus
        let hi = bus.CpuRead(zpAddr)  // This is the T3 bus access
        let baseAddr = next.TempAddress ||| (uint16 hi <<< 8)
        let correctAddr = baseAddr + uint16 next.Y
        // Calculate the "wrong" address: base high byte with adjusted low byte
        let wrongAddr = (baseAddr &&& 0xFF00us) ||| (correctAddr &&& 0x00FFus)
        // Store wrong address for T4, correct address in TempValue for later
        next.TempAddress <- wrongAddr
        next.TempValue <- correctAddr)

    /// Read from wrong address (T4) and then set TempAddress to correct address for T5
    let readWrongAddressFixRMWIZY : MicroOp = microOp (fun prev next bus ->
        bus.CpuRead(next.TempAddress) |> ignore  // Read from wrong address
        next.TempAddress <- next.TempValue)       // Set correct address for next read

    /// Read pointer low byte from absolute TempAddress (for JMP indirect)
    let readPointerLowAbs : MicroOp = microOp (fun prev next bus ->
        let lo = bus.CpuRead(next.TempAddress)
        next.TempValue <- next.TempAddress  // Store full pointer address
        next.TempAddress <- uint16 lo)

    /// Read pointer high byte from (stored pointer + 1) for JMP indirect
    let readPointerHighAbs : MicroOp = microOp (fun prev next bus ->
        let hi = bus.CpuRead(next.TempValue + 1us)
        next.TempAddress <- next.TempAddress ||| (uint16 hi <<< 8))

    /// Read pointer high byte with NMOS page wrap bug for JMP ($xxFF)
    /// When pointer is at $xxFF, high byte is read from $xx00 instead of $xx00+1
    let readPointerHighAbsNMOS : MicroOp = microOp (fun prev next bus ->
        // If low byte of pointer address is $FF, wrap within page
        let hiAddr = 
            if (next.TempValue &&& 0xFFus) = 0xFFus then
                next.TempValue &&& 0xFF00us  // Wrap to $xx00
            else
                next.TempValue + 1us
        let hi = bus.CpuRead(hiAddr)
        next.TempAddress <- next.TempAddress ||| (uint16 hi <<< 8))

    // ========================================
    // Phase 3: Increment/Decrement & Transfers
    // ========================================

    /// INX - Increment X register
    let inx : MicroOp = microOp (fun prev next bus ->
        next.X <- next.X + 1uy
        setNZ next next.X)

    /// INY - Increment Y register
    let iny : MicroOp = microOp (fun prev next bus ->
        next.Y <- next.Y + 1uy
        setNZ next next.Y)

    /// DEX - Decrement X register
    let dex : MicroOp = microOp (fun prev next bus ->
        next.X <- next.X - 1uy
        setNZ next next.X)

    /// DEY - Decrement Y register
    let dey : MicroOp = microOp (fun prev next bus ->
        next.Y <- next.Y - 1uy
        setNZ next next.Y)

    /// INC A - Increment Accumulator (65C02)
    let incA : MicroOp = microOp (fun prev next bus ->
        next.A <- next.A + 1uy
        setNZ next next.A)

    /// DEC A - Decrement Accumulator (65C02)
    let decA : MicroOp = microOp (fun prev next bus ->
        next.A <- next.A - 1uy
        setNZ next next.A)

    /// INC memory - Increment TempValue
    let incMem : MicroOp = microOp (fun prev next bus ->
        let result = (tempByte next) + 1uy
        setTempByte next result
        setNZ next result)

    /// DEC memory - Decrement TempValue
    let decMem : MicroOp = microOp (fun prev next bus ->
        let result = (tempByte next) - 1uy
        setTempByte next result
        setNZ next result)

    /// TAX - Transfer A to X
    let tax : MicroOp = microOp (fun prev next bus ->
        next.X <- next.A
        setNZ next next.X)

    /// TXA - Transfer X to A
    let txa : MicroOp = microOp (fun prev next bus ->
        next.A <- next.X
        setNZ next next.A)

    /// TAY - Transfer A to Y
    let tay : MicroOp = microOp (fun prev next bus ->
        next.Y <- next.A
        setNZ next next.Y)

    /// TYA - Transfer Y to A
    let tya : MicroOp = microOp (fun prev next bus ->
        next.A <- next.Y
        setNZ next next.A)

    /// TSX - Transfer SP to X
    let tsx : MicroOp = microOp (fun prev next bus ->
        next.X <- next.SP
        setNZ next next.X)

    /// TXS - Transfer X to SP (no flags affected)
    let txs : MicroOp = microOp (fun prev next bus ->
        next.SP <- next.X)

    // ========================================
    // Phase 4: Shifts & Rotates
    // ========================================

    /// ASL A - Arithmetic Shift Left Accumulator
    let aslA : MicroOp = microOp (fun prev next bus ->
        next.CarryFlag <- (next.A &&& 0x80uy) <> 0uy
        next.A <- next.A <<< 1
        setNZ next next.A)

    /// ASL memory - Arithmetic Shift Left TempValue
    let aslMem : MicroOp = microOp (fun prev next bus ->
        let m = tempByte next
        next.CarryFlag <- (m &&& 0x80uy) <> 0uy
        let result = m <<< 1
        setTempByte next result
        setNZ next result)

    /// LSR A - Logical Shift Right Accumulator
    let lsrA : MicroOp = microOp (fun prev next bus ->
        next.CarryFlag <- (next.A &&& 0x01uy) <> 0uy
        next.A <- next.A >>> 1
        setNZ next next.A)

    /// LSR memory - Logical Shift Right TempValue
    let lsrMem : MicroOp = microOp (fun prev next bus ->
        let m = tempByte next
        next.CarryFlag <- (m &&& 0x01uy) <> 0uy
        let result = m >>> 1
        setTempByte next result
        setNZ next result)

    /// ROL A - Rotate Left Accumulator
    let rolA : MicroOp = microOp (fun prev next bus ->
        let oldCarry = if next.CarryFlag then 1uy else 0uy
        next.CarryFlag <- (next.A &&& 0x80uy) <> 0uy
        next.A <- (next.A <<< 1) ||| oldCarry
        setNZ next next.A)

    /// ROL memory - Rotate Left TempValue
    let rolMem : MicroOp = microOp (fun prev next bus ->
        let m = tempByte next
        let oldCarry = if next.CarryFlag then 1uy else 0uy
        next.CarryFlag <- (m &&& 0x80uy) <> 0uy
        let result = (m <<< 1) ||| oldCarry
        setTempByte next result
        setNZ next result)

    /// ROR A - Rotate Right Accumulator
    let rorA : MicroOp = microOp (fun prev next bus ->
        let oldCarry = if next.CarryFlag then 0x80uy else 0uy
        next.CarryFlag <- (next.A &&& 0x01uy) <> 0uy
        next.A <- (next.A >>> 1) ||| oldCarry
        setNZ next next.A)

    /// ROR memory - Rotate Right TempValue
    let rorMem : MicroOp = microOp (fun prev next bus ->
        let m = tempByte next
        let oldCarry = if next.CarryFlag then 0x80uy else 0uy
        next.CarryFlag <- (m &&& 0x01uy) <> 0uy
        let result = (m >>> 1) ||| oldCarry
        setTempByte next result
        setNZ next result)

    // ========================================
    // Illegal/Undocumented 6502 Operations
    // ========================================

    /// LAX - Load A and X with the same value
    let laxOp : MicroOp = microOp (fun prev next bus ->
        let m = tempByte next
        next.A <- m
        next.X <- m
        setNZ next m)

    /// SAX - Store A AND X to memory (no flags affected)
    let saxOp : MicroOp = microOp (fun prev next bus ->
        setTempByte next (next.A &&& next.X))

    /// DCP - Decrement memory then Compare with A
    let dcpOp : MicroOp = microOp (fun prev next bus ->
        // Decrement memory
        let m = (tempByte next) - 1uy
        setTempByte next m
        // Compare with A
        let result = int next.A - int m
        next.CarryFlag <- next.A >= m
        next.ZeroFlag <- (result &&& 0xFF) = 0
        next.NegativeFlag <- (result &&& 0x80) <> 0)

    /// ISC/ISB - Increment memory then Subtract from A (respects decimal mode on NMOS)
    let iscOp : MicroOp = microOp (fun prev next bus ->
        // Increment memory
        let m = (tempByte next) + 1uy
        setTempByte next m
        // SBC operation - use decimal mode if D flag is set
        if next.DecimalFlag then
            sbcDecimalNmos.Invoke(prev, next, bus)
        else
            sbcBinary.Invoke(prev, next, bus))

    /// SLO - Shift Left memory then OR with A
    let sloOp : MicroOp = microOp (fun prev next bus ->
        let m = tempByte next
        // ASL
        next.CarryFlag <- (m &&& 0x80uy) <> 0uy
        let shifted = m <<< 1
        setTempByte next shifted
        // ORA
        next.A <- next.A ||| shifted
        setNZ next next.A)

    /// RLA - Rotate Left memory then AND with A
    let rlaOp : MicroOp = microOp (fun prev next bus ->
        let m = tempByte next
        let oldCarry = if next.CarryFlag then 1uy else 0uy
        // ROL
        next.CarryFlag <- (m &&& 0x80uy) <> 0uy
        let rotated = (m <<< 1) ||| oldCarry
        setTempByte next rotated
        // AND
        next.A <- next.A &&& rotated
        setNZ next next.A)

    /// SRE - Shift Right memory then EOR with A
    let sreOp : MicroOp = microOp (fun prev next bus ->
        let m = tempByte next
        // LSR
        next.CarryFlag <- (m &&& 0x01uy) <> 0uy
        let shifted = m >>> 1
        setTempByte next shifted
        // EOR
        next.A <- next.A ^^^ shifted
        setNZ next next.A)

    /// RRA - Rotate Right memory then ADC with A (respects decimal mode on NMOS)
    let rraOp : MicroOp = microOp (fun prev next bus ->
        let m = tempByte next
        let oldCarry = if next.CarryFlag then 0x80uy else 0uy
        // ROR
        next.CarryFlag <- (m &&& 0x01uy) <> 0uy
        let rotated = (m >>> 1) ||| oldCarry
        setTempByte next rotated
        // ADC operation - use decimal mode if D flag is set
        if next.DecimalFlag then
            adcDecimalNmos.Invoke(prev, next, bus)
        else
            adcBinary.Invoke(prev, next, bus))

    /// ANC - AND immediate then set Carry from bit 7
    let ancOp : MicroOp = microOp (fun prev next bus ->
        next.A <- next.A &&& (tempByte next)
        setNZ next next.A
        next.CarryFlag <- (next.A &&& 0x80uy) <> 0uy)

    /// ALR/ASR - AND immediate then LSR A
    let alrOp : MicroOp = microOp (fun prev next bus ->
        next.A <- next.A &&& (tempByte next)
        next.CarryFlag <- (next.A &&& 0x01uy) <> 0uy
        next.A <- next.A >>> 1
        setNZ next next.A)

    /// ARR - AND immediate then ROR A (special flag handling)
    /// ARR - AND immediate then ROR, with special flag handling
    /// In decimal mode, ARR has very unusual behavior with BCD fixups
    let arrOp : MicroOp = microOp (fun prev next bus ->
        let imm = tempByte next
        let andResult = next.A &&& imm
        let oldCarry = if next.CarryFlag then 0x80uy else 0uy
        let rorResult = (andResult >>> 1) ||| oldCarry

        if next.DecimalFlag then
            // Decimal mode: ARR has special BCD behavior
            // N and Z are set based on the ROR result
            setNZ next rorResult
            // V is set if bit 6 changed during the "BCD fixup"
            next.OverflowFlag <- ((andResult ^^^ rorResult) &&& 0x40uy) <> 0uy

            // BCD fixup on low nibble: if (andResult & 0x0F) + (andResult & 0x01) > 5
            let lowNibble = andResult &&& 0x0Fuy
            let mutable result = rorResult
            if lowNibble + (andResult &&& 0x01uy) > 5uy then
                result <- (result &&& 0xF0uy) ||| ((result + 6uy) &&& 0x0Fuy)

            // BCD fixup on high nibble: if high nibble of andResult >= 5
            // (indicating the pre-rotated high nibble would overflow BCD)
            let highNibble = andResult >>> 4
            if highNibble >= 5uy then
                result <- result + 0x60uy
                next.CarryFlag <- true
            else
                next.CarryFlag <- false

            next.A <- result
        else
            // Binary mode: standard ARR behavior
            next.A <- rorResult
            setNZ next next.A
            // Carry is set from bit 6 of result
            next.CarryFlag <- (next.A &&& 0x40uy) <> 0uy
            // Overflow is bit 6 XOR bit 5
            next.OverflowFlag <- ((next.A &&& 0x40uy) ^^^ ((next.A &&& 0x20uy) <<< 1)) <> 0uy)

    /// AXS/SBX - X = (A AND X) - immediate (no borrow)
    let axsOp : MicroOp = microOp (fun prev next bus ->
        let andResult = next.A &&& next.X
        let m = tempByte next
        let result = int andResult - int m
        next.CarryFlag <- andResult >= m
        next.X <- byte (result &&& 0xFF)
        setNZ next next.X)

    // ========================================
    // Unstable/Unreliable Illegal Opcodes
    // These have unpredictable behavior on real hardware.
    // The "magic constant" varies by chip; Harte tests use 0xEE.
    // ========================================

    /// ANE/XAA - A = (A | const) & X & imm
    /// The constant is chip-dependent; Harte tests expect 0xEE
    let aneOp : MicroOp = microOp (fun prev next bus ->
        let magic = 0xEEuy  // Chip-dependent constant (Harte tests use 0xEE)
        let imm = tempByte next
        next.A <- (next.A ||| magic) &&& next.X &&& imm
        setNZ next next.A)

    /// LXA/LAX imm - A = X = (A | const) & imm
    /// The constant is chip-dependent; Harte tests expect 0xEE
    let lxaOp : MicroOp = microOp (fun prev next bus ->
        let magic = 0xEEuy  // Chip-dependent constant (Harte tests use 0xEE)
        let imm = tempByte next
        let result = (next.A ||| magic) &&& imm
        next.A <- result
        next.X <- result
        setNZ next result)

    /// LAS - A = X = SP = SP & mem
    let lasOp : MicroOp = microOp (fun prev next bus ->
        let m = tempByte next
        let result = next.SP &&& m
        next.A <- result
        next.X <- result
        next.SP <- result
        setNZ next result)

    /// SHA/AHX - Store (A & X & (high byte of BASE address + 1))
    /// For unstable store opcodes, TempValue holds the BASE address high byte before indexing
    /// TempAddress holds the final (possibly wrong) address
    let shaOp : MicroOp = microOp (fun prev next bus ->
        // TempValue high byte contains the base address high byte (before adding index)
        let baseHigh = byte (next.TempValue >>> 8)
        let value = next.A &&& next.X &&& (baseHigh + 1uy)
        setTempByte next value)

    /// SHX - Store (X & (high byte of BASE address + 1))
    let shxOp : MicroOp = microOp (fun prev next bus ->
        let baseHigh = byte (next.TempValue >>> 8)
        let value = next.X &&& (baseHigh + 1uy)
        setTempByte next value)

    /// SHY - Store (Y & (high byte of BASE address + 1))
    let shyOp : MicroOp = microOp (fun prev next bus ->
        let baseHigh = byte (next.TempValue >>> 8)
        let value = next.Y &&& (baseHigh + 1uy)
        setTempByte next value)

    /// TAS/SHS - SP = A & X, then store (A & X & (high + 1))
    let tasOp : MicroOp = microOp (fun prev next bus ->
        next.SP <- next.A &&& next.X
        let baseHigh = byte (next.TempValue >>> 8)
        let value = next.A &&& next.X &&& (baseHigh + 1uy)
        setTempByte next value)

    /// Write for unstable store opcodes - handles the weird page-crossing address glitch
    /// If page crossed, the destination high byte is AND'd with the value
    let writeUnstableStore : MicroOp = microOp (fun prev next bus ->
        let value = tempByte next
        let baseHigh = byte (next.TempValue >>> 8)
        let finalHigh = byte (next.TempAddress >>> 8)
        let finalLow = byte (next.TempAddress &&& 0xFFus)
        // If page crossed (base high != final high), the write address high is base_high & value
        let writeHigh = if baseHigh <> finalHigh then baseHigh &&& value else finalHigh
        let writeAddr = (uint16 writeHigh <<< 8) ||| uint16 finalLow
        bus.Write(writeAddr, value))

    /// JAM/KIL - Set status to Jammed (or Bypassed if IgnoreHaltStopWait is true)
    let jamOp : MicroOp = microOp (fun prev next bus ->
        if next.IgnoreHaltStopWait then
            next.Status <- CpuStatus.Bypassed // Mark as bypassed but continue running
        else
            next.Status <- CpuStatus.Jammed)

    /// STP - Stop the processor (or Bypassed if IgnoreHaltStopWait is true)
    let stpOp : MicroOp = microOp (fun prev next bus ->
        if next.IgnoreHaltStopWait then
            next.Status <- CpuStatus.Bypassed // Mark as bypassed but continue running
        else
            next.Status <- CpuStatus.Stopped)

    /// WAI - Wait for interrupt (or Bypassed if IgnoreHaltStopWait is true)
    let waiOp : MicroOp = microOp (fun prev next bus ->
        if next.IgnoreHaltStopWait then
            next.Status <- CpuStatus.Bypassed // Mark as bypassed but continue running
        else
            next.Status <- CpuStatus.Waiting)
