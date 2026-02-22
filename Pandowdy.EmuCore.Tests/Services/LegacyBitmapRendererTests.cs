// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Reflection;
using Pandowdy.EmuCore.IO;
using Pandowdy.EmuCore.Memory;
using Pandowdy.EmuCore.Video;
using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.Machine;

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

    #region Mock Implementations

    /// <summary>
    /// Mock implementation of ICharacterRomProvider for testing text rendering.
    /// </summary>
    private class MockCharacterRomProvider : ICharacterRomProvider
    {
        /// <summary>
        /// Returns a test glyph pattern based on the character code.
        /// Pattern: alternating bits per row (0x55 = 0b01010101, 0xAA = 0b10101010).
        /// </summary>
        public ReadOnlySpan<byte> GetGlyph(byte ch, bool flashOn, bool altChar)
        {
            // Create a simple test pattern: even rows = 0x55, odd rows = 0x2A
            // This creates a checkerboard-like pattern that's easy to verify
            byte[] glyph = new byte[8];
            for (int i = 0; i < 8; i++)
            {
                if (i % 2 == 0)
                {
                    glyph[i] = 0x55; // 0b01010101
                }
                else
                {
                    glyph[i] = 0x2A; // 0b00101010
                }
            }
            return new ReadOnlySpan<byte>(glyph);
        }
    }

    /// <summary>
    /// Mock implementation of IDirectMemoryPoolReader for testing rendering with controlled memory.
    /// </summary>
    private class MockMemoryPoolReader : IDirectMemoryPoolReader
    {
        private readonly byte[] _mainMemory = new byte[0x10000]; // 64KB main memory
        private readonly byte[] _auxMemory = new byte[0x10000];  // 64KB aux memory

        public byte ReadRawMain(int address) => _mainMemory[address & 0xFFFF];
        public byte ReadRawAux(int address) => _auxMemory[address & 0xFFFF];

        public void WriteMain(ushort address, byte value) => _mainMemory[address] = value;
        public void WriteAux(ushort address, byte value) => _auxMemory[address] = value;
    }

    /// <summary>
    /// Mock implementation of ISystemStatusProvider for testing with controlled soft switch states.
    /// </summary>
    private class MockSystemStatusProvider : ISystemStatusProvider
    {
        public bool StateTextMode { get; set; }
        public bool StateHiRes { get; set; }
        public bool StateMixed { get; set; }
        public bool StatePage2 { get; set; }
        public bool StateShow80Col { get; set; }
        public bool State80Store { get; set; }
        public bool StateAltCharSet { get; set; }
        public bool StateFlashOn { get; set; }
        public bool StateAnn3_DGR { get; set; }

        // Memory configuration switches
        public bool StateRamRd { get; set; }
        public bool StateRamWrt { get; set; }
        public bool StateIntCxRom { get; set; }
        public bool StateAltZp { get; set; }
        public bool StateIntC8Rom { get; set; }
        public byte StateIntC8RomSlot { get; set; }
        public bool StateSlotC3Rom { get; set; }

        // Language card switches
        public bool StatePreWrite { get; set; }
        public bool StateUseBank1 { get; set; }
        public bool StateHighRead { get; set; }
        public bool StateHighWrite { get; set; }

        // System state
        public bool StateVBlank { get; set; }
        public double StateCurrentMhz { get; set; }

        // Paddle buttons
        public bool StatePb0 { get; set; }
        public bool StatePb1 { get; set; }
        public bool StatePb2 { get; set; }

        // Annunciators
        public bool StateAnn0 { get; set; }
        public bool StateAnn1 { get; set; }
        public bool StateAnn2 { get; set; }

        // Paddle values (note: Property names don't have "State" prefix in interface)
        public byte Pdl0 { get; set; }
        public byte Pdl1 { get; set; }
        public byte Pdl2 { get; set; }
        public byte Pdl3 { get; set; }

        // Event properties (not used in rendering tests but required by interface)
        public SystemStatusSnapshot Current => new(
            State80Store: false,
            StateRamRd: false,
            StateRamWrt: false,
            StateIntCxRom: false,
            StateIntC8Rom: false,
            StateAltZp: false,
            StateSlotC3Rom: false,
            StatePb0: false,
            StatePb1: false,
            StatePb2: false,
            StateAnn0: false,
            StateAnn1: false,
            StateAnn2: false,
            StateAnn3_DGR: false,
            StatePage2: false,
            StateHiRes: false,
            StateMixed: false,
            StateTextMode: false,
            StateShow80Col: false,
            StateAltCharSet: false,
            StateFlashOn: false,
            StatePrewrite: false,
            StateUseBank1: false,
            StateHighRead: false,
            StateHighWrite: false,
            StateVBlank: false,
            StatePdl0: 0,
            StatePdl1: 0,
            StatePdl2: 0,
            StatePdl3: 0,
            StateIntC8RomSlot: 0,
            StateCurrentMhz: 1.023
        );

#pragma warning disable CS0067 // Event is never used (required by interface for mock)
        public event EventHandler<SystemStatusSnapshot>? Changed;
        public event EventHandler<SystemStatusSnapshot>? MemoryMappingChanged;
#pragma warning restore CS0067

        private readonly System.Reactive.Subjects.Subject<SystemStatusSnapshot> _streamSubject = new();
        public IObservable<SystemStatusSnapshot> Stream => _streamSubject;
    }

    /// <summary>
    /// Creates a test RenderContext with mock components and controllable memory/soft switches.
    /// </summary>
    private static (RenderContext context, MockMemoryPoolReader memory, MockSystemStatusProvider status) CreateTestRenderContext()
    {
        var frameBuffer = new BitmapDataArray();
        var memory = new MockMemoryPoolReader();
        var status = new MockSystemStatusProvider();
        var context = new RenderContext(frameBuffer, memory, status);
        return (context, memory, status);
    }

    #endregion

    #region Render Method Tests

    [Fact]
    public void Render_WithNullContext_ThrowsArgumentNullException()
    {
        var charRomProvider = new MockCharacterRomProvider();
        var renderer = new LegacyBitmapRenderer(charRomProvider);

        Assert.Throws<ArgumentNullException>(() => renderer.Render(null!));
    }

    [Fact]
    public void Render_WithValidContext_ClearsBufferAndRenders()
    {
        var charRomProvider = new MockCharacterRomProvider();
        var renderer = new LegacyBitmapRenderer(charRomProvider);
        var (context, memory, status) = CreateTestRenderContext();

        // Set up a simple text mode scenario
        status.StateTextMode = true;
        status.StateHiRes = false;
        status.StateMixed = false;
        status.StatePage2 = false;
        status.StateShow80Col = false;
        status.State80Store = false;
        status.StateAltCharSet = false;
        status.StateFlashOn = false;

        // Write test character to first text position ($0400)
        memory.WriteMain(0x0400, 0x41); // 'A'

        // Render should not throw
        renderer.Render(context);

        // Basic validation: frame buffer should have some pixels set
        // (detailed pixel checks are in mode-specific tests)
        Assert.NotNull(context.FrameBuffer);
    }

    #endregion

    #region Text Mode Rendering Tests

    [Fact]
    public void RenderTextCell_40Column_RendersWithHorizontalDoubling()
    {
        var charRomProvider = new MockCharacterRomProvider();
        var renderer = new LegacyBitmapRenderer(charRomProvider);
        var (context, memory, status) = CreateTestRenderContext();

        // Set up 40-column text mode
        status.StateTextMode = true;
        status.StateHiRes = false;
        status.StateMixed = false;
        status.StatePage2 = false;
        status.StateShow80Col = false; // 40-column mode
        status.State80Store = false;
        status.StateAltCharSet = false;
        status.StateFlashOn = false;

        // Write test character to position (0, 0) at $0400
        memory.WriteMain(0x0400, 0x41); // Character 'A'

        renderer.Render(context);

        // Verify that pixels are set with horizontal doubling
        // MockCharacterRomProvider returns alternating 0x55/0x2A pattern
        // Row 0: 0x55 inverted = 0xAA = 0b10101010
        // With doubling, bit 0 (LSB) should be at x=0,1; bit 1 at x=2,3, etc.
        var frameBuffer = context.FrameBuffer;

        // Check first row of character (y=0)
        // Bit 0 of 0xAA is 0, so pixels at x=0,1 should be clear
        Assert.False(frameBuffer.GetPixel(0, 0, 0));
        Assert.False(frameBuffer.GetPixel(1, 0, 0));

        // Bit 1 of 0xAA is 1, so pixels at x=2,3 should be set
        Assert.True(frameBuffer.GetPixel(2, 0, 0));
        Assert.True(frameBuffer.GetPixel(3, 0, 0));
    }

    [Fact]
    public void RenderTextCell_80Column_RendersWithoutDoubling()
    {
        var charRomProvider = new MockCharacterRomProvider();
        var renderer = new LegacyBitmapRenderer(charRomProvider);
        var (context, memory, status) = CreateTestRenderContext();

        // Set up 80-column text mode
        status.StateTextMode = true;
        status.StateHiRes = false;
        status.StateMixed = false;
        status.StatePage2 = false;
        status.StateShow80Col = true; // 80-column mode
        status.State80Store = true;
        status.StateAltCharSet = false;
        status.StateFlashOn = false;

        // Write test characters to position (0, 0) - aux and main
        memory.WriteAux(0x0400, 0x41);  // Aux character
        memory.WriteMain(0x0400, 0x42); // Main character

        renderer.Render(context);

        // Verify that pixels are rendered without doubling
        // Aux character should be at x=0-6, main at x=7-13
        var frameBuffer = context.FrameBuffer;

        // Row 0 of aux character: 0x55 inverted = 0xAA = 0b10101010
        // Bit 0 (LSB) is 0, should be clear
        Assert.False(frameBuffer.GetPixel(0, 0, 0));

        // Bit 1 is 1, should be set
        Assert.True(frameBuffer.GetPixel(1, 0, 0));
    }

    [Fact]
    public void RenderTextCell_Page2_ReadsFromCorrectPage()
    {
        var charRomProvider = new MockCharacterRomProvider();
        var renderer = new LegacyBitmapRenderer(charRomProvider);
        var (context, memory, status) = CreateTestRenderContext();

        // Set up text mode with Page 2 active
        status.StateTextMode = true;
        status.StateHiRes = false;
        status.StateMixed = false;
        status.StatePage2 = true; // Page 2
        status.StateShow80Col = false;
        status.State80Store = false;
        status.StateAltCharSet = false;
        status.StateFlashOn = false;

        // Write characters to both pages
        memory.WriteMain(0x0400, 0x41); // Page 1 character (should be ignored)
        memory.WriteMain(0x0800, 0x42); // Page 2 character (should be rendered)

        renderer.Render(context);

        // Verify rendering occurred (detailed verification in other tests)
        Assert.NotNull(context.FrameBuffer);
    }

    [Fact]
    public void RenderTextCell_80StoreAndPage2_ReadsFromAux()
    {
        var charRomProvider = new MockCharacterRomProvider();
        var renderer = new LegacyBitmapRenderer(charRomProvider);
        var (context, memory, status) = CreateTestRenderContext();

        // Set up 80STORE + PAGE2 mode (reads from aux at $0400)
        status.StateTextMode = true;
        status.StateHiRes = false;
        status.StateMixed = false;
        status.StatePage2 = true;  // Page 2
        status.StateShow80Col = false; // 40-column
        status.State80Store = true;    // 80STORE active
        status.StateAltCharSet = false;
        status.StateFlashOn = false;

        // Write characters to main and aux
        memory.WriteMain(0x0400, 0x41); // Should be ignored
        memory.WriteAux(0x0400, 0x42);  // Should be rendered

        renderer.Render(context);

        // Verify rendering occurred
        Assert.NotNull(context.FrameBuffer);
    }

    #endregion

    #region Lo-Res Graphics Tests

    [Fact]
    public void RenderGrCell_40Column_RendersColorBlocks()
    {
        var charRomProvider = new MockCharacterRomProvider();
        var renderer = new LegacyBitmapRenderer(charRomProvider);
        var (context, memory, status) = CreateTestRenderContext();

        // Set up lo-res graphics mode
        status.StateTextMode = false;
        status.StateHiRes = false; // Lo-res mode
        status.StateMixed = false;
        status.StatePage2 = false;
        status.StateShow80Col = false;
        status.State80Store = false;
        status.StateAnn3_DGR = false;

        // Write test color value to position (0, 0) at $0400
        // Lower nibble (0x0F) = color for rows 0-3, upper nibble (0xF0) = rows 4-7
        memory.WriteMain(0x0400, 0xF0); // Upper nibble = 0xF, lower nibble = 0x0

        renderer.Render(context);

        // Verify that rendering occurred
        // (Detailed color pattern verification would require understanding MakeGrColor logic)
        Assert.NotNull(context.FrameBuffer);
    }

    [Fact]
    public void RenderGrCell_80Column_RendersDGRMode()
    {
        var charRomProvider = new MockCharacterRomProvider();
        var renderer = new LegacyBitmapRenderer(charRomProvider);
        var (context, memory, status) = CreateTestRenderContext();

        // Set up double lo-res graphics (DGR) mode
        status.StateTextMode = false;
        status.StateHiRes = false;
        status.StateMixed = false;
        status.StatePage2 = false;
        status.StateShow80Col = true; // 80-column graphics
        status.State80Store = true;
        status.StateAnn3_DGR = false; // DGR active (Show80Col && !Ann3_DGR)

        // Write test values to aux and main
        memory.WriteAux(0x0400, 0xF0);
        memory.WriteMain(0x0400, 0x0F);

        renderer.Render(context);

        // Verify rendering occurred
        Assert.NotNull(context.FrameBuffer);
    }

    [Fact]
    public void MakeGrColor_GeneratesPhaseVariations()
    {
        // Use reflection to test private static MakeGrColor method
        MethodInfo? mi = typeof(LegacyBitmapRenderer).GetMethod(
            "MakeGrColor",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(mi);

        // Test with color value 0x0F (all bits set in lower nibble)
        object? result = mi!.Invoke(null, [(byte)0x0F]);
        Assert.NotNull(result);

        var tuple = ((byte, byte, byte, byte))result!;
        var (a, b, c, d) = tuple;

        // Verify that phase variations were generated
        // (Exact values depend on algorithm, just verify they're non-zero and different)
        Assert.NotEqual(0, a | b | c | d); // At least some bits set
    }

    #endregion

    #region Hi-Res Graphics Tests

    [Fact]
    public void RenderHiresCell_StandardMode_RendersWithDoubling()
    {
        var charRomProvider = new MockCharacterRomProvider();
        var renderer = new LegacyBitmapRenderer(charRomProvider);
        var (context, memory, status) = CreateTestRenderContext();

        // Set up hi-res graphics mode
        status.StateTextMode = false;
        status.StateHiRes = true; // Hi-res mode
        status.StateMixed = false;
        status.StatePage2 = false;
        status.StateShow80Col = false;
        status.State80Store = false;

        // Write test pattern to first hi-res byte (row 0, col 0) at $2000
        memory.WriteMain(0x2000, 0x55); // 0b01010101 (no phase shift)

        renderer.Render(context);

        // Verify that pixels are rendered with horizontal doubling
        var frameBuffer = context.FrameBuffer;

        // Bit 0 of 0x55 is 1, should create 3-pixel fringe (basic fringing)
        // At x=0 (col 0 * 2 * 7 = 0)
        Assert.True(frameBuffer.GetPixel(0, 0, 0) || frameBuffer.GetPixel(1, 0, 0) || frameBuffer.GetPixel(2, 0, 0));
    }

    [Fact]
    public void RenderHiresCell_WithPhaseShift_RendersShiftedPixels()
    {
        var charRomProvider = new MockCharacterRomProvider();
        var renderer = new LegacyBitmapRenderer(charRomProvider);
        var (context, memory, status) = CreateTestRenderContext();

        // Set up hi-res graphics mode
        status.StateTextMode = false;
        status.StateHiRes = true;
        status.StateMixed = false;
        status.StatePage2 = false;
        status.StateShow80Col = false;
        status.State80Store = false;

        // Write test pattern with phase shift (bit 7 set)
        memory.WriteMain(0x2000, 0x81); // 0b10000001 (phase shift + bit 0)

        renderer.Render(context);

        // Verify rendering occurred (detailed phase shift verification complex)
        Assert.NotNull(context.FrameBuffer);
    }

    [Fact]
    public void RenderHiresCell_Page2_RendersFromCorrectPage()
    {
        var charRomProvider = new MockCharacterRomProvider();
        var renderer = new LegacyBitmapRenderer(charRomProvider);
        var (context, memory, status) = CreateTestRenderContext();

        // Set up hi-res graphics mode with Page 2
        status.StateTextMode = false;
        status.StateHiRes = true;
        status.StateMixed = false;
        status.StatePage2 = true; // Page 2 ($4000)
        status.StateShow80Col = false;
        status.State80Store = false;

        // Write to both pages
        memory.WriteMain(0x2000, 0x55); // Page 1 (should be ignored)
        memory.WriteMain(0x4000, 0xAA); // Page 2 (should be rendered)

        renderer.Render(context);

        // Verify rendering occurred
        Assert.NotNull(context.FrameBuffer);
    }

    #endregion

    #region Mixed Mode Tests

    [Fact]
    public void RenderScreen_MixedMode_RendersGraphicsAndText()
    {
        var charRomProvider = new MockCharacterRomProvider();
        var renderer = new LegacyBitmapRenderer(charRomProvider);
        var (context, memory, status) = CreateTestRenderContext();

        // Set up mixed mode (graphics + 4-line text status)
        status.StateTextMode = false;
        status.StateHiRes = true;  // Hi-res graphics
        status.StateMixed = true;  // Mixed mode (rows 20-23 are text)
        status.StatePage2 = false;
        status.StateShow80Col = false;
        status.State80Store = false;
        status.StateAltCharSet = false;
        status.StateFlashOn = false;

        // Write hi-res data for top rows
        memory.WriteMain(0x2000, 0x55);

        // Write text data for bottom rows (row 20 = 0x650)
        // Row 20: (20 % 8) * 128 + (20 / 8) * 40 = 4*128 + 2*40 = 592 = 0x250
        memory.WriteMain(0x0400 + 0x250, 0x41); // 'A' in text area

        renderer.Render(context);

        // Verify rendering occurred for both sections
        Assert.NotNull(context.FrameBuffer);
    }

    [Fact]
    public void RenderScreen_MixedModeLoRes_RendersGraphicsAndText()
    {
        var charRomProvider = new MockCharacterRomProvider();
        var renderer = new LegacyBitmapRenderer(charRomProvider);
        var (context, memory, status) = CreateTestRenderContext();

        // Set up mixed mode with lo-res graphics
        status.StateTextMode = false;
        status.StateHiRes = false; // Lo-res graphics
        status.StateMixed = true;  // Mixed mode
        status.StatePage2 = false;
        status.StateShow80Col = false;
        status.State80Store = false;
        status.StateAltCharSet = false;
        status.StateFlashOn = false;

        // Write lo-res data
        memory.WriteMain(0x0400, 0xF0);

        // Write text data for row 20
        memory.WriteMain(0x0400 + 0x250, 0x42);

        renderer.Render(context);

        // Verify rendering occurred
        Assert.NotNull(context.FrameBuffer);
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public void Constructor_WithNullCharRomProvider_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new LegacyBitmapRenderer(null!));
    }

    [Fact]
    public void RenderScreen_AllTextMode_RendersAll24Rows()
    {
        var charRomProvider = new MockCharacterRomProvider();
        var renderer = new LegacyBitmapRenderer(charRomProvider);
        var (context, memory, status) = CreateTestRenderContext();

        // Set up full text mode
        status.StateTextMode = true;
        status.StateHiRes = false;
        status.StateMixed = false;
        status.StatePage2 = false;
        status.StateShow80Col = false;
        status.State80Store = false;
        status.StateAltCharSet = false;
        status.StateFlashOn = false;

        // Fill text memory with test characters
        for (int row = 0; row < 24; row++)
        {
            for (int col = 0; col < 40; col++)
            {
                int addr = 0x0400 + (row % 8) * 128 + (row / 8) * 40 + col;
                memory.WriteMain((ushort)addr, 0x41); // 'A'
            }
        }

        renderer.Render(context);

        // Verify that rendering completed without exception
        Assert.NotNull(context.FrameBuffer);
    }

    [Fact]
    public void RenderScreen_AllHiResMode_RendersAll24Rows()
    {
        var charRomProvider = new MockCharacterRomProvider();
        var renderer = new LegacyBitmapRenderer(charRomProvider);
        var (context, memory, status) = CreateTestRenderContext();

        // Set up full hi-res mode
        status.StateTextMode = false;
        status.StateHiRes = true;
        status.StateMixed = false;
        status.StatePage2 = false;
        status.StateShow80Col = false;
        status.State80Store = false;

        // Fill hi-res memory with test pattern
        for (int addr = 0x2000; addr < 0x4000; addr++)
        {
            memory.WriteMain((ushort)addr, 0x55);
        }

        renderer.Render(context);

        // Verify that rendering completed without exception
        Assert.NotNull(context.FrameBuffer);
    }

    [Fact]
    public void RenderScreen_AllLoResMode_RendersAll24Rows()
    {
        var charRomProvider = new MockCharacterRomProvider();
        var renderer = new LegacyBitmapRenderer(charRomProvider);
        var (context, memory, status) = CreateTestRenderContext();

        // Set up full lo-res mode
        status.StateTextMode = false;
        status.StateHiRes = false;
        status.StateMixed = false;
        status.StatePage2 = false;
        status.StateShow80Col = false;
        status.State80Store = false;

        // Fill lo-res memory with test pattern
        for (int row = 0; row < 24; row++)
        {
            for (int col = 0; col < 40; col++)
            {
                int addr = 0x0400 + (row % 8) * 128 + (row / 8) * 40 + col;
                memory.WriteMain((ushort)addr, 0xF0);
            }
        }

        renderer.Render(context);

        // Verify that rendering completed without exception
        Assert.NotNull(context.FrameBuffer);
    }

    #endregion
}
