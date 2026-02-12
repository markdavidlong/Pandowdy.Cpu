// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.DiskII.Messages;

/// <summary>
/// Message requesting the internal disk image be exported (saved) to a user-chosen file.
/// Also updates the attached destination path for future Save operations.
/// </summary>
/// <param name="DriveNumber">1-based drive number (1 or 2).</param>
/// <param name="FilePath">Destination path for the exported disk image.</param>
public record SaveDiskAsMessage(int DriveNumber, string FilePath) : ICardMessage;
