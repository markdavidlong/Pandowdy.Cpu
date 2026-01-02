using System.Runtime.CompilerServices;
using Emulator;
using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore
{
    public class CPUAdapter : ICpu
    {
        private Emulator.CPU _oldCpu;

        public UInt16 PC {get => _oldCpu.PC; }
        public Byte SP { get => _oldCpu.SP; }
        public Byte A { get => _oldCpu.A; }
        public Byte X { get => _oldCpu.X;  }
        public Byte Y { get => _oldCpu.Y; }
        public ProcessorStatus Status { get => _oldCpu.Status; }

        public CPUAdapter(Emulator.CPU cpu)
        {
            ArgumentNullException.ThrowIfNull(cpu);
            _oldCpu = cpu;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Byte Read(UInt16 address) { return _oldCpu.Read(address); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(UInt16 address, Byte data)
        {
            _oldCpu.Write(address, data);
        }

        public void Reset(IAppleIIBus bus) {
            _oldCpu.Connect(bus);
            _oldCpu.Reset();
            _oldCpu.Connect(null);
        }

        public bool IsInstructionComplete()
        {
            return _oldCpu.IsInstructionComplete();
        }

        public void InterruptRequest(IAppleIIBus bus)
        {
            _oldCpu.Connect(bus);
            _oldCpu.InterruptRequest();
            _oldCpu.Connect(null);

        }

        public void NonMaskableInterrupt(IAppleIIBus bus)
        {
            _oldCpu.Connect(bus);
            _oldCpu.NonMaskableInterrupt();
            _oldCpu.Connect(null);

        }

        public void Clock(IAppleIIBus bus)
        {
            _oldCpu.Connect(bus);
            _oldCpu.Clock();
            _oldCpu.Connect(null);
        }

        public override string ToString() { return _oldCpu.ToString(); }

    }
}
