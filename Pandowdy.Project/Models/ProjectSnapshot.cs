// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Project.Models;

/// <summary>
/// Complete snapshot of project state for persistence.
/// Passed to <see cref="Pandowdy.Project.Interfaces.IProjectStore.Save"/>. Includes all metadata plus
/// whatever disk image blobs and attachment data are currently resident in memory.
/// </summary>
public sealed class ProjectSnapshot
{
    public required ProjectMetadata Metadata { get; init; }

    public required Dictionary<string, string> EmulatorOverrides { get; init; }
    public required Dictionary<string, string> DisplayOverrides { get; init; }
    public required Dictionary<string, string> ProjectSettings { get; init; }

    public required List<DiskImageRecord> DiskImages { get; init; }

    /// <summary>
    /// Disk image blobs currently resident in memory. Only these are written on Save.
    /// Blobs not present here are already persisted in the store and left untouched.
    /// Keyed by (diskImageId, version). Version 0 = original; 1+ = working versions.
    /// </summary>
    public required Dictionary<(long Id, int Version), byte[]> Blobs { get; init; }

    public required List<AttachmentRecord> Attachments { get; init; }

    /// <summary>
    /// Attachment data currently resident in memory. Only these are written on Save.
    /// Keyed by attachmentId. Simple overwrite semantics (no versioning).
    /// </summary>
    public required Dictionary<long, byte[]> AttachmentData { get; init; }

    public required List<MountConfiguration> MountConfigurations { get; init; }

    /// <summary>
    /// User-editable project notes (text or Markdown). Always present; defaults to empty string.
    /// </summary>
    public required string Notes { get; init; }
}
