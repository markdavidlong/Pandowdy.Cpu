// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.EmuCore.Slots;

/// <summary>
/// Factory for creating instances of peripheral cards from registered prototypes.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="ICardFactory"/> manages a registry of card prototypes and provides methods
/// to create independent instances of cards for installation in expansion slots. This design
/// allows multiple slots to use the same card type without sharing state.
/// </para>
/// <para>
/// <strong>Prototype Pattern:</strong><br/>
/// The factory maintains a single prototype instance of each card type. When a card is requested,
/// the factory calls the prototype's <see cref="ICard.Clone"/> method to create a fresh,
/// independent instance. This ensures that:
/// </para>
/// <list type="bullet">
/// <item><description>Each slot gets its own card instance with independent state</description></item>
/// <item><description>Multiple slots can use the same card type simultaneously</description></item>
/// <item><description>Removing a card from one slot doesn't affect other slots</description></item>
/// <item><description>Card initialization occurs once during factory setup</description></item>
/// </list>
/// <para>
/// <strong>Registration and Validation:</strong><br/>
/// Cards are registered during factory construction (typically via dependency injection).
/// The factory validates that:
/// </para>
/// <list type="bullet">
/// <item><description>No two cards share the same ID (<see cref="ICard.Id"/>)</description></item>
/// <item><description>No two cards share the same name (<see cref="ICard.Name"/>, case-insensitive)</description></item>
/// <item><description>A <see cref="NullCard"/> (ID = 0) is always registered</description></item>
/// </list>
/// <para>
/// Registration violations throw <see cref="InvalidOperationException"/> during construction,
/// preventing the emulator from starting with an invalid card configuration.
/// </para>
/// <para>
/// <strong>Retrieval Methods:</strong><br/>
/// The factory provides three ways to retrieve cards:
/// </para>
/// <list type="number">
/// <item><description><see cref="GetCardWithId"/> - Look up by numeric ID (fastest, used for serialization)</description></item>
/// <item><description><see cref="GetCardWithName"/> - Look up by display name (user-friendly, used for configuration)</description></item>
/// <item><description><see cref="GetNullCard"/> - Convenience method for empty slot placeholder (always succeeds)</description></item>
/// </list>
/// <para>
/// All methods return <c>null</c> if the requested card is not registered, allowing callers
/// to handle missing cards gracefully.
/// </para>
/// <para>
/// <strong>Thread Safety:</strong><br/>
/// Implementations should be thread-safe for read operations (card creation). Write operations
/// (card registration) occur only during factory construction. The prototype instances should
/// not be modified after registration.
/// </para>
/// </remarks>
/// <example>
/// <para>
/// <strong>Typical usage in dependency injection:</strong>
/// </para>
/// <code>
/// // Register card prototypes
/// services.AddSingleton&lt;ICardFactory&gt;(sp =&gt; new CardFactory(
///     new ICard []
///     {
///         new NullCard(),
///         new DiskIIController(),
///         new SuperSerialCard(),
///         new ThundercardController()
///     }
/// ));
/// 
/// // Use factory to install cards
/// var factory = serviceProvider.GetRequiredService&lt;ICardFactory&gt;();
/// var slots = new Slots(factory, rom, floatingBus, status);
/// 
/// slots.InstallCard("Disk II Controller", SlotNumber.Slot6);
/// slots.InstallCard(2, SlotNumber.Slot2); // By ID
/// </code>
/// </example>
/// <seealso cref="ICard"/>
/// <seealso cref="ISlots"/>
/// <seealso cref="NullCard"/>
public interface ICardFactory
{
    /// <summary>
    /// Retrieves a new instance of the <see cref="NullCard"/> placeholder.
    /// </summary>
    /// <returns>
    /// A new <see cref="NullCard"/> instance, or <c>null</c> if NullCard is not registered
    /// (which indicates a critical factory configuration error).
    /// </returns>
    /// <remarks>
    /// <para>
    /// This is a convenience method equivalent to <c>GetCardWithId(0)</c>. It provides
    /// a clear, semantic way to obtain the empty slot placeholder without hard-coding the
    /// NullCard ID.
    /// </para>
    /// <para>
    /// <strong>Critical Requirement:</strong><br/>
    /// This method must never return <c>null</c> in a properly configured emulator. The
    /// <see cref="NullCard"/> is required for:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Initializing all slots during <see cref="Slots"/> construction</description></item>
    /// <item><description>Replacing removed cards via <see cref="ISlots.RemoveCard"/></description></item>
    /// <item><description>Providing a safe fallback when card creation fails</description></item>
    /// </list>
    /// <para>
    /// Both <see cref="Slots"/> constructor and <see cref="ISlots.RemoveCard"/> throw
    /// <see cref="InvalidOperationException"/> if this method returns <c>null</c>.
    /// </para>
    /// </remarks>
    /// <seealso cref="GetCardWithId"/>
    /// <seealso cref="NullCard"/>
    public ICard? GetNullCard();

    /// <summary>
    /// Creates a new instance of a card by its numeric identifier.
    /// </summary>
    /// <param name="internalId">
    /// The unique numeric ID of the card type (see <see cref="ICard.Id"/>).
    /// Common values: 0 = NullCard, 1 = Disk II, 2 = Serial Card, etc.
    /// </param>
    /// <returns>
    /// A new independent instance of the requested card type via <see cref="ICard.Clone"/>,
    /// or <c>null</c> if no card with the specified ID is registered.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method is the fastest retrieval option since it uses direct numeric comparison.
    /// It's ideal for:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Deserializing saved configurations (IDs are stable)</description></item>
    /// <item><description>Programmatic card installation where IDs are known</description></item>
    /// <item><description>Performance-critical card lookups</description></item>
    /// </list>
    /// <para>
    /// <strong>Return Value:</strong><br/>
    /// The method returns <c>null</c> if the ID is not found, allowing callers to detect
    /// and handle missing cards. The <see cref="ISlots.InstallCard(int, SlotNumber)"/>
    /// method converts this <c>null</c> into an <see cref="InvalidOperationException"/>.
    /// </para>
    /// <para>
    /// Each call creates a new, independent instance via <see cref="ICard.Clone"/>, ensuring
    /// that slots don't share card state.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Install a Disk II controller (ID = 1) in slot 6
    /// slots.InstallCard(1, SlotNumber.Slot6);
    /// 
    /// // Check if a card type is available
    /// ICard? serialCard = factory.GetCardWithId(2);
    /// if (serialCard != null)
    /// {
    ///     // Serial card is available
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="GetCardWithName"/>
    /// <seealso cref="GetNullCard"/>
    public ICard? GetCardWithId(int internalId);

    /// <summary>
    /// Creates a new instance of a card by its human-readable name.
    /// </summary>
    /// <param name="name">
    /// The display name of the card type (see <see cref="ICard.Name"/>).
    /// Comparison is case-insensitive. Examples: "Disk II Controller", "Super Serial Card".
    /// </param>
    /// <returns>
    /// A new independent instance of the requested card type via <see cref="ICard.Clone"/>,
    /// or <c>null</c> if no card with the specified name is registered.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method provides a user-friendly way to create cards using their display names
    /// instead of numeric IDs. The name comparison is case-insensitive to accommodate
    /// user input and configuration files. It's ideal for:
    /// </para>
    /// <list type="bullet">
    /// <item><description>User configuration interfaces (card selection dialogs)</description></item>
    /// <item><description>Text-based configuration files (JSON, XML, INI)</description></item>
    /// <item><description>Command-line arguments and scripts</description></item>
    /// </list>
    /// <para>
    /// <strong>Return Value:</strong><br/>
    /// The method returns <c>null</c> if the name is not found (or doesn't match any
    /// registered card after case-insensitive comparison). The <see cref="ISlots.InstallCard(string, SlotNumber)"/>
    /// method converts this <c>null</c> into an <see cref="InvalidOperationException"/>.
    /// </para>
    /// <para>
    /// Each call creates a new, independent instance via <see cref="ICard.Clone"/>, ensuring
    /// that slots don't share card state.
    /// </para>
    /// <para>
    /// <strong>Name Uniqueness:</strong><br/>
    /// The factory validates during construction that no two cards share the same name
    /// (case-insensitive). This ensures that name-based lookup is unambiguous.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Install a Disk II controller by name
    /// slots.InstallCard("Disk II Controller", SlotNumber.Slot6);
    /// 
    /// // Case-insensitive lookup works
    /// slots.InstallCard("disk ii controller", SlotNumber.Slot5); // Also works
    /// 
    /// // Check if a card type is available
    /// ICard? serialCard = factory.GetCardWithName("Super Serial Card");
    /// if (serialCard != null)
    /// {
    ///     // Serial card is available
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="GetCardWithId"/>
    /// <seealso cref="GetNullCard"/>
    public ICard? GetCardWithName(string name);

    /// <summary>
    /// Retrieves a list of all registered card types with their IDs and names.
    /// </summary>
    /// <returns>
    /// A list of tuples containing (ID, Name) pairs for all registered cards, sorted by ID.
    /// Always includes at least the <see cref="NullCard"/> (0, "Empty Slot").
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method provides a catalog of all available card types, useful for:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Populating UI card selection dropdowns/lists</description></item>
    /// <item><description>Generating configuration documentation</description></item>
    /// <item><description>Debugging and diagnostic tools</description></item>
    /// <item><description>Validating configuration files against available cards</description></item>
    /// </list>
    /// <para>
    /// <strong>Ordering:</strong><br/>
    /// The list is sorted by card ID in ascending order, placing <see cref="NullCard"/> (ID = 0)
    /// first, followed by actual peripheral cards in ID order.
    /// </para>
    /// <para>
    /// <strong>List Content:</strong><br/>
    /// Each tuple contains:
    /// </para>
    /// <list type="bullet">
    /// <item><description><strong>Item1 (int):</strong> The card's unique ID from <see cref="ICard.Id"/></description></item>
    /// <item><description><strong>Item2 (string):</strong> The card's display name from <see cref="ICard.Name"/></description></item>
    /// </list>
    /// <para>
    /// The returned list is a new collection that can be freely modified without affecting
    /// the factory's internal state.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Display all available card types
    /// var cardTypes = factory.GetAllCardTypes();
    /// Console.WriteLine("Available cards:");
    /// foreach (var (id, name) in cardTypes)
    /// {
    ///     Console.WriteLine($"  [{id}] {name}");
    /// }
    /// // Output:
    /// //   [0] Empty Slot
    /// //   [1] Disk II Controller
    /// //   [2] Super Serial Card
    /// //   [3] Thundercard Controller
    /// 
    /// // Populate a UI dropdown
    /// cardListBox.ItemsSource = factory.GetAllCardTypes()
    ///     .Where(card =&gt; card.Item1 != 0) // Exclude NullCard
    ///     .Select(card =&gt; card.Item2);
    /// </code>
    /// </example>
    /// <seealso cref="GetCardWithId"/>
    /// <seealso cref="GetCardWithName"/>
    public List<(int Id, string Name)> GetAllCardTypes();
}
