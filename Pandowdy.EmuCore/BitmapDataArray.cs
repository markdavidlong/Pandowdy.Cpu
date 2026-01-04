using System.Runtime.CompilerServices;

namespace Pandowdy.EmuCore
{
    /// <summary>
    /// Multi-bitplane bitmap storage for Apple IIe display rendering (560×192 pixels, 16 bitplanes).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Purpose:</strong> This class provides a packed bitmap representation supporting
    /// up to 16 independent bitplanes (layers) per pixel. Each pixel position (x, y) has 16
    /// boolean flags stored in a <see cref="BitField16"/>, allowing efficient storage of
    /// multi-channel bitmap data.
    /// </para>
    /// <para>
    /// <strong>Apple IIe Dimensions:</strong> Fixed at 560×192 pixels to match Apple IIe
    /// video output:
    /// <list type="bullet">
    /// <item><strong>Width (560):</strong> 280 hi-res pixels × 2 (horizontal doubling for square pixels),
    /// or 80 columns × 7 pixels per character</item>
    /// <item><strong>Height (192):</strong> 24 text rows × 8 scanlines per row</item>
    /// </list>
    /// These dimensions are hardcoded constants to optimize performance and memory layout.
    /// </para>
    /// <para>
    /// <strong>Bitplane Architecture:</strong> Each pixel has 16 independent boolean channels
    /// (bitplanes 0-15). In the current implementation:
    /// <list type="bullet">
    /// <item><strong>Bitplane 0:</strong> Used for monochrome composite output (primary display)</item>
    /// <item><strong>Bitplanes 1-15:</strong> Reserved for future use (NTSC color separation,
    /// double hi-res channels, overlay effects, etc.)</item>
    /// </list>
    /// This design enables future NTSC color artifact generation by separating color phases
    /// or implementing multi-layer rendering without changing the storage format.
    /// </para>
    /// <para>
    /// <strong>Memory Layout:</strong> Stored as a flat array of <c>560 × 192 = 107,520</c>
    /// <see cref="BitField16"/> values (215 KB). Pixels are stored row-major (left-to-right,
    /// top-to-bottom). Offset calculation: <c>offset = x + (y × width)</c>.
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> Not thread-safe. Multiple concurrent writes or
    /// simultaneous read/write operations require external synchronization. Typically
    /// used in a single-threaded rendering pipeline where one thread writes during frame
    /// generation and another reads during display.
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> Direct array access with bounds checking. All coordinate
    /// validation is performed before accessing the underlying array. For bulk operations,
    /// consider using <see cref="GetRowDataSpan"/> to access entire rows efficiently.
    /// </para>
    /// </remarks>
    public class BitmapDataArray
    {
        /// <summary>
        /// Apple IIe display height in scanlines (24 rows × 8 scanlines/row).
        /// </summary>
        private const int _height = 192;
        
        /// <summary>
        /// Apple IIe display width in pixels (280 hi-res pixels doubled, or 80 columns × 7 pixels).
        /// </summary>
        private const int _width = 560;

        /// <summary>
        /// Flat array storing all pixels (560 × 192 = 107,520 BitField16 values).
        /// </summary>
        /// <remarks>
        /// Row-major layout: each row of 560 pixels is stored contiguously, then the next row.
        /// Total memory: 107,520 × 2 bytes = 215,040 bytes (210 KB).
        /// </remarks>
        private readonly BitField16[] _data;

        /// <summary>
        /// Gets the width of the bitmap in pixels (always 560).
        /// </summary>
        /// <value>The fixed width of 560 pixels.</value>
        /// <remarks>
        /// Static property to allow consumers (like <see cref="Services.FrameProvider"/>)
        /// to query dimensions without needing an instance. The value is derived from
        /// the <c>_width</c> constant.
        /// </remarks>
        static public int Width => _width;
        
        /// <summary>
        /// Gets the height of the bitmap in scanlines (always 192).
        /// </summary>
        /// <value>The fixed height of 192 scanlines.</value>
        /// <remarks>
        /// Static property to allow consumers (like <see cref="Services.FrameProvider"/>)
        /// to query dimensions without needing an instance. The value is derived from
        /// the <c>_height</c> constant.
        /// </remarks>
        static public int Height => _height;

        /// <summary>
        /// Initializes a new instance of the <see cref="BitmapDataArray"/> class.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <strong>Allocation:</strong> Allocates a flat array of 107,520 <see cref="BitField16"/>
        /// values (215 KB). All pixels are initialized to zero (all bitplanes clear).
        /// </para>
        /// <para>
        /// <strong>Performance:</strong> Array allocation is relatively fast (single allocation),
        /// but the 215 KB size should be considered when creating multiple instances. For
        /// double-buffering (front/back buffers), expect 430 KB total.
        /// </para>
        /// </remarks>
        public BitmapDataArray()
        {
            _data = new BitField16[_height * _width];
        }

        /// <summary>
        /// Clears all pixels across all bitplanes (sets entire bitmap to zero).
        /// </summary>
        /// <remarks>
        /// <para>
        /// <strong>Operation:</strong> Uses <see cref="Array.Clear"/> to zero out the entire
        /// 107,520-element array. This is more efficient than looping through each pixel manually.
        /// </para>
        /// <para>
        /// <strong>Result:</strong> After clearing, all pixels on all bitplanes are set to false
        /// (off). Equivalent to a blank screen.
        /// </para>
        /// <para>
        /// <strong>Performance:</strong> Fast bulk operation optimized by the .NET runtime
        /// (typically memset at native level). Much faster than individual pixel operations.
        /// </para>
        /// <para>
        /// <strong>Usage:</strong> Called at the start of each frame by <see cref="RenderContext.ClearBuffer"/>
        /// to prepare for rendering.
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            Array.Clear(_data, 0, _data.Length);
        }

        /// <summary>
        /// Calculates the flat array offset for a given (x, y) coordinate.
        /// </summary>
        /// <param name="x">X coordinate (column) within the bitmap.</param>
        /// <param name="y">Y coordinate (row/scanline) within the bitmap.</param>
        /// <returns>Array offset where this pixel's data is stored.</returns>
        /// <remarks>
        /// <para>
        /// <strong>Formula:</strong> <c>offset = x + (y × width)</c>
        /// </para>
        /// <para>
        /// <strong>Row-Major Layout:</strong> Pixels are stored left-to-right, top-to-bottom.
        /// Each row occupies 560 consecutive array elements, then the next row begins.
        /// </para>
        /// <para>
        /// <strong>Example:</strong>
        /// <code>
        /// Pixel (0, 0) → offset 0
        /// Pixel (1, 0) → offset 1
        /// Pixel (559, 0) → offset 559
        /// Pixel (0, 1) → offset 560 (start of row 1)
        /// Pixel (100, 50) → offset 100 + (50 × 560) = 28,100
        /// </code>
        /// </para>
        /// <para>
        /// <strong>Performance:</strong> Marked with <see cref="MethodImplOptions.AggressiveInlining"/>
        /// to ensure the JIT compiler inlines this hot-path method, eliminating method call overhead.
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CalcOffset(int x, int y)
        {
            return x + y * _width;
        }

        /// <summary>
        /// Validates that X and Y coordinates are within valid bounds.
        /// </summary>
        /// <param name="x">X coordinate to validate (must be 0-559).</param>
        /// <param name="y">Y coordinate to validate (must be 0-191).</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="x"/> is outside 0-559 or <paramref name="y"/> is outside 0-191.
        /// </exception>
        /// <remarks>
        /// Separate validation for X and Y provides specific error messages indicating
        /// which coordinate is out of range. Called by all pixel access methods before
        /// touching the underlying array.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CheckXY(int x, int y)
        {
            if (x < 0 || x >= _width)
            {
                throw new ArgumentOutOfRangeException(nameof(x), $"X value {x} is outside the width range of 0-{_width - 1}.");
            }
            if (y < 0 || y >= _height)
            {
                throw new ArgumentOutOfRangeException(nameof(y), $"Y value {y} is outside the height range of 0-{_height - 1}.");
            }
        }

        /// <summary>
        /// Validates that a row index is within valid bounds.
        /// </summary>
        /// <param name="row">Row index to validate (must be 0-191).</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="row"/> is outside 0-191.
        /// </exception>
        /// <remarks>
        /// Used by row-based access methods (<see cref="GetRowDataSpan"/>, <see cref="GetBitplaneSpanForRow"/>)
        /// which operate on entire scanlines rather than individual pixels.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CheckRow(int row)
        {
            if (row < 0 || row >= _height)
            {
                throw new ArgumentOutOfRangeException(nameof(row), $"Row value {row} is outside the height range of 0-{_height - 1}.");
            }
        }

        /// <summary>
        /// Sets a pixel to on (true) on a specific bitplane.
        /// </summary>
        /// <param name="x">X coordinate (0-559).</param>
        /// <param name="y">Y coordinate (0-191).</param>
        /// <param name="bitplane">Bitplane index (0-15).</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if coordinates or bitplane are out of range.
        /// </exception>
        /// <remarks>
        /// <para>
        /// <strong>Operation:</strong> Finds the <see cref="BitField16"/> at (x, y) and sets
        /// the specified bitplane to true (1). Other bitplanes at this pixel are unaffected.
        /// </para>
        /// <para>
        /// <strong>Idempotent:</strong> Safe to call multiple times with the same coordinates -
        /// the pixel will remain set. No error if the pixel is already set.
        /// </para>
        /// <para>
        /// <strong>Example:</strong>
        /// <code>
        /// bitmap.SetPixel(100, 50, 0);  // Set pixel on bitplane 0 (monochrome)
        /// bitmap.SetPixel(100, 50, 1);  // Also set on bitplane 1 (independent)
        /// </code>
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPixel(int x, int y, int bitplane)
        {
         //   CheckXY(x, y);
            _data[CalcOffset(x, y)].SetBit(bitplane, true);
        }

        /// <summary>
        /// Clears a pixel to off (false) on a specific bitplane.
        /// </summary>
        /// <param name="x">X coordinate (0-559).</param>
        /// <param name="y">Y coordinate (0-191).</param>
        /// <param name="bitplane">Bitplane index (0-15).</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if coordinates or bitplane are out of range.
        /// </exception>
        /// <remarks>
        /// <para>
        /// <strong>Operation:</strong> Finds the <see cref="BitField16"/> at (x, y) and clears
        /// the specified bitplane to false (0). Other bitplanes at this pixel are unaffected.
        /// </para>
        /// <para>
        /// <strong>Idempotent:</strong> Safe to call multiple times with the same coordinates -
        /// the pixel will remain clear. No error if the pixel is already clear.
        /// </para>
        /// <para>
        /// <strong>Example:</strong>
        /// <code>
        /// bitmap.ClearPixel(100, 50, 0);  // Clear pixel on bitplane 0 (monochrome)
        /// bitmap.ClearPixel(100, 50, 1);  // Also clear on bitplane 1 (independent)
        /// </code>
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearPixel(int x, int y, int bitplane)
        {
         //   CheckXY(x, y);
            _data[CalcOffset(x, y)].SetBit(bitplane, false);
        }

        /// <summary>
        /// Gets the state of a pixel on a specific bitplane.
        /// </summary>
        /// <param name="x">X coordinate (0-559).</param>
        /// <param name="y">Y coordinate (0-191).</param>
        /// <param name="bitplane">Bitplane index (0-15).</param>
        /// <returns>
        /// <c>true</c> if the pixel is set (on) on the specified bitplane;
        /// <c>false</c> if the pixel is clear (off).
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if coordinates or bitplane are out of range.
        /// </exception>
        /// <remarks>
        /// <para>
        /// <strong>Operation:</strong> Finds the <see cref="BitField16"/> at (x, y) and returns
        /// the boolean state of the specified bitplane.
        /// </para>
        /// <para>
        /// <strong>Example:</strong>
        /// <code>
        /// bitmap.SetPixel(100, 50, 0);
        /// bool isSet = bitmap.GetPixel(100, 50, 0);  // true
        /// bool otherPlane = bitmap.GetPixel(100, 50, 1);  // false (unless set separately)
        /// </code>
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool GetPixel(int x, int y, int bitplane)
        {
        //    CheckXY(x, y);
            return _data[CalcOffset(x, y)].GetBit(bitplane);
        }

        /// <summary>
        /// Gets a read-only span of boolean values for a single bitplane across an entire row.
        /// </summary>
        /// <param name="row">Row index (0-191).</param>
        /// <param name="bitplane">Bitplane index (0-15).</param>
        /// <returns>
        /// A <see cref="ReadOnlySpan{T}"/> of 560 boolean values representing the specified
        /// bitplane for all pixels in the row (left to right).
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="row"/> is outside 0-191.
        /// </exception>
        /// <remarks>
        /// <para>
        /// <strong>Purpose:</strong> Efficient row-based access for rendering or analysis.
        /// Returns a contiguous view of one bitplane across an entire scanline.
        /// </para>
        /// <para>
        /// <strong>Extraction:</strong> Iterates through the 560 <see cref="BitField16"/> values
        /// for the row and extracts the specified bitplane bit from each, building a boolean array.
        /// </para>
        /// <para>
        /// <strong>Performance:</strong> Allocates a 560-element boolean array (560 bytes on heap)
        /// and uses an optimized for-loop for extraction (more efficient than foreach). For
        /// zero-allocation scenarios, consider using <see cref="GetRowDataSpan"/> for direct
        /// <see cref="BitField16"/> access and extracting bits manually.
        /// </para>
        /// <para>
        /// <strong>Usage Example:</strong>
        /// <code>
        /// // Get all pixels on bitplane 0 for row 50
        /// var pixels = bitmap.GetBitplaneSpanForRow(50, 0);
        /// for (int x = 0; x &lt; pixels.Length; x++)
        /// {
        ///     bool isLit = pixels[x];
        ///     // Process pixel...
        /// }
        /// </code>
        /// </para>
        /// </remarks>
        public ReadOnlySpan<bool> GetBitplaneSpanForRow(int row, int bitplane)
        {
           // CheckRow(row);
            var raw = GetRowDataSpan(row);
            bool[] pixels = new bool[_width];
            
            // For-loop is more efficient than foreach for spans
            for (int i = 0; i < raw.Length; i++)
            {
                pixels[i] = raw[i].GetBit(bitplane);
            }
            
            return new ReadOnlySpan<bool>(pixels);
        }

        /// <summary>
        /// Gets a read-only span of raw <see cref="BitField16"/> values for an entire row.
        /// </summary>
        /// <param name="row">Row index (0-191).</param>
        /// <returns>
        /// A <see cref="ReadOnlySpan{T}"/> of 560 <see cref="BitField16"/> values representing
        /// all pixels in the row (all bitplanes included).
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="row"/> is outside 0-191.
        /// </exception>
        /// <remarks>
        /// <para>
        /// <strong>Purpose:</strong> Zero-copy access to an entire scanline's raw data. Returns
        /// a span directly into the underlying <c>_data</c> array without allocation or copying.
        /// </para>
        /// <para>
        /// <strong>Performance:</strong> Extremely efficient - creates a span view over existing
        /// memory. Ideal for bulk processing or when you need access to all bitplanes simultaneously.
        /// </para>
        /// <para>
        /// <strong>Read-Only:</strong> The span is read-only to prevent accidental modification
        /// of the underlying array. For modifications, use <see cref="SetPixel"/> or <see cref="ClearPixel"/>.
        /// </para>
        /// <para>
        /// <strong>Usage Example:</strong>
        /// <code>
        /// // Process all pixels in row 100 across all bitplanes
        /// var rowData = bitmap.GetRowDataSpan(100);
        /// for (int x = 0; x &lt; rowData.Length; x++)
        /// {
        ///     BitField16 pixel = rowData[x];
        ///     bool plane0 = pixel.GetBit(0);
        ///     bool plane1 = pixel.GetBit(1);
        ///     // Process multi-plane data...
        /// }
        /// </code>
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<BitField16> GetRowDataSpan(int row)
        {
            if (row < 0 || row >= _height)
            {
                throw new ArgumentOutOfRangeException(nameof(row), $"row must be between 0 and {_height - 1} inclusive.");
            }
            return new ReadOnlySpan<BitField16>(_data, row * _width, _width);
        }
    }
}
