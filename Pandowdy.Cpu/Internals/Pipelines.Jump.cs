// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Cpu.Internals;

internal static partial class Pipelines
{
    // ========================================
    // JMP Pipelines
    // ========================================

    /// <summary>JMP abs (0x4C) - 3 cycles</summary>
    private static readonly MicroOp[] Jmp_Abs =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        (prev, current, bus) =>
        {
            MicroOps.FetchAddressHigh(prev, current, bus);
            MicroOps.JumpToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>JMP (ind) (0x6C) - 5 cycles - NMOS (with page boundary bug)</summary>
    private static readonly MicroOp[] Jmp_Ind_Nmos =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.ReadPointerLowAbs,
        (prev, current, bus) =>
        {
            MicroOps.ReadPointerHighAbsNMOS(prev, current, bus);
            MicroOps.JumpToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>JMP (ind) (0x6C) - 6 cycles - 65C02 (fixes page boundary bug)</summary>
    private static readonly MicroOp[] Jmp_Ind_65C02 =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.FetchAddressHigh,
        MicroOps.ReadPointerLowAbs,
        (prev, current, bus) =>
        {
            // T4: Check if pointer low byte is $FF (page boundary case)
            ushort pointerAddr = current.TempValue;
            if ((pointerAddr & 0xFF) == 0xFF)
            {
                // Page boundary: dummy read at buggy wrap address ($xx00)
                ushort buggyAddr = (ushort)(pointerAddr & 0xFF00);
                bus.CpuRead(buggyAddr);
            }
            else
            {
                // Normal case: read high byte from pointer+1
                byte hi = bus.CpuRead((ushort)(pointerAddr + 1));
                current.TempAddress = (ushort)(current.TempAddress | (hi << 8));
            }
        },
        (prev, current, bus) =>
        {
            // T5: Check if we need to read high byte (page boundary) or do dummy read
            ushort pointerAddr = current.TempValue;
            if ((pointerAddr & 0xFF) == 0xFF)
            {
                // Page boundary case: read high byte from correct address
                ushort correctAddr = (ushort)(pointerAddr + 1);
                byte hi = bus.CpuRead(correctAddr);
                current.TempAddress = (ushort)(current.TempAddress | (hi << 8));
            }
            else
            {
                // Normal case: dummy read at pointer+1
                bus.CpuRead((ushort)(pointerAddr + 1));
            }
            current.PC = current.TempAddress;
            current.InstructionComplete = true;
        }
    ];

    /// <summary>JMP (abs,X) (0x7C) - 6 cycles - 65C02 only</summary>
    private static readonly MicroOp[] Jmp_AbsX_Ind =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            // Fetch low byte and save this address for T3 dummy read
            ushort lowByteAddr = current.PC;
            byte lo = bus.CpuRead(current.PC);
            current.PC++;
            current.TempAddress = lo;
            current.TempValue = lowByteAddr;
        },
        (prev, current, bus) =>
        {
            // Fetch high byte
            byte hi = bus.CpuRead(current.PC);
            current.PC++;
            current.TempAddress = (ushort)(current.TempAddress | (hi << 8));
        },
        (prev, current, bus) =>
        {
            // Dummy read at low byte address (T3)
            bus.CpuRead(current.TempValue);
            // Now add X to form effective pointer address
            current.TempAddress = (ushort)(current.TempAddress + current.X);
        },
        MicroOps.ReadPointerLowAbs,
        (prev, current, bus) =>
        {
            MicroOps.ReadPointerHighAbs(prev, current, bus);
            MicroOps.JumpToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // JSR/RTS/RTI Pipelines
    // ========================================

    /// <summary>JSR - Jump to Subroutine (0x20) - 6 cycles</summary>
    private static readonly MicroOp[] Jsr =
    [
        MicroOps.FetchOpcode,
        MicroOps.FetchAddressLow,
        MicroOps.DummyStackRead,
        MicroOps.PushPCH,
        MicroOps.PushPCL,
        (prev, current, bus) =>
        {
            MicroOps.FetchAddressHigh(prev, current, bus);
            MicroOps.JumpToTempAddress(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>RTS - Return from Subroutine (0x60) - 6 cycles</summary>
    private static readonly MicroOp[] Rts =
    [
        MicroOps.FetchOpcode,
        MicroOps.DummyReadPC,
        MicroOps.DummyStackRead,
        MicroOps.PullPCL,
        MicroOps.PullPCH,
        (prev, current, bus) =>
        {
            // Cycle 6: Read from current PC (return address), THEN increment
            bus.CpuRead(current.PC);
            current.PC++;
            current.InstructionComplete = true;
        }
    ];

    /// <summary>RTI - Return from Interrupt (0x40) - 6 cycles</summary>
    private static readonly MicroOp[] Rti =
    [
        MicroOps.FetchOpcode,
        MicroOps.DummyReadPC,
        MicroOps.DummyStackRead,
        (prev, current, bus) => MicroOps.PullP(prev, current, bus),
        MicroOps.PullPCL,
        (prev, current, bus) =>
        {
            MicroOps.PullPCH(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];
}
