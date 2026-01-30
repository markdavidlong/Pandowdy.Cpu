// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Cpu.Internals;

/// <summary>
/// Represents a single micro-operation in the CPU pipeline.
/// </summary>
/// <param name="prev">The committed CPU state from the previous instruction boundary (read-only during execution).</param>
/// <param name="current">The working CPU state being modified during instruction execution.</param>
/// <param name="bus">The memory/IO bus interface for read/write operations.</param>
/// <remarks>
/// <para>
/// Micro-operations are the building blocks of CPU instruction execution. Each 6502 instruction
/// is decomposed into a sequence of micro-ops, with each micro-op representing one clock cycle.
/// </para>
/// <para>
/// The double-buffer architecture provides <paramref name="prev"/> for reading the original
/// register values and <paramref name="current"/> for writing the new values. This enables
/// clean instruction boundaries and debugging support.
/// </para>
/// </remarks>
internal delegate void MicroOp(CpuState prev, CpuState current, IPandowdyCpuBus bus);
