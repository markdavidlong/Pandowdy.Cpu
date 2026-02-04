// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Cpu;

/// <summary>
/// A debugging wrapper around a CPU that tracks state changes between instructions.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DebugCpu"/> decorates any <see cref="IPandowdyCpu"/> implementation to provide
/// debugging capabilities without impacting the performance of production code.
/// </para>
/// <para>
/// <b>Key Features:</b>
/// </para>
/// <list type="bullet">
///   <item><description>Automatically snapshots state before each instruction</description></item>
///   <item><description>Provides comparison helpers (PcChanged, BranchOccurred, etc.)</description></item>
///   <item><description>Tracks which registers changed during each instruction</description></item>
/// </list>
/// <para>
/// <b>Usage:</b>
/// </para>
/// <code>
/// var cpu = new Cpu65C02();
/// var debugCpu = new DebugCpu(cpu);
/// 
/// debugCpu.Reset(bus);
/// debugCpu.Step(bus);
/// 
/// if (debugCpu.BranchOccurred)
///     Console.WriteLine("Branch was taken!");
/// </code>
/// </remarks>
public class DebugCpu : IPandowdyCpu
{
    private readonly IPandowdyCpu _cpu;
    private CpuState? _prevState;
    private bool _instructionJustCompleted;

    /// <summary>
    /// Initializes a new instance of the <see cref="DebugCpu"/> class.
    /// </summary>
    /// <param name="cpu">The underlying CPU to wrap.</param>
    public DebugCpu(IPandowdyCpu cpu)
    {
        _cpu = cpu ?? throw new ArgumentNullException(nameof(cpu));
    }

    /// <summary>
    /// Gets the underlying CPU being debugged.
    /// </summary>
    public IPandowdyCpu UnderlyingCpu => _cpu;

    /// <summary>
    /// Gets the CPU variant this instance emulates.
    /// </summary>
    public CpuVariant Variant => _cpu.Variant;

    /// <summary>
    /// Gets or sets the current CPU state.
    /// </summary>
    public CpuState State
    {
        get => _cpu.State;
        set => _cpu.State = value;
    }

    /// <summary>
    /// Gets the state from before the most recently completed instruction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a snapshot taken at the start of the instruction. After <see cref="Step"/>
    /// completes, compare <see cref="PrevState"/> with <see cref="State"/> to see what changed.
    /// </para>
    /// <para>
    /// Returns <c>null</c> if no instruction has completed yet.
    /// </para>
    /// </remarks>
    public CpuState? PrevState => _prevState;

    /// <summary>
    /// Executes a single clock cycle.
    /// </summary>
    /// <param name="bus">The memory/IO bus.</param>
    /// <returns><c>true</c> if an instruction completed this cycle; otherwise, <c>false</c>.</returns>
    public bool Clock(IPandowdyCpuBus bus)
    {
        // Snapshot state at the beginning of a new instruction
        if (_instructionJustCompleted || _cpu.State.EffectivePipelineLength == 0)
        {
            _prevState = _cpu.State.Clone();
            _instructionJustCompleted = false;
        }

        bool completed = _cpu.Clock(bus);

        if (completed)
        {
            _instructionJustCompleted = true;
        }

        return completed;
    }

    /// <summary>
    /// Executes micro-ops until an instruction completes.
    /// </summary>
    /// <param name="bus">The memory/IO bus.</param>
    /// <returns>The number of clock cycles consumed.</returns>
    public int Step(IPandowdyCpuBus bus)
    {
        // Snapshot state before the instruction
        _prevState = _cpu.State.Clone();
        _instructionJustCompleted = false;

        int cycles = _cpu.Step(bus);

        _instructionJustCompleted = true;

        return cycles;
    }

    /// <summary>
    /// Runs the CPU for a specified number of clock cycles.
    /// </summary>
    /// <param name="bus">The memory/IO bus.</param>
    /// <param name="maxCycles">The maximum number of cycles to execute.</param>
    /// <returns>The number of clock cycles consumed.</returns>
    public int Run(IPandowdyCpuBus bus, int maxCycles)
    {
        return _cpu.Run(bus, maxCycles);
    }

    /// <summary>
    /// Resets the CPU to its initial state and loads the reset vector.
    /// </summary>
    /// <param name="bus">The memory/IO bus.</param>
    public void Reset(IPandowdyCpuBus bus)
    {
        _cpu.Reset(bus);
        _prevState = null;
        _instructionJustCompleted = false;
    }

    /// <summary>
    /// Signals an IRQ (Interrupt Request).
    /// </summary>
    public void SignalIrq() => _cpu.SignalIrq();

    /// <summary>
    /// Signals an NMI (Non-Maskable Interrupt).
    /// </summary>
    public void SignalNmi() => _cpu.SignalNmi();

    /// <summary>
    /// Signals a hardware Reset.
    /// </summary>
    public void SignalReset() => _cpu.SignalReset();

    /// <summary>
    /// Clears a pending IRQ signal.
    /// </summary>
    public void ClearIrq() => _cpu.ClearIrq();

    /// <summary>
    /// Checks for and handles any pending interrupt.
    /// </summary>
    /// <param name="bus">The bus interface for reading vectors and writing to the stack.</param>
    /// <returns><c>true</c> if an interrupt was handled; otherwise, <c>false</c>.</returns>
    public bool HandlePendingInterrupt(IPandowdyCpuBus bus) => _cpu.HandlePendingInterrupt(bus);

    // ========================================
    // Debugger Helper Properties
    // Compare PrevState vs State to detect state changes
    // ========================================

    /// <summary>
    /// Gets a value indicating whether the Program Counter changed during the current instruction.
    /// </summary>
    /// <remarks>Returns <c>false</c> if no instruction has completed yet.</remarks>
    public bool PcChanged => _prevState != null && _prevState.PC != State.PC;

    /// <summary>
    /// Gets a value indicating whether a JMP-style instruction occurred.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Returns true when PC changed to a non-sequential address.
    /// This is a heuristic and may require opcode inspection for accuracy.
    /// </para>
    /// <para>Returns <c>false</c> if no instruction has completed yet.</para>
    /// </remarks>
    public bool JumpOccurred
    {
        get
        {
            if (_prevState == null || !State.InstructionComplete)
            {
                return false;
            }
            // JMP is 3 bytes, so sequential would be PrevState.PC + 3
            // This is a heuristic - actual jump detection may need opcode inspection
            int expected = _prevState.PC + State.Pipeline.Length;
            return State.PC != expected && State.PC != _prevState.PC + 1;
        }
    }

    /// <summary>
    /// Gets a value indicating whether a branch instruction was taken.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Returns true when a branch opcode (BCC, BCS, BEQ, BMI, BNE, BPL, BVC, BVS, BRA)
    /// was executed AND the branch was taken (PC didn't advance sequentially).
    /// </para>
    /// <para>Returns <c>false</c> if no instruction has completed yet.</para>
    /// </remarks>
    public bool BranchOccurred
    {
        get
        {
            if (_prevState == null || !State.InstructionComplete)
            {
                return false;
            }

            // Check if the opcode is a branch instruction
            byte opcode = State.CurrentOpcode;
            bool isBranchOpcode = opcode switch
            {
                0x10 => true, // BPL
                0x30 => true, // BMI
                0x50 => true, // BVC
                0x70 => true, // BVS
                0x80 => true, // BRA (65C02)
                0x90 => true, // BCC
                0xB0 => true, // BCS
                0xD0 => true, // BNE
                0xF0 => true, // BEQ
                _ => false
            };

            if (!isBranchOpcode)
            {
                return false;
            }

            // Branch instruction: check if branch was taken (PC didn't advance by 2)
            // A not-taken branch advances PC by 2 (opcode + offset byte)
            int sequentialPc = _prevState.PC + 2;
            return State.PC != sequentialPc;
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
    /// <para>Returns <c>false</c> if no instruction has completed yet.</para>
    /// </remarks>
    public bool ReturnOccurred
    {
        get
        {
            if (_prevState == null || !State.InstructionComplete)
            {
                return false;
            }
            // Return instructions pop PC from stack, causing SP to increase
            return State.SP > _prevState.SP && PcChanged;
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
    /// <para>Returns <c>false</c> if no instruction has completed yet.</para>
    /// </remarks>
    public bool InterruptOccurred
    {
        get
        {
            if (_prevState == null || !State.InstructionComplete)
            {
                return false;
            }
            // Interrupts push PC and P, causing SP to decrease by 3
            return (_prevState.SP - State.SP) == 3 && State.InterruptDisableFlag;
        }
    }

    /// <summary>
    /// Gets a value indicating whether a page boundary was crossed during addressing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Returns true when the high byte of TempAddress changed between PrevState and State.
    /// Page crossing typically occurs with indexed addressing (e.g., LDA $12F0,X with X=$20
    /// crosses from page $12 to page $13).
    /// </para>
    /// <para>
    /// Page crossing usually incurs a one-cycle penalty on read operations.
    /// </para>
    /// <para>Returns <c>false</c> if no instruction has completed yet.</para>
    /// </remarks>
    public bool PageCrossed
    {
        get
        {
            if (_prevState == null)
            {
                return false;
            }
            // Page crossing typically adds a penalty cycle
            // Detected when high byte of effective address differs from base
            return (_prevState.TempAddress >> 8) != (State.TempAddress >> 8);
        }
    }

    /// <summary>
    /// Gets a value indicating whether stack activity occurred (push or pull).
    /// </summary>
    /// <remarks>Returns <c>false</c> if no instruction has completed yet.</remarks>
    public bool StackActivityOccurred => _prevState != null && _prevState.SP != State.SP;

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
    /// <para>Returns 0 if no instruction has completed yet.</para>
    /// </remarks>
    public int StackDelta => _prevState != null ? State.SP - _prevState.SP : 0;

    /// <summary>
    /// Gets an enumerable of register names that changed during the current instruction.
    /// </summary>
    /// <remarks>
    /// <para>Returns the names of registers whose values differ between PrevState and State:</para>
    /// <list type="bullet">
    ///   <item><description>"A" - Accumulator</description></item>
    ///   <item><description>"X" - X Index Register</description></item>
    ///   <item><description>"Y" - Y Index Register</description></item>
    ///   <item><description>"P" - Processor Status Register</description></item>
    ///   <item><description>"SP" - Stack Pointer</description></item>
    ///   <item><description>"PC" - Program Counter</description></item>
    /// </list>
    /// <para>Useful for debugging to quickly identify what an instruction modified.</para>
    /// <para>Returns an empty enumerable if no instruction has completed yet.</para>
    /// </remarks>
    public IEnumerable<string> ChangedRegisters
    {
        get
        {
            if (_prevState == null)
            {
                yield break;
            }

            if (_prevState.A != State.A)
            {
                yield return "A";
            }

            if (_prevState.X != State.X)
            {
                yield return "X";
            }

            if (_prevState.Y != State.Y)
            {
                yield return "Y";
            }

            if (_prevState.P != State.P)
            {
                yield return "P";
            }

            if (_prevState.SP != State.SP)
            {
                yield return "SP";
            }

            if (_prevState.PC != State.PC)
            {
                yield return "PC";
            }
        }
    }
}
