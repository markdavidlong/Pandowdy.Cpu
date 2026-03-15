# Memory-Resident Project Refactoring Plan

**Date:** 2026-03-07 (revised 2026-03-08, 2026-03-08b, 2026-03-08c, 2026-03-08d, 2026-03-14)  
**Status:** Planned  
**Scope:** `Pandowdy.Project` module -- `SkilletProject`, `SkilletProjectManager`, `ProjectIOThread`

**Target Framework:** .NET 10 (`net10.0`). The entire solution will be upgraded from `net8.0` to `net10.0` as a prerequisite or companion step to this refactoring. .NET 10 is mature enough for development use (no release yet) and opens the door to additional code-generation options and language features. All `.csproj` files should target `net10.0`.

## Motivation

The current `SkilletProject` implementation treats SQLite as a live transactional database: every property read, settings query, disk image lookup, and mount configuration check is dispatched through a dedicated `ProjectIOThread` to a held-open `SqliteConnection`. This creates unnecessary complexity:

- A dedicated background thread (`ProjectIOThread`) with `BlockingCollection<IORequest>` FIFO queue
- Cross-thread marshaling for every operation (`EnqueueAsync`, `EnqueueSync`)
- Two separate code paths for in-memory (`:memory:`) vs file-based connections
- `VACUUM INTO` + connection swap ceremony for Save As
- `TransitionToFileAsync` with pre-swap/post-swap actions to move from ad hoc to file

### Key Insight: Storage Agnosticism

The real problem is deeper than "SQLite as live database." The project's operational model is **coupled to its storage format**. SQLite is essentially an ersatz filesystem -- no different in principle from a zip file or a directory of flat files. The project should be agnostic to whatever wrapper we use for persistence.

This leads to a two-layer architecture:

1. **`SkilletProject`** -- memory-resident operational model. All project state lives in dictionaries, lists, and byte arrays. This is what the application works with during a session.
2. **`IProjectStore`** -- storage abstraction. Knows how to read/write a project's data to/from some persistent medium. The project doesn't know or care what that medium is.

By starting with a **directory-based store** (flat files + JSON manifest), we can get the operational dynamics right without also debugging SQLite connection management. Once the project model is solid, we can implement a SQLite store, a zip store, or anything else -- and swap it in without touching the operational code.

The `_checkedOutImages` dictionary already follows the memory-resident pattern. This refactoring generalizes it to all project state.

**Single-project constraint:** Pandowdy can only have one project open at a time. There is no multi-project scenario. This simplifies the design: the project and its backing store are always 1:1.

## Architecture: Three Layers

```
┌─────────────────────────────────────────────────────────┐
│  ISkilletProject / ISkilletProjectManager               │
│  (Public contracts -- what the app works with)           │
├─────────────────────────────────────────────────────────┤
│  SkilletProject                                         │
│  (Memory-resident operational model -- all state here)   │
├─────────────────────────────────────────────────────────┤
│  IProjectStore                                          │
│  (Storage abstraction -- reads/writes project data)      │
│                                                         │
│  ┌──────────────────┐  ┌──────────────────┐             │
│  │DirectoryStore    │  │ SqliteStore      │  (future)   │
│  │(flat files + JSON│  │ ZipStore         │  (future)   │
│  │ manifest)        │  │ etc.             │             │
│  └──────────────────┘  └──────────────────┘             │
└─────────────────────────────────────────────────────────┘
```

### Layer 1: `ISkilletProject` -- Operational Interface

The public interface that the application uses. ViewModels, DiskImageStoreProxy, and controllers program against this. Most signatures are preserved; all `Task`/`Task<T>` return types remain for compatibility even though most in-memory operations are synchronous. `DiskImageStoreProxy` requires no changes -- it is a passthrough that delegates to `ISkilletProject`, and behavioral changes (e.g., `ReturnAsync` skipping serialization for `DiscardChanges` disks) are transparent to it.

**Interface changes (see Phase 5 for full details):**
- `GetSettingAsync` / `SetSettingAsync` -- first parameter changes from `string tableName` to `SettingsScope scope` enum. Callers must update: `SkilletProjectManagerTests.cs` (lines 421-423).
- Add `SaveAsAsync(IProjectStore newStore)`
- Remove `RegenerateWorkingCopyAsync`, `WriteWorkingBlobAsync`
- `FilePath` becomes nullable (`string?`)

### Layer 2: `SkilletProject` -- Memory-Resident Model

All project state lives here during a session. No connection held, no background thread, no file handles.

**Always resident:** Metadata, settings, mount configuration, project notes, and disk image *records* (the small metadata about each disk). These are negligible in size and always loaded eagerly.

**Project notes:** A built-in text/Markdown string (`_projectNotes`) that the user can edit to describe the project, track provenance, list references, etc. Always present -- defaults to an empty string for new projects. Unlike attachments, this is a guaranteed first-class field: every project has exactly one, it's always resident, and it appears directly in the manifest. The user doesn't have to create an attachment just to have a place for notes.

**Lazy-loadable:** Disk image *blobs* (the actual PIDI data -- 100KB-32MB each). When a backing store exists, blobs can be loaded on demand and flushed when no longer actively needed. Ad hoc projects (no backing store) must hold all blobs in memory.

```
SkilletProject (in-memory)
├── _store: IProjectStore?                          // null = ad hoc (no backing store)
├── _metadata: ProjectMetadata                      // name, created timestamp, versions
├── _projectNotes: string                           // user-editable text/markdown notes (always present)
├── _projectDirty: bool                             // volatile, same as today
├── _collectionLock: object                         // guards _diskImages, _mountConfigs, _attachments (all List<T>)
├── _defaultSavePolicy: SavePolicy                  // convenience copy from _projectSettings["DefaultSavePolicy"]
│
├── Settings (3 dictionaries) -- always resident
│   ├── _emulatorOverrides: ConcurrentDictionary<string, string>
│   ├── _displayOverrides: ConcurrentDictionary<string, string>
│   └── _projectSettings: ConcurrentDictionary<string, string>
│
├── Disk Image Library
│   ├── _diskImages: List<DiskImageRecord>          // always resident (metadata, incl. HighestWorkingVersion)
│   ├── _blobs: ConcurrentDictionary<long,          // per-disk --> per-version blob storage
│   │            ConcurrentDictionary<int, byte[]>>  // [diskId][version] --> PIDI data
│   │            // Residency signal: key present = blob is resident in memory.
│   │            // Key absent = blob exists in the store but is not loaded.
│   │            // Never store null values -- use TryRemove to evict, TryAdd to load.
│   └── _nextDiskId: long                           // 1 for new; max(ids)+1 from manifest
│
│   ID gaps should be closed at the point of deletion so they never persist.
│   If a gap survives (e.g., manual manifest edit), max(ids)+1 is the safe fallback.
│
├── Mount Configuration -- always resident
│   ├── _mountConfigs: List<MountConfiguration>     // slot/drive assignments
│   └── _nextMountId: long                          // 1 for new; max(mount ids)+1 from manifest
│
│   `MountConfiguration.AutoMount` preserves the mounted state from the last save:
│   if a disk was mounted when the project was saved, `AutoMount = true` flags it
│   for automatic remounting when the project is next loaded. On project open, the
│   loader checks `AutoMount` and issues `CheckOutAsync` for each flagged entry.
│
├── Attachments (Non-Disk Files)
│   ├── _attachments: List<AttachmentRecord>        // always resident (metadata only)
│   ├── _attachmentData: ConcurrentDictionary<long, byte[]>   // [attachmentId] --> blob
│   │            // Same residency convention as _blobs: key present = resident, absent = not loaded.
│   └── _nextAttachmentId: long                     // 1 for new; max(ids)+1 from manifest
│
└── Checked-Out Images (already memory-resident today)
    └── _checkedOutImages: ConcurrentDictionary<long, InternalDiskImage>
```

**Blob versioning:** Each disk image has version 0 (the immutable original import) and optionally versions 1, 2, 3, ... (working snapshots). **Version 0 is write-once by default:** it is set at import time and should not be overwritten in normal operation. However, a forced v0 overwrite is permitted if the caller explicitly provides replacement data (e.g., a re-import operation). When a forced v0 overwrite occurs, the implementation must emit a warning to stderr (e.g., `Console.Error.WriteLine($"Warning: overwriting immutable version 0 for disk image {diskImageId}")`) so the operation is auditable. A future UI confirmation dialog ("Are you sure?") may gate this path, but is not in scope for this refactoring. The `DiskImageRecord.HighestWorkingVersion` tracks the highest working version number. If `HighestWorkingVersion == 0`, the only data is the original (v0). Versioning is append-only -- versions are never deleted individually.

**Per-disk save policy:** Each `DiskImageRecord` has a `SavePolicy` property that controls what happens when the disk's working data is persisted:

```csharp
/// <summary>
/// Controls how a disk image's working data is saved.
/// Stored per-disk in the manifest / DiskImageRecord.
/// </summary>
public enum SavePolicy
{
    /// <summary>
    /// Overwrite the single latest working version in place.
    /// Automatic saves always target the highest-numbered working version.
    /// If manual snapshots have created versions v1, v2, v3, the next automatic
    /// save overwrites v3 (or creates v1 if no working versions exist yet).
    /// The version count never decreases -- snapshots are preserved as history.
    /// </summary>
    OverwriteLatest,

    /// <summary>
    /// Preserve all versions created by manual snapshots. Automatic saves
    /// overwrite the current high-water mark (same as <see cref="OverwriteLatest"/>).
    /// New versions are only created by explicit <c>CreateSnapshotAsync</c> calls.
    /// This is NOT a transaction log — it does not auto-increment on every save.
    /// </summary>
    AppendVersion,

    /// <summary>
    /// Prompt the user before saving. The UI must obtain confirmation
    /// before <c>SkilletProject</c> will call <see cref="IProjectStore.SaveBlob"/>.
    /// </summary>
    PromptUser,

    /// <summary>
    /// Discard all working changes. The disk image reverts to its last
    /// persisted state on return. No working version is ever written.
    /// </summary>
    DiscardChanges
}
```

**Defaults:** The application default is `OverwriteLatest`. Each project stores its own `DefaultSavePolicy` in the `ProjectSettings` dictionary (key: `"DefaultSavePolicy"`, value: the enum name as a string, e.g., `"OverwriteLatest"`). `SkilletProject` reads this into `_defaultSavePolicy` at load time for convenient access. This project-level default overrides the application default for newly imported disks in that project. Users can change it at any time via the project settings UI, and can still override per-disk.

### `SettingsScope` Enum

Replaces the string-based `SkilletConstants.Table*` names used by the SQLite implementation. Each value selects one of the three in-memory settings dictionaries:

```csharp
/// <summary>
/// Identifies which settings dictionary to read from or write to.
/// Replaces the string-based table names formerly in <c>SkilletConstants</c>.
/// </summary>
public enum SettingsScope
{
    /// <summary>Machine-level emulator overrides (CPU speed, memory model, etc.).</summary>
    EmulatorOverrides,

    /// <summary>Display-level overrides (color mode, scan-line effects, etc.).</summary>
    DisplayOverrides,

    /// <summary>Project-wide settings (DefaultSavePolicy, etc.).</summary>
    ProjectSettings
}
```

The mapping to in-memory state is:

| `SettingsScope` value | In-memory dictionary |
|-----------------------|---------------------|
| `EmulatorOverrides` | `_emulatorOverrides` |
| `DisplayOverrides` | `_displayOverrides` |
| `ProjectSettings` | `_projectSettings` |

**`DiscardChanges` behavior:** When a disk with `DiscardChanges` is returned via `ReturnAsync`, the working changes are silently dropped -- the in-memory blob is not updated, and no version is written to the store. The disk effectively resets to its last saved state the next time it is checked out.

> **Intentional behavioral change:** The current implementation always writes the working blob on `ReturnAsync` when `image.IsDirty` is true, regardless of `PersistWorking`. The `PersistWorking` filter only applies later in `SaveAsync` (project-level save). In the new implementation, `ReturnAsync` checks the disk's `SavePolicy` **before** serializing and skips the write entirely for `DiscardChanges` disks. This is a deliberate simplification -- the old behavior of writing data that would later be ignored was wasteful. The new code path is correct: `DiscardChanges` means no working version is ever created.

### Attachments (Non-Disk Files)

Projects can contain non-disk file types: disassembled source code, supplementary documentation (PDF, text), graphics screenshots, files extracted from disk images, and other artifacts. These are simple mutable binary blobs with metadata -- no versioning, no immutable-original semantics, no save policies. Attachments are overwritten in place.

```csharp
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
```

### Layer 3: `IProjectStore` -- Storage Abstraction

Each `IProjectStore` instance is **bound to a specific location** (directory path, SQLite file, etc.). The store knows its own path -- callers never pass it. Since Pandowdy only has one project open at a time, there is at most one store instance alive.

```csharp
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
    /// A forced v0 overwrite is permitted (e.g., re-import) but emits a warning to stderr.
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

/// <summary>
/// Well-known blob version constants.
/// </summary>
public static class BlobVersion
{
    /// <summary>Version 0: the immutable original import.</summary>
    public const int Original = 0;

    /// <summary>
    /// Resolves to the highest working version number,
    /// or <see cref="Original"/> if no working versions exist.
    /// </summary>
    public const int Latest = -1;
}

/// <summary>
/// Controls how a blob save operation is handled.
/// This is the low-level mode passed to <see cref="IProjectStore.SaveBlob"/>.
/// The per-disk <see cref="SavePolicy"/> determines which mode is used at save time.
/// </summary>
public enum BlobSaveMode
{
    /// <summary>
    /// Overwrite the current active (latest) working version in place.
    /// In normal operation this never targets version 0. A forced v0 overwrite
    /// (e.g., re-import) is permitted but emits a warning to stderr.
    /// </summary>
    OverwriteActive,

    /// <summary>Create a new version, preserving all previous versions.</summary>
    CreateNewVersion
}
```

**Serialization ownership:** `SkilletProject` owns blob serialization/deserialization via `DiskBlobStore`. `IProjectStore` is byte-agnostic -- it accepts and returns raw `byte[]` and never interprets PIDI format. This keeps format knowledge out of the storage layer.

`SkilletProject` maps from `SavePolicy` --> `BlobSaveMode` at **automatic** save time:

| `SavePolicy` | Resulting `BlobSaveMode` | Extra requirement |
|---|---|---|
| `OverwriteLatest` | `OverwriteActive` | None |
| `AppendVersion` | `OverwriteActive` | New versions created only by manual `CreateSnapshotAsync`, not by automatic save |
| `PromptUser` | Determined by user choice | `SkilletProject` must obtain user confirmation before calling `SaveBlob` |
| `DiscardChanges` | *(no save performed)* | Working data silently dropped |

### Manual Snapshot

The user can force a snapshot at any time, regardless of the disk's `SavePolicy`. This always uses `CreateNewVersion` -- the new version becomes the latest, and the disk's `SavePolicy` is unchanged. This works even for `OverwriteLatest` and `DiscardChanges` disks.

```
CreateSnapshotAsync(diskImageId):
  image = _checkedOutImages[diskImageId]            // must be checked out
  blob = SerializeSnapshot(image)                    // thread-safe: acquires SerializationLock
  newVersion = record.HighestWorkingVersion + 1
  _blobs[diskImageId][newVersion] = blob             // key presence = resident
  record.HighestWorkingVersion = newVersion
  MarkDirty()
  // SavePolicy is NOT changed -- subsequent automatic saves
  // still follow the disk's configured policy.
```

This is a user-initiated action (e.g., a toolbar button or context menu), not part of the automatic save flow. It gives the user an explicit "bookmark this state" capability without altering how the disk's day-to-day saves work.

### Clone Blob ("Save Blob As")

A disk's current working state can be cloned into a **new** disk image entry in the project, regardless of the source disk's `SavePolicy` (including `DiscardChanges`). This is the only way to preserve changes on a `DiscardChanges` disk.

> **Precondition:** The source disk must **not** be checked out (i.e., it must be unmounted/returned first). This avoids serialization races with the live emulator and eliminates mount-swap complexity. The clone reads from the already-serialized blob in `_blobs`. If the user wants to clone a mounted disk, they must unmount it first (which triggers `ReturnAsync` and serializes the current state).
>
> **Implementation note for generated code:** Add a `// TODO: Review -- CloneBlobAsNewDisk currently requires the disk to be unmounted. A future enhancement could support live cloning via SerializeSnapshot if the unmount requirement proves too disruptive to workflows.` comment at the precondition check.

```
CloneBlobAsNewDisk(sourceDiskId, newName, newSavePolicy?):
  if sourceDiskId in _checkedOutImages:
    throw InvalidOperationException("Disk must be unmounted before cloning")
  latestVersion = get highest version for sourceDiskId
  blob = _blobs[sourceDiskId][latestVersion]         // already serialized
  newId = _nextDiskId++
  newRecord = new DiskImageRecord(...)               // version 0 = the cloned data
    { SavePolicy = newSavePolicy ?? _defaultSavePolicy }
  _diskImages.Add(newRecord)
  _blobs[newId][0] = blob                           // becomes the new original
  MarkDirty()
```

**GUI behavior for clone:**
- If the source disk's `SavePolicy` is `DiscardChanges`, the GUI should prompt the user to choose the new disk's `SavePolicy` (since inheriting `DiscardChanges` for a disk the user just intentionally saved would be surprising).
- Otherwise, the new disk defaults to the project's `DefaultSavePolicy`.
- The user can always override in the prompt.

### Future Disk Library Management

The in-memory model is designed to support a comprehensive disk image management GUI beyond what is scoped here. Anticipated operations include:

- **Delete** -- remove a disk image and all its versions from the project
- **Rename** -- change a disk image's display name
- **Clone** -- duplicate a disk image (already scoped above as "Save Blob As")
- **Revert to version** -- restore a previous version as the active working state
- **Purge old versions** -- trim version history to reclaim storage
- **Re-import** -- replace the original (version 0) from a new source file
- **Export** -- write a version out to an external disk image file

These are not detailed here but the architecture (in-memory records + versioned blobs + per-disk metadata) is intended to accommodate them without structural changes. New operations will be pure in-memory manipulations on `_diskImages`, `_blobs`, and `_mountConfigs`, persisted through the same `IProjectStore` interface.

### Future: Specialized Attachment Types

The basic attachment CRUD (add, retrieve, update, delete) is in scope for this refactoring -- the `AttachmentRecord` model, `IProjectStore` attachment methods, in-memory state collections, and directory store layout are all defined above.

What remains **future work** is specialized treatment of specific attachment types. As particular categories mature (e.g., extracted files that track their source disk image and path, or disassembly listings linked to specific addresses), they can be promoted to their own first-class record types with richer metadata -- similar to how `DiskImageRecord` already carries import provenance, versioning, and save policy. Specialized versioning rules for attachments are also deferred.

The generic `AttachmentRecord` blob provides the forward path until that specialization is warranted. The key design constraint is that `IProjectStore` and `ProjectSnapshot` remain extensible -- adding new record/blob collections is a non-breaking additive change.

Stores are created by a factory:

```csharp
/// <summary>
/// Creates path-bound <see cref="IProjectStore"/> instances.
/// Registered in DI; the concrete implementation determines the storage format.
/// </summary>
public interface IProjectStoreFactory
{
    /// <summary>
    /// Opens an existing store at <paramref name="path"/>.
    /// Throws if the store doesn't exist or is invalid.
    /// </summary>
    IProjectStore Open(string path);

    /// <summary>
    /// Creates a new, empty store at <paramref name="path"/>.
    /// Throws if a store already exists at that location.
    /// </summary>
    IProjectStore Create(string path);
}
```

The factory is what gets registered in DI -- swapping storage formats is a one-line DI change:

```csharp
// Today:
services.AddSingleton<IProjectStoreFactory, DirectoryProjectStoreFactory>();

// Future:
services.AddSingleton<IProjectStoreFactory, SqliteProjectStoreFactory>();
```

The `ProjectManifest` is the lightweight data loaded eagerly on Open -- everything except blobs:

```csharp
/// <summary>
/// Lightweight project data loaded eagerly on Open.
/// Contains metadata, settings, disk image records, attachment records,
/// and mount configuration, but NOT disk image blobs or attachment data.
/// </summary>
public sealed class ProjectManifest
{
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
```

The `ProjectSnapshot` is the full bag of state passed to `Save` -- includes whatever blobs are currently resident:

```csharp
/// <summary>
/// Complete snapshot of project state for persistence.
/// Passed to <see cref="IProjectStore.Save"/>. Includes all metadata plus
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
```

### `DirectoryProjectStore` -- First Implementation

The initial implementation stores each project as a directory of flat files. This is easy to inspect, debug, and diff -- and avoids all SQLite complexity during development.

> **Directory naming convention:** The directory store is a temporary solution while we regroup for further development of a single-file `.skillet` format. During this interim period, project directories are named `{projectname}_skilletdir` (e.g., `MyProject_skilletdir`). The project name is derived by stripping the `_skilletdir` suffix from the directory name. When the single-file format is ready, projects will use `{projectname}.skillet` instead.

> **File picker:** While the directory-based store is active, the GUI must use a **directory picker** (folder browser) for Open/Save As dialogs instead of a file picker. File-picker dialogs filter by extension and cannot meaningfully select directories. When a future single-file backend replaces the directory store, the GUI should revert to a standard file picker with a `.skillet` extension filter.

```
MyProject_skilletdir/                  (directory)
├── manifest.json                      (all project metadata, settings, records, and config)
├── disks/
│   ├── 1_v0.pidi                      (disk 1, version 0 -- original import)
│   ├── 1_v1.pidi                      (disk 1, version 1 -- first working snapshot)
│   ├── 1_v2.pidi                      (disk 1, version 2 -- latest)
│   ├── 2_v0.pidi                      (disk 2, version 0 -- original, no working versions)
│   └── ...
└── attachments/
    ├── 1.dat                          (attachment 1 -- flat binary, any content type)
    ├── 2.dat                          (attachment 2)
    └── ...
```

**manifest.json** contains all project structure (everything except binary blobs):

```json
{
  "Disclaimer": "This project file may contain third-party data, which is protected by the copyrights of the original rights holder. The author(s) of Pandowdy and all associated parties do not assert any claim of ownership or assume any license or rights to third-party data contained within this project file.",
  "Notes": "",
  "Metadata": {
    "Name": "My Apple II Project",
    "CreatedUtc": "2026-03-08T12:00:00Z",
    "SchemaVersion": 1,
    "PandowdyVersion": "0.1.0"
  },
  "EmulatorOverrides": {},
  "DisplayOverrides": {},
  "ProjectSettings": {
    "DefaultSavePolicy": "OverwriteLatest"
  },
  "DiskImages": [
    {
      "Id": 1,
      "Name": "DOS 3.3 System Master",
      "OriginalFilename": "dos33.nib",
      "OriginalFormat": "Nib",
      "ImportSourcePath": "/home/user/disks/dos33.nib",
      "ImportedUtc": "2026-03-08T12:05:00Z",
      "WholeTrackCount": 35,
      "OptimalBitTiming": 32,
      "IsWriteProtected": false,
      "SavePolicy": "OverwriteLatest",
      "Notes": null,
      "HighestWorkingVersion": 2,
      "CreatedUtc": "2026-03-08T12:05:00Z"
    }
  ],
  "MountConfigurations": [
    { "Id": 1, "Slot": 6, "DriveNumber": 1, "DiskImageId": 1, "AutoMount": true },
    { "Id": 2, "Slot": 6, "DriveNumber": 2, "DiskImageId": null, "AutoMount": true }
  ],
  "Attachments": [
    {
      "Id": 1,
      "Name": "HELLO.BAS",
      "ContentType": "text/plain",
      "OriginalFilename": "HELLO.BAS",
      "SizeBytes": 1234,
      "CreatedUtc": "2026-03-08T13:00:00Z",
      "Notes": "Extracted from DOS 3.3 System Master"
    }
  ]
}
```

The `disclaimer` field is written by the store on every save but never read or interpreted by the program. It exists solely as a notice for anyone manually inspecting the file. The canonical text is exposed as a static constant so the GUI can display it if desired:

```csharp
public static class ManifestConstants
{
    /// <summary>
    /// Current schema version. LoadManifest must validate
    /// Metadata.SchemaVersion against this and throw if unsupported.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    public const string Disclaimer =
        "This project file may contain third-party data, which is protected by the "
        + "copyrights of the original rights holder. The author(s) of Pandowdy and all "
        + "associated parties do not assert any claim of ownership or assume any license "
        + "or rights to third-party data contained within this project file.";
}
```

**JSON serialization convention:** `System.Text.Json` default PascalCase property naming is used throughout -- JSON property names match C# property names exactly. No `JsonNamingPolicy.CamelCase`, no `[JsonPropertyName]` attributes. This is a deliberate choice for consistency between the C# model types and the serialized manifest.

**JSON serializer instance:** `DirectoryProjectStore` must use a single `static readonly JsonSerializerOptions` instance (or a source-generated `JsonSerializerContext`) rather than creating options per call. `System.Text.Json` caches type metadata on the options object; constructing a new instance each time bypasses the cache, hurts performance, and triggers the `CA1869` analyzer warning. The shared options instance **must** include `JsonStringEnumConverter` in its `Converters` collection so that all enum properties (e.g., `SavePolicy`) are serialized as human-readable strings rather than raw integers. Using a global converter -- rather than per-enum `[JsonConverter]` attributes -- ensures any future enum added to the model automatically receives string serialization, preventing silent round-trip failures.

**Why start here:**
- Human-readable: you can `cat manifest.json` to inspect project state
- Debuggable: individual PIDI blobs can be examined with hex editors
- No connection management, no threading, no journal modes
- Gets the operational dynamics right first, without storage-format distractions
- Easy to test (create temp directories, assert file contents)

**Why not stay here permanently:**
- A directory is not a single portable file (user expectation: "my project is one file I can email")
- No atomic writes (partial save could corrupt the directory)
- Many small files in `disks/` for large libraries



### Current Code to Delete

All of this goes away regardless of which store implementation we start with:

| Code | Reason |
|------|--------|
| `ProjectIOThread` class (bottom of SkilletProject.cs, ~100 lines) | No held-open connection |
| `IORequest` / `IORequest<T>` classes (~30 lines) | No IO queue |
| `EnqueueAsync<T>` / `EnqueueAsync` / `EnqueueSync<T>` helpers | No IO queue |
| `TransitionToFileAsync` method | No connection swap |
| `SwapConnectionAsync` on `ProjectIOThread` | Deleted with `ProjectIOThread` |
| In-memory (`:memory:`) vs file-based connection bifurcation | Ad hoc = empty model, no SQLite |
| `VACUUM INTO` ceremony in `SkilletProjectManager.SaveAsAsync` | Manager creates new store via factory --> `project.SaveAsAsync(newStore)` |
| `RegenerateWorkingCopyAsync` on `ISkilletProject` | SQLite-era holdover. "Revert to original" is subsumed by future "Revert to version" in Disk Library Management. Remove from interface. |
| `WriteWorkingBlobAsync` on `ISkilletProject` | Dead method (zero callers). Raw-stream blob writing is subsumed by `SaveDiskImageAsync` (serialized) and `IProjectStore.SaveBlob` (byte-level). Remove from interface. |

## Changes to Existing Types

### `DiskImageRecord` -- Updated Definition

The current `DiskImageRecord` has 14 properties. This refactoring adds 2 (`SavePolicy`, `HighestWorkingVersion`), removes 3 (`PersistWorking`, `WorkingDirty`, `ModifiedUtc`), and preserves the remaining 11 unchanged.

**Current --> New property delta:**

| Property | Change | Notes |
|----------|--------|-------|
| `Id` | Unchanged | |
| `Name` | Unchanged | |
| `OriginalFilename` | Unchanged | |
| `OriginalFormat` | Unchanged | |
| `ImportSourcePath` | Unchanged | |
| `ImportedUtc` | Unchanged | |
| `WholeTrackCount` | Unchanged | |
| `OptimalBitTiming` | Unchanged | |
| `IsWriteProtected` | Unchanged | |
| `PersistWorking` | **Removed** | Replaced by `SavePolicy`. See migration mapping below. |
| `Notes` | Unchanged | |
| `WorkingDirty` | **Removed** | Replaced by `HighestWorkingVersion > 0` for "has working data" semantics. See below. |
| `CreatedUtc` | Unchanged | |
| `ModifiedUtc` | **Removed** | Not needed; filesystem timestamp is sufficient. |
| `SavePolicy` | **Added** | Per-disk save policy enum. Replaces `PersistWorking`. Default: project's `DefaultSavePolicy`. |
| `HighestWorkingVersion` | **Added** | Highest working version number (0 = only original v0 exists). This is a high-water mark, not a count of distinct snapshots. |

**Updated definition (13 properties -- net loss of 1):**

```csharp
public sealed class DiskImageRecord
{
    public long Id { get; init; }
    public required string Name { get; set; }
    public string? OriginalFilename { get; init; }
    public required string OriginalFormat { get; init; }
    public string? ImportSourcePath { get; init; }
    public DateTime ImportedUtc { get; init; }
    public int WholeTrackCount { get; init; } = 35;
    public byte OptimalBitTiming { get; init; } = 32;
    public bool IsWriteProtected { get; set; }

    /// <summary>
    /// Per-disk save policy controlling how working data is persisted.
    /// Replaces the former <c>PersistWorking</c> bool.
    /// Default: inherited from the project's <c>DefaultSavePolicy</c> at import time.
    /// </summary>
    public SavePolicy SavePolicy { get; set; } = SavePolicy.OverwriteLatest;

    public string? Notes { get; set; }

    /// <summary>
    /// Number of working versions (1, 2, ...). Zero means only the original (v0) exists.
    /// Replaces the former <c>WorkingDirty</c> bool. UI code that previously checked
    /// <c>WorkingDirty</c> should check <c>HighestWorkingVersion > 0</c> instead.
    /// </summary>
    public int HighestWorkingVersion { get; set; }

    public DateTime CreatedUtc { get; init; }
}
```

**`PersistWorking` --> `SavePolicy` migration mapping:**

| Old `PersistWorking` value | New `SavePolicy` value |
|---|---|
| `true` (default) | `SavePolicy.OverwriteLatest` |
| `false` | `SavePolicy.DiscardChanges` |

This mapping is applied by any store that reads legacy data (e.g., a future `SqliteProjectStore` reading V1 schema rows).

**`WorkingDirty` --> `HighestWorkingVersion` migration mapping:**

| Old `WorkingDirty` value | New `HighestWorkingVersion` value |
|---|---|
| `false` | `0` |
| `true` | `1` (a single working version exists) |

UI bindings that previously used `WorkingDirty` (e.g., `IsVisible="{Binding WorkingDirty}"` in `MountFromLibraryDialog.axaml`) should be updated to bind to a `HasWorkingVersions` computed property or `HighestWorkingVersion > 0`.

### `ProjectMetadata` -- Updated Definition

The current `ProjectMetadata` is a positional record with 5 parameters:

```csharp
public sealed record ProjectMetadata(
    string Name, DateTime CreatedUtc, DateTime ModifiedUtc,
    int SchemaVersion, string PandowdyVersion);
```

This refactoring removes `ModifiedUtc` (filesystem timestamp is sufficient). Updated definition (4 parameters):

```csharp
public sealed record ProjectMetadata(
    string Name,
    DateTime CreatedUtc,
    int SchemaVersion,
    string PandowdyVersion);
```

Because this is a positional record, removing a constructor parameter is a structural change that impacts all construction sites (including `SkilletProjectManager.LoadMetadataAsync` and all tests that construct `ProjectMetadata`).

## Session Operations (No Storage Access)

Every operation during a session reads/writes in-memory collections directly:

| Operation | Current (IO thread --> SQLite) | New (in-memory) |
|-----------|------------------------------|-----------------|
| `HasDiskImages` | `EnqueueSync(SELECT COUNT(*))` | `_diskImages.Count > 0` |
| `GetSettingAsync(scope, key)` | `EnqueueAsync(SELECT value)` | `dict.TryGetValue(key, out val)` -- `scope` is a `SettingsScope` enum selecting the dictionary |
| `SetSettingAsync(scope, key, val)` | `EnqueueAsync(INSERT OR UPDATE)` | `dict[key] = val; MarkDirty()` -- `scope` is a `SettingsScope` enum selecting the dictionary |
| `GetDiskImageAsync(id)` | `EnqueueAsync(SELECT ... WHERE id)` | `_diskImages.First(d => d.Id == id)` |
| `GetAllDiskImagesAsync()` | `EnqueueAsync(SELECT *)` | `_diskImages.AsReadOnly()` |
| `ImportDiskImageAsync(path, name)` | `EnqueueAsync(INSERT + blob)` | Read source file from filesystem, call `DiskImageFactory.GetImporter(format).Import(...)` (format detection and import pipeline), serialize via `DiskBlobStore.Serialize` to PIDI `byte[]`, then add `DiskImageRecord` + v0 blob to in-memory collections, assign id. **Note:** this is not a pure in-memory operation -- it performs file I/O to read the source disk image. |
| `RemoveDiskImageAsync(id)` | `EnqueueAsync(DELETE)` | Remove from lists/dicts |
| `CheckOutAsync(id)` | `EnqueueAsync(SELECT blob)` | Load latest version blob, deserialize |
| `ReturnAsync(id, image)` | `EnqueueAsync(UPDATE blob)` | Serialize to active version blob if dirty; skip if `DiscardChanges` |
| `GetMountConfigurationAsync()` | `EnqueueAsync(SELECT *)` | `_mountConfigs.AsReadOnly()` |
| `SetMountAsync(slot, drive, id?)` | `EnqueueAsync(UPSERT)` | Replace-in-list or add in `_mountConfigs`. `MountConfiguration` is an immutable record -- replacement uses `with` to create a new instance (e.g., `existing with { DiskImageId = newId }`). The `Id` is **preserved** on replacement: a mount slot's identity does not change when the disk in the drive is swapped. New entries get the next `_nextMountId`. |
| `OpenOriginalBlobRead(id)` | `EnqueueSync(SELECT blob)` | `new MemoryStream(_blobs[(id, 0)])` |
| `OpenWorkingBlobRead(id)` | `EnqueueSync(SELECT COALESCE)` | `new MemoryStream(latest version blob)` |
| `LoadDiskImageAsync(id)` | `EnqueueAsync(SELECT --> Deserialize)` | Deserialize from latest version blob |
| `SaveDiskImageAsync(id, image)` | `EnqueueAsync(Serialize --> UPDATE)` | Serialize to active version blob |
| `CreateSnapshotAsync(id)` | *(new operation)* | Force-create a new version from checked-out state |
| `CloneBlobAsNewDiskAsync(id, name)` | *(new operation)* | Clone unmounted disk's blob --> new DiskImageRecord + v0 blob (source must not be checked out) |
| `GetProjectNotes()` | *(new operation)* | `_projectNotes` (always returns a string, never null) |
| `SetProjectNotes(text)` | *(new operation)* | `_projectNotes = text; MarkDirty()` |
| `GetDisclaimer()` | *(new operation)* | `ManifestConstants.Disclaimer` (static constant, no instance needed) |
| `AddAttachmentAsync(name, data)` | *(new operation)* | Assign id, add `AttachmentRecord` + data to in-memory collections |
| `GetAttachmentAsync(id)` | *(new operation)* | Return `AttachmentRecord`; lazy-load data from store if not resident |
| `GetAllAttachmentsAsync()` | *(new operation)* | `_attachments.AsReadOnly()` (metadata only, no blob data) |
| `UpdateAttachmentAsync(id, data)` | *(new operation)* | Overwrite data in-memory; update `SizeBytes` |
| `DeleteAttachmentAsync(id)` | *(new operation)* | Remove from `_attachments`, `_attachmentData`; mark dirty |

## Lifecycle

```
CreateAdHoc:
  new SkilletProject()  <-- empty collections, _store = null
  No storage touched.

Create(path):
  store = factory.Create(path)                  <-- creates new store at path
  new SkilletProject(store)  <-- empty collections + seed data
  store.Save(project.ToSnapshot())

Open(path):
  store = factory.Open(path)                    <-- opens existing store
  manifest = store.LoadManifest()               <-- metadata, settings, records, mounts
  SkilletProject.FromManifest(manifest, store)
  // Blobs NOT loaded yet -- lazy on first access

Save:
  SnapshotCheckedOutDisks()
  _store!.Save(project.ToSnapshot())            <-- writes resident blobs; non-resident left in store
  MarkClean()

`Save` writes ALL resident blobs unconditionally, including clean (unmodified) blobs. There is no per-blob dirty tracking -- the overhead of re-writing a clean blob (~140-230 KB) is negligible and not worth the complexity of tracking dirty state.

SnapshotCheckedOutDisks():
  for each (id, image) in _checkedOutImages:
    disk = _diskImages.First(d => d.Id == id)
    if disk.SavePolicy == DiscardChanges:
      continue                                        <-- skip: policy says discard
    if disk.SavePolicy == PromptUser:
      continue                                        <-- skip: requires prior user confirmation
    if image.IsDirty:
      activeVersion = disk.HighestWorkingVersion       <-- always overwrite current high-water mark
      bytes = DiskBlobStore.SerializeSnapshot(image)   <-- thread-safe snapshot serialization
      _blobs[id][activeVersion] = bytes               // key presence = resident

SaveAs(newPath):                                  <-- called on SkilletProjectManager
  newStore = factory.Create(newPath)               <-- manager creates the new store
  project.SaveAsAsync(newStore)                    <-- delegates to project with store in hand

SaveAsAsync(newStore):                             <-- called on SkilletProject (receives IProjectStore)
  EnsureAllBlobsResident()                         <-- must load everything for copy to new store
  SnapshotCheckedOutDisks()
  newStore.Save(ToSnapshot())                      <-- full write to new location
  _store?.Dispose()                                <-- release old store (if any)
  _store = newStore                                <-- swap to new store
  newName = strip "_skilletdir" suffix from directory name
  _metadata = _metadata with { Name = newName }   <-- records are immutable; replace
  MarkClean()

Close:
  Dispose()  <-- calls _store?.Dispose(); no-op for DirectoryProjectStore
```

## Blob Residency: Lazy Loading and Flushing

Once a project has a backing store (`_store != null`), disk image blobs don't need to stay in memory at all times. The architecture supports two complementary patterns:

### Lazy Loading (Load on Demand)

When a project is Opened, only the lightweight `ProjectManifest` is read -- metadata, settings, disk records, mount config. No blobs are loaded. The blob dictionaries start empty.

When a blob is actually needed (checkout, export, inspection), `SkilletProject` calls `_store.LoadBlob(...)` to bring it into memory on demand:

```
CheckOutAsync(diskImageId):
  if diskImageId in _checkedOutImages:
    throw InvalidOperationException              // already checked out -- GUI must prevent this
  latestVer = ResolveLatest(diskImageId)       // HighestWorkingVersion or 0
  if _blobs[diskImageId].ContainsKey(latestVer) == false:  // not resident
    blob = _store!.LoadBlob(diskImageId)       // default = BlobVersion.Latest
    _blobs[diskImageId][latestVer] = blob      // key presence = resident
  Deserialize blob --> InternalDiskImage
  _checkedOutImages[diskImageId] = image
```

For ad hoc projects (`_store == null`), all blobs are always resident -- there's nowhere to lazy-load from. Blobs added via `ImportDiskImageAsync` are placed directly in the in-memory dictionaries.

### Flushing (Reclaim Memory)

When a disk image is not checked out and the project has a backing store, the blob can be flushed (evicted from memory, written back to the store). There is no per-blob dirty tracking -- all resident blobs are written back unconditionally on flush, unless the disk's `SavePolicy` prohibits saving (e.g., `DiscardChanges`). Blobs are small enough (~140-230 KB each) that the overhead of re-writing a clean blob is negligible.

```
FlushBlob(diskImageId, version):
  if diskImageId in _checkedOutImages:
    return  // can't flush an active image
  if version == 0:
    return  // v0 is immutable and always available from the store; never flush/rewrite it
  disk = _diskImages.First(d => d.Id == diskImageId)
  if disk.SavePolicy != DiscardChanges:
    _store!.SaveBlob(diskImageId, _blobs[diskImageId][version], OverwriteActive)
  _blobs[diskImageId].TryRemove(version)               // evict from memory; key absence = not resident
```

**`BlobSaveMode` constraint:** `FlushBlob` always uses `OverwriteActive` -- it is a memory-reclamation operation that preserves the current state, not a user-initiated versioning action. `FlushBlob` skips version 0 entirely: v0 is immutable, already persisted in the store from import time, and never needs to be written back. This avoids any accidental overwrite of the original import data. `CreateNewVersion` is only used by `CreateSnapshotAsync`, which is the sole project-level operation that creates new versions. Bulk `Save` writes all resident blobs via the `ProjectSnapshot`; `SaveBlob` exists on the store interface for the flush path and for internal use by `Save` implementations.

Flushing is an optimization -- it is never required for correctness. The project works correctly whether all blobs are resident or none are. This means flushing can be added incrementally:
- **Phase 1:** Mark the architecture to support it but load eagerly. No flushing.
- **Later:** Add a policy (e.g., evict after N minutes idle, or keep at most M blobs resident).

Attachment data follows the same residency pattern as disk blobs -- lazy-loaded from the store on first access, flushable when not actively in use.

### SaveAs Requires Full Residency

`SaveAs` writes to a *new* store that has no pre-existing blobs. All blobs must be in memory before the write. The `EnsureAllBlobsResident()` step loads any non-resident blobs from the old store before writing the full snapshot to the new one.

**Scope of "all blobs":** `EnsureAllBlobsResident()` loads **all versions of all disks** (v0, v1, v2, ... for every disk image) plus all attachment data. The new store needs a complete copy of the entire project. In practice, blob sizes are insignificant on modern hardware -- each PIDI blob is ~140-230 KB, and even a large project with many disks and accumulated versions won't approach meaningful memory pressure. The simplicity of loading everything outweighs any hypothetical memory savings from selective loading.

### Summary Table

| Scenario | Blobs Resident? | Lazy Load? | Flush? |
|----------|----------------|------------|--------|
| Ad hoc project | Always | No (nowhere to load from) | No (nowhere to flush to) |
| Opened from store | On demand | Yes | Yes (when not checked out) |
| After Save | Can flush | Yes | Yes |
| Before SaveAs | Must ensure all | Load all first | No (need them for write) |

## Phase Plan

> **Git commit policy:** The user performs all git commits manually. The implementation agent must **never** execute `git commit`, `git add`, `git push`, `git stash`, or any other git-modifying commands without the user's express permission. The agent should notify the user when a phase reaches a good commit point (indicated by ✅ GIT COMMIT POINT markers below). Between commit points, do not prompt for a commit -- partial-phase work is not in a committable state unless noted otherwise.

### Phase 1: Define Storage Abstraction

New files:

| File | Purpose |
|------|---------|
| `Interfaces/IProjectStore.cs` | Path-bound storage read/write contract (bulk + blob-level + attachment) |
| `Interfaces/IProjectStoreFactory.cs` | Factory for creating/opening path-bound stores |
| `Models/ProjectManifest.cs` | Lightweight DTO for Open (metadata, settings, records -- no blobs) |
| `Models/ProjectSnapshot.cs` | Full DTO for Save (includes resident blobs and attachment data) |
| `Models/AttachmentRecord.cs` | Metadata for non-disk file attachments |
| `Models/SavePolicy.cs` | Per-disk save policy enum |
| `Models/BlobSaveMode.cs` | Low-level blob save mode enum (used by `IProjectStore.SaveBlob`) |
| `Models/BlobVersion.cs` | Well-known blob version constants (`Original`, `Latest`) |
| `Models/SettingsScope.cs` | Settings dictionary selector enum (replaces string table names) |
| `Constants/ManifestConstants.cs` | Static `CurrentSchemaVersion` and `Disclaimer` constants. `LoadManifest` validates `Metadata.SchemaVersion` against `CurrentSchemaVersion` and throws if unsupported. |

> **Design note -- single version number:** There is no separate `FormatVersion`. The directory-based store is a temporary/stopgap solution; no backwards-compatibility commitment exists for it. When a single-file backend (e.g., `SqliteProjectStore`) replaces it, the directory structure will be removed entirely. A single `SchemaVersion` on `ProjectMetadata` is sufficient.

> ✅ **GIT COMMIT POINT — Phase 1 complete.** All new files are additive (interfaces, models, enums, constants). No existing code is modified. The solution should compile cleanly. Suggested commit message: `feat(project): add storage abstraction interfaces and model types`

### Phase 2: Implement `DirectoryProjectStore`

New files:

| File | Purpose |
|------|---------|
| `Stores/DirectoryProjectStore.cs` | Path-bound flat-file directory store |
| `Stores/DirectoryProjectStoreFactory.cs` | Factory: `Open(path)` / `Create(path)` --> `DirectoryProjectStore` |

Each `DirectoryProjectStore` instance is bound to a directory path at construction time. The store is stateless between calls -- it holds no locks, file handles, or cached state. `Dispose()` is a no-op. Multiple instances may point to the same directory simultaneously (e.g., during `SaveAs` error recovery); the caller is responsible for not interleaving writes.
- `LoadManifest()`: reads `manifest.json` (metadata, settings, records, mounts -- no blobs, no attachment data)
- `LoadBlob(id, version)`: reads `disks/{id}_v{version}.pidi`; `BlobVersion.Latest` resolves to highest version file present
- `LoadAttachment(id)`: reads `attachments/{id}.dat`
- `Save(snapshot)`: writes `manifest.json` and all resident disk blobs and attachment data to directory
- `SaveBlob(id, data, mode)`: `OverwriteActive` overwrites `disks/{id}_v{latest}.pidi`; `CreateNewVersion` writes `disks/{id}_v{latest+1}.pidi` and updates manifest `HighestWorkingVersion`
- `SaveAttachment(id, data)`: writes `attachments/{id}.dat` (simple overwrite)
- `DeleteAttachment(id)`: deletes `attachments/{id}.dat`

Uses `System.Text.Json` for manifest serialization (PascalCase convention -- see `DirectoryProjectStore` section). PIDI blobs are raw binary files (same `DiskBlobStore.Serialize`/`Deserialize` format).

**Error handling:** `LoadManifest()` throws `InvalidOperationException` if `manifest.json` is malformed or missing required elements. `LoadBlob()` throws `FileNotFoundException` if a blob file that should exist (per manifest `HighestWorkingVersion`) is missing -- this indicates store corruption, not a non-existent version. `LoadAttachment()` also throws `FileNotFoundException` if an attachment record exists in the manifest but the corresponding file is missing on disk -- any discrepancy between the manifest and the files in the directory indicates a deeper problem and should not be silently tolerated. If version numbering has gaps (e.g., `1_v0.pidi` and `1_v2.pidi` exist but `1_v1.pidi` does not), the store renumbers the versions on load to remove gaps while preserving the original ordering. This self-healing handles the case where a blob file was manually deleted or lost.

**Manifest sync policy:** Any time a store operation changes on-disk items referenced in the manifest (including gap-healing renumbering), the manifest must be rewritten as close to atomically as practical. For gap-healing specifically: `LoadManifest()` detects gaps, renames the blob files to their new version numbers, and immediately rewrites `manifest.json` with the corrected `HighestWorkingVersion` values -- all before returning the manifest to the caller. The on-disk state and the manifest must not diverge, even briefly during normal operation.

> ✅ **GIT COMMIT POINT — Phase 2 complete.** `DirectoryProjectStore`, its factory, and `DirectoryProjectStoreTests` are all additive. No existing code is modified yet. The solution should compile and all Phase 2 tests should pass. Suggested commit message: `feat(project): implement DirectoryProjectStore with tests`

### Phases 3-5: Rewrite Core, Manager, and Interface (Atomic Commit)

> **Implementation note:** Phases 3, 4, and 5 must be implemented as a single atomic commit. Phase 3 removes methods that Phase 4 still references, and Phase 4 depends on the `SaveAsAsync` addition from Phase 5. Implementing them separately would leave the project in an uncompilable state.
>
> ⛔ **DO NOT COMMIT between Phases 3, 4, and 5.** The codebase will not compile in an intermediate state. Wait for all three phases plus Phase 6 test updates to be complete before committing.
>
> **UI bindings:** Any UI bindings referencing removed properties (`WorkingDirty`, `PersistWorking`) must also be updated in this commit. Known binding: `MountFromLibraryDialog.axaml` line 33 (`IsVisible="{Binding WorkingDirty}"`) -- update to bind to `HasWorkingVersions` or `HighestWorkingVersion > 0`. Audit all `.axaml` files for references to removed properties before committing.

#### Phase 3: Rewrite `SkilletProject` (Memory-Resident Core)

Replace the body of `SkilletProject`:

1. **Private constructor** -- initialize from `ProjectManifest` or empty (for ad hoc), with optional `IProjectStore`
2. **`static FromManifest(manifest, store)`** -- factory for Open (store-backed). Trusts that the manifest is valid (the store is responsible for well-formed data). Throws `InvalidOperationException` if anything unexpected is encountered (e.g., duplicate disk IDs, negative IDs, mount configs referencing non-existent disk IDs). No lenient recovery -- fail fast on corrupt data.
3. **`static CreateNew(name)`** -- factory for CreateAdHoc (no store)
4. **`static CreateNew(name, store)`** -- factory for Create (with store)
5. **`ToSnapshot()`** -- produces `ProjectSnapshot` from current in-memory state
6. **All interface methods** -- pure in-memory dictionary/list operations
7. **`Dispose()`** -- calls `_store?.Dispose()` (no-op for `DirectoryProjectStore`; releases resources for future store types)

`SkilletProject` holds `_store: IProjectStore?` -- null for ad hoc, non-null when file-backed. The store knows its own path, so `SkilletProject` exposes the path via `_store?.Path`.

Delete everything listed in "Current Code to Delete" above.

The `internal` `EnqueueAsync<T>` helper used by tests will be replaced by an `internal` test factory that creates a `SkilletProject` pre-populated with test data:

```csharp
/// <summary>
/// Creates a SkilletProject pre-populated with the given manifest data and blobs.
/// For use in unit tests only (internal, exposed via InternalsVisibleTo).
/// </summary>
internal static SkilletProject CreateForTest(
    ProjectManifest manifest,
    Dictionary<(long DiskId, int Version), byte[]>? blobs = null,
    Dictionary<long, byte[]>? attachmentData = null,
    IProjectStore? store = null)
```

This factory calls the private constructor and populates the in-memory collections directly from the supplied manifest and blob dictionaries. Tests that previously used `InsertMockDiskImageAsync` (which injected raw SQL) will instead build a `ProjectManifest` with the desired `DiskImageRecord` entries and pass pre-serialized PIDI blobs via the `blobs` parameter. The `store` parameter is null for ad hoc test scenarios and a real or mock `IProjectStore` when testing store-backed behavior.

#### Phase 4: Simplify `SkilletProjectManager`

Receives `IProjectStoreFactory` via constructor (DI). All methods delegate to `SkilletProject` + factory-created stores:

1. **`CreateAdHocAsync()`** -- `SkilletProject.CreateNew("untitled")`; no store, no factory call
2. **`CreateAsync(path, name)`** -- `factory.Create(path)` --> `SkilletProject.CreateNew(name, store)` --> `store.Save(snapshot)`
3. **`OpenAsync(path)`** -- `factory.Open(path)` --> `store.LoadManifest()` --> `SkilletProject.FromManifest(manifest, store)`
4. **`SaveAsAsync(path)`** -- `factory.Create(path)` --> `project.SaveAsAsync(newStore)` (project handles residency, snapshot, store swap)
5. **`CloseAsync()`** -- `Dispose()` current --> `CreateAdHocAsync()`

`SkilletProjectManager` no longer needs to know about SQLite, connection strings, or IO threads.

#### Phase 5: Update `ISkilletProject` Interface

- `string? FilePath` --> sourced from `_store?.Path`. Keep the name `FilePath` (not `StorePath`) to avoid unnecessary UI churn -- `MainWindowViewModel.cs` references `.FilePath` in 4+ locations.
- `bool IsAdHoc` --> `_store == null`. An ad hoc project is a new, unsaved project with no backing store. The old implementation checked `string.IsNullOrEmpty(_filePath) || _filePath == ":memory:"`; the new implementation is simply `_store == null`. This property drives 6+ UI branching decisions and the save-on-exit flow.
- `GetSettingAsync(string tableName, string key)` --> `GetSettingAsync(SettingsScope scope, string key)`. The first parameter changes from a raw string to the `SettingsScope` enum. This is a **breaking signature change**. Known callers that must be updated: `SkilletProjectManagerTests.cs` (lines 421-423: `GetSettingAsync("project_settings", "test_key")` --> `GetSettingAsync(SettingsScope.ProjectSettings, "test_key")`). Audit all `.cs` files for `GetSettingAsync` / `SetSettingAsync` calls before committing.
- `SetSettingAsync(string tableName, string key, string value)` --> `SetSettingAsync(SettingsScope scope, string key, string value)`. Same breaking change as above.
- `Task SaveAsync()` remains on the interface. It is a storage operation (not a session operation), which is why it does not appear in the Session Operations table. Implementation: `_store!.Save(ToSnapshot())`. Throws if `_store` is null (ad hoc projects must use SaveAs first).
- Add `Task SaveAsAsync(IProjectStore newStore)` -- accepts an already-created store (created by the manager). The manager keeps the `string path` overload on `ISkilletProjectManager`; `SkilletProject` never holds `IProjectStoreFactory`. This avoids giving the domain model infrastructure creation responsibility (a DDD code smell) while still eliminating the concrete-type cast.

**Ad hoc save-on-exit flow:** `SaveAsync` throws for ad hoc projects (no store). The UI's `OnClosingAsync` must check `HasUnsavedChanges` for all projects (including ad hoc -- per copilot instructions, do not skip the check). If the project is ad hoc (`IsAdHoc == true`, i.e. `_store == null`), route through `SaveAsAsync` (which presents a directory picker to create a store) rather than `SaveAsync`. If the project is file-backed (`IsAdHoc == false`), use `SaveAsync` directly. The user may decline to save, in which case changes are discarded and the window closes.
- Remove `RegenerateWorkingCopyAsync` -- SQLite-era holdover that NULLed the working blob. Its "revert to original" semantics are subsumed by the future "Revert to version" operation in Disk Library Management. Audit complete: the only caller is the test `RegenerateWorkingCopyAsync_DirtyDisk_ClearsWorkingBlob` in `SkilletProjectDiskTests.cs` -- no production code calls this method. Remove from interface and delete the test.
- Remove `WriteWorkingBlobAsync` -- dead method with zero callers. Takes a raw `Stream` and writes it as the working blob; functionality is covered by `SaveDiskImageAsync` (high-level, serialized) and `IProjectStore.SaveBlob` (low-level, byte-level). Remove from interface.

### Phase 6: Update Tests

> **Testing policy:** Tests must be written or updated in the same phase that creates or changes the code under test. Do not defer tests to a later phase. Running comprehensive tests throughout all phase development work catches regressions and bugs as early as possible.

**Phase 1 tests:** No new tests (interfaces only, no behaviour to test).

**Phase 2 tests:**

| Test | What It Verifies |
|------|-----------------|
| `DirectoryProjectStoreTests.cs` | Load/Save round-trip, manifest format, blob file naming, attachment CRUD, factory Open/Create |

**Phases 3-5 tests (atomic commit):**

| Test | What It Verifies |
|------|-----------------|
| `SkilletProjectTests.cs` | In-memory operations: settings CRUD, disk CRUD, attachment CRUD, mount config, dirty tracking |
| `ProjectSnapshotTests.cs` | `ToSnapshot`/`FromManifest` round-trip fidelity |

**Tests that change (Phases 3-5 commit):**

| Test File | Change |
|-----------|--------|
| `SkilletProjectDiskTests.cs` | `InsertMockDiskImageAsync` uses internal factory instead of `EnqueueAsync` SQL injection |
| `SkilletProjectManagerTests.cs` | Constructor takes `IProjectStoreFactory`; tests use `DirectoryProjectStoreFactory` with temp directories |
| `ProjectSettingsStoreTests.cs` | **May be removed or repurposed** -- `ProjectSettingsStore` is SQLite-specific; directory store uses JSON |

**Tests unchanged:**

| Test File | Reason |
|-----------|--------|
| `DiskBlobStoreTests.cs` | PIDI format unchanged |
| `SkilletSchemaManagerTests.cs` | Still valid for future `SqliteProjectStore` |

> ✅ **GIT COMMIT POINT — Phases 3-6 complete.** This is the big one: core rewrite, manager simplification, interface update, UI binding fixes, and all test updates in a single commit. The solution must compile cleanly and all tests must pass. Suggested commit message: `feat(project): rewrite SkilletProject to memory-resident model`

### Phase 7 (Future): `SqliteProjectStore`

When the operational model is solid and the directory store has proven the abstraction, implement `SqliteProjectStore` + `SqliteProjectStoreFactory`:

- Reuses `SkilletSchemaManager`, `ProjectSettingsStore`, V1 schema
- Schema migration required: add `SavePolicy` column (mapped from `PersistWorking`), add `HighestWorkingVersion` column (mapped from `WorkingDirty`). See "Changes to Existing Types" for migration mapping tables.
- `SqliteProjectStoreFactory.Open(path)`: returns a `SqliteProjectStore` bound to that `.skillet` file
- `LoadManifest()`: open read-only --> read all tables --> close --> return `ProjectManifest`
- `Save(snapshot)`: open --> write all tables in transaction --> close
- Same schema, same PIDI blobs, just behind the `IProjectStore` interface
- Can optionally defer to `ZipProjectStore` + `ZipProjectStoreFactory` for a single-file format without SQLite overhead

This is a drop-in replacement: change the DI registration from `DirectoryProjectStoreFactory` to `SqliteProjectStoreFactory` and everything else stays the same.

## Files Summary

### New Files

| File | Layer |
|------|-------|
| `Interfaces/IProjectStore.cs` | Path-bound storage abstraction (bulk + blob-level + attachment access) |
| `Interfaces/IProjectStoreFactory.cs` | Factory for creating/opening stores |
| `Models/ProjectManifest.cs` | Lightweight data loaded on Open (no blobs) |
| `Models/ProjectSnapshot.cs` | Full data-transfer object for Save (includes resident blobs and attachment data) |
| `Models/AttachmentRecord.cs` | Metadata for non-disk file attachments |
| `Models/SavePolicy.cs` | Per-disk save policy enum |
| `Models/BlobSaveMode.cs` | Low-level blob save mode enum |
| `Models/BlobVersion.cs` | Well-known blob version constants |
| `Models/SettingsScope.cs` | Settings dictionary selector enum (replaces string table names) |
| `Constants/ManifestConstants.cs` | Static `CurrentSchemaVersion` and `Disclaimer` constants |
| `Stores/DirectoryProjectStore.cs` | Path-bound flat-file implementation |
| `Stores/DirectoryProjectStoreFactory.cs` | Factory for directory stores |

### Modified Files

| File | Change |
|------|--------|
| `Services/SkilletProject.cs` | Rewrite: memory-resident model, no IO thread |
| `Services/SkilletProjectManager.cs` | Simplify: takes `IProjectStore`, no SQLite knowledge |
| `Interfaces/ISkilletProject.cs` | Add `SaveAsAsync(IProjectStore)`, nullable `FilePath`, change `GetSettingAsync`/`SetSettingAsync` first param from `string` to `SettingsScope` |
| `Models/ProjectMetadata.cs` | Remove `ModifiedUtc` parameter (4 params instead of 5) |
| `Models/DiskImageRecord.cs` | Add `SavePolicy` + `HighestWorkingVersion`, remove `PersistWorking` + `WorkingDirty` + `ModifiedUtc` (13 properties) |
| `Pandowdy.Project.csproj` | Can remove `Microsoft.Data.Sqlite` dependency (until SqliteStore). Also remove `System.Reactive` -- currently unused in this project. Re-add only if a concrete need arises; no preemptive declarations. |
| `Pandowdy/Program.cs` | Register `IProjectStoreFactory` --> `DirectoryProjectStoreFactory` in DI |
| `Pandowdy.UI/Controls/MountFromLibraryDialog.axaml` | Line 33: change `IsVisible="{Binding WorkingDirty}"` to `IsVisible="{Binding HasWorkingVersions}"` |

> **DI registration site:** `Program.cs` is the primary DI registration site. `UiBootstrap.cs` and `CapabilityAwareServiceCollection.cs` may participate secondarily, but all project-related DI changes should go in `Program.cs`. Avoid modifying `UiBootstrap.cs` for DI purposes if at all possible.

### Deferred (Not Deleted Yet)

| File | Reason |
|------|--------|
| `Constants/SkilletConstants.cs` | Needed by future `SqliteProjectStore` |
| `Services/SkilletSchemaManager.cs` | Needed by future `SqliteProjectStore` |
| `Migrations/V1_InitialSchema.cs` | Needed by future `SqliteProjectStore` |
| `Migrations/ISchemaMigration.cs` | Needed by future `SqliteProjectStore` |
| `Services/ProjectSettingsStore.cs` | Needed by future `SqliteProjectStore` |

These can be moved to a `Stores/Sqlite/` subfolder when `SqliteProjectStore` is implemented, or left in place.

### Deleted

| Code | Reason |
|------|--------|
| `ProjectIOThread` class | No held-open connection, no IO queue |
| `IORequest` / `IORequest<T>` classes | No IO queue |
| `TransitionToFileAsync` method | No connection swap |
| `EnqueueAsync` / `EnqueueSync` wrappers | No IO queue |
| `Interfaces/ISettingsResolver.cs` | Unimplemented stub; not part of this refactoring. Remove and re-introduce if/when settings resolution is designed. |
| `Services/SettingsResolver.cs` | Unimplemented stub (Phase 1 hardcoded defaults only). Remove alongside interface. |
| In-memory vs file-based connection bifurcation | Ad hoc = empty model |

## Risk Assessment

| Risk | Mitigation |
|------|------------|
| Memory usage for large disk libraries | Architecture supports lazy loading and flushing of blobs from backing store. Phase 1 loads eagerly; lazy behavior added incrementally. Even worst case (all resident), 100 images ~ 23MB -- trivial with 32GB RAM. |
| Directory store is not a single portable file | Known trade-off. Addressed when we implement `SqliteProjectStore` or `ZipProjectStore`. |
| Partial writes to directory could corrupt | Low risk for development use. `DirectoryProjectStore.Save` can write to temp dir + atomic rename for safety. |
| Thread safety of in-memory collections | All mutable dictionaries use `ConcurrentDictionary` for thread safety -- settings, blobs, attachment data, and checked-out images. `_projectDirty` remains `volatile`. This is an intentional design decision: these are not hot-path accesses, and the overhead is negligible compared to the safety gained. The current directory-based store is temporary; a future single-file backend may require background I/O that touches these collections. **Do not downgrade to `Dictionary` as a premature optimization** -- annotate this decision in code comments so future agents and developers understand the rationale. **Non-dictionary collections** (`_diskImages`, `_mountConfigs`, `_attachments` -- all `List<T>`) are accessed under a coarse-grained `lock (_collectionLock)` for any read or mutation. This is simpler than `ConcurrentBag` (which lacks indexed access) and cheaper than `ImmutableList` copy-on-write for small collections. The same `_collectionLock` object guards all three lists. |
| Save-during-emulation race | Same `SerializeSnapshot` lock strategy as today. Unchanged. |
| Breaking `ISkilletProject` consumers | Most signatures preserved. `Task`/`Task<T>` returns kept. Breaking changes: `GetSettingAsync`/`SetSettingAsync` parameter type change (`string` --> `SettingsScope`), `SaveAsAsync` addition, `RegenerateWorkingCopyAsync`/`WriteWorkingBlobAsync` removal. Mocks in loose mode don't break on new methods. |

## What This Eliminates

- `ProjectIOThread`, `IORequest`, `IORequest<T>` (~200 lines of threading infrastructure)
- `VACUUM INTO` + `SwapConnectionAsync` + `TransitionToFileAsync` ceremony
- In-memory vs file-based connection string bifurcation
- Runtime SQLite dependency (until `SqliteProjectStore` is implemented)
- Cross-thread marshaling for every property read
- The dedicated `Pandowdy.Project.IO` background thread

## What This Preserves

- Most `ISkilletProject` method signatures (callers unchanged) -- except: `GetSettingAsync`/`SetSettingAsync` (parameter type change from `string tableName` to `SettingsScope scope`), `SaveAsAsync(IProjectStore)` (added), `RegenerateWorkingCopyAsync` and `WriteWorkingBlobAsync` (removed). See Phase 5 for details.
- All `ISkilletProjectManager` method signatures (callers unchanged)
- `IDiskImageStore` contract (checkout/return lifecycle). Note: this interface lives in `Pandowdy.EmuCore/DiskII/IDiskImageStore.cs`, not in `Pandowdy.Project`. Future refactorings may move it to a standalone Disk II project.
- `DiskBlobStore` PIDI format -- serialize/deserialize unchanged
- `DiskImageFactory` -- import/export unchanged
- Dirty tracking semantics (`_projectDirty`, `MarkDirty`, `MarkClean`)
- Snapshot-under-lock serialization for mounted disks
- `DiskImageRecord`, `MountConfiguration`, `ProjectMetadata` model types (`DiskImageRecord` gains `SavePolicy` + `HighestWorkingVersion`, loses `PersistWorking` + `WorkingDirty` -- see "Changes to Existing Types"). `ProjectMetadata` loses `ModifiedUtc` (not needed; filesystem timestamp is sufficient). `DefaultSavePolicy` is stored in the `ProjectSettings` dictionary, not as a `ProjectMetadata` property.
- Project instance preservation across Save As (same object reference)
- SQLite schema infrastructure (deferred, not deleted) for future `SqliteProjectStore`
