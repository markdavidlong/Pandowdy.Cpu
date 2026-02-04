// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Runtime.CompilerServices;
using Pandowdy.Cpu.Internals;

namespace Pandowdy.Cpu;

/// <summary>
/// Base class for CPU variants that provides shared execution logic.
/// </summary>
/// <remarks>
/// <para>
/// The CPU receives its <see cref="CpuState"/> via dependency injection through the constructor.
/// The state is provided by the caller to <see cref="CpuFactory.Create(CpuVariant, CpuState)"/>,
/// allowing external components to share or pre-configure the state before injection.
/// </para>
/// <para>
/// For debugging scenarios that require tracking the previous instruction state, use
/// <see cref="DebugCpu"/> which wraps a CPU and maintains its own state history snapshot.
/// </para>
/// </remarks>
public abstract class CpuBase : IPandowdyCpu
{
    /// <summary>Memory address of the NMI vector ($FFFA-$FFFB).</summary>
    protected const ushort NmiVector = 0xFFFA;

    /// <summary>Memory address of the Reset vector ($FFFC-$FFFD).</summary>
    protected const ushort ResetVector = 0xFFFC;

    /// <summary>Memory address of the IRQ/BRK vector ($FFFE-$FFFF).</summary>
    protected const ushort IrqVector = 0xFFFE;

    /// <summary>
    /// The CPU state (injected via constructor).
    /// </summary>
    private CpuState _state;

    /// <summary>
    /// The pipeline table for the CPU variant.
    /// </summary>
    private readonly MicroOp[][] _pipelines;

    /// <summary>
    /// Indicates whether the D flag should be cleared on interrupts.
    /// </summary>
    protected readonly bool _clearDecimalOnInterrupt;

    /// <summary>
    /// Initializes a new instance of the <see cref="CpuBase"/> class with a new state.
    /// </summary>
    /// <remarks>
    /// This constructor creates a new <see cref="CpuState"/> internally. Prefer using
    /// <see cref="CpuFactory.Create(CpuVariant, CpuState)"/> for proper state injection.
    /// </remarks>
    protected CpuBase() : this(new CpuState())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CpuBase"/> class with an injected state.
    /// </summary>
    /// <param name="state">The CPU state to use. This state is not owned by the CPU
    /// and may be shared with other components.</param>
    protected CpuBase(CpuState state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _pipelines = Pipelines.GetPipelines(Variant);
        _clearDecimalOnInterrupt = ClearDecimalOnInterrupt;
    }

    /// <summary>
    /// Gets the CPU variant this instance emulates.
    /// </summary>
    public abstract CpuVariant Variant { get; }

    /// <summary>
    /// Gets a value indicating whether the D flag should be cleared on interrupts.
    /// </summary>
    protected abstract bool ClearDecimalOnInterrupt { get; }

    /// <summary>
    /// Gets or sets the CPU state.
    /// </summary>
    public CpuState State
    {
        get => _state;
        set => _state = value;
    }

    /// <summary>
    /// Executes a single clock cycle.
    /// </summary>
    /// <param name="bus">The memory/IO bus.</param>
    /// <returns><c>true</c> if an instruction completed this cycle; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// This method is not virtual to allow JIT inlining. All CPU variants share the same
    /// execution logic; only the pipeline tables differ (selected at construction time).
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Clock(IPandowdyCpuBus bus)
    {
        var state = _state;

        // Check if CPU is halted (Stopped=1, Jammed=2, or Waiting=3)
        // This single range check replaces three separate comparisons.
        // Running=0 and Bypassed=4 are the non-halted states.
        var status = state.Status;
        if ((uint)(status - CpuStatus.Stopped) <= (uint)(CpuStatus.Waiting - CpuStatus.Stopped))
        {
            return true;
        }

        // Check if previous instruction completed - if so, reset for new instruction
        if (state.InstructionComplete)
        {
            ResetPipelineForNewInstruction(state, bus);
        }
        // Also check if pipeline is empty (initial state after Reset)
        else if (state.EffectivePipelineLength == 0)
        {
            ResetPipelineForNewInstruction(state, bus);
        }

        var microOp = state.GetPipelineOp(state.PipelineIndex);
        microOp(state, state, bus);  // Pass state for both prev and current (prev is unused)
        state.PipelineIndex++;

        return state.InstructionComplete;
    }

    /// <summary>
    /// Resets the pipeline state for a new instruction.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ResetPipelineForNewInstruction(CpuState state, IPandowdyCpuBus bus)
    {
        // Use Peek to determine the pipeline without recording a bus cycle.
        // The actual opcode fetch (with cycle tracking) happens in FetchOpcode.
        byte opcode = bus.Peek(state.PC);
        state.Pipeline = _pipelines[opcode];
        state.PipelineIndex = 0;
        state.WorkingPipelineLength = 0;
        state.InstructionComplete = false;
    }

    /// <summary>
    /// Executes micro-ops until an instruction completes.
    /// </summary>
    /// <param name="bus">The memory/IO bus.</param>
    /// <returns>The number of clock cycles consumed.</returns>
    public virtual int Step(IPandowdyCpuBus bus)
    {
        int cycles = 0;
        bool complete = false;
        const int maxCycles = 100; // Safety limit - no 6502 instruction should take this long

        while (!complete && cycles < maxCycles)
        {
            complete = Clock(bus);
            cycles++;
        }

        return cycles;
    }

    /// <summary>
    /// Runs the CPU for a specified number of clock cycles.
    /// </summary>
    /// <param name="bus">The memory/IO bus.</param>
    /// <param name="maxCycles">The maximum number of cycles to execute.</param>
    /// <returns>The number of clock cycles consumed.</returns>
    public virtual int Run(IPandowdyCpuBus bus, int maxCycles)
    {
        int cycles = 0;

        while (cycles < maxCycles)
        {
            Clock(bus);
            cycles++;
        }

        return cycles;
    }

    /// <summary>
    /// Resets the CPU to its initial state and loads the reset vector.
    /// </summary>
    /// <param name="bus">The memory/IO bus.</param>
    public virtual void Reset(IPandowdyCpuBus bus)
    {
        _state.Reset();
        LoadResetVector(bus);
    }

    /// <summary>
    /// Loads the reset vector into the program counter.
    /// </summary>
    private void LoadResetVector(IPandowdyCpuBus bus)
    {
        byte low = bus.CpuRead(ResetVector);
        byte high = bus.CpuRead((ushort)(ResetVector + 1));
        _state.PC = (ushort)((high << 8) | low);
    }

    /// <summary>
    /// Handles an IRQ (Interrupt Request).
    /// </summary>
    /// <param name="bus">The bus interface for reading the vector and writing to the stack.</param>
    protected virtual void HandleIrq(IPandowdyCpuBus bus)
    {
        var state = _state;
        state.PendingInterrupt = PendingInterrupt.None;

        // Push PC high byte
        bus.Write((ushort)(0x0100 + state.SP), (byte)(state.PC >> 8));
        state.SP--;

        // Push PC low byte
        bus.Write((ushort)(0x0100 + state.SP), (byte)(state.PC & 0xFF));
        state.SP--;

        // Push P with B flag clear (IRQ clears B, U always set)
        byte statusToPush = (byte)((state.P | CpuState.FlagU) & ~CpuState.FlagB);
        bus.Write((ushort)(0x0100 + state.SP), statusToPush);
        state.SP--;

        // Set I flag
        state.P |= CpuState.FlagI;

        // Clear D flag for 65C02
        if (_clearDecimalOnInterrupt)
        {
            state.DecimalFlag = false;
        }

        // Load PC from IRQ vector
        byte low = bus.CpuRead(IrqVector);
        byte high = bus.CpuRead((ushort)(IrqVector + 1));
        state.PC = (ushort)((high << 8) | low);

        // Clear pipeline and resume if waiting
        state.Pipeline = [];
        state.PipelineIndex = 0;
        state.InstructionComplete = false;
        if (state.Status == CpuStatus.Waiting)
        {
            state.Status = CpuStatus.Running;
        }
    }

    /// <summary>
    /// Handles an NMI (Non-Maskable Interrupt).
    /// </summary>
    /// <param name="bus">The bus interface for reading the vector and writing to the stack.</param>
    protected virtual void HandleNmi(IPandowdyCpuBus bus)
    {
        var state = _state;
        state.PendingInterrupt = PendingInterrupt.None;

        // Push PC high byte
        bus.Write((ushort)(0x0100 + state.SP), (byte)(state.PC >> 8));
        state.SP--;

        // Push PC low byte
        bus.Write((ushort)(0x0100 + state.SP), (byte)(state.PC & 0xFF));
        state.SP--;

        // Push P with B flag clear (NMI clears B, U always set)
        byte statusToPush = (byte)((state.P | CpuState.FlagU) & ~CpuState.FlagB);
        bus.Write((ushort)(0x0100 + state.SP), statusToPush);
        state.SP--;

        // Set I flag
        state.P |= CpuState.FlagI;

        // Clear D flag for 65C02
        if (_clearDecimalOnInterrupt)
        {
            state.DecimalFlag = false;
        }

        // Load PC from NMI vector
        byte low = bus.CpuRead(NmiVector);
        byte high = bus.CpuRead((ushort)(NmiVector + 1));
        state.PC = (ushort)((high << 8) | low);

        // Clear pipeline and resume if waiting
        state.Pipeline = [];
        state.PipelineIndex = 0;
        state.InstructionComplete = false;
        if (state.Status == CpuStatus.Waiting)
        {
            state.Status = CpuStatus.Running;
        }
    }

    /// <summary>
    /// Handles a hardware Reset.
    /// </summary>
    /// <param name="bus">The bus interface for reading the reset vector.</param>
    protected virtual void HandleReset(IPandowdyCpuBus bus)
    {
        var state = _state;
        state.PendingInterrupt = PendingInterrupt.None;

        // Reset registers
        state.A = 0;
        state.X = 0;
        state.Y = 0;
        state.P = CpuState.FlagU | CpuState.FlagI;  // Unused always set, IRQ disabled
        state.SP = 0xFD;          // Stack pointer after reset sequence

        state.TempAddress = 0;
        state.TempValue = 0;
        state.CurrentOpcode = 0;
        state.OpcodeAddress = 0;

        // Load PC from reset vector
        byte low = bus.CpuRead(ResetVector);
        byte high = bus.CpuRead((ushort)(ResetVector + 1));
        state.PC = (ushort)((high << 8) | low);

        // Clear pipeline and set to running
        state.Pipeline = [];
        state.PipelineIndex = 0;
        state.InstructionComplete = false;
        state.Status = CpuStatus.Running;
    }

    /// <summary>
    /// Signals an IRQ (Interrupt Request).
    /// </summary>
    public virtual void SignalIrq()
    {
        if (_state.PendingInterrupt == PendingInterrupt.None)
        {
            _state.PendingInterrupt = PendingInterrupt.Irq;
        }
    }

    /// <summary>
    /// Signals an NMI (Non-Maskable Interrupt).
    /// </summary>
    public virtual void SignalNmi()
    {
        _state.PendingInterrupt = PendingInterrupt.Nmi;
    }

    /// <summary>
    /// Signals a hardware Reset.
    /// </summary>
    public virtual void SignalReset()
    {
        _state.PendingInterrupt = PendingInterrupt.Reset;
    }

    /// <summary>
    /// Clears a pending IRQ signal.
    /// </summary>
    public virtual void ClearIrq()
    {
        if (_state.PendingInterrupt == PendingInterrupt.Irq)
        {
            _state.PendingInterrupt = PendingInterrupt.None;
        }
    }

    /// <summary>
    /// Checks for and handles any pending interrupt.
    /// </summary>
    /// <param name="bus">The bus interface for reading vectors and writing to the stack.</param>
    /// <returns><c>true</c> if an interrupt was handled; otherwise, <c>false</c>.</returns>
    public virtual bool HandlePendingInterrupt(IPandowdyCpuBus bus)
    {
        switch (_state.PendingInterrupt)
        {
            case PendingInterrupt.Reset:
                HandleReset(bus);
                return true;

            case PendingInterrupt.Nmi:
                HandleNmi(bus);
                return true;

            case PendingInterrupt.Irq:
                if (!_state.InterruptDisableFlag ||
                    _state.Status == CpuStatus.Waiting)
                {
                    HandleIrq(bus);
                    return true;
                }
                return false;

            default:
                return false;
        }
    }
}
