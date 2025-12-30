using System.Reflection.Metadata.Ecma335;
using Emulator;
using Pandowdy.EmuCore;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Services;
using Xunit;

namespace Pandowdy.Tests
{
    // Simple stub responder that records last values set
    internal sealed class StubSoftSwitchResponderAndSystemStatusProvider : ISoftSwitchResponder, ISystemStatusProvider
    {
        public bool RamRd, RamWrt, AltZp, Store80, HiRes, Page2, IntCxRom, SlotC3Rom, HighWrite, Bank1, HighRead, Text;
        public bool PreWrite, Mixed, An0, An1, An2, An3, AltChar, Vid80;

        public void SetRamRd(bool ramRd) => RamRd = ramRd;
        public bool StateRamRd { get => RamRd; }

        public void SetRamWrt(bool ramWrt) => RamWrt = ramWrt;
        public bool StateRamWrt { get => RamWrt; }

        public void SetAltZp(bool altZp) => AltZp = altZp;
        public bool StateAltZp { get => AltZp; }

        public void Set80Store(bool store80) => Store80 = store80;
        public bool State80Store { get => Store80; }

        public void SetHiRes(bool hires) => HiRes = hires;
        public bool StateHiRes { get => HiRes; }

        public void SetPage2(bool page2) => Page2 = page2;
        public bool StatePage2 { get => Page2; }

        public void SetIntCxRom(bool intCxRom) => IntCxRom = intCxRom;
        public bool StateIntCxRom { get => IntCxRom; }

        public void SetSlotC3Rom(bool slotC3Rom) => SlotC3Rom = slotC3Rom;
        public bool StateSlotC3Rom { get => SlotC3Rom; }

        public void SetHighWrite(bool enabled) => HighWrite = enabled;
        public bool StateHighWrite { get => HighWrite; }


        public void SetBank1(bool enabled) => Bank1 = enabled;
        public bool StateUseBank1{ get => Bank1; }

        public void SetHighRead(bool enabled) => HighRead = enabled;
        public bool StateHighRead { get => HighRead; }

        public void SetText(bool enabled) => Text = enabled;
        public bool StateTextMode{ get => Text; }

        public void SetPreWrite(bool enabled) => PreWrite = enabled;
        public bool StatePreWrite{ get => PreWrite; }

        public void SetMixed(bool enabled) => Mixed = enabled;
        public bool StateMixed { get => Mixed; }

        public void SetAn0(bool enabled) => An0 = enabled;
        public bool StateAnn0 { get => An0; }

        public void SetAn1(bool enabled) => An1 = enabled;
        public bool StateAnn1 { get => An1; }

        public void SetAn2(bool enabled) => An2 = enabled;
        public bool StateAnn2 { get => An2; }

        public void SetAn3(bool enabled) => An3 = enabled;
        public bool StateAnn3_DGR { get => An3; }

        public void SetAltChar(bool enabled) => AltChar = enabled;
        
        public void Set80Vid(bool enabled) => Vid80 = enabled;
        public bool StateShow80Col{ get => Vid80; }

        public bool StatePb0 { get => false; }
        public bool StatePb1 { get => false; }
        public bool StatePb2 { get => false; }
        public bool StateFlashOn { get => false; }
        public bool StateAltCharSet{ get => false; }

        public event EventHandler<SystemStatusSnapshot>? Changed;
        public SystemStatusSnapshot Current { get => null; }

        public IObservable<SystemStatusSnapshot> Stream { get => null; }

        public void Mutate(Action<SystemStatusSnapshotBuilder> _) { }


    }

    public class SoftSwitchResponderTests
    {
        private static VA2MBus CreateBus(StubSoftSwitchResponderAndSystemStatusProvider stub, /*out SystemStatusProvider status,*/ out MemoryPool mem)
        {
            mem = new MemoryPool();
            ICpu cpu = new CPUAdapter(new Emulator.CPU());
            var bus = new VA2MBus(mem, stub, cpu);
            //bus.Connect(cpu);
            return bus;
        }

        [Fact]
        public void Toggle_80Store_via_IO_Write_updates_status_and_responder()
        {
            var stub = new StubSoftSwitchResponderAndSystemStatusProvider();
            var bus = CreateBus(stub, /*out var _status,*/ out _);

            bus.CpuWrite(VA2MBus.CLR80STORE_, 0); // 80STORE ON
            //Assert.True(status.State80Store);
            Assert.True(stub.Store80);

            bus.CpuWrite(VA2MBus.SET80STORE_, 0); // 80STORE OFF
            //Assert.False(status.State80Store);
            Assert.False(stub.Store80);
        }

        [Fact]
        public void Toggle_RamRd_RamWrt_updates_status_and_responder()
        {
            var stub = new StubSoftSwitchResponderAndSystemStatusProvider();
            var bus = CreateBus(stub, /*out var _status,*/ out _);


            bus.CpuWrite(VA2MBus.RDCARDRAM_, 0); // RAMRD ON
            //Assert.True(status.StateRamRd);
            Assert.True(stub.RamRd);

            bus.CpuWrite(VA2MBus.RDMAINRAM_, 0); // RAMRD OFF
            //Assert.False(status.StateRamRd);
            Assert.False(stub.RamRd);

            bus.CpuWrite(VA2MBus.WRCARDRAM_, 0); // RAMWRT ON
            //Assert.True(status.StateRamWrt);
            Assert.True(stub.RamWrt);

            bus.CpuWrite(VA2MBus.WRMAINRAM_, 0); // RAMWRT OFF
            //Assert.False(status.StateRamWrt);
            Assert.False(stub.RamWrt);
        }

        [Fact]
        public void Toggle_IntCxRom_and_SlotC3Rom_updates_status_and_responder()
        {
            var stub = new StubSoftSwitchResponderAndSystemStatusProvider();
            var bus = CreateBus(stub, /*out var _status,*/ out _);


            bus.CpuWrite(VA2MBus.INTCXROM_, 0); // INTCXROM ON
            //Assert.True(status.StateIntCxRom);
            Assert.True(stub.IntCxRom);

            bus.CpuWrite(VA2MBus.SLOTCXROM_, 0); // INTCXROM OFF
            //Assert.False(status.StateIntCxRom);
            Assert.False(stub.IntCxRom);

            bus.CpuWrite(VA2MBus.SLOTC3ROM_, 0); // SLOTC3ROM ON
            //Assert.True(status.StateSlotC3Rom);
            Assert.True(stub.SlotC3Rom);

            bus.CpuWrite(VA2MBus.INTC3ROM_, 0); // SLOTC3ROM OFF
            //Assert.False(status.StateSlotC3Rom);
            Assert.False(stub.SlotC3Rom);
        }

        [Fact]
        public void Toggle_Text_Mixed_Page2_Hires_updates_status_and_responder()
        {
            var stub = new StubSoftSwitchResponderAndSystemStatusProvider();
            var bus = CreateBus(stub, /*out var _status,*/ out _);

            bus.CpuWrite(0xC051, 0); // TEXT ON
            //Assert.True(status.StateTextMode);

            bus.CpuWrite(0xC053, 0); // MIXED ON
            //Assert.True(status.StateMixed);

            bus.CpuWrite(0xC055, 0); // PAGE2 ON
            //Assert.True(status.StatePage2);
            Assert.True(stub.Page2);

            bus.CpuWrite(0xC057, 0); // HIRES ON
            //Assert.True(status.StateHiRes);
            Assert.True(stub.HiRes);
        }

        [Fact]
        public void Banking_Bank1_Read_Write_softswitches_update_status_and_responder()
        {
            var stub = new StubSoftSwitchResponderAndSystemStatusProvider();
            var bus = CreateBus(stub, /*out var _status,*/ out _);

            // Defaults: Bank2 selected (StateUseBank1 == false), HighRead false, HighWrite false
            //Assert.False(status.StateUseBank1);
            Assert.False(stub.Bank1);
            //Assert.False(status.StateHighRead);
            Assert.False(stub.HighRead);
            //Assert.False(status.StateHighWrite);
            Assert.False(stub.HighWrite);

            // Bank1 on
            bus.CpuWrite(0xC088, 0);
            //Assert.True(status.StateUseBank1);
            Assert.True(stub.Bank1);
            //Assert.True(status.StateHighRead);
            Assert.True(stub.HighRead);
            //Assert.False(status.StateHighWrite);
            Assert.False(stub.HighWrite);

            // Bank1 write-read toggle sequence
            bus.CpuWrite(0xC089, 0); // increment write count, read=false
            //Assert.False(status.StateHighRead);

            bus.CpuWrite(0xC08B, 0); // read=true
            //Assert.True(status.StateHighRead);
        }
        [Fact]
        public void Banking_Bank2_Read_Write_softswitches_update_status_and_responder()
        {
            var stub = new StubSoftSwitchResponderAndSystemStatusProvider();
            var bus = CreateBus(stub, /*out var _status,*/ out _);

            // Defaults: Bank2 selected (StateUseBank1 == false), HighRead false, HighWrite false
            //Assert.False(status.StateUseBank1);
            Assert.False(stub.Bank1);
            ////Assert.False(status.StateHighRead);
            Assert.False(stub.HighRead);
            //Assert.False(status.StateHighWrite);
            Assert.False(stub.HighWrite);

            // Bank2 on
            bus.CpuWrite(0xC080, 0);
            //Assert.False(status.StateUseBank1);
            Assert.False(stub.Bank1);
            //Assert.True(status.StateHighRead);
            Assert.True(stub.HighRead);
            //Assert.False(status.StateHighWrite);
            Assert.False(stub.HighWrite);

            // Bank2 write-read toggle sequence
            bus.CpuWrite(0xC081, 0); // increment write count, read=false
            //Assert.False(status.StateHighRead);

            bus.CpuWrite(0xC083, 0); // read=true
            //Assert.True(status.StateHighRead);
        }

        [Fact]
        public void Default_softswitch_states_at_initialization()
        {
            var stub = new StubSoftSwitchResponderAndSystemStatusProvider();
            var bus = CreateBus(stub, /*out var _status,*/ out _);

            Assert.NotNull(bus);
            
            // Status provider defaults
            //Assert.False(status.State80Store);
            //Assert.False(status.StateRamRd);
            //Assert.False(status.StateRamWrt);
            //Assert.True(status.StateIntCxRom);
            //Assert.False(status.StateAltZp);
            //Assert.False(status.StateSlotC3Rom);
            //Assert.False(status.StatePage2);
            //Assert.False(status.StateHiRes);
            //Assert.False(status.StateMixed);
            //Assert.True(status.StateTextMode);
            //Assert.False(status.StateShow80Col);
            //Assert.False(status.StateAltCharSet);
            //Assert.False(status.StateHighRead);
            //Assert.False(status.StateHighWrite);
            //Assert.False(status.StateUseBank1); // Bank2 default

            // Stub recorder defaults
            Assert.False(stub.Store80);
            Assert.False(stub.RamRd);
            Assert.False(stub.RamWrt);
            Assert.False(stub.IntCxRom); // responder not invoked yet
            Assert.False(stub.AltZp);
            Assert.False(stub.SlotC3Rom);
            Assert.False(stub.Page2);
            Assert.False(stub.HiRes);
            Assert.False(stub.HighRead);
            Assert.False(stub.HighWrite);
            Assert.False(stub.Bank1);
        }
    }
}
