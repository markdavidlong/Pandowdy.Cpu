using Emulator;
using System;
using System.Diagnostics;
using System.Threading;

namespace Pandowdy.Core;


public interface IAppleIIBus : IBus
{
    public void SetKeyValue(byte key);
}

/// <summary>
/// VA2M-specific Bus that owns the CPU connection and routes reads/writes to VA2MMemory.
/// Emits a VBlank event based on CPU cycles (every 17063 cycles ~= 60Hz at 1.023MHz).
/// Threading: All public methods (Connect, CpuRead/Write, Clock, Reset) are expected to be called ONLY from the emulator worker thread.
/// VBlank event is raised on the emulator thread; subscribers on the UI thread MUST marshal via dispatcher.
/// After disposal, no further events are raised and Clock() becomes a no-op.
/// </summary>
public sealed class VA2MBus(VA2MMemory ram,  VA2MMemory ROM, ISystemStatusProvider? statusProvider = null) : IAppleIIBus, IDisposable
{
    private readonly ISystemStatusProvider? _status = statusProvider;

    private int lastPc = 0;
    private AppleSoftHookTable? _hookTable;
    private CPU? _cpu;
    private ulong _systemClock;
    public IMemory RAM => ram;

    private IMemory Rom => ROM;
    public ulong SystemClockCounter => _systemClock;
    private bool _disposed;
    private byte _currKey = 0x00;

    // Cycle-based VBlank (1,023,000 Hz / 60 ? 17050; using 17063 from spec for Apple II NTSC)
    private const ulong CyclesPerVBlank = 17063; // adjust if more precise timing required
    private ulong _nextVblankCycle = CyclesPerVBlank;
    public event EventHandler? VBlank;

    public void Connect(CPU cpu)
    {
        ThrowIfDisposed();
        _cpu = cpu;
        _cpu.Connect(this);
        _hookTable ??= new AppleSoftHookTable();
        _hookTable.InitializeDefault();
    }


    public void SetKeyValue(byte key)
    {
     //   if (_currKey == 0)
        {
            _currKey = key;
        }
    }

    public byte CpuRead(ushort address, bool readOnly = false)
    {
        ThrowIfDisposed();

        if (address < 0xC000)
        {
            return RAM.Read(address);
        }
        else if (address < 0xC100)
        {
            if (address == 0xc000)
            { 
                return _currKey;
            }
            if (address == 0xC010)
            {
                _currKey &= 0x7f; // Clear high byte;
                //Temporary fix:
                return 0x00; // Should return 0x80 if key is pressed or 0x00 if not; simplified to always 0x00

            }
            else if (address == 0xc013)
            {
                return (byte) (_status!.StateRamRd ? 0x80 : 0x00);
            }
            else if (address == 0xc014)
            {
                return (byte) (_status!.StateRamWrt ? 0x80 : 0x00);
            }
            else if (address == 0xc015)
            {
                return (byte) (_status!.StateIntCxRom ? 0x80 : 0x00);
            }
            else if (address == 0xc016)
            {
                return (byte) (_status!.StateAltZp ? 0x80 : 0x00);
            }
            else if (address == 0xc017)
            {
                return (byte) (_status!.StateSlotC3Rom? 0x80 : 0x00);
            }
            else if (address == 0xc018)
            {
                return (byte) (_status!.State80Store ? 0x80 : 0x00);
            }
            // Soft switch update before returning data (Apple II triggers on read)
            if (_status != null && address >= 0xC050 && address <= 0xC057)
            {
                _status.Mutate(b =>
                {
                    switch (address)
                    {
                        case 0xC050:
                            b.StateTextMode = false;
                            break;   // TEXT OFF
                        case 0xC051:
                            b.StateTextMode = true;
                            break;    // TEXT ON
                        case 0xC052:
                            b.StateMixed = false;
                            break;      // MIXED OFF
                        case 0xC053:
                            b.StateMixed = true;
                            break;       // MIXED ON
                        case 0xC054:
                            b.StatePage2 = false;
                            break;      // PAGE2 OFF
                        case 0xC055:
                            b.StatePage2 = true;
                            break;       // PAGE2 ON
                        case 0xC056:
                            b.StateHiRes = false;
                            break;      // HIRES OFF
                        case 0xC057:
                            b.StateHiRes = true;
                            break;       // HIRES ON
                    }
                });
                // Return a dummy value (Apple II returns floating bus); keep existing semantics.
                return 0x5f;
            }

            return 0x67; // RAM.Read(address);
        }
        else
        {
            if (_status!.StateRamRd)
            {
                return RAM.Read(address);
            }
            else
            {
                return ROM.Read(address);
            }
        }
    }

    public void CpuWrite(ushort address, byte data)
    {
        ThrowIfDisposed();
        if (address >= 0xD000)
        {
            return;
        }
        else if (address >= 0xC000)
        {
            if (_status != null && address >= 0xC000 && address <= 0xC0FF)
            {
                _status.Mutate(b =>
                {
                    switch (address)
                    {
                        case 0xC000:
                            b.State80Store = false;
                            break;
                        case 0xC001:
                            b.State80Store = true;
                            break;
                        case 0xC002:
                            b.StateRamRd = false;
                            break;
                        case 0xC003:
                            b.StateRamRd = true;
                            break;
                        case 0xC004:
                            b.StateRamWrt = false;
                            break;
                        case 0xC005:
                            b.StateRamWrt = true;
                            break;
                        case 0xC006:
                            b.StateIntCxRom = false;
                            break;
                        case 0xC007:
                            b.StateIntCxRom = true;
                            break;
                        case 0xC008:
                            b.StateAltZp = false;
                            break;
                        case 0xC009:
                            b.StateAltZp = true;
                            break;
                        case 0xC00A:
                            b.StateSlotC3Rom = false;
                            break;
                        case 0xC00B:
                            b.StateSlotC3Rom = true;
                            break;
                        default: break;
                    }
                });
            }

            if (address == 0xC010)
            {
                _currKey &= 0x7f; // Clear high byte;

                //var keyval = auxram.Read(0xC000);
                //ram.Write(0xC000, (byte)(keyval & 0x7F));
                return;
            }
            ram.Write(address, data);
        }
        else
        {
            ram.Write(address, data);
        }
    }

    public void Clock()
    {
        if (_disposed)
        {
            return;
        }

        var currPc = _cpu!.PC;
        if (lastPc != currPc)
        {
            var hook = _hookTable?.Get((ushort)currPc);
            if (hook != null)
            {
                var lineNum = CpuRead(0x75) + (CpuRead(0x76) * 256);
                string ln = lineNum < 0xFA00 ? lineNum.ToString() : "IMM";
                var sp = _cpu!.SP;
                var spcs = 0xFF - sp;
                hook(-1, lineNum, spcs);
            }
            #region LegacyTrace_DoNotRemove
            // (Preserved commented instrumentation)
            #endregion
        }
        lastPc = currPc;

        _cpu!.Clock();
        _systemClock++;

        if (_systemClock >= _nextVblankCycle)
        {
            // Catch up if emulator ran fast (unthrottled batches)
            do { _nextVblankCycle += CyclesPerVBlank; } while (_systemClock >= _nextVblankCycle);
            if (!_disposed)
            {
                VBlank?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public void Reset()
    {
        ThrowIfDisposed();
        _cpu!.Reset();
        _systemClock = 0;
        _nextVblankCycle = CyclesPerVBlank;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        VBlank = null;
        _cpu = null;
        _hookTable = null;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(VA2MBus));
        }
    }
}
