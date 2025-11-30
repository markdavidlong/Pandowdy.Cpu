namespace Pandowdy.Core;

// Placeholder service interfaces & simple stub implementations.
// Incremental behavior will be added later without breaking existing UI.

public interface IFrameProvider {
    int Width { get; } // 80 row bytes per scanline
    int Height { get; } // 192 scanlines (24 rows * 8)
    bool IsGraphics { get; set; } // true if in graphics mode
    bool IsMixed { get; set;  } // true if in mixed text/graphics mode
    event EventHandler? FrameAvailable; // raised after new frame committed
    BitmapDataArray GetFrame(); // returns current front buffer (length = Width*Height)
    BitmapDataArray BorrowWritable(); // returns back buffer for composition
    void CommitWritable(); // swap buffers & raise event
}

public interface ISystemStatusProvider
{
    bool State80Store { get; }
    bool StateRamRd { get; }
    bool StateRamWrt { get; }
    bool StateIntCxRom { get; }
    bool StateAltZp { get; }
    bool StateSlotC3Rom { get; }
    bool StatePb0 { get; }
    bool StatePb1 { get; }
    bool StatePb2 { get; }
    bool StateAnn0 { get; }
    bool StateAnn1 { get; }
    bool StateAnn2 { get; }
    bool StateAnn3_DGR { get; }
    bool StatePage2 { get; }
    bool StateHiRes { get; }
    bool StateMixed { get; }
    bool StateTextMode { get; }
    bool StateShow80Col { get; }
    bool StateAltCharSet { get; }
    bool StateFlashOn { get; }
    byte BSRStatusByte { get; }
    byte StateBsrWriteCount { get; }

    SystemStatusSnapshot Current { get; }
    event EventHandler<SystemStatusSnapshot>? Changed; // event-style subscription
    IObservable<SystemStatusSnapshot> Stream { get; } // reactive subscription

    // Mutation hook used by core (bus) to update status snapshot
    void Mutate(Action<SystemStatusSnapshotBuilder> mutator);
}

public record SystemStatusSnapshot(
    bool State80Store,
    bool StateRamRd,
    bool StateRamWrt,
    bool StateIntCxRom, 
    bool StateAltZp,
    bool StateSlotC3Rom,
    bool StatePb0,
    bool StatePb1,
    bool StatePb2,
    bool StateAnn0,
    bool StateAnn1,
    bool StateAnn2,
    bool StateAnn3_DGR,
    bool StatePage2,
    bool StateHiRes,
    bool StateMixed,
    bool StateTextMode,
    bool StateShow80Col,
    bool StateAltCharSet,
    bool StateFlashOn,
    byte BSRStatusByte,
    byte StateBSRWriteCount
    );

public sealed class SystemStatusProvider : ISystemStatusProvider
{
    private SystemStatusSnapshot _current = new(
        State80Store: false,
        StateRamRd: false,
        StateRamWrt: false,
        StateIntCxRom: true,
        StateAltZp: false,
        StateSlotC3Rom: false,
        StatePb0: false,
        StatePb1: false,
        StatePb2: false,
        StateAnn0: false,
        StateAnn1: false,
        StateAnn2: false,
        StateAnn3_DGR: false,
        StatePage2: false,
        StateHiRes: false,
        StateMixed: false,
        StateTextMode: true,
        StateShow80Col: false,
        StateAltCharSet: false,
        StateFlashOn: false,
        BSRStatusByte: 0,
        StateBSRWriteCount: 0);

    private readonly System.Reactive.Subjects.BehaviorSubject<SystemStatusSnapshot> _subject;

    public event EventHandler<SystemStatusSnapshot>? Changed;

    public SystemStatusProvider() { _subject = new System.Reactive.Subjects.BehaviorSubject<SystemStatusSnapshot>(_current); }

    public bool State80Store => _current.State80Store;
    public bool StateRamRd => _current.StateRamRd;
    public bool StateRamWrt => _current.StateRamWrt;
    public bool StateIntCxRom => _current.StateIntCxRom;
    public bool StateAltZp => _current.StateAltZp;
    public bool StateSlotC3Rom => _current.StateSlotC3Rom;
    public bool StatePb0 => _current.StatePb0;
    public bool StatePb1 => _current.StatePb1;
    public bool StatePb2 => _current.StatePb2;
    public bool StateAnn0 => _current.StateAnn0;
    public bool StateAnn1 => _current.StateAnn1;
    public bool StateAnn2 => _current.StateAnn2;
    public bool StateAnn3_DGR => _current.StateAnn3_DGR;
    public bool StatePage2 => _current.StatePage2;
    public bool StateHiRes => _current.StateHiRes;
    public bool StateMixed => _current.StateMixed;
    public bool StateTextMode => _current.StateTextMode;
    public bool StateShow80Col => _current.StateShow80Col;
    public bool StateAltCharSet => _current.StateAltCharSet;
    public bool StateFlashOn => !_current.StateFlashOn;
    public byte BSRStatusByte => _current.BSRStatusByte;
    public byte StateBsrWriteCount => _current.StateBSRWriteCount;


    public SystemStatusSnapshot Current => _current;
    public IObservable<SystemStatusSnapshot> Stream => _subject;

    public void Mutate(Action<SystemStatusSnapshotBuilder> mutator)
    {
        var b = new SystemStatusSnapshotBuilder(_current);
        mutator(b);
        _current = b.Build();
        _subject.OnNext(_current);
        Changed?.Invoke(this, _current);
    }
}

public sealed class SystemStatusSnapshotBuilder(SystemStatusSnapshot s)
{
    public bool State80Store = s.State80Store;
    public bool StateRamRd = s.StateRamRd;
    public bool StateRamWrt = s.StateRamWrt;
    public bool StateIntCxRom = s.StateIntCxRom;
    public bool StateAltZp = s.StateAltZp;
    public bool StateSlotC3Rom = s.StateSlotC3Rom;
    public bool StatePb0 = s.StatePb0;
    public bool StatePb1 = s.StatePb1;
    public bool StatePb2 = s.StatePb2;
    public bool StateAnn0 = s.StateAnn0;
    public bool StateAnn1 = s.StateAnn1;
    public bool StateAnn2 = s.StateAnn2;
    public bool StateAnn3 = s.StateAnn3_DGR;
    public bool StatePage2 = s.StatePage2;
    public bool StateHiRes = s.StateHiRes;
    public bool StateMixed = s.StateMixed;
    public bool StateTextMode = s.StateTextMode;
    public bool StateShow80Col = s.StateShow80Col;
    public bool StateAltCharSet = s.StateAltCharSet;
    public bool StateFlashOn = s.StateFlashOn;
    byte BSRStatusByte = s.BSRStatusByte;
    byte StateBSRWriteCount = s.StateBSRWriteCount;

    public SystemStatusSnapshot Build() => new(
        State80Store, StateRamRd, StateRamWrt, StateIntCxRom, StateAltZp, StateSlotC3Rom,
        StatePb0, StatePb1, StatePb2, StateAnn0, StateAnn1, StateAnn2, StateAnn3,
        StatePage2, StateHiRes, StateMixed, StateTextMode, StateShow80Col, StateAltCharSet, StateFlashOn, BSRStatusByte, StateBSRWriteCount);
}

//public interface IErrorProvider {
//    IObservable<LogEvent> Events { get; }
//    void Publish(LogEvent evt);
//}

public interface IEmulatorState {
    IObservable<StateSnapshot> Stream { get; }
    StateSnapshot GetCurrent();
    void Update(StateSnapshot snapshot);
    void RequestPause();
    void RequestContinue();
    void RequestStep();
}

//public interface IDisassemblyProvider {
//    IObservable<DisassemblyUpdate> Updates { get; }
//    Task<Line[]> QueryRange(AddressRange range);
//    void Invalidate(AddressRange range);
//    void SetHighlight(ushort pc);
//}

public record StateSnapshot(ushort PC, byte SP, ulong Cycles, int? LineNumber, bool IsRunning, bool IsPaused);
//public record LogEvent(DateTime Timestamp, string Severity, string Message, ushort? PC = null);
//public record DisassemblyUpdate(AddressRange Range, IReadOnlyList<Line> Lines);
//public record Line(ushort Address, string BytesHex, string Mnemonic, string Comment);
//public record AddressRange(ushort Start, ushort End);

public sealed class FrameProvider : IFrameProvider {
    private const int W = 80;
    private const int H = 192;
    private BitmapDataArray _front = new();
    private BitmapDataArray _back = new ();
    public int Width => W;
    public int Height => H;
    public event EventHandler? FrameAvailable;
    public bool IsGraphics { get;  set; } = false;
    public bool IsMixed { get; set; } = false;
    public BitmapDataArray GetFrame() => _front;
    public BitmapDataArray BorrowWritable() => _back;
    public void CommitWritable() {
        (_back, _front) = (_front, _back);
        FrameAvailable?.Invoke(this, EventArgs.Empty);
    }
}

//public sealed class ErrorProvider : IErrorProvider {
//    private readonly System.Reactive.Subjects.Subject<LogEvent> _subject = new();
//    public IObservable<LogEvent> Events => _subject;
//    public void Publish(LogEvent evt) => _subject.OnNext(evt);
//}

public sealed class EmulatorStateProvider : IEmulatorState {
    private readonly System.Reactive.Subjects.BehaviorSubject<StateSnapshot> _subject = new(new StateSnapshot(0,0,0,null,false,false));
    public IObservable<StateSnapshot> Stream => _subject;
    public StateSnapshot GetCurrent() => _subject.Value;
    public void Update(StateSnapshot snapshot) => _subject.OnNext(snapshot);
    public void RequestPause() { /* placeholder */ }
    public void RequestContinue() { /* placeholder */ }
    public void RequestStep() { /* placeholder */ }
}

//public sealed class DisassemblyProvider : IDisassemblyProvider {
//    private readonly System.Reactive.Subjects.Subject<DisassemblyUpdate> _updates = new();
//    public IObservable<DisassemblyUpdate> Updates => _updates;
//    public Task<Line[]> QueryRange(AddressRange range) => Task.FromResult(Array.Empty<Line>());
//    public void Invalidate(AddressRange range) { }
//    public void SetHighlight(ushort pc) { }
//}
