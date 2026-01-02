using System.Diagnostics;

namespace Pandowdy.EmuCore;

/// <summary>
/// Holds AppleSoft ROM hook actions for high-level BASIC debugging.
/// Each hook has an intrinsic verbosity level; invocation supplies a targetLevel.
/// </summary>
public sealed class AppleSoftHookTable
{
    private readonly Action<int,int,int>?[] _table = new Action<int,int,int>?[65536];
    public Action<int,int,int>? Get(ushort address) => _table[address];
    public void Set(ushort address, int level, string label) => _table[address] = Make(level, label, address);
    public bool IsEmpty => _populatedCount == 0;
    private int _populatedCount;

    private static Action<int, int,int> Make(int level, string label, ushort addr) => (targetLevel, lineNum, spaces) =>
    {
        if (level <= targetLevel)
        {
            if (lineNum < 0xFA00)
            {
                Debug.Write($"{lineNum,-5} ");
            }
            else
            {
                Debug.Write("IMM".PadRight(5));
            }
            for (int x = 0; x < spaces; x++)
            {
                Debug.Write(' ');
            }
            Debug.WriteLine($"{label} at {addr:X4}");
        }
    };

    /// <summary>
    /// Initialize the default minimal set of statement-level hooks.
    /// Safe to call multiple times; duplicates ignored.
    /// </summary>
    public void InitializeDefault()
    {
        if (!IsEmpty)
        {
            return;
        }

        Add(1, 0xD870, "END");
        Add(1, 0xD766, "FOR");
        Add(1, 0xDCF9, "NEXT");
        Add(1, 0xD995, "DATA");
        Add(1, 0xDBB2, "INPUT");
        Add(1, 0xF331, "DEL");
        Add(1, 0xDFD9, "DIM");
        Add(1, 0xDBE2, "READ");
        Add(1, 0xF390, "GR");
        Add(1, 0xF399, "TEXT");
        Add(1, 0xF1E5, "PR#");
        Add(1, 0xF1DE, "IN#");
        Add(1, 0xF1D5, "CALL");
        Add(1, 0xF225, "PLOT");
        Add(1, 0xF232, "HLIN");
        Add(1, 0xF241, "VLIN");
        Add(1, 0xF3D8, "HGR2");
        Add(1, 0xF3E2, "HGR");
        Add(1, 0xF6E9, "HCOLOR");
        Add(1, 0xF6FE, "HPLOT");
        Add(1, 0xF769, "DRAW");
        Add(1, 0xF76F, "XDRAW");
        Add(1, 0xF7E7, "HTAB");
        Add(1, 0xFC58, "HOME");
        Add(1, 0xF721, "ROT=");
        Add(1, 0xF727, "SCALE=");
        Add(1, 0xF775, "SHLOAD");
        Add(1, 0xF26D, "SETTRACE");
        Add(1, 0xF26F, "TRACEOFF");
        Add(1, 0xF273, "SETNORM");
        Add(1, 0xF277, "INVERSE");
        Add(1, 0xF280, "FLASH");
        Add(1, 0xF24F, "COLOR");
        Add(1, 0xD96B, "RETURN (POP)");
        Add(1, 0xF256, "VTAB");
        Add(1, 0xF286, "HIMEMSET");
        Add(1, 0xF2A6, "LOMEMSET");
        Add(1, 0xF2CB, "ONERR");
        Add(1, 0xF318, "RESUME");
        Add(1, 0xF3BC, "RECALL");
        Add(1, 0xF39F, "STORE");
        Add(1, 0xF262, "SPEED");
        Add(1, 0xDA46, "LET");
        Add(1, 0xD93E, "GOTO");
        Add(1, 0xD912, "RUN");
        Add(1, 0xD9C9, "IF");
        Add(1, 0xD849, "RESTORE");
        Add(1, 0x03F5, "& VECTOR");
        Add(1, 0xD921, "GOSUB");
        Add(1, 0xD9DC, "REM");
        Add(1, 0xD86E, "STOP");
        Add(1, 0xD9EC, "ONGOTO");
        Add(1, 0xE784, "WAIT");
        Add(1, 0xD8B0, "SAVE");
        Add(1, 0xD8C9, "LOAD");
        Add(1, 0xE313, "DEF");
        Add(1, 0xE77B, "POKE");
        Add(1, 0xDAD5, "PRINT");
        Add(1, 0xD896, "CONT");
        Add(1, 0xD6A5, "LIST");
        Add(1, 0xD66A, "CLEAR");
        Add(1, 0xDBA0, "GET");
        Add(1, 0xD649, "NEW");
        Add(1, 0xEB90, "SGN");
        Add(1, 0xEC23, "INT");
        Add(1, 0xEBAF, "ABS");
        Add(1, 0x000A, "USRVEC");
        Add(1, 0xE2DE, "FRE");
        Add(1, 0xDEF9, "SCRN(");
        Add(1, 0xDFCD, "PDL");
        Add(1, 0xE2FF, "POS");
        Add(1, 0xEE8D, "SQR");
        Add(1, 0xEFAE, "RND");
        Add(1, 0xE941, "LOG");
        Add(1, 0xEF09, "EXP");
        Add(1, 0xEFEA, "COS");
        Add(1, 0xEFF1, "SIN");
        Add(1, 0xF03A, "TAN");
        Add(1, 0xF09E, "ATN");
        Add(1, 0xE764, "PEEK");
        Add(1, 0xE6D6, "LEN");
        Add(1, 0xE3C5, "STR$");
        Add(1, 0xE707, "VAL");
        Add(1, 0xE6E5, "ASC");
        Add(1, 0xE646, "CHR$");
        Add(1, 0xE65A, "LEFT$");
        Add(1, 0xE686, "RIGHT$");
        Add(1, 0xE691, "MID$");
        Add(1, 0xE7C1, "FADDT");
        Add(1, 0xE7AA, "FSUBT");
        Add(1, 0xE982, "FMULTT");
        Add(1, 0xEA69, "FDIVT");
        Add(1, 0xEE97, "FPWRT");
        Add(1, 0xDF55, "AND");
        Add(1, 0xDF4F, "OR");
        Add(1, 0xEED0, "NEGOP");
        Add(1, 0xDE98, "NOTFAC");
        Add(1, 0xDF65, "RELOPS");


        Add(0, 0xD828, "EXEC_STMT");
        Add(0, 0xDD7B, "FRMEVL");
        Add(0, 0xD412, "ERROR");
        Add(0, 0xD43C, "JUMPSTART");
        Add(0, 0xE000, "BASIC");
        Add(0, 0xE003, "BASIC2");
        Add(0, 0xF128, "COLDST");


        // Extended higher-verbosity hooks (level 3) - preserved list; add more as needed.
        Add(3, 0xD365, "STKSRCH");
        Add(3, 0xD393, "BLTU");
        Add(3, 0xD3D6, "CHKMEM");
        Add(3, 0xD3E3, "REASON");
        Add(3, 0xD410, "MEMERROR");
        Add(3, 0xD52C, "INLIN");
        Add(3, 0xD52E, "INLIN+2");
        Add(3, 0xD539, "GDBUFS");
        Add(3, 0xD553, "INCHR");
        Add(3, 0xD559, "RUN1");
        Add(3, 0xD56C, "RUN+");
        Add(3, 0xD61A, "FNDLIN");
        Add(3, 0xD64B, "SCRTCH");
        Add(3, 0xD66C, "CLEARC");
        Add(3, 0xD683, "STKINI");
        Add(3, 0xD697, "STXTPT");
        Add(3, 0xD6DA, "LIST1LIN");
        Add(3, 0xD7D2, "NEWSTT");
        Add(3, 0xD805, "TRACE_");
        Add(3, 0xD858, "ISCNTC");
        Add(3, 0xD8F0, "VARTIO");
        Add(3, 0xD901, "PROGIO");
        Add(3, 0xD941, "GOTO<");
        Add(3, 0xD979, "RETURN w/o GOSUB");
        Add(3, 0xD97C, "UNDEF'D STMT PRT");
        Add(3, 0xD998, "ADDON");
        Add(3, 0xD9A3, "DATAN");
        Add(3, 0xD9A6, "REMN");
        Add(3, 0xDA0C, "LINGET");
        Add(3, 0xDAB7, "COPY");
        Add(3, 0xDAFB, "CRDO");
        Add(3, 0xDA3A, "STROUT");
        Add(3, 0xDB3D, "STRPRT");
        Add(3, 0xDB57, "OUTSP");
        Add(3, 0xDB5A, "OUTQST");
        Add(3, 0xDD0B, "NEXT w/o FOR PRT");
        Add(3, 0xDD67, "FRMNUM");
        Add(3, 0xDD6A, "CHKNUM");
        Add(3, 0xDD6C, "CHKSTR");
        Add(3, 0xDD6D, "CHKVAL");
        Add(3, 0xDD76, "TYPMISM");
        Add(3, 0xDE47, "XORFPSIGN");
        Add(3, 0xDE60, "FRM_ELEMENT");
        Add(3, 0xDE81, "STRTXT");
        Add(3, 0xDEB2, "PARCHK");
        Add(3, 0xDEB8, "CHKCLS");
        Add(3, 0xDEBB, "CHKOPN");
        Add(3, 0xDEBE, "CHKCOM");
        Add(3, 0xDEC0, "SYNCHR");
        Add(3, 0xDEC9, "SYNERR");
        Add(3, 0xDFE3, "PTRGET");
        Add(3, 0xE07D, "ISLETC");
        Add(3, 0xE105, "EVAL EXPR => INT");
        Add(3, 0xE108, "AYPOSINT");
        Add(3, 0xE10C, "AYINT");
        Add(3, 0xE196, "SUB ERR");
        Add(3, 0xE199, "QTY ERR");
        Add(3, 0xE2F2, "GIVAYF");
        Add(3, 0xE301, "SNGFLT");
        Add(3, 0xE306, "ERRDIR");
        Add(3, 0xE30E, "UNDEF ERR");
        Add(3, 0xE3D5, "STRINI");
        Add(3, 0xE3DD, "STRSPA");
        Add(3, 0xE3E7, "STRLIT");
        Add(3, 0xE3ED, "STRLT2");
        Add(3, 0xE42A, "PUTNEW");
        Add(3, 0xE430, "FRM ERR");
        Add(3, 0xE452, "GETSPA");
        Add(3, 0xE484, "GARBAG");
        Add(3, 0xE597, "CAT");
        Add(3, 0xE5D4, "MOVINS");
        Add(3, 0xE5E2, "MOVSTR");
        Add(3, 0xE5FD, "FRESTR");
        Add(3, 0xE600, "FREFAC");
        Add(3, 0xE604, "FRETMP");
        Add(3, 0xE635, "FRETMS");
        Add(3, 0xE6F5, "GTBYTC");
        Add(3, 0xE6F8, "GETBYT");
        Add(3, 0xE6FB, "CONINT");
        Add(3, 0xE746, "GETNUM");
        Add(3, 0xE74C, "COMBYTE");
        Add(3, 0xE752, "GETADR");
        Add(3, 0xE7A0, "FADDH");
        Add(3, 0xE7A7, "FSUB");
        Add(3, 0xE7BE, "FADD");
        Add(3, 0xE7D5, "OVFLW ERR");
        Add(3, 0xE97F, "FMULT");
        Add(3, 0xE9E3, "CONUPK");
        Add(3, 0xEA39, "MUL10");
        Add(3, 0xEA55, "DIV10");
        Add(3, 0xEA66, "FDIV");
        Add(3, 0xEAE1, "DIVZERO ERR");
        Add(3, 0xEAF9, "MOVFM");
        Add(3, 0xEB1E, "MOV2F");
        Add(3, 0xEB21, "MOV1F");
        Add(3, 0xEB23, "MOVML");
        Add(3, 0xEB2B, "MOVMF");
        Add(3, 0xEB53, "MOVFA");
        Add(3, 0xEB63, "MOVAF");
        Add(3, 0xEB66, "MOVAF2");
        Add(3, 0xEB72, "RNDB");
        Add(3, 0xEB82, "SIGN");
        Add(3, 0xEB93, "FLOAT");
        Add(3, 0xEBB2, "FCOMP");
        Add(3, 0xEBF2, "QINT");
        Add(3, 0xEC40, "INITFACMANT");
        Add(3, 0xEC4A, "FIN");
        Add(3, 0xED19, "INPRT");
        Add(3, 0xED24, "LINPRT");
        Add(3, 0xED2E, "PRNTFAC");
        Add(3, 0xED34, "FOUT");
        Add(3, 0xEEFB, "LOGE(2)");
        Add(3, 0xF1EC, "PLOTFNS");
        Add(3, 0xF2E9, "HANDLERR");
        Add(3, 0xF3F2, "HCLR");
        Add(3, 0xF411, "HPOSN");
        Add(3, 0xF457, "HPLOT0");
        Add(3, 0xF53A, "HLINE");
        Add(3, 0xF5CB, "HFIND");
        Add(3, 0xF601, "DRAW1");
        Add(3, 0xF65D, "XDRAW1");
        Add(3, 0xF6B9, "HFNS");
        Add(3, 0xF6EC, "SETHCOL");
        Add(3, 0xF6F6, "COLRMASK");
        Add(3, 0xF7D9, "GETARYPT");
    }

    private void Add(int level, ushort addr, string label)
    {
        _table[addr] = Make(level, label, addr);
        _populatedCount++;
    }
}
