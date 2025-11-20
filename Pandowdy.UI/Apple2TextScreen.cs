using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.Input;
using Pandowdy.Core;
using System.Threading;

namespace Pandowdy.UI;

/// <summary>
/// A custom control for displaying Apple II text screen output.
/// Renders a561x384 monochrome bitmap with pixel-perfect scaling (no interpolation).
/// Maintains aspect ratio and prevents antialiasing for authentic retro appearance.
/// </summary>
public class Apple2TextScreen : Control
{
    /// <summary>
    /// Defines the Bitmap attached property for the screen image.
    /// </summary>
    public static readonly StyledProperty<Bitmap?> BitmapProperty =
    AvaloniaProperty.Register<Apple2TextScreen, Bitmap?>(
    nameof(Bitmap),
    null);

    /// <summary>
    /// Defines the Use80Cols property - whether to use80-column mode.
    /// </summary>
    public static readonly StyledProperty<bool> Use80ColsProperty =
    AvaloniaProperty.Register<Apple2TextScreen, bool>(
    nameof(Use80Cols),
    false);

    private IMappedMemory? _memorySource;
    private bool _suppressNextTextInput;
    private volatile bool _dirty;
    private volatile bool _applyScheduled;
    private VA2M? _machine;
    private VA2MBus? _vblankBus; // track subscription target to unsubscribe

    // Track dirty cells (40x24 =960)
    private readonly bool[] _cellDirty = new bool[40 *24];
    private readonly object _dirtyLock = new();

    private IFrameProvider? _frameProvider;
    private byte[]? _lastFrame;
    private DispatcherTimer? _refreshTimer; // wall-clock display timer (?60Hz)

    /// <summary>
    /// Optional mapped memory source this screen listens to for write notifications.
    /// When set, the control subscribes to MemoryWritten and MemoryBlockWritten events.
    /// </summary>
    public IMappedMemory? MemorySource
    {
        get => _memorySource;
        set
        {
            if (!ReferenceEquals(_memorySource, value))
            {
                DetachMemory(_memorySource);
                _memorySource = value;
                AttachMemory(_memorySource);

            }
        }
    }

    /// <summary>
    /// Gets or sets the bitmap to display on the screen.
    /// </summary>
    public Bitmap? Bitmap
    {
        get => GetValue(BitmapProperty);
        set => SetValue(BitmapProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to use80-column mode (false =40-column mode).
    /// Default: false (40-column mode)
    /// </summary>
    public bool Use80Cols
    {
        get => GetValue(Use80ColsProperty);
        set => SetValue(Use80ColsProperty, value);
    }

    // Apple II screen dimensions
    private const double SourceWidth = 561;
    private const double SourceHeight = 384;
    private const double SourceAspect = SourceWidth / SourceHeight;



    public Apple2TextScreen()
    {
        // Create a default checkerboard pattern
        Bitmap = CreateCheckerboardBitmap;
        Focusable = true;
        GotFocus += (_, __) => InvalidateVisual();
        LostFocus += (_, __) => InvalidateVisual();
        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;
        TextInput += OnTextInput; // use text input to get proper ASCII with layout
        // 60Hz wall-clock render cadence
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000.0 / 60.0) };
        _refreshTimer.Tick += (_, __) =>
        {
            if (_frameProvider != null && _lastFrame != null)
            {
                // Redraw current frame snapshot
                InvalidateVisual();
            }
            else if (_dirty)
            {
                // Fallback legacy path still uses dirty tracking
                InvalidateVisual();
            }
        };
        _refreshTimer.Start();
    }

    /// <summary>
    /// Convenience constructor to wire up a memory source.
    /// </summary>
    public Apple2TextScreen(IMappedMemory memory)
    : this()
    {
        MemorySource = memory;
    }

    private void AttachMemory(IMappedMemory? source)
    {
        if (source == null) return;
        source.MemoryWritten += OnMemoryWritten;
        source.MemoryBlockWritten += OnMemoryBlockWritten;
        // initial full refresh
        MarkAllCellsDirty();
    }

    private void DetachMemory(IMappedMemory? source)
    {
        if (source == null) return;
        source.MemoryWritten -= OnMemoryWritten;
        source.MemoryBlockWritten -= OnMemoryBlockWritten;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_refreshTimer != null)
        {
            _refreshTimer.Stop();
            _refreshTimer = null;
        }
        if (_vblankBus != null)
        {
            _vblankBus.VBlank -= OnVBlank;
            _vblankBus = null;
        }
        if (_memorySource != null)
        {
            DetachMemory(_memorySource);
        }
        if (_frameProvider != null)
        {
            _frameProvider.FrameAvailable -= OnFrameAvailable;
        }
    }

    private void MarkAllCellsDirty()
    {
        lock (_dirtyLock)
        {
            for (int i =0; i < _cellDirty.Length; i++) _cellDirty[i] = true;
            _dirty = true;
        }
    }

    public void SubscribeToVBlank(VA2MBus bus)
    {
        if (!ReferenceEquals(_vblankBus, bus))
        {
            if (_vblankBus != null) _vblankBus.VBlank -= OnVBlank;
            _vblankBus = bus;
            _vblankBus.VBlank += OnVBlank;
        }
    }

    public void UnsubscribeFromVBlank(VA2MBus bus)
    {
        if (ReferenceEquals(_vblankBus, bus))
        {
            _vblankBus.VBlank -= OnVBlank;
            _vblankBus = null;
        }
    }

    private void OnVBlank(object? sender, EventArgs e)
    {
        if (!_dirty) return;
        if (_applyScheduled) return; // coalesce if a post is already queued
        _applyScheduled = true;
        Dispatcher.UIThread.Post(() =>
        {
            _applyScheduled = false;
            ApplyDirtyAndInvalidate();
        }, DispatcherPriority.Render);
    }

    private void ApplyDirtyAndInvalidate()
    {
        if (!IsEffectivelyVisible || _memorySource == null) { _dirty = false; return; }
        bool any = false;
        // Walk text page addresses; draw only dirty cells
        for (int addr =0x400; addr <0x800; addr++)
        {
            int off = AddressToOffset(addr);
            if (off <0) continue;
            bool consume;
            lock (_dirtyLock)
            {
                consume = _cellDirty[off];
                if (consume) _cellDirty[off] = false; // consume under lock to avoid missing late writes
            }
            if (!consume) continue;
            int x = off %40;
            int y = off /40;
            byte val = _memorySource.Read((ushort)addr);
            SetChar(x, y, val);
            any = true;
        }
        if (any)
        {
            InvalidateVisual();
        }
        _dirty = false;
    }

    private void OnMemoryWritten(object? sender, MemoryAccessEventArgs e)
    {
        // Record dirtiness only; no UI thread marshalling per write
        if (e.Address >=0x400 && e.Address <0x800)
        {
            int off = AddressToOffset(e.Address);
            if (off >=0)
            {
                lock (_dirtyLock) { _cellDirty[off] = true; }
                _dirty = true;
            }
        }
    }

    private void OnMemoryBlockWritten(object? sender, MemoryAccessEventArgs e)
    {
        int start = e.Address;
        int end = e.Address + e.Length;
        for (int addr = start; addr < end; addr++)
        {
            if (addr >=0x400 && addr <0x800)
            {
                int off = AddressToOffset(addr);
                if (off >=0)
                {
                    lock (_dirtyLock) { _cellDirty[off] = true; }
                    _dirty = true;
                }
            }
        }
    }

    private void UpdateScreenBlock(int start, int end)
    {
        // No immediate drawing here; VBlank will handle rendering of dirty cells
        for (int addr = start; addr < end; addr++)
        {
            if (addr >=0x400 && addr <0x800)
            {
                int off = AddressToOffset(addr);
                if (off >=0)
                {
                    lock (_dirtyLock) { _cellDirty[off] = true; }
                    _dirty = true;
                }
            }
        }
    }

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

    private void OnFrameAvailable(object? sender, EventArgs e)
    {
        // Worker thread invokes this; just update buffer reference, do not invalidate here.
        _lastFrame = _frameProvider?.GetFrame();
        // UI will redraw on next 60Hz tick.
    }

    /// <summary>
    /// Measures the desired size of the control.
    /// Returns full available size to fill parent container.
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        // If we have infinite space, use a reasonable default
        if (double.IsInfinity(availableSize.Width) || double.IsInfinity(availableSize.Height))
        {
            return new Size(700, 525); //561:384 aspect ratio
        }

        // Return the full available size - we'll center the content in Render with padding
        return availableSize;
    }

    /// <summary>
    /// Arranges the control to fill available space while maintaining aspect ratio.
    /// </summary>
    protected override Size ArrangeOverride(Size finalSize)
    {
        return finalSize;
    }

    /// <summary>
    /// Creates a checkerboard pattern bitmap for the default display.
    /// Pattern:14x16 pixel blocks in alternating black/white.
    /// Resolution:561x384 (4014-pixel blocks wide ×2416-pixel blocks tall +1 pixel horizontal). 
    /// </summary>
    /// <returns>A new Bitmap with checkerboard pattern.</returns>
    private static Bitmap CreateCheckerboardBitmap
    {
        get
        {
            const int numRows = 24;
            const int numCols = 80; //80 max,40 colum doubles pixel values.
            const int blockWidth = 7;
            const int blockHeight = 16;
            const int bitmapWidth = 561; //40 blocks ×14 pixels (+1 pixel)
            const int bitmapHeight = 384; //24 blocks ×16 pixels

            // Create pixel data (BGRA format,32 bits per pixel)
            var pixelData = new byte[bitmapWidth * bitmapHeight * 4];

            // Fill with checkerboard pattern
            for (int y = 0; y < numRows * blockHeight; y++)
            {
                for (int x = 0; x < numCols * blockWidth; x++)
                {
                    // Determine which block this pixel belongs to
                    int blockX = x / blockWidth;
                    int blockY = y / blockHeight;

                    // Checkerboard: alternate black/white based on block coordinates
                    bool isWhite = (blockX + blockY) % 2 == 0;

                    // Calculate pixel index in BGRA format
                    int pixelIndex = (y * bitmapWidth + x) * 4;

                    // Set BGRA values (B, G, R, A)
                    byte colorValue = isWhite ? (byte) 80 : (byte) 64;
                    pixelData[pixelIndex + 0] = colorValue; // B
                    pixelData[pixelIndex + 1] = colorValue; // G
                    pixelData[pixelIndex + 2] = colorValue; // R
                    pixelData[pixelIndex + 3] = 255; // A (fully opaque)
                }
            }

            // Create bitmap from pixel data
            var bitmap = new WriteableBitmap(
            new PixelSize(bitmapWidth, bitmapHeight),
            new Vector(96, 96), // DPI
            PixelFormat.Bgra8888);

            // Copy pixel data to bitmap
            using (var frameBuffer = bitmap.Lock())
            {
                System.Runtime.InteropServices.Marshal.Copy(
                pixelData, 0, frameBuffer.Address, pixelData.Length);
            }

            return bitmap;
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_frameProvider != null && _lastFrame != null && _frameProvider.Width == 80 && _frameProvider.Height == 192)
        {
            EnsureBitmapForFrame();
            if (Bitmap is WriteableBitmap wb)
            {
                using var fb = wb.Lock();
                unsafe
                {
                    byte* dst = (byte*)fb.Address;
                    int stridePixels = 561;
                    for (int y = 0; y < _frameProvider.Height; y++)
                    {
                        int outYTop = y * 2;
                        if (outYTop + 1 >= 384) break;
                        for (int xByte = 0; xByte < 80; xByte++)
                        {
                            byte bits = _lastFrame[y * 80 + xByte];
                            for (int bit = 0; bit < 7; bit++)
                            {
                                bool on = (bits & (1 << bit)) == 0;
                                int outX = xByte * 7 + bit;
                                if (outX >= 561) break;
                                uint color = on ? 0xFFFFFFFFu : 0xFF000000u;
                                WritePixel(dst, stridePixels, outX, outYTop, color);
                                WritePixel(dst, stridePixels, outX, outYTop + 1, color);
                            }
                        }
                    }
                }
            }
            DrawBitmapScaled(context);
            return;
        }
        // fallback legacy path
        if (Bitmap == null)
        {
            context.FillRectangle(new SolidColorBrush(Colors.Black), new Rect(Bounds.Size));
            return;
        }
        DrawBitmapScaled(context);
    }

    private void EnsureBitmapForFrame()
    {
        if (Bitmap is WriteableBitmap) return;
        Bitmap = new WriteableBitmap(new PixelSize(561, 384), new Vector(96,96), PixelFormat.Bgra8888);
    }

    private static unsafe void WritePixel(byte* basePtr, int width, int x, int y, uint rgba)
    {
        int idx = (y * width + x) * 4;
        basePtr[idx + 0] = (byte)(rgba & 0xFF);
        basePtr[idx + 1] = (byte)((rgba >> 8) & 0xFF);
        basePtr[idx + 2] = (byte)((rgba >> 16) & 0xFF);
        basePtr[idx + 3] = 0xFF;
    }

    private void DrawBitmapScaled(DrawingContext context)
    {
        const double padding = 30;
        if (Bitmap == null) return;
        double availableWidth = Bounds.Width - padding * 2;
        double availableHeight = Bounds.Height - padding * 2;
        double displayAspect = availableWidth / availableHeight;
        double SourceWidth = 561, SourceHeight = 384, SourceAspect = SourceWidth / SourceHeight;
        double scaledWidth, scaledHeight, offsetX, offsetY;
        if (displayAspect > SourceAspect)
        {
            scaledHeight = availableHeight;
            scaledWidth = availableHeight * SourceAspect;
            offsetX = padding + (availableWidth - scaledWidth) / 2;
            offsetY = padding;
        }
        else
        {
            scaledWidth = availableWidth;
            scaledHeight = availableWidth / SourceAspect;
            offsetX = padding;
            offsetY = padding + (availableHeight - scaledHeight) / 2;
        }
        var rect = new Rect(offsetX, offsetY, scaledWidth, scaledHeight);
        using (context.PushClip(new Rect(Bounds.Size)))
        {
            context.DrawImage(Bitmap, rect);
        }
    }

    /// <summary>
    /// Sets a character at the specified screen position.
    /// </summary>
    /// <param name="x">Column (0-39 for40-column mode,0-79 for80-column mode)</param>
    /// <param name="y">Row (0-23)</param>
    /// <param name="val">Character code (0-255)</param>
    /// <exception cref="ArgumentOutOfRangeException">If coordinates are out of valid range</exception>
    public void SetChar(int x, int y, byte val)
    {
        // Validate x coordinate based on mode
        int maxX = Use80Cols ? 79 : 39;
        if (x < 0 || x > maxX)
        {
            throw new ArgumentOutOfRangeException(nameof(x),
            $"X coordinate must be between0 and {maxX} (Use80Cols={Use80Cols})");
        }

        // Validate y coordinate
        if (y < 0 || y > 23)
        {
            throw new ArgumentOutOfRangeException(nameof(y), "Y coordinate must be between0 and23");
        }

        // Cannot modify bitmap if it's not a WriteableBitmap
        if (Bitmap is not WriteableBitmap writableBitmap)
        {
            return;
        }

        const int bitmapWidth = 561;
        const int bitmapHeight = 384;
        const int charWidth = 7;
        const int charHeight = 8;

        // Get the character bitmap data from the font ROM
        byte[] charBitmap = Apple2Font.GetCharacterBitmap(val);

        // Lock the bitmap for pixel access
        using (var frameBuffer = writableBitmap.Lock())
        {
            unsafe
            {
                byte* pixelPtr = (byte*) frameBuffer.Address;

                // For each row of the character
                for (int row = 0; row < charHeight; row++)
                {
                    byte rowData = charBitmap[row];
                    int screenY = y * charHeight + row;

                    // For each pixel in the row (7 pixels wide)
                    for (int col = 0; col < charWidth; col++)
                    {
                        // Extract pixel from rowData (LSB = leftmost pixel)
                        bool isPixelSet = (rowData & 1 << col) == 0;

                        // Calculate screen X position
                        int screenX = Use80Cols ? x * charWidth : x * charWidth * 2;

                        // If40-column mode, double the pixel width
                        int pixelCount = Use80Cols ? 1 : 2;

                        for (int p = 0; p < pixelCount; p++)
                        {
                            int finalScreenX = screenX + col * pixelCount + p;

                            // Bounds check
                            if (finalScreenX < bitmapWidth && screenY < bitmapHeight)
                            {
                                // Calculate pixel index in BGRA format (4 bytes per pixel)
                                int pixelIndex = (screenY * 2 * bitmapWidth + finalScreenX) * 4;

                                // Set BGRA values (B, G, R, A)
                                byte colorValue = isPixelSet ? (byte) 255 : (byte) 0;
                                pixelPtr[pixelIndex + 0] = colorValue; // B
                                pixelPtr[pixelIndex + 1] = colorValue; // G
                                pixelPtr[pixelIndex + 2] = colorValue; // R
                                pixelPtr[pixelIndex + 3] = 255; // A (fully opaque)
                                pixelPtr[pixelIndex + bitmapWidth * 4 + 0] = colorValue; // B
                                pixelPtr[pixelIndex + bitmapWidth * 4 + 1] = colorValue; // G
                                pixelPtr[pixelIndex + bitmapWidth * 4 + 2] = colorValue; // R
                                pixelPtr[pixelIndex + bitmapWidth * 4 + 3] = 255;
                            }
                        }
                    }
                }
            }
        }

        // Redraw is driven by VBlank
    }

    // The returned offset (if >=0) can be interpreted as column = offset %40, row = offset /40
    // TODO: Handle80 columns. Might have to change guard values and constants to handle whatever memory config is used. Forgive the magic numbers for now.
    private static int AddressToOffset(int address)
    {
        if (address < 0x400 || address >= 0x800)
        {
            throw new ArgumentOutOfRangeException(nameof(address), "Address must be in range0x400-0x7FF for text screen memory.");
        }

        address -= 0x400;

        var macroline_x = address % 128; //0-127 (0-119 visible,120-127 screen hole)
        var macroline_y = address / 128; //0-7

        if (macroline_x >= 120) // screen hole
        {
            return -1;
        }

        int section = macroline_x / 40; //0-2
        int row = macroline_y + 8 * section; //0-23

        return macroline_x % 40 + 40 * row;
    }

    private bool GetCapsLockEnabled()
    {
        var tl = TopLevel.GetTopLevel(this);
        if (tl is MainWindow mw)
        {
            // Access MainWindow flag via internal accessor
            return mw.IsCapsLockEnabledForInput;
        }
        return false;
    }

    private bool IsCapsLockEnabled => GetCapsLockEnabled();

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (_machine == null)
            return;
        if (_suppressNextTextInput)
        { _suppressNextTextInput = false; return; }
        if (string.IsNullOrEmpty(e.Text))
            return;

        foreach (char ch in e.Text)
        {
            char c = ch;
            if (c == '\n')
                c = '\r';
            if (IsCapsLockEnabled && c >= 'a' && c <= 'z')
                c = (char) (c - 32);
            if (c <= 0x7F)
            {
                byte value = (byte) ((byte) c | 0x80);
                _machine.InjectKey(value);
            }
        }
        e.Handled = true;
    }
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_machine == null)
        {
            return;
        }

        // If Alt is held, or a function key is pressed, don't handle here; let the Window menu handle it.
        if ((e.KeyModifiers & KeyModifiers.Alt) != 0 || IsFunctionKey(e.Key))
        {
            _suppressNextTextInput = true;
            return;
        }

        // Handle control-key combinations to generate control codes (Ctrl+A =>0x01 ... Ctrl+Z =>0x1A)
        if ((e.KeyModifiers & KeyModifiers.Control) != 0 && e.Key >= Key.A && e.Key <= Key.Z)
        {
            byte ctrl = (byte) (e.Key - Key.A + 1);
            byte value = (byte) (ctrl | 0x80);
            _machine.InjectKey(value);
            e.Handled = true;
            return;
        }

        // Handle arrows and special non-text keys
        byte? ascii = null;
        switch (e.Key)
        {
            case Key.Up:
            {
                // ^K
                ascii = 0x0B;
                break;
            }
            case Key.Down:
            {
                // ^J (LF)
                ascii = 0x0A;
                break;
            }
            case Key.Left:
            {
                // ^H (BS)
                ascii = 0x08;
                break;
            }
            case Key.Right:
            {
                // ^U
                ascii = 0x15;
                break;
            }
            case Key.Back:
            {
                // Treat Backspace as Left (BS). Shift+Backspace => DEL (127)
                if ((e.KeyModifiers & KeyModifiers.Shift) != 0)
                {
                    ascii = 0x7F;
                }
                else
                {
                    ascii = 0x08;
                }
                break;
            }
            case Key.Delete:
            {
                // DEL
                ascii = 0x7F;
                break;
            }
            case Key.Enter:
            {
                ascii = (byte) '\r';
                break;
            }
            case Key.Tab:
            {
                ascii = (byte) '\t';
                break;
            }
            case Key.Escape:
            {
                ascii = 0x1B;
                break;
            }
            default:
            {
                break;
            }
        }

        if (ascii.HasValue)
        {
            byte value = (byte) (ascii.Value | 0x80);
            _machine.InjectKey(value);
            e.Handled = true;
            return;
        }

        // Let TextInput handle printable characters and layout-dependent keys
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        // If releasing Alt or function keys, ensure we don't suppress subsequent text input.
        if ((e.Key == Key.LeftAlt || e.Key == Key.RightAlt) || IsFunctionKey(e.Key))
        {
            _suppressNextTextInput = false;
        }
    }

    private static bool IsFunctionKey(Key key)
    {
        return key >= Key.F1 && key <= Key.F24; // cover extended function keys if present
    }

    public void AttachMachine(VA2M machine)
    {
        _machine = machine;
    }
}

