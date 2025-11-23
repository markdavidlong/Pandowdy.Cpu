using System;
using System.Reflection;

namespace Pandowdy.Core;

/// <summary>
/// Core-side Apple II Enhanced Video font access for glyph composition.
/// Provides raw 8x7 glyph rows (8 bytes per character).
/// </summary>
public static class VideoFont
{
    public static readonly byte[] FontData; // 2048 bytes (256 chars * 8)

    static VideoFont()
    {
        FontData = LoadFont();
    }

    private static byte[] LoadFont()
    {
        var asm = Assembly.GetExecutingAssembly();
        // Resource name aligned with project embedding
        string[] candidates =
        {
            "Pandowdy.Core.Resources.a2e_enh_video.rom",
            "Pandowdy.Core.a2e_enh_video.rom",
            "a2e_enh_video.rom"
        };
        foreach (var name in candidates)
        {
            using var s = asm.GetManifestResourceStream(name);
            if (s == null)
            {
                continue;
            }

            if (s.Length < 2048)
            {
                throw new InvalidOperationException($"Video ROM '{name}' unexpected size {s.Length}");
            }

            var buf = new byte[2048];
            int read = s.Read(buf, 0, 2048);
            if (read != 2048)
            {
                throw new InvalidOperationException("Incomplete font read");
            }

            return buf;
        }
        throw new InvalidOperationException("Enhanced video ROM resource not found for glyph composition.");
    }

    public static byte[] Glyph(byte ch)
        => FontData.AsSpan(ch * 8, 8).ToArray();

    public static (byte[], byte[]) GetGlyphPair(byte ch, bool altCharSet = false)
            {
        if (!altCharSet)
        {
            return (Glyph(ch), Glyph(ch));
        }
        else
        {
            return (Glyph(ch), Glyph(ch));
        }
    }
}
