//------------------------------------------------------------------------------
// SoftSwitch.cs
//
// Implements the Apple IIe soft switch system, which controls memory mapping,
// video modes, and peripheral access through memory-mapped I/O addresses.
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
// DESIGN PATTERN: Observable + Command Pattern
// This implementation uses two complementary patterns:
//
/// **Responder Pattern:**
///    Components (MemoryPool, video renderer) register as ISoftSwitchResponder
///    and receive notifications when switches change. This decouples the soft
///    switch management from the components that react to switch changes.
//
// **Current Use:**
/// - DumpSoftSwitchStatus() shows which switches are active and how often they change
/// - Useful for verifying emulator behavior during development
//
// **Planned/Future Use:**
/// - **Performance Profiling:** Identify switches that toggle excessively, causing
///   unnecessary memory remapping overhead (RAMRD/RAMWRT thrashing)
/// - **Compatibility Testing:** Compare switch usage patterns between programs to
///   detect emulation inaccuracies
/// - **Regression Detection:** Track switch usage during test runs; changes in
///   patterns may indicate bugs introduced
/// - **UI Visualization:** Display real-time switch activity in debugger panel
///   (blinking indicators for frequently-toggled switches)
/// - **Save State Validation:** Include change counts in save states to detect
///   desync issues during replay
///
/// While this violates YAGNI (You Aren't Gonna Need It), the minimal cost
/// (single int increment per change) is justified by the anticipated debugging
/// value. The feature is passive and can be completely ignored when not needed.
///
/// THREAD SAFETY:
/// Not thread-safe. Soft switches are accessed from the CPU thread and should
/// not be modified from multiple threads concurrently. The responder pattern
/// assumes single-threaded CPU execution.
///
/// PERFORMANCE:
/// Switch changes trigger responder callbacks immediately. For switches that
/// affect memory mapping (RAMRD, RAMWRT, etc.), this causes UpdateMemoryMappings()
/// to run, which has a write-lock cost. However, soft switch changes are
/// relatively infrequent compared to memory accesses.
//------------------------------------------------------------------------------

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore;

/// <summary>
/// Represents a single Apple IIe soft switch with a name and boolean state.
/// </summary>
/// <remarks>
/// <para>
/// Apple IIe soft switches are memory-mapped I/O addresses ($C000-$C0FF) that control
/// hardware behavior. Examples: 80STORE ($C000/$C001), RAMRD ($C002/$C003),
/// TEXT ($C050/$C051), PAGE2 ($C054/$C055).
/// </para>
/// <para>
/// Inherits change-counting functionality to track how often each switch changes state,
/// useful for debugging and performance analysis.
/// </para>
/// </remarks>
public sealed class SoftSwitch(string name)  
{
    /// <summary>
    /// Gets the human-readable name of this soft switch (e.g., "80STORE", "RAMRD", "TEXT").
    /// </summary>
    /// <value>The switch name, typically matching Apple IIe documentation conventions.</value>
    public string Name { get; private set; } = name;

    public bool Value { get; set; }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(bool val = true) { Value = val; }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Get() { return Value; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Toggle() { Value = !Value; }

    /// <summary>
    /// Returns a string representation of the soft switch showing its name and current state.
    /// </summary>
    /// <returns>A string in the format "SwitchName: True/False".</returns>
    public override string ToString() => $"{Name}: {Value}";
}

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
        PreWrite
    }

    /// <summary>
    /// Internal dictionary mapping switch IDs to their corresponding SoftSwitch instances.
    /// </summary>
    private Dictionary<SoftSwitchId, SoftSwitch> _switches = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="SoftSwitches"/> class with all
    /// switches set to their default states.
    /// </summary>
    /// <remarks>
    /// All switches are initialized to false (off) except INTCXROM which defaults to true.
    /// This matches the Apple IIe power-on state where internal ROMs are enabled by default.
    /// </remarks>
    public SoftSwitches()
    {
        _switches[SoftSwitchId.Store80] = new SoftSwitch("80STORE");
        _switches[SoftSwitchId.RamRd] = new SoftSwitch("RAMRD");
        _switches[SoftSwitchId.RamWrt] = new SoftSwitch("RAMWRT");
        _switches[SoftSwitchId.IntCxRom] = new SoftSwitch("INTCXROM");
        _switches[SoftSwitchId.AltZp] = new SoftSwitch("ALTZP");
        _switches[SoftSwitchId.SlotC3Rom] = new SoftSwitch("SLOTC3ROM");
        _switches[SoftSwitchId.Vid80] = new SoftSwitch("80VID");
        _switches[SoftSwitchId.AltChar] = new SoftSwitch("ALTCHAR");
        _switches[SoftSwitchId.Text] = new SoftSwitch("TEXT");
        _switches[SoftSwitchId.Mixed] = new SoftSwitch("MIXED");
        _switches[SoftSwitchId.Page2] = new SoftSwitch("PAGE2");
        _switches[SoftSwitchId.HiRes] = new SoftSwitch("HIRES");
        _switches[SoftSwitchId.An0] = new SoftSwitch("AN0");
        _switches[SoftSwitchId.An1] = new SoftSwitch("AN1");
        _switches[SoftSwitchId.An2] = new SoftSwitch("AN2");
        _switches[SoftSwitchId.An3] = new SoftSwitch("AN3");
        _switches[SoftSwitchId.Bank1] = new SoftSwitch("BANK1");
        _switches[SoftSwitchId.HighWrite] = new SoftSwitch("HIGHWRITE");
        _switches[SoftSwitchId.HighRead] = new SoftSwitch("HIGHREAD");
        _switches[SoftSwitchId.PreWrite] = new SoftSwitch("PREWRITE");

        
     //   DumpSoftSwitchStatus("Init:");
    }

    /// <summary>
    /// Collection of registered responders that receive notifications when switches change.
    /// </summary>
    private HashSet<ISoftSwitchResponder> _responders = [];

    /// <summary>
    /// Writes the current state of all soft switches to the debug output.
    /// </summary>
    /// <param name="header">Optional header text to display before the switch status list.
    /// If provided, each switch status line will be indented.</param>
    /// <remarks>
    /// Output format: "SwitchName: On/Off (Changes: count)"
    /// Useful for debugging switch state during emulation and tracking switch activity patterns.
    /// </remarks>
    public void DumpSoftSwitchStatus(string header = "")
    {
        // Cycle through all soft switches and use Debug.WriteLine to show the switch name and On or Off, depending on its value
        if (!string.IsNullOrEmpty(header))
        {
            Debug.WriteLine(header);
        }
        foreach (var kvp in _switches)
        {
            if (!string.IsNullOrEmpty(header))
            {
                Debug.Write("    ");
            }
            string status = kvp.Value.Value ? "On" : "Off";
            Debug.WriteLine($"{kvp.Value.Name}: {status}");
        }

    }

    /// <summary>
    /// Registers a component to receive notifications when soft switches change.
    /// </summary>
    /// <param name="responder">The responder to add. Must implement <see cref="ISoftSwitchResponder"/>.</param>
    /// <remarks>
    /// Responders receive immediate callbacks when switches change via the <see cref="TriggerResponder"/>
    /// method. Typical responders include MemoryPool (for memory remapping) and video renderers
    /// (for display mode changes).
    /// </remarks>
    public void AddResponder(ISoftSwitchResponder responder)
    {
        _responders.Add(responder);
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
            kvp.Value.Value = (kvp.Key==SoftSwitchId.IntCxRom);
            TriggerResponder(kvp.Key, kvp.Value.Value);
        }
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
        if (_switches.TryGetValue(id, out var softSwitch))
        {
            softSwitch.Value = value;
        }
        TriggerResponder(id, value);
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

    /// <summary>
    /// Returns a snapshot of all switches with their current state and change counts.
    /// </summary>
    /// <returns>A list of tuples containing switch ID and current valuefor each switch.</returns>
    /// <remarks>
    /// Useful for diagnostics, save state serialization, and UI display of switch status.
    /// The change count indicates how many times each switch has toggled since reset.
    /// </remarks>
    public List<(SoftSwitchId id, bool value)> GetSwitchList()
    {
        var result = new List<(SoftSwitchId id, bool value)>();
        foreach (var kvp in _switches)
        {
            result.Add((kvp.Key, kvp.Value.Value));
        }
        return result;
    }

    /// <summary>
    /// Notifies all registered responders about a switch state change.
    /// </summary>
    /// <param name="id">The switch that changed.</param>
    /// <param name="value">The new state of the switch.</param>
    /// <remarks>
    /// This method dispatches the switch change to the appropriate responder method
    /// based on the switch ID. Each responder receives a specific callback (e.g.,
    /// SetRamRd, SetText) matching the switch that changed. This allows responders
    /// to react differently to different switch types.
    /// </remarks>
    private void TriggerResponder(SoftSwitchId id, bool value = false)
    {
        foreach (var responder in _responders)
        {
            switch (id)
            {
                case SoftSwitchId.Store80:
                    responder.Set80Store(value);
                    break;

                case SoftSwitchId.RamRd:
                    responder.SetRamRd(value);
                    break;

                case SoftSwitchId.RamWrt:
                    responder.SetRamWrt(value);
                    break;

                case SoftSwitchId.IntCxRom:
                    responder.SetIntCxRom(value);
                    break;

                case SoftSwitchId.AltZp:
                    responder.SetAltZp(value);
                    break;

                case SoftSwitchId.SlotC3Rom:
                    responder.SetSlotC3Rom(value);
                    break;

                case SoftSwitchId.Vid80:
                    responder.Set80Vid(value);
                    break;

                case SoftSwitchId.AltChar:
                    responder.SetAltChar(value);
                    break;

                case SoftSwitchId.Text:
                    responder.SetText(value);
                    break;

                case SoftSwitchId.Mixed:
                    responder.SetMixed(value);
                    break;

                case SoftSwitchId.Page2:
                    responder.SetPage2(value);
                    break;

                case SoftSwitchId.HiRes:
                    responder.SetHiRes(value);
                    break;

                case SoftSwitchId.An0:
                    responder.SetAn0(value);
                    break;

                case SoftSwitchId.An1:
                    responder.SetAn1(value);
                    break;

                case SoftSwitchId.An2:
                    responder.SetAn2(value);
                    break;

                case SoftSwitchId.An3:
                    responder.SetAn3(value);
                    break;

                case SoftSwitchId.Bank1:
                    responder.SetBank1(value);
                    break;

                case SoftSwitchId.HighWrite:
                    responder.SetHighWrite(value);
                    break;

                case SoftSwitchId.HighRead:
                    responder.SetHighRead(value);
                    break;

                case SoftSwitchId.PreWrite:
                    responder.SetPreWrite(value);
                    break;
            }
        }
    }
}
