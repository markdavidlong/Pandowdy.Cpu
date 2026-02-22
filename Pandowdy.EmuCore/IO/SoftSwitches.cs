// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

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

using System.Runtime.CompilerServices;
using Pandowdy.EmuCore.Machine;

namespace Pandowdy.EmuCore.IO;

/// <summary>
/// Manages all Apple IIe soft switches with direct coupling to SystemStatusProvider.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Direct Coupling Pattern:</strong> This implementation directly mutates the
/// SystemStatusProvider when switches change, eliminating the overhead of the responder
/// pattern since only one component (SystemStatusProvider) needs to track switch states.
/// Components that need to react to switch changes (like memory subsystems) subscribe
/// to SystemStatusProvider's MemoryMappingChanged event.
/// </para>
/// <para>
/// <strong>Switch Categories:</strong>
/// Memory Mapping (RAMRD, RAMWRT, ALTZP, 80STORE, BANK1),
/// Video Mode (TEXT, MIXED, HIRES, PAGE2, 80VID, ALTCHAR),
/// ROM Selection (INTCXROM, SLOTC3ROM),
/// Annunciators (AN0-AN3).
/// </para>
/// </remarks>
[Capability(typeof(IRestartable))]
public sealed class SoftSwitches : IRestartable
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

        /// /// <summary>
        /// SLOTC3ROM switch ($C00A/$C00B). When enabled, accesses to $C300-$C3FF
        /// use slot 3 card ROM instead of internal ROM. Only applies when INTCXROM is OFF.
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
        VBlank,

        /// <summary>
        /// Status of Internal C800-CFFF ROM switch for slot 3 use (See Sather p. 5-28 (p. 100 of new edition))
        /// </summary>
        IntC8Rom
    }

    /// <summary>
    /// Array-based storage for switch values, indexed by SoftSwitchId enum value.
    /// </summary>
    /// <remarks>
    /// Uses array instead of Dictionary for O(1) direct indexing without hashing overhead.
    /// This is a hot path accessed on every soft switch read/write.
    /// </remarks>
    private readonly bool[] _switchValues = new bool[23]; // Count of SoftSwitchId values + 1

  

    private SystemStatusProvider _status;

    /// <summary>
    /// Initializes a new instance of the <see cref="SoftSwitches"/> class with all
    /// switches set to their default states.
    /// </summary>
    /// <remarks>
    /// All switches are initialized to false (off).
    /// This matches the Apple IIe power-on state where internal ROMs are enabled by default.
    /// </remarks>
    public SoftSwitches(SystemStatusProvider status)
    {
        ArgumentNullException.ThrowIfNull(status);
        _status = status;

        // Initialize switch values from current status
        _switchValues[(int)SoftSwitchId.Store80] = _status.State80Store;
        _switchValues[(int)SoftSwitchId.RamRd] = _status.StateRamRd;
        _switchValues[(int)SoftSwitchId.RamWrt] = _status.StateRamWrt;
        _switchValues[(int)SoftSwitchId.IntCxRom] = _status.StateIntCxRom;
        _switchValues[(int)SoftSwitchId.AltZp] = _status.StateAltZp;
        _switchValues[(int)SoftSwitchId.SlotC3Rom] = _status.StateSlotC3Rom;
        _switchValues[(int)SoftSwitchId.Vid80] = _status.StateShow80Col;
        _switchValues[(int)SoftSwitchId.AltChar] = _status.StateAltCharSet;
        _switchValues[(int)SoftSwitchId.Text] = _status.StateTextMode;
        _switchValues[(int)SoftSwitchId.Mixed] = _status.StateMixed;
        _switchValues[(int)SoftSwitchId.Page2] = _status.StatePage2;
        _switchValues[(int)SoftSwitchId.HiRes] = _status.StateHiRes;
        _switchValues[(int)SoftSwitchId.An0] = _status.StateAnn0;
        _switchValues[(int)SoftSwitchId.An1] = _status.StateAnn1;
        _switchValues[(int)SoftSwitchId.An2] = _status.StateAnn2;
        _switchValues[(int)SoftSwitchId.An3] = _status.StateAnn3_DGR;
        _switchValues[(int)SoftSwitchId.Bank1] = _status.StateUseBank1;
        _switchValues[(int)SoftSwitchId.HighWrite] = _status.StateHighWrite;
        _switchValues[(int)SoftSwitchId.HighRead] = _status.StateHighRead;
        _switchValues[(int)SoftSwitchId.PreWrite] = _status.StatePreWrite;
        _switchValues[(int)SoftSwitchId.VBlank] = _status.StateVBlank;
        _switchValues[(int)SoftSwitchId.IntC8Rom] = _status.StateIntC8Rom;

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
        // Reset all switches to false, except INTCXROM which defaults to true
        for (int i = 0; i < _switchValues.Length; i++)
        {
            bool defaultValue = false;  // (i == (int)SoftSwitchId.IntCxRom);
            _switchValues[i] = defaultValue;
            SetStatus((SoftSwitchId)i, defaultValue);
        }
    }

    /// <summary>
    /// Restores all soft switches to their initial power-on state (cold boot).
    /// </summary>
    /// <remarks>
    /// Delegates to <see cref="ResetAllSwitches"/> — for soft switches the power-on
    /// default (all false) is already the correct cold-boot state.
    /// </remarks>
    public void Restart()
    {
        ResetAllSwitches();
        // IntC8RomSlot is a byte (not a boolean switch) tracking which slot's
        // extended ROM is banked into $C800-$CFFF. Reset to 0 (no slot active)
        // alongside the IntC8Rom boolean that ResetAllSwitches() already clears.
        _status.SetIntC8RomSlot(0);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool QuietlySet(SoftSwitchId id, bool value)
    {
        int index = (int)id;
        if (index >= 0 && index < _switchValues.Length)
        {
            _switchValues[index] = value;
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
    /// This immediately updates soft switch state and sets the corresponding status
    /// </remarks>
    public void Set(SoftSwitchId id, bool value)
    {
        bool oldVal = Get(id);
        if (QuietlySet(id, value))
        {
            if (value != oldVal)
            {
    //            Debug.WriteLine($"Set id {id} to {value}");

                SetStatus(id, value);  
            }
        //    else
        //    {
        ////        Debug.WriteLine($"Did not re-set id {id} to old value {value}");

        //    }
        }
     //   else
     //   {
     ////       Debug.WriteLine($"Could not quietly set id {id}");
     //   }

    }

    /// <summary>
    /// Retrieves the current state of a specific soft switch.
    /// </summary>
    /// <param name="id">The identifier of the switch to query.</param>
    /// <returns>True if the switch is on (enabled), false if off (disabled) or not found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Get(SoftSwitchId id)
    {
        int index = (int)id;
        return index >= 0 && index < _switchValues.Length && _switchValues[index];
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

            case SoftSwitchId.IntC8Rom:
                _status.SetIntC8Rom(value);
                break;
        }
    }
}
