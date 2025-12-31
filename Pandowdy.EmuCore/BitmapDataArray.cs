using System;
using System.Diagnostics;
using System.Reflection;

namespace Pandowdy.EmuCore
{
    public class BitmapDataArray
    {
        private const int _height = 192; // logical scanlines
        private const int _width = 560; // visible pixel width

        private readonly BitField16[] _data;

        static public int Width => _width;
        static public int Height => _height;


        public BitmapDataArray()
        {
            _data = new BitField16[_height * _width];
        }

        public void Clear()
        {
            Array.Clear(_data, 0, _data.Length);
        }

        private static int CalcOffset(int x, int y)
        {
            return x + y * _width;
        }

        private static void CheckXY(int x, int y)
        {
            if (x < 0 || x >= _width)
            {
                throw new ArgumentOutOfRangeException(nameof(x), $"X value {x} is outside the width range of 0-{_width - 1}.");
            }
            if (y < 0 || y >= _height)
            {
                throw new ArgumentOutOfRangeException(nameof(x), $"Y value {y} is outside the height range of 0-{_height - 1}.");
            }
        }
        private static void CheckRow(int row)
        {
            if (row < 0 || row >= _height)
            {
                throw new ArgumentOutOfRangeException(nameof(row), $"Row value {row} is outside the height range of 0-{_height - 1}.");
            }
        }

        public void SetPixel(int x, int y, int bitplane)
        {
            CheckXY(x, y);
            _data[CalcOffset(x, y)].SetBit(bitplane, true);
        }

        public void ClearPixel(int x, int y, int bitplane)
        {
            CheckXY(x, y);
            _data[CalcOffset(x, y)].SetBit(bitplane, false);
        }

        public bool GetPixel(int x, int y, int bitplane)
        {
            CheckXY(x, y);
            return _data[CalcOffset(x, y)].GetBit(bitplane);
        }


        // TODO: This should be moved to the GR renderer
        //public void SetByteAt(int x, int y, byte value, int bitplane)
        //{

        //    int px = x;
        //    for (int bit = 0; bit < 8; bit++)
        //    {
        //        bool on = (value & (1 << (bit))) != 0;
        //        int p = px + (bit);
        //        CheckXY(p, y);
        //        if (on)
        //        {
        //            SetPixel(p, y,bitplane);
        //        }
        //        else
        //        {
        //            ClearPixel(p, y, bitplane);
        //        }
        //    }
        //}




        //TODO: This should be moved to the HGR renderer
        //public void InsertHgrByteAt(int x, int y, byte value, bool prevShift, int bitplane)
        //{
        //    CheckXY(x, y);
        //    int px = x;


        //    bool shift = (value & 0x80) == 0x80;


        //    for (int bit = 0; bit < 7; bit++)
        //    {
        //        bool on = (value & (1 << bit)) != 0;

        //        int p0 = px + (bit * 2) + (shift ? 1 : 0);
        //        int p1 = p0 + 1;
        //        if (on)
        //        {
        //            SetPixel(p0, y);
        //            SetPixel(p1, y);
        //            SetPixel(p1 + 1, y);
        //        }
        //        else
        //        {
        //            if (bit > 0 || (prevShift == shift))
        //            {
        //                ClearPixel(p0, y);
        //            }
        //            ClearPixel(p1, y);
        //        }
        //    }
        //}


        //TODO: This should be moved to the text renderer
        //public void Insert7BitLsbAt(int x, int y, byte value, bool expand = false)
        //{
        //    CheckXY(x, y);

        //    int px = x;
        //    for (int bit = 0; bit < 8; bit++)
        //    {
        //        bool on = (value & (1 << bit)) != 0;
        //        if (expand)
        //        {
        //            int p0 = px + (bit * 2);
        //            int p1 = p0 + 1;


        //            if (on)
        //            {
        //                SetPixel(p0, y);
        //                SetPixel(p1, y);
        //            }
        //            else
        //            {
        //                ClearPixel(p0, y);
        //                ClearPixel(p1, y);
        //            }
        //        }
        //        else
        //        {
        //            int p = px + bit;
        //            if (on)
        //            {
        //                SetPixel(p, y);
        //            }
        //            else
        //            {
        //                ClearPixel(p, y);
        //            }
        //        }
        //    }
        //}






        //public Span<BitField16> GeBitplaneSpan(int x, int y, int length)
        //{

        //    Span<bool> span = new bool[length];
        //    for (int i = 0; i < length; i++)
        //    {
        //        span[i] = GetPixel(x + i, y);
        //    }
        //    return span;
        //}

        public ReadOnlySpan<bool> GetBitplaneSpanForRow(int row, int bitplane)
        {
            CheckRow(row);
            var raw = GetRowDataSpan(row);
            bool[] pixels = new bool[raw.Length];
            int offset = 0;
            
            foreach (var val in raw)
            {
                pixels[offset++] = val.GetBit(bitplane);
            }
            return new ReadOnlySpan<bool>(pixels);

        }

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
