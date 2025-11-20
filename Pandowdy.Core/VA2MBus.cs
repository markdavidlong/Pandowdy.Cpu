using Emulator;
using System;
using System.Diagnostics;
using System.Threading;

namespace Pandowdy.Core;

/// <summary>
/// VA2M-specific Bus that owns the CPU connection and routes reads/writes to VA2MMemory.
/// Emits a VBlank event based on CPU cycles (every 17063 cycles ~= 60Hz at 1.023MHz).
/// Threading: All public methods (Connect, CpuRead/Write, Clock, Reset) are expected to be called ONLY from the emulator worker thread.
/// VBlank event is raised on the emulator thread; subscribers on the UI thread MUST marshal via dispatcher.
/// After disposal, no further events are raised and Clock() becomes a no-op.
/// </summary>
public sealed class VA2MBus(VA2MMemory ram, VA2MMemory auxram, VA2MMemory ROM) : IBus, IDisposable
{
    private int lastPc = 0;
    private AppleSoftHookTable? _hookTable;
    private CPU? _cpu;
    private ulong _systemClock;
    public IMemory RAM => ram;
    private IMemory AuxRAM => auxram;
    private IMemory Rom => ROM;
    public ulong SystemClockCounter => _systemClock;
    private bool _disposed;

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

    public byte CpuRead(ushort address, bool readOnly = false)
    {
        ThrowIfDisposed();
        if (address < 0xC000)
        {
            return RAM.Read(address);
        }
        else if (address < 0xC100)
        {
            if (address == 0xC010)
            {
                var keyval = auxram.Read(0xC000);
                auxram.Write(0xC000, (byte)(keyval & 0x7F));
            }
            return RAM.Read(address);
        }
        else
        {
            return ROM.Read(address);
        }
    }

    public void CpuWrite(ushort address, byte data)
    {
        ThrowIfDisposed();
        if (address >= 0xD000) return;
        else if (address >= 0xC000)
        {
            if (address == 0xC010)
            {
                var keyval = auxram.Read(0xC000);
                ram.Write(0xC000, (byte)(keyval & 0x7F));
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
        if (_disposed) return;
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
        if (_disposed) return;
        _disposed = true;
        VBlank = null;
        _cpu = null;
        _hookTable = null;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(VA2MBus));
    }
}
