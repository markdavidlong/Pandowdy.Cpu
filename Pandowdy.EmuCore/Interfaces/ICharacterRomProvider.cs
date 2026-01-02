using System;

namespace Pandowdy.EmuCore.Interfaces
{
    /// <summary>
    /// Provides access to the Apple II character ROM for text rendering
    /// </summary>
    public interface ICharacterRomProvider
    {
        /// <summary>
        /// Get the character ROM data for a specific character
        /// </summary>
        /// <param name="ch">Character code</param>
        /// <param name="flashOn">Whether flash mode is active</param>
        /// <param name="altChar">Whether alternate character set is active</param>
        /// <returns>8 bytes representing the character glyph (one byte per scanline)</returns>
        ReadOnlySpan<byte> GetGlyph(byte ch, bool flashOn, bool altChar);
    }
}
