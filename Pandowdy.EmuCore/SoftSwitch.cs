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
// 1. **CountableVariable/CountableBool:**
///    Tracks value changes with a counter. Useful for debugging soft switch
///    activity and detecting unexpected state changes.
///
/// 2. **Responder Pattern:**
///    Components (MemoryPool, video renderer) register as ISoftSwitchResponder
///    and receive notifications when switches change. This decouples the soft
///    switch management from the components that react to switch changes.
///
// CHANGE TRACKING RATIONALE:
// The change-counting mechanism (CountableVariable) is built in anticipation
// of several debugging and profiling scenarios:
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
using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore
{
   
  

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
    public sealed class SoftSwitch(string name) : CountableBool
    {
        public string Name { get; private set; } = name;

        public override string ToString() => $"{Name}: {base.Value}";
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
        public enum SoftSwitchId
        {
            Store80,
            RamRd,
            RamWrt,
            IntCxRom,
            AltZp,
            SlotC3Rom,
            Vid80,  
            AltChar, 
            Text,  
            Mixed,  
            Page2,
            HiRes,
            An0,  
            An1,  
            An2,  
            An3, 
            Bank1,
            HighWrite,
            HighRead,
            PreWrite  
        }

        private Dictionary<SoftSwitchId, SoftSwitch> _switches = [];

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


            
         //   DumpSoftSwitchStatus("Init:"));
        }

        private HashSet<ISoftSwitchResponder> _responders = [];

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
                Debug.WriteLine($"{kvp.Value.Name}: {status} (Changes: {kvp.Value.Count})");
            }

        }

        public void AddResponder(ISoftSwitchResponder responder)
        {
            _responders.Add(responder);
        }



        public void ResetAllSwitches(bool resetCounts = false)
        {
            foreach (var kvp in _switches)
            {

                kvp.Value.Value = (kvp.Key==SoftSwitchId.IntCxRom);
                if (resetCounts)
                {
                    kvp.Value.ResetCount();
                }
                TriggerResponder(kvp.Key, kvp.Value.Value);
            }
            

            
        }

        public void Set(SoftSwitchId id, bool value)
        {
            if (_switches.TryGetValue(id, out var softSwitch))
            {
                softSwitch.Value = value;
            }
            TriggerResponder(id, value);
        }

        public bool Get(SoftSwitchId id)
        {
            if (_switches.TryGetValue(id, out var softSwitch))
            {
                return softSwitch.Value;
            }
            return false;
        }

        public List<(SoftSwitchId id, bool value, int count)> GetSwitchList()
        {
            var result = new List<(SoftSwitchId id, bool value, int count)>();
            foreach (var kvp in _switches)
            {
                result.Add((kvp.Key, kvp.Value.Value, kvp.Value.Count));
            }
            return result;
        }

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

        public void ResetSwitchUsageCounts()
        {
            foreach (var kvp in _switches)
            {
                kvp.Value.ResetCount();
            }
        }
    }
}
