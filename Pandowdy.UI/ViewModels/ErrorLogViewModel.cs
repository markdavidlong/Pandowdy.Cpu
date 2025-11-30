using System;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using Pandowdy.Core;

namespace Pandowdy.UI.ViewModels;

//public sealed class ErrorLogViewModel : ReactiveObject, IActivatableViewModel
//{
//    private readonly IErrorProvider _errors;
//    public ViewModelActivator Activator { get; } = new();
//    public ReadOnlyObservableCollection<LogEvent> Logs { get; }
//    private readonly ObservableCollection<LogEvent> _mutable = new();

//    public ErrorLogViewModel(IErrorProvider errors)
//    {
//        _errors = errors;
//        Logs = new ReadOnlyObservableCollection<LogEvent>(_mutable);
//        this.WhenActivated(disposables =>
//        {
//            _errors.Events
//                .Buffer(TimeSpan.FromMilliseconds(100)) // batch bursts
//                .Where(batch => batch.Count > 0)
//                .ObserveOn(RxApp.MainThreadScheduler)
//                .Subscribe(batch =>
//                {
//                    foreach (var evt in batch)
//                    {
//                        _mutable.Add(evt);
//                        if (_mutable.Count > 1000)
//                        {
//                            _mutable.RemoveAt(0); // simple cap
//                        }
//                    }
//                })
//                .DisposeWith(disposables);
//        });
//    }
//}
