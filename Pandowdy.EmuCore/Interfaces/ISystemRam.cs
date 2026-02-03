namespace Pandowdy.EmuCore.Interfaces;

/// <summary>
/// Represents a contiguous RAM bank with bulk copy capability for efficient memory snapshots.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Purpose:</strong> Extends the base <see cref="IPandowdyMemory"/> interface with a bulk copy
/// operation that enables efficient memory snapshots for video rendering. The Apple IIe emulator
/// captures video memory at VBlank (~60 Hz), requiring fast memory copies to minimize emulator
/// thread blocking.
/// </para>
/// <para>
/// <strong>Apple IIe Memory Banks:</strong> The Apple IIe has two 48KB RAM banks:
/// <list type="bullet">
/// <item><strong>Main RAM ($0000-$BFFF):</strong> Primary system memory containing zero page,
/// stack, text pages, and hi-res graphics pages</item>
/// <item><strong>Auxiliary RAM ($0000-$BFFF):</strong> Extended memory for 80-column mode,
/// double hi-res graphics, and additional program storage</item>
/// </list>
/// </para>
/// <para>
/// <strong>Performance:</strong> The <see cref="CopyIntoSpan"/> method uses Span-based memory
/// operations which compile to efficient native memory copies (similar to memcpy). A 48KB copy
/// typically takes 1-3 microseconds on modern CPUs.
/// </para>
/// </remarks>
/// <seealso cref="ISystemRamSelector"/>
/// <seealso cref="IPandowdyMemory"/>
public interface ISystemRam : IPandowdyMemory
{
    /// <summary>
    /// Copies the entire contents of this RAM bank into the provided destination span.
    /// </summary>
    /// <param name="destination">
    /// A span of bytes to receive the RAM contents. Must be at least as large as the RAM bank
    /// (typically 48KB / 0xC000 bytes for Apple IIe memory banks).
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown if the destination span is smaller than the RAM bank size.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <strong>Usage:</strong> Called by the video memory snapshot system to capture RAM state
    /// for threaded rendering. This allows the renderer to work from a stable copy while the
    /// emulator continues executing.
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> The caller is responsible for ensuring no concurrent
    /// writes to the RAM bank during the copy. Typically called during VBlank when the CPU
    /// is at a safe point.
    /// </para>
    /// </remarks>
    public void CopyIntoSpan(Span<byte> destination);
}
