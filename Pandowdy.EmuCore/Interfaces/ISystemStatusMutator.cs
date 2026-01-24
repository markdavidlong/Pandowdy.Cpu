using Pandowdy.EmuCore.Services;

namespace Pandowdy.EmuCore.Interfaces;

/// <summary>
/// Provides write access to system status and soft switch states, extending the read-only <see cref="ISystemStatusProvider"/>.
/// </summary>
/// <remarks>
/// <para>
/// This interface extends <see cref="ISystemStatusProvider"/> to add mutation methods for updating
/// Apple IIe soft switch states and system flags. Components that need to modify system state
/// (such as <see cref="SoftSwitches"/> or the bus) should depend on this interface.
/// </para>
/// <para>
/// <strong>Inheritance Design:</strong> By inheriting from <see cref="ISystemStatusProvider"/>,
/// this interface provides both read and write access. This is intentional - components that
/// can mutate state typically also need to read it for synchronization and verification.
/// </para>
/// <para>
/// <strong>Usage Pattern:</strong> The <see cref="SoftSwitches"/> class accepts an
/// <see cref="ISystemStatusMutator"/> via dependency injection, giving it both read access
/// (to initialize from current state) and write access (to update state when switches toggle).
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> Mutation methods are not thread-safe. Callers must ensure
/// serialized access from a single thread (typically the emulator CPU thread).
/// </para>
/// </remarks>
public interface ISystemStatusMutator : ISystemStatusProvider
{
    #region Memory Configuration Switch Mutations

    /// <summary>
    /// Sets the state of the 80STORE soft switch ($C000/$C001).
    /// </summary>
    /// <param name="store80">True to enable 80STORE (page 2 redirected to auxiliary memory); false to disable.</param>
    /// <remarks>
    /// When enabled, page 2 video memory is redirected to auxiliary RAM, enabling
    /// 80-column text mode and double hi-res graphics with independent video pages.
    /// </remarks>
    void Set80Store(bool store80);

    /// <summary>
    /// Sets the state of the RAMRD soft switch ($C002/$C003).
    /// </summary>
    /// <param name="ramRd">True to read from auxiliary memory; false to read from main memory.</param>
    /// <remarks>
    /// Controls which 64KB bank (main or auxiliary) is used for memory reads.
    /// Allows programs to access the Apple IIe's extended 128KB memory space.
    /// </remarks>
    void SetRamRd(bool ramRd);

    /// <summary>
    /// Sets the state of the RAMWRT soft switch ($C004/$C005).
    /// </summary>
    /// <param name="ramWrt">True to write to auxiliary memory; false to write to main memory.</param>
    /// <remarks>
    /// Controls which 64KB bank (main or auxiliary) is used for memory writes.
    /// Independent from RAMRD, allowing asymmetric read/write configurations.
    /// </remarks>
    void SetRamWrt(bool ramWrt);

    /// <summary>
    /// Sets the state of the INTCXROM soft switch ($C006/$C007).
    /// </summary>
    /// <param name="intCxRom">True to use internal ROM; false to use slot ROMs.</param>
    /// <remarks>
    /// Controls whether the $C100-$CFFF range accesses internal ROM or peripheral
    /// card slot ROMs. Typically set by the monitor during initialization.
    /// </remarks>
    void SetIntCxRom(bool intCxRom);

    /// <summary>
    /// Sets the state of the ALTZP soft switch ($C008/$C009).
    /// </summary>
    /// <param name="altZp">True to use auxiliary zero page/stack; false to use main.</param>
    /// <remarks>
    /// Controls whether zero page ($0000-$00FF) and stack ($0100-$01FF) access
    /// auxiliary memory. Allows programs to maintain separate CPU contexts.
    /// </remarks>
    void SetAltZp(bool altZp);

    /// <summary>
    /// Sets the state of the SLOTC3ROM soft switch ($C00A/$C00B).
    /// </summary>
    /// <param name="slotC3Rom">True to use slot C3 ROM; false to use internal ROM.</param>
    /// <remarks>
    /// Controls whether the $C300-$C3FF range (slot 3) uses peripheral card ROM
    /// or internal ROM. Independent of INTCXROM. Commonly used for 80-column card firmware.
    /// </remarks>
    void SetSlotC3Rom(bool slotC3Rom);

    /// <summary>
    /// Sets the state of the internal C800-CFFF ROM switch.
    /// </summary>
    /// <param name="intC8Rom">True to use internal C800 ROM; false to use slot-based extended ROM.</param>
    /// <remarks>
    /// <para>
    /// Controls whether the $C800-$CFFF extended ROM space uses the internal (motherboard) ROM
    /// or the currently banked-in slot's extended ROM. This switch is typically set automatically
    /// when accessing slot ROM space.
    /// </para>
    /// <para>
    /// <strong>Bank Selection:</strong> When false, the slot specified by <see cref="ISystemStatusProvider.StateIntC8RomSlot"/>
    /// provides the extended ROM data. Accessing $CFFF resets this to internal ROM.
    /// </para>
    /// </remarks>
    void SetIntC8Rom(bool intC8Rom);

    /// <summary>
    /// Sets which slot's extended ROM is banked into $C800-$CFFF.
    /// </summary>
    /// <param name="slotNumber">Slot number (1-7) whose extended ROM should be active.</param>
    /// <remarks>
    /// <para>
    /// When <see cref="ISystemStatusProvider.StateIntC8Rom"/> is false, this slot's extended ROM is mapped into
    /// the $C800-$CFFF address range. Each slot can have up to 2KB of extended firmware ROM.
    /// </para>
    /// <para>
    /// <strong>Automatic Selection:</strong> This value is typically set automatically when
    /// the CPU accesses a slot's $Cx00-$CxFF ROM space - that slot becomes the active C800 bank.
    /// </para>
    /// </remarks>
    void SetIntC8RomSlot(byte slotNumber);

    
    #endregion

    #region Video Mode Switch Mutations

    /// <summary>
    /// Sets the state of the 80VID soft switch ($C00C/$C00D).
    /// </summary>
    /// <param name="vid">True to enable 80-column mode; false for 40-column mode.</param>
    /// <remarks>
    /// Enables 80-column text mode, which uses both main and auxiliary memory to display
    /// 80 characters per line (alternating bytes from main and aux). When false,
    /// standard 40-column text mode is used.
    /// </remarks>
    void Set80Vid(bool vid);

    /// <summary>
    /// Sets the state of the ALTCHAR soft switch ($C00E/$C00F).
    /// </summary>
    /// <param name="altChar">True to use MouseText character set; false to use standard.</param>
    /// <remarks>
    /// Selects between the standard Apple II character set and the alternate MouseText
    /// character set. MouseText provides graphical symbols for building text-based UIs.
    /// </remarks>
    void SetAltChar(bool altChar);

    /// <summary>
    /// Sets the state of the TEXT soft switch ($C050/$C051).
    /// </summary>
    /// <param name="text">True for text mode; false for graphics mode.</param>
    /// <remarks>
    /// Controls the primary video mode. When true, displays 40-column or 80-column text.
    /// When false, displays lo-res or hi-res graphics, depending on the HIRES switch.
    /// </remarks>
    void SetText(bool text);

    /// <summary>
    /// Sets the state of the MIXED soft switch ($C052/$C053).
    /// </summary>
    /// <param name="mixed">True to enable mixed mode; false for full-screen mode.</param>
    /// <remarks>
    /// When true and in graphics mode, displays graphics on the top 20 rows (160 scanlines)
    /// and text on the bottom 4 rows (32 scanlines). Commonly used in games to show
    /// graphics with a text status line. Has no effect in text mode.
    /// </remarks>
    void SetMixed(bool mixed);

    /// <summary>
    /// Sets the state of the PAGE2 soft switch ($C054/$C055).
    /// </summary>
    /// <param name="page2">True to display page 2; false to display page 1.</param>
    /// <remarks>
    /// Selects which video page is displayed. Page 1 uses $0400-$07FF (text) or
    /// $2000-$3FFF (hi-res). Page 2 uses $0800-$0BFF (text) or $4000-$5FFF (hi-res).
    /// Can be used for page flipping animation.
    /// </remarks>
    void SetPage2(bool page2);

    /// <summary>
    /// Sets the state of the HIRES soft switch ($C056/$C057).
    /// </summary>
    /// <param name="hires">True for hi-res graphics mode; false for lo-res graphics mode.</param>
    /// <remarks>
    /// In graphics mode (TEXT off), controls whether to display hi-res graphics
    /// (280×192 pixels, 6 colors) or lo-res graphics (40×48 color blocks, 16 colors).
    /// Has no effect when TEXT mode is enabled.
    /// </remarks>
    void SetHiRes(bool hires);

    #endregion

    #region Annunciator Mutations

    /// <summary>
    /// Sets the state of annunciator 0 ($C058/$C059).
    /// </summary>
    /// <param name="an0">True to turn on annunciator 0; false to turn off.</param>
    /// <remarks>
    /// General-purpose output signal, rarely used in standard software.
    /// Some games use annunciators for audio effects or peripheral control.
    /// </remarks>
    void SetAn0(bool an0);

    /// <summary>
    /// Sets the state of annunciator 1 ($C05A/$C05B).
    /// </summary>
    /// <param name="an1">True to turn on annunciator 1; false to turn off.</param>
    /// <remarks>
    /// General-purpose output signal, rarely used in standard software.
    /// </remarks>
    void SetAn1(bool an1);

    /// <summary>
    /// Sets the state of annunciator 2 ($C05C/$C05D).
    /// </summary>
    /// <param name="an2">True to turn on annunciator 2; false to turn off.</param>
    /// <remarks>
    /// General-purpose output signal, rarely used in standard software.
    /// </remarks>
    void SetAn2(bool an2);

    /// <summary>
    /// Sets the state of annunciator 3 / double hi-res enable ($C05E/$C05F).
    /// </summary>
    /// <param name="an3">True to turn on annunciator 3; false to turn off.</param>
    /// <remarks>
    /// Annunciator 3 doubles as the double hi-res (DHR) enable switch.
    /// <para>
    /// <strong>Important:</strong> The logic is inverted for DHGR - when this value is
    /// <em>false</em> (annunciator off), double hi-res mode is <em>enabled</em>.
    /// When this value is <em>true</em> (annunciator on), double hi-res is <em>disabled</em>.
    /// </para>
    /// </remarks>
    void SetAn3(bool an3);

    #endregion

    #region Language Card Switch Mutations

    /// <summary>
    /// Sets the language card bank selection state.
    /// </summary>
    /// <param name="enabled">True to select bank 1; false to select bank 2.</param>
    /// <remarks>
    /// The language card provides two 4KB RAM banks for the $D000-$DFFF address space.
    /// Bank 1 is physically located at $C000-$CFFF, while bank 2 is at $D000-$DFFF.
    /// </remarks>
    void SetBank1(bool enabled);

    /// <summary>
    /// Sets the language card write enable state.
    /// </summary>
    /// <param name="enabled">True to enable writing to language card RAM; false to write-protect.</param>
    /// <remarks>
    /// Controls whether writes to the $D000-$FFFF range modify the language card RAM.
    /// Write protection is engaged through a two-access sequence to prevent accidental
    /// modification of critical code.
    /// </remarks>
    void SetHighWrite(bool enabled);

    /// <summary>
    /// Sets the language card read enable state.
    /// </summary>
    /// <param name="enabled">True to read from language card RAM; false to read from ROM.</param>
    /// <remarks>
    /// Controls whether reads from $D000-$FFFF access the language card RAM or the
    /// built-in ROM. Independent of write enable, allowing read-from-ROM,
    /// write-to-RAM configurations.
    /// </remarks>
    void SetHighRead(bool enabled);

    /// <summary>
    /// Sets the language card pre-write state.
    /// </summary>
    /// <param name="enabled">True to activate pre-write sequence; false otherwise.</param>
    /// <remarks>
    /// The language card uses a two-access sequence to enable writing. The first access
    /// sets pre-write mode. A second consecutive access to the same address enables
    /// write mode. This is an internal state for the write protection mechanism.
    /// </remarks>
    void SetPreWrite(bool enabled);

    #endregion


    #region System State Mutations

    /// <summary>
    /// Sets the vertical blanking interval state ($C019, bit 7).
    /// </summary>
    /// <param name="active">True if in VBlank period; false during visible display.</param>
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
    /// </remarks>
    void SetVBlank(bool active);

    /// <summary>
    /// Sets the flash state for flashing characters.
    /// </summary>
    /// <param name="flashOn">True if flash characters should be displayed in inverse; false for normal.</param>
    /// <remarks>
    /// <para>
    /// The Apple IIe automatically alternates the display of flashing characters
    /// (codes $40-$7F) between normal and inverse video at approximately 2 Hz.
    /// This property reflects the current phase of that cycle.
    /// </para>
    /// <para>
    /// <strong>Timing:</strong> Typically toggled by the emulator at ~2.1 Hz to match
    /// Apple IIe hardware behavior (managed by <see cref="VA2M"/> flash timer).
    /// </para>
    /// </remarks>
    void SetFlashOn(bool flashOn);

    /// <summary>
    /// Sets the current effective CPU speed in megahertz.
    /// </summary>
    /// <param name="mhz">The current effective CPU speed (e.g., 1.023 for stock Apple IIe speed).</param>
    /// <remarks>
    /// <para>
    /// This value is updated periodically by <see cref="VA2M"/> based on actual emulation performance.
    /// It reflects the real-time measured speed, not the target speed.
    /// </para>
    /// <para>
    /// <strong>Typical Values:</strong>
    /// <list type="bullet">
    /// <item>~1.023 MHz: Stock Apple IIe speed (throttled mode)</item>
    /// <item>10-15 MHz: Typical unthrottled speed on modern hardware</item>
    /// <item>-1.0: Not yet measured (initial value)</item>
    /// </list>
    /// </para>
    /// </remarks>
    void SetCurrentMhz(double mhz);
    #endregion
}
