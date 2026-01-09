using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

using Pandowdy.EmuCore;
using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.UI;

/// <summary>
/// Custom Avalonia control for rendering Apple IIe video output with NTSC color emulation.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Purpose:</strong> Provides hardware-accurate rendering of Apple IIe video modes including
/// text, low-res graphics, hi-res graphics, and mixed modes with authentic NTSC color artifact
/// simulation or monochrome display.
/// </para>
/// <para>
/// <strong>Rendering Features:</strong>
/// <list type="bullet">
/// <item>NTSC color artifact emulation (16 colors from 4-bit patterns)</item>
/// <item>Monochrome mode (green/amber phosphor simulation)</item>
/// <item>Scanline effect for CRT authenticity</item>
/// <item>Aspect ratio preservation with letterboxing</item>
/// <item>Double-scanline rendering (192 → 384 vertical resolution)</item>
/// </list>
/// </para>
/// <para>
/// <strong>Keyboard Handling:</strong> Captures keyboard input and translates it to Apple IIe
/// key codes, including support for control characters, arrow keys, and caps lock emulation.
/// </para>
/// <para>
/// <strong>Frame Source:</strong> Receives rendered frames from IFrameProvider (560x192 pixels)
/// and scales them to display resolution (563x384 with padding).
/// </para>
/// <para>
/// <strong>Performance:</strong> Uses unsafe pointer operations for fast pixel writes during
/// NTSC color generation and scanline rendering.
/// </para>
/// </remarks>
public class Apple2Display : Control
{
    /// <summary>
    /// Styled property for the bitmap being displayed.
    /// </summary>
    public static readonly StyledProperty<Bitmap?> BitmapProperty =
        AvaloniaProperty.Register<Apple2Display, Bitmap?>(nameof(Bitmap), null);

    /// <summary>
    /// Flag to suppress next TextInput event (used when handling special keys).
    /// </summary>
    private bool _suppressNextTextInput;
    
    /// <summary>
    /// Reference to the emulator core control interface for keyboard input injection.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="IEmulatorCoreInterface"/> abstraction instead of concrete VA2M type.
    /// This decouples the display control from the emulator implementation while providing
    /// thread-safe keyboard input queueing via EnqueueKey().
    /// </remarks>
    private IEmulatorCoreInterface? _machine;

    #region NTSC Color Constants

    // RGBA color constants for NTSC color artifact emulation
    // These colors match the Apple IIe's NTSC color output
    
    /// <summary>Black color (NTSC color 0).</summary>
    private const uint BlackColor = 0xFF000000;
    /// <summary>Magenta color (NTSC artifact color).</summary>
    private const uint MagentaColor = 0xFF930B7C;
    /// <summary>Dark blue color (NTSC artifact color).</summary>
    private const uint DarkBlueColor = 0xFF1F35D3;
    /// <summary>Purple/violet color (NTSC artifact color).</summary>
    private const uint PurpleColor = 0xFFBB36FF;
    /// <summary>Dark green color (NTSC artifact color).</summary>
    private const uint DarkGreenColor = 0xFF00760C;
    /// <summary>Gray color (NTSC artifact color).</summary>
    private const uint GrayColor = 0xFF7E7E7E;
    /// <summary>Medium blue color (NTSC artifact color).</summary>
    private const uint BlueColor = 0xFF07A5FF;
    /// <summary>Light blue color (NTSC artifact color).</summary>
    private const uint LightBlueColor = 0xFF6AB6FF;
    /// <summary>Brown/orange color (NTSC artifact color).</summary>
    private const uint BrownColor = 0xFF7B3F00;
    /// <summary>Orange color (NTSC artifact color).</summary>
    private const uint OrangeColor = 0xFFFF6A00;
    /// <summary>Gray 2 color (NTSC artifact color, same as Gray).</summary>
    private const uint Gray2Color = 0xFF7E7E7E;
    /// <summary>Pink color (NTSC artifact color).</summary>
    private const uint PinkColor = 0xFFFF9ACD;
    /// <summary>Bright green color (NTSC artifact color).</summary>
    private const uint GreenColor = 0xFF00FF00;
    /// <summary>Yellow color (NTSC artifact color).</summary>
    private const uint YellowColor = 0xFFFFFF00;
    /// <summary>Aqua/cyan color (NTSC artifact color).</summary>
    private const uint AquaColor = 0xFF00FFFF;
    /// <summary>White color (NTSC color 15).</summary>
    private const uint WhiteColor = 0xFFFFFFFF;

    // Alternative color palette (commented out for future experimentation)
    //private const uint BlackColor = 0xFF000000;
    //private const uint MagentaColor = 0xFFE31E60;
    //private const uint DarkBlueColor = 0xFF604EBD;
    //private const uint PurpleColor = 0xFF442FD0;
    //private const uint DarkGreenColor = 0xFF00A360;
    //private const uint GrayColor = 0xFF9C9C9C;
    //private const uint BlueColor = 0xFF14CFD0;
    //private const uint LightBlueColor = 0xFFD0C3FF;
    //private const uint BrownColor = 0xFF607203;
    //private const uint OrangeColor = 0xFFFF6A3C;
    //private const uint Gray2Color = 0xFF9C9C9C;
    //private const uint PinkColor = 0xFFFFA0D0;
    //private const uint GreenColor = 0xFF14F53C;
    //private const uint YellowColor = 0xFFD0DD8D;
    //private const uint AquaColor = 0xFF72FFD0;
    //private const uint WhiteColor = 0xFFFFFFFF;

    #endregion

    #region Frame Provider and Rendering State

    /// <summary>
    /// Frame provider supplying rendered video frames from the emulator.
    /// </summary>
    private IFrameProvider? _frameProvider;
    
    /// <summary>
    /// Most recently received frame data (560x192 bitmap).
    /// </summary>
    private BitmapDataArray? _lastFrame;

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets or sets the bitmap being displayed.
    /// </summary>
    /// <value>WriteableBitmap containing the rendered Apple IIe screen, or null.</value>
    public Bitmap? Bitmap
    {
        get => GetValue(BitmapProperty);
        set => SetValue(BitmapProperty, value);
    }

    /// <summary>
    /// Bright fringe alpha value for color artifact anti-aliasing.
    /// </summary>
    public const byte BrightFringeValue = 0xf0;
    
    /// <summary>
    /// Reduced fringe alpha value for softer color artifact edges.
    /// </summary>
    public const byte ReducedFringeValue = 0xB0;

    /// <summary>
    /// Gets or sets whether to force monochrome (green/amber) display mode.
    /// </summary>
    /// <value>True to render in monochrome, false for NTSC color.</value>
    /// <remarks>
    /// When true, all output is rendered in monochrome regardless of video mode.
    /// Simulates green or amber phosphor monitors common with the Apple IIe.
    /// </remarks>
    public bool ForceMono { set; get; } = false;
    
    /// <summary>
    /// Gets or sets whether to show CRT scanline effect.
    /// </summary>
    /// <value>True to show scanlines (3/4 brightness on odd lines), false for smooth rendering.</value>
    /// <remarks>
    /// When enabled, alternating scanlines are rendered at 3/4 brightness to simulate
    /// the appearance of a CRT monitor with visible scanlines.
    /// </remarks>
    public bool ShowScanLines { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to defringe mixed text area in mixed graphics mode.
    /// </summary>
    /// <value>True to render bottom 4 text lines in monochrome, false for full color.</value>
    /// <remarks>
    /// <para>
    /// Mixed mode displays graphics with 4 lines of text at the bottom (lines 20-23).
    /// Color artifacts on text can reduce readability.
    /// </para>
    /// <para>
    /// When enabled, the bottom 4 text lines (y >= 160) are rendered in monochrome
    /// for improved clarity while keeping graphics in color.
    /// </para>
    /// </remarks>
    public bool DefringeMixedText { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to use non-luma contrast masking for color fringing.
    /// </summary>
    /// <value>True to use alpha blending for color fringe reduction, false for full opacity.</value>
    /// <remarks>
    /// <para>
    /// When enabled, pixels adjacent to color transitions are rendered with reduced alpha
    /// (transparency) to soften color artifacts and improve text readability.
    /// </para>
    /// <para>
    /// Uses graduated alpha values (0xff, 0xc0, 0x90, 0x70, 0x50) based on distance from
    /// lit pixels to create smooth color transitions.
    /// </para>
    /// </remarks>
    public bool UseNonLumaContrastMask { get; set; } = false;

    #endregion

    #region Display Constants

    /// <summary>
    /// Source bitmap width (563 pixels).
    /// </summary>
    private const double SourceWidth = 563;
    
    /// <summary>
    /// Source bitmap height (384 pixels, double-scanned from 192).
    /// </summary>
    private const double SourceHeight = 384;
    
    /// <summary>
    /// Source aspect ratio (563:384 ≈ 1.466).
    /// </summary>
    private const double SourceAspect = SourceWidth / SourceHeight;

    #endregion

    #region Bit Plane Selection

    /// <summary>
    /// Backing field for ActiveBitPlane property.
    /// </summary>
    private int _activeBitPlane = 0;

    /// <summary>
    /// Gets or sets which bit plane to render (for debugging double-hires modes).
    /// </summary>
    /// <value>Bit plane index (0 for main, 1 for auxiliary).</value>
    /// <remarks>
    /// <para>
    /// The Apple IIe can use both main and auxiliary memory for double hi-res graphics
    /// (560x192 resolution). This property allows selecting which plane to visualize.
    /// </para>
    /// <para>
    /// <strong>Typical Values:</strong>
    /// <list type="bullet">
    /// <item>0: Main memory bit plane (normal operation)</item>
    /// <item>1: Auxiliary memory bit plane (for debugging or special effects)</item>
    /// </list>
    /// </para>
    /// </remarks>
    public int ActiveBitPlane
    {
        get => _activeBitPlane;
        set => _activeBitPlane = value;
    }

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="Apple2Display"/> control.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Initialization:</strong>
    /// <list type="bullet">
    /// <item>Creates checkerboard placeholder bitmap</item>
    /// <item>Enables keyboard focus for input handling</item>
    /// <item>Registers event handlers for keyboard input</item>
    /// <item>Sets up focus change visualization</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Keyboard Events:</strong> Registers handlers for KeyDown, KeyUp, and TextInput
    /// to capture and translate keyboard input to Apple IIe key codes.
    /// </para>
    /// </remarks>
    public Apple2Display()
    {
        Bitmap = CreateCheckerboardBitmap;
        Focusable = true;
        GotFocus += (_, __) => { InvalidateVisual(); };
        LostFocus += (_, __) => { InvalidateVisual(); };
        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;
        TextInput += OnTextInput;
    }

    #endregion

    #region Lifecycle Methods

    /// <summary>
    /// Called when the control is detached from the visual tree.
    /// </summary>
    /// <param name="e">Visual tree attachment event arguments.</param>
    /// <remarks>
    /// Unsubscribes from frame provider events to prevent memory leaks.
    /// </remarks>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_frameProvider != null)
        {
            _frameProvider.FrameAvailable -= OnFrameAvailable;
        }
    }

    #endregion

    #region Frame Provider Attachment

    /// <summary>
    /// Attaches a frame provider to supply video frames for rendering.
    /// </summary>
    /// <param name="provider">Frame provider supplying rendered emulator output.</param>
    /// <remarks>
    /// <para>
    /// <strong>Subscription Management:</strong> If a frame provider is already attached,
    /// unsubscribes from its events before subscribing to the new provider.
    /// </para>
    /// <para>
    /// <strong>Initial Frame:</strong> Retrieves the current frame immediately to ensure
    /// the display shows content even before the first frame event fires.
    /// </para>
    /// </remarks>
    public void AttachFrameProvider(IFrameProvider provider)
    {
        if (_frameProvider != null)
        {
            _frameProvider.FrameAvailable -= OnFrameAvailable;
        }
        _frameProvider = provider;
        _frameProvider.FrameAvailable += OnFrameAvailable;
        _lastFrame = _frameProvider.GetFrame();
    }

    /// <summary>
    /// Handles frame available events from the frame provider.
    /// </summary>
    /// <param name="sender">Event sender (frame provider).</param>
    /// <param name="e">Event arguments (unused).</param>
    /// <remarks>
    /// <para>
    /// Retrieves the latest frame and stores it for rendering. The actual redraw is
    /// deferred to the next render cycle triggered by the UI refresh timer.
    /// </para>
    /// <para>
    /// <strong>Performance Note:</strong> Does not call InvalidateVisual() here to avoid
    /// excessive redraws. The UI refresh timer (60 Hz) controls the actual render frequency.
    /// </para>
    /// </remarks>
    private void OnFrameAvailable(object? sender, EventArgs e)
    {
        _lastFrame = _frameProvider?.GetFrame(); // redraw deferred to timer
    }

    #endregion

    #region Layout Overrides

    /// <summary>
    /// Measures the desired size of the control.
    /// </summary>
    /// <param name="availableSize">Available size from parent layout.</param>
    /// <returns>Desired size maintaining 563:384 aspect ratio.</returns>
    /// <remarks>
    /// If available size is infinite, returns a baseline size of 700x525 pixels
    /// (maintaining approximately the correct aspect ratio).
    /// </remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        if (double.IsInfinity(availableSize.Width) || double.IsInfinity(availableSize.Height))
        {
            return new Size(700, 525); // maintain aspect ratio baseline
        }
        return availableSize;
    }

    /// <summary>
    /// Arranges the control within the final size allocated by parent layout.
    /// </summary>
    /// <param name="finalSize">Final size allocated to the control.</param>
    /// <returns>The final size (same as input).</returns>
    protected override Size ArrangeOverride(Size finalSize) => finalSize;

    #endregion

    #region Checkerboard Bitmap Creation

    /// <summary>
    /// Creates a checkerboard pattern bitmap used as a placeholder before frames are available.
    /// </summary>
    /// <value>563x384 bitmap with alternating 7x16 pixel blocks.</value>
    /// <remarks>
    /// <para>
    /// The checkerboard pattern consists of 24 rows by 80 columns of 7x16 pixel blocks,
    /// matching the Apple IIe's text mode character cell layout (80 columns x 24 rows).
    /// </para>
    /// <para>
    /// <strong>Colors:</strong> Alternates between dark gray (64) and lighter gray (80)
    /// in a checkerboard pattern.
    /// </para>
    /// </remarks>
    private static Bitmap CreateCheckerboardBitmap
    {
        get
        {
            const int numRows = 24;
            const int numCols = 80;
            const int blockWidth = 7;
            const int blockHeight = 16;
            const int bitmapWidth = 563;
            const int bitmapHeight = 384;
            var pixelData = new byte[bitmapWidth * bitmapHeight * 4];
            for (int y = 0; y < numRows * blockHeight; y++)
            {
                for (int x = 0; x < numCols * blockWidth; x++)
                {
                    int blockX = x / blockWidth;
                    int blockY = y / blockHeight;
                    bool isWhite = (blockX + blockY) % 2 == 0;
                    int pixelIndex = (y * bitmapWidth + x) * 4;
                    byte colorValue = isWhite ? (byte) 80 : (byte) 64;
                    pixelData[pixelIndex + 0] = colorValue;
                    pixelData[pixelIndex + 1] = colorValue;
                    pixelData[pixelIndex + 2] = colorValue;
                    pixelData[pixelIndex + 3] = 255;
                }
            }
            var bitmap = new WriteableBitmap(new PixelSize(bitmapWidth, bitmapHeight), new Vector(96, 96), PixelFormat.Bgra8888);
            using (var frameBuffer = bitmap.Lock())
            {
                System.Runtime.InteropServices.Marshal.Copy(pixelData, 0, frameBuffer.Address, pixelData.Length);
            }
            return bitmap;
        }
    }

    #endregion

    #region Rendering Methods

    /// <summary>
    /// Renders the control's visual content.
    /// </summary>
    /// <param name="context">Drawing context for rendering operations.</param>
    /// <remarks>
    /// <para>
    /// <strong>Rendering Pipeline:</strong>
    /// <list type="number">
    /// <item>Ensure bitmap is allocated (563x384 WriteableBitmap)</item>
    /// <item>Lock bitmap for unsafe pointer access</item>
    /// <item>For each scanline (192 lines): Render line twice (double-scan to 384 lines)</item>
    /// <item>Choose monochrome or NTSC rendering based on mode and settings</item>
    /// <item>Apply scanline effect if enabled (3/4 brightness on odd lines)</item>
    /// <item>Draw scaled bitmap with aspect ratio preservation</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Rendering Mode Selection:</strong>
    /// <list type="bullet">
    /// <item><strong>Monochrome:</strong> ForceMono, non-graphics mode, or mixed text area with DefringeMixedText</item>
    /// <item><strong>NTSC Color:</strong> Graphics mode without monochrome override</item>
    /// </list>
    /// </para>
    /// </remarks>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_frameProvider != null && _lastFrame != null && _frameProvider.Width == 560 && _frameProvider.Height == 192)
        {
            EnsureBitmapForFrame();
            if (Bitmap is WriteableBitmap wb)
            {
                using var fb = wb.Lock();
                unsafe
                {
                    byte* dst = (byte*) fb.Address;
                    int stridePixels = 563;
                    for (int y = 0; y < _frameProvider.Height; y++)
                    {
                        int outYTop = y * 2;
                        if (outYTop + 1 >= 384)
                        { break; }
                        if (!_frameProvider.IsGraphics || ForceMono || 
                            (_frameProvider.IsGraphics && _frameProvider.IsMixed && DefringeMixedText && y >= 160))
                        {
                            RenderMonochromeLine(dst, stridePixels, outYTop,
                              //  _lastFrame.GetBitplaneSpanForRow(y, ActiveBitPlane),
                              _lastFrame.GetRowDataSpan(y),
                                ShowScanLines);
                        }
                        else
                        {
                            RenderNtscLine(dst, stridePixels, outYTop,
                                //_lastFrame.GetBitplaneSpanForRow(y, ActiveBitPlane),
                                _lastFrame.GetRowDataSpan(y),
                                ShowScanLines);
                        }
                    }
                }
            }
            DrawBitmapScaled(context);
            return;
        }
        if (Bitmap == null)
        {
            context.FillRectangle(new SolidColorBrush(Colors.Red), new Rect(Bounds.Size));
            return;
        }
        DrawBitmapScaled(context);
    }

    /// <summary>
    /// Requests an immediate visual refresh of the display.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Triggers InvalidateVisual() if a frame provider and frame data are available.
    /// Typically called by the UI refresh timer (60 Hz).
    /// </para>
    /// </remarks>
    public void RequestRefresh()
    {
        if (_frameProvider != null && _lastFrame != null)
        {
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Renders a scanline in monochrome mode (green/amber phosphor simulation).
    /// </summary>
    /// <param name="dst">Pointer to destination bitmap buffer.</param>
    /// <param name="stridePixels">Width of bitmap in pixels.</param>
    /// <param name="outYTop">Y coordinate of top output scanline.</param>
    /// <param name="lineData">Source line data (560 booleans, true = pixel lit).</param>
    /// <param name="showScanLines">True to render odd scanline at 3/4 brightness.</param>
    /// <remarks>
    /// <para>
    /// <strong>Rendering:</strong> Each input pixel becomes a white or black output pixel.
    /// The scanline is rendered twice vertically (double-scan), with the second line
    /// optionally dimmed for CRT scanline effect.
    /// </para>
    /// <para>
    /// <strong>Scanline Effect:</strong> When enabled, odd scanlines (outYTop + 1) are
    /// rendered at 3/4 brightness by shifting RGB channels right by 2 bits (divide by 4,
    /// keeping 3/4 of original value).
    /// </para>
    /// </remarks>
    static private unsafe void RenderMonochromeLine(byte* dst, int stridePixels, int outYTop, ReadOnlySpan<BitField16> lineData, bool showScanLines)
    {
        for (int xPos = -3; xPos < lineData.Length; xPos++)
        {
            bool on = xPos >=0 && ((lineData[xPos].Value & 0x01) == 0x01); // simplified for full byte
            if (xPos <= stridePixels)
            {
                uint color = on ? 0xFFFFFFFFu : 0xFF000000u;
                uint dimColor = showScanLines ? (((color & 0x00fcfcfc) >> 2) | 0xff000000) : color; // 3/4 brightness when enabled
                WritePixel(dst, stridePixels, xPos+3, outYTop, color);
                WritePixel(dst, stridePixels, xPos+3, outYTop + 1, dimColor);
            }
        }
    }

    /// <summary>
    /// Renders a scanline with NTSC color artifact emulation.
    /// </summary>
    /// <param name="dst">Pointer to destination bitmap buffer.</param>
    /// <param name="stridePixels">Width of bitmap in pixels.</param>
    /// <param name="outYTop">Y coordinate of top output scanline.</param>
    /// <param name="lineData">Source line data (560 booleans, true = pixel lit).</param>
    /// <param name="showScanLines">True to render odd scanline at 3/4 brightness.</param>
    /// <remarks>
    /// <para>
    /// <strong>NTSC Color Artifact Emulation:</strong> The Apple IIe generates colors by
    /// manipulating NTSC chroma subcarrier phase. This method simulates that by examining
    /// 4-bit pixel patterns and their phase alignment.
    /// </para>
    /// <para>
    /// <strong>Algorithm:</strong>
    /// <list type="number">
    /// <item>Examine 4 consecutive pixels (bits[0..3])</item>
    /// <item>Calculate 4-bit value and phase (xPos mod 4)</item>
    /// <item>Look up NTSC color from pattern + phase</item>
    /// <item>Apply alpha blending for color fringing if enabled</item>
    /// <item>Render top scanline, then dimmed bottom scanline</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Color Fringing:</strong> When UseNonLumaContrastMask is enabled, pixels near
    /// color transitions are rendered with reduced alpha for softer edges and improved text
    /// readability.
    /// </para>
    /// </remarks>
    private unsafe void RenderNtscLine(byte* dst, int stridePixels, int outYTop,
ReadOnlySpan<BitField16> lineData,
bool showScanLines)
    {
        bool neg1 = false;
        bool neg2 = false;
        bool neg3 = false;
        for (int xPos = -3; xPos < lineData.Length; xPos++)
        {
            var bits = new bool[4];
            bits[0] = xPos >= 0 && ((lineData[xPos].Value & 0x01) == 0x01);
            bits[1] = xPos + 1 >= 0 && (xPos + 1 < lineData.Length) && ((lineData[xPos+1].Value & 0x01) == 0x01);
            bits[2] = xPos + 2 >= 0 && (xPos + 2 < lineData.Length) && ((lineData[xPos + 2].Value & 0x01) == 0x01);
            bits[3] = xPos + 3 >= 0 && (xPos + 3 < lineData.Length) && ((lineData[xPos + 3].Value & 0x01) == 0x01);
            var phase = (byte)(xPos % 4);

            if (xPos <= stridePixels)
            {
                int pixDist = 4;
                if (bits[0])
                {
                    pixDist = 0;
                }
                else if (bits[1] || neg1)
                {
                    pixDist = 1;
                }
                else if (bits[2] || neg2)
                {
                    pixDist = 2;
                }
                else if (bits[3] || neg3)
                {
                    pixDist = 3;
                }
            

                byte bitval = (byte) ((bits[0] ? 0x08 : 0x00) +
                        (bits[2] ? 0x02 : 0x00) +
                        (bits[1] ? 0x04 : 0x00) +
                        (bits[3] ? 0x01 : 0x00));

                uint color = GetNTSCColorFromBits(bitval, phase);

                byte alpha;// = 0x00; // (byte) (bits[0] ? 0xff: (UseNonLumaContrastMask? ReducedFringeValue:BrightFringeValue));
                byte[] alphas = [0xff, 0xc0, 0x90, 0x70, 0x50];
                if (UseNonLumaContrastMask) 
                { 
                    alpha = alphas[pixDist]; 
                }
                else
                { 
                    alpha = 0xff; 
                }


                WritePixel(dst, stridePixels, xPos+3, outYTop, color,alpha);
                uint dimColor = showScanLines ? (((color & 0x00fcfcfc) >> 2) | 0xff000000) : color; // 3/4 brightness when enabled

                WritePixel(dst, stridePixels, xPos + 3, outYTop + 1, dimColor, alpha);
                neg3 = neg2;
                neg2 = neg1;
                neg1 = bits[0];
            }
        }
    }

    /// <summary>
    /// Maps a 4-bit pixel pattern and phase to an NTSC color value.
    /// </summary>
    /// <param name="bitval">4-bit pixel value (0-15) from current and next 3 pixels.</param>
    /// <param name="phase">Horizontal phase (0-3) determining color from bit pattern.</param>
    /// <returns>32-bit RGBA color value.</returns>
    /// <remarks>
    /// <para>
    /// <strong>NTSC Color Generation:</strong> The Apple IIe generates 16 colors from 4-bit
    /// patterns by manipulating NTSC chroma phase. The same bit pattern produces different
    /// colors depending on horizontal position (phase).
    /// </para>
    /// <para>
    /// <strong>Color Table:</strong> Contains all 16 colors x 4 phases = 64 possible combinations.
    /// Patterns are grouped by bit value with phase variations producing color shifts.
    /// </para>
    /// <para>
    /// <strong>Example:</strong> Pattern 0001 (bit 0 set) produces:
    /// <list type="bullet">
    /// <item>Phase 0: Brown</item>
    /// <item>Phase 1: (different color based on subcarrier alignment)</item>
    /// <item>Phase 2: (different color)</item>
    /// <item>Phase 3: Dark Green (wraps to next phase cycle)</item>
    /// </list>
    /// </para>
    /// </remarks>
    static uint GetNTSCColorFromBits(byte bitval, byte phase)
    {
        phase %= 4;

        if (bitval == 0)
        { return BlackColor; }

        // 0001 0010 0100 1000
        if (bitval == 1 && phase == 0)
        { return BrownColor; }
        if (bitval == 2 && phase == 1)
        { return BrownColor; }
        if (bitval == 4 && phase == 2)
        { return BrownColor; }
        if (bitval == 8 && phase == 3)
        { return BrownColor; }

        // 0010 0100 1000 0001
        if (bitval == 2 && phase == 0)
        { return DarkGreenColor; }
        if (bitval == 4 && phase == 1)
        { return DarkGreenColor; }
        if (bitval == 8 && phase == 2)
        { return DarkGreenColor; }
        if (bitval == 1 && phase == 3)
        { return DarkGreenColor; }

        // 0011 0110 1100 1001
        if (bitval == 3 && phase == 0)
        { return GreenColor; }
        if (bitval == 6 && phase == 1)
        { return GreenColor; }
        if (bitval == 12 && phase == 2)
        { return GreenColor; }
        if (bitval == 9 && phase == 3)
        { return GreenColor; }

        // 0100 1000 0001 0010
        if (bitval == 4 && phase == 0)
        { return DarkBlueColor; }
        if (bitval == 8 && phase == 1)
        { return DarkBlueColor; }
        if (bitval == 1 && phase == 2)
        { return DarkBlueColor; }
        if (bitval == 2 && phase == 3)
        { return DarkBlueColor; }

        // 0101 1010 0101 1010
        if (bitval == 5 && phase == 0)
        { return GrayColor; }
        if (bitval == 10 && phase == 1)
        { return GrayColor; }
        if (bitval == 5 && phase == 2)
        { return GrayColor; }
        if (bitval == 10 && phase == 3)
        { return GrayColor; }

        // 0110 1100 1001 0011  
        if (bitval == 6 && phase == 0)
        { return BlueColor; }
        if (bitval == 12 && phase == 1)
        { return BlueColor; }
        if (bitval == 9 && phase == 2)
        { return BlueColor; }
        if (bitval == 3 && phase == 3)
        { return BlueColor; }

        // 0111 1110 1110 1101
        if (bitval == 7 && phase == 0)
        { return AquaColor; }
        if (bitval == 14 && phase == 1)
        { return AquaColor; }
        if (bitval == 13 && phase == 2)
        { return AquaColor; }
        if (bitval == 11 && phase == 3)
        { return AquaColor; }

        // 1000 0001 0010 0100
        if (bitval == 8 && phase == 0)
        { return MagentaColor; }
        if (bitval == 1 && phase == 1)
        { return MagentaColor; }
        if (bitval == 2 && phase == 2)
        { return MagentaColor; }
        if (bitval == 4 && phase == 3)
        { return MagentaColor; }

        // 1001 0011 0110 1100
        if (bitval == 9 && phase == 0)
        { return OrangeColor; }
        if (bitval == 3 && phase == 1)
        { return OrangeColor; }
        if (bitval == 6 && phase == 2)
        { return OrangeColor; }
        if (bitval == 12 && phase == 3)
        { return OrangeColor; }

        // 1010 0101 1010 0101
        if (bitval == 10 && phase == 0)
        { return Gray2Color; }
        if (bitval == 5 && phase == 1)
        { return Gray2Color; }
        if (bitval == 10 && phase == 2)
        { return Gray2Color; }
        if (bitval == 5 && phase == 3)
        { return Gray2Color; }

        // 1011 0111 1110 1110
        if (bitval == 11 && phase == 0)
        { return YellowColor; }
        if (bitval == 7 && phase == 1)
        { return YellowColor; }
        if (bitval == 14 && phase == 2)
        { return YellowColor; }
        if (bitval == 13 && phase == 3)
        { return YellowColor; }

        // 1100 1001 0011 0110
        if (bitval == 12 && phase == 0)
        { return PurpleColor; }
        if (bitval == 9 && phase == 1)
        { return PurpleColor; }
        if (bitval == 3 && phase == 2)
        { return PurpleColor; }
        if (bitval == 6 && phase == 3)
        { return PurpleColor; }

        // 1101 1011 1101 1110
        if (bitval == 13 && phase == 0)
        {
            return PinkColor;
        }
        if (bitval == 11 && phase == 1)
        { return PinkColor; }
        if (bitval == 7 && phase == 2)
        { return PinkColor; }
        if (bitval == 14 && phase == 3)
        { return PinkColor; }

        // 1110 1110 1111 1101
        if (bitval == 14 && phase == 0)
        { return LightBlueColor; }
        if (bitval == 13 && phase == 1)
        { return LightBlueColor; }
        if (bitval == 11 && phase == 2)
        { return LightBlueColor; }
        if (bitval == 7 && phase == 3)
        { return LightBlueColor; }

        return WhiteColor;
    }

    /// <summary>
    /// Ensures a WriteableBitmap is allocated for rendering the current frame.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If Bitmap is not a WriteableBitmap or is null, creates a new 563x384 WriteableBitmap
    /// in BGRA8888 format at 96 DPI.
    /// </para>
    /// <para>
    /// WriteableBitmap is required for unsafe pointer-based pixel writing used in
    /// RenderMonochromeLine and RenderNtscLine.
    /// </para>
    /// </remarks>
    private void EnsureBitmapForFrame()
    {
        if (Bitmap is WriteableBitmap)
        {
            return;
        }
        Bitmap = new WriteableBitmap(new PixelSize(563, 384), new Vector(96, 96), PixelFormat.Bgra8888);
    }

    /// <summary>
    /// Writes a single pixel to the bitmap buffer with alpha blending support.
    /// </summary>
    /// <param name="basePtr">Pointer to bitmap buffer base.</param>
    /// <param name="width">Width of bitmap in pixels.</param>
    /// <param name="x">X coordinate of pixel.</param>
    /// <param name="y">Y coordinate of pixel.</param>
    /// <param name="rgba">32-bit RGBA color value.</param>
    /// <param name="alpha">Alpha value (0-255, default 255 = opaque).</param>
    /// <remarks>
    /// <para>
    /// <strong>Premultiplied Alpha:</strong> Color channels are premultiplied by alpha before
    /// writing to support proper alpha blending. Uses integer math to avoid floating point overhead.
    /// </para>
    /// <para>
    /// <strong>Pixel Format:</strong> Writes in BGRA order (Blue, Green, Red, Alpha) to match
    /// Avalonia's Bgra8888 pixel format.
    /// </para>
    /// </remarks>
    private static unsafe void WritePixel(byte* basePtr, int width, int x, int y, uint rgba, byte alpha =0xff)
    {
        //alpha = (byte) (x & 0xff);
        int idx = (y * width + x) * 4;
        byte b = (byte)(rgba & 0xFF);
        byte g = (byte)((rgba >> 8) & 0xFF);
        byte r = (byte)((rgba >> 16) & 0xFF);
        // Premultiply color channels by alpha
        // Using integer math to avoid floating point
        basePtr[idx + 0] = (byte)((b * alpha) / 255); // B
        basePtr[idx + 1] = (byte)((g * alpha) / 255); // G
        basePtr[idx + 2] = (byte)((r * alpha) / 255); // R
        basePtr[idx + 3] = alpha;                      // A
    }

    /// <summary>
    /// Draws the bitmap scaled to fit the control while preserving aspect ratio.
    /// </summary>
    /// <param name="context">Drawing context for render operations.</param>
    /// <remarks>
    /// <para>
    /// <strong>Scaling Strategy:</strong>
    /// <list type="bullet">
    /// <item>Calculate available space (control bounds minus 30px padding on all sides)</item>
    /// <item>Determine which dimension is constraining (width or height)</item>
    /// <item>Scale to fit constraining dimension while preserving 563:384 aspect ratio</item>
    /// <item>Center in available space (letterbox/pillarbox as needed)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Padding:</strong> 30 pixels on all sides provides visual separation from control edges.
    /// </para>
    /// </remarks>
    private void DrawBitmapScaled(DrawingContext context)
    {
        const double padding = 30;
        if (Bitmap == null)
        {
            return;
        }
        double availableWidth = Bounds.Width - padding * 2;
        double availableHeight = Bounds.Height - padding * 2;
        double displayAspect = availableWidth / availableHeight;
        double SourceAspectLocal = SourceWidth / SourceHeight;
        double scaledWidth, scaledHeight, offsetX, offsetY;
        if (displayAspect > SourceAspectLocal)
        {
            scaledHeight = availableHeight;
            scaledWidth = availableHeight * SourceAspectLocal;
            offsetX = padding + (availableWidth - scaledWidth) / 2;
            offsetY = padding;
        }
        else
        {
            scaledWidth = availableWidth;
            scaledHeight = availableWidth / SourceAspectLocal;
            offsetX = padding;
            offsetY = padding + (availableHeight - scaledHeight) / 2;
        }
        var rect = new Rect(offsetX, offsetY, scaledWidth, scaledHeight);
        using (context.PushClip(new Rect(Bounds.Size)))
        {
            context.DrawImage(Bitmap, rect);
        }
    }

    #endregion

    #region Machine Attachment and Keyboard Support

    /// <summary>
    /// Determines if a key is a function key (F1-F24).
    /// </summary>
    /// <param name="key">Key to check.</param>
    /// <returns>True if key is between F1 and F24 inclusive.</returns>
    private static bool IsFunctionKey(Key key) => key >= Key.F1 && key <= Key.F24;

    /// <summary>
    /// Attaches the emulator core control interface to this display control.
    /// </summary>
    /// <param name="machine">Emulator core interface for keyboard input queueing.</param>
    /// <remarks>
    /// <para>
    /// <strong>Abstraction:</strong> Accepts <see cref="IEmulatorCoreInterface"/> instead of
    /// concrete VA2M type, decoupling the display from emulator implementation details.
    /// </para>
    /// <para>
    /// Called by MainWindow.Initialize() after control construction. The machine reference
    /// is used to queue keyboard input via EnqueueKey() when the user types.
    /// </para>
    /// </remarks>
    public void AttachMachine(IEmulatorCoreInterface machine) => _machine = machine;

    /// <summary>
    /// Gets whether caps lock emulation is enabled from the parent MainWindow.
    /// </summary>
    /// <returns>True if caps lock is enabled, false otherwise.</returns>
    /// <remarks>
    /// Walks up the visual tree to find the MainWindow and queries its IsCapsLockEnabledForInput property.
    /// </remarks>
    private bool GetCapsLockEnabled()
    {
        var tl = TopLevel.GetTopLevel(this);
        if (tl is MainWindow mw)
        {
            return mw.IsCapsLockEnabledForInput;
        }
        return false;
    }

    /// <summary>
    /// Gets whether caps lock emulation is currently enabled.
    /// </summary>
    private bool IsCapsLockEnabled => GetCapsLockEnabled();

    /// <summary>
    /// Handles text input events for standard character input.
    /// </summary>
    /// <param name="sender">Event sender (this control).</param>
    /// <param name="e">Text input event arguments containing input text.</param>
    /// <remarks>
    /// <para>
    /// <strong>Processing:</strong>
    /// <list type="number">
    /// <item>Check if input should be suppressed (after Alt or F-key)</item>
    /// <item>Convert newline to carriage return (Apple IIe convention)</item>
    /// <item>Apply emulator caps lock if enabled and character is lowercase letter</item>
    /// <item>Inject ASCII character with high bit set (Apple IIe key latch format)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Caps Lock Emulation:</strong> When enabled, the Apple IIe keyboard was uppercase-only.
    /// This emulates that behavior by converting lowercase input to uppercase. The TextInput event
    /// provides the actual character typed (respecting physical keyboard Shift and Caps Lock states),
    /// and we apply an additional software Caps Lock on top of that.
    /// </para>
    /// <para>
    /// <strong>High Bit:</strong> Apple IIe keyboard latch requires bit 7 set (OR with 0x80).
    /// </para>
    /// </remarks>
    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (_machine == null)
        {
            return;
        }
        if (_suppressNextTextInput)
        {
            _suppressNextTextInput = false;
            return;
        }
        if (string.IsNullOrEmpty(e.Text))
        {
            return;
        }
        
        bool emuCapsLockState = IsCapsLockEnabled;
        
        foreach (char ch in e.Text)
        {
            char c = ch == '\n' ? '\r' : ch;
            
            // Apply emulator caps lock conversion if:
            // 1. Emulator caps lock is enabled, AND
            // 2. Character is lowercase (from keyboard)
            if (emuCapsLockState && c >= 'a' && c <= 'z')
            {
                c = (char) (c - 32);  // Convert lowercase to uppercase
            }
            
            if (c <= 0x7F)
            {
                _machine.EnqueueKey((byte) ((byte) c | 0x80));
            }
        }
        e.Handled = true;
    }

    /// <summary>
    /// Handles key down events for special keys and control characters.
    /// </summary>
    /// <param name="sender">Event sender (this control).</param>
    /// <param name="e">Key event arguments containing key and modifiers.</param>
    /// <remarks>
    /// <para>
    /// <strong>Special Key Handling:</strong>
    /// <list type="bullet">
    /// <item><strong>Alt + key, F-keys:</strong> Suppresses next TextInput event (menu shortcuts)</item>
    /// <item><strong>Ctrl + A-Z:</strong> Generates control characters (0x01-0x1A)</item>
    /// <item><strong>Arrow keys:</strong> Up=0x0B, Down=0x0A, Left=0x08, Right=0x15</item>
    /// <item><strong>Delete:</strong> 0x7F (DEL character)</item>
    /// <item><strong>Backspace:</strong> 0x08 (normal), 0x7F (with Shift)</item>
    /// <item><strong>Enter:</strong> 0x0D (carriage return)</item>
    /// <item><strong>Tab:</strong> 0x09</item>
    /// <item><strong>Escape:</strong> 0x1B</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Apple IIe Control Characters:</strong> Matches Apple IIe keyboard behavior
    /// where Ctrl + letter produces ASCII control characters.
    /// </para>
    /// </remarks>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_machine == null)
        {
            return;
        }
        if ((e.KeyModifiers & KeyModifiers.Alt) != 0 || IsFunctionKey(e.Key))
        {
            _suppressNextTextInput = true;
            return;
        }
        if ((e.KeyModifiers & KeyModifiers.Control) != 0 && e.Key >= Key.A && e.Key <= Key.Z)
        {
            byte ctrl = (byte) (e.Key - Key.A + 1);
            _machine.EnqueueKey((byte) (ctrl | 0x80));
            e.Handled = true;
            return;
        }
        byte? ascii = e.Key switch
        {
            Key.Up => (byte) 0x0B,
            Key.Down => (byte) 0x0A,
            Key.Left => (byte) 0x08,
            Key.Right => (byte) 0x15,
            Key.Delete => (byte) 0x7F,
            Key.Enter => (byte) '\r',
            Key.Tab => (byte) '\t',
            Key.Escape => (byte) 0x1B,
            Key.Back => (e.KeyModifiers & KeyModifiers.Shift) != 0 ? (byte) 0x7F : (byte) 0x08,
            _ => null
        };
        if (ascii.HasValue)
        {
            _machine.EnqueueKey((byte) (ascii.Value | 0x80));
            e.Handled = true;
        }
    }

    /// <summary>
    /// Handles key up events to clear text input suppression flag.
    /// </summary>
    /// <param name="sender">Event sender (this control).</param>
    /// <param name="e">Key event arguments containing released key.</param>
    /// <remarks>
    /// Clears the _suppressNextTextInput flag when Alt or function keys are released,
    /// allowing normal text input to resume.
    /// </remarks>
    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if ((e.Key == Key.LeftAlt || e.Key == Key.RightAlt) || IsFunctionKey(e.Key))
        {
            _suppressNextTextInput = false;
        }
    }

    #endregion
}

