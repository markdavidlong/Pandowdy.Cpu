using Emulator;

namespace Pandowdy.EmuCore.Interfaces
{
    /// <summary>
    /// Represents a 6502 microprocessor and its associated registers and operations.
    /// </summary>
    /// <remarks>
    /// The 6502 is an 8-bit processor used in the Apple IIe and many other computers
    /// of the 1970s-1980s era. It features a 16-bit address bus (64KB address space),
    /// three general-purpose registers (A, X, Y), an 8-bit stack pointer, and a status
    /// register with condition flags. This interface provides access to all registers
    /// and methods for executing instructions, handling interrupts, and managing CPU state.
    /// </remarks>
    public interface ICpu
    {
        /// <summary>
        /// Gets the Program Counter register, which points to the next instruction to execute.
        /// </summary>
        /// <value>
        /// A 16-bit address ($0000-$FFFF) indicating the memory location of the next
        /// instruction byte to be fetched and executed.
        /// </value>
        UInt16 PC { get; }

        /// <summary>
        /// Gets the Stack Pointer register, which points to the current top of the stack.
        /// </summary>
        /// <value>
        /// An 8-bit offset ($00-$FF) within the stack page ($0100-$01FF). The actual
        /// stack address is $0100 + SP. The stack grows downward (SP decrements on push).
        /// </value>
        Byte SP { get; }

        /// <summary>
        /// Gets the Accumulator register, the primary register for arithmetic and logic operations.
        /// </summary>
        /// <value>
        /// An 8-bit value used as the primary operand for ALU operations, loads, stores,
        /// and comparisons. Most 6502 instructions operate on or with the accumulator.
        /// </value>
        Byte A { get; }

        /// <summary>
        /// Gets the X Index register, used for indexed addressing modes and counters.
        /// </summary>
        /// <value>
        /// An 8-bit value commonly used for indexed addressing (e.g., "LDA $1000,X"),
        /// loop counters, and temporary storage.
        /// </value>
        Byte X { get; }

        /// <summary>
        /// Gets the Y Index register, used for indexed addressing modes and counters.
        /// </summary>
        /// <value>
        /// An 8-bit value commonly used for indexed addressing (e.g., "LDA $1000,Y"),
        /// loop counters, and temporary storage. Similar to X but with fewer addressing modes.
        /// </value>
        Byte Y { get; }

        /// <summary>
        /// Gets the Processor Status register containing condition flags.
        /// </summary>
        /// <value>
        /// An 8-bit register containing seven status flags:
        /// <list type="bullet">
        /// <item>Bit 7 (N): Negative flag - Set if result is negative (bit 7 = 1)</item>
        /// <item>Bit 6 (V): Overflow flag - Set if signed arithmetic overflow occurred</item>
        /// <item>Bit 5: Unused (always 1)</item>
        /// <item>Bit 4 (B): Break flag - Set if interrupt was caused by BRK instruction</item>
        /// <item>Bit 3 (D): Decimal mode flag - Enables BCD arithmetic (not used on Apple II)</item>
        /// <item>Bit 2 (I): Interrupt disable flag - When set, IRQ interrupts are ignored</item>
        /// <item>Bit 1 (Z): Zero flag - Set if result is zero</item>
        /// <item>Bit 0 (C): Carry flag - Set by arithmetic operations and shifts</item>
        /// </list>
        /// </value>
        ProcessorStatus Status { get; }

        /// <summary>
        /// Executes one clock cycle of the CPU.
        /// </summary>
        /// <param name="bus">The system bus for memory and I/O access</param>
        /// <remarks>
        /// The 6502 is a multi-cycle processor where each instruction takes 2-7 cycles
        /// to complete. This method advances the CPU by one cycle, fetching instruction
        /// bytes, calculating addresses, and executing operations as appropriate for the
        /// current cycle of the instruction being executed. Call this method repeatedly
        /// (typically at 1.023 MHz for Apple IIe timing) to execute the program.
        /// </remarks>
        void Clock(IAppleIIBus bus);

        /// <summary>
        /// Triggers an Interrupt Request (IRQ), causing the CPU to jump to the IRQ handler.
        /// </summary>
        /// <param name="bus">The system bus for memory and I/O access</param>
        /// <remarks>
        /// IRQ is a maskable hardware interrupt that can be disabled by setting the I flag
        /// in the Status register. When triggered and not masked, the CPU:
        /// <list type="number">
        /// <item>Finishes the current instruction</item>
        /// <item>Pushes PC (high byte, then low byte) onto the stack</item>
        /// <item>Pushes Status register onto the stack</item>
        /// <item>Sets the I flag to disable further IRQs</item>
        /// <item>Loads PC from the IRQ vector at $FFFE-$FFFF</item>
        /// </list>
        /// On the Apple IIe, IRQ is not commonly used.
        /// </remarks>
        void InterruptRequest(IAppleIIBus bus);

        /// <summary>
        /// Checks whether the current instruction has completed execution.
        /// </summary>
        /// <returns>True if the current instruction is complete and the CPU is ready
        /// to fetch the next instruction; false if the instruction is still executing.</returns>
        /// <remarks>
        /// Since 6502 instructions take multiple clock cycles, this method allows external
        /// code to determine when an instruction boundary has been reached, which is useful
        /// for debugging, breakpoints, and cycle-accurate timing.
        /// </remarks>
        bool IsInstructionComplete();

        /// <summary>
        /// Triggers a Non-Maskable Interrupt (NMI), causing the CPU to jump to the NMI handler.
        /// </summary>
        /// <param name="bus">The system bus for memory and I/O access</param>
        /// <remarks>
        /// NMI is a high-priority hardware interrupt that cannot be disabled. When triggered,
        /// the CPU behaves similarly to IRQ but uses the NMI vector at $FFFA-$FFFB:
        /// <list type="number">
        /// <item>Finishes the current instruction</item>
        /// <item>Pushes PC (high byte, then low byte) onto the stack</item>
        /// <item>Pushes Status register onto the stack</item>
        /// <item>Sets the I flag to disable IRQs</item>
        /// <item>Loads PC from the NMI vector at $FFFA-$FFFB</item>
        /// </list>
        /// </remarks>
        void NonMaskableInterrupt(IAppleIIBus bus);

        /// <summary>
        /// Reads a byte from memory at the specified address.
        /// </summary>
        /// <param name="address">The 16-bit memory address to read from ($0000-$FFFF)</param>
        /// <returns>The byte value at the specified address</returns>
        /// <remarks>
        /// This method provides direct memory access for the CPU, typically delegating
        /// to the bus's CpuRead method. Used internally during instruction execution
        /// for operand fetches and data loads.
        /// </remarks>
        byte Read(ushort address);

        /// <summary>
        /// Resets the CPU to its power-on state.
        /// </summary>
        /// <param name="bus">The system bus for memory and I/O access</param>
        /// <remarks>
        /// Reset initializes the CPU to a known state:
        /// <list type="bullet">
        /// <item>PC is loaded from the reset vector at $FFFC-$FFFD</item>
        /// <item>SP is set to $FD (or $FF on some implementations)</item>
        /// <item>Status register is initialized with I flag set (interrupts disabled)</item>
        /// <item>A, X, Y registers are not initialized (undefined state)</item>
        /// </list>
        /// This simulates powering on the computer or pressing the reset key.
        /// On the Apple IIe, the reset vector typically points to the monitor ROM at $FF59.
        /// </remarks>
        void Reset(IAppleIIBus bus);

        /// <summary>
        /// Writes a byte to memory at the specified address.
        /// </summary>
        /// <param name="address">The 16-bit memory address to write to ($0000-$FFFF)</param>
        /// <param name="data">The byte value to write</param>
        /// <remarks>
        /// This method provides direct memory access for the CPU, typically delegating
        /// to the bus's CpuWrite method. Used internally during instruction execution
        /// for data stores and stack operations.
        /// </remarks>
        void Write(ushort address, byte data);
    }
}
