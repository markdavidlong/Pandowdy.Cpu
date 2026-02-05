namespace Pandowdy.Disassembler;

public static class OpcodeTable
{
    public static readonly OpcodeInfo[] Table = new OpcodeInfo[256];

    static OpcodeTable()
    {
        // 00–1F (MOS 65C02 + Rockwell)
        // Undefined opcodes use "%undef" and correct ParamBytes.

        Table[0x00] = new OpcodeInfo(0x00, "BRK", 1, "%1");          // BRK pushes PC+2, so 1 param byte
        Table[0x01] = new OpcodeInfo(0x01, "ORA", 1, "(%1,X)");
        Table[0x02] = new OpcodeInfo(0x02, "???", 0, "%undef");      // Undefined (1‑byte NOP)
        Table[0x03] = new OpcodeInfo(0x03, "???", 0, "%undef");      // Undefined (1‑byte NOP)
        Table[0x04] = new OpcodeInfo(0x04, "TSB", 1, "%1");          // 65C02 new
        Table[0x05] = new OpcodeInfo(0x05, "ORA", 1, "%1");
        Table[0x06] = new OpcodeInfo(0x06, "ASL", 1, "%1");
        Table[0x07] = new OpcodeInfo(0x07, "???", 0, "%undef");      // Undefined (1‑byte NOP)

        Table[0x08] = new OpcodeInfo(0x08, "PHP", 0, "");
        Table[0x09] = new OpcodeInfo(0x09, "ORA", 1, "#%1");
        Table[0x0A] = new OpcodeInfo(0x0A, "ASL", 0, "");
        Table[0x0B] = new OpcodeInfo(0x0B, "???", 0, "%undef");      // Undefined (1‑byte NOP)
        Table[0x0C] = new OpcodeInfo(0x0C, "TSB", 2, "%2");          // 65C02 new
        Table[0x0D] = new OpcodeInfo(0x0D, "ORA", 2, "%2");
        Table[0x0E] = new OpcodeInfo(0x0E, "ASL", 2, "%2");
        Table[0x0F] = new OpcodeInfo(0x0F, "???", 0, "%undef");      // Undefined (1‑byte NOP)

        Table[0x10] = new OpcodeInfo(0x10, "BPL", 1, "%branch");
        Table[0x11] = new OpcodeInfo(0x11, "ORA", 1, "(%1),Y");
        Table[0x12] = new OpcodeInfo(0x12, "ORA", 1, "(%1)");        // Rockwell (zp) addressing
        Table[0x13] = new OpcodeInfo(0x13, "???", 0, "%undef");      // Undefined (1‑byte NOP)
        Table[0x14] = new OpcodeInfo(0x14, "TRB", 1, "%1");          // 65C02 new
        Table[0x15] = new OpcodeInfo(0x15, "ORA", 1, "%1,X");
        Table[0x16] = new OpcodeInfo(0x16, "ASL", 1, "%1,X");
        Table[0x17] = new OpcodeInfo(0x17, "???", 0, "%undef");      // Undefined (1‑byte NOP)

        Table[0x18] = new OpcodeInfo(0x18, "CLC", 0, "");
        Table[0x19] = new OpcodeInfo(0x19, "ORA", 2, "%2,Y");
        Table[0x1A] = new OpcodeInfo(0x1A, "INC", 0, "");            // 65C02 new (INC A)
        Table[0x1B] = new OpcodeInfo(0x1B, "???", 0, "%undef");      // Undefined (1‑byte NOP)
        Table[0x1C] = new OpcodeInfo(0x1C, "TRB", 2, "%2");          // 65C02 new
        Table[0x1D] = new OpcodeInfo(0x1D, "ORA", 2, "%2,X");
        Table[0x1E] = new OpcodeInfo(0x1E, "ASL", 2, "%2,X");
        Table[0x1F] = new OpcodeInfo(0x1F, "???", 0, "%undef");      // Undefined (1‑byte NOP)
             
        Table[0x20] = new OpcodeInfo(0x20, "JSR", 2, "%2");
        Table[0x21] = new OpcodeInfo(0x21, "AND", 1, "(%1,X)");
        Table[0x22] = new OpcodeInfo(0x22, "???", 0, "%undef");      // Undefined
        Table[0x23] = new OpcodeInfo(0x23, "???", 0, "%undef");      // Undefined
        Table[0x24] = new OpcodeInfo(0x24, "BIT", 1, "%1");
        Table[0x25] = new OpcodeInfo(0x25, "AND", 1, "%1");
        Table[0x26] = new OpcodeInfo(0x26, "ROL", 1, "%1");
        Table[0x27] = new OpcodeInfo(0x27, "???", 0, "%undef");      // Undefined

        Table[0x28] = new OpcodeInfo(0x28, "PLP", 0, "");
        Table[0x29] = new OpcodeInfo(0x29, "AND", 1, "#%1");
        Table[0x2A] = new OpcodeInfo(0x2A, "ROL", 0, "");
        Table[0x2B] = new OpcodeInfo(0x2B, "???", 0, "%undef");      // Undefined
        Table[0x2C] = new OpcodeInfo(0x2C, "BIT", 2, "%2");
        Table[0x2D] = new OpcodeInfo(0x2D, "AND", 2, "%2");
        Table[0x2E] = new OpcodeInfo(0x2E, "ROL", 2, "%2");
        Table[0x2F] = new OpcodeInfo(0x2F, "???", 0, "%undef");      // Undefined

        Table[0x30] = new OpcodeInfo(0x30, "BMI", 1, "%branch");
        Table[0x31] = new OpcodeInfo(0x31, "AND", 1, "(%1),Y");
        Table[0x32] = new OpcodeInfo(0x32, "AND", 1, "(%1)");        // Rockwell (zp)
        Table[0x33] = new OpcodeInfo(0x33, "???", 0, "%undef");      // Undefined
        Table[0x34] = new OpcodeInfo(0x34, "BIT", 1, "%1,X");        // 65C02 new
        Table[0x35] = new OpcodeInfo(0x35, "AND", 1, "%1,X");
        Table[0x36] = new OpcodeInfo(0x36, "ROL", 1, "%1,X");
        Table[0x37] = new OpcodeInfo(0x37, "???", 0, "%undef");      // Undefined

        Table[0x38] = new OpcodeInfo(0x38, "SEC", 0, "");
        Table[0x39] = new OpcodeInfo(0x39, "AND", 2, "%2,Y");
        Table[0x3A] = new OpcodeInfo(0x3A, "DEC", 0, "");            // 65C02 new (DEC A)
        Table[0x3B] = new OpcodeInfo(0x3B, "???", 0, "%undef");      // Undefined
        Table[0x3C] = new OpcodeInfo(0x3C, "BIT", 2, "%2,X");        // 65C02 new
        Table[0x3D] = new OpcodeInfo(0x3D, "AND", 2, "%2,X");
        Table[0x3E] = new OpcodeInfo(0x3E, "ROL", 2, "%2,X");
        Table[0x3F] = new OpcodeInfo(0x3F, "???", 0, "%undef");      // Undefined

        Table[0x40] = new OpcodeInfo(0x40, "RTI", 0, "");
        Table[0x41] = new OpcodeInfo(0x41, "EOR", 1, "(%1,X)");
        Table[0x42] = new OpcodeInfo(0x42, "???", 0, "%undef");      // Undefined
        Table[0x43] = new OpcodeInfo(0x43, "???", 0, "%undef");      // Undefined
        Table[0x44] = new OpcodeInfo(0x44, "???", 0, "%undef");      // Undefined 
        Table[0x45] = new OpcodeInfo(0x45, "EOR", 1, "%1");
        Table[0x46] = new OpcodeInfo(0x46, "LSR", 1, "%1");
        Table[0x47] = new OpcodeInfo(0x47, "RMB0", 1, "%1");         // Rockwell/WDC bit op

        Table[0x48] = new OpcodeInfo(0x48, "PHA", 0, "");
        Table[0x49] = new OpcodeInfo(0x49, "EOR", 1, "#%1");
        Table[0x4A] = new OpcodeInfo(0x4A, "LSR", 0, "");
        Table[0x4B] = new OpcodeInfo(0x4B, "???", 0, "%undef");      // Undefined
        Table[0x4C] = new OpcodeInfo(0x4C, "JMP", 2, "%2");
        Table[0x4D] = new OpcodeInfo(0x4D, "EOR", 2, "%2");
        Table[0x4E] = new OpcodeInfo(0x4E, "LSR", 2, "%2");
        Table[0x4F] = new OpcodeInfo(0x4F, "BBR0", 2, "%1,%branch"); // Rockwell/WDC bit op

        Table[0x50] = new OpcodeInfo(0x50, "BVC", 1, "%branch");
        Table[0x51] = new OpcodeInfo(0x51, "EOR", 1, "(%1),Y");
        Table[0x52] = new OpcodeInfo(0x52, "EOR", 1, "(%1)");        // Rockwell/WDC (zp)
        Table[0x53] = new OpcodeInfo(0x53, "???", 0, "%undef");      // Undefined
        Table[0x54] = new OpcodeInfo(0x54, "???", 0, "%undef");      // Undefined (NMOS had NOP zp,X)
        Table[0x55] = new OpcodeInfo(0x55, "EOR", 1, "%1,X");
        Table[0x56] = new OpcodeInfo(0x56, "LSR", 1, "%1,X");
        Table[0x57] = new OpcodeInfo(0x57, "RMB1", 1, "%1");         // Rockwell/WDC bit op

        Table[0x58] = new OpcodeInfo(0x58, "CLI", 0, "");
        Table[0x59] = new OpcodeInfo(0x59, "EOR", 2, "%2,Y");
        Table[0x5A] = new OpcodeInfo(0x5A, "PHY", 0, "");            // 65C02 new
        Table[0x5B] = new OpcodeInfo(0x5B, "???", 0, "%undef");      // Undefined
        Table[0x5C] = new OpcodeInfo(0x5C, "???", 0, "%undef");      // Undefined
        Table[0x5D] = new OpcodeInfo(0x5D, "EOR", 2, "%2,X");
        Table[0x5E] = new OpcodeInfo(0x5E, "LSR", 2, "%2,X");
        Table[0x5F] = new OpcodeInfo(0x5F, "BBR1", 2, "%1,%branch"); // Rockwell/WDC bit op

        Table[0x60] = new OpcodeInfo(0x60, "RTS", 0, "");
        Table[0x61] = new OpcodeInfo(0x61, "ADC", 1, "(%1,X)");
        Table[0x62] = new OpcodeInfo(0x62, "???", 0, "%undef");      // Undefined
        Table[0x63] = new OpcodeInfo(0x63, "???", 0, "%undef");      // Undefined
        Table[0x64] = new OpcodeInfo(0x64, "STZ", 1, "%1");          // 65C02 new
        Table[0x65] = new OpcodeInfo(0x65, "ADC", 1, "%1");
        Table[0x66] = new OpcodeInfo(0x66, "ROR", 1, "%1");
        Table[0x67] = new OpcodeInfo(0x67, "RMB2", 1, "%1");         // Rockwell/WDC bit op

        Table[0x68] = new OpcodeInfo(0x68, "PLA", 0, "");
        Table[0x69] = new OpcodeInfo(0x69, "ADC", 1, "#%1");
        Table[0x6A] = new OpcodeInfo(0x6A, "ROR", 0, "");
        Table[0x6B] = new OpcodeInfo(0x6B, "???", 0, "%undef");      // Undefined
        Table[0x6C] = new OpcodeInfo(0x6C, "JMP", 2, "(%2)");        // Indirect
        Table[0x6D] = new OpcodeInfo(0x6D, "ADC", 2, "%2");
        Table[0x6E] = new OpcodeInfo(0x6E, "ROR", 2, "%2");
        Table[0x6F] = new OpcodeInfo(0x6F, "BBR2", 2, "%1,%branch"); // Rockwell/WDC bit op

        Table[0x70] = new OpcodeInfo(0x70, "BVS", 1, "%branch");
        Table[0x71] = new OpcodeInfo(0x71, "ADC", 1, "(%1),Y");
        Table[0x72] = new OpcodeInfo(0x72, "ADC", 1, "(%1)");        // Rockwell/WDC (zp)
        Table[0x73] = new OpcodeInfo(0x73, "???", 0, "%undef");      // Undefined
        Table[0x74] = new OpcodeInfo(0x74, "STZ", 1, "%1,X");        // 65C02 new
        Table[0x75] = new OpcodeInfo(0x75, "ADC", 1, "%1,X");
        Table[0x76] = new OpcodeInfo(0x76, "ROR", 1, "%1,X");
        Table[0x77] = new OpcodeInfo(0x77, "RMB3", 1, "%1");         // Rockwell/WDC bit op

        Table[0x78] = new OpcodeInfo(0x78, "SEI", 0, "");
        Table[0x79] = new OpcodeInfo(0x79, "ADC", 2, "%2,Y");
        Table[0x7A] = new OpcodeInfo(0x7A, "PLY", 0, "");            // 65C02 new
        Table[0x7B] = new OpcodeInfo(0x7B, "???", 0, "%undef");      // Undefined
        Table[0x7C] = new OpcodeInfo(0x7C, "JMP", 2, "(%2,X)");      // Indirect,X (65C02 new)
        Table[0x7D] = new OpcodeInfo(0x7D, "ADC", 2, "%2,X");
        Table[0x7E] = new OpcodeInfo(0x7E, "ROR", 2, "%2,X");
        Table[0x7F] = new OpcodeInfo(0x7F, "BBR3", 2, "%1,%branch"); // Rockwell/WDC bit op

        // 80–9F (MOS 65C02 + Rockwell + WDC superset)

        Table[0x80] = new OpcodeInfo(0x80, "BRA", 1, "%branch");     // 65C02 new
        Table[0x81] = new OpcodeInfo(0x81, "STA", 1, "(%1,X)");
        Table[0x82] = new OpcodeInfo(0x82, "???", 0, "%undef");      // Undefined
        Table[0x83] = new OpcodeInfo(0x83, "???", 0, "%undef");      // Undefined
        Table[0x84] = new OpcodeInfo(0x84, "STY", 1, "%1");
        Table[0x85] = new OpcodeInfo(0x85, "STA", 1, "%1");
        Table[0x86] = new OpcodeInfo(0x86, "STX", 1, "%1");
        Table[0x87] = new OpcodeInfo(0x87, "SMB0", 1, "%1");         // Rockwell/WDC bit op

        Table[0x88] = new OpcodeInfo(0x88, "DEY", 0, "");
        Table[0x89] = new OpcodeInfo(0x89, "BIT", 1, "#%1");         // 65C02 new (BIT immediate)
        Table[0x8A] = new OpcodeInfo(0x8A, "TXA", 0, "");
        Table[0x8B] = new OpcodeInfo(0x8B, "???", 0, "%undef");      // Undefined
        Table[0x8C] = new OpcodeInfo(0x8C, "STY", 2, "%2");
        Table[0x8D] = new OpcodeInfo(0x8D, "STA", 2, "%2");
        Table[0x8E] = new OpcodeInfo(0x8E, "STX", 2, "%2");
        Table[0x8F] = new OpcodeInfo(0x8F, "BBS0", 2, "%1,%branch"); // Rockwell/WDC bit op

        Table[0x90] = new OpcodeInfo(0x90, "BCC", 1, "%branch");
        Table[0x91] = new OpcodeInfo(0x91, "STA", 1, "(%1),Y");
        Table[0x92] = new OpcodeInfo(0x92, "STA", 1, "(%1)");        // Rockwell/WDC (zp)
        Table[0x93] = new OpcodeInfo(0x93, "???", 0, "%undef");      // Undefined
        Table[0x94] = new OpcodeInfo(0x94, "STY", 1, "%1,X");
        Table[0x95] = new OpcodeInfo(0x95, "STA", 1, "%1,X");
        Table[0x96] = new OpcodeInfo(0x96, "STX", 1, "%1,Y");
        Table[0x97] = new OpcodeInfo(0x97, "SMB1", 1, "%1");         // Rockwell/WDC bit op

        Table[0x98] = new OpcodeInfo(0x98, "TYA", 0, "");
        Table[0x99] = new OpcodeInfo(0x99, "STA", 2, "%2,Y");
        Table[0x9A] = new OpcodeInfo(0x9A, "TXS", 0, "");
        Table[0x9B] = new OpcodeInfo(0x9B, "???", 0, "%undef");      // Undefined
        Table[0x9C] = new OpcodeInfo(0x9C, "STZ", 2, "%2");          // 65C02 new
        Table[0x9D] = new OpcodeInfo(0x9D, "STA", 2, "%2,X");
        Table[0x9E] = new OpcodeInfo(0x9E, "STZ", 2, "%2,X");        // 65C02 new
        Table[0x9F] = new OpcodeInfo(0x9F, "BBS1", 2, "%1,%branch"); // Rockwell/WDC bit op

        // A0–BF (MOS 65C02 + Rockwell + WDC superset)

        Table[0xA0] = new OpcodeInfo(0xA0, "LDY", 1, "#%1");
        Table[0xA1] = new OpcodeInfo(0xA1, "LDA", 1, "(%1,X)");
        Table[0xA2] = new OpcodeInfo(0xA2, "LDX", 1, "#%1");
        Table[0xA3] = new OpcodeInfo(0xA3, "???", 0, "%undef");      // Undefined
        Table[0xA4] = new OpcodeInfo(0xA4, "LDY", 1, "%1");
        Table[0xA5] = new OpcodeInfo(0xA5, "LDA", 1, "%1");
        Table[0xA6] = new OpcodeInfo(0xA6, "LDX", 1, "%1");
        Table[0xA7] = new OpcodeInfo(0xA7, "SMB2", 1, "%1");         // Rockwell/WDC bit op

        Table[0xA8] = new OpcodeInfo(0xA8, "TAY", 0, "");
        Table[0xA9] = new OpcodeInfo(0xA9, "LDA", 1, "#%1");
        Table[0xAA] = new OpcodeInfo(0xAA, "TAX", 0, "");
        Table[0xAB] = new OpcodeInfo(0xAB, "???", 0, "%undef");      // Undefined
        Table[0xAC] = new OpcodeInfo(0xAC, "LDY", 2, "%2");
        Table[0xAD] = new OpcodeInfo(0xAD, "LDA", 2, "%2");
        Table[0xAE] = new OpcodeInfo(0xAE, "LDX", 2, "%2");
        Table[0xAF] = new OpcodeInfo(0xAF, "BBS2", 2, "%1,%branch"); // Rockwell/WDC bit op

        Table[0xB0] = new OpcodeInfo(0xB0, "BCS", 1, "%branch");
        Table[0xB1] = new OpcodeInfo(0xB1, "LDA", 1, "(%1),Y");
        Table[0xB2] = new OpcodeInfo(0xB2, "LDA", 1, "(%1)");        // Rockwell/WDC (zp)
        Table[0xB3] = new OpcodeInfo(0xB3, "???", 0, "%undef");      // Undefined
        Table[0xB4] = new OpcodeInfo(0xB4, "LDY", 1, "%1,X");
        Table[0xB5] = new OpcodeInfo(0xB5, "LDA", 1, "%1,X");
        Table[0xB6] = new OpcodeInfo(0xB6, "LDX", 1, "%1,Y");
        Table[0xB7] = new OpcodeInfo(0xB7, "SMB3", 1, "%1");         // Rockwell/WDC bit op

        Table[0xB8] = new OpcodeInfo(0xB8, "CLV", 0, "");
        Table[0xB9] = new OpcodeInfo(0xB9, "LDA", 2, "%2,Y");
        Table[0xBA] = new OpcodeInfo(0xBA, "TSX", 0, "");
        Table[0xBB] = new OpcodeInfo(0xBB, "???", 0, "%undef");      // Undefined
        Table[0xBC] = new OpcodeInfo(0xBC, "LDY", 2, "%2,X");
        Table[0xBD] = new OpcodeInfo(0xBD, "LDA", 2, "%2,X");
        Table[0xBE] = new OpcodeInfo(0xBE, "LDX", 2, "%2,Y");
        Table[0xBF] = new OpcodeInfo(0xBF, "BBS3", 2, "%1,%branch"); // Rockwell/WDC bit op

        // C0–DF (MOS 65C02 + Rockwell + WDC superset)

        Table[0xC0] = new OpcodeInfo(0xC0, "CPY", 1, "#%1");
        Table[0xC1] = new OpcodeInfo(0xC1, "CMP", 1, "(%1,X)");
        Table[0xC2] = new OpcodeInfo(0xC2, "???", 0, "%undef");      // Undefined
        Table[0xC3] = new OpcodeInfo(0xC3, "???", 0, "%undef");      // Undefined
        Table[0xC4] = new OpcodeInfo(0xC4, "CPY", 1, "%1");
        Table[0xC5] = new OpcodeInfo(0xC5, "CMP", 1, "%1");
        Table[0xC6] = new OpcodeInfo(0xC6, "DEC", 1, "%1");
        Table[0xC7] = new OpcodeInfo(0xC7, "SMB4", 1, "%1");         // Rockwell/WDC bit op

        Table[0xC8] = new OpcodeInfo(0xC8, "INY", 0, "");
        Table[0xC9] = new OpcodeInfo(0xC9, "CMP", 1, "#%1");
        Table[0xCA] = new OpcodeInfo(0xCA, "DEX", 0, "");
        Table[0xCB] = new OpcodeInfo(0xCB, "WAI", 0, "");            // WDC addition
        Table[0xCC] = new OpcodeInfo(0xCC, "CPY", 2, "%2");
        Table[0xCD] = new OpcodeInfo(0xCD, "CMP", 2, "%2");
        Table[0xCE] = new OpcodeInfo(0xCE, "DEC", 2, "%2");
        Table[0xCF] = new OpcodeInfo(0xCF, "BBS4", 2, "%1,%branch"); // Rockwell/WDC bit op

        Table[0xD0] = new OpcodeInfo(0xD0, "BNE", 1, "%branch");
        Table[0xD1] = new OpcodeInfo(0xD1, "CMP", 1, "(%1),Y");
        Table[0xD2] = new OpcodeInfo(0xD2, "CMP", 1, "(%1)");        // Rockwell/WDC (zp)
        Table[0xD3] = new OpcodeInfo(0xD3, "???", 0, "%undef");      // Undefined
        Table[0xD4] = new OpcodeInfo(0xD4, "???", 0, "%undef");      // Undefined 
        Table[0xD5] = new OpcodeInfo(0xD5, "CMP", 1, "%1,X");
        Table[0xD6] = new OpcodeInfo(0xD6, "DEC", 1, "%1,X");
        Table[0xD7] = new OpcodeInfo(0xD7, "SMB5", 1, "%1");         // Rockwell/WDC bit op

        Table[0xD8] = new OpcodeInfo(0xD8, "CLD", 0, "");
        Table[0xD9] = new OpcodeInfo(0xD9, "CMP", 2, "%2,Y");
        Table[0xDA] = new OpcodeInfo(0xDA, "PHX", 0, "");            // 65C02 new
        Table[0xDB] = new OpcodeInfo(0xDB, "STP", 0, "");            // WDC addition
        Table[0xDC] = new OpcodeInfo(0xDC, "???", 0, "%undef");      // Undefined 
        Table[0xDD] = new OpcodeInfo(0xDD, "CMP", 2, "%2,X");
        Table[0xDE] = new OpcodeInfo(0xDE, "DEC", 2, "%2,X");
        Table[0xDF] = new OpcodeInfo(0xDF, "BBS5", 2, "%1,%branch"); // Rockwell/WDC bit op

        // E0–FF (MOS 65C02 + Rockwell + WDC superset)

        Table[0xE0] = new OpcodeInfo(0xE0, "CPX", 1, "#%1");
        Table[0xE1] = new OpcodeInfo(0xE1, "SBC", 1, "(%1,X)");
        Table[0xE2] = new OpcodeInfo(0xE2, "???", 0, "%undef");      // Undefined
        Table[0xE3] = new OpcodeInfo(0xE3, "???", 0, "%undef");      // Undefined
        Table[0xE4] = new OpcodeInfo(0xE4, "CPX", 1, "%1");
        Table[0xE5] = new OpcodeInfo(0xE5, "SBC", 1, "%1");
        Table[0xE6] = new OpcodeInfo(0xE6, "INC", 1, "%1");
        Table[0xE7] = new OpcodeInfo(0xE7, "SMB6", 1, "%1");         // Rockwell/WDC bit op

        Table[0xE8] = new OpcodeInfo(0xE8, "INX", 0, "");
        Table[0xE9] = new OpcodeInfo(0xE9, "SBC", 1, "#%1");
        Table[0xEA] = new OpcodeInfo(0xEA, "NOP", 0, "");            // Official NOP
        Table[0xEB] = new OpcodeInfo(0xEB, "???", 0, "%undef");      // Undefined
        Table[0xEC] = new OpcodeInfo(0xEC, "CPX", 2, "%2");
        Table[0xED] = new OpcodeInfo(0xED, "SBC", 2, "%2");
        Table[0xEE] = new OpcodeInfo(0xEE, "INC", 2, "%2");
        Table[0xEF] = new OpcodeInfo(0xEF, "BBS6", 2, "%1,%branch"); // Rockwell/WDC bit op

        Table[0xF0] = new OpcodeInfo(0xF0, "BEQ", 1, "%branch");
        Table[0xF1] = new OpcodeInfo(0xF1, "SBC", 1, "(%1),Y");
        Table[0xF2] = new OpcodeInfo(0xF2, "SBC", 1, "(%1)");        // Rockwell/WDC (zp)
        Table[0xF3] = new OpcodeInfo(0xF3, "???", 0, "%undef");      // Undefined
        Table[0xF4] = new OpcodeInfo(0xF4, "???", 0, "%undef");      // Undefined 
        Table[0xF5] = new OpcodeInfo(0xF5, "SBC", 1, "%1,X");
        Table[0xF6] = new OpcodeInfo(0xF6, "INC", 1, "%1,X");
        Table[0xF7] = new OpcodeInfo(0xF7, "SMB7", 1, "%1");         // Rockwell/WDC bit op

        Table[0xF8] = new OpcodeInfo(0xF8, "SED", 0, "");
        Table[0xF9] = new OpcodeInfo(0xF9, "SBC", 2, "%2,Y");
        Table[0xFA] = new OpcodeInfo(0xFA, "PLX", 0, "");            // 65C02 new
        Table[0xFB] = new OpcodeInfo(0xFB, "???", 0, "%undef");      // Undefined
        Table[0xFC] = new OpcodeInfo(0xFC, "???", 0, "%undef");      // Undefined 
        Table[0xFD] = new OpcodeInfo(0xFD, "SBC", 2, "%2,X");
        Table[0xFE] = new OpcodeInfo(0xFE, "INC", 2, "%2,X");
        Table[0xFF] = new OpcodeInfo(0xFF, "BBS7", 2, "%1,%branch"); // Rockwell/WDC bit op
    }
}
