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
/// <item>Memory read/write routing to AddressSpaceController</item>
/// <item>System I/O routing ($C000-$C08F) to SystemIoHandler</item>
/// <item>VBlank timing and event generation (~60 Hz)</item>
/// <item>Keyboard input delegation (via ISystemIoHandler)</item>
/// <item>Game controller pushbutton delegation (via ISystemIoHandler)</item>
/// </list>
/// </para>
/// <para>
/// <strong>Architecture Changes:</strong>
/// <list type="bullet">
/// <item>I/O handling delegated to ISystemIoHandler (no longer internal)</item>
/// <item>Memory operations delegated to AddressSpaceController (renamed from MemoryPool)</item>
/// <item>Soft switch management handled by SystemIoHandler</item>
/// <item>Language card banking logic handled by SystemIoHandler</item>
/// </list>
/// </para>
/// <para>
/// <strong>Threading Model:</strong> All public methods (CpuRead/Write, Clock, Reset)
/// must be called ONLY from the emulator worker thread. The VBlank event is raised on the
/// emulator thread; UI subscribers MUST marshal to UI thread via dispatcher.
/// </para>
/// <para>
/// <strong>VBlank Timing:</strong> Emits VBlank event every 17,030 cycles (~60 Hz at 1.023 MHz),
/// matching the Apple IIe NTSC vertical blanking interval. The VBlank blackout period lasts
/// 4,550 cycles during which RD_VERTBLANK_ ($C019) reads as $80.
/// </para>
/// <para>
/// <strong>Disposal:</strong> After disposal, no further events are raised and Clock() becomes
/// a no-op. Proper disposal prevents event handler leaks and ensures clean shutdown.
/// </para>
/// <para>
/// <strong>⚠️ REFACTORING IN PROGRESS:</strong> This class has been simplified to focus on
/// bus coordination only. I/O handling, keyboard input, and pushbutton management have been
/// extracted to dedicated subsystems. See Pandowdy.EmuCore/_docs_/VA2MBus-Refactoring-Notes.md
/// for architectural details.
/// </para>
/// </remarks>
public sealed class VA2MBus : IAppleIIBus, IDisposable, IKeyboardSetter
{
    /// <summary>
    /// Address space controller managing the 128KB Apple IIe memory space.
    /// </summary>
    /// <remarks>
    /// Renamed from MemoryPool to better reflect its responsibility of managing
    /// the entire address space including RAM, ROM, and memory banking.
    /// </remarks>
    private readonly AddressSpaceController _addressSpace;
    
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
    /// System I/O handler managing $C000-$C08F I/O space.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Handles all system I/O operations including:
    /// <list type="bullet">
    /// <item>Soft switch management (memory mapping, video modes)</item>
    /// <item>Keyboard input (KBD, KEYSTRB)</item>
    /// <item>Pushbutton states (BUTTON0-2)</item>
    /// <item>Language card banking ($C080-$C08F)</item>
    /// <item>VBlank status (RD_VERTBLANK)</item>
    /// </list>
    /// </para>
    /// </remarks>
    private ISystemIoHandler _io;

    /// <summary>
    /// Gets the AddressSpaceController for direct memory access.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Primarily used for testing and direct memory inspection. Normal CPU access
    /// should go through CpuRead/CpuWrite which handle I/O space routing.
    /// </para>
    /// <para>
    /// <strong>Note:</strong> This exposes the full address space controller, not just
    /// RAM. It includes memory banking, ROM mapping, and other address space features.
    /// The IMemory interface is used for compatibility with existing code.
    /// </para>
    /// </remarks>
    public IMemory RAM => _addressSpace;


    /// <summary>
    /// Gets the total number of CPU cycles executed since last reset.
    /// </summary>
    public ulong SystemClockCounter => _systemClock;
    
    /// <summary>
    /// Flag indicating whether this bus has been disposed.
    /// </summary>
    private bool _disposed;
    
    

    /// <summary>
    /// Number of CPU cycles between VBlank events (17,030 = 262 scanlines × 65 cycles/scanline).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Apple IIe NTSC Timing:</strong>
    /// <list type="bullet">
    /// <item>192 visible scanlines (cycles 0-12,479)</item>
    /// <item>70 vertical blanking scanlines (cycles 12,480-17,029)</item>
    /// <item>Total: 262 scanlines × 65 cycles/scanline = 17,030 cycles/frame</item>
    /// <item>Frame rate: 1,023,000 Hz / 17,030 cycles ≈ 60.06 Hz (NTSC)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Why 17,030 not 17,063:</strong> The correct Apple IIe timing is 17,030 cycles
    /// per frame. The value 17,063 was an approximation that caused synchronization issues
    /// with 80-column firmware which expects VBlank at scanline 192 (cycle 12,480).
    /// </para>
    /// </remarks>
    private const ulong CyclesPerVBlank = 17030;

    /// <summary>
    /// Number of cycles during which the vertical blanking flag (RD_VERTBLANK_) reads as $80.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The VBlank blackout period is 4,550 cycles (70 scanlines × 65 cycles/scanline) during
    /// which the video scanner is not drawing visible scanlines. Software can use this period
    /// for graphics updates without causing visual artifacts.
    /// </para>
    /// <para>
    /// <strong>Timing:</strong>
    /// <list type="bullet">
    /// <item>VBlank starts at cycle 12,480 (scanline 192)</item>
    /// <item>VBlank ends at cycle 17,029 (scanline 261)</item>
    /// <item>Duration: 70 scanlines × 65 cycles = 4,550 cycles</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>80-Column Firmware Synchronization:</strong> The 80-column firmware relies on
    /// testing RD_VERTBLANK_ to determine when to start page-flipping. By ensuring VBlank
    /// fires at the correct cycle (12,480), we maintain synchronization with firmware timing
    /// expectations.
    /// </para>
    /// </remarks>
    private const int VBlankBlackoutCycles = 4550;
    
    /// <summary>
    /// Cycle count within frame at which VBlank starts (scanline 192 begins).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Visible Display Ends:</strong> After 192 visible scanlines × 65 cycles/scanline
    /// = 12,480 cycles, the video scanner enters vertical blanking. This is when RD_VERTBLANK_
    /// ($C019) transitions from $00 to $80.
    /// </para>
    /// <para>
    /// <strong>Why This Matters:</strong> The Apple IIe 80-column firmware expects VBlank to
    /// occur at this specific cycle offset, not at cycle 0 (start of frame). By firing VBlank
    /// at cycle 12,480, we ensure firmware PAGE2 toggles are synchronized with the correct
    /// scanline positions.
    /// </para>
    /// </remarks>
    private const ulong VBlankStartCycle = 12480; // 192 scanlines × 65 cycles
    
    /// <summary>
    /// Cycle count at which the next VBlank event will fire.
    /// </summary>
    /// <remarks>
    /// Initialized to VBlankStartCycle so the first VBlank fires at cycle 12,480 (not 0),
    /// matching Apple IIe hardware behavior where VBlank occurs after visible display.
    /// </remarks>
    private ulong _nextVblankCycle = VBlankStartCycle;
    
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



    /// <summary>Start of system I/O space ($C000).</summary>
    public const ushort SYSTEM_IO_START = 0xC000;
    /// <summary>End of Apple IIe internal system I/O address space</summary>
    public const ushort IO_SYSTEM_AREA_END = 0xC08F;




    


    /// <summary>
    /// Initializes a new instance of the VA2MBus class.
    /// </summary>
    /// <param name="addressSpace">Address space controller managing 128KB Apple IIe memory space (RAM, ROM, banking).</param>
    /// <param name="ioHandler">System I/O handler managing $C000-$C08F I/O space operations.</param>
    /// <param name="cpu">CPU instance (6502 emulator) to connect to this bus.</param>
    /// <exception cref="ArgumentNullException">Thrown if any parameter is null.</exception>
    /// <remarks>
    /// <para>
    /// <strong>Initialization Sequence:</strong>
    /// <list type="number">
    /// <item>Validate all dependencies (null checks)</item>
    /// <item>Store AddressSpaceController reference for memory operations</item>
    /// <item>Store SystemIoHandler reference for I/O operations</item>
    /// <item>Store CPU reference for instruction execution</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Simplified Architecture:</strong> This bus no longer manages I/O handler
    /// dictionaries or soft switch logic directly. All I/O operations are delegated to
    /// the injected ISystemIoHandler, which handles soft switches, keyboard input,
    /// pushbuttons, and language card banking internally.
    /// </para>
    /// <para>
    /// <strong>Address Routing:</strong>
    /// <list type="bullet">
    /// <item>$C000-$C08F: Routed to ISystemIoHandler</item>
    /// <item>All other addresses: Routed to AddressSpaceController</item>
    /// </list>
    /// </para>
    /// </remarks>
    public VA2MBus(AddressSpaceController addressSpace, ISystemIoHandler ioHandler , ICpu cpu)
    {
        ArgumentNullException.ThrowIfNull(addressSpace);
        ArgumentNullException.ThrowIfNull(ioHandler);
        ArgumentNullException.ThrowIfNull(cpu);
        _addressSpace = addressSpace;
        _cpu = cpu;
        _io = ioHandler;
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
    /// Injects a keyboard character into the keyboard latch via the I/O handler.
    /// </summary>
    /// <param name="key">ASCII key value with bit 7 set (indicating key available).</param>
    /// <inheritdoc cref="IKeyboardSetter.EnqueueKey" path="/remarks"/>
    /// <remarks>
    /// <para>
    /// <strong>Implementation Note:</strong> This method delegates to ISystemIoHandler.EnqueueKey(),
    /// which manages the internal keyboard latch (_currKey). The strobe bit (bit 7) should already
    /// be set by the caller. Software reads $C010 (KEYSTRB) to clear bit 7, indicating the key
    /// has been read.
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> Should be called only from the emulator thread. External
    /// threads should enqueue via VA2M.EnqueueKey() which marshals to the emulator thread safely.
    /// </para>
    /// </remarks>
    public void EnqueueKey(byte key)
    {
        _io.EnqueueKey(key);
    }

   

    /// <summary>
    /// Sets the state of a pushbutton via the I/O handler.
    /// </summary>
    /// <param name="num">Button number (0-2).</param>
    /// <param name="pressed">True if button is pressed, false if released.</param>
    /// <remarks>
    /// <para>
    /// <strong>Implementation Note:</strong> This method delegates to ISystemIoHandler.SetPushButton(),
    /// which manages the internal button state (_button0, _button1, _button2). Button states are
    /// readable at I/O addresses $C061-$C063 with bit 7 set when pressed.
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> Should be called only from the emulator thread. External
    /// threads should enqueue via VA2M.SetPushButton().
    /// </para>
    /// <para>
    /// Invalid button numbers are silently ignored by the I/O handler.
    /// </para>
    /// </remarks>
    public void SetPushButton(int num, bool pressed)
    {
        _io.SetPushButton(num, pressed);
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
    /// <strong>Routing Logic:</strong>
    /// <list type="bullet">
    /// <item>$C000-$C08F: System I/O space → ISystemIoHandler.Read(offset)</item>
    /// <item>All other addresses → AddressSpaceController.Read(address)</item>
    /// </para>
    /// <para>
    /// <strong>Address Translation:</strong> System I/O addresses are converted to zero-based
    /// offsets (address - 0xC000) before passing to the I/O handler. This allows the handler
    /// to work with offsets 0x00-0x8F instead of absolute addresses.
    /// </para>
    /// <para>
    /// <strong>Future Expansion:</strong> Slot I/O space ($C090-$C0FF) routing will be added
    /// in a future refactoring phase when slot card support is implemented.
    /// </para>
    /// <para>
    /// <strong>Thread Context:</strong> Must be called from emulator thread only.
    /// </para>
    /// </remarks>
    public byte CpuRead(ushort address, bool readOnly = false)
    {
        ThrowIfDisposed();
        if (address >= SYSTEM_IO_START && address <= IO_SYSTEM_AREA_END)
        {
            return _io.Read((ushort)(address-0xC000));
        }
        return _addressSpace.Read(address);
    }

    /// <summary>
    /// Writes a byte to memory or I/O space, routing to appropriate handler.
    /// </summary>
    /// <param name="address">16-bit address to write to ($0000-$FFFF).</param>
    /// <param name="data">Byte value to write.</param>
    /// <exception cref="ObjectDisposedException">Thrown if the bus has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// <strong>Routing Logic:</strong>
    /// <list type="bullet">
    /// <item>$C000-$C08F: System I/O space → ISystemIoHandler.Write(offset, data)</item>
    /// <item>All other addresses → AddressSpaceController.Write(address, data)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Address Translation:</strong> System I/O addresses are converted to zero-based
    /// offsets (address - 0xC000) before passing to the I/O handler. This allows the handler
    /// to work with offsets 0x00-0x8F instead of absolute addresses.
    /// </para>
    /// <para>
    /// <strong>Write Protection:</strong> The AddressSpaceController enforces write protection
    /// based on soft switch states (ROM areas, language card write enable, etc.). The I/O handler
    /// handles soft switch writes which may toggle these protection states.
    /// </para>
    /// <para>
    /// <strong>Future Expansion:</strong> Slot I/O space ($C090-$C0FF) routing will be added
    /// in a future refactoring phase when slot card support is implemented.
    /// </para>
    /// <para>
    /// <strong>Thread Context:</strong> Must be called from emulator thread only.
    /// </para>
    /// </remarks>
    public void CpuWrite(ushort address, byte data)
    {
        ThrowIfDisposed();
        if (address >= SYSTEM_IO_START && address <= IO_SYSTEM_AREA_END)
        {
            _io.Write((ushort) (address - 0xC000),data);
            return;
        }
        _addressSpace.Write(address, data);
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
    /// <item>Update I/O handler's VBlank counter for RD_VERTBLANK reads</item>
    /// <item>Check if VBlank cycle reached, fire event if so</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>VBlank Counter Synchronization:</strong> The _VblankBlackoutCounter is shared
    /// with the I/O handler via UpdateVBlankCounter() so that reads to $C019 (RD_VERTBLANK_)
    /// return the correct bit 7 state (set during VBlank, clear during visible scanlines).
    /// </para>
    /// <para>
    /// <strong>VBlank Timing:</strong> When _systemClock reaches _nextVblankCycle (initially
    /// 12,480, then 29,510, 46,540, ...), the VBlank event is fired and _nextVblankCycle is
    /// advanced by CyclesPerVBlank (17,030). The VBlank blackout counter is reset to 4,550 cycles.
    /// </para>
    /// <para>
    /// <strong>Frame Cycle Calculation:</strong>
    /// <code>
    /// Frame 0: VBlank at cycle 12,480 (scanline 192)
    /// Frame 1: VBlank at cycle 29,510 (12,480 + 17,030)
    /// Frame 2: VBlank at cycle 46,540 (29,510 + 17,030)
    /// ...and so on
    /// </code>
    /// </para>
    /// <para>
    /// <strong>Catch-Up Logic:</strong> If the emulator runs fast (unthrottled batches), multiple
    /// VBlank cycles may be skipped. The do-while loop ensures _nextVblankCycle catches up to
    /// _systemClock, preventing event spam while maintaining proper phase alignment.
    /// </para>
    /// <para>
    /// <strong>80-Column Synchronization:</strong> By firing VBlank at cycle 12,480 (not 0),
    /// we maintain synchronization with Apple IIe 80-column firmware which rapidly toggles
    /// PAGE2 during visible scanlines and relies on VBlank flag ($C019) for timing.
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
        _io.UpdateVBlankCounter(_VblankBlackoutCounter);
         

        if (_systemClock >= _nextVblankCycle)
        {
            // Catch up if emulator ran fast (unthrottled batches)
            // Advance VBlank cycle by full frame duration (17,030 cycles)
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
    /// <item>Reset I/O handler (soft switches, keyboard latch, button states)</item>
    /// <item>Reset address space controller (memory ranges, banking)</item>
    /// <item>Reset CPU (PC loaded from $FFFC/$FFFD, SP = $FF)</item>
    /// <item>Reset system clock to zero</item>
    /// <item>Reset VBlank cycle counter to 17,030 (first VBlank at cycle 17,030)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>⚠️ Temporal Coupling Warning:</strong> The order of _io.Reset() and _addressSpace.Reset()
    /// may matter if there are dependencies between I/O state and memory banking state. This should
    /// be reviewed and documented or refactored to eliminate ordering dependencies.
    /// </para>
    /// <para>
    /// <strong>Difference from UserReset:</strong> Reset() is a cold boot that clears everything.
    /// <see cref="UserReset"/> is a warm reset that has the same implementation currently but may
    /// preserve memory contents in a future enhancement.
    /// </para>
    /// <para>
    /// <strong>Thread Context:</strong> Must be called from emulator thread only.
    /// </para>
    /// </remarks>
    public void Reset()
    {
        ThrowIfDisposed();
        //TODO: Check for temporal coupling between _io and _addressSpace resets
        _io.Reset();
        _addressSpace.Reset();
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
    /// <item>Reset I/O handler (soft switches, keyboard latch, button states)</item>
    /// <item>Reset address space controller (memory ranges, banking)</item>
    /// <item>Reset CPU (PC loaded from $FFFC/$FFFD)</item>
    /// <item>Reset system clock to zero</item>
    /// <item>Reset VBlank cycle counter to 17,030</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>⚠️ Temporal Coupling Warning:</strong> The order of _io.Reset() and _addressSpace.Reset()
    /// may matter if there are dependencies between I/O state and memory banking state. This should
    /// be reviewed and documented or refactored to eliminate ordering dependencies.
    /// </para>
    /// <para>
    /// <strong>Current Implementation Note:</strong> In the current implementation, UserReset() and
    /// Reset() are identical. Future enhancements may preserve more state during UserReset (e.g.,
    /// RAM contents, some soft switches) to better match Apple IIe Ctrl+Reset behavior, which
    /// preserves memory while resetting the CPU.
    /// </para>
    /// <para>
    /// <strong>Thread Context:</strong> Must be called from emulator thread only.
    /// </para>
    /// </remarks>
    public void UserReset()
    {
        ThrowIfDisposed();
        //TODO: Check for temporal coupling between _io and _addressSpace resets
        _io.Reset();
        _addressSpace.Reset();
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
