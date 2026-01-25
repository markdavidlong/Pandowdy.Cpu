using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.Services;

/// <summary>
/// Thread-safe implementation of <see cref="ITelemetryAggregator"/> for device telemetry.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Singleton Pattern:</strong> This class should be registered as a singleton in the
/// DI container. All devices share the same aggregator instance, ensuring messages from all
/// sources flow through a single stream.
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> ID allocation uses <see cref="Interlocked"/> methods
/// for lock-free atomic increments. The underlying <see cref="Subject{T}"/> handles
/// concurrent Publish calls safely.
/// </para>
/// <para>
/// <strong>Memory:</strong> The stream does not replay messages to late subscribers.
/// Each subscriber receives only messages published after subscription.
/// </para>
/// <para>
/// <strong>Resend Requests:</strong> Subscribers can request providers to resend their
/// current state via the request methods. Providers subscribe to <see cref="ResendRequests"/>
/// and filter for matching requests using <see cref="ResendRequest.MatchesProvider"/>.
/// </para>
/// <para>
/// <strong>Disposal:</strong> The internal Subject is not explicitly disposed. In a typical
/// application lifecycle, the aggregator lives for the entire session. If explicit cleanup
/// is needed, implement IDisposable and complete the subject.
/// </para>
/// </remarks>
public sealed class TelemetryAggregator : ITelemetryAggregator
{
    /// <summary>
    /// Counter for generating unique telemetry IDs.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="Interlocked"/> for thread-safe ID generation.
    /// Starting at 0 means first ID will be 1.
    /// </remarks>
    private int _nextId;

    /// <summary>
    /// The reactive subject for telemetry messages.
    /// </summary>
    /// <remarks>
    /// Subject is used rather than ReplaySubject because telemetry is real-time;
    /// late subscribers don't need historical messages.
    /// </remarks>
    private readonly Subject<TelemetryMessage> _messageSubject;

    /// <summary>
    /// The reactive subject for resend requests.
    /// </summary>
    private readonly Subject<ResendRequest> _resendSubject;

    /// <summary>
    /// Cached observable wrapper for messages to prevent direct access to the subject.
    /// </summary>
    private readonly IObservable<TelemetryMessage> _stream;

    /// <summary>
    /// Cached observable wrapper for resend requests to prevent direct access to the subject.
    /// </summary>
    private readonly IObservable<ResendRequest> _resendStream;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelemetryAggregator"/> class.
    /// </summary>
    public TelemetryAggregator()
    {
        _messageSubject = new Subject<TelemetryMessage>();
        _resendSubject = new Subject<ResendRequest>();
        _stream = _messageSubject.AsObservable();
        _resendStream = _resendSubject.AsObservable();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Thread-safe via <see cref="Interlocked"/>.
    /// </remarks>
    public TelemetryId CreateId(string category)
    {
        ArgumentNullException.ThrowIfNull(category);

        int id = Interlocked.Increment(ref _nextId);
        return new TelemetryId(id, category);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Thread-safe via the underlying Subject implementation.
    /// Messages are delivered synchronously to all subscribers.
    /// </remarks>
    public void Publish(TelemetryMessage message)
    {
        _messageSubject.OnNext(message);
    }

        /// <inheritdoc />
        public IObservable<TelemetryMessage> Stream => _stream;

        /// <inheritdoc />
        public IObservable<ResendRequest> ResendRequests => _resendStream;

        /// <inheritdoc />
        /// <remarks>
        /// Publishes the resend request to all subscribers of the <see cref="ResendRequests"/> stream.
        /// Providers filter for matching requests using <see cref="ResendRequest.MatchesProvider"/>.
        /// </remarks>
        public void PublishResendRequest(ResendRequest request)
        {
            _resendSubject.OnNext(request);
        }
    }
