// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

namespace Pandowdy.Project.Models;

/// <summary>
/// Metadata for a non-disk file attached to a project.
/// Simple mutable blobs -- no versioning, no immutable original.
/// </summary>
public sealed record AttachmentRecord
{
    public required long Id { get; init; }

    /// <summary>Display name (e.g., "HELLO.BAS", "README.txt").</summary>
    public required string Name { get; set; }

    /// <summary>MIME type or general category tag (e.g., "text/plain", "application/pdf").</summary>
    public string? ContentType { get; set; }

    /// <summary>Original filename if imported from an external file.</summary>
    public string? OriginalFilename { get; set; }

    /// <summary>Size in bytes of the attachment data.</summary>
    public long SizeBytes { get; set; }

    public DateTime CreatedUtc { get; init; }

    /// <summary>Free-form notes about this attachment.</summary>
    public string? Notes { get; set; }
}
