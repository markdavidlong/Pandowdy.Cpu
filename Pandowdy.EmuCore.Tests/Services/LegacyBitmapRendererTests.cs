// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Reflection;
using Pandowdy.EmuCore.Services;

namespace Pandowdy.EmuCore.Tests.Services;

/// <summary>
/// Unit tests for LegacyBitmapRenderer, which handles rendering of Apple II
/// video modes (text, lo-res, hi-res) to the bitmap display.
/// </summary>
public class LegacyBitmapRendererTests
{
    #region Helper Methods

    /// <summary>
    /// Invokes the private static GetAddressForXY method via reflection.
    /// This method calculates Apple II memory addresses for display cells.
    /// </summary>
    private static int InvokeGetAddressForXY(int x, int y, bool text, bool hires, bool mixed, bool page2, int cellRowOffset = 0)
    {
        MethodInfo? mi = typeof(LegacyBitmapRenderer).GetMethod(
            "GetAddressForXY", 
            BindingFlags.NonPublic | BindingFlags.Static);
        
        Assert.NotNull(mi);
        
        object? result = mi!.Invoke(null, [x, y, text, hires, mixed, page2, cellRowOffset]);
        Assert.NotNull(result);
        
        return (int)result!;
    }

    #endregion

    #region GetAddressForXY Tests

    [Fact]
    public void GetAddressForXY_TextPage1_BasicPositions()
    {
        // Row 0, Column 0
        int addr00 = InvokeGetAddressForXY(0, 0, text: true, hires: false, mixed: false, page2: false);
        Assert.Equal(0x0400, addr00);

        // Row 0, Column 1
        int addr10 = InvokeGetAddressForXY(1, 0, text: true, hires: false, mixed: false, page2: false);
        Assert.Equal(0x0401, addr10);

        // Row 1, Column 0 (128 bytes offset per row in first group of 8)
        int addr01 = InvokeGetAddressForXY(0, 1, text: true, hires: false, mixed: false, page2: false);
        Assert.Equal(0x0400 + 128, addr01);

        // Row 8, Column 0 (second group of 8 rows, +40 bytes)
        int addr08 = InvokeGetAddressForXY(0, 8, text: true, hires: false, mixed: false, page2: false);
        Assert.Equal(0x0400 + 40, addr08);

        // Row 16, Column 0 (third group of 8 rows, +80 bytes)
        int addr16 = InvokeGetAddressForXY(0, 16, text: true, hires: false, mixed: false, page2: false);
        Assert.Equal(0x0400 + 80, addr16);
    }

    [Fact]
    public void GetAddressForXY_TextPage2_BaseAddress()
    {
        // Text Page 2 starts at $0800 instead of $0400
        int addrPage2 = InvokeGetAddressForXY(0, 0, text: true, hires: false, mixed: false, page2: true);
        Assert.Equal(0x0800, addrPage2);
    }

    [Fact]
    public void GetAddressForXY_HiResPage1_BasicPositions()
    {
        // HiRes Page 1 starts at $2000
        int addrHires00 = InvokeGetAddressForXY(0, 0, text: false, hires: true, mixed: false, page2: false);
        Assert.Equal(0x2000, addrHires00);

        // With cellRowOffset=1 (second scanline within cell)
        int addrHires08 = InvokeGetAddressForXY(0, 0, text: false, hires: true, mixed: false, page2: false, cellRowOffset: 1);
        Assert.Equal(0x2400, addrHires08);

        // Row 9, Column 0, cellRowOffset=0
        int addrHires72 = InvokeGetAddressForXY(0, 9, text: false, hires: true, mixed: false, page2: false, cellRowOffset: 0);
        Assert.Equal(0x20A8, addrHires72);

        // Last position: Row 23, Column 39, cellRowOffset=7
        int addrHires191_39 = InvokeGetAddressForXY(39, 23, text: false, hires: true, mixed: false, page2: false, cellRowOffset: 7);
        Assert.Equal(0x3ff7, addrHires191_39);
    }

    [Fact]
    public void GetAddressForXY_HiResPage2_BasicPositions()
    {
        // HiRes Page 2 starts at $4000
        int addr2Hires00 = InvokeGetAddressForXY(0, 0, text: false, hires: true, mixed: false, page2: true);
        Assert.Equal(0x4000, addr2Hires00);

        // With cellRowOffset=1
        int addr2Hires08 = InvokeGetAddressForXY(0, 0, text: false, hires: true, mixed: false, page2: true, cellRowOffset: 1);
        Assert.Equal(0x4400, addr2Hires08);

        // Row 9, Column 0
        int addr2Hires72 = InvokeGetAddressForXY(0, 9, text: false, hires: true, mixed: false, page2: true, cellRowOffset: 0);
        Assert.Equal(0x40A8, addr2Hires72);

        // Last position
        int addr2Hires191_39 = InvokeGetAddressForXY(39, 23, text: false, hires: true, mixed: false, page2: true, cellRowOffset: 7);
        Assert.Equal(0x5ff7, addr2Hires191_39);

        // Column 5, cellRowOffset=1
        int addr2Hires08_5 = InvokeGetAddressForXY(5, 0, text: false, hires: true, mixed: false, page2: true, cellRowOffset: 1);
        Assert.Equal(0x4405, addr2Hires08_5);
    }

    [Fact]
    public void GetAddressForXY_MixedMode_BottomTextArea()
    {
        // Mixed mode: rows >= 20 should map to text page
        // Even with page2=true for graphics, text portion uses appropriate page
        int addrMixedText = InvokeGetAddressForXY(0, 21, text: false, hires: true, mixed: true, page2: true);
        
        // Text Page 2 base ($0800) + row offset
        // Row 21: (21 % 8) * 128 + (21 / 8) * 40 = 5*128 + 2*40 = 640 + 80 = 720 = 0x2D0
        Assert.Equal(0x0800 + (21 % 8) * 128 + (21 / 8) * 40, addrMixedText);
    }

    [Fact]
    public void GetAddressForXY_InvalidCoordinates_ReturnsNegativeOne()
    {
        // X out of range (>= 40)
        int addrInvalidX = InvokeGetAddressForXY(41, 0, text: true, hires: false, mixed: false, page2: false);
        Assert.Equal(-1, addrInvalidX);

        // Y out of range (>= 24)
        int addrInvalidY = InvokeGetAddressForXY(0, 24, text: true, hires: false, mixed: false, page2: false);
        Assert.Equal(-1, addrInvalidY);

        // Negative X
        int addrNegativeX = InvokeGetAddressForXY(-1, 0, text: true, hires: false, mixed: false, page2: false);
        Assert.Equal(-1, addrNegativeX);

        // Negative Y
        int addrNegativeY = InvokeGetAddressForXY(0, -1, text: true, hires: false, mixed: false, page2: false);
        Assert.Equal(-1, addrNegativeY);
    }

    [Theory]
    [InlineData(0, 0, 0x0400)]    // First position
    [InlineData(39, 0, 0x0427)]   // Last column, first row
    [InlineData(0, 23, 0x07D0)]   // First column, last row (row 23: (23%8)*128 + (23/8)*40 = 7*128 + 2*40 = 976 + 1024 = 2000)
    [InlineData(39, 23, 0x07F7)]  // Last position (row 23, col 39: 2000 + 39 = 2039)
    public void GetAddressForXY_TextPage1_BoundaryTests(int x, int y, int expectedAddress)
    {
        int address = InvokeGetAddressForXY(x, y, text: true, hires: false, mixed: false, page2: false);
        Assert.Equal(expectedAddress, address);
    }

    [Fact]
    public void GetAddressForXY_LoResMode_UsesTextPageLayout()
    {
        // Lo-res graphics use the same memory layout as text mode
        // (text=false, hires=false means lo-res)
        int addrLoRes = InvokeGetAddressForXY(5, 10, text: false, hires: false, mixed: false, page2: false);
        int addrText = InvokeGetAddressForXY(5, 10, text: true, hires: false, mixed: false, page2: false);
        
        Assert.Equal(addrText, addrLoRes);
    }

    #endregion

    #region Future Test Placeholders

    // TODO: Add tests for rendering methods when they become testable
    // - RenderScreen
    // - RenderTextOrGRCell
    // - RenderHiresCell
    // - RenderTextCell
    // - RenderGrCell
    // - InsertHgrByteAt
    // - MakeGrColor
    
    // These will require:
    // - Mock ICharacterRomProvider
    // - Mock RenderContext with test memory
    // - Verification of bitmap output

    #endregion
}
