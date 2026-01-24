using System;
using Pandowdy.EmuCore.DataTypes;

namespace Pandowdy.EmuCore.Interfaces;

/// <summary>
/// Read-only interface for subscribing to device telemetry messages.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Purpose:</strong> Provides read-only access to the telemetry message stream for
/// subscribers (typically ViewModels in the UI layer). This interface exposes only the
/// subscription capability, not the publishing or ID allocation methods.
/// </para>
/// <para>
/// <strong>Interface Segregation:</strong> This interface is exposed through
/// <see cref="IEmulatorCoreInterface"/> to ensure the UI can only subscribe to messages,
/// not create IDs or publish messages (which are internal operations for devices).
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> The stream can be subscribed to from any thread.
/// Subscribers should use <c>ObserveOn(RxApp.MainThreadScheduler)</c> to marshal
/// callbacks to the UI thread before updating bound properties.
/// </para>
/// <para>
/// <strong>Resend Requests:</strong> To request providers to resend their current state,
/// use the request methods on <see cref="IEmulatorCoreInterface"/> (RequestTelemetryResend,
/// RequestTelemetryResendById, RequestTelemetryResendByCategory). These are routed through
/// the emulator's command queue for thread-safe execution. Providers listen to
/// <see cref="ResendRequests"/> and republish their current state when a matching request arrives.
/// </para>
/// <para>
/// <strong>Usage Pattern:</strong>
/// <code>
/// // In a ViewModel:
/// machine.Telemetry.Stream
///     .Where(m => m.SourceId.Category == "DiskII")
///     .ObserveOn(RxApp.MainThreadScheduler)
///     .Subscribe(HandleDiskTelemetry);
/// 
/// // Request current state on startup (through seam interface, queued)
/// machine.RequestTelemetryResendByCategory("DiskII");
/// </code>
/// </para>
/// </remarks>
public interface ITelemetryStream
{
    /// <summary>
    /// Gets the observable stream of all telemetry messages.
    /// </summary>
    /// <value>
    /// An <see cref="IObservable{TelemetryMessage}"/> that emits all published messages.
    /// </value>
    /// <remarks>
    /// <para>
    /// Subscribers receive all messages from all providers. Use LINQ operators
    /// to filter by <see cref="TelemetryId.Category"/> or specific source IDs:
    /// </para>
    /// <code>
    /// // Filter by category
    /// stream.Where(m => m.SourceId.Category == "DiskII")
    /// 
    /// // Filter by specific device
    /// stream.Where(m => m.SourceId.Id == knownDeviceId)
    /// 
    /// // Filter by message type
    /// stream.Where(m => m.MessageType == "motor")
    /// </code>
    /// <para>
    /// For UI consumption, remember to observe on the main thread scheduler.
    /// </para>
    /// </remarks>
    IObservable<TelemetryMessage> Stream { get; }

    /// <summary>
    /// Gets the observable stream of resend requests.
    /// </summary>
    /// <value>
    /// An <see cref="IObservable{ResendRequest}"/> that emits when resend requests are processed.
    /// </value>
    /// <remarks>
    /// <para>
    /// Providers subscribe to this stream to respond to resend requests. Use the
    /// <see cref="ResendRequest.MatchesProvider"/> method to filter for relevant requests:
    /// </para>
    /// <code>
    /// aggregator.ResendRequests
    ///     .Where(r => r.MatchesProvider(_myId))
    ///     .Subscribe(_ => PublishCurrentState());
    /// </code>
    /// <para>
    /// <strong>Threading:</strong> Resend requests are processed on the emulator thread
    /// after being queued through <see cref="IEmulatorCoreInterface"/>. This ensures
    /// providers receive requests in a thread-safe manner consistent with other
    /// emulator operations.
    /// </para>
    /// </remarks>
    IObservable<ResendRequest> ResendRequests { get; }
}

/// <summary>
/// Central hub for device telemetry, providing ID allocation, message publishing, and subscription.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Purpose:</strong> The TelemetryAggregator serves as a central message bus for
/// devices and components within the emulator core to communicate status, events, and
/// diagnostic information to the UI layer or other observers without tight coupling.
/// </para>
/// <para>
/// <strong>Interface Segregation:</strong> This interface extends <see cref="ITelemetryStream"/>
/// to add publishing and ID allocation capabilities. Devices receive this full interface,
/// while the UI receives only <see cref="ITelemetryStream"/> through <see cref="IEmulatorCoreInterface"/>.
/// </para>
/// <para>
/// <strong>DI Pattern:</strong> This interface is registered as a singleton in the DI container
/// and injected into any components that need to emit telemetry (cards, drives, peripherals).
/// ViewModels subscribe to the stream to receive and display device status.
/// </para>
/// <para>
/// <strong>ID Allocation:</strong> Components call <see cref="CreateId"/> during construction
/// to obtain a unique <see cref="TelemetryId"/>. The category parameter allows receivers to
/// filter and decode messages appropriately.
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> Implementations must be thread-safe, as devices may publish
/// from the emulator thread while UI subscribes and processes on the main thread.
/// </para>
/// <para>
/// <strong>Usage Pattern:</strong>
/// <code>
/// // In a device constructor:
/// public DiskIIDrive(ITelemetryAggregator telemetry)
/// {
///     _telemetryId = telemetry.CreateId("DiskII");
///     _telemetry = telemetry;
/// }
/// 
/// // When state changes:
/// _telemetry.Publish(new TelemetryMessage(_telemetryId, "motor", motorOn));
/// </code>
/// </para>
/// </remarks>
public interface ITelemetryAggregator : ITelemetryStream
{
    /// <summary>
    /// Creates a unique telemetry ID for a provider.
    /// </summary>
    /// <param name="category">
    /// A descriptor string indicating the type of telemetry data this provider will emit.
    /// Examples: "DiskII", "Printer", "LanguageCard", "MockCard".
    /// </param>
    /// <returns>
    /// A new <see cref="TelemetryId"/> with a unique numeric ID and the specified category.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Each call to this method returns a new ID with a unique numeric component.
    /// Multiple providers can share the same category (e.g., two DiskII drives),
    /// but each will have a distinct numeric ID.
    /// </para>
    /// <para>
    /// This method should be called once per provider, typically in the constructor.
    /// The returned ID should be stored and used for all subsequent Publish calls.
    /// </para>
    /// </remarks>
    TelemetryId CreateId(string category);
    
        /// <summary>
        /// Publishes a telemetry message to all subscribers.
        /// </summary>
        /// <param name="message">The telemetry message to publish.</param>
        /// <remarks>
        /// <para>
        /// Messages are delivered to all subscribers of <see cref="ITelemetryStream.Stream"/> synchronously
        /// on the calling thread. For high-frequency messages, consider throttling or
        /// sampling on the subscriber side.
        /// </para>
        /// <para>
        /// This method is thread-safe and may be called from any thread.
        /// </para>
        /// </remarks>
        void Publish(TelemetryMessage message);

        /// <summary>
        /// Publishes a resend request to all providers.
        /// </summary>
        /// <param name="request">The resend request to publish.</param>
        /// <remarks>
        /// <para>
        /// <strong>Internal Use:</strong> This method is called by VA2M after processing
        /// queued resend requests from <see cref="IEmulatorCoreInterface"/>. External callers
        /// should use the request methods on IEmulatorCoreInterface instead.
        /// </para>
        /// <para>
        /// Requests are delivered to all subscribers of <see cref="ITelemetryStream.ResendRequests"/>.
        /// Providers filter for matching requests using <see cref="ResendRequest.MatchesProvider"/>.
        /// </para>
        /// </remarks>
        void PublishResendRequest(ResendRequest request);
    }
