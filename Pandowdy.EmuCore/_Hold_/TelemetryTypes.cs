namespace Pandowdy.EmuCore.DataTypes;

/// <summary>
/// Unique identifier for a telemetry provider, containing both a numeric ID and a category descriptor.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Purpose:</strong> Each device or component that emits telemetry receives a unique
/// TelemetryId from the <see cref="Interfaces.ITelemetryAggregator"/>. This ID is included in
/// every telemetry message, allowing receivers to identify the source and decode the payload
/// appropriately.
/// </para>
/// <para>
/// <strong>Category:</strong> The category string describes the type of device or data format
/// (e.g., "DiskII", "Printer", "MockCard"). Receivers can filter messages by category and use
/// appropriate decoders for each category type.
/// </para>
/// <para>
/// <strong>Uniqueness:</strong> The numeric Id is unique per provider instance within a session.
/// Two DiskII drives would have different Ids but the same Category.
/// </para>
/// </remarks>
/// <param name="Id">Unique numeric identifier assigned by the TelemetryAggregator.</param>
/// <param name="Category">Descriptor string indicating the type of telemetry data (e.g., "DiskII").</param>
public readonly record struct TelemetryId(int Id, string Category)
{
    /// <summary>
    /// Returns a string representation of the telemetry ID.
    /// </summary>
    /// <returns>Format: "Category:Id" (e.g., "DiskII:3").</returns>
    public override string ToString() => $"{Category}:{Id}";
}

/// <summary>
/// A telemetry message emitted by a device or component.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Structure:</strong> Each message contains the source identifier, a message type
/// describing what changed, and an optional payload with the actual data.
/// </para>
/// <para>
/// <strong>MessageType:</strong> A string describing the specific event or state change
/// (e.g., "motor-on", "track-changed", "disk-inserted"). The valid message types depend
/// on the source category.
/// </para>
/// <para>
/// <strong>Payload:</strong> The payload type varies by message type. Receivers should
/// use the SourceId.Category and MessageType to determine how to decode the payload.
/// Common patterns:
/// <list type="bullet">
/// <item>Boolean for on/off states (motor, write-protect)</item>
/// <item>Integer for numeric values (track number, sector)</item>
/// <item>String for descriptive data (disk name, error messages)</item>
/// <item>Custom records for complex state</item>
/// </list>
/// </para>
/// </remarks>
/// <param name="SourceId">The telemetry ID of the device that emitted this message.</param>
/// <param name="MessageType">A string describing the type of event or state change.</param>
/// <param name="Payload">Optional data associated with the message; type depends on MessageType.</param>
public readonly record struct TelemetryMessage(
    TelemetryId SourceId,
    string MessageType,
    object? Payload = null)
{
    /// <summary>
    /// Returns a string representation of the telemetry message.
    /// </summary>
        /// <returns>Format: "[SourceId] MessageType: Payload".</returns>
        public override string ToString() => 
            Payload is null 
                ? $"[{SourceId}] {MessageType}" 
                : $"[{SourceId}] {MessageType}: {Payload}";
    }

    /// <summary>
    /// Scope of a telemetry resend request.
    /// </summary>
    public enum ResendScope
    {
        /// <summary>Request all providers to resend their current state.</summary>
        All,

        /// <summary>Request a specific provider (by ID) to resend.</summary>
        ById,

        /// <summary>Request all providers of a category to resend.</summary>
        ByCategory
    }

    /// <summary>
    /// A request for telemetry providers to resend their current state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Purpose:</strong> Allows subscribers (ViewModels) to request current state from
    /// providers without waiting for the next state change. Useful for initialization, refresh,
    /// or reconnection scenarios.
    /// </para>
    /// <para>
    /// <strong>Provider Handling:</strong> Providers subscribe to the ResendRequests stream
    /// and filter for requests that match their ID or category:
    /// <code>
    /// aggregator.ResendRequests
    ///     .Where(r => r.MatchesProvider(_myId))
    ///     .Subscribe(_ => PublishCurrentState());
    /// </code>
    /// </para>
    /// </remarks>
    /// <param name="Scope">The scope of the resend request (All, ById, or ByCategory).</param>
    /// <param name="ProviderId">The specific provider ID (only valid when Scope is ById).</param>
    /// <param name="Category">The category filter (only valid when Scope is ByCategory).</param>
    public readonly record struct ResendRequest(
        ResendScope Scope,
        int? ProviderId = null,
        string? Category = null)
    {
        /// <summary>
        /// Creates a request for all providers to resend.
        /// </summary>
        public static ResendRequest All => new(ResendScope.All);

        /// <summary>
        /// Creates a request for a specific provider to resend.
        /// </summary>
        /// <param name="providerId">The numeric ID of the provider.</param>
        public static ResendRequest ForProvider(int providerId) => 
            new(ResendScope.ById, ProviderId: providerId);

        /// <summary>
        /// Creates a request for all providers of a category to resend.
        /// </summary>
        /// <param name="category">The category name (e.g., "DiskII").</param>
        /// <exception cref="ArgumentNullException">Thrown when category is null.</exception>
        public static ResendRequest ForCategory(string category)
        {
            ArgumentNullException.ThrowIfNull(category);
            return new(ResendScope.ByCategory, Category: category);
        }

        /// <summary>
        /// Determines if this resend request applies to the specified provider.
        /// </summary>
        /// <param name="telemetryId">The provider's telemetry ID.</param>
        /// <returns>True if the provider should respond to this request.</returns>
        public bool MatchesProvider(TelemetryId telemetryId) => Scope switch
        {
            ResendScope.All => true,
            ResendScope.ById => ProviderId == telemetryId.Id,
            ResendScope.ByCategory => Category == telemetryId.Category,
            _ => false
        };

        /// <summary>
        /// Returns a string representation of the resend request.
        /// </summary>
        public override string ToString() => Scope switch
        {
            ResendScope.All => "ResendRequest[All]",
            ResendScope.ById => $"ResendRequest[Id={ProviderId}]",
            ResendScope.ByCategory => $"ResendRequest[Category={Category}]",
            _ => "ResendRequest[Unknown]"
        };
    }
