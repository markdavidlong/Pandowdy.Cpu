using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Threading;
using Emulator;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Pandowdy.Core;


public interface IAppleIIBus : IBus
{
    public void SetKeyValue(byte key);
    public void SetPushButton(int num, bool enabled);
}

/// <summary>
/// VA2M-specific Bus that owns the CPU connection and routes reads/writes to VA2MMemory.
/// Emits a VBlank event based on CPU cycles (every 17063 cycles ~= 60Hz at 1.023MHz).
/// Threading: All public methods (Connect, CpuRead/Write, Clock, Reset) are expected to be called ONLY from the emulator worker thread.
/// VBlank event is raised on the emulator thread; subscribers on the UI thread MUST marshal via dispatcher.
/// After disposal, no further events are raised and Clock() becomes a no-op.
/// </summary>
public sealed class VA2MBus(MemoryPool mempool, ISystemStatusProvider? statusProvider = null) : IAppleIIBus, IDisposable
{
    private readonly ISystemStatusProvider? _status = statusProvider;

    private readonly MemoryPool _memoryPool = mempool;
    private int lastPc = 0;
    private AppleSoftHookTable? _hookTable;
    private CPU? _cpu;
    private ulong _systemClock;

    public IMemory RAM => _memoryPool;

    public ulong SystemClockCounter => _systemClock;
    private bool _disposed;
    private byte _currKey = 0x00;

    // Cycle-based VBlank (1,023,000 Hz / 60 ? 17050; using 17063 from spec for Apple II NTSC)
    private const ulong CyclesPerVBlank = 17063; // adjust if more precise timing required
    private ulong _nextVblankCycle = CyclesPerVBlank;
    public event EventHandler? VBlank;

    // 2.1 Hz flash timer (approx every 476 ms)
    private Timer? _flashTimer;
    private static readonly TimeSpan FlashPeriod = TimeSpan.FromMilliseconds(476);

    public void Connect(CPU cpu)
    {
        ThrowIfDisposed();
        _cpu = cpu;
        _cpu.Connect(this);
        _hookTable ??= new AppleSoftHookTable();
        _hookTable.InitializeDefault();
        // Start flash timer if status provider available
        if (_status != null && _flashTimer == null)
        {
            _flashTimer = new Timer(_ =>
            {
                if (_disposed) { return; }
                _status!.Mutate(b => b.StateFlashOn = !b.StateFlashOn);
            }, null, FlashPeriod, FlashPeriod);
        }
    }

    public void SetKeyValue(byte key)
    {
        _currKey = key;
    }


    public void SetPushButton(int num, bool pressed)
    {
        _status!.Mutate(b =>
        {
            switch (num)
            {
                case 0:
                    b.StatePb0 = pressed;
                    break;
                case 1:
                    b.StatePb1 = pressed;
                    break;
                case 2:
                    b.StatePb2 = pressed;
                    break;
            }
        });
    }


    void Set80Store(bool enabled)
    {
        _status!.Mutate(b => b.State80Store = enabled);
        _memoryPool.Set80Store(enabled);
    }

    void SetRamRead(bool enabled)
    {
        _status!.Mutate(b => b.StateRamRd = enabled);
        _memoryPool.SetRamRd(enabled);
    }
    void SetRamWrite(bool enabled)
    {
        _status!.Mutate(b => b.StateRamWrt = enabled);
        _memoryPool.SetRamWrt(enabled);
    }


    void SetSlotCxRom()
    {
        _status!.Mutate(b => b.StateIntCxRom = false);
        _memoryPool.SetIntCxRom(false);
    }

    void SetIntCxRom()
    {
        _status!.Mutate(b => b.StateIntCxRom = true);
        _memoryPool.SetIntCxRom(true);
    }

    void SetAltZp(bool enabled)
    {
        _status!.Mutate(b => b.StateAltZp = enabled);
        _memoryPool.SetAltZp(enabled);
    }

    void SetSlotC3Rom(bool enabled)
    {
        _status!.Mutate(b => b.StateSlotC3Rom = enabled);
        _memoryPool.SetSlotC3Rom(enabled);
    }

    void SetShow80Col(bool enabled)
    {
        _status!.Mutate(b => b.StateShow80Col = enabled);
    }

    void SetAltCharSet(bool enabled)
    {
        _status!.Mutate(b => b.StateAltCharSet = enabled);
    }

    void SetTextMode(bool enabled)
    {
        _status!.Mutate(b => b.StateTextMode = enabled);
    }

    void SetMixed(bool enabled)
    {
        _status!.Mutate(b => b.StateMixed = enabled);
    }

    void SetPage2(bool enabled)
    {
        _status!.Mutate(b => b.StatePage2 = enabled);
    }

    void SetHires(bool enabled)
    {
        _status!.Mutate(b => b.StateHiRes = enabled);
    }

    void SetAnnunciator(int num, bool enabled) { 
        _status!.Mutate(b =>
            {
                switch (num)
                {
                    case 0:
                        b.StateAnn0 = enabled;
                        break;
                    case 1:
                        b.StateAnn1 = enabled;
                        break;
                    case 2:
                        b.StateAnn2 = enabled;
                        break;
                    case 3:
                        b.StateAnn3 = enabled;
                        break;
                }
            });
    }

    void ClearWrtCount()
    {
        _status!.Mutate(b => b.StateWriteCount = 0);
    }

    void IncrementWrtCount()
    {
        int count = _status!.StateWriteCount + 1;

        _status!.Mutate(b => b.StateWriteCount = count);
        if (count >= 2)
        {
            count = 2;
            _status!.Mutate(b => b.StateHighWrite = true);
            _memoryPool.SetHighWrite(true);
        }
    }

    void SetBank1(bool status)
    {
        _status!.Mutate(b => b.StateUseBank1 = status);
        _memoryPool.SetBank1(status);
    }
    void SetHighRead(bool status)
    {
        _status!.Mutate(b => b.StateHighRead = status);
        _memoryPool.SetHighRead(status);
    }
    void DisableHighWrite()
    {
        _status!.Mutate(b => b.StateHighWrite = false);
        _memoryPool.SetHighWrite(false);
    }

    private byte ReadFromIOSpace(ushort address)
    {
        if (address >= 0xC000 && address <= 0xC00F)
        {
/*
            if (address == 0xC001)
            {
                Set80Store(true);
            }

            if (address == 0xc002)
            {
                SetRamRead(false);
            }
            if (address == 0xC003)
            {
                SetRamRead(true);
            }
            if (address == 0xC004)
            {
                SetRamWrite(false);
            }
            if (address == 0xC005)
            {
                SetRamWrite(true);
            }
            if (address == 0xC006)
            {
                SetCxRom(false);
            }
            if (address == 0xC007)
            {
                SetCxRom(true);
            }
            if (address == 0xC008)
            {
                SetAltZp(false);
            }
            if (address == 0xC009)
            {
                SetAltZp(true);
            }
            if (address == 0xC00A)
            {
                SetSlotC3Rom(false);
            }
            if (address == 0xC00B)
            {
                SetSlotC3Rom(true);
            }
            if (address == 0xC00C)
            {
                SetShow80Col(false);
            }
            if (address == 0xC00D)
            {
                SetShow80Col(true);
            }
            if (address == 0xC00E)
            {
                SetAltCharSet(false);
            }
            if (address == 0xC00F)
            {
                SetAltCharSet(true);
            }
*/
            return _currKey;
        }


        if (address == 0xC010)
        {
            _currKey &= 0x7f; // Clear high byte;
                              //Temporary fix:
            return _currKey; // Should return 0x80 if key is pressed or 0x00 if not; simplified to always 0x00
        }

        if (address == 0xC011)
        {
            // Status of selected $Dx bank
            // NOT IMPLEMENTED YET
            return _currKey;
        }

        if (address == 0xC012)
        {
            // Status of $Dx ROM / $Dx RAM
            // NOT IMPLEMENTED YET
            return _currKey;
        }

        if (address == 0xC013)
        {
            return (byte) ((_status!.StateRamRd ? 0x80 : 0x00) + _currKey & 0x7f);
        }

        if (address == 0xC014)
        {
            return (byte) ((_status!.StateRamWrt ? 0x80 : 0x00) + _currKey & 0x7f);
        }

        if (address == 0xC015)
        {
            return (byte) ((_status!.StateIntCxRom ? 0x80 : 0x00) + _currKey & 0x7f);
        }

        if (address == 0xC016)
        {
            return (byte) ((_status!.StateAltZp ? 0x80 : 0x00) + _currKey & 0x7f);
        }

        if (address == 0xC017)
        {
            return (byte) ((_status!.StateSlotC3Rom ? 0x80 : 0x00) + _currKey & 0x7f);
        }

        if (address == 0xC018)
        {
            return (byte) ((_status!.State80Store ? 0x80 : 0x00) + _currKey & 0x7f);
        }

        if (address == 0xC019)
        {
            // Vert Blanking Status => 1 = Drawing
            // NOT IMPLEMENTED YET
            return _currKey;
        }

        if (address == 0xC01A)
        {
            return (byte) ((_status!.StateTextMode ? 0x80 : 0x00) + _currKey & 0x7f);
        }

        if (address == 0xC01B)
        {
            return (byte) ((_status!.StateMixed ? 0x80 : 0x00) + _currKey & 0x7f);
        }

        if (address == 0xC01C)
        {
            return (byte) ((_status!.StatePage2 ? 0x80 : 0x00) + _currKey & 0x7f);
        }

        if (address == 0xC01D)
        {
            return (byte) ((_status!.StateHiRes ? 0x80 : 0x00) + _currKey & 0x7f);
        }

        if (address == 0xC01E)
        {
            return (byte) ((_status!.StateAltCharSet ? 0x80 : 0x00) + _currKey & 0x7f);
        }

        if (address == 0xC01F)
        {
            return (byte) ((_status!.StateShow80Col ? 0x80 : 0x00) + _currKey & 0x7f);
        }

        if (address >= 0xC020 && address <= 0xC02F) // TAPEOUT
        {
            // NOT IMPLEMENTED YET
            return 0;
        }

        if (address >= 0xC030 && address <= 0xC03F) // SPKR
        {
            // NOT IMPLEMENTED YET
            return 0;
        }

        if (address >= 0xC040 && address <= 0xC04F) // STROBE
        {
            // NOT IMPLEMENTED YET
            return 0;
        }

        if (address == 0xC050) // TEXT OFF
        {
            SetTextMode(false);
            return 0xA0; // Placeholder value
        }
        if (address == 0xC051) // TEXT ON
        {
            SetTextMode(true);
            return 0xA0; // Placeholder value
        }

        if (address == 0xC052) // MIXED OFF
        {
            SetMixed(false);
            return 0xA0; // Placeholder value
        }

        if (address == 0xC053) // MIXED ON
        {
            SetMixed(true);
            return 0xA0; // Placeholder value
        }

        if (address == 0xC054) // PAGE2 OFF
        {
            SetPage2(false);
            return 0xA0; // Placeholder value
        }
        if (address == 0xC055) // PAGE2 ON
        {
            SetPage2(true);
            return 0xA0; // Placeholder value
        }
        if (address == 0xC056) // HIRES OFF
        {
            SetHires(false);
            return 0xA0; // Placeholder value
        }
        if (address == 0xC057) // HIRES ON
        {
            SetHires(true);
            return 0xA0; // Placeholder value
        }

        if (address == 0xC058) // ANN0
        {
            SetAnnunciator(0, false);
            return (byte) (_status!.StateAnn0 ? 0x80 : 0x00);
        }
        if (address == 0xC059)  
        {
            SetAnnunciator(0, true);
            return (byte) (_status!.StateAnn0 ? 0x80 : 0x00);
        }
        if (address == 0xC05A)  // ANN 1
        {
            SetAnnunciator(1, false);
            return (byte) (_status!.StateAnn1 ? 0x80 : 0x00);
        }
        if (address == 0xC05B)  
        {
            SetAnnunciator(1, true);
            return (byte) (_status!.StateAnn1 ? 0x80 : 0x00);
        }
        if (address == 0xC05C)  // ANN 2
        {
            SetAnnunciator(2, false);
            return (byte) (_status!.StateAnn2 ? 0x80 : 0x00);
        }
        if (address == 0xC05D)  
        {
            SetAnnunciator(2, true);
            return (byte) (_status!.StateAnn2 ? 0x80 : 0x00);
        }
        if (address == 0xC05E) // ANN 3  (or DHGR)
        {
            SetAnnunciator(3, false);
            return (byte) (_status!.StateAnn3_DGR ? 0x80 : 0x00);
        }
        if (address == 0xC05F)  
        {
            SetAnnunciator(3, true);
            return (byte) (_status!.StateAnn3_DGR ? 0x80 : 0x00);
        }

        if (address == 0xC060) // TAPE IN
        {
            // NOT IMPLEMENTED YET
            return 0x00;
        }

        if (address == 0xC061) // Button 0
        {
            return (byte) (_status!.StatePb0 ? 0x80 : 0x00);
        }

        if (address == 0xC062) // Button 1
        {
            return (byte) (_status!.StatePb1 ? 0x80 : 0x00);
        }

        if (address == 0xC063) // Button 2
        {
            return (byte) (_status!.StatePb2 ? 0x80 : 0x00);
        }

        if (address == 0xC064) // Paddle 0
        {
            // NOT IMPLEMENTED YET
            return 0x00;
        }
        if (address == 0xC065) // Paddle 1
        {
            // NOT IMPLEMENTED YET
            return 0x00;
        }
        if (address == 0xC066) // Paddle 2
        {
            // NOT IMPLEMENTED YET
            return 0x00;
        }
        if (address == 0xC067) // Paddle 3
        {
            // NOT IMPLEMENTED YET
            return 0x00;
        }



        if (address == 0xC080 || address == 0xC084)
        {
            ClearWrtCount();
            SetBank1(false);
            SetHighRead(true);
            DisableHighWrite();
        }

        if (address == 0xC081 || address == 0xC085)
        {
            SetBank1(false);
            IncrementWrtCount();
            SetHighRead(false);
        }


        if (address == 0xC082 || address == 0xC086)
        {
            SetBank1(false);
            ClearWrtCount();
            SetHighRead(false);
            DisableHighWrite();
        }

        if (address == 0xC083 || address == 0xC087)
        {
            SetBank1(false);
            IncrementWrtCount();
            SetHighRead(true);
        }


        if (address == 0xC088 || address == 0xC08C)
        {
            ClearWrtCount();
            SetBank1(true);
            SetHighRead(true);
            DisableHighWrite();
        }

        if (address == 0xC089 || address == 0xC08D)
        {
            SetBank1(true);
            IncrementWrtCount();
            SetHighRead(false);
        }


        if (address == 0xC08A || address == 0xC08E)
        {
            SetBank1(true);
            ClearWrtCount();
            SetHighRead(false);
            DisableHighWrite();
        }

        if (address == 0xC08B || address == 0xC08F)
        {
            SetBank1(true);
            IncrementWrtCount();
            SetHighRead(true);
        }

        // C080 / C084 -> Bank2, WrtCount = 0, Read = true
        // C081 / C085 -> Bank2, WrtCount ++, Read = false
        // C082 / C086 -> Bank2, WrtCount = 0, Read = false;
        // C083 / C087 -> Bank2, WrtCount ++, Raed = true

        // C088 / C08C -> Bank1, WrtCount = 0, Read = true
        // C089 / C08D -> Bank1, WrtCount ++, Read = false
        // C08A / C08E -> Bank1, WrtCount = 0, Read = false
        // C08B / C08F -> Bank1, WrtCount ++, Read = true

        // Todo: Handle other I/O writes here if needed

        return 0x67; // Placehold Value
    }

    void WriteToIOSpace(ushort address, byte _ /*data*/)
    {


        if (_status != null && address >= 0xC000 && address <= 0xC0FF)
        {
            if (address == 0xC000)
            {
      //          Debug.WriteLine("80Store Off");
                Set80Store(false);
                return;
            }
            else if (address == 0xC001)
            {
   //             Debug.WriteLine("80Store On");
                Set80Store(true);
                return;
            }
            else if (address == 0xC002)
            {
                SetRamRead(false);
                return;
            }
            else if (address == 0xC003)
            {
                SetRamRead(true);
                return;
            }
            else if (address == 0xC004)
            {
                SetRamWrite(false);
                return;
            }
            else if (address == 0xC005)
            {
                SetRamWrite(true);
                return;
            }
            else if (address == 0xC006)
            {
                SetSlotCxRom();
                return;
            }
            else if (address == 0xC007)
            {
                SetIntCxRom();
                return;
            }
            else if (address == 0xC008)
            {
                SetAltZp(false);
                return;
            }
            else if (address == 0xC009)
            {
                SetAltZp(true);
                return;
            }
            else if (address == 0xC00A)
            {
                SetSlotC3Rom(false);
                return;
            }
            else if (address == 0xC00B)
            {
                SetSlotC3Rom(true);
                return;
            }
            else if (address == 0xC00C)
            {
                SetShow80Col(false);
                return;
            }
            else if (address == 0xC00D)
            {
                SetShow80Col(true);
                return;
            }
            else if (address == 0xC00E)
            {
                SetAltCharSet(false);
                return;
            }
            else if (address == 0xC00F)
            {
                SetAltCharSet(true);
                return;
            }

          

            if (address == 0xC010)
            {
                _currKey &= 0x7f; // Clear high byte;

                //var keyval = auxram.Read(0xC000);
                //ram.Write(0xC000, (byte)(keyval & 0x7F));
                return;
            }

            if (address == 0xC050)
            {
                SetTextMode(false);
                return;

            }
        
            if (address == 0xC051)
            {
                SetTextMode(true);
                return;
            }

            if (address == 0xC052)
            {
                SetMixed(false);
                return;
            }

            if (address == 0xC053)
            {
                SetMixed(true);
                return;
            }

            if (address == 0xC054)
            {
        //        Debug.WriteLine("Page2 Off");

                SetPage2(false);
                return;
            }

            if (address == 0xC055)
            {
   //             Debug.WriteLine("Page2 On");

                SetPage2(true);
                return;
            }

            if (address == 0xC056)
            {
                SetHires(false);
                return;
            }

            if (address == 0xC057)
            {
                SetHires(true);
                return;
            }

            if (address == 0xC058)
            {
                SetAnnunciator(0, false);
            }

            if (address == 0xC059)
            {
                SetAnnunciator(0, true);
            }

            if (address == 0xC05A)
            {
                SetAnnunciator(1, false);
            }

            if (address == 0xC05B)
            {
                SetAnnunciator(1, true);
            }

            if (address == 0xC05C)
            {
                SetAnnunciator(2, false);
            }

            if (address == 0xC05D)
            {
                SetAnnunciator(2, true);
            }

            if (address == 0xC05E)
            {
                SetAnnunciator(3, false);
            }

            if (address == 0xC05F)
            {
                SetAnnunciator(3, true);
            }

            if (address == 0xC080 || address == 0xC084)
            {
                ClearWrtCount();
                SetBank1(false);
                SetHighRead(true);
                DisableHighWrite();
            }

            if (address == 0xC081 || address == 0xC085)
            {
                SetBank1(false);
                ClearWrtCount();
                SetHighRead(true);
            }


            if (address == 0xC082 || address == 0xC086)
            {
                SetBank1(false);
                ClearWrtCount();
                SetHighRead(true);
                DisableHighWrite();
            }

            if (address == 0xC083 || address == 0xC087)
            {
                SetBank1(false);
                ClearWrtCount();
                SetHighRead(true);
            }

            if (address == 0xC088 || address == 0xC08C)
            {
                ClearWrtCount();
                SetBank1(true);
                SetHighRead(true);
                DisableHighWrite();
            }

            if (address == 0xC089 || address == 0xC08D)
            {
                SetBank1(true);
                ClearWrtCount();
                SetHighRead(true);
            }


            if (address == 0xC08A || address == 0xC08E)
            {
                SetBank1(true);
                ClearWrtCount();
                SetHighRead(true);
                DisableHighWrite();
            }

            if (address == 0xC08B || address == 0xC08F)
            {
                SetBank1(true);
                ClearWrtCount();
                SetHighRead(true);
            }

            // C080 / C084 -> Bank2, WrtCount = 0, Read = true
            // C081 / C085 -> Bank2, WrtCount = 0, Read = false
            // C082 / C086 -> Bank2, WrtCount = 0, Read = false;
            // C083 / C087 -> Bank2, WrtCount = 0, Read = true

            // C088 / C08C -> Bank1, WrtCount = 0, Read = true
            // C089 / C08D -> Bank1, WrtCount = 0, Read = false
            // C08A / C08E -> Bank1, WrtCount = 0, Read = false
            // C08B / C08F -> Bank1, WrtCount = 0, Read = true

        }

        // Todo: Handle C100-CFFF I/O writes here if needed
    }


    public byte CpuRead(ushort address, bool readOnly = false)
    {
        ThrowIfDisposed();

        if (address >= 0xC000 && address < 0xC100)
        {
            return ReadFromIOSpace(address);
        }
        else
        {

            //if (address >= 0xC100 && address < 0xD000)
            //{
            //    Debug.WriteLine($"Read from {address:X4}");
            //}


            return _memoryPool.Read(address);
        }
    }

    public void CpuWrite(ushort address, byte data)
    {
        ThrowIfDisposed();

        if (address >= 0xC000 && address < 0xC100)
        {
            WriteToIOSpace(address, data);
            return;
        }

        //if (address == 0x400)
        //{
        //    Debug.WriteLine($"Write to 0400: {data:X2}");
        //}

        _memoryPool.Write(address, data);
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
        SetRamRead(false);
        SetRamWrite(false);
        SetIntCxRom();
        SetHires(false);
        SetMixed(false);
        SetPage2(false);
        SetShow80Col(false);
        SetSlotC3Rom(false);
        Set80Store(false);
        SetAltCharSet(false);
        SetTextMode(true);
        SetAltZp(false);
        SetAnnunciator(0, false);
        SetAnnunciator(1, false);
        SetAnnunciator(2, false);
        SetAnnunciator(3, false);
        SetBank1(false);
        SetHighRead(false);
        DisableHighWrite();
        ClearWrtCount();


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
        _flashTimer?.Dispose();
        _flashTimer = null;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(VA2MBus));
    }
}
