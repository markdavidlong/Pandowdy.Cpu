// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.DiskII.Messages;

/// <summary>
/// Message requesting the disk images in Drive 1 and Drive 2 be swapped.
/// </summary>
public record SwapDrivesMessage() : ICardMessage;
