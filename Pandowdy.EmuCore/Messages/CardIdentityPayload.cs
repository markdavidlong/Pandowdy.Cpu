// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.Messages;

/// <summary>
/// Response payload for card identification requests.
/// All cards (including NullCard) respond with this payload.
/// </summary>
/// <param name="CardName">Human-readable card name (e.g., "Disk II Controller", "Empty Slot").</param>
public record CardIdentityPayload(string CardName) : ICardResponsePayload;
