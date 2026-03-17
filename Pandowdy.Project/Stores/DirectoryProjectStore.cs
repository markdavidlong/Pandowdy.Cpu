// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Text.Json;
using System.Text.Json.Serialization;
using Pandowdy.Project.Constants;
using Pandowdy.Project.Interfaces;
using Pandowdy.Project.Models;

namespace Pandowdy.Project.Stores;

/// <summary>
/// File-system store backed by a directory of flat files.
/// Layout:
/// <code>
///   {name}_skilletdir/
///     manifest.json
///     disks/
///       {id}_v0.pidi       ← immutable original
///       {id}_v1.pidi       ← first working version
///       {id}_v{n}.pidi
///     attachments/
///       {id}.dat
/// </code>
/// This implementation is stateless: every call reads from or writes to disk.
/// <see cref="Dispose"/> is a no-op.
/// </summary>
public sealed class DirectoryProjectStore(string path) : IProjectStore
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <inheritdoc/>
    public string Path { get; } = path ?? throw new ArgumentNullException(nameof(path));

    private string ManifestPath  => System.IO.Path.Combine(Path, "manifest.json");
    private string DisksPath     => System.IO.Path.Combine(Path, "disks");
    private string AttachmentsPath => System.IO.Path.Combine(Path, "attachments");

    private string BlobFilePath(long id, int version) =>
        System.IO.Path.Combine(DisksPath, $"{id}_v{version}.pidi");

    private string AttachmentFilePath(long id) =>
        System.IO.Path.Combine(AttachmentsPath, $"{id}.dat");

    // ─── IDisposable ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void Dispose() { }

    // ─── Manifest ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public ProjectManifest LoadManifest()
    {
        if (!File.Exists(ManifestPath))
        {
            throw new InvalidOperationException(
                $"manifest.json not found in '{Path}'.");
        }

        ProjectManifest manifest;
        try
        {
            var json = File.ReadAllText(ManifestPath);
            manifest = JsonSerializer.Deserialize<ProjectManifest>(json, _jsonOptions)
                       ?? throw new InvalidOperationException("manifest.json deserialised to null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"manifest.json in '{Path}' is malformed: {ex.Message}", ex);
        }

        if (manifest.Metadata.SchemaVersion != ManifestConstants.CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported manifest schema version {manifest.Metadata.SchemaVersion}. " +
                $"Expected {ManifestConstants.CurrentSchemaVersion}.");
        }

        if (HealVersionGaps(manifest))
        {
            WriteManifest(manifest);
        }

        return manifest;
    }

    // ─── Blob access ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public byte[]? LoadBlob(long diskImageId, int version = BlobVersion.Latest)
    {
        int resolvedVersion = version == BlobVersion.Latest
            ? ResolveLatestVersion(diskImageId)
            : version;

        if (resolvedVersion < 0)
        {
            // No blob files at all for this disk image.
            return null;
        }

        var filePath = BlobFilePath(diskImageId, resolvedVersion);

        if (!File.Exists(filePath))
        {
            if (version == BlobVersion.Latest)
            {
                // ResolveLatestVersion returned a version but the file is gone — corruption.
                throw new FileNotFoundException(
                    $"Blob file '{filePath}' is missing. The store may be corrupted.",
                    filePath);
            }

            // A specific version the caller asked for simply doesn't exist on disk.
            return null;
        }

        return File.ReadAllBytes(filePath);
    }

    // ─── Full save ────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void Save(ProjectSnapshot snapshot)
    {
        Directory.CreateDirectory(DisksPath);
        Directory.CreateDirectory(AttachmentsPath);

        // Build and persist the manifest first so orphan cleanup has fresh metadata.
        var manifest = SnapshotToManifest(snapshot);
        WriteManifest(manifest);

        // Write resident blobs.
        foreach (var (key, data) in snapshot.Blobs)
        {
            File.WriteAllBytes(BlobFilePath(key.Id, key.Version), data);
        }

        // Write resident attachment data.
        foreach (var (id, data) in snapshot.AttachmentData)
        {
            File.WriteAllBytes(AttachmentFilePath(id), data);
        }

        // Remove any orphaned files that are no longer referenced.
        CleanupOrphanBlobs(snapshot.DiskImages);
        CleanupOrphanAttachments(snapshot.Attachments);
    }

    // ─── Blob save ────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void SaveBlob(long diskImageId, byte[] data,
                         BlobSaveMode mode = BlobSaveMode.OverwriteActive,
                         bool userConfirmed = false)
    {
        var manifest = LoadManifest();
        var disk = manifest.DiskImages.Find(d => d.Id == diskImageId)
                   ?? throw new InvalidOperationException(
                       $"Disk image {diskImageId} not found in manifest.");

        if (disk.SavePolicy == SavePolicy.PromptUser && !userConfirmed)
        {
            throw new InvalidOperationException(
                $"Disk image {diskImageId} has SavePolicy.PromptUser; " +
                "userConfirmed must be true.");
        }

        Directory.CreateDirectory(DisksPath);

        if (mode == BlobSaveMode.OverwriteActive)
        {
            if (disk.HighestWorkingVersion > 0)
            {
                // Overwrite the current highest working version.
                File.WriteAllBytes(BlobFilePath(diskImageId, disk.HighestWorkingVersion), data);
            }
            else
            {
                // No working versions yet — create v1. Never touch v0 (immutable original).
                File.WriteAllBytes(BlobFilePath(diskImageId, 1), data);
                disk.HighestWorkingVersion = 1;
                WriteManifest(manifest);
            }
        }
        else // CreateNewVersion
        {
            int newVersion = disk.HighestWorkingVersion + 1;
            File.WriteAllBytes(BlobFilePath(diskImageId, newVersion), data);
            disk.HighestWorkingVersion = newVersion;
            WriteManifest(manifest);
        }
    }

    // ─── Attachment CRUD ──────────────────────────────────────────────────

    /// <inheritdoc/>
    public byte[]? LoadAttachment(long attachmentId)
    {
        var filePath = AttachmentFilePath(attachmentId);
        return File.Exists(filePath) ? File.ReadAllBytes(filePath) : null;
    }

    /// <inheritdoc/>
    public void SaveAttachment(long attachmentId, byte[] data)
    {
        Directory.CreateDirectory(AttachmentsPath);
        File.WriteAllBytes(AttachmentFilePath(attachmentId), data);
    }

    /// <inheritdoc/>
    public void DeleteAttachment(long attachmentId)
    {
        var filePath = AttachmentFilePath(attachmentId);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    // ─── Private helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Returns the highest version number found on disk for the given disk image,
    /// or -1 if no blob files exist for it.
    /// </summary>
    private int ResolveLatestVersion(long diskImageId)
    {
        if (!Directory.Exists(DisksPath))
        {
            return -1;
        }

        var prefix = $"{diskImageId}_v";
        var highest = -1;

        foreach (var file in Directory.EnumerateFiles(DisksPath, $"{diskImageId}_v*.pidi"))
        {
            var filename = System.IO.Path.GetFileNameWithoutExtension(file);
            if (filename.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(filename.AsSpan(prefix.Length), out var v))
            {
                if (v > highest)
                {
                    highest = v;
                }
            }
        }

        return highest;
    }

    /// <summary>
    /// Checks each disk image record for gaps in the version sequence and renames
    /// files to close those gaps (e.g. v0, v2, v4 → v0, v1, v2).
    /// Updates <see cref="DiskImageRecord.HighestWorkingVersion"/> accordingly.
    /// </summary>
    /// <returns><c>true</c> if any files were renamed (the manifest must be rewritten).</returns>
    private bool HealVersionGaps(ProjectManifest manifest)
    {
        var healed = false;

        if (!Directory.Exists(DisksPath))
        {
            return false;
        }

        foreach (var disk in manifest.DiskImages)
        {
            // Collect all version numbers that actually exist on disk.
            var prefix = $"{disk.Id}_v";
            var versions = new List<int>();

            foreach (var file in Directory.EnumerateFiles(DisksPath, $"{disk.Id}_v*.pidi"))
            {
                var filename = System.IO.Path.GetFileNameWithoutExtension(file);
                if (filename.StartsWith(prefix, StringComparison.Ordinal)
                    && int.TryParse(filename.AsSpan(prefix.Length), out var v))
                {
                    versions.Add(v);
                }
            }

            if (versions.Count == 0)
            {
                continue;
            }

            versions.Sort();

            // Walk through actual versions; if expected sequence differs, rename via temp.
            var expectedNext = 0;
            var renames = new List<(string From, string To)>();

            foreach (var actualVersion in versions)
            {
                if (actualVersion != expectedNext)
                {
                    renames.Add((BlobFilePath(disk.Id, actualVersion),
                                 BlobFilePath(disk.Id, expectedNext)));
                    healed = true;
                }

                expectedNext++;
            }

            if (renames.Count > 0)
            {
                // Use temp names to avoid collision during renaming.
                var tempPaths = new List<(string Temp, string Final)>();

                foreach (var (from, to) in renames)
                {
                    var temp = from + ".healtemp";
                    File.Move(from, temp);
                    tempPaths.Add((temp, to));
                }

                foreach (var (temp, final) in tempPaths)
                {
                    File.Move(temp, final);
                }

                disk.HighestWorkingVersion = expectedNext - 1;
            }
            else if (versions.Count > 0)
            {
                disk.HighestWorkingVersion = versions[^1];
            }
        }

        return healed;
    }

    /// <summary>
    /// Serialises and writes <paramref name="manifest"/> to <c>manifest.json</c>,
    /// always stamping the disclaimer before serialisation.
    /// </summary>
    private void WriteManifest(ProjectManifest manifest)
    {
        manifest.Disclaimer = ManifestConstants.Disclaimer;
        var json = JsonSerializer.Serialize(manifest, _jsonOptions);
        File.WriteAllText(ManifestPath, json);
    }

    /// <summary>Converts a <see cref="ProjectSnapshot"/> to a <see cref="ProjectManifest"/>.</summary>
    private static ProjectManifest SnapshotToManifest(ProjectSnapshot snapshot) =>
        new()
        {
            Disclaimer = string.Empty,   // WriteManifest overwrites this
            Metadata = snapshot.Metadata,
            EmulatorOverrides = snapshot.EmulatorOverrides,
            DisplayOverrides = snapshot.DisplayOverrides,
            ProjectSettings = snapshot.ProjectSettings,
            DiskImages = snapshot.DiskImages,
            Attachments = snapshot.Attachments,
            MountConfigurations = snapshot.MountConfigurations,
            Notes = snapshot.Notes
        };

    /// <summary>
    /// Deletes any <c>.pidi</c> files in <c>disks/</c> that are not referenced by any
    /// disk image record's version range (0 … HighestWorkingVersion).
    /// </summary>
    private void CleanupOrphanBlobs(List<DiskImageRecord> diskImages)
    {
        if (!Directory.Exists(DisksPath))
        {
            return;
        }

        // Build the expected set.
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var disk in diskImages)
        {
            for (var v = 0; v <= disk.HighestWorkingVersion; v++)
            {
                expected.Add(System.IO.Path.GetFullPath(BlobFilePath(disk.Id, v)));
            }
        }

        // Delete anything not in the expected set.
        foreach (var file in Directory.EnumerateFiles(DisksPath, "*.pidi"))
        {
            if (!expected.Contains(System.IO.Path.GetFullPath(file)))
            {
                File.Delete(file);
            }
        }
    }

    /// <summary>
    /// Deletes any <c>.dat</c> files in <c>attachments/</c> whose IDs are not
    /// present in <paramref name="attachments"/>.
    /// </summary>
    private void CleanupOrphanAttachments(List<AttachmentRecord> attachments)
    {
        if (!Directory.Exists(AttachmentsPath))
        {
            return;
        }

        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var attachment in attachments)
        {
            expected.Add(System.IO.Path.GetFullPath(AttachmentFilePath(attachment.Id)));
        }

        foreach (var file in Directory.EnumerateFiles(AttachmentsPath, "*.dat"))
        {
            if (!expected.Contains(System.IO.Path.GetFullPath(file)))
            {
                File.Delete(file);
            }
        }
    }
}
