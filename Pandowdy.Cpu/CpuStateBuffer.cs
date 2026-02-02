// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Runtime.CompilerServices;
using Pandowdy.Cpu.Internals;

namespace Pandowdy.Cpu;

/// <summary>
/// Provides double-buffered CPU state for clean instruction boundaries, debugging, and state comparison.
/// </summary>
/// <remarks>
/// <para>
/// The double-buffer architecture maintains two complete CPU states:
/// </para>
/// <list type="bullet">
///   <item>
///     <term><see cref="Prev"/></term>
///     <description>The state at the beginning of the current (or most recent) instruction. May be null in optimized mode.</description>
///   </item>
///   <item>
///     <term><see cref="Current"/></term>
///     <description>The state at the end of the current (or most recent) instruction.</description>
///   </item>
/// </list>
/// <para>
/// This design provides several benefits:
/// </para>
/// <list type="bullet">
///   <item><description><b>State comparison:</b> Compare Prev vs Current to see what changed during an instruction.</description></item>
///   <item><description><b>Debugging support:</b> Inspect the before/after state of each Step().</description></item>
///   <item><description><b>Clean boundaries:</b> Prev always reflects the state before the last executed instruction.</description></item>
/// </list>
/// <para>
/// <b>Performance Mode:</b>
/// </para>
/// <para>
/// When <paramref name="enablePrevState"/> is <c>false</c> in the constructor, the <see cref="Prev"/>
/// state is not allocated and no copying occurs at instruction boundaries. This significantly improves
/// performance for production use. For debugging scenarios, create a buffer with <c>enablePrevState: true</c>.
/// </para>
/// <para>
/// <b>Execution flow:</b>
/// </para>
/// <list type="number">
///   <item><description>At the start of a new instruction cycle, Current is copied to Prev (saving the "before" state) - only if Prev is enabled.</description></item>
///   <item><description>Micro-ops modify Current during execution.</description></item>
///   <item><description>When Step() returns, Prev = before, Current = after.</description></item>
///   <item><description>The pipeline state is preserved until the next instruction cycle begins.</description></item>
/// </list>
/// </remarks>
/// <seealso cref="CpuState"/>
public class CpuStateBuffer
{
    /// <summary>
    /// Gets the state from before the current (or most recent) instruction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// After a <see cref="IPandowdyCpu.Step"/> completes, this contains the CPU state as it was
    /// before the instruction executed. Compare with <see cref="Current"/> to see what changed.
    /// </para>
    /// <para>
    /// This property is <c>null</c> when the buffer was created with <c>enablePrevState: false</c>
    /// for performance optimization.
    /// </para>
    /// </remarks>
    public CpuState? Prev { get; private set; }

    /// <summary>
    /// Gets the state after the current (or most recent) instruction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This state is actively modified during instruction execution.
    /// After a <see cref="IPandowdyCpu.Step"/> completes, this contains the resulting CPU state.
    /// </para>
    /// </remarks>
    public CpuState Current { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the Prev state is enabled for this buffer.
    /// </summary>
    /// <remarks>
    /// When <c>false</c>, no state copying occurs at instruction boundaries, improving performance.
    /// </remarks>
    public bool PrevStateEnabled => Prev != null;

    /// <summary>
    /// Initializes a new instance of the <see cref="CpuStateBuffer"/> class with default CPU states.
    /// </summary>
    /// <param name="enablePrevState">
    /// If <c>true</c> (default), allocates and maintains the Prev state for debugging.
    /// If <c>false</c>, skips Prev state allocation and copying for maximum performance.
    /// </param>
    public CpuStateBuffer(bool enablePrevState = true)
    {
        Prev = enablePrevState ? new CpuState() : null;
        Current = new CpuState();
    }


    /// <summary>
    /// Commits the current instruction by swapping buffers and syncing essential state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When an instruction completes, this method:
    /// </para>
    /// <list type="number">
    ///   <item><description>Swaps the Prev and Current references (if Prev is enabled).</description></item>
    ///   <item><description>Copies only essential registers from new Prev to new Current (not temp/pipeline state).</description></item>
    ///   <item><description>Resets Current's pipeline state for the next instruction.</description></item>
    /// </list>
    /// <para>
    /// The swap-based approach is more efficient than full copy because instruction-specific
    /// temporary values (TempAddress, TempValue, etc.) don't need to be copied - they'll be
    /// overwritten during the next instruction anyway.
    /// </para>
    /// <para>
    /// If the instruction is not complete, this method does nothing.
    /// </para>
    /// <para>
    /// When <see cref="PrevStateEnabled"/> is <c>false</c>, the swap step is skipped entirely for performance.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SwapStateAndResetPipeline()
    {
        if (!Current.InstructionComplete)
        {
            return;
        }

        if (Prev != null)
        {
            // Swap references - new Prev now has the completed instruction's state
            (Prev, Current) = (Current, Prev);

            // Single 8-byte copy for all packed registers (A, X, Y, P, SP, PC, Status)
            // This replaces 7 individual field assignments with one 64-bit copy
            Current.Registers.Packed = Prev.Registers.Packed;
            Current.PendingInterrupt = Prev.PendingInterrupt;
        }

        // Reset Current's pipeline state for the next instruction
        Current.InstructionComplete = false;
        Current.PipelineIndex = 0;
        Current.Pipeline = [];
        Current.WorkingPipelineLength = 0;
    }

    /// <summary>
    /// Saves the current state to Prev before beginning a new instruction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Call this at the start of a new instruction (when PipelineIndex is 0 and Pipeline is empty).
    /// After this call:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Prev contains the state before the instruction executes (if enabled).</description></item>
    ///   <item><description>Current will be modified during instruction execution.</description></item>
    /// </list>
    /// <para>
    /// After the instruction completes and Step() returns, you can compare Prev (before) vs Current (after)
    /// to see what changed during the instruction.
    /// </para>
    /// <para>
    /// This method does nothing if <see cref="PrevStateEnabled"/> is <c>false</c>.
    /// </para>
    /// </remarks>
    public void SaveStateBeforeInstruction()
    {
        Prev?.CopyFrom(Current);
    }

    /// <summary>
    /// Resets both buffers to initial power-on state.
    /// </summary>
    /// <remarks>
    /// Calls <see cref="CpuState.Reset"/> on both Prev (if enabled) and Current states.
    /// Use this when performing a full CPU reset.
    /// </remarks>
    public void Reset()
    {
        Prev?.Reset();
        Current.Reset();
    }

    /// <summary>
    /// Loads the reset vector and initializes PC in both states.
    /// </summary>
    /// <param name="bus">The bus interface for reading the reset vector.</param>
    /// <remarks>
    /// Reads the 16-bit address from $FFFC-$FFFD and sets it as the PC
    /// in both Prev (if enabled) and Current states. Call this after <see cref="Reset"/>
    /// to complete the initialization.
    /// </remarks>
    public void LoadResetVector(IPandowdyCpuBus bus)
    {
        ushort resetVector = (ushort)(bus.CpuRead(0xFFFC) | (bus.CpuRead(0xFFFD) << 8));
        if (Prev != null)
        {
            Prev.PC = resetVector;
        }
        Current.PC = resetVector;
    }

    // ========================================
    // Debugger Helper Properties
    // Compare Prev vs Current to detect state changes
    // These properties require PrevStateEnabled to be true.
    // ========================================

    /// <summary>
    /// Gets a value indicating whether the Program Counter changed during the current instruction.
    /// </summary>
    /// <remarks>Returns <c>false</c> if Prev state is not enabled.</remarks>
    public bool PcChanged => Prev != null && Prev.PC != Current.PC;

    /// <summary>
    /// Gets a value indicating whether a JMP-style instruction occurred.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Returns true when PC changed to a non-sequential address.
    /// This is a heuristic and may require opcode inspection for accuracy.
    /// </para>
    /// <para>Returns <c>false</c> if Prev state is not enabled.</para>
    /// </remarks>
    public bool JumpOccurred
    {
        get
        {
            if (Prev == null || !Current.InstructionComplete)
            {
                return false;
            }
            // JMP is 3 bytes, so sequential would be Prev.PC + 3
            // This is a heuristic - actual jump detection may need opcode inspection
            int expected = Prev.PC + Current.Pipeline.Length;
            return Current.PC != expected && Current.PC != Prev.PC + 1;
        }
    }

    /// <summary>
    /// Gets a value indicating whether a branch instruction was taken.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Returns true when PC changed by a relative offset typical of branch instructions
    /// (within -128 to +127 bytes from the instruction after the branch).
    /// </para>
    /// <para>
    /// Note: This is a heuristic. For definitive branch detection, inspect the opcode.
    /// </para>
    /// <para>Returns <c>false</c> if Prev state is not enabled.</para>
    /// </remarks>
    public bool BranchOccurred
    {
        get
        {
            if (Prev == null || !Current.InstructionComplete)
            {
                return false;
            }
            // Branch instructions are 2 bytes when not taken
            // If taken, PC will differ from Prev.PC + 2
            return PcChanged && Math.Abs(Current.PC - Prev.PC) <= 129;
        }
    }

    /// <summary>
    /// Gets a value indicating whether a return instruction (RTS/RTI) occurred.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Returns true when SP increased (values were pulled from stack) and PC changed.
    /// This pattern matches RTS and RTI instructions.
    /// </para>
    /// <para>Returns <c>false</c> if Prev state is not enabled.</para>
    /// </remarks>
    public bool ReturnOccurred
    {
        get
        {
            if (Prev == null || !Current.InstructionComplete)
            {
                return false;
            }
            // Return instructions pop PC from stack, causing SP to increase
            return Current.SP > Prev.SP && PcChanged;
        }
    }

    /// <summary>
    /// Gets a value indicating whether an interrupt was triggered.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Returns true when SP decreased by 3 (PC and P pushed) and the I flag is now set.
    /// This pattern matches interrupt handling for NMI, IRQ, and BRK.
    /// </para>
    /// <para>
    /// Note: This also matches BRK instruction. Check the opcode for distinction.
    /// </para>
    /// <para>Returns <c>false</c> if Prev state is not enabled.</para>
    /// </remarks>
    public bool InterruptOccurred
    {
        get
        {
            if (Prev == null || !Current.InstructionComplete)
            {
                return false;
            }
            // Interrupts push PC and P, causing SP to decrease by 3
            return (Prev.SP - Current.SP) == 3 && Current.InterruptDisableFlag;
        }
    }

    /// <summary>
    /// Gets a value indicating whether a page boundary was crossed during addressing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Returns true when the high byte of TempAddress changed between Prev and Current.
    /// Page crossing typically occurs with indexed addressing (e.g., LDA $12F0,X with X=$20
    /// crosses from page $12 to page $13).
    /// </para>
    /// <para>
    /// Page crossing usually incurs a one-cycle penalty on read operations.
    /// </para>
    /// <para>Returns <c>false</c> if Prev state is not enabled.</para>
    /// </remarks>
    public bool PageCrossed
    {
        get
        {
            if (Prev == null)
            {
                return false;
            }
            // Page crossing typically adds a penalty cycle
            // Detected when high byte of effective address differs from base
            return (Prev.TempAddress >> 8) != (Current.TempAddress >> 8);
        }
    }

    /// <summary>
    /// Gets a value indicating whether stack activity occurred (push or pull).
    /// </summary>
    /// <remarks>Returns <c>false</c> if Prev state is not enabled.</remarks>
    public bool StackActivityOccurred => Prev != null && Prev.SP != Current.SP;

    /// <summary>
    /// Gets the change in stack pointer during the current instruction.
    /// </summary>
    /// <remarks>
    /// <para>Negative values indicate pushes (SP decreased), positive values indicate pulls (SP increased).</para>
    /// <para>Common values:</para>
    /// <list type="bullet">
    ///   <item><description>-1: Single push (PHA, PHP, etc.)</description></item>
    ///   <item><description>-2: JSR (pushes 2-byte return address)</description></item>
    ///   <item><description>-3: Interrupt/BRK (pushes PC and P)</description></item>
    ///   <item><description>+1: Single pull (PLA, PLP, etc.)</description></item>
    ///   <item><description>+2: RTS (pulls 2-byte return address)</description></item>
    ///   <item><description>+3: RTI (pulls P and PC)</description></item>
    /// </list>
    /// <para>Returns 0 if Prev state is not enabled.</para>
    /// </remarks>
    public int StackDelta => Prev != null ? Current.SP - Prev.SP : 0;

    /// <summary>
    /// Gets an enumerable of register names that changed during the current instruction.
    /// </summary>
    /// <remarks>
    /// <para>Returns the names of registers whose values differ between Prev and Current:</para>
    /// <list type="bullet">
    ///   <item><description>"A" - Accumulator</description></item>
    ///   <item><description>"X" - X Index Register</description></item>
    ///   <item><description>"Y" - Y Index Register</description></item>
    ///   <item><description>"P" - Processor Status Register</description></item>
    ///   <item><description>"SP" - Stack Pointer</description></item>
    ///   <item><description>"PC" - Program Counter</description></item>
    /// </list>
    /// <para>Useful for debugging to quickly identify what an instruction modified.</para>
    /// <para>Returns an empty enumerable if Prev state is not enabled.</para>
    /// </remarks>
    public IEnumerable<string> ChangedRegisters
    {
        get
        {
            if (Prev == null)
            {
                yield break;
            }

            if (Prev.A != Current.A)
            {
                yield return "A";
            }

            if (Prev.X != Current.X)
            {
                yield return "X";
            }

            if (Prev.Y != Current.Y)
            {
                yield return "Y";
            }

            if (Prev.P != Current.P)
            {
                yield return "P";
            }

            if (Prev.SP != Current.SP)
            {
                yield return "SP";
            }

            if (Prev.PC != Current.PC)
            {
                yield return "PC";
            }
        }
    }
}
