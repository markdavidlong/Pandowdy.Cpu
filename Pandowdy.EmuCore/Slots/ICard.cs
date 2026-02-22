// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Machine;

namespace Pandowdy.EmuCore.Slots;

/// <summary>
/// Represents an Apple II peripheral card that can be installed in expansion slots 1-7.
/// </summary>
/// <remarks>
/// <para>
/// The Apple IIe provides seven expansion slots, each offering three distinct address spaces
/// that cards can use for I/O operations, firmware, and extended functionality:
/// </para>
/// <list type="bullet">
/// <item><description><b>Card I/O</b> ($C0x0-$C0xF): 16 bytes for hardware control registers</description></item>
/// <item><description><b>Card ROM</b> ($Cx00-$CxFF): 256 bytes for boot/driver firmware</description></item>
/// <item><description><b>Extended ROM</b> ($C800-$CFFF): 2KB shared space for extended firmware</description></item>
/// </list>
/// <para>
/// All read methods return nullable bytes (<see cref="Nullable{Byte}"/>). A <c>null</c> return
/// indicates the card did not respond at that address, allowing the system to return floating
/// bus values or fall back to system ROM.
/// </para>
/// <para>
/// Each card type must implement the <see cref="Clone"/> method to support multiple instances
/// being created from a single registered prototype in the <see cref="ICardFactory"/>.
/// </para>
/// <para>
/// <strong>Configuration Management:</strong><br/>
/// Cards implement <see cref="IConfigurable"/>, enabling save/restore of card-specific
/// configuration (drive images, serial port settings, etc.). The metadata format is
/// card-specific, but cards with child components (like drives) should use the hierarchical
/// configuration pattern, embedding child metadata within their own. See <see cref="IConfigurable"/>
/// for detailed guidance on configuration strategies.
/// </para>
/// </remarks>
/// <seealso cref="IConfigurable"/>
/// <seealso cref="ICardFactory"/>
/// <seealso cref="ISlots"/>
public interface ICard : IConfigurable
{


    public SlotNumber Slot { get; }

    /// <summary>
    /// Gets the human-readable name of the card (e.g., "Disk II Controller", "Super Serial Card").
    /// </summary>
    /// <remarks>
    /// This name is used for display in UI and configuration. Card names should be unique
    /// across all card types to prevent ambiguity during card selection.
    /// </remarks>
    public string Name { get; }

    /// <summary>
    /// Gets a detailed description of the card's functionality and features.
    /// </summary>
    /// <remarks>
    /// This description can be displayed in tooltips, help text, or card selection dialogs
    /// to help users understand what the card does.
    /// </remarks>
    public string Description { get; }

    /// <summary>
    /// Gets the unique numeric identifier for this card type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// While implemented as an instance property, this should return a constant value
    /// for all instances of the same card type. Each card implementation should use
    /// a unique ID (e.g., NullCard = 0, Disk II = 1, Serial = 2).
    /// </para>
    /// <para>
    /// The <see cref="ICardFactory"/> validates that no two card types share the same ID
    /// during registration and will throw an exception if duplicates are detected.
    /// </para>
    /// </remarks>
    public int Id { get; }

    // Reads all return nullable bytes. A nullable indicates nothing was provided by the card.

    /// <summary>
    /// Reads a byte from the card's I/O address space ($C0x0-$C0xF).
    /// </summary>
    /// <param name="offset">The offset within the 16-byte I/O space (valid range: 0x00-0x0F).</param>
    /// <returns>
    /// The byte value if the card responds at this address, or <c>null</c> if the card
    /// does not implement this I/O address. A <c>null</c> return allows the system to
    /// return floating bus values.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The I/O space maps to addresses $C0x0-$C0xF where 'x' represents the slot number plus 8
    /// (slot 1 = $C090-$C09F, slot 2 = $C0A0-$C0AF, etc.). This space is typically used for
    /// hardware control registers, device status, and triggering operations.
    /// </para>
    /// <para>
    /// Common usage patterns:
    /// <list type="bullet">
    /// <item><description>Reading device status flags</description></item>
    /// <item><description>Receiving data from the peripheral</description></item>
    /// <item><description>Triggering actions via read-triggered soft switches</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public byte? ReadIO(byte offset);

    /// <summary>
    /// Writes a byte to the card's I/O address space ($C0x0-$C0xF).
    /// </summary>
    /// <param name="offset">The offset within the 16-byte I/O space (valid range: 0x00-0x0F).</param>
    /// <param name="value">The byte value to write to the card.</param>
    /// <remarks>
    /// <para>
    /// The I/O space is used for sending commands and data to the peripheral device.
    /// Common operations include setting control flags, sending data bytes, and
    /// triggering hardware operations.
    /// </para>
    /// <para>
    /// If the card does not implement a particular I/O address, this method should
    /// perform no operation (no-op) rather than throwing an exception.
    /// </para>
    /// </remarks>
    public void WriteIO(byte offset, byte value);

    /// <summary>
    /// Reads a byte from the card's ROM address space ($Cx00-$CxFF).
    /// </summary>
    /// <param name="offset">The offset within the 256-byte ROM space (valid range: 0x00-0xFF).</param>
    /// <returns>
    /// The ROM byte if the card provides firmware at this address, or <c>null</c> if the
    /// card does not implement ROM or does not respond at this specific offset. A <c>null</c>
    /// return allows the system to fall back to internal ROM.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The card ROM space maps to $Cx00-$CxFF where 'x' is the slot number (1-7).
    /// This space typically contains:
    /// <list type="bullet">
    /// <item><description>Boot ROM code that the system executes during slot scanning</description></item>
    /// <item><description>Driver routines for the peripheral</description></item>
    /// <item><description>Interface tables and vectors</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The first few bytes ($Cx00-$Cx0F) are special:
    /// <list type="bullet">
    /// <item><description>$Cx01: Must be $20, $00, $03, or $3C to be recognized during boot scan</description></item>
    /// <item><description>$Cx03, $Cx05, $Cx07: May contain entry point code</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public byte? ReadRom(byte offset);

    /// <summary>
    /// Writes a byte to the card's ROM address space ($Cx00-$CxFF).
    /// </summary>
    /// <param name="offset">The offset within the 256-byte ROM space (valid range: 0x00-0xFF).</param>
    /// <param name="value">The byte value to write.</param>
    /// <remarks>
    /// <para>
    /// In most cases, this is a no-op since card ROM is typically read-only firmware.
    /// However, some specialized cards may use this address space for writeable RAM
    /// or configuration registers.
    /// </para>
    /// <para>
    /// If the card does not support writes to ROM space, this method should perform
    /// no operation rather than throwing an exception.
    /// </para>
    /// </remarks>
    public void WriteRom(byte offset, byte value);

    /// <summary>
    /// Reads a byte from the card's extended ROM address space ($C800-$CFFF).
    /// </summary>
    /// <param name="offset">The offset within the 2KB extended ROM space (valid range: 0x0000-0x07FF / 0-2047).</param>
    /// <returns>
    /// The ROM byte if the card provides extended firmware at this address, or <c>null</c>
    /// if the card does not implement extended ROM. A <c>null</c> return allows the
    /// system to return floating bus values.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The extended ROM space ($C800-$CFFF) is a 2KB region shared among all slots
    /// via bank switching. Only one card's extended ROM can be active at a time.
    /// The active card is determined by which slot last accessed address $CFFF.
    /// </para>
    /// <para>
    /// Extended ROM is typically used for:
    /// <list type="bullet">
    /// <item><description>Additional driver code that doesn't fit in the 256-byte card ROM</description></item>
    /// <item><description>Enhanced features and utilities</description></item>
    /// <item><description>Configuration and diagnostic code</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The <see cref="ISlots"/> implementation manages the bank selection mechanism,
    /// so individual cards only need to return their ROM data when queried.
    /// </para>
    /// </remarks>
    public byte? ReadExtendedRom(ushort offset);

    /// <summary>
    /// Writes a byte to the card's extended ROM address space ($C800-$CFFF).
    /// </summary>
    /// <param name="offset">The offset within the 2KB extended ROM space (valid range: 0x0000-0x07FF / 0-2047).</param>
    /// <param name="value">The byte value to write.</param>
    /// <remarks>
    /// <para>
    /// Like <see cref="WriteRom"/>, this is typically a no-op since extended ROM is
    /// usually read-only firmware. However, some cards may use this address range
    /// for writeable RAM or memory-mapped hardware.
    /// </para>
    /// <para>
    /// If the card does not support writes to extended ROM space, this method should
    /// perform no operation rather than throwing an exception.
    /// </para>
    /// </remarks>
    public void WriteExtendedRom(ushort offset, byte value);

    /// <summary>
    /// Creates a new independent copy of this card instance.
    /// </summary>
    /// <returns>A new <see cref="ICard"/> instance with the same type and configuration as this card.</returns>
    /// <remarks>
    /// <para>
    /// The <see cref="ICardFactory"/> maintains a single prototype instance of each card type.
    /// When a card is requested (via <see cref="ICardFactory.GetCardWithId"/> or
    /// <see cref="ICardFactory.GetCardWithName"/>), the factory calls <see cref="Clone"/>
    /// to create a fresh, independent instance for the requesting slot.
    /// </para>
    /// <para>
    /// This ensures that:
    /// <list type="bullet">
    /// <item><description>Each slot gets its own card instance with independent state</description></item>
    /// <item><description>Multiple slots can use the same card type without interference</description></item>
    /// <item><description>Removing a card from one slot doesn't affect cards in other slots</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Implementation should typically return a new instance via constructor:
    /// <code>
    /// public ICard Clone() => new MyCard();
    /// </code>
    /// If the card has configurable state, that state may need to be copied to the clone
    /// depending on the desired behavior.
    /// </para>
    /// </remarks>
    ICard Clone();


    /// <summary>
    /// Called when the card is installed into a slot, allowing runtime initialization.
    /// </summary>
    /// <param name="slot">The slot number where the card is being installed.</param>
    /// <remarks>
    /// <para>
    /// This method is invoked by <see cref="ISlots.InstallCard(int, SlotNumber)"/> or
    /// <see cref="ISlots.InstallCard(string, SlotNumber)"/> after the card has been
    /// created by the factory but before it becomes accessible to the system.
    /// </para>
    /// <para>
    /// <strong>Use Cases:</strong>
    /// <list type="bullet">
    /// <item><description>Creating child components (drives, ports, etc.) with slot-specific names</description></item>
    /// <item><description>Initializing hardware state that depends on slot location</description></item>
    /// <item><description>Performing deferred construction to keep factory lightweight</description></item>
    /// <item><description>Setting up resources that require knowledge of the slot number</description></item>
    /// <item><description>Configuring slot-specific I/O addresses or resources</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Design Rationale:</strong><br/>
    /// This callback allows cards to defer expensive initialization until they are actually
    /// installed into a slot. The factory can maintain lightweight prototype instances,
    /// and each cloned card can perform full initialization only when needed. The slot
    /// number parameter enables slot-aware initialization and diagnostics.
    /// </para>
    /// <para>
    /// For cards with no initialization requirements (like <see cref="NullCard"/>),
    /// this method can be implemented as an empty no-op.
    /// </para>
    /// </remarks>
    void OnInstalled(SlotNumber slot);


    /// <summary>
    /// Resets the card to its power-on or initial state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Reset Behavior:</strong> Called when the Apple IIe system is reset (power cycle
    /// or Ctrl+Reset). Each card should restore its internal state to match what would happen
    /// on a real Apple IIe hardware reset:
    /// <list type="bullet">
    /// <item>Clear pending I/O operations</item>
    /// <item>Reset hardware registers to default values</item>
    /// <item>Stop motors or other active devices</item>
    /// <item>Clear buffers and queues</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Disk Controller Example:</strong> A disk controller should stop any drive motors,
    /// clear the current track/sector state, but preserve the mounted disk images.
    /// </para>
    /// <para>
    /// <strong>Serial Card Example:</strong> A serial card should reset baud rate and control
    /// registers to defaults, clear transmit/receive buffers, but maintain connection state.
    /// </para>
    /// <para>
    /// For cards with no state to reset (like <see cref="NullCard"/>), this method can be
    /// implemented as an empty no-op.
    /// </para>
    /// </remarks>
    public void Reset();

    /// <summary>
    /// Handles a message sent to this card.
    /// </summary>
    /// <param name="message">The message to process.</param>
    /// <exception cref="Exceptions.CardMessageException">
    /// Thrown if the message is not recognized or cannot be processed.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <strong>Required Messages:</strong> All cards must handle <see cref="Messages.IdentifyCardMessage"/>
    /// by emitting a <see cref="Messages.CardIdentityPayload"/> response via <see cref="ICardResponseEmitter"/>.
    /// This includes NullCard (empty slots), which responds with CardId=0 and CardName="Empty Slot".
    /// </para>
    /// <para>
    /// <strong>Thread Context:</strong> Always called on the emulator thread at an
    /// instruction boundary. Implementations do not need to worry about thread safety
    /// relative to other emulator operations.
    /// </para>
    /// <para>
    /// <strong>Unrecognized Messages:</strong> Cards should throw <see cref="Exceptions.CardMessageException"/>
    /// for messages they don't recognize. However, during broadcast operations (slot=null),
    /// these exceptions are caught and ignored.
    /// </para>
    /// </remarks>
    void HandleMessage(ICardMessage message);

}





