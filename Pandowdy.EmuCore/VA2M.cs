using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using Emulator;
using System.Collections.Concurrent;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Services;

namespace Pandowdy.EmuCore;

public partial class VA2M : IDisposable
{
    public MemoryPool _memoryPool { get; }//private set; } = new MemoryPool();

    public IAppleIIBus _bus { get; }

 //   private readonly ICpu _cpu;
    private readonly Stopwatch _throttleSw = Stopwatch.StartNew();
    private long _throttleCycles;
    public bool ThrottleEnabled { get; set; } = true;
    public double TargetHz { get; set; } = 1_023_000d;
    public ulong SystemClock => _bus.SystemClockCounter;


    private readonly IEmulatorState _stateSink; 
    private readonly IFrameProvider _frameSink; 
    private readonly ISystemStatusProvider _sysStatusSink; 

    // Flash timer to toggle StateFlashOn at ~2.1 Hz
    private Timer? _flashTimer;
    private static readonly TimeSpan FlashPeriod = TimeSpan.FromMilliseconds(1000/2.1);
    private int _pendingFlashToggle; // 0/1 flag set by timer, consumed on VBlank

    public VA2M(IEmulatorState stateSink, IFrameProvider frameSink, ISystemStatusProvider statusProvider, IAppleIIBus bus, MemoryPool memoryPool )
    {
        ArgumentNullException.ThrowIfNull(stateSink);
        ArgumentNullException.ThrowIfNull(frameSink);
        ArgumentNullException.ThrowIfNull(statusProvider);
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(memoryPool);



        _stateSink = stateSink;
        _frameSink = frameSink;
        _sysStatusSink = statusProvider;
        _bus = bus;
        _memoryPool = memoryPool;
        TryLoadEmbeddedRom("Pandowdy.EmuCore.Resources.a2e_enh_c-f.rom");
        
   //     _cpu = new CPUAdapter(new CPU());
    //   Bus = new VA2MBus(MemoryPool, _sysStatusSink as ISoftSwitchResponder, _cpu);
        //Bus.Connect(_cpu);
        if (_bus is VA2MBus vb)
        {
            vb.VBlank += OnVBlank;
        }
        // Start flash timer if status provider available
        if (_flashTimer == null)
        {
            _flashTimer = new Timer(_ =>
            {
                try
                {
                    Interlocked.Exchange(ref _pendingFlashToggle, 1);
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

    private void OnVBlank(object? sender, EventArgs e)
    {
        // Apply pending flash toggle at frame boundary for consistent rendering
        if (System.Threading.Interlocked.Exchange(ref _pendingFlashToggle, 0) != 0)
        {
            _sysStatusSink.Mutate(s => s.StateFlashOn = !s.StateFlashOn);
        }
        var buf = _frameSink.BorrowWritable();
        buf.Clear();

        RenderScreen(buf);

        _frameSink.IsGraphics = !_sysStatusSink.StateTextMode;
        _frameSink.IsMixed = _sysStatusSink.StateMixed;
        _frameSink.CommitWritable();
    }


    private void TryLoadEmbeddedRom(string resourceName)
    {
        var asm = Assembly.GetExecutingAssembly();
        using Stream? s = asm.GetManifestResourceStream(resourceName);
        if (s != null)
        {
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            _memoryPool.InstallApple2ROM(ms.ToArray());
        }
    }

    /// <summary>
    /// Advance one system clock (one CPU/bus cycle). If throttling is enabled,
    /// the call will delay to keep approx TargetHz. Suitable for simple loops.
    /// </summary>
    public void Clock()
    {
        // Execute commands enqueued from other threads
        ProcessPending();
        _bus.Clock();
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
            _bus.Reset();
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
            (_bus as VA2MBus)!.UserReset();
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
                    _bus.Clock();
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
                    _bus.Clock();
                    _throttleCycles++;
                    if (ct.IsCancellationRequested)
                    {
                        break;
                    }
                }
                PublishState();
                try
                {
                    await Task.Delay(0, ct);
                }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private void PublishState()
    {
        int lineNum = (int)(_bus.CpuRead(0x75) + (_bus.CpuRead(0x76) << 8));
        int? basicLine = lineNum < 0xFA00 ? lineNum : null;
        var snapshot = new StateSnapshot((ushort) _bus.Cpu.PC, (byte)_bus.Cpu.SP, _bus.SystemClockCounter, basicLine, true, false);
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
        Enqueue(() => _bus.SetKeyValue(val));
    }

    public void SetPushButton(byte num, bool pressed)
    {
        Enqueue(() => _bus.SetPushButton(num, pressed));
    }

    public void GenerateStatusData()
    {
        Enqueue(() => BuildStatusData());
    }

    private static readonly ImmutableDictionary<SoftSwitches.SoftSwitchId, System.Action<SystemStatusSnapshotBuilder, bool>> _switchSetters
        = new Dictionary<SoftSwitches.SoftSwitchId, System.Action<SystemStatusSnapshotBuilder, bool>>
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
        }.ToImmutableDictionary();

    private void BuildStatusData()
    {
        var switches = (_bus as VA2MBus)?.Switches;
        var data = switches!.GetSwitchList();

        _sysStatusSink.Mutate(b =>
        {
            foreach (var (id, value, count) in data)
            {
                if (_switchSetters.TryGetValue(id, out var setter))
                {
                    setter(b, value);
                }
            }

            var vb = _bus as VA2MBus;
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
        // Dispose flash timer
        _flashTimer?.Dispose();
        _flashTimer = null;
        
        // Clear pending queue
        while (_pending.TryDequeue(out _)) { }
        
        // Dispose bus (which handles VBlank event cleanup)
        if (_bus is IDisposable disposableBus)
        {
            disposableBus.Dispose();
        }
        
        // Dispose memory pool
      //  MemoryPool?.Dispose();
        
        // Note: _cpu doesn't implement IDisposable in legacy 6502.NET library
    }



}
