namespace Pandowdy.EmuCore.Interfaces;

/// <summary>
/// Provides floating bus data for unmapped or undriven memory/I/O reads.
/// </summary>
/// <remarks>
/// <para>
/// <strong>What is the Floating Bus?</strong>
/// </para>
/// <para>
/// In real Apple II hardware, reading from unmapped memory addresses or undriven I/O locations
/// does not return a fixed value like 0x00 or 0xFF. Instead, the data bus "floats" and returns
/// the last value that was present on the bus from a previous access. This is typically the
/// most recent memory read by either the CPU or the video scanner.
/// </para>
/// <para>
/// <strong>Why It Matters for Emulation:</strong>
/// </para>
/// <list type="bullet">
/// <item><strong>Software Compatibility:</strong> Many Apple II programs rely on floating bus
/// behavior for copy protection, hardware detection, and timing-sensitive operations.</item>
/// <item><strong>Video Effects:</strong> Some software reads video memory through the floating
/// bus during display refresh cycles to create visual effects or save memory.</item>
/// <item><strong>Emulator Detection:</strong> Copy protection routines often test for floating
/// bus behavior to distinguish real hardware from emulators that return zero.</item>
/// </list>
/// <para>
/// <strong>Implementation Strategies:</strong>
/// </para>
/// <para>
/// Different implementations can provide varying levels of accuracy:
/// </para>
/// <list type="table">
/// <listheader>
/// <term>Strategy</term>
/// <description>Accuracy vs Performance</description>
/// </listheader>
/// <item>
/// <term>LastValueBus</term>
/// <description>Tracks the last CPU read/write value. Fast, moderately accurate.</description>
/// </item>
/// <item>
/// <term>VideoScannerBus</term>
/// <description>Returns the value being scanned by the video circuitry based on cycle timing.
/// Accurate but requires cycle-perfect emulation.</description>
/// </item>
/// <item>
/// <term>ZeroBus</term>
/// <description>Always returns 0x00. Fast but breaks compatibility with floating bus-dependent software.</description>
/// </item>
/// <item>
/// <term>RandomBus</term>
/// <description>Returns random values. Useful for testing robustness but not accurate.</description>
/// </item>
/// </list>
/// <para>
/// <strong>Usage Example:</strong>
/// </para>
/// <code>
/// // In memory read logic:
/// public byte Read(ushort address)
/// {
///     if (IsUnmappedAddress(address))
///     {
///         // Return floating bus value instead of 0
///         return _floatingBusProvider.Read();
///     }
///     return _memory[address];
/// }
/// 
/// // In I/O space handler:
/// public byte ReadIO(ushort address)
/// {
///     if (!HasIOHandler(address))
///     {
///         // Unhandled I/O returns floating bus
///         return _floatingBusProvider.Read();
///     }
///     return IOHandlers[address].Read();
/// }
/// </code>
/// <para>
/// <strong>Real-World Example: 80-Column Ghost Characters</strong>
/// </para>
/// <para>
/// On an Apple IIe without an 80-column card, enabling 80-column mode causes characters to
/// appear doubled (ghosted) because:
/// <list type="number">
/// <item>Video scanner alternates reading main memory (even columns) and aux memory (odd columns)</item>
/// <item>Without aux memory installed, aux reads return the floating bus value</item>
/// <item>The floating bus contains the character just read from main memory</item>
/// <item>Result: Each character appears twice: <c>HELLO</c> becomes <c>HHEELLLLOO</c></item>
/// </list>
/// An emulator returning 0 for unmapped reads would show spaces instead of doubled characters,
/// breaking visual compatibility.
/// </para>
/// <para>
/// <strong>Thread Safety:</strong>
/// </para>
/// <para>
/// Implementations are not required to be thread-safe. The emulator core should ensure
/// single-threaded access, or provide synchronization at a higher level.
/// </para>
/// </remarks>
public interface IFloatingBusProvider
{
    /// <summary>
    /// Reads the current floating bus value.
    /// </summary>
    /// <returns>
    /// The byte value that would appear on the data bus for an unmapped or undriven read.
    /// This is typically the last value placed on the bus by the CPU or video scanner.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>When to Call:</strong> Call this method whenever the emulator needs to read
    /// from an address or I/O location that has no active hardware driving the data bus:
    /// </para>
    /// <list type="bullet">
    /// <item>Unmapped memory regions (e.g., reads beyond installed RAM)</item>
    /// <item>Unimplemented I/O addresses (e.g., empty expansion slots)</item>
    /// <item>Auxiliary memory reads when no auxiliary memory is installed</item>
    /// <item>Peripheral soft switches that don't implement all 16 addresses</item>
    /// </list>
    /// <para>
    /// <strong>Implementation Notes:</strong>
    /// </para>
    /// <para>
    /// Simple implementations may return the last CPU read/write value. More sophisticated
    /// implementations can track video scanner activity to return the actual video memory
    /// being scanned at the current cycle, which is more accurate but requires cycle-perfect
    /// timing simulation.
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> This method may be called frequently (millions of times
    /// per second in a running emulator), so implementations should be fast. Avoid complex
    /// calculations or I/O operations.
    /// </para>
    /// </remarks>
    public byte Read();
}
