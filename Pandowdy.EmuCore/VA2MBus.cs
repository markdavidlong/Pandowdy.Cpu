using System.Diagnostics;
using Emulator;
using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore;

/// <summary>
/// VA2M-specific system bus that coordinates CPU, memory, and I/O operations for Apple IIe emulation.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Responsibilities:</strong>
/// <list type="bullet">
/// <item>CPU connection and cycle execution</item>
/// <item>Memory read/write routing to MemoryPool</item>
/// <item>I/O space address decoding ($C000-$CFFF)</item>
/// <item>Soft switch management and coordination</item>
/// <item>Language card banking logic</item>
/// <item>Keyboard input handling</item>
/// <item>Game controller pushbutton management</item>
/// <item>VBlank timing and event generation (~60 Hz)</item>
/// </list>
/// </para>
/// <para>
/// <strong>Threading Model:</strong> All public methods (Connect, CpuRead/Write, Clock, Reset)
/// must be called ONLY from the emulator worker thread. The VBlank event is raised on the
/// emulator thread; UI subscribers MUST marshal to UI thread via dispatcher.
/// </para>
/// <para>
/// <strong>VBlank Timing:</strong> Emits VBlank event every 17,063 cycles (~60 Hz at 1.023 MHz),
/// matching the Apple IIe NTSC vertical blanking interval. The VBlank blackout period lasts
/// 4,550 cycles during which RD_VERTBLANK_ ($C019) reads as $80.
/// </para>
/// <para>
/// <strong>Disposal:</strong> After disposal, no further events are raised and Clock() becomes
/// a no-op. Proper disposal prevents event handler leaks and ensures clean shutdown.
/// </para>
/// <para>
/// <strong>⚠️ PLANNED FOR REFACTORING:</strong> See Pandowdy.EmuCore/_docs_/VA2MBus-Refactoring-Notes.md
/// for planned separation of concerns (slot bus system, floating bus emulation, etc.).
/// </para>
/// </remarks>
public sealed class VA2MBus : IAppleIIBus, IDisposable
{
    /// <summary>
    /// Memory pool managing the 128KB Apple IIe memory space.
    /// </summary>
    private readonly MemoryPool _memoryPool;
    
    /// <summary>
    /// CPU instance (6502 emulator) connected to this bus.
    /// </summary>
    private readonly ICpu _cpu;
    
    /// <summary>
    /// Gets the CPU instance connected to this bus.
    /// </summary>
    public ICpu Cpu { get => _cpu; }

  //  private int lastPc = 0;
  //  private AppleSoftHookTable? _hookTable;
  
    /// <summary>
    /// System clock counter tracking total CPU cycles executed since reset.
    /// </summary>
    private ulong _systemClock;
    
    /// <summary>
    /// Soft switches managing memory mapping, video modes, ROM selection, and annunciators.
    /// </summary>
    private SoftSwitches _softSwitches = new();
//    private bool _isInVBlankBlackout = false;

    /// <summary>
    /// Gets the RAM (MemoryPool) for direct memory access.
    /// </summary>
    /// <remarks>
    /// Primarily used for testing and direct memory inspection. Normal CPU access
    /// should go through CpuRead/CpuWrite which handle I/O space routing.
    /// </remarks>
    public IMemory RAM => _memoryPool;

    /// <summary>
    /// Gets the total number of CPU cycles executed since last reset.
    /// </summary>
    public ulong SystemClockCounter => _systemClock;
    
    /// <summary>
    /// Flag indicating whether this bus has been disposed.
    /// </summary>
    private bool _disposed;
    
    /// <summary>
    /// Current keyboard latch value (appears at $C000, bit 7 indicates key available).
    /// </summary>
    private byte _currKey = 0x00;
    
    /// <summary>
    /// Pushbutton 0 state (readable at $C061, bit 7 set when pressed).
    /// </summary>
    private bool _button0 = false;
    
    /// <summary>
    /// Pushbutton 1 state (readable at $C062, bit 7 set when pressed).
    /// </summary>
    private bool _button1 = false;
    
    /// <summary>
    /// Pushbutton 2 state (readable at $C063, bit 7 set when pressed).
    /// </summary>
    private bool _button2 = false;

    /// <summary>
    /// Number of CPU cycles between VBlank events (17,063 = 1,023,000 Hz / 60 Hz).
    /// </summary>
    /// <remarks>
    /// This value matches the Apple IIe NTSC specification. For PAL systems,
    /// this would be different (~20,460 cycles for 50 Hz).
    /// </remarks>
    private const ulong CyclesPerVBlank = 17063;

    /// <summary>
    /// Number of cycles during which the vertical blanking flag (RD_VERTBLANK_) reads as $80.
    /// </summary>
    /// <remarks>
    /// The VBlank blackout period is 4,550 cycles during which the video scanner is not
    /// drawing visible scanlines. Software can use this period for graphics updates without
    /// causing visual artifacts.
    /// </remarks>
    private const int VBlankBlackoutCycles = 4550;
    
    /// <summary>
    /// Cycle count at which the next VBlank event will fire.
    /// </summary>
    private ulong _nextVblankCycle = CyclesPerVBlank;
    
    /// <summary>
    /// Countdown timer for VBlank blackout period (decremented each cycle).
    /// </summary>
    /// <remarks>
    /// When > 0, RD_VERTBLANK_ ($C019) reads as $80 (in VBlank).
    /// When ≤ 0, RD_VERTBLANK_ reads as $00 (visible scanlines).
    /// </remarks>
    private long _VblankBlackoutCounter = VBlankBlackoutCycles;

    /// <summary>
    /// Event raised every ~60 Hz (17,063 cycles) to signal vertical blanking interval.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Timing:</strong> Fires at 1,023,000 Hz / 17,063 cycles ≈ 60.02 Hz (NTSC).
    /// </para>
    /// <para>
    /// <strong>Thread Context:</strong> Always raised on the emulator thread. UI subscribers
    /// must marshal to UI thread via Dispatcher.BeginInvoke or similar.
    /// </para>
    /// <para>
    /// <strong>Use Cases:</strong>
    /// <list type="bullet">
    /// <item>Frame rendering synchronization</item>
    /// <item>Cursor blink timing</item>
    /// <item>Status snapshot generation</item>
    /// <item>Audio buffer flushing</item>
    /// </list>
    /// </para>
    /// </remarks>
    public event EventHandler? VBlank;


    /// <summary>
    /// Dictionary mapping I/O addresses to read handler functions.
    /// </summary>
    /// <remarks>
    /// Handlers return the byte value to be read from the I/O address. Many handlers
    /// also have side effects (toggling soft switches, clearing keyboard latch, etc.).
    /// </remarks>
    private readonly System.Collections.Generic.Dictionary<ushort, System.Func<byte>> _ioReadHandlers = [];
    
    /// <summary>
    /// Dictionary mapping I/O addresses to write handler actions.
    /// </summary>
    /// <remarks>
    /// Handlers receive the byte value written to the I/O address. Most handlers
    /// ignore the data value and simply toggle soft switches based on the address.
    /// </remarks>
    private readonly System.Collections.Generic.Dictionary<ushort, System.Action<byte>> _ioWriteHandlers = [];


    /// <summary>Start of Apple IIe I/O address space ($C000).</summary>
    public const ushort IO_AREA_START = 0xC000;
    /// <summary>End of Apple IIe I/O address space ($CFFF).</summary>
    public const ushort IO_AREA_END = 0xCFFF;
    /// <summary>Start of system I/O space ($C000).</summary>
    public const ushort SYSTEM_IO_START = 0xC000;
    /// <summary>End of system I/O space ($C0FF).</summary>
    public const ushort SYSTEM_UI_END = 0xC0FF;

    /// <summary>Keyboard data register (read: returns last key + high bit if available) ($C000).</summary>
    public const ushort KBD_ = 0xC000;
    /// <summary>Set 80STORE switch off ($C000 write).</summary>
    public const ushort SET80STORE_ = 0xC000;
    /// <summary>Set 80STORE switch on ($C001 write).</summary>
    public const ushort CLR80STORE_ = 0xC001;
    /// <summary>Read from main RAM ($C002 write).</summary>
    public const ushort RDMAINRAM_ = 0xC002;
    /// <summary>Read from auxiliary (card) RAM ($C003 write).</summary>
    public const ushort RDCARDRAM_ = 0xC003;
    /// <summary>Write to main RAM ($C004 write).</summary>
    public const ushort WRMAINRAM_ = 0xC004;
    /// <summary>Write to auxiliary (card) RAM ($C005 write).</summary>
    public const ushort WRCARDRAM_ = 0xC005;
    /// <summary>Use slot ROMs ($C006 write).</summary>
    public const ushort SLOTCXROM_ = 0xC006;
    /// <summary>Use internal ROMs ($C007 write).</summary>
    public const ushort INTCXROM_ = 0xC007;
    /// <summary>Use main zero page/stack ($C008 write).</summary>
    public const ushort STDZP_ = 0xC008;
    /// <summary>Use auxiliary zero page/stack ($C009 write).</summary>
    public const ushort ALTZP_ = 0xC009;
    /// <summary>Use internal slot 3 ROM ($C00A write).</summary>
    public const ushort INTC3ROM_ = 0xC00A;
    /// <summary>Use slot 3 ROM ($C00B write).</summary>
    public const ushort SLOTC3ROM_ = 0xC00B;
    /// <summary>Turn off 80-column display ($C00C write).</summary>
    public const ushort CLR80VID_ = 0xC00C;
    /// <summary>Turn on 80-column display ($C00D write).</summary>
    public const ushort SET80VID_ = 0xC00D;
    /// <summary>Use primary character set ($C00E write).</summary>
    public const ushort CLRALTCHAR_ = 0xC00E;
    /// <summary>Use alternate character set ($C00F write).</summary>
    public const ushort SETALTCHAR_ = 0xC00F;

    /// <summary>Keyboard strobe (read: clears keyboard high bit) ($C010).</summary>
    public const ushort KEYSTRB_ = 0xC010;
    /// <summary>Read language card bank 1 status ($C011).</summary>
    public const ushort RD_LC_BANK1_ = 0xC011;
    /// <summary>Read language card RAM read status ($C012).</summary>
    public const ushort RD_LC_RAM = 0xC012;
    /// <summary>Read RAMRD status ($C013).</summary>
    public const ushort RD_RAMRD_ = 0xC013;
    /// <summary>Read RAMWRT status ($C014).</summary>
    public const ushort RD_RAMWRT_ = 0xC014;
    /// <summary>Read INTCXROM status ($C015).</summary>
    public const ushort RD_INTCXROM_ = 0xC015;
    /// <summary>Read ALTZP status ($C016).</summary>
    public const ushort RD_ALTZP_ = 0xC016;
    /// <summary>Read SLOTC3ROM status ($C017).</summary>
    public const ushort RD_SLOTC3ROM_ = 0xC017;
    /// <summary>Read 80STORE status ($C018).</summary>
    public const ushort RD_80STORE_ = 0xC018;
    /// <summary>Read vertical blanking status (bit 7 = 1 during VBlank) ($C019).</summary>
    public const ushort RD_VERTBLANK_ = 0xC019;
    /// <summary>Read TEXT mode status ($C01A).</summary>
    public const ushort RD_TEXT_ = 0xC01A;
    /// <summary>Read MIXED mode status ($C01B).</summary>
    public const ushort RD_MIXED_ = 0xC01B;
    /// <summary>Read PAGE2 status ($C01C).</summary>
    public const ushort RD_PAGE2_ = 0xC01C;
    /// <summary>Read HIRES status ($C01D).</summary>
    public const ushort RD_HIRES_ = 0xC01D;
    /// <summary>Read ALTCHAR status ($C01E).</summary>
    public const ushort RD_ALTCHAR_ = 0xC01E;
    /// <summary>Read 80VID status ($C01F).</summary>
    public const ushort RD_80VID_ = 0xC01F;

    /// <summary>Cassette tape output (read/write range start) ($C020).</summary>
    public const ushort TAPEOUT_ = 0xC020;
    /// <summary>Cassette tape output (read range end) ($C02F).</summary>
    public const ushort END_TAPEOUT_RD_ = 0xC02F;
    /// <summary>Speaker toggle (read/write range start) ($C030).</summary>
    public const ushort SPKR_ = 0xC030;
    /// <summary>Speaker toggle (read range end) ($C03F).</summary>
    public const ushort END_SPKR_RD_ = 0xC03F;
    /// <summary>Game controller strobe (read/write range start) ($C040).</summary>
    public const ushort STROBE_ = 0xC040;
    /// <summary>Game controller strobe (read range end) ($C04F).</summary>
    public const ushort END_STROBE_RD_ = 0xC04F;

    /// <summary>Clear TEXT mode (read/write) ($C050).</summary>
    public const ushort CLRTXT_ = 0xC050;
    /// <summary>Set TEXT mode (read/write) ($C051).</summary>
    public const ushort SETTXT_ = 0xC051;
    /// <summary>Clear MIXED mode (read/write) ($C052).</summary>
    public const ushort CLRMIXED_ = 0xC052;
    /// <summary>Set MIXED mode (read/write) ($C053).</summary>
    public const ushort SETMIXED_ = 0xC053;
    /// <summary>Display page 1 (read/write) ($C054).</summary>
    public const ushort CLRPAGE2_ = 0xC054;
    /// <summary>Display page 2 (read/write) ($C055).</summary>
    public const ushort SETPAGE2_ = 0xC055;
    /// <summary>Select low-res graphics (read/write) ($C056).</summary>
    public const ushort CLRHIRES_ = 0xC056;
    /// <summary>Select hi-res graphics (read/write) ($C057).</summary>
    public const ushort SETHIRES_ = 0xC057;
    /// <summary>Clear annunciator 0 (read/write) ($C058).</summary>
    public const ushort CLRAN0_ = 0xC058;
    /// <summary>Set annunciator 0 (read/write) ($C059).</summary>
    public const ushort SETAN0_ = 0xC059;
    /// <summary>Clear annunciator 1 (read/write) ($C05A).</summary>
    public const ushort CLRAN1_ = 0xC05A;
    /// <summary>Set annunciator 1 (read/write) ($C05B).</summary>
    public const ushort SETAN1_ = 0xC05B;
    /// <summary>Clear annunciator 2 (read/write) ($C05C).</summary>
    public const ushort CLRAN2_ = 0xC05C;
    /// <summary>Set annunciator 2 (read/write) ($C05D).</summary>
    public const ushort SETAN2_ = 0xC05D;
    /// <summary>Clear annunciator 3 (read/write) ($C05E).</summary>
    public const ushort CLRAN3_ = 0xC05E;
    /// <summary>Set annunciator 3 (read/write) ($C05F).</summary>
    public const ushort SETAN3_ = 0xC05F;

    /// <summary>Cassette tape input (read) ($C060).</summary>
    public const ushort TAPEIN_ = 0xC060;
    /// <summary>Pushbutton 0 status (read, bit 7 = pressed) ($C061).</summary>
    public const ushort BUTTON0_ = 0xC061;
    /// <summary>Pushbutton 1 status (read, bit 7 = pressed) ($C062).</summary>
    public const ushort BUTTON1_ = 0xC062;
    /// <summary>Pushbutton 2 status (read, bit 7 = pressed) ($C063).</summary>
    public const ushort BUTTON2_ = 0xC063;
    /// <summary>Paddle 0 analog value (read) ($C064).</summary>
    public const ushort PADDLE0_ = 0xC064;
    /// <summary>Paddle 1 analog value (read) ($C065).</summary>
    public const ushort PADDLE1_ = 0xC065;
    /// <summary>Paddle 2 analog value (read) ($C066).</summary>
    public const ushort PADDLE2_ = 0xC066;
    /// <summary>Paddle 3 analog value (read) ($C067).</summary>
    public const ushort PADDLE3_ = 0xC067;

    /// <summary>Paddle timer trigger (read/write) ($C070).</summary>
    public const ushort PTRIG_ = 0xC070;
    /// <summary>Read IOU disable status ($C07E).</summary>
    public const ushort RD_IOUDISABLE_ = 0xC07E;
    /// <summary>Disable IOU (write) ($C07E).</summary>
    public const ushort IOUDISABLE_ = 0xC07E;
    /// <summary>Enable IOU (write) ($C07F).</summary>
    public const ushort IOUENABLE_ = 0xC07F;

    // Language Card Banking ($C080-$C08F)
    // Bank 2 addresses ($C080-$C087)
    /// <summary>Bank 2: Read RAM, no write ($C080).</summary>
    public const ushort B2_RD_RAM_NO_WRT_ = 0xC080;
    /// <summary>Bank 2: Read RAM, no write (alternate) ($C084).</summary>
    public const ushort B2_RD_RAM_NO_WRT_ALT_ = 0xC084;
    /// <summary>Bank 2: Read ROM, write RAM ($C081).</summary>
    public const ushort B2_RD_ROM_WRT_RAM_ = 0xC081;
    /// <summary>Bank 2: Read ROM, write RAM (alternate) ($C085).</summary>
    public const ushort B2_RD_ROM_WRT_RAM_ALT_ = 0xC085;
    /// <summary>Bank 2: Read ROM, no write ($C082).</summary>
    public const ushort B2_RD_ROM_NO_WRT_ = 0xC082;
    /// <summary>Bank 2: Read ROM, no write (alternate) ($C086).</summary>
    public const ushort B2_RD_ROM_NO_WRT_ALT_ = 0xC086;
    /// <summary>Bank 2: Read RAM, write RAM ($C083).</summary>
    public const ushort B2_RD_RAM_WRT_RAM_ = 0xC083;
    /// <summary>Bank 2: Read RAM, write RAM (alternate) ($C087).</summary>
    public const ushort B2_RD_RAM_WRT_RAM_ALT_ = 0xC087;

    // Bank 1 addresses ($C088-$C08F)
    /// <summary>Bank 1: Read RAM, no write ($C088).</summary>
    public const ushort B1_RD_RAM_NO_WRT_ = 0xC088;
    /// <summary>Bank 1: Read RAM, no write (alternate) ($C08C).</summary>
    public const ushort B1_RD_RAM_NO_WRT_ALT_ = 0xC08C;
    /// <summary>Bank 1: Read ROM, write RAM ($C089).</summary>
    public const ushort B1_RD_ROM_WRT_RAM_ = 0xC089;
    /// <summary>Bank 1: Read ROM, write RAM (alternate) ($C08D).</summary>
    public const ushort B1_RD_ROM_WRT_RAM_ALT_ = 0xC08D;
    /// <summary>Bank 1: Read ROM, no write ($C08A).</summary>
    public const ushort B1_RD_ROM_NO_WRT_ = 0xC08A;
    /// <summary>Bank 1: Read ROM, no write (alternate) ($C08E).</summary>
    public const ushort B1_RD_ROM_NO_WRT_ALT_ = 0xC08E;
    /// <summary>Bank 1: Read RAM, write RAM ($C08B).</summary>
    public const ushort B1_RD_RAM_WRT_RAM_ = 0xC08B;
    /// <summary>Bank 1: Read RAM, write RAM (alternate) ($C08F).</summary>
    public const ushort B1_RD_RAM_WRT_RAM_ALT_ = 0xC08F;

    // Expansion Slot I/O Space ($C090-$C0FF)
    /// <summary>Slot 1 I/O space start ($C090).</summary>
    public const ushort SLOT1_IO_SPACE = 0xC090;
    /// <summary>Slot 1 I/O space end ($C09F).</summary>
    public const ushort SLOT1_IO_SPACE_END = 0xC09F;
    /// <summary>Slot 2 I/O space start ($C0A0).</summary>
    public const ushort SLOT2_IO_SPACE = 0xC0A0;
    /// <summary>Slot 2 I/O space end ($C0AF).</summary>
    public const ushort SLOT2_IO_SPACE_END = 0xC0AF;
    /// <summary>Slot 3 I/O space start ($C0B0).</summary>
    public const ushort SLOT3_IO_SPACE = 0xC0B0;
    /// <summary>Slot 3 I/O space end ($C0BF).</summary>
    public const ushort SLOT3_IO_SPACE_END = 0xC0BF;
    /// <summary>Slot 4 I/O space start ($C0C0).</summary>
    public const ushort SLOT4_IO_SPACE = 0xC0C0;
    /// <summary>Slot 4 I/O space end ($C0CF).</summary>
    public const ushort SLOT4_IO_SPACE_END = 0xC0CF;
    /// <summary>Slot 5 I/O space start ($C0D0).</summary>
    public const ushort SLOT5_IO_SPACE = 0xC0D0;
    /// <summary>Slot 5 I/O space end ($C0DF).</summary>
    public const ushort SLOT5_IO_SPACE_END = 0xC0DF;
    /// <summary>Slot 6 I/O space start ($C0E0).</summary>
    public const ushort SLOT6_IO_SPACE = 0xC0E0;
    /// <summary>Slot 6 I/O space end ($C0EF).</summary>
    public const ushort SLOT6_IO_SPACE_END = 0xC0EF;
    /// <summary>Slot 7 I/O space start ($C0F0).</summary>
    public const ushort SLOT7_IO_SPACE = 0xC0F0;
    /// <summary>Slot 7 I/O space end ($C0FF).</summary>
    public const ushort SLOT7_IO_SPACE_END = 0xC0FF;
    
    /// <summary>
    /// Gets the soft switches managing memory mapping, video modes, ROM selection, and annunciators.
    /// </summary>
    /// <remarks>
    /// Provides access to the soft switch collection for querying states, registering responders,
    /// and debugging. The MemoryPool and SystemStatusProvider are automatically registered as
    /// responders during construction.
    /// </remarks>
    public SoftSwitches Switches => _softSwitches;

    /// <summary>
    /// Initializes a new instance of the VA2MBus class.
    /// </summary>
    /// <param name="mempool">Memory pool managing 128KB Apple IIe memory space.</param>
    /// <param name="responder">System status provider implementing ISoftSwitchResponder for status updates.</param>
    /// <param name="cpu">CPU instance (6502 emulator) to connect to this bus.</param>
    /// <exception cref="ArgumentNullException">Thrown if any parameter is null.</exception>
    /// <remarks>
    /// <para>
    /// <strong>Initialization Sequence:</strong>
    /// <list type="number">
    /// <item>Store dependency references</item>
    /// <item>Initialize I/O read handler dictionary (InitIoReadHandlers)</item>
    /// <item>Initialize I/O write handler dictionary (InitIoWriteHandlers)</item>
    /// <item>Register MemoryPool as soft switch responder (for memory remapping)</item>
    /// <item>Register SystemStatusProvider as soft switch responder (for UI updates)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>I/O Handler Dictionaries:</strong> The bus maintains two dictionaries mapping I/O
    /// addresses to handler functions. This provides efficient dispatch and clear organization
    /// of I/O space behavior.
    /// </para>
    /// </remarks>
    public VA2MBus(MemoryPool mempool, ISystemStatusProvider responder, ICpu cpu)
    {
        ArgumentNullException.ThrowIfNull(responder);
        ArgumentNullException.ThrowIfNull(responder);
        ArgumentNullException.ThrowIfNull(cpu);
        _memoryPool = mempool;
        _cpu = cpu;
        InitIoReadHandlers();
        InitIoWriteHandlers();

        // Always add the MemoryPool as a responder (it needs to update memory mappings)
        _softSwitches.AddResponder(mempool);

        if (responder is ISoftSwitchResponder softSwitchResponder)
        {
            _softSwitches.AddResponder(softSwitchResponder);
        }
    }

    /// <summary>
    /// Initializes I/O read handler dictionary with functions for all readable I/O addresses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Handler Categories:</strong>
    /// <list type="bullet">
    /// <item><strong>Simple reads:</strong> Return composed values (keyboard latch, soft switch states)</item>
    /// <item><strong>Side-effect reads:</strong> Toggle soft switches when read (TEXT, MIXED, PAGE2, HIRES, annunciators)</item>
    /// <item><strong>Language card reads:</strong> Complex banking logic with two-access write sequence</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Apple II Behavior:</strong> Many I/O addresses have side effects when read. For example,
    /// reading $C050 (CLRTXT_) turns off text mode, and reading $C051 (SETTXT_) turns it on.
    /// This is standard Apple II behavior, not a bug.
    /// </para>
    /// <para>
    /// <strong>Return Values:</strong> Most handlers return either the keyboard latch value, a soft
    /// switch state encoded in bit 7, or $A0 (arbitrary floating bus value).
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// Applies language card banking flags when reading from language card I/O addresses.
    /// </summary>
    /// <param name="bank1">True to select bank 1 ($D000-$DFFF), false for bank 2.</param>
    /// <param name="address">Specific language card I/O address ($C080-$C08F) being accessed.</param>
    /// <remarks>
    /// <para>
    /// <strong>Language Card Banking:</strong> The Apple IIe language card allows RAM to be mapped
    /// into the $D000-$FFFF space normally occupied by ROM. Two banks are available ($D000-$DFFF) only,
    /// and reading/writing can be independently controlled.
    /// </para>
    /// <para>
    /// <strong>Two-Access Write Sequence:</strong> To enable writing to language card RAM, two
    /// consecutive accesses to certain addresses are required. The first access sets PreWrite,
    /// the second sets HighWrite. This prevents accidental writes.
    /// </para>
    /// <para>
    /// <strong>Address Patterns:</strong>
    /// <list type="bullet">
    /// <item>Addresses ending in 0, 4, 8, C: Read RAM, no write</item>
    /// <item>Addresses ending in 1, 5, 9, D: Read ROM, write RAM (after two accesses)</item>
    /// <item>Addresses ending in 2, 6, A, E: Read ROM, no write</item>
    /// <item>Addresses ending in 3, 7, B, F: Read RAM, write RAM (after two accesses)</item>
    /// </list>
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// Initializes I/O write handler dictionary with functions for all writable I/O addresses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Write Behavior:</strong> Most I/O write handlers ignore the data value and simply
    /// toggle soft switches based on the address. This matches Apple II hardware behavior where
    /// the address strobe, not the data, controls the logic.
    /// </para>
    /// <para>
    /// <strong>Handler Categories:</strong>
    /// <list type="bullet">
    /// <item><strong>Soft switch writes:</strong> Set/clear memory mapping and video mode switches</item>
    /// <item><strong>Language card writes:</strong> Banking control with different semantics than reads</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Write vs Read Semantics:</strong> Language card write handlers have different behavior
    /// than read handlers. Specifically, the two-access write sequence is reset on writes to certain
    /// addresses (see <see cref="ApplyBankIoWriteFlags"/>).
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// Applies language card banking flags when writing to language card I/O addresses.
    /// </summary>
    /// <param name="bank1">True to select bank 1 ($D000-$DFFF), false for bank 2.</param>
    /// <param name="address">Specific language card I/O address ($C080-$C08F) being accessed.</param>
    /// <remarks>
    /// <para>
    /// <strong>Write Semantics:</strong> Language card write handlers have slightly different behavior
    /// than read handlers. The two-access write sequence is reset by certain writes, preventing
    /// accidental write enable.
    /// </para>
    /// <para>
    /// <strong>Key Difference:</strong> Writes to addresses ending in 1, 5, 9, D immediately clear
    /// PreWrite, unlike reads which set it. This asymmetry is part of the Apple IIe design.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// Legacy CPU connection method. Not supported in VA2MBus.
    /// </summary>
    /// <param name="_">CPU instance (ignored).</param>
    /// <exception cref="NotSupportedException">Always thrown. VA2MBus requires CPU in constructor.</exception>
    /// <remarks>
    /// VA2MBus uses constructor injection for the CPU connection. The Connect method is
    /// deprecated and should not be called. This method exists only for IAppleIIBus interface
    /// compatibility.
    /// </remarks>
    public void Connect(Emulator.CPU _)
    {
        throw new NotSupportedException("This should not be called. Connect is deprecated.");
    }

    /// <summary>
    /// Sets the keyboard latch value as if a key was pressed.
    /// </summary>
    /// <param name="key">ASCII key value with bit 7 set (indicating key available).</param>
    /// <remarks>
    /// <para>
    /// <strong>Keyboard Latch:</strong> The Apple IIe keyboard latch appears at $C000. When a key
    /// is pressed, the ASCII value with bit 7 set appears at this address. Software reads $C010
    /// (KEYSTRB) to clear bit 7, indicating the key has been read.
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> Should be called only from the emulator thread. External
    /// threads should enqueue via VA2M.InjectKey().
    /// </para>
    /// </remarks>
    public void SetKeyValue(byte key)
    {
        _currKey = key;
    }

    /// <summary>
    /// Gets the current state of a pushbutton.
    /// </summary>
    /// <param name="num">Button number (0-2).</param>
    /// <returns>True if button is pressed, false if released or invalid button number.</returns>
    /// <remarks>
    /// Button states are readable at I/O addresses $C061-$C063 with bit 7 set when pressed.
    /// </remarks>
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

    /// <summary>
    /// Sets the state of a pushbutton.
    /// </summary>
    /// <param name="num">Button number (0-2).</param>
    /// <param name="pressed">True if button is pressed, false if released.</param>
    /// <remarks>
    /// <para>
    /// <strong>Thread Safety:</strong> Should be called only from the emulator thread. External
    /// threads should enqueue via VA2M.SetPushButton().
    /// </para>
    /// <para>
    /// Invalid button numbers are silently ignored.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// Handles the two-access write sequence for language card write enable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Two-Access Sequence:</strong> To enable writing to language card RAM, software must
    /// access certain language card addresses twice in succession. The first access sets PreWrite,
    /// the second sets HighWrite.
    /// </para>
    /// <para>
    /// <strong>Implementation:</strong>
    /// <list type="bullet">
    /// <item>If PreWrite is not set: Set PreWrite and return (first access)</item>
    /// <item>If PreWrite is already set: Set HighWrite (second access, write enabled)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Reset Condition:</strong> Accessing other language card addresses resets PreWrite,
    /// requiring the sequence to start over.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// Builds a byte value with a soft switch state in bit 7.
    /// </summary>
    /// <param name="state">Soft switch state (true = bit 7 set, false = bit 7 clear).</param>
    /// <param name="other">Base byte value (typically keyboard latch), bits 0-6 preserved.</param>
    /// <returns>Byte with bit 7 set according to <paramref name="state"/>, bits 0-6 from <paramref name="other"/>.</returns>
    /// <remarks>
    /// Apple IIe soft switch status reads typically return the keyboard latch value (bits 0-6)
    /// with the switch state encoded in bit 7.
    /// </remarks>
    private static byte BuildHiBitVal(bool state, byte other = 0x00)
    {
        return (byte) ((state ? 0x80 : 0x00) | (other & 0x7f));
    }

    /// <summary>
    /// Reads a byte from I/O space ($C000-$C0FF) by dispatching to the appropriate handler.
    /// </summary>
    /// <param name="address">I/O address to read from ($C000-$C0FF).</param>
    /// <returns>Byte value returned by the I/O handler, or $A0 if unhandled.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Handler Dispatch:</strong> Looks up the address in the _ioReadHandlers dictionary
    /// and invokes the handler function. If no handler is registered, falls back to legacy
    /// address range checks and returns appropriate values.
    /// </para>
    /// <para>
    /// <strong>Unimplemented I/O:</strong> Some I/O addresses (TAPEOUT, SPKR, STROBE) are logged
    /// but not fully implemented. They return 0 or $A0.
    /// </para>
    /// <para>
    /// <strong>Floating Bus:</strong> Unhandled addresses return $A0 (arbitrary value). A future
    /// enhancement will implement proper floating bus emulation.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// Writes a byte to I/O space ($C000-$C0FF) by dispatching to the appropriate handler.
    /// </summary>
    /// <param name="address">I/O address to write to ($C000-$C0FF).</param>
    /// <param name="_">Byte value to write (usually ignored by handlers).</param>
    /// <remarks>
    /// <para>
    /// <strong>Handler Dispatch:</strong> Looks up the address in the _ioWriteHandlers dictionary
    /// and invokes the handler action. If no handler is registered, falls back to legacy
    /// address range checks.
    /// </para>
    /// <para>
    /// <strong>Data Value Ignored:</strong> Most Apple IIe I/O write handlers ignore the data value
    /// and simply toggle switches based on the address. This matches hardware behavior.
    /// </para>
    /// <para>
    /// <strong>Unimplemented I/O:</strong> Some I/O addresses (TAPEOUT, SPKR, STROBE) are logged
    /// but not fully implemented.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// Reads a byte from memory or I/O space, routing to appropriate handler.
    /// </summary>
    /// <param name="address">16-bit address to read from ($0000-$FFFF).</param>
    /// <param name="readOnly">If true, indicates a non-mutating read (not currently used).</param>
    /// <returns>Byte value from memory or I/O space.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the bus has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// <strong>Routing:</strong>
    /// <list type="bullet">
    /// <item>$C000-$C0FF: I/O space (ReadFromIOSpace)</item>
    /// <item>All other addresses: MemoryPool.Read (RAM/ROM)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Thread Context:</strong> Must be called from emulator thread only.
    /// </para>
    /// </remarks>
    public byte CpuRead(ushort address, bool readOnly = false)
    {
        ThrowIfDisposed();
        if (address >= SYSTEM_IO_START && address <= SLOT7_IO_SPACE_END)
        {
            return ReadFromIOSpace(address);
        }
        return _memoryPool.Read(address);
    }

    /// <summary>
    /// Writes a byte to memory or I/O space, routing to appropriate handler.
    /// </summary>
    /// <param name="address">16-bit address to write to ($0000-$FFFF).</param>
    /// <param name="data">Byte value to write.</param>
    /// <exception cref="ObjectDisposedException">Thrown if the bus has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// <strong>Routing:</strong>
    /// <list type="bullet">
    /// <item>$C000-$C0FF: I/O space (WriteToIOSpace)</item>
    /// <item>All other addresses: MemoryPool.Write (RAM, respecting write protection)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Write Protection:</strong> The MemoryPool enforces write protection based on
    /// soft switch states (ROM areas, language card write enable, etc.).
    /// </para>
    /// <para>
    /// <strong>Thread Context:</strong> Must be called from emulator thread only.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// Executes one CPU clock cycle and updates VBlank timing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Execution Sequence:</strong>
    /// <list type="number">
    /// <item>Execute one CPU instruction (CPU.Clock)</item>
    /// <item>Increment system clock counter</item>
    /// <item>Decrement VBlank blackout counter</item>
    /// <item>Check if VBlank cycle reached, fire event if so</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>VBlank Timing:</strong> When _systemClock reaches _nextVblankCycle, the VBlank
    /// event is fired and _nextVblankCycle is advanced by CyclesPerVBlank (17,063). The VBlank
    /// blackout counter is reset to 4,550 cycles.
    /// </para>
    /// <para>
    /// <strong>Catch-Up Logic:</strong> If the emulator runs fast (unthrottled batches), multiple
    /// VBlank cycles may be skipped. The do-while loop ensures _nextVblankCycle catches up to
    /// _systemClock, preventing event spam.
    /// </para>
    /// <para>
    /// <strong>Disposal Safety:</strong> If the bus has been disposed, Clock() returns immediately
    /// without executing CPU or firing events.
    /// </para>
    /// <para>
    /// <strong>Thread Context:</strong> Must be called from emulator thread only. VBlank event
    /// subscribers must marshal to UI thread if needed.
    /// </para>
    /// </remarks>
    public void Clock()
    {
        if (_disposed)
        {
            return;
        }

        //var currPc = _cpu!.PC;
        //if (lastPc != currPc)
        //{
        //    var hook = _hookTable?.Get((ushort) currPc);
        //    if (hook != null)
        //    {
        //        var lineNum = this.CpuRead(0x75) + (this.CpuRead(0x76) * 256);
        //        string ln = lineNum < 0xFA00 ? lineNum.ToString() : "IMM";
        //        var sp = _cpu!.SP;
        //        var spcs = 0xFF - sp;
        //        hook(-1, lineNum, spcs);
        //    }

        //}
        //lastPc = currPc;

        // Execute a single CPU cycle
        _cpu.Clock(this);
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

    /// <summary>
    /// Performs a full system reset (power cycle).
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if the bus has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// <strong>Reset Operations:</strong>
    /// <list type="number">
    /// <item>Reset memory ranges (MemoryPool.ResetRanges)</item>
    /// <item>Reset all soft switches to power-on state</item>
    /// <item>Reset CPU (PC loaded from $FFFC/$FFFD, SP = $FF)</item>
    /// <item>Reset system clock to zero</item>
    /// <item>Reset VBlank cycle counter</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Difference from UserReset:</strong> Reset() is a cold boot that clears everything.
    /// <see cref="UserReset"/> is a warm reset that preserves more state.
    /// </para>
    /// <para>
    /// <strong>Thread Context:</strong> Must be called from emulator thread only.
    /// </para>
    /// </remarks>
    public void Reset()
    {
        ThrowIfDisposed();
        _memoryPool.ResetRanges();
        _softSwitches.ResetAllSwitches();
        _cpu!.Reset(this);
        _systemClock = 0;
        _nextVblankCycle = CyclesPerVBlank;
    }

    /// <summary>
    /// Performs a warm reset (Ctrl+Reset) without fully clearing state.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if the bus has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// <strong>Reset Operations:</strong>
    /// <list type="number">
    /// <item>Reset all soft switches to power-on state</item>
    /// <item>Reset memory ranges (MemoryPool.ResetRanges)</item>
    /// <item>Reset CPU (PC loaded from $FFFC/$FFFD)</item>
    /// <item>Reset system clock to zero</item>
    /// <item>Reset VBlank cycle counter</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Difference from Reset:</strong> In current implementation, UserReset() and Reset()
    /// are nearly identical. Future enhancements may preserve more state during UserReset (e.g.,
    /// RAM contents, some soft switches) to better match Apple IIe Ctrl+Reset behavior.
    /// </para>
    /// <para>
    /// <strong>Thread Context:</strong> Must be called from emulator thread only.
    /// </para>
    /// </remarks>
    public void UserReset()
    {
        ThrowIfDisposed();

        _softSwitches.ResetAllSwitches();
        _memoryPool.ResetRanges();
        _cpu!.Reset(this);
        _systemClock = 0;
        _nextVblankCycle = CyclesPerVBlank;
    }

    /// <summary>
    /// Disposes the bus, cleaning up resources and preventing further operation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Cleanup Operations:</strong>
    /// <list type="bullet">
    /// <item>Set _disposed flag to true</item>
    /// <item>Clear VBlank event subscribers (prevent event handler leaks)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Post-Disposal Behavior:</strong>
    /// <list type="bullet">
    /// <item>Clock() becomes a no-op (returns immediately)</item>
    /// <item>CpuRead/CpuWrite throw ObjectDisposedException</item>
    /// <item>VBlank event is never fired again</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Idempotent:</strong> Multiple calls to Dispose() are safe (no-op after first call).
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> Should be called from the thread that owns the bus instance
    /// (typically after stopping emulation).
    /// </para>
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        VBlank = null;

    }

    /// <summary>
    /// Throws ObjectDisposedException if the bus has been disposed.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if _disposed is true.</exception>
    /// <remarks>
    /// Called at the start of methods that should not execute after disposal (CpuRead, CpuWrite,
    /// Reset, UserReset).
    /// </remarks>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(VA2MBus));
    }
}
