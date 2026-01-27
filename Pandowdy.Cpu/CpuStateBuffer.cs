namespace Pandowdy.Cpu;

/// <summary>
/// Provides double-buffered CPU state for clean instruction boundaries, debugging, and time-travel capabilities.
/// </summary>
/// <remarks>
/// <para>
/// The double-buffer architecture maintains two complete CPU states:
/// </para>
/// <list type="bullet">
///   <item>
///     <term><see cref="Prev"/></term>
///     <description>The committed state at the start of the current instruction. Read-only during execution.</description>
///   </item>
///   <item>
///     <term><see cref="Current"/></term>
///     <description>The working state being modified during instruction execution.</description>
///   </item>
/// </list>
/// <para>
/// This design provides several benefits:
/// </para>
/// <list type="bullet">
///   <item><description><b>Clean instruction boundaries:</b> State changes are atomic at instruction completion.</description></item>
///   <item><description><b>Debugging support:</b> Compare Prev vs Current to see what changed during an instruction.</description></item>
///   <item><description><b>Rollback capability:</b> Discard Current changes by copying from Prev.</description></item>
///   <item><description><b>Time-travel debugging:</b> Save/restore Prev state for reverse execution.</description></item>
/// </list>
/// <para>
/// <b>Execution flow:</b>
/// </para>
/// <list type="number">
///   <item><description>At instruction start, Current is copied from Prev.</description></item>
///   <item><description>Micro-ops modify Current while reading from Prev for original values.</description></item>
///   <item><description>When instruction completes, <see cref="SwapIfComplete"/> promotes Current to Prev.</description></item>
/// </list>
/// </remarks>
/// <seealso cref="CpuState"/>
public class CpuStateBuffer
{
    /// <summary>
    /// Gets the committed state from the previous instruction boundary.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This state represents the CPU as it was at the start of the current instruction.
    /// Micro-operations read from this state when they need the "before" value of a register.
    /// </para>
    /// <para>
    /// Do not modify this state during instruction execution. It is updated automatically
    /// when <see cref="SwapIfComplete"/> is called after an instruction completes.
    /// </para>
    /// </remarks>
    public CpuState Prev { get; private set; }

    /// <summary>
    /// Gets the working state for the current instruction execution.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This state is actively modified during instruction execution.
    /// Micro-operations write their results here, building up the new CPU state.
    /// </para>
    /// <para>
    /// When <see cref="CpuState.InstructionComplete"/> is set to true and
    /// <see cref="SwapIfComplete"/> is called, this state becomes the new Prev.
    /// </para>
    /// </remarks>
    public CpuState Current { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CpuStateBuffer"/> class with default CPU states.
    /// </summary>
    public CpuStateBuffer()
    {
        Prev = new CpuState();
        Current = new CpuState();
    }

    /// <summary>
    /// Prepares for the next execution cycle by copying Prev state to Current.
    /// </summary>
    /// <remarks>
    /// Called at the start of a new instruction to initialize Current with the
    /// committed state from Prev. This ensures any partial changes from a previous
    /// aborted instruction are discarded.
    /// </remarks>
    public void PrepareNextCycle()
    {
        Current.CopyFrom(Prev);
    }

    /// <summary>
    /// Atomically commits the current instruction by swapping buffers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When an instruction completes (Current.InstructionComplete is true), this method:
    /// </para>
    /// <list type="number">
    ///   <item><description>Swaps the Prev and Current references.</description></item>
    ///   <item><description>Copies the new Prev to Current for the next instruction.</description></item>
    ///   <item><description>Resets Current's pipeline state for the next instruction.</description></item>
    /// </list>
    /// <para>
    /// If the instruction is not complete, this method does nothing.
    /// </para>
    /// </remarks>
    public void SwapIfComplete()
    {
        if (!Current.InstructionComplete)
        {
            return;
        }

        // Swap the references
        (Prev, Current) = (Current, Prev);

        // Reset the new Current for the next instruction
        Current.CopyFrom(Prev);
        Current.InstructionComplete = false;
        Current.PipelineIndex = 0;
        Current.Pipeline = [];
    }

    /// <summary>
    /// Resets both buffers to initial power-on state.
    /// </summary>
    /// <remarks>
    /// Calls <see cref="CpuState.Reset"/> on both Prev and Current states.
    /// Use this when performing a full CPU reset.
    /// </remarks>
    public void Reset()
    {
        Prev.Reset();
        Current.Reset();
    }

    /// <summary>
    /// Loads the reset vector and initializes PC in both states.
    /// </summary>
    /// <param name="bus">The bus interface for reading the reset vector.</param>
    /// <remarks>
    /// Reads the 16-bit address from $FFFC-$FFFD and sets it as the PC
    /// in both Prev and Current states. Call this after <see cref="Reset"/>
    /// to complete the initialization.
    /// </remarks>
    public void LoadResetVector(IPandowdyCpuBus bus)
    {
        ushort resetVector = (ushort)(bus.CpuRead(0xFFFC) | (bus.CpuRead(0xFFFD) << 8));
        Prev.PC = resetVector;
        Current.PC = resetVector;
    }

    // ========================================
    // Debugger Helper Properties
    // Compare Prev vs Current to detect state changes
    // ========================================

    /// <summary>
    /// Gets a value indicating whether the Program Counter changed during the current instruction.
    /// </summary>
    public bool PcChanged => Prev.PC != Current.PC;

    /// <summary>
    /// Gets a value indicating whether a JMP-style instruction occurred.
    /// </summary>
    /// <remarks>
    /// Returns true when PC changed to a non-sequential address.
    /// This is a heuristic and may require opcode inspection for accuracy.
    /// </remarks>
    public bool JumpOccurred
    {
        get
        {
            if (!Current.InstructionComplete)
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
    /// </remarks>
    public bool BranchOccurred
    {
        get
        {
            if (!Current.InstructionComplete)
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
    /// Returns true when SP increased (values were pulled from stack) and PC changed.
    /// This pattern matches RTS and RTI instructions.
    /// </remarks>
    public bool ReturnOccurred
    {
        get
        {
            if (!Current.InstructionComplete)
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
        /// </remarks>
        public bool InterruptOccurred
        {
            get
            {
                if (!Current.InstructionComplete)
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
        /// </remarks>
        public bool PageCrossed
        {
            get
            {
                // Page crossing typically adds a penalty cycle
                // Detected when high byte of effective address differs from base
                return (Prev.TempAddress >> 8) != (Current.TempAddress >> 8);
            }
        }

        /// <summary>
        /// Gets a value indicating whether stack activity occurred (push or pull).
        /// </summary>
        public bool StackActivityOccurred => Prev.SP != Current.SP;

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
        /// </remarks>
        public int StackDelta => Current.SP - Prev.SP;

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
        /// </remarks>
        public IEnumerable<string> ChangedRegisters
        {
            get
            {
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
