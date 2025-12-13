using System;
using System.Diagnostics;

namespace Pandowdy.Core
{
    public class BitmapDataArray
    {
        private const int lines = 192; // logical scanlines
        private const int logicalPixels = 560; // visible pixel width
        private const int rowBytes = (logicalPixels + 7) >> 3; // packed bytes per row
        private const int stridePixels = rowBytes * 8 + 3; // capacity (allows small overscan past logicalPixels)
        private readonly byte[] data = new byte[lines * rowBytes];

        static BitmapDataArray()
        {
            Debug.Assert(rowBytes == ((logicalPixels + 7) >> 3), "rowBytes round-up calculation mismatch");
            Debug.Assert(stridePixels >= logicalPixels, "Stride capacity must cover logical pixel width");
        }

        public void Clear()
        {
            Array.Clear(data, 0, data.Length);
        }

        public void SetPixel(int x, int y)
        {
            if (x < 0 || x >= stridePixels)
            {
              //  Debug.Assert(false, $"SetPixel: x {x} outside capacity 0..{stridePixels - 1}");
                return;
            }
            if (y < 0 || y >= lines)
            {
         //       Debug.Assert(false, $"SetPixel: y {y} outside capacity 0..{lines - 1}");
                return;
            }
            if (x >= logicalPixels)
            {
      //          Debug.Assert(false, $"SetPixel: x {x} in overscan region >= {logicalPixels}");
                return;
            }
            int index = y * rowBytes + (x >> 3);
            byte mask = (byte)(0x80 >> (x & 7));
            data[index] |= mask;
        }

        public void SetDoublePixel(int x, int y)
        {
            if (x < 0 || x >= stridePixels / 2)
            {
         //       Debug.Assert(false, $"SetDoublePixel: x {x} outside capacity 0..{(stridePixels / 2) - 1}");
                return;
            }
            if (y < 0 || y >= lines)
            {
          //      Debug.Assert(false, $"SetDoublePixel: y {y} outside capacity 0..{lines - 1}");
                return;
            }
            int px = x; // doubled space origin
            SetPixel(px, y);
            SetPixel(px + 1, y);
        }

        public void SetByteAt(int x, int y, byte value)
        {
            if (x < 0 || x >= stridePixels - 7)
            {
         //       Debug.Assert(false, $"SetByteAt: x {x} outside capacity 0..{stridePixels - 8}");
        //        return;
            }
            if (y < 0 || y >= lines)
            {
          //      Debug.Assert(false, $"SetByteAt: y {y} outside capacity 0..{lines - 1}");
                return;
            }
            int px = x;
            for (int bit = 0; bit < 8; bit++)
            {
                bool on = (value & (1 << (bit))) != 0;
                int p = px + (bit);
                if (on)
                {
                    SetPixel(p, y);
                }
                else
                {
                    ClearPixel(p, y);
                }
            }
        }


        public void Insert7BitLsbAt(int x, int y, byte value, bool expand = false)
        {
            if (x < 0 || x >= stridePixels)
            {
         //       Debug.Assert(false, $"Insert7BitLsbAt: x {x} outside capacity 0..{stridePixels - 1}");
                return;
            }
            if (y < 0 || y >= lines)
            {
          //      Debug.Assert(false, $"Insert7BitLsbAt: y {y} outside capacity 0..{lines - 1}");
                return;
            }
            int px = x;
            for (int bit = 0; bit < 8; bit++)
            {
                bool on = (value & (1 << bit)) != 0;
                if (expand)
                {
                    int p0 = px + (bit * 2);
                    int p1 = p0 + 1;
                    if (on)
                    {
                        SetPixel(p0, y);
                        SetPixel(p1, y);
                    }
                    else
                    {
                        ClearPixel(p0, y);
                        ClearPixel(p1, y);
                    }
                }
                else
                {
                    int p = px + bit;
                    if (on)
                    {
                        SetPixel(p, y);
                    }
                    else
                    {
                        ClearPixel(p, y);
                    }
                }
            }
        }

        public void ClearDoublePixel(int x, int y)
        {
            if (x < 0 || x >= stridePixels / 2)
            {
         //       Debug.Assert(false, $"ClearDoublePixel: x {x} outside capacity 0..{(stridePixels / 2) - 1}");
                return;
            }
            if (y < 0 || y >= lines)
            {
          //      Debug.Assert(false, $"ClearDoublePixel: y {y} outside capacity 0..{lines - 1}");
                return;
            }
            int px = x;
            ClearPixel(px, y);
            ClearPixel(px + 1, y);
        }

        public void ClearPixel(int x, int y)
        {
            if (x < 0 || x >= stridePixels)
            {
    //            Debug.Assert(false, $"ClearPixel: x {x} outside capacity 0..{stridePixels - 1}");
                return;
            }
            if (y < 0 || y >= lines)
            {
   //             Debug.Assert(false, $"ClearPixel: y {y} outside capacity 0..{lines - 1}");
                return;
            }
            if (x >= logicalPixels)
            {
    //            Debug.Assert(false, $"ClearPixel: x {x} in overscan region >= {logicalPixels}");
                return;
            }
            int index = y * rowBytes + (x >> 3);
            byte mask = (byte)(0x80 >> (x & 7));
            data[index] &= (byte)~mask;
        }

        public bool GetPixel(int x, int y)
        {
            if (x < 0 || x >= stridePixels)
            {
                return false;
   //             throw new ArgumentOutOfRangeException(nameof(x), $"x must be between 0 and {stridePixels - 1} inclusive.");
            }
            if (y < 0 || y >= lines)
            {
                return false;
              //  throw new ArgumentOutOfRangeException(nameof(y), $"y must be between 0 and {lines - 1} inclusive.");
            }
            if (x >= logicalPixels)
            {
                return false;
            }
            int index = y * rowBytes + (x >> 3);
            byte mask = (byte)(0x80 >> (x & 7));
            return (data[index] & mask) != 0;
        }

        public Span<bool> GetPixelSpan(int x, int y, int length)
        {
            if (x < 0 || x + length > stridePixels)
            {
                throw new ArgumentOutOfRangeException(nameof(x), $"Range must fit within 0..{stridePixels - 1}.");
            }
            if (y < 0 || y >= lines)
            {
                throw new ArgumentOutOfRangeException(nameof(y), $"y must be between 0 and {lines - 1} inclusive.");
            }
            Span<bool> span = new bool[length];
            for (int i = 0; i < length; i++)
            {
                span[i] = GetPixel(x + i, y);
            }
            return span;
        }

        public ReadOnlySpan<byte> GetRowDataSpan(int row)
        {
            if (row < 0 || row >= lines)
            {
                throw new ArgumentOutOfRangeException(nameof(row), $"row must be between 0 and {lines - 1} inclusive.");
            }
            return new ReadOnlySpan<byte>(data, row * rowBytes, rowBytes);
        }

        static public int Width => logicalPixels;
        static public int CapacityWidth => stridePixels;
        static public int Height => lines;
        static public int RowByteCount => rowBytes;
    }
}
