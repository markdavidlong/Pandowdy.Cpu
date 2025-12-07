using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Emulator;
using System.Reflection.Metadata.Ecma335;
using System.Collections.Concurrent;

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

    public VA2M() : this(null, null, null) { }

    public VA2M(IEmulatorState? stateSink, IFrameProvider? frameSink, ISystemStatusProvider? statusProvider = null)
    {
        _stateSink = stateSink;
        _frameSink = frameSink;
        _sysStatusSink = statusProvider;
        TryLoadEmbeddedRom("Pandowdy.Core.Resources.a2e_enh_c-f.rom");
        //var mem = new VA2MMemory(0,RamSize);
        
        Bus = new VA2MBus(MemoryPool/*, statusProvider*/);
        _cpu = new CPU();
        Bus.Connect(_cpu);
        if (_frameSink is not null && Bus is VA2MBus vb)
        {
            vb.VBlank += OnVBlank;
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
            try { act(); } catch { }
        }
    }

    private void OnVBlank(object? sender, EventArgs e)
    {
        if (_frameSink is null) { return; }
        var buf = _frameSink.BorrowWritable();
        buf.Clear();
        //Array.Clear(buf, 0, buf.Length);
        for (int addr = 0x400; addr < 0x800; addr++)
        {
            var col_row = AddressToOffset(addr);
            if (col_row == null)
            {
                continue;
            }
            int col = col_row.Value.Item1;
            int row = col_row.Value.Item2;

            if (_sysStatusSink!.StateShow80Col)
            // if 80 cols
            {
                byte ch1 = MemoryPool.ReadRawMain((ushort) addr);
                var glyph1 = VideoFont.Glyph(ch1); // returns span of 8 rows

                byte ch2 = MemoryPool.ReadRawAux((ushort) addr);
                var glyph2 = VideoFont.Glyph(ch2); // returns span of 8 rows

                for (int r = 0; r < 8; r++)  // 8 rows per glyph
                {
                    int y = row * 8 + r;
                    if (y >= _frameSink.Height)
                    { break; }
                    byte fontRow1 = (byte) ~glyph1[r]; // invert bits (was glyph ^ 0xff intent)
                                                       //  fontRow = (byte)((y / 8) % 16 * 0x11);
                    byte fontRow2 = (byte) ~glyph2[r]; 
                                                       

                    int baseX = col * 2;
                    {
                        if (baseX >= _frameSink.Width)
                        { break; }
                        buf.Insert7BitLsbAt(col * 2 * 7, y, fontRow1, false);
                        buf.Insert7BitLsbAt(col * 2 * 7 + 7, y, fontRow2, false);
                    }
                }
            }
            else // 40 columns
            {
                byte ch = MemoryPool.Read((ushort) addr);
                var glyph = VideoFont.Glyph(ch); // returns span of 8 rows

                for (int r = 0; r < 8; r++)  // 8 rows per glyph
                {
                    int y = row * 8 + r;
                    if (y >= _frameSink.Height)
                    { break; }
                    byte fontRow = (byte) ~glyph[r]; // invert bits (was glyph ^ 0xff intent)
                                                     //  fontRow = (byte)((y / 8) % 16 * 0x11);

                    buf.Insert7BitLsbAt(col * 2 * 7, y, fontRow, true);
                }
            }
        }
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
        Bus.Reset();
        _throttleCycles = 0;
        _throttleSw.Restart();
    }

    /// <summary>
    /// Run the emulator asynchronously with batched cycles and time slices.
    /// Batches cycles per tick (e.g.,1 ms or60 Hz) to reduce overhead of per-cycle waits.
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
    /// Write a byte to the bus at the specified address (e.g., $C000 keyboard latch, $C010 strobe clear).
    /// </summary>
    public void Poke(ushort address, byte value) => Bus.CpuWrite(address, value);

    /// <summary>
    /// Read a byte from the bus at the specified address.
    /// </summary>
    public byte Peek(ushort address) => Bus.CpuRead(address);

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
    }

}
