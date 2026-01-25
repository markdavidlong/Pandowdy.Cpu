using ReactiveUI;
using Pandowdy.EmuCore.Interfaces;
using System;
using Pandowdy.EmuCore.Services;

namespace Pandowdy.UI.ViewModels;

/// <summary>
/// View model for displaying Apple IIe system status including soft switches and pushbutton states.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Purpose:</strong> Provides reactive properties for all Apple IIe soft switches,
/// pushbuttons, and system state flags. These properties are bound to UI elements that display
/// the current hardware configuration and status.
/// </para>
/// <para>
/// <strong>Update Pattern:</strong> Unlike EmulatorStateViewModel, this view model uses immediate
/// subscription (no WhenActivated) because system status updates are relatively infrequent and
/// always needed for accurate display. Updates are pushed from ISystemStatusProvider.Stream.
/// </para>
/// <para>
/// <strong>Initialization:</strong> The constructor immediately subscribes to the status stream
/// and applies the current snapshot to initialize all properties with correct initial values.
/// </para>
/// <para>
/// <strong>Property Count:</strong> Contains 24 boolean properties representing:
/// <list type="bullet">
/// <item>6 memory mapping switches (80STORE, RAMRD, RAMWRT, INTCXROM, ALTZP, SLOTC3ROM)</item>
/// <item>3 pushbuttons (PB0, PB1, PB2)</item>
/// <item>4 annunciators (AN0, AN1, AN2, AN3/DGR)</item>
/// <item>6 video mode switches (PAGE2, HIRES, MIXED, TEXT, 80COL, ALTCHAR)</item>
/// <item>1 cursor flash state (FLASHON)</item>
/// <item>4 language card banking switches (BANK1, PREWRITE, HIGHREAD, HIGHWRITE)</item>
/// </list>
/// </para>
/// </remarks>
public sealed class SystemStatusViewModel : ReactiveObject
{
    #region Private Fields

    /// <summary>
    /// System status provider supplying the reactive stream of status snapshots.
    /// </summary>
    private readonly ISystemStatusProvider _status;

    /// <summary>
    /// Backing fields for all system status properties.
    /// </summary>
    private bool _state80Store, _stateRamRd, _stateRamWrt, _stateIntCxRom, _stateIntC8Rom,_stateAltZp, _stateSlotC3Rom,
                 _statePb0, _statePb1, _statePb2, _stateAnn0, _stateAnn1, _stateAnn2, _stateAnn3,
                 _statePage2, _stateHiRes, _stateMixed, _stateTextMode, _stateShow80Col, _stateAltCharSet, 
                 _stateFlashOn, _stateBank1, _statePrewrite, _stateHighRead, _stateHighWrite, _stateVBlank;
    private byte _stateIntC8RomSlot;
    private double _stateCurrentMhz;

    #endregion

    #region Memory Mapping Switch Properties

    /// <summary>
    /// Gets the state of the 80STORE soft switch.
    /// </summary>
    /// <value>True when 80STORE is active, false otherwise.</value>
    /// <remarks>
    /// When active, the PAGE2 switch controls auxiliary memory access rather than display page selection.
    /// Affects memory mapping for $0400-$07FF and $2000-$3FFF ranges.
    /// </remarks>
    public bool State80Store
    {
        get => _state80Store;
        private set => this.RaiseAndSetIfChanged(ref _state80Store, value);
    }
    
    /// <summary>
    /// Gets the state of the RAMRD soft switch.
    /// </summary>
    /// <value>True when reading from auxiliary RAM, false when reading from main RAM.</value>
    /// <remarks>
    /// Controls whether reads from certain memory ranges ($0200-$BFFF) come from main or auxiliary RAM.
    /// Typically used with 80-column card or extended memory applications.
    /// </remarks>
    public bool StateRamRd
    {
        get => _stateRamRd;
        private set => this.RaiseAndSetIfChanged(ref _stateRamRd, value);
    }
    
    /// <summary>
    /// Gets the state of the RAMWRT soft switch.
    /// </summary>
    /// <value>True when writing to auxiliary RAM, false when writing to main RAM.</value>
    /// <remarks>
    /// Controls whether writes to certain memory ranges ($0200-$BFFF) go to main or auxiliary RAM.
    /// Can be different from RAMRD, allowing read-from-one/write-to-other configurations.
    /// </remarks>
    public bool StateRamWrt
    {
        get => _stateRamWrt;
        private set => this.RaiseAndSetIfChanged(ref _stateRamWrt, value);
    }
    
    /// <summary>
    /// Gets the state of the INTCXROM soft switch.
    /// </summary>
    /// <value>True when using internal ROM, false when using peripheral card ROMs.</value>
    /// <remarks>
    /// Controls whether accesses to $C100-$CFFF use internal ROM or peripheral card ROMs.
    /// When true (default at power-on), internal ROM is active; when false, slot ROMs are accessible.
    /// </remarks>
    public bool StateIntCxRom
    {
        get => _stateIntCxRom;
        private set => this.RaiseAndSetIfChanged(ref _stateIntCxRom, value);
    }

    public bool StateIntC8Rom
    {
        get => _stateIntC8Rom;
        private set => this.RaiseAndSetIfChanged(ref _stateIntC8Rom, value);
    }

    public byte StateIntC8RomSlot
    {
        get => _stateIntC8RomSlot;
        private set => this.RaiseAndSetIfChanged(ref _stateIntC8RomSlot, value);
    }



    /// <summary>
    /// Gets the state of the ALTZP soft switch.
    /// </summary>
    /// <value>True when using auxiliary zero page/stack, false when using main memory.</value>
    /// <remarks>
    /// When active, zero page ($0000-$00FF) and stack ($0100-$01FF) are mapped to auxiliary RAM.
    /// Allows programs to maintain separate zero page/stack contexts.
    /// </remarks>
    public bool StateAltZp
    {
        get => _stateAltZp;
        private set => this.RaiseAndSetIfChanged(ref _stateAltZp, value);
    }
    
    /// <summary>
    /// Gets the state of the SLOTC3ROM soft switch.
    /// </summary>
    /// <value>True when using slot 3 ROM, false when using internal ROM.</value>
    /// <remarks>
    /// Controls whether accesses to $C300-$C3FF use internal ROM or slot 3 peripheral ROM.
    /// Independent of INTCXROM, allowing slot 3 to be controlled separately.
    /// </remarks>
    public bool StateSlotC3Rom
    {
        get => _stateSlotC3Rom;
        private set => this.RaiseAndSetIfChanged(ref _stateSlotC3Rom, value);
    }

    #endregion

    #region Pushbutton Properties

    /// <summary>
    /// Gets the state of pushbutton 0 (game controller button 0).
    /// </summary>
    /// <value>True when button 0 is pressed, false when released.</value>
    /// <remarks>
    /// Readable at $C061. Typically used for joystick or paddle button 0.
    /// </remarks>
    public bool StatePb0
    {
        get => _statePb0;
        private set => this.RaiseAndSetIfChanged(ref _statePb0, value);
    }
    
    /// <summary>
    /// Gets the state of pushbutton 1 (game controller button 1).
    /// </summary>
    /// <value>True when button 1 is pressed, false when released.</value>
    /// <remarks>
    /// Readable at $C062. Typically used for joystick or paddle button 1.
    /// </remarks>
    public bool StatePb1
    {
        get => _statePb1;
        private set => this.RaiseAndSetIfChanged(ref _statePb1, value);
    }
    
    /// <summary>
    /// Gets the state of pushbutton 2 (game controller button 2).
    /// </summary>
    /// <value>True when button 2 is pressed, false when released.</value>
    /// <remarks>
    /// Readable at $C063. Typically used for additional game controller buttons.
    /// </remarks>
    public bool StatePb2
    {
        get => _statePb2;
        private set => this.RaiseAndSetIfChanged(ref _statePb2, value);
    }

    #endregion

    #region Annunciator Properties

    /// <summary>
    /// Gets the state of annunciator 0.
    /// </summary>
    /// <value>True when annunciator 0 is on, false when off.</value>
    /// <remarks>
    /// General-purpose output bit controlled by $C058/$C059. Often used for peripheral control
    /// or as a flag for custom hardware.
    /// </remarks>
    public bool StateAnn0
    {
        get => _stateAnn0;
        private set => this.RaiseAndSetIfChanged(ref _stateAnn0, value);
    }
    
    /// <summary>
    /// Gets the state of annunciator 1.
    /// </summary>
    /// <value>True when annunciator 1 is on, false when off.</value>
    /// <remarks>
    /// General-purpose output bit controlled by $C05A/$C05B. Often used for peripheral control
    /// or as a flag for custom hardware.
    /// </remarks>
    public bool StateAnn1
    {
        get => _stateAnn1;
        private set => this.RaiseAndSetIfChanged(ref _stateAnn1, value);
    }
    
    /// <summary>
    /// Gets the state of annunciator 2.
    /// </summary>
    /// <value>True when annunciator 2 is on, false when off.</value>
    /// <remarks>
    /// General-purpose output bit controlled by $C05C/$C05D. Often used for peripheral control
    /// or as a flag for custom hardware.
    /// </remarks>
    public bool StateAnn2
    {
        get => _stateAnn2;
        private set => this.RaiseAndSetIfChanged(ref _stateAnn2, value);
    }
    
    /// <summary>
    /// Gets the state of annunciator 3 (also known as DGR - Double Graphics).
    /// </summary>
    /// <value>True when annunciator 3 is on, false when off.</value>
    /// <remarks>
    /// <para>
    /// General-purpose output bit controlled by $C05E/$C05F. Commonly used to enable
    /// double hi-res graphics mode on the Apple IIe.
    /// </para>
    /// <para>
    /// <strong>Double Hi-Res Mode:</strong> When combined with other switches, enables 560x192
    /// 16-color graphics mode using both main and auxiliary memory.
    /// </para>
    /// </remarks>
    public bool StateAnn3
    {
        get => _stateAnn3;
        private set => this.RaiseAndSetIfChanged(ref _stateAnn3, value);
    }

    #endregion

    #region Video Mode Properties

    /// <summary>
    /// Gets the state of the PAGE2 soft switch.
    /// </summary>
    /// <value>True when displaying page 2, false when displaying page 1.</value>
    /// <remarks>
    /// <para>
    /// Controls which video page is displayed (page 1 at $0400 or page 2 at $0800).
    /// Behavior is affected by 80STORE switch.
    /// </para>
    /// <para>
    /// <strong>When 80STORE is off:</strong> PAGE2 selects display page.
    /// <strong>When 80STORE is on:</strong> PAGE2 affects auxiliary memory access instead.
    /// </para>
    /// </remarks>
    public bool StatePage2
    {
        get => _statePage2;
        private set => this.RaiseAndSetIfChanged(ref _statePage2, value);
    }
    
    /// <summary>
    /// Gets the state of the HIRES soft switch.
    /// </summary>
    /// <value>True for hi-res graphics mode, false for low-res graphics mode.</value>
    /// <remarks>
    /// Controls whether graphics mode uses hi-res (280x192 or 560x192) or low-res (40x48).
    /// Only affects graphics mode; has no effect when TEXT mode is active.
    /// </remarks>
    public bool StateHiRes
    {
        get => _stateHiRes;
        private set => this.RaiseAndSetIfChanged(ref _stateHiRes, value);
    }
    
    /// <summary>
    /// Gets the state of the MIXED soft switch.
    /// </summary>
    /// <value>True for mixed text/graphics mode, false for full-screen mode.</value>
    /// <remarks>
    /// When active, displays graphics with 4 lines of text at the bottom (lines 20-23).
    /// Commonly used for games to show status information while displaying graphics.
    /// </remarks>
    public bool StateMixed
    {
        get => _stateMixed;
        private set => this.RaiseAndSetIfChanged(ref _stateMixed, value);
    }
    
    /// <summary>
    /// Gets the state of the TEXT soft switch.
    /// </summary>
    /// <value>True for text mode, false for graphics mode.</value>
    /// <remarks>
    /// Master switch controlling text vs graphics display. When true, displays 40-column or
    /// 80-column text. When false, displays graphics (low-res or hi-res based on HIRES switch).
    /// </remarks>
    public bool StateTextMode
    {
        get => _stateTextMode;
        private set => this.RaiseAndSetIfChanged(ref _stateTextMode, value);
    }
    
    /// <summary>
    /// Gets the state of the 80COL (80VID) soft switch.
    /// </summary>
    /// <value>True for 80-column display, false for 40-column display.</value>
    /// <remarks>
    /// When active, enables 80-column text mode using both main and auxiliary video memory.
    /// Requires auxiliary memory (80-column card or equivalent).
    /// </remarks>
    public bool StateShow80Col
    {
        get => _stateShow80Col;
        private set => this.RaiseAndSetIfChanged(ref _stateShow80Col, value);
    }
    
    /// <summary>
    /// Gets the state of the ALTCHAR soft switch.
    /// </summary>
    /// <value>True when using alternate character set, false for primary character set.</value>
    /// <remarks>
    /// Selects between primary and alternate character sets in text mode. The alternate character
    /// set includes MouseText characters and inverse lowercase letters.
    /// </remarks>
    public bool StateAltCharSet
    {
        get => _stateAltCharSet;
        private set => this.RaiseAndSetIfChanged(ref _stateAltCharSet, value);
    }
    
    /// <summary>
    /// Gets the cursor/mode indicator flash state.
    /// </summary>
    /// <value>True when flash is on (visible), false when flash is off (hidden).</value>
    /// <remarks>
    /// <para>
    /// Toggles at approximately 2.1 Hz (~476ms period) to control cursor blinking and
    /// mode indicator flashing. Used for inverse/flashing characters in text mode.
    /// </para>
    /// <para>
    /// <strong>Usage:</strong> Characters with bit 6 set (flashing) alternate between
    /// normal and inverse video based on this state.
    /// </para>
    /// </remarks>
    public bool StateFlashOn
    {
        get => _stateFlashOn;
        private set => this.RaiseAndSetIfChanged(ref _stateFlashOn, value);
    }

    #endregion

    #region Language Card Banking Properties

    /// <summary>
    /// Gets the state of the BANK1 soft switch.
    /// </summary>
    /// <value>True when using language card bank 1, false when using bank 2.</value>
    /// <remarks>
    /// Controls which 4KB bank of RAM is mapped into $D000-$DFFF. Bank 2 is at lower addresses,
    /// bank 1 is at higher addresses in the physical RAM chip.
    /// </remarks>
    public bool StateBank1
    {
        get => _stateBank1;
        private set => this.RaiseAndSetIfChanged(ref _stateBank1, value);
    }
    
    /// <summary>
    /// Gets the HIGHREAD soft switch state.
    /// </summary>
    /// <value>True when reading from language card RAM, false when reading from ROM.</value>
    /// <remarks>
    /// Controls whether reads from $D000-$FFFF come from RAM or ROM. Independent of write enable.
    /// </remarks>
    public bool StateHighRead
    {
        get => _stateHighRead;
        private set => this.RaiseAndSetIfChanged(ref _stateHighRead, value);
    }
    
    /// <summary>
    /// Gets the HIGHWRITE soft switch state.
    /// </summary>
    /// <value>True when writing to language card RAM is enabled, false when write-protected.</value>
    /// <remarks>
    /// <para>
    /// Controls whether writes to $D000-$FFFF are allowed to modify RAM. Requires the two-access
    /// write enable sequence (PREWRITE) to activate.
    /// </para>
    /// <para>
    /// <strong>Write Enable Sequence:</strong> Two consecutive accesses to specific language card
    /// addresses are required to enable writing, preventing accidental ROM overwrites.
    /// </para>
    /// </remarks>
    public bool StateHighWrite
    {
        get => _stateHighWrite;
        private set => this.RaiseAndSetIfChanged(ref _stateHighWrite, value);
    }
    
    /// <summary>
    /// Gets the PREWRITE state (first step of write enable sequence).
    /// </summary>
    /// <value>True when first access has occurred, false otherwise.</value>
    /// <remarks>
    /// <para>
    /// First step in the two-access write enable sequence. When true, the next access to the
    /// appropriate language card address will enable writing (set HIGHWRITE).
    /// </para>
    /// <para>
    /// <strong>Implementation Note:</strong> This is an internal state flag, not directly
    /// controlled by a specific soft switch address. It's set by the first of two required
    /// accesses to enable language card RAM writing.
    /// </para>
    /// </remarks>
    public bool StatePrewrite
    {
        get => _statePrewrite;
        private set => this.RaiseAndSetIfChanged(ref _statePrewrite, value);
    }

    #endregion

    #region Vertical Blanking Interval

    /// <summary>
    /// Gets the vertical blanking interval state (readable at $C019, bit 7).
    /// </summary>
    /// <value>True during VBlank (70 scanlines), false during visible display (192 scanlines).</value>
    /// <remarks>
    /// <para>
    /// The vertical blanking interval occurs during scanlines 192-261 (70 scanlines Ã— 65 cycles = 4,550 cycles)
    /// of each frame. During VBlank, the CRT electron beam returns from bottom to top.
    /// </para>
    /// <para>
    /// <strong>Update Frequency:</strong> This property toggles approximately 120 times per second
    /// (twice per frame at 60 Hz). It will appear to flicker rapidly during normal emulation but
    /// is useful for debugging and single-step mode.
    /// </para>
    /// <para>
    /// <strong>Hardware Behavior:</strong> Reading $C019 returns bit 7 = 1 during VBlank, bit 7 = 0 otherwise.
    /// </para>
    /// </remarks>
    public bool StateVBlank
    {
        get => _stateVBlank;
        private set => this.RaiseAndSetIfChanged(ref _stateVBlank, value);
    }

    #endregion

    public double StateCurrentMhz
    {
        get => _stateCurrentMhz;
        private set => this.RaiseAndSetIfChanged(ref _stateCurrentMhz, value);
    }

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemStatusViewModel"/> class.
    /// </summary>
    /// <param name="status">System status provider supplying reactive status updates.</param>
    /// <remarks>
    /// <para>
    /// <strong>Immediate Subscription:</strong> Unlike EmulatorStateViewModel, this view model
    /// subscribes immediately to the status stream without using WhenActivated. This ensures
    /// system status is always up-to-date and available for display.
    /// </para>
    /// <para>
    /// <strong>Initialization:</strong> After subscribing to the stream, applies the current
    /// snapshot from <see cref="ISystemStatusProvider.Current"/> to initialize all properties
    /// with correct starting values. This prevents a brief period where properties would be
    /// default (false) until the first update arrives.
    /// </para>
    /// <para>
    /// <strong>Update Frequency:</strong> System status updates are relatively infrequent
    /// (typically triggered by soft switch changes or periodic polling), so immediate subscription
    /// has minimal performance impact compared to the frequently-updating EmulatorState.
    /// </para>
    /// </remarks>
    public SystemStatusViewModel(ISystemStatusProvider status)
    {
        _status = status;
        
        // Immediate subscription; no activation required since status updates are infrequent
        _status.Stream.Subscribe(OnStatusNext);
        
        // Initialize with current snapshot so UI shows initial values immediately
        OnStatusNext(_status.Current);
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Handles incoming system status snapshots by updating all reactive properties.
    /// </summary>
    /// <param name="s">System status snapshot containing current soft switch and button states.</param>
    /// <remarks>
    /// <para>
    /// This method is called whenever the status stream emits a new snapshot. It updates all
    /// 24 properties with values from the snapshot, triggering UI updates via ReactiveUI's
    /// property change notifications.
    /// </para>
    /// <para>
    /// <strong>Thread Context:</strong> Called on the thread that publishes status updates
    /// (typically the emulator thread). ReactiveUI handles marshaling property change notifications
    /// to the UI thread automatically.
    /// </para>
    /// <para>
    /// <strong>Property Mapping:</strong> Each property is mapped directly from the snapshot,
    /// with one special case: StateAnn3 is mapped from StateAnn3_DGR in the snapshot (DGR =
    /// Double Graphics mode indicator).
    /// </para>
    /// </remarks>
    private void OnStatusNext(SystemStatusSnapshot s)
    {
        // Memory mapping switches
        State80Store = s.State80Store;
        StateRamRd = s.StateRamRd;
        StateRamWrt = s.StateRamWrt;
        StateIntCxRom = s.StateIntCxRom;
        StateIntC8Rom = s.StateIntC8Rom;
        StateIntC8RomSlot = s.StateIntC8RomSlot;
        StateAltZp = s.StateAltZp;
        StateSlotC3Rom = s.StateSlotC3Rom;
        
        // Pushbuttons
        StatePb0 = s.StatePb0;
        StatePb1 = s.StatePb1;
        StatePb2 = s.StatePb2;
        
        // Annunciators
        StateAnn0 = s.StateAnn0;
        StateAnn1 = s.StateAnn1;
        StateAnn2 = s.StateAnn2;
        StateAnn3 = s.StateAnn3_DGR; // DGR = Double Graphics mode
        
        // Video mode switches
        StatePage2 = s.StatePage2;
        StateHiRes = s.StateHiRes;
        StateMixed = s.StateMixed;
        StateTextMode = s.StateTextMode;
        StateShow80Col = s.StateShow80Col;
        StateAltCharSet = s.StateAltCharSet;
        StateFlashOn = s.StateFlashOn;
        
        // Language card banking
        StatePrewrite = s.StatePrewrite;
        StateBank1 = s.StateUseBank1;
        StateHighRead = s.StateHighRead;
        StateHighWrite = s.StateHighWrite;
        
        // Vertical blanking interval
        StateVBlank = s.StateVBlank;

        // Current emulated CPU speed in MHz
        StateCurrentMhz = s.StateCurrentMhz;
    }

    #endregion
}
