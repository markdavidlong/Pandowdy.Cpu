using System.Runtime.CompilerServices;
using Pandowdy.EmuCore.Interfaces;


namespace Pandowdy.EmuCore;

/// <summary>
/// Routes read/write operations to main or auxiliary RAM based on Apple IIe soft switch states.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Purpose:</strong> The SystemRamSelector manages the complex memory banking logic
/// for the lower 48KB ($0000-$BFFF) of the Apple IIe address space, deciding whether each
/// read or write operation should target main memory, auxiliary memory, or floating bus.
/// </para>
/// <para>
/// <strong>Memory Regions Managed:</strong>
/// <list type="bullet">
/// <item>$0000-$01FF: Zero page/stack (controlled by ALTZP)</item>
/// <item>$0200-$03FF: Primary text page (controlled by 80STORE and PAGE2)</item>
/// <item>$0400-$07FF: Primary text page extension (controlled by 80STORE and PAGE2)</item>
/// <item>$0800-$1FFF: General RAM (controlled by RAMRD/RAMWRT)</item>
/// <item>$2000-$3FFF: Hi-res graphics page 1 (controlled by 80STORE, HIRES, and PAGE2)</item>
/// <item>$4000-$BFFF: General RAM (controlled by RAMRD/RAMWRT)</item>
/// </list>
/// </para>
/// <para>
/// <strong>Soft Switch Dependencies:</strong>
/// <list type="bullet">
/// <item><strong>ALTZP:</strong> When set, $0000-$01FF accesses auxiliary memory</item>
/// <item><strong>RAMRD:</strong> When set, reads from specified ranges use auxiliary memory</item>
/// <item><strong>RAMWRT:</strong> When set, writes to specified ranges use auxiliary memory</item>
/// <item><strong>80STORE:</strong> When set, overrides RAMRD/RAMWRT for text/graphics pages</item>
/// <item><strong>PAGE2:</strong> With 80STORE, selects auxiliary memory for text/graphics</item>
/// <item><strong>HIRES:</strong> With 80STORE, affects whether PAGE2 applies to $2000-$3FFF</item>
/// </list>
/// </para>
/// <para>
/// <strong>80STORE Behavior:</strong> When the 80STORE soft switch is enabled, memory access
/// for the text pages ($0400-$07FF) and hi-res page 1 ($2000-$3FFF) is controlled by the
/// PAGE2 switch instead of RAMRD/RAMWRT. This allows independent control of display memory
/// for 80-column mode and double hi-res graphics.
/// </para>
/// <para>
/// <strong>Performance:</strong> All read/write methods use <see cref="MethodImplOptions.AggressiveInlining"/>
/// for maximum performance, as this code is executed hundreds of thousands of times per second
/// during emulation.
/// </para>
/// <para>
/// <strong>Floating Bus:</strong> When auxiliary memory is not installed and a read targets aux memory,
/// the floating bus provider returns a pseudo-random value simulating the Apple IIe's floating bus behavior.
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> Not thread-safe. Designed for single-threaded CPU execution on the
/// emulator thread. The <see cref="ISystemStatusProvider"/> provides the necessary soft switch state.
/// </para>
/// </remarks>
public class SystemRamSelector(
    ISystemRam mainRam,
    ISystemRam? auxRam,
    IFloatingBusProvider floatingBus,
    ISystemStatusProvider status) : ISystemRamSelector
{
    /// <summary>
    /// Required size for main and auxiliary RAM banks (48KB each).
    /// </summary>
    private const int RequiredRamSize = 0xC000; // 48KB


    private readonly ISystemRam _mainRam = Utility.ValidateIPandowdyMemorySize(mainRam, nameof(mainRam), RequiredRamSize);
    private readonly ISystemRam? _auxRam = auxRam != null ? Utility.ValidateIPandowdyMemorySize(auxRam, nameof(auxRam), RequiredRamSize) : null;
    private readonly IFloatingBusProvider _floatingBus = floatingBus ?? throw new ArgumentNullException(nameof(floatingBus));
    private readonly ISystemStatusProvider _status = status ?? throw new ArgumentNullException(nameof(status));

    /// <summary>
    /// Copies the entire main memory bank (48KB) into the provided destination span.
    /// </summary>
    /// <param name="destination">
    /// Destination span that must be at least 48KB (0xC000 bytes) in size.
    /// </param>
    /// <remarks>
    /// Used by the video renderer to capture a snapshot of main memory for frame rendering
    /// without interfering with CPU execution. The snapshot includes all visible memory
    /// regions (text pages, graphics pages, etc.).
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown if the destination span is smaller than 48KB.
    /// </exception>
    public void CopyMainMemoryIntoSpan(Span<byte> destination) { _mainRam.CopyIntoSpan(destination); }
    
    /// <summary>
    /// Copies the entire auxiliary memory bank (48KB) into the provided destination span.
    /// </summary>
    /// <param name="destination">
    /// Destination span that must be at least 48KB (0xC000 bytes) in size.
    /// </param>
    /// <returns>
    /// <c>true</c> if auxiliary memory exists and was copied; <c>false</c> if auxiliary memory
    /// is not installed (base 64KB Apple IIe configuration).
    /// </returns>
    /// <remarks>
    /// Used by the video renderer to capture a snapshot of auxiliary memory for 80-column mode
    /// and double hi-res graphics rendering. If auxiliary memory is not installed, the destination
    /// span is left unchanged and the method returns <c>false</c>.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown if the destination span is smaller than 48KB.
    /// </exception>
    public bool CopyAuxMemoryIntoSpan(Span<byte> destination)
    {
        if (auxRam != null)
        {
            _auxRam!.CopyIntoSpan(destination);
            return true;
        }
        return false;
    }


    /// <summary>
    /// Gets the size of the addressable system RAM space (48KB).
    /// </summary>
    /// <value>
    /// Always returns 0xC000 (49,152 bytes), representing the $0000-$BFFF address range
    /// managed by this RAM selector.
    /// </value>
    /// <remarks>
    /// This does not include the upper memory areas ($C000-$FFFF) which are managed by
    /// the Language Card and system I/O handlers.
    /// </remarks>
    public int Size => RequiredRamSize; // 48kb addressable space


    /// <summary>
    /// Reads a byte directly from main memory, bypassing soft switch logic.
    /// </summary>
    /// <param name="address">Physical address within main RAM (0-65535, masked to 16-bit).</param>
    /// <returns>Byte value from main RAM at the specified address.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Direct Access:</strong> This method bypasses the soft switch mapping system
    /// and always reads from main memory, regardless of RAMRD, ALTZP, or 80STORE settings.
    /// </para>
    /// <para>
    /// <strong>Use Cases:</strong>
    /// <list type="bullet">
    /// <item>Video renderer reading raw display memory</item>
    /// <item>Debugger/memory viewer showing actual RAM contents</item>
    /// <item>Disk I/O routines that need predictable memory access</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> Aggressively inlined for hot-path execution.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte ReadRawMain(int address)
    {
        return _mainRam[(ushort)(address & 0xffff)];
    }


    /// <summary>
    /// Reads a byte directly from auxiliary memory, bypassing soft switch logic.
    /// </summary>
    /// <param name="address">Physical address within auxiliary RAM (0-65535, masked to 16-bit).</param>
    /// <returns>
    /// Byte value from auxiliary RAM if installed; otherwise, a floating bus value simulating
    /// the behavior of an Apple IIe with no auxiliary memory card.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Direct Access:</strong> This method bypasses the soft switch mapping system
    /// and always reads from auxiliary memory (if present), regardless of RAMRD, ALTZP, or
    /// 80STORE settings.
    /// </para>
    /// <para>
    /// <strong>Floating Bus:</strong> If auxiliary memory is not installed, returns a value
    /// from the floating bus provider, simulating the unpredictable data that appears on the
    /// bus when reading from non-existent memory in the real Apple IIe hardware.
    /// </para>
    /// <para>
    /// <strong>Use Cases:</strong>
    /// <list type="bullet">
    /// <item>80-column display renderer reading character data from aux memory</item>
    /// <item>Double hi-res graphics renderer accessing aux graphics pages</item>
    /// <item>Debugger showing raw auxiliary memory contents</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> Aggressively inlined for hot-path execution.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte ReadRawAux(int address)
    {
        return _auxRam == null ? _floatingBus.Read() : _auxRam[(ushort) (address & 0xffff)];
    }

           
    /// <summary>
    /// Reads a byte from system RAM, routing to main or auxiliary memory based on soft switch states.
    /// </summary>
    /// <param name="address">Address to read from ($0000-$BFFF).</param>
    /// <returns>Byte value from the selected memory bank (main, auxiliary, or floating bus).</returns>
    /// <remarks>
    /// <para>
    /// <strong>Routing Logic:</strong> The method determines which memory bank to access based
    /// on the address and current soft switch states:
    /// <code>
    /// $0000-$01FF: ALTZP controls main vs aux
    /// $0200-$03FF: 80STORE ? PAGE2 : RAMRD
    /// $0400-$07FF: 80STORE ? PAGE2 : RAMRD
    /// $0800-$1FFF: RAMRD controls main vs aux
    /// $2000-$3FFF: 80STORE ? (HIRES ? PAGE2 : RAMRD) : RAMRD
    /// $4000-$BFFF: RAMRD controls main vs aux
    /// </code>
    /// </para>
    /// <para>
    /// <strong>80STORE Special Case:</strong> When 80STORE is enabled, the text pages ($0400-$07FF)
    /// and potentially hi-res page 1 ($2000-$3FFF, if HIRES is set) use PAGE2 instead of RAMRD
    /// to select memory. This allows the display controller and CPU to access different memory
    /// banks, enabling smooth page-flipping for 80-column mode.
    /// </para>
    /// <para>
    /// <strong>Floating Bus:</strong> If auxiliary memory is requested but not installed, returns
    /// a floating bus value instead of crashing.
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> Aggressively inlined and optimized for the most common cases.
    /// This is one of the hottest code paths in the emulator.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte Read(ushort address)
    {
        bool readAux;

        if (address < 0x200)
        {
            readAux = _status.StateAltZp;
        }
        else if (address < 0x400 || (address >= 0x800 && address < 0x2000) || address >= 0x4000)
        {
            readAux = _status.StateRamRd;
           
        }
        else if (!_status.State80Store)
        {
            readAux = _status.StateRamRd;
        }
        else
        {
            if (address < 0x800) // 0x400-7ff
            {
                readAux = _status.StatePage2;
            }
            else // 0x2000-3fff)
            {
                if (_status.StateHiRes)
                {
                    readAux = _status.StatePage2;
                }
                else
                {
                    readAux = _status.StateRamRd;
                }
            }
        }

        IPandowdyMemory? targetMemory = readAux ? _auxRam : _mainRam;
        return targetMemory != null ? targetMemory[address] : _floatingBus.Read();
    }

    
    /// <summary>
    /// Writes a byte to system RAM, routing to main or auxiliary memory based on soft switch states.
    /// </summary>
    /// <param name="address">Address to write to ($0000-$BFFF).</param>
    /// <param name="data">Byte value to write.</param>
    /// <remarks>
    /// <para>
    /// <strong>Routing Logic:</strong> The method determines which memory bank to write to based
    /// on the address and current soft switch states:
    /// <code>
    /// $0000-$01FF: ALTZP controls main vs aux
    /// $0200-$03FF: 80STORE ? PAGE2 : RAMWRT
    /// $0400-$07FF: 80STORE ? PAGE2 : RAMWRT
    /// $0800-$1FFF: RAMWRT controls main vs aux
    /// $2000-$3FFF: 80STORE ? (HIRES ? PAGE2 : RAMWRT) : RAMWRT
    /// $4000-$BFFF: RAMWRT controls main vs aux
    /// </code>
    /// </para>
    /// <para>
    /// <strong>80STORE Special Case:</strong> Same logic as <see cref="Read"/>, but uses RAMWRT
    /// instead of RAMRD as the default selector for non-page-switched regions.
    /// </para>
    /// <para>
    /// <strong>Write to Missing Aux Memory:</strong> If auxiliary memory is requested but not
    /// installed, the write is silently ignored (no-op), matching Apple IIe hardware behavior.
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> Aggressively inlined for hot-path execution.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(ushort address, byte data)
    {
        bool writeAux;

        if (address < 0x200)
        {
            writeAux = _status.StateAltZp;
        }
        else if (address < 0x400 || (address >= 0x800 && address < 0x2000) || address >= 0x4000)
        {
            writeAux = _status.StateRamRd;

        }
        else if (!_status.State80Store)
        {
            writeAux = _status.StateRamRd;
        }
        else
        {
            if (address < 0x800) // 0x400-7ff
            {
                writeAux = _status.StatePage2;
            }
            else // 0x2000-3fff)
            {
                if (_status.StateHiRes)
                {
                    writeAux = _status.StatePage2;
                }
                else
                {
                    writeAux = _status.StateRamRd;
                }
            }
        }

        IPandowdyMemory? targetMemory = writeAux ? _auxRam : _mainRam;
        if (targetMemory != null)
        {
            targetMemory[address] = data;
        }
        else
        {
            // No Op. Possibly FloatingBus.Write() later on?
        }
    }


    /// <summary>
    /// Gets or sets a byte at the specified address using indexer syntax.
    /// </summary>
    /// <param name="address">Address to access ($0000-$BFFF).</param>
    /// <returns>Byte value from the selected memory bank.</returns>
    /// <remarks>
    /// Provides array-like syntax for memory access: <c>memory[0x1000] = 0x42;</c>
    /// Delegates to <see cref="Read"/> and <see cref="Write"/> methods, which handle
    /// all soft switch logic for main/auxiliary memory selection.
    /// </remarks>
    public byte this[ushort address]
    {
        get => Read(address);
        set => Write(address, value);
    }
}

