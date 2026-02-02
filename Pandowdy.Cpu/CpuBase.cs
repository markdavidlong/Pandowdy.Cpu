// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Runtime.CompilerServices;
using Pandowdy.Cpu.Internals;

namespace Pandowdy.Cpu;

/// <summary>
/// Base class for CPU variants that provides shared execution logic.
/// </summary>
public abstract class CpuBase : IPandowdyCpu
{
    /// <summary>Memory address of the NMI vector ($FFFA-$FFFB).</summary>
    protected const ushort NmiVector = 0xFFFA;

    /// <summary>Memory address of the Reset vector ($FFFC-$FFFD).</summary>
    protected const ushort ResetVector = 0xFFFC;

    /// <summary>Memory address of the IRQ/BRK vector ($FFFE-$FFFF).</summary>
    protected const ushort IrqVector = 0xFFFE;

    /// <summary>
    /// The state buffer used by this CPU instance.
    /// </summary>
    protected CpuStateBuffer _buffer;

    /// <summary>
    /// The pipeline table for the CPU variant.
    /// </summary>
    private readonly MicroOp[][] _pipelines;

    /// <summary>
    /// Indicates whether the D flag should be cleared on interrupts.
    /// </summary>
    protected readonly bool _clearDecimalOnInterrupt;

    /// <summary>
    /// Initializes a new instance of the <see cref="CpuBase"/> class.
    /// </summary>
    /// <param name="buffer">The state buffer to use.</param>
    protected CpuBase(CpuStateBuffer buffer)
    {
        _buffer = buffer;
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
    /// Gets or sets the state buffer used by this CPU instance.
    /// </summary>
    public CpuStateBuffer Buffer
    {
        get => _buffer;
        set => _buffer = value;
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
        var current = _buffer.Current;

        // Check if CPU is halted (Stopped=1, Jammed=2, or Waiting=3)
        // This single range check replaces three separate comparisons.
        // Running=0 and Bypassed=4 are the non-halted states.
        var status = current.Status;
        if ((uint)(status - CpuStatus.Stopped) <= (uint)(CpuStatus.Waiting - CpuStatus.Stopped))
        {
            return true;
        }

        // Running or Bypassed - continue execution
        // When Prev is null (performance mode), pass current as prev - micro-ops will still work
        var prev = _buffer.Prev ?? current;

        int effectiveLength = current.EffectivePipelineLength;
        if (effectiveLength == 0 || current.PipelineIndex >= effectiveLength)
        {
            // Beginning of a new instruction - save current state to Prev (if enabled)
            _buffer.SwapStateAndResetPipeline();

            // Use Peek to determine the pipeline without recording a bus cycle.
            // The actual opcode fetch (with cycle tracking) happens in FetchOpcode.
            byte opcode = bus.Peek(current.PC);
            current.Pipeline = _pipelines[opcode];
            current.PipelineIndex = 0;
            current.WorkingPipelineLength = 0;  // Reset working pipeline for new instruction
            current.InstructionComplete = false;
        }

        var microOp = current.GetPipelineOp(current.PipelineIndex);
        microOp(prev, current, bus);
        current.PipelineIndex++;

        return current.InstructionComplete;
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
        _buffer.Reset();
        _buffer.LoadResetVector(bus);
    }

    /// <summary>
    /// Handles an IRQ (Interrupt Request).
    /// </summary>
    /// <param name="bus">The bus interface for reading the vector and writing to the stack.</param>
    protected virtual void HandleIrq(IPandowdyCpuBus bus)
    {
        var current = _buffer.Current;
        current.PendingInterrupt = PendingInterrupt.None;

        // Push PC high byte
        bus.Write((ushort)(0x0100 + current.SP), (byte)(current.PC >> 8));
        current.SP--;

        // Push PC low byte
        bus.Write((ushort)(0x0100 + current.SP), (byte)(current.PC & 0xFF));
        current.SP--;

        // Push P with B flag clear (IRQ clears B, U always set)
        byte statusToPush = (byte)((current.P | CpuState.FlagU) & ~CpuState.FlagB);
        bus.Write((ushort)(0x0100 + current.SP), statusToPush);
        current.SP--;

        // Set I flag
        current.P |= CpuState.FlagI;

        // Clear D flag for 65C02
        if (_clearDecimalOnInterrupt)
        {
            current.DecimalFlag = false;
        }

        // Load PC from IRQ vector
        byte low = bus.CpuRead(IrqVector);
        byte high = bus.CpuRead((ushort)(IrqVector + 1));
        current.PC = (ushort)((high << 8) | low);

        // Clear pipeline and resume if waiting
        current.Pipeline = [];
        current.PipelineIndex = 0;
        current.InstructionComplete = false;
        if (current.Status == CpuStatus.Waiting)
        {
            current.Status = CpuStatus.Running;
        }
    }

    /// <summary>
    /// Handles an NMI (Non-Maskable Interrupt).
    /// </summary>
    /// <param name="bus">The bus interface for reading the vector and writing to the stack.</param>
    protected virtual void HandleNmi(IPandowdyCpuBus bus)
    {
        var current = _buffer.Current;
        current.PendingInterrupt = PendingInterrupt.None;

        // Push PC high byte
        bus.Write((ushort)(0x0100 + current.SP), (byte)(current.PC >> 8));
        current.SP--;

        // Push PC low byte
        bus.Write((ushort)(0x0100 + current.SP), (byte)(current.PC & 0xFF));
        current.SP--;

        // Push P with B flag clear (NMI clears B, U always set)
        byte statusToPush = (byte)((current.P | CpuState.FlagU) & ~CpuState.FlagB);
        bus.Write((ushort)(0x0100 + current.SP), statusToPush);
        current.SP--;

        // Set I flag
        current.P |= CpuState.FlagI;

        // Clear D flag for 65C02
        if (_clearDecimalOnInterrupt)
        {
            current.DecimalFlag = false;
        }

        // Load PC from NMI vector
        byte low = bus.CpuRead(NmiVector);
        byte high = bus.CpuRead((ushort)(NmiVector + 1));
        current.PC = (ushort)((high << 8) | low);

        // Clear pipeline and resume if waiting
        current.Pipeline = [];
        current.PipelineIndex = 0;
        current.InstructionComplete = false;
        if (current.Status == CpuStatus.Waiting)
        {
            current.Status = CpuStatus.Running;
        }
    }

    /// <summary>
    /// Handles a hardware Reset.
    /// </summary>
    /// <param name="bus">The bus interface for reading the reset vector.</param>
    protected virtual void HandleReset(IPandowdyCpuBus bus)
    {
        var current = _buffer.Current;
        current.PendingInterrupt = PendingInterrupt.None;

        // Reset registers
        current.A = 0;
        current.X = 0;
        current.Y = 0;
        current.P = CpuState.FlagU | CpuState.FlagI;  // Unused always set, IRQ disabled
        current.SP = 0xFD;          // Stack pointer after reset sequence

        current.TempAddress = 0;
        current.TempValue = 0;
        current.CurrentOpcode = 0;
        current.OpcodeAddress = 0;

        // Load PC from reset vector
        byte low = bus.CpuRead(ResetVector);
        byte high = bus.CpuRead((ushort)(ResetVector + 1));
        current.PC = (ushort)((high << 8) | low);

        // Clear pipeline and set to running
        current.Pipeline = [];
        current.PipelineIndex = 0;
        current.InstructionComplete = false;
        current.Status = CpuStatus.Running;
    }

    /// <summary>
    /// Signals an IRQ (Interrupt Request).
    /// </summary>
    public virtual void SignalIrq()
    {
        if (_buffer.Current.PendingInterrupt == PendingInterrupt.None)
        {
            _buffer.Current.PendingInterrupt = PendingInterrupt.Irq;
        }
    }

    /// <summary>
    /// Signals an NMI (Non-Maskable Interrupt).
    /// </summary>
    public virtual void SignalNmi()
    {
        _buffer.Current.PendingInterrupt = PendingInterrupt.Nmi;
    }

    /// <summary>
    /// Signals a hardware Reset.
    /// </summary>
    public virtual void SignalReset()
    {
        _buffer.Current.PendingInterrupt = PendingInterrupt.Reset;
    }

    /// <summary>
    /// Clears a pending IRQ signal.
    /// </summary>
    public virtual void ClearIrq()
    {
        if (_buffer.Current.PendingInterrupt == PendingInterrupt.Irq)
        {
            _buffer.Current.PendingInterrupt = PendingInterrupt.None;
        }
    }

    /// <summary>
    /// Checks for and handles any pending interrupt.
    /// </summary>
    /// <param name="bus">The bus interface for reading vectors and writing to the stack.</param>
    /// <returns><c>true</c> if an interrupt was handled; otherwise, <c>false</c>.</returns>
    public virtual bool HandlePendingInterrupt(IPandowdyCpuBus bus)
    {
        switch (_buffer.Current.PendingInterrupt)
        {
            case PendingInterrupt.Reset:
                HandleReset(bus);
                return true;

            case PendingInterrupt.Nmi:
                HandleNmi(bus);
                return true;

            case PendingInterrupt.Irq:
                if (!_buffer.Current.InterruptDisableFlag ||
                    _buffer.Current.Status == CpuStatus.Waiting)
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
