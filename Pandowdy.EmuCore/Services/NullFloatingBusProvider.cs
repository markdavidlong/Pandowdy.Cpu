using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.Services;

/// <summary>
/// Simple floating bus provider that always returns zero.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Purpose:</strong> Provides a simple, deterministic floating bus implementation
/// for testing, debugging, or scenarios where floating bus accuracy is not required.
/// </para>
/// <para>
/// <strong>Behavior:</strong> Always returns 0x00 for unmapped reads, regardless of
/// CPU activity or video state. This is the simplest possible floating bus implementation.
/// </para>
/// <para>
/// <strong>Use Cases:</strong>
/// <list type="bullet">
/// <item>Unit testing - Predictable, deterministic behavior</item>
/// <item>Initial development - Simplest implementation to get started</item>
/// <item>Debugging - Eliminates floating bus complexity from troubleshooting</item>
/// <item>Performance baseline - Minimal overhead for benchmarking</item>
/// </list>
/// </para>
/// <para>
/// <strong>Limitations:</strong> This implementation is not accurate to Apple II hardware.
/// Software that relies on floating bus behavior (copy protection, timing loops, video
/// synchronization) will not work correctly. For production use, consider
/// <c>LastValueFloatingBus</c> or <c>CycleAccurateFloatingBus</c>.
/// </para>
/// <para>
/// <strong>Performance:</strong> Fastest possible implementation - no state, no logic,
/// just returns a constant. JIT compiler may inline this to a single instruction.
/// </para>
/// </remarks>
public sealed class NullFloatingBusProvider : IFloatingBusProvider
{
    /// <summary>
    /// Reads the floating bus value (always returns 0).
    /// </summary>
    /// <returns>Always returns 0x00.</returns>
    /// <remarks>
    /// This implementation ignores all bus activity and always returns zero.
    /// This is not accurate to Apple II hardware behavior, where the floating bus
    /// would typically return the last value on the data bus.
    /// </remarks>
    public byte Read() => 0;
}
