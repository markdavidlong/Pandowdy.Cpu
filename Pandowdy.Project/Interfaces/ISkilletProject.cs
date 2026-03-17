// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using Pandowdy.EmuCore.DiskII;
using Pandowdy.Project.Models;

namespace Pandowdy.Project.Interfaces;

/// <summary>
/// Represents an open .skillet project with read/write access.
/// </summary>
/// <remarks>
/// <para>
/// Extends <see cref="IDiskImageStore"/> to allow the controller card to check out
/// and return disk images during mount/eject operations.
/// </para>
/// </remarks>
public interface ISkilletProject : IDiskImageStore, IDisposable
{
    /// <summary>
    /// Gets the file path of the .skillet project.
    /// </summary>
    string? FilePath { get; }

    /// <summary>
    /// Gets a value indicating whether this is an ad hoc (in-memory) project.
    /// </summary>
    /// <remarks>
    /// Ad hoc projects have no file path and exist only in memory.
    /// They must be persisted via Save As before they can be saved normally.
    /// </remarks>
    bool IsAdHoc { get; }

    /// <summary>
    /// Gets the project metadata.
    /// </summary>
    ProjectMetadata Metadata { get; }

    /// <summary>
    /// Gets a value indicating whether the project has been modified since it was
    /// created, opened, or last saved.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Backed by a single <c>_projectDirty</c> flag set via the centralized
    /// <c>MarkDirty()</c> method whenever any mutation occurs (import, remove,
    /// settings change, mount configuration, eject auto-flush, working copy
    /// regeneration). Cleared by <see cref="SaveAsync"/>.
    /// </para>
    /// <para>
    /// This is distinct from the per-disk-image <c>working_dirty</c> SQL column,
    /// which tracks whether a specific disk image's blob has been modified via eject
    /// auto-flush. The SQL column is an internal persistence detail used by
    /// <see cref="SaveAsync"/> to manage which blobs to flush — it is not consulted
    /// by this property.
    /// </para>
    /// </remarks>
    bool HasUnsavedChanges { get; }

    /// <summary>
    /// Gets a value indicating whether the project's disk image library contains any images.
    /// </summary>
    /// <remarks>
    /// Used for menu enablement — Export Disk Image is disabled when the library is empty.
    /// This checks the <c>disk_images</c> table, not whether any disks are currently mounted.
    /// </remarks>
    bool HasDiskImages { get; }

    // Disk image management

    /// <summary>
    /// Imports a disk image from the filesystem into the project.
    /// </summary>
    Task<long> ImportDiskImageAsync(string filePath, string name);

    /// <summary>
    /// Gets a disk image record by ID.
    /// </summary>
    Task<DiskImageRecord> GetDiskImageAsync(long id);

    /// <summary>
    /// Gets all disk images in the project.
    /// </summary>
    Task<IReadOnlyList<DiskImageRecord>> GetAllDiskImagesAsync();

    /// <summary>
    /// Removes a disk image from the project.
    /// </summary>
    Task RemoveDiskImageAsync(long id);

    /// <summary>
    /// Opens the original blob for reading (streaming).
    /// </summary>
    Stream OpenOriginalBlobRead(long diskImageId);

    /// <summary>
    /// Opens the working blob for reading (streaming). Falls back to original if working is null.
    /// </summary>
    Stream OpenWorkingBlobRead(long diskImageId);

    /// <summary>
    /// Deserializes the working copy (or original if working is null) into an InternalDiskImage.
    /// </summary>
    Task<InternalDiskImage> LoadDiskImageAsync(long diskImageId);

    /// <summary>
    /// Serializes an InternalDiskImage and writes it to the working blob.
    /// </summary>
    Task SaveDiskImageAsync(long diskImageId, InternalDiskImage diskImage);

    // Mount configuration

    /// <summary>
    /// Gets the current mount configuration.
    /// </summary>
    Task<IReadOnlyList<MountConfiguration>> GetMountConfigurationAsync();

    /// <summary>
    /// Sets a mount assignment for a slot/drive.
    /// </summary>
    Task SetMountAsync(int slot, int driveNumber, long? diskImageId);

    // Settings

    /// <summary>
    /// Gets a setting value.
    /// </summary>
    Task<string?> GetSettingAsync(SettingsScope scope, string key);

    /// <summary>
    /// Sets a setting value.
    /// </summary>
    Task SetSettingAsync(SettingsScope scope, string key, string value);

    // Lifecycle

    /// <summary>
    /// Saves the project to a new store (Save As). Makes the new store the active store.
    /// All blobs are loaded into memory before writing so the new store is complete.
    /// </summary>
    Task SaveAsAsync(IProjectStore newStore);

    /// <summary>
    /// Saves the project, serializing any currently checked-out (mounted) disk images.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Checked-out disk images are serialized using a snapshot-under-lock strategy:
    /// <see cref="InternalDiskImage.SerializationLock"/> is held briefly (~1ms) to copy
    /// raw quarter-track data, then released. Deflate compression runs outside the lock
    /// with no contention against the emulator write path. Disks remain mounted
    /// throughout — no eject/remount cycle is needed.
    /// </para>
    /// </remarks>
    Task SaveAsync();
}
