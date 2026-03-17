// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System;
using System.Reactive.Linq;
using Avalonia.Threading;
using Pandowdy.UI.Interfaces;

namespace Pandowdy.UI;

/// <summary>
/// Avalonia-based implementation of <see cref="IRefreshTicker"/> providing UI refresh timing.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Purpose:</strong> Provides a timing signal for driving UI-based frame rendering
/// and updates at the frequency defined in <see cref="Constants.RefreshRates.BaseTickerHz"/>.
/// Uses Avalonia's DispatcherTimer to ensure all timing events occur on the UI thread.
/// </para>
/// <para>
/// <strong>Implementation:</strong> Uses Rx.NET's Subject pattern to create an observable stream
/// of timestamps. The DispatcherTimer publishes to this stream at the configured refresh rate.
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> All timer callbacks execute on the Avalonia UI thread via
/// DispatcherTimer, ensuring safe interaction with UI elements without explicit marshaling.
/// </para>
/// <para>
/// <strong>Lifecycle:</strong> The timer starts when <see cref="Start"/> is called and runs
/// continuously until the application closes. The <see cref="Stop"/> method is currently a no-op
/// as Avalonia's DispatcherTimer automatically stops when the application shuts down.
/// </para>
/// <para>
/// <strong>Usage Pattern:</strong>
/// <code>
/// var ticker = new AvaloniaRefreshTicker();
/// ticker.Stream.Subscribe(timestamp => RefreshDisplay());
/// ticker.Start(); // Begin emitting updates at configured refresh rate
/// </code>
/// </para>
/// </remarks>
public sealed class AvaloniaRefreshTicker : IRefreshTicker
{
    /// <summary>
    /// Subject used to publish timestamp events to the observable stream.
    /// </summary>
    /// <remarks>
    /// Stored as concrete Subject&lt;DateTime&gt; type rather than IObserver&lt;DateTime&gt;
    /// for improved performance by avoiding interface dispatch overhead.
    /// </remarks>
    private readonly System.Reactive.Subjects.Subject<DateTime> _subject;

    /// <summary>
    /// Initializes a new instance of the <see cref="AvaloniaRefreshTicker"/> class.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Rx.NET Pattern:</strong> Creates a Subject that acts as both an observer
    /// (for publishing events) and an observable (for subscribers to receive events).
    /// </para>
    /// <para>
    /// <strong>Stream Setup:</strong> The Subject is wrapped in AsObservable() to prevent
    /// external code from directly calling OnNext/OnError/OnCompleted on the Subject.
    /// This encapsulation ensures only this class can publish events.
    /// </para>
    /// <para>
    /// <strong>No Timer Start:</strong> The constructor does not start the timer automatically.
    /// Call <see cref="Start"/> to begin emitting timing events.
    /// </para>
    /// </remarks>
    public AvaloniaRefreshTicker()
    {
        _subject = new System.Reactive.Subjects.Subject<DateTime>();
        Stream = _subject.AsObservable();
    }

    /// <summary>
    /// Gets the observable stream that emits timestamps at the configured refresh rate.
    /// </summary>
    /// <value>
    /// An <see cref="IObservable{DateTime}"/> that emits UTC timestamps each time the ticker fires.
    /// </value>
    /// <remarks>
    /// <para>
    /// <strong>Frequency:</strong> Emits at the rate defined in <see cref="Constants.RefreshRates.BaseTickerHz"/>
    /// when the ticker is running.
    /// </para>
    /// <para>
    /// <strong>Thread Context:</strong> All emissions occur on the Avalonia UI thread, making it
    /// safe to update UI elements directly from subscribers without additional marshaling.
    /// </para>
    /// <para>
    /// <strong>Timestamp Format:</strong> Provides DateTime.UtcNow at each emission. Subscribers
    /// can use these timestamps for time-based calculations or simply as periodic triggers.
    /// </para>
    /// <para>
    /// <strong>Subscription Example:</strong>
    /// <code>
    /// ticker.Stream
    ///     .Subscribe(timestamp =>
    ///     {
    ///         // This executes on UI thread at the configured refresh rate
    ///         UpdateDisplay();
    ///     });
    /// </code>
    /// </para>
    /// </remarks>
    public IObservable<DateTime> Stream { get; }

    /// <summary>
    /// Starts the refresh ticker, causing it to begin emitting periodic timing signals.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Timer Setup:</strong> Uses Avalonia's DispatcherTimer.Run() with the interval
    /// defined in <see cref="Constants.RefreshRates.BaseTickerMs"/>
    /// to achieve the configured refresh rate.
    /// </para>
    /// <para>
    /// <strong>Callback Behavior:</strong> The timer callback returns true to indicate it should
    /// continue running. Each callback publishes the current UTC time to the stream via
    /// _subject.OnNext(DateTime.UtcNow).
    /// </para>
    /// <para>
    /// <strong>Idempotency:</strong> Calling Start() multiple times will create multiple timer
    /// instances. This is generally not recommended but won't cause crashes. Consider adding
    /// a guard flag if multiple Start() calls are a concern.
    /// </para>
    /// <para>
    /// <strong>Thread Affinity:</strong> All timer callbacks execute on the Avalonia UI dispatcher
    /// thread, ensuring safe UI interaction without explicit Invoke() calls.
    /// </para>
    /// <para>
    /// <strong>Timing Accuracy:</strong> The actual interval may vary slightly based on UI thread
    /// load and system scheduling, but should maintain an average close to the configured rate
    /// for smooth animation.
    /// </para>
    /// </remarks>
    public void Start()
    {
        DispatcherTimer.Run(() =>
        {
            _subject.OnNext(DateTime.UtcNow);
            return true;
        }, TimeSpan.FromMilliseconds(Constants.RefreshRates.BaseTickerMs));
    }

    /// <summary>
    /// Stops the refresh ticker. Currently a no-op implementation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Current Implementation:</strong> This method is currently a no-op (does nothing).
    /// The DispatcherTimer will automatically stop when the Avalonia application closes.
    /// </para>
    /// <para>
    /// <strong>Future Enhancement:</strong> A proper implementation would:
    /// <list type="bullet">
    /// <item>Store the timer handle returned by DispatcherTimer.Run()</item>
    /// <item>Call timer.Stop() or dispose the handle to halt timing events</item>
    /// <item>Potentially call _observer.OnCompleted() to signal stream completion</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Current Behavior:</strong> Timers continue running until application shutdown.
    /// For most use cases (single ticker for application lifetime), this is acceptable.
    /// </para>
    /// <para>
    /// <strong>Memory Implications:</strong> If creating/disposing multiple tickers during
    /// application lifetime, the lack of Stop() implementation could result in multiple
    /// timer instances running simultaneously.
    /// </para>
    /// </remarks>
    public void Stop()
    {
        // No-op for now; Avalonia timer will stop when app closes
    }
}
