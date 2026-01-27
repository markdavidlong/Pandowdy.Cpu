namespace Pandowdy.Cpu;

/// <summary>
/// Represents the execution status of the CPU.
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
/// </remarks>
public enum CpuStatus
{
    /// <summary>Normal execution mode - CPU is actively processing instructions.</summary>
    Running,

    /// <summary>
    /// CPU halted by STP instruction. Only a hardware reset can resume execution.
    /// Available on 65C02 and later variants.
    /// </summary>
    Stopped,

    /// <summary>
    /// CPU frozen by executing an illegal JAM/KIL opcode.
    /// Only available on NMOS 6502. Requires hardware reset to recover.
    /// </summary>
    Jammed,

    /// <summary>
    /// CPU suspended by WAI instruction, waiting for an interrupt (IRQ, NMI, or Reset).
    /// When an interrupt occurs, the CPU resumes execution at the interrupt handler.
    /// Available on 65C02 and later variants.
    /// </summary>
    Waiting,

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
    /// This status persists until <see cref="Cpu.reset"/> is called or it is
    /// manually reset to <see cref="Running"/>.
    /// </para>
    /// </remarks>
    Bypassed
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
/// For debugging and time-travel debugging, use <see cref="CpuStateBuffer"/> which maintains
/// both the previous and current state, allowing comparison of state changes per instruction.
/// </para>
/// </remarks>
/// <seealso cref="CpuStateBuffer"/>
/// <seealso cref="CpuStatus"/>
/// <seealso cref="PendingInterrupt"/>
public class CpuState
{
    // ========================================
    // Programmer-Visible Registers
    // ========================================

    /// <summary>
    /// Gets or sets the Accumulator register (A).
    /// </summary>
    /// <remarks>
    /// The accumulator is the primary register for arithmetic and logic operations.
    /// Most ALU operations use the accumulator as both source and destination.
    /// </remarks>
    public byte A { get; set; }

    /// <summary>
    /// Gets or sets the X Index register.
    /// </summary>
    /// <remarks>
    /// Used for indexed addressing modes (e.g., LDA $1234,X) and as a general-purpose counter.
    /// Some instructions like TAX, TXA, TXS, TSX, INX, DEX operate directly on X.
    /// </remarks>
    public byte X { get; set; }

    /// <summary>
    /// Gets or sets the Y Index register.
    /// </summary>
    /// <remarks>
    /// Used for indexed addressing modes (e.g., LDA $1234,Y) and as a general-purpose counter.
    /// Some instructions like TAY, TYA, INY, DEY operate directly on Y.
    /// </remarks>
    public byte Y { get; set; }

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
    public byte P { get; set; }

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
    public byte SP { get; set; }

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
    public ushort PC { get; set; }

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

    // ========================================
    // Pipeline Execution State
    // ========================================

    /// <summary>
    /// Gets or sets the array of micro-operations for the current instruction.
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
    /// </remarks>
    public Action<CpuState, CpuState, IPandowdyCpuBus>[] Pipeline { get; set; } = [];

    /// <summary>
    /// Gets or sets the index of the next micro-operation to execute in the pipeline.
    /// </summary>
    public int PipelineIndex { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the current instruction has completed.
    /// </summary>
    /// <remarks>
    /// Set to <c>true</c> by the final micro-op of an instruction.
    /// When true, the CPU will swap buffers and check for pending interrupts.
    /// </remarks>
    public bool InstructionComplete { get; set; }

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
    public CpuStatus Status { get; set; }

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

        Pipeline = [];
        PipelineIndex = 0;
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
    /// </remarks>
    public void CopyFrom(CpuState other)
    {
        A = other.A;
        X = other.X;
        Y = other.Y;
            P = other.P;
            SP = other.SP;
            PC = other.PC;

            TempAddress = other.TempAddress;
            TempValue = other.TempValue;

            // Share the pipeline array reference (immutable during execution)
            Pipeline = other.Pipeline;
            PipelineIndex = other.PipelineIndex;
            InstructionComplete = other.InstructionComplete;
            Status = other.Status;
            PendingInterrupt = other.PendingInterrupt;
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

        // ========================================
        // Interrupt Vector Addresses
        // ========================================

        /// <summary>Memory address of the NMI vector ($FFFA-$FFFB).</summary>
        private const ushort NmiVector = 0xFFFA;

        /// <summary>Memory address of the Reset vector ($FFFC-$FFFD).</summary>
        private const ushort ResetVector = 0xFFFC;

        /// <summary>Memory address of the IRQ/BRK vector ($FFFE-$FFFF).</summary>
        private const ushort IrqVector = 0xFFFE;

        // ========================================
        // Interrupt Signal Methods
        // ========================================

        /// <summary>
        /// Signals an IRQ (Interrupt Request).
        /// </summary>
        /// <remarks>
        /// <para>
        /// The IRQ will be serviced at the next instruction boundary if the
        /// <see cref="InterruptDisableFlag"/> is clear. If the I flag is set,
        /// the IRQ remains pending until the flag is cleared.
        /// </para>
        /// <para>
        /// IRQ has the lowest priority. If an NMI or Reset is already pending,
        /// the IRQ signal is ignored.
        /// </para>
        /// </remarks>
        public void SignalIrq()
        {
            // Only set if no higher priority interrupt is pending
            if (PendingInterrupt == PendingInterrupt.None)
            {
                PendingInterrupt = PendingInterrupt.Irq;
            }
        }

        /// <summary>
        /// Signals an NMI (Non-Maskable Interrupt).
        /// </summary>
        /// <remarks>
        /// <para>
        /// The NMI will be serviced at the next instruction boundary.
        /// Unlike IRQ, NMI cannot be disabled by the I flag.
        /// </para>
        /// <para>
        /// NMI has higher priority than IRQ but lower than Reset.
        /// If a Reset is already pending, the NMI signal is ignored.
        /// </para>
        /// </remarks>
        public void SignalNmi()
        {
            // NMI has priority over IRQ, but not Reset
            if (PendingInterrupt != PendingInterrupt.Reset)
            {
                PendingInterrupt = PendingInterrupt.Nmi;
            }
        }

        /// <summary>
        /// Signals a hardware Reset.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Reset has the highest priority and will always be serviced at the
        /// next instruction boundary. All other pending interrupts are superseded.
        /// </para>
        /// <para>
        /// When handled, Reset reinitializes all CPU registers and loads PC
        /// from the reset vector at $FFFC-$FFFD.
        /// </para>
        /// </remarks>
        public void SignalReset()
        {
            // Reset has highest priority
            PendingInterrupt = PendingInterrupt.Reset;
        }

        /// <summary>
        /// Clears a pending IRQ signal.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Use this for level-triggered IRQ behavior. Call when the IRQ line
        /// goes high (inactive) to clear the pending interrupt before it is serviced.
        /// </para>
        /// <para>
        /// Only clears the pending interrupt if it is an IRQ; NMI and Reset
        /// signals are not affected.
        /// </para>
        /// </remarks>
        public void ClearIrq()
        {
            if (PendingInterrupt == PendingInterrupt.Irq)
            {
                PendingInterrupt = PendingInterrupt.None;
            }
        }

        // ========================================
        // Interrupt Handler Methods
        // ========================================

        /// <summary>
        /// Checks for and handles any pending interrupt.
        /// </summary>
        /// <param name="bus">The bus interface for reading vectors and writing to the stack.</param>
        /// <returns><c>true</c> if an interrupt was handled; <c>false</c> if no interrupt was pending or IRQ was masked.</returns>
        /// <remarks>
        /// <para>
        /// This method should be called at instruction boundaries (when <see cref="InstructionComplete"/> is true).
        /// It checks for pending interrupts in priority order: Reset > NMI > IRQ.
        /// </para>
        /// <para>
        /// For IRQ, the interrupt is only serviced if the <see cref="InterruptDisableFlag"/> is clear,
        /// unless the CPU is in <see cref="CpuStatus.Waiting"/> state (WAI instruction allows
        /// IRQ to wake the CPU even when I flag is set).
        /// </para>
        /// </remarks>
        public bool HandlePendingInterrupt(IPandowdyCpuBus bus)
        {
            switch (PendingInterrupt)
            {
                case PendingInterrupt.Reset:
                    HandleReset(bus);
                    return true;

                case PendingInterrupt.Nmi:
                    HandleNmi(bus);
                    return true;

                case PendingInterrupt.Irq:
                    // IRQ is ignored if I flag is set (unless we're in Waiting state)
                    if (InterruptDisableFlag && Status != CpuStatus.Waiting)
                    {
                        return false;
                    }

                    HandleIrq(bus);
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Handles an NMI (Non-Maskable Interrupt).
        /// </summary>
        /// <param name="bus">The bus interface for reading the vector and writing to the stack.</param>
        /// <remarks>
        /// <para>Performs the following sequence:</para>
        /// <list type="number">
        ///   <item><description>Push PC high byte to stack</description></item>
        ///   <item><description>Push PC low byte to stack</description></item>
        ///   <item><description>Push P to stack (with B clear, U set)</description></item>
        ///   <item><description>Set the I flag</description></item>
        ///   <item><description>Load PC from NMI vector ($FFFA-$FFFB)</description></item>
        /// </list>
        /// <para>If the CPU was in <see cref="CpuStatus.Waiting"/> state, it resumes to <see cref="CpuStatus.Running"/>.</para>
        /// </remarks>
        private void HandleNmi(IPandowdyCpuBus bus)
        {
            PendingInterrupt = PendingInterrupt.None;

            // Push PC high byte
            bus.Write((ushort) (0x0100 + SP), (byte) (PC >> 8));
            SP--;

            // Push PC low byte
            bus.Write((ushort) (0x0100 + SP), (byte) (PC & 0xFF));
            SP--;

            // Push P with B flag clear (NMI clears B, U always set)
        byte statusToPush = (byte) ((P | FlagU) & ~FlagB);
        bus.Write((ushort) (0x0100 + SP), statusToPush);
        SP--;

        // Set I flag
            P |= FlagI;

            // Load PC from NMI vector
            byte low = bus.CpuRead(NmiVector);
            byte high = bus.CpuRead((ushort) (NmiVector + 1));
            PC = (ushort) ((high << 8) | low);

            // Clear pipeline and resume if waiting
            Pipeline = [];
            PipelineIndex = 0;
            InstructionComplete = false;
            if (Status == CpuStatus.Waiting)
            {
                Status = CpuStatus.Running;
            }
        }

        /// <summary>
        /// Handles an IRQ (Interrupt Request).
        /// </summary>
        /// <param name="bus">The bus interface for reading the vector and writing to the stack.</param>
        /// <remarks>
        /// <para>Performs the following sequence:</para>
        /// <list type="number">
        ///   <item><description>Push PC high byte to stack</description></item>
        ///   <item><description>Push PC low byte to stack</description></item>
        ///   <item><description>Push P to stack (with B clear, U set)</description></item>
        ///   <item><description>Set the I flag</description></item>
        ///   <item><description>Load PC from IRQ vector ($FFFE-$FFFF)</description></item>
        /// </list>
        /// <para>If the CPU was in <see cref="CpuStatus.Waiting"/> state, it resumes to <see cref="CpuStatus.Running"/>.</para>
        /// </remarks>
        private void HandleIrq(IPandowdyCpuBus bus)
        {
            PendingInterrupt = PendingInterrupt.None;

            // Push PC high byte
            bus.Write((ushort) (0x0100 + SP), (byte) (PC >> 8));
            SP--;

            // Push PC low byte
            bus.Write((ushort) (0x0100 + SP), (byte) (PC & 0xFF));
            SP--;

            // Push P with B flag clear (IRQ clears B, U always set)
            byte statusToPush = (byte) ((P | FlagU) & ~FlagB);
            bus.Write((ushort) (0x0100 + SP), statusToPush);
            SP--;

            // Set I flag
            P |= FlagI;

            // Load PC from IRQ vector
            byte low = bus.CpuRead(IrqVector);
            byte high = bus.CpuRead((ushort) (IrqVector + 1));
            PC = (ushort) ((high << 8) | low);

            // Clear pipeline and resume if waiting
            Pipeline = [];
            PipelineIndex = 0;
            InstructionComplete = false;
            if (Status == CpuStatus.Waiting)
            {
                Status = CpuStatus.Running;
            }
        }

        /// <summary>
        /// Handles a hardware Reset.
        /// </summary>
        /// <param name="bus">The bus interface for reading the reset vector.</param>
        /// <remarks>
        /// <para>Reinitializes the CPU to power-on state:</para>
        /// <list type="bullet">
        ///   <item><description>A, X, Y = 0</description></item>
        ///   <item><description>SP = $FD</description></item>
        ///   <item><description>P = $24 (U and I flags set)</description></item>
        ///   <item><description>Load PC from reset vector ($FFFC-$FFFD)</description></item>
        ///   <item><description>Status = Running</description></item>
        /// </list>
        /// <para>Unlike NMI/IRQ, Reset does not push anything to the stack.</para>
        /// </remarks>
        private void HandleReset(IPandowdyCpuBus bus)
        {
            PendingInterrupt = PendingInterrupt.None;

            // Reset registers
            A = 0;
            X = 0;
            Y = 0;
            P = FlagU | FlagI;  // Unused always set, IRQ disabled
            SP = 0xFD;          // Stack pointer after reset sequence

            TempAddress = 0;
            TempValue = 0;

            // Load PC from reset vector
            byte low = bus.CpuRead(ResetVector);
            byte high = bus.CpuRead((ushort) (ResetVector + 1));
            PC = (ushort) ((high << 8) | low);

        // Clear pipeline and set to running
        Pipeline = [];
        PipelineIndex = 0;
        InstructionComplete = false;
        Status = CpuStatus.Running;
    }
}