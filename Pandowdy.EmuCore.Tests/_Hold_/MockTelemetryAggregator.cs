using System.Reactive.Linq;
using System.Reactive.Subjects;
using Pandowdy.EmuCore.DataTypes;
using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.Tests.Helpers;

/// <summary>
/// Mock implementation of <see cref="ITelemetryAggregator"/> for unit testing.
/// </summary>
/// <remarks>
/// <para>
/// This mock records all published messages for verification in tests.
/// It also provides observable streams for testing subscription behavior.
/// </para>
/// </remarks>
public class MockTelemetryAggregator : ITelemetryAggregator, IDisposable
{
    private int _nextId;
    private readonly Subject<TelemetryMessage> _messageSubject = new();
    private readonly Subject<ResendRequest> _resendSubject = new();
    private bool _disposed;

    /// <summary>
    /// Gets the list of all published telemetry messages.
    /// </summary>
    public List<TelemetryMessage> PublishedMessages { get; } = [];

    /// <summary>
    /// Gets the list of all published resend requests.
    /// </summary>
    public List<ResendRequest> PublishedResendRequests { get; } = [];

    /// <inheritdoc />
    public TelemetryId CreateId(string category)
    {
        return new TelemetryId(Interlocked.Increment(ref _nextId), category);
    }

    /// <inheritdoc />
    public void Publish(TelemetryMessage message)
    {
        PublishedMessages.Add(message);
        _messageSubject.OnNext(message);
    }

    /// <inheritdoc />
    public void PublishResendRequest(ResendRequest request)
    {
        PublishedResendRequests.Add(request);
        _resendSubject.OnNext(request);
    }

    /// <inheritdoc />
    public IObservable<TelemetryMessage> Stream => _messageSubject.AsObservable();

    /// <inheritdoc />
    public IObservable<ResendRequest> ResendRequests => _resendSubject.AsObservable();

    /// <summary>
    /// Clears all recorded messages and requests.
    /// </summary>
    public void Clear()
    {
        PublishedMessages.Clear();
        PublishedResendRequests.Clear();
    }

    /// <summary>
    /// Gets the last published message, or null if none.
    /// </summary>
    public TelemetryMessage? LastMessage =>
        PublishedMessages.Count > 0 ? PublishedMessages[^1] : null;

    /// <summary>
    /// Gets all messages of a specific type.
    /// </summary>
    public IEnumerable<TelemetryMessage> GetMessagesByType(string messageType) =>
        PublishedMessages.Where(m => m.MessageType == messageType);

    /// <summary>
    /// Gets all messages from a specific category.
    /// </summary>
    public IEnumerable<TelemetryMessage> GetMessagesByCategory(string category) =>
        PublishedMessages.Where(m => m.SourceId.Category == category);

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _messageSubject.Dispose();
            _resendSubject.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
