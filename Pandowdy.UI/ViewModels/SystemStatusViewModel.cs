using ReactiveUI;
using Pandowdy.Core;
using System;

namespace Pandowdy.UI.ViewModels;

public sealed class SystemStatusViewModel : ReactiveObject
{
    private readonly ISystemStatusProvider _status;

    public bool State80Store { get => _state80Store; private set => this.RaiseAndSetIfChanged(ref _state80Store, value); }
    public bool StateRamRd { get => _stateRamRd; private set => this.RaiseAndSetIfChanged(ref _stateRamRd, value); }
    public bool StateRamWrt { get => _stateRamWrt; private set => this.RaiseAndSetIfChanged(ref _stateRamWrt, value); }
    public bool StateIntCxRom { get => _stateIntCxRom; private set => this.RaiseAndSetIfChanged(ref _stateIntCxRom, value); }
    public bool StateAltZp { get => _stateAltZp; private set => this.RaiseAndSetIfChanged(ref _stateAltZp, value); }
    public bool StateSlotC3Rom { get => _stateSlotC3Rom; private set => this.RaiseAndSetIfChanged(ref _stateSlotC3Rom, value); }
    public bool StatePb0 { get => _statePb0; private set => this.RaiseAndSetIfChanged(ref _statePb0, value); }
    public bool StatePb1 { get => _statePb1; private set => this.RaiseAndSetIfChanged(ref _statePb1, value); }
    public bool StatePb2 { get => _statePb2; private set => this.RaiseAndSetIfChanged(ref _statePb2, value); }
    public bool StateAnn0 { get => _stateAnn0; private set => this.RaiseAndSetIfChanged(ref _stateAnn0, value); }
    public bool StateAnn1 { get => _stateAnn1; private set => this.RaiseAndSetIfChanged(ref _stateAnn1, value); }
    public bool StateAnn2 { get => _stateAnn2; private set => this.RaiseAndSetIfChanged(ref _stateAnn2, value); }
    public bool StateAnn3 { get => _stateAnn3; private set => this.RaiseAndSetIfChanged(ref _stateAnn3, value); }
    public bool StatePage2 { get => _statePage2; private set => this.RaiseAndSetIfChanged(ref _statePage2, value); }
    public bool StateHiRes { get => _stateHiRes; private set => this.RaiseAndSetIfChanged(ref _stateHiRes, value); }
    public bool StateMixed { get => _stateMixed; private set => this.RaiseAndSetIfChanged(ref _stateMixed, value); }
    public bool StateTextMode { get => _stateTextMode; private set => this.RaiseAndSetIfChanged(ref _stateTextMode, value); }
    public bool StateShow80Col { get => _stateShow80Col; private set => this.RaiseAndSetIfChanged(ref _stateShow80Col, value); }
    public bool StateAltCharSet { get => _stateAltCharSet; private set => this.RaiseAndSetIfChanged(ref _stateAltCharSet, value); }
    public bool StateFlashOn { get => _stateFlashOn; private set => this.RaiseAndSetIfChanged(ref _stateFlashOn, value); }

    private bool _state80Store, _stateRamRd, _stateRamWrt, _stateIntCxRom, _stateAltZp, _stateSlotC3Rom,
                 _statePb0, _statePb1, _statePb2, _stateAnn0, _stateAnn1, _stateAnn2, _stateAnn3,
                 _statePage2, _stateHiRes, _stateMixed, _stateTextMode, _stateShow80Col, _stateAltCharSet, _stateFlashOn;

    public SystemStatusViewModel(ISystemStatusProvider status)
    {
        _status = status;
        // Immediate subscription; no activation required.
        _status.Stream.Subscribe(OnStatusNext);
        // Initialize with current snapshot so UI shows initial values.
        OnStatusNext(_status.Current);
    }

    private void OnStatusNext(SystemStatusSnapshot s)
    {
        State80Store = s.State80Store;
        StateRamRd = s.StateRamRd;
        StateRamWrt = s.StateRamWrt;
        StateIntCxRom = s.StateIntCxRom;
        StateAltZp = s.StateAltZp;
        StateSlotC3Rom = s.StateSlotC3Rom;
        StatePb0 = s.StatePb0;
        StatePb1 = s.StatePb1;
        StatePb2 = s.StatePb2;
        StateAnn0 = s.StateAnn0;
        StateAnn1 = s.StateAnn1;
        StateAnn2 = s.StateAnn2;
        StateAnn3 = s.StateAnn3_DGR;
        StatePage2 = s.StatePage2;
        StateHiRes = s.StateHiRes;
        StateMixed = s.StateMixed;
        StateTextMode = s.StateTextMode;
        StateShow80Col = s.StateShow80Col;
        StateAltCharSet = s.StateAltCharSet;
        StateFlashOn = s.StateFlashOn;
    }
}