using ReactiveUI;
using Pandowdy.EmuCore;
using System.Reactive;

namespace Pandowdy.UI.ViewModels;

public sealed class MainWindowViewModel : ReactiveObject
{
    public EmulatorStateViewModel EmulatorState { get; }
    //public ErrorLogViewModel ErrorLog { get; }
    //public DisassemblyViewModel Disassembly { get; }
    public SystemStatusViewModel SystemStatus { get; }

    public ReactiveCommand<Unit, Unit> PauseCommand { get; }
    public ReactiveCommand<Unit, Unit> ContinueCommand { get; }
    public ReactiveCommand<Unit, Unit> StepCommand { get; }

    // UI state properties for menu bindings
    private bool _throttleEnabled = true;
    private bool _capsLockEnabled = true;
    private bool _showScanLines = true;
    private bool _forceMonochrome;
    private bool _decreaseContrast;
    private bool _monoMixed;

    public bool ThrottleEnabled
    {
        get => _throttleEnabled;
        set => this.RaiseAndSetIfChanged(ref _throttleEnabled, value);
    }

    public bool CapsLockEnabled
    {
        get => _capsLockEnabled;
        set => this.RaiseAndSetIfChanged(ref _capsLockEnabled, value);
    }

    public bool ShowScanLines
    {
        get => _showScanLines;
        set => this.RaiseAndSetIfChanged(ref _showScanLines, value);
    }

    public bool ForceMonochrome
    {
        get => _forceMonochrome;
        set => this.RaiseAndSetIfChanged(ref _forceMonochrome, value);
    }

    public bool DecreaseContrast
    {
        get => _decreaseContrast;
        set => this.RaiseAndSetIfChanged(ref _decreaseContrast, value);
    }

    public bool MonoMixed
    {
        get => _monoMixed;
        set => this.RaiseAndSetIfChanged(ref _monoMixed, value);
    }

    // Commands for menu actions
    public ReactiveCommand<Unit, Unit> ToggleThrottle { get; }
    public ReactiveCommand<Unit, Unit> ToggleCapsLock { get; }
    public ReactiveCommand<Unit, Unit> ToggleScanLines { get; }
    public ReactiveCommand<Unit, Unit> ToggleMonochrome { get; }
    public ReactiveCommand<Unit, Unit> ToggleDecreaseContrast { get; }
    public ReactiveCommand<Unit, Unit> ToggleMonoMixed { get; }

    // Emulator control commands bound from the menu
    public ReactiveCommand<Unit, Unit> StartEmu { get; }
    public ReactiveCommand<Unit, Unit> StopEmu { get; }
    public ReactiveCommand<Unit, Unit> ResetEmu { get; }
    public ReactiveCommand<Unit, Unit> StepOnce { get; }

    private readonly IEmulatorState _emuState;

    public MainWindowViewModel(EmulatorStateViewModel emulatorState,
                               //ErrorLogViewModel errorLog,
                               //DisassemblyViewModel disassembly,
                               IEmulatorState emuState,
                               SystemStatusViewModel systemStatus)
    {
        EmulatorState = emulatorState;
        //ErrorLog = errorLog;
        //Disassembly = disassembly;
        _emuState = emuState;
        SystemStatus = systemStatus;

        PauseCommand = ReactiveCommand.Create(() => _emuState.RequestPause());
        ContinueCommand = ReactiveCommand.Create(() => _emuState.RequestContinue());
        StepCommand = ReactiveCommand.Create(() => _emuState.RequestStep());

        // Toggle commands mutate reactive properties; the View can observe and bridge to services
        ToggleThrottle = ReactiveCommand.Create(() => { ThrottleEnabled = !ThrottleEnabled; });
        ToggleCapsLock = ReactiveCommand.Create(() => { CapsLockEnabled = !CapsLockEnabled; });
        ToggleScanLines = ReactiveCommand.Create(() => { ShowScanLines = !ShowScanLines; });
        ToggleMonochrome = ReactiveCommand.Create(() => { ForceMonochrome = !ForceMonochrome; });
        ToggleDecreaseContrast = ReactiveCommand.Create(() => { DecreaseContrast = !DecreaseContrast; });
        ToggleMonoMixed = ReactiveCommand.Create(() => { MonoMixed = !MonoMixed; });

        // Emulator commands (view bridges to actions)
        StartEmu = ReactiveCommand.Create(() => { });
        StopEmu = ReactiveCommand.Create(() => { });
        ResetEmu = ReactiveCommand.Create(() => { });
        StepOnce = ReactiveCommand.Create(() => { });
    }
}
