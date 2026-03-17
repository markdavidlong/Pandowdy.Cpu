// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Machine;

namespace Pandowdy.EmuCore.Memory;

/// <summary>
/// Floating bus provider that returns random values for each read.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Purpose:</strong> Provides a randomized floating bus implementation that simulates
/// unpredictable bus noise, useful for testing software robustness and detecting code that
/// incorrectly relies on specific floating bus values.
/// </para>
/// <para>
/// <strong>Behavior:</strong> Returns a random byte value (0x00-0xFF) for each read operation,
/// simulating the unpredictable nature of reading from unmapped memory addresses where the
/// data bus may contain arbitrary values from previous bus activity.
/// </para>
/// <para>
/// <strong>Use Cases:</strong>
/// <list type="bullet">
/// <item>Testing - Verifies software doesn't depend on specific floating bus values</item>
/// <item>Debugging - Helps identify uninitialized memory reads or bus conflicts</item>
/// <item>Robustness validation - Ensures code handles arbitrary floating bus data</item>
/// <item>Copy protection analysis - Some protection schemes test for random vs. deterministic behavior</item>
/// </list>
/// </para>
/// <para>
/// <strong>Limitations:</strong> This implementation is not accurate to Apple II hardware behavior.
/// Real hardware floating bus values are not truly random - they reflect the last value on the
/// data bus (often video data during screen refresh). Software that relies on specific floating
/// bus patterns (video synchronization, timing loops, some copy protection) will not work correctly.
/// For production use, consider <c>LastValueFloatingBus</c> or <c>CycleAccurateFloatingBus</c>.
/// </para>
/// <para>
/// <strong>Randomness:</strong> Uses <see cref="Random"/> with a time-based seed for
/// non-cryptographic randomness. Each instance maintains its own random number generator
/// to ensure deterministic behavior within a single emulator session if needed.
/// </para>
/// <para>
/// <strong>Performance:</strong> Slightly slower than <see cref="NullFloatingBusProvider"/> due to
/// random number generation overhead, but still negligible compared to other emulation operations.
/// The random number generator is instance-scoped to avoid thread-safety issues.
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> Not thread-safe. Each instance should be used by a single thread
/// (typically the emulator worker thread). The internal <see cref="Random"/> instance is not
/// synchronized for performance reasons.
/// </para>
/// </remarks>
public sealed class RandomFloatingBusProvider : IFloatingBusProvider
{
    /// <summary>
    /// Random number generator for producing floating bus values.
    /// </summary>
    private readonly Random _random;

    /// <summary>
    /// Initializes a new instance of the <see cref="RandomFloatingBusProvider"/> class.
    /// </summary>
    /// <remarks>
    /// Creates a new random number generator with a time-based seed. Each instance will
    /// produce a different sequence of values, making each emulator session unique.
    /// </remarks>
    public RandomFloatingBusProvider()
    {
        _random = new Random();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RandomFloatingBusProvider"/> class with a specific seed.
    /// </summary>
    /// <param name="seed">Seed value for the random number generator.</param>
    /// <remarks>
    /// <para>
    /// Using a specific seed allows for reproducible behavior across multiple runs, which is
    /// useful for:
    /// <list type="bullet">
    /// <item>Debugging - Reproduce specific floating bus sequences that trigger bugs</item>
    /// <item>Testing - Ensure consistent test results across runs</item>
    /// <item>Save states - Maintain consistent floating bus behavior when loading saved state</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Example:</strong>
    /// <code>
    /// // Create deterministic floating bus provider for testing
    /// var floatingBus = new RandomFloatingBusProvider(12345);
    /// 
    /// // Same seed produces same sequence
    /// var floatingBus2 = new RandomFloatingBusProvider(12345);
    /// Assert.Equal(floatingBus.Read(), floatingBus2.Read());
    /// </code>
    /// </para>
    /// </remarks>
    public RandomFloatingBusProvider(int seed)
    {
        _random = new Random(seed);
    }

    /// <summary>
    /// Reads the floating bus value (returns a random byte).
    /// </summary>
    /// <returns>A random byte value between 0x00 and 0xFF.</returns>
    /// <remarks>
    /// <para>
    /// Each call returns a new random value. This simulates the unpredictable nature of
    /// reading from unmapped memory where the data bus contains arbitrary values.
    /// </para>
    /// <para>
    /// <strong>Distribution:</strong> Uses uniform distribution across the full byte range
    /// (0-255). All values have equal probability.
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> Generates a random integer and masks to byte range.
    /// Typical execution time is ~10-20ns on modern processors.
    /// </para>
    /// </remarks>
    public byte Read() => (byte)_random.Next(256);
}
