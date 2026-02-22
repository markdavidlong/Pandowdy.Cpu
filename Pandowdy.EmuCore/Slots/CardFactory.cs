// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details


using Pandowdy.EmuCore.Machine;
using Pandowdy.EmuCore.DiskII;

namespace Pandowdy.EmuCore.Slots;

/// <summary>
/// Factory for creating peripheral card instances from registered card prototypes.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Prototype Pattern:</strong> The factory maintains a collection of card prototypes.
/// When a card is requested, the prototype is cloned to create an independent instance
/// for the requesting slot.
/// </para>
/// <para>
/// <strong>Registration Validation:</strong> During construction, the factory validates that
/// no two registered cards share the same ID or name (case-insensitive for names). This
/// prevents ambiguity when retrieving cards by either identifier.
/// </para>
/// <para>
/// <strong>NullCard Requirement:</strong> The factory must always have a card with ID 0
/// (NullCard) registered, as this is used to represent empty slots. The Slots class
/// depends on this during initialization.
/// </para>
/// <para>
/// <strong>Special Dependency Injection (Phase 2a):</strong> Some cards require runtime-injected
/// dependencies that are not available at prototype creation time. For these cards (currently
/// DiskIIControllerCard descendants), the factory bypasses Clone() and creates instances directly,
/// injecting the required dependencies. This pattern allows cards to depend on services that
/// have shorter or different lifetimes than the factory itself (e.g., IDiskImageStore tied to
/// the current project).
/// </para>
/// <para>
/// <strong>Restart Lifecycle:</strong> Factory-created cards participate in cold boot via
/// <see cref="Slots.Restart"/>, which iterates all installed cards and calls
/// <see cref="IRestartable.Restart"/>. Cards do not need to be registered
/// individually in <see cref="RestartCollection"/>.
/// </para>
/// </remarks>
public class CardFactory(IEnumerable<ICard> cards, IDiskImageStore diskImageStore) : ICardFactory
{
    private readonly IEnumerable<ICard> _allCards = InitializeCards(cards);
    private readonly IDiskImageStore _diskImageStore = diskImageStore ?? throw new ArgumentNullException(nameof(diskImageStore));

    private static IEnumerable<ICard> InitializeCards(IEnumerable<ICard> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);

        // Convert to list to avoid multiple enumeration
        var cardList = cards.ToList();

        // Check for duplicate IDs
        var duplicateIds = cardList
            .GroupBy(card => card.Id)
            .Where(group => group.Count() > 1)
            .Select(group => new
            {
                Id = group.Key,
                Types = string.Join(", ", group.Select(c => c.GetType().Name))
            })
            .ToList();

        if (duplicateIds.Count > 0)
        {
            var errorMessage = string.Join(Environment.NewLine,
                duplicateIds.Select(dup =>
                    $"Card ID {dup.Id} is duplicated by: {dup.Types}"));
            throw new InvalidOperationException(
                $"Duplicate card IDs detected during registration:{Environment.NewLine}{errorMessage}");
        }

        // Check for duplicate names
        var duplicateNames = cardList
            .GroupBy(card => card.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => new
            {
                Name = group.Key,
                Types = string.Join(", ", group.Select(c => c.GetType().Name))
            })
            .ToList();

        if (duplicateNames.Count > 0)
        {
            var errorMessage = string.Join(Environment.NewLine,
                duplicateNames.Select(dup =>
                    $"Card name '{dup.Name}' is duplicated by: {dup.Types}"));
            throw new InvalidOperationException(
                $"Duplicate card names detected during registration:{Environment.NewLine}{errorMessage}");
        }

        return cardList;
    }

    /// <summary>
    /// Creates a card instance, injecting runtime dependencies for cards that require them.
    /// </summary>
    /// <param name="prototype">The prototype card to clone or recreate.</param>
    /// <returns>A new card instance with appropriate dependencies injected.</returns>
    /// <remarks>
    /// DiskIIControllerCard descendants require IDiskImageStore, which is injected here
    /// rather than using Clone(). This allows the store reference to come from the current
    /// project (which may change during the application lifetime).
    /// </remarks>
    private ICard CreateCardInstance(ICard prototype)
    {
        return prototype switch
        {
            DiskIIControllerCard diskCard => diskCard.CreateWithStore(_diskImageStore),
            _ => prototype.Clone()
        };
    }

    /// <inheritdoc />
    /// <remarks>
    /// Returns a cloned instance of the prototype card with the specified ID.
    /// The clone is independent and can be installed in any slot.
    /// </remarks>
    public ICard? GetCardWithId(int internalId)
    {
        var prototype = _allCards.FirstOrDefault(card => card.Id == internalId);
        return prototype != null ? CreateCardInstance(prototype) : null;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Name matching is case-sensitive. Returns a cloned instance of the prototype
    /// card with the matching name.
    /// </remarks>
    public ICard? GetCardWithName(string name)
    {
        var prototype = _allCards.FirstOrDefault(card => card.Name == name);
        return prototype != null ? CreateCardInstance(prototype) : null;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Convenience method that returns the card with ID 0 (NullCard).
    /// This card type must always be registered for proper slot initialization.
    /// </remarks>
    public ICard? GetNullCard()
    {
        return GetCardWithId(0);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Returns a list of (ID, Name) tuples for all registered card types,
    /// sorted by ID. Useful for building UI card selection menus.
    /// </remarks>
    public List<(int, string)> GetAllCardTypes()
    {
        return [.. _allCards
            .Select(card => (card.Id, card.Name))
            .OrderBy(tuple => tuple.Id)];
    }
}
