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

    public IMemory RAM => _memoryPool;

    public ulong SystemClockCounter => _systemClock;
    private bool _disposed;
    private byte _currKey = 0x00;
    private bool _button0 = false;
    private bool _button1 = false;
    private bool _button2 = false;

    // Cycle-based VBlank (1,023,000 Hz / 60 ? 17050; using 17063 from spec for Apple II NTSC)
    private const ulong CyclesPerVBlank = 17063; // adjust if more precise timing required

    private ulong _nextVblankCycle = CyclesPerVBlank;

    public event EventHandler? VBlank;

    // 2.1 Hz flash timer (approx every 476 ms)
    private Timer? _flashTimer;

    private static readonly TimeSpan FlashPeriod = TimeSpan.FromMilliseconds(476);

    public static readonly ushort IOSTART = 0xC000;
    public static readonly ushort IOEND = 0xC0FF;

    public static readonly ushort KBD_ = 0xC000;
    public static readonly ushort SET80STORE_ = 0xC000;
    public static readonly ushort CLR80STORE_ = 0xC001;
    public static readonly ushort RDMAINRAM_ = 0xC002;
    public static readonly ushort RDCARDRAM_ = 0xC003;
    public static readonly ushort WRMAINRAM_ = 0xC004;
    public static readonly ushort WRCARDRAM_ = 0xC005;
    public static readonly ushort SLOTCXROM_ = 0xC006;
    public static readonly ushort INTCXROM_ = 0xC007;
    public static readonly ushort STDZP_ = 0xC008;
    public static readonly ushort ALTZP_ = 0xC009;
    public static readonly ushort INTC3ROM_ = 0xC00A;
    public static readonly ushort SLOTC3ROM_ = 0xC00B;
    public static readonly ushort CLR80VID_ = 0xC00C;
    public static readonly ushort SET80VID_ = 0xC00D;
    public static readonly ushort CLRALTCHAR_ = 0xC00E;
    public static readonly ushort SETALTCHAR_ = 0xC00F;

    public static readonly ushort KEYSTRB_ = 0xC010;
    public static readonly ushort RD_LC_BANK1_ = 0xC011;
    public static readonly ushort RD_LC_RAM = 0xC012;
    public static readonly ushort RD_RAMRD_ = 0xC013;
    public static readonly ushort RD_RAMWRT_ = 0xC014;
    public static readonly ushort RD_INTCXROM_ = 0xC015;
    public static readonly ushort RD_ALTZP_ = 0xC016;
    public static readonly ushort RD_SLOTC3ROM_ = 0xC017;
    public static readonly ushort RD_80STORE_ = 0xC018;
    public static readonly ushort RD_VERTBLANK_ = 0xC019;
    public static readonly ushort RD_TEXT_ = 0xC01A;
    public static readonly ushort RD_MIXED_ = 0xC01B;
    public static readonly ushort RD_PAGE2_ = 0xC01C;
    public static readonly ushort RD_HIRES_ = 0xC01D;
    public static readonly ushort RD_ALTCHAR_ = 0xC01E;
    public static readonly ushort RD_80VID_ = 0xC01F;

    public static readonly ushort TAPEOUT_ = 0xC020; // to 0xC02F
    public static readonly ushort END_TAPEOUT_RD_ = 0xC02F;
    public static readonly ushort SPKR_ = 0xC030; // to 0xC03F
    public static readonly ushort END_SPKR_RD_ = 0xC03F;
    public static readonly ushort STROBE_ = 0xC040; // to 0xC04F
    public static readonly ushort END_STROBE_RD_ = 0xC04F;

    public static readonly ushort CLRTXT_ = 0xC050;
    public static readonly ushort SETTXT_ = 0xC051;
    public static readonly ushort CLRMIXED_ = 0xC052;
    public static readonly ushort SETMIXED_ = 0xC053;
    public static readonly ushort CLRPAGE2_ = 0xC054;
    public static readonly ushort SETPAGE2_ = 0xC055;
    public static readonly ushort CLRHIRES_ = 0xC056;
    public static readonly ushort SETHIRES_ = 0xC057;
    public static readonly ushort CLRAN0_ = 0xC058;
    public static readonly ushort SETAN0_ = 0xC059;
    public static readonly ushort CLRAN1_ = 0xC05A;
    public static readonly ushort SETAN1_ = 0xC05B;
    public static readonly ushort CLRAN2_ = 0xC05C;
    public static readonly ushort SETAN2_ = 0xC05D;
    public static readonly ushort CLRAN3_ = 0xC05E;
    public static readonly ushort SETAN3_ = 0xC05F;

    public static readonly ushort TAPEIN_ = 0xC060;
    public static readonly ushort BUTTON0_ = 0xC061;
    public static readonly ushort BUTTON1_ = 0xC062;
    public static readonly ushort BUTTON2_ = 0xC063;
    public static readonly ushort PADDLE0_ = 0xC064;
    public static readonly ushort PADDLE1_ = 0xC065;
    public static readonly ushort PADDLE2_ = 0xC066;
    public static readonly ushort PADDLE3_ = 0xC067;

    public static readonly ushort PTRIG_ = 0xC070;
    public static readonly ushort RD_IOUDISABLE_ = 0xC07E;
    public static readonly ushort IOUDISABLE_ = 0xC07E;
    public static readonly ushort IOUENABLE_ = 0xC07F;

    public static readonly ushort B2_RD_RAM_NO_WRT_ = 0xC080;
    public static readonly ushort B2_RD_RAM_NO_WRT_ALT_ = 0xC084;
    public static readonly ushort B2_RD_ROM_WRT_RAM_ = 0xC081;
    public static readonly ushort B2_RD_ROM_WRT_RAM_ALT_ = 0xC085;
    public static readonly ushort B2_RD_ROM_NO_WRT_ = 0xC082;
    public static readonly ushort B2_RD_ROM_NO_WRT_ALT_ = 0xC086;
    public static readonly ushort B2_RD_RAM_WRT_RAM_ = 0xC083;
    public static readonly ushort B2_RD_RAM_WRT_RAM_ALT_ = 0xC087;

    public static readonly ushort B1_RD_RAM_NO_WRT_ = 0xC088;
    public static readonly ushort B1_RD_RAM_NO_WRT_ALT_ = 0xC08C;
    public static readonly ushort B1_RD_ROM_WRT_RAM_ = 0xC089;
    public static readonly ushort B1_RD_ROM_WRT_RAM_ALT_ = 0xC08C;
    public static readonly ushort B1_RD_ROM_NO_WRT_ = 0xC08A;
    public static readonly ushort B1_RD_ROM_NO_WRT_ALT_ = 0xC08E;
    public static readonly ushort B1_RD_RAM_WRT_RAM_ = 0xC08B;
    public static readonly ushort B1_RD_RAM_WRT_RAM_ALT_ = 0xC08F;

    public VA2MBus(MemoryPool mempool, ISoftSwitchResponder? responder = null)
    {
        _memoryPool = mempool;

        _softSwitches.AddResponder(responder ?? mempool);
    }

    public void Connect(CPU cpu)
    {
        ThrowIfDisposed();
        _cpu = cpu;
        _cpu.Connect(this);
        _hookTable ??= new AppleSoftHookTable();
        _hookTable.InitializeDefault();

        //TODO: MOVE THE FLASH TIMER TO VA2M SYSTEM CLASS. It doesn't need to be in the bus.
        //
        //// Start flash timer if status provider available
        //if (_status != null && _flashTimer == null)
        //{
        //    _flashTimer = new Timer(_ =>
        //    {
        //        if (_disposed) { return; }
        //        _status!.Mutate(b => b.StateFlashOn = !b.StateFlashOn);
        //    }, null, FlashPeriod, FlashPeriod);
        //}
    }

    public void SetKeyValue(byte key)
    {
        _currKey = key;
    }

    public void SetPushButton(int num, bool pressed)
    {
        //_status!.Mutate(b =>
        //{
        switch (num)
        {
            case 0:
                _button0 = pressed;
                //b.StatePb0 = pressed;
                break;

            case 1:
                _button1 = pressed;
                //b.StatePb1 = pressed;
                break;

            case 2:
                _button2 = pressed;
                //b.StatePb2 = pressed;
                break;
        }
        //});
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

    private byte ReadFromIOSpace(ushort address)
    {
        if (address >= SET80STORE_ && address <= SETALTCHAR_)
        {
            return _currKey;
        }

        if (address == KEYSTRB_)
        {
            _currKey &= 0x7f; // Clear high byte;
                              //Temporary fix:
            return _currKey; // Should return 0x80 if key is pressed or 0x00 if not; simplified to always 0x00
        }

        if (address == RD_LC_BANK1_)
        {
            var state = _softSwitches.Get(SoftSwitches.SoftSwitchId.Bank1);
            return (byte) ((state ? 0x80 : 0x00) + _currKey & 0x7f);
        }

        if (address == RD_LC_RAM)
        {
            var state = _softSwitches.Get(SoftSwitches.SoftSwitchId.HighRead);
            return (byte) ((state ? 0x80 : 0x00) + _currKey & 0x7f);
        }

        if (address == RD_RAMRD_)
        {
            var state = _softSwitches.Get(SoftSwitches.SoftSwitchId.RamRd);
            return (byte) ((state ? 0x80 : 0x00) + _currKey & 0x7f);
        }

        if (address == RD_RAMWRT_)
        {
            var state = _softSwitches.Get(SoftSwitches.SoftSwitchId.RamWrt);
            return (byte) ((state ? 0x80 : 0x00) + _currKey & 0x7f);
        }

        if (address == RD_INTCXROM_)
        {
            var state = _softSwitches.Get(SoftSwitches.SoftSwitchId.IntCxRom);
            return (byte) ((state ? 0x80 : 0x00) + _currKey & 0x7f);
        }

        if (address == RD_ALTZP_)
        {
            var state = _softSwitches.Get(SoftSwitches.SoftSwitchId.AltZp);
            return (byte) ((state ? 0x80 : 0x00) + _currKey & 0x7f);
        }

        if (address == RD_SLOTC3ROM_)
        {
            var state = _softSwitches.Get(SoftSwitches.SoftSwitchId.SlotC3Rom);
            return (byte) ((state ? 0x80 : 0x00) + _currKey & 0x7f);
        }

        if (address == RD_80STORE_)
        {
            var state = _softSwitches.Get(SoftSwitches.SoftSwitchId.Store80);
            return (byte) ((state ? 0x80 : 0x00) + _currKey & 0x7f);
        }

        if (address == RD_VERTBLANK_)
        {
            // Vert Blanking Status => 1 = Drawing
            // NOT IMPLEMENTED YET
            return _currKey;
        }

        if (address == RD_TEXT_)
        {
            var state = _softSwitches.Get(SoftSwitches.SoftSwitchId.Text);
            return (byte) ((state ? 0x80 : 0x00) + _currKey & 0x7f);
        }

        if (address == RD_MIXED_)
        {
            var state = _softSwitches.Get(SoftSwitches.SoftSwitchId.Mixed);
            return (byte) ((state ? 0x80 : 0x00) + _currKey & 0x7f);
        }

        if (address == RD_PAGE2_)
        {
            var state = _softSwitches.Get(SoftSwitches.SoftSwitchId.Page2);
            return (byte) ((state ? 0x80 : 0x00) + _currKey & 0x7f);
        }

        if (address == RD_HIRES_)
        {
            var state = _softSwitches.Get(SoftSwitches.SoftSwitchId.HiRes);
            return (byte) ((state ? 0x80 : 0x00) + _currKey & 0x7f);
        }

        if (address == RD_ALTCHAR_)
        {
            var state = _softSwitches.Get(SoftSwitches.SoftSwitchId.AltChar);
            return (byte) ((state ? 0x80 : 0x00) + _currKey & 0x7f);
        }

        if (address == RD_80VID_)
        {
            var state = _softSwitches.Get(SoftSwitches.SoftSwitchId.Vid80);
            return (byte) ((state ? 0x80 : 0x00) + _currKey & 0x7f);
        }

        if (address >= TAPEOUT_ && address <= END_TAPEOUT_RD_) // TAPEOUT
        {
            // NOT IMPLEMENTED YET
            return 0;
        }

        if (address >= SPKR_ && address <= END_SPKR_RD_) // SPKR
        {
            // NOT IMPLEMENTED YET
            return 0;
        }

        if (address >= STROBE_ && address <= END_STROBE_RD_) // STROBE
        {
            // NOT IMPLEMENTED YET
            return 0;
        }

        if (address == CLRTXT_) // TEXT OFF
        {
            _softSwitches.Set(SoftSwitches.SoftSwitchId.Text, false);
            return 0xA0; // Placeholder value
        }
        if (address == SETTXT_) // TEXT ON
        {
            _softSwitches.Set(SoftSwitches.SoftSwitchId.Text, true);
            return 0xA0; // Placeholder value
        }

        if (address == CLRMIXED_) // MIXED OFF
        {
            _softSwitches.Set(SoftSwitches.SoftSwitchId.Mixed, false);
            return 0xA0; // Placeholder value
        }

        if (address == SETMIXED_) // MIXED ON
        {
            _softSwitches.Set(SoftSwitches.SoftSwitchId.Mixed, true);
            return 0xA0; // Placeholder value
        }

        if (address == CLRPAGE2_) // PAGE2 OFF
        {
            _softSwitches.Set(SoftSwitches.SoftSwitchId.Page2, false);
            return 0xA0; // Placeholder value
        }
        if (address == SETPAGE2_) // PAGE2 ON
        {
            _softSwitches.Set(SoftSwitches.SoftSwitchId.Page2, true);
            return 0xA0; // Placeholder value
        }
        if (address == CLRHIRES_) // HIRES OFF
        {
            _softSwitches.Set(SoftSwitches.SoftSwitchId.HiRes, false);
            return 0xA0; // Placeholder value
        }
        if (address == SETHIRES_) // HIRES ON
        {
            _softSwitches.Set(SoftSwitches.SoftSwitchId.HiRes, true);
            return 0xA0; // Placeholder value
        }

        if (address == CLRAN0_) // ANN0
        {
            _softSwitches.Set(SoftSwitches.SoftSwitchId.An0, false);
            var state = _softSwitches.Get(SoftSwitches.SoftSwitchId.An0);
            return (byte) (state ? 0x80 : 0x00);
        }
        if (address == SETAN0_)
        {
            _softSwitches.Set(SoftSwitches.SoftSwitchId.An0, true);
            var state = _softSwitches.Get(SoftSwitches.SoftSwitchId.An0);
            return (byte) (state ? 0x80 : 0x00);
        }
        if (address == CLRAN1_)  // ANN 1
        {
            _softSwitches.Set(SoftSwitches.SoftSwitchId.An1, false);
            var state = _softSwitches.Get(SoftSwitches.SoftSwitchId.An1);
            return (byte) (state ? 0x80 : 0x00);
        }
        if (address == SETAN1_)
        {
            _softSwitches.Set(SoftSwitches.SoftSwitchId.An1, true);
            var state = _softSwitches.Get(SoftSwitches.SoftSwitchId.An1);
            return (byte) (state ? 0x80 : 0x00);
        }
        if (address == CLRAN2_)  // ANN 2
        {
            _softSwitches.Set(SoftSwitches.SoftSwitchId.An2, false);
            var state = _softSwitches.Get(SoftSwitches.SoftSwitchId.An2);
            return (byte) (state ? 0x80 : 0x00);
        }
        if (address == SETAN2_)
        {
            _softSwitches.Set(SoftSwitches.SoftSwitchId.An2, true);
            var state = _softSwitches.Get(SoftSwitches.SoftSwitchId.An2);
            return (byte) (state ? 0x80 : 0x00);
        }
        if (address == CLRAN3_) // ANN 3  (or DHGR)
        {
            _softSwitches.Set(SoftSwitches.SoftSwitchId.An3, false);
            var state = _softSwitches.Get(SoftSwitches.SoftSwitchId.An3);
            return (byte) (state ? 0x80 : 0x00);
        }
        if (address == SETAN3_)
        {
            _softSwitches.Set(SoftSwitches.SoftSwitchId.An3, true);
            var state = _softSwitches.Get(SoftSwitches.SoftSwitchId.An3);
            return (byte) (state ? 0x80 : 0x00);
        }

        if (address == TAPEIN_) // TAPE IN
        {
            // NOT IMPLEMENTED YET
            return 0x00;
        }

        if (address == BUTTON0_) // Button 0
        {
            return (byte) (_button0 ? 0x80 : 0x00);
        }

        if (address == BUTTON1_) // Button 1
        {
            return (byte) (_button1 ? 0x80 : 0x00);
        }

        if (address == BUTTON2_) // Button 2
        {
            return (byte) (_button2 ? 0x80 : 0x00);
        }

        if (address == PADDLE0_) // Paddle 0
        {
            // NOT IMPLEMENTED YET
            return 0x00;
        }
        if (address == PADDLE1_) // Paddle 1
        {
            // NOT IMPLEMENTED YET
            return 0x00;
        }
        if (address == PADDLE2_) // Paddle 2
        {
            // NOT IMPLEMENTED YET
            return 0x00;
        }
        if (address == PADDLE3_) // Paddle 3
        {
            // NOT IMPLEMENTED YET
            return 0x00;
        }

        if (address == B2_RD_RAM_NO_WRT_ || address == B2_RD_RAM_NO_WRT_ALT_)
        {
            _softSwitches.Set(SoftSwitches.SoftSwitchId.PreWrite, false);
            _softSwitches.Set(SoftSwitches.SoftSwitchId.Bank1, false);
            _softSwitches.Set(SoftSwitches.SoftSwitchId.HighRead, true);
            _softSwitches.Set(SoftSwitches.SoftSwitchId.HighWrite, false);
        }

        if (address == B2_RD_ROM_WRT_RAM_ || address == B2_RD_ROM_WRT_RAM_ALT_)
        {
            _softSwitches.Set(SoftSwitches.SoftSwitchId.Bank1, false);
            SetWriteOrPrewrite();
            _softSwitches.Set(SoftSwitches.SoftSwitchId.HighRead, false);
        }

        if (address == B2_RD_ROM_NO_WRT_ || address == B2_RD_ROM_NO_WRT_ALT_)
        {
            _softSwitches.Set(SoftSwitches.SoftSwitchId.Bank1, false);
            _softSwitches.Set(SoftSwitches.SoftSwitchId.PreWrite, false);
            _softSwitches.Set(SoftSwitches.SoftSwitchId.HighRead, false);
            _softSwitches.Set(SoftSwitches.SoftSwitchId.HighWrite, false);
        }

        if (address == B2_RD_RAM_WRT_RAM_ || address == B2_RD_RAM_WRT_RAM_ALT_)
        {
            _softSwitches.Set(SoftSwitches.SoftSwitchId.Bank1, false);
            SetWriteOrPrewrite();
            _softSwitches.Set(SoftSwitches.SoftSwitchId.HighRead, true);
        }

        if (address == B1_RD_RAM_NO_WRT_ || address == B1_RD_RAM_NO_WRT_ALT_)
        {
            _softSwitches.Set(SoftSwitches.SoftSwitchId.PreWrite, false);
            _softSwitches.Set(SoftSwitches.SoftSwitchId.Bank1, true);
            _softSwitches.Set(SoftSwitches.SoftSwitchId.HighRead, true);
            _softSwitches.Set(SoftSwitches.SoftSwitchId.HighWrite, false);
        }

        if (address == B1_RD_ROM_WRT_RAM_ || address == B1_RD_ROM_WRT_RAM_ALT_)
        {
            _softSwitches.Set(SoftSwitches.SoftSwitchId.Bank1, true);
            SetWriteOrPrewrite();
            _softSwitches.Set(SoftSwitches.SoftSwitchId.HighRead, false);
        }

        if (address == B1_RD_ROM_NO_WRT_ || address == B1_RD_ROM_NO_WRT_ALT_)
        {
            _softSwitches.Set(SoftSwitches.SoftSwitchId.Bank1, true);
            _softSwitches.Set(SoftSwitches.SoftSwitchId.PreWrite, false);
            _softSwitches.Set(SoftSwitches.SoftSwitchId.HighRead, false);
            _softSwitches.Set(SoftSwitches.SoftSwitchId.HighWrite, false);
        }

        if (address == B1_RD_RAM_WRT_RAM_ || address == B1_RD_RAM_WRT_RAM_ALT_)
        {
            _softSwitches.Set(SoftSwitches.SoftSwitchId.Bank1, true);
            SetWriteOrPrewrite();
            _softSwitches.Set(SoftSwitches.SoftSwitchId.HighRead, true);
        }

        // C080 / C084 -> Bank2, WrtCount = 0, Read = true
        // C081 / C085 -> Bank2, WrtCount ++, Read = false
        // C082 / C086 -> Bank2, WrtCount = 0, Read = false;
        // C083 / C087 -> Bank2, WrtCount ++, Raed = true

        // C088 / C08C -> Bank1, WrtCount = 0, Read = true
        // C089 / C08D -> Bank1, WrtCount ++, Read = false
        // C08A / C08E -> Bank1, WrtCount = 0, Read = false
        // C08B / C08F -> Bank1, WrtCount ++, Read = true

        // Todo: Handle other I/O writes here if needed

        return 0x67; // Placehold Value
    }

    private void WriteToIOSpace(ushort address, byte _ /*data*/)
    {
        if (address >= IOSTART && address <= IOEND)
        {
            if (address == SET80STORE_)
            {
                _softSwitches.Set(SoftSwitches.SoftSwitchId.Store80, false);
                //          Debug.WriteLine("80Store Off");
                //Set80Store(false);
                return;
            }
            else if (address == CLR80STORE_)
            {
                _softSwitches.Set(SoftSwitches.SoftSwitchId.Store80, true);
                //             Debug.WriteLine("80Store On");
                //Set80Store(true);
                return;
            }
            else if (address == RDMAINRAM_)
            {
                _softSwitches.Set(SoftSwitches.SoftSwitchId.RamRd, false);

                //SetRamRead(false);
                return;
            }
            else if (address == RDCARDRAM_)
            {
                //SetRamRead(true);
                _softSwitches.Set(SoftSwitches.SoftSwitchId.RamRd, true);

                return;
            }
            else if (address == WRMAINRAM_)
            {
                _softSwitches.Set(SoftSwitches.SoftSwitchId.RamWrt, false);

                //SetRamWrite(false);
                return;
            }
            else if (address == WRCARDRAM_)
            {
                _softSwitches.Set(SoftSwitches.SoftSwitchId.RamWrt, true);

                //SetRamWrite(true);
                return;
            }
            else if (address == SLOTCXROM_)
            {
                _softSwitches.Set(SoftSwitches.SoftSwitchId.IntCxRom, false);

                //SetSlotCxRom();
                return;
            }
            else if (address == INTCXROM_)
            {
                _softSwitches.Set(SoftSwitches.SoftSwitchId.IntCxRom, true);
                //SetIntCxRom();
                return;
            }
            else if (address == STDZP_)
            {
                _softSwitches.Set(SoftSwitches.SoftSwitchId.AltZp, false);

                //   SetAltZp(false);
                return;
            }
            else if (address == ALTZP_)
            {
                _softSwitches.Set(SoftSwitches.SoftSwitchId.AltZp, true);
                //SetAltZp(true);
                return;
            }
            else if (address == INTC3ROM_)
            {
                _softSwitches.Set(SoftSwitches.SoftSwitchId.SlotC3Rom, false);

                //SetSlotC3Rom(false);
                return;
            }
            else if (address == SLOTC3ROM_)
            {
                _softSwitches.Set(SoftSwitches.SoftSwitchId.SlotC3Rom, true);
                //SetSlotC3Rom(true);
                return;
            }
            else if (address == CLR80VID_)
            {
                _softSwitches.Set(SoftSwitches.SoftSwitchId.Vid80, false);

                //SetShow80Col(false);
                return;
            }
            else if (address == SET80VID_)
            {
                _softSwitches.Set(SoftSwitches.SoftSwitchId.Vid80, true);
                //SetShow80Col(true);
                return;
            }
            else if (address == CLRALTCHAR_)
            {
                _softSwitches.Set(SoftSwitches.SoftSwitchId.AltChar, false);
                //    SetAltCharSet(false);
                return;
            }
            else if (address == SETALTCHAR_)
            {
                _softSwitches.Set(SoftSwitches.SoftSwitchId.AltChar, true);
                //SetAltCharSet(true);
                return;
            }

            if (address == KEYSTRB_)
            {
                _currKey &= 0x7f; // Clear high byte;

                //var keyval = auxram.Read(SET80STORE_);
                //ram.Write(SET80STORE_, (byte)(keyval & 0x7F));
                return;
            }

            if (address == CLRTXT_)
            {
                _softSwitches.Set(SoftSwitches.SoftSwitchId.Text, false);

                // SetTextMode(false);
                return;
            }

            if (address == SETTXT_)
            {
                _softSwitches.Set(SoftSwitches.SoftSwitchId.Text, true);

                //SetTextMode(true);
                return;
            }

            if (address == CLRMIXED_)
            {
                _softSwitches.Set(SoftSwitches.SoftSwitchId.Mixed, false);

                //SetMixed(false);
                return;
            }

            if (address == SETMIXED_)
            {
                _softSwitches.Set(SoftSwitches.SoftSwitchId.Mixed, true);

                //SetMixed(true);
                return;
            }

            if (address == CLRPAGE2_)
            {
                //        Debug.WriteLine("Page2 Off");

                _softSwitches.Set(SoftSwitches.SoftSwitchId.Page2, false);

                // SetPage2(false);
                return;
            }

            if (address == SETPAGE2_)
            {
                //             Debug.WriteLine("Page2 On");

                _softSwitches.Set(SoftSwitches.SoftSwitchId.Page2, true);

                //SetPage2(true);
                return;
            }

            if (address == CLRHIRES_)
            {
                _softSwitches.Set(SoftSwitches.SoftSwitchId.HiRes, false);

                //SetHires(false);
                return;
            }

            if (address == SETHIRES_)
            {
                _softSwitches.Set(SoftSwitches.SoftSwitchId.HiRes, true);

                //SetHires(true);
                return;
            }

            if (address == CLRAN0_)
            {
                _softSwitches.Set(SoftSwitches.SoftSwitchId.An0, false);

                //SetAnnunciator(0, false);
            }

            if (address == SETAN0_)
            {
                _softSwitches.Set(SoftSwitches.SoftSwitchId.An0, true);
                //SetAnnunciator(0, true);
            }

            if (address == CLRAN1_)
            {
                _softSwitches.Set(SoftSwitches.SoftSwitchId.An1, false);
                //SetAnnunciator(1, false);
            }

            if (address == SETAN1_)
            {
                _softSwitches.Set(SoftSwitches.SoftSwitchId.An1, true);
                //SetAnnunciator(1, true);
            }

            if (address == CLRAN2_)
            {
                _softSwitches.Set(SoftSwitches.SoftSwitchId.An2, false);
                //SetAnnunciator(2, false);
            }

            if (address == SETAN2_)
            {
                _softSwitches.Set(SoftSwitches.SoftSwitchId.An2, true);
                //SetAnnunciator(2, true);
            }

            if (address == CLRAN3_)
            {
                _softSwitches.Set(SoftSwitches.SoftSwitchId.An3, false);
                //SetAnnunciator(3, false);
            }

            if (address == SETAN3_)
            {
                _softSwitches.Set(SoftSwitches.SoftSwitchId.An3, true);
                //  SetAnnunciator(3, true);
            }

            if (address == B2_RD_RAM_NO_WRT_ || address == B2_RD_RAM_NO_WRT_ALT_)
            {
                _softSwitches.Set(SoftSwitches.SoftSwitchId.PreWrite, false);
                //ClearWrtCount();

                _softSwitches.Set(SoftSwitches.SoftSwitchId.Bank1, false);
                //SetBank1(false);

                _softSwitches.Set(SoftSwitches.SoftSwitchId.HighRead, true);
                //SetHighRead(true);

                _softSwitches.Set(SoftSwitches.SoftSwitchId.HighWrite, false);
                //DisableHighWrite();
            }

            if (address == B2_RD_ROM_WRT_RAM_ || address == B2_RD_ROM_WRT_RAM_)
            {
                _softSwitches.Set(SoftSwitches.SoftSwitchId.Bank1, false);
                //SetBank1(false);

                _softSwitches.Set(SoftSwitches.SoftSwitchId.PreWrite, false);
                //ClearWrtCount();

                _softSwitches.Set(SoftSwitches.SoftSwitchId.HighRead, false);
                //SetHighRead(false);
            }

            if (address == B2_RD_ROM_NO_WRT_ || address == B2_RD_ROM_NO_WRT_ALT_)
            {
                _softSwitches.Set(SoftSwitches.SoftSwitchId.Bank1, false);
                //SetBank1(false);

                _softSwitches.Set(SoftSwitches.SoftSwitchId.PreWrite, false);
                //ClearWrtCount();

                _softSwitches.Set(SoftSwitches.SoftSwitchId.HighRead, false);
                //SetHighRead(false);

                _softSwitches.Set(SoftSwitches.SoftSwitchId.HighWrite, false);
                //DisableHighWrite();
            }

            if (address == B2_RD_RAM_WRT_RAM_ || address == B2_RD_RAM_WRT_RAM_ALT_)
            {
                _softSwitches.Set(SoftSwitches.SoftSwitchId.Bank1, false);
                //SetBank1(false);

                _softSwitches.Set(SoftSwitches.SoftSwitchId.PreWrite, false);
                //ClearWrtCount();

                _softSwitches.Set(SoftSwitches.SoftSwitchId.HighRead, true);
                //SetHighRead(true);
            }

            if (address == B1_RD_RAM_NO_WRT_ || address == B1_RD_RAM_NO_WRT_ALT_)
            {
                _softSwitches.Set(SoftSwitches.SoftSwitchId.PreWrite, false);
                //ClearWrtCount();

                _softSwitches.Set(SoftSwitches.SoftSwitchId.Bank1, true);
                //SetBank1(true);

                _softSwitches.Set(SoftSwitches.SoftSwitchId.HighRead, true);
                //SetHighRead(true);

                _softSwitches.Set(SoftSwitches.SoftSwitchId.HighWrite, false);
                //DisableHighWrite();
            }

            if (address == B1_RD_ROM_WRT_RAM_ || address == B1_RD_ROM_WRT_RAM_ALT_)
            {
                _softSwitches.Set(SoftSwitches.SoftSwitchId.Bank1, true);
                //SetBank1(true);

                _softSwitches.Set(SoftSwitches.SoftSwitchId.PreWrite, false);
                //ClearWrtCount();

                _softSwitches.Set(SoftSwitches.SoftSwitchId.HighRead, false);
                //SetHighRead(false);
            }

            if (address == B1_RD_ROM_NO_WRT_ || address == B1_RD_ROM_NO_WRT_ALT_)
            {
                _softSwitches.Set(SoftSwitches.SoftSwitchId.Bank1, true);
                //SetBank1(true);

                _softSwitches.Set(SoftSwitches.SoftSwitchId.PreWrite, false);
                //ClearWrtCount();

                _softSwitches.Set(SoftSwitches.SoftSwitchId.HighRead, false);
                //SetHighRead(false);

                _softSwitches.Set(SoftSwitches.SoftSwitchId.HighWrite, false);
                //DisableHighWrite();
            }

            if (address == B1_RD_RAM_WRT_RAM_ || address == B1_RD_RAM_WRT_RAM_ALT_)
            {
                _softSwitches.Set(SoftSwitches.SoftSwitchId.Bank1, true);
                //SetBank1(true);

                _softSwitches.Set(SoftSwitches.SoftSwitchId.PreWrite, false);
                //ClearWrtCount();

                _softSwitches.Set(SoftSwitches.SoftSwitchId.HighRead, true);
                //SetHighRead(true);
            }

            // C080 / C084 -> Bank2, WrtCount = 0, Read = true
            // C081 / C085 -> Bank2, WrtCount = 0, Read = false
            // C082 / C086 -> Bank2, WrtCount = 0, Read = false;
            // C083 / C087 -> Bank2, WrtCount = 0, Read = true

            // C088 / C08C -> Bank1, WrtCount = 0, Read = true
            // C089 / C08D -> Bank1, WrtCount = 0, Read = false
            // C08A / C08E -> Bank1, WrtCount = 0, Read = false
            // C08B / C08F -> Bank1, WrtCount = 0, Read = true
        }

        // Todo: Handle C100-CFFF I/O writes here if needed
    }

    public byte CpuRead(ushort address, bool readOnly = false)
    {
        ThrowIfDisposed();

        if (address >= IOSTART && address <= IOEND)
        {
            return ReadFromIOSpace(address);
        }
        else
        {
            //if (address >= 0xC100 && address < 0xD000)
            //{
            //    Debug.WriteLine($"Read from {address:X4}");
            //}

            return _memoryPool.Read(address);
        }
    }

    public void CpuWrite(ushort address, byte data)
    {
        ThrowIfDisposed();

        if (address >= IOSTART && address <= IOEND)
        {
            WriteToIOSpace(address, data);
            return;
        }

        //if (address == 0x400)
        //{
        //    Debug.WriteLine($"Write to 0400: {data:X2}");
        //}

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
                var lineNum = CpuRead(0x75) + (CpuRead(0x76) * 256);
                string ln = lineNum < 0xFA00 ? lineNum.ToString() : "IMM";
                var sp = _cpu!.SP;
                var spcs = 0xFF - sp;
                hook(-1, lineNum, spcs);
            }

            #region LegacyTrace_DoNotRemove

            // (Preserved commented instrumentation)

            #endregion LegacyTrace_DoNotRemove
        }
        lastPc = currPc;

        _cpu!.Clock();
        _systemClock++;

        if (_systemClock >= _nextVblankCycle)
        {
            // Catch up if emulator ran fast (unthrottled batches)
            do
            { _nextVblankCycle += CyclesPerVBlank; } while (_systemClock >= _nextVblankCycle);
            if (!_disposed)
            {
                VBlank?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public void Reset()
    {
        ThrowIfDisposed();
        _softSwitches.ResetAllSwitches();
        //Set80Store(false);
        //SetRamRead(false);
        //SetRamWrite(false);
        //SetIntCxRom();
        //SetAltZp(false);
        //SetSlotC3Rom(false);
        //SetShow80Col(false); // Vid80
        //SetAltCharSet(false);
        //SetTextMode(true);
        //SetMixed(false);
        //SetPage2(false);
        //SetHires(false);
        //SetAnnunciator(0, false);
        //SetAnnunciator(1, false);
        //SetAnnunciator(2, false);
        //SetAnnunciator(3, false);
        //SetBank1(false);
        //SetHighRead(false);
        //DisableHighWrite();
        // No PreWrite
        //ClearWrtCount();

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