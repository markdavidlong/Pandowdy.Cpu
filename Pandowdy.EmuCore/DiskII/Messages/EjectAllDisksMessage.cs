// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Slots;

namespace Pandowdy.EmuCore.DiskII.Messages;

/// <summary>
/// Message requesting all drives eject their disks.
/// </summary>
/// <remarks>
/// <para>
/// Used during project close to ensure all <see cref="InternalDiskImage"/> objects
/// are returned to the store (via <see cref="IDiskImageStore.ReturnAsync"/>) before
/// the SQLite connection is torn down. This prevents data loss.
/// </para>
/// <para>
/// The controller processes this message by ejecting each mounted drive, calling
/// <see cref="IDiskImageStore.ReturnAsync"/> for each, then clearing all drives.
/// </para>
/// </remarks>
public record EjectAllDisksMessage() : ICardMessage;
