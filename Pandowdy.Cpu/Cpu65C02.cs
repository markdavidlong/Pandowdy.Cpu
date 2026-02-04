// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Cpu;

/// <summary>
/// WDC 65C02 CPU with CMOS enhancements.
/// </summary>
public sealed class Cpu65C02 : CpuBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Cpu65C02"/> class.
    /// </summary>
    /// <remarks>
    /// Prefer using <see cref="CpuFactory.Create(CpuVariant, CpuState)"/> for state injection.
    /// </remarks>
        public Cpu65C02() : base() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="Cpu65C02"/> class with an injected state.
    /// </summary>
    /// <param name="state">The CPU state to use.</param>
    public Cpu65C02(CpuState state) : base(state) { }

    /// <inheritdoc />
    public override CpuVariant Variant => CpuVariant.Wdc65C02;

    /// <inheritdoc />
    protected override bool ClearDecimalOnInterrupt => true;
}
