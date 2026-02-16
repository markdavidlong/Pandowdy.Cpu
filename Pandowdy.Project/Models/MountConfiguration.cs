// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Project.Models;

/// <summary>
/// Mount configuration record (slot/drive to disk image mapping).
/// </summary>
public sealed record MountConfiguration(
    long Id,
    int Slot,
    int DriveNumber,
    long? DiskImageId,
    bool AutoMount);
