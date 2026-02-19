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
    string FilePath { get; }

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
    /// Gets a value indicating whether the project has unsaved changes.
    /// </summary>
    bool HasUnsavedChanges { get; }

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
    /// Regenerates the working copy from the original blob.
    /// </summary>
    Task RegenerateWorkingCopyAsync(long id);

    /// <summary>
    /// Opens the original blob for reading (streaming).
    /// </summary>
    Stream OpenOriginalBlobRead(long diskImageId);

    /// <summary>
    /// Opens the working blob for reading (streaming). Falls back to original if working is null.
    /// </summary>
    Stream OpenWorkingBlobRead(long diskImageId);

    /// <summary>
    /// Writes a working blob from a stream.
    /// </summary>
    Task WriteWorkingBlobAsync(long diskImageId, Stream data);

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
    /// Gets a setting value from a settings table.
    /// </summary>
    Task<string?> GetSettingAsync(string tableName, string key);

    /// <summary>
    /// Sets a setting value in a settings table.
    /// </summary>
    Task SetSettingAsync(string tableName, string key, string value);

    // Lifecycle

    /// <summary>
    /// Saves the project, optionally ejecting all mounted disks first to ensure working copies are serialized.
    /// </summary>
    /// <param name="ejectAllFirst">If true, sends EjectAllDisksMessage to ensure mounted disk changes are serialized before save.</param>
    /// <param name="emulatorCore">Optional emulator core interface for sending eject message. Required if ejectAllFirst is true.</param>
    Task SaveAsync(bool ejectAllFirst = true, Pandowdy.EmuCore.Interfaces.IEmulatorCoreInterface? emulatorCore = null);
}
