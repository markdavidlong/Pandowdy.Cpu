using System;
using Avalonia.Threading;
using Pandowdy.Core;

namespace Pandowdy.UI
{
    public sealed class AvaloniaRefreshTicker : IRefreshTicker
    {
        private readonly DispatcherTimer _timer;
        private readonly System.Reactive.Subjects.Subject<DateTime> _subject = new();

        public AvaloniaRefreshTicker()
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1000.0 / 60.0)
            };
            _timer.Tick += (_, __) => _subject.OnNext(DateTime.UtcNow);
        }

        public IObservable<DateTime> Stream => _subject;

        public void Start()
        {
            if (!_timer.IsEnabled)
            {
                _timer.Start();
            }
        }

        public void Stop()
        {
            if (_timer.IsEnabled)
            {
                _timer.Stop();
            }
        }
    }
}
