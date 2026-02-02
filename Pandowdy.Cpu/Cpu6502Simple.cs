// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Cpu;

/// <summary>
/// NMOS 6502 CPU with illegal opcodes treated as NOPs.
/// </summary>
public sealed class Cpu6502Simple : CpuBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Cpu6502Simple"/> class with a new state.
    /// </summary>
    public Cpu6502Simple() : base() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="Cpu6502Simple"/> class with an existing state.
    /// </summary>
    /// <param name="state">The CPU state to use.</param>
    public Cpu6502Simple(CpuState state) : base(state) { }

    /// <inheritdoc />
    public override CpuVariant Variant => CpuVariant.Nmos6502Simple;

    /// <inheritdoc />
    protected override bool ClearDecimalOnInterrupt => false;
}
