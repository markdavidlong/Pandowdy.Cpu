// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Runtime.CompilerServices;

namespace Pandowdy.Cpu.Internals;

/// <summary>
/// Provides micro-operation implementations for 6502/65C02 CPU emulation.
/// </summary>
/// <remarks>
/// <para>
/// Each micro-op represents a single clock cycle of CPU execution. Instructions are
/// decomposed into sequences of micro-ops for cycle-accurate emulation.
/// </para>
/// <para>
/// Micro-ops receive three parameters:
/// <list type="bullet">
///   <item><description><c>prev</c>: The committed state at instruction start (read-only reference).</description></item>
///   <item><description><c>current</c>: The working state being modified during execution.</description></item>
///   <item><description><c>bus</c>: The memory/IO bus for read/write operations.</description></item>
/// </list>
/// </para>
/// </remarks>
internal static class MicroOps
{
    // ========================================
    // Helper Methods
    // ========================================

    /// <summary>
    /// Sets the Zero and Negative flags based on a byte value.
    /// </summary>
    /// <param name="state">The CPU state to modify.</param>
    /// <param name="value">The value to test.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetNZ(CpuState state, byte value)
    {
        state.ZeroFlag = value == 0;
        state.NegativeFlag = (value & 0x80) != 0;
    }

    /// <summary>
    /// Gets the low byte of TempValue for 8-bit operations.
    /// </summary>
    /// <param name="state">The CPU state to read from.</param>
    /// <returns>The low byte of TempValue.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte TempByte(CpuState state) => (byte)state.TempValue;

    /// <summary>
    /// Stores a byte in TempValue (ensures high byte is zero).
    /// </summary>
    /// <param name="state">The CPU state to modify.</param>
    /// <param name="value">The byte value to store.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetTempByte(CpuState state, byte value)
    {
        state.TempValue = value;
    }

    /// <summary>
    /// Inserts a micro-op after the current operation in the pipeline.
    /// </summary>
    /// <param name="state">The CPU state containing the pipeline.</param>
    /// <param name="op">The micro-op to insert.</param>
    /// <remarks>
    /// <para>
    /// During micro-op execution, PipelineIndex points to the current op. After execution,
    /// the index is incremented. This method inserts at PipelineIndex + 1 to make the
    /// new op execute next.
    /// </para>
    /// <para>
    /// This method uses an in-place insertion strategy to avoid allocations:
    /// On first insertion, the base pipeline is copied to WorkingPipeline.
    /// Subsequent insertions modify WorkingPipeline directly.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void InsertAfterCurrentOp(CpuState state, MicroOp op)
    {
        int insertIdx = state.PipelineIndex + 1;

        // If not already using working pipeline, copy from base pipeline
        if (state.WorkingPipelineLength == 0)
        {
            int baseLen = state.Pipeline.Length;
            Array.Copy(state.Pipeline, state.WorkingPipeline, baseLen);
            state.WorkingPipelineLength = baseLen;
        }

        int currentLen = state.WorkingPipelineLength;

        // Shift elements after insertIdx to make room
        for (int i = currentLen - 1; i >= insertIdx; i--)
        {
            state.WorkingPipeline[i + 1] = state.WorkingPipeline[i];
        }

        // Insert the new op
        state.WorkingPipeline[insertIdx] = op;
        state.WorkingPipelineLength = currentLen + 1;
    }

    /// <summary>
    /// Appends a micro-op to the end of the working pipeline.
    /// </summary>
    /// <param name="state">The CPU state containing the pipeline.</param>
    /// <param name="op">The micro-op to append.</param>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="InsertAfterCurrentOp"/> which inserts immediately after
    /// the current operation, this method appends to the very end of the pipeline.
    /// </para>
    /// <para>
    /// This is used when adding multiple penalty cycles where the first penalty
    /// is inserted and the second should come after all existing operations.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AppendToWorkingPipeline(CpuState state, MicroOp op)
    {
        // If not already using working pipeline, copy from base pipeline
        if (state.WorkingPipelineLength == 0)
        {
            int baseLen = state.Pipeline.Length;
            Array.Copy(state.Pipeline, state.WorkingPipeline, baseLen);
            state.WorkingPipelineLength = baseLen;
        }

        // Append at the end
        state.WorkingPipeline[state.WorkingPipelineLength] = op;
        state.WorkingPipelineLength++;
    }

    /// <summary>
    /// Adds a penalty cycle by inserting a dummy read after the current operation.
    /// </summary>
    /// <param name="state">The CPU state to modify.</param>
    /// <remarks>
    /// The 6502 always accesses the bus, so penalty cycles perform a read at the incomplete address.
    /// </remarks>
    public static void AddPenaltyCycle(CpuState state)
    {
        InsertAfterCurrentOp(state, DummyReadTempAddress);
    }

    // ========================================
    // Addressing Mode Micro-Ops
    // ========================================

    /// <summary>
    /// Fetches the opcode byte from PC and increments PC.
    /// </summary>
    public static readonly MicroOp FetchOpcode = (prev, current, bus) =>
    {
        current.OpcodeAddress = current.PC;
        byte opcode = bus.CpuRead(current.PC);
        current.CurrentOpcode = opcode;
        SetTempByte(current, opcode);
        current.PC++;
    };

    /// <summary>
    /// Fetches an immediate operand from PC and increments PC.
    /// </summary>
    public static readonly MicroOp FetchImmediate = (prev, current, bus) =>
    {
        SetTempByte(current, bus.CpuRead(current.PC));
        current.PC++;
    };

    /// <summary>
    /// Fetches the low byte of an address from PC and increments PC.
    /// </summary>
    public static readonly MicroOp FetchAddressLow = (prev, current, bus) =>
    {
        byte lo = bus.CpuRead(current.PC);
        current.TempAddress = lo;
        current.PC++;
    };

    /// <summary>
    /// Fetches the high byte of an address from PC and increments PC.
    /// </summary>
    public static readonly MicroOp FetchAddressHigh = (prev, current, bus) =>
    {
        byte hi = bus.CpuRead(current.PC);
        current.TempAddress = (ushort)(current.TempAddress | (hi << 8));
        current.PC++;
    };

    /// <summary>
    /// Reads a byte from TempAddress into TempValue.
    /// </summary>
    public static readonly MicroOp ReadFromTempAddress = (prev, current, bus) =>
    {
        SetTempByte(current, bus.CpuRead(current.TempAddress));
    };

    /// <summary>
    /// Writes TempValue to TempAddress.
    /// </summary>
    public static readonly MicroOp WriteToTempAddress = (prev, current, bus) =>
    {
        bus.Write(current.TempAddress, TempByte(current));
    };

    /// <summary>
    /// Reads from zero page TempAddress (masked to 0x00-0xFF).
    /// </summary>
    public static readonly MicroOp ReadZeroPage = (prev, current, bus) =>
    {
        SetTempByte(current, bus.CpuRead((ushort)(current.TempAddress & 0x00FF)));
    };

    /// <summary>
    /// Writes to zero page TempAddress (masked to 0x00-0xFF).
    /// </summary>
    public static readonly MicroOp WriteZeroPage = (prev, current, bus) =>
    {
        bus.Write((ushort)(current.TempAddress & 0x00FF), TempByte(current));
    };

    /// <summary>
    /// Adds X to TempAddress (no bus access).
    /// </summary>
    public static readonly MicroOp AddX = (prev, current, bus) =>
    {
        current.TempAddress = (ushort)(current.TempAddress + current.X);
    };

    /// <summary>
    /// Adds X to TempAddress with a dummy read at the partially-computed address.
    /// Used for RMW abs,X instructions which always take the extra cycle.
    /// </summary>
    public static readonly MicroOp AddXWithDummyRead = (prev, current, bus) =>
    {
        ushort baseAddr = current.TempAddress;
        byte lowPlusX = (byte)(baseAddr + current.X);
        ushort wrongAddr = (ushort)((baseAddr & 0xFF00) | lowPlusX);
        bus.CpuRead(wrongAddr);
        current.TempAddress = (ushort)(baseAddr + current.X);
    };

    /// <summary>
    /// Adds Y to TempAddress (no bus access).
    /// </summary>
    public static readonly MicroOp AddY = (prev, current, bus) =>
    {
        current.TempAddress = (ushort)(current.TempAddress + current.Y);
    };

    /// <summary>
    /// Adds Y to TempAddress with a dummy read at the partially-computed address.
    /// Used for store abs,Y instructions which always read before writing.
    /// </summary>
    public static readonly MicroOp AddYWithDummyRead = (prev, current, bus) =>
    {
        ushort baseAddr = current.TempAddress;
        byte lowPlusY = (byte)(baseAddr + current.Y);
        ushort wrongAddr = (ushort)((baseAddr & 0xFF00) | lowPlusY);
        bus.CpuRead(wrongAddr);
        current.TempAddress = (ushort)(baseAddr + current.Y);
    };

    /// <summary>
    /// No operation - does nothing.
    /// </summary>
    public static readonly MicroOp NoOp = (prev, current, bus) => { };

    /// <summary>
    /// Dummy read at PC - used for implied/accumulator addressing modes.
    /// </summary>
    public static readonly MicroOp DummyReadPC = (prev, current, bus) =>
    {
        bus.CpuRead(current.PC);
    };

    /// <summary>
    /// Dummy read at TempAddress - used for read-modify-write instructions.
    /// </summary>
    public static readonly MicroOp DummyReadTempAddress = (prev, current, bus) =>
    {
        bus.CpuRead(current.TempAddress);
    };

    /// <summary>
    /// Dummy read at PenaltyAddress - used for page-crossing penalty cycles.
    /// </summary>
    /// <remarks>
    /// This avoids allocating a closure to capture the wrong address.
    /// The address is stored in PenaltyAddress before the penalty cycle is inserted.
    /// </remarks>
    public static readonly MicroOp DummyReadPenaltyAddress = (prev, current, bus) =>
    {
        bus.CpuRead(current.PenaltyAddress);
    };

    /// <summary>
    /// Branch penalty cycle: reads from BranchOldPC (taken branch, no page cross).
    /// </summary>
    public static readonly MicroOp BranchPenaltyReadOldPCAndComplete = (prev, current, bus) =>
    {
        bus.CpuRead(current.BranchOldPC);
        current.InstructionComplete = true;
    };

    /// <summary>
    /// Branch penalty cycle T2: reads from BranchOldPC (page crossing, first penalty).
    /// </summary>
    public static readonly MicroOp BranchPenaltyReadOldPC = (prev, current, bus) =>
    {
        bus.CpuRead(current.BranchOldPC);
    };

    /// <summary>
    /// Branch penalty cycle T3: reads from PenaltyAddress and completes (page crossing, second penalty).
    /// </summary>
    public static readonly MicroOp BranchPenaltyReadWrongAddrAndComplete = (prev, current, bus) =>
    {
        bus.CpuRead(current.PenaltyAddress);
        current.InstructionComplete = true;
    };

    /// <summary>
    /// Dummy write to TempAddress - writes the original value back before the modified value.
    /// </summary>
    public static readonly MicroOp DummyWriteTempAddress = (prev, current, bus) =>
    {
        bus.Write(current.TempAddress, TempByte(current));
    };

    /// <summary>
    /// Dummy write to zero page TempAddress.
    /// </summary>
    public static readonly MicroOp DummyWriteZeroPage = (prev, current, bus) =>
    {
        bus.Write((ushort)(current.TempAddress & 0x00FF), TempByte(current));
    };

    /// <summary>
    /// Checks for page crossing and adds a penalty cycle if needed.
    /// </summary>
    public static readonly MicroOp CheckPageCrossing = (prev, current, bus) =>
    {
        ushort basePage = (ushort)(prev.TempAddress >> 8);
        ushort newPage = (ushort)(current.TempAddress >> 8);
        if (basePage != newPage)
        {
            AddPenaltyCycle(current);
        }
    };

    /// <summary>
    /// Adds X to TempAddress with zero page wrap and dummy read.
    /// </summary>
    public static readonly MicroOp AddXZeroPage = (prev, current, bus) =>
    {
        bus.CpuRead((ushort)(current.TempAddress & 0x00FF));
        current.TempAddress = (ushort)((current.TempAddress + current.X) & 0x00FF);
    };

    /// <summary>
    /// Adds Y to TempAddress with zero page wrap and dummy read.
    /// </summary>
    public static readonly MicroOp AddYZeroPage = (prev, current, bus) =>
    {
        bus.CpuRead((ushort)(current.TempAddress & 0x00FF));
        current.TempAddress = (ushort)((current.TempAddress + current.Y) & 0x00FF);
    };

    /// <summary>
    /// Adds X to TempAddress and checks for page crossing (NMOS).
    /// </summary>
    public static readonly MicroOp AddXCheckPage = (prev, current, bus) =>
    {
        ushort baseAddr = current.TempAddress;
        current.TempAddress = (ushort)(current.TempAddress + current.X);
        ushort basePage = (ushort)(baseAddr >> 8);
        ushort newPage = (ushort)(current.TempAddress >> 8);
        if (basePage != newPage)
        {
            // Store the wrong address for the penalty cycle to read from
            current.PenaltyAddress = (ushort)((baseAddr & 0xFF00) | (current.TempAddress & 0x00FF));
            InsertAfterCurrentOp(current, DummyReadPenaltyAddress);
        }
    };

    /// <summary>
    /// Adds X to TempAddress and checks for page crossing (65C02).
    /// </summary>
    public static readonly MicroOp AddXCheckPage65C02 = (prev, current, bus) =>
    {
        ushort baseAddr = current.TempAddress;
        ushort highByteAddr = current.TempValue;
        current.TempAddress = (ushort)(current.TempAddress + current.X);
        ushort basePage = (ushort)(baseAddr >> 8);
        ushort newPage = (ushort)(current.TempAddress >> 8);
        if (basePage != newPage)
        {
            // Store the high byte address for the penalty cycle to read from
            current.PenaltyAddress = highByteAddr;
            InsertAfterCurrentOp(current, DummyReadPenaltyAddress);
        }
    };

    /// <summary>
    /// Adds Y to TempAddress and checks for page crossing (NMOS).
    /// </summary>
    public static readonly MicroOp AddYCheckPage = (prev, current, bus) =>
    {
        ushort baseAddr = current.TempAddress;
        current.TempAddress = (ushort)(current.TempAddress + current.Y);
        ushort basePage = (ushort)(baseAddr >> 8);
        ushort newPage = (ushort)(current.TempAddress >> 8);
        if (basePage != newPage)
        {
            // Store the wrong address for the penalty cycle to read from
            current.PenaltyAddress = (ushort)((baseAddr & 0xFF00) | (current.TempAddress & 0x00FF));
            InsertAfterCurrentOp(current, DummyReadPenaltyAddress);
        }
    };

    /// <summary>
    /// Adds Y to TempAddress and checks for page crossing (65C02).
    /// </summary>
    public static readonly MicroOp AddYCheckPage65C02 = (prev, current, bus) =>
    {
        ushort baseAddr = current.TempAddress;
        ushort highByteAddr = current.TempValue;
        current.TempAddress = (ushort)(current.TempAddress + current.Y);
        ushort basePage = (ushort)(baseAddr >> 8);
        ushort newPage = (ushort)(current.TempAddress >> 8);
        if (basePage != newPage)
        {
            // Store the high byte address for the penalty cycle to read from
            current.PenaltyAddress = highByteAddr;
            InsertAfterCurrentOp(current, DummyReadPenaltyAddress);
        }
    };


    // ========================================
    // Load/Store Micro-Ops
    // ========================================

    /// <summary>
    /// Loads TempValue into the Accumulator and sets N/Z flags.
    /// </summary>
    public static readonly MicroOp LoadA = (prev, current, bus) =>
    {
        current.A = TempByte(current);
        SetNZ(current, current.A);
    };

    /// <summary>
    /// Loads TempValue into the X register and sets N/Z flags.
    /// </summary>
    public static readonly MicroOp LoadX = (prev, current, bus) =>
    {
        current.X = TempByte(current);
        SetNZ(current, current.X);
    };

    /// <summary>
    /// Loads TempValue into the Y register and sets N/Z flags.
    /// </summary>
    public static readonly MicroOp LoadY = (prev, current, bus) =>
    {
        current.Y = TempByte(current);
        SetNZ(current, current.Y);
    };

    /// <summary>
    /// Stores the Accumulator into TempValue.
    /// </summary>
    public static readonly MicroOp StoreA = (prev, current, bus) =>
    {
        SetTempByte(current, current.A);
    };

    /// <summary>
    /// Stores the X register into TempValue.
    /// </summary>
    public static readonly MicroOp StoreX = (prev, current, bus) =>
    {
        SetTempByte(current, current.X);
    };

    /// <summary>
    /// Stores the Y register into TempValue.
    /// </summary>
    public static readonly MicroOp StoreY = (prev, current, bus) =>
    {
        SetTempByte(current, current.Y);
    };

    /// <summary>
    /// Stores zero into TempValue (STZ - 65C02).
    /// </summary>
    public static readonly MicroOp StoreZ = (prev, current, bus) =>
    {
        SetTempByte(current, 0);
    };

    // ========================================
    // ALU Micro-Ops
    // ========================================

    /// <summary>
    /// ADC in binary mode.
    /// </summary>
    public static readonly MicroOp AdcBinary = (prev, current, bus) =>
    {
        int a = current.A;
        int m = current.TempValue & 0xFF;
        int c = current.CarryFlag ? 1 : 0;
        int sum = a + m + c;
        current.CarryFlag = sum > 255;
        byte result = (byte)sum;
        current.OverflowFlag = ((a ^ result) & (m ^ result) & 0x80) != 0;
        current.A = result;
        SetNZ(current, current.A);
    };

    /// <summary>
    /// ADC decimal mode for NMOS 6502.
    /// </summary>
    public static readonly MicroOp AdcDecimalNmos = (prev, current, bus) =>
    {
        byte a = current.A;
        byte m = TempByte(current);
        int c = current.CarryFlag ? 1 : 0;

        int lo = (a & 0x0F) + (m & 0x0F) + c;
        if (lo > 9)
        {
            lo += 6;
        }

        int hi = (a >> 4) + (m >> 4) + (lo > 0x0F ? 1 : 0);
        byte intermediate = (byte)(((hi & 0x0F) << 4) | (lo & 0x0F));
        int binSum = a + m + c;

        current.OverflowFlag = ((a ^ intermediate) & (m ^ intermediate) & 0x80) != 0;

        if (hi > 9)
        {
            hi += 6;
        }

        current.CarryFlag = hi > 0x0F;
        byte result = (byte)(((hi & 0x0F) << 4) | (lo & 0x0F));
        current.A = result;
        current.ZeroFlag = (byte)binSum == 0;
        current.NegativeFlag = (intermediate & 0x80) != 0;
    };

    /// <summary>
    /// ADC decimal mode for CMOS 65C02.
    /// </summary>
    public static readonly MicroOp AdcDecimalCmos = (prev, current, bus) =>
    {
        byte a = current.A;
        byte m = TempByte(current);
        int c = current.CarryFlag ? 1 : 0;

        int lo = (a & 0x0F) + (m & 0x0F) + c;
        if (lo > 9)
        {
            lo += 6;
        }

        int hi = (a >> 4) + (m >> 4) + (lo > 0x0F ? 1 : 0);
        int intermediate = ((hi & 0x0F) << 4) | (lo & 0x0F);

        current.OverflowFlag = ((a ^ intermediate) & (m ^ intermediate) & 0x80) != 0;

        if (hi > 9)
        {
            hi += 6;
        }

        current.CarryFlag = hi > 0x0F;
        byte result = (byte)(((hi & 0x0F) << 4) | (lo & 0x0F));
        current.A = result;
        SetNZ(current, current.A);
    };

    /// <summary>
    /// ADC for NMOS 6502 (binary or decimal based on D flag).
    /// </summary>
    public static readonly MicroOp AdcNmos = (prev, current, bus) =>
    {
        if (current.DecimalFlag)
        {
            AdcDecimalNmos(prev, current, bus);
        }
        else
        {
            AdcBinary(prev, current, bus);
        }
    };

    /// <summary>
    /// ADC for CMOS 65C02 (binary or decimal based on D flag).
    /// </summary>
    public static readonly MicroOp AdcCmos = (prev, current, bus) =>
    {
        if (current.DecimalFlag)
        {
            AdcDecimalCmos(prev, current, bus);
        }
        else
        {
            AdcBinary(prev, current, bus);
        }
    };

    /// <summary>
    /// SBC in binary mode.
    /// </summary>
    public static readonly MicroOp SbcBinary = (prev, current, bus) =>
    {
        int a = current.A;
        int m = current.TempValue & 0xFF;
        int c = current.CarryFlag ? 0 : 1;
        int diff = a - m - c;
        current.CarryFlag = diff >= 0;
        byte result = (byte)diff;
        current.OverflowFlag = ((a ^ result) & ((~m) ^ result) & 0x80) != 0;
        current.A = result;
        SetNZ(current, current.A);
    };

    /// <summary>
    /// SBC decimal mode for NMOS 6502.
    /// </summary>
    public static readonly MicroOp SbcDecimalNmos = (prev, current, bus) =>
    {
        byte a = current.A;
        byte m = TempByte(current);
        int c = current.CarryFlag ? 0 : 1;

        int lo = (a & 0x0F) - (m & 0x0F) - c;
        if (lo < 0)
        {
            lo -= 6;
        }

        int hi = (a >> 4) - (m >> 4) - (lo < 0 ? 1 : 0);
        if (hi < 0)
        {
            hi -= 6;
        }

        int binDiff = a - m - c;
        current.CarryFlag = binDiff >= 0;
        current.OverflowFlag = ((a ^ binDiff) & (a ^ m) & 0x80) != 0;
        byte result = (byte)(((hi & 0x0F) << 4) | (lo & 0x0F));
        current.A = result;
        SetNZ(current, (byte)binDiff);
    };

    /// <summary>
    /// SBC decimal mode for CMOS 65C02.
    /// </summary>
    public static readonly MicroOp SbcDecimalCmos = (prev, current, bus) =>
    {
        int a = current.A;
        int m = TempByte(current);
        int c = current.CarryFlag ? 0 : 1;

        int binResult = a - m - c;
        int result = binResult;

        if ((a & 0x0F) < ((m & 0x0F) + c))
        {
            result -= 6;
        }

        if (binResult < 0)
        {
            result -= 0x60;
        }

        current.CarryFlag = binResult >= 0;
        current.OverflowFlag = ((a ^ binResult) & (a ^ m) & 0x80) != 0;
        current.A = (byte)result;
        SetNZ(current, current.A);
    };

    /// <summary>
    /// SBC for NMOS 6502 (binary or decimal based on D flag).
    /// </summary>
    public static readonly MicroOp SbcNmos = (prev, current, bus) =>
    {
        if (current.DecimalFlag)
        {
            SbcDecimalNmos(prev, current, bus);
        }
        else
        {
            SbcBinary(prev, current, bus);
        }
    };

    /// <summary>
    /// SBC for CMOS 65C02 (binary or decimal based on D flag).
    /// </summary>
    public static readonly MicroOp SbcCmos = (prev, current, bus) =>
    {
        if (current.DecimalFlag)
        {
            SbcDecimalCmos(prev, current, bus);
        }
        else
        {
            SbcBinary(prev, current, bus);
        }
    };

    /// <summary>
    /// AND - Logical AND with Accumulator.
    /// </summary>
    public static readonly MicroOp AndOp = (prev, current, bus) =>
    {
        current.A = (byte)(current.A & TempByte(current));
        SetNZ(current, current.A);
    };

    /// <summary>
    /// ORA - Logical OR with Accumulator.
    /// </summary>
    public static readonly MicroOp OraOp = (prev, current, bus) =>
    {
        current.A = (byte)(current.A | TempByte(current));
        SetNZ(current, current.A);
    };

    /// <summary>
    /// EOR - Logical XOR with Accumulator.
    /// </summary>
    public static readonly MicroOp EorOp = (prev, current, bus) =>
    {
        current.A = (byte)(current.A ^ TempByte(current));
        SetNZ(current, current.A);
    };

    /// <summary>
    /// CMP - Compare with Accumulator.
    /// </summary>
    public static readonly MicroOp CmpOp = (prev, current, bus) =>
    {
        byte m = TempByte(current);
        int result = current.A - m;
        current.CarryFlag = current.A >= m;
        SetNZ(current, (byte)result);
    };

    /// <summary>
    /// CPX - Compare with X register.
    /// </summary>
    public static readonly MicroOp CpxOp = (prev, current, bus) =>
    {
        byte m = TempByte(current);
        int result = current.X - m;
        current.CarryFlag = current.X >= m;
        SetNZ(current, (byte)result);
    };

    /// <summary>
    /// CPY - Compare with Y register.
    /// </summary>
    public static readonly MicroOp CpyOp = (prev, current, bus) =>
    {
        byte m = TempByte(current);
        int result = current.Y - m;
        current.CarryFlag = current.Y >= m;
        SetNZ(current, (byte)result);
    };

    /// <summary>
    /// BIT - Bit test (sets N and V from memory, Z from A AND memory).
    /// </summary>
    public static readonly MicroOp BitOp = (prev, current, bus) =>
    {
        byte m = TempByte(current);
        current.ZeroFlag = (current.A & m) == 0;
        current.NegativeFlag = (m & 0x80) != 0;
        current.OverflowFlag = (m & 0x40) != 0;
    };

    /// <summary>
    /// BIT #imm - Immediate mode only sets Z flag (65C02).
    /// </summary>
    public static readonly MicroOp BitImmOp = (prev, current, bus) =>
    {
        current.ZeroFlag = (current.A & TempByte(current)) == 0;
    };

    /// <summary>
    /// TRB - Test and Reset Bits (65C02).
    /// </summary>
    public static readonly MicroOp TrbOp = (prev, current, bus) =>
    {
        byte m = TempByte(current);
        current.ZeroFlag = (current.A & m) == 0;
        SetTempByte(current, (byte)(m & ~current.A));
    };

    /// <summary>
    /// TSB - Test and Set Bits (65C02).
    /// </summary>
    public static readonly MicroOp TsbOp = (prev, current, bus) =>
    {
        byte m = TempByte(current);
        current.ZeroFlag = (current.A & m) == 0;
        SetTempByte(current, (byte)(m | current.A));
    };

    // ========================================
    // Shift/Rotate Micro-Ops
    // ========================================

    /// <summary>
    /// ASL A - Arithmetic Shift Left Accumulator.
    /// </summary>
    public static readonly MicroOp AslA = (prev, current, bus) =>
    {
        current.CarryFlag = (current.A & 0x80) != 0;
        current.A = (byte)(current.A << 1);
        SetNZ(current, current.A);
    };

    /// <summary>
    /// ASL memory - Arithmetic Shift Left TempValue.
    /// </summary>
    public static readonly MicroOp AslMem = (prev, current, bus) =>
    {
        byte m = TempByte(current);
        current.CarryFlag = (m & 0x80) != 0;
        byte result = (byte)(m << 1);
        SetTempByte(current, result);
        SetNZ(current, result);
    };

    /// <summary>
    /// LSR A - Logical Shift Right Accumulator.
    /// </summary>
    public static readonly MicroOp LsrA = (prev, current, bus) =>
    {
        current.CarryFlag = (current.A & 0x01) != 0;
        current.A = (byte)(current.A >> 1);
        SetNZ(current, current.A);
    };

    /// <summary>
    /// LSR memory - Logical Shift Right TempValue.
    /// </summary>
    public static readonly MicroOp LsrMem = (prev, current, bus) =>
    {
        byte m = TempByte(current);
        current.CarryFlag = (m & 0x01) != 0;
        byte result = (byte)(m >> 1);
        SetTempByte(current, result);
        SetNZ(current, result);
    };

    /// <summary>
    /// ROL A - Rotate Left Accumulator.
    /// </summary>
    public static readonly MicroOp RolA = (prev, current, bus) =>
    {
        byte oldCarry = (byte)(current.CarryFlag ? 1 : 0);
        current.CarryFlag = (current.A & 0x80) != 0;
        current.A = (byte)((current.A << 1) | oldCarry);
        SetNZ(current, current.A);
    };

    /// <summary>
    /// ROL memory - Rotate Left TempValue.
    /// </summary>
    public static readonly MicroOp RolMem = (prev, current, bus) =>
    {
        byte m = TempByte(current);
        byte oldCarry = (byte)(current.CarryFlag ? 1 : 0);
        current.CarryFlag = (m & 0x80) != 0;
        byte result = (byte)((m << 1) | oldCarry);
        SetTempByte(current, result);
        SetNZ(current, result);
    };

    /// <summary>
    /// ROR A - Rotate Right Accumulator.
    /// </summary>
    public static readonly MicroOp RorA = (prev, current, bus) =>
    {
        byte oldCarry = (byte)(current.CarryFlag ? 0x80 : 0);
        current.CarryFlag = (current.A & 0x01) != 0;
        current.A = (byte)((current.A >> 1) | oldCarry);
        SetNZ(current, current.A);
    };

    /// <summary>
    /// ROR memory - Rotate Right TempValue.
    /// </summary>
    public static readonly MicroOp RorMem = (prev, current, bus) =>
    {
        byte m = TempByte(current);
        byte oldCarry = (byte)(current.CarryFlag ? 0x80 : 0);
        current.CarryFlag = (m & 0x01) != 0;
        byte result = (byte)((m >> 1) | oldCarry);
        SetTempByte(current, result);
        SetNZ(current, result);
    };

    // ========================================
    // Increment/Decrement Micro-Ops
    // ========================================

    /// <summary>
    /// INX - Increment X register.
    /// </summary>
    public static readonly MicroOp Inx = (prev, current, bus) =>
    {
        current.X++;
        SetNZ(current, current.X);
    };

    /// <summary>
    /// INY - Increment Y register.
    /// </summary>
    public static readonly MicroOp Iny = (prev, current, bus) =>
    {
        current.Y++;
        SetNZ(current, current.Y);
    };

    /// <summary>
    /// DEX - Decrement X register.
    /// </summary>
    public static readonly MicroOp Dex = (prev, current, bus) =>
    {
        current.X--;
        SetNZ(current, current.X);
    };

    /// <summary>
    /// DEY - Decrement Y register.
    /// </summary>
    public static readonly MicroOp Dey = (prev, current, bus) =>
    {
        current.Y--;
        SetNZ(current, current.Y);
    };

    /// <summary>
    /// INC A - Increment Accumulator (65C02).
    /// </summary>
    public static readonly MicroOp IncA = (prev, current, bus) =>
    {
        current.A++;
        SetNZ(current, current.A);
    };

    /// <summary>
    /// DEC A - Decrement Accumulator (65C02).
    /// </summary>
    public static readonly MicroOp DecA = (prev, current, bus) =>
    {
        current.A--;
        SetNZ(current, current.A);
    };

    /// <summary>
    /// INC memory - Increment TempValue.
    /// </summary>
    public static readonly MicroOp IncMem = (prev, current, bus) =>
    {
        byte result = (byte)(TempByte(current) + 1);
        SetTempByte(current, result);
        SetNZ(current, result);
    };

    /// <summary>
    /// DEC memory - Decrement TempValue.
    /// </summary>
    public static readonly MicroOp DecMem = (prev, current, bus) =>
    {
        byte result = (byte)(TempByte(current) - 1);
        SetTempByte(current, result);
        SetNZ(current, result);
    };

    // ========================================
    // Transfer Micro-Ops
    // ========================================

    /// <summary>
    /// TAX - Transfer A to X.
    /// </summary>
    public static readonly MicroOp Tax = (prev, current, bus) =>
    {
        current.X = current.A;
        SetNZ(current, current.X);
    };

    /// <summary>
    /// TXA - Transfer X to A.
    /// </summary>
    public static readonly MicroOp Txa = (prev, current, bus) =>
    {
        current.A = current.X;
        SetNZ(current, current.A);
    };

    /// <summary>
    /// TAY - Transfer A to Y.
    /// </summary>
    public static readonly MicroOp Tay = (prev, current, bus) =>
    {
        current.Y = current.A;
        SetNZ(current, current.Y);
    };

    /// <summary>
    /// TYA - Transfer Y to A.
    /// </summary>
    public static readonly MicroOp Tya = (prev, current, bus) =>
    {
        current.A = current.Y;
        SetNZ(current, current.A);
    };

    /// <summary>
    /// TSX - Transfer SP to X.
    /// </summary>
    public static readonly MicroOp Tsx = (prev, current, bus) =>
    {
        current.X = current.SP;
        SetNZ(current, current.X);
    };

    /// <summary>
    /// TXS - Transfer X to SP (no flags affected).
    /// </summary>
    public static readonly MicroOp Txs = (prev, current, bus) =>
    {
        current.SP = current.X;
    };

    // ========================================
    // Flag Micro-Ops
    // ========================================

    /// <summary>
    /// CLC - Clear Carry Flag.
    /// </summary>
    public static readonly MicroOp Clc = (prev, current, bus) =>
    {
        current.CarryFlag = false;
    };

    /// <summary>
    /// SEC - Set Carry Flag.
    /// </summary>
    public static readonly MicroOp Sec = (prev, current, bus) =>
    {
        current.CarryFlag = true;
    };

    /// <summary>
    /// CLD - Clear Decimal Flag.
    /// </summary>
    public static readonly MicroOp Cld = (prev, current, bus) =>
    {
        current.DecimalFlag = false;
    };

    /// <summary>
    /// SED - Set Decimal Flag.
    /// </summary>
    public static readonly MicroOp Sed = (prev, current, bus) =>
    {
        current.DecimalFlag = true;
    };

    /// <summary>
    /// CLI - Clear Interrupt Disable Flag.
    /// </summary>
    public static readonly MicroOp Cli = (prev, current, bus) =>
    {
        current.InterruptDisableFlag = false;
    };

    /// <summary>
    /// SEI - Set Interrupt Disable Flag.
    /// </summary>
    public static readonly MicroOp Sei = (prev, current, bus) =>
    {
        current.InterruptDisableFlag = true;
    };

    /// <summary>
    /// CLV - Clear Overflow Flag.
    /// </summary>
    public static readonly MicroOp Clv = (prev, current, bus) =>
    {
        current.OverflowFlag = false;
    };

    /// <summary>
    /// Set Interrupt Disable flag.
    /// </summary>
    public static readonly MicroOp SetInterruptDisable = (prev, current, bus) =>
    {
        current.InterruptDisableFlag = true;
    };

    /// <summary>
    /// Marks the current instruction as complete.
    /// </summary>
    public static readonly MicroOp MarkComplete = (prev, current, bus) =>
    {
        current.InstructionComplete = true;
    };

    // ========================================
    // Branch Micro-Ops
    // ========================================

    /// <summary>
    /// Creates a branch micro-op that branches if the condition is met.
    /// </summary>
    /// <param name="condition">The condition to test on the previous state.</param>
    /// <returns>A micro-op that performs the conditional branch.</returns>
    public static MicroOp BranchIf(Func<CpuState, bool> condition) => (prev, current, bus) =>
    {
        sbyte offset = (sbyte)TempByte(current);
        if (condition(prev))
        {
            ushort oldPC = current.PC;
            ushort newPC = (ushort)(current.PC + offset);
            current.PC = newPC;

            // Store addresses for penalty cycles (avoids closure allocations)
            current.BranchOldPC = oldPC;

            if ((oldPC >> 8) != (newPC >> 8))
            {
                // Page crossing: need two penalty cycles
                current.PenaltyAddress = (ushort)((oldPC & 0xFF00) | (newPC & 0x00FF));
                InsertAfterCurrentOp(current, BranchPenaltyReadOldPC);

                // Add the second penalty cycle to the working pipeline
                if (current.WorkingPipelineLength == 0)
                {
                    // Copy base pipeline to working pipeline first
                    int baseLen = current.Pipeline.Length;
                    Array.Copy(current.Pipeline, current.WorkingPipeline, baseLen);
                    current.WorkingPipelineLength = baseLen;
                }
                current.WorkingPipeline[current.WorkingPipelineLength] = BranchPenaltyReadWrongAddrAndComplete;
                current.WorkingPipelineLength++;
            }
            else
            {
                // No page crossing: just one penalty cycle
                InsertAfterCurrentOp(current, BranchPenaltyReadOldPCAndComplete);
            }
        }
        else
        {
            current.InstructionComplete = true;
        }
    };

    /// <summary>BCS - Branch if Carry Set.</summary>
    public static readonly MicroOp BranchIfCarrySet = BranchIf(s => s.CarryFlag);


    /// <summary>BCC - Branch if Carry Clear.</summary>
    public static readonly MicroOp BranchIfCarryClear = BranchIf(s => !s.CarryFlag);

    /// <summary>BEQ - Branch if Zero Set.</summary>
    public static readonly MicroOp BranchIfZeroSet = BranchIf(s => s.ZeroFlag);

    /// <summary>BNE - Branch if Zero Clear.</summary>
    public static readonly MicroOp BranchIfZeroClear = BranchIf(s => !s.ZeroFlag);

    /// <summary>BMI - Branch if Negative Set.</summary>
    public static readonly MicroOp BranchIfNegative = BranchIf(s => s.NegativeFlag);

    /// <summary>BPL - Branch if Negative Clear.</summary>
    public static readonly MicroOp BranchIfPositive = BranchIf(s => !s.NegativeFlag);

    /// <summary>BVS - Branch if Overflow Set.</summary>
    public static readonly MicroOp BranchIfOverflowSet = BranchIf(s => s.OverflowFlag);

    /// <summary>BVC - Branch if Overflow Clear.</summary>
    public static readonly MicroOp BranchIfOverflowClear = BranchIf(s => !s.OverflowFlag);

    /// <summary>BRA - Branch Always (65C02).</summary>
    public static readonly MicroOp BranchAlways = BranchIf(_ => true);

    // ========================================
    // Stack Micro-Ops
    // ========================================

    /// <summary>
    /// Creates a push micro-op that pushes a value to the stack.
    /// </summary>
    private static MicroOp Push(Func<CpuState, byte> getValue) => (prev, current, bus) =>
    {
        ushort addr = (ushort)(0x0100 + current.SP);
        bus.Write(addr, getValue(current));
        current.SP--;
    };

    /// <summary>
    /// Creates a pull micro-op that pulls a value from the stack.
    /// </summary>
    private static MicroOp Pull(Action<CpuState, byte> setValue) => (prev, current, bus) =>
    {
        current.SP++;
        ushort addr = (ushort)(0x0100 + current.SP);
        setValue(current, bus.CpuRead(addr));
    };

    /// <summary>Push PCH to stack.</summary>
    public static readonly MicroOp PushPCH = Push(s => (byte)(s.PC >> 8));

    /// <summary>Push PCL to stack.</summary>
    public static readonly MicroOp PushPCL = Push(s => (byte)s.PC);

    /// <summary>Push P to stack with B and U flags set (for BRK/PHP).</summary>
    public static readonly MicroOp PushP = Push(s => (byte)(s.P | CpuState.FlagB | CpuState.FlagU));

    /// <summary>Push P to stack for interrupt (B clear, U set).</summary>
    public static readonly MicroOp PushPForInterrupt = Push(s => (byte)((s.P | CpuState.FlagU) & ~CpuState.FlagB));

    /// <summary>Pull P from stack (B ignored, U always set).</summary>
    public static readonly MicroOp PullP = Pull((s, v) => s.P = (byte)((v | CpuState.FlagU) & ~CpuState.FlagB));

    /// <summary>Push A to stack.</summary>
    public static readonly MicroOp PushA = Push(s => s.A);

    /// <summary>Pull A from stack and set N/Z flags.</summary>
    public static readonly MicroOp PullA = (prev, current, bus) =>
    {
        current.SP++;
        ushort addr = (ushort)(0x0100 + current.SP);
        current.A = bus.CpuRead(addr);
        SetNZ(current, current.A);
    };

    /// <summary>Push X to stack (65C02).</summary>
    public static readonly MicroOp PushX = Push(s => s.X);

    /// <summary>Pull X from stack and set N/Z flags (65C02).</summary>
    public static readonly MicroOp PullX = (prev, current, bus) =>
    {
        current.SP++;
        ushort addr = (ushort)(0x0100 + current.SP);
        current.X = bus.CpuRead(addr);
        SetNZ(current, current.X);
    };

    /// <summary>Push Y to stack (65C02).</summary>
    public static readonly MicroOp PushY = Push(s => s.Y);

    /// <summary>Pull Y from stack and set N/Z flags (65C02).</summary>
    public static readonly MicroOp PullY = (prev, current, bus) =>
    {
        current.SP++;
        ushort addr = (ushort)(0x0100 + current.SP);
        current.Y = bus.CpuRead(addr);
        SetNZ(current, current.Y);
    };

    /// <summary>Pull PCL from stack (for RTS/RTI).</summary>
    public static readonly MicroOp PullPCL = (prev, current, bus) =>
    {
        current.SP++;
        ushort addr = (ushort)(0x0100 + current.SP);
        byte lo = bus.CpuRead(addr);
        current.TempAddress = lo;
    };

    /// <summary>Pull PCH from stack and set PC (for RTS/RTI).</summary>
    public static readonly MicroOp PullPCH = (prev, current, bus) =>
    {
        current.SP++;
        ushort addr = (ushort)(0x0100 + current.SP);
        byte hi = bus.CpuRead(addr);
        current.PC = (ushort)(current.TempAddress | (hi << 8));
    };

    /// <summary>Increment PC after RTS (since JSR pushes PC-1).</summary>
    public static readonly MicroOp IncrementPC = (prev, current, bus) =>
    {
        current.PC++;
    };

    /// <summary>Dummy read from stack address.</summary>
    public static readonly MicroOp DummyStackRead = (prev, current, bus) =>
    {
        ushort addr = (ushort)(0x0100 + current.SP);
        bus.CpuRead(addr);
    };

    // ========================================
    // Jump/Subroutine Micro-Ops
    // ========================================

    /// <summary>Read vector low byte.</summary>
    public static MicroOp ReadVectorLow(ushort vectorAddr) => (prev, current, bus) =>
    {
        byte lo = bus.CpuRead(vectorAddr);
        current.TempAddress = lo;
    };

    /// <summary>Read vector high byte and set PC.</summary>
    public static MicroOp ReadVectorHigh(ushort vectorAddr) => (prev, current, bus) =>
    {
        byte hi = bus.CpuRead((ushort)(vectorAddr + 1));
        current.PC = (ushort)(current.TempAddress | (hi << 8));
    };

    /// <summary>Jump to TempAddress.</summary>
    public static readonly MicroOp JumpToTempAddress = (prev, current, bus) =>
    {
        current.PC = current.TempAddress;
    };

    /// <summary>Decrement PC for return address storage.</summary>
    public static readonly MicroOp IncrementPCForReturn = (prev, current, bus) =>
    {
        current.PC--;
    };

    // ========================================
    // Indirect Addressing Micro-Ops
    // ========================================

    /// <summary>Read pointer low byte from zero page, store ZP addr in TempValue.</summary>
    public static readonly MicroOp ReadPointerLowZP = (prev, current, bus) =>
    {
        ushort zpAddr = (ushort)(current.TempAddress & 0x00FF);
        byte lo = bus.CpuRead(zpAddr);
        current.TempValue = zpAddr;
        current.TempAddress = lo;
    };

    /// <summary>Read pointer high byte from (stored ZP + 1) with ZP wrap.</summary>
    public static readonly MicroOp ReadPointerHighZP = (prev, current, bus) =>
    {
        ushort zpAddr = (ushort)((current.TempValue + 1) & 0x00FF);
        byte hi = bus.CpuRead(zpAddr);
        current.TempAddress = (ushort)(current.TempAddress | (hi << 8));
    };

    /// <summary>Read pointer high byte and add Y with page crossing check (NMOS).</summary>
    public static readonly MicroOp ReadPointerHighZPAddY = (prev, current, bus) =>
    {
        ushort zpAddr = (ushort)((current.TempValue + 1) & 0x00FF);
        byte hi = bus.CpuRead(zpAddr);
        ushort baseAddr = (ushort)(current.TempAddress | (hi << 8));
        current.TempAddress = (ushort)(baseAddr + current.Y);
        ushort basePage = (ushort)(baseAddr >> 8);
        ushort newPage = (ushort)(current.TempAddress >> 8);
        if (basePage != newPage)
        {
            // Store the wrong address for the penalty cycle to read from
            current.PenaltyAddress = (ushort)((baseAddr & 0xFF00) | (current.TempAddress & 0x00FF));
            InsertAfterCurrentOp(current, DummyReadPenaltyAddress);
        }
    };

    /// <summary>Read pointer high byte and add Y with page crossing check (65C02).</summary>
    public static readonly MicroOp ReadPointerHighZPAddY65C02 = (prev, current, bus) =>
    {
        ushort zpAddr = (ushort)((current.TempValue + 1) & 0x00FF);
        byte hi = bus.CpuRead(zpAddr);
        ushort baseAddr = (ushort)(current.TempAddress | (hi << 8));
        current.TempAddress = (ushort)(baseAddr + current.Y);
        ushort basePage = (ushort)(baseAddr >> 8);
        ushort newPage = (ushort)(current.TempAddress >> 8);
        if (basePage != newPage)
        {
            // Store the operand address for the penalty cycle to read from
            current.PenaltyAddress = (ushort)(current.PC - 1);
            InsertAfterCurrentOp(current, DummyReadPenaltyAddress);
        }
    };

    /// <summary>Read pointer high byte and setup for RMW (indirect),Y instructions.</summary>
    public static readonly MicroOp ReadPointerHighZPSetupRMWIZY = (prev, current, bus) =>
    {
        ushort zpAddr = (ushort)((current.TempValue + 1) & 0x00FF);
        byte hi = bus.CpuRead(zpAddr);
        ushort baseAddr = (ushort)(current.TempAddress | (hi << 8));
        ushort correctAddr = (ushort)(baseAddr + current.Y);
        ushort wrongAddr = (ushort)((baseAddr & 0xFF00) | (correctAddr & 0x00FF));
        current.TempAddress = wrongAddr;
        current.TempValue = correctAddr;
    };

    /// <summary>Read from wrong address (T4) and set TempAddress to correct address.</summary>
    public static readonly MicroOp ReadWrongAddressFixRMWIZY = (prev, current, bus) =>
    {
        bus.CpuRead(current.TempAddress);
        current.TempAddress = current.TempValue;
    };

    /// <summary>Read pointer low byte from absolute TempAddress (for JMP indirect).</summary>
    public static readonly MicroOp ReadPointerLowAbs = (prev, current, bus) =>
    {
        byte lo = bus.CpuRead(current.TempAddress);
        current.TempValue = current.TempAddress;
        current.TempAddress = lo;
    };

    /// <summary>Read pointer high byte from (stored pointer + 1).</summary>
    public static readonly MicroOp ReadPointerHighAbs = (prev, current, bus) =>
    {
        byte hi = bus.CpuRead((ushort)(current.TempValue + 1));
        current.TempAddress = (ushort)(current.TempAddress | (hi << 8));
    };

    /// <summary>Read pointer high byte with NMOS page wrap bug for JMP ($xxFF).</summary>
    public static readonly MicroOp ReadPointerHighAbsNMOS = (prev, current, bus) =>
    {
        ushort hiAddr = (current.TempValue & 0xFF) == 0xFF
            ? (ushort)(current.TempValue & 0xFF00)
            : (ushort)(current.TempValue + 1);
        byte hi = bus.CpuRead(hiAddr);
        current.TempAddress = (ushort)(current.TempAddress | (hi << 8));
    };

    // ========================================
    // Rockwell 65C02 Extensions
    // ========================================

    /// <summary>RMB - Reset Memory Bit.</summary>
    public static MicroOp RmbOp(int bit) => (prev, current, bus) =>
    {
        byte mask = (byte)~(1 << bit);
        SetTempByte(current, (byte)(TempByte(current) & mask));
    };

    /// <summary>SMB - Set Memory Bit.</summary>
    public static MicroOp SmbOp(int bit) => (prev, current, bus) =>
    {
        byte mask = (byte)(1 << bit);
        SetTempByte(current, (byte)(TempByte(current) | mask));
    };

    /// <summary>BBR - Branch on Bit Reset.</summary>
    public static MicroOp BbrOp(int bit) => (prev, current, bus) =>
    {
        byte mask = (byte)(1 << bit);
        if ((TempByte(current) & mask) == 0)
        {
            sbyte offset = (sbyte)bus.CpuRead(current.PC);
            current.PC++;
            ushort oldPC = current.PC;
            ushort newPC = (ushort)(current.PC + offset);
            current.PC = newPC;

            MicroOp completeOp = (_, n, _) => n.InstructionComplete = true;
            if ((oldPC >> 8) != (newPC >> 8))
            {
                InsertAfterCurrentOp(current, NoOp);
                current.Pipeline = [.. current.Pipeline, completeOp];
            }
            else
            {
                InsertAfterCurrentOp(current, completeOp);
            }
        }
        else
        {
            current.PC++;
            current.InstructionComplete = true;
        }
    };

    /// <summary>BBS - Branch on Bit Set.</summary>
    public static MicroOp BbsOp(int bit) => (prev, current, bus) =>
    {
        byte mask = (byte)(1 << bit);
        if ((TempByte(current) & mask) != 0)
        {
            sbyte offset = (sbyte)bus.CpuRead(current.PC);
            current.PC++;
            ushort oldPC = current.PC;
            ushort newPC = (ushort)(current.PC + offset);
            current.PC = newPC;

            MicroOp completeOp = (_, n, _) => n.InstructionComplete = true;
            if ((oldPC >> 8) != (newPC >> 8))
            {
                InsertAfterCurrentOp(current, NoOp);
                current.Pipeline = [.. current.Pipeline, completeOp];
            }
            else
            {
                InsertAfterCurrentOp(current, completeOp);
            }
        }
        else
        {
            current.PC++;
            current.InstructionComplete = true;
        }
    };

    // ========================================
    // Illegal/Undocumented NMOS 6502 Operations
    // ========================================

    /// <summary>LAX - Load A and X with the same value.</summary>
    public static readonly MicroOp LaxOp = (prev, current, bus) =>
    {
        byte m = TempByte(current);
        current.A = m;
        current.X = m;
        SetNZ(current, m);
    };

    /// <summary>SAX - Store A AND X to memory.</summary>
    public static readonly MicroOp SaxOp = (prev, current, bus) =>
    {
        SetTempByte(current, (byte)(current.A & current.X));
    };

    /// <summary>DCP - Decrement memory then Compare with A.</summary>
    public static readonly MicroOp DcpOp = (prev, current, bus) =>
    {
        byte m = (byte)(TempByte(current) - 1);
        SetTempByte(current, m);
        int result = current.A - m;
        current.CarryFlag = current.A >= m;
        current.ZeroFlag = (result & 0xFF) == 0;
        current.NegativeFlag = (result & 0x80) != 0;
    };

    /// <summary>ISC/ISB - Increment memory then Subtract from A.</summary>
    public static readonly MicroOp IscOp = (prev, current, bus) =>
    {
        byte m = (byte)(TempByte(current) + 1);
        SetTempByte(current, m);
        if (current.DecimalFlag)
        {
            SbcDecimalNmos(prev, current, bus);
        }
        else
        {
            SbcBinary(prev, current, bus);
        }
    };

    /// <summary>SLO - Shift Left memory then OR with A.</summary>
    public static readonly MicroOp SloOp = (prev, current, bus) =>
    {
        byte m = TempByte(current);
        current.CarryFlag = (m & 0x80) != 0;
        byte shifted = (byte)(m << 1);
        SetTempByte(current, shifted);
        current.A = (byte)(current.A | shifted);
        SetNZ(current, current.A);
    };

    /// <summary>RLA - Rotate Left memory then AND with A.</summary>
    public static readonly MicroOp RlaOp = (prev, current, bus) =>
    {
        byte m = TempByte(current);
        byte oldCarry = (byte)(current.CarryFlag ? 1 : 0);
        current.CarryFlag = (m & 0x80) != 0;
        byte rotated = (byte)((m << 1) | oldCarry);
        SetTempByte(current, rotated);
        current.A = (byte)(current.A & rotated);
        SetNZ(current, current.A);
    };

    /// <summary>SRE - Shift Right memory then EOR with A.</summary>
    public static readonly MicroOp SreOp = (prev, current, bus) =>
    {
        byte m = TempByte(current);
        current.CarryFlag = (m & 0x01) != 0;
        byte shifted = (byte)(m >> 1);
        SetTempByte(current, shifted);
        current.A = (byte)(current.A ^ shifted);
        SetNZ(current, current.A);
    };

    /// <summary>RRA - Rotate Right memory then ADC with A.</summary>
    public static readonly MicroOp RraOp = (prev, current, bus) =>
    {
        byte m = TempByte(current);
        byte oldCarry = (byte)(current.CarryFlag ? 0x80 : 0);
        current.CarryFlag = (m & 0x01) != 0;
        byte rotated = (byte)((m >> 1) | oldCarry);
        SetTempByte(current, rotated);
        if (current.DecimalFlag)
        {
            AdcDecimalNmos(prev, current, bus);
        }
        else
        {
            AdcBinary(prev, current, bus);
        }
    };

    /// <summary>ANC - AND with immediate, then copy N to C.</summary>
    public static readonly MicroOp AncOp = (prev, current, bus) =>
    {
        current.A = (byte)(current.A & TempByte(current));
        SetNZ(current, current.A);
        current.CarryFlag = current.NegativeFlag;
    };

    /// <summary>ALR/ASR - AND with immediate, then LSR A.</summary>
    public static readonly MicroOp AlrOp = (prev, current, bus) =>
    {
        current.A = (byte)(current.A & TempByte(current));
        current.CarryFlag = (current.A & 0x01) != 0;
        current.A = (byte)(current.A >> 1);
        SetNZ(current, current.A);
    };

    /// <summary>ARR - AND with immediate, then ROR A with special flag behavior.</summary>
    /// <remarks>
    /// In binary mode: A = (A AND imm) ROR 1, with C from bit 6 and V from bit 6 XOR bit 5.
    /// In decimal mode: Additional BCD correction is applied after the ROR.
    /// </remarks>
    public static readonly MicroOp ArrOp = (prev, current, bus) =>
    {
        byte operand = TempByte(current);
        byte andResult = (byte)(current.A & operand);
        byte oldCarry = (byte)(current.CarryFlag ? 0x80 : 0);
        byte rorResult = (byte)((andResult >> 1) | oldCarry);

        // N and Z are set based on ROR result
        SetNZ(current, rorResult);

        // C is set from bit 6 of the ROR result
        current.CarryFlag = (rorResult & 0x40) != 0;

        // V is set from bit 6 XOR bit 5 of the ROR result
        current.OverflowFlag = ((rorResult & 0x40) ^ ((rorResult & 0x20) << 1)) != 0;

        if (current.DecimalFlag)
        {
            // Decimal mode: BCD correction based on the AND result (before ROR)
            byte result = rorResult;

            // Low nibble fixup: if low nibble of AND result >= 5, add 6 to low nibble
            if ((andResult & 0x0F) >= 0x05)
            {
                result = (byte)((result & 0xF0) | ((result + 0x06) & 0x0F));
            }

            // High nibble fixup: if high nibble of AND result >= 0x50, add 0x60 and set carry
            if ((andResult & 0xF0) >= 0x50)
            {
                result = (byte)(result + 0x60);
                current.CarryFlag = true;
            }

            current.A = result;
        }
        else
        {
            current.A = rorResult;
        }
    };

    /// <summary>XAA/ANE - Unstable opcode: A = (A | magic) AND X AND imm.</summary>
    public static readonly MicroOp XaaOp = (prev, current, bus) =>
    {
        current.A = (byte)((current.A | 0xEE) & current.X & TempByte(current));
        SetNZ(current, current.A);
    };

    /// <summary>AXS/SBX - X = (A AND X) - imm, no borrow.</summary>
    public static readonly MicroOp AxsOp = (prev, current, bus) =>
    {
        int result = (current.A & current.X) - TempByte(current);
        current.CarryFlag = result >= 0;
        current.X = (byte)result;
        SetNZ(current, current.X);
    };

    /// <summary>LAS - A, X, SP = memory AND SP.</summary>
    public static readonly MicroOp LasOp = (prev, current, bus) =>
    {
        byte result = (byte)(TempByte(current) & current.SP);
        current.A = result;
        current.X = result;
        current.SP = result;
        SetNZ(current, result);
    };

    /// <summary>SHA/AHX - Store A AND X AND (high byte + 1).</summary>
    public static readonly MicroOp ShaOp = (prev, current, bus) =>
    {
        byte baseHigh = (byte)(current.TempValue >> 8);
        byte value = (byte)(current.A & current.X & (baseHigh + 1));
        SetTempByte(current, value);
    };

    /// <summary>SHX/SXA - Store X AND (high byte + 1).</summary>
    public static readonly MicroOp ShxOp = (prev, current, bus) =>
    {
        byte baseHigh = (byte)(current.TempValue >> 8);
        byte value = (byte)(current.X & (baseHigh + 1));
        SetTempByte(current, value);
    };

    /// <summary>SHY/SYA - Store Y AND (high byte + 1).</summary>
    public static readonly MicroOp ShyOp = (prev, current, bus) =>
    {
        byte baseHigh = (byte)(current.TempValue >> 8);
        byte value = (byte)(current.Y & (baseHigh + 1));
        SetTempByte(current, value);
    };

    /// <summary>TAS/SHS - SP = A AND X, then store (A AND X AND (high + 1)).</summary>
    public static readonly MicroOp TasOp = (prev, current, bus) =>
    {
        current.SP = (byte)(current.A & current.X);
        byte baseHigh = (byte)(current.TempValue >> 8);
        byte value = (byte)(current.A & current.X & (baseHigh + 1));
        SetTempByte(current, value);
    };

    /// <summary>Write for unstable store opcodes - handles page-crossing address glitch.</summary>
    public static readonly MicroOp WriteUnstableStore = (prev, current, bus) =>
    {
        byte value = TempByte(current);
        byte baseHigh = (byte)(current.TempValue >> 8);
        byte finalHigh = (byte)(current.TempAddress >> 8);
        byte finalLow = (byte)(current.TempAddress & 0xFF);
        byte writeHigh = baseHigh != finalHigh ? (byte)(baseHigh & value) : finalHigh;
        ushort writeAddr = (ushort)((writeHigh << 8) | finalLow);
        bus.Write(writeAddr, value);
    };

    // ========================================
    // Halt Instructions
    // ========================================

    /// <summary>JAM/KIL - Set status to Jammed (or Bypassed if IgnoreHaltStopWait).</summary>
    public static readonly MicroOp JamOp = (prev, current, bus) =>
    {
        current.Status = current.IgnoreHaltStopWait ? CpuStatus.Bypassed : CpuStatus.Jammed;
    };

    /// <summary>STP - Stop the processor (or Bypassed if IgnoreHaltStopWait).</summary>
    public static readonly MicroOp StpOp = (prev, current, bus) =>
    {
        current.Status = current.IgnoreHaltStopWait ? CpuStatus.Bypassed : CpuStatus.Stopped;
    };

    /// <summary>WAI - Wait for interrupt (or Bypassed if IgnoreHaltStopWait).</summary>
    public static readonly MicroOp WaiOp = (prev, current, bus) =>
    {
        current.Status = current.IgnoreHaltStopWait ? CpuStatus.Bypassed : CpuStatus.Waiting;
    };

    // ========================================
    // Aliases for backward compatibility
    // ========================================

    /// <summary>AddXZeroPageWithDummyRead - alias for AddXZeroPage.</summary>
    public static readonly MicroOp AddXZeroPageWithDummyRead = AddXZeroPage;

    /// <summary>ANE/XAA - alias for XaaOp (same instruction, different name).</summary>
    public static readonly MicroOp AneOp = XaaOp;

    /// <summary>LXA/LAX imm - A = X = (A | magic) AND imm.</summary>
    public static readonly MicroOp LxaOp = (prev, current, bus) =>
    {
        byte result = (byte)((current.A | 0xEE) & TempByte(current));
        current.A = result;
        current.X = result;
        SetNZ(current, result);
    };
}
