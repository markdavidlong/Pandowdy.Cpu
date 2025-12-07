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

    private bool _suppressNextTextInput;
    private VA2M? _machine;

    // RGBA color constants
    private const UInt32 BlackColor = 0xFF000000;
    private const UInt32 MagentaColor = 0xFF930B7C;
    private const UInt32 DarkBlueColor = 0xFF1F35D3;
    private const UInt32 PurpleColor = 0xFFBB36FF;
    private const UInt32 DarkGreenColor = 0xFF00760C;
    private const UInt32 GrayColor = 0xFF7E7E7E;
    private const UInt32 BlueColor = 0xFF07A5FF;
    private const UInt32 LightBlueColor = 0xFF6AB6FF;
    private const UInt32 BrownColor = 0xFF7B3F00;
    private const UInt32 OrangeColor = 0xFFFF6A00;
    private const UInt32 Gray2Color = 0xFF7E7E7E;
    private const UInt32 PinkColor = 0xFFFF9ACD;
    private const UInt32 GreenColor = 0xFF00FF00;
    private const UInt32 YellowColor = 0xFFFFFF00;
    private const UInt32 AquaColor = 0xFF00FFFF;
    private const UInt32 WhiteColor = 0xFFFFFFFF;

    //private const UInt32 BlackColor = 0xFF000000;
    //private const UInt32 MagentaColor = 0xFFE31E60;
    //private const UInt32 DarkBlueColor = 0xFF604EBD;
    //private const UInt32 PurpleColor = 0xFF442FD0;
    //private const UInt32 DarkGreenColor = 0xFF00A360;
    //private const UInt32 GrayColor = 0xFF9C9C9C;
    //private const UInt32 BlueColor = 0xFF14CFD0;
    //private const UInt32 LightBlueColor = 0xFFD0C3FF;
    //private const UInt32 BrownColor = 0xFF607203;
    //private const UInt32 OrangeColor = 0xFFFF6A3C;
    //private const UInt32 Gray2Color = 0xFF9C9C9C;
    //private const UInt32 PinkColor = 0xFFFFA0D0;
    //private const UInt32 GreenColor = 0xFF14F53C;
    //private const UInt32 YellowColor = 0xFFD0DD8D;
    //private const UInt32 AquaColor = 0xFF72FFD0;
    //private const UInt32 WhiteColor = 0xFFFFFFFF;


    private IFrameProvider? _frameProvider;
    //RTH private byte[]? _lastFrame;
    private BitmapDataArray? _lastFrame;
    // Refresh cadence driven externally (MainWindow ticker)

  //  private ISystemStatusProvider? _status;
    //private bool _flagText, _flagMixed, _flagHiRes, _flagPage2;

    public bool ShowScanLines { get; set; } = true;

    public Bitmap? Bitmap
    {
        get => GetValue(BitmapProperty);
        set => SetValue(BitmapProperty, value);
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
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
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
                    byte* dst = (byte*) fb.Address;
                    int stridePixels = 561;
                    for (int y = 0; y < _frameProvider.Height; y++)
                    {
                        int outYTop = y * 2;
                        if (outYTop + 1 >= 384)
                        { break; }
                        if (_frameProvider.IsGraphics)
                        {
                            RenderNtscLine(dst, stridePixels, outYTop, _lastFrame.GetPixelSpan(0, y, BitmapDataArray.Width), ShowScanLines);
                        }
                        else
                        {
                            RenderMonochromeLine(dst, stridePixels, outYTop, _lastFrame.GetPixelSpan(0, y, BitmapDataArray.Width), ShowScanLines);
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

    public void RequestRefresh()
    {
        // Ensure we have a frame to render before invalidating
        if (_frameProvider != null && _lastFrame != null)
        {
            InvalidateVisual();
        }
    }

    static private unsafe void RenderMonochromeLine(byte* dst, int stridePixels, int outYTop, ReadOnlySpan<bool> lineData, bool showScanLines)
    {
        //for (int xByte = 0; xByte < 80; xByte++)
        for (int xPos = 0; xPos < lineData.Length; xPos++)
        {
            bool on = lineData[xPos]; // simplified for full byte
            if (xPos <= stridePixels)
            {
                uint color = on ? 0xFFFFFFFFu : 0xFF000000u;
                uint dimColor = showScanLines ? (((color & 0x00fcfcfc) >> 2) | 0xff000000) : color; // 3/4 brightness when enabled
                WritePixel(dst, stridePixels, xPos, outYTop, color);
                WritePixel(dst, stridePixels, xPos, outYTop + 1, dimColor);
            }
        }
    }


    /*

void HiresScreenWidget::drawNtscLine(QPainter &painter, int lineNum, const QBitArray& data) const {
QList<QColor> colors;
colors.resize(data.size() + 3);

for (int idx = 0; idx < data.size(); idx++) {
    QBitArray tmp(4);
    tmp[0]=data.at(idx+0);
    if (idx < data.size()-1) tmp[1]=data.at(idx+1); else tmp[1] = false;
    if (idx < data.size()-2) tmp[2]=data.at(idx+2); else tmp[2] = false;
    if (idx < data.size()-3) tmp[3]=data.at(idx+3); else tmp[3] = false;
    const auto color = getColorFromBits(tmp, idx % 4);
    colors[idx]   = color;
    colors[idx + 1] = color;
    colors[idx + 2] = color;
    colors[idx + 3] = color;
}

for (int idx = 0; idx < colors.size(); idx++)
{
    painter.setPen(colors.at(idx));
    painter.setBrush(colors.at(idx));
    painter.drawPoint(idx,lineNum);
}
}
*/
    static private unsafe void RenderNtscLine(byte* dst, int stridePixels, int outYTop, ReadOnlySpan<bool> lineData, bool showScanLines)
    {
        for (int xPos = 0; xPos < lineData.Length; xPos++)
        {
            var bits = new bool[4];
            bits[0] = lineData[xPos + 0];
            bits[1] = (xPos + 1 < lineData.Length) && lineData[xPos + 1];
            bits[2] = (xPos + 2 < lineData.Length) && lineData[xPos + 2];
            bits[3] = (xPos + 3 < lineData.Length) && lineData[xPos + 3];
            var phase = (byte)(xPos % 4);

            if (xPos <= stridePixels)
            {

                byte bitval = (byte) ((bits[0] ? 0x08 : 0x00) +
                        (bits[1] ? 0x04 : 0x00) +
                        (bits[2] ? 0x02 : 0x00) +
                        (bits[3] ? 0x01 : 0x00));

                uint color = GetNTSCColorFromBits(bitval, phase); //on ? 0xFFFF0000u : 0xFF000000u;
                WritePixel(dst, stridePixels, xPos, outYTop, color);
                uint dimColor = showScanLines ? (((color & 0x00fcfcfc) >> 2) | 0xff000000) : color; // 3/4 brightness when enabled

                WritePixel(dst, stridePixels, xPos, outYTop + 1, dimColor);
            }
        }
    }

    static UInt32 GetNTSCColorFromBits(byte bitval, byte phase)
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
        basePtr[idx + 0] = (byte) (rgba & 0xFF);
        basePtr[idx + 1] = (byte) ((rgba >> 8) & 0xFF);
        basePtr[idx + 2] = (byte) ((rgba >> 16) & 0xFF);
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
                c = (char) (c - 32);
            }
            if (c <= 0x7F)
            {
                _machine.InjectKey((byte) ((byte) c | 0x80));
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
            byte ctrl = (byte) (e.Key - Key.A + 1);
            _machine.InjectKey((byte) (ctrl | 0x80));
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
            _machine.InjectKey((byte) (ascii.Value | 0x80));
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

