// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.EmuCore.Slots;

/// <summary>
/// Response payload for device enumeration requests.
/// </summary>
/// <param name="Devices">List of peripheral type IDs for each device attached to this card.
/// The list length implicitly defines the device count. Empty list for NullCard.</param>
public record DeviceListPayload(IReadOnlyList<PeripheralType> Devices) : ICardResponsePayload;
