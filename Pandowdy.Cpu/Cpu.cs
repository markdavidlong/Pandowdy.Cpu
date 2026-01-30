// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.Cpu.Internals;

namespace Pandowdy.Cpu;

/// <summary>
/// CPU execution engine using micro-op pipeline architecture.
/// </summary>
/// <remarks>
/// <para>
/// The CPU executes instructions by decomposing them into sequences of micro-operations,
/// where each micro-op represents a single clock cycle. This enables cycle-accurate emulation.
/// </para>
/// <para>
/// The execution model uses a double-buffered state pattern via <see cref="CpuStateBuffer"/>:
/// <list type="bullet">
///   <item><description><c>Prev</c>: The CPU state at the start of the instruction (before execution).</description></item>
///   <item><description><c>Current</c>: The CPU state during and after execution.</description></item>
/// </list>
/// At the start of each new instruction cycle, <c>Current</c> is copied to <c>Prev</c>, preserving
/// the "before" state. After <c>Step()</c> returns, <c>Prev</c> = before, <c>Current</c> = after.
/// </para>
/// </remarks>
public static class Cpu
{
    /// <summary>
    /// Executes a single clock cycle.
    /// </summary>
    /// <param name="variant">The CPU variant to emulate.</param>
    /// <param name="buffer">The CPU state buffer.</param>
    /// <param name="bus">The memory/IO bus.</param>
    /// <returns><c>true</c> if an instruction completed this cycle; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// <para>
    /// If the CPU is halted (Stopped, Jammed, or Waiting), this method returns <c>true</c>
    /// without advancing the PC or executing any micro-ops.
    /// </para>
    /// <para>
    /// On the first cycle of a new instruction, the opcode is peeked (without recording a bus cycle)
    /// to determine the pipeline. The actual opcode fetch is performed by the first micro-op.
    /// </para>
    /// </remarks>
    public static bool Clock(CpuVariant variant, CpuStateBuffer buffer, IPandowdyCpuBus bus)
    {
        var current = buffer.Current;

        // Check if CPU is halted (Stopped, Jammed, or Waiting)
        // If halted, return true (instruction complete) but don't advance PC
        // Note: Bypassed status means the CPU is still running (halt was bypassed)
        if (current.Status == CpuStatus.Stopped ||
            current.Status == CpuStatus.Jammed ||
            current.Status == CpuStatus.Waiting)
        {
            return true;
        }

        // Running or Bypassed - continue execution
        var prev = buffer.Prev;

        if (current.Pipeline.Length == 0 || current.PipelineIndex >= current.Pipeline.Length)
        {
            // Beginning of a new instruction - save current state to Prev
            buffer.SwapStateAndResetPipeline();

            // Use Peek to determine the pipeline without recording a bus cycle.
            // The actual opcode fetch (with cycle tracking) happens in FetchOpcode.
            byte opcode = bus.Peek(current.PC);
            var pipelines = Pipelines.GetPipelines(variant);
            current.Pipeline = pipelines[opcode];
            current.PipelineIndex = 0;
            current.InstructionComplete = false;
        }

        var microOp = current.Pipeline[current.PipelineIndex];
        microOp(prev, current, bus);
        current.PipelineIndex++;

        if (current.InstructionComplete)
        {

            return true;
        }

        return false;
    }

    /// <summary>
    /// Executes micro-ops until an instruction completes.
    /// </summary>
    /// <param name="variant">The CPU variant to emulate.</param>
    /// <param name="buffer">The CPU state buffer.</param>
    /// <param name="bus">The memory/IO bus.</param>
    /// <returns>The number of clock cycles consumed.</returns>
    /// <remarks>
    /// This method calls <see cref="Clock"/> repeatedly until an instruction completes.
    /// A safety limit of 100 cycles prevents infinite loops for malformed pipelines.
    /// </remarks>
    public static int Step(CpuVariant variant, CpuStateBuffer buffer, IPandowdyCpuBus bus)
    {
        int cycles = 0;
        bool complete = false;
        const int maxCycles = 100; // Safety limit - no 6502 instruction should take this long

        while (!complete && cycles < maxCycles)
        {
            complete = Clock(variant, buffer, bus);
            cycles++;
        }

        return cycles;
    }

    /// <summary>
    /// Runs the CPU for a specified number of clock cycles.
    /// </summary>
    /// <param name="variant">The CPU variant to emulate.</param>
    /// <param name="buffer">The CPU state buffer.</param>
    /// <param name="bus">The memory/IO bus.</param>
    /// <param name="maxCycles">The maximum number of cycles to execute.</param>
    /// <returns>The number of clock cycles consumed (always equals <paramref name="maxCycles"/>).</returns>
    /// <remarks>
    /// Unlike <see cref="Step"/>, this method does not stop at instruction boundaries.
    /// It simply executes the specified number of clock cycles.
    /// </remarks>
    public static int Run(CpuVariant variant, CpuStateBuffer buffer, IPandowdyCpuBus bus, int maxCycles)
    {
        int cycles = 0;

        while (cycles < maxCycles)
        {
            Clock(variant, buffer, bus);
            cycles++;
        }

        return cycles;
    }

    /// <summary>
    /// Resets the CPU to its initial state and loads the reset vector.
    /// </summary>
    /// <param name="buffer">The CPU state buffer.</param>
    /// <param name="bus">The memory/IO bus.</param>
    /// <remarks>
    /// This method:
    /// <list type="number">
    ///   <item><description>Resets all CPU state to default values.</description></item>
    ///   <item><description>Loads the reset vector from $FFFC-$FFFD into PC.</description></item>
    /// </list>
    /// </remarks>
    public static void Reset(CpuStateBuffer buffer, IPandowdyCpuBus bus)
    {
        buffer.Reset();
        buffer.LoadResetVector(bus);
    }

    /// <summary>
    /// Gets the opcode of the currently executing instruction, or the next instruction if complete.
    /// </summary>
    /// <param name="buffer">The CPU state buffer.</param>
    /// <param name="bus">The memory/IO bus.</param>
    /// <returns>The opcode byte.</returns>
    /// <remarks>
    /// <para>
    /// If an instruction is in progress (pipeline started but not complete), reads from <c>Prev.PC</c>
    /// to return the opcode of the instruction being executed.
    /// </para>
    /// <para>
    /// If the instruction is complete or no instruction is in progress, reads from <c>Current.PC</c>
    /// to return the opcode of the next instruction to be executed.
    /// </para>
    /// </remarks>
    public static byte CurrentOpcode(CpuStateBuffer buffer, IPandowdyCpuBus bus)
    {
        // If instruction is complete, return the next opcode (at Current.PC)
        if (buffer.Current.InstructionComplete)
        {
            return bus.CpuRead(buffer.Current.PC);
        }

        // If an instruction is in progress, return the opcode being executed (at Prev.PC)
        if (buffer.Current.Pipeline.Length > 0 && buffer.Current.PipelineIndex > 0)
        {
            return bus.CpuRead(buffer.Prev.PC);
        }

        // No instruction in progress, return opcode at Current.PC
        return bus.CpuRead(buffer.Current.PC);
    }

    /// <summary>
    /// Gets the number of clock cycles remaining in the current instruction.
    /// </summary>
    /// <param name="buffer">The CPU state buffer.</param>
    /// <returns>The number of remaining cycles, or 0 if no instruction is in progress.</returns>
    public static int CyclesRemaining(CpuStateBuffer buffer)
    {
        return buffer.Current.Pipeline.Length - buffer.Current.PipelineIndex;
    }
}
