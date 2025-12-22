using System;
using ReactiveUI;
using Pandowdy.EmuCore;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace Pandowdy.UI.ViewModels;

public sealed class EmulatorStateViewModel : ReactiveObject, IActivatableViewModel
{
    private readonly IEmulatorState _state;
    public ViewModelActivator Activator { get; } = new();

    private ushort _pc;
    public ushort PC { get => _pc; private set => this.RaiseAndSetIfChanged(ref _pc, value); }

    private int? _lineNumber;
    public int? LineNumber { get => _lineNumber; private set => this.RaiseAndSetIfChanged(ref _lineNumber, value); }

    private ulong _cycles;
    public ulong Cycles { get => _cycles; private set => this.RaiseAndSetIfChanged(ref _cycles, value); }

    public EmulatorStateViewModel(IEmulatorState state)
    {
        _state = state;
        this.WhenActivated(disposables =>
        {
            var sub = _state.Stream.Sample(TimeSpan.FromMilliseconds(50))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(s => { PC = s.PC; LineNumber = s.LineNumber; Cycles = s.Cycles; });
            disposables.Add(sub);
        });
    }
}
