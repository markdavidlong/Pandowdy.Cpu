using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.Input;
using Pandowdy.Core;

namespace Pandowdy.UI;

/// <summary>
/// Apple II text screen control rendering from emulator frame provider at fixed 60Hz.
/// Legacy per-character incremental rendering removed.
/// </summary>
public class Apple2Display : Control
{
    public static readonly StyledProperty<Bitmap?> BitmapProperty =
        AvaloniaProperty.Register<Apple2Display, Bitmap?>(nameof(Bitmap), null);
    public static readonly StyledProperty<bool> Use80ColsProperty =
        AvaloniaProperty.Register<Apple2Display, bool>(nameof(Use80Cols), false);

    private bool _suppressNextTextInput;
    private VA2M? _machine;

    private IFrameProvider? _frameProvider;
    private byte[]? _lastFrame;
    private DispatcherTimer? _refreshTimer; // ~60Hz UI redraw cadence

    private ISystemStatusProvider? _status;
    private bool _flagText, _flagMixed, _flagHiRes, _flagPage2;

    public Bitmap? Bitmap       
    {
        get => GetValue(BitmapProperty);
        set => SetValue(BitmapProperty, value);
    }

    public bool Use80Cols
    {
        get => GetValue(Use80ColsProperty);
        set => SetValue(Use80ColsProperty, value);
    }

    private const double SourceWidth = 561;
    private const double SourceHeight = 384;
    private const double SourceAspect = SourceWidth / SourceHeight;

    public Apple2Display()
    {
        Bitmap = CreateCheckerboardBitmap;
        Focusable = true;
        GotFocus += (_, __) => { InvalidateVisual(); };
        LostFocus += (_, __) => { InvalidateVisual(); };
        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;
        TextInput += OnTextInput;

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000.0 / 60.0) };
        _refreshTimer.Tick += (_, __) =>
        {
            if (_frameProvider != null && _lastFrame != null)
            {
                InvalidateVisual();
            }
        };
        _refreshTimer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _refreshTimer?.Stop();
        _refreshTimer = null;
        if (_frameProvider != null)
        {
            _frameProvider.FrameAvailable -= OnFrameAvailable;
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
        _lastFrame = _frameProvider?.GetFrame(); // redraw deferred to timer
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (double.IsInfinity(availableSize.Width) || double.IsInfinity(availableSize.Height))
        {
            return new Size(700, 525); // maintain aspect ratio baseline
        }
        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize) => finalSize;

    private static Bitmap CreateCheckerboardBitmap
    {
        get
        {
            const int numRows = 24;
            const int numCols = 80;
            const int blockWidth = 7;
            const int blockHeight = 16;
            const int bitmapWidth = 561;
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
                    byte colorValue = isWhite ? (byte)80 : (byte)64;
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
                        if (outYTop + 1 >= 384) { break; }
                        int lineOffset = y * 80;
                        RenderMonochromeLine(dst, stridePixels, outYTop, _lastFrame, lineOffset);
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

    static private unsafe void RenderMonochromeLine(byte* dst, int stridePixels, int outYTop, byte[] frame, int lineOffset)
    {
        for (int xByte = 0; xByte < 80; xByte++)
        {
            byte bits = frame[lineOffset + xByte];
            for (int bit = 0; bit < 7; bit++)
            {
                bool on = (bits & (1 << bit)) == 0; // inverted Apple II bit convention
                int outX = xByte * 7 + bit;
                if (outX >= 561)
                {
                    break;
                }

                uint color = on ? 0xFFFFFFFFu : 0xFF000000u;
                WritePixel(dst, stridePixels, outX, outYTop, color);
                WritePixel(dst, stridePixels, outX, outYTop + 1, color);
            }
        }
    }

    private void EnsureBitmapForFrame()
    {
        if (Bitmap is WriteableBitmap)
        {
            return;
        }
        Bitmap = new WriteableBitmap(new PixelSize(561, 384), new Vector(96, 96), PixelFormat.Bgra8888);
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

    private static bool IsFunctionKey(Key key) => key >= Key.F1 && key <= Key.F24;

    public void AttachMachine(VA2M machine) => _machine = machine;

    private bool GetCapsLockEnabled()
    {
        var tl = TopLevel.GetTopLevel(this);
        if (tl is MainWindow mw)
        {
            return mw.IsCapsLockEnabledForInput;
        }
        return false;
    }

    private bool IsCapsLockEnabled => GetCapsLockEnabled();

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
        foreach (char ch in e.Text)
        {
            char c = ch == '\n' ? '\r' : ch;
            if (IsCapsLockEnabled && c >= 'a' && c <= 'z')
            {
                c = (char)(c - 32);
            }
            if (c <= 0x7F)
            {
                _machine.InjectKey((byte)((byte)c | 0x80));
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
        if ((e.KeyModifiers & KeyModifiers.Alt) != 0 || IsFunctionKey(e.Key))
        {
            _suppressNextTextInput = true;
            return;
        }
        if ((e.KeyModifiers & KeyModifiers.Control) != 0 && e.Key >= Key.A && e.Key <= Key.Z)
        {
            byte ctrl = (byte)(e.Key - Key.A + 1);
            _machine.InjectKey((byte)(ctrl | 0x80));
            e.Handled = true;
            return;
        }
        byte? ascii = e.Key switch
        {
            Key.Up => (byte)0x0B,
            Key.Down => (byte)0x0A,
            Key.Left => (byte)0x08,
            Key.Right => (byte)0x15,
            Key.Delete => (byte)0x7F,
            Key.Enter => (byte)'\r',
            Key.Tab => (byte)'\t',
            Key.Escape => (byte)0x1B,
            Key.Back => (e.KeyModifiers & KeyModifiers.Shift) != 0 ? (byte)0x7F : (byte)0x08,
            _ => null
        };
        if (ascii.HasValue)
        {
            _machine.InjectKey((byte)(ascii.Value | 0x80));
            e.Handled = true;
        }
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if ((e.Key == Key.LeftAlt || e.Key == Key.RightAlt) || IsFunctionKey(e.Key))
        {
            _suppressNextTextInput = false;
        }
    }
}

