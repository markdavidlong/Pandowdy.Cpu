//------------------------------------------------------------------------------
// CPUAdapter.cs
//
// ⚠️ STOPGAP IMPLEMENTATION - PLANNED FOR REPLACEMENT ⚠️
//
// This adapter wraps a third-party 6502 CPU emulator (legacy/6502.NET/Emulator)
// to provide compatibility with Pandowdy's ICpu interface. It is a temporary
// solution until a native 6502 implementation is written.
//
// SCOPE: This adapter bridges the gap between the legacy Emulator.CPU class
// and Pandowdy's ICpu interface, handling the connection/disconnection pattern
// required by the legacy CPU implementation.
//
// Current Limitations:
// - Requires connect/disconnect cycle for each bus operation
// - Performance overhead from repeated connection management
// - Dependency on external legacy codebase (6502.NET)
// - Limited control over CPU implementation details
//
// Future Replacement:
// The planned replacement will implement:
// - Native ICpu implementation without external dependencies
// - Direct bus access without connection overhead
// - Full control over cycle-accurate timing
// - Optimized instruction execution
// - Better integration with Pandowdy architecture
//
// The interface (ICpu) will remain stable to ensure the replacement can be
// swapped in transparently.
//------------------------------------------------------------------------------

using System.Runtime.CompilerServices;
using Emulator;
using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore
{
    /// <summary>
    /// Adapter wrapping a third-party 6502 CPU emulator for Pandowdy compatibility (stopgap implementation).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>⚠️ TEMPORARY IMPLEMENTATION:</strong> This adapter wraps the legacy
    /// <c>Emulator.CPU</c> class from the 6502.NET project to provide compatibility with
    /// Pandowdy's <see cref="ICpu"/> interface. It will be replaced by a native 6502
    /// implementation in a future update.
    /// </para>
    /// <para>
    /// <strong>Purpose:</strong> Bridges the gap between the legacy CPU emulator and
    /// Pandowdy's architecture. The legacy CPU requires explicit connection to a bus
    /// before each operation, while Pandowdy's design passes the bus as a parameter.
    /// This adapter handles the connection/disconnection pattern.
    /// </para>
    /// <para>
    /// <strong>Connection Pattern:</strong> The legacy CPU uses a "Connect-Execute-Disconnect"
    /// pattern for each bus operation. The bus connection is temporary and scoped to the
    /// operation lifetime:
    /// <code>
    /// _oldCpu.Connect(bus);    // Connect to bus
    /// _oldCpu.Clock();         // Execute operation
    /// _oldCpu.Connect(null);   // Disconnect
    /// </code>
    /// This pattern is repeated for every bus operation (Clock, Reset, interrupts), adding
    /// overhead compared to a native implementation that would hold a bus reference.
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> Thread safety is guaranteed by VA2M's command queue
    /// architecture. All CPU operations are serialized on the emulator thread and executed
    /// at instruction boundaries via ProcessPending(). This ensures the Connect-Disconnect
    /// pattern cannot be interrupted by operations from other threads, eliminating race
    /// conditions.
    /// </para>
    /// <para>
    /// <strong>Performance Considerations:</strong> The connection overhead is minimal but
    /// not ideal. Read/Write operations are marked with <c>[MethodImpl(MethodImplOptions.AggressiveInlining)]</c>
    /// to reduce call overhead since they're invoked frequently during instruction execution.
    /// </para>
    /// <para>
    /// <strong>Dependency:</strong> Requires the <c>Emulator</c> project from legacy/6502.NET,
    /// which is a third-party 6502 emulator. The replacement will eliminate this external
    /// dependency.
    /// </para>
    /// </remarks>
    public class CPUAdapter : ICpu
    {
        /// <summary>
        /// The wrapped legacy 6502 CPU instance from the third-party Emulator library.
        /// </summary>
        private Emulator.CPU _oldCpu;

        /// <inheritdoc />
        /// <remarks>
        /// Directly exposes the Program Counter from the wrapped CPU. No adaptation needed.
        /// </remarks>
        public UInt16 PC { get => _oldCpu.PC; }
        
        /// <inheritdoc />
        /// <remarks>
        /// Directly exposes the Stack Pointer from the wrapped CPU. No adaptation needed.
        /// </remarks>
        public Byte SP { get => _oldCpu.SP; }
        
        /// <inheritdoc />
        /// <remarks>
        /// Directly exposes the Accumulator from the wrapped CPU. No adaptation needed.
        /// </remarks>
        public Byte A { get => _oldCpu.A; }
        
        /// <inheritdoc />
        /// <remarks>
        /// Directly exposes the X Index register from the wrapped CPU. No adaptation needed.
        /// </remarks>
        public Byte X { get => _oldCpu.X; }
        
        /// <inheritdoc />
        /// <remarks>
        /// Directly exposes the Y Index register from the wrapped CPU. No adaptation needed.
        /// </remarks>
        public Byte Y { get => _oldCpu.Y; }
        
        /// <inheritdoc />
        /// <remarks>
        /// Directly exposes the Processor Status register from the wrapped CPU. No adaptation needed.
        /// </remarks>
        public ProcessorStatus Status { get => _oldCpu.Status; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CPUAdapter"/> class.
        /// </summary>
        /// <param name="cpu">
        /// The legacy <c>Emulator.CPU</c> instance to wrap. This CPU instance should be
        /// pre-configured but not yet connected to a bus.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="cpu"/> is null.
        /// </exception>
        /// <remarks>
        /// The adapter takes ownership of the CPU instance and manages its connection lifecycle.
        /// The CPU should not be used directly after being wrapped by this adapter.
        /// </remarks>
        public CPUAdapter(Emulator.CPU cpu)
        {
            ArgumentNullException.ThrowIfNull(cpu);
            _oldCpu = cpu;
        }

        /// <summary>
        /// Reads a byte from memory at the specified address.
        /// </summary>
        /// <param name="address">16-bit memory address to read from ($0000-$FFFF).</param>
        /// <returns>The byte value at the specified address.</returns>
        /// <remarks>
        /// <para>
        /// <strong>Performance:</strong> Marked with <c>[AggressiveInlining]</c> because this
        /// method is called frequently during instruction execution (multiple times per instruction
        /// for multi-byte operands and addressing mode calculations). Inlining eliminates call
        /// overhead.
        /// </para>
        /// <para>
        /// <strong>Direct Delegation:</strong> Simply forwards to the wrapped CPU's Read method.
        /// The actual bus access is handled by the legacy CPU implementation.
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Byte Read(UInt16 address)
        {
            return _oldCpu.Read(address);
        }

        /// <summary>
        /// Writes a byte to memory at the specified address.
        /// </summary>
        /// <param name="address">16-bit memory address to write to ($0000-$FFFF).</param>
        /// <param name="data">Byte value to write to the specified address.</param>
        /// <remarks>
        /// <para>
        /// <strong>Performance:</strong> Marked with <c>[AggressiveInlining]</c> because this
        /// method is called frequently during instruction execution (for store operations,
        /// stack pushes, and memory-modifying instructions). Inlining eliminates call overhead.
        /// </para>
        /// <para>
        /// <strong>Direct Delegation:</strong> Simply forwards to the wrapped CPU's Write method.
        /// The actual bus access is handled by the legacy CPU implementation.
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(UInt16 address, Byte data)
        {
            _oldCpu.Write(address, data);
        }

        /// <summary>
        /// Resets the CPU to its power-on state.
        /// </summary>
        /// <param name="bus">The system bus for reading the reset vector.</param>
        /// <remarks>
        /// <para>
        /// <strong>Reset Sequence:</strong> The 6502 reset process:
        /// <list type="number">
        /// <item>Connects to the bus temporarily</item>
        /// <item>Reads the reset vector from $FFFC/$FFFD</item>
        /// <item>Sets PC to the vector address</item>
        /// <item>Initializes SP, Status register, and other state</item>
        /// <item>Disconnects from the bus</item>
        /// </list>
        /// </para>
        /// <para>
        /// <strong>Connection Pattern:</strong> Uses the Connect-Execute-Disconnect pattern
        /// required by the legacy CPU. The bus connection is temporary and released after reset.
        /// </para>
        /// <para>
        /// <strong>Thread Safety:</strong> Thread safety is guaranteed by VA2M's command queue,
        /// which ensures this method executes serially on the emulator thread at instruction
        /// boundaries. No race conditions can occur.
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset(IAppleIIBus bus)
        {
            ArgumentNullException.ThrowIfNull(bus);
            _oldCpu.Connect(bus);
            _oldCpu.Reset();
            _oldCpu.Connect(null);
        }

        /// <summary>
        /// Checks if the current instruction has completed execution.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the current instruction has finished all its cycles;
        /// <c>false</c> if the instruction is still in progress.
        /// </returns>
        /// <remarks>
        /// <para>
        /// <strong>Multi-Cycle Instructions:</strong> 6502 instructions take 2-7 cycles to
        /// complete. This method allows the emulator to determine when an instruction boundary
        /// has been reached, which is useful for:
        /// <list type="bullet">
        /// <item>Debugging (stepping through instructions)</item>
        /// <item>Breakpoints (stopping at instruction boundaries)</item>
        /// <item>Timing-sensitive operations (syncing with video refresh)</item>
        /// </list>
        /// </para>
        /// <para>
        /// <strong>Direct Delegation:</strong> Simply forwards to the wrapped CPU's method.
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsInstructionComplete()
        {
            return _oldCpu.IsInstructionComplete();
        }

        /// <summary>
        /// Triggers an Interrupt Request (IRQ), causing the CPU to jump to the IRQ handler.
        /// </summary>
        /// <param name="bus">The system bus for pushing return address and reading IRQ vector.</param>
        /// <remarks>
        /// <para>
        /// <strong>IRQ Process:</strong> When triggered (and not masked by the I flag):
        /// <list type="number">
        /// <item>Connects to the bus temporarily</item>
        /// <item>Finishes current instruction</item>
        /// <item>Pushes PC and Status to stack</item>
        /// <item>Reads IRQ vector from $FFFE/$FFFF</item>
        /// <item>Sets PC to vector address</item>
        /// <item>Sets I flag (disable further IRQs)</item>
        /// <item>Disconnects from the bus</item>
        /// </list>
        /// </para>
        /// <para>
        /// <strong>Apple IIe Usage:</strong> Used for peripheral interrupts, keyboard input,
        /// and timer-based events. The handler must eventually execute RTI (Return from Interrupt)
        /// to restore CPU state and clear the I flag.
        /// </para>
        /// <para>
        /// <strong>Connection Pattern:</strong> Uses the Connect-Execute-Disconnect pattern
        /// required by the legacy CPU.
        /// </para>
        /// <para>
        /// <strong>Thread Safety:</strong> Thread safety is guaranteed by VA2M's command queue,
        /// which ensures this method executes serially on the emulator thread at instruction
        /// boundaries.
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void InterruptRequest(IAppleIIBus bus)
        {
            ArgumentNullException.ThrowIfNull(bus);
            _oldCpu.Connect(bus);
            _oldCpu.InterruptRequest();
            _oldCpu.Connect(null);
        }

        /// <summary>
        /// Triggers a Non-Maskable Interrupt (NMI), unconditionally jumping to the NMI handler.
        /// </summary>
        /// <param name="bus">The system bus for pushing return address and reading NMI vector.</param>
        /// <remarks>
        /// <para>
        /// <strong>NMI Process:</strong> Always executed (cannot be masked):
        /// <list type="number">
        /// <item>Connects to the bus temporarily</item>
        /// <item>Finishes current instruction</item>
        /// <item>Pushes PC and Status to stack</item>
        /// <item>Reads NMI vector from $FFFA/$FFFB</item>
        /// <item>Sets PC to vector address</item>
        /// <item>Sets I flag (prevent IRQ during NMI)</item>
        /// <item>Disconnects from the bus</item>
        /// </list>
        /// </para>
        /// <para>
        /// <strong>Apple IIe Usage:</strong> NMI is rarely used on the Apple IIe. Some expansion
        /// cards may use it for critical events that must be handled immediately regardless of
        /// the interrupt disable flag state.
        /// </para>
        /// <para>
        /// <strong>Connection Pattern:</strong> Uses the Connect-Execute-Disconnect pattern
        /// required by the legacy CPU.
        /// </para>
        /// <para>
        /// <strong>Thread Safety:</strong> Thread safety is guaranteed by VA2M's command queue,
        /// which ensures this method executes serially on the emulator thread at instruction
        /// boundaries.
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void NonMaskableInterrupt(IAppleIIBus bus)
        {
            ArgumentNullException.ThrowIfNull(bus);
            _oldCpu.Connect(bus);
            _oldCpu.NonMaskableInterrupt();
            _oldCpu.Connect(null);
        }

        /// <summary>
        /// Executes one clock cycle of the CPU.
        /// </summary>
        /// <param name="bus">The system bus for memory and I/O access during this cycle.</param>
        /// <remarks>
        /// <para>
        /// <strong>Cycle Execution:</strong> The 6502 is a multi-cycle processor. Each call to
        /// this method advances the CPU by one cycle:
        /// <list type="bullet">
        /// <item>Cycle 1: Fetch instruction opcode</item>
        /// <item>Cycles 2-6: Fetch operands, calculate addresses, execute operation (varies by instruction)</item>
        /// <item>Final cycle: Write result if needed</item>
        /// </list>
        /// </para>
        /// <para>
        /// <strong>Timing:</strong> Call this method at approximately 1.023 MHz for accurate
        /// Apple IIe timing (1,020,484 Hz to be precise). The emulator typically runs this in
        /// a loop, either continuously or in bursts synchronized with video refresh.
        /// </para>
        /// <para>
        /// <strong>Connection Pattern:</strong> Uses the Connect-Execute-Disconnect pattern
        /// required by the legacy CPU. This is the most frequently called method in the emulator,
        /// so the connection overhead is most noticeable here. The native replacement will
        /// eliminate this overhead.
        /// </para>
        /// <para>
        /// <strong>Thread Safety:</strong> Thread safety is guaranteed by VA2M's architecture.
        /// This method is only ever called from the emulator thread (via Bus.Clock() in VA2M.RunAsync()
        /// or VA2M.Clock()). All external operations are queued and executed at instruction boundaries
        /// via ProcessPending(), ensuring serial execution with no race conditions.
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clock(IAppleIIBus bus)
        {
            ArgumentNullException.ThrowIfNull(bus);
            _oldCpu.Connect(bus);
            _oldCpu.Clock();
            _oldCpu.Connect(null);
        }

        /// <summary>
        /// Returns a string representation of the CPU state.
        /// </summary>
        /// <returns>
        /// A string showing current register values and processor status, typically formatted
        /// for debugging and logging purposes.
        /// </returns>
        /// <remarks>
        /// Delegates to the wrapped CPU's ToString implementation, which typically includes
        /// PC, SP, A, X, Y, and Status register values in a readable format.
        /// </remarks>
        public override string ToString()
        {
            return _oldCpu.ToString();
        }
    }
}
