using System.Reflection;

using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.Services;

/// <summary>
/// Loads Apple IIe enhanced character ROM from embedded resources and provides glyph access.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Resource:</strong> Loads "a2e_enh_video.rom" (2048 bytes) from assembly manifest 
/// resources at construction. The character ROM contains 256 character definitions, each 8 bytes tall.
/// </para>
/// <para>
/// <strong>Performance:</strong> Character ROM is cached in memory for fast O(1) glyph access.
/// All GetGlyph() calls return zero-copy views into the cached ROM buffer.
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> Thread-safe for reads after construction. The ROM buffer is 
/// immutable and never modified after initialization.
/// </para>
/// </remarks>
public class CharacterRomProvider : ICharacterRomProvider
{
    private readonly byte[] _characterRom;

    /// <summary>
    /// Initializes a new instance and loads the Apple IIe enhanced character ROM.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the embedded ROM resource "a2e_enh_video.rom" cannot be found in the assembly.
    /// This typically indicates a build configuration issue where the ROM file was not embedded.
    /// </exception>
    public CharacterRomProvider()
    {
        _characterRom = LoadCharacterRom();
    }

    /// <summary>
    /// Loads the 2048-byte Apple IIe enhanced character ROM from embedded assembly resources.
    /// </summary>
    /// <returns>
    /// A 2048-byte array containing 256 character definitions (8 bytes each), loaded from
    /// the embedded resource "Pandowdy.EmuCore.Resources.a2e_enh_video.rom".
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the embedded resource is not found in the assembly.
    /// </exception>
    private static byte[] LoadCharacterRom()
    {
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream("Pandowdy.EmuCore.Resources.a2e_enh_video.rom") ?? throw new InvalidOperationException(
                "Character ROM resource not found. Ensure 'a2e_enh_video.rom' is embedded " +
                "as a manifest resource in Pandowdy.EmuCore with build action 'Embedded Resource'.");

        byte[] buffer = new byte[stream.Length];
        stream.Read(buffer, 0, buffer.Length);
        return buffer;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>Implementation Note:</strong> This method handles Apple IIe character ROM quirks
    /// by adjusting the character code before lookup. The ROM is organized with inverse and normal
    /// characters in non-sequential positions, requiring remapping for certain character ranges.
    /// </para>
    /// <para>
    /// <strong>Character Remapping:</strong> When not using the alternate character set (MouseText),
    /// characters in the range 0x40-0x7F (flashing characters) are remapped:
    /// <list type="bullet">
    /// <item>Bit 6 is cleared (maps to 0x00-0x3F range in ROM)</item>
    /// <item>Bit 7 is set based on flash state: set when flash is OFF (inverse display), 
    ///       clear when flash is ON (normal display)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> Returns a ReadOnlySpan view into the cached ROM buffer (zero-copy).
    /// The span lifetime is tied to the CharacterRomProvider instance.
    /// </para>
    /// </remarks>
    public ReadOnlySpan<byte> GetGlyph(byte ch, bool flashOn, bool altChar)
    {
        // Apple IIe character ROM quirk: Characters 0x40-0x7F need special handling
        // unless we're using the alternate character set (MouseText)
        if (!altChar)
        {
            if (ch >= 0x40 && ch < 0x80)
            {
                ch &= 0x3f;           // Clear bit 6 (map to 0x00-0x3F range in ROM)
                if (!flashOn)
                {
                    ch |= 0x80;       // Set bit 7 when flash is off (inverse display)
                }
            }
        }
        
        // Return 8-byte glyph (zero-copy view into ROM buffer)
        return _characterRom.AsSpan(ch * 8, 8);
    }
}
