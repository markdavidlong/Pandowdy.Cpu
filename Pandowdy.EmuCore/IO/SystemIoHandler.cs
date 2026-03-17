// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.Input;
using Pandowdy.EmuCore.Machine;

namespace Pandowdy.EmuCore.IO;

/// <summary>
/// Handles system I/O space ($C000-$C08F) for the Apple IIe, managing soft switches,
/// keyboard input, game controller, language card banking, and status reads.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Responsibility:</strong> Manages the critical $C000-$C08F I/O address range:
/// keyboard input/strobe, soft switch toggles, status reads, game controller input,
/// and language card banking.
/// </para>
/// <para>
/// <strong>Handler Dispatch:</strong> Uses dictionary-based dispatch tables for O(1) lookup
/// and easy extensibility. Addresses without handlers fall back to legacy range-based logic.
/// </para>
/// <para>
/// <strong>Integration:</strong> Shares IKeyboardReader with VA2M for single source of truth.
/// Reads game controller via IGameControllerStatus. VBlank synchronized via UpdateVBlankCounter().
/// </para>
/// </remarks>
// Handles C000-C08F (System IO Area)
public class SystemIoHandler : ISystemIoHandler
{
    /// <summary>
    /// Gets the size of managed I/O space (0x90 bytes for $C000-$C08F).
    /// </summary>
    public int Size => 0x90; // Handles C000-C08F (0x90 bytes)

    /// <summary>
    /// Keyboard reader for accessing keyboard state from I/O handlers.
    /// </summary>
    /// <remarks>
    /// This is the same SingularKeyHandler instance that VA2M uses (as IKeyboardSetter)
    /// to inject keys, ensuring single source of truth for keyboard emulation.
    /// </remarks>
    private readonly IKeyboardReader _keyboard;

    /// <summary>
    /// Game controller for accessing button and paddle states.
    /// </summary>
    /// <remarks>
    /// Provides read-only access to 3 pushbuttons and 4 analog paddle/joystick inputs.
    /// SystemIoHandler reads from this when CPU accesses $C061-$C063 (buttons) or
    /// paddle timer addresses. State synchronization with SystemStatus happens
    /// directly via SystemStatusProvider, not through this class.
    /// </remarks>
    private readonly IGameControllerStatus _gameController;

    /// <summary>
    /// VBlank status handler for vertical blanking timing synchronization.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Shared State:</strong> This VBlankStatusHandler instance is shared with VA2MBus
    /// to maintain consistent VBlank timing state across the emulator. VA2MBus owns the counter
    /// lifecycle (decrementing every CPU cycle, resetting when VBlank starts), while SystemIoHandler
    /// reads the InVBlank property when servicing RD_VERTBLANK_ ($C019) I/O reads.
    /// </para>
    /// <para>
    /// <strong>$C019 (RD_VERTBLANK_) Handling:</strong> When CPU reads address $C019, the I/O handler
    /// returns bit 7 set ($80) if currently in VBlank period, or bit 7 clear ($00) if not. The lower
    /// 7 bits contain the current keyboard latch value. This allows software to detect the vertical
    /// blanking interval for flicker-free graphics updates.
    /// </para>
    /// <para>
    /// <strong>Read-Only Access:</strong> SystemIoHandler only reads from VBlankStatusHandler via
    /// the InVBlank property. It never modifies the counter or state. All counter management is
    /// performed by VA2MBus.Clock() which decrements the counter every CPU cycle and resets it
    /// when VBlank starts (every 17,030 cycles at cycle offset 12,480).
    /// </para>
    /// <para>
    /// <strong>Apple IIe Timing:</strong> VBlank is active for 4,550 cycles (70 scanlines Ã— 65
    /// cycles/scanline) starting at cycle 12,480 of each 17,030-cycle frame. Software uses this
    /// period for graphics updates, page flipping, and other operations that should not cause
    /// visual artifacts during active display.
    /// </para>
    /// </remarks>
    private CpuClockingCounters _vblank;

    /// <summary>
    /// Initializes the SystemIoHandler with required dependencies.
    /// </summary>
    /// <param name="switches">Soft switches for memory mapping and video modes.</param>
    /// <param name="keyboard">Keyboard reader (shared with VA2M as IKeyboardSetter).</param>
    /// <param name="gameController">Game controller for button and paddle state.</param>
    /// <param name="vb">VBlank status handler for vertical blanking timing synchronization.</param>
    /// <exception cref="ArgumentNullException">Thrown if any parameter is null.</exception>
    /// <remarks>
    /// <para>
    /// <strong>Initialization:</strong> Validates all dependencies and initializes I/O handler
    /// dictionaries via InitIoReadHandlers() and InitIoWriteHandlers().
    /// </para>
    /// <para>
    /// <strong>VBlank Integration:</strong> The VBlankStatusHandler is shared with VA2MBus to
    /// synchronize VBlank state. VA2MBus manages the countdown (decrements Counter every cycle),
    /// while SystemIoHandler reads the InVBlank property when servicing $C019 (RD_VERTBLANK_) reads.
    /// </para>
    /// <para>
    /// <strong>Event Subscription:</strong> Does not subscribe to game controller events -
    /// SystemStatusProvider handles that directly to maintain clean separation:
    /// <list type="bullet">
    /// <item>SystemIoHandler: Reads controller state for CPU I/O operations</item>
    /// <item>SystemStatusProvider: Observes controller changes for state snapshots</item>
    /// </list>
    /// </para>
    /// </remarks>
    public SystemIoHandler(SoftSwitches switches, IKeyboardReader keyboard, IGameControllerStatus gameController, CpuClockingCounters vb)
    {
        ArgumentNullException.ThrowIfNull(switches);
        ArgumentNullException.ThrowIfNull(keyboard);
        ArgumentNullException.ThrowIfNull(gameController);
        ArgumentNullException.ThrowIfNull(vb);

        _softSwitches = switches;
        _keyboard = keyboard;
        _gameController = gameController;
        _vblank = vb;

        // Note: We do NOT subscribe to game controller events here.
        // SystemStatusProvider subscribes directly to maintain clean separation:
        // - SystemIoHandler: Reads controller state for CPU I/O operations
        // - SystemStatusProvider: Observes controller changes for state snapshots

        InitIoReadHandlers();
        InitIoWriteHandlers();
    }

    /// <summary>
    /// Resets all soft switches to power-on defaults.
    /// </summary>
    /// <remarks>
    /// Called during system reset. Does not reset keyboard or game controller - those
    /// are managed by their respective subsystems.
    /// </remarks>
    public void Reset()
    {
        _softSwitches.ResetAllSwitches();
    }

    /// <summary>
    /// Restores the system I/O handler to its initial power-on state (cold boot).
    /// </summary>
    /// <remarks>
    /// <para>
    /// SystemIoHandler is stateless beyond the soft switches it wraps. Soft switches are
    /// independently restartable via <see cref="RestartCollection"/>, so this method is a
    /// no-op. Keyboard and game controller are also independently restartable.
    /// </para>
    /// <para>
    /// <strong>Note:</strong> <c>Reset()</c> still calls <see cref="SoftSwitches.ResetAllSwitches"/>
    /// for warm-reset (Ctrl+Reset) which uses the tree-based call path. Cold boot uses the
    /// flat <see cref="RestartCollection"/> path instead.
    /// </para>
    /// </remarks>
    public void Restart()
    {
        // No-op: SoftSwitches is independently restartable via RestartCollection.
        // SystemIoHandler has no additional state to clear.
    }

    /// <summary>
    /// Reads from I/O space using zero-based offset (0x00-0x8F for $C000-$C08F).
    /// </summary>
    /// <param name="offset">Zero-based offset (0x00-0x8F).</param>
    /// <returns>Byte value from I/O handler.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if offset â‰¥ 0x90.</exception>
    /// <remarks>
    /// Translates offset to absolute address ($C000 + offset) and delegates to ReadFromIOSpace().
    /// </remarks>
    public byte Read(ushort offset)
    {
        if (offset >= Size)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset,
           $"Address must be less than {Size} (0x{Size:X}). Got 0x{offset:X4}.");
        }

        ushort address = (ushort) (0xC000 + offset);
        return ReadFromIOSpace(address);
    }

    /// Placeholder for now until we can create a Read method that does not affect IO states.
    public byte Peek(ushort _) { return 0; }

    /// <summary>
    /// Writes to I/O space using zero-based offset (0x00-0x8F for $C000-$C08F).
    /// </summary>
    /// <param name="offset">Zero-based offset (0x00-0x8F).</param>
    /// <param name="data">Byte to write (usually ignored by handlers).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if offset â‰¥ 0x90.</exception>
    /// <remarks>
    /// Translates offset to absolute address ($C000 + offset) and delegates to WriteToIOSpace().
    /// Most handlers ignore data value and toggle switches based on address.
    /// </remarks>
    public void Write(ushort offset, byte data)
    {
        if (offset >= Size)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset,
           $"Address must be less than {Size} (0x{Size:X}). Got 0x{offset:X4}.");
        }


        ushort address = (ushort) (0xC000 + offset);
        WriteToIOSpace(address, data);
    }

    /// <summary>
    /// Reads a byte from I/O space ($C000-$C0FF) by dispatching to the appropriate handler.
    /// </summary>
    /// <param name="address">I/O address to read from ($C000-$C0FF).</param>
    /// <returns>Byte value returned by the I/O handler, or $A0 if unhandled.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Handler Dispatch:</strong> Looks up the address offset in the _ioReadHandlers array
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte ReadFromIOSpace(ushort address)
    {
        int offset = address - 0xC000;
        var handler = _ioReadHandlers[offset];
        if (handler != null)
        {
            return handler();
        }
        // Fallback to existing logic for unhandled addresses
        if (address >= CLR80STORE_ && address <= SETALTCHAR_)
        {
            return _keyboard.PeekCurrentKeyAndStrobe();
            //return _currKey;
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
            case PADDLE0_:
                return 0x00;
            case PADDLE1_:
                return 0x00;
            case PADDLE2_:
                return 0x00;
            case PADDLE3_:
                return 0x00;
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
    /// <strong>Handler Dispatch:</strong> Looks up the address offset in the _ioWriteHandlers array
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteToIOSpace(ushort address, byte _ /*data*/)
    {
        int offset = address - 0xC000;
        var writer = _ioWriteHandlers[offset];
        if (writer != null)
        {
   //         Debug.WriteLine($"Found a write handler for IO value {address:X}");

            writer(_);
            return;
        }
        else
        {
      //      Debug.WriteLine($"Could not find a write handler for IO value {address:X}");
        }
        if (address >= KEYSTRB_ && address <= KEYSTRB_ + 0x1F)
        {
            _keyboard.ClearStrobe();
            return;
        }
        if (address >= TAPEOUT_ && address <= END_TAPEOUT_RD_)
        {
            Debug.WriteLine($"TAPEOUT not implemented yet (Write to {address:X4})");
            return;
        }
        if (address >= SPKR_ && address <= END_SPKR_RD_)
        {
            //   Debug.WriteLine($"SPKR not implemented yet (Write to {address:X4})");
            return;
        }
        if (address >= STROBE_ && address <= END_STROBE_RD_)
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
    /// Soft switches managing memory mapping, video modes, ROM selection, and annunciators.
    /// </summary>
    private SoftSwitches _softSwitches;

    /// <summary>
    /// Array mapping I/O offsets (0x00-0x8F) to write handler actions.
    /// </summary>
    /// <remarks>
    /// Uses array instead of Dictionary for O(1) direct indexing without hashing overhead.
    /// Handlers receive the byte value written to the I/O address. Most handlers
    /// ignore the data value and simply toggle soft switches based on the address.
    /// </remarks>
    private readonly Action<byte>?[] _ioWriteHandlers = new Action<byte>?[0x90];

    /// <summary>
    /// Array mapping I/O offsets (0x00-0x8F) to read handler functions.
    /// </summary>
    /// <remarks>
    /// Uses array instead of Dictionary for O(1) direct indexing without hashing overhead.
    /// Handlers return the byte value to be read from the I/O address. Many handlers
    /// also have side effects (toggling soft switches, clearing keyboard latch, etc.).
    /// </remarks>
    private readonly Func<byte>?[] _ioReadHandlers = new Func<byte>?[0x90];

    #region Constants 
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
    public const ushort CLR80STORE_ = 0xC000;
    /// <summary>Set 80STORE switch on ($C001 write).</summary>
    public const ushort SET80STORE_ = 0xC001;
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
    /// <summary>Use slot 3 ROM ($C00B).</summary>
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

    #endregion

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
        // Simple soft-switch writes (use offset = address - 0xC000)
        _ioWriteHandlers[CLR80STORE_ - 0xC000] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.Store80, false);
        _ioWriteHandlers[SET80STORE_ - 0xC000] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.Store80, true);
        _ioWriteHandlers[RDMAINRAM_ - 0xC000] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.RamRd, false);
        _ioWriteHandlers[RDCARDRAM_ - 0xC000] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.RamRd, true);
        _ioWriteHandlers[WRMAINRAM_ - 0xC000] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.RamWrt, false);
        _ioWriteHandlers[WRCARDRAM_ - 0xC000] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.RamWrt, true);
        _ioWriteHandlers[SLOTCXROM_ - 0xC000] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.IntCxRom, false);
        _ioWriteHandlers[INTCXROM_ - 0xC000] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.IntCxRom, true);
        _ioWriteHandlers[STDZP_ - 0xC000] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.AltZp, false);
        _ioWriteHandlers[ALTZP_ - 0xC000] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.AltZp, true);
        _ioWriteHandlers[INTC3ROM_ - 0xC000] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.SlotC3Rom, false);
        _ioWriteHandlers[SLOTC3ROM_ - 0xC000] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.SlotC3Rom, true);
        _ioWriteHandlers[CLR80VID_ - 0xC000] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.Vid80, false);
        _ioWriteHandlers[SET80VID_ - 0xC000] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.Vid80, true);
        _ioWriteHandlers[CLRALTCHAR_ - 0xC000] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.AltChar, false);
        _ioWriteHandlers[SETALTCHAR_ - 0xC000] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.AltChar, true);
        _ioWriteHandlers[CLRTXT_ - 0xC000] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.Text, false);
        _ioWriteHandlers[SETTXT_ - 0xC000] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.Text, true);
        _ioWriteHandlers[CLRMIXED_ - 0xC000] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.Mixed, false);
        _ioWriteHandlers[SETMIXED_ - 0xC000] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.Mixed, true);
        _ioWriteHandlers[CLRPAGE2_ - 0xC000] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.Page2, false);
        _ioWriteHandlers[SETPAGE2_ - 0xC000] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.Page2, true);
        _ioWriteHandlers[CLRHIRES_ - 0xC000] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.HiRes, false);
        _ioWriteHandlers[SETHIRES_ - 0xC000] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.HiRes, true);
        _ioWriteHandlers[CLRAN0_ - 0xC000] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.An0, false);
        _ioWriteHandlers[SETAN0_ - 0xC000] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.An0, true);
        _ioWriteHandlers[CLRAN1_ - 0xC000] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.An1, false);
        _ioWriteHandlers[SETAN1_ - 0xC000] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.An1, true);
        _ioWriteHandlers[CLRAN2_ - 0xC000] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.An2, false);
        _ioWriteHandlers[SETAN2_ - 0xC000] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.An2, true);
        _ioWriteHandlers[CLRAN3_ - 0xC000] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.An3, false);
        _ioWriteHandlers[SETAN3_ - 0xC000] = _ => _softSwitches.Set(SoftSwitches.SoftSwitchId.An3, true);

        // Banked block writes (unrolled) â€” note write-path semantics differ from read-path
        _ioWriteHandlers[B2_RD_RAM_NO_WRT_ - 0xC000] = _ => ApplyBankIoWriteFlags(false, B2_RD_RAM_NO_WRT_);
        _ioWriteHandlers[B2_RD_RAM_NO_WRT_ALT_ - 0xC000] = _ => ApplyBankIoWriteFlags(false, B2_RD_RAM_NO_WRT_ALT_);
        _ioWriteHandlers[B2_RD_ROM_WRT_RAM_ - 0xC000] = _ => ApplyBankIoWriteFlags(false, B2_RD_ROM_WRT_RAM_);
        _ioWriteHandlers[B2_RD_ROM_WRT_RAM_ALT_ - 0xC000] = _ => ApplyBankIoWriteFlags(false, B2_RD_ROM_WRT_RAM_ALT_);
        _ioWriteHandlers[B2_RD_ROM_NO_WRT_ - 0xC000] = _ => ApplyBankIoWriteFlags(false, B2_RD_ROM_NO_WRT_);
        _ioWriteHandlers[B2_RD_ROM_NO_WRT_ALT_ - 0xC000] = _ => ApplyBankIoWriteFlags(false, B2_RD_ROM_NO_WRT_ALT_);
        _ioWriteHandlers[B2_RD_RAM_WRT_RAM_ - 0xC000] = _ => ApplyBankIoWriteFlags(false, B2_RD_RAM_WRT_RAM_);
        _ioWriteHandlers[B2_RD_RAM_WRT_RAM_ALT_ - 0xC000] = _ => ApplyBankIoWriteFlags(false, B2_RD_RAM_WRT_RAM_ALT_);

        _ioWriteHandlers[B1_RD_RAM_NO_WRT_ - 0xC000] = _ => ApplyBankIoWriteFlags(true, B1_RD_RAM_NO_WRT_);
        _ioWriteHandlers[B1_RD_RAM_NO_WRT_ALT_ - 0xC000] = _ => ApplyBankIoWriteFlags(true, B1_RD_RAM_NO_WRT_ALT_);
        _ioWriteHandlers[B1_RD_ROM_WRT_RAM_ - 0xC000] = _ => ApplyBankIoWriteFlags(true, B1_RD_ROM_WRT_RAM_);
        _ioWriteHandlers[B1_RD_ROM_WRT_RAM_ALT_ - 0xC000] = _ => ApplyBankIoWriteFlags(true, B1_RD_ROM_WRT_RAM_ALT_);
        _ioWriteHandlers[B1_RD_ROM_NO_WRT_ - 0xC000] = _ => ApplyBankIoWriteFlags(true, B1_RD_ROM_NO_WRT_);
        _ioWriteHandlers[B1_RD_ROM_NO_WRT_ALT_ - 0xC000] = _ => ApplyBankIoWriteFlags(true, B1_RD_ROM_NO_WRT_ALT_);
        _ioWriteHandlers[B1_RD_RAM_WRT_RAM_ - 0xC000] = _ => ApplyBankIoWriteFlags(true, B1_RD_RAM_WRT_RAM_);
        _ioWriteHandlers[B1_RD_RAM_WRT_RAM_ALT_ - 0xC000] = _ => ApplyBankIoWriteFlags(true, B1_RD_RAM_WRT_RAM_ALT_);
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
        // Simple reads that only return composed values (use offset = address - 0xC000)
        _ioReadHandlers[KEYSTRB_ - 0xC000] = () => { return _keyboard.ClearStrobe();  };
        _ioReadHandlers[RD_LC_BANK1_ - 0xC000] = () => (byte) (Utility.BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.Bank1), _keyboard.PeekCurrentKeyValue()) ^ 0x80);
        _ioReadHandlers[RD_LC_RAM - 0xC000] = () => Utility.BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.HighRead), _keyboard.PeekCurrentKeyValue());
        _ioReadHandlers[RD_RAMRD_ - 0xC000] = () => Utility.BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.RamRd), _keyboard.PeekCurrentKeyValue());
        _ioReadHandlers[RD_RAMWRT_ - 0xC000] = () => Utility.BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.RamWrt), _keyboard.PeekCurrentKeyValue());
        _ioReadHandlers[RD_INTCXROM_ - 0xC000] = () => Utility.BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.IntCxRom), _keyboard.PeekCurrentKeyValue());
        _ioReadHandlers[RD_ALTZP_ - 0xC000] = () => Utility.BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.AltZp), _keyboard.PeekCurrentKeyValue());
        _ioReadHandlers[RD_SLOTC3ROM_ - 0xC000] = () => Utility.BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.SlotC3Rom), _keyboard.PeekCurrentKeyValue());
        _ioReadHandlers[RD_80STORE_ - 0xC000] = () => Utility.BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.Store80), _keyboard.PeekCurrentKeyValue());
        _ioReadHandlers[RD_TEXT_ - 0xC000] = () => Utility.BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.Text), _keyboard.PeekCurrentKeyValue());
        _ioReadHandlers[RD_MIXED_ - 0xC000] = () => Utility.BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.Mixed), _keyboard.PeekCurrentKeyValue());
        _ioReadHandlers[RD_PAGE2_ - 0xC000] = () => Utility.BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.Page2), _keyboard.PeekCurrentKeyValue());
        _ioReadHandlers[RD_HIRES_ - 0xC000] = () => Utility.BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.HiRes), _keyboard.PeekCurrentKeyValue());
        _ioReadHandlers[RD_ALTCHAR_ - 0xC000] = () => Utility.BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.AltChar), _keyboard.PeekCurrentKeyValue());
        _ioReadHandlers[RD_80VID_ - 0xC000] = () => Utility.BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.Vid80), _keyboard.PeekCurrentKeyValue());
        _ioReadHandlers[RD_VERTBLANK_ - 0xC000] = () => { return (byte) ((byte) (_vblank.InVBlank ? 0x80 : 0x00) | _keyboard.PeekCurrentKeyValue() & 0x7f); };
        _ioReadHandlers[TAPEIN_ - 0xC000] = () => 0x00;
        _ioReadHandlers[BUTTON0_ - 0xC000] = () => (byte) (_gameController.GetButton(0) ? 0x80 : 0x00);
        _ioReadHandlers[BUTTON1_ - 0xC000] = () => (byte) (_gameController.GetButton(1) ? 0x80 : 0x00);
        _ioReadHandlers[BUTTON2_ - 0xC000] = () => (byte) (_gameController.GetButton(2) ? 0x80 : 0x00);

        // Reads that also toggle soft switches (Apple II behavior: reading addresses sets switches)
        _ioReadHandlers[CLRTXT_ - 0xC000] = () => { _softSwitches.Set(SoftSwitches.SoftSwitchId.Text, false); return 0xA0; };
        _ioReadHandlers[SETTXT_ - 0xC000] = () => { _softSwitches.Set(SoftSwitches.SoftSwitchId.Text, true); return 0xA0; };
        _ioReadHandlers[CLRMIXED_ - 0xC000] = () => { _softSwitches.Set(SoftSwitches.SoftSwitchId.Mixed, false); return 0xA0; };
        _ioReadHandlers[SETMIXED_ - 0xC000] = () => { _softSwitches.Set(SoftSwitches.SoftSwitchId.Mixed, true); return 0xA0; };
        _ioReadHandlers[CLRPAGE2_ - 0xC000] = () => { _softSwitches.Set(SoftSwitches.SoftSwitchId.Page2, false); return 0xA0; };
        _ioReadHandlers[SETPAGE2_ - 0xC000] = () => { _softSwitches.Set(SoftSwitches.SoftSwitchId.Page2, true); return 0xA0; };
        _ioReadHandlers[CLRHIRES_ - 0xC000] = () => { _softSwitches.Set(SoftSwitches.SoftSwitchId.HiRes, false); return 0xA0; };
        _ioReadHandlers[SETHIRES_ - 0xC000] = () => { _softSwitches.Set(SoftSwitches.SoftSwitchId.HiRes, true); return 0xA0; };
        _ioReadHandlers[CLRAN0_ - 0xC000] = () => { _softSwitches.Set(SoftSwitches.SoftSwitchId.An0, false); return Utility.BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.An0)); };
        _ioReadHandlers[SETAN0_ - 0xC000] = () => { _softSwitches.Set(SoftSwitches.SoftSwitchId.An0, true); return Utility.BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.An0)); };
        _ioReadHandlers[CLRAN1_ - 0xC000] = () => { _softSwitches.Set(SoftSwitches.SoftSwitchId.An1, false); return Utility.BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.An1)); };
        _ioReadHandlers[SETAN1_ - 0xC000] = () => { _softSwitches.Set(SoftSwitches.SoftSwitchId.An1, true); return Utility.BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.An1)); };
        _ioReadHandlers[CLRAN2_ - 0xC000] = () => { _softSwitches.Set(SoftSwitches.SoftSwitchId.An2, false); return Utility.BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.An2)); };
        _ioReadHandlers[SETAN2_ - 0xC000] = () => { _softSwitches.Set(SoftSwitches.SoftSwitchId.An2, true); return Utility.BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.An2)); };
        _ioReadHandlers[CLRAN3_ - 0xC000] = () => { _softSwitches.Set(SoftSwitches.SoftSwitchId.An3, false); return Utility.BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.An3)); };
        _ioReadHandlers[SETAN3_ - 0xC000] = () => { _softSwitches.Set(SoftSwitches.SoftSwitchId.An3, true); return Utility.BuildHiBitVal(_softSwitches.Get(SoftSwitches.SoftSwitchId.An3)); };

        // Banked block reads (unrolled to individual addresses, sharing helper)
        _ioReadHandlers[B2_RD_RAM_NO_WRT_ - 0xC000] = () => { ApplyBankIoReadFlags(false, B2_RD_RAM_NO_WRT_); return 0xA0; };
        _ioReadHandlers[B2_RD_RAM_NO_WRT_ALT_ - 0xC000] = () => { ApplyBankIoReadFlags(false, B2_RD_RAM_NO_WRT_ALT_); return 0xA0; };
        _ioReadHandlers[B2_RD_ROM_WRT_RAM_ - 0xC000] = () => { ApplyBankIoReadFlags(false, B2_RD_ROM_WRT_RAM_); return 0xA0; };
        _ioReadHandlers[B2_RD_ROM_WRT_RAM_ALT_ - 0xC000] = () => { ApplyBankIoReadFlags(false, B2_RD_ROM_WRT_RAM_ALT_); return 0xA0; };
        _ioReadHandlers[B2_RD_ROM_NO_WRT_ - 0xC000] = () => { ApplyBankIoReadFlags(false, B2_RD_ROM_NO_WRT_); return 0xA0; };
        _ioReadHandlers[B2_RD_ROM_NO_WRT_ALT_ - 0xC000] = () => { ApplyBankIoReadFlags(false, B2_RD_ROM_NO_WRT_ALT_); return 0xA0; };
        _ioReadHandlers[B2_RD_RAM_WRT_RAM_ - 0xC000] = () => { ApplyBankIoReadFlags(false, B2_RD_RAM_WRT_RAM_); return 0xA0; };
        _ioReadHandlers[B2_RD_RAM_WRT_RAM_ALT_ - 0xC000] = () => { ApplyBankIoReadFlags(false, B2_RD_RAM_WRT_RAM_ALT_); return 0xA0; };

        _ioReadHandlers[B1_RD_RAM_NO_WRT_ - 0xC000] = () => { ApplyBankIoReadFlags(true, B1_RD_RAM_NO_WRT_); return 0xA0; };
        _ioReadHandlers[B1_RD_RAM_NO_WRT_ALT_ - 0xC000] = () => { ApplyBankIoReadFlags(true, B1_RD_RAM_NO_WRT_ALT_); return 0xA0; };
        _ioReadHandlers[B1_RD_ROM_WRT_RAM_ - 0xC000] = () => { ApplyBankIoReadFlags(true, B1_RD_ROM_WRT_RAM_); return 0xA0; };
        _ioReadHandlers[B1_RD_ROM_WRT_RAM_ALT_ - 0xC000] = () => { ApplyBankIoReadFlags(true, B1_RD_ROM_WRT_RAM_ALT_); return 0xA0; };
        _ioReadHandlers[B1_RD_ROM_NO_WRT_ - 0xC000] = () => { ApplyBankIoReadFlags(true, B1_RD_ROM_NO_WRT_); return 0xA0; };
        _ioReadHandlers[B1_RD_ROM_NO_WRT_ALT_ - 0xC000] = () => { ApplyBankIoReadFlags(true, B1_RD_ROM_NO_WRT_ALT_); return 0xA0; };
        _ioReadHandlers[B1_RD_RAM_WRT_RAM_ - 0xC000] = () => { ApplyBankIoReadFlags(true, B1_RD_RAM_WRT_RAM_); return 0xA0; };
        _ioReadHandlers[B1_RD_RAM_WRT_RAM_ALT_ - 0xC000] = () => { ApplyBankIoReadFlags(true, B1_RD_RAM_WRT_RAM_ALT_); return 0xA0; };
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

}
