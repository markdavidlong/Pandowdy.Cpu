// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Slots;

namespace Pandowdy.EmuCore.DiskII.Messages;

/// <summary>
/// Message requesting a disk be ejected from a specific drive.
/// </summary>
/// <param name="DriveNumber">1-based drive number (1 or 2).</param>
/// <param name="DiscardChanges">
/// When true, the disk's dirty flag is cleared before returning it to the store,
/// causing <see cref="IDiskImageStore.ReturnAsync"/> to skip saving the working copy.
/// Used when the user explicitly chooses "Eject without saving".
/// </param>
public record EjectDiskMessage(int DriveNumber, bool DiscardChanges = false) : ICardMessage;
