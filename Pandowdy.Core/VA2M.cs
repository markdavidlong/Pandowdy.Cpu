using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Emulator;

namespace Pandowdy.Core;

public sealed class VA2M : IDisposable
{
    public const int RamSize = 64 * 1024;

    public IBus Bus { get; }
    public VA2MMemory RamModel { get; }
    public IMappedMemory RamMapped => RamModel;

    private readonly CPU _cpu;
    private readonly Stopwatch _throttleSw = Stopwatch.StartNew();
    private long _throttleCycles;
    public bool ThrottleEnabled { get; set; } = true;
    public double TargetHz { get; set; } = 1_023_000d;
    public ulong SystemClock => Bus.SystemClockCounter;

    public VA2M()
    {
        var mem = new VA2MMemory(0,RamSize);
        RamModel = mem;
        // IO = new VA2MIO();
        // ROM = new VA2MMemory(16 * 1024, Va2MMemory::ReadOnly); // 16KB ROM space at $C000-$FFFF
        Bus = new VA2MBus(mem/*,IO*/);
        _cpu = new CPU();
        Bus.Connect(_cpu);

        TryLoadEmbeddedRomToRam("Pandowdy.Core.Resources.a2e_enh_c-f.rom", 0xC000);
        for (var addr = 0xc400; addr < 0xc800; addr++)
        {
            Poke((ushort) addr, 0x00); // BRK
        }
    }

    private void TryLoadEmbeddedRomToRam(string resourceName, ushort baseAddress)
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using Stream? s = asm.GetManifestResourceStream(resourceName);
            if (s == null)
            { return; }
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            LoadRom(ms.ToArray(), baseAddress);
        }
        catch { }
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
    /// </summary>
    /// <param name="ct">Cancellation token to stop the runner.</param>
    /// <param name="ticksPerSecond">Time slice frequency. Use1000 for1ms slices or60 for video-frame pacing.</param>
    public async Task RunAsync(CancellationToken ct, double ticksPerSecond = 1000d)
    {
        if (ticksPerSecond <= 0)
        { throw new ArgumentOutOfRangeException(nameof(ticksPerSecond)); }
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1.0 / ticksPerSecond));
        double cyclesPerTick = TargetHz / ticksPerSecond;
        double carry = 0.0;
        // The async runner does its own pacing; disable per-cycle throttle effects
        bool prevThrottle = ThrottleEnabled;
        ThrottleEnabled = false;
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                double want = cyclesPerTick + carry;
                int cycles = (int) want;
                carry = want - cycles;
                for (int i = 0; i < cycles; i++)
                {
                    Bus.Clock();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // normal stop
        }
        finally
        {
            ThrottleEnabled = prevThrottle;
        }
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
    /// Typical Apple II behavior: bit7 set indicates key ready; low7 bits hold ASCII.
    /// Clearing strobe: read or write to $C010.
    /// </summary>
    public void InjectKey(byte asciiWithReadyBit) => Poke(0xC000, asciiWithReadyBit);

    /// <summary>
    /// Clear the keyboard strobe at $C010.
    /// </summary>
    public void ClearKeyStrobe() => Poke(0xC010, 0);

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
        byte[] buffer = image.Slice(0, toCopy).ToArray();
        RamModel.WriteBlock(baseAddress, buffer);
    }

    public void Dispose()
    {
    }
}
