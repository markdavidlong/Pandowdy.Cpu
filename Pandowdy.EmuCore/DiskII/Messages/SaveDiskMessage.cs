// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Slots;

namespace Pandowdy.EmuCore.DiskII.Messages;

/// <summary>
/// Message requesting the internal disk image be saved to its attached destination path.
/// </summary>
/// <param name="DriveNumber">1-based drive number (1 or 2).</param>
public record SaveDiskMessage(int DriveNumber) : ICardMessage;
