// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.Messages;

/// <summary>
/// Message requesting a card to identify itself via the card response channel.
/// </summary>
/// <remarks>
/// <para>
/// <strong>All cards must handle this message.</strong> Including NullCard, which responds
/// with CardId=0 and CardName="Empty Slot". This is the primary mechanism for the GUI
/// to discover what cards are installed in which slots.
/// </para>
/// <para>
/// When <see cref="IEmulatorCoreInterface.SendCardMessageAsync"/> is called with a null
/// slot, this message is broadcast to all 7 slots, causing each card to emit a
/// <see cref="CardIdentityPayload"/> response.
/// </para>
/// </remarks>
public record IdentifyCardMessage() : ICardMessage;
