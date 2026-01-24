
using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.Services;

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
/// </remarks>
public class CardFactory : ICardFactory
{
    private readonly IEnumerable<ICard> _allCards;

    public CardFactory(IEnumerable<ICard> cards)
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

            _allCards = cardList;
        }

            /// <inheritdoc />
            /// <remarks>
            /// Returns a cloned instance of the prototype card with the specified ID.
            /// The clone is independent and can be installed in any slot.
            /// </remarks>
            public ICard? GetCardWithId(int internalId)
            {
                return _allCards.FirstOrDefault(card => card.Id == internalId)?.Clone();
            }

            /// <inheritdoc />
            /// <remarks>
            /// Name matching is case-sensitive. Returns a cloned instance of the prototype
            /// card with the matching name.
            /// </remarks>
            public ICard? GetCardWithName(string name)
            {
                return _allCards.FirstOrDefault(card => card.Name == name)?.Clone();
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
