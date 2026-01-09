//------------------------------------------------------------------------------
// SoftSwitches.cs
//
// Manages all Apple IIe soft switches and coordinates state changes with the
// SystemStatusProvider.
//
// APPLE IIe SOFT SWITCH ARCHITECTURE:
// The Apple IIe uses "soft switches" - memory locations in the $C000-$C0FF
// range that control hardware behavior when read or written. Unlike typical
// memory-mapped I/O, many soft switches change state simply by being accessed,
// regardless of the data value written.
//
// Examples:
// - $C050: Select text mode (just reading/writing this address activates it)
// - $C051: Select graphics mode
// - $C054: Select page 1, $C055: Select page 2
//
// DESIGN PATTERN: Direct Coupling with SystemStatusProvider
// This implementation directly mutates the SystemStatusProvider when switches
// change, eliminating the overhead of the responder pattern since only one
// component (SystemStatusProvider) needs to track switch states.
//
// Components that need to react to switch changes (like MemoryPool) subscribe
// to SystemStatusProvider's MemoryMappingChanged event instead of implementing
// a responder interface.
//
// THREAD SAFETY:
// Not thread-safe. Soft switches are accessed from the CPU thread and should
// not be modified from multiple threads concurrently.
//
// PERFORMANCE:
// Switch changes trigger direct mutation of SystemStatusProvider, which fires
// appropriate events (Changed, MemoryMappingChanged) to notify subscribers.
//------------------------------------------------------------------------------

using System.Diagnostics;
using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Services;

namespace Pandowdy.EmuCore;

/// <summary>
/// Manages all Apple IIe soft switches and notifies responders when switches change.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Responder Pattern:</strong> Components register as <see cref="ISoftSwitchResponder"/>
/// to receive notifications when switches change. This decouples switch management from
/// the components that react to changes (MemoryPool, video renderer, etc.).
/// </para>
/// <para>
/// <strong>Switch Categories:</strong>
/// Memory Mapping (RAMRD, RAMWRT, ALTZP, 80STORE, BANK1),
/// Video Mode (TEXT, MIXED, HIRES, PAGE2, 80VID, ALTCHAR),
/// ROM Selection (INTCXROM, SLOTC3ROM),
/// Annunciators (AN0-AN3).
/// </para>
/// </remarks>
public sealed class SoftSwitches
{
    /// <summary>
    /// Identifies specific Apple IIe soft switches for type-safe access.
    /// </summary>
    /// <remarks>
    /// Each enum value corresponds to a specific Apple IIe soft switch that controls
    /// memory mapping, video modes, ROM selection, or annunciators.
    /// </remarks>
    public enum SoftSwitchId
    {
        /// <summary>
        /// 80STORE switch ($C000/$C001). When enabled, affects whether PAGE2 switch 
        /// controls auxiliary memory or video display page selection.
        /// </summary>
        Store80,
        
        /// <summary>
        /// RAMRD switch ($C002/$C003). Controls whether reads from certain memory
        /// ranges come from main or auxiliary memory.
        /// </summary>
        RamRd,
        
        /// <summary>
        /// RAMWRT switch ($C004/$C005). Controls whether writes to certain memory
        /// ranges go to main or auxiliary memory.
        /// </summary>
        RamWrt,
        
        /// <summary>
        /// INTCXROM switch ($C006/$C007). When enabled, accesses to $C100-$CFFF
        /// use internal ROM instead of peripheral card ROMs.
        /// </summary>
        IntCxRom,
        
        /// <summary>
        /// ALTZP switch ($C008/$C009). When enabled, zero page ($0000-$01FF) and
        /// stack ($0100-$01FF) are mapped to auxiliary memory.
        /// </summary>
        AltZp,
        
        /// <summary>
        /// SLOTC3ROM switch ($C00A/$C00B). When enabled, accesses to $C300-$C3FF
        /// use internal ROM instead of slot 3 card ROM.
        /// </summary>
        SlotC3Rom,
        
        /// <summary>
        /// 80VID switch ($C00C/$C00D). Enables 80-column video mode when set.
        /// </summary>
        Vid80,
        
        /// <summary>
        /// ALTCHAR switch ($C00E/$C00F). Selects alternate character set for text mode.
        /// </summary>
        AltChar,
        
        /// <summary>
        /// TEXT switch ($C050/$C051). When enabled, display shows text mode;
        /// when disabled, shows graphics mode.
        /// </summary>
        Text,
        
        /// <summary>
        /// MIXED switch ($C052/$C053). When enabled, displays graphics with 4 lines
        /// of text at the bottom of the screen.
        /// </summary>
        Mixed,
        
        /// <summary>
        /// PAGE2 switch ($C054/$C055). Selects display page (page 1 or page 2)
        /// and may affect auxiliary memory access depending on 80STORE state.
        /// </summary>
        Page2,
        
        /// <summary>
        /// HIRES switch ($C056/$C057). When enabled, selects high-resolution graphics
        /// mode; when disabled, selects low-resolution graphics mode.
        /// </summary>
        HiRes,
        
        /// <summary>
        /// Annunciator 0 ($C058/$C059). General-purpose output bit, often used
        /// for peripheral control.
        /// </summary>
        An0,
        
        /// <summary>
        /// Annunciator 1 ($C05A/$C05B). General-purpose output bit, often used
        /// for peripheral control.
        /// </summary>
        An1,
        
        /// <summary>
        /// Annunciator 2 ($C05C/$C05D). General-purpose output bit, often used
        /// for peripheral control.
        /// </summary>
        An2,
        
        /// <summary>
        /// Annunciator 3 ($C05E/$C05F). General-purpose output bit, often used
        /// for peripheral control (commonly used for double hi-res mode).
        /// </summary>
        An3,
        
        /// <summary>
        /// BANK1 switch. Selects which bank of auxiliary memory is active for
        /// the language card area ($D000-$FFFF).
        /// </summary>
        Bank1,
        
        /// <summary>
        /// HIGHWRITE switch. Controls write protection for the language card
        /// RAM in the $D000-$FFFF range.
        /// </summary>
        HighWrite,
        
        /// <summary>
        /// HIGHREAD switch. Controls whether reads from $D000-$FFFF come from
        /// RAM or ROM.
        /// </summary>
        HighRead,
        
        /// <summary>
        /// PREWRITE switch. Pre-write state for language card write protection.
        /// Two consecutive reads of certain addresses are required to enable writing.
        /// </summary>
        PreWrite,

        /// <summary>
        /// VBlank switch ($C060). Indicates vertical blanking interval for video.
        /// </summary>
        VBlank
    }

    /// <summary>
    /// Internal dictionary mapping switch IDs to their corresponding SoftSwitch instances.
    /// </summary>
    private Dictionary<SoftSwitchId, SoftSwitch> _switches = [];

  

    private SystemStatusProvider _status;

    /// <summary>
    /// Initializes a new instance of the <see cref="SoftSwitches"/> class with all
    /// switches set to their default states.
    /// </summary>
    /// <remarks>
    /// All switches are initialized to false (off) except INTCXROM which defaults to true.
    /// This matches the Apple IIe power-on state where internal ROMs are enabled by default.
    /// </remarks>
    public SoftSwitches(SystemStatusProvider status)
    {
        ArgumentNullException.ThrowIfNull(status);
        _status = status;

        _switches[SoftSwitchId.Store80] = new SoftSwitch("80STORE", _status.State80Store);
        _switches[SoftSwitchId.RamRd] = new SoftSwitch("RAMRD", _status.StateRamRd);
        _switches[SoftSwitchId.RamWrt] = new SoftSwitch("RAMWRT", _status.StateRamWrt);
        _switches[SoftSwitchId.IntCxRom] = new SoftSwitch("INTCXROM", _status.StateIntCxRom);
        _switches[SoftSwitchId.AltZp] = new SoftSwitch("ALTZP", _status.StateAltZp);
        _switches[SoftSwitchId.SlotC3Rom] = new SoftSwitch("SLOTC3ROM", _status.StateSlotC3Rom);
        _switches[SoftSwitchId.Vid80] = new SoftSwitch("80VID", _status.StateShow80Col);
        _switches[SoftSwitchId.AltChar] = new SoftSwitch("ALTCHAR", _status.StateAltCharSet);
        _switches[SoftSwitchId.Text] = new SoftSwitch("TEXT", _status.StateTextMode);
        _switches[SoftSwitchId.Mixed] = new SoftSwitch("MIXED", _status.StateMixed);
        _switches[SoftSwitchId.Page2] = new SoftSwitch("PAGE2", _status.StatePage2);
        _switches[SoftSwitchId.HiRes] = new SoftSwitch("HIRES", _status.StateHiRes);
        _switches[SoftSwitchId.An0] = new SoftSwitch("AN0", _status.StateAnn0);
        _switches[SoftSwitchId.An1] = new SoftSwitch("AN1", _status.StateAnn1);
        _switches[SoftSwitchId.An2] = new SoftSwitch("AN2", _status.StateAnn2);
        _switches[SoftSwitchId.An3] = new SoftSwitch("AN3", _status.StateAnn3_DGR);
        _switches[SoftSwitchId.Bank1] = new SoftSwitch("BANK1", _status.StateUseBank1);
        _switches[SoftSwitchId.HighWrite] = new SoftSwitch("HIGHWRITE", _status.StateHighWrite);
        _switches[SoftSwitchId.HighRead] = new SoftSwitch("HIGHREAD", _status.StateHighRead);
        _switches[SoftSwitchId.PreWrite] = new SoftSwitch("PREWRITE", _status.StatePreWrite);
        _switches[SoftSwitchId.VBlank] = new SoftSwitch("VBLANK", _status.StateVBlank);

        ResetAllSwitches();
    }

   


    /// <summary>
    /// Resets all soft switches to their default power-on state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Default state: All switches off except INTCXROM which is on (matching Apple IIe power-on).
    /// </para>
    /// <para>
    /// All registered responders are notified of the state changes to ensure memory mappings
    /// and video modes are properly initialized.
    /// </para>
    /// </remarks>
    public void ResetAllSwitches()
    {
        foreach (var kvp in _switches)
        {
            kvp.Value.Value = (kvp.Key == SoftSwitchId.IntCxRom);
            SetStatus(kvp.Key, kvp.Value.Value); 
        }
    }


    public bool QuietlySet(SoftSwitchId id, bool value)
    {
        if (_switches.TryGetValue(id, out var softSwitch))
        {
            softSwitch.Value = value;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Sets the state of a specific soft switch and notifies all registered responders.
    /// </summary>
    /// <param name="id">The identifier of the switch to modify.</param>
    /// <param name="value">The new state for the switch (true = on, false = off).</param>
    /// <remarks>
    /// This method updates the switch state and immediately triggers responder callbacks,
    /// which may cause memory remapping or video mode changes. The change counter for
    /// the switch is automatically incremented if the value changes.
    /// </remarks>
    public void Set(SoftSwitchId id, bool value)
    {
        bool oldVal = Get(id);
        if (QuietlySet(id, value))
        {
            if (value != oldVal)
            {
                SetStatus(id, value);  
            }
        }    

    }

    /// <summary>
    /// Retrieves the current state of a specific soft switch.
    /// </summary>
    /// <param name="id">The identifier of the switch to query.</param>
    /// <returns>True if the switch is on (enabled), false if off (disabled) or not found.</returns>
    public bool Get(SoftSwitchId id)
    {
        if (_switches.TryGetValue(id, out var softSwitch))
        {
            return softSwitch.Value;
        }
        return false;
    }



    private void SetStatus(SoftSwitchId id, bool value = false)
    {
  
            switch (id)
            {
            case SoftSwitchId.Store80:
                _status.Set80Store(value);
                break;

            case SoftSwitchId.RamRd:
                _status.SetRamRd(value);
                break;

            case SoftSwitchId.RamWrt:
                _status.SetRamWrt(value);
                break;

            case SoftSwitchId.IntCxRom:
                _status.SetIntCxRom(value);
                break;

            case SoftSwitchId.AltZp:
                _status.SetAltZp(value);
                break;

            case SoftSwitchId.SlotC3Rom:
                _status.SetSlotC3Rom(value);
                break;

            case SoftSwitchId.Vid80:
                _status.Set80Vid(value);
                break;

            case SoftSwitchId.AltChar:
                _status.SetAltChar(value);
                break;

            case SoftSwitchId.Text:
                _status.SetText(value);
                break;

            case SoftSwitchId.Mixed:
                _status.SetMixed(value);
                break;

            case SoftSwitchId.Page2:
                _status.SetPage2(value);
                break;

            case SoftSwitchId.HiRes:
                _status.SetHiRes(value);
                break;

            case SoftSwitchId.An0:
                _status.SetAn0(value);
                break;

            case SoftSwitchId.An1:
                _status.SetAn1(value);
                break;

            case SoftSwitchId.An2:
                _status.SetAn2(value);
                break;

            case SoftSwitchId.An3:
                _status.SetAn3(value);
                break;

            case SoftSwitchId.Bank1:
                _status.SetBank1(value);
                break;

            case SoftSwitchId.HighWrite:
                _status.SetHighWrite(value);
                break;

            case SoftSwitchId.HighRead:
                _status.SetHighRead(value);
                break;

            case SoftSwitchId.PreWrite:
                _status.SetPreWrite(value);
                break;


            case SoftSwitchId.VBlank:
                _status.SetVBlank(value);
                break;
            
        }
    }
}
