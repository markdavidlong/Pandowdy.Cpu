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

/// <summary>
/// Pure routing layer for Apple IIe address space, delegating to specialized memory subsystems.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Architecture:</strong> AddressSpaceController is a stateless router that delegates
/// all memory operations to specialized subsystems. It owns no memory itself and performs no
/// bank switching logic - all decisions are made by the subsystems.
/// </para>
/// <para>
/// <strong>Address Space Routing ($0000-$FFFF):</strong>
/// <list type="bullet">
/// <item><strong>$0000-$BFFF:</strong> System RAM (48KB) â†’ ISystemRamSelector (handles main/aux banking via soft switches)</item>
/// <item><strong>$C000-$C08F:</strong> System I/O (intercepted by VA2MBus, never reaches this class)</item>
/// <item><strong>$C090-$CFFF:</strong> Slot I/O and ROM (3952 bytes) â†’ ISlots (handles slot cards and internal ROM via SystemRomProvider)</item>
/// <item><strong>$D000-$FFFF:</strong> Language Card (12KB) â†’ ILanguageCard (handles RAM/ROM banking via SystemRomProvider)</item>
/// </list>
/// </para>
/// <para>
/// <strong>Subsystem Responsibilities:</strong>
/// <list type="bullet">
/// <item><strong>SystemRamSelector:</strong> Manages 128KB (64KB main + 64KB aux) with soft switch-driven banking</item>
/// <item><strong>Slots:</strong> Manages slot card ROM, I/O, and internal ROM via SystemRomProvider</item>
/// <item><strong>LanguageCard:</strong> Manages 16KB ROM and language card RAM banking via SystemRomProvider</item>
/// </list>
/// </para>
/// <para>
/// <strong>Design Pattern:</strong> This class implements the Router pattern - it routes requests
/// to appropriate handlers without implementing business logic. All state management, soft switch
/// handling, and ROM management is delegated to injected subsystems.
/// </para>
/// <para>
/// <strong>Performance:</strong> Direct method delegation with switch expressions provides optimal
/// performance (~2-3 CPU cycles overhead per memory access). No locks, no dictionaries, no events.
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> This class is not thread-safe. All methods must be called from
/// the emulator worker thread. Cross-thread access should be coordinated by the bus (VA2MBus).
/// </para>
/// </remarks>
public sealed class AddressSpaceController : IMemory, IMemoryAccessNotifier, IDirectMemoryPoolReader, IDisposable
{
    /// <summary>
    /// Gets the size of the addressable memory space (always 64KB for 6502).
    /// </summary>
    /// <value>65,536 bytes ($0000-$FFFF).</value>
    /// <remarks>
    /// The 6502 has a 16-bit address bus, providing 64KB of address space. The actual
    /// physical memory may be larger (128KB main+aux + ROM), but it's accessed through
    /// this 64KB window via soft switch-controlled bank switching.
    /// </remarks>
    public int Size => 0x10000; // 64k addressable space

    /// <summary>
    /// Gets or sets a byte at the specified address (indexer syntax).
    /// </summary>
    /// <param name="address">16-bit address ($0000-$FFFF).</param>
    /// <returns>Byte value at the mapped physical location.</returns>
    /// <remarks>
    /// Provides array-like syntax for memory access: <c>memory[0x1000] = 0x42;</c>
    /// Delegates to <see cref="Read"/> and <see cref="Write"/>.
    /// </remarks>
    public byte this[ushort address]
    {
        get => Read(address);
        set => Write(address, value);
    }

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

    /// <summary>
    /// Reads directly from the main memory bank, bypassing soft switch mapping.
    /// </summary>
    /// <param name="address">Physical address (0-65535 for main bank).</param>
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
    /// <para>
    /// <strong>Delegation:</strong> Delegates to <see cref="IDirectMemoryPoolReader.ReadRawMain"/>.
    /// </para>
    /// </remarks>
    public byte ReadRawMain(int address) => _systemRam.ReadRawMain(address);
    
    /// <summary>
    /// Reads directly from the auxiliary memory bank, bypassing soft switch mapping.
    /// </summary>
    /// <param name="address">Physical address (0-65535 for aux bank).</param>
    /// <returns>Byte value at the physical location.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Direct Access:</strong> Reads from the auxiliary 64KB bank. Used for 80-column
    /// display, double hi-res graphics, and debugging.
    /// </para>
    /// <para>
    /// <strong>Example:</strong> 80-column text mode interleaves main and auxiliary memory
    /// for each character position, so the renderer uses this method to access aux memory
    /// directly.
    /// </para>
    /// <para>
    /// <strong>Delegation:</strong> Delegates to <see cref="IDirectMemoryPoolReader.ReadRawAux"/>.
    /// </para>
    /// </remarks>
    public byte ReadRawAux(int address) => _systemRam.ReadRawAux(address);

    /// <summary>
    /// Minimum required size for system RAM (48KB, $0000-$BFFF).
    /// </summary>
    private const int RequiredRamSize = 0xC000;

    /// <summary>
    /// System IO handling $C000-$C08F 
    /// </summary>
    private readonly ISystemIoHandler _io;

    /// <summary>
    /// Slot system handling $C090-$CFFF (slot I/O and ROM).
    /// </summary>
    private readonly ISlots _slots;

    /// <summary>
    /// Language card handling $D000-$FFFF (RAM/ROM banking).
    /// </summary>
    private readonly ILanguageCard _langCard;
    
    /// <summary>
    /// System RAM handling $0000-$BFFF (main and auxiliary memory).
    /// </summary>
    private readonly ISystemRamSelector _systemRam;


    /// <summary>
    /// Gets the system RAM selector for direct access to memory subsystem.
    /// </summary>
    /// <remarks>
    /// Used by video rendering and debugging to access raw memory contents.
    /// </remarks>
    public ISystemRamSelector SystemRam => _systemRam;

    /// <summary>
    /// Initializes a new instance of the <see cref="AddressSpaceController"/> class.
    /// </summary>
    /// <param name="langCard">Language card managing $D000-$FFFF RAM/ROM banking.</param>
    /// <param name="systemRam">System RAM managing $0000-$BFFF main and auxiliary memory.</param>
    /// <param name="ioHandler">System I/O handler managing $C000-$C08F keyboard, video, and soft switches.</param>
    /// <param name="slots">Slot system managing $C090-$CFFF slot I/O and ROM.</param>
    /// <exception cref="ArgumentNullException">Thrown if any parameter is null.</exception>
    /// <exception cref="ArgumentException">Thrown if systemRam doesn't meet minimum size requirement (48KB).</exception>
    /// <remarks>
    /// <para>
    /// <strong>Initialization:</strong> Stores references to injected subsystems. No memory
    /// allocation occurs - this is a pure routing layer with no state.
    /// </para>
    /// <para>
    /// <strong>Size Validation:</strong> The system RAM must be at least 48KB ($C000 bytes)
    /// to cover the $0000-$BFFF address range. Validation is performed via
    /// <see cref="Utility.ValidateIMemorySize"/>.
    /// </para>
    /// </remarks>
    public AddressSpaceController(
        ILanguageCard langCard,
        ISystemRamSelector systemRam,
        ISystemIoHandler ioHandler,
        ISlots slots)
    {
        ArgumentNullException.ThrowIfNull(langCard);
        ArgumentNullException.ThrowIfNull(systemRam);
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(ioHandler);

        _io = ioHandler;
        _langCard = langCard;
        _systemRam = Utility.ValidateIMemorySize(systemRam, nameof(systemRam), RequiredRamSize);
        _slots = slots;
    }

    /// <summary>
    /// Reads a byte from the specified address, routing to the appropriate subsystem.
    /// </summary>
    /// <param name="address">16-bit address to read from ($0000-$FFFF).</param>
    /// <returns>Byte value at the address.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if address is in $C000-$C08F range (should be intercepted by VA2MBus).
    /// </exception>
    /// <remarks>
    /// <para>
    /// <strong>Routing Logic:</strong>
    /// <list type="bullet">
    /// <item><strong>$E000-$FFFF, $D000-$DFFF:</strong> Language Card (ROM or banked RAM)</item>
    /// <item><strong>$C090-$CFFF:</strong> Slots (slot card ROM/I/O or internal ROM)</item>
    /// <item><strong>$C000-$C08F:</strong> ERROR - should never reach here (VA2MBus intercepts)</item>
    /// <item><strong>$0000-$BFFF:</strong> System RAM (main or aux based on soft switches)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> Direct delegation via switch expression provides optimal
    /// performance with minimal overhead (~2-3 CPU cycles).
    /// </para>
    /// </remarks>
    public byte Read(ushort address) => address switch
    {
        >= 0xE000 => _langCard.Read(address),        // $E000-$FFFF
        >= 0xD000 => _langCard.Read(address),        // $D000-$DFFF
        >= 0xC090 => _slots.Read((ushort)(address - 0xC000)), // $C090-$CFFF
        >= 0xC000 => _io.Read((ushort) (address - 0xC000)),
        _ => _systemRam.Read(address)                // $0000-$BFFF
    };

    public void Reset()
    {
        _slots.Reset();
        _io.Reset();
    }

    /// <summary>
    /// Writes a byte to the specified address, routing to the appropriate subsystem.
    /// </summary>
    /// <param name="address">16-bit address to write to ($0000-$FFFF).</param>
    /// <param name="value">Byte value to write.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if address is in $C000-$C08F range (should be intercepted by VA2MBus).
    /// </exception>
    /// <remarks>
    /// <para>
    /// <strong>Routing Logic:</strong> Same as <see cref="Read"/> - delegates to appropriate
    /// subsystem based on address range. Write-protected regions (ROM) are handled by the
    /// subsystems themselves.
    /// </para>
    /// <para>
    /// <strong>Event Notification:</strong> After successful write, the <see cref="MemoryWritten"/>
    /// event is raised for debugging/monitoring purposes. Note that the event fires even if the
    /// write was to ROM (silently ignored by subsystem).
    /// </para>
    /// </remarks>
    public void Write(ushort address, byte value)
    {
        switch (address)
        {
            case >= 0xE000:
            case >= 0xD000:
                _langCard.Write(address, value);
                break;

            case >= 0xC090:
                _slots.Write((ushort)(address - 0xC000), value);
                break;

            case >= 0xC000:
                _io.Write((ushort) (address - 0xC000), value);
                break;

            default: // $0000-$BFFF
                _systemRam.Write(address, value);
                break;
        }

        MemoryWritten?.Invoke(this, new MemoryAccessEventArgs { Address = address, Value = value });
    }

    /// <summary>
    /// Releases all resources used by this instance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Current Implementation:</strong> AddressSpaceController is now stateless and owns
    /// no resources. This method exists for IDisposable interface compliance and future expansion.
    /// </para>
    /// <para>
    /// <strong>Subsystem Disposal:</strong> The injected subsystems (_slots, _langCard, _systemRam)
    /// are owned by the DI container and should be disposed by their owner, not here.
    /// </para>
    /// </remarks>
    public void Dispose()
    {
        // AddressSpaceController is now stateless - nothing to dispose
        // Kept for IDisposable interface compliance and future expansion
        GC.SuppressFinalize(this);
    }
}
