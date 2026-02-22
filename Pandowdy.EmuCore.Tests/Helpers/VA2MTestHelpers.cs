// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.Cpu;
using Pandowdy.EmuCore.Input;
using Pandowdy.EmuCore.IO;
using Pandowdy.EmuCore.Machine;
using Pandowdy.EmuCore.Memory;
using Pandowdy.EmuCore.Slots;
using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.Video;

using Pandowdy.EmuCore.DiskII;
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
        private ISystemStatusMutator? _systemStatusProvider;
        private IAppleIIBus? _bus;
        private AddressSpaceController? _memoryPool;
        private IFrameGenerator? _frameGenerator;
        private IKeyboardSetter? _keyboardSetter;
        private IGameControllerStatus? _gameController;
        private IDiskStatusProvider? _diskStatusProvider;
        private CpuClockingCounters? _clockCounters;
        private IMemoryInspector? _memoryInspector;
        private ISlots? _slots;

        public VA2MBuilder()
        {
            // Set defaults
            _emulatorState = new TestEmulatorState();
            _frameProvider = new TestFrameProvider();
            _gameController = new SimpleGameController(); // Create controller first
            _systemStatusProvider = new SystemStatusProvider(_gameController); // Pass controller to status provider
            _bus = new TestAppleIIBus();
            _keyboardSetter = new SingularKeyHandler(); // Default keyboard handler
            _diskStatusProvider = new DiskStatusProvider(); // Default disk status provider
            _clockCounters = new CpuClockingCounters(); // Default clock counters

            // Create I/O handler
            var softSwitches = new SoftSwitches(_systemStatusProvider as SystemStatusProvider 
                ?? new SystemStatusProvider(_gameController));
            var vblank = _clockCounters;
            var ioHandler = new SystemIoHandler(softSwitches, _keyboardSetter as IKeyboardReader 
                ?? new SingularKeyHandler(), _gameController, vblank);

            var testSlots = new TestSlots(_systemStatusProvider);
            _slots = testSlots;
            _memoryPool = new AddressSpaceController(
                new TestLanguageCard(), 
                new TestSystemRamSelector(),
                ioHandler,
                testSlots);
            _frameGenerator = new TestFrameGenerator();

            // Create test memory inspector
            _memoryInspector = new TestMemoryInspector();
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

        public VA2MBuilder WithSystemStatusProvider(ISystemStatusMutator provider)
        {
            _systemStatusProvider = provider;
            return this;
        }

        public VA2MBuilder WithBus(IAppleIIBus bus)
        {
            _bus = bus;
            return this;
        }

        public VA2MBuilder WithMemoryPool(AddressSpaceController memoryPool)
        {
            _memoryPool = memoryPool;
            return this;
        }

        public VA2MBuilder WithFrameGenerator(IFrameGenerator frameGenerator)
        {
            _frameGenerator = frameGenerator;
            return this;
        }

        public VA2MBuilder WithKeyboardSetter(IKeyboardSetter keyboardSetter)
        {
            _keyboardSetter = keyboardSetter;
            return this;
        }

        public VA2MBuilder WithGameController(IGameControllerStatus gameController)
        {
            _gameController = gameController;
            return this;
        }

        public VA2MBuilder WithDiskStatusProvider(IDiskStatusProvider diskStatusProvider)
        {
            _diskStatusProvider = diskStatusProvider;
            return this;
        }

        public VA2MBuilder WithClockCounters(CpuClockingCounters clockCounters)
        {
            _clockCounters = clockCounters;
            return this;
        }

        public VA2M Build()
        {
            // Create rendering service and snapshot pool for tests
            var snapshotPool = new VideoMemorySnapshotPool();
            var renderingService = new RenderingService(_frameGenerator!, snapshotPool);

            return new VA2M(
                _emulatorState!,
                _frameProvider!,
                _systemStatusProvider!,
                _bus!,
                _memoryPool!,
                _frameGenerator!,
                renderingService,
                snapshotPool,
                _keyboardSetter!,
                _gameController!,
                _diskStatusProvider!,
                _clockCounters!,
                _memoryInspector!,
                _slots!,
                new RestartCollection([])
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

    public BitmapDataArray? BorrowWritable() => _frame;

    public void CommitWritable(BitmapDataArray renderedBuffer)
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
    private readonly IPandowdyCpu _cpu;
    private readonly AddressSpaceController _memory;
    private ulong _clockCounter = 0;
    private byte _keyValue = 0;
    private readonly bool[] _pushButtons = new bool[3];
    private int _resetCount = 0;
    private int _userResetCount = 0;
    private readonly List<byte> _enqueuedKeyHistory = [];

    public TestAppleIIBus()
    {
        var gameController = new SimpleGameController();
        var statusProvider = new SystemStatusProvider(gameController);
        var softSwitches = new SoftSwitches(statusProvider);
        var keyboard = new SingularKeyHandler();
        var vblank = new CpuClockingCounters();
        var ioHandler = new SystemIoHandler(softSwitches, keyboard, gameController, vblank);

        _memory = new AddressSpaceController(
            new TestLanguageCard(), 
            new Test64KSystemRamSelector(),
            ioHandler,
            new TestSlots(statusProvider));

        // Create CPU using factory with DI-style pattern
        var cpuState = new CpuState();
        _cpu = CpuFactory.Create(CpuVariant.Wdc65C02, cpuState);

        // Reset CPU so it's at an instruction boundary (InstructionComplete = true)
        _cpu.Reset(this);
    }

    // IAppleIIBus implementation
    public IPandowdyMemory RAM => _memory;
    public IPandowdyCpu Cpu => _cpu;
    public ulong SystemClockCounter => _clockCounter;

    // VBlank event for testing
    public event Action? VBlankOccurred;

    public void EnqueueKey(byte key)
    {
        _keyValue = key;
        _enqueuedKeyHistory.Add(key);
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

        // Trigger VBlank event every ~12,480 cycles (60 Hz at 1.023 MHz)
        if (_clockCounter % 12_480 == 0)
        {
            VBlankOccurred?.Invoke();
        }
    }

    public void Reset()
    {
        _resetCount++;
        _clockCounter = 0;
    }

    public void UserReset()
    {
        _userResetCount++;
        _clockCounter = 0;
    }

    // Test helpers
    public byte GetKeyValue() => _keyValue;
    public byte GetLastEnqueuedKey() => _enqueuedKeyHistory.Count > 0 ? _enqueuedKeyHistory[^1] : (byte)0;
    public List<byte> GetEnqueuedKeyHistory() => [.. _enqueuedKeyHistory];
    public bool GetPushButton(int num) => num >= 0 && num < 3 && _pushButtons[num];
    public int ResetCount => _resetCount;
    public int UserResetCount => _userResetCount;

    public void Restart() => Reset();
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
        var gameController = new SimpleGameController();
        var statusProvider = new SystemStatusProvider(gameController);
        var softSwitches = new SoftSwitches(statusProvider);
        var keyboard = new SingularKeyHandler();
        var vblank = new CpuClockingCounters();
        var ioHandler = new SystemIoHandler(softSwitches, keyboard, gameController, vblank);
        
        return new RenderContext(
            new BitmapDataArray(),
            new AddressSpaceController(
                new TestLanguageCard(), 
                new TestSystemRamSelector(),
                ioHandler,
                new TestSlots(statusProvider)),
            statusProvider
        );
    }

    public void RenderFrame(RenderContext context)
    {
        _renderCallCount++;
        _lastContext = context;
        context.Invalidate();
    }
    
    public void RenderFrameFromSnapshot(VideoMemorySnapshot snapshot)
    {
        // Test implementation - just increment counter
        _renderCallCount++;
    }

    // Test helpers
    public int RenderCallCount => _renderCallCount;
    public RenderContext? LastContext => _lastContext;
}

/// <summary>
/// Test implementation of IMemoryInspector for unit testing.
/// </summary>
public class TestMemoryInspector : IMemoryInspector
{
    private readonly byte[] _mainRam = new byte[65536];
    private readonly byte[] _auxRam = new byte[65536];
    private readonly byte[] _systemRom = new byte[16384]; // 16KB ROM

    public byte ReadRawMain(int address) => _mainRam[address & 0xFFFF];
    public byte ReadRawAux(int address) => _auxRam[address & 0xFFFF];

    public byte ReadSystemRom(int address)
    {
        // $C000-$C0FF is I/O space, not readable
        if (address < 0xC100 || address > 0xFFFF)
        {
            return 0;
        }
        return _systemRom[(address - 0xC000) & 0x3FFF];
    }

    public byte ReadActiveHighMemory(int address)
    {
        // $C000-$C0FF is I/O space, not readable
        if (address < 0xC100 || address > 0xFFFF)
        {
            return 0;
        }
        // Test implementation returns system ROM for simplicity
        return ReadSystemRom(address);
    }

    public byte ReadSlotRom(int slot, int offset) => 0;

    public byte ReadSlotExtendedRom(int slot, int offset) => 0;

    public byte[] ReadMainBlock(int startAddress, int length)
    {
        var result = new byte[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = _mainRam[(startAddress + i) & 0xFFFF];
        }
        return result;
    }

    public byte[] ReadAuxBlock(int startAddress, int length)
    {
        var result = new byte[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = _auxRam[(startAddress + i) & 0xFFFF];
        }
        return result;
    }

    // Test helpers
    public void SetMainRam(int address, byte value) => _mainRam[address & 0xFFFF] = value;
    public void SetAuxRam(int address, byte value) => _auxRam[address & 0xFFFF] = value;
    public void SetSystemRom(int address, byte value)
    {
        if (address >= 0xC000 && address <= 0xFFFF)
        {
            _systemRom[(address - 0xC000) & 0x3FFF] = value;
        }
    }
}
