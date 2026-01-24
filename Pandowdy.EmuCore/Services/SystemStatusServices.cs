using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.Services;

/// <summary>
/// Immutable snapshot of all Apple IIe system status flags and soft switch states.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Immutability:</strong> This record is immutable - once created, its state cannot change.
/// Any modification requires creating a new snapshot via <see cref="SystemStatusSnapshotBuilder"/>.
/// This ensures thread-safe reads and clear change tracking through the observable pattern.
/// </para>
/// <para>
/// <strong>Complete State Capture:</strong> Contains all soft switch states and I/O values for a single
/// point in time, including memory configuration, video modes, and game controller state.
/// This allows consumers (renderers, debuggers, UI) to work with a consistent view of system state.
/// </para>
/// <para>
/// <strong>Apple IIe Context:</strong> These flags directly correspond to Apple IIe hardware
/// soft switches and I/O registers, providing authentic emulation of the system's state
/// management. See Apple IIe Technical Reference Manual for detailed switch descriptions.
/// </para>
/// <para>
/// <strong>Keyboard State:</strong> Note that keyboard state (current key, strobe) is NOT included
/// in this snapshot. Keyboard state is managed entirely by the keyboard subsystem (SingularKeyHandler)
/// and read directly by SystemIoHandler when the CPU accesses $C000/$C010. This separation keeps
/// the keyboard subsystem truly independent.
/// </para>
/// <para>
/// <strong>Game Controller Synchronization:</strong> Button and paddle states are included in this
/// snapshot and automatically synchronized from IGameControllerStatus via event subscription. This
/// ensures UI and debuggers can observe controller state without directly accessing the controller.
/// </para>
/// </remarks>
/// <param name="State80Store">80STORE switch - redirects page 2 to aux memory ($C000/$C001).</param>
/// <param name="StateRamRd">RAMRD switch - reads from aux memory ($C002/$C003).</param>
/// <param name="StateRamWrt">RAMWRT switch - writes to aux memory ($C004/$C005).</param>
/// <param name="StateIntCxRom">INTCXROM switch - uses internal ROM vs slot ROMs ($C006/$C007).</param>
/// <param name="StateIntC8Rom">INTC8ROM switch - uses internal C800-CFFF ROM vs slot 3 ROM ($C008/$C009).</param>
/// <param name="StateAltZp">ALTZP switch - uses aux zero page and stack ($C008/$C009).</param>
/// <param name="StateSlotC3Rom">SLOTC3ROM switch - enables slot 3 ROM ($C00A/$C00B).</param>
/// <param name="StatePb0">Pushbutton 0 state (readable at $C061, bit 7) - synchronized from IGameControllerStatus.</param>
/// <param name="StatePb1">Pushbutton 1 state (readable at $C062, bit 7) - synchronized from IGameControllerStatus.</param>
/// <param name="StatePb2">Pushbutton 2 state (readable at $C063, bit 7) - synchronized from IGameControllerStatus.</param>
/// <param name="StateAnn0">Annunciator 0 state ($C058/$C059).</param>
/// <param name="StateAnn1">Annunciator 1 state ($C05A/$C05B).</param>
/// <param name="StateAnn2">Annunciator 2 state ($C05C/$C05D).</param>
/// <param name="StateAnn3_DGR">Annunciator 3 / Double Graphics state ($C05E/$C05F).</param>
/// <param name="StatePage2">PAGE2 switch - selects page 1 or 2 ($C054/$C055).</param>
/// <param name="StateHiRes">HIRES switch - graphics vs text ($C056/$C057).</param>
/// <param name="StateMixed">MIXED switch - mixed graphics and text ($C052/$C053).</param>
/// <param name="StateTextMode">TEXT switch - text mode on/off ($C050/$C051).</param>
/// <param name="StateShow80Col">80COL switch - 80-column display ($C00C/$C00D).</param>
/// <param name="StateAltCharSet">ALTCHARSET switch - alternate character set ($C00E/$C00F).</param>
/// <param name="StateFlashOn">Flash state for inverse/flashing characters (timing-based).</param>
/// <param name="StatePrewrite">PREWRITE flag for language card bank switching.</param>
/// <param name="StateUseBank1">Language card bank selection (bank 1 vs bank 2).</param>
/// <param name="StateHighRead">Language card read enable ($D000-$FFFF).</param>
/// <param name="StateHighWrite">Language card write enable ($D000-$FFFF).</param>
/// <param name="StateVBlank">Vertical blanking interval active (readable at $C019, bit 7).</param>
/// <param name="StatePdl0">Paddle 0 analog value 0-255 (timer readable at $C064, bit 7) - synchronized from IGameControllerStatus.</param>
/// <param name="StatePdl1">Paddle 1 analog value 0-255 (timer readable at $C064, bit 6) - synchronized from IGameControllerStatus.</param>
/// <param name="StatePdl2">Paddle 2 analog value 0-255 (timer readable at $C065, bit 7) - synchronized from IGameControllerStatus.</param>
/// <param name="StatePdl3">Paddle 3 analog value 0-255 (timer readable at $C065, bit 6) - synchronized from IGameControllerStatus.</param>
/// <param name="StateIntC8RomSlot">IntC8RomSlotValue</param>
/// <param name="StateCurrentMhz">Current emulated speed in Mhz></param>
public record SystemStatusSnapshot(
    bool State80Store,
    bool StateRamRd,
    bool StateRamWrt,
    bool StateIntCxRom,
    bool StateIntC8Rom,
    bool StateAltZp,
    bool StateSlotC3Rom,
    bool StatePb0,
    bool StatePb1,
    bool StatePb2,
    bool StateAnn0,
    bool StateAnn1,
    bool StateAnn2,
    bool StateAnn3_DGR,
    bool StatePage2,
    bool StateHiRes,
    bool StateMixed,
    bool StateTextMode,
    bool StateShow80Col,
    bool StateAltCharSet,
    bool StateFlashOn,
    bool StatePrewrite,
    bool StateUseBank1,
    bool StateHighRead,
    bool StateHighWrite,
    bool StateVBlank,
    byte StatePdl0,
    byte StatePdl1,
    byte StatePdl2,
    byte StatePdl3,
    byte StateIntC8RomSlot,
    double StateCurrentMhz
    );

/// <summary>
/// Provides observable system status tracking with mutation interface.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Dual Interface Implementation:</strong> This class implements both:
/// <list type="bullet">
/// <item><see cref="ISystemStatusProvider"/> - Read-only observable access for consumers
/// (renderers, debuggers, UI) to query and subscribe to system state changes.</item>
/// <item><see cref="ISystemStatusMutator"/> - Write interface for components that need to
/// update state (such as <see cref="SoftSwitches"/> when soft switches are toggled).</item>
/// </list>
/// </para>
/// <para>
/// <strong>Observable Pattern:</strong> Uses <see cref="System.Reactive.Subjects.BehaviorSubject{T}"/>
/// to provide reactive updates via <see cref="Stream"/>. The BehaviorSubject replays the
/// current state to new subscribers, ensuring they always have the latest snapshot.
/// Traditional .NET events are also supported via <see cref="Changed"/>.
/// </para>
/// <para>
/// <strong>Default State:</strong> Initializes to Apple IIe power-on defaults:
/// <list type="bullet">
/// <item>Text mode enabled</item>
/// <item>Internal ROM enabled (INTCXROM = true)</item>
/// <item>All other switches off</item>
/// </list>
/// This matches the behavior of a freshly powered-on Apple IIe before the monitor runs.
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> Updates via mutation methods are not inherently
/// thread-safe. The caller (typically <see cref="SoftSwitches"/>) must ensure serialized access
/// from a single thread. Reads are safe from any thread due to immutable snapshots.
/// </para>
/// <para>
/// <strong>Change Notification Order:</strong> When state changes, the observable stream
/// (<see cref="Stream"/>) is notified first, followed by the <see cref="Changed"/> event.
/// This ensures reactive subscribers see updates before event handlers.
/// </para>
/// </remarks>
public sealed class SystemStatusProvider : ISystemStatusMutator
{
    // Current system state snapshot (immutable)
    private SystemStatusSnapshot _current = new(
        State80Store: false,
        StateRamRd: false,
        StateRamWrt: false,
        StateIntCxRom: false,        
        StateIntC8Rom: false,
        StateAltZp: false,
        StateSlotC3Rom: false,
        StatePb0: false,
        StatePb1: false,
        StatePb2: false,
        StateAnn0: false,
        StateAnn1: false,
        StateAnn2: false,
        StateAnn3_DGR: false,
        StatePage2: false,
        StateHiRes: false,
        StateMixed: false,
        StateTextMode: true,        // Apple IIe powers on in text mode
        StateShow80Col: false,
        StateAltCharSet: false,
        StateFlashOn: false,
        StatePrewrite: false,
        StateUseBank1: false,
        StateHighRead: false,
        StateHighWrite: false,
        StateVBlank: false, // Apple IIe powers on outside VBlank period
        StatePdl0: 0,
        StatePdl1: 0,
        StatePdl2: 0,
        StatePdl3: 0,
        StateIntC8RomSlot: 0,
        StateCurrentMhz: -1.0);        

    // Reactive subject for observable pattern (replays current state to new subscribers)
    private readonly System.Reactive.Subjects.BehaviorSubject<SystemStatusSnapshot> _subject;

    /// <inheritdoc />
    public event EventHandler<SystemStatusSnapshot>? Changed;

    /// <inheritdoc />
    public event EventHandler<SystemStatusSnapshot>? MemoryMappingChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemStatusProvider"/> class.
    /// </summary>
    /// <param name="gameController">Optional game controller for automatic state synchronization.</param>
    /// <remarks>
    /// <para>
    /// Creates the reactive subject with the default power-on state, ensuring
    /// new subscribers immediately receive the current system status.
    /// </para>
    /// <para>
    /// <strong>Game Controller Integration:</strong> If a game controller is provided,
    /// this class subscribes to its change events and automatically updates the
    /// button and paddle state in the system status snapshot. This ensures all
    /// observers receive consistent controller state without requiring intermediate
    /// synchronization layers.
    /// </para>
    /// </remarks>
    public SystemStatusProvider(IGameControllerStatus? gameController = null)
    {
        _subject = new System.Reactive.Subjects.BehaviorSubject<SystemStatusSnapshot>(_current);
        
        // Subscribe to game controller events if provided
        if (gameController != null)
        {
            gameController.ButtonChanged += OnGameControllerButtonChanged;
            gameController.PaddleChanged += OnGameControllerPaddleChanged;
        }
    }

    /// <summary>
    /// Handles game controller button state changes and synchronizes with system status.
    /// </summary>
    /// <param name="sender">Event sender (game controller).</param>
    /// <param name="e">Event args containing button number and new state.</param>
    /// <remarks>
    /// <para>
    /// <strong>Direct Integration:</strong> SystemStatusProvider subscribes directly to
    /// game controller events, eliminating the need for intermediate synchronization
    /// through SystemIoHandler. This is the correct architectural layer for this
    /// synchronization since SystemStatus is the observable state container.
    /// </para>
    /// </remarks>
    private void OnGameControllerButtonChanged(object? sender, GameControllerButtonChangedEventArgs e)
    {
        // Update button state in system status
        switch (e.ButtonNumber)
        {
            case 0:
                SetButton0(e.IsPressed);
                break;
            case 1:
                SetButton1(e.IsPressed);
                break;
            case 2:
                SetButton2(e.IsPressed);
                break;
        }
    }

    /// <summary>
    /// Handles game controller paddle value changes and synchronizes with system status.
    /// </summary>
    /// <param name="sender">Event sender (game controller).</param>
    /// <param name="e">Event args containing paddle number and new value.</param>
    /// <remarks>
    /// <para>
    /// <strong>Direct Integration:</strong> SystemStatusProvider subscribes directly to
    /// game controller events, providing automatic synchronization of paddle values
    /// into the system status snapshot for all observers.
    /// </para>
    /// </remarks>
    private void OnGameControllerPaddleChanged(object? sender, GameControllerPaddleChangedEventArgs e)
    {
        // Update paddle value in system status
        switch (e.PaddleNumber)
        {
            case 0:
                SetPdl0(e.Value);
                break;
            case 1:
                SetPdl1(e.Value);
                break;
            case 2:
                SetPdl2(e.Value);
                break;
            case 3:
                SetPdl3(e.Value);
                break;
        }
    }

    #region ISystemStatusProvider - Read-only property access

    /// <inheritdoc />
    public bool State80Store => _current.State80Store;
    
    /// <inheritdoc />
    public bool StateRamRd => _current.StateRamRd;
    
    /// <inheritdoc />
    public bool StateRamWrt => _current.StateRamWrt;
    
    /// <inheritdoc />
    public bool StateIntCxRom => _current.StateIntCxRom;

    /// <inheritdoc />
    public bool StateIntC8Rom => _current.StateIntC8Rom;

    /// <inheritdoc />
    public byte StateIntC8RomSlot => _current.StateIntC8RomSlot;

    /// <inheritdoc />
    public bool StateAltZp => _current.StateAltZp;
    
    /// <inheritdoc />
    public bool StateSlotC3Rom => _current.StateSlotC3Rom;
    
    /// <inheritdoc />
    public bool StatePb0 => _current.StatePb0;
    
    /// <inheritdoc />
    public bool StatePb1 => _current.StatePb1;
    
    /// <inheritdoc />
    public bool StatePb2 => _current.StatePb2;
    
    /// <inheritdoc />
    public bool StateAnn0 => _current.StateAnn0;
    
    /// <inheritdoc />
    public bool StateAnn1 => _current.StateAnn1;
    
    /// <inheritdoc />
    public bool StateAnn2 => _current.StateAnn2;
    
    /// <inheritdoc />
    public bool StateAnn3_DGR => _current.StateAnn3_DGR;
    
    /// <inheritdoc />
    public bool StatePage2 => _current.StatePage2;
    
    /// <inheritdoc />
    public bool StateHiRes => _current.StateHiRes;
    
    /// <inheritdoc />
    public bool StateMixed => _current.StateMixed;
    
    /// <inheritdoc />
    public bool StateTextMode => _current.StateTextMode;
    
    /// <inheritdoc />
    public bool StateShow80Col => _current.StateShow80Col;
    
    /// <inheritdoc />
    public bool StateAltCharSet => _current.StateAltCharSet;
    
    /// <inheritdoc />
    public bool StateFlashOn => _current.StateFlashOn;
    
    /// <inheritdoc />
    public bool StatePreWrite => _current.StatePrewrite;
    
    /// <inheritdoc />
    public bool StateUseBank1 => _current.StateUseBank1;
    
    /// <inheritdoc />
    public bool StateHighRead => _current.StateHighRead;
    
    /// <inheritdoc />
    public bool StateHighWrite => _current.StateHighWrite;

    /// <inheritdoc />
    public bool StateVBlank => _current.StateVBlank;

    /// <inheritdoc />
    public byte Pdl0 => _current.StatePdl0;
    
    /// <inheritdoc />
    public byte Pdl1 => _current.StatePdl1;
    
    /// <inheritdoc />
    public byte Pdl2 => _current.StatePdl2;
    
    /// <inheritdoc />
    public byte Pdl3 => _current.StatePdl3;

    /// <inheritdoc />
    public double StateCurrentMhz => _current.StateCurrentMhz;

    /// <inheritdoc />
    public SystemStatusSnapshot Current => _current;
    
    /// <inheritdoc />
    public IObservable<SystemStatusSnapshot> Stream => _subject;

    #endregion

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>Builder Pattern:</strong> Accepts an action that modifies a
    /// <see cref="SystemStatusSnapshotBuilder"/>, allowing batch updates to multiple
    /// flags in a single atomic operation. This is more efficient than individual
    /// property updates when multiple switches change simultaneously.
    /// </para>
    /// <para>
    /// <strong>Notification Order:</strong>
    /// <list type="number">
    /// <item>Apply changes via builder</item>
    /// <item>Create new immutable snapshot</item>
    /// <item>Update <c>_current</c> field</item>
    /// <item>Notify <see cref="Stream"/> subscribers (Rx.NET)</item>
    /// <item>Notify <see cref="Changed"/> event handlers (.NET events)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Usage Example:</strong>
    /// <code>
    /// statusProvider.Mutate(b => {
    ///     b.StateTextMode = false;
    ///     b.StateHiRes = true;
    ///     b.StatePage2 = true;
    /// });
    /// </code>
    /// This atomically switches to hi-res graphics on page 2.
    /// </para>
    /// </remarks>
    public void Mutate(Action<SystemStatusSnapshotBuilder> mutator)
    {
        // Capture old snapshot
        var oldSnapshot = _current;

        // Create builder from current snapshot
        var b = new SystemStatusSnapshotBuilder(_current);
        
        // Apply mutations
        mutator(b);
        
        // Build new immutable snapshot
        _current = b.Build();
        
        // Notify observers (Rx.NET stream first, then .NET events)
        _subject.OnNext(_current);
        Changed?.Invoke(this, _current);

        // Check if any of the 12 memory-affecting switches or the IntC8RomSlot changed
        if (oldSnapshot.StateRamRd != _current.StateRamRd ||
            oldSnapshot.StateRamWrt != _current.StateRamWrt ||
            oldSnapshot.StateAltZp != _current.StateAltZp ||
            oldSnapshot.State80Store != _current.State80Store ||
            oldSnapshot.StateHiRes != _current.StateHiRes ||
            oldSnapshot.StatePage2 != _current.StatePage2 ||
            oldSnapshot.StateIntCxRom != _current.StateIntCxRom ||
            oldSnapshot.StateSlotC3Rom != _current.StateSlotC3Rom ||
            oldSnapshot.StateHighWrite != _current.StateHighWrite ||
            oldSnapshot.StateUseBank1 != _current.StateUseBank1 ||
            oldSnapshot.StateHighRead != _current.StateHighRead ||
            oldSnapshot.StateIntC8Rom != _current.StateIntC8Rom ||
            oldSnapshot.StateIntC8RomSlot != _current.StateIntC8RomSlot)
        {
            MemoryMappingChanged?.Invoke(this, _current);
        }
    }

    #region ISystemStatusMutator - Mutation interface

    // Memory configuration switches
    /// <inheritdoc />
    public void Set80Store(bool store80) => Mutate(b => b.State80Store = store80);
    
    /// <inheritdoc />
    public void SetRamRd(bool ramRd) => Mutate(b => b.StateRamRd = ramRd);
    
    /// <inheritdoc />
    public void SetRamWrt(bool ramWrt) => Mutate(b => b.StateRamWrt = ramWrt);
    
    /// <inheritdoc />
    public void SetIntCxRom(bool intCxRom) => Mutate(b => b.StateIntCxRom = intCxRom);
    
    /// <inheritdoc />
    public void SetAltZp(bool altZp) => Mutate(b => b.StateAltZp = altZp);
    
    /// <inheritdoc />
    public void SetSlotC3Rom(bool slotC3Rom) => Mutate(b => b.StateSlotC3Rom = slotC3Rom);

    // Video mode switches
    /// <inheritdoc />
    public void Set80Vid(bool vid) => Mutate(b => b.StateShow80Col = vid);
    
    /// <inheritdoc />
    public void SetAltChar(bool altChar) => Mutate(b => b.StateAltCharSet = altChar);
    
    /// <inheritdoc />
    public void SetText(bool text) => Mutate(b => b.StateTextMode = text);
    
    /// <inheritdoc />
    public void SetMixed(bool mixed) => Mutate(b => b.StateMixed = mixed);
    
    /// <inheritdoc />
    public void SetPage2(bool page2) => Mutate(b => b.StatePage2 = page2);
    
    /// <inheritdoc />
    public void SetHiRes(bool hires) => Mutate(b => b.StateHiRes = hires);

    // Annunciators and I/O
    /// <inheritdoc />
    public void SetAn0(bool an0) => Mutate(b => b.StateAnn0 = an0);
    
    /// <inheritdoc />
    public void SetAn1(bool an1) => Mutate(b => b.StateAnn1 = an1);
    
    /// <inheritdoc />
    public void SetAn2(bool an2) => Mutate(b => b.StateAnn2 = an2);
    
    /// <inheritdoc />
    public void SetAn3(bool an3) => Mutate(b => b.StateAnn3 = an3);

    // Language card switches
    /// <inheritdoc />
    public void SetBank1(bool enabled) => Mutate(b => b.StateUseBank1 = enabled);
    
    /// <inheritdoc />
    public void SetHighWrite(bool enabled) => Mutate(b => b.StateHighWrite = enabled);
    
    /// <inheritdoc />
    public void SetHighRead(bool enabled) => Mutate(b => b.StateHighRead = enabled);
    
    /// <inheritdoc />
    public void SetPreWrite(bool enabled) => Mutate(b => b.StatePrewrite = enabled);

    // Pushbuttons (game controller) - Private to sync from GameController subsystem.  Should only be changed internally.
    /// <inheritdoc />
    private void SetButton0(bool pressed) => Mutate(b => b.StatePb0 = pressed);
    
    /// <inheritdoc />
    private void SetButton1(bool pressed) => Mutate(b => b.StatePb1 = pressed);
    
    /// <inheritdoc />
    private void SetButton2(bool pressed) => Mutate(b => b.StatePb2 = pressed);

    // Keyboard and paddles
   
    /// <inheritdoc />
    private void SetPdl0(byte value) => Mutate(b => b.StatePdl0 = value);

    /// <inheritdoc />
    private void SetPdl1(byte value) => Mutate(b => b.StatePdl1 = value);

    /// <inheritdoc />
    private void SetPdl2(byte value) => Mutate(b => b.StatePdl2 = value);

    /// <inheritdoc />
    private void SetPdl3(byte value) => Mutate(b => b.StatePdl3 = value);

    // Vertical blanking interval
    /// <inheritdoc />
    public void SetVBlank(bool active) => Mutate(b => b.StateVBlank = active);

    // Flash state (timing-based, toggled by VA2M)
    /// <inheritdoc />
    public void SetFlashOn(bool flashOn) => Mutate(b => b.StateFlashOn = flashOn);

    /// <inheritdoc />
    public void SetIntC8Rom(bool intC8Rom) => Mutate(b => b.StateIntC8Rom = intC8Rom);

    /// <inheritdoc />
    public void SetIntC8RomSlot(byte slot) => Mutate(b => b.StateIntC8RomSlot = slot);

    /// <inheritdoc />
    public void SetCurrentMhz(double mhz) => Mutate(b => b.StateCurrentMhz = mhz);
    #endregion
}

/// <summary>
/// Mutable builder for creating <see cref="SystemStatusSnapshot"/> instances.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Builder Pattern:</strong> Provides a mutable workspace for constructing new snapshots.
/// All fields can be freely modified before calling <see cref="Build"/> to produce an
/// immutable <see cref="SystemStatusSnapshot"/>. This is more efficient than creating
/// snapshots via the record's <c>with</c> syntax when modifying multiple fields.
/// </para>
/// <para>
/// <strong>Usage:</strong> Typically used internally by <see cref="SystemStatusProvider.Mutate"/>
/// to batch multiple soft switch updates into a single atomic state change.
/// </para>
/// </remarks>
/// <param name="s">Initial snapshot to copy field values from.</param>
public sealed class SystemStatusSnapshotBuilder(SystemStatusSnapshot s)
{
    /// <summary>80STORE switch state.</summary>
    public bool State80Store = s.State80Store;
    
    /// <summary>RAMRD switch state.</summary>
    public bool StateRamRd = s.StateRamRd;
    
    /// <summary>RAMWRT switch state.</summary>
    public bool StateRamWrt = s.StateRamWrt;

    /// <summary>INTCXROM switch state.</summary>
    public bool StateIntCxRom = s.StateIntCxRom;

    /// <summary>INTC8ROM switch state.</summary>
    public bool StateIntC8Rom = s.StateIntC8Rom;

    /// <summary>ALTZP switch state.</summary>
    public bool StateAltZp = s.StateAltZp;
    
    /// <summary>SLOTC3ROM switch state.</summary>
    public bool StateSlotC3Rom = s.StateSlotC3Rom;
    
    /// <summary>Paddle button 0 state.</summary>
    public bool StatePb0 = s.StatePb0;
    
    /// <summary>Paddle button 1 state.</summary>
    public bool StatePb1 = s.StatePb1;
    
    /// <summary>Paddle button 2 state.</summary>
    public bool StatePb2 = s.StatePb2;
    
    /// <summary>Annunciator 0 state.</summary>
    public bool StateAnn0 = s.StateAnn0;
    
    /// <summary>Annunciator 1 state.</summary>
    public bool StateAnn1 = s.StateAnn1;
    
    /// <summary>Annunciator 2 state.</summary>
    public bool StateAnn2 = s.StateAnn2;
    
    /// <summary>Annunciator 3 / Double Graphics state.</summary>
    public bool StateAnn3 = s.StateAnn3_DGR;
    
    /// <summary>PAGE2 switch state.</summary>
    public bool StatePage2 = s.StatePage2;
    
    /// <summary>HIRES switch state.</summary>
    public bool StateHiRes = s.StateHiRes;
    
    /// <summary>MIXED switch state.</summary>
    public bool StateMixed = s.StateMixed;
    
    /// <summary>TEXT switch state.</summary>
    public bool StateTextMode = s.StateTextMode;
    
    /// <summary>80COL switch state.</summary>
    public bool StateShow80Col = s.StateShow80Col;
    
    /// <summary>ALTCHARSET switch state.</summary>
    public bool StateAltCharSet = s.StateAltCharSet;
    
    /// <summary>Flash state for inverse/flashing characters.</summary>
    public bool StateFlashOn = s.StateFlashOn;
    
    /// <summary>Language card PREWRITE flag.</summary>
    public bool StatePrewrite = s.StatePrewrite;
    
    /// <summary>Language card bank selection.</summary>
    public bool StateUseBank1 = s.StateUseBank1;
    
    /// <summary>Language card read enable.</summary>
    public bool StateHighRead = s.StateHighRead;
    
    /// <summary>Language card write enable.</summary>
    public bool StateHighWrite = s.StateHighWrite;

    /// <summary>Vertical blanking interval state.</summary>
    public bool StateVBlank = s.StateVBlank;
    public byte StatePdl0 = s.StatePdl0;
    public byte StatePdl1 = s.StatePdl1;
    public byte StatePdl2 = s.StatePdl2;
    public byte StatePdl3 = s.StatePdl3;
    public byte StateIntC8RomSlot = s.StateIntC8RomSlot;
    public double StateCurrentMhz = s.StateCurrentMhz;



    /// <summary>
    /// Builds an immutable <see cref="SystemStatusSnapshot"/> from the current field values.
    /// </summary>
    /// <returns>A new immutable snapshot containing all current field values.</returns>
    /// <remarks>
    /// This method can be called multiple times to create multiple snapshots from the
    /// same builder state. The builder remains mutable and can be modified after calling Build().
    /// </remarks>
    public SystemStatusSnapshot Build() => new(
        State80Store, StateRamRd, StateRamWrt, StateIntCxRom, StateIntC8Rom, StateAltZp, StateSlotC3Rom,
        StatePb0, StatePb1, StatePb2, StateAnn0, StateAnn1, StateAnn2, StateAnn3,
        StatePage2, StateHiRes, StateMixed, StateTextMode, StateShow80Col, StateAltCharSet,
        StateFlashOn, StatePrewrite, StateUseBank1, StateHighRead, StateHighWrite, StateVBlank,
        StatePdl0, StatePdl1, StatePdl2, StatePdl3, StateIntC8RomSlot, StateCurrentMhz);
}

