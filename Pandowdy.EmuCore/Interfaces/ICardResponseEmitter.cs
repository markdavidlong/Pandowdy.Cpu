// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.EmuCore.Interfaces;

/// <summary>
/// Allows cards to emit responses. Injected into cards that need to respond.
/// </summary>
public interface ICardResponseEmitter
{
    /// <summary>
    /// Emits a response through the card response channel.
    /// </summary>
    /// <param name="slot">The slot of the card emitting the response.</param>
    /// <param name="cardId">The card's unique ID.</param>
    /// <param name="payload">The response payload.</param>
    void Emit(SlotNumber slot, int cardId, ICardResponsePayload payload);
}
