// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Project.Models;

/// <summary>
/// Disk image metadata record from the disk_images table.
/// Blobs (original_blob, working_blob) are accessed via DiskBlobStore streaming APIs.
/// </summary>
public sealed class DiskImageRecord
{
    public long Id { get; init; }
    public required string Name { get; set; }
    public string? OriginalFilename { get; init; }
    public required string OriginalFormat { get; init; }
    public string? ImportSourcePath { get; init; }
    public DateTime ImportedUtc { get; init; }
    public int TrackCount { get; init; } = 35;
    public byte OptimalBitTiming { get; init; } = 32;
    public bool IsWriteProtected { get; set; }
    public bool PersistWorking { get; set; } = true;
    public string? Notes { get; set; }
    public bool WorkingDirty { get; set; }
    public DateTime CreatedUtc { get; init; }
    public DateTime ModifiedUtc { get; set; }
}
