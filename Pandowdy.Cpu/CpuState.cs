// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Pandowdy.Cpu.Internals;

namespace Pandowdy.Cpu;

/// <summary>
/// Represents the execution status of the CPU as a byte-backed enum for efficient packing.
/// </summary>
/// <remarks>
/// <para>
/// The CPU can be in one of five states during execution:
/// </para>
/// <list type="bullet">
///   <item><description><see cref="Running"/>: Normal instruction execution.</description></item>
///   <item><description><see cref="Stopped"/>: Halted by STP instruction, requires hardware reset.</description></item>
///   <item><description><see cref="Jammed"/>: Frozen by illegal JAM/KIL opcode (NMOS only).</description></item>
///   <item><description><see cref="Waiting"/>: Suspended by WAI instruction, waiting for interrupt.</description></item>
///   <item><description><see cref="Bypassed"/>: A halt instruction was bypassed, CPU continues running.</description></item>
/// </list>
/// <para>
/// This enum is byte-backed to allow packing into <see cref="CpuRegisters"/> for efficient copying.
/// </para>
/// </remarks>
public enum CpuStatus : byte
{
    /// <summary>Normal execution mode - CPU is actively processing instructions.</summary>
    Running = 0,

    /// <summary>
    /// CPU halted by STP instruction. Only a hardware reset can resume execution.
    /// Available on 65C02 and later variants.
    /// </summary>
    Stopped = 1,

    /// <summary>
    /// CPU frozen by executing an illegal JAM/KIL opcode.
    /// Only available on NMOS 6502. Requires hardware reset to recover.
    /// </summary>
    Jammed = 2,

    /// <summary>
    /// CPU suspended by WAI instruction, waiting for an interrupt (IRQ, NMI, or Reset).
    /// When an interrupt occurs, the CPU resumes execution at the interrupt handler.
    /// Available on 65C02 and later variants.
    /// </summary>
    Waiting = 3,

    /// <summary>
    /// Indicates that a halt instruction (JAM, STP, or WAI) was encountered but bypassed
    /// because <see cref="CpuState.IgnoreHaltStopWait"/> was set to <c>true</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The CPU continues executing normally, but this status indicates that a halt
    /// instruction was bypassed. This is useful for debugging to detect when halt
    /// instructions are encountered without actually stopping execution.
    /// </para>
    /// <para>
    /// This status persists until <c>Cpu.Reset</c> is called or it is
    /// manually reset to <see cref="Running"/>.
    /// </para>
    /// </remarks>
    Bypassed = 4
}

/// <summary>
/// Packed CPU registers using explicit memory layout for efficient single-copy operations.
/// </summary>
/// <remarks>
/// <para>
/// This struct packs the essential CPU registers into exactly 8 bytes (one ulong) using
/// explicit field offsets. This allows copying all registers with a single 8-byte assignment
/// instead of multiple individual field copies.
/// </para>
/// <para>Memory layout:</para>
/// <list type="table">
///   <listheader><term>Offset</term><description>Field</description></listheader>
///   <item><term>0</term><description>A (1 byte) - Accumulator</description></item>
///   <item><term>1</term><description>X (1 byte) - X Index Register</description></item>
///   <item><term>2</term><description>Y (1 byte) - Y Index Register</description></item>
///   <item><term>3</term><description>P (1 byte) - Processor Status</description></item>
///   <item><term>4</term><description>SP (1 byte) - Stack Pointer</description></item>
///   <item><term>5-6</term><description>PC (2 bytes) - Program Counter</description></item>
///   <item><term>7</term><description>Status (1 byte) - CPU execution status</description></item>
/// </list>
/// </remarks>
[StructLayout(LayoutKind.Explicit, Size = 8)]
public struct CpuRegisters
{
    /// <summary>
    /// All registers packed into a single 64-bit value for efficient copying.
    /// </summary>
    [FieldOffset(0)]
    public ulong Packed;

    /// <summary>Accumulator register (A).</summary>
    [FieldOffset(0)]
    public byte A;

    /// <summary>X Index register.</summary>
    [FieldOffset(1)]
    public byte X;

    /// <summary>Y Index register.</summary>
    [FieldOffset(2)]
    public byte Y;

    /// <summary>Processor Status register (P).</summary>
    [FieldOffset(3)]
    public byte P;

    /// <summary>Stack Pointer register (SP).</summary>
    [FieldOffset(4)]
    public byte SP;

    /// <summary>Program Counter (PC).</summary>
    [FieldOffset(5)]
    public ushort PC;

    /// <summary>CPU execution status.</summary>
    [FieldOffset(7)]
    public CpuStatus Status;
}

/// <summary>
/// Represents pending interrupt signals checked at instruction boundaries.
/// </summary>
/// <remarks>
/// <para>
/// Interrupts are checked and serviced at instruction boundaries (after an instruction completes).
/// When multiple interrupts are pending, they are handled according to priority:
/// </para>
/// <list type="number">
///   <item><description><see cref="Reset"/>: Highest priority - reinitializes the CPU.</description></item>
///   <item><description><see cref="Nmi"/>: Non-maskable, cannot be disabled by the I flag.</description></item>
///   <item><description><see cref="Irq"/>: Lowest priority, ignored when I flag is set.</description></item>
/// </list>
/// </remarks>
public enum PendingInterrupt
{
    /// <summary>No interrupt pending.</summary>
    None,

    /// <summary>
    /// Interrupt Request pending. Will be serviced if the I (Interrupt Disable) flag is clear.
    /// Jumps to the address stored at $FFFE-$FFFF.
    /// </summary>
    Irq,

    /// <summary>
    /// Non-Maskable Interrupt pending. Cannot be disabled and always takes precedence over IRQ.
    /// Jumps to the address stored at $FFFA-$FFFB.
    /// </summary>
    Nmi,

    /// <summary>
    /// Hardware Reset pending. Highest priority interrupt that reinitializes the CPU.
    /// Loads the program counter from the reset vector at $FFFC-$FFFD.
    /// </summary>
    Reset
}

/// <summary>
/// Represents the complete state of a 6502/65C02 CPU including registers,
/// temporary values, and pipeline execution state.
/// </summary>
/// <remarks>
/// <para>
/// This class encapsulates all CPU state required for accurate emulation:
/// </para>
/// <list type="bullet">
///   <item><description><b>Programmer-visible registers:</b> A, X, Y, SP, PC, and status flags (P).</description></item>
///   <item><description><b>Execution state:</b> Pipeline, instruction completion, and CPU status.</description></item>
///   <item><description><b>Temporary values:</b> Address and data scratch registers used during instruction execution.</description></item>
///   <item><description><b>Interrupt state:</b> Pending interrupt signals awaiting service.</description></item>
/// </list>
/// <para>
/// The CPU uses a micro-op pipeline architecture where each instruction is broken into
/// cycle-accurate micro-operations. The <see cref="Pipeline"/> array contains these operations,
/// and <see cref="PipelineIndex"/> tracks the current execution position.
/// </para>
/// <para>
/// For debugging and state comparison, use <see cref="DebugCpu"/> which wraps any CPU
/// and tracks the previous state, allowing comparison of state changes per instruction.
/// </para>
/// </remarks>
/// <seealso cref="DebugCpu"/>
/// <seealso cref="CpuStatus"/>
/// <seealso cref="PendingInterrupt"/>
public class CpuState
{
    // ========================================
    // Packed Registers (for efficient copying)
    // ========================================

    /// <summary>
    /// Packed CPU registers for efficient single-copy operations at instruction boundaries.
    /// </summary>
    /// <remarks>
    /// This struct contains A, X, Y, P, SP, PC, and Status in a single 8-byte block
    /// that can be copied with a single 64-bit assignment.
    /// </remarks>
    internal CpuRegisters Registers;

    // ========================================
    // Programmer-Visible Register Properties
    // (delegate to packed Registers struct)
    // ========================================

    /// <summary>
    /// Gets or sets the Accumulator register (A).
    /// </summary>
    /// <remarks>
    /// The accumulator is the primary register for arithmetic and logic operations.
    /// Most ALU operations use the accumulator as both source and destination.
    /// </remarks>
    public byte A
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Registers.A;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => Registers.A = value;
    }

    /// <summary>
    /// Gets or sets the X Index register.
    /// </summary>
    /// <remarks>
    /// Used for indexed addressing modes (e.g., LDA $1234,X) and as a general-purpose counter.
    /// Some instructions like TAX, TXA, TXS, TSX, INX, DEX operate directly on X.
    /// </remarks>
    public byte X
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Registers.X;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => Registers.X = value;
    }

    /// <summary>
    /// Gets or sets the Y Index register.
    /// </summary>
    /// <remarks>
    /// Used for indexed addressing modes (e.g., LDA $1234,Y) and as a general-purpose counter.
    /// Some instructions like TAY, TYA, INY, DEY operate directly on Y.
    /// </remarks>
    public byte Y
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Registers.Y;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => Registers.Y = value;
    }

    /// <summary>
    /// Gets or sets the Processor Status register (P).
    /// </summary>
    /// <remarks>
    /// <para>Contains the CPU status flags in the following bit layout:</para>
    /// <list type="table">
    ///   <listheader><term>Bit</term><description>Flag</description></listheader>
    ///   <item><term>7</term><description>N - Negative</description></item>
    ///   <item><term>6</term><description>V - Overflow</description></item>
    ///   <item><term>5</term><description>U - Unused (always 1)</description></item>
    ///   <item><term>4</term><description>B - Break (only set when pushed to stack by BRK)</description></item>
    ///   <item><term>3</term><description>D - Decimal Mode</description></item>
    ///   <item><term>2</term><description>I - Interrupt Disable</description></item>
    ///   <item><term>1</term><description>Z - Zero</description></item>
    ///   <item><term>0</term><description>C - Carry</description></item>
    /// </list>
    /// <para>Use the convenience properties (e.g., <see cref="CarryFlag"/>, <see cref="ZeroFlag"/>) for easier access.</para>
    /// </remarks>
    public byte P
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Registers.P;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => Registers.P = value;
    }

    /// <summary>
    /// Gets or sets the Stack Pointer register (SP).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Points to the next free location on the stack. The stack occupies page 1 ($0100-$01FF)
    /// and grows downward. After reset, SP is initialized to $FD.
    /// </para>
    /// <para>
    /// Push operations write to $0100+SP then decrement SP.
    /// Pull operations increment SP then read from $0100+SP.
    /// </para>
    /// </remarks>
    public byte SP
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Registers.SP;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => Registers.SP = value;
    }

    /// <summary>
    /// Gets or sets the Program Counter (PC).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The 16-bit program counter points to the next instruction to be fetched.
    /// It is automatically incremented during instruction fetch and can be modified
    /// by jump, branch, call, and return instructions.
    /// </para>
    /// <para>
    /// After reset, PC is loaded from the reset vector at $FFFC-$FFFD.
    /// </para>
    /// </remarks>
    public ushort PC
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Registers.PC;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => Registers.PC = value;
    }

    // ========================================
    // Temporary Execution State
    // ========================================

    /// <summary>
    /// Gets or sets the temporary address register used during instruction execution.
    /// </summary>
    /// <remarks>
    /// Used internally to compute effective addresses for memory operations.
    /// This is not a programmer-visible register but is essential for accurate
    /// cycle-by-cycle emulation of addressing modes.
    /// </remarks>
    public ushort TempAddress { get; set; }

    /// <summary>
    /// Gets or sets the temporary value register used during instruction execution.
    /// </summary>
    /// <remarks>
    /// Holds intermediate data during instruction execution. The low byte is used
    /// for most operations; the full 16-bit value is used for indirect addressing.
    /// </remarks>
    public ushort TempValue { get; set; }

    /// <summary>
    /// Gets or sets the opcode byte currently being executed.
    /// </summary>
    /// <remarks>
    /// Set during instruction fetch. Useful for debugging and disassembly.
    /// Initialized to 0 in constructor and Reset().
    /// </remarks>
    public byte CurrentOpcode { get; set; }

    /// <summary>
    /// Gets or sets the address from which the current opcode was read.
    /// </summary>
    /// <remarks>
    /// Set during instruction fetch. This is the PC value at the start of the instruction,
    /// eliminating guesswork about the PC's relationship to the current instruction.
    /// Initialized to 0 in constructor and Reset().
    /// </remarks>
    public ushort OpcodeAddress { get; set; }

    /// <summary>
    /// Gets or sets the address to use for penalty cycle reads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When a page-crossing penalty cycle is needed, this field stores the "wrong" address
    /// (the incomplete/uncorrected address) that the 6502 reads before it has the correct
    /// high byte. This avoids allocating a closure to capture the address.
    /// </para>
    /// <para>
    /// Set by AddXCheckPage/AddYCheckPage before inserting a penalty cycle.
    /// Read by DummyReadPenaltyAddress during the penalty cycle.
    /// </para>
    /// </remarks>
    internal ushort PenaltyAddress { get; set; }

    /// <summary>
    /// Gets or sets the old PC for branch penalty cycles.
    /// </summary>
    /// <remarks>
    /// When a branch is taken, this stores the PC value before the branch offset was applied.
    /// Used by the branch penalty cycle micro-ops to avoid closure allocations.
    /// </remarks>
    internal ushort BranchOldPC { get; set; }

    // ========================================
    // Pipeline Execution State
    // ========================================

    /// <summary>
    /// Maximum capacity for the working pipeline buffer.
    /// </summary>
    /// <remarks>
    /// The longest 6502 instruction is 7 cycles (RMW absolute,X). With up to 2 penalty cycles
    /// for page crossing, we need at most 9 slots. 16 provides ample headroom.
    /// </remarks>
    internal const int WorkingPipelineCapacity = 16;

    /// <summary>
    /// Gets the working pipeline buffer for in-place modification.
    /// </summary>
    /// <remarks>
    /// This buffer is used when penalty cycles need to be inserted. The pipeline is copied
    /// here once, then subsequent insertions modify this buffer in-place without allocation.
    /// </remarks>
    internal MicroOp[] WorkingPipeline { get; } = new MicroOp[WorkingPipelineCapacity];

    /// <summary>
    /// Gets or sets the actual length of the working pipeline (0 if not using working pipeline).
    /// </summary>
    internal int WorkingPipelineLength { get; set; }

    /// <summary>
    /// Gets or sets the base pipeline array for the current instruction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each instruction is decomposed into cycle-accurate micro-operations.
    /// The pipeline is populated when an instruction is decoded and executed
    /// one micro-op per clock cycle.
    /// </para>
    /// <para>
    /// Each micro-op is a delegate that receives (prev, current, bus) parameters,
    /// where prev is the state at instruction start and current is the working state.
    /// </para>
    /// <para>
    /// When <see cref="WorkingPipelineLength"/> is 0, this array is used directly.
    /// When penalty cycles are inserted, the pipeline is copied to <see cref="WorkingPipeline"/>
    /// and <see cref="WorkingPipelineLength"/> is set to the new length.
    /// </para>
    /// </remarks>
    internal MicroOp[] Pipeline { get; set; } = [];

    /// <summary>
    /// Gets or sets the index of the next micro-operation to execute in the pipeline.
    /// </summary>
    internal int PipelineIndex { get; set; }

    /// <summary>
    /// Gets the effective length of the current pipeline (working or base).
    /// </summary>
    internal int EffectivePipelineLength
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => WorkingPipelineLength > 0 ? WorkingPipelineLength : Pipeline.Length;
    }

    /// <summary>
    /// Gets the micro-op at the specified index from the effective pipeline.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal MicroOp GetPipelineOp(int index) =>
        WorkingPipelineLength > 0 ? WorkingPipeline[index] : Pipeline[index];

    /// <summary>
    /// Gets or sets a value indicating whether the current instruction has completed.
    /// </summary>
    /// <remarks>
    /// Set to <c>true</c> by the final micro-op of an instruction.
    /// When true, the CPU will swap buffers and check for pending interrupts.
    /// </remarks>
    public bool InstructionComplete { get; set; }

    /// <summary>
    /// Gets the number of clock cycles remaining in the current instruction.
    /// </summary>
    /// <remarks>
    /// Returns 0 when no instruction is in progress.
    /// </remarks>
    public int CyclesRemaining => EffectivePipelineLength - PipelineIndex;

    /// <summary>
    /// Gets or sets the current execution status of the CPU.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Indicates whether the CPU is running normally or halted:
    /// </para>
    /// <list type="bullet">
    ///   <item><description><see cref="CpuStatus.Running"/>: Normal execution.</description></item>
    ///   <item><description><see cref="CpuStatus.Stopped"/>: Halted by STP instruction.</description></item>
    ///   <item><description><see cref="CpuStatus.Jammed"/>: Frozen by illegal opcode.</description></item>
    ///   <item><description><see cref="CpuStatus.Waiting"/>: Suspended by WAI instruction.</description></item>
    /// </list>
    /// </remarks>
    public CpuStatus Status
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Registers.Status;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => Registers.Status = value;
    }

    /// <summary>
    /// Gets or sets the pending interrupt signal awaiting service.
    /// </summary>
    /// <remarks>
    /// Interrupts are checked at instruction boundaries. When an instruction completes,
    /// if a pending interrupt exists, it is serviced before the next instruction begins.
    /// </remarks>
    public PendingInterrupt PendingInterrupt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether halt instructions (JAM, STP, WAI) should be ignored.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When set to <c>true</c>, instructions that would normally halt the CPU
    /// (JAM/KIL, STP, WAI) are treated as NOPs instead. The PC advances past the
    /// instruction and execution continues normally.
    /// </para>
    /// <para>
    /// When set to <c>false</c> (the default), these instructions set the appropriate
    /// <see cref="Status"/> flag and halt execution.
    /// </para>
    /// <para>
    /// This is useful for debugging, disassembly, or test scenarios where you need
    /// to step through halt instructions without actually stopping the CPU.
    /// </para>
    /// </remarks>
    public bool IgnoreHaltStopWait { get; set; }

    // ========================================
    // Status Flag Bit Constants
    // ========================================

    /// <summary>Bit mask for the Carry flag (bit 0).</summary>
    public const byte FlagC = 0x01;

    /// <summary>Bit mask for the Zero flag (bit 1).</summary>
    public const byte FlagZ = 0x02;

    /// <summary>Bit mask for the Interrupt Disable flag (bit 2).</summary>
    public const byte FlagI = 0x04;

    /// <summary>Bit mask for the Decimal Mode flag (bit 3).</summary>
    public const byte FlagD = 0x08;

    /// <summary>Bit mask for the Break flag (bit 4). Only meaningful when P is pushed to stack.</summary>
    public const byte FlagB = 0x10;

    /// <summary>Bit mask for the Unused flag (bit 5). Always reads as 1.</summary>
    public const byte FlagU = 0x20;

    /// <summary>Bit mask for the Overflow flag (bit 6).</summary>
    public const byte FlagV = 0x40;

    /// <summary>Bit mask for the Negative flag (bit 7).</summary>
    public const byte FlagN = 0x80;

    /// <summary>
    /// Initializes a new instance of the <see cref="CpuState"/> class with power-on defaults.
    /// </summary>
    public CpuState()
    {
        Reset();
    }

    /// <summary>
    /// Resets the CPU state to power-on defaults.
    /// </summary>
    /// <remarks>
    /// <para>Sets all registers and state to their initial values:</para>
    /// <list type="bullet">
    ///   <item><description>A, X, Y = 0</description></item>
    ///   <item><description>SP = $FD (after reset sequence)</description></item>
    ///   <item><description>PC = $0000 (will be loaded from reset vector)</description></item>
    ///   <item><description>P = $24 (Unused and Interrupt Disable flags set)</description></item>
    ///   <item><description>Status = Running</description></item>
    ///   <item><description>Pipeline cleared</description></item>
    /// </list>
    /// </remarks>
    public void Reset()
    {
        A = 0;
        X = 0;
        Y = 0;
        P = FlagU | FlagI;  // Unused always set, IRQ disabled
        SP = 0xFD;          // Stack pointer after reset sequence
        PC = 0x0000;        // Will be loaded from reset vector

        TempAddress = 0;
        TempValue = 0;
        CurrentOpcode = 0;
        OpcodeAddress = 0;
        PenaltyAddress = 0;
        BranchOldPC = 0;

        Pipeline = [];
        PipelineIndex = 0;
        WorkingPipelineLength = 0;
        InstructionComplete = false;
        Status = CpuStatus.Running;
        PendingInterrupt = PendingInterrupt.None;
    }

    /// <summary>
    /// Copies all state from another <see cref="CpuState"/> instance.
    /// </summary>
    /// <param name="other">The source state to copy from.</param>
    /// <remarks>
    /// <para>
    /// Performs a shallow copy of all fields. The <see cref="Pipeline"/> array reference
    /// is shared (not cloned) since the pipeline is immutable during execution.
    /// </para>
    /// <para>
    /// Note: <see cref="IgnoreHaltStopWait"/> is not copied as it is a configuration
    /// setting rather than execution state.
    /// </para>
    /// <para>
    /// Note: <see cref="WorkingPipeline"/> contents are not copied since the working
    /// pipeline is only valid during instruction execution. At instruction boundaries
    /// (when this method is called), the working pipeline is reset anyway.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyFrom(CpuState other)
    {
        // Single 8-byte copy for all packed registers (A, X, Y, P, SP, PC, Status)
        Registers.Packed = other.Registers.Packed;

        TempAddress = other.TempAddress;
        TempValue = other.TempValue;
        CurrentOpcode = other.CurrentOpcode;
        OpcodeAddress = other.OpcodeAddress;
        PenaltyAddress = other.PenaltyAddress;
        BranchOldPC = other.BranchOldPC;

        // Share the pipeline array reference (immutable during execution)
        Pipeline = other.Pipeline;
        PipelineIndex = other.PipelineIndex;

        // Only copy WorkingPipelineLength, not the array contents.
        // At instruction boundaries, working pipeline is reset, so contents don't need copying.
        WorkingPipelineLength = other.WorkingPipelineLength;

        InstructionComplete = other.InstructionComplete;
        PendingInterrupt = other.PendingInterrupt;
    }

    /// <summary>
    /// Creates a deep copy of this CPU state.
    /// </summary>
    /// <returns>A new <see cref="CpuState"/> instance with all values copied.</returns>
    /// <remarks>
    /// Use this for save states or when you need an independent copy.
    /// For hot-path updates where you want to avoid allocation, use
    /// <see cref="CopyFrom"/> on an existing instance instead.
    /// </remarks>
    public CpuState Clone()
    {
        var clone = new CpuState();
        clone.CopyFrom(this);
        return clone;
    }

    // ========================================
    // Status Flag Helpers
    // ========================================

    /// <summary>
    /// Gets the value of a specific status flag.
    /// </summary>
    /// <param name="flag">The flag bit mask (e.g., <see cref="FlagC"/>, <see cref="FlagZ"/>).</param>
    /// <returns><c>true</c> if the flag is set; otherwise, <c>false</c>.</returns>
    public bool GetFlag(byte flag) => (P & flag) != 0;

    /// <summary>
    /// Sets or clears a specific status flag.
    /// </summary>
    /// <param name="flag">The flag bit mask (e.g., <see cref="FlagC"/>, <see cref="FlagZ"/>).</param>
    /// <param name="value"><c>true</c> to set the flag; <c>false</c> to clear it.</param>
    public void SetFlag(byte flag, bool value)
    {
        if (value)
        {
            P |= flag;
        }
        else
        {
            P = (byte) (P & ~flag);
        }
    }

    // ========================================
    // Convenience Flag Properties
    // ========================================

    /// <summary>Gets or sets the Carry flag. Set when an arithmetic operation produces a carry or borrow.</summary>
    public bool CarryFlag { get => GetFlag(FlagC); set => SetFlag(FlagC, value); }

    /// <summary>Gets or sets the Zero flag. Set when an operation produces a zero result.</summary>
    public bool ZeroFlag { get => GetFlag(FlagZ); set => SetFlag(FlagZ, value); }

    /// <summary>Gets or sets the Interrupt Disable flag. When set, IRQ interrupts are ignored.</summary>
    public bool InterruptDisableFlag { get => GetFlag(FlagI); set => SetFlag(FlagI, value); }

    /// <summary>Gets or sets the Decimal Mode flag. When set, ADC and SBC use BCD arithmetic.</summary>
    public bool DecimalFlag { get => GetFlag(FlagD); set => SetFlag(FlagD, value); }

    /// <summary>Gets or sets the Break flag. Only meaningful when P is pushed to stack by BRK instruction.</summary>
    public bool BreakFlag { get => GetFlag(FlagB); set => SetFlag(FlagB, value); }

    /// <summary>Gets or sets the Overflow flag. Set when a signed arithmetic operation overflows.</summary>
    public bool OverflowFlag { get => GetFlag(FlagV); set => SetFlag(FlagV, value); }

    /// <summary>Gets or sets the Negative flag. Set when the result has bit 7 set (negative in signed arithmetic).</summary>
    public bool NegativeFlag { get => GetFlag(FlagN); set => SetFlag(FlagN, value); }
}
