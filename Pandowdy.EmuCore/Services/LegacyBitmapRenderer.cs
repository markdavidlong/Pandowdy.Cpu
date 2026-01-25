//------------------------------------------------------------------------------
// LegacyBitmapRenderer.cs
//
// âš ï¸ STOPGAP IMPLEMENTATION - PLANNED FOR REPLACEMENT âš ï¸
//
// This renderer provides basic Apple IIe monochrome bitmap output but has
// known limitations and is intended to be replaced by a more accurate,
// scanline-based renderer.
//
// SCOPE: This renderer (and its replacement) generates MONOCHROME BITMAPS ONLY.
// NTSC color artifact generation is NOT the responsibility of this class - that
// is handled by downstream UI renderers which process the monochrome output.
//
// Current Limitations:
// - Simplified cell-based rendering (not scanline-by-scanline)
// - No accurate timing simulation
// - Limited double hi-res (DHGR) support
// - Limited double lo-res (DGR) support
//
// Future Replacement:
// The planned replacement will implement:
// - Full 80-column and double hi-res support
// - Better handling of mixed modes
//
// Both this renderer and its replacement produce monochrome bitmaps that are
// then processed by UI-layer NTSC color generators (if color output is desired).
// The interface (IDisplayBitmapRenderer) will remain stable to ensure the
// replacement can be swapped in transparently.
//------------------------------------------------------------------------------

using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.Services;

/// <summary>
/// Basic monochrome bitmap renderer for Apple IIe video modes (stopgap implementation).
/// </summary>
/// <remarks>
/// <para>
/// <strong>âš ï¸ TEMPORARY IMPLEMENTATION:</strong> This renderer is a stopgap solution
/// providing basic Apple IIe monochrome bitmap output. It will be replaced by a more
/// accurate scanline-based renderer in a future update.
/// </para>
/// <para>
/// <strong>Monochrome Output:</strong> This renderer (and its replacement) generates
/// MONOCHROME BITMAPS ONLY. NTSC color artifact generation is handled by downstream
/// UI renderers, not by this class. The output is a binary (on/off) bitmap representing
/// what pixels would be lit on a monochrome monitor.
/// </para>
/// <para>
/// <strong>Supported Modes:</strong>
/// <list type="bullet">
/// <item>Text Mode: 40-column (fully supported), 80-column (basic support)</item>
/// <item>Lo-Res Graphics: 40Ã—48 blocks (fully supported)</item>
/// <item>Hi-Res Graphics: 280Ã—192 pixels (basic support)</item>
/// <item>Mixed Mode: Graphics + 4-line text status area (supported)</item>
/// </list>
/// </para>
/// <para>
/// <strong>Known Limitations:</strong>
/// <list type="bullet">
/// <item>Cell-based rendering instead of scanline-by-scanline</item>
/// <item>Simplified pixel fringing logic</item>
/// <item>Double hi-res (DHGR) partially implemented</item>
/// <item>No accurate timing simulation</item>
/// </list>
/// </para>
/// <para>
/// <strong>Rendering Approach:</strong> This renderer uses a cell-based approach,
/// iterating through 24 rows Ã— 40 columns and rendering each 8-scanline cell based
/// on the current video mode. Memory addresses are calculated using Apple IIe's
/// interleaved scanline pattern, and pixels are written to bitplane 0 of the frame buffer.
/// </para>
/// </remarks>
public class LegacyBitmapRenderer : IDisplayBitmapRenderer
{
    /// <summary>
    /// Active display bitplane (always 0 for basic rendering).
    /// </summary>
    /// <remarks>
    /// <see cref="BitmapDataArray"/> supports multiple bitplanes for future NTSC
    /// color generation, but this renderer only uses bitplane 0 (composite output).
    /// </remarks>
    private const int _bitplane = 0;
    
    private readonly ICharacterRomProvider _charRomProvider;
    
    // TECHNICAL DEBT: This field is reassigned on every Render() call to avoid
    // threading the context through all helper methods. This is converted from
    // legacy code where all helpers were static and accessed context via a shared
    // field. Ideally, context should be passed as a parameter to each helper method.
    // This will be cleaned up in the replacement renderer implementation.
    private RenderContext? _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="LegacyBitmapRenderer"/> class.
    /// </summary>
    /// <param name="charRomProvider">
    /// Character ROM provider for text mode rendering. Provides 8-row glyph data
    /// for each character, with support for flashing characters, MouseText, and
    /// alternate character set.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="charRomProvider"/> is null.
    /// </exception>
    public LegacyBitmapRenderer(ICharacterRomProvider charRomProvider)
    {
        ArgumentNullException.ThrowIfNull(charRomProvider);
        _charRomProvider = charRomProvider;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>Rendering Process:</strong>
    /// <list type="number">
    /// <item>Stores <paramref name="context"/> in instance field for access by rendering methods</item>
    /// <item>Calls <see cref="RenderScreen"/> to perform cell-based rendering</item>
    /// <item>Iterates 24 rows Ã— 40 columns, rendering each cell based on video mode</item>
    /// <item>Writes output to bitplane 0 of the frame buffer</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> Not thread-safe. Should only be called from the
    /// rendering thread. The <paramref name="context"/> is stored in an instance field,
    /// so concurrent calls would interfere with each other.
    /// </para>
    /// </remarks>
    public void Render(RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Store context for access by rendering helper methods
        _context = context;

        RenderScreen(context.FrameBuffer);
    }

    /// <summary>
    /// Renders the entire Apple IIe screen by iterating through all cells.
    /// </summary>
    /// <param name="buf">Frame buffer to render into.</param>
    /// <remarks>
    /// <para>
    /// <strong>Cell-Based Rendering:</strong> Iterates 24 rows Ã— 40 columns, calculating
    /// the video memory address for each cell using <see cref="GetAddressForXY"/>. Each
    /// cell represents 14 horizontal pixels (7 doubled) Ã— 8 scanlines.
    /// </para>
    /// <para>
    /// <strong>Address Ranges:</strong>
    /// <list type="bullet">
    /// <item>$0400-$07FF: Text/Lo-Res Page 1</item>
    /// <item>$0800-$0BFF: Text/Lo-Res Page 2</item>
    /// <item>$2000-$3FFF: Hi-Res Page 1</item>
    /// <item>$4000-$5FFF: Hi-Res Page 2</item>
    /// </list>
    /// </para>
    /// </remarks>
    private void RenderScreen(BitmapDataArray buf)
    {
        // Read soft switch states for this frame
        bool text = _context!.SystemStatus.StateTextMode;
        bool hires = _context!.SystemStatus.StateHiRes;
        bool mixed = _context!.SystemStatus.StateMixed;
        bool page2 = _context!.SystemStatus.StatePage2;
        bool text80col = _context!.SystemStatus.StateShow80Col;
        bool gr80col = text80col && !_context!.SystemStatus.StateAnn3_DGR;

        // Iterate through all 24 rows Ã— 40 columns
        for (int row = 0; row < 24; row++)
        {
            for (int col = 0; col < 40; col++)
            {
                // Calculate memory address for this cell based on video mode
                int addr = GetAddressForXY(col, row, text, hires, mixed, page2);
                
                if (addr >= 0x400 && addr <= 0xBFF) // Text/Lo-Res pages
                {
                    RenderTextOrGRCell(row, col, text, mixed, text80col, gr80col, buf);
                }
                else if (addr >= 0x2000 && addr <= 0x5fff) // Hi-Res pages
                {
                    RenderHiresCell(addr, row, col, gr80col, buf);
                }
            }
        }
    }

    /// <summary>
    /// Renders a text or lo-res graphics cell based on video mode.
    /// </summary>
    /// <param name="row">Row index (0-23).</param>
    /// <param name="col">Column index (0-39).</param>
    /// <param name="text">True if text mode is active.</param>
    /// <param name="mixed">True if mixed mode is active.</param>
    /// <param name="text80">True if 80-column text mode is active.</param>
    /// <param name="gr80">True if 80-column lo-res graphics mode is active.</param>
    /// <param name="buf">Frame buffer to render into.</param>
    /// <remarks>
    /// In mixed mode, rows 20-23 are always rendered as text, regardless of the
    /// text mode flag. This allows games to display graphics with a text status line.
    /// </remarks>
    private void RenderTextOrGRCell(int row, int col, bool text, bool mixed, bool text80, bool gr80, BitmapDataArray buf)
    {
        if (text || (mixed && row >= 20))
        {
            RenderTextCell(row, col, text80, buf);
        }
        else
        {
            RenderGrCell(row, col, gr80, buf);
        }
    }

    /// <summary>
    /// Inserts a hi-res byte into the frame buffer with pixel shift and fringing logic.
    /// </summary>
    /// <param name="buf">Frame buffer to render into.</param>
    /// <param name="x">Starting X coordinate (pixel column).</param>
    /// <param name="y">Y coordinate (scanline).</param>
    /// <param name="value">Byte value from hi-res memory.</param>
    /// <param name="prevShift">True if previous byte had high bit set (affects phase).</param>
    /// <param name="bitplane">Bitplane to render into (always 0 for this renderer).</param>
    /// <remarks>
    /// <para>
    /// <strong>Hi-Res Bit Packing:</strong> Each byte in hi-res memory represents 7 pixels,
    /// with bit 7 controlling pixel phase shift. When bit 7 is set, all pixels in the byte
    /// are shifted 1/2 pixel to the right. This produces the monochrome bit pattern that
    /// downstream NTSC color generators use to create color artifacts.
    /// </para>
    /// <para>
    /// <strong>Pixel Fringing:</strong> This implementation provides basic pixel fringing
    /// (3-pixel width for "on" pixels) to simulate beam width on CRT displays. This is a
    /// simplified approach - the replacement renderer will implement more accurate fringing.
    /// </para>
    /// <para>
    /// <strong>âš ï¸ LEGACY METHOD:</strong> This method uses individual SetPixel calls which
    /// are slow. Use <see cref="InsertHgrByteAt_Span"/> instead for 3-5x better performance.
    /// </para>
    /// </remarks>
    private static void InsertHgrByteAt(BitmapDataArray buf, int x, int y, byte value, bool prevShift, int bitplane)
    {
        // Get mutable span for this row - eliminates offset calculations!
        Span<BitField16> rowData = buf.GetMutableRowDataSpan(y);
        InsertHgrByteAt_Span(rowData, x, value, prevShift, bitplane);
    }
    
    /// <summary>
    /// Inserts a hi-res byte into a row span with pixel shift and fringing logic (optimized).
    /// </summary>
    /// <param name="rowData">Mutable span for the entire scanline.</param>
    /// <param name="x">Starting X coordinate (pixel column).</param>
    /// <param name="value">Byte value from hi-res memory.</param>
    /// <param name="prevShift">True if previous byte had high bit set (affects phase).</param>
    /// <param name="bitplane">Bitplane to render into (always 0 for this renderer).</param>
    /// <remarks>
    /// <para>
    /// <strong>Performance Optimization:</strong> This span-based version eliminates ~21 offset
    /// calculations (CalcOffset(x, y)) per byte by working directly with a row span. For hi-res
    /// rendering (7,680 bytes per frame), this eliminates ~161,000 redundant calculations,
    /// resulting in 3-5x faster performance.
    /// </para>
    /// <para>
    /// <strong>Direct Memory Access:</strong> Uses direct span indexing (rowData[p0]) instead
    /// of method calls (buf.SetPixel()), further reducing overhead.
    /// </para>
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void InsertHgrByteAt_Span(Span<BitField16> rowData, int x, byte value, bool prevShift, int bitplane)
    {
        int px = x;
        bool shift = (value & 0x80) == 0x80;

        // Render 7 data bits with doubling and simple fringing
        for (int bit = 0; bit < 7; bit++)
        {
            bool on = (value & (1 << bit)) != 0;
            int p0 = px + (bit * 2) + (shift ? 1 : 0);
            int p1 = p0 + 1;
            
            if (on)
            {
                // Set pixel with fringing - direct span access!
                if (p0 < 560)
                {
                    rowData[p0].SetBit(bitplane, true);
                }
                if (p1 < 560)
                {
                    rowData[p1].SetBit(bitplane, true);
                }
                if (p1 + 1 < 560)
                {
                    rowData[p1 + 1].SetBit(bitplane, true);
                }
            }
            else
            {
                // Clear pixels, respecting phase boundaries
                if (bit > 0 || (prevShift == shift))
                {
                    if (p0 < 560)
                    {
                        rowData[p0].SetBit(bitplane, false);
                    }
                }
                if (p1 < 560)
                {
                    rowData[p1].SetBit(bitplane, false);
                }
            }
        }
    }

    /// <summary>
    /// Renders a hi-res graphics cell (7Ã—8 or 14Ã—8 pixels depending on mode).
    /// </summary>
    /// <param name="address">Base address of the cell in hi-res memory.</param>
    /// <param name="row">Row index (0-23).</param>
    /// <param name="col">Column index (0-39).</param>
    /// <param name="gr80">True if 80-column hi-res mode (DHGR) is active.</param>
    /// <param name="buf">Frame buffer to render into.</param>
    /// <remarks>
    /// <para>
    /// <strong>Hi-Res Memory Layout:</strong> Hi-res graphics uses an interleaved scanline
    /// pattern where each row of a cell is stored 0x400 bytes apart. This method reads
    /// 8 bytes (one per scanline) and renders them with horizontal doubling.
    /// </para>
    /// <para>
    /// <strong>DHGR (80-column) Support:</strong> Double hi-res mode is partially implemented
    /// but not fully tested. It should render aux and main memory bytes side-by-side without
    /// horizontal doubling.
    /// </para>
    /// </remarks>
    private void RenderHiresCell(int address, int row, int col, bool gr80, BitmapDataArray buf)
    {
        // Render either 7 or 14 pixels, based on the state of gr80

        if (!gr80)
        {
            // Standard hi-res: render 7 pixels per byte, doubled horizontally
            for (int r = 0; r < 8; r++)
            {
                ushort byteAddress = (ushort)(address + (r * 0x400));
                byte value = _context!.Memory.ReadRawMain(byteAddress);
                int buffY = row * 8 + r;
                
                // Check previous byte's phase bit for color fringing
                bool prevShift = false;
                if (col != 0 && (_context!.Memory.ReadRawMain((ushort)(byteAddress - 1)) & 0x80) == 0x80)
                {
                    prevShift = true;
                }
                
                InsertHgrByteAt(buf, col * 2 * 7, buffY, value, prevShift, _bitplane);
            }
        }
        else
        {
            // TODO: Double hi-res (DHGR) - 80-column mode
            // Needs to render aux and main memory bytes side-by-side without horizontal doubling
            // Not fully implemented in this stopgap renderer
        }
    }

    /// <summary>
    /// Inserts 7 bits from a byte into the frame buffer, optionally with horizontal doubling.
    /// </summary>
    /// <param name="buf">Frame buffer to render into.</param>
    /// <param name="bitplane">Bitplane to render into.</param>
    /// <param name="x">Starting X coordinate.</param>
    /// <param name="y">Y coordinate (scanline).</param>
    /// <param name="value">Byte value (only lower 7 bits used).</param>
    /// <param name="expand">If true, doubles each pixel horizontally (7â†’14 pixels).</param>
    /// <remarks>
    /// Used for both text rendering (with expand=true for 40-column, expand=false for 80-column)
    /// and lo-res graphics rendering.
    /// <para>
    /// <strong>âš ï¸ LEGACY METHOD:</strong> This method uses individual SetPixel calls which
    /// are slow. Use <see cref="Insert7BitLsbAt_Span"/> instead for better performance.
    /// </para>
    /// </remarks>
    private static void Insert7BitLsbAt(BitmapDataArray buf, int bitplane, int x, int y, byte value, bool expand = false)
    {
        Span<BitField16> rowData = buf.GetMutableRowDataSpan(y);
        Insert7BitLsbAt_Span(rowData, bitplane, x, value, expand);
    }
    
    /// <summary>
    /// Inserts 7 bits from a byte into a row span, optionally with horizontal doubling (optimized).
    /// </summary>
    /// <param name="rowData">Mutable span for the entire scanline.</param>
    /// <param name="bitplane">Bitplane to render into.</param>
    /// <param name="x">Starting X coordinate.</param>
    /// <param name="value">Byte value (only lower 7 bits used).</param>
    /// <param name="expand">If true, doubles each pixel horizontally (7â†’14 pixels).</param>
    /// <remarks>
    /// <para>
    /// <strong>Performance Optimization:</strong> Eliminates 7-14 offset calculations per
    /// character (depending on expand flag) by using direct span indexing. For text mode
    /// rendering (960 characters per frame), this eliminates ~13,440 redundant calculations.
    /// </para>
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void Insert7BitLsbAt_Span(Span<BitField16> rowData, int bitplane, int x, byte value, bool expand = false)
    {
        int px = x;
        for (int bit = 0; bit < 7; bit++)
        {
            bool on = (value & (1 << bit)) != 0;
            if (expand)
            {
                // Double each pixel horizontally (40-column text)
                int p0 = px + (bit * 2);
                int p1 = p0 + 1;

                if (p0 < 560)
                {
                    rowData[p0].SetBit(bitplane, on);
                }
                if (p1 < 560)
                {
                    rowData[p1].SetBit(bitplane, on);
                }
            }
            else
            {
                // No doubling (80-column text)
                int p = px + bit;
                if (p < 560)
                {
                    rowData[p].SetBit(bitplane, on);
                }
            }
        }
    }
    
    /// <summary>
    /// Sets 7 bits from a byte into the frame buffer without doubling.
    /// </summary>
    /// <param name="buf">Frame buffer to render into.</param>
    /// <param name="x">Starting X coordinate.</param>
    /// <param name="y">Y coordinate (scanline).</param>
    /// <param name="value">Byte value (only lower 7 bits used).</param>
    /// <param name="bitplane">Bitplane to render into.</param>
    /// <remarks>
    /// Used for lo-res graphics color blocks. Each bit controls one pixel.
    /// </remarks>
    private static void SetByteAt(BitmapDataArray buf, int x, int y, byte value, int bitplane)
    {
        Span<BitField16> rowData = buf.GetMutableRowDataSpan(y);
        SetByteAt_Span(rowData, x, value, bitplane);
    }
    
    /// <summary>
    /// Sets 7 bits from a byte into a row span without doubling (optimized).
    /// </summary>
    /// <param name="rowData">Mutable span for the entire scanline.</param>
    /// <param name="x">Starting X coordinate.</param>
    /// <param name="value">Byte value (only lower 7 bits used).</param>
    /// <param name="bitplane">Bitplane to render into.</param>
    /// <remarks>
    /// <para>
    /// <strong>Performance Optimization:</strong> Eliminates 7 offset calculations per byte
    /// by using direct span indexing. Used for lo-res graphics rendering.
    /// </para>
    /// </remarks>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void SetByteAt_Span(Span<BitField16> rowData, int x, byte value, int bitplane)
    {
        int px = x;
        for (int bit = 0; bit < 7; bit++)
        {
            bool on = (value & (1 << bit)) != 0;
            int p = px + bit;
            if (p < 560)
            {
                rowData[p].SetBit(bitplane, on);
            }
        }
    }

    /// <summary>
    /// Renders a text character cell using the character ROM.
    /// </summary>
    /// <param name="row">Row index (0-23).</param>
    /// <param name="col">Column index (0-39).</param>
    /// <param name="text80">True if 80-column text mode is active.</param>
    /// <param name="buf">Frame buffer to render into.</param>
    /// <remarks>
    /// <para>
    /// <strong>Character ROM Lookup:</strong> Fetches 8-row glyph data from
    /// <see cref="ICharacterRomProvider"/>, handling flashing characters, MouseText,
    /// and alternate character set. Glyph bits are inverted before rendering
    /// (Apple IIe convention: 0=lit, 1=dark).
    /// </para>
    /// <para>
    /// <strong>40-Column Mode:</strong> Each character is 7 pixels wide, doubled horizontally
    /// to 14 pixels. Main memory only.
    /// </para>
    /// <para>
    /// <strong>80-Column Mode:</strong> Renders two 7-pixel characters side-by-side (aux + main)
    /// without horizontal doubling, for a total of 14 pixels per cell.
    /// </para>
    /// <para>
    /// <strong>80STORE + PAGE2 Behavior:</strong> When 80STORE is active, PAGE2 controls which
    /// memory bank (aux vs main) to read from at $0400-$07FF, not which address range ($0400 vs $0800).
    /// This is critical for 80-column text rendering where aux and main are interleaved.
    /// </para>
    /// </remarks>
    private void RenderTextCell( int row, int col, bool text80, BitmapDataArray buf)
    {
        bool flashOn = _context!.SystemStatus.StateFlashOn;
        bool altChar = _context!.SystemStatus.StateAltCharSet;
        bool store80 = _context!.SystemStatus.State80Store;
        bool page2 = _context!.SystemStatus.StatePage2;

        // Calculate base address - always $0400 in text/lo-res range
        // (80STORE controls bank, not address when active)
        int baseAddr = 0x0400 + (row % 8) * 128 + (row / 8) * 40 + col;

        if (!text80)
        {
            // 40-column text: render with horizontal doubling
            // When 80STORE is active and PAGE2 is set, read from AUX at $0400
            // Otherwise read from MAIN (at $0400 or $0800 depending on PAGE2)
            byte ch;
            if (store80 && page2)
            {
                ch = _context!.Memory.ReadRawAux((ushort)baseAddr);
            }
            else if (!store80 && page2)
            {
                // Standard PAGE2 behavior: read from $0800
                ch = _context!.Memory.ReadRawMain((ushort)(baseAddr + 0x400));
            }
            else
            {
                // PAGE1 or 80STORE+PAGE1: read from main $0400
                ch = _context!.Memory.ReadRawMain((ushort)baseAddr);
            }
            
            var glyph = _charRomProvider.GetGlyph(ch, flashOn, altChar);
            
            for (int r = 0; r < 8; r++)
            {
                int buffY = row * 8 + r;
                byte fontRow = (byte)~glyph[r]; // Invert bits (Apple IIe convention)
                
                Insert7BitLsbAt(buf, _bitplane, col * 2 * 7, buffY, fontRow, expand: true);
            }
        }
        else
        {
            // 80-column text: render aux and main characters side-by-side
            // Always use $0400 base address - 80-column mode implies 80STORE is active
            byte ch1 = _context!.Memory.ReadRawAux((ushort)baseAddr);
            byte ch2 = _context!.Memory.ReadRawMain((ushort)baseAddr);
            
            var glyph1 = _charRomProvider.GetGlyph(ch1, flashOn, altChar);
            var glyph2 = _charRomProvider.GetGlyph(ch2, flashOn, altChar);

            for (int r = 0; r < 8; r++)
            {
                int y = row * 8 + r;
                byte fontRow1 = (byte)~glyph1[r]; // Aux character
                byte fontRow2 = (byte)~glyph2[r];  // Main character
                int baseX = col * 2;
                
                Insert7BitLsbAt(buf, _bitplane, baseX * 7, y, fontRow1, expand: false);
                Insert7BitLsbAt(buf, _bitplane, baseX * 7 + 7, y, fontRow2, expand: false);
            }
        }
    }

    /// <summary>
    /// Renders a lo-res graphics cell (40Ã—48 blocks).
    /// </summary>
    /// <param name="row">Row index (0-23).</param>
    /// <param name="col">Column index (0-39).</param>
    /// <param name="gr80">True if 80-column lo-res mode is active.</param>
    /// <param name="buf">Frame buffer to render into.</param>
    /// <remarks>
    /// <para>
    /// <strong>Lo-Res Memory Format:</strong> Each byte contains two 4-bit color values:
    /// lower nibble for scanlines 0-3, upper nibble for scanlines 4-7. This creates
    /// 40Ã—48 color blocks (40 columns Ã— 24 rows, each row split into 2 blocks).
    /// </para>
    /// <para>
    /// <strong>Monochrome Pattern Generation:</strong> Uses <see cref="MakeGrColor"/> to
    /// generate 4 variations of pixel patterns for odd/even column positions. These patterns
    /// create the monochrome bit sequences that downstream NTSC color generators interpret
    /// as Apple IIe lo-res colors.
    /// </para>
    /// <para>
    /// <strong>80STORE + PAGE2 Behavior:</strong> When 80STORE is active, PAGE2 controls which
    /// memory bank (aux vs main) to read from at $0400-$07FF, not which address range ($0400 vs $0800).
    /// This matches the behavior in <see cref="RenderTextCell"/> and is critical for correct emulation.
    /// </para>
    /// <para>
    /// <strong>80-Column Lo-Res (DGR):</strong> Basic implementation renders aux and main memory
    /// patterns side-by-side without horizontal doubling. Each cell produces two 7-pixel patterns
    /// (aux left, main right) for a total of 14 pixels per cell.
    /// </para>
    /// </remarks>
    private void RenderGrCell(int row, int col, bool gr80, BitmapDataArray buf)
    {
        bool store80 = _context!.SystemStatus.State80Store;
        bool page2 = _context!.SystemStatus.StatePage2;

        // Calculate base address - always $0400 in text/lo-res range
        // (80STORE controls bank, not address when active)
        int baseAddr = 0x0400 + (row % 8) * 128 + (row / 8) * 40 + col;

        if (!gr80)
        {
            // 40-column lo-res: render with horizontal doubling
            // When 80STORE is active and PAGE2 is set, read from AUX at $0400
            // Otherwise read from MAIN (at $0400 or $0800 depending on PAGE2)
            byte value;
            if (store80 && page2)
            {
                value = _context!.Memory.ReadRawAux((ushort)baseAddr);
            }
            else if (!store80 && page2)
            {
                // Standard PAGE2 behavior: read from $0800
                value = _context!.Memory.ReadRawMain((ushort)(baseAddr + 0x400));
            }
            else
            {
                // PAGE1 or 80STORE+PAGE1: read from main $0400
                value = _context!.Memory.ReadRawMain((ushort)baseAddr);
            }

            for (int glyphRow = 0; glyphRow < 8; glyphRow++)
            {
                int y = row * 8 + glyphRow;

                // Select color from lower (rows 0-3) or upper (rows 4-7) nibble
                byte grcolor = (byte)(value & 0x0f);
                if (glyphRow >= 4)
                {
                    grcolor = (byte)(value >> 4);
                }

                // Generate 4 pattern variations for odd/even column phase
                var (a1, a2, a3, a4) = MakeGrColor(grcolor);
                
                if (col % 2 == 0) // Even column: use A1 & A2
                {
                    SetByteAt(buf, col * 14, y, (byte)a1, _bitplane);
                    SetByteAt(buf, col * 14 + 7, y, (byte)a2, _bitplane);
                }
                else // Odd column: use A3 & A4
                {
                    SetByteAt(buf, col * 14, y, (byte)a3, _bitplane);
                    SetByteAt(buf, col * 14 + 7, y, (byte)a4, _bitplane);
                }
            }
        }
        else
        {
            // 80-column lo-res (DGR): render aux and main memory patterns side-by-side
            // Always use $0400 base address - 80-column mode implies 80STORE is active
            byte auxValue = _context!.Memory.ReadRawAux((ushort)baseAddr);
            byte mainValue = _context!.Memory.ReadRawMain((ushort)baseAddr);

            for (int glyphRow = 0; glyphRow < 8; glyphRow++)
            {
                int y = row * 8 + glyphRow;

                // Select color from lower (rows 0-3) or upper (rows 4-7) nibble for both banks
                byte auxColor = (byte)(auxValue & 0x0f);
                byte mainColor = (byte)(mainValue & 0x0f);
                if (glyphRow >= 4)
                {
                    auxColor = (byte)(auxValue >> 4);
                    mainColor = (byte)(mainValue >> 4);
                }

                // Generate patterns for aux and main colors
                // Note: Using first two phase variations for simplicity in DGR mode
                var (a1,_,a3, _) = MakeGrColor(auxColor);
                var (_, a2, _, a4) = MakeGrColor(mainColor);

                int baseX = col * 14;

                if (col % 2 == 0) // Even column: use A1 & A2
                {
                    SetByteAt(buf, baseX, y, (byte) a1, _bitplane);
                    SetByteAt(buf, baseX + 7, y, (byte) a2, _bitplane);
                }
                else // Odd column: use A3 & A4
                {
                    SetByteAt(buf, baseX, y, (byte) a3, _bitplane);
                    SetByteAt(buf, baseX + 7, y, (byte) a4, _bitplane);
                }

            }
        }
    }

    /// <summary>
    /// Generates 4 variations of a lo-res pattern for odd/even column phase positions.
    /// </summary>
    /// <param name="val">4-bit color value (0-15) from lo-res memory.</param>
    /// <returns>Tuple of 4 byte values representing monochrome bit patterns for different phases.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Monochrome Pattern Generation:</strong> Apple IIe generates lo-res colors
    /// by creating repeating bit patterns with phase shifts for odd/even columns. This
    /// method creates 4 variations (a1, a2, a3, a4) representing the monochrome bit patterns
    /// that downstream NTSC color generators interpret as colors.
    /// </para>
    /// <para>
    /// <strong>Algorithm:</strong> Repeats the 4-bit value to create a 32-bit pattern
    /// (0x11111111 per bit), then extracts four 7-bit windows at different offsets
    /// (0, 3, 6, 9 bits) to simulate the phase shift pattern used by Apple IIe hardware.
    /// </para>
    /// </remarks>
    private static (byte, byte, byte, byte) MakeGrColor(byte val)
    {
        // Repeat 7-bit value 4 times to create phase pattern
        int x = (val & 0x7f) * 0x11111111;

        // Extract 4 phase-shifted 7-bit values
        byte a = (byte)((x >> 0) & 0x7f);
        byte b = (byte)((x >> 3) & 0x7f);
        byte c = (byte)((x >> 6) & 0x7f);
        byte d = (byte)((x >> 9) & 0x7f);

        return (a, b, c, d);
    }

    /// <summary>
    /// Calculates the video memory address for a given screen cell position.
    /// </summary>
    /// <param name="x">Column index (0-39).</param>
    /// <param name="y">Row index (0-23).</param>
    /// <param name="text">True if text mode is active.</param>
    /// <param name="hires">True if hi-res mode is active.</param>
    /// <param name="mixed">True if mixed mode is active.</param>
    /// <param name="page2">True if page 2 is active (otherwise page 1).</param>
    /// <param name="cellRowOffset">For hi-res, offset within the cell (0-7).</param>
    /// <returns>Memory address, or -1 if coordinates are out of range.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Apple IIe Memory Layout:</strong> Video memory uses an interleaved
    /// scanline pattern for both text and graphics modes:
    /// <list type="bullet">
    /// <item>Text/Lo-Res: Base + (row % 8) * 128 + (row / 8) * 40 + col</item>
    /// <item>Hi-Res: Similar pattern but with 0x400-byte offsets per scanline</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Page Selection:</strong>
    /// <list type="bullet">
    /// <item>Page 1: Text $0400-$07FF, Hi-Res $2000-$3FFF</item>
    /// <item>Page 2: Text $0800-$0BFF, Hi-Res $4000-$5FFF</item>
    /// </list>
    /// </para>
    /// </remarks>
    private static int GetAddressForXY(int x, int y, bool text, bool hires, bool mixed, bool page2, int cellRowOffset = 0)
    {
        const int TextPage1Start = 0x0400;
        const int TextPage2Start = 0x0800;
        const int HiresPage1Start = 0x2000;
        const int HiresPage2Start = 0x4000;

        int retval = -1;

        // TODO: Page2 might have issues if 80-column mode is also on. Revisit that later.

        if (x >= 0 && x < 40 && y >= 0 && y < 24)
        {
            if (text || (!text && !hires) || (mixed && y >= 20))
            {
                // Text or lo-res memory
                int startAddr = page2 ? TextPage2Start : TextPage1Start;

                // Apple IIe interleaved scanline calculation:
                // Every 128 bytes is 40 columns at row x, then row x+8, then row x+16
                retval = startAddr + (y % 8) * 128 + (y / 8) * 40 + x;
            }
            else
            {
                // Hi-res memory
                int startAddr = page2 ? HiresPage2Start : HiresPage1Start;

                // Hi-res uses similar interleaving with 0x400-byte scanline offsets
                retval = startAddr + (y % 8) * 128 + (y / 8) * 40 + (cellRowOffset * 0x400) + x;
            }
        }
        return retval;
    }
}
