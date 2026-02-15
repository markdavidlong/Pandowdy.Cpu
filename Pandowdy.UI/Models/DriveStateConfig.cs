// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Collections.Generic;

namespace Pandowdy.UI.Models;

/// <summary>
/// Represents the state of a single disk drive.
/// </summary>
public class DriveStateEntry
{
    /// <summary>
    /// Slot number (0-7) of the disk controller card.
    /// </summary>
    public byte Slot { get; set; }

    /// <summary>
    /// Drive number (1 or 2) on the controller card.
    /// </summary>
    public byte DriveNumber { get; set; }

    /// <summary>
    /// Full path to the disk image file, or null if no disk is inserted.
    /// </summary>
    public string? DiskImagePath { get; set; }
}

/// <summary>
/// Configuration for all disk drive states.
/// </summary>
public class DriveStateConfig
{
    /// <summary>
    /// Configuration file format version.
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// List of drive state entries.
    /// </summary>
    public List<DriveStateEntry> Drives { get; set; } = [];
}
