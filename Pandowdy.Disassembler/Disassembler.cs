namespace Pandowdy.Disassembler;

public static class Disassembler
{
    public static string FormatLine(
        OpcodeInfo info,
        ushort pc,
        byte p1,
        byte p2)
    {
        string t = info.Template;

        // Branches
        if (t == "%branch")
        {
            sbyte offset = unchecked((sbyte) p1);
            ushort dest = (ushort) (pc + 2 + offset);
            return $"{info.Mnemonic,-4} ${dest:X4}";
        }

        // General template replacement
        string result = t;

        if (info.ParamBytes >= 1)
        {
            result = result.Replace("%1", $"${p1:X2}");
        }

        if (info.ParamBytes == 2)
        {
            ushort addr = (ushort) (p1 | (p2 << 8));
            result = result.Replace("%2", $"${addr:X4}");
        }

        return $"{info.Mnemonic,-4} {result}".TrimEnd();
    }
}
