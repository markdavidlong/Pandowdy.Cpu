// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.EmuCore.Slots;

/// <summary>
/// A response sent by a card through the card response channel.
/// </summary>
/// <param name="Slot">The slot containing the card that sent this response.</param>
/// <param name="CardId">The card's unique numeric ID (0 = NullCard/empty slot).</param>
/// <param name="Payload">The response-specific data.</param>
public record CardResponse(
    SlotNumber Slot,
    int CardId,
    ICardResponsePayload Payload);
