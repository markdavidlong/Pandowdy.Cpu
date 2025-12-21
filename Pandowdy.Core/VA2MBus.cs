using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Threading;
using Emulator;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Pandowdy.Core;

public interface IAppleIIBus : IBus
{
    public void SetKeyValue(byte key);

    public void SetPushButton(int num, bool enabled);
}

/// <summary>
/// VA2M-specific Bus that owns the CPU connection and routes reads/writes to VA2MMemory.
/// Emits a VBlank event based on CPU cycles (every 17063 cycles ~= 60Hz at 1.023MHz).
/// Threading: All public methods (Connect, CpuRead/Write, Clock, Reset) are expected to be called ONLY from the emulator worker thread.
/// VBlank event is raised on the emulator thread; subscribers on the UI thread MUST marshal via dispatcher.
/// After disposal, no further events are raised and Clock() becomes a no-op.
/// </summary>
public sealed class VA2MBus : IAppleIIBus, IDisposable
{
    //private readonly ISystemStatusProvider? _status;

    private readonly MemoryPool _memoryPool;
    private int lastPc = 0;
    private AppleSoftHookTable? _hookTable;
    private CPU? _cpu;
    private ulong _systemClock;
    private SoftSwitches _softSwitches = new();
//    private bool _isInVBlankBlackout = false;

    public IMemory RAM => _memoryPool;

    public ulong SystemClockCounter => _systemClock;
    private bool _disposed;
    private byte _currKey = 0x00;
    private bool _button0 = false;
    private bool _button1 = false;
    private bool _button2 = false;

    // Cycle-based VBlank (1,023,000 Hz / 60 ? 17050; using 17063 from spec for Apple II NTSC)
    private const ulong CyclesPerVBlank = 17063; // adjust if more precise timing required

    private const int VBlankBlackoutCycles = 4550;
    private ulong _nextVblankCycle = CyclesPerVBlank;
    private long _VblankBlackoutCounter = VBlankBlackoutCycles;

    public event EventHandler? VBlank;

    // 2.1 Hz flash timer (approx every 476 ms)
    private Timer? _flashTimer;

    private static readonly TimeSpan FlashPeriod = TimeSpan.FromMilliseconds(476);

    public const ushort IO_AREA_START = 0xC000;
    public const ushort IO_AREA_END = 0xCFFF;
    public const ushort SYSTEM_IO_START = 0xC000;
    public const ushort SYSTEM_UI_END = 0xC0FF;

    public const ushort KBD_ = 0xC000;
    public const ushort SET80STORE_ = 0xC000;
    public const ushort CLR80STORE_ = 0xC001;
    public const ushort RDMAINRAM_ = 0xC002;
    public const ushort RDCARDRAM_ = 0xC003;
    public const ushort WRMAINRAM_ = 0xC004;
    public const ushort WRCARDRAM_ = 0xC005;
    public const ushort SLOTCXROM_ = 0xC006;
    public const ushort INTCXROM_ = 0xC007;
    public const ushort STDZP_ = 0xC008;
    public const ushort ALTZP_ = 0xC009;
    public const ushort INTC3ROM_ = 0xC00A;
    public const ushort SLOTC3ROM_ = 0xC00B;
    public const ushort CLR80VID_ = 0xC00C;
    public const ushort SET80VID_ = 0xC00D;
    public const ushort CLRALTCHAR_ = 0xC00E;
    public const ushort SETALTCHAR_ = 0xC00F;

    public const ushort KEYSTRB_ = 0xC010;
    public const ushort RD_LC_BANK1_ = 0xC011;
    public const ushort RD_LC_RAM = 0xC012;
    public const ushort RD_RAMRD_ = 0xC013;
    public const ushort RD_RAMWRT_ = 0xC014;
    public const ushort RD_INTCXROM_ = 0xC015;
    public const ushort RD_ALTZP_ = 0xC016;
    public const ushort RD_SLOTC3ROM_ = 0xC017;
    public const ushort RD_80STORE_ = 0xC018;
    public const ushort RD_VERTBLANK_ = 0xC019;
    public const ushort RD_TEXT_ = 0xC01A;
    public const ushort RD_MIXED_ = 0xC01B;
    public const ushort RD_PAGE2_ = 0xC01C;
    public const ushort RD_HIRES_ = 0xC01D;
    public const ushort RD_ALTCHAR_ = 0xC01E;
    public const ushort RD_80VID_ = 0xC01F;

    public const ushort TAPEOUT_ = 0xC020; // to 0xC02F
    public const ushort END_TAPEOUT_RD_ = 0xC02F;
    public const ushort SPKR_ = 0xC030; // to 0xC03F
    public const ushort END_SPKR_RD_ = 0xC03F;
    public const ushort STROBE_ = 0xC040; // to 0xC04F
    public const ushort END_STROBE_RD_ = 0xC04F;

    public const ushort CLRTXT_ = 0xC050;
    public const ushort SETTXT_ = 0xC051;
    public const ushort CLRMIXED_ = 0xC052;
    public const ushort SETMIXED_ = 0xC053;
    public const ushort CLRPAGE2_ = 0xC054;
    public const ushort SETPAGE2_ = 0xC055;
    public const ushort CLRHIRES_ = 0xC056;
    public const ushort SETHIRES_ = 0xC057;
    public const ushort CLRAN0_ = 0xC058;
    public const ushort SETAN0_ = 0xC059;
    public const ushort CLRAN1_ = 0xC05A;
    public const ushort SETAN1_ = 0xC05B;
    public const ushort CLRAN2_ = 0xC05C;
    public const ushort SETAN2_ = 0xC05D;
    public const ushort CLRAN3_ = 0xC05E;
    public const ushort SETAN3_ = 0xC05F;

    public const ushort TAPEIN_ = 0xC060;
    public const ushort BUTTON0_ = 0xC061;
    public const ushort BUTTON1_ = 0xC062;
    public const ushort BUTTON2_ = 0xC063;
    public const ushort PADDLE0_ = 0xC064;
    public const ushort PADDLE1_ = 0xC065;
    public const ushort PADDLE2_ = 0xC066;
    public const ushort PADDLE3_ = 0xC067;

    public const ushort PTRIG_ = 0xC070;
    public const ushort RD_IOUDISABLE_ = 0xC07E;
    public const ushort IOUDISABLE_ = 0xC07E;
    public const ushort IOUENABLE_ = 0xC07F;

    public const ushort B2_RD_RAM_NO_WRT_ = 0xC080;
    public const ushort B2_RD_RAM_NO_WRT_ALT_ = 0xC084;
    public const ushort B2_RD_ROM_WRT_RAM_ = 0xC081;
    public const ushort B2_RD_ROM_WRT_RAM_ALT_ = 0xC085;
    public const ushort B2_RD_ROM_NO_WRT_ = 0xC082;
    public const ushort B2_RD_ROM_NO_WRT_ALT_ = 0xC086;
    public const ushort B2_RD_RAM_WRT_RAM_ = 0xC083;
    public const ushort B2_RD_RAM_WRT_RAM_ALT_ = 0xC087;

    public const ushort B1_RD_RAM_NO_WRT_ = 0xC088;
    public const ushort B1_RD_RAM_NO_WRT_ALT_ = 0xC08C;
    public const ushort B1_RD_ROM_WRT_RAM_ = 0xC089;
    public const ushort B1_RD_ROM_WRT_RAM_ALT_ = 0xC08D;
    public const ushort B1_RD_ROM_NO_WRT_ = 0xC08A;
    public const ushort B1_RD_ROM_NO_WRT_ALT_ = 0xC08E;
    public const ushort B1_RD_RAM_WRT_RAM_ = 0xC08B;
    public const ushort B1_RD_RAM_WRT_RAM_ALT_ = 0xC08F;

    public const ushort SLOT1_IO_SPACE = 0xC090;
    public const ushort SLOT1_IO_SPACE_END = 0xC09F;
    public const ushort SLOT2_IO_SPACE = 0xC0A0;
    public const ushort SLOT2_IO_SPACE_END = 0xC0AF;
    public const ushort SLOT3_IO_SPACE = 0xC0B0;
    public const ushort SLOT3_IO_SPACE_END = 0xC0BF;
    public const ushort SLOT4_IO_SPACE = 0xC0C0;
    public const ushort SLOT4_IO_SPACE_END = 0xC0CF;
    public const ushort SLOT5_IO_SPACE = 0xC0D0;
    public const ushort SLOT5_IO_SPACE_END = 0xC0DF;
    public const ushort SLOT6_IO_SPACE = 0xC0E0;
    public const ushort SLOT6_IO_SPACE_END = 0xC0EF;
    public const ushort SLOT7_IO_SPACE = 0xC0F0;
    public const ushort SLOT7_IO_SPACE_END = 0xC0FF;
    
    public SoftSwitches Switches => _softSwitches;

    public VA2MBus(MemoryPool mempool, ISoftSwitchResponder? responder = null)
    {
        _memoryPool = mempool;

        _softSwitches.AddResponder(responder ?? mempool);
    }

    private readonly System.Collections.Generic.Dictionary<ushort, System.Func<byte>> _ioReadHandlers = [];
    private readonly System.Collections.Generic.Dictionary<ushort, System.Action<byte>> _ioWriteHandlers = [];

    private void InitIoReadHandlers()
    {
        // Simple reads that only return composed values
        _ioReadHandlers[KEYSTRB_] = () => { _currKey &= 0x7f; return _currKey; };
        _ioReadHandlers[RD_LC_BANK1_] = () => (byte)(BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.Bank1), _currKey) ^ 0x80);
        _ioReadHandlers[RD_LC_RAM] = () => BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.HighRead), _currKey);
        _ioReadHandlers[RD_RAMRD_] = () => BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.RamRd), _currKey);
        _ioReadHandlers[RD_RAMWRT_] = () => BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.RamWrt), _currKey);
        _ioReadHandlers[RD_INTCXROM_] = () => BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.IntCxRom), _currKey);
        _ioReadHandlers[RD_ALTZP_] = () => BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.AltZp), _currKey);
        _ioReadHandlers[RD_SLOTC3ROM_] = () => BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.SlotC3Rom), _currKey);
        _ioReadHandlers[RD_80STORE_] = () => BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.Store80), _currKey);
        _ioReadHandlers[RD_TEXT_] = () => BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.Text), _currKey);
        _ioReadHandlers[RD_MIXED_] = () => BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.Mixed), _currKey);
        _ioReadHandlers[RD_PAGE2_] = () => BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.Page2), _currKey);
        _ioReadHandlers[RD_HIRES_] = () => BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.HiRes), _currKey);
        _ioReadHandlers[RD_ALTCHAR_] = () => BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.AltChar), _currKey);
        _ioReadHandlers[RD_80VID_] = () => BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.Vid80), _currKey);
        _ioReadHandlers[RD_VERTBLANK_] = () => { byte inVBlank = (byte)(_VblankBlackoutCounter > 0 ? 0x80 : 0x00); return (byte)(inVBlank | _currKey & 0x7f); };
        _ioReadHandlers[TAPEIN_] = () => 0x00;
        _ioReadHandlers[BUTTON0_] = () => (byte)(_button0 ? 0x80 : 0x00);
        _ioReadHandlers[BUTTON1_] = () => (byte)(_button1 ? 0x80 : 0x00);
        _ioReadHandlers[BUTTON2_] = () => (byte)(_button2 ? 0x80 : 0x00);

        // Reads that also toggle soft switches (Apple II behavior: reading addresses sets switches)
        _ioReadHandlers[CLRTXT_] = () => { _softSwitches.Set(SoftSwitches.SoftSwitchId.Text, false); return 0xA0; };
        _ioReadHandlers[SETTXT_] = () => { _softSwitches.Set(SoftSwitches.SoftSwitchId.Text, true); return 0xA0; };
        _ioReadHandlers[CLRMIXED_] = () => { _softSwitches.Set(SoftSwitches.SoftSwitchId.Mixed, false); return 0xA0; };
        _ioReadHandlers[SETMIXED_] = () => { _softSwitches.Set(SoftSwitches.SoftSwitchId.Mixed, true); return 0xA0; };
        _ioReadHandlers[CLRPAGE2_] = () => { _softSwitches.Set(SoftSwitches.SoftSwitchId.Page2, false); return 0xA0; };
        _ioReadHandlers[SETPAGE2_] = () => { _softSwitches.Set(SoftSwitches.SoftSwitchId.Page2, true); return 0xA0; };
        _ioReadHandlers[CLRHIRES_] = () => { _softSwitches.Set(SoftSwitches.SoftSwitchId.HiRes, false); return 0xA0; };
        _ioReadHandlers[SETHIRES_] = () => { _softSwitches.Set(SoftSwitches.SoftSwitchId.HiRes, true); return 0xA0; };
        _ioReadHandlers[CLRAN0_] = () => { _softSwitches.Set(SoftSwitches.SoftSwitchId.An0, false); return BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.An0)); };
        _ioReadHandlers[SETAN0_] = () => { _softSwitches.Set(SoftSwitches.SoftSwitchId.An0, true); return BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.An0)); };
        _ioReadHandlers[CLRAN1_] = () => { _softSwitches.Set(SoftSwitches.SoftSwitchId.An1, false); return BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.An1)); };
        _ioReadHandlers[SETAN1_] = () => { _softSwitches.Set(SoftSwitches.SoftSwitchId.An1, true); return BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.An1)); };
        _ioReadHandlers[CLRAN2_] = () => { _softSwitches.Set(SoftSwitches.SoftSwitchId.An2, false); return BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.An2)); };
        _ioReadHandlers[SETAN2_] = () => { _softSwitches.Set(SoftSwitches.SoftSwitchId.An2, true); return BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.An2)); };
        _ioReadHandlers[CLRAN3_] = () => { _softSwitches.Set(SoftSwitches.SoftSwitchId.An3, false); return BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.An3)); };
        _ioReadHandlers[SETAN3_] = () => { _softSwitches.Set(SoftSwitches.SoftSwitchId.An3, true); return BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.An3)); };

        // Banked block reads (unrolled to individual addresses, sharing helper)
        _ioReadHandlers[B2_RD_RAM_NO_WRT_] = () => { ApplyBankIoReadFlags(false, B2_RD_RAM_NO_WRT_); return 0xA0; };
        _ioReadHandlers[B2_RD_RAM_NO_WRT_ALT_] = () => { ApplyBankIoReadFlags(false, B2_RD_RAM_NO_WRT_ALT_); return 0xA0; };
        _ioReadHandlers[B2_RD_ROM_WRT_RAM_] = () => { ApplyBankIoReadFlags(false, B2_RD_ROM_WRT_RAM_); return 0xA0; };
        _ioReadHandlers[B2_RD_ROM_WRT_RAM_ALT_] = () => { ApplyBankIoReadFlags(false, B2_RD_ROM_WRT_RAM_ALT_); return 0xA0; };
        _ioReadHandlers[B2_RD_ROM_NO_WRT_] = () => { ApplyBankIoReadFlags(false, B2_RD_ROM_NO_WRT_); return 0xA0; };
        _ioReadHandlers[B2_RD_ROM_NO_WRT_ALT_] = () => { ApplyBankIoReadFlags(false, B2_RD_ROM_NO_WRT_ALT_); return 0xA0; };
        _ioReadHandlers[B2_RD_RAM_WRT_RAM_] = () => { ApplyBankIoReadFlags(false, B2_RD_RAM_WRT_RAM_); return 0xA0; };
        _ioReadHandlers[B2_RD_RAM_WRT_RAM_ALT_] = () => { ApplyBankIoReadFlags(false, B2_RD_RAM_WRT_RAM_ALT_); return 0xA0; };

        _ioReadHandlers[B1_RD_RAM_NO_WRT_] = () => { ApplyBankIoReadFlags(true, B1_RD_RAM_NO_WRT_); return 0xA0; };
        _ioReadHandlers[B1_RD_RAM_NO_WRT_ALT_] = () => { ApplyBankIoReadFlags(true, B1_RD_RAM_NO_WRT_ALT_); return 0xA0; };
        _ioReadHandlers[B1_RD_ROM_WRT_RAM_] = () => { ApplyBankIoReadFlags(true, B1_RD_ROM_WRT_RAM_); return 0xA0; };
        _ioReadHandlers[B1_RD_ROM_WRT_RAM_ALT_] = () => { ApplyBankIoReadFlags(true, B1_RD_ROM_WRT_RAM_ALT_); return 0xA0; };
        _ioReadHandlers[B1_RD_ROM_NO_WRT_] = () => { ApplyBankIoReadFlags(true, B1_RD_ROM_NO_WRT_); return 0xA0; };
        _ioReadHandlers[B1_RD_ROM_NO_WRT_ALT_] = () => { ApplyBankIoReadFlags(true, B1_RD_ROM_NO_WRT_ALT_); return 0xA0; };
        _ioReadHandlers[B1_RD_RAM_WRT_RAM_] = () => { ApplyBankIoReadFlags(true, B1_RD_RAM_WRT_RAM_); return 0xA0; };
        _ioReadHandlers[B1_RD_RAM_WRT_RAM_ALT_] = () => { ApplyBankIoReadFlags(true, B1_RD_RAM_WRT_RAM_ALT_); return 0xA0; };
    }

    private void ApplyBankIoReadFlags(bool bank1, ushort address)
    {
        _softSwitches.Set(SoftSwitches.SoftSwitchId.Bank1, bank1);
        if (address == B2_RD_RAM_NO_WRT_ || address == B2_RD_RAM_NO_WRT_ALT_ || address == B1_RD_RAM_NO_WRT_ || address == B1_RD_RAM_NO_WRT_ALT_)
        {
            _softSwitches.Set(SoftSwitches.SoftSwitchId.PreWrite, false);
            _softSwitches.Set(SoftSwitches.SoftSwitchId.HighWrite, false);
            _softSwitches.Set(SoftSwitches.SoftSwitchId.HighRead, true);
            return;
        }
        if (address == B2_RD_ROM_WRT_RAM_ || address == B2_RD_ROM_WRT_RAM_ALT_ || address == B1_RD_ROM_WRT_RAM_ || address == B1_RD_ROM_WRT_RAM_ALT_)
        {
            SetWriteOrPrewrite();
            _softSwitches.Set(SoftSwitches.SoftSwitchId.HighRead, false);
            return;
        }
        if (address == B2_RD_ROM_NO_WRT_ || address == B2_RD_ROM_NO_WRT_ALT_ || address == B1_RD_ROM_NO_WRT_ || address == B1_RD_ROM_NO_WRT_ALT_)
        {
            _softSwitches.Set(SoftSwitches.SoftSwitchId.PreWrite, false);
            _softSwitches.Set(SoftSwitches.SoftSwitchId.HighWrite, false);
            _softSwitches.Set(SoftSwitches.SoftSwitchId.HighRead, false);
            return;
        }
        if (address == B2_RD_RAM_WRT_RAM_ || address == B2_RD_RAM_WRT_RAM_ALT_ || address == B1_RD_RAM_WRT_RAM_ || address == B1_RD_RAM_WRT_RAM_ALT_)
        {
            SetWriteOrPrewrite();
            _softSwitches.Set(SoftSwitches.SoftSwitchId.HighRead, true);
            return;
        }
    }

    private void InitIoWriteHandlers()
    {
        // Simple soft-switch writes
        _ioWriteHandlers[SET80STORE_] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.Store80, false);
        _ioWriteHandlers[CLR80STORE_] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.Store80, true);
        _ioWriteHandlers[RDMAINRAM_] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.RamRd, false);
        _ioWriteHandlers[RDCARDRAM_] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.RamRd, true);
        _ioWriteHandlers[WRMAINRAM_] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.RamWrt, false);
        _ioWriteHandlers[WRCARDRAM_] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.RamWrt, true);
        _ioWriteHandlers[SLOTCXROM_] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.IntCxRom, false);
        _ioWriteHandlers[INTCXROM_] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.IntCxRom, true);
        _ioWriteHandlers[STDZP_] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.AltZp, false);
        _ioWriteHandlers[ALTZP_] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.AltZp, true);
        _ioWriteHandlers[INTC3ROM_] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.SlotC3Rom, false);
        _ioWriteHandlers[SLOTC3ROM_] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.SlotC3Rom, true);
        _ioWriteHandlers[CLR80VID_] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.Vid80, false);
        _ioWriteHandlers[SET80VID_] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.Vid80, true);
        _ioWriteHandlers[CLRALTCHAR_] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.AltChar, false);
        _ioWriteHandlers[SETALTCHAR_] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.AltChar, true);
        _ioWriteHandlers[CLRTXT_] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.Text, false);
        _ioWriteHandlers[SETTXT_] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.Text, true);
        _ioWriteHandlers[CLRMIXED_] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.Mixed, false);
        _ioWriteHandlers[SETMIXED_] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.Mixed, true);
        _ioWriteHandlers[CLRPAGE2_] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.Page2, false);
        _ioWriteHandlers[SETPAGE2_] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.Page2, true);
        _ioWriteHandlers[CLRHIRES_] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.HiRes, false);
        _ioWriteHandlers[SETHIRES_] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.HiRes, true);
        _ioWriteHandlers[CLRAN0_] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.An0, false);
        _ioWriteHandlers[SETAN0_] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.An0, true);
        _ioWriteHandlers[CLRAN1_] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.An1, false);
        _ioWriteHandlers[SETAN1_] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.An1, true);
        _ioWriteHandlers[CLRAN2_] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.An2, false);
        _ioWriteHandlers[SETAN2_] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.An2, true);
        _ioWriteHandlers[CLRAN3_] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.An3, false);
        _ioWriteHandlers[SETAN3_] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.An3, true);

        // Banked block writes (unrolled) — note write-path semantics differ from read-path
        _ioWriteHandlers[B2_RD_RAM_NO_WRT_] = _ => ApplyBankIoWriteFlags(false, B2_RD_RAM_NO_WRT_);
        _ioWriteHandlers[B2_RD_RAM_NO_WRT_ALT_] = _ => ApplyBankIoWriteFlags(false, B2_RD_RAM_NO_WRT_ALT_);
        _ioWriteHandlers[B2_RD_ROM_WRT_RAM_] = _ => ApplyBankIoWriteFlags(false, B2_RD_ROM_WRT_RAM_);
        _ioWriteHandlers[B2_RD_ROM_WRT_RAM_ALT_] = _ => ApplyBankIoWriteFlags(false, B2_RD_ROM_WRT_RAM_ALT_);
        _ioWriteHandlers[B2_RD_ROM_NO_WRT_] = _ => ApplyBankIoWriteFlags(false, B2_RD_ROM_NO_WRT_);
        _ioWriteHandlers[B2_RD_ROM_NO_WRT_ALT_] = _ => ApplyBankIoWriteFlags(false, B2_RD_ROM_NO_WRT_ALT_);
        _ioWriteHandlers[B2_RD_RAM_WRT_RAM_] = _ => ApplyBankIoWriteFlags(false, B2_RD_RAM_WRT_RAM_);
        _ioWriteHandlers[B2_RD_RAM_WRT_RAM_ALT_] = _ => ApplyBankIoWriteFlags(false, B2_RD_RAM_WRT_RAM_ALT_);

        _ioWriteHandlers[B1_RD_RAM_NO_WRT_] = _ => ApplyBankIoWriteFlags(true, B1_RD_RAM_NO_WRT_);
        _ioWriteHandlers[B1_RD_RAM_NO_WRT_ALT_] = _ => ApplyBankIoWriteFlags(true, B1_RD_RAM_NO_WRT_ALT_);
        _ioWriteHandlers[B1_RD_ROM_WRT_RAM_] = _ => ApplyBankIoWriteFlags(true, B1_RD_ROM_WRT_RAM_);
        _ioWriteHandlers[B1_RD_ROM_WRT_RAM_ALT_] = _ => ApplyBankIoWriteFlags(true, B1_RD_ROM_WRT_RAM_ALT_);
        _ioWriteHandlers[B1_RD_ROM_NO_WRT_] = _ => ApplyBankIoWriteFlags(true, B1_RD_ROM_NO_WRT_);
        _ioWriteHandlers[B1_RD_ROM_NO_WRT_ALT_] = _ => ApplyBankIoWriteFlags(true, B1_RD_ROM_NO_WRT_ALT_);
        _ioWriteHandlers[B1_RD_RAM_WRT_RAM_] = _ => ApplyBankIoWriteFlags(true, B1_RD_RAM_WRT_RAM_);
        _ioWriteHandlers[B1_RD_RAM_WRT_RAM_ALT_] = _ => ApplyBankIoWriteFlags(true, B1_RD_RAM_WRT_RAM_ALT_);
    }

    private void ApplyBankIoWriteFlags(bool bank1, ushort address)
    {
        _softSwitches.Set(SoftSwitches.SoftSwitchId.Bank1, bank1);
        if (address == B2_RD_RAM_NO_WRT_ || address == B2_RD_RAM_NO_WRT_ALT_ || address == B1_RD_RAM_NO_WRT_ || address == B1_RD_RAM_NO_WRT_ALT_)
        {
            _softSwitches.Set(SoftSwitches.SoftSwitchId.PreWrite, false);
            _softSwitches.Set(SoftSwitches.SoftSwitchId.HighWrite, false);
            _softSwitches.Set(SoftSwitches.SoftSwitchId.HighRead, true);
            return;
        }
        if (address == B2_RD_ROM_WRT_RAM_ || address == B2_RD_ROM_WRT_RAM_ALT_ || address == B1_RD_ROM_WRT_RAM_ || address == B1_RD_ROM_WRT_RAM_ALT_)
        {
            _softSwitches.Set(SoftSwitches.SoftSwitchId.PreWrite, false);
            _softSwitches.Set(SoftSwitches.SoftSwitchId.HighRead, false);
            return;
        }
        if (address == B2_RD_ROM_NO_WRT_ || address == B2_RD_ROM_NO_WRT_ALT_ || address == B1_RD_ROM_NO_WRT_ || address == B1_RD_ROM_NO_WRT_ALT_)
        {
            _softSwitches.Set(SoftSwitches.SoftSwitchId.PreWrite, false);
            _softSwitches.Set(SoftSwitches.SoftSwitchId.HighWrite, false);
            _softSwitches.Set(SoftSwitches.SoftSwitchId.HighRead, false);
            return;
        }
        if (address == B2_RD_RAM_WRT_RAM_ || address == B2_RD_RAM_WRT_RAM_ALT_ || address == B1_RD_RAM_WRT_RAM_ || address == B1_RD_RAM_WRT_RAM_ALT_)
        {
            _softSwitches.Set(SoftSwitches.SoftSwitchId.PreWrite, false);
            _softSwitches.Set(SoftSwitches.SoftSwitchId.HighRead, true);
            return;
        }
    }

    public void Connect(CPU cpu)
    {
        ThrowIfDisposed();
        _cpu = cpu;
        _cpu.Connect(this);
        _hookTable ??= new AppleSoftHookTable();
        _hookTable.InitializeDefault();
        InitIoReadHandlers();
        InitIoWriteHandlers();
    }

    public void SetKeyValue(byte key)
    {
        _currKey = key;
    }

    public bool GetPushButton(int num)
    {
        return num switch
        {
            0 => _button0,
            1 => _button1,
            2 => _button2,
            _ => false,
        };
    }

    public void SetPushButton(int num, bool pressed)
    {
        switch (num)
        {
            case 0:
                _button0 = pressed;
                break;

            case 1:
                _button1 = pressed;
                break;

            case 2:
                _button2 = pressed;
                break;
        }
    }

    private void SetWriteOrPrewrite()
    {
        // if prewrite isn't set, set it and return.
        // if prewrite is already set, increment write count

        if (!_softSwitches.Get(SoftSwitches.SoftSwitchId.PreWrite))
        {
            _softSwitches.Set(SoftSwitches.SoftSwitchId.PreWrite, true);
        }
        else
        {
            _softSwitches.Set(SoftSwitches.SoftSwitchId.HighWrite, true);
        }
    }

    private static byte BuildHiBitVal(bool state, byte other = 0x00)
    {
        return (byte) ((state ? 0x80 : 0x00) | (other & 0x7f));
    }

    private byte ReadFromIOSpace(ushort address)
    {
        if (_ioReadHandlers.TryGetValue(address, out var handler))
        {
            return handler();
        }
        // Fallback to existing logic for unhandled addresses
        if (address >= SET80STORE_ && address <= SETALTCHAR_)
        {
            return _currKey;
        }
        if (address >= TAPEOUT_ && address <= END_TAPEOUT_RD_)
        {
            Debug.WriteLine($"TAPEOUT read not implemented yet (Read from {address:X4})");
            return 0;
        }
        if (address >= SPKR_ && address <= END_SPKR_RD_)
        {
            return 0;
        }
        if (address >= STROBE_ && address <= END_STROBE_RD_)
        {
            Debug.WriteLine($"STROBE read not implemented yet (Read from {address:X4})");
            return 0;
        }
        if (address == TAPEIN_)
        {
            return 0x00;
        }
        // Preserve original switch-case fallback for any addresses not yet tabled
        switch (address)
        {
            case PADDLE0_: return 0x00;
            case PADDLE1_: return 0x00;
            case PADDLE2_: return 0x00;
            case PADDLE3_: return 0x00;
        }

        if (address >= B2_RD_RAM_NO_WRT_ && address <= B2_RD_RAM_WRT_RAM_ALT_)
        {
            ApplyBankIoReadFlags(false, address);
        }
        else if (address >= B1_RD_RAM_NO_WRT_ && address <= B1_RD_RAM_WRT_RAM_ALT_)
        {
            ApplyBankIoReadFlags(true, address);
        }
        else
        {
            Debug.WriteLine($"Read from to unhandled IO Space: {address:X4}");
        }
        return 0xA0;
    }

    private void WriteToIOSpace(ushort address, byte _ /*data*/)
    {
        if (_ioWriteHandlers.TryGetValue(address, out var writer))
        {
            writer(_);
            return;
        }
        if (address >= KEYSTRB_ && address <= KEYSTRB_ + 0x1F)
        {
            _currKey &= 0x7f; // Clear high byte;
            return;
        }
        if (address >= TAPEOUT_ && address <= TAPEOUT_ + 0x0F)
        {
            Debug.WriteLine($"TAPEOUT not implemented yet (Write to {address:X4})");
            return;
        }
        if (address >= SPKR_ && address <= KEYSTRB_ + 0x0F)
        {
            Debug.WriteLine($"SPKR not implemented yet (Write to {address:X4})");
            return;
        }
        if (address >= STROBE_ && address <= STROBE_ + 0x0F)
        {
            Debug.WriteLine($"STROBE not implemented yet (Write to {address:X4})");
            return;
        }
        if (address >= B2_RD_RAM_NO_WRT_ && address <= B2_RD_RAM_WRT_RAM_ALT_)
        {
            ApplyBankIoWriteFlags(false, address);
            return;
        }
        if (address >= B1_RD_RAM_NO_WRT_ && address <= B1_RD_RAM_WRT_RAM_ALT_)
        {
            ApplyBankIoWriteFlags(true, address);
            return;
        }
        Debug.WriteLine($"Write to unhandled IO Space: {address:X4}");
    }

    public byte CpuRead(ushort address, bool readOnly = false)
    {
        ThrowIfDisposed();
        if (address >= SYSTEM_IO_START && address <= SLOT7_IO_SPACE_END)
        {
            return ReadFromIOSpace(address);
        }
        return _memoryPool.Read(address);
    }

    public void CpuWrite(ushort address, byte data)
    {
        ThrowIfDisposed();
        if (address >= SYSTEM_IO_START && address <= SLOT7_IO_SPACE_END)
        {
            WriteToIOSpace(address, data);
            return;
        }
        _memoryPool.Write(address, data);
    }

    public void Clock()
    {
        if (_disposed)
        {
            return;
        }

        var currPc = _cpu!.PC;
        if (lastPc != currPc)
        {
            var hook = _hookTable?.Get((ushort) currPc);
            if (hook != null)
            {
                var lineNum = this.CpuRead(0x75) + (this.CpuRead(0x76) * 256);
                string ln = lineNum < 0xFA00 ? lineNum.ToString() : "IMM";
                var sp = _cpu!.SP;
                var spcs = 0xFF - sp;
                hook(-1, lineNum, spcs);
            }

        }
        lastPc = currPc;

        // Execute a single CPU cycle
        _cpu!.Clock();
        _systemClock++;
        _VblankBlackoutCounter--;

        if (_systemClock >= _nextVblankCycle)
        {
            // Catch up if emulator ran fast (unthrottled batches)
            do
            { 
                _nextVblankCycle += CyclesPerVBlank;
                _VblankBlackoutCounter = VBlankBlackoutCycles;

            } while (_systemClock >= _nextVblankCycle);

            if (!_disposed)
            {
                VBlank?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public void Reset()
    {
        ThrowIfDisposed();
        _memoryPool.ResetRanges();
        _softSwitches.ResetAllSwitches();
        _cpu!.Reset();
        _systemClock = 0;
        _nextVblankCycle = CyclesPerVBlank;
    }

    public void UserReset()
    {
        ThrowIfDisposed();

        _softSwitches.ResetAllSwitches();
        _memoryPool.ResetRanges();
        _cpu!.Reset();
        _systemClock = 0;
        _nextVblankCycle = CyclesPerVBlank;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        VBlank = null;
        _cpu = null;
        _hookTable = null;
        _flashTimer?.Dispose();
        _flashTimer = null;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(VA2MBus));
    }
}