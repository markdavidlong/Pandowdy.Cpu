// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.Messages;

/// <summary>
/// Message requesting a card to enumerate its attached devices.
/// </summary>
/// <remarks>
/// <para>
/// <strong>All cards should handle this message.</strong> Cards respond via the card response
/// channel with a <see cref="DeviceListPayload"/> containing the number and types of devices.
/// </para>
/// <para>
/// For a Disk II controller, this returns 2 devices (both floppy drives). For a SmartPort
/// controller, this could return multiple devices of varying types. For NullCard, this
/// returns 0 devices.
/// </para>
/// </remarks>
public record EnumerateDevicesMessage() : ICardMessage;
