// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Cpu;

/// <summary>
/// NMOS 6502 CPU with undocumented/illegal opcodes.
/// </summary>
public sealed class Cpu6502 : CpuBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Cpu6502"/> class.
    /// </summary>
    /// <remarks>
    /// Prefer using <see cref="CpuFactory.Create(CpuVariant, CpuState)"/> for state injection.
    /// </remarks>
        public Cpu6502() : base() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="Cpu6502"/> class with an injected state.
    /// </summary>
    /// <param name="state">The CPU state to use.</param>
    public Cpu6502(CpuState state) : base(state) { }

    /// <inheritdoc />
    public override CpuVariant Variant => CpuVariant.Nmos6502;

    /// <inheritdoc />
    protected override bool ClearDecimalOnInterrupt => false;
}
