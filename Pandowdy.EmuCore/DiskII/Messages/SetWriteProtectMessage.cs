// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.DiskII.Messages;

/// <summary>
/// Message requesting the write-protect state of a drive be changed.
/// </summary>
/// <param name="DriveNumber">1-based drive number (1 or 2).</param>
/// <param name="WriteProtected">True to enable write protection, false to disable.</param>
public record SetWriteProtectMessage(int DriveNumber, bool WriteProtected) : ICardMessage;
