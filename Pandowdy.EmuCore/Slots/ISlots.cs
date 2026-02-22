// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Memory;

namespace Pandowdy.EmuCore.Slots;

/// <summary>
/// Defines the slot numbers for Apple IIe expansion cards.
/// </summary>
/// <remarks>
/// The Apple IIe provides seven expansion slots numbered 1 through 7. Slot 0 is
/// reserved for system use and is not accessible to expansion cards. Each slot
/// provides access to three address spaces: I/O ($C0x0-$C0xF), ROM ($Cx00-$CxFF),
/// and shared extended ROM ($C800-$CFFF).
/// </remarks>
public enum SlotNumber
{
    /// <summary>Card is not installed in any slot (prototype or unassigned state).</summary>
    Unslotted,
    /// <summary>Expansion slot 1.</summary>
    Slot1,
    /// <summary>Expansion slot 2.</summary>
    Slot2,
    /// <summary>Expansion slot 3 (typically 80-column card).</summary>
    Slot3,
    /// <summary>Expansion slot 4.</summary>
    Slot4,
    /// <summary>Expansion slot 5.</summary>
    Slot5,
    /// <summary>Expansion slot 6 (typically disk controller).</summary>
    Slot6,
    /// <summary>Expansion slot 7.</summary>
    Slot7
}

/// <summary>
/// Manages Apple IIe expansion slots and coordinates peripheral card I/O and ROM access.
/// </summary>
/// <seealso cref="IPandowdyMemory"/>
/// <seealso cref="IConfigurable"/>
/// <seealso cref="ICard"/>
/// <seealso cref="ICardFactory"/>
public interface ISlots : IPandowdyMemory, IConfigurable
{
   
    /// <summary>
    /// Installs a peripheral card in the specified slot using its numeric card ID.
    /// </summary>
    /// <param name="id">The unique numeric identifier of the card type to install (see <see cref="ICard.Id"/>).</param>
    /// <param name="slot">The slot number (1-7) where the card should be installed.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the card factory cannot create a card with the specified ID.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This method uses the <see cref="ICardFactory"/> to create a new instance of the
    /// specified card type via <see cref="ICardFactory.GetCardWithId"/>. The card is
    /// cloned from a registered prototype, ensuring each slot gets an independent instance.
    /// </para>
    /// <para>
    /// If a card is already installed in the specified slot, it is replaced by the new card.
    /// No state from the previous card is preserved.
    /// </para>
    /// <para>
    /// Common card IDs:
    /// <list type="bullet">
    /// <item><description>0 = NullCard (empty slot)</description></item>
    /// <item><description>1 = Disk II Controller</description></item>
    /// <item><description>2 = Super Serial Card</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <seealso cref="InstallCard(string, SlotNumber)"/>
    /// <seealso cref="RemoveCard"/>
    public void InstallCard(int id, SlotNumber slot);

    /// <summary>
    /// Installs a peripheral card in the specified slot using its human-readable name.
    /// </summary>
    /// <param name="name">The name of the card type to install (see <see cref="ICard.Name"/>).</param>
    /// <param name="slot">The slot number (1-7) where the card should be installed.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the card factory cannot create a card with the specified name.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This method provides a more user-friendly alternative to <see cref="InstallCard(int, SlotNumber)"/>
    /// by accepting the card's display name instead of its numeric ID. The card name must exactly
    /// match the value returned by <see cref="ICard.Name"/> (case-sensitive).
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// slots.InstallCard("Disk II Controller", SlotNumber.Slot6);
    /// slots.InstallCard("Super Serial Card", SlotNumber.Slot2);
    /// </code>
    /// </para>
    /// <para>
    /// If a card is already installed in the specified slot, it is replaced by the new card.
    /// </para>
    /// </remarks>
    /// <seealso cref="InstallCard(int, SlotNumber)"/>
    /// <seealso cref="RemoveCard"/>
    public void InstallCard(string name, SlotNumber slot);

    /// <summary>
    /// Removes the card from the specified slot, replacing it with an empty slot (NullCard).
    /// </summary>
    /// <param name="slot">The slot number (Slot1–Slot7) to clear.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the card factory cannot create a NullCard to fill the vacated slot.
    /// </exception>
    /// <seealso cref="InstallCard(int, SlotNumber)"/>
    /// <seealso cref="InstallCard(string, SlotNumber)"/>
    public void RemoveCard(SlotNumber slot);

    /// <summary>
    /// Retrieves the card currently installed in the specified slot.
    /// </summary>
    /// <param name="slot">The slot number (1-7) to query.</param>
    /// <returns>
    /// The <see cref="ICard"/> instance installed in the slot. Returns a <see cref="ICardFactory.GetNullCard"/>
    /// instance if the slot is empty.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method provides direct access to the card instance for inspection, debugging,
    /// or advanced operations. Normal emulator operation doesn't require calling this method
    /// since <see cref="IPandowdyMemory.Read"/> and <see cref="IPandowdyMemory.Write"/> automatically route
    /// to the appropriate card based on address.
    /// </para>
    /// <para>
    /// <strong>Usage Examples:</strong>
    /// <code>
    /// // Check what's installed in slot 6
    /// ICard card = slots.GetCardIn(SlotNumber.Slot6);
    /// Console.WriteLine($"Slot 6: {card.Name}");
    /// 
    /// // Verify a slot is empty
    /// if (slots.GetCardIn(SlotNumber.Slot3).Id == 0) // NullCard.Id == 0
    /// {
    ///     Console.WriteLine("Slot 3 is empty");
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public ICard GetCardIn(SlotNumber slot);

    /// <summary>
    /// Determines whether the specified slot is empty (contains a NullCard).
    /// </summary>
    /// <param name="slot">The slot number (1-7) to check.</param>
    /// <returns>
    /// <c>true</c> if the slot contains a <see cref="ICardFactory.GetNullCard"/> (empty slot);
    /// <c>false</c> if the slot contains an actual peripheral card.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This is a convenience method that checks whether the card in the specified slot
    /// has an <see cref="ICard.Id"/> of 0 (the NullCard identifier). It provides a more
    /// readable alternative to manually checking the card ID.
    /// </para>
    /// <para>
    /// <strong>Usage Examples:</strong>
    /// <code>
    /// // Clear alternative to checking card ID
    /// if (slots.IsEmpty(SlotNumber.Slot6))
    /// {
    ///     slots.InstallCard("Disk II Controller", SlotNumber.Slot6);
    /// }
    /// 
    /// // List all installed cards
    /// for (int i = 1; i &lt;= 7; i++)
    /// {
    ///     var slot = (SlotNumber)(i - 1);
    ///     if (!slots.IsEmpty(slot))
    ///     {
    ///         Console.WriteLine($"Slot {i}: {slots.GetCardIn(slot).Name}");
    ///     }
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// This method is equivalent to:
    /// <code>
    /// bool IsEmpty(SlotNumber slot) => GetCardIn(slot).Id == 0;
    /// </code>
    /// </para>
    /// </remarks>
    /// <seealso cref="GetCardIn"/>
    /// <seealso cref="RemoveCard"/>
    public bool IsEmpty(SlotNumber slot);

    //** Inherited from IPandowdyMemory **
    //
    // The following members are inherited from IPandowdyMemory and handle the $C090-$CFFF address range:
    //
    // byte Read(ushort address)
    //   Reads a byte from the slots address space ($C090-$CFFF, offset by $C000).
    //   Handles card I/O, card ROM, and extended ROM based on soft switch settings.
    //   Returns floating bus values for empty slots or when cards return null.
    //
    // void Write(ushort address, byte value)
    //   Writes a byte to the slots address space ($C090-$CFFF, offset by $C000).
    //   Handles card I/O, card ROM, and extended ROM based on soft switch settings.
    //   Most writes are no-ops since ROM is read-only, but some cards may use RAM.
    //
    // Note: All addresses are offset by $C000. For example, to access $C600, pass 0x0600.


    /// <summary>
    /// Sends a reset to all attached cards.
    /// </summary>
    public void Reset();
}

