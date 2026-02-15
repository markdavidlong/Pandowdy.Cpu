// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Exceptions;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Messages;

namespace Pandowdy.EmuCore;

/// <summary>
/// Represents an empty expansion slot in the Apple IIe, providing no functionality.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="NullCard"/> is a special implementation of <see cref="ICard"/> that
/// represents the absence of a peripheral card. It is the only card type guaranteed to
/// be available in the emulator and serves as the default state for all expansion slots
/// upon initialization.
/// </para>
/// <para>
/// <strong>Purpose and Design:</strong>
/// </para>
/// <list type="bullet">
/// <item><description><strong>Default State:</strong> All slots (1-7) are initialized with NullCard instances, representing empty slots</description></item>
/// <item><description><strong>Removal Placeholder:</strong> When a card is removed via <see cref="ISlots.RemoveCard"/>, it's replaced with a NullCard</description></item>
/// <item><description><strong>Floating Bus Enabler:</strong> By returning <c>null</c> for all read operations, NullCard causes the slots system to return floating bus values</description></item>
/// <item><description><strong>Silent Writes:</strong> All write operations are no-ops, preventing any state changes or side effects</description></item>
/// </list>
/// <para>
/// <strong>Behavioral Characteristics:</strong><br/>
/// The NullCard implements complete passivity:
/// </para>
/// <list type="bullet">
/// <item><description>All read methods return <c>null</c> (indicating "no response")</description></item>
/// <item><description>All write methods perform no operation</description></item>
/// <item><description>No internal state is maintained (completely stateless)</description></item>
/// <item><description>Cloning creates a new instance (though functionally identical)</description></item>
/// <item><description>Configuration methods are no-ops (<see cref="GetMetadata"/> returns empty, <see cref="ApplyMetadata"/> always succeeds)</description></item>
/// </list>
/// <para>
/// <strong>Configuration Support:</strong><br/>
/// NullCard implements <see cref="IConfigurable"/> (via <see cref="ICard"/>) with trivial
/// behavior: <see cref="GetMetadata"/> always returns an empty string, and <see cref="ApplyMetadata"/>
/// always returns <c>true</c> while ignoring any provided metadata. This allows NullCard to
/// participate in configuration serialization without requiring special-case handling.
/// </para>
/// <para>
/// <strong>Real Hardware Analogy:</strong><br/>
/// The NullCard emulates the behavior of an actual Apple IIe with no card installed in a slot.
/// When software accesses an empty slot's I/O or ROM space, the hardware returns whatever data
/// happens to be floating on the bus at that moment, which this implementation delegates to
/// the <see cref="IFloatingBusProvider"/>.
/// </para>
/// <para>
/// <strong>Factory Integration:</strong><br/>
/// The <see cref="ICardFactory"/> must always be able to provide NullCard instances via
/// <see cref="ICardFactory.GetNullCard"/>. This is enforced during <see cref="Slots"/>
/// initialization and card removal operations, which throw exceptions if NullCard creation fails.
/// </para>
/// <para>
/// <strong>Thread Safety:</strong><br/>
/// The NullCard is completely stateless and therefore inherently thread-safe. Multiple threads
/// can safely call any methods simultaneously without synchronization, though normal emulator
/// usage restricts all card operations to the emulator worker thread.
/// </para>
/// </remarks>
/// <seealso cref="ICard"/>
/// <seealso cref="IConfigurable"/>
/// <seealso cref="ISlots"/>
/// <seealso cref="ICardFactory"/>
/// <remarks>
/// Initializes a new instance of the <see cref="NullCard"/> class.
/// </remarks>
/// <param name="responseEmitter">The card response emitter for sending card responses.</param>
public class NullCard(ICardResponseEmitter responseEmitter) : ICard
{
    private readonly ICardResponseEmitter _responseEmitter = responseEmitter;
    private SlotNumber _slotNumber = SlotNumber.Unslotted;

    public SlotNumber Slot { get => _slotNumber; }

    /// <summary>
    /// Gets the display name for empty slots.
    /// </summary>
    /// <value>Always returns "Empty Slot".</value>
    /// <remarks>
    /// This name appears in UI elements like slot configuration dialogs and status displays
    /// to indicate that no peripheral card is installed.
    /// </remarks>
    public string Name => "Empty Slot";

    /// <summary>
    /// Gets a brief description of the NullCard.
    /// </summary>
    /// <value>Always returns "No card".</value>
    /// <remarks>
    /// This minimal description is appropriate since the NullCard represents the absence
    /// of functionality rather than any actual peripheral device.
    /// </remarks>
    public string Description => "No card";

    /// <summary>
    /// Gets the unique identifier for the NullCard type.
    /// </summary>
    /// <value>Always returns 0.</value>
    /// <remarks>
    /// <para>
    /// The ID value of 0 is reserved for NullCard and serves as a sentinel value throughout
    /// the emulator. Code can check <c>card.Id == 0</c> to determine if a slot is empty,
    /// which is formalized in the <see cref="ISlots.IsEmpty"/> convenience method.
    /// </para>
    /// <para>
    /// All other card types must use IDs â‰¥ 1 to avoid conflicts. The <see cref="ICardFactory"/>
    /// validates that no registered card uses ID 0 during registration.
    /// </para>
    /// </remarks>
    public int Id => 0;

    /// <summary>
    /// Attempts to read from the card's I/O space, always returning <c>null</c>.
    /// </summary>
    /// <param name="_">The I/O offset (ignored).</param>
    /// <returns>Always <c>null</c>, indicating no response.</returns>
    /// <remarks>
    /// The <c>null</c> return causes <see cref="ISlots"/> to return a floating bus value,
    /// emulating the behavior of reading from an empty slot on real Apple IIe hardware.
    /// </remarks>
    public byte? ReadIO(byte _) => null;

    /// <summary>
    /// Attempts to write to the card's I/O space, performing no operation.
    /// </summary>
    /// <param name="_">The I/O offset (ignored).</param>
    /// <param name="_1">The value to write (ignored).</param>
    /// <remarks>
    /// Writes to empty slots are silently ignored, matching real hardware behavior where
    /// writes to unpopulated slots have no effect.
    /// </remarks>
    public void WriteIO(byte _, byte _1) { /* NOP */ }

    /// <summary>
    /// Attempts to read from the card's ROM space, always returning <c>null</c>.
    /// </summary>
    /// <param name="_">The ROM offset (ignored).</param>
    /// <returns>Always <c>null</c>, indicating no response.</returns>
    /// <remarks>
    /// The <c>null</c> return causes <see cref="ISlots"/> to return a floating bus value.
    /// On real hardware, reading from an empty slot's ROM space returns whatever data
    /// happens to be on the bus.
    /// </remarks>
    public byte? ReadRom(byte _) => null;

    /// <summary>
    /// Attempts to write to the card's ROM space, performing no operation.
    /// </summary>
    /// <param name="_">The ROM offset (ignored).</param>
    /// <param name="_1">The value to write (ignored).</param>
    /// <remarks>
    /// Writes to empty slot ROM space are silently ignored, matching real hardware behavior.
    /// </remarks>
    public void WriteRom(byte _, byte _1) { /* NOP */ }

    /// <summary>
    /// Attempts to read from the card's extended ROM space, always returning <c>null</c>.
    /// </summary>
    /// <param name="_">The extended ROM offset (ignored).</param>
    /// <returns>Always <c>null</c>, indicating no response.</returns>
    /// <remarks>
    /// The <c>null</c> return causes <see cref="ISlots"/> to return a floating bus value.
    /// Even when an empty slot's extended ROM is "selected" via slot ROM access,
    /// reads return floating bus values since no physical ROM is present.
    /// </remarks>
    public byte? ReadExtendedRom(ushort _) => null;

    /// <summary>
    /// Attempts to write to the card's extended ROM space, performing no operation.
    /// </summary>
    /// <param name="_">The extended ROM offset (ignored).</param>
    /// <param name="_1">The value to write (ignored).</param>
    /// <remarks>
    /// Writes to empty slot extended ROM space are silently ignored, matching real hardware behavior.
    /// </remarks>
    public void WriteExtendedRom(ushort _, byte _1) { /* NOP */ }

    /// <summary>
    /// Creates a new instance of <see cref="NullCard"/>.
    /// </summary>
    /// <returns>A new <see cref="NullCard"/> instance.</returns>
    /// <remarks>
    /// <para>
    /// Although NullCard is stateless and all instances are functionally identical, the
    /// <see cref="Clone"/> method creates a new instance to maintain consistency with the
    /// <see cref="ICard"/> contract and allow each slot to have its own card reference.
    /// </para>
    /// <para>
    /// This is used during <see cref="Slots"/> initialization to populate all slots with
    /// independent NullCard instances, even though they behave identically.
    /// </para>
    /// </remarks>
    public ICard Clone() => new NullCard(_responseEmitter);

    /// <summary>
    /// Gets the configuration metadata for this NullCard.
    /// </summary>
    /// <returns>
    /// Always returns an empty string since NullCard has no configurable state.
    /// </returns>
    /// <remarks>
    /// <para>
    /// NullCard is completely stateless and has no configuration to persist. This method
    /// always returns an empty string to indicate the absence of any configuration data.
    /// </para>
    /// <para>
    /// This implementation satisfies the <see cref="IConfigurable"/> contract inherited
    /// through <see cref="ICard"/>, allowing NullCard to participate in configuration
    /// serialization without requiring special-case handling.
    /// </para>
    /// </remarks>
    public string GetMetadata() => string.Empty;

    /// <summary>
    /// Applies configuration metadata to this NullCard.
    /// </summary>
    /// <param name="metadata">The configuration metadata (ignored).</param>
    /// <returns>
    /// Always returns <c>true</c> since NullCard accepts any metadata without effect.
    /// </returns>
    /// <remarks>
    /// <para>
    /// NullCard is completely stateless and cannot be configured. This method ignores
    /// the provided metadata and always returns <c>true</c> to indicate "success" in
    /// the sense that the NullCard remains in its expected state (no configuration).
    /// </para>
    /// <para>
    /// This implementation allows NullCard to participate in configuration restoration
    /// without requiring special-case handling. Empty slots are simply skipped during
    /// configuration, and any metadata provided is silently ignored.
    /// </para>
    /// </remarks>
    public bool ApplyMetadata(string? metadata) => true;


    public void OnInstalled(SlotNumber slot)
    {
        _slotNumber = slot;
    }

    public void Reset()
    {
        /* NOP */
    }

    /// <summary>
    /// Handles a message sent to this empty slot.
    /// </summary>
    /// <param name="message">The message to process.</param>
    /// <exception cref="CardMessageException">
    /// Thrown if the message requires card-specific operations that an empty slot cannot perform.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <strong>Supported Messages:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item><description><see cref="IdentifyCardMessage"/>: Responds with CardId=0 and CardName="Empty Slot"</description></item>
    /// <item><description><see cref="EnumerateDevicesMessage"/>: Responds with an empty device list (0 devices)</description></item>
    /// <item><description><see cref="RefreshStatusMessage"/>: No-op (empty slot has no status to push)</description></item>
    /// </list>
    /// <para>
    /// All other messages throw <see cref="CardMessageException"/> because an empty slot
    /// cannot perform card-specific operations like inserting disks or configuring peripherals.
    /// </para>
    /// </remarks>
    public void HandleMessage(ICardMessage message)
    {
        switch (message)
        {
            case IdentifyCardMessage:
                _responseEmitter.Emit(Slot, Id, new CardIdentityPayload(Name));
                break;

            case EnumerateDevicesMessage:
                _responseEmitter.Emit(Slot, Id, new DeviceListPayload([]));
                break;

            case RefreshStatusMessage:
                // No-op: empty slot has no status to push. This is not a failure.
                break;

            default:
                // Empty slot cannot perform card-specific operations
                throw new CardMessageException(
                    $"Empty slot {Slot} does not support message type '{message.GetType().Name}'.");
        }
    }
}
