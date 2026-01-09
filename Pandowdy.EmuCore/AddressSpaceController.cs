
using Emulator;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Services;

namespace Pandowdy.EmuCore;

/// <summary>
/// Event arguments for memory access notifications to non-UI consumers.
/// </summary>
/// <remarks>
/// Used to notify observers (debuggers, trace logs, etc.) when memory is read or written.
/// The <see cref="Value"/> is null for read notifications, non-null for write notifications.
/// </remarks>
public sealed class MemoryAccessEventArgs : EventArgs
{
    /// <summary>
    /// Gets the 16-bit address that was accessed ($0000-$FFFF).
    /// </summary>
    public ushort Address { get; init; }
    
    /// <summary>
    /// Gets the byte value that was written, or null if this was a read operation.
    /// </summary>
    public byte? Value { get; init; }
}


public sealed class AddressSpaceController : IMemory, IMemoryAccessNotifier, IDirectMemoryPoolReader,  IDisposable
{
    //Methods from IMemory:
    
    /// <summary>
    /// Gets the size of the addressable memory space (always 64KB for 6502).
    /// </summary>
    /// <value>65,536 bytes ($0000-$FFFF).</value>
    /// <remarks>
    /// The 6502 has a 16-bit address bus, providing 64KB of address space. The actual
    /// physical memory may be larger (128KB main+aux + ROM), but it's accessed through
    /// this 64KB window via soft switch-controlled bank switching.
    /// </remarks>
    public int Size { get => 0x10000;  } // 64k addressable space

    /// <summary>
    /// Reads a byte from the specified address in the mapped memory space.
    /// </summary>
    /// <param name="address">16-bit address to read from ($0000-$FFFF).</param>
    /// <returns>Byte value at the mapped physical location for this address.</returns>
    /// <remarks>
    /// The physical memory accessed depends on current soft switch states (RAMRD, ALTZP,
    /// etc.). This method delegates to <see cref="ReadMapped"/> which performs the
    /// region-based lookup.
    /// </remarks>
    public byte Read(ushort address) => ReadMapped(address);
    
    /// <summary>
    /// Writes a byte to the specified address in the mapped memory space.
    /// </summary>
    /// <param name="address">16-bit address to write to ($0000-$FFFF).</param>
    /// <param name="value">Byte value to write.</param>
    /// <remarks>
    /// The physical memory accessed depends on current soft switch states (RAMWRT, ALTZP,
    /// etc.). Write-protected regions (ROM, unmapped I/O) are silently ignored. This method
    /// delegates to <see cref="WriteMapped"/>.
    /// </remarks>
    public void Write (ushort address, byte value) => WriteMapped(address, value);

    /// <summary>
    /// Gets or sets a byte at the specified address (indexer syntax).
    /// </summary>
    /// <param name="address">16-bit address ($0000-$FFFF).</param>
    /// <returns>Byte value at the mapped physical location.</returns>
    /// <remarks>
    /// Provides array-like syntax for memory access: <c>memory[0x1000] = 0x42;</c>
    /// Delegates to <see cref="ReadMapped"/> and <see cref="WriteMapped"/>.
    /// </remarks>
    public byte this[ushort address]
    {
        get => ReadMapped(address);
        set => WriteMapped(address, value);
    }

    //Methods from IMemoryAccessNotifier:

    /// <summary>
    /// Event raised when memory is written to.
    /// </summary>
    /// <remarks>
    /// Consumers (debuggers, trace logs, memory viewers) can subscribe to this event
    /// to monitor memory writes. The event includes the address and value written.
    /// Only fires for successful writes (not write-protected regions).
    /// </remarks>
    public event EventHandler<MemoryAccessEventArgs>? MemoryWritten;

    /// <summary>
    /// Event raised when memory is read from.
    /// </summary>
    /// <remarks>
    /// Currently not implemented (no reads trigger this event). Reserved for future
    /// use by debuggers or profilers that need to track memory access patterns.
    /// </remarks>
#pragma warning disable CS0067 // Event is never used - reserved for future debugger/profiler support
    public event EventHandler<MemoryAccessEventArgs>? MemoryRead;
#pragma warning restore CS0067


    //Methods from IDirectMemoryPoolReader:

    /// <summary>
    /// Reads directly from the main memory bank, bypassing soft switch mapping.
    /// </summary>
    /// <param name="address">Physical address in the pool (0-65535 for main bank).</param>
    /// <returns>Byte value at the physical location.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Direct Access:</strong> This method bypasses the soft switch mapping system
    /// and reads directly from the physical main memory bank. Used by debuggers, memory
    /// viewers, and video renderers that need to see actual RAM contents regardless of
    /// current bank switching.
    /// </para>
    /// <para>
    /// <strong>Example:</strong> The video renderer needs to read from hi-res page 2 even
    /// if PAGE2 is off, so it uses this method instead of the normal <see cref="Read"/> method.
    /// </para>
    /// </remarks>
    public byte ReadRawMain(int address) => _systemRam.ReadRawMain(address);
    
    /// <summary>
    /// Reads directly from the auxiliary memory bank, bypassing soft switch mapping.
    /// </summary>
    /// <param name="address">Physical address in the pool (0-65535 for aux bank).</param>
    /// <returns>Byte value at the physical location.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Direct Access:</strong> Reads from the auxiliary 64KB bank (offset 0x10000
    /// in the pool). Used for 80-column display, double hi-res graphics, and debugging.
    /// </para>
    /// <para>
    /// <strong>Example:</strong> 80-column text mode interleaves main and auxiliary memory
    /// for each character position, so the renderer uses this method to access aux memory
    /// directly.
    /// </para>
    /// </remarks>

    public byte ReadRawAux(int address) => _systemRam.ReadRawAux(address);
    /// <summary>
    /// The backing store - single contiguous byte array containing all memory regions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Size:</strong> 163,072 bytes (0x27F00):
    /// <list type="bullet">
    /// <item>Main RAM: 64KB (offset 0x00000)</item>
    /// <item>Auxiliary RAM: 64KB (offset 0x10000)</item>
    /// <item>ROM/I/O: 16KB + 4KB (offset 0x20000)</item>
    /// <item>Slot ROMs: ~19KB (offset 0x24000)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> Single allocation eliminates GC pressure and provides
    /// cache-friendly memory layout. All Memory&lt;byte&gt; slices reference this array.
    /// </para>
    /// </remarks>
    private readonly byte[] _pool;
    



    public enum Ranges
    {
        Region_0000_01FF = 0,
        Region_0200_03FF = 0x200,
        Region_0400_07FF = 0x400,
        Region_0800_1FFF = 0x800,
        Region_2000_3FFF = 0x2000,
        Region_4000_5FFF = 0x4000,
        Region_6000_BFFF = 0x6000,
        Region_C000_C0FF = 0xC000,
        Region_C100_C1FF = 0xC100,
        Region_C200_C2FF = 0xC200,
        Region_C300_C3FF = 0xC300,
        Region_C400_C4FF = 0xC400,
        Region_C500_C5FF = 0xC500,
        Region_C600_C6FF = 0xC600,
        Region_C700_C7FF = 0xC700,
        Region_C800_CFFF = 0xC800,
        Region_D000_DFFF = 0xD000,
        Region_E000_FFFF = 0xE000,
        Region_SysRam = 0xFFFFFE,
        Region_LangCard = 0xFFFFFF
    }


    // Nullable maps allow unmapped / write-protected regions; instance (was static)
    private readonly Dictionary<Ranges, Memory<byte>?> _readRanges = [];

    private readonly Dictionary<Ranges, Memory<byte>?> _writeRanges = [];


    // Region slices (instance readonly; previously static)


    private readonly Memory<byte> _io;

    private readonly Memory<byte> _int1;
    private readonly Memory<byte> _int2;
    private readonly Memory<byte> _int3;
    private readonly Memory<byte> _int4;
    private readonly Memory<byte> _int5;
    private readonly Memory<byte> _int6;
    private readonly Memory<byte> _int7;
    private readonly Memory<byte> _intext;


    private readonly Memory<byte> _s1;
    private readonly Memory<byte> _s2;
    private readonly Memory<byte> _s3;
    private readonly Memory<byte> _s4;
    private readonly Memory<byte> _s5;
    private readonly Memory<byte> _s6;
    private readonly Memory<byte> _s7;

    private readonly Memory<byte> _s1ext;
    private readonly Memory<byte> _s2ext;
    private readonly Memory<byte> _s3ext;
    private readonly Memory<byte> _s4ext;
    private readonly Memory<byte> _s5ext;
    private readonly Memory<byte> _s6ext;
    private readonly Memory<byte> _s7ext;

    private const int RequiredRamSize = 0xC000; // 48KB


    private ISystemStatusProvider _status;

    private ILanguageCard _langCard;
    private ISystemRamSelector _systemRam;

    public ISystemRamSelector SystemRam { get => _systemRam; }

    public AddressSpaceController(
        ISystemStatusProvider status,
        ILanguageCard langCard,
        ISystemRamSelector systemRam,
        int poolSize = 0x27F00,
        bool randomInit = false)
    {
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(langCard);
        ArgumentNullException.ThrowIfNull(systemRam);

        _status = status;
        _langCard = langCard;
        _systemRam = Utility.ValidateIMemorySize(systemRam, nameof(systemRam), RequiredRamSize);

        // Subscribe to memory mapping changes
        _status.MemoryMappingChanged += OnMemoryMappingChanged;

        _pool = new byte[poolSize];
        if (randomInit)
        {
            var rnd = new Random();
            rnd.NextBytes(_pool);
        }
        // Fill _pool with the value 0xA0
        else
        {
            for (int i = 0; i < _pool.Length; i++)
            {
                _pool[i] = 0x00; //0xA0;
            }
        }

        // Create slices (offset, length) exactly as before
        //_m1 = _pool.AsMemory(0x0000, 0x0200); // Default
        //_m2 = _pool.AsMemory(0x0200, 0x0200); // Default
        //_m3 = _pool.AsMemory(0x0400, 0x0400); // Default
        //_m4 = _pool.AsMemory(0x0800, 0x1800); // Default
        //_m5 = _pool.AsMemory(0x2000, 0x2000); // Default
        //_m6 = _pool.AsMemory(0x4000, 0x2000); // Default
        //_m7 = _pool.AsMemory(0x6000, 0x6000); // Default


        //_a1 = _pool.AsMemory(0x10000, 0x0200);
        //_a2 = _pool.AsMemory(0x10200, 0x0200);
        //_a3 = _pool.AsMemory(0x10400, 0x0400);
        //_a4 = _pool.AsMemory(0x10800, 0x1800);
        //_a5 = _pool.AsMemory(0x12000, 0x2000);
        //_a6 = _pool.AsMemory(0x14000, 0x2000);
        //_a7 = _pool.AsMemory(0x16000, 0x6000);


        _io = _pool.AsMemory(0x20000, 0x0100);

        _int1 = _pool.AsMemory(0x20100, 0x0100);
        _int2 = _pool.AsMemory(0x20200, 0x0100);
        _int3 = _pool.AsMemory(0x20300, 0x0100);
        _int4 = _pool.AsMemory(0x20400, 0x0100);
        _int5 = _pool.AsMemory(0x20500, 0x0100);
        _int6 = _pool.AsMemory(0x20600, 0x0100);
        _int7 = _pool.AsMemory(0x20700, 0x0100);
        _intext = _pool.AsMemory(0x20800, 0x0800); // Default


        _s1 = _pool.AsMemory(0x24000, 0x0100); // Default
        _s2 = _pool.AsMemory(0x24100, 0x0100); // Default
        _s3 = _pool.AsMemory(0x24200, 0x0100); // Default
        _s4 = _pool.AsMemory(0x24300, 0x0100); // Default
        _s5 = _pool.AsMemory(0x24400, 0x0100); // Default
        _s6 = _pool.AsMemory(0x24500, 0x0100); // Default
        _s7 = _pool.AsMemory(0x24600, 0x0100); // Default

        _s1ext = _pool.AsMemory(0x24700, 0x0800);
        _s2ext = _pool.AsMemory(0x24F00, 0x0800);
        _s3ext = _pool.AsMemory(0x25700, 0x0800);
        _s4ext = _pool.AsMemory(0x25F00, 0x0800);
        _s5ext = _pool.AsMemory(0x26700, 0x0800);
        _s6ext = _pool.AsMemory(0x26F00, 0x0800);
        _s7ext = _pool.AsMemory(0x27700, 0x0800);

        SetDefaultReadRanges();
        SetDefaultWriteRanges();
    }

    private void OnMemoryMappingChanged(object? sender, SystemStatusSnapshot e)
    {
        UpdateMemoryMappings();
    }

    public void Reset()
    {
        SetDefaultReadRanges();
        SetDefaultWriteRanges();

        // Reset IO when installed
    }

    private void SetDefaultReadRanges()
    {
        _readRanges[Ranges.Region_C000_C0FF] = _io;
        _readRanges[Ranges.Region_C100_C1FF] = _int1;
        _readRanges[Ranges.Region_C200_C2FF] = _int2;
        _readRanges[Ranges.Region_C300_C3FF] = _int3;
        _readRanges[Ranges.Region_C400_C4FF] = _int4;
        _readRanges[Ranges.Region_C500_C5FF] = _int5;
        _readRanges[Ranges.Region_C600_C6FF] = _int6;
        _readRanges[Ranges.Region_C700_C7FF] = _int7;
        _readRanges[Ranges.Region_C800_CFFF] = _intext;

    }

    private void SetDefaultWriteRanges()
    {
        _writeRanges[Ranges.Region_C000_C0FF] = _io;
        _writeRanges[Ranges.Region_C100_C1FF] = null;
        _writeRanges[Ranges.Region_C200_C2FF] = null;
        _writeRanges[Ranges.Region_C300_C3FF] = null;
        _writeRanges[Ranges.Region_C400_C4FF] = null;
        _writeRanges[Ranges.Region_C500_C5FF] = null;
        _writeRanges[Ranges.Region_C600_C6FF] = null;
        _writeRanges[Ranges.Region_C700_C7FF] = null;
        _writeRanges[Ranges.Region_C800_CFFF] = null;

    }



    public byte ReadPool(int address) => _pool[address];

    public void WritePool(int address, byte value) => _pool[address] = value;

    private byte ReadFromRegion(Ranges region, int address)
    {
        _mappingLock.EnterReadLock();
        try
        {
            _readRanges.TryGetValue(region, out var mem);
            if (!mem.HasValue)
            { return 0; }
            var m = mem.Value;
            int baseAddr = (int) region;
            int offset = address - baseAddr;
            if ((uint) offset >= m.Length)
            { return 0; }
            return m.Span[offset];
        }
        finally
        {
            _mappingLock.ExitReadLock();
        }
    }

    private bool WriteToRegion(Ranges region, int address, byte value)
    {
        _mappingLock.EnterReadLock();
        try
        {
            _writeRanges.TryGetValue(region, out var mem);
            if (!mem.HasValue)
            {
                return false;
            }

            var m = mem.Value;
            int baseAddr = (int) region;
            int offset = address - baseAddr;
            if ((uint) offset >= m.Length)
            {
                return false;
            }

            m.Span[offset] = value;
            return true;
        }
        finally
        {
            _mappingLock.ExitReadLock();
        }
    }

    public byte ReadMapped(ushort address) => address switch
    {

        >= (ushort) Ranges.Region_E000_FFFF => _langCard.Read(address),
        >= (ushort) Ranges.Region_D000_DFFF => _langCard.Read(address),
        >= (ushort) Ranges.Region_C800_CFFF => ReadFromRegion(Ranges.Region_C800_CFFF, address),
        >= (ushort) Ranges.Region_C700_C7FF => ReadFromRegion(Ranges.Region_C700_C7FF, address),
        >= (ushort) Ranges.Region_C600_C6FF => ReadFromRegion(Ranges.Region_C600_C6FF, address),
        >= (ushort) Ranges.Region_C500_C5FF => ReadFromRegion(Ranges.Region_C500_C5FF, address),
        >= (ushort) Ranges.Region_C400_C4FF => ReadFromRegion(Ranges.Region_C400_C4FF, address),
        >= (ushort) Ranges.Region_C300_C3FF => ReadFromRegion(Ranges.Region_C300_C3FF, address),
        >= (ushort) Ranges.Region_C200_C2FF => ReadFromRegion(Ranges.Region_C200_C2FF, address),
        >= (ushort) Ranges.Region_C100_C1FF => ReadFromRegion(Ranges.Region_C100_C1FF, address),
        >= (ushort) Ranges.Region_C000_C0FF => ReadFromRegion(Ranges.Region_C000_C0FF, address),
        >= (ushort) Ranges.Region_6000_BFFF => _systemRam.Read(address),
        >= (ushort) Ranges.Region_4000_5FFF => _systemRam.Read(address),
        >= (ushort) Ranges.Region_2000_3FFF => _systemRam.Read(address),
        >= (ushort) Ranges.Region_0800_1FFF => _systemRam.Read(address),
        >= (ushort) Ranges.Region_0400_07FF => _systemRam.Read(address),
        >= (ushort) Ranges.Region_0200_03FF => _systemRam.Read(address),
        _ => _systemRam.Read(address)
    };

    public void WriteMapped(ushort address, byte value)
    {

        var range = address switch
        {
            >= (ushort) Ranges.Region_E000_FFFF => Ranges.Region_LangCard,
            >= (ushort) Ranges.Region_D000_DFFF => Ranges.Region_LangCard,
            >= (ushort) Ranges.Region_C800_CFFF => Ranges.Region_C800_CFFF,
            >= (ushort) Ranges.Region_C700_C7FF => Ranges.Region_C700_C7FF,
            >= (ushort) Ranges.Region_C600_C6FF => Ranges.Region_C600_C6FF,
            >= (ushort) Ranges.Region_C500_C5FF => Ranges.Region_C500_C5FF,
            >= (ushort) Ranges.Region_C400_C4FF => Ranges.Region_C400_C4FF,
            >= (ushort) Ranges.Region_C300_C3FF => Ranges.Region_C300_C3FF,
            >= (ushort) Ranges.Region_C200_C2FF => Ranges.Region_C200_C2FF,
            >= (ushort) Ranges.Region_C100_C1FF => Ranges.Region_C100_C1FF,
            >= (ushort) Ranges.Region_C000_C0FF => Ranges.Region_C000_C0FF,
            >= (ushort) Ranges.Region_6000_BFFF => Ranges.Region_SysRam,
            >= (ushort) Ranges.Region_4000_5FFF => Ranges.Region_SysRam,
            >= (ushort) Ranges.Region_2000_3FFF => Ranges.Region_SysRam,
            >= (ushort) Ranges.Region_0800_1FFF => Ranges.Region_SysRam,
            >= (ushort) Ranges.Region_0400_07FF => Ranges.Region_SysRam,
            >= (ushort) Ranges.Region_0200_03FF => Ranges.Region_SysRam,
            _ => Ranges.Region_SysRam
        };
        var validWrite = true;
        if (range == Ranges.Region_SysRam)
        {
            _systemRam.Write(address, value);
        }
        else if(range == Ranges.Region_LangCard)
        {
            _langCard.Write(address, value);
        }
        else
        {
            validWrite = WriteToRegion(range, address, value);
        }
        if (!validWrite)
        {
       //     Debug.WriteLine($"Write to unmapped address {address:X4} ignored."); return;
        }
        MemoryWritten?.Invoke(this, new MemoryAccessEventArgs { Address = address, Value = value});

    }



    public void UpdateMemoryMappings()
    {

        bool _intCxRom = _status.StateIntCxRom;
        bool _slotC3Rom = _status.StateSlotC3Rom;

        _mappingLock.EnterWriteLock();
        try
        {

            // This will take the place of the card mechanism for now.
            bool[] hasCard = [false, true, true, true, true, true, true, true]; // Slots 0-7 (0 -> unused)

            _readRanges[Ranges.Region_C100_C1FF] = _intCxRom ? _int1 : (hasCard[1] ? _s1 : _int1);
            _readRanges[Ranges.Region_C200_C2FF] = _intCxRom ? _int2 : (hasCard[2] ? _s2 : _int2);
            _readRanges[Ranges.Region_C300_C3FF] = _intCxRom ? _int3 : (hasCard[3] ? _s3 : _int3);
            _readRanges[Ranges.Region_C400_C4FF] = _intCxRom ? _int4 : (hasCard[4] ? _s4 : _int4);
            _readRanges[Ranges.Region_C500_C5FF] = _intCxRom ? _int5 : (hasCard[5] ? _s5 : _int5);
            _readRanges[Ranges.Region_C600_C6FF] = _intCxRom ? _int6 : (hasCard[6] ? _s6 : _int6);
            _readRanges[Ranges.Region_C700_C7FF] = _intCxRom ? _int7 : (hasCard[7] ? _s7 : _int7);
            _readRanges[Ranges.Region_C800_CFFF] = _intext; // TODO: This is hardcoded to internal rom right now

            // Region_C300_C3FF
            _readRanges[Ranges.Region_C300_C3FF] = (_intCxRom || !_slotC3Rom) ? _int3 : _s3;
        }
        finally
        {
            _mappingLock.ExitWriteLock();
        }
    }

    // Thread synchronization for memory mapping updates
    private readonly ReaderWriterLockSlim _mappingLock = new(LockRecursionPolicy.NoRecursion);

    public void Dispose()
    {
        // Unsubscribe from events
        if (_status != null)
        {
            _status.MemoryMappingChanged -= OnMemoryMappingChanged;
        }

        // Dispose logic if needed
        _mappingLock?.Dispose();
    }

    public void InstallApple2ROM(byte[] rom)
    {
        // check rom size and throw if not 16k
        if (rom.Length != 0x4000)
        {
            throw new Exception("Apple IIe ROM must be exactly 16KB in size.");
        }

        rom.AsSpan(0x0000, 0x0100).CopyTo(_io.Span);

        rom.AsSpan(0x0100, 0x0100).CopyTo(_int1.Span);
        rom.AsSpan(0x0200, 0x0100).CopyTo(_int2.Span);
        rom.AsSpan(0x0300, 0x0100).CopyTo(_int3.Span);
        rom.AsSpan(0x0400, 0x0100).CopyTo(_int4.Span);
        rom.AsSpan(0x0500, 0x0100).CopyTo(_int5.Span);
        rom.AsSpan(0x0600, 0x0100).CopyTo(_int6.Span);
        rom.AsSpan(0x0700, 0x0100).CopyTo(_int7.Span);

        rom.AsSpan(0x0800, 0x0800).CopyTo(_intext.Span);
    }
}
