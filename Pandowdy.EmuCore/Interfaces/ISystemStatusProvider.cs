using Pandowdy.EmuCore.Services;

namespace Pandowdy.EmuCore.Interfaces;

/// <summary>
/// Provides read-only access to the current state of Apple IIe system status and soft switches.
/// </summary>
/// <remarks>
/// This interface allows components (such as the UI, debugger, or video renderer) to query
/// the current state of all Apple IIe soft switches and system flags without directly
/// accessing the bus or memory. It provides a clean, observable interface for monitoring
/// system state changes.
/// <para>
/// The provider supports three patterns for accessing status:
/// <list type="bullet">
/// <item><strong>Direct property access</strong> - Query individual switch states synchronously</item>
/// <item><strong>Event-based</strong> - Subscribe to <see cref="Changed"/> event for notifications</item>
/// <item><strong>Reactive (Rx.NET)</strong> - Subscribe to <see cref="Stream"/> for reactive updates</item>
/// </list>
/// </para>
/// <para>
/// <strong>Read-Only Design:</strong> All state properties are read-only. Components that need
/// to modify system state should depend on <see cref="ISystemStatusMutator"/> instead, which
/// extends this interface with mutation methods. See <see cref="SoftSwitches"/> for an example
/// of a component that uses <see cref="ISystemStatusMutator"/> to both read and write state.
/// </para>
/// </remarks>
public interface ISystemStatusProvider
{
    #region Memory Configuration Switches
    
    /// <summary>
    /// Gets the state of the 80STORE soft switch ($C000/$C001).
    /// </summary>
    /// <value>
    /// True if 80STORE is enabled (page 2 redirected to auxiliary memory); false otherwise.
    /// </value>
    /// <remarks>
    /// When enabled, page 2 video memory is redirected to auxiliary RAM, enabling
    /// 80-column text mode and double hi-res graphics with independent video pages.
    /// </remarks>
    bool State80Store { get; }
    
    /// <summary>
    /// Gets the state of the RAMRD soft switch ($C002/$C003).
    /// </summary>
    /// <value>
    /// True if reading from auxiliary memory; false if reading from main memory.
    /// </value>
    /// <remarks>
    /// Controls which 64KB bank (main or auxiliary) is used for memory reads.
    /// Allows programs to access the Apple IIe's extended 128KB memory space.
    /// </remarks>
    bool StateRamRd { get; }
    
    /// <summary>
    /// Gets the state of the RAMWRT soft switch ($C004/$C005).
    /// </summary>
    /// <value>
    /// True if writing to auxiliary memory; false if writing to main memory.
    /// </value>
    /// <remarks>
    /// Controls which 64KB bank (main or auxiliary) is used for memory writes.
    /// Independent from RAMRD, allowing asymmetric read/write configurations.
    /// </remarks>
    bool StateRamWrt { get; }
    
    /// <summary>
    /// Gets the state of the INTCXROM soft switch ($C006/$C007).
    /// </summary>
    /// <value>
    /// True if using internal ROM; false if using slot ROMs.
    /// </value>
    /// <remarks>
    /// Controls whether the $C100-$CFFF range accesses internal ROM or peripheral
    /// card slot ROMs. Typically set by the monitor during initialization.
    /// </remarks>
    bool StateIntCxRom { get; }
    
    /// <summary>
    /// Gets the state of the ALTZP soft switch ($C008/$C009).
    /// </summary>
    /// <value>
    /// True if using auxiliary zero page/stack; false if using main.
    /// </value>
    /// <remarks>
    /// Controls whether zero page ($0000-$00FF) and stack ($0100-$01FF) access
    /// auxiliary memory. Allows programs to maintain separate CPU contexts.
    /// </remarks>
    bool StateAltZp { get; }
    
    /// <summary>
    /// Gets the state of the SLOTC3ROM soft switch ($C00A/$C00B).
    /// </summary>
    /// <value>
    /// True if using slot C3 ROM; false if using internal ROM.
    /// </value>
    /// <remarks>
    /// Controls whether the $C300-$C3FF range (slot 3) uses peripheral card ROM
    /// or internal ROM. Independent of INTCXROM. Commonly used for 80-column card firmware.
    /// </remarks>
    bool StateSlotC3Rom { get; }

    bool StateIntC8Rom { get; }

    byte StateIntC8RomSlot { get; }

    #endregion

    double StateCurrentMhz { get; }

    #region Game Controller and Keyboard

    /// <summary>
    /// Gets the state of pushbutton 0 ($C061).
    /// </summary>
    /// <value>
    /// True if button 0 is pressed; false if released.
    /// </value>
    /// <remarks>
    /// Represents the state of game controller button 0. The high bit of the
    /// byte read from $C061 indicates the button state.
    /// </remarks>
    bool StatePb0 { get; }
    
    /// <summary>
    /// Gets the state of pushbutton 1 ($C062).
    /// </summary>
    /// <value>
    /// True if button 1 is pressed; false if released.
    /// </value>
    /// <remarks>
    /// Represents the state of game controller button 1. The high bit of the
    /// byte read from $C062 indicates the button state.
    /// </remarks>
    bool StatePb1 { get; }
    
    /// <summary>
    /// Gets the state of pushbutton 2 ($C063).
    /// </summary>
    /// <value>
    /// True if button 2 is pressed; false if released.
    /// </value>
    /// <remarks>
    /// Represents the state of game controller button 2. The high bit of the
    /// byte read from $C063 indicates the button state.
    /// </remarks>
    bool StatePb2 { get; }
    
    
    /// <summary>
    /// Gets the state of annunciator 0 ($C058/$C059).
    /// </summary>
    /// <value>
    /// True if annunciator 0 is on; false if off.
    /// </value>
    /// <remarks>
    /// General-purpose output signal, rarely used in standard software.
    /// Some games use annunciators for audio effects or peripheral control.
    /// </remarks>
    bool StateAnn0 { get; }
    
    /// <summary>
    /// Gets the state of annunciator 1 ($C05A/$C05B).
    /// </summary>
    /// <value>
    /// True if annunciator 1 is on; false if off.
    /// </value>
    /// <remarks>
    /// General-purpose output signal, rarely used in standard software.
    /// </remarks>
    bool StateAnn1 { get; }
    
    /// <summary>
    /// Gets the state of annunciator 2 ($C05C/$C05D).
    /// </summary>
    /// <value>
    /// True if annunciator 2 is on; false if off.
    /// </value>
    /// <remarks>
    /// General-purpose output signal, rarely used in standard software.
    /// </remarks>
    bool StateAnn2 { get; }
    
    /// <summary>
    /// Gets the state of annunciator 3 / double hi-res enable ($C05E/$C05F).
    /// </summary>
    /// <value>
    /// True if annunciator 3 is on; false if off.
    /// </value>
    /// <remarks>
    /// Annunciator 3 doubles as the double hi-res (DHR) enable switch.
    /// <para>
    /// <strong>Important:</strong> The logic is inverted for DHGR - when this value is
    /// <em>false</em> (annunciator off), double hi-res mode is <em>enabled</em>.
    /// When this value is <em>true</em> (annunciator on), double hi-res is <em>disabled</em>.
    /// </para>
    /// </remarks>
    bool StateAnn3_DGR { get; }
    


    /// <summary>
    /// Gets the current value of paddle 0 (game controller analog input).
    /// </summary>
    /// <value>
    /// Paddle 0 position value (0-255), where 0 is fully left/up and 255 is fully right/down.
    /// </value>
    /// <remarks>
    /// <para>
    /// The Apple IIe game port provides four analog inputs read through a timer-based
    /// mechanism. Software triggers a read by accessing $C070, then measures how long
    /// it takes for the paddle value to time out by polling $C064 (bit 7).
    /// </para>
    /// <para>
    /// <strong>Timing:</strong> The paddle timer counts from 0 to approximately 2805 Î¼s
    /// (at 1.023 MHz). The value stored here represents the pre-calculated time-out
    /// period for emulator efficiency.
    /// </para>
    /// </remarks>
    byte Pdl0 { get; }

    /// <summary>
    /// Gets the current value of paddle 1 (game controller analog input).
    /// </summary>
    /// <value>
    /// Paddle 1 position value (0-255), where 0 is fully left/up and 255 is fully right/down.
    /// </value>
    /// <remarks>
    /// See <see cref="Pdl0"/> for detailed paddle mechanics and timing information.
    /// </remarks>
    byte Pdl1 { get; }

    /// <summary>
    /// Gets the current value of paddle 2 (game controller analog input).
    /// </summary>
    /// <value>
    /// Paddle 2 position value (0-255), where 0 is fully left/up and 255 is fully right/down.
    /// </value>
    /// <remarks>
    /// See <see cref="Pdl0"/> for detailed paddle mechanics and timing information.
    /// </remarks>
    byte Pdl2 { get; }

    /// <summary>
    /// Gets the current value of paddle 3 (game controller analog input).
    /// </summary>
    /// <value>
    /// Paddle 3 position value (0-255), where 0 is fully left/up and 255 is fully right/down.
    /// </value>
    /// <remarks>
    /// See <see cref="Pdl0"/> for detailed paddle mechanics and timing information.
    /// </remarks>
    byte Pdl3 { get; }
    
    #endregion
    
    #region Video Mode Switches
    
    /// <summary>
    /// Gets the state of the PAGE2 soft switch ($C054/$C055).
    /// </summary>
    /// <value>
    /// True if displaying page 2; false if displaying page 1.
    /// </value>
    /// <remarks>
    /// Selects which video page is displayed. Page 1 uses $0400-$07FF (text) or
    /// $2000-$3FFF (hi-res). Page 2 uses $0800-$0BFF (text) or $4000-$5FFF (hi-res).
    /// Can be used for page flipping animation.
    /// </remarks>
    bool StatePage2 { get; }
    
    /// <summary>
    /// Gets the state of the HIRES soft switch ($C056/$C057).
    /// </summary>
    /// <value>
    /// True if hi-res graphics mode; false if lo-res graphics mode.
    /// </value>
    /// <remarks>
    /// In graphics mode (TEXT off), controls whether to display hi-res graphics
    /// (280Ã—192 pixels, 6 colors) or lo-res graphics (40Ã—48 color blocks, 16 colors).
    /// Has no effect when TEXT mode is enabled.
    /// </remarks>
    bool StateHiRes { get; }
    
    /// <summary>
    /// Gets the state of the MIXED soft switch ($C052/$C053).
    /// </summary>
    /// <value>
    /// True if mixed mode enabled; false if full-screen mode.
    /// </value>
    /// <remarks>
    /// When true and in graphics mode, displays graphics on the top 20 rows (160 scanlines)
    /// and text on the bottom 4 rows (32 scanlines). Commonly used in games to show
    /// graphics with a text status line. Has no effect in text mode.
    /// </remarks>
    bool StateMixed { get; }
    
    /// <summary>
    /// Gets the state of the TEXT soft switch ($C050/$C051).
    /// </summary>
    /// <value>
    /// True if text mode; false if graphics mode.
    /// </value>
    /// <remarks>
    /// Controls the primary video mode. When true, displays 40-column or 80-column text.
    /// When false, displays lo-res or hi-res graphics, depending on the HIRES switch.
    /// </remarks>
    bool StateTextMode { get; }
    
    /// <summary>
    /// Gets the state of the 80VID soft switch ($C00C/$C00D).
    /// </summary>
    /// <value>
    /// True if 80-column mode enabled; false if 40-column mode.
    /// </value>
    /// <remarks>
    /// Enables 80-column text mode, which uses both main and auxiliary memory to display
    /// 80 characters per line (alternating bytes from main and aux). When false,
    /// standard 40-column text mode is used.
    /// </remarks>
    bool StateShow80Col { get; }
    
    /// <summary>
    /// Gets the state of the ALTCHAR soft switch ($C00E/$C00F).
    /// </summary>
    /// <value>
    /// True if using MouseText character set; false if using standard.
    /// </value>
    /// <remarks>
    /// Selects between the standard Apple II character set and the alternate MouseText
    /// character set. MouseText provides graphical symbols for building text-based UIs.
    /// </remarks>
    bool StateAltCharSet { get; }
    
    /// <summary>
    /// Gets the current flash state for flashing characters.
    /// </summary>
    /// <value>
    /// True if flash characters should be displayed in inverse; false for normal.
    /// </value>
    /// <remarks>
    /// The Apple IIe automatically alternates the display of flashing characters
    /// (codes $40-$7F) between normal and inverse video at approximately 2 Hz.
    /// This property reflects the current phase of that cycle.
    /// </remarks>
    bool StateFlashOn { get; }
    
    #endregion
    
    #region Language Card Switches
    
    /// <summary>
    /// Gets the language card pre-write state.
    /// </summary>
    /// <value>
    /// True if the pre-write sequence is active; false otherwise.
    /// </value>
    /// <remarks>
    /// The language card uses a two-access sequence to enable writing. The first access
    /// sets pre-write mode. A second consecutive access to the same address enables
    /// write mode. This is an internal state for the write protection mechanism.
    /// </remarks>
    bool StatePreWrite { get; }
    
    /// <summary>
    /// Gets the language card bank selection state.
    /// </summary>
    /// <value>
    /// True if bank 1 is selected; false if bank 2 is selected.
    /// </value>
    /// <remarks>
    /// The language card provides two 4KB RAM banks for the $D000-$DFFF address space.
    /// Bank 1 is physically located at $C000-$CFFF, while bank 2 is at $D000-$DFFF.
    /// </remarks>
    bool StateUseBank1 { get; }
    
    /// <summary>
    /// Gets the language card read enable state.
    /// </summary>
    /// <value>
    /// True if reading from language card RAM; false if reading from ROM.
    /// </value>
    /// <remarks>
    /// Controls whether reads from $D000-$FFFF access the language card RAM or the
    /// built-in ROM. Independent of write enable, allowing read-from-ROM,
    /// write-to-RAM configurations.
    /// </remarks>
    bool StateHighRead { get; }
    
    /// <summary>
    /// Gets the language card write enable state.
    /// </summary>
    /// <value>
    /// True if writing to language card RAM is enabled; false if write-protected.
    /// </value>
    /// <remarks>
    /// Controls whether writes to the $D000-$FFFF range modify the language card RAM.
    /// Write protection is engaged through a two-access sequence to prevent accidental
    /// modification of critical code.
    /// </remarks>
    bool StateHighWrite { get; }
    
    /// <summary>
    /// Gets the vertical blanking interval state (readable at $C019, bit 7).
    /// </summary>
    /// <value>
    /// True if in VBlank period (70 scanlines); false during visible display (192 scanlines).
    /// </value>
    /// <remarks>
    /// <para>
    /// The vertical blanking interval occurs during scanlines 192-261 of each frame.
    /// During VBlank, the CRT electron beam returns from bottom to top, and software
    /// can safely update graphics without causing visual artifacts.
    /// </para>
    /// <para>
    /// <strong>Timing:</strong> At 60 Hz frame rate, VBlank toggles approximately 120 times
    /// per second (twice per frame: ON at cycle 12,480, OFF at cycle 17,029).
    /// </para>
    /// <para>
    /// <strong>Hardware:</strong> Reading $C019 returns bit 7 = 1 during VBlank.
    /// </para>
    /// </remarks>
    bool StateVBlank { get; }
    
    #endregion

    /// <summary>
    /// Gets the current system status as an immutable snapshot.
    /// </summary>
    /// <value>
    /// A <see cref="SystemStatusSnapshot"/> containing all current soft switch states
    /// and system flags.
    /// </value>
    /// <remarks>
    /// This property provides a point-in-time snapshot of the entire system state,
    /// useful for comparing states, logging, or passing to components that need
    /// multiple status values atomically.
    /// </remarks>
    SystemStatusSnapshot Current { get; }
    
    /// <summary>
    /// Raised when any system status changes.
    /// </summary>
    /// <remarks>
    /// This event fires whenever any soft switch or system flag changes state.
    /// The event args contain a <see cref="SystemStatusSnapshot"/> with the new state.
    /// Subscribers can use this for event-driven updates to UI or other components.
    /// <para>
    /// For reactive programming patterns, consider using <see cref="Stream"/> instead.
    /// </para>
    /// </remarks>
    event EventHandler<SystemStatusSnapshot>? Changed;


    /// <summary>
    /// Event raised when any soft switch affecting memory mapping changes.
    /// </summary>
    /// <remarks>
    /// Fires only when RAMRD, RAMWRT, ALTZP, 80STORE, HIRES, PAGE2, INTCXROM,
    /// SLOTC3ROM, HIGHWRITE, BANK1, or HIGHREAD change. Provides the full
    /// <see cref="SystemStatusSnapshot"/> for convenience.
    /// </remarks>
    public event EventHandler<SystemStatusSnapshot>? MemoryMappingChanged;


    /// <summary>
    /// Gets an observable stream of system status snapshots.
    /// </summary>
    /// <value>
    /// An <see cref="IObservable{SystemStatusSnapshot}"/> that emits snapshots
    /// whenever system status changes.
    /// </value>
    /// <remarks>
    /// This observable stream provides the same notifications as the <see cref="Changed"/>
    /// event, but in a reactive (Rx.NET) format. Useful for composing reactive pipelines,
    /// throttling updates, or combining with other observables.
    /// <para>
    /// Example usage:
    /// <code>
    /// statusProvider.Stream
    ///     .Throttle(TimeSpan.FromMilliseconds(16))
    ///     .ObserveOn(RxApp.MainThreadScheduler)
    ///     .Subscribe(snapshot => UpdateUI(snapshot));
    /// </code>
    /// </para>
    /// </remarks>
    IObservable<SystemStatusSnapshot> Stream { get; }
}
