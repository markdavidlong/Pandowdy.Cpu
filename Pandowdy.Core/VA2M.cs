using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Emulator;
using System.Reflection.Metadata.Ecma335;
using System.Collections.Concurrent;
using System.Diagnostics.Tracing;

namespace Pandowdy.Core;

public sealed class VA2M : IDisposable
{
    public MemoryPool MemoryPool { get; private set; } = new MemoryPool();

 //   public const int RamSize = 64 * 1024;

    public IAppleIIBus Bus { get; }

    private readonly CPU _cpu;
    private readonly Stopwatch _throttleSw = Stopwatch.StartNew();
    private long _throttleCycles;
    public bool ThrottleEnabled { get; set; } = true;
    public double TargetHz { get; set; } = 1_023_000d;
    public ulong SystemClock => Bus.SystemClockCounter;


    private readonly IEmulatorState? _stateSink; // optional DI-provided state publisher
    private readonly IFrameProvider? _frameSink; // optional DI-provided frame publisher
    private readonly ISystemStatusProvider? _sysStatusSink; // optional DI-provided status publisher

    // Flash timer to toggle StateFlashOn at ~2.1 Hz
    private Timer? _flashTimer;
    private static readonly TimeSpan FlashPeriod = TimeSpan.FromMilliseconds(1000/2.1);

    public VA2M() : this(null, null, null) { }

    public VA2M(IEmulatorState? stateSink, IFrameProvider? frameSink, ISystemStatusProvider? statusProvider = null)
    {
        _stateSink = stateSink;
        _frameSink = frameSink;
        _sysStatusSink = statusProvider;
        TryLoadEmbeddedRom("Pandowdy.Core.Resources.a2e_enh_c-f.rom");
        
        Bus = new VA2MBus(MemoryPool);
        _cpu = new CPU();
        Bus.Connect(_cpu);
        if (_frameSink is not null && Bus is VA2MBus vb)
        {
            vb.VBlank += OnVBlank;
        }
        // Start flash timer if status provider available
        if (_sysStatusSink != null && _flashTimer == null)
        {
            _flashTimer = new Timer(_ =>
            {
                try
                {
                    _sysStatusSink?.Mutate(s => s.StateFlashOn = !s.StateFlashOn);
                }
                catch { }
            }, null, FlashPeriod, FlashPeriod);
        }
    }

    
    // Queue for cross-thread emulator actions
    private readonly ConcurrentQueue<Action> _pending = new();

    private void Enqueue(Action action)
    {
        if (action != null)
        {
            _pending.Enqueue(action);
        }
    }

    private void ProcessPending()
    {
        while (_pending.TryDequeue(out var act))
        {
            try { act(); } catch { Debug.WriteLine($"Exception during ProcessPending()"); }
        }
    }


    private void RenderScreen(BitmapDataArray buf)
    {
        bool text = _sysStatusSink!.StateTextMode;
        bool hires = _sysStatusSink!.StateHiRes;
        bool mixed = _sysStatusSink!.StateMixed;
        bool page2 = _sysStatusSink!.StatePage2;
        bool text80col = _sysStatusSink!.StateShow80Col;
        bool gr80col = text80col && !_sysStatusSink!.StateAnn3_DGR;

        for (int row = 0; row < 24; row++)
        {
            for (int col = 0; col < 40; col++)
            {
                int addr = GetAddressForXY(col, row, text, hires, mixed, page2);
                if (addr >= 0x400 && addr <= 0xBFF) // Text/GR Pages 1/2
                {
                    RenderTextOrGRCell(addr, row, col, text, mixed, text80col, gr80col, buf);
                }
                else if (addr >= 0x2000 && addr <= 0x5fff) // HGR Pages 1/2
                {
                    RenderHiresCell(addr, row, col, gr80col, buf);
                }
            }
        }
    }

    private void RenderTextOrGRCell(int address, int row, int col, bool text, bool mixed, bool text80, bool gr80, BitmapDataArray buf)
    {
        if (text || (mixed && row >= 20))
        {
            RenderTextCell(address, row, col, text80, buf);
        }
        else
        {
            RenderGrCell(address, row, col, gr80, buf);
        }
    }


    private void RenderHiresCell(int address, int row, int col, bool gr80, BitmapDataArray buf)
    {
        // Render either 7 or 14 pixels, based on the state of gr80
        
        if (!gr80)
        {
            for (int r = 0; r < 8; r++)
            {
                ushort byteAddress = (ushort) (address + (r * 0x400));
                byte value = MemoryPool.Read(byteAddress);
                int buffY = row * 8 + r;
                bool prevShift = false;
                if (col != 0 && (MemoryPool.Read((ushort) (byteAddress-1)) & 0x80) == 0x80)
                {
                    prevShift = true;
                }
                buf.InsertHgrByteAt(col * 2 * 7, buffY, value, prevShift);
            }
        }

        // if 80-col
        //    iterate through the 8 columns in the cell
        //       look up the aux and main values at that address
        //       write aux/main into buffer unexpanded
    }

    private void RenderTextCell(int address, int row, int col, bool text80, BitmapDataArray buf)
    {
        bool flashOn = _sysStatusSink!.StateFlashOn;
        bool altChar = _sysStatusSink!.StateAltCharSet;
        
        byte ch = MemoryPool.Read((ushort) address);
        var glyph = VideoFont.Glyph(ch, flashOn, altChar); // returns span of 8 rows

        if (!text80)
        {

            for (int r = 0; r < 8; r++)  // 8 rows per glyph
            {
                int buffY = row * 8 + r;
                byte fontRow = (byte) ~glyph[r]; // invert bits (was glyph ^ 0xff intent)
                                                 //  fontRow = (byte)((y / 8) % 16 * 0x11);

                buf.Insert7BitLsbAt(col * 2 * 7, buffY, fontRow, true);

            }
        }
        else
        {
            byte ch1 = MemoryPool.ReadRawAux((ushort) address);
            var glyph1 = VideoFont.Glyph(ch1, flashOn, altChar); // returns span of 8 rows

            for (int r = 0; r < 8; r++)  // 8 rows per glyph
            {
                int y = row * 8 + r;
                byte fontRow1 = (byte) ~glyph1[r];  
                byte fontRow2 = (byte) ~glyph[r];
                int baseX = col * 2;
                {
                    buf.Insert7BitLsbAt(baseX * 7, y, fontRow1, false);
                    buf.Insert7BitLsbAt(baseX * 7 + 7, y, fontRow2, false);
                }
            }
        }
    }

    private void RenderGrCell(int address, int row, int col, bool gr80, BitmapDataArray buf)
    {
        // Render either 1 40-column or 2 80-column cells depending on the state of the 80-showflag and ann3 (dgr)
        // if 40 colunns
        if (!gr80)
        {
            byte value = MemoryPool.Read((ushort) address);

            for (int glyphRow = 0; glyphRow < 8; glyphRow++)
            {
                int y = row * 8 + glyphRow;

                byte grcolor = (byte) (value & 0x0f);
                if (glyphRow >= 4)
                {
                    grcolor = (byte) (value >> 4);
                }

                var (a1, a2, a3, a4) = MakeGrColor(grcolor);
                if (col % 2 == 0) // Even -- Use A1 & A2
                {
                    buf.SetByteAt(col * 14, y, (byte) a1);
                    buf.SetByteAt((col * 14 + 7), y, (byte) a2);
                }
                else // Odd -- Use A3 & A4
                {
                    buf.SetByteAt((col * 14), y, (byte) a3);
                    buf.SetByteAt((col * 14 + 7), y, (byte) a4);
                }
            }
        }
        // if 80 columns
        //    get the aux and main memory values at the address
        //       determine if we're in the top or bottom nybble of each
        //       get the proper colors for aux and main and their 4 mem values
        //       depending on whether we're even or odd columns draw 0/1 for aux/main values or 2/3 for aux/main into the proper buffer bytes
    }



    private static (byte, byte, byte, byte) MakeGrColor(byte val)
    {
        int x = (val & 0x7f) * 0x11111111;

        byte a = (byte) ((x >> 0) & 0x7f);
        byte b = (byte) ((x >> 3) & 0x7f);
        byte c = (byte) ((x >> 6) & 0x7f);
        byte d = (byte) ((x >> 9) & 0x7f);

        return (a, b, c, d);
    }



    // this is using 0-based X/Y
    private static int GetAddressForXY(int x, int y, bool text, bool hires, bool mixed, bool page2, int cellRowOffset = 0)
    {
        const int TextPage1Start = 0x0400;
        const int TextPage2Start = 0x0800;
        const int HiresPage1Start = 0x2000;
        const int HiresPage2Start = 0x4000;

        int retval = -1;

        //Todo: Note:  Page2 might have issues if 80-column mode is also on.  Revisit that later.

        if (x >= 0 && x < 40 && y >= 0 && y < 24)
        {

            if (text || (!text && !hires) || (mixed && y > 20))
            {
                int startAddr = page2 ? TextPage2Start : TextPage1Start;

                // Every 128 bytes is 40 columns at row x, then 40 columns at row x+8, then 40 columns at row x+16.  So row 0 starts at StartAddr, row 1 = startAddress+128, row 2 = startAddress+256, etc. This is normalized to (row % 8) * 128 to start with, then if row >= 8, add 40, if row >= 16 add another 40.
                retval = startAddr + (y % 8) * 128 + (y / 8) * 40 + x;
            }
            else // We're either HiRes full screen or HiRes Mixed with y <= 20
            {
                int startAddr = page2 ? HiresPage2Start : HiresPage1Start;

                retval = startAddr + (y % 8) * 128 + (y / 8) * 40 + (cellRowOffset * 0x400) + x;
            }
        }
        return retval;
    }
    private void OnVBlank(object? sender, EventArgs e)
    {
        if (_frameSink is null) { return; }
        var buf = _frameSink.BorrowWritable();
        buf.Clear();

        RenderScreen(buf);

        _frameSink.IsGraphics = !_sysStatusSink!.StateTextMode;
        _frameSink.IsMixed = _sysStatusSink!.StateMixed;
        _frameSink.CommitWritable();
    }


    private static (int,int)? AddressToOffset(int address)
    {
        if (address < 0x400 || address >= 0x800) { return null; }
        address -= 0x400;
        var macroline_x = address % 128;
        var macroline_y = address / 128;
        // Return -1 for screen holes.
        if (macroline_x >= 120) { return null; }
        int section = macroline_x / 40;
        int row = macroline_y + 8 * section;
        return (macroline_x % 40, row);
    }

    private void TryLoadEmbeddedRom(string resourceName)
    {
        // Temporarily disable exceptions for missing resources. Don't want it to silently catch.
        //    try
        {
            var asm = Assembly.GetExecutingAssembly();
            using Stream? s = asm.GetManifestResourceStream(resourceName);
            if (s != null)
            {
                using var ms = new MemoryStream();
                s.CopyTo(ms);
                MemoryPool.InstallApple2ROM(ms.ToArray());
            }
        }
   //     catch { }
    }

    /// <summary>
    /// Advance one system clock (one CPU/bus cycle). If throttling is enabled,
    /// the call will delay to keep approx TargetHz. Suitable for simple loops.
    /// </summary>
    public void Clock()
    {
        // Execute commands enqueued from other threads
        ProcessPending();
        Bus.Clock();
        _throttleCycles++;
        if (ThrottleEnabled)
        {
            ThrottleOneCycle();
        }
        PublishState();
    }

    private void ThrottleOneCycle()
    {
        // Expected elapsed time in seconds for executed cycles
        double expectedSec = _throttleCycles / TargetHz;
        double elapsedSec = _throttleSw.Elapsed.TotalSeconds;
        double leadSec = expectedSec - elapsedSec; // >0 means we are ahead (need to wait)
        if (leadSec <= 0) { return; }

        // Sleep for the whole milliseconds part
        int sleepMs = (int) (leadSec * 1000.0);
        if (sleepMs > 0)
        {
            Thread.Sleep(sleepMs);
        }
        // Busy-wait for the remaining sub-ms time slice
        while (_throttleSw.Elapsed.TotalSeconds < expectedSec)
        {
            Thread.SpinWait(100);
        }
    }

    /// <summary>
    /// Reset machine and system clock.
    /// </summary>
    public void Reset()
    {
        //Enqueue(() =>
        {
            Bus.Reset();
            _throttleCycles = 0;
            _throttleSw.Restart();
        }
        //);
    }

    public void UserReset()
    {
       // Enqueue(() =>
        {
            Debug.WriteLine("Calling UserReset() in VA2M");
            (Bus as VA2MBus)!.UserReset();
            //_throttleCycles = 0;
            //_throttleSw.Restart();
        }
   //     );
    }

    /// <summary>
    /// Run the emulator asynchronously with batched cycles and time slices.
    /// Batches cycles per tick (e.g.,1 ms or 60 Hz) to reduce overhead of per-cycle waits.
    /// When ThrottleEnabled is true, pacing uses the periodic timer to approximate TargetHz.
    /// When false, runs fast batches without waiting.
    /// </summary>
    /// <param name="ct">Cancellation token to stop the runner.</param>
    /// <param name="ticksPerSecond">Time slice frequency. Use 1000 for 1ms slices or 60 for video-frame pacing.</param>
    public async Task RunAsync(CancellationToken ct, double ticksPerSecond = 1000d)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ticksPerSecond);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1.0 / ticksPerSecond));
        double cyclesPerTick = TargetHz / ticksPerSecond;
        double carry = 0.0;
        while (!ct.IsCancellationRequested)
        {
            if (ThrottleEnabled)
            {
                try
                {
                    if (!await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                    {
                        break;
                    }
                }
                catch (OperationCanceledException) { break; }
                // Execute queued commands on emulator thread before cycles
                ProcessPending();
                double want = cyclesPerTick + carry;
                int cycles = (int)want;
                carry = want - cycles;
                for (int i = 0; i < cycles; i++)
                {
                    Bus.Clock();
                    _throttleCycles++;
                }
                PublishState();
            }
            else
            {
                const int FastBatch = 10_000;
                // Execute queued commands on emulator thread before fast batch
                    ProcessPending();
                for (int i = 0; i < FastBatch; i++)
                {
                    Bus.Clock();
                    _throttleCycles++;
                    if (ct.IsCancellationRequested)
                    {
                        break;
                    }
                }
                PublishState();
                await Task.Yield();
            }
        }
    }

    private void PublishState()
    {
        if (_stateSink is null) { return; }
        int lineNum = (int)(Bus.CpuRead(0x75) + (Bus.CpuRead(0x76) << 8));
        int? basicLine = lineNum < 0xFA00 ? lineNum : null;
        var snapshot = new StateSnapshot((ushort)_cpu.PC, (byte)_cpu.SP, Bus.SystemClockCounter, basicLine, true, false);
        _stateSink.Update(snapshot);
    }



    /// <summary>
    /// Inject a keyboard value into the machine as if a key was latched at $C000.
    /// High bit must be set.  Cleared by access of $C010.
    /// </summary>
    
    public void InjectKey(byte ascii)
    {
        // Enqueue to run on emulator thread
        byte val = (byte)(ascii | 0x80);
        Enqueue(() => Bus.SetKeyValue(val));
    }

    public void SetPushButton(byte num, bool pressed)
    {
        Enqueue(() => Bus.SetPushButton(num, pressed));
    }

    public void GenerateStatusData()
    {
        Enqueue(() => BuildStatusData());
    }

    private static readonly System.Collections.Generic.IReadOnlyDictionary<SoftSwitches.SoftSwitchId, System.Action<SystemStatusSnapshotBuilder, bool>> _switchSetters
        = new System.Collections.Generic.Dictionary<SoftSwitches.SoftSwitchId, System.Action<SystemStatusSnapshotBuilder, bool>>
        {
            { SoftSwitches.SoftSwitchId.Store80, (b,v) => b.State80Store = v },
            { SoftSwitches.SoftSwitchId.RamRd, (b,v) => b.StateRamRd = v },
            { SoftSwitches.SoftSwitchId.RamWrt, (b,v) => b.StateRamWrt = v },
            { SoftSwitches.SoftSwitchId.IntCxRom, (b,v) => b.StateIntCxRom = v },
            { SoftSwitches.SoftSwitchId.AltZp, (b,v) => b.StateAltZp = v },
            { SoftSwitches.SoftSwitchId.SlotC3Rom, (b,v) => b.StateSlotC3Rom = v },
            { SoftSwitches.SoftSwitchId.Vid80, (b,v) => b.StateShow80Col = v },
            { SoftSwitches.SoftSwitchId.AltChar, (b,v) => b.StateAltCharSet = v },
            { SoftSwitches.SoftSwitchId.Text, (b,v) => b.StateTextMode = v },
            { SoftSwitches.SoftSwitchId.Mixed, (b,v) => b.StateMixed = v },
            { SoftSwitches.SoftSwitchId.Page2, (b,v) => b.StatePage2 = v },
            { SoftSwitches.SoftSwitchId.HiRes, (b,v) => b.StateHiRes = v },
            { SoftSwitches.SoftSwitchId.An0, (b,v) => b.StateAnn0 = v },
            { SoftSwitches.SoftSwitchId.An1, (b,v) => b.StateAnn1 = v },
            { SoftSwitches.SoftSwitchId.An2, (b,v) => b.StateAnn2 = v },
            { SoftSwitches.SoftSwitchId.An3, (b,v) => b.StateAnn3 = v },
            { SoftSwitches.SoftSwitchId.Bank1, (b,v) => b.StateUseBank1 = v },
            { SoftSwitches.SoftSwitchId.HighRead, (b,v) => b.StateHighRead = v },
            { SoftSwitches.SoftSwitchId.HighWrite, (b,v) => b.StateHighWrite = v },
        };

    private void BuildStatusData()
    {
        var switches = (Bus as VA2MBus)?.Switches;
        var data = switches!.GetSwitchList();

        _sysStatusSink?.Mutate(b =>
        {
            foreach (var item in data)
            {
                if (_switchSetters.TryGetValue(item.id, out var setter))
                {
                    setter(b, item.value);
                }
            }

            var vb = Bus as VA2MBus;
            b.StatePb0 = vb!.GetPushButton(0);
            b.StatePb1 = vb!.GetPushButton(1);
            b.StatePb2 = vb!.GetPushButton(2);
        });
    }

    /*    /// <summary>
        /// Load a ROM image into RAM at the specified base address (for early testing).
        /// Clips to RAM bounds; partial copy if image overflows.
        /// </summary>
        public void LoadRom(ReadOnlySpan<byte> image, ushort baseAddress)
        {
            // Calculate how many bytes fit from baseAddress to end of RAM
            int available = RamSize - baseAddress;
            if (available <= 0 || image.IsEmpty) { return; }
            int toCopy = Math.Min(available, image.Length);

            // IMemoryModel has WriteBlock with params byte[] - allocate exact-sized array slice
            byte[] buffer = image[..toCopy].ToArray();
            ROM.WriteBlock(baseAddress, buffer);
        }*/

    public void Dispose()
    {
        _flashTimer?.Dispose();
        _flashTimer = null;
    }



}
