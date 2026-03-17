// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.Project.Models;

namespace Pandowdy.Project.Interfaces;

/// <summary>
/// A path-bound abstraction for reading and writing project data to a persistent store.
/// Each instance is tied to a specific storage location at creation time.
/// </summary>
/// <remarks>
/// <para>
/// The project model is storage-agnostic. Whether the backing store is a directory
/// of flat files, a SQLite database, a zip archive, or anything else is an
/// implementation detail hidden behind this interface.
/// </para>
/// <para>
/// The interface supports three access patterns:
/// <list type="bullet">
/// <item><strong>Bulk:</strong> <see cref="LoadManifest"/> and <see cref="Save"/> for
///   full project load/save (metadata, settings, mount config, disk records, attachment records).</item>
/// <item><strong>Blob-level:</strong> <see cref="LoadBlob"/> and <see cref="SaveBlob"/>
///   for on-demand loading and flushing of individual disk image blobs.</item>
/// <item><strong>Attachment-level:</strong> <see cref="LoadAttachment"/>,
///   <see cref="SaveAttachment"/>, and <see cref="DeleteAttachment"/> for
///   simple mutable non-disk file blobs.</item>
/// </list>
/// This separation allows the project to eagerly load the small metadata on Open,
/// then lazily load large disk blobs and attachments only when needed.
/// </para>
/// <para>
/// Implements <see cref="IDisposable"/> preemptively. <c>DirectoryProjectStore</c>
/// has no held resources and its <c>Dispose()</c> is a no-op, but future store
/// implementations (e.g., <c>SqliteProjectStore</c>) will need to release connections.
/// Adding it now avoids a breaking interface change later.
/// </para>
/// </remarks>
public interface IProjectStore : IDisposable
{
    /// <summary>
    /// The path this store is bound to (for display, dirty-tracking, etc.).
    /// </summary>
    string Path { get; }

    /// <summary>
    /// Reads project metadata, settings, disk records, and mount configuration.
    /// Does NOT load disk image blobs -- those are loaded on demand via <see cref="LoadBlob"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the store data is malformed or missing required elements.
    /// </exception>
    ProjectManifest LoadManifest();

    /// <summary>
    /// Loads a single disk image blob from the store.
    /// </summary>
    /// <param name="diskImageId">ID of the disk image.</param>
    /// <param name="version">
    /// The version to load. Use <see cref="BlobVersion.Original"/> (0) for the immutable
    /// import, <see cref="BlobVersion.Latest"/> (-1, default) for the most recent working
    /// version, or a specific version number (1, 2, ...).
    /// </param>
    /// <returns>The PIDI blob bytes, or null if the requested version does not exist.</returns>
    /// <exception cref="FileNotFoundException">
    /// Thrown if a blob file that should exist (per manifest metadata) is missing
    /// from the store. This indicates store corruption, not a non-existent version.
    /// </exception>
    byte[]? LoadBlob(long diskImageId, int version = BlobVersion.Latest);

    /// <summary>
    /// Writes all project data to the store. Blobs included in the snapshot
    /// are written; blobs not included (not resident) are left untouched.
    /// Before writing, reconciles the store's contents against the snapshot:
    /// blob files and attachment files that are not referenced by the snapshot's
    /// disk image records or attachment records are deleted (orphan cleanup).
    /// </summary>
    void Save(ProjectSnapshot snapshot);

    /// <summary>
    /// Writes a single disk image blob to the store.
    /// </summary>
    /// <param name="diskImageId">ID of the disk image.</param>
    /// <param name="data">The PIDI blob bytes.</param>
    /// <param name="mode">
    /// <see cref="BlobSaveMode.OverwriteActive"/> replaces the current latest working
    /// version (version 1+); <see cref="BlobSaveMode.CreateNewVersion"/> appends a new version.
    /// In normal operation, neither mode will overwrite version 0 (the immutable original).
    /// A forced v0 overwrite (e.g., re-import) is permitted but emits a warning to stderr.
    /// If no working versions exist yet, both modes create version 1.
    /// </param>
    /// <param name="userConfirmed">
    /// Required when the disk's <see cref="SavePolicy"/> is <see cref="SavePolicy.PromptUser"/>.
    /// Must be <c>true</c> to indicate the user has approved the save.
    /// Ignored for other save policies.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the disk's <see cref="SavePolicy"/> is
    /// <see cref="SavePolicy.PromptUser"/> and <paramref name="userConfirmed"/> is <c>false</c>.
    /// </exception>
    void SaveBlob(long diskImageId, byte[] data,
                  BlobSaveMode mode = BlobSaveMode.OverwriteActive,
                  bool userConfirmed = false);

    // ── Attachment CRUD (non-disk files) ──────────────────────────────────

    /// <summary>
    /// Loads a single attachment blob from the store.
    /// </summary>
    /// <param name="attachmentId">ID of the attachment.</param>
    /// <returns>The attachment bytes, or null if not found.</returns>
    byte[]? LoadAttachment(long attachmentId);

    /// <summary>
    /// Writes a single attachment blob to the store, overwriting any existing data.
    /// Attachments have no versioning -- this is a simple upsert.
    /// </summary>
    /// <param name="attachmentId">ID of the attachment.</param>
    /// <param name="data">The attachment bytes.</param>
    void SaveAttachment(long attachmentId, byte[] data);

    /// <summary>
    /// Deletes an attachment blob from the store.
    /// Does nothing if the attachment does not exist.
    /// </summary>
    /// <param name="attachmentId">ID of the attachment to delete.</param>
    void DeleteAttachment(long attachmentId);
}
