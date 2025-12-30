using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pandowdy.EmuCore
{
    public partial class VA2M
    {

        private void RenderScreen(BitmapDataArray buf)
        {
            bool text = _sysStatusSink.StateTextMode;
            bool hires = _sysStatusSink.StateHiRes;
            bool mixed = _sysStatusSink.StateMixed;
            bool page2 = _sysStatusSink.StatePage2;
            bool text80col = _sysStatusSink.StateShow80Col;
            bool gr80col = text80col && !_sysStatusSink.StateAnn3_DGR;

            for (int row = 0; row < 24; row++)
            {
                for (int col = 0; col < 40; col++)
                {
                    int addr = GetAddressForXY(col, row, text, hires, mixed, page2);
                    if (addr >= 0x400 && addr <= 0xBFF) // Text/GR Pages 1/2
                    {
                        RenderTextOrGRCell(addr, row, col, text, mixed, text80col, gr80col, buf);
                    }
                    else if (addr >= 0x2000 && addr <= 0x5fff) // HGR Pages 1/2
                    {
                        RenderHiresCell(addr, row, col, gr80col, buf);
                    }
                }
            }
        }

        private void RenderTextOrGRCell(int address, int row, int col, bool text, bool mixed, bool text80, bool gr80, BitmapDataArray buf)
        {
            if (text || (mixed && row >= 20))
            {
                RenderTextCell(address, row, col, text80, buf);
            }
            else
            {
                RenderGrCell(address, row, col, gr80, buf);
            }
        }


        private void RenderHiresCell(int address, int row, int col, bool gr80, BitmapDataArray buf)
        {
            // Render either 7 or 14 pixels, based on the state of gr80

            if (!gr80)
            {
                for (int r = 0; r < 8; r++)
                {
                    ushort byteAddress = (ushort) (address + (r * 0x400));
                    byte value = _memoryPool.Read(byteAddress);
                    int buffY = row * 8 + r;
                    bool prevShift = false;
                    if (col != 0 && (_memoryPool.Read((ushort) (byteAddress - 1)) & 0x80) == 0x80)
                    {
                        prevShift = true;
                    }
                    buf.InsertHgrByteAt(col * 2 * 7, buffY, value, prevShift);
                }
            }

            // if 80-col
            //    iterate through the 8 columns in the cell
            //       look up the aux and main values at that address
            //       write aux/main into buffer unexpanded
        }

        private void RenderTextCell(int address, int row, int col, bool text80, BitmapDataArray buf)
        {
            bool flashOn = _sysStatusSink.StateFlashOn;
            bool altChar = _sysStatusSink.StateAltCharSet;

            byte ch = _memoryPool.Read((ushort) address);
            var glyph = VideoFont.Glyph(ch, flashOn, altChar); // returns span of 8 rows

            if (!text80)
            {

                for (int r = 0; r < 8; r++)  // 8 rows per glyph
                {
                    int buffY = row * 8 + r;
                    byte fontRow = (byte) ~glyph[r]; // invert bits (was glyph ^ 0xff intent)
                                                     //  fontRow = (byte)((y / 8) % 16 * 0x11);

                    buf.Insert7BitLsbAt(col * 2 * 7, buffY, fontRow, true);

                }
            }
            else
            {
                byte ch1 = _memoryPool.ReadRawAux((ushort) address);
                var glyph1 = VideoFont.Glyph(ch1, flashOn, altChar); // returns span of 8 rows

                for (int r = 0; r < 8; r++)  // 8 rows per glyph
                {
                    int y = row * 8 + r;
                    byte fontRow1 = (byte) ~glyph1[r];
                    byte fontRow2 = (byte) ~glyph[r];
                    int baseX = col * 2;
                    {
                        buf.Insert7BitLsbAt(baseX * 7, y, fontRow1, false);
                        buf.Insert7BitLsbAt(baseX * 7 + 7, y, fontRow2, false);
                    }
                }
            }
        }

        private void RenderGrCell(int address, int row, int col, bool gr80, BitmapDataArray buf)
        {
            // Render either 1 40-column or 2 80-column cells depending on the state of the 80-showflag and ann3 (dgr)
            // if 40 colunns
            if (!gr80)
            {
                byte value = _memoryPool.Read((ushort) address);

                for (int glyphRow = 0; glyphRow < 8; glyphRow++)
                {
                    int y = row * 8 + glyphRow;

                    byte grcolor = (byte) (value & 0x0f);
                    if (glyphRow >= 4)
                    {
                        grcolor = (byte) (value >> 4);
                    }

                    var (a1, a2, a3, a4) = MakeGrColor(grcolor);
                    if (col % 2 == 0) // Even -- Use A1 & A2
                    {
                        buf.SetByteAt(col * 14, y, (byte) a1);
                        buf.SetByteAt((col * 14 + 7), y, (byte) a2);
                    }
                    else // Odd -- Use A3 & A4
                    {
                        buf.SetByteAt((col * 14), y, (byte) a3);
                        buf.SetByteAt((col * 14 + 7), y, (byte) a4);
                    }
                }
            }
            // if 80 columns
            //    get the aux and main memory values at the address
            //       determine if we're in the top or bottom nybble of each
            //       get the proper colors for aux and main and their 4 mem values
            //       depending on whether we're even or odd columns draw 0/1 for aux/main values or 2/3 for aux/main into the proper buffer bytes
        }



        private static (byte, byte, byte, byte) MakeGrColor(byte val)
        {
            int x = (val & 0x7f) * 0x11111111;

            byte a = (byte) ((x >> 0) & 0x7f);
            byte b = (byte) ((x >> 3) & 0x7f);
            byte c = (byte) ((x >> 6) & 0x7f);
            byte d = (byte) ((x >> 9) & 0x7f);

            return (a, b, c, d);
        }



        // this is using 0-based X/Y
        private static int GetAddressForXY(int x, int y, bool text, bool hires, bool mixed, bool page2, int cellRowOffset = 0)
        {
            const int TextPage1Start = 0x0400;
            const int TextPage2Start = 0x0800;
            const int HiresPage1Start = 0x2000;
            const int HiresPage2Start = 0x4000;

            int retval = -1;

            //Todo: Note:  Page2 might have issues if 80-column mode is also on.  Revisit that later.

            if (x >= 0 && x < 40 && y >= 0 && y < 24)
            {

                if (text || (!text && !hires) || (mixed && y > 20))
                {
                    int startAddr = page2 ? TextPage2Start : TextPage1Start;

                    // Every 128 bytes is 40 columns at row x, then 40 columns at row x+8, then 40 columns at row x+16.  So row 0 starts at StartAddr, row 1 = startAddress+128, row 2 = startAddress+256, etc. This is normalized to (row % 8) * 128 to start with, then if row >= 8, add 40, if row >= 16 add another 40.
                    retval = startAddr + (y % 8) * 128 + (y / 8) * 40 + x;
                }
                else // We're either HiRes full screen or HiRes Mixed with y <= 20
                {
                    int startAddr = page2 ? HiresPage2Start : HiresPage1Start;

                    retval = startAddr + (y % 8) * 128 + (y / 8) * 40 + (cellRowOffset * 0x400) + x;
                }
            }
            return retval;
        }

    }
}
