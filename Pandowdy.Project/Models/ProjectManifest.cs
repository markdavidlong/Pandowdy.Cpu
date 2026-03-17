// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Project.Models;

/// <summary>
/// Lightweight project data loaded eagerly on Open.
/// Contains metadata, settings, disk image records, attachment records,
/// and mount configuration, but NOT disk image blobs or attachment data.
/// </summary>
public sealed class ProjectManifest
{
    /// <summary>Copyright notice written on every save. Never read programmatically.</summary>
    public string Disclaimer { get; set; } = string.Empty;

    public required ProjectMetadata Metadata { get; init; }

    public required Dictionary<string, string> EmulatorOverrides { get; init; }
    public required Dictionary<string, string> DisplayOverrides { get; init; }
    public required Dictionary<string, string> ProjectSettings { get; init; }

    public required List<DiskImageRecord> DiskImages { get; init; }

    public required List<AttachmentRecord> Attachments { get; init; }

    public required List<MountConfiguration> MountConfigurations { get; init; }

    /// <summary>
    /// User-editable project notes (text or Markdown). Always present; defaults to empty string.
    /// </summary>
    public required string Notes { get; init; }
}
