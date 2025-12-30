using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.Services;


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
    bool StatePrewrite,
    bool StateUseBank1,
    bool StateHighRead,
    bool StateHighWrite
    );

public sealed class SystemStatusProvider : ISystemStatusProvider, ISoftSwitchResponder
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
        StatePrewrite: false,
        StateUseBank1: false,
        StateHighRead: false,
        StateHighWrite: false);

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
    public bool StateFlashOn => _current.StateFlashOn;
    public bool StatePreWrite => _current.StatePrewrite;
    public bool StateUseBank1 => _current.StateUseBank1;
    public bool StateHighRead => _current.StateHighRead;
    public bool StateHighWrite => _current.StateHighWrite;

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

    // ISoftSwitchResponder implementation - update snapshot via Mutate
    public void Set80Store(bool store80) => Mutate(b => b.State80Store = store80);
    public void SetRamRd(bool ramRd) => Mutate(b => b.StateRamRd = ramRd);
    public void SetRamWrt(bool ramWrt) => Mutate(b => b.StateRamWrt = ramWrt);
    public void SetIntCxRom(bool intCxRom) => Mutate(b => b.StateIntCxRom = intCxRom);
    public void SetAltZp(bool altZp) => Mutate(b => b.StateAltZp = altZp);
    public void SetSlotC3Rom(bool slotC3Rom) => Mutate(b => b.StateSlotC3Rom = slotC3Rom);
    public void Set80Vid(bool vid) => Mutate(b => b.StateShow80Col = vid);
    public void SetAltChar(bool altChar) => Mutate(b => b.StateAltCharSet = altChar);
    public void SetText(bool text) => Mutate(b => b.StateTextMode = text);
    public void SetMixed(bool mixed) => Mutate(b => b.StateMixed = mixed);
    public void SetPage2(bool page2) => Mutate(b => b.StatePage2 = page2);
    public void SetHiRes(bool hires) => Mutate(b => b.StateHiRes = hires);
    public void SetAn0(bool an0) => Mutate(b => b.StateAnn0 = an0);
    public void SetAn1(bool an1) => Mutate(b => b.StateAnn1 = an1);
    public void SetAn2(bool an2) => Mutate(b => b.StateAnn2 = an2);
    public void SetAn3(bool an3) => Mutate(b => b.StateAnn3 = an3);
    public void SetBank1(bool enabled) => Mutate(b => b.StateUseBank1 = enabled);
    public void SetHighWrite(bool enabled) => Mutate(b => b.StateHighWrite = enabled);
    public void SetHighRead(bool enabled) => Mutate(b => b.StateHighRead = enabled);
    public void SetPreWrite(bool enabled) => Mutate(b => b.StatePrewrite = enabled);
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
    public bool StatePrewrite = s.StatePrewrite;
    public bool StateUseBank1 = s.StateUseBank1;
    public bool StateHighRead = s.StateHighRead;
    public bool StateHighWrite = s.StateHighWrite;


    public SystemStatusSnapshot Build() => new(
        State80Store, StateRamRd, StateRamWrt, StateIntCxRom, StateAltZp, StateSlotC3Rom,
        StatePb0, StatePb1, StatePb2, StateAnn0, StateAnn1, StateAnn2, StateAnn3,
        StatePage2, StateHiRes, StateMixed, StateTextMode, StateShow80Col, StateAltCharSet, StateFlashOn, StatePrewrite, StateUseBank1, StateHighRead, StateHighWrite);
}

