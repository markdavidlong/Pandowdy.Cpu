using Emulator;

namespace Pandowdy.EmuCore.Interfaces
{
    public interface ICpu
    {
        public UInt16 PC { get; }
        public Byte SP { get; }
        public Byte A { get; }
        public Byte X { get ; }
        public Byte Y { get ; }
        public ProcessorStatus Status { get; }

        public void Clock(IAppleIIBus bus);
        public void InterruptRequest(IAppleIIBus bus);
        public bool IsInstructionComplete();
        public void NonMaskableInterrupt(IAppleIIBus bus);
        public byte Read(ushort address);
        public void Reset(IAppleIIBus bus);
        public string ToString();
        public void Write(ushort address, byte data);
    }
}