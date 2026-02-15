// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Reactive.Subjects;
using Pandowdy.EmuCore.Interfaces;
using Pandowdy.EmuCore.Messages;

namespace Pandowdy.EmuCore.Services;

/// <summary>
/// Implements both <see cref="ICardResponseProvider"/> and <see cref="ICardResponseEmitter"/>
/// to provide a card response channel for card-to-GUI communication.
/// </summary>
/// <remarks>
/// <para>
/// This service is intended for <strong>low-bandwidth, occasional messages</strong>
/// (e.g., card identification at startup, configuration changes). High-velocity data
/// (motor state, track positions, dirty flags) should flow through dedicated status
/// providers like <see cref="IDiskStatusProvider"/>, which have optimized snapshot
/// types and update throttling.
/// </para>
/// </remarks>
public class CardResponseChannel : ICardResponseProvider, ICardResponseEmitter
{
    private readonly Subject<CardResponse> _responseSubject = new();

    /// <inheritdoc />
    public IObservable<CardResponse> Responses => _responseSubject;

    /// <inheritdoc />
    public void Emit(SlotNumber slot, int cardId, ICardResponsePayload payload)
    {
        _responseSubject.OnNext(new CardResponse(slot, cardId, payload));
    }
}
