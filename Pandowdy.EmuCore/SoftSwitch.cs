using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;
using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore
{
    // Basic stub representing a single soft switch

    public class CountableVariable<T>(T initialValue)
    {
        protected T _value = initialValue;
        protected int _count = 0;

        public T Value
        {
            get => _value;
            set
            {
                if (!Equals(_value, value))
                {
                    _value = value;
                    _count++;
                }
            }
        }

        public int Count => _count;

        public void ResetCount() => _count = 0;

        public override string ToString()
        {
            if (Value != null)
            {
                return Value.ToString() + $" ({_count})";
            }
            else
            {
                return $"null ({_count})";
            }
        }

        public static string ToDebugString(CountableVariable<T> variable)
        {
            return $"Value: {variable._value}, ChangeCount: {variable._count}";
        }
    }

    public class CountableBool : CountableVariable<bool>
    {
        public CountableBool() : base(false)
        {
        }

        public void Set()
        { Value = true; }

        public void Clear()
        { Value = false; }

        public void Toggle()
        { Value = !Value; }
    }

    public sealed class SoftSwitch(string name) : CountableBool
    {
        public string Name { get; private set; } = name;

        public override string ToString() => $"{Name}: {base.Value}";
    }

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