using Emulator;

namespace Pandowdy.Core;

/// <summary>
/// VA2M-specific Bus that owns the CPU connection and routes reads/writes to VA2MMemory.
/// </summary>
public sealed class VA2MBus : IBus
{
    private readonly VA2MMemory _ram;
    private CPU? _cpu;
    private ulong _systemClock;

    public VA2MBus(VA2MMemory ram)
    {
        _ram = ram;
    }

    public IMemory RAM => _ram; // IMemory-typed view of VA2MMemory

    public ulong SystemClockCounter => _systemClock;

    public void Connect(CPU cpu)
    {
        _cpu = cpu;
        _cpu.Connect(this);
    }

    public byte CpuRead(ushort address, bool readOnly = false) => _ram.Read(address);

    public void CpuWrite(ushort address, byte data) => _ram.Write(address, data);

    public void Clock()
    {
        _cpu!.Clock();
        _systemClock++;
    }

    public void Reset()
    {
        _cpu!.Reset();
        _systemClock = 0;
    }
}
