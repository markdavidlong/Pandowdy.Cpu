namespace Pandowdy.Disassembler;

public readonly struct OpcodeInfo(byte opcode, string mnemonic, byte paramBytes, string template)
{
    public readonly byte Opcode = opcode;
    public readonly string Mnemonic = mnemonic;
    public readonly byte ParamBytes = paramBytes;
    public readonly string Template = template;
}
