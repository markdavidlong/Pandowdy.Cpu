// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.EmuCore.Slots;

/// <summary>
/// Subscribable stream of card responses.
/// </summary>
public interface ICardResponseProvider
{
    /// <summary>
    /// Observable stream of all card responses.
    /// Subscribe to receive responses from any card.
    /// </summary>
    IObservable<CardResponse> Responses { get; }
}
