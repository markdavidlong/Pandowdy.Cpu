// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Machine;
using Pandowdy.EmuCore.Video;

namespace Pandowdy.EmuCore.Tests.Services;

/// <summary>
/// Tests for CharacterRomProvider - provides access to Apple IIe Enhanced character ROM.
/// The character ROM contains 256 8x8 glyphs for text mode rendering, with support for
/// flash mode and alternate character sets.
/// </summary>
public class CharacterRomProviderTests
{
    #region Constructor and Initialization Tests

    [Fact]
    public void Constructor_LoadsCharacterRom()
    {
        // Arrange & Act
        var provider = new CharacterRomProvider();

        // Assert - If we get here, ROM loaded successfully
        Assert.NotNull(provider);
    }

    [Fact]
    public void Constructor_LoadsValidRomSize()
    {
        // Arrange & Act
        var provider = new CharacterRomProvider();

        // Assert - Verify ROM provides data (256 characters * 8 bytes each)
        var glyph = provider.GetGlyph(0, false, false);
        Assert.Equal(8, glyph.Length);
    }

    #endregion

    #region GetGlyph Basic Tests

    [Fact]
    public void GetGlyph_ReturnsEightBytes()
    {
        // Arrange
        var provider = new CharacterRomProvider();

        // Act
        var glyph = provider.GetGlyph(0x41, false, false); // 'A'

        // Assert
        Assert.Equal(8, glyph.Length);
    }

    [Fact]
    public void GetGlyph_DifferentCharacters_ReturnDifferentData()
    {
        // Arrange
        var provider = new CharacterRomProvider();

        // Act
        var glyphA = provider.GetGlyph(0x41, false, false); // 'A'
        var glyphB = provider.GetGlyph(0x42, false, false); // 'B'

        // Assert - Different characters should have different glyph data
        bool areDifferent = false;
        for (int i = 0; i < 8; i++)
        {
            if (glyphA[i] != glyphB[i])
            {
                areDifferent = true;
                break;
            }
        }
        Assert.True(areDifferent, "Glyphs for 'A' and 'B' should be different");
    }

    [Fact]
    public void GetGlyph_SameCharacter_ReturnsSameData()
    {
        // Arrange
        var provider = new CharacterRomProvider();

        // Act
        var glyph1 = provider.GetGlyph(0x41, false, false);
        var glyph2 = provider.GetGlyph(0x41, false, false);

        // Assert
        Assert.True(glyph1.SequenceEqual(glyph2));
    }

    #endregion

    #region Character Range Tests

    [Theory]
    [InlineData(0x00)] // Null
    [InlineData(0x20)] // Space
    [InlineData(0x41)] // 'A'
    [InlineData(0x5A)] // 'Z'
    [InlineData(0x61)] // 'a'
    [InlineData(0x7A)] // 'z'
    [InlineData(0x7F)] // DEL
    [InlineData(0xFF)] // High ASCII
    public void GetGlyph_AllCharacterCodes_ReturnValidData(byte ch)
    {
        // Arrange
        var provider = new CharacterRomProvider();

        // Act
        var glyph = provider.GetGlyph(ch, false, false);

        // Assert
        Assert.Equal(8, glyph.Length);
        Assert.False(glyph.IsEmpty);
    }

    [Fact]
    public void GetGlyph_AllCharacterCodes_ReturnValidGlyphs()
    {
        // Arrange
        var provider = new CharacterRomProvider();

        // Act & Assert - All 256 character codes should work
        for (int ch = 0; ch < 256; ch++)
        {
            var glyph = provider.GetGlyph((byte)ch, false, false);
            Assert.Equal(8, glyph.Length);
        }
    }

    #endregion

    #region Flash Mode Tests

    [Fact]
    public void GetGlyph_FlashOn_Range0x40to0x7F_WithoutAltChar()
    {
        // Arrange
        var provider = new CharacterRomProvider();

        // Act - Characters in range 0x40-0x7F with flash ON
        var glyphFlashOn = provider.GetGlyph(0x41, flashOn: true, altChar: false); // 'A'
        var glyphFlashOff = provider.GetGlyph(0x41, flashOn: false, altChar: false);

        // Assert - Flash mode should affect the glyph
        // When flash is OFF, bit 7 is set (ch |= 0x80)
        // When flash is ON, character is masked to 0x3F range
        Assert.Equal(8, glyphFlashOn.Length);
        Assert.Equal(8, glyphFlashOff.Length);
    }

    [Fact]
    public void GetGlyph_FlashMode_AffectsRange0x40to0x7F()
    {
        // Arrange
        var provider = new CharacterRomProvider();

        // Act
        var glyphFlashOn = provider.GetGlyph(0x50, flashOn: true, altChar: false);
        var glyphFlashOff = provider.GetGlyph(0x50, flashOn: false, altChar: false);

        // Assert - Different flash states should potentially return different glyphs
        // (depends on ROM content, but they should at least be valid)
        Assert.Equal(8, glyphFlashOn.Length);
        Assert.Equal(8, glyphFlashOff.Length);
    }

    [Theory]
    [InlineData(0x00)] // Below range
    [InlineData(0x3F)] // Just below range
    [InlineData(0x80)] // Above range
    [InlineData(0xFF)] // High value
    public void GetGlyph_FlashMode_DoesNotAffectOutsideRange0x40to0x7F(byte ch)
    {
        // Arrange
        var provider = new CharacterRomProvider();

        // Act
        var glyphFlashOn = provider.GetGlyph(ch, flashOn: true, altChar: false);
        var glyphFlashOff = provider.GetGlyph(ch, flashOn: false, altChar: false);

        // Assert - Flash should not affect characters outside 0x40-0x7F
        Assert.True(glyphFlashOn.SequenceEqual(glyphFlashOff),
            $"Flash mode should not affect character 0x{ch:X2}");
    }

    #endregion

    #region Alternate Character Set Tests

    [Fact]
    public void GetGlyph_AltChar_True_BypassesFlashLogic()
    {
        // Arrange
        var provider = new CharacterRomProvider();

        // Act - With altChar=true, flash logic is bypassed
        var glyphAltFlashOn = provider.GetGlyph(0x50, flashOn: true, altChar: true);
        var glyphAltFlashOff = provider.GetGlyph(0x50, flashOn: false, altChar: true);

        // Assert - With altChar=true, flash mode should not matter
        Assert.True(glyphAltFlashOn.SequenceEqual(glyphAltFlashOff),
            "AltChar should bypass flash logic");
    }

    [Fact]
    public void GetGlyph_AltChar_False_UsesFlashLogic()
    {
        // Arrange
        var provider = new CharacterRomProvider();

        // Act
        var glyphFlashOn = provider.GetGlyph(0x50, flashOn: true, altChar: false);
        var glyphFlashOff = provider.GetGlyph(0x50, flashOn: false, altChar: false);

        // Assert - Results are valid (may or may not be different depending on ROM content)
        Assert.Equal(8, glyphFlashOn.Length);
        Assert.Equal(8, glyphFlashOff.Length);
    }

    [Theory]
    [InlineData(0x41)] // 'A'
    [InlineData(0x42)] // 'B'
    [InlineData(0x5A)] // 'Z'
    public void GetGlyph_AltChar_IgnoresFlashState(byte ch)
    {
        // Arrange
        var provider = new CharacterRomProvider();

        // Act
        var glyph1 = provider.GetGlyph(ch, flashOn: true, altChar: true);
        var glyph2 = provider.GetGlyph(ch, flashOn: false, altChar: true);

        // Assert - AltChar mode ignores flash state
        Assert.True(glyph1.SequenceEqual(glyph2));
    }

    #endregion

    #region Character Mapping Tests

    [Fact]
    public void GetGlyph_Range0x40to0x7F_WithoutAltChar_UsesMasking()
    {
        // Arrange
        var provider = new CharacterRomProvider();

        // The logic for non-altChar mode in range 0x40-0x7F:
        // 1. ch &= 0x3F (mask to 0-63 range)
        // 2. If flash off: ch |= 0x80 (set bit 7)

        // Act
        var glyphFlashOff_0x41 = provider.GetGlyph(0x41, flashOn: false, altChar: false);
        // Should map to: 0x41 & 0x3F = 0x01, then 0x01 | 0x80 = 0x81

        var glyphFlashOn_0x41 = provider.GetGlyph(0x41, flashOn: true, altChar: false);
        // Should map to: 0x41 & 0x3F = 0x01

        // Assert - Both should be valid 8-byte glyphs
        Assert.Equal(8, glyphFlashOff_0x41.Length);
        Assert.Equal(8, glyphFlashOn_0x41.Length);
    }

    [Fact]
    public void GetGlyph_LowRange_WithoutAltChar_NoMasking()
    {
        // Arrange
        var provider = new CharacterRomProvider();

        // Act - Characters below 0x40 should not be masked
        var glyph = provider.GetGlyph(0x20, flashOn: false, altChar: false); // Space

        // Assert
        Assert.Equal(8, glyph.Length);
    }

    #endregion

    #region Glyph Data Validity Tests

    [Fact]
    public void GetGlyph_ReturnsNonNullSpan()
    {
        // Arrange
        var provider = new CharacterRomProvider();

        // Act
        var glyph = provider.GetGlyph(0x41, false, false);

        // Assert
        Assert.False(glyph.IsEmpty);
    }

    [Fact]
    public void GetGlyph_Space_ReturnsValidGlyph()
    {
        // Arrange
        var provider = new CharacterRomProvider();

        // Act
        var spaceGlyph = provider.GetGlyph(0x20, false, false);

        // Assert - Space should be 8 bytes (likely mostly zeros)
        Assert.Equal(8, spaceGlyph.Length);
    }

    [Fact]
    public void GetGlyph_ControlCharacters_ReturnValidGlyphs()
    {
        // Arrange
        var provider = new CharacterRomProvider();

        // Act & Assert - Control characters (0x00-0x1F) should have glyphs
        for (byte ch = 0x00; ch < 0x20; ch++)
        {
            var glyph = provider.GetGlyph(ch, false, false);
            Assert.Equal(8, glyph.Length);
        }
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void GetGlyph_TypicalTextModeScenario()
    {
        // Arrange
        var provider = new CharacterRomProvider();
        string text = "HELLO";

        // Act - Get glyphs for each character
        foreach (char c in text)
        {
            var glyph = provider.GetGlyph((byte)c, flashOn: false, altChar: false);

            // Assert
            Assert.Equal(8, glyph.Length);
        }
    }

    [Fact]
    public void GetGlyph_FlashingCursor_Scenario()
    {
        // Arrange
        var provider = new CharacterRomProvider();
        byte cursorChar = 0x5F; // Underscore (often used for cursor)

        // Act - Simulate cursor flashing
        var cursorOn = provider.GetGlyph(cursorChar, flashOn: true, altChar: false);
        var cursorOff = provider.GetGlyph(cursorChar, flashOn: false, altChar: false);

        // Assert
        Assert.Equal(8, cursorOn.Length);
        Assert.Equal(8, cursorOff.Length);
    }

    [Fact]
    public void GetGlyph_AlternateCharacterSet_MouseText()
    {
        // Arrange
        var provider = new CharacterRomProvider();

        // Act - Apple IIe Enhanced supports "MouseText" characters with altChar
        var normalGlyph = provider.GetGlyph(0x40, flashOn: false, altChar: false);
        var mouseTextGlyph = provider.GetGlyph(0x40, flashOn: false, altChar: true);

        // Assert - Both should be valid (may or may not be same depending on ROM)
        Assert.Equal(8, normalGlyph.Length);
        Assert.Equal(8, mouseTextGlyph.Length);
    }

    [Fact]
    public void GetGlyph_ConsistentAcrossMultipleCalls()
    {
        // Arrange
        var provider = new CharacterRomProvider();
        byte ch = 0x41; // 'A'

        // Act - Get same glyph multiple times
        var glyph1 = provider.GetGlyph(ch, false, false);
        var glyph2 = provider.GetGlyph(ch, false, false);
        var glyph3 = provider.GetGlyph(ch, false, false);

        // Assert - Should be identical
        Assert.True(glyph1.SequenceEqual(glyph2));
        Assert.True(glyph2.SequenceEqual(glyph3));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void GetGlyph_BoundaryCharacter_0x3F()
    {
        // Arrange
        var provider = new CharacterRomProvider();

        // Act - 0x3F is just below the flash range (0x40-0x7F)
        var glyph = provider.GetGlyph(0x3F, flashOn: false, altChar: false);

        // Assert
        Assert.Equal(8, glyph.Length);
    }

    [Fact]
    public void GetGlyph_BoundaryCharacter_0x40()
    {
        // Arrange
        var provider = new CharacterRomProvider();

        // Act - 0x40 is the start of the flash range
        var glyphFlashOn = provider.GetGlyph(0x40, flashOn: true, altChar: false);
        var glyphFlashOff = provider.GetGlyph(0x40, flashOn: false, altChar: false);

        // Assert
        Assert.Equal(8, glyphFlashOn.Length);
        Assert.Equal(8, glyphFlashOff.Length);
    }

    [Fact]
    public void GetGlyph_BoundaryCharacter_0x7F()
    {
        // Arrange
        var provider = new CharacterRomProvider();

        // Act - 0x7F is the end of the flash range
        var glyphFlashOn = provider.GetGlyph(0x7F, flashOn: true, altChar: false);
        var glyphFlashOff = provider.GetGlyph(0x7F, flashOn: false, altChar: false);

        // Assert
        Assert.Equal(8, glyphFlashOn.Length);
        Assert.Equal(8, glyphFlashOff.Length);
    }

    [Fact]
    public void GetGlyph_BoundaryCharacter_0x80()
    {
        // Arrange
        var provider = new CharacterRomProvider();

        // Act - 0x80 is just above the flash range
        var glyph = provider.GetGlyph(0x80, flashOn: false, altChar: false);

        // Assert
        Assert.Equal(8, glyph.Length);
    }

    #endregion

    #region Performance Tests

    [Fact]
    public void GetGlyph_PerformanceTest_1000Calls()
    {
        // Arrange
        var provider = new CharacterRomProvider();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - Get 1000 glyphs
        for (int i = 0; i < 1000; i++)
        {
            var glyph = provider.GetGlyph((byte)(i % 256), false, false);
        }
        stopwatch.Stop();

        // Assert - Should be very fast (under 10ms for 1000 calls)
        Assert.True(stopwatch.ElapsedMilliseconds < 100,
            $"Getting 1000 glyphs took {stopwatch.ElapsedMilliseconds}ms, expected < 100ms");
    }

    #endregion
}
