namespace Pandowdy.EmuCore.Interfaces;

/// <summary>
/// Provides access to the Apple IIe character ROM for text rendering.
/// </summary>
/// <remarks>
/// The Apple IIe contains a character set stored in ROM which contains glyphs for a primary set
/// and an alternate (MouseText) set. Each character is defined by a 7x8 pixel bitmap,
/// represented as 8 bytes (one byte per scanline with the high bit ignored). This interface
/// provides methods to retrieve character glyphs with support for flash mode (inverse video blinking)
/// and the alternate character set.
/// </remarks>
public interface ICharacterRomProvider
{
    /// <summary>
    /// Gets the character ROM data for a specific character.
    /// </summary>
    /// <param name="ch">The character code (0-255) to retrieve. This is the Apple II
    /// screen code (not ASCII), which determines the glyph appearance based on inverse,
    /// flashing, and alternate character set attributes.</param>
    /// <param name="flashOn">True if flash mode (inverse video) is currently active.
    /// When true, flashing characters (codes 0x40-0x7F) are displayed in their inverse
    /// state. Flash typically alternates at approximately 2 Hz.</param>
    /// <param name="altChar">True if the alternate character set (MouseText) should be used.
    /// MouseText provides graphical symbols for user interfaces, replacing control
    /// characters in the range 0x40-0x5F when enabled via the SETALTCHAR soft switch.</param>
    /// <returns>A read-only span of 8 bytes representing the character glyph, where each
    /// byte represents one horizontal scanline from top to bottom. Bits 0-6 of each byte
    /// represent pixels (1=lit, 0=dark), with bit 6 being the leftmost pixel. Bit 7 is ignored.</returns>
    /// <remarks>
    /// The character ROM layout is organized by display attributes, not ASCII values:
    /// <list type="bullet">
    /// <item>0x00-0x1F: Control characters (displayed as inverse uppercase when altChar is false)</item>
    /// <item>0x20-0x3F: Inverse special characters and numbers</item>
    /// <item>0x40-0x5F: Flashing uppercase letters (or MouseText when altChar is true)</item>
    /// <item>0x60-0x7F: Flashing special characters</item>
    /// <item>0x80-0x9F: Control characters displayed as normal uppercase</item>
    /// <item>0xA0-0xBF: Normal special characters and numbers</item>
    /// <item>0xC0-0xDF: Normal uppercase letters</item>
    /// <item>0xE0-0xFF: Normal lowercase letters and special characters</item>
    /// </list>
    /// </remarks>
    ReadOnlySpan<byte> GetGlyph(byte ch, bool flashOn, bool altChar);
}
