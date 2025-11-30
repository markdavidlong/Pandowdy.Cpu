using System;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using Pandowdy.Core;

namespace Pandowdy.UI.ViewModels;

//public sealed class DisassemblyViewModel : ReactiveObject, IActivatableViewModel
//{
//    private readonly IDisassemblyProvider _disasm;
//    public ViewModelActivator Activator { get; } = new();

//    private readonly ObservableCollection<Line> _lines = new();
//    public ReadOnlyObservableCollection<Line> Lines { get; }

//    private ushort _highlightPc;
//    public ushort HighlightPC { get => _highlightPc; private set => this.RaiseAndSetIfChanged(ref _highlightPc, value); }

//    public DisassemblyViewModel(IDisassemblyProvider disasm)
//    {
//        _disasm = disasm;
//        Lines = new ReadOnlyObservableCollection<Line>(_lines);
//        this.WhenActivated(disposables =>
//        {
//            _disasm.Updates
//                .ObserveOn(RxApp.MainThreadScheduler)
//                .Subscribe(update =>
//                {
//                    // naive replace; later optimize for partial patch
//                    _lines.Clear();
//                    foreach (var l in update.Lines)
//                    {
//                        _lines.Add(l);
//                    }
//                })
//                .DisposeWith(disposables);
//        });
//    }

//    public void RequestRange(AddressRange range) => _disasm.Invalidate(range);
//}
