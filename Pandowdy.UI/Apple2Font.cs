using System;
using System.Reflection;

namespace Pandowdy.UI;

/// <summary>
/// Apple II Enhanced Video ROM font data.
/// Contains the character bitmaps for the Apple II 40/80 column text modes.
/// Source: a2e_enh_video.rom (4096 bytes, but using first 2048 bytes)
/// 
/// The ROM file contains two font sets (4096 bytes total):
/// - First half (0-2047): 256 characters × 8 bytes = 2048 bytes (used)
/// - Second half (2048-4095): 256 characters × 8 bytes = 2048 bytes (reserved)
/// </summary>
public static class Apple2Font
{
    /// <summary>
    /// Apple II Enhanced Video ROM font data (2048 bytes).
    /// Each character occupies 8 bytes of bitmap data (7 pixels wide, 8 pixels tall).
    /// 256 characters × 8 bytes = 2048 bytes total.
    /// </summary>
    public static readonly byte[] FontData;

    /// <summary>
    /// Static constructor - loads the font ROM from embedded resources.
    /// </summary>
    static Apple2Font()
    {
        FontData = LoadFontRom();
    }

    /// <summary>
    /// Loads the Apple II Enhanced Video ROM from embedded resources.
    /// Reads the first 2048 bytes (256 characters).
    /// </summary>
    /// <returns>2048-byte array containing font data</returns>
    private static byte[] LoadFontRom()
    {
        // Try multiple possible resource names since the exact path depends on project structure
        string[] possibleResourceNames =
        [
            "Pandowdy.UI.Resources.a2e_enh_video.rom",  // Correct name
            "pandowdy.Resources.a2e_enh_video.rom",
            "pandowdy.a2e_enh_video.rom",
            "a2e_enh_video.rom"
        ];

        const int fontSize = 2048;
        const int fileSize = 4096;

        var assembly = Assembly.GetExecutingAssembly();

        foreach (var resourceName in possibleResourceNames)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                // Verify file size
                if (stream.Length != fileSize)
                {
                    throw new InvalidOperationException(
                        $"Font ROM '{resourceName}' is {stream.Length} bytes, expected {fileSize} bytes.");
                }

                // Read only the first 2048 bytes (256 characters)
                var fontData = new byte[fontSize];
                int bytesRead = stream.Read(fontData, 0, fontSize);

                if (bytesRead != fontSize)
                {
                    throw new InvalidOperationException(
                        $"Read {bytesRead} bytes, expected {fontSize} bytes from font ROM.");
                }

                return fontData;
            }
        }

        // If we get here, the resource wasn't found
        var allResources = assembly.GetManifestResourceNames();
        throw new InvalidOperationException(
            $"Failed to load embedded resource 'a2e_enh_video.rom'. " +
            $"Tried: {string.Join(", ", possibleResourceNames)}. " +
            $"Available resources: {string.Join(", ", allResources)}");
    }

    /// <summary>
    /// Gets the font bitmap data for a specific character.
    /// </summary>
    /// <param name="charCode">ASCII character code (0-255)</param>
    /// <returns>8-byte array containing the character bitmap (7 pixels wide, 8 pixels tall)</returns>
    /// <remarks>
    /// Each byte represents one row of the character, with bits 0-6 representing the 7 pixels
    /// (bit 0 = leftmost pixel, bit 6 = rightmost pixel). Bit 7 is unused.
    /// </remarks>
    public static byte[] GetCharacterBitmap(byte charCode)
    {
        var bitmap = new byte[8];
        Array.Copy(FontData, charCode * 8, bitmap, 0, 8);
        return bitmap;
    }

    /// <summary>
    /// Gets a single row of pixels for a character.
    /// </summary>
    /// <param name="charCode">ASCII character code (0-255)</param>
    /// <param name="row">Row index (0-7, where 0 is top row)</param>
    /// <returns>Byte representing 7 pixels (bits 0-6, where bit 0 is leftmost)</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if row is not in range 0-7.</exception>
    /// <remarks>
    /// <para>
    /// This is more efficient than <see cref="GetCharacterBitmap"/> when only a single row
    /// is needed for rendering or analysis.
    /// </para>
    /// <para>
    /// <strong>Bit Layout:</strong> Bit 0 (LSB) = leftmost pixel, Bit 6 = rightmost pixel, Bit 7 = unused.
    /// </para>
    /// </remarks>
    public static byte GetCharacterRow(byte charCode, int row)
    {
        if (row < 0 || row > 7)
        {
            throw new ArgumentOutOfRangeException(nameof(row), "Row must be 0-7");
        }

        return FontData[charCode * 8 + row];
    }
}
