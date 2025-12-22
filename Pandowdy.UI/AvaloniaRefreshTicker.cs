using System;
using System.Reactive.Linq;
using Avalonia.Threading;
using Pandowdy.EmuCore;

namespace Pandowdy.UI;

public sealed class AvaloniaRefreshTicker : IRefreshTicker
{
    private readonly IObservable<DateTime> _stream;
    private readonly IObserver<DateTime> _observer;

    public AvaloniaRefreshTicker()
    {
        var subject = new System.Reactive.Subjects.Subject<DateTime>();
        _observer = subject;
        _stream = subject.AsObservable();
    }

    public IObservable<DateTime> Stream => _stream;

    public void Start()
    {
        DispatcherTimer.Run(() =>
        {
            _observer.OnNext(DateTime.UtcNow);
            return true;
        }, TimeSpan.FromSeconds(1.0/60.0));
    }

    public void Stop()
    {
        // No-op for now; Avalonia timer will stop when app closes
    }
}
