using Emulator;
using System;
using System.Diagnostics;

namespace Pandowdy.Core;


/// <summary>
/// VA2M-specific Bus that owns the CPU connection and routes reads/writes to VA2MMemory.
/// Emits a VBlank event at ~60Hz based on wall-clock time.
/// </summary>
public sealed class VA2MBus(VA2MMemory ram, VA2MMemory auxram, VA2MMemory ROM) : IBus
{
    private int lastPc = 0;
    private AppleSoftHookTable? _hookTable; // externalized hook table

    private CPU? _cpu;
    private ulong _systemClock;
    public IMemory RAM => ram; // IMemory-typed view of VA2MMemory
    private IMemory AuxRAM => auxram;
    private IMemory Rom => ROM;
    public ulong SystemClockCounter => _systemClock;

    // VBlank at ~60Hz wall-clock
    private readonly long _vblankIntervalTicks = Stopwatch.Frequency / 60;
    private long _nextVblankTicks = Stopwatch.GetTimestamp();
    public event EventHandler? VBlank;

    public void Connect(CPU cpu)
    {
        _cpu = cpu;
        _cpu.Connect(this);
        // Initialize hook table lazily
        _hookTable ??= new AppleSoftHookTable();
        _hookTable.InitializeDefault();
    }

    public byte CpuRead(ushort address, bool readOnly = false)
    {
        if (address < 0xC000)
        {
            return RAM.Read(address);
        }
        else if (address < 0xC100)
        {
            if (address == 0xC010)
            {
                var keyval = auxram.Read(0xC000);
                auxram.Write(0xC000, (byte)(keyval & 0x7F)); // clear high bit on read of strobe
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
        if (address >= 0xD000)
        {
            // ROM area is not writable
            return;
        }
        else if (address >= 0xC000)
        {
            if (address == 0xC010)
            {
                var keyval = auxram.Read(0xC000);
                ram.Write(0xC000, (byte)(keyval & 0x7F)); // clear high bit on read of strobe
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
        // PC trace (preserved)
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
                hook(-1, lineNum, spcs); // 1 = BASIC commands, 3= more detailed interpreter calls
            }

            #region LegacyTrace_DoNotRemove
            // The following debug trace code is intentionally left commented for
            // temporary instrumentation during AppleSoft tracing. Do not remove.
            //if (currPc ==0xD805)
            //{
            // var lineNum = CpuRead(0x75) + (CpuRead(0x76) *256);
            // if (lineNum <0xFF00)
            // {
            // Debug.WriteLine($"LineNum: {lineNum}");
            // }
            // else
            // {
            // Debug.WriteLine("LineNum: IMMEDIATE");
            // }
            //}
            //else if (currPc ==0xD766)
            //{
            // Debug.WriteLine(" FOR");
            //}
            //else if (currPc ==0xDCF9)
            //{
            // Debug.WriteLine(" NEXT");
            //}
            #endregion
        }
        lastPc = currPc;

        // Execute one CPU cycle
        _cpu!.Clock();
        _systemClock++;

        // Wall-clock driven VBlank: coalesce multiple due events so we don't drift
        long now = Stopwatch.GetTimestamp();
        if (now >= _nextVblankTicks)
        {
            do { _nextVblankTicks += _vblankIntervalTicks; } while (now >= _nextVblankTicks);
            VBlank?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Reset()
    {
        _cpu!.Reset();
        _systemClock = 0;
        _nextVblankTicks = Stopwatch.GetTimestamp() + _vblankIntervalTicks;
    }
}
