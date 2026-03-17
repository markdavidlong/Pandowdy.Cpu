// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System.Collections.Concurrent;
using Pandowdy.EmuCore.DiskII;
using Pandowdy.Project.Constants;
using Pandowdy.Project.Interfaces;
using Pandowdy.Project.Models;

namespace Pandowdy.Project.Services;

/// <summary>
/// Memory-resident implementation of <see cref="ISkilletProject"/>.
/// All project state lives in dictionaries, lists, and byte arrays.
/// A backing <see cref="IProjectStore"/> provides persistence when non-null.
/// Ad hoc projects (no store) hold everything in memory only.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Thread-safety model:</strong>
/// <list type="bullet">
/// <item><see cref="ConcurrentDictionary{TKey,TValue}"/> is used for settings, blobs,
///   attachment data, and checked-out images. These are read and modified from multiple
///   threads (emulator write path, UI thread) and need fine-grained concurrency.</item>
/// <item><c>_collectionLock</c> guards all <c>List&lt;T&gt;</c> collections
///   (<c>_diskImages</c>, <c>_mountConfigs</c>, <c>_attachments</c>) because
///   <c>List&lt;T&gt;</c> is not thread-safe and requires a coarse lock for mutation.</item>
/// <item><c>_projectDirty</c> is <c>volatile</c> so the UI thread reads the freshest
///   value without a full memory barrier.</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class SkilletProject : ISkilletProject
{
    // Thread-safety: coarse lock for all List<T> collections.
    private readonly object _collectionLock = new();

    private IProjectStore? _store;
    private ProjectMetadata _metadata;
    private string _projectNotes;

    // Volatile: UI thread reads this without needing a full memory barrier.
    private volatile bool _projectDirty;

    // Convenience copy read from _projectSettings["DefaultSavePolicy"] at construction.
    private SavePolicy _defaultSavePolicy;

    // Settings — ConcurrentDictionary because reads can race with writes from any thread.
    private readonly ConcurrentDictionary<string, string> _emulatorOverrides;
    private readonly ConcurrentDictionary<string, string> _displayOverrides;
    private readonly ConcurrentDictionary<string, string> _projectSettings;

    // Disk images
    private readonly List<DiskImageRecord> _diskImages;   // guarded by _collectionLock

    // Per-disk, per-version PIDI blobs. Key presence = resident in memory; absence = not loaded.
    // Outer key: diskImageId. Inner key: version number (0 = original, 1+ = working).
    private readonly ConcurrentDictionary<long, ConcurrentDictionary<int, byte[]>> _blobs = new();

    private long _nextDiskId;

    // Mount configurations
    private readonly List<MountConfiguration> _mountConfigs;  // guarded by _collectionLock
    private long _nextMountId;

    // Attachments
    private readonly List<AttachmentRecord> _attachments;     // guarded by _collectionLock
    private readonly ConcurrentDictionary<long, byte[]> _attachmentData = new();
    private long _nextAttachmentId;

    // Images currently checked out to the emulator.
    private readonly ConcurrentDictionary<long, InternalDiskImage> _checkedOutImages = new();

    // ─── Private constructor ──────────────────────────────────────────────

    private SkilletProject(
        IProjectStore? store,
        ProjectMetadata metadata,
        string projectNotes,
        ConcurrentDictionary<string, string> emulatorOverrides,
        ConcurrentDictionary<string, string> displayOverrides,
        ConcurrentDictionary<string, string> projectSettings,
        List<DiskImageRecord> diskImages,
        List<MountConfiguration> mountConfigs,
        List<AttachmentRecord> attachments)
    {
        _store = store;
        _metadata = metadata;
        _projectNotes = projectNotes;
        _emulatorOverrides = emulatorOverrides;
        _displayOverrides = displayOverrides;
        _projectSettings = projectSettings;
        _diskImages = diskImages;
        _mountConfigs = mountConfigs;
        _attachments = attachments;

        _defaultSavePolicy = Enum.TryParse<SavePolicy>(
            _projectSettings.GetValueOrDefault("DefaultSavePolicy"), out var p)
            ? p : SavePolicy.OverwriteLatest;

        _nextDiskId = diskImages.Count > 0 ? diskImages.Max(d => d.Id) + 1 : 1;
        _nextMountId = mountConfigs.Count > 0 ? mountConfigs.Max(m => m.Id) + 1 : 1;
        _nextAttachmentId = attachments.Count > 0 ? attachments.Max(a => a.Id) + 1 : 1;
    }

    // ─── Static factories ─────────────────────────────────────────────────

    /// <summary>
    /// Creates a new ad hoc (in-memory only) project with no backing store.
    /// </summary>
    public static SkilletProject CreateNew(string name) =>
        new(store: null,
            metadata: BuildDefaultMetadata(name),
            projectNotes: string.Empty,
            emulatorOverrides: new ConcurrentDictionary<string, string>(),
            displayOverrides: new ConcurrentDictionary<string, string>(),
            projectSettings: BuildDefaultProjectSettings(),
            diskImages: new List<DiskImageRecord>(),
            mountConfigs: new List<MountConfiguration>(),
            attachments: new List<AttachmentRecord>());

    /// <summary>
    /// Creates a new store-backed project. Caller is responsible for calling
    /// <see cref="IProjectStore.Save"/> with <see cref="ToSnapshot()"/> afterwards.
    /// </summary>
    public static SkilletProject CreateNew(string name, IProjectStore store) =>
        new(store: store,
            metadata: BuildDefaultMetadata(name),
            projectNotes: string.Empty,
            emulatorOverrides: new ConcurrentDictionary<string, string>(),
            displayOverrides: new ConcurrentDictionary<string, string>(),
            projectSettings: BuildDefaultProjectSettings(),
            diskImages: new List<DiskImageRecord>(),
            mountConfigs: new List<MountConfiguration>(),
            attachments: new List<AttachmentRecord>());

    /// <summary>
    /// Constructs a store-backed project from an already-loaded manifest.
    /// Throws <see cref="InvalidOperationException"/> on corrupt data (duplicate IDs,
    /// negative IDs, mount configs referencing non-existent disk images).
    /// Blobs are NOT loaded here — they are lazy-loaded on first access.
    /// </summary>
    public static SkilletProject FromManifest(ProjectManifest manifest, IProjectStore store)
    {
        // Fail fast on corrupt manifest data.
        var diskIds = manifest.DiskImages.Select(d => d.Id).ToList();

        if (diskIds.Any(id => id < 0))
        {
            throw new InvalidOperationException("Manifest contains a disk image with a negative ID.");
        }

        if (diskIds.Count != diskIds.Distinct().Count())
        {
            throw new InvalidOperationException("Manifest contains duplicate disk image IDs.");
        }

        var mountIds = manifest.MountConfigurations.Select(m => m.Id).ToList();

        if (mountIds.Count != mountIds.Distinct().Count())
        {
            throw new InvalidOperationException("Manifest contains duplicate mount configuration IDs.");
        }

        var diskIdSet = diskIds.ToHashSet();

        foreach (var mount in manifest.MountConfigurations)
        {
            if (mount.DiskImageId.HasValue && !diskIdSet.Contains(mount.DiskImageId.Value))
            {
                throw new InvalidOperationException(
                    $"Mount configuration references non-existent disk image ID {mount.DiskImageId.Value}.");
            }
        }

        return new SkilletProject(
            store: store,
            metadata: manifest.Metadata,
            projectNotes: manifest.Notes,
            emulatorOverrides: new ConcurrentDictionary<string, string>(manifest.EmulatorOverrides),
            displayOverrides: new ConcurrentDictionary<string, string>(manifest.DisplayOverrides),
            projectSettings: new ConcurrentDictionary<string, string>(manifest.ProjectSettings),
            diskImages: manifest.DiskImages.ToList(),
            mountConfigs: manifest.MountConfigurations.ToList(),
            attachments: manifest.Attachments.ToList());
    }

    /// <summary>
    /// Creates a <see cref="SkilletProject"/> pre-populated with test data.
    /// For use in unit tests only (internal; exposed via <c>InternalsVisibleTo</c>).
    /// </summary>
    internal static SkilletProject CreateForTest(
        ProjectManifest manifest,
        Dictionary<(long DiskId, int Version), byte[]>? blobs = null,
        Dictionary<long, byte[]>? attachmentData = null,
        IProjectStore? store = null)
    {
        // Reuse FromManifest for validation, but allow a null store for ad hoc tests.
        var project = store != null
            ? FromManifest(manifest, store)
            : new SkilletProject(
                store: null,
                metadata: manifest.Metadata,
                projectNotes: manifest.Notes,
                emulatorOverrides: new ConcurrentDictionary<string, string>(manifest.EmulatorOverrides),
                displayOverrides: new ConcurrentDictionary<string, string>(manifest.DisplayOverrides),
                projectSettings: new ConcurrentDictionary<string, string>(manifest.ProjectSettings),
                diskImages: manifest.DiskImages.ToList(),
                mountConfigs: manifest.MountConfigurations.ToList(),
                attachments: manifest.Attachments.ToList());

        if (blobs != null)
        {
            foreach (var ((diskId, version), data) in blobs)
            {
                project._blobs.GetOrAdd(diskId, _ => new ConcurrentDictionary<int, byte[]>())[version] = data;
            }
        }

        if (attachmentData != null)
        {
            foreach (var (id, data) in attachmentData)
            {
                project._attachmentData[id] = data;
            }
        }

        return project;
    }

    // ─── ISkilletProject properties ───────────────────────────────────────

    public string? FilePath => _store?.Path;
    public bool IsAdHoc => _store == null;
    public ProjectMetadata Metadata => _metadata;
    public bool HasUnsavedChanges => _projectDirty;

    public bool HasDiskImages
    {
        get
        {
            lock (_collectionLock)
            {
                return _diskImages.Count > 0;
            }
        }
    }

    // ─── Disk image management ────────────────────────────────────────────

    public Task<long> ImportDiskImageAsync(string filePath, string name)
    {
        return Task.Run(() =>
        {
            var format = DiskFormatHelper.GetFormatFromPath(filePath);

            if (format == DiskFormat.Unknown)
            {
                throw new ArgumentException(
                    $"Unsupported disk image format: {Path.GetExtension(filePath)}", nameof(filePath));
            }

            var importer = DiskImageFactory.GetImporter(format);
            var diskImage = importer.Import(filePath);
            var blob = DiskBlobStore.Serialize(diskImage);

            long newId;

            lock (_collectionLock)
            {
                newId = _nextDiskId++;
            }

            var record = new DiskImageRecord
            {
                Id = newId,
                Name = name,
                OriginalFilename = Path.GetFileName(filePath),
                OriginalFormat = format.ToString(),
                ImportSourcePath = filePath,
                ImportedUtc = DateTime.UtcNow,
                WholeTrackCount = diskImage.PhysicalTrackCount,
                OptimalBitTiming = diskImage.OptimalBitTiming,
                IsWriteProtected = diskImage.IsWriteProtected,
                SavePolicy = _defaultSavePolicy,
                HighestWorkingVersion = 0,
                Notes = null,
                CreatedUtc = DateTime.UtcNow
            };

            lock (_collectionLock)
            {
                _diskImages.Add(record);
            }

            // v0 is the immutable original.
            _blobs.GetOrAdd(newId, _ => new ConcurrentDictionary<int, byte[]>())[0] = blob;
            MarkDirty();
            return newId;
        });
    }

    public Task<DiskImageRecord> GetDiskImageAsync(long id)
    {
        lock (_collectionLock)
        {
            var record = _diskImages.Find(d => d.Id == id)
                         ?? throw new InvalidOperationException($"Disk image {id} not found.");
            return Task.FromResult(record);
        }
    }

    public Task<IReadOnlyList<DiskImageRecord>> GetAllDiskImagesAsync()
    {
        lock (_collectionLock)
        {
            return Task.FromResult<IReadOnlyList<DiskImageRecord>>(_diskImages.ToList());
        }
    }

    public Task RemoveDiskImageAsync(long id)
    {
        lock (_collectionLock)
        {
            var index = _diskImages.FindIndex(d => d.Id == id);

            if (index < 0)
            {
                throw new InvalidOperationException($"Disk image {id} not found.");
            }

            _diskImages.RemoveAt(index);
        }

        _blobs.TryRemove(id, out _);
        MarkDirty();
        return Task.CompletedTask;
    }

    public Stream OpenOriginalBlobRead(long diskImageId)
    {
        EnsureBlobResident(diskImageId, 0);
        return new MemoryStream(_blobs[diskImageId][0], writable: false);
    }

    public Stream OpenWorkingBlobRead(long diskImageId)
    {
        var latestVer = GetHighestWorkingVersion(diskImageId);
        EnsureBlobResident(diskImageId, latestVer);
        return new MemoryStream(_blobs[diskImageId][latestVer], writable: false);
    }

    public Task<InternalDiskImage> LoadDiskImageAsync(long diskImageId)
    {
        var latestVer = GetHighestWorkingVersion(diskImageId);
        EnsureBlobResident(diskImageId, latestVer);
        var blob = _blobs[diskImageId][latestVer];
        return Task.FromResult(DiskBlobStore.Deserialize(blob));
    }

    public Task SaveDiskImageAsync(long diskImageId, InternalDiskImage diskImage)
    {
        var blob = DiskBlobStore.Serialize(diskImage);

        DiskImageRecord disk;

        lock (_collectionLock)
        {
            disk = _diskImages.Find(d => d.Id == diskImageId)
                   ?? throw new InvalidOperationException($"Disk image {diskImageId} not found.");
        }

        int activeVersion = disk.HighestWorkingVersion > 0 ? disk.HighestWorkingVersion : 1;

        if (disk.HighestWorkingVersion == 0)
        {
            disk.HighestWorkingVersion = 1;
        }

        _blobs.GetOrAdd(diskImageId, _ => new ConcurrentDictionary<int, byte[]>())[activeVersion] = blob;
        MarkDirty();
        return Task.CompletedTask;
    }

    // ─── IDiskImageStore ──────────────────────────────────────────────────

    /// <inheritdoc/>
    /// <remarks>
    /// Loads the latest version blob lazily from the store if not already resident.
    /// Registers the image in <c>_checkedOutImages</c> so it is captured by
    /// <see cref="SnapshotCheckedOutDisks"/> during save.
    /// </remarks>
    public Task<InternalDiskImage> CheckOutAsync(long diskImageId)
    {
        if (_checkedOutImages.ContainsKey(diskImageId))
        {
            throw new InvalidOperationException(
                $"Disk image {diskImageId} is already checked out. Unmount it before checking out again.");
        }

        DiskImageRecord disk;

        lock (_collectionLock)
        {
            disk = _diskImages.Find(d => d.Id == diskImageId)
                   ?? throw new InvalidOperationException($"Disk image {diskImageId} not found.");
        }

        var latestVer = disk.HighestWorkingVersion;
        EnsureBlobResident(diskImageId, latestVer);

        var blob = _blobs[diskImageId][latestVer];
        var image = DiskBlobStore.Deserialize(blob);
        image.DiskImageName = disk.Name;

        _checkedOutImages[diskImageId] = image;
        return Task.FromResult(image);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Serializes the image to the active working version if it is dirty.
    /// Skips serialization entirely for <see cref="SavePolicy.DiscardChanges"/> disks —
    /// changes are silently dropped.
    /// This is an intentional behavioral change from the old SQLite implementation,
    /// which always wrote the blob regardless of policy on return.
    /// </remarks>
    public Task ReturnAsync(long diskImageId, InternalDiskImage image)
    {
        _checkedOutImages.TryRemove(diskImageId, out _);

        if (!image.IsDirty)
        {
            return Task.CompletedTask;
        }

        DiskImageRecord disk;

        lock (_collectionLock)
        {
            disk = _diskImages.Find(d => d.Id == diskImageId)
                   ?? throw new InvalidOperationException($"Disk image {diskImageId} not found.");
        }

        if (disk.SavePolicy == SavePolicy.DiscardChanges)
        {
            // Intentional: discard working changes per policy.
            return Task.CompletedTask;
        }

        int activeVersion = disk.HighestWorkingVersion > 0 ? disk.HighestWorkingVersion : 1;

        if (disk.HighestWorkingVersion == 0)
        {
            disk.HighestWorkingVersion = 1;
        }

        var blob = DiskBlobStore.Serialize(image);
        _blobs.GetOrAdd(diskImageId, _ => new ConcurrentDictionary<int, byte[]>())[activeVersion] = blob;
        MarkDirty();
        return Task.CompletedTask;
    }

    // ─── Mount configuration ──────────────────────────────────────────────

    public Task<IReadOnlyList<MountConfiguration>> GetMountConfigurationAsync()
    {
        lock (_collectionLock)
        {
            return Task.FromResult<IReadOnlyList<MountConfiguration>>(_mountConfigs.ToList());
        }
    }

    public Task SetMountAsync(int slot, int driveNumber, long? diskImageId)
    {
        lock (_collectionLock)
        {
            var index = _mountConfigs.FindIndex(m => m.Slot == slot && m.DriveNumber == driveNumber);

            if (index >= 0)
            {
                var existing = _mountConfigs[index];

                if (existing.DiskImageId == diskImageId)
                {
                    // No actual change — don't mark dirty.
                    return Task.CompletedTask;
                }

                // Replace in-place; preserve the existing Id.
                _mountConfigs[index] = existing with { DiskImageId = diskImageId };
            }
            else
            {
                var newId = _nextMountId++;
                _mountConfigs.Add(new MountConfiguration(
                    Id: newId,
                    Slot: slot,
                    DriveNumber: driveNumber,
                    DiskImageId: diskImageId,
                    AutoMount: true));

                if (!diskImageId.HasValue)
                {
                    // New slot registered but no disk assigned — not a meaningful dirty state.
                    return Task.CompletedTask;
                }
            }

            MarkDirty();
        }

        return Task.CompletedTask;
    }

    // ─── Settings ─────────────────────────────────────────────────────────

    public Task<string?> GetSettingAsync(SettingsScope scope, string key)
    {
        var dict = GetDictionary(scope);
        dict.TryGetValue(key, out var value);
        return Task.FromResult<string?>(value);
    }

    public Task SetSettingAsync(SettingsScope scope, string key, string value)
    {
        var dict = GetDictionary(scope);
        dict[key] = value;

        // Keep _defaultSavePolicy in sync when the project-level setting changes.
        if (scope == SettingsScope.ProjectSettings && key == "DefaultSavePolicy")
        {
            if (Enum.TryParse<SavePolicy>(value, out var policy))
            {
                _defaultSavePolicy = policy;
            }
        }

        MarkDirty();
        return Task.CompletedTask;
    }

    // ─── Lifecycle ────────────────────────────────────────────────────────

    /// <summary>
    /// Saves the project to its current backing store.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the project is ad hoc (no backing store). Use
    /// <see cref="SaveAsAsync"/> to persist an ad hoc project to a new store first.
    /// </exception>
    public async Task SaveAsync()
    {
        if (_store == null)
        {
            throw new InvalidOperationException(
                "Cannot save an ad hoc project. Use SaveAsAsync to save to a new location first.");
        }

        await Task.Run(() =>
        {
            SnapshotCheckedOutDisks();
            _store.Save(ToSnapshot());
            MarkClean();
        });
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Ensures all blobs are resident before writing, because the new store has no
    /// pre-existing blobs — a complete copy of the project is required.
    /// Swaps the active store after the write, disposing the old store.
    /// Derives the new project name from the store directory path (strips the
    /// <c>_skilletdir</c> suffix if present).
    /// </remarks>
    public async Task SaveAsAsync(IProjectStore newStore)
    {
        await Task.Run(() =>
        {
            // Derive project name from directory name first so ToSnapshot() captures it.
            var dirName = Path.GetFileName(
                newStore.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            const string suffix = "_skilletdir";
            var newName = dirName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                ? dirName[..^suffix.Length]
                : dirName;

            _metadata = _metadata with { Name = newName };

            EnsureAllBlobsResident();
            SnapshotCheckedOutDisks();
            newStore.Save(ToSnapshot());

            _store?.Dispose();
            _store = newStore;

            MarkClean();
        });
    }

    public void Dispose()
    {
        _store?.Dispose();
        _store = null;
    }

    // ─── Internal operations (future interface candidates) ────────────────

    /// <summary>
    /// Creates a new version snapshot of a currently checked-out disk image.
    /// The new version becomes the highest working version.
    /// Does NOT change the disk's <see cref="SavePolicy"/> — subsequent automatic
    /// saves continue to follow the configured policy.
    /// </summary>
    internal Task CreateSnapshotAsync(long diskImageId)
    {
        if (!_checkedOutImages.TryGetValue(diskImageId, out var image))
        {
            throw new InvalidOperationException(
                $"Disk image {diskImageId} is not checked out. It must be mounted to take a snapshot.");
        }

        DiskImageRecord disk;

        lock (_collectionLock)
        {
            disk = _diskImages.Find(d => d.Id == diskImageId)
                   ?? throw new InvalidOperationException($"Disk image {diskImageId} not found.");
        }

        var newVersion = disk.HighestWorkingVersion + 1;
        var blob = DiskBlobStore.SerializeSnapshot(image);
        _blobs.GetOrAdd(diskImageId, _ => new ConcurrentDictionary<int, byte[]>())[newVersion] = blob;
        disk.HighestWorkingVersion = newVersion;
        MarkDirty();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Clones the latest working blob of an unmounted disk into a new disk image entry.
    /// The original's current working state becomes the clone's v0 (original).
    /// </summary>
    /// <remarks>
    /// TODO: Review — CloneBlobAsNewDisk currently requires the source disk to be
    /// unmounted. A future enhancement could support live cloning via
    /// <see cref="DiskBlobStore.SerializeSnapshot"/> if the unmount prerequisite
    /// proves too disruptive to workflows.
    /// </remarks>
    internal Task<long> CloneBlobAsNewDiskAsync(long sourceDiskId, string newName,
                                                 SavePolicy? newSavePolicy = null)
    {
        if (_checkedOutImages.ContainsKey(sourceDiskId))
        {
            throw new InvalidOperationException(
                $"Disk image {sourceDiskId} must be unmounted before cloning. Eject it first.");
        }

        DiskImageRecord sourceDisk;

        lock (_collectionLock)
        {
            sourceDisk = _diskImages.Find(d => d.Id == sourceDiskId)
                         ?? throw new InvalidOperationException($"Disk image {sourceDiskId} not found.");
        }

        var latestVer = sourceDisk.HighestWorkingVersion;
        EnsureBlobResident(sourceDiskId, latestVer);
        var blob = _blobs[sourceDiskId][latestVer];

        long newId;

        lock (_collectionLock)
        {
            newId = _nextDiskId++;
        }

        var cloneRecord = new DiskImageRecord
        {
            Id = newId,
            Name = newName,
            OriginalFilename = sourceDisk.OriginalFilename,
            OriginalFormat = sourceDisk.OriginalFormat,
            ImportSourcePath = sourceDisk.ImportSourcePath,
            ImportedUtc = DateTime.UtcNow,
            WholeTrackCount = sourceDisk.WholeTrackCount,
            OptimalBitTiming = sourceDisk.OptimalBitTiming,
            IsWriteProtected = sourceDisk.IsWriteProtected,
            SavePolicy = newSavePolicy ?? _defaultSavePolicy,
            HighestWorkingVersion = 0,
            Notes = null,
            CreatedUtc = DateTime.UtcNow
        };

        lock (_collectionLock)
        {
            _diskImages.Add(cloneRecord);
        }

        // The cloned state becomes the new v0 (original) for the clone.
        _blobs.GetOrAdd(newId, _ => new ConcurrentDictionary<int, byte[]>())[0] = blob;
        MarkDirty();
        return Task.FromResult(newId);
    }

    // ─── Snapshot and residency helpers ───────────────────────────────────

    /// <summary>
    /// Produces a <see cref="ProjectSnapshot"/> from current in-memory state.
    /// </summary>
    internal ProjectSnapshot ToSnapshot()
    {
        lock (_collectionLock)
        {
            var blobs = new Dictionary<(long Id, int Version), byte[]>();

            foreach (var (diskId, versions) in _blobs)
            {
                foreach (var (ver, data) in versions)
                {
                    blobs[(diskId, ver)] = data;
                }
            }

            return new ProjectSnapshot
            {
                Metadata = _metadata,
                EmulatorOverrides = new Dictionary<string, string>(_emulatorOverrides),
                DisplayOverrides = new Dictionary<string, string>(_displayOverrides),
                ProjectSettings = new Dictionary<string, string>(_projectSettings),
                DiskImages = _diskImages.ToList(),
                Blobs = blobs,
                Attachments = _attachments.ToList(),
                AttachmentData = new Dictionary<long, byte[]>(_attachmentData),
                MountConfigurations = _mountConfigs.ToList(),
                Notes = _projectNotes
            };
        }
    }

    /// <summary>
    /// Serializes all dirty checked-out images into the in-memory blob dictionaries,
    /// ready for the next <see cref="IProjectStore.Save"/> call.
    /// <para>
    /// Respects <see cref="SavePolicy"/>: skips <c>DiscardChanges</c> and
    /// <c>PromptUser</c> disks silently.
    /// </para>
    /// </summary>
    private void SnapshotCheckedOutDisks()
    {
        foreach (var (id, image) in _checkedOutImages)
        {
            DiskImageRecord? disk;

            lock (_collectionLock)
            {
                disk = _diskImages.Find(d => d.Id == id);
            }

            if (disk == null) { continue; }
            if (disk.SavePolicy == SavePolicy.DiscardChanges) { continue; }
            if (disk.SavePolicy == SavePolicy.PromptUser) { continue; }
            if (!image.IsDirty) { continue; }

            // Always overwrite the current high-water mark.
            // If no working version exists yet, start at v1 and update the record.
            int activeVersion = disk.HighestWorkingVersion > 0 ? disk.HighestWorkingVersion : 1;

            if (disk.HighestWorkingVersion == 0)
            {
                disk.HighestWorkingVersion = 1;
            }

            var bytes = DiskBlobStore.SerializeSnapshot(image);
            _blobs.GetOrAdd(id, _ => new ConcurrentDictionary<int, byte[]>())[activeVersion] = bytes;
        }
    }

    /// <summary>
    /// Loads all non-resident blobs (across all versions of all disks) and all
    /// attachment data from the backing store into memory.
    /// Required before <see cref="SaveAsAsync"/> so the new store receives a complete
    /// copy of the project.
    /// No-op for ad hoc projects (nothing to load).
    /// </summary>
    private void EnsureAllBlobsResident()
    {
        if (_store == null)
        {
            return;
        }

        List<DiskImageRecord> disksCopy;
        List<AttachmentRecord> attachmentsCopy;

        lock (_collectionLock)
        {
            disksCopy = _diskImages.ToList();
            attachmentsCopy = _attachments.ToList();
        }

        foreach (var disk in disksCopy)
        {
            var versions = _blobs.GetOrAdd(disk.Id, _ => new ConcurrentDictionary<int, byte[]>());

            for (var ver = 0; ver <= disk.HighestWorkingVersion; ver++)
            {
                if (versions.ContainsKey(ver)) { continue; }

                var blob = _store.LoadBlob(disk.Id, ver);

                if (blob != null)
                {
                    versions[ver] = blob;
                }
            }
        }

        foreach (var attachment in attachmentsCopy)
        {
            if (_attachmentData.ContainsKey(attachment.Id)) { continue; }

            var data = _store.LoadAttachment(attachment.Id);

            if (data != null)
            {
                _attachmentData[attachment.Id] = data;
            }
        }
    }

    // ─── Private helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Ensures the given version blob is present in <c>_blobs</c>,
    /// loading it from the store if needed.
    /// </summary>
    private void EnsureBlobResident(long diskImageId, int version)
    {
        var versions = _blobs.GetOrAdd(diskImageId, _ => new ConcurrentDictionary<int, byte[]>());

        if (versions.ContainsKey(version)) { return; }

        if (_store == null)
        {
            throw new InvalidOperationException(
                $"Blob v{version} for disk image {diskImageId} is not resident and there is no backing store to load from.");
        }

        var blob = _store.LoadBlob(diskImageId, version);

        if (blob == null)
        {
            throw new InvalidOperationException(
                $"Blob v{version} for disk image {diskImageId} could not be loaded from the store.");
        }

        versions[version] = blob;
    }

    /// <summary>Returns the highest version number for the disk (0 = only original exists).</summary>
    private int GetHighestWorkingVersion(long diskImageId)
    {
        lock (_collectionLock)
        {
            var disk = _diskImages.Find(d => d.Id == diskImageId)
                       ?? throw new InvalidOperationException($"Disk image {diskImageId} not found.");
            return disk.HighestWorkingVersion;
        }
    }

    /// <summary>Selects the in-memory settings dictionary for the given scope.</summary>
    private ConcurrentDictionary<string, string> GetDictionary(SettingsScope scope) => scope switch
    {
        SettingsScope.EmulatorOverrides => _emulatorOverrides,
        SettingsScope.DisplayOverrides  => _displayOverrides,
        SettingsScope.ProjectSettings   => _projectSettings,
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null)
    };

    private void MarkDirty() => _projectDirty = true;
    private void MarkClean() => _projectDirty = false;

    private static ProjectMetadata BuildDefaultMetadata(string name) =>
        new(name, DateTime.UtcNow, ManifestConstants.CurrentSchemaVersion, "0.0.0");

    private static ConcurrentDictionary<string, string> BuildDefaultProjectSettings() =>
        new(new Dictionary<string, string>
        {
            ["DefaultSavePolicy"] = nameof(SavePolicy.OverwriteLatest)
        });
}
