namespace Pandowdy.EmuCore.Interfaces;

/// <summary>
/// Provides direct access to the physical memory pools, bypassing the bus mapping
/// and soft switch logic used during normal CPU access.
/// </summary>
/// <remarks>
/// This interface is intended for components that need to read the raw physical memory
/// banks (main and auxiliary RAM) without the address translation, bank switching, and
/// soft switch logic that the bus normally applies. This is useful for:
/// <list type="bullet">
/// <item>Video rendering code that needs to scan video memory pages directly</item>
/// <item>Debugging tools that need to inspect physical memory layout</item>
/// <item>Memory dump utilities that need raw memory contents</item>
/// </list>
/// <para>
/// <strong>Important Memory Banking Detail:</strong> The Apple IIe language card provides
/// two 4KB banks of RAM that can be mapped into the $D000-$DFFF address range. However,
/// in physical memory layout:
/// <list type="bullet">
/// <item>Bank 1 is physically stored at $C000-$CFFF</item>
/// <item>Bank 2 is physically stored at $D000-$DFFF</item>
/// </list>
/// When reading directly using this interface, you must account for this physical layout.
/// If you want to read from language card bank 1 at logical address $D000, you must
/// physically read from $C000.
/// </para>
/// </remarks>
public interface IDirectMemoryPoolReader
{
    /// <summary>
    /// Reads a byte from the main memory pool, bypassing all bus mapping and soft switches.
    /// </summary>
    /// <param name="address">The physical address to read from ($0000-$FFFF). Note that
    /// for the language card region, bank 1 ($D000-$DFFF logical) is physically located
    /// at $C000-$CFFF, while bank 2 ($D000-$DFFF logical) is at $D000-$DFFF physical.</param>
    /// <returns>The byte value at the specified physical address in main memory, or 0
    /// if the address is not available (e.g., ROM regions with no RAM underneath).</returns>
    /// <remarks>
    /// This method reads directly from the main 64KB memory pool without any address
    /// translation, auxiliary memory selection, or bank switching logic. It always reads
    /// from main memory, never auxiliary memory, and ignores all soft switch states
    /// (RAMRD, RAMWRT, 80STORE, etc.).
    /// <para>
    /// Use this when you need to access the actual physical memory layout, such as when
    /// rendering video from specific memory pages or debugging memory contents.
    /// </para>
    /// </remarks>
    public byte ReadRawMain(int address);

    /// <summary>
    /// Reads a byte from the auxiliary memory pool, bypassing all bus mapping and soft switches.
    /// </summary>
    /// <param name="address">The physical address to read from ($0000-$FFFF). Note that
    /// for the language card region, bank 1 ($D000-$DFFF logical) is physically located
    /// at $C000-$CFFF, while bank 2 ($D000-$DFFF logical) is at $D000-$DFFF physical.</param>
    /// <returns>The byte value at the specified physical address in auxiliary memory, or 0
    /// if the address is not available (e.g., addresses where no auxiliary RAM exists).</returns>
    /// <remarks>
    /// This method reads directly from the auxiliary 64KB memory pool without any address
    /// translation, auxiliary memory selection, or bank switching logic. It always reads
    /// from auxiliary memory, never main memory, and ignores all soft switch states
    /// (RAMRD, RAMWRT, 80STORE, etc.).
    /// <para>
    /// The Apple IIe's auxiliary memory is primarily used for 80-column text mode,
    /// double hi-res graphics, and extended memory for programs. Use this method when
    /// you need direct access to the auxiliary memory bank, such as when rendering
    /// 80-column text or double hi-res graphics.
    /// </para>
    /// </remarks>
    public byte ReadRawAux(int address);
}

