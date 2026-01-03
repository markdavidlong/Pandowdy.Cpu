using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Services;
using Emulator;

namespace Pandowdy.EmuCore.Tests.Helpers;

/// <summary>
/// Test helpers and builders for creating VA2M test instances with mock dependencies.
/// </summary>
public static class VA2MTestHelpers
{
    /// <summary>
    /// Builder pattern for creating VA2M instances with configurable mock dependencies.
    /// </summary>
    public class VA2MBuilder
    {
        private IEmulatorState? _emulatorState;
        private IFrameProvider? _frameProvider;
        private ISystemStatusProvider? _systemStatusProvider;
        private IAppleIIBus? _bus;
        private MemoryPool? _memoryPool;
        private IFrameGenerator? _frameGenerator;

        public VA2MBuilder()
        {
            // Set defaults
            _emulatorState = new TestEmulatorState();
            _frameProvider = new TestFrameProvider();
            _systemStatusProvider = new SystemStatusProvider();
            _bus = new TestAppleIIBus();
            _memoryPool = new MemoryPool();
            _frameGenerator = new TestFrameGenerator();
        }

        public VA2MBuilder WithEmulatorState(IEmulatorState state)
        {
            _emulatorState = state;
            return this;
        }

        public VA2MBuilder WithFrameProvider(IFrameProvider provider)
        {
            _frameProvider = provider;
            return this;
        }

        public VA2MBuilder WithSystemStatusProvider(ISystemStatusProvider provider)
        {
            _systemStatusProvider = provider;
            return this;
        }

        public VA2MBuilder WithBus(IAppleIIBus bus)
        {
            _bus = bus;
            return this;
        }

        public VA2MBuilder WithMemoryPool(MemoryPool memoryPool)
        {
            _memoryPool = memoryPool;
            return this;
        }

        public VA2MBuilder WithFrameGenerator(IFrameGenerator frameGenerator)
        {
            _frameGenerator = frameGenerator;
            return this;
        }

        public VA2M Build()
        {
            return new VA2M(
                _emulatorState!,
                _frameProvider!,
                _systemStatusProvider!,
                _bus!,
                _memoryPool!,
                _frameGenerator!
            );
        }
    }

    /// <summary>
    /// Factory method to create a VA2MBuilder with default test dependencies.
    /// </summary>
    public static VA2MBuilder CreateBuilder() => new();
}

/// <summary>
/// Test implementation of IEmulatorState that tracks all state updates.
/// </summary>
public class TestEmulatorState : IEmulatorState
{
    private StateSnapshot _current = new(0, 0, 0, null, false, false);
    private readonly List<StateSnapshot> _history = [];

    public IObservable<StateSnapshot> Stream => throw new NotImplementedException("Use GetCurrent() for testing");

    public StateSnapshot GetCurrent() => _current;

    public void Update(StateSnapshot snapshot)
    {
        _current = snapshot;
        _history.Add(snapshot);
    }

    public void RequestPause() { }
    public void RequestContinue() { }
    public void RequestStep() { }

    // Test helpers
    public List<StateSnapshot> GetHistory() => _history;
    public int UpdateCount => _history.Count;
}

/// <summary>
/// Test implementation of IFrameProvider that tracks frame operations.
/// </summary>
public class TestFrameProvider : IFrameProvider
{
    private BitmapDataArray _frame = new();
    private int _commitCount = 0;

    public int Width => 560;
    public int Height => 192;
    public bool IsGraphics { get; set; }
    public bool IsMixed { get; set; }

    public event EventHandler? FrameAvailable;

    public BitmapDataArray GetFrame() => _frame;

    public BitmapDataArray BorrowWritable() => _frame;

    public void CommitWritable()
    {
        _commitCount++;
        FrameAvailable?.Invoke(this, EventArgs.Empty);
    }

    // Test helpers
    public int CommitCount => _commitCount;
}

/// <summary>
/// Test implementation of IAppleIIBus that simulates bus operations.
/// </summary>
public class TestAppleIIBus : IAppleIIBus
{
    private readonly TestCpu _cpu = new();
    private readonly MemoryPool _memory = new();
    private ulong _clockCounter = 0;
    private byte _keyValue = 0;
    private readonly bool[] _pushButtons = new bool[3];
    private int _resetCount = 0;

    // IAppleIIBus implementation
    public IMemory RAM => _memory;
    public ICpu Cpu => _cpu;
    public ulong SystemClockCounter => _clockCounter;

    public void Connect(CPU cpu)
    {
        // Not needed for test
    }

    public void SetKeyValue(byte key)
    {
        _keyValue = key;
    }

    public void SetPushButton(int num, bool enabled)
    {
        if (num >= 0 && num < 3)
        {
            _pushButtons[num] = enabled;
        }
    }

    public byte CpuRead(ushort address, bool readOnly = false)
    {
        return _memory.Read(address);
    }

    public void CpuWrite(ushort address, byte data)
    {
        _memory.Write(address, data);
    }

    public void Clock()
    {
        _clockCounter++;
    }

    public void Reset()
    {
        _resetCount++;
        _clockCounter = 0;
    }

    // Test helpers
    public byte GetKeyValue() => _keyValue;
    public bool GetPushButton(int num) => num >= 0 && num < 3 && _pushButtons[num];
    public int ResetCount => _resetCount;
}

/// <summary>
/// Test implementation of ICpu that provides minimal CPU simulation.
/// </summary>
public class TestCpu : ICpu
{
    public ushort PC { get; set; } = 0;
    public byte A { get; set; } = 0;
    public byte X { get; set; } = 0;
    public byte Y { get; set; } = 0;
    public byte SP { get; set; } = 0xFF;
    public ProcessorStatus Status { get; set; } = new ProcessorStatus();

    public void Clock(IAppleIIBus bus) { }
    
    public void Reset(IAppleIIBus bus) 
    { 
        PC = 0;
        A = 0;
        X = 0;
        Y = 0;
        SP = 0xFF;
        Status = new ProcessorStatus();
    }
    
    public void InterruptRequest(IAppleIIBus bus) { }
    public void NonMaskableInterrupt(IAppleIIBus bus) { }
    public bool IsInstructionComplete() => true;
    public byte Read(ushort address) => 0;
    public void Write(ushort address, byte data) { }

    public override string ToString()
    {
        return $"PC:{PC:X4} A:{A:X2} X:{X:X2} Y:{Y:X2} SP:{SP:X2}";
    }
}

/// <summary>
/// Test implementation of IFrameGenerator that tracks render calls.
/// </summary>
public class TestFrameGenerator : IFrameGenerator
{
    private int _renderCallCount = 0;
    private RenderContext? _lastContext;

    public RenderContext AllocateRenderContext()
    {
        return new RenderContext(
            new BitmapDataArray(),
            new MemoryPool(),
            new SystemStatusProvider()
        );
    }

    public void RenderFrame(RenderContext context)
    {
        _renderCallCount++;
        _lastContext = context;
        context.Invalidate();
    }

    // Test helpers
    public int RenderCallCount => _renderCallCount;
    public RenderContext? LastContext => _lastContext;
}
