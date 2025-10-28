using System.Text;

namespace Pandowdy.Core;

public static class VA2MMemoryDumping
{
    public static void DumpPage(this VA2MMemory mem, int pageNumber, StringBuilder sb, int width = 1, bool writeHeader = true, bool writeFooter = true)
    {
        const int bytesPerLine = 16;
        int startingAddress = pageNumber * 0x100;
        int columns = bytesPerLine * width;
        int rows = 0x100 / columns;
        if (writeHeader)
        {
            sb.Append(" ");
            AppendHeader(sb, width);
            sb.AppendLine();
            sb.AppendLine($" +{new string('-', 48 * width + 1)}+");
        }
        for (int row = 0; row < rows; row++)
        {
            int address = startingAddress + row * columns;
            sb.Append($" {address:X4} |");
            for (int col = 0; col < columns; col++)
            {
                sb.Append($" {mem[(ushort) (address + col)]:X2}");
            }
            sb.Append(" |");
            sb.AppendLine();
        }
        if (writeFooter)
        {
            sb.AppendLine($" +{new string('-', 48 * width + 1)}+");
        }
    }

    public static void DumpPages(this VA2MMemory mem, Range range, StringBuilder sb, int width = 1)
    {
        int pageCount = mem.Size / 0xFF;
        (int offset, int length) = range.GetOffsetAndLength(pageCount);
        for (int pageNumber = offset; pageNumber < offset + length; pageNumber++)
        {
            mem.DumpPage(pageNumber, sb, width, pageNumber == offset, pageNumber == offset + length - 1);
        }
    }

    private static void AppendHeader(StringBuilder sb, int width)
    {
        for (int i = 0; i < width * 16; i++)
        {
            sb.Append($"{i.ToString("X").PadRight(3)}");
        }
    }
}
