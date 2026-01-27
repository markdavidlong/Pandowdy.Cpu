namespace Pandowdy.Cpu;

/// <summary>
/// Interface for CPU bus operations (memory and I/O access).
/// </summary>
public interface IPandowdyCpuBus
{
    /// <summary>
    /// Reads a byte from the specified address.
    /// </summary>
    /// <param name="address">The 16-bit address to read from.</param>
    /// <returns>The byte value at the specified address.</returns>
    byte CpuRead(ushort address);

    /// <summary>
    /// Writes a byte to the specified address.
    /// </summary>
    /// <param name="address">The 16-bit address to write to.</param>
    /// <param name="value">The byte value to write.</param>
    void Write(ushort address, byte value);
}
