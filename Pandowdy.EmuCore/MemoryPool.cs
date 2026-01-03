//------------------------------------------------------------------------------
// MemoryPool.cs
//
// ⚠️ PERFORMANCE-OPTIMIZED IMPLEMENTATION - PLANNED FOR REFACTORING ⚠️
//
// This class implements Apple IIe memory management using a slice-based approach
// where all memory regions are allocated from a single backing pool and mapped
// dynamically based on soft switch states. While this provides excellent
// performance, it trades clarity for speed.
//
// DESIGN RATIONALE:
// The Apple IIe's 128KB memory space (64KB main + 64KB auxiliary) plus ROM and
// I/O regions are all carved from a single byte[] pool. Memory accesses are
// mapped to slices via switch expressions, eliminating allocation overhead and
// providing cache-friendly sequential access patterns.
//
// PERFORMANCE BENEFITS:
// - Single allocation (no GC pressure from multiple arrays)
// - Cache-friendly contiguous memory layout
// - Fast slice-based remapping (just pointer arithmetic)
// - Lock-based thread safety for mapping updates
//
// CLARITY TRADE-OFFS:
// - Complex slice management (40+ Memory<byte> fields)
// - Non-obvious address mapping logic
// - Harder to understand Apple IIe memory model at first glance
// - Tight coupling between soft switches and memory layout
//
// FUTURE REFACTORING:
// The planned refactoring will prioritize clarity:
// - Explicit memory region classes (MainRAM, AuxiliaryRAM, ROM, etc.)
// - Clear separation between physical memory and address space mapping
// - Strategy pattern for soft switch-based mapping rules
// - Better abstraction of Apple IIe memory architecture
//
// For now, this implementation works well and is thoroughly tested. The
// refactoring will improve maintainability without sacrificing performance.
//------------------------------------------------------------------------------

using Emulator;
using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore
{
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

    /// <summary>
    /// Manages Apple IIe memory (128KB RAM + ROM/I/O) using a slice-based pool architecture.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>⚠️ PERFORMANCE-OPTIMIZED DESIGN:</strong> This class uses a single backing
    /// array with memory slices to implement the Apple IIe's complex memory architecture.
    /// While fast, this design trades clarity for performance and will be refactored to
    /// improve maintainability in the future.
    /// </para>
    /// <para>
    /// <strong>Apple IIe Memory Architecture:</strong> The Apple IIe has 128KB of RAM
    /// (64KB main + 64KB auxiliary) plus 16KB of ROM and 4KB of I/O space. Memory is
    /// accessed through a 16-bit address space ($0000-$FFFF), but the actual physical
    /// memory accessed depends on soft switch settings.
    /// </para>
    /// <para>
    /// <strong>Memory Layout:</strong>
    /// <code>
    /// Pool Layout (163,072 bytes total):
    /// 
    /// Main Memory (64KB):
    ///   $0000-$01FF: _m1  (512B)  - Zero Page + Stack
    ///   $0200-$03FF: _m2  (512B)  - Input Buffer
    ///   $0400-$07FF: _m3  (1KB)   - Text Page 1
    ///   $0800-$1FFF: _m4  (6KB)   - Main RAM
    ///   $2000-$3FFF: _m5  (8KB)   - Hi-Res Page 1
    ///   $4000-$5FFF: _m6  (8KB)   - Hi-Res Page 2
    ///   $6000-$BFFF: _m7  (24KB)  - Main RAM
    ///   $C000-$CFFF: _m8a (4KB)   - Language Card Bank 1
    ///   $D000-$DFFF: _m8b (4KB)   - Language Card Bank 2
    ///   $E000-$FFFF: _m9  (8KB)   - Language Card High
    /// 
    /// Auxiliary Memory (64KB):
    ///   $0000-$01FF: _a1  (512B)  - Aux Zero Page + Stack
    ///   $0200-$03FF: _a2  (512B)  - Aux Input Buffer
    ///   $0400-$07FF: _a3  (1KB)   - Aux Text Page 1
    ///   $0800-$1FFF: _a4  (6KB)   - Aux RAM
    ///   $2000-$3FFF: _a5  (8KB)   - Aux Hi-Res Page 1
    ///   $4000-$5FFF: _a6  (8KB)   - Aux Hi-Res Page 2
    ///   $6000-$BFFF: _a7  (24KB)  - Aux RAM
    ///   $C000-$CFFF: _a8a (4KB)   - Aux Language Card Bank 1
    ///   $D000-$DFFF: _a8b (4KB)   - Aux Language Card Bank 2
    ///   $E000-$FFFF: _a9  (8KB)   - Aux Language Card High
    /// 
    /// ROM/I/O (16KB + 4KB):
    ///   $C000-$C0FF: _io      (256B) - I/O Space
    ///   $C100-$C7FF: _int1-7  (7×256B) - Internal ROM (per slot)
    ///   $C800-$CFFF: _intext  (2KB)  - Extended Internal ROM
    ///   $D000-$DFFF: _rom1    (4KB)  - Monitor ROM Bank 1
    ///   $E000-$FFFF: _rom2    (8KB)  - Monitor ROM Bank 2 + Reset Vector
    /// 
    /// Slot ROMs (7 × 256B + 7 × 2KB extensions):
    ///   $C100-$C7FF: _s1-7    (7×256B) - Slot ROM (1 page per slot)
    ///   $C800-$CFFF: _s1ext-7 (7×2KB)  - Slot ROM extensions
    /// </code>
    /// </para>
    /// <para>
    /// <strong>Soft Switch Mapping:</strong> The Apple IIe uses soft switches to control
    /// which physical memory is mapped into the 64KB address space:
    /// <list type="bullet">
    /// <item><strong>RAMRD/RAMWRT:</strong> Select main vs auxiliary RAM for reads/writes</item>
    /// <item><strong>ALTZP:</strong> Select main vs auxiliary zero page and stack</item>
    /// <item><strong>80STORE:</strong> Page 2 selection for text/hi-res pages</item>
    /// <item><strong>HIRES:</strong> Hi-res page selection when 80STORE is active</item>
    /// <item><strong>PAGE2:</strong> Select page 1 vs page 2 for video</item>
    /// <item><strong>INTCXROM:</strong> Internal ROM vs slot ROMs ($C100-$C7FF)</item>
    /// <item><strong>SLOTC3ROM:</strong> Slot 3 ROM vs internal ROM</item>
    /// <item><strong>Language Card:</strong> Bank switching for $D000-$FFFF</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Slice-Based Performance:</strong> All memory regions are sliced from a single
    /// backing array. Mapping updates change which slices are active for each address range,
    /// providing fast remapping without copying data. Read/write operations use switch
    /// expressions for efficient address-to-slice lookup.
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> Memory mapping updates (soft switch changes) use a
    /// <see cref="ReaderWriterLockSlim"/> to allow concurrent reads while serializing
    /// mapping updates. This is important since the bus is accessed from the CPU thread
    /// while soft switches may be toggled from UI or I/O operations.
    /// </para>
    /// <para>
    /// <strong>Future Refactoring:</strong> This class will be refactored to improve clarity:
    /// <list type="bullet">
    /// <item>Explicit memory region classes instead of generic slices</item>
    /// <item>Strategy pattern for soft switch mapping rules</item>
    /// <item>Better separation between physical memory and address space</item>
    /// <item>Clearer documentation of Apple IIe memory model</item>
    /// </list>
    /// The refactoring will maintain performance while improving maintainability.
    /// </para>
    /// </remarks>
    public sealed class MemoryPool : IMemory, IMemoryAccessNotifier, IDirectMemoryPoolReader, ISoftSwitchResponder, IDisposable
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
        public System.Int32 Size { get => 0x10000;  } // 64k addressable space

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
        public byte ReadRawMain(int address) => _pool[(address & 0xffff)];
        
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
        public byte ReadRawAux(int address) => _pool[(address & 0xffff) | 0x10000];



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
        
        /// <summary>
        /// Gets the backing store array (for advanced/debugging use).
        /// </summary>
        /// <remarks>
        /// Exposes the raw pool for scenarios that need direct memory access (save states,
        /// memory dumps, advanced debugging). Use with caution - modifying the pool directly
        /// bypasses all soft switch logic and mapping.
        /// </remarks>
        public byte[] Pool => _pool;

        /// <summary>
        /// Memory address ranges used for region-based mapping.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <strong>Purpose:</strong> The Apple IIe's 64KB address space is divided into
        /// regions based on how soft switches affect them. Each range has independent
        /// mapping rules.
        /// </para>
        /// <para>
        /// <strong>Region Boundaries:</strong>
        /// <list type="bullet">
        /// <item>$0000-$01FF: Zero page + Stack (ALTZP controls)</item>
        /// <item>$0200-$03FF: Input buffer (RAMRD/RAMWRT control)</item>
        /// <item>$0400-$07FF: Text page 1 (80STORE + PAGE2 control)</item>
        /// <item>$0800-$1FFF: Main low RAM (RAMRD/RAMWRT control)</item>
        /// <item>$2000-$3FFF: Hi-res page 1 (80STORE + HIRES + PAGE2 control)</item>
        /// <item>$4000-$5FFF: Hi-res page 2 (RAMRD/RAMWRT control)</item>
        /// <item>$6000-$BFFF: Main high RAM (RAMRD/RAMWRT control)</item>
        /// <item>$C000-$C0FF: I/O space (always I/O)</item>
        /// <item>$C100-$C7FF: Slot ROMs (INTCXROM + SLOTC3ROM control, per-slot)</item>
        /// <item>$C800-$CFFF: Extended ROM (slot-selected or internal)</item>
        /// <item>$D000-$DFFF: Language card bank (bank switching)</item>
        /// <item>$E000-$FFFF: Language card high + reset vector (bank switching)</item>
        /// </list>
        /// </para>
        /// </remarks>
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
            Region_E000_FFFF = 0xE000
        }


        // Soft switch state (Apple IIe memory management)
        
        /// <summary>
        /// RAMRD soft switch - controls auxiliary memory selection for reads.
        /// </summary>
        /// <remarks>
        /// When true, reads from most address ranges access auxiliary memory instead of main
        /// memory. Exceptions: Zero page (controlled by ALTZP) and regions affected by 80STORE.
        /// </remarks>
        private bool _ramRd = false;
        
        /// <summary>
        /// RAMWRT soft switch - controls auxiliary memory selection for writes.
        /// </summary>
        /// <remarks>
        /// When true, writes to most address ranges go to auxiliary memory instead of main
        /// memory. Exceptions: Zero page (controlled by ALTZP) and regions affected by 80STORE.
        /// </remarks>
        private bool _ramWrt = false;
        
        /// <summary>
        /// ALTZP soft switch - controls auxiliary zero page and stack selection.
        /// </summary>
        /// <remarks>
        /// When true, $0000-$01FF (zero page + stack) access auxiliary memory. This is
        /// independent of RAMRD/RAMWRT and affects both reads and writes. Critical for
        /// programs that use auxiliary memory as their primary memory space.
        /// </remarks>
        private bool _altZp = false;
        
        /// <summary>
        /// 80STORE soft switch - enables page 2 selection for text and hi-res pages.
        /// </summary>
        /// <remarks>
        /// When true, PAGE2 controls which physical memory is accessed for text page 1
        /// ($0400-$07FF) and optionally hi-res page 1 ($2000-$3FFF, if HIRES is true).
        /// Used for 80-column text mode where main and aux memory are interleaved.
        /// </remarks>
        private bool _80Store = false;
        
        /// <summary>
        /// HIRES soft switch - enables hi-res graphics mode.
        /// </summary>
        /// <remarks>
        /// When true AND 80STORE is true, PAGE2 controls which memory is accessed for
        /// $2000-$3FFF. Used for double hi-res graphics where main and aux memory provide
        /// 16 colors instead of 6.
        /// </remarks>
        private bool _hires = false;
        
        /// <summary>
        /// PAGE2 soft switch - selects page 2 for video display and memory access.
        /// </summary>
        /// <remarks>
        /// Primary effect is video display (show page 1 vs page 2). When 80STORE is enabled,
        /// also controls which memory bank is accessed for text and hi-res pages. PAGE2=false
        /// uses main memory, PAGE2=true uses auxiliary memory.
        /// </remarks>
        private bool _page2 = false;
        
        /// <summary>
        /// INTCXROM soft switch - enables internal ROM in $C100-$C7FF range.
        /// </summary>
        /// <remarks>
        /// When true, $C100-$C7FF accesses internal ROM (peripheral diagnostics, etc.)
        /// instead of slot ROMs. When false, each slot's ROM is accessible in its 256-byte
        /// region. Exception: SLOTC3ROM can override for slot 3.
        /// </remarks>
        private bool _intCxRom = false;
        
        /// <summary>
        /// SLOTC3ROM soft switch - enables slot 3 ROM even when INTCXROM is set.
        /// </summary>
        /// <remarks>
        /// When true, slot 3 ROM ($C300-$C3FF) is accessible even if INTCXROM is true.
        /// Allows the 80-column firmware to remain accessible while using internal ROM
        /// for other slots. When false, INTCXROM controls all slots uniformly.
        /// </remarks>
        private bool _slotC3Rom = false;

        // Language Card soft switches (bank switching for $D000-$FFFF)
        
        /// <summary>
        /// Language Card write enable - allows writes to $D000-$FFFF.
        /// </summary>
        /// <remarks>
        /// When false, $D000-$FFFF is write-protected (ROM behavior). When true, writes go
        /// to RAM (main or auxiliary depending on ALTZP). The Language Card required two
        /// sequential accesses to enable writes (PREWRITE then HIGHWRITE) to prevent
        /// accidental ROM overwrites.
        /// </remarks>
        private bool _highWrite = false;
        
        /// <summary>
        /// Language Card bank selection - selects bank 1 vs bank 2 for $D000-$DFFF.
        /// </summary>
        /// <remarks>
        /// The Language Card has two 4KB banks for $D000-$DFFF. Bank 1 is typically used
        /// for the main program, bank 2 for alternate code or data. $E000-$FFFF always
        /// accesses the same 8KB region regardless of bank selection.
        /// </remarks>
        private bool _bank1 = false;
        
        /// <summary>
        /// Language Card read enable - reads from RAM instead of ROM for $D000-$FFFF.
        /// </summary>
        /// <remarks>
        /// When false, $D000-$FFFF reads from ROM (monitor, reset vector). When true, reads
        /// from RAM (main or auxiliary depending on ALTZP), allowing programs to use the
        /// Language Card's 16KB RAM space.
        /// </remarks>
        private bool _highRead = false;
        
        /// <summary>
        /// Language Card pre-write state - first step in enabling writes.
        /// </summary>
        /// <remarks>
        /// The Language Card requires two sequential accesses to soft switch addresses to
        /// enable writes. This prevents accidental writes to ROM by requiring intentional
        /// access patterns. PREWRITE tracks the first access; HIGHWRITE is set on the second.
        /// </remarks>
        private bool _preWrite = false;


        // Nullable maps allow unmapped / write-protected regions; instance (was static)
        private readonly Dictionary<Ranges, Memory<byte>?> _readRanges = [];

        private readonly Dictionary<Ranges, Memory<byte>?> _writeRanges = [];


        // Region slices (instance readonly; previously static)
        private readonly Memory<byte> _m1;

        private readonly Memory<byte> _m2;
        private readonly Memory<byte> _m3;
        private readonly Memory<byte> _m4;
        private readonly Memory<byte> _m5;
        private readonly Memory<byte> _m6;
        private readonly Memory<byte> _m7;
        private readonly Memory<byte> _m8a;
        private readonly Memory<byte> _m8b;
        private readonly Memory<byte> _m9;

        private readonly Memory<byte> _a1;
        private readonly Memory<byte> _a2;
        private readonly Memory<byte> _a3;
        private readonly Memory<byte> _a4;
        private readonly Memory<byte> _a5;
        private readonly Memory<byte> _a6;
        private readonly Memory<byte> _a7;
        private readonly Memory<byte> _a8a;
        private readonly Memory<byte> _a8b;
        private readonly Memory<byte> _a9;

        private readonly Memory<byte> _io;

        private readonly Memory<byte> _int1;
        private readonly Memory<byte> _int2;
        private readonly Memory<byte> _int3;
        private readonly Memory<byte> _int4;
        private readonly Memory<byte> _int5;
        private readonly Memory<byte> _int6;
        private readonly Memory<byte> _int7;
        private readonly Memory<byte> _intext;
        private readonly Memory<byte> _rom1;
        private readonly Memory<byte> _rom2;

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

        public MemoryPool(int poolSize = 0x27F00, bool randomInit = false)
        {
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
            _m1 = _pool.AsMemory(0x0000, 0x0200); // Default
            _m2 = _pool.AsMemory(0x0200, 0x0200); // Default
            _m3 = _pool.AsMemory(0x0400, 0x0400); // Default
            _m4 = _pool.AsMemory(0x0800, 0x1800); // Default
            _m5 = _pool.AsMemory(0x2000, 0x2000); // Default
            _m6 = _pool.AsMemory(0x4000, 0x2000); // Default
            _m7 = _pool.AsMemory(0x6000, 0x6000); // Default
            _m8a = _pool.AsMemory(0xC000, 0x1000);
            _m8b = _pool.AsMemory(0xD000, 0x1000);
            _m9 = _pool.AsMemory(0xE000, 0x2000);

            _a1 = _pool.AsMemory(0x10000, 0x0200);
            _a2 = _pool.AsMemory(0x10200, 0x0200);
            _a3 = _pool.AsMemory(0x10400, 0x0400);
            _a4 = _pool.AsMemory(0x10800, 0x1800);
            _a5 = _pool.AsMemory(0x12000, 0x2000);
            _a6 = _pool.AsMemory(0x14000, 0x2000);
            _a7 = _pool.AsMemory(0x16000, 0x6000);
            _a8a = _pool.AsMemory(0x1C000, 0x1000);
            _a8b = _pool.AsMemory(0x1D000, 0x1000);
            _a9 = _pool.AsMemory(0x1E000, 0x2000);

            _io = _pool.AsMemory(0x20000, 0x0100);

            _int1 = _pool.AsMemory(0x20100, 0x0100);
            _int2 = _pool.AsMemory(0x20200, 0x0100);
            _int3 = _pool.AsMemory(0x20300, 0x0100);
            _int4 = _pool.AsMemory(0x20400, 0x0100);
            _int5 = _pool.AsMemory(0x20500, 0x0100);
            _int6 = _pool.AsMemory(0x20600, 0x0100);
            _int7 = _pool.AsMemory(0x20700, 0x0100);
            _intext = _pool.AsMemory(0x20800, 0x0800); // Default

            _rom1 = _pool.AsMemory(0x21000, 0x1000); // Default
            _rom2 = _pool.AsMemory(0x22000, 0x2000); // Default
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

        public void ResetRanges()
        {
            SetDefaultReadRanges();
            SetDefaultWriteRanges();
        }

        private void SetDefaultReadRanges()
        {
            _readRanges[Ranges.Region_0000_01FF] = _m1;
            _readRanges[Ranges.Region_0200_03FF] = _m2;
            _readRanges[Ranges.Region_0400_07FF] = _m3;
            _readRanges[Ranges.Region_0800_1FFF] = _m4;
            _readRanges[Ranges.Region_2000_3FFF] = _m5;
            _readRanges[Ranges.Region_4000_5FFF] = _m6;
            _readRanges[Ranges.Region_6000_BFFF] = _m7;
            _readRanges[Ranges.Region_C000_C0FF] = _io;
            _readRanges[Ranges.Region_C100_C1FF] = _int1;
            _readRanges[Ranges.Region_C200_C2FF] = _int2;
            _readRanges[Ranges.Region_C300_C3FF] = _int3;
            _readRanges[Ranges.Region_C400_C4FF] = _int4;
            _readRanges[Ranges.Region_C500_C5FF] = _int5;
            _readRanges[Ranges.Region_C600_C6FF] = _int6;
            _readRanges[Ranges.Region_C700_C7FF] = _int7;
            _readRanges[Ranges.Region_C800_CFFF] = _intext;
            _readRanges[Ranges.Region_D000_DFFF] = _rom1;
            _readRanges[Ranges.Region_E000_FFFF] = _rom2;
        }

        private void SetDefaultWriteRanges()
        {
            _writeRanges[Ranges.Region_0000_01FF] = _m1;
            _writeRanges[Ranges.Region_0200_03FF] = _m2;
            _writeRanges[Ranges.Region_0400_07FF] = _m3;
            _writeRanges[Ranges.Region_0800_1FFF] = _m4;
            _writeRanges[Ranges.Region_2000_3FFF] = _m5;
            _writeRanges[Ranges.Region_4000_5FFF] = _m6;
            _writeRanges[Ranges.Region_6000_BFFF] = _m7;
            _writeRanges[Ranges.Region_C000_C0FF] = _io;
            _writeRanges[Ranges.Region_C100_C1FF] = null;
            _writeRanges[Ranges.Region_C200_C2FF] = null;
            _writeRanges[Ranges.Region_C300_C3FF] = null;
            _writeRanges[Ranges.Region_C400_C4FF] = null;
            _writeRanges[Ranges.Region_C500_C5FF] = null;
            _writeRanges[Ranges.Region_C600_C6FF] = null;
            _writeRanges[Ranges.Region_C700_C7FF] = null;
            _writeRanges[Ranges.Region_C800_CFFF] = null;
            _writeRanges[Ranges.Region_D000_DFFF] = null;
            _writeRanges[Ranges.Region_E000_FFFF] = null;
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
            >= (ushort) Ranges.Region_E000_FFFF => ReadFromRegion(Ranges.Region_E000_FFFF, address),
            >= (ushort) Ranges.Region_D000_DFFF => ReadFromRegion(Ranges.Region_D000_DFFF, address),
            >= (ushort) Ranges.Region_C800_CFFF => ReadFromRegion(Ranges.Region_C800_CFFF, address),
            >= (ushort) Ranges.Region_C700_C7FF => ReadFromRegion(Ranges.Region_C700_C7FF, address),
            >= (ushort) Ranges.Region_C600_C6FF => ReadFromRegion(Ranges.Region_C600_C6FF, address),
            >= (ushort) Ranges.Region_C500_C5FF => ReadFromRegion(Ranges.Region_C500_C5FF, address),
            >= (ushort) Ranges.Region_C400_C4FF => ReadFromRegion(Ranges.Region_C400_C4FF, address),
            >= (ushort) Ranges.Region_C300_C3FF => ReadFromRegion(Ranges.Region_C300_C3FF, address),
            >= (ushort) Ranges.Region_C200_C2FF => ReadFromRegion(Ranges.Region_C200_C2FF, address),
            >= (ushort) Ranges.Region_C100_C1FF => ReadFromRegion(Ranges.Region_C100_C1FF, address),
            >= (ushort) Ranges.Region_C000_C0FF => ReadFromRegion(Ranges.Region_C000_C0FF, address),
            >= (ushort) Ranges.Region_6000_BFFF => ReadFromRegion(Ranges.Region_6000_BFFF, address),
            >= (ushort) Ranges.Region_4000_5FFF => ReadFromRegion(Ranges.Region_4000_5FFF, address),
            >= (ushort) Ranges.Region_2000_3FFF => ReadFromRegion(Ranges.Region_2000_3FFF, address),
            >= (ushort) Ranges.Region_0800_1FFF => ReadFromRegion(Ranges.Region_0800_1FFF, address),
            >= (ushort) Ranges.Region_0400_07FF => ReadFromRegion(Ranges.Region_0400_07FF, address),
            >= (ushort) Ranges.Region_0200_03FF => ReadFromRegion(Ranges.Region_0200_03FF, address),
            _ => ReadFromRegion(Ranges.Region_0000_01FF, address)
        };

        public void WriteMapped(ushort address, byte value)
        {

            var range = address switch
            {
                >= (ushort) Ranges.Region_E000_FFFF => Ranges.Region_E000_FFFF,
                >= (ushort) Ranges.Region_D000_DFFF => Ranges.Region_D000_DFFF,
                >= (ushort) Ranges.Region_C800_CFFF => Ranges.Region_C800_CFFF,
                >= (ushort) Ranges.Region_C700_C7FF => Ranges.Region_C700_C7FF,
                >= (ushort) Ranges.Region_C600_C6FF => Ranges.Region_C600_C6FF,
                >= (ushort) Ranges.Region_C500_C5FF => Ranges.Region_C500_C5FF,
                >= (ushort) Ranges.Region_C400_C4FF => Ranges.Region_C400_C4FF,
                >= (ushort) Ranges.Region_C300_C3FF => Ranges.Region_C300_C3FF,
                >= (ushort) Ranges.Region_C200_C2FF => Ranges.Region_C200_C2FF,
                >= (ushort) Ranges.Region_C100_C1FF => Ranges.Region_C100_C1FF,
                >= (ushort) Ranges.Region_C000_C0FF => Ranges.Region_C000_C0FF,
                >= (ushort) Ranges.Region_6000_BFFF => Ranges.Region_6000_BFFF,
                >= (ushort) Ranges.Region_4000_5FFF => Ranges.Region_4000_5FFF,
                >= (ushort) Ranges.Region_2000_3FFF => Ranges.Region_2000_3FFF,
                >= (ushort) Ranges.Region_0800_1FFF => Ranges.Region_0800_1FFF,
                >= (ushort) Ranges.Region_0400_07FF => Ranges.Region_0400_07FF,
                >= (ushort) Ranges.Region_0200_03FF => Ranges.Region_0200_03FF,
                _ => Ranges.Region_0000_01FF
            };
            var validWrite = WriteToRegion(range, address, value);
            if (!validWrite)
            {
           //     Debug.WriteLine($"Write to unmapped address {address:X4} ignored."); return;
            }
            MemoryWritten?.Invoke(this, new MemoryAccessEventArgs { Address = address, Value = value});

        }

        public void SetRamRd(bool ramRd)
        {
            _ramRd = ramRd;
            UpdateMemoryMappings();
        }

        public void SetRamWrt(bool ramWrt)
        {
            _ramWrt = ramWrt;
            UpdateMemoryMappings();
        }

        public void SetAltZp(bool altZp)
        {
            _altZp = altZp;
            UpdateMemoryMappings();
        }

        public void Set80Store(bool store80)
        {
            _80Store = store80;
            UpdateMemoryMappings();
        }

        public void SetHiRes(bool hires)
        {
            _hires = hires;
            UpdateMemoryMappings();
        }

        public void SetPage2(bool page2)
        {
            _page2 = page2;
            UpdateMemoryMappings();
        }

        public void SetIntCxRom(bool intCxRom)
        {           
            _intCxRom = intCxRom;
            
            UpdateMemoryMappings();
        }

        public void SetSlotC3Rom(bool slotC3Rom)
        {
            _slotC3Rom = slotC3Rom;
            UpdateMemoryMappings();
        }


        public void SetHighWrite(bool enabled)
        {
            _highWrite = enabled;
            UpdateMemoryMappings();
        }


        public void SetBank1(bool enabled)
        {
            _bank1 = enabled;
            UpdateMemoryMappings();
        }

        public void SetHighRead(bool enabled)
        {
            _highRead = enabled;
            UpdateMemoryMappings();
        }

        public void SetMixed(bool _) {  /* NA - display only */ }
        public void SetText(bool _) {  /* NA - display only */ }
        public void SetAn0(bool _) {  /* NA - display only */ }
        public void SetAn1(bool _) {  /* NA - display only */ }
        public void SetAn2(bool _) {  /* NA - display only */ }
        public void SetAn3(bool _) {  /* NA - display only */ }
        public void Set80Vid(bool _) {  /* NA - display only */ }
        public void SetAltChar(bool _) {  /* NA - display only */ }
        
        public void SetPreWrite(bool enabled)
        {
            _preWrite = enabled;
            UpdateMemoryMappings();
        }

        public void UpdateMemoryMappings()
        {
            _mappingLock.EnterWriteLock();
            try
            {
                _readRanges[Ranges.Region_0000_01FF] = _altZp ? _a1 : _m1;
                _writeRanges[Ranges.Region_0000_01FF] = _altZp ? _a1 : _m1;

                _readRanges[Ranges.Region_0200_03FF] = (_ramRd ? _a2 : _m2);
                _readRanges[Ranges.Region_0800_1FFF] = (_ramRd ? _a4 : _m4);
                _readRanges[Ranges.Region_4000_5FFF] = (_ramRd ? _a6 : _m6);
                _readRanges[Ranges.Region_6000_BFFF] = (_ramRd ? _a7 : _m7);

                _writeRanges[Ranges.Region_0200_03FF] = (_ramWrt ? _a2 : _m2);
                _writeRanges[Ranges.Region_0800_1FFF] = (_ramWrt ? _a4 : _m4);
                _writeRanges[Ranges.Region_4000_5FFF] = (_ramWrt ? _a6 : _m6);
                _writeRanges[Ranges.Region_6000_BFFF] = (_ramWrt ? _a7 : _m7);

                if (!_80Store)
                {
                    _readRanges[Ranges.Region_0400_07FF] = (_ramRd ? _a3 : _m3);
                    _readRanges[Ranges.Region_2000_3FFF] = (_ramRd ? _a5 : _m5);
                    _writeRanges[Ranges.Region_0400_07FF] = (_ramWrt ? _a3 : _m3);
                    _writeRanges[Ranges.Region_2000_3FFF] = (_ramWrt ? _a5 : _m5);
                }
                else
                {
                    _readRanges[Ranges.Region_0400_07FF] = (_page2 ? _a3 : _m3);
                    _writeRanges[Ranges.Region_0400_07FF] = (_page2 ? _a3 : _m3);
                    if (_hires)
                    {
                        _readRanges[Ranges.Region_2000_3FFF] = (_page2 ? _a5 : _m5);
                        _writeRanges[Ranges.Region_2000_3FFF] = (_page2 ? _a5 : _m5);
                    }
                    else
                    {
                        _readRanges[Ranges.Region_2000_3FFF] = (_ramRd ? _a5 : _m5);
                        _writeRanges[Ranges.Region_2000_3FFF] = (_ramWrt ? _a5 : _m5);
                    }
                }

                // Region_D000_DFFF -> Slot ROM
                // Write:
                if (_highWrite)
                {
                    if (_altZp) // Aux a8a/a8b + a9
                    { 
                        _writeRanges[Ranges.Region_D000_DFFF] = _bank1?_a8a:_a8b;
                        _writeRanges[Ranges.Region_E000_FFFF] = _a9;
                    }
                    else // Main m8a/m8b + m9
                    {
                        _writeRanges[Ranges.Region_D000_DFFF] = _bank1?_m8a:_m8b;
                        _writeRanges[Ranges.Region_E000_FFFF] = _m9;
                    }
                }
                else
                {
                    _writeRanges[Ranges.Region_D000_DFFF] = null;
                    _writeRanges[Ranges.Region_E000_FFFF] = null;
                }

                // Read:
                if (_highRead)
                {
                    if (_altZp) // Aux a8a/a8b + a9
                    {
                        _readRanges[Ranges.Region_D000_DFFF] = _bank1?_a8a:_a8b;
                        _readRanges[Ranges.Region_E000_FFFF] = _a9;
                    }
                    else // Main m8a/m8b + m9
                    {
                        _readRanges[Ranges.Region_D000_DFFF] = _bank1?_m8a:_m8b;
                        _readRanges[Ranges.Region_E000_FFFF] = _m9;
                    }
                }
                else
                {
                    _readRanges[Ranges.Region_D000_DFFF] = _rom1;
                    _readRanges[Ranges.Region_E000_FFFF] = _rom2;
                }

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

            // rom should be 16k with the rom data filling _io, _int1-_int7, _intext, _rom1, _rom2
            rom.AsSpan(0x0000, 0x0100).CopyTo(_io.Span);

            rom.AsSpan(0x0100, 0x0100).CopyTo(_int1.Span);
            rom.AsSpan(0x0200, 0x0100).CopyTo(_int2.Span);
            rom.AsSpan(0x0300, 0x0100).CopyTo(_int3.Span);
            rom.AsSpan(0x0400, 0x0100).CopyTo(_int4.Span);
            rom.AsSpan(0x0500, 0x0100).CopyTo(_int5.Span);
            rom.AsSpan(0x0600, 0x0100).CopyTo(_int6.Span);
            rom.AsSpan(0x0700, 0x0100).CopyTo(_int7.Span);

            rom.AsSpan(0x0800, 0x0800).CopyTo(_intext.Span);

            rom.AsSpan(0x1000, 0x1000).CopyTo(_rom1.Span);
            rom.AsSpan(0x2000, 0x2000).CopyTo(_rom2.Span);
        }
    }
}
