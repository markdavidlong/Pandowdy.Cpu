// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Cpu.Internals;

internal static partial class Pipelines
{
    // ========================================
    // Stack Pipelines
    // ========================================

    /// <summary>PHA - Push Accumulator (0x48) - 3 cycles</summary>
    private static readonly MicroOp[] Pha_Impl =
    [
        MicroOps.FetchOpcode,
        MicroOps.DummyReadPC,
        (prev, current, bus) =>
        {
            MicroOps.PushA(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>PLA - Pull Accumulator (0x68) - 4 cycles</summary>
    private static readonly MicroOp[] Pla_Impl =
    [
        MicroOps.FetchOpcode,
        MicroOps.DummyReadPC,
        MicroOps.DummyStackRead,
        (prev, current, bus) =>
        {
            MicroOps.PullA(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>PHP - Push Processor Status (0x08) - 3 cycles</summary>
    private static readonly MicroOp[] Php_Impl =
    [
        MicroOps.FetchOpcode,
        MicroOps.DummyReadPC,
        (prev, current, bus) =>
        {
            MicroOps.PushP(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>PLP - Pull Processor Status (0x28) - 4 cycles</summary>
    private static readonly MicroOp[] Plp_Impl =
    [
        MicroOps.FetchOpcode,
        MicroOps.DummyReadPC,
        MicroOps.DummyStackRead,
        (prev, current, bus) =>
        {
            MicroOps.PullP(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>PHX - Push X Register (0xDA) - 3 cycles - 65C02</summary>
    private static readonly MicroOp[] Phx_Impl =
    [
        MicroOps.FetchOpcode,
        MicroOps.DummyReadPC,
        (prev, current, bus) =>
        {
            MicroOps.PushX(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>PLX - Pull X Register (0xFA) - 4 cycles - 65C02</summary>
    private static readonly MicroOp[] Plx_Impl =
    [
        MicroOps.FetchOpcode,
        MicroOps.DummyReadPC,
        MicroOps.DummyStackRead,
        (prev, current, bus) =>
        {
            MicroOps.PullX(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>PHY - Push Y Register (0x5A) - 3 cycles - 65C02</summary>
    private static readonly MicroOp[] Phy_Impl =
    [
        MicroOps.FetchOpcode,
        MicroOps.DummyReadPC,
        (prev, current, bus) =>
        {
            MicroOps.PushY(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>PLY - Pull Y Register (0x7A) - 4 cycles - 65C02</summary>
    private static readonly MicroOp[] Ply_Impl =
    [
        MicroOps.FetchOpcode,
        MicroOps.DummyReadPC,
        MicroOps.DummyStackRead,
        (prev, current, bus) =>
        {
            MicroOps.PullY(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // Transfer Pipelines
    // ========================================

    /// <summary>TAX (0xAA) - 2 cycles</summary>
    private static readonly MicroOp[] Tax_Impl =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.DummyReadPC(prev, current, bus);
            MicroOps.Tax(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>TXA (0x8A) - 2 cycles</summary>
    private static readonly MicroOp[] Txa_Impl =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.DummyReadPC(prev, current, bus);
            MicroOps.Txa(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>TAY (0xA8) - 2 cycles</summary>
    private static readonly MicroOp[] Tay_Impl =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.DummyReadPC(prev, current, bus);
            MicroOps.Tay(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>TYA (0x98) - 2 cycles</summary>
    private static readonly MicroOp[] Tya_Impl =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.DummyReadPC(prev, current, bus);
            MicroOps.Tya(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>TSX (0xBA) - 2 cycles</summary>
    private static readonly MicroOp[] Tsx_Impl =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.DummyReadPC(prev, current, bus);
            MicroOps.Tsx(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>TXS (0x9A) - 2 cycles</summary>
    private static readonly MicroOp[] Txs_Impl =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.DummyReadPC(prev, current, bus);
            MicroOps.Txs(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    // ========================================
    // Flag Pipelines
    // ========================================

    /// <summary>CLC - Clear Carry (0x18) - 2 cycles</summary>
    private static readonly MicroOp[] Clc_Impl =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.DummyReadPC(prev, current, bus);
            MicroOps.Clc(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>SEC - Set Carry (0x38) - 2 cycles</summary>
    private static readonly MicroOp[] Sec_Impl =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.DummyReadPC(prev, current, bus);
            MicroOps.Sec(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>CLD - Clear Decimal (0xD8) - 2 cycles</summary>
    private static readonly MicroOp[] Cld_Impl =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.DummyReadPC(prev, current, bus);
            MicroOps.Cld(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>SED - Set Decimal (0xF8) - 2 cycles</summary>
    private static readonly MicroOp[] Sed_Impl =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.DummyReadPC(prev, current, bus);
            MicroOps.Sed(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>CLI - Clear Interrupt Disable (0x58) - 2 cycles</summary>
    private static readonly MicroOp[] Cli_Impl =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.DummyReadPC(prev, current, bus);
            MicroOps.Cli(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>SEI - Set Interrupt Disable (0x78) - 2 cycles</summary>
    private static readonly MicroOp[] Sei_Impl =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.DummyReadPC(prev, current, bus);
            MicroOps.Sei(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];

    /// <summary>CLV - Clear Overflow (0xB8) - 2 cycles</summary>
    private static readonly MicroOp[] Clv_Impl =
    [
        MicroOps.FetchOpcode,
        (prev, current, bus) =>
        {
            MicroOps.DummyReadPC(prev, current, bus);
            MicroOps.Clv(prev, current, bus);
            current.InstructionComplete = true;
        }
    ];
}
