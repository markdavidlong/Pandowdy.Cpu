// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.EmuCore.Slots;

/// <summary>
/// Marker interface for messages that can be sent to expansion cards via
/// <see cref="Machine.IEmulatorCoreInterface.SendCardMessageAsync"/>.
/// </summary>
/// <remarks>
/// Each card type defines its own concrete message types. The card's
/// <see cref="ICard.HandleMessage"/> method is responsible for recognizing
/// and executing messages it supports, and rejecting those it does not.
/// </remarks>
public interface ICardMessage
{
}
