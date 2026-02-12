// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.DiskII.Messages;

/// <summary>
/// Message requesting a disk be ejected from a specific drive.
/// </summary>
/// <param name="DriveNumber">1-based drive number (1 or 2).</param>
public record EjectDiskMessage(int DriveNumber) : ICardMessage;
