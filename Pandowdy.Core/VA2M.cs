using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Emulator;
using System.Reflection.Metadata.Ecma335;

namespace Pandowdy.Core;

public sealed class VA2M : IDisposable
{
    public const int RamSize = 64 * 1024;

    public IBus Bus { get; }
    public VA2MMemory RamModel { get; }
    public VA2MMemory AuxRamModel { get; }
    public IMappedMemory RamMapped => RamModel;
    public IMappedMemory AuxRamMapped => AuxRamModel;

    private readonly CPU _cpu;
    private readonly Stopwatch _throttleSw = Stopwatch.StartNew();
    private long _throttleCycles;
    public bool ThrottleEnabled { get; set; } = true;
    public double TargetHz { get; set; } = 1_023_000d;
    public ulong SystemClock => Bus.SystemClockCounter;

    // 16KB ROM space at $C000-$FFFF
    private VA2MMemory ROM = new (0x0000, 64 * 1024, VA2MMemory.MemAccessType.ReadWrite); 

    private readonly IEmulatorState? _stateSink; // optional DI-provided state publisher
    private readonly IFrameProvider? _frameSink; // optional DI-provided frame publisher

    public VA2M() : this(null, null) { }

    public VA2M(IEmulatorState? stateSink, IFrameProvider? frameSink)
    {
        _stateSink = stateSink;
        _frameSink = frameSink;
        TryLoadEmbeddedRom("Pandowdy.Core.Resources.a2e_enh_c-f.rom", 0xC000);
        var mem = new VA2MMemory(0,RamSize);
        RamModel = mem;
        var auxmem = new VA2MMemory(0, RamSize);
        AuxRamModel = auxmem;
        Bus = new VA2MBus(mem,auxmem,ROM);
        _cpu = new CPU();
        Bus.Connect(_cpu);
        if (_frameSink is not null && Bus is VA2MBus vb)
        {
            vb.VBlank += OnVBlank;
        }
    }

    public bool Use80Cols { get; set; } = false; // text mode flag mirrored from UI

    private void OnVBlank(object? sender, EventArgs e)
    {
        if (_frameSink is null) return;
        var buf = _frameSink.BorrowWritable();
        Array.Clear(buf, 0, buf.Length);
        for (int addr = 0x400; addr < 0x800; addr++)
        {
            int off = AddressToOffset(addr);
            if (off < 0) continue;
            int col = off % 40;
            int row = off / 40;
            byte ch = RamModel.Read((ushort)addr);
            var glyph = VideoFont.Glyph(ch);
            for (int r = 0; r < 8; r++)
            {
                int y = row * 8 + r;
                if (y >= _frameSink.Height) break;
                byte fontRow = (byte) (glyph[r]); 
                int baseX = col * 2;
                if (!Use80Cols)
                {
                    // 40-column mode: expand each bit (MSB first) into two horizontal bits forming 16-bit pattern
                    // Pattern: 0xXABCDEFG becomes 0x0AABBCCD, 0x0DEEFFGG (or 0x0AABBCCD0DEEFFGG as a word)
                    if (baseX + 1 >= _frameSink.Width) break;

                    byte hi = 0xff;
                    byte lo = 0xff;
                    
                    if ((fontRow & 0x01) == 0) { hi &= 0b11111100;                    }
                    if ((fontRow & 0x02) == 0) { hi &= 0b11110011;                    }
                    if ((fontRow & 0x04) == 0) { hi &= 0b11001111;                    }
                    if ((fontRow & 0x08) == 0) { hi &= 0b10111111; lo &= 0b11111110;  }
                    if ((fontRow & 0x10) == 0) {                   lo &= 0b11111001;  }
                    if ((fontRow & 0x20) == 0) {                   lo &= 0b11100111;  }
                    if ((fontRow & 0x40) == 0) {                   lo &= 0b10011111;  }


                    buf[y * _frameSink.Width + baseX] = hi;
                    buf[y * _frameSink.Width + baseX + 1] = lo;
                }
                else
                {
                    // 80-column mode: single 8-bit glyph row per character (use first slot)
                    if (baseX >= _frameSink.Width) break;
                    buf[y * _frameSink.Width + baseX] = fontRow;
                }
            }
        }
        _frameSink.CommitWritable();
    }

    private static int AddressToOffset(int address)
    {
        if (address < 0x400 || address >= 0x800) return -1;
        address -= 0x400;
        var macroline_x = address % 128;
        var macroline_y = address / 128;
        // Return -1 for screen holes.
        if (macroline_x >= 120)
            return -1; 
        int section = macroline_x / 40;
        int row = macroline_y + 8 * section;
        return macroline_x % 40 + 40 * row;
    }

    private void TryLoadEmbeddedRom(string resourceName, ushort baseAddress)
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
                LoadRom(ms.ToArray(), baseAddress);
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
        if (leadSec <= 0)
        { return; }

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
                    if (!await timer.WaitForNextTickAsync(ct).ConfigureAwait(false)) break;
                }
                catch (OperationCanceledException) { break; }
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
                for (int i = 0; i < FastBatch; i++)
                {
                    Bus.Clock();
                    _throttleCycles++;
                    if (ct.IsCancellationRequested) break;
                }
                PublishState();
                await Task.Yield();
            }
        }
    }

    private void PublishState()
    {
        if (_stateSink is null) return;
        int lineNum = (int)(Bus.CpuRead(0x75) + (Bus.CpuRead(0x76) << 8));
        int? basicLine = lineNum < 0xFA00 ? lineNum : null; // treat IMMEDIATE as null
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
    public void InjectKey(byte ascii) => Poke(0xC000, (byte) (ascii | 0x80));


    /// <summary>
    /// Load a ROM image into RAM at the specified base address (for early testing).
    /// Clips to RAM bounds; partial copy if image overflows.
    /// </summary>
    public void LoadRom(ReadOnlySpan<byte> image, ushort baseAddress)
    {
        // Calculate how many bytes fit from baseAddress to end of RAM
        int available = RamSize - baseAddress;
        if (available <= 0 || image.IsEmpty)
        { return; }
        int toCopy = Math.Min(available, image.Length);

        // IMemoryModel has WriteBlock with params byte[] - allocate exact-sized array slice
        byte[] buffer = image[..toCopy].ToArray();
        ROM.WriteBlock(baseAddress, buffer);

        /*
        const byte NormalCharOffset = 0x80;
        ROM.Write(0xff0a, (byte) 'P' + NormalCharOffset, true);
        ROM.Write(0xff0b, (byte) 'a' + NormalCharOffset, true);
        ROM.Write(0xff0c, (byte) 'n' + NormalCharOffset, true);
        ROM.Write(0xff0d, (byte) 'd' + NormalCharOffset, true);
        ROM.Write(0xff0e, (byte) 'o' + NormalCharOffset, true);
        ROM.Write(0xff0f, (byte) 'w' + NormalCharOffset, true);
        ROM.Write(0xff10, (byte) 'd' + NormalCharOffset, true);
        ROM.Write(0xff11, (byte) 'y' + NormalCharOffset, true);
        ROM.Write(0xff12, (byte) '!' + NormalCharOffset, true);
        */
    }

    public void Dispose()
    {
    }
}
