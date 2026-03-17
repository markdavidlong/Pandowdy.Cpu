// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Slots;

namespace Pandowdy.EmuCore.DiskII.Messages;

/// <summary>
/// Message requesting a blank (empty, formatted) disk be inserted into a specific drive.
/// </summary>
/// <param name="DriveNumber">1-based drive number (1 or 2).</param>
/// <param name="FilePath">Optional file path to associate with the blank disk (for later Save).
/// Empty string means in-memory only.</param>
public record InsertBlankDiskMessage(int DriveNumber, string FilePath = "") : ICardMessage;
